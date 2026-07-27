using System.Collections.Generic;

namespace Wildgrove.Data
{
    /// <summary>
    /// A tincture (design doc §5, Apothecary): a buff consumable brewed at the
    /// fire. Its id matches the recipe output that produces it; drinking one
    /// spends a unit of stock and grants the effects for DurationSec of sim
    /// time (refresh, never stack).
    /// </summary>
    public sealed class TinctureDef
    {
        public string Id { get; set; }
        public string Name { get; set; }

        /// <summary>The bottle's own line in the HUD — what drinking it does, in voice.</summary>
        public string Description { get; set; }

        public double DurationSec { get; set; }
        public List<EffectDef> Effects { get; set; } = new List<EffectDef>();
    }
}
