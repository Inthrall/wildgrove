using NUnit.Framework;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins <see cref="Stationing.HasBodyAt"/> — "who is standing here", which
    /// is the question the world strip draws one plate per — and
    /// <see cref="Stationing.WatchAgentsAt"/>, the watch a site's own post
    /// supplies.
    ///
    /// Deliberately not a yield test. Standing somewhere and earning there are
    /// different questions, and since the watch became watch-only (2026-08-09)
    /// they come apart completely: a watching body earns at no node at all, so a
    /// yield test would report the strip empty while someone plainly stands on
    /// it.
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
        public void HasBodyAt_AWatchPost_FollowsWhoeverHoldsIt()
        {
            var watch = Familiar.WatchStation("old-growth-wood");
            var warden = new GameState();
            Warden.Watch(warden, "old-growth-wood");

            var familiar = WithFamiliarAt(watch);
            var nobody = new GameState();

            Assert.That(Stationing.HasBodyAt(warden, watch), Is.True, "warden watching");
            Assert.That(Stationing.HasBodyAt(familiar, watch), Is.True, "familiar watching");
            Assert.That(Stationing.HasBodyAt(nobody, watch), Is.False, "nobody watching");
        }

        [Test]
        public void HasBodyAt_AWatchingWarden_LeavesEveryNodeAndEveryOtherSiteEmpty()
        {
            // The case the strip's filter turns on. One body stands at one post,
            // and a watching warden's post is that one site's watch — the nodes
            // are empty whatever they are or aren't earning, and so is every
            // other site (the watch stopped being one post over the whole map on
            // 2026-08-12).
            var state = new GameState();
            Warden.Watch(state, "old-growth-wood");

            Assert.That(Stationing.HasBodyAt(state, "n1"), Is.False);
            Assert.That(Stationing.HasBodyAt(state, "n2"), Is.False);
            Assert.That(Stationing.HasBodyAt(state, Familiar.WatchStation("old-growth-wood")), Is.True);
            Assert.That(Stationing.HasBodyAt(state, Familiar.WatchStation("mistfen-marsh")), Is.False);
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
        public void WatchAgentsAt_AWatchingWarden_WatchesTheirOwnSiteAtTheBaseRate()
        {
            // Since the gather-share retired (2026-08-09) watching is the WHOLE
            // of what a watch post does, so the warden's branch of the watch
            // supply is the only thing their holding it still means. Nothing
            // else pins it: the observation fixtures staff the watch with
            // familiars, and the gather tests assert only that a watching warden
            // picks nothing — every one of which stays green if this branch is
            // lost, leaving a watching warden wholly inert while the watch card
            // says nobody is there.
            var state = new GameState();
            Warden.Watch(state, "old-growth-wood");

            Assert.That(Stationing.WatchAgentsAt(state, null, "old-growth-wood"), Is.EqualTo(1.0).Within(1e-9));
        }

        [Test]
        public void WatchAgentsAt_AWatchingWarden_WatchesNoOtherSite()
        {
            // The whole of the 2026-08-12 revision, in one assertion: a body
            // watches the place it stands and no other. It used to be that one
            // holder of one post sketched at every site on the map, so a player
            // who sent someone to watch a wood saw plates filling in at the
            // marsh and the river too.
            var state = new GameState();
            Warden.Watch(state, "old-growth-wood");

            Assert.That(Stationing.WatchAgentsAt(state, null, "mistfen-marsh"), Is.EqualTo(0.0).Within(1e-9));
        }

        [Test]
        public void WatchAgentsAt_NobodyWatching_IsNoWatchAtAll()
        {
            var state = new GameState();
            Warden.Rest(state);

            Assert.That(Stationing.WatchAgentsAt(state, null, "old-growth-wood"), Is.EqualTo(0.0).Within(1e-9));
        }

        [Test]
        public void WatchAgentsAt_AWardenAtANode_WatchesNothing()
        {
            // The warden watches because they hold a site's post, not because
            // they are posted at all: a warden stood at a node is gathering, and
            // the sites go unwatched until someone takes their watch.
            var state = new GameState();
            state.nodes.Add(new NodeState { id = "n1", resourceId = "berries" });
            Warden.Post(state, state.nodes[0]);

            Assert.That(Stationing.WatchAgentsAt(state, null, "old-growth-wood"), Is.EqualTo(0.0).Within(1e-9));
        }

        [Test]
        public void IsWatching_TellsAWatchPostFromAGround()
        {
            // The question the chrome and the strip ask of the warden, and the
            // roster rows ask of a companion: a watch post is a post, but it is
            // not a ground.
            var warden = new GameState();
            Warden.Watch(warden, "the-hollows");
            var familiar = WithFamiliarAt(Familiar.WatchStation("mistfen-marsh"));
            var gathering = WithFamiliarAt("n1");

            Assert.That(Warden.IsWatching(new GameState()), Is.False, "an unposted warden");
            Assert.That(Warden.IsWatching(warden), Is.True, "the warden at a site");
            Assert.That(familiar.roster[0].IsWatching, Is.True, "a companion at a site");
            Assert.That(familiar.roster[0].IsWatchingAt("mistfen-marsh"), Is.True, "and it knows which site");
            Assert.That(familiar.roster[0].IsWatchingAt("the-hollows"), Is.False, "but not another's");
            Assert.That(gathering.roster[0].IsWatching, Is.False, "a companion at a node is not watching");
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
