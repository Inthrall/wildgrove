using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;
using Wildgrove.Sim.Saves;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins the deep amber (design §6/§7): authored pieces surface strictly in
    /// order at their one zone's observation site, a pity clock keeps the lore
    /// from starving, the completed set grants its plate effects at once, and
    /// the count survives both a save round trip and the fold. Any other
    /// zone's site — and an unconfigured window — draws no rng at all.
    /// </summary>
    public class DeepAmberTests
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
                verdure = new EconomyData.VerdureData { renownDivisor = 5000, exponent = 0.5, yieldBonusPerPoint = 0.02 },
                offline = new EconomyData.OfflineData { baseCapHours = 4, rateMultiplier = 1.0 },
                observation = new EconomyData.ObservationData { pityTimerHoursWatched = 4, baseSketchesPerHour = 0.25 },
            };
            _data.resources = new List<ResourceData>
            {
                new ResourceData { id = "berries", sellValue = 2, skill = "foraging" },
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
            _data.deepAmber = new DeepAmberData
            {
                zoneId = GameStateFactory.StartingZoneId,
                // High enough that one 1-second sub-step is a certain find —
                // a chance-based assert would flake otherwise.
                findsPerHour = 36000,
                pityHoursWatched = 12,
                plateName = "The Deep Amber",
                completedLore = "The world it flew through has ended.",
                effects = new List<EffectData>
                {
                    new EffectData { type = EffectType.YieldBonus, skill = "all-gathering", value = 0.25 },
                },
                pieces = new List<AmberPieceData>
                {
                    new AmberPieceData { id = "the-wing", displayName = "The Wing", lore = "A wing." },
                    new AmberPieceData { id = "the-seed", displayName = "The Seed", lore = "A seed." },
                },
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_data);
        }

        private GameState StateWithAWatcher()
        {
            var state = GameStateFactory.NewGame(_data);
            state.digSites.Add(new DigSiteState { zoneId = GameStateFactory.StartingZoneId });
            TestKith.Station(state, Familiar.WatchStation(GameStateFactory.StartingZoneId), 1);
            return state;
        }

        [Test]
        public void Watching_SurfacesThePiecesInAuthoredOrder()
        {
            var state = StateWithAWatcher();

            Simulation.Advance(state, _data, 1.0);
            Assert.That(DeepAmber.FoundCount(state), Is.EqualTo(1), "one certain find per step");

            var found = new List<AmberPieceData>(DeepAmber.FoundPieces(state, _data));
            Assert.That(found, Has.Count.EqualTo(1));
            Assert.That(found[0].id, Is.EqualTo("the-wing"), "the sequence is the story — nothing but timing rolls");

            Simulation.Advance(state, _data, 1.0);
            found = new List<AmberPieceData>(DeepAmber.FoundPieces(state, _data));
            Assert.That(found[1].id, Is.EqualTo("the-seed"));
        }

        [Test]
        public void CompletedSet_GrantsThePlateEffectsAtOnce()
        {
            var state = StateWithAWatcher();

            Simulation.Advance(state, _data, 1.0);
            Assert.That(DeepAmber.IsComplete(state, _data), Is.False);
            Assert.That(state.nodes[0].yieldMultiplier, Is.EqualTo(1.0).Within(Tolerance),
                "no effect until the last piece is up");

            Simulation.Advance(state, _data, 1.0);
            Assert.That(DeepAmber.IsComplete(state, _data), Is.True);
            Assert.That(state.nodes[0].yieldMultiplier, Is.EqualTo(1.25).Within(Tolerance),
                "the plate's bonus folds into the multipliers the moment the set completes");
        }

        [Test]
        public void CompleteSet_DrawsNoFurtherRng()
        {
            var state = StateWithAWatcher();
            Simulation.Advance(state, _data, 1.0);
            Simulation.Advance(state, _data, 1.0);
            Assert.That(DeepAmber.IsComplete(state, _data), Is.True);

            var rngBefore = state.rngState;
            DeepAmber.AdvanceSite(state, _data, GameStateFactory.StartingZoneId, 1.0, 1.0, 1.0);

            Assert.That(state.rngState, Is.EqualTo(rngBefore), "a shut window burns no rng — sequences stay stable");
            Assert.That(DeepAmber.FoundCount(state), Is.EqualTo(2), "and nothing past the authored pieces");
        }

        [Test]
        public void OtherZonesSites_NeverDraw()
        {
            var state = GameStateFactory.NewGame(_data);
            var rngBefore = state.rngState;

            DeepAmber.AdvanceSite(state, _data, "elsewhere", 1.0, 1.0, 1.0);

            Assert.That(state.rngState, Is.EqualTo(rngBefore));
            Assert.That(DeepAmber.FoundCount(state), Is.EqualTo(0));
            Assert.That(state.deepAmberPityHours, Is.EqualTo(0.0).Within(Tolerance),
                "another zone's watching banks no pity toward the deep site");
        }

        [Test]
        public void UnconfiguredWindow_IsInert()
        {
            _data.deepAmber = null;
            var state = StateWithAWatcher();
            var rngBefore = state.rngState;

            DeepAmber.AdvanceSite(state, _data, GameStateFactory.StartingZoneId, 1.0, 1.0, 1.0);

            Assert.That(DeepAmber.Configured(_data), Is.False);
            Assert.That(state.rngState, Is.EqualTo(rngBefore), "pre-amber saves must replay identically");
            Assert.That(DeepAmber.FoundCount(state), Is.EqualTo(0));
        }

        [Test]
        public void PityClock_GuaranteesTheNextPiece()
        {
            var state = GameStateFactory.NewGame(_data);
            // A rate the roll can never realistically hit, so only pity fires.
            _data.deepAmber.findsPerHour = 1e-12;
            _data.deepAmber.pityHoursWatched = 1.0;

            DeepAmber.AdvanceSite(state, _data, GameStateFactory.StartingZoneId, 1.0, 1.0, 1800.0);
            Assert.That(DeepAmber.FoundCount(state), Is.EqualTo(0), "half an hour watched — not yet");
            Assert.That(state.deepAmberPityHours, Is.EqualTo(0.5).Within(Tolerance));

            DeepAmber.AdvanceSite(state, _data, GameStateFactory.StartingZoneId, 1.0, 1.0, 1800.0);
            Assert.That(DeepAmber.FoundCount(state), Is.EqualTo(1), "the hour guarantees the piece — the lore must not starve");
            Assert.That(state.deepAmberPityHours, Is.EqualTo(0.0).Within(Tolerance), "the clock restarts");
        }

        [Test]
        public void SaveRoundTrip_KeepsTheCountAndThePityClock()
        {
            var state = GameStateFactory.NewGame(_data);
            state.deepAmberFound = 1;
            state.deepAmberPityHours = 3.5;

            var restored = SaveCodec.Restore(SaveCodec.Capture(state, 0L), _data);

            Assert.That(restored.deepAmberFound, Is.EqualTo(1));
            Assert.That(restored.deepAmberPityHours, Is.EqualTo(3.5).Within(Tolerance));
        }

        [Test]
        public void RestoredCompleteSet_RebuildsThePlateEffects()
        {
            var state = GameStateFactory.NewGame(_data);
            state.deepAmberFound = 2;

            var restored = SaveCodec.Restore(SaveCodec.Capture(state, 0L), _data);

            Assert.That(restored.nodes[0].yieldMultiplier, Is.EqualTo(1.25).Within(Tolerance),
                "restore rebuilds the multipliers with the plate recorded");
        }
    }
}
