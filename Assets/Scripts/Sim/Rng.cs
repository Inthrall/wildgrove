namespace Wildgrove.Sim
{
    /// <summary>
    /// The run's random source: xorshift64* over a ulong state carried in
    /// GameState (and saved with it), so the sim stays deterministic — given
    /// the same state, the same rolls fall in the same order, live or during
    /// an offline catch-up, and tests can pin outcomes by seeding.
    /// </summary>
    public static class Rng
    {
        /// <summary>
        /// A fresh nonzero seed for a new run. Wall-clock ticks are fine here —
        /// this is the one place the sim touches nondeterminism, at run birth.
        /// </summary>
        public static ulong NewSeed()
        {
            return Sanitise((ulong)System.DateTime.UtcNow.Ticks);
        }

        /// <summary>Xorshift64 state must never be zero (it's a fixed point); map 0 to an arbitrary constant.</summary>
        public static ulong Sanitise(ulong state)
        {
            return state == 0UL ? 0x9E3779B97F4A7C15UL : state;
        }

        /// <summary>Advance the state and return a uniform double in [0, 1).</summary>
        public static double NextDouble(ref ulong state)
        {
            state = Sanitise(state);
            state ^= state >> 12;
            state ^= state << 25;
            state ^= state >> 27;

            // xorshift64*: the multiply scrambles the low-quality low bits; the
            // top 53 bits make an exact IEEE double in [0, 1).
            var scrambled = state * 0x2545F4914F6CDD1DUL;
            return (scrambled >> 11) * (1.0 / 9007199254740992.0);
        }
    }
}
