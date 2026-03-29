param(
    [string]$GodotVersion = "4.6.1",
    [string]$ReleaseStatus = "stable",
    [string]$ProductFlavor = "mono",
    [string]$InstallRoot = "",
    [string]$AppDataRoot = $env:APPDATA
)

$normalizedVersion = $GodotVersion.Trim()
$normalizedStatus = $ReleaseStatus.Trim().ToLowerInvariant()
$normalizedFlavor = $ProductFlavor.Trim().ToLowerInvariant()

if ([string]::IsNullOrWhiteSpace($normalizedVersion))
{
    throw "GodotVersion 不能为空。"
}

if ([string]::IsNullOrWhiteSpace($normalizedStatus))
{
    throw "ReleaseStatus 不能为空。"
}

if ($normalizedFlavor -ne "mono")
{
    throw "当前仅支持 mono / .NET 版 Godot 工具链。"
}

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\..\.."))
if ([string]::IsNullOrWhiteSpace($InstallRoot))
{
    $InstallRoot = Join-Path $repoRoot "artifacts\toolchains\godot"
}

if ([string]::IsNullOrWhiteSpace($AppDataRoot))
{
    throw "AppDataRoot 不能为空。"
}

$editorDirectoryName = "Godot_v{0}-{1}_{2}_win64" -f $normalizedVersion, $normalizedStatus, $normalizedFlavor
$consoleExeName = "{0}_console.exe" -f $editorDirectoryName
$guiExeName = "{0}.exe" -f $editorDirectoryName
$templateVersion = "{0}.{1}.{2}" -f $normalizedVersion, $normalizedStatus, $normalizedFlavor
$archivesRoot = Join-Path $InstallRoot "archives"
$editorArchiveFileName = "{0}.zip" -f $editorDirectoryName
$templatesArchiveFileName = "Godot_v{0}-{1}_{2}_export_templates.tpz" -f $normalizedVersion, $normalizedStatus, $normalizedFlavor
$toolchainDirectory = Join-Path $InstallRoot $editorDirectoryName
$editorArchivePath = Join-Path $archivesRoot $editorArchiveFileName
$templatesArchivePath = Join-Path $archivesRoot $templatesArchiveFileName
$exportTemplatesRoot = Join-Path $AppDataRoot "Godot\export_templates"
$templateInstallDirectory = Join-Path $exportTemplatesRoot $templateVersion
$editorDownloadUrl = "https://downloads.godotengine.org/?flavor={0}&platform=windows.64&slug=mono_win64.zip&version={1}" -f $normalizedStatus, $normalizedVersion
$templatesDownloadUrl = "https://downloads.godotengine.org/?flavor={0}&platform=templates&slug=mono_export_templates.tpz&version={1}" -f $normalizedStatus, $normalizedVersion

[pscustomobject]@{
    GodotVersion             = $normalizedVersion
    ReleaseStatus            = $normalizedStatus
    ProductFlavor            = $normalizedFlavor
    EditorDirectoryName      = $editorDirectoryName
    ConsoleExeName           = $consoleExeName
    GuiExeName               = $guiExeName
    ToolchainDirectory       = $toolchainDirectory
    ArchivesRoot             = $archivesRoot
    EditorArchivePath        = $editorArchivePath
    TemplatesArchivePath     = $templatesArchivePath
    EditorDownloadUrl        = $editorDownloadUrl
    TemplatesDownloadUrl     = $templatesDownloadUrl
    AppDataRoot              = $AppDataRoot
    ExportTemplatesRoot      = $exportTemplatesRoot
    TemplateVersion          = $templateVersion
    TemplateInstallDirectory = $templateInstallDirectory
    VersionFileName          = "version.txt"
    RequiredTemplateFiles    = @(
        "windows_release_x86_64.exe",
        "windows_debug_x86_64.exe",
        "version.txt"
    )
}
