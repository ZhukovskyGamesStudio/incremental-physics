# Headless balance run (Assets/Editor/BalanceSim.cs): plays the whole game like AutoPlayBot, without animations,
# and writes the minutes to every milestone plus an experiment-by-experiment log of the first run.
# The editor may stay open: the project is copied (without Library) to a temp folder and run there.
#   powershell -File Tools/BalanceSim/run.ps1 [-Out balance.txt] [-Seeds 5]
param([string]$Out = "", [int]$Seeds = 5,
      [string]$Unity = "D:\Apps\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe")
$src = Resolve-Path "$PSScriptRoot\..\.."
$dst = Join-Path $env:TEMP "incremental-physics-sim"
if ($Out -eq "") { $Out = Join-Path $dst "balance.txt" }
foreach ($d in "Assets", "Packages", "ProjectSettings") { robocopy "$src\$d" "$dst\$d" /MIR /NFL /NDL /NJH /NJS /NP | Out-Null }
Remove-Item $Out -ErrorAction SilentlyContinue
& $Unity -batchmode -quit -nographics -projectPath $dst -executeMethod ChalkPhysics.EditorTools.BalanceSim.Batch `
    -simOut $Out -simSeeds $Seeds -logFile "$dst\unity.log" | Out-Null
if (Test-Path $Out) { "written: $Out" } else { "FAILED, see $dst\unity.log"; Select-String -Path "$dst\unity.log" -Pattern "error CS" | Select-Object -First 20 | ForEach-Object { $_.Line } }
