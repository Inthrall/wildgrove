using System;
using System.Collections.Generic;

namespace Wildgrove.Game.Services
{
    /// <summary>
    /// Who is waiting on the billing connection, and how they are let go.
    /// <para>
    /// Billing connects lazily — nothing touches it until the player presses a
    /// buy or restore button — so between the press and the connection there is
    /// always a queue of callers holding a callback. The rule this type exists
    /// to keep is that <b>every one of them is answered</b>: on success, and
    /// equally on failure. A waiter released with <c>false</c> can report the
    /// fault; a waiter never released is a button that does nothing at all, with
    /// nothing in the UI to say why.
    /// </para>
    /// <para>
    /// A failed attempt is not remembered. The queue empties, no attempt is in
    /// flight, and the next caller starts a fresh one — a store that could not
    /// be reached on a train is very often reachable a minute later, and a flag
    /// saying otherwise would outlive the reason for it.
    /// </para>
    /// <para>
    /// Lives apart from <see cref="UnityIapStore"/> because that class cannot be
    /// constructed off a device: it reaches straight into Unity IAP. This is the
    /// part with the states in it, so this is the part with the tests.
    /// </para>
    /// </summary>
    public sealed class StoreConnection
    {
        private readonly List<Action<bool>> _waiting = new List<Action<bool>>();

        private bool _connected;
        private bool _attempting;

        /// <summary>True once the store has connected and entitlements are known.</summary>
        public bool IsConnected => _connected;

        /// <summary>Whether an attempt is in flight — nothing else should start a second one.</summary>
        public bool IsAttempting => _attempting;

        /// <summary>How many callers are queued behind the connection.</summary>
        public int Waiting => _waiting.Count;

        /// <summary>
        /// Queue <paramref name="resume"/> behind the connection, and answer
        /// whether the caller must now start an attempt.
        /// <para>
        /// Already connected: <paramref name="resume"/> is called with true
        /// before this returns, and the answer is false — there is nothing to
        /// start. Already attempting: the waiter joins the queue and the answer
        /// is again false. Otherwise the waiter is queued, this hands out the
        /// one attempt, and the caller owns finishing it with
        /// <see cref="Succeeded"/> or <see cref="Failed"/>.
        /// </para>
        /// </summary>
        public bool Wait(Action<bool> resume)
        {
            if (_connected)
            {
                resume?.Invoke(true);
                return false;
            }

            if (resume != null)
            {
                _waiting.Add(resume);
            }

            if (_attempting)
            {
                return false;
            }

            _attempting = true;
            return true;
        }

        /// <summary>The store connected: release the queue, and every later waiter runs immediately.</summary>
        public void Succeeded()
        {
            _connected = true;
            Release(true);
        }

        /// <summary>
        /// The attempt did not come up. Release the queue with the bad news so
        /// each waiting caller can report it, and leave nothing behind — the
        /// next press tries again from the top.
        /// </summary>
        public void Failed()
        {
            Release(false);
        }

        private void Release(bool connected)
        {
            _attempting = false;
            if (_waiting.Count == 0)
            {
                return;
            }

            // Take a copy and clear FIRST: a released waiter is entitled to ask
            // for the connection again (a retry, or a purchase begun from the
            // failure handler), and it must land in a clean queue rather than
            // one being iterated.
            var waiting = _waiting.ToArray();
            _waiting.Clear();

            foreach (var resume in waiting)
            {
                resume(connected);
            }
        }
    }
}
