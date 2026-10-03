<#
.SYNOPSIS
    Runs the EditMode and PlayMode test suites of the project from the command line.

.DESCRIPTION
    Calls `unity test <project> --mode <mode> --report-format junit` once per mode and writes one JUnit report per
    mode to TestResults/ (git-ignored). Both modes always run, even when the first one fails, so one run shows every
    failure. The exit code is 0 only when every selected suite ran and passed; any failure, missing report or
    startup problem gives a non-zero exit code.

    The project must not be open in the Unity Editor: two Editors cannot use the same project. PlayMode tests that
    load scenes need the Addressables Play Mode Script set to "Use Asset Database" (the default of the project).

.PARAMETER Mode
    Which suites to run: All (default), EditMode or PlayMode.

.PARAMETER Filter
    Only run tests whose names match this filter (passed to `unity test --filter`).

.PARAMETER TimeoutSeconds
    Kill the Unity process of a suite after this many seconds. 0 (default) means no limit.

.PARAMETER Repeat
    Run the selected suites this many times (default 1), for example 10 to check that no test is flaky. The reports of
    a repeated run get the run number in their name.

.PARAMETER EditorPath
    Path to a specific Unity Editor binary. By default the CLI picks the version of ProjectSettings/ProjectVersion.txt.

.EXAMPLE
    ./Tools/run-tests.ps1

.EXAMPLE
    ./Tools/run-tests.ps1 -Mode PlayMode -Filter "Coika.Tests.PlayMode.ScoreIntegrationPlayModeTests"

.EXAMPLE
    ./Tools/run-tests.ps1 -Repeat 10
#>
[CmdletBinding()]
param(
    [ValidateSet('All', 'EditMode', 'PlayMode')]
    [string]$Mode = 'All',

    [string]$Filter,

    [int]$TimeoutSeconds = 0,

    [ValidateRange(1, 100)]
    [int]$Repeat = 1,

    [string]$EditorPath
)

$ErrorActionPreference = 'Stop'

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$resultsDir = Join-Path $projectRoot 'TestResults'

if (-not (Get-Command unity -ErrorAction SilentlyContinue)) {
    Write-Host 'The unity CLI was not found on PATH. Install it and open a new terminal.' -ForegroundColor Red
    exit 2
}

# A project that is open in an Editor holds an exclusive lock on Temp/UnityLockfile.
$lockFile = Join-Path $projectRoot 'Temp/UnityLockfile'
if (Test-Path $lockFile) {
    try {
        $stream = [System.IO.File]::Open($lockFile, 'Open', 'ReadWrite', 'None')
        $stream.Close()
    }
    catch {
        Write-Host 'The project is open in the Unity Editor. Close it and run the tests again.' -ForegroundColor Red
        exit 2
    }
}

New-Item -ItemType Directory -Force -Path $resultsDir | Out-Null

# C-01 pre-flight (runs whatever the mode): the same grep rules as ProjectFoundationTests and TestHygieneTests, so a
# PlayMode-only run is checked too. Comment lines are ignored; the fixtures that hold the patterns as text are skipped.
function Find-Violations([string]$Pattern, [string[]]$Roots, [string[]]$AllowedFiles) {
    $skipped = @('ProjectFoundationTests.cs', 'TestHygieneTests.cs') + $AllowedFiles
    $found = @()
    foreach ($root in $Roots) {
        Get-ChildItem -Path (Join-Path $projectRoot $root) -Filter '*.cs' -Recurse -File |
            Where-Object { $skipped -notcontains $_.Name } |
            ForEach-Object {
                $file = $_
                $lineNumber = 0
                foreach ($line in [System.IO.File]::ReadLines($file.FullName)) {
                    $lineNumber++
                    $text = $line.TrimStart()
                    if (-not $text.StartsWith('//') -and $text -match $Pattern) {
                        $found += "$($file.Name):${lineNumber}: $text"
                    }
                }
            }
    }
    return $found
}

$violations = @()
$violations += Find-Violations '\bResources\.Load' @('Assets/Scripts', 'Assets/Tests') @()
$violations += Find-Violations '\bSceneManager\.LoadScene' @('Assets/Scripts', 'Assets/Tests') @('GameInstaller.cs', 'SceneLoaderService.cs')
$violations += Find-Violations '\bAddressables\.[A-Z]\w*\s*[(<]' @('Assets/Scripts', 'Assets/Tests') @('AssetService.cs', 'SceneLoaderService.cs', 'GameInstaller.cs')
$resourcesFolders = Get-ChildItem -Path (Join-Path $projectRoot 'Assets') -Directory -Recurse -Filter 'Resources' |
    Where-Object { $_.FullName -notmatch 'TextMesh Pro' }
foreach ($folder in $resourcesFolders) { $violations += "Resources folder: $($folder.FullName)" }

if ($violations.Count -gt 0) {
    Write-Host 'C-01 violations found:' -ForegroundColor Red
    $violations | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    exit 1
}

Write-Host 'C-01 grep checks passed.' -ForegroundColor Green

$modes = if ($Mode -eq 'All') { @('EditMode', 'PlayMode') } else { @($Mode) }
$failed = @()

for ($run = 1; $run -le $Repeat; $run++) {
foreach ($suite in $modes) {
    $reportName = if ($Repeat -gt 1) { "$suite.$run.junit.xml" } else { "$suite.junit.xml" }
    $report = Join-Path $resultsDir $reportName
    if (Test-Path $report) {
        Remove-Item $report -Force
    }

    $arguments = @('test', $projectRoot, '--mode', $suite, '--report-format', 'junit', '--output', $report, '--non-interactive', '--no-banner')
    if ($Filter) { $arguments += @('--filter', $Filter) }
    if ($TimeoutSeconds -gt 0) { $arguments += @('--timeout', $TimeoutSeconds) }
    if ($EditorPath) { $arguments += @('--editor-path', $EditorPath) }

    Write-Host "=== $suite tests (run $run of $Repeat) ===" -ForegroundColor Cyan
    # Windows PowerShell 5.1 turns any stderr line of a native command into a terminating error under 'Stop'.
    $ErrorActionPreference = 'Continue'
    & unity @arguments
    $code = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'

    if ($code -ne 0) {
        Write-Host "$suite tests failed (exit code $code)." -ForegroundColor Red
        $failed += "$suite (run $run)"
    }
    elseif (-not (Test-Path $report)) {
        Write-Host "$suite tests ended without writing $report." -ForegroundColor Red
        $failed += "$suite (run $run)"
    }
    else {
        Write-Host "$suite tests passed. Report: $report" -ForegroundColor Green
    }
}
}

if ($failed.Count -gt 0) {
    Write-Host ("Failed suites: " + ($failed -join ', ')) -ForegroundColor Red
    exit 1
}

Write-Host 'All selected suites passed.' -ForegroundColor Green
exit 0
