using System;
using System.Collections.Generic;
using BreakInfinity;
using UnityEngine;
using UnityEngine.UI;
using Wildgrove.Sim;
using static Wildgrove.Game.JournalTheme;
using static Wildgrove.Game.JournalWidgets;

namespace Wildgrove.Game
{
    // What the pinned chrome says, on the refresh cadence rather than per
    // frame: the header, the three-currency ledger, the Rite/Fold tracker, and
    // the handwritten margin note. Everything here reads state and writes
    // strings — it never builds or moves a widget, which is GameHud.Layout.cs.
    public sealed partial class GameHud
    {
        // ─────────────────────────── Chrome refresh ──────────────────────────

        private void RefreshChrome()
        {
            if (!_hintPostDone)
            {
                var state = _loop.State;
                _hintPostDone = Kith.Walking(state) > 0
                                || Warden.PostNodeId(state) != null
                                || Warden.IsSketching(state);
                if (_hintPostDone && _noteRevert <= 0f && _note != null)
                {
                    // The gesture just landed (or a posted save just loaded) —
                    // advance the teaching line rather than leaving stale advice.
                    ShowNote(HintText());
                }
            }

            RefreshHeader();
            RefreshLedger();
            RefreshTracker();
            UpdateWorldGap();
            // After the gap: the rail seats as many cells as the band's height
            // allows, so it must be asked once the band has this cadence's size.
            RefreshEventRail();
        }

        private void RefreshHeader()
        {
            string title;
            switch (_tab)
            {
                case TabCamp:
                    // The three station lines the page actually carries. It
                    // named the caravan until 2026-08-14, and the caravan went
                    // to the Stores page on 2026-08-13 with the Exchange — a
                    // page titled after a card that is no longer on it.
                    title = "Fire, Forge & Bench";
                    break;
                case TabWarden:
                    title = "Kit & Crafts";
                    break;
                case TabRecord:
                    title = "The Journal's Back Pages";
                    break;
                case TabStores:
                    // Without a case of its own the Stores page fell through to
                    // the Trail's title and headed itself with the name of the
                    // zone — a page about the shelves at home announcing the
                    // hedgerow it came from.
                    title = "The Stores";
                    break;
                default:
                    var zone = _labels.LatestZone();
                    title = zone != null ? zone.displayName : "The Trail";
                    break;
            }

            _title.text = title;
            _slotCounter.text = _loop.KithWalking() + " / " + _loop.KithSlots() + " POSTED";
        }

        /// <summary>
        /// What a page calls itself in the running head — the mock's
        /// <c>.running-head</c>, which every page carries and not only the Trail
        /// (docs/wildgrove-journal.html). A different register from the title
        /// above: the title says what is ON the page ("Fire, Forge &amp; Bench"),
        /// the running head says WHICH page of the book it is, which is the
        /// question a facing page raises and a single column doesn't.
        /// </summary>
        private static string RunningHeadName(string tab)
        {
            switch (tab)
            {
                case TabCamp: return "THE CAMP";
                case TabStores: return "THE STORES";
                case TabWarden: return "THE WARDEN";
                case TabRecord: return "THE RECORD";
                default: return "THE TRAIL";
            }
        }

        /// <summary>
        /// The two pages whose running head is a NAME rather than a label — the
        /// camp's and the warden's (design §9's sink slate, worn by the run and
        /// by the player). Both carried a whole card to say it until
        /// 2026-08-14: a bordered panel, its own head, and a row sized by the
        /// quill's touch plate, 154 units to hold two words on the shortest
        /// pages in the book. A page's own head is where a name belongs, and
        /// that line is one the spread was drawing anyway.
        /// <para>
        /// It is also why a single column has a running head at all now. There
        /// the title says which page this is, so a label under it would only
        /// restate it — but a NAME is not a restatement, and it is cheaper as a
        /// head than as a card wherever the book is being read.
        /// </para>
        /// </summary>
        private bool PageIsNamed(string tab)
        {
            return tab == TabCamp || tab == TabWarden;
        }

        private string PageName(string tab)
        {
            return tab == TabCamp ? _loop.CampName() : _loop.WardenName();
        }

        private void OpenPageNaming(string tab)
        {
            if (tab == TabCamp)
            {
                _sheets.OpenCampNamingSheet();
                return;
            }

            _sheets.OpenWardenNamingSheet();
        }

        private void RefreshLedger()
        {
            // Legacy Text wraps at any plain space — a non-breaking one inside
            // each label-value pair means the line only ever breaks BETWEEN
            // entries, never between a name and its number.
            const string pair = " ";
            var state = _loop.State;
            // The three meta currencies only — never the held stores. One entry
            // per resource grows a wrap every zone and quietly eats the page
            // this line sits above. Holdings read on the Record page, each
            // beside its own compendium entry, where "how much do I hold" is
            // asked deliberately rather than glanced at.
            var parts = new List<string>();

            // OchreInk, not Ochre — the theme's own rule: plain ochre fails
            // contrast at ledger size, and Renown is read hundreds of times.
            parts.Add("<color=" + OchreInkHex + ">RENOWN" + pair + "<b>" + NumberFormat.Short(state.renown) + "</b></color>");
            // Verdure appears once the fold economy is real — and styled as
            // RENOWN's peer, not lowercase flavour. Both meta numbers go
            // through NumberFormat so they never read "1234" beside "1.23K".
            if (state.verdurePoints > 0.0)
            {
                // UNSPENT, not the banked lifetime total. Learning a line
                // allocates rather than burns, so the total never moves — and a
                // counter that reads the same after a purchase looks broken and
                // overstates what the next line can draw on. The full total is
                // still what the +2%/pt passive counts, and it reads on the
                // Warden page as the fold forecast.
                parts.Add("<color=" + MossDeepHex + ">VERDURE" + pair + "<b>"
                          + NumberFormat.Short(new BigDouble(System.Math.Floor(_loop.AvailableVerdure()))) + "</b></color>");
            }

            if (state.amber > 0.0)
            {
                // The paid currency wears its own resin ink and full caps —
                // lowercase-ochre made it a visual twin of RENOWN, and that's
                // a real-money misread waiting to happen.
                parts.Add("<color=" + AmberInkHex + ">AMBER" + pair + "<b>"
                          + NumberFormat.Short(new BigDouble(System.Math.Floor(state.amber))) + "</b></color>");
            }

            // The tracker's guillemet marks a banner as a link; the ledger is
            // one too now (it opens the Record page's stores), so it wears the
            // same mark rather than being a tap nobody would guess at.
            _ledger.text = string.Join(" · ", parts)
                           + "  " + SizeOpen(15) + "<color=" + Ink2Hex + ">»</color></size>";
        }

        // Where the tracker row's tap lands — Layout wires the button once,
        // and RefreshTracker points it at whatever the row is showing.
        private string _trackerTarget = "verse";

        private void RefreshTracker()
        {
            _trackerTarget = "verse";
            if (_loop.CanMigrate())
            {
                var gain = System.Math.Max(0.0, _loop.VerdureAfterMigration() - _loop.State.verdurePoints);
                // The banner carries the curve now — a percentage is the one
                // form of "how close am I" a player can read without knowing
                // what a Renown threshold is.
                _trackerText.text = "<color=" + OchreInkHex + ">THE FOLD</color> · +<b>"
                                    + Mathf.FloorToInt((float)gain) + "</b> Verdure banked · "
                                    + Mathf.FloorToInt((float)_loop.ProgressToNextVerdure() * 100f) + "% 'til the next point";
                _foldButton.gameObject.SetActive(true);
                _trackerPanel.SetActive(true);
                return;
            }

            _foldButton.gameObject.SetActive(false);
            // Verses in play only — a verse whose trail this fold cannot reach
            // is not something the run can sing, so counting it would tell the
            // warden to walk somewhere that does not exist yet.
            var versesInPlay = Rite.VersesInPlay(_loop.State, _loop.Data);
            if (versesInPlay.Count > 0)
            {
                foreach (var verse in versesInPlay)
                {
                    if (Rite.IsVerseComplete(_loop.State, _loop.Data, verse))
                    {
                        continue;
                    }

                    // An unsung verse whose site the trail hasn't reached is
                    // still the rite's next step — and still what the fold is
                    // waiting on. Skipping it blanked the banner outright, so a
                    // run that had sung every reachable verse lost the only
                    // signpost to Migration the HUD has.
                    if (!Rite.IsVerseRevealed(_loop.State, _loop.Data, verse))
                    {
                        // How many verses still stand between the run and the
                        // fold — the one number that says why the fold button
                        // isn't there. Which zone they wait in is the Trail
                        // page's job; the banner is a count, not a route.
                        var unsung = versesInPlay.Count - Rite.CompletedVerseCount(_loop.State, _loop.Data);
                        _trackerText.text = "<color=" + OchreInkHex + ">THE FOLD</color> · <b>" + unsung
                                            + (unsung == 1 ? " verse</b> must be sung" : " verses</b> must be sung")
                                            + " before migration  »";
                        _trackerPanel.SetActive(true);
                        return;
                    }

                    var done = Rite.CompletedSlotCount(_loop.State, verse);
                    var need = Rite.RequiredSlots(_loop.State, _loop.Data, verse);
                    // The trailing guillemet marks the banner as a link — it
                    // jumps to the verse card, far down the Trail page.
                    // The verse alone. A grey "· Ostara-tide" used to ride the
                    // end of this line, two rows above a rail cell wearing the
                    // same sabbat's plate and its countdown — the demotion the
                    // rail was built to end, restated by the row that demoted
                    // it. Dropped 2026-08-13; the banner says one thing.
                    _trackerText.text = "Verse of " + _labels.ZoneName(verse.zone) + ": <b>"
                                        + Mathf.Min(done, need) + " of " + need + "</b> answered  »";
                    _trackerPanel.SetActive(true);
                    return;
                }
            }

            // With no verse pinned, an open tide takes the row alone — the
            // keeping is the one clock left running (design §15), and the
            // guillemet links to its card at the head of the Trail.
            var tide = _loop.OpenTide();
            if (tide != null)
            {
                _trackerTarget = "keeping";
                _trackerText.text = "<b>" + tide.displayName + "-tide</b> · " + KeepingWord()
                                    + " · " + TideCloseWord() + "  »";
                _trackerPanel.SetActive(true);
                return;
            }

            // Nothing pinned — hide the panel outright; an empty bordered
            // strip reads as a rendering bug.
            _trackerText.text = string.Empty;
            _trackerPanel.SetActive(false);
        }

        private string KeepingWord()
        {
            switch (_loop.KeepingTierReached())
            {
                case 1: return "kept the eve";
                case 2: return "kept the day";
                case 3: return "kept the wheel";
                default: return "unkept";
            }
        }

        private string TideCloseWord()
        {
            var closeMs = _loop.OpenTideCloseMs();
            var days = (long)System.Math.Ceiling((closeMs - _loop.NowUnixMs()) / 86400000.0);
            return days <= 1 ? "closes at the fire tonight" : "closes in " + days + " days";
        }

        internal void SetNote(string text)
        {
            if (_note != null)
            {
                ShowNote(text);
                _noteRevert = NoteRevertSeconds;
            }
        }

        /// <summary>
        /// Write the margin note. The lane it stands in is left to
        /// <see cref="StandNoteLane"/>, which is the same question asked when
        /// the page folds.
        /// </summary>
        private void ShowNote(string text)
        {
            if (_note == null || _noteLane == null)
            {
                return;
            }

            _note.text = text ?? string.Empty;
            StandNoteLane();
        }

        /// <summary>
        /// Stand the note's lane, or take it away.
        /// <para>
        /// In the column the lane ALWAYS stands, empty or not — the line of
        /// paper is held open from the moment the book does. The note is pinned
        /// above the page, so a lane that arrived with the first sentence shoved
        /// the whole journal down a line under a thumb already reading it, and
        /// the first sentence is usually the outcome of the tap that thumb has
        /// just made. One blank line for the life of the run is the cheaper half
        /// of that trade; <see cref="UpdateWorldGap"/> measures the rows that are
        /// standing, so the strip above simply keeps a line less and keeps it
        /// from the outset.
        /// </para>
        /// <para>
        /// On a spread the note stands BESIDE the tracker (<see cref="ApplyNoteFold"/>)
        /// and so costs the page no height at all — there a silent lane is worth
        /// taking away, because what it hands back is the tracker's other half
        /// of the row rather than a line of the page.
        /// </para>
        /// <para>
        /// The lane is what stands or goes, never the label inside it: the lane
        /// carries the mask and the one-line height (<see cref="MarqueeLine"/>),
        /// so hiding the label alone would leave an empty line of paper behind.
        /// </para>
        /// </summary>
        private void StandNoteLane()
        {
            if (_noteLane == null)
            {
                return;
            }

            var stands = !_wide || (_note != null && _note.text.Length > 0);
            if (_noteLane.gameObject.activeSelf != stands)
            {
                _noteLane.gameObject.SetActive(stands);
            }
        }

        /// <summary>
        /// Android's hardware/gesture Back (Escape on desktop): dismiss the open
        /// sheet the safe way, step back to the Trail tab, then follow platform
        /// convention and exit (the run saves on pause/quit).
        /// </summary>
    }
}
