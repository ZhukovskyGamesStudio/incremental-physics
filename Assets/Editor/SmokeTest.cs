using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChalkPhysics.EditorTools
{
    /// Headless smoke test of the real screens: opens the game scene, lets AutoPlayBot play it in play mode for a while
    /// (fast-forwarded), and writes every error and exception to a file. Catches what the balance sim cannot: the bench,
    /// its little games, the drags, the cards. The editor may reload its assemblies on the way, so the test keeps its
    /// state in SessionState and picks itself up again after a reload.
    /// Unity.exe -batchmode -projectPath <copy> -executeMethod ChalkPhysics.EditorTools.SmokeTest.Run -smokeOut <file> [-smokeSec 90]
    [InitializeOnLoad]
    public static class SmokeTest
    {
        const string On = "smoke.on", Out = "smoke.out", Sec = "smoke.sec", Started = "smoke.started", Log = "smoke.log", Errors = "smoke.errors";

        static SmokeTest()
        {
            if (!SessionState.GetBool(On, false)) return;
            Application.logMessageReceived += OnLog;
            EditorApplication.update += Step;
        }

        public static void Run()
        {
            var args = System.Environment.GetCommandLineArgs();
            string Arg(string k, string def) { int i = System.Array.IndexOf(args, k); return i >= 0 && i + 1 < args.Length ? args[i + 1] : def; }
            SessionState.SetString(Out, Arg("-smokeOut", Path.Combine(Application.dataPath, "../Logs/smoke.txt")));
            SessionState.SetFloat(Sec, float.Parse(Arg("-smokeSec", "90"), System.Globalization.CultureInfo.InvariantCulture));
            SessionState.SetFloat(Started, -1);
            SessionState.SetString(Log, "");
            SessionState.SetInt(Errors, 0);
            SessionState.SetBool(On, true);
            GameState.DeleteSave();
            Application.logMessageReceived += OnLog;
            EditorApplication.update += Step;
        }

        static void OnLog(string msg, string stack, LogType type)
        {
            bool bot = type == LogType.Log && msg.StartsWith("[BOT]");
            bool bad = type == LogType.Error || type == LogType.Exception || type == LogType.Assert;
            if (!bot && !bad) return;
            if (bad && (stack.Contains("UnityEditor.Search") || msg.Contains("SearchDatabase"))) return;   // the editor's own indexer
            int n = SessionState.GetInt(Errors, 0);
            if (bad) SessionState.SetInt(Errors, ++n);
            if (bad && n > 30) return;
            SessionState.SetString(Log, SessionState.GetString(Log, "") + (bad ? $"{type}: {msg}\n{stack}\n" : msg + "\n"));
        }

        static void Step()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            float started = SessionState.GetFloat(Started, -1);
            if (!EditorApplication.isPlaying && started < 0)
            {   // the scene, the bot, fast-forward, play
                if (EditorApplication.timeSinceStartup < 15) return;
                EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
                var root = Object.FindAnyObjectByType<GameRoot>();
                if (root == null) { Finish("FAILED: no GameRoot in the scene", 1); return; }
                AudioListener.volume = 0; Sfx.Muted = true;
                root.autoPlayBot = true;
                root.timeScale = 6f;
                SessionState.SetFloat(Started, (float)EditorApplication.timeSinceStartup);
                EditorApplication.isPlaying = true;
                return;
            }
            if (EditorApplication.isPlaying)
            {
                var gr = Object.FindAnyObjectByType<GameRoot>();
                if (gr != null) { gr.devSpeed = 40; Time.timeScale = 6; Time.maximumDeltaTime = 1f; }
                AudioListener.volume = 0; AudioListener.pause = true; Sfx.Muted = true;   // silent: never a sound from a test
            }
            if (started < 0 || EditorApplication.timeSinceStartup - started < SessionState.GetFloat(Sec, 90)) return;
            var g = GameState.I;
            int errors = SessionState.GetInt(Errors, 0);
            Finish($"Smoke test: {errors} errors in {SessionState.GetFloat(Sec, 90):0} s (x6 speed, income x40). Lessons {g?.Lessons}, era {g?.Era}, helpers {g?.Helpers}, rank {g?.RankName}, built {(g != null ? string.Join(",", g.BuiltStations()) : "")}\n\n" + SessionState.GetString(Log, ""), errors > 0 ? 1 : 0);
        }

        static void Finish(string text, int code)
        {
            EditorApplication.update -= Step;
            SessionState.SetBool(On, false);
            File.WriteAllText(SessionState.GetString(Out, "smoke.txt"), text);
            GameState.DeleteSave();
            EditorApplication.Exit(code);
        }
    }
}
