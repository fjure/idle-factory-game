<#
.SYNOPSIS
T410 helper: samples the game process on the emulator while a measurement session runs elsewhere.
Every -CpuSeconds it reads /proc/<pid>/stat (utime+stime ticks); every -MemSeconds it stores dumpsys meminfo.
Rows carry the host epoch milliseconds so a capture window (from file timestamps) can be cut out afterwards.
Stop it with Ctrl+C / Stop-Process, or it ends after -MaxMinutes.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $OutDir,
    [string] $Package = 'com.DefaultCompany.idlefactorygame',
    [int] $CpuSeconds = 5,
    [int] $MemSeconds = 100000,   # dumpsys meminfo forces a GC in the player: sample it only once, never inside timed windows
    [int] $MaxMinutes = 120,
    [string] $AdbPath = (Join-Path $env:LOCALAPPDATA 'Android\Sdk\platform-tools\adb.exe')
)
$ErrorActionPreference = 'Continue'
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$cpuFile = Join-Path $OutDir 'cpu-samples.tsv'
$memFile = Join-Path $OutDir 'meminfo-samples.txt'
"epoch_ms`tpid`tticks`thost_busy_threads`tgame_procs" | Set-Content $cpuFile
$prevHostCpu = (Get-Process | Measure-Object CPU -Sum).Sum
$prevHostAt = Get-Date
$end = (Get-Date).AddMinutes($MaxMinutes)
$nextMem = Get-Date
while ((Get-Date) -lt $end) {
    $now = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    $hostCpu = (Get-Process | Measure-Object CPU -Sum).Sum
    $hostAt = Get-Date
    $busy = [math]::Round(($hostCpu - $prevHostCpu) / [math]::Max(0.001, ($hostAt - $prevHostAt).TotalSeconds), 2)
    $prevHostCpu = $hostCpu
    $prevHostAt = $hostAt
    $procId = (& $AdbPath -s emulator-5554 shell pidof $Package 2>$null | Out-String).Trim()
    if ($procId -match '^\d+$') {
        $stat = (& $AdbPath -s emulator-5554 shell cat "/proc/$procId/stat" 2>$null | Out-String).Trim()
        if ($stat -match '\) \S+ (?:\S+ ){10}(\d+) (\d+)') {
            $ticks = [long]$Matches[1] + [long]$Matches[2]
            $games = @(Get-Process -Name TFTClient-Win64-Shipping,LeagueofLegends,VALORANT-Win64-Shipping,Cyberpunk2077,eldenring -ErrorAction SilentlyContinue).Count
            "$now`t$procId`t$ticks`t$busy`t$games" | Add-Content $cpuFile
        }
        if ((Get-Date) -ge $nextMem) {
            $mem = & $AdbPath -s emulator-5554 shell dumpsys meminfo $Package 2>$null | Out-String
            "=== epoch_ms=$now pid=$procId`n$mem" | Add-Content $memFile
            $nextMem = (Get-Date).AddSeconds($MemSeconds)
        }
    }
    Start-Sleep -Seconds $CpuSeconds
}
