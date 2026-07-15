using System.Collections.Generic;
using BreakInfinity;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// The core game tick. Pure and deterministic: given a state, the content
    /// database and an elapsed time, it accrues gathered resources. Called both
    /// per-frame with a small delta and once on load with the offline delta.
    /// </summary>
    public static class Simulation
    {
        /// <summary>
        /// Advance the run by <paramref name="deltaSeconds"/>, accruing every
        /// active node's yield into the inventory. Non-positive deltas are a
        /// no-op so a paused or clock-skewed tick can't rewind progress.
        /// </summary>
        public static void Advance(GameState state, GameDataAsset data, double deltaSeconds)
        {
            if (state == null || data == null || deltaSeconds <= 0.0)
            {
                return;
            }

            var economy = data.economy;
            var burstMult = economy?.tending != null ? economy.tending.burstYieldMult : 1.0;

            foreach (var node in state.nodes)
            {
                if (node.familiarCount > 0)
                {
                    // Split the tick into its bursted and normal slices so a burst
                    // that expires part-way through a big delta (e.g. the offline
                    // catch-up tick) only pays out for the seconds it was live.
                    var burstSeconds = node.tendBurstRemaining > 0.0
                        ? System.Math.Min(deltaSeconds, node.tendBurstRemaining)
                        : 0.0;
                    var normalSeconds = deltaSeconds - burstSeconds;

                    var baseRate = YieldPerSecond(node, state, economy);
                    var gained = baseRate * (normalSeconds + burstSeconds * burstMult);
                    state.AddResource(node.resourceId, gained);
                }

                if (node.tendBurstRemaining > 0.0)
                {
                    node.tendBurstRemaining = System.Math.Max(0.0, node.tendBurstRemaining - deltaSeconds);
                }
            }
        }

        /// <summary>
        /// Apply a Tending burst to <paramref name="node"/> (the tap-to-tend
        /// interaction, design §5): for the next economy.tending.burstDurationSec
        /// seconds the node yields at burstYieldMult. Refreshes rather than stacks
        /// — a fresh tap resets the timer to the full duration. No-op when tending
        /// isn't configured. The design's brief Pristine-chance bump arrives with
        /// the quality system.
        /// </summary>
        public static void Tend(NodeState node, EconomyData economy)
        {
            if (node == null || economy?.tending == null)
            {
                return;
            }

            node.tendBurstRemaining = economy.tending.burstDurationSec;
        }

        /// <summary>
        /// Credit time away since the last session, per design doc §8:
        /// offlineEarn = rate · min(t, cap). Real elapsed time is capped at the
        /// offline cap (base cap hours, raised by any offlineCapHours upgrade
        /// owned; gear/Almanac bonuses arrive with their systems) and the
        /// offline rate multiplier is applied, then the tick runs once with
        /// that effective delta. Returns
        /// the capped wall-clock seconds credited (before the rate multiplier),
        /// so the welcome-back summary can report how much of the absence paid out.
        /// </summary>
        public static double AdvanceOffline(GameState state, GameDataAsset data, double realElapsedSeconds)
        {
            if (state == null || data == null || realElapsedSeconds <= 0.0)
            {
                return 0.0;
            }

            var capSeconds = Upgrades.OfflineCapHours(state, data) * 3600.0;
            var creditedSeconds = System.Math.Min(realElapsedSeconds, capSeconds);

            Advance(state, data, creditedSeconds * data.economy.offline.rateMultiplier);
            return creditedSeconds;
        }

        /// <summary>
        /// <see cref="AdvanceOffline"/> plus a report of what it paid out — the
        /// welcome-back sheet's data. Snapshots the inventory, runs the catch-up,
        /// and diffs, so the gains stay correct however the tick evolves.
        /// </summary>
        public static OfflineSummary AdvanceOfflineWithSummary(GameState state, GameDataAsset data, double realElapsedSeconds)
        {
            var summary = new OfflineSummary
            {
                realSeconds = System.Math.Max(0.0, realElapsedSeconds),
            };
            if (state == null)
            {
                return summary;
            }

            var before = new Dictionary<string, BigDouble>(state.resources);
            summary.creditedSeconds = AdvanceOffline(state, data, realElapsedSeconds);

            foreach (var pair in state.resources)
            {
                before.TryGetValue(pair.Key, out var had);
                var gained = pair.Value - had;
                if (gained > BigDouble.Zero)
                {
                    summary.gains[pair.Key] = gained;
                }
            }

            return summary;
        }

        /// <summary>
        /// Gather rate for a node, per design doc §8:
        /// yield/sec = familiars · tool/gear mult · (1 + masteryBonus·mastery) · global.
        /// Base rate is one unit per familiar per second; global folds in the
        /// permanent Verdure bonus (almanac / museum / fossil / boost factors
        /// arrive with their systems and multiply in here later).
        /// </summary>
        public static BigDouble YieldPerSecond(NodeState node, GameState state, EconomyData economy)
        {
            var masteryBonus = 1.0 + economy.mastery.yieldBonusPerLevel * node.masteryLevel;
            var global = 1.0 + economy.verdure.yieldBonusPerPoint * state.verdurePoints;

            return new BigDouble(node.familiarCount) * node.yieldMultiplier * masteryBonus * global;
        }
    }

    /// <summary>What one offline catch-up credited — the welcome-back sheet's data.</summary>
    public sealed class OfflineSummary
    {
        /// <summary>Wall-clock seconds the player was actually away (0 on clock skew).</summary>
        public double realSeconds;

        /// <summary>Capped wall-clock seconds the catch-up paid out for (0 when nothing was credited).</summary>
        public double creditedSeconds;

        /// <summary>Resources gained during the catch-up, keyed by resource id.</summary>
        public Dictionary<string, BigDouble> gains = new Dictionary<string, BigDouble>();
    }
}
