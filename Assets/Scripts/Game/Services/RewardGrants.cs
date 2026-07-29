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
                // Already in the book — same as the Halter: a re-delivery is a
                // legitimate arrival to acknowledge, it just says nothing new.
                if (!PlayRewards.ApplyWayfarersPlate(state, data, true) && !state.wayfarersPlateOwned)
                {
                    return null;
                }

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
                var amount = Amber.ReceiveWeeklyCache(state, data, nowUnixMs);
                if (amount <= 0.0)
                {
                    return null;
                }

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
    }
}
