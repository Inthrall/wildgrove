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
    /// them. The (+) closing the strip runs the pair; the warden's own empty
    /// ground runs the first half alone, since tapping their place has already
    /// answered the second.
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

        // The tile itself is JournalWidgets.PlateTile — the Warden page's roster
        // is the same drawer of plates asked from a page rather than the strip,
        // so the square, its caption strip and its corner mark are shared.

        /// <summary>
        /// Step one of the strip's (+): which ground? Every node in the run,
        /// each wearing whoever stands there now. Picking one asks who walks it
        /// (<see cref="OpenBodyPickSheet"/>). The watch posts are not among them:
        /// they stand over no crop, so they have no place in a drawer of
        /// gathering grounds — each site's own watch card and a companion's
        /// station sheet are where a watcher is sent.
        /// </summary>
        internal void OpenGroundPickSheet()
        {
            var sheet = BeginSheet();
            MakeText(sheet, "Which ground?", 32, TextAnchor.UpperCenter, Ink, _serif);
            MakeText(sheet, "posts walked " + _loop.KithWalking() + " of " + _loop.KithSlots(),
                14, TextAnchor.UpperCenter, Ink2, _smallCaps);

            BuildGroundGrid(sheet, OpenBodyPickSheet, includeWatchPosts: false);
        }

        /// <summary>
        /// The warden's empty ground asks only where: the body is already chosen,
        /// so a pick walks them there instead of opening step two. Reached from
        /// the head of the strip while they hold no node — the same question the
        /// posting sheet's warden row and the body picker's warden tile ask from
        /// the other side, and all three end at <see cref="WalkWardenTo"/>.
        /// </summary>
        internal void OpenWardenWalkSheet()
        {
            var sheet = BeginSheet();

            // The warden is named in the question, so this is where the name can
            // be changed — the same quill-beside-the-heading pair a familiar's
            // station sheet uses, for the same reason.
            var heading = Row((RectTransform)sheet);
            var headingLayout = heading.GetComponent<HorizontalLayoutGroup>();
            headingLayout.childAlignment = TextAnchor.MiddleCenter;
            headingLayout.spacing = 2;
            MakeText(heading.transform, "Where shall " + _loop.WardenName() + " walk?",
                30, TextAnchor.MiddleCenter, Ink, _serif);
            IconButton(heading.transform, JournalSprites.QuillSprite(), 40f, 120f, () =>
            {
                CloseSheet();
                // Back to this sheet afterwards, so the new name is in the
                // question and the ground being chosen is not lost to an aside.
                OpenWardenNamingSheet(OpenWardenWalkSheet);
            });

            MakeText(sheet, WardenWhereabouts().ToUpperInvariant(), 16, TextAnchor.UpperCenter, Ink2, _smallCaps);

            // The warden keeps the watch posts among their grounds — watching is
            // work, not gathering, and a site is watched by whoever stands at
            // it. ("Tending" is the windfall catch and nothing else — a watcher
            // never does it.)
            BuildGroundGrid(sheet, WalkWardenTo, includeWatchPosts: true);
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
            var isWatchPost = Familiar.IsWatchStation(stationId);
            var node = FindNode(stationId);
            var wardenHere = node != null
                ? Warden.PostNodeId(state) == node.id
                : isWatchPost && Warden.PostNodeId(state) == stationId;
            var hasRoom = Kith.HasRoom(state, _loop.Data);

            MakeText(sheet, "Who walks here?", 32, TextAnchor.UpperCenter, Ink, _serif);

            // The ground being filled, drawn the way the posting sheet heads
            // itself: its crop, and whoever stands there now.
            var holder = occupantHere != null
                ? occupantHere.name + " walks here"
                : wardenHere ? _loop.WardenName() + " walks here" : "no one walks here";
            PictureRow(sheet, StationPlate(stationId),
                StationLabel(stationId).ToUpperInvariant()
                + "\n" + SizeOpen(16) + "<color=" + (occupantHere != null || wardenHere ? InkHex : Ink2Hex) + ">"
                + holder + "</color></size>",
                occupantHere != null
                    ? ArtLibrary.ForSpecies(occupantHere.speciesId)
                    : wardenHere ? ArtLibrary.ForWarden() : null, 120f, 740f);

            if (occupantHere == null)
            {
                var notice = EmptyPostNotice(node != null || isWatchPost);
                if (notice != null)
                {
                    var line = MakeText(sheet, "<i>" + notice + "</i>", 16, TextAnchor.UpperCenter, Ink2);
                    var element = line.gameObject.AddComponent<LayoutElement>();
                    element.minWidth = 740;
                    element.preferredWidth = 740;
                }
            }

            var grid = SheetGrid(sheet);

            // The warden takes a node or a watch post, never the trail: they
            // tend, the kith carries. Nothing to offer when they already stand
            // here — the post's own sheet is where a holder stands down.
            if (!wardenHere && (node != null || isWatchPost))
            {
                // Wearing the ground they stand on now, exactly as the companion
                // tiles below do — the warden is a body like any other here.
                PlateTile(grid, ArtLibrary.ForWarden(), _loop.WardenName(),
                    StationPlate(Warden.PostNodeId(state)), () => WalkWardenTo(stationId));
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
                // What the ground gains from this body, under its name. The
                // caption strip is 56 deep against a 13pt name, so a second
                // line at 12 costs no geometry — and the tiles are a drawer of
                // near-identical portraits, which is exactly where a number is
                // the only thing that tells them apart.
                var bonus = PostBonus(captured, node, isWatchPost);
                var caption = bonus.Length == 0
                    ? captured.name
                    : captured.name + "\n" + SizeOpen(12) + "<color=" + MossDeepHex + ">" + bonus + "</color></size>";

                var tile = PlateTile(grid, ArtLibrary.ForSpecies(captured.speciesId), caption,
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

        /// <summary>Every ground a body can be sent to, as a drawer of plates: the run's nodes, then — when asked for — one watch post per open site.</summary>
        private void BuildGroundGrid(Transform sheet, System.Action<string> onPick, bool includeWatchPosts)
        {
            var grid = SheetGrid(sheet);
            foreach (var node in _loop.State.nodes)
            {
                var captured = node.id;
                GroundTile(grid, captured, ArtLibrary.ForResource(node.resourceId), () => onPick(captured));
            }

            if (!includeWatchPosts)
            {
                return;
            }

            // A watch post stands over no crop, so every one of them borrows the
            // watch's own mark; the tile's caption is what says which site it is.
            foreach (var site in _loop.State.digSites)
            {
                var captured = Familiar.WatchStation(site.zoneId);
                GroundTile(grid, captured, ArtLibrary.ForSkill("observation"), () => onPick(captured));
            }
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

        /// <summary>Send the warden to the ground picked — a site's watch included — and say so in the margin.</summary>
        private void WalkWardenTo(string stationId)
        {
            var watchZone = Familiar.WatchZoneOf(stationId);
            if (watchZone != null)
            {
                _loop.WatchWarden(watchZone);
                SetNote(_loop.WardenName() + " settles in to watch " + ZoneName(watchZone) + ".");
                CloseSheet();
                return;
            }

            var node = FindNode(stationId);
            if (node == null)
            {
                return;
            }

            _loop.PostWarden(node);
            SetNote(_loop.WardenName() + " walks to " + StationLabel(stationId) + ".");
            CloseSheet();
        }

        /// <summary>
        /// Why an EMPTY post may be unfillable, said once above the choices
        /// rather than as a tail on every line it applies to: you either rob
        /// another post or grow the kith. Null when there is nothing to warn
        /// about. Shared by the posting sheet and the body picker so the two
        /// never disagree about the rules for a place at the warden's side.
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
                       + (wardenCanStand ? " until then " + _loop.WardenName() + " can stand here alone." : string.Empty);
            }

            if (!anyResting)
            {
                return "everyone is already posted. move one here and the post they leave falls idle, or make room beside you: a verse sung opens another place at your side, and the Warden page keeps the count.";
            }

            if (!Kith.HasRoom(state, _loop.Data))
            {
                return "someone waits at camp, but every place at your side is spoken for. a verse sung opens another, or move a walker here from a post you need less. the Warden page keeps the count.";
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
    }
}
