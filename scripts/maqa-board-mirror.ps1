<#
.SYNOPSIS
Mirrors MAQA GitHub Issue state into the GitHub Projects board.

.DESCRIPTION
This is the `board_mirror` companion step described by the MAQA coordinator.
GitHub Issues stay the status authority: the board is written only after a
verified issue transition and is never read back as workflow state.

Single issue: the parent calls this after each verified coordinator transition,
for example `-Issue 105 -Status InProgress`.

Backfill: `-Backfill` adds every MAQA issue and derives its status from the
issue itself:
  closed                  -> Done
  label maqa:in-review    -> In review
  label maqa:in-progress  -> In progress
  label maqa:blocked      -> In progress (blocked work has started)
  otherwise               -> Todo

Adding an issue that is already on the board returns the existing item, so both
modes are idempotent. The Project's built-in workflows ("Item added to project",
"Pull request linked to issue") set a status asynchronously right after an item
is added and can overwrite the mirrored one. The script therefore re-reads the
board after a newly added item settles and re-applies any overwritten status. Board IDs come from
maqa-github-projects/github-projects-config.yml.

Mirror failures never change MAQA state. A single-issue mirror failure is a
warning and exits 0, as the coordinator requires; a backfill with failures
exits 1 so the operator notices.

.EXAMPLE
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/maqa-board-mirror.ps1 -Issue 105 -Status InReview

.EXAMPLE
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/maqa-board-mirror.ps1 -Backfill
#>
[CmdletBinding(DefaultParameterSetName = 'Single')]
param(
    [Parameter(Mandatory, ParameterSetName = 'Single')]
    [int] $Issue,

    [Parameter(Mandatory, ParameterSetName = 'Single')]
    [ValidateSet('Todo', 'InProgress', 'InReview', 'Done')]
    [string] $Status,

    [Parameter(Mandatory, ParameterSetName = 'Backfill')]
    [switch] $Backfill,

    [string] $ConfigFile = 'maqa-github-projects/github-projects-config.yml'
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'lib/GitHubDirect.ps1')

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$configPath = if ([IO.Path]::IsPathRooted($ConfigFile)) { $ConfigFile } else { Join-Path $repositoryRoot $ConfigFile }
if (-not (Test-Path -LiteralPath $configPath -PathType Leaf)) {
    throw "Board config not found: $configPath. Run the maqa-github-projects setup first."
}

# The config is flat `key: "value"` YAML written by the setup command.
$config = @{}
foreach ($line in Get-Content -LiteralPath $configPath) {
    if ($line -match '^\s*(?<key>[a-z_]+)\s*:\s*"?(?<value>[^"#]*?)"?\s*(#.*)?$') {
        $config[$Matches.key] = $Matches.value.Trim()
    }
}
foreach ($key in 'owner', 'project_number', 'project_id', 'status_field_id',
    'todo_option_id', 'in_progress_option_id', 'in_review_option_id', 'done_option_id') {
    if (-not $config[$key]) {
        throw "Board config $configPath is missing '$key'."
    }
}

$nameByStatus = @{
    Todo       = 'Todo'
    InProgress = 'In Progress'
    InReview   = 'In Review'
    Done       = 'Done'
}
$settleSeconds = 15

$optionByStatus = @{
    Todo       = $config.todo_option_id
    InProgress = $config.in_progress_option_id
    InReview   = $config.in_review_option_id
    Done       = $config.done_option_id
}

$repository = Get-OriginRepository -RepositoryRoot $repositoryRoot

function Get-BoardStatusByIssue {
    # Issue number -> current Status option name for every issue item on the board.
    $json = Invoke-GitHubDirect -Executable gh -NativeArguments @(
        'project', 'item-list', $config.project_number, '--owner', $config.owner,
        '--limit', '1000', '--format', 'json'
    )
    $statusByIssue = @{}
    foreach ($item in (($json -join "`n") | ConvertFrom-Json).items) {
        if ($item.content -and $item.content.type -eq 'Issue') {
            $status = if ($item.PSObject.Properties['status']) { $item.status } else { $null }
            $statusByIssue[[int] $item.content.number] = $status
        }
    }
    return $statusByIssue
}

function Set-BoardStatus {
    param(
        [int] $Number,
        [string] $TargetStatus
    )

    $url = "https://github.com/$repository/issues/$Number"
    $added = Invoke-GitHubDirect -Executable gh -NativeArguments @(
        'project', 'item-add', $config.project_number, '--owner', $config.owner,
        '--url', $url, '--format', 'json'
    )
    $itemId = (($added -join "`n") | ConvertFrom-Json).id
    if (-not $itemId) {
        throw "gh project item-add returned no item ID for #$Number."
    }
    Invoke-GitHubDirect -Executable gh -NativeArguments @(
        'project', 'item-edit', '--id', $itemId, '--project-id', $config.project_id,
        '--field-id', $config.status_field_id,
        '--single-select-option-id', $optionByStatus[$TargetStatus]
    ) | Out-Null
}

function Get-DerivedStatus {
    param($IssueObject)

    if ($IssueObject.state -eq 'CLOSED') { return 'Done' }
    $labels = @($IssueObject.labels | ForEach-Object { $_.name })
    if ($labels -contains 'maqa:in-review') { return 'InReview' }
    if ($labels -contains 'maqa:in-progress' -or $labels -contains 'maqa:blocked') { return 'InProgress' }
    return 'Todo'
}

if ($PSCmdlet.ParameterSetName -eq 'Single') {
    try {
        $wasOnBoard = (Get-BoardStatusByIssue).ContainsKey($Issue)
        Set-BoardStatus -Number $Issue -TargetStatus $Status
        if (-not $wasOnBoard) {
            Start-Sleep -Seconds $settleSeconds
            if ((Get-BoardStatusByIssue)[$Issue] -ne $nameByStatus[$Status]) {
                Set-BoardStatus -Number $Issue -TargetStatus $Status
                Write-Output "Re-applied $Status after a built-in board workflow overwrote it."
            }
        }
        Write-Output "Mirrored #$Issue -> $Status on project $($config.owner)/$($config.project_number)."
    }
    catch {
        Write-Warning "Board mirror failed for #$Issue -> $Status (MAQA state is unaffected): $($_.Exception.Message)"
    }
    exit 0
}

$issues = @(Get-MaqaIssues -Repository $repository | Sort-Object number)
$targetByIssue = @{}
foreach ($item in $issues) {
    $targetByIssue[[int] $item.number] = Get-DerivedStatus -IssueObject $item
}

$failures = 0
$counts = @{ Todo = 0; InProgress = 0; InReview = 0; Done = 0 }
$boardBefore = Get-BoardStatusByIssue
$added = 0
foreach ($item in $issues) {
    $number = [int] $item.number
    $target = $targetByIssue[$number]
    $counts[$target]++
    if ($boardBefore[$number] -eq $nameByStatus[$target]) {
        continue
    }
    if (-not $boardBefore.ContainsKey($number)) { $added++ }
    try {
        Set-BoardStatus -Number $number -TargetStatus $target
        Write-Output ("#{0} -> {1}" -f $number, $target)
    }
    catch {
        $failures++
        Write-Warning "Board mirror failed for #${number}: $($_.Exception.Message)"
    }
}

# Let built-in workflows on newly added items settle, then repair overwrites.
if ($added -gt 0) {
    Start-Sleep -Seconds $settleSeconds
    $boardAfter = Get-BoardStatusByIssue
    foreach ($number in $targetByIssue.Keys) {
        $target = $targetByIssue[$number]
        if ($boardAfter[$number] -ne $nameByStatus[$target]) {
            try {
                Set-BoardStatus -Number $number -TargetStatus $target
                Write-Output ("#{0} -> {1} (re-applied after a built-in board workflow)" -f $number, $target)
            }
            catch {
                $failures++
                Write-Warning "Board mirror repair failed for #${number}: $($_.Exception.Message)"
            }
        }
    }
}

Write-Output ("Backfill of {0} MAQA issue(s): Todo {1}, In progress {2}, In review {3}, Done {4}; {5} newly added, failed {6}." -f
    $issues.Count, $counts.Todo, $counts.InProgress, $counts.InReview, $counts.Done, $added, $failures)
if ($failures -gt 0) {
    exit 1
}
