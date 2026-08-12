using System.Collections.Generic;
using System.Linq;
using BreakInfinity;
using Wildgrove.Data;

namespace Wildgrove.Sim.Saves
{
    /// <summary>
    /// The live run written down: <see cref="GameState"/> to the versioned wire
    /// shape. Every field captured here needs a counterpart in
    /// <c>SaveCodec.Restore</c> and, once shipped, a rung in
    /// <c>SaveCodec.Migrations</c>.
    /// </summary>
    public static partial class SaveCodec
    {
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
                campName = state.campName,
                amber = state.amber,
                foldedVersesSung = state.foldedVersesSung,
                sungVerseZones = new List<string>(state.sungVerseZones),
                grandfatheredKithSlots = state.grandfatheredKithSlots,
                purchasedKithSlots = state.purchasedKithSlots,
                starterBundleAmberGranted = state.starterBundleAmberGranted,
                droversHalterOwned = state.droversHalterOwned,
                wayfarersPlateOwned = state.wayfarersPlateOwned,
                weeklyCacheClaimedUnixMs = state.weeklyCacheClaimedUnixMs,
                adDripClaimedUnixMs = state.adDripClaimedUnixMs,
                timeSkipClaimedUnixMs = state.timeSkipClaimedUnixMs,
                timeSkipBudgetHours = state.timeSkipBudgetHours,
                timeSkipBudgetStampUnixMs = state.timeSkipBudgetStampUnixMs,
                exchangeConsiderationWindowIndex = state.exchangeConsiderationWindowIndex,
                exchangeConsiderationsThisWindow = state.exchangeConsiderationsThisWindow,
                secondQueueBought = state.secondQueueBought,
                clockHighWaterUnixMs = state.clockHighWaterUnixMs,
                hemisphere = state.hemisphere,
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

            foreach (var claim in state.sabbatClaims)
            {
                save.sabbatClaims.Add(new SavedSabbatClaim
                {
                    sabbatId = claim.sabbatId,
                    year = claim.year,
                    hemisphere = claim.hemisphere,
                });
            }

            if (state.keeping != null)
            {
                save.keeping = new SavedKeeping
                {
                    sabbatId = state.keeping.sabbatId,
                    year = state.keeping.year,
                    hemisphere = state.keeping.hemisphere,
                    generatedForMigration = state.keeping.generatedForMigration,
                    tierGranted = state.keeping.tierGranted,
                };
                foreach (var slot in state.keeping.slots)
                {
                    save.keeping.slots.Add(new SavedKeepingSlot
                    {
                        kind = slot.kind,
                        goodsId = slot.goodsId,
                        target = slot.target,
                        delivered = slot.delivered,
                        renownGrant = slot.renownGrant,
                    });
                }
            }

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
    }
}
