using System.Collections.Generic;
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
                fossilCards =
                {
                    new StringEntry { key = "antler-crown", text = "The meadow remembers." },
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

            Assert.That(Narrative.NextUnreadWaystone(state, _data)?.id, Is.EqualTo(GameStateFactory.StartingZoneId),
                "the first arrival is the game's first launch");

            Narrative.MarkWaystoneRead(state, GameStateFactory.StartingZoneId);
            Assert.That(Narrative.NextUnreadWaystone(state, _data), Is.Null,
                "bramble's stone waits behind its trail map");

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
            Assert.That(Narrative.FossilCard(_data, "antler-crown"), Is.EqualTo("The meadow remembers."));
            Assert.That(Narrative.FossilCard(_data, "sunken-jaw"), Is.Null);
        }
    }
}
