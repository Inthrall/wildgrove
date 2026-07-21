using Wildgrove.Data;

namespace Wildgrove.Sim
{
    /// <summary>
    /// The kith's slot ladder (design §4): six slots, four free from minute
    /// one, the last two earned — The Old Friend Almanac node and the Warden's
    /// Gallery spread each grant a kithSlot effect. Slots cap how many
    /// familiars walk with the warden at once; an earned bond whose slot isn't
    /// open yet simply waits in the grass (Roster.SyncBonded is called after
    /// every action that can open one).
    /// </summary>
    public static class Kith
    {
        // Hand-built test data often carries no economy — mirror the authored
        // economy.kith values so sim tests exercise the real ladder shape.
        private const int FallbackSlotsBase = 4;
        private const int FallbackSlotsMax = 6;

        /// <summary>The ladder's hard ceiling, however many kithSlot effects content grants.</summary>
        public static int SlotsMax(GameDataAsset data)
        {
            return data?.economy?.kith != null && data.economy.kith.slotsMax > 0
                ? data.economy.kith.slotsMax
                : FallbackSlotsMax;
        }

        /// <summary>Active slots right now: slotsBase + owned kithSlot effects, never above slotsMax.</summary>
        public static int Slots(GameState state, GameDataAsset data)
        {
            var slotsBase = data?.economy?.kith != null && data.economy.kith.slotsBase > 0
                ? data.economy.kith.slotsBase
                : FallbackSlotsBase;
            var slotsMax = SlotsMax(data);

            var slots = slotsBase;
            if (state != null && data != null)
            {
                foreach (var effect in Upgrades.ActiveEffects(state, data))
                {
                    if (effect.type == EffectType.KithSlot)
                    {
                        slots += (int)effect.value;
                    }
                }
            }

            return slots < slotsMax ? slots : slotsMax;
        }

        /// <summary>How many familiars currently hold a slot (the whole roster — presence never lapses at MVP).</summary>
        public static int Count(GameState state)
        {
            return state?.roster != null ? state.roster.Count : 0;
        }

        /// <summary>True when another familiar can join — recruitment events and bond materialisation both ask first.</summary>
        public static bool HasRoom(GameState state, GameDataAsset data)
        {
            return Count(state) < Slots(state, data);
        }
    }
}
