<#
.SYNOPSIS
Points this clone's Git hooks at the versioned .githooks directory.

.DESCRIPTION
Sets `core.hooksPath` to `.githooks` in the local repository configuration. The
setting is shared by every worktree of this clone; a worktree whose checkout
predates .githooks simply runs no hook. Run it once per clone. Undo with
`git config --unset core.hooksPath`.
#>
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
& git -C $repositoryRoot config core.hooksPath .githooks
if ($LASTEXITCODE -ne 0) {
    throw "git config failed with exit code $LASTEXITCODE."
}
Write-Output "core.hooksPath = $(& git -C $repositoryRoot config core.hooksPath)"
