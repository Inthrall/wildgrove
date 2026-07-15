namespace Wildgrove.Data
{
    public enum EffectType
    {
        YieldMult,
        YieldBonus,
        HaulMult,
        SellValueBonus,
        CraftSpeedMult,
        DigSpeedMult,
        PristineChanceBonus,
        MuseumSetBonusMult,
        OfflineCapHours,
        OfflineCapBonusHours,
        OfflineNightFullRate,
        TendingBurstBonus,
        PorterCapacityBonus,
        NoSpoilage,
        UnlockZone,
        UnlockSkill,
        UnlockRecipe,
        UnlockDigSite,
        UnlockMigration
    }

    /// <summary>
    /// One machine-readable effect from upgrades/gear/fossils. Which of the
    /// optional target fields is populated depends on <see cref="Type"/>.
    /// </summary>
    public sealed class EffectDef
    {
        public EffectType Type { get; set; }
        public string Skill { get; set; }
        public string Zone { get; set; }
        public string Resource { get; set; }
        public string Recipe { get; set; }
        public double? Value { get; set; }
    }
}
