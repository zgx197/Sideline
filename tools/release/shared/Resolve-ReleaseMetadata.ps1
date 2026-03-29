param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$Platform = "win-x64",

    [string]$ProductName = "Sideline"
)

$normalizedVersion = $Version.Trim()
if ([string]::IsNullOrWhiteSpace($normalizedVersion))
{
    throw "版本号不能为空。"
}

if ($normalizedVersion -notmatch '^v\d+\.\d+\.\d+([\-+][0-9A-Za-z\.\-]+)?$')
{
    throw "版本号必须符合 vX.Y.Z 语义版本格式，例如 v0.1.0 或 v0.2.0-alpha.1。"
}

$versionWithoutPrefix = $normalizedVersion.Substring(1)
if ([string]::IsNullOrWhiteSpace($versionWithoutPrefix))
{
    throw "版本号缺少 v 前缀后的主体内容。"
}

$packageName = "{0}-{1}-{2}.zip" -f $ProductName, $normalizedVersion, $Platform
$releaseTitle = "{0} {1}" -f $ProductName, $normalizedVersion
$artifactExportName = "{0}-{1}-export" -f $ProductName.ToLowerInvariant(), $Platform
$artifactPackageName = "{0}-{1}-package" -f $ProductName.ToLowerInvariant(), $Platform

[pscustomobject]@{
    Version              = $normalizedVersion
    VersionWithoutPrefix = $versionWithoutPrefix
    Platform             = $Platform
    ProductName          = $ProductName
    PackageName          = $packageName
    ReleaseTitle         = $releaseTitle
    ArtifactExportName   = $artifactExportName
    ArtifactPackageName  = $artifactPackageName
}
