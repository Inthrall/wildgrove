using UnityEngine;
using Wildgrove.Game.Services;
using Wildgrove.Sim;
using static Wildgrove.Game.JournalTheme;
using static Wildgrove.Game.JournalWidgets;

namespace Wildgrove.Game
{
    /// <summary>
    /// The journal's modal sheets and the camp-actions strip, split across this
    /// file's partials:
    /// <list type="bullet">
    /// <item><c>Chrome</c> — the sheet scaffold: <c>BeginSheet</c>/<c>CloseSheet</c>,
    /// the scrim, the tap guard, the generic confirm.</item>
    /// <item><c>Arrivals</c> — the unbidden good news: waystones, arrivals,
    /// rewards, keepings, bonds, a place at the warden's side.</item>
    /// <item><c>Welcome</c> — the two sheets that bracket an absence: coming
    /// back, and folding the camp.</item>
    /// <item><c>Naming</c> — naming a companion, the warden, the camp.</item>
    /// <item><c>Posts</c> — "who walks here?", asked from a post.</item>
    /// <item><c>Roster</c> — "where shall this one walk?", asked from a roster row.</item>
    /// <item><c>Posting</c> — the world strip's plate drawers for the same question.</item>
    /// <item><c>InsideCover</c>, <c>Events</c> — the inside cover, and the Wheel's
    /// own sheets.</item>
    /// <item><c>CampStrip</c> — the rewarded and purchase buttons at the head of
    /// the Camp page.</item>
    /// </list>
    /// This file keeps the pump that decides which of them the player is shown
    /// next, and the Standing.
    /// </summary>
    internal sealed partial class JournalSheets : JournalSection
    {
        internal JournalSheets(GameHud hud) : base(hud) { }

        // Pumped sheets appear unbidden, one after another, with their buttons
        // in roughly the same place — a guard swallows the taps that were
        // meant for the previous sheet.
        private const float PumpedSheetGuardSeconds = 0.5f;

        internal void PumpSheets()
        {
            if (_sheet != null)
            {
                return;
            }

            // A long absence is still being credited a slice per frame, so
            // there is no figure to greet the player with yet — and no sheet at
            // all until there is. This is what "behind the welcome-back sheet"
            // means: the catch-up runs in the pause the pump already takes on a
            // cold launch (see WelcomeRewardedWaitSeconds), and the player waits
            // on a sheet rather than on a locked frame.
            if (_loop.CatchingUp)
            {
                return;
            }

            // Welcome-back FIRST — it's the context for everything after it;
            // being asked to name a newcomer before being told what happened
            // while away read backwards. The whole pump waits with it, for the
            // same reason: a newcomer must not be announced ahead of the
            // absence they arrived during.
            if (WaitingOnDoubleIt(_loop.PendingOfflineSummary))
            {
                return;
            }

            var summary = _loop.TakePendingOfflineSummary();
            _welcomeWaitDeadline = -1f;
            if (summary != null && summary.creditedSeconds >= GameLoop.WelcomeBackMinSeconds)
            {
                OpenWelcomeSheet(summary);
                AddTapGuard(PumpedSheetGuardSeconds);
                return;
            }

            // Then anything Play Games handed over while we were away. Ahead of
            // the arrivals because a reward can BE an arrival — the Halter's
            // pony is named on the sheet after this one, and being asked to
            // name her before being told where she came from read backwards.
            var reward = _loop.TakePendingReward();
            if (reward != null)
            {
                OpenRewardSheet(reward);
                AddTapGuard(PumpedSheetGuardSeconds);
                return;
            }

            var arrival = _loop.PeekPendingArrival();
            if (arrival != null)
            {
                OpenArrivalSheet(arrival);
                AddTapGuard(PumpedSheetGuardSeconds);
                return;
            }

            var bond = _loop.TakePendingBondCelebration();
            if (bond != null)
            {
                OpenBondSheet(bond);
                AddTapGuard(PumpedSheetGuardSeconds);
                return;
            }

            // After the bond: earning one can be the same beat that opens a
            // place for it, and the companion is the news — the room is why.
            var slots = _loop.TakePendingSlotCelebration();
            if (slots > 0)
            {
                OpenKithSlotSheet(slots);
                AddTapGuard(PumpedSheetGuardSeconds);
                return;
            }

            // A kept tier is earned news, so it goes ahead of the ambient
            // stones — but after the kith beats: a companion is always the
            // bigger moment than a page.
            var keptSabbat = _loop.TakePendingKeepingCelebration(out var keptTier);
            if (keptSabbat != null)
            {
                OpenKeepingSheet(keptSabbat, keptTier);
                AddTapGuard(PumpedSheetGuardSeconds);
                return;
            }

            var waystoneZone = Narrative.NextUnreadWaystone(_loop.State, _loop.Data);
            if (waystoneZone != null)
            {
                OpenWaystoneSheet(waystoneZone);
                AddTapGuard(PumpedSheetGuardSeconds);
                return;
            }

            // Behind the ordinary stones deliberately: the peaks' arrival stone
            // is what says more are coming, so it must never be queued after
            // the first of the ones it announces.
            var finalStone = Narrative.NextFinalWaystone(_loop.State, _loop.Data);
            if (finalStone != null)
            {
                OpenFinalWaystoneSheet(finalStone);
                AddTapGuard(PumpedSheetGuardSeconds);
            }
        }

        /// <summary>
        /// The Standing — the Renown board, set in the journal's own hand rather
        /// than Play Games' overlay. The overlay is unreachable on a modern
        /// target SDK (its bridge extends the framework <c>android.app.Fragment</c>),
        /// so the scores are read through the leaderboards client and drawn here.
        /// Which is the better place for them anyway: the folk you stand among
        /// belong in the book, not in a Google sheet over the top of it.
        /// </summary>
        internal void OpenStandingSheet(LeaderboardEntry[] entries)
        {
            var sheet = BeginSheet();
            MakeText(sheet, "The Standing", 32, TextAnchor.UpperCenter, Ink, _serif);

            if (entries == null || entries.Length == 0)
            {
                MakeText(sheet, entries == null
                        ? "<i>the board would not be read. Play Games kept it shut.</i>"
                        : "<i>no one has yet been recorded here.</i>",
                    20, TextAnchor.MiddleCenter, Ink2, _serif);
                return;
            }

            var unranked = false;
            foreach (var entry in entries)
            {
                // The player's own line is marked rather than moved — the board
                // reads as a ladder, and finding yourself on it is the point.
                var name = string.IsNullOrEmpty(entry.name) ? "a warden" : entry.name;

                // Play hands back rank -1 for a score it holds but has not placed,
                // and "-1." read as a position. A dash says the same thing honestly.
                unranked |= entry.rank <= 0;
                var place = entry.rank > 0 ? entry.rank + "." : "-";

                // Scores come off the board log-scaled (see Leaderboards.RenownScore),
                // so they are decoded back to Renown before anyone reads them.
                var line = place + "  " + name + "   <color=" + OchreHex + ">"
                           + NumberFormat.Short(Leaderboards.RenownFromScore(entry.score)) + "</color>";
                MakeText(sheet, entry.isPlayer ? "<b>" + line + "</b>" : line,
                    21, TextAnchor.MiddleCenter, entry.isPlayer ? Ink : Ink2, _serif);
            }

            if (unranked)
            {
                MakeText(sheet, "<i>Unranked</i>",
                    16, TextAnchor.MiddleCenter, Ink2, _serif);
            }
        }
    }
}
