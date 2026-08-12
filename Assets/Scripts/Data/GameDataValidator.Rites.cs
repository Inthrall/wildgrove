using System.Collections.Generic;
using System.Linq;

namespace Wildgrove.Data
{
    /// <summary>
    /// The Rite: its verses, the slots they ask to be filled, and the narrative
    /// that frames them, including the final waystones chain.
    /// </summary>
    public static partial class GameDataValidator
    {
        private static void ValidateRites(GameData data, HashSet<string> resourceIds, List<string> issues)
        {
            if (data.Rites == null)
            {
                issues.Add("Rites data is missing");
                return;
            }

            CheckIds(data.Rites.Rites.Select(r => r.Id), "rite", issues);
            CheckIds(data.Rites.Rites.SelectMany(r => r.Verses).Select(v => v.Id), "verse", issues);

            // Resource offerings credit Renown at trade value (design §7) —
            // and only gathered raws and trade-kind recipe outputs have one.
            // A material offering must carry its own renownGrant instead.
            var tradeValued = new HashSet<string>(data.Zones.SelectMany(z => z.Resources));
            tradeValued.UnionWith(data.Recipes
                .Where(r => r.Kind == "trade" && r.Output != null)
                .Select(r => r.Output));

            if (data.Rites.ChooseCount <= 0)
            {
                issues.Add("Rites chooseCount must be positive");
            }

            var generator = data.Rites.Generator;
            if (generator != null)
            {
                if (generator.DemandGrowth <= 1.0)
                {
                    issues.Add("Rites generator demandGrowth must exceed 1 — each Rite must ask more than the last");
                }

                // At or above 1 the specimen slot asks more than the verse's
                // middling goods slot, and the drawer fills at a percent or two
                // of what the stores do — the luck lane would become the wall.
                if (generator.SpecimenSlotFraction < 0.0 || generator.SpecimenSlotFraction >= 1.0)
                {
                    issues.Add("Rites generator specimenSlotFraction must be in [0, 1) — a specimen slot is priced off its verse's goods asks and must stay under the middling one; 0 means the authored counts hold");
                }

                if (generator.SpotlightDiscount <= 0.0 || generator.SpotlightDiscount > 1.0)
                {
                    issues.Add("Rites generator spotlightDiscount must be in (0, 1] — the spotlight is the cheap path");
                }

                if (generator.OffSpotlightPremium < 1.0)
                {
                    issues.Add("Rites generator offSpotlightPremium must be at least 1 — off-spotlight grinds at a premium");
                }

                if (generator.ChooseCountPerMigrations < 0)
                {
                    issues.Add("Rites generator chooseCountPerMigrations cannot be negative — it is folds per extra required slot, or 0 for no ramp");
                }

                // Above one would EXAGGERATE the split it exists to soften,
                // asking millions of the cheapest good; below zero inverts it,
                // asking most of whatever is dearest. Zero reads as absent.
                if (generator.ValueSpread < 0.0 || generator.ValueSpread > 1.0)
                {
                    issues.Add("Rites generator valueSpread must be in (0, 1] — 1 is a pure value split, below it pulls dear and cheap asks together, and 0 or absent means the same as 1");
                }

                // A ceiling under the floor would ramp the gate DOWNWARDS.
                if (generator.ChooseCountMax > 0 && generator.ChooseCountMax < data.Rites.ChooseCount)
                {
                    issues.Add($"Rites generator chooseCountMax ({generator.ChooseCountMax}) is below chooseCount ({data.Rites.ChooseCount}) — the breadth ramp must not go backwards");
                }

                // Whether a widened verse still offers a CHOICE depends on how
                // many candidate goods its zone has by that point in the run,
                // which the validator can't see — so that proof lives in
                // RiteGeneratorTests alongside the existing reachability sweep,
                // the same split the generator's other invariants already use.
            }

            // CurrentRite takes the FIRST rite matching a migration index —
            // a duplicate silently shadows its twin forever.
            foreach (var duplicate in data.Rites.Rites.GroupBy(r => r.Migration).Where(g => g.Count() > 1))
            {
                issues.Add($"Duplicate rite migration index {duplicate.Key} — only the first is ever served");
            }

            // A verse only accepts offerings once its zone is unlocked, and
            // the Rite needs EVERY verse complete — a verse keyed to a zone
            // nothing opens would block Migration permanently.
            var unlockableZones = UnlockableZoneIds(data);

            foreach (var rite in data.Rites.Rites)
            {
                if (rite.Migration < 0)
                {
                    issues.Add($"Rite '{rite.Id}' has negative migration index");
                }

                if (rite.Verses.Count == 0)
                {
                    issues.Add($"Rite '{rite.Id}' has no verses");
                }

                foreach (var verse in rite.Verses)
                {
                    if (verse.Zone == null || !data.ZonesById.ContainsKey(verse.Zone))
                    {
                        issues.Add($"Verse '{verse.Id}' references unknown zone '{verse.Zone}'");
                    }
                    else if (!unlockableZones.Contains(verse.Zone))
                    {
                        issues.Add($"Verse '{verse.Id}' zone '{verse.Zone}' is never unlockable — no upgrade grants it, so the Rite could never complete");
                    }

                    // The choose-N-of-M safety valve only exists if M > N.
                    if (verse.Slots.Count <= data.Rites.ChooseCount)
                    {
                        issues.Add($"Verse '{verse.Id}' has {verse.Slots.Count} slots but chooseCount is {data.Rites.ChooseCount} — no slot choice left");
                    }

                    foreach (var skill in verse.Spotlight.Where(s => !KnownSkills.Contains(s)))
                    {
                        issues.Add($"Verse '{verse.Id}' spotlights unknown skill '{skill}'");
                    }

                    foreach (var slot in verse.Slots)
                    {
                        ValidateRiteSlot(verse.Id, slot, resourceIds, tradeValued, issues);
                    }
                }
            }
        }

        private static void ValidateRiteSlot(string verseId, RiteSlotDef slot, HashSet<string> resourceIds,
            HashSet<string> tradeValued, List<string> issues)
        {
            switch (slot.Type)
            {
                case RiteSlotType.Resource:
                    if (slot.Resource == null || !resourceIds.Contains(slot.Resource))
                    {
                        issues.Add($"Verse '{verseId}' resource slot references '{slot.Resource}' which is not gathered from any zone or produced by any recipe");
                    }
                    else if (!tradeValued.Contains(slot.Resource) && slot.RenownGrant <= 0)
                    {
                        // Materials price at zero, so without an explicit grant
                        // the slot would credit no Renown — taxing prestige.
                        issues.Add($"Verse '{verseId}' resource slot '{slot.Resource}' is a material with no trade value — it needs an explicit renownGrant");
                    }

                    if (slot.Amount <= 0)
                    {
                        issues.Add($"Verse '{verseId}' resource slot needs a positive amount");
                    }

                    break;

                case RiteSlotType.Deed:
                    if (string.IsNullOrWhiteSpace(slot.Deed))
                    {
                        issues.Add($"Verse '{verseId}' deed slot names no deed");
                    }
                    else if (!KnownDeeds.Contains(slot.Deed))
                    {
                        issues.Add($"Verse '{verseId}' deed slot names '{slot.Deed}', which the sim never records — it could never fill");
                    }

                    RequireCountAndGrant(verseId, slot, issues);
                    break;

                case RiteSlotType.Specimen:
                    if (slot.Quality == null || !KnownSpecimenQualities.Contains(slot.Quality))
                    {
                        issues.Add($"Verse '{verseId}' specimen slot has unknown quality '{slot.Quality}'");
                    }

                    RequireCountAndGrant(verseId, slot, issues);
                    break;

                case RiteSlotType.Sketch:
                    RequireCountAndGrant(verseId, slot, issues);
                    break;

                default:
                    issues.Add($"Verse '{verseId}' has slot type '{slot.Type}' with no validation rule");
                    break;
            }
        }

        private static void RequireCountAndGrant(string verseId, RiteSlotDef slot, List<string> issues)
        {
            if (slot.Count <= 0)
            {
                issues.Add($"Verse '{verseId}' {slot.Type} slot needs a positive count");
            }

            // Non-resource offerings have no trade value, so the fixed grant is
            // how they credit Renown (design doc §7) — zero would tax prestige.
            if (slot.RenownGrant <= 0)
            {
                issues.Add($"Verse '{verseId}' {slot.Type} slot needs a positive renownGrant");
            }
        }

        private static void ValidateDialogue(GameData data, List<string> issues)
        {
            if (data.Dialogue == null)
            {
                issues.Add("Dialogue data is missing");
                return;
            }

            foreach (var key in data.Dialogue.Waystones.Keys.Where(k => !data.ZonesById.ContainsKey(k)))
            {
                issues.Add($"Waystone text references unknown zone '{key}'");
            }

            foreach (var key in data.Dialogue.Verses.Keys.Where(k => !data.ZonesById.ContainsKey(k)))
            {
                issues.Add($"Verse text references unknown zone '{key}'");
            }

            foreach (var key in data.Dialogue.InsectPlates.Keys.Where(k => !data.InsectsById.ContainsKey(k)))
            {
                issues.Add($"Insect plate text references unknown insect '{key}'");
            }

            // The MVP zones ship with their words: a waystone that reveals
            // nothing on arrival, or a verse with no line at its site, is a
            // hole the player walks into. Later-scope zones may stay silent
            // until their content pass.
            foreach (var zone in data.Zones.Where(z => z.Scope == "mvp"))
            {
                if (!data.Dialogue.Waystones.TryGetValue(zone.Id, out var waystone)
                    || string.IsNullOrWhiteSpace(waystone))
                {
                    issues.Add($"MVP zone '{zone.Id}' has no waystone text");
                }

                if (!data.Dialogue.Verses.TryGetValue(zone.Id, out var verse)
                    || string.IsNullOrWhiteSpace(verse))
                {
                    issues.Add($"MVP zone '{zone.Id}' has no verse text");
                }
            }

            ValidateFinalWaystones(data, issues);
        }

        /// <summary>
        /// The final waystones (design §7). Nothing gates on the chain, so none
        /// of this can soft-lock a save — but the chain carries the one reveal
        /// the game has, one stone per fold, and a chain keyed to ground the
        /// warden can never stand on is a story that simply never gets told.
        /// </summary>
        private static void ValidateFinalWaystones(GameData data, List<string> issues)
        {
            var chain = data.Dialogue.FinalWaystones;
            if (chain == null)
            {
                return;
            }

            var stones = chain.Stones ?? new List<DialogueData.FinalWaystoneStone>();
            if (string.IsNullOrWhiteSpace(chain.Zone))
            {
                if (stones.Count > 0)
                {
                    issues.Add("The final waystones name no zone — the chain would stand nowhere");
                }

                return;
            }

            if (!data.ZonesById.ContainsKey(chain.Zone))
            {
                issues.Add($"The final waystones reference unknown zone '{chain.Zone}'");
                return;
            }

            // Mirrors the verse-zone and deep-amber rules.
            if (!UnlockableZoneIds(data).Contains(chain.Zone))
            {
                issues.Add($"The final waystones stand in '{chain.Zone}', which is never unlockable — the §7 reveal could never be read");
            }

            if (stones.Count == 0)
            {
                issues.Add($"The final waystones name zone '{chain.Zone}' but hold no stones");
            }

            CheckIds(stones.Select(s => s.Id), "final waystone", issues);

            foreach (var stone in stones.Where(s => string.IsNullOrWhiteSpace(s.Text)))
            {
                issues.Add($"Final waystone '{stone.Id}' has no text — a blank stone reads as a bug");
            }

            // The arrival stone is where the chain's cadence is taught (§7's
            // teaching pass), so a chain on a zone with no ordinary waystone
            // would hand the player a stone a season with nothing saying so.
            if (!data.Dialogue.Waystones.TryGetValue(chain.Zone, out var arrival)
                || string.IsNullOrWhiteSpace(arrival))
            {
                issues.Add($"Zone '{chain.Zone}' carries the final waystones but has no waystone text of its own — nothing would tell the player more stones are coming");
            }
        }
    }
}
