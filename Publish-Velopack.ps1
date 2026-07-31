# Velopack workflow: build the app, stage the files, then run `vpk pack`.
# See https://docs.velopack.io/packaging/overview and
# https://docs.velopack.io/reference/cli for the documented workflow/options.
# Skua-specific values below intentionally use this repo's package identity,
# Avalonia output paths, icon, and manager entry point.

[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [ValidateSet('win-x64', 'win-x86')]
    [string]$Runtime = 'win-x64',
    [ValidateSet('win', 'nightly')]
    [string]$Channel = 'win',
    [string]$Version
)

$ErrorActionPreference = 'Continue'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

$velopackVersion = '1.2.0'
$versionFile = [xml](Get-Content -LiteralPath 'Directory.Build.props')
$versionValue = if ($Version) { $Version } else { [string]$versionFile.Project.PropertyGroup.Version }
$versionMatch = [regex]::Match($versionValue, '^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)(?:\.\d+)?(?<suffix>[-+].*)?$')
if (-not $versionMatch.Success) { throw "Version '$versionValue' is not a supported SemVer value." }
$packVersion = '{0}.{1}.{2}{3}' -f $versionMatch.Groups['major'].Value, $versionMatch.Groups['minor'].Value, $versionMatch.Groups['patch'].Value, $versionMatch.Groups['suffix'].Value

$artifactRoot = Join-Path $root 'artifacts\velopack'
$publishRoot = Join-Path $artifactRoot 'publish'
$releaseRoot = Join-Path $artifactRoot 'releases'
$appOutput = Join-Path $root ("Skua.App.Avalonia\bin\{0}\net10.0-windows" -f $Configuration)
$managerOutput = Join-Path $root ("Skua.Manager.Avalonia\bin\{0}\net10.0" -f $Configuration)

dotnet restore '.\Skua.sln' -m --nologo
if ($LASTEXITCODE -ne 0) { throw 'Solution restore failed.' }

dotnet build '.\Skua.sln' -c $Configuration -m --no-restore --nologo -p:Version=$packVersion -p:AssemblyInformationalVersion=$packVersion
if ($LASTEXITCODE -ne 0) { throw 'Solution build failed.' }

if (Test-Path -LiteralPath $artifactRoot) {
    Remove-Item -LiteralPath $artifactRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $publishRoot, $releaseRoot -Force | Out-Null

if (-not (Test-Path -LiteralPath $appOutput)) { throw "Avalonia app output was not found: $appOutput" }
if (-not (Test-Path -LiteralPath $managerOutput)) { throw "Avalonia manager output was not found: $managerOutput" }

Copy-Item -Path (Join-Path $appOutput '*') -Destination $publishRoot -Recurse -Force
Copy-Item -Path (Join-Path $managerOutput '*') -Destination $publishRoot -Recurse -Force

$framework = if ($Runtime -eq 'win-x86') { 'net10.0-x86-desktop' } else { 'net10.0-x64-desktop' }

$vpkArguments = @(
    'pack',
    '--packId', 'Skua',
    '--packVersion', $packVersion,
    '--channel', $Channel,
    '--packDir', $publishRoot,
    '--mainExe', 'Skua.Manager.exe',
    '--outputDir', $releaseRoot,
    '--packTitle', 'Skua Manager',
    '--icon', '.\Skua.Shared.Avalonia\Assets\SkuaIcon.ico',
    '--shortcuts', 'Desktop,StartMenuRoot',
    '--framework', $framework
)

if (-not (Get-Command vpk -ErrorAction SilentlyContinue)) {
    throw ('Velopack CLI vpk {0} is required. Install it with: dotnet tool install --global vpk --version {0}' -f $velopackVersion)
}
& vpk @vpkArguments
if ($LASTEXITCODE -ne 0) { throw 'Velopack packaging failed.' }

Write-Host ('Velopack release created: {0}' -f (Resolve-Path $releaseRoot)) -ForegroundColor Green
