using System.Collections.Generic;
using UnityEngine;

namespace Wildgrove.Game
{
    /// <summary>
    /// Maps game ids (resource / zone / species / skill / gear / good / building /
    /// insect / journal-furniture / manufactured line motif / the warden's own
    /// mark) to the hand-picked
    /// public-domain naturalist plates and PSF ink drawings under
    /// <c>Assets/Resources/Art/</c>, loading each sprite lazily
    /// via <see cref="Resources"/> and caching it. Every lookup returns
    /// <c>null</c> when there is no art for that id (or the file is missing), so
    /// callers keep their <see cref="World.PlaceholderArt"/> fallback — the art
    /// is purely additive and a missing plate never breaks a screen.
    ///
    /// The id → file maps live here (not in filenames) because a few plates
    /// serve more than one id — one ingot plate covers copper, bronze, iron and
    /// deepsteel — and the file names stay stable for the CREDITS ledger.
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
            { "glow-moss", Plates + "Resources/res-glow-moss" },
            { "deep-ores", Plates + "Resources/res-deep-ores" },
            { "crystals", Plates + "Resources/res-crystals" },
            { "ashglass", Plates + "Resources/res-ashglass" },
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
            { "deep-ingot", Plates + "Goods/goods-ingot" },
            { "cordage", Plates + "Gear/gear-cordage" },
            // The tinctures (§5, Apothecary). Each is its own vessel so the four
            // read apart on a shelf of four rows: a carried flask, a salve pot,
            // a wicker-bound jug of something smoked, a stoppered summit phial.
            { "wardens-tonic", Plates + "Goods/goods-tonic" },
            { "glow-salve", Plates + "Goods/goods-salve" },
            { "peat-smoke-draught", Plates + "Goods/goods-draught" },
            { "aurora-cordial", Plates + "Goods/goods-cordial" },
            { "felted-cloak", Plates + "Goods/goods-cloak" },
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
        // animal is chosen to match the powerup it gives (§4). Every species has
        // one; the null path is still the designed fallback for anything added to
        // species.json ahead of its plate.
        private static readonly Dictionary<string, string> Familiar = new Dictionary<string, string>
        {
            { "meadow-vole", Plates + "Familiars/familiar-vole" },
            { "red-squirrel", Plates + "Familiars/familiar-squirrel" },
            { "sedge-linnet", Plates + "Familiars/familiar-songbird" },
            { "bramble-hare", Plates + "Familiars/familiar-hare" },
            { "warren-weasel", Plates + "Familiars/familiar-weasel" },
            { "furrow-hedgehog", Plates + "Familiars/familiar-hedgehog" },
            { "tawny-owl", Plates + "Familiars/familiar-owl" },
            { "osier-otter", Plates + "Familiars/familiar-otter" },
            // A long-eared bat stands in for the horseshoe — the roosting pose is
            // the one the inscription describes, and the coloured plate cuts
            // where the horseshoe engravings (a dark roof scene) would not.
            { "horseshoe-bat", Plates + "Familiars/familiar-bat" },
            // Mustela erminea, drawn in its summer coat as the plate has it; the
            // warden's is the winter animal the inscriptions describe.
            { "ermine", Plates + "Familiars/familiar-ermine" },
            // Keulemans' Buller plate — wings up over the scree, and a sheep
            // already fleeing in the background, which is the whole trait.
            { "kea", Plates + "Familiars/familiar-kea" },
            { "pika", Plates + "Familiars/familiar-pika" },
            { "pack-raven", Plates + "Familiars/familiar-raven" },
            // The Drover's Halter pony (§11). A head-and-neck portrait rather
            // than the whole animal — the plate's lower legs stand against dark
            // grass and would not cut, and the halter is a head thing anyway.
            { "fell-pony", Plates + "Familiars/familiar-pony" },
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
            { "oilskin-tarp", Plates + "Gear/gear-tarp" },
        };

        // insect id (insects.json) → deep-page plate
        private static readonly Dictionary<string, string> Insect = new Dictionary<string, string>
        {
            { "stags-herald", Plates + "Insects/insect-stags-herald" },
            { "silver-skimmer", Plates + "Insects/insect-silver-skimmer" },
            { "those-who-sow", Plates + "Insects/insect-those-who-sow" },
            // Both sexes of the glow-worm on one page — the winged male and the
            // wingless female who carries the light. "Bearers" is plural.
            { "lantern-bearers", Plates + "Insects/insect-lantern-bearers" },
            { "quiet-court", Plates + "Insects/insect-quiet-court" },
            // Parnassius apollo — the mountain white whose worn wings scale
            // like the pages it is drawn on.
            { "parchment-wings", Plates + "Insects/insect-parchment-wings" },
            // An alpine butterfly for the summit's blown-in insects — the plate
            // is one specimen, though the name is plural, because what reaches
            // the peaks arrives one at a time.
            { "windborne", Plates + "Insects/insect-windborne" },
            { "wayfarers-plate", Plates + "Insects/insect-wayfarers-plate" },
        };

        // manufactured line-art motifs the HUD places by a plain name — the PSF
        // ink drawings that aren't a single good/gear id: the trellis over every
        // planter, the seedling on Plant back, the hatchet heading the Ladder.
        private static readonly Dictionary<string, string> Line = new Dictionary<string, string>
        {
            { "planter", Plates + "Goods/goods-trellis" },
            { "seedling", Plates + "Goods/goods-seedling" },
            { "tools", Plates + "Goods/goods-tools" },
            // The deep amber's own plate — a fly held in the resin, which is the
            // whole of what the set is about. Keyed here rather than in Insect
            // because the four pieces are ambers.json, not an insect id.
            { "deep-amber", Plates + "Insects/insect-deep-amber" },
        };

        // journal furniture — chrome, keyed by a plain name
        private static readonly Dictionary<string, string> Journal = new Dictionary<string, string>
        {
            { "paper", Ui + "Journal/ui-paper-texture" },
            { "cairn", Ui + "Journal/ui-cairn" },
            { "caravan", Ui + "Journal/ui-caravan" },
            { "waystone", Ui + "Journal/ui-waystone" },
            { "almanac", Ui + "Journal/ui-almanac-tree" },

            // The Amber pile's own plate, for the events rail's cache cell.
            // Keyed here rather than in Resource because Amber is a currency
            // and has no resource id to look it up by, and NOT reusing Line's
            // "deep-amber" (a fly held in the resin): that is the collectible
            // set on the Record's deep pages, and the two must not read as
            // the same thing on a page where one of them is money.
            { "amber", Plates + "Resources/res-amber" },

            // The Wheel's plates (design §15) — the warden's own almanac
            // marks, not the land's naturalist pages: the calendar is the
            // warden's, so the mark is an almanac ornament in the warden's
            // ink. Authored originals (tools/make-sabbat-plates provenance in
            // repo history) — no licence owed, and a painted plate can land
            // over the same id any time.
            { "sabbat-samhain", Plates + "Wheel/sabbat-samhain" },
            { "sabbat-yule", Plates + "Wheel/sabbat-yule" },
            { "sabbat-imbolc", Plates + "Wheel/sabbat-imbolc" },
            { "sabbat-ostara", Plates + "Wheel/sabbat-ostara" },
            { "sabbat-beltane", Plates + "Wheel/sabbat-beltane" },
            { "sabbat-litha", Plates + "Wheel/sabbat-litha" },
            { "sabbat-lughnasadh", Plates + "Wheel/sabbat-lughnasadh" },
            { "sabbat-mabon", Plates + "Wheel/sabbat-mabon" },
        };

        // The warden's own mark. Keyed off nothing because there is exactly one
        // warden — a map of one id would only invite a second.
        private const string WardenMark = Ui + "ui-warden";

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

        /// <summary>The warden's mark — the player's own figure, worn on the badge of whichever post they stand at. Null when the plate is missing.</summary>
        public static Sprite ForWarden() => Load(WardenMark);

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
