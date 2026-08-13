using System.Collections.Generic;

namespace Wildgrove.Game
{
    /// <summary>
    /// Which of the journal's folding cards stand open. Most of them are the
    /// Record page's: the back pages are the longest in the journal by a wide
    /// margin — every gatherable and every recipe, nine Folio spreads, eight
    /// insect plates and the Wheel's shelf, all in one scroll — so the cards
    /// that only <em>record</em> fold shut behind their own tally, and the
    /// cards you <em>act on</em> stay open.
    /// <para>
    /// The keeping is the exception that made this a journal-wide rule rather
    /// than the Record's own (2026-08-13). It is a card you act on and it still
    /// folds, because it is the tallest thing in the book — a plate, a standing
    /// line and five slot rows, near a full phone viewport — and it stood at the
    /// head of the Trail for the ~68% of the year a tide holds, pushing every
    /// gathering plate off the screen. What it costs in reach it gets back
    /// twice over: the rail's cell is on screen at every tab and wears the moss
    /// the moment a slot can be answered, and the tide's own sheet reads every
    /// slot without opening anything. Both doors open the fold on the way
    /// through (see <c>GameHud.ScrollToOnTrail</c>), so the card is never a shut
    /// drawer at the end of a link.
    /// </para>
    /// <para>
    /// That rule is why this isn't <see cref="JournalZones"/>. The Trail's
    /// default for its grounds is positional (whichever ground is newest stands
    /// open, so the page shortens itself as the trail grows) and a card has no
    /// newest entry. Here the default is what a card is <em>for</em>: the
    /// Almanac is where a fold's Verdure is spent, which is why a player opens
    /// the back pages the minute a fold lands, and the rest answer questions
    /// that are asked on purpose.
    /// </para>
    /// <para>
    /// An id nobody has a rule for stands open. A card that defaulted shut by
    /// accident would be a card with no way in — the fold is its only door.
    /// </para>
    /// <para>
    /// Public for the same reason as <see cref="JournalZones"/> and
    /// <see cref="JournalLayout"/>: the tests are a separate assembly, and there
    /// is no InternalsVisibleTo anywhere in the project.
    /// </para>
    /// </summary>
    public static class JournalCardFolds
    {
        /// <summary>
        /// The Almanac. It FOLDS like every other card on the back pages, and
        /// it is the only one that arrives open — see <see cref="ShutUnasked"/>.
        /// It was a plain unfoldable card until 2026-08-14, which made "the card
        /// the page opens on" and "the card that cannot be put away" the same
        /// fact by accident: a warden with no Verdure to spend had a tree of
        /// learned lines nailed to the top of the page and no head to press.
        /// </summary>
        public const string Almanac = "almanac";

        public const string Compendium = "compendium";
        public const string Folio = "folio";
        public const string DeepPages = "deep-pages";
        public const string Wheel = "wheel";

        /// <summary>
        /// The Trail's keeping card. The string is also the landmark name the
        /// tracker's tide row and the tide sheet deep-link to, deliberately:
        /// the fold and the door are the same card, and two spellings of it
        /// would be two things to keep in step.
        /// </summary>
        public const string Keeping = "keeping";

        /// <summary>
        /// A crafting station's card on the Camp page (<c>CampPage.Crafting</c>).
        /// Built from the station id because the stations are content: the fire,
        /// the bench and the forge are data, and a run's data can name a fourth.
        /// </summary>
        public static string Station(string stationId)
        {
            return stationId == null ? null : "station-" + stationId;
        }

        /// <summary>
        /// The cards that keep their contents folded away until asked for —
        /// every Record card but the Almanac, and the Trail's keeping.
        /// <para>
        /// The Folio joined them on 2026-08-14, so the Record page now opens on
        /// the Almanac alone. It was the one card left out, on the grounds that
        /// it is the only Record card with a button — but a button is not what
        /// made the Almanac worth the height, the Verdure is: the Almanac is
        /// where a fold's whole reward is spent, and it is asked for the moment
        /// a fold lands. The Folio is nine spreads of plates, each a strip of
        /// specimens, and pressing one is a thing done when a Choice find turns
        /// up rather than on the way past. Shut, its head still carries the
        /// count of spreads pressed, which is the question the page is opened
        /// with.
        /// </para>
        /// <para>
        /// The Almanac is <em>absent</em> from this set rather than unfoldable
        /// (also 2026-08-14). Every card on the back pages now folds, so the
        /// page has one rule and no exceptions to it, and being the card the
        /// page opens on is a default the player can overrule like any other —
        /// which matters most for the warden it is least use to, the one with
        /// nothing unspent to spend on it.
        /// </para>
        /// <para>
        /// The station cards are not in here and cannot be: which one stands
        /// open is positional, the Trail's kind of rule rather than this one
        /// (<see cref="JournalZones"/>), so they hand their own default to the
        /// overloads below.
        /// </para>
        /// </summary>
        private static readonly HashSet<string> ShutUnasked = new HashSet<string>
        {
            Compendium,
            Folio,
            DeepPages,
            Wheel,
            Keeping,
        };

        /// <summary>
        /// Whether a card nobody has pressed stands open, by what the card is
        /// FOR — the rule this class exists to hold. Cards with a positional
        /// default don't ask it; they pass their own answer instead.
        /// </summary>
        public static bool OpenUnasked(string cardId)
        {
            return cardId != null && !ShutUnasked.Contains(cardId);
        }

        /// <summary>
        /// Whether <paramref name="cardId"/>'s contents are drawn.
        /// <paramref name="choices"/> holds only the cards the player has
        /// pressed; everything else falls through to the rule above.
        /// </summary>
        public static bool IsOpen(IDictionary<string, bool> choices, string cardId)
        {
            return IsOpen(choices, cardId, OpenUnasked(cardId));
        }

        /// <summary>
        /// <see cref="IsOpen(IDictionary{string,bool},string)"/> for a card whose
        /// unasked default is the caller's to decide — the station cards, where
        /// it is "this is the one the work is standing at" and so cannot be a
        /// set of ids known here.
        /// </summary>
        public static bool IsOpen(IDictionary<string, bool> choices, string cardId, bool openUnasked)
        {
            if (cardId == null)
            {
                return false;
            }

            if (choices != null && choices.TryGetValue(cardId, out var chosen))
            {
                return chosen;
            }

            return openUnasked;
        }

        /// <summary>Fold the card the other way, and remember that the player said so.</summary>
        public static void Toggle(IDictionary<string, bool> choices, string cardId)
        {
            Toggle(choices, cardId, OpenUnasked(cardId));
        }

        /// <summary>
        /// Fold a card whose default the caller owns. The default has to come
        /// back in here as well as into <see cref="IsOpen"/>: a card drawn shut
        /// by a positional rule and toggled against this class's own would be
        /// set shut a second time, and the head would do nothing.
        /// </summary>
        public static void Toggle(IDictionary<string, bool> choices, string cardId, bool openUnasked)
        {
            if (choices == null || cardId == null)
            {
                return;
            }

            choices[cardId] = !IsOpen(choices, cardId, openUnasked);
        }
    }
}
