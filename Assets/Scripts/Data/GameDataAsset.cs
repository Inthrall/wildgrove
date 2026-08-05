using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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

        // SHA-256 of the source JSON this asset was generated from AND of this
        // class's own shape; lets the importer and tests detect a stale asset
        // without reparsing. See SchemaFingerprint for why the shape is in there.
        public string sourceHash;

        public EconomyData economy;
        public List<ResourceData> resources = new List<ResourceData>();
        public List<ZoneData> zones = new List<ZoneData>();
        public List<UpgradeData> upgrades = new List<UpgradeData>();
        public List<RecipeData> recipes = new List<RecipeData>();
        public List<BuildingData> buildings = new List<BuildingData>();
        public List<GearData> gear = new List<GearData>();
        public List<InsectData> insects = new List<InsectData>();
        public List<AlmanacNodeData> almanac = new List<AlmanacNodeData>();
        public List<FolioSpreadData> folioSpreads = new List<FolioSpreadData>();
        public List<BondData> bonds = new List<BondData>();
        public List<SpeciesData> species = new List<SpeciesData>();
        public List<PlanterData> planters = new List<PlanterData>();
        public List<RegionData> regions = new List<RegionData>();
        public List<TinctureData> tinctures = new List<TinctureData>();

        /// <summary>The deep amber window (design §6) — Unity serializes an authored-empty section as a zeroed object; DeepAmber.Configured is the liveness check, never a null test.</summary>
        public DeepAmberData deepAmber;

        public ExchangeData exchange;
        public RitesBundle rites;
        public DialogueBundle dialogue;

        private Dictionary<string, ResourceData> resourcesById;
        private Dictionary<string, ZoneData> zonesById;
        private Dictionary<string, UpgradeData> upgradesById;
        private Dictionary<string, RecipeData> recipesById;
        private Dictionary<string, BuildingData> buildingsById;
        private Dictionary<string, GearData> gearById;
        private Dictionary<string, InsectData> insectsById;
        private Dictionary<string, AlmanacNodeData> almanacById;
        private Dictionary<string, SpeciesData> speciesById;
        private Dictionary<string, PlanterData> plantersById;
        private Dictionary<string, TinctureData> tincturesById;

        public IReadOnlyDictionary<string, ResourceData> ResourcesById => resourcesById ??= Index(resources, r => r.id);
        public IReadOnlyDictionary<string, ZoneData> ZonesById => zonesById ??= Index(zones, z => z.id);
        public IReadOnlyDictionary<string, UpgradeData> UpgradesById => upgradesById ??= Index(upgrades, u => u.id);
        public IReadOnlyDictionary<string, RecipeData> RecipesById => recipesById ??= Index(recipes, r => r.id);
        public IReadOnlyDictionary<string, BuildingData> BuildingsById => buildingsById ??= Index(buildings, b => b.id);
        public IReadOnlyDictionary<string, GearData> GearById => gearById ??= Index(gear, g => g.id);
        public IReadOnlyDictionary<string, InsectData> InsectsById => insectsById ??= Index(insects, f => f.id);
        public IReadOnlyDictionary<string, AlmanacNodeData> AlmanacById => almanacById ??= Index(almanac, a => a.id);
        public IReadOnlyDictionary<string, SpeciesData> SpeciesById => speciesById ??= Index(species, s => s.id);
        public IReadOnlyDictionary<string, PlanterData> PlantersById => plantersById ??= Index(planters, p => p.id);
        public IReadOnlyDictionary<string, TinctureData> TincturesById => tincturesById ??= Index(tinctures, t => t.id);

        /// <summary>
        /// A fingerprint of this class's own serialized shape — every public
        /// field's name and type, in a fixed order.
        /// <para>
        /// It belongs in the import hash because the importer skips its work
        /// when the hash matches, and until this was in there the hash covered
        /// only the JSON. Add a field here (or change one's type) without
        /// touching design/data, and the hash was unchanged, the editor-load
        /// import skipped, and Play mode went on running against an asset built
        /// by the old mapper — silently, because a missing field is just a
        /// default. Builds were safe (the build step forces a re-import); the
        /// editor was not, which is where the confusing hours get spent.
        /// </para>
        /// <para>
        /// Shape only. A mapper that changes how it PROJECTS an unchanged field
        /// still needs Wildgrove &gt; Import Design Data — this catches the
        /// common case, not every case.
        /// </para>
        /// </summary>
        public static string SchemaFingerprint()
        {
            var fields = typeof(GameDataAsset).GetFields(BindingFlags.Public | BindingFlags.Instance);
            var names = new List<string>(fields.Length);
            foreach (var field in fields)
            {
                names.Add(field.Name + ":" + field.FieldType.FullName);
            }

            // Reflection makes no promise about field order, so sorting is what
            // keeps the fingerprint stable across runtimes rather than churning
            // the asset on someone else's machine.
            names.Sort(StringComparer.Ordinal);
            // Newline-separated, not comma: a generic field's FullName carries
            // its own commas (List`1[[...,, Version=..., Culture=neutral,...]]),
            // so a comma join can't be split back into the entries it was made
            // from — and something will want to, if only a test.
            return string.Join("\n", names);
        }

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
