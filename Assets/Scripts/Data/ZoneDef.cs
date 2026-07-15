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

        /// <summary>The quiet place where this zone's verse of the Rite is revealed (design doc §3). Required for mvp-scope zones.</summary>
        public string VerseSite { get; set; }
        public long? MapCostCoin { get; set; }
        public string Scope { get; set; }
    }
}
