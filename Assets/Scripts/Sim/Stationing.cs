using System.Collections.Generic;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// Where the kith stands (design §2) — the moment-to-moment allocation.
    /// Every post holds at most one body (warden or familiar); a stationed
    /// familiar works its post at full rate, scaled by its species trait,
    /// while a familiar without a post rests at camp and works nothing (a
    /// slot is the right to hold a post, §4 — resting help for free would
    /// make the ladder worthless).
    /// </summary>
    public static class Stationing
    {
        public static IEnumerable<Familiar> AssignedTo(GameState state, string stationId)
        {
            foreach (var familiar in state.roster)
            {
                if (!familiar.IsResting && familiar.stationId == stationId)
                {
                    yield return familiar;
                }
            }
        }

        /// <summary>The familiar holding a post, or null when it stands empty (one body per post, §2).</summary>
        public static Familiar OccupantOf(GameState state, string stationId)
        {
            if (string.IsNullOrEmpty(stationId))
            {
                return null;
            }

            foreach (var familiar in state.roster)
            {
                if (!familiar.IsResting && familiar.stationId == stationId)
                {
                    return familiar;
                }
            }

            return null;
        }

        public static int CountAssignedTo(GameState state, string stationId)
        {
            var count = 0;
            foreach (var familiar in state.roster)
            {
                if (!familiar.IsResting && familiar.stationId == stationId)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>Familiars resting at camp — companions without a post (§4: the collection outgrows the slots).</summary>
        public static int Resting(GameState state)
        {
            var count = 0;
            foreach (var familiar in state.roster)
            {
                if (familiar.IsResting)
                {
                    count++;
                }
            }

            return count;
        }

        public static int OnTrail(GameState state)
        {
            return CountAssignedTo(state, Familiar.TrailStation);
        }

        /// <summary>Familiars holding the wander post (0 or 1 — one body per post).</summary>
        public static int Wandering(GameState state)
        {
            return CountAssignedTo(state, Familiar.WanderStation);
        }

        /// <summary>
        /// Effective gatherers contributing to a node this tick: the familiar
        /// assigned to it counts as one, scaled by its trait when it matches —
        /// plus the wanderer's share, an even split of one gatherer across
        /// every node (roaming, averaged out).
        /// </summary>
        public static double GatherAgentsAt(GameState state, GameDataAsset data, NodeState node)
        {
            var sum = 0.0;
            var nodeCount = state.nodes.Count;
            foreach (var familiar in state.roster)
            {
                if (familiar.IsResting)
                {
                    continue;
                }

                if (familiar.stationId == node.id)
                {
                    sum += Traits.NodeYieldFactor(familiar, node, data);
                }
                else if (familiar.IsWandering && nodeCount > 0)
                {
                    sum += Traits.NodeYieldFactor(familiar, node, data) / nodeCount;
                }
            }

            return sum;
        }

        /// <summary>
        /// Effective carrier lanes on the trail: the familiar holding the trail
        /// post, scaled by its throughput trait. An unheld trail hauls
        /// nothing — the lane is a post like any other.
        /// </summary>
        public static double TrailCarriers(GameState state, GameDataAsset data)
        {
            var held = 0.0;
            foreach (var familiar in state.roster)
            {
                if (!familiar.IsResting && familiar.IsOnTrail)
                {
                    held += Traits.TrailThroughputFactor(familiar, data);
                }
            }

            return held;
        }

        /// <summary>
        /// Effective watchers the wander post supplies to every observation
        /// site (the watch is no longer a post of its own — the wanderer
        /// passes each site as it roams), scaled by watch-speed traits.
        /// </summary>
        public static double WanderAgents(GameState state, GameDataAsset data)
        {
            var sum = 0.0;
            foreach (var familiar in state.roster)
            {
                if (!familiar.IsResting && familiar.IsWandering)
                {
                    sum += Traits.DigSpeedFactor(familiar, data);
                }
            }

            // The warden may now take the wander post too, watching each site it
            // passes at the base rate (the warden carries no species trait).
            if (Warden.IsWandering(state))
            {
                sum += 1.0;
            }

            return sum;
        }
    }
}
