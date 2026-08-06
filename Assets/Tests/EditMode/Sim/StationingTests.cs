using NUnit.Framework;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins <see cref="Stationing.HasBodyAt"/> — "who is standing here", which
    /// is the question the world strip draws one plate per.
    ///
    /// Deliberately not a yield test. A wandering body pays a share into every
    /// node at once, so asking "does this ground earn" answers yes everywhere
    /// the moment anyone roams — wrong for a band that is supposed to show only
    /// the posts being worked.
    /// </summary>
    public class StationingTests
    {
        private static GameState WithFamiliarAt(string stationId)
        {
            var state = new GameState();
            state.roster.Add(new Familiar
            {
                id = state.NextFamiliarId(),
                speciesId = "test-species",
                stationId = stationId,
            });
            return state;
        }

        [Test]
        public void HasBodyAt_AStationedFamiliar_IsTrue()
        {
            var state = WithFamiliarAt("n1");

            Assert.That(Stationing.HasBodyAt(state, "n1"), Is.True);
        }

        [Test]
        public void HasBodyAt_ANodeNobodyStandsAt_IsFalse()
        {
            var state = WithFamiliarAt("n1");

            Assert.That(Stationing.HasBodyAt(state, "n2"), Is.False);
        }

        [Test]
        public void HasBodyAt_ARestingFamiliarsOldPost_IsFalse()
        {
            var state = WithFamiliarAt("n1");
            state.roster[0].stationId = null;

            Assert.That(Stationing.HasBodyAt(state, "n1"), Is.False);
        }

        [Test]
        public void HasBodyAt_TheWardensPost_IsTrue()
        {
            var state = new GameState { wardenPostNodeId = "n3" };

            Assert.That(Stationing.HasBodyAt(state, "n3"), Is.True);
        }

        [Test]
        public void HasBodyAt_TheWanderPost_FollowsWhoeverHoldsIt()
        {
            var wandering = new GameState();
            Warden.Wander(wandering);

            var familiar = WithFamiliarAt(Familiar.WanderStation);
            var nobody = new GameState();

            Assert.That(Stationing.HasBodyAt(wandering, Familiar.WanderStation), Is.True, "warden roaming");
            Assert.That(Stationing.HasBodyAt(familiar, Familiar.WanderStation), Is.True, "familiar roaming");
            Assert.That(Stationing.HasBodyAt(nobody, Familiar.WanderStation), Is.False, "nobody roaming");
        }

        [Test]
        public void HasBodyAt_AWanderingWarden_LeavesEveryNodeEmpty()
        {
            // The case the strip's filter turns on. A roaming warden works
            // every node a little, so a yield test would answer "worked" at all
            // of them and put the whole land back on the band. One body stands
            // at one post: the wander post.
            var state = new GameState();
            Warden.Wander(state);

            Assert.That(Stationing.HasBodyAt(state, "n1"), Is.False);
            Assert.That(Stationing.HasBodyAt(state, "n2"), Is.False);
            Assert.That(Stationing.HasBodyAt(state, Familiar.WanderStation), Is.True);
        }

        [Test]
        public void HasBodyAt_TheWardenAtCamp_HoldsNoPost()
        {
            var state = new GameState();
            Warden.Rest(state);

            Assert.That(Stationing.HasBodyAt(state, "n1"), Is.False);
        }

        [Test]
        public void HasBodyAt_NothingAndNonsense_IsFalseRatherThanThrowing()
        {
            var state = WithFamiliarAt("n1");

            Assert.That(Stationing.HasBodyAt(null, "n1"), Is.False);
            Assert.That(Stationing.HasBodyAt(state, null), Is.False);
            Assert.That(Stationing.HasBodyAt(state, string.Empty), Is.False);
        }

        [Test]
        public void HasBodyAt_AnUnpostedWarden_DoesNotMatchAnEmptyStationId()
        {
            // wardenPostNodeId is null at camp and Warden.PostNodeId normalises
            // empty to null — but a null == null match would report a body at
            // every unnamed post, so the empty-id guard has to stand on its own.
            var state = new GameState { wardenPostNodeId = string.Empty };

            Assert.That(Stationing.HasBodyAt(state, string.Empty), Is.False);
        }
    }
}
