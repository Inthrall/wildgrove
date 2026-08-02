using System.Collections.Generic;
using BreakInfinity;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins the Rite runtime (design §7): verses reveal with their zones,
    /// offerings are WHOLE — a slot takes its entire ask in one act or refuses,
    /// and nothing part-answered is banked for the next verse — consuming goods
    /// (or specimens, or torn sketches) and crediting Renown at trade value for
    /// plain resources, authored grants for everything else, deeds granting
    /// once. A verse completes at chooseCount slots, and the Rite completes only
    /// when every verse is sung.
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
                    choiceBonusDurationSec = 30.0, choiceChanceBonus = 1.0,
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
                    new RiteSlotData { type = RiteSlotType.Specimen, quality = "decent", count = 1, renownGrant = 100 },
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
        public void RequiredSlots_ClampsSoAVerseAlwaysKeepsAChoice()
        {
            // The ramp is authored against verses the generator widened in
            // step; a verse that couldn't widen must NOT become "fill every
            // slot", because a verse asking for a slot it hasn't got would seal
            // the Rite — and Migration with it — forever.
            _data.rites.generator = new RiteGeneratorConfigData
            {
                demandGrowth = 1.45,
                spotlightDiscount = 0.6,
                offSpotlightPremium = 1.5,
                chooseCountPerMigrations = 1,
                chooseCountMax = 20,
            };
            var state = GameStateFactory.NewGame(_data);

            Assert.That(Rite.RequiredSlots(state, _data, _sunfieldVerse), Is.EqualTo(2), "run 1 asks the authored count");

            state.migrationCount = 50;

            Assert.That(Rite.RequiredSlots(state, _data, _sunfieldVerse),
                Is.EqualTo(_sunfieldVerse.slots.Count - 1));
        }

        [Test]
        public void IsVerseComplete_WidensWithTheFold()
        {
            _data.rites.generator = new RiteGeneratorConfigData
            {
                demandGrowth = 1.45,
                spotlightDiscount = 0.6,
                offSpotlightPremium = 1.5,
                chooseCountPerMigrations = 2,
                chooseCountMax = 3,
            };
            var state = GameStateFactory.NewGame(_data);
            state.AddResource("berries", 100);
            state.AddResource("copper-ingot", 5);
            Rite.DeliverResource(state, _data, _sunfieldVerse, 0);
            Rite.DeliverResource(state, _data, _sunfieldVerse, 1);

            Assert.That(Rite.IsVerseComplete(state, _data, _sunfieldVerse), Is.True, "two answered, two asked");

            // The same two deliveries no longer answer a wider fold's verse.
            state.migrationCount = 2;

            Assert.That(Rite.IsVerseComplete(state, _data, _sunfieldVerse), Is.False);
        }

        [Test]
        public void IsVerseRevealed_NeedsTheZoneAndEveryEarlierVerseSung()
        {
            var state = GameStateFactory.NewGame(_data);

            Assert.That(Rite.IsVerseRevealed(state, _data, _sunfieldVerse), Is.True);
            Assert.That(Rite.IsVerseRevealed(state, _data, _brambleVerse), Is.False);

            state.purchasedUpgradeIds.Add("map-bramble");

            Assert.That(Rite.IsVerseRevealed(state, _data, _brambleVerse), Is.False,
                "the site is reached, but the sunfield verse is still unsung");
            Assert.That(Rite.IsVerseSealed(state, _data, _brambleVerse), Is.True);

            state.AddResource("berries", 100);
            state.AddResource("copper-ingot", 5);
            Rite.DeliverResource(state, _data, _sunfieldVerse, 0);
            Rite.DeliverResource(state, _data, _sunfieldVerse, 1);

            Assert.That(Rite.IsVerseRevealed(state, _data, _brambleVerse), Is.True);
            Assert.That(Rite.IsVerseSealed(state, _data, _brambleVerse), Is.False);
        }

        [Test]
        public void DeliverResource_SealedVerse_Refuses()
        {
            // Verses are answered in order — reaching the bramble site
            // doesn't open its verse while the sunfield verse is unsung.
            var state = GameStateFactory.NewGame(_data);
            state.purchasedUpgradeIds.Add("map-bramble");
            GameStateFactory.SyncUnlockedZones(state, _data);
            state.AddResource("nuts", 50);

            var given = Rite.DeliverResource(state, _data, _brambleVerse, 0);

            Assert.That(given.ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(state.GetResource("nuts").ToDouble(), Is.EqualTo(50.0).Within(Tolerance));
        }

        [Test]
        public void CompletingAVerse_UnsealsTheNext_WhoseDeedSlotStartsFromItsReveal()
        {
            var state = GameStateFactory.NewGame(_data);
            state.purchasedUpgradeIds.Add("map-bramble");
            GameStateFactory.SyncUnlockedZones(state, _data);

            var node = state.nodes[0];
            for (var i = 0; i < 5; i++)
            {
                Simulation.Tend(state, _data, node);
            }

            // Tending completed sunfield's deed slot; berries answer the verse.
            state.AddResource("berries", 100);
            Rite.DeliverResource(state, _data, _sunfieldVerse, 0);

            Assert.That(Rite.IsVerseComplete(state, _data, _sunfieldVerse), Is.True);
            Assert.That(Rite.IsVerseRevealed(state, _data, _brambleVerse), Is.True);
            Assert.That(Rite.SlotDelivered(state, _brambleVerse, 1), Is.EqualTo(0.0).Within(Tolerance),
                "the bramble verse is baselined where it reveals — the sunfield's tending was answered once already");

            Simulation.Tend(state, _data, node);

            Assert.That(Rite.SlotDelivered(state, _brambleVerse, 1), Is.EqualTo(1.0).Within(Tolerance),
                "tending after the reveal counts towards the verse that asked for it");
        }

        [Test]
        public void RecordDeed_CompletingAVerseMidSync_DoesNotHandTheNextOneTheRunsWholeTally()
        {
            // The sibling test above completes a verse with an OFFERING, which
            // re-enters the sync from the top and baselines the verse that opens.
            // A DEED completing it unseals the next verse *inside* the sync loop,
            // after the baseline pass has already been and gone — so the newly
            // opened verse used to be measured against an unset baseline of zero
            // and inherit every deed the run had ever done.
            _sunfieldVerse.slots[2].count = 25;
            var state = GameStateFactory.NewGame(_data);
            state.purchasedUpgradeIds.Add("map-bramble");
            GameStateFactory.SyncUnlockedZones(state, _data);

            // One slot answered by offering; the deed slot will be the second.
            state.AddResource("berries", 100);
            Rite.DeliverResource(state, _data, _sunfieldVerse, 0);
            for (var i = 0; i < 24; i++)
            {
                Rite.RecordDeed(state, _data, "tend");
            }

            Assert.That(Rite.IsVerseComplete(state, _data, _sunfieldVerse), Is.False, "one tend short");
            Assert.That(Rite.IsVerseRevealed(state, _data, _brambleVerse), Is.False, "still sealed, so still unbaselined");
            var renownBefore = state.renown;

            // The tend that answers the sunfield verse and opens the bramble one
            // in the same pass. The bramble verse asks for 5 tends; the run has
            // now done 25.
            Rite.RecordDeed(state, _data, "tend");

            Assert.That(Rite.IsVerseComplete(state, _data, _sunfieldVerse), Is.True);
            Assert.That(Rite.IsVerseRevealed(state, _data, _brambleVerse), Is.True);
            Assert.That(Rite.SlotDelivered(state, _brambleVerse, 1), Is.EqualTo(0.0).Within(Tolerance),
                "the verse that just opened counts from its reveal, not from the run's whole tally");
            Assert.That(state.renown.ToDouble(),
                Is.EqualTo((renownBefore + _sunfieldVerse.slots[2].renownGrant).ToDouble()).Within(Tolerance),
                "only the sunfield deed slot's grant — the bramble slot was never filled, so it was never paid");

            Rite.RecordDeed(state, _data, "tend");

            Assert.That(Rite.SlotDelivered(state, _brambleVerse, 1), Is.EqualTo(1.0).Within(Tolerance),
                "and the tends after the reveal are its own");
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

            state.AddDecent("berries", BigDouble.One);
            state.insectSketches["stags-herald"] = 2;
            var renownBefore = state.renown;

            Assert.That(Rite.DeliverSpecimen(state, _data, _sunfieldVerse, 3), Is.Null);
            Assert.That(Rite.DeliverSketch(state, _data, _sunfieldVerse, 4), Is.Null);
            Assert.That(state.GetDecent("berries").ToDouble(), Is.EqualTo(1.0).Within(Tolerance),
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
        public void CanDeliver_NeedsTheWholeAskInHand()
        {
            var state = GameStateFactory.NewGame(_data);

            Assert.That(Rite.CanDeliver(state, _data, _sunfieldVerse, 0), Is.False, "nothing held — nothing to set down");

            state.AddResource("berries", 99);
            Assert.That(Rite.CanDeliver(state, _data, _sunfieldVerse, 0), Is.False,
                "one berry short of the ask is not an offering");

            state.AddResource("berries", 1);
            Assert.That(Rite.CanDeliver(state, _data, _sunfieldVerse, 0), Is.True, "the whole ask is in the stores");

            state.AddResource("copper-ingot", 5);
            Rite.DeliverResource(state, _data, _sunfieldVerse, 0);
            Rite.DeliverResource(state, _data, _sunfieldVerse, 1);
            state.AddResource("berries", 100);

            Assert.That(Rite.CanDeliver(state, _data, _sunfieldVerse, 0), Is.False, "the answered verse asks nothing more");
        }

        [Test]
        public void DeliverResource_ConsumesTheWholeAskAndCreditsTradeValue()
        {
            var state = GameStateFactory.NewGame(_data);
            state.AddResource("berries", 100);

            var given = Rite.DeliverResource(state, _data, _sunfieldVerse, 0);

            // All 100 asked, in one act: consumed, tracked, worth 100 · 2 Renown
            // (full trade value — no double-tax, design §7).
            Assert.That(given.ToDouble(), Is.EqualTo(100.0).Within(Tolerance));
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(Rite.SlotDelivered(state, _sunfieldVerse, 0), Is.EqualTo(100.0).Within(Tolerance));
            Assert.That(state.renown.ToDouble(), Is.EqualTo(200.0).Within(Tolerance));
            Assert.That(Rite.IsSlotComplete(state, _sunfieldVerse, 0), Is.True);
        }

        [Test]
        public void DeliverResource_ShortOfTheWholeAsk_TakesNothing()
        {
            // The site takes a whole offering or none: a store short of the ask
            // buys no progress, so nothing is consumed and nothing is banked for
            // a later top-up (or for the verses behind this one) to inherit.
            var state = GameStateFactory.NewGame(_data);
            state.AddResource("berries", 99);

            var given = Rite.DeliverResource(state, _data, _sunfieldVerse, 0);

            Assert.That(given.ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(99.0).Within(Tolerance));
            Assert.That(Rite.SlotDelivered(state, _sunfieldVerse, 0), Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(Rite.SlotRemaining(state, _sunfieldVerse, 0), Is.EqualTo(100.0).Within(Tolerance));
            Assert.That(state.renown.ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
        }

        [Test]
        public void SlotInHand_IsWhatTheCampHolds_ClampedToTheAsk()
        {
            // The journal row reads "have / asked" — a fuller store must not
            // read "150 / 100", and a deed row still shows the work counted.
            var state = GameStateFactory.NewGame(_data);

            Assert.That(Rite.SlotInHand(state, _data, _sunfieldVerse, 0), Is.EqualTo(0.0).Within(Tolerance));

            state.AddResource("berries", 40);
            Assert.That(Rite.SlotInHand(state, _data, _sunfieldVerse, 0), Is.EqualTo(40.0).Within(Tolerance));

            state.AddResource("berries", 110);
            Assert.That(Rite.SlotInHand(state, _data, _sunfieldVerse, 0), Is.EqualTo(100.0).Within(Tolerance));

            Simulation.Tend(state, _data, state.nodes[0]);
            Assert.That(Rite.SlotInHand(state, _data, _sunfieldVerse, 2), Is.EqualTo(1.0).Within(Tolerance));
        }

        [Test]
        public void AnsweringAVerse_LeavesTheNextOneAskingInFull()
        {
            // No progress passes over: the bramble verse's own ask stands
            // untouched however much was set down in the sunfield's.
            var state = GameStateFactory.NewGame(_data);
            state.purchasedUpgradeIds.Add("map-bramble");
            GameStateFactory.SyncUnlockedZones(state, _data);
            state.AddResource("berries", 100);
            state.AddResource("copper-ingot", 5);
            Rite.DeliverResource(state, _data, _sunfieldVerse, 0);
            Rite.DeliverResource(state, _data, _sunfieldVerse, 1);
            Assert.That(Rite.IsVerseComplete(state, _data, _sunfieldVerse), Is.True);

            Assert.That(Rite.SlotDelivered(state, _brambleVerse, 0), Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(Rite.SlotRemaining(state, _brambleVerse, 0), Is.EqualTo(10.0).Within(Tolerance));

            state.AddResource("nuts", 9);

            Assert.That(Rite.CanDeliver(state, _data, _brambleVerse, 0), Is.False,
                "nine of the ten asked answers nothing here either");
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
        public void DeliverResource_MaterialSlot_CreditsTheWholeGrantAtOnce()
        {
            var state = GameStateFactory.NewGame(_data);
            state.AddResource("copper-ingot", 2);

            Rite.DeliverResource(state, _data, _sunfieldVerse, 1);

            // Two of the five asked: refused outright, so no share of the grant.
            Assert.That(state.renown.ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(state.GetResource("copper-ingot").ToDouble(), Is.EqualTo(2.0).Within(Tolerance));

            state.AddResource("copper-ingot", 3);
            Rite.DeliverResource(state, _data, _sunfieldVerse, 1);

            // Materials have no trade value — the authored 375 grant pays whole,
            // once the whole offering is made.
            Assert.That(state.renown.ToDouble(), Is.EqualTo(375.0).Within(Tolerance));
            Assert.That(Rite.IsSlotComplete(state, _sunfieldVerse, 1), Is.True);
        }

        [Test]
        public void DeliverSpecimen_ConsumesTheLargestMatchingPool()
        {
            var state = GameStateFactory.NewGame(_data);
            state.AddDecent("berries", 2);
            state.AddDecent("nuts", 5);

            var offered = Rite.DeliverSpecimen(state, _data, _sunfieldVerse, 3);

            Assert.That(offered, Is.EqualTo("nuts"));
            Assert.That(state.GetDecent("nuts").ToDouble(), Is.EqualTo(4.0).Within(Tolerance));
            Assert.That(Rite.IsSlotComplete(state, _sunfieldVerse, 3), Is.True);
            Assert.That(state.renown.ToDouble(), Is.EqualTo(100.0).Within(Tolerance));
        }

        [Test]
        public void DeliverSpecimen_ShortOfTheWholeCount_Refuses()
        {
            // A count slot is whole too: one perfect find answers nothing where
            // two are asked, and when both go the grant pays exactly once over.
            _sunfieldVerse.slots[3].count = 2;
            var state = GameStateFactory.NewGame(_data);
            state.AddDecent("berries", 1);

            Assert.That(Rite.DeliverSpecimen(state, _data, _sunfieldVerse, 3), Is.Null);
            Assert.That(state.GetDecent("berries").ToDouble(), Is.EqualTo(1.0).Within(Tolerance));
            Assert.That(state.renown.ToDouble(), Is.EqualTo(0.0).Within(Tolerance));

            state.AddDecent("nuts", 1);

            Assert.That(Rite.DeliverSpecimen(state, _data, _sunfieldVerse, 3), Is.Not.Null);
            Assert.That(Rite.IsSlotComplete(state, _sunfieldVerse, 3), Is.True);
            Assert.That(state.GetDecent("berries").ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(state.GetDecent("nuts").ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(state.renown.ToDouble(), Is.EqualTo(100.0).Within(Tolerance));
        }

        [Test]
        public void DeliverSpecimen_EmptyPool_ReturnsNull()
        {
            var state = GameStateFactory.NewGame(_data);
            state.AddChoice("berries", 3); // wrong quality — the slot wants decent

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
