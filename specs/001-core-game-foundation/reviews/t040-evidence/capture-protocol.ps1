[CmdletBinding()]
param(
    [string] $Adb = 'C:\Users\Domenik\AppData\Local\Android\Sdk\platform-tools\adb.exe',
    [string] $Device = 'emulator-5554',
    [string] $Package = 'com.DefaultCompany.idlefactorygame',
    [string] $Activity = 'com.idlefactory.player.IdleFactoryGameActivity',
    [string] $RawRoot = 'test-results/android/t040',
    [string] $SourceCheckpoint = 'test-results/android/t040/original-device-profile/v1'
)

$ErrorActionPreference = 'Stop'
$checkpointPath = "/storage/emulated/0/Android/data/$Package/files/IdleFactory/Checkpoints/v1"

function Invoke-Adb {
    param([string[]] $Command)
    & $Adb -s $Device @Command
    if ($LASTEXITCODE -ne 0) {
        throw "ADB failed ($LASTEXITCODE): $($Command -join ' ')"
    }
}

function Restore-Checkpoint {
    Invoke-Adb -Command @('shell', 'am', 'force-stop', $Package) | Out-Null
    Invoke-Adb -Command @('shell', 'rm', '-f', "$checkpointPath/factory-checkpoint.data-a.json", "$checkpointPath/factory-checkpoint.data-b.json", "$checkpointPath/factory-checkpoint.manifest-a.bin", "$checkpointPath/factory-checkpoint.manifest-b.bin", "$checkpointPath/factory-checkpoint.pending.json") | Out-Null
    Invoke-Adb -Command @('shell', 'mkdir', '-p', $checkpointPath) | Out-Null
    Get-ChildItem -LiteralPath $SourceCheckpoint -File | Sort-Object Name | ForEach-Object {
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
    @(
        "selected_file=$($selected.File)"
        "checkpoint_id=$($checkpoint.checkpointId)"
        "generation=$($checkpoint.generation)"
        "departure_utc_unix_ms=$($checkpoint.departureUtcUnixMilliseconds)"
        "reconciled_from_checkpoint_id=$($checkpoint.reconciledFromCheckpointId)"
        "factory_state_sha256=$hash"
        "factory_state=$canonical"
    ) | Set-Content -LiteralPath $OutputPath -Encoding utf8
}

if (-not (Test-Path -LiteralPath $SourceCheckpoint)) {
    throw "Source checkpoint not found: $SourceCheckpoint"
}

$deviceState = (& $Adb -s $Device get-state).Trim()
if ($LASTEXITCODE -ne 0 -or $deviceState -ne 'device') {
    throw "Expected $Device online; got '$deviceState'."
}

$sourceFiles = Get-ChildItem -LiteralPath $SourceCheckpoint -File
if ($sourceFiles.Count -ne 5) {
    throw "Expected five source checkpoint files; found $($sourceFiles.Count)."
}

$transcript = Join-Path $RawRoot 'capture-transcript.txt'
Start-Transcript -LiteralPath $transcript -Force
try {
    "protocol_started_utc=$([DateTime]::UtcNow.ToString('o'))"
    "source_checkpoint=$SourceCheckpoint"
    Get-FileHash -Algorithm SHA256 $sourceFiles.FullName | Sort-Object Path | Format-Table Path, Hash -AutoSize

    foreach ($run in 1..3) {
        $runDirectory = Join-Path $RawRoot "run-$run"
        $captureCheckpoint = Join-Path $runDirectory 'capture-start-checkpoint'
        New-Item -ItemType Directory -Force $captureCheckpoint | Out-Null
        "run_${run}_restore_utc=$([DateTime]::UtcNow.ToString('o'))"
        Restore-Checkpoint

        Invoke-Adb -Command @('logcat', '-c') | Out-Null
        $coldStart = Invoke-Adb -Command @('shell', 'am', 'start', '-W', '-n', "$Package/$Activity")
        $coldStart | Set-Content -LiteralPath (Join-Path $runDirectory 'cold-start.txt') -Encoding utf8
        $processId = (Invoke-Adb -Command @('shell', 'pidof', $Package)).Trim()
        if ([string]::IsNullOrWhiteSpace($processId)) {
            throw "Run $run launch did not produce a process."
        }
        "run_${run}_pid=$processId"
        "run_${run}_warmup_start_utc=$([DateTime]::UtcNow.ToString('o'))"
        Start-Sleep -Seconds 30
        "run_${run}_warmup_end_utc=$([DateTime]::UtcNow.ToString('o'))"

        Invoke-Adb -Command @('pull', $checkpointPath, $captureCheckpoint) | Out-Null
        $pulledV1 = Join-Path $captureCheckpoint 'v1'
        Write-StateRecord -CheckpointDirectory $pulledV1 -OutputPath (Join-Path $runDirectory 'capture-start-state.txt')
        Invoke-Adb -Command @('shell', 'screencap', '-p', "/sdcard/t040-run-$run-start.png") | Out-Null
        Invoke-Adb -Command @('pull', "/sdcard/t040-run-$run-start.png", (Join-Path $runDirectory 'capture-start.png')) | Out-Null
        Invoke-Adb -Command @('shell', 'rm', '-f', "/sdcard/t040-run-$run-start.png") | Out-Null

        Invoke-Adb -Command @('logcat', '-d', '-v', 'threadtime') | Set-Content -LiteralPath (Join-Path $runDirectory 'pre-capture-logcat.txt') -Encoding utf8
        Invoke-Adb -Command @('logcat', '-c') | Out-Null
        Invoke-Adb -Command @('shell', 'dumpsys', 'gfxinfo', $Package, 'reset') | Set-Content -LiteralPath (Join-Path $runDirectory 'gfxinfo-reset.txt') -Encoding utf8
        Invoke-Adb -Command @('shell', 'dumpsys', 'meminfo', $Package) | Set-Content -LiteralPath (Join-Path $runDirectory 'meminfo-start.txt') -Encoding utf8
        "run_${run}_capture_start_utc=$([DateTime]::UtcNow.ToString('o'))"
        $captureStopwatch = [Diagnostics.Stopwatch]::StartNew()
        Invoke-Adb -Command @('shell', 'top', '-b', '-d', '5', '-n', '13', '-p', $processId) | Set-Content -LiteralPath (Join-Path $runDirectory 'cpu-top.txt') -Encoding utf8
        $captureStopwatch.Stop()
        "run_${run}_capture_end_utc=$([DateTime]::UtcNow.ToString('o'))"
        "run_${run}_capture_elapsed_seconds=$([Math]::Round($captureStopwatch.Elapsed.TotalSeconds, 3))"

        Invoke-Adb -Command @('shell', 'dumpsys', 'gfxinfo', $Package, 'framestats') | Set-Content -LiteralPath (Join-Path $runDirectory 'gfxinfo-framestats.txt') -Encoding utf8
        Invoke-Adb -Command @('shell', 'dumpsys', 'meminfo', $Package) | Set-Content -LiteralPath (Join-Path $runDirectory 'meminfo-end.txt') -Encoding utf8
        Invoke-Adb -Command @('shell', 'dumpsys', 'cpuinfo') | Set-Content -LiteralPath (Join-Path $runDirectory 'cpuinfo-end.txt') -Encoding utf8
        Invoke-Adb -Command @('logcat', '-d', '-v', 'threadtime') | Set-Content -LiteralPath (Join-Path $runDirectory 'capture-logcat.txt') -Encoding utf8
        Invoke-Adb -Command @('shell', 'screencap', '-p', "/sdcard/t040-run-$run-end.png") | Out-Null
        Invoke-Adb -Command @('pull', "/sdcard/t040-run-$run-end.png", (Join-Path $runDirectory 'capture-end.png')) | Out-Null
        Invoke-Adb -Command @('shell', 'rm', '-f', "/sdcard/t040-run-$run-end.png") | Out-Null

        Invoke-Adb -Command @('shell', 'input', 'keyevent', 'KEYCODE_HOME') | Out-Null
        Start-Sleep -Seconds 2
        Invoke-Adb -Command @('shell', 'am', 'start', '-W', '-n', "$Package/$Activity") | Set-Content -LiteralPath (Join-Path $runDirectory 'warm-start.txt') -Encoding utf8
        "run_${run}_warm_start_pid=$((Invoke-Adb -Command @('shell', 'pidof', $Package)).Trim())"
        Invoke-Adb -Command @('shell', 'am', 'force-stop', $Package) | Out-Null
    }
}
finally {
    try {
        Restore-Checkpoint
        "original_profile_restored_utc=$([DateTime]::UtcNow.ToString('o'))"
    }
    finally {
        Stop-Transcript
    }
}
