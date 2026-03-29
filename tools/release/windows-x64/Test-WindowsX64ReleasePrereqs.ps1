param(
    [string]$RepoRoot = "",
    [string]$ProjectDir = "",
    [string]$GodotVersion = "4.6.1",
    [string]$ReleaseStatus = "stable",
    [string]$ProductFlavor = "mono",
    [string]$InstallRoot = "",
    [string]$GodotRoot = "",
    [string]$AppDataRoot = $env:APPDATA,
    [string]$ExportPresetName = "Windows Desktop",
    [string]$ExportTemplatesRoot = ""
)

$resolveToolchainScript = Join-Path $PSScriptRoot "..\shared\Resolve-GodotToolchainConfig.ps1"

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

$toolchainConfig = & $resolveToolchainScript `
    -GodotVersion $GodotVersion `
    -ReleaseStatus $ReleaseStatus `
    -ProductFlavor $ProductFlavor `
    -InstallRoot $InstallRoot `
    -AppDataRoot $AppDataRoot

$resolvedGodotRoot = $GodotRoot
if ([string]::IsNullOrWhiteSpace($resolvedGodotRoot))
{
    $resolvedGodotRoot = $toolchainConfig.ToolchainDirectory
}

$resolvedExportTemplatesRoot = $ExportTemplatesRoot
if ([string]::IsNullOrWhiteSpace($resolvedExportTemplatesRoot))
{
    $resolvedExportTemplatesRoot = $toolchainConfig.ExportTemplatesRoot
}

$errors = [System.Collections.Generic.List[string]]::new()
$godotExe = Join-Path $resolvedGodotRoot $toolchainConfig.ConsoleExeName
$presetFile = Join-Path $resolvedProjectDir "export_presets.cfg"
$mainScene = Join-Path $resolvedProjectDir "scenes\main\Main.tscn"
$templateDirectory = Join-Path $resolvedExportTemplatesRoot $toolchainConfig.TemplateVersion
$templateVersionFile = Join-Path $templateDirectory $toolchainConfig.VersionFileName

if (-not (Test-Path -LiteralPath $godotExe))
{
    $errors.Add("找不到 Godot 可执行文件: $godotExe")
}

if (-not (Test-Path -LiteralPath $resolvedProjectDir))
{
    $errors.Add("找不到 Godot 项目目录: $resolvedProjectDir")
}

if (-not (Test-Path -LiteralPath $presetFile))
{
    $errors.Add("找不到导出预设文件: $presetFile")
}

if (-not (Test-Path -LiteralPath $mainScene))
{
    $errors.Add("找不到主场景文件: $mainScene")
}

if (-not (Test-Path -LiteralPath $resolvedExportTemplatesRoot))
{
    $errors.Add("找不到 Godot export templates 根目录: $resolvedExportTemplatesRoot")
}

if (-not (Test-Path -LiteralPath $templateDirectory))
{
    $errors.Add("缺少目标版本 export templates 目录: $templateDirectory")
}

if (-not (Test-Path -LiteralPath $templateVersionFile))
{
    $errors.Add("缺少 export templates 版本文件: $templateVersionFile")
}
else
{
    $installedTemplateVersion = (Get-Content -LiteralPath $templateVersionFile -Encoding UTF8 | Select-Object -First 1).Trim()
    if ($installedTemplateVersion -ne $toolchainConfig.TemplateVersion)
    {
        $errors.Add("export templates 版本不匹配。期望: $($toolchainConfig.TemplateVersion)，实际: $installedTemplateVersion")
    }
}

foreach ($requiredTemplateFile in $toolchainConfig.RequiredTemplateFiles)
{
    $requiredTemplatePath = Join-Path $templateDirectory $requiredTemplateFile
    if (-not (Test-Path -LiteralPath $requiredTemplatePath))
    {
        $errors.Add("缺少必需的 export template 文件: $requiredTemplatePath")
    }
}

$presetMatched = $false
$architectureMatched = $false
$luaIncludeMatched = $false
if (Test-Path -LiteralPath $presetFile)
{
    $presetContent = Get-Content -LiteralPath $presetFile -Encoding UTF8 -Raw
    $expectedPresetLine = 'name="{0}"' -f $ExportPresetName
    $presetMatched = $presetContent -match [regex]::Escape($expectedPresetLine)
    if (-not $presetMatched)
    {
        $errors.Add("导出预设文件中未找到预设名称: $ExportPresetName")
    }

    $architectureMatched = $presetContent -match [regex]::Escape('binary_format/architecture="x86_64"')
    if (-not $architectureMatched)
    {
        $errors.Add("导出预设未显式声明 Windows x64 架构。")
    }

    $luaIncludeMatched = $presetContent -match [regex]::Escape('include_filter="scripts/facet/LuaScripts/*.lua"')
    if (-not $luaIncludeMatched)
    {
        $errors.Add("导出预设未显式纳入 Facet Lua 脚本资源。")
    }
}

[pscustomobject]@{
    GodotExe              = $godotExe
    ProjectDir            = $resolvedProjectDir
    PresetFile            = $presetFile
    ExportPresetName      = $ExportPresetName
    ExportTemplatesRoot   = $resolvedExportTemplatesRoot
    ExportTemplateDir     = $templateDirectory
    ExpectedTemplateVersion = $toolchainConfig.TemplateVersion
    HasGodotExe           = (Test-Path -LiteralPath $godotExe)
    HasProjectDir         = (Test-Path -LiteralPath $resolvedProjectDir)
    HasPresetFile         = (Test-Path -LiteralPath $presetFile)
    HasExportTemplates    = (Test-Path -LiteralPath $templateDirectory)
    HasPresetDefinition   = $presetMatched
    HasX64Architecture    = $architectureMatched
    HasLuaIncludeFilter   = $luaIncludeMatched
    Errors                = $errors
}
