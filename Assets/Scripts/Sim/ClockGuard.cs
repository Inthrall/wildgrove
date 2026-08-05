namespace Wildgrove.Sim
{
    /// <summary>
    /// The run's clock, made one-way. Every cooldown in the game and the whole
    /// offline credit are measured against the device's wall clock, and the
    /// device's wall clock is the player's to set — so without this, winding it
    /// forward pays a full away-cap catch-up, re-arms the rewarded Amber drip,
    /// refills the paid-skip budget, and re-offers the weekly cache; winding it
    /// back costs nothing, and the pair can be repeated all afternoon. Amber is
    /// sold for money, so that is a hole in the storefront rather than a
    /// curiosity.
    /// <para>
    /// The guard is a ratchet, and deliberately the simplest thing that closes
    /// it: the run remembers the latest moment it has ever been told about, and
    /// every reading is held at or above that mark. Winding forward still pays
    /// out — once — and then buys nothing at all until real time catches up
    /// with the mark, so the time is spent rather than minted. Winding back is
    /// simply not seen. No server, no monotonic platform clock, one long in the
    /// save.
    /// </para>
    /// <para>
    /// The cost to an honest player is a genuine backwards correction — a
    /// device whose clock was badly wrong and got fixed — after which idle
    /// credit and cooldowns stand still until the real hour arrives. That is
    /// the same deal every offline game makes, and it is the right way round:
    /// the failure is a pause, not a loss.
    /// </para>
    /// </summary>
    public static class ClockGuard
    {
        /// <summary>
        /// The guarded reading of <paramref name="nowUnixMs"/>, and the high
        /// water mark advanced to match. A null state (before a run is loaded)
        /// passes straight through — there is nothing to protect yet, and
        /// nothing yet reading it.
        /// </summary>
        public static long Now(GameState state, long nowUnixMs)
        {
            if (state == null)
            {
                return nowUnixMs;
            }

            if (nowUnixMs > state.clockHighWaterUnixMs)
            {
                state.clockHighWaterUnixMs = nowUnixMs;
                return nowUnixMs;
            }

            return state.clockHighWaterUnixMs;
        }

        /// <summary>
        /// Whether the device clock currently reads before the run's high water
        /// mark — the run is standing still, and something wound the clock back
        /// (or the mark was set by a wind forward that has not been paid off
        /// yet). Reads without advancing the mark; for telemetry and for the
        /// inside cover, which is the only honest place to say so.
        /// </summary>
        public static bool IsBehind(GameState state, long nowUnixMs)
        {
            return state != null && nowUnixMs < state.clockHighWaterUnixMs;
        }

        /// <summary>
        /// Milliseconds of real time before the device clock catches the mark
        /// back up, or 0 when it already has. What "standing still until" is
        /// worth saying in.
        /// </summary>
        public static long BehindByMs(GameState state, long nowUnixMs)
        {
            var behind = state != null ? state.clockHighWaterUnixMs - nowUnixMs : 0L;
            return behind > 0L ? behind : 0L;
        }
    }
}
