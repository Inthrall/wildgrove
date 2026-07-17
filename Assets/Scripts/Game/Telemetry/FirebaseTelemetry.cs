using System;
using System.Collections.Generic;
using Firebase;
using Firebase.Analytics;
using Firebase.Crashlytics;
using Firebase.Extensions;

namespace Wildgrove.Game.Telemetry
{
    /// <summary>
    /// The real sink (design §12 Phase 1): Firebase Analytics for product
    /// events, Crashlytics for uncaught crashes and logged non-fatals.
    /// Events fired before the dependency check completes are buffered
    /// (bounded) and flushed on success; if Firebase can't initialise the
    /// buffer is dropped and only the fallback keeps logging. Every event
    /// also mirrors to the fallback (the Unity log) so logcat stays grep-able
    /// in the field.
    /// </summary>
    public sealed class FirebaseTelemetry : ITelemetry
    {
        // Events fire rarely (purchases, sessions, milestones) — a burst
        // bigger than this before init means something is wrong anyway.
        private const int MaxBuffered = 128;

        private readonly ITelemetry _fallback;
        private readonly Queue<(string name, (string key, object value)[] parameters)> _buffer =
            new Queue<(string, (string, object)[])>();

        private bool _ready;
        private bool _failed;

        public FirebaseTelemetry(ITelemetry fallback)
        {
            _fallback = fallback;
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
            {
                if (task.Result == DependencyStatus.Available)
                {
                    // Unhandled Unity exceptions count as crashes, not silent
                    // non-fatals — the stability metric must see them.
                    Crashlytics.ReportUncaughtExceptionsAsFatal = true;
                    _ready = true;
                    while (_buffer.Count > 0)
                    {
                        var buffered = _buffer.Dequeue();
                        Send(buffered.name, buffered.parameters);
                    }
                }
                else
                {
                    _failed = true;
                    _buffer.Clear();
                    UnityEngine.Debug.LogWarning("[telemetry] Firebase unavailable: " + task.Result);
                }
            });
        }

        public void LogEvent(string name, params (string key, object value)[] parameters)
        {
            _fallback.LogEvent(name, parameters);

            if (_ready)
            {
                Send(name, parameters);
            }
            else if (!_failed && _buffer.Count < MaxBuffered)
            {
                _buffer.Enqueue((name, parameters));
            }
        }

        public void LogException(Exception exception)
        {
            _fallback.LogException(exception);
            if (_ready)
            {
                Crashlytics.LogException(exception);
            }
        }

        private static void Send(string name, (string key, object value)[] parameters)
        {
            if (parameters == null || parameters.Length == 0)
            {
                FirebaseAnalytics.LogEvent(name);
                return;
            }

            var converted = new Parameter[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                converted[i] = ToParameter(parameters[i].key, parameters[i].value);
            }

            FirebaseAnalytics.LogEvent(name, converted);
        }

        /// <summary>GA4 parameters are string/long/double — everything else stringifies.</summary>
        private static Parameter ToParameter(string key, object value)
        {
            switch (value)
            {
                case int i:
                    return new Parameter(key, (long)i);
                case long l:
                    return new Parameter(key, l);
                case float f:
                    return new Parameter(key, (double)f);
                case double d:
                    return new Parameter(key, d);
                default:
                    return new Parameter(key, value?.ToString() ?? string.Empty);
            }
        }
    }
}
