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
                amber = new EconomyData.AmberData { digFindsPerHour = 36000, perFind = 2, timeSkipHours = 0.01, timeSkipCostAmber = 15, adDripAmber = 3, weeklyCacheAmber = 20 },
                store = new EconomyData.StoreData { starterBundleAmber = 30, amberPackSmall = 50, amberPackLarge = 150 },
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

        [Test]
        public void GrantDrip_CreditsTheConfiguredPile()
        {
            var state = GameStateFactory.NewGame(_data);

            var granted = Amber.GrantDrip(state, _data);

            Assert.That(granted, Is.EqualTo(3.0).Within(Tolerance));
            Assert.That(state.amber, Is.EqualTo(3.0).Within(Tolerance), "the rewarded-ad drip lands in the coffer");
        }

        [Test]
        public void GrantDrip_RefusedWhenUnconfigured()
        {
            _data.economy.amber = null;
            var state = GameStateFactory.NewGame(_data);

            Assert.That(Amber.GrantDrip(state, _data), Is.EqualTo(0.0));
            Assert.That(state.amber, Is.EqualTo(0.0).Within(Tolerance));
        }

        [Test]
        public void WeeklyCache_FirstClaimGrantsAndStamps()
        {
            var state = GameStateFactory.NewGame(_data);
            const long now = 1_000_000_000_000L;

            Assert.That(Amber.CanClaimWeeklyCache(state, _data, now), Is.True, "never claimed — ready now");
            var granted = Amber.ClaimWeeklyCache(state, _data, now);

            Assert.That(granted, Is.EqualTo(20.0).Within(Tolerance));
            Assert.That(state.amber, Is.EqualTo(20.0).Within(Tolerance));
            Assert.That(state.weeklyCacheClaimedUnixMs, Is.EqualTo(now), "the claim time is stamped");
        }

        [Test]
        public void WeeklyCache_RefusedBeforeAWeekElapses()
        {
            var state = GameStateFactory.NewGame(_data);
            const long now = 1_000_000_000_000L;
            Amber.ClaimWeeklyCache(state, _data, now);

            var sixDays = now + (6L * 24L * 60L * 60L * 1000L);
            Assert.That(Amber.CanClaimWeeklyCache(state, _data, sixDays), Is.False, "still cooling down");
            Assert.That(Amber.ClaimWeeklyCache(state, _data, sixDays), Is.EqualTo(0.0));
            Assert.That(state.amber, Is.EqualTo(20.0).Within(Tolerance), "no second pile before the week is out");
        }

        [Test]
        public void WeeklyCache_ReArmsAfterAWeek()
        {
            var state = GameStateFactory.NewGame(_data);
            const long now = 1_000_000_000_000L;
            Amber.ClaimWeeklyCache(state, _data, now);

            var aWeekOn = now + Amber.WeeklyCacheCooldownMs;
            Assert.That(Amber.CanClaimWeeklyCache(state, _data, aWeekOn), Is.True, "the cache re-arms a week later");
            Assert.That(Amber.ClaimWeeklyCache(state, _data, aWeekOn), Is.EqualTo(20.0).Within(Tolerance));
            Assert.That(state.amber, Is.EqualTo(40.0).Within(Tolerance), "two weeks, two caches");
        }

        [Test]
        public void GrantPack_CreditsAmount()
        {
            var state = GameStateFactory.NewGame(_data);

            var granted = Amber.GrantPack(state, 50.0);

            Assert.That(granted, Is.EqualTo(50.0).Within(Tolerance));
            Assert.That(state.amber, Is.EqualTo(50.0).Within(Tolerance));
        }

        [Test]
        public void GrantPack_NonPositiveIsNoOp()
        {
            var state = GameStateFactory.NewGame(_data);
            state.amber = 5.0;

            Assert.That(Amber.GrantPack(state, 0.0), Is.EqualTo(0.0));
            Assert.That(state.amber, Is.EqualTo(5.0).Within(Tolerance), "an unconfigured pack mints nothing");
        }

        [Test]
        public void Digging_BanksAmberForTelemetry()
        {
            var state = StateWithAWanderer();

            Simulation.Advance(state, _data, 1.0);

            Assert.That(state.amberFoundUnlogged, Is.EqualTo(2.0).Within(Tolerance),
                "the find is banked for GameLoop to report, alongside crediting the balance");
        }
    }
}
