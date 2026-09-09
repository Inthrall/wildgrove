using Newtonsoft.Json;

namespace Wildgrove.Sim.Saves
{
    /// <summary>
    /// Converts between the live <see cref="GameState"/> and the versioned
    /// <see cref="SaveData"/> wire shape (and its JSON), across this file's
    /// partials: <c>Capture</c> writes a run down, <c>Restore</c> reads one
    /// back and refits it to the current content data, and <c>Migrations</c>
    /// holds the version ladder.
    /// <para>
    /// Any change to the persisted shape means all three: capture the field,
    /// cope with its absence in restore, and add a rung. This file keeps the two
    /// version constants that bound the ladder, and the JSON pair.
    /// </para>
    /// </summary>
    public static partial class SaveCodec
    {
        /// <summary>Bump when the wire shape changes, and add the matching migration step to <see cref="TryMigrate"/>.</summary>
        public const int CurrentVersion = 55;

        /// <summary>
        /// The oldest wire shape this build reads. Saves below it are refused
        /// whole rather than partly understood — <see cref="TryMigrate"/> has no
        /// rung to stand them on, and SaveFile sets them aside instead of
        /// deleting them.
        /// <para>
        /// It stays at 42 while the ladder grows above it: a v42 save climbs
        /// every rung to the current version and is read whole. It moves only when the bottom rungs are
        /// deliberately retired, which is a decision about whose saves stop
        /// working — never a side effect of adding a rung on top.
        /// </para>
        /// </summary>
        public const int EarliestReadableVersion = 42;

        public static string ToJson(SaveData save)
        {
            return JsonConvert.SerializeObject(save, Formatting.Indented);
        }

        /// <summary>Parse a save file's JSON. Returns null when the text isn't a save (corrupt file) — the caller picks the fallback.</summary>
        public static SaveData FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                return JsonConvert.DeserializeObject<SaveData>(json);
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
