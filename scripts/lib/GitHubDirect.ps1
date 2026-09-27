# Shared helpers for repository scripts that call GitHub CLI or Git.
# See docs/integrations/github-cli.md: the agent shell can inherit a refusing
# proxy (http://127.0.0.1:9), so proxy variables are cleared for the native
# process only and restored afterwards.

Set-StrictMode -Version Latest

function Invoke-GitHubDirect {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, Position = 0)]
        [string] $Executable,

        [Parameter(Mandatory)]
        [string[]] $NativeArguments
    )

    $proxyNames = @(
        'HTTP_PROXY', 'HTTPS_PROXY', 'ALL_PROXY',
        'http_proxy', 'https_proxy', 'all_proxy',
        'NO_PROXY', 'no_proxy'
    )
    $saved = @{}
    foreach ($name in $proxyNames) {
        $saved[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
    }

    try {
        foreach ($name in $proxyNames) {
            [Environment]::SetEnvironmentVariable($name, $null, 'Process')
        }
        $env:NO_PROXY = '127.0.0.1,localhost'
        $env:no_proxy = '127.0.0.1,localhost'

        $output = & $Executable @NativeArguments
        if ($LASTEXITCODE -ne 0) {
            throw "$Executable exited with code $LASTEXITCODE."
        }
        return $output
    }
    finally {
        foreach ($name in $proxyNames) {
            [Environment]::SetEnvironmentVariable($name, $saved[$name], 'Process')
        }
    }
}

function Get-OriginRepository {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string] $RepositoryRoot
    )

    $url = (& git -C $RepositoryRoot remote get-url origin).Trim()
    if ($LASTEXITCODE -ne 0 -or -not $url) {
        throw "Cannot read the origin remote of $RepositoryRoot."
    }
    if ($url -notmatch 'github\.com[:/](?<owner>[^/]+)/(?<name>[^/]+?)(\.git)?$') {
        throw "Origin '$url' is not a GitHub repository."
    }
    return "$($Matches.owner)/$($Matches.name)"
}

function Get-MaqaIssues {
    # Returns every MAQA-managed issue (label `maqa`) of the origin repository.
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string] $Repository
    )

    $json = Invoke-GitHubDirect -Executable gh -NativeArguments @(
        'issue', 'list', '--repo', $Repository, '--label', 'maqa', '--state', 'all',
        '--limit', '1000', '--json', 'number,title,state,labels'
    )
    # Windows PowerShell 5.1 emits a JSON array as one pipeline object, so
    # assign first and let `return` enumerate the elements.
    $issues = ($json -join "`n") | ConvertFrom-Json
    return $issues
}

function Get-TaskIdFromIssueTitle {
    # MAQA issue titles start with the task ID, for example "T061: Render ...".
    param([string] $Title)

    if ($Title -match '^\s*(?<id>T\d{3,})\b') {
        return $Matches.id
    }
    return $null
}
