using UnityEngine;
using Wildgrove.Game.Services;
using Wildgrove.Sim;
using static Wildgrove.Game.JournalTheme;
using static Wildgrove.Game.JournalFormat;
using static Wildgrove.Game.JournalWidgets;

namespace Wildgrove.Game
{
    /// <summary>
    /// The events rail's popups — one per cell (see <see cref="EventRail"/>).
    /// <para>
    /// Each says the same three things in the same order: what this is, how it
    /// stands right now, and the way through to the page that answers it. That
    /// last part is why the Wheel's sheets and the cache's carry no offering
    /// button and no claim: the keeping is answered on the Trail's card and the
    /// cache is looked for on the Camp's row, and a second set of controls over
    /// the same state is two places to fix a bug and two places for the wording
    /// to disagree. A popup that ONLY tells you things and then strands you would
    /// be the worse failure, so each ends in a door.
    /// </para>
    /// <para>
    /// The middle of those three is what a sheet MAY say, not what it owes:
    /// from 2026-09-10 the tide's says nothing about how the keeping stands,
    /// because the card at the other end of its own button is where that is
    /// drawn, and standing it here as well was the same page twice, a tap apart.
    /// </para>
    /// <para>
    /// The time-skip's sheet is the exception, and it is the same rule read the
    /// other way: from 2026-08-13 there IS no page that answers it, so this sheet
    /// is the one set of controls rather than a second. It has to be a sheet
    /// rather than the cell's own tap because the tap costs an ad, and an ad that
    /// starts from a cell nobody read is a surprise the player did not agree to.
    /// </para>
    /// </summary>
    internal sealed partial class JournalSheets
    {
        internal void OpenEventSheet(string id)
        {
            switch (id)
            {
                case EventRail.OpenTideId:
                    OpenTideEventSheet();
                    break;
                case EventRail.ComingSabbatId:
                    OpenComingSabbatSheet();
                    break;
                case EventRail.WeeklyCacheId:
                    OpenWeeklyCacheSheet();
                    break;
                case EventRail.TimeSkipId:
                    OpenTimeSkipSheet();
                    break;
            }
        }

        /// <summary>
        /// The open season in the warden's own words, and nothing else: what
        /// the day it is named for meant in the old year, the sign the land is
        /// giving for it, and the way through to the card that answers it. The
        /// tier moment keeps its own sheet (<see cref="OpenKeepingSheet"/>):
        /// that one is a celebration the game raises, this one is the page the
        /// player asked for, and they should not read alike.
        /// <para>
        /// The sabbat's <c>lore</c> joined it on 2026-09-09, when the seasons
        /// were laid end to end (design §15). It used to belong to the sheet
        /// that waited a month for a tide to open, and there is no waiting any
        /// more: a season names itself for a day, and this is now the only place
        /// that says what the day was.
        /// </para>
        /// <para>
        /// Three things came off it on 2026-09-10 (Mo's call), leaving lore and
        /// a door. The countdown, which is the rail cell that opened this sheet
        /// read aloud, and the keeping card's head says it again at the other
        /// end of the button. The keeping's slot list, which is that same card
        /// drawn a second time, one tap before it. And the touch's <i>While it
        /// holds ×1.2</i> line, whose work the sabbat's <c>sign</c> already does
        /// directly above it in the warden's hand. §15 asks for the touch to be
        /// seen as a sign, and a table of multipliers under the sign is that
        /// one lean told twice, once in the voice and once in the numbers.
        /// </para>
        /// </summary>
        private void OpenTideEventSheet()
        {
            var tide = _loop.OpenTide();
            if (tide == null)
            {
                // The season turned between the cell being painted and the
                // finger landing on it. Nothing to say and nothing to do — say
                // that.
                SetNote("the season turned. the fire keeps what it got.");
                return;
            }

            var sheet = BeginSheet();
            MakeText(sheet, tide.displayName + "-tide", 32, TextAnchor.UpperCenter, Ink, _serif);

            var plate = ArtLibrary.ForJournal("sabbat-" + tide.id);
            if (plate != null)
            {
                PlateImage(sheet, plate, 220f);
            }

            MakeText(sheet, "<i>" + tide.lore + "</i>", 17, TextAnchor.MiddleCenter, Ink2, _serif);
            MakeText(sheet, "<i>" + tide.sign + "</i>", 19, TextAnchor.MiddleCenter, Ink2, _hand);

            var go = Button(sheet, "Open the keeping", 360, () =>
            {
                CloseSheet();
                _hud.GoToTrail("keeping");
            });
            KeyAction(go);
        }

        /// <summary>
        /// The sabbat being waited for. No plate: the plate is what a keeping
        /// earns (design §15), and a season that has not begun has earned
        /// nothing yet — the sheet is a date and a promise, not a page.
        /// <para>
        /// A name, a countdown to the night it takes the wheel, and the
        /// sabbat's own lore — nothing else, from 2026-08-13. The third line was
        /// the reckoning ("the calendar is the warden's… by the south's wheel")
        /// for the first half of that day: it answered a question nobody had
        /// asked, and said it a third time, the reckoning already naming the
        /// Record's Wheel card and labelling the inside-cover button that
        /// changes it.
        /// </para>
        /// <para>
        /// Since the seasons were laid end to end (2026-09-09) this is the rare
        /// half of the Wheel's rail cell rather than the common one: a season is
        /// open every day the calendar covers, so the only way here is a cursor
        /// sitting before the first authored night. It stays because that IS a
        /// state the game can be in and a blank rail would say nothing about it.
        /// </para>
        /// </summary>
        private void OpenComingSabbatSheet()
        {
            var coming = _loop.NextSabbat(out _);
            if (coming == null)
            {
                return;
            }

            var sheet = BeginSheet();
            MakeText(sheet, coming.displayName + " is coming", 32, TextAnchor.UpperCenter, Ink, _serif);

            if (_loop.NextNightOf(coming, out var nightMs))
            {
                MakeText(sheet, "it takes the wheel in "
                                + NumberFormat.Countdown((nightMs - _loop.NowUnixMs()) / 1000.0),
                    18, TextAnchor.MiddleCenter, Ink2, _serif);
            }

            MakeText(sheet, "<i>" + coming.lore + "</i>", 17, TextAnchor.MiddleCenter, Ink2, _serif);

            var seen = Button(sheet, "Walk on", 320, CloseSheet);
            KeyAction(seen);
        }

        /// <summary>
        /// The weekly cache. Says plainly what the Camp row's countdown means:
        /// the week turning over is OUR clock, and what Play has actually set
        /// out is only known by asking it.
        /// </summary>
        private void OpenWeeklyCacheSheet()
        {
            var amber = _loop.Data.economy?.amber;
            var sheet = BeginSheet();
            MakeText(sheet, "The weekly cache", 32, TextAnchor.UpperCenter, Ink, _serif);

            if (amber != null && amber.weeklyCacheAmber > 0.0)
            {
                MakeText(sheet, "<color=" + AmberInkHex + ">+" + Mathf.FloorToInt((float)amber.weeklyCacheAmber)
                                + " amber</color>, once a week", 22, TextAnchor.MiddleCenter, Ink, _serif);
            }

            MakeText(sheet, "<i>Play Games sets a cache out for a challenge met, about once a week. The"
                            + " journal's own week turns over at midnight on Monday. What Play has"
                            + " actually left is only known by looking.</i>",
                17, TextAnchor.UpperLeft, Ink2, _serif);

            MakeHairline((RectTransform)sheet);
            if (!_loop.GameServices.IsSignedIn)
            {
                MakeText(sheet, "Play Games isn't signed in, so there is nobody for a cache to be left for.",
                    18, TextAnchor.UpperLeft, Ink);
            }
            else if (_loop.WeeklyCacheDue)
            {
                // The same number the rail cell wears, said in full. What it
                // counts is the warden's week running out, not a cache waiting,
                // so the line says what it knows and leaves the rest to a look.
                MakeText(sheet, "<color=" + MossDeepHex + ">none taken this week, and the week runs out in "
                                + NumberFormat.Countdown(_loop.WeeklyCacheNextDueIn) + ".</color>",
                    18, TextAnchor.UpperLeft, Ink);
                MakeText(sheet, LastCacheLine(), 17, TextAnchor.UpperLeft, Ink2, _serif);
            }
            else
            {
                MakeText(sheet, "a cache came this week. the next is due in "
                                + NumberFormat.Countdown(_loop.WeeklyCacheNextDueIn) + ".",
                    18, TextAnchor.UpperLeft, Ink);
            }

            var go = Button(sheet, "Go", 360, () =>
            {
                CloseSheet();
                _hud.GoToCard(TabCamp, "amber");
            });
            KeyAction(go);
        }

        /// <summary>
        /// Whether a cache has ever come, and how long ago. The claim stamp is
        /// the only lasting record of an arrival, so for a player who missed the
        /// confirmation this is the only place it is still written down.
        /// </summary>
        private string LastCacheLine()
        {
            var since = _loop.WeeklyCacheSinceLast;
            return since < 0.0
                ? "no cache has come yet."
                : "the last came " + NumberFormat.Countdown(since) + " ago.";
        }

        /// <summary>
        /// The rewarded time-skip (design §10): hours of gathering for a short
        /// ad, once a cooldown. This sheet is where it is taken, and the only
        /// place — it was a plate at the head of the Camp page until 2026-08-13
        /// (see <see cref="EventRail"/> for why it moved to the rail).
        /// <para>
        /// The one rail sheet that acts rather than pointing at a page, and it
        /// has to be a sheet: the tap spends an ad, and the cell alone cannot say
        /// what the hours are worth or what they cost before it starts one.
        /// </para>
        /// <para>
        /// It names the amber row as well. The two are the same verb at two
        /// prices — this credits at the away rate for an ad, the Camp's row at
        /// full pace for amber — and until they were a tap apart neither surface
        /// mentioned the other's existence.
        /// </para>
        /// </summary>
        private void OpenTimeSkipSheet()
        {
            var hours = Amber.RewardedTimeSkipHours;
            var sheet = BeginSheet();
            MakeText(sheet, "Pass the time", 32, TextAnchor.UpperCenter, Ink, _serif);

            var plate = ArtLibrary.ForJournal("glass");
            if (plate != null)
            {
                PlateImage(sheet, plate, 200f);
            }

            MakeText(sheet, "<color=" + MossDeepHex + ">+" + NumberFormat.Duration(hours * 3600.0)
                            + "</color> of gathering, at the pace the land keeps while you are away",
                22, TextAnchor.MiddleCenter, Ink, _serif);
            MakeText(sheet, "<i>the kith work the hours through in a breath. every batch, every post, every"
                            + " site: the same hours you would have had by putting the book down.</i>",
                17, TextAnchor.UpperLeft, Ink2, _serif);

            MakeHairline((RectTransform)sheet);
            if (!_loop.CanTimeSkipReward)
            {
                MakeText(sheet, "the land has given its hours for now. the glass turns again in "
                                + NumberFormat.Countdown(_loop.TimeSkipRewardCooldownRemaining) + ".",
                    18, TextAnchor.UpperLeft, Ink);
                KeyAction(Button(sheet, "Walk on", 320, CloseSheet));
                return;
            }

            MakeText(sheet, "<i>the amber row at the camp hastens the same hours at FULL pace, for amber"
                            + " rather than an ad.</i>", 15, TextAnchor.UpperLeft, Ink2, _serif);

            var take = Button(sheet, "Pass the time" + _loop.RewardedActionSuffix, 420, () =>
            {
                CloseSheet();
                TakeTimeSkip(hours);
            });
            KeyAction(take);
        }

        /// <summary>
        /// Watch for the hours and credit them. The cooldown is re-checked at the
        /// grant as well as at the tap: the sheet can sit open, and
        /// <see cref="GameLoop.CreditTimeSkip"/> is the one authority on whether
        /// the land owes anything.
        /// </summary>
        private void TakeTimeSkip(double hours)
        {
            _loop.WatchRewarded(RewardedPlacement.TimeSkip,
                () =>
                {
                    if (!_loop.CreditTimeSkip(hours))
                    {
                        return;
                    }

                    _loop.Telemetry.LogEvent("rewarded_ad", ("placement", "time_skip"));
                    SetNote(NumberFormat.Duration(hours * 3600.0)
                            + " pass in a breath. the kith kept to it.");
                    _dirty = true;
                });
        }
    }
}
