using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Wildgrove.Data;

namespace Wildgrove.Sim.Tests
{
    /// <summary>
    /// Pins the run-2+ Rite generator (design §7/§8): deterministic per
    /// migration, demand scaling by demandGrowth^m, the rotating spotlight,
    /// zone-order gating of what a verse may ask, material grants, and the
    /// authored-template slot shape. The real-data tests are the "spreadsheet
    /// proof" the design doc demands — runs 2–10 generated from the shipping
    /// JSON must always leave at least chooseCount reachable slots per verse.
    /// </summary>
    public class RiteGeneratorTests
    {
        private GameDataAsset _data;

        [SetUp]
        public void SetUp()
        {
            _data = ScriptableObject.CreateInstance<GameDataAsset>();
            _data.economy = new EconomyData
            {
                mastery = new EconomyData.MasteryData { yieldBonusPerLevel = 0.05 },
                verdure = new EconomyData.VerdureData { renownDivisor = 5000, exponent = 0.5, yieldBonusPerPoint = 0.02 },
                offline = new EconomyData.OfflineData { baseCapHours = 4, rateMultiplier = 1.0 },
            };
            _data.resources = new List<ResourceData>
            {
                new ResourceData { id = "berries", sellValue = 2, skill = "foraging" },
                new ResourceData { id = "nuts", sellValue = 3, skill = "foraging" },
                new ResourceData { id = "copper", sellValue = 2, skill = "mining" },
            };
            _data.zones = new List<ZoneData>
            {
                new ZoneData
                {
                    id = GameStateFactory.StartingZoneId,
                    order = 1,
                    resources = new List<string> { "berries", "nuts" },
                    unlocks = new List<string> { "foraging" },
                },
                new ZoneData
                {
                    id = "bramble",
                    order = 2,
                    resources = new List<string> { "copper" },
                    unlocks = new List<string> { "mining", "smithing" },
                },
            };
            _data.recipes = new List<RecipeData>
            {
                new RecipeData
                {
                    id = "copper-ingot", output = "copper-ingot", kind = "material", skill = "smithing",
                    valueMult = 3, defaultKnown = true,
                    inputs = new List<ItemAmount> { new ItemAmount { id = "copper", amount = 5 } },
                },
            };
            _data.rites = new RitesBundle
            {
                chooseCount = 2,
                generator = new RiteGeneratorConfigData
                {
                    demandGrowth = 2.0,
                    spotlightDiscount = 0.5,
                    offSpotlightPremium = 1.5,
                },
                rites = new List<RiteData>
                {
                    new RiteData
                    {
                        id = "first-rite",
                        migration = 0,
                        verses =
                        {
                            new RiteVerseData
                            {
                                id = "verse-sunfield",
                                zone = GameStateFactory.StartingZoneId,
                                spotlight = new List<string> { "foraging" },
                                slots =
                                {
                                    new RiteSlotData { type = RiteSlotType.Resource, resource = "berries", amount = 100 },
                                    new RiteSlotData { type = RiteSlotType.Resource, resource = "nuts", amount = 50 },
                                    new RiteSlotData { type = RiteSlotType.Deed, deed = "tend", count = 10, renownGrant = 40 },
                                    new RiteSlotData { type = RiteSlotType.Specimen, quality = "fine", count = 1, renownGrant = 80 },
                                },
                            },
                            new RiteVerseData
                            {
                                id = "verse-bramble",
                                zone = "bramble",
                                spotlight = new List<string> { "mining" },
                                slots =
                                {
                                    new RiteSlotData { type = RiteSlotType.Resource, resource = "copper", amount = 200 },
                                    new RiteSlotData { type = RiteSlotType.Resource, resource = "copper-ingot", amount = 5, renownGrant = 150 },
                                    new RiteSlotData { type = RiteSlotType.Sketch, count = 1, renownGrant = 100 },
                                },
                            },
                        },
                    },
                },
            };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_data);
        }

        [Test]
        public void Generate_IsDeterministic()
        {
            var first = RiteGenerator.Generate(_data, 3);
            var second = RiteGenerator.Generate(_data, 3);

            Assert.That(second.verses.Count, Is.EqualTo(first.verses.Count));
            for (var v = 0; v < first.verses.Count; v++)
            {
                var a = first.verses[v];
                var b = second.verses[v];
                Assert.That(b.spotlight, Is.EqualTo(a.spotlight));
                Assert.That(b.slots.Count, Is.EqualTo(a.slots.Count));
                for (var s = 0; s < a.slots.Count; s++)
                {
                    Assert.That(b.slots[s].type, Is.EqualTo(a.slots[s].type));
                    Assert.That(b.slots[s].resource, Is.EqualTo(a.slots[s].resource));
                    Assert.That(b.slots[s].amount, Is.EqualTo(a.slots[s].amount));
                    Assert.That(b.slots[s].renownGrant, Is.EqualTo(a.slots[s].renownGrant));
                }
            }
        }

        [Test]
        public void Generate_KeepsTheTemplateSlotShape()
        {
            var rite = RiteGenerator.Generate(_data, 1);

            Assert.That(rite.migration, Is.EqualTo(1));
            Assert.That(rite.verses.Count, Is.EqualTo(2));
            Assert.That(rite.verses[0].zone, Is.EqualTo(GameStateFactory.StartingZoneId));
            Assert.That(rite.verses[0].slots.Select(s => s.type), Is.EqualTo(new[]
            {
                RiteSlotType.Resource, RiteSlotType.Resource, RiteSlotType.Deed, RiteSlotType.Specimen,
            }));
            Assert.That(rite.verses[1].slots.Select(s => s.type), Is.EqualTo(new[]
            {
                RiteSlotType.Resource, RiteSlotType.Resource, RiteSlotType.Sketch,
            }));
        }

        [Test]
        public void Generate_DeepMigrationCounts_SaturateInsteadOfOverflowingNegative()
        {
            // demandGrowth^500 is far past long range — the unchecked cast
            // would land on long.MinValue and a completed slot would SUBTRACT
            // ~9.2e18 renown. Saturation keeps every demand and grant sane.
            var rite = RiteGenerator.Generate(_data, 500);

            foreach (var slot in rite.verses.SelectMany(v => v.slots))
            {
                Assert.That(slot.renownGrant, Is.GreaterThanOrEqualTo(0L),
                    $"slot {slot.type}/{slot.resource} grant overflowed negative");
                if (slot.type == RiteSlotType.Resource)
                {
                    Assert.That(slot.amount, Is.GreaterThanOrEqualTo(1L));
                }
            }
        }

        [Test]
        public void Generate_DemandScalesByGrowthPerMigration()
        {
            // Verse 1's candidates are all foraging, so both goods slots sit
            // in the spotlight at every migration — pricing is stable and the
            // only variable between runs is the d^m demand scale.
            var run2 = RiteGenerator.Generate(_data, 1);
            var run3 = RiteGenerator.Generate(_data, 2);

            foreach (var slot in run2.verses[0].slots.Where(s => s.type == RiteSlotType.Resource))
            {
                var later = run3.verses[0].slots.Single(s => s.resource == slot.resource);
                Assert.That(later.amount / (double)slot.amount, Is.EqualTo(2.0).Within(0.05),
                    $"'{slot.resource}' should ask ~2x more each migration");
            }
        }

        [Test]
        public void Generate_ZoneOrderGatesWhatAVerseMayAsk()
        {
            var rite = RiteGenerator.Generate(_data, 1);

            var verse1Goods = rite.verses[0].slots
                .Where(s => s.type == RiteSlotType.Resource)
                .Select(s => s.resource)
                .ToList();

            Assert.That(verse1Goods, Is.EquivalentTo(new[] { "berries", "nuts" }),
                "the starting verse must stay answerable from its own zone's raw finds");
        }

        [Test]
        public void Generate_SpotlightRotatesWithMigration()
        {
            var run2 = RiteGenerator.Generate(_data, 1);
            var run3 = RiteGenerator.Generate(_data, 2);

            // Verse 2's pool is [mining, smithing]: the one-skill spotlight
            // window slides one step per migration.
            Assert.That(run2.verses[1].spotlight, Is.EqualTo(new[] { "smithing" }));
            Assert.That(run3.verses[1].spotlight, Is.EqualTo(new[] { "mining" }));
        }

        [Test]
        public void Generate_MaterialPicksCarryARenownGrant()
        {
            var rite = RiteGenerator.Generate(_data, 1);

            var ingot = rite.verses[1].slots.Single(s => s.resource == "copper-ingot");

            // Materials trade at zero — without a grant the slot would tax
            // prestige. The grant equals the slot's notional worth.
            var notionalUnit = Economy.NotionalUnitValue(_data, "copper-ingot").ToDouble();
            Assert.That(ingot.renownGrant, Is.EqualTo((long)System.Math.Round(ingot.amount * notionalUnit)));
            Assert.That(ingot.renownGrant, Is.GreaterThan(0));
        }

        [Test]
        public void Generate_SpecialSlots_KeepCountsAndScaleGrants()
        {
            var rite = RiteGenerator.Generate(_data, 1);

            var deed = rite.verses[0].slots.Single(s => s.type == RiteSlotType.Deed);
            var specimen = rite.verses[0].slots.Single(s => s.type == RiteSlotType.Specimen);
            var fragment = rite.verses[1].slots.Single(s => s.type == RiteSlotType.Sketch);

            Assert.That(deed.deed, Is.EqualTo("tend"));
            Assert.That(deed.count, Is.EqualTo(10), "deeds price in taps, not goods — the count stays authored");
            Assert.That(deed.renownGrant, Is.EqualTo(80), "40 x 2^1");
            Assert.That(specimen.quality, Is.EqualTo("fine"));
            Assert.That(specimen.count, Is.EqualTo(1));
            Assert.That(specimen.renownGrant, Is.EqualTo(160));
            Assert.That(fragment.count, Is.EqualTo(1));
            Assert.That(fragment.renownGrant, Is.EqualTo(200));
        }

        [Test]
        public void CurrentRite_AuthoredForItsMigration_GeneratedBeyond()
        {
            var state = GameStateFactory.NewGame(_data);

            Assert.That(Rite.CurrentRite(state, _data), Is.SameAs(_data.rites.rites[0]),
                "run 1 walks the authored rite");

            state.migrationCount = 1;
            var generated = Rite.CurrentRite(state, _data);
            Assert.That(generated.id, Is.EqualTo("rite-m1"));
            Assert.That(generated.migration, Is.EqualTo(1));
            Assert.That(Rite.CurrentRite(state, _data), Is.SameAs(generated), "regeneration is memoised");
        }

        [Test]
        public void CurrentRite_WithoutGeneratorTuning_RewalksTheAuthoredRite()
        {
            _data.rites.generator = null;
            var state = GameStateFactory.NewGame(_data);
            state.migrationCount = 1;

            Assert.That(Rite.CurrentRite(state, _data), Is.SameAs(_data.rites.rites[0]));
        }

        [Test]
        public void RecordDeed_FillsAGeneratedDeedSlot()
        {
            var state = GameStateFactory.NewGame(_data);
            state.migrationCount = 1;
            var verse = Rite.CurrentRite(state, _data).verses[0];

            for (var i = 0; i < 10; i++)
            {
                Rite.RecordDeed(state, _data, "tend");
            }

            Assert.That(Rite.IsSlotComplete(state, verse, 2), Is.True);
            Assert.That(state.renown.ToDouble(), Is.EqualTo(80.0).Within(1e-9), "the scaled grant lands once");
        }

        // ---- The real-data proof (design §12: "verify runs 2+ before wiring
        // UI") — generated Rites from the shipping JSON must never be able to
        // hard-stick a run.

        private static GameDataAsset LoadRealData()
        {
            var dataDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "design", "data"));
            var parsed = GameData.Parse(GameData.ReadSourcesFromFiles(dataDir));
            var asset = ScriptableObject.CreateInstance<GameDataAsset>();
            GameDataMapper.Populate(asset, parsed);
            return asset;
        }

        [Test]
        public void Generate_RealData_Runs2To10_EveryVerseKeepsThreeReachableSlots()
        {
            var data = LoadRealData();
            try
            {
                // What a run can ever offer: resources of unlockable zones,
                // plus the fixpoint of obtainable recipes over them.
                var unlockableZones = new HashSet<string> { GameStateFactory.StartingZoneId };
                var unlockedRecipes = new HashSet<string>();
                var grantsDigSite = false;
                foreach (var effect in data.upgrades.SelectMany(u => u.effects))
                {
                    if (effect.type == EffectType.UnlockZone)
                    {
                        unlockableZones.Add(effect.zone);
                    }

                    if (effect.type == EffectType.UnlockRecipe)
                    {
                        unlockedRecipes.Add(effect.recipe);
                    }

                    grantsDigSite |= effect.type == EffectType.UnlockDigSite;
                }

                var offerable = new HashSet<string>(data.zones
                    .Where(z => unlockableZones.Contains(z.id))
                    .SelectMany(z => z.resources));
                bool grew;
                do
                {
                    grew = false;
                    foreach (var recipe in data.recipes)
                    {
                        if (!offerable.Contains(recipe.output)
                            && (recipe.defaultKnown || unlockedRecipes.Contains(recipe.id))
                            && recipe.inputs.All(i => offerable.Contains(i.id)))
                        {
                            offerable.Add(recipe.output);
                            grew = true;
                        }
                    }
                }
                while (grew);

                for (var migration = 1; migration <= 10; migration++)
                {
                    var rite = RiteGenerator.Generate(data, migration);
                    Assert.That(rite, Is.Not.Null);

                    foreach (var verse in rite.verses)
                    {
                        var reachable = verse.slots.Count(slot =>
                            (slot.type == RiteSlotType.Resource && offerable.Contains(slot.resource) && slot.amount > 0)
                            || (slot.type == RiteSlotType.Deed && slot.deed == "tend")
                            || (slot.type == RiteSlotType.Specimen && SpecimenChance(data, slot.quality) > 0)
                            || (slot.type == RiteSlotType.Sketch && grantsDigSite && data.insects.Count > 0));

                        Assert.That(reachable, Is.GreaterThanOrEqualTo(data.rites.chooseCount),
                            $"migration {migration}, verse '{verse.id}': a generated Rite can never hard-stick a run");
                        Assert.That(verse.spotlight, Is.Not.Empty, $"verse '{verse.id}' spotlights nothing");

                        foreach (var slot in verse.slots.Where(s => s.type == RiteSlotType.Resource))
                        {
                            Assert.That(slot.amount, Is.GreaterThan(0), $"'{slot.resource}' asks for nothing");
                            if (!(Economy.TradeUnitValue(data, slot.resource) > BreakInfinity.BigDouble.Zero))
                            {
                                Assert.That(slot.renownGrant, Is.GreaterThan(0),
                                    $"material '{slot.resource}' must carry a grant or offering it taxes prestige");
                            }
                        }
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void Generate_RealData_StartingVerseAsksOnlyItsOwnRawFinds()
        {
            var data = LoadRealData();
            try
            {
                var sunfield = data.ZonesById[GameStateFactory.StartingZoneId];
                for (var migration = 1; migration <= 5; migration++)
                {
                    var verse = RiteGenerator.Generate(data, migration).verses[0];
                    foreach (var slot in verse.slots.Where(s => s.type == RiteSlotType.Resource))
                    {
                        Assert.That(sunfield.resources, Does.Contain(slot.resource),
                            "no crafted content exists by zone 1 — the first verse must stay raw-answerable");
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(data);
            }
        }

        private static double SpecimenChance(GameDataAsset data, string quality)
        {
            var q = data.economy?.quality;
            if (q == null)
            {
                return 0.0;
            }

            return quality == "pristine" ? q.pristineBaseChance : q.fineChance;
        }
    }
}
