using UnityEngine;
using UnityEngine.UI;
using Wildgrove.Sim;
using static Wildgrove.Game.JournalTheme;
using static Wildgrove.Game.JournalWidgets;

namespace Wildgrove.Game
{
    /// <summary>
    /// The strip's pickers — a posting asked as its two halves, in order:
    /// <em>which ground?</em> then <em>who walks there?</em> Both are drawers of
    /// plates rather than lists of sentences, because both questions are about
    /// things that HAVE pictures: the grove's crops and the creatures that work
    /// them. The (+) closing the strip runs the pair; the warden's own (+) runs
    /// the first half alone, since tapping their plate has already answered the
    /// second.
    /// <para>
    /// These are the strip's way in. The prose-row sheets stay the journal's:
    /// <see cref="OpenPostingSheet"/> when a post is tapped (it must also stand
    /// its holder down, and name why an empty post can't be filled), and
    /// <see cref="OpenStationPickSheet"/> from a roster row. The rules are the
    /// same in all of them — one body per post, and a resting companion needs a
    /// free slot to take an empty one.
    /// </para>
    /// </summary>
    internal sealed partial class JournalSheets
    {
        // Four to a row on the sheet's 740 of card: a plate that size still
        // reads as a specimen, and the whole grove fits without a scroll until
        // the fourth zone.
        private const float TileCell = 170f;

        // Deep enough for two lines of the caption's own 13: these captions are
        // names ("copper-scree", a familiar's), not the Stores drawer's counts,
        // and a name clipped in half names nothing.
        private const float TileCaption = 56f;
        private const float TileInset = 6f;

        // The holder's own plate, pinned in the tile's corner — small enough
        // that it reads as a mark ON the ground rather than a second choice.
        private const float HolderGlyph = 46f;

        /// <summary>
        /// Step one of the strip's (+): which ground? Every node in the run,
        /// plus the wander post, each wearing whoever stands there now. Picking
        /// one asks who walks it (<see cref="OpenBodyPickSheet"/>).
        /// </summary>
        internal void OpenGroundPickSheet()
        {
            var sheet = BeginSheet();
            MakeText(sheet, "Which ground?", 32, TextAnchor.UpperCenter, Ink, _serif);
            MakeText(sheet, "posts walked " + _loop.KithWalking() + " of " + _loop.KithSlots(),
                14, TextAnchor.UpperCenter, Ink2, _smallCaps);

            BuildGroundGrid(sheet, OpenBodyPickSheet);
        }

        /// <summary>
        /// The warden's plate at camp asks only where: the body is already
        /// chosen, so a pick walks them there instead of opening step two.
        /// </summary>
        internal void OpenWardenWalkSheet()
        {
            var sheet = BeginSheet();
            MakeText(sheet, "Where shall the warden walk?", 30, TextAnchor.UpperCenter, Ink, _serif);
            MakeText(sheet, WardenWhereabouts().ToUpperInvariant(), 16, TextAnchor.UpperCenter, Ink2, _smallCaps);

            BuildGroundGrid(sheet, WalkWardenTo);
        }

        /// <summary>
        /// Step two: who walks the ground just chosen? The warden leads — they
        /// are always free to stand somewhere — then the kith, with anyone who
        /// would need a slot they haven't got greyed out (the reason is on the
        /// notice above, said once).
        /// </summary>
        internal void OpenBodyPickSheet(string stationId)
        {
            var sheet = BeginSheet();
            var state = _loop.State;
            var occupantHere = Stationing.OccupantOf(state, stationId);
            var isWanderPost = stationId == Familiar.WanderStation;
            var node = FindNode(stationId);
            var wardenHere = node != null ? Warden.PostNodeId(state) == node.id : isWanderPost && Warden.IsWandering(state);
            var hasRoom = Kith.HasRoom(state, _loop.Data);

            MakeText(sheet, "Who walks here?", 32, TextAnchor.UpperCenter, Ink, _serif);

            // The ground being filled, drawn the way the posting sheet heads
            // itself: its crop, and whoever stands there now.
            var holder = occupantHere != null
                ? occupantHere.name + " walks here"
                : wardenHere ? "the warden walks here" : "no one walks here";
            PictureRow(sheet, StationPlate(stationId),
                StationLabel(stationId).ToUpperInvariant()
                + "\n" + SizeOpen(16) + "<color=" + (occupantHere != null || wardenHere ? InkHex : Ink2Hex) + ">"
                + holder + "</color></size>",
                occupantHere != null ? ArtLibrary.ForSpecies(occupantHere.speciesId) : null, 120f, 740f);

            if (occupantHere == null)
            {
                var notice = EmptyPostNotice(node != null || isWanderPost);
                if (notice != null)
                {
                    var line = MakeText(sheet, "<i>" + notice + "</i>", 16, TextAnchor.UpperCenter, Ink2);
                    var element = line.gameObject.AddComponent<LayoutElement>();
                    element.minWidth = 740;
                    element.preferredWidth = 740;
                }
            }

            var grid = SheetGrid(sheet);

            // The warden takes a node or the wander post, never the trail: they
            // tend, the kith carries. Nothing to offer when they already stand
            // here — the post's own sheet is where a holder stands down.
            if (!wardenHere && (node != null || isWanderPost))
            {
                PlateTile(grid, ArtLibrary.ForWarden(), "the warden", null, () => WalkWardenTo(stationId));
            }

            foreach (var familiar in state.roster)
            {
                // The pony walks her own lane and cannot be posted (§11), so a
                // tile for her would only be a plate that refuses.
                if (familiar.IsPony || PostMatches(familiar.stationId, stationId))
                {
                    continue;
                }

                var captured = familiar;
                // Taking an empty post from rest needs a free slot; stepping in
                // for a holder always works — the vacated slot covers it.
                var blocked = captured.IsResting && occupantHere == null && !hasRoom;
                var tile = PlateTile(grid, ArtLibrary.ForSpecies(captured.speciesId), captured.name,
                    captured.IsResting ? null : StationPlate(captured.stationId), () =>
                    {
                        Station(captured, stationId);
                        CloseSheet();
                    });
                if (blocked)
                {
                    tile.interactable = false;
                    SetButtonTint(tile, false);
                    DimTilePlate(tile);
                }
            }

            // The way back to the first question, for a ground picked by
            // mistake — the cross in the corner leaves the pair entirely.
            Button(sheet, "Choose another ground", 380, OpenGroundPickSheet);
        }

        /// <summary>Every ground a body can be sent to, as a drawer of plates: the run's nodes, then the wander post.</summary>
        private void BuildGroundGrid(Transform sheet, System.Action<string> onPick)
        {
            var grid = SheetGrid(sheet);
            foreach (var node in _loop.State.nodes)
            {
                var captured = node.id;
                GroundTile(grid, captured, ArtLibrary.ForResource(node.resourceId), () => onPick(captured));
            }

            // The wander post stands over no crop, so it borrows the watch's own
            // mark — the same one it wore when it had a plate on the strip.
            GroundTile(grid, Familiar.WanderStation, ArtLibrary.ForSkill("observation"),
                () => onPick(Familiar.WanderStation));
        }

        /// <summary>One ground's tile — its crop, its name, and the mark of whoever stands there now.</summary>
        private void GroundTile(RectTransform grid, string stationId, Sprite plate, UnityEngine.Events.UnityAction onPick)
        {
            var occupant = Stationing.OccupantOf(_loop.State, stationId);
            Sprite held;
            if (Warden.PostNodeId(_loop.State) == stationId)
            {
                held = ArtLibrary.ForWarden();
            }
            else
            {
                held = occupant != null ? ArtLibrary.ForSpecies(occupant.speciesId) : null;
            }

            PlateTile(grid, plate, StationLabel(stationId), held, onPick);
        }

        /// <summary>Send the warden to the ground picked — the wander post included — and say so in the margin.</summary>
        private void WalkWardenTo(string stationId)
        {
            if (stationId == Familiar.WanderStation)
            {
                _loop.WanderWarden();
                SetNote("the warden sets off to wander the run.");
                CloseSheet();
                return;
            }

            var node = FindNode(stationId);
            if (node == null)
            {
                return;
            }

            _loop.PostWarden(node);
            SetNote("the warden walks to " + StationLabel(stationId) + ".");
            CloseSheet();
        }

        /// <summary>
        /// Why an EMPTY post may be unfillable, said once above the choices
        /// rather than as a tail on every line it applies to: you either rob
        /// another post or grow the kith. Null when there is nothing to warn
        /// about. Shared by the posting sheet and the body picker so the two
        /// never disagree about the ladder's rules.
        /// </summary>
        private string EmptyPostNotice(bool wardenCanStand)
        {
            var state = _loop.State;
            var anyResting = false;
            foreach (var familiar in state.roster)
            {
                if (familiar.IsResting)
                {
                    anyResting = true;
                    break;
                }
            }

            if (state.roster.Count == 0)
            {
                // A brand-new warden's first tap can land here — the sheet must
                // answer "how do I ever fill this?" or it's a riddle. One
                // instruction, in the order it is done: leave the pile, someone
                // comes.
                return "no one walks with you yet. leave a pile of a plate's own goods on the Trail page and whoever is drawn to it comes to stay."
                       + (wardenCanStand ? " until then the warden can stand here alone." : string.Empty);
            }

            if (!anyResting)
            {
                return "everyone is already posted. move one here and the post they leave falls idle, or open a slot on the Ladder to walk with one more.";
            }

            if (!Kith.HasRoom(state, _loop.Data))
            {
                return "someone waits at camp, but every slot is walked. open a slot on the Ladder to put them to work, or move a walker here from a post you need less.";
            }

            return null;
        }

        /// <summary>
        /// Wash out a blocked tile's plate as well as its paper.
        /// <see cref="SetButtonTint"/> reaches the panel and the caption, but a
        /// full-colour portrait on dead paper still reads as the liveliest thing
        /// in the drawer — which is the one thing a refused choice must not be.
        /// </summary>
        private static void DimTilePlate(Button tile)
        {
            var plate = tile.transform.Find("Plate");
            if (plate == null)
            {
                return;
            }

            var image = plate.GetComponent<Image>();
            if (image != null)
            {
                image.color = new Color(1f, 1f, 1f, 0.35f);
            }
        }

        /// <summary>The square drawer a picker lays its plates on, sized to the sheet's own card width.</summary>
        private static RectTransform SheetGrid(Transform sheet)
        {
            var go = MakeRect("Grid", sheet).gameObject;
            var grid = go.AddComponent<SquareCellGrid>();
            grid.idealCell = TileCell;
            grid.minColumns = 3;
            grid.maxColumns = 4;
            grid.spacing = new Vector2(10f, 10f);
            grid.childAlignment = TextAnchor.UpperCenter;
            // The card's width, not the tiles' — a grid that hugged its plates
            // would stand a short row somewhere other than under the heading.
            var element = go.AddComponent<LayoutElement>();
            element.minWidth = 740;
            element.preferredWidth = 740;
            return (RectTransform)go.transform;
        }

        /// <summary>
        /// One square of a picker: the plate filling it, its name on a strip
        /// along the bottom inside edge, and an optional corner mark for
        /// whoever/whatever is already there. The caption is INSIDE the square
        /// on purpose — a label hung under the tile would make the cell oblong
        /// and the drawer ragged (the Stores drawer's rule).
        /// </summary>
        private static Button PlateTile(RectTransform grid, Sprite plate, string caption, Sprite corner,
            UnityEngine.Events.UnityAction onPick)
        {
            var go = MakePanel("Tile", grid, DeepPaper);
            var tile = (RectTransform)go.transform;
            AddBorder(go, Ink2);

            if (plate != null)
            {
                var art = new GameObject("Plate", typeof(Image));
                art.transform.SetParent(tile, false);
                var image = art.GetComponent<Image>();
                image.sprite = plate;
                image.preserveAspect = true;
                image.raycastTarget = false;
                var rect = (RectTransform)art.transform;
                Stretch(rect);
                // Clear of the border and of the caption strip below it — a
                // plate read through either is a plate read twice.
                rect.offsetMin = new Vector2(TileInset, TileCaption);
                rect.offsetMax = new Vector2(-TileInset, -TileInset);
            }

            var strip = MakePanel("Caption", tile, CardPaper);
            var stripRect = (RectTransform)strip.transform;
            stripRect.anchorMin = Vector2.zero;
            stripRect.anchorMax = new Vector2(1f, 0f);
            stripRect.offsetMin = new Vector2(2f, 2f);
            stripRect.offsetMax = new Vector2(-2f, TileCaption - 2f);
            strip.GetComponent<Image>().raycastTarget = false;

            var label = MakeText(strip.transform, caption, 13, TextAnchor.MiddleCenter, Ink, SmallCapsFont);
            Stretch((RectTransform)label.transform);
            label.raycastTarget = false;

            if (corner != null)
            {
                var mark = IconImage(tile, corner, HolderGlyph, Color.white);
                var rect = (RectTransform)mark.transform;
                rect.anchorMin = Vector2.one;
                rect.anchorMax = Vector2.one;
                rect.pivot = Vector2.one;
                rect.anchoredPosition = new Vector2(-TileInset, -TileInset);
                rect.sizeDelta = new Vector2(HolderGlyph, HolderGlyph);
            }

            var button = go.AddComponent<Button>();
            // Without a targetGraphic the ColorTint transition has nothing to
            // tint, and the tile acknowledges no press at all.
            button.targetGraphic = go.GetComponent<Image>();
            var colours = button.colors;
            colours.highlightedColor = new Color(0.97f, 0.96f, 0.93f, 1f);
            colours.pressedColor = new Color(0.8f, 0.76f, 0.68f, 1f);
            // Disabled stays SetButtonTint's job — white here so the two
            // channels don't multiply into a blank plate.
            colours.disabledColor = Color.white;
            button.colors = colours;
            button.onClick.AddListener(onPick);
            return button;
        }
    }
}
