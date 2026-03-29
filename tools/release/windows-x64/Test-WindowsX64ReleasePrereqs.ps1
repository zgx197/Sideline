param(
    [string]$GodotRoot = "D:\GodotCSharp\Godot_v4.6.1-stable_mono_win64",
    [string]$ProjectDir = "D:\work\Sideline\godot",
    [string]$ExportPresetName = "Windows Desktop",
    [string]$ExportTemplatesRoot = "$env:APPDATA\Godot\export_templates"
)

$errors = [System.Collections.Generic.List[string]]::new()
$godotExe = Join-Path $GodotRoot "Godot_v4.6.1-stable_mono_win64_console.exe"
$presetFile = Join-Path $ProjectDir "export_presets.cfg"
$mainScene = Join-Path $ProjectDir "scenes\main\Main.tscn"

if (-not (Test-Path -LiteralPath $godotExe))
{
    $errors.Add("找不到 Godot 可执行文件: $godotExe")
}

if (-not (Test-Path -LiteralPath $ProjectDir))
{
    $errors.Add("找不到 Godot 项目目录: $ProjectDir")
}

if (-not (Test-Path -LiteralPath $presetFile))
{
    $errors.Add("找不到导出预设文件: $presetFile")
}

if (-not (Test-Path -LiteralPath $mainScene))
{
    $errors.Add("找不到主场景文件: $mainScene")
}

$templateDirectories = @()
if (Test-Path -LiteralPath $ExportTemplatesRoot)
{
    $templateDirectories = Get-ChildItem -LiteralPath $ExportTemplatesRoot -Directory -ErrorAction SilentlyContinue
}

if ($templateDirectories.Count -eq 0)
{
    $errors.Add("未检测到 Godot export templates。当前目录为空: $ExportTemplatesRoot")
}

$presetMatched = $false
if (Test-Path -LiteralPath $presetFile)
{
    $presetContent = Get-Content -LiteralPath $presetFile -Encoding UTF8 -Raw
    $expectedPresetLine = 'name="{0}"' -f $ExportPresetName
    $presetMatched = $presetContent -match [regex]::Escape($expectedPresetLine)
    if (-not $presetMatched)
    {
        $errors.Add("导出预设文件中未找到预设名称: $ExportPresetName")
    }
}

[pscustomobject]@{
    GodotExe            = $godotExe
    ProjectDir          = $ProjectDir
    PresetFile          = $presetFile
    ExportPresetName    = $ExportPresetName
    ExportTemplatesRoot = $ExportTemplatesRoot
    ExportTemplateDirs  = $templateDirectories.FullName
    HasGodotExe         = (Test-Path -LiteralPath $godotExe)
    HasProjectDir       = (Test-Path -LiteralPath $ProjectDir)
    HasPresetFile       = (Test-Path -LiteralPath $presetFile)
    HasExportTemplates  = ($templateDirectories.Count -gt 0)
    HasPresetDefinition = $presetMatched
    Errors              = $errors
}
