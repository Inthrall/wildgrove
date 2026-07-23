using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins the warden's kit (design §4): crafting a piece spends its
    /// materials and wears it at once, slots hold one piece each (a new craft
    /// replaces the old), the craft skill gates the making, and worn effects
    /// join the run's accumulators — the burst, the haul load, the offline cap.
    /// </summary>
    public class GearTests
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
                tending = new EconomyData.TendingData
                {
                    burstYieldMult = 3.0, burstDurationSec = 5.0,
                    pristineBonusDurationSec = 30.0, pristineChanceBonus = 1.0,
                },
            };
            _data.zones = new List<ZoneData>
            {
                new ZoneData
                {
                    id = GameStateFactory.StartingZoneId,
                    order = 1,
                    resources = new List<string> { "berries" },
                    unlocks = new List<string> { "foraging", "bushcraft" },
                },
            };
            _data.gear = new List<GearData>
            {
                new GearData
                {
                    id = "cordage-wraps", displayName = "Cordage Wraps", slot = "hands", skill = "bushcraft",
                    materials = { new ItemAmount { id = "fibres", amount = 40 } },
                    effects = { new EffectData { type = EffectType.TendingBurstBonus, value = 0.5 } },
                },
                new GearData
                {
                    id = "birch-frame-pack", displayName = "Birch Frame Pack", slot = "pack", skill = "bushcraft",
                    materials = { new ItemAmount { id = "timber", amount = 25 } },
                    effects = { new EffectData { type = EffectType.CarrierCapacityBonus, value = 0.25 } },
                },
                new GearData
                {
                    id = "oilskin-tarp", displayName = "Oilskin Tarp", slot = "camp", skill = "bushcraft",
                    materials = { new ItemAmount { id = "reeds", amount = 30 } },
                    effects = { new EffectData { type = EffectType.OfflineCapBonusHours, value = 2 } },
                },
                new GearData
                {
                    id = "waxed-canopy", displayName = "Waxed Canopy", slot = "camp", skill = "bushcraft",
                    materials = { new ItemAmount { id = "reeds", amount = 10 } },
                    effects = { new EffectData { type = EffectType.OfflineCapBonusHours, value = 1 } },
                },
                new GearData
                {
                    id = "pitch-torch", displayName = "Pitch Torch", slot = "camp", skill = "firecraft",
                    materials = { new ItemAmount { id = "timber", amount = 10 } },
                    effects = { new EffectData { type = EffectType.OfflineNightFullRate } },
                },
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_data);
        }

        [Test]
        public void TryCraft_SpendsMaterialsAndWearsThePiece()
        {
            var state = GameStateFactory.NewGame(_data);
            state.AddResource("fibres", 50);

            var crafted = Gear.TryCraft(state, _data, _data.gear[0]);

            Assert.That(crafted, Is.True);
            Assert.That(state.GetResource("fibres").ToDouble(), Is.EqualTo(10.0).Within(Tolerance));
            Assert.That(Gear.EquippedInSlot(state, "hands"), Is.EqualTo("cordage-wraps"));
        }

        [Test]
        public void TryCraft_CraftSkillLocked_Refuses()
        {
            var state = GameStateFactory.NewGame(_data);
            state.AddResource("timber", 50);

            // The torch is firecraft — the starting zone only teaches
            // foraging and bushcraft.
            Assert.That(Gear.TryCraft(state, _data, _data.gear[4]), Is.False);
            Assert.That(state.GetResource("timber").ToDouble(), Is.EqualTo(50.0).Within(Tolerance));
        }

        [Test]
        public void TryCraft_MaterialsShort_Refuses()
        {
            var state = GameStateFactory.NewGame(_data);
            state.AddResource("fibres", 39);

            Assert.That(Gear.TryCraft(state, _data, _data.gear[0]), Is.False);
            Assert.That(Gear.EquippedInSlot(state, "hands"), Is.Null);
        }

        [Test]
        public void TryCraft_OccupiedSlot_ReplacesTheWornPiece()
        {
            var state = GameStateFactory.NewGame(_data);
            state.AddResource("reeds", 60);
            Gear.TryCraft(state, _data, _data.gear[2]); // oilskin tarp — camp

            var crafted = Gear.TryCraft(state, _data, _data.gear[3]); // waxed canopy — also camp

            // One piece per slot: the tarp is worn out and gone, and only the
            // canopy's effect remains.
            Assert.That(crafted, Is.True);
            Assert.That(Gear.EquippedInSlot(state, "camp"), Is.EqualTo("waxed-canopy"));
            Assert.That(Upgrades.OfflineCapHours(state, _data), Is.EqualTo(5.0).Within(Tolerance));
        }

        [Test]
        public void TendingBurstBonus_StrengthensTheBurst()
        {
            var state = GameStateFactory.NewGame(_data);
            TestKith.Station(state, state.nodes[0].id, 1);
            state.AddResource("fibres", 40);
            Gear.TryCraft(state, _data, _data.gear[0]);
            Simulation.Tend(state.nodes[0], _data.economy);

            Simulation.Advance(state, _data, 2.0);

            // 1 familiar · 2 s · burst 3 · (1 + the wraps' 0.5) = 9.
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(9.0).Within(Tolerance));
        }

        [Test]
        public void CarrierCapacityBonus_WidensTheHaulLoad()
        {
            var state = GameStateFactory.NewGame(_data);
            state.AddResource("timber", 25);
            Gear.TryCraft(state, _data, _data.gear[1]);

            Assert.That(Upgrades.HaulCapacityMultiplier(state, _data), Is.EqualTo(1.25).Within(Tolerance));
        }

        [Test]
        public void OfflineCapBonusHours_AddsToTheCap()
        {
            var state = GameStateFactory.NewGame(_data);
            state.AddResource("reeds", 30);
            Gear.TryCraft(state, _data, _data.gear[2]);

            // Base 4 h plus the tarp's +2 — additive, not a raise-to.
            Assert.That(Upgrades.OfflineCapHours(state, _data), Is.EqualTo(6.0).Within(Tolerance));
        }
    }
}
