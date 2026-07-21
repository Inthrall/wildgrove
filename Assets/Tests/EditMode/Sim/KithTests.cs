using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins the kith's slot ladder (design §4): six slots, four free from
    /// minute one, slot 5 opened by The Old Friend Almanac node and slot 6 by
    /// the Warden's Gallery spread. Slots cap recruitment and bond
    /// materialisation; a bond with no room waits in the grass.
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
                kith = new EconomyData.KithData { slotsBase = 4, slotsMax = 6 },
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
            _data.folioSpreads = new List<FolioSpreadData>
            {
                new FolioSpreadData
                {
                    id = "wardens-gallery",
                    displayName = "The Warden's Gallery",
                    entries = new List<string> { "berries", "nuts" },
                    effects = new List<EffectData>
                    {
                        new EffectData { type = EffectType.KithSlot, value = 1 },
                    },
                },
            };
            _data.almanac = new List<AlmanacNodeData>
            {
                new AlmanacNodeData
                {
                    id = "old-friend",
                    displayName = "The Old Friend",
                    costVerdure = 12,
                    effects = new List<EffectData>
                    {
                        new EffectData { type = EffectType.KithSlot, value = 1 },
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
        public void Slots_FreshRun_HasTheFourFreeSlots()
        {
            var state = GameStateFactory.NewGame(_data);

            Assert.That(Kith.Slots(state, _data), Is.EqualTo(4));
            Assert.That(Kith.Count(state), Is.EqualTo(2), "the seed vole and raven hold two of them");
            Assert.That(Kith.HasRoom(state, _data), Is.True);
        }

        [Test]
        public void Slots_TheOldFriend_OpensSlotFive()
        {
            var state = GameStateFactory.NewGame(_data);

            state.almanacNodeIds.Add("old-friend");

            Assert.That(Kith.Slots(state, _data), Is.EqualTo(5));
        }

        [Test]
        public void Slots_TheCompletedGallery_OpensSlotSix()
        {
            var state = GameStateFactory.NewGame(_data);
            state.almanacNodeIds.Add("old-friend");

            state.fixedResources.Add("berries");
            Assert.That(Kith.Slots(state, _data), Is.EqualTo(5), "a half-done spread opens nothing");

            state.fixedResources.Add("nuts");
            Assert.That(Kith.Slots(state, _data), Is.EqualTo(6));
        }

        [Test]
        public void Slots_NeverClimbAboveSlotsMax()
        {
            var state = GameStateFactory.NewGame(_data);
            _data.almanac[0].effects[0].value = 9;

            state.almanacNodeIds.Add("old-friend");

            Assert.That(Kith.Slots(state, _data), Is.EqualTo(6), "slotsMax is the ceiling however generous the content");
        }

        [Test]
        public void Slots_TheCabinetMultiplier_NeverScalesTheGallerySlot()
        {
            _data.economy.kith.slotsMax = 10;
            _data.folioSpreads[0].effects[0].value = 2;
            _data.upgrades = new List<UpgradeData>
            {
                new UpgradeData
                {
                    id = "curators-cabinet",
                    effects = new List<EffectData>
                    {
                        new EffectData { type = EffectType.FolioSpreadBonusMult, value = 1.5 },
                    },
                },
            };
            var state = GameStateFactory.NewGame(_data);
            state.purchasedUpgradeIds.Add("curators-cabinet");

            state.fixedResources.Add("berries");
            state.fixedResources.Add("nuts");

            Assert.That(Kith.Slots(state, _data), Is.EqualTo(6), "whole slots, never a scaled band (4 + 2, not 4 + 3)");
        }

        [Test]
        public void Slots_HandBuiltDataWithoutEconomy_UsesTheAuthoredLadderShape()
        {
            var bare = ScriptableObject.CreateInstance<GameDataAsset>();
            try
            {
                Assert.That(Kith.Slots(new GameState(), bare), Is.EqualTo(4));
            }
            finally
            {
                Object.DestroyImmediate(bare);
            }
        }

        [Test]
        public void Recruit_WhenEverySlotIsHeld_ReturnsNullAndAddsNoOne()
        {
            var state = GameStateFactory.NewGame(_data);
            TestKith.Station(state, state.nodes[0].id, 2);
            Assert.That(Kith.HasRoom(state, _data), Is.False);

            var recruit = Roster.Recruit(state, _data, "meadow-vole", null);

            Assert.That(recruit, Is.Null);
            Assert.That(state.roster.Count, Is.EqualTo(4), "recruitment waits for an open slot");
        }

        [Test]
        public void SyncBonded_WithNoRoomOpen_LeavesTheBondWaitingInTheGrass()
        {
            _data.bonds = new List<BondData>
            {
                new BondData
                {
                    id = "sootwing", displayName = "Sootwing", species = "pack-raven", role = "carrier",
                    source = new BondSourceData { type = "folioSpread", id = "wardens-gallery" },
                },
            };
            var state = GameStateFactory.NewGame(_data);
            TestKith.Station(state, state.nodes[0].id, 2);

            // Earned — but the Gallery's own slot is what makes room, so strip
            // its slot effect to model a bond earned against a full kith.
            _data.folioSpreads[0].effects.Clear();
            state.fixedResources.Add("berries");
            state.fixedResources.Add("nuts");
            Roster.SyncBonded(state, _data);
            Assert.That(state.roster.Exists(f => f.bondId == "sootwing"), Is.False,
                "no slot open — the companion waits in the grass");

            // The Old Friend opens slot 5: the waiting bond steps in on the next sync.
            state.almanacNodeIds.Add("old-friend");
            Roster.SyncBonded(state, _data);
            Assert.That(state.roster.Exists(f => f.bondId == "sootwing"), Is.True);
        }
    }
}
