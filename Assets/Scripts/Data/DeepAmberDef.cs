using System.Collections.Generic;

namespace Wildgrove.Data
{
    /// <summary>
    /// The deep amber (design §6/§7): authored pieces of ancient resin
    /// surfaced in order at one zone's observation site — the deep-past lore
    /// window. Completing the set records a plate whose effects persist like
    /// any other (the journal crosses every fold).
    /// </summary>
    public sealed class DeepAmberDef
    {
        /// <summary>The zone whose observation site surfaces the pieces.</summary>
        public string Zone { get; set; }

        /// <summary>Piece find rate per sketcher-hour, before digSpeedMult modifiers.</summary>
        public double FindsPerHour { get; set; }

        /// <summary>Watched hours without a piece that guarantee the next one — the lore must not starve.</summary>
        public double PityHoursWatched { get; set; }

        /// <summary>The completed set's plate name in the journal.</summary>
        public string PlateName { get; set; }

        /// <summary>The finished plate's field note.</summary>
        public string CompletedLore { get; set; }

        /// <summary>Effects the completed plate grants — permanent, Migration-surviving.</summary>
        public List<EffectDef> Effects { get; set; } = new List<EffectDef>();

        /// <summary>The pieces, in the order they surface — the sequence is the story.</summary>
        public List<AmberPieceDef> Pieces { get; set; } = new List<AmberPieceDef>();
    }

    /// <summary>One authored piece of the deep amber and its lore line.</summary>
    public sealed class AmberPieceDef
    {
        public string Id { get; set; }
        public string Name { get; set; }

        /// <summary>The piece's own field note (design §7 register).</summary>
        public string Lore { get; set; }
    }
}
