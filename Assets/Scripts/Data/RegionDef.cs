using System.Collections.Generic;

namespace Wildgrove.Data
{
    /// <summary>
    /// A region modifier (design doc §8): the flavour a run-2+ region arrives
    /// with — lush, misted, ashen. Effects use the shared effect vocabulary
    /// and stay live for the whole run; the sign is the land's one line about
    /// the season, spoken at the fold.
    /// </summary>
    public sealed class RegionDef
    {
        public string Id { get; set; }

        /// <summary>Completes the fold forecast's "ahead: …" line — authored with its article ("a misted region").</summary>
        public string Name { get; set; }

        /// <summary>The one line the land offers about the season, shown with the Migration vignette.</summary>
        public string Sign { get; set; }

        public List<EffectDef> Effects { get; set; } = new List<EffectDef>();
    }
}
