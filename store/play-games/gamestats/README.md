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
as it reads on **2026-09-09**: one milestone row, **September 2026 — "Players start
seeing game stats on their Gamer profile"**, and one plain sentence, "The Game Stats
UI will be available in September 2026". That is the whole of what is still ahead.

**The guide was rewritten between 2026-08-13 and 2026-09-09, and the caveats went
with it.** It used to call the API "available for early feedback and will be
Generally Available (GA) starting August 2026" and to promise no month at all for
the Play Console upload experience. The words "beta" and "early feedback" no longer
appear on the page. GA has happened, and the note below about the console running
ahead of its documentation is now history rather than a live warning: the
documentation caught up.

**Uploaded and accepted 2026-08-13.** All seven events and all seven stats sit in
the console as **Draft — available to testers**, icons rendering in the stat list,
which is the third place today where the console runs ahead of its own
documentation: the guide dates draft-config testing to September 2026 and the
console is offering it now. Two things follow, and they are the opposite of the
obvious ones:

- ~~**Do not publish yet.**~~ **DECIDED 2026-09-09 (Mo): publish. Closed testing
  has not begun, so there are no testers and no players: a production stats config
  can disturb nothing, which makes this the cheapest the click will ever be, and
  it takes both these figures off the board before anyone is looking at them.** The
  argument it overrides, kept because it is the one that will come back if a stat
  ever needs deleting: in draft every row still has a `Delete`, and this config
  needed three corrections on its first day. Once published, a row is a public
  artifact carrying player data, and it stays. So **get the schema right before
  the click, not after** — the CSVs in this folder are the thing to re-read, and
  `GameStatsTests.EveryEventTheGameRecords_IsDeclaredInTheConsoleSchema` is what
  proves code and CSV still agree. (Tester access is the PGS project's own testers
  list, not the `Settings → License testing` list todo §3.2 tracks for purchases.)
- **Publish is project-wide, which is why the decision above is one decision and
  not two.** Publishing the three corrected achievement step counts (§2) promotes
  this stats draft to production in the same act, and vice versa. The console
  gives no hint of it, which is the reason it is written down here.
  <br>**One caution that follows from it:** publish promotes *everything* staged,
  not only the rows you came to change. Before clicking, look over what is sitting
  in draft on both screens, because a half-finished row made by hand weeks ago goes
  live with the rest.

**Settled 2026-08-13: the screen is there, and it validates.** The question above
was answered by walking into the console. What September still owns, as far as
anything published says, is **players** seeing stats on the Gamer profile; the
tester half turned out not to be waiting for it at all.

## What the validator actually enforces — 2026-08-13, the expensive way

The first upload was **rejected on content, not on shape**, which settles several
guesses at once. The header row was accepted exactly as authored, so the page's
formal format lines are what the console implements and dropping the worked
example's `Sequence Number` column was right. `INT64`, `INCREASING`, lower-case
`true`/`false` and the seven 512 × 512 icons all passed without comment.

Twelve errors came back across six rows, and they reduce to two rules the page
states thinly and one it does not explain at all:

- **`Unit` is a closed enum of physical units, not a label.** Distance
  (`METER`, `KILOMETER`, `MILE`, `FOOT`, …), speed (`KILOMETER_PER_HOUR`, …),
  percentage, or empty/`UNITLESS`. Every stat here counts *things*, so **every
  `Unit` is now empty**: `items`, `windfalls`, `specimens`, `verses`,
  `migrations` and `trails` were display words, and this column was never for
  those.
- **On a `STRING` or `BOOL` property the `Unit` column must be empty**, and a
  value there is read as a *formatting configuration*. That is what the console's
  most confusing message means: "Invalid format — formatting configuration is
  allowed only for duration-type stats" appeared on precisely the three rows that
  count a string (`windfallCaught`, `specimenFixed`, `verseCompleted`), while the
  three numeric rows got "Invalid unit" instead. Same mistake, two error messages,
  told apart by the property's type.
- **The limits invert on `Is Competitive`.** Both `Min limit` and `Max limit` are
  **required** when it is `true`, and both must be **absent** when it is `false`.
  A single `0` in every row was wrong in both directions simultaneously.

**The competitive ceiling is a judgement, recorded so it can be revisited.**
`resources_gathered` is the competitive stat (§12), and it sums a lifetime
`BigDouble` total in a game that needs `BigDouble` precisely because Renown
outgrows `long` early in an idle run, which is why the Renown leaderboard submits
`log10 × 1e6` instead of a raw figure. Against that, a fixed "maximum allowable
score" is an awkward fit. The declared range is **0 to 9007199254740991**, the
largest integer a `double` holds exactly: it keeps §12 intact, it is one config
edit to change, and Play documents nothing about what happens to a value above the
ceiling — clamped, dropped or flagged is unknown. If it ever bites, the better
answer is moving the competitive flag to a naturally bounded stat (`verses_sung`
tops out near 37, `migrations` in the tens), which would also sidestep the
extraction-race objection §12's Leagues note raises against ranking on
most-gathered.

*One loose thread:* the competitive-limits error named the offending stat as
`""` rather than `resources_gathered`. Either the validator interpolates a field
we leave blank, or it reads the stat's name from a column we are not filling the
way it expects. If that empty name comes back on a later upload, it is worth
chasing then; nothing else suggested a mapping problem.

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

**~~A caution for the first upload~~ — settled 2026-08-13: the console took the
format lines' shape without complaint, so the paragraph below is history rather
than a live risk. Kept because it explains why the headers read as they do.** Its formal format lines and its worked example disagree about the config
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
