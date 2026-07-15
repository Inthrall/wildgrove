using System.Collections.Generic;
using Newtonsoft.Json;
using Wildgrove.Data;

namespace Wildgrove.Sim.Saves
{
    /// <summary>
    /// Converts between the live <see cref="GameState"/> and the versioned
    /// <see cref="SaveData"/> wire shape (and its JSON). Restore rebuilds the
    /// node list from the current content data and overlays the saved per-node
    /// progress by node id, so a save taken on older data self-corrects:
    /// removed nodes drop away, new nodes appear with fresh defaults, and
    /// derived values (skill, yieldMultiplier) are recomputed rather than
    /// trusted. Unknown resource and upgrade ids are kept as-is — the run
    /// doesn't lose property because a data version renamed something, and the
    /// sim already tolerates unknown ids downstream.
    /// </summary>
    public static class SaveCodec
    {
        /// <summary>Bump when the wire shape changes, and add the matching migration step to <see cref="TryMigrate"/>.</summary>
        public const int CurrentVersion = 1;

        public static SaveData Capture(GameState state, long savedAtUnixMs)
        {
            var save = new SaveData
            {
                version = CurrentVersion,
                savedAtUnixMs = savedAtUnixMs,
                coin = state.coin,
                verdurePoints = state.verdurePoints,
                purchasedUpgradeIds = new List<string>(state.purchasedUpgradeIds),
            };

            foreach (var pair in state.resources)
            {
                save.resources.Add(new SavedResource { id = pair.Key, amount = pair.Value });
            }

            foreach (var node in state.nodes)
            {
                save.nodes.Add(new SavedNode
                {
                    id = node.id,
                    familiarCount = node.familiarCount,
                    masteryLevel = node.masteryLevel,
                    tendBurstRemaining = node.tendBurstRemaining,
                });
            }

            return save;
        }

        public static GameState Restore(SaveData save, GameDataAsset data)
        {
            // The fresh-run baseline supplies the node set the current data says
            // exists (Phase 1: the starting zone; zone unlocks extend this with
            // Phase 3 — see docs/todo.md).
            var state = GameStateFactory.NewGame(data);
            state.coin = save.coin;
            state.verdurePoints = save.verdurePoints;
            state.purchasedUpgradeIds = save.purchasedUpgradeIds != null
                ? new List<string>(save.purchasedUpgradeIds)
                : new List<string>();

            state.resources.Clear();
            if (save.resources != null)
            {
                foreach (var resource in save.resources)
                {
                    if (resource?.id != null)
                    {
                        state.resources[resource.id] = resource.amount;
                    }
                }
            }

            var savedById = new Dictionary<string, SavedNode>();
            if (save.nodes != null)
            {
                foreach (var node in save.nodes)
                {
                    if (node?.id != null)
                    {
                        savedById[node.id] = node;
                    }
                }
            }

            foreach (var node in state.nodes)
            {
                // A node the save predates keeps its fresh-run defaults
                // (including the first-node starter familiar).
                if (!savedById.TryGetValue(node.id, out var saved))
                {
                    continue;
                }

                node.familiarCount = saved.familiarCount;
                node.masteryLevel = saved.masteryLevel;
                node.tendBurstRemaining = saved.tendBurstRemaining;
            }

            Upgrades.RecomputeYieldMultipliers(state, data);
            return state;
        }

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

        /// <summary>
        /// Bring an older save up to <see cref="CurrentVersion"/> in place.
        /// Returns false for a save from a future build — never guess at a
        /// shape this build doesn't know.
        /// </summary>
        public static bool TryMigrate(SaveData save)
        {
            if (save == null || save.version > CurrentVersion)
            {
                return false;
            }

            // Migration steps go here as `while (save.version < CurrentVersion)
            // switch (save.version) { ... }` once v2 exists; v1 is the first
            // released shape, so anything at or below it just stamps current.
            save.version = CurrentVersion;
            return true;
        }
    }
}
