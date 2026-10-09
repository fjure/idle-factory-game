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

Scenario F003 (-Scenario F003, T123, T245) adds the Feature 003 steps below and defaults
-Steps to Preflight,Install,Launch,TouchBuild,RemoveConfirmRotation,BackGesture,
PrestigeConfirmRotation,W003Push,OfflineReturnTiming,ProducingFps,F003LogScan.
The default scenario keeps the step set above unchanged.

The device sets are the SHIPPED-catalog sets of T245 (Fixtures/SaveV2/device/): the
player rejects the older W003 set device/w003-s (save.v2.invariant, return.blocked)
because it was generated against a test-local catalog. W003Push, OfflineReturnTiming
and ProducingFps use w003-s-shipped; PrestigeConfirmRotation uses prestige-ready;
-DeviceSet selects another one for every step that uses a set.
  TouchBuild              touchscreen feature, optional scripted build taps (-BuildTaps):
                          the Save V2 checkpoint gains a machine. The checkpoint is only
                          written by a lifecycle event, so a departure checkpoint is
                          forced (HOME and return) before the baseline and after the taps.
                          T247: on a fresh clear the baseline waits until the UI is ready
                          and retries the forced departure; a baseline that stays absent
                          after the retries is still a FAIL
  RemoveConfirmRotation   -RemoveTaps open the Remove confirmation, a rotation cancels it:
  PrestigeConfirmRotation -PrestigeTaps open the Prestige confirmation (prestige-ready
                          set pushed and cold-started first), same checks. T247: without
                          -PrestigeTaps the step dismisses the Welcome back summary of the
                          8 h credit, taps the Prestige tab, scrolls the sheet down twice and
                          taps "Sell the factory" (1080x2400 fractions). R20: a rotation
                          is a normal FR-037 transition, so a return reconcile is allowed
                          (counted, INFO) and the offline summary is withheld (screenshot);
                          same process, nothing spent (machines, run number, prestige
                          count, cash not lower) read from forced departure checkpoints
  BackGesture             opens the Remove confirmation (-RemoveTaps), injects the OS back
                          gesture (edge swipe, gesture navigation; -BackInput Keycode sends
                          KEYCODE_BACK instead as the control). Cancel is reported only from
                          a machine-checked signal: the dialog's red danger button (pixel
                          count in the central band of a screenshot) is on screen before the
                          input and gone after it, so a camera pan or a system-bar overlay
                          cannot pass. Same process, resumed activity and nothing spent are
                          guards, never evidence of Cancel; the "closed" screenshot is named
                          as the decisive manual check. A second back with no surface open is
                          recorded (INFO) and the app relaunched
  W003Push                pushes the w003-s-shipped set (T245) into Checkpoints/v2 and
                          verifies its SHA-256 on the device against the committed hash
  OfflineReturnTiming     3 cold starts of the set (re-pushed before each), parses the
                          Development-build line "ResumeAndReconcile ms=<n>" (T110), per
                          start and median over ACCEPTED reconciles only, budget <= 2000 ms
                          (PE-2); a start with return.blocked or a save.v2.* diagnostic is a
                          FAIL and never enters the median (all blocked = FAIL); the
                          departure is far in the past so each accepted start reconciles
                          the capped eight hours; production must continue afterwards (PE-6)
  ProducingFps            capture of the set between two fresh-run controls in the same
                          session (profiler-free build; budget >= 60 average FPS); the
                          pushed run must have loaded (accepted reconcile) before capturing
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
Besides "x,y" a list may contain "scroll" (swipe the sheet up by about 40 percent of the
screen height) and "swipe:x1,y1,x2,y2[,ms]" (values <= 1 are fractions of the display).
Default: Continue, Prestige tab, scroll, scroll, "Sell the factory" for the 1080x2400 portrait AVD.

.PARAMETER BackInput
F003 BackGesture: Gesture (default; edge swipe in gesture navigation) or Keycode
(KEYCODE_BACK, the control that must behave the same).

.PARAMETER BackSwipes
F003 BackGesture with -BackInput Gesture: number of edge swipes, 0.4 s apart (default 1). T247 found that the player
runs in immersive mode (Start In Fullscreen), where SystemUI disables the back gesture while the navigation bar is hidden:
the first swipe only reveals the transient system bars and the second one is the Back (-BackSwipes 2).

.PARAMETER DeviceSet
F003: the committed shipped-catalog device set to push, w003-s-shipped or prestige-ready.
Default: w003-s-shipped, and prestige-ready for PrestigeConfirmRotation.

.PARAMETER W003Fixture
Directory holding the checkpoint set to push instead of the committed one (same file
names and hashes as the selected -DeviceSet; for a relocated copy of the fixtures).

.PARAMETER ProductionSeconds
F003 OfflineReturnTiming: seconds to let the device set run after the last accepted start.

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
    [ValidateSet('Gesture', 'Keycode')] [string] $BackInput = 'Gesture',
    [ValidateRange(1, 3)] [int] $BackSwipes = 1,
    [string] $W003Fixture,
    [string] $DeviceSet,
    [int] $ProductionSeconds = 20
)

$ErrorActionPreference = 'Stop'
$knownSteps = 'Preflight', 'Install', 'Launch', 'Rotation', 'Home', 'Lock', 'ColdRestart', 'LogScan', 'Profile'
$knownScenarios = 'Default', 'F003'
if ($knownScenarios -notcontains $Scenario) {
    throw "Unknown scenario '$Scenario'. Known scenarios: $($knownScenarios -join ', ')."
}
if ($Scenario -eq 'F003') {
    $knownSteps = @($knownSteps) + @('TouchBuild', 'RemoveConfirmRotation', 'BackGesture', 'PrestigeConfirmRotation', 'W003Push', 'OfflineReturnTiming', 'ProducingFps', 'F003LogScan')
    if (-not $PSBoundParameters.ContainsKey('Steps')) {
        $Steps = 'Preflight', 'Install', 'Launch', 'TouchBuild', 'RemoveConfirmRotation', 'BackGesture', 'PrestigeConfirmRotation', 'W003Push', 'OfflineReturnTiming', 'ProducingFps', 'F003LogScan'
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

# The committed device sets (T245, Fixtures/SaveV2/README.md, section Device checkpoint sets). They are generated
# against the SHIPPED catalog and are the only sets the player accepts. The older device/w003-s set (test-local
# catalog) is rejected with save.v2.invariant and is deliberately not offered here. SHA-256 over the file bytes.
$deviceSets = [ordered]@{
    'w003-s-shipped' = [pscustomobject]@{
        Name           = 'w003-s-shipped'
        Directory      = 'device/w003-s-shipped'
        CheckpointId   = '22222222-2222-2222-2222-000000000030'
        DepartureUtcMs = 1700000010400
        Files          = [ordered]@{
            'run-checkpoint.data-a.json'    = '4f7ed559fb1a396abca012204a0a64308ebdb6715f92cc364473256e1319403f'
            'run-checkpoint.manifest-a.bin' = '781810f0d39f30691fff16dea19d81856904ea059302161ac856dbfaf3e760e5'
        }
    }
    'prestige-ready' = [pscustomobject]@{
        Name           = 'prestige-ready'
        Directory      = 'device/prestige-ready'
        CheckpointId   = '22222222-2222-2222-2222-000000000031'
        DepartureUtcMs = 1700000014800
        Files          = [ordered]@{
            'run-checkpoint.data-a.json'    = '4849e8f8d051f11491c80994caba9c7b7f27a67fb61dee1c9318731e1fdf74b9'
            'run-checkpoint.manifest-a.bin' = '46d98136301dbc39d370f5a524a3a101c0e24e3dcf241207c2a3d47ac2c73ccf'
        }
    }
}
if ($DeviceSet -and -not $deviceSets.Contains($DeviceSet)) {
    throw "Unknown device set '$DeviceSet'. Known device sets: $($deviceSets.Keys -join ', ')."
}
$reconcileBudgetMs = 2000
$fpsBudget = 60
# A start the player did not accept: a coded rejection or a persistence diagnostic in its logcat.
$blockedPattern = 'return\.blocked|save\.v2\.|checkpoint\.(invalid|version|content)|\[IdleFactory\.Persistence\]'
$script:f003Logs = New-Object System.Text.StringBuilder

function Get-DeviceSetFor {
    # -DeviceSet wins; otherwise PrestigeConfirmRotation uses prestige-ready and every other step w003-s-shipped.
    param([string] $Step)
    $name = if ($DeviceSet) { $DeviceSet } elseif ($Step -eq 'PrestigeConfirmRotation') { 'prestige-ready' } else { 'w003-s-shipped' }
    return $deviceSets[$name]
}

function Get-DeviceSetDir {
    param($Set)
    if ($W003Fixture) { return (Resolve-Path $W003Fixture).Path }
    return (Join-Path $repositoryRoot ('Assets/IdleFactory/Tests/EditMode/Fixtures/SaveV2/' + $Set.Directory))
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

function Wait-UiReady {
    # The first tap must not be lost on a start-up screen. Ready means: the process has a SurfaceView layer (Unity
    # created its surface) and, with -RequireLoaded, the cold start has finished (a "ResumeAndReconcile ms=" line, or a
    # coded rejection that the caller then reports). Returns an object, or $null on timeout.
    param([int] $ProcessId, [switch] $RequireLoaded, [int] $TimeoutSeconds = 0)
    if ($TimeoutSeconds -le 0) { $TimeoutSeconds = $StartupTimeoutSeconds + 60 }
    $started = Get-Date
    $deadline = $started.AddSeconds($TimeoutSeconds)
    do {
        $layers = Invoke-Adb shell dumpsys SurfaceFlinger --list -AllowFailure
        $surface = [bool] ($layers -match ('SurfaceView\[' + [regex]::Escape($Package)))
        $log = Invoke-Adb logcat -d --pid $ProcessId -AllowFailure
        $loaded = [bool] ($log -match 'ResumeAndReconcile ms=\d+')
        $blocked = [bool] ($log -match $blockedPattern)
        if ($surface -and (-not $RequireLoaded -or $loaded -or $blocked)) {
            Start-Sleep -Seconds 2
            return [pscustomobject]@{ Seconds = [Math]::Round(((Get-Date) - $started).TotalSeconds); Loaded = $loaded; Blocked = $blocked }
        }
        Start-Sleep -Seconds 2
    } while ((Get-Date) -lt $deadline)
    return $null
}

function Save-DepartureCheckpoint {
    # The Save V2 checkpoint is only written by a lifecycle event (focus loss, HOME), never on a timer, so a
    # checkpoint read at an arbitrary moment is stale or absent. Forces a departure checkpoint: HOME, wait for a newer
    # generation, then return in the same process. Written is false when no newer generation appeared.
    param([string] $Label)
    $latest = Get-DeviceCheckpointV2 -Label "$Label-latest"
    $startPid = Get-AppPid
    Invoke-Adb shell input keyevent KEYCODE_HOME | Out-Null
    $departure = Wait-CheckpointV2 -After $latest -Label "$Label-departure"
    $written = [bool] ($departure -and (-not $latest -or $departure.Generation -gt $latest.Generation))
    $resumedPid = Start-App
    if ($resumedPid) { $null = Wait-UiReady -ProcessId $resumedPid -TimeoutSeconds 60 }
    return [pscustomobject]@{
        Checkpoint  = $(if ($written) { $departure } else { $null })
        Written     = $written
        SameProcess = [bool] ($startPid -and $resumedPid -eq $startPid)
        Pid         = $startPid
    }
}

function Test-NothingSpent {
    # Machines, run number, prestige count unchanged and Cash not lower (a reconcile may only add).
    param($Before, $After)
    return [bool] ($Before -and $After -and $After.Machines -eq $Before.Machines -and $After.RunNumber -eq $Before.RunNumber -and $After.PrestigeCount -eq $Before.PrestigeCount -and $After.Cash -ge $Before.Cash)
}

function ConvertTo-TapPoints {
    # "x,y" items (or "x,y;x,y") are taps; "scroll" and "swipe:x1,y1,x2,y2[,ms]" are swipes. Values <= 1 are fractions of the
    # current display size.
    param([string[]] $Taps)
    $size = (Get-DisplayState).Size
    $w = 0; $h = 0
    if ($size -match '^(\d+)x(\d+)$') { $w = [int] $Matches[1]; $h = [int] $Matches[2] }
    $invariant = [Globalization.CultureInfo]::InvariantCulture
    $scaleX = { param($v) if ($v -le 1) { [int] [Math]::Round($v * $w) } else { [int] [Math]::Round($v) } }
    $scaleY = { param($v) if ($v -le 1) { [int] [Math]::Round($v * $h) } else { [int] [Math]::Round($v) } }
    foreach ($tap in @($Taps | ForEach-Object { $_ -split ';' } | Where-Object { $_ })) {
        $item = $tap.Trim()
        if ($item -eq 'scroll') { $item = 'swipe:0.5,0.79,0.5,0.42,400' }
        if ($item -match '^swipe:(.+)$') {
            $v = @($Matches[1] -split ',' | ForEach-Object { [double]::Parse($_.Trim(), $invariant) })
            if ($v.Count -lt 4 -or $v.Count -gt 5) { throw "Swipe '$tap' is not swipe:x1,y1,x2,y2[,ms]." }
            [pscustomobject]@{ Kind = 'swipe'; X = (& $scaleX $v[0]); Y = (& $scaleY $v[1]); X2 = (& $scaleX $v[2]); Y2 = (& $scaleY $v[3]); Ms = $(if ($v.Count -eq 5) { [int] $v[4] } else { 400 }) }
            continue
        }
        $xy = $item -split ','
        if ($xy.Count -ne 2) { throw "Tap '$tap' is not x,y." }
        $x = [double]::Parse($xy[0], $invariant)
        $y = [double]::Parse($xy[1], $invariant)
        [pscustomobject]@{ Kind = 'tap'; X = (& $scaleX $x); Y = (& $scaleY $y); X2 = 0; Y2 = 0; Ms = 0 }
    }
}

function Invoke-Taps {
    param([string[]] $Taps, [string] $Step = 'Taps')
    $points = @(ConvertTo-TapPoints -Taps $Taps)
    if ($points.Count -gt 0) {
        # The first tap waits until the UI is ready: a tap on the start-up screen builds nothing (first T124 run).
        $tapPid = Get-AppPid
        $ready = if ($tapPid) { Wait-UiReady -ProcessId $tapPid } else { $null }
        Add-Result $Step 'UI ready before the first tap' $(if ($ready) { 'INFO' } else { 'FAIL' }) $(if ($ready) { "surface present after $($ready.Seconds) s" } else { 'no running process with a surface; the taps would be lost' })
    }
    foreach ($point in $points) {
        if ($point.Kind -eq 'swipe') {
            Invoke-Adb shell input swipe $point.X $point.Y $point.X2 $point.Y2 $point.Ms | Out-Null
        }
        else {
            Invoke-Adb shell input tap $point.X $point.Y | Out-Null
        }
        Start-Sleep -Seconds 1
    }
}

function Get-DialogButtonPixels {
    # Pixels of the confirmation dialog's red danger button (about 248,113,113: Remove, Sell the factory) in the central band of
    # a screenshot, scaled from a 3 px sampling grid. The open dialog shows about 30,000 of them; the Machine sheet's warning
    # icons and text show a few hundred at most. Machine-checkable "the confirmation is on screen" that does not depend on the
    # sheet, the camera or the system bars behind it.
    param([string] $Path)
    Add-Type -AssemblyName System.Drawing
    $bitmap = New-Object System.Drawing.Bitmap $Path
    try {
        $count = 0
        $y0 = [int] (0.3 * $bitmap.Height); $y1 = [int] (0.7 * $bitmap.Height)
        for ($y = $y0; $y -lt $y1; $y += 3) {
            for ($x = 0; $x -lt $bitmap.Width; $x += 3) {
                $pixel = $bitmap.GetPixel($x, $y)
                if ($pixel.R -ge 220 -and $pixel.G -ge 80 -and $pixel.G -le 150 -and $pixel.B -ge 80 -and $pixel.B -le 150) { $count++ }
            }
        }
        return $count * 9
    }
    finally { $bitmap.Dispose() }
}

function Get-ScreenshotChange {
    # Fraction of sampled pixels inside a screen region (left, top, right, bottom as fractions) whose colour differs between
    # two screenshots by more than 15 in any channel (a modal scrim dims dark pixels by about 20). Machine-checkable signal that a dialog (and its scrim) appeared or
    # vanished, without reading the UI tree, which Unity does not expose.
    param([string] $PathA, [string] $PathB, [double[]] $Region = @(0.05, 0.3, 0.95, 0.7), [int] $Step = 6)
    Add-Type -AssemblyName System.Drawing
    $a = New-Object System.Drawing.Bitmap $PathA
    $b = New-Object System.Drawing.Bitmap $PathB
    try {
        $w = [Math]::Min($a.Width, $b.Width); $h = [Math]::Min($a.Height, $b.Height)
        $x0 = [int] ($Region[0] * $w); $y0 = [int] ($Region[1] * $h); $x1 = [int] ($Region[2] * $w); $y1 = [int] ($Region[3] * $h)
        $total = 0; $changed = 0
        for ($y = $y0; $y -lt $y1; $y += $Step) {
            for ($x = $x0; $x -lt $x1; $x += $Step) {
                $pa = $a.GetPixel($x, $y); $pb = $b.GetPixel($x, $y)
                $total++
                if ([Math]::Abs($pa.R - $pb.R) -gt 15 -or [Math]::Abs($pa.G - $pb.G) -gt 15 -or [Math]::Abs($pa.B - $pb.B) -gt 15) { $changed++ }
            }
        }
        if ($total -eq 0) { return 0.0 }
        return [Math]::Round($changed / $total, 3)
    }
    finally { $a.Dispose(); $b.Dispose() }
}

function Invoke-TouchBuild {
    $features = Invoke-Adb shell pm list features
    Assert-Check 'TouchBuild' 'touchscreen feature' ($features -match 'android\.hardware\.touchscreen') 'android.hardware.touchscreen present'
    $display = Get-DisplayState
    Add-Result 'TouchBuild' 'display' 'INFO' "$($display.Rotation) $($display.Size), requested $($display.Requested)"
    Add-Result 'TouchBuild' 'screenshot before' 'INFO' (Save-Screenshot 'touch-before')
    if (-not @($BuildTaps).Count) {
        Add-Result 'TouchBuild' 'build by touch' 'INFO' 'skipped: no -BuildTaps supplied (layout dependent; T124 supplies them from the screenshots)'
        return
    }
    # The checkpoint only changes on a lifecycle event: force a departure checkpoint for the baseline and again after the taps.
    # T247: right after pm clear and the first launch a HOME produced no file (run H). Wait until the UI is up, then retry the
    # forced departure; the baseline is then a checkpoint of the untouched fresh run (0 machines).
    $touchPid = Get-AppPid
    if ($touchPid) { $null = Wait-UiReady -ProcessId $touchPid }
    $baseline = $null
    for ($attempt = 1; $attempt -le 3; $attempt++) {
        $baseline = Save-DepartureCheckpoint -Label "touch-0-before-$attempt"
        if ($baseline.Written) { break }
        Add-Result 'TouchBuild' 'baseline departure checkpoint attempt' 'INFO' "attempt $attempt wrote no checkpoint"
        Start-Sleep -Seconds 5
    }
    $before = $baseline.Checkpoint
    Assert-Check 'TouchBuild' 'baseline departure checkpoint' $baseline.Written (Format-CheckpointV2 $before)
    $startPid = Get-AppPid
    Invoke-Taps -Step 'TouchBuild' -Taps $BuildTaps
    Add-Result 'TouchBuild' 'screenshot after' 'INFO' (Save-Screenshot 'touch-after')
    $samePid = [bool] ($startPid -and (Get-AppPid) -eq $startPid)
    $departure = Save-DepartureCheckpoint -Label 'touch-1-after'
    $after = $departure.Checkpoint
    Assert-Check 'TouchBuild' 'machine built by touch' ($after -and $before -and $after.Machines -gt $before.Machines) ("{0} -> {1}" -f (Format-CheckpointV2 $before), (Format-CheckpointV2 $after))
    Assert-Check 'TouchBuild' 'same process' $samePid "PID $startPid"
}

function Invoke-ConfirmRotation {
    param([string] $Step, [string[]] $Taps)
    if ($Step -eq 'PrestigeConfirmRotation' -and -not @($Taps).Count) {
        # T247: the sale button is below the fold in portrait, so the step scrolls itself. The cold start from the set shows
        # the Welcome back summary of the 8 h credit; Continue (922,1772 of 1080x2400) dismisses it.
        $Taps = @('0.854,0.738', '0.917,0.97', 'scroll', 'scroll', '0.181,0.9')
        Add-Result $Step 'taps' 'INFO' 'default: Continue, Prestige tab, scroll, scroll, Sell the factory (1080x2400 fractions)'
    }
    if (-not @($Taps).Count) {
        Add-Result $Step 'confirmation cancelled by rotation' 'INFO' 'skipped: no taps supplied for this confirmation (layout dependent; T124 supplies them)'
        return
    }
    if ($Step -eq 'PrestigeConfirmRotation') {
        # The Prestige confirmation needs a Factory Value above the Sale threshold: cold-start the prestige-ready set first.
        $set = Get-DeviceSetFor -Step $Step
        Invoke-DeviceSetPush -Step $Step -Set $set
        Add-LogSegment
        Invoke-Adb logcat -c | Out-Null
        $loadPid = Start-App
        $ready = if ($loadPid) { Wait-UiReady -ProcessId $loadPid -RequireLoaded } else { $null }
        $loadLog = Invoke-Adb logcat -d
        Save-Text -Name "$Step-load-logcat.txt" -Text $loadLog | Out-Null
        $loaded = [bool] ($ready -and $ready.Loaded -and -not $ready.Blocked -and -not ($loadLog -match $blockedPattern))
        Assert-Check $Step "$($set.Name) run loaded" $loaded $(if ($loaded) { "accepted by the player after $($ready.Seconds) s" } elseif ($ready) { 'the player rejected the set (coded diagnostic in the logcat)' } else { 'the cold start did not finish in time' })
        if (-not $loaded) { return }
    }
    Invoke-Adb shell cmd window user-rotation free | Out-Null
    Set-Sensor '0:9.81:0'
    # R20: a rotation is a normal FR-037 transition. It may run a return reconcile, it withholds the offline summary, and it
    # spends nothing. The checkpoints are read after a forced departure (HOME and return), because only a lifecycle event writes one.
    $baseline = Save-DepartureCheckpoint -Label "$Step-0-before"
    $before = $baseline.Checkpoint
    Assert-Check $Step 'baseline departure checkpoint' $baseline.Written (Format-CheckpointV2 $before)
    Add-LogSegment
    Invoke-Adb logcat -c | Out-Null
    $startPid = Get-AppPid
    Invoke-Taps -Step $Step -Taps $Taps
    Add-Result $Step 'screenshot confirmation open' 'INFO' (Save-Screenshot "$Step-open")
    Set-Sensor '9.81:0:0'
    $state = Get-DisplayState
    Assert-Check $Step 'rotated to landscape' ($state.Rotation -eq 'ROTATION_90') "$($state.Rotation) $($state.Size), requested $($state.Requested)"
    Start-Sleep -Seconds 2
    Add-Result $Step 'screenshot after rotation (dialog cancelled, offline summary withheld)' 'INFO' (Save-Screenshot "$Step-rotated")
    $log = Invoke-Adb logcat -d
    Save-Text -Name "$Step-logcat.txt" -Text $log | Out-Null
    $samePid = [bool] ($startPid -and (Get-AppPid) -eq $startPid)
    Assert-Check $Step 'same process' $samePid "PID $startPid"
    $reconciles = @(($log -split "`n") | Where-Object { $_ -match 'ResumeAndReconcile ms=(\d+)' } | ForEach-Object { [regex]::Match($_, 'ResumeAndReconcile ms=(\d+)').Groups[1].Value })
    Add-Result $Step 'return reconcile (R20: allowed)' 'INFO' $(if ($reconciles.Count) { "$($reconciles.Count) ResumeAndReconcile line(s), ms: $($reconciles -join ', ')" } else { 'no ResumeAndReconcile line' })
    Assert-Check $Step 'no coded diagnostics during the rotation' (-not ($log -match $blockedPattern)) 'no return.blocked / save.v2.* / persistence diagnostic while the dialog was open and rotated'
    Add-Result $Step 'offline summary withheld' 'INFO' "the player logs no marker for the summary panel; judge it on the screenshots $Step-open and $Step-rotated (no Welcome back dialog, notice Cancelled by rotation)"
    $departure = Save-DepartureCheckpoint -Label "$Step-1-after"
    $after = $departure.Checkpoint
    if ($before -and $after) {
        Assert-Check $Step 'nothing spent' (Test-NothingSpent $before $after) ("{0} -> {1}" -f (Format-CheckpointV2 $before), (Format-CheckpointV2 $after))
    }
    else {
        Add-Result $Step 'nothing spent' 'FAIL' "no forced departure checkpoint to compare (before: $(Format-CheckpointV2 $before), after: $(Format-CheckpointV2 $after))"
    }
    Add-LogSegment
    Set-Sensor '0:9.81:0'
}

function Send-BackInput {
    # KEYCODE_BACK, or -BackSwipes edge swipes from x=3 (the system edge handler ignores a swipe that starts at x=0: T247 control
    # with the Settings app, where x=0 did not go back and x=3 did) 0.4 s apart.
    param([int] $Width, [int] $Height)
    if ($BackInput -eq 'Keycode') {
        Invoke-Adb shell input keyevent 4 | Out-Null
        return
    }
    $mid = [int] [Math]::Round($Height / 2)
    $reach = [int] [Math]::Round($Width * 0.4)
    for ($swipe = 1; $swipe -le $BackSwipes; $swipe++) {
        Invoke-Adb shell input swipe 3 $mid $reach $mid 300 | Out-Null
        if ($swipe -lt $BackSwipes) { Start-Sleep -Milliseconds 400 }
    }
}

function Invoke-BackGesture {
    # T139/T247: the OS back gesture (edge swipe) must cancel the Remove confirmation exactly like KEYCODE_BACK: the dialog
    # closes, nothing is spent, the process stays. Cancel is reported only from the screenshot signal; "same process, resumed,
    # nothing spent" also hold when the input did nothing, so they are guards only. -BackInput Keycode is the control.
    $step = 'BackGesture'
    if (-not @($RemoveTaps).Count) {
        Add-Result $step 'back gesture cancels the confirmation' 'INFO' 'skipped: no -RemoveTaps supplied (layout dependent; T124 supplies them)'
        return
    }
    $navigationPrefix = 'com.android.internal.systemui.navbar.'
    $overlays = Invoke-Adb shell cmd overlay list --user 0 -AllowFailure
    $previousMode = if ($overlays -match ('\[x\]\s+' + [regex]::Escape($navigationPrefix) + '(\w+)')) { $Matches[1] } else { 'threebutton' }
    $restoreMode = $false
    try {
        if ($previousMode -ne 'gestural') {
            Invoke-Adb shell cmd overlay enable-exclusive --category "${navigationPrefix}gestural" -AllowFailure | Out-Null
            $restoreMode = $true
            Start-Sleep -Seconds 3
        }
        $overlays = Invoke-Adb shell cmd overlay list --user 0 -AllowFailure
        Assert-Check $step 'gesture navigation active' ([bool] ($overlays -match ('\[x\]\s+' + [regex]::Escape($navigationPrefix) + 'gestural'))) "navigation mode before the step: $previousMode; input under test: $BackInput$(if ($BackInput -eq 'Gesture') { " x $BackSwipes swipe(s)" })"

        Invoke-Adb shell cmd window user-rotation free | Out-Null
        Set-Sensor '0:9.81:0'
        $baseline = Save-DepartureCheckpoint -Label "$step-0-before"
        $before = $baseline.Checkpoint
        Assert-Check $step 'baseline departure checkpoint' $baseline.Written (Format-CheckpointV2 $before)
        Add-LogSegment
        Invoke-Adb logcat -c | Out-Null
        $startPid = Get-AppPid
        $preName = Save-Screenshot "$step-before-dialog"
        # The first tap after a forced HOME and return is sometimes lost: check that the dialog opened (screen change against the
        # pre-dialog screenshot) and repeat the taps once when it did not.
        $openPixels = 0
        for ($attempt = 1; $attempt -le 2 -and $openPixels -lt 5000; $attempt++) {
            Invoke-Taps -Step $step -Taps $RemoveTaps
            Start-Sleep -Seconds 1
            $openName = Save-Screenshot "$step-open"
            $openPixels = Get-DialogButtonPixels -Path (Join-Path $OutDir $openName)
            Add-Result $step "screenshot confirmation open (attempt $attempt)" 'INFO' "$openName, $openPixels danger-button pixels (>= 5000 means the dialog is on screen)"
        }

        $size = (Get-DisplayState).Size
        $width = 1080; $height = 2400
        if ($size -match '^(\d+)x(\d+)$') { $width = [int] $Matches[1]; $height = [int] $Matches[2] }
        $systemUiBefore = (Invoke-Adb shell dumpsys activity service SystemUIService -AllowFailure) -split "`n" | Where-Object { $_ -match 'navbar_hidden|backGestureDisabled' } | ForEach-Object { $_.Trim() }
        Add-Result $step 'SystemUI state before the input (INFO: navbar_hidden with backGestureDisabled=true means immersive mode, the first edge swipe only reveals the system bars)' 'INFO' (($systemUiBefore -join '; ') -replace '\s+', ' ')
        Send-BackInput -Width $width -Height $height
        Start-Sleep -Seconds 2
        $closedName = Save-Screenshot "$step-closed"
        Add-Result $step 'screenshot after the back input (DECISIVE MANUAL CHECK: dialog closed, focus ring on the Remove button; a single swipe in immersive mode only reveals the system bars)' 'INFO' $closedName

        # Machine-checked Cancel: the dialog's danger button was on screen before the input (>= 5000 pixels) and is gone after it
        # (< 500). A camera pan, a system-bar overlay or any other screen change cannot satisfy both.
        $closedPixels = Get-DialogButtonPixels -Path (Join-Path $OutDir $closedName)
        Assert-Check $step "$BackInput cancels the confirmation (machine-checked: danger-button pixels)" ($openPixels -ge 5000 -and $closedPixels -lt 500) "danger-button pixels before the input $openPixels (needs >= 5000), after $closedPixels (needs < 500)"

        $samePid = [bool] ($startPid -and (Get-AppPid) -eq $startPid)
        $activities = Invoke-Adb shell dumpsys activity activities
        $resumed = [bool] ($activities -match ('(mResumedActivity|topResumedActivity)[^\r\n]*' + [regex]::Escape("$Package/")))
        Assert-Check $step 'guard: same process (also true when the input did nothing)' $samePid "PID $startPid"
        Assert-Check $step 'guard: activity stays resumed (the input did not leave the app)' $resumed "resumed activity of $Package"
        Save-Text -Name "$step-logcat.txt" -Text (Invoke-Adb logcat -d) | Out-Null
        $departure = Save-DepartureCheckpoint -Label "$step-1-after"
        $after = $departure.Checkpoint
        if ($before -and $after) {
            Assert-Check $step 'guard: nothing spent, machine kept (also true when the input did nothing)' (Test-NothingSpent $before $after) ("{0} -> {1}" -f (Format-CheckpointV2 $before), (Format-CheckpointV2 $after))
        }
        else {
            Add-Result $step 'guard: nothing spent, machine kept' 'FAIL' "no forced departure checkpoint to compare (before: $(Format-CheckpointV2 $before), after: $(Format-CheckpointV2 $after))"
        }

        # Behaviour with no open surface (T247 step 3): record what the same input does, then bring the app back.
        $idleName = Save-Screenshot "$step-idle-before"
        Invoke-Adb logcat -c | Out-Null
        Send-BackInput -Width $width -Height $height
        Start-Sleep -Seconds 3
        $idlePid = Get-AppPid
        $topActivity = ((Invoke-Adb shell dumpsys activity activities) -split "`n" | Where-Object { $_ -match 'topResumedActivity|mResumedActivity' } | Select-Object -First 1)
        $idleAfter = Save-Screenshot "$step-idle-after"
        $idleChange = Get-ScreenshotChange -PathA (Join-Path $OutDir $idleName) -PathB (Join-Path $OutDir $idleAfter) -Region @(0.0, 0.0, 1.0, 1.0)
        Add-Result $step 'back with no surface open (recorded, not asserted)' 'INFO' ("process alive: {0} (PID {1}), top activity: {2}, screenshots {3} -> {4}, whole-screen change {5}" -f [bool] $idlePid, $idlePid, (("$topActivity") -replace '\s+', ' ').Trim(), $idleName, $idleAfter, $idleChange)
        Save-Text -Name "$step-idle-logcat.txt" -Text (Invoke-Adb logcat -d) | Out-Null
        $null = Start-App
        Add-LogSegment
    }
    finally {
        if ($restoreMode) {
            Invoke-Adb shell cmd overlay enable-exclusive --category "${navigationPrefix}$previousMode" -AllowFailure | Out-Null
        }
    }
}

function Push-DeviceSet {
    # Replaces the device checkpoints with a committed device set (V2 only).
    param($Set)
    $dir = Get-DeviceSetDir -Set $Set
    Invoke-Adb shell am force-stop $Package | Out-Null
    Invoke-Adb shell rm -rf $deviceCheckpointDir | Out-Null
    Invoke-Adb shell rm -rf $deviceCheckpointDirV2 | Out-Null
    Invoke-Adb shell mkdir -p $deviceCheckpointDirV2 | Out-Null
    foreach ($name in $Set.Files.Keys) {
        Invoke-Adb push (Join-Path $dir $name) "$deviceCheckpointDirV2/$name" | Out-Null
    }
}

function Invoke-DeviceSetPush {
    # Verifies the committed hashes, pushes the set and verifies the hashes on the device.
    param([string] $Step, $Set)
    $dir = Get-DeviceSetDir -Set $Set
    foreach ($name in $Set.Files.Keys) {
        $local = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $dir $name)).Hash.ToLowerInvariant()
        Assert-Check $Step "committed $($Set.Name)/$name" ($local -eq $Set.Files[$name]) "SHA-256 $($local.Substring(0, 16))"
    }
    Push-DeviceSet -Set $Set
    foreach ($name in $Set.Files.Keys) {
        $device = Invoke-Adb shell sha256sum "$deviceCheckpointDirV2/$name" -AllowFailure
        $deviceHash = if ($device -match '^([0-9a-fA-F]{64})') { $Matches[1].ToLowerInvariant() } else { '' }
        Assert-Check $Step "device $($Set.Name)/$name" ($deviceHash -eq $Set.Files[$name]) $(if ($deviceHash) { "SHA-256 $($deviceHash.Substring(0, 16)) on the device" } else { "no hash from the device: $device" })
    }
}

function Invoke-W003Push {
    Invoke-DeviceSetPush -Step 'W003Push' -Set (Get-DeviceSetFor -Step 'W003Push')
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
    $set = Get-DeviceSetFor -Step 'OfflineReturnTiming'
    $step = 'OfflineReturnTiming'
    $deviceNow = Invoke-Adb shell date +%s
    if ($deviceNow -match '^\d+$') {
        $gapHours = ([double] $deviceNow * 1000 - $set.DepartureUtcMs) / 3600000
        Assert-Check $step 'departure beyond the eight-hour cap' ($gapHours -gt 8) ("{0} departure is {1:N0} h before the device clock, so each accepted start reconciles exactly the capped 8 h" -f $set.Name, $gapHours)
    }
    else {
        Add-Result $step 'device clock' 'FAIL' "cannot read the device clock: $deviceNow"
    }
    # Only a start the player accepted counts: a reconcile line, no coded rejection (return.blocked, save.v2.*, persistence
    # diagnostic) and a published checkpoint that was reconciled from the set. A blocked start prints a reconcile line as well
    # (the time to the rejection path), so the line alone proves nothing (first T124 run).
    $accepted = New-Object System.Collections.Generic.List[long]
    $blockedStarts = 0
    $last = $null
    $lastPid = $null
    for ($i = 1; $i -le 3; $i++) {
        Push-DeviceSet -Set $set
        Add-LogSegment
        Invoke-Adb logcat -c | Out-Null
        $startPid = Start-App
        if (-not $startPid) { Add-Result $step "start $i" 'FAIL' 'process did not start'; continue }
        $ms = Wait-ReconcileLine -ProcessId $startPid
        if ($null -eq $ms) {
            Add-Result $step "start $i" 'FAIL' "no 'ResumeAndReconcile ms=' line within $($StartupTimeoutSeconds + 60) s (Development build required)"
            continue
        }
        $startLog = Invoke-Adb logcat -d --pid $startPid -AllowFailure
        Save-Text -Name "offline-return-start-$i-logcat.txt" -Text $startLog | Out-Null
        $diagnostics = @(($startLog -split "`n") | Where-Object { $_ -match $blockedPattern })
        $cp = Wait-CheckpointV2 -After ([pscustomobject]@{ Generation = 0 }) -Label "reconcile-$i"
        $reconciled = [bool] ($cp -and $cp.ReconciledFrom -eq $set.CheckpointId)
        if ($diagnostics.Count -gt 0 -or -not $reconciled) {
            $blockedStarts++
            $reason = if ($diagnostics.Count -gt 0) { "BLOCKED: $($diagnostics.Count) rejection diagnostic line(s), first: $($diagnostics[0].Trim())" } else { 'NOT ACCEPTED: the published checkpoint was not reconciled from the pushed set' }
            Add-Result $step "start $i" 'FAIL' ("{0} ms (rejection path, excluded from the median); {1}; {2}" -f $ms, $reason, (Format-CheckpointV2 $cp))
            continue
        }
        $accepted.Add($ms)
        $budgetNote = if ($ms -gt $reconcileBudgetMs) { " (over the $reconcileBudgetMs ms budget)" } else { '' }
        Add-Result $step "start $i" $(if ($ms -le $reconcileBudgetMs) { 'PASS' } else { 'FAIL' }) ("{0} ms{1}; accepted, reconciled from {2}; {3}" -f $ms, $budgetNote, $set.Name, (Format-CheckpointV2 $cp))
        $last = $cp
        $lastPid = $startPid
    }
    if ($accepted.Count -eq 0) {
        Add-Result $step 'median' 'FAIL' "no start was accepted by the player ($blockedStarts of 3 blocked or not reconciled); a median over rejection paths is not a measurement"
        return
    }
    $sorted = @($accepted | Sort-Object)
    if ($sorted.Count % 2 -eq 1) { $median = $sorted[($sorted.Count - 1) / 2] }
    else { $median = [Math]::Round(($sorted[$sorted.Count / 2 - 1] + $sorted[$sorted.Count / 2]) / 2) }
    Assert-Check $step 'median of accepted starts' ($accepted.Count -eq 3 -and $median -le $reconcileBudgetMs) ("median {0} ms of [{1}] accepted ({2} of 3 starts accepted; budget <= {3} ms, PE-2)" -f $median, ($accepted -join ', '), $accepted.Count, $reconcileBudgetMs)

    # The set keeps producing after the capped reconcile.
    if ($last -and $lastPid -and (Get-AppPid) -eq $lastPid) {
        $null = Wait-UnityReady -ProcessId $lastPid
        Start-Sleep -Seconds $ProductionSeconds
        Invoke-Adb shell input keyevent KEYCODE_HOME | Out-Null
        $later = Wait-CheckpointV2 -After $last -Label 'reconcile-production'
        Assert-Check $step "$($set.Name) keeps producing" ($later -and $later.Generation -gt $last.Generation -and $later.Cash -gt $last.Cash) ("{0} -> {1}" -f (Format-CheckpointV2 $last), (Format-CheckpointV2 $later))
        Start-App | Out-Null
    }
    else {
        Add-Result $step "$($set.Name) keeps producing" 'FAIL' 'the last accepted start did not stay in one process'
    }
    Add-LogSegment
}

function Invoke-FpsCapture {
    param([string] $Label, [scriptblock] $Prepare, [string] $ExpectedReconciledFrom)
    Invoke-Adb shell am force-stop $Package | Out-Null
    & $Prepare
    Add-LogSegment
    Invoke-Adb logcat -c | Out-Null
    Invoke-Adb shell dumpsys SurfaceFlinger --timestats -enable | Out-Null
    $fpsPid = Start-App
    if (-not $fpsPid) { Add-Result 'ProducingFps' "$Label launch" 'FAIL' 'process did not start'; return $null }
    $null = Wait-UnityReady -ProcessId $fpsPid
    if ($ExpectedReconciledFrom) {
        # A capture of a start-up screen is not a producing-factory figure (first T124 run): the pushed run must have loaded.
        $ms = Wait-ReconcileLine -ProcessId $fpsPid
        $loadLog = Invoke-Adb logcat -d --pid $fpsPid -AllowFailure
        $rejected = @(($loadLog -split "`n") | Where-Object { $_ -match $blockedPattern })
        $cp = Wait-CheckpointV2 -After ([pscustomobject]@{ Generation = 0 }) -Label "fps-$Label-loaded"
        $loaded = [bool] (($null -ne $ms) -and $rejected.Count -eq 0 -and $cp -and $cp.ReconciledFrom -eq $ExpectedReconciledFrom)
        Assert-Check 'ProducingFps' "$Label pushed run loaded" $loaded $(if ($loaded) { "reconciled in $ms ms from $ExpectedReconciledFrom; $(Format-CheckpointV2 $cp)" } elseif ($rejected.Count) { "the player rejected the pushed set: $($rejected[0].Trim())" } else { "no accepted reconcile of the pushed set ($(Format-CheckpointV2 $cp))" })
        if (-not $loaded) { return $null }
    }
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
    # Same-session control: a fresh, empty run (pm clear) before and after the device-set capture.
    $set = Get-DeviceSetFor -Step 'ProducingFps'
    $control = { Invoke-Adb shell pm clear $Package | Out-Null }
    $producer = { Push-DeviceSet -Set $set }
    $c1 = Invoke-FpsCapture -Label 'control-1' -Prepare $control
    $p = Invoke-FpsCapture -Label 'producing' -Prepare $producer -ExpectedReconciledFrom $set.CheckpointId
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
    $coded = @($lines | Where-Object { $_ -match 'persistence\.failed|checkpoint\.invalid|checkpoint\.version|presentation\.key\.unresolved|return\.blocked|save\.v2\.|\[IdleFactory\.Persistence\]' })
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
            'BackGesture' { Invoke-BackGesture }
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
