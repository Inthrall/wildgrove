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
        FolioSpreadBonusMult,
        OfflineCapHours,
        OfflineCapBonusHours,
        OfflineNightFullRate,
        TendingBurstBonus,
        CarrierCapacityBonus,
        NoSpoilage,
        UnlockZone,
        UnlockSkill,
        UnlockRecipe,
        UnlockDigSite,

        /// <summary>Reveals the live Verdure forecast (the Almanac Desk). Migration itself is gated by the Rite, not an upgrade.</summary>
        UnlockVerdureForecast
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
        public double? Value { get; set; }
    }
}
