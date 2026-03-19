[CmdletBinding()]
param(
    [string]$ProjectName = 'Sideline',

    [string]$LogPath,

    [string]$Category,

    [string]$SessionId,

    [ValidateSet('Trace', 'Debug', 'Info', 'Warning', 'Error')]
    [string]$MinimumLevel = 'Trace',

    [ValidateSet('Text', 'Json', 'Markdown')]
    [string]$Format = 'Text',

    [int]$Tail = 50
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$levelOrder = @{
    Trace = 0
    Debug = 1
    Info = 2
    Warning = 3
    Error = 4
}

function Get-EntryValue {
    param(
        $Entry,
        [string]$Name,
        $Default = $null
    )

    if ($null -eq $Entry) {
        return $Default
    }

    $property = $Entry.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $Default
    }

    return $property.Value
}

function Get-LevelRank {
    param($LevelValue)

    if ($null -eq $LevelValue) {
        return -1
    }

    if ($LevelValue -is [int] -or $LevelValue -is [long]) {
        return [int]$LevelValue
    }

    $text = [string]$LevelValue
    if ([string]::IsNullOrWhiteSpace($text)) {
        return -1
    }

    if ($levelOrder.ContainsKey($text)) {
        return $levelOrder[$text]
    }

    $parsedNumber = 0
    if ([int]::TryParse($text, [ref]$parsedNumber)) {
        return $parsedNumber
    }

    return -1
}

function Get-LevelName {
    param($LevelValue)

    $rank = Get-LevelRank -LevelValue $LevelValue
    foreach ($name in $levelOrder.Keys) {
        if ($levelOrder[$name] -eq $rank) {
            return $name
        }
    }

    return [string]$LevelValue
}

function Resolve-StructuredLogPath {
    param(
        [string]$ProjectName,
        [string]$LogPath
    )

    if (-not [string]::IsNullOrWhiteSpace($LogPath)) {
        return (Resolve-Path $LogPath -ErrorAction Stop).Path
    }

    $defaultPath = Join-Path $env:APPDATA "Godot\app_userdata\$ProjectName\logs\facet-structured.jsonl"
    if (-not (Test-Path $defaultPath)) {
        throw "Structured log file was not found: $defaultPath"
    }

    return (Resolve-Path $defaultPath).Path
}

$resolvedPath = Resolve-StructuredLogPath -ProjectName $ProjectName -LogPath $LogPath
$entries = @(
    Get-Content -Path $resolvedPath -Encoding utf8 |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
    ForEach-Object { $_ | ConvertFrom-Json }
)

$minLevelRank = $levelOrder[$MinimumLevel]
$filtered = @(
    $entries |
    Where-Object {
        $entrySessionId = [string](Get-EntryValue -Entry $_ -Name 'SessionId' -Default '')
        $entryCategory = [string](Get-EntryValue -Entry $_ -Name 'Category' -Default '')
        $entryLevel = Get-EntryValue -Entry $_ -Name 'Level'

        (Get-LevelRank -LevelValue $entryLevel) -ge $minLevelRank -and
        ([string]::IsNullOrWhiteSpace($Category) -or $entryCategory -like "$Category*") -and
        ([string]::IsNullOrWhiteSpace($SessionId) -or $entrySessionId -like "$SessionId*")
    }
)

$sessionGroups = @(
    $filtered |
    Group-Object { [string](Get-EntryValue -Entry $_ -Name 'SessionId' -Default '<legacy>') } |
    Sort-Object -Property @(
        @{ Expression = 'Count'; Descending = $true },
        @{ Expression = 'Name'; Descending = $false }
    )
)

$categoryGroups = @(
    $filtered |
    Group-Object { [string](Get-EntryValue -Entry $_ -Name 'Category' -Default '<unknown>') } |
    Sort-Object -Property @(
        @{ Expression = 'Count'; Descending = $true },
        @{ Expression = 'Name'; Descending = $false }
    )
)

$report = [pscustomobject]@{
    LogPath = $resolvedPath
    TotalCount = $entries.Count
    FilteredCount = $filtered.Count
    MinimumLevel = $MinimumLevel
    CategoryFilter = $Category
    SessionFilter = $SessionId
    Sessions = @(
        $sessionGroups |
        ForEach-Object {
            [pscustomobject]@{
                SessionId = $_.Name
                Count = $_.Count
            }
        }
    )
    Categories = @(
        $categoryGroups |
        ForEach-Object {
            [pscustomobject]@{
                Category = $_.Name
                Count = $_.Count
            }
        }
    )
    Tail = @($filtered | Select-Object -Last $Tail)
}

function Format-TextReport {
    param($Report)

    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add("LogPath: $($Report.LogPath)")
    $lines.Add("Total: $($Report.TotalCount)  Filtered: $($Report.FilteredCount)  MinimumLevel: $($Report.MinimumLevel)  CategoryFilter: $($Report.CategoryFilter)  SessionFilter: $($Report.SessionFilter)")
    $lines.Add('')
    $lines.Add('Sessions:')
    foreach ($session in $Report.Sessions) {
        $lines.Add("- [$($session.Count)] $($session.SessionId)")
    }

    $lines.Add('')
    $lines.Add('Categories:')
    foreach ($category in $Report.Categories) {
        $lines.Add("- [$($category.Count)] $($category.Category)")
    }

    $lines.Add('')
    $lines.Add('Tail:')
    foreach ($entry in $Report.Tail) {
        $entrySessionId = [string](Get-EntryValue -Entry $entry -Name 'SessionId' -Default '<legacy>')
        $entryEventId = [string](Get-EntryValue -Entry $entry -Name 'EventId' -Default '?')
        $entryTimestamp = [string](Get-EntryValue -Entry $entry -Name 'TimestampUtc' -Default '')
        $entryCategory = [string](Get-EntryValue -Entry $entry -Name 'Category' -Default '<unknown>')
        $entryMessage = [string](Get-EntryValue -Entry $entry -Name 'Message' -Default '')
        $entryLevel = Get-EntryValue -Entry $entry -Name 'Level'
        $entryPayload = Get-EntryValue -Entry $entry -Name 'Payload'
        $payloadText = if ($null -eq $entryPayload) { '' } else { " Payload=$($entryPayload | ConvertTo-Json -Compress -Depth 8)" }
        $lines.Add("- [S:$entrySessionId] [E:$entryEventId] [$entryTimestamp] [$(Get-LevelName -LevelValue $entryLevel)] [$entryCategory] $entryMessage$payloadText")
    }

    return ($lines -join "`n")
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