# Windows build of the game (the map and the 3D lab day) into Build/Windows, headless.
# Runs on the project itself, so the Unity editor must be closed.
#   powershell -ExecutionPolicy Bypass -File Tools/Build/win.ps1
param([string]$Unity = "D:\Apps\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe")
$root = Resolve-Path "$PSScriptRoot\..\.."
& $Unity -batchmode -projectPath $root -buildTarget Win64 -executeMethod ChalkPhysics.EditorTools.WinBuild.Build -logFile "$root\Temp\winbuild.log" | Out-Null
Select-String -Path "$root\Temp\winbuild.log" -Pattern "\[WinBuild\]|error CS|Build Failed|Exception" | ForEach-Object { $_.Line }
