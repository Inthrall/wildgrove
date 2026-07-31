using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins species traits (design §4): each species carries one fixed trait,
    /// applied where its familiar is stationed — the berry specialist at berry
    /// nodes, the hauler on the trail, the wanderer watching the sites, soft paws for
    /// Pristine points. Resting familiars contribute nothing; absent species
    /// data no-ops (fixtures).
    /// </summary>
    public class TraitsTests
    {
        private const double Tolerance = 1e-9;

        private GameDataAsset _data;

        [SetUp]
        public void SetUp()
        {
            _data = ScriptableObject.CreateInstance<GameDataAsset>();
            _data.species = new List<SpeciesData>
            {
                new SpeciesData
                {
                    id = "meadow-vole", displayName = "meadow vole", roleLean = "gatherer",
                    suggestedNames = new List<string> { "Bramble" },
                    trait = new TraitData { displayName = "Meadow-forager", kind = "nodeYieldBonus", value = 0.4, resources = new List<string> { "berries", "wildflowers" } },
                },
                new SpeciesData
                {
                    id = "pack-raven", displayName = "pack raven", roleLean = "carrier",
                    suggestedNames = new List<string> { "Sootwing" },
                    trait = new TraitData { displayName = "Deep pockets", kind = "bubbleRewardBonus", value = 0.25 },
                },
                new SpeciesData
                {
                    id = "tawny-owl", displayName = "tawny owl", roleLean = "gatherer",
                    suggestedNames = new List<string> { "Blink" },
                    trait = new TraitData { displayName = "Patient watcher", kind = "digSpeedBonus", value = 0.4 },
                },
                new SpeciesData
                {
                    id = "ermine", displayName = "ermine", roleLean = "gatherer",
                    suggestedNames = new List<string> { "Sleet" },
                    trait = new TraitData { displayName = "Soft paws", kind = "pristineBonus", value = 0.01 },
                },
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_data);
        }

        private static Familiar At(string speciesId, string stationId)
        {
            return new Familiar { id = "fam-" + speciesId, speciesId = speciesId, stationId = stationId };
        }

        [Test]
        public void NodeYieldFactor_EitherResourceOfThePair_AppliesTheTrait()
        {
            var berries = new NodeState { id = "n1", resourceId = "berries" };
            var wildflowers = new NodeState { id = "n2", resourceId = "wildflowers" };

            Assert.That(Traits.NodeYieldFactor(At("meadow-vole", "n1"), berries, _data),
                Is.EqualTo(1.4).Within(Tolerance), "the first node of the pair");
            Assert.That(Traits.NodeYieldFactor(At("meadow-vole", "n2"), wildflowers, _data),
                Is.EqualTo(1.4).Within(Tolerance), "and the second — one familiar works both");
        }

        [Test]
        public void NodeYieldFactor_OtherResourceOrOtherKind_IsPlain()
        {
            var nuts = new NodeState { id = "n1", resourceId = "nuts" };

            Assert.That(Traits.NodeYieldFactor(At("meadow-vole", "n1"), nuts, _data),
                Is.EqualTo(1.0).Within(Tolerance), "the berry specialist is ordinary at a nut grove");
            Assert.That(Traits.NodeYieldFactor(At("pack-raven", "n1"), nuts, _data),
                Is.EqualTo(1.0).Within(Tolerance), "a bubble trait never touches gathering");
        }

        [Test]
        public void BubbleAndWatchFactors_ApplyTheirKinds()
        {
            var state = new GameState();
            state.roster.Add(At("pack-raven", "n1"));
            state.roster.Add(At("meadow-vole", "n2"));

            Assert.That(Traits.BubbleRewardBonus(state, _data),
                Is.EqualTo(0.25).Within(Tolerance), "the raven fattens windfalls from any post");
            Assert.That(Traits.DigSpeedFactor(At("tawny-owl", Familiar.WanderStation), _data),
                Is.EqualTo(1.4).Within(Tolerance));
        }

        [Test]
        public void BubbleRewardBonus_ARestingRaven_ContributesNothing()
        {
            var state = new GameState();
            state.roster.Add(At("pack-raven", null));

            Assert.That(Traits.BubbleRewardBonus(state, _data), Is.EqualTo(0.0).Within(Tolerance));
        }

        [Test]
        public void PristineBonusAt_CountsOnlyTheStationedSoftPaws()
        {
            var node = new NodeState { id = "n1", resourceId = "berries" };
            var state = new GameState();
            state.roster.Add(At("ermine", "n1"));
            state.roster.Add(At("ermine", null));
            state.roster.Add(At("meadow-vole", "n1"));

            Assert.That(Traits.PristineBonusAt(state, _data, node), Is.EqualTo(0.01).Within(Tolerance),
                "one stationed ermine counts; the resting one and the vole don't");
        }

        /// <summary>Signature config (design §4): milestones at Kinship 2/4/7, each sharpening the trait by +25% of its base value.</summary>
        private void ConfigureSignatures()
        {
            _data.economy = new EconomyData
            {
                familiarXp = new EconomyData.FamiliarXpData
                {
                    baseXp = 60, growth = 1.12, maxLevel = 99, xpPerSecond = 1.0,
                    kinshipDivisor = 100.0, kinshipXpRatePerLevel = 0.02,
                    signatureMilestones = new List<int> { 2, 4, 7 },
                    signatureDeepening = 0.25,
                },
            };
        }

        private static Familiar WithKinship(string speciesId, string stationId, double kinship)
        {
            var familiar = At(speciesId, stationId);
            familiar.kinshipXp = kinship;
            return familiar;
        }

        [Test]
        public void DeepeningFactor_Unconfigured_IsOne()
        {
            Assert.That(Traits.DeepeningFactor(_data, WithKinship("meadow-vole", "n1", 9)),
                Is.EqualTo(1.0).Within(Tolerance), "no signature config, no sharpening");
        }

        [Test]
        public void MilestonesPassedAt_CountsTheThresholdsCrossed()
        {
            ConfigureSignatures();

            Assert.That(Kinship.MilestonesPassedAt(1, _data), Is.EqualTo(0));
            Assert.That(Kinship.MilestonesPassedAt(2, _data), Is.EqualTo(1), "the milestone itself counts");
            Assert.That(Kinship.MilestonesPassedAt(3, _data), Is.EqualTo(1));
            Assert.That(Kinship.MilestonesPassedAt(4, _data), Is.EqualTo(2));
            Assert.That(Kinship.MilestonesPassedAt(7, _data), Is.EqualTo(3));
            Assert.That(Kinship.MilestonesPassedAt(40, _data), Is.EqualTo(3), "the ladder tops out");
        }

        [Test]
        public void NodeYieldFactor_DeepensAtKinshipMilestones()
        {
            ConfigureSignatures();
            var berries = new NodeState { id = "n1", resourceId = "berries" };

            Assert.That(Traits.NodeYieldFactor(WithKinship("meadow-vole", "n1", 1), berries, _data),
                Is.EqualTo(1.4).Within(Tolerance), "below the first milestone the trait is its plain self");
            Assert.That(Traits.NodeYieldFactor(WithKinship("meadow-vole", "n1", 4), berries, _data),
                Is.EqualTo(1.0 + 0.4 * 1.5).Within(Tolerance), "two milestones passed: +40% deepens to +60%");
        }

        [Test]
        public void BubbleAndPristine_DeepenTheSameWay()
        {
            ConfigureSignatures();

            var ravenState = new GameState();
            ravenState.roster.Add(WithKinship("pack-raven", "n1", 7));
            Assert.That(Traits.BubbleRewardBonus(ravenState, _data),
                Is.EqualTo(0.25 * 1.75).Within(Tolerance), "all three milestones deepen the windfall bonus");

            var node = new NodeState { id = "n1", resourceId = "berries" };
            var state = new GameState();
            state.roster.Add(WithKinship("ermine", "n1", 2));

            Assert.That(Traits.PristineBonusAt(state, _data, node),
                Is.EqualTo(0.01 * 1.25).Within(Tolerance), "soft paws sharpen too");
        }

        [Test]
        public void LevelAfterFold_AddsThisRunsConversion()
        {
            ConfigureSignatures();
            var familiar = WithKinship("meadow-vole", "n1", 1);
            familiar.xp = 400.0;

            // floor(√(400 / 100)) = 2 banked on top of the level held.
            Assert.That(Kinship.LevelAfterFold(familiar, _data), Is.EqualTo(3));
            Assert.That(Kinship.MilestonesPassedAt(Kinship.LevelAfterFold(familiar, _data), _data),
                Is.GreaterThan(Kinship.SignatureMilestonesPassed(familiar, _data)),
                "the fold forecast can see the sharpening coming");
        }

        [Test]
        public void InscriptionsEarned_OnePerMilestone_UnauthoredNeverShow()
        {
            ConfigureSignatures();
            _data.species[0].inscriptions = new List<string> { "first line", "second line" };

            Assert.That(Kinship.InscriptionsEarned(WithKinship("meadow-vole", "n1", 1), _data), Is.Empty);
            Assert.That(Kinship.InscriptionsEarned(WithKinship("meadow-vole", "n1", 2), _data),
                Is.EqualTo(new[] { "first line" }));
            Assert.That(Kinship.InscriptionsEarned(WithKinship("meadow-vole", "n1", 7), _data),
                Is.EqualTo(new[] { "first line", "second line" }),
                "three milestones passed but only two lines authored — the third simply never shows");
            Assert.That(Kinship.InscriptionsEarned(WithKinship("pack-raven", "n1", 7), _data), Is.Empty,
                "a species with no authored lines stays silent");
        }

        [Test]
        public void UnknownSpeciesOrBareData_NoOps()
        {
            var node = new NodeState { id = "n1", resourceId = "berries" };

            Assert.That(Traits.Of(_data, At("unknown", "n1")), Is.Null);
            Assert.That(Traits.NodeYieldFactor(At("unknown", "n1"), node, _data), Is.EqualTo(1.0).Within(Tolerance));

            var bare = ScriptableObject.CreateInstance<GameDataAsset>();
            try
            {
                Assert.That(Traits.NodeYieldFactor(At("meadow-vole", "n1"), node, bare),
                    Is.EqualTo(1.0).Within(Tolerance), "hand-built fixture data no-ops");
            }
            finally
            {
                Object.DestroyImmediate(bare);
            }
        }
    }
}
