# Launch-DevOpsInControl.ps1 — User-facing launcher for DevOps InControl
# Shows a branded terminal with the application running.
# Meant to be called from the Start Menu shortcut.

param(
    [int]$Port = 5172
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$logFile   = Join-Path $scriptDir "launch.log"

# Helper: pause without ReadKey (ReadKey can throw in certain console states)
function Wait-AndClose {
    Write-Host ""
    Write-Host "  Press any key to close..." -ForegroundColor DarkGray
    try { cmd /c pause | Out-Null } catch { Start-Sleep -Seconds 10 }
}

# Trap any unhandled error so the terminal never silently closes
trap {
    $msg = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') FATAL: $_"
    try { $msg | Out-File -Append -FilePath $logFile -Encoding utf8 } catch {}
    Write-Host ""
    Write-Host "  FATAL: $_" -ForegroundColor Red
    Wait-AndClose
    exit 1
}

$ErrorActionPreference = "Stop"
# Repo layout: scripts/ subfolder → use parent. Installed layout: script at root → use self.
$root = if (Test-Path (Join-Path $scriptDir "..\start.ps1")) { (Resolve-Path (Join-Path $scriptDir "..")).Path } else { $scriptDir }
$Host.UI.RawUI.WindowTitle = "DevOps InControl"

# ---------------------------------------------------------------------------
# Banner
# ---------------------------------------------------------------------------
Write-Host ""
Write-Host "  ╔════════════════════════════╗" -ForegroundColor Cyan
Write-Host "  ║     DevOps InControl       ║" -ForegroundColor Cyan
Write-Host "  ╚════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Starting..." -ForegroundColor DarkGray
Write-Host ""

# ---------------------------------------------------------------------------
# Locate the published app
# ---------------------------------------------------------------------------
$exe = Join-Path $root "DashboardApi.exe"
if (-not (Test-Path $exe)) {
    # Fallback: dev layout where app/ is a subfolder
    $exe = Join-Path $root "app\DashboardApi.exe"
}

if (-not (Test-Path $exe)) {
    Write-Host "  ERROR: Published app not found." -ForegroundColor Red
    Write-Host "  Searched:" -ForegroundColor Red
    Write-Host "    $(Join-Path $root 'DashboardApi.exe')" -ForegroundColor Red
    Write-Host "    $(Join-Path $root 'app\DashboardApi.exe')" -ForegroundColor Red
    Write-Host ""
    Write-Host "  Run install.ps1 first to build the application." -ForegroundColor Yellow
    Write-Host ""
    Wait-AndClose
    exit 1
}

# ---------------------------------------------------------------------------
# Kill any stale instance on the port
# ---------------------------------------------------------------------------
$stalePids = netstat -ano | Select-String ":$Port\s" |
    ForEach-Object { if ($_ -match '\s(\d+)\s*$') { [int]$Matches[1] } } |
    Where-Object { $_ -ne 0 } | Sort-Object -Unique
foreach ($p in $stalePids) {
    Stop-Process -Id $p -Force -ErrorAction SilentlyContinue
}

# Wait for the port to become free (up to 10 seconds)
if ($stalePids) {
    $deadline = (Get-Date).AddSeconds(10)
    while ((Get-Date) -lt $deadline) {
        $inUse = netstat -ano | Select-String ":$Port\s" |
            ForEach-Object { if ($_ -match '\s(\d+)\s*$') { [int]$Matches[1] } } |
            Where-Object { $_ -ne 0 }
        if (-not $inUse) { break }
        Start-Sleep -Milliseconds 300
    }
}

# ---------------------------------------------------------------------------
# Environment
# ---------------------------------------------------------------------------
if (-not $env:DB_TRUST_SERVER_CERTIFICATE) { $env:DB_TRUST_SERVER_CERTIFICATE = "true" }
if (-not $env:DB_POSTGRES_SSL_MODE) { $env:DB_POSTGRES_SSL_MODE = "prefer" }

# ---------------------------------------------------------------------------
# Start the application
# ---------------------------------------------------------------------------
$env:ASPNETCORE_URLS = "http://127.0.0.1:$Port"
$env:ASPNETCORE_ENVIRONMENT = "Production"

Write-Host "  Dashboard: " -NoNewline -ForegroundColor DarkGray
Write-Host "http://localhost:$Port" -ForegroundColor Green
Write-Host "  Press Ctrl+C to stop." -ForegroundColor DarkGray
Write-Host ""
Write-Host "  ─────────────────────────────────────────" -ForegroundColor DarkGray
Write-Host ""

# Open browser after a short delay (background job — non-critical)
try {
    $null = Start-Job -ScriptBlock {
        param($p)
        $deadline = (Get-Date).AddSeconds(30)
        while ((Get-Date) -lt $deadline) {
            try {
                $client = New-Object System.Net.Sockets.TcpClient
                $iar = $client.BeginConnect("127.0.0.1", $p, $null, $null)
                if ($iar.AsyncWaitHandle.WaitOne(500) -and $client.Connected) {
                    $client.EndConnect($iar)
                    $client.Close()
                    Start-Process "http://localhost:$p"
                    return
                }
                $client.Close()
            } catch {}
            Start-Sleep -Milliseconds 250
        }
    } -ArgumentList $Port
} catch {
    # Start-Job can fail in constrained environments — non-fatal
    "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') WARN: Start-Job failed: $_" | Out-File -Append -FilePath $logFile -Encoding utf8
}

# Run the app in the foreground — terminal stays open showing logs
try {
    & $exe --urls "http://127.0.0.1:$Port"
    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Host "  ERROR: Application exited with code $LASTEXITCODE." -ForegroundColor Red
        Write-Host "  The port may still be in use. Wait a few seconds and try again." -ForegroundColor Yellow
        "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') ERROR: Exit code $LASTEXITCODE" | Out-File -Append -FilePath $logFile -Encoding utf8
    }
} catch {
    Write-Host ""
    Write-Host "  ERROR: $($_.Exception.Message)" -ForegroundColor Red
    "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') ERROR: $($_.Exception.Message)" | Out-File -Append -FilePath $logFile -Encoding utf8
} finally {
    Write-Host ""
    Write-Host "  DevOps InControl has stopped." -ForegroundColor Yellow
    Wait-AndClose
}
