using System.Collections.Generic;
using Wildgrove.Data;
using Wildgrove.Sim;

namespace Wildgrove.Game
{
    /// <summary>What a rail cell stands for — the popup and the cell's own face both switch on this.</summary>
    public enum EventRailKind
    {
        /// <summary>A sabbat's tide, open now: the keeping is answerable and the world is leaning.</summary>
        OpenTide,

        /// <summary>The sabbat being waited for, with its tide still shut.</summary>
        ComingSabbat,

        /// <summary>The weekly Play Games cache (design §11) — the other thing in the game with a clock on it.</summary>
        WeeklyCache,
    }

    /// <summary>One cell of the events rail: a face, a countdown, and whether it wants a tap now.</summary>
    public struct EventRailEntry
    {
        /// <summary>Stable within a kind — the rail rebuilds its cells only when the ids change, and the popup is opened by id.</summary>
        public string id;

        public EventRailKind kind;

        /// <summary>The words the cell falls back on where it has no plate, and the popup's heading.</summary>
        public string title;

        /// <summary>Seconds until this turns over, or a negative when there is nothing to count.</summary>
        public double remainingSeconds;

        /// <summary>A word standing where the countdown would go — "set down", "sign in" — or null to count the clock.</summary>
        public string mark;

        /// <summary>True when there is something to do RIGHT NOW — the cell wears the moss and the popup leads with the act.</summary>
        public bool ready;

        /// <summary>The sabbat behind a Wheel cell, or null.</summary>
        public string sabbatId;
    }

    /// <summary>
    /// The events rail's contents (design §15, and whatever else grows a clock):
    /// the short list of time-boxed things running right now, in the order they
    /// deserve the player's attention.
    /// <para>
    /// The Wheel is why this exists. Its five surfaces are scattered through the
    /// journal and four of them draw nothing outside a tide, so a player who
    /// opened the book in a fallow week saw no evidence the system existed —
    /// and a player inside a tide only met it if they happened to be on the
    /// Trail. A rail beside the strip is on screen at every tab, which is the
    /// chrome budget rule's test (GameHud), and it pays for itself in the
    /// strip's width rather than the page's height.
    /// </para>
    /// <para>
    /// Pure over (state, data, now) so the ordering and the inert cases can be
    /// pinned without a screen: the rail draws exactly what comes back, and
    /// nothing here knows the band's height. What does not fit is the caller's
    /// problem — see <see cref="GameHud"/>'s rail — and the list is ordered so
    /// that dropping the tail drops the least.
    /// </para>
    /// </summary>
    public static class EventRail
    {
        public const string OpenTideId = "tide";
        public const string ComingSabbatId = "sabbat-next";
        public const string WeeklyCacheId = "weekly-cache";

        /// <summary>
        /// Fill <paramref name="into"/> with the live entries, most urgent
        /// first. Cleared first, so the caller can hold one list for the run.
        /// </summary>
        /// <param name="playSignedIn">
        /// Whether Play Games knows this player. Passed in rather than read,
        /// because the cache cell must not promise a reward to somebody Play
        /// cannot award one to — and a service handle in here would cost the
        /// whole class its testability for one bool.
        /// </param>
        public static void Collect(GameState state, GameDataAsset data, long nowUnixMs, bool playSignedIn,
            List<EventRailEntry> into)
        {
            into.Clear();
            if (state == null || data == null)
            {
                return;
            }

            CollectWheel(state, data, nowUnixMs, into);
            CollectWeeklyCache(state, data, nowUnixMs, playSignedIn, into);
        }

        /// <summary>
        /// The Wheel's one cell: the open tide while one holds, else the sabbat
        /// being waited for. Never both — a tide that is open IS the news, and
        /// a second cell counting down the one after it would be the rail
        /// spending a fingertip on something four weeks away.
        /// </summary>
        private static void CollectWheel(GameState state, GameDataAsset data, long nowUnixMs, List<EventRailEntry> into)
        {
            var open = Wheel.OpenTide(state, data);
            if (open != null)
            {
                var offerable = AnySlotOfferable(state, data, open);
                into.Add(new EventRailEntry
                {
                    id = OpenTideId,
                    kind = EventRailKind.OpenTide,
                    title = open.displayName,
                    sabbatId = open.id,
                    remainingSeconds = (Wheel.OpenTideCloseMs(state, data) - nowUnixMs) / 1000.0,
                    ready = offerable,
                    mark = offerable ? "set down" : null,
                });
                return;
            }

            var coming = Wheel.NextSabbat(state, data, out _);
            if (coming == null || !Wheel.NextNightOf(state, data, coming, out _, out var opensMs))
            {
                return;
            }

            into.Add(new EventRailEntry
            {
                id = ComingSabbatId,
                kind = EventRailKind.ComingSabbat,
                title = coming.displayName,
                sabbatId = coming.id,
                remainingSeconds = (opensMs - nowUnixMs) / 1000.0,
                ready = false,
            });
        }

        /// <summary>
        /// The weekly Play Games cache (design §11) — the rail's second
        /// inhabitant, and the proof it is a rail rather than a Wheel widget.
        /// <para>
        /// It stands only while the cache is unclaimed. Claimed, the cell goes
        /// entirely rather than counting the next one down: a rail cell is a
        /// thing to act on, and one that spends six days in seven saying "not
        /// yet" is what teaches a player to stop reading the rail — the same
        /// price the Camp row's greyed-out Look was already paying.
        /// </para>
        /// <para>
        /// What it counts while it stands is OUR week turning over, not the
        /// cache arriving. "Due" is only this clock saying the week is up, and
        /// whether Play has set anything out is known by looking
        /// (<see cref="Amber.WeeklyCacheDue"/> says as much itself) — so the
        /// countdown is the one number that is honest either way: how long is
        /// left to go and look. Signed out it says the one thing the player can
        /// act on instead, and says it whether the week is up or not.
        /// </para>
        /// </summary>
        private static void CollectWeeklyCache(GameState state, GameDataAsset data, long nowUnixMs,
            bool playSignedIn, List<EventRailEntry> into)
        {
            var amber = data.economy?.amber;
            if (amber == null || amber.weeklyCacheAmber <= 0.0)
            {
                return;
            }

            if (!playSignedIn)
            {
                into.Add(new EventRailEntry
                {
                    id = WeeklyCacheId,
                    kind = EventRailKind.WeeklyCache,
                    title = "the cache",
                    remainingSeconds = -1.0,
                    mark = "sign in",
                    ready = false,
                });
                return;
            }

            if (!Amber.WeeklyCacheDue(state, data, nowUnixMs))
            {
                return;
            }

            into.Add(new EventRailEntry
            {
                id = WeeklyCacheId,
                kind = EventRailKind.WeeklyCache,
                title = "the cache",
                remainingSeconds = (Wheel.NextWeekStartMs(nowUnixMs, state.utcOffsetMinutes) - nowUnixMs) / 1000.0,
                ready = true,
            });
        }

        /// <summary>
        /// True while the camp holds a whole outstanding offering for any slot
        /// of the open keeping — the cell's "there is something to set down"
        /// mark. Whole-offering only, the keeping's own rule: a cell that lit
        /// for a slot the stores are short of would be a lie a tap away.
        /// <para>
        /// It checks for an EXISTING keeping first, rather than going straight
        /// to <see cref="Keeping.Current"/>, and that guard is load-bearing:
        /// Current generates the keeping on first read, and the slots it draws
        /// come from the content unlocked at that moment (a fold redraws the
        /// unanswered ones; a mid-run unlock never does). The rail is on screen
        /// at every tab, so asking through Current would move every keeping's
        /// draw from "the first time the player looked at it" to "the instant
        /// its tide opened" — a balance change made silently by a piece of
        /// chrome. Until a keeping exists there is nothing to set down, which
        /// is the honest answer anyway.
        /// </para>
        /// </summary>
        private static bool AnySlotOfferable(GameState state, GameDataAsset data, SabbatData open)
        {
            var keeping = state.keeping;
            if (keeping?.slots == null
                || keeping.sabbatId != open.id
                || keeping.hemisphere != state.hemisphere
                || keeping.year != Keeping.YearOfEpochDay(Wheel.OpenNightDay(state, data)))
            {
                return false;
            }

            for (var index = 0; index < keeping.slots.Count; index++)
            {
                if (Keeping.CanOffer(state, data, index))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
