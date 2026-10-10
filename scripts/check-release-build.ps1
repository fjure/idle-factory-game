<#
.SYNOPSIS
Verifies that a release build and the committed ProjectSettings contain no DevTools.

.DESCRIPTION
Security/DevOps guard for F005 (research F5-R18, SEC-6, OPS-3, OPS-4). The DevTools
assembly (IdleFactory.DevTools, define constraint IDLEFACTORY_DEV) must never reach a
shipped build. Checks:

  ProjectSettings (every invocation; path relative to the repo root)
    runInBackground        ProjectSettings.asset must contain `runInBackground: 0`.
    dev-define             No IDLEFACTORY_DEV anywhere in ProjectSettings.asset (this
                           covers every scripting-define entry). The define is applied
                           only through BuildPlayerOptions.extraScriptingDefines.

  Build (when -BuildPath is given)
    Windows player folder  Detected by a `<Name>_Data` folder inside -BuildPath (or
                           -BuildPath being that folder). Checks:
                             - Managed/ lists IdleFactory.DevTools*.dll (Mono)
                             - il2cpp_data/Metadata/global-metadata.dat (IL2CPP), if
                               present, is scanned like the Android metadata
                             - `<Name>_Data/boot.config` must not carry
                               `player-connection-debug=1` (marker of a development
                               build); a development build is a finding because the
                               scan targets release builds.
    Android .apk           Opened with System.IO.Compression and read in memory (no
                           extraction to disk). Checks:
                             - assets/bin/Data/Managed/Metadata/global-metadata.dat
                               is scanned as bytes for the DevTools assembly name and
                               type names (UTF-8/ASCII)
                             - any `*IdleFactory.DevTools*` entry name in the archive
                             - assets/bin/Data/boot.config must not carry
                               `player-connection-debug=1`
                             - a missing global-metadata.dat is a finding (unknown
                               layout; the scan could not run)

  DevTools type names are parsed at run time from Assets/IdleFactory/DevTools/*.cs
  (class/struct/enum/interface declarations); the assembly name is read from
  IdleFactory.DevTools.asmdef, with the documented fallback list used when the sources
  are unavailable.

  Source defense in depth (every invocation, relative to the repo root)
    IDLEFACTORY_DEV in Assets/**/*.rsp, Assets/**/*.asmdef and Directory.Build.* at the
    repo root is a finding. Exempt: the `defineConstraints` array (only that array) of
    Assets/IdleFactory/DevTools/IdleFactory.DevTools.asmdef and
    Assets/IdleFactory/Tests/DevTools/IdleFactory.DevToolsTests.asmdef. A versionDefines
    entry that produces IDLEFACTORY_DEV is still a finding.

  Scope and limits
    - A development player always fails (boot.config marker); this script is for
      release builds only.
    - Windows Mono: every Managed/*.dll is also scanned byte-wise for the DevTools
      assembly and type names (whole identifiers), so DevTools compiled into another
      assembly is caught; the file-name check stays.
    - Native binaries (GameAssembly.dll, libil2cpp.so) are not scanned; that is deferred
      to T339/T350. IL2CPP coverage comes from global-metadata.dat.
    - -ProjectSettingsPath and -RepoRoot are test hooks. Release runs must not use them.
    - Any unexpected error (for example a corrupt APK) is reported as
      `FINDING [error] ...` with exit 1.

  Manifest checks (backup exclusions) are added by T337 and are not part of this script.

Output: one line per finding, prefixed `FINDING [<code>]`, then a summary line.
Exit codes: 0 no findings, 1 one or more findings (including a missing path or an
unknown layout).

.PARAMETER BuildPath
Windows player folder or Android .apk to scan. Optional with -ProjectSettingsOnly.

.PARAMETER ProjectSettingsOnly
Run only the ProjectSettings guards.

.PARAMETER ProjectSettingsPath
Override the ProjectSettings.asset file to check (used by tests with synthetic copies).

.EXAMPLE
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check-release-build.ps1 -BuildPath Builds\Win64

.EXAMPLE
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check-release-build.ps1 -ProjectSettingsOnly
#>
[CmdletBinding()]
param(
    [string]$BuildPath,
    [switch]$ProjectSettingsOnly,
    [string]$ProjectSettingsPath,
    [string]$RepoRoot
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

# PowerShell variables are case-insensitive: $repoRoot is the -RepoRoot parameter.
if ([string]::IsNullOrWhiteSpace($RepoRoot)) { $repoRoot = Split-Path -Parent $PSScriptRoot }
$findings = New-Object System.Collections.Generic.List[string]

function Add-Finding {
    param([string]$Code, [string]$Message)
    $findings.Add($Message) | Out-Null
    Write-Output ("FINDING [{0}] {1}" -f $Code, $Message)
}

function Get-DevToolsNames {
    $assembly = 'IdleFactory.DevTools'
    $types = @('DevTimeScale', 'DevToolsOverlay', 'DevToolsRuntime')
    $dir = Join-Path $repoRoot 'Assets/IdleFactory/DevTools'
    if (Test-Path -LiteralPath $dir) {
        $asmdef = Join-Path $dir 'IdleFactory.DevTools.asmdef'
        if (Test-Path -LiteralPath $asmdef) {
            $m = [regex]::Match((Get-Content -LiteralPath $asmdef -Raw), '"name"\s*:\s*"([^"]+)"')
            if ($m.Success) { $assembly = $m.Groups[1].Value }
        }
        $parsed = New-Object System.Collections.Generic.List[string]
        foreach ($f in @(Get-ChildItem -LiteralPath $dir -Filter '*.cs' -File -Recurse)) {
            $text = Get-Content -LiteralPath $f.FullName -Raw
            foreach ($m in [regex]::Matches($text, '(?:class|struct|enum|interface|record)\s+([A-Za-z_][A-Za-z0-9_]*)')) {
                $parsed.Add($m.Groups[1].Value) | Out-Null
            }
        }
        $types = @(@($types) + @($parsed) | Select-Object -Unique)
    }
    return [pscustomobject]@{ Assembly = $assembly; Types = $types }
}

function Find-NamesInBytes {
    param([byte[]]$Bytes, [string[]]$Names)
    # Latin1 maps each byte to one char, so ASCII/UTF-8 identifier strings match exactly.
    # Names must be whole identifiers, so ITimeScaleSource never matches DevTimeScale etc.
    $text = [System.Text.Encoding]::GetEncoding(28591).GetString($Bytes)
    $hits = @()
    foreach ($n in $Names) {
        $pattern = '(?<![A-Za-z0-9_])' + [regex]::Escape($n) + '(?![A-Za-z0-9_])'
        if ([regex]::IsMatch($text, $pattern)) { $hits += $n }
    }
    return ,$hits
}

function Test-BootConfigDev {
    param([string]$Text, [string]$Where)
    if ($Text -match '(?m)^\s*player-connection-debug\s*=\s*1\s*$') {
        Add-Finding 'development-build' ("{0} has player-connection-debug=1: this is a development build, not a release build" -f $Where)
    }
}

try {
# ---- ProjectSettings guards --------------------------------------------------
if ([string]::IsNullOrWhiteSpace($ProjectSettingsPath)) {
    $ProjectSettingsPath = Join-Path $repoRoot 'ProjectSettings/ProjectSettings.asset'
}
if (-not (Test-Path -LiteralPath $ProjectSettingsPath -PathType Leaf)) {
    Add-Finding 'settings-missing' ("ProjectSettings file not found: {0}" -f $ProjectSettingsPath)
} else {
    $settings = Get-Content -LiteralPath $ProjectSettingsPath -Raw
    $rib = [regex]::Matches($settings, '(?m)^\s*runInBackground:\s*(\S+)\s*$')
    if ($rib.Count -eq 0) {
        Add-Finding 'run-in-background' 'runInBackground entry not found in ProjectSettings.asset (expected runInBackground: 0)'
    } else {
        foreach ($m in $rib) {
            if ($m.Groups[1].Value -ne '0') {
                Add-Finding 'run-in-background' ("ProjectSettings.asset has runInBackground: {0} (expected 0)" -f $m.Groups[1].Value)
            }
        }
    }
    if ($settings -match 'IDLEFACTORY_DEV') {
        Add-Finding 'dev-define' 'ProjectSettings.asset contains IDLEFACTORY_DEV (scripting define committed by hand; apply it only via BuildPlayerOptions.extraScriptingDefines)'
    }
}

# ---- Source defense in depth ---------------------------------------------------
$exempt = @('Assets/IdleFactory/DevTools/IdleFactory.DevTools.asmdef', 'Assets/IdleFactory/Tests/DevTools/IdleFactory.DevToolsTests.asmdef')
$candidates = @()
$assetsDir = Join-Path $repoRoot 'Assets'
if (Test-Path -LiteralPath $assetsDir) {
    foreach ($f in @(Get-ChildItem -LiteralPath $assetsDir -File -Recurse | Where-Object { $_.Extension -ieq '.rsp' -or $_.Extension -ieq '.asmdef' })) { $candidates += $f }
}
foreach ($f in @(Get-ChildItem -LiteralPath $repoRoot -File -Filter 'Directory.Build.*')) { $candidates += $f }
foreach ($f in $candidates) {
    $rel = $f.FullName.Substring($repoRoot.TrimEnd([char]92, [char]47).Length).TrimStart([char]92, [char]47).Replace([string][char]92, '/')
    $text = Get-Content -LiteralPath $f.FullName -Raw
    if ($exempt -contains $rel) {
        $text = [regex]::Replace($text, '"defineConstraints"\s*:\s*\[[^\]]*\]', '')
    }
    if ($text -match 'IDLEFACTORY_DEV') {
        Add-Finding 'dev-define-source' ("{0} contains IDLEFACTORY_DEV outside an exempt defineConstraints array" -f $rel)
    }
}

# ---- Build scan --------------------------------------------------------------
if (-not $ProjectSettingsOnly) {
    if ([string]::IsNullOrWhiteSpace($BuildPath)) {
        Add-Finding 'build-path' '-BuildPath is required unless -ProjectSettingsOnly is given'
    } elseif (-not (Test-Path -LiteralPath $BuildPath)) {
        Add-Finding 'build-path' ("Build path not found: {0}" -f $BuildPath)
    } else {
        $names = Get-DevToolsNames
        $allNames = @($names.Assembly) + @($names.Types)
        $item = Get-Item -LiteralPath $BuildPath

        if (-not $item.PSIsContainer -and $item.Extension -ieq '.apk') {
            $zip = [System.IO.Compression.ZipFile]::OpenRead($item.FullName)
            try {
                foreach ($e in @($zip.Entries)) {
                    if ($e.FullName -like ('*' + $names.Assembly + '*')) {
                        Add-Finding 'devtools-entry' ("APK contains entry {0}" -f $e.FullName)
                    }
                }
                # Real APKs use '/'; tolerate '' written by Windows zip tools.
                $meta = @($zip.Entries | Where-Object { $_.FullName.Replace([string][char]92, '/') -eq 'assets/bin/Data/Managed/Metadata/global-metadata.dat' }) | Select-Object -First 1
                if ($null -eq $meta) {
                    Add-Finding 'layout' 'APK has no assets/bin/Data/Managed/Metadata/global-metadata.dat (unknown layout or Mono build); metadata scan could not run'
                } else {
                    $ms = New-Object System.IO.MemoryStream
                    $s = $meta.Open()
                    try { $s.CopyTo($ms) } finally { $s.Dispose() }
                    $hits = Find-NamesInBytes -Bytes $ms.ToArray() -Names $allNames
                    foreach ($h in @($hits)) {
                        Add-Finding 'devtools-metadata' ("global-metadata.dat contains DevTools name '{0}'" -f $h)
                    }
                }
                $boot = @($zip.Entries | Where-Object { $_.FullName.Replace([string][char]92, '/') -eq 'assets/bin/Data/boot.config' }) | Select-Object -First 1
                if ($null -ne $boot) {
                    $sr = New-Object System.IO.StreamReader($boot.Open())
                    try { Test-BootConfigDev -Text $sr.ReadToEnd() -Where 'APK assets/bin/Data/boot.config' } finally { $sr.Dispose() }
                }
            } finally { $zip.Dispose() }
        } elseif ($item.PSIsContainer) {
            $dataDirs = @()
            if ($item.Name -like '*_Data') { $dataDirs += $item }
            else { $dataDirs += @(Get-ChildItem -LiteralPath $item.FullName -Directory -Filter '*_Data') }
            if (@($dataDirs).Count -eq 0) {
                Add-Finding 'layout' ("No <Name>_Data folder under {0}: not a Windows player folder" -f $item.FullName)
            }
            foreach ($d in @($dataDirs)) {
                $managed = Join-Path $d.FullName 'Managed'
                $il2cppMeta = Join-Path $d.FullName 'il2cpp_data/Metadata/global-metadata.dat'
                $hasManaged = Test-Path -LiteralPath $managed -PathType Container
                $hasMeta = Test-Path -LiteralPath $il2cppMeta -PathType Leaf
                if (-not $hasManaged -and -not $hasMeta) {
                    Add-Finding 'layout' ("{0} has neither Managed/ nor il2cpp_data/Metadata/global-metadata.dat" -f $d.FullName)
                }
                if ($hasManaged) {
                    foreach ($f in @(Get-ChildItem -LiteralPath $managed -File -Filter ('*' + $names.Assembly + '*'))) {
                        Add-Finding 'devtools-assembly' ("Managed/ contains {0}" -f $f.FullName)
                    }
                    foreach ($f in @(Get-ChildItem -LiteralPath $managed -File -Filter '*.dll')) {
                        $hits = Find-NamesInBytes -Bytes ([System.IO.File]::ReadAllBytes($f.FullName)) -Names $allNames
                        foreach ($h in @($hits)) {
                            Add-Finding 'devtools-managed' ("{0} contains DevTools name '{1}'" -f $f.FullName, $h)
                        }
                    }
                }
                if ($hasMeta) {
                    $hits = Find-NamesInBytes -Bytes ([System.IO.File]::ReadAllBytes($il2cppMeta)) -Names $allNames
                    foreach ($h in @($hits)) {
                        Add-Finding 'devtools-metadata' ("global-metadata.dat contains DevTools name '{0}'" -f $h)
                    }
                }
                $bootFile = Join-Path $d.FullName 'boot.config'
                if (Test-Path -LiteralPath $bootFile -PathType Leaf) {
                    Test-BootConfigDev -Text (Get-Content -LiteralPath $bootFile -Raw) -Where $bootFile
                }
            }
        } else {
            Add-Finding 'layout' ("Unsupported build path (expected a player folder or .apk): {0}" -f $item.FullName)
        }
    }
}

} catch {
    Add-Finding 'error' ($_.Exception.Message)
}

$count = @($findings).Count
if ($count -gt 0) {
    Write-Output ("check-release-build: FAIL, {0} finding(s)" -f $count)
    exit 1
}
Write-Output 'check-release-build: PASS, 0 findings'
exit 0
