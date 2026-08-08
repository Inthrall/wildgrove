using System.Collections.Generic;
using System.Globalization;
using Wildgrove.Sim;

namespace Wildgrove.Game
{
    /// <summary>
    /// First-launch default for the Wheel's hemisphere (design §15): guessed
    /// once from the device locale's country, written into the save, and the
    /// warden's to flip in the journal afterwards (the equator simply chooses;
    /// a traveller keeps their own reckoning). Only the large populations need
    /// the guess to be right — everything else is what the setting is for.
    /// </summary>
    public static class HemisphereGuess
    {
        // Countries whose people overwhelmingly live south of the equator.
        // Deliberately conservative: a wrong North default is exactly as
        // recoverable as a wrong South one, so borderline (equatorial)
        // countries are left to the default rather than guessed.
        private static readonly HashSet<string> SouthernCountries = new HashSet<string>
        {
            "AU", "NZ", "FJ", "PG", "SB", "VU", "WS", "TO", "NC", "PF", "TL",
            "AR", "BO", "CL", "PY", "PE", "UY", "BR",
            "ZA", "NA", "BW", "ZW", "MZ", "MG", "ZM", "MW", "AO", "LS", "SZ", "TZ"
        };

        public static int FromLocale()
        {
            try
            {
                var region = RegionInfo.CurrentRegion;
                if (region != null && SouthernCountries.Contains(region.TwoLetterISORegionName))
                {
                    return Wheel.HemisphereSouth;
                }
            }
            catch (System.Exception)
            {
                // An invariant or broken locale (some devices, some players)
                // is not an error — it is just a device that gets the default.
            }

            return Wheel.HemisphereNorth;
        }
    }
}
