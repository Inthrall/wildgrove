using System.Collections.Generic;

namespace Wildgrove.Data
{
    /// <summary>
    /// One Museum set (design doc §5): a group of Compendium entries completed
    /// by donating a Pristine specimen of each, granting a permanent effect
    /// that survives Migration.
    /// </summary>
    public sealed class MuseumSetDef
    {
        public string Id { get; set; }
        public string Name { get; set; }

        /// <summary>Resource ids whose Pristine specimens the set asks for, one donation each.</summary>
        public List<string> Entries { get; set; } = new List<string>();

        public List<EffectDef> Effects { get; set; } = new List<EffectDef>();
    }
}
