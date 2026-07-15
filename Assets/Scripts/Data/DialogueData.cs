using System.Collections.Generic;

namespace Wildgrove.Data
{
    public sealed class DialogueData
    {
        public Dictionary<string, string> Waystones { get; set; } = new Dictionary<string, string>();

        /// <summary>Verse lines of the Rite, keyed by zone — the living land's asks (design doc §6).</summary>
        public Dictionary<string, string> Verses { get; set; } = new Dictionary<string, string>();

        public List<ProvisionerLine> Provisioner { get; set; } = new List<ProvisionerLine>();
        public List<string> MigrationVignette { get; set; } = new List<string>();
        public Dictionary<string, string> FossilCards { get; set; } = new Dictionary<string, string>();

        public sealed class ProvisionerLine
        {
            public string Id { get; set; }
            public string Trigger { get; set; }
            public string Line { get; set; }
        }
    }
}
