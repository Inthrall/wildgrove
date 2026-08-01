using System.Collections.Generic;

namespace Wildgrove.Game
{
    /// <summary>
    /// Which grounds the Trail page has open. Eight zones of gathering plates
    /// is a very long scroll to walk past to reach the newest ground, so each
    /// zone folds shut behind its name and only one stands open by default.
    /// <para>
    /// The rule, and the reason it isn't just a set of open ids: <b>a zone the
    /// player has never touched is open only while it is the newest.</b> So the
    /// page stays short on its own — when a trail map opens the next ground,
    /// the one before it folds shut without being asked, because it was never
    /// opened by hand, only by being new. A zone the player did open stays
    /// open, newest or not; a newest zone the player closed stays closed.
    /// </para>
    /// <para>
    /// Public for the same reason as <see cref="JournalNav"/> and
    /// <see cref="JournalLayout"/>: the tests are a separate assembly, and
    /// there is no InternalsVisibleTo anywhere in the project.
    /// </para>
    /// </summary>
    public static class JournalZones
    {
        /// <summary>
        /// Whether <paramref name="zoneId"/>'s plates are drawn.
        /// <paramref name="choices"/> holds only the zones the player has
        /// pressed; everything else falls through to "the newest one".
        /// </summary>
        public static bool IsOpen(IDictionary<string, bool> choices, string zoneId, string newestZoneId)
        {
            if (choices != null && zoneId != null && choices.TryGetValue(zoneId, out var chosen))
            {
                return chosen;
            }

            return zoneId != null && zoneId == newestZoneId;
        }

        /// <summary>
        /// Fold the zone the other way, and remember that the player said so —
        /// which is what takes it out of the newest-only default for good.
        /// </summary>
        public static void Toggle(IDictionary<string, bool> choices, string zoneId, string newestZoneId)
        {
            if (choices == null || zoneId == null)
            {
                return;
            }

            choices[zoneId] = !IsOpen(choices, zoneId, newestZoneId);
        }
    }
}
