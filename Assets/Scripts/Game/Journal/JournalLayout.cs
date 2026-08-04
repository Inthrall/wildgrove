using UnityEngine;

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

        /// <summary>
        /// The widest the book is ever drawn, in canvas units: two
        /// reference-width pages and the spine gap between them. Past this the
        /// margins grow instead of the columns — a 32:9 canvas measures ~2700
        /// units across, and a page that wide runs the type past any sensible
        /// measure while the chrome's centred lines strand themselves in the
        /// middle of a metre of paper. (Not letterboxing: the margins ARE the
        /// world camera's paper, so nothing is masked off.)
        /// </summary>
        public const float MaxWidth = 2f * 1080f + SpreadGap;

        /// <summary>
        /// The page's side margin in canvas units — the base margin, widened to
        /// centre the book once the canvas is broader than <see cref="MaxWidth"/>.
        /// </summary>
        public static int SideMargin(float width, int margin)
        {
            if (width <= MaxWidth)
            {
                return margin;
            }

            return margin + Mathf.RoundToInt((width - MaxWidth) * 0.5f);
        }

        // ── The vertical budget ──────────────────────────────────────────────
        // Three rows compete for the canvas height: the pinned chrome (fixed,
        // and measured), the world strip's band, and the open page. The shares
        // below are of the WHOLE canvas height, not of what the chrome leaves.

        /// <summary>The strip band's ceiling, as a share of canvas height, in a single column.</summary>
        public const float StripShareMax = 0.26f;

        /// <summary>The strip band's floor, as a share of canvas height, in a single column.</summary>
        public const float StripShareMin = 0.14f;

        /// <summary>What the open page keeps before the band may grow, in a single column.</summary>
        public const float MinPageShare = 0.32f;

        // A spread is a landscape shape, and landscape is where height is
        // scarce: a 16:9 canvas is ~1080 units tall against a phone's 1920,
        // while the chrome above the page costs the same absolute units on
        // both. Held at the portrait shares the band took a quarter of that
        // short canvas — a deep, near-empty band with three plates adrift in it
        // — and the page underneath was squeezed to its floor and clipped by
        // the tabs. Wide, the band gives up height it does not need (it is
        // twice as broad, so the plates spread sideways instead of stacking —
        // see WorldStrip.PerRowCap) and the page takes the difference.

        /// <summary>The strip band's ceiling, as a share of canvas height, on a spread.</summary>
        public const float WideStripShareMax = 0.17f;

        /// <summary>The strip band's floor, as a share of canvas height, on a spread.</summary>
        public const float WideStripShareMin = 0.1f;

        /// <summary>What the open page keeps before the band may grow, on a spread.</summary>
        public const float WideMinPageShare = 0.44f;

        /// <summary>
        /// The world strip's band height, in the same units as
        /// <paramref name="available"/>: whatever the pinned chrome and the
        /// page's floor don't need, clamped to the layout's share of the canvas
        /// so the band neither swells into empty paper on a tall screen nor
        /// collapses below a readable row on a short one.
        /// </summary>
        /// <param name="available">The canvas height the root has to divide.</param>
        /// <param name="chrome">Measured height of every pinned row — header, ledger, note, tracker, tabs — plus padding and spacing.</param>
        /// <param name="wide">True on a spread (see <see cref="IsWide"/>).</param>
        public static float StripHeight(float available, float chrome, bool wide)
        {
            if (available <= 0f)
            {
                return 0f;
            }

            var pageFloor = available * (wide ? WideMinPageShare : MinPageShare);
            var floor = available * (wide ? WideStripShareMin : StripShareMin);
            var ceiling = available * (wide ? WideStripShareMax : StripShareMax);
            return Mathf.Clamp(available - chrome - pageFloor, floor, ceiling);
        }
    }
}
