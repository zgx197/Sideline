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

if (-not $normalizedVersion.StartsWith("v"))
{
    throw "版本号必须使用 v 前缀，例如 v0.1.0。"
}

$versionWithoutPrefix = $normalizedVersion.Substring(1)
if ([string]::IsNullOrWhiteSpace($versionWithoutPrefix))
{
    throw "版本号缺少 v 前缀后的主体内容。"
}

$packageName = "{0}-{1}-{2}.zip" -f $ProductName, $normalizedVersion, $Platform

[pscustomobject]@{
    Version              = $normalizedVersion
    VersionWithoutPrefix = $versionWithoutPrefix
    Platform             = $Platform
    ProductName          = $ProductName
    PackageName          = $packageName
}
