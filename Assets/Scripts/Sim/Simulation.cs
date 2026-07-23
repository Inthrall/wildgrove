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
        /// Longest slice a single tick is integrated over. Basket caps make big
        /// deltas path-dependent — gather and haul run concurrently in real
        /// time, so a 4-hour offline tick evaluated in one step would clamp a
        /// whole absence's gathering into one basketful. Sub-stepping keeps the
        /// catch-up honest: baskets fill, drain and overflow the way they
        /// would have live.
        /// </summary>
        private const double MaxStepSeconds = 1.0;

        /// <summary>
        /// Advance the run by <paramref name="deltaSeconds"/>: familiars gather
        /// into their node's basket, then carriers haul basket contents to the
        /// camp inventory (design §2 gather → haul → camp; only camp stock is
        /// spendable). Non-positive deltas are a no-op so a paused or
        /// clock-skewed tick can't rewind progress.
        /// </summary>
        public static void Advance(GameState state, GameDataAsset data, double deltaSeconds)
        {
            if (state == null || data == null || deltaSeconds <= 0.0)
            {
                return;
            }

            while (deltaSeconds > 0.0)
            {
                var step = System.Math.Min(deltaSeconds, MaxStepSeconds);
                Step(state, data, step);
                deltaSeconds -= step;
            }
        }

        private static void Step(GameState state, GameDataAsset data, double deltaSeconds)
        {
            var economy = data.economy;
            // Worn gear can strengthen the burst (the Cordage Wraps' +50%).
            var burstMult = economy?.tending != null
                ? economy.tending.burstYieldMult * (1.0 + Upgrades.TendingBurstBonus(state, data))
                : 1.0;
            var hauling = economy?.hauling;
            // The Store line's bought levels stretch every basket.
            var basketCapacity = hauling != null
                ? new BigDouble(hauling.basketCapacity * Buildings.BasketCapacityMultiplier(state, data))
                : BigDouble.Zero;

            foreach (var node in state.nodes)
            {
                // Split the tick into its bursted and normal slices so a burst
                // that expires part-way through a step only pays for the
                // seconds it was live.
                var burstSeconds = node.tendBurstRemaining > 0.0
                    ? System.Math.Min(deltaSeconds, node.tendBurstRemaining)
                    : 0.0;

                // Gate on the rate, not the flock count: a bonded gatherer
                // posted at an empty node (design §7) gathers alone.
                var baseRate = YieldPerSecond(node, state, data, economy);
                if (baseRate > BigDouble.Zero)
                {
                    var normalSeconds = deltaSeconds - burstSeconds;
                    var gained = baseRate * (normalSeconds + burstSeconds * burstMult);

                    // XP from every action (design §4) — credited on the gross
                    // gather, so a full basket loses the goods but not the
                    // practice. Mastery and the Compendium's lifetime record
                    // accrue alongside.
                    Skills.AddGatherXp(state, data, node.skill, gained);
                    Mastery.AddGatherXp(node, economy, gained);
                    Compendium.RecordGather(state, node.resourceId, gained);

                    if (hauling != null)
                    {
                        // Into the basket, clamped at capacity — a full basket
                        // overflows and the excess is lost (the §2 bottleneck).
                        // Timber-frame planters (design §3) stretch this node's
                        // basket beyond the global Store line.
                        var nodeCapacity = basketCapacity * Planters.BasketCapacityMultiplier(state, data, node);
                        node.basket = BigDouble.Min(node.basket + gained, nodeCapacity);
                    }
                    else
                    {
                        // Hauling not configured (hand-built test data): goods
                        // go straight to camp, the pre-carrier behaviour.
                        state.AddResource(node.resourceId, gained);
                    }
                }

                // The warden's own hands, at their post — always on (being
                // somewhere is the kickstart, not a tap surge), boosted while
                // a burst is live, and straight to camp with no carrier (they
                // pocket what they pick). This is how a bare node earns its
                // first own-resource gift (design §13 decision).
                var wardenRate = Warden.GatherPerSecond(state, economy, node);
                if (wardenRate > 0.0)
                {
                    var wardenGathered = new BigDouble(wardenRate *
                        (deltaSeconds - burstSeconds + burstSeconds * burstMult));
                    state.AddResource(node.resourceId, wardenGathered);
                    Skills.AddGatherXp(state, data, node.skill, wardenGathered);
                    Mastery.AddGatherXp(node, economy, wardenGathered);
                    Compendium.RecordGather(state, node.resourceId, wardenGathered);
                }

                if (node.tendBurstRemaining > 0.0)
                {
                    node.tendBurstRemaining = System.Math.Max(0.0, node.tendBurstRemaining - deltaSeconds);
                }

                if (node.pristineBonusRemaining > 0.0)
                {
                    node.pristineBonusRemaining = System.Math.Max(0.0, node.pristineBonusRemaining - deltaSeconds);
                }
            }

            if (hauling != null)
            {
                Haul(state, data, hauling, deltaSeconds);
            }

            // After the haul so goods that just reached camp can feed a batch —
            // sub-stepping keeps offline crafting batch-by-batch, like live play.
            Crafting.Advance(state, data, deltaSeconds);

            Observation.Advance(state, data, deltaSeconds);

            // Every familiar earns run XP at its post (design §4) — a trickle
            // that also feeds Renown (§9). Runs each sub-step so offline
            // catch-up credits it too.
            AccrueFamiliarXp(state, data, deltaSeconds);
        }

        private static void AccrueFamiliarXp(GameState state, GameDataAsset data, double deltaSeconds)
        {
            var famXp = data.economy?.familiarXp;
            if (famXp == null || famXp.xpPerSecond <= 0.0)
            {
                return;
            }

            // Roosts comfort (design §4): stationed familiars level faster per
            // bought level. Once per tick — it can't change mid-loop.
            var comfort = Buildings.ComfortXpMultiplier(state, data);
            foreach (var familiar in state.roster)
            {
                Familiars.AddPostXp(state, data, familiar, famXp.xpPerSecond, deltaSeconds, comfort);
            }
        }

        /// <summary>
        /// Move basket contents to camp in discrete deliveries — the "haul
        /// batch" design §5's quality rolls attach to (the roll itself arrives
        /// with the quality system). The fleet lands one delivery every
        /// tripSeconds / carrierCount (carriers evenly staggered on the trail);
        /// each delivery takes up to one load (carryCapacity · upgradeMult)
        /// from the fullest basket, so a batch is always a single resource.
        /// Average throughput matches the old continuous drain:
        /// carriers · carryCapacity · upgradeMult / tripSeconds. Trip progress
        /// only accrues while something is waiting — idle carriers sit at camp
        /// rather than banking trips against future goods.
        /// </summary>
        private static void Haul(GameState state, GameDataAsset data, EconomyData.HaulingData hauling, double deltaSeconds)
        {
            // Hauling is a post (design §2): the familiars holding the trail
            // carry, at their throughput traits; an unheld trail hauls nothing.
            // Bonded familiars are just roster members that persist, so they
            // count here when stationed.
            var carriers = Stationing.TrailCarriers(state, data);
            if (carriers <= 0.0)
            {
                return;
            }

            if (FullestBasket(state) == null)
            {
                state.haulTripProgress = 0.0;
                return;
            }

            var load = new BigDouble(hauling.baseCarryCapacity) * Upgrades.HaulCapacityMultiplier(state, data);
            var interval = hauling.tripSeconds / carriers;
            if (interval <= 0.0 || load <= BigDouble.Zero)
            {
                // Degenerate hand-built data (the validator rejects real
                // content like this) — don't spin the delivery loop.
                return;
            }

            state.haulTripProgress += deltaSeconds;
            while (state.haulTripProgress >= interval)
            {
                var node = FullestBasket(state);
                if (node == null)
                {
                    // Everything delivered mid-step; what's left of the
                    // progress is idle time at camp, not a banked trip.
                    state.haulTripProgress = 0.0;
                    return;
                }

                state.haulTripProgress -= interval;
                var moved = BigDouble.Min(node.basket, load);
                node.basket -= moved;
                Deliver(state, data, node, moved);
            }
        }

        /// <summary>
        /// Land one haul batch at camp with its design §5 quality roll: the
        /// whole delivery takes the rolled tier. Common goes to plain stock,
        /// Fine to the fine pool (sold at the bonus alongside plain stock),
        /// Pristine to the specimen pool (held for an explicit windfall sale —
        /// or, later, donation or offering).
        /// </summary>
        private static void Deliver(GameState state, GameDataAsset data, NodeState node, BigDouble amount)
        {
            switch (Quality.Roll(state, data, node))
            {
                case QualityTier.Pristine:
                    state.AddPristine(node.resourceId, amount);
                    Compendium.RecordPristine(state, node.resourceId, amount);
                    break;

                case QualityTier.Fine:
                    state.AddFine(node.resourceId, amount);
                    break;

                default:
                    state.AddResource(node.resourceId, amount);
                    break;
            }
        }

        /// <summary>The node with the most waiting in its basket (where the next carrier heads), or null when every basket is empty.</summary>
        private static NodeState FullestBasket(GameState state)
        {
            NodeState fullest = null;
            foreach (var node in state.nodes)
            {
                if (node.basket > BigDouble.Zero && (fullest == null || node.basket > fullest.basket))
                {
                    fullest = node;
                }
            }

            return fullest;
        }

        /// <summary>
        /// Apply a Tending burst to <paramref name="node"/> (the tap-to-tend
        /// interaction, design §5): for the next economy.tending.burstDurationSec
        /// seconds the node yields at burstYieldMult, and for
        /// pristineBonusDurationSec its haul batches roll Pristine at the
        /// tending-boosted chance. Both refresh rather than stack — a fresh tap
        /// resets each timer to its full duration. No-op when tending isn't
        /// configured.
        /// </summary>
        public static void Tend(NodeState node, EconomyData economy)
        {
            if (node == null || economy?.tending == null)
            {
                return;
            }

            node.tendBurstRemaining = economy.tending.burstDurationSec;
            node.pristineBonusRemaining = economy.tending.pristineBonusDurationSec;
        }

        /// <summary>
        /// <see cref="Tend(NodeState, EconomyData)"/> plus the deed record —
        /// tending is a warden act, and the Rite's deed slots count it
        /// (design §7). It no longer moves the warden's post — standing
        /// somewhere is an explicit assignment now (one body per post, §2).
        /// The driver calls this; the state-less overload stays for
        /// burst-maths tests.
        /// </summary>
        public static void Tend(GameState state, GameDataAsset data, NodeState node)
        {
            if (state == null || data == null || node == null || data.economy?.tending == null)
            {
                return;
            }

            Tend(node, data.economy);
            Rite.RecordDeed(state, data, "tend");
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
        /// welcome-back sheet's data. Snapshots the holdings (camp stock plus
        /// what's waiting in node baskets, so goods the carriers hadn't hauled
        /// yet still count as gained), runs the catch-up, and diffs — the gains
        /// stay correct however the tick evolves.
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

            var before = SnapshotHoldings(state);
            summary.creditedSeconds = AdvanceOffline(state, data, realElapsedSeconds);
            var after = SnapshotHoldings(state);

            foreach (var pair in after)
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
        /// Credit a haul a second time as camp stock — the reward for the
        /// OfflineBoost rewarded ad ("double it"). The bonus lands as
        /// base-quality resources in camp; the original catch-up already paid
        /// out in place, so this doubles the effective welcome-back gain.
        /// </summary>
        public static void GrantHaul(GameState state, Dictionary<string, BigDouble> gains)
        {
            if (state == null || gains == null)
            {
                return;
            }

            foreach (var pair in gains)
            {
                state.resources.TryGetValue(pair.Key, out var held);
                state.resources[pair.Key] = held + pair.Value;
            }
        }

        /// <summary>
        /// Camp stock plus basket contents, per resource — quality pools
        /// included, so a Fine or Pristine batch landed offline still counts
        /// as a welcome-back gain of its resource.
        /// </summary>
        private static Dictionary<string, BigDouble> SnapshotHoldings(GameState state)
        {
            var holdings = new Dictionary<string, BigDouble>(state.resources);
            AddPool(holdings, state.fineResources);
            AddPool(holdings, state.pristineResources);
            foreach (var node in state.nodes)
            {
                holdings.TryGetValue(node.resourceId, out var held);
                holdings[node.resourceId] = held + node.basket;
            }

            return holdings;
        }

        private static void AddPool(Dictionary<string, BigDouble> holdings, Dictionary<string, BigDouble> pool)
        {
            foreach (var pair in pool)
            {
                holdings.TryGetValue(pair.Key, out var held);
                holdings[pair.Key] = held + pair.Value;
            }
        }

        /// <summary>
        /// Gather rate for a node, per design doc §8:
        /// yield/sec = familiars · tool/gear mult · (1 + masteryBonus·mastery) · global.
        /// Base rate is one unit per familiar per second; global folds in the
        /// permanent Verdure bonus (almanac / museum / insect / boost factors
        /// arrive with their systems and multiply in here later).
        /// </summary>
        public static BigDouble YieldPerSecond(NodeState node, GameState state, EconomyData economy)
        {
            return YieldPerSecond(node, state, null, economy);
        }

        /// <summary>
        /// Data-aware overload: the stationed kith (design §2) does the
        /// gathering — assigned familiars at full rate, scaled by their traits
        /// (see <see cref="Stationing"/>). Resting familiars work nothing.
        /// </summary>
        public static BigDouble YieldPerSecond(NodeState node, GameState state, GameDataAsset data, EconomyData economy)
        {
            var masteryBonus = 1.0 + economy.mastery.yieldBonusPerLevel * Mastery.Level(node, economy);
            var global = 1.0 + economy.verdure.yieldBonusPerPoint * state.verdurePoints;
            var richness = Replanting.RichnessMultiplier(node, economy);
            // Cordage-trellis planters (design §3): a second yield lane at the node.
            var planters = Planters.NodeYieldMultiplier(state, data, node);
            var agents = Stationing.GatherAgentsAt(state, data, node);

            return new BigDouble(agents) * node.yieldMultiplier * masteryBonus * richness * planters * global;
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
