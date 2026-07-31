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
        public Dictionary<string, string> InsectPlates { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// The final waystones (design §7): the last zone's ordered chain, one
        /// stone per fold, carrying the reveal the deep amber's notes point at.
        /// Absent in data means no chain — never a broken one.
        /// </summary>
        public FinalWaystoneChain FinalWaystones { get; set; }

        public sealed class ProvisionerLine
        {
            public string Id { get; set; }
            public string Trigger { get; set; }
            public string Line { get; set; }
        }

        public sealed class FinalWaystoneChain
        {
            public string Zone { get; set; }
            public List<FinalWaystoneStone> Stones { get; set; } = new List<FinalWaystoneStone>();
        }

        public sealed class FinalWaystoneStone
        {
            public string Id { get; set; }
            public string Text { get; set; }
        }
    }
}
