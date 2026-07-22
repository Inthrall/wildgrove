using UnityEngine;

namespace Wildgrove.Game
{
    /// <summary>
    /// The Warden's Journal palette and tab identifiers, lifted from the
    /// docs/wildgrove-journal.html mock's CSS custom properties. Shared by every
    /// journal view via <c>using static</c>.
    /// </summary>
    internal static class JournalTheme
    {
        // The journal palette, lifted from the mock's CSS custom properties.
        internal static readonly Color PagePaper = new Color(0.949f, 0.918f, 0.839f, 1f);      // #F2EAD6
        internal static readonly Color CardPaper = new Color(0.918f, 0.878f, 0.776f, 1f);      // #EAE0C6
        internal static readonly Color DeepPaper = new Color(0.867f, 0.812f, 0.682f, 1f);      // #DDCFAE
        internal static readonly Color Ink = new Color(0.227f, 0.192f, 0.149f, 1f);            // #3A3126
        internal static readonly Color Ink2 = new Color(0.431f, 0.376f, 0.278f, 1f);           // #6E6047
        internal static readonly Color RulePaper = new Color(0.804f, 0.741f, 0.6f, 1f);        // #CDBD99
        internal static readonly Color MossWash = new Color(0.83f, 0.815f, 0.7f, 1f);          // paper washed with moss
        internal static readonly Color OchreWash = new Color(0.885f, 0.79f, 0.69f, 1f);        // paper washed with ochre
        internal static readonly Color MossDeep = new Color(0.333f, 0.392f, 0.247f, 1f);       // #55643F
        internal static readonly Color Ochre = new Color(0.627f, 0.353f, 0.212f, 1f);          // #A05A36
        internal static readonly Color DimColor = new Color(0.09f, 0.075f, 0.051f, 0.78f);
        internal static readonly Color NightInk = new Color(0.09f, 0.075f, 0.051f, 1f);        // #17130D
        internal static readonly Color NightText = new Color(0.91f, 0.863f, 0.753f, 1f);       // #E8DCC0

        internal const string InkHex = "#3A3126";
        internal const string Ink2Hex = "#6E6047";
        internal const string OchreHex = "#A05A36";
        // Ochre's small-text sibling: #A05A36 only manages ~4:1 contrast on
        // card paper, which fails at hint sizes — warnings inline in body
        // copy use this darker mix instead.
        internal const string OchreInkHex = "#7E421F";
        internal const string MossDeepHex = "#55643F";

        internal const string TabTrail = "trail";
        internal const string TabCamp = "camp";
        internal const string TabWarden = "warden";
        internal const string TabRecord = "record";
        internal static readonly string[] Tabs = { TabTrail, TabCamp, TabWarden, TabRecord };
    }
}
