using BreakInfinity;
using UnityEngine;
using Wildgrove.Data;
using Wildgrove.Sim;

namespace Wildgrove.Game
{
    /// <summary>
    /// The scene-side driver that turns the pure simulation into a running game:
    /// it owns the content asset and the live <see cref="GameState"/>, advances
    /// the tick every frame, and exposes the core-loop player actions (hire crew,
    /// sell to the Provisioner) for the input/UI layer to call. All game logic
    /// lives in Wildgrove.Sim; this class is deliberately thin wiring.
    /// </summary>
    public sealed class GameLoop : MonoBehaviour
    {
        public GameDataAsset Data { get; private set; }
        public GameState State { get; private set; }

        private void Awake()
        {
            Data = GameDataAsset.LoadFromResources();

            // Until the save system lands (versioned JSON, then cloud Saved Games
            // in Phase 5), every launch starts a fresh run. When it does, load the
            // persisted state here and feed the away-time to ApplyOfflineProgress.
            State = GameStateFactory.NewGame(Data);
        }

        private void Update()
        {
            Simulation.Advance(State, Data, Time.deltaTime);
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

        /// <summary>Coin cost of the next crew hire — for the hire button's label and enabled state.</summary>
        public BigDouble NextCrewHireCost()
        {
            return Economy.CrewHireCost(State, Data.economy);
        }

        /// <summary>Hire one crew onto the node. Returns false (no change) if the run can't afford it.</summary>
        public bool HireCrew(NodeState node)
        {
            return Economy.TryHireCrew(State, Data.economy, node);
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
