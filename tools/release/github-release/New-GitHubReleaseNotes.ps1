param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$Platform = "Windows x64",

    [string]$PackageName = "",

    [string]$ProductName = "Sideline",

    [string]$OutputPath = ""
)

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\..\.."))
$metadataScript = Join-Path $repoRoot "tools\release\shared\Resolve-ReleaseMetadata.ps1"
$metadata = & $metadataScript -Version $Version -ProductName $ProductName

$resolvedPackageName = $PackageName
if ([string]::IsNullOrWhiteSpace($resolvedPackageName))
{
    $resolvedPackageName = $metadata.PackageName
}

$lines = @(
    "# $($metadata.ReleaseTitle)",
    "",
    "- 渠道：GitHub Release",
    "- 平台：$Platform",
    "- 包名：$resolvedPackageName",
    "",
    "## 包含内容",
    "",
    "- Windows x64 可执行文件 ``Sideline.exe``",
    "- 对应的 Godot PCK 内容文件",
    "- Godot .NET 运行时数据目录",
    "",
    "## 使用方式",
    "",
    "1. 下载并解压 ``$resolvedPackageName``",
    "2. 运行 ``Sideline.exe``",
    "3. 如需排查启动问题，请优先查看可执行文件同级目录下的 ``logs/``"
)
$notes = [string]::Join([Environment]::NewLine, $lines)

if ([string]::IsNullOrWhiteSpace($OutputPath))
{
    $notes
    return
}

$notes | Set-Content -LiteralPath $OutputPath -Encoding UTF8
