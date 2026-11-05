using UnityEditor;
using UnityEngine;

namespace ChalkPhysics.EditorTools
{
    /// Command-line Windows build: the map and the 3D lab day, a 64-bit Mono player.
    /// Unity.exe -batchmode -quit -projectPath <project> -buildTarget Win64 -executeMethod ChalkPhysics.EditorTools.WinBuild.Build
    /// Tools/Build/win.ps1 runs it.
    public static class WinBuild
    {
        public static void Build()
        {
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Game.unity", "Assets/Scenes/Lab3D.unity" },
                locationPathName = "Build/Windows/Incremental physics.exe",
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log($"[WinBuild] {report.summary.result}, {report.summary.totalSize / 1048576} MB, errors {report.summary.totalErrors}");
            EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
        }
    }
}
