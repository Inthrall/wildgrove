using System.Collections.Generic;
using BreakInfinity;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins the Rite runtime (design §7): verses reveal with their zones,
    /// offerings consume goods (or specimens, or dug fragments) and credit
    /// Renown — trade value for plain resources, authored grants (pro-rata)
    /// for everything else, deeds granting once — a verse completes at
    /// chooseCount slots, and the Rite completes only when every verse is
    /// sung.
    /// </summary>
    public class RiteTests
    {
        private const double Tolerance = 1e-9;

        private GameDataAsset _data;
        private RiteVerseData _sunfieldVerse;
        private RiteVerseData _brambleVerse;

        [SetUp]
        public void SetUp()
        {
            _data = ScriptableObject.CreateInstance<GameDataAsset>();
            _data.economy = new EconomyData
            {
                mastery = new EconomyData.MasteryData { yieldBonusPerLevel = 0.05 },
                verdure = new EconomyData.VerdureData { yieldBonusPerPoint = 0.02 },
                offline = new EconomyData.OfflineData { baseCapHours = 4, rateMultiplier = 1.0 },
                tending = new EconomyData.TendingData
                {
                    burstYieldMult = 3.0, burstDurationSec = 5.0,
                    pristineBonusDurationSec = 30.0, pristineChanceBonus = 1.0,
                },
                warden = new EconomyData.WardenData { gatherPerSecond = 0.5 },
            };
            _data.resources = new List<ResourceData>
            {
                new ResourceData { id = "berries", sellValue = 2 },
                new ResourceData { id = "nuts", sellValue = 3 },
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
                    id = "bramble-hedgerows",
                    order = 2,
                    resources = new List<string> { "nuts" },
                    unlocks = new List<string> { "firecraft" },
                },
            };
            _data.upgrades = new List<UpgradeData>
            {
                new UpgradeData
                {
                    order = 4, id = "map-bramble",
                    effects = { new EffectData { type = EffectType.UnlockZone, zone = "bramble-hedgerows" } },
                },
            };
            _data.insects = new List<InsectData>
            {
                new InsectData
                {
                    id = "stags-herald", sketches = 3,
                    habitats = new List<string> { "bramble-hedgerows" }, rarity = 1.0,
                },
                new InsectData
                {
                    id = "silver-skimmer", sketches = 4,
                    habitats = new List<string> { "bramble-hedgerows" }, rarity = 0.7,
                },
            };
            _sunfieldVerse = new RiteVerseData
            {
                id = "verse-sunfield",
                zone = GameStateFactory.StartingZoneId,
                slots =
                {
                    new RiteSlotData { type = RiteSlotType.Resource, resource = "berries", amount = 100 },
                    new RiteSlotData { type = RiteSlotType.Resource, resource = "copper-ingot", amount = 5, renownGrant = 375 },
                    new RiteSlotData { type = RiteSlotType.Deed, deed = "tend", count = 3, renownGrant = 50 },
                    new RiteSlotData { type = RiteSlotType.Specimen, quality = "fine", count = 1, renownGrant = 100 },
                    new RiteSlotData { type = RiteSlotType.Sketch, count = 1, renownGrant = 500 },
                },
            };
            _brambleVerse = new RiteVerseData
            {
                id = "verse-bramble",
                zone = "bramble-hedgerows",
                slots =
                {
                    new RiteSlotData { type = RiteSlotType.Resource, resource = "nuts", amount = 10 },
                    new RiteSlotData { type = RiteSlotType.Deed, deed = "tend", count = 5, renownGrant = 10 },
                },
            };
            _data.rites = new RitesBundle
            {
                chooseCount = 2,
                rites = new List<RiteData>
                {
                    new RiteData { id = "first-rite", migration = 0, verses = { _sunfieldVerse, _brambleVerse } },
                },
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_data);
        }

        [Test]
        public void IsVerseRevealed_FollowsTheZoneUnlock()
        {
            var state = GameStateFactory.NewGame(_data);

            Assert.That(Rite.IsVerseRevealed(state, _data, _sunfieldVerse), Is.True);
            Assert.That(Rite.IsVerseRevealed(state, _data, _brambleVerse), Is.False);

            state.purchasedUpgradeIds.Add("map-bramble");

            Assert.That(Rite.IsVerseRevealed(state, _data, _brambleVerse), Is.True);
        }

        [Test]
        public void Deliver_VerseAlreadyAnswered_TheUnchosenSlotsAreExpired()
        {
            // Any chooseCount slots answer the verse; the rest expire (§8) —
            // they must never keep eating stock or paying Renown.
            var state = GameStateFactory.NewGame(_data);
            state.AddResource("berries", 100);
            state.AddResource("copper-ingot", 5);
            Rite.DeliverResource(state, _data, _sunfieldVerse, 0);
            Rite.DeliverResource(state, _data, _sunfieldVerse, 1);
            Assert.That(Rite.IsVerseComplete(state, _data, _sunfieldVerse), Is.True);

            state.AddFine("berries", BigDouble.One);
            state.insectSketches["stags-herald"] = 2;
            var renownBefore = state.renown;

            Assert.That(Rite.DeliverSpecimen(state, _data, _sunfieldVerse, 3), Is.Null);
            Assert.That(Rite.DeliverSketch(state, _data, _sunfieldVerse, 4), Is.Null);
            Assert.That(state.GetFine("berries").ToDouble(), Is.EqualTo(1.0).Within(Tolerance),
                "an expired slot takes nothing");
            Assert.That(state.insectSketches["stags-herald"], Is.EqualTo(2));
            Assert.That(state.renown.ToDouble(), Is.EqualTo(renownBefore.ToDouble()).Within(Tolerance));
        }

        [Test]
        public void RecordDeed_VerseAlreadyAnswered_TheExpiredDeedSlotNeverGrants()
        {
            var state = GameStateFactory.NewGame(_data);
            state.AddResource("berries", 100);
            state.AddResource("copper-ingot", 5);
            Rite.DeliverResource(state, _data, _sunfieldVerse, 0);
            Rite.DeliverResource(state, _data, _sunfieldVerse, 1);
            var renownBefore = state.renown;

            for (var i = 0; i < 3; i++)
            {
                Rite.RecordDeed(state, _data, "tend");
            }

            Assert.That(Rite.SlotDelivered(state, _sunfieldVerse, 2), Is.EqualTo(0.0).Within(Tolerance),
                "an expired deed slot's progress is frozen");
            Assert.That(state.renown.ToDouble(), Is.EqualTo(renownBefore.ToDouble()).Within(Tolerance));
        }

        [Test]
        public void CanDeliver_TracksStockAndVerseState()
        {
            var state = GameStateFactory.NewGame(_data);

            Assert.That(Rite.CanDeliver(state, _data, _sunfieldVerse, 0), Is.False, "nothing held — nothing to set down");

            state.AddResource("berries", 10);
            Assert.That(Rite.CanDeliver(state, _data, _sunfieldVerse, 0), Is.True);

            state.AddResource("berries", 90);
            state.AddResource("copper-ingot", 5);
            Rite.DeliverResource(state, _data, _sunfieldVerse, 0);
            Rite.DeliverResource(state, _data, _sunfieldVerse, 1);
            state.AddResource("berries", 10);

            Assert.That(Rite.CanDeliver(state, _data, _sunfieldVerse, 0), Is.False, "the answered verse asks nothing more");
        }

        [Test]
        public void DeliverResource_ConsumesStockAndCreditsTradeValue()
        {
            var state = GameStateFactory.NewGame(_data);
            state.AddResource("berries", 40);

            var given = Rite.DeliverResource(state, _data, _sunfieldVerse, 0);

            // 40 of the 100 asked: consumed, tracked, worth 40 · 2 Renown
            // (full trade value — no double-tax, design §7).
            Assert.That(given.ToDouble(), Is.EqualTo(40.0).Within(Tolerance));
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(Rite.SlotDelivered(state, _sunfieldVerse, 0), Is.EqualTo(40.0).Within(Tolerance));
            Assert.That(state.renown.ToDouble(), Is.EqualTo(80.0).Within(Tolerance));
            Assert.That(Rite.IsSlotComplete(state, _sunfieldVerse, 0), Is.False);
        }

        [Test]
        public void DeliverResource_ClampsAtTheSlotTarget()
        {
            var state = GameStateFactory.NewGame(_data);
            state.AddResource("berries", 150);

            var given = Rite.DeliverResource(state, _data, _sunfieldVerse, 0);

            // Only the 100 the verse asks for leaves the camp.
            Assert.That(given.ToDouble(), Is.EqualTo(100.0).Within(Tolerance));
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(50.0).Within(Tolerance));
            Assert.That(Rite.IsSlotComplete(state, _sunfieldVerse, 0), Is.True);
            Assert.That(state.renown.ToDouble(), Is.EqualTo(200.0).Within(Tolerance));
        }

        [Test]
        public void DeliverResource_UnrevealedVerse_Refuses()
        {
            var state = GameStateFactory.NewGame(_data);
            state.AddResource("nuts", 50);

            var given = Rite.DeliverResource(state, _data, _brambleVerse, 0);

            Assert.That(given.ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(state.GetResource("nuts").ToDouble(), Is.EqualTo(50.0).Within(Tolerance));
        }

        [Test]
        public void DeliverResource_MaterialSlot_CreditsTheGrantProRata()
        {
            var state = GameStateFactory.NewGame(_data);
            state.AddResource("copper-ingot", 2);

            Rite.DeliverResource(state, _data, _sunfieldVerse, 1);

            // Materials have no trade value — the authored 375 grant pays out
            // 2/5 now…
            Assert.That(state.renown.ToDouble(), Is.EqualTo(150.0).Within(Tolerance));

            state.AddResource("copper-ingot", 3);
            Rite.DeliverResource(state, _data, _sunfieldVerse, 1);

            // …and the rest with the rest.
            Assert.That(state.renown.ToDouble(), Is.EqualTo(375.0).Within(Tolerance));
            Assert.That(Rite.IsSlotComplete(state, _sunfieldVerse, 1), Is.True);
        }

        [Test]
        public void DeliverSpecimen_ConsumesTheLargestMatchingPool()
        {
            var state = GameStateFactory.NewGame(_data);
            state.AddFine("berries", 2);
            state.AddFine("nuts", 5);

            var offered = Rite.DeliverSpecimen(state, _data, _sunfieldVerse, 3);

            Assert.That(offered, Is.EqualTo("nuts"));
            Assert.That(state.GetFine("nuts").ToDouble(), Is.EqualTo(4.0).Within(Tolerance));
            Assert.That(Rite.IsSlotComplete(state, _sunfieldVerse, 3), Is.True);
            Assert.That(state.renown.ToDouble(), Is.EqualTo(100.0).Within(Tolerance));
        }

        [Test]
        public void DeliverSpecimen_EmptyPool_ReturnsNull()
        {
            var state = GameStateFactory.NewGame(_data);
            state.AddPristine("berries", 3); // wrong quality — the slot wants fine

            Assert.That(Rite.DeliverSpecimen(state, _data, _sunfieldVerse, 3), Is.Null);
            Assert.That(state.renown.ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
        }

        [Test]
        public void DeliverSketch_TakesFromTheRichestUnrecordedPlate()
        {
            var state = GameStateFactory.NewGame(_data);
            state.insectSketches["stags-herald"] = 3; // assembled — untouchable
            state.insectSketches["silver-skimmer"] = 2;   // still assembling

            var offeredFrom = Rite.DeliverSketch(state, _data, _sunfieldVerse, 4);

            // The sacrifice comes from the dig chase, never a finished insect.
            Assert.That(offeredFrom, Is.EqualTo("silver-skimmer"));
            Assert.That(Insects.SketchCount(state, "silver-skimmer"), Is.EqualTo(1));
            Assert.That(Insects.SketchCount(state, "stags-herald"), Is.EqualTo(3));
            Assert.That(state.renown.ToDouble(), Is.EqualTo(500.0).Within(Tolerance));
        }

        [Test]
        public void DeliverSketch_NoLooseFragments_ReturnsNull()
        {
            var state = GameStateFactory.NewGame(_data);
            state.insectSketches["stags-herald"] = 3; // complete — not offerable

            Assert.That(Rite.DeliverSketch(state, _data, _sunfieldVerse, 4), Is.Null);
        }

        [Test]
        public void Tend_CountsAsADeed_AndTheGrantIsOneShot()
        {
            var state = GameStateFactory.NewGame(_data);
            var node = state.nodes[0];

            Simulation.Tend(state, _data, node);
            Simulation.Tend(state, _data, node);
            Assert.That(Rite.SlotDelivered(state, _sunfieldVerse, 2), Is.EqualTo(2.0).Within(Tolerance));
            Assert.That(state.renown.ToDouble(), Is.EqualTo(0.0).Within(Tolerance));

            Simulation.Tend(state, _data, node);
            Assert.That(Rite.IsSlotComplete(state, _sunfieldVerse, 2), Is.True);
            Assert.That(state.renown.ToDouble(), Is.EqualTo(50.0).Within(Tolerance));

            Simulation.Tend(state, _data, node);
            Assert.That(state.renown.ToDouble(), Is.EqualTo(50.0).Within(Tolerance), "the deed grant pays once");
        }

        [Test]
        public void Verse_CompletesAtChooseCountSlots()
        {
            var state = GameStateFactory.NewGame(_data);
            state.AddResource("berries", 100);

            Rite.DeliverResource(state, _data, _sunfieldVerse, 0);
            Assert.That(Rite.IsVerseComplete(state, _data, _sunfieldVerse), Is.False);

            var node = state.nodes[0];
            for (var i = 0; i < 3; i++)
            {
                Simulation.Tend(state, _data, node);
            }

            // Two of five slots filled — choose 2 (this fixture's chooseCount).
            Assert.That(Rite.IsVerseComplete(state, _data, _sunfieldVerse), Is.True);
        }

        [Test]
        public void Rite_CompletesOnlyWhenEveryVerseIsSung()
        {
            var state = GameStateFactory.NewGame(_data);
            state.AddResource("berries", 100);
            state.AddResource("copper-ingot", 5);
            Rite.DeliverResource(state, _data, _sunfieldVerse, 0);
            Rite.DeliverResource(state, _data, _sunfieldVerse, 1);
            Assert.That(Rite.IsVerseComplete(state, _data, _sunfieldVerse), Is.True);
            Assert.That(Rite.IsRiteComplete(state, _data), Is.False, "the bramble verse still waits");

            state.purchasedUpgradeIds.Add("map-bramble");
            GameStateFactory.SyncUnlockedZones(state, _data);
            state.AddResource("nuts", 10);
            Rite.DeliverResource(state, _data, _brambleVerse, 0);
            var node = state.nodes[0];
            for (var i = 0; i < 5; i++)
            {
                Simulation.Tend(state, _data, node);
            }

            Assert.That(Rite.IsVerseComplete(state, _data, _brambleVerse), Is.True);
            Assert.That(Rite.IsRiteComplete(state, _data), Is.True);
        }
    }
}
