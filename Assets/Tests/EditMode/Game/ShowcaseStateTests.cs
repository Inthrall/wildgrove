using System.IO;
using System.Linq;
using NUnit.Framework;
using Wildgrove.Data;
using Wildgrove.Sim;
using Wildgrove.Sim.Saves;

namespace Wildgrove.Game.Tests
{
    /// <summary>
    /// The camp the store-screenshot harness stages, against the real content
    /// data — because a listing shot is published, and the harness's own
    /// output (a .png) says nothing about whether what it photographed was the
    /// camp it staged. Two things were wrong here at once and neither made a
    /// sound: a Kinship LEVEL of 4200 on the drawer's first plate, and a
    /// captured page that read like an early run rather than the showcase.
    /// <para>
    /// The second is what the round-trip test is for. It walks the same path a
    /// launch does — stage, capture, write the slot, load it, restore — so a
    /// showcase that arrives thinner than it left has somewhere to fail
    /// loudly, in a suite, rather than in a published screenshot nobody
    /// re-counts.
    /// </para>
    /// </summary>
    public class ShowcaseStateTests
    {
        private GameDataAsset _data;
        private string _scratch;

        [SetUp]
        public void SetUp()
        {
            _data = GameDataAsset.LoadFromResources();
            _scratch = Path.Combine(Path.GetTempPath(), "wildgrove-showcase-tests", Path.GetRandomFileName());
            Directory.CreateDirectory(_scratch);
            // The showcase writes a save slot; never the editor's own, which
            // holds the developer's run (see SaveFileTests).
            SaveFile.DirectoryOverride = _scratch;
        }

        [TearDown]
        public void TearDown()
        {
            SaveFile.DirectoryOverride = null;
            if (Directory.Exists(_scratch))
            {
                Directory.Delete(_scratch, true);
            }
        }

        [Test]
        public void Stage_OpensTheWholeLadderAndFillsIt()
        {
            var state = ShowcaseState.Stage(_data);

            // Six: the ladder's two recruit rungs plus the four staged. The
            // point of the showcase is a FULL drawer — the fault it hides
            // otherwise is a photograph of two companions on one slot.
            Assert.That(state.roster, Has.Count.EqualTo(6),
                "the showcase stages four companions on top of the ladder's vole and raven");
            Assert.That(Kith.Slots(state, _data), Is.EqualTo(Kith.SlotsMax(_data)),
                "ten verses sung plus both purchased slots is the whole ladder");
            Assert.That(Kith.Walking(state), Is.GreaterThanOrEqualTo(4),
                "the four staged companions hold posts; a drawer of resting bodies photographs as an idle camp");
        }

        [Test]
        public void Stage_PostsOnlyGroundsAndSitesTheLadderOpened()
        {
            var state = ShowcaseState.Stage(_data);

            // Every staged post has to be one this run actually holds: SaveCodec
            // rests a body whose station resolves to nothing, so a post the
            // ladder never opened puts a companion at camp in the photograph and
            // says nothing about it.
            foreach (var familiar in state.roster)
            {
                var watchZone = Familiar.SketchZoneOf(familiar.stationId);
                if (watchZone != null)
                {
                    Assert.That(state.digSites.Any(site => site.zoneId == watchZone), Is.True,
                        "a watch at a site this run never opened is a companion resting in the listing shot");
                    continue;
                }

                Assert.That(familiar.IsResting || state.nodes.Any(node => node.id == familiar.stationId), Is.True,
                    familiar.speciesId + " stands at " + familiar.stationId + ", which is no ground this run holds");
            }

            Assert.That(state.roster.Any(familiar => familiar.IsSketching), Is.True,
                "and the watch itself is staged — an unwatched map is a photograph of unfinished work");
        }

        [Test]
        public void Stage_GivesEverySpeciesItsOwnCompanion()
        {
            var state = ShowcaseState.Stage(_data);

            // One familiar per species, ever (design §4). A duplicate would be
            // deduped away on the next restore — silently, and only in the
            // shot.
            Assert.That(state.roster.Select(familiar => familiar.speciesId).Distinct().Count(),
                Is.EqualTo(state.roster.Count), "the collection holds one familiar per species");
        }

        [Test]
        public void Stage_KinshipIsALevelACompanionCouldHold()
        {
            var state = ShowcaseState.Stage(_data);

            // kinshipXp stores the LEVEL. Every value here has to read as one
            // on a plate: 4200 rendered as "KINSHIP MMMMCC" in the listing.
            foreach (var familiar in state.roster)
            {
                Assert.That(Kinship.Level(familiar), Is.InRange(0, 20),
                    "a staged Kinship must be a level, not run XP: " + familiar.speciesId);
            }
        }

        [Test]
        public void Stage_ThroughTheSaveSlot_ArrivesAsTheCampItStaged()
        {
            var staged = ShowcaseState.Stage(_data);
            var stagedWalking = Kith.Walking(staged);

            // The launch path, whole: capture, write the slot, read it back,
            // restore. Anything the save cannot carry drops out here.
            SaveFile.Write(SaveCodec.Capture(staged, 1_700_000_000_000L));
            Assert.That(SaveFile.TryLoad(out var save), Is.True, "the staged slot must read back");
            var restored = SaveCodec.Restore(save, _data);

            Assert.That(restored.roster, Has.Count.EqualTo(staged.roster.Count),
                "every companion staged is a companion photographed");
            Assert.That(restored.foldedVersesSung, Is.EqualTo(staged.foldedVersesSung));
            Assert.That(restored.purchasedKithSlots, Is.EqualTo(staged.purchasedKithSlots));
            Assert.That(Kith.Slots(restored, _data), Is.EqualTo(Kith.Slots(staged, _data)),
                "the ladder the showcase opened is the ladder the page reads");
            Assert.That(Kith.Walking(restored), Is.EqualTo(stagedWalking),
                "a restore rests anyone past the slots — with the ladder open, nobody is");
            Assert.That(Kinship.Level(restored.roster[0]), Is.EqualTo(Kinship.Level(staged.roster[0])));
        }
    }
}
