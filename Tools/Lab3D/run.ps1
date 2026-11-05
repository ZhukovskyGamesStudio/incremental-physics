# The 3D lab, rebuilt headless: Blender makes the models (Tools/Blender/lab3d.py, customers.py, odd.py, desk.py, body.py), Unity builds the scene
# Assets/Scenes/Lab3D.unity (Assets/Editor/Lab3DBuilder.cs) and renders pictures into Build/Lab3D.
# Runs on the project itself, so the Unity editor must be closed (with it open use the menu Chalk / Lab 3D).
# -Check then plays it headless (Assets/Editor/Lab3DCheck.cs): a new game, the map, a day, the door, the map again
#   (about 6 game minutes, 4x fast, silent; the save in memory), with pictures into Build/Lab3D.
#   powershell -ExecutionPolicy Bypass -File Tools/Lab3D/run.ps1 [-NoBlender] [-Check [-Min 7]]
param([switch]$NoBlender, [switch]$Check, [float]$Min = 7, [switch]$Grow,
      [string]$Unity = "D:\Apps\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe",
      [string]$Blender = "D:\Apps\Steam\steamapps\common\Blender\blender.exe")
$root = Resolve-Path "$PSScriptRoot\..\.."
if (-not $NoBlender) {
    # every model script: the room and the apparatus (lab3d.py), the customers (customers.py); add new ones here
    foreach ($script in @("lab3d.py", "customers.py")) {
        & $Blender -b --factory-startup -P "$root\Tools\Blender\$script" -- "$root\Assets\Lab3D\Models" | Select-String "done|Error|Traceback"
    }
}
& $Unity -batchmode -projectPath $root -executeMethod ChalkPhysics.EditorTools.Lab3DBuilder.BuildBatch -noaudio -logFile "$root\Temp\lab3d.log" | Out-Null
Select-String -Path "$root\Temp\lab3d.log" -Pattern "\[LAB3D\]|error CS|Exception" | ForEach-Object { $_.Line }
"pictures: " + (Get-ChildItem "$root\Build\Lab3D" -Filter *.png -ErrorAction SilentlyContinue).Count + " in $root\Build\Lab3D"
if ($Check) {
    & $Unity -batchmode -projectPath $root -executeMethod ChalkPhysics.EditorTools.Lab3DCheck.Run -lab3dMin $Min $(if ($Grow) { "-lab3dGrow" }) -noaudio -logFile "$root\Temp\lab3d_check.log" | Out-Null
    # a script that does not compile leaves the old one running: say so loudly
    Select-String -Path "$root\Temp\lab3d_check.log" -Pattern "error CS" | ForEach-Object { "COMPILE ERROR: " + $_.Line }
    Get-Content "$root\Temp\lab3d_check.txt" -ErrorAction SilentlyContinue
}
