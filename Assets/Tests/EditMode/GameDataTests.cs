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
            Assert.That(data.Upgrades, Has.Count.EqualTo(29),
                "design doc §9 defines 30 named upgrades; Mistfen's trail map is held back until the zone's v1.1 companions (skills, dig site, waystone) ship with it");
            Assert.That(data.Recipes, Is.Not.Empty);
            Assert.That(data.Buildings, Has.Count.EqualTo(5), "design §9 defines the five camp building lines");
            Assert.That(data.Gear, Is.Not.Empty);
            Assert.That(data.Fossils, Is.Not.Empty);
            Assert.That(data.Rites.Rites, Is.Not.Empty);
            Assert.That(data.Bonds, Is.Not.Empty);
            Assert.That(data.Dialogue.Waystones, Is.Not.Empty);
            Assert.That(data.Dialogue.Verses, Is.Not.Empty);
        }

        [Test]
        public void Parse_RealData_TypedValuesSurvive()
        {
            var data = GameData.Parse(LoadSources());

            Assert.That(data.Economy.CostGrowth.GathererGift, Is.EqualTo(1.09));
            Assert.That(data.Economy.Gifts.GathererBaseGoods, Is.EqualTo(10L));
            Assert.That(data.Economy.Tools.Tiers.First(), Is.EqualTo("flint"));
            Assert.That(data.ResourcesById["berries"].SellValue, Is.GreaterThan(0));
            Assert.That(data.ResourcesById["copper-scree"].Skill, Is.EqualTo("mining"));
            Assert.That(data.Economy.Crafting.BaseCraftSeconds, Is.EqualTo(5.0));
            Assert.That(data.Economy.FamiliarCaps.FlockCapBase, Is.EqualTo(8));
            Assert.That(data.BuildingsById["forge"].BaseCostCoin, Is.EqualTo(8000L));
            Assert.That(data.BuildingsById["forge"].MilestoneUpgradeIds, Is.EqualTo(new[] { "bellows-forge" }));
            Assert.That(data.BuildingsById["roosts"].PerLevel.Type, Is.EqualTo("familiarCaps"));
            Assert.That(data.RecipesById["iron-ingot"].StationLevel, Is.EqualTo(2), "iron heat is forge 2");
            Assert.That(data.RecipesById["copper-ingot"].StationLevel, Is.EqualTo(1), "absent stationLevel defaults to 1");
            Assert.That(data.RecipesById["iron-ingot"].SkillLevel, Is.EqualTo(5), "iron smelting waits for forgecraft 5");
            Assert.That(data.RecipesById["copper-ingot"].SkillLevel, Is.EqualTo(1), "absent skillLevel defaults to 1");
            Assert.That(data.Economy.Xp.GatherPerUnit, Is.EqualTo(1.0));
            Assert.That(data.Economy.Xp.CraftPerBatch, Is.EqualTo(25.0));
            Assert.That(data.Economy.Mastery.Base, Is.EqualTo(50.0));
            Assert.That(data.Economy.Mastery.XpPerUnit, Is.EqualTo(0.25));
            Assert.That(data.Economy.Quality.PristineValueMult, Is.EqualTo(10.0));
            Assert.That(data.Economy.Tending.PristineChanceBonus, Is.EqualTo(1.0));
            Assert.That(data.Economy.Excavation.BaseFragmentsPerHour, Is.EqualTo(0.25));
            Assert.That(data.Economy.Amber.DigFindsPerHour, Is.EqualTo(0.06), "the free amber drip from dig sites");
            Assert.That(data.Economy.Amber.TimeSkipHours, Is.EqualTo(4.0));
            Assert.That(data.Economy.Amber.TimeSkipCostAmber, Is.EqualTo(15.0));
            Assert.That(data.UpgradesById["map-oldgrowth"].Effects.Any(e => e.Type == EffectType.UnlockSkill && e.Skill == "excavation"),
                Is.True, "the first dig site's map also teaches excavation");
            Assert.That(data.ZonesById["silverrun-river"].RequiredTool, Is.EqualTo("bronze"));
            Assert.That(data.ZonesById["sunfield-meadow"].RequiredTool, Is.Null, "the starting zone is ungated");
            Assert.That(data.UpgradesById["copper-sickle"].ToolTier, Is.EqualTo("copper"));
            Assert.That(data.AlmanacById["old-songs-ii"].Requires, Is.EqualTo("old-songs-i"));
            Assert.That(data.AlmanacById["long-watch-i"].CostVerdure, Is.EqualTo(2.0));
            Assert.That(data.MuseumSetsById["river-catch"].Entries, Has.Count.EqualTo(4));
            Assert.That(data.Rites.Rites.Single().Verses[1].Slots[2].RenownGrant, Is.EqualTo(375), "material offerings carry an explicit grant");
            Assert.That(data.Rites.Generator.DemandGrowth, Is.EqualTo(2.5), "the run-2+ generator's d in baseQty · d^m");
            Assert.That(data.BondsById["sootwing"].Role, Is.EqualTo("carrier"), "a carrier bonds as a carrier");
            Assert.That(data.BondsById["sootwing"].Source.Type, Is.EqualTo("museumSet"));
            Assert.That(data.BondsById["burr"].Source.Id, Is.EqualTo("old-friend"), "the Almanac-node bond");
            Assert.That(data.AlmanacById["old-friend"].Effects, Is.Empty, "the bond node's promise is the companion, not an effect");
            Assert.That(data.Rites.Generator.SpotlightDiscount, Is.EqualTo(0.6));
            Assert.That(data.Rites.Generator.OffSpotlightPremium, Is.EqualTo(1.5));
            Assert.That(data.ZonesById["sunfield-meadow"].MapCostCoin, Is.EqualTo(0L));
            Assert.That(data.ZonesById["the-hollows"].MapCostCoin, Is.Null, "unpriced zones stay null, not zero");
            Assert.That(data.UpgradesById["copper-sickle"].Materials["copper-ingot"], Is.EqualTo(5));
            Assert.That(data.UpgradesById["flint-sickle"].Effects.Single().Type, Is.EqualTo(EffectType.YieldMult));
            Assert.That(data.RecipesById["bronze-ingot"].Inputs["tin-seam"], Is.EqualTo(2));
            Assert.That(data.FossilsById["those-who-planted"].DigSites, Has.Count.EqualTo(2));
            Assert.That(data.ZonesById["sunfield-meadow"].VerseSite, Is.EqualTo("the fire circle"));
            Assert.That(data.Rites.ChooseCount, Is.EqualTo(3));
            Assert.That(data.Rites.Rites.Single().Verses.First().Slots.First().Type, Is.EqualTo(RiteSlotType.Resource));
            Assert.That(data.Rites.Rites.Single().Verses.First().Slots.Last().Type, Is.EqualTo(RiteSlotType.Specimen));
        }

        [Test]
        public void Parse_UpgradeWithoutMaterials_GetsEmptyDictionary()
        {
            var data = GameData.Parse(LoadSources());

            Assert.That(data.UpgradesById["waxed-satchel"].Materials, Is.Empty);
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
            // the-hollows exists but is staged content — nothing unlocks it.
            sources.RitesJson = sources.RitesJson.Replace(
                "\"zone\": \"silverrun-river\"",
                "\"zone\": \"the-hollows\"");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("the-hollows") && i.Contains("never unlockable")),
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
                "\"type\": \"basketCapacityBonus\"",
                "\"type\": \"frobnicateBonus\"");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("frobnicateBonus")), Is.True, string.Join("\n", issues));
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
        public void Validate_FossilAtNonDigSiteZone_IsReported()
        {
            var sources = LoadSources();
            sources.FossilsJson = sources.FossilsJson.Replace(
                "\"digSites\": [\"old-growth-wood\"],",
                "\"digSites\": [\"sunfield-meadow\"],");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("not a dig site")), Is.True, string.Join("\n", issues));
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
        public void Validate_NonPositiveHaulingValue_IsReported()
        {
            var sources = LoadSources();
            sources.EconomyJson = sources.EconomyJson.Replace(
                "\"tripSeconds\": 10",
                "\"tripSeconds\": 0");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("hauling values must all be positive")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_FossilEffectWithUnknownResource_IsReported()
        {
            var sources = LoadSources();
            sources.FossilsJson = sources.FossilsJson.Replace(
                "{ \"type\": \"pristineChanceBonus\", \"value\": 0.01 }",
                "{ \"type\": \"noSpoilage\", \"resource\": \"bogus-item\" }");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("bogus-item")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_DuplicateUpgradeId_IsReported()
        {
            var sources = LoadSources();
            sources.UpgradesJson = sources.UpgradesJson.Replace(
                "\"id\": \"waxed-satchel\"",
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
                "\"gatherPerUnit\": 1,",
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
            // apothecary is a known skill, but nothing unlocks it at runtime.
            sources.RecipesJson = sources.RecipesJson.Replace(
                "\"skill\": \"firecraft\",  \"inputs\": { \"fish\": 2 }",
                "\"skill\": \"apothecary\", \"inputs\": { \"fish\": 2 }");

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
        public void Validate_MuseumEntryNotGathered_IsReported()
        {
            var sources = LoadSources();
            // Pristine specimens only come from haul batches — a non-gathered
            // entry could never be donated.
            sources.MuseumJson = sources.MuseumJson.Replace(
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
                "\"source\": { \"type\": \"museumSet\", \"id\": \"meadow-blooms\" }",
                "\"source\": { \"type\": \"museumSet\", \"id\": \"lost-set\" }");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("unknown museumSet 'lost-set'")), Is.True, string.Join("\n", issues));
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
            sources.RitesJson = sources.RitesJson.Replace(
                "\"demandGrowth\": 2.5,",
                "\"demandGrowth\": 0.9,");

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
            // Strip the steel toolset's tier — the steel-gated zones become
            // unenterable forever.
            sources.UpgradesJson = sources.UpgradesJson.Replace(
                "\"toolTier\": \"steel\",",
                "");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("can never be met")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_NonPositiveExcavationRate_IsReported()
        {
            var sources = LoadSources();
            // Rate 0 with any pity means fossils only ever arrive on pity —
            // rate 0 AND pity 0 means never; both are authoring mistakes.
            sources.EconomyJson = sources.EconomyJson.Replace(
                "\"baseFragmentsPerHour\": 0.25,",
                "\"baseFragmentsPerHour\": 0,");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("excavation values")), Is.True, string.Join("\n", issues));
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
                "\"resource\": \"copper-ingot\",   \"amount\": 5,   \"renownGrant\": 375",
                "\"resource\": \"copper-ingot\",   \"amount\": 5");

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
        public void ImportedAsset_SurfacesCurrencyAsBigDouble()
        {
            var asset = GameDataAsset.LoadFromResources();

            Assert.That(asset.UpgradesById["flint-sickle"].costCoin.ToDouble(), Is.EqualTo(100d));
            Assert.That(asset.ZonesById["silverrun-river"].mapCostCoin.ToDouble(), Is.EqualTo(320000d));
            Assert.That(asset.ZonesById["the-hollows"].priced, Is.False, "unpriced zones carry priced=false, not a zero cost");
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
            Assert.That(asset.economy.gifts.gathererBaseGoods.ToDouble(), Is.EqualTo(10d));
            Assert.That(asset.economy.warden.gatherPerSecond, Is.EqualTo(0.5d));
            Assert.That(asset.ZonesById["sunfield-meadow"].verseSite, Is.EqualTo("the fire circle"));
            Assert.That(asset.rites.chooseCount, Is.EqualTo(3));
            Assert.That(asset.rites.rites.Single().verses, Has.Count.EqualTo(4));
            Assert.That(asset.dialogue.verses.Single(v => v.key == "sunfield-meadow").text, Is.Not.Empty);
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
