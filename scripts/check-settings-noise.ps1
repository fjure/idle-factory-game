<#
.SYNOPSIS
Rejects known Unity re-serialization noise in ProjectSettings before it is committed.

.DESCRIPTION
Unity builds, target switches and test runs re-serialize settings that the project
never changed on purpose. Every player-build task since T038 has discarded them by
hand, and T038 once failed QA because they were committed. This check compares the
new content with its baseline and fails only when a change introduces one of these
signatures:

  ProjectSettings/ProjectSettings.asset
    empty-icon-slots        m_BuildTargetPlatformIcons becomes a list whose icon
                            slots all have `m_Textures: []` (baseline `[]`)
    automatic-graphics-api  m_BuildTargetGraphicsAPIs gains an entry with
                            `m_Automatic: 1`, which means the same as `[]`
    leaked-graphics-api     an AndroidPlayer entry gains `m_Automatic: 0`: the GLES3
                            emulator build's override leaked (see T056)
  ProjectSettings/EditorSettings.asset
    enter-play-mode         m_EnterPlayModeOptions or m_EnterPlayModeOptionsEnabled
                            changed

Modes:
  -Staged (default)  index versus HEAD; used by the .githooks/pre-commit hook.
  -Commit <rev>      <rev> versus its first parent; used by MAQA QA on the exact
                     assignment commit.

A deliberate change to one of these settings needs an explicit decision. Set
IDLEFACTORY_ALLOW_SETTINGS_NOISE=1 for that one commit; the check then warns and
passes. Record the decision in the commit message.

.EXAMPLE
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check-settings-noise.ps1 -Commit HEAD
#>
[CmdletBinding(DefaultParameterSetName = 'Staged')]
param(
    [Parameter(ParameterSetName = 'Staged')]
    [switch] $Staged,

    [Parameter(Mandatory, ParameterSetName = 'Commit')]
    [string] $Commit
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (& git rev-parse --show-toplevel).Trim()

function Get-GitText {
    # Returns the file content at a revision spec (":path" = index), or $null.
    param([string] $Spec)
    $lines = & git -C $repositoryRoot show $Spec 2>$null
    if ($LASTEXITCODE -ne 0) {
        return $null
    }
    return ($lines -join "`n")
}

function Get-TopLevelBlock {
    # Lines of a two-space-indented PlayerSettings key up to the next sibling key.
    param([string] $Text, [string] $Key)
    if (-not $Text) { return @() }
    $lines = $Text -split "`n"
    $start = [Array]::FindIndex($lines, [Predicate[string]] { param($l) $l -match "^  $([regex]::Escape($Key)):" })
    if ($start -lt 0) { return @() }
    $block = New-Object System.Collections.Generic.List[string]
    $block.Add($lines[$start])
    for ($i = $start + 1; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '^  [A-Za-z_]') { break }
        $block.Add($lines[$i])
    }
    return $block.ToArray()
}

function Get-Signatures {
    param([string] $Path, [string] $Text)
    $found = @{}
    if (-not $Text) { return $found }

    if ($Path -eq 'ProjectSettings/ProjectSettings.asset') {
        $icons = Get-TopLevelBlock -Text $Text -Key 'm_BuildTargetPlatformIcons'
        $textures = @($icons | Where-Object { $_ -match 'm_Textures:' })
        $emptyTextures = @($textures | Where-Object { $_ -match 'm_Textures: \[\]\s*$' })
        if ($textures.Count -gt 0 -and $textures.Count -eq $emptyTextures.Count) {
            $found['empty-icon-slots'] = "m_BuildTargetPlatformIcons lists $($textures.Count) icon slot(s), all with empty textures"
        }

        $apis = Get-TopLevelBlock -Text $Text -Key 'm_BuildTargetGraphicsAPIs'
        $target = $null
        $automaticTargets = New-Object System.Collections.Generic.List[string]
        foreach ($line in $apis) {
            if ($line -match 'm_BuildTarget:\s*(?<t>\S+)') { $target = $Matches.t }
            elseif ($line -match 'm_Automatic:\s*1\s*$') { $automaticTargets.Add($target) }
            elseif ($line -match 'm_Automatic:\s*0\s*$' -and $target -eq 'AndroidPlayer') {
                $found['leaked-graphics-api'] = 'AndroidPlayer Graphics APIs are no longer automatic (GLES3 emulator override leaked; see T056)'
            }
        }
        if ($automaticTargets.Count -gt 0) {
            $found['automatic-graphics-api'] = "explicit automatic Graphics API entry for $($automaticTargets -join ', ') (equivalent to [])"
        }
    }
    elseif ($Path -eq 'ProjectSettings/EditorSettings.asset') {
        $values = @(($Text -split "`n") | Where-Object { $_ -match '^\s*m_EnterPlayModeOptions(Enabled)?:' } | ForEach-Object { $_.Trim() })
        $found['enter-play-mode'] = $values -join '; '
    }
    return $found
}

if ($PSCmdlet.ParameterSetName -eq 'Commit') {
    $resolved = (& git -C $repositoryRoot rev-parse --verify "$Commit^{commit}").Trim()
    $changed = & git -C $repositoryRoot diff-tree --no-commit-id --name-only -r --root $resolved
    $newSpec = "${resolved}:"
    $oldSpec = "${resolved}^:"
    $label = "commit $($resolved.Substring(0, 7))"
}
else {
    $changed = & git -C $repositoryRoot diff --cached --name-only
    $newSpec = ':'
    $oldSpec = 'HEAD:'
    $label = 'staged changes'
}

$findings = New-Object System.Collections.Generic.List[string]
$affected = New-Object System.Collections.Generic.List[string]
foreach ($path in 'ProjectSettings/ProjectSettings.asset', 'ProjectSettings/EditorSettings.asset') {
    if ($changed -notcontains $path) { continue }
    $new = Get-Signatures -Path $path -Text (Get-GitText "$newSpec$path")
    $old = Get-Signatures -Path $path -Text (Get-GitText "$oldSpec$path")
    foreach ($name in $new.Keys) {
        $introduced = if ($name -eq 'enter-play-mode') { $new[$name] -ne $old[$name] } else { -not $old.ContainsKey($name) }
        if ($introduced) {
            $findings.Add("${path}: [$name] $($new[$name])")
            if (-not $affected.Contains($path)) { $affected.Add($path) }
        }
    }
}

if ($findings.Count -eq 0) {
    Write-Output "Settings noise check: OK ($label)."
    exit 0
}

Write-Output "Settings noise check: known Unity re-serialization noise in $label."
$findings | ForEach-Object { Write-Output "  - $_" }
Write-Output "Discard it, for example: git restore --staged --worktree $($affected -join ' ')"
Write-Output 'For a deliberate settings change, set IDLEFACTORY_ALLOW_SETTINGS_NOISE=1 for that commit and record the decision.'
if ($env:IDLEFACTORY_ALLOW_SETTINGS_NOISE -eq '1') {
    Write-Output 'IDLEFACTORY_ALLOW_SETTINGS_NOISE=1: passing with a warning.'
    exit 0
}
exit 1
