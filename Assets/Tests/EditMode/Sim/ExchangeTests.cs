using System.Collections.Generic;
using BreakInfinity;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins the Exchange (design §9): rates derive from the single trade-value
    /// table less a spread — never authored per pair — small trades round up in
    /// the player's favour, materials (trade value zero) can't be bartered, and
    /// there is no Coin. A hand-built content asset, no scene.
    /// </summary>
    public class ExchangeTests
    {
        private const double Tolerance = 1e-6;

        private GameDataAsset _data;

        [SetUp]
        public void SetUp()
        {
            _data = ScriptableObject.CreateInstance<GameDataAsset>();
            _data.economy = new EconomyData
            {
                mastery = new EconomyData.MasteryData { yieldBonusPerLevel = 0.05 },
            };
            _data.exchange = new ExchangeData { spread = 0.15 };
            _data.resources = new List<ResourceData>
            {
                new ResourceData { id = "berries", sellValue = 2 },
                new ResourceData { id = "nuts", sellValue = 3 },
            };
            _data.recipes = new List<RecipeData>
            {
                new RecipeData
                {
                    id = "cordage", station = "bench", skill = "bushcraft",
                    inputs = { new ItemAmount { id = "fibres", amount = 8 } },
                    output = "cordage", valueMult = 2, kind = "material",
                },
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_data);
        }

        [Test]
        public void Rate_DerivesFromTradeValuesLessSpread()
        {
            // tradeValue(berries)/tradeValue(nuts) · (1 − 0.15) = 2/3 · 0.85.
            Assert.That(Exchange.Rate(new GameState(), _data, "berries", "nuts").ToDouble(),
                Is.EqualTo(2.0 / 3.0 * 0.85).Within(Tolerance));
        }

        [Test]
        public void Rate_SameResource_IsZero()
        {
            Assert.That(Exchange.Rate(new GameState(), _data, "berries", "berries").ToDouble(),
                Is.EqualTo(0.0).Within(Tolerance));
        }

        [Test]
        public void Rate_UnpricedResource_IsZero()
        {
            // A material (trade value 0) can't anchor a rate either way.
            Assert.That(Exchange.Rate(new GameState(), _data, "berries", "cordage").ToDouble(),
                Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(Exchange.Rate(new GameState(), _data, "cordage", "berries").ToDouble(),
                Is.EqualTo(0.0).Within(Tolerance));
        }

        [Test]
        public void Quote_IsExactRateTimesAmount()
        {
            // Exact, not rounded — 10 · (2/3 · 0.85). Rounding up would let a
            // round trip mint value despite the spread.
            Assert.That(Exchange.Quote(new GameState(), _data, "berries", "nuts", new BigDouble(10.0)).ToDouble(),
                Is.EqualTo(10.0 * (2.0 / 3.0 * 0.85)).Within(Tolerance));
        }

        [Test]
        public void RoundTrip_NeverProfits()
        {
            var state = new GameState();
            state.AddResource("berries", new BigDouble(500.0));

            var nuts = Exchange.TryTrade(state, _data, "berries", "nuts", new BigDouble(500.0));
            Exchange.TryTrade(state, _data, "nuts", "berries", nuts);

            // Two spreads applied — a there-and-back can only lose.
            Assert.That(state.GetResource("berries").ToDouble(), Is.LessThan(500.0));
        }

        [Test]
        public void TryTrade_SpendsFromAndAddsTo()
        {
            var state = new GameState();
            state.AddResource("berries", new BigDouble(10.0));

            var received = Exchange.TryTrade(state, _data, "berries", "nuts", new BigDouble(10.0));
            var expected = 10.0 * (2.0 / 3.0 * 0.85);

            Assert.That(received.ToDouble(), Is.EqualTo(expected).Within(Tolerance));
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(state.GetResource("nuts").ToDouble(), Is.EqualTo(expected).Within(Tolerance));
        }

        [Test]
        public void TryTrade_ShortStock_ChangesNothing()
        {
            var state = new GameState();
            state.AddResource("berries", new BigDouble(3.0));

            Assert.That(Exchange.TryTrade(state, _data, "berries", "nuts", new BigDouble(10.0)).ToDouble(),
                Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(3.0).Within(Tolerance));
            Assert.That(state.GetResource("nuts").ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
        }

        [Test]
        public void TryTrade_Unconfigured_IsRefused()
        {
            _data.exchange = null;
            var state = new GameState();
            state.AddResource("berries", new BigDouble(10.0));

            Assert.That(Exchange.TryTrade(state, _data, "berries", "nuts", new BigDouble(10.0)).ToDouble(),
                Is.EqualTo(0.0).Within(Tolerance));
        }
    }
}
