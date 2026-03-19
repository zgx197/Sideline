[CmdletBinding()]
param(
    [ValidateSet('Auto', 'Build', 'LogFile')]
    [string]$Mode = 'Auto',

    [string]$ProjectPath = 'godot/Sideline.csproj',

    [string]$LogPath,

    [ValidateSet('Text', 'Json', 'Markdown')]
    [string]$Format = 'Text',

    [int]$MaxItemsPerFile = 10,

    [switch]$IncludeWarnings
)

$scriptPath = Join-Path $PSScriptRoot 'codex-skills/godot-problems-inspector/scripts/get_godot_problems.ps1'

& $scriptPath `
    -Mode $Mode `
    -ProjectPath $ProjectPath `
    -LogPath $LogPath `
    -Format $Format `
    -MaxItemsPerFile $MaxItemsPerFile `
    -IncludeWarnings:$IncludeWarnings
