using System;

namespace Wildgrove.Game
{
    /// <summary>
    /// Wall-clock reading, behind a seam. The save's timestamps and every
    /// cooldown in the game are measured against it, so a test needs to be able
    /// to say what time it is — and a device with a wrong clock is a real case,
    /// not a hypothetical one.
    /// </summary>
    public interface IClock
    {
        /// <summary>Milliseconds since the Unix epoch, UTC.</summary>
        long NowUnixMs();
    }

    /// <summary>The device's own clock — the only implementation the game ships with.</summary>
    public sealed class SystemClock : IClock
    {
        public static readonly SystemClock Instance = new SystemClock();

        public long NowUnixMs()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }
}
