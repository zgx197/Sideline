param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$PublishDir,

    [string]$OutputRoot = "",
    [string]$RepoRoot = "",
    [string]$Platform = "win-x64",
    [string]$ProductName = "Sideline"
)

$ErrorActionPreference = "Stop"

$resolvedRepoRoot = $RepoRoot
if ([string]::IsNullOrWhiteSpace($resolvedRepoRoot))
{
    $resolvedRepoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\..\.."))
}

$resolvedOutputRoot = $OutputRoot
if ([string]::IsNullOrWhiteSpace($resolvedOutputRoot))
{
    $resolvedOutputRoot = Join-Path $resolvedRepoRoot "artifacts\release\windows-x64"
}

$metadataScript = Join-Path $resolvedRepoRoot "tools\release\shared\Resolve-ReleaseMetadata.ps1"
$metadata = & $metadataScript -Version $Version -Platform $Platform -ProductName $ProductName

if (-not (Test-Path -LiteralPath $PublishDir))
{
    throw "找不到待打包的导出目录: $PublishDir"
}

$exePath = Join-Path $PublishDir ("{0}.exe" -f $metadata.ProductName)
$pckPath = Join-Path $PublishDir ("{0}.pck" -f $metadata.ProductName)
$managedDataDir = Join-Path $PublishDir ("data_{0}_windows_x86_64" -f $metadata.ProductName)
$runtimeConfigPath = Join-Path $managedDataDir ("{0}.runtimeconfig.json" -f $metadata.ProductName)
$managedAssemblyPath = Join-Path $managedDataDir ("{0}.dll" -f $metadata.ProductName)

$requiredPaths = @($exePath, $pckPath, $managedDataDir, $runtimeConfigPath, $managedAssemblyPath)
foreach ($requiredPath in $requiredPaths)
{
    if (-not (Test-Path -LiteralPath $requiredPath))
    {
        throw "打包前校验失败，缺少必需发布文件: $requiredPath"
    }
}

New-Item -ItemType Directory -Path $resolvedOutputRoot -Force | Out-Null
$packagePath = Join-Path $resolvedOutputRoot $metadata.PackageName
if (Test-Path -LiteralPath $packagePath)
{
    Remove-Item -LiteralPath $packagePath -Force
}

$stagingDirectory = Join-Path $resolvedOutputRoot "package-staging"
if (Test-Path -LiteralPath $stagingDirectory)
{
    Remove-Item -LiteralPath $stagingDirectory -Recurse -Force
}

New-Item -ItemType Directory -Path $stagingDirectory -Force | Out-Null
Get-ChildItem -LiteralPath $PublishDir -Force |
    Where-Object { $_.Name -ne 'logs' } |
    Copy-Item -Destination $stagingDirectory -Recurse -Force

Compress-Archive -Path (Join-Path $stagingDirectory "*") -DestinationPath $packagePath -Force
if (-not (Test-Path -LiteralPath $packagePath))
{
    throw "压缩打包失败，未生成发布包: $packagePath"
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead($packagePath)
try
{
    $entries = $zip.Entries.FullName | ForEach-Object { $_.Replace('\', '/') }
    $requiredEntries = @(
        ("{0}.exe" -f $metadata.ProductName),
        ("{0}.pck" -f $metadata.ProductName),
        ("data_{0}_windows_x86_64/{0}.dll" -f $metadata.ProductName),
        ("data_{0}_windows_x86_64/{0}.runtimeconfig.json" -f $metadata.ProductName)
    )

    foreach ($requiredEntry in $requiredEntries)
    {
        if ($entries -notcontains $requiredEntry)
        {
            throw "压缩包内容校验失败，缺少条目: $requiredEntry"
        }
    }
}
finally
{
    $zip.Dispose()

    if (Test-Path -LiteralPath $stagingDirectory)
    {
        Remove-Item -LiteralPath $stagingDirectory -Recurse -Force
    }
}

[pscustomobject]@{
    Version     = $metadata.Version
    PublishDir  = $PublishDir
    PackagePath = $packagePath
}

