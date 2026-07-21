# Wildgrove — Design Document

*Design document v0.11 · July 2026 · Working title · Prior rationale lives in repo history*

> An idle gathering & collecting game about tending a wilderness that remembers. Walk deeper with a small crew of wild companions, craft to survive, plant back more than you take — and listen when the land speaks, which is rarely.

## Contents

1. [Vision & pillars](#1--vision--pillars)
2. [Core loop & stationing](#2--core-loop)
3. [The Trail — zones](#3--the-trail)
4. [Familiars — the small flock](#4--familiars)
5. [Crafts & survival gear](#5--crafts)
6. [The Journal — Compendium, Folio & Deep Pages](#6--collection)
7. [Narrative & tone](#7--narrative--tone)
8. [Prestige — the Rite & Migration](#8--prestige)
9. [Economy math](#9--economy)
10. [First 30 upgrades](#10--progression)
11. [Monetization](#11--monetization)
12. [Level Up compliance](#12--level-up-compliance)
13. [MVP development plan](#13--mvp-development-plan)
14. [Open questions](#14--open-questions)

---

## 1 · Vision & pillars

### Tend, don't take

You are the warden of a small camp on the edge of an old, patient wilderness — and you are the only human in it. What company you have, the land sends: **familiars**, a handful of wild creatures who choose a careful warden — never more than a few, each one a name, a face, and a working partner. Together you gather, and together you give back: **replanting** what you take, building the land richer than you found it. Around the fire you craft what the crew brings home into the gear and goods a camp needs to push deeper. Some finds are **Pristine**: specimens worth keeping forever. Some the land only lets you watch — the rare **insects** that gather at quiet sites: observed and drawn in careful sketches, then let go — each plate a sentence in a story nobody finishes telling you. And each region asks something back: a **Rite**, performed verse by verse at quiet places, offerings set down for spirits you never see — the land deciding whether you may move on. When the Rite is done and the season turns, the camp **migrates**, most of the crew slips back into the grass, and the land — and the creatures — remember how you treated them.

The fantasy is *stewardship, not extraction*. You are a guest who works hard, with a few friends who noticed.

### Direction note — leaning into Obelisk (decided)

Wildgrove borrows Idle Obelisk's spine (the Trail) and gate (the Rite) — and, deliberately, its heart: a small crew of individually upgradeable workers whose builds and postings are the run's central decisions.

| Axis          | Lineage                              | Description                                                                                           |
| ------------- | ------------------------------------ | ------------------------------------------------------------------------------------------------------ |
| **The Trail** | Spine · Idle Obelisk                 | Push deeper through eight zones. Each opens new resources, a new craft, a waystone, and — from Zone 3 — observation sites. |
| **Familiars** | Crew · Idle Obelisk (the drones)     | Up to five active companions (decided). Each levels at its post, chooses powerups, and is missed when it wanders. |
| **Crafts**    | Breadth · Melvor Idle                | Parallel gathering and survival-crafting skills with XP, mastery, tools, and gear — the economy's backbone (§9). |
| **The Rite**  | Gate · Idle Obelisk (minus fighting) | A staged ritual of offerings — five slots a verse, any three. The finished Rite unlocks Migration.       |
| **Migration** | Meta · Egg Inc                       | Prestige resets the region and banks Verdure — the land's memory — into the permanent Almanac.           |

**Guard rails.** Two things Obelisk does not have, and Wildgrove must keep: nine crafts that genuinely interlock (ore → ingot → tool; timber → planter → yield), and a land that is a character, not a mine. If a change makes the familiars feel like appliances or the zones like ore veins, it has gone too far. The target is *obelisk with a heartbeat*.

**The collecting identity:** the Compendium (living things, your crew among them), the Folio, and the Deep Pages are the axes that never reset. They carry the spread bonuses, the 40+ achievements, nearly all of the story — and the familiar roster you actually play (§4).

### Art direction — the naturalist's plates (decided)

- **Every collectible is a plate.** Compendium entries, insect cards, gear, and every roster familiar: fine ink linework, muted watercolour and sepia washes, numbered figures, hand-written margin annotations.
- **UI is the warden's field journal.** Paper grounds, journal tabs, stitched spines; texture as identity, never as noise.
- **Zones are the stage, plates are the star.** Layered painted-parallax backdrops, light ambient motion. The illustration budget concentrates where the collecting identity lives.
- **Solo-scale production:** one fixed plate template; ~60 plates at MVP. Scope pressure cuts *plate count*, never plate quality.

---

## 2 · Core loop

### Gather → haul → craft → give back

```
[WILDS]                      [TRAIL]              [CAMP]
Warden + familiars      ──►  Trail post     ──►  Fire, forge & bench  ──►  goods
gather at their posts        hauls home          │
(stationing = the            (a job, not         ├─► GEAR (the warden's kit)
 allocation decision)         a species)         ├─► TRADE GOODS ──► the Exchange (barter)
     ▲                                            ├─► OFFERINGS ──► the current verse of the Rite
     │                                            └─► REPLANTING & PLANTERS ──► richer nodes
     └───────── tools, gear, provisions, planter materials ◄─────────┘
```

1. **Agents at posts.** The warden and each active familiar can be **stationed** at a post; an unassigned familiar **wanders** (stationing rules, below) — half-hearted help, never zero. Up to five in the crew, one usually on the trail, eight-plus nodes by mid-run: coverage is never enough, and *where the crew stands* is the moment-to-moment decision.
2. **Tending** a node (tap / click / key) is the warden's own act: a burst of yield and briefly raised Pristine chance. Tending a node stations the warden there (decided 2026-07-17). Attention is rewarded, never demanded.
3. Harvest travels home by whoever holds the **trail post** — hauling is a post like any node, not a species (DECIDED 2026-07-18). The pack raven arrives at minute one and takes it first. Under-invest in the post and baskets overflow at the node: the visible bottleneck.
4. The **fire and benches** turn raw finds into four kinds of output: **survival gear** (the kit — permanent-for-the-run buffs), **trade goods** (dense barter weight at the Exchange), **offerings** (consumed by the Rite, §8), and **planter materials** (given back to the nodes, §3).
5. The **Exchange** — the silent caravan — barters goods for goods. Deeper zones demand better tools *and* better gear → invest → advance. Repeat.

> **Design tension to preserve:** gather rate (stationing + richness) vs. haul rate (the trail post + powerups) vs. craft rate (queues + materials) stays slightly out of balance — upgrading one always exposes the next bottleneck. And the craft split is a **four-way dilemma**: craft for the kit, for the caravan, for the spirits, or for the land itself? Tuning rule: no lane may starve.

### Stationing rules (DECIDED 2026-07-18)

The single most load-bearing rule set in the game — written down so every system builds on the same one:

- **Assigned or wandering (DECIDED 2026-07-18).** An assigned agent works its post deliberately. An **unassigned familiar wanders** — it drifts to work on its own, a random unlocked node, or the trail when baskets back up and no one holds the post — at **×0.5 rate and ×0.5 XP**, with no powerup effects. Help, never zero: deliberate assignment is optimization, not a chore. The warden never wanders — the warden's post is wherever they last tended.
- **Unattended nodes.** A node with no assigned agent keeps its richness, planters, and basket; only wanderers touch it, glancingly.
- **Transit.** Reassignment is always allowed and never costs goods; the agent *walks* — seconds, scaled by trail distance, producing nothing en route and visible on the trail. The map is honest.
- **The trail post.** Hauling is a stationing assignment: one trail post at MVP, a second via Spare Wing (§11). The familiar holding it gathers nothing — haul rate is bought with a gatherer, which is the real price. The warden never takes the trail post: the warden tends, the crew carries. An unheld trail is covered badly by wanderers — half of one lane at best — so the trap is never a silent zero, only a visible half.
- **Offline.** Per node: `earn = min(gather rate, trail-post throughput) · min(t, cap)`, with wanderers counting at ×0.5 and an unheld trail moving ×0.5 of one lane. The welcome-back sheet always names what limited the night (the haul cap, or wanderers covering the trail). Per-agent base rates are tuned **up** from flock-era assumptions so a night away with a small crew still feels generous (magnitudes in the Phase 3 spreadsheet).
- **Reachability.** Anywhere the design says "reachable" — most importantly the Rite validator (§8) — it means *satisfiable under plausible stationing with the current crew size*, not merely unlocked.

---

## 3 · The Trail

### Eight zones, four at MVP

| # | Zone                  | Resources                             | Unlocks                                                | Keystone specimen   | Scope |
| --- | --------------------- | ------------------------------------- | ------------------------------------------------------ | ------------------- | ----- |
| 1 | **Sunfield Meadow**   | Berries, wildflowers, fibres          | Foraging (start)                                       | Sunburst Poppy      | MVP   |
| 2 | **Bramble Hedgerows** | Nuts, herbs, copper scree             | Firecraft, Mining                                      | Amber-Shelled Snail | MVP   |
| 3 | **Old-Growth Wood**   | Deadfall timber, mushrooms, tin seams | Logging, Bushcraft, **first observation site**                 | Ancient Acorn       | MVP   |
| 4 | **Silverrun River**   | Fish, reeds, clay, iron-rich gravel   | Fishing, riverbank observation site                            | Moonscale Trout     | MVP   |
| 5 | **Mistfen Marsh**     | Peat, rare herbs, fireflies           | Entomology, Apothecary                                 | Lantern Firefly     | v1.1  |
| 6 | **The Hollows**       | Deep ores, crystals, bone beds        | Delving; the rarest insects                     | Echo Geode          | v1.1  |
| 7 | **Highland Crags**    | Eggs, wool, lichen                    | Husbandry                                              | Cloudfleece Ram     | v1.2  |
| 8 | **Cloudreach Peaks**  | Sky-blossoms, glacier ice             | The final waystones (endgame)                          | Aurora Bloom        | v1.2  |

Each zone is a screen: 2–3 nodes, the trail home, one keystone specimen, one **waystone** (the past speaking, §7), and one **verse site** (the present, §8). From Zone 3, a **observation site**. Zone entry costs **provisions** (a goods bundle — you pack for the walk, §9) *and* a tool tier, so Trail progress is paced by economy and craft investment.

### Replanting & planters — the fourth claim (decided)

Nodes are not fixed faucets; they can be **made richer**, and the making costs goods.

- **Replanting** (node's own resource → richness): each node has a richness level, raised by replanting its own resource back into it — `replantCost(L) = base · r^L`, per node, per run. Richness raises the node's **base yield**. The lever split that keeps the UI legible: *replant the node, level the familiar* (§4) — one improves the place, the other the worker.
- **Planters** (cross-resource → infrastructure): built structures costing *other zones'* goods. Timber frames raise a meadow node's **capacity** (basket size); clay beds speed **regrowth** after a Tend burst; cordage trellises open a **second yield lane** at flower nodes; reed screens steady a observation site's sketch progress. The backward flow that keeps old zones alive forever: Zone 3 timber has a job in Zone 1.
- **Bootstrap:** the warden's trickle at their post self-funds a virgin node's first replant. Presence, not currency (decided 2026-07-17).
- **No Renown.** Replanting pays you back in yield; the land's memory (§9) is reserved for what you gave *up*. Dev-facing: replanting is the most on-theme verb in the game — the wardens are the land's experiment in trying again, and this is the trying. It earns tone, not numbers.
- Richness and planters **reset at Migration**. One Almanac node — *The First Planting* — lets a single planter survive the fold: the land keeping something you built, for once.

---

## 4 · Familiars

### The small flock (decided)

Familiars are not a count; they are a **crew**. Each is an individual: a name, a Compendium plate, a level, a build, and — over many seasons — a memory of you.

**Active slots** — the ladder:

There is no carrier type — carrying is a **post** (the trail), and any familiar can hold it. Slots are just slots:

| Slot        | When                        | How                                                                    |
| ----------- | --------------------------- | ----------------------------------------------------------------------- |
| 1 & 2       | Minute one                  | The land's first gesture — a vole and a raven arrive unasked. One gathers; one takes the trail post. The bootstrap, twice. |
| 3           | First hour                  | A **gift event**, unlocked by verse 1's completion (decided 2026-07-18): leave a pile of goods at a node; something says yes. The first verse is answered by the warden's own hands. |
| 4           | Mid-game (early Migrations) | Almanac node — the land trusts you with another.                        |
| 5           | Late mid / endgame          | A keystone or completed-spread moment. Every slot arrival is an *event*. |
| Trail posts | 1 at MVP · 2nd via **Spare Wing** (§11) | A second haul lane — at these counts, enormous; hauling equipment (§10) is tuned assuming two posts eventually exist (§14). |

Gifts are *recruitment events* — one pile, one yes — keeping the <60s first-companion beat without a cost curve. Nothing repeatedly buys a creature; nothing ever has.

**Mini-wardens.** A familiar is stationed exactly as the warden is: one agent system (§2), and the trail is simply another post. Assign it to a node and it works there — a steady gather trickle, and a slow tending cadence if its build allows; assign it to the trail and it carries. Stationing scarce agents across abundant nodes is the Obelisk allocation decision.

**XP & powerups** — the build system:

- A familiar earns **XP at its post**, from its own work — faster where specialized, a trickle offline.
- Every 5 levels it offers a **choice of 2–3 powerups**, drawn from its species' **fixed, authored pool** (DECIDED 2026-07-18: pools are deterministic per species at MVP, never rolled — the Rite generator can rely on what any crew can become; rolled variety is a v1.1 lever) and filtered to unlocked content (no dead picks — a timber powerup is never offered before the Wood opens). Chosen once, **kept for the run** — no respec; a build is a commitment. (Species pools lean by nature: the raven's favours the trail, the vole's the ground.)
- **Levels never scale output.** A familiar's level paces XP and Kinship only; throughput and yield come from tools and hauling equipment (the post's levers), powerups (the holder's), and richness. No double-scaling — and no grinding a hauler.

| Pool (examples)    | Effect                                     | Flavour                                  |
| ------------------ | ------------------------------------------ | ---------------------------------------- |
| Berry-wise         | +40% yield at berry nodes                  | knows which bramble the birds missed     |
| Timber-sense       | +40% yield at deadfall                     | hears the dead branches before they fall |
| Soft paws          | +1pt Pristine chance at its node           | takes nothing it bruises                 |
| Deep pockets       | +25% throughput when it holds the trail post | cheeks like saddlebags                 |
| Green claw         | its node's planters regrow 25% faster      | the land likes how it digs               |
| Patient watcher     | +observation speed at observation sites                    | was going to dig anyway                  |
| Early riser        | its node's offline earnings +20%           | up before the warden, always             |

**Roster > slots.** The **roster** is every familiar ever befriended — gift events, keystones, Museum sets, zone arrivals — each with its own plate. Only slotted familiars work. Fielding two of six is a real choice, informed by the region modifier and the run's plan: the collection becomes something you *play*.

### Familiar power across Migration — the two-track model (DECIDED 2026-07-18)

Familiar power lives on two tracks, mirroring the game's own grammar (fast-resetting run power under a slow permanent track — the same shape as mastery, and as Verdure itself):

- **Run track (resets for everyone):** levels and powerup picks are per-run. At Migration every familiar — bonded or not — returns to level 1 with a clean build. Run 2 asks a different question; the build stays a live decision forever.
- **Kinship (never resets):** each roster familiar carries a permanent **Kinship level** — *the creature's memory of careful hands*, as Verdure is the land's. At Migration, a familiar's run XP converts to Kinship XP (conversion only — run XP already credited Renown as it was earned; **no second Renown grant**). The √ conversion decelerates in parallel with Verdure — both permanent tracks flatten together — and Kinship gains appear **in the same fold forecast panel** (§8), so the creature's memory never quietly argues against leaving. Kinship gives small permanent perks: **higher starting level** and **+XP rate**. At Kinship milestones a familiar locks in one **signature trait** — a permanent powerup that becomes its identity, inscribed on its plate (*"Bramble, meadow vole — soft paws"*). **MVP ships starting level + XP rate only; signature traits are the 1.1 depth lever.**
- *(Vocabulary: the permanent track is **Kinship**, never "bond level" — "bond" belongs to Migration-crossing, below.)*

**Bonding** is the separate, rarer honour (earned, never bought): a **bonded** familiar crosses the fold and is present from minute one — and its Kinship is why it is also *good* from minute one. Most of the roster slips back into the grass at Migration and is re-met in later regions — a quiet reunion beat, never a re-grind: roster and Kinship persist, only presence lapses. MVP: 1–2 bondable (final counts and rarity: Mo to settle, §14).

**Deliberately not adopted:** Almanac-mediated familiar power ("familiars start at level 5" Verdure nodes). Familiar permanence is exclusively Kinship — the relationship is with the creature, not purchased from the tree. The Almanac's familiar-adjacent nodes are limited to slot access (crew slot 4) and *The First Planting*.

**Roosts & Burrows:** the building line levels **familiar comfort** — +XP rate for all stationed familiars per level; late levels add roster capacity. Depth, not headcount.

---

## 5 · Crafts

### Nine crafts at MVP, twelve at 1.2

| Craft          | Type              | Feeds on                         | Produces                              | Scope |
| -------------- | ----------------- | -------------------------------- | ------------------------------------- | ----- |
| **Foraging**   | Gathering         | Meadow, Hedgerow nodes           | Berries, flowers, fibres, nuts, herbs | MVP   |
| **Logging**    | Gathering         | Old-Growth deadfall              | Timber, mushrooms (by-find)           | MVP   |
| **Fishing**    | Gathering         | Silverrun nodes                  | Fish, reeds, clay                     | MVP   |
| **Mining**     | Gathering         | Scree, seams, gravel (Zones 2–4) | Copper, tin, iron ore; flint, stone   | MVP   |
| **Firecraft**  | Survival crafting | Berries, nuts, mushrooms, fish   | Preserves, skewers, meals; torches    | MVP   |
| **Forgecraft** | Survival crafting | Ores, timber (charcoal), clay    | Ingots → every tool tier; fittings    | MVP   |
| **Bushcraft**  | Survival crafting | Timber, fibres, reeds, clay      | Cordage, planks, baskets; **planters**; gear | MVP |
| **Observation** | Collection       | Observation sites (Zones 3+)     | Field sketches (insect-plate portions), amber | MVP |
| **Curation**   | Collection        | Pristine specimens, insect plates | Folio fixings, spread bonuses         | MVP   |
| **Entomology** | Gathering         | Marsh nodes                      | Insects (pure collection value)       | v1.1  |
| **Apothecary** | Survival crafting | Herbs, peat, fungi               | Tinctures (buff consumables)          | v1.1  |
| **Husbandry**  | Gathering         | Crag nodes                       | Eggs, wool                            | v1.2  |

### Skill structure (per craft)

- **Skill level** 1–99, XP from every action. With Coin gone (§9), *levels are the gate and materials are the cost*, everywhere.
- **Mastery** per resource: +5% yield/value per mastery level. The long-tail chase.
- **Tools are mined, smelted, and smithed — never bought, literally.** Tier 1 is knapped from surface flint (a meadow by-find; no forge). Every tier after needs a **skill gate + an ingot batch**: Copper → Bronze → Iron → Steel → deep ores. Each tier ×2 yield. Mining/Forgecraft is the unlock backbone: zones gate ores, ores gate tools, tools gate zones.
- A stationed agent works its node's gathering craft; crafting runs in parallel via fire/bench queues — bars always filling. The Melvor texture lives chiefly in the **queues** (the gather side is a handful of trickles, not a wall of flocks); base queue counts are tuned so all four output lanes (§2) genuinely compete.

### Survival gear (the warden's kit)

Three kit slots — **Hands**, **Pack**, **Camp** — worn by the warden alone; persists for the run, rebuilt cheaply after Migration (the early-run ritual).

| Gear (MVP)       | Slot  | Craft     | Materials              | Effect                          |
| ---------------- | ----- | --------- | ---------------------- | ------------------------------- |
| Cordage Wraps    | Hands | Bushcraft | Fibres ×40             | Tending burst +50%              |
| Birch Frame Pack | Pack  | Bushcraft | Timber ×25, cordage ×5 | Trail post carries +25%         |
| Pitch Torch      | Camp  | Firecraft | Timber ×10, fibres ×10 | Night hours count fully offline |
| Oilskin Tarp     | Camp  | Bushcraft | Reeds ×30, fish oil ×5 | Offline cap +2 h                |
| Clay-Lined Creel | Pack  | Bushcraft | Clay ×20, reeds ×15    | Fish never spoil in transit     |

### Recipe chains (MVP trade goods & planters)

```
Berries + Nuts        ──►  Fire      ──►  Berry Preserve     (×4 barter weight)
Mushrooms + Berries   ──►  Fire      ──►  Forager's Skewer   (×5)
Fish                  ──►  Fire      ──►  Smoked Trout       (×5)
Timber                ──►  Bench     ──►  Planks             (×3)
Planks + Reeds        ──►  Bench     ──►  Reed Baskets       (×6)

Timber + Cordage      ──►  Bench     ──►  Planter Frame      (node capacity)
Clay + Fibres         ──►  Bench     ──►  Growing Bed        (node regrowth)
Planks + Cordage      ──►  Bench     ──►  Trellis            (second yield lane)

Copper Scree          ──►  Fire      ──►  Copper Ingots  ─┐
Tin + Copper          ──►  Forge     ──►  Bronze Ingots  ─┼─►  Tool tiers & fittings
Iron Gravel + Charcoal──►  Forge     ──►  Iron Ingots    ─┘   (equip, don't trade)
```

---

## 6 · Collection

### One book — the journal is the multiplier (Museum retheme DECIDED 2026-07-18)

There is no museum. There was never anywhere to put one. Everything the collecting game keeps lives in **the journal itself** — the one thing the warden truly owns, and the one thing that crosses every fold. That is also *why* collection grants power: what the warden understands, the warden tends better. A completed page is not a trophy; it is competence.

**The Compendium** records every gatherable, creature, and recipe on first meeting — a plate, a line or two, lifetime counters. **Roster familiars each get a plate**, growing over seasons: name, species, Kinship level, and (at 1.1) an inscribed signature trait — the game's warmest pages, and its most-consulted, because the roster is fielded from here.

Finds roll a quality: **Common** (96%), **Fine** (~3.5%, +50% barter weight), **Pristine** (~0.5% base, upgradeable). Quality rolls happen per **haul batch**, not per unit — at idle rates a per-unit roll would shower Pristines and cheapen the windfall. A Pristine find can be traded at the Exchange for a windfall — **fixed into the Folio** — or **offered**: the three-way windfall choice.

**The Folio** (replaces the Museum) is the journal's back pages, where Pristine specimens are physically **fixed**: flowers pressed, feathers tipped in, scales gummed to the paper, a nut split and mounted. **Spreads** group 4–8 related entries; a completed spread grants a *permanent* bonus surviving Migration (+% yield, +Pristine chance, +offline cap) — and one MVP spread grants crew slot 5's moment (§4). **Fixing is a real choice:** run-speed now versus permanence — the specimen is consumed by the page. (Note the line this draws: the living land *gives*, and what it gives may be kept, pressed, traded, or offered; the buried past is only ever borrowed with your eyes — see below.)

### Insects — the deep chase (observe · sketch · release — DECIDED 2026-07-21)

The one system that used to take. It doesn't anymore: **nothing is kept.** From Zone 3, each region has an **observation site** where its rare living things gather — beetles, dragonflies, the pollinators. Familiars set to **Observation** watch and wait; each patient watch adds a **field sketch** — one portion of a plate, drawn in the warden's hand — and then the creature goes on about its life. **The Deep Pages are a book of drawings, not a case of pinned specimens.** The collection is knowledge; the living keep their lives.

- An insect's plate completes when **all portions are sketched** (3–5 per plate; pity timer: a portion is guaranteed per 4 h watched). A completed plate grants its **large permanent multiplier** unchanged — the land rewarding *attention paid* — and its **lore line**, now literally the finished plate and its field note.
- **The release beat:** completing a plate's final portion plays as a small sign (the creature lifts, circles once, and is gone) — never words. The 1.1 active-play prototype (hold the frame still, draw the line) *is* the sketching moment, and it has an ending.
- **Amber is the exception, and stays takeable:** old resin with an ancient insect sealed inside — the one creature the land lets you keep, because it was gone long before you came. Its observation-site source is untouched, and it is the only window the ground still opens onto the deep past.
- **Sketches can be offered.** A verse slot may ask for a field sketch: the page is torn out for the spirits and that portion must be **re-observed** (the pity timer keeps this fair). Giving up the *record* is the steepest offering in the game, priced accordingly in Renown.

| Insect plate (MVP)       | Portions | Where                  | Bonus                      | What it whispers                                          |
| ------------------------ | -------- | ---------------------- | -------------------------- | --------------------------------------------------------- |
| **The Stag's Herald**    | 3        | Old-Growth Wood        | +10% all yields            | A beetle armoured like something ten times its size. It remembers being feared. |
| **The Silver Skimmer**   | 4        | Silverrun River        | +15% fishing, +1% Pristine | A damselfly older than the river's name. It has watched the water change and change. |
| **Those Who Sow**        | 5        | Both sites, rare hours | +20% all yields            | The pollinators. They have tended this land far longer than you, and asked for nothing. |
| *The deep amber* (v1.1)  | —        | Hollows                | —                          | An insect no one living has seen, held in resin. The world it flew through has ended. |

Target: ~6 plates at MVP, ~30 by 1.2 — each a multiplier *and* a chapter.

> **Note (2026-07-21 reframe):** the collectible was fossils (uncover·record·rebury); it is now living insects (observe·sketch·release), and the deep-time lore that fossils used to carry now rides on **amber** (an ancient insect in resin) and the final waystones. The lore lines above and the §7 backstory are a **draft to re-voice**.

---

## 7 · Narrative & tone

### The land speaks sparingly

No cutscenes, no quest log, no exposition. Story arrives through inscriptions, terse strangers, and places that imply more than they say. A player who ignores every word still has a complete idle game; a player who reads everything assembles something quietly devastating.

Six delivery channels:

- **Waystones** — one per zone, an inscription revealed on arrival. Two lines, never more. Weathered, second-person, addressed to wardens in general — the *past* speaking.
- **Verses** — the living land's asks, the *present* speaking. A single line naming its offerings. **No spirit is ever seen.** A completed verse is answered with a sign — the wind turns, the fireflies gather — never with words.
- **Insect plates** — the load-bearing lore: the completed rubbing and its field note, trailing off. The Long Winter is only ever visible as an absence.
- **The Exchange** — the caravan speaks one line per visit, maybe. Dry, oblique, faintly amused. Never answers a question the game lets you ask.
- **Migration vignettes** — three lines over a dark screen. Twelve words.
- **Plate inscriptions** — a familiar's plate gains a margin line at Kinship milestones, in the warden's hand. The only channel about *individuals*, budgeted inside the 1,200.

### Voice samples

> Walk gently. The meadow fed them, too, and they are under it.
> — *Waystone · Sunfield Meadow*
> These roots drink deeper than you will ever dig. Let them.
> — *Waystone · Old-Growth Wood*
> What you carry is borrowed. Set some down, and see what follows you home.
> — *First verse · the fire circle, Sunfield Meadow*
> Antlers on a beetle no longer than your thumb. It fought something once, or meant to.
> — *Insect plate · The Stag's Herald*
> Hold still. Let it finish and go — the drawing is enough.
> — *margin note · the first release*
> Sell me nothing you would weep to lose. I do not give refunds, and the land does not either.
> — *the caravan, at the Exchange*
> The meadow does not need you. It is letting you help.
> — *margin note · richness 5, any meadow node*
> Third season she has found me. I have stopped calling it luck.
> — *plate inscription · Kinship milestone*
> The camp folds. The land exhales.
> What you gave, it keeps.
> — *Migration vignette*

### The authorial truth (dev-facing — never stated in game)

Long before the game begins, a civilization — *Those Who Planted* — worked this land the way the player does, and did not stop. Their taking outran the land's giving, and the Long Winter answered: the ash line the deepest amber is sealed beneath. The wilderness that regrew is not wilderness; it is a survivor, and it is watching. The wardens are its long experiment in trying again — Verdure is the land's memory of careful hands, **Kinship is a creature's** (the experiment watching back, up close, and deciding it likes you), and Migration happens on the land's terms: that is what the Rite is. The late-game implication, assembled only from the deepest amber and the final waystones: the warden order descends from Those Who Planted. You are the apology. Replanting is the apology *practiced*.

### Writing rules

- Total word budget at MVP: **~1,200 words**, all channels included. Scarcity is the aesthetic.
- Max two lines on screen at once. Everything skippable, re-readable in the Compendium.
- Proper nouns are never explained. Questions are answered only by other questions, three zones later.
- Nothing hostile, nothing gory. The dread is geological — patient and mostly kind. Tone target: a nature documentary narrated by someone grieving politely.
- Each new system's *first* margin note doubles as its instruction, in voice ("left a pile by the bramble; something is watching it") — the teaching pass is a writing task, not a tutorial system.

---

## 8 · Prestige

### The Rite — the region's exit gate

- **Verses**, one revealed per zone at its verse site (four at MVP) — visible and chippable from session one. Workable in parallel, any order; Migration requires every revealed verse complete.
- **Choose 3 of 5.** Five offering slots — raw finds, crafted goods, occasionally a Fine or Pristine specimen, occasionally a deed (Tend N times) — any three finish the verse. One or two **spotlight crafts** are the cheapest path, rotating run to run. Unchosen slots expire, no partial credit.
- **Offerings** are delivered incrementally, consumed on delivery, and **credit Renown at full trade value** (§9): you give up liquidity, never prestige progress. Deed, specimen, and field-sketch slots carry fixed Renown grants — the sketch's is the largest, as the steepest thing a warden can give (§6). Gifts and replanting earn no Renown — feeding the spirits is remembered; feeding the voles is lunch.
- **Authored once, generated after.** Run 1 hand-authored (`rites.json`) as the tutorial; from run 2 a generator builds each Rite from migration count × region modifier × unlocked content — validator-guaranteed ≥3 reachable slots per verse, where *reachable* means satisfiable under plausible stationing with the current crew size (§2), not merely unlocked.
- **Not for sale.** Amber never fills a verse slot.

### Migration

When the Rite completes and the region slows, the camp folds. Levels, builds, richness, planters, buildings, gear, kit, and zone progress reset. You keep **the journal entire** — Compendium, Folio spreads, insect plates — plus the roster and every **Kinship** level, Amber, and newly banked **Verdure**.

- **Verdure** — from lifetime Renown (§9) — permanent, stacking **+2% all yields**.
- **The Almanac** — the permanent Verdure tree: offline caps, starting tool tiers, trail-post efficiency, Pristine chance, observation speed, auto-craft, zone skips, crew slot 4, *The First Planting*. ~12 nodes MVP, ~40 by 1.1. **No familiar-power nodes** (§4).
- **Bonded familiars** cross the fold, present and Kinship-strong from minute one — much of why run 2 feels faster.
- **Region modifiers** (lush: +herbs · misted: +fish, −flowers · ashen: +observation speed) flavour each run and feed the Rite generator.
- Rebuilding the kit in the first minutes stays deliberate — the survival ritual that makes each region feel inhabited.

### When to migrate — DECIDED (2026-07-18): the fold forecast is the decision

Once the Rite completes, the pinned tracker becomes the **fold forecast** — **every permanent gain in one panel, nothing hidden**:

> *+7 Verdure · Bramble +2 Kinship · Fern +1 · the 8th Verdure is ~40 min away at current pace · ahead: a misted region*

The √ curves do the design work: each Verdure point costs more Renown than the last, each Kinship level more XP — **both permanent tracks flatten together**, so the forecast visibly decelerates while the fresh-run alternative (fast early points, compounding Almanac bonuses, the previewed modifier) grows relatively better the longer you linger. Renown lands in **chunks** as well as trickle — verse completions, familiar level-ups, Folio fixings, sketched portions carry fixed grants — so "stay for one more level-up" gives the curve texture. **Guard rail: the forecast sets timing only.** The Rite remains the sole gate; no minimum-Verdure or minimum-Kinship requirement, ever.

---

## 9 · Economy

### Money becomes XP (decided) — there is no Coin

| The wallet's old jobs | Owner                                                                            |
| --------------------- | ---------------------------------------------------------------------------------- |
| Tools                 | **Skill gates + material costs** (level + ingots; no wallet involved)               |
| Camp buildings        | **Material bundles** — Bushcraft is the construction backbone                       |
| Trail Maps            | **Provisions** — a goods bundle; you pack for the walk, you don't buy it            |
| Selling               | **The Exchange** — the caravan barters goods for goods. Rates are **always derived from the single trade-value table** (never authored per pair — hand-set pairs breed arbitrage), less a spread; small trades **round in the player's favour** — the caravan is dry, not petty. Off-zone inputs (berries for nuts) are its real job; trade goods remain the densest barter weight. |

**The pacing spine is XP**, on two tracks: warden craft XP (nine skills, gating tools and recipes) and familiar XP (gating powerups; earned at the post — *where the crew stands is how the run is spent*).

**The readability rule:** Coin was the single climbing number and the universal price signal. In its place: (1) **Renown is the ledger's big number** — always visible, always climbing; the Verdure input and the score; (2) trade-value weights keep a de facto price signal at the Exchange; (3) the Phase 1 gate asks *can a new player say what anything is worth?* Fallback if not: a cosmetic skin over Renown, never a returned wallet.

### Currencies

| Currency    | Role             | Sources                                                 | Sinks                                        |
| ----------- | ---------------- | ------------------------------------------------------- | --------------------------------------------- |
| **Renown**  | Per-run score    | All XP earned (warden + familiars) + offering credits   | None — the measure, not a wallet              |
| **Verdure** | Meta (permanent) | Migration (√ of Renown)                                 | Almanac nodes; +2%/pt passive                 |
| **Amber**   | Hard / premium   | IAP, observation sites, rewarded ads, weekly Play Games Reward  | Time-skips, cosmetics, extra craft queues     |
| *(Goods)*   | Everything else  | Gathering, crafting                                     | Kit · Exchange · offerings · replanting/planters · buildings · provisions — six sinks competing |

### Formulas

```
yield/sec         = Σ stationed agents · specMult · richnessMult(node) · planterMult
                    · toolMult · gearMult · (1 + 0.05·mastery) · global
global            = (1 + 0.02·Verdure) · almanac · museumSets · insects · boosts
richnessMult(node)= 1 + 0.10 · richnessLevel
replantCost(n, L) = base · r^L                      node's own resource; per node, per run
planterCost(tier) = material bundle                 authored per planter type
famXP/sec         = workRate · postMatch · comfort  postMatch >1 when specced for the node
famXPToLevel(L)   = 60 · 1.12^L                     powerup choice every 5 levels
kinshipGain(fam)  = floor( √( runFamXP / K_f ) )    at Migration; Kinship XP only — run XP
                                                    already credited Renown (no double count)
kinshipPerks(K)   = starting level +K · XP rate +2%·K    signature trait at milestones (1.1)
buildingCost(L)   = bundle(base) scaled 1.25^L      paid in goods
mapCost(zone)     = provisions bundle               authored per zone
exchangeRate(a→b) = tradeValue(a) / tradeValue(b) · (1 − spread)     spread ~15%, tuned;
                                                    rounding favours the player on small trades
toolGate(t)       = skill level threshold + ingot batch              each tier ×2 yield
xpToLevel(L)      = 100 · 1.10^L                    warden craft XP
Renown            = lifetime XP (warden + familiar) + offering credits (at trade value)
verdureGain       = floor( √( Renown / K ) )        K tuned to the XP scale
offlineEarn       = Σ per node: min(gather, trail rate) · min(t, cap)   wanderers ×0.5; unheld trail ×0.5 lane
pristineChance    = (0.5% + fieldPress + almanac) · (1 + tendingBonus)
sketchProgress    = watchers · siteSpeed · rarity              pity: portion sketched / 4 h watched
verseDemand(m)    = baseQty · d^m · modifierWeight
spotlight(m)      = rotate(crafts, m + regionSeed)
```

The √ Verdure curve keeps the when-to-reset decision legible (each ~4× Renown ≈ 2× Verdure); offerings crediting Renown in full means the Rite never taxes prestige; Kinship's matching √ means both permanent tracks decelerate in step (§8); the observation pity timer keeps the deep chase strictly fair.

### Run 1 Rite — paper prototype (placeholder quantities; structure is the shipped run-1 tutorial)

| Verse (site)                    | Spotlight             | Five slots — complete any 3                                                             |
| ------------------------------- | --------------------- | ---------------------------------------------------------------------------------------- |
| 1 · the fire circle, Sunfield   | Foraging              | Berries ×300 · wildflowers ×150 · fibres ×200 · Tend 25 times · 1 Fine specimen          |
| 2 · the hollow oak, Bramble     | Firecraft, Mining     | Berry Preserves ×8 · nuts ×400 · copper ingots ×5 · herbs ×300 · 1 Fine specimen         |
| 3 · the oldest root, Old-Growth | Bushcraft, Forgecraft | Planks ×20 · cordage ×12 · Skewers ×12 · bronze ingots ×4 · 1 field sketch (torn out — re-observe the portion) |
| 4 · the river bend, Silverrun   | Fishing               | Fish ×500 · Smoked Trout ×20 · clay ×300 · iron ingots ×6 · 1 Pristine specimen          |

### Pacing targets

| Moment                                          | Target time | Why                                                        |
| ----------------------------------------------- | ----------- | ----------------------------------------------------------- |
| First two familiars arrive (unasked)            | < 60 s      | The land's gesture, twice; one takes the trail              |
| First tool crafted (Flint Sickle)               | ~4 min      | First ×2 spike                                              |
| First verse revealed                            | ~5 min      | Goal structure visible in session one                       |
| First powerup chosen                            | ~8 min      | The build system's hook, set early                          |
| First replant                                   | ~10 min     | The fourth lane opens; the theme lands                      |
| Zone 2 + first waystone                         | ~12 min     | New content, first tone                                     |
| First recipe cooked                             | ~20 min     | Second system online                                        |
| Verse 1 complete                                | ~30 min     | First sign from the land — and it unlocks the gift event    |
| Third familiar gifted                           | ~45–60 min  | Assignment outgrows the crew; the puzzle begins             |
| Zone 3: Logging + first observation site                | ~50 min     | Parallel skills; insect hook                                |
| First portion sketched                          | ~Day 1      | The long chase starts                                       |
| Fold forecast visible (Almanac Desk)            | Day 1       | Meta-math revealed early                                    |
| Rite complete + first Migration (~10 Verdure)   | Day 1–2     | Hook set before the day-3 churn window                      |
| First Kinship conversion seen                   | Day 1–2     | The two-track promise proven at the first fold              |
| First plate completed (Stag's Herald)           | ~Week 1     | First lore payoff + big multiplier — and the first release |

---

## 10 · Progression

### First 30 named upgrades (level gates + materials; all bundles placeholder)

Familiar slots arrive by event (§4), never purchase; the **Hauling** track buys warden-built equipment the trail post works with — nothing ever buys a creature. Camp entries are debut levels of building lines (repeatable after, `bundle · 1.25^L`).

| #  | Upgrade                      | Track      | Gate            | Materials (placeholder)             | Effect                                              |
| --- | ---------------------------- | ---------- | --------------- | ------------------------------------ | ---------------------------------------------------- |
| 1  | Flint Sickle                 | Tools      | Foraging 2      | Surface flint ×3, fibres ×20         | Foraging yield ×2                                    |
| 2  | Waxed Satchel                | Hauling    | Foraging 4      | Fibres ×60, berries ×40 (wax)        | Carry capacity ×1.5                                  |
| 3  | Drying Rack                  | Camp       | —               | Fibres ×80, wildflowers ×30          | Berry barter weight +25%                             |
| 4  | Trail Map: Bramble Hedgerows | Trail      | Flint tools     | Provisions bundle                    | Unlock Zone 2 + Mining                               |
| 5  | Rawhide Gloves               | Tools      | Foraging 8      | Fibres ×120, nuts ×60                | Hedgerow foraging ×2                                 |
| 6  | Handcart                     | Hauling    | Bushcraft-ready | Timber via Exchange, fibres ×150     | Carry capacity ×2                                    |
| 7  | Camp Fire Ring               | Camp       | —               | Stone ×40, fibres ×100               | Unlock Firecraft + Forgecraft (copper on open fire) + Preserve recipe |
| 8  | Copper Sickle                | Tools      | Foraging 12     | Copper ingots ×5                     | Foraging yield ×2                                    |
| 9  | Root Cellar                  | Camp       | —               | Stone ×80, timber ×20 (Exchange)     | Offline cap 4 h → 6 h                                |
| 10 | Preserving Jars              | Firecraft  | Firecraft 10    | Clay via Exchange, herbs ×80         | Preserve barter weight +50%                          |
| 11 | Trail Map: Old-Growth Wood   | Trail      | Copper tools    | Provisions bundle                    | Unlock Zone 3 + Logging + **first observation site**         |
| 12 | Bronze Hatchet               | Tools      | Logging 5       | Bronze ingots ×5                     | Logging yield ×2                                     |
| 13 | Stag Harness                 | Hauling    | Bushcraft 12    | Cordage ×10, timber ×40              | Carry capacity ×2                                    |
| 14 | Carving Bench                | Camp       | —               | Timber ×60, stone ×30                | Unlock Bushcraft + Plank, Cordage & **Planter** recipes |
| 15 | Whetstone                    | Tools      | Mining 10       | Stone ×50, iron gravel ×10           | All gathering yield +25%                             |
| 16 | Forager's Skewers            | Firecraft  | Firecraft 18    | Timber ×30, herbs ×120               | Mushroom Skewer recipe                               |
| 17 | Field Press                  | Compendium | Curation 5      | Planks ×15, cordage ×6               | Pristine chance +1pt                                 |
| 18 | Bellows Forge                | Camp       | Forgecraft 15   | Planks ×30, clay ×80, copper ×10     | Forge L2: iron heat; Forgecraft speed ×2             |
| 19 | Wagon                        | Hauling    | Bushcraft 20    | Planks ×40, iron fittings ×4         | Carry capacity ×2                                    |
| 20 | Smokehouse                   | Camp       | —               | Planks ×50, clay ×120, stone ×60     | Offline cap 6 h → 8 h                                |
| 21 | Trail Map: Silverrun River   | Trail      | Bronze tools    | Provisions bundle                    | Unlock Zone 4 + Fishing + riverbank observation site         |
| 22 | Iron Toolset                 | Tools      | 3 crafts @ 20   | Iron ingots ×8                       | Foraging, Logging & Mining ×2                        |
| 23 | Brush Screens                | Entomology | Entomology 8    | Reeds ×80, planks ×20                | Observation speed ×2                                         |
| 24 | Smoking Racks + Willow Rod   | Firecraft  | Firecraft 28    | Timber ×80, reeds ×60                | Smoked Trout recipe; Fishing ×2                      |
| 25 | Reed Weaving                 | Bushcraft  | Bushcraft 30    | Reeds ×150, cordage ×20              | Reed Basket recipe (densest barter weight)           |
| 26 | Pressing Boards              | Compendium | Curation 15     | Planks ×60, iron fittings ×6         | Folio spread bonuses ×1.5                            |
| 27 | Steel Toolset                | Tools      | Forgecraft 30   | Iron ingots ×10, charcoal ×20        | All gathering ×2                                     |
| 28 | Raven Panniers               | Hauling    | Bushcraft 35    | Cordage ×30, planks ×30              | Carry capacity ×2                                    |
| 29 | Almanac Desk                 | Camp       | —               | Planks ×80, iron fittings ×10        | The fold forecast (§8 decision surface)              |
| 30 | Trail Map: Mistfen Marsh     | Trail      | Iron tools      | Provisions bundle                    | Unlock Zone 5 (v1.1 gate)                            |

> **Provisions:** each Trail Map's bundle is authored per zone (Zone 2: Berry Preserves ×4 + fibres ×50 · Zone 3: Preserves ×10 + copper ingots ×2 + cordage ×6 · Zone 4: Skewers ×10 + planks ×12 + bronze ingots ×2). Early bundles lean on the Exchange for off-zone items — teaching the barter loop.
> **First-hour achievements** (Quest eligibility needs 4): *First Harvest* · *First Friends* (roster of 3) · *Off the Beaten Path* (Zone 2) · *Fire & Fruit* (cook a recipe). *First Verse* (~30 min) and *Green Hands* (first replant, ~10 min) in reserve.

### Camp buildings — the repeatable goods sink

Named entries above are debut levels; each further level costs an escalating material bundle (`1.25^L`) forever — the bottomless honest sink, paid in goods and therefore competing with the other five goods lanes.

| Line                 | Debut                          | Levels grant                                                            |
| -------------------- | ------------------------------ | ------------------------------------------------------------------------ |
| **The Fire**         | #7 Camp Fire Ring              | Fire recipes; copper heat from L1; then fire craft speed                 |
| **The Forge**        | Clay Furnace (between #11–12)  | L1 bronze heat · L2 (#18) iron heat · L3+ forge speed                    |
| **The Bench**        | #14 Carving Bench              | Bench recipes incl. planters; then queue speed                           |
| **The Store**        | #9 Root Cellar → #20 Smokehouse| Offline cap; then storage capacity                                       |
| **Roosts & Burrows** | ~early debut                   | **Familiar comfort**: +XP rate per level; late levels +1 roster capacity |

---

## 11 · Monetization

### The Egg Inc posture

Free, generous, player-initiated. The gathering loop is never interrupted by ads; the story is never sold.

**Rewarded video:** ×2 yields and XP rates for 4 min (stackable to 1 h) · double offline earnings on return (the single highest-value placement) · instant-finish a craft queue · small Amber drip. *(Time-boxed boosts may touch XP; permanent multipliers never do — next line.)*

**IAP:** **Warden's Sigil** — permanent ×2 yields and craft speed (~US$7); **permanent XP multipliers are never sold** — familiar growth, Kinship, and Renown pacing stay money-clean · remove ads · Amber packs · starter bundle · cosmetic camp, gear & familiar skins (skins never touch Kinship or builds).

**Never sold: the Rite — and never sold: a creature, a Kinship level, or a build.** Amber accelerates production; it cannot fill a verse slot, recruit a familiar, or level one.

**Play Games Rewards (Level Up requirement, not IAP):** Wayfarer's Cloak (cosmetic) + **Spare Wing** (+1 trail post) single-use by Sep 30 2026 · Weekly Amber Cache (20, max 1/wk) repeatable by Mar 1 2027.

---

## 12 · Level Up compliance

| Requirement                                                   | Wildgrove answer                                                                                                              | Phase |
| -------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------- | ----- |
| PGS v2 SDK, init at startup                                   | Unity plugin, initialized in bootstrap scene                                                                                    | 0     |
| Achievements (10 min / 40+ rec / 4 in first hour)             | 40+ from zones, crafts, spreads, plates, verses, Rites, Migrations, Kinship milestones, powerup builds; first-hour four per §10   | 5     |
| Game Stats (≥5 repetitive, ≥1 competitive, ≥1 progression)    | Resources gathered (competitive) · deepest zone, Rites completed (progression) · verses · recipes · specimens · insect plates · Migrations · Renown earned | 5     |
| Cloud save + conflict policy                                  | Saved Games API (<100 KB); conflict = highest lifetime Renown wins, prompt on tie                                               | 5     |
| Sidekick overlay                                              | App Bundle + Play Console toggle; test early                                                                                    | 5     |
| Rewards items (2 single-use / 1 repeatable)                   | Cloak, Spare Wing, Weekly Amber Cache                                                                                           | 6     |
| Vulkan primary (Unity 2021+)                                  | Unity 6 LTS + URP, Vulkan first from day one                                                                                    | 0     |
| 60 fps (avg ≥55 / P90 ≥50 / P99 ≥30)                          | 2D URP; frame budget checked each phase gate                                                                                    | all   |
| Stability <1% crash / <2% ANR                                 | Crashlytics from Phase 1; vitals gate before launch                                                                             | all   |
| Large screens, no letterboxing (4:3 / 16:10 / 21:9 + portrait)| Adaptive UI is Phase 2, not post-launch polish: portrait column ↔ landscape dashboard                                           | 2     |
| Play Games on PC                                              | Idle UI suits PC; no touch-only features; opt in at beta                                                                        | 6     |
| Full keyboard/mouse + controller                              | Input abstraction from Phase 1: Tending = tap/click/Space/pad-A; full menu navigation                                           | 2     |
| Title availability parity                                     | Play-only launch across mobile/tablet/PC                                                                                        | 6     |

> **Leaderboard integrity:** ship Play Integrity API checks and server-side sanity bounds (max plausible Renown/hour) before any competitive stat goes live.

Reference: [Google Play Level Up guidelines](https://developer.android.com/games/guidelines)

---

## 13 · MVP development plan

Solo, part-time. Each phase ends at a **gate** — a concrete question answered before more is built. MVP = zones 1–4, nine crafts, the small flock with powerups and Kinship, the warden's kit, the ore→ingot→tool chain, replanting & planters, the Exchange, the four-verse Rite (authored + generator), 6 insects, Migration + 12-node Almanac, 1–2 bonded familiars, Compendium v1 with roster plates, ~1,200 words, a launch onboarding pass, full PGS layer, monetization.

### Phase 0 — Foundations (1–2 wks)

- Unity 6 LTS, URP 2D, **Vulkan first**; verify on a real device
- Git + GitHub Actions Android (AAB) build; Play Console internal track live with a walking skeleton
- Data-driven content: resources/upgrades/recipes/gear/planters/species-pools/rites/dialogue as JSON in `design/data/`, validated into a ScriptableObject database at editor-load/build — balancing and writing never require code changes
- BreakInfinity numbers; versioned local JSON save with migration hooks
- PGS v2 SDK initializing (sign-in only)

**Gate:** a signed AAB installs from the internal track, signs into Play Games, renders 60 fps under Vulkan.

### Phase 1 — Core loop slice (3–4 wks)

- Sunfield only: two nodes, stationing (warden + the first two familiars, one holding the trail post), Tending, replanting, familiar XP with the first powerup choice, the Exchange with two tradeable goods
- Offline progress (4 h cap, stationing rules §2) + a welcome-back summary that names what limited the night (haul cap; wanderers covering an unheld trail)
- Sunfield-reachable upgrades wired to data; placeholder art, real numbers; input abstraction (touch now, K&M/pad later); Crashlytics + basic analytics

**Gate (two questions):** *is 20 minutes fun?* — hand it to 3–5 people, watch where they stall — and *can a new player say what anything is worth without Coin?* If either fails with placeholder art, stop and fix; content won't save it.

### Phase 2 — Adaptive UI & input (2–3 wks)

- The four-page journal: **Trail** (zones, stationing, replanting, verse sites; the map is the page's own navigation) · **Camp** (queues, buildings, Exchange) · **Warden** (kit, skills, roster & slots, stats; the Almanac appears here after the first Migration) · **Record** (Compendium, Folio, Deep Pages). The Rite has **no tab** — verses live at their sites, with the compact tracker pinned on every page. *(The map is never called "Almanac.")*
- Responsive: portrait column ↔ landscape spread (Trail permanent right page; Camp/Warden/Record turn left); test 4:3, 16:10, 21:9, cutouts, foldable resize
- Keyboard/mouse + controller: every interaction reachable without touch; focus states; gamepad manifest
- Frame-budget pass on a mid-tier reference device

**Gate:** fully playable with a pad and with K&M on a 16:10 tablet window, no letterboxing, no touch fallbacks.

### Phase 3 — Systems build-out (7–9 wks)

- Zones 2–4; Foraging/Logging/Fishing/Mining with XP, levels, per-resource mastery
- Firecraft + Forgecraft + Bushcraft queues; ore→ingot→tool tiers; trade-good chains; the five kit items; **planter recipes + richness curves**
- Camp building lines + station gating (`buildings.json`), costed in material bundles; Roosts as comfort
- **The Exchange in full:** derived rate table, spread, player-favour rounding; provisions bundles
- **Familiar system in full:** XP at post, deterministic species pools (content-filtered), powerup choice UI, roster & fielding
- **The Rite, authored run 1:** verse sites, offering delivery, the pinned tracker; reachability made stationing-aware — a slot counts only if its good's raw-input footprint fits the crew's gather posts (per-slot; `RiteGenerator.StationingFootprint` ≤ `CrewGatherPosts`), enforced in the generator's candidate picks and the runs-2–10 ≥3-reachable proof
- Observation as **observe · sketch · release**: two observation sites, portion pity timer, three insect plates, the release beat (sign-style, no words); Compendium v1 incl. roster plates; quality rolls per haul batch; Folio spreads
- Familiar world-sprites (static + light bob is fine) — creatures at posts, the trail post's runner on the trail, baskets overflowing
- Waystones 1–4, verse lines, caravan lines + the **teaching pass** (each system's first margin note is its instruction, §7)
- Upgrades 4–30 recosted; balance spreadsheet vs. §9 pacing targets — including Rite demands, **offline magnitude for a small crew**, and the **hour-six spend proof** (a run's sixth hour must always have a meaningful next purchase). Seed model shipped with this revision (`economy-model.xlsx`): the proof **passes on defaults**, with the building lines holding the 10–90 min purchase band all run; per-level replants go trivial after H2, so raise `r` toward ~1.5 or sell replants in batches before this becomes the late-run lever. Solved starting constants: K ≈ 425, K_f ≈ 650 (derived from the pacing targets, recomputed automatically as inputs move)

**Gate:** pacing table holds ±30% through hour one; a bar is always filling — checked on the **Trail and Camp pages separately**; verse 1 completes unprompted; at least one tester asks what the Long Winter is.

### Phase 4 — Prestige (3–4 wks)

- Migration flow gated by the completed Rite: the **fold forecast** (Verdure + per-familiar Kinship + next-point ETA + region preview, one panel), the vignette, a deliberate confirm — players fear their first prestige; sell it hard
- **Kinship**: conversion at the fold, perk application (starting level, XP rate), roster persistence and the reunion beat
- **Rite generator** (behind the authored Rite as fallback): demands from migration count × region modifier × unlocked content; spreadsheet-verify runs 2–5 before wiring UI
- Almanac tree (12 nodes, incl. Gatherer 3 and The First Planting); region modifiers; first bonded familiar; kit re-craft tuned to ~2 minutes
- Second-run tuning: run 2 reaches the old wall in ~⅓ the time

**Gate:** testers migrate voluntarily; run 2 feels faster, worth it, and asks something different; the crew's return feels like a reunion, not a re-grind; and testers can articulate what staying another hour would have bought — the unified forecast doing its job.

### Phase 5 — Play Games layer (2–3 wks)

- Cloud save (Saved Games API) + conflict policy (highest lifetime Renown; prompt on ambiguity)
- 40+ achievements (first-hour four verified), 5+ Game Stats; Sidekick enabled and tested; leaderboards (deepest zone, weekly resources)
- Play Integrity + save sanity bounds before leaderboards go live

**Gate:** uninstall/reinstall on a second device restores progress perfectly; achievements and stats visible in Sidekick.

### Phase 6 — Monetization, beta & launch (3–4 wks)

- AdMob rewarded placements + IAP (Sigil per §11's XP rule, remove-ads, Amber) + Play Games Rewards items
- Plate illustration pass (~60 plates, per-zone batches) + backdrops; final narrative edit (cut 20% — the budget is a ceiling)
- **Onboarding pass (final step before launch):** a light tutorial layer — the Phase 3 teaching notes verified against beta FTUE analytics, plus contextual first-time nudges (a soft mark on the first Tend, the first replant, the first powerup and fielding choice) that stay inside the no-popup, two-lines-on-screen tone; the first-hour funnel must be green before ship
- Store listing (tablet/PC screenshots); closed beta, 2–3 weeks of vitals; Play Games on PC opt-in; Level Up self-check; launch

**Gate:** vitals green 14 consecutive days and D1 retention >30% in beta → ship.

**Total: roughly 6–8 months part-time.** The two classic solo-dev failure modes this plan defends against: building content before the loop is proven fun (Phase 1 gate), and treating form-factor/input as launch polish (Phase 2 exists because Level Up makes it compliance).

---

## 14 · Open questions

**Before Phase 1 ends**
- **Readability without Coin** — the gate question. Fallback: a cosmetic skin over Renown, never a wallet.
- **Active-play depth:** ship Tending only; prototype the hold-still-to-sketch observation reveal at 1.1 — doubly tempting now, since the sketch *is* the insect moment and release gives it an ending.
- **Name:** "Wildgrove" is a working title — check Play Store collisions and trademark before the listing.

**Before Phase 3**
- **Species pool contents:** author the deterministic pools (structure is decided; the entries are a writing/balance task).
- **Planter caps:** per-run richness cap, and whether the self-funding loop (berries→berries) needs a clamp beyond the cost curve. Spreadsheet proof alongside the generator's.
- **Exchange spread value:** ~15% starting point — big enough to stop arbitrage hoarding, small enough to feel generous.
- **Offline magnitude:** per-agent base rates and caps for a ≤5-agent crew — a night away must still feel generous.
- **Spare Wing verification:** hauling equipment (#2, #6, #13, #19, #28) tuned assuming two trail posts eventually exist; verify the bottleneck triangle survives the reward.
- **Amber earn rate:** lean generous — ~40/week free between dig finds and the weekly cache.

**Before Phase 4**
- **Kinship K_f:** tune so Kinship rewards seasons, not marathon single runs.
- **Roster pacing:** lean 5–6 members by Migration 3 — enough that fielding is a choice, few enough that each plate is an event.
- **Bonded companion numbers:** Mo to finalize. Working assumptions until then: 1–2 bondable at MVP; 1 earned per 2–3 Migrations early, slower after.
- **Signature trait milestones (1.1):** which Kinship levels; one trait per familiar, ever — identity, not a second build slot.
- **Generator guardrails:** slot spread, spotlight-vs-unlock order, quantity clamps, final-verse reachability under stationing, and powerup-pool coverage (simplified by deterministic pools) — spreadsheet proof across runs 2–10 before it ships.

**Carried**
- **Narrative volume:** 1,200 words is a ceiling. If playtesters want more story, the answer is more insects, not more words per insect.
- **Waystone vs. verse-site legibility:** the past and the present must read as distinct objects on a zone screen. Check in the Phase 3 playtest.
