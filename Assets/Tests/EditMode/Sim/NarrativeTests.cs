using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins the narrative display logic (design §6): waystones reveal once
    /// per zone on arrival (unlocked + authored + unread, in data order),
    /// unauthored lines never show, the read-set survives Migration (lore
    /// stays read), and the dialogue lookups return null rather than empty
    /// strings so the HUD can simply skip what isn't written yet.
    /// </summary>
    public class NarrativeTests
    {
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
                    resources = new List<string> { "berries" },
                    unlocks = new List<string> { "foraging" },
                },
                new ZoneData
                {
                    id = "bramble",
                    order = 2,
                    resources = new List<string> { "nuts" },
                    unlocks = new List<string> { "foraging" },
                },
            };
            _data.upgrades = new List<UpgradeData>
            {
                new UpgradeData
                {
                    id = "map-bramble", displayName = "Trail Map: Bramble",
                    effects = { new EffectData { type = EffectType.UnlockZone, zone = "bramble" } },
                },
            };
            _data.dialogue = new DialogueBundle
            {
                waystones =
                {
                    new StringEntry { key = GameStateFactory.StartingZoneId, text = "Walk gently." },
                    new StringEntry { key = "bramble", text = "The stone does not grow back." },
                },
                verses =
                {
                    new StringEntry { key = GameStateFactory.StartingZoneId, text = "What you carry is borrowed." },
                    new StringEntry { key = "bramble", text = "   " },
                },
                insectPlates =
                {
                    new StringEntry { key = "stags-herald", text = "The meadow remembers." },
                },
                finalWaystones = new FinalWaystonesData
                {
                    zoneId = "bramble",
                    stones =
                    {
                        new StringEntry { key = "first", text = "The fields were in rows." },
                        new StringEntry { key = "second", text = "We did not stop." },
                        new StringEntry { key = "third", text = "You have their hands." },
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
        public void NextUnreadWaystone_RevealsOnArrival_InTrailOrder()
        {
            var state = GameStateFactory.NewGame(_data);

            // The starting stone is pre-marked at run birth — a brand-new
            // player is never met by a lore sheet before they've done anything.
            Assert.That(Narrative.NextUnreadWaystone(state, _data), Is.Null,
                "bramble's stone waits behind its trail map; the starting stone never interrupts");

            state.purchasedUpgradeIds.Add("map-bramble");
            Assert.That(Narrative.NextUnreadWaystone(state, _data)?.id, Is.EqualTo("bramble"));
        }

        [Test]
        public void NextUnreadWaystone_SkipsUnauthoredStones()
        {
            _data.dialogue.waystones[0].text = "";
            var state = GameStateFactory.NewGame(_data);

            Assert.That(Narrative.NextUnreadWaystone(state, _data), Is.Null,
                "an empty inscription never interrupts anyone");
        }

        [Test]
        public void MarkWaystoneRead_IsIdempotent()
        {
            var state = GameStateFactory.NewGame(_data);

            Narrative.MarkWaystoneRead(state, GameStateFactory.StartingZoneId);
            Narrative.MarkWaystoneRead(state, GameStateFactory.StartingZoneId);

            Assert.That(state.seenWaystoneZoneIds, Has.Count.EqualTo(1));
        }

        [Test]
        public void Lookups_ReturnNullForUnauthoredOrUnknownKeys()
        {
            Assert.That(Narrative.VerseLine(_data, GameStateFactory.StartingZoneId), Is.EqualTo("What you carry is borrowed."));
            Assert.That(Narrative.VerseLine(_data, "bramble"), Is.Null, "whitespace is unauthored, not content");
            Assert.That(Narrative.VerseLine(_data, "nowhere"), Is.Null);
            Assert.That(Narrative.WaystoneText(_data, "bramble"), Is.EqualTo("The stone does not grow back."));
            Assert.That(Narrative.InsectPlate(_data, "stags-herald"), Is.EqualTo("The meadow remembers."));
            Assert.That(Narrative.InsectPlate(_data, "silver-skimmer"), Is.Null);
        }

        [Test]
        public void NextFinalWaystone_WaitsForTheChainsOwnGround()
        {
            var state = GameStateFactory.NewGame(_data);

            Assert.That(Narrative.NextFinalWaystone(state, _data), Is.Null,
                "the chain stands in bramble — there is nothing to read from the meadow");

            state.purchasedUpgradeIds.Add("map-bramble");

            Assert.That(Narrative.NextFinalWaystone(state, _data)?.text,
                Is.EqualTo("The fields were in rows."), "and the first stone is the first one authored");
        }

        [Test]
        public void NextFinalWaystone_GivesUpOneStonePerFold()
        {
            var state = GameStateFactory.NewGame(_data);
            state.purchasedUpgradeIds.Add("map-bramble");

            Narrative.MarkFinalWaystoneRead(state, _data);

            Assert.That(state.finalWaystonesRead, Is.EqualTo(1));
            Assert.That(Narrative.NextFinalWaystone(state, _data), Is.Null,
                "this fold has given what it had — four beats must not arrive as one wall of text");

            state.migrationCount++;

            Assert.That(Narrative.NextFinalWaystone(state, _data)?.text, Is.EqualTo("We did not stop."),
                "the next season brings the next stone");
        }

        [Test]
        public void NextFinalWaystone_ArrivingLate_StillPacesOneAFold()
        {
            // The pacing is anchored on the fold the last stone was READ on, not
            // on the fold the zone opened, so a warden who climbs at fold nine
            // does not collect the whole reveal in one sitting.
            var state = GameStateFactory.NewGame(_data);
            state.purchasedUpgradeIds.Add("map-bramble");
            state.migrationCount = 9;

            Narrative.MarkFinalWaystoneRead(state, _data);

            Assert.That(state.finalWaystonesRead, Is.EqualTo(1), "one stone, however late the climb");
            Assert.That(Narrative.NextFinalWaystone(state, _data), Is.Null);
        }

        [Test]
        public void MarkFinalWaystoneRead_RunsOutAtTheLastStone()
        {
            var state = GameStateFactory.NewGame(_data);
            state.purchasedUpgradeIds.Add("map-bramble");

            for (var fold = 0; fold < 5; fold++)
            {
                state.migrationCount = fold;
                Narrative.MarkFinalWaystoneRead(state, _data);
            }

            Assert.That(state.finalWaystonesRead, Is.EqualTo(3), "three authored stones, and no fourth");
            Assert.That(Narrative.AreFinalWaystonesComplete(state, _data), Is.True);
            Assert.That(Narrative.NextFinalWaystone(state, _data), Is.Null);
            Assert.That(Narrative.ReadFinalWaystones(state, _data).Select(s => s.key),
                Is.EqualTo(new[] { "first", "second", "third" }), "and they re-read in the authored order");
        }

        [Test]
        public void FinalWaystones_WithNoChainAuthored_AreSimplyAbsent()
        {
            // Unity serializes an authored-empty section as a zeroed object, so
            // "no chain" has to read as absence rather than as a broken one.
            _data.dialogue.finalWaystones = new FinalWaystonesData();
            var state = GameStateFactory.NewGame(_data);
            state.purchasedUpgradeIds.Add("map-bramble");

            Assert.That(Narrative.FinalWaystonesConfigured(_data), Is.False);
            Assert.That(Narrative.FinalWaystoneCount(_data), Is.EqualTo(0));
            Assert.That(Narrative.NextFinalWaystone(state, _data), Is.Null);
            Assert.That(Narrative.AreFinalWaystonesComplete(state, _data), Is.False,
                "nothing to read is not the same as having read it all");
            Assert.That(Narrative.ReadFinalWaystones(state, _data), Is.Empty);
        }
    }
}
