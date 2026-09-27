<#
.SYNOPSIS
Mirrors MAQA GitHub Issue state into the task checkboxes of a tasks.md file.

.DESCRIPTION
GitHub Issues remain the MAQA status authority. This script only derives the
checkboxes from them, so core Spec Kit skills that read tasks.md (learn-review,
converge, implement) see the real implementation status.

A task is ticked when every MAQA issue for its task ID is closed; it is
unticked when any of them is open. The coordinator closes an issue only after
it verified the QA-approved merge, which is the parent verification the
orchestration ledger requires. Tasks without a MAQA issue are left unchanged
and reported. The file is otherwise preserved byte for byte.

.PARAMETER TasksFile
Explicit path to specs/<feature>/tasks.md.

.PARAMETER Check
Report drift and exit 1 instead of writing the file.

.EXAMPLE
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/maqa-sync-task-checkboxes.ps1 -TasksFile specs/001-core-game-foundation/tasks.md
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $TasksFile,

    [switch] $Check
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'lib/GitHubDirect.ps1')

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$tasksPath = if ([IO.Path]::IsPathRooted($TasksFile)) { $TasksFile } else { Join-Path $repositoryRoot $TasksFile }
if (-not (Test-Path -LiteralPath $tasksPath -PathType Leaf)) {
    throw "Tasks file not found: $tasksPath"
}

$repository = Get-OriginRepository -RepositoryRoot $repositoryRoot

# Task ID -> $true (done) / $false (open work remains).
$doneByTask = @{}
foreach ($issue in Get-MaqaIssues -Repository $repository) {
    $taskId = Get-TaskIdFromIssueTitle -Title $issue.title
    if (-not $taskId) {
        continue
    }
    $closed = $issue.state -eq 'CLOSED'
    if ($doneByTask.ContainsKey($taskId)) {
        $doneByTask[$taskId] = $doneByTask[$taskId] -and $closed
    }
    else {
        $doneByTask[$taskId] = $closed
    }
}

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$original = [IO.File]::ReadAllText($tasksPath, $utf8NoBom)

$taskLine = [regex] '(?m)^(?<prefix>[ \t]*- \[)(?<mark>[ xX])(?<suffix>\] (?<id>T\d{3,})\b)'
$ticked = New-Object System.Collections.Generic.List[string]
$unticked = New-Object System.Collections.Generic.List[string]
$withoutIssue = New-Object System.Collections.Generic.List[string]
$doneCount = 0
$taskCount = 0

$updated = $taskLine.Replace($original, {
    param($match)
    $script:taskCount++
    $id = $match.Groups['id'].Value
    $mark = $match.Groups['mark'].Value
    if (-not $doneByTask.ContainsKey($id)) {
        $withoutIssue.Add($id)
        if ($mark -ne ' ') { $script:doneCount++ }
        return $match.Value
    }
    $done = [bool] $doneByTask[$id]
    if ($done) { $script:doneCount++ }
    $wasDone = $mark -ne ' '
    if ($wasDone -ne $done) {
        if ($done) { $ticked.Add($id) } else { $unticked.Add($id) }
    }
    # Uppercase X is the Spec Kit form; a lowercase x is normalized silently.
    $newMark = if ($done) { 'X' } else { ' ' }
    return $match.Groups['prefix'].Value + $newMark + $match.Groups['suffix'].Value
})

Write-Output "Repository: $repository"
Write-Output "Tasks: $taskCount, done after sync: $doneCount"
if ($ticked.Count) { Write-Output ("Tick:   " + ($ticked -join ', ')) }
if ($unticked.Count) { Write-Output ("Untick: " + ($unticked -join ', ')) }
if ($withoutIssue.Count) { Write-Output ("No MAQA issue (unchanged): " + ($withoutIssue -join ', ')) }

$drift = $ticked.Count + $unticked.Count
if ($Check) {
    if ($drift -gt 0) {
        Write-Output "DRIFT: $drift task checkbox(es) differ from GitHub Issue state."
        exit 1
    }
    Write-Output 'OK: task checkboxes match GitHub Issue state.'
    exit 0
}

if ($updated -ne $original) {
    [IO.File]::WriteAllText($tasksPath, $updated, $utf8NoBom)
    Write-Output "Updated $tasksPath ($drift checkbox change(s))."
}
else {
    Write-Output 'No change: task checkboxes already match GitHub Issue state.'
}
