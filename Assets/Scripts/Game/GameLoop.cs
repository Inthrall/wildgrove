using System;
using BreakInfinity;
using UnityEngine;
using Wildgrove.Data;
using Wildgrove.Sim;
using Wildgrove.Sim.Saves;

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
        private const double AutosaveIntervalSeconds = 30.0;

        public GameDataAsset Data { get; private set; }
        public GameState State { get; private set; }

        /// <summary>
        /// What the load-time offline catch-up credited, held until the HUD
        /// collects it via <see cref="TakePendingOfflineSummary"/>. Null when
        /// the launch had nothing to credit (fresh run, or already shown).
        /// </summary>
        public OfflineSummary PendingOfflineSummary { get; private set; }

        private double _autosaveCountdown = AutosaveIntervalSeconds;

        private void Awake()
        {
            Initialise();
        }

        private void Update()
        {
            // A script recompile during Play reloads the app domain: non-serialised
            // fields reset and Awake does not re-run. Re-initialise rather than
            // ticking dead state — the run comes back from the last autosave.
            if (State == null)
            {
                Initialise();
            }

            Simulation.Advance(State, Data, Time.deltaTime);

            _autosaveCountdown -= Time.deltaTime;
            if (_autosaveCountdown <= 0.0)
            {
                _autosaveCountdown = AutosaveIntervalSeconds;
                SaveNow();
            }
        }

        private void OnApplicationPause(bool paused)
        {
            // Android's "the player switched away" signal — the last reliable
            // moment to persist before the OS may kill the process.
            if (paused)
            {
                SaveNow();
            }
        }

        private void OnApplicationQuit()
        {
            SaveNow();
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

            if (SaveFile.TryLoad(out var save))
            {
                State = SaveCodec.Restore(save, Data);
                var awaySeconds = (NowUnixMs() - save.savedAtUnixMs) / 1000.0;
                PendingOfflineSummary = Simulation.AdvanceOfflineWithSummary(State, Data, awaySeconds);
            }
            else
            {
                State = GameStateFactory.NewGame(Data);
            }

            _autosaveCountdown = AutosaveIntervalSeconds;
        }

        /// <summary>Persist the run now (also runs on the autosave interval, on pause, and on quit).</summary>
        public void SaveNow()
        {
            if (State == null)
            {
                return;
            }

            SaveFile.Write(SaveCodec.Capture(State, NowUnixMs()));
        }

        /// <summary>Collect (and clear) the load-time offline summary, so the welcome-back sheet shows once.</summary>
        public OfflineSummary TakePendingOfflineSummary()
        {
            var summary = PendingOfflineSummary;
            PendingOfflineSummary = null;
            return summary;
        }

        private static long NowUnixMs()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
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
