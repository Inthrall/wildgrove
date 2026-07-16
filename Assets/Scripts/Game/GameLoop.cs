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

        /// <summary>
        /// Below this much credited absence the welcome-back sheet stays quiet
        /// (quick restarts and editor recompiles shouldn't greet the player).
        /// The welcome_back telemetry event uses the same bar so the metric
        /// counts what players actually saw.
        /// </summary>
        public const double WelcomeBackMinSeconds = 60.0;

        public GameDataAsset Data { get; private set; }
        public GameState State { get; private set; }

        /// <summary>
        /// What the last offline catch-up (cold launch or pause→resume)
        /// credited, held until the HUD collects it via
        /// <see cref="TakePendingOfflineSummary"/>. Null when there was
        /// nothing to credit (fresh run, or already shown).
        /// </summary>
        public OfflineSummary PendingOfflineSummary { get; private set; }

        /// <summary>The analytics/crash-reporting sink (Debug.Log until Firebase lands — see docs/todo.md).</summary>
        public ITelemetry Telemetry { get; private set; }

        private double _autosaveCountdown = AutosaveIntervalSeconds;
        private float _sessionStartRealtime;
        private bool _sessionOpen;
        private long _lastSavedUnixMs;

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
                // Resuming a still-alive process — Android's most common
                // return path. Update's next delta is clamped to a fraction of
                // a second, so the hours away must be credited here exactly
                // like a cold launch credits them (the pause branch saved on
                // the way out, making the last save the absence baseline).
                // Without this the next autosave would forfeit the absence.
                CreditAbsence((NowUnixMs() - _lastSavedUnixMs) / 1000.0);
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
                CreditAbsence((NowUnixMs() - save.savedAtUnixMs) / 1000.0);
            }
            else
            {
                State = GameStateFactory.NewGame(Data);
            }

            _lastSavedUnixMs = NowUnixMs();
            _autosaveCountdown = AutosaveIntervalSeconds;
            StartSession();
        }

        /// <summary>
        /// Run the offline catch-up for an absence and queue the welcome-back
        /// summary — shared by the cold-launch load and the pause→resume path.
        /// </summary>
        private void CreditAbsence(double awaySeconds)
        {
            var summary = Simulation.AdvanceOfflineWithSummary(State, Data, awaySeconds);

            // An unshown summary from a previous absence keeps priority — it
            // credited earlier, larger time; don't clobber it with a top-up.
            if (PendingOfflineSummary == null && summary.creditedSeconds > 0.0)
            {
                PendingOfflineSummary = summary;
            }

            if (summary.creditedSeconds >= WelcomeBackMinSeconds)
            {
                Telemetry.LogEvent("welcome_back",
                    ("away_sec", System.Math.Round(summary.realSeconds)),
                    ("credited_sec", System.Math.Round(summary.creditedSeconds)));
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

            _lastSavedUnixMs = NowUnixMs();
            SaveFile.Write(SaveCodec.Capture(State, _lastSavedUnixMs));
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

        /// <summary>Tend a node — a burst of extra yield for a short while (the tap-to-tend action, counted as a Rite deed).</summary>
        public void Tend(NodeState node)
        {
            Simulation.Tend(State, Data, node);
        }

        /// <summary>Size of the node's next gatherer gift, in units of its own resource — for the gift button's label and enabled state.</summary>
        public BigDouble NextGathererGiftCost(NodeState node)
        {
            return Economy.GathererGiftCost(node, Data.economy);
        }

        /// <summary>True when a gatherer gift can land on the node (stock covers it and the zone's flock is under cap).</summary>
        public bool CanGiftGatherer(NodeState node)
        {
            return Economy.CanGiftGatherer(State, Data, node);
        }

        /// <summary>Gift one gatherer onto the node, paying in its own resource. Returns false (no change) if stock is short or the flock is at cap.</summary>
        public bool GiftGatherer(NodeState node)
        {
            var cost = Economy.GathererGiftCost(node, Data.economy);
            if (!Economy.TryGiftGatherer(State, Data, node))
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

        /// <summary>True when the Feeder can be filled (stock covers the bundle and a carrier slot is free) — for the gift button's enabled state.</summary>
        public bool CanGiftCarrier()
        {
            return Economy.CanGiftCarrier(State, Data);
        }

        /// <summary>Camp-wide carrier slots (design §8, raised by the roosts line) — for the header readout.</summary>
        public int CarrierSlots()
        {
            return Buildings.CarrierSlots(State, Data);
        }

        /// <summary>Fill the Feeder to gift one carrier into the camp pool. Returns false (no change) if any of the bundle is short or the slots are full.</summary>
        public bool GiftCarrier()
        {
            var costEach = Economy.CarrierGiftCostEach(State, Data.economy);
            var bundleSize = Economy.FeederResources(State).Count;
            if (!Economy.TryGiftCarrier(State, Data))
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

        /// <summary>Per-resource size of the next digger's zone-bundle gift — for the gift button's label.</summary>
        public BigDouble NextDiggerGiftCostEach(DigSiteState site)
        {
            return Economy.DiggerGiftCostEach(site, Data.economy);
        }

        /// <summary>True when a digger gift can land (stock covers the zone bundle, flock under cap) — for the gift button's enabled state.</summary>
        public bool CanGiftDigger(DigSiteState site)
        {
            return Economy.CanGiftDigger(State, Data, site);
        }

        /// <summary>Gift one digger onto the zone's dig site. Returns false (no change) when stock is short or the flock is capped.</summary>
        public bool GiftDigger(DigSiteState site)
        {
            var costEach = Economy.DiggerGiftCostEach(site, Data.economy);
            if (!Economy.TryGiftDigger(State, Data, site))
            {
                return false;
            }

            Telemetry.LogEvent("familiar_gifted",
                ("role", "digger"),
                ("zone", site.zoneId),
                ("goods_cost_each", costEach.ToDouble()),
                ("site_diggers", site.familiarCount));
            return true;
        }

        /// <summary>True once the run owns the one-off upgrade.</summary>
        public bool IsUpgradePurchased(UpgradeData upgrade)
        {
            return State.HasUpgrade(upgrade.id);
        }

        /// <summary>The tool tier blocking this upgrade (design §3 zone gate), or null when none — for the buy button's "needs … tools" line.</summary>
        public string MissingToolTier(UpgradeData upgrade)
        {
            return Upgrades.MissingToolTier(State, Data, upgrade);
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

        /// <summary>A building line's current level (bought + owned milestone upgrades) — for the buildings row.</summary>
        public int BuildingLevel(BuildingData building)
        {
            return Buildings.TotalLevel(State, building);
        }

        /// <summary>Coin cost of the line's next level — for the build button's label.</summary>
        public BigDouble NextBuildingCost(BuildingData building)
        {
            return Buildings.NextLevelCost(State, Data, building);
        }

        /// <summary>Buy the line's next level. Returns false (no change) when the purse can't cover it.</summary>
        public bool BuyBuildingLevel(BuildingData building)
        {
            var cost = Buildings.NextLevelCost(State, Data, building);
            if (!Buildings.TryBuyLevel(State, Data, building))
            {
                return false;
            }

            Telemetry.LogEvent("building_level_bought",
                ("building", building.id),
                ("level", Buildings.TotalLevel(State, building)),
                ("coin_cost", cost.ToDouble()));
            return true;
        }

        /// <summary>The recipes the run can see — for the HUD's crafting section (level-locked ones included, as visible goals).</summary>
        public System.Collections.Generic.List<RecipeData> AvailableRecipes()
        {
            return Crafting.AvailableRecipes(State, Data);
        }

        /// <summary>True when the run's skill level covers the recipe's skillLevel — for the HUD's requirement hint.</summary>
        public bool IsRecipeLevelMet(RecipeData recipe)
        {
            return Crafting.SkillLevelMet(State, Data, recipe);
        }

        /// <summary>True when a station may actively work the recipe (every gate) — the assign/advance gate.</summary>
        public bool IsRecipeWorkable(RecipeData recipe)
        {
            return Crafting.IsWorkable(State, Data, recipe);
        }

        /// <summary>The skill's current level (1 when the XP system is unconfigured).</summary>
        public int SkillLevel(string skill)
        {
            return Skills.Level(State, Data, skill);
        }

        /// <summary>Fraction of the way to the skill's next level (0 once capped).</summary>
        public double SkillProgress(string skill)
        {
            return Skills.ProgressToNext(State, Data, skill);
        }

        /// <summary>The skills this run has opened, for the HUD's readout — stable alphabetical order.</summary>
        public System.Collections.Generic.List<string> UnlockedSkills()
        {
            var skills = new System.Collections.Generic.List<string>(Upgrades.UnlockedSkills(State, Data));
            skills.Sort(System.StringComparer.Ordinal);
            return skills;
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

            // Gated: Assign would refuse anyway, but bail here so the
            // telemetry can't report a craft that never started.
            if (!Crafting.IsWorkable(State, Data, recipe))
            {
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

        /// <summary>Offer camp stock into a resource slot of the Rite (design §7). Returns the units delivered.</summary>
        public BigDouble OfferResource(RiteVerseData verse, int slotIndex)
        {
            var wasComplete = Rite.IsVerseComplete(State, Data, verse);
            var given = Rite.DeliverResource(State, Data, verse, slotIndex);
            if (given > BigDouble.Zero)
            {
                Telemetry.LogEvent("offering_made",
                    ("verse", verse.id),
                    ("slot", slotIndex),
                    ("amount", given.ToDouble()));
                AfterOffering(verse, wasComplete);
            }

            return given;
        }

        /// <summary>Offer one Fine/Pristine specimen into a specimen slot. Returns true when one was given.</summary>
        public bool OfferSpecimen(RiteVerseData verse, int slotIndex)
        {
            var wasComplete = Rite.IsVerseComplete(State, Data, verse);
            var resourceId = Rite.DeliverSpecimen(State, Data, verse, slotIndex);
            if (resourceId == null)
            {
                return false;
            }

            Telemetry.LogEvent("offering_made",
                ("verse", verse.id),
                ("slot", slotIndex),
                ("specimen", resourceId));
            AfterOffering(verse, wasComplete);
            return true;
        }

        /// <summary>Offer one dug fossil fragment into a fragment slot — a real sacrifice from the dig chase. Returns true when one was given.</summary>
        public bool OfferFragment(RiteVerseData verse, int slotIndex)
        {
            var wasComplete = Rite.IsVerseComplete(State, Data, verse);
            var fossilId = Rite.DeliverFragment(State, Data, verse, slotIndex);
            if (fossilId == null)
            {
                return false;
            }

            Telemetry.LogEvent("offering_made",
                ("verse", verse.id),
                ("slot", slotIndex),
                ("fragment_from", fossilId));
            AfterOffering(verse, wasComplete);
            return true;
        }

        /// <summary>Craft and wear a piece of the kit (design §4) — spends its materials, fills its slot. Returns false when it can't be made.</summary>
        public bool CraftGear(GearData gear)
        {
            if (!Gear.TryCraft(State, Data, gear))
            {
                return false;
            }

            Telemetry.LogEvent("gear_crafted", ("gear", gear.id), ("slot", gear.slot));
            return true;
        }

        /// <summary>Verdure not yet allocated to an Almanac node — for the section header and buy buttons.</summary>
        public double AvailableVerdure()
        {
            return Almanac.AvailableVerdure(State, Data);
        }

        /// <summary>Buy an Almanac node with unallocated Verdure (permanent — survives Migration). Returns false when it can't be bought.</summary>
        public bool BuyAlmanacNode(AlmanacNodeData node)
        {
            if (!Almanac.TryBuy(State, Data, node))
            {
                return false;
            }

            Telemetry.LogEvent("almanac_node_bought",
                ("node", node.id),
                ("verdure_cost", node.costVerdure));
            return true;
        }

        /// <summary>True when the Rite has consented — the Migrate button's visibility.</summary>
        public bool CanMigrate()
        {
            return Migration.CanMigrate(State, Data);
        }

        /// <summary>The Verdure total a Migration right now would bank — for the confirm sheet.</summary>
        public double VerdureAfterMigration()
        {
            return Migration.VerdureAfterMigration(State, Data);
        }

        /// <summary>
        /// Fold the camp (design §7): swap in the next run's state, keeping
        /// the permanents, and save at once so the old run can't be resumed
        /// by force-closing. Returns false when the Rite hasn't consented.
        /// </summary>
        public bool Migrate()
        {
            var next = Migration.Migrate(State, Data);
            if (next == null)
            {
                return false;
            }

            Telemetry.LogEvent("migration_completed",
                ("number", next.migrationCount),
                ("verdure", next.verdurePoints),
                ("renown", State.renown.ToDouble()));
            State = next;
            SaveNow();
            return true;
        }

        private void AfterOffering(RiteVerseData verse, bool verseWasComplete)
        {
            if (!verseWasComplete && Rite.IsVerseComplete(State, Data, verse))
            {
                Telemetry.LogEvent("verse_completed", ("verse", verse.id));
                if (Rite.IsRiteComplete(State, Data))
                {
                    Telemetry.LogEvent("rite_completed", ("renown", State.renown.ToDouble()));
                }
            }
        }

        /// <summary>The windfall: sell one resource's Pristine specimens (design §5 — always an explicit act). Returns Coin gained.</summary>
        public BigDouble SellPristine(string resourceId)
        {
            var coin = Economy.SellPristine(State, Data, resourceId);
            if (coin > BigDouble.Zero)
            {
                Telemetry.LogEvent("pristine_sold", ("resource", resourceId), ("coin", coin.ToDouble()));
            }

            return coin;
        }
    }
}
