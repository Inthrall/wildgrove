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
        public List<EffectDef> Effects { get; set; } = new List<EffectDef>();
    }
}
