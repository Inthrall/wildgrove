using System;

namespace Wildgrove.Game.Telemetry
{
    /// <summary>
    /// The analytics/crash-reporting seam (design §12 Phase 1: "Crashlytics +
    /// basic analytics events"). Game code logs product events and notable
    /// caught exceptions through this; the sink behind it is swappable —
    /// <see cref="UnityLogTelemetry"/> until the Firebase project exists, then
    /// Firebase Analytics + Crashlytics implement it without touching the call
    /// sites. Event names and parameter keys are snake_case to match GA4
    /// conventions from day one.
    /// </summary>
    public interface ITelemetry
    {
        /// <summary>Record a product event, e.g. ("upgrade_purchased", ("upgrade_id", "flint-sickle")).</summary>
        void LogEvent(string name, params (string key, object value)[] parameters);

        /// <summary>Record a caught-but-notable exception (a crash reporter's non-fatal).</summary>
        void LogException(Exception exception);

        /// <summary>
        /// Turn product-event collection on or off — the inside cover's "what
        /// this book tells us" choice, applied at launch and whenever it is
        /// changed. Crash reporting is deliberately outside this: it carries no
        /// play data, and a build that stops reporting its own crashes cannot
        /// be fixed.
        /// </summary>
        void SetCollectionEnabled(bool enabled);

        /// <summary>
        /// Pass on the answer Google's consent form got — the regional one (EEA
        /// and UK), which is not ours to ask and not the same choice as
        /// <see cref="SetCollectionEnabled"/>. That one is the player's
        /// preference about product events; this one is whether the sink may use
        /// storage at all, and it governs ads as well as analytics.
        /// <para>
        /// The two are independent and both must hold: a player can share their
        /// notes and still be in a region that has refused storage. Called
        /// whenever the answer is known or re-known — first launch, a cached
        /// answer on a later one, and after the form is re-opened from the
        /// inside cover.
        /// </para>
        /// </summary>
        void SetConsent(bool granted);
    }
}
