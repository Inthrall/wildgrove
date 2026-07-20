# Wildgrove

*Working title.*

An idle gathering & collecting game for Google Play, pitched between **Melvor Idle**, **Idle Obelisk Miner**, and **Egg Inc** — about tending a wilderness that remembers. Walk deeper along the Trail, craft to survive, unearth the fossils of those who came before, and listen when the land speaks, which is rarely.

## Design pillars

| Axis | Lineage | What it is |
|---|---|---|
| **The Trail** | Idle Obelisk | Eight zones, each with new resources, a craft, a waystone, and (from Zone 3) dig sites |
| **Crafts** | Melvor Idle | Parallel gathering + survival-crafting skills — XP, mastery, mined-and-smithed tool tiers |
| **Migration** | Egg Inc | Prestige banks Verdure — the land's memory of careful hands — into the permanent Almanac |

The collection layer (Compendium + Fossil Wing, hand-drawn naturalist plates) and a sparse, elliptical narrative carry the identity. Built to meet the full [Google Play Level Up](https://developer.android.com/games/guidelines) guideline set.

## Documents

- [Design document](docs/design-doc.md) — vision, systems, economy math, narrative, Level Up compliance, MVP plan
- [The Warden's Journal](docs/wildgrove-journal.html) — the same document, styled (field-journal treatment)
- [Economy model](docs/economy-model.xlsx) — Phase 3 pacing spreadsheet: the hour-six spend proof and solved starting constants

## Stack (planned)

Unity 6 LTS · URP 2D · Vulkan-primary · Play Games Services v2 · AdMob

## After cloning

The Google/Firebase Unity packages referenced by `Packages/manifest.json` are
not committed (61 MB of tarballs). Fetch them once before opening the project:

```bash
tools/fetch-google-packages.sh
```

## License

Copyright © 2026 Mo Nicholson. All rights reserved. This repository is
publicly viewable for reference only — **any usage requires a paid
license**. See [LICENSE](LICENSE) for the full terms.
