namespace Wildgrove.Data
{
    public enum EffectType
    {
        YieldMult,
        YieldBonus,
        SellValueBonus,
        CraftSpeedMult,
        DigSpeedMult,
        PristineChanceBonus,
        FolioSpreadBonusMult,
        OfflineCapHours,
        OfflineCapBonusHours,
        TendingBurstBonus,

        /// <summary>Quickens the warden's own hands: summed with the fell pony's wardenYieldBonus trait into one additive band on the warden's gather rate.</summary>
        WardenYieldBonus,

        /// <summary>Fattens every caught windfall: summed with the pack raven's bubbleRewardBonus trait into one additive band on the flat bubble haul.</summary>
        BubbleRewardBonus,
        UnlockZone,
        UnlockSkill,
        UnlockRecipe,
        UnlockDigSite,

        /// <summary>A familiar of <see cref="EffectDef.Species"/> joins the kith when the upgrade is taken (resting at camp, named on arrival). No-op if that species already walks — each joins once, ever.</summary>
        RecruitSpecies,

        /// <summary>The ladder rung named by <see cref="EffectDef.Upgrade"/> is owned from every run's start, free — the Almanac's starting tool tiers and zone skips (design §8). Almanac nodes only.</summary>
        GrantUpgrade,

        /// <summary>The camp's stations keep their standing orders across the fold, taking each up again as the new run re-earns it (design §8's auto-craft). Almanac nodes only.</summary>
        KeepCraftOrders
    }

    /// <summary>
    /// One machine-readable effect from upgrades/gear/insects. Which of the
    /// optional target fields is populated depends on <see cref="Type"/>.
    /// </summary>
    public sealed class EffectDef
    {
        public EffectType Type { get; set; }
        public string Skill { get; set; }
        public string Zone { get; set; }
        public string Resource { get; set; }
        public string Recipe { get; set; }
        public string Species { get; set; }
        public string Upgrade { get; set; }
        public double? Value { get; set; }
    }
}
