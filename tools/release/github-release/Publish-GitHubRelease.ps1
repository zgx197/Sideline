param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$PackagePath,

    [Parameter(Mandatory = $true)]
    [string]$NotesPath,

    [string]$Repository = $env:GITHUB_REPOSITORY,

    [string]$TargetCommitish = $env:GITHUB_SHA,

    [string]$ProductName = "Sideline"
)

$ErrorActionPreference = "Stop"

if (-not (Get-Command gh -ErrorAction SilentlyContinue))
{
    throw "当前环境未安装 GitHub CLI (gh)。"
}

if ([string]::IsNullOrWhiteSpace($Repository))
{
    throw "Repository 不能为空。请提供 GITHUB_REPOSITORY 或显式传入 -Repository。"
}

if (-not (Test-Path -LiteralPath $PackagePath))
{
    throw "找不到待上传的发布包: $PackagePath"
}

if (-not (Test-Path -LiteralPath $NotesPath))
{
    throw "找不到发布说明文件: $NotesPath"
}

$metadataScript = Join-Path $PSScriptRoot "..\shared\Resolve-ReleaseMetadata.ps1"
$metadata = & $metadataScript -Version $Version -ProductName $ProductName

$releaseExists = $false
$viewOutput = & gh release view $Version --repo $Repository 2>$null
if ($LASTEXITCODE -eq 0)
{
    $releaseExists = $true
}

if (-not $releaseExists)
{
    $arguments = @(
        'release', 'create', $Version,
        $PackagePath,
        '--repo', $Repository,
        '--title', $metadata.ReleaseTitle,
        '--notes-file', $NotesPath
    )

    if (-not [string]::IsNullOrWhiteSpace($TargetCommitish))
    {
        $arguments += @('--target', $TargetCommitish)
    }

    & gh @arguments
    if ($LASTEXITCODE -ne 0)
    {
        throw "gh release create 执行失败。"
    }
}
else
{
    & gh release edit $Version --repo $Repository --title $metadata.ReleaseTitle --notes-file $NotesPath
    if ($LASTEXITCODE -ne 0)
    {
        throw "gh release edit 执行失败。"
    }

    & gh release upload $Version $PackagePath --repo $Repository --clobber
    if ($LASTEXITCODE -ne 0)
    {
        throw "gh release upload 执行失败。"
    }
}

[pscustomobject]@{
    Version     = $metadata.Version
    ReleaseTitle = $metadata.ReleaseTitle
    Repository  = $Repository
    PackagePath = $PackagePath
    ReleaseExistsBeforePublish = $releaseExists
}
