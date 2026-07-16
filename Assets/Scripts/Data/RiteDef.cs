using System.Collections.Generic;

namespace Wildgrove.Data
{
    public enum RiteSlotType
    {
        /// <summary>A quantity of a gathered or crafted good; credits Renown at trade value.</summary>
        Resource,

        /// <summary>An act rather than goods (e.g. Tend N times); carries a fixed renownGrant.</summary>
        Deed,

        /// <summary>A Fine or Pristine specimen; carries a fixed renownGrant.</summary>
        Specimen,

        /// <summary>A fossil fragment; carries a fixed renownGrant.</summary>
        Fragment
    }

    /// <summary>One offering slot in a verse. Which fields apply depends on <see cref="Type"/>.</summary>
    public sealed class RiteSlotDef
    {
        public RiteSlotType Type { get; set; }
        public string Resource { get; set; }
        public long Amount { get; set; }
        public string Deed { get; set; }
        public int Count { get; set; }
        public string Quality { get; set; }
        public long RenownGrant { get; set; }
    }

    /// <summary>One verse of a Rite — revealed at its zone's verse site; chooseCount slots complete it.</summary>
    public sealed class RiteVerseDef
    {
        public string Id { get; set; }
        public string Zone { get; set; }
        public List<string> Spotlight { get; set; } = new List<string>();
        public List<RiteSlotDef> Slots { get; set; } = new List<RiteSlotDef>();
    }

    /// <summary>A region's Rite — the staged offering ritual gating Migration (design doc §7).</summary>
    public sealed class RiteDef
    {
        public string Id { get; set; }

        /// <summary>Which migration index this authored Rite serves (0 = run 1). Runs beyond the authored set use the generator.</summary>
        public int Migration { get; set; }

        public List<RiteVerseDef> Verses { get; set; } = new List<RiteVerseDef>();
    }

    /// <summary>
    /// Tuning for the run-2+ Rite generator (design §8: verseDemand(m) =
    /// baseQty · d^m; the 1–2 spotlight slots are the cheapest path).
    /// </summary>
    public sealed class RiteGeneratorDef
    {
        /// <summary>d in verseDemand(m) = baseQty · d^m — how much more each Rite asks than the last.</summary>
        public double DemandGrowth { get; set; }

        /// <summary>Spotlight slots price at value × this (≤ 1 — the cheap path).</summary>
        public double SpotlightDiscount { get; set; }

        /// <summary>Off-spotlight slots price at value × this (≥ 1 — grind at a premium).</summary>
        public double OffSpotlightPremium { get; set; }
    }

    /// <summary>Top-level shape of rites.json.</summary>
    public sealed class RitesConfig
    {
        /// <summary>How many of a verse's slots must be filled to complete it (design: 3 of 5).</summary>
        public int ChooseCount { get; set; }

        /// <summary>Run-2+ generator tuning; null disables the generator (later runs re-walk the authored Rite).</summary>
        public RiteGeneratorDef Generator { get; set; }

        public List<RiteDef> Rites { get; set; } = new List<RiteDef>();
    }
}
