using System.Collections.Generic;

namespace Wildgrove.Data
{
    public sealed class ZoneDef
    {
        public string Id { get; set; }
        public int Order { get; set; }
        public string Name { get; set; }
        public List<string> Resources { get; set; } = new List<string>();
        public List<string> Unlocks { get; set; } = new List<string>();
        public string Keystone { get; set; }
        public bool DigSite { get; set; }
        public long? MapCostCoin { get; set; }
        public string Scope { get; set; }
    }
}
