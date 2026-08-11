using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins the kith's slot ladder (design §4): one slot from minute one,
    /// three earned the first time each NAMED verse is sung, two more from the
    /// store. Slots cap who holds a post — the roster itself is the collection
    /// (one familiar per species, ever) and companions past the slots rest at
    /// camp.
    /// <para>
    /// The ladder read a lifetime verse TALLY until 2026-08-11. The cases that
    /// matter most here are the ones that separate the two: a warden with a
    /// pile of verses sung and none of the named ones earns nothing, and the
    /// fold never takes an earned place back.
    /// </para>
    /// </summary>
    public class KithTests
    {
        private GameDataAsset _data;

        [SetUp]
        public void SetUp()
        {
            _data = ScriptableObject.CreateInstance<GameDataAsset>();
            _data.economy = new EconomyData
            {
                kith = new EconomyData.KithData
                {
                    slotsBase = 1,
                    slotsMax = 6,
                    slotVerseZones = new List<string> { "hedgerow", "marsh", "crags" },
                },
            };
            _data.resources = new List<ResourceData>
            {
                new ResourceData { id = "berries", sellValue = 2, skill = "foraging" },
                new ResourceData { id = "nuts", sellValue = 3, skill = "foraging" },
            };
            _data.zones = new List<ZoneData>
            {
                new ZoneData
                {
                    id = GameStateFactory.StartingZoneId,
                    order = 1,
                    resources = new List<string> { "berries", "nuts" },
                    unlocks = new List<string> { "foraging" },
                },
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_data);
        }

        [Test]
        public void Slots_FreshRun_HasTheSingleOpeningSlot()
        {
            var state = GameStateFactory.NewGame(_data);

            Assert.That(Kith.Slots(state, _data), Is.EqualTo(1));
            Assert.That(Kith.Count(state), Is.EqualTo(0), "no one at birth — the kith arrives through play");
            Assert.That(Kith.Walking(state), Is.EqualTo(0));
            Assert.That(Kith.HasRoom(state, _data), Is.True, "the opening slot waits for the first friend");
        }

        [Test]
        public void Slots_NamedVersesSung_OpenTheEarnedRungs()
        {
            var state = GameStateFactory.NewGame(_data);

            state.sungVerseZones.Add("hedgerow");
            Assert.That(Kith.Slots(state, _data), Is.EqualTo(2));

            state.sungVerseZones.Add("marsh");
            Assert.That(Kith.Slots(state, _data), Is.EqualTo(3));

            state.sungVerseZones.Add("crags");
            Assert.That(Kith.Slots(state, _data), Is.EqualTo(4), "every earned rung - the rest belong to the store");
        }

        [Test]
        public void Slots_OrderTheNamedVersesLandIn_DoesNotMatter()
        {
            var state = GameStateFactory.NewGame(_data);

            // The list is authored early/mid/late, but nothing enforces that a
            // warden meets them in that order - a zone can open ahead of its
            // turn off a bought tool tier. Each verse is worth its own place
            // whenever it lands.
            state.sungVerseZones.Add("crags");
            Assert.That(Kith.Slots(state, _data), Is.EqualTo(2));
        }

        [Test]
        public void Slots_VersesThatOpenNoPlace_EarnNothing()
        {
            var state = GameStateFactory.NewGame(_data);
            state.foldedVersesSung = 99;
            state.sungVerseZones.Add("sunfield-meadow");
            state.sungVerseZones.Add("old-growth-wood");

            Assert.That(Kith.Slots(state, _data), Is.EqualTo(1),
                "a hundred verses sung and none of them a named one - the ladder is not a tally any more, "
                + "which is the whole of the 2026-08-11 change");
        }

        [Test]
        public void Slots_ASungVerseIsNeverUnsung()
        {
            var state = GameStateFactory.NewGame(_data);
            state.sungVerseZones.Add("hedgerow");
            state.sungVerseZones.Add("hedgerow");

            Assert.That(Kith.Slots(state, _data), Is.EqualTo(2),
                "and a duplicate in the record is still one verse, so still one place");
        }

        [Test]
        public void Slots_GrandfatheredPlaces_AreAFloorTheNamedVersesOvertake()
        {
            var state = GameStateFactory.NewGame(_data);

            // What save rung 52 writes for a save that had earned two places
            // under the old lifetime tally: it cannot know WHICH verses were
            // sung, only how many places were standing.
            state.grandfatheredKithSlots = 2;
            Assert.That(Kith.Slots(state, _data), Is.EqualTo(3), "no save comes back with fewer places");

            state.sungVerseZones.Add("hedgerow");
            Assert.That(Kith.Slots(state, _data), Is.EqualTo(3),
                "the first named verse is inside the floor, so it adds nothing yet");

            state.sungVerseZones.Add("marsh");
            state.sungVerseZones.Add("crags");
            Assert.That(Kith.Slots(state, _data), Is.EqualTo(4), "and the floor is overtaken, never added to");
        }

        [Test]
        public void Slots_PurchasedSlots_StackOnTheEarnedLadder()
        {
            var state = GameStateFactory.NewGame(_data);
            state.sungVerseZones.AddRange(new[] { "hedgerow", "marsh", "crags" });

            state.purchasedKithSlots = 1;
            Assert.That(Kith.Slots(state, _data), Is.EqualTo(5));

            state.purchasedKithSlots = 2;
            Assert.That(Kith.Slots(state, _data), Is.EqualTo(6));
        }

        [Test]
        public void Slots_NeverClimbAboveSlotsMax()
        {
            var state = GameStateFactory.NewGame(_data);
            state.sungVerseZones.AddRange(new[] { "hedgerow", "marsh", "crags" });
            state.purchasedKithSlots = 9;

            Assert.That(Kith.Slots(state, _data), Is.EqualTo(6), "slotsMax is the ceiling however generous the counters");
        }

        [Test]
        public void Resting_CountsTheIdleAndLeavesThePonyOut()
        {
            var state = GameStateFactory.NewGame(_data);
            state.sungVerseZones.Add("hedgerow");
            Roster.Recruit(state, _data, "meadow-vole", state.nodes[1].id);
            Roster.Recruit(state, _data, "pack-raven", null);
            state.droversHalterOwned = true;
            Roster.SyncDroversHalter(state, _data);

            Assert.That(Roster.OfSpecies(state, Familiar.PonySpecies), Is.Not.Null, "the Halter's pony stands in her lane");
            Assert.That(Kith.Resting(state), Is.EqualTo(1), "only the raven is idle");
            Assert.That(Kith.Count(state) - Kith.Walking(state), Is.EqualTo(2),
                "count minus walking counts the pony as idle — which is why Resting exists");
        }

        [Test]
        public void NextSlotVerseZone_WalksTheAuthoredOrder()
        {
            var state = GameStateFactory.NewGame(_data);

            Assert.That(Kith.NextSlotVerseZone(state, _data), Is.EqualTo("hedgerow"));

            state.sungVerseZones.Add("hedgerow");
            Assert.That(Kith.NextSlotVerseZone(state, _data), Is.EqualTo("marsh"));

            state.sungVerseZones.Add("marsh");
            Assert.That(Kith.NextSlotVerseZone(state, _data), Is.EqualTo("crags"));

            state.sungVerseZones.Add("crags");
            Assert.That(Kith.NextSlotVerseZone(state, _data), Is.Null, "every earned place is open");
        }

        [Test]
        public void NextSlotVerseZone_SkipsOneAlreadySungOutOfOrder()
        {
            var state = GameStateFactory.NewGame(_data);
            state.sungVerseZones.Add("marsh");

            Assert.That(Kith.NextSlotVerseZone(state, _data), Is.EqualTo("hedgerow"),
                "it names what is still owed, not where the warden got to");
        }

        [Test]
        public void Slots_HandBuiltDataWithoutEconomy_UsesTheAuthoredLadderShape()
        {
            var bare = ScriptableObject.CreateInstance<GameDataAsset>();
            try
            {
                var state = new GameState();
                state.sungVerseZones.Add("bramble-hedgerows");
                Assert.That(Kith.Slots(state, bare), Is.EqualTo(2),
                    "fallback = base 1 + the first authored verse, so a fixture with no economy still "
                    + "exercises the real ladder shape");
            }
            finally
            {
                Object.DestroyImmediate(bare);
            }
        }

        [Test]
        public void Recruit_ASpeciesAlreadyWalking_ReturnsNullAndAddsNoOne()
        {
            var state = GameStateFactory.NewGame(_data);
            Roster.Recruit(state, _data, "meadow-vole", null);

            var duplicate = Roster.Recruit(state, _data, "meadow-vole", null);

            Assert.That(duplicate, Is.Null);
            Assert.That(state.roster.Count, Is.EqualTo(1), "one familiar per species, ever");
        }

        [Test]
        public void Recruit_WhenEverySlotIsHeld_JoinsTheCollectionResting()
        {
            var state = GameStateFactory.NewGame(_data);
            Roster.Recruit(state, _data, "meadow-vole", state.nodes[1].id);
            Assert.That(Kith.HasRoom(state, _data), Is.False, "the vole holds the only slot");

            var recruit = Roster.Recruit(state, _data, "holt-otter", Familiar.WanderStation);

            Assert.That(recruit, Is.Not.Null, "the collection is never slot-capped");
            Assert.That(recruit.IsResting, Is.True, "no slot open — the post request is quietly dropped");
            Assert.That(state.roster.Count, Is.EqualTo(2));
        }

        [Test]
        public void Recruit_WithASlotOpenAndThePostFree_TakesIt()
        {
            var state = GameStateFactory.NewGame(_data);

            var recruit = Roster.Recruit(state, _data, "holt-otter", Familiar.WanderStation);

            Assert.That(recruit.stationId, Is.EqualTo(Familiar.WanderStation));
        }

        [Test]
        public void Recruit_AHeldPost_JoinsRestingInstead()
        {
            var state = GameStateFactory.NewGame(_data);
            state.foldedVersesSung = 2;

            // The warden holds the first node — an arrival never bumps anyone.
            var recruit = Roster.Recruit(state, _data, "holt-otter", state.nodes[0].id);

            Assert.That(recruit.IsResting, Is.True);
            Assert.That(Warden.PostNodeId(state), Is.EqualTo(state.nodes[0].id));
        }

        [Test]
        public void Station_ARestingFamiliarWithNoRoom_IsRefused()
        {
            var state = GameStateFactory.NewGame(_data);
            Roster.Recruit(state, _data, "meadow-vole", state.nodes[1].id);
            var raven = Roster.Recruit(state, _data, "pack-raven", null);
            Assert.That(raven.IsResting, Is.True);

            Assert.That(Roster.Station(state, _data, raven, Familiar.WanderStation), Is.False);
            Assert.That(raven.IsResting, Is.True, "the refusal changes nothing");
        }

        [Test]
        public void Station_AWalkingFamiliar_MovesFreelyAndRestsFreely()
        {
            var state = GameStateFactory.NewGame(_data);
            var vole = Roster.Recruit(state, _data, "meadow-vole", state.nodes[1].id);

            Assert.That(Roster.Station(state, _data, vole, Familiar.WanderStation), Is.True,
                "a held slot moves post to post without asking again");

            Assert.That(Roster.Station(state, _data, vole, null), Is.True, "resting is always allowed");
            Assert.That(Kith.Walking(state), Is.EqualTo(0));
        }

        [Test]
        public void Station_AfterOneRests_TheOtherTakesTheSlot()
        {
            var state = GameStateFactory.NewGame(_data);
            var vole = Roster.Recruit(state, _data, "meadow-vole", state.nodes[1].id);
            var raven = Roster.Recruit(state, _data, "pack-raven", null);

            Roster.Station(state, _data, vole, null);

            Assert.That(Roster.Station(state, _data, raven, Familiar.WanderStation), Is.True,
                "the swap is the point of the ladder");
        }

        [Test]
        public void Station_AHeldPost_SwapsTheHolderOutWithoutNeedingRoom()
        {
            var state = GameStateFactory.NewGame(_data);
            var vole = Roster.Recruit(state, _data, "meadow-vole", state.nodes[1].id);
            var raven = Roster.Recruit(state, _data, "pack-raven", null);
            Assert.That(Kith.HasRoom(state, _data), Is.False, "one slot, and the vole holds it");

            Assert.That(Roster.Station(state, _data, raven, state.nodes[1].id), Is.True,
                "swapping in frees the holder's slot in the same move");

            Assert.That(raven.stationId, Is.EqualTo(state.nodes[1].id));
            Assert.That(vole.IsResting, Is.True, "one body per post — the vole steps back");
            Assert.That(Kith.Walking(state), Is.EqualTo(1));
        }

        [Test]
        public void Station_TheWardensNode_SendsTheWardenToCamp()
        {
            var state = GameStateFactory.NewGame(_data);
            var vole = Roster.Recruit(state, _data, "meadow-vole", null);
            var wardenNode = Warden.PostNodeId(state);
            Assert.That(wardenNode, Is.EqualTo(state.nodes[0].id), "the warden opens on the first node");

            Assert.That(Roster.Station(state, _data, vole, wardenNode), Is.True);

            Assert.That(vole.stationId, Is.EqualTo(wardenNode));
            Assert.That(Warden.PostNodeId(state), Is.Null, "one body per post — the warden walks back to camp");
        }

        [Test]
        public void TryPurchase_ARecruitRung_CallsTheFamiliarResting()
        {
            var rung = new UpgradeData
            {
                order = 1, id = "first-friend",
                materials = { new ItemAmount { id = "berries", amount = 100 } },
                effects = { new EffectData { type = EffectType.RecruitSpecies, species = "meadow-vole" } },
            };
            _data.upgrades = new List<UpgradeData> { rung };
            var state = GameStateFactory.NewGame(_data);
            state.resources["berries"] = 120;

            Assert.That(Upgrades.TryPurchase(state, _data, rung), Is.True);

            var vole = Roster.OfSpecies(state, "meadow-vole");
            Assert.That(vole, Is.Not.Null, "the pile is answered");
            Assert.That(vole.IsResting, Is.True, "the arrival waits to be posted from the strip");
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(20.0));
        }

        [Test]
        public void TryPurchase_ARecruitRungAlreadyAnswered_IsSpentAndRefused()
        {
            var rung = new UpgradeData
            {
                order = 1, id = "first-friend",
                materials = { new ItemAmount { id = "berries", amount = 100 } },
                effects = { new EffectData { type = EffectType.RecruitSpecies, species = "meadow-vole" } },
            };
            _data.upgrades = new List<UpgradeData> { rung };
            var state = GameStateFactory.NewGame(_data);
            // The kith crossed a fold: the vole already walks, the ladder reset.
            Roster.Recruit(state, _data, "meadow-vole", null);
            state.resources["berries"] = 120;

            Assert.That(Upgrades.IsSpentRecruit(state, rung), Is.True);
            Assert.That(Upgrades.TryPurchase(state, _data, rung), Is.False,
                "a rung with nothing left to give must not take the goods");
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(120.0));
        }

        [Test]
        public void SyncBonded_ASpeciesAlreadyPresent_IsHonouredKeepingItsName()
        {
            _data.bonds = new List<BondData>
            {
                new BondData
                {
                    id = "burr", displayName = "Burr", species = "meadow-vole", role = "gatherer",
                    source = new BondSourceData { type = "almanacNode", id = "old-friend" },
                },
            };
            var state = GameStateFactory.NewGame(_data);
            var vole = Roster.Recruit(state, _data, "meadow-vole", null);
            Roster.Rename(vole, "Pip");

            state.almanacNodeIds.Add("old-friend");
            Roster.SyncBonded(state, _data);

            Assert.That(vole.bonded, Is.True);
            Assert.That(vole.bondId, Is.EqualTo("burr"));
            Assert.That(vole.name, Is.EqualTo("Pip"), "the bond honours the companion, it doesn't rename it");
            Assert.That(state.roster.Count, Is.EqualTo(1), "no duplicate vole is minted");
        }

        [Test]
        public void SyncBonded_ASpeciesNeverMet_ArrivesRestingEvenWithNoRoom()
        {
            _data.bonds = new List<BondData>
            {
                new BondData
                {
                    id = "hallow", displayName = "Hallow", species = "dray-stag", role = "carrier",
                    source = new BondSourceData { type = "almanacNode", id = "old-friend" },
                },
            };
            var state = GameStateFactory.NewGame(_data);
            Roster.Recruit(state, _data, "meadow-vole", state.nodes[1].id);
            Assert.That(Kith.HasRoom(state, _data), Is.False);

            state.almanacNodeIds.Add("old-friend");
            Roster.SyncBonded(state, _data);

            var stag = Roster.OfSpecies(state, "dray-stag");
            Assert.That(stag, Is.Not.Null, "the collection is never slot-capped");
            Assert.That(stag.bonded, Is.True);
            Assert.That(stag.name, Is.EqualTo("Hallow"));
            Assert.That(stag.IsResting, Is.True);
        }
    }
}
