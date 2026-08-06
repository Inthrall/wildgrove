namespace Wildgrove.Game
{
    /// <summary>
    /// What the inside cover says about where the book is kept. The wording is
    /// derived here rather than in the sheet because the interesting part is the
    /// bookkeeping — which of "signed out", "the last copy didn't go up" and "a
    /// further-along run was taken up from another device" is the one thing
    /// worth saying — and that is worth pinning with tests.
    /// <para>
    /// Everything it reports was already true and silent: the run is written
    /// every 30 s and mirrored to Play Games each time, and
    /// <see cref="RunPersistence.Reconcile"/> can swap the run under the player
    /// without a word.
    /// </para>
    /// </summary>
    public static class SaveStanding
    {
        /// <summary>Below this the writing reads as "just now" rather than a number of seconds.</summary>
        private const double JustNowSeconds = 5.0;

        /// <summary>When the run was last written down, in the journal's voice.</summary>
        public static string WrittenLine(long lastSavedUnixMs, long nowUnixMs)
        {
            var seconds = (nowUnixMs - lastSavedUnixMs) / 1000.0;
            if (seconds < JustNowSeconds)
            {
                return "Written down just now.";
            }

            return "Written down " + NumberFormat.Duration(seconds) + " ago.";
        }

        /// <summary>
        /// Where the copy lives — and the one thing that is off, if anything is.
        /// Order matters: signed out explains a stale copy on its own, so it is
        /// said instead of a failure rather than alongside it.
        /// </summary>
        public static string CloudLine(bool signedIn, bool lastCloudWriteFailed, bool adoptedThisSession)
        {
            if (!signedIn)
            {
                return "Play Games is not signed in, so this book lives on this device alone.";
            }

            if (lastCloudWriteFailed)
            {
                return "Play Games would not take the last copy. It is offered again "
                       + "every time the book is written.";
            }

            if (adoptedThisSession)
            {
                return "Kept with Play Games. This run was taken up from another device "
                       + "when the book opened, because it was further along than the one here.";
            }

            return "Kept with Play Games, and copied up every time the book is written.";
        }
    }
}
