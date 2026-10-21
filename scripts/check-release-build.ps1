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

  Android backup (T339, FR-031, D-M) - for every .apk build, and for -ManifestPath
    The merged manifest is read with `aapt2 dump xmltree` from the APK (SDK from
    -AndroidSdkPath, ANDROID_SDK_ROOT, ANDROID_HOME or %LOCALAPPDATA%\Android\Sdk); when
    aapt2 is unavailable the newest `*merged_manifest*/AndroidManifest.xml` under
    -GradleOutPath (Unity's Temp/gradleOut) is used and reported as possibly stale.
      - allowBackup must not be false
      - android:fullBackupContent and android:dataExtractionRules must both be present
      - no FileProvider provider (D-M)
      - the referenced XML (resolved from the APK resource table, or -RulesRoot / gradleOut by
        `@xml/<name>`) must exclude SessionLog, Exports, IdleFactory/SessionLog and IdleFactory/Exports (the real
        persistence root is <persistentDataPath>/IdleFactory) in the file and external domains,
        in full-backup-content and in both cloud-backup and device-transfer; an <include>
        of '.', SessionLog or Exports is a finding
      - rules that cannot be read from the build output are a finding (unverified)
    -ManifestPath checks an explicit merged manifest (XML or an aapt2 xmltree dump) with
    -RulesRoot as the folder of the referenced XML files; it is also a test hook.

Output: one line per finding, prefixed `FINDING [<code>]`, then a summary line.
Exit codes: 0 no findings, 1 one or more findings (including a missing path or an
unknown layout).

.PARAMETER BuildPath
Windows player folder or Android .apk to scan. Optional with -ProjectSettingsOnly.

.PARAMETER ProjectSettingsOnly
Run only the ProjectSettings guards.

.PARAMETER AndroidSdkPath
Android SDK root that holds build-tools/<version>/aapt2 (optional; see the Android backup section).

.PARAMETER GradleOutPath
Unity's Gradle output folder (Temp/gradleOut) used when aapt2 is unavailable.

.PARAMETER ManifestPath
Explicit merged manifest (XML or aapt2 xmltree dump) to check; also a test hook.

.PARAMETER RulesRoot
Folder of the backup-rules XML files referenced by -ManifestPath (`@xml/<name>` resolves to <name>.xml).

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
    [string]$RepoRoot,
    [string]$AndroidSdkPath,
    [string]$GradleOutPath,
    [string]$ManifestPath,
    [string]$RulesRoot
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

# ---- Android backup (T339, FR-031, D-M) ---------------------------------------------
# Node model shared by both sources: @{ Name; Depth; Attrs = @{ localName = value } }.
function ConvertFrom-XmlText {
    param([string]$Text)
    $doc = New-Object System.Xml.XmlDocument
    $doc.LoadXml($Text)
    $nodes = New-Object System.Collections.Generic.List[object]
    $walk = $null
    $walk = {
        param($el, $depth)
        $attrs = @{}
        foreach ($a in $el.Attributes) { $attrs[$a.LocalName] = $a.Value }
        $nodes.Add(@{ Name = $el.LocalName; Depth = $depth; Attrs = $attrs }) | Out-Null
        foreach ($c in $el.ChildNodes) { if ($c.NodeType -eq 'Element') { & $walk $c ($depth + 1) } }
    }
    & $walk $doc.DocumentElement 0
    return ,$nodes
}

function ConvertFrom-Aapt2XmlTree {
    param([string]$Text)
    $nodes = New-Object System.Collections.Generic.List[object]
    foreach ($line in ($Text -split "`r?`n")) {
        $m = [regex]::Match($line, '^(\s*)E:\s+(\S+)')
        if ($m.Success) {
            $name = $m.Groups[2].Value
            if ($name.Contains(':')) { $name = $name.Substring($name.LastIndexOf(':') + 1) }
            $nodes.Add(@{ Name = $name; Depth = [int]($m.Groups[1].Value.Length / 2); Attrs = @{} }) | Out-Null
            continue
        }
        $a = [regex]::Match($line, '^\s*A:\s+(.*?)(?:\(0x[0-9a-fA-F]+\))?=(.*)$')
        if ($a.Success -and $nodes.Count -gt 0) {
            $an = $a.Groups[1].Value.Trim()
            if ($an.Contains(':')) { $an = $an.Substring($an.LastIndexOf(':') + 1) }
            $v = $a.Groups[2].Value.Trim()
            $v = [regex]::Replace($v, '\s*\(Raw:\s*".*"\)\s*$', '')
            $v = $v.Trim('"')
            $v = [regex]::Replace($v, '^\(type 0x[0-9a-fA-F]+\)', '')
            if ($v -eq '0xffffffff') { $v = 'true' } elseif ($v -eq '0x0') { $v = 'false' }
            $nodes[$nodes.Count - 1].Attrs[$an] = $v
        }
    }
    return ,$nodes
}

function ConvertTo-Nodes {
    param([string]$Text)
    if ($Text -match '(?m)^\s*N:\s|^\s*E:\s') { return ConvertFrom-Aapt2XmlTree -Text $Text }
    return ConvertFrom-XmlText -Text $Text
}

function Test-BackupRuleNodes {
    param($Nodes, [string]$Label, [string]$Kind)  # Kind: full | extraction
    if ($Nodes.Count -eq 0) { Add-Finding 'backup-rules' ("{0} is empty" -f $Label); return }
    $expectRoot = if ($Kind -eq 'full') { 'full-backup-content' } else { 'data-extraction-rules' }
    if ($Nodes[0].Name -ne $expectRoot) {
        Add-Finding 'backup-rules' ("{0} root element is '{1}' (expected {2})" -f $Label, $Nodes[0].Name, $expectRoot)
        return
    }
    $wanted = if ($Kind -eq 'full') { @('full-backup-content') } else { @('cloud-backup', 'device-transfer') }
    foreach ($section in $wanted) {
        $excludes = New-Object System.Collections.Generic.List[string]
        $found = $false
        $inSection = $false
        foreach ($n in $Nodes) {
            if ($n.Name -in @('full-backup-content', 'cloud-backup', 'device-transfer')) {
                $inSection = ($n.Name -eq $section)
                if ($inSection) { $found = $true }
            } elseif ($n.Name -eq 'exclude' -and $inSection) {
                $excludes.Add(("{0}|{1}" -f $n.Attrs['domain'], ($n.Attrs['path'] -replace '/+$', ''))) | Out-Null
            } elseif ($n.Name -eq 'include' -and $inSection) {
                $p = $n.Attrs['path']
                if ($p -eq '.' -or $p -eq 'IdleFactory' -or $p -match '(^|/)(SessionLog|Exports)$') {
                    Add-Finding 'backup-rules' ("{0} <{1}> has an include for '{2}' that can re-add the session log or exports" -f $Label, $section, $p)
                }
            }
        }
        if (-not $found) { Add-Finding 'backup-rules' ("{0} has no <{1}> section" -f $Label, $section); continue }
        foreach ($domain in @('file', 'external')) {
            foreach ($path in @('SessionLog', 'Exports', 'IdleFactory/SessionLog', 'IdleFactory/Exports')) {
                if (-not $excludes.Contains(("{0}|{1}" -f $domain, $path))) {
                    Add-Finding 'backup-rules' ("{0} <{1}> does not exclude {2} in domain '{3}'" -f $Label, $section, $path, $domain)
                }
            }
        }
    }
}

function Test-ManifestNodes {
    # Returns @{ FullRef; DataRef } (resource references as written) after reporting manifest findings.
    param($Nodes, [string]$Label)
    $app = $Nodes | Where-Object { $_.Name -eq 'application' } | Select-Object -First 1
    if ($null -eq $app) { Add-Finding 'backup-manifest' ("{0} has no <application> element" -f $Label); return $null }
    if ($app.Attrs['allowBackup'] -eq 'false') {
        Add-Finding 'backup-manifest' ("{0} has allowBackup=false; the backup rules are the unconditional contract (FR-031), keep allowBackup true or unset" -f $Label)
    }
    $full = $app.Attrs['fullBackupContent']
    $data = $app.Attrs['dataExtractionRules']
    if ([string]::IsNullOrWhiteSpace($full)) { Add-Finding 'backup-manifest' ("{0} <application> has no android:fullBackupContent" -f $Label) }
    if ([string]::IsNullOrWhiteSpace($data)) { Add-Finding 'backup-manifest' ("{0} <application> has no android:dataExtractionRules" -f $Label) }
    foreach ($n in $Nodes) {
        if ($n.Name -eq 'provider' -and ($n.Attrs['name'] -match 'FileProvider')) {
            Add-Finding 'backup-manifest' ("{0} declares a FileProvider ({1}); the export is by path only (D-M)" -f $Label, $n.Attrs['name'])
        }
    }
    return @{ FullRef = $full; DataRef = $data }
}

function Find-Aapt2 {
    param([string]$SdkPath)
    $roots = @()
    if (-not [string]::IsNullOrWhiteSpace($SdkPath)) { $roots += $SdkPath }
    foreach ($e in @($env:ANDROID_SDK_ROOT, $env:ANDROID_HOME)) { if (-not [string]::IsNullOrWhiteSpace($e)) { $roots += $e } }
    if ($env:LOCALAPPDATA) { $roots += (Join-Path $env:LOCALAPPDATA 'Android\Sdk') }
    foreach ($r in $roots) {
        $bt = Join-Path $r 'build-tools'
        if (-not (Test-Path -LiteralPath $bt)) { continue }
        $dirs = @(Get-ChildItem -LiteralPath $bt -Directory | Sort-Object { try { [version]($_.Name -replace '[^0-9.].*$', '') } catch { [version]'0.0' } } -Descending)
        foreach ($d in $dirs) {
            foreach ($exe in @('aapt2.exe', 'aapt2')) {
                $p = Join-Path $d.FullName $exe
                if (Test-Path -LiteralPath $p -PathType Leaf) { return $p }
            }
        }
    }
    return $null
}

function Invoke-Aapt2 {
    param([string]$Aapt2, [string[]]$Arguments)
    $out = & $Aapt2 @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) { throw ("aapt2 {0} failed: {1}" -f ($Arguments -join ' '), ($out -join ' ')) }
    return ($out -join "`n")
}

function Resolve-RuleNodesFromGradleOut {
    param([string]$GradleOut, [string]$ResourceName)
    if ([string]::IsNullOrWhiteSpace($GradleOut) -or -not (Test-Path -LiteralPath $GradleOut)) { return $null }
    $hit = @(Get-ChildItem -LiteralPath $GradleOut -Recurse -File -Filter ($ResourceName + '.xml') -ErrorAction SilentlyContinue |
        Where-Object { $_.Directory.Name -eq 'xml' } | Sort-Object LastWriteTime -Descending) | Select-Object -First 1
    if ($null -eq $hit) { return $null }
    return ConvertFrom-XmlText -Text (Get-Content -LiteralPath $hit.FullName -Raw)
}

function Test-AndroidBackupManifest {
    param([string]$Apk, [string]$ManifestFile, [string]$SdkPath, [string]$GradleOut, [string]$RulesRoot)
    $manifestNodes = $null
    $label = $null
    $aapt2 = $null
    $resourceDump = $null
    if (-not [string]::IsNullOrWhiteSpace($ManifestFile)) {
        if (-not (Test-Path -LiteralPath $ManifestFile -PathType Leaf)) { Add-Finding 'backup-manifest' ("Manifest file not found: {0}" -f $ManifestFile); return }
        $manifestNodes = ConvertTo-Nodes -Text (Get-Content -LiteralPath $ManifestFile -Raw)
        $label = "manifest {0}" -f (Split-Path -Leaf $ManifestFile)
    } else {
        $aapt2 = Find-Aapt2 -SdkPath $SdkPath
        if ($null -ne $aapt2 -and -not [string]::IsNullOrWhiteSpace($Apk)) {
            $manifestNodes = ConvertFrom-Aapt2XmlTree -Text (Invoke-Aapt2 -Aapt2 $aapt2 -Arguments @('dump', 'xmltree', '--file', 'AndroidManifest.xml', $Apk))
            $label = 'APK merged manifest (aapt2)'
        } else {
            $cand = $null
            if (-not [string]::IsNullOrWhiteSpace($GradleOut) -and (Test-Path -LiteralPath $GradleOut)) {
                $cand = @(Get-ChildItem -LiteralPath $GradleOut -Recurse -File -Filter 'AndroidManifest.xml' -ErrorAction SilentlyContinue |
                    Where-Object { $_.FullName -match 'merged_manifest' } | Sort-Object LastWriteTime -Descending) | Select-Object -First 1
            }
            if ($null -eq $cand) {
                Add-Finding 'backup-manifest' 'No merged manifest source: aapt2 not found (set -AndroidSdkPath) and no merged_manifests/AndroidManifest.xml under the gradleOut path'
                return
            }
            $manifestNodes = ConvertFrom-XmlText -Text (Get-Content -LiteralPath $cand.FullName -Raw)
            $label = 'gradleOut merged manifest'
            Write-Output ("INFO   aapt2 unavailable; using {0} (may be older than the APK)" -f $cand.FullName)
        }
    }
    Write-Output ("INFO   backup manifest source: {0}" -f $label)
    # Add-Finding writes to the output stream, so separate the finding lines from the returned table.
    $result = @(Test-ManifestNodes -Nodes $manifestNodes -Label $label)
    foreach ($line in @($result | Where-Object { $_ -is [string] })) { Write-Output $line }
    $refs = @($result | Where-Object { $_ -is [hashtable] }) | Select-Object -First 1
    if ($null -eq $refs) { return }

    foreach ($pair in @(@('FullRef', 'full', 'fullBackupContent'), @('DataRef', 'extraction', 'dataExtractionRules'))) {
        $ref = $refs[$pair[0]]
        if ([string]::IsNullOrWhiteSpace($ref)) { continue }
        $nodes = $null
        $ruleLabel = "{0} ({1})" -f $pair[2], $ref
        $name = $null
        if ($ref -match '^@xml/(.+)$') { $name = $Matches[1] }
        if ($null -ne $name) {
            if (-not [string]::IsNullOrWhiteSpace($RulesRoot)) {
                $f = Join-Path $RulesRoot ($name + '.xml')
                if (Test-Path -LiteralPath $f -PathType Leaf) { $nodes = ConvertFrom-XmlText -Text (Get-Content -LiteralPath $f -Raw) }
            }
        } elseif ($ref -match '^@?0x[0-9a-fA-F]+$' -and $null -ne $aapt2 -and -not [string]::IsNullOrWhiteSpace($Apk)) {
            # aapt2 prints the resource id; map it to the packaged file path through the resource table.
            if ($null -eq $resourceDump) { $resourceDump = Invoke-Aapt2 -Aapt2 $aapt2 -Arguments @('dump', 'resources', $Apk) }
            $id = $ref.TrimStart('@').ToLowerInvariant()
            $m = [regex]::Match($resourceDump, ('(?ms)resource\s+' + [regex]::Escape($id) + '\s+\S+.*?\(file\)\s+(\S+)'))
            if ($m.Success) {
                $nodes = ConvertFrom-Aapt2XmlTree -Text (Invoke-Aapt2 -Aapt2 $aapt2 -Arguments @('dump', 'xmltree', '--file', $m.Groups[1].Value, $Apk))
            }
        }
        if ($null -eq $nodes -and $null -ne $name -and [string]::IsNullOrWhiteSpace($Apk)) {
            $nodes = Resolve-RuleNodesFromGradleOut -GradleOut $GradleOut -ResourceName $name
        }
        if ($null -eq $nodes) {
            Add-Finding 'backup-rules-unresolved' ("{0}: the referenced rules XML could not be read from the build output; the exclusion is unverified" -f $ruleLabel)
            continue
        }
        Test-BackupRuleNodes -Nodes $nodes -Label $ruleLabel -Kind $pair[1]
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

# ---- Android backup, explicit merged manifest (also the test hook) -------------
if (-not [string]::IsNullOrWhiteSpace($ManifestPath)) {
    Test-AndroidBackupManifest -ManifestFile $ManifestPath -SdkPath $AndroidSdkPath -GradleOut $GradleOutPath -RulesRoot $RulesRoot
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
            if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
                Test-AndroidBackupManifest -Apk $item.FullName -SdkPath $AndroidSdkPath -GradleOut $GradleOutPath -RulesRoot $RulesRoot
            }
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
