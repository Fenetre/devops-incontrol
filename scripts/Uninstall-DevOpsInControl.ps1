# Uninstall-DevOpsInControl.ps1 — Removes DevOps InControl from the system
# Removes: Start Menu shortcut, Desktop shortcut, scheduled task, published app,
# and Apps & Features registry entry.
# Preserves: dashboard/data/config.json (user configuration)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Definition)

Write-Host ""
Write-Host "  DevOps InControl Uninstaller" -ForegroundColor Cyan
Write-Host "  ============================" -ForegroundColor Cyan
Write-Host ""

# ---------------------------------------------------------------------------
# 1. Stop any running instance
# ---------------------------------------------------------------------------
Write-Host "[1/5] Stopping running instances..." -ForegroundColor Yellow
Get-Process -Name DashboardApi -ErrorAction SilentlyContinue | Stop-Process -Force
Write-Host "  Done." -ForegroundColor Green

# ---------------------------------------------------------------------------
# 2. Remove Start Menu shortcut
# ---------------------------------------------------------------------------
Write-Host "[2/5] Removing Start Menu shortcut..." -ForegroundColor Yellow
$startMenuPath = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\DevOps InControl.lnk"
if (Test-Path $startMenuPath) {
    Remove-Item $startMenuPath -Force
    Write-Host "  Removed." -ForegroundColor Green
} else {
    Write-Host "  Not found (skipped)." -ForegroundColor DarkGray
}

# Remove Desktop shortcut if present
$desktopPath = Join-Path ([Environment]::GetFolderPath("Desktop")) "DevOps InControl.lnk"
if (Test-Path $desktopPath) {
    Remove-Item $desktopPath -Force
    Write-Host "  Desktop shortcut removed." -ForegroundColor Green
}

# ---------------------------------------------------------------------------
# 3. Remove scheduled task (auto-start)
# ---------------------------------------------------------------------------
Write-Host "[3/5] Removing auto-start scheduled task..." -ForegroundColor Yellow
$taskName = "DevOpsBacklogMonitor"
$task = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
if ($task) {
    Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
    Write-Host "  Scheduled task '$taskName' removed." -ForegroundColor Green
} else {
    Write-Host "  No scheduled task found (skipped)." -ForegroundColor DarkGray
}

# ---------------------------------------------------------------------------
# 4. Remove published app folder
# ---------------------------------------------------------------------------
Write-Host "[4/5] Removing published app..." -ForegroundColor Yellow
$appDir = Join-Path $root "app"
if (Test-Path $appDir) {
    Remove-Item $appDir -Recurse -Force
    Write-Host "  Removed: $appDir" -ForegroundColor Green
} else {
    Write-Host "  Not found (skipped)." -ForegroundColor DarkGray
}

# ---------------------------------------------------------------------------
# 5. Remove Apps & Features registry entry
# ---------------------------------------------------------------------------
Write-Host "[5/5] Removing registry entry..." -ForegroundColor Yellow
$regPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\DevOpsInControl"
if (Test-Path $regPath) {
    Remove-Item $regPath -Recurse -Force
    Write-Host "  Registry entry removed." -ForegroundColor Green
} else {
    Write-Host "  Not found (skipped)." -ForegroundColor DarkGray
}

# ---------------------------------------------------------------------------
# Done
# ---------------------------------------------------------------------------
Write-Host ""
Write-Host "  Uninstall complete." -ForegroundColor Green
Write-Host ""
Write-Host "  Note: Your configuration (dashboard/data/config.json) was preserved." -ForegroundColor Cyan
Write-Host "  To fully remove all files, delete the folder:" -ForegroundColor DarkGray
Write-Host "    $root" -ForegroundColor White
Write-Host ""
