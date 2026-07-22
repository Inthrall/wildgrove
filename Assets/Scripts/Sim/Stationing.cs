using System.Collections.Generic;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// Where the kith stands (design §2) — the moment-to-moment allocation.
    /// Stationed familiars work their post at full rate, scaled by their
    /// species trait; a familiar without a post rests at camp and works
    /// nothing (a slot is the right to hold a post, §4 — resting help for
    /// free would make the ladder worthless). The warden never rests.
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

        public static int AtDigSite(GameState state, string zoneId)
        {
            return CountAssignedTo(state, Familiar.DigStationPrefix + zoneId);
        }

        /// <summary>
        /// Effective gatherers contributing to a node this tick: each assigned
        /// familiar counts as one, scaled by its trait when it matches.
        /// </summary>
        public static double GatherAgentsAt(GameState state, GameDataAsset data, NodeState node)
        {
            var sum = 0.0;
            foreach (var familiar in state.roster)
            {
                if (!familiar.IsResting && familiar.stationId == node.id)
                {
                    sum += Traits.NodeYieldFactor(familiar, node, data);
                }
            }

            return sum;
        }

        /// <summary>
        /// Effective carrier lanes on the trail: the familiars holding the trail
        /// post, scaled by their throughput traits. An unheld trail hauls
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

        /// <summary>Effective watchers at a zone's site, scaled by watch-speed traits; resting familiars don't watch.</summary>
        public static double DigAgentsAt(GameState state, GameDataAsset data, string zoneId)
        {
            var sum = 0.0;
            var station = Familiar.DigStationPrefix + zoneId;
            foreach (var familiar in state.roster)
            {
                if (!familiar.IsResting && familiar.stationId == station)
                {
                    sum += Traits.DigSpeedFactor(familiar, data);
                }
            }

            return sum;
        }
    }
}
