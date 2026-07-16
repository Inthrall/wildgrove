using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Wildgrove.Data
{
    /// <summary>
    /// The runtime content database — a single ScriptableObject generated from
    /// design/data/*.json by GameDataImporter (Wildgrove > Import Design Data).
    /// Do not edit the asset by hand; edit the JSON and reimport.
    /// </summary>
    public sealed class GameDataAsset : ScriptableObject
    {
        public const string ResourcesPath = "Data/GameData";

        // SHA-256 of the source JSON this asset was generated from; lets the
        // importer and tests detect a stale asset without reparsing.
        public string sourceHash;

        public EconomyData economy;
        public List<ResourceData> resources = new List<ResourceData>();
        public List<ZoneData> zones = new List<ZoneData>();
        public List<UpgradeData> upgrades = new List<UpgradeData>();
        public List<RecipeData> recipes = new List<RecipeData>();
        public List<BuildingData> buildings = new List<BuildingData>();
        public List<GearData> gear = new List<GearData>();
        public List<FossilData> fossils = new List<FossilData>();
        public List<AlmanacNodeData> almanac = new List<AlmanacNodeData>();
        public List<MuseumSetData> museumSets = new List<MuseumSetData>();
        public RitesBundle rites;
        public DialogueBundle dialogue;

        private Dictionary<string, ResourceData> resourcesById;
        private Dictionary<string, ZoneData> zonesById;
        private Dictionary<string, UpgradeData> upgradesById;
        private Dictionary<string, RecipeData> recipesById;
        private Dictionary<string, BuildingData> buildingsById;
        private Dictionary<string, GearData> gearById;
        private Dictionary<string, FossilData> fossilsById;
        private Dictionary<string, AlmanacNodeData> almanacById;

        public IReadOnlyDictionary<string, ResourceData> ResourcesById => resourcesById ??= Index(resources, r => r.id);
        public IReadOnlyDictionary<string, ZoneData> ZonesById => zonesById ??= Index(zones, z => z.id);
        public IReadOnlyDictionary<string, UpgradeData> UpgradesById => upgradesById ??= Index(upgrades, u => u.id);
        public IReadOnlyDictionary<string, RecipeData> RecipesById => recipesById ??= Index(recipes, r => r.id);
        public IReadOnlyDictionary<string, BuildingData> BuildingsById => buildingsById ??= Index(buildings, b => b.id);
        public IReadOnlyDictionary<string, GearData> GearById => gearById ??= Index(gear, g => g.id);
        public IReadOnlyDictionary<string, FossilData> FossilsById => fossilsById ??= Index(fossils, f => f.id);
        public IReadOnlyDictionary<string, AlmanacNodeData> AlmanacById => almanacById ??= Index(almanac, a => a.id);

        public static GameDataAsset LoadFromResources()
        {
            var asset = Resources.Load<GameDataAsset>(ResourcesPath);
            if (asset == null)
            {
                throw new FileNotFoundException($"Missing GameDataAsset at Resources/{ResourcesPath} — run Wildgrove > Import Design Data.");
            }

            return asset;
        }

        private static Dictionary<string, T> Index<T>(IEnumerable<T> items, System.Func<T, string> id)
        {
            var index = new Dictionary<string, T>();
            foreach (var item in items)
            {
                var key = id(item);
                if (key != null && !index.ContainsKey(key))
                {
                    index.Add(key, item);
                }
            }

            return index;
        }
    }
}
