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
        /// Longest slice a single tick is integrated over. Timed processes
        /// (tincture buffs, tend bursts, crafting batches) make big deltas
        /// path-dependent, so a 4-hour offline tick evaluated in one step
        /// would mis-clock them. Sub-stepping keeps the catch-up honest:
        /// buffs lapse and batches land the way they would have live.
        /// </summary>
        private const double MaxStepSeconds = 1.0;

        /// <summary>
        /// Advance the run by <paramref name="deltaSeconds"/>: familiars gather
        /// at their posts and the day's pickings land at camp in periodic
        /// deliveries (design §2 gather → camp; only camp stock is spendable).
        /// Nothing is ever lost on the way — the delivery cadence exists so
        /// quality rolls attach to discrete batches, not to keep score.
        /// Non-positive deltas are a no-op so a paused or clock-skewed tick
        /// can't rewind progress.
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
            // Tincture buffs tick down first, so a buff that lapses this step
            // stops paying before the gather below reads the multipliers —
            // sub-stepping means offline catch-up honours the same clock.
            Tinctures.Advance(state, data, deltaSeconds);

            var economy = data.economy;
            // Worn gear can strengthen the burst (the Cordage Wraps' +50%).
            var burstMult = economy?.tending != null
                ? economy.tending.burstYieldMult * (1.0 + Upgrades.TendingBurstBonus(state, data))
                : 1.0;
            var delivery = economy?.delivery;

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

                    // XP from every action (design §4) — Mastery and the
                    // Compendium's lifetime record accrue alongside.
                    Skills.AddGatherXp(state, data, node.skill, gained);
                    Mastery.AddGatherXp(node, economy, gained);
                    Compendium.RecordGather(state, node.resourceId, gained);

                    if (delivery != null)
                    {
                        // The day's pickings pool at the node until the next
                        // delivery lands them at camp as one quality-rolled
                        // batch — a cadence, not a cap. Nothing overflows and
                        // nothing is lost.
                        node.basket += gained;
                    }
                    else
                    {
                        // Deliveries not configured (hand-built test data):
                        // goods go straight to camp, un-batched.
                        state.AddResource(node.resourceId, gained);
                    }
                }

                // The warden's own hands, at their post — always on (being
                // somewhere is the kickstart, not a tap surge), boosted while
                // a burst is live, and straight to camp with no carrier (they
                // pocket what they pick). This is how a bare node earns its
                // first own-resource gift (design §13 decision).
                var wardenRate = Warden.GatherPerSecond(state, data, economy, node);
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

                if (node.choiceBonusRemaining > 0.0)
                {
                    node.choiceBonusRemaining = System.Math.Max(0.0, node.choiceBonusRemaining - deltaSeconds);
                }
            }

            if (delivery != null)
            {
                DeliverPending(state, data, delivery, deltaSeconds);
            }

            // After the deliveries so goods that just reached camp can feed a batch —
            // sub-stepping keeps offline crafting batch-by-batch, like live play.
            Crafting.Advance(state, data, deltaSeconds);

            Observation.Advance(state, data, deltaSeconds);

            // Every familiar earns run XP at its post (design §4) — a trickle
            // that also feeds Renown (§9). Runs each sub-step so offline
            // catch-up credits it too.
            AccrueFamiliarXp(state, data, deltaSeconds);

            // The sim clock cursor walks with the step, AFTER the step has been
            // evaluated at its start time — this is what carries a tide edge
            // (design §15) through an offline catch-up at the exact second it
            // would have passed live. Whole-second offline slices make the
            // rounding exact there; live play re-stamps every frame, so the
            // fractional rounding never accumulates.
            if (state.simNowUnixMs > 0L)
            {
                state.simNowUnixMs += (long)System.Math.Round(deltaSeconds * 1000.0);
            }
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
        /// Land every node's pooled pickings at camp on a fixed cadence — the
        /// "delivery batch" design §5's quality rolls attach to. Each node's
        /// pool arrives as one batch (a batch is always a single resource), so
        /// rolls stay per-batch, never per unit, and Choice keeps landing as
        /// a discrete windfall. Progress only accrues while something is
        /// waiting, so an idle grove doesn't bank deliveries against future
        /// goods.
        /// </summary>
        private static void DeliverPending(GameState state, GameDataAsset data, EconomyData.DeliveryData delivery, double deltaSeconds)
        {
            if (delivery.batchSeconds <= 0.0)
            {
                // Degenerate hand-built data (the validator rejects real
                // content like this) — don't spin the delivery loop.
                return;
            }

            if (!AnythingPending(state))
            {
                state.deliveryProgress = 0.0;
                return;
            }

            state.deliveryProgress += deltaSeconds;
            while (state.deliveryProgress >= delivery.batchSeconds)
            {
                state.deliveryProgress -= delivery.batchSeconds;
                foreach (var node in state.nodes)
                {
                    if (node.basket <= BigDouble.Zero)
                    {
                        continue;
                    }

                    var moved = node.basket;
                    node.basket = BigDouble.Zero;
                    Deliver(state, data, node, moved);
                }
            }
        }

        /// <summary>
        /// Land one delivery batch at camp with its design §5 quality roll: the
        /// whole delivery takes the rolled tier. Poor goes to plain stock,
        /// Decent to the decent pool (sold at the bonus alongside plain stock),
        /// Choice to the specimen pool (held for an explicit windfall sale —
        /// or, later, donation or offering).
        /// </summary>
        private static void Deliver(GameState state, GameDataAsset data, NodeState node, BigDouble amount)
        {
            switch (Quality.Roll(state, data, node))
            {
                case QualityTier.Choice:
                    state.AddChoice(node.resourceId, amount);
                    Compendium.RecordChoice(state, node.resourceId, amount);
                    break;

                case QualityTier.Decent:
                    state.AddDecent(node.resourceId, amount);
                    break;

                default:
                    state.AddResource(node.resourceId, amount);
                    break;
            }
        }

        /// <summary>True when any node has pickings waiting for the next delivery.</summary>
        private static bool AnythingPending(GameState state)
        {
            foreach (var node in state.nodes)
            {
                if (node.basket > BigDouble.Zero)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Apply a Tending burst to <paramref name="node"/> (the tap-to-tend
        /// interaction, design §5): for the next economy.tending.burstDurationSec
        /// seconds the node yields at burstYieldMult, and for
        /// choiceBonusDurationSec its haul batches roll Choice at the
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
            node.choiceBonusRemaining = economy.tending.choiceBonusDurationSec;
        }

        /// <summary>
        /// <see cref="Tend(NodeState, EconomyData)"/> plus the deed record —
        /// tending is a warden act, and the Rite's deed slots count it
        /// (design §7). It does not move the warden's post — standing somewhere
        /// is an explicit assignment (one body per post, §2).
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
        /// How much of an absence pays out, per design doc §8:
        /// offlineEarn = rate · min(t, cap). Real elapsed time is capped at the
        /// offline cap (base cap hours, raised by any offlineCapHours upgrade
        /// owned, plus the additive band) — the wall-clock seconds credited,
        /// before the rate multiplier.
        /// </summary>
        public static double CreditedSeconds(GameState state, GameDataAsset data, double realElapsedSeconds)
        {
            if (state == null || data == null || realElapsedSeconds <= 0.0)
            {
                return 0.0;
            }

            var capSeconds = Upgrades.OfflineCapHours(state, data) * 3600.0;
            return System.Math.Min(realElapsedSeconds, capSeconds);
        }

        /// <summary>
        /// The offline rate, and what it multiplies. It scales the SIM SECONDS
        /// the catch-up runs, not the yield those seconds pay — a half rate
        /// means the grove lived half as long while you were gone, so tincture
        /// buffs lapse and craft batches land at the slower pace too, not just
        /// the gathering. That is the intent (an absence is a thinner slice of
        /// the same day, not a discounted one), and it is worth stating because
        /// the value is 1.0 today and nothing would notice if it drifted.
        /// Absent or non-positive config reads as 1.0 — hand-built fixtures
        /// carry no offline section, and the validator rejects a real one below
        /// zero.
        /// </summary>
        public static double OfflineRateMultiplier(GameDataAsset data)
        {
            var offline = data?.economy?.offline;
            return offline != null && offline.rateMultiplier > 0.0 ? offline.rateMultiplier : 1.0;
        }

        /// <summary>
        /// Credit time away since the last session, all in one call. Returns the
        /// capped wall-clock seconds credited, so a caller can report how much
        /// of the absence paid out. <see cref="OfflineCatchUp"/> is the same
        /// work spread over frames, for an absence long enough to be worth it.
        /// </summary>
        public static double AdvanceOffline(GameState state, GameDataAsset data, double realElapsedSeconds)
        {
            var catchUp = OfflineCatchUp.Begin(state, data, realElapsedSeconds);
            catchUp.RunToCompletion();
            return catchUp.Summary.creditedSeconds;
        }

        /// <summary>
        /// <see cref="AdvanceOffline"/> plus a report of what it paid out — the
        /// welcome-back sheet's data. Snapshots the holdings (camp stock plus
        /// what's pooled at the nodes awaiting the next delivery, so those
        /// still count as gained), runs the catch-up, and diffs — the gains
        /// stay correct however the tick evolves.
        /// </summary>
        public static OfflineSummary AdvanceOfflineWithSummary(GameState state, GameDataAsset data, double realElapsedSeconds)
        {
            var catchUp = OfflineCatchUp.Begin(state, data, realElapsedSeconds);
            catchUp.RunToCompletion();
            return catchUp.Summary;
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
        /// included, so a Decent or Choice batch landed offline still counts
        /// as a welcome-back gain of its resource. Internal to the assembly
        /// rather than private: <see cref="OfflineCatchUp"/> takes the two ends
        /// of the same diff, frames apart.
        /// </summary>
        internal static Dictionary<string, BigDouble> SnapshotHoldings(GameState state)
        {
            var holdings = new Dictionary<string, BigDouble>(state.resources);
            AddPool(holdings, state.decentResources);
            AddPool(holdings, state.choiceResources);
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
            // A familiar's base hands. Cut to 0.1 when hauling retired
            // (2026-07-31) — lossless deliveries multiplied effective camp
            // income, so the base rate absorbs the correction. Absent or 0
            // (hand-built fixtures) keeps the historical 1/s.
            var baseRate = economy.kith != null && economy.kith.gatherPerSecond > 0.0
                ? economy.kith.gatherPerSecond
                : 1.0;
            // The open tide's lean on this node's find (design §15) — read live
            // from the sim clock cursor, never from the cached effect union.
            var tide = Wheel.YieldMult(state, data, node.resourceId);

            return new BigDouble(agents * baseRate) * node.yieldMultiplier * masteryBonus * richness * planters * tide * global;
        }

        /// <summary>
        /// Everything the node yields per second right now — the stationed
        /// kith's lane plus the warden's own hands. <see cref="YieldPerSecond"/>
        /// is deliberately the basket lane alone (the warden pockets theirs
        /// straight to camp, so it never pools in a basket), but that is an
        /// accounting seam and not something a reader of the node's plate
        /// cares about: a posted warden read as "0.0/s" on the very node they
        /// were standing on. Anything asking "how fast is this ground worked"
        /// wants this one.
        /// </summary>
        public static BigDouble TotalYieldPerSecond(NodeState node, GameState state, GameDataAsset data, EconomyData economy)
        {
            if (node == null || state == null)
            {
                return BigDouble.Zero;
            }

            return YieldPerSecond(node, state, data, economy)
                   + new BigDouble(Warden.GatherPerSecond(state, data, economy, node));
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
