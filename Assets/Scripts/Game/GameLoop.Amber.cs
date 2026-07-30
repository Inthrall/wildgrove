using Wildgrove.Data;
using Wildgrove.Game.Services;
using Wildgrove.Sim;

namespace Wildgrove.Game
{
    // Amber, ads and the store (design §10, §11) — every way the run gains
    // something it didn't gather: a rewarded ad's grant, the time skip, a bought
    // Amber pack or kith slot, a Play Games Reward set out for the player. The
    // entitlement folds are idempotent by design; see GameLoop.cs, which calls
    // them again on every launch and after any adopted cloud save.
    public sealed partial class GameLoop
    {
        /// <summary>
        /// Grant the OfflineBoost rewarded-ad reward: credit the welcome-back
        /// haul a second time. Called from the welcome sheet's "Double it" button
        /// once the rewarded ad reports the reward earned.
        /// </summary>
        public void GrantOfflineBonus(OfflineSummary summary)
        {
            if (summary == null)
            {
                return;
            }

            Simulation.GrantHaul(State, summary.gains);
        }

        /// <summary>
        /// Grant the TimeSkip rewarded-ad reward: advance the run by
        /// <paramref name="hours"/> of gathering, exactly as an offline catch-up
        /// of that length would (subject to the same offline rate and cap).
        /// Refused (returns false) while the reward is still cooling down, so it
        /// can't be tapped without limit once Remove Ads drops the ad.
        /// </summary>
        public bool CreditTimeSkip(double hours)
        {
            if (State == null || hours <= 0.0 || !Amber.CanRewardedTimeSkip(State, NowUnixMs()))
            {
                return false;
            }

            Simulation.AdvanceOffline(State, Data, hours * 3600.0);
            Amber.StampRewardedTimeSkip(State, NowUnixMs());
            return true;
        }

        /// <summary>
        /// Whether a rewarded reward can be taken right now — a loaded ad, or the
        /// Remove Ads entitlement (which grants without one). Every "watch an ad"
        /// button gates its shown/enabled state on this so the reward stays
        /// reachable once ads are removed.
        /// </summary>
        public bool RewardedReady(RewardedPlacement placement) => Store.RemoveAdsOwned || Ads.IsRewardedReady(placement);

        /// <summary>The tail a reward button's label carries — dropped once Remove Ads is owned, since no ad plays.</summary>
        public string RewardedActionSuffix => Store.RemoveAdsOwned ? string.Empty : " (watch a short ad)";

        /// <summary>
        /// Take a rewarded reward for <paramref name="placement"/>. Normally shows
        /// the ad; once Remove Ads is owned the reward is granted immediately with
        /// no ad — the whole point of the purchase. Every rewarded placement routes
        /// through here so "no more ads" stays true for all of them.
        /// </summary>
        public void WatchRewarded(RewardedPlacement placement, System.Action onReward, System.Action onClosed = null)
        {
            if (Store.RemoveAdsOwned)
            {
                onReward?.Invoke();
                onClosed?.Invoke();
                return;
            }

            Ads.ShowRewarded(placement, onReward, onClosed);
        }

        // ─────────────────────── The store's kith slots (design §4) ──────────

        /// <summary>
        /// Fold the store's entitlements into the run (design §4 ladder): the
        /// starter bundle and the plain slot each open a slot, the bundle pays
        /// its one-time Amber. Call sites: startup (saved values bridge until
        /// billing resolves) and every purchase result.
        /// </summary>
        public void SyncKithPurchases()
        {
            if (KithPurchases.Apply(State, Data,
                    Store.IsOwned(StoreProductIds.StarterBundle),
                    Store.IsOwned(StoreProductIds.KithSlot)))
            {
                SaveNow();
            }
        }

        // ───────────────── Play Games Rewards (design §11) ───────────────────

        /// <summary>
        /// Fold the single-use Play Games Rewards the store says are owned. Same
        /// shape as <see cref="SyncKithPurchases"/>: additive, idempotent, and
        /// the reinstall-proof path — the redemption handler catches the moment
        /// one arrives, this catches every launch after.
        /// </summary>
        public void SyncRewardEntitlements()
        {
            var landed = PlayRewards.ApplyDroversHalter(State, Data, Store.IsOwned(RewardProductIds.DroversHalter));

            // Non-short-circuiting on purpose: both entitlements are asked
            // about on every launch, and one landing must not skip the other.
            landed |= PlayRewards.ApplyWayfarersPlate(State, Data, Store.IsOwned(RewardProductIds.WayfarersPlate));

            if (landed)
            {
                SaveNow();
            }
        }

        /// <summary>
        /// Receive a reward Google Play has awarded. Grants it, queues the
        /// confirmation the player is owed, and saves — returning true only then,
        /// because the store acknowledges the order on the strength of this
        /// answer. A refusal (no run loaded yet, or an id this build can't grant)
        /// leaves the order unacknowledged so Play can refund and re-offer it.
        /// </summary>
        private bool OnRewardRedeemed(string productId)
        {
            if (State == null)
            {
                return false;
            }

            var grant = RewardGrants.Apply(State, Data, productId, NowUnixMs());
            if (grant == null)
            {
                return false;
            }

            _announce.QueueReward(grant);
            Telemetry.LogEvent("play_reward_received", ("reward", grant.rewardId));
            SaveNow();
            return true;
        }

        /// <summary>The next delivered reward still owed its confirmation, or null. The sheet pump drains this.</summary>
        public RewardGrant TakePendingReward()
        {
            return _announce.TakeReward();
        }

        /// <summary>
        /// Ask Play whether anything has been set out since the last look, for a
        /// player who redeemed a moment ago and would rather not relaunch. Calls
        /// back with how many rewards landed. Rewards also arrive unprompted on
        /// the first purchase fetch of every launch — this is the manual nudge.
        /// </summary>
        public void CheckPlayRewards(System.Action<int> onComplete)
        {
            var before = _announce.RewardsReceived;
            Store.RestorePurchases(() =>
            {
                SyncRewardEntitlements();
                onComplete?.Invoke(_announce.RewardsReceived - before);
            });
        }

        /// <summary>
        /// Start a store purchase of a kith slot product and fold the
        /// entitlement in on success. The HUD owns the button copy; the result
        /// callback fires on the main thread like every IStore callback.
        /// </summary>
        public void PurchaseKithProduct(string productId, System.Action<StoreResult> onComplete)
        {
            Store.Purchase(productId, result =>
            {
                if (result == StoreResult.Purchased || result == StoreResult.AlreadyOwned)
                {
                    SyncKithPurchases();
                    Telemetry.LogEvent("iap_purchased", ("product", productId));
                }

                onComplete?.Invoke(result);
            });
        }

        /// <summary>Whether the time-skip is configured and affordable — the button's enabled state.</summary>
        public bool CanTimeSkip()
        {
            return Amber.CanTimeSkip(State, Data);
        }

        /// <summary>Spend Amber to instantly credit hours of full-rate production (design §10). Returns the hours credited (0 = refused).</summary>
        public double TimeSkip()
        {
            var cost = Data.economy?.amber?.timeSkipCostAmber ?? 0.0;
            var hours = Amber.TryTimeSkip(State, Data);
            if (hours > 0.0)
            {
                Telemetry.LogEvent("time_skip_used", ("hours", hours), ("amber_cost", cost));
            }

            return hours;
        }

        /// <summary>
        /// Credit the rewarded-ad Amber drip (design §10). The caller shows the
        /// ad and calls this only on the reward; returns the amount granted.
        /// </summary>
        public double GrantAmberDrip()
        {
            var amount = Amber.GrantDrip(State, Data, NowUnixMs());
            if (amount > 0.0)
            {
                Telemetry.LogEvent("amber_drip", ("amount", amount));
                SaveNow();
            }

            return amount;
        }

        /// <summary>Whether the rewarded Amber drip can be taken right now (configured, off cooldown) — a "Watch" button gates its enabled state on this and RewardedReady.</summary>
        public bool CanWatchAmberDrip => Amber.CanGrantDrip(State, Data, NowUnixMs());

        /// <summary>Seconds until the rewarded Amber drip re-arms, or 0 when it's ready now — the amber card counts down from this.</summary>
        public double AmberDripCooldownRemaining => Amber.AdDripCooldownRemainingMs(State, Data, NowUnixMs()) / 1000.0;

        /// <summary>Whether the rewarded time-skip can be taken right now (off cooldown) — "Hasten a while" gates on this and RewardedReady.</summary>
        public bool CanTimeSkipReward => Amber.CanRewardedTimeSkip(State, NowUnixMs());

        /// <summary>Seconds until the rewarded time-skip re-arms, or 0 when it's ready now — the camp strip counts down from this.</summary>
        public double TimeSkipRewardCooldownRemaining => Amber.RewardedTimeSkipCooldownRemainingMs(State, NowUnixMs()) / 1000.0;

        /// <summary>
        /// Whether a week has turned since the last Amber cache arrived — the
        /// card's "due" reading only. The cache is no longer a tap the game can
        /// grant itself: it is a Play Games Reward, set out by Play and received
        /// through <see cref="CheckPlayRewards"/> or on launch (design §11).
        /// </summary>
        public bool WeeklyCacheDue => Amber.WeeklyCacheDue(State, Data, NowUnixMs());

        /// <summary>Seconds until the weekly Amber cache is next due, or 0 when it's due now — the amber card counts down from this.</summary>
        public double WeeklyCacheCooldownRemaining => Amber.WeeklyCacheCooldownRemainingMs(State, Data, NowUnixMs()) / 1000.0;

        /// <summary>
        /// Buy a consumable Amber pack (design §10) and credit its pile on success.
        /// The result callback fires on the main thread like every IStore callback.
        /// </summary>
        public void PurchaseAmberPack(string productId, System.Action<StoreResult> onComplete)
        {
            Store.Purchase(productId, result =>
            {
                if (result == StoreResult.Purchased)
                {
                    var amount = Amber.GrantPack(State, AmberPackAmount(productId));
                    Telemetry.LogEvent("amber_pack", ("product", productId), ("amount", amount));
                    SaveNow();
                }

                onComplete?.Invoke(result);
            });
        }

        /// <summary>
        /// Credit a consumable pack that the store confirmed without a live
        /// callback — an interrupted purchase, consumed on this launch. The Play
        /// token is already spent, so this is the only place its pile is granted.
        /// </summary>
        private void OnConsumableRecovered(string productId)
        {
            if (State == null)
            {
                return;
            }

            var amount = Amber.GrantPack(State, AmberPackAmount(productId));
            if (amount > 0.0)
            {
                Telemetry.LogEvent("amber_pack_recovered", ("product", productId), ("amount", amount));
                SaveNow();
            }
        }

        /// <summary>The Amber pile a pack product grants, from the store catalogue (0 for an unknown id).</summary>
        public double AmberPackAmount(string productId)
        {
            var store = Data.economy?.store;
            if (store == null)
            {
                return 0.0;
            }

            if (productId == StoreProductIds.AmberPackSmall)
            {
                return store.amberPackSmall;
            }

            if (productId == StoreProductIds.AmberPackLarge)
            {
                return store.amberPackLarge;
            }

            return 0.0;
        }

    }
}
