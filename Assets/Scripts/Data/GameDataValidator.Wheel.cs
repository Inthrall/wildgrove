using System.Collections.Generic;
using System.Linq;

namespace Wildgrove.Data
{
    /// <summary>
    /// The Wheel of the year: the sabbats, their nights in both hemispheres, and
    /// the rule that no two tides may overlap in the same hemisphere.
    /// </summary>
    public static partial class GameDataValidator
    {
        private static void ValidateWheel(GameData data, HashSet<string> resourceIds, List<string> issues)
        {
            var wheel = data.Wheel;
            if (wheel == null || wheel.Sabbats == null || wheel.Sabbats.Count == 0)
            {
                // The Wheel is optional data (fixtures) — absent is inert, not wrong.
                return;
            }

            if (wheel.OpenDaysBefore < 1 || wheel.OpenDaysBefore > 60)
            {
                issues.Add($"Wheel openDaysBefore must be 1..60 (was {wheel.OpenDaysBefore})");
            }

            if (wheel.Observance != null)
            {
                var observance = wheel.Observance;
                if (observance.SlotCount < 2 || observance.SlotCount > 8)
                {
                    issues.Add($"Wheel observance slotCount must be 2..8 (was {observance.SlotCount})");
                }

                if (observance.TierSlots == null || observance.TierSlots.Count == 0)
                {
                    issues.Add("Wheel observance has no tierSlots — a keeping with no tiers pays nothing");
                }
                else
                {
                    for (var i = 0; i < observance.TierSlots.Count; i++)
                    {
                        if (observance.TierSlots[i] < 1 || observance.TierSlots[i] > observance.SlotCount
                            || (i > 0 && observance.TierSlots[i] <= observance.TierSlots[i - 1]))
                        {
                            issues.Add("Wheel observance tierSlots must climb within 1..slotCount — each tier asks more than the last");
                            break;
                        }
                    }

                    if (observance.TierAmber == null || observance.TierAmber.Count != observance.TierSlots.Count)
                    {
                        issues.Add("Wheel observance tierAmber must pair one grant with each tier");
                    }
                    else if (observance.TierAmber.Any(amount => amount < 0.0))
                    {
                        issues.Add("Wheel observance tierAmber grants must not be negative");
                    }
                }

                if (observance.SlotValueMult <= 0.0)
                {
                    issues.Add("Wheel observance slotValueMult must be positive");
                }

                if (observance.SpecimenRenown < 0L)
                {
                    issues.Add("Wheel observance specimenRenown must not be negative");
                }
            }

            CheckIds(wheel.Sabbats.Select(s => s.Id), "sabbat", issues);

            foreach (var sabbat in wheel.Sabbats)
            {
                // The forecast prints the name and the margin says the sign —
                // a nameless or silent sabbat reads as a bug.
                if (string.IsNullOrWhiteSpace(sabbat.Name))
                {
                    issues.Add($"Sabbat '{sabbat.Id}' has no name — the forecast prints it");
                }

                if (string.IsNullOrWhiteSpace(sabbat.Sign))
                {
                    issues.Add($"Sabbat '{sabbat.Id}' has no sign — the warden's margin says one line per tide");
                }

                // The coming-sabbat sheet is a name, a countdown and this. A
                // tide with no lore leaves it a bare date a month out.
                if (string.IsNullOrWhiteSpace(sabbat.Lore))
                {
                    issues.Add($"Sabbat '{sabbat.Id}' has no lore — the sheet that waits for a tide says what the day is");
                }

                if (sabbat.Kind != "fire" && sabbat.Kind != "quarter")
                {
                    issues.Add($"Sabbat '{sabbat.Id}' kind must be 'fire' or 'quarter' (was '{sabbat.Kind}')");
                }

                // A touch-less tide is just a date — the ambient touch is the
                // world's one lean now (design §8, §15).
                if (sabbat.Touch == null || sabbat.Touch.Count == 0)
                {
                    issues.Add($"Sabbat '{sabbat.Id}' has no touch — a tide with no lean is a bare date");
                }
                else
                {
                    foreach (var effect in sabbat.Touch)
                    {
                        ValidateEffect($"Sabbat '{sabbat.Id}'", effect, data, resourceIds, issues, sabbatTouch: true);

                        // The Wheel matches yield leans by resource alone — a
                        // skill- or zone-targeted lean would silently apply to
                        // nothing (the same trap the target-less rule guards).
                        if (effect.Type == EffectType.YieldMult && effect.Resource == null)
                        {
                            issues.Add($"Sabbat '{sabbat.Id}' yieldMult touch must target a resource — the Wheel's grain is the resource");
                        }
                    }
                }

                // The keeping's theme must name real goods, or the generator's
                // bias silently applies to nothing (the target-less-effect trap).
                foreach (var lean in sabbat.VerseLean ?? new List<string>())
                {
                    if (!resourceIds.Contains(lean))
                    {
                        issues.Add($"Sabbat '{sabbat.Id}' verseLean references unknown goods '{lean}'");
                    }
                }

                ValidateHemisphereNights(sabbat, "north", sabbat.Nights?.North, issues);
                ValidateHemisphereNights(sabbat, "south", sabbat.Nights?.South, issues);
            }

            ValidateNoOverlappingTides(wheel, "north", s => s.Nights?.North, issues);
            ValidateNoOverlappingTides(wheel, "south", s => s.Nights?.South, issues);
        }

        /// <summary>
        /// Within one hemisphere, no two tides may be open at once — the sim
        /// carries a single open tide, and an overlap would silently drop one.
        /// Windows are [night − openDaysBefore, night + 1), so consecutive
        /// nights need a gap of at least openDaysBefore + 1 days.
        /// </summary>
        private static void ValidateNoOverlappingTides(WheelDef wheel, string hemisphere,
            System.Func<SabbatDef, List<string>> nightsOf, List<string> issues)
        {
            var nights = new List<(System.DateTime day, string sabbat)>();
            foreach (var sabbat in wheel.Sabbats)
            {
                foreach (var night in nightsOf(sabbat) ?? new List<string>())
                {
                    if (System.DateTime.TryParseExact(night, "yyyy-MM-dd",
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None, out var day))
                    {
                        nights.Add((day, sabbat.Id));
                    }
                }
            }

            nights.Sort((a, b) => a.day.CompareTo(b.day));
            for (var i = 1; i < nights.Count; i++)
            {
                var gap = (nights[i].day - nights[i - 1].day).TotalDays;
                if (gap < wheel.OpenDaysBefore + 1)
                {
                    issues.Add($"Sabbat '{nights[i].sabbat}' ({hemisphere}, {nights[i].day:yyyy-MM-dd}) opens before"
                               + $" '{nights[i - 1].sabbat}' closes — nights need a gap of at least {wheel.OpenDaysBefore + 1} days");
                }
            }
        }

        private static void ValidateHemisphereNights(SabbatDef sabbat, string hemisphere, List<string> nights, List<string> issues)
        {
            // Both hemispheres must be authored — the mirror is the design
            // (design §15), and a one-sided sabbat would silently never fire
            // for half the world.
            if (nights == null || nights.Count == 0)
            {
                issues.Add($"Sabbat '{sabbat.Id}' has no {hemisphere} nights — both hemispheres must be authored");
                return;
            }

            foreach (var night in nights)
            {
                if (!System.DateTime.TryParseExact(night, "yyyy-MM-dd",
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out _))
                {
                    issues.Add($"Sabbat '{sabbat.Id}' {hemisphere} night '{night}' is not a yyyy-MM-dd date");
                }
            }
        }
    }
}
