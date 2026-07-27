using System.Collections.Generic;
using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// The warden's kit (design §4): three slots — hands, pack, camp — filled
    /// by crafted survival gear, worn by the warden alone. Crafting a piece
    /// spends its materials from camp stock and wears it at once; the piece it
    /// displaces keeps in the kit bag (<see cref="GameState.gearCrafted"/>) and
    /// can be worn again for nothing, so a slot's two contenders are a choice
    /// the player can revisit rather than a purchase they can regret. Each
    /// piece is therefore paid for exactly once per run; Migration resets both
    /// the worn kit and the bag. Equipped effects join the run's ActiveEffects
    /// — a piece resting in the bag gives nothing.
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

        /// <summary>True when the piece has been made this run — worn now, or resting in the kit bag.</summary>
        public static bool IsCrafted(GameState state, GearData gear)
        {
            return state != null && gear != null && state.gearCrafted.Contains(gear.id);
        }

        /// <summary>
        /// True when the piece can be made: its craft skill is unlocked, camp
        /// stock covers the materials, and it hasn't been made already (a piece
        /// in the bag is worn, not re-crafted — see <see cref="CanWear"/>).
        /// </summary>
        public static bool CanCraft(GameState state, GameDataAsset data, GearData gear)
        {
            if (state == null || data == null || gear == null || IsCrafted(state, gear))
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
        /// Craft and wear the piece: spends the materials, puts it in the kit
        /// bag and fills its slot at once (whatever was worn there keeps in the
        /// bag). Returns false (and changes nothing) when <see cref="CanCraft"/>
        /// says no.
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

            state.gearCrafted.Add(gear.id);
            state.gearBySlot[gear.slot] = gear.id;
            Upgrades.RecomputeYieldMultipliers(state, data);
            return true;
        }

        /// <summary>True when the piece is in the kit bag and not already on the warden — a free swap waiting to be taken.</summary>
        public static bool CanWear(GameState state, GearData gear)
        {
            return IsCrafted(state, gear) && !IsEquipped(state, gear);
        }

        /// <summary>
        /// Wear a piece already in the kit bag: fills its slot and sends the
        /// piece it displaces back to the bag. Costs nothing — the materials
        /// were spent when it was made. Returns false (and changes nothing)
        /// when <see cref="CanWear"/> says no.
        /// </summary>
        public static bool TryWear(GameState state, GameDataAsset data, GearData gear)
        {
            if (data == null || !CanWear(state, gear))
            {
                return false;
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
