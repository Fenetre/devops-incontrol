# Register-StartupTask.ps1
# Registers (or updates) a Windows Scheduled Task that launches the
# DevOps Backlog Monitor at user logon.
# Run this script once from an elevated (admin) PowerShell prompt.

$ErrorActionPreference = "Stop"

$taskName   = "DevOpsBacklogMonitor"
$root       = Split-Path -Parent $MyInvocation.MyCommand.Definition

# Resolve mapped drive letters to UNC paths so the task works before
# drive mappings are ready at logon.
$driveLetter = [System.IO.Path]::GetPathRoot($root).TrimEnd('\')
$disk = Get-CimInstance Win32_LogicalDisk -Filter "DeviceID='$driveLetter'" -ErrorAction SilentlyContinue
if ($disk -and $disk.ProviderName) {
    $root = $root -replace [regex]::Escape($driveLetter), $disk.ProviderName
    Write-Host "Resolved to UNC path: $root"
}

$startScript = Join-Path $root "start.ps1"

if (-not (Test-Path $startScript)) {
    Write-Error "start.ps1 not found at $startScript"
    exit 1
}

# ---------------------------------------------------------------------------
# Window-hiding strategy
#
# pwsh.exe is a console-subsystem binary, so Windows allocates a conhost
# window the instant it spawns. When Task Scheduler runs the task in an
# interactive session, -WindowStyle Hidden is honoured only after pwsh has
# finished starting up, and the scheduler tends to leave a minimised entry
# in the taskbar.
#
# S4U / "run whether user is logged on or not" would solve this completely
# but cannot authenticate to network shares — and the script lives on a
# remote drive — so we use the interactive logon instead and rely on
# start.ps1 calling ShowWindow(GetConsoleWindow(), SW_HIDE) on entry to
# remove the window (and its taskbar button) within milliseconds.
# ---------------------------------------------------------------------------

# Build the action: pwsh with -WindowStyle Hidden as a hint; start.ps1 will
# force-hide the console via Win32 ShowWindow as soon as it is loaded.
$action = New-ScheduledTaskAction `
    -Execute "pwsh.exe" `
    -Argument "-ExecutionPolicy Bypass -WindowStyle Hidden -NoProfile -NonInteractive -File `"$startScript`" -Background" `
    -WorkingDirectory $root

# Trigger: at logon of the current user, with a 30-second delay to let
# networking come up.
$currentUser = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $currentUser
$trigger.Delay = "PT30S"

# Settings — Hidden=true also hides the task entry in the Task Scheduler UI.
$settings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -StartWhenAvailable `
    -ExecutionTimeLimit ([TimeSpan]::Zero)   # no time limit
$settings.Hidden = $true

# Interactive principal — required so the task can read files from the
# user's mapped network drive. start.ps1 hides its own window on startup.
$principal = New-ScheduledTaskPrincipal `
    -UserId    $currentUser `
    -LogonType Interactive `
    -RunLevel  Limited

# Remove existing task if present, then register
if (Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue) {
    Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
    Write-Host "Removed existing '$taskName' task."
}

Register-ScheduledTask `
    -TaskName    $taskName `
    -Action      $action `
    -Trigger     $trigger `
    -Settings    $settings `
    -Principal   $principal `
    -Description "Starts the DevOps Backlog Monitor (backend + frontend) at logon." | Out-Null

Write-Host ""
Write-Host "Scheduled task '$taskName' registered successfully."
Write-Host "It will start automatically 30 seconds after you log in."
Write-Host "Start script : $startScript"
Write-Host "Log folder   : `%TEMP`%\DevOpsInControl\"
Write-Host ""
Write-Host "To remove:  Unregister-ScheduledTask -TaskName '$taskName' -Confirm:`$false"
