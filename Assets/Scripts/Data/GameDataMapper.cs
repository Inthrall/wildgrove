using System.Collections.Generic;
using System.Linq;

namespace Wildgrove.Data
{
    /// <summary>Maps the JSON authoring model (GameData) onto the Unity-serializable runtime model (GameDataAsset).</summary>
    public static class GameDataMapper
    {
        public static void Populate(GameDataAsset asset, GameData data)
        {
            asset.economy = MapEconomy(data.Economy);
            asset.resources = data.Resources.Select(MapResource).ToList();
            asset.zones = data.Zones.Select(MapZone).ToList();
            asset.upgrades = data.Upgrades.Select(MapUpgrade).ToList();
            asset.recipes = data.Recipes.Select(MapRecipe).ToList();
            asset.buildings = data.Buildings.Select(MapBuilding).ToList();
            asset.gear = data.Gear.Select(MapGear).ToList();
            asset.insects = data.Insects.Select(MapInsect).ToList();
            asset.almanac = data.Almanac.Select(MapAlmanacNode).ToList();
            asset.folioSpreads = data.Spreads.Select(MapSpread).ToList();
            asset.bonds = data.Bonds.Select(MapBond).ToList();
            asset.species = data.Species.Select(MapSpecies).ToList();
            asset.planters = data.Planters.Select(MapPlanter).ToList();
            asset.regions = data.Regions.Select(MapRegion).ToList();
            asset.tinctures = data.Tinctures.Select(MapTincture).ToList();
            asset.deepAmber = MapDeepAmber(data.DeepAmber);
            asset.exchange = data.Exchange == null ? null : new ExchangeData
            {
                spread = data.Exchange.Spread,
                offerMinutes = data.Exchange.OfferMinutes
            };
            asset.rites = MapRites(data.Rites);
            asset.dialogue = MapDialogue(data.Dialogue);
        }

        private static ResourceData MapResource(ResourceDef r)
        {
            return new ResourceData
            {
                id = r.Id,
                sellValue = r.SellValue,
                skill = r.Skill
            };
        }

        private static ZoneData MapZone(ZoneDef z)
        {
            return new ZoneData
            {
                id = z.Id,
                order = z.Order,
                displayName = z.Name,
                resources = new List<string>(z.Resources),
                unlocks = new List<string>(z.Unlocks),
                keystone = z.Keystone,
                digSite = z.DigSite,
                verseSite = z.VerseSite,
                requiredTool = z.RequiredTool,
                minMigration = z.MinMigration,
                scope = z.Scope
            };
        }

        private static UpgradeData MapUpgrade(UpgradeDef u)
        {
            return new UpgradeData
            {
                order = u.Order,
                id = u.Id,
                displayName = u.Name,
                track = u.Track,
                toolTier = u.ToolTier,
                gateSkill = u.GateSkill,
                gateLevel = u.GateLevel,
                minMigration = u.MinMigration,
                materials = MapItemAmounts(u.Materials),
                effects = u.Effects.Select(MapEffect).ToList()
            };
        }

        private static RecipeData MapRecipe(RecipeDef r)
        {
            return new RecipeData
            {
                id = r.Id,
                station = r.Station,
                skill = r.Skill,
                inputs = MapItemAmounts(r.Inputs),
                output = r.Output,
                valueMult = r.ValueMult,
                kind = r.Kind,
                defaultKnown = r.DefaultKnown,
                stationLevel = r.StationLevel,
                skillLevel = r.SkillLevel
            };
        }

        private static BuildingData MapBuilding(BuildingDef b)
        {
            return new BuildingData
            {
                id = b.Id,
                displayName = b.Name,
                materials = MapItemAmounts(b.Materials),
                milestoneUpgradeIds = new List<string>(b.MilestoneUpgradeIds),
                perLevel = b.PerLevel == null ? null : new BuildingPerLevelData
                {
                    type = b.PerLevel.Type,
                    station = b.PerLevel.Station,
                    value = b.PerLevel.Value,
                },
            };
        }

        private static GearData MapGear(GearDef g)
        {
            return new GearData
            {
                id = g.Id,
                displayName = g.Name,
                slot = g.Slot,
                skill = g.Skill,
                materials = MapItemAmounts(g.Materials),
                effects = g.Effects.Select(MapEffect).ToList()
            };
        }

        private static FolioSpreadData MapSpread(FolioSpreadDef s)
        {
            return new FolioSpreadData
            {
                id = s.Id,
                displayName = s.Name,
                entries = new List<string>(s.Entries),
                effects = s.Effects.Select(MapEffect).ToList()
            };
        }

        private static BondData MapBond(BondDef b)
        {
            return new BondData
            {
                id = b.Id,
                displayName = b.Name,
                species = b.Species,
                role = b.Role,
                source = b.Source == null ? null : new BondSourceData { type = b.Source.Type, id = b.Source.Id }
            };
        }

        private static SpeciesData MapSpecies(SpeciesDef s)
        {
            return new SpeciesData
            {
                id = s.Id,
                displayName = s.Name,
                roleLean = s.RoleLean,
                suggestedNames = new List<string>(s.SuggestedNames),
                trait = MapTrait(s.Trait),
                minMigration = s.MinMigration,
                inscriptions = new List<string>(s.Inscriptions)
            };
        }

        private static RegionData MapRegion(RegionDef r)
        {
            return new RegionData
            {
                id = r.Id,
                displayName = r.Name,
                sign = r.Sign,
                effects = r.Effects.Select(MapEffect).ToList()
            };
        }

        private static TinctureData MapTincture(TinctureDef t)
        {
            return new TinctureData
            {
                id = t.Id,
                displayName = t.Name,
                description = t.Description,
                durationSec = t.DurationSec,
                effects = t.Effects.Select(MapEffect).ToList()
            };
        }

        private static DeepAmberData MapDeepAmber(DeepAmberDef d)
        {
            return d == null ? null : new DeepAmberData
            {
                zoneId = d.Zone,
                findsPerHour = d.FindsPerHour,
                pityHoursWatched = d.PityHoursWatched,
                plateName = d.PlateName,
                completedLore = d.CompletedLore,
                effects = d.Effects.Select(MapEffect).ToList(),
                pieces = d.Pieces.Select(p => new AmberPieceData
                {
                    id = p.Id,
                    displayName = p.Name,
                    lore = p.Lore
                }).ToList()
            };
        }

        private static TraitData MapTrait(TraitDef t)
        {
            return t == null ? null : new TraitData
            {
                displayName = t.Name,
                description = t.Description,
                kind = t.Kind,
                value = t.Value,
                resources = ResolveTraitResources(t)
            };
        }

        // The authored pair (Resources), falling back to the legacy single
        // Resource so older files still map. Empty for non-node traits.
        private static List<string> ResolveTraitResources(TraitDef t)
        {
            if (t.Resources != null && t.Resources.Count > 0)
            {
                return new List<string>(t.Resources);
            }

            return string.IsNullOrEmpty(t.Resource) ? new List<string>() : new List<string> { t.Resource };
        }

        private static PlanterData MapPlanter(PlanterDef p)
        {
            return new PlanterData
            {
                id = p.Id,
                displayName = p.Name,
                kind = p.Kind,
                value = p.Value,
                target = p.Target,
                materials = MapItemAmounts(p.Materials)
            };
        }

        private static AlmanacNodeData MapAlmanacNode(AlmanacDef a)
        {
            return new AlmanacNodeData
            {
                id = a.Id,
                displayName = a.Name,
                costVerdure = a.CostVerdure,
                requires = a.Requires,
                repeatable = a.Repeatable,
                effects = a.Effects.Select(MapEffect).ToList()
            };
        }

        private static InsectData MapInsect(InsectDef f)
        {
            return new InsectData
            {
                id = f.Id,
                displayName = f.Name,
                sketches = f.Sketches,
                habitats = new List<string>(f.Habitats),
                rarity = f.Rarity,
                rewarded = f.Rewarded,
                effects = f.Effects.Select(MapEffect).ToList()
            };
        }

        private static EffectData MapEffect(EffectDef e)
        {
            return new EffectData
            {
                type = e.Type,
                skill = e.Skill,
                zone = e.Zone,
                resource = e.Resource,
                recipe = e.Recipe,
                species = e.Species,
                upgrade = e.Upgrade,
                value = e.Value ?? 0
            };
        }

        private static RitesBundle MapRites(RitesConfig r)
        {
            return new RitesBundle
            {
                chooseCount = r.ChooseCount,
                generator = r.Generator == null ? null : new RiteGeneratorConfigData
                {
                    demandGrowth = r.Generator.DemandGrowth,
                    spotlightDiscount = r.Generator.SpotlightDiscount,
                    offSpotlightPremium = r.Generator.OffSpotlightPremium,
                    chooseCountPerMigrations = r.Generator.ChooseCountPerMigrations,
                    chooseCountMax = r.Generator.ChooseCountMax,
                    valueSpread = r.Generator.ValueSpread
                },
                rites = r.Rites.Select(rite => new RiteData
                {
                    id = rite.Id,
                    migration = rite.Migration,
                    verses = rite.Verses.Select(v => new RiteVerseData
                    {
                        id = v.Id,
                        zone = v.Zone,
                        spotlight = new List<string>(v.Spotlight),
                        slots = v.Slots.Select(s => new RiteSlotData
                        {
                            type = s.Type,
                            resource = s.Resource,
                            amount = s.Amount,
                            deed = s.Deed,
                            count = s.Count,
                            quality = s.Quality,
                            renownGrant = s.RenownGrant
                        }).ToList()
                    }).ToList()
                }).ToList()
            };
        }

        private static DialogueBundle MapDialogue(DialogueData d)
        {
            return new DialogueBundle
            {
                waystones = d.Waystones.Select(kv => new StringEntry { key = kv.Key, text = kv.Value }).ToList(),
                verses = d.Verses.Select(kv => new StringEntry { key = kv.Key, text = kv.Value }).ToList(),
                provisioner = d.Provisioner.Select(p => new ProvisionerEntry { id = p.Id, trigger = p.Trigger, line = p.Line }).ToList(),
                migrationVignette = new List<string>(d.MigrationVignette),
                insectPlates = d.InsectPlates.Select(kv => new StringEntry { key = kv.Key, text = kv.Value }).ToList(),
                finalWaystones = new FinalWaystonesData
                {
                    zoneId = d.FinalWaystones?.Zone,
                    stones = (d.FinalWaystones?.Stones ?? new List<DialogueData.FinalWaystoneStone>())
                        .Select(s => new StringEntry { key = s.Id, text = s.Text }).ToList()
                }
            };
        }

        private static List<ItemAmount> MapItemAmounts(Dictionary<string, int> source)
        {
            return source.Select(kv => new ItemAmount { id = kv.Key, amount = kv.Value }).ToList();
        }

        private static EconomyData MapEconomy(EconomyConfig e)
        {
            RequireEconomySections(e);
            return new EconomyData
            {
                costGrowth = new EconomyData.CostGrowthData
                {
                    building = e.CostGrowth.Building,
                    almanac = e.CostGrowth.Almanac
                },
                gifts = new EconomyData.GiftsData
                {
                    pileGoods = e.Gifts.PileGoods
                },
                delivery = new EconomyData.DeliveryData
                {
                    batchSeconds = e.Delivery.BatchSeconds
                },
                kith = new EconomyData.KithData
                {
                    slotsBase = e.Kith.SlotsBase,
                    slotsMax = e.Kith.SlotsMax,
                    verseMilestones = e.Kith.VerseMilestones != null
                        ? new List<int>(e.Kith.VerseMilestones)
                        : new List<int>(),
                    generatorGatherPosts = e.Kith.GeneratorGatherPosts,
                    gatherPerSecond = e.Kith.GatherPerSecond
                },
                crafting = new EconomyData.CraftingData
                {
                    baseCraftSeconds = e.Crafting.BaseCraftSeconds
                },
                tools = new EconomyData.ToolsData
                {
                    tiers = new List<string>(e.Tools.Tiers)
                },
                mastery = new EconomyData.MasteryData
                {
                    yieldBonusPerLevel = e.Mastery.YieldBonusPerLevel,
                    baseXp = e.Mastery.Base,
                    growth = e.Mastery.Growth,
                    maxLevel = e.Mastery.MaxLevel,
                    xpPerUnit = e.Mastery.XpPerUnit
                },
                verdure = new EconomyData.VerdureData
                {
                    renownDivisor = e.Verdure.RenownDivisor,
                    exponent = e.Verdure.Exponent,
                    yieldBonusPerPoint = e.Verdure.YieldBonusPerPoint
                },
                xp = new EconomyData.XpData
                {
                    baseXp = e.Xp.Base,
                    growth = e.Xp.Growth,
                    maxLevel = e.Xp.MaxLevel,
                    gatherPerUnit = e.Xp.GatherPerUnit,
                    craftPerBatch = e.Xp.CraftPerBatch
                },
                offline = new EconomyData.OfflineData
                {
                    baseCapHours = e.Offline.BaseCapHours,
                    rateMultiplier = e.Offline.RateMultiplier
                },
                quality = new EconomyData.QualityData
                {
                    fineChance = e.Quality.FineChance,
                    fineValueMult = e.Quality.FineValueMult,
                    pristineBaseChance = e.Quality.PristineBaseChance,
                    pristineValueMult = e.Quality.PristineValueMult
                },
                observation = new EconomyData.ObservationData
                {
                    pityTimerHoursWatched = e.Observation.PityTimerHoursWatched,
                    baseSketchesPerHour = e.Observation.BaseSketchesPerHour,
                    skill = e.Observation.Skill,
                    watchXpPerHour = e.Observation.WatchXpPerHour
                },
                amber = e.Amber == null ? null : new EconomyData.AmberData
                {
                    digFindsPerHour = e.Amber.DigFindsPerHour,
                    perFind = e.Amber.PerFind,
                    timeSkipHours = e.Amber.TimeSkipHours,
                    timeSkipCostAmber = e.Amber.TimeSkipCostAmber,
                    timeSkipDailyCapHours = e.Amber.TimeSkipDailyCapHours,
                    adDripAmber = e.Amber.AdDripAmber,
                    weeklyCacheAmber = e.Amber.WeeklyCacheAmber,
                    renameCostAmber = e.Amber.RenameCostAmber
                },
                store = e.Store == null ? null : new EconomyData.StoreData
                {
                    starterBundleAmber = e.Store.StarterBundleAmber,
                    amberPackSmall = e.Store.AmberPackSmall,
                    amberPackLarge = e.Store.AmberPackLarge
                },
                tending = new EconomyData.TendingData
                {
                    burstYieldMult = e.Tending.BurstYieldMult,
                    burstDurationSec = e.Tending.BurstDurationSec,
                    pristineBonusDurationSec = e.Tending.PristineBonusDurationSec,
                    pristineChanceBonus = e.Tending.PristineChanceBonus
                },
                warden = new EconomyData.WardenData
                {
                    gatherPerSecond = e.Warden.GatherPerSecond
                },
                bubbles = e.Bubbles == null ? null : new EconomyData.BubblesData
                {
                    spawnIntervalSec = e.Bubbles.SpawnIntervalSec,
                    lifetimeSec = e.Bubbles.LifetimeSec,
                    maxLive = e.Bubbles.MaxLive,
                    rewardSeconds = e.Bubbles.RewardSeconds,
                    rewardRatePerSecond = e.Bubbles.RewardRatePerSecond
                },
                familiarXp = e.FamiliarXp == null ? null : new EconomyData.FamiliarXpData
                {
                    baseXp = e.FamiliarXp.Base,
                    growth = e.FamiliarXp.Growth,
                    maxLevel = e.FamiliarXp.MaxLevel,
                    xpPerSecond = e.FamiliarXp.XpPerSecond,
                    kinshipDivisor = e.FamiliarXp.KinshipDivisor,
                    kinshipXpRatePerLevel = e.FamiliarXp.KinshipXpRatePerLevel,
                    signatureMilestones = e.FamiliarXp.SignatureMilestones != null
                        ? new List<int>(e.FamiliarXp.SignatureMilestones)
                        : new List<int>(),
                    signatureDeepening = e.FamiliarXp.SignatureDeepening
                },
                replant = e.Replant == null ? null : new EconomyData.ReplantData
                {
                    baseCost = e.Replant.BaseCost,
                    growth = e.Replant.Growth,
                    richnessPerLevel = e.Replant.RichnessPerLevel
                }
            };
        }

        /// <summary>
        /// Fail with a readable message naming the missing economy section(s)
        /// rather than a bare NullReferenceException, for the case where mapping
        /// runs on config that never passed <see cref="GameDataValidator"/>
        /// (which requires all of these). Amber/Store/FamiliarXp/Replant are
        /// optional and null-guarded at the map site, so they aren't listed here.
        /// </summary>
        private static void RequireEconomySections(EconomyConfig e)
        {
            if (e == null)
            {
                throw new System.InvalidOperationException(
                    "Economy config is missing — run GameDataValidator before mapping.");
            }

            var missing = new List<string>();
            if (e.CostGrowth == null) { missing.Add("costGrowth"); }
            if (e.Gifts == null) { missing.Add("gifts"); }
            if (e.Delivery == null) { missing.Add("delivery"); }
            if (e.Kith == null) { missing.Add("kith"); }
            if (e.Crafting == null) { missing.Add("crafting"); }
            if (e.Tools == null) { missing.Add("tools"); }
            if (e.Mastery == null) { missing.Add("mastery"); }
            if (e.Verdure == null) { missing.Add("verdure"); }
            if (e.Xp == null) { missing.Add("xp"); }
            if (e.Offline == null) { missing.Add("offline"); }
            if (e.Quality == null) { missing.Add("quality"); }
            if (e.Observation == null) { missing.Add("observation"); }
            if (e.Tending == null) { missing.Add("tending"); }
            if (e.Warden == null) { missing.Add("warden"); }

            if (missing.Count > 0)
            {
                throw new System.InvalidOperationException(
                    "Economy config is missing required section(s): " + string.Join(", ", missing)
                    + " — run GameDataValidator before mapping.");
            }
        }
    }
}
