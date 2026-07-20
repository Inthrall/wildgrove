#!/usr/bin/env bash
# Downloads the Google/Firebase Unity packages that Packages/manifest.json
# references as file: tarballs. They are NOT committed (the firebase.app tgz
# alone is 61 MB and would bloat every clone forever) — run this once after
# cloning, and CI runs it before every Unity invocation. Sources are Google's
# official Unity package registry endpoint; versions are pinned here and must
# match manifest.json.
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
