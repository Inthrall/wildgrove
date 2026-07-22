using System.Collections.Generic;
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
                return "—";
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

        internal static string Cycle(List<string> options, string current)
        {
            if (options.Count == 0)
            {
                return current;
            }

            var index = options.IndexOf(current);
            return options[(index + 1) % options.Count];
        }

        internal static string PlainNumber(double value)
        {
            return value % 1.0 == 0.0 ? ((long)value).ToString() : value.ToString("0.##");
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
                    return "one " + (string.IsNullOrEmpty(slot.quality) ? "fine" : slot.quality) + " find";
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
                case "basketCapacityBonus": return "each level: " + pct + " basket capacity";
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
