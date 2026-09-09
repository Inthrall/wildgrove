using System.Collections.Generic;
using UnityEngine;
using Wildgrove.Data;
using Wildgrove.Sim;

namespace Wildgrove.Game.Services
{
    /// <summary>
    /// What a delivered Play Games Reward landed, and the words the game owes
    /// the player for it. Google requires an in-game confirmation naming the
    /// item after any purchase granted outside the app, so the copy travels
    /// with the grant rather than being reconstructed at the sheet.
    /// </summary>
    public sealed class RewardGrant
    {
        /// <summary>The reward product id, for telemetry and the acknowledgement.</summary>
        public string rewardId;

        /// <summary>The item's name, stated plainly — the compliance line's subject.</summary>
        public string itemName;

        /// <summary>The plain statement that it was received, and from where.</summary>
        public string statement;

        /// <summary>One line in the journal's own voice, under the plain one.</summary>
        public string flavour;
    }

    /// <summary>
    /// Maps a delivered Play Games Reward onto the run (design §11).
    ///
    /// The delivery contract, in order, from Google's out-of-app purchase flow:
    /// grant → tell the player → acknowledge. Acknowledging first would mean a
    /// crash between the two loses the reward outright, while an unacknowledged
    /// one is simply refunded by Play after three days and can be offered again.
    /// So this runs before the store confirms the order, and an id it can't
    /// grant returns null — which leaves the order unacknowledged on purpose.
    /// </summary>
    public static class RewardGrants
    {
        /// <summary>
        /// Apply a delivered reward. Returns what landed, or null when the id
        /// isn't a reward this build can grant (leave it unacknowledged) or the
        /// grant would be empty (an unconfigured cache mints nothing).
        /// <para>
        /// A grant that lands also writes down that the player is owed its
        /// confirmation, in the run itself: the credit is banked here and the
        /// sheet cannot be shown from here, so without the record a process that
        /// dies in between leaves them paid and never told.
        /// </para>
        /// </summary>
        public static RewardGrant Apply(GameState state, GameDataAsset data, string productId, long nowUnixMs)
        {
            if (state == null || productId == null)
            {
                return null;
            }

            if (productId == RewardProductIds.DroversHalter)
            {
                // Already hers — a re-delivered entitlement (a reinstall, a
                // second device) is still a legitimate arrival to acknowledge,
                // it just has nothing new to say.
                if (!PlayRewards.ApplyDroversHalter(state, data, true) && !state.droversHalterOwned)
                {
                    return null;
                }

                return Owe(state, data, productId);
            }

            if (productId == RewardProductIds.WayfarersPlate)
            {
                // Already in the book — same as the Halter: a re-delivery is a
                // legitimate arrival to acknowledge, it just says nothing new.
                if (!PlayRewards.ApplyWayfarersPlate(state, data, true) && !state.wayfarersPlateOwned)
                {
                    return null;
                }

                return Owe(state, data, productId);
            }

            if (productId == RewardProductIds.WeeklyAmberCache)
            {
                if (Amber.ReceiveWeeklyCache(state, data, nowUnixMs) <= 0.0)
                {
                    return null;
                }

                return Owe(state, data, productId);
            }

            return null;
        }

        /// <summary>
        /// The words a delivered reward owes the player, WITHOUT granting it a
        /// second time — for a confirmation the save carried over from a session
        /// that never got to show its sheet. Null for an id this build cannot
        /// name, which is how a retired reward's debt is dropped rather than
        /// drawn as a blank sheet.
        /// <para>
        /// The copy is authored here rather than persisted beside the debt, so a
        /// telling that waited a build over reads in this build's words and no
        /// save carries a line of prose it can never be rid of.
        /// </para>
        /// </summary>
        public static RewardGrant Words(GameDataAsset data, string productId)
        {
            if (productId == RewardProductIds.DroversHalter)
            {
                return new RewardGrant
                {
                    rewardId = productId,
                    itemName = "The Drover's Halter",
                    statement = "Received from Play Games: The Drover's Halter.",
                    flavour = "a fell pony stands at the head of a second lane. she will not be led elsewhere."
                };
            }

            if (productId == RewardProductIds.WayfarersPlate)
            {
                return new RewardGrant
                {
                    rewardId = productId,
                    itemName = "The Wayfarer's Plate",
                    statement = "Received from Play Games: The Wayfarer's Plate.",
                    flavour = "a page in another hand, steadier than yours and long gone. the book has room for it."
                };
            }

            if (productId == RewardProductIds.WeeklyAmberCache)
            {
                var amount = data?.economy?.amber != null ? data.economy.amber.weeklyCacheAmber : 0.0;
                var pile = Mathf.FloorToInt((float)amount);
                return new RewardGrant
                {
                    rewardId = productId,
                    itemName = "the weekly amber cache",
                    statement = "Received from Play Games: the weekly amber cache, +" + pile + " amber.",
                    flavour = "a little resin, freely given."
                };
            }

            return null;
        }

        /// <summary>
        /// Write the debt down and hand back the words: the run remembers it
        /// owes a telling until the player acknowledges one, and the caller gets
        /// the sheet's copy.
        /// </summary>
        private static RewardGrant Owe(GameState state, GameDataAsset data, string productId)
        {
            if (state.rewardsOwedTelling == null)
            {
                state.rewardsOwedTelling = new List<string>();
            }

            state.rewardsOwedTelling.Add(productId);
            return Words(data, productId);
        }

        /// <summary>
        /// Strike one telling off the debt, the player having acknowledged it.
        /// The first matching id only: two deliveries of the same reward are two
        /// tellings, and answering one does not answer the other.
        /// </summary>
        public static void TellingDone(GameState state, string productId)
        {
            state?.rewardsOwedTelling?.Remove(productId);
        }
    }
}
