using System;

namespace Wildgrove.Game.Services
{
    /// <summary>
    /// The Play Games Services seam — sign-in, achievements, and cloud save
    /// (Snapshots). Game code drives all three through this; the backend is
    /// swappable — <see cref="StubGameServices"/> until the Play Games plugin
    /// implementation exists, then the real service implements it without
    /// touching the call sites.
    /// </summary>
    public interface IGameServices
    {
        /// <summary>True once the player is authenticated with Play Games.</summary>
        bool IsSignedIn { get; }

        /// <summary>
        /// Attempt sign-in (silent, falling back to interactive on first run).
        /// <paramref name="onComplete"/> receives the resulting signed-in state.
        /// </summary>
        void SignIn(Action<bool> onComplete = null);

        /// <summary>Unlock an achievement by its encoded ID (see <see cref="AchievementIds"/>). No-op if already unlocked.</summary>
        void UnlockAchievement(string achievementId);

        /// <summary>Read the cloud save blob (null when none exists or signed out).</summary>
        void LoadCloud(Action<string> onLoaded);

        /// <summary>
        /// Write the cloud save blob (no-op when signed out). <paramref name="savedAtUnixMs"/>
        /// is the save's timestamp, recorded as the snapshot's played-time so the
        /// Snapshots layer's UseLongestPlaytime conflict resolution picks the most
        /// recent save — matching GameLoop's own newest-wins reconcile — instead
        /// of comparing zeros and resolving arbitrarily.
        /// </summary>
        void SaveCloud(string data, long savedAtUnixMs, Action onComplete = null);
    }
}
