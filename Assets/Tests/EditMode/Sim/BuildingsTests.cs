using System.Collections.Generic;
using BreakInfinity;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins the camp building lines (design §9): the level maths (bought +
    /// milestone upgrades), the base·1.25^L cost curve, purchase spending,
    /// and what levels grant — station craft speed, basket capacity, and the
    /// roosts line's familiar caps (§8).
    /// </summary>
    public class BuildingsTests
    {
        private const double Tolerance = 1e-9;

        private GameDataAsset _data;

        [SetUp]
        public void SetUp()
        {
            _data = ScriptableObject.CreateInstance<GameDataAsset>();
            _data.economy = new EconomyData
            {
                costGrowth = new EconomyData.CostGrowthData { gathererGift = 1.09, carrierGift = 1.10, building = 1.25 },
                familiarCaps = new EconomyData.FamiliarCapsData
                {
                    flockCapBase = 8, flockCapPerRoostLevel = 2, carrierSlotsBase = 2, carrierSlotsPerRoostLevel = 1,
                },
            };
            _data.upgrades = new List<UpgradeData>
            {
                new UpgradeData { order = 7, id = "camp-fire-ring", costCoin = 1300 },
            };
            _data.buildings = new List<BuildingData>
            {
                new BuildingData
                {
                    id = "fire", displayName = "The Fire", baseCostCoin = 1300,
                    milestoneUpgradeIds = new List<string> { "camp-fire-ring" },
                    perLevel = new BuildingPerLevelData { type = "stationSpeedBonus", station = "fire", value = 0.05 },
                },
                new BuildingData
                {
                    id = "store", displayName = "The Store", baseCostCoin = 3000,
                    perLevel = new BuildingPerLevelData { type = "basketCapacityBonus", value = 0.1 },
                },
                new BuildingData
                {
                    id = "roosts", displayName = "Roosts & Burrows", baseCostCoin = 2000,
                    perLevel = new BuildingPerLevelData { type = "familiarCaps" },
                },
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_data);
        }

        private BuildingData Building(string id)
        {
            return _data.BuildingsById[id];
        }

        [Test]
        public void TotalLevel_CountsBoughtLevelsPlusOwnedMilestones()
        {
            var state = new GameState();
            Assert.That(Buildings.TotalLevel(state, Building("fire")), Is.EqualTo(0));

            state.purchasedUpgradeIds.Add("camp-fire-ring");
            Assert.That(Buildings.TotalLevel(state, Building("fire")), Is.EqualTo(1));

            state.buildingLevels["fire"] = 2;
            Assert.That(Buildings.TotalLevel(state, Building("fire")), Is.EqualTo(3));
        }

        [Test]
        public void NextLevelCost_FollowsTheBuildingCurve()
        {
            var state = new GameState();

            // base · 1.25^L with L = current total level.
            Assert.That(Buildings.NextLevelCost(state, _data, Building("roosts")).ToDouble(),
                Is.EqualTo(2000.0).Within(Tolerance));

            state.buildingLevels["roosts"] = 1;
            Assert.That(Buildings.NextLevelCost(state, _data, Building("roosts")).ToDouble(),
                Is.EqualTo(2500.0).Within(Tolerance));

            state.buildingLevels["roosts"] = 2;
            Assert.That(Buildings.NextLevelCost(state, _data, Building("roosts")).ToDouble(),
                Is.EqualTo(3125.0).Within(Tolerance));
        }

        [Test]
        public void NextLevelCost_MilestoneLevelsRaiseTheCurveToo()
        {
            var state = new GameState();
            state.purchasedUpgradeIds.Add("camp-fire-ring");

            // Fire is already level 1 via its milestone: the first bought level
            // costs base · 1.25^1.
            Assert.That(Buildings.NextLevelCost(state, _data, Building("fire")).ToDouble(),
                Is.EqualTo(1625.0).Within(Tolerance));
        }

        [Test]
        public void TryBuyLevel_SpendsCoinAndRecordsTheLevel()
        {
            var state = new GameState { coin = new BigDouble(2500.0) };

            var bought = Buildings.TryBuyLevel(state, _data, Building("roosts"));

            Assert.That(bought, Is.True);
            Assert.That(state.coin.ToDouble(), Is.EqualTo(500.0).Within(Tolerance));
            Assert.That(Buildings.BoughtLevels(state, "roosts"), Is.EqualTo(1));
        }

        [Test]
        public void TryBuyLevel_WhenCoinShort_LeavesStateUnchanged()
        {
            var state = new GameState { coin = new BigDouble(1999.0) };

            Assert.That(Buildings.TryBuyLevel(state, _data, Building("roosts")), Is.False);
            Assert.That(state.coin.ToDouble(), Is.EqualTo(1999.0).Within(Tolerance));
            Assert.That(Buildings.BoughtLevels(state, "roosts"), Is.EqualTo(0));
        }

        [Test]
        public void StationSpeedMultiplier_CountsBoughtLevelsOnly()
        {
            var state = new GameState();
            state.purchasedUpgradeIds.Add("camp-fire-ring");

            // The milestone grants the station, not the speed taper.
            Assert.That(Buildings.StationSpeedMultiplier(state, _data, "fire"), Is.EqualTo(1.0).Within(Tolerance));

            state.buildingLevels["fire"] = 2;
            Assert.That(Buildings.StationSpeedMultiplier(state, _data, "fire"), Is.EqualTo(1.1).Within(Tolerance));
            Assert.That(Buildings.StationSpeedMultiplier(state, _data, "bench"), Is.EqualTo(1.0).Within(Tolerance));
        }

        [Test]
        public void BasketCapacityMultiplier_GrowsWithStoreLevels()
        {
            var state = new GameState();
            Assert.That(Buildings.BasketCapacityMultiplier(state, _data), Is.EqualTo(1.0).Within(Tolerance));

            state.buildingLevels["store"] = 3;
            Assert.That(Buildings.BasketCapacityMultiplier(state, _data), Is.EqualTo(1.3).Within(Tolerance));
        }

        [Test]
        public void FamiliarCaps_RaisedByRoostLevels()
        {
            var state = new GameState();
            Assert.That(Buildings.FlockCap(state, _data), Is.EqualTo(8));
            Assert.That(Buildings.CarrierSlots(state, _data), Is.EqualTo(2));

            state.buildingLevels["roosts"] = 2;
            Assert.That(Buildings.FlockCap(state, _data), Is.EqualTo(12));
            Assert.That(Buildings.CarrierSlots(state, _data), Is.EqualTo(4));
        }

        [Test]
        public void FamiliarCaps_WithoutCapData_AreUnlimited()
        {
            _data.economy.familiarCaps = null;
            var state = new GameState();

            Assert.That(Buildings.FlockCap(state, _data), Is.EqualTo(int.MaxValue));
            Assert.That(Buildings.CarrierSlots(state, _data), Is.EqualTo(int.MaxValue));
        }
    }
}
