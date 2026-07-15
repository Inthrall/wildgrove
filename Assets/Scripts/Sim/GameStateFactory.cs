using System.Linq;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// Builds the starting state for a fresh run. Phase 1 opens on Sunfield
    /// Meadow only — a gathering node per zone resource, worked by the zone's
    /// foraging familiars, with one seeded so the loop is alive on first frame.
    /// </summary>
    public static class GameStateFactory
    {
        public const string StartingZoneId = "sunfield-meadow";

        public static GameState NewGame(GameDataAsset data)
        {
            var state = new GameState();
            var zone = data.ZonesById[StartingZoneId];
            var skill = PrimaryGatheringSkill(zone);

            foreach (var resourceId in zone.resources)
            {
                state.nodes.Add(new NodeState
                {
                    id = NodeId(zone.id, resourceId),
                    zoneId = zone.id,
                    resourceId = resourceId,
                    skill = skill,
                    familiarCount = 0,
                });
            }

            // Seed one gatherer on the first node and one carrier for the camp —
            // "every region opens with one gatherer and one carrier already
            // helping" (design §2): immediate agency, and goods flow to camp
            // from the first frame.
            if (state.nodes.Count > 0)
            {
                state.nodes[0].familiarCount = 1;
            }

            state.carrierCount = 1;

            return state;
        }

        public static string NodeId(string zoneId, string resourceId)
        {
            return zoneId + ":" + resourceId;
        }

        /// <summary>
        /// The zone's gathering skill — the first skill it unlocks. Sunfield
        /// unlocks foraging only; later zones list their gathering skill first
        /// in the unlocks table.
        /// </summary>
        private static string PrimaryGatheringSkill(ZoneData zone)
        {
            return zone.unlocks.FirstOrDefault();
        }
    }
}
