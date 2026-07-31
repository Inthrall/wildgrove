using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BreakInfinity;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;
using Wildgrove.Game.Services;
using Wildgrove.Sim;

namespace Wildgrove.Game.Tests
{
    /// <summary>
    /// Pins the achievement re-assert (GameLoop's sign-in callback): the milestone
    /// mapping runs against current state, so an achievement earned while signed
    /// out is granted the moment sign-in completes — the one-shot bond celebration
    /// that would otherwise fire it never shows again on a later launch. Any earned
    /// bond satisfies "First kith".
    /// </summary>
    public class AchievementsTests
    {
        private GameDataAsset _data;
        private RecordingGameServices _services;

        [SetUp]
        public void SetUp()
        {
            _data = ScriptableObject.CreateInstance<GameDataAsset>();
            _data.economy = new EconomyData
            {
                mastery = new EconomyData.MasteryData { yieldBonusPerLevel = 0.05 },
                verdure = new EconomyData.VerdureData { renownDivisor = 5000, exponent = 0.5, yieldBonusPerPoint = 0.02 },
                offline = new EconomyData.OfflineData { baseCapHours = 4, rateMultiplier = 1.0 },
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
            _data.almanac = new List<AlmanacNodeData>
            {
                new AlmanacNodeData { id = "old-friend", displayName = "The Old Friend", costVerdure = 12 },
            };
            _data.bonds = new List<BondData>
            {
                new BondData
                {
                    id = "burr", displayName = "Burr", species = "meadow-vole", role = "gatherer",
                    source = new BondSourceData { type = "almanacNode", id = "old-friend" },
                },
            };
            _services = new RecordingGameServices();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_data);
        }

        [Test]
        public void Reassert_WithAnEarnedBond_UnlocksFirstKith()
        {
            var state = GameStateFactory.NewGame(_data);
            state.almanacNodeIds.Add("old-friend");

            Achievements.Reassert(_services, state, _data);

            Assert.That(_services.Unlocked, Does.Contain(AchievementIds.FirstKith));
        }

        [Test]
        public void Reassert_WithNoEarnedBond_UnlocksNothing()
        {
            var state = GameStateFactory.NewGame(_data);

            Achievements.Reassert(_services, state, _data);

            Assert.That(_services.Unlocked, Is.Empty, "no bond earned — no achievement to re-assert");
        }

        [Test]
        public void Reassert_IsIdempotent_UnlocksFirstKithOncePerCall()
        {
            var state = GameStateFactory.NewGame(_data);
            state.almanacNodeIds.Add("old-friend");

            Achievements.Reassert(_services, state, _data);
            Achievements.Reassert(_services, state, _data);

            // One unlock per call even with two earnable bonds' worth of state —
            // the loop stops at the first (Play Games itself dedupes the repeats).
            Assert.That(_services.Unlocked.FindAll(id => id == AchievementIds.FirstKith).Count, Is.EqualTo(2));
        }

        [Test]
        public void EveryAchievementId_IsEvaluatedByReassert()
        {
            // The failure this catches is silent by nature: an achievement no
            // rule evaluates never unlocks on anyone's device, and nothing
            // anywhere says so. The console will happily hold it forever.
            var evaluated = new HashSet<string>(Achievements.EvaluatedIds());
            var missing = DeclaredIds()
                .Where(pair => !evaluated.Contains(pair.Value))
                .Select(pair => pair.Key)
                .ToList();

            Assert.That(missing, Is.Empty,
                "these AchievementIds constants have no rule in Achievements: " + string.Join(", ", missing));
        }

        [Test]
        public void EveryRule_TargetsADeclaredAchievementId()
        {
            var declared = new HashSet<string>(DeclaredIds().Select(pair => pair.Value));
            var unknown = Achievements.EvaluatedIds().Where(id => !declared.Contains(id)).ToList();

            Assert.That(unknown, Is.Empty,
                "these rules target ids that are not in AchievementIds: " + string.Join(", ", unknown));
        }

        [Test]
        public void NoAchievement_HasTwoRules()
        {
            var ids = Achievements.EvaluatedIds().ToList();

            Assert.That(ids.Count, Is.EqualTo(ids.Distinct().Count()), "an id is evaluated by more than one rule");
        }

        [Test]
        public void TheZonesTheLadderNamesByHand_ExistInTheShippedData()
        {
            // Into the Hollows and Cloudreach are the only rules that name a
            // zone as a literal, so they are the only two a rename could quietly
            // strand. Everything else reads a count.
            var dataDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "design", "data"));
            var design = GameData.Parse(GameData.ReadSourcesFromFiles(dataDir));
            var zoneIds = design.Zones.Select(zone => zone.Id).ToList();

            Assert.That(zoneIds, Does.Contain("the-hollows"));
            Assert.That(zoneIds, Does.Contain("cloudreach-peaks"));
        }

        [Test]
        public void EveryStoneRead_TurnsOverOnTheLastZoneTheDataActuallyHas()
        {
            // The threshold is a hardcoded 8 because the console has to agree
            // with it, so it cannot be derived at runtime — but it CAN be pinned,
            // and it needs to be: for seven zones' worth of the ladder's life it
            // stood at 8 against 7 reachable stones and could never fire, which
            // is indistinguishable from a broken achievement. The peaks made it
            // exactly right by luck. Asserting both sides of the boundary means
            // a ninth zone fails here instead of quietly unlocking "every stone"
            // one zone early.
            var dataDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "design", "data"));
            var design = GameData.Parse(GameData.ReadSourcesFromFiles(dataDir));
            var stones = design.Zones
                .Count(zone => design.Dialogue.Waystones.TryGetValue(zone.Id, out var text)
                               && !string.IsNullOrWhiteSpace(text));
            Assert.That(stones, Is.GreaterThan(1), "the data must have stones for this to prove anything");

            // It is incremental, so the code reports absolute progress and Play
            // turns it over at the target — the target is the thing to pin, and
            // it must be exactly the number of stones the data holds.
            Assert.That(Achievements.StepTarget(AchievementIds.EveryStoneRead), Is.EqualTo(stones),
                "\"every stone\" must ask for exactly the stones the data has: fewer and it fires a zone early, "
                + "more and it can never fire at all (which is where it sat until the peaks landed). "
                + "The Play Console holds the same figure — move both.");

            var allOfThem = GameStateFactory.NewGame(_data);
            allOfThem.seenWaystoneZoneIds = Enumerable.Range(0, stones).Select(i => "zone-" + i).ToList();
            Achievements.Reassert(_services, allOfThem, _data);

            Assert.That(_services.Steps, Does.ContainKey(AchievementIds.EveryStoneRead));
            Assert.That(_services.Steps[AchievementIds.EveryStoneRead], Is.EqualTo(stones),
                "and reading every stone must report every step");
        }

        [Test]
        public void Reassert_WhenSignedOut_ReportsNothing()
        {
            var state = GameStateFactory.NewGame(_data);
            state.almanacNodeIds.Add("old-friend");
            _services.SignedIn = false;

            Achievements.Reassert(_services, state, _data);

            Assert.That(_services.Unlocked, Is.Empty, "achievements belong to a gamer profile");
            Assert.That(_services.Steps, Is.Empty);
        }

        [Test]
        public void Reassert_WithAThousandGathered_UnlocksAFullBasket()
        {
            var state = GameStateFactory.NewGame(_data);
            state.lifetimeGathered["berries"] = new BigDouble(1000);

            Achievements.Reassert(_services, state, _data);

            Assert.That(_services.Unlocked, Does.Contain(AchievementIds.AFullBasket));
            Assert.That(_services.Unlocked, Does.Not.Contain(AchievementIds.TheLongHaul),
                "a hundred thousand is a further rung");
        }

        [Test]
        public void Reassert_ReportsIncrementalProgressAsSteps()
        {
            var state = GameStateFactory.NewGame(_data);
            state.migrationCount = 2;

            Achievements.Reassert(_services, state, _data);

            Assert.That(_services.Steps[AchievementIds.ThreeFolds], Is.EqualTo(2));
            Assert.That(_services.Unlocked, Does.Contain(AchievementIds.TheFirstFold),
                "one fold is a plain unlock and two folds have passed it");
        }

        [Test]
        public void Reassert_ClampsStepsToTheTarget()
        {
            var state = GameStateFactory.NewGame(_data);
            state.migrationCount = 99;

            Achievements.Reassert(_services, state, _data);

            // Play rejects a step count above the configured total, so a run
            // that runs away with the counter must still report the target.
            Assert.That(_services.Steps[AchievementIds.TenFolds], Is.EqualTo(10));
        }

        [Test]
        public void Reassert_CountsSpeciesFromTheLifetimeRecord_NotTheRoster()
        {
            var state = GameStateFactory.NewGame(_data);
            state.speciesEverBefriended.AddRange(new[] { "meadow-vole", "red-squirrel", "bramble-hare" });

            Achievements.Reassert(_services, state, _data);

            // The roster is empty — these were befriended in runs already folded.
            Assert.That(state.roster, Is.Empty);
            Assert.That(_services.Steps[AchievementIds.TheWholeWood], Is.EqualTo(3));
        }

        [Test]
        public void Reassert_CountsStationsFromTheLifetimeRecord()
        {
            var state = GameStateFactory.NewGame(_data);
            state.stationsEverWorked.AddRange(new[] { "fire", "bench", "forge" });

            Achievements.Reassert(_services, state, _data);

            Assert.That(_services.Steps[AchievementIds.TheWholeCampWorking], Is.EqualTo(3));
        }

        /// <summary>The encoded ids declared in the generated <see cref="AchievementIds"/>, by constant name.</summary>
        private static IEnumerable<KeyValuePair<string, string>> DeclaredIds()
        {
            return typeof(AchievementIds)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.IsLiteral && field.FieldType == typeof(string))
                .Select(field => new KeyValuePair<string, string>(field.Name, (string)field.GetRawConstantValue()));
        }

        /// <summary>An <see cref="IGameServices"/> that records the achievements it was asked to unlock.</summary>
        private sealed class RecordingGameServices : IGameServices
        {
            public readonly List<string> Unlocked = new List<string>();

            /// <summary>Latest step count reported per incremental achievement.</summary>
            public readonly Dictionary<string, int> Steps = new Dictionary<string, int>();

            /// <summary>Settable so a test can ask what happens with no gamer profile.</summary>
            public bool SignedIn = true;

            public bool IsSignedIn => SignedIn;

            public void SignIn(Action<bool> onComplete = null)
            {
                onComplete?.Invoke(true);
            }

            public void SignInInteractive(Action<bool> onComplete = null)
            {
                onComplete?.Invoke(true);
            }

            public void UnlockAchievement(string achievementId)
            {
                Unlocked.Add(achievementId);
            }

            public void SetAchievementSteps(string achievementId, int steps)
            {
                Steps[achievementId] = steps;
            }

            public void SubmitScore(string leaderboardId, long score)
            {
            }

            public void ShowLeaderboard(string leaderboardId, Action<bool> onClosed = null)
            {
                onClosed?.Invoke(true);
            }

            public void LoadLeaderboard(string leaderboardId, int rowCount, Action<LeaderboardEntry[]> onLoaded)
            {
                onLoaded?.Invoke(new LeaderboardEntry[0]);
            }

            public void RecordStat(string eventName, params (string key, object value)[] properties)
            {
            }

            public void FlushStats()
            {
            }

            public void LoadCloud(Action<string> onLoaded)
            {
                onLoaded?.Invoke(null);
            }

            public void SaveCloud(string data, long playedMs, Action onComplete = null)
            {
                onComplete?.Invoke();
            }
        }
    }
}
