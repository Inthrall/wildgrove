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
        }

        [Test]
        public void Portion_PartShare_IsWholeUnits()
        {
            // The card names the amount in whole goods, so a part share must
            // spend the number it shows — half of five is two, not two and a
            // half traded under a label reading "2".
            Assert.That(Exchange.Portion(new BigDouble(5.0), 0.5).ToDouble(), Is.EqualTo(2.0).Within(Tolerance));
            Assert.That(Exchange.Portion(new BigDouble(3.47), 0.5).ToDouble(), Is.EqualTo(1.0).Within(Tolerance));
        }

        [Test]
        public void Portion_PartShareOfASmallPile_IsAtLeastOne()
        {
            // A quarter of three floors to nothing, which would read as a dead
            // button on a pile the camp plainly holds.
            Assert.That(Exchange.Portion(new BigDouble(3.0), 0.25).ToDouble(), Is.EqualTo(1.0).Within(Tolerance));
        }

        [Test]
        public void Portion_PartShareUnderAWholeUnit_IsZero()
        {
            // Below one whole good there is nothing the caravan will take.
            Assert.That(Exchange.Portion(new BigDouble(0.6), 0.5).ToDouble(), Is.EqualTo(0.0).Within(Tolerance));
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

        // ──────── A consideration for the drover (design §9's sink slate) ────

        /// <summary>Arm the consideration alongside the rotating offer — three goods so a re-deal has room to move.</summary>
        private GameState BribableState()
        {
            _data.exchange.offerMinutes = 5.0;
            _data.economy.amber = new EconomyData.AmberData { considerationCostAmber = 5 };
            _data.resources.Add(new ResourceData { id = "fibres", sellValue = 5 });
            var state = DiscoveredState();
            Compendium.RecordGather(state, "fibres", new BigDouble(1.0));
            return state;
        }

        [Test]
        public void PressConsideration_RedealsAtOnceAndSpendsTheAmber()
        {
            var state = BribableState();
            state.amber = 12.0;
            const long now = 900_000L;
            var standing = Exchange.OfferAt(state, _data, now);

            var redealt = Exchange.PressConsideration(state, _data, now);

            Assert.That(redealt, Is.Not.Null);
            Assert.That(redealt.from == standing.from && redealt.to == standing.to, Is.False,
                "the coin must always change something — a repeat of the standing deal is nudged apart");
            Assert.That(state.amber, Is.EqualTo(7.0).Within(Tolerance), "the consideration is spent");

            var read = Exchange.OfferAt(state, _data, now);
            Assert.That(read.from, Is.EqualTo(redealt.from), "the re-dealt offer is now the standing one");
            Assert.That(read.to, Is.EqualTo(redealt.to));
        }

        [Test]
        public void PressConsideration_AReloadCannotRerollTheBribedDeal()
        {
            // Restore rebuilds the run from a fresh NewGame, which needs the
            // starting zone this fixture otherwise does without.
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
            var state = BribableState();
            state.amber = 12.0;
            const long now = 900_000L;
            var redealt = Exchange.PressConsideration(state, _data, now);

            var restored = Saves.SaveCodec.Restore(Saves.SaveCodec.Capture(state, now), _data);
            var read = Exchange.OfferAt(restored, _data, now);

            Assert.That(read.from, Is.EqualTo(redealt.from),
                "(window, considerations) → deal — the pair rides the save, so a reload lands on the same deal");
            Assert.That(read.to, Is.EqualTo(redealt.to));
        }

        [Test]
        public void PressConsideration_TheWindowTurnClearsTheBribe()
        {
            var state = BribableState();
            state.amber = 100.0;
            const long now = 900_000L;
            Exchange.PressConsideration(state, _data, now);

            var nextWindow = now + 5L * 60_000L;
            var unbribed = Exchange.OfferAt(DiscoveredStateWithFibres(), _data, nextWindow);
            var read = Exchange.OfferAt(state, _data, nextWindow);

            Assert.That(read.from, Is.EqualTo(unbribed.from),
                "a stale count belongs to a window that has closed — the new window opens on its own plain deal");
            Assert.That(read.to, Is.EqualTo(unbribed.to));
        }

        /// <summary>A never-bribed state knowing the same three goods — the window-turn test's control.</summary>
        private GameState DiscoveredStateWithFibres()
        {
            var state = DiscoveredState();
            Compendium.RecordGather(state, "fibres", new BigDouble(1.0));
            return state;
        }

        [Test]
        public void PressConsideration_RepeatedPressesKeepChangingTheDeal()
        {
            var state = BribableState();
            state.amber = 100.0;
            const long now = 900_000L;

            var previous = Exchange.OfferAt(state, _data, now);
            for (var press = 0; press < 10; press++)
            {
                var redealt = Exchange.PressConsideration(state, _data, now);
                Assert.That(redealt.from == previous.from && redealt.to == previous.to, Is.False,
                    "press " + press + " repeated the deal it replaced");
                previous = redealt;
            }

            Assert.That(state.amber, Is.EqualTo(50.0).Within(Tolerance), "ten considerations at 5 apiece");
        }

        [Test]
        public void PressConsideration_TwoGoodsRedealToTheReverse()
        {
            _data.exchange.offerMinutes = 5.0;
            _data.economy.amber = new EconomyData.AmberData { considerationCostAmber = 5 };
            var state = DiscoveredState(); // berries and nuts only
            state.amber = 100.0;
            const long now = 900_000L;
            var standing = Exchange.OfferAt(state, _data, now);

            var redealt = Exchange.PressConsideration(state, _data, now);

            Assert.That(redealt.from, Is.EqualTo(standing.to),
                "two goods hold exactly two deals — the re-deal can only be the reverse");
            Assert.That(redealt.to, Is.EqualTo(standing.from));
        }

        [Test]
        public void PressConsideration_RefusedWhenShortOrUnconfigured()
        {
            var state = BribableState();
            state.amber = 4.0; // one short of the 5-amber asking
            Assert.That(Exchange.CanPressConsideration(state, _data, 900_000L), Is.False);
            Assert.That(Exchange.PressConsideration(state, _data, 900_000L), Is.Null);
            Assert.That(state.amber, Is.EqualTo(4.0).Within(Tolerance), "nothing spent on a refusal");

            _data.economy.amber = null; // the sink unconfigured — the row hides, pressing is refused
            state.amber = 100.0;
            Assert.That(Exchange.CanPressConsideration(state, _data, 900_000L), Is.False);
            Assert.That(Exchange.PressConsideration(state, _data, 900_000L), Is.Null);
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
