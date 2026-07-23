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
                    trait = new TraitData { displayName = "Berry-wise", kind = "nodeYieldBonus", value = 0.4, resource = "berries" },
                },
                new SpeciesData
                {
                    id = "pack-raven", displayName = "pack raven", roleLean = "carrier",
                    suggestedNames = new List<string> { "Sootwing" },
                    trait = new TraitData { displayName = "Deep pockets", kind = "trailThroughputBonus", value = 0.25 },
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
        public void NodeYieldFactor_MatchingResource_AppliesTheTrait()
        {
            var berries = new NodeState { id = "n1", resourceId = "berries" };

            Assert.That(Traits.NodeYieldFactor(At("meadow-vole", "n1"), berries, _data),
                Is.EqualTo(1.4).Within(Tolerance));
        }

        [Test]
        public void NodeYieldFactor_OtherResourceOrOtherKind_IsPlain()
        {
            var nuts = new NodeState { id = "n1", resourceId = "nuts" };

            Assert.That(Traits.NodeYieldFactor(At("meadow-vole", "n1"), nuts, _data),
                Is.EqualTo(1.0).Within(Tolerance), "the berry specialist is ordinary at a nut grove");
            Assert.That(Traits.NodeYieldFactor(At("pack-raven", "n1"), nuts, _data),
                Is.EqualTo(1.0).Within(Tolerance), "a trail trait never touches gathering");
        }

        [Test]
        public void TrailAndWatchFactors_ApplyTheirKinds()
        {
            Assert.That(Traits.TrailThroughputFactor(At("pack-raven", Familiar.TrailStation), _data),
                Is.EqualTo(1.25).Within(Tolerance));
            Assert.That(Traits.TrailThroughputFactor(At("meadow-vole", Familiar.TrailStation), _data),
                Is.EqualTo(1.0).Within(Tolerance), "any familiar can hold the trail, plainly");
            Assert.That(Traits.DigSpeedFactor(At("tawny-owl", Familiar.WanderStation), _data),
                Is.EqualTo(1.4).Within(Tolerance));
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
