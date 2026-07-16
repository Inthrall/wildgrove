using System.Collections.Generic;
using BreakInfinity;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins design §5's quality rolls: one roll per haul batch (never per
    /// unit), the whole delivery taking the rolled tier — Fine to its own
    /// pool, Pristine held apart as specimens — with the Pristine chance
    /// following §8 (flat bonuses add points, Tending multiplies) and every
    /// roll drawn from the run's saved, deterministic rng.
    /// </summary>
    public class QualityTests
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
                hauling = new EconomyData.HaulingData { baseCarryCapacity = 1.0, tripSeconds = 2.0, basketCapacity = 10.0 },
                // Chances start at 0 — each test dials up the tier it pins.
                quality = new EconomyData.QualityData
                {
                    fineChance = 0.0, fineValueMult = 1.5, pristineBaseChance = 0.0, pristineValueMult = 10.0,
                },
                tending = new EconomyData.TendingData
                {
                    burstYieldMult = 3.0, burstDurationSec = 5.0,
                    pristineBonusDurationSec = 30.0, pristineChanceBonus = 1.0,
                    handGatherPerSecond = 0.0,
                },
            };
            _data.zones = new List<ZoneData>
            {
                new ZoneData
                {
                    id = GameStateFactory.StartingZoneId,
                    resources = new List<string> { "berries", "wildflowers" },
                    unlocks = new List<string> { "foraging" },
                },
            };
            _data.upgrades = new List<UpgradeData>
            {
                new UpgradeData
                {
                    order = 17, id = "field-press", costCoin = 70000,
                    effects = { new EffectData { type = EffectType.PristineChanceBonus, value = 0.01 } },
                },
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_data);
        }

        [Test]
        public void Advance_CertainFineRoll_LandsTheWholeBatchInTheFinePool()
        {
            _data.economy.quality.fineChance = 1.0;
            var state = GameStateFactory.NewGame(_data);

            Simulation.Advance(state, _data, 2.0);

            // The t=2 delivery of 1 unit rolled Fine — nothing reaches the
            // common stock.
            Assert.That(state.GetFine("berries").ToDouble(), Is.EqualTo(1.0).Within(Tolerance));
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
        }

        [Test]
        public void Advance_CertainPristineRoll_LandsTheWholeBatchAsSpecimens()
        {
            _data.economy.quality.pristineBaseChance = 1.0;
            var state = GameStateFactory.NewGame(_data);

            Simulation.Advance(state, _data, 2.0);

            Assert.That(state.GetPristine("berries").ToDouble(), Is.EqualTo(1.0).Within(Tolerance));
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(state.GetFine("berries").ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
        }

        [Test]
        public void Advance_QualityUnconfigured_EverythingLandsCommonWithoutBurningRng()
        {
            // Both chances 0 (the fixture default) — the system is off, the
            // way every pre-quality fixture in the suite runs.
            var state = GameStateFactory.NewGame(_data);
            var seedBefore = state.rngState;

            Simulation.Advance(state, _data, 2.0);

            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(1.0).Within(Tolerance));
            Assert.That(state.rngState, Is.EqualTo(seedBefore), "an unconfigured roll must not consume rng");
        }

        [Test]
        public void PristineChance_AddsOwnedFlatBonusPoints()
        {
            _data.economy.quality.pristineBaseChance = 0.005;
            var state = GameStateFactory.NewGame(_data);
            state.purchasedUpgradeIds.Add("field-press");

            var chance = Quality.PristineChance(state, _data, state.nodes[0]);

            // 0.5% base + the Field Press's 1pt (design §8's additive band).
            Assert.That(chance, Is.EqualTo(0.015).Within(Tolerance));
        }

        [Test]
        public void PristineChance_TendWindowMultiplies()
        {
            _data.economy.quality.pristineBaseChance = 0.005;
            var state = GameStateFactory.NewGame(_data);
            state.purchasedUpgradeIds.Add("field-press");
            state.nodes[0].pristineBonusRemaining = 5.0;

            var chance = Quality.PristineChance(state, _data, state.nodes[0]);

            // (0.005 + 0.01) · (1 + 1.0) — flat points add, Tending multiplies.
            Assert.That(chance, Is.EqualTo(0.03).Within(Tolerance));
        }

        [Test]
        public void PristineChance_IsCappedAtOne()
        {
            _data.economy.quality.pristineBaseChance = 0.9;
            var state = GameStateFactory.NewGame(_data);
            state.nodes[0].pristineBonusRemaining = 5.0;

            Assert.That(Quality.PristineChance(state, _data, state.nodes[0]), Is.EqualTo(1.0));
        }

        [Test]
        public void Tend_OpensThePristineWindow_WhichOutlastsTheYieldBurst()
        {
            var state = GameStateFactory.NewGame(_data);

            Simulation.Tend(state.nodes[0], _data.economy);
            Simulation.Advance(state, _data, 10.0);

            // The 5 s yield burst is long spent; the 30 s Pristine window runs on.
            Assert.That(state.nodes[0].tendBurstRemaining, Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(state.nodes[0].pristineBonusRemaining, Is.EqualTo(20.0).Within(Tolerance));
        }

        [Test]
        public void Advance_SameSeed_RollsTheSameOutcomes()
        {
            _data.economy.quality.fineChance = 0.5;
            var first = GameStateFactory.NewGame(_data);
            var second = GameStateFactory.NewGame(_data);
            first.rngState = 42UL;
            second.rngState = 42UL;

            Simulation.Advance(first, _data, 20.0);
            Simulation.Advance(second, _data, 20.0);

            Assert.That(second.GetFine("berries").ToDouble(),
                Is.EqualTo(first.GetFine("berries").ToDouble()).Within(Tolerance));
            Assert.That(second.GetResource("berries").ToDouble(),
                Is.EqualTo(first.GetResource("berries").ToDouble()).Within(Tolerance));
            Assert.That(second.rngState, Is.EqualTo(first.rngState));
        }

        [Test]
        public void AdvanceOfflineWithSummary_CountsQualityPoolsAsGains()
        {
            _data.economy.quality.fineChance = 1.0;
            _data.economy.hauling.baseCarryCapacity = 1e9;
            var state = GameStateFactory.NewGame(_data);

            var summary = Simulation.AdvanceOfflineWithSummary(state, _data, 30.0);

            // Everything landed in the Fine pool, but the welcome-back sheet
            // still reports the absence's full harvest.
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(summary.gains["berries"].ToDouble(), Is.EqualTo(30.0).Within(Tolerance));
        }

        [Test]
        public void Rng_NextDouble_IsDeterministicAndInRange()
        {
            var a = 123456789UL;
            var b = 123456789UL;

            for (var i = 0; i < 100; i++)
            {
                var fromA = Rng.NextDouble(ref a);
                var fromB = Rng.NextDouble(ref b);
                Assert.That(fromA, Is.EqualTo(fromB), "same state must draw the same sequence");
                Assert.That(fromA, Is.GreaterThanOrEqualTo(0.0));
                Assert.That(fromA, Is.LessThan(1.0));
            }
        }

        [Test]
        public void Rng_ZeroState_IsSanitisedNotStuck()
        {
            var state = 0UL;

            var roll = Rng.NextDouble(ref state);

            // Zero is xorshift's fixed point — it must be mapped away, never
            // left to draw 0 forever.
            Assert.That(state, Is.Not.EqualTo(0UL));
            Assert.That(roll, Is.GreaterThanOrEqualTo(0.0).And.LessThan(1.0));
        }
    }
}
