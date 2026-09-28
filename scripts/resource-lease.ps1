<#
.SYNOPSIS
Leases the single-user workstation resources (Unity editor, Android emulator) across sessions.

.DESCRIPTION
The workstation has one Unity editor slot and one Android emulator. Parallel
agent sessions and MAQA workers that used them at the same time corrupted each
other's evidence (T040 profiling, the emulator journey), and Unity runs with
Android as the active build target kill the adb server that emulator work needs.

A lease is a small JSON file in the Git common directory
(<git-common-dir>/resource-leases/<resource>.json). Every worktree and every
session of this clone sees the same file; nothing is versioned. Creation is
atomic, so two sessions cannot both acquire a free resource.

Resources:
  unity     the Unity editor slot and Unity CLI runs (tests, builds)
  emulator  emulator-5554 and anything that talks to its adb server

A Unity run whose active build target is Android also needs `emulator`,
because it ends with "Killing ADB server". Acquire both in one call:
-Resource unity,emulator.

Actions (exit codes: 0 success, 2 held by another owner, 1 usage/error):
  -Status             list every lease with owner, purpose and expiry
  -Acquire            take the resources, or renew them when you already hold
                      them; all or nothing
  -Check              succeed when each resource is free, expired, or held by
                      -Owner
  -Release            give the resources back; only the owner may release a
                      live lease, anyone may clear an expired one

An expired lease counts as free. Taking it over (with -Acquire) reports the
previous owner, so the takeover is visible in the output.

.EXAMPLE
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/resource-lease.ps1 -Acquire -Resource unity,emulator -Owner "issue-113" -Purpose "T063 Play Mode suite" -TtlMinutes 90

.EXAMPLE
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/resource-lease.ps1 -Status
#>
[CmdletBinding(DefaultParameterSetName = 'Status')]
param(
    [Parameter(ParameterSetName = 'Status')]
    [switch] $Status,

    [Parameter(Mandatory, ParameterSetName = 'Acquire')]
    [switch] $Acquire,

    [Parameter(Mandatory, ParameterSetName = 'Check')]
    [switch] $Check,

    [Parameter(Mandatory, ParameterSetName = 'Release')]
    [switch] $Release,

    # Comma-separated when called through powershell -File, e.g. unity,emulator.
    [Parameter(Mandatory, ParameterSetName = 'Acquire')]
    [Parameter(Mandatory, ParameterSetName = 'Check')]
    [Parameter(Mandatory, ParameterSetName = 'Release')]
    [string[]] $Resource,

    # Stable holder identity: a MAQA issue ("issue-113") or a session name.
    [Parameter(Mandatory, ParameterSetName = 'Acquire')]
    [Parameter(Mandatory, ParameterSetName = 'Check')]
    [Parameter(Mandatory, ParameterSetName = 'Release')]
    [string] $Owner,

    [Parameter(Mandatory, ParameterSetName = 'Acquire')]
    [string] $Purpose,

    [Parameter(ParameterSetName = 'Acquire')]
    [ValidateRange(1, 1440)]
    [int] $TtlMinutes = 120
)

$ErrorActionPreference = 'Stop'
$knownResources = 'unity', 'emulator'

$commonDir = (& git -C $PSScriptRoot rev-parse --git-common-dir).Trim()
if ($LASTEXITCODE -ne 0) {
    throw 'Not inside a Git repository.'
}
if (-not [IO.Path]::IsPathRooted($commonDir)) {
    $commonDir = Join-Path (Split-Path -Parent $PSScriptRoot) $commonDir
}
$leaseDir = Join-Path $commonDir 'resource-leases'
New-Item -ItemType Directory -Force -Path $leaseDir | Out-Null

if ($Resource) {
    # Fixed order keeps multi-resource acquisition deadlock-free.
    $Resource = @($Resource | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim().ToLowerInvariant() } | Where-Object { $_ } | Select-Object -Unique)
    foreach ($name in $Resource) {
        if ($knownResources -notcontains $name) {
            throw "Unknown resource '$name'. Known resources: $($knownResources -join ', ')."
        }
    }
    $Resource = @($knownResources | Where-Object { $Resource -contains $_ })
}

function Get-LeasePath {
    param([string] $Name)
    return Join-Path $leaseDir "$Name.json"
}

function Read-Lease {
    param([string] $Name)
    $path = Get-LeasePath $Name
    if (-not (Test-Path -LiteralPath $path)) { return $null }
    try {
        $lease = Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
    }
    catch {
        # An unreadable lease is treated as expired so it can be cleared.
        return [pscustomobject]@{ resource = $Name; owner = '(unreadable)'; purpose = ''; acquiredUtc = ''; expiresUtc = '2000-01-01T00:00:00Z'; host = ''; worktree = '' }
    }
    return $lease
}

function Test-Expired {
    param($Lease)
    return [DateTimeOffset]::Parse($Lease.expiresUtc) -le [DateTimeOffset]::UtcNow
}

function Format-Lease {
    param($Lease)
    $state = if (Test-Expired $Lease) { 'EXPIRED' } else { 'held' }
    return "{0}: {1} by '{2}' ({3}); acquired {4}, expires {5}; worktree {6}" -f
        $Lease.resource, $state, $Lease.owner, $Lease.purpose, $Lease.acquiredUtc, $Lease.expiresUtc, $Lease.worktree
}

function New-LeaseJson {
    param([string] $Name, [string] $AcquiredUtc)
    $now = [DateTimeOffset]::UtcNow
    return ([ordered]@{
        resource    = $Name
        owner       = $Owner
        purpose     = $Purpose
        acquiredUtc = $(if ($AcquiredUtc) { $AcquiredUtc } else { $now.ToString('yyyy-MM-ddTHH:mm:ssZ') })
        expiresUtc  = $now.AddMinutes($TtlMinutes).ToString('yyyy-MM-ddTHH:mm:ssZ')
        host        = $env:COMPUTERNAME
        worktree    = (Split-Path -Parent $PSScriptRoot)
    } | ConvertTo-Json)
}

function Write-LeaseAtomically {
    # Returns $true when this call created the file, $false when it already existed.
    param([string] $Name, [string] $Json)
    try {
        $stream = [IO.File]::Open((Get-LeasePath $Name), [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    }
    catch [IO.IOException] {
        return $false
    }
    try {
        $bytes = (New-Object System.Text.UTF8Encoding($false)).GetBytes($Json)
        $stream.Write($bytes, 0, $bytes.Length)
    }
    finally {
        $stream.Dispose()
    }
    return $true
}

switch ($PSCmdlet.ParameterSetName) {
    'Status' {
        $files = @(Get-ChildItem -LiteralPath $leaseDir -Filter '*.json' -ErrorAction SilentlyContinue)
        if ($files.Count -eq 0) {
            Write-Output 'No resource leases: unity and emulator are free.'
        }
        foreach ($name in $knownResources) {
            $lease = Read-Lease $name
            if ($lease) { Write-Output (Format-Lease $lease) } else { Write-Output "${name}: free" }
        }
        exit 0
    }

    'Check' {
        $blocked = 0
        foreach ($name in $Resource) {
            $lease = Read-Lease $name
            if (-not $lease) {
                Write-Output "${name}: free"
            }
            elseif ($lease.owner -eq $Owner) {
                Write-Output "${name}: held by you ('$Owner') until $($lease.expiresUtc)"
            }
            elseif (Test-Expired $lease) {
                Write-Output "${name}: free (expired lease of '$($lease.owner)')"
            }
            else {
                Write-Output "BLOCKED $(Format-Lease $lease)"
                $blocked++
            }
        }
        exit $(if ($blocked) { 2 } else { 0 })
    }

    'Acquire' {
        $acquired = New-Object System.Collections.Generic.List[string]
        foreach ($name in $Resource) {
            $lease = Read-Lease $name
            if ($lease -and $lease.owner -eq $Owner) {
                # Renewal: rewrite in place, keeping the original acquisition time.
                [IO.File]::WriteAllText((Get-LeasePath $name), (New-LeaseJson -Name $name -AcquiredUtc $lease.acquiredUtc), (New-Object System.Text.UTF8Encoding($false)))
                Write-Output "${name}: renewed for '$Owner' ($TtlMinutes min)"
                continue
            }
            if ($lease -and (Test-Expired $lease)) {
                Write-Output "${name}: taking over the expired lease of '$($lease.owner)' ($($lease.purpose))"
                Remove-Item -LiteralPath (Get-LeasePath $name) -Force
            }
            if (Write-LeaseAtomically -Name $name -Json (New-LeaseJson -Name $name)) {
                $acquired.Add($name)
                Write-Output "${name}: acquired by '$Owner' for $TtlMinutes min ($Purpose)"
                continue
            }
            # Held by someone else (or created a moment ago): roll back this call.
            $holder = Read-Lease $name
            Write-Output "BLOCKED $(Format-Lease $holder)"
            foreach ($taken in $acquired) {
                Remove-Item -LiteralPath (Get-LeasePath $taken) -Force
                Write-Output "${taken}: released again (all-or-nothing)"
            }
            exit 2
        }
        exit 0
    }

    'Release' {
        $refused = 0
        foreach ($name in $Resource) {
            $lease = Read-Lease $name
            if (-not $lease) {
                Write-Output "${name}: already free"
            }
            elseif ($lease.owner -eq $Owner -or (Test-Expired $lease)) {
                Remove-Item -LiteralPath (Get-LeasePath $name) -Force
                Write-Output "${name}: released (was '$($lease.owner)')"
            }
            else {
                Write-Output "REFUSED ${name}: held by '$($lease.owner)', not '$Owner'; ask that holder to release it"
                $refused++
            }
        }
        exit $(if ($refused) { 2 } else { 0 })
    }
}
