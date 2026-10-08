# Builds self-contained release packages for x64 and ARM64 into .\artifacts
# Usage: pwsh ./build-release.ps1
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

[xml]$project = Get-Content WinDimmer.csproj
$version = ($project.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
$artifacts = Join-Path $PSScriptRoot 'artifacts'

if (Test-Path $artifacts) { Remove-Item $artifacts -Recurse -Force }
New-Item -ItemType Directory $artifacts | Out-Null

$targets = @(
    @{ Platform = 'x64';   Rid = 'win-x64' },
    @{ Platform = 'ARM64'; Rid = 'win-arm64' }
)

foreach ($target in $targets) {
    $publishDir = Join-Path $artifacts "publish\$($target.Rid)"
    Write-Host "Publishing $($target.Rid)..."
    dotnet publish -c Release -r $target.Rid -p:Platform=$($target.Platform) -o $publishDir
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $($target.Rid)" }

    $zip = Join-Path $artifacts "WinDimmer-$version-$($target.Rid).zip"
    # Zip a top-level WinDimmer folder so extracting doesn't scatter files.
    $staging = Join-Path $artifacts "staging\WinDimmer"
    New-Item -ItemType Directory -Force (Split-Path $staging) | Out-Null
    Copy-Item $publishDir $staging -Recurse
    Compress-Archive -Path $staging -DestinationPath $zip -CompressionLevel Optimal
    Remove-Item (Split-Path $staging) -Recurse -Force
}

Get-ChildItem $artifacts -Filter *.zip | ForEach-Object {
    $hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $($_.Name)"
} | Set-Content (Join-Path $artifacts 'SHA256SUMS.txt')

Get-ChildItem $artifacts -File | Select-Object Name, @{ n = 'MB'; e = { [math]::Round($_.Length / 1MB, 1) } }
