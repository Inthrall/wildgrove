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
    }
}
