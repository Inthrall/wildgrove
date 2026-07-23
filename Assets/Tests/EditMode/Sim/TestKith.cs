namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Test helpers for the v0.11 kith model: stationing individual familiars in
    /// place of the old anonymous per-node/per-camp counts. Keeps the sim tests
    /// terse — "put 4 gatherers on this node" is one call.
    /// </summary>
    internal static class TestKith
    {
        /// <summary>
        /// Add <paramref name="count"/> familiars stationed at <paramref name="stationId"/>
        /// (a node id, "trail", or "wander"). Each gets its own made-up species —
        /// the collection rule is one familiar per species, and a save round trip
        /// dedupes duplicates, so staged crowds must not share one. Bypasses
        /// Roster.Station, so a hand-built crowd CAN share a post — the sim's
        /// aggregators tolerate it; only the mutation layer enforces one body
        /// per post.
        /// </summary>
        public static void Station(GameState state, string stationId, int count)
        {
            for (var i = 0; i < count; i++)
            {
                var id = state.NextFamiliarId();
                state.roster.Add(new Familiar
                {
                    id = id,
                    speciesId = "test-species-" + id,
                    stationId = stationId,
                });
            }
        }

        /// <summary>A fresh state with <paramref name="count"/> gatherers stationed at <paramref name="nodeId"/> and nothing else.</summary>
        public static GameState WithGatherers(string nodeId, int count)
        {
            var state = new GameState();
            Station(state, nodeId, count);
            return state;
        }

        /// <summary>
        /// Station one gatherer on the first node and one carrier on the trail
        /// — the shape the old seed kith gave every fresh run, staged
        /// explicitly now that a new game opens with the warden alone.
        /// </summary>
        public static void StageGathererAndCarrier(GameState state)
        {
            Station(state, state.nodes[0].id, 1);
            Station(state, Familiar.TrailStation, 1);
        }

        /// <summary>Un-station every familiar (all rest at camp) — the "no one working" setup.</summary>
        public static void ClearStations(GameState state)
        {
            foreach (var familiar in state.roster)
            {
                familiar.stationId = null;
            }
        }
    }
}
