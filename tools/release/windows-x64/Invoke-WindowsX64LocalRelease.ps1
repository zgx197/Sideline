param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$GodotRoot = "D:\GodotCSharp\Godot_v4.6.1-stable_mono_win64",
    [string]$RepoRoot = "D:\work\Sideline",
    [string]$ProjectDir = "D:\work\Sideline\godot",
    [string]$OutputRoot = "D:\work\Sideline\artifacts\release\windows-x64",
    [switch]$SkipDotnetBuild
)

$ErrorActionPreference = "Stop"

$sharedScript = Join-Path $RepoRoot "tools\release\shared\Resolve-ReleaseMetadata.ps1"
$prereqScript = Join-Path $RepoRoot "tools\release\windows-x64\Test-WindowsX64ReleasePrereqs.ps1"

$metadata = & $sharedScript -Version $Version
$prereqs = & $prereqScript -GodotRoot $GodotRoot -ProjectDir $ProjectDir

if ($prereqs.Errors.Count -gt 0)
{
    $message = ($prereqs.Errors -join [Environment]::NewLine)
    throw "发布前置检查失败:`n$message"
}

$publishDir = Join-Path $OutputRoot "publish"
$packagePath = Join-Path $OutputRoot $metadata.PackageName
$exePath = Join-Path $publishDir ("{0}.exe" -f $metadata.ProductName)
$managedDataDir = Join-Path $publishDir ("data_{0}_windows_x86_64" -f $metadata.ProductName)
$projectFile = Join-Path $ProjectDir "Sideline.csproj"
$godotExe = $prereqs.GodotExe
$logDir = Join-Path $OutputRoot "logs"
$exportLogPath = Join-Path $logDir ("export-{0}.log" -f $metadata.VersionWithoutPrefix)

if (Test-Path -LiteralPath $publishDir)
{
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}

if (Test-Path -LiteralPath $packagePath)
{
    Remove-Item -LiteralPath $packagePath -Force
}

if (Test-Path -LiteralPath $logDir)
{
    Remove-Item -LiteralPath $logDir -Recurse -Force
}

New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
New-Item -ItemType Directory -Path $logDir -Force | Out-Null

if (-not $SkipDotnetBuild)
{
    dotnet build $projectFile -c ExportRelease -p:RestoreIgnoreFailedSources=true -nologo
}

& $godotExe --headless --path $ProjectDir --export-release "Windows Desktop" $exePath *>&1 |
    Tee-Object -FilePath $exportLogPath

if (-not (Test-Path -LiteralPath $exePath))
{
    throw "Godot 导出命令已执行，但未生成目标可执行文件: $exePath"
}

if (-not (Test-Path -LiteralPath $managedDataDir))
{
    throw "Godot 导出未生成 .NET 数据目录: $managedDataDir`n请检查导出日志: $exportLogPath"
}

Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $packagePath -Force

if (-not (Test-Path -LiteralPath $packagePath))
{
    throw "压缩打包失败，未生成发布包: $packagePath"
}

[pscustomobject]@{
    Version     = $metadata.Version
    PublishDir  = $publishDir
    Executable  = $exePath
    ManagedData = $managedDataDir
    ExportLog   = $exportLogPath
    PackagePath = $packagePath
}
