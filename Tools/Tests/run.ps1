# The edit-mode tests (Assets/Editor/Tests): the game's rules checked in seconds, not in a ten-minute bot run.
# Unity starts headless on the project itself, so the editor must be closed (with it open: Window / General /
# Test Runner, EditMode, Run All). Prints every failure and the time it took; exit code 1 if anything failed.
#   powershell -ExecutionPolicy Bypass -File Tools/Tests/run.ps1 [-Filter LabRulesTests.TheTipIsUpToHalfTheRewardForQuickAndRight]
param([string]$Filter = "", [string]$Unity = "D:\Apps\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe")
$root = Resolve-Path "$PSScriptRoot\..\.."
$xml = "$root\Temp\tests.xml"
$log = "$root\Temp\tests.log"
Remove-Item $xml -ErrorAction SilentlyContinue
$a = @("-batchmode", "-projectPath", $root, "-runTests", "-testPlatform", "EditMode", "-testResults", $xml, "-noaudio", "-logFile", $log)
if ($Filter) { $a += @("-testFilter", $Filter) }
$time = Measure-Command { & $Unity @a | Out-Null }
$errors = Select-String -Path $log -Pattern "error CS" -ErrorAction SilentlyContinue
if ($errors) { $errors | ForEach-Object { "COMPILE ERROR: " + $_.Line }; exit 1 }
if (-not (Test-Path $xml)) { "no results: is the Unity editor open? see $log"; exit 1 }
[xml]$r = Get-Content $xml -Encoding UTF8
$run = $r.'test-run'
foreach ($c in $r.SelectNodes("//test-case[@result='Failed']")) {
    "FAILED " + $c.name
    "  " + ($c.failure.message.'#cdata-section' -replace "`r?`n", "`n  ").Trim()
}
"{0} passed, {1} failed of {2} in {3:0} s" -f $run.passed, $run.failed, $run.total, $time.TotalSeconds
if ([int]$run.failed -gt 0) { exit 1 }
