using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins the fold gate (design §8): content may be authored to wait for a
    /// number of folds, so an early run walks a shorter trail and the deep
    /// zones arrive over runs instead of all at once.
    ///
    /// The tests that matter most here are the ones about the Rite. A Rite
    /// completes only when every verse in it is sung, a verse can only be sung
    /// once its zone is open, and completing the Rite is the ONLY way to
    /// migrate — so a verse for a zone this fold cannot reach would seal
    /// Migration permanently. Everything else in this file is ordinary gating;
    /// those are the ones guarding a save that could never progress again.
    /// </summary>
    public class FoldGateTests
    {
        private GameDataAsset _data;
        private RiteVerseData _sunfieldVerse;
        private RiteVerseData _marshVerse;
        private RiteVerseData _heathVerse;

        [SetUp]
        public void SetUp()
        {
            _data = ScriptableObject.CreateInstance<GameDataAsset>();
            _data.economy = new EconomyData
            {
                mastery = new EconomyData.MasteryData { yieldBonusPerLevel = 0.05 },
                verdure = new EconomyData.VerdureData { yieldBonusPerPoint = 0.02 },
                offline = new EconomyData.OfflineData { baseCapHours = 4, rateMultiplier = 1.0 },
                tending = new EconomyData.TendingData { burstYieldMult = 3.0, burstDurationSec = 5.0 },
                warden = new EconomyData.WardenData { gatherPerSecond = 0.5 },
                gifts = new EconomyData.GiftsData { pileGoods = 10 },
                kith = new EconomyData.KithData
                {
                    slotsBase = 1,
                    slotsMax = 6,
                    verseMilestones = new List<int> { 2, 5, 10 },
                },
            };
            _data.resources = new List<ResourceData>
            {
                new ResourceData { id = "berries", sellValue = 2, skill = "foraging" },
                new ResourceData { id = "peat", sellValue = 9, skill = "foraging" },
                new ResourceData { id = "heather", sellValue = 4, skill = "foraging" },
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
                // The gated trail: it does not exist until two folds are behind
                // the warden.
                new ZoneData
                {
                    id = "mistfen-marsh",
                    order = 2,
                    resources = new List<string> { "peat" },
                    unlocks = new List<string> { "foraging" },
                    minMigration = 2,
                },
                // An ungated zone AFTER the gated one in rite order, so the
                // sealing tests can prove a gated verse doesn't block what
                // follows it rather than passing because nothing followed.
                new ZoneData
                {
                    id = "open-heath",
                    order = 3,
                    resources = new List<string> { "heather" },
                    unlocks = new List<string> { "foraging" },
                },
            };
            _data.upgrades = new List<UpgradeData>
            {
                new UpgradeData
                {
                    order = 1, id = "map-mistfen",
                    effects = { new EffectData { type = EffectType.UnlockZone, zone = "mistfen-marsh" } },
                },
                new UpgradeData
                {
                    order = 2, id = "map-heath",
                    effects = { new EffectData { type = EffectType.UnlockZone, zone = "open-heath" } },
                },
            };
            _data.species = new List<SpeciesData>
            {
                new SpeciesData
                {
                    id = "meadow-vole", displayName = "meadow vole", roleLean = "gatherer",
                    trait = new TraitData
                    {
                        displayName = "Berry-wise", kind = "nodeYieldBonus", value = 0.4,
                        resources = new List<string> { "berries" },
                    },
                },
                // A specialist of the STARTING zone held back a fold — the case
                // the species gate exists for, since a species in a gated zone
                // is already unreachable for want of a node.
                new SpeciesData
                {
                    id = "osier-otter", displayName = "osier otter", roleLean = "gatherer",
                    minMigration = 1,
                    trait = new TraitData
                    {
                        displayName = "Heath-wise", kind = "nodeYieldBonus", value = 0.4,
                        resources = new List<string> { "heather" },
                    },
                },
            };
            _sunfieldVerse = new RiteVerseData
            {
                id = "verse-sunfield",
                zone = GameStateFactory.StartingZoneId,
                slots =
                {
                    new RiteSlotData { type = RiteSlotType.Resource, resource = "berries", amount = 10 },
                    new RiteSlotData { type = RiteSlotType.Deed, deed = "tend", count = 3, renownGrant = 50 },
                },
            };
            _marshVerse = new RiteVerseData
            {
                id = "verse-mistfen",
                zone = "mistfen-marsh",
                slots =
                {
                    new RiteSlotData { type = RiteSlotType.Resource, resource = "peat", amount = 10 },
                    new RiteSlotData { type = RiteSlotType.Deed, deed = "tend", count = 3, renownGrant = 50 },
                },
            };
            _heathVerse = new RiteVerseData
            {
                id = "verse-heath",
                zone = "open-heath",
                slots =
                {
                    new RiteSlotData { type = RiteSlotType.Resource, resource = "heather", amount = 10 },
                    new RiteSlotData { type = RiteSlotType.Deed, deed = "tend", count = 3, renownGrant = 50 },
                },
            };
            _data.rites = new RitesBundle
            {
                chooseCount = 1,
                rites = new List<RiteData>
                {
                    new RiteData
                    {
                        id = "first-rite", migration = 0,
                        verses = { _sunfieldVerse, _marshVerse, _heathVerse },
                    },
                },
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_data);
        }

        [Test]
        public void FoldGate_ReadsTheZoneGateThroughItsMapRung()
        {
            // The trail is gated in one place — the zone — and the rung that
            // opens it inherits that fold rather than repeating the number.
            Assert.That(Upgrades.FoldGate(_data, _data.upgrades[0]), Is.EqualTo(2));
            Assert.That(Upgrades.FoldGate(_data, _data.upgrades[1]), Is.EqualTo(0));
        }

        [Test]
        public void TryPurchase_RefusesAGatedTrailMap_UntilItsFold()
        {
            var state = GameStateFactory.NewGame(_data);
            var map = _data.upgrades[0];

            Assert.That(Upgrades.TryPurchase(state, _data, map), Is.False, "the marsh does not exist on run 1");
            Assert.That(state.HasUpgrade("map-mistfen"), Is.False);

            state.migrationCount = 1;
            Assert.That(Upgrades.TryPurchase(state, _data, map), Is.False, "nor on run 2");

            state.migrationCount = 2;

            Assert.That(Upgrades.TryPurchase(state, _data, map), Is.True, "two folds behind them, the marsh is there");
            Assert.That(Upgrades.UnlockedZoneIds(state, _data).Contains("mistfen-marsh"), Is.True);
        }

        [Test]
        public void FoldsUntilAvailable_CountsDownTheFoldsLeft()
        {
            var state = GameStateFactory.NewGame(_data);

            Assert.That(Upgrades.FoldsUntilAvailable(state, _data, _data.upgrades[0]), Is.EqualTo(2));

            state.migrationCount = 1;
            Assert.That(Upgrades.FoldsUntilAvailable(state, _data, _data.upgrades[0]), Is.EqualTo(1));

            state.migrationCount = 5;
            Assert.That(Upgrades.FoldsUntilAvailable(state, _data, _data.upgrades[0]), Is.EqualTo(0),
                "never negative — a gate long passed is simply open");
        }

        [Test]
        public void IsVerseInPlay_HoldsAGatedZonesVerseOutOfTheRite()
        {
            var state = GameStateFactory.NewGame(_data);

            Assert.That(Rite.IsVerseInPlay(state, _data, _sunfieldVerse), Is.True);
            Assert.That(Rite.IsVerseInPlay(state, _data, _marshVerse), Is.False, "the marsh's verse is not this run's");
            Assert.That(Rite.IsVerseInPlay(state, _data, _heathVerse), Is.True);
        }

        [Test]
        public void IsRiteComplete_IgnoresAGatedVerse_SoMigrationIsNeverSealed()
        {
            // The hard-lock this whole mechanism has to avoid: singing every
            // verse the run CAN reach must complete the Rite. If a gated verse
            // counted, the Rite would never complete, and since the Rite is the
            // Migration gate the save could never fold again — so it could never
            // reach the fold that would open the gated zone either.
            var state = GameStateFactory.NewGame(_data);
            GameStateFactory.SyncUnlockedZones(state, _data);
            Upgrades.TryPurchase(state, _data, _data.upgrades[1]);
            state.AddResource("berries", 10);
            state.AddResource("heather", 10);

            Rite.DeliverResource(state, _data, _sunfieldVerse, 0);
            Rite.DeliverResource(state, _data, _heathVerse, 0);

            Assert.That(Rite.IsVerseComplete(state, _data, _marshVerse), Is.False, "and it never will be this run");
            Assert.That(Rite.IsRiteComplete(state, _data), Is.True,
                "every verse the run could reach is sung, so the fold is earned");
        }

        [Test]
        public void IsVerseSealed_AGatedVerseDoesNotSealTheOnesBehindIt()
        {
            // Verses are sung in order, each sealed until the one before it is
            // answered. A verse that isn't in play never had a turn to take, so
            // it cannot be what a later verse is waiting on.
            var state = GameStateFactory.NewGame(_data);
            state.AddResource("berries", 10);
            Rite.DeliverResource(state, _data, _sunfieldVerse, 0);

            Assert.That(Rite.IsVerseComplete(state, _data, _sunfieldVerse), Is.True);
            Assert.That(Rite.IsVerseSealed(state, _data, _heathVerse), Is.False,
                "the marsh's verse sits between them but is not in play");
        }

        [Test]
        public void VersesInPlay_GrowsWithTheFold()
        {
            var state = GameStateFactory.NewGame(_data);

            Assert.That(Rite.VersesInPlay(state, _data).Count, Is.EqualTo(2), "run 1 walks the shorter Rite");

            state.migrationCount = 2;

            var later = Rite.VersesInPlay(state, _data);
            Assert.That(later.Count, Is.EqualTo(3), "the marsh joins the Rite on its fold");
            Assert.That(later[1].id, Is.EqualTo("verse-mistfen"), "and in its authored place, not appended");
        }

        [Test]
        public void IsVerseInPlay_KeepsTheVerseWhenTheZoneIsAlreadyOpen()
        {
            // The retuned-save case: opening a zone is never taken back, so a
            // run already holding the map keeps the verse rather than being left
            // with a zone it can work and a verse that doesn't count.
            var state = GameStateFactory.NewGame(_data);
            state.migrationCount = 2;
            Assert.That(Upgrades.TryPurchase(state, _data, _data.upgrades[0]), Is.True);

            state.migrationCount = 0;

            Assert.That(Rite.IsVerseInPlay(state, _data, _marshVerse), Is.True);
        }

        [Test]
        public void SpeciesAnswers_HoldsASpecialistUntilItsFold()
        {
            var state = GameStateFactory.NewGame(_data);
            var otter = _data.species[1];

            Assert.That(Gifts.SpeciesAnswers(state, otter), Is.False);
            Assert.That(Gifts.SpeciesAnswers(state, _data.species[0]), Is.True, "the ungated one always answers");

            state.migrationCount = 1;

            Assert.That(Gifts.SpeciesAnswers(state, otter), Is.True);
        }

        [Test]
        public void NodeCanCall_IsQuietAtANodeWhoseSpecialistIsGated()
        {
            // The line only shows where a pile could actually be answered, so a
            // gated specialist's node offers nothing rather than taking goods
            // for a familiar that won't come.
            var state = GameStateFactory.NewGame(_data);
            state.migrationCount = 2;
            Upgrades.TryPurchase(state, _data, _data.upgrades[1]);
            var heatherNode = state.nodes.Find(n => n.resourceId == "heather");
            Assert.That(heatherNode, Is.Not.Null, "the heath's node must exist, or this proves nothing");
            state.AddResource("heather", 100);

            Assert.That(Gifts.NodeCanCall(state, _data, heatherNode), Is.True, "fold 2 is past the otter's gate");

            state.migrationCount = 0;

            Assert.That(Gifts.NodeCanCall(state, _data, heatherNode), Is.False);
        }
    }
}
