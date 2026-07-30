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
    /// quality tier of the from-good is taken — Fine and Pristine at their
    /// value multipliers — and the caravan always pays in plain goods, so
    /// excess high-quality stock trades down into more units of something else.
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
        /// index — the generator's idiom (nothing persisted, so a reload cannot
        /// reroll the caravan; the deal turns on the same beat for everyone).
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

            // The window index seeds its own throwaway rng — the run's saved
            // stream is never drawn, so browsing the caravan can't shift a roll.
            var seed = Rng.Sanitise((ulong)(nowUnixMs / periodMs));
            var fromIndex = (int)(Rng.NextDouble(ref seed) * goods.Count);
            var toIndex = (int)(Rng.NextDouble(ref seed) * (goods.Count - 1));
            if (toIndex >= fromIndex)
            {
                toIndex++;
            }

            return new ExchangeOffer { from = goods[fromIndex], to = goods[toIndex] };
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
                case QualityTier.Fine:
                    return config.fineValueMult > 0.0 ? config.fineValueMult : 1.0;
                case QualityTier.Pristine:
                    return config.pristineValueMult > 0.0 ? config.pristineValueMult : 1.0;
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

            return valueFrom / valueTo * new BigDouble(1.0 - data.exchange.spread);
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
            return Quote(state, data, from, to, amount, QualityTier.Common);
        }

        /// <summary>
        /// The quality-aware quote: a Fine or Pristine trade-in is worth its
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
        /// What a fraction-of-holdings amount ("half", "all") comes to. A whole
        /// trade passes <paramref name="have"/> through untouched so the good
        /// ends at zero rather than a floating-point crumb the UI would round
        /// away and the player would never be able to spend.
        /// </summary>
        public static BigDouble Portion(BigDouble have, double fraction)
        {
            if (have <= BigDouble.Zero || fraction <= 0.0)
            {
                return BigDouble.Zero;
            }

            return fraction >= 1.0 ? have : have * new BigDouble(fraction);
        }

        /// <summary>
        /// Barter <paramref name="amount"/> of <paramref name="from"/> for
        /// <paramref name="to"/>. Returns the units received (0 and no change when
        /// stock is short or the pair isn't tradeable), so the caller can leave
        /// the button disabled.
        /// </summary>
        public static BigDouble TryTrade(GameState state, GameDataAsset data, string from, string to, BigDouble amount)
        {
            return TryTrade(state, data, from, to, amount, QualityTier.Common);
        }

        /// <summary>
        /// The quality-aware trade: spends from the tier's own pool (camp
        /// stock, the Fine pool, or the held Pristines) and always pays into
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
                case QualityTier.Fine:
                    return state.fineResources;
                case QualityTier.Pristine:
                    return state.pristineResources;
                default:
                    return state.resources;
            }
        }
    }
}
