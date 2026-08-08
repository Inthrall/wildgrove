using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;

namespace Wildgrove.Game.Tests
{
    /// <summary>
    /// Every id the shipped data holds has a plate behind it. A missing one is
    /// silent by design — <see cref="ArtLibrary"/> returns null and the
    /// placeholder disc stands in — so nothing else in the game would ever say
    /// that a plate had been mistyped, moved out of Resources, or left behind
    /// when content was added.
    ///
    /// These walk the real GameData asset rather than a hand-built one: it is the
    /// content the build ships, and the point is coverage of that content.
    /// </summary>
    public class ArtLibraryTests
    {
        private GameDataAsset _data;

        [SetUp]
        public void SetUp()
        {
            _data = GameDataAsset.LoadFromResources();
            if (_data == null)
            {
                Assert.Ignore("GameData.asset is not built — run Wildgrove > Import Design Data.");
            }
        }

        [Test]
        public void EveryResource_HasAPlate()
        {
            var missing = new List<string>();
            foreach (var resource in _data.resources)
            {
                if (ArtLibrary.ForResource(resource.id) == null)
                {
                    missing.Add(resource.id);
                }
            }

            Assert.That(missing, Is.Empty, "resources with no plate: " + string.Join(", ", missing));
        }

        [Test]
        public void EveryRecipeOutput_HasAPlate()
        {
            // Crafted goods fall back to the raw plate of the same id, so this
            // covers the material outputs too.
            var missing = new List<string>();
            foreach (var recipe in _data.recipes)
            {
                if (ArtLibrary.ForGood(recipe.output) == null)
                {
                    missing.Add(recipe.output);
                }
            }

            Assert.That(missing, Is.Empty, "crafted goods with no plate: " + string.Join(", ", missing));
        }

        [Test]
        public void EverySpecies_HasARosterPlate()
        {
            var missing = new List<string>();
            foreach (var species in _data.species)
            {
                if (ArtLibrary.ForSpecies(species.id) == null)
                {
                    missing.Add(species.id);
                }
            }

            Assert.That(missing, Is.Empty, "species with no portrait: " + string.Join(", ", missing));
        }

        [Test]
        public void EveryInsect_HasADeepPagePlate()
        {
            var missing = new List<string>();
            foreach (var insect in _data.insects)
            {
                if (ArtLibrary.ForInsect(insect.id) == null)
                {
                    missing.Add(insect.id);
                }
            }

            Assert.That(missing, Is.Empty, "insects with no plate: " + string.Join(", ", missing));
        }

        [Test]
        public void EveryZoneGearAndBuilding_HasAPlate()
        {
            var missing = new List<string>();
            foreach (var zone in _data.zones)
            {
                if (ArtLibrary.ForZone(zone.id) == null)
                {
                    missing.Add("zone " + zone.id);
                }
            }

            foreach (var gear in _data.gear)
            {
                if (ArtLibrary.ForGear(gear.id) == null)
                {
                    missing.Add("gear " + gear.id);
                }
            }

            foreach (var building in _data.buildings)
            {
                if (ArtLibrary.ForBuilding(building.id) == null)
                {
                    missing.Add("building " + building.id);
                }
            }

            Assert.That(missing, Is.Empty, "no plate for: " + string.Join(", ", missing));
        }

        [Test]
        public void EverySkillNamedByTheData_HasACraftGlyph()
        {
            var skills = new SortedSet<string>();
            foreach (var resource in _data.resources)
            {
                if (!string.IsNullOrEmpty(resource.skill))
                {
                    skills.Add(resource.skill);
                }
            }

            foreach (var recipe in _data.recipes)
            {
                if (!string.IsNullOrEmpty(recipe.skill))
                {
                    skills.Add(recipe.skill);
                }
            }

            var missing = new List<string>();
            foreach (var skill in skills)
            {
                if (ArtLibrary.ForSkill(skill) == null)
                {
                    missing.Add(skill);
                }
            }

            Assert.That(missing, Is.Empty, "skills with no glyph: " + string.Join(", ", missing));
        }

        [Test]
        public void EveryJournalFurnishingAndLineMotif_LoadsFromResources()
        {
            // Keyed by plain name rather than a data id, so nothing but this
            // would notice a renamed file.
            foreach (var name in new[] { "paper", "cairn", "caravan", "waystone", "almanac" })
            {
                Assert.That(ArtLibrary.ForJournal(name), Is.Not.Null, "journal furnishing " + name);
            }

            foreach (var name in new[] { "planter", "seedling", "tools", "deep-amber" })
            {
                Assert.That(ArtLibrary.ForLine(name), Is.Not.Null, "line motif " + name);
            }
        }

        [Test]
        public void EverySabbat_HasItsOwnPlate()
        {
            // The keeping card, the tier sheet and the Record shelf all ask by
            // "sabbat-{id}" (design §15) — and each mark must be its own: a
            // mis-keyed plate still loads a plate, so distinctness is the pin.
            var seen = new System.Collections.Generic.HashSet<UnityEngine.Sprite>();
            foreach (var id in new[]
                     {
                         "sabbat-samhain", "sabbat-yule", "sabbat-imbolc", "sabbat-ostara",
                         "sabbat-beltane", "sabbat-litha", "sabbat-lughnasadh", "sabbat-mabon",
                     })
            {
                var plate = ArtLibrary.ForJournal(id);
                Assert.That(plate, Is.Not.Null, "sabbat plate " + id);
                Assert.That(seen.Add(plate), Is.True, id + " must be its own plate, not a neighbour's");
            }
        }

        [Test]
        public void TheWarden_HasTheirOwnMark()
        {
            // Keyed off no data id at all — there is one warden — so nothing in
            // the game would report the file missing. The badge falls back to
            // the placeholder triangle it wore before the plate landed, which
            // is a silent regression rather than a visible one.
            var plate = UnityEngine.Resources.Load<Sprite>("Art/UI/ui-warden");

            Assert.That(plate, Is.Not.Null, "no plate at Art/UI/ui-warden");
            Assert.That(ArtLibrary.ForWarden(), Is.SameAs(plate));
        }

        [Test]
        public void EveryLineMotif_DrawsItsOwnPlateRatherThanABorrowedOne()
        {
            // Loading is not enough for these four. A key pointed at the wrong
            // file still returns a sprite, and the page draws a plausible
            // picture that nothing would ever question — which is how the deep
            // amber shipped on the ordinary amber's photograph while its own
            // plate sat in the build, already good enough for the store card.
            var expected = new Dictionary<string, string>
            {
                { "planter", "Art/Plates/Goods/goods-trellis" },
                { "seedling", "Art/Plates/Goods/goods-seedling" },
                { "tools", "Art/Plates/Goods/goods-tools" },
                { "deep-amber", "Art/Plates/Insects/insect-deep-amber" },
            };

            foreach (var pair in expected)
            {
                var plate = UnityEngine.Resources.Load<Sprite>(pair.Value);
                Assert.That(plate, Is.Not.Null, "no plate at " + pair.Value);
                Assert.That(ArtLibrary.ForLine(pair.Key), Is.SameAs(plate),
                    "line motif " + pair.Key + " does not draw " + pair.Value);
            }
        }

        [Test]
        public void AnUnknownId_ReturnsNullRatherThanThrowing()
        {
            // The null path is the designed fallback — callers keep the
            // placeholder disc — so it must survive nonsense and nothing.
            Assert.That(ArtLibrary.ForResource("no-such-resource"), Is.Null);
            Assert.That(ArtLibrary.ForSpecies(null), Is.Null);
            Assert.That(ArtLibrary.ForGood(string.Empty), Is.Null);
            Assert.That(ArtLibrary.ForJournal("no-such-furnishing"), Is.Null);
        }
    }
}
