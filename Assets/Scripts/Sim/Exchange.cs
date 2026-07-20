using BreakInfinity;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// The Exchange (design §9): the silent barter caravan that replaces selling
    /// for Coin. Rates derive from the single trade-value table (<see cref="Economy"/>)
    /// — never authored per pair — less a spread, and small trades round in the
    /// player's favour. Goods buy goods; materials (trade value zero) can't be
    /// bartered. No-ops when exchange data is absent (fixtures).
    /// </summary>
    public static class Exchange
    {
        public static bool Configured(GameDataAsset data)
        {
            return data?.exchange != null;
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
            if (amount <= BigDouble.Zero)
            {
                return BigDouble.Zero;
            }

            var rate = Rate(state, data, from, to);
            return rate <= BigDouble.Zero ? BigDouble.Zero : rate * amount;
        }

        /// <summary>
        /// Barter <paramref name="amount"/> of <paramref name="from"/> for
        /// <paramref name="to"/>. Returns the units received (0 and no change when
        /// stock is short or the pair isn't tradeable), so the caller can leave
        /// the button disabled.
        /// </summary>
        public static BigDouble TryTrade(GameState state, GameDataAsset data, string from, string to, BigDouble amount)
        {
            if (state == null || data == null || amount <= BigDouble.Zero || from == to)
            {
                return BigDouble.Zero;
            }

            if (state.GetResource(from) < amount)
            {
                return BigDouble.Zero;
            }

            var received = Quote(state, data, from, to, amount);
            if (received <= BigDouble.Zero)
            {
                return BigDouble.Zero;
            }

            state.resources[from] = state.GetResource(from) - amount;
            state.AddResource(to, received);
            return received;
        }
    }
}
