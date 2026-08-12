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

        /// <summary>
        /// True when a body stands at <paramref name="stationId"/> — the warden
        /// or a stationed familiar (a node id, or a watch post).
        ///
        /// This asks who is STANDING here, not whether the ground earns — the
        /// strip draws one post per body, and a yield test
        /// (<see cref="Simulation.TotalYieldPerSecond"/>) can read zero for
        /// reasons that are nothing to do with standing.
        /// </summary>
        public static bool HasBodyAt(GameState state, string stationId)
        {
            if (state == null || string.IsNullOrEmpty(stationId))
            {
                return false;
            }

            return Warden.PostNodeId(state) == stationId || OccupantOf(state, stationId) != null;
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

        /// <summary>
        /// Familiars keeping <paramref name="zoneId"/>'s watch (0 or 1 — one
        /// body per post). A probe, kept for the tests and for anything
        /// diagnosing the watch: <see cref="WatchAgentsAt"/> is what the tick
        /// asks — it counts the warden too, and scales by watch traits.
        /// </summary>
        public static int WatchersAt(GameState state, string zoneId)
        {
            return CountAssignedTo(state, Familiar.WatchStation(zoneId));
        }

        /// <summary>
        /// Effective gatherers contributing to a node this tick: the familiar
        /// assigned to it counts as one, scaled by its trait when it matches.
        /// A watcher contributes nothing here — a watch post is the watch and
        /// only the watch (a body that also gathered read as two jobs on one
        /// post, and players couldn't say what the post was for).
        /// </summary>
        public static double GatherAgentsAt(GameState state, GameDataAsset data, NodeState node)
        {
            var sum = 0.0;
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
            }

            return sum;
        }

        /// <summary>
        /// Effective watchers at <paramref name="zoneId"/>'s observation site,
        /// scaled by watch-speed traits — the site's own post and nobody else's.
        /// A body watches the one place it stands (revised 2026-08-12): the
        /// single roaming post it replaced sketched at every site at once, so a
        /// player who sent one companion to one site saw plates filling in
        /// across the whole map and had no way to ask why.
        /// </summary>
        public static double WatchAgentsAt(GameState state, GameDataAsset data, string zoneId)
        {
            var station = Familiar.WatchStation(zoneId);
            var sum = 0.0;
            foreach (var familiar in state.roster)
            {
                if (!familiar.IsResting && familiar.stationId == station)
                {
                    sum += Traits.DigSpeedFactor(familiar, data);
                }
            }

            // The warden may take a watch post like any familiar, watching that
            // site at the base rate (the warden carries no species trait).
            if (Warden.IsWatchingAt(state, zoneId))
            {
                sum += 1.0;
            }

            return sum;
        }
    }
}
