<#
.SYNOPSIS
Verifies that the mandatory after_implement hooks ran on the current implementation.

.DESCRIPTION
The MAQA lane never passes through $speckit-implement, so nothing dispatches the
after_implement hooks automatically there. The parent runs this gate after each
merged batch and before $speckit-converge. It fails when:

  1. .specify/extensions.yml no longer registers speckit.wiki.ingest and
     speckit.learn.review under after_implement with `optional: false`.
  2. <feature>/learn.md is missing or older than the last implementation commit.
  3. wiki/sources.md does not show the feature directory re-ingested after the
     last implementation commit.
  4. wiki/lint-report.md is older than that ingest.
  5. tasks.md checkboxes differ from GitHub Issue state (skipped with -SkipGitHub).

"Implementation commit" means the newest commit on HEAD that touches Assets/,
Packages/ or ProjectSettings/. A file counts as newer when it has uncommitted
changes, or when its last commit is not an ancestor of that implementation
commit. Commit ancestry avoids same-day ambiguity in the ISO dates.

.EXAMPLE
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check-after-implement-hooks.ps1 -FeatureDir specs/001-core-game-foundation
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $FeatureDir,

    [switch] $SkipGitHub
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$featureRelative = $FeatureDir.Replace('\', '/').TrimEnd('/')
$featurePath = Join-Path $repositoryRoot $featureRelative
if (-not (Test-Path -LiteralPath $featurePath -PathType Container)) {
    throw "Feature directory not found: $featurePath"
}

$results = New-Object System.Collections.Generic.List[object]
function Add-Result {
    param([string] $Check, [bool] $Pass, [string] $Detail)
    $results.Add([pscustomobject]@{ Check = $Check; Result = $(if ($Pass) { 'PASS' } else { 'FAIL' }); Detail = $Detail })
}

function Invoke-Git {
    $output = & git -C $repositoryRoot @args
    if ($LASTEXITCODE -ne 0) {
        throw "git $($args -join ' ') failed with exit code $LASTEXITCODE."
    }
    return $output
}

function Test-NewerThanImplementation {
    # True when the path has uncommitted changes, or its last commit is not an
    # ancestor of (and not equal to) the implementation commit.
    param([string] $RelativePath, [string] $ImplementationCommit)

    if (Invoke-Git status --porcelain -- $RelativePath) {
        return $true
    }
    $pathCommit = Invoke-Git log -1 --format=%H -- $RelativePath
    if (-not $pathCommit) {
        return $false
    }
    if ($pathCommit -eq $ImplementationCommit) {
        return $true
    }
    & git -C $repositoryRoot merge-base --is-ancestor $pathCommit $ImplementationCommit
    return $LASTEXITCODE -ne 0
}

# 1. Hook registration.
$extensions = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot '.specify/extensions.yml')
$afterImplement = if ($extensions -match '(?ms)^  after_implement:\r?\n(?<block>(?:    .*\r?\n?|  - .*\r?\n?)+)') { $Matches.block } else { '' }
foreach ($command in 'speckit.wiki.ingest', 'speckit.learn.review') {
    $entry = [regex]::Match($afterImplement, "(?ms)command: $([regex]::Escape($command))\s*\r?\n(?<rest>(?:    .*\r?\n?)*)")
    $mandatory = $entry.Success -and $entry.Groups['rest'].Value -match '(?m)^    optional: false\s*$'
    Add-Result "Hook $command is mandatory" $mandatory $(if ($mandatory) { 'optional: false' } else { 'missing or optional under after_implement' })
}

# Implementation baseline.
$implementationCommit = Invoke-Git log -1 --format=%H -- Assets Packages ProjectSettings
if (-not $implementationCommit) {
    throw 'No implementation commit touches Assets/, Packages/ or ProjectSettings/.'
}
$implementationDate = [DateTime]::Parse((Invoke-Git log -1 --format=%cs $implementationCommit))
$implementationLabel = "{0} ({1})" -f $implementationCommit.Substring(0, 7), $implementationDate.ToString('yyyy-MM-dd')

# 2. learn.md.
$learnRelative = "$featureRelative/learn.md"
if (-not (Test-Path -LiteralPath (Join-Path $repositoryRoot $learnRelative))) {
    Add-Result 'learn.md is current' $false "$learnRelative is missing; run `$speckit-learn-review"
}
else {
    $fresh = Test-NewerThanImplementation -RelativePath $learnRelative -ImplementationCommit $implementationCommit
    Add-Result 'learn.md is current' $fresh $(if ($fresh) { "newer than implementation $implementationLabel" } else { "older than implementation $implementationLabel; run `$speckit-learn-review" })
}

# 3. Wiki ingest of the feature directory.
$sourcesRelative = 'wiki/sources.md'
$ingestDate = $null
foreach ($line in Get-Content -LiteralPath (Join-Path $repositoryRoot $sourcesRelative)) {
    $cells = $line.Split('|') | ForEach-Object { $_.Trim() }
    if ($cells.Count -ge 7 -and $cells[2].Trim('`').TrimEnd('/') -eq $featureRelative) {
        $ingestDate = [DateTime]::Parse($cells[5])
    }
}
if (-not $ingestDate) {
    Add-Result 'Feature re-ingested into the wiki' $false "$featureRelative is not registered in $sourcesRelative; run `$speckit-wiki-ingest $featureRelative"
}
else {
    $fresh = ($ingestDate -ge $implementationDate) -and
        (Test-NewerThanImplementation -RelativePath $sourcesRelative -ImplementationCommit $implementationCommit)
    Add-Result 'Feature re-ingested into the wiki' $fresh ("last ingested {0}, implementation {1}" -f $ingestDate.ToString('yyyy-MM-dd'), $implementationLabel)
}

# 4. Wiki lint after the ingest.
$lintPath = Join-Path $repositoryRoot 'wiki/lint-report.md'
$lintDate = $null
if (Test-Path -LiteralPath $lintPath) {
    $heading = Get-Content -LiteralPath $lintPath -TotalCount 5 | Where-Object { $_ -match '(?<date>\d{4}-\d{2}-\d{2})' } | Select-Object -First 1
    if ($heading -and $heading -match '(?<date>\d{4}-\d{2}-\d{2})') {
        $lintDate = [DateTime]::Parse($Matches.date)
    }
}
$lintFresh = $lintDate -and $ingestDate -and ($lintDate -ge $ingestDate)
Add-Result 'Wiki linted after the ingest' ([bool] $lintFresh) $(if ($lintDate) { "lint report {0}" -f $lintDate.ToString('yyyy-MM-dd') } else { 'no dated wiki/lint-report.md; run `$speckit-wiki-lint' })

# 5. Task checkboxes mirror GitHub Issues.
if ($SkipGitHub) {
    Add-Result 'tasks.md mirrors GitHub Issues' $true 'skipped (-SkipGitHub)'
}
else {
    $syncOutput = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'maqa-sync-task-checkboxes.ps1') -TasksFile "$featureRelative/tasks.md" -Check
    $inSync = $LASTEXITCODE -eq 0
    $summary = ($syncOutput | Where-Object { $_ -match '^(OK|DRIFT):' }) -join ' '
    Add-Result 'tasks.md mirrors GitHub Issues' $inSync $(if ($summary) { $summary } else { 'checkbox sync check failed to run' })
}

$results | Format-Table -AutoSize -Wrap | Out-String -Width 200 | Write-Output
$failed = @($results | Where-Object { $_.Result -eq 'FAIL' }).Count
if ($failed -gt 0) {
    Write-Output "after_implement gate: FAIL ($failed of $($results.Count) checks)."
    exit 1
}
Write-Output 'after_implement gate: PASS.'
