using System.Collections.Generic;
using System.Globalization;
using BreakInfinity;
using UnityEngine;
using Wildgrove.Data;
using Wildgrove.Sim;

namespace Wildgrove.Game
{
    /// <summary>
    /// Pure journal formatters — numerals, percentages, bundle lines, slot and
    /// building labels that depend only on their arguments (no game state).
    /// Shared by every journal view via <c>using static</c>.
    /// </summary>
    internal static class JournalFormat
    {
        internal static string Roman(int value)
        {
            if (value <= 0)
            {
                return "-";
            }

            int[] values = { 1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1 };
            string[] symbols = { "M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I" };
            var sb = new System.Text.StringBuilder();
            for (var i = 0; i < values.Length && value > 0; i++)
            {
                while (value >= values[i])
                {
                    sb.Append(symbols[i]);
                    value -= values[i];
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// A good's id as the journal says it aloud — "copper-scree" is a key,
        /// "copper scree" is a name. Ids double as display text throughout
        /// (there are no authored resource names), and the hyphens show worst
        /// at the Exchange, where two of them sit either side of an arrow.
        /// </summary>
        internal static string GoodName(string id)
        {
            return string.IsNullOrEmpty(id) ? id : id.Replace('-', ' ');
        }

        internal static string PlainNumber(double value)
        {
            // Invariant, like every NumberFormat path. A comma-decimal locale
            // otherwise puts "2,4" beside an invariant "2.4" in the same line of
            // copy — the journal has one voice, and that includes its numerals.
            return value % 1.0 == 0.0
                ? ((long)value).ToString(CultureInfo.InvariantCulture)
                : value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        internal static string Percent(double value)
        {
            return Mathf.RoundToInt((float)(value * 100.0)) + "%";
        }

        internal static string SlotName(RiteSlotData slot)
        {
            switch (slot.type)
            {
                case RiteSlotType.Resource:
                    return slot.resource;
                case RiteSlotType.Deed:
                    return slot.deed;
                case RiteSlotType.Specimen:
                    // Named in the plural whatever the ask, because the count
                    // column beside it carries the number — same as every goods
                    // row. Writing "one" into the name was true only while the
                    // ask was pinned at 1, and it survived the zone and fold
                    // ramps as "one choice find  0 / 8".
                    return (string.IsNullOrEmpty(slot.quality) ? "decent" : slot.quality) + " finds";
                case RiteSlotType.Sketch:
                    return "a field sketch";
                default:
                    return slot.resource ?? slot.deed ?? "offering";
            }
        }

        /// <summary>What one bought level of a building line grants — the row's "say what it gives" clause.</summary>
        internal static string PerLevelGivesLabel(BuildingData building)
        {
            var perLevel = building.perLevel;
            if (perLevel == null)
            {
                return null;
            }

            var pct = "+" + Mathf.RoundToInt((float)(perLevel.value * 100.0)) + "%";
            switch (perLevel.type)
            {
                case "stationSpeedBonus": return "each level: " + pct + " craft speed at this station";
                case "offlineCapBonusHours": return "each level: the away credit runs +" + perLevel.value.ToString("0.##", CultureInfo.InvariantCulture) + "h longer";
                case "comfort": return "each level: " + pct + " familiar XP while posted";
                default: return null;
            }
        }

        internal static string BundleLabel(List<ItemAmount> materials)
        {
            if (materials == null || materials.Count == 0)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            foreach (var material in materials)
            {
                parts.Add(material.amount + " " + material.id);
            }

            return string.Join(", ", parts);
        }

        internal static string BundleLabel(List<Buildings.MaterialCost> bundle)
        {
            if (bundle == null || bundle.Count == 0)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            foreach (var cost in bundle)
            {
                parts.Add(NumberFormat.Short(cost.amount) + " " + cost.id);
            }

            return string.Join(", ", parts);
        }

        internal static IEnumerable<(string id, BigDouble amount)> Costs(List<ItemAmount> materials)
        {
            foreach (var material in materials ?? new List<ItemAmount>())
            {
                yield return (material.id, new BigDouble(material.amount));
            }
        }

        internal static IEnumerable<(string id, BigDouble amount)> Costs(List<Buildings.MaterialCost> bundle)
        {
            foreach (var cost in bundle ?? new List<Buildings.MaterialCost>())
            {
                yield return (cost.id, cost.amount);
            }
        }
    }
}
