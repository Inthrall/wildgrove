using System.Collections.Generic;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// The narrative display layer's logic (design §6): which words show
    /// where, never the words themselves (those live in dialogue.json, on
    /// the ~1,200-word budget). Waystone inscriptions reveal on arrival —
    /// the first time each zone with an authored inscription is unlocked —
    /// and stay re-readable in the Compendium forever; the read-set survives
    /// Migration (the warden has read the stone; lore stays read, so run 2
    /// isn't re-interrupted). Unauthored (empty) lines simply never show.
    /// </summary>
    public static class Narrative
    {
        /// <summary>The waystone inscription for a zone, or null when unauthored.</summary>
        public static string WaystoneText(GameDataAsset data, string zoneId)
        {
            return Find(data.dialogue?.waystones, zoneId);
        }

        /// <summary>The verse line spoken at a zone's verse site, or null when unauthored.</summary>
        public static string VerseLine(GameDataAsset data, string zoneId)
        {
            return Find(data.dialogue?.verses, zoneId);
        }

        /// <summary>The assembled fossil's card line, or null when unauthored.</summary>
        public static string FossilCard(GameDataAsset data, string fossilId)
        {
            return Find(data.dialogue?.fossilCards, fossilId);
        }

        /// <summary>
        /// The next waystone to reveal: the first zone (data order) that is
        /// unlocked, carries an authored inscription, and hasn't been read.
        /// Null when the trail is caught up.
        /// </summary>
        public static ZoneData NextUnreadWaystone(GameState state, GameDataAsset data)
        {
            var unlocked = Upgrades.UnlockedZoneIds(state, data);
            foreach (var zone in data.zones)
            {
                if (unlocked.Contains(zone.id)
                    && !state.seenWaystoneZoneIds.Contains(zone.id)
                    && WaystoneText(data, zone.id) != null)
                {
                    return zone;
                }
            }

            return null;
        }

        public static void MarkWaystoneRead(GameState state, string zoneId)
        {
            if (!state.seenWaystoneZoneIds.Contains(zoneId))
            {
                state.seenWaystoneZoneIds.Add(zoneId);
            }
        }

        private static string Find(List<StringEntry> entries, string key)
        {
            if (entries == null)
            {
                return null;
            }

            foreach (var entry in entries)
            {
                if (entry.key == key)
                {
                    return string.IsNullOrWhiteSpace(entry.text) ? null : entry.text;
                }
            }

            return null;
        }
    }
}
