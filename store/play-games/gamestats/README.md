# Game Stats — the Play Console side

The Level Up guideline asks for **5 repetitive stats, at least one of them usable
for competitive engagement, plus 1 player progression stat** (submitted through
the Game Stats API, client or server). What the game records lives in
`Assets/Scripts/Game/Services/GameStats.cs`; what the console must be told lives
here. They are one thing in two places — an event Play has not been told about is
silently dropped, so `GameStatsTests.EveryEventTheGameRecords_IsDeclaredInTheConsoleSchema`
fails the build if the two drift apart.

## Upload

**Grow users → Play Games Services → Setup and management → Game Stats**
(the path the integration guide gives, read 2026-08-12), then:

1. **`PlayerGameEvent.csv`** — declares the raw event schema (event name,
   property, type).
2. **A ZIP** of `RepetitiveStatsConfig.csv`, `ProgressionStatConfig.csv` and the
   stat icon files.

`StatLocalizations.csv` is not here: the game ships English only, and the display
names in the config CSVs are what a single-language title needs.

## What the console still owes, and when

Google's own timeline, from the [Game Stats guide](https://developer.android.com/games/pgs/gamestats)
as it read on **2026-08-12**: the API and SDK are "available for early feedback and
will be Generally Available (GA) starting August 2026", and **September 2026** is
when players start seeing game stats on their Gamer profile and when a **draft
stats configuration can be tested with test accounts**.

Read that carefully, because this file used to say something firmer than the guide
does. GA is dated to *this month* without a day, and the overview never promises a
month for the **Play Console upload experience** at all.

The integration page below is the better evidence, and it points the other way: it
gives a live console path and describes the upload as a thing you do, with no beta
language anywhere on it. Against that, the Play Console **help** page for Play
Games Services features still lists only achievements, leaderboards, saved games
and translations, with no mention of Game Stats, so the help side has not caught
up either way. On balance the screen is probably there; one look settles it, and
nothing in the repo can. The half that is certainly not ready before September is
proving the draft config against a test account, which batches with the Sep 1
rewards-association visit.

## The spec exists after all, on a page this file had never read

The overview guide is not where the CSV format lives. **[Integrate Game
Stats](https://developer.android.com/games/pgs/integrate-gamestats)** is, it gives
the console path above, and unlike the overview it carries no beta caveat at all.
Found 2026-08-12, and it says the authored CSVs were wrong in three ways that a
validator would have bounced. All three are now fixed here:

- **`HIGHER` was never a value.** The column takes `INCREASING` or `DECREASING`,
  in as many words: "specifies whether higher values `INCREASING` or lower values
  `DECREASING` are better". Every row said `HIGHER`.
- **`INT` was never a type.** `Property Type` is a case-sensitive enum of
  `INT64`, `DOUBLE`, `STRING`, `BOOL`. Two rows said `INT`, so the two integer
  events in the schema were the ones that would have failed.
- **`Is Competitive` is lower-case** `true` / `false`, not `TRUE` / `FALSE`.

**The icons are drawn — 2026-08-12.** The spec turned out to be exact: 512 × 512
(1:1), PNG or JPEG, at most 1 MB each, in the **root** of the ZIP. All seven sit
here beside the configs at 512 × 512 RGBA, 80–145 KB, so the ZIP is just this
folder's `stat-*.png` plus the two config CSVs. `make-store-art.py` grows a
`GAMESTAT_PLATES` table that is keyed off the CSVs themselves and fails if a stat
has no plate, so a stat cannot be added without an icon; the cards keep the
achievements' circular inset and vignette, because nothing published says what
shape a stat icon is displayed in and an inset ring costs nothing if it is square.

*A trap worth knowing before choosing any plate for a card:* three of the
committed plates were cut with no alpha channel, so the softening that hides a
straight crop has nothing to fade and they land as white rectangles on the
parchment. Two of them (`insect-windborne`, `insect-parchment-wings`) name these
stats better than what is drawn here and were rejected for exactly that reason.
The tool now prints `WHITE BOX` for any such card, which is also how it reports
that **three published achievement icons** already look that way (see
`docs/todo.md` §3.2).

**A caution for the first upload: the page contradicts itself, and we have taken a
side.** Its formal format lines and its worked example disagree about the config
headers, and the CSVs here were originally authored from the *example*. They now
follow the *format lines*, which drop the example's `Sequence number` column and
spell things differently (`Stat Id` not `Stat ID`, `Event Property Name` not
`Property Name`, `Aggregation Type` not `Aggregation`, `Min limit`/`Max limit` not
`minLimit`/`maxLimit`, `Unit` not `unit`). `ProgressionStatConfig.csv` gained the
`Event Property Name` column the format line requires and the example omits, so it
now names `currentProgress`. If the console rejects the file, the example's shape
is the immediate second attempt rather than a mystery, and one of the two spellings
is then confirmed for good. Stat ids must be letters, numbers and underscores, and
there is a ceiling of **50 stats** across both config files, so nothing here is
near a limit.

## The client SDK landed, and the game submits

**GPGS 2.2.0** (released 2026-07-31, vendored 2026-08-04) added the client half
this file used to be waiting on. `PlayGamesServices.RecordStat` builds a real
`PlayerGameEvent` and hands it to `PlayGamesPlatform.Instance.RecordEvent`,
`FlushStats` nudges `RequestEventsUpload` on the save cadence, and a dev build
logs `[play-games] game-stats: recorded <event>` per event as the on-device
instrument. Recording has been live **since 2026-08-04**, so the console side is
now the only unfinished half, and an event uploaded here that the client does not
record (or the reverse) is what the schema test catches.

One quirk to expect on a re-vendor: 2.2.0 ships its own `PluginVersion.cs` still
reading 2.1.0, and the copy in this repo is patched to 2.2.0 by hand. See
`docs/todo.md` §3.4, which carries the rest of the plugin's rough edges.
