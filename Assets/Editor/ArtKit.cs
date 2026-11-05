using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChalkPhysics.EditorTools
{
    /// The whole art of the game for an artist, rendered headless by ItchArt (Build/ItchArt): the logo, Мел in every
    /// mood, every icon, every instrument as the theory screen sketches it, the specimens, the chalk helper (each on
    /// black and on white, for the alpha), then screenshots of real play by AutoPlayBot. Never touches the real save.
    /// Unity.exe -batchmode -projectPath <copy> -executeMethod ChalkPhysics.EditorTools.ArtKit.Run [-artSec 300]
    /// Then Tools/ArtKit/pack.py makes the folder.
    [InitializeOnLoad]
    public static class ArtKit
    {
        const string On = "artkit.on", Sec = "artkit.sec";

        static ArtKit()
        {
            if (!SessionState.GetBool(On, false)) return;
            GameState.Sandbox = true;
            EditorApplication.update += Step;
        }

        public static void Run()
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-artSec");
            SessionState.SetFloat(Sec, i >= 0 && i + 1 < args.Length ? float.Parse(args[i + 1], System.Globalization.CultureInfo.InvariantCulture) : 300);
            SessionState.SetBool(On, true);
            GameState.Sandbox = true;
            EditorApplication.update += Step;
        }

        static readonly string[] Icons = { "bulb", "fall", "dyna", "spring", "pend", "arch", "density", "friction", "slide", "fly", "stand", "charge", "lamp", "cap", "coil", "heater", "calor", "engine", "ice", "piston", "psi", "pusher", "hook", "cam", "stack", "scoop", "scales", "nugget", "console", "hand", "bank", "list", "bolt", "spark", "gear", "lens", "star", "box", "eye", "clock", "rev", "act", "table", "chalkie", "guest" };

        static Queue<(System.Func<string> act, int frames)> _q;   // frames to wait after; negative: seconds
        static int _waitUntil;
        static double _playStart = -1, _lastShot, _waitTime;
        static int _shot;

        static void Solo(string what, string name, float scale, float k = 4f, int wait = 20)
        {
            _q.Enqueue((() => ItchArt.Solo(what, scale, 260f, k), 0));
            _q.Enqueue((() => ItchArt.Background(false), wait));
            _q.Enqueue((() => ItchArt.Snap(name + "_k"), 0));
            _q.Enqueue((() => ItchArt.Background(true), 3));
            _q.Enqueue((() => ItchArt.Snap(name + "_w"), 0));
        }

        static void Plan()
        {
            _q = new Queue<(System.Func<string>, int)>();
            _q.Enqueue((() => ItchArt.Begin(3840, 2160), 0));
            _q.Enqueue((() => "the studio's mark and the menu come up", -7));
            _q.Enqueue((() => ItchArt.Snap("shot_000_menu"), 0));
            _q.Enqueue((() => ItchArt.Board(), 0));
            Solo("logo", "logo", 2f, wait: -4);
            foreach (var m in System.Enum.GetNames(typeof(Buddy.Mood))) Solo("buddy:" + m, "mel_" + m.ToLower(), 5f, wait: -1);
            foreach (var ic in Icons) Solo("icon:" + ic, "icon_" + ic, 10f, 2f);
            foreach (var f in Defs.Formulas) Solo("sketch:" + f.id, "apparatus_" + f.id, 3f);
            Solo("samples", "samples", 3f);
            Solo("chalkie", "chalkie_white", 6f, 6f);
            Solo("chalkie:yellow", "chalkie_yellow", 6f, 6f);
            _q.Enqueue((() => ItchArt.Unsolo(), 2));
            _q.Enqueue((() => { ItchArt.Background(false); return Play(); }, 30));
        }

        /// From the menu into the game, the bot at the wheel, a little fast-forwarded.
        static string Play()
        {
            var gr = GameRoot.I;
            typeof(GameRoot).GetMethod("PlayFromMenu", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(gr, null);
            gr.devSpeed = 150; GameState.DevSpeed = 150;
            gr.gameObject.AddComponent<AutoPlayBot>();
            _playStart = EditorApplication.timeSinceStartup;
            return "bot playing";
        }

        static string Screen()
        {
            var g = GameState.I;
            if (g == null) return "none";
            if (g.Phase == Phase.Lesson) return "bench";
            var alch = Object.FindAnyObjectByType<Alchemy>();
            return alch != null && alch.gameObject.activeInHierarchy ? "theory" : "map";
        }

        static void Step()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (!EditorApplication.isPlaying)
            {
                if (_q != null || EditorApplication.timeSinceStartup < 15) return;
                EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
                var root = Object.FindAnyObjectByType<GameRoot>();
                if (root == null) { Finish(1); return; }
                root.autoPlayBot = false;
                root.timeScale = 1f;
                root.devSpeed = 1f;
                AudioListener.volume = 0; Sfx.Muted = true;
                EditorApplication.isPlaying = true;
                return;
            }
            AudioListener.volume = 0; AudioListener.pause = true; Sfx.Muted = true;   // silent
            if (_q == null) { Plan(); _waitUntil = Time.frameCount + 5; }
            if (Time.frameCount < _waitUntil || EditorApplication.timeSinceStartup < _waitTime) return;
            if (_q.Count > 0)
            {
                var (act, frames) = _q.Dequeue();
                Debug.Log("[ART] " + act());
                _waitUntil = Time.frameCount + Mathf.Max(frames, 0);
                if (frames < 0) _waitTime = EditorApplication.timeSinceStartup - frames;
                return;
            }
            // play: a screenshot every few seconds, named by the screen it shows
            Time.timeScale = 2.5f;   // income ×150 and a faster clock: every era within minutes
            double now = EditorApplication.timeSinceStartup;
            if (now - _lastShot > 2.5)
            {
                _lastShot = now;
                var g = GameState.I;
                ItchArt.Snap($"shot_{++_shot:000}_{Screen()}_era{g?.Era}");
                if (_shot % 8 == 3) GameRoot.I.ShowAlchemy();   // the bot proves theories without the screen: look at it now and then
                if (_shot % 8 == 5) GameRoot.I.ShowMap();
            }
            if (now - _playStart > SessionState.GetFloat(Sec, 300)) Finish(0);
        }

        static void Finish(int code)
        {
            EditorApplication.update -= Step;
            SessionState.SetBool(On, false);
            ItchArt.End();
            EditorApplication.Exit(code);
        }
    }
}
