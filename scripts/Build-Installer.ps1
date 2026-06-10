# Build-Installer.ps1 — Builds the application and produces DevOpsInControl-Setup.exe
# Requires: .NET 8 SDK, Node.js 18+, Inno Setup 6 (iscc.exe in PATH or default location)

param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Definition)

Write-Host ""
Write-Host "  DevOps InControl — Installer Build" -ForegroundColor Cyan
Write-Host "  ===================================" -ForegroundColor Cyan
Write-Host ""

# ---------------------------------------------------------------------------
# 1. Locate Inno Setup Compiler
# ---------------------------------------------------------------------------
Write-Host "[1/5] Locating Inno Setup compiler..." -ForegroundColor Yellow
$iscc = Get-Command iscc -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source
if (-not $iscc) {
    # Check default install locations
    $defaultPaths = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    )
    foreach ($p in $defaultPaths) {
        if (Test-Path $p) { $iscc = $p; break }
    }
}
if (-not $iscc) {
    Write-Host "  ERROR: Inno Setup compiler (ISCC.exe) not found." -ForegroundColor Red
    Write-Host "  Install Inno Setup 6 from: https://jrsoftware.org/isdownload.php" -ForegroundColor Red
    exit 1
}
Write-Host "  Found: $iscc" -ForegroundColor Green

# ---------------------------------------------------------------------------
# 2. Install npm dependencies & build frontend
# ---------------------------------------------------------------------------
Write-Host "[2/5] Building frontend..." -ForegroundColor Yellow
$frontendDir = Join-Path $root "dashboard\frontend"
Push-Location $frontendDir
try {
    npm install --loglevel warn --omit=peer
    if ($LASTEXITCODE -ne 0) { throw "npm install failed" }
    npm run build
    if ($LASTEXITCODE -ne 0) { throw "npm run build failed" }
} finally {
    Pop-Location
}
Write-Host "  Frontend built." -ForegroundColor Green

# ---------------------------------------------------------------------------
# 3. Publish backend
# ---------------------------------------------------------------------------
Write-Host "[3/5] Publishing backend..." -ForegroundColor Yellow
$backendDir = Join-Path $root "dashboard\backend-dotnet"
$appDir = Join-Path $root "app"

# Stop any running instance
Get-Process -Name DashboardApi -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

# Clean previous output
if (Test-Path $appDir) { Remove-Item $appDir -Recurse -Force }

dotnet publish "$backendDir" --configuration $Configuration --output "$appDir" --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

# Copy frontend dist into wwwroot
$distDir = Join-Path $frontendDir "dist"
$wwwroot = Join-Path $appDir "wwwroot"
if (Test-Path $wwwroot) { Remove-Item $wwwroot -Recurse -Force }
Copy-Item $distDir $wwwroot -Recurse

# Copy icon
$iconSrc = Join-Path $root "dashboard\frontend\public\app.ico"
if (Test-Path $iconSrc) { Copy-Item $iconSrc (Join-Path $appDir "app.ico") -Force }

# Ensure data directory exists (app creates config.json on first run)
$configDestDir = Join-Path $appDir "data"
if (-not (Test-Path $configDestDir)) { New-Item -ItemType Directory -Path $configDestDir | Out-Null }

# NOTE: config.json is NOT bundled — it contains user-specific DPAPI-encrypted
# secrets that only work on the original machine. The app generates a default
# config on first run.

Write-Host "  Backend published to app/." -ForegroundColor Green

# ---------------------------------------------------------------------------
# 4. Compile installer
# ---------------------------------------------------------------------------
Write-Host "[4/5] Compiling installer..." -ForegroundColor Yellow
$issFile = Join-Path $root "installer\setup.iss"
& $iscc "$issFile"
if ($LASTEXITCODE -ne 0) { throw "Inno Setup compilation failed" }
Write-Host "  Installer compiled." -ForegroundColor Green

# ---------------------------------------------------------------------------
# 5. Done
# ---------------------------------------------------------------------------
$outputDir = $root
$exeFile = Get-ChildItem $outputDir -Filter "DevOpsInControl-*-Setup.exe" | Sort-Object LastWriteTime -Descending | Select-Object -First 1

Write-Host ""
Write-Host "[5/5] Done!" -ForegroundColor Green
Write-Host ""
Write-Host "  Installer: $($exeFile.FullName)" -ForegroundColor Cyan
Write-Host "  Size:      $([math]::Round($exeFile.Length / 1MB, 1)) MB" -ForegroundColor Cyan
Write-Host ""
