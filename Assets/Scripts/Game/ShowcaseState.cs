using System.Linq;
using BreakInfinity;
using Wildgrove.Data;
using Wildgrove.Sim;

namespace Wildgrove.Game
{
    /// <summary>
    /// The camp the store-screenshot harness photographs: three zones open,
    /// flocks working, specimens banked, an insect plate half-sketched, the
    /// Rite part-sung. The showcase isn't a legal run, it's a photograph.
    /// <para>
    /// It lives here, beside <see cref="StoreCaptureRunner"/>, rather than in
    /// the editor harness that calls it — <c>Assets/Editor</c> compiles into
    /// Assembly-CSharp-Editor, which no test asmdef can reference, and the
    /// staging is exactly the half that was wrong with nothing to say so: a
    /// Kinship LEVEL of 4200 rode in every published listing shot. What a
    /// screenshot proves is that the layout drew; the drawer being FULL when
    /// it drew is what a test has to pin.
    /// </para>
    /// </summary>
    public static class ShowcaseState
    {
        /// <summary>How far up the Ladder the showcase marches — far enough to open three zones and the crafting lines.</summary>
        private const int LadderRungs = 16;

        public static GameState Stage(GameDataAsset data)
        {
            var state = GameStateFactory.NewGame(data);

            // Skill XP up front so the §9 ladder's skill gates open (money→XP).
            if (data.economy?.xp != null)
            {
                Skills.AddGatherXp(state, data, "foraging", new BigDouble(200000));
                Skills.AddGatherXp(state, data, "mining", new BigDouble(80000));
                Skills.AddGatherXp(state, data, "logging", new BigDouble(60000));
                Skills.AddGatherXp(state, data, "firecraft", new BigDouble(60000));
                Skills.AddGatherXp(state, data, "forgecraft", new BigDouble(60000));
                Skills.AddGatherXp(state, data, "bushcraft", new BigDouble(60000));
            }

            // March up the ladder, granting the materials each rung asks for.
            // The first two rungs are the recruit rungs, so the vole and the
            // raven join here — resting, as any recruit does — and the staged
            // companions below are additional to them.
            foreach (var upgrade in data.upgrades.OrderBy(u => u.order).Take(LadderRungs))
            {
                foreach (var material in upgrade.materials)
                {
                    state.AddResource(material.id, new BigDouble(material.amount));
                }

                Upgrades.TryPurchase(state, data, upgrade);
            }

            state.renown = new BigDouble(1250);
            state.amber = 12;

            // A kith worth photographing: four staged companions on top of the
            // ladder's two, each its own species (the collection model — one
            // familiar per species, ever), on a fully-opened ladder. Restore
            // rests anything past the earned slots, so open them all: ten
            // verses sung plus both store slots.
            state.foldedVersesSung = 10;
            state.purchasedKithSlots = 2;
            // Grounds and the watch, and nothing else: a dig-site post
            // ("dig:{zone}") is retired — SaveCodec rests whoever carries one on
            // load — so the owl staged at one photographed as a body idling at
            // camp, on a page whose whole subject is a full drawer.
            var staged = new[]
            {
                ("sedge-linnet", NodeAt(state, 1)),
                ("red-squirrel", NodeAt(state, 2)),
                ("bramble-hare", Familiar.WanderStation),
                ("tawny-owl", NodeAt(state, 3)),
            };
            for (var i = 0; i < staged.Length; i++)
            {
                var (species, station) = staged[i];

                // Loud, because the quiet version shipped: a retired species id
                // resolves to no plate and no name list, so SuggestName falls
                // back to "Familiar N" and the store screenshots go out with a
                // blank-faced companion in them. Nothing else here would say so.
                if (!data.SpeciesById.ContainsKey(species))
                {
                    throw new System.InvalidOperationException(
                        "[showcase] staged species '" + species + "' is not in species.json");
                }

                state.roster.Add(new Familiar
                {
                    id = state.NextFamiliarId(),
                    speciesId = species,
                    name = Roster.SuggestName(state, data, species),
                    stationId = station,
                    xp = 900 + i * 400,
                    // kinshipXp STORES THE LEVEL — Kinship.Level casts it
                    // straight to int, and the √ conversion happens at
                    // Migration. The 4200 that stood here was read as a level:
                    // the drawer's first plate said "KINSHIP MMMMCC" in every
                    // listing shot taken since.
                    kinshipXp = i == 0 ? 4 : 0,
                });
            }

            for (var i = 0; i < state.nodes.Count; i++)
            {
                var node = state.nodes[i];
                node.masteryXp = 1800 + i * 700;
                node.basket = new BigDouble(12 + i * 7);
                state.AddResource(node.resourceId, new BigDouble(900 + i * 2100));
            }

            state.AddResource("copper-ingot", new BigDouble(14));
            state.AddResource("charcoal", new BigDouble(22));
            state.AddChoice("berries", new BigDouble(3));
            state.AddChoice("wildflowers", new BigDouble(1));
            state.AddDecent("nuts", new BigDouble(45));
            Folio.TryFix(state, data, "wildflowers");

            state.insectSketches["stags-herald"] = 2;
            state.deedCounts["tend"] = 14;
            state.wardenPostNodeId = state.nodes.Count > 1 ? state.nodes[1].id : null;

            Upgrades.RecomputeYieldMultipliers(state, data);
            return state;
        }

        /// <summary>The nth node of the opened map, falling back to the first — the ladder above opens three zones, so the fallback is for hand-built data.</summary>
        private static string NodeAt(GameState state, int index)
        {
            return state.nodes.Count > index ? state.nodes[index].id : state.nodes[0].id;
        }
    }
}
