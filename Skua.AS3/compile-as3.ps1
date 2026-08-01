[CmdletBinding()]
param(
    [string]$MxmlcPath
)

$ErrorActionPreference = 'Stop'
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Join-Path $scriptRoot 'skua'
$sourcePath = Join-Path $projectRoot 'src'
$mainSourcePath = Join-Path $sourcePath 'skua\Main.as'
$outputDirectory = Join-Path $projectRoot 'bin'
$outputPath = Join-Path $outputDirectory 'skua.swf'

if (-not (Test-Path -LiteralPath $mainSourcePath)) {
    throw "AS3 entry point was not found: $mainSourcePath"
}

if ([string]::IsNullOrWhiteSpace($MxmlcPath)) {
    $mxmlcCommand = Get-Command mxmlc -ErrorAction SilentlyContinue
    if ($mxmlcCommand) {
        $MxmlcPath = $mxmlcCommand.Source
    }
    elseif ($env:FLEX_HOME) {
        $MxmlcPath = Join-Path $env:FLEX_HOME 'bin\mxmlc.bat'
    }
}

if ([string]::IsNullOrWhiteSpace($MxmlcPath) -or -not (Test-Path -LiteralPath $MxmlcPath)) {
    throw 'Apache Flex mxmlc was not found. Install Apache Flex SDK 4.16.1 or pass -MxmlcPath.'
}

New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

& $MxmlcPath `
    -source-path $sourcePath `
    -default-size 958 550 `
    -output $outputPath `
    $mainSourcePath `
    -target-player 28.0 `
    -optimize

if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $outputPath)) {
    throw "AS3 compilation failed: $outputPath"
}

$output = Get-Item -LiteralPath $outputPath
Write-Host "AS3 game content built: $($output.FullName) ($($output.Length) bytes)" -ForegroundColor Green