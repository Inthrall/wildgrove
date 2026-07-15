# Dev setup

## Prerequisites

1. **Unity Hub** — `winget install Unity.UnityHub`
2. **Unity 6 LTS (6000.0.x)** via Hub, with modules:
   - Android Build Support (**+ OpenJDK + Android SDK & NDK** — tick all three)
3. **Git LFS** — `git lfs install` (once per machine) before adding binary art

## Project conventions (locked at Phase 0 — see design doc §12)

| Concern | Decision |
|---|---|
| Editor | Unity 6 LTS (pin exact version in `ProjectSettings/ProjectVersion.txt`) |
| Render pipeline | URP, 2D Renderer |
| Graphics API | **Vulkan first** in Player Settings → Android → Graphics APIs (Level Up requirement) |
| Big numbers | BreakInfinity.cs (`BigDouble`) for all currencies — vendored at `Assets/Plugins/BreakInfinity/`; content defs surface costs as `BigDouble` |
| Content | Data-driven: resources / upgrades / recipes / gear / rites / dialogue authored as JSON in `design/data/`, validated and imported into a ScriptableObject database (`Assets/Resources/Data/GameData.asset`) by `GameDataImporter` — auto on editor load and build, or `Wildgrove > Import Design Data`. Balancing must never require code changes |
| Saves | Versioned JSON with migration hooks; cloud = Saved Games API (Phase 5) |
| Input | All interactions through an input abstraction from day one (touch / mouse / keyboard / gamepad) |
| Min spec | Android 8.0 (API 26)+, target latest API per Play policy |

## Unity project creation checklist

- [ ] Create project from **2D (URP)** template at repo root
- [ ] Player Settings → Android: uncheck *Auto Graphics API*, order = **Vulkan, OpenGLES3**
- [ ] Set company/product: Inthrall / Wildgrove; package id e.g. `com.inthrall.wildgrove`
- [ ] Switch platform to Android; verify a device build renders 60 fps
- [ ] Add packages: Input System, TextMeshPro; import BreakInfinity
- [ ] Google Play Games plugin v2 (`com.google.play.games`) + init in bootstrap scene
- [ ] Generate a signing keystore — keep OUT of git (`.gitignore` already blocks `*.keystore`/`*.jks`); back it up somewhere safe, losing it means losing the Play listing
- [ ] Play Console: create app, enable Play Games Services, internal testing track

## CI

`.github/workflows/android-build.yml` builds an AAB via game-ci. Manual trigger until:
1. the Unity project exists (`ProjectVersion.txt` drives the editor version), and
2. `UNITY_LICENSE` / `UNITY_EMAIL` / `UNITY_PASSWORD` secrets are set — see https://game.ci/docs/github/activation

Then switch the trigger to `push: branches: [main]`.
