<#
.SYNOPSIS
Reports stale Git worktrees and prints the commands to remove them. Removes nothing.

.DESCRIPTION
MAQA cleanup deletes a worker's worktree and branch after the merge, but that
step can be skipped (for example when an agent may not delete branches). This
report lists every linked worktree with the facts needed to decide safely:

  Merged   HEAD is an ancestor of origin/<default branch>; a branch whose
           commits exist there only as equivalent patches (re-committed or
           rebased) is named as such but stays REVIEW
  Dirty    uncommitted or untracked files in the worktree
  Issue    state of the MAQA issue named by a maqa/issue-<n>-<slug> branch
  Remote   whether the branch still exists on origin

A worktree is REMOVABLE only when it is merged, clean, and its MAQA issue (if
any) is closed. Everything else is REVIEW with the reason. Removal commands use
the non-forcing forms (`git worktree remove`, `git branch -d`), so Git itself
refuses to delete unmerged or dirty state. Worktrees owned by other tools
(.codex, .kilo) are reported but never listed as REMOVABLE.

.EXAMPLE
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/maqa-stale-worktrees.ps1
#>
[CmdletBinding()]
param(
    [string] $DefaultBranch = 'main',

    # Skip the GitHub issue lookup and the origin fetch (offline report).
    [switch] $Offline
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'lib/GitHubDirect.ps1')

$repositoryRoot = Split-Path -Parent $PSScriptRoot

function Invoke-Git {
    param([string] $Directory)
    $gitArgs = $args
    $output = & git -C $Directory @gitArgs 2>$null
    return [pscustomobject]@{ Output = $output; ExitCode = $LASTEXITCODE }
}

if (-not $Offline) {
    Invoke-GitHubDirect -Executable git -NativeArguments @('-C', $repositoryRoot, 'fetch', '--quiet', '--prune', 'origin') | Out-Null
}
$baseRef = "origin/$DefaultBranch"

# Parse `git worktree list --porcelain` into records.
$records = New-Object System.Collections.Generic.List[object]
$current = $null
foreach ($line in (& git -C $repositoryRoot worktree list --porcelain)) {
    if ($line -like 'worktree *') {
        $current = [ordered]@{ Path = $line.Substring(9); Head = $null; Branch = $null; Prunable = $false }
        $records.Add($current)
    }
    elseif ($line -like 'HEAD *') { $current.Head = $line.Substring(5) }
    elseif ($line -like 'branch *') { $current.Branch = $line.Substring(7) -replace '^refs/heads/', '' }
    elseif ($line -like 'prunable*') { $current.Prunable = $true }
}
$mainWorktree = $records[0].Path
$linked = @($records | Select-Object -Skip 1)

$issueStateByNumber = @{}
if (-not $Offline) {
    foreach ($issue in Get-MaqaIssues -Repository (Get-OriginRepository -RepositoryRoot $repositoryRoot)) {
        $issueStateByNumber[[int] $issue.number] = $issue.state
    }
}
$remoteBranches = @{}
foreach ($ref in (& git -C $repositoryRoot for-each-ref --format='%(refname:short)' refs/remotes/origin)) {
    $remoteBranches[($ref -replace '^origin/', '')] = $true
}

$rows = New-Object System.Collections.Generic.List[object]
foreach ($record in $linked) {
    $path = $record.Path
    $branch = if ($record.Branch) { $record.Branch } else { '(detached)' }
    $owner = if ($path -match '[\\/]\.codex[\\/]') { 'codex' }
        elseif ($path -match '[\\/]\.kilo[\\/]') { 'kilo' }
        elseif ($path -match '[\\/]\.maqa-worktrees[\\/]') { 'maqa' }
        else { 'side' }

    $reasons = New-Object System.Collections.Generic.List[string]
    if ($record.Prunable -or -not (Test-Path -LiteralPath $path)) {
        $rows.Add([pscustomobject]@{ Verdict = 'PRUNE'; Owner = $owner; Branch = $branch; Merged = '-'; Dirty = '-'; Issue = '-'; Remote = '-'; Path = $path; Reason = 'directory is gone' })
        continue
    }

    $merged = (Invoke-Git $repositoryRoot merge-base --is-ancestor $record.Head $baseRef).ExitCode -eq 0
    $dirtyLines = @((Invoke-Git $path status --porcelain).Output | Where-Object { $_ })
    $dirty = $dirtyLines.Count -gt 0

    $issueLabel = '-'
    $issueOpen = $false
    if ($record.Branch -match '^maqa/issue-(?<n>\d+)-') {
        $number = [int] $Matches.n
        $state = if ($issueStateByNumber.ContainsKey($number)) { $issueStateByNumber[$number] } elseif ($Offline) { 'unknown' } else { 'not found' }
        $issueLabel = "#$number $state"
        $issueOpen = $state -ne 'CLOSED'
    }
    $onRemote = $record.Branch -and $remoteBranches.ContainsKey($record.Branch)

    if (-not $merged) {
        $ahead = (Invoke-Git $repositoryRoot rev-list --count "$baseRef..$($record.Head)").Output
        # `git cherry` marks commits whose patch is already on the base with '-'.
        $cherry = @((Invoke-Git $repositoryRoot cherry $baseRef $record.Head).Output | Where-Object { $_ })
        if ($cherry.Count -gt 0 -and -not ($cherry | Where-Object { $_ -notlike '- *' })) {
            $reasons.Add("$ahead commit(s) not on $baseRef, but every patch is already there (re-committed); verify, then remove with git branch -D")
        }
        else {
            $reasons.Add("$ahead commit(s) not on $baseRef")
        }
    }
    if ($dirty) { $reasons.Add("$($dirtyLines.Count) uncommitted/untracked path(s)") }
    if ($issueOpen) { $reasons.Add("issue $issueLabel") }
    if ($owner -in 'codex', 'kilo') { $reasons.Add("owned by $owner; remove from that tool") }

    $verdict = if ($reasons.Count -eq 0) { 'REMOVABLE' } else { 'REVIEW' }
    $rows.Add([pscustomobject]@{
        Verdict = $verdict; Owner = $owner; Branch = $branch
        Merged = $(if ($merged) { 'yes' } else { 'no' }); Dirty = $(if ($dirty) { 'yes' } else { 'no' })
        Issue = $issueLabel; Remote = $(if ($onRemote) { 'yes' } else { 'no' })
        Path = $path; Reason = $(if ($reasons.Count) { $reasons -join '; ' } elseif ($issueLabel -ne '-') { 'merged, clean, issue closed' } else { 'merged, clean' })
    })
}

Write-Output "Main worktree: $mainWorktree (never listed). Base: $baseRef."
$rows | Sort-Object Verdict, Branch | Format-Table Verdict, Owner, Branch, Merged, Dirty, Issue, Remote, Reason -AutoSize -Wrap |
    Out-String -Width 220 | Write-Output

$removable = @($rows | Where-Object Verdict -eq 'REMOVABLE')
$prunable = @($rows | Where-Object Verdict -eq 'PRUNE')
if ($removable.Count -or $prunable.Count) {
    Write-Output 'Commands (non-forcing; run from the main worktree after reviewing the table):'
    Write-Output ''
    foreach ($row in $removable) {
        Write-Output "git worktree remove `"$($row.Path)`""
        if ($row.Branch -ne '(detached)') {
            Write-Output "git branch -d $($row.Branch)"
            if ($row.Remote -eq 'yes') {
                Write-Output "git push origin --delete $($row.Branch)"
            }
        }
    }
    if ($prunable.Count) {
        Write-Output 'git worktree prune'
    }
}
else {
    Write-Output 'No removable worktrees.'
}
Write-Output ("Summary: {0} linked worktree(s): {1} removable, {2} review, {3} prune." -f
    $rows.Count, $removable.Count, @($rows | Where-Object Verdict -eq 'REVIEW').Count, $prunable.Count)
