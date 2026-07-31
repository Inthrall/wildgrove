using System.Collections.Generic;
using BreakInfinity;
using UnityEngine;
using UnityEngine.UI;
using Wildgrove.Data;
using Wildgrove.Game.Services;
using Wildgrove.Sim;
using static Wildgrove.Game.JournalTheme;
using static Wildgrove.Game.JournalFormat;
using static Wildgrove.Game.JournalWidgets;

namespace Wildgrove.Game
{
    /// <summary>
    /// The journal's modal sheets and the camp-actions strip. Owns the
    /// one-open-sheet lifecycle (<see cref="BeginSheet"/>/<see cref="CloseSheet"/>),
    /// the pending-sheet pump, the posting sheet, and the rewarded/purchase
    /// buttons at the head of the Camp page.
    /// </summary>
    internal sealed class JournalSheets : JournalSection
    {
        // The time-skip ad credits this many hours of gathering.
        private const double TimeSkipHours = 2.0;

        // How much of the screen a sheet's lines may fill before they scroll
        // instead of growing (the card's padding rides on top).
        private const float SheetMaxCanvasShare = 0.72f;
        private Button _timeSkipButton;
        private Button _removeAdsButton;
        // The open sheet's safe way out — what Android Back and a tap on the
        // scrim mean. Null when no sheet is open.
        private System.Action _sheetDismiss;
        private bool _removeAdsPending;

        internal JournalSheets(GameHud hud) : base(hud) { }

        /// <summary>
        /// Dismiss the open sheet the safe way — hardware Back and the scrim
        /// route here. Each sheet chooses what "dismiss" means (cancel, keep
        /// the suggested name, walk on); plain close is the default.
        /// </summary>
        internal void DismissSheet()
        {
            if (_sheet == null)
            {
                return;
            }

            var dismiss = _sheetDismiss;
            _sheetDismiss = null;
            if (dismiss != null)
            {
                dismiss();
            }
            else
            {
                CloseSheet();
            }
        }

        /// <summary>
        /// Forwards a tap on a sheet's dim scrim — but only outside the paper
        /// panel — to the sheet's dismissal. uGUI clicks bubble up from the
        /// panel's own widgets to the scrim, so the handler must check where
        /// the tap actually landed rather than trusting that it reached here.
        /// </summary>
        private sealed class ScrimTap : MonoBehaviour, UnityEngine.EventSystems.IPointerClickHandler
        {
            internal System.Action OnTap;
            internal RectTransform Panel;

            public void OnPointerClick(UnityEngine.EventSystems.PointerEventData eventData)
            {
                if (Panel == null
                    || !RectTransformUtility.RectangleContainsScreenPoint(Panel, eventData.position, eventData.enterEventCamera))
                {
                    OnTap?.Invoke();
                }
            }
        }

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

            // Welcome-back FIRST — it's the context for everything after it;
            // being asked to name a newcomer before being told what happened
            // while away read backwards.
            var summary = _loop.TakePendingOfflineSummary();
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

            var waystoneZone = Narrative.NextUnreadWaystone(_loop.State, _loop.Data);
            if (waystoneZone != null)
            {
                OpenWaystoneSheet(waystoneZone);
                AddTapGuard(PumpedSheetGuardSeconds);
            }
        }

        /// <summary>
        /// A transparent click-eater over the whole open sheet for its first
        /// moments. It carries its own (listener-less) Button so uGUI's click
        /// bubbling stops at it rather than walking up to the scrim's dismiss.
        /// </summary>
        private void AddTapGuard(float seconds)
        {
            if (_sheet == null)
            {
                return;
            }

            var guard = new GameObject("TapGuard", typeof(Image), typeof(Button));
            guard.transform.SetParent(_sheet.transform, false);
            Stretch((RectTransform)guard.transform);
            guard.GetComponent<Image>().color = Color.clear;
            var guardButton = guard.GetComponent<Button>();
            guardButton.transition = Selectable.Transition.None;
            // It only exists to swallow taps — focus landing on it would be a
            // dead end for the half-second it lives.
            NoNavigation(guardButton);
            _hud.StartCoroutine(DestroyAfter(guard, seconds));
        }

        private static System.Collections.IEnumerator DestroyAfter(GameObject go, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (go != null)
            {
                Object.Destroy(go);
            }
        }

        private void OpenWaystoneSheet(ZoneData zone)
        {
            var text = Narrative.WaystoneText(_loop.Data, zone.id);
            // Dismissing IS walking on — the stone must mark itself read, or
            // the pump re-raises it a quarter-second later.
            var sheet = BeginSheet(() =>
            {
                _loop.MarkWaystoneRead(zone.id);
                CloseSheet();
            });
            MakeText(sheet, "A waystone", 32, TextAnchor.UpperCenter, Ink, _serif);
            MakeText(sheet, zone.displayName.ToUpperInvariant(), 18, TextAnchor.UpperCenter, Ink2, _smallCaps);
            if (!string.IsNullOrEmpty(text))
            {
                MakeText(sheet, "<i>“" + text + "”</i>", 24, TextAnchor.MiddleCenter, Ink, _serif);
            }

            Button(sheet, "Walk on", 320, () =>
            {
                _loop.MarkWaystoneRead(zone.id);
                CloseSheet();
                // Several stones unread (a cloud-save adoption) page straight
                // through in one sitting instead of materialising one by one
                // on the pump's cadence; the guard still paces each page.
                var next = Narrative.NextUnreadWaystone(_loop.State, _loop.Data);
                if (next != null)
                {
                    OpenWaystoneSheet(next);
                    AddTapGuard(PumpedSheetGuardSeconds);
                }
            });
        }

        private void OpenArrivalSheet(Familiar familiar)
        {
            // Backing out keeps the (free) suggested name — the arrival must
            // resolve either way, or the pump re-raises the sheet at once.
            var sheet = BeginSheet(() =>
            {
                _loop.TakePendingArrival();
                _dirty = true;
                CloseSheet();
            });
            Celebrate(sheet, ArrivalSeeds);
            MakeText(sheet, "A new friend", 32, TextAnchor.UpperCenter, Ink, _serif);
            MakeText(sheet, "a " + SpeciesName(familiar.speciesId) + " arrives", 22, TextAnchor.UpperCenter, Ink2, _hand);

            // Who it is, in the book's own hand — a name is being asked for, and
            // naming something you can't see is a form filled in, not a meeting.
            var portrait = ArtLibrary.ForSpecies(familiar.speciesId);
            if (portrait != null)
            {
                PlateImage(sheet, portrait, 200f);
            }

            var cost = Mathf.FloorToInt((float)_loop.RenameCost());
            if (cost > 0)
            {
                MakeText(sheet, "keep this name, or choose one for " + SizeOpen(19) + "<color=" + OchreHex + ">"
                                + cost + " amber</color></size>", 16, TextAnchor.UpperCenter, Ink2, _hand);
            }

            var field = MakeInputField(sheet, familiar.name);
            Button(sheet, "Walk together", 320, () =>
            {
                var typed = field.text;
                // Keeping the suggested name is free; choosing a different one
                // asks the rename price (design §4). Short of amber, the
                // suggestion holds — the arrival never stalls on the coffer.
                if (!string.IsNullOrWhiteSpace(typed) && typed.Trim() != familiar.name
                    && !_loop.RenameFamiliar(familiar, typed))
                {
                    SetNote("not enough amber for a chosen name, so the suggestion holds.");
                }

                _loop.TakePendingArrival();
                _dirty = true;
                CloseSheet();
            });
        }

        /// <summary>
        /// The confirmation Google requires for anything granted outside the
        /// game (design §11). The rules are specific and they outrank the
        /// journal's usual reticence: the item must be named plainly, the source
        /// said out loud, there must be no way to decline, and it must stay up
        /// until the player acknowledges it. So the plain line comes first and
        /// the grove's own voice second, there is one button, the scrim is inert,
        /// and backing out with Esc takes the same door as Continue — the reward
        /// is already granted and saved by the time this opens; this is the
        /// telling, not the taking.
        /// </summary>
        private void OpenRewardSheet(RewardGrant grant)
        {
            var sheet = BeginSheet(CloseSheet, scrimDismisses: false);
            MakeText(sheet, "A gift from Play Games", 32, TextAnchor.UpperCenter, Ink, _serif);
            MakeText(sheet, grant.statement, 21, TextAnchor.MiddleCenter, Ink, _serif);
            if (!string.IsNullOrEmpty(grant.flavour))
            {
                MakeText(sheet, "<i>" + grant.flavour + "</i>", 20, TextAnchor.MiddleCenter, Ink2, _hand);
            }

            var accept = Button(sheet, "Continue", 320, CloseSheet);
            KeyAction(accept);
        }

        private void OpenBondSheet(BondData bond)
        {
            // First bond earned unlocks "First kith" (idempotent — later bonds no-op).
            _loop.GameServices.UnlockAchievement(AchievementIds.FirstKith);

            var sheet = BeginSheet();
            // The heaviest drift in the game. A bond is the one thing the fold
            // can't take back, and it was reading like a receipt for it.
            Celebrate(sheet, BondSeeds);
            MakeText(sheet, "A bond is made", 32, TextAnchor.UpperCenter, Ink, _serif);

            var portrait = ArtLibrary.ForSpecies(bond.species);
            if (portrait != null)
            {
                PlateImage(sheet, portrait, 220f);
            }

            MakeText(sheet, bond.displayName, 26, TextAnchor.UpperCenter, Ink, _serif);
            MakeText(sheet, "will cross every fold with you.", 22, TextAnchor.UpperCenter, Ink2, _serif);
            MakeText(sheet, "<i>the grove keeps few things through a migration. this is one.</i>",
                18, TextAnchor.MiddleCenter, Ink2, _hand);
            Button(sheet, "Walk together", 320, CloseSheet);
        }

        /// <summary>
        /// The warden's own ladder widening (design §4) — a verse sung past a
        /// milestone, or a slot bought. Nothing announced it before: the count
        /// on the Warden page simply read one higher the next time anyone
        /// looked, which is no way to mark the thing the whole kith is gated on.
        /// </summary>
        private void OpenKithSlotSheet(int slots)
        {
            var sheet = BeginSheet();
            Celebrate(sheet, SlotSeeds);
            MakeText(sheet, "The circle widens", 32, TextAnchor.UpperCenter, Ink, _serif);

            var hearth = ArtLibrary.ForBuilding("fire");
            if (hearth != null)
            {
                PlateImage(sheet, hearth, 180f);
            }

            MakeText(sheet, "another may hold a post", 22, TextAnchor.UpperCenter, Ink2, _hand);
            MakeText(sheet, slots + " of " + Kith.SlotsMax(_loop.Data) + " places at the fire",
                20, TextAnchor.UpperCenter, Ink, _smallCaps);

            // Where the reward actually lands — a slot is worth nothing until
            // someone resting is walked out to a node.
            var resting = _loop.KithCount() - _loop.KithWalking();
            MakeText(sheet, resting > 0
                    ? "<i>" + (resting == 1 ? "one of the kith rests" : resting + " of the kith rest")
                      + " at camp, and the Warden page will station them.</i>"
                    : "<i>the next to arrive can walk straight out.</i>",
                18, TextAnchor.MiddleCenter, Ink2, _hand);
            Button(sheet, "Good", 320, CloseSheet);
        }

        // How heavy the drift is, by how much the moment is worth: a bond is
        // permanent, a slot is the ladder, an arrival happens most runs.
        private const int BondSeeds = 16;
        private const int SlotSeeds = 12;
        private const int ArrivalSeeds = 8;

        /// <summary>Sow a drift of seed up a sheet — the journal's one celebration, in the ink it reads in.</summary>
        private static void Celebrate(Transform sheet, int seeds)
        {
            Seedfall.Sow(sheet, seeds, new Color(Ink2.r, Ink2.g, Ink2.b, 0.5f));
        }

        private void OpenWelcomeSheet(OfflineSummary summary)
        {
            var sheet = BeginSheet();
            MakeText(sheet, "Welcome back", 32, TextAnchor.UpperCenter, Ink, _serif);
            MakeText(sheet, "Away " + NumberFormat.Duration(summary.realSeconds)
                            + " · credited " + NumberFormat.Duration(summary.creditedSeconds), 20, TextAnchor.UpperCenter, Ink2);

            // Kept so a doubled haul can rewrite its own lines — the proof of
            // the reward belongs on the sheet the player is looking at.
            var gainLines = new List<(Text line, string id, BigDouble amount)>();
            foreach (var pair in summary.gains)
            {
                if (gainLines.Count >= 6)
                {
                    break;
                }

                var line = MakeText(sheet, "+" + NumberFormat.Short(pair.Value) + " " + pair.Key, 18, TextAnchor.MiddleCenter, Ink);
                gainLines.Add((line, pair.Key, pair.Value));
            }

            // Opt-in rewarded ad: watch to double the haul just credited (or,
            // with Remove Ads owned, doubled outright with no ad). The sheet
            // STAYS OPEN whatever the ad does — closing it threw the summary
            // away as punishment for an abandoned ad, and swallowed the proof
            // of a watched one.
            if (summary.gains.Count > 0 && _loop.RewardedReady(RewardedPlacement.OfflineBoost))
            {
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
                            _loop.GrantOfflineBonus(summary);
                            _loop.Telemetry.LogEvent("rewarded_ad", ("placement", "offline_boost"));
                            foreach (var gain in gainLines)
                            {
                                gain.line.text = "<color=" + MossDeepHex + ">+" + NumberFormat.Short(gain.amount * 2)
                                                 + " " + gain.id + " (doubled)</color>";
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
                            // flag guards a destroyed reference as well.)
                            if (!doubled)
                            {
                                doubleIt.interactable = true;
                                SetButtonTint(doubleIt, true, true);
                                SetButtonLabel(doubleIt, originalLabel);
                            }
                        });
                });
                KeyAction(doubleIt);
            }

            Button(sheet, "Continue", 320, CloseSheet);
        }

        internal void OpenMigrationSheet()
        {
            var gain = System.Math.Max(0.0, _loop.VerdureAfterMigration() - _loop.State.verdurePoints);
            // The scrim stays inert on the run's one destructive confirm — a
            // stray tap must not answer it either way; Back still cancels.
            var sheet = BeginSheet(scrimDismisses: false);
            MakeText(sheet, "Fold the camp", 32, TextAnchor.UpperCenter, Ink, _serif);

            // Say plainly what a fold IS before saying what it costs. "They were
            // never yours" is the right voice but it isn't an explanation, and a
            // player who has never met a prestige reset can't infer one.
            MakeText(sheet, "<b>Start this camp over, on purpose.</b>\nYou begin again in the first meadow with nothing built,"
                            + " and every run after this one runs richer.",
                20, TextAnchor.MiddleCenter, Ink, _serif);

            var bonus = _loop.Data.economy?.verdure?.yieldBonusPerPoint ?? 0.0;
            var after = Mathf.FloorToInt((float)_loop.VerdureAfterMigration());
            var carried = "<color=" + MossDeepHex + "><b>You carry:</b> +" + Mathf.FloorToInt((float)gain)
                          + " Verdure (" + after + " in all"
                          + (bonus > 0.0 ? ", worth +" + Mathf.RoundToInt((float)(after * bonus * 100.0)) + "% to everything you gather, for good" : string.Empty)
                          + "). The kith walk with you, and the journal keeps every plate, sketch and Almanac node.</color>";
            MakeText(sheet, carried, 19, TextAnchor.MiddleCenter, Ink);

            // Signatures about to sharpen (design §4): the creature's memory
            // arguing FOR the fold, named before the cost line.
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

                    names.Append(familiar.name).Append("'s ")
                        .Append(_loop.FamiliarTrait(familiar)?.displayName ?? "way");
                }

                MakeText(sheet, "<color=" + MossDeepHex + "><i>" + names + (sharpenings.Count > 1 ? " deepen" : " deepens")
                                + " at this fold, and the plate takes a new line.</i></color>",
                    19, TextAnchor.MiddleCenter, Ink, _serif);
            }

            MakeText(sheet, "<b>You leave behind:</b> coin and stores, the camp buildings, tools and gear,"
                            + " the trails you opened, and every skill level. <i>They were never yours</i>.",
                19, TextAnchor.MiddleCenter, Ink2, _serif);

            // The §8 forecast's region preview — the fold names where it leads.
            var ahead = _loop.NextRegion();
            if (ahead != null)
            {
                MakeText(sheet, "<i>Ahead: " + ahead.displayName + ".</i>", 19, TextAnchor.MiddleCenter, Ink2, _serif);
            }

            // Where Verdure comes from. The "how close is the next point"
            // question is answered by the fold banner's percentage, not here.
            MakeText(sheet, "<i>Verdure is drawn from LIFETIME Renown: " + NumberFormat.Short(_loop.State.renown)
                            + " earned so far, and never spent. Renown comes from every level the kith and the"
                            + " crafts earn, and from what the Rite is given.</i>",
                17, TextAnchor.MiddleCenter, Ink2, _serif);
            Button(sheet, "Stay a while", 320, CloseSheet);
            var migrate = Button(sheet, "Fold and begin again", 320, () =>
            {
                CloseSheet();
                if (_loop.Migrate())
                {
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

            // The land's one line about the season just arrived in (§8) —
            // Migrate has already run, so the CURRENT region is the new one.
            var region = _loop.CurrentRegion();
            if (region != null && !string.IsNullOrEmpty(region.sign))
            {
                MakeText(dim.transform, "<i>" + region.sign + "</i>", 22, TextAnchor.MiddleCenter,
                    new Color(NightText.r, NightText.g, NightText.b, 0.75f), _serif);
            }

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

        /// <summary>
        /// Rename a familiar. <paramref name="onClosed"/> runs after the sheet
        /// closes however it closes — saved, cancelled, or dismissed by the
        /// scrim — so a caller mid-decision can put its own sheet back up. The
        /// station sheet uses it: renaming there is an aside, and losing the
        /// post you were choosing would make the pen cost more than it offers.
        /// </summary>
        internal void OpenNamingSheet(Familiar familiar, System.Action onClosed = null)
        {
            System.Action done = () =>
            {
                CloseSheet();
                if (onClosed != null)
                {
                    onClosed();
                }
            };

            // The dismiss hook takes over closing entirely when it is set (see
            // DismissSheet), so it has to close as well as continue.
            var sheet = onClosed == null ? BeginSheet() : BeginSheet(done);
            MakeText(sheet, "Rename", 32, TextAnchor.UpperCenter, Ink, _serif);

            var cost = Mathf.FloorToInt((float)_loop.RenameCost());
            if (cost > 0)
            {
                MakeText(sheet, "a new name asks " + SizeOpen(19) + "<color=" + OchreHex + ">"
                                + cost + " amber</color></size>", 18, TextAnchor.UpperCenter, Ink2, _hand);
            }

            var field = MakeInputField(sheet, familiar.name);

            // Refusals must land INSIDE the sheet — the margin note sits under
            // the scrim, so "Save did nothing" read as a broken button.
            var error = MakeText(sheet, string.Empty, 16, TextAnchor.MiddleCenter, Ink2, _serif);

            var save = Button(sheet, cost > 0 ? "Save · " + cost + " amber" : "Save", 320, () =>
            {
                var typed = field.text;
                if (string.IsNullOrWhiteSpace(typed) || typed.Trim() == familiar.name)
                {
                    // Nothing changed — no charge, just close.
                    done();
                    return;
                }

                // Amber is premium and hard-won — refuse rather than part-charge.
                if (!_loop.CanRenameFamiliar())
                {
                    error.text = "<color=" + OchreInkHex + "><i>not enough amber. resin is dear, and the old name holds.</i></color>";
                    return;
                }

                if (_loop.RenameFamiliar(familiar, typed))
                {
                    SetNote(cost > 0 ? "a name, paid in resin, set in the journal." : "a new name, set in the journal.");
                    _dirty = true;
                }

                done();
            });
            KeyAction(save);

            Button(sheet, "Cancel", 320, () => done());
        }

        /// <summary>
        /// The posting sheet — the strip's badges are the post affordance
        /// (one body per post, design §2), so the sheet asks only "who".
        /// Whoever holds the post is named at the top and gets their own
        /// "send to camp" line; everyone else is a one-tap REPLACE, which is
        /// the ordinary way to change who works a node — the holder steps back
        /// to camp in the same move, freeing their slot for the newcomer.
        /// When the post stands empty and the kith is fully committed, a notice
        /// explains the only ways forward (rob another post, or grow the kith),
        /// and any who can't take it until a slot opens are shown but disabled.
        /// </summary>
        internal void OpenPostingSheet(string stationId)
        {
            var sheet = BeginSheet();
            MakeText(sheet, "Who walks here?", 32, TextAnchor.UpperCenter, Ink, _serif);

            var state = _loop.State;
            var occupantHere = Stationing.OccupantOf(state, stationId);
            var hasRoom = Kith.HasRoom(state, _loop.Data);

            // The warden stands at a node, or now takes the wander post and
            // roams (design §2) — but never the trail: the warden tends, the
            // kith carries.
            var node = FindNode(stationId);
            var isWanderPost = stationId == Familiar.WanderStation;
            var wardenCanStand = node != null || isWanderPost;
            var wardenHere = wardenCanStand
                && (node != null ? Warden.PostNodeId(state) == node.id : Warden.IsWandering(state));

            // The post itself, drawn the way the rows below it are drawn: its
            // crop on the left, whoever holds it on the right, the words
            // between. The heading is then the same shape as the choice it
            // introduces — "instead of them, who?" — instead of a portrait and
            // three separate lines of type saying the same thing.
            var holder = occupantHere != null
                ? occupantHere.name + " walks here"
                : wardenHere ? "the warden walks here" : "no one walks here";
            var holderPlate = occupantHere != null ? ArtLibrary.ForSpecies(occupantHere.speciesId) : null;
            PictureRow(sheet, StationPlate(stationId),
                StationLabel(stationId).ToUpperInvariant()
                + "\n" + SizeOpen(16) + "<color=" + (occupantHere != null || wardenHere ? InkHex : Ink2Hex) + ">"
                + holder + "</color></size>", holderPlate, 120f, 740f);

            MakeText(sheet, "posts walked " + _loop.KithWalking() + " of " + _loop.KithSlots(),
                14, TextAnchor.UpperCenter, Ink2, _smallCaps);

            // Standing the holder down is its own act, not something you
            // stumble into by tapping their name in a list of candidates.
            if (occupantHere != null)
            {
                var standing = occupantHere;
                Button(sheet, "Send " + standing.name + " back to camp", 740, () =>
                {
                    Station(standing, null);
                    CloseSheet();
                });
            }
            else if (wardenHere)
            {
                Button(sheet, "Send the warden back to camp", 740, () =>
                {
                    _loop.RestWarden();
                    SetNote("the warden steps back to camp.");
                    CloseSheet();
                });
            }

            if (wardenCanStand && !wardenHere)
            {
                // Moss verbs — these are the actions the sheet exists for;
                // ochre made them read as warnings.
                var wardenVerb = isWanderPost ? "Send the warden wandering" : "Walk the warden here";
                Button(sheet, "<color=" + MossDeepHex + ">" + wardenVerb + "</color>  "
                              + SizeOpen(15) + "<color=" + Ink2Hex + ">" + WardenWhereabouts() + "</color></size>", 740, () =>
                {
                    if (isWanderPost)
                    {
                        _loop.WanderWarden();
                        SetNote("the warden sets off to wander the run.");
                    }
                    else
                    {
                        _loop.PostWarden(node);
                        SetNote("the warden walks to " + StationLabel(node.id) + ".");
                    }

                    CloseSheet();
                });
            }

            // The friction the old sheet hid: to fill an EMPTY post with the
            // kith fully committed, you either rob another post or grow the
            // kith. Name it up front so the flat roster list isn't a puzzle.
            if (occupantHere == null)
            {
                var anyResting = false;
                foreach (var f in state.roster)
                {
                    if (f.IsResting)
                    {
                        anyResting = true;
                        break;
                    }
                }

                string notice = null;
                if (state.roster.Count == 0)
                {
                    // A brand-new warden's first tap can land here — the sheet
                    // must answer "how do I ever fill this?" or it's a riddle.
                    // One instruction, in the order it is done: leave the pile,
                    // someone comes. The old line opened on what the land does
                    // and left the doing to be inferred.
                    notice = "no one walks with you yet. leave a pile of a plate's own goods on the Trail page and whoever is drawn to it comes to stay."
                             + (wardenCanStand ? " until then the warden can stand here alone." : string.Empty);
                }
                else if (!anyResting)
                {
                    notice = "everyone is already posted. move one here and the post they leave falls idle, or open a slot on the Ladder to walk with one more.";
                }
                else if (!hasRoom)
                {
                    notice = "someone waits at camp, but every slot is walked. open a slot on the Ladder to put them to work, or move a walker here from a post you need less.";
                }

                if (notice != null)
                {
                    var line = MakeText(sheet, "<i>" + notice + "</i>", 16, TextAnchor.UpperCenter, Ink2);
                    var element = line.gameObject.AddComponent<LayoutElement>();
                    element.minWidth = 740;
                    element.preferredWidth = 740;
                }
            }

            // Candidates only — the holder is handled above. Ordered so the
            // choice reads top-down: companions free to take it, then those a
            // move would pull off another post, then any who can't take this
            // empty post until a slot opens.
            var ordered = new List<Familiar>();
            foreach (var familiar in state.roster)
            {
                // The pony is never offered anywhere (§11): she walks her own
                // lane and cannot be posted, so listing her would only be a row
                // that refuses. This covers every post — nodes, the trail and
                // the wander post all open this sheet.
                if (!PostMatches(familiar.stationId, stationId) && !familiar.IsPony)
                {
                    ordered.Add(familiar);
                }
            }

            ordered.Sort((a, b) => PostRank(a, occupantHere, hasRoom).CompareTo(PostRank(b, occupantHere, hasRoom)));

            foreach (var familiar in ordered)
            {
                var captured = familiar;
                var resting = captured.IsResting;
                // A resting companion can only take an empty post when a slot is
                // free; swapping in for an occupant, or moving off another post,
                // always works (the vacated slot covers it).
                var blocked = resting && occupantHere == null && !hasRoom;

                // The verb IS the outcome — replacing the holder is a single
                // tap, and says so, rather than being inferred from a list.
                var verb = occupantHere != null ? "Replace " + occupantHere.name : "Post here";
                // Why a blocked line is dead is said ONCE, in the notice above —
                // repeating "needs an open slot" on every row it applies to
                // made a wall of the same sentence and pushed the sheet off
                // the screen. The greyed plate is the per-line signal.
                //
                // The creature and what it works are PICTURES: its own plate
                // leads the row, and the crop it stands over trails it. A
                // companion at camp trails nothing at all — an empty hand is
                // the plainest way to say "resting". Species and station names
                // only appear where there is no plate to show instead (the
                // trail and the wander post have no crop, and unmapped art
                // falls back to its word rather than vanishing).
                var portrait = ArtLibrary.ForSpecies(captured.speciesId);
                var working = resting ? null : StationPlate(captured.stationId);
                var speciesTail = portrait != null
                    ? string.Empty
                    : "  " + SizeOpen(15) + "<color=" + Ink2Hex + ">" + SpeciesName(captured.speciesId) + "</color></size>";
                var whereTail = resting || working != null
                    ? string.Empty
                    : "  " + SizeOpen(15) + "<color=" + Ink2Hex + ">" + StationLabel(captured.stationId) + "</color></size>";

                var button = PictureButton(sheet, portrait,
                    "<color=" + (blocked ? Ink2Hex : MossDeepHex) + ">" + verb + "</color>  "
                    // Wider than a plain row: two plates and a verb on ONE line
                    // needs the width the sheet's own notice already uses.
                    + captured.name + speciesTail + whereTail, working, 740, 120f, () =>
                {
                    Station(captured, stationId);
                    CloseSheet();
                });
                if (blocked)
                {
                    // Disabled with the reason spelled out above — tapping it
                    // would only fail with "every slot is walked".
                    button.interactable = false;
                    SetButtonTint(button, false);
                }
            }
        }

        /// <summary>
        /// The posting sheet's mirror, asked from the roster: not "who walks
        /// here?" but "where shall this one walk?" — every post as a one-tap
        /// destination, with the current holder named where a move would
        /// displace someone. The journal pages describe posts; this is how a
        /// page can also CHANGE one without a trip out to the world strip.
        /// </summary>
        internal void OpenStationPickSheet(Familiar familiar)
        {
            var sheet = BeginSheet();

            // The name is asked about here, so this is where it can be changed —
            // a quill beside it, rather than a word competing for room on every
            // roster line. Centred as a pair: the question and the pen read as
            // one heading, and the pen is next to the name it renames.
            var heading = Row((RectTransform)sheet);
            var headingLayout = heading.GetComponent<HorizontalLayoutGroup>();
            headingLayout.childAlignment = TextAnchor.MiddleCenter;
            headingLayout.spacing = 2;
            MakeText(heading.transform,
                familiar.IsPony ? familiar.name + " walks her own lane" : "Where shall " + familiar.name + " walk?",
                30, TextAnchor.MiddleCenter, Ink, _serif);
            IconButton(heading.transform, JournalSprites.QuillSprite(), 40f, 120f, () =>
            {
                CloseSheet();
                // Back to this sheet afterwards, rebuilt — so the new name is in
                // the question, and the post being chosen is not lost to an aside.
                OpenNamingSheet(familiar, () => OpenStationPickSheet(familiar));
            });

            // The one it is about and where they stand now, drawn as the rows
            // below are drawn: the animal on the "who" side, the crop they work
            // on the "where" side. The posting sheet's heading, mirrored — and
            // at camp the crop side is simply empty. Species and station are
            // spelled out only where no plate can stand in.
            var subject = ArtLibrary.ForSpecies(familiar.speciesId);
            PictureRow(sheet, subject,
                ((subject != null ? string.Empty : SpeciesName(familiar.speciesId) + " · ")
                 + "now " + StationLabel(familiar.stationId)).ToUpperInvariant(),
                StationPlate(familiar.stationId), 120f, 740f);

            // What this one is good at, at the moment it decides where they
            // walk — the roster row used to carry it on every line, which is
            // where it was read least and cost most.
            var trait = _loop.FamiliarTrait(familiar);
            if (trait != null)
            {
                MakeText(sheet, "<i>" + trait.displayName.ToLowerInvariant() + ": " + trait.description + "</i>",
                    17, TextAnchor.UpperCenter, MossDeep, _serif);
            }

            // What walking with this one for a long time has actually bought.
            // Kinship's three perks all compounded silently: the plate showed a
            // Roman numeral and the inscriptions it earned, and never once a
            // number — so the deepest bond in the grove read as a badge.
            var reckoning = KinshipReckoning(familiar);
            if (reckoning.Length > 0)
            {
                var lines = MakeText(sheet, reckoning, 16, TextAnchor.UpperCenter, Ink2);
                var element = lines.gameObject.AddComponent<LayoutElement>();
                element.minWidth = 740;
                element.preferredWidth = 740;
            }

            // The pony's page is the one page in the journal with nothing to
            // decide — say why, and stop. Renaming her stays available above,
            // because the name is the player's.
            if (familiar.IsPony)
            {
                var untamed = MakeText(sheet,
                    "<i>this wild pony can't be fully tamed. she comes to the panniers and no further. she walks her own lane, takes no slot from the kith, and will not be posted elsewhere or sent back to camp.</i>",
                    16, TextAnchor.UpperCenter, Ink2);
                var untamedElement = untamed.gameObject.AddComponent<LayoutElement>();
                untamedElement.minWidth = 740;
                untamedElement.preferredWidth = 740;
                return;
            }

            // Said once, above the list: from rest, an EMPTY post needs a free
            // slot, while stepping in for someone always works. It used to be
            // repeated as a tail on every empty destination.
            if (familiar.IsResting && !Kith.HasRoom(_loop.State, _loop.Data))
            {
                var notice = MakeText(sheet,
                    "<i>every slot is walked, so the empty posts stay shut. open one on the Ladder to walk with one more. stepping in for someone already posted still works: they go back to camp.</i>",
                    16, TextAnchor.UpperCenter, Ink2);
                var element = notice.gameObject.AddComponent<LayoutElement>();
                element.minWidth = 740;
                element.preferredWidth = 740;
            }

            if (!familiar.IsResting)
            {
                Button(sheet, "Send " + familiar.name + " back to camp", 740, () =>
                {
                    Station(familiar, null);
                    CloseSheet();
                });
            }

            foreach (var node in _loop.State.nodes)
            {
                AddStationChoice(sheet, familiar, node.id);
            }

            AddStationChoice(sheet, familiar, Familiar.WanderStation);
        }

        /// <summary>
        /// Kinship's perks in numbers (design §4) — the XP rate this one earns
        /// at a post, how far its signature has deepened, and the level it
        /// begins every run at. All three compounded invisibly: the roster
        /// showed a Roman numeral and the lines the bond had earned, so the
        /// deepest friendship in the grove read as a badge rather than a
        /// reckoning. Each clause appears only once it has something to say, and
        /// the whole thing is empty on unconfigured data (fixtures).
        /// </summary>
        private string KinshipReckoning(Familiar familiar)
        {
            var parts = new List<string>();
            var baseRate = _loop.FamiliarBaseXpPerSecond();
            if (baseRate > 0.0)
            {
                if (familiar.IsResting)
                {
                    // The clearest statement of what a slot buys: resting is
                    // fully idle, and that is a decision, not an oversight.
                    parts.Add("resting at camp: learns nothing, and works nothing");
                }
                else
                {
                    var bands = new List<string>();
                    var comfort = _loop.ComfortXpMultiplier();
                    if (comfort > 1.0)
                    {
                        bands.Add("+" + Percent(comfort - 1.0) + " the roosts");
                    }

                    var kinshipRate = _loop.FamiliarKinshipXpRate(familiar);
                    if (kinshipRate > 1.0)
                    {
                        bands.Add("+" + Percent(kinshipRate - 1.0) + " kinship");
                    }

                    parts.Add("learns " + PlainNumber(_loop.FamiliarXpPerSecond(familiar)) + " xp/s at a post"
                              + (bands.Count > 0 ? "  (" + string.Join(" · ", bands) + ")" : string.Empty));
                }
            }

            if (_loop.FamiliarKinship(familiar) > 0)
            {
                parts.Add("begins every run at level " + Roman(_loop.FamiliarStartingLevel(familiar))
                          + ", whatever the fold takes");

                // The trait's own numbers, deepened — the one perk whose size
                // is authored per species, so it can only be said here.
                var trait = _loop.FamiliarTrait(familiar);
                var deepening = _loop.FamiliarTraitDeepening(familiar);
                if (trait != null && deepening > 1.0 && trait.value > 0.0)
                {
                    parts.Add(trait.displayName.ToLowerInvariant() + " has deepened to +"
                              + Percent(trait.value * deepening) + ", from +" + Percent(trait.value));
                }
            }

            return string.Join("\n", parts);
        }

        /// <summary>One destination line of the station-pick sheet — skipped when the familiar already holds it.</summary>
        private void AddStationChoice(Transform sheet, Familiar familiar, string stationId)
        {
            if (PostMatches(familiar.stationId, stationId))
            {
                return;
            }

            var occupant = Stationing.OccupantOf(_loop.State, stationId);
            // Taking an empty post from rest needs an open slot; a swap or a
            // move always works (the vacated slot covers it) — the posting
            // sheet's rule, asked from the other side. The reason is on the
            // notice above; the line itself just greys out.
            var blocked = familiar.IsResting && occupant == null && !Kith.HasRoom(_loop.State, _loop.Data);

            // The posting sheet's rows, mirrored: the crop leads (it is the
            // WHERE being offered) and whoever stands there trails it. An empty
            // post trails nothing — the plainest way to say "stands empty" —
            // and since only one of each species walks with you, the plate
            // names the individual it would replace. Words fill in only where
            // no plate can: the trail and the wander post have no crop, and a
            // species without art keeps its name.
            var where = StationPlate(stationId);
            var held = occupant != null ? ArtLibrary.ForSpecies(occupant.speciesId) : null;
            var replaces = occupant != null && held == null
                ? "  " + SizeOpen(15) + "<color=" + Ink2Hex + ">replaces " + occupant.name + "</color></size>"
                : string.Empty;

            var button = PictureButton(sheet, where,
                "<color=" + (blocked ? Ink2Hex : MossDeepHex) + ">Walk to " + StationLabel(stationId)
                + "</color>" + replaces, held, 740, 120f, () =>
            {
                Station(familiar, stationId);
                CloseSheet();
            });
            if (blocked)
            {
                button.interactable = false;
                SetButtonTint(button, false);
            }
        }

        /// <summary>Display order for the posting sheet's candidates: free, movable, then slot-blocked.</summary>
        private static int PostRank(Familiar familiar, Familiar occupantHere, bool hasRoom)
        {
            if (familiar.IsResting)
            {
                return occupantHere == null && !hasRoom ? 2 : 0;
            }

            return 1;
        }

        /// <summary>Where the warden stands now, for the sheet's detail line.</summary>
        private string WardenWhereabouts()
        {
            var postId = Warden.PostNodeId(_loop.State);
            return postId == null ? "now: at camp" : "now: " + StationLabel(postId);
        }

        /// <summary>
        /// The plate for what a post yields — the resource of the node it is.
        /// Null for camp (no station at all), the trail, the wander post and
        /// dig stations: none of those stand over a crop, so there is no
        /// picture to show and the row falls back to naming them.
        /// </summary>
        private Sprite StationPlate(string stationId)
        {
            var node = FindNode(stationId);
            return node == null ? null : ArtLibrary.ForResource(node.resourceId);
        }

        private NodeState FindNode(string stationId)
        {
            foreach (var node in _loop.State.nodes)
            {
                if (node.id == stationId)
                {
                    return node;
                }
            }

            return null;
        }

        private static bool PostMatches(string stationId, string buttonStationId)
        {
            return string.IsNullOrEmpty(stationId) ? string.IsNullOrEmpty(buttonStationId) : stationId == buttonStationId;
        }

        internal void Station(Familiar familiar, string stationId)
        {
            if (!_loop.StationFamiliar(familiar, stationId))
            {
                // The ladder said no — every slot already walks (design §4).
                SetNote("every slot is walked. rest someone before " + familiar.name + " takes a post.");
                return;
            }

            SetNote(string.IsNullOrEmpty(stationId)
                ? familiar.name + " rests at camp, watching the fire."
                : familiar.name + " walks to " + StationLabel(stationId) + ".");
        }

        /// <summary>
        /// The camp-actions strip at the head of the Camp page: the rewarded
        /// time-skip and the one-off remove-ads purchase.
        /// </summary>
        internal void BuildCampActions(Transform root)
        {
            var bar = MakePanel("CampActions", (RectTransform)root, CardPaper);
            var layout = bar.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.padding = new RectOffset(12, 12, 8, 8);
            layout.spacing = 10;
            var element = bar.AddComponent<LayoutElement>();
            element.flexibleHeight = 0;
            AddBorder(bar, Ink2);

            _timeSkipButton = Button(bar.transform, TimeSkipLabel(), 380, OnTimeSkip);
            KeyAction(_timeSkipButton);

            // Hidden until the store resolves ownership (RefreshCampActions is the
            // authority): shown only once billing is initialised and the player
            // doesn't already own Remove Ads. Built inactive so an owner never sees
            // it flash up before entitlements land.
            _removeAdsButton = Button(bar.transform, RemoveAdsLabel(), 300, OnRemoveAds);
            _removeAdsButton.gameObject.SetActive(false);

            RefreshCampActions();
        }

        /// <summary>
        /// Keep the camp-strip buttons current — the Camp page registers this
        /// as one of its live updaters, and it no-ops on every other tab, where
        /// the strip isn't built. The time-skip greys out and counts down while
        /// its reward cooldown holds, rather than accepting a tap only to
        /// refuse it with a note.
        /// </summary>
        internal void RefreshCampActions()
        {
            if (_timeSkipButton == null || _loop == null || _loop.State == null)
            {
                return;
            }

            var ready = _loop.CanTimeSkipReward;
            _timeSkipButton.interactable = ready;
            SetButtonTint(_timeSkipButton, ready, true);
            SetButtonLabel(_timeSkipButton, ready
                ? TimeSkipLabel()
                : "Pass the time (ready in " + NumberFormat.Duration(_loop.TimeSkipRewardCooldownRemaining) + ")");

            if (_removeAdsButton != null)
            {
                // Show only once billing has resolved entitlements and the player
                // doesn't already own it. Owning Remove Ads — or a store that's
                // still connecting or unavailable — keeps it hidden.
                _removeAdsButton.gameObject.SetActive(_loop.Store.IsInitialised && !_loop.Store.RemoveAdsOwned);
                if (!_removeAdsPending)
                {
                    // The store's price lands after the catalogue fetch — keep
                    // the label current so the tap is never a surprise dialog.
                    SetButtonLabel(_removeAdsButton, RemoveAdsLabel());
                }
            }
        }

        /// <summary>"Remove ads · $x.xx" once the catalogue has priced it.</summary>
        private string RemoveAdsLabel()
        {
            var price = _loop.Store.PriceLabel(StoreProductIds.RemoveAds);
            return string.IsNullOrEmpty(price) ? "Remove ads" : "Remove ads · " + price;
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

        /// <summary>
        /// The colophon — the plates and the hands that drew them. The five CC BY
        /// works are named in full with their licence and the change made to
        /// them, because that is what the licence asks of a shipped build; the
        /// public-domain remainder is thanked without obligation.
        /// </summary>
        internal void OpenColophonSheet()
        {
            var sheet = BeginSheet();
            MakeText(sheet, "The Colophon", 32, TextAnchor.UpperCenter, Ink, _serif);
            MakeText(sheet, ArtCredits.Preamble, 17, TextAnchor.UpperLeft, Ink2, _serif);

            foreach (var work in ArtCredits.Licensed)
            {
                MakeText(sheet, ArtCredits.Line(work), 15, TextAnchor.UpperLeft, Ink, _serif);
            }

            MakeText(sheet, ArtCredits.PublicDomainNote, 15, TextAnchor.UpperLeft, Ink2, _serif);
            var done = Button(sheet, "Close the book", 320, CloseSheet);
            KeyAction(done);
        }

        /// <summary>
        /// A modal yes/no confirmation — a title, a body line, and a paired
        /// "never mind" / go-ahead choice styled like the Fold sheet's Migrate.
        /// The confirmed action runs after the sheet closes, so it may open a
        /// sheet of its own.
        /// </summary>
        internal void OpenConfirmSheet(string title, string body, string confirmLabel, System.Action onConfirm)
        {
            // Same rule as the Fold sheet: a confirm's scrim is inert.
            var sheet = BeginSheet(scrimDismisses: false);
            MakeText(sheet, title, 32, TextAnchor.UpperCenter, Ink, _serif);
            if (!string.IsNullOrEmpty(body))
            {
                MakeText(sheet, body, 20, TextAnchor.MiddleCenter, Ink2, _serif);
            }

            Button(sheet, "Never mind", 320, CloseSheet);
            var confirm = Button(sheet, confirmLabel, 320, () =>
            {
                CloseSheet();
                onConfirm?.Invoke();
            });
            KeyAction(confirm);
        }

        /// <summary>"Pass the time, +2 hours" (+ the "watch a short ad" tail until Remove Ads is owned) — says what the reward gives.</summary>
        private string TimeSkipLabel()
        {
            var unit = System.Math.Abs(TimeSkipHours - 1.0) < 0.0001 ? "hour" : "hours";
            return "Pass the time, +" + TimeSkipHours.ToString("0.##") + " " + unit + _loop.RewardedActionSuffix;
        }

        private void OnTimeSkip()
        {
            if (!_loop.CanTimeSkipReward)
            {
                // Still cooling down — refuse before showing an ad or granting.
                SetNote("the land has given its hours for now. let a while pass.");
                return;
            }

            _loop.WatchRewarded(RewardedPlacement.TimeSkip,
                () =>
                {
                    if (!_loop.CreditTimeSkip(TimeSkipHours))
                    {
                        return;
                    }

                    _loop.Telemetry.LogEvent("rewarded_ad", ("placement", "time_skip"));
                    SetNote(NumberFormat.Duration(TimeSkipHours * 3600.0)
                            + " pass in a breath, and the kith kept to the work.");
                    _dirty = true;
                });
        }

        private void OnRemoveAds()
        {
            if (_removeAdsButton != null)
            {
                // Don't let a second tap launch a second Play flow while the first
                // is open — Google rejects the re-buy with "you already own this
                // item". Re-enabled when the purchase doesn't go through, and
                // visibly pending meanwhile — a silent dead button reads broken.
                _removeAdsPending = true;
                _removeAdsButton.interactable = false;
                SetButtonTint(_removeAdsButton, false);
                SetButtonLabel(_removeAdsButton, "Opening the store…");
            }

            _loop.Store.Purchase(StoreProductIds.RemoveAds, result =>
            {
                _removeAdsPending = false;
                switch (result)
                {
                    case StoreResult.Purchased:
                    case StoreResult.AlreadyOwned:
                        if (_removeAdsButton != null)
                        {
                            _removeAdsButton.gameObject.SetActive(false);
                        }

                        SetNote("The ads step aside. Thank you for keeping the grove.");
                        break;
                    case StoreResult.Cancelled:
                        // The player backed out of the Play sheet — the most
                        // ordinary outcome. Restore the button, say nothing.
                        RestoreRemoveAdsButton();
                        break;
                    case StoreResult.Failed:
                        RestoreRemoveAdsButton();
                        SetNote("That didn't go through. Nothing was charged.");
                        break;
                }
            });
        }

        private void RestoreRemoveAdsButton()
        {
            if (_removeAdsButton != null)
            {
                _removeAdsButton.interactable = true;
                SetButtonTint(_removeAdsButton, true);
                SetButtonLabel(_removeAdsButton, RemoveAdsLabel());
            }
        }

        /// <summary>
        /// Open the standard sheet scaffold. <paramref name="dismiss"/> is the
        /// sheet's safe way out (Back / scrim tap) — plain close by default;
        /// <paramref name="scrimDismisses"/> false keeps the scrim inert for
        /// confirms, where a stray tap must never answer the question (Back
        /// still cancels — cancelling is always safe).
        /// <para>
        /// The panel hugs its content until it would outgrow the screen, then
        /// pins and scrolls: the sheets whose length is a function of the save
        /// (the posting sheet lists the whole roster, the station pick every
        /// post) used to grow straight off the top and bottom of the display,
        /// taking "Never mind" with them. The scroll layer lives INSIDE the
        /// panel so the card, its rules and its padding stay put and only the
        /// lines move.
        /// </para>
        /// </summary>
        private Transform BeginSheet(System.Action dismiss = null, bool scrimDismisses = true)
        {
            var dim = MakePanel("Sheet", (RectTransform)_modalLayer, DimColor);
            Stretch((RectTransform)dim.transform);
            _sheet = dim;
            _sheetDismiss = dismiss;

            var panel = MakePanel("Panel", (RectTransform)dim.transform, PagePaper);
            if (scrimDismisses)
            {
                var tap = dim.AddComponent<ScrimTap>();
                tap.OnTap = DismissSheet;
                tap.Panel = (RectTransform)panel.transform;
            }
            AddBorder(panel, Ink2);
            AddBorder(panel, RulePaper, 6f);
            var rt = (RectTransform)panel.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            // Fixed width, hugged height — a fixed height leaves a welcome-back
            // note floating in a half-empty card.
            rt.sizeDelta = new Vector2(800, 0);
            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            // The scroll layer is the panel's only child and carries no width of
            // its own — it takes the card's.
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(30, 30, 30, 36);
            layout.spacing = 0;
            var fitter = panel.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scrollGo = new GameObject("SheetScroll", typeof(RectTransform), typeof(ScrollRect), typeof(RectMask2D));
            scrollGo.transform.SetParent(panel.transform, false);
            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 24f;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.viewport = (RectTransform)scrollGo.transform;

            var content = MakeRect("Content", (RectTransform)scrollGo.transform);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            var contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.childAlignment = TextAnchor.UpperCenter;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = false;
            contentLayout.childForceExpandHeight = false;
            contentLayout.spacing = 16;
            var contentFitter = content.gameObject.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;

            // Hug the lines until they'd fill the screen, then pin — the panel's
            // own padding rides on top of this, so the card lands near 80%.
            var clamp = scrollGo.AddComponent<HeightClampedElement>();
            clamp.content = content;
            clamp.canvas = (RectTransform)_modalLayer;
            clamp.maxCanvasShare = SheetMaxCanvasShare;

            AddSheetStitch(panel, scroll);
            // A cross in the corner, where a hand looks for the way out — but
            // only where leaving is free. A sheet whose scrim is inert is
            // asking a question, and those keep their own worded way out
            // rather than gaining a corner that answers for the player.
            if (scrimDismisses)
            {
                AddSheetClose(panel);
            }

            return content;
        }

        /// <summary>
        /// The sheet's close cross — pinned to the panel's top-right corner,
        /// outside the vertical flow (and so outside the scroll), so a long
        /// sheet can't carry it off the bottom of the screen the way a trailing
        /// "Never mind" did.
        /// </summary>
        private void AddSheetClose(GameObject panel)
        {
            var close = IconButton(panel.transform, JournalSprites.CrossSprite(), 36f, 96f, DismissSheet);
            var element = close.GetComponent<LayoutElement>();
            element.ignoreLayout = true;
            var rect = (RectTransform)close.transform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            // Sized by hand: an ignored layout element still carries the touch
            // size, but nothing is left to apply it.
            rect.sizeDelta = new Vector2(96f, 96f);
            // Riding the panel's own padding, so it sits in the corner rather
            // than shouldering the title out of the middle.
            rect.anchoredPosition = new Vector2(-6f, -6f);
        }

        /// <summary>
        /// The sheet's scroll stitch — the page's slim ink scrollbar, riding the
        /// panel's right padding. Outside the scroll's mask so it isn't clipped,
        /// and auto-hidden while the sheet fits, so its presence is itself the
        /// signal that there is more below.
        /// </summary>
        private static void AddSheetStitch(GameObject panel, ScrollRect scroll)
        {
            var barGo = new GameObject("Stitch", typeof(Image), typeof(Scrollbar), typeof(LayoutElement));
            barGo.transform.SetParent(panel.transform, false);
            barGo.GetComponent<LayoutElement>().ignoreLayout = true;
            var track = barGo.GetComponent<Image>();
            track.color = new Color(RulePaper.r, RulePaper.g, RulePaper.b, 0.45f);
            var barRect = (RectTransform)barGo.transform;
            barRect.anchorMin = new Vector2(1f, 0f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(1f, 0.5f);
            // 6 wide, inset into the card's right padding, and short of the
            // panel's top and bottom padding so it reads as a margin rule.
            barRect.sizeDelta = new Vector2(6f, -60f);
            barRect.anchoredPosition = new Vector2(-12f, -3f);

            var handleGo = new GameObject("Handle", typeof(Image));
            handleGo.transform.SetParent(barGo.transform, false);
            var handle = handleGo.GetComponent<Image>();
            handle.color = new Color(Ink2.r, Ink2.g, Ink2.b, 0.55f);
            var handleRect = (RectTransform)handleGo.transform;
            handleRect.offsetMin = Vector2.zero;
            handleRect.offsetMax = Vector2.zero;

            var scrollbar = barGo.GetComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = handleRect;
            scrollbar.targetGraphic = handle;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            NoNavigation(scrollbar);
        }

        private void CloseSheet()
        {
            _sheetDismiss = null;
            if (_sheet != null)
            {
                Object.Destroy(_sheet);
                _sheet = null;
            }
        }
    }
}
