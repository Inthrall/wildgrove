#!/usr/bin/env bash
# Downloads the Google/Firebase Unity packages that Packages/manifest.json
# references as file: tarballs. They are NOT committed (the firebase.app tgz
# alone is 61 MB and would bloat every clone forever) — run this once after
# cloning, and CI runs it before every Unity invocation. Sources are Google's
# official Unity package registry endpoint; versions are pinned here and must
# match manifest.json.
#
# The other half of that arrangement, because it looks like an oversight and
# isn't: Assets/GeneratedLocalRepo/Firebase IS committed, 22 MB of it. EDM4U
# generates that maven repo by copying the .srcaar out of the package above,
# and settingsTemplate.gradle points Gradle straight at the directory — so the
# committed copy is what every Android build actually resolves against. No CI
# job runs the resolver (AndroidResolverRunner.ForceResolve exists but nothing
# calls it; EDM's auto-resolution does not run in batchmode), which means
# deleting those files does not make the build regenerate them, it makes the
# build fail to find firebase-app-unity. The bytes are also already in history,
# so removing them now would not shrink a clone by one byte.
#
# What that leaves is drift: the tarball version is pinned here, the generated
# repo is pinned by whenever someone last resolved, and nothing connects the
# two. Bump the versions above without re-resolving and the build links the OLD
# Firebase while every file in the repo says otherwise. So after fetching, the
# committed aars are checked against the tarballs they are supposed to have
# come from, and a mismatch stops the build rather than shipping quietly.
#
# Usage: tools/fetch-google-packages.sh
set -euo pipefail

dir="$(cd "$(dirname "$0")/.." && pwd)/Packages/GooglePackages"
mkdir -p "$dir"

packages=(
  "com.google.external-dependency-manager-1.2.186"
  "com.google.firebase.analytics-13.13.0"
  "com.google.firebase.app-13.13.0"
  "com.google.firebase.crashlytics-13.13.0"
)

for pkg in "${packages[@]}"; do
  file="$dir/$pkg.tgz"
  name="${pkg%-*}"
  if [ -s "$file" ]; then
    echo "ok: $pkg.tgz already present"
    continue
  fi
  url="https://dl.google.com/games/registry/unity/$name/$pkg.tgz"
  echo "fetching $url"
  curl --fail --location --silent --show-error --retry 3 --output "$file.part" "$url"
  mv "$file.part" "$file"
done

echo "All Google packages present in Packages/GooglePackages/."

# --- the committed maven repo still matches the packages above ---------------
# Only the Firebase artifacts: the Play Games one is generated from a package
# that is itself committed, so it cannot drift from a version pinned here.
root="$(cd "$(dirname "$0")/.." && pwd)"
repo="$root/Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase"
scratch="$(mktemp -d)"
trap 'rm -rf "$scratch"' EXIT

drifted=0
for product in analytics app crashlytics; do
  version="13.13.0"
  artifact="firebase-$product-unity-$version"
  committed="$repo/firebase-$product-unity/$version/$artifact.aar"
  tarball="$dir/com.google.firebase.$product-$version.tgz"

  if [ ! -f "$committed" ]; then
    echo "DRIFT: $artifact.aar is missing from Assets/GeneratedLocalRepo — the Android build cannot resolve it." >&2
    drifted=1
    continue
  fi

  tar -xzf "$tarball" -C "$scratch" --wildcards "*/$artifact.srcaar" 2>/dev/null || true
  extracted="$(find "$scratch" -name "$artifact.srcaar" -print -quit)"
  if [ -z "$extracted" ]; then
    echo "DRIFT: $tarball holds no $artifact.srcaar — the pinned version moved." >&2
    drifted=1
    continue
  fi

  if cmp -s "$extracted" "$committed"; then
    echo "ok: $artifact.aar matches the pinned package"
  else
    echo "DRIFT: $artifact.aar differs from the one in $tarball." >&2
    echo "       Re-resolve and commit the result:" >&2
    echo "       Unity -batchmode -quit -projectPath . -executeMethod Wildgrove.EditorTools.AndroidResolverRunner.ForceResolve" >&2
    drifted=1
  fi
done

if [ "$drifted" -ne 0 ]; then
  echo "Assets/GeneratedLocalRepo is out of step with Packages/GooglePackages — see above." >&2
  exit 1
fi
