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

        /// <summary>The recorded insect plate's lore line, or null when unauthored.</summary>
        public static string InsectPlate(GameDataAsset data, string insectId)
        {
            return Find(data.dialogue?.insectPlates, insectId);
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

        /// <summary>Unity serializes an authored-empty section as a zeroed object — a zone-less or stone-less chain reads as "no final waystones".</summary>
        public static bool FinalWaystonesConfigured(GameDataAsset data)
        {
            var chain = data?.dialogue?.finalWaystones;
            return chain != null && !string.IsNullOrEmpty(chain.zoneId)
                && chain.stones != null && chain.stones.Count > 0;
        }

        public static int FinalWaystoneCount(GameDataAsset data)
        {
            return FinalWaystonesConfigured(data) ? data.dialogue.finalWaystones.stones.Count : 0;
        }

        /// <summary>The stones already read, in authored order — the Compendium re-reads them from here.</summary>
        public static IEnumerable<StringEntry> ReadFinalWaystones(GameState state, GameDataAsset data)
        {
            if (!FinalWaystonesConfigured(data))
            {
                yield break;
            }

            var stones = data.dialogue.finalWaystones.stones;
            var count = System.Math.Min(state.finalWaystonesRead, stones.Count);
            for (var i = 0; i < count; i++)
            {
                yield return stones[i];
            }
        }

        public static bool AreFinalWaystonesComplete(GameState state, GameDataAsset data)
        {
            return FinalWaystonesConfigured(data)
                && state.finalWaystonesRead >= data.dialogue.finalWaystones.stones.Count;
        }

        /// <summary>
        /// The next final waystone to reveal (design §7), or null. Three things
        /// must hold: the chain's zone is open, the chain isn't finished, and
        /// this fold hasn't already given one up — the reveal is paced a stone
        /// per fold so the four beats can't arrive as one wall of text on the
        /// run that opens the peaks.
        /// </summary>
        public static StringEntry NextFinalWaystone(GameState state, GameDataAsset data)
        {
            if (!FinalWaystonesConfigured(data) || AreFinalWaystonesComplete(state, data))
            {
                return null;
            }

            if (!Upgrades.UnlockedZoneIds(state, data).Contains(data.dialogue.finalWaystones.zoneId))
            {
                return null;
            }

            if (state.migrationCount <= state.finalWaystoneLastFold)
            {
                return null;
            }

            return data.dialogue.finalWaystones.stones[state.finalWaystonesRead];
        }

        /// <summary>
        /// Take the next stone. Stamping the fold is what holds the chain to one
        /// a fold; the count is what holds it to the authored order.
        /// </summary>
        public static void MarkFinalWaystoneRead(GameState state, GameDataAsset data)
        {
            if (NextFinalWaystone(state, data) == null)
            {
                return;
            }

            state.finalWaystonesRead++;
            state.finalWaystoneLastFold = state.migrationCount;
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
