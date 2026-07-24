using System.Collections.Generic;
using UnityEngine;

namespace Wildgrove.Game
{
    /// <summary>
    /// Maps game ids (resource / zone / species / skill / gear / good / building /
    /// insect / journal-furniture / manufactured line motif) to the hand-picked
    /// public-domain naturalist plates and PSF ink drawings under
    /// <c>Assets/Resources/Art/</c>, loading each sprite lazily
    /// via <see cref="Resources"/> and caching it. Every lookup returns
    /// <c>null</c> when there is no art for that id (or the file is missing), so
    /// callers keep their <see cref="World.PlaceholderArt"/> fallback — the art
    /// is purely additive and a missing plate never breaks a screen.
    ///
    /// The id → file maps live here (not in filenames) because a few plates
    /// serve more than one id — one ingot plate covers copper/bronze/iron, the
    /// firefly keystone doubles as the fireflies gatherable — and the file names
    /// stay stable for the CREDITS ledger.
    /// </summary>
    public static class ArtLibrary
    {
        private const string Plates = "Art/Plates/";
        private const string Ui = "Art/UI/";

        // resource id (resources.json) → plate path
        private static readonly Dictionary<string, string> Resource = new Dictionary<string, string>
        {
            { "berries", Plates + "Resources/res-berries" },
            { "wildflowers", Plates + "Resources/res-wildflowers" },
            { "fibres", Plates + "Resources/res-fibres" },
            { "nuts", Plates + "Resources/res-nuts" },
            { "herbs", Plates + "Resources/res-herbs" },
            { "copper-scree", Plates + "Resources/res-copper-ore" },
            { "timber", Plates + "Resources/res-timber" },
            { "mushrooms", Plates + "Resources/res-mushrooms" },
            { "tin-seam", Plates + "Resources/res-tin-ore" },
            { "fish", Plates + "Resources/res-fish" },
            { "reeds", Plates + "Resources/res-reeds" },
            { "clay", Plates + "Resources/res-clay" },
            { "iron-gravel", Plates + "Resources/res-iron-ore" },
            { "peat", Plates + "Resources/res-peat" },
            { "rare-herbs", Plates + "Resources/res-rare-herbs" },
            { "fireflies", Plates + "Zones/keystone-lantern-firefly" },
            { "deep-ores", Plates + "Resources/res-deep-ores" },
            { "crystals", Plates + "Resources/res-crystals" },
            { "bone-beds", Plates + "Resources/res-bone" },
            { "eggs", Plates + "Resources/res-eggs" },
            { "wool", Plates + "Resources/res-wool" },
            { "lichen", Plates + "Resources/res-lichen" },
            { "sky-blossoms", Plates + "Resources/res-sky-blossoms" },
            { "glacier-ice", Plates + "Resources/res-glacier-ice" },
        };

        // crafted-good id (recipes.json output) → plate path. Falls through to
        // Resource for raw-material outputs that already have a gatherable plate.
        private static readonly Dictionary<string, string> Good = new Dictionary<string, string>
        {
            { "berry-preserve", Plates + "Goods/goods-preserve" },
            { "mushroom-skewer", Plates + "Goods/goods-skewer" },
            { "smoked-trout", Plates + "Goods/goods-smoked-trout" },
            { "fish-oil", Plates + "Goods/goods-fish-oil" },
            { "planks", Plates + "Goods/goods-planks" },
            { "reed-baskets", Plates + "Goods/goods-basket" },
            { "charcoal", Plates + "Goods/goods-charcoal" },
            { "copper-ingot", Plates + "Goods/goods-ingot" },
            { "bronze-ingot", Plates + "Goods/goods-ingot" },
            { "iron-ingot", Plates + "Goods/goods-ingot" },
            { "cordage", Plates + "Gear/gear-cordage" },
        };

        // building id (buildings.json) → camp-line plate
        private static readonly Dictionary<string, string> Building = new Dictionary<string, string>
        {
            { "fire", Plates + "Buildings/building-fire" },
            { "forge", Plates + "Buildings/building-forge" },
            { "bench", Plates + "Buildings/building-bench" },
            { "store", Plates + "Buildings/building-store" },
            { "roosts", Plates + "Buildings/building-roosts" },
        };

        // zone id (zones.json) → keystone specimen plate
        private static readonly Dictionary<string, string> Zone = new Dictionary<string, string>
        {
            { "sunfield-meadow", Plates + "Zones/keystone-sunburst-poppy" },
            { "bramble-hedgerows", Plates + "Zones/keystone-amber-snail" },
            { "old-growth-wood", Plates + "Zones/keystone-ancient-acorn" },
            { "silverrun-river", Plates + "Zones/keystone-moonscale-trout" },
            { "mistfen-marsh", Plates + "Zones/keystone-lantern-firefly" },
            { "the-hollows", Plates + "Zones/keystone-echo-geode" },
            { "highland-crags", Plates + "Zones/keystone-cloudfleece-ram" },
            { "cloudreach-peaks", Plates + "Zones/keystone-aurora-bloom" },
        };

        // species id (species.json) → roster plate. One plate per species; the
        // animal is chosen to match the powerup it gives (§4). All eight plates
        // are now wired to the starting kith.
        private static readonly Dictionary<string, string> Familiar = new Dictionary<string, string>
        {
            { "meadow-vole", Plates + "Familiars/familiar-vole" },
            { "red-squirrel", Plates + "Familiars/familiar-squirrel" },
            { "sedge-linnet", Plates + "Familiars/familiar-songbird" },
            { "bramble-hare", Plates + "Familiars/familiar-hare" },
            { "warren-weasel", Plates + "Familiars/familiar-weasel" },
            { "furrow-hedgehog", Plates + "Familiars/familiar-hedgehog" },
            { "tawny-owl", Plates + "Familiars/familiar-owl" },
            { "pack-raven", Plates + "Familiars/familiar-raven" },
        };

        // skill id (resources.json / recipes.json) → craft glyph
        private static readonly Dictionary<string, string> Craft = new Dictionary<string, string>
        {
            { "foraging", Ui + "Crafts/craft-foraging" },
            { "logging", Ui + "Crafts/craft-logging" },
            { "fishing", Ui + "Crafts/craft-fishing" },
            { "mining", Ui + "Crafts/craft-mining" },
            { "delving", Ui + "Crafts/craft-mining" },
            { "firecraft", Ui + "Crafts/craft-firecraft" },
            { "forgecraft", Ui + "Crafts/craft-forgecraft" },
            { "bushcraft", Ui + "Crafts/craft-bushcraft" },
            { "observation", Ui + "Crafts/craft-observation" },
            { "curation", Ui + "Crafts/craft-curation" },
            { "entomology", Ui + "Crafts/craft-entomology" },
            { "apothecary", Ui + "Crafts/craft-apothecary" },
            { "husbandry", Ui + "Crafts/craft-husbandry" },
        };

        // gear id (gear.json) → kit plate
        private static readonly Dictionary<string, string> Gear = new Dictionary<string, string>
        {
            { "cordage-wraps", Plates + "Gear/gear-cordage" },
            { "birch-frame-pack", Plates + "Gear/gear-pack" },
            { "pitch-torch", Plates + "Gear/gear-torch" },
            { "oilskin-tarp", Plates + "Gear/gear-tarp" },
            { "clay-lined-creel", Plates + "Gear/gear-creel" },
        };

        // insect id (insects.json) → deep-page plate
        private static readonly Dictionary<string, string> Insect = new Dictionary<string, string>
        {
            { "stags-herald", Plates + "Insects/insect-stags-herald" },
            { "silver-skimmer", Plates + "Insects/insect-silver-skimmer" },
            { "those-who-sow", Plates + "Insects/insect-those-who-sow" },
        };

        // manufactured line-art motifs the HUD places by a plain name — the PSF
        // ink drawings that aren't a single good/gear id: the trellis over every
        // planter, the seedling on Plant back, the hatchet heading the Ladder.
        private static readonly Dictionary<string, string> Line = new Dictionary<string, string>
        {
            { "planter", Plates + "Goods/goods-trellis" },
            { "seedling", Plates + "Goods/goods-seedling" },
            { "tools", Plates + "Goods/goods-tools" },
        };

        // journal furniture — chrome, keyed by a plain name
        private static readonly Dictionary<string, string> Journal = new Dictionary<string, string>
        {
            { "paper", Ui + "Journal/ui-paper-texture" },
            { "cairn", Ui + "Journal/ui-cairn" },
            { "caravan", Ui + "Journal/ui-caravan" },
            { "waystone", Ui + "Journal/ui-waystone" },
            { "almanac", Ui + "Journal/ui-almanac-tree" },
        };

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <summary>The plate for a gatherable resource, or null when there's no art.</summary>
        public static Sprite ForResource(string resourceId) => Load(Lookup(Resource, resourceId));

        /// <summary>The plate for a crafted good; falls back to the raw-resource plate of the same id.</summary>
        public static Sprite ForGood(string goodId) => Load(Lookup(Good, goodId) ?? Lookup(Resource, goodId));

        /// <summary>The keystone specimen plate for a zone, or null.</summary>
        public static Sprite ForZone(string zoneId) => Load(Lookup(Zone, zoneId));

        /// <summary>The roster plate for a familiar species, or null.</summary>
        public static Sprite ForSpecies(string speciesId) => Load(Lookup(Familiar, speciesId));

        /// <summary>The craft glyph for a skill, or null.</summary>
        public static Sprite ForSkill(string skillId) => Load(Lookup(Craft, skillId));

        /// <summary>The kit plate for a gear item, or null.</summary>
        public static Sprite ForGear(string gearId) => Load(Lookup(Gear, gearId));

        /// <summary>The deep-page plate for an insect, or null.</summary>
        public static Sprite ForInsect(string insectId) => Load(Lookup(Insect, insectId));

        /// <summary>The camp-line plate for a building, or null.</summary>
        public static Sprite ForBuilding(string buildingId) => Load(Lookup(Building, buildingId));

        /// <summary>A manufactured line-art motif by plain name ("planter", "seedling", "tools"), or null.</summary>
        public static Sprite ForLine(string name) => Load(Lookup(Line, name));

        /// <summary>A journal-furniture sprite by plain name ("paper", "waystone", …), or null.</summary>
        public static Sprite ForJournal(string name) => Load(Lookup(Journal, name));

        private static string Lookup(Dictionary<string, string> map, string id)
        {
            return !string.IsNullOrEmpty(id) && map.TryGetValue(id, out var path) ? path : null;
        }

        private static Sprite Load(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            if (Cache.TryGetValue(path, out var cached))
            {
                return cached;
            }

            var sprite = Resources.Load<Sprite>(path);
            Cache[path] = sprite; // cache the miss too, so a missing file isn't reloaded every frame
            return sprite;
        }
    }
}
