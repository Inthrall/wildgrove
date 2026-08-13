using UnityEngine;
using UnityEngine.UI;
using Wildgrove.Data;
using Wildgrove.Game.Services;
using Wildgrove.Sim;
using static Wildgrove.Game.JournalTheme;
using static Wildgrove.Game.JournalFormat;
using static Wildgrove.Game.JournalWidgets;

namespace Wildgrove.Game
{
    /// <summary>
    /// Everything the camp sells or gives: the amber card and its rows. The
    /// rewarded time-skip, the second queue, the weekly cache, the ad drip, and
    /// the amber packs themselves.
    /// <para>
    /// These rows are the game's money, so each one must read honestly before it
    /// reads generously: a row that cannot pay out yet says what it is waiting
    /// for rather than accepting a tap and refusing it.
    /// </para>
    /// </summary>
    internal sealed partial class CampPage
    {
        /// <summary>
        /// The Amber lines (design §10/§11): spend it on a full-rate time-skip,
        /// earn it from a rewarded ad or the weekly Play Games cache, or buy a
        /// pack. The whole card stays hidden while the amber economy is inert.
        /// </summary>
        private void BuildAmberCard()
        {
            var economy = _loop.Data.economy;
            if (economy?.amber == null)
            {
                return;
            }

            var card = Card("THE AMBER");

            if (Amber.Configured(economy))
            {
                BuildTimeSkipRow(card, economy);
            }

            if (economy.amber.secondQueueCostAmber > 0.0)
            {
                BuildSecondQueueRow(card, economy);
            }

            if (economy.amber.weeklyCacheAmber > 0.0)
            {
                BuildWeeklyCacheRow(card, economy);
            }

            if (economy.amber.adDripAmber > 0.0)
            {
                BuildAmberDripRow(card, economy);
            }

            if (economy.store != null)
            {
                BuildAmberPackRow(card, StoreProductIds.AmberPackSmall, economy.store.amberPackSmall);
                BuildAmberPackRow(card, StoreProductIds.AmberPackLarge, economy.store.amberPackLarge);
            }
        }

        private void BuildTimeSkipRow(RectTransform card, EconomyData economy)
        {
            var hours = economy.amber.timeSkipHours;
            var cost = economy.amber.timeSkipCostAmber;
            var row = Row(card);
            var label = MakeText(row.transform, string.Empty, 19, TextAnchor.MiddleLeft, Ink);
            FlexibleWidth(label.gameObject, 1f);
            var text = "hasten " + NumberFormat.Duration(hours * 3600.0) + " at full pace"
                       + SizeOpen(15) + "<color=" + OchreHex + ">  " + Mathf.FloorToInt((float)cost) + " amber</color></size>";
            label.text = text;
            var amber = Mathf.FloorToInt((float)cost);
            Button skip = null;
            skip = Button(row.transform, "Hasten", 170, () =>
            {
                if (!_loop.CanTimeSkip())
                {
                    return;
                }

                // Amber is premium and hard-won — never spend it on a stray tap.
                _hud.Sheets.OpenConfirmSheet(
                    "Spend " + amber + " amber",
                    "Hasten " + NumberFormat.Duration(hours * 3600.0) + " of gathering at full pace?",
                    "Spend " + amber + " amber",
                    () =>
                    {
                        if (_loop.TimeSkip() > 0.0)
                        {
                            SetNote("amber spent: " + NumberFormat.Duration(hours * 3600.0) + " of gathering, in a breath.");
                            _dirty = true;
                        }
                    });
            });

            // The skip budget is what the warden can wait out, so it's what
            // the line counts down (like the drip's cooldown); being short of
            // amber only greys the button.
            _liveUpdaters.Add(() =>
            {
                var ok = _loop.CanTimeSkip();
                label.text = text + (_loop.TimeSkipBudgetSpent()
                    ? WaitingTail(_loop.TimeSkipBudgetRemaining)
                    : string.Empty);
                skip.interactable = ok;
                SetButtonTint(skip, ok);
            });
        }

        /// <summary>
        /// The run's second craft-queue slot (design §9's sink slate): each
        /// station may hold two standing orders until the fold. One purchase,
        /// so one row — the station cards' own rule lines say the new count.
        /// </summary>
        private void BuildSecondQueueRow(RectTransform card, EconomyData economy)
        {
            var cost = Mathf.FloorToInt((float)economy.amber.secondQueueCostAmber);
            var row = Row(card);
            var label = MakeText(row.transform, string.Empty, 19, TextAnchor.MiddleLeft, Ink);
            FlexibleWidth(label.gameObject, 1f);
            var offer = "a second queue at every station, until the fold"
                        + SizeOpen(15) + "<color=" + OchreHex + ">  " + cost + " amber</color></size>";
            var held = "a second queue at every station"
                       + SizeOpen(15) + "<color=" + MossDeepHex + ">  held, this run</color></size>";
            Button buy = null;
            buy = Button(row.transform, "Buy", 170, () =>
            {
                if (!_loop.CanBuySecondQueue())
                {
                    return;
                }

                // Amber is premium and hard-won — never spend it on a stray tap.
                _hud.Sheets.OpenConfirmSheet(
                    "Spend " + cost + " amber",
                    "A second standing order at every station, until the camp folds?",
                    "Spend " + cost + " amber",
                    () =>
                    {
                        if (_loop.BuySecondQueue())
                        {
                            SetNote("amber spent: every station takes a second work.");
                            _dirty = true;
                        }
                    });
            });

            _liveUpdaters.Add(() =>
            {
                var owned = _loop.SecondQueueOwned;
                label.text = owned ? held : offer;
                buy.gameObject.SetActive(!owned);
                if (!owned)
                {
                    var ok = _loop.CanBuySecondQueue();
                    buy.interactable = ok;
                    SetButtonTint(buy, ok);
                }
            });
        }

        /// <summary>
        /// The weekly Amber cache (design §11) — a Play Games Reward, not a tap
        /// the game grants itself. Play sets one out at most weekly for a Social
        /// Challenge and delivers it through the store, so this row reports the
        /// week's state and offers a re-read for a player who redeemed a moment
        /// ago; the cache itself lands through the reward sheet either way.
        /// </summary>
        private void BuildWeeklyCacheRow(RectTransform card, EconomyData economy)
        {
            var row = Row(card);
            var label = MakeText(row.transform, string.Empty, 19, TextAnchor.MiddleLeft, Ink);
            FlexibleWidth(label.gameObject, 1f);
            var text = "the weekly amber cache"
                       + SizeOpen(15) + "<color=" + OchreHex + ">  +" + Mathf.FloorToInt((float)economy.amber.weeklyCacheAmber) + " amber</color></size>";
            var checking = false;
            Button look = null;
            look = Button(row.transform, "Look", 170, () =>
            {
                // Signed out, the button IS the sign-in — hiding the row hid the
                // cache from exactly the players it should convert, and a reward
                // can't be awarded to someone Play Games doesn't know at all
                // (the Standing card's pattern).
                if (!_loop.GameServices.IsSignedIn)
                {
                    Flash(look, "asking Play Games", true);
                    _loop.GameServices.SignInInteractive(signedIn => SetNote(signedIn
                        ? "signed in. Play Games can set the cache out."
                        : "Play Games didn't answer. the cache keeps."));
                    return;
                }

                if (checking)
                {
                    return;
                }

                checking = true;
                SetButtonLabel(look, "Looking…");
                _loop.CheckPlayRewards(found =>
                {
                    checking = false;
                    if (found == null)
                    {
                        // Play was never asked, so "nothing set out" would be a
                        // guess dressed as an answer — and the cache might well
                        // be waiting.
                        SetNote("Play couldn't be reached just now. the cache keeps.");
                        return;
                    }

                    if (found > 0)
                    {
                        // The reward sheet says what arrived and by whose hand —
                        // this line only stops the page reading dead.
                        _dirty = true;
                        return;
                    }

                    SetNote("nothing set out yet. the cache waits on a challenge.");
                });
            });

            _liveUpdaters.Add(() =>
            {
                var signedIn = _loop.GameServices.IsSignedIn;
                var due = _loop.WeeklyCacheDue;
                label.text = signedIn
                    ? text + (due
                        ? SizeOpen(15) + "<color=" + Ink2Hex + ">  set out by Play Games</color></size>"
                        : WaitingTail(_loop.WeeklyCacheNextDueIn))
                    : text + SizeOpen(15) + "<color=" + Ink2Hex + ">  Play Games isn't signed in</color></size>";
                if (!checking)
                {
                    SetButtonLabel(look, signedIn ? "Look" : "Sign in");
                }

                // Greys out once this week's cache has been taken, like the
                // drip's Watch — a live button that only ever answers "nothing
                // set out yet" teaches the player to stop reading the row.
                // Signed out it stays live, because there it is the sign-in.
                // Cost of greying: an off-cadence delivery (Play's week need not
                // start on the warden's Monday) waits for the next launch's
                // purchase fetch, which receives it unprompted — deferred,
                // never lost.
                var live = !checking && (!signedIn || due);
                look.interactable = live;
                SetButtonTint(look, live);
            });
        }

        private void BuildAmberDripRow(RectTransform card, EconomyData economy)
        {
            var row = Row(card);
            var label = MakeText(row.transform, string.Empty, 19, TextAnchor.MiddleLeft, Ink);
            FlexibleWidth(label.gameObject, 1f);
            var text = "a little amber" + _loop.RewardedActionSuffix
                       + SizeOpen(15) + "<color=" + OchreHex + ">  +" + Mathf.FloorToInt((float)economy.amber.adDripAmber) + " amber</color></size>";
            label.text = text;
            Button watch = null;
            watch = Button(row.transform, "Watch", 170, () =>
            {
                _loop.WatchRewarded(RewardedPlacement.AmberDrip, () =>
                {
                    var amount = _loop.GrantAmberDrip();
                    if (amount > 0.0)
                    {
                        Flash(watch, "+" + Mathf.FloorToInt((float)amount) + " amber", true);
                        SetNote("a little amber, for your patience.");
                        _dirty = true;
                    }
                });
            });

            // The drip's own cooldown is what the warden can wait out, so it's
            // what the line counts down; an unloaded ad only greys the button.
            _liveUpdaters.Add(() =>
            {
                var offCooldown = _loop.CanWatchAmberDrip;
                var ready = _loop.RewardedReady(RewardedPlacement.AmberDrip) && offCooldown;
                label.text = text + (offCooldown ? string.Empty : WaitingTail(_loop.AmberDripCooldownRemaining));
                watch.interactable = ready;
                SetButtonTint(watch, ready);
            });
        }

        /// <summary>The muted "ready in 6d 4h" tail an amber line wears while its cooldown holds.</summary>
        private static string WaitingTail(double seconds)
        {
            return SizeOpen(15) + "<color=" + Ink2Hex + ">  ready in " + NumberFormat.Countdown(seconds) + "</color></size>";
        }

        /// <summary>
        /// Which pack a line is offering. Both rows read "an amber pack" before,
        /// so the only thing telling the small pile from the large was the
        /// number beside it — two near-identical lines, one above the other.
        /// </summary>
        private static string AmberPackTitle(string productId)
        {
            if (productId == StoreProductIds.AmberPackLarge)
            {
                return "a hoard of amber";
            }

            return "a handful of amber";
        }

        private void BuildAmberPackRow(RectTransform card, string productId, double amount)
        {
            if (amount <= 0.0)
            {
                return;
            }

            var row = Row(card);
            var label = MakeText(row.transform, string.Empty, 19, TextAnchor.MiddleLeft, Ink);
            FlexibleWidth(label.gameObject, 1f);
            var baseText = AmberPackTitle(productId)
                           + SizeOpen(15) + "<color=" + OchreHex + ">  +" + Mathf.FloorToInt((float)amount) + " amber</color></size>";
            label.text = baseText;
            // The store's price lands after the (lazy) catalogue fetch — keep
            // the line current so the tap is never a surprise dialog.
            _liveUpdaters.Add(() => label.text = baseText + PriceTail(productId));
            Button buy = null;
            buy = Button(row.transform, "Buy", 170, () =>
            {
                _loop.PurchaseAmberPack(productId, result =>
                {
                    if (result == StoreResult.Purchased)
                    {
                        Flash(buy, "+" + Mathf.FloorToInt((float)amount) + " amber", true);
                        SetNote("the caravan trades in resin, amber for the coffer.");
                        _dirty = true;
                    }
                    else if (result == StoreResult.Failed)
                    {
                        SetNote("that didn't go through, nothing was charged.");
                    }
                    else if (result == StoreResult.Unavailable)
                    {
                        SetNote("the caravan couldn't be reached. nothing charged.");
                    }
                    else if (result == StoreResult.Deferred)
                    {
                        SetNote("not cleared yet. the amber comes when Play is done.");
                    }
                });
            });
        }
    }
}
