namespace Wildgrove.Game
{
    /// <summary>
    /// The journal's responsive rules (design §13 Phase 2, and a Level Up
    /// compliance requirement: no letterboxing on 4:3 / 16:10 / 21:9). Pure
    /// maths over canvas-space sizes so the breakpoint can be reasoned about
    /// and tested without a screen.
    /// <para>
    /// The mock (<c>docs/wildgrove-journal.html</c>) turns two-column at a
    /// CSS min-width of 880px against a ~430px phone column — a little over
    /// double. Canvas units are not CSS pixels, so the port asks the question
    /// the mock was really asking: <em>is this shape wide enough to read two
    /// pages side by side?</em> That is an aspect question, not a pixel one —
    /// under ScaleWithScreenSize a 4:3 tablet held in portrait is physically
    /// broad but still a column, while a landscape phone is not much wider in
    /// canvas units yet clearly wants the spread.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Public, unlike the rest of the Journal family, for the same reason
    /// <see cref="World.WorldStrip"/> is: the EditMode tests live in their own
    /// assembly and this is maths worth pinning.
    /// </remarks>
    public static class JournalLayout
    {
        /// <summary>Wider than this (w/h) and the book opens to a spread. 4:3 landscape (1.33) is in; 4:3 portrait (0.75) is not.</summary>
        public const float WideAspect = 1.2f;

        /// <summary>
        /// A floor in canvas units under the spread, so a small or oddly
        /// scaled canvas keeps one readable column rather than two cramped
        /// ones. A landscape phone measures ~1920 canvas units across.
        /// </summary>
        public const float MinWideWidth = 1200f;

        /// <summary>The gap between the two pages of a spread, in canvas units (the mock's 52px column-gap, at journal scale).</summary>
        public const float SpreadGap = 78f;

        /// <summary>True when the canvas should carry the open page and the Trail side by side.</summary>
        public static bool IsWide(float width, float height)
        {
            if (width <= 0f || height <= 0f)
            {
                return false;
            }

            return width >= MinWideWidth && width / height >= WideAspect;
        }
    }
}
