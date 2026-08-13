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
    /// A zone's observation site, closing that zone's section: what is still to
    /// be drawn there, who is drawing it, and how close the pity clock has come.
    /// </summary>
    internal sealed partial class TrailPage
    {
        /// <summary>
        /// The sketching at this zone's observation site — its own post (design
        /// §6), so the card both describes the place in a sentence and is where
        /// its sketcher is sent. The strip carries the post's plate too, from
        /// 2026-08-13; this card is where the RATE and what is left to draw are
        /// read, which no plate can say.
        /// <para>
        /// One sentence and one gauge, where this was two lines of watched-hour
        /// arithmetic: what a player wants off this card is what is left to find
        /// here and whether the finding is happening, and the pity clock — the
        /// only number on it that MOVES — says that better as a band filling than
        /// as two durations to subtract. The rate keeps a clause under the gauge,
        /// since "every 12m drawn" is the one figure that tells a quickened
        /// site from a slow one. The heading drops the zone's name: the card now
        /// sits inside that zone's own section.
        /// </para>
        /// </summary>
        private void BuildSketchingPlate(DigSiteState site)
        {
            var captured = site;
            var station = Familiar.SketchStation(site.zoneId);
            var card = Card("THE SKETCHING");
            var row = Row(card);
            var line = MakeText(row.transform, string.Empty, 18, TextAnchor.MiddleLeft, Ink2);
            FlexibleWidth(line.gameObject, 1f);
            var post = GlyphButton(row.transform, PlusSprite(), "Post a sketcher", PostPlate, PostGlyph,
                () => _hud.Sheets.OpenPostingSheet(station));
            var gauge = Gauge(card, SketchGaugeHeight, out var gaugeFill);
            var rate = MakeText(card, string.Empty, 15, TextAnchor.MiddleLeft, Ink2);

            if (_loop.PlantersUnlocked() && _loop.DigSitePlanters().Count > 0)
            {
                var actions = ActionRow(card);
                foreach (var planter in _loop.DigSitePlanters())
                {
                    AddPlanterAction(actions, planter, captured.zoneId);
                }
            }

            _liveUpdaters.Add(() =>
            {
                // Ask the same question Observation asks — SketchAgentsAt, not a
                // count of familiars: the warden may take this post too, and
                // counting only familiars told a warden who WAS drawing here that
                // nobody was.
                var drawing = Stationing.SketchAgentsAt(_loop.State, _loop.Data, captured.zoneId) > 0.0;
                // Asked once and handed on: all three readings below turn on
                // what is left to record here, and the list is an allocation.
                var eligible = Observation.EligibleInsects(_loop.State, _loop.Data, captured.zoneId);
                line.text = SketchingSentence(captured, eligible.Count);
                // The post takes one body like any other, so its plate wears
                // that body — the sketcher's own portrait, or the warden's mark
                // when they are the one drawing here.
                SetButtonGlyph(post, PostMark(Stationing.OccupantOf(_loop.State, station),
                        Warden.IsSketchingAt(_loop.State, captured.zoneId)),
                    drawing ? "Change post" : "Post a sketcher");

                // The gauge and its clause only run while someone draws:
                // quoting a rate at an empty site would contradict the sentence
                // above it, and a band that cannot move reads as a stalled one.
                // The TRACK stays up across a sketch landing even though the
                // band drops to nothing — a gauge that vanished at zero would
                // make the card jump at the exact moment the player is looking
                // at it for the good news.
                var live = drawing && eligible.Count > 0 && PityClockConfigured();
                gauge.SetActive(live);
                SetFill(gaugeFill, live ? PityFraction(captured) : 0f);
                var clause = drawing ? SketchRateClause(captured, eligible) : string.Empty;
                rate.gameObject.SetActive(clause.Length > 0);
                rate.text = clause;
            });
        }

        /// <summary>The pity gauge's height — a rule with weight, not a widget with a border.</summary>
        private const float SketchGaugeHeight = 10f;

        /// <summary>
        /// The site in one sentence: who draws there, and how much of the
        /// place is still unrecorded — the two things a player is deciding
        /// between when they look at a zone's site at all. The deep amber gets
        /// a clause rather than a clock of its own (design §6: lore pacing), so
        /// the one site holding it still says that something is down there.
        /// </summary>
        private string SketchingSentence(DigSiteState site, int left)
        {
            var keeper = SketchKeeperName(site.zoneId);
            var resin = Sim.DeepAmber.Configured(_loop.Data)
                        && _loop.Data.deepAmber.zoneId == site.zoneId
                        && !Sim.DeepAmber.IsComplete(_loop.State, _loop.Data);

            if (left == 0)
            {
                if (keeper == null)
                {
                    return resin
                        ? "every plate here is drawn, and only the old resin is left to find"
                        : "every plate here is drawn, and the small lives are left to themselves";
                }

                // Said as something to act on: a body drawing at a finished site
                // is a slot the player can spend somewhere else, and the card is
                // where they would notice.
                return resin
                    ? keeper + " sits with the old resin now, every plate here drawn"
                    : keeper + " sits on there, though every plate here is drawn";
            }

            var still = left == 1 ? "one plate is still unrecorded" : left + " plates are still unrecorded";
            var tail = resin ? still + ", and the old resin holds something older" : still;
            return keeper == null
                ? "<color=" + OchreInkHex + ">no one draws here, and " + tail + "</color>"
                : keeper + " draws where the small lives cross, and " + tail;
        }

        /// <summary>Who draws at this site — a companion's name, the warden's, or null while the post stands empty.</summary>
        private string SketchKeeperName(string zoneId)
        {
            var occupant = Stationing.OccupantOf(_loop.State, Familiar.SketchStation(zoneId));
            if (occupant != null)
            {
                return occupant.name;
            }

            return Warden.IsSketchingAt(_loop.State, zoneId) ? _loop.WardenName() : null;
        }

        /// <summary>
        /// Whether the data has a pity timer at all — hand-built fixtures don't,
        /// and a gauge filling toward a certainty that cannot arrive is a lie
        /// about a clock, so the whole track comes off the card instead. (The
        /// other half of that test is whether anything is left to record here,
        /// which the card already has in hand.)
        /// </summary>
        private bool PityClockConfigured()
        {
            var observation = _loop.Data.economy?.observation;
            return observation != null && observation.pityTimerHoursWatched > 0.0;
        }

        /// <summary>
        /// How far this site's drawn hours have run toward the sketch the pity
        /// timer guarantees — the gauge's band, and the one anti-starvation
        /// number that used to run unseen. Only meaningful where
        /// <see cref="PityClockConfigured"/> holds.
        /// </summary>
        private float PityFraction(DigSiteState site)
        {
            var hours = _loop.Data.economy?.observation?.pityTimerHoursWatched ?? 0.0;
            return hours > 0.0 ? Mathf.Clamp01((float)(site.pityHours / hours)) : 0f;
        }

        /// <summary>
        /// The clause under the gauge: how often a sketch comes here at the
        /// current rates, and what the gauge is filling toward. The surviving
        /// half of the old clocks line — the rate is the figure that tells a
        /// quickened site from a slow one, and the band cannot say it.
        /// </summary>
        private string SketchRateClause(DigSiteState site, List<InsectData> eligible)
        {
            var observation = _loop.Data.economy?.observation;
            if (observation == null || observation.baseSketchesPerHour <= 0.0 || eligible.Count == 0)
            {
                return string.Empty;
            }

            var sketchers = Stationing.SketchAgentsAt(_loop.State, _loop.Data, site.zoneId);
            var siteMult = Upgrades.DigSpeedMultiplier(_loop.State, _loop.Data)
                           * Wheel.DigSpeedMult(_loop.State, _loop.Data)
                           * Planters.DigSpeedMultiplier(_loop.State, _loop.Data, site.zoneId);
            var totalRarity = 0.0;
            foreach (var insect in eligible)
            {
                totalRarity += insect.rarity;
            }

            var perHour = sketchers * observation.baseSketchesPerHour * siteMult * totalRarity;
            if (perHour <= 0.0)
            {
                return "the sketching is stalled";
            }

            var clause = "a sketch about every " + NumberFormat.Duration(3600.0 / perHour) + " drawn";
            return observation.pityTimerHoursWatched > 0.0
                ? clause + ", certain by " + NumberFormat.Duration(observation.pityTimerHoursWatched * 3600.0)
                : clause;
        }
    }
}
