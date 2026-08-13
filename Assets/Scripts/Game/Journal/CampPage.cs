using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Wildgrove.Data;
using Wildgrove.Sim;
using static Wildgrove.Game.JournalTheme;
using static Wildgrove.Game.JournalFormat;
using static Wildgrove.Game.JournalWidgets;

namespace Wildgrove.Game
{
    /// <summary>
    /// The Camp page: what the camp makes of what the trail brings home. This
    /// file assembles the page and keeps its plainer furniture, the camp's name
    /// card and the Ladder's next rungs; the two subsystems with a card each of
    /// their own live in the partials beside it (<c>Amber</c>, <c>Crafting</c>).
    /// <para>
    /// The caravan was the third until 2026-08-13, when it went to the Stores
    /// page — a trade is stock for stock (see <c>StoresPage.Exchange</c>).
    /// </para>
    /// </summary>
    internal sealed partial class CampPage : JournalSection
    {
        // How many unpurchased Ladder rungs the camp shows at once.
        private const int UpgradeWindow = 3;

        // The two cards on this page whose own button can grow the page above
        // them — a rung or a raise that opens a recipe builds a station card,
        // and the crafting cards are the first thing on the page. Both presses
        // therefore keep their card's place rather than the scrolled distance.
        // Not fold ids: nothing folds here, and a collision with one would
        // scroll to the wrong card.
        private const string LadderAnchor = "camp-ladder";
        private const string BuildingsAnchor = "camp-buildings";

        internal CampPage(GameHud hud) : base(hud) { }

        internal void BuildCampPage()
        {
            // Remove Ads until it's owned, and nothing else: a purchase is camp
            // business, so it heads the camp's page rather than a bar pinned in
            // the chrome that all four tabs would pay for. Being in the body also
            // means it refreshes off the page's own updater pool rather than the
            // HUD's chrome pass.
            //
            // The rewarded time-skip was the other half of this strip until
            // 2026-08-13, and it did go to the chrome — but to the events rail,
            // which is paid for out of the world band's left MARGIN rather than
            // out of any page's height, so the objection above never applied to
            // it. A clock counting itself down is what that rail is for.
            _hud.Sheets.BuildCampActions(_body);
            _liveUpdaters.Add(() => _hud.Sheets.RefreshCampActions());

            BuildCampNameCard();
            BuildCraftingCards();
            BuildBuildingsCard();
            BuildLadderCard();
            BuildAmberCard();
        }

        /// <summary>
        /// The page's own head: what this camp is called (design §9's sink
        /// slate) — the Warden page's name card, worn by the run instead of
        /// the player. The quill opens the naming sheet, which carries the
        /// price; the card says the name and nothing else, for the same
        /// measured-at-zero-width reason the warden's does.
        /// </summary>
        private void BuildCampNameCard()
        {
            var card = Card("THE CAMP");

            var row = Row(card);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 2;

            var name = MakeText(row.transform, _loop.CampName(), 30, TextAnchor.MiddleCenter, Ink, _serif);
            IconButton(row.transform, JournalSprites.QuillSprite(), 40f, 120f,
                () => _hud.Sheets.OpenCampNamingSheet());

            _liveUpdaters.Add(() => name.text = _loop.CampName());
        }

        private void BuildBuildingsCard()
        {
            var card = Card("BUILDING LINES");
            Anchor(BuildingsAnchor, card);
            foreach (var building in _loop.Data.buildings)
            {
                var captured = building;
                // Filter to what the run can see, like the crafting card —
                // every line in the game data from minute one names resources
                // the player hasn't met. A line waits until every good its next
                // level asks for has been gathered.
                if (!BundleDiscovered(_loop.NextBuildingBundle(captured)))
                {
                    continue;
                }

                var gives = PerLevelGivesLabel(captured);
                var row = Row(card);
                var plate = ArtLibrary.ForBuilding(captured.id);
                if (plate != null)
                {
                    IconImage(row.transform, plate, 64f, Color.white);
                }

                var label = MakeText(row.transform, string.Empty, 19, TextAnchor.MiddleLeft, Ink);
                FlexibleWidth(label.gameObject, 1f);
                Button build = null;
                build = Button(row.transform, "Raise", 160, () =>
                {
                    if (_loop.BuyBuildingLevel(captured))
                    {
                        Flash(build, "raised", true);
                        SetNote(captured.displayName.ToLowerInvariant() + " goes up. the camp sleeps closer to the work.");
                        // A raise can bring a station up to a recipe's heat,
                        // which adds a row to a card above this one.
                        KeepInPlace(BuildingsAnchor, card);
                    }
                });

                _liveUpdaters.Add(() =>
                {
                    label.text = captured.displayName + "  <color=" + Ink2Hex + ">level " + _loop.BuildingLevel(captured) + "</color>"
                                 + (gives != null ? "\n" + SizeOpen(15) + "<color=" + Ink2Hex + ">" + gives + "</color></size>" : string.Empty)
                                 + "\n" + SizeOpen(15) + "<color=" + Ink2Hex + ">next: " + BundleHaveLabel(_loop.NextBuildingBundle(captured)) + "</color></size>";
                    var ok = _loop.CanAffordBuilding(captured);
                    build.interactable = ok;
                    SetButtonTint(build, ok);
                });
            }
        }

        private bool BundleDiscovered(List<Buildings.MaterialCost> bundle)
        {
            foreach (var cost in bundle ?? new List<Buildings.MaterialCost>())
            {
                if (!GoodDiscovered(cost.id))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>A good is known by doing: gathered as a raw find, or crafted at least once.</summary>
        private bool GoodDiscovered(string id)
        {
            if (Compendium.IsResourceDiscovered(_loop.State, id))
            {
                return true;
            }

            foreach (var recipe in _loop.Data.recipes)
            {
                if (recipe.output == id && Compendium.IsRecipeDiscovered(_loop.State, recipe.id))
                {
                    return true;
                }
            }

            return false;
        }

        private void BuildLadderCard()
        {
            var card = Card("THE LADDER");
            Anchor(LadderAnchor, card);

            // The hatchet heads the rungs — the work sharpens as you climb.
            var tools = ArtLibrary.ForLine("tools");
            if (tools != null)
            {
                PlateImage(card, tools, 150f);
            }

            // The next few unpurchased rungs of the §9 ladder, in order. A
            // recruit rung whose familiar already walks (the kith crossed a
            // fold) has nothing left to give — it doesn't reappear.
            var next = new List<UpgradeData>();
            foreach (var upgrade in _loop.Data.upgrades.OrderBy(u => u.order))
            {
                if (_loop.IsUpgradePurchased(upgrade) || Upgrades.IsSpentRecruit(_loop.State, upgrade))
                {
                    continue;
                }

                next.Add(upgrade);
                if (next.Count >= UpgradeWindow)
                {
                    break;
                }
            }

            if (next.Count == 0)
            {
                MakeText(card, "every rung climbed.", 18, TextAnchor.MiddleCenter, Ink2);
                return;
            }

            foreach (var upgrade in next)
            {
                var captured = upgrade;
                var row = Row(card);
                var label = MakeText(row.transform, string.Empty, 19, TextAnchor.MiddleLeft, Ink);
                FlexibleWidth(label.gameObject, 1f);
                Button buy = null;
                buy = Button(row.transform, "Take up", 170, () =>
                {
                    if (_loop.PurchaseUpgrade(captured))
                    {
                        Flash(buy, "taken up", true);
                        SetNote(captured.displayName.ToLowerInvariant() + ": the work changes shape.");
                        // The rung that opens the fire ring or the bench builds a
                        // whole station card at the head of the page, so the
                        // Ladder holds its own place rather than being shoved
                        // down by what it just bought. Still true now the cards
                        // fold: a new station arrives shut, and a head is 132
                        // units the rung did not have above it a moment ago.
                        KeepInPlace(LadderAnchor, card);
                    }
                });

                // What the rung gives is fixed data — settle it once; only the
                // requirement line and affordability move.
                var gives = EffectsLabel(captured.effects);
                _liveUpdaters.Add(() =>
                {
                    label.text = captured.displayName
                                 + (gives.Length > 0 ? "\n" + SizeOpen(15) + "<color=" + MossDeepHex + ">" + gives + "</color></size>" : string.Empty)
                                 + "\n" + SizeOpen(15) + "<color=" + Ink2Hex + ">" + UpgradeRequirement(captured) + "</color></size>";
                    var ok = !_loop.IsUpgradePurchased(captured) && _loop.CanAffordUpgrade(captured)
                             && _loop.MeetsUpgradeSkillGate(captured) && string.IsNullOrEmpty(_loop.MissingToolTier(captured))
                             && _loop.FoldsUntilUpgrade(captured) == 0;
                    buy.interactable = ok;
                    SetButtonTint(buy, ok);
                });
            }
        }
    }
}
