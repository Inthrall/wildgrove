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
            asset.fossils = data.Fossils.Select(MapFossil).ToList();
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
                priced = z.MapCostCoin.HasValue,
                mapCostCoin = z.MapCostCoin ?? 0,
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
                costCoin = u.CostCoin,
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
                stationLevel = r.StationLevel
            };
        }

        private static BuildingData MapBuilding(BuildingDef b)
        {
            return new BuildingData
            {
                id = b.Id,
                displayName = b.Name,
                baseCostCoin = b.BaseCostCoin,
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

        private static FossilData MapFossil(FossilDef f)
        {
            return new FossilData
            {
                id = f.Id,
                displayName = f.Name,
                fragments = f.Fragments,
                digSites = new List<string>(f.DigSites),
                strataRarity = f.StrataRarity,
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
                value = e.Value ?? 0
            };
        }

        private static RitesBundle MapRites(RitesConfig r)
        {
            return new RitesBundle
            {
                chooseCount = r.ChooseCount,
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
                fossilCards = d.FossilCards.Select(kv => new StringEntry { key = kv.Key, text = kv.Value }).ToList()
            };
        }

        private static List<ItemAmount> MapItemAmounts(Dictionary<string, int> source)
        {
            return source.Select(kv => new ItemAmount { id = kv.Key, amount = kv.Value }).ToList();
        }

        private static EconomyData MapEconomy(EconomyConfig e)
        {
            return new EconomyData
            {
                costGrowth = new EconomyData.CostGrowthData
                {
                    gathererGift = e.CostGrowth.GathererGift,
                    carrierGift = e.CostGrowth.CarrierGift,
                    building = e.CostGrowth.Building
                },
                gifts = new EconomyData.GiftsData
                {
                    gathererBaseGoods = e.Gifts.GathererBaseGoods,
                    carrierBaseGoods = e.Gifts.CarrierBaseGoods
                },
                hauling = new EconomyData.HaulingData
                {
                    baseCarryCapacity = e.Hauling.BaseCarryCapacity,
                    tripSeconds = e.Hauling.TripSeconds,
                    basketCapacity = e.Hauling.BasketCapacity
                },
                familiarCaps = new EconomyData.FamiliarCapsData
                {
                    flockCapBase = e.FamiliarCaps.FlockCapBase,
                    flockCapPerRoostLevel = e.FamiliarCaps.FlockCapPerRoostLevel,
                    carrierSlotsBase = e.FamiliarCaps.CarrierSlotsBase,
                    carrierSlotsPerRoostLevel = e.FamiliarCaps.CarrierSlotsPerRoostLevel
                },
                crafting = new EconomyData.CraftingData
                {
                    baseCraftSeconds = e.Crafting.BaseCraftSeconds
                },
                tools = new EconomyData.ToolsData
                {
                    baseCostCoin = e.Tools.BaseCostCoin,
                    costMultPerTier = e.Tools.CostMultPerTier,
                    yieldMultPerTier = e.Tools.YieldMultPerTier,
                    tiers = new List<string>(e.Tools.Tiers)
                },
                mastery = new EconomyData.MasteryData
                {
                    yieldBonusPerLevel = e.Mastery.YieldBonusPerLevel
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
                    maxLevel = e.Xp.MaxLevel
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
                    pristineBaseChance = e.Quality.PristineBaseChance
                },
                excavation = new EconomyData.ExcavationData
                {
                    pityTimerHoursDug = e.Excavation.PityTimerHoursDug
                },
                tending = new EconomyData.TendingData
                {
                    burstYieldMult = e.Tending.BurstYieldMult,
                    burstDurationSec = e.Tending.BurstDurationSec,
                    pristineBonusDurationSec = e.Tending.PristineBonusDurationSec,
                    handGatherPerSecond = e.Tending.HandGatherPerSecond
                }
            };
        }
    }
}
