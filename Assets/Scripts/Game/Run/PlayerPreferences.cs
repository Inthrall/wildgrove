using UnityEngine;

namespace Wildgrove.Game
{
    /// <summary>
    /// Where a preference is kept between sessions, behind a seam so the
    /// defaults and the round trip can be exercised without a device — the same
    /// reason <see cref="ISaveStore"/> exists.
    /// </summary>
    public interface IPreferenceStore
    {
        bool GetBool(string key, bool fallback);

        void SetBool(string key, bool value);
    }

    /// <summary>The real one: Unity's PlayerPrefs, flushed on every write.</summary>
    public sealed class PlayerPrefsStore : IPreferenceStore
    {
        public bool GetBool(string key, bool fallback)
        {
            return PlayerPrefs.GetInt(key, fallback ? 1 : 0) == 1;
        }

        public void SetBool(string key, bool value)
        {
            PlayerPrefs.SetInt(key, value ? 1 : 0);
            // Written through immediately: a preference changed and then lost to
            // a process kill is a setting that silently didn't take, which reads
            // as the toggle being broken rather than the save being late.
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// The choices the player makes about the app rather than the run — held
    /// apart from <see cref="Wildgrove.Sim.GameState"/> because they belong to
    /// the device and must survive starting the book again.
    /// <para>
    /// Only analytics sharing lives here today. Ad consent is not a preference
    /// of ours: it belongs to Google's UMP form, which stores its own answer and
    /// is reachable from the same sheet.
    /// </para>
    /// </summary>
    public sealed class PlayerPreferences
    {
        internal const string ShareAnalyticsKey = "wildgrove.settings.shareAnalytics";
        internal const string AdsRemovedKey = "wildgrove.entitlements.adsRemoved";

        private readonly IPreferenceStore _store;

        public PlayerPreferences(IPreferenceStore store)
        {
            _store = store;
        }

        /// <summary>
        /// Whether product events reach the analytics sink — and, since Game
        /// Stats, whether gameplay events reach the gamer profile too. Both are
        /// notes on how the game is played, so they answer to the one switch: a
        /// switch that stopped only one of them would be worse than none, because
        /// the sheet says it stops the telling. Defaults to true — the sink's own
        /// regional consent gate is UMP's job, and a default of false would
        /// quietly turn analytics off for every existing player on the update
        /// that ships this.
        /// <para>
        /// Crash reports are deliberately NOT covered: they carry no play data,
        /// and a build that stops reporting its own crashes cannot be fixed.
        /// The sheet says as much rather than implying it covers everything.
        /// </para>
        /// </summary>
        public bool ShareAnalytics
        {
            get => _store.GetBool(ShareAnalyticsKey, true);
            set => _store.SetBool(ShareAnalyticsKey, value);
        }

        /// <summary>
        /// Whether the store said, last time it was asked, that the ads had been
        /// bought away. Remembered on the device for one reason: the launch has to
        /// decide whether to wake the ads SDK *before* billing can be asked
        /// anything (billing is deliberately off the launch path), and waking it
        /// for a player who paid is exactly what the purchase is meant to prevent.
        /// <para>
        /// This is not the source of truth for the entitlement — the store still
        /// is, and <see cref="Wildgrove.Game.Services.IAds.SetAdsWanted"/> corrects
        /// this launch either way once it answers. Nothing is granted on the
        /// strength of this flag, so the worst a stale one can do is cost a
        /// session its rewarded ads, never hand out something unpaid for.
        /// </para>
        /// </summary>
        public bool AdsRemoved
        {
            get => _store.GetBool(AdsRemovedKey, false);
            set => _store.SetBool(AdsRemovedKey, value);
        }
    }
}
