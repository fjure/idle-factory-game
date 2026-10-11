<#
.SYNOPSIS
Runs the Unity Edit Mode and Play Mode tests in batch mode and checks the results.

.PARAMETER Suite
EditMode or PlayMode. Without -Suite (and without -TestFile) both suites run.

.PARAMETER TestFile
Repository-relative C# test file; runs only its single test class.

.PARAMETER Category
NUnit category filter, passed to Unity as -testCategory. The value is passed through unchanged:
`;` separates several categories (any match runs) and a leading `!` negates a category,
for example "BalanceFull", "A;B" or "!BalanceFull". Allowed characters per name: letters, digits,
`_`, `-`, `.`. Quotes, spaces and other characters are rejected before Unity starts.
Rules:
- Requires -Suite or -TestFile, so a category that exists in only one suite cannot fail the other
  suite with zero tests.
- Combines with -TestFile (Unity applies both filters, so the class must contain a matching test).
- Rejected together with -StandaloneWindowsPlayer (OPS-2): that run already fixes its category
  to StandalonePlayer.
- A category run that matches zero tests fails ("discovered zero tests"); it is never a pass.

.PARAMETER StandaloneWindowsPlayer
Runs the tests tagged StandalonePlayer in a Windows player (PlayMode only).
#>
[CmdletBinding()]
param(
    [ValidateSet("EditMode", "PlayMode")]
    [string]$Suite,

    [switch]$StandaloneWindowsPlayer,

    [string]$TestFile,

    [string]$Category,

    [string]$UnityPath = "C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe",

    [ValidateRange(1, 7200)]
    [int]$TimeoutSeconds = 900
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
$resultsRoot = Join-Path $projectRoot "test-results\unity"
$lockPath = Join-Path $projectRoot ".unity-test-runner.lock"
$pinnedVersion = (Get-Content -LiteralPath (Join-Path $projectRoot "ProjectSettings\ProjectVersion.txt") |
        Select-String -Pattern "^m_EditorVersion:\s*(.+)$").Matches.Groups[1].Value.Trim()

function Assert-NoReparsePointWithinProject {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Label
    )

    $projectBoundary = $projectRoot.TrimEnd("\")
    $current = Get-Item -LiteralPath $Path -Force
    while ($current) {
        if (($current.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "$Label cannot traverse a symbolic link or junction: $($current.FullName)"
        }

        $currentPath = $current.FullName.TrimEnd("\")
        if ($currentPath.Equals($projectBoundary, [StringComparison]::OrdinalIgnoreCase)) {
            return
        }

        $current = if ($current -is [IO.DirectoryInfo]) {
            $current.Parent
        }
        elseif ($current -is [IO.FileInfo]) {
            $current.Directory
        }
        else {
            throw "$Label has an unsupported filesystem item type: $($current.GetType().FullName)"
        }
    }

    throw "$Label is not rooted in the repository: $Path"
}

function Resolve-FocusedTest {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [string]$RequestedSuite
    )

    if ([IO.Path]::IsPathRooted($Path)) {
        throw "Focused test path must be repository-relative: $Path"
    }
    $candidate = Join-Path $projectRoot $Path

    if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
        throw "Focused test file does not exist: $Path"
    }

    $resolved = (Resolve-Path -LiteralPath $candidate).Path
    $projectPrefix = $projectRoot.TrimEnd("\") + "\"
    if (-not $resolved.StartsWith($projectPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Focused test file must be inside the repository: $resolved"
    }
    Assert-NoReparsePointWithinProject -Path $resolved -Label "Focused test file"

    if ([IO.Path]::GetExtension($resolved) -ne ".cs") {
        throw "Focused test input must be a C# test file: $resolved"
    }

    $relative = $resolved.Substring($projectPrefix.Length).Replace("\", "/")
    $detectedSuite = if ($relative -match "(?i)/Tests/EditMode/") {
        "EditMode"
    }
    elseif ($relative -match "(?i)/Tests/PlayMode/") {
        "PlayMode"
    }
    else {
        throw "Cannot map focused file to EditMode or PlayMode from its path: $relative"
    }

    if ($RequestedSuite -and $RequestedSuite -ne $detectedSuite) {
        throw "Focused file maps to $detectedSuite but -Suite requested $RequestedSuite."
    }

    $source = Get-Content -LiteralPath $resolved -Raw
    $namespaceMatches = [regex]::Matches(
        $source,
        "(?m)^\s*namespace\s+([A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*)\s*[;{]"
    )
    if ($namespaceMatches.Count -ne 1) {
        throw "Focused test files must declare exactly one namespace: $relative"
    }
    $namespace = $namespaceMatches[0].Groups[1].Value

    $classMatches = [regex]::Matches(
        $source,
        "(?m)^\s*(?:(?:public|internal)\s+)?(?:(?:sealed|abstract|static)\s+)?class\s+([A-Za-z_][A-Za-z0-9_]*)"
    )
    if ($classMatches.Count -ne 1 -or $source -match "(?m)^\s*.*\bpartial\s+class\b" -or
        $source -match "(?m)^\s*.*\bclass\s+[A-Za-z_][A-Za-z0-9_]*\s*<") {
        throw "Focused test files must contain exactly one non-partial, non-generic class: $relative"
    }
    if ($source -notmatch "\[(?:Test|TestCase|TestCaseSource|UnityTest)\b") {
        throw "Focused file contains no recognized test attribute: $relative"
    }

    $className = "$namespace.$($classMatches[0].Groups[1].Value)"
    $assemblyDirectory = Split-Path -Parent $resolved
    $assetsRoot = Join-Path $projectRoot "Assets"
    $assemblyPath = $null
    while ($assemblyDirectory.StartsWith($assetsRoot, [StringComparison]::OrdinalIgnoreCase)) {
        $assemblyFiles = @(Get-ChildItem -LiteralPath $assemblyDirectory -Filter "*.asmdef" -File)
        if ($assemblyFiles.Count -gt 1) {
            throw "Multiple owning asmdefs found for focused file in $assemblyDirectory."
        }
        if ($assemblyFiles.Count -eq 1) {
            $assemblyPath = $assemblyFiles[0].FullName
            break
        }
        if ($assemblyDirectory -eq $assetsRoot) {
            break
        }
        $assemblyDirectory = Split-Path -Parent $assemblyDirectory
    }
    if (-not $assemblyPath) {
        throw "No owning test asmdef found for focused file: $relative"
    }
    Assert-NoReparsePointWithinProject -Path $assemblyPath -Label "Owning test asmdef"

    $assembly = Get-Content -LiteralPath $assemblyPath -Raw | ConvertFrom-Json
    if (@($assembly.optionalUnityReferences) -notcontains "TestAssemblies") {
        throw "Owning asmdef is not a Unity test assembly: $assemblyPath"
    }
    $includePlatforms = @($assembly.includePlatforms)
    if ($detectedSuite -eq "EditMode" -and $includePlatforms -notcontains "Editor") {
        throw "EditMode test asmdef must include the Editor platform: $assemblyPath"
    }
    if ($detectedSuite -eq "PlayMode" -and $includePlatforms -contains "Editor") {
        throw "PlayMode test asmdef cannot be Editor-only: $assemblyPath"
    }

    [pscustomobject]@{
        AbsolutePath = $resolved
        RelativePath = $relative
        Suite        = $detectedSuite
        AssemblyPath = $assemblyPath
        AssemblyName = [string]$assembly.name
        Classes      = @($className)
        Filter       = $className
    }
}

function Assert-NoActiveEditor {
    $instancePath = Join-Path $projectRoot "Library\EditorInstance.json"
    if (-not (Test-Path -LiteralPath $instancePath -PathType Leaf)) {
        return
    }

    try {
        $instance = Get-Content -LiteralPath $instancePath -Raw | ConvertFrom-Json
    }
    catch {
        throw "Cannot validate existing Unity editor ownership file '$instancePath': $($_.Exception.Message)"
    }

    $processId = 0
    if ($null -eq $instance.process_id -or -not ([int]::TryParse([string]$instance.process_id, [ref]$processId))) {
        throw "Unity editor ownership file has no valid process_id: $instancePath"
    }
    if (Get-Process -Id $processId -ErrorAction SilentlyContinue) {
        throw "Unity process $processId already owns this checkout. Close it before batch tests."
    }
}

function ConvertTo-NativeArgument {
    param([Parameter(Mandatory = $true)][string]$Value)

    if ($Value.Contains('"')) {
        throw "Unity arguments must not contain quotation marks."
    }

    $escaped = $Value
    if ($escaped.EndsWith("\")) {
        $escaped += "\"
    }
    return '"' + $escaped + '"'
}

# Prints a bounded failure digest so agents need not open the full result XML or Unity log.
function Write-UnityFailureSummary {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [object[]]$NonPassedCases = @(),
        [string]$LogText = "",
        [string[]]$FatalPatterns = @(),
        [int]$MaxCases = 10,
        [int]$MaxMessageLines = 4,
        [int]$MaxLogMatches = 10,
        [int]$LogContextLines = 2
    )

    Write-Host "UNITY_TEST_FAILURE_SUMMARY suite=$Name nonPassed=$($NonPassedCases.Count) fatalLogPatterns=$($FatalPatterns.Count)"

    foreach ($case in @($NonPassedCases | Select-Object -First $MaxCases)) {
        Write-Host "  [$($case.result)] $($case.fullname)"
        $message = $case.SelectSingleNode("failure/message")
        if ($null -eq $message) {
            $message = $case.SelectSingleNode("reason/message")
        }
        if ($null -ne $message) {
            @(([string]$message.InnerText).Trim() -split "\r?\n" |
                    Select-Object -First $MaxMessageLines) |
                ForEach-Object { Write-Host "      $_" }
        }
        $stack = $case.SelectSingleNode("failure/stack-trace")
        if ($null -ne $stack) {
            $frame = @(([string]$stack.InnerText).Trim() -split "\r?\n" |
                    Where-Object { $_ -match "\.cs:\d+|\(at " } | Select-Object -First 1)
            if ($frame.Count -gt 0) {
                Write-Host "      at: $($frame[0].Trim())"
            }
        }
    }
    if ($NonPassedCases.Count -gt $MaxCases) {
        Write-Host "  ... $($NonPassedCases.Count - $MaxCases) more non-passed tests in the result XML"
    }

    if ($FatalPatterns.Count -gt 0 -and $LogText) {
        $logLines = $LogText -split "\r?\n"
        $shown = 0
        for ($i = 0; $i -lt $logLines.Count -and $shown -lt $MaxLogMatches; $i++) {
            $line = $logLines[$i]
            $hit = @($FatalPatterns | Where-Object { $line -match ($_ -replace '^\(\?m\)', '') })
            if ($hit.Count -eq 0) {
                continue
            }
            $from = [Math]::Max(0, $i - $LogContextLines)
            $to = [Math]::Min($logLines.Count - 1, $i + $LogContextLines)
            Write-Host "  log line $($i + 1):"
            $logLines[$from..$to] | ForEach-Object { Write-Host "      $_" }
            $shown++
            $i = $to
        }
        if ($shown -ge $MaxLogMatches) {
            Write-Host "  ... further fatal log matches truncated; see the log"
        }
    }
}

function Invoke-UnitySuite {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("EditMode", "PlayMode", "PlayerPlayMode")]
        [string]$Name,
        [object]$Focus,
        [Parameter(Mandatory = $true)]
        [string]$RunDirectory,
        [string]$CategoryFilter
    )

    $resultPath = Join-Path $RunDirectory "$Name-results.xml"
    $logPath = Join-Path $RunDirectory "$Name.log"
    $startedUtc = [DateTime]::UtcNow
    $testPlatform = if ($Name -eq "PlayerPlayMode") { "StandaloneWindows64" } else { $Name }

    $arguments = @("-batchmode")
    if ($Name -eq "PlayerPlayMode") {
        # Explicitly select Windows before Unity starts the test-player build.
        # A batch run against an Android target shuts down the shared adb server.
        $arguments += @("-buildTarget", "win64")
    }
    $arguments += @(
        "-projectPath", $projectRoot,
        "-runTests",
        "-testPlatform", $testPlatform,
        "-testResults", $resultPath,
        "-logFile", $logPath
    )
    if ($Focus) {
        $arguments += @("-testFilter", $Focus.Filter)
    }
    if ($CategoryFilter) {
        $arguments += @("-testCategory", $CategoryFilter)
    }
    if ($Name -eq "PlayerPlayMode") {
        # The existing PlayMode suite includes fixtures that rely on Editor-only APIs.
        # Only tests deliberately marked as runnable in a standalone Player belong here.
        $arguments += @("-testCategory", "StandalonePlayer")
    }

    Write-Host "Running Unity $Name tests..."
    $argumentLine = ($arguments | ForEach-Object { ConvertTo-NativeArgument -Value $_ }) -join " "
    $unityProcess = Start-Process -FilePath $UnityPath -ArgumentList $argumentLine -PassThru -WindowStyle Hidden
    if (-not $unityProcess.WaitForExit($TimeoutSeconds * 1000)) {
        $terminated = $false
        try {
            $taskkillPath = Join-Path $env:SystemRoot "System32\taskkill.exe"
            & $taskkillPath /PID $unityProcess.Id /T /F *> $null
            $terminated = $unityProcess.WaitForExit(10000)
        }
        catch {
            Write-Warning "Could not terminate timed-out Unity process tree $($unityProcess.Id): $($_.Exception.Message)"
        }
        if (-not $terminated) {
            try {
                Stop-Process -Id $unityProcess.Id -Force -ErrorAction Stop
                $terminated = $unityProcess.WaitForExit(5000)
            }
            catch {
                Write-Warning "Could not terminate timed-out Unity process $($unityProcess.Id): $($_.Exception.Message)"
            }
        }
        if (-not $terminated) {
            $script:removeOwnedLock = $false
            throw "$Name timed out after $TimeoutSeconds seconds and process $($unityProcess.Id) could not be terminated. Checkout lock retained. Log: $logPath"
        }
        throw "$Name timed out after $TimeoutSeconds seconds. Log: $logPath"
    }
    $unityProcess.WaitForExit()
    $unityExitCode = $unityProcess.ExitCode

    if (-not (Test-Path -LiteralPath $resultPath -PathType Leaf)) {
        throw "$Name produced no result XML. Unity exit code: $unityExitCode. Log: $logPath"
    }
    if (-not (Test-Path -LiteralPath $logPath -PathType Leaf)) {
        throw "$Name produced no log file."
    }

    $resultInfo = Get-Item -LiteralPath $resultPath
    $logInfo = Get-Item -LiteralPath $logPath
    if ($resultInfo.LastWriteTimeUtc -lt $startedUtc.AddSeconds(-2) -or
        $logInfo.LastWriteTimeUtc -lt $startedUtc.AddSeconds(-2)) {
        throw "$Name produced stale result or log output."
    }

    [xml]$resultXml = Get-Content -LiteralPath $resultPath -Raw
    $testCases = @($resultXml.SelectNodes("//test-case"))
    if ($testCases.Count -eq 0) {
        throw "$Name discovered zero tests. Result: $resultPath"
    }

    $rootResult = [string]$resultXml."test-run".result
    $passedCases = @($testCases | Where-Object { $_.result -eq "Passed" })
    $failedCases = @($testCases | Where-Object { $_.result -eq "Failed" })
    $skippedCases = @($testCases | Where-Object { $_.result -eq "Skipped" })
    $nonPassedCases = @($testCases | Where-Object { $_.result -ne "Passed" })
    $logText = Get-Content -LiteralPath $logPath -Raw
    $knownBenignLogLines = @(
        "[Licensing::Module] Error: Access token is unavailable; failed to update"
    )
    $relevantLogText = (($logText -split "\r?\n") |
            Where-Object { $_ -notin $knownBenignLogLines }) -join [Environment]::NewLine
    $fatalPatterns = @(
        "error CS\d{4}:",
        "Scripts have compiler errors",
        "Compilation failed",
        "Aborting batchmode due to failure",
        "Unhandled Exception",
        "(?m)^\[[^\]]+\]\s+Error:",
        "(?m)^Error:",
        "(?m)^Assertion failed",
        "(?m)^(?:NullReferenceException|MissingReferenceException|IndexOutOfRangeException|ArgumentOutOfRangeException|InvalidOperationException|UnityException):",
        "(?m)^.*(?:Debug(?:\.|:)LogError|LogError)\b"
    )
    $fatalMatches = @($fatalPatterns | Where-Object { $relevantLogText -match $_ })

    if ($Focus) {
        $unexpectedClasses = @($testCases | ForEach-Object { [string]$_.classname } |
                Where-Object { $_ -and $_ -notin $Focus.Classes } |
                Select-Object -Unique)
        if ($unexpectedClasses.Count -gt 0) {
            throw "Focused run returned tests outside '$($Focus.RelativePath)': $($unexpectedClasses -join ', ')"
        }
    }

    if ($unityExitCode -ne 0 -or $rootResult -ne "Passed" -or $passedCases.Count -eq 0 -or
        $nonPassedCases.Count -gt 0 -or $fatalMatches.Count -gt 0) {
        Write-UnityFailureSummary -Name $Name -NonPassedCases $nonPassedCases `
            -LogText $relevantLogText -FatalPatterns $fatalMatches
        throw (
            "$Name failed: exit=$unityExitCode, result=$rootResult, tests=$($testCases.Count), " +
            "passed=$($passedCases.Count), failed=$($failedCases.Count), skipped=$($skippedCases.Count), " +
            "otherNonPassed=$($nonPassedCases.Count - $failedCases.Count - $skippedCases.Count), " +
            "fatalLogPatterns=$($fatalMatches.Count). " +
            "Result: $resultPath Log: $logPath"
        )
    }

    Write-Host (
        "UNITY_TEST_PASS suite=$Name tests=$($testCases.Count) passed=$($passedCases.Count) skipped=0 " +
        "result=$resultPath log=$logPath"
    )
}

if ($PSBoundParameters.ContainsKey("Category")) {
    if ($StandaloneWindowsPlayer) {
        throw "-Category cannot be combined with -StandaloneWindowsPlayer (that run is fixed to the StandalonePlayer category)."
    }
    $categoryName = "!?[A-Za-z0-9_.\-]+"
    if ([string]::IsNullOrEmpty($Category) -or $Category -cnotmatch "^$categoryName(;$categoryName)*$") {
        throw "Invalid -Category '$Category': use names of letters, digits, '_', '-', '.', optionally prefixed with '!', separated by ';'. No quotes or spaces."
    }
    if (-not $Suite -and -not $TestFile) {
        throw "-Category requires -Suite EditMode|PlayMode or -TestFile."
    }
}
if ($StandaloneWindowsPlayer -and $Suite -eq "EditMode") {
    throw "Standalone Windows Player runs support PlayMode tests only."
}
if (-not (Test-Path -LiteralPath $UnityPath -PathType Leaf)) {
    throw "Unity executable not found: $UnityPath"
}
if (-not $pinnedVersion) {
    throw "Could not read the pinned Unity version."
}
if ([IO.Path]::GetFullPath($UnityPath) -notmatch [regex]::Escape($pinnedVersion)) {
    throw "Unity path does not match pinned editor ${pinnedVersion}: $UnityPath"
}

$focus = if ($TestFile) { Resolve-FocusedTest -Path $TestFile -RequestedSuite $Suite } else { $null }
if ($StandaloneWindowsPlayer -and $focus -and $focus.Suite -ne "PlayMode") {
    throw "Standalone Windows Player runs support PlayMode tests only: $($focus.RelativePath)"
}
$suites = if ($StandaloneWindowsPlayer) {
    @("PlayerPlayMode")
}
elseif ($focus) {
    @($focus.Suite)
}
elseif ($Suite) {
    @($Suite)
}
else {
    @("EditMode", "PlayMode")
}

New-Item -ItemType Directory -Path $resultsRoot -Force | Out-Null
$runDirectory = Join-Path $resultsRoot ([DateTime]::UtcNow.ToString("yyyyMMddTHHmmssfffZ"))
New-Item -ItemType Directory -Path $runDirectory | Out-Null

$lockStream = $null
$removeOwnedLock = $true
try {
    Assert-NoActiveEditor
    $lockStream = [IO.File]::Open(
        $lockPath,
        [IO.FileMode]::CreateNew,
        [IO.FileAccess]::Write,
        [IO.FileShare]::None
    )

    foreach ($suiteName in $suites) {
        Invoke-UnitySuite -Name $suiteName -Focus $focus -RunDirectory $runDirectory -CategoryFilter $Category
    }
}
finally {
    if ($lockStream) {
        $lockStream.Dispose()
        if ($removeOwnedLock -and (Test-Path -LiteralPath $lockPath)) {
            Remove-Item -LiteralPath $lockPath -Force
        }
    }
}
