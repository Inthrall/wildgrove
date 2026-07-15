using NUnit.Framework;
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
    }
}
