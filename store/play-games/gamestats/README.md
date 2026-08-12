# Game Stats — the Play Console side

The Level Up guideline asks for **5 repetitive stats, at least one of them usable
for competitive engagement, plus 1 player progression stat** (submitted through
the Game Stats API, client or server). What the game records lives in
`Assets/Scripts/Game/Services/GameStats.cs`; what the console must be told lives
here. They are one thing in two places — an event Play has not been told about is
silently dropped, so `GameStatsTests.EveryEventTheGameRecords_IsDeclaredInTheConsoleSchema`
fails the build if the two drift apart.

## Upload

Play Console → Play Games Services → Game Stats, then:

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
does. GA is dated to *this month* without a day, and the guide never promises a
month for the **Play Console upload experience** at all. So whether the CSVs here
can be uploaded today is a question only the console can answer: look for Play
Games Services → Game Stats on the next visit, and if it is there, upload. The
half that is certainly not ready before September is proving the draft config
against a test account, which batches with the Sep 1 rewards-association visit.

Two consequences worth keeping straight:

- **The icons are not drawn yet.** The column is filled in with the names they
  will have (`stat-*.png`), but Google has published no size or shape spec for
  stat icons: the guide asks only that you "provide a unique icon representing
  the stat by entering the exact icon filename in the CSV file", with no pixel
  dimensions and no format, re-checked 2026-08-12. Guessing is how you draw six
  images twice. Generate them with `tools/make-store-art.py` once the console
  tells us the format.
- **The column spellings are inferred, not documented.** This file used to claim
  they came from the guide's column list; they do not. The guide describes both
  columns in prose only, "whether an increasing value or decreasing value is
  good for the player" and "an optional unit of measurement for the stat such as
  km, miles, and seconds", and publishes no allowed set for either. So `HIGHER`
  and the free-text units are our reading of that prose, and the console is the
  authority the first time these are uploaded. Expect one round of correction.

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
