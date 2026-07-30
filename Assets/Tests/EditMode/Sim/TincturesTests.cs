using System.Collections.Generic;
using BreakInfinity;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;
using Wildgrove.Sim.Saves;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins the tinctures (design §5, Apothecary): drinking spends one bottle
    /// of camp stock and grants the effects for durationSec of sim time; a
    /// second bottle adds its duration on top (the clock stacks, the effect
    /// never does); expiry drops the buff
    /// and rebuilds the modifiers the same step. Unauthored tinctures leave
    /// everything untouched, and the buff survives a save round trip.
    /// </summary>
    public class TincturesTests
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
                    resources = new List<string> { "berries" },
                    unlocks = new List<string> { "foraging" },
                },
            };
            _data.tinctures = new List<TinctureData>
            {
                new TinctureData
                {
                    id = "wardens-tonic",
                    displayName = "Warden's Tonic",
                    description = "steadies the hands",
                    durationSec = 10.0,
                    effects = new List<EffectData>
                    {
                        new EffectData { type = EffectType.YieldBonus, skill = "all-gathering", value = 0.25 },
                    },
                },
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_data);
        }

        private GameState StateWithNode()
        {
            var state = new GameState();
            state.nodes.Add(new NodeState { id = "n1", zoneId = GameStateFactory.StartingZoneId, resourceId = "berries", skill = "foraging", yieldMultiplier = 1.0 });
            return state;
        }

        private TinctureData Tonic => _data.tinctures[0];

        [Test]
        public void CanDrink_NeedsABottleInStock()
        {
            var state = StateWithNode();

            Assert.That(Tinctures.CanDrink(state, Tonic), Is.False, "the shelf is bare");
            Assert.That(Tinctures.TryDrink(state, _data, Tonic), Is.False);
            Assert.That(state.activeTinctures, Is.Empty);
        }

        [Test]
        public void TryDrink_SpendsTheBottleAndAppliesTheBuff()
        {
            var state = StateWithNode();
            state.AddResource("wardens-tonic", new BigDouble(2));

            Assert.That(Tinctures.TryDrink(state, _data, Tonic), Is.True);
            Assert.That(state.GetResource("wardens-tonic").ToDouble(), Is.EqualTo(1.0).Within(Tolerance), "one bottle spent");
            Assert.That(Tinctures.RemainingSeconds(state, "wardens-tonic"), Is.EqualTo(10.0).Within(Tolerance));
            Assert.That(state.nodes[0].yieldMultiplier, Is.EqualTo(1.25).Within(Tolerance),
                "the tonic's yield bonus folds into the node multipliers at once");
        }

        [Test]
        public void TryDrink_WhileActive_StacksDurationNeverEffect()
        {
            var state = StateWithNode();
            state.AddResource("wardens-tonic", new BigDouble(2));
            Tinctures.TryDrink(state, _data, Tonic);
            Tinctures.Advance(state, _data, 7.0);

            Assert.That(Tinctures.TryDrink(state, _data, Tonic), Is.True);

            Assert.That(state.activeTinctures, Has.Count.EqualTo(1), "one buff, not two");
            Assert.That(Tinctures.RemainingSeconds(state, "wardens-tonic"), Is.EqualTo(13.0).Within(Tolerance),
                "the second bottle banks its full duration on top of what's left");
            Assert.That(state.nodes[0].yieldMultiplier, Is.EqualTo(1.25).Within(Tolerance), "the bonus never doubles");
        }

        [Test]
        public void Advance_TicksDown_AndExpiryDropsTheBuff()
        {
            var state = StateWithNode();
            state.AddResource("wardens-tonic", BigDouble.One);
            Tinctures.TryDrink(state, _data, Tonic);

            Tinctures.Advance(state, _data, 4.0);
            Assert.That(Tinctures.RemainingSeconds(state, "wardens-tonic"), Is.EqualTo(6.0).Within(Tolerance));
            Assert.That(state.nodes[0].yieldMultiplier, Is.EqualTo(1.25).Within(Tolerance), "still live");

            Tinctures.Advance(state, _data, 6.0);
            Assert.That(state.activeTinctures, Is.Empty, "the lapsed buff is dropped");
            Assert.That(state.nodes[0].yieldMultiplier, Is.EqualTo(1.0).Within(Tolerance),
                "and the node multipliers are rebuilt the same step");
        }

        [Test]
        public void SimulationAdvance_RunsTheTinctureClock()
        {
            var state = StateWithNode();
            state.AddResource("wardens-tonic", BigDouble.One);
            Tinctures.TryDrink(state, _data, Tonic);

            Simulation.Advance(state, _data, 3.0);

            Assert.That(Tinctures.RemainingSeconds(state, "wardens-tonic"), Is.EqualTo(7.0).Within(Tolerance),
                "the tick (and so offline catch-up) ages the buff in sim time");
        }

        [Test]
        public void SaveRoundTrip_KeepsTheLiveBuff()
        {
            var state = StateWithNode();
            state.AddResource("wardens-tonic", BigDouble.One);
            Tinctures.TryDrink(state, _data, Tonic);
            Tinctures.Advance(state, _data, 4.0);

            var restored = SaveCodec.Restore(SaveCodec.Capture(state, 0L), _data);

            Assert.That(Tinctures.RemainingSeconds(restored, "wardens-tonic"), Is.EqualTo(6.0).Within(Tolerance));
            Assert.That(restored.nodes[0].yieldMultiplier, Is.EqualTo(1.25).Within(Tolerance),
                "restore rebuilds the multipliers with the buff still live");
        }

        [Test]
        public void UnauthoredTinctures_LeaveEverythingUntouched()
        {
            _data.tinctures = new List<TinctureData>();
            var state = StateWithNode();

            Assert.That(Tinctures.Configured(_data), Is.False);
            Tinctures.Advance(state, _data, 5.0);
            Upgrades.RecomputeYieldMultipliers(state, _data);

            Assert.That(state.nodes[0].yieldMultiplier, Is.EqualTo(1.0).Within(Tolerance));
        }
    }
}
