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
        protected Dictionary<string, bool> _cardOpen => _hud.CardOpen;
        protected RectTransform _firstVerseCard { get => _hud.FirstVerseCard; set => _hud.FirstVerseCard = value; }
        protected RectTransform _firstKeepingCard { get => _hud.FirstKeepingCard; set => _hud.FirstKeepingCard = value; }
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

        // ─── Keeping the reader's place ───

        /// <summary>
        /// Name a rect as this page draws it, so a rebuild anchored on that id
        /// can put the page back where it stood. Offered on every build; kept
        /// only by the one rebuild that asked for it.
        /// </summary>
        protected void Anchor(string anchorId, RectTransform target) => _hud.MarkAnchor(anchorId, target);

        /// <summary>
        /// Rebuild the page, keeping <paramref name="target"/> where it stands —
        /// what a press uses in place of <c>_dirty = true</c> when its answer
        /// can add height ABOVE the card that was pressed. See
        /// <see cref="GameHud.KeepInPlace"/>.
        /// </summary>
        protected void KeepInPlace(string anchorId, RectTransform target) => _hud.KeepInPlace(anchorId, target);

        // ─── The fold ───

        /// <summary>
        /// A card whose head is the fold that opens it. Shut, the head carries
        /// <paramref name="tally"/> — the card's own count — so the folded page
        /// reads as a table of contents with progress on it rather than as a row
        /// of closed drawers (the Trail's rule for a folded ground).
        /// <para>
        /// The head is the journal's button plate rather than the card's usual
        /// text so that it is a real control: focus reaches it, the pad presses
        /// it, and it answers a touch the way every other plate does.
        /// </para>
        /// <para>
        /// Shared rather than the Record page's own since the keeping learnt to
        /// fold (2026-08-13). It lives here for the same reason
        /// <see cref="StationPlate"/> does: two pages drawing the same control
        /// two ways is one bug and two wordings.
        /// </para>
        /// </summary>
        protected RectTransform FoldingCard(string cardId, string head, string tally, out bool open)
        {
            return FoldingCard(cardId, head, tally, out open, out _);
        }

        /// <summary>
        /// <see cref="FoldingCard(string,string,string,out bool)"/>, handing
        /// back the head itself — for a card whose tally is a clock rather than
        /// a count, and so has to be rewritten on the cadence rather than
        /// written once at the build.
        /// </summary>
        /// <param name="tallyWhenOpen">
        /// Keep the tally under the head once the card is open, rather than
        /// letting the contents speak for themselves. For a card whose tally is
        /// the answer and whose contents are the working — the keeping, where
        /// "3 of 5 set down" is what the player came to read and five slot rows
        /// are how it got there.
        /// </param>
        protected RectTransform FoldingCard(string cardId, string head, string tally, out bool open, out Button heading,
            bool tallyWhenOpen = false)
        {
            return FoldingCard(cardId, head, tally, JournalCardFolds.OpenUnasked(cardId), out open, out heading,
                tallyWhenOpen);
        }

        /// <summary>
        /// <see cref="FoldingCard(string,string,string,out bool,out Button,bool)"/> for
        /// a card whose unasked default is positional rather than a matter of
        /// what the card is for — the Camp page's stations. The same answer goes
        /// to the head's own press, or the fold would be computed one way and
        /// toggled the other.
        /// </summary>
        protected RectTransform FoldingCard(string cardId, string head, string tally, bool openUnasked,
            out bool open, out Button heading, bool tallyWhenOpen = false)
        {
            open = JournalCardFolds.IsOpen(_cardOpen, cardId, openUnasked);
            var card = JournalWidgets.Card(null);
            card.gameObject.name = "Card_" + head;

            var label = FoldingCardLabel(head, open && !tallyWhenOpen ? null : tally);

            // The heading hands its own rect to the fold, which notes where it
            // stands in the viewport — the rebuilt page puts it back there.
            Button pressed = null;
            pressed = JournalWidgets.Button(card, label, 400,
                () => _hud.FoldCard(cardId, (RectTransform)pressed.transform, openUnasked));
            pressed.gameObject.name = "CardHeading";
            JournalWidgets.AddFoldArrow(pressed, open);
            Anchor(cardId, (RectTransform)pressed.transform);

            heading = pressed;
            return card;
        }

        /// <summary>
        /// The head's own label: the card's name, and under it the tally, where
        /// there is one to wear. Separate from the build so a live tally can be
        /// rewritten by the same rule that wrote it — and it knows nothing about
        /// folding, which is the caller's business: an empty tally is a head
        /// with a name and nothing else.
        /// </summary>
        protected static string FoldingCardLabel(string head, string tally)
        {
            return string.IsNullOrEmpty(tally)
                ? JournalWidgets.SizeOpen(15) + head + "</size>"
                : JournalWidgets.SizeOpen(15) + head + "</size>" + JournalWidgets.SizeOpen(13)
                  + "\n<color=" + JournalTheme.Ink2Hex + ">" + tally + "</color></size>";
        }

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
        /// The places at the warden's side as marks (design §4) — one for every
        /// place the kith can ever open, so the places still to be earned or
        /// bought are visible as faint marks rather than as nothing at all.
        /// Shared by the Warden page and the attunement sheet: the same marks,
        /// painted by the same rule, so the celebration and the page can never
        /// disagree about how many there are.
        /// </summary>
        protected Image[] BuildKithPlaces(Transform parent, float size)
        {
            return JournalWidgets.MarkRow(parent, Kith.SlotsMax(_loop.Data), size);
        }

        /// <summary>
        /// Ink for a place someone stands in, moss for one standing open and
        /// waiting for a body, the faint rule for one not yet earned.
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
        /// The plate for what a post yields — the resource of the node it is, or
        /// the sketching plate at an observation site (it stands over no crop,
        /// but it is a place, and every picker draws a post as a picture).
        /// Null for camp (no station at all) and the trail: neither is a place
        /// with a face, so the caller falls back to naming them.
        /// <para>
        /// Shared rather than the sheets' own: the Warden page's roster tiles
        /// wear the ground each companion works, and a second lookup would be
        /// the one place the page and the picker could disagree about what a
        /// post looks like.
        /// </para>
        /// <para>
        /// A sketching post wore the observation CRAFT glyph until 2026-08-13,
        /// which made it the one tile in a drawer of naturalist plates carrying a
        /// flat two-tone mark — so the eye read it as a different kind of thing
        /// rather than another place a body can stand. It has a plate of its own
        /// now (<see cref="ArtLibrary.ForSketching"/>).
        /// </para>
        /// </summary>
        protected Sprite StationPlate(string stationId)
        {
            if (Familiar.IsSketchStation(stationId))
            {
                return ArtLibrary.ForSketching();
            }

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
        protected string PostBonus(Familiar familiar, NodeState node, bool isSketchPost) => _hud.Labels.PostBonus(familiar, node, isSketchPost);
        protected string NodeSkill(string targetId) => _hud.Labels.NodeSkill(targetId);
    }
}
