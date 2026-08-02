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
        public void Portion_TakesTheFractionOfWhatIsHeld()
        {
            Assert.That(Exchange.Portion(new BigDouble(240.0), 0.25).ToDouble(), Is.EqualTo(60.0).Within(Tolerance));
            Assert.That(Exchange.Portion(new BigDouble(5.0), 0.5).ToDouble(), Is.EqualTo(2.5).Within(Tolerance));
        }

        [Test]
        public void Portion_Whole_IsTheHoldingExactly()
        {
            // Passed through, not multiplied — a whole trade must leave zero
            // rather than a crumb no button could ever spend.
            var have = new BigDouble(1.0 / 3.0);
            Assert.That(Exchange.Portion(have, 1.0).ToDouble(), Is.EqualTo(have.ToDouble()));
        }

        [Test]
        public void Portion_NothingHeldOrNothingAsked_IsZero()
        {
            Assert.That(Exchange.Portion(BigDouble.Zero, 1.0).ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(Exchange.Portion(new BigDouble(240.0), 0.0).ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
        }

        [Test]
        public void Portion_WholeTrade_EmptiesTheGood()
        {
            var state = new GameState();
            state.AddResource("berries", new BigDouble(240.0));

            Exchange.TryTrade(state, _data, "berries", "nuts",
                Exchange.Portion(state.GetResource("berries"), 1.0));

            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
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

        // ─────────────── The quality tiers (excess windfalls trade down) ─────

        private void GiveQualityConfig()
        {
            _data.economy.quality = new EconomyData.QualityData
            {
                decentChance = 0.035,
                decentValueMult = 1.5,
                choiceBaseChance = 0.005,
                choiceValueMult = 10.0,
            };
        }

        [Test]
        public void Quote_DecentTier_PaysTheValueMultiplier()
        {
            GiveQualityConfig();
            Assert.That(Exchange.Quote(new GameState(), _data, "berries", "nuts", new BigDouble(10.0), QualityTier.Decent).ToDouble(),
                Is.EqualTo(10.0 * (2.0 / 3.0 * 0.85) * 1.5).Within(Tolerance));
        }

        [Test]
        public void QualityValueMultiplier_NoQualityConfig_IsOne()
        {
            // Hand-built fixtures without a quality section trade at par.
            Assert.That(Exchange.QualityValueMultiplier(_data, QualityTier.Choice), Is.EqualTo(1.0).Within(Tolerance));
        }

        [Test]
        public void TryTrade_DecentTier_SpendsTheDecentPool_AndPaysPlainGoods()
        {
            GiveQualityConfig();
            var state = new GameState();
            state.AddResource("berries", new BigDouble(100.0));
            state.AddDecent("berries", new BigDouble(10.0));

            var received = Exchange.TryTrade(state, _data, "berries", "nuts", new BigDouble(10.0), QualityTier.Decent);
            var expected = 10.0 * (2.0 / 3.0 * 0.85) * 1.5;

            Assert.That(received.ToDouble(), Is.EqualTo(expected).Within(Tolerance));
            // The decent pool paid; the plain stock never moved; the payout is plain.
            Assert.That(state.GetDecent("berries").ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(100.0).Within(Tolerance));
            Assert.That(state.GetResource("nuts").ToDouble(), Is.EqualTo(expected).Within(Tolerance));
        }

        [Test]
        public void TryTrade_ChoiceTier_PaysTenfold_IntoPlainStock()
        {
            GiveQualityConfig();
            var state = new GameState();
            state.AddChoice("berries", new BigDouble(2.0));

            var received = Exchange.TryTrade(state, _data, "berries", "nuts", new BigDouble(2.0), QualityTier.Choice);

            Assert.That(received.ToDouble(), Is.EqualTo(2.0 * (2.0 / 3.0 * 0.85) * 10.0).Within(Tolerance));
            Assert.That(state.GetChoice("berries").ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
        }

        [Test]
        public void TryTrade_TierShortStock_NeverBorrowsFromAnotherPool()
        {
            GiveQualityConfig();
            var state = new GameState();
            // Plenty of plain berries, no decent ones — a decent trade must refuse
            // rather than quietly spending the camp stock at the decent rate.
            state.AddResource("berries", new BigDouble(100.0));

            Assert.That(Exchange.TryTrade(state, _data, "berries", "nuts", new BigDouble(10.0), QualityTier.Decent).ToDouble(),
                Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(state.GetResource("berries").ToDouble(), Is.EqualTo(100.0).Within(Tolerance));
        }

        // ──────────────────── The rotating offer (the caravan's deal) ────────

        private GameState DiscoveredState()
        {
            var state = new GameState();
            Compendium.RecordGather(state, "berries", new BigDouble(1.0));
            Compendium.RecordGather(state, "nuts", new BigDouble(1.0));
            return state;
        }

        [Test]
        public void OfferAt_SameWindow_IsTheSameDeal()
        {
            _data.exchange.offerMinutes = 5.0;
            var state = DiscoveredState();

            // The third window runs [900000, 1200000) — first and last ms of it.
            var early = Exchange.OfferAt(state, _data, 900_000L);
            var late = Exchange.OfferAt(state, _data, 1_199_999L);

            Assert.That(early, Is.Not.Null);
            Assert.That(late.from, Is.EqualTo(early.from));
            Assert.That(late.to, Is.EqualTo(early.to));
        }

        [Test]
        public void OfferAt_NeverTradesAGoodForItself()
        {
            _data.exchange.offerMinutes = 5.0;
            var state = DiscoveredState();

            // Walk a day of windows — every deal must pair two goods.
            for (var window = 0; window < 288; window++)
            {
                var offer = Exchange.OfferAt(state, _data, window * 5 * 60_000L);
                Assert.That(offer.from, Is.Not.EqualTo(offer.to), "window " + window);
            }
        }

        [Test]
        public void OfferAt_TheDealTurns()
        {
            _data.exchange.offerMinutes = 5.0;
            var state = DiscoveredState();
            Compendium.RecordGather(state, "fibres", new BigDouble(1.0));
            _data.resources.Add(new ResourceData { id = "fibres", sellValue = 5 });

            // With three goods, some later window must name a different deal —
            // a caravan that never changes its ask is the bug this pins.
            var first = Exchange.OfferAt(state, _data, 0L);
            var changed = false;
            for (var window = 1; window < 48 && !changed; window++)
            {
                var offer = Exchange.OfferAt(state, _data, window * 5 * 60_000L);
                changed = offer.from != first.from || offer.to != first.to;
            }

            Assert.That(changed, Is.True);
        }

        [Test]
        public void OfferAt_UndiscoveredGoods_NeverNamed()
        {
            _data.exchange.offerMinutes = 5.0;
            var state = new GameState();
            Compendium.RecordGather(state, "berries", new BigDouble(1.0));

            // One known good is no deal at all — far-zone names must not leak.
            Assert.That(Exchange.OfferAt(state, _data, 0L), Is.Null);
        }

        [Test]
        public void OfferAt_NoRotationConfigured_IsNoDeal()
        {
            // offerMinutes 0 (pre-rotation fixtures): the caravan names nothing.
            Assert.That(Exchange.OfferAt(DiscoveredState(), _data, 0L), Is.Null);
        }

        [Test]
        public void OfferSecondsRemaining_CountsDownTheWindow()
        {
            _data.exchange.offerMinutes = 5.0;

            // One minute into a five-minute window: four minutes left.
            Assert.That(Exchange.OfferSecondsRemaining(_data, 60_000L), Is.EqualTo(240.0).Within(Tolerance));
        }
    }
}
