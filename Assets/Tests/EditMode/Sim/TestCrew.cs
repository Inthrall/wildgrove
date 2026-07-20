namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Test helpers for the v0.11 crew model: stationing individual familiars in
    /// place of the old anonymous per-node/per-camp counts. Keeps the sim tests
    /// terse — "put 4 gatherers on this node" is one call.
    /// </summary>
    internal static class TestCrew
    {
        /// <summary>Add <paramref name="count"/> familiars stationed at <paramref name="stationId"/> (a node id, "trail", or "dig:{zone}").</summary>
        public static void Station(GameState state, string stationId, int count)
        {
            for (var i = 0; i < count; i++)
            {
                state.roster.Add(new Familiar
                {
                    id = state.NextFamiliarId(),
                    speciesId = "meadow-vole",
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

        /// <summary>Un-station every familiar (all wander) — the "no one working" setup.</summary>
        public static void ClearStations(GameState state)
        {
            foreach (var familiar in state.roster)
            {
                familiar.stationId = null;
            }
        }
    }
}
