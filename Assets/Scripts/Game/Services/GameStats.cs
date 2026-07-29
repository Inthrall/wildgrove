using BreakInfinity;
using Wildgrove.Sim;

namespace Wildgrove.Game.Services
{
    /// <summary>
    /// The declared Game Stats event names. Play drops any event that isn't in the
    /// console's uploaded schema, so this list and
    /// <c>store/play-games/gamestats/PlayerGameEvent.csv</c> are one thing in two
    /// places — change both together. camelCase because Google's own events are
    /// (and <see cref="ProgressUpdate"/> is a name they reserve, not a choice).
    /// </summary>
    public static class StatEventNames
    {
        /// <summary>A carrier's load reaching camp — the units gathered since the last look.</summary>
        public const string HaulArrived = "haulArrived";

        /// <summary>A station finishing batches — the goods crafted since the last look.</summary>
        public const string CraftCompleted = "craftCompleted";

        /// <summary>A windfall caught at a node.</summary>
        public const string WindfallCaught = "windfallCaught";

        /// <summary>A specimen fixed into the Folio.</summary>
        public const string SpecimenFixed = "specimenFixed";

        /// <summary>A verse of the Rite completed.</summary>
        public const string VerseCompleted = "verseCompleted";

        /// <summary>The camp folded — a Migration.</summary>
        public const string MigrationCompleted = "migrationCompleted";

        /// <summary>The player's progression level. Google's reserved event name; the property must be currentProgress.</summary>
        public const string ProgressUpdate = "progressUpdate";
    }

    /// <summary>Declared property keys for <see cref="StatEventNames"/> — likewise mirrored in the console CSV.</summary>
    public static class StatPropertyNames
    {
        public const string Amount = "amount";
        public const string Resource = "resource";
        public const string Verse = "verse";
        public const string Number = "number";

        /// <summary>Google's reserved property on progressUpdate. INT or STRING; ours is INT.</summary>
        public const string CurrentProgress = "currentProgress";
    }

    /// <summary>
    /// Maps game state to the Play Games Game Stats events it should record,
    /// dispatched through an injected <see cref="IGameServices"/> — the same shape
    /// as <see cref="Leaderboards"/> and <see cref="Achievements"/>, so the wiring
    /// is testable with a fake service and no <see cref="GameLoop"/> behind it.
    /// This one holds state (the last totals reported), so it's an instance.
    /// <para>
    /// Five of these are repetitive stats, one of them competitive (resources
    /// gathered, which is also what the Renown board ranks on), and the trails
    /// walked are the progression stat — the Level Up minimum is five, one, and
    /// one. All six ride actions on the free path: nothing here can be reached
    /// only by paying, watching an ad, or owning a reward, which the guideline
    /// requires and which the amber lines would fail.
    /// </para>
    /// <para>
    /// The two continuous stats are reported as deltas against the last figure
    /// sent, because Play aggregates SUM over the events it receives while the
    /// game's own counters are lifetime totals. A delta is only banked once the
    /// event is actually recorded, so a signed-out stretch accumulates instead of
    /// being swallowed.
    /// </para>
    /// </summary>
    public sealed class GameStats
    {
        private readonly IGameServices _services;

        private double _reportedGathered;
        private double _reportedCrafted;
        private int _reportedProgress = -1;

        public GameStats(IGameServices services)
        {
            _services = services;
        }

        /// <summary>
        /// Seed the totals baseline from a run without reporting any of it — at
        /// launch, and again whenever the run is replaced wholesale (cloud-save
        /// adoption). Without it the first flush would post a lifetime total as
        /// though it had all happened in one session, and adopting a further-along
        /// cloud save would post the difference between two runs as gathering.
        /// </summary>
        public void Rebase(GameState state)
        {
            if (state == null)
            {
                return;
            }

            _reportedGathered = TotalGathered(state);
            _reportedCrafted = TotalCrafted(state);
        }

        /// <summary>
        /// Report what has accumulated since the last flush and nudge Play to
        /// upload. Called on the save cadence (autosave, pause, quit): hauls and
        /// crafts land continuously in an idle run, so "as soon as it occurs"
        /// would be an event per tick.
        /// </summary>
        public void Flush(GameState state)
        {
            if (_services == null || state == null)
            {
                return;
            }

            _reportedGathered = ReportDelta(StatEventNames.HaulArrived, TotalGathered(state), _reportedGathered);
            _reportedCrafted = ReportDelta(StatEventNames.CraftCompleted, TotalCrafted(state), _reportedCrafted);
            ReportProgress(state);
            _services.FlushStats();
        }

        /// <summary>A windfall caught (the repetitive tap of the moment-to-moment loop).</summary>
        public void RecordWindfall(string resourceId)
        {
            Record(StatEventNames.WindfallCaught, (StatPropertyNames.Resource, (object)Safe(resourceId)));
        }

        /// <summary>A specimen fixed into the Folio.</summary>
        public void RecordSpecimenFixed(string resourceId)
        {
            Record(StatEventNames.SpecimenFixed, (StatPropertyNames.Resource, (object)Safe(resourceId)));
        }

        /// <summary>A verse of the Rite completed.</summary>
        public void RecordVerseCompleted(string verseId)
        {
            Record(StatEventNames.VerseCompleted, (StatPropertyNames.Verse, (object)Safe(verseId)));
        }

        /// <summary>The camp folded. <paramref name="number"/> is the new run's Migration count.</summary>
        public void RecordMigration(int number)
        {
            Record(StatEventNames.MigrationCompleted, (StatPropertyNames.Number, (object)number));
        }

        /// <summary>
        /// The progression stat: how much of the world the warden has walked.
        /// Waystones seen cross a Migration (Migration.Migrate carries them), so
        /// this only ever climbs — a progression level that fell back to 1 on
        /// every fold would read as a bug on the gamer profile. Sent when it
        /// changes, and on the first flush of a launch, as Google asks.
        /// </summary>
        public void ReportProgress(GameState state)
        {
            if (state == null)
            {
                return;
            }

            var progress = state.seenWaystoneZoneIds != null ? state.seenWaystoneZoneIds.Count : 0;
            if (progress == _reportedProgress)
            {
                return;
            }

            if (Record(StatEventNames.ProgressUpdate, (StatPropertyNames.CurrentProgress, (object)progress)))
            {
                _reportedProgress = progress;
            }
        }

        /// <summary>Lifetime units gathered, across every resource — the competitive stat's source.</summary>
        public static double TotalGathered(GameState state)
        {
            if (state?.lifetimeGathered == null)
            {
                return 0.0;
            }

            var total = BigDouble.Zero;
            foreach (var pair in state.lifetimeGathered)
            {
                total += pair.Value;
            }

            return total.ToDouble();
        }

        /// <summary>Lifetime goods crafted, across every recipe.</summary>
        public static double TotalCrafted(GameState state)
        {
            if (state?.lifetimeCrafted == null)
            {
                return 0.0;
            }

            var total = 0.0;
            foreach (var pair in state.lifetimeCrafted)
            {
                total += pair.Value;
            }

            return total;
        }

        /// <summary>
        /// Post the growth in a lifetime total as one event, returning the figure
        /// now reported. A run deep enough for the total to overflow a double is
        /// past anything a stat can say, so a non-finite reading is left alone
        /// rather than sent as infinity.
        /// </summary>
        private double ReportDelta(string eventName, double total, double reported)
        {
            if (double.IsNaN(total) || double.IsInfinity(total))
            {
                return reported;
            }

            var delta = total - reported;
            if (delta <= 0.0)
            {
                // Totals never fall, so this is a rebase case, not a loss.
                return total;
            }

            return Record(eventName, (StatPropertyNames.Amount, (object)delta)) ? total : reported;
        }

        /// <summary>
        /// Hand one event to Play, reporting whether it was taken. Signed out it
        /// is not: the stat belongs to a gamer profile, and a caller that banks a
        /// delta needs to know the difference between recorded and dropped.
        /// </summary>
        private bool Record(string eventName, params (string key, object value)[] properties)
        {
            if (_services == null || !_services.IsSignedIn)
            {
                return false;
            }

            _services.RecordStat(eventName, properties);
            return true;
        }

        private static string Safe(string id)
        {
            return string.IsNullOrEmpty(id) ? "unknown" : id;
        }
    }
}
