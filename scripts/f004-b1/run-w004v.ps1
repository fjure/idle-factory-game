<#
.SYNOPSIS
T410 helper: one GRAPHICAL (not batch) Unity editor run of the throwaway probe test B1W004VProbeTests on the checked-out commit.
The probe is copied into Assets/IdleFactory/Tests/PlayMode/ for the run and removed afterwards, so the committed test manifest is unchanged.
Needs a real GPU (the batch editor reports 0 for every render counter). Close every other Unity editor first.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $OutDir,
    [int] $TimeoutSeconds = 1200,
    [string] $UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe'
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$OutDir = (Resolve-Path $OutDir).Path
$target = Join-Path $root 'Assets\IdleFactory\Tests\PlayMode\B1W004VProbeTests.cs'
$result = Join-Path $OutDir 'w004v-results.xml'
$log = Join-Path $OutDir 'w004v-editor.log'
Remove-Item -LiteralPath $result -ErrorAction SilentlyContinue
try {
    Copy-Item (Join-Path $PSScriptRoot 'B1W004VProbeTests.cs') $target
    $proc = Start-Process -FilePath $UnityPath -PassThru -ArgumentList @('-projectPath', "`"$root`"", '-runTests', '-testPlatform', 'PlayMode', '-testFilter', 'B1W004VProbeTests', '-testResults', "`"$result`"", '-logFile', "`"$log`"")
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline -and -not (Test-Path $result) -and -not $proc.HasExited) { Start-Sleep -Seconds 5 }
    Start-Sleep -Seconds 5
    if (-not $proc.HasExited) { Stop-Process -Id $proc.Id -Force }
    Write-Host "result file: $(Test-Path $result)"
}
finally {
    Remove-Item -LiteralPath $target -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath "$target.meta" -Force -ErrorAction SilentlyContinue
}
