using System.Collections.Generic;
using NUnit.Framework;
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
