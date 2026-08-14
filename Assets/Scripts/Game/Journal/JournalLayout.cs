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

        /// <summary>
        /// One journal tab's width where the tabs stand in a bar — a preference
        /// the phone's column is free to refuse, since it shares 1,048 units
        /// between five of them.
        /// </summary>
        public const float SpreadTabWidth = 260f;

        /// <summary>
        /// The fore-edge rail's width, in canvas units — what the tabs cost a
        /// spread once they leave the bottom edge (see
        /// <c>GameHud.ApplyTabFold</c>). Wide enough to seat the longest tab
        /// name at the journal's small-caps size with a margin either side.
        /// <para>
        /// This is the trade the whole landscape pass turns on, and it is a good
        /// one because the two axes are not worth the same: the bar cost 132 of
        /// a ~1,150-unit height (11%), the rail costs 170 of a ~2,000-unit width
        /// (8%), and it is height that landscape has none of. Each page of the
        /// spread narrows by ~95 units — about a tenth — and still keeps a
        /// measure inside what the type was cut for.
        /// </para>
        /// </summary>
        public const float RailTabWidth = 170f;

        /// <summary>How deep a tab is in either home — 132 units ≈ 53dp, clear of Android's 48dp touch floor.</summary>
        public const float TabDepth = 132f;

        /// <summary>The breath between two tabs standing in the rail.</summary>
        public const float RailTabSpacing = 6f;

        /// <summary>
        /// How much of a page's height a rail of <paramref name="tabs"/> takes.
        /// The rail cannot scroll and must not be clipped — a tab a player
        /// cannot reach is a page they cannot open — so this has to fit the
        /// shortest spread the book ever draws.
        /// </summary>
        public static float RailDepth(int tabs)
        {
            return tabs <= 0 ? 0f : tabs * TabDepth + (tabs - 1) * RailTabSpacing;
        }

        /// <summary>
        /// What the chrome above the page costs on a spread, in canvas units:
        /// the title's line (which carries the ledger and the tracker banner
        /// too), its rule, and the root's own padding and spacing. A constant
        /// rather than a measurement because <see cref="RailSeatsTabs"/> is
        /// asked before any layout pass has run, and it only has to be close —
        /// it decides which edge the tabs stand on, and the shapes anywhere
        /// near the threshold are ones nobody holds a game on.
        /// </summary>
        private const float SpreadChrome = 110f;

        /// <summary>
        /// True when a spread's page is deep enough to seat the tabs down its
        /// fore-edge.
        /// <para>
        /// Wide is not the same question as tall enough. A 32:9 canvas measures
        /// ~760 units from top to bottom — it is emphatically a spread, and
        /// there is no room in it for a column of four fingertips beside the
        /// page. The tabs go back to the bar along the bottom there, where a
        /// row of them always fits however short the screen is; the rail is an
        /// answer to height being scarce, and on that shape height is not
        /// scarce so much as absent.
        /// </para>
        /// </summary>
        public static bool RailSeatsTabs(float canvasHeight, int tabs)
        {
            if (canvasHeight <= 0f)
            {
                return false;
            }

            // The least the page is ever left with: the chrome above it, and
            // the band at its ceiling.
            var page = canvasHeight - SpreadChrome - canvasHeight * WideStripShareMax;
            return RailDepth(tabs) <= page;
        }

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
        /// The widest ONE page is ever drawn, in canvas units — the reference
        /// width, which is the measure every authored type size was chosen
        /// against. Past this the margins grow instead of the column.
        /// </summary>
        public const float PageMaxWidth = 1080f;

        /// <summary>
        /// The widest the book is ever drawn, in canvas units: two
        /// reference-width pages and the spine gap between them. Past this the
        /// margins grow instead of the columns — a 32:9 canvas measures ~2700
        /// units across, and a page that wide runs the type past any sensible
        /// measure while the chrome's centred lines strand themselves in the
        /// middle of a metre of paper. (Not letterboxing: the margins ARE the
        /// world camera's paper, so nothing is masked off.)
        /// </summary>
        public const float MaxWidth = 2f * PageMaxWidth + SpreadGap;

        /// <summary>
        /// The page's side margin in canvas units — the base margin, widened to
        /// centre the book once the canvas is broader than the book can be
        /// drawn: <see cref="MaxWidth"/> on a spread, and one
        /// <see cref="PageMaxWidth"/> in a single column.
        /// <para>
        /// A column was capped at the SPREAD's width until 2026-08-14, which is
        /// two pages' worth of measure for one page: a 4:3 tablet held upright
        /// ran its body text 1,215 units wide against the 1,048 the type was
        /// cut for, and would have run it to 1,935 once
        /// <see cref="RoomFactor"/> started handing big screens their units.
        /// Capped at one page, every shape the game runs on converges on the
        /// same ~1,050-unit measure, and what a larger screen buys is more rows
        /// and wider margins rather than longer lines.
        /// </para>
        /// </summary>
        /// <param name="wide">True on a spread (see <see cref="IsWide"/>) — two pages are being fitted, not one.</param>
        public static int SideMargin(float width, int margin, bool wide)
        {
            var cap = wide ? MaxWidth : PageMaxWidth;
            if (width <= cap)
            {
                return margin;
            }

            return margin + Mathf.RoundToInt((width - cap) * 0.5f);
        }

        // ── How much room the canvas is given ────────────────────────────────
        // Every measurement in the journal is in canvas units, and how many of
        // them a screen gets is the CanvasScaler's decision. At
        // ScaleWithScreenSize with matchWidthOrHeight 0.5 against a 1080x1920
        // reference, that decision comes out as
        //
        //     scaleFactor = sqrt(pixelWidth · pixelHeight) / 1440
        //
        // — the square root of the pixel AREA. Which means the journal holds
        // the same FRACTION of every screen it is ever drawn on. On phones that
        // is exactly right. On a tablet it means the book is a magnified phone:
        // the same nine cards, each one physically twice the size, on a screen
        // held no closer to the eye.
        //
        // The numbers, for a 120-unit plate (the touch floor everything here is
        // cut to): 48dp on the 5.5" 1080p phone it was measured against, 72dp
        // on an 8" tablet, 90dp on a 10.1", 120dp on a 13". Two and a half
        // times the size a fingertip asks for, paid in content nobody sees.

        /// <summary>
        /// The screen the authored units were cut for, as the geometric mean of
        /// its width and height in inches: a 5.5" 16:9 phone, on which a
        /// 120-unit plate lands on Android's 48dp touch floor exactly.
        /// </summary>
        public const float ReferenceInches = 3.6f;

        /// <summary>
        /// The density that screen has — 1440px of geometric mean over
        /// <see cref="ReferenceInches"/>. What the Editor's plain game view is
        /// answered with, since the only density it can otherwise report is the
        /// developer's monitor (see <c>DeviceForm.ScreenDpi</c>).
        /// </summary>
        public const float ReferenceDpi = 1440f / ReferenceInches;

        /// <summary>
        /// The largest screen still held as a phone — a 6.7" 20:9, near enough
        /// — and so the size below which nothing changes at all.
        /// <para>
        /// The curve starts at the biggest PHONE rather than at the reference
        /// one because the whole argument for handing a screen more room is
        /// that it is a tablet: held further away, on a desk or two hands, with
        /// a lap between it and the eye. A 6.7" phone is a 5.5" phone held the
        /// same way, and shrinking its marks by six percent would buy half a
        /// card for no reason anybody could name.
        /// </para>
        /// </summary>
        public const float PhoneInches = 4.2f;

        /// <summary>
        /// The most room any screen is given, however large it reports itself.
        /// A cap rather than a curve past this point: beyond ~11" the reading
        /// distance grows with the screen, so the marks should start growing
        /// again too.
        /// </summary>
        public const float MaxRoom = 1.6f;

        // Outside this, a screen is not a handheld panel and is not believed:
        // Android runs ~120dpi (an old 7" tablet) to ~640 (a QHD flagship), and
        // a reading far outside that is a device lying or a platform that isn't
        // one. Unknown takes the phone's answer, never the roomiest — being
        // wrong toward "more room" is what put a tablet's layout on a phone.
        private const float MinPlausibleDpi = 100f;
        private const float MaxPlausibleDpi = 900f;

        /// <summary>
        /// How many times more canvas units than a phone this screen should be
        /// given — the square root of how much bigger it physically is.
        /// <para>
        /// The square root is the whole design. Room of 1 is what ships today:
        /// the journal keeps a constant share of the screen, and a tablet is a
        /// magnified phone. Room of <c>size / PhoneInches</c> is the other
        /// extreme — constant PHYSICAL size, where a 13" tablet shows six times
        /// the content of a phone at the same mark size, which is a spreadsheet,
        /// not a book held at arm's length. Halfway between them in the exponent
        /// gives a mark that grows with the screen but more slowly than the
        /// screen does, which is what a reader actually wants.
        /// </para>
        /// <para>
        /// It carries its own safety proof, and this is why it is written as a
        /// factor rather than as a table of devices. Substituting
        /// <c>room = sqrt(size / PhoneInches)</c> back into the dp a plate lands
        /// at gives <c>dp = (units / 9) · sqrt(PhoneInches · size)</c> — the
        /// geometric mean of the phone's dp and this screen's. Two things
        /// follow, and both are pinned in the tests. Nothing that gets room can
        /// land under <c>(units / 9) · PhoneInches</c>, which is 56dp for a
        /// plate — comfortably over the 48 floor. And nothing that doesn't gets
        /// touched at all: under <see cref="PhoneInches"/> the clamp holds room
        /// at 1, so every phone is left exactly as it is.
        /// </para>
        /// <para>
        /// A screen that will not say how big it is takes the PHONE's answer,
        /// never the roomiest — the failure has to fall toward the layout that
        /// is right for the smallest device, because a tablet's page at a
        /// phone's size is unreadable while the reverse is merely generous.
        /// That covers a dpi of zero, one outside any plausible handheld panel,
        /// and the Editor's plain game view, which reports the developer's
        /// monitor and is answered before it ever reaches here (see
        /// <c>DeviceForm.ScreenDpi</c>).
        /// </para>
        /// </summary>
        /// <param name="widthPx">Screen width in pixels — <c>Screen.width</c>, not a canvas measure.</param>
        /// <param name="heightPx">Screen height in pixels.</param>
        /// <param name="dpi">The screen's density, from <c>DeviceForm.ScreenDpi</c>. Unknown or implausible takes the phone's answer.</param>
        public static float RoomFactor(float widthPx, float heightPx, float dpi)
        {
            if (widthPx <= 0f || heightPx <= 0f
                || dpi < MinPlausibleDpi || dpi > MaxPlausibleDpi)
            {
                return 1f;
            }

            // Geometric mean rather than the diagonal: it is the mean that makes
            // the dp proof above hold, and it does not reward a screen for being
            // long and thin — a 20:9 phone is not a tablet.
            var inches = Mathf.Sqrt(widthPx * heightPx) / dpi;
            return Mathf.Clamp(Mathf.Sqrt(inches / PhoneInches), 1f, MaxRoom);
        }

        /// <summary>
        /// The reference resolution to hand a <c>CanvasScaler</c>: the authored
        /// 1080x1920, opened out by <see cref="RoomFactor"/>. A reference n
        /// times larger is a canvas n times larger in units, so every measure in
        /// the journal keeps its meaning and simply has more room to stand in.
        /// <para>
        /// EVERY canvas the game draws has to be handed the same answer — the
        /// HUD's, the events rail's (which lays itself against the world band by
        /// hand, in the HUD's units) and the FPS counter's. Two scalers that
        /// disagree put the rail somewhere other than the band it belongs to.
        /// </para>
        /// </summary>
        public static Vector2 ReferenceResolution(float widthPx, float heightPx, float dpi)
        {
            var room = RoomFactor(widthPx, heightPx, dpi);
            return new Vector2(1080f * room, 1920f * room);
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
