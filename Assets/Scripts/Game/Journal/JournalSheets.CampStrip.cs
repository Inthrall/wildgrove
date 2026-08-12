using UnityEngine;
using UnityEngine.UI;
using Wildgrove.Game.Services;
using Wildgrove.Sim;
using static Wildgrove.Game.JournalTheme;
using static Wildgrove.Game.JournalWidgets;

namespace Wildgrove.Game
{
    /// <summary>
    /// The camp-actions strip at the head of the Camp page, and the two offers on
    /// it: the rewarded time-skip and the one-off remove-ads purchase.
    /// <para>
    /// Not a sheet at all, but it belongs with them because both buttons open
    /// something modal that this class owns, and because both have a state a
    /// live updater has to keep honest. The rule they share is that neither may
    /// ever accept a tap it will then refuse: the time-skip greys out and counts
    /// its cooldown down on its own face, and remove-ads stays hidden until
    /// billing has resolved who already owns it.
    /// </para>
    /// </summary>
    internal sealed partial class JournalSheets
    {
        // The time-skip ad credits this many hours of gathering.
        private const double TimeSkipHours = 2.0;
        private Button _timeSkipButton;
        private Button _removeAdsButton;
        private bool _removeAdsPending;

        /// <summary>
        /// The camp-actions strip at the head of the Camp page: the rewarded
        /// time-skip and the one-off remove-ads purchase.
        /// </summary>
        internal void BuildCampActions(Transform root)
        {
            var bar = MakePanel("CampActions", (RectTransform)root, CardPaper);
            var layout = bar.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.padding = new RectOffset(12, 12, 8, 8);
            layout.spacing = 10;
            var element = bar.AddComponent<LayoutElement>();
            element.flexibleHeight = 0;
            AddBorder(bar, Ink2);

            _timeSkipButton = Button(bar.transform, TimeSkipLabel(), 380, OnTimeSkip);
            KeyAction(_timeSkipButton);

            // Hidden until the store resolves ownership (RefreshCampActions is the
            // authority): shown only once billing is initialised and the player
            // doesn't already own Remove Ads. Built inactive so an owner never sees
            // it flash up before entitlements land.
            _removeAdsButton = Button(bar.transform, RemoveAdsLabel(), 300, OnRemoveAds);
            _removeAdsButton.gameObject.SetActive(false);

            RefreshCampActions();
        }

        /// <summary>
        /// Keep the camp-strip buttons current — the Camp page registers this
        /// as one of its live updaters, and it no-ops on every other tab, where
        /// the strip isn't built. The time-skip greys out and counts down while
        /// its reward cooldown holds, rather than accepting a tap only to
        /// refuse it with a note.
        /// </summary>
        internal void RefreshCampActions()
        {
            if (_timeSkipButton == null || _loop == null || _loop.State == null)
            {
                return;
            }

            // Off cooldown AND something to show. Gating on the cooldown alone
            // lit the button whenever the placement was empty, and ShowRewarded
            // reports that case through onClosed — which this caller doesn't
            // pass. So the tap did nothing and said nothing, the one thing the
            // camp strip must never do. (The drip row already asks both.)
            // RewardedReady is true outright for a Remove Ads owner, so the
            // stricter gate can't shut the button on the player who paid.
            var offCooldown = _loop.CanTimeSkipReward;
            var ready = offCooldown && _loop.RewardedReady(RewardedPlacement.TimeSkip);
            _timeSkipButton.interactable = ready;
            SetButtonTint(_timeSkipButton, ready, true);
            SetButtonLabel(_timeSkipButton, ready
                ? TimeSkipLabel()
                : offCooldown
                    // Waiting on fill, not on the clock — saying "ready in 0s"
                    // here would be a countdown that never ends.
                    ? "Pass the time (no ad to hand)"
                    : "Pass the time (ready in " + NumberFormat.Duration(_loop.TimeSkipRewardCooldownRemaining) + ")");

            if (_removeAdsButton != null)
            {
                // Show only once billing has resolved entitlements and the player
                // doesn't already own it. Owning Remove Ads — or a store that's
                // still connecting or unavailable — keeps it hidden.
                _removeAdsButton.gameObject.SetActive(_loop.Store.IsInitialised && !_loop.Store.RemoveAdsOwned);
                if (!_removeAdsPending)
                {
                    // The store's price lands after the catalogue fetch — keep
                    // the label current so the tap is never a surprise dialog.
                    SetButtonLabel(_removeAdsButton, RemoveAdsLabel());
                }
            }
        }

        /// <summary>"Remove ads · $x.xx" once the catalogue has priced it.</summary>
        private string RemoveAdsLabel()
        {
            var price = _loop.Store.PriceLabel(StoreProductIds.RemoveAds);
            return string.IsNullOrEmpty(price) ? "Remove ads" : "Remove ads · " + price;
        }

        /// <summary>"Pass the time, +2 hours" (+ the "watch a short ad" tail until Remove Ads is owned) — says what the reward gives.</summary>
        private string TimeSkipLabel()
        {
            var unit = System.Math.Abs(TimeSkipHours - 1.0) < 0.0001 ? "hour" : "hours";
            return "Pass the time, +" + TimeSkipHours.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                   + " " + unit + _loop.RewardedActionSuffix;
        }

        private void OnTimeSkip()
        {
            if (!_loop.CanTimeSkipReward)
            {
                // Still cooling down — refuse before showing an ad or granting.
                SetNote("the land has given its hours for now. let a while pass.");
                return;
            }

            _loop.WatchRewarded(RewardedPlacement.TimeSkip,
                () =>
                {
                    if (!_loop.CreditTimeSkip(TimeSkipHours))
                    {
                        return;
                    }

                    _loop.Telemetry.LogEvent("rewarded_ad", ("placement", "time_skip"));
                    SetNote(NumberFormat.Duration(TimeSkipHours * 3600.0)
                            + " pass in a breath, and the kith kept to the work.");
                    _dirty = true;
                });
        }

        private void OnRemoveAds()
        {
            if (_removeAdsButton != null)
            {
                // Don't let a second tap launch a second Play flow while the first
                // is open — Google rejects the re-buy with "you already own this
                // item". Re-enabled when the purchase doesn't go through, and
                // visibly pending meanwhile — a silent dead button reads broken.
                _removeAdsPending = true;
                _removeAdsButton.interactable = false;
                SetButtonTint(_removeAdsButton, false);
                SetButtonLabel(_removeAdsButton, "Opening the store…");
            }

            _loop.Store.Purchase(StoreProductIds.RemoveAds, result =>
            {
                // A deferred order is still outstanding with Play, so the flow
                // hasn't actually ended — staying "pending" keeps the live
                // updater from putting the price back on a button that must
                // stay down.
                _removeAdsPending = result == StoreResult.Deferred;
                switch (result)
                {
                    case StoreResult.Purchased:
                    case StoreResult.AlreadyOwned:
                        if (_removeAdsButton != null)
                        {
                            _removeAdsButton.gameObject.SetActive(false);
                        }

                        // A confirmed purchase doesn't re-raise the store's
                        // resolved event, so this is the only place that hears it
                        // — and until it is heard, the SDK already up from launch
                        // goes on preloading ads this player has just paid to be
                        // rid of.
                        _loop.NoteAdsEntitlement(true);
                        SetNote("The ads step aside. Thank you for keeping the grove.");
                        break;
                    case StoreResult.Cancelled:
                        // The player backed out of the Play sheet — the most
                        // ordinary outcome. Restore the button, say nothing.
                        RestoreRemoveAdsButton();
                        break;
                    case StoreResult.Failed:
                        RestoreRemoveAdsButton();
                        SetNote("That didn't go through. Nothing was charged.");
                        break;
                    case StoreResult.Unavailable:
                        // The store was never reached, so the button must come
                        // back — this is the one outcome a second press can fix.
                        RestoreRemoveAdsButton();
                        SetNote("The store couldn't be reached. Nothing was charged. Try again shortly.");
                        break;
                    case StoreResult.Deferred:
                        // Play holds the order until the payment clears. A second
                        // press can't help and Play would reject it, so the button
                        // says what it's waiting for and stays down; it comes back
                        // on the next open, and once the payment clears the row
                        // isn't built at all.
                        if (_removeAdsButton != null)
                        {
                            SetButtonLabel(_removeAdsButton, "Waiting on Play…");
                        }

                        SetNote("Play is still finishing that payment. The ads step aside when it clears, with nothing more to do.");
                        break;
                }
            });
        }

        private void RestoreRemoveAdsButton()
        {
            if (_removeAdsButton != null)
            {
                _removeAdsButton.interactable = true;
                SetButtonTint(_removeAdsButton, true);
                SetButtonLabel(_removeAdsButton, RemoveAdsLabel());
            }
        }
    }
}
