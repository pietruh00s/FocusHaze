# Builds self-contained release packages for x64 and ARM64 into .\artifacts
# Usage: pwsh ./build-release.ps1
#
# Package layout (the root holds only the launcher and the license):
#   WinDimmer\
#     WinDimmer.exe   launcher that starts app\WinDimmer.exe
#     LICENSE
#     app\            the self-contained app (.NET, Windows App SDK, resources)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

[xml]$props = Get-Content Directory.Build.props
$version = $props.Project.PropertyGroup.Version
$artifacts = Join-Path $PSScriptRoot 'artifacts'

if (Test-Path $artifacts) { Remove-Item $artifacts -Recurse -Force }
New-Item -ItemType Directory $artifacts | Out-Null

Write-Host 'Building launcher...'
$launcherDir = Join-Path $artifacts 'launcher'
dotnet build Launcher\WinDimmer.Launcher.csproj -c Release -o $launcherDir
if ($LASTEXITCODE -ne 0) { throw 'Launcher build failed' }

$targets = @(
    @{ Platform = 'x64';   Rid = 'win-x64' },
    @{ Platform = 'ARM64'; Rid = 'win-arm64' }
)

foreach ($target in $targets) {
    $publishDir = Join-Path $artifacts "publish\$($target.Rid)"
    Write-Host "Publishing $($target.Rid)..."
    dotnet publish WinDimmer.csproj -c Release -r $target.Rid -p:Platform=$($target.Platform) -o $publishDir
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $($target.Rid)" }

    # Zip a top-level WinDimmer folder so extracting doesn't scatter files.
    $package = Join-Path $artifacts "package\$($target.Rid)\WinDimmer"
    New-Item -ItemType Directory -Force $package | Out-Null
    Copy-Item $publishDir (Join-Path $package 'app') -Recurse
    Copy-Item (Join-Path $launcherDir 'WinDimmer.exe') $package
    Copy-Item LICENSE $package

    $zip = Join-Path $artifacts "WinDimmer-$version-$($target.Rid).zip"
    Compress-Archive -Path $package -DestinationPath $zip -CompressionLevel Optimal
}

Get-ChildItem $artifacts -Filter *.zip | ForEach-Object {
    $hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $($_.Name)"
} | Set-Content (Join-Path $artifacts 'SHA256SUMS.txt')

Get-ChildItem $artifacts -File | Select-Object Name, @{ n = 'MB'; e = { [math]::Round($_.Length / 1MB, 1) } }
