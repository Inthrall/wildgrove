using System.Collections.Generic;
using BreakInfinity;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>The caravan's standing deal: one good given for one good taken, until the window turns.</summary>
    public sealed class ExchangeOffer
    {
        public string from;
        public string to;
    }

    /// <summary>
    /// The Exchange (design §9): the silent barter caravan that replaces selling
    /// for Coin. Rates derive from the single trade-value table (<see cref="Economy"/>)
    /// — never authored per pair — less a spread, and small trades round in the
    /// player's favour. Goods buy goods; materials (trade value zero) can't be
    /// bartered. No-ops when exchange data is absent (fixtures).
    /// <para>
    /// The caravan names the deal, not the player: one from-good and one
    /// to-good, drawn deterministically from the wall-clock window
    /// (<see cref="OfferAt"/>) and changing every exchange.offerMinutes. Every
    /// quality tier of the from-good is taken — Decent and Choice at their
    /// value multipliers — and the caravan always pays in plain goods, so
    /// excess high-quality stock trades down into more units of something else.
    /// A pressed <b>consideration</b> in Amber re-deals the standing offer at
    /// once (design §9's sink slate) — see <see cref="PressConsideration"/>.
    /// </para>
    /// </summary>
    public static class Exchange
    {
        public static bool Configured(GameDataAsset data)
        {
            return data?.exchange != null;
        }

        /// <summary>
        /// The goods the caravan will speak of: DISCOVERED goods (a raw find
        /// the camp has gathered, or a trade good it has crafted) with a
        /// positive trade value. The offer draws from this list, so an unmet
        /// zone's names never leak through a deal.
        /// </summary>
        public static List<string> TradeableGoods(GameState state, GameDataAsset data)
        {
            var goods = new List<string>();
            if (state == null || data == null)
            {
                return goods;
            }

            if (data.resources != null)
            {
                foreach (var resource in data.resources)
                {
                    if (Compendium.IsResourceDiscovered(state, resource.id)
                        && Economy.TradeValuePerUnit(state, data, resource.id) > BigDouble.Zero)
                    {
                        goods.Add(resource.id);
                    }
                }
            }

            if (data.recipes != null)
            {
                foreach (var recipe in data.recipes)
                {
                    if (recipe.kind == "trade" && recipe.output != null && !goods.Contains(recipe.output)
                        && Compendium.IsRecipeDiscovered(state, recipe.id)
                        && Economy.TradeValuePerUnit(state, data, recipe.output) > BigDouble.Zero)
                    {
                        goods.Add(recipe.output);
                    }
                }
            }

            return goods;
        }

        /// <summary>
        /// The deal standing at <paramref name="nowUnixMs"/>: one from-good and
        /// one to-good, drawn deterministically from the wall-clock window's
        /// index and the considerations pressed this window — the generator's
        /// idiom ((window, considerations) → deal, so a reload cannot reroll
        /// the caravan; an un-bribed deal turns on the same beat for everyone).
        /// Null while unconfigured or the camp knows fewer than two goods.
        /// </summary>
        public static ExchangeOffer OfferAt(GameState state, GameDataAsset data, long nowUnixMs)
        {
            var periodMs = OfferPeriodMs(data);
            if (periodMs <= 0L)
            {
                return null;
            }

            var goods = TradeableGoods(state, data);
            if (goods.Count < 2)
            {
                return null;
            }

            var windowIndex = nowUnixMs / periodMs;
            return OfferFor(goods, windowIndex, ConsiderationsAt(state, windowIndex));
        }

        /// <summary>Considerations pressed in <paramref name="windowIndex"/> — a count stored under any other window is stale, and reads as none.</summary>
        private static int ConsiderationsAt(GameState state, long windowIndex)
        {
            return state != null
                   && state.exchangeConsiderationWindowIndex == windowIndex
                   && state.exchangeConsiderationsThisWindow > 0
                ? state.exchangeConsiderationsThisWindow
                : 0;
        }

        /// <summary>
        /// The deal for a window after <paramref name="considerations"/>
        /// re-deals. Each re-deal draws fresh from its own seed, then is
        /// nudged apart from the deal it replaces when the draw repeats it —
        /// a pressed consideration must always change something. Walked from
        /// the window's first deal so the whole chain is a pure function of
        /// (window, considerations).
        /// </summary>
        private static ExchangeOffer OfferFor(List<string> goods, long windowIndex, int considerations)
        {
            ExchangeOffer offer = null;
            for (var redeal = 0; redeal <= considerations; redeal++)
            {
                var drawn = Draw(goods, windowIndex, redeal);
                offer = offer != null && drawn.from == offer.from && drawn.to == offer.to
                    ? NudgeApart(goods, drawn, offer)
                    : drawn;
            }

            return offer;
        }

        /// <summary>
        /// One deal from its own throwaway rng — the run's saved stream is
        /// never drawn, so browsing (or bribing) the caravan can't shift a
        /// roll. Re-deal 0 seeds exactly as the pre-consideration code did, so
        /// an un-bribed window's deal is unchanged across the feature.
        /// </summary>
        private static ExchangeOffer Draw(List<string> goods, long windowIndex, int redeal)
        {
            var seed = Rng.Sanitise((ulong)windowIndex ^ (ulong)redeal * 0x9E3779B97F4A7C15UL);
            var fromIndex = (int)(Rng.NextDouble(ref seed) * goods.Count);
            var toIndex = (int)(Rng.NextDouble(ref seed) * (goods.Count - 1));
            if (toIndex >= fromIndex)
            {
                toIndex++;
            }

            return new ExchangeOffer { from = goods[fromIndex], to = goods[toIndex] };
        }

        /// <summary>
        /// Move a repeated draw off the deal it replaces: step the to-good
        /// forward (past the from-good); when only two goods exist that lands
        /// back on the same deal, so the one other deal — the reverse — is it.
        /// </summary>
        private static ExchangeOffer NudgeApart(List<string> goods, ExchangeOffer drawn, ExchangeOffer previous)
        {
            var fromIndex = goods.IndexOf(drawn.from);
            var toIndex = goods.IndexOf(drawn.to);
            do
            {
                toIndex = (toIndex + 1) % goods.Count;
            }
            while (toIndex == fromIndex);

            var nudged = new ExchangeOffer { from = drawn.from, to = goods[toIndex] };
            if (nudged.from == previous.from && nudged.to == previous.to)
            {
                return new ExchangeOffer { from = previous.to, to = previous.from };
            }

            return nudged;
        }

        // ──────── A consideration for the drover (design §9's sink slate) ────

        /// <summary>The Amber a pressed consideration asks, or 0 when the sink is unconfigured — the row hides then; there is nothing a player loses by its absence.</summary>
        public static double ConsiderationCost(GameDataAsset data)
        {
            var amber = data?.economy?.amber;
            return amber != null && amber.considerationCostAmber > 0.0 ? amber.considerationCostAmber : 0.0;
        }

        /// <summary>Whether a consideration can be pressed right now — configured, affordable, and a caravan standing to press it on.</summary>
        public static bool CanPressConsideration(GameState state, GameDataAsset data, long nowUnixMs)
        {
            var cost = ConsiderationCost(data);
            return cost > 0.0
                && state != null
                && state.amber >= cost
                && OfferAt(state, data, nowUnixMs) != null;
        }

        /// <summary>
        /// Press a consideration on the drover: spend the Amber and the
        /// standing deal re-draws at once, never repeating the deal it
        /// replaces. The re-deal count persists with the run and mixes into
        /// the window's seed, so a reload still cannot reroll — and it resets
        /// when the window turns. Returns the new deal, or null when refused
        /// (unconfigured, short, or no caravan to press it on).
        /// </summary>
        public static ExchangeOffer PressConsideration(GameState state, GameDataAsset data, long nowUnixMs)
        {
            if (!CanPressConsideration(state, data, nowUnixMs))
            {
                return null;
            }

            var windowIndex = nowUnixMs / OfferPeriodMs(data);
            state.exchangeConsiderationsThisWindow = ConsiderationsAt(state, windowIndex) + 1;
            state.exchangeConsiderationWindowIndex = windowIndex;
            state.amber -= ConsiderationCost(data);
            return OfferAt(state, data, nowUnixMs);
        }

        /// <summary>Seconds until the standing deal turns (0 when no deal rotates).</summary>
        public static double OfferSecondsRemaining(GameDataAsset data, long nowUnixMs)
        {
            var periodMs = OfferPeriodMs(data);
            return periodMs <= 0L ? 0.0 : (periodMs - nowUnixMs % periodMs) / 1000.0;
        }

        private static long OfferPeriodMs(GameDataAsset data)
        {
            var minutes = data?.exchange != null ? data.exchange.offerMinutes : 0.0;
            return minutes > 0.0 ? (long)(minutes * 60_000.0) : 0L;
        }

        /// <summary>
        /// What a tier's goods are worth at the caravan, per unit, against the
        /// same good's plain stock (design §5's value multipliers) — the
        /// higher-quality trade-in pays out in higher counts.
        /// </summary>
        public static double QualityValueMultiplier(GameDataAsset data, QualityTier quality)
        {
            var config = data?.economy?.quality;
            if (config == null)
            {
                return 1.0;
            }

            switch (quality)
            {
                case QualityTier.Decent:
                    return config.decentValueMult > 0.0 ? config.decentValueMult : 1.0;
                case QualityTier.Choice:
                    return config.choiceValueMult > 0.0 ? config.choiceValueMult : 1.0;
                default:
                    return 1.0;
            }
        }

        /// <summary>
        /// Units of <paramref name="to"/> per one unit of <paramref name="from"/>:
        /// tradeValue(from) / tradeValue(to) · (1 − spread). Zero when either is
        /// unpriced (a material) or the pair is the same resource.
        /// </summary>
        public static BigDouble Rate(GameState state, GameDataAsset data, string from, string to)
        {
            if (data?.exchange == null || string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to) || from == to)
            {
                return BigDouble.Zero;
            }

            var valueFrom = Economy.TradeValuePerUnit(state, data, from);
            var valueTo = Economy.TradeValuePerUnit(state, data, to);
            if (valueFrom <= BigDouble.Zero || valueTo <= BigDouble.Zero)
            {
                return BigDouble.Zero;
            }

            // Mabon's tide eases the spread a few points (design §15) — the
            // caravan keeps the day too, in its way. Clamped so an authored
            // ease can never push the spread negative and mint value.
            var spread = System.Math.Max(0.0, data.exchange.spread - Wheel.SpreadEase(state, data));
            return valueFrom / valueTo * new BigDouble(1.0 - spread);
        }

        /// <summary>
        /// Units of <paramref name="to"/> received for spending <paramref name="amount"/>
        /// of <paramref name="from"/>: the exact rate × amount. Deliberately NOT
        /// rounded — goods are fractional (BigDouble) throughout, and any
        /// round-*up* mints value on a round trip (a 1-unit A→B→A cycle would net
        /// positive despite the spread), which the naive favourable-rounding did.
        /// </summary>
        public static BigDouble Quote(GameState state, GameDataAsset data, string from, string to, BigDouble amount)
        {
            return Quote(state, data, from, to, amount, QualityTier.Poor);
        }

        /// <summary>
        /// The quality-aware quote: a Decent or Choice trade-in is worth its
        /// value multiplier more of the plain good coming back.
        /// </summary>
        public static BigDouble Quote(GameState state, GameDataAsset data, string from, string to, BigDouble amount, QualityTier quality)
        {
            if (amount <= BigDouble.Zero)
            {
                return BigDouble.Zero;
            }

            var rate = Rate(state, data, from, to);
            return rate <= BigDouble.Zero
                ? BigDouble.Zero
                : rate * amount * new BigDouble(QualityValueMultiplier(data, quality));
        }

        /// <summary>
        /// What a fraction-of-holdings amount ("half", "all") comes to, in
        /// whole units — the caravan counts goods, and the card names the
        /// amount in the same whole units every other readout uses, so a part
        /// share that quietly spent 2.5 under a label reading 2 would have the
        /// card lying about its own deal. A whole trade still passes
        /// <paramref name="have"/> through untouched so the good ends at zero
        /// rather than a floating-point crumb the UI would round away and the
        /// player would never be able to spend.
        /// </summary>
        public static BigDouble Portion(BigDouble have, double fraction)
        {
            if (have <= BigDouble.Zero || fraction <= 0.0)
            {
                return BigDouble.Zero;
            }

            if (fraction >= 1.0)
            {
                return have;
            }

            // A part share of a small pile still answers with something: a
            // quarter of three is one, not a dead button. Under a whole unit
            // held there is nothing to give, and the row stays hidden.
            var part = BigDouble.Floor(have * new BigDouble(fraction));
            return part < BigDouble.One && have >= BigDouble.One ? BigDouble.One : part;
        }

        /// <summary>
        /// Barter <paramref name="amount"/> of <paramref name="from"/> for
        /// <paramref name="to"/>. Returns the units received (0 and no change when
        /// stock is short or the pair isn't tradeable), so the caller can leave
        /// the button disabled.
        /// </summary>
        public static BigDouble TryTrade(GameState state, GameDataAsset data, string from, string to, BigDouble amount)
        {
            return TryTrade(state, data, from, to, amount, QualityTier.Poor);
        }

        /// <summary>
        /// The quality-aware trade: spends from the tier's own pool (camp
        /// stock, the Decent pool, or the held Choice finds) and always pays into
        /// plain camp stock — quality trades DOWN, excess windfalls becoming
        /// more units of an ordinary good.
        /// </summary>
        public static BigDouble TryTrade(GameState state, GameDataAsset data, string from, string to, BigDouble amount, QualityTier quality)
        {
            if (state == null || data == null || amount <= BigDouble.Zero || from == to)
            {
                return BigDouble.Zero;
            }

            var pool = PoolFor(state, quality);
            pool.TryGetValue(from, out var have);
            if (have < amount)
            {
                return BigDouble.Zero;
            }

            var received = Quote(state, data, from, to, amount, quality);
            if (received <= BigDouble.Zero)
            {
                return BigDouble.Zero;
            }

            pool[from] = have - amount;
            state.AddResource(to, received);
            return received;
        }

        /// <summary>What the camp holds of a good at one quality tier — the trade-in ceiling per row.</summary>
        public static BigDouble Held(GameState state, string resourceId, QualityTier quality)
        {
            PoolFor(state, quality).TryGetValue(resourceId, out var have);
            return have;
        }

        private static Dictionary<string, BigDouble> PoolFor(GameState state, QualityTier quality)
        {
            switch (quality)
            {
                case QualityTier.Decent:
                    return state.decentResources;
                case QualityTier.Choice:
                    return state.choiceResources;
                default:
                    return state.resources;
            }
        }
    }
}
