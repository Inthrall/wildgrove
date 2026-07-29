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

## Not yet possible, and why

Google's own timeline, from the [Game Stats guide](https://developer.android.com/games/pgs/gamestats):
the API is in **early feedback**, GA from **July 2026**, and the **Play Console
upload experience goes live August 2026**. So this cannot be uploaded yet.

Two consequences worth keeping straight:

- **The icons are not drawn yet.** The column is filled in with the names they
  will have (`stat-*.png`), but Google has published no size or shape spec for
  stat icons, and guessing one is how you draw six images twice. Generate them
  with `tools/make-store-art.py` once the console tells us the format.
- **The column spellings are Google's documented ones, not verified ones.**
  `HIGHER` for good-value direction and the free-text `unit` values are taken
  from the guide's column list; the console is the authority the first time these
  are uploaded. Expect one round of correction.

## The client SDK is also missing

The game records nothing on a device yet — not a wiring bug. Client integration
"will be made available using Unity, Java and C++ SDKs"; the shipped Unity plugin
(**GPGS 2.1.0**, July 2025, still the latest release) has no `PlayerGameEvent`
and no `RecordEvent`, and the Java coordinate is unpublished, so there is nothing
to bridge over JNI either. `PlayGamesServices.RecordStat` counts what it could
not send and says so in logcat; when the plugin lands, that one method body is
the whole change.
