using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Wildgrove.Data.Tests
{
    /// <summary>
    /// Loads the real design/data JSON and proves it parses into the typed
    /// model and passes cross-file validation. The negative tests corrupt the
    /// real JSON in memory to prove the validator actually catches breakage.
    /// </summary>
    public class GameDataTests
    {
        private static string DataDir =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "design", "data"));

        private static GameDataSources LoadSources()
        {
            return GameData.ReadSourcesFromFiles(DataDir);
        }

        [Test]
        public void Parse_RealData_PopulatesAllSections()
        {
            var data = GameData.Parse(LoadSources());

            Assert.That(data.Economy, Is.Not.Null);
            Assert.That(data.Zones, Is.Not.Empty);
            Assert.That(data.Upgrades, Has.Count.EqualTo(30),
                "design doc §9 defines 30 named upgrades; the kith track adds the two recruit rungs, Mistfen's trail map landed with the zone (apothecary), the Hollows brought its map plus the deepsteel toolset, the Almanac Desk moved into the Verdure tree, and the Crags brought its map plus the fleece shears, and the five hauling rungs left when hauling retired (2026-07-31)");
            Assert.That(data.Recipes, Is.Not.Empty);
            Assert.That(data.Buildings, Has.Count.EqualTo(5), "design §9 defines the five camp building lines");
            Assert.That(data.Gear, Is.Not.Empty);
            Assert.That(data.Insects, Is.Not.Empty);
            Assert.That(data.Rites.Rites, Is.Not.Empty);
            Assert.That(data.Bonds, Is.Not.Empty);
            Assert.That(data.Species, Is.Not.Empty, "design §4 defines the familiar species");
            Assert.That(data.Planters, Is.Not.Empty, "design §3 defines the planters");
            Assert.That(data.Regions, Is.Not.Empty, "design §8 defines the region modifiers");
            Assert.That(data.Tinctures, Is.Not.Empty, "design §5 defines the Apothecary's tinctures");
            Assert.That(data.DeepAmber, Is.Not.Null, "design §6 defines the deep amber window");
            Assert.That(data.Exchange, Is.Not.Null, "design §9 the Exchange spread");
            Assert.That(data.Dialogue.Waystones, Is.Not.Empty);
            Assert.That(data.Dialogue.Verses, Is.Not.Empty);
        }

        [Test]
        public void Parse_RealData_TypedValuesSurvive()
        {
            var data = GameData.Parse(LoadSources());

            Assert.That(data.Economy.CostGrowth.Building, Is.EqualTo(1.25));
            Assert.That(data.Economy.Gifts.PileGoods, Is.EqualTo(10L));
            Assert.That(data.Economy.Tools.Tiers.First(), Is.EqualTo("flint"));
            Assert.That(data.ResourcesById["berries"].SellValue, Is.GreaterThan(0));
            Assert.That(data.ResourcesById["copper-scree"].Skill, Is.EqualTo("mining"));
            Assert.That(data.Economy.Crafting.BaseCraftSeconds, Is.EqualTo(5.0));
            Assert.That(data.Economy.Kith.SlotsBase, Is.EqualTo(1), "design §4 ladder: one slot from minute one");
            Assert.That(data.Economy.Kith.SlotsMax, Is.EqualTo(6), "design §4 ladder: six kith slots total");
            Assert.That(data.Economy.Kith.VerseMilestones, Is.EqualTo(new[] { 2, 5, 10 }), "verses sung earn the middle rungs");
            Assert.That(data.Economy.Kith.GeneratorGatherPosts, Is.EqualTo(2), "the run-2+ generator's stationing assumption");
            Assert.That(data.Economy.Kith.GatherPerSecond, Is.EqualTo(0.1), "a familiar's base hands — cut to a tenth when hauling retired (2026-07-31)");
            Assert.That(data.Economy.Store.StarterBundleAmber, Is.GreaterThan(0), "the starter bundle's one-time Amber pile");
            Assert.That(data.SpeciesById["meadow-vole"].Trait.Kind, Is.EqualTo("nodeYieldBonus"));
            Assert.That(data.SpeciesById["meadow-vole"].Trait.Resources,
                Is.EquivalentTo(new[] { "berries", "wildflowers" }), "the vole works a related pair of meadow nodes");
            Assert.That(data.SpeciesById["warren-weasel"].Trait.Resources,
                Is.EquivalentTo(new[] { "copper-scree", "tin-seam" }), "the weasel works the ore pair (copper + tin)");
            Assert.That(data.SpeciesById["pack-raven"].Trait.Kind, Is.EqualTo("bubbleRewardBonus"));
            Assert.That(data.BuildingsById["forge"].Materials.ContainsKey("copper-scree"), Is.True, "buildings cost a material bundle now (money→XP)");
            Assert.That(data.BuildingsById["forge"].MilestoneUpgradeIds, Is.EqualTo(new[] { "bellows-forge" }));
            Assert.That(data.BuildingsById["roosts"].PerLevel.Type, Is.EqualTo("comfort"), "Roosts levels familiar comfort (design §4)");
            Assert.That(data.BuildingsById["roosts"].PerLevel.Value, Is.GreaterThan(0));
            Assert.That(data.RecipesById["iron-ingot"].StationLevel, Is.EqualTo(2), "iron heat is forge 2");
            Assert.That(data.RecipesById["copper-ingot"].StationLevel, Is.EqualTo(1), "absent stationLevel defaults to 1");
            Assert.That(data.RecipesById["iron-ingot"].SkillLevel, Is.EqualTo(5), "iron smelting waits for forgecraft 5");
            Assert.That(data.RecipesById["copper-ingot"].SkillLevel, Is.EqualTo(1), "absent skillLevel defaults to 1");
            Assert.That(data.Economy.Xp.GatherPerUnit, Is.EqualTo(3.0));
            Assert.That(data.Economy.Xp.CraftPerBatch, Is.EqualTo(25.0));
            Assert.That(data.Economy.Mastery.Base, Is.EqualTo(50.0));
            Assert.That(data.Economy.Mastery.XpPerUnit, Is.EqualTo(0.25));
            Assert.That(data.Economy.Replant.RichnessPerLevel, Is.EqualTo(0.10), "design §3 replanting richness");
            Assert.That(data.Economy.Quality.PristineValueMult, Is.EqualTo(10.0));
            Assert.That(data.Economy.Tending.PristineChanceBonus, Is.EqualTo(1.0));
            Assert.That(data.Economy.Observation.BaseSketchesPerHour, Is.EqualTo(0.25));
            Assert.That(data.Economy.Amber.DigFindsPerHour, Is.EqualTo(0.06), "the free amber drip from dig sites");
            Assert.That(data.Economy.Amber.TimeSkipHours, Is.EqualTo(4.0));
            Assert.That(data.Economy.Amber.TimeSkipCostAmber, Is.EqualTo(15.0));
            Assert.That(data.UpgradesById["map-oldgrowth"].Effects.Any(e => e.Type == EffectType.UnlockSkill && e.Skill == "entomology"),
                Is.True, "the first observation site's map also teaches entomology");
            Assert.That(data.ZonesById["silverrun-river"].RequiredTool, Is.EqualTo("bronze"));
            Assert.That(data.ZonesById["sunfield-meadow"].RequiredTool, Is.Null, "the starting zone is ungated");
            Assert.That(data.UpgradesById["copper-sickle"].ToolTier, Is.EqualTo("copper"));
            Assert.That(data.AlmanacById["old-songs-ii"].Requires, Is.EqualTo("old-songs-i"));
            Assert.That(data.AlmanacById["long-watch-i"].CostVerdure, Is.EqualTo(2.0));
            Assert.That(data.SpreadsById["river-catch"].Entries, Has.Count.EqualTo(4));
            Assert.That(data.Rites.Rites.Single().Verses[1].Slots[2].RenownGrant, Is.EqualTo(750), "material offerings carry an explicit grant");
            Assert.That(data.Rites.Generator.DemandGrowth, Is.EqualTo(1.45), "the run-2+ generator's d in baseQty · d^m");
            Assert.That(data.Rites.Generator.ChooseCountPerMigrations, Is.EqualTo(2), "folds per extra required slot — the breadth ramp");
            Assert.That(data.Rites.Generator.ChooseCountMax, Is.EqualTo(5));
            Assert.That(data.Economy.CostGrowth.Almanac, Is.EqualTo(1.25), "the geometric step on the endless Almanac line");
            Assert.That(data.AlmanacById["the-long-song"].Repeatable, Is.True, "Verdure's endless sink");
            Assert.That(data.AlmanacById["known-way-i"].Effects.Single().Upgrade, Is.EqualTo("map-bramble"),
                "the zone skip grants the trail's own rung, never a second unlock path");
            Assert.That(data.AlmanacById["the-fire-remembers"].Effects.Single().Type, Is.EqualTo(EffectType.KeepCraftOrders));
            Assert.That(data.BondsById["sootwing"].Role, Is.EqualTo("carrier"), "a carrier bonds as a carrier");
            Assert.That(data.BondsById["sootwing"].Source.Type, Is.EqualTo("folioSpread"));
            Assert.That(data.BondsById["burr"].Source.Id, Is.EqualTo("old-friend"), "the Almanac-node bond");
            Assert.That(data.AlmanacById["old-friend"].Effects, Is.Empty,
                "the Old Friend is the bond alone — slots come from verses and the store now");
            Assert.That(data.Rites.Generator.SpotlightDiscount, Is.EqualTo(0.6));
            Assert.That(data.Rites.Generator.OffSpotlightPremium, Is.EqualTo(1.5));
            Assert.That(data.UpgradesById["copper-sickle"].Materials["copper-ingot"], Is.EqualTo(5));
            Assert.That(data.UpgradesById["flint-sickle"].Effects.Single().Type, Is.EqualTo(EffectType.YieldMult));
            Assert.That(data.RecipesById["bronze-ingot"].Inputs["tin-seam"], Is.EqualTo(2));
            Assert.That(data.InsectsById["those-who-sow"].Habitats, Has.Count.EqualTo(2));
            Assert.That(data.ZonesById["sunfield-meadow"].VerseSite, Is.EqualTo("the fire circle"));
            Assert.That(data.Rites.ChooseCount, Is.EqualTo(3));
            Assert.That(data.Rites.Rites.Single().Verses.First().Slots.First().Type, Is.EqualTo(RiteSlotType.Resource));
            Assert.That(data.Rites.Rites.Single().Verses.First().Slots.Last().Type, Is.EqualTo(RiteSlotType.Specimen));
            Assert.That(data.Regions.Single(r => r.Id == "misted").Effects
                    .Any(e => e.Type == EffectType.YieldMult && e.Resource == "fish" && e.Value > 1.0),
                Is.True, "a misted region favours the river (design §8)");
            Assert.That(data.Regions.All(r => !string.IsNullOrWhiteSpace(r.Sign)), Is.True,
                "every season gets its one line");
            Assert.That(data.Economy.FamiliarXp.SignatureMilestones, Is.EqualTo(new[] { 2, 4, 7 }),
                "Kinship signature milestones (design §4)");
            Assert.That(data.Economy.FamiliarXp.SignatureDeepening, Is.EqualTo(0.25));
            Assert.That(data.Species.All(s => s.Inscriptions.Count == 3), Is.True,
                "every species' plate has its three margin lines authored (§7)");

            // Mistfen Marsh (zone 5, v1.1) — fireflies are observed now, not
            // gathered: the marsh's third find is glow-moss and the lanterns
            // are an insect plate at its watch site (design §3/§6).
            Assert.That(data.ZonesById["mistfen-marsh"].Resources,
                Is.EquivalentTo(new[] { "peat", "rare-herbs", "glow-moss" }));
            Assert.That(data.Resources.Any(r => r.Id == "fireflies"), Is.False,
                "fireflies stopped being a gatherable when the deep chase became observe-sketch-release");
            Assert.That(data.InsectsById["lantern-bearers"].Habitats, Is.EqualTo(new[] { "mistfen-marsh" }));
            Assert.That(data.ZonesById["mistfen-marsh"].VerseSite, Is.EqualTo("the lantern pool"));
            Assert.That(data.UpgradesById["map-mistfen"].Effects.Any(e => e.Type == EffectType.UnlockSkill && e.Skill == "apothecary"),
                Is.True, "the marsh map teaches the Apothecary");
            Assert.That(data.UpgradesById["map-mistfen"].Effects.Any(e => e.Type == EffectType.UnlockDigSite && e.Zone == "mistfen-marsh"),
                Is.True, "and opens its observation site");
            Assert.That(data.SpeciesById["osier-otter"].Trait.Resources,
                Is.EquivalentTo(new[] { "peat", "glow-moss" }), "the marsh's pair specialist");
            Assert.That(data.Tinctures.All(t => t.DurationSec > 0 && t.Effects.Count > 0), Is.True);
            Assert.That(data.Recipes.Any(r => r.Skill == "apothecary"), Is.True, "the brews are fire recipes");

            // The Hollows (zone 6, v1.1) — bone beds stopped being a crop for
            // the same reason fireflies did: the buried past is borrowed with
            // the eyes only (§6). The third find is ashglass, the deep amber
            // is the site's authored chase, and delving feeds the deepsteel tier.
            Assert.That(data.ZonesById["the-hollows"].Resources,
                Is.EquivalentTo(new[] { "deep-ores", "crystals", "ashglass" }));
            Assert.That(data.Resources.Any(r => r.Id == "bone-beds"), Is.False,
                "bone beds stopped being a gatherable — digging up the dead contradicts §6 outright");
            Assert.That(data.ZonesById["the-hollows"].VerseSite, Is.EqualTo("the echo gallery"));
            Assert.That(data.UpgradesById["map-hollows"].Effects.Any(e => e.Type == EffectType.UnlockSkill && e.Skill == "delving"),
                Is.True, "the Hollows map teaches delving");
            Assert.That(data.UpgradesById["map-hollows"].Effects.Any(e => e.Type == EffectType.UnlockDigSite && e.Zone == "the-hollows"),
                Is.True, "and opens the deep watch site");
            Assert.That(data.UpgradesById["deepsteel-toolset"].ToolTier, Is.EqualTo("deepsteel"),
                "§5's tier past steel — deep ores gate it");
            Assert.That(data.Economy.Tools.Tiers.Last(), Is.EqualTo("deepsteel"));
            Assert.That(data.RecipesById["deep-ingot"].StationLevel, Is.EqualTo(3), "deep heat is forge 3");
            Assert.That(data.SpeciesById["horseshoe-bat"].Trait.Resources,
                Is.EquivalentTo(new[] { "deep-ores", "crystals" }), "the dark's pair specialist");
            Assert.That(data.SpeciesById["ermine"].Trait.Resources,
                Is.EquivalentTo(new[] { "ashglass", "glacier-ice" }), "the winter-walker pairs the burning's two residues");
            // Drawable plates only: an awarded plate holds rarity 0 because it
            // is never in the roll, which would otherwise read as the rarest.
            Assert.That(data.InsectsById["quiet-court"].Rarity,
                Is.EqualTo(data.Insects.Where(i => !i.Rewarded).Min(i => i.Rarity)),
                "the Hollows hosts the rarest plate anyone can draw");
            Assert.That(data.Rites.Rites.Single().Verses.Last().Zone, Is.EqualTo("highland-crags"),
                "the Rite grew a seventh verse with the zone");
            Assert.That(data.SpeciesById["kea"].Trait.Resources,
                Is.EquivalentTo(new[] { "eggs", "wool" }), "the flock-rider works the flock's two gifts");
            Assert.That(data.SpeciesById["pika"].Trait.Resources,
                Is.EquivalentTo(new[] { "lichen", "sky-blossoms" }), "the hay-piler grazes the heights, half a zone ahead");
            Assert.That(data.DeepAmber.Zone, Is.EqualTo("the-hollows"));
            Assert.That(data.DeepAmber.Pieces, Has.Count.EqualTo(4), "the four authored deep-past pieces");
            Assert.That(data.DeepAmber.Pieces.First().Id, Is.EqualTo("the-wing"), "the sequence is the story");
            Assert.That(data.DeepAmber.Effects, Is.Not.Empty, "the finished set is a plate — a multiplier as well as a chapter");
        }

        [Test]
        public void Parse_UpgradeWithoutMaterials_GetsEmptyDictionary()
        {
            var data = GameData.Parse(LoadSources());

            Assert.That(data.UpgradesById["map-bramble"].Materials, Is.Empty);
        }

        [Test]
        public void Parse_UnknownEffectType_Throws()
        {
            var sources = LoadSources();
            sources.UpgradesJson = sources.UpgradesJson.Replace(
                "\"type\": \"yieldMult\"",
                "\"type\": \"frobnicateMult\"");

            Assert.That(() => GameData.Parse(sources), Throws.Exception);
        }

        [Test]
        public void Validate_RealData_ReturnsNoIssues()
        {
            var issues = GameDataValidator.Validate(GameData.Parse(LoadSources()));

            Assert.That(issues, Is.Empty, string.Join("\n", issues));
        }

        [Test]
        public void Validate_RegionEffectWithUnknownResource_IsCaught()
        {
            var sources = LoadSources();
            sources.RegionsJson = sources.RegionsJson.Replace(
                "\"resource\": \"herbs\"",
                "\"resource\": \"moon-cheese\"");
            Assert.That(sources.RegionsJson, Does.Contain("moon-cheese"), "the corruption must land, or this test proves nothing");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues, Has.Some.Contains("moon-cheese"));
        }

        [Test]
        public void Validate_TinctureNoRecipeBrews_IsCaught()
        {
            var sources = LoadSources();
            sources.RecipesJson = sources.RecipesJson.Replace(
                "\"output\": \"wardens-tonic\"",
                "\"output\": \"wardens-cordial\"");
            Assert.That(sources.RecipesJson, Does.Contain("wardens-cordial"), "the corruption must land, or this test proves nothing");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues, Has.Some.Contains("could never be brewed"));
        }

        [Test]
        public void Validate_DeepAmberZoneWithoutASite_IsCaught()
        {
            var sources = LoadSources();
            // sunfield-meadow has no observation site — a window keyed there
            // could never surface a piece.
            sources.AmbersJson = sources.AmbersJson.Replace(
                "\"zone\": \"the-hollows\"",
                "\"zone\": \"sunfield-meadow\"");
            Assert.That(sources.AmbersJson, Does.Contain("sunfield-meadow"), "the corruption must land, or this test proves nothing");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues, Has.Some.Contains("no observation site"));
        }

        [Test]
        public void Validate_DeepAmberPieceWithoutLore_IsCaught()
        {
            var sources = LoadSources();
            sources.AmbersJson = sources.AmbersJson.Replace(
                "\"lore\": \"A wing in the resin, veined like nothing the trail knows. Whatever sky it flew, that sky has ended.\"",
                "\"lore\": \"\"");
            Assert.That(sources.AmbersJson, Does.Contain("\"lore\": \"\""), "the corruption must land, or this test proves nothing");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues, Has.Some.Contains("the piece is the story"));
        }

        [Test]
        public void Validate_DeepAmberWithDeadRates_IsCaught()
        {
            var sources = LoadSources();
            var before = sources.AmbersJson;
            sources.AmbersJson = sources.AmbersJson.Replace(
                "\"findsPerHour\": 0.1,",
                "\"findsPerHour\": 0,");
            Assert.That(sources.AmbersJson, Is.Not.EqualTo(before), "the corruption must land, or this test proves nothing");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues, Has.Some.Contains("can never surface a piece"));
        }

        [Test]
        public void Validate_InscriptionPastTheLastMilestone_IsCaught()
        {
            var sources = LoadSources();
            sources.EconomyJson = sources.EconomyJson.Replace(
                "\"signatureMilestones\": [2, 4, 7],",
                "\"signatureMilestones\": [2, 4],");
            Assert.That(sources.EconomyJson, Does.Contain("[2, 4],"), "the corruption must land, or this test proves nothing");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            // Every species authors three lines — with only two milestones the
            // third can never be read.
            Assert.That(issues, Has.Some.Contains("unreachable"));
        }

        [Test]
        public void Validate_StartingZoneRenamed_IsReported()
        {
            var sources = LoadSources();
            sources.ZonesJson = sources.ZonesJson.Replace(
                "\"id\": \"sunfield-meadow\"",
                "\"id\": \"meadow-prime\"");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            // The runtime seeds NewGame from the fixed id — a rename must not
            // pass validation green and then throw on first launch.
            Assert.That(issues.Any(i => i.Contains("Starting zone") && i.Contains("does not exist")),
                Is.True, string.Join("\n", issues));
        }

        [Test]
        public void RealData_FoldGates_OpenOneNewTrailPerFold()
        {
            // Design §8's fold gate as authored: the first three zones are the
            // run-1 trail and one new zone arrives per fold. Pins the shape (a
            // non-decreasing gate in zone order, reachable one fold at a time),
            // not the exact folds — those are first guesses and meant to be
            // tuned. The ladder is every zone a trail map can open; a staged
            // zone with no map yet (cloudreach-peaks) stands aside until its
            // rung lands, and then this test covers it with no edit.
            var data = GameData.Parse(LoadSources());
            var unlockable = new HashSet<string> { GameData.StartingZoneId };
            unlockable.UnionWith(data.Upgrades
                .SelectMany(u => u.Effects)
                .Where(e => e.Type == EffectType.UnlockZone && e.Zone != null)
                .Select(e => e.Zone));
            var ladder = data.Zones
                .Where(z => unlockable.Contains(z.Id))
                .OrderBy(z => z.Order)
                .ToList();

            Assert.That(ladder.First().MinMigration, Is.EqualTo(0), "the run has to start somewhere");
            for (var i = 1; i < ladder.Count; i++)
            {
                Assert.That(ladder[i].MinMigration, Is.GreaterThanOrEqualTo(ladder[i - 1].MinMigration),
                    $"'{ladder[i].Id}' opens before the zone in front of it — the trail would arrive out of order");
                Assert.That(ladder[i].MinMigration, Is.LessThanOrEqualTo(ladder[i - 1].MinMigration + 1),
                    $"'{ladder[i].Id}' skips a fold — a run with nothing new in it is the thing this gate exists to prevent");
            }

            Assert.That(ladder.Count(z => z.MinMigration == 0), Is.EqualTo(3), "run 1 walks three zones");
            Assert.That(data.Zones.Single(z => z.Id == "highland-crags").MinMigration, Is.EqualTo(4),
                "the deepest trail waits for the fifth run");
        }

        [Test]
        public void Validate_GatedStartingZone_IsReported()
        {
            var sources = LoadSources();
            sources.ZonesJson = sources.ZonesJson.Replace(
                "\"id\": \"sunfield-meadow\",    \"order\": 1",
                "\"id\": \"sunfield-meadow\",    \"minMigration\": 1,    \"order\": 1");
            Assert.That(sources.ZonesJson, Does.Contain("\"minMigration\": 1,    \"order\": 1"),
                "the corruption must land, or this test proves nothing");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("Starting zone") && i.Contains("no trail")),
                Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_MapRungCarryingItsOwnFold_IsReported()
        {
            // A trail is gated in one place. Authoring the fold on the rung as
            // well is two numbers that must agree forever, and the day they
            // drift the Rite and the ladder answer to different folds.
            var sources = LoadSources();
            sources.UpgradesJson = sources.UpgradesJson.Replace(
                "\"id\": \"map-mistfen\",",
                "\"id\": \"map-mistfen\", \"minMigration\": 4,");
            Assert.That(sources.UpgradesJson, Does.Contain("\"minMigration\": 4,"),
                "the corruption must land, or this test proves nothing");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("map-mistfen") && i.Contains("gate the zone instead")),
                Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_RecruitRungEarlierThanItsSpecies_IsReported()
        {
            // Roster.Recruit doesn't consult the species' fold — the rung's own
            // gate is the gate — so an earlier rung would silently beat the
            // species' gate and make it mean nothing.
            var sources = LoadSources();
            sources.SpeciesJson = sources.SpeciesJson.Replace(
                "\"id\": \"meadow-vole\",",
                "\"id\": \"meadow-vole\", \"minMigration\": 3,");
            Assert.That(sources.SpeciesJson, Does.Contain("\"minMigration\": 3,"),
                "the corruption must land, or this test proves nothing");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("meadow-vole") && i.Contains("gated to fold 3")),
                Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_RiteTemplateWithNothingOpenOnRunOne_IsReported()
        {
            // The Rite is the Migration gate, so a Rite with no verse in play
            // can never be completed and the save can never fold again. Gating
            // every zone of the template would do exactly that.
            var sources = LoadSources();
            // Gate the three zones that are open on run 1 (the others already
            // carry a fold), targeting each by its verse site so no zone ends
            // up with the key twice.
            foreach (var site in new[] { "the fire circle", "the hollow oak", "the oldest root" })
            {
                sources.ZonesJson = sources.ZonesJson.Replace(
                    $"\"verseSite\": \"{site}\",",
                    $"\"minMigration\": 5, \"verseSite\": \"{site}\",");
            }

            Assert.That(sources.ZonesJson.Split(new[] { "\"minMigration\": 5" }, System.StringSplitOptions.None).Length - 1,
                Is.EqualTo(3), "the corruption must land on all three, or this test proves nothing");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("no verse open on the first run")
                                        || i.Contains("no verse whose zone is open")),
                Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_StartingZoneNotLowestOrder_IsReported()
        {
            var sources = LoadSources();
            sources.ZonesJson = sources.ZonesJson.Replace(
                "\"id\": \"sunfield-meadow\",    \"order\": 1",
                "\"id\": \"sunfield-meadow\",    \"order\": 99");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("not the lowest-order zone")),
                Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_UnknownRecipeKind_IsReported()
        {
            var sources = LoadSources();
            sources.RecipesJson = sources.RecipesJson.Replace(
                "\"kind\": \"trade\"",
                "\"kind\": \"trading\"");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            // The sim switches on the literal — a typo silently makes the
            // good unsellable.
            Assert.That(issues.Any(i => i.Contains("unknown kind 'trading'")),
                Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_UnrecordableDeed_IsReported()
        {
            var sources = LoadSources();
            sources.RitesJson = sources.RitesJson.Replace(
                "\"deed\": \"tend\"",
                "\"deed\": \"forage\"");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("'forage'") && i.Contains("never records")),
                Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_VerseZoneNoTrailMapOpens_IsReported()
        {
            var sources = LoadSources();
            // cloudreach-peaks exists but is staged content — nothing unlocks it.
            // (highland-crags held this role until its trail map landed, and
            // the-hollows before that.)
            sources.RitesJson = sources.RitesJson.Replace(
                "\"zone\": \"silverrun-river\"",
                "\"zone\": \"cloudreach-peaks\"");
            Assert.That(sources.RitesJson, Does.Contain("cloudreach-peaks"), "the corruption must land, or this test proves nothing");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("cloudreach-peaks") && i.Contains("never unlockable")),
                Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_ZeroOfflineRateMultiplier_IsReported()
        {
            var sources = LoadSources();
            sources.EconomyJson = sources.EconomyJson.Replace(
                "\"rateMultiplier\": 1.0",
                "\"rateMultiplier\": 0.0");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("rateMultiplier must be positive")),
                Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_UnknownRecipeReference_IsReported()
        {
            var sources = LoadSources();
            sources.UpgradesJson = sources.UpgradesJson.Replace(
                "\"unlockRecipe\", \"recipe\": \"berry-preserve\"",
                "\"unlockRecipe\", \"recipe\": \"missing-recipe\"");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("missing-recipe")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_BuildingMilestoneUpgradeMissing_IsReported()
        {
            var sources = LoadSources();
            sources.BuildingsJson = sources.BuildingsJson.Replace(
                "\"milestoneUpgradeIds\": [\"carving-bench\"]",
                "\"milestoneUpgradeIds\": [\"no-such-upgrade\"]");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("no-such-upgrade")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_BuildingUnknownPerLevelType_IsReported()
        {
            var sources = LoadSources();
            sources.BuildingsJson = sources.BuildingsJson.Replace(
                "\"type\": \"offlineCapBonusHours\"",
                "\"type\": \"frobnicateBonus\"");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("frobnicateBonus")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_ZeroedBubblesValue_IsReported()
        {
            var sources = LoadSources();
            sources.EconomyJson = sources.EconomyJson.Replace(
                "\"spawnIntervalSec\": 25",
                "\"spawnIntervalSec\": 0");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("bubbles values must all be positive")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_NonPositiveCraftSeconds_IsReported()
        {
            var sources = LoadSources();
            sources.EconomyJson = sources.EconomyJson.Replace(
                "\"baseCraftSeconds\": 5",
                "\"baseCraftSeconds\": 0");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("baseCraftSeconds")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_ResourceWithUnknownSkill_IsReported()
        {
            var sources = LoadSources();
            sources.ResourcesJson = sources.ResourcesJson.Replace(
                "\"skill\": \"fishing\"",
                "\"skill\": \"basket-weaving\"");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("basket-weaving")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_InsectAtNonObservationSiteZone_IsReported()
        {
            var sources = LoadSources();
            sources.InsectsJson = sources.InsectsJson.Replace(
                "\"habitats\": [\"old-growth-wood\"],",
                "\"habitats\": [\"sunfield-meadow\"],");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("no observation site")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void RealData_TheAwardedPlateIsOutOfTheRollAndCarriesItsFieldNote()
        {
            var data = GameData.Parse(LoadSources());
            var plate = data.InsectsById["wayfarers-plate"];
            var effect = plate.Effects.Single();

            Assert.That(plate.Rewarded, Is.True);
            Assert.That(plate.Habitats, Is.Empty, "no site can offer it — it is given, not drawn");
            Assert.That(plate.Rarity, Is.EqualTo(0.0), "and it holds no draw weight");
            Assert.That(data.Dialogue.InsectPlates.ContainsKey("wayfarers-plate"), Is.True,
                "a plate with no field note reads as a hole in the journal");

            // Free, permanent and across every fold — so it sits below the
            // mildest thing anyone can earn in the same band, deliberately.
            var earned = data.Insects
                .Where(i => !i.Rewarded)
                .SelectMany(i => i.Effects)
                .Where(e => e.Type == effect.Type)
                .Select(e => e.Value)
                .ToList();
            Assert.That(earned, Is.Not.Empty, "nothing to compare against, so this test proves nothing");
            Assert.That(effect.Value, Is.LessThan(earned.Min()),
                "the gift is the weakest plate in the book: its value is that no one walked for it");
        }

        [Test]
        public void Validate_AwardedInsectWithADrawWeight_IsReported()
        {
            var sources = LoadSources();
            sources.InsectsJson = sources.InsectsJson.Replace(
                "\"rarity\": 0, \"rewarded\": true",
                "\"rarity\": 0.4, \"rewarded\": true");
            Assert.That(sources.InsectsJson, Does.Contain("\"rarity\": 0.4"), "the corruption must land, or this test proves nothing");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues, Has.Some.Contains("must have rarity 0"), string.Join("\n", issues));
        }

        [Test]
        public void Validate_AwardedInsectWithAHabitat_IsReported()
        {
            // A habitat on an awarded plate promises a site that will never
            // offer it — the roll skips awarded plates outright.
            var sources = LoadSources();
            sources.InsectsJson = sources.InsectsJson.Replace(
                "\"habitats\": [], \"rarity\": 0, \"rewarded\": true",
                "\"habitats\": [\"the-hollows\"], \"rarity\": 0, \"rewarded\": true");
            Assert.That(sources.InsectsJson, Does.Contain("\"habitats\": [\"the-hollows\"], \"rarity\": 0"),
                "the corruption must land, or this test proves nothing");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues, Has.Some.Contains("must hold no habitats"), string.Join("\n", issues));
        }

        [Test]
        public void Validate_RecipeWithoutOutput_IsReported()
        {
            var sources = LoadSources();
            sources.RecipesJson = sources.RecipesJson.Replace(
                "\"output\": \"planks\",",
                "");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("has no output")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_RecipeWithoutInputs_IsReported()
        {
            var sources = LoadSources();
            sources.RecipesJson = sources.RecipesJson.Replace(
                "\"inputs\": { \"fish\": 3 },",
                "");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("has no inputs")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_UpgradeWithoutId_IsReported()
        {
            var sources = LoadSources();
            sources.UpgradesJson = sources.UpgradesJson.Replace(
                "\"id\": \"flint-sickle\",",
                "");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("upgrade entry has no id")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_MissingEconomySection_IsReported()
        {
            var sources = LoadSources();
            sources.EconomyJson = sources.EconomyJson.Replace(
                "\"offline\":",
                "\"offlineTypo\":");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("Economy section 'offline' is missing")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_NonPositiveDeliveryCadence_IsReported()
        {
            var sources = LoadSources();
            sources.EconomyJson = sources.EconomyJson.Replace(
                "\"batchSeconds\": 10",
                "\"batchSeconds\": 0");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("delivery batchSeconds must be positive")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_InsectEffectWithUnknownResource_IsReported()
        {
            var sources = LoadSources();
            sources.InsectsJson = sources.InsectsJson.Replace(
                "{ \"type\": \"pristineChanceBonus\", \"value\": 0.01 }",
                "{ \"type\": \"sellValueBonus\", \"resource\": \"bogus-item\", \"value\": 0.5 }");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("bogus-item")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_DuplicateUpgradeId_IsReported()
        {
            var sources = LoadSources();
            sources.UpgradesJson = sources.UpgradesJson.Replace(
                "\"id\": \"drying-rack\"",
                "\"id\": \"flint-sickle\"");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("Duplicate upgrade id")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_UnreachableRecipe_IsReported()
        {
            var sources = LoadSources();
            sources.RecipesJson = sources.RecipesJson.Replace(
                "\"valueMult\": 1, \"kind\": \"material\", \"defaultKnown\": true",
                "\"valueMult\": 1, \"kind\": \"material\"");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("neither defaultKnown nor unlocked")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_DefaultKnownAndUpgradeUnlocked_IsReported()
        {
            var sources = LoadSources();
            sources.RecipesJson = sources.RecipesJson.Replace(
                "\"output\": \"copper-ingot\",    \"valueMult\": 3, \"kind\": \"material\"",
                "\"output\": \"copper-ingot\",    \"valueMult\": 3, \"kind\": \"material\", \"defaultKnown\": true");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("pick one")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_RecipeSkillLevelBelowOne_IsReported()
        {
            var sources = LoadSources();
            sources.RecipesJson = sources.RecipesJson.Replace(
                "\"skillLevel\": 5",
                "\"skillLevel\": 0");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("skillLevel below 1")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_NegativeXpGain_IsReported()
        {
            var sources = LoadSources();
            sources.EconomyJson = sources.EconomyJson.Replace(
                "\"gatherPerUnit\": 3,",
                "\"gatherPerUnit\": -1,");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("xp gains")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_DegenerateMasteryCurve_IsReported()
        {
            var sources = LoadSources();
            sources.EconomyJson = sources.EconomyJson.Replace(
                "\"xpPerUnit\": 0.25,",
                "\"xpPerUnit\": -1,");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("mastery progression is degenerate")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_RecipeCycle_IsReported()
        {
            var sources = LoadSources();
            // Planks now require planks — referentially fine, never craftable.
            sources.RecipesJson = sources.RecipesJson.Replace(
                "\"inputs\": { \"timber\": 3 },",
                "\"inputs\": { \"planks\": 3 },");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("can never be crafted")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_SkillLevelAboveXpCap_IsReported()
        {
            var sources = LoadSources();
            sources.RecipesJson = sources.RecipesJson.Replace(
                "\"skillLevel\": 5",
                "\"skillLevel\": 120");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("exceeds xp.maxLevel")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_RecipeOnNeverGrantedSkill_IsReported()
        {
            var sources = LoadSources();
            // excavation is a known skill, but nothing unlocks it at runtime —
            // it was retired when observation replaced digging, and only the
            // validator whitelist still carries it. (This was apothecary until
            // the Mistfen map granted it, then husbandry until the Crags map
            // did. Every zone skill is granted now, so the retired skill is the
            // one example left that cannot quietly evaporate.)
            sources.RecipesJson = sources.RecipesJson.Replace(
                "\"skill\": \"firecraft\",  \"inputs\": { \"fish\": 2 }",
                "\"skill\": \"excavation\", \"inputs\": { \"fish\": 2 }");
            Assert.That(sources.RecipesJson, Does.Contain("excavation"), "the corruption must land, or this test proves nothing");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("never unlockable")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_NonPositiveRecipeInputAmount_IsReported()
        {
            var sources = LoadSources();
            sources.RecipesJson = sources.RecipesJson.Replace(
                "\"inputs\": { \"fish\": 3 },",
                "\"inputs\": { \"fish\": 0 },");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("amount must be positive")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_RecipeStationWithNoBuildingLine_IsReported()
        {
            var sources = LoadSources();
            // A typo'd station id must fail loudly — at runtime it would
            // silently REMOVE the station gate, not break the recipe.
            sources.RecipesJson = sources.RecipesJson.Replace(
                "\"id\": \"cordage\",         \"station\": \"bench\"",
                "\"id\": \"cordage\",         \"station\": \"benchh\"");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("station gate would silently vanish")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_ZeroXpBase_IsReported()
        {
            var sources = LoadSources();
            // base 0 → every rung free → all skills read max level at 0 XP.
            sources.EconomyJson = sources.EconomyJson.Replace(
                "\"base\": 100,",
                "\"base\": 0,");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("xp progression is degenerate")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_FolioEntryNotGathered_IsReported()
        {
            var sources = LoadSources();
            // Pristine specimens only come from haul batches — a non-gathered
            // entry could never be fixed into the Folio.
            sources.FolioJson = sources.FolioJson.Replace(
                "\"herbs\", \"copper-scree\"]",
                "\"herbs\", \"bogus-find\"]");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("'bogus-find' is not a gathered resource")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_MvpZoneWithoutWaystoneText_IsReported()
        {
            var sources = LoadSources();
            // An MVP zone's waystone must have its inscription — a blank one
            // is a hole the player walks into on arrival.
            sources.DialogueJson = sources.DialogueJson.Replace(
                "The river keeps no ledger. Count what you take, for it will not.",
                "");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("MVP zone 'silverrun-river' has no waystone text")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_MvpZoneWithoutVerseText_IsReported()
        {
            var sources = LoadSources();
            sources.DialogueJson = sources.DialogueJson.Replace(
                "Of all you pulled from the water, the river asks the finest back. It knows you have it.",
                "");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("MVP zone 'silverrun-river' has no verse text")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_ZeroedWardenGather_IsReported()
        {
            var sources = LoadSources();
            // Not a tuning value: the warden's hands are a bare node's only
            // route to its first own-resource gift.
            sources.EconomyJson = sources.EconomyJson.Replace(
                "\"gatherPerSecond\": 0.5,",
                "\"gatherPerSecond\": 0,");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("warden.gatherPerSecond must be positive")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_ZeroedAmberSection_IsReported()
        {
            var sources = LoadSources();
            sources.EconomyJson = sources.EconomyJson.Replace(
                "\"perFind\": 2,",
                "\"perFind\": 0,");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("amber values must all be positive")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_BondWithUnknownSource_IsReported()
        {
            var sources = LoadSources();
            sources.BondsJson = sources.BondsJson.Replace(
                "\"source\": { \"type\": \"folioSpread\", \"id\": \"meadow-blooms\" }",
                "\"source\": { \"type\": \"folioSpread\", \"id\": \"lost-set\" }");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("unknown folioSpread 'lost-set'")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_BondWithUnknownRole_IsReported()
        {
            var sources = LoadSources();
            sources.BondsJson = sources.BondsJson.Replace(
                "\"role\": \"carrier\",",
                "\"role\": \"warden\",");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("unknown role 'warden'")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_RiteGeneratorShrinkingDemand_IsReported()
        {
            var sources = LoadSources();
            // d <= 1 would make each Rite CHEAPER than the last while the
            // economy compounds — the gate would stop gating.
            var before = sources.RitesJson;
            sources.RitesJson = sources.RitesJson.Replace(
                "\"demandGrowth\": 1.45,",
                "\"demandGrowth\": 0.9,");
            Assert.That(sources.RitesJson, Is.Not.EqualTo(before), "the corruption must land — retuning d silently no-ops this Replace");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("demandGrowth must exceed 1")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_RiteGeneratorSpotlightDearerThanOffSpotlight_IsReported()
        {
            var sources = LoadSources();
            sources.RitesJson = sources.RitesJson.Replace(
                "\"spotlightDiscount\": 0.6,",
                "\"spotlightDiscount\": 1.4,");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("spotlightDiscount must be in (0, 1]")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_RiteGeneratorBreadthCeilingBelowChooseCount_IsReported()
        {
            var sources = LoadSources();
            var before = sources.RitesJson;
            sources.RitesJson = sources.RitesJson.Replace(
                "\"chooseCountMax\": 5,",
                "\"chooseCountMax\": 2,");
            Assert.That(sources.RitesJson, Is.Not.EqualTo(before), "the corruption must land");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("breadth ramp must not go backwards")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_RepeatableAlmanacLineWithAMultiplicativeEffect_IsReported()
        {
            // Levels scale a repeatable line's effect value LINEARLY, which is
            // only right for the additive bands — a multiplicative type would
            // want value^levels and would otherwise fail silently.
            var sources = LoadSources();
            var before = sources.AlmanacJson;
            sources.AlmanacJson = sources.AlmanacJson.Replace(
                "{ \"type\": \"yieldBonus\", \"skill\": \"all-gathering\", \"value\": 0.05 }",
                "{ \"type\": \"craftSpeedMult\", \"value\": 1.05 }");
            Assert.That(sources.AlmanacJson, Is.Not.EqualTo(before), "the corruption must land");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("may only grant additive effects")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_RepeatableAlmanacLinePricedFlat_IsReported()
        {
            var sources = LoadSources();
            var before = sources.EconomyJson;
            sources.EconomyJson = sources.EconomyJson.Replace(
                "\"almanac\": 1.25,",
                "\"almanac\": 1.0,");
            Assert.That(sources.EconomyJson, Is.Not.EqualTo(before), "the corruption must land");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("costGrowth.almanac must exceed 1")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Parse_ReadsThePaidSkipCap()
        {
            var data = GameData.Parse(LoadSources());

            // The whale throttle (design §10): sim-time is the only thing amber
            // buys, so this one number pins a heavy spender to at most twice a
            // free player's pace (24 natural sim-hours a day + 24 skipped).
            Assert.That(data.Economy.Amber.TimeSkipDailyCapHours, Is.EqualTo(24.0));
        }

        [Test]
        public void Validate_PaidSkipCapBelowOneSkip_IsReported()
        {
            var sources = LoadSources();
            var before = sources.EconomyJson;
            sources.EconomyJson = sources.EconomyJson.Replace(
                "\"timeSkipDailyCapHours\": 24,",
                "\"timeSkipDailyCapHours\": 2,");
            Assert.That(sources.EconomyJson, Is.Not.EqualTo(before), "the corruption must land");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            // The budget refills to the cap and a skip needs timeSkipHours of it,
            // so a positive cap below one skip is a sink that can never be spent.
            Assert.That(issues.Any(i => i.Contains("timeSkipDailyCapHours")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_AlmanacRequiresUnknownNode_IsReported()
        {
            var sources = LoadSources();
            sources.AlmanacJson = sources.AlmanacJson.Replace(
                "\"requires\": \"old-songs-ii\",",
                "\"requires\": \"lost-songs\",");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("requires unknown node 'lost-songs'")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_FreeAlmanacNode_IsReported()
        {
            var sources = LoadSources();
            sources.AlmanacJson = sources.AlmanacJson.Replace(
                "\"costVerdure\": 4,",
                "\"costVerdure\": 0,");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("must cost Verdure")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_GrantUpgradeOutsideTheAlmanac_IsReported()
        {
            // Only the tree that survives the fold may hand out rungs — a rung
            // granting a rung is a cycle waiting to happen.
            var sources = LoadSources();
            var before = sources.UpgradesJson;
            sources.UpgradesJson = sources.UpgradesJson.Replace(
                "{ \"type\": \"sellValueBonus\", \"resource\": \"berries\", \"value\": 0.25 }",
                "{ \"type\": \"grantUpgrade\", \"upgrade\": \"flint-sickle\" }");
            Assert.That(sources.UpgradesJson, Is.Not.EqualTo(before), "the corruption must land");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("only the Almanac may grant ladder rungs")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_GrantOfUnknownUpgrade_IsReported()
        {
            var sources = LoadSources();
            sources.AlmanacJson = sources.AlmanacJson.Replace(
                "\"upgrade\": \"flint-sickle\"",
                "\"upgrade\": \"nonsuch\"");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("grants unknown upgrade 'nonsuch'")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_GrantOfARecruitRung_IsReported()
        {
            // Familiar permanence is Kinship's alone (design §4) — the Almanac
            // never buys creatures, not even sideways through a granted rung.
            var sources = LoadSources();
            var before = sources.AlmanacJson;
            sources.AlmanacJson = sources.AlmanacJson.Replace(
                "\"upgrade\": \"map-bramble\"",
                "\"upgrade\": \"first-friend\"");
            Assert.That(sources.AlmanacJson, Is.Not.EqualTo(before), "the corruption must land");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("recruits a familiar")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_ZoneSkipWithoutItsCoveringTool_IsReported()
        {
            // A map grant whose requires chain doesn't carry the zone's tool
            // would sit inert forever — a bought node that does nothing.
            var sources = LoadSources();
            var before = sources.AlmanacJson;
            sources.AlmanacJson = sources.AlmanacJson.Replace(
                "\"requires\": \"remembered-edge-i\",",
                "");
            Assert.That(sources.AlmanacJson, Is.Not.EqualTo(before), "the corruption must land");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("demands flint tools")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_UnknownRequiredTool_IsReported()
        {
            var sources = LoadSources();
            sources.ZonesJson = sources.ZonesJson.Replace(
                "\"requiredTool\": \"flint\",",
                "\"requiredTool\": \"flintt\",");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("requiredTool 'flintt'")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_RequiredToolNoUpgradeGrants_IsReported()
        {
            var sources = LoadSources();
            // Strip the steel AND deepsteel toolsets' tiers — the highest
            // grantable tier falls below steel and the steel-gated zones
            // become unenterable forever. (Stripping steel alone stopped
            // proving anything once the deepsteel toolset out-ranked it.)
            sources.UpgradesJson = sources.UpgradesJson
                .Replace("\"toolTier\": \"steel\",", "")
                .Replace("\"toolTier\": \"deepsteel\",", "");
            Assert.That(sources.UpgradesJson, Does.Not.Contain("steel\","),
                "the corruption must land, or this test proves nothing");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("can never be met")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_NonPositiveObservationRate_IsReported()
        {
            var sources = LoadSources();
            // Rate 0 with any pity means field sketches only ever arrive on pity —
            // rate 0 AND pity 0 means never; both are authoring mistakes.
            sources.EconomyJson = sources.EconomyJson.Replace(
                "\"baseSketchesPerHour\": 0.25,",
                "\"baseSketchesPerHour\": 0,");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("observation values")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_QualityChancesSummingAboveOne_IsReported()
        {
            var sources = LoadSources();
            // 0.999 + pristine's 0.005 leaves no room for Common in one draw.
            sources.EconomyJson = sources.EconomyJson.Replace(
                "\"fineChance\": 0.035,",
                "\"fineChance\": 0.999,");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("must not sum above 1")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_NonPositiveQualityValueMult_IsReported()
        {
            var sources = LoadSources();
            sources.EconomyJson = sources.EconomyJson.Replace(
                "\"pristineValueMult\": 10,",
                "\"pristineValueMult\": 0,");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("quality value multipliers")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_MaterialRiteOfferingWithoutRenownGrant_IsReported()
        {
            var sources = LoadSources();
            sources.RitesJson = sources.RitesJson.Replace(
                "\"resource\": \"copper-ingot\",   \"amount\": 10,  \"renownGrant\": 750",
                "\"resource\": \"copper-ingot\",   \"amount\": 10");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("needs an explicit renownGrant")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_TypoInStartingZoneUnlocks_IsReported()
        {
            var sources = LoadSources();
            sources.ZonesJson = sources.ZonesJson.Replace(
                "\"unlocks\": [\"foraging\"],",
                "\"unlocks\": [\"forraging\"],");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("is not a known skill")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void ImportedAsset_IsUpToDateWithDesignData()
        {
            var asset = GameDataAsset.LoadFromResources();

            Assert.That(asset.sourceHash, Is.EqualTo(GameData.ComputeSourceHash(LoadSources())),
                "GameData.asset is stale — run Wildgrove > Import Design Data and commit the asset");
        }

        [Test]
        public void ImportedAsset_SurfacesUpgradeGatesAndBuildingMaterials()
        {
            var asset = GameDataAsset.LoadFromResources();

            // Money→XP (design §9): upgrades gate on a skill level + materials, no Coin.
            Assert.That(asset.UpgradesById["flint-sickle"].gateSkill, Is.EqualTo("foraging"));
            Assert.That(asset.UpgradesById["flint-sickle"].gateLevel, Is.EqualTo(2));
            Assert.That(asset.BuildingsById["forge"].materials.Exists(m => m.id == "copper-scree"), Is.True);
        }

        [Test]
        public void Mapper_MapsAuthoringModelToRuntimeShapes()
        {
            var data = GameData.Parse(LoadSources());
            var asset = ScriptableObject.CreateInstance<GameDataAsset>();
            GameDataMapper.Populate(asset, data);

            Assert.That(asset.zones, Has.Count.EqualTo(data.Zones.Count));
            Assert.That(asset.UpgradesById["copper-sickle"].materials.Single(m => m.id == "copper-ingot").amount, Is.EqualTo(5));
            Assert.That(asset.RecipesById["charcoal"].defaultKnown, Is.True);
            Assert.That(asset.dialogue.waystones.Single(w => w.key == "sunfield-meadow").text, Is.Not.Empty);
            Assert.That(asset.economy.xp.baseXp, Is.EqualTo(100d));
            Assert.That(asset.ResourcesById["berries"].sellValue, Is.EqualTo(data.ResourcesById["berries"].SellValue));
            Assert.That(asset.economy.gifts.pileGoods.ToDouble(), Is.EqualTo(10d));
            Assert.That(asset.economy.warden.gatherPerSecond, Is.EqualTo(0.5d));
            Assert.That(asset.ZonesById["sunfield-meadow"].verseSite, Is.EqualTo("the fire circle"));
            Assert.That(asset.rites.chooseCount, Is.EqualTo(3));
            Assert.That(asset.rites.rites.Single().verses, Has.Count.EqualTo(7), "one verse per zone through the Crags");
            Assert.That(asset.dialogue.verses.Single(v => v.key == "sunfield-meadow").text, Is.Not.Empty);
            Assert.That(asset.deepAmber.zoneId, Is.EqualTo("the-hollows"));
            Assert.That(asset.deepAmber.pieces, Has.Count.EqualTo(data.DeepAmber.Pieces.Count));
            Assert.That(asset.deepAmber.pieces.First().lore, Is.Not.Empty);
            Assert.That(asset.deepAmber.effects, Is.Not.Empty);
        }

        [Test]
        public void Validate_RiteSlotWithUnknownResource_IsReported()
        {
            var sources = LoadSources();
            sources.RitesJson = sources.RitesJson.Replace(
                "\"resource\": \"berries\",     \"amount\": 300",
                "\"resource\": \"moon-cheese\", \"amount\": 300");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("moon-cheese")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_MvpZoneWithoutVerseSite_IsReported()
        {
            var sources = LoadSources();
            sources.ZonesJson = sources.ZonesJson.Replace(
                "\"verseSite\": \"the fire circle\",  ",
                "");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("no verseSite")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_GatheredResourceWithoutSellValue_IsReported()
        {
            var sources = LoadSources();
            sources.ResourcesJson = sources.ResourcesJson.Replace(
                "{ \"id\": \"berries\",      \"sellValue\": 1,   \"skill\": \"foraging\" },",
                "");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("berries") && i.Contains("no sell value")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_PricedResourceThatIsNotGathered_IsReported()
        {
            var sources = LoadSources();
            sources.ResourcesJson = sources.ResourcesJson.Replace(
                "{ \"id\": \"berries\",      \"sellValue\": 1,   \"skill\": \"foraging\" },",
                "{ \"id\": \"berries\",      \"sellValue\": 1,   \"skill\": \"foraging\" },\n    { \"id\": \"gold-bar\", \"sellValue\": 999, \"skill\": \"foraging\" },");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("gold-bar") && i.Contains("not gathered")), Is.True, string.Join("\n", issues));
        }
    }
}
