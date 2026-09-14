# Closed testing

Wildgrove's Play account is a **personal account created after 13 November 2023**,
so closed testing is not a beta we choose to run. It is the gate in front of
production access. This file is what that gate actually asks for, checked against
Google's own pages on **2026-09-15**, and the order to do it in.

## The gate

**Twelve testers, opted in continuously for fourteen days, before you may apply
for production access.** From [App testing requirements for new personal developer
accounts](https://support.google.com/googleplay/android-developer/answer/14151465)
(read 2026-09-15):

- It applies to personal accounts created after **13 Nov 2023**. Organisation
  accounts are exempt; ours is not.
- The fourteen days must be **continuous**. A tester who opts in, plays, and opts
  out before the fourteenth day does not count, and one who opts out and back in
  starts the count again from zero. There is no partial credit.
- Applying is **Dashboard → Apply for production**. The form covers three things:
  the closed test, the app, and production readiness. It asks whether "testers
  used all available app features" and whether "tester usage matched expected
  production user behavior".
- Review is "usually seven days or less, but can occasionally take longer".

**The letter of the rule is opt-in; the spirit is use.** Twelve names who never
open the game satisfy the counter and are precisely what those readiness questions
exist to catch. So recruit people who will actually play, and recruit **more than
twelve**, because a single opt-out silently resets that tester and you will not
notice until you count on day fourteen.

## The critical path, which is not the obvious one

The fourteen days are the long pole and nothing shortens them. Everything else
outstanding fits **inside** that window: the narrative pass, the audio pass, the
device sitting, the onboarding nudges, the three white-box achievement icons. A
closed test accepts new builds the whole time it runs, and it should get them.

The one sequencing mistake that costs two weeks is polishing first and starting
the clock afterwards. Get the track live, get twelve people opted in, start the
clock, then keep building while it runs.

## What must be green before the track goes live

Play will not roll out a closed release until the console stops complaining. As of
2026-09-15:

| Item | State |
|---|---|
| Signed AAB on the closed track | CI already uploads to `internal` on every push to main; `android-release.yml` takes a `track` dispatch input, so this is a workflow dispatch |
| Data safety form | ✅ answered and submitted 2026-09-09 (todo §3.2 holds the answer of record) |
| Privacy policy URL | ✅ live, `https://decryptic.app/wildgrove/privacy` |
| App access | ✅ answered: all functionality available without special access |
| Ads declaration | contains ads, yes (AdMob) |
| Content rating questionnaire | **not started** |
| Target audience and content | **not started** |
| Store listing: name, descriptions | **draft below, not in the console** |
| Store listing: icon, feature graphic | **do not exist** |
| Store listing: screenshots | **do not exist**; harness exists, see below |
| Countries and regions for the track | **not chosen** |
| Tester list | **does not exist** |

Note that the Play Games Services testers list is a **separate** list from the
closed-track tester list, and the two do not feed each other. A tester who is on
the track but not on the PGS project's list sees the game but not its achievements
or stats.

## Listing assets: the specs, and one trap

From [the store listing asset requirements](https://support.google.com/googleplay/android-developer/answer/9866151)
(read 2026-09-15):

- **App icon**: 512 x 512, 32-bit PNG **with alpha**, at most 1024 KB.
- **Feature graphic**: 1024 x 500, JPEG or 24-bit PNG, **no alpha**.
- **Phone screenshots**: at least 2. Each side between 320 and 3840 px, and
  **the long side may be at most twice the short side**.
- **Tablet screenshots**: 4 each for 7-inch and 10-inch, 1080 to 7680 px, at 16:9
  landscape or 9:16 portrait.
- Promotional eligibility wants at least 4 phone shots at 1080p or better.

**⚠️ The harness's default size is out of spec, and Play will reject the set.**
`StoreScreenshots.ShotSize()` defaults to **1080 x 2400**, which is 2.22 times its
short side against a limit of 2. Pass `WILDGROVE_SHOT_SIZE=1080x1920` instead: it
is inside the rule, it is exactly the 9:16 the tablet spec asks for, and it is the
promo-eligible resolution. The default is fine for looking at the game and wrong
for uploading, which is the kind of difference that only shows up at the console.

The short description limit is 80 characters. The page above does not carry the
app name or full description limits; the console enforces **30** and **4000**.

## Capturing the screenshots (a hand job, not a script)

The harness needs a real GameView, so it runs the **GUI** editor, not batchmode.
It stages a showcase save, sets the game view size, enters play, walks the nav
pages capturing one shot each, then restores your real save and closes the editor.
It owns the session, so do not use the machine for Unity while it runs.

```powershell
$env:WILDGROVE_STORE_CAPTURE = "1"
$env:WILDGROVE_SHOT_DIR = "C:\Personal\Wildgrove-testout\store-shots"
$env:WILDGROVE_SHOT_SIZE = "1080x1920"
& "C:\Program Files\Unity\Hub\Editor\6000.5.8f1\Editor\Unity.exe" `
    -projectPath "C:\Personal\Wildgrove" `
    -executeMethod Wildgrove.EditorTools.StoreScreenshots.CaptureForCli
```

Then the landscape set for the tablet slots, which is the one layout no portrait
shot can show:

```powershell
$env:WILDGROVE_SHOT_SIZE = "2560x1440"
```

**Read the log before trusting a shot.** todo §2 carries an open bug: the harness
has photographed a run that was not the showcase, and the mechanism is still
unproven. `WarnIfNotTheShowcase` logs an error naming both runs when the woken run
does not match the staged one. A shot of the wrong save looks completely fine.

## Testers

From [Set up an open, closed, or internal test](https://support.google.com/googleplay/android-developer/answer/9845334)
(read 2026-09-15):

- Two ways in: **email lists** (up to 200 lists, up to 2,000 addresses each, at
  most 50 lists on one track) or a **Google Group**.
- Testers cannot find a closed test by searching the Play Store. They need the
  **opt-in link**, which explains the responsibilities and carries the opt-in.
- An internal tester cannot be in a closed or open test at the same time without
  opting out of the internal one first. Worth knowing before wondering why someone
  cannot see the build.

A Google Group is the better instrument here even though an email list is fewer
clicks: the group gives a place for testers to send feedback, and the production
application asks what the feedback was.

## Listing copy (draft, and it is a draft)

Written 2026-09-15 against the name decision of the same day (keep "Wildgrove"; no
Play Store collision and no game or software trademark found, though a web search
is not a register search). **Every line here is first-draft register**, the same
condition as the rest of the game's words per todo §1.4, and the narrative pass
owns it before it goes in the console.

**App name** (30 max)

```
Wildgrove
```

**Short description** (80 max)

```
A quiet grove to tend. Gather, befriend, and draw what you find into a journal.
```

**Full description** (4000 max)

```
You keep a grove, and a journal about it.

Wildgrove is an idle game with the pace of an afternoon. Nodes give up berries and
clay and ice while you are away; companions you have befriended work the ground
beside you; and the whole of it is read through a warden's journal rather than a
dashboard, one page a tab, plates where another game would put numbers.

Walk the grove and tend it by hand when you are there. Post your companions where
the work is. Watch for a windfall crossing the strip and catch it. Then put the
book down, and find the baskets full when you open it again.

TEND
Five zones, from the meadow to the Cloudreach Peaks, each with its own ground to
work and its own weather in the margins. Nodes replant, stations craft, and the
trail carries it home without losing any of it.

KEEP COMPANY
A roster of animals who arrive, stay, and get better at what they do. Each carries
one fixed trait from the day it arrives: that is identity, not a build. Bonds are
permanent honours and there are not many of them.

DRAW
Observe, sketch, release. Insects are found rather than caught, and a recorded
plate goes into the Folio in your own hand. A page not yet earned keeps its
secret.

THE WHEEL
Eight seasons on the old calendar, each holding until the next one takes it, in
whichever hemisphere you are standing. The grove leans with them. Keep the
observance and the season pays.

BEGIN AGAIN
Fold the run and carry your Renown forward into the next one, with the map walked
and the ground already known. The grove is the same; you are not.

No timers you have to be there for. No energy bar. Nothing that stops when you
close it.
```

## The order to work in

1. **Recruit twelve people** and get the Google Group standing. Nothing else here
   has a two-week tail; this does. Start it before the listing is finished.
2. **App icon and feature graphic.** They do not exist, they are the only two
   listing assets with no harness behind them, and the track will not go live
   without them. `tools/make-store-art.py` is the precedent for generating them
   in the game's own plate idiom.
3. **Screenshots**, with the size fix above, reading the log for the showcase
   warning.
4. **Content rating and target audience** in the console. Answer them off what
   the game actually does, the way the Data Safety table was derived, rather than
   off what feels right. Ads and IAP both need declaring.
5. **Listing copy** into the console, after the narrative pass has had the draft
   above.
6. **Dispatch `android-release.yml` at the closed track**, pick countries, roll
   out, and send the opt-in link to the group.
7. **Start counting.** Day fourteen is the earliest you may apply, and that is
   only true if nobody has dropped out.
