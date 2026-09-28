[CmdletBinding()]
param(
    # Prepare:  restore the fixed T040 checkpoint, cold-launch, 30 s no-input warm-up, record the capture-start state.
    # Finish:   record the capture-end state and logs of the same process, then force-stop.
    # Unscored: the one discarded launch after every install (restore, launch, wait, force-stop).
    # Restore:  push a checkpoint directory back to the device (use it with -SourceCheckpoint for the original profile).
    # Capture:  record -CaptureSeconds with the standalone profiler, export counters, take and read a memory snapshot.
    # Run:      Prepare -AttachProfiler, Capture and Finish for one run.
    [Parameter(Mandatory)][ValidateSet('Prepare', 'Capture', 'Finish', 'Run', 'Unscored', 'Restore')] [string] $Phase,
    [int] $Run = 0,
    [string] $Adb = 'C:\Users\Domenik\AppData\Local\Android\Sdk\platform-tools\adb.exe',
    [string] $Device = 'emulator-5554',
    [string] $Package = 'com.DefaultCompany.idlefactorygame',
    [string] $Activity = 'com.idlefactory.player.IdleFactoryGameActivity',
    [string] $RawRoot = 'test-results/android/t064',
    [string] $SourceCheckpoint = 'test-results/android/t064/source-checkpoint-v1',
    [string] $ExpectedApkSha256 = '',
    [int] $WarmupSeconds = 30,
    [int] $CaptureSeconds = 60,
    # Prepare: attach the Unity Profiler (Standalone Process) through profiler-mcp.ps1 right after the launch.
    [switch] $AttachProfiler
)

# T064: T040 capture-start protocol (specs/001-core-game-foundation/reviews/t040-evidence/capture-protocol.ps1)
# split into phases, so the Unity Profiler (Standalone Process) can connect and record between Prepare and Finish.
# The restore, launch, warm-up and state hash are unchanged from T040.

$ErrorActionPreference = 'Stop'
$checkpointPath = "/storage/emulated/0/Android/data/$Package/files/IdleFactory/Checkpoints/v1"
$checkpointFiles = @('factory-checkpoint.data-a.json', 'factory-checkpoint.data-b.json', 'factory-checkpoint.manifest-a.bin', 'factory-checkpoint.manifest-b.bin', 'factory-checkpoint.pending.json')

function Invoke-Adb {
    param([string[]] $Command)
    & $Adb -s $Device @Command
    if ($LASTEXITCODE -ne 0) {
        throw "ADB failed ($LASTEXITCODE): $($Command -join ' ')"
    }
}

function Stamp([string] $Name) {
    "$Name=$([DateTime]::UtcNow.ToString('o'))"
}

function Restore-Checkpoint([string] $From) {
    $files = Get-ChildItem -LiteralPath $From -File
    if ($files.Count -ne 5) {
        throw "Expected five checkpoint files in $From; found $($files.Count)."
    }
    Invoke-Adb -Command @('shell', 'am', 'force-stop', $Package) | Out-Null
    Invoke-Adb -Command (@('shell', 'rm', '-f') + ($checkpointFiles | ForEach-Object { "$checkpointPath/$_" })) | Out-Null
    Invoke-Adb -Command @('shell', 'mkdir', '-p', $checkpointPath) | Out-Null
    $files | Sort-Object Name | ForEach-Object {
        Invoke-Adb -Command @('push', $_.FullName, "$checkpointPath/$($_.Name)") | Out-Null
    }
}

function Write-StateRecord {
    param([string] $CheckpointDirectory, [string] $OutputPath)

    $candidates = Get-ChildItem -LiteralPath $CheckpointDirectory -Filter '*.json' -File |
        ForEach-Object {
            try {
                $checkpoint = Get-Content -Raw -LiteralPath $_.FullName | ConvertFrom-Json
                [pscustomobject]@{ File = $_.Name; Checkpoint = $checkpoint }
            }
            catch {
                $null
            }
        } |
        Where-Object { $null -ne $_ -and $null -ne $_.Checkpoint.generation } |
        Sort-Object { [long] $_.Checkpoint.generation } -Descending

    if (-not $candidates) {
        throw "No readable checkpoint JSON found in $CheckpointDirectory"
    }

    $selected = $candidates[0]
    $checkpoint = $selected.Checkpoint
    $factory = $checkpoint.factory
    $buildingRecords = foreach ($building in @($factory.buildings) | Sort-Object instanceId) {
        $inputs = (@($building.inputQuantities) | Sort-Object resourceId | ForEach-Object { "$($_.resourceId)=$($_.quantity)" }) -join ','
        $outputs = (@($building.outputQuantities) | Sort-Object resourceId | ForEach-Object { "$($_.resourceId)=$($_.quantity)" }) -join ','
        "$($building.instanceId)|$($building.definitionId)|anchor=$($building.anchorX),$($building.anchorY)|orientation=$($building.orientationQuarterTurns)|active=$($building.isActive.ToString().ToLowerInvariant())|inputs=$inputs|outputs=$outputs|recipe=$($building.production.recipeId)|progressTicks=$($building.production.progressTicks)"
    }
    $canonical = "origin=$($factory.originX),$($factory.originY);size=$($factory.width)x$($factory.height);count=$(@($factory.buildings).Count);buildings=$($buildingRecords -join ';')"
    $bytes = [Text.Encoding]::UTF8.GetBytes($canonical)
    $sha256 = [Security.Cryptography.SHA256]::Create()
    try {
        $hash = ([BitConverter]::ToString($sha256.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha256.Dispose()
    }
    $record = @(
        "selected_file=$($selected.File)"
        "checkpoint_id=$($checkpoint.checkpointId)"
        "generation=$($checkpoint.generation)"
        "departure_utc_unix_ms=$($checkpoint.departureUtcUnixMilliseconds)"
        "reconciled_from_checkpoint_id=$($checkpoint.reconciledFromCheckpointId)"
        "factory_state_sha256=$hash"
        "factory_state=$canonical"
    )
    $record | Set-Content -LiteralPath $OutputPath -Encoding utf8
    $record
}

function Save-Screenshot([string] $Name, [string] $OutputPath) {
    Invoke-Adb -Command @('shell', 'screencap', '-p', "/sdcard/$Name.png") | Out-Null
    Invoke-Adb -Command @('pull', "/sdcard/$Name.png", $OutputPath) | Out-Null
    Invoke-Adb -Command @('shell', 'rm', '-f', "/sdcard/$Name.png") | Out-Null
    "screenshot_sha256=$((Get-FileHash -Algorithm SHA256 -LiteralPath $OutputPath).Hash.ToLowerInvariant())"
}

function Assert-DeviceReady {
    $state = (& $Adb -s $Device get-state).Trim()
    if ($LASTEXITCODE -ne 0 -or $state -ne 'device') {
        throw "Expected $Device online; got '$state'."
    }
    if ($ExpectedApkSha256) {
        $apkPath = (Invoke-Adb -Command @('shell', 'pm', 'path', $Package)).Replace('package:', '').Trim()
        $installed = ((Invoke-Adb -Command @('shell', 'sha256sum', $apkPath)) -split '\s+')[0]
        if ($installed -ne $ExpectedApkSha256.ToLowerInvariant()) {
            throw "Installed APK SHA-256 $installed does not match $ExpectedApkSha256."
        }
        "installed_apk_sha256=$installed"
    }
}

if ($Phase -eq 'Run') {
    if ($Run -lt 1) { throw 'Run needs -Run.' }
    $common = @{ Run = $Run; Adb = $Adb; Device = $Device; Package = $Package; Activity = $Activity; RawRoot = $RawRoot; SourceCheckpoint = $SourceCheckpoint; ExpectedApkSha256 = $ExpectedApkSha256; WarmupSeconds = $WarmupSeconds; CaptureSeconds = $CaptureSeconds }
    & $PSCommandPath -Phase Prepare -AttachProfiler @common
    & $PSCommandPath -Phase Capture @common
    & $PSCommandPath -Phase Finish @common
    return
}

$runDirectory = Join-Path $RawRoot ($(if ($Phase -eq 'Unscored') { 'unscored' } else { "run-$Run" }))
New-Item -ItemType Directory -Force $runDirectory | Out-Null
$transcript = Join-Path $runDirectory "$($Phase.ToLowerInvariant())-transcript.txt"
Start-Transcript -LiteralPath $transcript -Force | Out-Null
try {
    switch ($Phase) {
        'Restore' {
            Restore-Checkpoint $SourceCheckpoint
            Stamp 'restored_utc'
            "restored_from=$SourceCheckpoint"
        }
        'Unscored' {
            Assert-DeviceReady
            Restore-Checkpoint $SourceCheckpoint
            Invoke-Adb -Command @('logcat', '-c') | Out-Null
            Stamp 'unscored_launch_utc'
            Invoke-Adb -Command @('shell', 'am', 'start', '-W', '-n', "$Package/$Activity")
            Start-Sleep -Seconds 60
            Invoke-Adb -Command @('logcat', '-d', '-v', 'threadtime') | Set-Content -LiteralPath (Join-Path $runDirectory 'logcat.txt') -Encoding utf8
            Select-String -LiteralPath (Join-Path $runDirectory 'logcat.txt') -Pattern 'Fully drawn' | ForEach-Object Line
            Invoke-Adb -Command @('shell', 'am', 'force-stop', $Package) | Out-Null
            Restore-Checkpoint $SourceCheckpoint
        }
        'Prepare' {
            if ($Run -lt 1) { throw 'Prepare needs -Run.' }
            Assert-DeviceReady
            "source_checkpoint=$SourceCheckpoint"
            Get-FileHash -Algorithm SHA256 (Get-ChildItem -LiteralPath $SourceCheckpoint -File).FullName | Sort-Object Path | ForEach-Object { "source_sha256 $([IO.Path]::GetFileName($_.Path))=$($_.Hash.ToLowerInvariant())" }
            Restore-Checkpoint $SourceCheckpoint
            Invoke-Adb -Command @('logcat', '-c') | Out-Null
            Stamp 'launch_utc'
            $coldStart = Invoke-Adb -Command @('shell', 'am', 'start', '-W', '-n', "$Package/$Activity")
            $coldStart | Set-Content -LiteralPath (Join-Path $runDirectory 'cold-start.txt') -Encoding utf8
            $coldStart
            $processId = (Invoke-Adb -Command @('shell', 'pidof', $Package)).Trim()
            if ([string]::IsNullOrWhiteSpace($processId)) { throw "Run $Run launch did not produce a process." }
            "pid=$processId"
            # The warm-up and every *_after_launch_seconds value count from the return of `am start -W`, as in T040.
            Stamp 'warmup_start_utc'
            $warmup = [Diagnostics.Stopwatch]::StartNew()
            if ($AttachProfiler) {
                # Attach the standalone profiler right after the launch, while the scene still loads: the T040 editor
                # attachment stalled the player in exactly this phase. The player must be a -noProfilerAutoconnect build.
                # Wait until the player listens: before that, adb accepts the forward and the connection drops at once.
                $listening = $null
                while (-not $listening -and $warmup.Elapsed.TotalSeconds -lt $WarmupSeconds) {
                    $listening = Invoke-Adb -Command @('logcat', '-d', '-s', 'Unity:D') | Select-String 'Player connection .*Started UDP target info broadcast' | Select-Object -First 1
                    if (-not $listening) { Start-Sleep -Milliseconds 250 }
                }
                if (-not $listening) { throw "Run ${Run}: the player did not start listening for a profiler within $WarmupSeconds s." }
                "player_listening=$($listening.Line.Trim())"
                "player_listening_seen_after_launch_seconds=$([Math]::Round($warmup.Elapsed.TotalSeconds, 1))"
                Invoke-Adb -Command @('forward', 'tcp:34999', "localabstract:Unity-$Package") | Out-Null
                $attached = $false
                for ($attempt = 1; $attempt -le 10 -and -not $attached; $attempt++) {
                    $line = & (Join-Path $PSScriptRoot 'profiler-mcp.ps1') -Snippet connect
                    "attach_attempt_$attempt=$line"
                    $attached = $line -match 'identifier=(?!Main Editor Process|Profiler-WindowsEditor)\S'
                    if (-not $attached) { Start-Sleep -Seconds 2 }
                }
                if (-not $attached) { throw "Run ${Run}: the standalone profiler did not attach to the player." }
                Stamp 'profiler_attached_utc'
                "profiler_attached_after_launch_seconds=$([Math]::Round($warmup.Elapsed.TotalSeconds, 1))"
            }
            while ($warmup.Elapsed.TotalSeconds -lt $WarmupSeconds) { Start-Sleep -Milliseconds 250 }
            Stamp 'warmup_end_utc'
            if ($AttachProfiler) {
                # The attached profiler must still hold the player when the capture-start state is recorded.
                $line = & (Join-Path $PSScriptRoot 'profiler-mcp.ps1') -Snippet status
                "profiler_status_at_capture_start=$line"
                if ($line -match 'identifier=(Main Editor Process|Profiler-WindowsEditor)') { throw "Run ${Run}: the profiler lost the player during the warm-up." }
            }
            $captureCheckpoint = Join-Path $runDirectory 'capture-start-checkpoint'
            New-Item -ItemType Directory -Force $captureCheckpoint | Out-Null
            Invoke-Adb -Command @('pull', $checkpointPath, $captureCheckpoint) | Out-Null
            Write-StateRecord -CheckpointDirectory (Join-Path $captureCheckpoint 'v1') -OutputPath (Join-Path $runDirectory 'capture-start-state.txt')
            Save-Screenshot "t064-run-$Run-start" (Join-Path $runDirectory 'capture-start.png')
            Invoke-Adb -Command @('logcat', '-d', '-v', 'threadtime') | Set-Content -LiteralPath (Join-Path $runDirectory 'pre-capture-logcat.txt') -Encoding utf8
            Select-String -LiteralPath (Join-Path $runDirectory 'pre-capture-logcat.txt') -Pattern 'Fully drawn' | ForEach-Object Line
            Stamp 'capture_start_ready_utc'
        }
        'Capture' {
            if ($Run -lt 1) { throw 'Capture needs -Run.' }
            $mcp = Join-Path $PSScriptRoot 'profiler-mcp.ps1'
            $outPrefix = [IO.Path]::GetFullPath((Join-Path $runDirectory 'profiler'))
            "pid=$((Invoke-Adb -Command @('shell', 'pidof', $Package)).Trim())"
            # Host processes during the capture: the main editor must be idle with its MCP bridge stopped.
            Get-CimInstance Win32_Process | Where-Object { $_.Name -match '^(Unity|adb|emulator|qemu-system-x86_64|python|mcp-for-unity)' } |
                ForEach-Object { "host_process $($_.ProcessId) $($_.Name) $(if ($_.CommandLine -match 'ump-process-role"? "?(\w+)') { "role=$($Matches[1])" })" }
            & $mcp -Snippet start-recording
            $streamWait = [Diagnostics.Stopwatch]::StartNew()
            $streaming = $false
            while (-not $streaming -and $streamWait.Elapsed.TotalSeconds -lt 30) {
                Start-Sleep -Seconds 1
                $status = & $mcp -Snippet status
                $streaming = $status -match ' last=(\d+) ' -and [int] $Matches[1] -ge 0
            }
            "stream_status=$status"
            "stream_first_frame_after_seconds=$([Math]::Round($streamWait.Elapsed.TotalSeconds, 1))"
            if (-not $streaming) { throw "Run ${Run}: no player frame arrived within 30 s of switching recording on." }
            & $mcp -Snippet clear-frames
            Stamp 'capture_start_utc'
            $stopwatch = [Diagnostics.Stopwatch]::StartNew()
            while ($stopwatch.Elapsed.TotalSeconds -lt $CaptureSeconds) { Start-Sleep -Milliseconds 250 }
            Stamp 'capture_end_utc'
            $export = & $mcp -Snippet stop-and-export -Out $outPrefix
            $export
            if ($export -match ' frames=0 ') { throw "Run ${Run}: the attached profiler recorded no player frames." }
            & $mcp -Snippet memory-snapshot -Out $outPrefix
            $status = "$outPrefix-snapshot-status.txt"
            $waited = [Diagnostics.Stopwatch]::StartNew()
            while (-not (Test-Path -LiteralPath $status) -and $waited.Elapsed.TotalSeconds -lt 180) { Start-Sleep -Seconds 1 }
            if (-not (Test-Path -LiteralPath $status)) { throw "Run ${Run}: memory snapshot did not finish within 180 s." }
            Get-Content -LiteralPath $status
            & $mcp -Snippet snapshot-residency -Out $outPrefix
        }
        'Finish' {
            if ($Run -lt 1) { throw 'Finish needs -Run.' }
            Stamp 'finish_utc'
            "pid=$((Invoke-Adb -Command @('shell', 'pidof', $Package)).Trim())"
            Save-Screenshot "t064-run-$Run-end" (Join-Path $runDirectory 'capture-end.png')
            Invoke-Adb -Command @('shell', 'dumpsys', 'meminfo', $Package) | Set-Content -LiteralPath (Join-Path $runDirectory 'meminfo-end.txt') -Encoding utf8
            Invoke-Adb -Command @('logcat', '-d', '-v', 'threadtime') | Set-Content -LiteralPath (Join-Path $runDirectory 'capture-logcat.txt') -Encoding utf8
            Select-String -LiteralPath (Join-Path $runDirectory 'capture-logcat.txt') -Pattern 'FATAL EXCEPTION|F DEBUG|SIGSEGV| E Unity|ActivityTaskManager: START' | ForEach-Object Line
            Invoke-Adb -Command @('shell', 'am', 'force-stop', $Package) | Out-Null
        }
    }
}
finally {
    Stop-Transcript | Out-Null
}
