using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wildgrove.Sim.Saves;

namespace Wildgrove.Game.Tests
{
    /// <summary>
    /// The disk half of the save, which <see cref="RunPersistence"/>'s fake
    /// store deliberately doesn't reach: the atomic replace, and the two
    /// set-aside slots a file lands in when it can no longer be read.
    /// <para>
    /// Every rule here exists because of something that happened on a device —
    /// the crash loop on an unreadable save, and a healthy save from a newer
    /// build being destroyed by the build that couldn't read it — and none of
    /// it is reachable without a real file. So these run against a scratch
    /// directory rather than the editor's own persistent path, which holds the
    /// developer's actual run.
    /// </para>
    /// </summary>
    public class SaveFileTests
    {
        private string _scratch;

        [SetUp]
        public void SetUp()
        {
            _scratch = Path.Combine(Path.GetTempPath(), "wildgrove-savefile-tests", Path.GetRandomFileName());
            Directory.CreateDirectory(_scratch);
            SaveFile.DirectoryOverride = _scratch;
        }

        [TearDown]
        public void TearDown()
        {
            // Null is the shipping value, not merely the one this fixture found:
            // nothing else in the project ever sets it. Restoring matters even
            // though the EditMode runner is single-threaded — a later fixture
            // that touched SaveFile would otherwise inherit a deleted scratch
            // directory as its save slot.
            SaveFile.DirectoryOverride = null;
            if (Directory.Exists(_scratch))
            {
                Directory.Delete(_scratch, true);
            }
        }

        private static SaveData Run(int version, long savedAtUnixMs, int migrations)
        {
            return new SaveData { version = version, savedAtUnixMs = savedAtUnixMs, migrationCount = migrations };
        }

        [Test]
        public void TryLoad_WithNothingWritten_IsFalseAndHandsBackNoRun()
        {
            Assert.That(SaveFile.TryLoad(out var save), Is.False);
            Assert.That(save, Is.Null);
        }

        [Test]
        public void Write_ThenTryLoad_ReturnsTheRunThatWasWritten()
        {
            SaveFile.Write(Run(SaveCodec.CurrentVersion, 1_700_000_000_000L, 3));

            Assert.That(SaveFile.TryLoad(out var save), Is.True);
            Assert.That(save, Is.Not.Null);
            Assert.That(save.version, Is.EqualTo(SaveCodec.CurrentVersion));
            Assert.That(save.savedAtUnixMs, Is.EqualTo(1_700_000_000_000L));
            Assert.That(save.migrationCount, Is.EqualTo(3));
        }

        [Test]
        public void Write_OverAnExistingSave_ReplacesItAndLeavesNoTempBehind()
        {
            // The second write takes the File.Replace branch rather than the
            // Move one — the path an autosave takes every thirty seconds for
            // the whole life of a run, and the only one the first write never
            // exercises.
            SaveFile.Write(Run(SaveCodec.CurrentVersion, 1L, 1));
            SaveFile.Write(Run(SaveCodec.CurrentVersion, 2L, 2));

            Assert.That(SaveFile.TryLoad(out var save), Is.True);
            Assert.That(save.migrationCount, Is.EqualTo(2));
            Assert.That(File.Exists(SaveFile.Path + ".tmp"), Is.False, "the temp file must not survive the swap");
        }

        [Test]
        public void TryLoad_WithAnUnreadableFile_SetsItAsideRatherThanLeavingItToFailAgain()
        {
            // Leaving it in place is the crash loop: every launch reads the
            // same bad bytes. It has to go somewhere the next launch won't look.
            File.WriteAllText(SaveFile.Path, "this is not a save");
            LogAssert.Expect(LogType.Error, new Regex(@"unreadable.*set aside as .*\.corrupt"));

            Assert.That(SaveFile.TryLoad(out var save), Is.False);
            Assert.That(save, Is.Null);
            Assert.That(File.Exists(SaveFile.Path), Is.False, "the bad slot must not be read again");
            Assert.That(File.Exists(SaveFile.Path + ".corrupt"), Is.True);
            Assert.That(File.ReadAllText(SaveFile.Path + ".corrupt"), Is.EqualTo("this is not a save"),
                "set aside means kept, not truncated");
        }

        [Test]
        public void TryLoad_WithASaveFromANewerBuild_ParksItWholeAndCallsItNewerNotCorrupt()
        {
            // An APK rollback or a staged-rollout downgrade: healthy data this
            // build simply can't read. Filing it as corrupt would be a lie, and
            // would let a later genuine corruption overwrite the only copy of a
            // run that is still perfectly good.
            var json = SaveCodec.ToJson(Run(SaveCodec.CurrentVersion + 1, 5L, 9));
            File.WriteAllText(SaveFile.Path, json);
            LogAssert.Expect(LogType.Error, new Regex(@"newer build.*set aside as .*\.newer"));

            Assert.That(SaveFile.TryLoad(out var save), Is.False);
            Assert.That(save, Is.Null);
            Assert.That(File.Exists(SaveFile.Path + ".newer"), Is.True);
            Assert.That(File.Exists(SaveFile.Path + ".corrupt"), Is.False);
            Assert.That(File.ReadAllText(SaveFile.Path + ".newer"), Is.EqualTo(json),
                "re-upgrading recovers this by hand, so it must be intact");
        }

        [Test]
        public void TryLoad_WithAnUnreadableFile_LeavesAlreadySetAsideNewerDataAlone()
        {
            // The two slots are separate for this exact sequence: downgrade,
            // play, then corrupt. One set-aside must not consume the other.
            var newer = SaveCodec.ToJson(Run(SaveCodec.CurrentVersion + 1, 5L, 9));
            File.WriteAllText(SaveFile.Path + ".newer", newer);
            File.WriteAllText(SaveFile.Path, "rubbish");
            LogAssert.Expect(LogType.Error, new Regex(@"set aside as .*\.corrupt"));

            Assert.That(SaveFile.TryLoad(out _), Is.False);
            Assert.That(File.ReadAllText(SaveFile.Path + ".newer"), Is.EqualTo(newer));
        }

        [Test]
        public void Write_WhenTheSlotCannotBeWritten_SaysSoWithoutTakingTheSessionDown()
        {
            // A failed autosave is a logged error and nothing else — the next
            // interval retries. Throwing here would surface as the game dying
            // every thirty seconds on a device with a full or locked disk.
            SaveFile.DirectoryOverride = Path.Combine(_scratch, "no-such-directory");
            LogAssert.Expect(LogType.Error, new Regex("Save write failed"));

            Assert.DoesNotThrow(() => SaveFile.Write(Run(SaveCodec.CurrentVersion, 1L, 1)));
        }
    }
}
