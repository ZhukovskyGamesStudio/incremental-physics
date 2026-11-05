# The game's art for an artist (Assets/Editor/ArtKit.cs): renders the pieces and screenshots of real play headless,
# on a copy of the project (the editor may stay open), then packs them into Build/ArtPack (pack.py).
#   powershell -ExecutionPolicy Bypass -File Tools/ArtKit/run.ps1 [-Sec 300]
param([int]$Sec = 300,
      [string]$Unity = "D:\Apps\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe")
$src = Resolve-Path "$PSScriptRoot\..\.."
$dst = Join-Path $env:TEMP "incremental-physics-sim"
foreach ($d in "Assets", "Packages", "ProjectSettings") { robocopy "$src\$d" "$dst\$d" /MIR /NFL /NDL /NJH /NJS /NP | Out-Null }
Remove-Item "$dst\Build\ItchArt" -Recurse -ErrorAction SilentlyContinue
& $Unity -batchmode -projectPath $dst -executeMethod ChalkPhysics.EditorTools.ArtKit.Run -noaudio -artSec $Sec -logFile "$dst\artkit.log" | Out-Null
"rendered: " + (Get-ChildItem "$dst\Build\ItchArt" -Filter *.png -ErrorAction SilentlyContinue).Count + " pictures in $dst\Build\ItchArt"
