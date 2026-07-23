using System.Collections.Generic;
using System.Linq;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// Builds run state from content data: the fresh-run starting zone, and the
    /// nodes of any zone a trail-map upgrade unlocks later. A fresh run opens
    /// with the warden alone, posted on the first node — the kith arrives
    /// through play (the Ladder's recruit rungs, gift piles, bonds), not at
    /// birth, so the first minutes are the warden's own hands.
    /// </summary>
    public static class GameStateFactory
    {
        public const string StartingZoneId = GameData.StartingZoneId;

        public static GameState NewGame(GameDataAsset data)
        {
            var state = new GameState { rngState = Rng.NewSeed() };
            AddZone(state, data, data.ZonesById[StartingZoneId]);
            Roster.SyncBonded(state, data);
            SeedWardenPost(state);

            // The starting zone's waystone stays quiet — a brand-new player
            // shouldn't meet a lore sheet before they've picked a berry. The
            // inscription remains readable in the Compendium, and later
            // zones' stones still show once on arrival.
            Narrative.MarkWaystoneRead(state, StartingZoneId);
            return state;
        }

        /// <summary>
        /// The warden opens the run already standing somewhere (being
        /// somewhere is the kickstart, design §13) — the first node a
        /// bonded companion doesn't hold. On a true first run that is the
        /// first node; after a fold the carried kith rests, so it still is.
        /// </summary>
        private static void SeedWardenPost(GameState state)
        {
            foreach (var node in state.nodes)
            {
                if (Stationing.OccupantOf(state, node.id) == null)
                {
                    state.wardenPostNodeId = node.id;
                    return;
                }
            }
        }

        /// <summary>
        /// Ensure every unlocked zone's nodes exist (starting zone + owned
        /// unlockZone effects), creating and seeding any that are missing, in
        /// zone order. Call after a purchase and on restore — existing zones
        /// (and the whole node list order) are left untouched.
        /// </summary>
        public static void SyncUnlockedZones(GameState state, GameDataAsset data)
        {
            var unlocked = Upgrades.UnlockedZoneIds(state, data);
            var existing = new HashSet<string>(state.nodes.Select(n => n.zoneId));

            foreach (var zone in data.zones.OrderBy(z => z.order))
            {
                if (unlocked.Contains(zone.id) && !existing.Contains(zone.id))
                {
                    AddZone(state, data, zone);
                }
            }

            SyncDigSites(state, data);
        }

        /// <summary>
        /// Ensure every owned unlockDigSite effect's site exists, in zone
        /// order. Sites open empty — the first digger is a gift, there's no
        /// regional seed for the soil.
        /// </summary>
        public static void SyncDigSites(GameState state, GameDataAsset data)
        {
            var unlocked = Upgrades.UnlockedDigSiteZones(state, data);
            var existing = new HashSet<string>(state.digSites.Select(s => s.zoneId));

            foreach (var zone in data.zones.OrderBy(z => z.order))
            {
                if (zone.digSite && unlocked.Contains(zone.id) && !existing.Contains(zone.id))
                {
                    state.digSites.Add(new DigSiteState { zoneId = zone.id });
                }
            }
        }

        private static void AddZone(GameState state, GameDataAsset data, ZoneData zone)
        {
            // Just the nodes now — the kith is a roster of stationed individuals
            // (design §2/§4), seeded once at run birth, not per-zone. A newly
            // opened zone's nodes wait for the player to station someone there.
            foreach (var resourceId in zone.resources)
            {
                state.nodes.Add(new NodeState
                {
                    id = NodeId(zone.id, resourceId),
                    zoneId = zone.id,
                    resourceId = resourceId,
                    skill = SkillFor(data, zone, resourceId),
                });
            }
        }

        public static string NodeId(string zoneId, string resourceId)
        {
            return zoneId + ":" + resourceId;
        }

        /// <summary>
        /// The node's gathering skill comes from its resource (zones mix
        /// skills — Bramble's copper-scree is mining while its nuts are
        /// foraging). The zone's first unlock is the fallback for hand-built
        /// test data that doesn't author resources.
        /// </summary>
        private static string SkillFor(GameDataAsset data, ZoneData zone, string resourceId)
        {
            if (data.resources != null && data.resources.Count > 0
                && data.ResourcesById.TryGetValue(resourceId, out var resource)
                && !string.IsNullOrEmpty(resource.skill))
            {
                return resource.skill;
            }

            return zone.unlocks.FirstOrDefault();
        }
    }
}
