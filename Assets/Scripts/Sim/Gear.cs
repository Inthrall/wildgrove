using System.Collections.Generic;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// The warden's kit (design §4): three slots — hands, pack, camp — filled
    /// by crafted survival gear, worn by the warden alone. Crafting a piece
    /// spends its materials from camp stock and wears it at once; crafting
    /// into an occupied slot replaces what's there (the old piece is worn
    /// out — the kit is rebuilt cheaply after Migration anyway, and Migration
    /// resets it). Equipped effects join the run's ActiveEffects.
    /// </summary>
    public static class Gear
    {
        /// <summary>The gear id worn in a slot, or null when the slot is empty.</summary>
        public static string EquippedInSlot(GameState state, string slot)
        {
            return state.gearBySlot.TryGetValue(slot, out var gearId) ? gearId : null;
        }

        public static bool IsEquipped(GameState state, GearData gear)
        {
            return EquippedInSlot(state, gear.slot) == gear.id;
        }

        /// <summary>
        /// True when the piece can be made: its craft skill is unlocked, camp
        /// stock covers the materials, and it isn't already being worn.
        /// </summary>
        public static bool CanCraft(GameState state, GameDataAsset data, GearData gear)
        {
            if (state == null || data == null || gear == null || IsEquipped(state, gear))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(gear.skill) && !Upgrades.UnlockedSkills(state, data).Contains(gear.skill))
            {
                return false;
            }

            foreach (var material in gear.materials)
            {
                if (state.GetResource(material.id) < material.amount)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Craft and wear the piece: spends the materials and fills its slot
        /// (replacing whatever was worn there). Returns false (and changes
        /// nothing) when <see cref="CanCraft"/> says no.
        /// </summary>
        public static bool TryCraft(GameState state, GameDataAsset data, GearData gear)
        {
            if (!CanCraft(state, data, gear))
            {
                return false;
            }

            foreach (var material in gear.materials)
            {
                state.resources[material.id] = state.GetResource(material.id) - material.amount;
            }

            state.gearBySlot[gear.slot] = gear.id;
            Upgrades.RecomputeYieldMultipliers(state, data);
            return true;
        }

        /// <summary>The effects of everything currently worn — folded into Upgrades.ActiveEffects.</summary>
        public static IEnumerable<EffectData> EquippedEffects(GameState state, GameDataAsset data)
        {
            foreach (var pair in state.gearBySlot)
            {
                // A gear id this data version doesn't know is skipped, same
                // policy as purchased upgrades.
                if (!data.GearById.TryGetValue(pair.Value, out var gear))
                {
                    continue;
                }

                foreach (var effect in gear.effects)
                {
                    yield return effect;
                }
            }
        }
    }
}
