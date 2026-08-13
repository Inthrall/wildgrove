using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins the Amber economy layer (design §10): the wander surfaces it as a
    /// separate channel from field sketches (a fully-recorded site keeps producing),
    /// once for the round and flat — neither the site count nor the dig-speed
    /// stack touches the earn — unconfigured data draws no rng, and the time-skip sink credits full
    /// live-rate production for its cost — refused when short. IAP/ads are
    /// the plugin pass; the Rite can never be paid in Amber by construction.
    /// </summary>
    public class AmberTests
    {
        private const double Tolerance = 1e-9;

        /// <summary>A fixed "now" for the paid skip's budget settling — the fixture's cap is 0 (uncapped) unless a test sets one.</summary>
        private const long Now = 1_000_000_000_000L;

        private const long HourMs = 60L * 60L * 1000L;

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
                // The two rename prices are deliberately different numbers here,
                // so a test that read the wrong one could not pass by accident.
                // The naming prices are deliberately three different numbers
                // here, so a test that read the wrong knob could not pass by
                // accident: familiar 5, warden 20, camp 40.
                amber = new EconomyData.AmberData { digFindsPerHour = 36000, perFind = 2, timeSkipHours = 0.01, timeSkipCostAmber = 15, adDripAmber = 3, weeklyCacheAmber = 20, renameCostAmber = 5, wardenRenameCostAmber = 20, campNameCostAmber = 40, secondQueueCostAmber = 25 },
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

        private GameState StateWithAWatcher()
        {
            var state = GameStateFactory.NewGame(_data);
            state.digSites.Add(new DigSiteState { zoneId = GameStateFactory.StartingZoneId });
            TestKith.Station(state, Familiar.SketchStation(GameStateFactory.StartingZoneId), 1);
            return state;
        }

        [Test]
        public void Digging_SurfacesAmber()
        {
            var state = StateWithAWatcher();

            Simulation.Advance(state, _data, 1.0);

            Assert.That(state.amber, Is.EqualTo(2.0).Within(Tolerance), "one certain find, perFind amber");
        }

        [Test]
        public void FullyDugGround_KeepsSurfacingAmber()
        {
            var state = StateWithAWatcher();
            state.insectSketches["stags-herald"] = 3; // nothing left to find

            Simulation.Advance(state, _data, 1.0);

            Assert.That(state.amber, Is.EqualTo(2.0).Within(Tolerance),
                "amber is the dig's renewable — the insect channel falling quiet doesn't stop it");
        }

        /// <summary>
        /// Record every plate this site holds, so the sketch channel falls
        /// quiet and draws no rng — leaving the amber roll as the tick's only
        /// draw, which is what lets a test reason about it per sub-step.
        /// </summary>
        private static void SilenceTheSketchChannel(GameState state)
        {
            state.insectSketches["stags-herald"] = 3;
        }

        [Test]
        public void Digging_RollsOnceForTheRound_NotOncePerSite()
        {
            var state = StateWithAWatcher();
            state.digSites.Add(new DigSiteState { zoneId = "silverrun-river" });
            state.digSites.Add(new DigSiteState { zoneId = "the-hollows" });

            Simulation.Advance(state, _data, 1.0);

            Assert.That(state.amber, Is.EqualTo(2.0).Within(Tolerance),
                "one find for the round at perFind — the roll sat inside the site walk until 2026-08-02, so opening ground multiplied the earn and six sites paid six times over");
        }

        [Test]
        public void Digging_IgnoresTheDigSpeedStack()
        {
            // Half a find per 1 s sub-step, so Brush Screens' ×2 would push the
            // roll to a flat certainty: "did every single step pay out?" is then
            // an exact discriminator rather than a statistical one. Comparing a
            // stacked run against a plain one roll-for-roll is not available —
            // owning any upgrade shifts the run's rng sequence by itself.
            const double seconds = 60.0;
            _data.economy.amber.digFindsPerHour = 1800;
            _data.upgrades = new List<UpgradeData>
            {
                new UpgradeData
                {
                    order = 23, id = "brush-screens",
                    effects = { new EffectData { type = EffectType.DigSpeedMult, value = 2 } },
                },
            };

            var state = StateWithAWatcher();
            SilenceTheSketchChannel(state);
            state.purchasedUpgradeIds.Add("brush-screens");
            Assert.That(Upgrades.DigSpeedMultiplier(state, _data), Is.EqualTo(2.0).Within(Tolerance),
                "the watch stack is genuinely on, or the test below passes for the wrong reason");

            Simulation.Advance(state, _data, seconds);

            Assert.That(state.amber, Is.GreaterThan(0.0), "the channel is live");
            Assert.That(state.amber, Is.LessThan(seconds * _data.economy.amber.perFind),
                "the watch stack quickens sketching, never amber — it used to do both, and per-site × a multiplicative stack compounded into a login payout ~30x the design lean");
        }

        [Test]
        public void UnconfiguredAmber_BurnsNoRng()
        {
            _data.economy.amber = null;
            var state = StateWithAWatcher();
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

            var hours = Amber.TryTimeSkip(state, _data, Now);

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

            Assert.That(Amber.CanTimeSkip(state, _data, Now), Is.False);
            Assert.That(Amber.TryTimeSkip(state, _data, Now), Is.EqualTo(0.0));
            Assert.That(state.amber, Is.EqualTo(14.0).Within(Tolerance), "nothing spent on a refusal");
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
        }

        [Test]
        public void TimeSkip_RefusedWhenUnconfigured()
        {
            _data.economy.amber = null;
            var state = GameStateFactory.NewGame(_data);
            state.amber = 100.0;

            Assert.That(Amber.TryTimeSkip(state, _data, Now), Is.EqualTo(0.0));
        }

        [Test]
        public void TimeSkip_BudgetSpendsDownAndRefusesTheThird()
        {
            // Cap = exactly two skips' worth. The throttle is the whole point:
            // sim-time is the only thing amber buys, so this is the number
            // that bounds a heavy spender's pace (x2 a free player at 24).
            _data.economy.amber.timeSkipDailyCapHours = 0.02;
            var state = GameStateFactory.NewGame(_data);
            state.amber = 100.0;

            Assert.That(Amber.TryTimeSkip(state, _data, Now), Is.EqualTo(0.01).Within(Tolerance), "a full budget covers the first");
            Assert.That(Amber.TryTimeSkip(state, _data, Now), Is.EqualTo(0.01).Within(Tolerance), "and the second");
            Assert.That(Amber.CanTimeSkip(state, _data, Now), Is.False, "the day's budget is spent");
            Assert.That(Amber.TryTimeSkip(state, _data, Now), Is.EqualTo(0.0));
            Assert.That(state.amber, Is.EqualTo(70.0).Within(Tolerance), "two costs spent, never a third — refusal charges nothing");
        }

        [Test]
        public void TimeSkip_BudgetRefillsAtCapPer24Hours()
        {
            _data.economy.amber.timeSkipDailyCapHours = 0.02;
            var state = GameStateFactory.NewGame(_data);
            state.amber = 100.0;
            Amber.TryTimeSkip(state, _data, Now);
            Amber.TryTimeSkip(state, _data, Now);

            // The bucket leaks back at cap/24 per wall hour: half a day
            // refills half the cap — exactly one skip's worth here.
            var halfADayOn = Now + 12L * HourMs;
            Assert.That(Amber.CanTimeSkip(state, _data, halfADayOn - 1L), Is.False, "a moment short of one skip's refill");
            Assert.That(Amber.TryTimeSkip(state, _data, halfADayOn), Is.EqualTo(0.01).Within(Tolerance));
            Assert.That(Amber.CanTimeSkip(state, _data, halfADayOn), Is.False, "and it is spent again");
        }

        [Test]
        public void TimeSkip_BudgetStartsFullAndHoldsAtTheCap()
        {
            _data.economy.amber.timeSkipDailyCapHours = 0.02;
            var state = GameStateFactory.NewGame(_data);
            state.amber = 100.0;

            Assert.That(Amber.SkipBudgetHours(state, _data, Now), Is.EqualTo(0.02).Within(Tolerance),
                "an unstamped state reads a full budget");

            // Draining it and then waiting a hundred days refills to the cap,
            // never beyond — unspent days don't bank extra hastening.
            Amber.TryTimeSkip(state, _data, Now);
            Amber.TryTimeSkip(state, _data, Now);
            var muchLater = Now + 100L * 24L * HourMs;
            Assert.That(Amber.SkipBudgetHours(state, _data, muchLater), Is.EqualTo(0.02).Within(Tolerance));
        }

        [Test]
        public void SkipBudgetRemaining_CountsDownToOneSkipAndReadsZeroWhenCovered()
        {
            _data.economy.amber.timeSkipDailyCapHours = 0.02;
            var state = GameStateFactory.NewGame(_data);
            state.amber = 100.0;

            Assert.That(Amber.SkipBudgetRemainingMs(state, _data, Now), Is.EqualTo(0L), "a full budget waits for nothing");

            Amber.TryTimeSkip(state, _data, Now);
            Amber.TryTimeSkip(state, _data, Now);
            Assert.That(Amber.SkipBudgetRemainingMs(state, _data, Now), Is.EqualTo(12L * HourMs),
                "one skip's worth refills in half a day at cap 0.02");
            Assert.That(Amber.SkipBudgetRemainingMs(state, _data, Now + 12L * HourMs), Is.EqualTo(0L));
        }

        [Test]
        public void TimeSkip_UncappedWhenNoCapIsConfigured()
        {
            // The fixture ships cap 0 — the pre-cap behaviour, kept for data
            // that never opts in. Ten in a row must all land.
            var state = GameStateFactory.NewGame(_data);
            state.amber = 150.0;

            for (var i = 0; i < 10; i++)
            {
                Assert.That(Amber.TryTimeSkip(state, _data, Now), Is.EqualTo(0.01).Within(Tolerance), "skip " + i);
            }
        }

        [Test]
        public void TimeSkip_BudgetSurvivesMigration()
        {
            _data.economy.amber.timeSkipDailyCapHours = 0.02;
            var verse = new RiteVerseData
            {
                id = "verse-sunfield",
                zone = GameStateFactory.StartingZoneId,
                slots = { new RiteSlotData { type = RiteSlotType.Resource, resource = "berries", amount = 10 } },
            };
            _data.rites = new RitesBundle
            {
                chooseCount = 1,
                rites = new List<RiteData> { new RiteData { id = "first-rite", migration = 0, verses = { verse } } },
            };
            var state = GameStateFactory.NewGame(_data);
            state.AddResource("berries", 10);
            Rite.DeliverResource(state, _data, verse, 0);
            state.amber = 100.0;
            Amber.TryTimeSkip(state, _data, Now);
            Amber.TryTimeSkip(state, _data, Now);

            var next = Migration.Migrate(state, _data);

            Assert.That(next, Is.Not.Null, "the sung rite lets the fold happen at all");
            Assert.That(Amber.CanTimeSkip(next, _data, Now), Is.False,
                "folding is the one repeatable act a player controls — it must not refill the day's hastening");
        }

        [Test]
        public void Rename_ChargesTheAmberPriceWhenTheNameChanges()
        {
            var state = GameStateFactory.NewGame(_data);
            var familiar = Roster.Recruit(state, _data, "test-vole", null, "Pip");
            state.amber = 12.0;

            Assert.That(Amber.CanRename(state, _data), Is.True);
            var renamed = Amber.TryRename(state, _data, familiar, "Bramble");

            Assert.That(renamed, Is.True);
            Assert.That(familiar.name, Is.EqualTo("Bramble"));
            Assert.That(state.amber, Is.EqualTo(7.0).Within(Tolerance), "the 5-amber price is spent");
        }

        [Test]
        public void Rename_RefusedAndSpendsNothingWhenShort()
        {
            var state = GameStateFactory.NewGame(_data);
            var familiar = Roster.Recruit(state, _data, "test-vole", null, "Pip");
            state.amber = 4.0; // one short of the 5-amber price

            Assert.That(Amber.CanRename(state, _data), Is.False);
            Assert.That(Amber.TryRename(state, _data, familiar, "Bramble"), Is.False);
            Assert.That(familiar.name, Is.EqualTo("Pip"), "the name holds");
            Assert.That(state.amber, Is.EqualTo(4.0).Within(Tolerance), "nothing spent on a refusal");
        }

        [Test]
        public void Rename_UnchangedOrBlankNameSpendsNothing()
        {
            var state = GameStateFactory.NewGame(_data);
            var familiar = Roster.Recruit(state, _data, "test-vole", null, "Pip");
            state.amber = 12.0;

            Assert.That(Amber.TryRename(state, _data, familiar, "Pip"), Is.False, "no change, no charge");
            Assert.That(Amber.TryRename(state, _data, familiar, "  "), Is.False, "blank is a free no-op");
            Assert.That(state.amber, Is.EqualTo(12.0).Within(Tolerance), "nothing spent on a no-op");
        }

        [Test]
        public void Rename_IsFreeWhenAmberIsInert()
        {
            _data.economy.amber = null;
            var state = GameStateFactory.NewGame(_data);
            var familiar = Roster.Recruit(state, _data, "test-vole", null, "Pip");
            state.amber = 0.0;

            Assert.That(Amber.RenameCost(_data), Is.EqualTo(0.0));
            Assert.That(Amber.TryRename(state, _data, familiar, "Bramble"), Is.True,
                "no amber economy — a rename is free, never blocked");
            Assert.That(familiar.name, Is.EqualTo("Bramble"));
            Assert.That(state.amber, Is.EqualTo(0.0).Within(Tolerance));
        }

        [Test]
        public void WardenName_ReadsAsTheWardenUntilOneIsBought()
        {
            var state = GameStateFactory.NewGame(_data);

            Assert.That(Warden.IsNamed(state), Is.False);
            Assert.That(Warden.DisplayName(state), Is.EqualTo("the warden"),
                "an un-renamed run must read exactly as it did before naming existed");
            Assert.That(Warden.PossessiveName(state), Is.EqualTo("the warden's"));
        }

        [Test]
        public void WardenRename_ChargesItsOwnDearerPriceAndNamesEverySurface()
        {
            var state = GameStateFactory.NewGame(_data);
            state.amber = 25.0;

            Assert.That(Amber.CanRenameWarden(state, _data), Is.True);
            Assert.That(Amber.TryRenameWarden(state, _data, "Rowan"), Is.True);

            Assert.That(state.wardenName, Is.EqualTo("Rowan"));
            Assert.That(Warden.IsNamed(state), Is.True);
            Assert.That(Warden.DisplayName(state), Is.EqualTo("Rowan"));
            Assert.That(Warden.PossessiveName(state), Is.EqualTo("Rowan's"));
            Assert.That(state.amber, Is.EqualTo(5.0).Within(Tolerance),
                "the warden's own 20-amber price is spent, not the familiar's 5");
        }

        [Test]
        public void WardenRename_RefusedAndSpendsNothingWhenShort()
        {
            var state = GameStateFactory.NewGame(_data);
            state.amber = 19.0; // one short of the warden's 20-amber price

            Assert.That(Amber.CanRenameWarden(state, _data), Is.False);
            Assert.That(Amber.TryRenameWarden(state, _data, "Rowan"), Is.False);
            Assert.That(Warden.DisplayName(state), Is.EqualTo("the warden"), "the anonymity holds");
            Assert.That(state.amber, Is.EqualTo(19.0).Within(Tolerance), "nothing spent on a refusal");
        }

        [Test]
        public void WardenRename_AffordableAtTheFamiliarPriceIsStillRefused()
        {
            var state = GameStateFactory.NewGame(_data);
            state.amber = 6.0; // enough for a familiar's 5, nowhere near the warden's 20

            Assert.That(Amber.CanRename(state, _data), Is.True, "a companion is affordable here");
            Assert.That(Amber.TryRenameWarden(state, _data, "Rowan"), Is.False,
                "the warden's price must be read from its own knob, not the familiar's");
            Assert.That(state.amber, Is.EqualTo(6.0).Within(Tolerance));
        }

        [Test]
        public void WardenRename_UnchangedOrBlankNameSpendsNothing()
        {
            var state = GameStateFactory.NewGame(_data);
            state.amber = 60.0;

            Assert.That(Amber.TryRenameWarden(state, _data, "  "), Is.False, "blank is a free no-op");
            Assert.That(Amber.TryRenameWarden(state, _data, "the warden"), Is.False,
                "typing the anonymous name back is no change — and must never be charged for");

            Assert.That(Amber.TryRenameWarden(state, _data, "Rowan"), Is.True);
            Assert.That(Amber.TryRenameWarden(state, _data, "Rowan"), Is.False, "no change, no charge");
            Assert.That(Amber.TryRenameWarden(state, _data, " Rowan "), Is.False,
                "the same name in whitespace is still the same name");
            Assert.That(state.amber, Is.EqualTo(40.0).Within(Tolerance), "exactly one naming was paid for");
        }

        [Test]
        public void WardenRename_IsFreeWhenAmberIsInert()
        {
            _data.economy.amber = null;
            var state = GameStateFactory.NewGame(_data);
            state.amber = 0.0;

            Assert.That(Amber.WardenRenameCost(_data), Is.EqualTo(0.0));
            Assert.That(Amber.TryRenameWarden(state, _data, "Rowan"), Is.True,
                "no amber economy — naming is free, never blocked");
            Assert.That(Warden.DisplayName(state), Is.EqualTo("Rowan"));
        }

        [Test]
        public void WardenName_SurvivesTheFold()
        {
            // The same one-verse rite TimeSkip_BudgetSurvivesMigration stands up,
            // for the same reason: a fold only happens once a rite is sung.
            var verse = new RiteVerseData
            {
                id = "verse-sunfield",
                zone = GameStateFactory.StartingZoneId,
                slots = { new RiteSlotData { type = RiteSlotType.Resource, resource = "berries", amount = 10 } },
            };
            _data.rites = new RitesBundle
            {
                chooseCount = 1,
                rites = new List<RiteData> { new RiteData { id = "first-rite", migration = 0, verses = { verse } } },
            };
            var state = GameStateFactory.NewGame(_data);
            state.AddResource("berries", 10);
            Rite.DeliverResource(state, _data, verse, 0);
            state.amber = 25.0;
            Assert.That(Amber.TryRenameWarden(state, _data, "Rowan"), Is.True);

            var next = Migration.Migrate(state, _data);

            Assert.That(next, Is.Not.Null, "the sung rite lets the fold happen at all");
            Assert.That(Warden.DisplayName(next), Is.EqualTo("Rowan"),
                "a bought name crosses the fold — a warden does not forget their name by migrating");
        }

        /// <summary>An absence summary in hours — the ledger's input, as the welcome-back flow would hold it.</summary>
        private static OfflineSummary AbsenceOf(double realHours, double creditedHours)
        {
            return new OfflineSummary
            {
                realSeconds = realHours * 3600.0,
                creditedSeconds = creditedHours * 3600.0,
            };
        }

        [Test]
        public void Ledger_UncoveredHours_IsTheRemainderBeyondTheCap()
        {
            Assert.That(Amber.UncoveredHours(AbsenceOf(20.0, 12.0)), Is.EqualTo(8.0).Within(Tolerance));
            Assert.That(Amber.UncoveredHours(AbsenceOf(3.0, 3.0)), Is.EqualTo(0.0).Within(Tolerance),
                "a covered absence leaves nothing to settle");
            Assert.That(Amber.UncoveredHours(null), Is.EqualTo(0.0).Within(Tolerance));
        }

        [Test]
        public void Ledger_SettlesTheUncoveredHoursAtFullLiveRate()
        {
            var state = GameStateFactory.NewGame(_data);
            TestKith.Station(state, state.nodes[0].id, 1);
            state.amber = 100.0;

            var hours = Amber.TrySettleLedger(state, _data, 0.02, Now);

            Assert.That(hours, Is.EqualTo(0.02).Within(Tolerance), "the whole uncovered remainder is settled");
            Assert.That(state.amber, Is.EqualTo(70.0).Within(Tolerance),
                "priced pro-rata on the skip's own rate (15 per 0.01h) — the ledger is never a cheaper skip");
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(72.0).Within(Tolerance),
                "credited at the full live rate — no away cap, no offline multiplier");
        }

        [Test]
        public void Ledger_FractionOfAnAmberRoundsUpNeverDown()
        {
            var state = GameStateFactory.NewGame(_data);
            state.amber = 100.0;

            var hours = Amber.TrySettleLedger(state, _data, 0.005, Now);

            Assert.That(hours, Is.EqualTo(0.005).Within(Tolerance));
            Assert.That(state.amber, Is.EqualTo(92.0).Within(Tolerance),
                "7.5 amber owed reads as 8 — a fraction rounds against the buyer here, never silently under");
        }

        [Test]
        public void Ledger_HeldToTheSkipBudget()
        {
            // Cap = three skips' worth; two paid skips leave 0.01h of budget,
            // so a 0.02h ledger settles only what the budget still allows —
            // the x2 pace pin holds across both sinks with no second rule.
            _data.economy.amber.timeSkipDailyCapHours = 0.03;
            var state = GameStateFactory.NewGame(_data);
            state.amber = 100.0;
            Assert.That(Amber.TryTimeSkip(state, _data, Now), Is.GreaterThan(0.0));
            Assert.That(Amber.TryTimeSkip(state, _data, Now), Is.GreaterThan(0.0));

            var hours = Amber.TrySettleLedger(state, _data, 0.02, Now);

            Assert.That(hours, Is.EqualTo(0.01).Within(Tolerance), "the offer is held to the budget's remainder");
            Assert.That(state.amber, Is.EqualTo(55.0).Within(Tolerance), "two skips at 15 and a half-size settle at 15");
            Assert.That(Amber.SkipBudgetHours(state, _data, Now), Is.EqualTo(0.0).Within(Tolerance),
                "the settle drew down the same leaky bucket the skips did");
        }

        [Test]
        public void Ledger_RefusedWhenShortAndSpendsNothing()
        {
            var state = GameStateFactory.NewGame(_data);
            TestKith.Station(state, state.nodes[0].id, 1);
            state.amber = 29.0; // one short of the 0.02h settle's 30

            Assert.That(Amber.CanSettleLedger(state, _data, 0.02, Now), Is.False);
            Assert.That(Amber.TrySettleLedger(state, _data, 0.02, Now), Is.EqualTo(0.0));
            Assert.That(state.amber, Is.EqualTo(29.0).Within(Tolerance), "nothing spent on a refusal");
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(0.0).Within(Tolerance),
                "and no hours were credited");
        }

        [Test]
        public void Ledger_RefusedWhenUnconfigured()
        {
            _data.economy.amber = null;
            var state = GameStateFactory.NewGame(_data);
            state.amber = 100.0;

            Assert.That(Amber.TrySettleLedger(state, _data, 8.0, Now), Is.EqualTo(0.0));
        }

        [Test]
        public void SecondQueue_BuysOncePerRunAndSpendsTheAmber()
        {
            var state = GameStateFactory.NewGame(_data);
            state.amber = 60.0;

            Assert.That(Amber.CanBuySecondQueue(state, _data), Is.True);
            Assert.That(Amber.TryBuySecondQueue(state, _data), Is.True);
            Assert.That(state.secondQueueBought, Is.True);
            Assert.That(Crafting.OrderCapacity(state), Is.EqualTo(2));
            Assert.That(state.amber, Is.EqualTo(35.0).Within(Tolerance), "the queue's own 25-amber price is spent");

            Assert.That(Amber.CanBuySecondQueue(state, _data), Is.False, "the run already owns it");
            Assert.That(Amber.TryBuySecondQueue(state, _data), Is.False);
            Assert.That(state.amber, Is.EqualTo(35.0).Within(Tolerance), "never double-charged");
        }

        [Test]
        public void SecondQueue_RefusedWhenShortOrUnconfigured()
        {
            var state = GameStateFactory.NewGame(_data);
            state.amber = 24.0; // one short of the queue's 25-amber price

            Assert.That(Amber.CanBuySecondQueue(state, _data), Is.False);
            Assert.That(Amber.TryBuySecondQueue(state, _data), Is.False);
            Assert.That(state.amber, Is.EqualTo(24.0).Within(Tolerance), "nothing spent on a refusal");

            _data.economy.amber = null; // the sink unconfigured — the row hides, buying is refused
            state.amber = 100.0;
            Assert.That(Amber.TryBuySecondQueue(state, _data), Is.False);
        }

        [Test]
        public void CampName_ReadsAsTheCampUntilOneIsBought()
        {
            var state = GameStateFactory.NewGame(_data);

            Assert.That(Camp.IsNamed(state), Is.False);
            Assert.That(Camp.DisplayName(state), Is.EqualTo("the camp"),
                "an un-named run must read exactly as it did before naming existed");
        }

        [Test]
        public void CampNaming_ChargesItsOwnPriceAndNamesThePage()
        {
            var state = GameStateFactory.NewGame(_data);
            state.amber = 45.0;

            Assert.That(Amber.CanNameCamp(state, _data), Is.True);
            Assert.That(Amber.TryNameCamp(state, _data, "Thistledown"), Is.True);

            Assert.That(state.campName, Is.EqualTo("Thistledown"));
            Assert.That(Camp.DisplayName(state), Is.EqualTo("Thistledown"));
            Assert.That(state.amber, Is.EqualTo(5.0).Within(Tolerance),
                "the camp's own 40-amber price is spent — not the warden's 20, not the familiar's 5");
        }

        [Test]
        public void CampNaming_RefusedAndSpendsNothingWhenShort()
        {
            var state = GameStateFactory.NewGame(_data);
            state.amber = 39.0; // one short of the camp's 40-amber price

            Assert.That(Amber.CanNameCamp(state, _data), Is.False);
            Assert.That(Amber.TryNameCamp(state, _data, "Thistledown"), Is.False);
            Assert.That(Camp.DisplayName(state), Is.EqualTo("the camp"), "the anonymity holds");
            Assert.That(state.amber, Is.EqualTo(39.0).Within(Tolerance), "nothing spent on a refusal");
        }

        [Test]
        public void CampNaming_UnchangedOrBlankNameSpendsNothing()
        {
            var state = GameStateFactory.NewGame(_data);
            state.amber = 100.0;

            Assert.That(Amber.TryNameCamp(state, _data, "  "), Is.False, "blank is a free no-op");
            Assert.That(Amber.TryNameCamp(state, _data, "the camp"), Is.False,
                "typing the anonymous name back is no change — and must never be charged for");

            Assert.That(Amber.TryNameCamp(state, _data, "Thistledown"), Is.True);
            Assert.That(Amber.TryNameCamp(state, _data, "Thistledown"), Is.False, "no change, no charge");
            Assert.That(Amber.TryNameCamp(state, _data, " Thistledown "), Is.False,
                "the same name in whitespace is still the same name");
            Assert.That(state.amber, Is.EqualTo(60.0).Within(Tolerance), "exactly one naming was paid for");
        }

        [Test]
        public void CampNaming_IsFreeWhenAmberIsInert()
        {
            _data.economy.amber = null;
            var state = GameStateFactory.NewGame(_data);
            state.amber = 0.0;

            Assert.That(Amber.CampNameCost(_data), Is.EqualTo(0.0));
            Assert.That(Amber.TryNameCamp(state, _data, "Thistledown"), Is.True,
                "no amber economy — naming is free, never blocked");
            Assert.That(Camp.DisplayName(state), Is.EqualTo("Thistledown"));
        }

        [Test]
        public void CampName_SurvivesTheFold()
        {
            // The same one-verse rite WardenName_SurvivesTheFold stands up, and
            // since 2026-08-13 the two bought names cross together: this pinned
            // the camp's name being dropped until the name was made permanent.
            var verse = new RiteVerseData
            {
                id = "verse-sunfield",
                zone = GameStateFactory.StartingZoneId,
                slots = { new RiteSlotData { type = RiteSlotType.Resource, resource = "berries", amount = 10 } },
            };
            _data.rites = new RitesBundle
            {
                chooseCount = 1,
                rites = new List<RiteData> { new RiteData { id = "first-rite", migration = 0, verses = { verse } } },
            };
            var state = GameStateFactory.NewGame(_data);
            state.AddResource("berries", 10);
            Rite.DeliverResource(state, _data, verse, 0);
            state.amber = 45.0;
            Assert.That(Amber.TryNameCamp(state, _data, "Thistledown"), Is.True);

            var next = Migration.Migrate(state, _data);

            Assert.That(next, Is.Not.Null, "the sung rite lets the fold happen at all");
            Assert.That(Camp.DisplayName(next), Is.EqualTo("Thistledown"),
                "a bought name crosses the fold — the camp is re-pitched a region north, not replaced");
        }

        [Test]
        public void GrantDrip_CreditsTheConfiguredPileAndStamps()
        {
            var state = GameStateFactory.NewGame(_data);
            const long now = 1_000_000_000_000L;

            Assert.That(Amber.CanGrantDrip(state, _data, now), Is.True, "never claimed — ready now");
            var granted = Amber.GrantDrip(state, _data, now);

            Assert.That(granted, Is.EqualTo(3.0).Within(Tolerance));
            Assert.That(state.amber, Is.EqualTo(3.0).Within(Tolerance), "the rewarded-ad drip lands in the coffer");
            Assert.That(state.adDripClaimedUnixMs, Is.EqualTo(now), "the claim time is stamped so the cooldown holds");
        }

        [Test]
        public void GrantDrip_RefusedWithinTheCooldown()
        {
            var state = GameStateFactory.NewGame(_data);
            const long now = 1_000_000_000_000L;
            Amber.GrantDrip(state, _data, now);

            // A second claim a moment later — the only throttle once Remove Ads
            // drops the ad — must be refused and mint nothing.
            var again = now + Amber.AdDripCooldownMs - 1L;
            Assert.That(Amber.CanGrantDrip(state, _data, again), Is.False);
            Assert.That(Amber.GrantDrip(state, _data, again), Is.EqualTo(0.0));
            Assert.That(state.amber, Is.EqualTo(3.0).Within(Tolerance), "no second pile within the cooldown");

            // Once the cooldown elapses it re-arms.
            var later = now + Amber.AdDripCooldownMs;
            Assert.That(Amber.CanGrantDrip(state, _data, later), Is.True);
            Assert.That(Amber.GrantDrip(state, _data, later), Is.EqualTo(3.0).Within(Tolerance));
        }

        [Test]
        public void GrantDrip_RefusedWhenUnconfigured()
        {
            _data.economy.amber = null;
            var state = GameStateFactory.NewGame(_data);

            Assert.That(Amber.GrantDrip(state, _data, 1_000_000_000_000L), Is.EqualTo(0.0));
            Assert.That(state.amber, Is.EqualTo(0.0).Within(Tolerance));
        }

        [Test]
        public void AdDripCooldownRemaining_CountsDownAndReadsZeroWhenReady()
        {
            var state = GameStateFactory.NewGame(_data);
            const long now = 1_000_000_000_000L;

            Assert.That(Amber.AdDripCooldownRemainingMs(state, _data, now), Is.EqualTo(0L), "never claimed — nothing to wait for");

            Amber.GrantDrip(state, _data, now);
            var anHourOn = now + (60L * 60L * 1000L);
            Assert.That(Amber.AdDripCooldownRemainingMs(state, _data, anHourOn),
                Is.EqualTo(Amber.AdDripCooldownMs - (60L * 60L * 1000L)), "an hour of the cooldown has run");
            Assert.That(Amber.AdDripCooldownRemainingMs(state, _data, now + Amber.AdDripCooldownMs), Is.EqualTo(0L), "ready again");
            Assert.That(Amber.AdDripCooldownRemainingMs(state, _data, now + Amber.AdDripCooldownMs + 5_000L), Is.EqualTo(0L),
                "an overdue cooldown never counts backwards");
        }

        [Test]
        public void WeeklyCacheNextDue_CountsToTheWardensNextMonday_ClaimedOrNot()
        {
            var state = GameStateFactory.NewGame(_data);
            // Epoch day 11574 is a Sunday and `now` stands 1h 46m 40s into it at
            // offset 0, so the warden's week turns over 22h 13m 20s out.
            const long now = 1_000_000_000_000L;
            const long tillMonday = 80_000_000L;

            Assert.That(Amber.WeeklyCacheNextDueInMs(state, now), Is.EqualTo(tillMonday),
                "never claimed, and it still answers: the week is a calendar fact, not a cooldown "
                + "counted off a claim that may not have happened yet");

            Amber.ReceiveWeeklyCache(state, _data, now);
            Assert.That(Amber.WeeklyCacheNextDueInMs(state, now), Is.EqualTo(tillMonday),
                "and taking one does not move it — that is the whole point of a calendar week");
            Assert.That(Amber.WeeklyCacheNextDueInMs(state, now + tillMonday),
                Is.EqualTo(7L * 24L * 60L * 60L * 1000L),
                "the moment it turns, the next is a whole week out — never zero, so no surface "
                + "ever draws an empty countdown");
        }

        [Test]
        public void CooldownRemaining_IsZeroWhenAmberIsInert()
        {
            _data.economy.amber = null;
            var state = GameStateFactory.NewGame(_data);
            state.adDripClaimedUnixMs = 1L;
            state.weeklyCacheClaimedUnixMs = 1L;

            Assert.That(Amber.AdDripCooldownRemainingMs(state, _data, 1_000_000_000_000L), Is.EqualTo(0L));
            // The cache's week has nothing left to configure, so its inertness is
            // caught where it still counts: nothing is ever due.
            Assert.That(Amber.WeeklyCacheDue(state, _data, 1_000_000_000_000L), Is.False);
        }

        [Test]
        public void RewardedTimeSkip_CooldownGatesRepeatClaims()
        {
            var state = GameStateFactory.NewGame(_data);
            const long now = 1_000_000_000_000L;

            Assert.That(Amber.CanRewardedTimeSkip(state, now), Is.True, "never taken — ready now");
            Amber.StampRewardedTimeSkip(state, now);

            Assert.That(Amber.CanRewardedTimeSkip(state, now + Amber.TimeSkipCooldownMs - 1L), Is.False);
            Assert.That(Amber.CanRewardedTimeSkip(state, now + Amber.TimeSkipCooldownMs), Is.True);
        }

        [Test]
        public void WeeklyCache_FirstDeliveryGrantsAndStamps()
        {
            var state = GameStateFactory.NewGame(_data);
            const long now = 1_000_000_000_000L;

            Assert.That(Amber.WeeklyCacheDue(state, _data, now), Is.True, "none yet — due now");
            var granted = Amber.ReceiveWeeklyCache(state, _data, now);

            Assert.That(granted, Is.EqualTo(20.0).Within(Tolerance));
            Assert.That(state.amber, Is.EqualTo(20.0).Within(Tolerance));
            Assert.That(state.weeklyCacheClaimedUnixMs, Is.EqualTo(now), "the arrival time is stamped");
        }

        [Test]
        public void WeeklyCache_ReadsNotDueForTheRestOfTheWeekItWasTakenIn()
        {
            var state = GameStateFactory.NewGame(_data);
            // A Sunday (see WeeklyCacheNextDue_...), so this claim is the last of
            // its week and every hour left in it must still read "taken".
            const long now = 1_000_000_000_000L;
            Amber.ReceiveWeeklyCache(state, _data, now);

            var tillMonday = Amber.WeeklyCacheNextDueInMs(state, now);
            Assert.That(Amber.WeeklyCacheDue(state, _data, now + tillMonday - 1L), Is.False,
                "the last millisecond of the week it was taken in");
        }

        [Test]
        public void WeeklyCache_ComesDueAgainWhenTheWeekTurns_NotAWeekAfterTheClaim()
        {
            var state = GameStateFactory.NewGame(_data);
            const long now = 1_000_000_000_000L;
            Amber.ReceiveWeeklyCache(state, _data, now);

            // Taken late on a Sunday, so the next is hours away, not seven days.
            // A cooldown counted off the claim would have said Sunday again, and
            // the week after that a little later still.
            var monday = now + Amber.WeeklyCacheNextDueInMs(state, now);
            Assert.That(monday - now, Is.LessThan(24L * 60L * 60L * 1000L),
                "the calendar's Monday, not seven days off the stamp");
            Assert.That(Amber.WeeklyCacheDue(state, _data, monday), Is.True, "the cache comes due when the week turns");
            Assert.That(Amber.ReceiveWeeklyCache(state, _data, monday), Is.EqualTo(20.0).Within(Tolerance));
            Assert.That(state.amber, Is.EqualTo(40.0).Within(Tolerance), "two weeks, two caches");
        }

        [Test]
        public void WeeklyCache_HonoursAnEarlyDeliveryRatherThanDroppingIt()
        {
            // Play owns the once-a-week cadence now. If it hands one over early
            // that is Play's arithmetic — refusing it would lose a reward the
            // player was promised and can never be offered again.
            var state = GameStateFactory.NewGame(_data);
            const long now = 1_000_000_000_000L;
            Amber.ReceiveWeeklyCache(state, _data, now);

            // Later the same day, so still inside the week that was just claimed.
            var sameEvening = now + (6L * 60L * 60L * 1000L);
            Assert.That(Amber.WeeklyCacheDue(state, _data, sameEvening), Is.False, "the page still says it isn't due");
            Assert.That(Amber.ReceiveWeeklyCache(state, _data, sameEvening), Is.EqualTo(20.0).Within(Tolerance),
                "but a delivery is never turned away");
            Assert.That(state.amber, Is.EqualTo(40.0).Within(Tolerance));
            Assert.That(state.weeklyCacheClaimedUnixMs, Is.EqualTo(sameEvening), "and it re-stamps from the latest");
        }

        [Test]
        public void WeeklyCache_MintsNothingWhenUnconfigured()
        {
            _data.economy.amber = null;
            var state = GameStateFactory.NewGame(_data);

            Assert.That(Amber.ReceiveWeeklyCache(state, _data, 1_000_000_000_000L), Is.EqualTo(0.0));
            Assert.That(state.amber, Is.EqualTo(0.0).Within(Tolerance));
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
            var state = StateWithAWatcher();

            Simulation.Advance(state, _data, 1.0);

            Assert.That(state.amberFoundUnlogged, Is.EqualTo(2.0).Within(Tolerance),
                "the find is banked for GameLoop to report, alongside crediting the balance");
        }
    }
}
