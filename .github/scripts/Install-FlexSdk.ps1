[CmdletBinding()]
param(
    [string]$SdkRoot
)

$ErrorActionPreference = 'Stop'

$flexVersion = '4.16.1'
$archiveUrl = "https://dlcdn.apache.org/flex/$flexVersion/binaries/apache-flex-sdk-$flexVersion-bin.zip"
$md5Url = "https://downloads.apache.org/flex/$flexVersion/binaries/apache-flex-sdk-$flexVersion-bin.zip.md5"
$tempRoot = if ($env:RUNNER_TEMP) { $env:RUNNER_TEMP } else { [IO.Path]::GetTempPath() }
$SdkRoot = if ($SdkRoot) { $SdkRoot } else { Join-Path $tempRoot "apache-flex-sdk-$flexVersion" }
$sdkArchive = Join-Path $tempRoot "apache-flex-sdk-$flexVersion-bin.zip"
$sdkHash = Join-Path $tempRoot "apache-flex-sdk-$flexVersion-bin.zip.md5"

function Find-SdkDirectory([string]$Root) {
    if (Test-Path -LiteralPath (Join-Path $Root 'bin\mxmlc.bat')) {
        return Get-Item -LiteralPath $Root
    }

    return Get-ChildItem -LiteralPath $Root -Directory | Where-Object {
        Test-Path -LiteralPath (Join-Path $_.FullName 'bin\mxmlc.bat')
    } | Select-Object -First 1
}

$sdkDirectory = if (Test-Path -LiteralPath $SdkRoot) { Find-SdkDirectory $SdkRoot } else { $null }
if (-not $sdkDirectory) {
    Invoke-WebRequest -Uri $archiveUrl -OutFile $sdkArchive
    Invoke-WebRequest -Uri $md5Url -OutFile $sdkHash

    $expectedHash = [regex]::Match((Get-Content -Raw $sdkHash), '[0-9a-fA-F]{32}').Value.ToUpperInvariant()
    $actualHash = (Get-FileHash -LiteralPath $sdkArchive -Algorithm MD5).Hash.ToUpperInvariant()
    if (-not $expectedHash -or $expectedHash -ne $actualHash) {
        throw 'Apache Flex SDK checksum validation failed.'
    }

    if (Test-Path -LiteralPath $SdkRoot) {
        Remove-Item -LiteralPath $SdkRoot -Recurse -Force
    }
    Expand-Archive -LiteralPath $sdkArchive -DestinationPath $SdkRoot
    $sdkDirectory = Find-SdkDirectory $SdkRoot
}

if (-not $sdkDirectory) {
    throw 'Apache Flex SDK archive did not contain mxmlc.bat.'
}

$playerGlobalDirectory = Join-Path $sdkDirectory.FullName 'frameworks\libs\player\28.0'
$playerGlobalPath = Join-Path $playerGlobalDirectory 'playerglobal.swc'
$playerGlobalSource = Join-Path $PSScriptRoot '..\..\Skua.AS3\playerglobal\28.0\playerglobal.swc'
$expectedPlayerGlobalHash = '19AD5364EC5AC9FF57E16A5CD3D65B0CCCE5D42E8C27FC28B9907FE81688BDE4'

New-Item -ItemType Directory -Path $playerGlobalDirectory -Force | Out-Null
Copy-Item -LiteralPath $playerGlobalSource -Destination $playerGlobalPath -Force
$actualPlayerGlobalHash = (Get-FileHash -LiteralPath $playerGlobalPath -Algorithm SHA256).Hash
if ($actualPlayerGlobalHash -ne $expectedPlayerGlobalHash) {
    throw 'PlayerGlobal 28 checksum validation failed.'
}

$playerGlobalHome = Split-Path -Parent $playerGlobalDirectory
$env:FLEX_HOME = $sdkDirectory.FullName
$env:PLAYERGLOBAL_HOME = $playerGlobalHome

if ($env:GITHUB_ENV) {
    "FLEX_HOME=$($env:FLEX_HOME)" >> $env:GITHUB_ENV
    "PLAYERGLOBAL_HOME=$($env:PLAYERGLOBAL_HOME)" >> $env:GITHUB_ENV
}

Write-Host "Apache Flex SDK ready: $($sdkDirectory.FullName)" -ForegroundColor Green