using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Wildgrove.Data;
using Wildgrove.Game.Services;
using Wildgrove.Sim;
using static Wildgrove.Game.JournalTheme;
using static Wildgrove.Game.JournalWidgets;

namespace Wildgrove.Game
{
    /// <summary>
    /// The journal's modal sheets and the persistent camp-actions strip. Owns
    /// the one-open-sheet lifecycle (<see cref="BeginSheet"/>/<see cref="CloseSheet"/>),
    /// the pending-sheet pump, the posting sheet, and the rewarded/purchase
    /// buttons pinned under the world gap.
    /// </summary>
    internal sealed class JournalSheets : JournalSection
    {
        // The time-skip ad credits this many hours of gathering.
        private const double TimeSkipHours = 2.0;
        private Button _timeSkipButton;
        private Button _removeAdsButton;

        internal JournalSheets(GameHud hud) : base(hud) { }

        internal void PumpSheets()
        {
            if (_sheet != null)
            {
                return;
            }

            var arrival = _loop.PeekPendingArrival();
            if (arrival != null)
            {
                OpenArrivalSheet(arrival);
                return;
            }

            var bond = _loop.TakePendingBondCelebration();
            if (bond != null)
            {
                OpenBondSheet(bond);
                return;
            }

            var summary = _loop.TakePendingOfflineSummary();
            if (summary != null && summary.creditedSeconds >= GameLoop.WelcomeBackMinSeconds)
            {
                OpenWelcomeSheet(summary);
                return;
            }

            var waystoneZone = Narrative.NextUnreadWaystone(_loop.State, _loop.Data);
            if (waystoneZone != null)
            {
                OpenWaystoneSheet(waystoneZone);
            }
        }

        private void OpenWaystoneSheet(ZoneData zone)
        {
            var text = Narrative.WaystoneText(_loop.Data, zone.id);
            var sheet = BeginSheet();
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
            });
        }

        private void OpenArrivalSheet(Familiar familiar)
        {
            var sheet = BeginSheet();
            MakeText(sheet, "A new friend", 32, TextAnchor.UpperCenter, Ink, _serif);
            MakeText(sheet, "a " + SpeciesName(familiar.speciesId) + " arrives", 22, TextAnchor.UpperCenter, Ink2, _hand);

            var field = MakeInputField(sheet, familiar.name);
            Button(sheet, "Walk together", 320, () =>
            {
                var typed = field.text;
                if (!string.IsNullOrWhiteSpace(typed))
                {
                    _loop.RenameFamiliar(familiar, typed);
                }

                _loop.TakePendingArrival();
                _dirty = true;
                CloseSheet();
            });
        }

        private void OpenBondSheet(BondData bond)
        {
            // First bond earned unlocks "First kith" (idempotent — later bonds no-op).
            _loop.GameServices.UnlockAchievement(AchievementIds.FirstKith);

            var sheet = BeginSheet();
            MakeText(sheet, "A bond is made", 32, TextAnchor.UpperCenter, Ink, _serif);
            MakeText(sheet, bond.displayName + " will cross every fold with you.", 22, TextAnchor.UpperCenter, Ink2, _serif);
            Button(sheet, "Walk together", 320, CloseSheet);
        }

        private void OpenWelcomeSheet(OfflineSummary summary)
        {
            var sheet = BeginSheet();
            MakeText(sheet, "Welcome back", 32, TextAnchor.UpperCenter, Ink, _serif);
            MakeText(sheet, "Away " + NumberFormat.Duration(summary.realSeconds)
                            + " · credited " + NumberFormat.Duration(summary.creditedSeconds), 20, TextAnchor.UpperCenter, Ink2);

            var lines = 0;
            foreach (var pair in summary.gains)
            {
                if (lines++ >= 6)
                {
                    break;
                }

                MakeText(sheet, "+" + NumberFormat.Short(pair.Value) + " " + pair.Key, 18, TextAnchor.MiddleCenter, Ink);
            }

            // Opt-in rewarded ad: watch to double the haul just credited.
            if (summary.gains.Count > 0 && _loop.Ads.IsRewardedReady)
            {
                var doubleIt = Button(sheet, "Double it — watch a short ad", 360, () =>
                {
                    _loop.Ads.ShowRewarded(RewardedPlacement.OfflineBoost,
                        () =>
                        {
                            _loop.GrantOfflineBonus(summary);
                            _loop.Telemetry.LogEvent("rewarded_ad", ("placement", "offline_boost"));
                            SetNote("The land gives twice — your haul is doubled.");
                            _dirty = true;
                        },
                        CloseSheet);
                });
                doubleIt.GetComponent<Image>().color = OchreWash;
                doubleIt.GetComponentInChildren<Text>().color = Ochre;
            }

            Button(sheet, "Continue", 320, CloseSheet);
        }

        internal void OpenMigrationSheet()
        {
            var gain = System.Math.Max(0.0, _loop.VerdureAfterMigration() - _loop.State.verdurePoints);
            var sheet = BeginSheet();
            MakeText(sheet, "Fold the camp", 32, TextAnchor.UpperCenter, Ink, _serif);
            MakeText(sheet, "Everything stays behind: the stores, the kit, the richness,\nthe buildings — <i>they were never yours</i>.",
                20, TextAnchor.MiddleCenter, Ink2, _serif);
            MakeText(sheet, "<color=" + MossDeepHex + ">The journal crosses entire, with +"
                            + Mathf.FloorToInt((float)gain) + " Verdure.\nThe kith walks with you.</color>",
                20, TextAnchor.MiddleCenter, Ink);
            Button(sheet, "Stay a while", 320, CloseSheet);
            var migrate = Button(sheet, "Migrate", 320, () =>
            {
                CloseSheet();
                if (_loop.Migrate())
                {
                    _dirty = true;
                    OpenVignette(gain);
                }
            });
            migrate.GetComponent<Image>().color = OchreWash;
            migrate.GetComponentInChildren<Text>().color = Ochre;
        }

        /// <summary>The full-dark Migration vignette — the mock's fixed overlay; tap anywhere to walk on.</summary>
        private void OpenVignette(double verdureGained)
        {
            var dim = MakePanel("Sheet", (RectTransform)_modalLayer, NightInk);
            Stretch((RectTransform)dim.transform);
            _sheet = dim;
            var tap = dim.AddComponent<Button>();
            tap.transition = Selectable.Transition.None;
            tap.onClick.AddListener(CloseSheet);

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

            MakeText(dim.transform, "+" + Mathf.FloorToInt((float)verdureGained) + " VERDURE", 20,
                TextAnchor.MiddleCenter, new Color(0.624f, 0.682f, 0.494f, 1f), _smallCaps);
            MakeText(dim.transform, "TAP TO WALK ON", 15, TextAnchor.MiddleCenter,
                new Color(NightText.r, NightText.g, NightText.b, 0.45f), _smallCaps);
        }

        internal void OpenNamingSheet(Familiar familiar)
        {
            var sheet = BeginSheet();
            MakeText(sheet, "Rename", 32, TextAnchor.UpperCenter, Ink, _serif);
            var field = MakeInputField(sheet, familiar.name);
            Button(sheet, "Save", 320, () =>
            {
                _loop.RenameFamiliar(familiar, field.text);
                _dirty = true;
                CloseSheet();
            });
            Button(sheet, "Cancel", 320, CloseSheet);
        }

        /// <summary>
        /// The posting sheet — the strip's badges are the post affordance
        /// (one body per post, design §2), so the sheet asks only "who".
        /// Picking someone new swaps them in; whoever held the post steps
        /// back to camp. Tapping the current holder sends them home.
        /// </summary>
        internal void OpenPostingSheet(string stationId)
        {
            var sheet = BeginSheet();
            MakeText(sheet, "Who walks here?", 32, TextAnchor.UpperCenter, Ink, _serif);
            MakeText(sheet, StationLabel(stationId).ToUpperInvariant(), 18, TextAnchor.UpperCenter, Ink2, _smallCaps);

            // The warden can only stand at a node — no warden entry on the
            // trail or wander sheets.
            var node = FindNode(stationId);
            if (node != null)
            {
                var wardenHere = Warden.PostNodeId(_loop.State) == node.id;
                var detail = wardenHere
                    ? "posted here — tap to send them back to camp"
                    : WardenWhereabouts();
                var warden = Button(sheet, "the warden  " + SizeOpen(15) + "<color=" + Ink2Hex + ">"
                                           + detail + "</color></size>", 560, () =>
                {
                    if (wardenHere)
                    {
                        _loop.RestWarden();
                        SetNote("the warden steps back to camp.");
                    }
                    else
                    {
                        _loop.PostWarden(node);
                        SetNote("the warden walks to " + StationLabel(node.id) + ".");
                    }

                    CloseSheet();
                });
                if (wardenHere)
                {
                    warden.GetComponent<Image>().color = MossWash;
                }
            }

            foreach (var familiar in _loop.State.roster)
            {
                var captured = familiar;
                var here = PostMatches(captured.stationId, stationId);
                var detail = here ? "posted here — tap to send them to camp" : "now: " + StationLabel(captured.stationId);
                var button = Button(sheet, captured.name + "  " + SizeOpen(15) + "<color=" + Ink2Hex + ">"
                                           + SpeciesName(captured.speciesId) + " · " + detail + "</color></size>", 560, () =>
                {
                    Station(captured, here ? null : stationId);
                    CloseSheet();
                });
                if (here)
                {
                    button.GetComponent<Image>().color = MossWash;
                }
            }

            Button(sheet, "Never mind", 320, CloseSheet);
        }

        /// <summary>Where the warden stands now, for the sheet's detail line.</summary>
        private string WardenWhereabouts()
        {
            var postId = Warden.PostNodeId(_loop.State);
            return postId == null ? "now: at camp" : "now: " + StationLabel(postId);
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
        /// The persistent camp-actions strip under the node/world gap: the
        /// rewarded time-skip and the one-off remove-ads purchase, reachable
        /// from every journal page.
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

            _timeSkipButton = Button(bar.transform, "Hasten a while — watch a short ad", 380, OnTimeSkip);
            _timeSkipButton.GetComponent<Image>().color = OchreWash;
            _timeSkipButton.GetComponentInChildren<Text>().color = Ochre;

            _removeAdsButton = Button(bar.transform, "Remove ads", 200, OnRemoveAds);
            if (_loop.Store.RemoveAdsOwned)
            {
                _removeAdsButton.gameObject.SetActive(false);
            }
        }

        private void OnTimeSkip()
        {
            _loop.Ads.ShowRewarded(RewardedPlacement.TimeSkip,
                () =>
                {
                    _loop.CreditTimeSkip(TimeSkipHours);
                    _loop.Telemetry.LogEvent("rewarded_ad", ("placement", "time_skip"));
                    SetNote(NumberFormat.Duration(TimeSkipHours * 3600.0)
                            + " pass in a breath — the kith kept to the work.");
                    _dirty = true;
                });
        }

        private void OnRemoveAds()
        {
            _loop.Store.Purchase(StoreProductIds.RemoveAds, result =>
            {
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
                    case StoreResult.Failed:
                        SetNote("That didn't go through — nothing was charged.");
                        break;
                }
            });
        }

        private Transform BeginSheet()
        {
            var dim = MakePanel("Sheet", (RectTransform)_modalLayer, DimColor);
            Stretch((RectTransform)dim.transform);
            _sheet = dim;

            var panel = MakePanel("Panel", (RectTransform)dim.transform, PagePaper);
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
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(30, 30, 30, 36);
            layout.spacing = 16;
            var fitter = panel.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return panel.transform;
        }

        private void CloseSheet()
        {
            if (_sheet != null)
            {
                Object.Destroy(_sheet);
                _sheet = null;
            }
        }
    }
}
