using System.Collections.Generic;

namespace Wildgrove.Data
{
    public sealed class GearDef
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Slot { get; set; }
        public string Skill { get; set; }
        public Dictionary<string, int> Materials { get; set; } = new Dictionary<string, int>();
        public List<EffectDef> Effects { get; set; } = new List<EffectDef>();
    }
}
