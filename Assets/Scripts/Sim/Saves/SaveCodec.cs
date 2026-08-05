using System.Collections.Generic;
using System.Linq;
using BreakInfinity;
using Newtonsoft.Json;
using Wildgrove.Data;

namespace Wildgrove.Sim.Saves
{
    /// <summary>
    /// Converts between the live <see cref="GameState"/> and the versioned
    /// <see cref="SaveData"/> wire shape (and its JSON). Restore rebuilds the
    /// node list from the current content data and overlays the saved per-node
    /// progress by node id, so a save taken on older data self-corrects:
    /// removed nodes drop away, new nodes appear with fresh defaults, and
    /// derived values (skill, yieldMultiplier) are recomputed rather than
    /// trusted. Unknown resource and upgrade ids are kept as-is — the run
    /// doesn't lose property because a data version renamed something, and the
    /// sim already tolerates unknown ids downstream.
    /// </summary>
    public static class SaveCodec
    {
        /// <summary>Bump when the wire shape changes, and add the matching migration step to <see cref="TryMigrate"/>.</summary>
        public const int CurrentVersion = 44;

        /// <summary>
        /// The oldest wire shape this build reads. Saves below it are refused
        /// whole rather than partly understood — <see cref="TryMigrate"/> has no
        /// rung to stand them on, and SaveFile sets them aside instead of
        /// deleting them.
        /// <para>
        /// It stays at 42 while the ladder grows above it: a v42 save climbs
        /// 42→43→44 and is read whole. It moves only when the bottom rungs are
        /// deliberately retired, which is a decision about whose saves stop
        /// working — never a side effect of adding a rung on top.
        /// </para>
        /// </summary>
        public const int EarliestReadableVersion = 42;

        public static SaveData Capture(GameState state, long savedAtUnixMs)
        {
            var save = new SaveData
            {
                version = CurrentVersion,
                savedAtUnixMs = savedAtUnixMs,
                verdurePoints = state.verdurePoints,
                renown = state.renown,
                migrationCount = state.migrationCount,
                almanacNodeIds = new List<string>(state.almanacNodeIds),
                fixedResources = new List<string>(state.fixedResources),
                wardenPostNodeId = state.wardenPostNodeId,
                wardenName = state.wardenName,
                amber = state.amber,
                foldedVersesSung = state.foldedVersesSung,
                purchasedKithSlots = state.purchasedKithSlots,
                starterBundleAmberGranted = state.starterBundleAmberGranted,
                droversHalterOwned = state.droversHalterOwned,
                wayfarersPlateOwned = state.wayfarersPlateOwned,
                weeklyCacheClaimedUnixMs = state.weeklyCacheClaimedUnixMs,
                adDripClaimedUnixMs = state.adDripClaimedUnixMs,
                timeSkipClaimedUnixMs = state.timeSkipClaimedUnixMs,
                timeSkipBudgetHours = state.timeSkipBudgetHours,
                timeSkipBudgetStampUnixMs = state.timeSkipBudgetStampUnixMs,
                clockHighWaterUnixMs = state.clockHighWaterUnixMs,
                playedMs = state.playedMs,
                deepAmberFound = state.deepAmberFound,
                deepAmberPityHours = state.deepAmberPityHours,
                seenWaystoneZoneIds = new List<string>(state.seenWaystoneZoneIds),
                finalWaystonesRead = state.finalWaystonesRead,
                finalWaystoneLastFold = state.finalWaystoneLastFold,
                speciesEverBefriended = new List<string>(state.speciesEverBefriended),
                stationsEverWorked = new List<string>(state.stationsEverWorked),
                nextFamiliarSeq = state.nextFamiliarSeq,
                deliveryProgress = state.deliveryProgress,
                rngState = state.rngState,
                purchasedUpgradeIds = new List<string>(state.purchasedUpgradeIds),
            };

            foreach (var familiar in state.roster)
            {
                save.roster.Add(new SavedFamiliar
                {
                    id = familiar.id,
                    name = familiar.name,
                    speciesId = familiar.speciesId,
                    xp = familiar.xp,
                    kinshipXp = familiar.kinshipXp,
                    stationId = familiar.stationId,
                    bonded = familiar.bonded,
                    bondId = familiar.bondId,
                    gifted = familiar.gifted,
                });
            }

            foreach (var pair in state.resources)
            {
                save.resources.Add(new SavedResource { id = pair.Key, amount = pair.Value });
            }

            foreach (var pair in state.decentResources)
            {
                save.decentResources.Add(new SavedResource { id = pair.Key, amount = pair.Value });
            }

            foreach (var pair in state.choiceResources)
            {
                save.choiceResources.Add(new SavedResource { id = pair.Key, amount = pair.Value });
            }

            foreach (var pair in state.lifetimeGathered)
            {
                save.lifetimeGathered.Add(new SavedResource { id = pair.Key, amount = pair.Value });
            }

            foreach (var pair in state.lifetimeCrafted)
            {
                save.lifetimeCrafted.Add(new SavedTally { id = pair.Key, count = pair.Value });
            }

            foreach (var pair in state.lifetimeChoice)
            {
                save.lifetimeChoice.Add(new SavedResource { id = pair.Key, amount = pair.Value });
            }

            foreach (var node in state.nodes)
            {
                save.nodes.Add(new SavedNode
                {
                    id = node.id,
                    masteryXp = node.masteryXp,
                    richnessLevel = node.richnessLevel,
                    tendBurstRemaining = node.tendBurstRemaining,
                    choiceBonusRemaining = node.choiceBonusRemaining,
                    basket = node.basket,
                });
            }

            foreach (var station in state.stations)
            {
                save.stations.Add(new SavedStation
                {
                    stationId = station.stationId,
                    recipeId = station.recipeId,
                    inFlight = station.inFlight,
                    progressSeconds = station.progressSeconds,
                });
            }

            foreach (var tincture in state.activeTinctures)
            {
                save.activeTinctures.Add(new SavedTincture
                {
                    tinctureId = tincture.tinctureId,
                    remainingSeconds = tincture.remainingSeconds,
                });
            }

            foreach (var pair in state.buildingLevels)
            {
                save.buildingLevels.Add(new SavedBuildingLevel { id = pair.Key, levels = pair.Value });
            }

            foreach (var pair in state.almanacLevels)
            {
                save.almanacLevels.Add(new SavedAlmanacLevel { id = pair.Key, levels = pair.Value });
            }

            foreach (var pair in state.skillXp)
            {
                save.skillXp.Add(new SavedSkillXp { id = pair.Key, xp = pair.Value });
            }

            foreach (var site in state.digSites)
            {
                save.digSites.Add(new SavedDigSite
                {
                    zoneId = site.zoneId,
                    pityHours = site.pityHours,
                });
            }

            foreach (var planter in state.builtPlanters)
            {
                save.builtPlanters.Add(new SavedPlanter { planterId = planter.planterId, targetId = planter.targetId });
            }

            foreach (var pair in state.insectSketches)
            {
                save.insectSketches.Add(new SavedInsectSketches { id = pair.Key, sketches = pair.Value });
            }

            foreach (var pair in state.deedCounts)
            {
                save.deedCounts.Add(new SavedDeedCount { id = pair.Key, count = pair.Value });
            }

            foreach (var pair in state.gearBySlot)
            {
                save.gear.Add(new SavedGearSlot { slot = pair.Key, gearId = pair.Value });
            }

            save.gearCrafted.AddRange(state.gearCrafted);

            foreach (var verse in state.verseProgress)
            {
                var savedVerse = new SavedVerseProgress { verseId = verse.verseId };
                foreach (var slot in verse.slots)
                {
                    savedVerse.slots.Add(new SavedSlotProgress
                    {
                        delivered = slot.delivered,
                        granted = slot.granted,
                        deedBaseline = slot.deedBaseline,
                        deedBaselineSet = slot.deedBaselineSet,
                    });
                }

                save.verseProgress.Add(savedVerse);
            }

            return save;
        }

        public static GameState Restore(SaveData save, GameDataAsset data)
        {
            // The baseline supplies the node set the current data says exists:
            // the fresh-run starting zone, extended with every zone the save had
            // opened. Regional seeds for zones the save KNEW are overwritten by
            // the saved values below; a zone that first materialises during this
            // restore (a data update added the unlock) keeps its seeds, matching
            // the live unlock path.
            //
            // Both halves of "had opened" must be in hand before the sync: the
            // purchases, and — since a sung verse opens the next trail along —
            // the verse progress and the fold count its gates are read against.
            // Sync late and those zones' nodes wouldn't exist when the saved
            // node rows are matched by id below, silently dropping a whole
            // zone's mastery, richness and baskets.
            var state = GameStateFactory.NewGame(data);
            state.purchasedUpgradeIds = save.purchasedUpgradeIds != null
                ? new List<string>(save.purchasedUpgradeIds)
                : new List<string>();
            state.migrationCount = save.migrationCount;
            RestoreVerseProgress(save, state);
            GameStateFactory.SyncUnlockedZones(state, data);

            // Replace the fresh-run seed kith with the saved roster of
            // individuals (design §4). A station pointing at a node the current
            // data no longer builds is cleared to resting, like the warden
            // post; an empty name gets a species-appropriate default.
            state.roster = new List<Familiar>();
            if (save.roster != null)
            {
                foreach (var saved in save.roster)
                {
                    if (saved == null)
                    {
                        continue;
                    }

                    state.roster.Add(new Familiar
                    {
                        id = saved.id,
                        name = saved.name,
                        speciesId = saved.speciesId,
                        xp = saved.xp,
                        kinshipXp = saved.kinshipXp,
                        stationId = StationValid(state, saved.stationId) ? saved.stationId : null,
                        bonded = saved.bonded,
                        bondId = saved.bondId,
                        gifted = saved.gifted,
                    });
                }
            }

            // A save always carries a seq ahead of every id; only fall back for
            // a malformed one. roster.Count + 1 could collide with an
            // existing "fam-N" if the roster has a gap, so derive the next seq
            // from the highest id actually present.
            state.nextFamiliarSeq = save.nextFamiliarSeq > 0 ? save.nextFamiliarSeq : NextSeqAfter(state.roster);

            foreach (var familiar in state.roster)
            {
                if (string.IsNullOrEmpty(familiar.name))
                {
                    familiar.name = Roster.SuggestName(state, data, familiar.speciesId);
                }
            }

            state.verdurePoints = save.verdurePoints;
            state.renown = save.renown;
            state.almanacNodeIds = save.almanacNodeIds != null
                ? new List<string>(save.almanacNodeIds)
                : new List<string>();
            state.fixedResources = save.fixedResources != null
                ? new List<string>(save.fixedResources)
                : new List<string>();
            // A post at a node the current data no longer builds (zone or
            // resource retuned) would strand the warden, matching no node at
            // all. Dangling post ids self-correct on restore like nodes do:
            // cleared, so the warden stands at camp until re-posted. The wander
            // post is always a valid warden post — it binds to no single node.
            state.wardenPostNodeId = save.wardenPostNodeId == Familiar.WanderStation
                                     || NodeExists(state, save.wardenPostNodeId)
                ? save.wardenPostNodeId
                : null;
            // A blank or whitespace name restores as no name at all rather than
            // as a warden called " " — the display falls back to "the warden",
            // which is the same thing an un-renamed run reads.
            state.wardenName = string.IsNullOrWhiteSpace(save.wardenName) ? null : save.wardenName.Trim();
            state.amber = save.amber;
            state.foldedVersesSung = save.foldedVersesSung > 0 ? save.foldedVersesSung : 0;
            state.purchasedKithSlots = save.purchasedKithSlots > 0 ? save.purchasedKithSlots : 0;
            state.starterBundleAmberGranted = save.starterBundleAmberGranted;
            state.droversHalterOwned = save.droversHalterOwned;
            state.wayfarersPlateOwned = save.wayfarersPlateOwned;
            state.weeklyCacheClaimedUnixMs = save.weeklyCacheClaimedUnixMs > 0 ? save.weeklyCacheClaimedUnixMs : 0L;
            state.adDripClaimedUnixMs = save.adDripClaimedUnixMs > 0 ? save.adDripClaimedUnixMs : 0L;
            state.timeSkipClaimedUnixMs = save.timeSkipClaimedUnixMs > 0 ? save.timeSkipClaimedUnixMs : 0L;
            state.timeSkipBudgetStampUnixMs = save.timeSkipBudgetStampUnixMs > 0 ? save.timeSkipBudgetStampUnixMs : 0L;
            state.timeSkipBudgetHours = state.timeSkipBudgetStampUnixMs > 0L && save.timeSkipBudgetHours > 0.0
                ? save.timeSkipBudgetHours
                : 0.0;
            // The ratchet can only ever move forward, so a save carrying a
            // negative or absent mark reads as "never told the time" rather
            // than as a mark in the past — a past mark would be no guard at all.
            state.clockHighWaterUnixMs = save.clockHighWaterUnixMs > 0L ? save.clockHighWaterUnixMs : 0L;
            state.playedMs = save.playedMs > 0 ? save.playedMs : 0L;
            state.deepAmberFound = save.deepAmberFound > 0 ? save.deepAmberFound : 0;
            state.deepAmberPityHours = save.deepAmberPityHours > 0.0 ? save.deepAmberPityHours : 0.0;
            state.seenWaystoneZoneIds = save.seenWaystoneZoneIds != null
                ? new List<string>(save.seenWaystoneZoneIds)
                : new List<string>();
            state.finalWaystonesRead = save.finalWaystonesRead > 0 ? save.finalWaystonesRead : 0;
            // -1 is "no stone yet", and it is the floor rather than 0: a save
            // written before the chain existed must not read as "one was taken
            // on fold 0", which would hold the first stone back until fold 1.
            state.finalWaystoneLastFold = save.finalWaystonesRead > 0 ? save.finalWaystoneLastFold : -1;
            state.speciesEverBefriended = save.speciesEverBefriended != null
                ? new List<string>(save.speciesEverBefriended)
                : new List<string>();
            state.stationsEverWorked = save.stationsEverWorked != null
                ? new List<string>(save.stationsEverWorked)
                : new List<string>();

            state.resources.Clear();
            if (save.resources != null)
            {
                foreach (var resource in save.resources)
                {
                    if (resource?.id != null)
                    {
                        state.resources[resource.id] = resource.amount;
                    }
                }
            }

            RestorePool(state.decentResources, save.decentResources);
            RestorePool(state.choiceResources, save.choiceResources);
            RestorePool(state.lifetimeGathered, save.lifetimeGathered);
            RestorePool(state.lifetimeChoice, save.lifetimeChoice);

            state.lifetimeCrafted.Clear();
            if (save.lifetimeCrafted != null)
            {
                foreach (var tally in save.lifetimeCrafted)
                {
                    if (tally?.id != null)
                    {
                        state.lifetimeCrafted[tally.id] = tally.count;
                    }
                }
            }

            // Zero is xorshift's fixed point, so a save carrying no rng state
            // keeps the fresh seed NewGame just rolled rather than pinning every
            // restored run to the same constant.
            if (save.rngState != 0UL)
            {
                state.rngState = save.rngState;
            }

            var savedById = new Dictionary<string, SavedNode>();
            if (save.nodes != null)
            {
                foreach (var node in save.nodes)
                {
                    if (node?.id != null)
                    {
                        savedById[node.id] = node;
                    }
                }
            }

            state.deliveryProgress = save.deliveryProgress;

            foreach (var node in state.nodes)
            {
                // A node the save predates keeps its fresh-run defaults.
                if (!savedById.TryGetValue(node.id, out var saved))
                {
                    continue;
                }

                node.masteryXp = saved.masteryXp;
                node.richnessLevel = saved.richnessLevel;
                node.tendBurstRemaining = saved.tendBurstRemaining;
                node.choiceBonusRemaining = saved.choiceBonusRemaining;
                node.basket = saved.basket;
            }

            state.stations.Clear();
            if (save.stations != null)
            {
                foreach (var station in save.stations)
                {
                    if (station?.stationId != null)
                    {
                        // A recipe id the current data doesn't know is kept —
                        // Crafting.Advance skips it harmlessly, same policy as
                        // unknown resource/upgrade ids.
                        state.stations.Add(new StationState
                        {
                            stationId = station.stationId,
                            recipeId = station.recipeId,
                            inFlight = station.inFlight,
                            progressSeconds = station.progressSeconds,
                        });
                    }
                }
            }

            state.activeTinctures.Clear();
            if (save.activeTinctures != null)
            {
                foreach (var tincture in save.activeTinctures)
                {
                    // A tincture id the current data doesn't know is kept —
                    // its effects sit inert (Tinctures.ActiveEffects skips it),
                    // same policy as unknown recipe/upgrade ids.
                    if (tincture?.tinctureId != null && tincture.remainingSeconds > 0.0)
                    {
                        state.activeTinctures.Add(new ActiveTincture
                        {
                            tinctureId = tincture.tinctureId,
                            remainingSeconds = tincture.remainingSeconds,
                        });
                    }
                }
            }

            state.buildingLevels.Clear();
            if (save.buildingLevels != null)
            {
                foreach (var building in save.buildingLevels)
                {
                    if (building?.id != null)
                    {
                        // Unknown line ids are kept, same policy as elsewhere.
                        state.buildingLevels[building.id] = building.levels;
                    }
                }
            }

            state.almanacLevels.Clear();
            if (save.almanacLevels != null)
            {
                foreach (var line in save.almanacLevels)
                {
                    if (line?.id != null)
                    {
                        // Unknown line ids are kept, same policy as elsewhere.
                        state.almanacLevels[line.id] = line.levels;
                    }
                }
            }

            state.skillXp.Clear();
            if (save.skillXp != null)
            {
                foreach (var skill in save.skillXp)
                {
                    if (skill?.id != null)
                    {
                        // Unknown skill ids are kept, same policy as elsewhere.
                        state.skillXp[skill.id] = skill.xp;
                    }
                }
            }

            // Dig-site identity was rebuilt by SyncUnlockedZones above (owned
            // unlockDigSite upgrades); overlay the saved diggers and pity by
            // zone. A saved site the data no longer grants simply drops away.
            if (save.digSites != null)
            {
                foreach (var savedSite in save.digSites)
                {
                    foreach (var site in state.digSites)
                    {
                        if (site.zoneId == savedSite?.zoneId)
                        {
                            site.pityHours = savedSite.pityHours;
                        }
                    }
                }
            }

            state.builtPlanters.Clear();
            if (save.builtPlanters != null)
            {
                foreach (var planter in save.builtPlanters)
                {
                    if (planter?.planterId != null && planter.targetId != null)
                    {
                        // Unknown planter ids or stale target ids are kept, same
                        // policy as elsewhere — Planters skips them harmlessly.
                        state.builtPlanters.Add(new BuiltPlanter { planterId = planter.planterId, targetId = planter.targetId });
                    }
                }
            }

            state.insectSketches.Clear();
            if (save.insectSketches != null)
            {
                foreach (var insect in save.insectSketches)
                {
                    if (insect?.id != null)
                    {
                        // Unknown insect ids are kept, same policy as elsewhere.
                        state.insectSketches[insect.id] = insect.sketches;
                    }
                }
            }

            state.deedCounts.Clear();
            if (save.deedCounts != null)
            {
                foreach (var deed in save.deedCounts)
                {
                    if (deed?.id != null)
                    {
                        state.deedCounts[deed.id] = deed.count;
                    }
                }
            }

            state.gearBySlot.Clear();
            if (save.gear != null)
            {
                foreach (var worn in save.gear)
                {
                    if (worn?.slot != null && worn.gearId != null)
                    {
                        // Unknown gear ids are kept, same policy as elsewhere —
                        // EquippedEffects skips what the data doesn't know.
                        state.gearBySlot[worn.slot] = worn.gearId;
                    }
                }
            }

            state.gearCrafted.Clear();
            if (save.gearCrafted != null)
            {
                foreach (var gearId in save.gearCrafted)
                {
                    if (gearId != null && !state.gearCrafted.Contains(gearId))
                    {
                        state.gearCrafted.Add(gearId);
                    }
                }
            }

            // A worn piece was certainly made, so the bag holds it whatever the
            // save says — this keeps a hand-edited or partially-migrated save
            // from showing a Craft button for something already on the warden.
            foreach (var pair in state.gearBySlot)
            {
                if (!state.gearCrafted.Contains(pair.Value))
                {
                    state.gearCrafted.Add(pair.Value);
                }
            }

            // The roster is a collection — one familiar per species, ever
            // (design §4). A save that carries duplicates keeps each species'
            // best — bonded first, then the deepest Kinship, then roster order —
            // and lets the rest slip back into the grass.
            var bySpecies = new HashSet<string>();
            var deduped = new List<Familiar>(state.roster.Count);
            foreach (var familiar in state.roster
                .OrderByDescending(f => f.bonded)
                .ThenByDescending(f => f.kinshipXp))
            {
                if (bySpecies.Add(familiar.speciesId ?? string.Empty))
                {
                    deduped.Add(familiar);
                }
            }

            if (deduped.Count < state.roster.Count)
            {
                state.roster = deduped;
            }

            // One body per post (§2): a save carrying several familiars on one
            // station keeps bonded first, then deepest Kinship; the rest go home
            // to camp.
            var taken = new HashSet<string>();
            foreach (var familiar in state.roster
                .OrderByDescending(f => f.bonded)
                .ThenByDescending(f => f.kinshipXp))
            {
                if (!familiar.IsResting && !taken.Add(familiar.stationId))
                {
                    familiar.stationId = null;
                }
            }

            // ...and the warden steps back to camp rather than crowd a node a
            // familiar holds (the familiar's post was the explicit choice).
            if (Stationing.OccupantOf(state, state.wardenPostNodeId) != null)
            {
                state.wardenPostNodeId = null;
            }

            // Slots cap who holds a post, not who belongs (§4 ladder). A save
            // from a wider ladder (or a retuned milestone table) can have more
            // familiars stationed than the slots it restores to — the extras
            // rest at camp, bonded and deepest-Kinship keeping their posts.
            var slots = Kith.Slots(state, data);
            if (Kith.Walking(state) > slots)
            {
                var keep = slots;
                foreach (var familiar in state.roster
                    .OrderByDescending(f => f.bonded)
                    .ThenByDescending(f => f.kinshipXp))
                {
                    // The pony is not on the ladder (§11): she holds no slot, so
                    // she must neither be rested by the trim nor spend one of
                    // the posts it is preserving.
                    if (familiar.IsResting || familiar.IsPony)
                    {
                        continue;
                    }

                    if (keep > 0)
                    {
                        keep--;
                    }
                    else
                    {
                        familiar.stationId = null;
                    }
                }
            }

            // Rungs the Almanac grants (starting tools, known trails) are
            // derived from node ownership, never trusted from the save — a
            // save older than the grant (a data retune, a node bought on a
            // build without the sync) picks them up here. Idempotent; a save
            // already carrying them is untouched.
            Almanac.SyncGrantedUpgrades(state, data);

            // A bond whose source (a kept Folio spread / Almanac node) is
            // already satisfied must have its companion honoured — bind or
            // materialise any the saved roster lacks (idempotent by bondId).
            Roster.SyncBonded(state, data);

            // The fell pony's presence and lane derive from the entitlement, not
            // from what the save happened to store (§11).
            Roster.SyncDroversHalter(state, data);

            // Last, so recorded insect plates' effects fold in with the
            // upgrades'. Settling also picks up any trail an Almanac grant just
            // added, and a verse that revealed since this save was taken (or a
            // deed slot an older build left unsynced) credits deeds already
            // done. The zones were synced up front for the node rows; this is
            // the idempotent second pass.
            Rite.Settle(state, data);
            return state;
        }

        /// <summary>
        /// The saved Rite progress, verbatim. Restored early — before the zones
        /// are synced — because a sung verse opens the next trail along, so the
        /// node set can't be built without it.
        /// </summary>
        private static void RestoreVerseProgress(SaveData save, GameState state)
        {
            state.verseProgress.Clear();
            if (save.verseProgress == null)
            {
                return;
            }

            foreach (var savedVerse in save.verseProgress)
            {
                if (savedVerse?.verseId == null)
                {
                    continue;
                }

                // Unknown verse ids are kept (a retuned rite may rename); slot
                // rows beyond the current data's slot count are harmless —
                // progress reads go by the data's indices.
                var verse = new VerseProgressState { verseId = savedVerse.verseId };
                if (savedVerse.slots != null)
                {
                    foreach (var slot in savedVerse.slots)
                    {
                        verse.slots.Add(new SlotProgressState
                        {
                            delivered = slot?.delivered ?? 0.0,
                            granted = slot?.granted ?? false,
                            deedBaseline = slot?.deedBaseline ?? 0.0,
                            deedBaselineSet = slot?.deedBaselineSet ?? false,
                        });
                    }
                }

                state.verseProgress.Add(verse);
            }
        }

        /// <summary>
        /// Whether a saved familiar's station id still resolves under the
        /// current data (else it's cleared to resting, so the familiar isn't
        /// stranded as a silent no-op). A "dig:" station fails here by design:
        /// the watch is not a post, so anyone still carrying one is rested.
        /// </summary>
        private static bool StationValid(GameState state, string stationId)
        {
            if (string.IsNullOrEmpty(stationId)
                || stationId == Familiar.WanderStation
                || stationId == Familiar.PonyStation)
            {
                return true;
            }

            return NodeExists(state, stationId);
        }

        /// <summary>
        /// The next roster-id sequence guaranteed not to collide with any id
        /// already in the roster: one past the highest "fam-N" present, or 1 on
        /// an empty/unparseable roster. Used only as a fallback when a save's
        /// stored seq is missing or malformed.
        /// </summary>
        private static int NextSeqAfter(List<Familiar> roster)
        {
            var highest = 0;
            if (roster != null)
            {
                foreach (var familiar in roster)
                {
                    if (familiar?.id != null
                        && familiar.id.StartsWith("fam-")
                        && int.TryParse(familiar.id.Substring(4), out var seq)
                        && seq > highest)
                    {
                        highest = seq;
                    }
                }
            }

            return highest + 1;
        }

        private static bool NodeExists(GameState state, string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId))
            {
                return false;
            }

            foreach (var node in state.nodes)
            {
                if (node.id == nodeId)
                {
                    return true;
                }
            }

            return false;
        }

        private static void RestorePool(Dictionary<string, BigDouble> pool, List<SavedResource> saved)
        {
            pool.Clear();
            if (saved == null)
            {
                return;
            }

            foreach (var resource in saved)
            {
                if (resource?.id != null)
                {
                    // Unknown resource ids are kept, same policy as elsewhere.
                    pool[resource.id] = resource.amount;
                }
            }
        }

        public static string ToJson(SaveData save)
        {
            return JsonConvert.SerializeObject(save, Formatting.Indented);
        }

        /// <summary>Parse a save file's JSON. Returns null when the text isn't a save (corrupt file) — the caller picks the fallback.</summary>
        public static SaveData FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                return JsonConvert.DeserializeObject<SaveData>(json);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>
        /// Bring an older save up to <see cref="CurrentVersion"/> in place.
        /// Returns false for a save this build must not read: one from a future
        /// build, and one older than <see cref="EarliestReadableVersion"/> —
        /// never guess at a shape this build doesn't know.
        /// </summary>
        public static bool TryMigrate(SaveData save)
        {
            if (save == null || save.version > CurrentVersion || save.version < EarliestReadableVersion)
            {
                return false;
            }

            while (save.version < CurrentVersion)
            {
                switch (save.version)
                {
                    // One case per rung, each bringing a save up exactly one
                    // version and letting the loop carry it the rest of the way.
                    // A step only ever fills in what its version predates; it
                    // never reaches for the current content data, because a
                    // migration has to hold for a save opened years later.
                    //
                    // Retiring the bottom of the ladder means raising
                    // EarliestReadableVersion to match, so a save that can no
                    // longer climb is refused outright rather than half-read.

                    case 42:
                        // v43 added the clock ratchet's high water mark. Left at
                        // zero on purpose — "this run has never been told the
                        // time", which is exactly true of a save written before
                        // the ratchet existed. Stamping it with anything else
                        // would either invent a mark (and freeze the run's
                        // cooldowns until real time passed it) or need the
                        // current clock, which a migration must never reach for.
                        save.version = 43;
                        break;

                    case 43:
                        // v44 added the warden's bought name. Left null: a save
                        // written before the rename existed belongs to a warden
                        // who was never named, and null is exactly how an
                        // un-renamed v44 run reads — every line falls back to
                        // "the warden", which is what that save already said.
                        save.version = 44;
                        break;

                    default:
                        // A gap in the ladder is a coding error — refuse rather
                        // than spin.
                        return false;
                }
            }

            return true;
        }
    }
}
