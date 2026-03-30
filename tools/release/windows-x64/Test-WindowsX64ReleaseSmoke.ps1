param(
    [Parameter(Mandatory = $true)]
    [string]$PublishDir,

    [string]$ProductName = "Sideline",
    [string]$ExpectedPageId = "client.idle",
    [string]$ExpectedLuaScript = "res://scripts/facet/LuaScripts/idle_runtime.lua",
    [int]$StartupTimeoutSeconds = 30,
    [int]$PollIntervalMilliseconds = 500
)

$ErrorActionPreference = "Stop"

if ($StartupTimeoutSeconds -lt 5)
{
    throw "StartupTimeoutSeconds must be at least 5 seconds."
}

if ($PollIntervalMilliseconds -lt 100)
{
    throw "PollIntervalMilliseconds must be at least 100 milliseconds."
}

$resolvedPublishDir = [System.IO.Path]::GetFullPath($PublishDir)
if (-not (Test-Path -LiteralPath $resolvedPublishDir))
{
    throw "Publish directory not found: $resolvedPublishDir"
}

$executablePath = Join-Path $resolvedPublishDir ("{0}.exe" -f $ProductName)
if (-not (Test-Path -LiteralPath $executablePath))
{
    throw "Smoke test failed because the executable is missing: $executablePath"
}

$logDir = Join-Path $resolvedPublishDir "logs"
$consoleLogPath = Join-Path $logDir "facet-console.log"
$structuredLogPath = Join-Path $logDir "facet-structured.jsonl"

if (Test-Path -LiteralPath $logDir)
{
    Remove-Item -LiteralPath $logDir -Recurse -Force
}

function Test-StructuredSmokeEvidence
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$ExpectedPageId,

        [Parameter(Mandatory = $true)]
        [string]$ExpectedLuaScript
    )

    $result = [ordered]@{
        BootstrapRuntimeReady = $false
        InitialPageReady = $false
        LuaControllerReady = $false
    }

    if (-not (Test-Path -LiteralPath $Path))
    {
        return [pscustomobject]$result
    }

    $lines = Get-Content -LiteralPath $Path -Encoding UTF8 -ErrorAction SilentlyContinue
    foreach ($line in $lines)
    {
        if ([string]::IsNullOrWhiteSpace($line))
        {
            continue
        }

        try
        {
            $entry = $line | ConvertFrom-Json -ErrorAction Stop
        }
        catch
        {
            continue
        }

        $payload = $entry.Payload
        if ($null -eq $payload)
        {
            continue
        }

        if (
            $entry.Category -eq "Bootstrap" -and
            $payload.runtimeEnvironment -eq "runtime" -and
            $payload.usesPackagedResources -eq $true
        )
        {
            $result.BootstrapRuntimeReady = $true
        }

        if (
            $entry.Category -eq "Client.Main" -and
            $payload.currentPageId -eq $ExpectedPageId
        )
        {
            $result.InitialPageReady = $true
        }

        if (
            $entry.Category -eq "Client.Main" -and
            $payload.currentPageId -eq $ExpectedPageId -and
            $payload.hasLuaController -eq $true -and
            $payload.luaControllerScript -eq $ExpectedLuaScript
        )
        {
            $result.LuaControllerReady = $true
        }
    }

    return [pscustomobject]$result
}

$process = $null
$smokePassed = $false
$failureReason = ""

try
{
    $process = Start-Process -FilePath $executablePath -WorkingDirectory $resolvedPublishDir -PassThru
    $deadline = (Get-Date).AddSeconds($StartupTimeoutSeconds)

    while ((Get-Date) -lt $deadline)
    {
        if ($process.HasExited)
        {
            $failureReason = "Sideline.exe exited during startup smoke test. ExitCode=$($process.ExitCode)"
            break
        }

        $hasConsoleLog = Test-Path -LiteralPath $consoleLogPath
        $hasStructuredLog = Test-Path -LiteralPath $structuredLogPath
        $evidence = Test-StructuredSmokeEvidence `
            -Path $structuredLogPath `
            -ExpectedPageId $ExpectedPageId `
            -ExpectedLuaScript $ExpectedLuaScript

        if (
            $hasConsoleLog -and
            $hasStructuredLog -and
            $evidence.BootstrapRuntimeReady -and
            $evidence.InitialPageReady -and
            $evidence.LuaControllerReady
        )
        {
            $smokePassed = $true
            break
        }

        Start-Sleep -Milliseconds $PollIntervalMilliseconds
    }

    if (-not $smokePassed)
    {
        if ([string]::IsNullOrWhiteSpace($failureReason))
        {
            $evidence = Test-StructuredSmokeEvidence `
                -Path $structuredLogPath `
                -ExpectedPageId $ExpectedPageId `
                -ExpectedLuaScript $ExpectedLuaScript
            $failureReason = "Did not collect complete startup evidence within ${StartupTimeoutSeconds}s. Bootstrap=$($evidence.BootstrapRuntimeReady); InitialPage=$($evidence.InitialPageReady); LuaController=$($evidence.LuaControllerReady)"
        }

        throw "Windows x64 release smoke test failed.`nReason: $failureReason`nConsoleLog: $consoleLogPath`nStructuredLog: $structuredLogPath"
    }
}
finally
{
    if ($null -ne $process -and -not $process.HasExited)
    {
        try
        {
            Stop-Process -Id $process.Id -Force
            $process.WaitForExit()
        }
        catch
        {
        }
    }
}

[pscustomobject]@{
    PublishDir = $resolvedPublishDir
    Executable = $executablePath
    ConsoleLogPath = $consoleLogPath
    StructuredLogPath = $structuredLogPath
    ExpectedPageId = $ExpectedPageId
    ExpectedLuaScript = $ExpectedLuaScript
    SmokeTestPassed = $smokePassed
}
