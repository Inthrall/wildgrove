using System.Collections.Generic;

namespace Wildgrove.Game
{
    /// <summary>
    /// Which of the Record page's cards stand open. The back pages are the
    /// longest in the journal by a wide margin — every gatherable and every
    /// recipe, nine Folio spreads, eight insect plates and the Wheel's shelf,
    /// all in one scroll — so the cards that only <em>record</em> fold shut
    /// behind their own tally, and the cards you <em>act on</em> stay open.
    /// <para>
    /// That rule is why this isn't <see cref="JournalZones"/>. The Trail's
    /// default is positional (whichever ground is newest stands open, so the
    /// page shortens itself as the trail grows) and a record has no newest
    /// entry. Here the default is what a card is <em>for</em>: the Folio holds
    /// specimens waiting to be pressed, which is why a player opens these pages
    /// mid-run at all, and the other three answer questions that are asked on
    /// purpose.
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
    public static class JournalRecordFolds
    {
        public const string Compendium = "compendium";
        public const string Folio = "folio";
        public const string DeepPages = "deep-pages";
        public const string Wheel = "wheel";

        /// <summary>
        /// The cards that keep their contents folded away until asked for. The
        /// Folio is the one of the four left out: it is the only one with a
        /// button on it.
        /// </summary>
        private static readonly HashSet<string> ShutUnasked = new HashSet<string>
        {
            Compendium,
            DeepPages,
            Wheel,
        };

        /// <summary>
        /// Whether <paramref name="cardId"/>'s contents are drawn.
        /// <paramref name="choices"/> holds only the cards the player has
        /// pressed; everything else falls through to the rule above.
        /// </summary>
        public static bool IsOpen(IDictionary<string, bool> choices, string cardId)
        {
            if (cardId == null)
            {
                return false;
            }

            if (choices != null && choices.TryGetValue(cardId, out var chosen))
            {
                return chosen;
            }

            return !ShutUnasked.Contains(cardId);
        }

        /// <summary>Fold the card the other way, and remember that the player said so.</summary>
        public static void Toggle(IDictionary<string, bool> choices, string cardId)
        {
            if (choices == null || cardId == null)
            {
                return;
            }

            choices[cardId] = !IsOpen(choices, cardId);
        }
    }
}
