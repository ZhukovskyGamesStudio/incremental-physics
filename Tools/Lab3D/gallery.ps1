# The gallery: every model and game object of the 3D lab as it looks in the game (the real camera and chalk renderer)
# into Build/Gallery/*.png, with a contact sheet Build/Gallery/index.html (Assets/Editor/Lab3DGallery.cs).
# Plays the Lab3D scene headless and silent with a new game in memory that owns everything; the player's save is
# never touched. Runs on the project itself, so the Unity editor must be closed.
#   powershell -ExecutionPolicy Bypass -File Tools/Lab3D/gallery.ps1 [-Only customer_sailor] [-Build] [-NoBlender]
# -Only <substring>: just the pictures whose names have it (customer_, customer_sailor, dept_tongue... dept_taste,
#   desk_, samples_, room_); the others in the folder are kept and the contact sheet lists them all.
# -Build: first rebuild the models and the scene (Tools/Lab3D/run.ps1; -NoBlender skips Blender).
param([string]$Only = "", [switch]$Build, [switch]$NoBlender,
      [string]$Unity = "D:\Apps\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe")
$root = Resolve-Path "$PSScriptRoot\..\.."
if ($Build) { & "$PSScriptRoot\run.ps1" -NoBlender:$NoBlender | Out-Null }
$a = @("-batchmode", "-projectPath", $root, "-executeMethod", "ChalkPhysics.EditorTools.Lab3DGallery.Run", "-noaudio", "-logFile", "$root\Temp\gallery.log")
if ($Only) { $a += @("-galleryOnly", $Only) }
Remove-Item "$root\Temp\gallery.txt" -ErrorAction SilentlyContinue
$time = Measure-Command { & $Unity @a | Out-Null }
Select-String -Path "$root\Temp\gallery.log" -Pattern "error CS" | ForEach-Object { "COMPILE ERROR: " + $_.Line }
$log = Get-Content "$root\Temp\gallery.txt" -Encoding UTF8 -ErrorAction SilentlyContinue
if (-not $log) { "no gallery log: is the Unity editor open? see $root\Temp\gallery.log" }
$log | Where-Object { $_ -notmatch "^снимок " }
"{0:0} s; contact sheet: {1}" -f $time.TotalSeconds, "$root\Build\Gallery\index.html"
