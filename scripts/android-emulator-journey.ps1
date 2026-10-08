<#
.SYNOPSIS
Runs the reproducible Android emulator journey and the T040 exploratory profile.

.DESCRIPTION
Automates the adb sequences that T038, T055, T057, T061 and T062 ran by hand on
AVD Medium_Phone_API_36.0. Results are emulator-only development evidence: they
make no physical-device, performance-qualification or release claim.

Journey steps (run in the order given by -Steps):
  Preflight    device, AVD, API, ABIs, native bridge, GLES renderer, fingerprint
  Install      adb install -r (-Apk), then pm clear unless -KeepData
  Launch       logcat -c, launcher start, PID, primary ABI, graphics device, load time
  Rotation     sensor/lock matrix (T055/T058): +x, lock 0, free, -x, +y; same PID,
               FULL_USER requested orientation throughout
  Home         HOME, departure checkpoint written, return in the same process,
               return checkpoint written
  Lock         POWER (asleep), departure checkpoint, WAKEUP + dismiss keyguard,
               same process, return checkpoint
  ColdRestart  force-stop, relaunch in a new process, reconciliation checkpoint
  LogScan      FATAL EXCEPTION / F DEBUG / SIGSEGV / [IdleFactory.Persistence] /
               E Unity counts in the captured logcat
  Profile      T040: for each of -Runs runs, restore the fixed checkpoint, launch,
               warm up -WarmupSeconds with no input, capture -CaptureSeconds of
               SurfaceFlinger frame statistics, process CPU time and PSS, then
               record the end state through a HOME departure checkpoint

Scenario F003 (-Scenario F003, T123) adds the Feature 003 steps below and defaults
-Steps to Preflight,Install,Launch,TouchBuild,RemoveConfirmRotation,
PrestigeConfirmRotation,W003Push,OfflineReturnTiming,ProducingFps,F003LogScan.
The default scenario keeps the step set above unchanged.
  TouchBuild              touchscreen feature, optional scripted build taps (-BuildTaps):
                          the Save V2 checkpoint gains a machine
  RemoveConfirmRotation   -RemoveTaps open the Remove confirmation, a rotation cancels it:
  PrestigeConfirmRotation -PrestigeTaps open the Prestige confirmation, same checks:
                          same process, nothing spent (machines, run number, prestige
                          count, cash not lower), no reconcile (offline summary) line
  W003Push                pushes the committed W003-S set (T138) into Checkpoints/v2 and
                          verifies its SHA-256 on the device against the committed hash
  OfflineReturnTiming     3 cold starts of W003-S (set re-pushed before each), parses the
                          Development-build line "ResumeAndReconcile ms=<n>" (T110), per
                          start and median, budget <= 2000 ms (PE-2); the departure is far
                          in the past so each start reconciles the capped eight hours;
                          production must continue afterwards (PE-6)
  ProducingFps            capture of W003-S between two fresh-run controls in the same
                          session (profiler-free build; budget >= 60 average FPS)
  F003LogScan             crash signatures, coded diagnostics and E Unity lines over the
                          logcat segments collected by the F003 steps

The emulator is a single shared resource. Do not run it while another session
uses the emulator, or while a Unity build or batch test run with Android as the
active build target is running: those end with "Killing ADB server". The script
fails the run when the adb server restarts under it.

Touch interaction (select, place, save) depends on the HUD layout and is not
scripted; the Play Mode suites own it. Allocations/GC and asset cost need the
Unity Profiler and are reported as unavailable, never as zero.

Every step writes screenshots, raw command output and pulled checkpoints below
-OutDir, plus journey.md (summary table) and journey.json. The script exits 1
when any step FAILs.

.PARAMETER Apk
APK to install. Build it with IdleFactory.Editor.PlatformBuildCommands.BuildAndroidArm64DevelopmentGles3.

.PARAMETER Scenario
Default (the T038-T062 journey, unchanged) or F003 (Feature 003, T123). Run for T124:
-Scenario F003 -Apk <f003-dev.apk> -LeaseOwner issue-<n>.

.PARAMETER BuildTaps
F003 TouchBuild: screen taps that build one machine, as "x,y" pairs (values <= 1 are
fractions of the current display, larger values pixels). Layout dependent, so the
operator supplies them from the T124 screenshots; without them the build is INFO only.

.PARAMETER RemoveTaps
F003 RemoveConfirmRotation: taps that open the Remove confirmation (same format).

.PARAMETER PrestigeTaps
F003 PrestigeConfirmRotation: taps that open the Prestige confirmation (same format).

.PARAMETER W003Fixture
Directory holding the committed W003-S checkpoint set (default: the T138 device fixture).

.PARAMETER ProductionSeconds
F003 OfflineReturnTiming: seconds to let W003-S run after the third start.

.PARAMETER RestoreCheckpoint
Profile start state: a directory holding factory-checkpoint.* files (a pulled
Checkpoints/v1 directory). Without it, Profile snapshots the device's current
checkpoint once and reuses it for every run.

.PARAMETER Boot
Cold-boot the AVD with host GPU when no device is attached (see the Android
emulator validation wiki page; quick boot restores SwiftShader and breaks Unity).

.EXAMPLE
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/android-emulator-journey.ps1 -Apk test-results/android/t062/CoreGameFoundation-t062-gles3.apk

.EXAMPLE
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/android-emulator-journey.ps1 -Steps Preflight,Profile -RestoreCheckpoint test-results/android/t040/start-checkpoint
#>
[CmdletBinding()]
param(
    [string] $Apk,

    # Comma-separated when called through powershell -File, e.g. Preflight,Profile.
    [string[]] $Steps = @('Preflight', 'Install', 'Launch', 'Rotation', 'Home', 'Lock', 'ColdRestart', 'LogScan'),

    [string] $Package = 'com.DefaultCompany.idlefactorygame',
    [string] $Avd = 'Medium_Phone_API_36.0',
    [string] $Serial,
    [string] $OutDir,
    [string] $AdbPath = (Join-Path $env:LOCALAPPDATA 'Android\Sdk\platform-tools\adb.exe'),
    [string] $EmulatorPath = (Join-Path $env:LOCALAPPDATA 'Android\Sdk\emulator\emulator.exe'),

    [switch] $KeepData,
    [switch] $Boot,

    # Lease holder for the shared emulator (scripts/resource-lease.ps1): the MAQA
    # issue ("issue-40") or session name that owns this run. A lease already held
    # by this owner is renewed; a lease held by anyone else stops the run.
    [string] $LeaseOwner = "$env:USERNAME-journey-$PID",

    [int] $AwaySeconds = 10,
    [int] $StartupTimeoutSeconds = 180,
    [string] $RestoreCheckpoint,
    [int] $Runs = 3,
    [int] $WarmupSeconds = 30,
    [int] $CaptureSeconds = 60,

    # T123: Default keeps the original journey; F003 adds the Feature 003 steps.
    [string] $Scenario = 'Default',
    [string[]] $BuildTaps = @(),
    [string[]] $RemoveTaps = @(),
    [string[]] $PrestigeTaps = @(),
    [string] $W003Fixture,
    [int] $ProductionSeconds = 20
)

$ErrorActionPreference = 'Stop'
$knownSteps = 'Preflight', 'Install', 'Launch', 'Rotation', 'Home', 'Lock', 'ColdRestart', 'LogScan', 'Profile'
$knownScenarios = 'Default', 'F003'
if ($knownScenarios -notcontains $Scenario) {
    throw "Unknown scenario '$Scenario'. Known scenarios: $($knownScenarios -join ', ')."
}
if ($Scenario -eq 'F003') {
    $knownSteps = @($knownSteps) + @('TouchBuild', 'RemoveConfirmRotation', 'PrestigeConfirmRotation', 'W003Push', 'OfflineReturnTiming', 'ProducingFps', 'F003LogScan')
    if (-not $PSBoundParameters.ContainsKey('Steps')) {
        $Steps = 'Preflight', 'Install', 'Launch', 'TouchBuild', 'RemoveConfirmRotation', 'PrestigeConfirmRotation', 'W003Push', 'OfflineReturnTiming', 'ProducingFps', 'F003LogScan'
    }
}
$Steps = @($Steps | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
foreach ($step in $Steps) {
    if ($knownSteps -notcontains $step) {
        throw "Unknown step '$step'. Known steps: $($knownSteps -join ', ')."
    }
}
$repositoryRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutDir) {
    $OutDir = Join-Path $repositoryRoot ("test-results/android/journey-" + (Get-Date -Format 'yyyyMMddTHHmmss'))
}
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$OutDir = (Resolve-Path $OutDir).Path
if (-not (Test-Path -LiteralPath $AdbPath)) {
    throw "adb not found at $AdbPath. Pass -AdbPath."
}

$deviceCheckpointDir = "/sdcard/Android/data/$Package/files/IdleFactory/Checkpoints/v1"
$results = New-Object System.Collections.Generic.List[object]
$environment = [ordered]@{}
$script:shotIndex = 0
$script:adbRestarts = 0

# ---------------------------------------------------------------- helpers

function Invoke-Adb {
    # Runs adb against the selected device; returns trimmed text output.
    # Deliberately a simple function: adb flags such as -c or -p must reach adb
    # as strings instead of binding to PowerShell parameters. A literal
    # -AllowFailure argument suppresses the failure exception.
    $AllowFailure = $false
    $Arguments = @(foreach ($a in $args) { if ("$a" -eq '-AllowFailure') { $AllowFailure = $true } else { "$a" } })
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $deviceArgs = if ($script:Serial) { @('-s', $script:Serial) } else { @() }
        $output = & $AdbPath @deviceArgs @Arguments 2>&1 | ForEach-Object { "$_" }
        $code = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previous
    }
    $text = ($output -join "`n").Trim()
    if ($text -match 'daemon not running; starting now') {
        # Something killed the adb server mid-run, typically a Unity build or
        # batch test run whose active target is Android ("Killing ADB server").
        # Device reads around that moment are unreliable, so the run fails.
        $script:adbRestarts++
        $results.Add([pscustomobject]@{ Step = 'Environment'; Check = 'adb server stable'; Result = 'FAIL'; Detail = "adb server restarted during: adb $($Arguments -join ' '). Another process killed it (for example a Unity run with Android as active target); discard this run." })
        Write-Host "[FAIL] Environment / adb server stable: restarted during adb $($Arguments -join ' ')"
    }
    if ($code -ne 0 -and -not $AllowFailure) {
        throw "adb $($Arguments -join ' ') failed ($code): $text"
    }
    return $text
}

function Add-Result {
    param([string] $Step, [string] $Check, [ValidateSet('PASS', 'FAIL', 'INFO')] [string] $Result, [string] $Detail)
    $results.Add([pscustomobject]@{ Step = $Step; Check = $Check; Result = $Result; Detail = $Detail })
    # Write-Host keeps progress out of function return values.
    Write-Host ("[{0}] {1} / {2}: {3}" -f $Result, $Step, $Check, $Detail)
}

function Assert-Check {
    param([string] $Step, [string] $Check, [bool] $Condition, [string] $Detail)
    Add-Result -Step $Step -Check $Check -Result $(if ($Condition) { 'PASS' } else { 'FAIL' }) -Detail $Detail
}

function Save-Text {
    param([string] $Name, [string] $Text)
    $path = Join-Path $OutDir $Name
    [IO.File]::WriteAllText($path, $Text, (New-Object System.Text.UTF8Encoding($false)))
    return $path
}

function Save-Screenshot {
    param([string] $Label)
    $script:shotIndex++
    $name = '{0:D2}-{1}.png' -f $script:shotIndex, $Label
    Invoke-Adb shell screencap -p /sdcard/journey-shot.png | Out-Null
    Invoke-Adb pull /sdcard/journey-shot.png (Join-Path $OutDir $name) | Out-Null
    Invoke-Adb shell rm -f /sdcard/journey-shot.png | Out-Null
    return $name
}

function Get-AppPid {
    $value = Invoke-Adb shell pidof $Package -AllowFailure
    if ($value -match '^\d+$') { return [int] $value }
    return $null
}

function Start-App {
    Invoke-Adb shell monkey -p $Package -c android.intent.category.LAUNCHER 1 | Out-Null
    for ($i = 0; $i -lt 40; $i++) {
        $appPid = Get-AppPid
        if ($appPid) { return $appPid }
        Start-Sleep -Milliseconds 500
    }
    return $null
}

function Get-DisplayState {
    $window = Invoke-Adb shell dumpsys window displays
    $rotation = if ($window -match 'mCurrentRotation=(ROTATION_\d+)') { $Matches[1] } else { 'unknown' }
    $size = if ($window -match '\bcur=(\d+x\d+)') { $Matches[1] } else { 'unknown' }

    # requestedOrientation of this package's ActivityRecord.
    $requested = 'unknown'
    # Summary lines (mResumedActivity, mFocusedApp) also name the record, so try
    # every mention until one is followed by its requestedOrientation.
    $lines = (Invoke-Adb shell dumpsys activity activities) -split "`n"
    for ($i = 0; $i -lt $lines.Count -and $requested -eq 'unknown'; $i++) {
        if ($lines[$i] -match 'ActivityRecord\{' -and $lines[$i] -match [regex]::Escape("$Package/")) {
            for ($j = $i + 1; $j -lt [Math]::Min($i + 40, $lines.Count); $j++) {
                if ($lines[$j] -match 'ActivityRecord\{') { break }
                if ($lines[$j] -match 'requestedOrientation=(\S+)') { $requested = $Matches[1]; break }
            }
        }
    }
    return [pscustomobject]@{ Rotation = $rotation; Size = $size; Requested = $requested }
}

function Get-StateFingerprint {
    # Canonical, order-independent description of a Save V1 factory.
    param($Data)
    $parts = foreach ($b in @($Data.factory.buildings | Sort-Object instanceId)) {
        $inputs = (@($b.inputQuantities | Sort-Object resourceId | ForEach-Object { "$($_.resourceId)=$($_.quantity)" }) -join ',')
        $outputs = (@($b.outputQuantities | Sort-Object resourceId | ForEach-Object { "$($_.resourceId)=$($_.quantity)" }) -join ',')
        $progress = if ($b.production) { "$($b.production.recipeId)@$($b.production.progressTicks)" } else { 'none' }
        "$($b.instanceId)|$($b.definitionId)|$($b.anchorX),$($b.anchorY)|r$($b.orientationQuarterTurns)|active=$($b.isActive)|in[$inputs]|out[$outputs]|$progress"
    }
    $canonical = "grid=$($Data.factory.originX),$($Data.factory.originY),$($Data.factory.width)x$($Data.factory.height)`n" + ($parts -join "`n")
    $sha = [Security.Cryptography.SHA256]::Create()
    $hash = ($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($canonical)) | ForEach-Object { $_.ToString('x2') }) -join ''
    return [pscustomobject]@{ Hash = $hash.Substring(0, 16); Canonical = $canonical }
}

function Get-DeviceCheckpoint {
    # Pulls Checkpoints/v1 and describes the highest-generation data file
    # (the active one after every promotion, including previous-recovery).
    param([string] $Label)
    $target = Join-Path $OutDir "checkpoints/$Label"
    # The running game may replace a file between `ls` and `pull`, or a pulled
    # file may be mid-write; take a fresh, complete snapshot up to three times.
    $best = $null
    for ($attempt = 1; $attempt -le 3 -and -not $best; $attempt++) {
        if (Test-Path -LiteralPath $target) { Remove-Item -Recurse -Force -LiteralPath $target }
        New-Item -ItemType Directory -Force -Path $target | Out-Null
        $listing = Invoke-Adb shell ls $deviceCheckpointDir -AllowFailure
        if ($listing -notmatch 'data-[ab]\.json') {
            # Only an empty or missing directory means "no checkpoint"; an adb
            # error (offline device, killed server) must not read as one.
            if (-not $listing -or $listing -match 'No such file or directory') {
                return $null
            }
            throw "Cannot list device checkpoints: $listing"
        }
        $complete = $true
        foreach ($name in ($listing -split "\s+" | Where-Object { $_ -like 'factory-checkpoint.*' })) {
            $pulled = Invoke-Adb pull "$deviceCheckpointDir/$name" (Join-Path $target $name) -AllowFailure
            if ($pulled -notmatch '1 file pulled') { $complete = $false }
        }
        try {
            foreach ($file in Get-ChildItem -LiteralPath $target -Filter 'factory-checkpoint.data-*.json') {
                $data = Get-Content -Raw -LiteralPath $file.FullName | ConvertFrom-Json
                if (-not $best -or [long] $data.generation -gt [long] $best.Data.generation) {
                    $best = [pscustomobject]@{ File = $file.Name; Data = $data }
                }
            }
        }
        catch {
            $best = $null
        }
        if (-not $complete) { $best = $null }
        if (-not $best) { Start-Sleep -Milliseconds 500 }
    }
    if (-not $best) { return $null }
    $fingerprint = Get-StateFingerprint -Data $best.Data
    Save-Text -Name "checkpoints/$Label/state.txt" -Text $fingerprint.Canonical | Out-Null
    return [pscustomobject]@{
        Generation   = [long] $best.Data.generation
        CheckpointId = $best.Data.checkpointId
        Buildings    = @($best.Data.factory.buildings).Count
        File         = $best.File
        Fingerprint  = $fingerprint.Hash
    }
}

function Format-Checkpoint {
    param($Checkpoint)
    if (-not $Checkpoint) { return 'none' }
    return "gen $($Checkpoint.Generation), id $($Checkpoint.CheckpointId.Substring(0, 8)), $($Checkpoint.Buildings) building(s), state $($Checkpoint.Fingerprint)"
}

function Wait-Checkpoint {
    # Waits until the device holds a checkpoint newer than $After (or any, if $After is null).
    param($After, [string] $Label, [int] $TimeoutSeconds = 45)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        $current = Get-DeviceCheckpoint -Label $Label
        if ($current -and (-not $After -or $current.Generation -gt $After.Generation)) { return $current }
        Start-Sleep -Seconds 1
    } while ((Get-Date) -lt $deadline)
    return $current
}

function Wait-UnityReady {
    # Unity creates its graphics device 10-70 s after process start under the
    # ARM64 translation layer (slowest right after a fresh install). Waits for
    # that line from this process only; returns the elapsed seconds or $null.
    param([int] $ProcessId)
    $started = Get-Date
    $deadline = $started.AddSeconds($StartupTimeoutSeconds)
    do {
        $log = Invoke-Adb logcat -d --pid $ProcessId -AllowFailure
        if ($log -match 'Creating (OpenGL ES [\d.]+|Vulkan)') {
            return [pscustomobject]@{ Seconds = [Math]::Round(((Get-Date) - $started).TotalSeconds) }
        }
        Start-Sleep -Seconds 2
    } while ((Get-Date) -lt $deadline)
    return $null
}

function Set-Sensor {
    param([string] $Vector)
    Invoke-Adb emu sensor set acceleration $Vector | Out-Null
    Start-Sleep -Seconds 3
}

# ---------------------------------------------------------------- steps

function Start-AdbServer {
    # Start the adb daemon detached. A daemon started implicitly by a piped adb
    # call inherits the pipe and keeps the caller waiting forever; Start-Process
    # does not pass redirected handles on, and WaitForExit() waits only for the
    # launcher, not the daemon it leaves behind.
    $process = Start-Process -FilePath $AdbPath -ArgumentList 'start-server' -WindowStyle Hidden -PassThru
    [void] $process.WaitForExit(30000)
}

function Get-AttachedDevices {
    return @((& $AdbPath devices 2>$null) | Select-Object -Skip 1 | Where-Object { $_ -match '\tdevice$' } | ForEach-Object { ($_ -split "`t")[0] })
}

function Invoke-Preflight {
    Start-AdbServer
    # After a daemon restart a running emulator reconnects within a few seconds.
    $devices = @(Get-AttachedDevices)
    for ($i = 0; $i -lt 15 -and $devices.Count -eq 0; $i++) {
        Start-Sleep -Seconds 2
        $devices = @(Get-AttachedDevices)
    }
    if ($devices.Count -eq 0 -and $Boot) {
        Write-Host "No device attached; cold-booting $Avd with host GPU."
        Start-Process -FilePath $EmulatorPath -ArgumentList @('-avd', $Avd, '-gpu', 'host', '-no-snapshot-load', '-no-snapshot-save') -WindowStyle Minimized | Out-Null
        for ($i = 0; $i -lt 90 -and $devices.Count -eq 0; $i++) {
            Start-Sleep -Seconds 2
            $devices = @(Get-AttachedDevices)
        }
    }
    if (-not $script:Serial) {
        if ($devices.Count -ne 1) {
            Assert-Check 'Preflight' 'single device attached' $false "found $($devices.Count); pass -Serial or -Boot"
            return $false
        }
        $script:Serial = $devices[0]
    }
    for ($i = 0; $i -lt 150 -and (Invoke-Adb shell getprop sys.boot_completed -AllowFailure) -ne '1'; $i++) {
        Start-Sleep -Seconds 2
    }
    $booted = (Invoke-Adb shell getprop sys.boot_completed -AllowFailure) -eq '1'
    Assert-Check 'Preflight' 'device booted' $booted $script:Serial

    $environment.Serial = $script:Serial
    $environment.Avd = ((Invoke-Adb emu avd name -AllowFailure) -split "`n")[0].Trim()
    $environment.Fingerprint = Invoke-Adb shell getprop ro.build.fingerprint
    $environment.Api = Invoke-Adb shell getprop ro.build.version.sdk
    $environment.Abis = Invoke-Adb shell getprop ro.product.cpu.abilist
    $environment.NativeBridge = Invoke-Adb shell getprop ro.dalvik.vm.native.bridge
    $surfaceFlinger = Invoke-Adb shell dumpsys SurfaceFlinger
    $environment.Gles = if ($surfaceFlinger -match '(?m)^GLES:\s*(.+)$') { $Matches[1].Trim() } else { 'unknown' }
    $environment.Display = (Get-DisplayState).Size
    Assert-Check 'Preflight' 'expected AVD' ($environment.Avd -eq $Avd) "$($environment.Avd), API $($environment.Api)"
    Assert-Check 'Preflight' 'arm64-v8a via translation' (($environment.Abis -match 'arm64-v8a') -and $environment.NativeBridge) "$($environment.Abis); bridge $($environment.NativeBridge)"
    Add-Result 'Preflight' 'GLES renderer' 'INFO' $environment.Gles
    return $booted
}

function Invoke-Install {
    if (-not $Apk) { throw 'Install needs -Apk.' }
    $apkPath = (Resolve-Path $Apk).Path
    $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $apkPath).Hash.ToLowerInvariant()
    $environment.Apk = "$apkPath ($((Get-Item $apkPath).Length) bytes, SHA-256 $hash)"
    # Unity Android builds restart the adb daemon; make sure it is back first.
    Start-AdbServer
    for ($i = 0; $i -lt 30 -and @(Get-AttachedDevices) -notcontains $script:Serial; $i++) { Start-Sleep -Seconds 2 }
    $install = Invoke-Adb install -r $apkPath -AllowFailure
    Assert-Check 'Install' 'adb install -r' ($install -match 'Success') ((($install -split "`n") | Select-Object -Last 1))
    if (-not $KeepData) {
        $clear = Invoke-Adb shell pm clear $Package -AllowFailure
        Assert-Check 'Install' 'pm clear' ($clear -match 'Success') $clear
    }
}

function Invoke-Launch {
    Invoke-Adb logcat -c | Out-Null
    $script:appPid = Start-App
    Assert-Check 'Launch' 'process started' ([bool] $script:appPid) "PID $script:appPid"
    $ready = Wait-UnityReady -ProcessId $script:appPid
    $log = Invoke-Adb logcat -d
    Save-Text -Name 'launch-logcat.txt' -Text $log | Out-Null
    Add-Result 'Launch' 'time to graphics device' 'INFO' $(if ($ready) { "$($ready.Seconds) s after process start" } else { "not within $StartupTimeoutSeconds s" })
    $packageInfo = Invoke-Adb shell dumpsys package $Package
    $abi = if ($packageInfo -match 'primaryCpuAbi=(\S+)') { $Matches[1] } else { 'unknown' }
    Assert-Check 'Launch' 'primary ABI' ($abi -eq 'arm64-v8a') $abi
    $build = if ($log -match "Build type '([^']+)', Scripting Backend '([^']+)', CPU '([^']+)'") { "$($Matches[1]), $($Matches[2]), $($Matches[3])" } else { 'unknown' }
    $environment.UnityBuild = $build
    Add-Result 'Launch' 'Unity build' 'INFO' $build
    $graphics = if ($log -match 'Creating (OpenGL ES [\d.]+|Vulkan)[^\r\n]*') { $Matches[0].Trim() } else { $null }
    Assert-Check 'Launch' 'graphics device' ([bool] $graphics) $(if ($graphics) { $graphics } else { 'no graphics device line in logcat' })
    $displayed = if ($log -match "Displayed $([regex]::Escape($Package))/\S+ for[^\r\n]*?\+(\S+)|Displayed $([regex]::Escape($Package))/\S+:? \+(\S+)") { @($Matches[1], $Matches[2] | Where-Object { $_ })[0] } else { 'unavailable' }
    Add-Result 'Launch' 'time to first display' 'INFO' $displayed
    $display = Get-DisplayState
    Add-Result 'Launch' 'display' 'INFO' "$($display.Rotation) $($display.Size), requested $($display.Requested)"
    Add-Result 'Launch' 'screenshot' 'INFO' (Save-Screenshot 'launch')
}

function Invoke-Rotation {
    $startPid = Get-AppPid
    Invoke-Adb shell cmd window user-rotation free | Out-Null
    Set-Sensor '0:9.81:0'
    $matrix = @(
        @{ Name = 'sensor +x'; Action = { Set-Sensor '9.81:0:0' }; Expect = 'ROTATION_90' },
        @{ Name = 'user-rotation lock 0 with sensor +x'; Action = { Invoke-Adb shell cmd window user-rotation lock 0 | Out-Null; Start-Sleep -Seconds 3 }; Expect = 'ROTATION_0' },
        @{ Name = 'user-rotation free'; Action = { Invoke-Adb shell cmd window user-rotation free | Out-Null; Start-Sleep -Seconds 3 }; Expect = 'ROTATION_90' },
        @{ Name = 'sensor -x'; Action = { Set-Sensor '-9.81:0:0' }; Expect = 'ROTATION_270' },
        @{ Name = 'sensor +y'; Action = { Set-Sensor '0:9.81:0' }; Expect = 'ROTATION_0' }
    )
    $log = New-Object System.Text.StringBuilder
    foreach ($case in $matrix) {
        & $case.Action
        $state = Get-DisplayState
        [void] $log.AppendLine("$($case.Name): $($state.Rotation) $($state.Size) requested=$($state.Requested)")
        $ok = $state.Rotation -eq $case.Expect -and $state.Requested -eq 'SCREEN_ORIENTATION_FULL_USER'
        Assert-Check 'Rotation' $case.Name $ok "$($state.Rotation) $($state.Size), requested $($state.Requested) (expected $($case.Expect), FULL_USER)"
        if ($case.Name -eq 'sensor +x') { Add-Result 'Rotation' 'screenshot' 'INFO' (Save-Screenshot 'landscape') }
    }
    Save-Text -Name 'rotation-matrix.txt' -Text $log.ToString() | Out-Null
    $endPid = Get-AppPid
    Assert-Check 'Rotation' 'same process throughout' ($startPid -and $startPid -eq $endPid) "PID $startPid -> $endPid"
}

function Invoke-Home {
    $before = Get-DeviceCheckpoint -Label 'home-0-before'
    $startPid = Get-AppPid
    Invoke-Adb shell input keyevent KEYCODE_HOME | Out-Null
    $departure = Wait-Checkpoint -After $before -Label 'home-1-departure'
    Assert-Check 'Home' 'departure checkpoint written' ($departure -and (-not $before -or $departure.Generation -gt $before.Generation)) ("{0} -> {1}" -f (Format-Checkpoint $before), (Format-Checkpoint $departure))
    Start-Sleep -Seconds $AwaySeconds
    $resumedPid = Start-App
    Assert-Check 'Home' 'return in the same process' ($startPid -and $resumedPid -eq $startPid) "PID $startPid -> $resumedPid"
    $return = Wait-Checkpoint -After $departure -Label 'home-2-return'
    Assert-Check 'Home' 'return checkpoint written' ($return -and $departure -and $return.Generation -gt $departure.Generation) (Format-Checkpoint $return)
    Start-Sleep -Seconds 2
    Add-Result 'Home' 'screenshot (offline return summary)' 'INFO' (Save-Screenshot 'home-return')
}

function Invoke-Lock {
    $before = Get-DeviceCheckpoint -Label 'lock-0-before'
    $startPid = Get-AppPid
    Invoke-Adb shell input keyevent KEYCODE_POWER | Out-Null
    $wakefulness = 'unknown'
    for ($i = 0; $i -lt 10; $i++) {
        Start-Sleep -Seconds 1
        if ((Invoke-Adb shell dumpsys power) -match 'mWakefulness=(\w+)') { $wakefulness = $Matches[1] }
        if ($wakefulness -in 'Asleep', 'Dozing') { break }
    }
    Assert-Check 'Lock' 'device asleep' ($wakefulness -in 'Asleep', 'Dozing') "mWakefulness=$wakefulness"
    $departure = Wait-Checkpoint -After $before -Label 'lock-1-departure'
    Assert-Check 'Lock' 'departure checkpoint written' ($departure -and (-not $before -or $departure.Generation -gt $before.Generation)) (Format-Checkpoint $departure)
    Start-Sleep -Seconds $AwaySeconds
    Invoke-Adb shell input keyevent KEYCODE_WAKEUP | Out-Null
    Invoke-Adb shell wm dismiss-keyguard | Out-Null
    Start-Sleep -Seconds 3
    $awake = (Invoke-Adb shell dumpsys power) -match 'mWakefulness=Awake'
    $resumedPid = Get-AppPid
    Assert-Check 'Lock' 'awake in the same process' ($awake -and $startPid -and $resumedPid -eq $startPid) "awake=$awake, PID $startPid -> $resumedPid"
    $return = Wait-Checkpoint -After $departure -Label 'lock-2-return'
    Assert-Check 'Lock' 'return checkpoint written' ($return -and $departure -and $return.Generation -gt $departure.Generation) (Format-Checkpoint $return)
    Add-Result 'Lock' 'screenshot' 'INFO' (Save-Screenshot 'lock-return')
}

function Invoke-ColdRestart {
    $before = Get-DeviceCheckpoint -Label 'cold-0-before'
    $oldPid = Get-AppPid
    Invoke-Adb shell am force-stop $Package | Out-Null
    Start-Sleep -Seconds 1
    Assert-Check 'ColdRestart' 'process stopped' (-not (Get-AppPid)) "PID $oldPid gone"
    $newPid = Start-App
    Assert-Check 'ColdRestart' 'new process' ($newPid -and $newPid -ne $oldPid) "PID $oldPid -> $newPid"
    $ready = Wait-UnityReady -ProcessId $newPid
    Add-Result 'ColdRestart' 'time to graphics device' 'INFO' $(if ($ready) { "$($ready.Seconds) s" } else { "not within $StartupTimeoutSeconds s" })
    $restored = Wait-Checkpoint -After $before -Label 'cold-1-restored' -TimeoutSeconds 30
    if ($before) {
        Assert-Check 'ColdRestart' 'reconciled from the last durable checkpoint' ($restored -and $restored.Generation -gt $before.Generation) (Format-Checkpoint $restored)
        Assert-Check 'ColdRestart' 'building set restored' ($restored -and $restored.Buildings -eq $before.Buildings) "$($before.Buildings) -> $($restored.Buildings) building(s)"
    }
    else {
        Add-Result 'ColdRestart' 'reconciled checkpoint' 'INFO' 'no checkpoint before the restart'
    }
    Start-Sleep -Seconds 2
    Add-Result 'ColdRestart' 'screenshot (restored factory before any input)' 'INFO' (Save-Screenshot 'cold-restart')
}

function Invoke-LogScan {
    $log = Invoke-Adb logcat -d
    Save-Text -Name 'logcat.txt' -Text $log | Out-Null
    $lines = $log -split "`n"
    $count = { param($pattern) @($lines | Where-Object { $_ -match $pattern }).Count }
    $fatal = & $count 'FATAL EXCEPTION|\sF DEBUG\s|SIGSEGV'
    $persistence = & $count '\[IdleFactory\.Persistence\]'
    $unityErrors = @($lines | Where-Object { $_ -match '\sE Unity\s' })
    Assert-Check 'LogScan' 'no crash signatures' ($fatal -eq 0) "$fatal FATAL/F DEBUG/SIGSEGV line(s) in $($lines.Count)"
    Assert-Check 'LogScan' 'no persistence diagnostics' ($persistence -eq 0) "$persistence [IdleFactory.Persistence] line(s)"
    # Two per process are the known Development-build probes (Play Asset Delivery, Autoconnect Profiler).
    Add-Result 'LogScan' 'E Unity lines' 'INFO' "$($unityErrors.Count) (known benign: AssetPackManager and Autoconnect Profiler probes, two per process)"
}

function Get-ProcessCpuTicks {
    param([int] $ProcessId)
    $fields = (Invoke-Adb shell cat "/proc/$ProcessId/stat") -replace '^.*\)\s', '' -split '\s+'
    # After the comm field: index 11 = utime, 12 = stime (fields 14 and 15 of proc(5)).
    return [long] $fields[11] + [long] $fields[12]
}

function Get-TotalPss {
    $meminfo = Invoke-Adb shell dumpsys meminfo $Package
    if ($meminfo -match 'TOTAL PSS:\s+(\d+)') { return [long] $Matches[1] }
    if ($meminfo -match '(?m)^\s*TOTAL\s+(\d+)') { return [long] $Matches[1] }
    return $null
}

function Get-LayerTimeStats {
    param([string] $RawName)
    $dump = Invoke-Adb shell dumpsys SurfaceFlinger --timestats -dump
    if ($RawName) { Save-Text -Name $RawName -Text $dump | Out-Null }
    $blocks = $dump -split '(?=layerName = )'
    # A relaunch can leave an older SurfaceView layer of the package; take the busiest.
    $block = $blocks | Where-Object { $_ -match "^layerName = .*SurfaceView\[$([regex]::Escape($Package))" } |
        Sort-Object { if ($_ -match 'totalFrames = (\d+)') { [long] $Matches[1] } else { 0 } } -Descending |
        Select-Object -First 1
    if (-not $block) { return $null }
    $value = { param($name) if ($block -match "$name = ([\d.]+)") { $Matches[1] } else { $null } }
    return [pscustomobject]@{
        TotalFrames   = & $value 'totalFrames'
        DroppedFrames = & $value 'droppedFrames'
        AverageFps    = & $value 'averageFPS'
        Raw           = $block.Trim()
    }
}

function Invoke-Profile {
    # Fixed start checkpoint.
    $startDir = Join-Path $OutDir 'profile-start-checkpoint'
    if ($RestoreCheckpoint) {
        New-Item -ItemType Directory -Force -Path $startDir | Out-Null
        Copy-Item -Path (Join-Path (Resolve-Path $RestoreCheckpoint).Path 'factory-checkpoint.*') -Destination $startDir
    }
    else {
        $current = Get-DeviceCheckpoint -Label 'profile-source'
        if (-not $current) {
            Assert-Check 'Profile' 'start checkpoint' $false 'no -RestoreCheckpoint and no checkpoint on the device'
            return
        }
        New-Item -ItemType Directory -Force -Path $startDir | Out-Null
        Copy-Item -Path (Join-Path $OutDir 'checkpoints/profile-source/factory-checkpoint.*') -Destination $startDir
    }
    $startFiles = @(Get-ChildItem -LiteralPath $startDir -Filter 'factory-checkpoint.*' | Where-Object { $_.Name -notlike '*.pending.json' })
    $startHash = ($startFiles | Sort-Object Name | ForEach-Object { (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash.Substring(0, 12) }) -join '/'
    Add-Result 'Profile' 'start checkpoint files' 'INFO' "$($startFiles.Name -join ', ') (SHA-256 prefixes $startHash)"
    $clockTicks = [int] (Invoke-Adb shell getconf CLK_TCK)
    # The cold start reconciles the restored checkpoint into a newer generation.
    $pushedGeneration = (@(Get-ChildItem -LiteralPath $startDir -Filter 'factory-checkpoint.data-*.json' |
        ForEach-Object { [long] (Get-Content -Raw -LiteralPath $_.FullName | ConvertFrom-Json).generation }) | Measure-Object -Maximum).Maximum
    $restoredGeneration = [pscustomobject]@{ Generation = $pushedGeneration }

    $rows = New-Object System.Collections.Generic.List[object]
    for ($run = 1; $run -le $Runs; $run++) {
        $label = "run$run"
        Invoke-Adb shell am force-stop $Package | Out-Null
        Invoke-Adb shell rm -rf $deviceCheckpointDir | Out-Null
        Invoke-Adb shell mkdir -p $deviceCheckpointDir | Out-Null
        foreach ($file in $startFiles) {
            Invoke-Adb push $file.FullName "$deviceCheckpointDir/$($file.Name)" | Out-Null
        }
        Invoke-Adb logcat -c | Out-Null
        Invoke-Adb shell dumpsys SurfaceFlinger --timestats -enable | Out-Null
        $appPid = Start-App
        if (-not $appPid) {
            Assert-Check 'Profile' "$label launch" $false 'process did not start'
            continue
        }
        # The cold start reconciles the restored checkpoint and promotes the result:
        # that generation is the recorded start state (before warm-up).
        # Warm-up starts once Unity runs, not when the process exists.
        $ready = Wait-UnityReady -ProcessId $appPid
        $start = Wait-Checkpoint -After $restoredGeneration -Label "$label-start" -TimeoutSeconds 30
        Add-Result 'Profile' "$label startup" 'INFO' $(if ($ready) { "graphics device after $($ready.Seconds) s; start $(Format-Checkpoint $start)" } else { "no graphics device within $StartupTimeoutSeconds s" })
        Start-Sleep -Seconds $WarmupSeconds

        Invoke-Adb shell dumpsys SurfaceFlinger --timestats -clear | Out-Null
        $cpuStart = Get-ProcessCpuTicks -ProcessId $appPid
        $pssStart = Get-TotalPss
        $captureStarted = Get-Date
        Start-Sleep -Seconds $CaptureSeconds
        $elapsed = ((Get-Date) - $captureStarted).TotalSeconds
        $stats = Get-LayerTimeStats -RawName "$label-surfaceflinger-timestats-full.txt"
        $cpuEnd = Get-ProcessCpuTicks -ProcessId $appPid
        $pssEnd = Get-TotalPss
        Save-Screenshot "$label-capture-end" | Out-Null
        if ($stats) { Save-Text -Name "$label-surfaceflinger-timestats.txt" -Text $stats.Raw | Out-Null }
        Save-Text -Name "$label-meminfo-end.txt" -Text (Invoke-Adb shell dumpsys meminfo $Package) | Out-Null

        $sameProcess = (Get-AppPid) -eq $appPid
        Invoke-Adb shell input keyevent KEYCODE_HOME | Out-Null
        $end = Wait-Checkpoint -After $start -Label "$label-end"
        Save-Text -Name "$label-logcat.txt" -Text (Invoke-Adb logcat -d) | Out-Null
        Invoke-Adb shell am force-stop $Package | Out-Null

        $cpuPercent = ([Math]::Round(100.0 * ($cpuEnd - $cpuStart) / ($clockTicks * $elapsed), 1)).ToString([Globalization.CultureInfo]::InvariantCulture)
        $row = [pscustomobject]@{
            Run           = $run
            StartState    = $(if ($start) { $start.Fingerprint } else { 'unavailable' })
            StartCheckpoint = (Format-Checkpoint $start)
            EndState      = $(if ($end) { $end.Fingerprint } else { 'unavailable' })
            SameProcess   = $sameProcess
            CaptureSeconds = ([Math]::Round($elapsed, 1)).ToString([Globalization.CultureInfo]::InvariantCulture)
            Frames        = $(if ($stats) { $stats.TotalFrames } else { 'unavailable' })
            DroppedFrames = $(if ($stats) { $stats.DroppedFrames } else { 'unavailable' })
            AverageFps    = $(if ($stats) { $stats.AverageFps } else { 'unavailable' })
            CpuPercent    = $cpuPercent
            PssStartKb    = $(if ($pssStart) { $pssStart } else { 'unavailable' })
            PssEndKb      = $(if ($pssEnd) { $pssEnd } else { 'unavailable' })
        }
        $rows.Add($row)
        Assert-Check 'Profile' "$label captured" ($sameProcess -and [bool] $stats -and [bool] $start) ("start {0}; {1} frames, {2} dropped, {3} fps; CPU {4}%; PSS {5}->{6} KiB" -f
            $row.StartState, $row.Frames, $row.DroppedFrames, $row.AverageFps, $row.CpuPercent, $row.PssStartKb, $row.PssEndKb)
    }
    Invoke-Adb shell dumpsys SurfaceFlinger --timestats -disable | Out-Null

    $script:profileRows = $rows
    $startStates = @($rows | ForEach-Object { $_.StartState } | Sort-Object -Unique)
    Assert-Check 'Profile' 'capture-start states match' ($startStates.Count -eq 1 -and $startStates[0] -ne 'unavailable') $(if ($startStates.Count -eq 1) { "all runs start at state $($startStates[0])" } else { "states differ ($($startStates -join ', ')); report the runs but do not compare them" })
    Add-Result 'Profile' 'allocations/GC, asset cost' 'INFO' 'unavailable from adb; needs the Unity Profiler (Development build with Autoconnect Profiler)'
}

# ---------------------------------------------------------------- Scenario F003 (T123)

$deviceCheckpointDirV2 = "/sdcard/Android/data/$Package/files/IdleFactory/Checkpoints/v2"
# SHA-256 of the committed W003-S set (Fixtures/SaveV2/README.md, T138).
$w003Expected = [ordered]@{
    'run-checkpoint.data-a.json'    = '0467d293d69ed358ae1ec74d5b94192aaaf5f983f75cfad23bca6dd27a95ab73'
    'run-checkpoint.manifest-a.bin' = '84cfe0b4e72757432798c261d386de3994cd22fd656c7d1258f42de3501de7bb'
}
$w003CheckpointId = '22222222-2222-2222-2222-000000000010'
$w003DepartureUtcMs = 1700000000000
$reconcileBudgetMs = 2000
$fpsBudget = 60
$script:f003Logs = New-Object System.Text.StringBuilder

function Get-W003FixtureDir {
    if ($W003Fixture) { return (Resolve-Path $W003Fixture).Path }
    return (Join-Path $repositoryRoot 'Assets/IdleFactory/Tests/EditMode/Fixtures/SaveV2/device/w003-s')
}

function Add-LogSegment {
    # Keeps the logcat of a segment before a step clears it, for F003LogScan.
    [void] $script:f003Logs.AppendLine((Invoke-Adb logcat -d -AllowFailure))
}

function Get-DeviceCheckpointV2 {
    # Pulls Checkpoints/v2 and describes the data file with the highest generation.
    param([string] $Label)
    $target = Join-Path $OutDir "checkpoints/$Label"
    $best = $null
    for ($attempt = 1; $attempt -le 3 -and -not $best; $attempt++) {
        if (Test-Path -LiteralPath $target) { Remove-Item -Recurse -Force -LiteralPath $target }
        New-Item -ItemType Directory -Force -Path $target | Out-Null
        $listing = Invoke-Adb shell ls $deviceCheckpointDirV2 -AllowFailure
        if ($listing -notmatch 'run-checkpoint\.data-[ab]\.json') {
            if (-not $listing -or $listing -match 'No such file or directory') { return $null }
            throw "Cannot list device checkpoints: $listing"
        }
        $complete = $true
        foreach ($name in ($listing -split "\s+" | Where-Object { $_ -like 'run-checkpoint.*' })) {
            $pulled = Invoke-Adb pull "$deviceCheckpointDirV2/$name" (Join-Path $target $name) -AllowFailure
            if ($pulled -notmatch '1 file pulled') { $complete = $false }
        }
        try {
            foreach ($file in Get-ChildItem -LiteralPath $target -Filter 'run-checkpoint.data-*.json') {
                $data = Get-Content -Raw -LiteralPath $file.FullName | ConvertFrom-Json
                if (-not $best -or [long] $data.generation -gt [long] $best.Data.generation) {
                    $best = [pscustomobject]@{ File = $file.Name; Data = $data; Sha = (Get-FileHash -Algorithm SHA256 -LiteralPath $file.FullName).Hash.ToLowerInvariant() }
                }
            }
        }
        catch { $best = $null }
        if (-not $complete) { $best = $null }
        if (-not $best) { Start-Sleep -Milliseconds 500 }
    }
    if (-not $best) { return $null }
    $d = $best.Data
    return [pscustomobject]@{
        Generation     = [long] $d.generation
        CheckpointId   = [string] $d.checkpointId
        DepartureMs    = [long] $d.departureUtcMs
        ReconciledFrom = [string] $d.reconciledFromCheckpointId
        Cash           = [decimal] $d.run.cash
        Machines       = @($d.run.machines).Count
        RunNumber      = [long] $d.run.runNumber
        PrestigeCount  = [long] $d.permanent.prestigeCount
        File           = $best.File
        Sha            = $best.Sha
    }
}

function Format-CheckpointV2 {
    param($Checkpoint)
    if (-not $Checkpoint) { return 'none' }
    return "gen $($Checkpoint.Generation), id $($Checkpoint.CheckpointId.Substring(0, 8)), run $($Checkpoint.RunNumber), cash $($Checkpoint.Cash), $($Checkpoint.Machines) machine(s), prestige $($Checkpoint.PrestigeCount), data $($Checkpoint.Sha.Substring(0, 12))"
}

function Wait-CheckpointV2 {
    param($After, [string] $Label, [int] $TimeoutSeconds = 45)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        $current = Get-DeviceCheckpointV2 -Label $Label
        if ($current -and (-not $After -or $current.Generation -gt $After.Generation)) { return $current }
        Start-Sleep -Seconds 1
    } while ((Get-Date) -lt $deadline)
    return $current
}

function ConvertTo-TapPoints {
    # "x,y" items (or "x,y;x,y"); values <= 1 are fractions of the current display size.
    param([string[]] $Taps)
    $size = (Get-DisplayState).Size
    $w = 0; $h = 0
    if ($size -match '^(\d+)x(\d+)$') { $w = [int] $Matches[1]; $h = [int] $Matches[2] }
    foreach ($tap in @($Taps | ForEach-Object { $_ -split ';' } | Where-Object { $_ })) {
        $xy = $tap -split ','
        if ($xy.Count -ne 2) { throw "Tap '$tap' is not x,y." }
        $x = [double]::Parse($xy[0], [Globalization.CultureInfo]::InvariantCulture)
        $y = [double]::Parse($xy[1], [Globalization.CultureInfo]::InvariantCulture)
        if ($x -le 1) { $x = $x * $w }
        if ($y -le 1) { $y = $y * $h }
        [pscustomobject]@{ X = [int] [Math]::Round($x); Y = [int] [Math]::Round($y) }
    }
}

function Invoke-Taps {
    param([string[]] $Taps)
    foreach ($point in @(ConvertTo-TapPoints -Taps $Taps)) {
        Invoke-Adb shell input tap $point.X $point.Y | Out-Null
        Start-Sleep -Seconds 1
    }
}

function Invoke-TouchBuild {
    $features = Invoke-Adb shell pm list features
    Assert-Check 'TouchBuild' 'touchscreen feature' ($features -match 'android\.hardware\.touchscreen') 'android.hardware.touchscreen present'
    $display = Get-DisplayState
    Add-Result 'TouchBuild' 'display' 'INFO' "$($display.Rotation) $($display.Size), requested $($display.Requested)"
    $before = Get-DeviceCheckpointV2 -Label 'touch-0-before'
    Add-Result 'TouchBuild' 'screenshot before' 'INFO' (Save-Screenshot 'touch-before')
    if (-not @($BuildTaps).Count) {
        Add-Result 'TouchBuild' 'build by touch' 'INFO' 'skipped: no -BuildTaps supplied (layout dependent; T124 supplies them from the screenshots)'
        return
    }
    $startPid = Get-AppPid
    Invoke-Taps -Taps $BuildTaps
    $after = Wait-CheckpointV2 -After $before -Label 'touch-1-after'
    Add-Result 'TouchBuild' 'screenshot after' 'INFO' (Save-Screenshot 'touch-after')
    Assert-Check 'TouchBuild' 'machine built by touch' ($after -and $before -and $after.Machines -gt $before.Machines) ("{0} -> {1}" -f (Format-CheckpointV2 $before), (Format-CheckpointV2 $after))
    Assert-Check 'TouchBuild' 'same process' ($startPid -and (Get-AppPid) -eq $startPid) "PID $startPid"
}

function Invoke-ConfirmRotation {
    param([string] $Step, [string[]] $Taps)
    Invoke-Adb shell cmd window user-rotation free | Out-Null
    Set-Sensor '0:9.81:0'
    $before = Get-DeviceCheckpointV2 -Label "$Step-0-before"
    $startPid = Get-AppPid
    Add-LogSegment
    Invoke-Adb logcat -c | Out-Null
    if (-not @($Taps).Count) {
        Add-Result $Step 'confirmation cancelled by rotation' 'INFO' 'skipped: no taps supplied for this confirmation (layout dependent; T124 supplies them)'
        return
    }
    Invoke-Taps -Taps $Taps
    Add-Result $Step 'screenshot confirmation open' 'INFO' (Save-Screenshot "$Step-open")
    Set-Sensor '9.81:0:0'
    $state = Get-DisplayState
    Assert-Check $Step 'rotated to landscape' ($state.Rotation -eq 'ROTATION_90') "$($state.Rotation) $($state.Size), requested $($state.Requested)"
    Start-Sleep -Seconds 2
    Add-Result $Step 'screenshot after rotation (dialog cancelled, no offline summary)' 'INFO' (Save-Screenshot "$Step-rotated")
    $after = Get-DeviceCheckpointV2 -Label "$Step-1-after"
    $log = Invoke-Adb logcat -d
    Save-Text -Name "$Step-logcat.txt" -Text $log | Out-Null
    Assert-Check $Step 'same process' ($startPid -and (Get-AppPid) -eq $startPid) "PID $startPid"
    Assert-Check $Step 'no reconcile (no offline summary)' (-not ($log -match 'ResumeAndReconcile ms=')) 'no ResumeAndReconcile line while the dialog was open and rotated'
    if ($before -and $after) {
        Assert-Check $Step 'nothing spent' ($after.Machines -eq $before.Machines -and $after.RunNumber -eq $before.RunNumber -and $after.PrestigeCount -eq $before.PrestigeCount -and $after.Cash -ge $before.Cash) ("{0} -> {1}" -f (Format-CheckpointV2 $before), (Format-CheckpointV2 $after))
        Assert-Check $Step 'no new reconciliation source' ($after.ReconciledFrom -eq $before.ReconciledFrom) "reconciledFromCheckpointId '$($before.ReconciledFrom)' -> '$($after.ReconciledFrom)'"
    }
    else {
        Add-Result $Step 'nothing spent' 'FAIL' "no Save V2 checkpoint on the device to compare (before: $(Format-CheckpointV2 $before), after: $(Format-CheckpointV2 $after))"
    }
    Add-LogSegment
    Set-Sensor '0:9.81:0'
}

function Push-W003Set {
    # Replaces the device checkpoints with the committed W003-S set (V2 only).
    $dir = Get-W003FixtureDir
    Invoke-Adb shell am force-stop $Package | Out-Null
    Invoke-Adb shell rm -rf $deviceCheckpointDir | Out-Null
    Invoke-Adb shell rm -rf $deviceCheckpointDirV2 | Out-Null
    Invoke-Adb shell mkdir -p $deviceCheckpointDirV2 | Out-Null
    foreach ($name in $w003Expected.Keys) {
        Invoke-Adb push (Join-Path $dir $name) "$deviceCheckpointDirV2/$name" | Out-Null
    }
}

function Invoke-W003Push {
    $dir = Get-W003FixtureDir
    foreach ($name in $w003Expected.Keys) {
        $local = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $dir $name)).Hash.ToLowerInvariant()
        Assert-Check 'W003Push' "committed $name" ($local -eq $w003Expected[$name]) "SHA-256 $($local.Substring(0, 16))"
    }
    Push-W003Set
    foreach ($name in $w003Expected.Keys) {
        $device = Invoke-Adb shell sha256sum "$deviceCheckpointDirV2/$name" -AllowFailure
        $deviceHash = if ($device -match '^([0-9a-fA-F]{64})') { $Matches[1].ToLowerInvariant() } else { '' }
        Assert-Check 'W003Push' "device $name" ($deviceHash -eq $w003Expected[$name]) $(if ($deviceHash) { "SHA-256 $($deviceHash.Substring(0, 16)) on the device" } else { "no hash from the device: $device" })
    }
}

function Wait-ReconcileLine {
    param([int] $ProcessId)
    $deadline = (Get-Date).AddSeconds($StartupTimeoutSeconds + 60)
    do {
        $log = Invoke-Adb logcat -d --pid $ProcessId -AllowFailure
        if ($log -match 'ResumeAndReconcile ms=(\d+)') { return [long] $Matches[1] }
        Start-Sleep -Seconds 2
    } while ((Get-Date) -lt $deadline)
    return $null
}

function Invoke-OfflineReturnTiming {
    $deviceNow = Invoke-Adb shell date +%s
    if ($deviceNow -match '^\d+$') {
        $gapHours = ([double] $deviceNow * 1000 - $w003DepartureUtcMs) / 3600000
        Assert-Check 'OfflineReturnTiming' 'departure beyond the eight-hour cap' ($gapHours -gt 8) ("fixture departure is {0:N0} h before the device clock, so each start reconciles exactly the capped 8 h" -f $gapHours)
    }
    else {
        Add-Result 'OfflineReturnTiming' 'device clock' 'FAIL' "cannot read the device clock: $deviceNow"
    }
    $times = New-Object System.Collections.Generic.List[long]
    $last = $null
    $lastPid = $null
    for ($i = 1; $i -le 3; $i++) {
        Push-W003Set
        Add-LogSegment
        Invoke-Adb logcat -c | Out-Null
        $startPid = Start-App
        if (-not $startPid) { Add-Result 'OfflineReturnTiming' "start $i" 'FAIL' 'process did not start'; continue }
        $ms = Wait-ReconcileLine -ProcessId $startPid
        if ($null -eq $ms) {
            Add-Result 'OfflineReturnTiming' "start $i" 'FAIL' "no 'ResumeAndReconcile ms=' line within $($StartupTimeoutSeconds + 60) s (Development build required)"
            continue
        }
        $times.Add($ms)
        $cp = Wait-CheckpointV2 -After ([pscustomobject]@{ Generation = 0 }) -Label "reconcile-$i"
        $reconciled = [bool] ($cp -and $cp.ReconciledFrom -eq $w003CheckpointId)
        $ok = ($ms -le $reconcileBudgetMs) -and $reconciled
        $budgetNote = if ($ms -gt $reconcileBudgetMs) { " (over the $reconcileBudgetMs ms budget)" } else { '' }
        Add-Result 'OfflineReturnTiming' "start $i" $(if ($ok) { 'PASS' } else { 'FAIL' }) ("{0} ms{1}; reconciled from W003-S: {2}; {3}" -f $ms, $budgetNote, $reconciled, (Format-CheckpointV2 $cp))
        $last = $cp
        $lastPid = $startPid
    }
    if ($times.Count -eq 0) {
        Add-Result 'OfflineReturnTiming' 'median' 'FAIL' 'no start produced a reconcile line'
        return
    }
    $sorted = @($times | Sort-Object)
    if ($sorted.Count % 2 -eq 1) { $median = $sorted[($sorted.Count - 1) / 2] }
    else { $median = [Math]::Round(($sorted[$sorted.Count / 2 - 1] + $sorted[$sorted.Count / 2]) / 2) }
    Assert-Check 'OfflineReturnTiming' 'median of starts' ($times.Count -eq 3 -and $median -le $reconcileBudgetMs) ("median {0} ms of [{1}] (budget <= {2} ms, PE-2)" -f $median, ($times -join ', '), $reconcileBudgetMs)

    # W003-S keeps producing after the capped reconcile.
    if ($last -and $lastPid -and (Get-AppPid) -eq $lastPid) {
        $null = Wait-UnityReady -ProcessId $lastPid
        Start-Sleep -Seconds $ProductionSeconds
        Invoke-Adb shell input keyevent KEYCODE_HOME | Out-Null
        $later = Wait-CheckpointV2 -After $last -Label 'reconcile-production'
        Assert-Check 'OfflineReturnTiming' 'W003-S keeps producing' ($later -and $later.Generation -gt $last.Generation -and $later.Cash -gt $last.Cash) ("{0} -> {1}" -f (Format-CheckpointV2 $last), (Format-CheckpointV2 $later))
        Start-App | Out-Null
    }
    else {
        Add-Result 'OfflineReturnTiming' 'W003-S keeps producing' 'FAIL' 'the last start did not stay in one process'
    }
    Add-LogSegment
}

function Invoke-FpsCapture {
    param([string] $Label, [scriptblock] $Prepare)
    Invoke-Adb shell am force-stop $Package | Out-Null
    & $Prepare
    Add-LogSegment
    Invoke-Adb logcat -c | Out-Null
    Invoke-Adb shell dumpsys SurfaceFlinger --timestats -enable | Out-Null
    $fpsPid = Start-App
    if (-not $fpsPid) { Add-Result 'ProducingFps' "$Label launch" 'FAIL' 'process did not start'; return $null }
    $null = Wait-UnityReady -ProcessId $fpsPid
    Start-Sleep -Seconds $WarmupSeconds
    Invoke-Adb shell dumpsys SurfaceFlinger --timestats -clear | Out-Null
    $started = Get-Date
    Start-Sleep -Seconds $CaptureSeconds
    $elapsed = ((Get-Date) - $started).TotalSeconds
    $stats = Get-LayerTimeStats -RawName "fps-$Label-surfaceflinger-timestats-full.txt"
    Save-Screenshot "fps-$Label" | Out-Null
    if ($stats) { Save-Text -Name "fps-$Label-surfaceflinger-timestats.txt" -Text $stats.Raw | Out-Null }
    $log = Invoke-Adb logcat -d
    Save-Text -Name "fps-$Label-logcat.txt" -Text $log | Out-Null
    $sameProcess = (Get-AppPid) -eq $fpsPid
    $fps = $null
    if ($stats -and $stats.AverageFps) { $fps = [double]::Parse($stats.AverageFps, [Globalization.CultureInfo]::InvariantCulture) }
    $frames = if ($stats) { $stats.TotalFrames } else { 'unavailable' }
    $dropped = if ($stats) { $stats.DroppedFrames } else { 'unavailable' }
    $avg = if ($stats) { $stats.AverageFps } else { 'unavailable' }
    Add-Result 'ProducingFps' "$Label capture" 'INFO' ("{0} frames, {1} dropped, {2} fps over {3:N0} s; same process {4}" -f $frames, $dropped, $avg, $elapsed, $sameProcess)
    $profilerLines = @(($log -split "`n") | Where-Object { $_ -match 'Autoconnect Profiler' }).Count
    return [pscustomobject]@{ Fps = $fps; SameProcess = $sameProcess; ProfilerLines = $profilerLines }
}

function Invoke-ProducingFps {
    # Same-session control: a fresh, empty run (pm clear) before and after the W003-S capture.
    $control = { Invoke-Adb shell pm clear $Package | Out-Null }
    $producer = { Push-W003Set }
    $c1 = Invoke-FpsCapture -Label 'control-1' -Prepare $control
    $p = Invoke-FpsCapture -Label 'producing' -Prepare $producer
    $c2 = Invoke-FpsCapture -Label 'control-2' -Prepare $control
    Invoke-Adb shell dumpsys SurfaceFlinger --timestats -disable | Out-Null
    if (-not $p -or $null -eq $p.Fps) {
        Add-Result 'ProducingFps' 'producing average FPS' 'FAIL' 'no frame statistics for the producing capture'
        return
    }
    Assert-Check 'ProducingFps' 'profiler-free build' ($p.ProfilerLines -eq 0) "$($p.ProfilerLines) Autoconnect Profiler line(s) in the producing capture (the frame-rate build must not autoconnect the profiler)"
    Assert-Check 'ProducingFps' 'producing average FPS' ($p.Fps -ge $fpsBudget -and $p.SameProcess) ("{0} fps (budget >= {1}, owner decision 2026-09-29)" -f $p.Fps, $fpsBudget)
    $controls = @(@($c1, $c2) | Where-Object { $_ -and $null -ne $_.Fps } | ForEach-Object { $_.Fps })
    if ($controls.Count) {
        $mean = [Math]::Round(($controls | Measure-Object -Average).Average, 1)
        Add-Result 'ProducingFps' 'same-session control (context)' 'INFO' "fresh-run control mean $mean fps (controls: $($controls -join ', ')); producing $($p.Fps) fps"
    }
    else {
        Add-Result 'ProducingFps' 'same-session control (context)' 'INFO' 'no control statistics'
    }
}

function Invoke-F003LogScan {
    Add-LogSegment
    $log = $script:f003Logs.ToString()
    Save-Text -Name 'logcat-f003.txt' -Text $log | Out-Null
    $lines = $log -split "`n"
    $fatal = @($lines | Where-Object { $_ -match 'FATAL EXCEPTION|\sF DEBUG\s|SIGSEGV' }).Count
    $coded = @($lines | Where-Object { $_ -match 'persistence\.failed|checkpoint\.invalid|checkpoint\.version|presentation\.key\.unresolved|return\.blocked|\[IdleFactory\.Persistence\]' })
    $unity = @($lines | Where-Object { $_ -match '\sE Unity\s' })
    $unexpected = @($unity | Where-Object { $_ -notmatch 'AssetPackManager|Autoconnect' })
    Assert-Check 'F003LogScan' 'no crash signatures' ($fatal -eq 0) "$fatal FATAL/F DEBUG/SIGSEGV line(s) in $($lines.Count)"
    Assert-Check 'F003LogScan' 'no coded diagnostics' ($coded.Count -eq 0) "$($coded.Count) persistence/start-up diagnostic line(s)"
    Add-Result 'F003LogScan' 'E Unity lines' 'INFO' "$($unity.Count), of which $($unexpected.Count) outside the known benign AssetPackManager and Autoconnect Profiler probes"
    foreach ($line in @($unexpected | Select-Object -First 5)) { Add-Result 'F003LogScan' 'unexpected E Unity line' 'INFO' ($line.Trim()) }
}

# ---------------------------------------------------------------- run

$started = Get-Date

# Lease the emulator for the whole run. The estimate covers a fresh-install
# launch, the journey and every profile run, plus a margin.
$leaseScript = Join-Path $PSScriptRoot 'resource-lease.ps1'
$leaseMinutes = [int] [Math]::Ceiling(20 + $Runs * ($StartupTimeoutSeconds + $WarmupSeconds + $CaptureSeconds + 60) / 60)
if ($Scenario -eq 'F003') {
    # Three timing starts, three frame-rate captures, rotation steps and a margin.
    $leaseMinutes += [int] [Math]::Ceiling((3 * ($StartupTimeoutSeconds + 60) + 3 * ($StartupTimeoutSeconds + $WarmupSeconds + $CaptureSeconds + 60)) / 60) + 15
}
$leaseOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File $leaseScript -Acquire -Resource emulator -Owner $LeaseOwner -Purpose "android-emulator-journey $($Steps -join ',')" -TtlMinutes $leaseMinutes
$leaseCode = $LASTEXITCODE
$leaseAcquiredHere = ($leaseOutput -join "`n") -match 'emulator: acquired'
if ($leaseCode -ne 0) {
    Add-Result 'Environment' 'emulator lease' 'FAIL' (($leaseOutput -join ' ') + ' - another session or issue holds the emulator; wait for its release.')
    $Steps = @()
}
else {
    Add-Result 'Environment' 'emulator lease' 'INFO' ($leaseOutput -join ' ')
}

foreach ($step in $Steps) {
    if (-not $script:Serial -or $step -eq 'Preflight') {
        $ready = $false
        try { $ready = [bool] (Invoke-Preflight) }
        catch { Add-Result 'Preflight' 'step completed' 'FAIL' $_.Exception.Message }
        if (-not $ready) { break }
        if ($step -eq 'Preflight') { continue }
    }
    try {
        switch ($step) {
            'Install' { Invoke-Install }
            'Launch' { Invoke-Launch }
            'Rotation' { Invoke-Rotation }
            'Home' { Invoke-Home }
            'Lock' { Invoke-Lock }
            'ColdRestart' { Invoke-ColdRestart }
            'LogScan' { Invoke-LogScan }
            'Profile' { Invoke-Profile }
            'TouchBuild' { Invoke-TouchBuild }
            'RemoveConfirmRotation' { Invoke-ConfirmRotation -Step 'RemoveConfirmRotation' -Taps $RemoveTaps }
            'PrestigeConfirmRotation' { Invoke-ConfirmRotation -Step 'PrestigeConfirmRotation' -Taps $PrestigeTaps }
            'W003Push' { Invoke-W003Push }
            'OfflineReturnTiming' { Invoke-OfflineReturnTiming }
            'ProducingFps' { Invoke-ProducingFps }
            'F003LogScan' { Invoke-F003LogScan }
        }
    }
    catch {
        Add-Result $step 'step completed' 'FAIL' $_.Exception.Message
    }
}

# ---------------------------------------------------------------- report

$failed = @($results | Where-Object Result -eq 'FAIL').Count
$md = New-Object System.Text.StringBuilder
[void] $md.AppendLine("# Android emulator journey - $($started.ToString('yyyy-MM-dd HH:mm'))")
[void] $md.AppendLine()
[void] $md.AppendLine('**Emulator only**: development evidence for this AVD and its ARM64 translation layer. No physical-device, performance-qualification or release claim.')
[void] $md.AppendLine()
[void] $md.AppendLine('| Environment | Value |')
[void] $md.AppendLine('|---|---|')
foreach ($key in $environment.Keys) { [void] $md.AppendLine("| $key | ``$($environment[$key])`` |") }
[void] $md.AppendLine()
[void] $md.AppendLine('| Step | Check | Result | Detail |')
[void] $md.AppendLine('|---|---|---|---|')
foreach ($r in $results) { [void] $md.AppendLine("| $($r.Step) | $($r.Check) | **$($r.Result)** | $($r.Detail -replace '\|', '/') |") }
if ($script:profileRows) {
    [void] $md.AppendLine()
    [void] $md.AppendLine("Profile: $Runs run(s), warm-up ${WarmupSeconds} s without input, capture ${CaptureSeconds} s. Exploratory baseline; no threshold applies.")
    [void] $md.AppendLine()
    [void] $md.AppendLine('| Run | Start state | Start checkpoint | End state | Capture s | Frames | Dropped | Avg FPS | CPU % | PSS start KiB | PSS end KiB |')
    [void] $md.AppendLine('|---|---|---|---|---|---|---|---|---|---|---|')
    foreach ($p in $script:profileRows) {
        [void] $md.AppendLine("| $($p.Run) | $($p.StartState) | $($p.StartCheckpoint) | $($p.EndState) | $($p.CaptureSeconds) | $($p.Frames) | $($p.DroppedFrames) | $($p.AverageFps) | $($p.CpuPercent) | $($p.PssStartKb) | $($p.PssEndKb) |")
    }
}
[void] $md.AppendLine()
[void] $md.AppendLine("Result: $(if ($failed) { "FAIL ($failed check(s))" } else { 'PASS' }). Raw evidence: this directory.")
Save-Text -Name 'journey.md' -Text $md.ToString() | Out-Null
Save-Text -Name 'journey.json' -Text (@{ environment = $environment; results = $results; profile = $script:profileRows } | ConvertTo-Json -Depth 5) | Out-Null

if ($leaseAcquiredHere) {
    # Release only a lease this run took; a lease the caller already held stays with the caller.
    & powershell -NoProfile -ExecutionPolicy Bypass -File $leaseScript -Release -Resource emulator -Owner $LeaseOwner | Out-Null
}

Write-Output ''
Write-Output "Report: $(Join-Path $OutDir 'journey.md')"
if ($failed) {
    Write-Output "Journey: FAIL ($failed check(s))."
    exit 1
}
Write-Output 'Journey: PASS.'
