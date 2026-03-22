[CmdletBinding()]
param(
    [ValidateSet('LatestProjectLog', 'LogFile')]
    [string]$Mode = 'LatestProjectLog',

    [string]$ProjectName = 'Sideline',

    [string]$LogPath,

    [ValidateSet('Text', 'Json', 'Markdown')]
    [string]$Format = 'Text',

    [int]$Tail = 80,

    [int]$HistoryCount = 5,

    [switch]$IncludeHistory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-ProjectLogsRoot {
    param([string]$ProjectName)

    $root = Join-Path $env:APPDATA "Godot\app_userdata\$ProjectName\logs"
    if (-not (Test-Path $root)) {
        throw "Project log directory was not found: $root"
    }

    return (Resolve-Path $root).Path
}

function Get-LatestProjectLog {
    param([string]$ProjectName)

    $root = Get-ProjectLogsRoot -ProjectName $ProjectName
    $active = Join-Path $root 'godot.log'

    if (Test-Path $active) {
        $item = Get-Item $active
        if ($item.Length -gt 0) {
            return $item
        }
    }

    $latest = Get-ChildItem $root -Filter 'godot*.log' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -eq $latest) {
        throw "No Godot log files were found under: $root"
    }

    return $latest
}

function Get-HistoryItems {
    param(
        [string]$ProjectName,
        [int]$Count
    )

    $root = Get-ProjectLogsRoot -ProjectName $ProjectName
    return @(Get-ChildItem $root -Filter 'godot*.log' | Sort-Object LastWriteTime -Descending | Select-Object -First $Count)
}

function Get-TagFromLine {
    param([string]$Line)

    $match = [regex]::Match($Line, '^\[(?<tag>[^\]]+)\]')
    if ($match.Success) {
        return $match.Groups['tag'].Value
    }

    if ($Line -match '^Godot Engine ') {
        return 'Engine'
    }

    if ($Line -match '^OpenGL API ' -or $Line -match '^Vulkan ') {
        return 'Renderer'
    }

    return '<plain>'
}

function Get-LevelFromLine {
    param([string]$Line)

    if ($Line -match '^\s*ERROR:') {
        return 'error'
    }

    if ($Line -match '^\s*WARNING:' -or $Line -match '\[Warning\]') {
        return 'warning'
    }

    return 'info'
}

function Build-Report {
    param(
        [System.IO.FileInfo]$LogFile,
        [int]$Tail,
        [bool]$IncludeHistory,
        [int]$HistoryCount,
        [string]$ProjectName
    )

    $lines = [string[]](Get-Content -Path $LogFile.FullName -Encoding utf8)
    $nonEmptyLines = @($lines | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    $tailLines = if ($Tail -gt 0) { @($lines | Select-Object -Last $Tail) } else { @() }

    $tagGroups = @(
        $nonEmptyLines |
        Group-Object { Get-TagFromLine -Line $_ } |
        Sort-Object -Property @(
            @{ Expression = 'Count'; Descending = $true },
            @{ Expression = 'Name'; Descending = $false }
        )
    )

    $history = @()
    if ($IncludeHistory) {
        $history = @(
            Get-HistoryItems -ProjectName $ProjectName -Count $HistoryCount |
            ForEach-Object {
                [pscustomobject]@{
                    Name = $_.Name
                    LastWriteTime = $_.LastWriteTime
                    Length = $_.Length
                }
            }
        )
    }

    [pscustomobject]@{
        ProjectName = $ProjectName
        LogPath = $LogFile.FullName
        LastWriteTime = $LogFile.LastWriteTime
        LineCount = $lines.Count
        NonEmptyLineCount = $nonEmptyLines.Count
        ErrorCount = @($nonEmptyLines | Where-Object { (Get-LevelFromLine -Line $_) -eq 'error' }).Count
        WarningCount = @($nonEmptyLines | Where-Object { (Get-LevelFromLine -Line $_) -eq 'warning' }).Count
        Tags = @(
            $tagGroups | ForEach-Object {
                [pscustomobject]@{
                    Tag = $_.Name
                    Count = $_.Count
                }
            }
        )
        Tail = $tailLines
        History = $history
    }
}

function Format-TextReport {
    param($Report)

    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add("Project: $($Report.ProjectName)")
    $lines.Add("LogPath: $($Report.LogPath)")
    $lines.Add("LastWriteTime: $($Report.LastWriteTime)")
    $lines.Add("Lines: $($Report.LineCount)  NonEmpty: $($Report.NonEmptyLineCount)  Errors: $($Report.ErrorCount)  Warnings: $($Report.WarningCount)")
    $lines.Add('')
    $lines.Add('Tags:')
    foreach ($tag in $Report.Tags) {
        $lines.Add("- [$($tag.Count)] $($tag.Tag)")
    }

    if ($Report.History.Count -gt 0) {
        $lines.Add('')
        $lines.Add('RecentLogs:')
        foreach ($item in $Report.History) {
            $lines.Add("- $($item.Name)  $($item.LastWriteTime)  $($item.Length) bytes")
        }
    }

    $lines.Add('')
    $lines.Add('Tail:')
    foreach ($line in $Report.Tail) {
        $lines.Add($line)
    }

    return ($lines -join "`n")
}

if ($Mode -eq 'LogFile') {
    if ([string]::IsNullOrWhiteSpace($LogPath)) {
        throw 'LogFile mode requires -LogPath.'
    }

    $resolved = Get-Item (Resolve-Path $LogPath -ErrorAction Stop)
    $report = Build-Report -LogFile $resolved -Tail $Tail -IncludeHistory:$false -HistoryCount $HistoryCount -ProjectName $ProjectName
}
else {
    $logFile = Get-LatestProjectLog -ProjectName $ProjectName
    $report = Build-Report -LogFile $logFile -Tail $Tail -IncludeHistory:$IncludeHistory.IsPresent -HistoryCount $HistoryCount -ProjectName $ProjectName
}

switch ($Format) {
    'Json' {
        $report | ConvertTo-Json -Depth 8
    }
    'Markdown' {
        $text = Format-TextReport -Report $report
        @('```text', $text, '```') -join "`n"
    }
    default {
        Format-TextReport -Report $report
    }
}