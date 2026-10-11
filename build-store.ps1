# Builds the Microsoft Store upload: one .msixbundle (x64 + ARM64) in .\artifacts\store
# Usage:
#   pwsh ./build-store.ps1
#   (optional overrides) -IdentityName <Package/Identity/Name> -Publisher <Package/Identity/Publisher> `
#                          [-PublisherDisplayName <Package/Properties/PublisherDisplayName>]
# The three values are shown in Partner Center under Product management › Product identity.
# The bundle is unsigned: the Store signs it during certification.
param(
    # Defaults are FocusHaze's Partner Center identity (Store ID 9P3J7F0RK3RT).
    [string]$IdentityName = 'PiotrOsika.FocusHaze',
    [string]$Publisher = 'CN=AA3B8BEC-E366-47D0-AC85-FF7587FD30FC',
    [string]$PublisherDisplayName = 'Piotr Osika'
)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

[xml]$props = Get-Content Directory.Build.props
$version = "$($props.Project.PropertyGroup.Version).0"   # Store versions have four parts; the last must be 0
$storeDir = Join-Path $PSScriptRoot 'artifacts\store'
$manifestPath = Join-Path $PSScriptRoot 'Package.appxmanifest'
$originalManifest = [IO.File]::ReadAllText($manifestPath)

$makeappx = Get-ChildItem "$env:USERPROFILE\.nuget\packages\microsoft.windows.sdk.buildtools" -Recurse -Filter makeappx.exe |
    Where-Object { $_.FullName -match '\\x64\\' } | Sort-Object FullName -Descending | Select-Object -First 1
if (-not $makeappx) { throw 'makeappx.exe not found (restore the project first: dotnet restore)' }

if (Test-Path $storeDir) { Remove-Item $storeDir -Recurse -Force }
New-Item -ItemType Directory $storeDir | Out-Null

try {
    # Fill in the Store identity and version for this build only.
    [xml]$manifest = $originalManifest
    $manifest.Package.Identity.Name = $IdentityName
    $manifest.Package.Identity.Publisher = $Publisher
    $manifest.Package.Identity.Version = $version
    $manifest.Package.Properties.PublisherDisplayName = $PublisherDisplayName
    $manifest.Save($manifestPath)

    $bundleInput = Join-Path $storeDir 'bundle-input'
    New-Item -ItemType Directory $bundleInput | Out-Null
    foreach ($target in @(@{ Platform = 'x64'; Rid = 'win-x64' }, @{ Platform = 'ARM64'; Rid = 'win-arm64' })) {
        Write-Host "Building MSIX for $($target.Rid)..."
        dotnet publish FocusHaze.csproj -c Release -r $target.Rid -p:Platform=$($target.Platform) -p:StorePackage=true
        if ($LASTEXITCODE -ne 0) { throw "MSIX build failed for $($target.Rid)" }
    }
    Get-ChildItem (Join-Path $storeDir 'packages') -Recurse -Filter *.msix |
        Where-Object { $_.Name -notmatch 'WindowsAppRuntime|Dependencies' -and $_.Directory.Name -notmatch 'Dependencies' } |
        ForEach-Object { Copy-Item $_.FullName $bundleInput }

    $bundle = Join-Path $storeDir "FocusHaze_$version.msixbundle"
    & $makeappx.FullName bundle /d $bundleInput /p $bundle /bv $version /o | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'makeappx bundle failed' }
    Remove-Item $bundleInput -Recurse -Force

    Get-Item $bundle | Select-Object Name, @{ n = 'MB'; e = { [math]::Round($_.Length / 1MB, 1) } }
}
finally {
    [IO.File]::WriteAllText($manifestPath, $originalManifest, [Text.UTF8Encoding]::new($false))
}
