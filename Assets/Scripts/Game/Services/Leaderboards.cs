using BreakInfinity;
using Wildgrove.Data;
using Wildgrove.Sim;

namespace Wildgrove.Game.Services
{
    /// <summary>
    /// Maps game state to the Play Games leaderboard scores it should post,
    /// dispatched through an injected <see cref="IGameServices"/>. Pure and
    /// side-effect-free but for the submit calls — so the wiring is testable with a
    /// fake service, without standing up the <see cref="GameLoop"/> MonoBehaviour.
    /// Mirrors <see cref="Achievements"/>.
    /// </summary>
    public static class Leaderboards
    {
        /// <summary>
        /// Submit the current best for every leaderboard. Idempotent from Play
        /// Games' side (it keeps only the player's best), so it is safe to call on
        /// each save and on sign-in.
        /// </summary>
        public static void SubmitAll(IGameServices services, GameState state, GameDataAsset data)
        {
            if (services == null || state == null || data == null)
            {
                return;
            }

            services.SubmitScore(LeaderboardIds.Renown, RenownScore(state));
        }

        /// <summary>
        /// Renown log-scaled to a Play Games score. Renown is a <see cref="BigDouble"/>
        /// that outgrows <c>long</c> (max ~9.2e18) early in an idle run, so the raw
        /// value would overflow and scramble the top of the board. log10 × 1e6 is
        /// monotonic in Renown and stays tiny (1e300 Renown → 3e8), preserving order
        /// without overflow. Set the Console leaderboard's format to "fixed, 6
        /// decimals" and the board reads as orders-of-magnitude of Renown.
        /// </summary>
        public static long RenownScore(GameState state)
        {
            // Max(renown, One) keeps log10 ≥ 0: a fresh run's zero Renown would be
            // log10(0) = -infinity otherwise.
            return (long)(BigDouble.Log10(BigDouble.Max(state.renown, BigDouble.One)) * 1_000_000);
        }
    }
}
