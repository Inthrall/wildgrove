using System;
using System.Collections.Generic;
using BreakInfinity;

namespace Wildgrove.Sim
{
    /// <summary>
    /// The mutable runtime state of a single run (one migration cycle). Plain
    /// C# so the simulation is testable without a scene and serialisable by the
    /// save system when it lands. Content constants come from GameDataAsset;
    /// this holds only what changes as the player plays.
    /// </summary>
    [Serializable]
    public sealed class GameState
    {
        /// <summary>Per-run soft currency, spent on tools, buildings and maps (and, as a Phase-1 placeholder, familiar gifts).</summary>
        public BigDouble coin = BigDouble.Zero;

        /// <summary>Meta currency carried across migrations; drives the global yield bonus.</summary>
        public double verdurePoints;

        /// <summary>Raw and crafted materials at camp, keyed by resource id — the only stock that can be sold, gifted, or spent. Goods reach camp by carrier haul from the nodes' baskets.</summary>
        public Dictionary<string, BigDouble> resources = new Dictionary<string, BigDouble>();

        /// <summary>Carrier familiars hauling for the camp (design §8: a camp-wide pool, not per-node).</summary>
        public int carrierCount;

        /// <summary>Every gathering node the player has access to this run.</summary>
        public List<NodeState> nodes = new List<NodeState>();

        /// <summary>Ids of the one-off §9 upgrades bought this run (reset by Migration).</summary>
        public List<string> purchasedUpgradeIds = new List<string>();

        public BigDouble GetResource(string resourceId)
        {
            return resources.TryGetValue(resourceId, out var amount) ? amount : BigDouble.Zero;
        }

        public void AddResource(string resourceId, BigDouble amount)
        {
            resources[resourceId] = GetResource(resourceId) + amount;
        }

        public bool HasUpgrade(string upgradeId)
        {
            return purchasedUpgradeIds.Contains(upgradeId);
        }

        /// <summary>Total familiars befriended this run across every node — drives the gift-cost curve.</summary>
        public int TotalFamiliars()
        {
            var total = 0;
            foreach (var node in nodes)
            {
                total += node.familiarCount;
            }

            return total;
        }
    }

    /// <summary>
    /// One worked resource node — a single resource within a zone, gathered by
    /// that zone's flock of familiars. Familiars accrue the resource
    /// automatically each tick.
    /// </summary>
    [Serializable]
    public sealed class NodeState
    {
        /// <summary>Stable node id, e.g. "sunfield-meadow:berries".</summary>
        public string id;
        public string zoneId;
        public string resourceId;

        /// <summary>The gathering skill working this node (e.g. "foraging").</summary>
        public string skill;

        /// <summary>Number of familiars working the node; the base gather rate is one unit per familiar per second.</summary>
        public int familiarCount;

        /// <summary>Per-resource mastery level; each level adds economy.mastery.yieldBonusPerLevel.</summary>
        public int masteryLevel;

        /// <summary>
        /// Seconds left on an active Tending burst. While positive, the node's
        /// yield is multiplied by economy.tending.burstYieldMult for that slice
        /// of the tick; a fresh Tend refreshes it to the full burst duration.
        /// </summary>
        public double tendBurstRemaining;

        /// <summary>
        /// Goods gathered but not yet hauled to camp — the basket at the node.
        /// Capped at economy.hauling.basketCapacity; gathering into a full
        /// basket is lost (design §2: "under-invest in carriers and baskets
        /// overflow at the node").
        /// </summary>
        public BigDouble basket;

        /// <summary>
        /// Combined tool + gear + upgrade multiplier for this node. Defaults to
        /// 1 and is rebuilt by Upgrades.RecomputeYieldMultipliers on purchase
        /// (gear folds in when its system lands); the tick treats it as an
        /// opaque multiplier so it stays balance-agnostic.
        /// </summary>
        public double yieldMultiplier = 1.0;
    }
}
