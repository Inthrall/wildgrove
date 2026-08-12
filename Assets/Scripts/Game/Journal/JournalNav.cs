using UnityEngine;

namespace Wildgrove.Game
{
    /// <summary>
    /// The rules behind keyboard and controller navigation of the journal
    /// (design §13 Phase 2, and a Level Up compliance requirement: every
    /// interaction reachable without touch). Pure maths and string work, so
    /// the awkward parts — which tab a shoulder button lands on when the Trail
    /// has no tab, how far to scroll to bring a focused control into view, and
    /// where focus goes after the page is rebuilt underneath it — can be
    /// reasoned about and tested without a device.
    /// </summary>
    /// <remarks>
    /// Public, unlike the rest of the Journal family, for the same reason
    /// <see cref="JournalLayout"/> and <see cref="World.WorldStrip"/> are: the
    /// EditMode tests live in their own assembly.
    /// </remarks>
    public static class JournalNav
    {
        /// <summary>
        /// Padding kept between a focused control and the edge of the page, so
        /// a control never sits flush against the fold.
        /// </summary>
        public const float RevealPad = 12f;

        /// <summary>
        /// The tab a shoulder press (or Q/E) lands on, wrapping at both ends.
        /// While the book is a spread the Trail has no tab of its own — it is
        /// always open on the right page — so stepping skips it rather than
        /// stopping on a tab the player cannot see.
        /// </summary>
        public static string StepTab(string current, int delta, bool wide)
        {
            var tabs = JournalTheme.Tabs;
            if (delta == 0 || tabs.Length == 0)
            {
                return current;
            }

            var index = System.Array.IndexOf(tabs, current);
            if (index < 0)
            {
                index = 0;
            }

            var step = delta > 0 ? 1 : -1;
            // At most one lap: if every other tab is skipped we are back where
            // we started, which is the honest answer rather than a spin.
            for (var i = 0; i < tabs.Length; i++)
            {
                index = (index + step + tabs.Length) % tabs.Length;
                var candidate = tabs[index];
                if (wide && candidate == JournalTheme.TabTrail)
                {
                    continue;
                }

                return candidate;
            }

            return current;
        }

        /// <summary>
        /// Where focus goes when the page it was on has just been rebuilt. The
        /// journal rebuilds whenever its structure changes — a familiar
        /// arrives, a craft finishes — which destroys the focused control
        /// underneath a controller player. Focus returns to the same position
        /// in the page rather than to the top, clamped because the new page may
        /// be shorter. Returns -1 when there is nothing to focus, or when
        /// nothing was focused to begin with.
        /// </summary>
        public static int RestoreIndex(int remembered, int count)
        {
            if (count <= 0 || remembered < 0)
            {
                return -1;
            }

            return Mathf.Min(remembered, count - 1);
        }

        /// <summary>
        /// The scroll position that keeps the reader's place across a rebuild.
        /// <para>
        /// The fresh page is rarely the height the old one was — a rung leaves
        /// the ladder, a station card arrives, a line gains a clause. Keeping
        /// the <em>normalised</em> position keeps the same fraction of a
        /// different page, which is a different place: the page slides under
        /// the finger that has just tapped it. So the distance scrolled is
        /// kept in the page's own pixels and converted back against the new
        /// height, and only a change ABOVE the reader moves anything.
        /// </para>
        /// </summary>
        /// <param name="offsetFromTop">How far down the page the window's top edge sat, in the page's own units.</param>
        /// <param name="range">The new page's height less the window's — how far there is to scroll at all.</param>
        public static float KeptPosition(float offsetFromTop, float range)
        {
            if (range <= 0f)
            {
                // The whole page fits the window, so the top is the only place
                // it can be — and dividing by the range would be a divide by zero.
                return 1f;
            }

            return 1f - Mathf.Clamp01(offsetFromTop / range);
        }

        /// <summary>
        /// The scroll position that brings a control into view, or the current
        /// position when it is already visible. Scrolls the shortest distance
        /// that works — a focused control one row below the fold should bring
        /// the page up by a row, not jump the row to the top of the page (which
        /// is what a landmark deep-link deliberately does instead).
        /// </summary>
        /// <param name="current">Current vertical normalised position: 1 is the top of the page, 0 the bottom (uGUI's convention).</param>
        /// <param name="contentHeight">Height of the whole page.</param>
        /// <param name="viewportHeight">Height of the window onto it.</param>
        /// <param name="targetTop">The control's distance from the top of the page.</param>
        /// <param name="targetHeight">The control's height.</param>
        public static float RevealPosition(float current, float contentHeight, float viewportHeight,
            float targetTop, float targetHeight, float pad)
        {
            var range = contentHeight - viewportHeight;
            if (range <= 0f)
            {
                // The whole page fits — there is nowhere to scroll to.
                return current;
            }

            current = Mathf.Clamp01(current);
            // How far down the page the top of the window sits.
            var windowTop = (1f - current) * range;
            var windowBottom = windowTop + viewportHeight;

            var wanted = windowTop;
            if (targetTop - pad < windowTop)
            {
                wanted = targetTop - pad;
            }
            else if (targetTop + targetHeight + pad > windowBottom)
            {
                wanted = targetTop + targetHeight + pad - viewportHeight;
            }
            else
            {
                return current;
            }

            return Mathf.Clamp01(1f - Mathf.Clamp(wanted, 0f, range) / range);
        }
    }
}
