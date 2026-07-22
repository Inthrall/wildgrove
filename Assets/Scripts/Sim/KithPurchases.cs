using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// Folds store entitlements into the sim (design §4 ladder): the starter
    /// bundle and the plain slot product each open one kith slot, and the
    /// bundle pays its one-time Amber pile. The store is the source of truth —
    /// call after the billing connection resolves and after every purchase;
    /// the saved values only bridge sessions that start before billing does.
    /// Idempotent: entitlements re-resolve on every device, the Amber arrives
    /// once, and slots never go backwards (a billing hiccup must not shrink a
    /// ladder the save remembers).
    /// </summary>
    public static class KithPurchases
    {
        /// <summary>Apply what the store says is owned. Returns true when anything changed (worth a save).</summary>
        public static bool Apply(GameState state, GameDataAsset data, bool starterBundleOwned, bool kithSlotOwned)
        {
            if (state == null)
            {
                return false;
            }

            var changed = false;

            var slots = (starterBundleOwned ? 1 : 0) + (kithSlotOwned ? 1 : 0);
            if (slots > state.purchasedKithSlots)
            {
                state.purchasedKithSlots = slots;
                changed = true;
            }

            if (starterBundleOwned && !state.starterBundleAmberGranted)
            {
                var amber = data?.economy?.store != null ? data.economy.store.starterBundleAmber : 0.0;
                if (amber > 0.0)
                {
                    state.amber += amber;
                }

                // Flag even when unconfigured — the entitlement was honoured;
                // a later config change must not mint a retroactive pile.
                state.starterBundleAmberGranted = true;
                changed = true;
            }

            return changed;
        }
    }
}
