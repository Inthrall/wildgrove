using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// Amber (design §10): the premium currency, kept generous and
    /// player-initiated. This is the in-game economy layer — the free earn
    /// (observation sites surface it, rolled in <see cref="Observation"/>), the
    /// rewarded-ad drip, the weekly Play Games cache, and the time-skip sink.
    /// IAP amber packs credit through <see cref="GrantPack"/>. Amber survives
    /// Migration ("you keep … Amber") and can never fill a verse slot — the
    /// gate is not for sale, so no such code path exists.
    /// </summary>
    public static class Amber
    {
        /// <summary>
        /// Cooldown between rewarded Amber-drip claims — the throttle that does
        /// not depend on the ad-watch, so the drip still can't be tapped without
        /// limit once Remove Ads grants the reward with no ad. Tuning value —
        /// safe to adjust.
        /// </summary>
        public const long AdDripCooldownMs = 4L * 60L * 60L * 1000L;

        /// <summary>Cooldown between rewarded time-skips — same role as <see cref="AdDripCooldownMs"/> for the "Hasten a while" reward. Tuning value.</summary>
        public const long TimeSkipCooldownMs = 4L * 60L * 60L * 1000L;

        /// <summary>Unity can't serialize a null section — a zeroed sink also reads as "no amber system".</summary>
        public static bool Configured(EconomyData economy)
        {
            return economy?.amber != null && economy.amber.timeSkipCostAmber > 0.0 && economy.amber.timeSkipHours > 0.0;
        }

        public static bool CanTimeSkip(GameState state, GameDataAsset data, long nowUnixMs)
        {
            return Configured(data.economy)
                && state.amber >= data.economy.amber.timeSkipCostAmber
                && SkipBudgetHours(state, data, nowUnixMs) >= data.economy.amber.timeSkipHours;
        }

        /// <summary>
        /// Spend Amber to instantly credit timeSkipHours of production at the
        /// FULL live rate — unlike offline credit there is no cap and no rate
        /// multiplier; that's what makes it worth paying for. Returns the
        /// hours credited, or 0 when refused (unconfigured, short, or the
        /// day's skip budget spent — see <see cref="SkipBudgetHours"/>).
        /// </summary>
        public static double TryTimeSkip(GameState state, GameDataAsset data, long nowUnixMs)
        {
            if (!CanTimeSkip(state, data, nowUnixMs))
            {
                return 0.0;
            }

            var amber = data.economy.amber;
            SpendSkipBudget(state, data, nowUnixMs, amber.timeSkipHours);
            state.amber -= amber.timeSkipCostAmber;
            Simulation.Advance(state, data, amber.timeSkipHours * 3600.0);
            return amber.timeSkipHours;
        }

        // ─────────────── The paid-skip budget (the whale throttle) ───────────
        //
        // Sim-time is the only thing money buys here, so bounding how much of
        // it PAID skips may add per real day bounds a heavy spender's pace
        // outright: a day holds 24 natural sim-hours, the budget lets skips
        // add at most timeSkipDailyCapHours more (24 = at most twice a free
        // player's pace). It is a leaky bucket, not a midnight counter — the
        // budget refills at cap/24 per wall-clock hour and holds at the cap,
        // so the rule is the same on every timescale and no timezone or
        // date-rollover question exists. The REWARDED skip stays outside the
        // budget: free players get it too, so it is part of the shared
        // baseline, and its own cooldown already bounds it.

        /// <summary>
        /// Paid-skip hours available right now: the stored remainder plus
        /// everything refilled since it was stamped, held at the cap. A zero
        /// or absent cap means uncapped; an unstamped state (fresh run, older
        /// save) starts with the budget full.
        /// </summary>
        public static double SkipBudgetHours(GameState state, GameDataAsset data, long nowUnixMs)
        {
            var cap = data?.economy?.amber != null ? data.economy.amber.timeSkipDailyCapHours : 0.0;
            if (cap <= 0.0)
            {
                return double.MaxValue;
            }

            if (state.timeSkipBudgetStampUnixMs <= 0L)
            {
                return cap;
            }

            var refilled = (nowUnixMs - state.timeSkipBudgetStampUnixMs) / 3600000.0 * (cap / 24.0);
            var budget = state.timeSkipBudgetHours + (refilled > 0.0 ? refilled : 0.0);
            return budget < cap ? budget : cap;
        }

        /// <summary>Milliseconds until the budget next covers one full skip, or 0 when it already does — drives the hasten row's countdown.</summary>
        public static long SkipBudgetRemainingMs(GameState state, GameDataAsset data, long nowUnixMs)
        {
            var amber = data?.economy?.amber;
            if (amber == null || amber.timeSkipDailyCapHours <= 0.0)
            {
                return 0L;
            }

            var deficit = amber.timeSkipHours - SkipBudgetHours(state, data, nowUnixMs);
            if (deficit <= 0.0)
            {
                return 0L;
            }

            return (long)System.Math.Ceiling(deficit / (amber.timeSkipDailyCapHours / 24.0) * 3600000.0);
        }

        private static void SpendSkipBudget(GameState state, GameDataAsset data, long nowUnixMs, double hours)
        {
            var cap = data?.economy?.amber != null ? data.economy.amber.timeSkipDailyCapHours : 0.0;
            if (cap <= 0.0)
            {
                return;
            }

            state.timeSkipBudgetHours = SkipBudgetHours(state, data, nowUnixMs) - hours;
            state.timeSkipBudgetStampUnixMs = nowUnixMs;
        }

        // ───────────── Settle the ledger (design §9's sink slate) ────────────
        //
        // When an absence outruns the away cap, the hours beyond it are simply
        // lost. The welcome-back sheet may offer to settle them: credit the
        // uncovered remainder at the FULL live rate for Amber. That is exactly
        // what a paid skip sells, so the hours draw from the same leaky budget
        // (the ×2 pin holds with no new rule) and the price is pro-rata on the
        // skip's own rate — the ledger can never be a cheaper skip, only a
        // right-sized one. The offer is a moment, not a debt: it lives only as
        // long as the summary that measured the absence, and nothing about it
        // is persisted.

        /// <summary>The hours an absence left uncredited — real time beyond the away cap; 0 for a covered absence.</summary>
        public static double UncoveredHours(OfflineSummary summary)
        {
            if (summary == null)
            {
                return 0.0;
            }

            var uncovered = (summary.realSeconds - summary.creditedSeconds) / 3600.0;
            return uncovered > 0.0 ? uncovered : 0.0;
        }

        /// <summary>Amber per settled hour — the paid skip's own rate, so the ledger can never undercut the skip.</summary>
        public static double LedgerRatePerHour(GameDataAsset data)
        {
            var amber = data?.economy?.amber;
            return amber != null && amber.timeSkipHours > 0.0 && amber.timeSkipCostAmber > 0.0
                ? amber.timeSkipCostAmber / amber.timeSkipHours
                : 0.0;
        }

        /// <summary>
        /// The hours a settle would credit right now: the uncovered remainder,
        /// held to what the paid-skip budget still allows. 0 when the amber
        /// system is inert.
        /// </summary>
        public static double LedgerHoursOnOffer(GameState state, GameDataAsset data, double uncoveredHours, long nowUnixMs)
        {
            if (state == null || uncoveredHours <= 0.0 || !Configured(data?.economy))
            {
                return 0.0;
            }

            var budget = SkipBudgetHours(state, data, nowUnixMs);
            return uncoveredHours < budget ? uncoveredHours : budget;
        }

        /// <summary>What settling <paramref name="hours"/> costs, in whole Amber — fractions round up, never down.</summary>
        public static double LedgerCostAmber(GameDataAsset data, double hours)
        {
            if (hours <= 0.0)
            {
                return 0.0;
            }

            return System.Math.Ceiling(hours * LedgerRatePerHour(data));
        }

        /// <summary>Whether the offer stands and is affordable — the settle button's enabled state.</summary>
        public static bool CanSettleLedger(GameState state, GameDataAsset data, double uncoveredHours, long nowUnixMs)
        {
            var hours = LedgerHoursOnOffer(state, data, uncoveredHours, nowUnixMs);
            return hours > 0.0 && state.amber >= LedgerCostAmber(data, hours);
        }

        /// <summary>
        /// Settle the ledger: spend the Amber and the skip budget, and credit
        /// the offered hours at the full live rate — no cap, no offline
        /// multiplier, exactly as <see cref="TryTimeSkip"/> credits its own.
        /// Returns the hours credited, or 0 when refused (nothing uncovered,
        /// the budget dry, or the warden short).
        /// </summary>
        public static double TrySettleLedger(GameState state, GameDataAsset data, double uncoveredHours, long nowUnixMs)
        {
            if (!CanSettleLedger(state, data, uncoveredHours, nowUnixMs))
            {
                return 0.0;
            }

            var hours = LedgerHoursOnOffer(state, data, uncoveredHours, nowUnixMs);
            SpendSkipBudget(state, data, nowUnixMs, hours);
            state.amber -= LedgerCostAmber(data, hours);
            Simulation.Advance(state, data, hours * 3600.0);
            return hours;
        }

        /// <summary>
        /// The Amber a familiar's rename asks (design §4: rename any time), or 0
        /// when the amber system is inert — a rename is free then, never blocked.
        /// </summary>
        public static double RenameCost(GameDataAsset data)
        {
            var amber = data?.economy?.amber;
            return amber != null && amber.renameCostAmber > 0.0 ? amber.renameCostAmber : 0.0;
        }

        /// <summary>Whether a rename is affordable right now — free (cost 0), or the warden holds enough Amber for the price.</summary>
        public static bool CanRename(GameState state, GameDataAsset data)
        {
            return state != null && state.amber >= RenameCost(data);
        }

        /// <summary>
        /// Rename a familiar for its Amber price (design §4) — the arrival
        /// naming pays the same price. The cost is spent only when the name
        /// actually changes: a blank or unchanged name (e.g. keeping the
        /// suggested arrival name) is a free no-op, and it's never spent when
        /// the warden is short. Returns whether the name changed.
        /// </summary>
        public static bool TryRename(GameState state, GameDataAsset data, Familiar familiar, string name)
        {
            if (state == null || familiar == null || string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            // Re-typing the same name spends nothing — the price is for a change.
            if (name.Trim() == familiar.name)
            {
                return false;
            }

            if (!CanRename(state, data))
            {
                return false;
            }

            Roster.Rename(familiar, name);
            state.amber -= RenameCost(data);
            return true;
        }

        /// <summary>
        /// The Amber naming the warden asks — dearer than a companion's, because
        /// it is bought once for the body the player wears for every run and it
        /// reads on every page. 0 when the amber system is inert, so fixtures and
        /// a pre-amber save name for free rather than being blocked.
        /// </summary>
        public static double WardenRenameCost(GameDataAsset data)
        {
            var amber = data?.economy?.amber;
            return amber != null && amber.wardenRenameCostAmber > 0.0 ? amber.wardenRenameCostAmber : 0.0;
        }

        /// <summary>Whether naming the warden is affordable right now — free (cost 0), or enough Amber in hand for the price.</summary>
        public static bool CanRenameWarden(GameState state, GameDataAsset data)
        {
            return state != null && state.amber >= WardenRenameCost(data);
        }

        /// <summary>
        /// Name the warden for its Amber price. Charged on a change only, like a
        /// companion's: re-typing the same name, or typing blank, is a free
        /// no-op rather than a purchase. Returns whether the name changed.
        /// <para>
        /// Clearing a name back to unnamed is deliberately NOT offered here — a
        /// blank is refused, not treated as "call me the warden again", so a
        /// mis-tap can never silently spend 50 Amber undoing a name.
        /// </para>
        /// </summary>
        public static bool TryRenameWarden(GameState state, GameDataAsset data, string name)
        {
            if (state == null || string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            var trimmed = name.Trim();
            if (trimmed == Warden.DisplayName(state))
            {
                return false;
            }

            if (!CanRenameWarden(state, data))
            {
                return false;
            }

            state.wardenName = trimmed;
            state.amber -= WardenRenameCost(data);
            return true;
        }

        /// <summary>
        /// The Amber naming this run's camp asks (design §9's sink slate) —
        /// between a companion's price and the warden's, and unlike either it
        /// is paid once per RUN: the name folds with the camp, so naming is a
        /// ritual of each region. 0 when the amber system is inert, so
        /// fixtures and a pre-amber save name for free rather than being
        /// blocked.
        /// </summary>
        public static double CampNameCost(GameDataAsset data)
        {
            var amber = data?.economy?.amber;
            return amber != null && amber.campNameCostAmber > 0.0 ? amber.campNameCostAmber : 0.0;
        }

        /// <summary>Whether naming the camp is affordable right now — free (cost 0), or enough Amber in hand for the price.</summary>
        public static bool CanNameCamp(GameState state, GameDataAsset data)
        {
            return state != null && state.amber >= CampNameCost(data);
        }

        /// <summary>
        /// Name this run's camp for its Amber price. Charged on a change only,
        /// like the warden's: re-typing the same name, or typing blank, is a
        /// free no-op rather than a purchase — so a mis-tap can never silently
        /// spend the price undoing a name. Returns whether the name changed.
        /// </summary>
        public static bool TryNameCamp(GameState state, GameDataAsset data, string name)
        {
            if (state == null || string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            var trimmed = name.Trim();
            if (trimmed == Camp.DisplayName(state))
            {
                return false;
            }

            if (!CanNameCamp(state, data))
            {
                return false;
            }

            state.campName = trimmed;
            state.amber -= CampNameCost(data);
            return true;
        }

        /// <summary>
        /// The Amber this run's second craft-queue slot asks (design §9's sink
        /// slate), or 0 when the sink is unconfigured — the row hides then;
        /// nothing a player relies on is lost by its absence.
        /// </summary>
        public static double SecondQueueCost(GameDataAsset data)
        {
            var amber = data?.economy?.amber;
            return amber != null && amber.secondQueueCostAmber > 0.0 ? amber.secondQueueCostAmber : 0.0;
        }

        /// <summary>Whether the second queue can be bought right now — configured, not already this run's, and affordable.</summary>
        public static bool CanBuySecondQueue(GameState state, GameDataAsset data)
        {
            var cost = SecondQueueCost(data);
            return cost > 0.0
                && state != null
                && !state.secondQueueBought
                && state.amber >= cost;
        }

        /// <summary>
        /// Buy this run's second craft-queue slot (design §9): each station
        /// may then hold two standing orders (<see cref="Crafting.OrderCapacity"/>).
        /// Per run — it lapses at the fold, which is what makes it a recurring
        /// sink. Returns whether the purchase happened.
        /// </summary>
        public static bool TryBuySecondQueue(GameState state, GameDataAsset data)
        {
            if (!CanBuySecondQueue(state, data))
            {
                return false;
            }

            state.secondQueueBought = true;
            state.amber -= SecondQueueCost(data);
            return true;
        }

        /// <summary>
        /// The Amber a gift pile's answer asks alongside the pile itself
        /// (design §4's calling gift — the sink slate's early-game entry), or 0
        /// when the amber system is inert, so fixtures and a pre-amber save
        /// call for free rather than being blocked. Charged only through
        /// <see cref="Gifts.LeavePile"/>: unasked arrivals — the first vole and
        /// raven, a bonded companion crossing the fold, the Drover's pony —
        /// never route through a pile, so they never pay it.
        /// </summary>
        public static double CallingGiftCost(GameDataAsset data)
        {
            var amber = data?.economy?.amber;
            return amber != null && amber.callingGiftAmber > 0.0 ? amber.callingGiftAmber : 0.0;
        }

        /// <summary>Whether the calling gift is affordable right now — free (cost 0), or enough Amber in hand for the asking.</summary>
        public static bool CanPayCallingGift(GameState state, GameDataAsset data)
        {
            return state != null && state.amber >= CallingGiftCost(data);
        }

        /// <summary>Whether the rewarded Amber drip is configured and off cooldown — gates the "Watch" button on both the ad and the ad-free (Remove Ads) paths.</summary>
        public static bool CanGrantDrip(GameState state, GameDataAsset data, long nowUnixMs)
        {
            var amber = data?.economy?.amber;
            if (amber == null || amber.adDripAmber <= 0.0)
            {
                return false;
            }

            return state.adDripClaimedUnixMs <= 0L
                || nowUnixMs - state.adDripClaimedUnixMs >= AdDripCooldownMs;
        }

        /// <summary>Milliseconds until the rewarded Amber drip re-arms, or 0 when it's ready now — drives the amber card's countdown.</summary>
        public static long AdDripCooldownRemainingMs(GameState state, GameDataAsset data, long nowUnixMs)
        {
            var amber = data?.economy?.amber;
            if (amber == null || amber.adDripAmber <= 0.0 || state.adDripClaimedUnixMs <= 0L)
            {
                return 0L;
            }

            var remaining = AdDripCooldownMs - (nowUnixMs - state.adDripClaimedUnixMs);
            return remaining > 0L ? remaining : 0L;
        }

        /// <summary>
        /// Credit the rewarded-ad Amber drip (design §10). The caller shows the
        /// ad and only calls this on the reward; returns the amount granted, or
        /// 0 when unconfigured or still cooling down. Stamps the claim time so
        /// the cooldown holds even when Remove Ads grants without an ad.
        /// </summary>
        public static double GrantDrip(GameState state, GameDataAsset data, long nowUnixMs)
        {
            if (!CanGrantDrip(state, data, nowUnixMs))
            {
                return 0.0;
            }

            var amber = data.economy.amber;
            state.amber += amber.adDripAmber;
            state.adDripClaimedUnixMs = nowUnixMs;
            return amber.adDripAmber;
        }

        /// <summary>Whether the rewarded time-skip is off cooldown — gates "Hasten a while" on both the ad and the ad-free paths. (The amber-paid <see cref="TryTimeSkip"/> is throttled by its cost and the skip budget, not this.)</summary>
        public static bool CanRewardedTimeSkip(GameState state, long nowUnixMs)
        {
            return state.timeSkipClaimedUnixMs <= 0L
                || nowUnixMs - state.timeSkipClaimedUnixMs >= TimeSkipCooldownMs;
        }

        /// <summary>Stamp a rewarded time-skip's claim time, re-arming the cooldown. Called by the sim caller after it credits the skip.</summary>
        public static void StampRewardedTimeSkip(GameState state, long nowUnixMs)
        {
            state.timeSkipClaimedUnixMs = nowUnixMs;
        }

        /// <summary>Milliseconds until the rewarded time-skip re-arms, or 0 when it's ready now — drives the camp strip's countdown.</summary>
        public static long RewardedTimeSkipCooldownRemainingMs(GameState state, long nowUnixMs)
        {
            if (state.timeSkipClaimedUnixMs <= 0L)
            {
                return 0L;
            }

            var remaining = TimeSkipCooldownMs - (nowUnixMs - state.timeSkipClaimedUnixMs);
            return remaining > 0L ? remaining : 0L;
        }

        /// <summary>
        /// Whether the weekly Amber cache is configured and none has been taken
        /// in the week the run is standing in — the card's "ready" reading, not
        /// a gate (see <see cref="ReceiveWeeklyCache"/>).
        /// <para>
        /// The week is the calendar's, not a seven-day cooldown counted off the
        /// last claim (which is what this was until 2026-08-12). Two reasons,
        /// and the second is the one that decided it: a cooldown has no anchor
        /// at all before the first ever claim, so a countdown drawn from it had
        /// nothing to say to a new run; and a claim taken late in the week used
        /// to push the next one late as well, walking the cache slowly around
        /// the calendar and away from whatever day Play sets its own out on.
        /// </para>
        /// </summary>
        public static bool WeeklyCacheDue(GameState state, GameDataAsset data, long nowUnixMs)
        {
            var amber = data?.economy?.amber;
            if (amber == null || amber.weeklyCacheAmber <= 0.0)
            {
                return false;
            }

            // An unstamped claim is 0, which is before every week the game will
            // ever be played in — so a new run is due one, as it should be.
            return state.weeklyCacheClaimedUnixMs < Wheel.WeekStartMs(nowUnixMs, state.utcOffsetMinutes);
        }

        /// <summary>
        /// Milliseconds until the next weekly cache is due: the warden's week
        /// turning over. Never zero and never anchored on a claim, so the one
        /// number serves both readings a surface can want of it — how long the
        /// player has left to go and look for the cache this week, and how long
        /// until the next one is set out.
        /// </summary>
        public static long WeeklyCacheNextDueInMs(GameState state, long nowUnixMs)
        {
            return Wheel.NextWeekStartMs(nowUnixMs, state.utcOffsetMinutes) - nowUnixMs;
        }

        /// <summary>
        /// Receive the weekly Amber cache that Play Games has set out (design
        /// §11): credit its pile and stamp the arrival so the card can say when
        /// the next is due. Returns the amount, or 0 when unconfigured.
        ///
        /// Deliberately unconditional. Play enforces the once-a-week cadence on
        /// its side, so a delivery that lands early is Play's arithmetic, not an
        /// exploit — and refusing it would drop a reward the player has already
        /// been promised and can never be offered again. The cooldown here is a
        /// countdown for the page, not a gate.
        /// </summary>
        public static double ReceiveWeeklyCache(GameState state, GameDataAsset data, long nowUnixMs)
        {
            var amount = data?.economy?.amber != null ? data.economy.amber.weeklyCacheAmber : 0.0;
            if (state == null || amount <= 0.0)
            {
                return 0.0;
            }

            state.amber += amount;
            state.weeklyCacheClaimedUnixMs = nowUnixMs;
            return amount;
        }

        /// <summary>
        /// Credit a purchased Amber pack (design §10 IAP). The caller resolves the
        /// pile from the store catalogue by product id; a non-positive amount is a
        /// no-op so an unconfigured pack can never mint amber. Returns the amount.
        /// </summary>
        public static double GrantPack(GameState state, double amount)
        {
            if (amount <= 0.0)
            {
                return 0.0;
            }

            state.amber += amount;
            return amount;
        }
    }
}
