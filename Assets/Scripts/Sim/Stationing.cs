using System.Collections.Generic;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// Where the crew stands (design §2) — the moment-to-moment allocation.
    /// Assigned familiars work their post at full rate (with powerups); an
    /// unassigned familiar wanders at ×0.5 with no powerups, its half-help
    /// spread evenly across the unlocked gather nodes; an unheld trail is
    /// covered badly by wanderers (×0.5 of one lane). The warden never wanders.
    /// Interpretations (spread evenly; a flat unheld-trail half-lane) are
    /// flagged in docs/todo.md.
    /// </summary>
    public static class Stationing
    {
        public const double WanderMultiplier = 0.5;

        public static IEnumerable<Familiar> AssignedTo(GameState state, string stationId)
        {
            foreach (var familiar in state.roster)
            {
                if (!familiar.IsWandering && familiar.stationId == stationId)
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
                if (!familiar.IsWandering && familiar.stationId == stationId)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>Familiars drifting with no post — half-hearted help, never zero (§2).</summary>
        public static int Wandering(GameState state)
        {
            var count = 0;
            foreach (var familiar in state.roster)
            {
                if (familiar.IsWandering)
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
        /// familiar counts as one, scaled by its matching powerups, plus the
        /// wanderers' half-help share spread evenly across the unlocked gather
        /// nodes.
        /// </summary>
        public static double GatherAgentsAt(GameState state, GameDataAsset data, NodeState node)
        {
            var sum = 0.0;
            foreach (var familiar in state.roster)
            {
                if (!familiar.IsWandering && familiar.stationId == node.id)
                {
                    sum += Powerups.NodeYieldFactor(familiar, node, data);
                }
            }

            var gatherNodes = state.nodes.Count;
            if (gatherNodes > 0)
            {
                sum += WanderMultiplier * Wandering(state) / gatherNodes;
            }

            return sum;
        }

        /// <summary>
        /// Effective carrier lanes on the trail: the familiars holding the trail
        /// post (scaled by their throughput powerups), or a flat half-lane when
        /// the trail is unheld but the crew has anyone to drift onto it.
        /// </summary>
        public static double TrailCarriers(GameState state, GameDataAsset data)
        {
            var held = 0.0;
            foreach (var familiar in state.roster)
            {
                if (!familiar.IsWandering && familiar.IsOnTrail)
                {
                    held += Powerups.TrailThroughputFactor(familiar, data);
                }
            }

            if (held > 0.0)
            {
                return held;
            }

            return Wandering(state) > 0 ? WanderMultiplier : 0.0;
        }

        /// <summary>Effective diggers at a zone's dig site (scaled by dig-speed powerups); wanderers don't dig.</summary>
        public static double DigAgentsAt(GameState state, GameDataAsset data, string zoneId)
        {
            var sum = 0.0;
            var station = Familiar.DigStationPrefix + zoneId;
            foreach (var familiar in state.roster)
            {
                if (!familiar.IsWandering && familiar.stationId == station)
                {
                    sum += Powerups.DigSpeedFactor(familiar, data);
                }
            }

            return sum;
        }
    }
}
