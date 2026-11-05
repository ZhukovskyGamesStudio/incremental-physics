using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ChalkPhysics.Lab3D;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChalkPhysics.EditorTools
{
    /// The gallery: every model and game object of the 3D lab as it looks in the game — the real camera, the chalk
    /// renderer — in pictures in Build/Gallery, with a contact sheet Build/Gallery/index.html. Plays the Lab3D scene
    /// headless with a new game in memory that has everything (every theory, upgrade and odd department, the room at
    /// its full size, at noon), and shoots: every customer front, three-quarter and over the sill in a studio away from
    /// the room, a customer at the window from the player's place, every department where it stands and its apparatus
    /// close up, the desk's things, the sample shapes in a row, the room from a few places.
    /// Unity.exe -batchmode -projectPath <project> -executeMethod ChalkPhysics.EditorTools.Lab3DGallery.Run [-galleryOnly customer_sailor]
    /// (Tools/Lab3D/gallery.ps1). Survives the script reload on entering play mode (state in SessionState).
    [InitializeOnLoad]
    public static class Lab3DGallery
    {
        const string On = "gallery.on", Only = "gallery.only", Stage = "gallery.stage", Log = "gallery.log";
        const string Dir = "Build/Gallery";
        static readonly Vector3 Studio = new Vector3(40, 0, 0);

        static Lab3DGallery()
        {
            if (!SessionState.GetBool(On, false)) return;
            Application.logMessageReceived += OnLog;
            EditorApplication.update += Step;
        }

        public static void Run()
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-galleryOnly");
            string only = i >= 0 && i + 1 < args.Length ? args[i + 1] : "";
            SessionState.SetString(Only, only);
            SessionState.SetString(Log, "");
            SessionState.SetInt(Stage, 0);
            SessionState.SetBool(On, true);
            Directory.CreateDirectory(Dir);
            if (only == "") foreach (var f in Directory.GetFiles(Dir, "*.png")) File.Delete(f);   // a full run: none left from before
            Application.logMessageReceived += OnLog;
            EditorApplication.update += Step;
        }

        static void Say(string s) => SessionState.SetString(Log, SessionState.GetString(Log, "") + s + "\n");

        static void OnLog(string msg, string stack, LogType type)
        {
            if (type is LogType.Error or LogType.Exception or LogType.Assert) Say($"{type}: {msg}\n{stack}");
        }

        // ------------------------------------------------------------------ the run

        static Lab3DGame _game;
        static readonly List<(string name, System.Action act, float wait)> _jobs = new List<(string, System.Action, float)>();
        static int _next;
        static double _at;
        static bool _noon;
        static List<Customer3D> _customers;
        static int _shots;

        static void Step()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            int stage = SessionState.GetInt(Stage, 0);
            if (!EditorApplication.isPlaying)
            {
                if (stage == 0 && EditorApplication.timeSinceStartup > 5)
                {   // an empty scene first: the lab is loaded only past the script reload that comes with play mode
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    SessionState.SetInt(Stage, 1);
                    EditorApplication.isPlaying = true;
                }
                return;
            }
            AudioListener.volume = 0;
            if (stage == 1)
            {
                if (Time.timeSinceLevelLoad < 0.5f) return;
                Everything();
                SessionState.SetInt(Stage, 2);
                SceneManager.LoadScene("Lab3D");
                return;
            }
            if (_game == null)
            {
                var game = Object.FindAnyObjectByType<Lab3DGame>();
                if (game == null || Time.timeSinceLevelLoad < 1f || !game.Hands.Ready) return;
                _game = game;
                Setup();
                return;
            }
            if (!_noon)
            {   // the day run on fast to noon: the sun high, the room light
                if (_game.day.Hour < 12.5f) return;
                _game.day.autorun = false;
                Time.timeScale = 1;
                _noon = true;
                Jobs();
                _at = EditorApplication.timeSinceStartup;
                return;
            }
            if (EditorApplication.timeSinceStartup < _at) return;
            if (_next >= _jobs.Count) { Finish(); return; }
            var (name, act, wait) = _jobs[_next++];
            try { act(); }
            catch (System.Exception e) { Say($"{name}: {e}"); }
            _at = EditorApplication.timeSinceStartup + wait;
        }

        /// A new game in memory with everything the 3D lab has: the theories, the letters, every upgrade and odd
        /// department, the room grown to its full size already (no walls to slide away in the morning).
        static void Everything()
        {
            GameState.MemorySave = true;
            GameState.Use3D();
            var g = new GameState();
            g.NewGame();
            foreach (var l in new[] { "m", "g", "S", "h", "a3" }) if (!g.Known.Contains(l)) g.Known.Add(l);
            foreach (var f in new[] { "dyna", "bath", "dens", "incline" }) g.Proven.Add(f);
            foreach (var p in new[] { "scale1", "scale2", "scale3", "true1", "true2", "true3", "ord1", "ord2", "ord3", "vscale1", "vscale2",
                                      "vtrue1", "vtrue2", "astep1", "atrue1", "atrue2" }) g.Perks.Add(p);
            foreach (var t in Oddity.All) g.Perks.Add(Oddity.Perk(t));
            for (int s = 1; s <= LabRoom.LastStage; s++) g.Used.Add("3d:room" + s);
            g.Save();
        }

        /// The lab is quiet for the pictures: no customers come, the player's body is moved by hand, no screen over it.
        static void Setup()
        {
            var g = _game;
            g.Line.StopAllCoroutines();
            _customers = new List<Customer3D>(g.customers);
            foreach (var c in _customers) { c.StopAllCoroutines(); c.gameObject.SetActive(false); }
            g.customers.Clear();   // the line calls nobody: there is nobody free to come
            g.walker.enabled = false;
            g.walker.GetComponent<CharacterController>().enabled = false;
            var hud = g.GetComponentInChildren<Canvas>();
            if (hud != null) hud.enabled = false;
            g.day.autorun = true;
            Time.timeScale = 20;
        }

        static bool Wanted(string name)
        {
            var only = SessionState.GetString(Only, "");
            return only == "" || name.Contains(only);
        }

        static void Job(string name, System.Action act, float wait = 0)
        {
            if (Wanted(name)) _jobs.Add((name, act, wait));
        }

        static void Shot(string name, int w = 1600, int h = 900) => Job(name, () => Shoot(name, w, h));

        static void Jobs()
        {
            var g = _game;
            g.eye.aspect = 16 / 9f;
            // ---------------- the customers, in a studio away from the room
            foreach (var who in Persona.All)
            {
                string n = "customer_" + who;
                var shots = new (string tag, Quaternion turn, float pitch, float from, int w, int h)[]
                {
                    ("_front", Quaternion.identity, 0, 0, 900, 1200), ("_threequarter", Quaternion.Euler(0, 38, 0), 8, 0, 900, 1200),
                    ("_bust", Quaternion.Euler(0, -25, 0), 4, 1.15f, 1200, 900),
                };
                if (!shots.Any(x => Wanted(n + x.tag))) continue;
                _jobs.Add((n, () => Pose(who), 0));
                foreach (var x in shots) Job(n + x.tag, () => { Aspect(x.w, x.h); Frame(_posed, x.turn, x.pitch, x.from); Shoot(n + x.tag, x.w, x.h); });
            }
            _jobs.Add(("", () => { if (_posed != null) Object.DestroyImmediate(_posed.gameObject); }, 0));
            // and at the window, as the player sees them from the counter
            foreach (var who in new[] { "sailor", "cook", "gnome" })
            {
                string n = "customer_at_window_" + who;
                Job(n, () => AtWindow(who));
                Job(n, () => { Eye(new Vector3(1.95f, 1.62f, 2.2f), new Vector3(1.95f, 1.62f, 4.0f)); Shoot(n); });
            }
            _jobs.Add(("", () => { foreach (var c in _customers) c.gameObject.SetActive(false); }, 0));

            // ---------------- the departments where they stand, and their apparatus close up
            foreach (var d in g.departments)
            {
                if (d.table == null || !d.table.gameObject.activeInHierarchy) continue;
                var dept = d;
                string n = "dept_" + d.id;
                Job(n, () =>
                {   // from where the player stands in front of it
                    var b = BoundsOf(dept.table.gameObject);
                    var front = dept.table.forward;
                    float dist = Mathf.Max(1.25f, Fit(b, 1.05f) * 0.9f);
                    var at = new Vector3(b.center.x, 0, b.center.z) + front * (dist * 0.85f + b.extents.z * 0.5f);
                    Eye(at + Vector3.up * 1.6f, b.center);
                    Shoot(n);
                });
                if (d.apparatus.Count > 0 && d.apparatus[0] != null && d.apparatus[0].gameObject.activeInHierarchy && d.apparatus[0].GetComponentsInChildren<Renderer>().Length > 0)
                {
                    var a = d.apparatus[0];
                    Job(n + "_close", () =>
                    {
                        var b = BoundsOf(a.gameObject);
                        Close(b, dept.table.forward, 22);
                        Shoot(n + "_close");
                    });
                }
            }
            if (g.lightSwitch != null)
                Job("dept_glow", () => { Close(BoundsOf(g.lightSwitch.gameObject), g.lightSwitch.forward, 5, 3.2f); Shoot("dept_glow"); });

            // ---------------- the desk's things
            Job("desk_counter", () => { Eye(new Vector3(2.0f, 1.6f, 1.75f), g.counter.position + Vector3.up * 1.15f); Shoot("desk_counter"); });
            if (g.stamp != null) Job("desk_stamp", () => { var b = BoundsOf(g.stamp.gameObject); if (g.stampPad != null) b.Encapsulate(BoundsOf(g.stampPad.gameObject)); Close(b, Vector3.back, 35, 1.8f); Shoot("desk_stamp"); });
            if (g.dispatchBox != null) Job("desk_box", () => { Close(BoundsOf(g.dispatchBox.gameObject), Vector3.back, 35, 1.6f); Shoot("desk_box"); });
            if (g.calculator != null) Job("desk_calculator", () => { Close(BoundsOf(g.calculator), Vector3.back, 50, 1.4f); Shoot("desk_calculator"); });
            Job("desk_certificate", () =>
            {
                var c = g.awards != null ? g.awards.All.FirstOrDefault(x => x.gameObject.activeInHierarchy) : null;
                if (c == null) { Say("desk_certificate: no certificate on the walls"); return; }
                Close(BoundsOf(c.gameObject), c.transform.forward, 0, 1.3f);
                Shoot("desk_certificate");
            });
            Job("desk_sheet_in_hand", () =>
            {   // an order dealt onto the counter, picked up into the left hand
                var sh = g.sheets.Find(s => !s.gameObject.activeSelf && !g.Line.Incoming(s));
                if (sh == null) { Say("desk_sheet_in_hand: no free sheet"); return; }
                g.Book.Deal(sh, 1);
                Eye(new Vector3(2.1f, 1.6f, 2.15f), new Vector3(2.1f, 1.0f, 3.0f));
                g.Hands.Down(sh);
            }, 1.8f);
            Job("desk_sheet_in_hand", () => { Shoot("desk_sheet_in_hand"); if (g.Hands.Reading != null) g.Hands.CloseSheet(); }, 1.0f);
            Job("desk_sheet_in_hand", () => { Eye(new Vector3(2.1f, 1.6f, 2.15f), new Vector3(2.1f, 1.2f, 2.95f)); Shoot("desk_sheet_on_counter"); });

            // ---------------- the sample shapes, in a row
            Job("samples_shapes", () =>
            {
                var root = new GameObject("Shapes").transform;
                root.position = Studio + new Vector3(0, 0, 20);
                root.rotation = Quaternion.LookRotation(-Sunward);
                var all = SampleShapes.All;
                for (int i = 0; i < all.Length; i++)
                {
                    var (mesh, vol) = SampleShapes.Get(all[i], 0);
                    var go = new GameObject(all[i], typeof(MeshFilter), typeof(MeshRenderer));
                    go.transform.SetParent(root, false);
                    go.transform.localPosition = new Vector3((i % 7 - 3) * 0.24f, i < 7 ? 0.32f : 0, 0);
                    go.transform.localScale = Vector3.one * Mathf.Pow(0.0012f / Mathf.Max(vol, 1e-6f), 1 / 3f);
                    go.GetComponent<MeshFilter>().sharedMesh = mesh;
                    var r = go.GetComponent<MeshRenderer>();
                    r.sharedMaterial = g.sampleMats[1];
                    Art3D.Prop(r, "_Local", 1);
                }
                var b = BoundsOf(root.gameObject);
                Close(b, -Sunward, 10, 1.1f);
                Shoot("samples_shapes");
                Object.DestroyImmediate(root.gameObject);
            });

            // ---------------- the room
            Job("room_from_counter", () => { Eye(new Vector3(2.6f, 1.7f, 3.0f), new Vector3(-0.5f, 1.0f, -1.5f)); Shoot("room_from_counter"); });
            Job("room_from_far_corner", () => { Eye(new Vector3(2.6f, 1.7f, -3.2f), new Vector3(-0.5f, 1.0f, 1.5f)); Shoot("room_from_far_corner"); });
            Job("room_from_left", () => { Eye(new Vector3(-2.6f, 1.7f, -0.6f), new Vector3(1.5f, 1.0f, 0.3f)); Shoot("room_from_left"); });
            Job("room_door", () =>
            {
                var db = g.door.GetComponent<Renderer>().bounds;
                var into = db.size.x < db.size.z ? new Vector3(-Mathf.Sign(db.center.x), 0, 0) : new Vector3(0, 0, -Mathf.Sign(db.center.z));
                var at = db.center + into * 1.6f;
                Eye(new Vector3(at.x, 1.6f, at.z), db.center);
                Shoot("room_door");
            });
            Job("room_high", () => { Eye(new Vector3(2.7f, 2.8f, -3.3f), new Vector3(-0.6f, 0.6f, 1.2f)); Shoot("room_high"); });
        }

        // ------------------------------------------------------------------ the customers

        static Transform _posed;

        /// The customer alone in the studio, facing +z.
        static void Pose(string who)
        {
            if (_posed != null) Object.DestroyImmediate(_posed.gameObject);
            _posed = new GameObject("Posed " + who).transform;
            _posed.position = Studio;
            _posed.rotation = Quaternion.LookRotation(-Sunward);   // facing the light: lit from the camera's side
            Persona.Build(who, _posed, _game.kit);
        }

        /// The eye looks at the posed customer from the side `turn` faces it from, a little from above, the whole of it
        /// in the picture (or only what is over `from` metres: what shows over the counter's sill).
        static void Frame(Transform t, Quaternion turn, float pitch, float from = 0)
        {
            var b = BoundsOf(t.gameObject);
            if (from > 0)
            {
                var min = b.min; min.y = Mathf.Max(min.y, t.position.y + from);
                b.SetMinMax(min, b.max);
            }
            var dir = Quaternion.LookRotation(Sunward) * turn * Quaternion.Euler(pitch, 0, 0) * Vector3.forward;   // from the eye to it
            float d = Fit(b, 1.12f);
            Eye(b.center - dir * d, b.center);
        }

        /// A customer at the window, as in the day: on the corridor's floor before the sill, facing into the lab.
        static void AtWindow(string who)
        {
            var c = _customers[0];
            for (int i = c.look.childCount - 1; i >= 0; i--) Object.DestroyImmediate(c.look.GetChild(i).gameObject);
            c.look.localPosition = Vector3.zero;
            c.look.localRotation = Quaternion.identity;
            c.transform.SetPositionAndRotation(CustomerLine.Window, Quaternion.LookRotation(Vector3.back));
            c.gameObject.SetActive(true);
            Persona.Build(who, c.look, _game.kit);
        }

        // ------------------------------------------------------------------ the camera

        /// The sun's way across the floor: the studio's things face against it, the eye looks along it.
        static Vector3 Sunward
        {
            get
            {
                var f = _game.day.sun != null ? _game.day.sun.transform.forward : Vector3.forward;
                f.y = 0;
                return f.sqrMagnitude < 1e-4f ? Vector3.forward : f.normalized;
            }
        }

        /// The picture's shape, for the framing before the shot (Shoot sets it too).
        static void Aspect(int w, int h) => _game.eye.aspect = w / (float)h;

        static Bounds BoundsOf(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy && !(r is LineRenderer)).ToArray();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.3f);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        /// How far the eye stands for the bounds to fill the picture (`margin` over the tight fit).
        static float Fit(Bounds b, float margin)
        {
            var cam = _game.eye;
            float vh = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float hh = Mathf.Atan(Mathf.Tan(vh) * cam.aspect);
            float w = Mathf.Max(b.size.x, b.size.z);
            return margin * Mathf.Max(b.extents.y / Mathf.Tan(vh), w * 0.5f / Mathf.Tan(hh)) + Mathf.Min(b.size.x, b.size.z) * 0.5f;
        }

        /// Close up: the eye before the thing's `front`, `up` degrees over it.
        static void Close(Bounds b, Vector3 front, float up, float margin = 1.25f)
        {
            front.y = 0;
            front.Normalize();
            var dir = Quaternion.AngleAxis(up, Vector3.Cross(front, Vector3.up)) * front;   // from it to the eye
            float d = Mathf.Max(Fit(b, margin), 0.25f);
            Eye(b.center + dir * d, b.center);
        }

        static void Eye(Vector3 at, Vector3 lookAt)
        {
            var eye = _game.eye.transform;
            eye.SetPositionAndRotation(at, Quaternion.LookRotation(lookAt - at, Vector3.up));
            Chalk3DText.FaceAll();
            foreach (var mr in Object.FindObjectsByType<Mirror3D>()) mr.Refresh();
        }

        static void Shoot(string name, int w = 1600, int h = 900)
        {
            var cam = _game.eye;
            var rt = new RenderTexture(w, h, 24);
            cam.targetTexture = rt;
            cam.aspect = w / (float)h;
            cam.Render();
            cam.targetTexture = null;
            cam.aspect = 16 / 9f;   // the next one is framed for the usual picture unless it says otherwise
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            RenderTexture.active = null;
            Directory.CreateDirectory(Dir);
            File.WriteAllBytes($"{Dir}/{name}.png", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
            _shots++;
            Say("снимок " + name);
        }

        // ------------------------------------------------------------------ the contact sheet

        static readonly (string prefix, string title)[] Groups =
        {
            ("customer_at_window", "Заказчики у окна"), ("customer_", "Заказчики"), ("dept_", "Отделы и приборы"),
            ("desk_", "Стойка и мелочи"), ("samples_", "Образцы"), ("room_", "Комната"),
        };

        static void Index()
        {
            var files = Directory.GetFiles(Dir, "*.png").Select(Path.GetFileName).OrderBy(f => f, System.StringComparer.Ordinal).ToList();
            var sb = new StringBuilder();
            sb.Append("<!doctype html><html lang=\"ru\"><head><meta charset=\"utf-8\"><title>Галерея лаборатории</title><style>")
              .Append("body{background:#1b211f;color:#e8e6dc;font:14px sans-serif;margin:16px}h2{margin:28px 0 8px;font-weight:normal}")
              .Append(".g{display:flex;flex-wrap:wrap;gap:10px}figure{margin:0;width:300px}img{width:300px;border:1px solid #3a4440;background:#000}")
              .Append("figcaption{font-size:12px;color:#aab;margin-top:2px;word-break:break-all}a{color:inherit}</style></head><body>")
              .Append($"<h1>Галерея лаборатории</h1><p>{files.Count} снимков, {System.DateTime.Now:yyyy-MM-dd HH:mm}</p>");
            var done = new HashSet<string>();
            foreach (var (prefix, title) in Groups.Append(("", "Остальное")))
            {
                var mine = files.Where(f => f.StartsWith(prefix) && !done.Contains(f)).ToList();
                if (mine.Count == 0) continue;
                sb.Append($"<h2>{title}</h2><div class=\"g\">");
                foreach (var f in mine)
                {
                    done.Add(f);
                    var cap = Path.GetFileNameWithoutExtension(f);
                    sb.Append($"<figure><a href=\"{f}\"><img src=\"{f}\" loading=\"lazy\"></a><figcaption>{cap}</figcaption></figure>");
                }
                sb.Append("</div>");
            }
            sb.Append("</body></html>");
            File.WriteAllText($"{Dir}/index.html", sb.ToString());
        }

        static void Finish()
        {
            Index();
            Say($"снимков: {_shots}, в {Path.GetFullPath(Dir)}");
            File.WriteAllText(Path.GetFullPath("Temp/gallery.txt"), SessionState.GetString(Log, ""));
            SessionState.SetBool(On, false);
            EditorApplication.update -= Step;
            EditorApplication.Exit(0);
        }
    }
}
