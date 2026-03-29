param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$RepoRoot = "",
    [string]$ProjectDir = "",
    [string]$OutputRoot = "",
    [string]$GodotVersion = "4.6.1",
    [string]$ReleaseStatus = "stable",
    [string]$ProductFlavor = "mono",
    [string]$InstallRoot = "",
    [string]$GodotRoot = "",
    [string]$AppDataRoot = $env:APPDATA,
    [string]$ExportPresetName = "Windows Desktop",
    [int]$SmokeTestStartupTimeoutSeconds = 30,
    [switch]$SkipDotnetBuild,
    [switch]$SkipSmokeTest
)

$ErrorActionPreference = "Stop"

$resolvedRepoRoot = $RepoRoot
if ([string]::IsNullOrWhiteSpace($resolvedRepoRoot))
{
    $resolvedRepoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\..\.."))
}

$resolvedProjectDir = $ProjectDir
if ([string]::IsNullOrWhiteSpace($resolvedProjectDir))
{
    $resolvedProjectDir = Join-Path $resolvedRepoRoot "godot"
}

$resolvedOutputRoot = $OutputRoot
if ([string]::IsNullOrWhiteSpace($resolvedOutputRoot))
{
    $resolvedOutputRoot = Join-Path $resolvedRepoRoot "artifacts\release\windows-x64"
}

$exportScript = Join-Path $resolvedRepoRoot "tools\release\windows-x64\Export-WindowsX64Release.ps1"
$smokeScript = Join-Path $resolvedRepoRoot "tools\release\windows-x64\Test-WindowsX64ReleaseSmoke.ps1"
$packageScript = Join-Path $resolvedRepoRoot "tools\release\windows-x64\Package-WindowsX64Release.ps1"

$exportResult = & $exportScript `
    -Version $Version `
    -RepoRoot $resolvedRepoRoot `
    -ProjectDir $resolvedProjectDir `
    -OutputRoot $resolvedOutputRoot `
    -GodotVersion $GodotVersion `
    -ReleaseStatus $ReleaseStatus `
    -ProductFlavor $ProductFlavor `
    -InstallRoot $InstallRoot `
    -GodotRoot $GodotRoot `
    -AppDataRoot $AppDataRoot `
    -ExportPresetName $ExportPresetName `
    -SkipDotnetBuild:$SkipDotnetBuild

$smokeResult = $null
if (-not $SkipSmokeTest)
{
    $smokeResult = & $smokeScript `
        -PublishDir $exportResult.PublishDir `
        -StartupTimeoutSeconds $SmokeTestStartupTimeoutSeconds
}

$packageResult = & $packageScript `
    -Version $Version `
    -PublishDir $exportResult.PublishDir `
    -OutputRoot $resolvedOutputRoot `
    -RepoRoot $resolvedRepoRoot

[pscustomobject]@{
    Version            = $exportResult.Version
    PublishDir         = $exportResult.PublishDir
    Executable         = $exportResult.Executable
    PckPath            = $exportResult.PckPath
    ManagedData        = $exportResult.ManagedData
    RuntimeConfigPath  = $exportResult.RuntimeConfigPath
    ManagedAssembly    = $exportResult.ManagedAssembly
    ExportLog          = $exportResult.ExportLog
    SmokeTestPassed    = if ($null -ne $smokeResult) { $smokeResult.SmokeTestPassed } else { $false }
    SmokeConsoleLog    = if ($null -ne $smokeResult) { $smokeResult.ConsoleLogPath } else { $null }
    SmokeStructuredLog = if ($null -ne $smokeResult) { $smokeResult.StructuredLogPath } else { $null }
    PackagePath        = $packageResult.PackagePath
}
