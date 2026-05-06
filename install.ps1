# install.ps1 — DevOps InControl first-time setup
# Checks prerequisites, installs dependencies, builds the frontend,
# and optionally registers the Windows auto-start task.

param(
    [switch]$SkipAutoStart
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Definition

Write-Host ""
Write-Host "  DevOps InControl Installer" -ForegroundColor Cyan
Write-Host "  =======================" -ForegroundColor Cyan
Write-Host ""

# ---------------------------------------------------------------------------
# 1. Check .NET 8 SDK
# ---------------------------------------------------------------------------
Write-Host "[1/5] Checking .NET SDK..." -ForegroundColor Yellow
$dotnetVersion = $null
try {
    $dotnetVersion = dotnet --version 2>$null
} catch {}

if (-not $dotnetVersion) {
    Write-Host "  ERROR: .NET SDK not found." -ForegroundColor Red
    Write-Host "  Please install .NET 8 SDK from: https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Red
    exit 1
}

$major = ($dotnetVersion -split '\.')[0]
if ([int]$major -lt 8) {
    Write-Host "  ERROR: .NET 8+ required (found $dotnetVersion)." -ForegroundColor Red
    Write-Host "  Please install .NET 8 SDK from: https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Red
    exit 1
}
Write-Host "  .NET SDK $dotnetVersion" -ForegroundColor Green

# ---------------------------------------------------------------------------
# 2. Check Node.js / npm
# ---------------------------------------------------------------------------
Write-Host "[2/5] Checking Node.js..." -ForegroundColor Yellow
$nodeVersion = $null
try {
    $nodeVersion = node --version 2>$null
} catch {}

if (-not $nodeVersion) {
    Write-Host "  ERROR: Node.js not found." -ForegroundColor Red
    Write-Host "  Please install Node.js 18+ from: https://nodejs.org" -ForegroundColor Red
    exit 1
}
Write-Host "  Node.js $nodeVersion" -ForegroundColor Green

# ---------------------------------------------------------------------------
# 3. Install npm dependencies
# ---------------------------------------------------------------------------
Write-Host "[3/5] Installing frontend dependencies..." -ForegroundColor Yellow
$frontendDir = Join-Path $root "dashboard\frontend"
Push-Location $frontendDir
try {
    npm install --loglevel warn --omit=peer
    if ($LASTEXITCODE -ne 0) { throw "npm install failed" }
} finally {
    Pop-Location
}
Write-Host "  npm packages installed." -ForegroundColor Green

# ---------------------------------------------------------------------------
# 4. Build frontend + restore backend
# ---------------------------------------------------------------------------
Write-Host "[4/5] Building..." -ForegroundColor Yellow

Push-Location $frontendDir
try {
    npm run build
    if ($LASTEXITCODE -ne 0) { throw "npm run build failed" }
} finally {
    Pop-Location
}
Write-Host "  Frontend built." -ForegroundColor Green

$backendDir = Join-Path $root "dashboard\backend-dotnet"
# Stop any running instance so the build can overwrite binaries
Get-Process -Name DashboardApi -ErrorAction SilentlyContinue | Stop-Process -Force
dotnet restore "$backendDir" --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed" }
# Build to an isolated output path to avoid file-lock conflicts with running instances.
$backendBuildOutput = Join-Path $root ".tmpbuild\backend-install\"
dotnet build "$backendDir" --no-restore --verbosity quiet -p:BaseOutputPath="$backendBuildOutput"
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed" }
Write-Host "  Backend built." -ForegroundColor Green

# ---------------------------------------------------------------------------
# 5. Register auto-start (optional)
# ---------------------------------------------------------------------------
if (-not $SkipAutoStart) {
    Write-Host "[5/5] Auto-start registration..." -ForegroundColor Yellow
    Write-Host ""
    $answer = Read-Host "  Register DevOps InControl to start automatically at Windows logon? (Y/n)"
    if ($answer -match '^(y|Y|yes|)$') {
        $regScript = Join-Path $root "Register-StartupTask.ps1"
        Start-Process pwsh -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$regScript`"" -Verb RunAs -Wait
        Write-Host "  Auto-start registered." -ForegroundColor Green
    } else {
        Write-Host "  Skipped auto-start registration." -ForegroundColor DarkGray
    }
} else {
    Write-Host "[5/5] Skipping auto-start (--SkipAutoStart)." -ForegroundColor DarkGray
}

# ---------------------------------------------------------------------------
# Done
# ---------------------------------------------------------------------------
Write-Host ""
Write-Host "  Installation complete!" -ForegroundColor Green
Write-Host ""
Write-Host "  To start DevOps InControl:" -ForegroundColor Cyan
Write-Host "    .\start.ps1" -ForegroundColor White
Write-Host ""
Write-Host "  The dashboard will open at http://localhost:5172" -ForegroundColor Cyan
Write-Host "  A setup wizard will guide you through the first-time configuration." -ForegroundColor Cyan
Write-Host ""

$startNow = Read-Host "  Start DevOps InControl now? (Y/n)"
if ($startNow -match '^(y|Y|yes|)$') {
    & (Join-Path $root "start.ps1")
}
