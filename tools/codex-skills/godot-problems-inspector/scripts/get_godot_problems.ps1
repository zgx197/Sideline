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

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function New-Diagnostic {
    param(
        [string]$File,
        [Nullable[int]]$Line,
        [Nullable[int]]$Column,
        [string]$Level,
        [string]$Code,
        [string]$Message,
        [string]$Project,
        [string]$Source
    )

    [pscustomobject]@{
        File = if ([string]::IsNullOrWhiteSpace($File)) { '<project>' } else { $File }
        Line = $Line
        Column = $Column
        Level = $Level
        Code = $Code
        Message = $Message.Trim()
        Project = $Project
        Source = $Source
    }
}

function Parse-BuildOutputLine {
    param(
        [string]$Line,
        [string]$Source
    )

    $patterns = @(
        '^(?<file>[A-Za-z]:\\.*?|[^:]+?)\((?<line>\d+),(?<column>\d+)\):\s*(?<level>error|warning)\s*(?<code>[A-Z]{2,}\d+)?\s*:\s*(?<message>.+?)(?:\s*\[(?<project>.+)\])?$',
        '^(?<file>[A-Za-z]:\\.*?|[^:]+?)\s*:\s*(?<level>error|warning)\s*(?<code>[A-Z]{2,}\d+)?\s*:\s*(?<message>.+)$',
        '^(?<level>ERROR|WARNING):\s*(?<message>.+)$'
    )

    foreach ($pattern in $patterns) {
        $match = [regex]::Match($Line, $pattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
        if ($match.Success) {
            $lineValue = $null
            $columnValue = $null

            if ($match.Groups['line'].Success) {
                $lineValue = [int]$match.Groups['line'].Value
            }

            if ($match.Groups['column'].Success) {
                $columnValue = [int]$match.Groups['column'].Value
            }

            return New-Diagnostic `
                -File $match.Groups['file'].Value `
                -Line $lineValue `
                -Column $columnValue `
                -Level $match.Groups['level'].Value.ToLowerInvariant() `
                -Code $match.Groups['code'].Value `
                -Message $match.Groups['message'].Value `
                -Project $match.Groups['project'].Value `
                -Source $Source
        }
    }

    return $null
}

function Get-BuildLines {
    param([string]$ProjectPath)

    $projectFullPath = Resolve-Path $ProjectPath -ErrorAction Stop
    $tempFile = [System.IO.Path]::GetTempFileName()

    try {
        $arguments = @(
            'build'
            $projectFullPath.Path
            '-nologo'
            '-v:minimal'
            '/clp:Summary;ForceNoAlign'
        )

        $output = & dotnet @arguments 2>&1
        $output | Set-Content -Path $tempFile -Encoding utf8

        [pscustomobject]@{
            Source = 'dotnet-build'
            ExitCode = $LASTEXITCODE
            Lines = [string[]]$output
            TranscriptPath = $tempFile
        }
    }
    catch {
        Remove-Item -Path $tempFile -ErrorAction SilentlyContinue
        throw
    }
}

function Get-LogLines {
    param([string]$LogPath)

    $resolved = Resolve-Path $LogPath -ErrorAction Stop
    [pscustomobject]@{
        Source = 'log-file'
        ExitCode = 0
        Lines = [string[]](Get-Content -Path $resolved.Path -Encoding utf8)
        TranscriptPath = $resolved.Path
    }
}

function Classify-DiagnosticGroup {
    param($Group)

    $sample = $Group.Group | Select-Object -First 5
    $sampleText = ($sample | ForEach-Object { $_.Message }) -join "`n"
    $file = $Group.Name

    if ($sampleText -match 'Godot\.NET\.Sdk' -or $sampleText -match 'MSB4236') {
        return 'godot-sdk-resolution-failed'
    }

    if ($file -match '\\.godot\\mono\\temp\\obj\\' -and $sampleText -match 'TargetFrameworkAttribute|AssemblyCompanyAttribute|AssemblyVersionAttribute|duplicate') {
        return 'duplicate-generated-assembly-attributes'
    }

    if ($file -match '\\Tests\\' -or $sampleText -match 'Xunit|TheoryAttribute|InlineDataAttribute|FactAttribute') {
        return 'test-sources-compiled-into-main-project'
    }

    if ($sampleText -match 'GenerateSwizzle|Lattice\.Generators|namespace name ''Generators'' does not exist in the namespace ''Lattice''') {
        return 'lattice-generator-missing'
    }

    if ($sampleText -match 'Cannot navigate to' -or $sampleText -match 'not been found in the file system') {
        return 'godot-resource-path-invalid'
    }

    return 'unclassified'
}

function Build-Report {
    param(
        [System.Collections.Generic.List[object]]$Diagnostics,
        [string]$Source,
        [int]$ExitCode,
        [string]$TranscriptPath,
        [int]$MaxItemsPerFile,
        [bool]$IncludeWarnings
    )

    $filtered = if ($IncludeWarnings) {
        $Diagnostics
    }
    else {
        $Diagnostics.Where({ $_.Level -eq 'error' })
    }

    $errors = @($filtered | Where-Object { $_.Level -eq 'error' })
    $warnings = @($filtered | Where-Object { $_.Level -eq 'warning' })
    $grouped = @($filtered | Group-Object File | Sort-Object -Property @{ Expression = 'Count'; Descending = $true }, @{ Expression = 'Name'; Descending = $false })

    $files = foreach ($group in $grouped) {
        [pscustomobject]@{
            File = $group.Name
            Count = $group.Count
            Category = Classify-DiagnosticGroup -Group $group
            Items = @($group.Group | Select-Object -First $MaxItemsPerFile)
        }
    }

    [pscustomobject]@{
        Source = $Source
        ExitCode = $ExitCode
        TranscriptPath = $TranscriptPath
        IncludeWarnings = $IncludeWarnings
        ErrorCount = $errors.Count
        WarningCount = $warnings.Count
        FileCount = $grouped.Count
        Files = @($files)
    }
}

function Format-TextReport {
    param($Report)

    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add("Source: $($Report.Source)")
    $lines.Add("ExitCode: $($Report.ExitCode)")
    $lines.Add("Transcript: $($Report.TranscriptPath)")
    $lines.Add("Errors: $($Report.ErrorCount)  Warnings: $($Report.WarningCount)  Files: $($Report.FileCount)")
    $lines.Add('')

    foreach ($file in $Report.Files) {
        $lines.Add("[$($file.Count)] $($file.File) => $($file.Category)")
        foreach ($item in $file.Items) {
            $position = if ($null -ne $item.Line) {
                " ($($item.Line),$($item.Column))"
            }
            else {
                ''
            }

            $code = if ([string]::IsNullOrWhiteSpace($item.Code)) {
                ''
            }
            else {
                " [$($item.Code)]"
            }

            $lines.Add("  - $($item.Level.ToUpperInvariant())$code$position $($item.Message)")
        }

        if ($file.Count -gt $file.Items.Count) {
            $lines.Add("  - ... $($file.Count - $file.Items.Count) more")
        }

        $lines.Add('')
    }

    return ($lines -join "`n")
}

if ($Mode -eq 'LogFile' -and [string]::IsNullOrWhiteSpace($LogPath)) {
    throw 'LogFile mode requires -LogPath.'
}

$result = if ($Mode -eq 'LogFile') {
    Get-LogLines -LogPath $LogPath
}
elseif ($Mode -eq 'Build' -or $Mode -eq 'Auto') {
    Get-BuildLines -ProjectPath $ProjectPath
}
else {
    throw "Unsupported mode: $Mode"
}

$diagnostics = [System.Collections.Generic.List[object]]::new()
foreach ($line in $result.Lines) {
    $parsed = Parse-BuildOutputLine -Line $line -Source $result.Source
    if ($null -ne $parsed) {
        $diagnostics.Add($parsed)
    }
}

$report = Build-Report `
    -Diagnostics $diagnostics `
    -Source $result.Source `
    -ExitCode $result.ExitCode `
    -TranscriptPath $result.TranscriptPath `
    -MaxItemsPerFile $MaxItemsPerFile `
    -IncludeWarnings $IncludeWarnings.IsPresent

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
