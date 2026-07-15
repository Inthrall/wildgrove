using System.Collections.Generic;

namespace Wildgrove.Data
{
    public sealed class RecipeDef
    {
        public string Id { get; set; }
        public string Station { get; set; }
        public string Skill { get; set; }
        public Dictionary<string, int> Inputs { get; set; } = new Dictionary<string, int>();
        public string Output { get; set; }
        public double ValueMult { get; set; }
        public string Kind { get; set; }

        // Known from the moment its station + skill are available; recipes
        // without this must be granted by an upgrade's unlockRecipe effect.
        public bool DefaultKnown { get; set; }
    }
}
