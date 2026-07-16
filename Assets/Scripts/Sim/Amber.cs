using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// Amber (design §10): the premium currency, kept generous and
    /// player-initiated. This is the in-game economy layer — the free earn
    /// (dig sites surface it, rolled in <see cref="Excavation"/>) and the
    /// time-skip sink. IAP, rewarded ads, and the weekly Play Games cache
    /// arrive with the plugin pass. Amber survives Migration ("you keep …
    /// Amber") and can never fill a verse slot — the gate is not for sale,
    /// so no such code path exists.
    /// </summary>
    public static class Amber
    {
        /// <summary>Unity can't serialize a null section — a zeroed sink also reads as "no amber system".</summary>
        public static bool Configured(EconomyData economy)
        {
            return economy?.amber != null && economy.amber.timeSkipCostAmber > 0.0 && economy.amber.timeSkipHours > 0.0;
        }

        public static bool CanTimeSkip(GameState state, GameDataAsset data)
        {
            return Configured(data.economy) && state.amber >= data.economy.amber.timeSkipCostAmber;
        }

        /// <summary>
        /// Spend Amber to instantly credit timeSkipHours of production at the
        /// FULL live rate — unlike offline credit there is no cap and no rate
        /// multiplier; that's what makes it worth paying for. Returns the
        /// hours credited, or 0 when refused (unconfigured or short).
        /// </summary>
        public static double TryTimeSkip(GameState state, GameDataAsset data)
        {
            if (!CanTimeSkip(state, data))
            {
                return 0.0;
            }

            var amber = data.economy.amber;
            state.amber -= amber.timeSkipCostAmber;
            Simulation.Advance(state, data, amber.timeSkipHours * 3600.0);
            return amber.timeSkipHours;
        }
    }
}
