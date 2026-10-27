<#
.SYNOPSIS
T410 helper: memory of the W003-S shipped device set running in the installed player (PSS by category, outside any timed window).
Pushes the committed set after one app launch (the app must create its save folders first), cold-starts it, waits for the
ResumeAndReconcile line, lets it run -SettleSeconds, then stores dumpsys meminfo and gfxinfo-free GL numbers.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $OutFile,
    [int] $SettleSeconds = 45,
    [string] $Package = 'com.DefaultCompany.idlefactorygame',
    [string] $AdbPath = (Join-Path $env:LOCALAPPDATA 'Android\Sdk\platform-tools\adb.exe')
)
$ErrorActionPreference = 'Continue'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$set = Join-Path $root 'Assets\IdleFactory\Tests\EditMode\Fixtures\SaveV2\device\w003-s-shipped'
$dir = "/sdcard/Android/data/$Package/files/IdleFactory/Checkpoints/v2"
function Adb { & $AdbPath -s emulator-5554 @args 2>&1 | Out-String }
Adb shell am force-stop $Package | Out-Null
Adb shell pm clear $Package | Out-Null
Adb shell monkey -p $Package -c android.intent.category.LAUNCHER 1 | Out-Null
Start-Sleep -Seconds 25
Adb shell am force-stop $Package | Out-Null
Adb shell rm -rf $dir | Out-Null
Adb shell mkdir -p $dir | Out-Null
foreach ($name in 'run-checkpoint.data-a.json', 'run-checkpoint.manifest-a.bin') { Adb push (Join-Path $set $name) "$dir/$name" | Out-Null }
Adb logcat -c | Out-Null
Adb shell monkey -p $Package -c android.intent.category.LAUNCHER 1 | Out-Null
$appPid = ''
for ($i = 0; $i -lt 40 -and -not $appPid; $i++) { Start-Sleep -Milliseconds 500; $appPid = (Adb shell pidof $Package).Trim() }
$reconcile = ''
for ($i = 0; $i -lt 60 -and -not $reconcile; $i++) {
    Start-Sleep -Seconds 2
    $log = Adb logcat -d --pid $appPid
    if ($log -match 'ResumeAndReconcile ms=(\d+)') { $reconcile = $Matches[0] }
}
Start-Sleep -Seconds $SettleSeconds
$mem = Adb shell dumpsys meminfo $Package
$pidAfter = (Adb shell pidof $Package).Trim()
@("pid=$appPid pidAfter=$pidAfter reconcile=[$reconcile] settle=${SettleSeconds}s", $mem) | Set-Content $OutFile
Write-Host "wrote $OutFile (reconcile: $reconcile)"
