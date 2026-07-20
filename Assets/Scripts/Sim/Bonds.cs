using System.Collections.Generic;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// Bonded familiars (design §4): the rare exception to the Migration
    /// reset — earned, never bought. Earned state is DERIVED from the bond's
    /// source (a completed Folio spread or an owned Almanac node, both of which
    /// already survive Migration), so bonds cross with the warden for free and
    /// can never be lost to a stale save. A bonded familiar is materialised into
    /// the roster (see <see cref="Roster.SyncBonded"/>) and stationed like any
    /// other — carrying is a post now, not a species.
    /// </summary>
    public static class Bonds
    {
        public static bool IsEarned(GameState state, GameDataAsset data, BondData bond)
        {
            if (bond?.source == null)
            {
                return false;
            }

            switch (bond.source.type)
            {
                case "folioSpread":
                    return data.folioSpreads != null
                        && FindSpread(data, bond.source.id) is FolioSpreadData spread
                        && Folio.IsSpreadComplete(state, spread);
                case "almanacNode":
                    return state.almanacNodeIds.Contains(bond.source.id);
                default:
                    return false;
            }
        }

        public static IEnumerable<BondData> Earned(GameState state, GameDataAsset data)
        {
            if (data.bonds == null)
            {
                yield break;
            }

            foreach (var bond in data.bonds)
            {
                if (IsEarned(state, data, bond))
                {
                    yield return bond;
                }
            }
        }

        /// <summary>The bond a source grants, if any — for the HUD to promise the companion before it's earned.</summary>
        public static BondData BondForSource(GameDataAsset data, string sourceType, string sourceId)
        {
            if (data.bonds == null)
            {
                return null;
            }

            foreach (var bond in data.bonds)
            {
                if (bond.source != null && bond.source.type == sourceType && bond.source.id == sourceId)
                {
                    return bond;
                }
            }

            return null;
        }

        private static FolioSpreadData FindSpread(GameDataAsset data, string spreadId)
        {
            foreach (var spread in data.folioSpreads)
            {
                if (spread.id == spreadId)
                {
                    return spread;
                }
            }

            return null;
        }
    }
}
