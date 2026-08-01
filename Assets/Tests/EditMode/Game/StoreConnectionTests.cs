using NUnit.Framework;
using Wildgrove.Game.Services;

namespace Wildgrove.Game.Tests
{
    /// <summary>
    /// Pins the promise the billing connection makes to its callers: whoever
    /// waits is answered, whichever way it goes. The failure half is the one
    /// with history — a connect that threw used to be logged and nothing else,
    /// leaving the buy button waiting on a callback that could never come, with
    /// no way for the page to say so. That shape is what hid the UGS-unlinked
    /// bug for days, so these tests lead on it.
    /// </summary>
    public class StoreConnectionTests
    {
        private StoreConnection _sut;

        [SetUp]
        public void SetUp()
        {
            _sut = new StoreConnection();
        }

        [Test]
        public void Wait_WhenTheAttemptFails_ReleasesEveryWaiterWithFalse()
        {
            bool? first = null;
            bool? second = null;
            _sut.Wait(connected => first = connected);
            _sut.Wait(connected => second = connected);

            _sut.Failed();

            Assert.That(first, Is.False, "a waiter must hear about the failure, not wait forever");
            Assert.That(second, Is.False);
            Assert.That(_sut.Waiting, Is.Zero, "the queue is emptied by the release");
            Assert.That(_sut.IsConnected, Is.False);
        }

        [Test]
        public void Wait_TheFirstCallerStartsTheAttemptAndTheRestJoinIt()
        {
            Assert.That(_sut.Wait(_ => { }), Is.True, "nothing in flight — this caller connects");
            Assert.That(_sut.Wait(_ => { }), Is.False, "an attempt is already running");
            Assert.That(_sut.Wait(_ => { }), Is.False);
            Assert.That(_sut.Waiting, Is.EqualTo(3));
        }

        [Test]
        public void Wait_AfterAFailure_TheNextCallerTriesAgain()
        {
            // A failed connection is not remembered: a store out of reach on a
            // train is very often reachable a minute later, and a flag saying
            // otherwise would outlive the reason for it.
            _sut.Wait(_ => { });
            _sut.Failed();

            Assert.That(_sut.IsAttempting, Is.False, "nothing is in flight after a failure");
            Assert.That(_sut.Wait(_ => { }), Is.True, "so the next press starts a fresh attempt");
        }

        [Test]
        public void Wait_WhenTheAttemptSucceeds_ReleasesEveryWaiterWithTrue()
        {
            bool? first = null;
            bool? second = null;
            _sut.Wait(connected => first = connected);
            _sut.Wait(connected => second = connected);

            _sut.Succeeded();

            Assert.That(first, Is.True);
            Assert.That(second, Is.True);
            Assert.That(_sut.IsConnected, Is.True);
            Assert.That(_sut.Waiting, Is.Zero);
        }

        [Test]
        public void Wait_OnceConnected_RunsImmediatelyAndStartsNothing()
        {
            _sut.Succeeded();

            bool? connected = null;
            var startAttempt = _sut.Wait(answer => connected = answer);

            Assert.That(startAttempt, Is.False, "there is nothing to connect");
            Assert.That(connected, Is.True, "and no reason to make the caller wait");
        }

        [Test]
        public void Failed_AWaiterThatAsksAgainFromItsFailureHandler_GetsACleanQueue()
        {
            // A released waiter is entitled to ask again — a retry, or a purchase
            // begun from the failure handler. It must land in an empty queue
            // rather than one mid-release, or its own waiter is dropped.
            var retryStartedAttempt = false;
            bool? retryAnswer = null;

            _sut.Wait(connected =>
            {
                retryStartedAttempt = _sut.Wait(answer => retryAnswer = answer);
            });

            _sut.Failed();

            Assert.That(retryStartedAttempt, Is.True, "the re-ask owns the next attempt");
            Assert.That(_sut.Waiting, Is.EqualTo(1), "and its waiter is queued, not swallowed");

            _sut.Succeeded();
            Assert.That(retryAnswer, Is.True, "so the second attempt reaches it");
        }

        [Test]
        public void Failed_MidSessionAfterConnecting_LeavesTheConnectionStanding()
        {
            // The disconnect callback fires both for a connection that never came
            // up and for one dropped later. Only the first has callers waiting;
            // the second must not un-know entitlements already read.
            _sut.Succeeded();

            _sut.Failed();

            Assert.That(_sut.IsConnected, Is.True);
        }

        [Test]
        public void Wait_WithNoCallback_StillStartsTheAttempt()
        {
            // Initialise(null) at startup: nobody to answer, but the connection
            // still has to be made.
            Assert.That(_sut.Wait(null), Is.True);
            Assert.That(_sut.Waiting, Is.Zero);
            Assert.That(_sut.IsAttempting, Is.True);
        }
    }
}
