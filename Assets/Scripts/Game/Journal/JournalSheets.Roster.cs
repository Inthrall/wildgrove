using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Wildgrove.Sim;
using static Wildgrove.Game.JournalTheme;
using static Wildgrove.Game.JournalFormat;
using static Wildgrove.Game.JournalWidgets;

namespace Wildgrove.Game
{
    /// <summary>
    /// A post asked from the companion's side: <em>where shall this one walk?</em>
    /// Opened from a roster row, so it is also the one place a companion is read
    /// whole, and the only place kinship's three perks are ever put in numbers.
    /// <para>
    /// The pony's version of this sheet is the one page in the journal with
    /// nothing to decide, and it says so rather than offering posts that refuse.
    /// </para>
    /// </summary>
    internal sealed partial class JournalSheets
    {
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

            // The standing the roster used to carry on its row, before the
            // roster became a drawer of plates (2026-08-06): the level and how
            // far into the next, and the two honours the row wore. "% to next"
            // is the crafts card's idiom — the levels climb the same way, so
            // they read the same way. Moss, not ochre: accolades are honours,
            // and ochre is the ink of costs and halted work.
            var bonded = familiar.bonded
                ? "  " + SizeOpen(14) + "<color=" + MossDeepHex + ">BONDED</color></size>"
                : string.Empty;
            var kinship = _loop.FamiliarKinship(familiar) > 0
                ? "  " + SizeOpen(14) + "<color=" + MossDeepHex + ">KINSHIP "
                  + Roman(_loop.FamiliarKinship(familiar)) + "</color></size>"
                : string.Empty;
            MakeText(sheet, "level " + Roman(_loop.FamiliarLevel(familiar)) + " · "
                            + Mathf.RoundToInt((float)_loop.FamiliarLevelProgress(familiar) * 100f)
                            + "% to next" + bonded + kinship,
                18, TextAnchor.UpperCenter, Ink2, _serif);

            // What this one is good at, at the moment it decides where they
            // walk. Not on every roster row, which is where it is read least
            // and costs most.
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

            // The plate's newest margin line (design §7) — earned at Kinship
            // signature milestones, in the warden's hand. Older lines stay on
            // the plate's Record entry; the freshest reads here, where the
            // companion is read whole, rather than under a roster row.
            var earned = _loop.FamiliarInscriptions(familiar);
            if (earned.Count > 0)
            {
                var inscription = MakeText(sheet, "\"" + earned[earned.Count - 1] + "\"",
                    17, TextAnchor.UpperCenter, Ink2, _hand);
                var element = inscription.gameObject.AddComponent<LayoutElement>();
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

            // Said once, above the list, rather than as a tail on every empty
            // destination: from rest, an EMPTY post needs a free slot, while
            // stepping in for someone always works.
            if (familiar.IsResting && !Kith.HasRoom(_loop.State, _loop.Data))
            {
                var notice = MakeText(sheet,
                    "<i>every place at your side is spoken for, so the empty posts stay shut. a verse sung opens another, and the Warden page keeps the count. stepping in for someone already posted still works: they go back to camp.</i>",
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

            // One choice per open site: the watch is a place now, so "go and
            // watch" is as many offers as there are places to watch from.
            foreach (var site in _loop.State.digSites)
            {
                AddStationChoice(sheet, familiar, Familiar.WatchStation(site.zoneId));
            }
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
            // no plate can: the trail has none, and a species without art keeps
            // its name.
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
    }
}
