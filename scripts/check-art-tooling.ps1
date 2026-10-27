<#
.SYNOPSIS
  Preflight for the F004 art tooling: uv, Blender (BLENDER_EXE), ComfyUI reachability.

.DESCRIPTION
  Each tool reports one of three states:
    OK                 found and, where pinned, at the expected version
    tooling unavailable  not installed / not reachable. Blocks only art-production Issues (research R14),
                       never the pipeline tests on committed intermediates.
    FAIL               present but wrong (e.g. Blender version differs from the pin)
  Exit code: 0 all OK, 1 at least one FAIL, 2 no FAIL but something is unavailable.
  -RequireComfy makes an unreachable ComfyUI count as unavailable (default: reported, still exit 2 only
  when -RequireComfy is set); ComfyUI is optional for Blender-only work.

.PARAMETER BlenderExe
  Defaults to $env:BLENDER_EXE, else C:\Program Files\Blender Foundation\Blender 5.2\blender.exe.
#>
[CmdletBinding()]
param(
  [string]$BlenderExe = $env:BLENDER_EXE,
  [string]$ComfyUrl = 'http://127.0.0.1:8188',
  [string]$PinnedBlender = '5.2.1',
  [switch]$RequireComfy
)
$ErrorActionPreference = 'Stop'
if (-not $BlenderExe) { $BlenderExe = 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' }

$fail = 0; $unavailable = 0
function Report([string]$Tool, [string]$State, [string]$Detail) {
  Write-Output ('{0,-8} {1,-18} {2}' -f $Tool, $State, $Detail)
}

# uv
$uv = Get-Command uv -ErrorAction SilentlyContinue
if (-not $uv -and (Test-Path 'C:\Users\Domenik\.local\bin\uv.exe')) { $uv = Get-Item 'C:\Users\Domenik\.local\bin\uv.exe' }
if ($uv) {
  $path = if ($uv.PSObject.Properties['Source']) { $uv.Source } else { $uv.FullName }
  Report 'uv' 'OK' ((& $path --version) + " ($path)")
} else { Report 'uv' 'tooling unavailable' 'uv not on PATH'; $unavailable++ }

# Blender
if (Test-Path -LiteralPath $BlenderExe -PathType Leaf) {
  $line = (& $BlenderExe --version 2>$null | Select-Object -First 1)
  if ($line -match '(\d+\.\d+\.\d+)') {
    if ($Matches[1] -eq $PinnedBlender) { Report 'blender' 'OK' "$line ($BlenderExe)" }
    else { Report 'blender' 'FAIL' "found $($Matches[1]), pinned $PinnedBlender ($BlenderExe)"; $fail++ }
  } else { Report 'blender' 'FAIL' "could not read version from '$line'"; $fail++ }
} else { Report 'blender' 'tooling unavailable' "BLENDER_EXE not found: $BlenderExe"; $unavailable++ }

# ComfyUI: /system_stats is the HTTP source of the MCP server_info 'running' flag.
try {
  $s = Invoke-RestMethod -Uri "$ComfyUrl/system_stats" -TimeoutSec 5
  Report 'comfyui' 'OK' ("reachable at $ComfyUrl, ComfyUI " + $s.system.comfyui_version)
} catch {
  Report 'comfyui' 'tooling unavailable' "not reachable at $ComfyUrl (start Comfy Desktop)"
  if ($RequireComfy) { $unavailable++ }
}

if ($fail -gt 0) { exit 1 }
if ($unavailable -gt 0) { Write-Output 'Result: tooling unavailable (blocks art-production Issues only)'; exit 2 }
Write-Output 'Result: OK'
exit 0
