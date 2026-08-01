using System;
using UnityEngine;
using Wildgrove.Data;
using Wildgrove.Game.Services;
using Wildgrove.Sim;
using Wildgrove.Sim.Saves;

namespace Wildgrove.Game
{
    /// <summary>
    /// Which save the game wakes from, when it is written, and whether a cloud
    /// save from another device is further along than the one in hand. This is
    /// device bookkeeping rather than game rule, but it decides which run the
    /// player keeps — so it sits behind the <see cref="IClock"/> and
    /// <see cref="ISaveStore"/> seams where it can be tested, instead of inside
    /// a MonoBehaviour's Awake where it could not.
    /// </summary>
    public sealed class RunPersistence
    {
        private readonly GameDataAsset _data;
        private readonly ISaveStore _store;
        private readonly IGameServices _services;
        private readonly IClock _clock;

        public RunPersistence(GameDataAsset data, ISaveStore store, IGameServices services, IClock clock)
        {
            _data = data;
            _store = store;
            _services = services;
            _clock = clock;
        }

        /// <summary>When the slot was last written — the baseline a pause→resume credits its absence from.</summary>
        public long LastSavedUnixMs { get; private set; }

        /// <summary>
        /// The accumulated play time of the newest save we hold (0 for a fresh
        /// run, so any cloud save is adopted — reinstall recovery). This is the
        /// bar a cloud save has to beat.
        /// </summary>
        public long LoadedPlayedMs { get; private set; }

        /// <summary>
        /// True when the last mirror to the cloud did not land. Reported on the
        /// inside cover: a run that is only on this device, while the player
        /// believes Play Games is holding it, is the failure worth saying out
        /// loud. False before the first write of a session.
        /// </summary>
        public bool LastCloudWriteFailed { get; private set; }

        /// <summary>True once a cloud run has been taken up in place of the local one this session.</summary>
        public bool AdoptedFromCloud { get; private set; }

        /// <summary>
        /// Set when the player deliberately starts the book again, and the only
        /// thing that can refuse an adoption. Sign-in — and so
        /// <see cref="Reconcile"/> — resolves seconds after launch, which is
        /// exactly when a fresh run has 0 played time and the old cloud save
        /// would win: without this, starting again could be undone by the
        /// device's own copy landing a moment later.
        /// </summary>
        public bool StartedOver { get; private set; }

        /// <summary>A run in hand, and how long it had been left when we picked it up.</summary>
        public sealed class Run
        {
            public GameState State;

            /// <summary>False for a fresh run — nothing was waiting, so nothing has been met, named or earned yet.</summary>
            public bool WasLoaded;

            /// <summary>Seconds since the save was written, for the offline catch-up. 0 on a fresh run.</summary>
            public double AwaySeconds;

            /// <summary>When the save was written, for the adoption telemetry.</summary>
            public long SavedAtUnixMs;
        }

        /// <summary>
        /// The run to start playing: the saved one where there is one, else a
        /// fresh camp. Both set the reconcile baseline, so the cloud pull that
        /// follows compares against what we actually hold.
        /// </summary>
        public Run Load()
        {
            Run run;
            if (_store.TryLoad(out var save))
            {
                run = new Run
                {
                    State = SaveCodec.Restore(save, _data),
                    WasLoaded = true,
                    AwaySeconds = (_clock.NowUnixMs() - save.savedAtUnixMs) / 1000.0,
                    SavedAtUnixMs = save.savedAtUnixMs,
                };
            }
            else
            {
                run = new Run { State = GameStateFactory.NewGame(_data) };
            }

            LoadedPlayedMs = run.State.playedMs;
            LastSavedUnixMs = _clock.NowUnixMs();
            return run;
        }

        /// <summary>
        /// Write the run to the device slot and mirror it to the cloud.
        /// </summary>
        public void Save(GameState state)
        {
            LastSavedUnixMs = _clock.NowUnixMs();
            // Advance the reconcile baseline too: cloud adoption compares against
            // the play time of the newest local save we hold, not the run we
            // launched with. Without this a cloud save from a stale device could
            // wrongly beat freshly-autosaved progress and overwrite it.
            LoadedPlayedMs = state.playedMs;
            var save = SaveCodec.Capture(state, LastSavedUnixMs);
            _store.Write(save);
            // Mirror to cloud; Reconcile pulls it back on the next signed-in
            // launch, adopting it when it is further along than the local slot. Play
            // time is also the snapshot's played-time for the Snapshots conflict tiebreak.
            _services.SaveCloud(SaveCodec.ToJson(save), state.playedMs,
                landed => LastCloudWriteFailed = !landed);
        }

        /// <summary>
        /// Wipe the run and begin a fresh one: the device slot is overwritten
        /// immediately and the cloud copy with it, so the book the player just
        /// closed cannot come back on the next launch. From here on this session
        /// refuses to adopt a cloud run (see <see cref="StartedOver"/>).
        /// </summary>
        public Run StartOver()
        {
            StartedOver = true;
            AdoptedFromCloud = false;
            var run = new Run { State = GameStateFactory.NewGame(_data) };
            // Written before anything else can: the fresh run has 0 played time,
            // which every other copy in existence beats.
            Save(run.State);
            return run;
        }

        /// <summary>
        /// Pull the cloud save and hand back a run to adopt when it beats what we
        /// hold. "Beats" is most-played-wins by accumulated play time: a further
        /// along run from another device — or the only save left after a
        /// reinstall, where the local run had no play time (0). A local run at
        /// least as far along is kept and <paramref name="onAdopt"/> never fires;
        /// the next save pushes it back up. Play time (not wall-clock) is the
        /// criterion so a wrong device clock can't win; the cross-device Snapshots
        /// conflict is resolved on the same basis earlier, by UseLongestPlaytime
        /// in <see cref="Services.PlayGamesServices"/>.
        /// </summary>
        public void Reconcile(Action<Run> onAdopt)
        {
            _services.LoadCloud(json =>
            {
                if (string.IsNullOrEmpty(json) || StartedOver)
                {
                    return;
                }

                SaveData cloud;
                try
                {
                    cloud = SaveCodec.FromJson(json);
                }
                catch (Exception e)
                {
                    // A cloud blob this build can't decode shouldn't disrupt the
                    // running local run — leave it, the next save overwrites it.
                    Debug.LogError("Cloud save decode failed: " + e.Message);
                    return;
                }

                // A null/corrupt or future-build cloud save is left untouched (and
                // not overwritten — the local save only uploads on top once it is
                // genuinely further along), mirroring SaveFile's set-aside policy.
                if (cloud == null || !SaveCodec.TryMigrate(cloud) || cloud.playedMs <= LoadedPlayedMs)
                {
                    return;
                }

                var adopted = new Run
                {
                    State = SaveCodec.Restore(cloud, _data),
                    WasLoaded = true,
                    AwaySeconds = (_clock.NowUnixMs() - cloud.savedAtUnixMs) / 1000.0,
                    SavedAtUnixMs = cloud.savedAtUnixMs,
                };

                LoadedPlayedMs = adopted.State.playedMs;
                AdoptedFromCloud = true;
                onAdopt(adopted);
            });
        }
    }
}
