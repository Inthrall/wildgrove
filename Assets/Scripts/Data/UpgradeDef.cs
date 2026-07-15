using System.Collections.Generic;

namespace Wildgrove.Data
{
    public sealed class UpgradeDef
    {
        public int Order { get; set; }
        public string Id { get; set; }
        public string Name { get; set; }
        public string Track { get; set; }
        public long CostCoin { get; set; }
        public Dictionary<string, int> Materials { get; set; } = new Dictionary<string, int>();
        public List<EffectDef> Effects { get; set; } = new List<EffectDef>();
    }
}
