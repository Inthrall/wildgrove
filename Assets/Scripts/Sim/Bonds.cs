using System.Collections.Generic;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// Bonded familiars (design §7): the rare exception to the Migration
    /// reset — earned, never bought. Earned state is DERIVED from the bond's
    /// source (a completed Museum set or an owned Almanac node, both of which
    /// already survive Migration), so bonds cross with the warden for free
    /// and can never be lost to a stale save. Bonds are role-locked and sit
    /// outside the gift count, the cost curves, and the familiar caps: a
    /// bonded carrier hauls alongside the fleet without holding a slot, and a
    /// bonded gatherer works at the warden's side — the node last tended.
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
                case "museumSet":
                    return data.museumSets != null
                        && FindMuseumSet(data, bond.source.id) is MuseumSetData set
                        && Museum.IsSetComplete(state, set);
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

        private static MuseumSetData FindMuseumSet(GameDataAsset data, string setId)
        {
            foreach (var set in data.museumSets)
            {
                if (set.id == setId)
                {
                    return set;
                }
            }

            return null;
        }
    }
}
