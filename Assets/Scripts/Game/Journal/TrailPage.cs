using System.Collections.Generic;
using BreakInfinity;
using UnityEngine;
using UnityEngine.UI;
using Wildgrove.Data;
using Wildgrove.Sim;
using static Wildgrove.Game.JournalTheme;
using static Wildgrove.Game.JournalFormat;
using static Wildgrove.Game.JournalSprites;
using static Wildgrove.Game.JournalWidgets;

namespace Wildgrove.Game
{
    /// <summary>
    /// The Trail page: the land's own business, ground by ground. This file
    /// assembles the page and keeps the zone headings that fold it, with the rest
    /// in the partials beside it: <c>Home</c> (the recruit bar), <c>Wheel</c>
    /// (the tide and the keeping), <c>Nodes</c> (a ground's plates),
    /// <c>Sketching</c> (its observation site) and <c>Verses</c> (the Rite).
    /// <para>
    /// Posting lives on the world strip's badges, so the plates here carry only
    /// yields, replanting and planters. The kith roster lives on the Warden page.
    /// </para>
    /// <para>
    /// The page's head is kept SHORT, deliberately, and that is a rule rather
    /// than a preference (2026-08-13). Everything above the first ground is
    /// status, and the plates are the only thing on the page to act on: a head
    /// that grew to the tide's line, its touch label, an unfolded keeping card
    /// and the trail-home bar put the first gathering plate a viewport and a
    /// half down a page whose whole subject is gathering. A new pinned line at
    /// the top of this page has to be worth a plate, and almost nothing is.
    /// </para>
    /// </summary>
    internal sealed partial class TrailPage : JournalSection
    {
        internal TrailPage(GameHud hud) : base(hud) { }

        internal void BuildTrailPage()
        {
            BuildTideLine();
            BuildKeepingCard();
            BuildRecruitBar();

            var unlockedZones = ZonesInOrder();
            // Newest zone first — the page header names it, so the page must
            // open on it; the older grounds follow, folded shut behind their
            // names so eight zones of plates stay one readable page. Every one
            // of them is still worked whether or not its plates are drawn:
            // folding shortens the page, it doesn't rest the land.
            unlockedZones.Reverse();
            var newest = NewestZoneId();
            var figure = 1;
            foreach (var zone in unlockedZones)
            {
                // One ground and no heading to press: it must never fold, or
                // the page could be shut with nothing left to open it with.
                var open = unlockedZones.Count == 1 || JournalZones.IsOpen(_zoneOpen, zone.id, newest);

                // The zone's keystone specimen (design §3) rides the heading's
                // right margin, opposite the chevron. It stood on the page under
                // the heading until 2026-08-13, where it read as a loose plate
                // between the ground's name and its first card — and it only
                // drew while the ground was open, so a folded trail was a column
                // of bare names. In the margin it costs no height at all (the
                // heading already carries a 120 floor) and a shut ground keeps
                // its creature.
                var keystone = ArtLibrary.ForZone(zone.id);
                if (unlockedZones.Count > 1)
                {
                    BuildZoneHeading(zone, open, keystone);
                }
                else if (keystone != null)
                {
                    // One ground draws no heading, so there is no margin to ride:
                    // the mark keeps its old place on the page rather than the
                    // opening run losing its specimen entirely.
                    PlateImage(_body, keystone, 60f).name = "Keystone";
                }

                if (!open)
                {
                    continue;
                }

                foreach (var node in _loop.State.nodes)
                {
                    if (node.zoneId == zone.id)
                    {
                        BuildNodePlate(node, figure++);
                    }
                }

                // The zone's own site closes its section. The cards used to be
                // a block of their own after every ground, which put a run's six
                // sites in one stack of near-identical plates, each headed by a
                // place the reader had scrolled past — and folding a ground shut
                // left its site behind on the page.
                var site = SiteIn(zone.id);
                if (site != null)
                {
                    BuildSketchingPlate(site);
                }
            }

            BuildVerseCards();
            BuildWaystoneFooter();
        }

        /// <summary>The observation site in a zone, or null where the zone holds none (design §6: sites open from Zone 3).</summary>
        private DigSiteState SiteIn(string zoneId)
        {
            foreach (var site in _loop.State.digSites)
            {
                if (site.zoneId == zoneId)
                {
                    return site;
                }
            }

            return null;
        }

        /// <summary>
        /// A ground's name, and the fold that opens or shuts it. Closed, the
        /// name carries what grows there — the page still reads as an index of
        /// the trail rather than a row of shut drawers, and the warden can see
        /// where the fibres are without opening anything.
        /// <para>
        /// It's the journal's button plate rather than furniture of its own so
        /// that it is a real control: focus reaches it, the pad presses it, and
        /// it answers a touch the way every other plate does. The name is set
        /// smaller than a button's usual voice — it heads a section, it doesn't
        /// ask for anything.
        /// </para>
        /// <para>
        /// A chevron in the left margin says which way the ground is folded.
        /// Without it, which way a plate opens is legible only by inference —
        /// from whether plates follow it, and from the growing-list subtitle a
        /// shut ground wears — so a ground with nothing under it reads as an
        /// unresponsive button rather than an empty open one.
        /// </para>
        /// <para>
        /// The ground's keystone specimen rides the right margin, the chevron's
        /// opposite number: name on the left, creature on the right, and a
        /// folded trail reads as a row of grounds rather than a row of words.
        /// </para>
        /// </summary>
        private void BuildZoneHeading(ZoneData zone, bool open, Sprite keystone)
        {
            var captured = zone.id;
            var name = zone.displayName.ToUpperInvariant();
            var label = open
                ? SizeOpen(15) + name + "</size>"
                : SizeOpen(15) + name + "</size>" + SizeOpen(13) + "\n<color=" + Ink2Hex + ">"
                  + ZoneGrowth(zone) + "</color></size>";

            // The heading hands its own rect to the fold, which notes where it
            // stands in the viewport — the rebuilt page puts it back there.
            Button heading = null;
            heading = Button(_body, label, 400, () => _hud.FoldZone(captured, (RectTransform)heading.transform));
            heading.gameObject.name = "ZoneHeading";
            AddFoldArrow(heading, open);
            // After the chevron, never before: both widen the name's inset and
            // the mark's lane is the wider of the two, so it has to be set last.
            AddHeadingMark(heading, keystone);

            // The heading the page is being rebuilt around: the scroll comes
            // back to it once the fresh page has a height, so the ground the
            // player opened is still under the finger that opened it.
            Anchor(captured, (RectTransform)heading.transform);
        }

        /// <summary>
        /// The newest unlocked ground — the one the page opens on, and the
        /// default every unpressed fold is measured against.
        /// </summary>
        internal string NewestZoneId()
        {
            var zones = ZonesInOrder();
            return zones.Count > 0 ? zones[zones.Count - 1].id : null;
        }

        /// <summary>
        /// What a folded ground is still growing, named in its own words —
        /// the nodes' resources, in the order the page would have drawn them,
        /// and a word for a sketching post standing empty.
        /// <para>
        /// The sketching word is here because the card carrying it now folds away
        /// with the ground (it used to sit in a block of its own below every
        /// zone, always drawn): without it, shutting a ground would hide the one
        /// thing on it that is waiting to be answered.
        /// </para>
        /// </summary>
        private string ZoneGrowth(ZoneData zone)
        {
            var growing = new List<string>();
            foreach (var node in _loop.State.nodes)
            {
                if (node.zoneId == zone.id && !growing.Contains(node.resourceId))
                {
                    growing.Add(node.resourceId);
                }
            }

            var line = growing.Count == 0 ? "folded" : string.Join(" · ", growing.ToArray());
            var site = SiteIn(zone.id);
            if (site != null && Stationing.SketchAgentsAt(_loop.State, _loop.Data, zone.id) <= 0.0)
            {
                line += " · <color=" + OchreInkHex + ">no one sketches here</color>";
            }

            return line;
        }

        private void BuildWaystoneFooter()
        {
            var zone = LatestZone();
            if (zone == null)
            {
                return;
            }

            var text = Narrative.WaystoneText(_loop.Data, zone.id);
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var stone = ArtLibrary.ForJournal("waystone");
            if (stone != null)
            {
                PlateImage(_body, stone, 200f).name = "WaystoneMark";
            }

            var footer = MakeText(_body, "<i>“" + text + "”</i>\n" + SizeOpen(14) + "WAYSTONE · " + zone.displayName.ToUpperInvariant() + "</size>",
                18, TextAnchor.MiddleCenter, Ink2, _serif);
            footer.gameObject.name = "Waystone";
        }
    }
}
