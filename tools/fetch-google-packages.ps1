# Downloads the Google/Firebase Unity packages that Packages/manifest.json
# references as file: tarballs, and generates the local maven repo the Android
# build resolves Firebase from. Neither is committed: the firebase.app tgz alone
# is 61 MB and the generated repo another 22 MB, and both are derivable from a
# pinned version number. Run this once after cloning, and CI runs it before
# every Unity invocation. Sources are Google's official Unity package registry
# endpoint; versions are pinned here and must match manifest.json.
#
# Usage: .\tools\fetch-google-packages.ps1

$ErrorActionPreference = "Stop"

# Get the script directory, handling both direct execution and dot-sourcing
$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
# Script is in tools/, parent is the project root (Wildgrove)
$root = Split-Path -Parent $scriptDir
$dir = "$root\Packages\GooglePackages"
New-Item -ItemType Directory -Path $dir -Force | Out-Null

# Pinned versions. These must match Packages/manifest.json
$edm_version = "1.2.186"
$firebase_version = "13.13.0"
$firebase_products = @("analytics", "app", "crashlytics")

$packages = @(
	"com.google.external-dependency-manager-$edm_version",
	"com.google.firebase.analytics-$firebase_version",
	"com.google.firebase.app-$firebase_version",
	"com.google.firebase.crashlytics-$firebase_version"
)

foreach ($pkg in $packages) {
	$file = "$dir\$pkg.tgz"
	$name = $pkg -replace "-[0-9.]+$", ""

	if ((Test-Path $file) -and (Get-Item $file).Length -gt 0) {
		Write-Host "ok: $pkg.tgz already present"
		continue
	}

	$url = "https://dl.google.com/games/registry/unity/$name/$pkg.tgz"
	Write-Host "fetching $url"

	$tempFile = "$file.part"
	$retries = 3
	$success = $false

	for ($i = 0; $i -lt $retries; $i++) {
		try {
			Invoke-WebRequest -Uri $url -OutFile $tempFile -ErrorAction Stop
			$success = $true
			break
		}
		catch {
			if ($i -eq $retries - 1) {
				throw $_
			}
			Start-Sleep -Seconds 1
		}
	}

	if ($success) {
		Move-Item -Path $tempFile -Destination $file -Force
	}
}

Write-Host "All Google packages present in Packages/GooglePackages/."

# --- the maven repo the Android build resolves Firebase from -----------------
$repo_root = "$root\Assets\GeneratedLocalRepo"
$repo = "$repo_root\Firebase\m2repository\com\google\firebase"
$scratch = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), [System.IO.Path]::GetRandomFileName())
New-Item -ItemType Directory -Path $scratch -Force | Out-Null

function guid_for {
	param([string]$asset)

	$metaPath = "$root\$asset.meta"
	if (Test-Path $metaPath) {
		$content = Get-Content $metaPath -Raw
		if ($content -match "guid: ([0-9a-f]{32})") {
			return $matches[1]
		}
	}

	# Generate MD5 from asset path
	$bytes = [System.Text.Encoding]::UTF8.GetBytes($asset)
	$md5 = New-Object System.Security.Cryptography.MD5CryptoServiceProvider
	$hash = $md5.ComputeHash($bytes)
	$guid = ""
	foreach ($byte in $hash) {
		$guid += $byte.ToString("x2")
	}
	return $guid
}

function write_folder_meta {
	param([string]$asset)

	$guid = guid_for $asset
	$metaPath = "$root\$asset.meta"

	$lines = @(
		"fileFormatVersion: 2",
		"guid: $guid",
		"folderAsset: yes",
		"DefaultImporter:",
		"  externalObjects: {}",
		"  userData: ",
		"  assetBundleName: ",
		"  assetBundleVariant: "
	)

	Set-Content -Path $metaPath -Value $lines -Encoding UTF8
}

function write_aar_meta {
	param([string]$asset)

	$guid = guid_for $asset
	$metaPath = "$root\$asset.meta"

	$lines = @(
		"fileFormatVersion: 2",
		"guid: $guid",
		"labels:",
		"- gpsr",
		"PluginImporter:",
		"  externalObjects: {}",
		"  serializedVersion: 3",
		"  iconMap: {}",
		"  executionOrder: {}",
		"  defineConstraints: []",
		"  isPreloaded: 0",
		"  isOverridable: 0",
		"  isExplicitlyReferenced: 0",
		"  validateReferences: 1",
		"  platformData:",
		"    Android:",
		"      enabled: 0",
		"      settings: {}",
		"    Any:",
		"      enabled: 0",
		"      settings: {}",
		"    Editor:",
		"      enabled: 0",
		"      settings:",
		"        DefaultValueInitialized: true",
		"  userData: ",
		"  assetBundleName: ",
		"  assetBundleVariant: "
	)

	Set-Content -Path $metaPath -Value $lines -Encoding UTF8
}

function write_pom_meta {
	param([string]$asset)

	$guid = guid_for $asset
	$metaPath = "$root\$asset.meta"

	$lines = @(
		"fileFormatVersion: 2",
		"guid: $guid",
		"labels:",
		"- gpsr",
		"DefaultImporter:",
		"  externalObjects: {}",
		"  userData: ",
		"  assetBundleName: ",
		"  assetBundleVariant: "
	)

	Set-Content -Path $metaPath -Value $lines -Encoding UTF8
}

function Extract-TarGz {
	param(
		[string]$TarGzPath,
		[string]$DestinationPath,
		[string]$Pattern
	)

	# Use tar if available
	if (Get-Command tar -ErrorAction SilentlyContinue) {
		& tar -xzf $TarGzPath -C $DestinationPath 2>$null
	}
	elseif (Get-Command 7z -ErrorAction SilentlyContinue) {
		& 7z x $TarGzPath -o$DestinationPath -y | Out-Null
	}
	else {
		# Fallback: use .NET
		Add-Type -AssemblyName System.IO.Compression.FileSystem
		[System.IO.Compression.ZipFile]::ExtractToDirectory($TarGzPath, $DestinationPath)
	}
}

# Check if repo is current
$current = $true
foreach ($product in $firebase_products) {
	$artifact = "firebase-$product-unity-$firebase_version"
	$built = "$repo\firebase-$product-unity\$firebase_version\$artifact.aar"

	if (-not (Test-Path $built)) {
		$current = $false
		break
	}

	$tgzPath = "$dir\com.google.firebase.$product-$firebase_version.tgz"

	try {
		Extract-TarGz -TarGzPath $tgzPath -DestinationPath $scratch -Pattern "*/$artifact.srcaar"
		$packaged = Get-ChildItem -Path $scratch -Filter "$artifact.srcaar" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1

		if (-not $packaged) {
			$current = $false
			break
		}

		# Compare files
		$file1 = [System.IO.File]::ReadAllBytes($packaged.FullName)
		$file2 = [System.IO.File]::ReadAllBytes($built)

		if (-not (@(Compare-Object $file1 $file2 -SyncWindow 0).Length -eq 0)) {
			$current = $false
			break
		}
	}
	catch {
		$current = $false
		break
	}
}

if ($current) {
	Write-Host "ok: Assets/GeneratedLocalRepo holds the pinned Firebase $firebase_version."
	Remove-Item -Path $scratch -Recurse -Force -ErrorAction SilentlyContinue
	exit 0
}

Write-Host "generating Assets/GeneratedLocalRepo/Firebase from the pinned packages"
Remove-Item -Path "$repo_root\Firebase" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path "$repo_root\Firebase.meta" -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $repo -Force | Out-Null

# Extract and copy Firebase products
foreach ($product in $firebase_products) {
	$artifact = "firebase-$product-unity-$firebase_version"
	$tgzPath = "$dir\com.google.firebase.$product-$firebase_version.tgz"

	Write-Host "extracting firebase-$product from tarball"

	Extract-TarGz -TarGzPath $tgzPath -DestinationPath $scratch

	$srcAar = Get-ChildItem -Path $scratch -Filter "$artifact.srcaar" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
	if ($srcAar) {
		$productDir = "$repo\firebase-$product-unity\$firebase_version"
		New-Item -ItemType Directory -Path $productDir -Force | Out-Null

		$destAar = "$productDir\$artifact.aar"
		Copy-Item -Path $srcAar.FullName -Destination $destAar -Force

		# Create .aar.meta
		write_aar_meta "Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-$product-unity/$firebase_version/$artifact.aar"
	}
}

# Create folder .meta files
write_folder_meta "Assets/GeneratedLocalRepo"
write_folder_meta "Assets/GeneratedLocalRepo/Firebase"
write_folder_meta "Assets/GeneratedLocalRepo/Firebase/m2repository"
write_folder_meta "Assets/GeneratedLocalRepo/Firebase/m2repository/com"
write_folder_meta "Assets/GeneratedLocalRepo/Firebase/m2repository/com/google"
write_folder_meta "Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase"

foreach ($product in $firebase_products) {
	write_folder_meta "Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-$product-unity"
	write_folder_meta "Assets/GeneratedLocalRepo/Firebase/m2repository/com/google/firebase/firebase-$product-unity/$firebase_version"
}

Write-Host "generated repo complete"

# Cleanup
Remove-Item -Path $scratch -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "done"
