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
        protected NodeState _selected => _hud.Selected;
        protected bool _dirty { get => _hud.Dirty; set => _hud.Dirty = value; }
        protected RectTransform _body => _hud.Body;
        protected List<Action> _liveUpdaters => _hud.LiveUpdaters;
        protected List<Action> _frameUpdaters => _hud.FrameUpdaters;
        protected Dictionary<string, float> _flashAges => _hud.FlashAges;
        protected Dictionary<string, Text> _tendFlashes => _hud.TendFlashes;
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

        // ─── Data labels (read live game state) ───
        protected List<ZoneData> ZonesInOrder() => _hud.Labels.ZonesInOrder();
        protected ZoneData LatestZone() => _hud.Labels.LatestZone();
        protected string ZoneName(string zoneId) => _hud.Labels.ZoneName(zoneId);
        protected UpgradeData SkillSource(string skill) => _hud.Labels.SkillSource(skill);
        protected List<string> TradeableResources() => _hud.Labels.TradeableResources();
        protected string EffectsLabel(List<EffectData> effects) => _hud.Labels.EffectsLabel(effects);
        protected string StationLabel(string stationId) => _hud.Labels.StationLabel(stationId);
        protected string SpeciesName(string speciesId) => _hud.Labels.SpeciesName(speciesId);
        protected string UpgradeRequirement(UpgradeData upgrade) => _hud.Labels.UpgradeRequirement(upgrade);
        protected string BundleHaveLabel(List<ItemAmount> materials) => _hud.Labels.BundleHaveLabel(materials);
        protected string BundleHaveLabel(List<Buildings.MaterialCost> bundle) => _hud.Labels.BundleHaveLabel(bundle);
        protected List<string> PostedNames(NodeState node) => _hud.Labels.PostedNames(node);
        protected BigDouble NodeBasketCapacity(NodeState node) => _hud.Labels.NodeBasketCapacity(node);
        protected string PlanterDisplayName(PlanterData planter, string targetId) => _hud.Labels.PlanterDisplayName(planter, targetId);
        protected string NodeSkill(string targetId) => _hud.Labels.NodeSkill(targetId);
    }
}
