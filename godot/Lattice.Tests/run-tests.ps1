# Lattice Test Runner
# Usage: .\run-tests.ps1 [-Detailed] [-Category <name>] [-List] [-Coverage]

param(
    [switch]$Detailed,
    [switch]$List,
    [string]$Category = "",
    [string]$Filter = "",
    [switch]$Coverage,
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

# Colors
$Green = "Green"
$Red = "Red"
$Yellow = "Yellow"
$Cyan = "Cyan"

Write-Host "========================================" -ForegroundColor $Cyan
Write-Host "  Lattice Test Runner" -ForegroundColor $Cyan
Write-Host "========================================" -ForegroundColor $Cyan
Write-Host ""

# Build
if (-not $NoBuild) {
    Write-Host "Building..." -ForegroundColor $Yellow
    dotnet build --verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed!" -ForegroundColor $Red
        exit 1
    }
    Write-Host "Build success" -ForegroundColor $Green
    Write-Host ""
}

# Determine filter
$testFilter = $Filter
if ($Category) {
    $testFilter = "FullyQualifiedName~$Category"
    Write-Host "Category: $Category" -ForegroundColor $Cyan
}

# List tests
if ($List) {
    Write-Host "Available tests:" -ForegroundColor $Cyan
    dotnet test --list-tests
    exit 0
}

# Run tests
Write-Host "Running tests..." -ForegroundColor $Cyan
$args = @("test", "--no-build")

if (-not $Detailed) {
    $args += "--verbosity"
    $args += "minimal"
} else {
    $args += "--verbosity"
    $args += "normal"
}

if ($testFilter) {
    $args += "--filter"
    $args += $testFilter
}

if ($Coverage) {
    $args += "/p:CollectCoverage=true"
    $args += "/p:CoverletOutputFormat=lcov"
}

dotnet @args
$exitCode = $LASTEXITCODE

Write-Host ""
if ($exitCode -eq 0) {
    Write-Host "All tests passed!" -ForegroundColor $Green
} else {
    Write-Host "Some tests failed!" -ForegroundColor $Red
}

exit $exitCode
