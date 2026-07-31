using System;
using System.Collections.Generic;
using Wildgrove.Data;
using Wildgrove.Sim;

namespace Wildgrove.Game.Services
{
    /// <summary>
    /// Maps game state to the Play Games achievements it satisfies, dispatched
    /// through an injected <see cref="IGameServices"/>. Pure and side-effect-free
    /// but for the unlock calls — so the wiring is testable with a fake service,
    /// without standing up the <see cref="GameLoop"/> MonoBehaviour.
    /// <para>
    /// Every achievement is re-derived from whole state rather than fired at the
    /// moment it is earned. A one-shot celebration is the wrong place to grant
    /// one: a milestone reached while signed out drops its unlock, and the
    /// moment never comes again. Re-deriving costs nothing (Play dedupes
    /// unlocks, and steps are reported as an absolute figure), and it means a
    /// player who signs in for the first time after a hundred hours is given
    /// everything they have already done.
    /// </para>
    /// <para>
    /// The rules below are the other half of
    /// <c>store/play-games/achievements.json</c>, whose <c>anchor</c> field names
    /// the state each one reads. A test fails if any id in
    /// <see cref="AchievementIds"/> has no rule here, because an achievement
    /// nothing evaluates is invisible: it simply never unlocks, on anyone's
    /// device, with nothing in any log to say so.
    /// </para>
    /// </summary>
    public static class Achievements
    {
        /// <summary>Zone ids the ladder names directly (see <c>design/data/zones.json</c>).</summary>
        private const string HollowsZoneId = "the-hollows";
        private const string CloudreachZoneId = "cloudreach-peaks";

        /// <summary>The Kinship level "Well Known" asks for.</summary>
        private const int WellKnownKinshipLevel = 5;

        /// <summary>The Verdure "Verdant" asks the warden to be holding at once.</summary>
        private const double VerdantVerdure = 1000.0;

        /// <summary>
        /// One achievement and the state that decides it. <see cref="Steps"/> is
        /// zero for a plain unlock; anything higher makes it incremental, and
        /// the progress is reported as a step count Play draws as a bar.
        /// </summary>
        private readonly struct Rule
        {
            public readonly string Id;
            public readonly int Steps;
            public readonly Func<GameState, GameDataAsset, int> Progress;

            public Rule(string id, int steps, Func<GameState, GameDataAsset, int> progress)
            {
                Id = id;
                Steps = steps;
                Progress = progress;
            }
        }

        private static Rule Unlock(string id, Func<GameState, GameDataAsset, bool> earned)
        {
            return new Rule(id, 0, (state, data) => earned(state, data) ? 1 : 0);
        }

        private static Rule Count(string id, int steps, Func<GameState, GameDataAsset, int> progress)
        {
            return new Rule(id, steps, progress);
        }

        private static readonly Rule[] Rules =
        {
            // ── The first hour ────────────────────────────────────────────
            Unlock(AchievementIds.FirstHarvest, (s, d) => s.lifetimeGathered.Count > 0),
            Unlock(AchievementIds.FirstFriends, (s, d) => Kith.Count(s) >= 3),
            Unlock(AchievementIds.OffTheBeatenPath, (s, d) => Upgrades.UnlockedZoneIds(s, d).Count >= 2),
            Unlock(AchievementIds.FireAndFruit, (s, d) => s.lifetimeCrafted.Count > 0),
            Unlock(AchievementIds.GreenHands, (s, d) => AnyNodeReplanted(s)),

            // ── Gathering ─────────────────────────────────────────────────
            Unlock(AchievementIds.AFullBasket, (s, d) => GameStats.TotalGathered(s) >= 1e3),
            Unlock(AchievementIds.TheLongHaul, (s, d) => GameStats.TotalGathered(s) >= 1e5),
            Unlock(AchievementIds.LadenTrails, (s, d) => GameStats.TotalGathered(s) >= 1e7),
            Unlock(AchievementIds.TheGroveGives, (s, d) => GameStats.TotalGathered(s) >= 1e9),

            // ── Crafting ──────────────────────────────────────────────────
            Count(AchievementIds.TheWholeCampWorking, 3, (s, d) => s.stationsEverWorked.Count),
            Unlock(AchievementIds.SteadyHands, (s, d) => GameStats.TotalCrafted(s) >= 1e4),
            Unlock(AchievementIds.StoresOverflowing, (s, d) => GameStats.TotalCrafted(s) >= 1e6),

            // ── The kith ──────────────────────────────────────────────────
            Unlock(AchievementIds.FirstKith, (s, d) => EarnedBondCount(s, d) >= 1),
            Count(AchievementIds.ASecondBond, 2, EarnedBondCount),
            Unlock(AchievementIds.SixAtPost, (s, d) =>
            {
                // Guarded because hand-built fixtures carry no kith config, and
                // an unconfigured maximum of zero would otherwise read as met.
                var slots = Kith.SlotsMax(d);
                return slots > 0 && Kith.Walking(s) >= slots;
            }),
            Unlock(AchievementIds.WellKnown, (s, d) => BestKinshipLevel(s) >= WellKnownKinshipLevel),
            Unlock(AchievementIds.Inseparable, (s, d) =>
            {
                var milestones = SignatureMilestoneCount(d);
                return milestones > 0 && BestSignatureMilestones(s, d) >= milestones;
            }),
            Count(AchievementIds.TheWholeWood, 12, (s, d) => s.speciesEverBefriended.Count),

            // ── Compendium and Folio ──────────────────────────────────────
            Unlock(AchievementIds.Pristine, (s, d) => s.lifetimePristine.Count > 0),
            Unlock(AchievementIds.FixedInInk, (s, d) => s.fixedResources.Count > 0),
            Unlock(AchievementIds.ASpreadComplete, (s, d) => CompletedSpreadCount(s, d) >= 1),
            Unlock(AchievementIds.HalfTheFolio, (s, d) => CompletedSpreadCount(s, d) >= 5),
            Count(AchievementIds.TheWholeFolio, 9, CompletedSpreadCount),
            Count(AchievementIds.TheFullCabinet, 24, (s, d) => s.fixedResources.Count),

            // ── Observation ───────────────────────────────────────────────
            Unlock(AchievementIds.FirstSketch, (s, d) => s.insectSketches.Count > 0),
            Unlock(AchievementIds.APlateRecorded, (s, d) => RecordedPlateCount(s, d) >= 1),
            Count(AchievementIds.AllFivePlates, 5, RecordedPlateCount),
            Unlock(AchievementIds.SomethingOlder, (s, d) => s.deepAmberFound >= 1),
            Count(AchievementIds.TheDeepPages, 4, (s, d) => s.deepAmberFound),

            // ── The Rite ──────────────────────────────────────────────────
            Unlock(AchievementIds.FirstVerse, (s, d) => Kith.TotalVersesSung(s, d) >= 1),
            Unlock(AchievementIds.TheRiteComplete, (s, d) => Rite.IsRiteComplete(s, d)),
            Count(AchievementIds.FiveVerses, 5, (s, d) => Kith.TotalVersesSung(s, d)),
            Count(AchievementIds.TwentyFiveVerses, 25, (s, d) => Kith.TotalVersesSung(s, d)),

            // ── Migration ─────────────────────────────────────────────────
            Unlock(AchievementIds.TheFirstFold, (s, d) => s.migrationCount >= 1),
            Count(AchievementIds.ThreeFolds, 3, (s, d) => s.migrationCount),
            Count(AchievementIds.TenFolds, 10, (s, d) => s.migrationCount),
            Unlock(AchievementIds.Verdant, (s, d) => s.verdurePoints >= VerdantVerdure),

            // ── Trails and waystones ──────────────────────────────────────
            Count(AchievementIds.ReaderOfStones, 4, (s, d) => s.seenWaystoneZoneIds.Count),
            Count(AchievementIds.EveryStoneRead, 8, (s, d) => s.seenWaystoneZoneIds.Count),
            Unlock(AchievementIds.IntoTheHollows, (s, d) => Upgrades.UnlockedZoneIds(s, d).Contains(HollowsZoneId)),
            Unlock(AchievementIds.Cloudreach, (s, d) => Upgrades.UnlockedZoneIds(s, d).Contains(CloudreachZoneId)),

            // ── Camp and Almanac ──────────────────────────────────────────
            Unlock(AchievementIds.SteelInHand, HoldsSteel),
            Unlock(AchievementIds.TheAlmanacOpens, (s, d) => s.almanacNodeIds.Count >= 5),
            Count(AchievementIds.TheAlmanacComplete, 14, (s, d) => s.almanacNodeIds.Count),
            Count(AchievementIds.AFullerGrove, 5, BuiltCampLines),
        };

        /// <summary>
        /// Re-assert every achievement the current state satisfies. Idempotent:
        /// Play ignores an unlock it already holds, and a step count is set
        /// rather than added. Run it once sign-in completes and again on the
        /// save cadence, so a milestone crossed mid-session does not wait for
        /// the next launch.
        /// </summary>
        public static void Reassert(IGameServices services, GameState state, GameDataAsset data)
        {
            if (services == null || state == null || data == null || !services.IsSignedIn)
            {
                return;
            }

            foreach (var rule in Rules)
            {
                int progress;
                try
                {
                    progress = rule.Progress(state, data);
                }
                catch (Exception)
                {
                    // A rule reading a corner of state a hand-built fixture or a
                    // half-migrated save never filled must not take the other
                    // forty-four down with it.
                    continue;
                }

                if (progress <= 0)
                {
                    continue;
                }

                if (rule.Steps <= 0)
                {
                    services.UnlockAchievement(rule.Id);
                }
                else
                {
                    services.SetAchievementSteps(rule.Id, Math.Min(progress, rule.Steps));
                }
            }
        }

        /// <summary>Every achievement id this evaluates — the drift test's other side.</summary>
        public static IEnumerable<string> EvaluatedIds()
        {
            foreach (var rule in Rules)
            {
                yield return rule.Id;
            }
        }

        /// <summary>
        /// An incremental rule's step target, or 0 for a standard one. These
        /// numbers have to be hardcoded because the Play Console holds the same
        /// figure and the two must agree — which is exactly why the ones counting
        /// authored content are worth pinning against the data in a test. "Every
        /// Stone Read" sat at 8 while only seven zones had trail maps, so it could
        /// not fire on any device, and nothing anywhere said so.
        /// </summary>
        public static int StepTarget(string id)
        {
            foreach (var rule in Rules)
            {
                if (rule.Id == id)
                {
                    return rule.Steps;
                }
            }

            return 0;
        }

        private static bool AnyNodeReplanted(GameState state)
        {
            foreach (var node in state.nodes)
            {
                if (node.richnessLevel >= 1)
                {
                    return true;
                }
            }

            return false;
        }

        private static int EarnedBondCount(GameState state, GameDataAsset data)
        {
            var count = 0;
            foreach (var _ in Bonds.Earned(state, data))
            {
                count++;
            }

            return count;
        }

        private static int BestKinshipLevel(GameState state)
        {
            var best = 0;
            foreach (var familiar in state.roster)
            {
                var level = Kinship.Level(familiar);
                if (level > best)
                {
                    best = level;
                }
            }

            return best;
        }

        private static int SignatureMilestoneCount(GameDataAsset data)
        {
            var milestones = data.economy?.familiarXp?.signatureMilestones;
            return milestones?.Count ?? 0;
        }

        private static int BestSignatureMilestones(GameState state, GameDataAsset data)
        {
            var best = 0;
            foreach (var familiar in state.roster)
            {
                var passed = Kinship.SignatureMilestonesPassed(familiar, data);
                if (passed > best)
                {
                    best = passed;
                }
            }

            return best;
        }

        private static int CompletedSpreadCount(GameState state, GameDataAsset data)
        {
            var count = 0;
            foreach (var spread in data.folioSpreads)
            {
                if (Folio.IsSpreadComplete(state, spread))
                {
                    count++;
                }
            }

            return count;
        }

        private static int RecordedPlateCount(GameState state, GameDataAsset data)
        {
            var count = 0;
            foreach (var insect in data.insects)
            {
                if (Insects.IsRecorded(state, insect))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool HoldsSteel(GameState state, GameDataAsset data)
        {
            var tiers = data.economy?.tools?.tiers;
            if (tiers == null)
            {
                return false;
            }

            // The last tier is steel today; asking for the top of the ladder
            // rather than the name means a further tier moves this with it.
            return Upgrades.ToolTierIndex(state, data) >= tiers.Count - 1;
        }

        private static int BuiltCampLines(GameState state, GameDataAsset data)
        {
            var count = 0;
            foreach (var building in data.buildings)
            {
                if (Buildings.BoughtLevels(state, building.id) >= 1)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
