using UnityEngine;
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
    /// last part is why none of these sheets carries an offering button or a
    /// claim: the keeping is answered on the Trail's card and the cache is
    /// looked for on the Camp's row, and a second set of controls over the same
    /// state is two places to fix a bug and two places for the wording to
    /// disagree. A popup that ONLY tells you things and then strands you would
    /// be the worse failure, so each ends in a door.
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
            }
        }

        /// <summary>
        /// The open tide, in full: the warden's sign, what the world is leaning
        /// toward while it holds, and the keeping's slots as they stand. The
        /// tier moment keeps its own sheet (<see cref="OpenKeepingSheet"/>) —
        /// that one is a celebration the game raises, this one is a reference
        /// the player asked for, and they should not read alike.
        /// </summary>
        private void OpenTideEventSheet()
        {
            var tide = _loop.OpenTide();
            if (tide == null)
            {
                // The tide closed between the cell being painted and the finger
                // landing on it. Nothing to say and nothing to do — say that.
                SetNote("the tide has closed; the fire keeps what it was given.");
                return;
            }

            var sheet = BeginSheet();
            MakeText(sheet, tide.displayName + "-tide", 32, TextAnchor.UpperCenter, Ink, _serif);

            var plate = ArtLibrary.ForJournal("sabbat-" + tide.id);
            if (plate != null)
            {
                PlateImage(sheet, plate, 220f);
            }

            MakeText(sheet, "<i>" + tide.sign + "</i>", 19, TextAnchor.MiddleCenter, Ink2, _hand);

            var closes = (_loop.OpenTideCloseMs() - _loop.NowUnixMs()) / 1000.0;
            MakeText(sheet, "the tide closes at the fire, " + NumberFormat.Countdown(closes) + " from now",
                17, TextAnchor.MiddleCenter, Ink2, _smallCaps);

            var gives = EffectsLabel(tide.touch);
            if (gives.Length > 0)
            {
                MakeHairline((RectTransform)sheet);
                MakeText(sheet, "<b>While it holds</b>  " + gives, 18, TextAnchor.UpperLeft, Ink);
                MakeText(sheet, "<i>the Rite's verses ask none of it — keeping a sabbat never raises the"
                                + " price of anything.</i>", 15, TextAnchor.UpperLeft, Ink2, _serif);
            }

            BuildKeepingProgress(sheet);

            var go = Button(sheet, "Open the keeping", 360, () =>
            {
                CloseSheet();
                _hud.GoToTrail("keeping");
            });
            KeyAction(go);
        }

        /// <summary>The keeping as it stands — every slot, what is held against it, and the tier the fire has counted.</summary>
        private void BuildKeepingProgress(Transform sheet)
        {
            var keeping = _loop.CurrentKeeping();
            if (keeping?.slots == null || keeping.slots.Count == 0)
            {
                return;
            }

            MakeHairline((RectTransform)sheet);
            var tier = _loop.KeepingTierReached();
            var word = tier == 1 ? "the eve is kept" : tier == 2 ? "the day is kept"
                : tier >= 3 ? "the wheel is kept" : "nothing set down yet";
            MakeText(sheet, "<b>The keeping</b>  <color=" + Ink2Hex + ">" + word + " · "
                            + Keeping.CompletedSlotCount(keeping) + " of " + keeping.slots.Count
                            + " answered</color>", 18, TextAnchor.UpperLeft, Ink);

            foreach (var slot in keeping.slots)
            {
                var specimen = slot.kind == KeepingSlotState.SpecimenKind;
                var name = specimen ? "a Decent find" : GoodName(slot.goodsId ?? string.Empty);
                if (Keeping.IsSlotComplete(slot))
                {
                    MakeText(sheet, "<color=" + MossDeepHex + ">" + name + ", set down</color>",
                        17, TextAnchor.UpperLeft, Ink);
                    continue;
                }

                var held = specimen ? DecentFindsHeld() : _loop.State.GetResource(slot.goodsId).ToDouble();
                MakeText(sheet, name + "  <color=" + Ink2Hex + ">"
                                + NumberFormat.ShortFloor(System.Math.Floor(System.Math.Min(held, slot.target)))
                                + " / " + NumberFormat.Short(slot.target) + "</color>",
                    17, TextAnchor.UpperLeft, Ink);
            }
        }

        private double DecentFindsHeld()
        {
            var total = 0.0;
            foreach (var pair in _loop.State.decentResources)
            {
                total += pair.Value.ToDouble();
            }

            return total;
        }

        /// <summary>
        /// The sabbat being waited for. No plate: the plate is what a keeping
        /// earns (design §15), and a tide that has not opened has earned
        /// nothing yet — the sheet is a date and a promise, not a page.
        /// <para>
        /// A name, a countdown to the opening and one line of the warden's
        /// voice — nothing else, from 2026-08-13. It used to carry the closing
        /// night as well, the Wheel's rules read out in full, the tide's whole
        /// touch under <em>When it opens</em>, the years it had been kept, and
        /// a note on where the reckoning is changed: six things asked of a
        /// player who tapped a cell to learn when the season turns. What the
        /// tide gives belongs to the tide's own sheet, which says it the day it
        /// starts being true; the night is a month past the only date this
        /// sheet is about; and the years kept are the book's to remember, not
        /// this sheet's.
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

            if (_loop.NextNightOf(coming, out _, out var opensMs))
            {
                MakeText(sheet, "the tide opens in "
                                + NumberFormat.Countdown((opensMs - _loop.NowUnixMs()) / 1000.0),
                    18, TextAnchor.MiddleCenter, Ink2, _serif);
            }

            MakeText(sheet, "<i>the calendar is the warden's, not the land's, and the warden reckons by the "
                            + (_loop.State.hemisphere == Wheel.HemisphereSouth ? "south" : "north")
                            + "'s wheel.</i>", 17, TextAnchor.MiddleCenter, Ink2, _serif);

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
                            + " journal's own week turns over at midnight on Monday — what Play has"
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
                // The same number the rail cell wears, said in full: due now,
                // and the week it is due in runs out at that hour.
                MakeText(sheet, "<color=" + MossDeepHex + ">the week has turned over — worth a look. this"
                                + " week's runs out in " + NumberFormat.Countdown(_loop.WeeklyCacheNextDueIn) + ".</color>",
                    18, TextAnchor.UpperLeft, Ink);
            }
            else
            {
                MakeText(sheet, "the next is due in " + NumberFormat.Countdown(_loop.WeeklyCacheNextDueIn) + ".",
                    18, TextAnchor.UpperLeft, Ink);
            }

            var go = Button(sheet, "Go to the fire", 360, () =>
            {
                CloseSheet();
                _hud.OpenTab(TabCamp);
            });
            KeyAction(go);
        }
    }
}
