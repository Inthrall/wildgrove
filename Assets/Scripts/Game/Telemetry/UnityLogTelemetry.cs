using System;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Wildgrove.Game.Telemetry
{
    /// <summary>
    /// The plain <see cref="ITelemetry"/> sink: events go to the Unity log, so
    /// instrumented behaviour is visible in the editor console and in logcat.
    /// Used alone in the editor, and mirrored underneath
    /// <see cref="FirebaseTelemetry"/> on device.
    /// </summary>
    public sealed class UnityLogTelemetry : ITelemetry
    {
        private bool _collecting = true;

        public void LogEvent(string name, params (string key, object value)[] parameters)
        {
            if (!_collecting)
            {
                return;
            }

            Debug.Log(Format(name, parameters));
        }

        public void LogException(Exception exception)
        {
            // Exceptions are not play data — they follow the crash-reporting
            // rule, not the analytics one, and are logged whatever the choice.
            Debug.LogException(exception);
        }

        public void SetCollectionEnabled(bool enabled)
        {
            _collecting = enabled;
        }

        public void SetConsent(bool granted)
        {
            // Nothing leaves the device from here, so there is no storage to
            // govern — but say it, because a consent answer that never arrives
            // and one that arrives as "no" look identical in a log otherwise.
            Debug.Log("[telemetry] consent " + (granted ? "granted" : "denied"));
        }

        /// <summary>One log line per event: "[telemetry] name key=value key=value".</summary>
        public static string Format(string name, params (string key, object value)[] parameters)
        {
            var line = new StringBuilder("[telemetry] ").Append(name);
            foreach (var (key, value) in parameters)
            {
                line.Append(' ').Append(key).Append('=')
                    .Append(Convert.ToString(value, CultureInfo.InvariantCulture));
            }

            return line.ToString();
        }
    }
}
