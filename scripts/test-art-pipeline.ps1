<#
.SYNOPSIS
Runs the Feature 004 art-pipeline tests (tools/art-pipeline) and writes a machine-readable report.

.DESCRIPTION
Resolves `uv` by absolute path, clears proxy variables for this process only, and runs
`uv run --frozen --offline --project tools/art-pipeline pytest`. The `checks` CLI has no manifest to
run on until art-source/f004 exists; this script is the single place to add it then.

Exit codes:
  0  pass
  1  test failure
  3  tooling unavailable (uv or its offline cache is missing); NOT a test failure
  (2 is reserved for resource-lease conflicts elsewhere in scripts/.)

Report: test-results/art-pipeline/report.json (override with -ReportPath), always written, fields
schema, status (pass|fail|unavailable), exitCode, commit (HEAD sha), dirty (art paths have
uncommitted changes), startedUtc, uv, summary, detail. scripts/check-after-implement-hooks.ps1
requires status=pass with a commit that contains the newest art-path commit.

.PARAMETER UvPath
Explicit uv executable. A missing explicit path yields "tooling unavailable" (no fallback), which is
also how the no-uv dry run is done. Default order: -UvPath, $env:IDLEFACTORY_UV_PATH,
%USERPROFILE%\.local\bin\uv.exe, Get-Command uv.

.PARAMETER IfChanged
Run only when tools/art-pipeline/, art-source/ or Assets/IdleFactory/Art/F004/ changed relative to the
merge base with origin/main (committed, staged, unstaged or untracked). Otherwise print a skip and
exit 0 without touching the report. Fails safe (treated as changed) when origin/main is unknown.

.PARAMETER DetectOnly
Print `ART_PIPELINE_CHANGED=true|false` and exit 0; nothing runs.
#>
[CmdletBinding()]
param(
    [string] $UvPath,
    [string] $ReportPath,
    [switch] $IfChanged,
    [switch] $DetectOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$artPaths = @('tools/art-pipeline/', 'art-source/', 'Assets/IdleFactory/Art/F004/')

function Get-ArtPathsChanged {
    $base = (& git -C $repositoryRoot merge-base HEAD origin/main 2>$null)
    if ($LASTEXITCODE -ne 0 -or -not $base) { return $true }
    $changed = @(& git -C $repositoryRoot diff --name-only $base -- @artPaths)
    $changed += @(& git -C $repositoryRoot ls-files --others --exclude-standard -- @artPaths)
    return (@($changed | Where-Object { $_ }).Count -gt 0)
}

if ($DetectOnly -or $IfChanged) {
    $changed = Get-ArtPathsChanged
    if ($DetectOnly) {
        Write-Output "ART_PIPELINE_CHANGED=$($changed.ToString().ToLowerInvariant())"
        exit 0
    }
    if (-not $changed) {
        Write-Output 'ART_PIPELINE_SKIPPED: no art-pipeline paths changed relative to the merge base with origin/main.'
        exit 0
    }
}

if (-not $ReportPath) { $ReportPath = Join-Path $repositoryRoot 'test-results/art-pipeline/report.json' }

function Write-Report {
    param([string] $Status, [int] $ExitCode, [string] $Uv, [string] $Summary, [string] $Detail)
    $head = (& git -C $repositoryRoot rev-parse HEAD 2>$null)
    $dirty = [bool](& git -C $repositoryRoot status --porcelain -- @artPaths 2>$null)
    $report = [ordered]@{
        schema = 1; status = $Status; exitCode = $ExitCode; commit = "$head"; dirty = $dirty
        startedUtc = $script:startedUtc; uv = $Uv; summary = $Summary; detail = $Detail
    }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $ReportPath) | Out-Null
    ($report | ConvertTo-Json) | Set-Content -LiteralPath $ReportPath -Encoding utf8
}

$script:startedUtc = [DateTime]::UtcNow.ToString('o')

# Resolve uv to an absolute path.
$uv = $null
if ($UvPath) { $uv = $UvPath }
elseif ($env:IDLEFACTORY_UV_PATH) { $uv = $env:IDLEFACTORY_UV_PATH }
else {
    $candidate = if ($env:USERPROFILE) { Join-Path $env:USERPROFILE '.local\bin\uv.exe' } else { $null }
    if ($candidate -and (Test-Path -LiteralPath $candidate -PathType Leaf)) { $uv = $candidate }
    else {
        $cmd = Get-Command uv -ErrorAction SilentlyContinue
        if ($cmd) { $uv = $cmd.Source }
    }
}
if (-not $uv -or -not (Test-Path -LiteralPath $uv -PathType Leaf)) {
    $detail = "uv not found (looked for '$uv'). Install uv or pass -UvPath / set IDLEFACTORY_UV_PATH."
    Write-Report -Status 'unavailable' -ExitCode 3 -Uv "$uv" -Summary 'tooling unavailable' -Detail $detail
    Write-Output "ART_PIPELINE_TOOLING_UNAVAILABLE: $detail"
    exit 3
}
$uv = (Resolve-Path -LiteralPath $uv).Path

# Clear proxy variables for this process (and its uv child) only.
foreach ($name in 'HTTP_PROXY', 'HTTPS_PROXY', 'ALL_PROXY', 'NO_PROXY', 'http_proxy', 'https_proxy', 'all_proxy', 'no_proxy', 'GIT_HTTP_PROXY') {
    [Environment]::SetEnvironmentVariable($name, $null, 'Process')
}

$outFile = [IO.Path]::GetTempFileName()
$errFile = [IO.Path]::GetTempFileName()
try {
    $arguments = 'run --frozen --offline --project tools/art-pipeline pytest'
    $process = Start-Process -FilePath $uv -ArgumentList $arguments -WorkingDirectory $repositoryRoot `
        -NoNewWindow -Wait -PassThru -RedirectStandardOutput $outFile -RedirectStandardError $errFile
    $output = ((Get-Content -LiteralPath $outFile -Raw), (Get-Content -LiteralPath $errFile -Raw)) -join "`n"
    $output = $output.Trim()
    $uvExit = $process.ExitCode
}
finally {
    Remove-Item -LiteralPath $outFile, $errFile -Force -ErrorAction SilentlyContinue
}

$summaryLine = ($output -split "\r?\n" | Where-Object { $_ -match '\b\d+ (passed|failed|error)' } | Select-Object -Last 1)
Write-Output $output

if ($uvExit -eq 0 -and $summaryLine) {
    Write-Report -Status 'pass' -ExitCode 0 -Uv $uv -Summary "$summaryLine".Trim() -Detail ''
    Write-Output 'ART_PIPELINE_PASS'
    exit 0
}
if (-not $summaryLine) {
    # pytest never produced a result: uv itself failed (missing cache, environment, offline resolution).
    $tail = (($output -split "\r?\n") | Select-Object -Last 15) -join "`n"
    Write-Report -Status 'unavailable' -ExitCode 3 -Uv $uv -Summary 'tooling unavailable' -Detail "uv exit $uvExit without a pytest result: $tail"
    Write-Output "ART_PIPELINE_TOOLING_UNAVAILABLE: uv exit $uvExit without a pytest result (offline cache or environment missing)."
    exit 3
}
Write-Report -Status 'fail' -ExitCode 1 -Uv $uv -Summary "$summaryLine".Trim() -Detail "pytest exit $uvExit"
Write-Output "ART_PIPELINE_FAIL: $summaryLine"
exit 1
