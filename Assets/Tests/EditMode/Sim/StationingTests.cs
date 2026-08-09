using NUnit.Framework;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins <see cref="Stationing.HasBodyAt"/> — "who is standing here", which
    /// is the question the world strip draws one plate per — and
    /// <see cref="Stationing.WanderAgents"/>, the watch the wander post
    /// supplies.
    ///
    /// Deliberately not a yield test. Standing somewhere and earning there are
    /// different questions, and since the wander post became watch-only
    /// (2026-08-09) they come apart completely: a wandering body earns at no
    /// node at all, so a yield test would report the strip empty while someone
    /// plainly stands on it.
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
            // The case the strip's filter turns on. One body stands at one
            // post, and a roaming warden's post is the wander post — the nodes
            // are empty, whatever they are or aren't earning.
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
        public void WanderAgents_AWanderingWarden_WatchesAtTheBaseRate()
        {
            // Since the gather-share retired (2026-08-09) watching is the WHOLE
            // of what the wander post does, so the warden's branch of the watch
            // supply is the only thing their holding it still means. Nothing
            // else pins it: the observation fixtures staff the watch with
            // familiars, and the gather tests assert only that a wandering
            // warden picks nothing — every one of which stays green if this
            // branch is lost, leaving a warden who wanders wholly inert while
            // the watch card says nobody does.
            var state = new GameState();
            Warden.Wander(state);

            Assert.That(Stationing.WanderAgents(state, null), Is.EqualTo(1.0).Within(1e-9));
        }

        [Test]
        public void WanderAgents_NobodyRoaming_IsNoWatchAtAll()
        {
            var state = new GameState();
            Warden.Rest(state);

            Assert.That(Stationing.WanderAgents(state, null), Is.EqualTo(0.0).Within(1e-9));
        }

        [Test]
        public void WanderAgents_AWardenAtANode_WatchesNothing()
        {
            // The warden watches because they ROAM, not because they are
            // posted: a warden stood at a node is gathering, and the sites go
            // unwatched until someone takes the wander post.
            var state = new GameState();
            state.nodes.Add(new NodeState { id = "n1", resourceId = "berries" });
            Warden.Post(state, state.nodes[0]);

            Assert.That(Stationing.WanderAgents(state, null), Is.EqualTo(0.0).Within(1e-9));
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
