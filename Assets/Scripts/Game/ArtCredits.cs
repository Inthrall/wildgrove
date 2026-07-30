namespace Wildgrove.Game
{
    /// <summary>
    /// The attributions the art licences oblige the shipped game to carry.
    ///
    /// Most of the plates behind <see cref="ArtLibrary"/> are public domain and
    /// owe nothing. Five source works are CC BY, which obliges credit by name,
    /// the licence, a link to it, and a note that the work was changed — and an
    /// obligation stated only in a repo file is not carried by the build, so it
    /// lives here and is shown in the journal's colophon.
    ///
    /// `Assets/Resources/Art/CREDITS.md` remains the full ledger of every plate
    /// and its provenance; this is only the part that must be visible in game.
    /// Add a line here the same day a CC-licensed work joins the art.
    /// </summary>
    public static class ArtCredits
    {
        /// <summary>One CC BY source work and what the game did to it.</summary>
        public sealed class Work
        {
            public string title;
            public string author;
            public string licence;
            public string licenceUrl;

            /// <summary>How the work was changed — CC BY requires the modification be stated.</summary>
            public string change;
        }

        /// <summary>What the plates are, in the journal's own words, above the list.</summary>
        public const string Preamble =
            "The plates in this book are the work of other hands, long out of copyright, "
            + "gathered from the naturalists and engravers who drew them first. Five are "
            + "still under licence, and are named here with thanks.";

        /// <summary>The public-domain remainder, acknowledged without obligation.</summary>
        public const string PublicDomainNote =
            "Every other plate is public domain or CC0, chiefly Ernest Protheroe's "
            + "<i>The handy natural history</i> (1910), Kirby &amp; Schubert's <i>Natural history "
            + "of the animal kingdom</i> (1889), J. G. Kurr's mineral atlas (1859), Sturm's and "
            + "Köhler's botanical plates, Audubon, and the Pearson Scott Foresman drawings.";

        /// <summary>The CC BY works. Each must appear in the shipped credits.</summary>
        public static readonly Work[] Licensed =
        {
            new Work
            {
                title = "Fly in amber, Samland Peninsula along the Baltic Sea RU",
                author = "James St. John",
                licence = "CC BY 2.0",
                licenceUrl = "creativecommons.org/licenses/by/2.0",
                change = "cropped and trimmed",
            },
            new Work
            {
                title = "Dolichoderus longipilosus SMFBE1244 specimen tag and amber",
                author = "Vincent Perrichot",
                licence = "CC BY 4.0",
                licenceUrl = "creativecommons.org/licenses/by/4.0",
                change = "cropped and trimmed",
            },
            new Work
            {
                title = "Orchid, rotted and azalea peats, leaf mould, live sphagnum moss…",
                author = "C. W. Brownell & Co.",
                licence = "CC BY 2.0",
                licenceUrl = "creativecommons.org/licenses/by/2.0",
                change = "cropped and trimmed",
            },
            new Work
            {
                title = "Butterfly (game-icons.svg)",
                author = "Lorc",
                licence = "CC BY 3.0",
                licenceUrl = "creativecommons.org/licenses/by/3.0",
                change = "re-baked to sepia ink",
            },
            new Work
            {
                title = "Pickaxe icon (white).svg",
                author = "Arthur Shlain",
                licence = "CC BY 3.0",
                licenceUrl = "creativecommons.org/licenses/by/3.0",
                change = "re-baked to sepia ink",
            },
        };

        /// <summary>One credit line: "Title by Author, CC BY 2.0 (link), cropped and trimmed".</summary>
        public static string Line(Work work)
        {
            if (work == null)
            {
                return string.Empty;
            }

            return work.title + " by " + work.author + ", " + work.licence
                   + " (" + work.licenceUrl + "), " + work.change + ".";
        }
    }
}
