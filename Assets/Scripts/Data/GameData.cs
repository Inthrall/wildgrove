using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Wildgrove.Data
{
    /// <summary>Raw JSON text of the design data files.</summary>
    public sealed class GameDataSources
    {
        public string EconomyJson { get; set; }
        public string ResourcesJson { get; set; }
        public string ZonesJson { get; set; }
        public string UpgradesJson { get; set; }
        public string RecipesJson { get; set; }
        public string BuildingsJson { get; set; }
        public string GearJson { get; set; }
        public string InsectsJson { get; set; }
        public string RitesJson { get; set; }
        public string AlmanacJson { get; set; }
        public string FolioJson { get; set; }
        public string BondsJson { get; set; }
        public string SpeciesJson { get; set; }
        public string ExchangeJson { get; set; }
        public string DialogueJson { get; set; }
        public string PlantersJson { get; set; }
    }

    /// <summary>
    /// The authoring model: design/data/*.json parsed and indexed. Used by the
    /// validator, tests, and GameDataImporter (which maps it onto the runtime
    /// GameDataAsset ScriptableObject). Parse throws on malformed JSON or
    /// unknown effect types; referential integrity is checked separately by
    /// GameDataValidator.
    /// </summary>
    public sealed class GameData
    {
        /// <summary>
        /// The zone every fresh run opens in. Lives here (not in the sim's
        /// GameStateFactory) so the validator can enforce that the id exists
        /// and is the lowest-order zone — the two definitions of "starting
        /// zone" must never diverge, or validation proves reachability
        /// against a different zone than the one the runtime actually seeds.
        /// </summary>
        public const string StartingZoneId = "sunfield-meadow";

        public EconomyConfig Economy { get; private set; }
        public IReadOnlyList<ResourceDef> Resources { get; private set; }
        public IReadOnlyList<ZoneDef> Zones { get; private set; }
        public IReadOnlyList<UpgradeDef> Upgrades { get; private set; }
        public IReadOnlyList<RecipeDef> Recipes { get; private set; }
        public IReadOnlyList<BuildingDef> Buildings { get; private set; }
        public IReadOnlyList<GearDef> Gear { get; private set; }
        public IReadOnlyList<InsectDef> Insects { get; private set; }
        public RitesConfig Rites { get; private set; }
        public IReadOnlyList<AlmanacDef> Almanac { get; private set; }
        public IReadOnlyList<FolioSpreadDef> Spreads { get; private set; }
        public IReadOnlyList<BondDef> Bonds { get; private set; }
        public IReadOnlyList<SpeciesDef> Species { get; private set; }
        public IReadOnlyList<PlanterDef> Planters { get; private set; }
        public ExchangeConfig Exchange { get; private set; }
        public DialogueData Dialogue { get; private set; }

        public IReadOnlyDictionary<string, ResourceDef> ResourcesById { get; private set; }
        public IReadOnlyDictionary<string, ZoneDef> ZonesById { get; private set; }
        public IReadOnlyDictionary<string, UpgradeDef> UpgradesById { get; private set; }
        public IReadOnlyDictionary<string, RecipeDef> RecipesById { get; private set; }
        public IReadOnlyDictionary<string, BuildingDef> BuildingsById { get; private set; }
        public IReadOnlyDictionary<string, GearDef> GearById { get; private set; }
        public IReadOnlyDictionary<string, InsectDef> InsectsById { get; private set; }
        public IReadOnlyDictionary<string, AlmanacDef> AlmanacById { get; private set; }
        public IReadOnlyDictionary<string, FolioSpreadDef> SpreadsById { get; private set; }
        public IReadOnlyDictionary<string, BondDef> BondsById { get; private set; }
        public IReadOnlyDictionary<string, SpeciesDef> SpeciesById { get; private set; }
        public IReadOnlyDictionary<string, PlanterDef> PlantersById { get; private set; }

        private GameData()
        {
        }

        public static GameData Parse(GameDataSources sources)
        {
            var settings = CreateSerializerSettings();

            var data = new GameData
            {
                Economy = JsonConvert.DeserializeObject<EconomyConfig>(sources.EconomyJson, settings),
                Resources = JsonConvert.DeserializeObject<ResourcesFile>(sources.ResourcesJson, settings).Resources,
                Zones = JsonConvert.DeserializeObject<ZonesFile>(sources.ZonesJson, settings).Zones,
                Upgrades = JsonConvert.DeserializeObject<UpgradesFile>(sources.UpgradesJson, settings).Upgrades,
                Recipes = JsonConvert.DeserializeObject<RecipesFile>(sources.RecipesJson, settings).Recipes,
                Buildings = JsonConvert.DeserializeObject<BuildingsFile>(sources.BuildingsJson, settings).Buildings,
                Gear = JsonConvert.DeserializeObject<GearFile>(sources.GearJson, settings).Gear,
                Insects = JsonConvert.DeserializeObject<InsectsFile>(sources.InsectsJson, settings).Insects,
                Rites = JsonConvert.DeserializeObject<RitesConfig>(sources.RitesJson, settings),
                Almanac = JsonConvert.DeserializeObject<AlmanacFile>(sources.AlmanacJson, settings).Nodes,
                Spreads = JsonConvert.DeserializeObject<FolioFile>(sources.FolioJson, settings).Spreads,
                Bonds = JsonConvert.DeserializeObject<BondsFile>(sources.BondsJson, settings).Bonds,
                Species = JsonConvert.DeserializeObject<SpeciesFile>(sources.SpeciesJson, settings).Species,
                Planters = JsonConvert.DeserializeObject<PlantersFile>(sources.PlantersJson, settings).Planters,
                Exchange = JsonConvert.DeserializeObject<ExchangeConfig>(sources.ExchangeJson, settings),
                Dialogue = JsonConvert.DeserializeObject<DialogueData>(sources.DialogueJson, settings)
            };

            data.BuildLookups();
            return data;
        }

        /// <summary>Editor/test entry point — loads straight from a directory of the design/data JSON files.</summary>
        public static GameData LoadFromFiles(string directory)
        {
            return Parse(ReadSourcesFromFiles(directory));
        }

        public static GameDataSources ReadSourcesFromFiles(string directory)
        {
            return new GameDataSources
            {
                EconomyJson = File.ReadAllText(Path.Combine(directory, "economy.json")),
                ResourcesJson = File.ReadAllText(Path.Combine(directory, "resources.json")),
                ZonesJson = File.ReadAllText(Path.Combine(directory, "zones.json")),
                UpgradesJson = File.ReadAllText(Path.Combine(directory, "upgrades.json")),
                RecipesJson = File.ReadAllText(Path.Combine(directory, "recipes.json")),
                BuildingsJson = File.ReadAllText(Path.Combine(directory, "buildings.json")),
                GearJson = File.ReadAllText(Path.Combine(directory, "gear.json")),
                InsectsJson = File.ReadAllText(Path.Combine(directory, "insects.json")),
                RitesJson = File.ReadAllText(Path.Combine(directory, "rites.json")),
                AlmanacJson = File.ReadAllText(Path.Combine(directory, "almanac.json")),
                FolioJson = File.ReadAllText(Path.Combine(directory, "folio.json")),
                BondsJson = File.ReadAllText(Path.Combine(directory, "bonds.json")),
                SpeciesJson = File.ReadAllText(Path.Combine(directory, "species.json")),
                ExchangeJson = File.ReadAllText(Path.Combine(directory, "exchange.json")),
                DialogueJson = File.ReadAllText(Path.Combine(directory, "dialogue.json")),
                PlantersJson = File.ReadAllText(Path.Combine(directory, "planters.json"))
            };
        }

        /// <summary>Fingerprint of every source file, stored on GameDataAsset to detect staleness.</summary>
        public static string ComputeSourceHash(GameDataSources sources)
        {
            // The separator ends in an escaped NUL: it can't appear in JSON
            // text, so file boundaries never collide. Keep it as the escape
            // sequence — a raw NUL byte here once made git and grep treat
            // this whole file as binary.
            var combined = string.Join("\n\u0000", sources.EconomyJson, sources.ResourcesJson, sources.ZonesJson, sources.UpgradesJson,
                sources.RecipesJson, sources.BuildingsJson, sources.GearJson, sources.InsectsJson, sources.RitesJson,
                sources.AlmanacJson, sources.FolioJson, sources.BondsJson, sources.SpeciesJson, sources.ExchangeJson, sources.DialogueJson,
                sources.PlantersJson);
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(combined));
                var builder = new StringBuilder(bytes.Length * 2);
                foreach (var b in bytes)
                {
                    builder.Append(b.ToString("x2"));
                }

                return builder.ToString();
            }
        }

        private static JsonSerializerSettings CreateSerializerSettings()
        {
            return new JsonSerializerSettings
            {
                ContractResolver = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy() },
                Converters = { new StringEnumConverter(new CamelCaseNamingStrategy(), allowIntegerValues: false) },
                // Explicit nulls must not overwrite the collection initializers on the def classes.
                NullValueHandling = NullValueHandling.Ignore
            };
        }

        private void BuildLookups()
        {
            // First occurrence wins on duplicate ids; GameDataValidator reports them.
            ResourcesById = IndexById(Resources, r => r.Id);
            ZonesById = IndexById(Zones, z => z.Id);
            UpgradesById = IndexById(Upgrades, u => u.Id);
            RecipesById = IndexById(Recipes, r => r.Id);
            BuildingsById = IndexById(Buildings, b => b.Id);
            GearById = IndexById(Gear, g => g.Id);
            InsectsById = IndexById(Insects, f => f.Id);
            AlmanacById = IndexById(Almanac, a => a.Id);
            SpreadsById = IndexById(Spreads, s => s.Id);
            BondsById = IndexById(Bonds, b => b.Id);
            SpeciesById = IndexById(Species, s => s.Id);
            PlantersById = IndexById(Planters, p => p.Id);
        }

        private static IReadOnlyDictionary<string, T> IndexById<T>(IEnumerable<T> items, System.Func<T, string> id)
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

        private sealed class ResourcesFile
        {
            public List<ResourceDef> Resources { get; set; }
        }

        private sealed class ZonesFile
        {
            public List<ZoneDef> Zones { get; set; }
        }

        private sealed class UpgradesFile
        {
            public List<UpgradeDef> Upgrades { get; set; }
        }

        private sealed class RecipesFile
        {
            public List<RecipeDef> Recipes { get; set; }
        }

        private sealed class BuildingsFile
        {
            public List<BuildingDef> Buildings { get; set; }
        }

        private sealed class GearFile
        {
            public List<GearDef> Gear { get; set; }
        }

        private sealed class InsectsFile
        {
            public List<InsectDef> Insects { get; set; }
        }

        private sealed class AlmanacFile
        {
            public List<AlmanacDef> Nodes { get; set; }
        }

        private sealed class BondsFile
        {
            public List<BondDef> Bonds { get; set; }
        }

        private sealed class SpeciesFile
        {
            public List<SpeciesDef> Species { get; set; }
        }

        private sealed class PlantersFile
        {
            public List<PlanterDef> Planters { get; set; }
        }

        private sealed class FolioFile
        {
            public List<FolioSpreadDef> Spreads { get; set; }
        }
    }
}
