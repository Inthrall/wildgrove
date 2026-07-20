using System.Collections.Generic;
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
        public const int CurrentVersion = 19;

        public static SaveData Capture(GameState state, long savedAtUnixMs)
        {
            var save = new SaveData
            {
                version = CurrentVersion,
                savedAtUnixMs = savedAtUnixMs,
                coin = state.coin,
                verdurePoints = state.verdurePoints,
                renown = state.renown,
                migrationCount = state.migrationCount,
                almanacNodeIds = new List<string>(state.almanacNodeIds),
                donatedResources = new List<string>(state.donatedResources),
                wardenPostNodeId = state.wardenPostNodeId,
                amber = state.amber,
                seenWaystoneZoneIds = new List<string>(state.seenWaystoneZoneIds),
                carrierCount = state.carrierCount,
                haulTripProgress = state.haulTripProgress,
                rngState = state.rngState,
                purchasedUpgradeIds = new List<string>(state.purchasedUpgradeIds),
            };

            foreach (var pair in state.resources)
            {
                save.resources.Add(new SavedResource { id = pair.Key, amount = pair.Value });
            }

            foreach (var pair in state.fineResources)
            {
                save.fineResources.Add(new SavedResource { id = pair.Key, amount = pair.Value });
            }

            foreach (var pair in state.pristineResources)
            {
                save.pristineResources.Add(new SavedResource { id = pair.Key, amount = pair.Value });
            }

            foreach (var pair in state.lifetimeGathered)
            {
                save.lifetimeGathered.Add(new SavedResource { id = pair.Key, amount = pair.Value });
            }

            foreach (var pair in state.lifetimeCrafted)
            {
                save.lifetimeCrafted.Add(new SavedTally { id = pair.Key, count = pair.Value });
            }

            foreach (var pair in state.lifetimePristine)
            {
                save.lifetimePristine.Add(new SavedResource { id = pair.Key, amount = pair.Value });
            }

            foreach (var node in state.nodes)
            {
                save.nodes.Add(new SavedNode
                {
                    id = node.id,
                    familiarCount = node.familiarCount,
                    masteryXp = node.masteryXp,
                    tendBurstRemaining = node.tendBurstRemaining,
                    pristineBonusRemaining = node.pristineBonusRemaining,
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

            foreach (var pair in state.buildingLevels)
            {
                save.buildingLevels.Add(new SavedBuildingLevel { id = pair.Key, levels = pair.Value });
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
                    familiarCount = site.familiarCount,
                    pityHours = site.pityHours,
                });
            }

            foreach (var pair in state.fossilFragments)
            {
                save.fossilFragments.Add(new SavedFossilFragments { id = pair.Key, fragments = pair.Value });
            }

            foreach (var pair in state.deedCounts)
            {
                save.deedCounts.Add(new SavedDeedCount { id = pair.Key, count = pair.Value });
            }

            foreach (var pair in state.gearBySlot)
            {
                save.gear.Add(new SavedGearSlot { slot = pair.Key, gearId = pair.Value });
            }

            foreach (var verse in state.verseProgress)
            {
                var savedVerse = new SavedVerseProgress { verseId = verse.verseId };
                foreach (var slot in verse.slots)
                {
                    savedVerse.slots.Add(new SavedSlotProgress { delivered = slot.delivered, granted = slot.granted });
                }

                save.verseProgress.Add(savedVerse);
            }

            return save;
        }

        public static GameState Restore(SaveData save, GameDataAsset data)
        {
            // The baseline supplies the node set the current data says exists:
            // the fresh-run starting zone, extended with every zone the saved
            // purchases had unlocked. Regional seeds for zones the save KNEW
            // are overwritten by the saved values below; a zone that first
            // materialises during this restore (a data update added the
            // unlock) keeps its seeds, matching the live unlock path.
            var state = GameStateFactory.NewGame(data);
            state.purchasedUpgradeIds = save.purchasedUpgradeIds != null
                ? new List<string>(save.purchasedUpgradeIds)
                : new List<string>();
            GameStateFactory.SyncUnlockedZones(state, data);

            state.coin = save.coin;
            state.verdurePoints = save.verdurePoints;
            state.renown = save.renown;
            state.migrationCount = save.migrationCount;
            state.almanacNodeIds = save.almanacNodeIds != null
                ? new List<string>(save.almanacNodeIds)
                : new List<string>();
            state.donatedResources = save.donatedResources != null
                ? new List<string>(save.donatedResources)
                : new List<string>();
            // A post at a node the current data no longer builds (zone or
            // resource retuned) would strand the warden — and the bonded
            // gatherers who share the post — matching no node at all. Dangling
            // post ids self-correct on restore like nodes do: cleared, so the
            // first-node fallback takes over.
            state.wardenPostNodeId = NodeExists(state, save.wardenPostNodeId)
                ? save.wardenPostNodeId
                : null;
            state.amber = save.amber;
            state.seenWaystoneZoneIds = save.seenWaystoneZoneIds != null
                ? new List<string>(save.seenWaystoneZoneIds)
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

            RestorePool(state.fineResources, save.fineResources);
            RestorePool(state.pristineResources, save.pristineResources);
            RestorePool(state.lifetimeGathered, save.lifetimeGathered);
            RestorePool(state.lifetimePristine, save.lifetimePristine);

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

            // A pre-v8 save carries no rng state (0) — keep the fresh seed the
            // baseline NewGame just rolled rather than pinning every migrated
            // run to the same constant.
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

            // Zones the save knew contributed their seed carrier to the saved
            // carrierCount already; zones materialising for the first time in
            // this restore keep the +1 the live unlock path would have granted.
            var newZones = new HashSet<string>();
            var savedZones = new HashSet<string>();
            foreach (var node in state.nodes)
            {
                (savedById.ContainsKey(node.id) ? savedZones : newZones).Add(node.zoneId);
            }

            newZones.ExceptWith(savedZones);
            state.carrierCount = save.carrierCount + newZones.Count;
            state.haulTripProgress = save.haulTripProgress;

            foreach (var node in state.nodes)
            {
                // A node the save predates keeps its fresh-run defaults
                // (including the first-node starter familiar).
                if (!savedById.TryGetValue(node.id, out var saved))
                {
                    continue;
                }

                node.familiarCount = saved.familiarCount;
                node.masteryXp = saved.masteryXp;
                node.tendBurstRemaining = saved.tendBurstRemaining;
                node.pristineBonusRemaining = saved.pristineBonusRemaining;
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
                            site.familiarCount = savedSite.familiarCount;
                            site.pityHours = savedSite.pityHours;
                        }
                    }
                }
            }

            state.fossilFragments.Clear();
            if (save.fossilFragments != null)
            {
                foreach (var fossil in save.fossilFragments)
                {
                    if (fossil?.id != null)
                    {
                        // Unknown fossil ids are kept, same policy as elsewhere.
                        state.fossilFragments[fossil.id] = fossil.fragments;
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

            state.verseProgress.Clear();
            if (save.verseProgress != null)
            {
                foreach (var savedVerse in save.verseProgress)
                {
                    if (savedVerse?.verseId == null)
                    {
                        continue;
                    }

                    // Unknown verse ids are kept (a retuned rite may rename);
                    // slot rows beyond the current data's slot count are
                    // harmless — progress reads go by the data's indices.
                    var verse = new VerseProgressState { verseId = savedVerse.verseId };
                    if (savedVerse.slots != null)
                    {
                        foreach (var slot in savedVerse.slots)
                        {
                            verse.slots.Add(new SlotProgressState
                            {
                                delivered = slot?.delivered ?? 0.0,
                                granted = slot?.granted ?? false,
                            });
                        }
                    }

                    state.verseProgress.Add(verse);
                }
            }

            // Last, so completed fossils' effects fold in with the upgrades'.
            Upgrades.RecomputeYieldMultipliers(state, data);

            // A verse that revealed since this save was taken (or a deed slot
            // an older build left unsynced) credits deeds already done.
            Rite.SyncDeedSlots(state, data);
            return state;
        }

        /// <summary>Distinct zones among a save's nodes (ids are "zone:resource") — the v1 carrier back-grant.</summary>
        private static int CountZones(SaveData save)
        {
            var zones = new HashSet<string>();
            if (save.nodes != null)
            {
                foreach (var node in save.nodes)
                {
                    var split = node?.id?.IndexOf(':') ?? -1;
                    if (split > 0)
                    {
                        zones.Add(node.id.Substring(0, split));
                    }
                }
            }

            return zones.Count;
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
        /// Returns false for a save from a future build — never guess at a
        /// shape this build doesn't know.
        /// </summary>
        public static bool TryMigrate(SaveData save)
        {
            if (save == null || save.version > CurrentVersion)
            {
                return false;
            }

            // v1 was the first released shape; pre-v1 (hand-edited or very
            // early dev saves) enters the ladder at v1.
            if (save.version < 1)
            {
                save.version = 1;
            }

            while (save.version < CurrentVersion)
            {
                switch (save.version)
                {
                    case 1:
                        // v1 predates carriers and baskets. Grant the regional
                        // seed carrier per zone the save had opened, matching
                        // what the live path would have accrued; baskets
                        // default to empty via the missing-field default.
                        save.carrierCount = System.Math.Max(1, CountZones(save));
                        save.version = 2;
                        break;

                    case 2:
                        // v2 predates crafting — stations simply start empty.
                        save.stations = save.stations ?? new List<SavedStation>();
                        save.version = 3;
                        break;

                    case 3:
                        // v3 predates camp buildings — no levels bought yet.
                        save.buildingLevels = save.buildingLevels ?? new List<SavedBuildingLevel>();
                        save.version = 4;
                        break;

                    case 4:
                        // v4 predates skill XP — every skill starts back at
                        // level 1 (XP was never earned, not lost).
                        save.skillXp = save.skillXp ?? new List<SavedSkillXp>();
                        save.version = 5;
                        break;

                    case 5:
                        // v5 nodes carried a masteryLevel nothing ever granted
                        // (always 0) — dropped on read; masteryXp starts fresh.
                        save.version = 6;
                        break;

                    case 6:
                        // v6 predates discrete hauling — the fleet starts a
                        // fresh trip (haulTripProgress's missing-field zero).
                        save.version = 7;
                        break;

                    case 7:
                        // v7 predates quality rolls — the quality pools start
                        // empty and Restore reseeds the missing (0) rng state.
                        save.fineResources = save.fineResources ?? new List<SavedResource>();
                        save.pristineResources = save.pristineResources ?? new List<SavedResource>();
                        save.version = 8;
                        break;

                    case 8:
                        // v8 predates excavation — sites resync from owned
                        // upgrades on restore; no fragments found yet.
                        save.digSites = save.digSites ?? new List<SavedDigSite>();
                        save.fossilFragments = save.fossilFragments ?? new List<SavedFossilFragments>();
                        save.version = 9;
                        break;

                    case 9:
                        // v9 predates the Rite runtime — nothing offered, no
                        // deeds counted, renown at the missing-field zero.
                        save.deedCounts = save.deedCounts ?? new List<SavedDeedCount>();
                        save.verseProgress = save.verseProgress ?? new List<SavedVerseProgress>();
                        save.version = 10;
                        break;

                    case 10:
                        // v10 predates Migration — no camp has folded yet
                        // (migrationCount's missing-field zero).
                        save.version = 11;
                        break;

                    case 11:
                        // v11 predates the Almanac — no nodes bought yet.
                        save.almanacNodeIds = save.almanacNodeIds ?? new List<string>();
                        save.version = 12;
                        break;

                    case 12:
                        // v12 predates the warden's kit — bare hands.
                        save.gear = save.gear ?? new List<SavedGearSlot>();
                        save.version = 13;
                        break;

                    case 13:
                        // v13 predates the Museum — nothing donated yet.
                        save.donatedResources = save.donatedResources ?? new List<string>();
                        save.version = 14;
                        break;

                    case 14:
                        // v14 predates bonded familiars — no post recorded;
                        // earned bonds themselves are derived, never stored,
                        // so they need no migration at all.
                        save.version = 15;
                        break;

                    case 15:
                        // v15 predates the Compendium — the record starts here.
                        save.lifetimeGathered = save.lifetimeGathered ?? new List<SavedResource>();
                        save.lifetimeCrafted = save.lifetimeCrafted ?? new List<SavedTally>();
                        save.lifetimePristine = save.lifetimePristine ?? new List<SavedResource>();
                        save.version = 16;
                        break;

                    case 16:
                        // v16 predates the amber system — none held (the
                        // double already defaults to 0).
                        save.version = 17;
                        break;

                    case 17:
                        // v17 predates waystone reveals — an old save's
                        // already-unlocked zones will show their stones once,
                        // which reads as a feature, not a bug.
                        save.seenWaystoneZoneIds = save.seenWaystoneZoneIds ?? new List<string>();
                        save.version = 18;
                        break;

                    case 18:
                        // v18's "bonded post" became the warden's post — same
                        // meaning (the last-tended node), wider role.
                        save.wardenPostNodeId = save.bondedPostNodeId;
                        save.version = 19;
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
