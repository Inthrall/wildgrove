using System.Collections.Generic;

namespace Wildgrove.Data
{
    /// <summary>
    /// One Folio spread (design doc §6): a group of Compendium entries completed
    /// by fixing a Pristine specimen of each into the journal's back pages,
    /// granting a permanent effect that survives Migration.
    /// </summary>
    public sealed class FolioSpreadDef
    {
        public string Id { get; set; }
        public string Name { get; set; }

        /// <summary>Resource ids whose Pristine specimens the spread asks for, one fixed each.</summary>
        public List<string> Entries { get; set; } = new List<string>();

        public List<EffectDef> Effects { get; set; } = new List<EffectDef>();
    }
}
