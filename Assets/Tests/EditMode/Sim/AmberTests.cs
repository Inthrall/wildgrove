using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins the Amber economy layer (design §10): observation sites surface it as a
    /// separate channel from field sketches (a fully-recorded site keeps producing),
    /// unconfigured data draws no rng, and the time-skip sink credits full
    /// live-rate production for its cost — refused when short. IAP/ads are
    /// the plugin pass; the Rite can never be paid in Amber by construction.
    /// </summary>
    public class AmberTests
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
                // digFindsPerHour high enough that one 1-second sub-step is a
                // certain find — the chance test would flake otherwise.
                amber = new EconomyData.AmberData { digFindsPerHour = 36000, perFind = 2, timeSkipHours = 0.01, timeSkipCostAmber = 15 },
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
            _data.insects = new List<InsectData>
            {
                new InsectData
                {
                    id = "stags-herald", sketches = 3,
                    habitats = new List<string> { GameStateFactory.StartingZoneId }, rarity = 1.0,
                },
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_data);
        }

        private GameState StateWithAWanderer()
        {
            var state = GameStateFactory.NewGame(_data);
            state.digSites.Add(new DigSiteState { zoneId = GameStateFactory.StartingZoneId });
            TestKith.Station(state, Familiar.WanderStation, 1);
            return state;
        }

        [Test]
        public void Digging_SurfacesAmber()
        {
            var state = StateWithAWanderer();

            Simulation.Advance(state, _data, 1.0);

            Assert.That(state.amber, Is.EqualTo(2.0).Within(Tolerance), "one certain find, perFind amber");
        }

        [Test]
        public void FullyDugGround_KeepsSurfacingAmber()
        {
            var state = StateWithAWanderer();
            state.insectSketches["stags-herald"] = 3; // nothing left to find

            Simulation.Advance(state, _data, 1.0);

            Assert.That(state.amber, Is.EqualTo(2.0).Within(Tolerance),
                "amber is the dig's renewable — the insect channel falling quiet doesn't stop it");
        }

        [Test]
        public void UnconfiguredAmber_BurnsNoRng()
        {
            _data.economy.amber = null;
            var state = StateWithAWanderer();
            state.insectSketches["stags-herald"] = 3; // the sketch channel is quiet too
            var rngBefore = state.rngState;
            state.roster.RemoveAll(f => f.stationId == state.nodes[0].id);

            Simulation.Advance(state, _data, 1.0);

            Assert.That(state.rngState, Is.EqualTo(rngBefore),
                "pre-amber saves must replay identically — no draw without the section");
        }

        [Test]
        public void TimeSkip_CreditsFullLiveRateProduction()
        {
            var state = GameStateFactory.NewGame(_data);
            TestKith.Station(state, state.nodes[0].id, 1);
            state.amber = 15.0;

            var hours = Amber.TryTimeSkip(state, _data);

            Assert.That(hours, Is.EqualTo(0.01).Within(Tolerance));
            Assert.That(state.amber, Is.EqualTo(0.0).Within(Tolerance), "the cost is spent");
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(36.0).Within(Tolerance),
                "0.01h at the full live rate — no offline cap, no rate multiplier");
        }

        [Test]
        public void TimeSkip_RefusedWhenShort()
        {
            var state = GameStateFactory.NewGame(_data);
            state.amber = 14.0;

            Assert.That(Amber.CanTimeSkip(state, _data), Is.False);
            Assert.That(Amber.TryTimeSkip(state, _data), Is.EqualTo(0.0));
            Assert.That(state.amber, Is.EqualTo(14.0).Within(Tolerance), "nothing spent on a refusal");
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
        }

        [Test]
        public void TimeSkip_RefusedWhenUnconfigured()
        {
            _data.economy.amber = null;
            var state = GameStateFactory.NewGame(_data);
            state.amber = 100.0;

            Assert.That(Amber.TryTimeSkip(state, _data), Is.EqualTo(0.0));
        }
    }
}
