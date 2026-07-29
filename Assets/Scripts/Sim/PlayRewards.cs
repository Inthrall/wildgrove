using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// Folds Play Games Reward entitlements into the sim (design §11), the way
    /// <see cref="KithPurchases"/> folds the bought ones. A reward is awarded by
    /// Google Play for a Quest or a Social Challenge and arrives through the
    /// ordinary purchase machinery, so the store is the source of truth and the
    /// saved flag only bridges sessions that start before billing resolves.
    ///
    /// Additive, never subtractive: an entitlement the store can't see right now
    /// — offline, mid-connect, a billing hiccup — must not take back a reward
    /// the save remembers. Idempotent, so it is safe on every launch, after the
    /// billing connection resolves, and after adopting a cloud save.
    /// </summary>
    public static class PlayRewards
    {
        /// <summary>
        /// Honour The Drover's Halter. Returns true when it landed just now
        /// (worth a save and worth telling the player about) and false when it
        /// was already hers or isn't owned.
        /// </summary>
        public static bool ApplyDroversHalter(GameState state, GameDataAsset data, bool owned)
        {
            if (state == null || !owned || state.droversHalterOwned)
            {
                return false;
            }

            state.droversHalterOwned = true;
            // The pony herself is derived from the entitlement, never stored —
            // this is the one call that stands her in her lane.
            Roster.SyncDroversHalter(state, data);
            return true;
        }

        /// <summary>
        /// Honour The Wayfarer's Plate: a plate drawn by another hand, arriving
        /// already recorded (design §11). Returns true when it landed just now
        /// and false when the book already held it or it isn't owned.
        ///
        /// The plate is written into the Folio's own record rather than being
        /// derived from the flag, which is why it needs no Migration handling —
        /// recorded plates cross the fold on their own, like every other page.
        /// </summary>
        public static bool ApplyWayfarersPlate(GameState state, GameDataAsset data, bool owned)
        {
            if (state == null || !owned || state.wayfarersPlateOwned)
            {
                return false;
            }

            state.wayfarersPlateOwned = true;
            Insects.Record(state, data, WayfarersPlateId);
            return true;
        }

        /// <summary>The awarded plate's insects.json id — the one plate no observation site can offer.</summary>
        public const string WayfarersPlateId = "wayfarers-plate";
    }
}
