using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins design §6 observation: trail maps open observation sites, the
    /// wanderer passes them all as it roams (the watch is not a post of its
    /// own) and records field sketches (rate rolls + the pity guarantee, both
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
            // Sites open unwatched — the wanderer is sent, never seeded.
            Assert.That(Stationing.Wandering(state), Is.EqualTo(0));
        }

        [Test]
        public void Advance_WatcherAtACertainRate_SurfacesASketch()
        {
            var state = NewGameWithDigSite();
            TestKith.Station(state, Familiar.WanderStation, 1);

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
            TestKith.Station(state, Familiar.WanderStation, 1);

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
            TestKith.Station(state, Familiar.WanderStation, 1);
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
            TestKith.Station(state, Familiar.WanderStation, 1);
            state.insectSketches["stags-herald"] = 3; // already assembled
            var seedBefore = state.rngState;

            Simulation.Advance(state, _data, 10.0);

            Assert.That(Insects.SketchCount(state, "stags-herald"), Is.EqualTo(3));
            Assert.That(state.digSites[0].pityHours, Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(state.rngState, Is.EqualTo(seedBefore));
        }

        /// <summary>
        /// Turns on the XP economy and the watch-XP award, both of which the
        /// shared fixture leaves absent — most tests here care about sketches,
        /// and an always-on XP economy would put renown in every one of them.
        /// </summary>
        private void EnableWatchXp(double perHour = 50.0)
        {
            _data.economy.xp = new EconomyData.XpData
            {
                baseXp = 100, growth = 1.1, maxLevel = 99, gatherPerUnit = 3, craftPerBatch = 25,
            };
            _data.economy.observation.skill = "entomology";
            _data.economy.observation.watchXpPerHour = perHour;
        }

        [Test]
        public void Advance_Watching_TrainsTheObservationCraft()
        {
            EnableWatchXp();
            var state = NewGameWithDigSite();
            TestKith.Station(state, Familiar.WanderStation, 1);

            Simulation.Advance(state, _data, 3600.0);

            // One watcher · one site · one hour · dig speed ×1 = watchXpPerHour.
            Assert.That(Skills.Xp(state, "entomology"), Is.EqualTo(50.0).Within(1e-6));
        }

        [Test]
        public void Advance_WatchXp_ScalesWithTheSitesDigSpeed()
        {
            EnableWatchXp();
            var state = NewGameWithDigSite();
            TestKith.Station(state, Familiar.WanderStation, 1);
            state.purchasedUpgradeIds.Add("brush-screens"); // dig speed ×2

            Simulation.Advance(state, _data, 3600.0);

            Assert.That(Skills.Xp(state, "entomology"), Is.EqualTo(100.0).Within(1e-6));
        }

        [Test]
        public void Advance_NoWatchers_TrainsNothing()
        {
            EnableWatchXp();
            var state = NewGameWithDigSite();

            Simulation.Advance(state, _data, 3600.0);

            Assert.That(Skills.Xp(state, "entomology"), Is.EqualTo(0.0));
        }

        [Test]
        public void Advance_FullyRecordedSite_StillTrainsTheCraft()
        {
            // The quiet-site case is the whole reason this XP is paid per hour
            // watched rather than per sketch. Sketches are a finite lifetime pool
            // that rides the fold in insectSketches while skillXp resets, so a
            // per-sketch award would leave a later run with every plate already
            // recorded unable to train the craft at all — and Brush Screens
            // (entomology 8) unbuyable for the rest of the game.
            EnableWatchXp();
            var state = NewGameWithDigSite();
            TestKith.Station(state, Familiar.WanderStation, 1);
            state.insectSketches["stags-herald"] = 3; // every plate here recorded

            Simulation.Advance(state, _data, 3600.0);

            Assert.That(Insects.SketchCount(state, "stags-herald"), Is.EqualTo(3), "nothing left to sketch");
            Assert.That(Skills.Xp(state, "entomology"), Is.EqualTo(50.0).Within(1e-6), "but the watching still counts");
        }

        [Test]
        public void MeetsSkillGate_EnoughWatching_OpensAnEntomologyGate()
        {
            // What shipped broken: brush-screens gates on entomology 8, nothing
            // awarded entomology XP, so the rung drew on the ladder and sat at
            // level 1 forever with no error anywhere to say why.
            EnableWatchXp(1000.0); // a level-8 hour, so the test needn't watch twenty
            _data.upgrades[1].gateSkill = "entomology";
            _data.upgrades[1].gateLevel = 8;
            var state = NewGameWithDigSite();
            TestKith.Station(state, Familiar.WanderStation, 1);
            Assert.That(Upgrades.MeetsSkillGate(state, _data, _data.upgrades[1]), Is.False);

            Simulation.Advance(state, _data, 3600.0);

            Assert.That(Upgrades.MeetsSkillGate(state, _data, _data.upgrades[1]), Is.True);
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
            TestKith.Station(state, Familiar.WanderStation, 1);
            state.insectSketches["stags-herald"] = 3; // assembled — out of the pick

            Simulation.Advance(state, _data, 1.0);

            Assert.That(Insects.SketchCount(state, "those-who-sow"), Is.EqualTo(1));
            Assert.That(Insects.SketchCount(state, "stags-herald"), Is.EqualTo(3));
        }

        [Test]
        public void RecordedPlate_ChoiceChanceBonus_JoinsTheQualityChance()
        {
            _data.economy.quality = new EconomyData.QualityData
            {
                decentChance = 0.035, decentValueMult = 1.5, choiceBaseChance = 0.005, choiceValueMult = 10.0,
            };
            _data.insects[0].effects.Add(new EffectData { type = EffectType.ChoiceChanceBonus, value = 0.01 });
            var state = NewGameWithDigSite();
            state.insectSketches["stags-herald"] = 3;

            var chance = Quality.ChoiceChance(state, _data, state.nodes[0]);

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
        public void EligibleInsects_NeverOffersAnAwardedPlate()
        {
            // The Wayfarer's Plate is given, not drawn (design §11). If the roll
            // could reach it, a player would record it unawarded — and worse, an
            // award arriving for a plate already in the book would have nothing
            // to land, so the order could never be acknowledged.
            _data.insects.Add(new InsectData
            {
                id = PlayRewards.WayfarersPlateId, displayName = "The Wayfarer's Plate",
                sketches = 1, rarity = 0, rewarded = true,
                effects = { new EffectData { type = EffectType.ChoiceChanceBonus, value = 0.005 } },
            });

            var state = NewGameWithDigSite();
            var eligible = Observation.EligibleInsects(state, _data, "old-growth-wood");

            Assert.That(eligible.Count, Is.EqualTo(1), "only the drawable plate is in the roll");
            Assert.That(eligible[0].id, Is.EqualTo("stags-herald"));
        }

        [Test]
        public void EligibleInsects_StillSkipsAnAwardedPlateThatWasGivenAHabitat()
        {
            // Belt and braces: the validator refuses habitats on an awarded
            // plate, but the roll must not depend on that to hold the line.
            _data.insects.Add(new InsectData
            {
                id = PlayRewards.WayfarersPlateId, displayName = "The Wayfarer's Plate",
                sketches = 1, habitats = new List<string> { "old-growth-wood" }, rarity = 1.0, rewarded = true,
                effects = { new EffectData { type = EffectType.ChoiceChanceBonus, value = 0.005 } },
            });

            var state = NewGameWithDigSite();

            Assert.That(Observation.EligibleInsects(state, _data, "old-growth-wood").Count, Is.EqualTo(1));
        }

        [Test]
        public void TheWanderer_IsTheWatcher_AtEverySite()
        {
            var state = NewGameWithDigSite();
            Assert.That(Stationing.WanderAgents(state, _data), Is.EqualTo(0.0).Within(Tolerance));

            // The wander post supplies the watching (design §2) — one roaming
            // familiar covers every unlocked site.
            TestKith.Station(state, Familiar.WanderStation, 1);

            Assert.That(Stationing.Wandering(state), Is.EqualTo(1));
            Assert.That(Stationing.WanderAgents(state, _data), Is.EqualTo(1.0).Within(Tolerance));
        }
    }
}
