using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins design §6 observation: trail maps open observation sites, stationed
    /// watchers record field sketches (rate rolls + the pity guarantee, both
    /// from the run's saved rng), sketches complete an insect plate whose
    /// permanent effects go live at once, and a fully-recorded site falls quiet.
    /// </summary>
    public class ObservationTests
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
                offline = new EconomyData.OfflineData { baseCapHours = 8, rateMultiplier = 1.0 },
                costGrowth = new EconomyData.CostGrowthData { gathererGift = 1.09, carrierGift = 1.10 },
                gifts = new EconomyData.GiftsData { gathererBaseGoods = 10, carrierBaseGoods = 8 },
                // 3600/h → a certain drop every 1 s sub-step; tests that need
                // the pity path dial this down to (effectively) zero instead.
                observation = new EconomyData.ObservationData { pityTimerHoursWatched = 4, baseSketchesPerHour = 3600.0 },
            };
            _data.zones = new List<ZoneData>
            {
                new ZoneData
                {
                    id = GameStateFactory.StartingZoneId,
                    order = 1,
                    resources = new List<string> { "berries" },
                    unlocks = new List<string> { "foraging" },
                },
                new ZoneData
                {
                    id = "old-growth-wood",
                    order = 3,
                    resources = new List<string> { "timber", "mushrooms" },
                    unlocks = new List<string> { "logging" },
                    digSite = true,
                },
            };
            _data.upgrades = new List<UpgradeData>
            {
                new UpgradeData
                {
                    order = 11, id = "map-oldgrowth",
                    effects =
                    {
                        new EffectData { type = EffectType.UnlockZone, zone = "old-growth-wood" },
                        new EffectData { type = EffectType.UnlockDigSite, zone = "old-growth-wood" },
                    },
                },
                new UpgradeData
                {
                    order = 23, id = "brush-screens",
                    effects = { new EffectData { type = EffectType.DigSpeedMult, value = 2 } },
                },
            };
            _data.insects = new List<InsectData>
            {
                new InsectData
                {
                    id = "stags-herald", displayName = "The Antler Crown",
                    sketches = 3, habitats = new List<string> { "old-growth-wood" }, rarity = 1.0,
                    effects = { new EffectData { type = EffectType.YieldBonus, skill = "all", value = 0.10 } },
                },
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_data);
        }

        private GameState NewGameWithDigSite()
        {
            var state = GameStateFactory.NewGame(_data);
            Upgrades.TryPurchase(state, _data, _data.upgrades[0]);
            return state;
        }

        [Test]
        public void TryPurchase_UnlockDigSite_OpensTheSiteEmpty()
        {
            var state = NewGameWithDigSite();

            Assert.That(state.digSites, Has.Count.EqualTo(1));
            Assert.That(state.digSites[0].zoneId, Is.EqualTo("old-growth-wood"));
            // Sites open empty — a digger is stationed there, never seeded.
            Assert.That(Stationing.AtDigSite(state, "old-growth-wood"), Is.EqualTo(0));
        }

        [Test]
        public void Advance_WatcherAtACertainRate_SurfacesASketch()
        {
            var state = NewGameWithDigSite();
            TestCrew.Station(state, Familiar.DigStationPrefix + "old-growth-wood", 1);

            Simulation.Advance(state, _data, 1.0);

            Assert.That(Insects.SketchCount(state, "stags-herald"), Is.EqualTo(1));
            Assert.That(state.digSites[0].pityHours, Is.EqualTo(0.0).Within(Tolerance), "a find resets the pity timer");
        }

        [Test]
        public void Advance_NoWatchers_SurfacesNothing()
        {
            var state = NewGameWithDigSite();

            Simulation.Advance(state, _data, 10.0);

            Assert.That(Insects.SketchCount(state, "stags-herald"), Is.EqualTo(0));
            Assert.That(state.digSites[0].pityHours, Is.EqualTo(0.0).Within(Tolerance));
        }

        [Test]
        public void Advance_PityTimer_GuaranteesASketchInAQuietSite()
        {
            // Effectively-zero rate: only the pity guarantee can drop.
            _data.economy.observation.baseSketchesPerHour = 1e-12;
            var state = NewGameWithDigSite();
            TestCrew.Station(state, Familiar.DigStationPrefix + "old-growth-wood", 1);

            Simulation.Advance(state, _data, 4.5 * 3600.0);

            // One pity find at the 4-hour mark; the next is half an hour in.
            // (Tolerance spans one 1 s sub-step of FP accumulation drift.)
            Assert.That(Insects.SketchCount(state, "stags-herald"), Is.EqualTo(1));
            Assert.That(state.digSites[0].pityHours, Is.EqualTo(0.5).Within(1e-3));
        }

        [Test]
        public void Advance_CompletingAPlate_GrantsItsEffectsImmediately()
        {
            var state = NewGameWithDigSite();
            TestCrew.Station(state, Familiar.DigStationPrefix + "old-growth-wood", 1);
            state.insectSketches["stags-herald"] = 2;

            Simulation.Advance(state, _data, 1.0);

            Assert.That(Insects.IsRecorded(state, _data.insects[0]), Is.True);
            // The Antler Crown's +10% all yields lands on every node at once.
            foreach (var node in state.nodes)
            {
                Assert.That(node.yieldMultiplier, Is.EqualTo(1.1).Within(Tolerance),
                    "insect yieldBonus should fold into " + node.id);
            }
        }

        [Test]
        public void Advance_FullyRecordedSite_FallsQuietWithoutBurningRng()
        {
            var state = NewGameWithDigSite();
            TestCrew.Station(state, Familiar.DigStationPrefix + "old-growth-wood", 1);
            state.insectSketches["stags-herald"] = 3; // already assembled
            var seedBefore = state.rngState;

            Simulation.Advance(state, _data, 10.0);

            Assert.That(Insects.SketchCount(state, "stags-herald"), Is.EqualTo(3));
            Assert.That(state.digSites[0].pityHours, Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(state.rngState, Is.EqualTo(seedBefore));
        }

        [Test]
        public void Advance_SketchesGoOnlyToUnrecordedPlates()
        {
            // High enough that even the 0.35-rarity stratum is a certain drop.
            _data.economy.observation.baseSketchesPerHour = 36000.0;
            _data.insects.Add(new InsectData
            {
                id = "those-who-sow", displayName = "Those Who Planted",
                sketches = 5, habitats = new List<string> { "old-growth-wood" }, rarity = 0.35,
            });
            var state = NewGameWithDigSite();
            TestCrew.Station(state, Familiar.DigStationPrefix + "old-growth-wood", 1);
            state.insectSketches["stags-herald"] = 3; // assembled — out of the pick

            Simulation.Advance(state, _data, 1.0);

            Assert.That(Insects.SketchCount(state, "those-who-sow"), Is.EqualTo(1));
            Assert.That(Insects.SketchCount(state, "stags-herald"), Is.EqualTo(3));
        }

        [Test]
        public void RecordedPlate_PristineChanceBonus_JoinsTheQualityChance()
        {
            _data.economy.quality = new EconomyData.QualityData
            {
                fineChance = 0.035, fineValueMult = 1.5, pristineBaseChance = 0.005, pristineValueMult = 10.0,
            };
            _data.insects[0].effects.Add(new EffectData { type = EffectType.PristineChanceBonus, value = 0.01 });
            var state = NewGameWithDigSite();
            state.insectSketches["stags-herald"] = 3;

            var chance = Quality.PristineChance(state, _data, state.nodes[0]);

            // 0.5% base + the insect's 1pt — same additive band as upgrades.
            Assert.That(chance, Is.EqualTo(0.015).Within(Tolerance));
        }

        [Test]
        public void DigSpeedMultiplier_OwnedUpgradesMultiply()
        {
            var state = GameStateFactory.NewGame(_data);
            Assert.That(Upgrades.DigSpeedMultiplier(state, _data), Is.EqualTo(1.0).Within(Tolerance));

            state.purchasedUpgradeIds.Add("brush-screens");
            Assert.That(Upgrades.DigSpeedMultiplier(state, _data), Is.EqualTo(2.0).Within(Tolerance));
        }

        [Test]
        public void StationedWatchers_CountTowardTheSite()
        {
            var state = NewGameWithDigSite();
            Assert.That(Stationing.AtDigSite(state, "old-growth-wood"), Is.EqualTo(0));

            // Diggers are stationed (design §2) — no cost, no cap, just where the crew stands.
            TestCrew.Station(state, Familiar.DigStationPrefix + "old-growth-wood", 2);

            Assert.That(Stationing.AtDigSite(state, "old-growth-wood"), Is.EqualTo(2));
        }
    }
}
