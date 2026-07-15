using System;
using BreakInfinity;
using UnityEngine;
using Wildgrove.Data;
using Wildgrove.Game.Telemetry;
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

        /// <summary>The analytics/crash-reporting sink (Debug.Log until Firebase lands — see docs/todo.md).</summary>
        public ITelemetry Telemetry { get; private set; }

        private double _autosaveCountdown = AutosaveIntervalSeconds;
        private float _sessionStartRealtime;
        private bool _sessionOpen;

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
            // moment to persist before the OS may kill the process. It's also
            // the mobile session boundary: end on pause, start on resume.
            if (paused)
            {
                SaveNow();
                EndSession();
            }
            else if (!_sessionOpen && State != null)
            {
                StartSession();
            }
        }

        private void OnApplicationQuit()
        {
            SaveNow();
            EndSession();
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

            Telemetry = new UnityLogTelemetry();

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
            StartSession();

            var offline = PendingOfflineSummary;
            if (offline != null && offline.creditedSeconds > 0.0)
            {
                Telemetry.LogEvent("welcome_back",
                    ("away_sec", System.Math.Round(offline.realSeconds)),
                    ("credited_sec", System.Math.Round(offline.creditedSeconds)));
            }
        }

        private void StartSession()
        {
            _sessionStartRealtime = Time.realtimeSinceStartup;
            _sessionOpen = true;
            Telemetry.LogEvent("session_start");
        }

        private void EndSession()
        {
            // Guarded so quit-after-pause (or a pause before Awake) can't
            // double-count; the design's gate metric is session length.
            if (!_sessionOpen)
            {
                return;
            }

            _sessionOpen = false;
            Telemetry.LogEvent("session_end",
                ("length_sec", System.Math.Round(Time.realtimeSinceStartup - _sessionStartRealtime)));
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

        /// <summary>Size of the node's next gatherer gift, in units of its own resource — for the gift button's label and enabled state.</summary>
        public BigDouble NextGathererGiftCost(NodeState node)
        {
            return Economy.GathererGiftCost(node, Data.economy);
        }

        /// <summary>Gift one gatherer onto the node, paying in its own resource. Returns false (no change) if camp stock can't cover it.</summary>
        public bool GiftGatherer(NodeState node)
        {
            var cost = Economy.GathererGiftCost(node, Data.economy);
            if (!Economy.TryGiftGatherer(State, Data.economy, node))
            {
                return false;
            }

            Telemetry.LogEvent("familiar_gifted",
                ("role", "gatherer"),
                ("node", node.id),
                ("goods_cost", cost.ToDouble()),
                ("total_familiars", State.TotalFamiliars()));
            return true;
        }

        /// <summary>Per-resource size of the next carrier's Feeder bundle — for the gift button's label.</summary>
        public BigDouble NextCarrierGiftCostEach()
        {
            return Economy.CarrierGiftCostEach(State, Data.economy);
        }

        /// <summary>True when camp stock covers the whole Feeder bundle — for the gift button's enabled state.</summary>
        public bool CanGiftCarrier()
        {
            return Economy.CanGiftCarrier(State, Data.economy);
        }

        /// <summary>Fill the Feeder to gift one carrier into the camp pool. Returns false (no change) if any of the bundle is short.</summary>
        public bool GiftCarrier()
        {
            var costEach = Economy.CarrierGiftCostEach(State, Data.economy);
            var bundleSize = Economy.FeederResources(State).Count;
            if (!Economy.TryGiftCarrier(State, Data.economy))
            {
                return false;
            }

            Telemetry.LogEvent("familiar_gifted",
                ("role", "carrier"),
                ("goods_cost_each", costEach.ToDouble()),
                ("bundle_resources", bundleSize),
                ("total_carriers", State.carrierCount));
            return true;
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
            if (!Upgrades.TryPurchase(State, Data, upgrade))
            {
                return false;
            }

            Telemetry.LogEvent("upgrade_purchased",
                ("upgrade_id", upgrade.id),
                ("coin_cost", upgrade.costCoin.ToDouble()));
            return true;
        }

        /// <summary>The recipes the run can craft right now — for the HUD's crafting section.</summary>
        public System.Collections.Generic.List<RecipeData> AvailableRecipes()
        {
            return Crafting.AvailableRecipes(State, Data);
        }

        /// <summary>True while this recipe's station is assigned to it.</summary>
        public bool IsCrafting(RecipeData recipe)
        {
            return Crafting.ActiveStationFor(State, recipe) != null;
        }

        /// <summary>True when camp stock covers one batch of the recipe's inputs.</summary>
        public bool CanCraft(RecipeData recipe)
        {
            return Crafting.HasInputs(State, recipe);
        }

        /// <summary>The in-flight batch's fraction complete (0 when idle or stalled).</summary>
        public double CraftProgress(RecipeData recipe)
        {
            return Crafting.Progress(State, Data, recipe);
        }

        /// <summary>
        /// Start the recipe on its station (displacing whatever it was working,
        /// in-flight inputs refunded), or stop it if it's already running.
        /// </summary>
        public void ToggleCraft(RecipeData recipe)
        {
            if (IsCrafting(recipe))
            {
                Crafting.Stop(State, Data, recipe);
                return;
            }

            Crafting.Assign(State, Data, recipe);
            Telemetry.LogEvent("craft_started",
                ("recipe", recipe.id),
                ("station", recipe.station));
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
