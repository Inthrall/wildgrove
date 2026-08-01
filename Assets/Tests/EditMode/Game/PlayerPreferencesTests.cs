using System.Collections.Generic;
using NUnit.Framework;
using Wildgrove.Game;

namespace Wildgrove.Game.Tests
{
    /// <summary>
    /// The inside cover's one stored choice. The default is the part worth
    /// pinning: it ships to players who already have the game installed, and a
    /// default of false would silently opt every one of them out on update —
    /// a change nobody asked for, invisible until the analytics went quiet.
    /// </summary>
    public class PlayerPreferencesTests
    {
        private sealed class FakeStore : IPreferenceStore
        {
            internal readonly Dictionary<string, bool> Written = new Dictionary<string, bool>();

            public bool GetBool(string key, bool fallback)
            {
                return Written.TryGetValue(key, out var value) ? value : fallback;
            }

            public void SetBool(string key, bool value)
            {
                Written[key] = value;
            }
        }

        [Test]
        public void ShareAnalytics_WithNothingStored_IsOn()
        {
            var preferences = new PlayerPreferences(new FakeStore());

            Assert.That(preferences.ShareAnalytics, Is.True);
        }

        [Test]
        public void ShareAnalytics_SurvivesTheRoundTrip()
        {
            var store = new FakeStore();
            var preferences = new PlayerPreferences(store);

            preferences.ShareAnalytics = false;

            Assert.That(preferences.ShareAnalytics, Is.False);
            // Read back through a second instance: the choice belongs to the
            // device, not to the object that happened to set it.
            Assert.That(new PlayerPreferences(store).ShareAnalytics, Is.False);
        }

        [Test]
        public void ShareAnalytics_TurnedBackOn_Sticks()
        {
            var store = new FakeStore();
            var preferences = new PlayerPreferences(store) { ShareAnalytics = false };

            preferences.ShareAnalytics = true;

            Assert.That(new PlayerPreferences(store).ShareAnalytics, Is.True);
        }
    }
}
