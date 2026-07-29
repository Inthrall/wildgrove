using System.Collections.Generic;

namespace Wildgrove.Data
{
    public sealed class InsectDef
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int Sketches { get; set; }
        public List<string> Habitats { get; set; } = new List<string>();
        public double Rarity { get; set; }

        /// <summary>
        /// A plate no one draws: it arrives already recorded when Play Games
        /// awards it (design §11). Never entered in the observation roll, so it
        /// holds no habitats and no rarity — the validator enforces both, since
        /// either one would be a promise the sketch loop can't keep.
        /// </summary>
        public bool Rewarded { get; set; }

        public List<EffectDef> Effects { get; set; } = new List<EffectDef>();
    }
}
