# Wildgrove — Design Document

*Design document v0.12 · August 2026 · Working title · Prior rationale lives in repo history*

> An idle gathering & collecting game about tending a wilderness that remembers. Walk deeper with a small kith of wild companions, craft to survive, plant back more than you take — and listen when the land speaks, which is rarely.

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
15. [The Wheel — sabbat observances (draft)](#15--the-wheel-draft)

---

## 1 · Vision & pillars

### Tend, don't take

You are the warden of a small camp on the edge of an old, patient wilderness — and you are the only human in it. What company you have, the land sends: **familiars**, a handful of wild creatures who choose a careful warden — never more than a few, each one a name, a face, and a working partner. Together you gather, and together you give back: **replanting** what you take, building the land richer than you found it. Around the fire you craft what the kith brings home into the gear and goods a camp needs to push deeper. Some finds are **Choice**: specimens worth keeping forever. Some the land only lets you watch — the rare **insects** that gather at quiet sites: observed and drawn in careful sketches, then let go — each plate a sentence in a story nobody finishes telling you. And each region asks something back: a **Rite**, performed verse by verse at quiet places, offerings set down for spirits you never see — the land deciding whether you may move on. When the Rite is done and the season turns, the camp **migrates**, most of the kith slips back into the grass, and the land — and the creatures — remember how you treated them.

The fantasy is *stewardship, not extraction*. You are a guest who works hard, with a few friends who noticed.

### Direction note — leaning into Obelisk (decided)

Wildgrove borrows Idle Obelisk's spine (the Trail) and gate (the Rite) — and, deliberately, its heart: a small kith of individually upgradeable workers whose builds and postings are the run's central decisions.

| Axis          | Lineage                              | Description                                                                                           |
| ------------- | ------------------------------------ | ------------------------------------------------------------------------------------------------------ |
| **The Trail** | Spine · Idle Obelisk                 | Push deeper through eight zones. Each opens new resources, a new craft, a waystone, and — from Zone 3 — observation sites. |
| **Familiars** | Kith · Idle Obelisk (the drones)     | A growing collection (one familiar per species, each with a single fixed trait), up to six walking at once — one slot to start, three sung by verses, two bought (decided). |
| **Crafts**    | Breadth · Melvor Idle                | Parallel gathering and survival-crafting skills with XP, mastery, tools, and gear — the economy's backbone (§9). |
| **The Rite**  | Gate · Idle Obelisk (minus fighting) | A staged ritual of offerings — five slots a verse, any three. The finished Rite unlocks Migration.       |
| **Migration** | Meta · Egg Inc                       | Prestige resets the region and banks Verdure — the land's memory — into the permanent Almanac.           |

**Guard rails.** Two things Obelisk does not have, and Wildgrove must keep: nine crafts that genuinely interlock (ore → ingot → tool; timber → planter → yield), and a land that is a character, not a mine. If a change makes the familiars feel like appliances or the zones like ore veins, it has gone too far. The target is *obelisk with a heartbeat*.

**The collecting identity:** the Compendium (living things, your kith among them), the Folio, and the Deep Pages are the axes that never reset. They carry the spread bonuses, the 40+ achievements, nearly all of the story — and the familiar roster you actually play (§4).

### Art direction — the naturalist's plates (decided)

- **Every collectible is a plate.** Compendium entries, insect cards, gear, and every roster familiar: fine ink linework, muted watercolour and sepia washes, numbered figures, hand-written margin annotations.
- **UI is the warden's field journal.** Paper grounds, journal tabs, stitched spines; texture as identity, never as noise.
- **Zones are the stage, plates are the star.** Layered painted-parallax backdrops, light ambient motion. The illustration budget concentrates where the collecting identity lives.
- **Solo-scale production:** one fixed plate template; ~60 plates at MVP. Scope pressure cuts *plate count*, never plate quality.

---

## 2 · Core loop

### Gather → craft → give back

> **DECIDED 2026-07-31 — hauling is retired as a throughput system.** Carrying
> raced two absolute rates (gather vs. haul) whose growth lanes could never be
> kept level — tools/mastery/richness/slots all multiplied gathering while the
> trail sat flat between rungs — and any tuning was wrong for either a 4- or a
> 6-slot kith. Goods now travel home automatically and losslessly, landing at
> camp as one batch per node every few seconds (the batch survives purely so
> §5 quality rolls stay per-batch and a Choice find lands as a discrete windfall).
> With it went: the trail post and its lane maths, per-node basket caps and
> overflow, the five hauling upgrade rungs, Sure Paths, the Store's basket
> line (now: the away credit runs longer per level), the Timber Frame planter,
> and the Long Song's carrying half. The pack raven's trait is now a windfall-
> bubble bonus; the fell pony (§11) carries what the warden picks (+20% to the
> warden's own hands); the Birch Frame Pack quickens the warden the same way.
> Mentions of the trail post / haul rate / baskets below this line are
> historical record, kept for the DECIDED trail.

```
[WILDS]                                           [CAMP]
Warden + familiars      ──►  deliveries      ──►  Fire, forge & bench  ──►  goods
gather at their posts        walk themselves      │
(stationing = the            home, losslessly     ├─► GEAR (the warden's kit)
 allocation decision)                             ├─► TRADE GOODS ──► the Exchange (barter)
     ▲                                            ├─► OFFERINGS ──► the current verse of the Rite
     │                                            └─► REPLANTING & PLANTERS ──► richer nodes
     └───────── tools, gear, provisions, planter materials ◄─────────┘
```

1. **Agents at posts.** The warden and each slotted familiar can be **stationed** at a post; a familiar without a post **rests at camp** — no output (a slot is the right to hold a post, §4). Up to six slots by endgame (one to start — §4 ladder), one usually on the trail, eight-plus nodes by mid-run: coverage is never enough, and *where the kith stands* is the moment-to-moment decision.
2. **The windfall catch (REWORKED 2026-07-24 — replaces tap-to-tend).** A node drifts a **windfall bubble** into the strip now and then; catching it (tap / click / key) is the warden's own act. The catch pockets a flat burst of goods straight to camp — `economy.bubbles` pays a fixed few seconds of a notional gatherer's hands, the same at every node, so opening new ground never shrinks the prize — and **counts as Tending**: the yield burst, the briefly raised Choice chance, and the Rite's tend deeds all ride on the catch. **Any ground the run can reach drifts one, worked or fallow (DECIDED 2026-08-06, superseding "a fallow node drifts nothing — the bubble is still earned").** The old rule sounded right and played badly: the only nodes staffed are the two or three already drawn on the strip, so every windfall in the game rose from the plates the player was already looking at, and a camp with nobody posted drifted none at all. A windfall is the *land* handing something over, not a wage for standing there — and the reward is metered by the spawn interval and `maxLive` regardless, so widening the pool changes where a windfall comes from without paying the run any faster. Windfalls from unseen ground blow in along the foot of the band rather than rising off a plate. Tending no longer stations the warden (superseding 2026-07-17): standing somewhere is an explicit assignment, one body per post. Attention is rewarded, never demanded.
3. Harvest travels home on its own — deliveries are automatic and lossless (DECIDED 2026-07-31, superseding the 2026-07-18 trail post). The pack raven still arrives at minute one, now fattening every windfall bubble instead of walking a lane.
4. The **fire and benches** turn raw finds into four kinds of output: **survival gear** (the kit — permanent-for-the-run buffs), **trade goods** (dense barter weight at the Exchange), **offerings** (consumed by the Rite, §8), and **planter materials** (given back to the nodes, §3).
5. The **Exchange** — the silent caravan — barters goods for goods. Deeper zones demand better tools *and* better gear → invest → advance. Repeat.

> **Design tension to preserve:** gather rate (stationing + richness) vs. craft rate (queues + materials) stays slightly out of balance — upgrading one always exposes the next bottleneck. And the craft split is a **four-way dilemma**: craft for the kit, for the caravan, for the spirits, or for the land itself? Tuning rule: no lane may starve.

### Stationing rules (DECIDED 2026-07-18)

The single most load-bearing rule set in the game — written down so every system builds on the same one:

- **Assigned or watching (DECIDED 2026-07-18; revised 2026-08-09 — the watch gathers nothing; revised again 2026-08-12 — the watch is a PLACE).** An assigned agent works its post deliberately; a familiar without a post rests at camp and works nothing (a slot is the right to hold a post, §4 — resting help for free would make the ladder worthless). **Every observation site is its own post** (`dig:{zone}`), and its holder — warden or familiar — watches THAT site (§6) and **gathers nothing**, anywhere. The old gather-share (an even split of one gatherer across every node) is retired: a roamer who also gathered was two jobs on one post, and players couldn't say what the post was for. An unassigned warden simply stands at camp (or wherever they last tended), gathering nothing until posted again. *Two consequences worth naming: unworked ground now earns literally nothing, so a freshly opened zone is silent until a body walks it or a windfall is caught there (watch the first gift pile in the pacing sitting, since a pile is priced in the node's own resource) — and watching the whole map is now a real decision about slots rather than a free consequence of one posting.*
  - **Why the single roaming post went.** One body held "the watch" and sketched at every site on the map at once, so *assign a watcher here* silently meant *everywhere*: plates filled in at places the player had sent nobody, opening a zone quickened a watch nobody was standing at, and there was no way to ask the page why. A post whose reach is one place can be read off the page. The cost is the honest one — covering four sites now costs four slots — and it is the same shape as every other post in the game. *Save v53 carries the retired post's holder to the first open site's watch rather than resting them (`SaveCodec.RestoreStation`), so nobody loads to find their watcher at camp.*
- **Unattended nodes.** A node with no assigned agent keeps its richness and planters, and earns nothing until someone walks it again.
- **Transit.** Reassignment is always allowed and never costs goods; the agent *walks* — seconds, scaled by trail distance, producing nothing en route and visible on the trail. The map is honest.
- **The trail post — RETIRED (DECIDED 2026-07-31).** Deliveries are automatic; no body is spent carrying. The fell pony of **The Drover's Halter** (§11) keeps her lane as a station — at the warden's side, holding no slot — but her job is the warden's hands (+20%), not a load.
- **Offline.** Per node: `earn = gather rate · min(t, cap)`. Nothing caps the trip home any more; the welcome-back sheet reports the absence's full harvest. Per-agent base rates are tuned **up** from flock-era assumptions so a night away with a small kith still feels generous (magnitudes in the Phase 3 spreadsheet). **The cap itself is a ladder — DECIDED 2026-08-03: it starts at 2 h and tops out at 12 h**, and the top is a real ceiling (`economy.offline.maxCapHours`), not merely the highest rung anyone authored. The sources: raise-to rungs (Root Cellar 3 h, Smokehouse 5 h, The Long Watch I/II/III at 4/6/9 h) take the floor, then an additive band adds to it (Oilskin Tarp, The Almanac Desk and the Old-Growth Bounty spread at +1 h each, plus the Store line's +0.25 h per level). The authored kit lands on 12 exactly; the Store's endless levels are the one source left to overflow into the clamp, which is what makes it the early accelerator and nothing later.
- **Reachability.** Anywhere the design says "reachable" — most importantly the Rite validator (§8) — it means *satisfiable under plausible stationing with the current kith size*, not merely unlocked.

---

## 3 · The Trail

### Eight zones, four at MVP

| # | Zone                  | Resources                             | Unlocks                                                | Keystone specimen   | Scope |
| --- | --------------------- | ------------------------------------- | ------------------------------------------------------ | ------------------- | ----- |
| 1 | **Sunfield Meadow**   | Berries, wildflowers, fibres          | Foraging (start)                                       | Sunburst Poppy      | MVP   |
| 2 | **Bramble Hedgerows** | Nuts, herbs, copper scree             | Firecraft, Mining                                      | Amber-Shelled Snail | MVP   |
| 3 | **Old-Growth Wood**   | Deadfall timber, mushrooms, tin seams | Logging, Bushcraft, **first observation site**                 | Ancient Acorn       | MVP   |
| 4 | **Silverrun River**   | Fish, reeds, clay, iron-rich gravel   | Fishing, riverbank observation site                            | Moonscale Trout     | MVP   |
| 5 | **Mistfen Marsh**     | Peat, rare herbs, glow-moss           | Apothecary; marsh observation site                     | Lantern Firefly     | v1.1 (**built 2026-07-28**) |
| 6 | **The Hollows**       | Deep ores, crystals, ashglass         | Delving; the rarest insects; **the deep amber** | Echo Geode          | v1.1 (**built 2026-07-29** — bone beds stopped being a crop for the reason fireflies did: the buried past is watched, never gathered (§6); the third find is ashglass, the glass the burning left) |
| 7 | **Highland Crags**    | Eggs, wool, lichen                    | Husbandry; crag observation site                       | Cloudfleece Ram     | v1.2 (**built 2026-07-31** — the first zone behind the deepsteel gate and the fold-4 trail; the kea and the pika answer its gift piles) |
| 8 | **Cloudreach Peaks**  | Sky-blossoms, glacier ice             | The final waystones (endgame)                          | Aurora Bloom        | v1.2 (**built 2026-08-01** — the last ground, behind the same deepsteel gate and the fold-5 trail. The one trail map that teaches **no skill**: its two finds are worked with foraging and mining as they have been since zone 2, and what the zone adds instead is the §7 reveal. Its craft is a fourth tincture, the Aurora Cordial, not a fourth system) |

Each zone is a screen: 2–3 nodes, the trail home, one keystone specimen, one **waystone** (the past speaking, §7), and one **verse site** (the present, §8). From Zone 3, a **observation site**.

**The Rite is the way on (BUILT 2026-08-05).** Singing a zone's verse opens the *next* zone's gathering — the ground answers the offering, and the trail is walked because the Rite was answered rather than because a bundle was bought. The zone's own two gates still stand: a trail behind a **tool tier** waits for the tier (and arrives the moment it is forged), and a trail behind a **fold** waits for the fold (§8) — a sung verse can never conjure ground the run has no business standing on. A **Trail Map** rung remains a second way in and stays the only source of a zone's dig site, specialist skill and recipes, so its provisions bundle still teaches the Exchange (§9). *Open question:* with the Rite opening the ground, the map rungs' provisions are no longer on the critical path — whether they stay as an optional shortcut, get repriced, or lose the zone grant entirely is a tuning call for the run-1-to-run-3 sitting.

### Replanting & planters — the fourth claim (decided)

Nodes are not fixed faucets; they can be **made richer**, and the making costs goods.

- **Replanting** (node's own resource → richness): each node has a richness level, raised by replanting its own resource back into it — `replantCost(L) = base · r^L`, per node, per run. Richness raises the node's **base yield**. The lever split that keeps the UI legible: *replant the node, level the familiar* (§4) — one improves the place, the other the worker.
- **Planters** (cross-resource → infrastructure): built structures costing *other zones'* goods. Clay beds speed **regrowth** after a Tend burst; cordage trellises open a **second yield lane** at flower nodes; reed screens steady a observation site's sketch progress. (The timber frame left with the baskets — DECIDED 2026-07-31.) The backward flow that keeps old zones alive forever: Zone 3 timber has a job in Zone 1.
- **Bootstrap:** the warden's trickle at their post self-funds a virgin node's first replant. Presence, not currency (decided 2026-07-17). **The trickle is the *floor*, not the whole of it (REVISED 2026-08-13): the warden's hands ride the node's multiplier stack exactly as a familiar's do** — tools, mastery, richness, planters, tide, Verdure. They used to sit outside it, which meant the warden gathered a flat base rate for the entire run: ahead of a single familiar in the first minutes and roughly seventy times behind one by the midgame, on ground the player had spent the whole run improving. A post is a post; the ground does not care whose hands are on it. The warden keeps their own base rate and their own `wardenYieldBonus` band (the pony, the Birch Frame Pack, the Crag Flock) as the things that are theirs alone. **Base and band were rescaled together in the same change** — base `0.5 → 0.4`, pony and pack `+50% → +20%` each, the Crag Flock's `+10%` left alone. Scaling hands that had never scaled turned the band from a flat top-up into a multiplier on everything: at the old widths the warden swept from 1.67× a familiar bare to 4.33× fully kitted, so the intended 0.5 : 0.3 ratio was only ever true at one end of a run. At the new widths it runs 1.33× bare to 2.0× kitted, centred near the ratio rather than passing through it. *The two numbers are one lever now — retune either alone and the sweep re-opens.* `bubbles.rewardRatePerSecond` was pinned equal to the warden's base by a validator rule, which would have dragged the windfall haul from 30 units to 24 as a side effect of a change that was never about windfalls; **the rate held at 0.5 and the pin was deleted instead** (as that rule's own comment allowed for). The windfall's notional gatherer is a notional gatherer now, not the warden specifically, and is tuned on its own.
- **No Renown.** Replanting pays you back in yield; the land's memory (§9) is reserved for what you gave *up*. Dev-facing: replanting is the most on-theme verb in the game — the wardens are the land's experiment in trying again, and this is the trying. It earns tone, not numbers.
- Richness and planters **reset at Migration**. One Almanac node — *The First Planting* — lets a single planter survive the fold: the land keeping something you built, for once.

---

## 4 · Familiars

### The small flock (decided)

Familiars are not a count; they are a **kith**. Each is an individual: a name, a Compendium plate, a level, a build, and — over many seasons — a memory of you.

**Active slots** — the ladder (decided 2026-07-23: **six slots — one to start, three sung, two bought**):

There is no carrier type — and since 2026-07-31 no carrying at all: deliveries are automatic. A slot is *the right to hold a post*: companions past the slots stay in the kith but **rest at camp** — no post, no output, no run XP. Slots never cap the collection, only who's out working.

| Slot        | When                        | How                                                                    |
| ----------- | --------------------------- | ----------------------------------------------------------------------- |
| 1           | Free from minute one        | The land's first gesture: a vole and a raven arrive unasked; one takes the single post, the other rests — the first stationing swap is the tutorial. |
| 2–4         | Early / mid / late          | **The first time each named verse is sung** (`economy.kith.slotVerseZones` — the hedgerows, the marsh, the peaks; a verse sung is never unsung, and the record crosses Migrations). **DECIDED 2026-08-11 (Mo), replacing a lifetime TALLY** of 2 / 5 / 10 verses sung: a tally paid for folding early and often, which is the opposite of what a place at the warden's side should mark, and it is un-signpostable — “sing three more verses” tells a player to grind, where “sing the marsh's verse” tells them where to walk. The three are picked off the zones' own `minMigration` gates so they land early / mid / late: bramble-hedgerows is the second trail (mid-run-1), mistfen-marsh opens at fold 2 (run 3), cloudreach-peaks at fold 5 (run 6). Sunfield's verse is left out deliberately — it is the first thing anyone sings, before a place means anything — and so is old-growth-wood's, since run 1's last verse is the fold gate and a place there would pay for finishing rather than for going deeper. The last rung sits on the last trail (Mo's call, 2026-08-11): the peaks therefore carry both it and the run's narrative capstone, and the crags are where it moves back to if that reads as one beat too many at the same fire. |
| 5           | Store                       | The **starter bundle** (the initial purchase offer, Play Level Up): a slot and a one-time pile of Amber. |
| 6           | Store                       | The plain **kith slot** product — the ladder's last rung. |
| Trail posts | RETIRED (2026-07-31) | Deliveries are automatic. The pony of **The Drover's Halter** (§11) keeps her slot-free station at the warden's side; her job is the warden's own hands (+20%), not a lane. |

**Gift piles** (reworked 2026-07-23): every verse sung earns the warden **one pile, one yes** — counted for life, across Migrations. Leave a pile of a node's own resource and *that resource's specialist* answers, taking the node as its post: **where the pile is left is who comes.** A pile is refused where the specialist already walks (one familiar per species, ever) or when no slot is open for the arrival. The first verse is answered by the warden's own hands. Nothing repeatedly buys a creature — the pile is earned, one per verse, and Amber can never add one. Bonds honour the companion of their species — or bring it, resting, if it has never come.

**The calling gift — DECIDED (2026-08-06):** answering a pile also asks a little Amber (`economy.amber.callingGiftAmber`, first guess 10 — §9's early-game sink). The pile stays the gate; the gift prices the *yes*, never the right to ask. Guard rails: **unasked arrivals never ask** — the first vole and raven, a bonded companion crossing the fold, the Drover's pony — and a short warden's pile **waits, unconsumed**, until the gift can be met; it is never refused for want of Amber. Priced inside a week's free earn so it is a save-up, never a paywall.

**Mini-wardens.** A familiar is stationed exactly as the warden is: one agent system (§2), and the trail is simply another post. Assign it to a node and it works there — a steady gather trickle, and a slow tending cadence if its build allows; assign it to the trail and it carries. Stationing scarce agents across abundant nodes is the Obelisk allocation decision.

**Traits** — one per species, fixed for life (reworked 2026-07-23; replaces the level-5 powerup picks):

- Each species carries a **single fixed trait** — what makes it the specialist of one post. A familiar *comes with* its ability; there is no per-familiar build, no respec because there is nothing to respec. Species is identity: unique plate art, one trait, and **at most one familiar of each species, ever**.
- A familiar still earns **XP at its post**, from its own work. **Levels never scale output** — they pace XP and Kinship only; yield comes from tools (the post's levers), the species trait (the holder's), and richness.
- Trait kinds the sim reads: `nodeYieldBonus` (with a **pair** of related resources — the specialist works either node), `trailThroughputBonus`, `digSpeedBonus` (the watch), `choiceBonus`.
- **A node specialist works a related pair of nodes** (reworked 2026-07-24), so one familiar answers either node's gift pile and boosts both wherever it's posted (a watcher gathers nothing, 2026-08-09). The starting kith is sized to the gatherable nodes — seven pair-specialists cover zones 1–4 (plus rare-herbs) — and each species' animal is chosen to match the powerup it gives. Names each start with a distinct letter.

| Species        | Trait                                           | Flavour                              |
| -------------- | ----------------------------------------------- | ------------------------------------ |
| meadow vole    | Meadow-forager · +40% at berry & wildflower nodes | knows which bramble the birds missed |
| red squirrel   | Mast-hoarder · +40% at nut & mushroom nodes     | buries more than it ever digs up     |
| sedge linnet   | Nest-weaver · +40% at fibre & reed nodes        | pulls the year's best grasses first  |
| bramble hare   | Hedge-browser · +40% at herb & rare-herb nodes  | tastes the hedge for what's ripe     |
| warren weasel  | Ore-tunneler · +40% at copper scree & tin seam  | follows the seam like a burrow       |
| furrow hedgehog | Earth-rooter · +40% at clay & iron gravel nodes | roots the wet ground for what it holds |
| tawny owl      | Grove-and-river hunter · +40% at timber & fish  | works the wood's edge and the shallows |
| pack raven     | Deep pockets · windfall bubbles pay +25%        | cheeks like saddlebags (she fetches the windfalls home) |

**Collection > slots.** The **roster** is every familiar ever befriended — the seeds, the gift piles, the bonds — each its own species with its own plate. Only slotted familiars work; the rest rest at camp. Fielding two of six is a real choice, informed by the open tide (§15) and the run's plan: the collection becomes something you *play* — the berry specialist walks when berries are the plan, and Beltane-tide is her fortnight.

### Familiar power across Migration — the two-track model (DECIDED 2026-07-18)

Familiar power lives on two tracks, mirroring the game's own grammar (fast-resetting run power under a slow permanent track — the same shape as mastery, and as Verdure itself):

- **Run track (resets for everyone):** levels and powerup picks are per-run. At Migration every familiar — bonded or not — returns to level 1 with a clean build. Run 2 asks a different question; the build stays a live decision forever.
- **Kinship (never resets):** each roster familiar carries a permanent **Kinship level** — *the creature's memory of careful hands*, as Verdure is the land's. At Migration, a familiar's run XP converts to Kinship XP (conversion only — run XP already credited Renown as it was earned; **no second Renown grant**). The √ conversion decelerates in parallel with Verdure — both permanent tracks flatten together — and Kinship gains appear **in the same fold forecast panel** (§8), so the creature's memory never quietly argues against leaving. Kinship gives small permanent perks: **higher starting level** and **+XP rate**. At Kinship milestones a familiar's **signature deepens** (**built 2026-07-28** as trait deepening — the species trait is already its identity, so milestones sharpen it rather than add a second system): at Kinship 2 / 4 / 7 (`familiarXp.signatureMilestones`) the trait's value scales by +25% per milestone (`signatureDeepening`), and the plate takes an authored **inscription line** in the warden's hand (§7's channel about individuals; `species.json inscriptions`). The fold forecast names a sharpening before it lands, so the creature's memory argues *for* the fold in its own voice.
- *(Vocabulary: the permanent track is **Kinship**, never "bond level" — "bond" belongs to Migration-crossing, below.)*

**Bonding** is the separate, rarer honour (earned, never bought): a **bonded** familiar crosses the fold and is present from minute one — and its Kinship is why it is also *good* from minute one. Most of the roster slips back into the grass at Migration and is re-met in later regions — a quiet reunion beat, never a re-grind: roster and Kinship persist, only presence lapses. MVP: 1–2 bondable (final counts and rarity: Mo to settle, §14).

**Deliberately not adopted:** Almanac-mediated familiar power ("familiars start at level 5" Verdure nodes). Familiar permanence is exclusively Kinship — the relationship is with the creature, not purchased from the tree. The Almanac's familiar-adjacent nodes are limited to *The Old Friend* (the bond alone — slots come from verses and the store now) and *The First Planting*.

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
| **Curation**   | Collection        | Choice specimens, insect plates | Folio fixings, spread bonuses         | MVP   |
| **Entomology** | Gathering         | Marsh nodes                      | Insects (pure collection value)       | v1.1  |
| **Apothecary** | Survival crafting | Herbs, peat, fungi, glow-moss, summit bloom and glacier ice | **Tinctures** (buff consumables — built 2026-07-28; a fourth, the Aurora Cordial, arrived with zone 8 on 2026-08-01 and is the only one gated behind a rung, the Rime Still) | v1.1 |
| **Delving**    | Gathering         | Hollows nodes                    | Deep ores, crystals, ashglass (built 2026-07-29; deep ingots feed the deepsteel tier) | v1.1 |
| **Husbandry**  | Gathering         | Crag nodes                       | Eggs, wool (built 2026-07-31; wool feeds the felted cloak, the crags' bushcraft trade good) | v1.2 |

### Skill structure (per craft)

- **Skill level** 1–99, XP from every action. With Coin gone (§9), *levels are the gate and materials are the cost*, everywhere.
- **Mastery** per resource: +5% yield/value per mastery level. The long-tail chase.
- **Tools are mined, smelted, and smithed — never bought, literally.** Tier 1 is knapped from surface flint (a meadow by-find; no forge). Every tier after needs a **skill gate + an ingot batch**: Copper → Bronze → Iron → Steel → Deepsteel (the "deep ores" tier — built 2026-07-29 with the Hollows; deep ingots at forge 3). Each tier ×2 yield. Mining/Forgecraft is the unlock backbone: zones gate ores, ores gate tools, tools gate zones.
- A stationed agent works its node's gathering craft; crafting runs in parallel via fire/bench queues — bars always filling. The Melvor texture lives chiefly in the **queues** (the gather side is a handful of trickles, not a wall of flocks); base queue counts are tuned so all four output lanes (§2) genuinely compete.

### Survival gear (the warden's kit)

Three kit slots — **Hands**, **Pack**, **Camp** — worn by the warden alone; persists for the run, rebuilt cheaply after Migration (the early-run ritual).

> **REWORKED — the kit is one piece per slot for now.** The Pitch Torch and the
> Clay-Lined Creel moved into the Almanac (their jobs were permanent-track
> jobs), taking the shipped kit to three pieces — one per slot — so the swap is
> inert until new gear ships (a v1.1 lever; todo §1.5). The 2026-07-28 rule
> stands for when it does: **a made piece is never destroyed** — crafting is
> paid once per run, a displaced piece keeps in the kit bag and goes back on
> for nothing; charging materials to change your mind turned a decision into a
> trap. The Warden page groups the kit **by slot**, names what is worn in each,
> and labels a bagged piece's button *Wear* rather than *Craft*.

| Gear (MVP)       | Slot  | Craft     | Materials              | Effect                          |
| ---------------- | ----- | --------- | ---------------------- | ------------------------------- |
| Cordage Wraps    | Hands | Bushcraft | Fibres ×40             | Tending burst +50%              |
| Birch Frame Pack | Pack  | Bushcraft | Timber ×25, cordage ×5 | The warden's own hands +20% (hauling retired — the pack quickens the warden, not a lane) |
| Oilskin Tarp     | Camp  | Bushcraft | Reeds ×30, fish oil ×5 | Offline cap +1 h                |

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

Finds roll a grade: **Poor** (96%), **Decent** (~3.5%, +50% barter weight), **Choice** (~0.5% base, upgradeable). **The grade words are the harvest's, not a fossil dealer's (DECIDED 2026-08-02):** what the kith brings home is berries, timber, fish and ore, and the ladder is deliberately left open above Choice — the higher grades are unwritten, not absent. Quality rolls happen per **delivery batch**, not per unit — at idle rates a per-unit roll would shower Choice finds and cheapen the windfall. A Choice find can be traded at the Exchange for a windfall — **fixed into the Folio** — or **offered**: the three-way windfall choice.

**The Folio** (replaces the Museum) is the journal's back pages, where Choice specimens are physically **fixed**: flowers pressed, feathers tipped in, scales gummed to the paper, a nut split and mounted. **Spreads** group 4–8 related entries; a completed spread grants a *permanent* bonus surviving Migration (+% yield, +Choice chance, +offline cap) — the Warden's Gallery is the capstone spread (its slot grant moved to the store ladder, §4). **Fixing is a real choice:** run-speed now versus permanence — the specimen is consumed by the page. (Note the line this draws: the living land *gives*, and what it gives may be kept, pressed, traded, or offered; the buried past is only ever borrowed with your eyes — see below.)

### Insects — the deep chase (observe · sketch · release — DECIDED 2026-07-21)

The one system that used to take. It doesn't anymore: **nothing is kept.** From Zone 3, each region has an **observation site** where its rare living things gather — beetles, dragonflies, the pollinators. Familiars set to **Observation** watch and wait; each patient watch adds a **field sketch** — one portion of a plate, drawn in the warden's hand — and then the creature goes on about its life. **The Deep Pages are a book of drawings, not a case of pinned specimens.** The collection is knowledge; the living keep their lives.

- **A site is watched by the body standing at it (DECIDED 2026-08-12).** Each site is its own post (§2), and only that post's holder sketches there: an unwatched site records nothing, trains nothing, and surfaces no deep amber, and its pity clock **freezes** where it stood rather than resetting (those hours were watched — moving one companion around the map must not be worse than leaving them still). The watch card on the Trail page is where each site's watcher is sent, and it now sits inside that zone's own section.
- An insect's plate completes when **all portions are sketched** (3–5 per plate; pity timer: a portion is guaranteed per 4 h watched). A completed plate grants its **large permanent multiplier** unchanged — the land rewarding *attention paid* — and its **lore line**, now literally the finished plate and its field note.
- **The release beat:** completing a plate's final portion plays as a small sign (the creature lifts, circles once, and is gone) — never words. The 1.1 active-play prototype (hold the frame still, draw the line) *is* the sketching moment, and it has an ending.
- **Amber is the exception, and stays takeable:** old resin with an ancient insect sealed inside — the one creature the land lets you keep, because it was gone long before you came. It surfaces at the watch alongside the sketching and outlasts it — a fully-recorded map keeps turning up resin — and it is the only window the ground still opens onto the deep past. What that watch is *worth* in amber no longer scales with how much ground is open or how good the screens are (§9).
- **Sketches can be offered.** A verse slot may ask for a field sketch: the page is torn out for the spirits and that portion must be **re-observed** (the pity timer keeps this fair). Giving up the *record* is the steepest offering in the game, priced accordingly in Renown.

| Insect plate (MVP)       | Portions | Where                  | Bonus                      | What it whispers                                          |
| ------------------------ | -------- | ---------------------- | -------------------------- | --------------------------------------------------------- |
| **The Stag's Herald**    | 3        | Old-Growth Wood        | +10% all yields            | A beetle armoured like something ten times its size. It remembers being feared. |
| **The Silver Skimmer**   | 4        | Silverrun River        | +15% fishing, +1% Choice | A damselfly older than the river's name. It has watched the water change and change. |
| **Those Who Sow**        | 5        | Both sites, rare hours | +20% all yields            | The pollinators. They have tended this land far longer than you, and asked for nothing. |
| **The Lantern Bearers**  | 4        | Mistfen Marsh          | +20% observation speed     | They light the drowned paths every night, for no one. Fireflies are **watched, never gathered** — the marsh's third *find* is glow-moss (corrected 2026-07-28; a firefly in a basket contradicted §6 outright). |
| **The Quiet Court** (v1.1) | 5      | Hollows                | +25% delving               | Pale singers in the galleries no light has reached. The rarest plate (built 2026-07-29). |
| **The Parchment Wings** (v1.2) | 4  | Highland Crags         | +20% husbandry             | The mountain white, wings worn thin as the pages it is drawn on. It flies only while the sun holds (built 2026-07-31). |
| **The Windborne** (v1.2) | 4        | Cloudreach Peaks       | +25% foraging              | Nothing hatches at the summit. The wind carries them up and sets them down, and what lives on the rock lives on that (built 2026-08-01). |
| **The Deep Amber** (v1.1) | 4 pieces | Hollows               | +25% all yields            | An insect no one living has seen, held in resin. The world it flew through has ended. **Built 2026-07-29:** not observe-sketch-release — the one find that is *kept* (§6's exception). Four authored pieces surface **strictly in order** at the Hollows' watch site (`ambers.json`; pity clock so the lore can't starve); each carries a field note, and the finished set is a plate that crosses every fold. The §7 deep-past implication lives in these four notes plus the final waystones. |

Target: ~6 plates at MVP, ~30 by 1.2 — each a multiplier *and* a chapter.

> **Note (2026-07-21 reframe):** the collectible was fossils (uncover·record·rebury); it is now living insects (observe·sketch·release), and the deep-time lore that fossils used to carry now rides on **amber** (an ancient insect in resin) and the final waystones. The lore lines above and the §7 backstory are a **draft to re-voice**.

---

## 7 · Narrative & tone

### The land speaks sparingly

No cutscenes, no quest log, no exposition. Story arrives through inscriptions, terse strangers, and places that imply more than they say. A player who ignores every word still has a complete idle game; a player who reads everything assembles something quietly devastating.

Six delivery channels:

- **Waystones** — one per zone, an inscription revealed on arrival. Two lines, never more. Weathered, second-person, addressed to wardens in general — the *past* speaking. **The final waystones (built 2026-08-01)** are the exception that proves the channel: the last zone holds *four* stones as well as its arrival one, surfacing strictly in authored order and **at most one per fold**, so the reveal below is paced across the seasons that follow the climb rather than handed over in one sitting. The arrival stone is what teaches that cadence — it says more are coming — which is the teaching pass doing the work a tutorial would. Nothing gates on them and nothing gates behind them: a warden who never climbs simply never learns what the walking was for. They are re-read in the Deep Pages, directly under the deep amber, because that is the other half of the same reveal.
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

Long before the game begins, a civilization — *Those Who Planted* — worked this land the way the player does, and did not stop. Their taking outran the land's giving, and the Long Winter answered: the ash line the deepest amber is sealed beneath. The wilderness that regrew is not wilderness; it is a survivor, and it is watching. The wardens are its long experiment in trying again — Verdure is the land's memory of careful hands, **Kinship is a creature's** (the experiment watching back, up close, and deciding it likes you), and Migration happens on the land's terms: that is what the Rite is. The late-game implication, assembled only from the deepest amber and the final waystones: the warden order descends from Those Who Planted. You are the apology. Replanting is the apology *practiced*. **Both halves are now written (2026-08-01)** — four amber notes at the Hollows, four stones at the peaks, answering each other in order: the rows still visible from the summit, the debt the sky settled, the few who came down after and planted nothing they would live to sit under, and the instruction that is the whole of it. Draft wording, to re-voice with the narrative pass; nothing in either channel ever states the descent outright.

### Writing rules

- Total word budget at MVP: **~1,200 words**, all channels included. Scarcity is the aesthetic.
- Max two lines on screen at once. Everything skippable, re-readable in the Compendium.
- Proper nouns are never explained. Questions are answered only by other questions, three zones later.
- Nothing hostile, nothing gory. The dread is geological — patient and mostly kind. Tone target: a nature documentary narrated by someone grieving politely.
- Each new system's *first* margin note doubles as its instruction, in voice ("left a pile by the bramble; something is watching it") — the teaching pass is a writing task, not a tutorial system.

---

## 8 · Prestige

### The Rite — the region's exit gate

- **Verses**, one per zone at its verse site (four at MVP) — the first visible and chippable from session one. **Sung strictly in order**: a verse reveals only when its zone is reached *and* the verse before it is complete. A site reached ahead of its turn shows a sealed cairn naming the verse that still bars it. Migration requires every verse complete.
- **A sung verse opens the next zone's gathering (BUILT 2026-08-05).** The Rite is not a tax levied beside the trail, it is the trail: answer the meadow and the hedgerows open, answer the hedgerows and the wood does. This closes the loop the verse order already implied — each verse is sung in the ground the one before it earned — and makes the run's spine a single chain the player can name, rather than a ladder purchase running in parallel with a ritual. The zone's fold gate and tool tier still hold (§3, §8): the verse says *when you have earned it*, those two say *when you can survive it*, and a trail waiting on either arrives the moment it is answered.
- **Choose 3 of 5.** Five offering slots — raw finds, crafted goods, occasionally a Decent or Choice specimen, occasionally a deed (Tend N times) — any three finish the verse. One or two **spotlight crafts** are the cheapest path, rotating run to run. Unchosen slots expire, no partial credit. **Each verse stands alone (2026-07-31):** a deed slot counts only the work done since *its own* verse revealed, so no verse arrives part-answered by the deeds the one before it was already paid for.
- **A verse asks for the country it just opened (2026-07-31).** The generator's picks lean to the goods that debuted latest, reaching back to older ones only when the new tier runs out. The candidate pool is unchanged — this only reorders it — but without the lean a Crags verse could ask for berry preserves the camp has stockpiled since the first hour, and a verse that is already answered when it appears is not a gate.
- **How much it asks grows through the Rite, not just across folds (2026-07-31).** Goods amounts carry a geometric zone ramp of ×3.5 per zone order (`rites.json`, pinned flat for zones 1–2 by the hour-one targets), against a within-run power curve of about ×2.5 per zone step — so each verse costs roughly half again as long as the one before it, and verse 7 is the run's event rather than its seventh errand. This pushes the full-map estimates out past the FTP ~9–12 d / paid ~4–6 d the ×2.5 pass was measured against; the run-3-to-run-6 sitting judges both.
- **The specimen slot ramps too, on a curve of its own (2026-08-03).** ⚠️ **SUPERSEDED 2026-08-12 — see the next bullet. The linear-accrual premise below is not what `Simulation.Deliver` does, and the counts survive only as a floor. Kept for the reasoning, not the numbers.** It used to ask for exactly one specimen at every verse that had one, which meant that as the goods beside it climbed ×3.5 a zone the slot quietly stopped being an ask at all — a fair quarter of the river bend's verse, and about a fifteen-hundredth of the peaks'. Any warden who had ever caught a windfall had one in the drawer, so a choose-3-of-5 at depth was really a choose-2. It cannot take the goods ramp either: Choice comes from a per-batch roll (§5), so specimens accrue **linearly** in posted familiars × time × Choice chance while the goods economy compounds — ×3.5 would ask 150 of them at the peaks and wall the verse for good. The ask therefore carries ×1.6 per zone order, anchored at 1 on the river bend: **1 · 2 · 3 · 5 · 8** across zones 4–8, roughly tracking what the Choice-chance stack and the earned kith slots are worth by each zone's point in a run, so a deep specimen slot costs ~20–30 min of node time. It stays the *cheap* slot at depth on purpose — the luck lane must never become the wall — and the Folio wants the same specimens, which is the tension the ramp exists to create. Renown grants scale with the counts (per-specimen worth unchanged) and stay small against the goods slots, because a slot this cheap in wall-clock paying goods-slot Renown would simply win every verse. Zones 1–2 ask for a Decent and stay pinned at 1 with the rest of the hour-one table. Model-derived, not playtested.
- **The specimen slot is priced off its verse, not tabled (2026-08-12) — and the ramp above is superseded.** The linear-accrual premise the whole table rests on is not what the game does. `Simulation.Deliver` gives the **whole delivery** its rolled tier, so a Choice roll credits the batch's *full amount* to the drawer, not one find: the pool is `total gathering × Choice chance`, which rides the goods curve exactly, a percent or two behind it. It does not accrue linearly, and the Folio's Choice-chance spreads — which survive Migration — widen it further every fold. Against that, no authored count can hold: a save sitting on the river bend's verse at fold 2 held **~12K choice finds against an ask of 1**, so a fair quarter of that verse was free, and a choose-3-of-5 was really a choose-2. A specimen slot's count is therefore **derived**: `specimenSlotFraction` (0.4) of the geometric mean of that verse's own goods asks, floored at the authored count, skipped on zone order 1–2 whose hour-one pacing is pinned deliberately. A fraction cannot decay — both sides carry the same zone ramp and the same `demandGrowth^m`, forever — and cannot wall, because it asks for a slice of what the same run's gathering already produced. That puts the river bend at ~1000 against that ~12K drawer: a real ask, still well under a goods slot, because the drawer pools *every* resource's Choice rolls while a goods slot draws on one stock. Grants follow the count, so per-specimen worth is the one thing the change does not move. The `1 · 2 · 3 · 5 · 8` table survives only as the floor. **This fraction is now the whole lever.** Derived from one observed save, not playtested.
- **The verse card names the specimen slot in the plural and shows the whole drawer against it (2026-08-12).** The row said "one choice find" — written when the ask really was pinned at 1, and left standing through the zone ramp, so a peaks verse read *"one choice find  0 / 8"* under a button labelled "Offer one" that handed over eight. It now reads `choice finds  12.4K / 1020`, the count column carrying the number like every goods row. `Rite.SlotInHand` clamps to the ask on a goods row and **not** on a specimen row: clamping pinned it at "1 / 1" from the first find onwards, which is the one reading that tells the warden nothing, on the only slot whose stock the card does not otherwise give.
- **Offerings are whole (BUILT 2026-07-31).** A slot takes its entire ask in one act or not at all: the button stays shut until the stores hold all of it, and the row reads **have / asked** so the gap is the thing you watch. Holding part of an ask is not progress, and no part-answer is banked for the verses behind it to inherit — what a verse asks, it asks in full. *Why:* dribbling stock into five slots made the size of an ask meaningless, since a slot filled a grain at a time costs only patience; the decision the Rite is meant to pose is which whole ask this run can actually reach. Offerings are consumed on delivery and **credit Renown at full trade value** (§9): you give up liquidity, never prestige progress. Deed, specimen, and field-sketch slots carry fixed Renown grants — the sketch's is the largest, as the steepest thing a warden can give (§6). Gifts and replanting earn no Renown — feeding the spirits is remembered; feeding the voles is lunch.
- **Authored once, generated after.** Run 1 hand-authored (`rites.json`) as the tutorial; from run 2 a generator builds each Rite from migration count × unlocked content (the region-modifier input retired with the drawn season — 2026-08-08, Migration below) — validator-guaranteed ≥3 reachable slots per verse, where *reachable* means satisfiable under plausible stationing with the current kith size (§2), not merely unlocked.
- **The counts are readable side by side (`valueSpread`, 2026-07-31).** Dividing a slot's target value straight through by a good's own worth is honest and unreadable: 260 of a rich salve beside 20000 of a cheap preserve, the same offering twice over in two numbers that say nothing to each other. The division is softened towards each verse's own middle worth, closing that gap to about a fifth of what it was, and the set is rescaled so the softening only ever *redistributes* value between a verse's slots — difficulty stays the zone ramp's job alone. The cost is real and deliberate: a dear slot is no longer worth the same as a cheap one (about ×4 at the shipped 0.7), so expect the cheapest slots to be the popular answer and watch whether choose-3-of-5 still poses a decision.
- **Not for sale.** Amber never fills a verse slot.

### Migration

When the Rite completes and the region slows, the camp folds. Levels, builds, richness, planters, buildings, gear, kit, and zone progress reset. You keep **the journal entire** — Compendium, Folio spreads, insect plates — plus the roster and every **Kinship** level, Amber, and newly banked **Verdure**.

- **Verdure** — from lifetime Renown (§9) — permanent, stacking **+2% all yields**.
- **The Almanac** — the permanent Verdure tree: offline caps, starting tool tiers, trail-post efficiency, Choice chance, observation speed, auto-craft, zone skips, The Old Friend's bond, *The First Planting*. ~12 nodes MVP, ~40 by 1.1. **No familiar-power nodes** (§4).
- **Bonded familiars** cross the fold, present and Kinship-strong from minute one — much of why run 2 feels faster.
- **Region modifiers — RETIRED (DECIDED 2026-08-08, superseding 2026-07-28).** The per-fold drawn season (lush / misted / ashen / windswept, `regions.json`) is replaced by **the Wheel (§15)**: the world's lean now comes from the real calendar's tide, not a per-run draw — one season system, not two, and the one that keeps a real calendar. What the draw used to do passes over or lapses: the fold forecast names **the next sabbat** instead of the region ahead; the vignette keeps its twelve words without the season's sign; the Rite generator prices from migration count × unlocked content alone (`modifierWeight` and `regionSeed` retire with the draw); run 1 no longer needs a home-ground guarantee — every run is home ground now; and fielding reads the tide instead (§15 — Beltane is the vole's fortnight). One honest loss: folding no longer changes the world's flavour, so "a better season ahead" stops being an argument for the fold — the forecast's case rests on the permanent tracks alone. **The per-run randomness is parked, not condemned** (todo Appendix A): if the Wheel alone leaves runs too samey across a fallow month, a drawn overlay can return — but as the *second* flavour, judged beside the one that keeps the calendar.
- Rebuilding the kit in the first minutes stays deliberate — the survival ritual that makes each region feel inhabited.

### The fold gate — content that arrives over runs (BUILT 2026-07-30)

The pacing knobs (`demandGrowth`, the breadth ramp) only change *how fast* the same six zones go by; they cannot stop a run from reaching the end of the map. The fold gate is the lever that can. A zone may carry a **`minMigration`** — folds that must be behind the warden before that trail exists at all — so the far country is something the warden earns runs to see rather than a wall to grind through on the first.

- **Authored on the zone, once.** `minMigration` gates the zone's trail map *and* holds the zone's verse out of the Rite until then; the map rung inherits the zone's fold rather than repeating it, and the validator refuses a rung that carries its own. `upgrades.json` and `species.json` accept the same field for holding one rung or one specialist back inside a zone the run has already opened.
- **An early run is a shorter Rite, not a slower one.** Verses whose trail does not exist stand aside, so run 1 sings three verses over three zones and the Rite lengthens as the warden grows. **This is load-bearing:** the Rite is the only Migration gate, and a verse for an unreachable zone would seal Migration permanently — the fold that would have opened that zone included. The deep verses rejoin in their authored place on the fold their zone opens.
- **First guesses:** zones 1–3 open the first run, then one new trail per fold — Silverrun on run 2, Mistfen on run 3, the Hollows on run 4. Numbers are model-derived, not playtested.
- **Known consequence:** fewer verses early means fewer gift piles. It also used to mean a slower kith ladder — the second slot slipped to run 2 — which is one of the two things that retired the lifetime tally on 2026-08-11 (§4): the ladder now hangs off named verses, and bramble-hedgerows was chosen for the first of them precisely to put that place back in run 1. The gift piles still read off the tally and still thin out early; watch those in a real run-1-to-run-5 sitting.

### The Almanac's exotic lines — the re-climb scaled down (BUILT 2026-07-30)

The fold gate paces what a run may *reach*; these pace what it must *repeat*. Every fold re-runs the ladder from bare hands, and no yield knob shortens that — only starting further up can. The exotic lines act at the fold itself:

- **Granted rungs** (`grantUpgrade`, Almanac-only): an owned node puts a named ladder rung on every run **free** — no materials, no skill gate; the grant is the head start. *The Remembered Edge I/II* start the run at flint then copper tools; *The Known Way I/II* start it with the Bramble then Old-Growth trail maps, one requires chain (edge→way→edge→way) so the tool always precedes the trail that demands it. The **fold gate and the tool requirement still hold** — a granted trail behind `minMigration` arrives on the fold that earns it — and the validator refuses a map grant whose requires chain doesn't carry the zone's covering tool (a bought node that does nothing is worse than a refused one). Recruit rungs can never be granted: familiar permanence is Kinship's alone (§4).
- ***The Fire Remembers*** (`keepCraftOrders`): the stations carry their standing orders across the fold — the assignment, never the batch. Each stalls quietly until the new run re-earns its recipe's skill and heat, then takes the order up again without being asked.

Costs (6/10/14/22 up the granted chain, 8 for the Fire Remembers; the one-off tree now totals 159) are first guesses tuned so the line opens around folds 2–4 — the same playtest sitting as the rest of the pacing pass should set them.

### When to migrate — DECIDED (2026-07-18): the fold forecast is the decision

Once the Rite completes, the pinned tracker becomes the **fold forecast** — **every permanent gain in one panel, nothing hidden**:

> *+7 Verdure · Bramble +2 Kinship · Fern +1 · the 8th Verdure is ~40 min away at current pace · Beltane, twelve days off*

The √ curves do the design work: each Verdure point costs more Renown than the last, each Kinship level more XP — **both permanent tracks flatten together**, so the forecast visibly decelerates while the fresh-run alternative (fast early points, compounding Almanac bonuses) grows relatively better the longer you linger (the previewed modifier left with the drawn season, 2026-08-08 — the Wheel turns whether or not the camp folds). Renown lands in **chunks** as well as trickle — verse completions, familiar level-ups, Folio fixings, sketched portions carry fixed grants — so "stay for one more level-up" gives the curve texture. **Guard rail: the forecast sets timing only.** The Rite remains the sole gate; no minimum-Verdure or minimum-Kinship requirement, ever.

---

## 9 · Economy

### Money becomes XP (decided) — there is no Coin

| The wallet's old jobs | Owner                                                                            |
| --------------------- | ---------------------------------------------------------------------------------- |
| Tools                 | **Skill gates + material costs** (level + ingots; no wallet involved)               |
| Camp buildings        | **Material bundles** — Bushcraft is the construction backbone                       |
| Trail Maps            | **Provisions** — a goods bundle; you pack for the walk, you don't buy it            |
| Selling               | **The Exchange** — the caravan barters goods for goods. Rates are **always derived from the single trade-value table** (never authored per pair — hand-set pairs breed arbitrage), less a spread; small trades **round in the player's favour** — the caravan is dry, not petty. Off-zone inputs (berries for nuts) are its real job; trade goods remain the densest barter weight. A pressed **consideration** in Amber re-deals the standing offer at once (§9 sinks, 2026-08-06). |

**The pacing spine is XP**, on two tracks: warden craft XP (nine skills, gating tools and recipes) and familiar XP (gating powerups; earned at the post — *where the kith stands is how the run is spent*).

**The readability rule:** Coin was the single climbing number and the universal price signal. In its place: (1) **Renown is the ledger's big number** — always visible, always climbing; the Verdure input and the score; (2) trade-value weights keep a de facto price signal at the Exchange; (3) the Phase 1 gate asks *can a new player say what anything is worth?* Fallback if not: a cosmetic skin over Renown, never a returned wallet.

### Currencies

| Currency    | Role             | Sources                                                 | Sinks                                        |
| ----------- | ---------------- | ------------------------------------------------------- | --------------------------------------------- |
| **Renown**  | Per-run score    | All XP earned (warden + familiars) + offering credits   | None — the measure, not a wallet              |
| **Verdure** | Meta (permanent) | Migration (√ of Renown)                                 | Almanac nodes; +2%/pt passive                 |
| **Amber**   | Hard / premium   | IAP, observation sites, rewarded ads, weekly Play Games Reward  | Time-skips (paid skips budgeted to `timeSkipDailyCapHours`/day — 24 pins a heavy spender to ≤×2 a free player's pace, since sim-time is the only thing money buys; **built 2026-07-30**) · namings (companion 30 / warden 50, **built 2026-08-05**) · the 2026-08-06 slate (**built end-to-end 2026-08-06** — click-through and wording pass owed, todo §1.5): camp name · keepsake page · second queue · drover's consideration · settle-the-ledger (through the skip budget) · the calling gift (§4). Cosmetics stay retired for want of a substrate |
| *(Goods)*   | Everything else  | Gathering, crafting                                     | Kit · Exchange · offerings · replanting/planters · buildings · provisions — six sinks competing |

**DECIDED 2026-08-02 — the Amber earn is flat, and does not grow with the map.** The watch roll is taken **once for the round**, at `watchers × digFindsPerHour × hours` — the round's watchers summed across every site, since 2026-08-12 made the watch per-site — outside the site walk and untouched by the dig-speed stack. It scales with BODIES, which each cost one of the kith's slots, and never with how much ground stands open, which was the leak. It used to sit *inside* the walk and take the stack with it, and the two compounded: six sites against a multiplicative watch stack (brush screens ×2 · deep sight ×1.5 · hollow relics ×1.25 · lantern bearers ×1.2 · an ashen season ×1.5 · reed screens ×1.5 ≈ ×10) surfaced ~7 an hour, so one login's catch-up paid ~70 against the ~40/week free lean — about 30× over, in a game whose only sink is the time skip. Opening ground and quickening the watch are each meant to buy *sketches*; between them they were quietly buying the premium currency. `digFindsPerHour` stays 0.06 and is now the whole rate, so the number reads the way it is written. The open question moves the other way: whether 0.06 is now too **mean**, with the ad drip and the weekly cache carrying the free player. The roll still comes before the sketch walk, so a fully-recorded map keeps surfacing amber (§6).

**The Deep Amber is the deliberate exception** — it keeps both the per-site walk and the dig-speed multiplier. It is lore pacing rather than currency: four authored payouts behind a pity clock, and the watch stack shortening that walk is the intent, not a leak.

### The Amber sinks — DECIDED (2026-08-06): the slate, and the three lanes

One repeatable sink against four faucets left a free player converging on a pile with nothing to want — and a pile is what kills Amber-pack IAP, since nobody buys a currency they can't spend. The rule that shapes the slate: **every Amber sink is one of three lanes** — **expressive** (names, memory; no power), **convenience** (removes friction, never walls), or **pace routed through the paid-skip budget** (so the ×2 pin holds without a new rule) — and a proposed sink that fits none of the three is a leak, not a lane. The slate (prices are first guesses against the **diligent lean, ~120/week** — the accounting decision below):

- **Name the camp** *(expressive · 40 · once, kept)* — the sibling of the warden's naming. It reads on the fold forecast, the welcome-back sheet, and the journal's page headers. **Amended 2026-08-13 — the name is permanent, not per-run.** It shipped folding with the camp, to be the slate's first *recurring* expressive sink; that recurrence was the wrong thing to buy with it. A camp is struck and re-pitched a region north, not replaced, so a fold that wiped the name contradicted the fiction it was sold on — and re-charging 40 Amber for the name already given reads as a lapse, the shape the second queue is *supposed* to have and an expressive purchase never should. It now crosses Migration on exactly the warden's terms; a later change is a rename the player chooses to buy. The slate loses its only recurring expressive lane: see the lean note below, and if the pile grows unchecked the answer is a price rise or a new *convenience* sink, not re-arming this one.
- ~~**The keepsake page** *(expressive · 20 · per run)*~~ **CUT 2026-08-11.** Built and then removed: a piece of resin set into the journal as a mounted page remembering the run. Its whole payoff was one shelf line on the Record page, and that line's only distinctive content was the camp's name — so an unnamed camp bought "an unnamed camp · run 1 · 3 verses sung" for 20 Amber, indistinguishable from a dead button. Worse, `Keepsakes.TryMount` snapshotted the name at mounting, so the 40-Amber naming and the 20-Amber page only ever combined in one order and nothing said so: set the page first and the permanent record was blank for good, one page per run, unfixable. The lane was right and the artefact was not worth its price. **Do not re-raise as a page-with-a-line.** If run memory is wanted, the honest shape is a record the game writes for free at every fold (the fold already knows the run, the name and the verses) rather than a sink that charges for remembering. The commemorative rule it established still stands and the Wheel's sabbat plate inherits it (§15): no multiplier, no Renown, ever.
- **The second queue** *(convenience · 20 · per run)* — one extra craft-queue slot at the stations, lapsing at the fold so it recurs. It removes craft *latency*, never material walls — a queue cannot mint inputs — and it stays at **+1**: this is the one convenience that adds throughput outside the skip budget, and breadth here would be pace by another name.
- **A consideration for the drover** *(convenience · 5 · repeatable)* — press a little Amber on the caravan and the standing deal **re-draws now**, excluding the deal it replaces so the coin always changes something. Mechanically: a per-window re-deal count persists in the save and mixes into the window's seed, so the reload-never-rerolls invariant holds as (window, considerations) → deal, and the count resets when the window turns. *Rating note (checked 2026-08-06):* bribing a fictional trader is not an IARC/PEGI/ESRB questionnaire item; the line to stay clear of is Play's loot-box disclosure rule, and a re-dealt *offer* grants no item — the purchase changes what the caravan asks, not what the player receives — with the draw uniform over discovered goods if disclosure is ever wanted anyway. "Bribe" is the design word, not the page's: the caravan is dry, not petty — the in-game wording is the narrative pass's, in register (*a consideration*, *sweeten the deal*).
- **Settle the ledger** *(pace · through the budget)* — when an absence outran the offline cap, the welcome-back sheet offers to credit the **uncovered hours** of gathering and production at the **full live rate**, priced pro-rata on the skip (15 per 4 h ≈ 3.75/h) and **drawn from the same leaky skip budget**, so the ×2 pin holds unchanged and no new throttle rule exists. The moment of loss on return is the game's best-converting placement — it is why doubled offline earnings is the best ad slot — and this ships on plumbing that already exists (`SkipBudgetHours`).
- **The calling gift** *(early sink · 10 per arrival)* — answering a gift pile asks a little Amber alongside the pile (§4 owns the rules and exemptions). Early it is real money-shaped saving (drip-paced); by mid-game it is loose change — which is the point: it is the slate's early-game sink, and the pile itself stays the gate Amber can never add to.

**The lean describes the diligent claimer — DECIDED (2026-08-06).** "~40/week free" counted dig finds and the weekly cache but **not the ad drip**; the reference free player is now the one who claims it. The anchor: about four drips an awake day (≈84/week) + dig finds (~20) + the weekly cache (20) ≈ **~120/week free**, with the lazy player's ~40 (and the no-Play-sign-in ~20) as the *floor*, not the anchor. What follows from anchoring there:

- **The drip cooldown stays 4 h.** The 40-to-120 spread is no longer an accounting error to narrow — it is the design: diligence pays, and nothing behind the spread is walled (the floor still covers the calling gift and a companion's naming, just slowly).
- **The slate's prices read against 120.** *(Re-counted 2026-08-13, when the camp's name was made permanent.)* Run 1 spends the old ~60 (camp 40 + queue 20); **every fold after it spends 20**, the queue alone, against a diligent per-run earn of roughly 70–200 depending on the fold's length. So the recurring slate now clears trivially and the pile grows from run 2 — the cost of the amendment, taken deliberately. The repeatable lanes left holding it are the drover's consideration (5, uncapped) and the calling gift (10 per arrival, loose change by mid-game). If playtest shows the pile growing unchecked, raise a price or add a *convenience* sink; do not make an expressive name lapse to manufacture recurrence.
- **The built prices (skip 15, namings 30/50) now read cheap against the anchor.** Left deliberately — generosity is the posture — but they are the first candidates to raise if the diligent player's pile still grows unchecked in playtest.
- The economy.json `$note`s still cite the ~40/week lean; refresh them when the slate's keys land.

**Considered and rejected (2026-08-06), so the boundary is written down:** a Rite slot re-roll or offering discount (sells the Rite's decision — "never sold" is spirit, not letter) · Amber→gifts (buys a creature with one hop of indirection; the calling gift deliberately prices the *answer*, never adds a pile) · choosing the next region's season (breaks the deterministic draw; sells run flavour — moot since the drawn season retired, 2026-08-08 §8, but the boundary stands: run flavour is never sold) · any Amber→Verdure/Renown bridge (sells score and permanence; the money-clean XP rule exists for this).

### Formulas

```
yield/sec         = Σ stationed agents · specMult · richnessMult(node) · planterMult
                    · toolMult · gearMult · (1 + 0.05·mastery) · tideMult(node) · global
tideMult(node)    = 1 outside a tide; the sabbat's authored lean inside (§15)
global            = (1 + 0.02·Verdure) · almanac · museumSets · insects · boosts
richnessMult(node)= 1 + 0.10 · richnessLevel
replantCost(n, L) = base · r^L                      node's own resource; per node, per run
planterCost(tier) = material bundle                 authored per planter type
famXP/sec         = xpPerSecond · comfort · kinship  postMatch is UNBUILT (todo §1.5) — XP is
                                                    flat at any post; comfort & Kinship are
                                                    the only rate levers today
famXPToLevel(L)   = 60 · 1.12^L                     levels pace XP & Kinship only — traits
                                                    are fixed per species (2026-07-23)
kinshipGain(fam)  = floor( √( runFamXP / K_f ) )    at Migration; Kinship XP only — run XP
                                                    already credited Renown (no double count)
kinshipPerks(K)   = starting level +K · XP rate +2%·K    trait deepening at milestones (built 2026-07-28)
buildingCost(L)   = bundle(base) scaled 1.25^L      paid in goods
mapCost(zone)     = provisions bundle               authored per zone
exchangeRate(a→b) = tradeValue(a) / tradeValue(b) · (1 − spread)     spread ~15%, tuned;
                                                    rounding favours the player on small trades
toolGate(t)       = skill level threshold + ingot batch              each tier ×2 yield
xpToLevel(L)      = 100 · 1.10^L                    warden craft XP
Renown            = lifetime XP (warden + familiar) + offering credits (at trade value)
verdureGain       = floor( √( Renown / K ) )        K tuned to the XP scale
offlineEarn       = Σ per node: gather rate · min(t, cap)   the trail's own rate and its unheld ×0.5 lane retired with hauling (2026-07-31); watchers gather nothing (2026-08-09)
choiceChance      = (0.5% + fieldPress + almanac) · (1 + tendingBonus)
sketchProgress    = watchers AT THIS SITE · siteSpeed · rarity  pity: portion sketched / 4 h watched, and the clock freezes while the site stands unwatched (2026-08-12)
verseDemand(m)    = baseQty · d^m                     d = 1.45; modifierWeight retired with the drawn season (2026-08-08)
verseSlots(m)     = chooseCount + ⌊m / 2⌋, capped     the breadth ramp
spotlight(m)      = rotate(crafts, m)                 regionSeed retired with the drawn season (2026-08-08)
almanacLevel(L)   = baseCost · 1.25^L                 the endless line
```

The √ Verdure curve keeps the when-to-reset decision legible (each ~4× Renown ≈ 2× Verdure); offerings crediting Renown in full means the Rite never taxes prestige; Kinship's matching √ means both permanent tracks decelerate in step (§8); the observation pity timer keeps the deep chase strictly fair.

**What may scale with the fold count, and what may not.** Because Verdure is a √ of lifetime Renown and pays +2%/pt, player power grows asymptotically **linearly** in the fold count — the fold-to-fold power ratio starts near 3× and settles at 1.10–1.15× by fold 7. Anything exponential therefore beats it eventually, so:

- **Gates** (the Rite) may grow at most linearly. `d` sits just above the settled power ratio; the real growth is `verseSlots(m)` — **breadth**, which costs stationing, kith slots and map coverage rather than wait-time, and which is hard-bounded by how many slots a verse has. The generator widens each verse in step so the choice margin never narrows, and a per-verse clamp guarantees at least one slot of choice even where a lean zone can't be widened.
- **Sinks** (the Almanac) may grow exponentially, precisely because they gate nothing. *The Long Song* is the one repeatable line: level `L` costs `baseCost · 1.25^L` forever and pays an additive +5% to gathering (its carrying half left with the hauling system, 2026-07-31). Without it the tree finishes around the third fold and Verdure stops buying anything, which is what makes a late fold feel empty.

### Run 1 Rite — paper prototype (placeholder quantities; structure is the shipped run-1 tutorial)

| Verse (site)                    | Spotlight             | Five slots — complete any 3                                                             |
| ------------------------------- | --------------------- | ---------------------------------------------------------------------------------------- |
| 1 · the fire circle, Sunfield   | Foraging              | Berries ×300 · wildflowers ×150 · fibres ×200 · Tend 25 times · 1 Decent specimen          |
| 2 · the hollow oak, Bramble     | Firecraft, Mining     | Berry Preserves ×8 · nuts ×400 · copper ingots ×5 · herbs ×300 · 1 Decent specimen         |
| 3 · the oldest root, Old-Growth | Bushcraft, Forgecraft | Planks ×20 · cordage ×12 · Skewers ×12 · bronze ingots ×4 · 1 field sketch (torn out — re-observe the portion) |
| 4 · the river bend, Silverrun   | Fishing               | Fish ×500 · Smoked Trout ×20 · clay ×300 · iron ingots ×6 · 1 Choice specimen          |

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
| Third familiar gifted                           | ~45–60 min  | Assignment outgrows the kith; the puzzle begins             |
| Zone 3: Logging + first observation site                | ~50 min     | Parallel skills; insect hook                                |
| First portion sketched                          | ~Day 1      | The long chase starts                                       |
| Fold forecast visible (Almanac Desk)            | Day 1       | Meta-math revealed early                                    |
| Rite complete + first Migration (~10 Verdure)   | Day 1–2     | Hook set before the day-3 churn window                      |
| First Kinship conversion seen                   | Day 1–2     | The two-track promise proven at the first fold              |
| First plate completed (Stag's Herald)           | ~Week 1     | First lore payoff + big multiplier — and the first release |

---

## 10 · Progression

### First 30 named upgrades (level gates + materials; all bundles placeholder)

Familiar slots arrive by verses sung — plus the two store rungs (§4); creatures themselves arrive only by event, and nothing ever buys one. The **Hauling** track buys warden-built equipment the trail post works with. Camp entries are debut levels of building lines (repeatable after, `bundle · 1.25^L`).

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
| 9  | Root Cellar                  | Camp       | —               | Stone ×80, timber ×20 (Exchange)     | Offline cap 2 h → 3 h                                |
| 10 | Preserving Jars              | Firecraft  | Firecraft 10    | Clay via Exchange, herbs ×80         | Preserve barter weight +50%                          |
| 11 | Trail Map: Old-Growth Wood   | Trail      | Copper tools    | Provisions bundle                    | Unlock Zone 3 + Logging + **first observation site**         |
| 12 | Bronze Hatchet               | Tools      | Logging 5       | Bronze ingots ×5                     | Logging yield ×2                                     |
| 13 | Stag Harness                 | Hauling    | Bushcraft 12    | Cordage ×10, timber ×40              | Carry capacity ×2                                    |
| 14 | Carving Bench                | Camp       | —               | Timber ×60, stone ×30                | Unlock Bushcraft + Plank, Cordage & **Planter** recipes |
| 15 | Whetstone                    | Tools      | Mining 10       | Stone ×50, iron gravel ×10           | All gathering yield +25%                             |
| 16 | Forager's Skewers            | Firecraft  | Firecraft 18    | Timber ×30, herbs ×120               | Mushroom Skewer recipe                               |
| 17 | Field Press                  | Compendium | Curation 5      | Planks ×15, cordage ×6               | Choice chance +1pt                                 |
| 18 | Bellows Forge                | Camp       | Forgecraft 15   | Planks ×30, clay ×80, copper ×10     | Forge L2: iron heat; Forgecraft speed ×2             |
| 19 | Wagon                        | Hauling    | Bushcraft 20    | Planks ×40, iron fittings ×4         | Carry capacity ×2                                    |
| 20 | Smokehouse                   | Camp       | —               | Planks ×50, clay ×120, stone ×60     | Offline cap 3 h → 5 h                                |
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
| **The Store**        | #9 Root Cellar → #20 Smokehouse| Offline cap (+0.25 h/level, saturating at the 12 h ceiling); then storage capacity |
| **Roosts & Burrows** | ~early debut                   | **Familiar comfort**: +XP rate per level; late levels +1 roster capacity |

---

## 11 · Monetization

### The Egg Inc posture

Free, generous, player-initiated. The gathering loop is never interrupted by ads; the story is never sold.

**Rewarded video:** ×2 yields and XP rates for 4 min (stackable to 1 h) · double offline earnings on return (the single highest-value placement) · instant-finish a craft queue · small Amber drip. *(Time-boxed boosts may touch XP; permanent multipliers never do — next line.)*

**IAP:** **Warden's Sigil** — a permanent **+20% to yields and craft speed** (~US$7); **permanent XP multipliers are never sold** — familiar growth, Kinship, and Renown pacing stay money-clean · remove ads · Amber packs · starter bundle · cosmetic camp, gear & familiar skins (skins never touch Kinship or builds).

**DECIDED 2026-08-01 — the Sigil shrank from ×2 (Mo's call).** Sim-time is the only thing money buys here, and §10's paid-skip budget already caps that at ×2 a free player's day. A permanent ×2 on yields *and* craft speed stacked on top of that ceiling forever — it halved the whole map's walk (~9–12 FTP days to ~2–3) and turned one purchase into a second pace. At +20% the Sigil takes a sixth or so off a run and changes no wall's character: the craft-XP gates (firecraft 28, forgecraft 40), the fold gate and the skip budget stay exactly where they were placed. It is a thank-you that tilts the grove, not a tier of the game. If it ever needs to feel bigger, the lever is **breadth** — another cosmetic line, more Amber in the bundle — not a fatter multiplier.

**Never sold: the Rite — and never sold: a creature or a Kinship level.** Amber accelerates production; it cannot fill a verse slot, level a familiar, or add a gift pile — the right to call a creature is earned per verse, only. *(Amended 2026-08-06: answering a pile now asks a small Amber **calling gift** — §4, §9 — priced inside a week's free earn so it is a save-up, never a paywall; the earned pile remains the gate money cannot touch.)* (The last two *slots* are sold — the right to field more, never the friends themselves.)

**Play Games Rewards (Level Up requirement, not IAP):** **The Drover's Halter** (a fell pony that carries for the warden — their own gathering +20%) + **The Wayfarer's Plate** (a plate drawn by another hand, arriving already recorded) single-use by Sep 30 2026 · Weekly Amber Cache (20, max 1/wk) repeatable by Mar 1 2027.

**BUILT 2026-07-29 — how a reward reaches the grove.** A reward is an ordinary one-time product with a Play Games Reward offer attached: Google awards it for a Quest (single-use) or a Social Challenge (repeatable) and delivers it through the **out-of-app purchase flow**, so the game receives it like any other purchase. The order the game owes the player is **grant → tell them → acknowledge**, which makes refusing to acknowledge the *safe* failure and acknowledging-before-granting the unsafe one. *(Corrected 2026-08-12: the three days are real but this used to describe them wrongly. Google's Rewards page says the **player** has up to three days to open the game and claim, after which the reward is no longer available; it does not say an unacknowledged order is refunded and re-offered. The conclusion survives the correction, because a reward we acknowledge without granting is lost either way, but the same page adds a step we do not fully take: it asks the game to check for unacknowledged rewards when it **starts or is foregrounded** — see todo §3.4.)* The confirmation is a compliance artifact and outranks the journal's usual reticence: the item is named plainly, Play Games is said out loud, there is no way to decline, and it stays up until the player acknowledges it. All three land through this. **The cosmetic cloak that used to stand here was retired unbuilt (2026-07-30):** it needed a cosmetic substrate the game has never had, and building one to justify a single reward is the tail wagging the dog. **The Wayfarer's Plate** replaced it and meets the Sep 30 2026 bar of two single-use items. The plate is the one page no observation site can offer — it arrives already recorded, drawn by a hand that walked here first, which is also the only shape that makes an out-of-app arrival diegetic: the game's whole narrative register is marks left by others. It is deliberately the **weakest plate in the Folio**, because its value is that no one walked for it. It needs no Migration handling of its own — recorded plates already cross the fold, so writing it into the Folio rather than deriving it from the entitlement is what makes it permanent for free. The wayfarer survives as the figure who drew it.

**The weekly cache is no longer the game's to give.** It was a free weekly tap; it is now granted only by a Play delivery, and the grant is deliberately unconditional — Play owns the once-a-week cadence, and refusing an early delivery would drop a reward the player can never be offered again. The seven days survive only as the page's countdown.

**DECIDED 2026-08-12 — the journal's week is the calendar's, and there is only one of it.** That countdown used to run seven days from the last cache that arrived, which had two faults. Before the first ever claim there was nothing to count from, so the surfaces had no number to show the player a new run most needs it; and a cache taken late in the week pushed the next one later still, walking the cadence around the calendar and away from whatever day Play sets its own out on. The week now turns over at **warden-local midnight on Monday** — the same midnight the Wheel's tides close at, so the game has one week and one midnight rather than two of each. One reading serves every surface: how long the player has left to look for this week's cache *is* how long until the next is due. Whether Play's own reset is calendar-based, and on which day, is unconfirmed (§11 Play Console) — Monday is a guess that a console visit should either settle or correct.

**DECIDED 2026-07-29 — the second haul lane is an animal, not a post.** *(Amended 2026-07-31: hauling retired; the pony now carries what the warden picks — +20% to the warden's own hands — keeping her slot-free, unmovable station.)* The Drover's Halter (renamed from "Spare Wing") grants a **fell pony** — a real semi-feral Pennine pack breed, in register with the rest of the kith, and its historical job was carrying ore in panniers. The pony:
- **is always at its lane** while owned. There is no posting choice, it cannot rest, and it cannot be moved; its station is derived from the entitlement and re-asserted on load, so a reinstall or a Migration resolves to the same state.
- **holds no slot.** The lane costs nothing from the §4 ladder, so the reward lands the moment it is redeemed rather than waiting for a slot the player may not have. Because the pony can stand nowhere else, the exemption cannot leak into gathering — the reason it is an animal and not an abstract free post.
- **carries half a load.** Her trait ("Half-broke", kind `trailCarryFactor` 0.5) *is* the half load rather than a bonus on top of a lane, and unlike every other trait it **never deepens** — she cannot be fully tamed, so Kinship buys no sharper signature from her. That is what keeps a free, always-manned lane from doubling throughput, and it is the same fact her info page states in words.
- **is never offered anywhere a familiar is assigned to a node.** She walks with the warden only.

---

## 12 · Level Up compliance

| Requirement                                                   | Wildgrove answer                                                                                                              | Phase |
| -------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------- | ----- |
| PGS v2 SDK, init at startup                                   | Unity plugin, initialized in bootstrap scene                                                                                    | 0     |
| Achievements (10 min / 40+ rec / 4 in first hour ~~due July 2026~~) | 40+ from zones, crafts, spreads, plates, verses, Rites, Migrations, Kinship milestones; first-hour four per §10. **Re-read 2026-08-12: the thresholds hold and the July 2026 due date does not appear on the guideline at all**, so treat it as unsourced rather than met. The first-hour four is specifically the **Quest eligibility** bar, which makes it load-bearing for the Rewards row two lines down: single-use rewards are awarded on Quest completion, so no Quest-eligible achievement set means no way to deliver them. **45 published** — `AchievementIds.g.cs` is generated by `pgs-achievements.py` from the console round-trip. Remaining drift: three incremental step counts and the Pristine→Choice console rename (todo §2) | 5     |
| Game Stats (≥5 repetitive, ≥1 competitive, ≥1 progression)    | **Wired 2026-07-29** (`GameStats`): resources gathered (SUM, competitive) · goods crafted (SUM) · windfalls caught · specimens fixed · verses sung · Migrations (COUNT) · **trails walked** as the progression level. Continuous stats post as deltas against a baseline; cadence is the save cadence. **Submitting since 2026-08-04** on GPGS 2.2.0 (`RecordEvent` + `RequestEventsUpload`). Still owed: the console CSV upload, and **whether the console will take it yet is unconfirmed** (re-read 2026-08-12: the guide still calls the API "available for early feedback", dates GA to "starting August 2026" with no day, and names no month at all for the upload UI; September 2026 is the date it does give, for player-visible stats and draft-config testing). Schema authored in `store/play-games/gamestats/` | 5     |
| Cloud save + conflict policy (~~due November 2026~~)          | Saved Games API (<100 KB); conflict = highest lifetime Renown wins, prompt on tie. **The November 2026 date is unsourced** (re-read 2026-08-12: the guideline states the requirement and the multiple-accounts/conflict policy, with no date and an exemption only for saves over 3 MB, which ours is nowhere near) | 5     |
| Sidekick overlay (~~due July 2026~~)                          | **Enabled in Play Console 2026-07-29.** The July 2026 date is likewise unsourced; the guideline dates nothing here, and the console-vs-SDK split below is what it does say. No code: switched on at upload for app bundles (Testing → Advanced settings → *automatically* on for new bundles, which is the one CI depends on). SDK route is APK-only; minSdk 26 already clears its 23. Still owed: eyeball it on a device (Android 13+, 4 GB+, installed from Play, Sidekick on in Play Store developer options) | 5     |
| Rewards items (2 single-use by Sep 30 2026 / 1 repeatable by Mar 1 2027) | Drover's Halter, Wayfarer's Plate, Weekly Amber Cache (the cloak was retired unbuilt 2026-07-30 — §11). **Delivery path built 2026-07-29** — out-of-app purchase flow, grant → tell → acknowledge — and all three products live in the console (2026-08-04). The bar is met in code and catalogue; offers attach when the association UI opens Sep 1 2026 | 6     |
| Vulkan primary (Unity 2021+)                                  | Unity 6 LTS + URP, Vulkan first from day one. **Read 2026-08-12: there is no ANGLE escape for us.** The guideline lets a Unity project opt into ANGLE instead of Vulkan, but that concession is written for the **Built-in** render pipeline (until Sep 30 2027); on Unity 2021+ generally, Vulkan must be the primary API, with OpenGL ES tolerated only under ~10% of frames. URP is what we ship, so Vulkan-first is the requirement rather than a preference | 0     |
| 60 fps (avg ≥55 / P90 ≥50 / P99 ≥30)                          | 2D URP; frame budget checked each phase gate                                                                                    | all   |
| Stability <1% crash / <2% ANR **on reference devices**, <2% / <3% on 4 GB+ Android devices | Crashlytics from Phase 1; vitals gate before launch. Both columns re-read 2026-08-12, along with the basis they are judged on: a **28-day trailing average**, counted only on models with 1,500+ sessions, so a small beta cannot prove this row either way | all   |
| Large screens, no letterboxing (4:3 / 16:10 / 21:9 + portrait)| Adaptive UI is Phase 2, not post-launch polish: portrait column ↔ landscape dashboard. **⚠️ Found off 2026-08-01: `resizeableActivity` was `false`** (Unity's default is `true`), so the OS ran the game in compatibility mode — letterboxed on large screens, a restart prompt on unfold, and **the wide spread could never appear**, since a non-resizable activity is never handed a wide window. Now on | 2     |
| Play Games on PC                                              | Idle UI suits PC; no touch-only features; opt in at beta. **Pass done 2026-08-01**: `android.hardware.type.pc` + Google's 17 "not on a PC" features declared not-required (a permission implies a required feature, and the ad/analytics libraries own the permissions); PGoPC is no longer mistaken for a phone — `DeviceForm` asks `hasSystemFeature`, so Escape no longer quits the app and the wording follows the hardware. ARM64-via-translation kept over a third ABI. Owed: a play-through in the PGoPC developer emulator | 6     |
| Full keyboard/mouse + controller                              | Input abstraction from Phase 1; **journal navigation built 2026-07-29**: arrows/d-pad/stick move an ochre focus mark, Submit presses it, B/Esc backs out, the shoulders (or Q/E) turn the page, pad-X/C catches a windfall whatever is focused. **Declared 2026-08-01**: the build writes `android.hardware.gamepad` and `android.hardware.touchscreen` into the manifest as not-required — Play reads the manifest, not the build, and an undeclared touchscreen is *assumed* required. A lit rename field now owns the keyboard, so nothing typed navigates. Remaining: play it through on real hardware with a pad | 2     |
| Title availability parity (**effective Sep 30 2026**)         | Play-only launch across mobile/tablet/PC, which is the easy case: the rule is about shipping Android form factors *alongside* comparable non-Android ones, and there are none. Two details read 2026-08-12 that a solo launch should still know: a title already live on a non-Android platform before **Sep 30 2026** must sit on the Android form factors for **six months** before Level Up eligibility, and the form-factor ladder is dated separately (mobile/foldable/tablet **Sep 2026**, Googlebook **Mar 1 2027**, XR/TV/Auto **Sep 2027**) | 6     |

> **Leaderboard integrity:** ship Play Integrity API checks and server-side sanity bounds (max plausible Renown/hour) before any competitive stat goes live.

Reference: [Google Play Level Up guidelines](https://developer.android.com/games/guidelines)

> **The whole table was re-read against the live guideline on 2026-08-12**, because two claims in the Game Stats paperwork turned out to be firmer than the source (todo §3.4). What that pass changed: three due dates (achievements July 2026, Sidekick July 2026, cloud save November 2026) are **not on the guideline** and are struck rather than trusted; the stability row gained its second threshold column and the 28-day/1,500-session basis; Vulkan gained the note that ANGLE is a Built-in-pipeline concession we cannot use; and title availability gained its Sep 30 2026 date and the six-month rule. Confirmed unchanged: rewards **Sep 30 2026** and **Mar 1 2027**, reward creation and testing **from Sep 1 2026**, achievements 10/40+/4, Game Stats ≥5 + ≥1 competitive + ≥1 progression, and the 60 fps trio. Requirements the guideline carries that this table still does not answer for: the **memory** row Android 17 introduces (criteria "coming in future months"), and the **reference-device** list a compliance pass is judged on, now a 2026 phone set (Tensor G5, MTK MT6993, QCOM SM8845/8850, Exynos S5E9965) that no device here is a member of.

> **Play Games Leagues (checked 2026-07-29).** Not a guideline and nothing to build. Leagues are Google's own periodic competitive events, run in the Play Games app — Google picks the games, announces the scoring, and ranks players on "in-game performance from the start of the league period until it ends". The only thing our side owes is something to rank on, which is the **competitive Game Stat** above (resources gathered). The cadence isn't fixed or contractually monthly: "leagues may be offered periodically", schedules and durations vary.
>
> **Level Up vs Level Up+ (checked 2026-07-29).** Everything in this table is base **Level Up**, including the Rewards items. **Level Up+** is a further tier — a reduced service fee for games meeting *all* the revamped guidelines — so these rows gate it rather than belong to it. The one thing reserved to Level Up+ is **Play Points product promotions** (points exchanged for an in-game item), which ride the same one-time-product delivery the Rewards items are built on; nothing extra would be needed to serve them.

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
- Offline progress (2 h cap at the start, 12 h fully walked out — stationing rules §2) + a welcome-back summary of the absence's harvest
- Sunfield-reachable upgrades wired to data; placeholder art, real numbers; input abstraction (touch now, K&M/pad later); Crashlytics + basic analytics

**Gate (two questions):** *is 20 minutes fun?* — hand it to 3–5 people, watch where they stall — and *can a new player say what anything is worth without Coin?* If either fails with placeholder art, stop and fix; content won't save it.

### Phase 2 — Adaptive UI & input (2–3 wks)

- The five-page journal: **Trail** (zones, stationing, replanting, verse sites; the map is the page's own navigation; each ground folds shut behind its name and only the newest stands open, so eight zones of plates stay one readable page) · **Camp** (queues, buildings, Exchange) · **Stores** (the drawer — see below) · **Warden** (kit, skills, roster & slots, stats; the Almanac appears here after the first Migration) · **Record** (Compendium, Folio, Deep Pages). The Rite has **no tab** — verses live at their sites, with the compact tracker pinned on every page. *(The map is never called "Almanac.")*
- **The Stores drawer** (DECIDED 2026-08-04): what the camp holds *now*, as a grid of square plates rather than a list of names and numerals — the game's collectibles are all plates (§6), and the one page about owning them was the one page that never showed one. **One tile per stack, and the border is the grade:** the loot scale players already know — white for plain, green for Decent, blue for Choice — at printed-pigment saturation (`#FFFFFF` / `#338C21` / `#1A6BB8`). Took three passes, DECIDED 2026-08-04: parchment-mixed hues read as one border in three lights, WoW verbatim glowed like a backlight on the paper; the keeper is WoW's hues at the journal's ink weight. The three held figures moved off the Compendium, which had been answering *what have I ever found* and *what can I spend* in the same line and neither legibly; the Compendium keeps the lifetime record. **The tinctures moved here too** — a bottle is stock, the one thing on the Warden page that could be counted, spent and run out of — and are drunk by tapping the tile.
- **The Record page is drawn in plates, and what only records folds** (DECIDED 2026-08-13): the same argument as the Stores drawer above, applied to the page the collection actually lives on. The Compendium and the Crafts are drawers of square plates; a Folio spread is drawn as the strip of specimens it asks for, in full ink where one is pressed and pencilled in where the page is still bare, so the card says *which* specimen it wants rather than only how many; an uncaught insect's sketch ladder is drawn as marks. Two things the page never said, it now says: **what a Folio spread grants** (it asked the warden to consume a Choice find forever — the windfall's third fork, §6 — and priced it at a name) and **what a finished insect plate grants** (the largest permanent multipliers in the game were the ones nothing named). And the cards that only *record* — Compendium, Deep Pages, Wheel — fold shut behind their own tally, which is the Trail's rule for its grounds applied to the longer page; the cards you *act on* lead and stay open, the Almanac first (`JournalCardFolds`). Compendium **entry text** is still unwritten and belongs to the narrative pass (todo §1.2).
- **The Trail's head is short, and that is a rule** (DECIDED 2026-08-13, Mo's call): everything above the first ground is status, and the plates are the only thing on the page to act on — so a pinned line at the top of the Trail has to be worth a plate, and almost nothing is. It had grown to the tide's sign, the tide's touch label, an unfolded keeping card and the trail-home bar: roughly 1,480 canvas units of preamble against a ~1,030-unit viewport on a reference phone, which put the first gathering plate a viewport and a half down the one page whose whole subject is gathering, for the ~68% of the year a tide holds (§15's `openDaysBefore` 30). Five cuts, and every one of them is a thing said better somewhere the page doesn't pay for: **the fallow weeks' countdown went** (the events rail's coming-sabbat cell is the same `NumberFormat.Countdown` on every tab — the line was the cell read aloud, and the invisibility it was added to fix had already been fixed by the thing that replaced it); **the touch label went to the tide's sheet**, which carried the same sentence under *While it holds* already; **the keeping card folds shut** behind a live head reading tier · answered · closes-in, plus a moss clause the moment the stores can meet a slot — the one card in the book that folds while carrying buttons, earned because both doors into it (the tracker's tide row, the sheet's own button) open the fold on the way through, so it is never a shut drawer at the end of a link; **the tracker dropped its grey `· Ostara-tide` tail**, which restated, two rows above a rail cell wearing that sabbat's plate, exactly the demotion the rail was built to end; and **the trail-home bar went entirely** — 100 units of pinned height animating deliveries that had become automatic, lossless and untappable, an animation of a thing that cannot go wrong drawn above the plates that can. The keystone mark halved, 120 → 60. The carrier is gone for good; whether the **fell pony** gets a walk of her own again is deferred (she still reads on the roster, so it is a question about motion, not about visibility) — and if it comes back it belongs in the world strip, where ambient motion already lives and costs the page nothing.
- Responsive: portrait column ↔ landscape spread (Trail permanent right page; Camp/Warden/Record turn left) — **built 2026-07-28** (`JournalLayout`; the breakpoint is an aspect question, not a pixel one, so a portrait tablet stays a column); cutouts and safe areas land with it. Still to verify on real 4:3, 16:10, 21:9 and foldable-resize hardware
- Keyboard/mouse + controller: every interaction reachable without touch; focus states; gamepad manifest — **built 2026-07-29** (`JournalNav`, GameHud's focus section): touch-first, so the mark appears only once the player asks to move and a tap puts it away; focus is trapped inside an open sheet, survives the journal's own rebuilds, and scrolls its page to stay in view. The two interactions that were touch-only both have page-reachable paths: posting is the Trail page's own buttons, and the windfall catch gained a focus-independent binding. Still open here: the gamepad manifest, and verifying on real 4:3 / 16:10 / 21:9 / foldable hardware
- Frame-budget pass on a mid-tier reference device

**Gate:** fully playable with a pad and with K&M on a 16:10 tablet window, no letterboxing, no touch fallbacks.

### Phase 3 — Systems build-out (7–9 wks)

- Zones 2–4; Foraging/Logging/Fishing/Mining with XP, levels, per-resource mastery
- Firecraft + Forgecraft + Bushcraft queues; ore→ingot→tool tiers; trade-good chains; the five kit items; **planter recipes + richness curves**
- Camp building lines + station gating (`buildings.json`), costed in material bundles; Roosts as comfort
- **The Exchange in full:** derived rate table, spread, player-favour rounding; provisions bundles
- **Familiar system in full:** XP at post, deterministic species pools (content-filtered), powerup choice UI, roster & fielding
- **The Rite, authored run 1:** verse sites, offering delivery, the pinned tracker; reachability made stationing-aware — a slot counts only if its good's raw-input footprint fits the kith's gather posts (per-slot; `RiteGenerator.StationingFootprint` ≤ `KithGatherPosts`), enforced in the generator's candidate picks and the runs-2–10 ≥3-reachable proof
- Observation as **observe · sketch · release**: two observation sites, portion pity timer, three insect plates, the release beat (sign-style, no words); Compendium v1 incl. roster plates; quality rolls per delivery batch; Folio spreads
- Familiar world-sprites (static + light bob is fine) — creatures at posts, the delivery walking the trail home
- Waystones 1–4, verse lines, caravan lines + the **teaching pass** (each system's first margin note is its instruction, §7)
- Upgrades 4–30 recosted; balance spreadsheet vs. §9 pacing targets — including Rite demands, **offline magnitude for a small kith**, and the **hour-six spend proof** (a run's sixth hour must always have a meaningful next purchase). Seed model shipped with this revision (`economy-model.xlsx`): the proof **passes on defaults**, with the building lines holding the 10–90 min purchase band all run; per-level replants go trivial after H2, so raise `r` toward ~1.5 or sell replants in batches before this becomes the late-run lever. Solved starting constants: K ≈ 425, K_f ≈ 650 (derived from the pacing targets, recomputed automatically as inputs move)

**Gate:** pacing table holds ±30% through hour one; a bar is always filling — checked on the **Trail and Camp pages separately**; verse 1 completes unprompted; at least one tester asks what the Long Winter is.

### Phase 4 — Prestige (3–4 wks)

- Migration flow gated by the completed Rite: the **fold forecast** (Verdure + per-familiar Kinship + next-point ETA + region preview, one panel), the vignette, a deliberate confirm — players fear their first prestige; sell it hard
- **Kinship**: conversion at the fold, perk application (starting level, XP rate), roster persistence and the reunion beat
- **Rite generator** (behind the authored Rite as fallback): demands from migration count × unlocked content (region-modifier input retired 2026-08-08 — §8); spreadsheet-verify runs 2–5 before wiring UI
- Almanac tree (12 nodes, incl. Gatherer 3 and The First Planting); ~~region modifiers~~ (retired 2026-08-08 — the Wheel is the world's lean, §15); first bonded familiar; kit re-craft tuned to ~2 minutes
- Second-run tuning: run 2 reaches the old wall in ~⅓ the time

**Gate:** testers migrate voluntarily; run 2 feels faster, worth it, and asks something different; the kith's return feels like a reunion, not a re-grind; and testers can articulate what staying another hour would have bought — the unified forecast doing its job.

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
- **Active-play depth:** ship the windfall catch only (the 2026-07-24 rework of Tending, §2); prototype the hold-still-to-sketch observation reveal at 1.1 — doubly tempting now, since the sketch *is* the insect moment and release gives it an ending.
- **Name:** "Wildgrove" is a working title — check Play Store collisions and trademark before the listing.

**Before Phase 3**
- **Species pool contents:** author the deterministic pools (structure is decided; the entries are a writing/balance task).
- **Planter caps:** per-run richness cap, and whether the self-funding loop (berries→berries) needs a clamp beyond the cost curve. Spreadsheet proof alongside the generator's.
- **Exchange spread value:** ~15% starting point — big enough to stop arbitrage hoarding, small enough to feel generous.
- **Offline magnitude:** per-agent base rates and caps for a ≤6-agent kith — a night away must still feel generous.
- **Drover's Halter verification:** RESOLVED 2026-07-31 by retiring hauling — there are no lanes to balance. Her bonus on the warden's hands is the number to tune instead — +50% at first, cut to +20% on 2026-08-13 when the warden's hands began riding the node's multiplier stack and the band's old width swept the warden from 1.67x a familiar to 4.33x across a run.
- **Amber earn rate:** lean generous — **~120/week free for the diligent claimer** (drip ≈84 + dig finds ~20 + cache 20; the anchor, DECIDED 2026-08-06 — §9 sinks), floor ~40 for the lazy one.

**Before Phase 4**
- **Kinship K_f:** tune so Kinship rewards seasons, not marathon single runs.
- **Roster pacing:** lean 5–6 members by Migration 3 — enough that fielding is a choice, few enough that each plate is an event.
- **Bonded companion numbers:** Mo to finalize. Working assumptions until then: 1–2 bondable at MVP; 1 earned per 2–3 Migrations early, slower after.
- **Signature traits shipped early (2026-07-23):** every species carries its single fixed trait from arrival — identity, not a build. ✅ The freed 1.1 lever — *trait deepening* at Kinship milestones — landed 2026-07-28 (§4), with the plate-inscription channel riding on it.
- **Generator guardrails:** slot spread, spotlight-vs-unlock order, quantity clamps, final-verse reachability under stationing, and powerup-pool coverage (simplified by deterministic pools) — spreadsheet proof across runs 2–10 before it ships.

**Carried**
- **Narrative volume:** 1,200 words is a ceiling. If playtesters want more story, the answer is more insects, not more words per insect.
- **Waystone vs. verse-site legibility:** the past and the present must read as distinct objects on a zone screen. Check in the Phase 3 playtest.

---

## 15 · The Wheel (draft)

*Added 2026-08-08; **built the same day (Mo's call, superseding the v1.1 queue position): the Wheel replaced the drawn region season (§8) as the world's one lean, and the keeping — the tide's verse, tiers and claims — landed with it (todo §1.8).** The naming calls are decided (real names; the day is the sabbat). What remains is the art and voice pass: the eight plates behind the Record's year ticks, and the narrative re-voicing of the drafted margin lines. Nothing in Level Up asks for any of this (§12's Leagues note covers Google's own events). "League" is the dev-facing word for the genre shelf this sits on; no player ever reads it.*

### The shape: observances, not brackets

Eight real-world sabbats a year — the **Wheel of the Year**, hemisphere-mirrored — each a time-boxed **observance**: a sabbat verse to answer while the window is open, personal tiers, a commemorative plate. Deliberately **not** cohort leagues (weekly 30-player brackets, promotion/relegation), for four reasons that are each sufficient: there is no backend and PGS has no cohort API, so brackets mean a server and a standing ops tax on a solo project; an indie-scale population deals ghost brackets, which read worse than nothing; an offline idle game's scores are forgeable, and head-to-head stakes maximise the incentive while personal thresholds delete it; and a most-gathered race pays extraction in a game whose first pillar is tend-don't-take — §1's ore-veins guard rail, failed by design. Competition stays where §12 already put it (the weekly-resources leaderboard; Google's own Play Games Leagues if they ever pick the game) — garnish beside the observance, never its spine.

### The calendar

The practice's own terms — now the game's (naming below): the eight are **sabbats** — four Gaelic **fire festivals** on fixed cross-quarter dates and four **quarter days** on the solstices and equinoxes — collectively the Wheel of the Year, itself a modern braid (standardised mid-20th century; *Ostara*, *Litha* and *Mabon* are 20th-century coinages). The wheel is **self-dual under the hemisphere flip**: every sabbat sits on its opposite's date — Samhain↔Beltane, Imbolc↔Lughnasadh, Yule↔Litha, Ostara↔Mabon — so at any moment the world holds exactly two sabbats, always the opposite pair. *Elsewhere, the wheel turns the other way* is a line the journal should get to say.

| Sabbat | Kind | North | South | The tide leans (generator bias — first guesses) |
| --- | --- | --- | --- | --- |
| **Samhain** | fire festival | Oct 31 – Nov 1 | Apr 30 – May 1 | remembrance — preserves, smoked trout, amber |
| **Yule** | quarter day · midwinter | ~Dec 21 | ~Jun 21 | the fire — charcoal, torches, cooked meals |
| **Imbolc** | fire festival | Feb 1 – 2 | Aug 1 – 2 | first stirrings — fibres, herbs, the year's early greens |
| **Ostara** | quarter day · spring equinox | ~Mar 20 | ~Sep 22 | sowing — replant deeds (the tend-deed idiom extended), wildflowers |
| **Beltane** | fire festival | Apr 30 – May 1 | Oct 31 – Nov 1 | blossom and flame — wildflowers, fire-goods, tend deeds |
| **Litha** | quarter day · midsummer | ~Jun 21 | ~Dec 21 | the long light — berries, the meadow's plenty |
| **Lughnasadh** | fire festival | Aug 1 – 2 | Feb 1 – 2 | first harvest — nuts, baskets, the gathering-in |
| **Mabon** | quarter day · autumn equinox | ~Sep 22 | ~Mar 20 | the balance — trade goods, paired offerings, the Exchange |

The leans are the spotlight idiom (§8), not requirements — reachability holds regardless, and the generator draws only from the run's unlocked content.

### Names — real, and the warden's — **DECIDED 2026-08-08 (Mo: keep the real names; the day is the sabbat)**

The device that keeps the world sealed: **the calendar is the warden's, not the land's.** The warden came from somewhere with a reckoning; the journal margin writes *"Beltane, by my count"*; no spirit is named, none appears, and the land answers the observance as it answers everything — with signs, never words (§7 holds whole). The land has no calendar; it has weather and memory. §7's proper-noun rule survives intact: the wheel arrives as the one set of proper nouns the *player's* world explains instead.

Vocabulary — settled 2026-08-08:

- The day is **the sabbat** (Mo's call). The practice's own word, carried the same way *Beltane* is: the warden's inheritance, a word from the place the warden came from, written in the margin in their hand. The land still never says it. The occult weight the word carries was weighed and kept — it is the one word in the game that admits the warden *had* a tradition.
- The cycle is **the Wheel**; the open span is **the tide** — *Beltane-tide* — the old word English already uses for a festival's span (Yuletide; *tīd*).
- **Never "season."** The word is taken twice over: the fold's drawn weather (§8 — *an ashen season*) and, colloquially, the run itself (*third season she has found me*). A third calendar sharing it would be §6's firefly basket — a vocabulary self-contradiction. And the sabbats are days, not seasons, in the practice too; "tide" is the span-word that stays honest in both worlds.
- **"League" stays dev-facing** — the genre shelf, never the page.

### The observance

- **Window:** the tide opens **a month** before the sabbat night and **closes at the fire** — sabbat midnight, warden-local. Eight tides a year, ~68% of it inside one, with a fallow gap of 7 days at the tightest (Mabon→Samhain) and 22 at the widest; at FTP pace comfortably one whole tide per fold. **Widened from a fortnight, DECIDED 2026-08-11 (Mo).** A fortnight meant the Wheel was shut two visits in three, and it is shut *invisibly* — the tracker row, the Trail's tide line and the keeping card all stand down together (the rail and the Trail's countdown below are the other half of that fix). 30 is near the ceiling the authored calendar allows: the tightest pair of nights is 38 days apart, and the validator rejects `openDaysBefore + 1 > gap`, so ~37 is where the windows collide. The knock-on to watch is the ambient touch, not the keeping: the world is now leaning most of the time, which makes "leaning" closer to the baseline and the fallow week the anomaly — if that flattens the touch into wallpaper, the lever is the touch's SIZE, never the window (a tide you cannot reach is not a tuning knob).
- **The sabbat verse is the scored verb.** The §8 machinery whole: choose-3-of-5 offering slots, generated from the run's unlocked content under the existing reachability rule (*satisfiable under plausible stationing with the current kith size*), themed by the tide's lean the way spotlights already lean. It stands at the fire circle — no new world object, no tab (the Rite's own rule).
- **It is not a Rite verse.** It earns **no gift pile**, counts toward **no verse milestone** (§4's 2/5/10 — the lifetime ledger is the Rite's alone), and opens **no ground.** This is the cross-system leak to guard in review: one flag, checked everywhere "verses sung" is read.
- Offerings **credit Renown at trade value**, exactly as the Rite's do (§8) — the wheel never taxes prestige either, and it is no new faucet: the goods were gathered by play; the credit is the standard one.
- **A fold mid-tide** keeps completed slots (the observance is calendar-keyed, not run-keyed); open slots **redraw** against the new run's content, deterministic from (fold count, festival, year) — reload never rerolls, a fold redraws honestly.
- **Tiers are personal thresholds** on slots answered — working names *kept the eve · kept the day · kept the wheel* — offline-verifiable, no cohort, no rank, no relegation. A forged clock or save cheats its owner and nobody else, which is the entire anti-cheat budget this needs while nothing competitive pays.

### What a tide pays

- **The sabbat plate** — the prize is a page: the tide's plate drawn into the journal, commemorative, crossing folds as every recorded page does (the Wayfarer's Plate idiom, §11). It **recurs annually**; each keeping adds a margin **year-tick** in the warden's hand, so a missed year is a gap in a record, never a wound. **No multiplier, ever** — the keepsake rule (§9): a sabbat page that paid yield would be selling attendance.
- **A little Amber by tier**, priced against the ~120/week diligent lean (§9), so a kept sabbat reads as a good week — not a second cache; the weekly cadence is Play's (§11).
- **Never:** Verdure, Renown grants, Kinship, creatures, gift piles, slots, Rite progress. §9's rejected-sinks list already walks this exact boundary; the wheel inherits the never side whole and adds nothing to it.
- **The ambient touch** rides with the tide — its own section below.

### The ambient touch — the tide felt at the nodes (**in — 2026-08-08; the world's only lean, replacing the drawn season**)

While a tide is open, the world leans a little toward its sabbat — one authored effect per sabbat, small enough to miss without loss, present enough that the calendar lives in the grove and not only in the margin. Mechanically it is one more term in the §9 stack: a `tideMult` that is 1.0 outside a tide and a single narrow lean inside, standing where the drawn region season used to stand (§8 — retired 2026-08-08) but keyed to the calendar rather than the fold. The gathering leans deliberately echo the specialists' pairs (§4), so a tide is also a fielding question — Beltane is the vole's fortnight.

| Sabbat | First-guess touch — one lane each |
| --- | --- |
| **Samhain** | the watch leans close — sketch progress +20% (never the resin; below) |
| **Yule** | the fire burns willing — fire-station craft speed +20% |
| **Imbolc** | first stirrings — fibre & herb nodes +20% |
| **Ostara** | sowing weather — replanting costs −20% |
| **Beltane** | blossom — wildflower & berry nodes +20% |
| **Litha** | the long light — windfall bubbles pay +20% |
| **Lughnasadh** | the gathering-in — nut & mushroom nodes +20% |
| **Mabon** | the balance — the Exchange's spread eases 5 pts (the caravan keeps the day too, in its way) |

The rules that keep it a touch — the world's only lean now, and still never a wall:

- **It never gates.** Nothing is reachable only inside a tide, no content keys on it, and every lean stays inside one mastery band (~+25%) — a missed sabbat is flavour missed, never power lost. This is the FOMO line, and holding it is what makes the touch safe to ship at all.
- **It never multiplies the Amber faucet.** The 2026-08-02 decision flattened the amber roll precisely because innocent multipliers had compounded on the premium currency; the tide does not reopen that door. Samhain's lean is therefore the watch's *sketching*, never its resin — safe by construction now the amber roll sits outside the watch stack (§9). Where it grazes the Deep Amber's authored walk, that walk keeps its stack by design: lore pacing, not a leak.
- **It never enters `verseDemand`.** The drawn region season used to feed the Rite generator's asks; the tide never does — with the draw retired (§8) the generator prices from migration count × unlocked content alone, and a two-week overlay baked into a verse's quantities would price the sabbat *against* the warden keeping it. The sabbat verse takes the tide's lean as theme; its quantities price off the ordinary tables.
- **Offline stays honest across the boundary.** `OfflineCatchUp` already sub-steps in whole seconds; the touch is read per-step from the stepped clock, so an absence spanning a tide's edge earns the tide's rate for exactly the seconds inside it. The sliced-equals-unsliced property (`OfflineCatchUpTests`) is the test that pins it.
- **Deterministic by construction.** (ratcheted now, hemisphere) → open tide → authored touch. No draw, nothing persisted, nothing to reroll — reload-never-rerolls holds without new machinery.
- **Seen as a sign, taught in the margin (§7's pattern).** The touched node's plate takes a small mark for the span, and the sabbat's margin line is the instruction — *"Beltane, by my count. The meadow keeps it too; the flowers come readier."* Two lines, and they are that sabbat's share of the word budget — the same lines the Costs section counts, not additions.

Cost, once the calendar service exists for the observance itself: one multiplier hook in the yield path (plus the two odd ducks — a cost factor for Ostara, a spread ease for Mabon — each one line where those numbers are already computed), eight authored entries in `sabbats.json`, the boundary tests, and the margin lines. The risk is tuning, not plumbing.

### Hemisphere, clock, and data

- **`design/data/sabbats.json`** — windows authored as dates per hemisphere, **3+ years ahead**. No astronomy code: solstice drift is data, and an un-updated install keeps the wheel turning. That is the **evergreen rule**: live-ops may lapse; the game must not care.
- **Hemisphere defaults from locale/timezone; a journal setting flips it.** The equator simply chooses; a traveller keeps their own reckoning — the calendar is the warden's. The setting **locks while a tide is open** (the opposite sabbat sits on the same dates, so a mid-tide flip is a double-claim vector); claims key on (sabbat, year, hemisphere) regardless, so clock or timezone games move hours, never rewards.
- **Every window read goes through `GameLoop.NowUnixMs()`** — the ratchet stays the only clock (todo Appendix B's standing rule). A wound-forward clock celebrates alone, permanently ahead of the real wheel: spent, not minted. With no server, that plus personal-only stakes *is* the integrity story — one more reason the tiers pay pages, not power.
- The Wheel's state lives in the sim (pure C#, windows fed in as data, tested without the editor); the save grows tier progress, claims and the hemisphere choice — one `SaveCodec` rung, per the ladder rules.
- **In-journal UI only** — Appendix B bars the Play overlay anyway. The tide rides the pinned-tracker idiom (§8); the plate lives in the Record.
- **The events rail** *(added 2026-08-11)* — a column of cells down the **left edge of the world strip**, each a face, a countdown and a tap into its own popup. It exists because the Wheel's five surfaces were all conditional and all elsewhere: four of them draw nothing outside a tide, and inside one the tide could still be demoted to a grey suffix by any pinned verse, so the system was invisible to a player who never opened the fold sheet. The rail is the one surface that is always there. Rules it keeps: it costs the band's own left MARGIN, never the page's height and never the plates (the chrome budget rule, §13 — a bar that earns a pinned line must be read on every tab, and this is read twice a session; insetting the strip's rect to clear the rail moved every node plate over and shrank it, which is the plates paying for it — corrected 2026-08-12); cells are a full fingertip or they are dropped, urgent first; a cell never promises what cannot be collected (the weekly cache counts its week out rather than saying *take*, and says *sign in* when Play does not know the player; it said *look* until 2026-08-12, when the word gave way to the countdown and the cell learnt to stand down entirely once the week's cache has been taken); and the sabbat plate appears only once the tide opens, because the plate is what a keeping earns. Its second inhabitant is the **weekly Play cache** (§11) — deliberately, so it is a rail rather than a Wheel widget with delusions. It renders in the CAMERA rather than in the HUD's overlay canvas *(2026-08-12)*, at its own rung of `StripLayers` — the rail stands ON the band, and the band's own windfalls have to be able to drift across it; an overlay canvas is drawn after the camera has finished, so every one of them passed behind the cells and came out the top. That settles the tap as well: a press over a cell with a windfall under the finger **catches**, and the cell's own click is swallowed on the release — a cell that opened a popup while the picture under the finger was a windfall would be reading the player's aim off the thing they could not see. A press that catches nothing still belongs to the cell. Its clocks read in two units again *(2026-08-12)*, the same `NumberFormat.Countdown` the sheets and the Camp row read: a cell saying "1d" over a sheet saying "1d 3h" is one wait told two ways, and the coarse one is the half that can be acted on wrongly. The cache cell wears a **chest** rather than the Amber plate it wore first — amber is what is inside it, and the currency drawn on the strip reads as amber the player holds, which the ledger two rows above already says. From **2026-08-13** the rail is the Wheel's only always-on surface rather than one of three: the Trail's head gave up its countdown and the tracker its `· {sabbat}-tide` tail, both of which were the cell restated within one screen of it (§13 Phase 2). That is the rail paying for itself a second time — it was added to stop the Wheel being invisible, and it now stops it being said three ways at once.
- **The Record's shelf is a count and the names kept** *(2026-08-12, Mo's call — supersedes "the shelf reads forward", 2026-08-11)* — "3 of 8 kept" under a head naming the reckoning (THE SOUTHERN WHEEL / THE NORTHERN WHEEL), then the kept sabbats' plates and names in the wheel's own order. The two earlier shapes both failed on the same page: the eight in authored order against eight identical "unkept"s read as a checklist of things already missed, and the eight dated and ordered by what comes next read as a calendar, which is a thing to act on and so belongs where a player acts. The rail carries the next tide's countdown (the Trail's head carried it too until 2026-08-13, when it went as the cell's own caption read aloud — see §13 Phase 2); the Record holds what was done. The "not a wound" line goes with the unkept rows it was there to soften.

### Costs, honestly

- **Eight plates** in the one template — a bounded batch, reused every year. Scope pressure cuts count, never quality (§1): the four fire festivals alone are a coherent first ship (they carry the hemisphere mirror whole), quarters at the next pass.
- **~150–200 words** across eight tides, each sabbat's line doubling as its teaching note (§7's pattern; the touch's margin line is the same line, not an addition), sized deliberately against the post-MVP word budget.
- **A live-ops posture,** however mild: the calendar wants topping up every few years; each year's plates want nothing. The evergreen rule is what makes both true.

### Open questions (the v1.1 sitting)

- ~~Tide length: two weeks against a ~9–12 d FTP fold — should one fold always be able to keep one festival whole?~~ ✅ RESOLVED 2026-08-11 — yes. A month, which clears a ~9–12 d fold with room either side, and the question it leaves behind is the touch's size rather than the window's (see The observance, above).
- Does the sabbat verse scale with fold count at all? §9's rule says at most linearly; **flat** is simplest and probably right — the wheel greets every warden the same.
- Amber tier sizes against the weekly cache and the drip (§9's anchor).
- The ambient touch's sizes: the table's leans are unplaytested first guesses; the no-gate rule and the one-mastery-band ceiling are the lines to hold.
- With the drawn season retired (§8), do fallow-week runs read too alike? The parked per-run randomness (todo Appendix A) is the lever if so — and it returns as a second flavour beside the Wheel, never instead of it.
- Plate recurrence: identical each year (evergreen-safe) vs year-stamped variants (a bigger record, an unbounded art tail).
- Whether the PGS surface wants anything at v1.1 at all — the weekly-resources board already exists beside the wheel, and PGS resets offer no custom fortnights (§12's Leagues note covers Google's own events).
