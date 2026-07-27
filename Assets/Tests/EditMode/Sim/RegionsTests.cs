using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins the region modifiers (design §8): run 1 is home ground, runs 2+
    /// draw a region deterministically from the migration count alone (no
    /// persistence, no reroll on reload), the region's effects join the
    /// active-effect union — per-resource yields, site speed — and the Rite
    /// generator scales its demands by the region's weight (§9
    /// modifierWeight). Unauthored regions leave everything untouched.
    /// </summary>
    public class RegionsTests
    {
        private const double Tolerance = 1e-9;

        private GameDataAsset _data;

        [SetUp]
        public void SetUp()
        {
            _data = ScriptableObject.CreateInstance<GameDataAsset>();
            _data.economy = new EconomyData
            {
                mastery = new EconomyData.MasteryData { yieldBonusPerLevel = 0.05 },
                verdure = new EconomyData.VerdureData { yieldBonusPerPoint = 0.02 },
                offline = new EconomyData.OfflineData { baseCapHours = 4, rateMultiplier = 1.0 },
            };
            _data.zones = new List<ZoneData>
            {
                new ZoneData
                {
                    id = GameStateFactory.StartingZoneId,
                    order = 1,
                    resources = new List<string> { "berries", "wildflowers" },
                    unlocks = new List<string> { "foraging" },
                },
            };
            _data.regions = new List<RegionData>
            {
                new RegionData
                {
                    id = "lush",
                    displayName = "a lush region",
                    sign = "Green past the waterline.",
                    effects = new List<EffectData>
                    {
                        new EffectData { type = EffectType.YieldMult, resource = "berries", value = 2.0 },
                    },
                },
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_data);
        }

        private GameState StateAtMigration(int migration)
        {
            var state = new GameState { migrationCount = migration };
            state.nodes.Add(new NodeState { id = "n1", zoneId = GameStateFactory.StartingZoneId, resourceId = "berries", skill = "foraging", yieldMultiplier = 1.0 });
            state.nodes.Add(new NodeState { id = "n2", zoneId = GameStateFactory.StartingZoneId, resourceId = "wildflowers", skill = "foraging", yieldMultiplier = 1.0 });
            return state;
        }

        [Test]
        public void ForMigration_RunOne_IsHomeGround()
        {
            Assert.That(Regions.ForMigration(_data, 0), Is.Null, "run 1 is unmodified — the authored Rite assumes it");
        }

        [Test]
        public void ForMigration_IsDeterministic()
        {
            for (var migration = 1; migration <= 8; migration++)
            {
                var first = Regions.ForMigration(_data, migration);
                var again = Regions.ForMigration(_data, migration);

                Assert.That(first, Is.Not.Null, "every run 2+ wakes in a region");
                Assert.That(again, Is.SameAs(first), "a reload can never reroll the season");
            }
        }

        [Test]
        public void ForMigration_Unauthored_IsNull()
        {
            _data.regions = new List<RegionData>();

            Assert.That(Regions.ForMigration(_data, 3), Is.Null);
            Assert.That(Regions.Configured(_data), Is.False);
        }

        [Test]
        public void CurrentAndNext_ReadTheMigrationCount()
        {
            var state = StateAtMigration(2);

            Assert.That(Regions.Current(state, _data), Is.SameAs(Regions.ForMigration(_data, 2)));
            Assert.That(Regions.Next(state, _data), Is.SameAs(Regions.ForMigration(_data, 3)),
                "the fold forecast previews the draw one fold ahead");
        }

        [Test]
        public void RegionYield_FoldsIntoTheTargetedNodeOnly()
        {
            var state = StateAtMigration(1);
            Upgrades.RecomputeYieldMultipliers(state, _data);

            Assert.That(state.nodes[0].yieldMultiplier, Is.EqualTo(2.0).Within(Tolerance),
                "the lush season doubles the berry node");
            Assert.That(state.nodes[1].yieldMultiplier, Is.EqualTo(1.0).Within(Tolerance),
                "the resource grain never bleeds onto the neighbour");
        }

        [Test]
        public void HomeGround_LeavesEveryNodePlain()
        {
            var state = StateAtMigration(0);
            Upgrades.RecomputeYieldMultipliers(state, _data);

            Assert.That(state.nodes[0].yieldMultiplier, Is.EqualTo(1.0).Within(Tolerance));
        }

        [Test]
        public void RegionDigSpeed_FlowsThroughTheSnapshot()
        {
            _data.regions[0].effects = new List<EffectData>
            {
                new EffectData { type = EffectType.DigSpeedMult, value = 1.5 },
            };

            Assert.That(Upgrades.DigSpeedMultiplier(StateAtMigration(1), _data), Is.EqualTo(1.5).Within(Tolerance),
                "an ashen season steadies every observation site");
            Assert.That(Upgrades.DigSpeedMultiplier(StateAtMigration(0), _data), Is.EqualTo(1.0).Within(Tolerance));
        }

        [Test]
        public void DemandWeight_ReadsTheTargetedYieldMult()
        {
            var region = _data.regions[0];

            Assert.That(Regions.DemandWeight(region, "berries"), Is.EqualTo(2.0).Within(Tolerance));
            Assert.That(Regions.DemandWeight(region, "wildflowers"), Is.EqualTo(1.0).Within(Tolerance));
            Assert.That(Regions.DemandWeight(null, "berries"), Is.EqualTo(1.0).Within(Tolerance), "home ground weighs nothing");
        }

        [Test]
        public void Generator_ScalesDemandByTheRegionWeight()
        {
            // One zone, one raw find, no recipes: the generated goods slot can
            // only pick berries, so the region's ×2 weight must double what
            // the verse asks for — §9's verseDemand · modifierWeight.
            _data.resources = new List<ResourceData>
            {
                new ResourceData { id = "berries", sellValue = 2, skill = "foraging" },
                new ResourceData { id = "wildflowers", sellValue = 2, skill = "foraging" },
            };
            _data.rites = new RitesBundle
            {
                chooseCount = 1,
                generator = new RiteGeneratorConfigData { demandGrowth = 2.0, spotlightDiscount = 0.5, offSpotlightPremium = 1.5 },
                rites = new List<RiteData>
                {
                    new RiteData
                    {
                        id = "first-rite",
                        migration = 0,
                        verses =
                        {
                            new RiteVerseData
                            {
                                id = "verse-sunfield",
                                zone = GameStateFactory.StartingZoneId,
                                spotlight = new List<string> { "foraging" },
                                slots =
                                {
                                    new RiteSlotData { type = RiteSlotType.Resource, resource = "berries", amount = 100 },
                                    new RiteSlotData { type = RiteSlotType.Deed, deed = "tend", count = 10, renownGrant = 40 },
                                },
                            },
                        },
                    },
                },
            };
            _data.regions[0].effects = new List<EffectData>
            {
                new EffectData { type = EffectType.YieldMult, resource = "berries", value = 2.0 },
                new EffectData { type = EffectType.YieldMult, resource = "wildflowers", value = 2.0 },
            };

            var flavoured = RiteGenerator.Generate(_data, 1).verses[0];

            _data.regions = new List<RegionData>();
            var plain = RiteGenerator.Generate(_data, 1).verses[0];

            Assert.That(flavoured.slots[0].amount, Is.EqualTo(plain.slots[0].amount * 2),
                "the land asks more of what the season gives freely");
            Assert.That(flavoured.slots[1].count, Is.EqualTo(plain.slots[1].count),
                "deed slots price in taps, not goods — the season never touches them");
        }
    }
}
