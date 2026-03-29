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
    [string]$BuildConfiguration = "ExportRelease",
    [int]$ExportTimeoutSeconds = 900,
    [switch]$SkipDotnetBuild
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

$metadataScript = Join-Path $resolvedRepoRoot "tools\release\shared\Resolve-ReleaseMetadata.ps1"
$prereqScript = Join-Path $resolvedRepoRoot "tools\release\windows-x64\Test-WindowsX64ReleasePrereqs.ps1"

$metadata = & $metadataScript -Version $Version
$prereqs = & $prereqScript `
    -RepoRoot $resolvedRepoRoot `
    -ProjectDir $resolvedProjectDir `
    -GodotVersion $GodotVersion `
    -ReleaseStatus $ReleaseStatus `
    -ProductFlavor $ProductFlavor `
    -InstallRoot $InstallRoot `
    -GodotRoot $GodotRoot `
    -AppDataRoot $AppDataRoot `
    -ExportPresetName $ExportPresetName

if ($prereqs.Errors.Count -gt 0)
{
    $message = ($prereqs.Errors -join [Environment]::NewLine)
    throw "发布前置检查失败:`n$message"
}

$publishDir = Join-Path $resolvedOutputRoot "publish"
$exePath = Join-Path $publishDir ("{0}.exe" -f $metadata.ProductName)
$pckPath = Join-Path $publishDir ("{0}.pck" -f $metadata.ProductName)
$managedDataDir = Join-Path $publishDir ("data_{0}_windows_x86_64" -f $metadata.ProductName)
$runtimeConfigPath = Join-Path $managedDataDir ("{0}.runtimeconfig.json" -f $metadata.ProductName)
$managedAssemblyPath = Join-Path $managedDataDir ("{0}.dll" -f $metadata.ProductName)
$projectFile = Join-Path $resolvedProjectDir "Sideline.csproj"
$godotExe = $prereqs.GodotExe
$logDir = Join-Path $resolvedOutputRoot "logs"
$exportLogPath = Join-Path $logDir ("export-{0}.log" -f $metadata.VersionWithoutPrefix)
$stdoutLogPath = Join-Path $logDir ("export-{0}.stdout.log" -f $metadata.VersionWithoutPrefix)
$stderrLogPath = Join-Path $logDir ("export-{0}.stderr.log" -f $metadata.VersionWithoutPrefix)

if (Test-Path -LiteralPath $publishDir)
{
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}

if (Test-Path -LiteralPath $logDir)
{
    Remove-Item -LiteralPath $logDir -Recurse -Force
}

New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
New-Item -ItemType Directory -Path $logDir -Force | Out-Null

if (-not $SkipDotnetBuild)
{
    dotnet build $projectFile -c $BuildConfiguration -p:RestoreIgnoreFailedSources=true -nologo
}

$env:APPDATA = $AppDataRoot
$godotArgumentString = '--headless --path "{0}" --export-release "{1}" "{2}"' -f $resolvedProjectDir, $ExportPresetName, $exePath

$process = Start-Process -FilePath $godotExe -ArgumentList $godotArgumentString -PassThru -NoNewWindow -RedirectStandardOutput $stdoutLogPath -RedirectStandardError $stderrLogPath
$exportCompletedWithLingeringProcess = $false

for ($elapsedSeconds = 0; $elapsedSeconds -lt $ExportTimeoutSeconds; $elapsedSeconds += 2)
{
    if ($process.HasExited)
    {
        break
    }

    $hasPublishOutputs = (Test-Path -LiteralPath $exePath) -and (Test-Path -LiteralPath $pckPath) -and (Test-Path -LiteralPath $managedDataDir)
    $savepackCompleted = $false
    if (Test-Path -LiteralPath $stdoutLogPath)
    {
        $savepackCompleted = Select-String -LiteralPath $stdoutLogPath -Pattern 'DONE', 'savepack' -Quiet -SimpleMatch -ErrorAction SilentlyContinue
    }

    if ($hasPublishOutputs -and $savepackCompleted)
    {
        Start-Sleep -Seconds 5
        if (-not $process.HasExited)
        {
            Stop-Process -Id $process.Id -Force
            $exportCompletedWithLingeringProcess = $true
        }

        break
    }

    Start-Sleep -Seconds 2
}

if (-not $process.HasExited)
{
    try
    {
        Stop-Process -Id $process.Id -Force
    }
    catch
    {
    }

    throw "Godot 导出超时，超过 ${ExportTimeoutSeconds}s 仍未完成。请检查日志目录: $logDir"
}

if (Test-Path -LiteralPath $stdoutLogPath)
{
    Get-Content -LiteralPath $stdoutLogPath -Encoding UTF8 | Set-Content -LiteralPath $exportLogPath -Encoding UTF8
}
else
{
    New-Item -ItemType File -Path $exportLogPath -Force | Out-Null
}

if (Test-Path -LiteralPath $stderrLogPath)
{
    Add-Content -LiteralPath $exportLogPath -Value ([Environment]::NewLine + '--- STDERR ---') -Encoding UTF8
    Get-Content -LiteralPath $stderrLogPath -Encoding UTF8 | Add-Content -LiteralPath $exportLogPath -Encoding UTF8
}


if (-not (Test-Path -LiteralPath $exePath))
{
    throw "Godot 导出命令已执行，但未生成目标可执行文件: $exePath"
}

if (-not (Test-Path -LiteralPath $pckPath))
{
    throw "Godot 导出未生成 PCK 文件: $pckPath`n请检查导出日志: $exportLogPath"
}

if (-not (Test-Path -LiteralPath $managedDataDir))
{
    throw "Godot 导出未生成 .NET 数据目录: $managedDataDir`n请检查导出日志: $exportLogPath"
}

if (-not (Test-Path -LiteralPath $runtimeConfigPath))
{
    throw "导出结果缺少 runtimeconfig 文件: $runtimeConfigPath"
}

if (-not (Test-Path -LiteralPath $managedAssemblyPath))
{
    throw "导出结果缺少主程序集: $managedAssemblyPath"
}

$exitCode = $process.ExitCode

[pscustomobject]@{
    Version                          = $metadata.Version
    PublishDir                       = $publishDir
    Executable                       = $exePath
    PckPath                          = $pckPath
    ManagedData                      = $managedDataDir
    RuntimeConfigPath                = $runtimeConfigPath
    ManagedAssembly                  = $managedAssemblyPath
    ExportLog                        = $exportLogPath
    ExportCompletedWithLingeringProcess = $exportCompletedWithLingeringProcess
}


