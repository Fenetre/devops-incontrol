# DevOps Backlog Monitor — startup script
# Default: production mode (single .NET process serving the built frontend).
# Pass -Dev to run the Vite dev server alongside the backend (hot-reload).
# Pass -Background to suppress console output (used by scheduled task).

param(
    [switch]$Dev,
    [switch]$Background,
    [switch]$BackgroundChild,
    [int]$BackendPort = 5172,
    [int]$FrontendPort = 5173
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Definition

# ---------------------------------------------------------------------------
# Background mode: relaunch ourselves as a detached, console-less child and
# exit the visible parent immediately.
#
# Task Scheduler in an interactive session always allocates a conhost window
# for pwsh BEFORE pwsh can run any code. -WindowStyle Hidden and
# ShowWindow(SW_HIDE) both run too late — Explorer has already pinned a
# taskbar button, and on slow logons that button stays as a minimised stub
# until the process exits.
#
# The only reliable trick on a remote drive (S4U not available) is to make
# the visible parent process exit. We do that by re-spawning ourselves via
# Process.Start with UseShellExecute=false and CreateNoWindow=true: the
# child gets NO console at all, so there is no window and no taskbar entry
# to pin. The -BackgroundChild switch tells the second instance to skip this
# block and start the actual work.
# ---------------------------------------------------------------------------
if ($Background -and -not $BackgroundChild) {
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName        = (Get-Process -Id $PID).Path   # same pwsh.exe we are running under
    $psi.WorkingDirectory = $root
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow  = $true
    $psi.RedirectStandardOutput = $false
    $psi.RedirectStandardInput  = $false
    $psi.RedirectStandardError  = $false

    $childArgs = @(
        '-ExecutionPolicy', 'Bypass',
        '-NoProfile',
        '-NonInteractive',
        '-WindowStyle', 'Hidden',
        '-File', $MyInvocation.MyCommand.Path,
        '-Background',
        '-BackgroundChild',
        '-BackendPort', $BackendPort,
        '-FrontendPort', $FrontendPort
    )
    if ($Dev) { $childArgs += '-Dev' }
    foreach ($a in $childArgs) { $psi.ArgumentList.Add([string]$a) }

    [void][System.Diagnostics.Process]::Start($psi)
    # Exit the visible parent immediately — taskbar entry disappears with it.
    exit 0
}

# Local dev defaults for DB TLS compatibility. These can be overridden by
# explicitly setting the environment variables before launching the script.
if (-not $env:DB_TRUST_SERVER_CERTIFICATE) { $env:DB_TRUST_SERVER_CERTIFICATE = "true" }
if (-not $env:DB_POSTGRES_SSL_MODE) { $env:DB_POSTGRES_SSL_MODE = "prefer" }

# Log folder/file for headless / scheduled-task mode. One file per day,
# kept for 7 days. Written to %TEMP% so it is always writable and easy to
# find when diagnosing auto-start failures.
$logDir  = Join-Path $env:TEMP "DevOpsInControl"
$logFile = Join-Path $logDir ("start-" + (Get-Date -Format "yyyy-MM-dd") + ".log")

if ($Background) {
    if (-not (Test-Path $logDir)) { New-Item -ItemType Directory -Path $logDir | Out-Null }
    # Remove log files older than 7 days
    Get-ChildItem -Path $logDir -Filter "start-*.log" -File -ErrorAction SilentlyContinue |
        Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-7) } |
        Remove-Item -Force -ErrorAction SilentlyContinue
}

$backgroundLauncherMutex = $null
$backgroundLauncherMutexOwned = $false

function Write-Log {
    param([string]$Message)
    $ts = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $line = "$ts  $Message"
    if ($Background) {
        Add-Content -Path $logFile -Value $line
    } else {
        Write-Host $line
    }
}

function Try-AcquireBackgroundLauncherMutex {
    param([int]$Port)

    $name = "Local\DevOpsInControl.BackgroundLauncher.$Port"
    $script:backgroundLauncherMutex = New-Object System.Threading.Mutex($false, $name)

    try {
        $script:backgroundLauncherMutexOwned = $script:backgroundLauncherMutex.WaitOne(0)
    }
    catch [System.Threading.AbandonedMutexException] {
        $script:backgroundLauncherMutexOwned = $true
    }

    if (-not $script:backgroundLauncherMutexOwned) {
        $script:backgroundLauncherMutex.Dispose()
        $script:backgroundLauncherMutex = $null
        return $false
    }

    return $true
}

function Release-BackgroundLauncherMutex {
    if ($script:backgroundLauncherMutexOwned -and $null -ne $script:backgroundLauncherMutex) {
        try { $script:backgroundLauncherMutex.ReleaseMutex() } catch {}
    }

    if ($null -ne $script:backgroundLauncherMutex) {
        $script:backgroundLauncherMutex.Dispose()
        $script:backgroundLauncherMutex = $null
    }

    $script:backgroundLauncherMutexOwned = $false
}

# ---------------------------------------------------------------------------
# Helper: wait until a TCP port accepts connections (or timeout)
# ---------------------------------------------------------------------------
function Wait-ForPort {
    param(
        [int]$Port,
        [int]$TimeoutSeconds = 60
    )
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $client = New-Object System.Net.Sockets.TcpClient
            $iar = $client.BeginConnect("127.0.0.1", $Port, $null, $null)
            if ($iar.AsyncWaitHandle.WaitOne(500) -and $client.Connected) {
                $client.EndConnect($iar)
                $client.Close()
                return $true
            }
            $client.Close()
        } catch {
            # connection refused — keep waiting
        }
        Start-Sleep -Milliseconds 250
    }
    return $false
}

# ---------------------------------------------------------------------------
# Helper: kill any process listening on a given TCP port
# ---------------------------------------------------------------------------
function Stop-ListenersOnPort {
    param([int]$Port)
    $pids = netstat -ano | Select-String ":$Port\s" |
        ForEach-Object { if ($_ -match '\s(\d+)\s*$') { [int]$Matches[1] } } |
        Where-Object { $_ -ne 0 } | Sort-Object -Unique
    foreach ($p in $pids) {
        Write-Log "Killing stale process $p on port $Port"
        Stop-Process -Id $p -Force -ErrorAction SilentlyContinue
    }
}

# ---------------------------------------------------------------------------
# Helper: kill a process and its entire descendant tree (bottom-up)
# ---------------------------------------------------------------------------
function Stop-ProcessTree {
    param([int]$ProcessId)
    Get-CimInstance Win32_Process -Filter "ParentProcessId=$ProcessId" -ErrorAction SilentlyContinue |
        ForEach-Object { Stop-ProcessTree -ProcessId $_.ProcessId }
    Stop-Process -Id $ProcessId -Force -ErrorAction SilentlyContinue
}

# ---------------------------------------------------------------------------
# Build a minimal environment — strip secrets from child processes.
# ---------------------------------------------------------------------------
$allowedEnvKeys = @("PATH", "PATHEXT", "SYSTEMROOT", "TEMP", "TMP", "COMPUTERNAME",
                     "USERNAME", "USERPROFILE", "APPDATA", "LOCALAPPDATA", "PROGRAMDATA",
                     "PROGRAMFILES", "PROGRAMFILES(X86)",
                     "HOMEDRIVE", "HOMEPATH", "DOTNET_ROOT", "DOTNET_CLI_HOME",
                     "PROCESSOR_ARCHITECTURE", "NUMBER_OF_PROCESSORS",
                     "CORS_ORIGINS",
                     "ALLOW_UNPROTECTED_API", "DB_TRUST_SERVER_CERTIFICATE", "DB_POSTGRES_SSL_MODE",
                     "SMTP_ALLOW_INSECURE", "TRUST_X_FORWARDED_FOR", "TRUSTED_PROXY_IPS",
                     "API_AUTH_SESSION_DAYS", "AUTH_COOKIE_SECURE",
                     "RATE_LIMIT", "RATE_WINDOW", "MAX_BODY_BYTES", "AZDO_TIMEOUT")
$secretKeys = @("AZDO_PAT", "SMTP_PASSWORD", "API_KEY", "DB_PASSWORD")

$cleanEnv = @{}
foreach ($key in [System.Environment]::GetEnvironmentVariables().Keys) {
    if ($secretKeys -contains $key) { continue }
    if ($allowedEnvKeys -contains $key) {
        $cleanEnv[$key] = [System.Environment]::GetEnvironmentVariable($key)
    }
}

# Derive CORS_ORIGINS from the configured ports unless the user has already
# set it explicitly. Backend port is needed for production mode (browser hits
# the backend directly); frontend port is needed for dev mode (Vite forwards
# requests with its own Origin header via the /api proxy).
if (-not $cleanEnv.ContainsKey("CORS_ORIGINS") -or [string]::IsNullOrWhiteSpace($cleanEnv["CORS_ORIGINS"])) {
    $cleanEnv["CORS_ORIGINS"] = "http://localhost:$BackendPort,http://127.0.0.1:$BackendPort,http://localhost:$FrontendPort,http://127.0.0.1:$FrontendPort"
}

if ($Background) {
    if (-not (Try-AcquireBackgroundLauncherMutex -Port $BackendPort)) {
        Write-Log "Another background launcher is already active on port $BackendPort. Exiting."
        exit 0
    }
}

# ---------------------------------------------------------------------------
# Production mode: build frontend, then run backend only
# ---------------------------------------------------------------------------
if (-not $Dev) {
    $frontendDir = Join-Path $root "dashboard\frontend"
    $distDir = Join-Path $frontendDir "dist"

    # Build frontend if dist/ doesn't exist
    if (-not (Test-Path $distDir)) {
        Write-Log "Building frontend..."
        Push-Location $frontendDir
        try {
            npm run build
            if ($LASTEXITCODE -ne 0) { throw "npm run build failed" }
        } finally {
            Pop-Location
        }
        Write-Log "Frontend build complete."
    }

    Stop-ListenersOnPort -Port $BackendPort

    $dotnetProject = Join-Path $root "dashboard\backend-dotnet"
    $backendStartInfo = New-Object System.Diagnostics.ProcessStartInfo
    $backendStartInfo.FileName = "dotnet"
    $backendStartInfo.Arguments = "run --project `"$dotnetProject`" --urls http://127.0.0.1:$BackendPort"
    $backendStartInfo.WorkingDirectory = $root
    $backendStartInfo.UseShellExecute = $false
    if ($Background) { $backendStartInfo.CreateNoWindow = $true }
    $backendStartInfo.EnvironmentVariables.Clear()
    foreach ($key in $cleanEnv.Keys) { $backendStartInfo.EnvironmentVariables[$key] = $cleanEnv[$key] }
    $backendProc = [System.Diagnostics.Process]::Start($backendStartInfo)

    Write-Log "DevOps InControl starting (production mode)..."
    Write-Log "Dashboard -> http://localhost:$BackendPort  (PID $($backendProc.Id))"

    if (-not $Background) {
        Write-Log "Waiting for backend to be ready..."
        if (Wait-ForPort -Port $BackendPort -TimeoutSeconds 90) {
            Start-Process "http://localhost:$BackendPort"
        } else {
            Write-Log "Backend did not become ready in time; opening browser anyway."
            Start-Process "http://localhost:$BackendPort"
        }
    }

    if ($Background) {
        Write-Log "Background mode — monitoring backend (PID $($backendProc.Id))."
        $maxRestarts = 5
        $restartCount = 0
        while ($true) {
            $backendProc.WaitForExit()
            $exitCode = $backendProc.ExitCode
            Write-Log "Backend exited (exit code $exitCode, PID $($backendProc.Id))."
            $restartCount++
            if ($restartCount -ge $maxRestarts) {
                Write-Log "Reached max restarts ($maxRestarts). Giving up."
                Release-BackgroundLauncherMutex
                exit 1
            }
            Write-Log "Restarting backend (attempt $restartCount of $maxRestarts)..."
            Stop-ListenersOnPort -Port $BackendPort
            $backendProc = [System.Diagnostics.Process]::Start($backendStartInfo)
            Write-Log "Backend restarted -> http://localhost:$BackendPort  (PID $($backendProc.Id))"
        }
    }

    Write-Host ""
    Write-Host "  Press Ctrl+C to stop the server."
    Write-Host ""

    try {
        while (-not $backendProc.HasExited) {
            Start-Sleep -Milliseconds 500
        }
        Write-Log "Backend exited (exit code $($backendProc.ExitCode))."
    } finally {
        Stop-ProcessTree -ProcessId $backendProc.Id
        Write-Log "Stopped."
    }
    Release-BackgroundLauncherMutex
    exit 0
}

# ---------------------------------------------------------------------------
# Dev mode: run backend (watch) + Vite frontend side by side
# ---------------------------------------------------------------------------
Stop-ListenersOnPort -Port $BackendPort

$dotnetProject = Join-Path $root "dashboard\backend-dotnet"
$backendStartInfo = New-Object System.Diagnostics.ProcessStartInfo
$backendStartInfo.FileName = "dotnet"
$backendStartInfo.Arguments = "watch run --project `"$dotnetProject`" --urls http://127.0.0.1:$BackendPort"
$backendStartInfo.WorkingDirectory = $root
$backendStartInfo.UseShellExecute = $false
if ($Background) { $backendStartInfo.CreateNoWindow = $true }
$backendStartInfo.EnvironmentVariables.Clear()
foreach ($key in $cleanEnv.Keys) { $backendStartInfo.EnvironmentVariables[$key] = $cleanEnv[$key] }
$backendProc = [System.Diagnostics.Process]::Start($backendStartInfo)

# Resolve npm.cmd — Start-Process needs a real executable, not a shell alias.
$npmCmd = (Get-Command npm -ErrorAction SilentlyContinue).Source
if ($npmCmd -and $npmCmd.EndsWith('.ps1')) {
    # nvm4w installs npm.ps1 alongside npm.cmd — Start-Process needs the .cmd
    $npmCmd = [System.IO.Path]::ChangeExtension($npmCmd, '.cmd')
}
if (-not $npmCmd -or -not (Test-Path $npmCmd)) { throw "npm not found in PATH. Install Node.js first." }

$env:VITE_BACKEND_PORT = $BackendPort
$frontendArgs = @{
    FilePath         = $npmCmd
    ArgumentList     = "run", "dev", "--", "--port", "$FrontendPort"
    WorkingDirectory = Join-Path $root "dashboard\frontend"
    PassThru         = $true
}
if ($Background) {
    $frontendArgs.WindowStyle = "Hidden"
} else {
    $frontendArgs.NoNewWindow = $true
}
$frontendProc = Start-Process @frontendArgs

Write-Log "DevOps InControl starting (dev mode)..."
Write-Log "Backend  -> http://127.0.0.1:$BackendPort  (PID $($backendProc.Id))"
Write-Log "Frontend -> http://localhost:$FrontendPort   (PID $($frontendProc.Id))"

if ($Background) {
    Write-Log "Background mode — exiting launcher."
    Release-BackgroundLauncherMutex
    exit 0
}

Write-Host ""
Write-Host "  Press Ctrl+C to stop both servers."
Write-Host ""

try {
    while ($true) {
        if ($backendProc.HasExited) {
            Write-Log "Backend exited unexpectedly (exit code $($backendProc.ExitCode))."
            break
        }
        if ($frontendProc.HasExited) {
            Write-Log "Frontend exited unexpectedly (exit code $($frontendProc.ExitCode))."
            break
        }
        Start-Sleep -Milliseconds 500
    }
}
finally {
    Write-Log "Shutting down — killing process trees..."
    Stop-ProcessTree -ProcessId $backendProc.Id
    Stop-ProcessTree -ProcessId $frontendProc.Id
    Write-Log "Stopped."
}

Release-BackgroundLauncherMutex
