using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins design §5 excavation: trail maps open dig sites, gifted diggers
    /// surface fossil fragments (rate rolls + the pity guarantee, both from
    /// the run's saved rng), fragments assemble fossils whose permanent
    /// effects go live at once, and a fully-dug site falls quiet.
    /// </summary>
    public class ExcavationTests
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
                excavation = new EconomyData.ExcavationData { pityTimerHoursDug = 4, baseFragmentsPerHour = 3600.0 },
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
                    order = 11, id = "map-oldgrowth", costCoin = 100,
                    effects =
                    {
                        new EffectData { type = EffectType.UnlockZone, zone = "old-growth-wood" },
                        new EffectData { type = EffectType.UnlockDigSite, zone = "old-growth-wood" },
                    },
                },
                new UpgradeData
                {
                    order = 23, id = "brush-screens", costCoin = 100,
                    effects = { new EffectData { type = EffectType.DigSpeedMult, value = 2 } },
                },
            };
            _data.fossils = new List<FossilData>
            {
                new FossilData
                {
                    id = "antler-crown", displayName = "The Antler Crown",
                    fragments = 3, digSites = new List<string> { "old-growth-wood" }, strataRarity = 1.0,
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
            state.coin = 100;
            Upgrades.TryPurchase(state, _data, _data.upgrades[0]);
            return state;
        }

        [Test]
        public void TryPurchase_UnlockDigSite_OpensTheSiteEmpty()
        {
            var state = NewGameWithDigSite();

            Assert.That(state.digSites, Has.Count.EqualTo(1));
            Assert.That(state.digSites[0].zoneId, Is.EqualTo("old-growth-wood"));
            // No regional seed for the soil — the first digger is a gift.
            Assert.That(state.digSites[0].familiarCount, Is.EqualTo(0));
        }

        [Test]
        public void Advance_DiggerAtACertainRate_SurfacesAFragment()
        {
            var state = NewGameWithDigSite();
            state.digSites[0].familiarCount = 1;

            Simulation.Advance(state, _data, 1.0);

            Assert.That(Fossils.FragmentCount(state, "antler-crown"), Is.EqualTo(1));
            Assert.That(state.digSites[0].pityHours, Is.EqualTo(0.0).Within(Tolerance), "a find resets the pity timer");
        }

        [Test]
        public void Advance_NoDiggers_SurfacesNothing()
        {
            var state = NewGameWithDigSite();

            Simulation.Advance(state, _data, 10.0);

            Assert.That(Fossils.FragmentCount(state, "antler-crown"), Is.EqualTo(0));
            Assert.That(state.digSites[0].pityHours, Is.EqualTo(0.0).Within(Tolerance));
        }

        [Test]
        public void Advance_PityTimer_GuaranteesAFragmentInDryGround()
        {
            // Effectively-zero rate: only the pity guarantee can drop.
            _data.economy.excavation.baseFragmentsPerHour = 1e-12;
            var state = NewGameWithDigSite();
            state.digSites[0].familiarCount = 1;

            Simulation.Advance(state, _data, 4.5 * 3600.0);

            // One pity find at the 4-hour mark; the next is half an hour in.
            // (Tolerance spans one 1 s sub-step of FP accumulation drift.)
            Assert.That(Fossils.FragmentCount(state, "antler-crown"), Is.EqualTo(1));
            Assert.That(state.digSites[0].pityHours, Is.EqualTo(0.5).Within(1e-3));
        }

        [Test]
        public void Advance_CompletingAFossil_GrantsItsEffectsImmediately()
        {
            var state = NewGameWithDigSite();
            state.digSites[0].familiarCount = 1;
            state.fossilFragments["antler-crown"] = 2;

            Simulation.Advance(state, _data, 1.0);

            Assert.That(Fossils.IsComplete(state, _data.fossils[0]), Is.True);
            // The Antler Crown's +10% all yields lands on every node at once.
            foreach (var node in state.nodes)
            {
                Assert.That(node.yieldMultiplier, Is.EqualTo(1.1).Within(Tolerance),
                    "fossil yieldBonus should fold into " + node.id);
            }
        }

        [Test]
        public void Advance_FullyDugSite_FallsQuietWithoutBurningRng()
        {
            var state = NewGameWithDigSite();
            state.digSites[0].familiarCount = 1;
            state.fossilFragments["antler-crown"] = 3; // already assembled
            var seedBefore = state.rngState;

            Simulation.Advance(state, _data, 10.0);

            Assert.That(Fossils.FragmentCount(state, "antler-crown"), Is.EqualTo(3));
            Assert.That(state.digSites[0].pityHours, Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(state.rngState, Is.EqualTo(seedBefore));
        }

        [Test]
        public void Advance_FragmentsGoOnlyToIncompleteFossils()
        {
            // High enough that even the 0.35-rarity stratum is a certain drop.
            _data.economy.excavation.baseFragmentsPerHour = 36000.0;
            _data.fossils.Add(new FossilData
            {
                id = "those-who-planted", displayName = "Those Who Planted",
                fragments = 5, digSites = new List<string> { "old-growth-wood" }, strataRarity = 0.35,
            });
            var state = NewGameWithDigSite();
            state.digSites[0].familiarCount = 1;
            state.fossilFragments["antler-crown"] = 3; // assembled — out of the pick

            Simulation.Advance(state, _data, 1.0);

            Assert.That(Fossils.FragmentCount(state, "those-who-planted"), Is.EqualTo(1));
            Assert.That(Fossils.FragmentCount(state, "antler-crown"), Is.EqualTo(3));
        }

        [Test]
        public void CompletedFossil_PristineChanceBonus_JoinsTheQualityChance()
        {
            _data.economy.quality = new EconomyData.QualityData
            {
                fineChance = 0.035, fineValueMult = 1.5, pristineBaseChance = 0.005, pristineValueMult = 10.0,
            };
            _data.fossils[0].effects.Add(new EffectData { type = EffectType.PristineChanceBonus, value = 0.01 });
            var state = NewGameWithDigSite();
            state.fossilFragments["antler-crown"] = 3;

            var chance = Quality.PristineChance(state, _data, state.nodes[0]);

            // 0.5% base + the fossil's 1pt — same additive band as upgrades.
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
        public void TryGiftDigger_SpendsTheZoneBundle()
        {
            var state = NewGameWithDigSite();
            state.AddResource("timber", 25);
            state.AddResource("mushrooms", 25);

            var gifted = Economy.TryGiftDigger(state, _data, state.digSites[0]);

            // 10 of EACH of the zone's finds (base · 1.09^0) — depth pricing
            // per site, breadth across the zone's ground.
            Assert.That(gifted, Is.True);
            Assert.That(state.digSites[0].familiarCount, Is.EqualTo(1));
            Assert.That(state.GetResource("timber").ToDouble(), Is.EqualTo(15.0).Within(Tolerance));
            Assert.That(state.GetResource("mushrooms").ToDouble(), Is.EqualTo(15.0).Within(Tolerance));
        }

        [Test]
        public void TryGiftDigger_ShortOnAnyBundleResource_Refuses()
        {
            var state = NewGameWithDigSite();
            state.AddResource("timber", 25);
            state.AddResource("mushrooms", 5); // short

            Assert.That(Economy.TryGiftDigger(state, _data, state.digSites[0]), Is.False);
            Assert.That(state.digSites[0].familiarCount, Is.EqualTo(0));
            Assert.That(state.GetResource("timber").ToDouble(), Is.EqualTo(25.0).Within(Tolerance));
        }

        [Test]
        public void TryGiftDigger_DiggersShareTheZoneFlockCap()
        {
            _data.economy.familiarCaps = new EconomyData.FamiliarCapsData
            {
                flockCapBase = 1, flockCapPerRoostLevel = 1, carrierSlotsBase = 2, carrierSlotsPerRoostLevel = 1,
            };
            var state = NewGameWithDigSite();
            state.AddResource("timber", 100);
            state.AddResource("mushrooms", 100);

            // The zone's regional seed gatherer already fills the cap of 1.
            Assert.That(Economy.CanGiftDigger(state, _data, state.digSites[0]), Is.False);
            Assert.That(Economy.TryGiftDigger(state, _data, state.digSites[0]), Is.False);
        }
    }
}
