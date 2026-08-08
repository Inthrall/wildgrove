using System.Collections.Generic;
using Wildgrove.Data;
using Wildgrove.Game.Services;
using Wildgrove.Sim;

namespace Wildgrove.Game
{
    /// <summary>
    /// What the run still owes the player a moment for: a familiar waiting to be
    /// named, a rung of the slot ladder just opened, a bond made, a Play Games
    /// Reward set out, the haul credited while they were away. The HUD drains
    /// these as it shows them.
    /// <para>
    /// Nothing here reads or writes game state, which is the point: a
    /// celebration that never shows, or shows twice, is a bookkeeping mistake in
    /// these queues and marks — and now one a test can catch.
    /// </para>
    /// </summary>
    public sealed class Announcements
    {
        // Familiars the player has been introduced to (in-memory — a loaded kith
        // has already been met). A newly arrived, non-bonded familiar queues for
        // the naming sheet; bonded familiars have canonical names and their own
        // celebration (design §4).
        private readonly Queue<Familiar> _arrivals = new Queue<Familiar>();
        private readonly HashSet<string> _announced = new HashSet<string>();

        private readonly Queue<RewardGrant> _rewards = new Queue<RewardGrant>();

        // The kith's slot count is DERIVED (lifetime verses sung, plus bought
        // slots), so nothing raises an event when the ladder widens — it has to
        // be noticed. -1 means "not yet seen": the first look seeds the mark
        // silently, so a loaded ladder isn't announced as a new one.
        private int _seenKithSlots = -1;

        /// <summary>How many rewards have landed this session — lets a caller tell whether its own re-read found anything.</summary>
        public int RewardsReceived { get; private set; }

        /// <summary>The credited absence awaiting its welcome-back sheet, or null.</summary>
        public OfflineSummary PendingOfflineSummary { get; private set; }

        // The summary already handed to a welcome-back sheet and still awaiting
        // its answer. Kept because that sheet's "Double it" grants the haul a
        // second time on a reward that lands much later — long enough for the run
        // underneath to have been replaced by an adopted cloud save.
        private OfflineSummary _outstandingOfflineSummary;

        /// <summary>The most recently earned bond awaiting its celebration, or null.</summary>
        public BondData PendingBondCelebration { get; private set; }

        /// <summary>The slot count just reached, awaiting its celebration; 0 when none is pending.</summary>
        public int PendingSlotCelebration { get; private set; }

        /// <summary>Queue any newly arrived, un-met, non-bonded familiar for the naming sheet.</summary>
        public void NoticeArrivals(IEnumerable<Familiar> roster)
        {
            if (roster == null)
            {
                return;
            }

            foreach (var familiar in roster)
            {
                if (_announced.Add(familiar.id) && !familiar.bonded)
                {
                    _arrivals.Enqueue(familiar);
                }
            }
        }

        /// <summary>Take a whole roster as already met — a load, an adopted cloud save, a kith carried across a fold.</summary>
        public void MarkArrivalsSeen(IEnumerable<Familiar> roster)
        {
            if (roster == null)
            {
                return;
            }

            foreach (var familiar in roster)
            {
                _announced.Add(familiar.id);
            }
        }

        /// <summary>The next familiar awaiting a name (without dequeuing), or null.</summary>
        public Familiar PeekArrival()
        {
            return _arrivals.Count > 0 ? _arrivals.Peek() : null;
        }

        /// <summary>Claim the next arrival (the naming sheet dismisses it once named/accepted).</summary>
        public Familiar TakeArrival()
        {
            return _arrivals.Count > 0 ? _arrivals.Dequeue() : null;
        }

        /// <summary>
        /// Watch the slot ladder for a rung the player just earned, returning the
        /// new count when it widened (0 otherwise). Both ways of widening it — a
        /// verse sung past a milestone, a slot bought — land in the same derived
        /// count, so watching the count catches both and can't be forgotten at a
        /// new call site.
        /// </summary>
        public int NoticeKithSlots(int slots)
        {
            var opened = _seenKithSlots >= 0 && slots > _seenKithSlots ? slots : 0;
            if (opened > 0)
            {
                PendingSlotCelebration = opened;
            }

            _seenKithSlots = slots;
            return opened;
        }

        /// <summary>Forget the slot mark, so the next look re-seeds it silently — for a ladder that arrived rather than was earned.</summary>
        public void MarkKithSlotsSeen()
        {
            _seenKithSlots = -1;
        }

        /// <summary>Claim the pending slot celebration (clears it), or 0.</summary>
        public int TakeSlotCelebration()
        {
            var slots = PendingSlotCelebration;
            PendingSlotCelebration = 0;
            return slots;
        }

        /// <summary>A reward has been delivered and owes the player its confirmation.</summary>
        public void QueueReward(RewardGrant grant)
        {
            if (grant == null)
            {
                return;
            }

            _rewards.Enqueue(grant);
            RewardsReceived++;
        }

        /// <summary>The next delivered reward still owed its confirmation, or null.</summary>
        public RewardGrant TakeReward()
        {
            return _rewards.Count > 0 ? _rewards.Dequeue() : null;
        }

        /// <summary>A bond was made — a companion is rare enough to deserve a moment.</summary>
        public void CelebrateBond(BondData bond)
        {
            PendingBondCelebration = bond;
        }

        /// <summary>Claim the pending bond celebration (clears it), or null.</summary>
        public BondData TakeBondCelebration()
        {
            var bond = PendingBondCelebration;
            PendingBondCelebration = null;
            return bond;
        }

        /// <summary>The sabbat whose tier just landed, owed its moment — null when none is.</summary>
        public string PendingKeepingSabbatId { get; private set; }

        /// <summary>The tier that landed (1..3), meaningful only beside the id above.</summary>
        public int PendingKeepingTier { get; private set; }

        /// <summary>
        /// A keeping's tier landed (design §15) — the fire's answer. Tiers only
        /// cross at a live offer, never offline, so an in-memory mark is whole:
        /// nothing can be owed from an absence. A later tier in the same tide
        /// overwrites an unshown earlier one — the deeper word carries both.
        /// </summary>
        public void CelebrateKeeping(string sabbatId, int tier)
        {
            if (string.IsNullOrEmpty(sabbatId) || tier <= 0)
            {
                return;
            }

            PendingKeepingSabbatId = sabbatId;
            PendingKeepingTier = tier;
        }

        /// <summary>Claim the pending keeping celebration (clears it) — null when none is owed.</summary>
        public string TakeKeepingCelebration(out int tier)
        {
            tier = PendingKeepingTier;
            var sabbatId = PendingKeepingSabbatId;
            PendingKeepingSabbatId = null;
            PendingKeepingTier = 0;
            return sabbatId;
        }

        /// <summary>
        /// Offer a credited absence for the welcome-back sheet. An unshown summary
        /// from a previous absence keeps priority — it credited earlier, larger
        /// time, so a top-up must not clobber it. Returns true when this one took.
        /// </summary>
        public bool OfferOfflineSummary(OfflineSummary summary)
        {
            if (PendingOfflineSummary != null || summary == null || summary.creditedSeconds <= 0.0)
            {
                return false;
            }

            PendingOfflineSummary = summary;
            return true;
        }

        /// <summary>Collect (and clear) the offline summary, so the welcome-back sheet shows once.</summary>
        public OfflineSummary TakeOfflineSummary()
        {
            var summary = PendingOfflineSummary;
            PendingOfflineSummary = null;
            _outstandingOfflineSummary = summary;
            return summary;
        }

        /// <summary>
        /// Whether this summary still credits the run in hand. A summary taken by
        /// a sheet and since dropped answers false: the gains it lists were
        /// gathered by a state nothing holds any more, so granting them would pay
        /// out an absence the current run never had.
        /// </summary>
        public bool IsOfflineSummaryCurrent(OfflineSummary summary)
        {
            return summary != null && ReferenceEquals(summary, _outstandingOfflineSummary);
        }

        /// <summary>
        /// Discard the pending summary — it credited a run that has since been
        /// replaced. The one already taken by an open sheet goes with it: its
        /// "Double it" is still live, and the haul it would grant belongs to the
        /// state that was just set aside.
        /// </summary>
        public void DropOfflineSummary()
        {
            PendingOfflineSummary = null;
            _outstandingOfflineSummary = null;
        }

        /// <summary>
        /// Forget every moment owed and everyone met — for a book started again,
        /// where none of it belongs to the run now in hand. The met-list matters
        /// most: familiar ids are minted per run, so a fresh seed kith could
        /// otherwise be taken for one already introduced and never ask for its
        /// names.
        /// </summary>
        public void Forget()
        {
            _arrivals.Clear();
            _announced.Clear();
            _rewards.Clear();
            _seenKithSlots = -1;
            PendingOfflineSummary = null;
            _outstandingOfflineSummary = null;
            PendingBondCelebration = null;
            PendingSlotCelebration = 0;
            PendingKeepingSabbatId = null;
            PendingKeepingTier = 0;
        }
    }
}
