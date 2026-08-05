using NUnit.Framework;
using Wildgrove.Game.Services;

namespace Wildgrove.Game.Tests
{
    /// <summary>
    /// Pins the same promise <see cref="StoreConnection"/> keeps, one layer in:
    /// a purchase that was started is always answered. The in-flight set doubles
    /// as the buy button's gate, so an entry that outlives its order doesn't
    /// merely leak — it disables the button for the rest of the session with
    /// nothing on the page to explain it.
    /// </summary>
    public class PendingPurchasesTests
    {
        private const string Pack = "amber.pack.small";
        private const string Slot = "kith.slot";

        private PendingPurchases _sut;

        [SetUp]
        public void SetUp()
        {
            _sut = new PendingPurchases();
        }

        [Test]
        public void Begin_FirstFlow_IsAccepted()
        {
            Assert.That(_sut.Begin(Pack, _ => { }), Is.True);
            Assert.That(_sut.IsInFlight(Pack), Is.True);
        }

        [Test]
        public void Begin_SecondFlowForTheSameProduct_IsRefused()
        {
            _sut.Begin(Pack, _ => { });

            // Launching a second Play flow makes Google reject it as already
            // owned (non-consumable) or risks a double charge (consumable).
            Assert.That(_sut.Begin(Pack, _ => { }), Is.False);
            Assert.That(_sut.Count, Is.EqualTo(1));
        }

        [Test]
        public void Resolve_AnswersTheWaiterAndClearsTheGate()
        {
            StoreResult? got = null;
            _sut.Begin(Pack, result => got = result);

            _sut.Resolve(Pack, StoreResult.Purchased);

            Assert.That(got, Is.EqualTo(StoreResult.Purchased));
            Assert.That(_sut.IsInFlight(Pack), Is.False, "and the button works again");
        }

        [Test]
        public void Resolve_ForSomethingNeverStarted_IsAQuietNoOp()
        {
            // A confirmation can arrive for an order this session never began —
            // one fetched back from a previous launch. Not an error.
            Assert.DoesNotThrow(() => _sut.Resolve(Pack, StoreResult.Purchased));
        }

        [Test]
        public void ReleaseAll_AnswersEveryStrandedWaiterAsUnavailable()
        {
            StoreResult? pack = null;
            StoreResult? slot = null;
            _sut.Begin(Pack, result => pack = result);
            _sut.Begin(Slot, result => slot = result);

            _sut.ReleaseAll();

            Assert.That(pack, Is.EqualTo(StoreResult.Unavailable),
                "Unavailable, not Failed — nothing was refused, the store stopped being reachable");
            Assert.That(slot, Is.EqualTo(StoreResult.Unavailable));
            Assert.That(_sut.Count, Is.Zero);
        }

        [Test]
        public void ReleaseAll_LeavesTheGateOpenForARetry()
        {
            _sut.Begin(Pack, _ => { });

            _sut.ReleaseAll();

            Assert.That(_sut.Begin(Pack, _ => { }), Is.True,
                "the whole bug: a dropped connection used to leave this refusing forever");
        }

        [Test]
        public void ReleaseAll_AWaiterThatBuysAgainFromItsOwnHandler_LandsInACleanSet()
        {
            var retried = false;
            _sut.Begin(Pack, _ =>
            {
                // A released caller is entitled to press buy again immediately;
                // it must not be iterating the set it is adding to.
                retried = _sut.Begin(Pack, __ => { });
            });

            Assert.DoesNotThrow(() => _sut.ReleaseAll());
            Assert.That(retried, Is.True);
            Assert.That(_sut.Count, Is.EqualTo(1), "and the retry is the one left standing");
        }

        [Test]
        public void ReleaseAll_WithNothingWaiting_IsAQuietNoOp()
        {
            Assert.DoesNotThrow(() => _sut.ReleaseAll());
            Assert.That(_sut.Count, Is.Zero);
        }

        [Test]
        public void IsInFlight_NullProduct_IsFalse()
        {
            Assert.That(_sut.IsInFlight(null), Is.False);
            Assert.That(_sut.Begin(null, _ => { }), Is.False);
        }
    }
}
