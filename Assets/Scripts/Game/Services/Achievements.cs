using Wildgrove.Data;
using Wildgrove.Sim;

namespace Wildgrove.Game.Services
{
    /// <summary>
    /// Maps game state to the Play Games achievements it satisfies, dispatched
    /// through an injected <see cref="IGameServices"/>. Pure and side-effect-free
    /// but for the unlock calls — so the wiring is testable with a fake service,
    /// without standing up the <see cref="GameLoop"/> MonoBehaviour.
    /// </summary>
    public static class Achievements
    {
        /// <summary>
        /// Re-assert every achievement the current state already satisfies. Play
        /// Games unlocks are idempotent, so this is a no-op once granted — its job
        /// is the sign-in race: a milestone reached while signed out (or before
        /// sign-in resolved) drops its unlock, and the one-shot celebration that
        /// would fire it never shows again on a later launch. Run it once sign-in
        /// completes. Any earned bond satisfies "First kith".
        /// </summary>
        public static void Reassert(IGameServices services, GameState state, GameDataAsset data)
        {
            if (services == null || state == null || data == null)
            {
                return;
            }

            foreach (var _ in Bonds.Earned(state, data))
            {
                services.UnlockAchievement(AchievementIds.FirstKith);
                break;
            }
        }
    }
}
