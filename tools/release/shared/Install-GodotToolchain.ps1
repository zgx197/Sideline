param(
    [string]$GodotVersion = "4.6.1",
    [string]$ReleaseStatus = "stable",
    [string]$ProductFlavor = "mono",
    [string]$InstallRoot = "",
    [string]$AppDataRoot = $env:APPDATA,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$resolveScript = Join-Path $PSScriptRoot "Resolve-GodotToolchainConfig.ps1"
$config = & $resolveScript `
    -GodotVersion $GodotVersion `
    -ReleaseStatus $ReleaseStatus `
    -ProductFlavor $ProductFlavor `
    -InstallRoot $InstallRoot `
    -AppDataRoot $AppDataRoot

function Copy-DirectoryContents
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceDirectory,

        [Parameter(Mandatory = $true)]
        [string]$DestinationDirectory
    )

    New-Item -ItemType Directory -Path $DestinationDirectory -Force | Out-Null
    Get-ChildItem -LiteralPath $SourceDirectory -Force | Copy-Item -Destination $DestinationDirectory -Recurse -Force
}

function Find-ArchiveContentRoot
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$ExpandedDirectory,

        [Parameter(Mandatory = $true)]
        [string]$ProbeFileName
    )

    $probeFile = Get-ChildItem -LiteralPath $ExpandedDirectory -Recurse -File -Filter $ProbeFileName |
        Select-Object -First 1
    if ($null -eq $probeFile)
    {
        throw "解压结果中未找到探测文件: $ProbeFileName"
    }

    return $probeFile.DirectoryName
}

function Expand-ArchiveIntoDirectory
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$ArchivePath,

        [Parameter(Mandatory = $true)]
        [string]$ProbeFileName,

        [Parameter(Mandatory = $true)]
        [string]$DestinationDirectory
    )

    $stagingDirectory = Join-Path ([System.IO.Path]::GetDirectoryName($DestinationDirectory)) ([System.IO.Path]::GetRandomFileName())
    $temporaryZipPath = $null

    try
    {
        New-Item -ItemType Directory -Path $stagingDirectory -Force | Out-Null

        $archiveForExpand = $ArchivePath
        if ([System.IO.Path]::GetExtension($ArchivePath).Equals(".tpz", [System.StringComparison]::OrdinalIgnoreCase))
        {
            $temporaryZipPath = "{0}.zip" -f $ArchivePath
            Copy-Item -LiteralPath $ArchivePath -Destination $temporaryZipPath -Force
            $archiveForExpand = $temporaryZipPath
        }

        Expand-Archive -LiteralPath $archiveForExpand -DestinationPath $stagingDirectory -Force
        $contentRoot = Find-ArchiveContentRoot -ExpandedDirectory $stagingDirectory -ProbeFileName $ProbeFileName

        if (Test-Path -LiteralPath $DestinationDirectory)
        {
            Remove-Item -LiteralPath $DestinationDirectory -Recurse -Force
        }

        Copy-DirectoryContents -SourceDirectory $contentRoot -DestinationDirectory $DestinationDirectory
    }
    finally
    {
        if ($null -ne $temporaryZipPath -and (Test-Path -LiteralPath $temporaryZipPath))
        {
            Remove-Item -LiteralPath $temporaryZipPath -Force
        }

        if (Test-Path -LiteralPath $stagingDirectory)
        {
            Remove-Item -LiteralPath $stagingDirectory -Recurse -Force
        }
    }
}

function Test-InstalledEditor
{
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$ToolchainConfig
    )

    $consoleExePath = Join-Path $ToolchainConfig.ToolchainDirectory $ToolchainConfig.ConsoleExeName
    $guiExePath = Join-Path $ToolchainConfig.ToolchainDirectory $ToolchainConfig.GuiExeName
    return (Test-Path -LiteralPath $consoleExePath) -and (Test-Path -LiteralPath $guiExePath)
}

function Test-InstalledTemplates
{
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$ToolchainConfig
    )

    $versionFilePath = Join-Path $ToolchainConfig.TemplateInstallDirectory $ToolchainConfig.VersionFileName
    if (-not (Test-Path -LiteralPath $versionFilePath))
    {
        return $false
    }

    $installedVersion = (Get-Content -LiteralPath $versionFilePath -Encoding UTF8 | Select-Object -First 1).Trim()
    if ($installedVersion -ne $ToolchainConfig.TemplateVersion)
    {
        return $false
    }

    foreach ($requiredFile in $ToolchainConfig.RequiredTemplateFiles)
    {
        $requiredPath = Join-Path $ToolchainConfig.TemplateInstallDirectory $requiredFile
        if (-not (Test-Path -LiteralPath $requiredPath))
        {
            return $false
        }
    }

    return $true
}

New-Item -ItemType Directory -Path $config.ArchivesRoot -Force | Out-Null
New-Item -ItemType Directory -Path $config.ExportTemplatesRoot -Force | Out-Null

$downloadedEditor = $false
$downloadedTemplates = $false
$installedEditor = $false
$installedTemplates = $false

$editorAlreadyReady = Test-InstalledEditor -ToolchainConfig $config
if ($Force -or -not $editorAlreadyReady)
{
    Invoke-WebRequest -Uri $config.EditorDownloadUrl -OutFile $config.EditorArchivePath
    $downloadedEditor = $true
    Expand-ArchiveIntoDirectory -ArchivePath $config.EditorArchivePath -ProbeFileName $config.ConsoleExeName -DestinationDirectory $config.ToolchainDirectory
    $installedEditor = $true
}

$templatesAlreadyReady = Test-InstalledTemplates -ToolchainConfig $config
if ($Force -or -not $templatesAlreadyReady)
{
    Invoke-WebRequest -Uri $config.TemplatesDownloadUrl -OutFile $config.TemplatesArchivePath
    $downloadedTemplates = $true
    Expand-ArchiveIntoDirectory -ArchivePath $config.TemplatesArchivePath -ProbeFileName $config.VersionFileName -DestinationDirectory $config.TemplateInstallDirectory
    $installedTemplates = $true
}

if (-not (Test-InstalledEditor -ToolchainConfig $config))
{
    throw "Godot 工具链安装结果无效，未检测到可执行文件: $($config.ToolchainDirectory)"
}

if (-not (Test-InstalledTemplates -ToolchainConfig $config))
{
    throw "Godot export templates 安装结果无效，未检测到正确版本模板: $($config.TemplateInstallDirectory)"
}

[pscustomobject]@{
    GodotVersion        = $config.GodotVersion
    ReleaseStatus       = $config.ReleaseStatus
    ProductFlavor       = $config.ProductFlavor
    ToolchainDirectory  = $config.ToolchainDirectory
    GodotConsoleExePath = (Join-Path $config.ToolchainDirectory $config.ConsoleExeName)
    GodotGuiExePath     = (Join-Path $config.ToolchainDirectory $config.GuiExeName)
    TemplateVersion     = $config.TemplateVersion
    TemplatesDirectory  = $config.TemplateInstallDirectory
    DownloadedEditor    = $downloadedEditor
    DownloadedTemplates = $downloadedTemplates
    InstalledEditor     = $installedEditor
    InstalledTemplates  = $installedTemplates
}
