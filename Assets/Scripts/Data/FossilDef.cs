using System.Collections.Generic;

namespace Wildgrove.Data
{
    public sealed class FossilDef
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int Fragments { get; set; }
        public List<string> DigSites { get; set; } = new List<string>();
        public double StrataRarity { get; set; }
        public List<EffectDef> Effects { get; set; } = new List<EffectDef>();
    }
}
