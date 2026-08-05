using NUnit.Framework;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins the clock ratchet. The rule is one line — a reading is never below
    /// the highest one the run has seen — but the thing it protects is every
    /// Amber cooldown and the whole offline credit, so the wind-forward-then-back
    /// cycle is asserted end to end rather than left to inference from the
    /// getter.
    /// </summary>
    public class ClockGuardTests
    {
        private const long Hour = 3600_000L;

        [Test]
        public void Now_FirstReading_PassesThroughAndSetsTheMark()
        {
            var state = new GameState();

            Assert.That(ClockGuard.Now(state, 1_000L), Is.EqualTo(1_000L));
            Assert.That(state.clockHighWaterUnixMs, Is.EqualTo(1_000L));
        }

        [Test]
        public void Now_MovingForward_PassesThroughAndRaisesTheMark()
        {
            var state = new GameState();
            ClockGuard.Now(state, 1_000L);

            Assert.That(ClockGuard.Now(state, 5_000L), Is.EqualTo(5_000L));
            Assert.That(state.clockHighWaterUnixMs, Is.EqualTo(5_000L));
        }

        [Test]
        public void Now_ClockWoundBack_ReadsTheMarkAndDoesNotLowerIt()
        {
            var state = new GameState();
            ClockGuard.Now(state, 10 * Hour);

            Assert.That(ClockGuard.Now(state, 2 * Hour), Is.EqualTo(10 * Hour),
                "a backwards clock is not seen at all");
            Assert.That(state.clockHighWaterUnixMs, Is.EqualTo(10 * Hour),
                "and it certainly must not drag the mark down with it");
        }

        [Test]
        public void Now_WindForwardThenBack_BuysNoExtraTime()
        {
            // The whole point, in one test. Real time is hour 1. The player winds
            // the clock to hour 25, takes whatever that pays, and winds it back.
            var state = new GameState();
            var honest = ClockGuard.Now(state, 1 * Hour);
            var cheated = ClockGuard.Now(state, 25 * Hour);

            // Wound back to real time — and then real time genuinely passes.
            var afterWindBack = ClockGuard.Now(state, 1 * Hour);
            var anHourLater = ClockGuard.Now(state, 2 * Hour);
            var muchLater = ClockGuard.Now(state, 24 * Hour);

            Assert.That(honest, Is.EqualTo(1 * Hour));
            Assert.That(cheated, Is.EqualTo(25 * Hour), "the wind forward pays out — once");
            Assert.That(afterWindBack, Is.EqualTo(25 * Hour));
            Assert.That(anHourLater, Is.EqualTo(25 * Hour));
            Assert.That(muchLater, Is.EqualTo(25 * Hour),
                "and then buys nothing at all until real time catches the mark up: "
                + "the day was spent, not minted");
        }

        [Test]
        public void Now_NullState_PassesThrough()
        {
            Assert.That(ClockGuard.Now(null, 42L), Is.EqualTo(42L));
        }

        [Test]
        public void IsBehind_OnlyWhileTheClockReadsBeforeTheMark()
        {
            var state = new GameState();
            ClockGuard.Now(state, 10 * Hour);

            Assert.That(ClockGuard.IsBehind(state, 9 * Hour), Is.True);
            Assert.That(ClockGuard.IsBehind(state, 10 * Hour), Is.False);
            Assert.That(ClockGuard.IsBehind(state, 11 * Hour), Is.False);
            Assert.That(ClockGuard.IsBehind(null, 0L), Is.False);
        }

        [Test]
        public void IsBehind_DoesNotAdvanceTheMark()
        {
            var state = new GameState();
            ClockGuard.Now(state, 5 * Hour);

            ClockGuard.IsBehind(state, 50 * Hour);

            Assert.That(state.clockHighWaterUnixMs, Is.EqualTo(5 * Hour),
                "asking the question must not be a way to move the answer");
        }

        [Test]
        public void BehindByMs_IsTheWaitBackToTheMark()
        {
            var state = new GameState();
            ClockGuard.Now(state, 10 * Hour);

            Assert.That(ClockGuard.BehindByMs(state, 7 * Hour), Is.EqualTo(3 * Hour));
            Assert.That(ClockGuard.BehindByMs(state, 12 * Hour), Is.Zero);
            Assert.That(ClockGuard.BehindByMs(null, 0L), Is.Zero);
        }
    }
}
