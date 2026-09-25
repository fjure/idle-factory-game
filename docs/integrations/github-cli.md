# GitHub CLI access from Windows agent shells

**Verified**: 2026-09-24, Europe/Berlin

This workstation can expose `HTTP_PROXY`, `HTTPS_PROXY`, and `ALL_PROXY` (in upper- and lowercase) as `http://127.0.0.1:9`. That endpoint refuses connections. With those variables inherited, `gh auth status` can report the GitHub token as invalid and GitHub CLI or Git operations fail with `proxyconnect tcp`.

On 2026-09-24, clearing those proxy variables for the current PowerShell process made `gh auth status` succeed from the existing Windows keyring. `gh repo view fjure/idle-factory-game --json nameWithOwner,defaultBranchRef,url` then confirmed `fjure/idle-factory-game` and default branch `main`. The credential was already valid; no refresh or credential change was needed.

Use a process-scoped wrapper for GitHub CLI and Git operations. It preserves the caller's environment, removes proxy variables only while the command runs, and keeps loopback addresses out of the proxy path:

```powershell
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

        & $Executable @NativeArguments
        if ($LASTEXITCODE -ne 0) {
            throw "$Executable exited with code $LASTEXITCODE."
        }
    }
    finally {
        foreach ($name in $proxyNames) {
            [Environment]::SetEnvironmentVariable($name, $saved[$name], 'Process')
        }
    }
}

Invoke-GitHubDirect -Executable gh -NativeArguments @('auth','status')
Invoke-GitHubDirect -Executable gh -NativeArguments @(
    'repo','view','fjure/idle-factory-game','--json','nameWithOwner,defaultBranchRef,url'
)
```

Use the same wrapper for `git fetch`, `git push`, and `gh` issue commands in that shell. Every GitHub operation must still target the repository parsed from `origin`; bypassing the proxy does not change MAQA's repository, authentication, or issue-state rules. Do not persist proxy changes in the Windows profile or Git configuration, and do not print or store the keyring token.

## MAQA recovery

When an agent sees `proxyconnect tcp` to `127.0.0.1:9` or a misleading token-invalid result, retry `gh auth status` through `Invoke-GitHubDirect` before requesting a token refresh. Continue only after the repository identity and default branch checks pass. Follow [`docs/spec-kit-greenfield-workflow.md`](../spec-kit-greenfield-workflow.md) for the remaining MAQA gates.
