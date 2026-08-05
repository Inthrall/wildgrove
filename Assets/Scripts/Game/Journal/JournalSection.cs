using System;
using System.Collections.Generic;
using BreakInfinity;
using UnityEngine;
using UnityEngine.UI;
using Wildgrove.Data;
using Wildgrove.Sim;

namespace Wildgrove.Game
{
    /// <summary>
    /// Base for the journal's page and sheet builders. Holds the owning
    /// <see cref="GameHud"/> and forwards the shared HUD state, coordinator
    /// calls, and data labels the builders lean on — so each builder body reads
    /// exactly as it did when it lived on GameHud, only in its own file.
    /// The self-contained widget/theme/format/sprite helpers come in via
    /// <c>using static</c> at each subclass; only the stateful bits route here.
    /// </summary>
    internal abstract class JournalSection
    {
        protected readonly GameHud _hud;

        protected JournalSection(GameHud hud)
        {
            _hud = hud;
        }

        // ─── Shared HUD state ───
        protected GameLoop _loop => _hud.Loop;
        protected bool _dirty { get => _hud.Dirty; set => _hud.Dirty = value; }
        protected RectTransform _body => _hud.Body;
        protected List<Action> _liveUpdaters => _hud.LiveUpdaters;
        protected List<Action> _frameUpdaters => _hud.FrameUpdaters;
        protected Dictionary<string, float> _flashAges => _hud.FlashAges;
        protected Dictionary<string, Text> _tendFlashes => _hud.TendFlashes;
        protected Dictionary<string, bool> _zoneOpen => _hud.ZoneOpen;
        protected RectTransform _firstVerseCard { get => _hud.FirstVerseCard; set => _hud.FirstVerseCard = value; }
        protected GameObject _sheet { get => _hud.Sheet; set => _hud.Sheet = value; }
        protected Transform _modalLayer => _hud.ModalLayer;

        // The journal fonts, installed once on JournalWidgets.
        protected Font _font => JournalWidgets.BodyFont;
        protected Font _serif => JournalWidgets.SerifFont;
        protected Font _smallCaps => JournalWidgets.SmallCapsFont;
        protected Font _hand => JournalWidgets.HandFont;

        // ─── Coordinator calls ───
        protected void SetNote(string text) => _hud.SetNote(text);
        protected void Flash(Component near, string message, bool good) => _hud.Flash(near, message, good);

        /// <summary>
        /// The muted " $x.xx" a real-money line wears once the store has
        /// priced it (empty until the catalogue fetch) — every IAP surface
        /// shows its price so the Play dialog is never where the player
        /// first learns it.
        /// </summary>
        protected string PriceTail(string productId)
        {
            var price = _loop.Store.PriceLabel(productId);
            return string.IsNullOrEmpty(price)
                ? string.Empty
                : "  " + JournalWidgets.SizeOpen(15) + "<color=" + JournalTheme.Ink2Hex + ">" + price + "</color></size>";
        }

        /// <summary>
        /// The kith's ladder as marks (design §4) — one for every place it can
        /// ever open, so the places still to be earned or bought are visible as
        /// faint marks rather than as nothing at all. Shared by the Warden page
        /// and the attunement sheet: the same marks, painted by the same rule,
        /// so the celebration and the page can never disagree about the ladder.
        /// </summary>
        protected Image[] BuildKithPlaces(Transform parent, float size)
        {
            return JournalWidgets.MarkRow(parent, Kith.SlotsMax(_loop.Data), size);
        }

        /// <summary>
        /// Ink for a place someone stands in, moss for one standing open and
        /// waiting for a body, the faint rule for one not yet on the ladder.
        /// Moss because an open place is an invitation — ochre is the ink of
        /// costs and halted work.
        /// </summary>
        protected void PaintKithPlaces(Image[] places)
        {
            var walking = _loop.KithWalking();
            var open = _loop.KithSlots();
            for (var index = 0; index < places.Length; index++)
            {
                places[index].color = index < walking
                    ? JournalTheme.Ink
                    : index < open
                        ? JournalTheme.MossDeep
                        : JournalTheme.RulePaper;
            }
        }

        /// <summary>
        /// The plate for what a post yields — the resource of the node it is.
        /// Null for camp (no station at all), the trail, the wander post and
        /// dig stations: none of those stand over a crop, so there is no
        /// picture to show and the caller falls back to naming them.
        /// <para>
        /// Shared rather than the sheets' own: the Warden page's roster tiles
        /// wear the ground each companion works, and a second lookup would be
        /// the one place the page and the picker could disagree about what a
        /// post looks like.
        /// </para>
        /// </summary>
        protected Sprite StationPlate(string stationId)
        {
            var node = FindNode(stationId);
            return node == null ? null : ArtLibrary.ForResource(node.resourceId);
        }

        protected NodeState FindNode(string stationId)
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

        // ─── Data labels (read live game state) ───
        protected List<ZoneData> ZonesInOrder() => _hud.Labels.ZonesInOrder();
        protected ZoneData LatestZone() => _hud.Labels.LatestZone();
        protected string ZoneName(string zoneId) => _hud.Labels.ZoneName(zoneId);
        protected UpgradeData SkillSource(string skill) => _hud.Labels.SkillSource(skill);
        protected string EffectsLabel(List<EffectData> effects) => _hud.Labels.EffectsLabel(effects);
        protected string StationLabel(string stationId) => _hud.Labels.StationLabel(stationId);
        protected string SpeciesName(string speciesId) => _hud.Labels.SpeciesName(speciesId);
        protected string UpgradeRequirement(UpgradeData upgrade) => _hud.Labels.UpgradeRequirement(upgrade);
        protected string BundleHaveLabel(List<ItemAmount> materials) => _hud.Labels.BundleHaveLabel(materials);
        protected string BundleHaveLabel(List<Buildings.MaterialCost> bundle) => _hud.Labels.BundleHaveLabel(bundle);
        protected string PlanterDisplayName(PlanterData planter, string targetId) => _hud.Labels.PlanterDisplayName(planter, targetId);
        protected string PlanterGives(PlanterData planter) => _hud.Labels.PlanterGives(planter);
        protected string NodeSkill(string targetId) => _hud.Labels.NodeSkill(targetId);
    }
}
