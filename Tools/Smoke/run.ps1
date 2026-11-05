# Headless smoke test (Assets/Editor/SmokeTest.cs): plays the real game in play mode with AutoPlayBot, fast-forwarded,
# on a copy of the project (the editor may stay open), and lists every error and exception.
#   powershell -ExecutionPolicy Bypass -File Tools/Smoke/run.ps1 [-Sec 600] [-Out smoke.txt]
param([int]$Sec = 600, [string]$Out = "",
      [string]$Unity = "D:\Apps\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe")
$src = Resolve-Path "$PSScriptRoot\..\.."
$dst = Join-Path $env:TEMP "incremental-physics-sim"
if ($Out -eq "") { $Out = Join-Path $dst "smoke.txt" }
foreach ($d in "Assets", "Packages", "ProjectSettings") { robocopy "$src\$d" "$dst\$d" /MIR /NFL /NDL /NJH /NJS /NP | Out-Null }
Remove-Item $Out -ErrorAction SilentlyContinue
& $Unity -batchmode -projectPath $dst -executeMethod ChalkPhysics.EditorTools.SmokeTest.Run -noaudio -smokeOut $Out -smokeSec $Sec -logFile "$dst\smoke.log" | Out-Null
if (Test-Path $Out) { Get-Content $Out -TotalCount 60 -Encoding UTF8 } else { "FAILED, see $dst\smoke.log" }
