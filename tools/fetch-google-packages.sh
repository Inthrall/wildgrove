#!/usr/bin/env bash
# Downloads the Google/Firebase Unity packages that Packages/manifest.json
# references as file: tarballs, and generates the local maven repo the Android
# build resolves Firebase from. Neither is committed: the firebase.app tgz alone
# is 61 MB and the generated repo another 22 MB, and both are derivable from a
# pinned version number. Run this once after cloning, and CI runs it before
# every Unity invocation. Sources are Google's official Unity package registry
# endpoint; versions are pinned here and must match manifest.json.
#
# The generated half used to be committed, because EDM4U is what normally
# writes it (Assets > External Dependency Manager > Android Resolver > Force
# Resolve) and its auto-resolution does not run in batchmode — so no CI job was
# ever going to produce it, and deleting the files made the build fail to find
# firebase-app-unity rather than regenerate them. What EDM4U actually does for
# these three artifacts, though, is a copy: the .srcaar inside the package
# becomes the .aar in the repo, byte for byte, beside the package's own .pom.
# That is reproducible here in a few lines, and reproducing it kills the drift
# this file used to only detect — the tarball version and the generated repo
# can no longer disagree, because one is built from the other every time.
#
# The .meta files matter and are not decoration: an .aar under Assets/ with no
# .meta is imported as an Android plugin and lands in the build a second time,
# so the generated ones disable the plugin on every platform, exactly as EDM4U
# writes them. An existing .meta keeps its GUID, so re-running this does not
# churn the AssetDatabase.
#
# Usage: tools/fetch-google-packages.sh
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
dir="$root/Packages/GooglePackages"
mkdir -p "$dir"

# Pinned versions. These must match Packages/manifest.json; the Firebase one
# also names the directory the generated repo is written to, so a bump here
# regenerates it on the next run.
edm_version="1.2.186"
firebase_version="13.13.0"
firebase_products=(analytics app crashlytics)

packages=(
  "com.google.external-dependency-manager-$edm_version"
  "com.google.firebase.analytics-$firebase_version"
  "com.google.firebase.app-$firebase_version"
  "com.google.firebase.crashlytics-$firebase_version"
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

# --- the maven repo the Android build resolves Firebase from -----------------
# Only the Firebase artifacts: the Play Games repo is generated from a package
# that is itself committed, so it cannot drift from a version pinned here.
repo_root="$root/Assets/GeneratedLocalRepo"
repo="$repo_root/Firebase/m2repository/com/google/firebase"
scratch="$(mktemp -d)"
trap 'rm -rf "$scratch"' EXIT

# A GUID Unity will accept: the one already on disk when there is one (so a
# re-run leaves the AssetDatabase alone), else the asset path's md5, which is
# 32 hex digits and the same on every machine that ever generates this file.
guid_for() {
  local asset="$1"
  if [ -f "$root/$asset.meta" ]; then
    local existing
    existing="$(sed -n 's/^guid: \([0-9a-f]\{32\}\)$/\1/p' "$root/$asset.meta" | head -1)"
    if [ -n "$existing" ]; then
      printf '%s' "$existing"
      return
    fi
  fi
  printf '%s' "$asset" | md5sum | cut -c1-32
}

write_folder_meta() {
  local asset="$1"
  # Read the existing GUID before opening the file for writing: the redirection
  # truncates it, so a guid_for inside the here-doc would always find an empty
  # file and mint a new one on every run.
  local guid
  guid="$(guid_for "$asset")"
  cat > "$root/$asset.meta" <<META
fileFormatVersion: 2
guid: $guid
folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
META
}

# EDM4U labels what it generates "gpsr" and disables the plugin everywhere:
# Gradle resolves the .aar out of this repo by coordinate, so importing it as a
# plugin as well is how you get duplicate classes.
write_aar_meta() {
  local asset="$1"
  # Read the existing GUID before opening the file for writing: the redirection
  # truncates it, so a guid_for inside the here-doc would always find an empty
  # file and mint a new one on every run.
  local guid
  guid="$(guid_for "$asset")"
  cat > "$root/$asset.meta" <<META
fileFormatVersion: 2
guid: $guid
labels:
- gpsr
PluginImporter:
  externalObjects: {}
  serializedVersion: 3
  iconMap: {}
  executionOrder: {}
  defineConstraints: []
  isPreloaded: 0
  isOverridable: 0
  isExplicitlyReferenced: 0
  validateReferences: 1
  platformData:
    Android:
      enabled: 0
      settings: {}
    Any:
      enabled: 0
      settings: {}
    Editor:
      enabled: 0
      settings:
        DefaultValueInitialized: true
  userData: 
  assetBundleName: 
  assetBundleVariant: 
META
}

write_pom_meta() {
  local asset="$1"
  # Read the existing GUID before opening the file for writing: the redirection
  # truncates it, so a guid_for inside the here-doc would always find an empty
  # file and mint a new one on every run.
  local guid
  guid="$(guid_for "$asset")"
  cat > "$root/$asset.meta" <<META
fileFormatVersion: 2
guid: $guid
labels:
- gpsr
DefaultImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
META
}

# Nothing to do when every artifact is already the one its tarball holds —
# the common case, and it keeps a re-run from re-importing 22 MB.
current=1
for product in "${firebase_products[@]}"; do
  artifact="firebase-$product-unity-$firebase_version"
  built="$repo/firebase-$product-unity/$firebase_version/$artifact.aar"
  [ -f "$built" ] || { current=0; break; }
  tar -xzf "$dir/com.google.firebase.$product-$firebase_version.tgz" -C "$scratch" \
    --wildcards "*/$artifact.srcaar" 2>/dev/null || true
  packaged="$(find "$scratch" -name "$artifact.srcaar" -print -quit)"
  if [ -z "$packaged" ] || ! cmp -s "$packaged" "$built"; then
    current=0
    break
  fi
done

if [ "$current" -eq 1 ]; then
  echo "ok: Assets/GeneratedLocalRepo holds the pinned Firebase $firebase_version."
  exit 0
fi

# Wholesale, so a version bump cannot leave the old one behind for Gradle to
# find: Firebase/ is regenerated from nothing every time it is regenerated at
# all. Only Firebase/ — GeneratedLocalRepo may hold other resolvers' output.
echo "generating Assets/GeneratedLocalRepo/Firebase from the pinned packages"
rm -rf "$repo_root/Firebase" "$repo_root/Firebase.meta"
mkdir -p "$repo"

for product in "${firebase_products[@]}"; do
  artifact="firebase-$product-unity-$firebase_version"
  packaged_dir="Firebase/m2repository/com/google/firebase/firebase-$product-unity/$firebase_version"
  target="$repo/firebase-$product-unity/$firebase_version"
  mkdir -p "$target"

  # Failure is reported below rather than by tar, which says "Not found in
  # archive" without saying which pin moved.
  tar -xzf "$dir/com.google.firebase.$product-$firebase_version.tgz" -C "$scratch" \
    --wildcards "*/$packaged_dir/$artifact.srcaar" "*/$packaged_dir/$artifact.pom" 2>/dev/null || true

  srcaar="$(find "$scratch" -name "$artifact.srcaar" -print -quit)"
  pom="$(find "$scratch" -name "$artifact.pom" -print -quit)"
  if [ -z "$srcaar" ] || [ -z "$pom" ]; then
    echo "com.google.firebase.$product-$firebase_version.tgz holds no $artifact.srcaar/.pom — the pinned version moved." >&2
    exit 1
  fi

  # The rename is the whole of what EDM4U does here: .srcaar is an .aar that
  # Unity has been told not to import.
  cp "$srcaar" "$target/$artifact.aar"
  cp "$pom" "$target/$artifact.pom"
  rm -rf "${scratch:?}/"*

  relative="Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-$product-unity"
  write_folder_meta "$relative"
  write_folder_meta "$relative/$firebase_version"
  write_aar_meta "$relative/$firebase_version/$artifact.aar"
  write_pom_meta "$relative/$firebase_version/$artifact.pom"
  echo "ok: $artifact.aar generated from the pinned package"
done

write_folder_meta "Assets/GeneratedLocalRepo"
write_folder_meta "Assets/GeneratedLocalRepo/Firebase"
write_folder_meta "Assets/GeneratedLocalRepo/Firebase/m2repository"
write_folder_meta "Assets/GeneratedLocalRepo/Firebase/m2repository/com"
write_folder_meta "Assets/GeneratedLocalRepo/Firebase/m2repository/com/google"
write_folder_meta "Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase"

echo "Assets/GeneratedLocalRepo/Firebase now holds Firebase $firebase_version."
