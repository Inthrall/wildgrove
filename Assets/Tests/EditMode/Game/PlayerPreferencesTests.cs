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

        [Test]
        public void AdsRemoved_WithNothingStored_IsFalse()
        {
            // The other default that matters, and it points the other way: unknown
            // has to mean "ads wanted", because this flag decides whether the ads
            // SDK wakes at launch and nobody has paid anything yet.
            var preferences = new PlayerPreferences(new FakeStore());

            Assert.That(preferences.AdsRemoved, Is.False);
        }

        [Test]
        public void AdsRemoved_SurvivesTheRoundTrip()
        {
            // The whole point of remembering it: the next launch has to know
            // before billing can be asked, so it must be readable by an instance
            // that never saw the purchase.
            var store = new FakeStore();
            var preferences = new PlayerPreferences(store);

            preferences.AdsRemoved = true;

            Assert.That(new PlayerPreferences(store).AdsRemoved, Is.True);
        }

        [Test]
        public void AdsRemoved_ClearedAfterARefund_Sticks()
        {
            var store = new FakeStore();
            var preferences = new PlayerPreferences(store) { AdsRemoved = true };

            preferences.AdsRemoved = false;

            Assert.That(new PlayerPreferences(store).AdsRemoved, Is.False);
        }

        [Test]
        public void TheTwoChoices_DoNotShareAKey()
        {
            // They are stored side by side and read at the same moment in the
            // launch; one key would make turning off analytics buy the ads away.
            var store = new FakeStore();
            var preferences = new PlayerPreferences(store);

            preferences.ShareAnalytics = false;
            preferences.AdsRemoved = true;

            Assert.That(preferences.ShareAnalytics, Is.False);
            Assert.That(preferences.AdsRemoved, Is.True);
            Assert.That(store.Written, Has.Count.EqualTo(2));
        }
    }
}
