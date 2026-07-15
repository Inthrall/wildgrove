using BreakInfinity;
using UnityEngine;
using Wildgrove.Data;
using Wildgrove.Sim;

namespace Wildgrove.Game
{
    /// <summary>
    /// The scene-side driver that turns the pure simulation into a running game:
    /// it owns the content asset and the live <see cref="GameState"/>, advances
    /// the tick every frame, and exposes the core-loop player actions (gift a
    /// familiar, sell to the Provisioner) for the input/UI layer to call. All game logic
    /// lives in Wildgrove.Sim; this class is deliberately thin wiring.
    /// </summary>
    public sealed class GameLoop : MonoBehaviour
    {
        public GameDataAsset Data { get; private set; }
        public GameState State { get; private set; }

        private void Awake()
        {
            Initialise();
        }

        private void Update()
        {
            // A script recompile during Play reloads the app domain: non-serialised
            // fields reset and Awake does not re-run. Start a fresh run rather than
            // ticking dead state (no save system yet, so the run was lost anyway).
            if (State == null)
            {
                Initialise();
            }

            Simulation.Advance(State, Data, Time.deltaTime);
        }

        private void Initialise()
        {
            try
            {
                Data = GameDataAsset.LoadFromResources();
            }
            catch
            {
                // Missing/broken asset: fail loudly once, not once per frame.
                enabled = false;
                throw;
            }

            // Until the save system lands (versioned JSON, then cloud Saved Games
            // in Phase 5), every launch starts a fresh run. When it does, load the
            // persisted state here and feed the away-time to ApplyOfflineProgress.
            State = GameStateFactory.NewGame(Data);
        }

        /// <summary>
        /// Credit time the player spent away (capped and rate-scaled by the sim).
        /// Called by the load path once state persistence exists; returns the
        /// wall-clock seconds credited so a welcome-back summary can be shown.
        /// </summary>
        public double ApplyOfflineProgress(double realElapsedSeconds)
        {
            return Simulation.AdvanceOffline(State, Data, realElapsedSeconds);
        }

        /// <summary>Tend a node — a burst of extra yield for a short while (the tap-to-tend action).</summary>
        public void Tend(NodeState node)
        {
            Simulation.Tend(node, Data.economy);
        }

        /// <summary>Coin cost of the next familiar's gift — for the gift button's label and enabled state.</summary>
        public BigDouble NextFamiliarGiftCost()
        {
            return Economy.FamiliarGiftCost(State, Data.economy);
        }

        /// <summary>Gift one familiar onto the node. Returns false (no change) if the run can't afford it.</summary>
        public bool GiftFamiliar(NodeState node)
        {
            return Economy.TryGiftFamiliar(State, Data.economy, node);
        }

        /// <summary>True once the run owns the one-off upgrade.</summary>
        public bool IsUpgradePurchased(UpgradeData upgrade)
        {
            return State.HasUpgrade(upgrade.id);
        }

        /// <summary>True when the run holds the upgrade's Coin and material costs — for the buy button's enabled state.</summary>
        public bool CanAffordUpgrade(UpgradeData upgrade)
        {
            return Upgrades.CanAfford(State, upgrade);
        }

        /// <summary>Buy a one-off upgrade. Returns false (no change) when owned or unaffordable.</summary>
        public bool PurchaseUpgrade(UpgradeData upgrade)
        {
            return Upgrades.TryPurchase(State, Data, upgrade);
        }

        /// <summary>Sell the whole stock of one resource to the Provisioner. Returns Coin gained.</summary>
        public BigDouble SellResource(string resourceId)
        {
            return Economy.SellResource(State, Data, resourceId);
        }

        /// <summary>Sell every sellable resource on hand. Returns total Coin gained.</summary>
        public BigDouble SellAll()
        {
            return Economy.SellAll(State, Data);
        }
    }
}
