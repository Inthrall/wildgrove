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
        /// <summary>Per-run soft currency, spent on hires, tools, buildings and maps.</summary>
        public BigDouble coin = BigDouble.Zero;

        /// <summary>Meta currency carried across migrations; drives the global yield bonus.</summary>
        public double verdurePoints;

        /// <summary>Raw and crafted materials on hand, keyed by resource id.</summary>
        public Dictionary<string, BigDouble> resources = new Dictionary<string, BigDouble>();

        /// <summary>Every gathering node the player has access to this run.</summary>
        public List<NodeState> nodes = new List<NodeState>();

        public BigDouble GetResource(string resourceId)
        {
            return resources.TryGetValue(resourceId, out var amount) ? amount : BigDouble.Zero;
        }

        public void AddResource(string resourceId, BigDouble amount)
        {
            resources[resourceId] = GetResource(resourceId) + amount;
        }

        /// <summary>Total crew hired this run across every node — drives the hire-cost curve.</summary>
        public int TotalCrew()
        {
            var total = 0;
            foreach (var node in nodes)
            {
                total += node.crewCount;
            }

            return total;
        }
    }

    /// <summary>
    /// One worked resource node — a single resource within a zone, gathered by
    /// that zone's crew. Crews accrue the resource automatically each tick.
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

        /// <summary>Number of crew assigned; the base gather rate is one unit per crew per second.</summary>
        public int crewCount;

        /// <summary>Per-resource mastery level; each level adds economy.mastery.yieldBonusPerLevel.</summary>
        public int masteryLevel;

        /// <summary>
        /// Seconds left on an active Tending burst. While positive, the node's
        /// yield is multiplied by economy.tending.burstYieldMult for that slice
        /// of the tick; a fresh Tend refreshes it to the full burst duration.
        /// </summary>
        public double tendBurstRemaining;

        /// <summary>
        /// Combined tool + gear + upgrade multiplier for this node. Defaults to
        /// 1 and is recomputed by the upgrade/gear system when it lands; the
        /// tick treats it as an opaque multiplier so it stays balance-agnostic.
        /// </summary>
        public double yieldMultiplier = 1.0;
    }
}
