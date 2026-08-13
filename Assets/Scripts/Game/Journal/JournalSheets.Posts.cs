using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Wildgrove.Sim;
using static Wildgrove.Game.JournalTheme;
using static Wildgrove.Game.JournalWidgets;

namespace Wildgrove.Game
{
    /// <summary>
    /// A post asked from the post's side: <em>who walks here?</em> Opened by
    /// tapping a post, so it must also stand its holder down, and say why an
    /// empty post cannot be filled when the kith is fully committed.
    /// <para>
    /// Its mirror is <see cref="OpenStationPickSheet"/>, which asks the same
    /// question from the companion's side, and the plate drawers of
    /// <c>JournalSheets.Posting</c> ask it from the world strip. The rules are
    /// the same in all three: one body per post, and a resting companion needs a
    /// free slot to take an empty one, while stepping in for someone always
    /// works because the vacated slot covers it.
    /// </para>
    /// </summary>
    internal sealed partial class JournalSheets
    {
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

            // The warden stands at a node, or takes a site's watch (design §2) —
            // but never the trail: the warden tends, the kith carries.
            var node = FindNode(stationId);
            var watchZone = Familiar.WatchZoneOf(stationId);
            var wardenCanStand = node != null || watchZone != null;
            var wardenHere = wardenCanStand
                && (node != null ? Warden.PostNodeId(state) == node.id : Warden.PostNodeId(state) == stationId);

            // The post itself, drawn the way the rows below it are drawn: its
            // crop on the left, whoever holds it on the right, the words
            // between. The heading is then the same shape as the choice it
            // introduces — "instead of them, who?" — instead of a portrait and
            // three separate lines of type saying the same thing.
            var holder = occupantHere != null
                ? occupantHere.name + " walks here"
                : wardenHere ? _loop.WardenName() + " walks here" : "no one walks here";
            // The warden wears their own plate everywhere else a holder is
            // drawn — the strip's badge, both pickers, the roster's corner mark
            // — so the heading showed "the warden walks here" beside an empty
            // space: the one place a post's holder was named and not pictured.
            var holderPlate = occupantHere != null
                ? ArtLibrary.ForSpecies(occupantHere.speciesId)
                : wardenHere ? ArtLibrary.ForWarden() : null;
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
                Button(sheet, "Send " + _loop.WardenName() + " back to camp", 740, () =>
                {
                    _loop.RestWarden();
                    SetNote(_loop.WardenName() + " steps back to camp.");
                    CloseSheet();
                });
            }

            if (wardenCanStand && !wardenHere)
            {
                // Moss verbs — these are the actions the sheet exists for;
                // ochre made them read as warnings.
                var wardenVerb = watchZone != null
                    ? "Set " + _loop.WardenName() + " watching here"
                    : "Walk " + _loop.WardenName() + " here";
                // Drawn like the companion rows below it, because it is the same
                // offer: the body's own plate leads, the ground they stand on
                // now trails. It was the one candidate row with no pictures at
                // all, which read as a different KIND of choice rather than the
                // same choice about a different body. The whereabouts stay in
                // words only where no plate can stand in for them — camp and the
                // trail (the companion rows' rule).
                var wardenGround = StationPlate(Warden.PostNodeId(state));
                var whereTail = wardenGround != null
                    ? string.Empty
                    : "  " + SizeOpen(15) + "<color=" + Ink2Hex + ">" + WardenWhereabouts() + "</color></size>";
                PictureButton(sheet, ArtLibrary.ForWarden(),
                    "<color=" + MossDeepHex + ">" + wardenVerb + "</color>" + whereTail,
                    wardenGround, 740, 120f, () =>
                {
                    if (watchZone != null)
                    {
                        _loop.WatchWarden(watchZone);
                        SetNote(_loop.WardenName() + " settles in to watch " + ZoneName(watchZone) + ".");
                    }
                    else
                    {
                        _loop.PostWarden(node);
                        SetNote(_loop.WardenName() + " walks to " + StationLabel(node.id) + ".");
                    }

                    CloseSheet();
                });
            }

            // The friction the old sheet hid: to fill an EMPTY post with the
            // kith fully committed, you either rob another post or grow the
            // kith. Name it up front so the flat roster list isn't a puzzle.
            // (EmptyPostNotice words it — the body picker asks the same thing.)
            if (occupantHere == null)
            {
                var notice = EmptyPostNotice(wardenCanStand);
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
                // the watch posts all open this sheet.
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
                // trail has no plate, and unmapped art falls back to its word
                // rather than vanishing).
                var portrait = ArtLibrary.ForSpecies(captured.speciesId);
                var working = resting ? null : StationPlate(captured.stationId);
                var speciesTail = portrait != null
                    ? string.Empty
                    : "  " + SizeOpen(15) + "<color=" + Ink2Hex + ">" + SpeciesName(captured.speciesId) + "</color></size>";
                var whereTail = resting || working != null
                    ? string.Empty
                    : "  " + SizeOpen(15) + "<color=" + Ink2Hex + ">" + StationLabel(captured.stationId) + "</color></size>";

                // What this body is worth HERE, which the row could not say at
                // all before: a species trait is a line of prose on an arrival
                // sheet and nowhere near the moment it decides a posting. In
                // moss, the colour every other number that helps is written in,
                // and blank for everyone the ground gains nothing from — so the
                // eye finds the one that matters instead of reading twelve rows.
                //
                // Greyed with the verb on a blocked row: the plate tint does not
                // reach the text (which is why the verb colours itself), and a
                // moss number would otherwise be the liveliest thing on a row
                // that cannot be tapped.
                var bonus = PostBonus(captured, node, watchZone != null);
                var bonusTail = bonus.Length == 0
                    ? string.Empty
                    : "  " + SizeOpen(15) + "<color=" + (blocked ? Ink2Hex : MossDeepHex) + ">" + bonus + "</color></size>";

                var button = PictureButton(sheet, portrait,
                    "<color=" + (blocked ? Ink2Hex : MossDeepHex) + ">" + verb + "</color>  "
                    // Wider than a plain row: two plates and a verb on ONE line
                    // needs the width the sheet's own notice already uses.
                    + captured.name + speciesTail + whereTail + bonusTail, working, 740, 120f, () =>
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

        private static bool PostMatches(string stationId, string buttonStationId)
        {
            return string.IsNullOrEmpty(stationId) ? string.IsNullOrEmpty(buttonStationId) : stationId == buttonStationId;
        }

        internal void Station(Familiar familiar, string stationId)
        {
            if (!_loop.StationFamiliar(familiar, stationId))
            {
                // No room at the warden's side — every place already walks (design §4).
                SetNote("every slot is walked. rest someone before " + familiar.name + " takes a post.");
                return;
            }

            SetNote(string.IsNullOrEmpty(stationId)
                ? familiar.name + " rests at camp, watching the fire."
                : familiar.name + " walks to " + StationLabel(stationId) + ".");
        }
    }
}
