# Wildgrove — Design Document

*Design document v0.5 · July 2026 · Working title*

> An idle gathering & collecting game about tending a wilderness that remembers. Walk deeper, craft to survive, unearth what sleeps beneath — and listen when the land speaks, which is rarely.

## Contents

1. [Vision & pillars](#1--vision--pillars)
2. [Core loop](#2--core-loop)
3. [The Trail — zones](#3--the-trail)
4. [Crafts & survival gear](#4--crafts)
5. [Compendium, Museum & fossils](#5--collection)
6. [Narrative & tone](#6--narrative--tone)
7. [Prestige — the Rite & Migration](#7--prestige)
8. [Economy math](#8--economy)
9. [First 30 upgrades](#9--progression)
10. [Monetization](#10--monetization)
11. [Level Up compliance](#11--level-up-compliance)
12. [MVP development plan](#12--mvp-development-plan)
13. [Open questions](#13--open-questions)

---

## 1 · Vision & pillars

### Tend, don't take

You are the warden of a small camp on the edge of an old, patient wilderness — and you are the only human in it. What company you have, the land sends: **familiars**, wild creatures that drift into camp when a warden works carefully. Voles forage the meadow, ravens carry the harvest home, an otter fishes better than you ever will. Around the fire you craft what they bring into the gear and goods a camp needs to push deeper. Some finds are **Pristine**: specimens worth keeping forever. Some are older than that — **fossils**, surfacing from the soil, each one a sentence in a story nobody finishes telling you. And each region asks something back: a **Rite**, performed verse by verse at quiet places, offerings set down for spirits you never see — the land deciding whether you may move on. When the Rite is done and the season turns, the camp **migrates**, the familiars slip back into the grass, and the land remembers how you treated it.

The fantasy is *stewardship, not extraction*. You are a guest who works hard. The tone is warm, quiet, and slightly haunted.

| Axis | Lineage | Description |
|---|---|---|
| **The Trail** | Spine · Idle Obelisk | Push deeper through eight zones. Each opens new resources, a new craft, a waystone inscription, and — from Zone 3 — dig sites. |
| **Crafts** | Breadth · Melvor Idle | Parallel gathering and survival-crafting skills, each with XP, mastery, tools, and gear recipes. |
| **The Rite** | Gate · Idle Obelisk (the obelisk, minus the fighting) | Each region sets a staged ritual of offerings — five slots a verse, any three to complete. Rotating spotlight crafts change what each run asks of you; the finished Rite is what unlocks Migration. |
| **Migration** | Meta · Egg Inc | Prestige resets the region and banks Verdure — the land's memory of careful hands — into the permanent Almanac. |

**The collecting identity:** the Compendium (living things, your familiars among them) and the Fossil Wing (dead ages) are the axes that never reset. They carry the set bonuses, the 40+ achievements, and nearly all of the story.

### Art direction — the naturalist's plates (decided)

- **Every collectible is a plate.** Compendium entries, fossil cards, and gear are hand-drawn naturalist illustrations: fine ink linework, muted watercolour and sepia washes, numbered figures, small hand-written margin annotations. The Compendium reads like a 19th-century field guide someone loved.
- **UI is the warden's field journal.** Paper grounds, journal tabs, stitched spines; restrained — texture as identity, never as noise.
- **Zones are the stage, plates are the star.** World scenes stay simple: layered painted-parallax backdrops with light ambient motion (drifting pollen, river shimmer). The illustration budget concentrates where the collecting identity lives.
- **Solo-scale production:** one fixed plate template (frame, figure number, nameplate) so each new card costs only the specimen drawing. Target ~60 plates at MVP (~40 Compendium, including ~8 familiar species; 6 fossils; 5 gear; zone headers), produced in per-zone batches. Scope pressure cuts *plate count*, never plate quality.

---

## 2 · Core loop

### Gather → haul → craft → offer

```
[WILDS]                    [TRAIL]              [CAMP]
Familiars gather      ──►  Carriers haul  ──►  Fire, forge & bench  ──►  goods
(idle + the warden's       (throughput          │
 Tending)                   bottleneck)         ├─► GEAR (the warden's kit)
     ▲                                          ├─► TRADE GOODS ──► Provisioner ──► Coin
     │                                          └─► OFFERINGS ──► the current verse of the Rite
     └────────── gifts, tools, gear, camp, trail maps ◄──────────┘
```

1. **Familiars** work resource nodes automatically — wild helpers attracted by **gifts**, small piles of goods left at the node (cost curve in §8). Every region opens with one gatherer and one carrier already helping — the land's first gesture, and the economy's bootstrap: goods buy gifts, so the loop must never start (or restart after Migration) at zero. **Tending** a node (tap / click / key) — clearing brush, turning soil — is the warden's own act: a burst of yield and briefly raised Pristine chance. Attention is rewarded, never demanded.
2. Harvest travels home by **carrier familiars** — pack ravens, dray stags. Under-invest in carriers and baskets overflow at the node — the visible bottleneck tension.
3. The **fire and benches** turn raw finds into three kinds of output: **survival gear** the warden equips (cordage, torches, oilskins — permanent-for-the-run buffs), **trade goods** (preserves, planks, baskets) worth far more than raw materials, and **offerings** consumed by the current verse of the Rite (§7).
4. The **Provisioner** — a silent, recurring caravan — buys trade goods for Coin. Coin funds tools, camp buildings, and **Trail Maps**. Deeper zones demand better tools *and* better gear → invest → advance. Repeat.

> **Design tension to preserve:** gather rate vs. haul rate vs. crafting rate stays slightly out of balance — upgrading one always exposes the next bottleneck. And the craft split is now a three-way dilemma: craft for the kit, for the caravan, or for the spirits?

---

## 3 · The Trail

### Eight zones, four at MVP

| # | Zone | Resources | Unlocks | Keystone specimen | Scope |
|---|---|---|---|---|---|
| 1 | **Sunfield Meadow** | Berries, wildflowers, fibres | Foraging (start) | Sunburst Poppy | MVP |
| 2 | **Bramble Hedgerows** | Nuts, herbs, copper scree | Firecraft (the camp fire), Mining | Amber-Shelled Snail | MVP |
| 3 | **Old-Growth Wood** | Deadfall timber, mushrooms, tin seams | Logging, Bushcraft, **first dig site** | Ancient Acorn | MVP |
| 4 | **Silverrun River** | Fish, reeds, clay, iron-rich gravel | Fishing, riverbank dig site | Moonscale Trout | MVP |
| 5 | **Mistfen Marsh** | Peat, rare herbs, fireflies | Entomology, Apothecary | Lantern Firefly | v1.1 |
| 6 | **The Hollows** | Deep ores, crystals, bone beds | Delving; the richest fossil strata; shafts nobody living dug | Echo Geode | v1.1 |
| 7 | **Highland Crags** | Eggs, wool, lichen | Husbandry | Cloudfleece Ram | v1.2 |
| 8 | **Cloudreach Peaks** | Sky-blossoms, glacier ice | The final waystones (endgame) | Aurora Bloom | v1.2 |

Each zone is a screen: 2–3 nodes, the trail home, one **keystone specimen** (a guaranteed rare find gating a milestone achievement), one **waystone** — a standing stone whose inscription is revealed on arrival (§6) — and one **verse site**: the quiet place (a fire circle, the oldest tree, a river bend) where that zone's verse of the Rite is revealed and its offerings set down (§7). From Zone 3 onward, zones also hold a **dig site**. Zone entry costs a Trail Map (Coin) *and* a tool tier (Zone 2: flint · Zone 3: copper · Zone 4: bronze · Zone 5: iron · deeper: steel+), so Trail progress is paced by both economy and craft investment.

---

## 4 · Crafts

### Nine crafts at MVP, twelve at 1.2

| Craft | Type | Feeds on | Produces | Scope |
|---|---|---|---|---|
| **Foraging** | Gathering | Meadow, Hedgerow nodes | Berries, flowers, fibres, nuts, herbs | MVP |
| **Logging** | Gathering | Old-Growth deadfall | Timber, mushrooms (by-find) | MVP |
| **Fishing** | Gathering | Silverrun nodes | Fish, reeds, clay | MVP |
| **Mining** | Gathering | Scree, seams, gravel (Zones 2–4) | Copper, tin, iron ore; flint, stone | MVP |
| **Firecraft** | Survival crafting | Berries, nuts, mushrooms, fish | Preserves, skewers, meals; torches | MVP |
| **Forgecraft** | Survival crafting | Ores, timber (charcoal), clay | Ingots → every tool tier; fittings | MVP |
| **Bushcraft** | Survival crafting | Timber, fibres, reeds, clay | Cordage, planks, baskets; gear | MVP |
| **Excavation** | Collection | Dig sites (Zones 3+) | Fossil fragments, amber | MVP |
| **Curation** | Collection | Pristine specimens, fossils | Museum donations, set bonuses | MVP |
| **Entomology** | Gathering | Marsh nodes | Insects (pure collection value) | v1.1 |
| **Apothecary** | Survival crafting | Herbs, peat, fungi | Tinctures (buff consumables) | v1.1 |
| **Husbandry** | Gathering | Crag nodes | Eggs, wool | v1.2 |

### Skill structure (per craft)

- **Skill level** 1–99, XP from every action. Levels gate recipes and tool tiers.
- **Mastery** per resource: each mastery level gives +5% yield/value for that resource. The long-tail chase.
- **Tools are mined, smelted, and smithed — never bought.** Tier 1 is knapped flint (no forge). Every tier after needs Coin *plus* ingots at the forge: Copper (Zone 2 scree) → Bronze (add Zone 3 tin) → Iron (Zone 4 gravel) → Steel (charcoal + iron) → deep ores (Hollows, v1.1). Each tier ×2 yield, ~12× the cost of the last. **Mining/Forgecraft is the unlock backbone:** zones gate ores, ores gate tools, tools gate zones.
- One gathering craft runs per zone's **flock** (its familiars); crafting runs in parallel via fire/bench queues — several bars always filling, the Melvor feel. Base queue counts are tuned so gear, trade goods and offerings genuinely compete (§2's three-way dilemma) without any lane starving.

### Survival gear (the warden's kit)

Three kit slots — **Hands**, **Pack**, **Camp** — filled by crafted items and worn by the warden alone. The kit persists for the run and is rebuilt cheaply after Migration (an early-run ritual that makes the survival fantasy tactile).

| Gear (MVP) | Slot | Craft | Materials | Effect |
|---|---|---|---|---|
| Cordage Wraps | Hands | Bushcraft | Fibres ×40 | Tending burst +50% |
| Birch Frame Pack | Pack | Bushcraft | Timber ×25, cordage ×5 | Carrier capacity +25% |
| Pitch Torch | Camp | Firecraft | Timber ×10, fibres ×10 | Night hours count fully offline |
| Oilskin Tarp | Camp | Bushcraft | Reeds ×30, fish oil ×5 | Offline cap +2 h |
| Clay-Lined Creel | Pack | Bushcraft | Clay ×20, reeds ×15 | Fish never spoil in transit |

### Recipe chains (MVP trade goods)

```
Berries + Nuts        ──►  Fire      ──►  Berry Preserve     (×4 value)
Mushrooms + Berries   ──►  Fire      ──►  Forager's Skewer   (×5 value)
Fish                  ──►  Fire      ──►  Smoked Trout       (×5 value)
Timber                ──►  Bench     ──►  Planks             (×3 value)
Planks + Reeds        ──►  Bench     ──►  Reed Baskets       (×6 value)

Copper Scree          ──►  Fire      ──►  Copper Ingots  ─┐
Tin + Copper          ──►  Forge     ──►  Bronze Ingots  ─┼─►  Tool tiers & fittings
Iron Gravel + Charcoal──►  Forge     ──►  Iron Ingots    ─┘   (equip, don't sell)
```

---

## 5 · Collection

### Compendium, Museum & the Fossil Wing

Every gatherable, creature, and recipe has a Compendium entry — a hand-illustrated plate, a line or two of text, lifetime counters. Befriended familiar species get plates of their own — the game's warmest pages. Finds roll a quality: **Common** (96%), **Fine** (~3.5%, +50% value), **Pristine** (~0.5% base, upgradeable). Quality rolls happen per **haul batch** (a carrier delivery), not per unit — at idle rates a per-unit roll would shower Pristines and cheapen the windfall. Pristine finds can be sold to the Provisioner for a windfall — donated — or **offered**: some verse slots ask for a Fine or Pristine specimen, making the windfall a three-way choice.

- **Museum sets** group 4–8 related entries ("Meadow Blooms," "River Catch"). Completing a set grants a *permanent* bonus that survives Migration (+% yield, +Pristine chance, +offline cap).
- **Donation is a real choice**: run-speed now versus permanence.

### Fossils — the deep chase

From Zone 3, **dig sites** appear: familiars set to Excavation slowly turn soil, surfacing amber and **fossil fragments**. Fossils assemble from 3–5 fragments each; deeper zones hold older strata and rarer beds. Each completed fossil:

- grants a **large permanent multiplier** (this is the prophecy-egg analog — the months-long chase), and
- unlocks a **fossil card** — the game's primary lore vehicle (§6). The bones tell the story; nothing else will.

| Fossil set (MVP) | Pieces | Where | Bonus | What it whispers |
|---|---|---|---|---|
| **The Antler Crown** | 3 | Old-Growth roots | +10% all yields | A great elk. Something hunted *it*. |
| **The Sunken Jaw** | 4 | Riverbank | +15% fishing, +1% Pristine | A river leviathan. The Silverrun was once an ocean. |
| **Those Who Planted** | 5 | Both sites, rare beds | +20% all yields | Tools. Worked stone. Hands like yours, much older. |
| *The Long Winter strata* | — | Hollows (v1.1) | — | An ash layer above every other bed. Every set below it stops there. |

Target: ~6 fossils at MVP, ~30 by 1.2 — each a multiplier *and* a chapter.

---

## 6 · Narrative & tone

### The land speaks sparingly

No cutscenes, no quest log, no exposition. The story arrives the way it does in Dark Souls: through item descriptions, terse strangers, and places that imply more than they say. A player who ignores every word still has a complete idle game; a player who reads everything assembles something quietly devastating.

### Five delivery channels

- **Waystones** — one per zone, an inscription revealed on arrival. Two lines, never more. Weathered, second-person, addressed to wardens in general — the *past* speaking. You are clearly not the first.
- **Verses** — the living land's asks, the *present* speaking. Each verse of the Rite is a single line naming its offerings — the closest the spirits ever come to speech. **No spirit is ever seen.** A completed verse is answered with a sign — the wind turns, the fireflies gather, the river runs quiet for a moment — never with words.
- **Fossil cards** — the load-bearing lore. Each completed fossil adds a card written like a field note that trails off. The Long Winter is only ever visible as an absence: sets that stop, strata that end.
- **The Provisioner** — the silent caravan speaks one line per visit, maybe. Dry, oblique, faintly amused. Never answers a question the game lets you ask.
- **Migration vignettes** — three lines over a dark screen as the camp folds. The only scripted "cinematic" beat, and it is twelve words long.

### Voice samples

> Walk gently. The meadow fed them, too, and they are under it.
> — *Waystone · Sunfield Meadow*

> These roots drink deeper than you will ever dig. Let them.
> — *Waystone · Old-Growth Wood*

> What you carry is borrowed. Set some down, and see what follows you home.
> — *First verse · the fire circle, Sunfield Meadow*

> A crown of bone for a king of no kingdom. The meadow remembers being afraid, and will not say of what.
> — *Fossil card · The Antler Crown*

> The stone does not grow back. Take it like you mean it.
> — *Waystone · the copper scree, Bramble Hedgerows*

> Sell me nothing you would weep to lose. I do not give refunds, and the land does not either.
> — *The Provisioner*

> The last warden asked fewer questions. Hm. Perhaps that was the trouble.
> — *The Provisioner, much later*

> The camp folds. The land exhales.
> What you gave, it keeps.
> — *Migration vignette*

### The authorial truth (dev-facing — never stated in game)

> Long before the game begins, a civilization — *Those Who Planted* — worked this land the way the player does, and did not stop. Their taking outran the land's giving, and the Long Winter answered: the ash layer above every fossil bed. The wilderness that regrew is not wilderness; it is a survivor, and it is watching. The wardens are its long experiment in trying again — Verdure is literally the land's memory of careful hands, the familiars it sends are how it watches the experiment up close, and Migration happens on the land's terms, not the camp's: that is what the Rite is. The late-game implication, assembled only by completing the deep fossil sets and the final waystones: the warden order descends from Those Who Planted. You are the apology.

### Writing rules

- Total word budget at MVP: **~1,200 words**, verse lines included. Scarcity is the aesthetic; every added line cheapens the rest.
- Max two lines on screen at once. Everything skippable, everything re-readable in the Compendium.
- Proper nouns are never explained (*the Long Winter, Those Who Planted, the First Warden*). Questions are answered only by other questions, three zones later.
- Nothing hostile, nothing gory. The dread is geological — patient and mostly kind. Tone target: a nature documentary narrated by someone grieving politely.

---

## 7 · Prestige

### The Rite — the region's exit gate

Each region sets a **Rite**: a staged ritual of offerings, and the only way out.

- **Verses.** The Rite is performed in verses, one revealed per zone at its verse site (four at MVP) — so the bar is visible and chippable from the first session, not a checkout screen at the end of the run. Revealed verses may be worked in parallel and in any order; Migration requires **every** revealed verse complete, so rushing to the last zone doesn't skip the Rite.
- **Choose 3 of 5.** A verse presents five offering slots — quantities of raw finds, crafted goods, occasionally a Fine or Pristine specimen — and completing **any three** finishes the verse. Slots are weighted so one or two **spotlight crafts** are the cheapest path; a player who hates a spotlight craft can grind an off-spotlight slot at a premium. The spotlight rotates run to run, changing what each region asks of you.
- **Offerings.** Delivered incrementally and consumed on delivery — the verse bar is this game's obelisk. Offerings **credit Renown at full trade value** (§8): you give up Coin liquidity, never prestige progress. Never-sold offerables (ingots, gear-grade goods) credit the summed trade value of their inputs; deed and specimen slots (Tend N times, a fossil fragment, a Pristine) carry a fixed Renown grant set per slot. Gifts, by contrast, earn no Renown — feeding the spirits is remembered, feeding the voles is just lunch. Verse progress persists within a run; the two unchosen slots simply expire, no partial credit.
- **The gate, not the timer.** Completing the final verse means the land consents: Migration unlocks. Verdure keeps scaling with lifetime Renown after the gate opens, so *when* to leave stays a real decision — the Rite sets eligibility, the player sets timing.
- **Authored once, generated after.** Run 1's Rite is hand-authored as the tutorial (`design/data/rites.json`; verse lines in `dialogue.json`, verse sites in `zones.json`). From run 2, a generator builds each Rite from migration count + region modifier + unlocked content (§8); the data validator guarantees at least three reachable slots per verse — a Rite can never hard-stick a run.
- **Not for sale.** Amber accelerates production; it can never fill a verse slot directly (§10).

### Migration

When the Rite is complete and the region slows, the camp folds and **migrates**. Coin, familiars, tools, gear, zone progress, and skill levels reset. You keep: the Compendium, the Museum, every fossil, Amber, and newly-banked **Verdure**.

- **Verdure** — the land's memory of careful hands — is earned from lifetime Renown (≈ lifetime Coin earned this run, offerings included; formula in §8). Each point gives a permanent, stacking **+2% to all yields**.
- **The Almanac** is the permanent tree bought with Verdure: bigger offline caps, starting tool tiers, carrier efficiency, Pristine chance, dig speed, auto-craft, starting-zone skips. ~12 nodes at MVP, ~40 by 1.1. Two tiers mirror Egg Inc: cheap per-run Camp upgrades (Coin, wiped) vs. the Almanac (Verdure, permanent).
- **Bonded familiars** — the rare exception to the reset. Most familiars slip back into the grass at Migration; they were never yours. A handful — earned, never bought (a completed Museum set, a keystone moment, one Almanac node) — are *bonded* and cross with you. Each bond source grants exactly one permanent companion; bonded familiars are role-locked (a carrier bonds as a carrier), work any zone, and sit outside the gift count and its cost curve. A bonded raven hauling from minute one is much of why run 2 feels faster. MVP: 1–2 bondable; more by 1.1.
- Each new region gets a light modifier (lush: +herbs · misted: +fish, −flowers · ashen: +dig speed) so early runs feel different, not just faster. The modifier also feeds the Rite generator, flavouring what the region asks for.
- Rebuilding the kit in the first minutes of a run is deliberate — a small survival ritual that makes each new region feel inhabited rather than reskinned.

---

## 8 · Economy

### Currencies

| Currency | Role | Sources | Sinks |
|---|---|---|---|
| **Coin** | Per-run soft | Trade goods sold to the Provisioner | Tool costs, buildings, maps |
| **Verdure** | Meta (permanent) | Migration | Almanac nodes; +2%/pt passive |
| **Amber** | Hard / premium | IAP, dig sites, rewarded ads, weekly Play Games Reward | Time-skips, cosmetic gear & familiar skins, extra craft queues |

Familiar **gifts** and Rite **offerings** are paid in goods, not Coin — the economy's two goods-sinks, competing with the Provisioner for everything the camp produces.

### Formulas

```
giftCost(n)    = base · r^n            r: gatherer gifts 1.09 (n = familiars at that node — per-node depth pricing, so a virgin node always starts at base) · carrier gifts 1.10 (n = carriers hauling; paid as the Feeder — that many of each worked resource)
buildingCost(L)= base · 1.25^L         each camp building is a leveled line (Coin) — see §9; the repeatable Coin sink
flockCap(zone) = 8 + 2·roostLevel      gatherer familiars per zone; gifts fill slots up to the cap
carrierSlots   = 2 + roostLevel        camp-wide; +1 from the Spare Wing reward (§10); Almanac adds to both caps
toolCost(t)    = 100 · 12^(t−1) Coin + ingot batch (mined + smelted)    each tier ×2 yield
yield/sec      = familiars · toolMult · gearMult · (1 + 0.05·mastery) · global
global         = (1 + 0.02·Verdure) · almanac · museumSets · fossils · boosts
xpToLevel(L)   = 100 · 1.10^L
verdureGain    = floor( √( lifetimeRenown / 5,000 ) )   — awarded on Migration
offeringCredit : goods offered to a verse credit Renown at full trade value (no Coin is minted)
verseDemand(m) = baseQty · d^m · modifierWeight   — d tuned so each Rite costs a similar share of that run's lifetime output
spotlight(m)   = rotate(crafts, m + regionSeed)   — the 1–2 cheapest slots in each verse
offlineEarn    = rate · min(t, cap)    cap: 4 h base → +gear → 8 h buildings → 12 h Almanac
pristineChance = (0.5% + fieldPress + almanac) · (1 + tendingBonus)   — flat bonuses add points, Tending multiplies
fragmentFind   = digFamiliars · digSpeed · strataRarity   — pity timer: guaranteed fragment / 4 h dug
```

The square-root Verdure curve means each Migration at ~4× the previous lifetime Renown roughly doubles Verdure — the classic "when to reset" decision stays interesting without a wiki, and because offerings credit Renown in full, engaging the Rite never taxes it. The fossil pity timer keeps the deep chase strictly fair: patience always pays, luck only accelerates.

### Run 1 Rite — paper prototype (placeholder numbers)

| Verse (site) | Spotlight | Five slots — complete any 3 |
|---|---|---|
| 1 · the fire circle, Sunfield | Foraging | Berries ×300 · wildflowers ×150 · fibres ×200 · Tend 25 times · 1 Fine specimen |
| 2 · the hollow oak, Bramble | Firecraft, Mining | Berry Preserves ×8 · nuts ×400 · copper ingots ×5 · herbs ×300 · 1 Fine specimen |
| 3 · the oldest root, Old-Growth | Bushcraft, Forgecraft | Planks ×20 · cordage ×12 · Forager's Skewers ×12 · bronze ingots ×4 · 1 fossil fragment |
| 4 · the river bend, Silverrun | Fishing | Fish ×500 · Smoked Trout ×20 · clay ×300 · iron ingots ×6 · 1 Pristine specimen |

Alignment against the pacing targets below: verse 1 is completable ~25–30 min in, entirely from raw finds plus the Tend slot (which teaches the active verb); verse 2's Firecraft spotlight lands just as the Camp Fire Ring (upgrade #7, ~20 min) comes online — the rotation must never fight the unlock order; verse 3's fossil-fragment slot is the steep alternative path, kept Day-1-viable by the pity timer; verse 4's spotlight slots are raw river finds so the cheapest completion is genuinely Fishing (Smoked Trout, gated behind upgrade #24, is the *expensive* alternative — spotlight-cheapest must hold on the tuned path, not just in intent), tuned so the Rite completes early on Day 2, just ahead of the ~10-Verdure first-migration target. All quantities move in balancing.

### Pacing targets

| Moment | Target time | Why |
|---|---|---|
| First familiar gifted | < 60 s | Immediate agency |
| First tool crafted (Flint Sickle) | ~4 min | First ×2 spike; survival crafting established |
| First verse revealed (fire circle) | ~5 min | The Rite — and the game's goal structure — visible in session one |
| Zone 2 + first waystone read | ~10 min | New content and the first hint of tone |
| First recipe cooked | ~20 min | Second system online; 4th first-hour achievement |
| Verse 1 complete | ~30 min | First sign from the land; the offering loop proven |
| Zone 3: Logging + first dig site | ~45 min | Parallel skills begin; fossil hook set |
| First fossil fragment | ~Day 1 | The long chase visibly starts |
| Almanac Desk (Verdure forecast) | Day 1 | Meta-game math revealed early |
| Rite complete + first Migration (~10 Verdure) | Day 1–2 | Hook set before the day-3 churn window |
| First fossil completed (Antler Crown) | ~Week 1 | First lore payoff + big multiplier |

---

## 9 · Progression

### First 30 named upgrades

Familiar gifts and carriers scale separately by the cost formulas above; this is the *named* track that structures the first sessions. Gifts grow the flock (how many familiars); the Carriers track below buys the warden-built equipment they work with (how much each carries) — Coin never buys a creature. Camp entries are the **debut levels of building lines** (see below) — named here, repeatable after. Tool entries also consume an ingot batch (mined and smelted). Costs will move in balancing.

| # | Upgrade | Track | Cost (Coin) | Effect |
|---|---|---|---|---|
| 1 | Flint Sickle | Tools | 100 | Foraging yield ×2 |
| 2 | Waxed Satchel | Carriers | 150 | Carry capacity ×1.5 |
| 3 | Drying Rack | Camp | 250 | Berry sale value +25% |
| 4 | Trail Map: Bramble Hedgerows | Trail | 400 | Unlock Zone 2 (nuts, herbs, copper scree) + Mining |
| 5 | Rawhide Gloves | Tools | 600 | Hedgerow foraging ×2 |
| 6 | Handcart | Carriers | 900 | Carry capacity ×2 |
| 7 | Camp Fire Ring | Camp | 1,300 | Unlock Firecraft + Forgecraft (copper smelts on an open fire) + Berry Preserve recipe |
| 8 | Copper Sickle | Tools | 2,000 | Foraging yield ×2 (needs copper ingots ×5) |
| 9 | Root Cellar | Camp | 3,000 | Offline cap 4 h → 6 h |
| 10 | Preserving Jars | Firecraft | 4,500 | Preserve value +50% |
| 11 | Trail Map: Old-Growth Wood | Trail | 6,500 | Unlock Zone 3 (timber, tin seams) + Logging + **first dig site** |
| 12 | Bronze Hatchet | Tools | 10,000 | Logging yield ×2 (needs bronze ingots ×5) |
| 13 | Stag Harness | Carriers | 15,000 | Carry capacity ×2 |
| 14 | Carving Bench | Camp | 22,000 | Unlock Bushcraft + Plank & Cordage recipes |
| 15 | Whetstone | Tools | 32,000 | All gathering yield +25% |
| 16 | Forager's Skewers | Firecraft | 48,000 | Mushroom Skewer recipe |
| 17 | Field Press | Compendium | 70,000 | Pristine chance +1pt (additive: 0.5% → 1.5%) |
| 18 | Bellows Forge | Camp | 100,000 | Forge line L2: hot enough for iron (ore in Zone 4); Forgecraft speed ×2 |
| 19 | Wagon | Carriers | 150,000 | Carry capacity ×2 |
| 20 | Smokehouse | Camp | 220,000 | Offline cap 6 h → 8 h |
| 21 | Trail Map: Silverrun River | Trail | 320,000 | Unlock Zone 4 (fish, iron gravel) + Fishing + riverbank dig site |
| 22 | Iron Toolset | Tools | 470,000 | Foraging, Logging & Mining ×2 (needs iron ingots ×8) |
| 23 | Brush Screens | Excavation | 700,000 | Dig speed ×2 |
| 24 | Smoking Racks + Willow Rod | Firecraft | 1.0 M | Smoked Trout recipe; Fishing yield ×2 |
| 25 | Reed Weaving | Bushcraft | 1.5 M | Reed Basket recipe (highest-value good) |
| 26 | Curator's Cabinet | Compendium | 2.2 M | Museum set bonuses ×1.5 |
| 27 | Steel Toolset | Tools | 3.2 M | All gathering ×2 (iron ingots ×10 + charcoal ×20) |
| 28 | Raven Panniers | Carriers | 4.7 M | Carry capacity ×2 |
| 29 | Almanac Desk | Camp | 7.0 M | Live Verdure forecast for the Migration the Rite is unlocking |
| 30 | Trail Map: Mistfen Marsh | Trail | 10 M | Unlock Zone 5 (v1.1 content gate) |

> **First-hour achievement plan** (Quest eligibility needs 4): *First Harvest* (gather anything), *First Friends* (befriend 3 familiars), *Off the Beaten Path* (reach Zone 2), *Fire & Fruit* (cook a recipe). *First Verse* (complete verse 1, ~30 min) also lands inside the hour — a fifth in hand if any of the four slips in playtest. All inside the pacing targets above.

### Camp buildings — the repeatable Coin sink

Every camp building is a **leveled line**: the named entries above are debut levels; each further level costs `base · 1.25^L` (§8) forever. Early levels do structural things (stations, material heat, caps); past their named milestones the effects taper to small percentages — a bottomless, honest Coin sink so late-run Coin always has somewhere to go. Buildings and station gating are authored in `buildings.json` when the Phase 3 crafting system lands.

| Line | Debut | Levels grant |
|---|---|---|
| **The Fire** | #7 Camp Fire Ring | Fire-station recipes; copper smelts from L1; then fire craft speed |
| **The Forge** | **Clay Furnace** (~8,000 Coin, arrives between #11 and #12) | L1: forge station — bronze heat (the path to #12's bronze) · L2: #18 Bellows Forge — iron heat · L3+: forge speed |
| **The Bench** | #14 Carving Bench | Bench-station recipes; then queue speed |
| **The Store** | #9 Root Cellar → #20 Smokehouse | Offline cap; then storage capacity |
| **Roosts & Burrows** | new line, ~2,000 Coin debut | +2 gatherer cap per zone and +1 carrier slot per level — the familiar-cap lever (§8) |

---

## 10 · Monetization

### The Egg Inc posture

Free, generous, player-initiated. The gathering loop is never interrupted by ads, and the story is never sold — every word and every fossil is reachable free.

**Rewarded video (the workhorse)**
- ×2 all yields for 4 min (stackable to 1 h)
- Double offline earnings on return (the single highest-value placement)
- Instant-finish a craft queue · small Amber drip

**IAP**
- **Warden's Sigil** — permanent ×2 all yields (the "Pro" purchase, ~US$7)
- Remove ads · Amber packs · starter bundle · cosmetic camp, gear & familiar skins

**Never sold: the Rite.** Amber accelerates production; it cannot fill a verse slot directly. The gate is not for sale — and the Provisioner would have a line about anyone who tried.

**Play Games Rewards (Level Up requirement, not IAP)**
- Single-use ×2 by Sep 30 2026: **Wayfarer's Cloak** (warden cosmetic) + **Spare Wing** (+1 carrier slot)
- Repeatable ×1 by Mar 1 2027: **Weekly Amber Cache** (20 Amber, max 1/week)

---

## 11 · Level Up compliance

| Requirement | Wildgrove answer | Phase |
|---|---|---|
| PGS v2 SDK, init at startup | Unity plugin, initialized in bootstrap scene | 0 |
| Achievements (10 min / 40+ rec / 4 in first hour) | 40+ from zones, crafts, Museum sets, fossils, verses, Rites, Migrations; first-hour four planned above | 5 |
| Game Stats (≥5 repetitive, ≥1 competitive, ≥1 progression) | Resources gathered (competitive) · deepest zone, Rites completed (progression) · verses completed · recipes crafted · specimens catalogued · fossils completed · Migrations · Coin earned | 5 |
| Cloud save + conflict policy | Saved Games API (<100 KB save); conflict = highest lifetime Renown wins, prompt on tie | 5 |
| Sidekick overlay enabled | App Bundle + Play Console toggle; test early | 5 |
| Rewards items (2 single-use / 1 repeatable) | Cloak, Spare Wing, Weekly Amber Cache | 6 |
| Vulkan primary (Unity 2021+) | Unity 6 LTS + URP, Vulkan first in API list from day one | 0 |
| 60 fps (avg ≥55 / P90 ≥50 / P99 ≥30) | 2D URP; frame budget checked each phase gate | all |
| Stability <1% crash / <2% ANR | Crashlytics from Phase 1; vitals gate before launch | all |
| Large screens, no letterboxing at 4:3 / 16:10 / 21:9 (+ portrait complements) | Adaptive UI is Phase 2, not post-launch polish: phone portrait column ↔ tablet/PC landscape dashboard | 2 |
| Play Games on PC | Idle UI suits PC; no touch-only features; opt in at beta | 6 |
| Full keyboard/mouse + controller | Input abstraction from Phase 1: Tending = tap/click/Space/pad-A; full menu navigation | 2 |
| Title availability parity | Play-only launch across mobile/tablet/PC — satisfied by default | 6 |

> **Leaderboard integrity:** idle games are trivially save-edited. Ship Play Integrity API checks and server-side sanity bounds (max plausible Renown/hour) before any competitive stat goes live, or the leaderboards are noise within a week.

Reference: [Google Play Level Up guidelines](https://developer.android.com/games/guidelines)

---

## 12 · MVP development plan

Solo, part-time estimates. Each phase ends at a **gate** — a concrete question answered before more is built. MVP = zones 1–4, nine crafts, the warden's kit, the ore→ingot→tool chain, the four-verse Rite (authored run 1 + generator), 6 fossils, Migration + 12-node Almanac, 1–2 bonded familiars, Compendium v1, ~1,200 words of narrative, full PGS layer, monetization.

### Phase 0 — Foundations (1–2 wks)

- Unity 6 LTS project, URP 2D, **Vulkan first** in the graphics API list; verify on a real device
- Git repo + GitHub Actions Android build (AAB) from day one
- Play Console app created; internal testing track live with a walking-skeleton build
- Data-driven content: resources/upgrades/recipes/gear/rites/dialogue lines authored as JSON in `design/data/`, validated and imported into a ScriptableObject database at editor-load/build time — balancing and writing must never require code changes
- Number backbone: BreakInfinity (BigDouble) for currencies; versioned local JSON save with migration hooks
- PGS v2 SDK integrated and initializing (sign-in only, nothing else yet)

**Gate:** a signed AAB installs from the internal track, signs into Play Games, and renders at 60 fps under Vulkan.

### Phase 1 — Core loop slice (3–4 wks)

- Sunfield Meadow only: two nodes, Tending burst, familiar gifts, carriers, Provisioner sales
- The Sunfield-reachable upgrades (1–3, 6, 9); the rest of 1–30 land with their zones and stations in Phase 3. Cost/yield formulas wired to data
- Offline progress (4 h cap) + welcome-back summary sheet
- Placeholder art; real numbers. Input goes through an abstraction layer (touch now, K&M/pad later)
- Crashlytics + basic analytics events (session length, upgrade purchases)

**Gate:** is 20 minutes fun? Hand it to 3–5 people; watch where they stall. If the loop isn't compulsive with placeholder art, stop and fix — content won't save it.

### Phase 2 — Adaptive UI & input (2–3 wks)

- Responsive layout: phone-portrait single column ↔ landscape dashboard (camp left, zone right); test 4:3, 16:10, 21:9 + portrait complements, cutouts, foldable resize
- Keyboard/mouse + controller: every interaction reachable without touch; focus states; gamepad manifest flag
- Frame-budget pass on a mid-tier reference device

**Gate:** fully playable with a pad and with K&M on a 16:10 tablet window, no letterboxing, no touch fallbacks. Cheapest now, brutal to retrofit.

### Phase 3 — Systems build-out (6–8 wks)

- Zones 2–4; Foraging/Logging/Fishing/Mining with XP, levels, per-resource mastery
- Firecraft + Forgecraft + Bushcraft with queue slots; ore→ingot→tool tiers, trade-good chains, and the five MVP kit items
- Camp building lines + station gating (`buildings.json`): the Fire/Forge/Bench/Store/Roosts lines from §9, shelter caps live
- **The Rite, authored run 1** (already in `rites.json`): verse sites, offering delivery UI, the always-visible verse tracker in the journal; extend the validator's basic slot checks to full ≥3-slots-reachable analysis
- Excavation: two dig sites, fragment drops with pity timer, the three MVP fossil sets
- Compendium v1: entries, quality rolls (per haul batch), Museum sets, Fossil Wing, familiar plates
- Familiar world-sprites, placeholder tier (static with a light bob is fine at MVP) — the loop's visible feedback: creatures at nodes, carriers on the trail, baskets overflowing. Budgeted here, not in the plate pass
- Waystones 1–4, verse lines 1–4 + first Provisioner lines (dialogue is data; writing pass is cheap and late-editable)
- Upgrades 4–30; first real balance pass in a spreadsheet against the §8 pacing targets, Rite demand table included

**Gate:** pacing table holds within ±30% in real playtests through hour one; a bar is always filling; verse 1 completes without prompting; at least one tester asks what the Long Winter is without being prompted.

### Phase 4 — Prestige (3–4 wks)

- Migration flow gated by the completed Rite: Verdure forecast, the vignette, and a deliberate confirm (players fear their first prestige — sell it hard)
- **Rite generator**: procedural demands from migration count × region modifier × unlocked content; verify runs 2–5 in a spreadsheet before wiring UI. Ships *behind* the authored Rite — if the generator slips, runs 2–3 reuse the authored verses with scaled quantities and the phase still gates
- Almanac tree, 12 nodes; region modifiers for run variety; first bonded familiar; cheap kit re-craft ritual tuned to ~2 minutes
- Second-run tuning: run 2 must reach the old wall in ~⅓ the time

**Gate:** testers migrate voluntarily and report run 2 feels faster, worth it, and *asks for something different*.

### Phase 5 — Play Games layer (2–3 wks)

- Cloud save via Saved Games API + conflict policy (highest lifetime Renown wins; prompt on ambiguity)
- 40+ achievements (first-hour four verified in playtest), 5+ Game Stats submitted
- Sidekick enabled in Play Console and tested; leaderboards: deepest zone, weekly resources gathered
- Play Integrity + save sanity bounds before leaderboards go live

**Gate:** uninstall/reinstall on a second device restores progress perfectly; achievements and stats visible in Sidekick.

### Phase 6 — Monetization, beta & launch (3–4 wks)

- AdMob rewarded placements (§10) + IAP (Sigil, remove-ads, Amber) + Play Games Rewards items
- Plate illustration pass (~60 naturalist plates incl. familiar species, per-zone batches) + camp/zone backdrops; final narrative edit (cut 20% of the words — the doc's budget is a ceiling)
- Store listing (screenshots incl. tablet/PC), closed beta on the open track; 2–3 weeks of vitals: crash <1%, ANR <2%, fps thresholds on reference devices
- Play Games on PC opt-in; Level Up compliance self-check; launch

**Gate:** vitals green for 14 consecutive days and D1 retention >30% in beta → ship.

**Total: roughly 5–7 months part-time.** The two classic solo-dev failure modes this plan defends against: building content before the loop is proven fun (Phase 1 gate), and treating form-factor/input as launch polish (Phase 2 exists because Level Up makes it compliance, not polish).

---

## 13 · Open questions

To decide before Phase 1 ends:

- **Active-play depth:** is Tending enough, or does MVP want one minigame (e.g., a timed fishing catch, a brush-away-the-soil dig reveal)? The dig reveal is tempting — it's the fossil moment. Lean: ship Tending only; prototype the dig reveal at 1.1.
- **Amber earn rate for F2P:** generous (Egg Inc) vs. tight. Proposal: generous — ~40/week free between dig finds and the weekly Reward cache.
- **Narrative volume:** 1,200 words is a deliberate ceiling. If playtesters want more story, the answer is more fossils, not more words per fossil.
- **Name:** "Wildgrove" is a working title — check Play Store collisions and trademark before the listing goes up.

To decide before Phase 3 (systems build-out):

- **Gift denomination — DECIDED (2026-07-16):** gatherer gifts cost the node's own resource (you leave a pile of what the flock likes), paid from camp stock. The cold-start hole — a bare node produces nothing, so it could never self-fund its first gift — is closed by the warden's own hands: **while a Tend burst is live, the warden hand-gathers a small trickle straight to camp** (`tending.handGatherPerSecond`), fitting the tend-don't-take fiction and giving every node a bootstrap that costs attention, not currency. The gift curve is priced **per node** (n = familiars at that node, not the camp total), so a virgin trail's first gift always costs the base — a few taps of hand-gather — however large the flock is elsewhere; depth is paid where the depth is. Carrier gifts — decided the same day: **the camp Feeder**. A carrier's gift is a bundle of *every resource the flock currently works* (N of each, N on the carrier curve), laid out in a feeder at the camp edge — gatherers are priced in depth (their node's own resource), carriers in breadth, because they serve every trail. The bundle broadens as more nodes come into work, which is exactly when more carriers are needed, and it can't be bought off one overflowing node. ("Offering" was considered and rejected for the name — that word belongs to the Rite.)

To decide before Phase 4 (the Rite generator):

- **Bonded familiar rarity:** how hard-earned is a bond? Lean: rare enough that each one is an event — 1 per 2–3 Migrations early, slower after.
- **Familiar abilities:** are familiars pure count (a rate multiplier with a face) at MVP, or do species have small abilities? Lean: pure count at MVP; species abilities are the v1.1 depth lever alongside Entomology.
- **Generator guardrails:** min/max slot spread, spotlight-vs-unlock-order constraint, quantity clamps, and final-verse reachability (the last verse's zone must be affordable within a sane share of the run's budget) — the generator needs a spreadsheet proof across runs 2–10 before it ships.
- **Waystone vs. verse site legibility:** the past (waystones) and the present (verse sites) must read as distinct objects on a zone screen. Check in the Phase 3 playtest.
