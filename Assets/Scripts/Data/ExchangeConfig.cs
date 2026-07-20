namespace Wildgrove.Data
{
    /// <summary>
    /// The Exchange's barter constants (design §9). Rates are derived from the
    /// trade-value table, never authored per pair — only the spread and the
    /// small-trade rounding threshold are set here.
    /// </summary>
    public sealed class ExchangeConfig
    {
        /// <summary>The caravan's cut, subtracted from every derived rate (0–1).</summary>
        public double Spread { get; set; }

        /// <summary>Trades whose received amount is below this round up in the player's favour.</summary>
        public double RoundUpBelow { get; set; }
    }
}
