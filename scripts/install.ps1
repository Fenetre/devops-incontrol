# install.ps1 — DevOps InControl installer
# Checks prerequisites, builds the frontend, publishes the backend,
# creates a Start Menu shortcut, and optionally registers auto-start.

param(
    [switch]$SkipAutoStart
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Definition)

Write-Host ""
Write-Host "  DevOps InControl Installer" -ForegroundColor Cyan
Write-Host "  =======================" -ForegroundColor Cyan
Write-Host ""

# ---------------------------------------------------------------------------
# 1. Check .NET 8 SDK
# ---------------------------------------------------------------------------
Write-Host "[1/7] Checking .NET SDK..." -ForegroundColor Yellow
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
Write-Host "[2/7] Checking Node.js..." -ForegroundColor Yellow
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
Write-Host "[3/7] Installing frontend dependencies..." -ForegroundColor Yellow
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
# 4. Build frontend + publish backend
# ---------------------------------------------------------------------------
Write-Host "[4/7] Building application..." -ForegroundColor Yellow

Push-Location $frontendDir
try {
    npm run build
    if ($LASTEXITCODE -ne 0) { throw "npm run build failed" }
} finally {
    Pop-Location
}
Write-Host "  Frontend built." -ForegroundColor Green

$backendDir = Join-Path $root "dashboard\backend-dotnet"
$appDir = Join-Path $root "app"

# Stop any running instance so publish can overwrite binaries
Get-Process -Name DashboardApi -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

# Publish backend as framework-dependent (requires .NET 8 runtime)
dotnet publish "$backendDir" --configuration Release --output "$appDir" --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }
Write-Host "  Backend published." -ForegroundColor Green

# Copy frontend dist into the published app's wwwroot
$distDir = Join-Path $frontendDir "dist"
$wwwroot = Join-Path $appDir "wwwroot"
if (Test-Path $wwwroot) { Remove-Item $wwwroot -Recurse -Force }
Copy-Item $distDir $wwwroot -Recurse
Write-Host "  Frontend assets copied to app." -ForegroundColor Green

# Copy the icon into the app folder for the shortcut
$iconSrc = Join-Path $root "dashboard\frontend\public\app.ico"
$iconDest = Join-Path $appDir "app.ico"
if (Test-Path $iconSrc) { Copy-Item $iconSrc $iconDest -Force }

# ---------------------------------------------------------------------------
# 5. Create Start Menu shortcut
# ---------------------------------------------------------------------------
Write-Host "[5/7] Creating Start Menu shortcut..." -ForegroundColor Yellow

$launcherScript = Join-Path $root "scripts\Launch-DevOpsInControl.ps1"
$shortcutDir = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"
$shortcutPath = Join-Path $shortcutDir "DevOps InControl.lnk"

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = (Get-Process -Id $PID).Path  # pwsh.exe
$shortcut.Arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$launcherScript`""
$shortcut.WorkingDirectory = $root
$shortcut.Description = "DevOps InControl — Fenêtre ProjectMaster"
$shortcut.WindowStyle = 1  # Normal window
if (Test-Path $iconDest) { $shortcut.IconLocation = "$iconDest,0" }
$shortcut.Save()
Write-Host "  Start Menu shortcut created." -ForegroundColor Green

# ---------------------------------------------------------------------------
# 6. Register in Apps & Features
# ---------------------------------------------------------------------------
Write-Host "[6/7] Registering in Apps & Features..." -ForegroundColor Yellow

$regPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\DevOpsInControl"
$uninstallScript = Join-Path $root "scripts\Uninstall-DevOpsInControl.ps1"
$pwshPath = (Get-Process -Id $PID).Path

if (-not (Test-Path $regPath)) { New-Item -Path $regPath -Force | Out-Null }
Set-ItemProperty -Path $regPath -Name "DisplayName" -Value "DevOps InControl"
Set-ItemProperty -Path $regPath -Name "DisplayVersion" -Value "1.2.0"
Set-ItemProperty -Path $regPath -Name "Publisher" -Value "Fenêtre"
Set-ItemProperty -Path $regPath -Name "InstallLocation" -Value $root
Set-ItemProperty -Path $regPath -Name "UninstallString" -Value "`"$pwshPath`" -NoProfile -ExecutionPolicy Bypass -File `"$uninstallScript`""
Set-ItemProperty -Path $regPath -Name "DisplayIcon" -Value "$iconDest,0"
Set-ItemProperty -Path $regPath -Name "NoModify" -Value 1 -Type DWord
Set-ItemProperty -Path $regPath -Name "NoRepair" -Value 1 -Type DWord
Write-Host "  Registered." -ForegroundColor Green

# ---------------------------------------------------------------------------
# 7. Register auto-start (optional)
# ---------------------------------------------------------------------------
if (-not $SkipAutoStart) {
    Write-Host "[7/7] Auto-start registration..." -ForegroundColor Yellow
    Write-Host ""
    $answer = Read-Host "  Register DevOps InControl to start automatically at Windows logon? (y/n)"
    if ($answer -match '^(y|Y|yes|)$') {
        $launchScript = Join-Path $root "scripts\Launch-DevOpsInControl.ps1"
        $regKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
        $regValue = "pwsh -NoProfile -WindowStyle Normal -ExecutionPolicy Bypass -File `"$launchScript`""
        Set-ItemProperty -Path $regKey -Name "DevOpsInControl" -Value $regValue
        Write-Host "  Auto-start registered (Registry Run key)." -ForegroundColor Green
    } else {
        Write-Host "  Skipped auto-start registration." -ForegroundColor DarkGray
    }
} else {
    Write-Host "[7/7] Skipping auto-start (-SkipAutoStart)." -ForegroundColor DarkGray
}

# ---------------------------------------------------------------------------
# Done
# ---------------------------------------------------------------------------
Write-Host ""
Write-Host "  Installation complete!" -ForegroundColor Green
Write-Host ""
Write-Host "  You can now find 'DevOps InControl' in your Start menu." -ForegroundColor Cyan
Write-Host "  Or start it manually: .\start.ps1" -ForegroundColor Cyan
Write-Host ""
Write-Host "  The dashboard will open at http://localhost:5172" -ForegroundColor Cyan
Write-Host "  A setup wizard will guide you through the first-time configuration." -ForegroundColor Cyan
Write-Host ""

$startNow = Read-Host "  Start DevOps InControl now? (y/n)"
if ($startNow -match '^(y|Y|yes|)$') {
    & $launcherScript
}
