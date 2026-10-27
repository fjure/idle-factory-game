<#
.SYNOPSIS
Merges the pull request of one MAQA issue, but only the exact commit QA approved.

.DESCRIPTION
The repository is private on a plan without branch protection, so GitHub cannot
enforce "merge only after QA". This script is that gate. The parent agent may
run it without asking the user; it is the only merge path agents use.

It merges only when all of these hold:
  - the issue is open, labelled `maqa` and `maqa:in-review`, not `maqa:blocked`;
  - the newest worker-done / qa-pass / qa-fail event marker on the issue is a
    qa-pass marker (`<!-- maqa:event:qa-pass:#<n>:<sha> -->`);
  - exactly one open pull request exists for the branch named by the issue's
    start marker, it targets the default branch, is not a draft, is mergeable,
    and its head commit is the QA-approved commit;
  - `scripts/check-settings-noise.ps1 -Commit <sha>` passes.

The merge uses `gh pr merge --merge --match-head-commit <sha>`, so GitHub
refuses it if anything was pushed after the check. The script deletes no
branch or worktree and closes no issue: run the coordinator's `merged #<n>`
step afterwards. It then releases the `unity`/`emulator` leases that
`issue-<n>` still holds.

Exit codes: 0 merged (or would merge with -DryRun), 1 refused or error.

.EXAMPLE
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/maqa-merge.ps1 -Issue 366 -DryRun
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateRange(1, [int]::MaxValue)]
    [int] $Issue,

    # Run every check, but do not merge or release leases.
    [switch] $DryRun,

    [string] $DefaultBranch = 'main'
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'lib/GitHubDirect.ps1')

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$repository = Get-OriginRepository -RepositoryRoot $repositoryRoot

function Stop-Refused {
    param([string] $Reason)
    Write-Output "REFUSED #${Issue}: $Reason"
    exit 1
}

function Invoke-GhJson {
    param([string[]] $Arguments)
    $json = Invoke-GitHubDirect -Executable gh -NativeArguments $Arguments
    return (($json -join "`n") | ConvertFrom-Json)
}

# 1. Issue state and labels.
$issueData = Invoke-GhJson @('issue', 'view', "$Issue", '--repo', $repository, '--json', 'state,labels,comments,title')
$labels = @($issueData.labels | ForEach-Object { $_.name })
if ($issueData.state -ne 'OPEN') { Stop-Refused "issue is $($issueData.state), not OPEN" }
if ($labels -notcontains 'maqa') { Stop-Refused 'issue is not a MAQA issue (label maqa missing)' }
if ($labels -contains 'maqa:blocked') { Stop-Refused 'issue is labelled maqa:blocked' }
if ($labels -notcontains 'maqa:in-review') { Stop-Refused "issue is not labelled maqa:in-review (labels: $($labels -join ', '))" }

# 2. Event markers, in comment order.
$events = New-Object System.Collections.Generic.List[object]
foreach ($comment in @($issueData.comments)) {
    foreach ($match in [regex]::Matches([string]$comment.body, "<!-- maqa:event:(?<kind>[a-z-]+):#$Issue`:(?<value>[^ >]+) -->")) {
        $events.Add([pscustomobject]@{ Kind = $match.Groups['kind'].Value; Value = $match.Groups['value'].Value })
    }
}
$startEvent = $events | Where-Object Kind -eq 'start' | Select-Object -Last 1
if (-not $startEvent) { Stop-Refused 'no start marker names the issue branch' }
$branch = $startEvent.Value

$verdict = $events | Where-Object { $_.Kind -in 'worker-done', 'qa-pass', 'qa-fail' } | Select-Object -Last 1
if (-not $verdict -or $verdict.Kind -ne 'qa-pass') {
    Stop-Refused "the newest worker/QA event is '$(if ($verdict) { $verdict.Kind } else { 'none' })', not qa-pass"
}
$approvedSha = $verdict.Value
if ($approvedSha -notmatch '^[0-9a-f]{40}$') { Stop-Refused "qa-pass marker carries '$approvedSha', not a full commit SHA" }

# 3. The pull request.
$pulls = @(Invoke-GhJson @('pr', 'list', '--repo', $repository, '--head', $branch, '--state', 'open', '--json', 'number,headRefOid,baseRefName,isDraft,mergeable,url'))
if ($pulls.Count -ne 1) { Stop-Refused "expected exactly one open pull request for '$branch', found $($pulls.Count)" }
$pull = $pulls[0]
if ($pull.baseRefName -ne $DefaultBranch) { Stop-Refused "pull request #$($pull.number) targets '$($pull.baseRefName)', not '$DefaultBranch'" }
if ($pull.isDraft) { Stop-Refused "pull request #$($pull.number) is a draft" }
if ($pull.headRefOid -ne $approvedSha) {
    Stop-Refused "pull request #$($pull.number) head $($pull.headRefOid) is not the QA-approved commit $approvedSha"
}
# GitHub computes mergeability lazily; UNKNOWN settles within seconds.
for ($attempt = 0; $pull.mergeable -eq 'UNKNOWN' -and $attempt -lt 5; $attempt++) {
    Start-Sleep -Seconds 3
    $pull = Invoke-GhJson @('pr', 'view', "$($pull.number)", '--repo', $repository, '--json', 'number,headRefOid,mergeable,url')
}
if ($pull.mergeable -ne 'MERGEABLE') { Stop-Refused "pull request #$($pull.number) is $($pull.mergeable), not MERGEABLE" }

# 4. Settings noise on the approved commit.
Invoke-GitHubDirect -Executable git -NativeArguments @('-C', $repositoryRoot, 'fetch', '--quiet', 'origin', $branch) | Out-Null
$noise = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'check-settings-noise.ps1') -Commit $approvedSha
if ($LASTEXITCODE -ne 0) {
    $noise | ForEach-Object { Write-Output "  $_" }
    Stop-Refused "settings noise check failed on $approvedSha"
}

Write-Output "Issue #$Issue '$($issueData.title)': QA-approved $approvedSha is the head of $($pull.url), mergeable, settings clean."
if ($DryRun) {
    Write-Output 'Dry run: would merge now.'
    exit 0
}

# 5. Merge exactly that commit.
Invoke-GitHubDirect -Executable gh -NativeArguments @('pr', 'merge', "$($pull.number)", '--repo', $repository, '--merge', '--match-head-commit', $approvedSha) | ForEach-Object { Write-Output $_ }
Write-Output "MERGED #${Issue}: pull request #$($pull.number) at $approvedSha. Next: coordinator 'merged #$Issue'."

# 6. Release this issue's leases; a resource held by someone else stays untouched.
foreach ($resourceName in 'unity', 'emulator') {
    $check = & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'resource-lease.ps1') -Check -Resource $resourceName -Owner "issue-$Issue" -Offline
    if ($check -match "held by you") {
        & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'resource-lease.ps1') -Release -Resource $resourceName -Owner "issue-$Issue" | ForEach-Object { Write-Output $_ }
    }
}
exit 0
