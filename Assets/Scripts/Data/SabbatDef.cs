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

        /// <summary>The keeping's shape (design §15) — absent means the tide has no verse (touch only).</summary>
        public ObservanceDef Observance { get; set; }

        public List<SabbatDef> Sabbats { get; set; } = new List<SabbatDef>();
    }

    /// <summary>The keeping: the tide's own choose-any offering verse and its tier rewards.</summary>
    public sealed class ObservanceDef
    {
        /// <summary>Slots per keeping — the last is the specimen slot.</summary>
        public int SlotCount { get; set; } = 5;

        /// <summary>Slots answered to reach each tier: kept the eve / the day / the wheel.</summary>
        public List<int> TierSlots { get; set; } = new List<int>();

        /// <summary>Amber granted as each tier is crossed — read against the §9 lean.</summary>
        public List<double> TierAmber { get; set; } = new List<double>();

        /// <summary>Each goods slot's target value against the candidates' average worth — flat at every fold.</summary>
        public double SlotValueMult { get; set; } = 2.0;

        /// <summary>The specimen slot's fixed Renown grant — the luck lane stays cheap.</summary>
        public long SpecimenRenown { get; set; }
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

        /// <summary>Goods the keeping's generator favours — the tide's theme; reachability still governs.</summary>
        public List<string> VerseLean { get; set; } = new List<string>();

        public SabbatNightsDef Nights { get; set; }
    }

    /// <summary>The sabbat's night per hemisphere, "yyyy-MM-dd", authored years ahead (the evergreen rule).</summary>
    public sealed class SabbatNightsDef
    {
        public List<string> North { get; set; } = new List<string>();
        public List<string> South { get; set; } = new List<string>();
    }
}
