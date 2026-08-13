using UnityEngine;
using UnityEngine.UI;
using Wildgrove.Game.Services;
using static Wildgrove.Game.JournalTheme;
using static Wildgrove.Game.JournalWidgets;

namespace Wildgrove.Game
{
    /// <summary>
    /// The camp-actions strip at the head of the Camp page, and the offer on it:
    /// the one-off remove-ads purchase.
    /// <para>
    /// Not a sheet at all, but it belongs with them because the button opens
    /// something modal that this class owns, and because it has a state a live
    /// updater has to keep honest: it may never accept a tap it will then refuse,
    /// so it stays hidden until billing has resolved who already owns it.
    /// </para>
    /// <para>
    /// The rewarded time-skip stood beside it until 2026-08-13 and is now a cell
    /// on the events rail (see <see cref="EventRail"/> and
    /// <c>OpenTimeSkipSheet</c>) — a clock and a fingertip belong in the band,
    /// not in a 380-unit plate at the head of a page five viewports long. With it
    /// went the reason the strip was ever a row of two, so the strip goes with the
    /// button now: an owner of Remove Ads would otherwise open the Camp page onto
    /// an empty bordered panel.
    /// </para>
    /// </summary>
    internal sealed partial class JournalSheets
    {
        private GameObject _campActions;
        private Button _removeAdsButton;
        private bool _removeAdsPending;

        /// <summary>
        /// The camp-actions strip at the head of the Camp page: the one-off
        /// remove-ads purchase.
        /// </summary>
        internal void BuildCampActions(Transform root)
        {
            var bar = MakePanel("CampActions", (RectTransform)root, CardPaper);
            _campActions = bar;
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

            // Hidden until the store resolves ownership (RefreshCampActions is the
            // authority): shown only once billing is initialised and the player
            // doesn't already own Remove Ads. Built inactive so an owner never sees
            // it flash up before entitlements land.
            _removeAdsButton = Button(bar.transform, RemoveAdsLabel(), 300, OnRemoveAds);
            _removeAdsButton.gameObject.SetActive(false);

            RefreshCampActions();
        }

        /// <summary>
        /// Keep the camp strip current — the Camp page registers this as one of
        /// its live updaters, and it no-ops on every other tab, where the strip
        /// isn't built. The button may never accept a tap it will then refuse, so
        /// it is shown only once billing has resolved who owns what; the strip
        /// itself goes with it, since a bordered panel holding nothing reads as a
        /// card that failed to draw.
        /// </summary>
        internal void RefreshCampActions()
        {
            if (_removeAdsButton == null || _loop == null || _loop.State == null)
            {
                return;
            }

            // Owning Remove Ads — or a store that's still connecting or
            // unavailable — keeps it hidden.
            var offer = _loop.Store.IsInitialised && !_loop.Store.RemoveAdsOwned;
            _removeAdsButton.gameObject.SetActive(offer);
            if (_campActions != null)
            {
                _campActions.SetActive(offer);
            }

            if (!_removeAdsPending)
            {
                // The store's price lands after the catalogue fetch — keep the
                // label current so the tap is never a surprise dialog.
                SetButtonLabel(_removeAdsButton, RemoveAdsLabel());
            }
        }

        /// <summary>"Remove ads · $x.xx" once the catalogue has priced it.</summary>
        private string RemoveAdsLabel()
        {
            var price = _loop.Store.PriceLabel(StoreProductIds.RemoveAds);
            return string.IsNullOrEmpty(price) ? "Remove ads" : "Remove ads · " + price;
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
                        SetNote("The store couldn't be reached. Nothing was charged.");
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

                        SetNote("Not cleared yet. The ads step aside when Play is done.");
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
