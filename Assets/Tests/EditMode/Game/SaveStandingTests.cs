using NUnit.Framework;
using Wildgrove.Game;

namespace Wildgrove.Game.Tests
{
    /// <summary>
    /// What the inside cover says about where the run is kept. Each line is a
    /// claim about the player's data, so the ones that must never be swapped
    /// are pinned: signed out must not read as kept, and a failed mirror must
    /// not read as a copy safely made.
    /// </summary>
    public class SaveStandingTests
    {
        private const long Now = 1_700_000_000_000L;

        [Test]
        public void WrittenLine_MomentsAfterAWrite_ReadsAsJustNow()
        {
            Assert.That(SaveStanding.WrittenLine(Now - 1_000L, Now), Does.Contain("just now"));
        }

        [Test]
        public void WrittenLine_LaterOn_CountsTheTime()
        {
            var line = SaveStanding.WrittenLine(Now - 125_000L, Now);

            Assert.That(line, Does.Contain("2m 5s"));
            Assert.That(line, Does.Contain("ago"));
        }

        [Test]
        public void WrittenLine_WithAClockThatWentBackwards_DoesNotReadAsTheFuture()
        {
            // A device clock corrected between the write and the reading would
            // otherwise produce a negative age; Duration floors at zero, and
            // "just now" is the honest reading of it.
            Assert.That(SaveStanding.WrittenLine(Now + 60_000L, Now), Does.Contain("just now"));
        }

        [Test]
        public void CloudLine_SignedOut_SaysTheRunIsOnThisDeviceAlone()
        {
            var line = SaveStanding.CloudLine(false, false, false);

            Assert.That(line, Does.Contain("not signed in"));
            Assert.That(line, Does.Contain("this device alone"));
        }

        [Test]
        public void CloudLine_SignedOut_OutranksAFailedWrite()
        {
            // Signed out is why the write failed. Saying both would read as two
            // faults, and only one of them is actionable.
            Assert.That(SaveStanding.CloudLine(false, true, false), Does.Contain("not signed in"));
        }

        [Test]
        public void CloudLine_WhenTheLastCopyDidNotLand_SaysSo()
        {
            var line = SaveStanding.CloudLine(true, true, false);

            Assert.That(line, Does.Contain("would not take"));
            Assert.That(line, Does.Not.Contain("copied up every time"));
        }

        [Test]
        public void CloudLine_AfterAdoptingAnotherDevicesRun_SaysWhereTheRunCameFrom()
        {
            // The reconcile used to swap the run in silence — this line, and the
            // margin note that goes with it, are that silence answered.
            Assert.That(SaveStanding.CloudLine(true, false, true), Does.Contain("another device"));
        }

        [Test]
        public void CloudLine_WhenAllIsWell_SaysWhereTheCopyGoes()
        {
            Assert.That(SaveStanding.CloudLine(true, false, false), Does.Contain("Play Games"));
        }
    }
}
