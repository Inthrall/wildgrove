using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Game.Telemetry;

namespace Wildgrove.Game.Tests
{
    /// <summary>
    /// Pins the placeholder telemetry sink's log-line format so playtest logs
    /// stay grep-able while the sink is the Unity console.
    /// </summary>
    public class UnityLogTelemetryTests
    {
        [Test]
        public void Format_NoParameters_IsJustTheEventName()
        {
            Assert.That(UnityLogTelemetry.Format("session_start"),
                Is.EqualTo("[telemetry] session_start"));
        }

        [Test]
        public void Format_Parameters_AppendInOrderAsKeyValuePairs()
        {
            var line = UnityLogTelemetry.Format("upgrade_purchased",
                ("upgrade_id", "flint-sickle"), ("coin_cost", 100.0));

            Assert.That(line, Is.EqualTo("[telemetry] upgrade_purchased upgrade_id=flint-sickle coin_cost=100"));
        }

        [Test]
        public void Format_NumbersUseInvariantCulture()
        {
            Assert.That(UnityLogTelemetry.Format("e", ("v", 1234.5)),
                Is.EqualTo("[telemetry] e v=1234.5"));
        }

        [Test]
        public void SetCollectionEnabled_False_RecordsNothing()
        {
            // The inside cover's "kept to this device" has to mean it at the
            // sink, not merely at the button that set it.
            var lines = new List<string>();

            void Listen(string message, string stack, LogType type) => lines.Add(message);

            Application.logMessageReceived += Listen;
            try
            {
                var sink = new UnityLogTelemetry();
                sink.LogEvent("session_start");
                sink.SetCollectionEnabled(false);
                sink.LogEvent("session_end");

                Assert.That(lines, Has.Exactly(1).EqualTo("[telemetry] session_start"));
                Assert.That(lines, Has.None.EqualTo("[telemetry] session_end"));
            }
            finally
            {
                Application.logMessageReceived -= Listen;
            }
        }
    }
}
