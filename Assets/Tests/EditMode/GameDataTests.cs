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
            Assert.That(data.Upgrades, Has.Count.EqualTo(30), "design doc §9 defines exactly 30 named upgrades");
            Assert.That(data.Recipes, Is.Not.Empty);
            Assert.That(data.Gear, Is.Not.Empty);
            Assert.That(data.Fossils, Is.Not.Empty);
            Assert.That(data.Dialogue.Waystones, Is.Not.Empty);
        }

        [Test]
        public void Parse_RealData_TypedValuesSurvive()
        {
            var data = GameData.Parse(LoadSources());

            Assert.That(data.Economy.CostGrowth.CrewHire, Is.EqualTo(1.09));
            Assert.That(data.Economy.Hires.CrewBaseCoin, Is.EqualTo(10L));
            Assert.That(data.Economy.Tools.Tiers.First(), Is.EqualTo("flint"));
            Assert.That(data.ResourcesById["berries"].SellValue, Is.GreaterThan(0));
            Assert.That(data.ZonesById["sunfield-meadow"].MapCostCoin, Is.EqualTo(0L));
            Assert.That(data.ZonesById["the-hollows"].MapCostCoin, Is.Null, "unpriced zones stay null, not zero");
            Assert.That(data.UpgradesById["copper-sickle"].Materials["copper-ingot"], Is.EqualTo(5));
            Assert.That(data.UpgradesById["flint-sickle"].Effects.Single().Type, Is.EqualTo(EffectType.YieldMult));
            Assert.That(data.RecipesById["bronze-ingot"].Inputs["tin-seam"], Is.EqualTo(2));
            Assert.That(data.FossilsById["those-who-planted"].DigSites, Has.Count.EqualTo(2));
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
            Assert.That(asset.economy.hires.crewBaseCoin.ToDouble(), Is.EqualTo(10d));
        }

        [Test]
        public void Validate_GatheredResourceWithoutSellValue_IsReported()
        {
            var sources = LoadSources();
            sources.ResourcesJson = sources.ResourcesJson.Replace(
                "{ \"id\": \"berries\",      \"sellValue\": 1 },",
                "");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("berries") && i.Contains("no sell value")), Is.True, string.Join("\n", issues));
        }

        [Test]
        public void Validate_PricedResourceThatIsNotGathered_IsReported()
        {
            var sources = LoadSources();
            sources.ResourcesJson = sources.ResourcesJson.Replace(
                "{ \"id\": \"berries\",      \"sellValue\": 1 },",
                "{ \"id\": \"berries\",      \"sellValue\": 1 },\n    { \"id\": \"gold-bar\", \"sellValue\": 999 },");

            var issues = GameDataValidator.Validate(GameData.Parse(sources));

            Assert.That(issues.Any(i => i.Contains("gold-bar") && i.Contains("not gathered")), Is.True, string.Join("\n", issues));
        }
    }
}
