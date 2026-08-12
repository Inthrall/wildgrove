namespace Wildgrove.Game.World
{
    /// <summary>
    /// The strip's draw order, in one place — every SpriteRenderer and TextMesh
    /// on the band reads its <c>sortingOrder</c> from here.
    /// <para>
    /// It lived as loose literals in four files and drifted into a tie that
    /// showed: a node's plate and a badge's paper disc both sat at 3, so which
    /// one won was down to renderer instance order, and on the frames the plate
    /// won, the specimen drew straight through the medallion beneath it — the
    /// familiars and the warden looked pasted over the crop rather than pinned
    /// under it. A badge MUST occlude the plate it hangs off, so the two need
    /// separate rungs, and the whole stack needs somewhere the next rung can be
    /// added without guessing what is already taken.
    /// </para>
    /// <para>
    /// Two pairs deliberately share a rung, because each pair is two branches of
    /// one choice and can never both draw: a badge shows an icon OR its initial
    /// mark, and a windfall shows an authored plate OR the tinted bead that
    /// stands in for one.
    /// </para>
    /// <para>
    /// One rung is not a sprite at all — the events rail is uGUI, and it reads
    /// its <c>sortingOrder</c> from here because it renders in the camera
    /// rather than in an overlay canvas, precisely so the rungs above it can
    /// pass in front of it. See <c>GameHud.Events.cs</c>.
    /// </para>
    /// Windfalls sit above the whole strip on purpose — one drifts across the
    /// plates and must stay catchable over any of them.
    /// </summary>
    public static class StripLayers
    {
        /// <summary>The node's resource-coloured mount, under everything.</summary>
        public const int NodeDisc = 0;

        /// <summary>The node's naturalist plate, over its mount.</summary>
        public const int NodePlate = 1;

        /// <summary>The assignment badge's paper disc — opaque, so it bites a clean hole in the plate above it.</summary>
        public const int BadgePaper = 2;

        /// <summary>The badge's occupant: the warden's silhouette or a familiar's portrait.</summary>
        public const int BadgeIcon = 3;

        /// <summary>The badge's fallback initial, for a species with no plate yet — never drawn alongside <see cref="BadgeIcon"/>.</summary>
        public const int BadgeMark = 3;

        /// <summary>The badge's inked rim, over its own icon so a portrait can't break the circle.</summary>
        public const int BadgeRim = 4;

        /// <summary>The caption naming the plate — topmost on the strip, so nothing pinned to a plate can clip its glyphs.</summary>
        public const int NodeCaption = 5;

        /// <summary>The events rail's cells (a canvas, not a sprite) — a card laid over the strip, so above the plates and their captions and under everything a windfall carries.</summary>
        public const int RailCell = 6;

        /// <summary>The page a windfall carries, hiding the strip it drifts across — UNDER the clock, so the hairs still radiate over it.</summary>
        public const int WindfallPaper = 7;

        /// <summary>The seedhead a windfall rides on.</summary>
        public const int WindfallMount = 8;

        /// <summary>The tinted bead a windfall wears when its resource has no plate.</summary>
        public const int WindfallSkin = 9;

        /// <summary>The windfall's authored plate — never drawn alongside <see cref="WindfallShine"/>, which belongs to the bead.</summary>
        public const int WindfallPlate = 10;

        /// <summary>The bead's off-centre highlight.</summary>
        public const int WindfallShine = 10;

        /// <summary>The first-run "tap to catch" tag under a windfall.</summary>
        public const int WindfallHint = 11;

        /// <summary>Seed scattered from a caught windfall.</summary>
        public const int WindfallSeed = 12;

        /// <summary>The "+N" rising from a catch — the last thing drawn, at the finger.</summary>
        public const int CatchText = 13;
    }
}
