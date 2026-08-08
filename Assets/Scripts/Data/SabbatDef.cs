using System.Collections.Generic;

namespace Wildgrove.Data
{
    /// <summary>
    /// The Wheel's authoring model (design §15): eight real-world sabbats,
    /// hemisphere-mirrored, each with a ~two-week tide and one small ambient
    /// touch — the world's one lean since the drawn region season retired
    /// (design §8, 2026-08-08). Nights are authored dates, not astronomy.
    /// </summary>
    public sealed class WheelDef
    {
        /// <summary>Days before the sabbat night that its tide opens.</summary>
        public int OpenDaysBefore { get; set; } = 14;

        public List<SabbatDef> Sabbats { get; set; } = new List<SabbatDef>();
    }

    public sealed class SabbatDef
    {
        public string Id { get; set; }
        public string Name { get; set; }

        /// <summary>"fire" (cross-quarter) or "quarter" (solstice/equinox).</summary>
        public string Kind { get; set; }

        /// <summary>The warden's margin line — the calendar is the warden's, never the land's.</summary>
        public string Sign { get; set; }

        /// <summary>The ambient touch: one narrow lean, read live by the sim's Wheel — never joined into the effect union.</summary>
        public List<EffectDef> Touch { get; set; } = new List<EffectDef>();

        public SabbatNightsDef Nights { get; set; }
    }

    /// <summary>The sabbat's night per hemisphere, "yyyy-MM-dd", authored years ahead (the evergreen rule).</summary>
    public sealed class SabbatNightsDef
    {
        public List<string> North { get; set; } = new List<string>();
        public List<string> South { get; set; } = new List<string>();
    }
}
