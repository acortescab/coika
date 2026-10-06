<#
.SYNOPSIS
    Builds the Android APK (for testing) and AAB (for the store) of the project from the command line.

.DESCRIPTION
    Runs `unity build --target Android --execute-method Coika.Tools.AndroidBuild.Build`, which in this order:
    applies the committed Android player settings, builds the Addressables content, builds Builds/android/coika.apk,
    checks its merged manifest (VIBRATE required, INTERNET forbidden), builds Builds/android/coika.aab, prints the
    package and Addressables group sizes and fails when a package is bigger than the 50 MB base budget (C-01).
    Builds/ is git-ignored. The exit code is 0 only when the APK and the AAB were built and every check passed.

    The project must not be open in the Unity Editor. Builds are signed with the debug keystore unless the release
    keystore is given through environment variables (never commit them):
    COIKA_KEYSTORE_PATH, COIKA_KEYSTORE_PASS, COIKA_KEY_ALIAS and COIKA_KEY_PASS.

.PARAMETER OutputPath
    Directory for the packages, relative to the repository root. Default: Builds/android.

.PARAMETER EditorPath
    Path to a specific Unity Editor binary. By default the CLI picks the version of ProjectSettings/ProjectVersion.txt.

.EXAMPLE
    ./Tools/build-android.ps1

.EXAMPLE
    $env:COIKA_KEYSTORE_PATH = 'C:\keys\coika.keystore'; $env:COIKA_KEYSTORE_PASS = '...'
    $env:COIKA_KEY_ALIAS = 'coika'; $env:COIKA_KEY_PASS = '...'
    ./Tools/build-android.ps1
#>
[CmdletBinding()]
param(
    [string]$OutputPath = 'Builds/android',

    [string]$EditorPath
)

$ErrorActionPreference = 'Stop'

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$outputDir = Join-Path $projectRoot $OutputPath

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
        Write-Host 'The project is open in the Unity Editor. Close it and run the build again.' -ForegroundColor Red
        exit 2
    }
}

# Old packages must not pass for a fresh build when the new build fails.
foreach ($name in @('coika.apk', 'coika.aab')) {
    $old = Join-Path $outputDir $name
    if (Test-Path $old) { Remove-Item $old -Force }
}
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

$arguments = @('build', $projectRoot, '--target', 'Android', '--execute-method', 'Coika.Tools.AndroidBuild.Build',
    '--output-path', $outputDir, '--non-interactive', '--no-banner')
if ($EditorPath) { $arguments += @('--editor-path', $EditorPath) }

Write-Host '=== Android build ===' -ForegroundColor Cyan
# Windows PowerShell 5.1 turns any stderr line of a native command into a terminating error under 'Stop'.
$ErrorActionPreference = 'Continue'
& unity @arguments
$code = $LASTEXITCODE
$ErrorActionPreference = 'Stop'

$apk = Join-Path $outputDir 'coika.apk'
$aab = Join-Path $outputDir 'coika.aab'

if ($code -ne 0) {
    Write-Host "The Android build failed (exit code $code)." -ForegroundColor Red
    exit 1
}
if (-not ((Test-Path $apk) -and (Test-Path $aab))) {
    Write-Host "The build ended without writing $apk and $aab." -ForegroundColor Red
    exit 1
}

foreach ($file in @($apk, $aab)) {
    $megabytes = [math]::Round((Get-Item $file).Length / 1MB, 2)
    Write-Host "$(Split-Path $file -Leaf): $megabytes MB" -ForegroundColor Green
}
Write-Host 'Android build succeeded.' -ForegroundColor Green
exit 0
