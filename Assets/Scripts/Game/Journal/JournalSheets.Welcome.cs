using System.Collections.Generic;
using BreakInfinity;
using UnityEngine;
using UnityEngine.UI;
using Wildgrove.Game.Services;
using Wildgrove.Sim;
using static Wildgrove.Game.JournalTheme;
using static Wildgrove.Game.JournalFormat;
using static Wildgrove.Game.JournalWidgets;

namespace Wildgrove.Game
{
    /// <summary>
    /// The two sheets that bracket an absence: the welcome-back haul with its
    /// doubling offer and its buy-back of the hours the cap let go, and the
    /// fold, with the vignette that closes a run.
    /// <para>
    /// Both spend real money's worth, so both are written to be legible before
    /// they are generous: a doubled number with nothing to explain it reads as a
    /// bug, and a fold has to say what it takes before it says what it gives.
    /// </para>
    /// </summary>
    internal sealed partial class JournalSheets
    {
        // How many resource lines the welcome-back haul lists before it stops
        // and counts the rest.
        private const int WelcomeGainLines = 6;

        // How long the welcome-back sheet waits on a loaded rewarded ad before
        // going up without one. Long enough for an ordinary cold-launch fill —
        // the ads SDK's start and its first load are both network round trips —
        // and short enough that a launch which will never have one (no fill, no
        // signal, consent refused) isn't left staring at the grove.
        private const float WelcomeRewardedWaitSeconds = 3f;

        // When that wait runs out; -1 while nothing is waiting.
        private float _welcomeWaitDeadline = -1f;

        /// <summary>
        /// Whether to hold the welcome-back sheet back a moment longer so it can
        /// carry its "Double it" offer.
        /// <para>
        /// The offer is decided once, when the sheet is built, and on a cold
        /// launch the pump reaches it about a quarter-second in — while the ads
        /// SDK is still several async steps from its first loaded ad. The offer
        /// was therefore missing from most cold launches and present on every
        /// resume, which read as a bug and cost the player the reward. Asking
        /// again on the pump's cadence is also what re-requests a failed load,
        /// so the wait is the load's clock as well as its question.
        /// </para>
        /// </summary>
        private bool WaitingOnDoubleIt(OfflineSummary summary)
        {
            // Nothing owed, nothing to double, or too small an absence to raise
            // a sheet at all — no reason to wait on an ad none of them offer.
            if (summary == null
                || summary.gains.Count == 0
                || summary.creditedSeconds < GameLoop.WelcomeBackMinSeconds
                || _loop.RewardedReady(RewardedPlacement.OfflineBoost))
            {
                return false;
            }

            if (_welcomeWaitDeadline < 0f)
            {
                _welcomeWaitDeadline = Time.unscaledTime + WelcomeRewardedWaitSeconds;
            }

            return Time.unscaledTime < _welcomeWaitDeadline;
        }

        private void OpenWelcomeSheet(OfflineSummary summary)
        {
            var sheet = BeginSheet();
            // A bought camp name reads here first — the return is the moment
            // the camp is most itself (design §9's sink slate).
            MakeText(sheet, _loop.IsCampNamed() ? "Welcome back to " + _loop.CampName() : "Welcome back",
                32, TextAnchor.UpperCenter, Ink, _serif);

            // The absence itself is not news — the player knows how long they
            // were gone. "Away 7h 50m · credited 3h" led every return with a
            // subtraction they had to do themselves to reach the only part
            // that matters: that the camp stopped counting. So it says nothing
            // at all on a return the cap covered, and names the ceiling on one
            // it didn't.
            var uncovered = Amber.UncoveredHours(summary);
            if (uncovered > 0.0)
            {
                var cap = Mathf.RoundToInt((float)Upgrades.OfflineCapHours(_loop.State, _loop.Data));
                MakeText(sheet, "max " + cap + (cap == 1 ? " hour" : " hours"), 20, TextAnchor.UpperCenter, Ink2);
            }

            // Remove Ads owned: the haul is doubled before the sheet is drawn,
            // and nothing is asked. Offering "Double it" to someone who has
            // bought the ads away is a toll booth with the gate already up —
            // one tap, no ad, the same outcome whichever they choose, so the
            // only thing the question can do is make them answer it. Not
            // stopping to sell is what they paid for.
            var doubledUpFront = summary.gains.Count > 0
                                 && _loop.Store.RemoveAdsOwned
                                 && _loop.GrantOfflineBonus(summary);

            // Kept so a doubled haul can rewrite its own lines — the proof of
            // the reward belongs on the sheet the player is looking at. The
            // amount held here is always the UNdoubled one: only the ad path
            // rewrites these, and it is the path where nothing was doubled yet.
            var gainLines = new List<(Text line, string id, BigDouble amount)>();
            foreach (var pair in summary.gains)
            {
                if (gainLines.Count >= WelcomeGainLines)
                {
                    break;
                }

                var line = MakeText(sheet, "+" + NumberFormat.Short(doubledUpFront ? pair.Value * 2 : pair.Value)
                                           + " " + pair.Key, 18, TextAnchor.MiddleCenter, Ink);
                gainLines.Add((line, pair.Key, pair.Value));
            }

            // A silently truncated list reads as the whole haul — a camp that
            // works nine resources came back to six and looked robbed.
            if (summary.gains.Count > WelcomeGainLines)
            {
                MakeText(sheet, "<i>and " + (summary.gains.Count - WelcomeGainLines) + " more</i>",
                    16, TextAnchor.MiddleCenter, Ink2, _serif);
            }

            if (doubledUpFront)
            {
                // No button, but the doubling still has to be legible — the
                // numbers above are simply bigger than the night earned, and
                // unexplained generosity reads as a bug. Same line the watched
                // ad leaves behind, for the same reason.
                MakeText(sheet, "<i>The land gives twice</i>", 20, TextAnchor.MiddleCenter, MossDeep, _serif);
            }

            else if (summary.gains.Count > 0 && _loop.RewardedReady(RewardedPlacement.OfflineBoost))
            {
                // Opt-in rewarded ad: watch to double the haul just credited.
                // Not reached with Remove Ads owned — that haul is doubled
                // above without being asked; the branch still catches the case
                // where the up-front grant was refused because the run moved
                // under the sheet. The sheet STAYS OPEN whatever the ad does —
                // closing it threw the summary away as punishment for an
                // abandoned ad, and swallowed the proof of a watched one.
                Button doubleIt = null;
                var originalLabel = "Double it" + _loop.RewardedActionSuffix;
                doubleIt = Button(sheet, originalLabel, 360, () =>
                {
                    var doubled = false;
                    doubleIt.interactable = false;
                    SetButtonTint(doubleIt, false, true);
                    _loop.WatchRewarded(RewardedPlacement.OfflineBoost,
                        () =>
                        {
                            doubled = true;
                            if (!_loop.GrantOfflineBonus(summary))
                            {
                                // The run was replaced while the ad played (a
                                // cloud save adopted from another device), so
                                // this haul belongs to a book no longer in hand.
                                // The sheet describes that book — it goes, rather
                                // than rewrite its lines with numbers that never
                                // landed.
                                SetNote("the book moved on while that played. nothing doubled.");
                                CloseSheet();
                                return;
                            }

                            _loop.Telemetry.LogEvent("rewarded_ad", ("placement", "offline_boost"));

                            // The grant has landed and is safe; what follows only
                            // rewrites the sheet, and the sheet may not be there.
                            // A reward arrives after the ad, and the sheet can
                            // have closed under it in the meantime — writing to a
                            // destroyed widget throws inside the ads SDK's own
                            // callback, which is a bad place to raise anything.
                            if (doubleIt == null)
                            {
                                return;
                            }

                            foreach (var gain in gainLines)
                            {
                                if (gain.line != null)
                                {
                                    gain.line.text = "<color=" + MossDeepHex + ">+" + NumberFormat.Short(gain.amount * 2)
                                                     + " " + gain.id + " (doubled)</color>";
                                }
                            }

                            // The offer is spent — so it stops being a button.
                            // A dead plate reading "The land gives twice" still
                            // looks like something to tap; the line takes the
                            // button's place in the column as plain text.
                            var slot = doubleIt.transform.GetSiblingIndex();
                            var column = doubleIt.transform.parent;
                            Object.Destroy(doubleIt.gameObject);
                            var spent = MakeText(column, "<i>The land gives twice</i>", 20, TextAnchor.MiddleCenter, MossDeep, _serif);
                            spent.transform.SetSiblingIndex(slot);
                            _dirty = true;
                        },
                        () =>
                        {
                            // Ad closed without the reward — re-arm the offer.
                            // (On the rewarded path the button is gone, so the
                            // flag guards a destroyed reference as well — and the
                            // null check covers the sheet being closed under a
                            // still-open ad.)
                            if (!doubled && doubleIt != null)
                            {
                                doubleIt.interactable = true;
                                SetButtonTint(doubleIt, true, true);
                                SetButtonLabel(doubleIt, originalLabel);
                            }
                        });
                });
                KeyAction(doubleIt);
            }

            BuildLedgerOffer(sheet, summary);

            Button(sheet, "Continue", 320, CloseSheet);
        }

        /// <summary>
        /// Buy back the hours the away cap left uncredited (design §9's sink
        /// slate), at the full live rate, drawn from the paid-skip budget. The
        /// price sits on the button beside the hours it buys — this sheet is
        /// already the moment of decision, and a second confirm over a sheet
        /// would fight the scrim.
        /// </summary>
        private void BuildLedgerOffer(Transform sheet, OfflineSummary summary)
        {
            var hours = _loop.LedgerHoursOnOffer(summary);
            if (hours <= 0.0)
            {
                return;
            }

            var cost = Mathf.FloorToInt((float)_loop.LedgerCost(summary));

            // No sentence above the button. The sheet used to carry one ("the
            // night ran 4h 50m past what the camp could hold"), and every way
            // of keeping it went wrong once the away line went: it has to name
            // the hours lost to say anything at all, and naming them puts the
            // arithmetic straight back on a sheet that just shed it. Where the
            // skip budget makes the offer smaller than the loss, the offer is
            // simply smaller — the button says what it buys and what it costs,
            // and that is the entire decision in front of the player.
            var affordable = _loop.CanSettleLedger(summary);
            Button settle = null;
            settle = Button(sheet, "Catch up " + NumberFormat.Duration(hours * 3600.0) + " · " + cost + " amber", 420, () =>
            {
                var credited = _loop.SettleLedger(summary);
                if (credited <= 0.0)
                {
                    // The run moved under the sheet (an adopted save) or the
                    // amber is short after all — the offer quietly stands down.
                    SetNote("those hours keep. nothing was spent.");
                    return;
                }

                // The offer is spent — the line takes the button's place, the
                // doubled haul's own idiom.
                var slot = settle.transform.GetSiblingIndex();
                var column = settle.transform.parent;
                Object.Destroy(settle.gameObject);
                var spent = MakeText(column, "<i>caught up — " + NumberFormat.Duration(credited * 3600.0)
                                             + " credited at full pace</i>", 20, TextAnchor.MiddleCenter, MossDeep, _serif);
                spent.transform.SetSiblingIndex(slot);
                SetNote("amber spent: the hours the cap let go, given back.");
                _dirty = true;
            });
            settle.interactable = affordable;
            SetButtonTint(settle, affordable);
            if (!affordable)
            {
                MakeText(sheet, "<color=" + OchreInkHex + "><i>not enough amber — those hours keep.</i></color>",
                    16, TextAnchor.MiddleCenter, Ink2, _serif);
            }
        }

        internal void OpenMigrationSheet()
        {
            var gain = System.Math.Max(0.0, _loop.VerdureAfterMigration() - _loop.State.verdurePoints);
            // The scrim stays inert on the run's one destructive confirm — a
            // stray tap must not answer it either way; Back still cancels.
            var sheet = BeginSheet(scrimDismisses: false);
            // A named camp folds by name — it is this camp being struck, and
            // the sheet is where that should be felt. The name itself is not
            // among the losses: it pitches again in the next region.
            MakeText(sheet, _loop.IsCampNamed() ? "Fold " + _loop.CampName() : "Fold the camp",
                32, TextAnchor.UpperCenter, Ink, _serif);

            // Say plainly what a fold IS before saying what it costs. "They were
            // never yours" is the right voice but it isn't an explanation, and a
            // player who has never met a prestige reset can't infer one.
            MakeText(sheet, "<b>Start this camp over, on purpose.</b>\nYou begin again in the first meadow,"
                            + " and every run after this one runs richer.",
                20, TextAnchor.MiddleCenter, Ink, _serif);

            var bonus = _loop.Data.economy?.verdure?.yieldBonusPerPoint ?? 0.0;
            var after = Mathf.FloorToInt((float)_loop.VerdureAfterMigration());
            var carried = "<color=" + MossDeepHex + "><b>You carry:</b> +" + Mathf.FloorToInt((float)gain)
                          + " Verdure (" + after + " in all"
                          + (bonus > 0.0 ? ", +" + Mathf.RoundToInt((float)(after * bonus * 100.0)) + "% to everything you gather, for good" : string.Empty)
                          + "). The kith walk with you, and the journal keeps every plate, sketch and Almanac node.</color>";
            MakeText(sheet, carried, 19, TextAnchor.MiddleCenter, Ink);

            // Signatures about to sharpen (design §4): the creature's memory
            // arguing FOR the fold, named before the cost line. The NAMES alone
            // — a trait apiece ran the line to three on a phone, and which way
            // sharpens is the plate's business, read where the plate is.
            var sharpenings = _loop.FoldSharpenings();
            if (sharpenings.Count > 0)
            {
                var names = new System.Text.StringBuilder();
                foreach (var familiar in sharpenings)
                {
                    if (names.Length > 0)
                    {
                        names.Append(", ");
                    }

                    names.Append(familiar.name);
                }

                MakeText(sheet, "<color=" + MossDeepHex + "><i>" + names + (sharpenings.Count > 1 ? " deepen" : " deepens")
                                + " at this fold.</i></color>",
                    19, TextAnchor.MiddleCenter, Ink, _serif);
            }

            MakeText(sheet, "<b>You leave behind:</b> coin and stores, the camp buildings, tools and gear,"
                            + " the trails you opened, and every skill level. <i>They were never yours</i>.",
                19, TextAnchor.MiddleCenter, Ink2, _serif);

            // No tide line and no Renown explainer. The Wheel turns whether or
            // not the camp folds, so the season was never part of this choice,
            // and the events rail carries it on every tab anyway (design §15's
            // rule against telling one wait twice). Where Verdure comes from is
            // the fold banner's job, one tap behind this sheet: this is the
            // confirm, and a confirm carries what is gained, what is lost, and
            // the two ways out.
            Button(sheet, "Stay a while", 320, CloseSheet);
            var migrate = Button(sheet, "Fold and begin again", 320, () =>
            {
                CloseSheet();
                if (_loop.Migrate())
                {
                    // Same reason as starting the book again: which zones were
                    // folded shut is where the last run had got to, not a
                    // setting, and the new run's trail should open as the first
                    // one did.
                    _zoneOpen.Clear();
                    _dirty = true;
                    OpenVignette(gain);
                }
            });
            KeyAction(migrate);
        }

        /// <summary>The full-dark Migration vignette — the mock's fixed overlay; tap anywhere to walk on.</summary>
        private void OpenVignette(double verdureGained)
        {
            var dim = MakePanel("Sheet", (RectTransform)_modalLayer, NightInk);
            Stretch((RectTransform)dim.transform);
            _sheet = dim;
            _sheetDismiss = CloseSheet;
            // The tap that confirmed the fold can land again the next frame —
            // arm the tap-anywhere dismissal only after the vignette has had
            // a moment to be seen. Back (a deliberate act) stays immediate.
            var armAt = Time.unscaledTime + 1f;
            var tap = dim.AddComponent<Button>();
            tap.transition = Selectable.Transition.None;
            tap.onClick.AddListener(() =>
            {
                if (Time.unscaledTime >= armAt)
                {
                    CloseSheet();
                }
            });

            var layout = dim.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(40, 40, 60, 60);
            layout.spacing = 26;

            var lines = _loop.Data.dialogue?.migrationVignette;
            if (lines == null || lines.Count == 0)
            {
                lines = new List<string> { "The camp folds.", "The land exhales.", "What you gave, it keeps." };
            }

            foreach (var line in lines)
            {
                MakeText(dim.transform, "<i>" + line + "</i>", 30, TextAnchor.MiddleCenter, NightText, _serif);
            }

            // The vignette keeps its twelve words alone — the drawn season's
            // sign retired with the region draw (design §8, 2026-08-08); the
            // Wheel's margin lines live on the Trail page, in the warden's hand.
            MakeText(dim.transform, "+" + Mathf.FloorToInt((float)verdureGained) + " VERDURE", 20,
                TextAnchor.MiddleCenter, new Color(0.624f, 0.682f, 0.494f, 1f), _smallCaps);
            // The hint appears with the armed dismissal, not before it.
            var walkOn = MakeText(dim.transform, "TAP TO WALK ON", 15, TextAnchor.MiddleCenter,
                new Color(NightText.r, NightText.g, NightText.b, 0.45f), _smallCaps);
            walkOn.gameObject.SetActive(false);
            _hud.StartCoroutine(ShowAfter(walkOn.gameObject, 1f));
        }

        private static System.Collections.IEnumerator ShowAfter(GameObject go, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (go != null)
            {
                go.SetActive(true);
            }
        }
    }
}
