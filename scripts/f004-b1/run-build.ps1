<#
.SYNOPSIS
T410 helper: builds the profiler-free development GLES3 Android player of the checked-out commit with the
measurement probe (scripts/f004-b1/B1BuildProbe.cs) copied in temporarily, then dumps the build report.

.PARAMETER Output   APK path.
.PARAMETER OutDir   Folder for build.log, shader-variants.tsv and build-report.tsv.
.PARAMETER ControlFrameRate30  Temporary 30 FPS control: comments the frame-rate call out for this build only, restored afterwards (git checkout).
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Output,
    [Parameter(Mandatory)] [string] $OutDir,
    [switch] $ControlFrameRate30,
    [string] $UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe'
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$OutDir = (Resolve-Path $OutDir).Path
$probeDir = Join-Path $root 'Assets\TempB1Probe\Editor'
$bootstrap = Join-Path $root 'Assets\IdleFactory\Unity\Bootstrap\CoreGameFoundationBootstrap.cs'
$shaderLog = Join-Path $OutDir 'shader-variants.tsv'
Remove-Item -LiteralPath $shaderLog -ErrorAction SilentlyContinue
try {
    New-Item -ItemType Directory -Force -Path $probeDir | Out-Null
    Copy-Item (Join-Path $PSScriptRoot 'B1BuildProbe.cs') $probeDir
    if ($ControlFrameRate30) {
        $text = [IO.File]::ReadAllText($bootstrap)
        $needle = 'Application.targetFrameRate = TargetFrameRate;'
        if (-not $text.Contains($needle)) { throw 'frame-rate call not found' }
        [IO.File]::WriteAllText($bootstrap, $text.Replace($needle, '// control build: no frame-rate code (implicit Android 30 FPS cap)'))
    }
    $env:B1_SHADER_LOG = $shaderLog
    $env:B1_REPORT_OUT = Join-Path $OutDir 'build-report.tsv'
    $apkDir = Split-Path -Parent $Output
    New-Item -ItemType Directory -Force -Path $apkDir | Out-Null
    & $UnityPath -batchmode -quit -projectPath $root -executeMethod IdleFactory.Editor.PlatformBuildCommands.BuildAndroidArm64DevelopmentGles3 -noProfilerAutoconnect -buildOutput $Output -logFile (Join-Path $OutDir 'build.log') | Out-Null
    $buildExit = $LASTEXITCODE
    Write-Host "build exit $buildExit"
    if ($buildExit -eq 0) {
        & $UnityPath -batchmode -quit -projectPath $root -executeMethod IdleFactory.B1Probe.B1ReportDump.DumpLatest -logFile (Join-Path $OutDir 'report-dump.log') | Out-Null
        Write-Host "dump exit $LASTEXITCODE"
    }
}
finally {
    if ($ControlFrameRate30) { git -C $root checkout -- 'Assets/IdleFactory/Unity/Bootstrap/CoreGameFoundationBootstrap.cs' }
    Remove-Item -LiteralPath (Join-Path $root 'Assets\TempB1Probe') -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath (Join-Path $root 'Assets\TempB1Probe.meta') -Force -ErrorAction SilentlyContinue
    Remove-Item Env:B1_SHADER_LOG, Env:B1_REPORT_OUT -ErrorAction SilentlyContinue
}
