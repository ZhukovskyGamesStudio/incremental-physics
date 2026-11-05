using System.IO;
using System.Linq;
using ChalkPhysics.Lab3D;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ChalkPhysics.EditorTools
{
    /// Headless check of the game with the 3D day, in play mode, fast-forwarded, as the player goes: from a new game on
    /// the map (scene Game) the theories are solved and the upgrades bought (AutoPlayBot.Shop), the map's button starts
    /// the day (scene Lab3D), Lab3DBot works it through the same hands as the mouse and goes out through the door in
    /// the evening, and the map comes again. The save lives in memory (GameState.MemorySave): the player's own is never
    /// touched. Writes every day's sum, what was bought between the days, what every apparatus earned and every error to
    /// Temp/lab3d_check.txt, and takes pictures (the day with the HUD, the evening, the map) into Build/Lab3D.
    /// Survives the script reload on entering play mode (state in SessionState).
    /// Unity.exe -batchmode -projectPath <project> -executeMethod ChalkPhysics.EditorTools.Lab3DCheck.Run [-lab3dMin 6]
    [InitializeOnLoad]
    public static class Lab3DCheck
    {
        const string On = "lab3d.on", Log = "lab3d.log", Stage = "lab3d.stage", Min = "lab3d.min", Shot = "lab3d.shot",
            Shopped = "lab3d.shopped", Evened = "lab3d.evened", MapAt = "lab3d.mapAt";
        const float Speed = 6;
        static string OutPath => Path.GetFullPath("Temp/lab3d_check.txt");

        static Lab3DCheck()
        {
            if (!SessionState.GetBool(On, false)) return;
            Application.logMessageReceived += OnLog;
            EditorApplication.update += Step;
        }

        public static void Run()
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-lab3dMin");
            SessionState.SetFloat(Min, i >= 0 ? float.Parse(args[i + 1], System.Globalization.CultureInfo.InvariantCulture) : 6);
            // -lab3dGrow: departments are given on the map after days 1 and 2, to see the room grow
            SessionState.SetBool("lab3d.grow", System.Array.IndexOf(args, "-lab3dGrow") >= 0);
            SessionState.SetBool(On, true);
            SessionState.SetString(Log, "");
            Career.Fast = System.Array.IndexOf(args, "-lab3dGrow") >= 0;   // the goals between the days, sooner (Step keeps it)
            SessionState.SetInt(Stage, 0);
            SessionState.SetInt(Shot, 0);
            SessionState.SetInt(Shopped, -1);
            SessionState.SetInt(Evened, -1);
            SessionState.SetBool("lab3d.crewShot", false);
            SessionState.SetBool("lab3d.ordersShot", false);
            SessionState.SetInt("lab3d.dynaShot", 0);
            SessionState.SetBool("lab3d.sheetShot", false);
            SessionState.SetBool("lab3d.oddSheetShot", false);
            SessionState.SetBool("lab3d.lineup", false);
            SessionState.SetBool("lab3d.window", false);
            SessionState.SetBool("lab3d.bathShot", false);
            SessionState.SetBool("lab3d.slopeShot", false);
            SessionState.SetBool("lab3d.waitShot", false);
            SessionState.SetInt("lab3d.shapesShot", 0);
            SessionState.SetBool("lab3d.calcShot", false);
            SessionState.SetInt("lab3d.roomShot", 0);
            foreach (var m in new[] { "stamped", "boxed", "packing", "packed", "sending", "paid" }) SessionState.SetBool("lab3d.m." + m, false);
            foreach (var m in OddMoments) SessionState.SetBool("lab3d.m." + m, false);
            SessionState.SetFloat("lab3d.coins", 0);
            Application.logMessageReceived += OnLog;
            EditorApplication.update += Step;
        }

        static void Say(string s) => SessionState.SetString(Log, SessionState.GetString(Log, "") + s + "\n");
        static string Now => $"{Time.time / 60:0.0} мин";

        static void OnLog(string msg, string stack, LogType type)
        {
            if (type is LogType.Error or LogType.Exception or LogType.Assert) Say($"{type}: {msg}\n{stack}");
        }

        static void Step()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            Career.Fast = SessionState.GetBool("lab3d.grow", false);   // statics do not live through the reload into play mode
            int stage = SessionState.GetInt(Stage, 0);
            if (!EditorApplication.isPlaying)
            {
                if (stage == 0 && EditorApplication.timeSinceStartup > 10)
                {
                    // an empty scene first: the map is loaded only past the script reload that comes with play mode
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    SessionState.SetInt(Stage, 1);
                    EditorApplication.isPlaying = true;
                }
                return;
            }
            AudioListener.volume = 0;
            if (stage == 1)
            {   // past the reload that may come right after entering play mode: a new game, with the save in memory
                if (Time.timeSinceLevelLoad < 1) return;
                GameState.MemorySave = true;
                GameRoot.Booted = true;   // no splash and no menu
                SessionState.SetInt(Stage, 2);
                SceneManager.LoadScene(GameRoot.MapScene);
                return;
            }
            GameState.MemorySave = true;
            Time.timeScale = Speed;

            var game = Object.FindAnyObjectByType<Lab3DGame>();
            if (game != null) { Day(game); return; }
            if (GameRoot.I != null && GameState.I != null && Time.timeSinceLevelLoad > 1.5f) Map(GameState.I);
        }

        /// Between the days, on the map: every theory whose letters are open is solved, the upgrades are bought, and the
        /// map's own button starts the next day.
        static void Map(GameState g)
        {
            if (g.Phase == Phase.Lesson || SessionState.GetInt(Shopped, -1) == g.Lessons) return;
            SessionState.SetInt(Shopped, g.Lessons);
            if (g.Lessons > 0)
            {
                var acc = Lab3DBot.accuracy;
                float coins = (float)g.Total[(int)Cur.Coin], was = SessionState.GetFloat("lab3d.coins", 0);
                SessionState.SetFloat("lab3d.coins", coins);
                int lost = OrderBook.LostAll - SessionState.GetInt("lab3d.lost", 0), tips = OrderBook.TipsAll - SessionState.GetInt("lab3d.tips", 0);
                SessionState.SetInt("lab3d.lost", OrderBook.LostAll);
                SessionState.SetInt("lab3d.tips", OrderBook.TipsAll);
                Say($"{Now}  день {g.Lessons}: +{GameState.Fmt(coins - was)} монет (чаевые {tips}), ответов {acc.Count} из {g.OrdersPerDay}, ушли не дождавшись {lost}, средняя точность " +
                    $"{(acc.Count > 0 ? acc.Average() * 100 : 0):0}%, шкала {g.ScaleStep} Н, пружина ±{g.SpringError * 100:0.#}%");
                acc.Clear();
            }
            int map = SessionState.GetInt(MapAt, 0);
            if (map == 1 || map == 3) MapPicture(map);
            SessionState.SetInt(MapAt, map + 1);
            if (Time.time >= SessionState.GetFloat(Min, 6) * 60) { Summary(g); return; }
            if (g.CanRevolt) { Say($"{Now}  ЭПОХА ПРОЙДЕНА: революция по карману после дня {g.Lessons}"); Summary(g); return; }

            var known = g.Known.ToDictionary(x => x, g.Level);
            for (int pass = 0; pass < 2; pass++)   // the theories, the shop, and the theories its letters have made possible
            {
                foreach (var f in g.Discoverable().ToList())
                {
                    if (f.id is "bath" or "dens" or "incline") TheoryPicture(f.id);   // its sketch, before it is solved
                    foreach (var slot in f.Slots()) g.Place(f.id, slot, slot);
                    if (g.Built(f.id)) Say($"{Now}  карта: теория {f.title}");
                }
                if (pass == 0) AutoPlayBot.Shop(g, s => Say($"{Now}  карта: {s}"));
            }
            foreach (var id in g.Known)
            {
                int was = known.TryGetValue(id, out var l) ? l : 0;
                if (g.Level(id) != was) Say($"{Now}  карта: {Defs.L(id).sym} {(was == 0 ? "открыта" : "")} уровень {g.Level(id)} = {g.LetterValue(id)}");
            }
            foreach (var (text, fresh) in Career.MapLines(g)) Say($"{Now}  цели: {(fresh ? "! " : "")}{text}");
            Say($"{Now}  карта: осталось {GameState.Fmt(g.Cur[(int)Cur.Obs])} идей, {GameState.Fmt(g.Cur[(int)Cur.J])} Дж → день {g.Lessons + 1} ({g.DaySeconds:0} с)");
            if (!g.AnyStation) { Say("нечего ставить на стол: день не начать"); Finish(1); return; }
            if (SessionState.GetBool("lab3d.grow", false) && g.Lessons is >= 1 and <= 4)
            {
                if (g.Lessons <= 2) foreach (var f in g.Lessons == 1 ? new[] { "bath" } : new[] { "dens", "incline" }) g.Proven.Add(f);   // the inclined plane on day 3: its orders forced, its jack to be seen
                // the odd departments (DESIGN.md 2б, «Крейзи-измерения»): four after day 3, the other three after day 4
                if (g.Lessons == 3) foreach (var t in new[] { "taste", "glow", "fish", "mirror" }) g.Perks.Add(Oddity.Perk(t));
                if (g.Lessons == 4) foreach (var t in new[] { "cat", "note", "tickle" }) g.Perks.Add(Oddity.Perk(t));
                OrderSheet.forceSlope = g.Lessons == 2;   // the inclined plane's orders, to be seen working
                OrderSheet.forceQueue.Clear();
                if (g.Lessons >= 3)   // every odd department's orders, to be seen working
                    foreach (var t in g.Lessons == 3 ? new[] { "taste", "glow", "fish", "mirror", "taste" } : new[] { "cat", "note", "tickle", "fish", "mirror" }) OrderSheet.forceQueue.Add(t);
                if (g.Lessons == 1) { g.Perks.Add("scale1"); g.Perks.Add("scale2"); }   // the loupe, to be seen
                Say($"{Now}  проверка роста: даны отделы {string.Join(", ", g.Proven)}; {string.Join(", ", g.Perks.Where(p => p.StartsWith("x_")))}");
            }
            GameRoot.I.StartLesson(false);
        }

        static float _sheetAt = -1;
        static OrderSheet _shotSheet;
        static bool _slideShot;

        static void Day(Lab3DGame game)
        {
            if (game.GetComponent<Lab3DBot>() == null)
            {
                if (Time.timeSinceLevelLoad < 0.5f || game.G == null) return;
                game.gameObject.SetActive(false);   // so the bot's OnEnable sees its game
                game.gameObject.AddComponent<Lab3DBot>().game = game;
                game.gameObject.SetActive(true);
                var g0 = game.G;
                Say($"{Now}  день {game.day.Day}: приборы {string.Join(", ", game.apparatus.Where(a => a.Active).Select(a => a.id + (a.copy > 0 ? (a.copy + 1).ToString() : "")))}, " +
                    $"мелков {g0.Crew}, мест на стойке {g0.CounterSlots}, заказ раз в {g0.OrderEvery:0.#} с");
                return;
            }
            game.day.autorun = true;
            if (game.day.Day == 1 && game.day.T > 3f && !SessionState.GetBool("lab3d.aimTest", false))
            {   // the player close up to the counter and further back: what the dot finds over each sheet
                SessionState.SetBool("lab3d.aimTest", true);
                var cc = game.walker.GetComponent<CharacterController>();
                cc.enabled = false;
                foreach (var z in new[] { 1.5f, 2.0f, 2.3f, 2.45f })
                    foreach (var sh in game.sheets)
                    {
                        if (!sh.gameObject.activeSelf) continue;
                        game.walker.transform.position = new Vector3(sh.transform.position.x, 0.02f, z);
                        var to = sh.transform.position + Vector3.up * 0.01f - game.eye.transform.position;
                        game.walker.transform.rotation = Quaternion.Euler(0, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, 0);
                        game.eye.transform.localRotation = Quaternion.Euler(Mathf.Atan2(-to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg, 0, 0);
                        var ray = game.eye.ViewportPointToRay(new Vector3(0.5f, 0.5f));
                        string hitName = Physics.Raycast(ray, out var hit, 5, ~0, QueryTriggerInteraction.Collide) ? hit.collider.name + " (" + hit.collider.transform.parent?.name + ") на " + hit.distance.ToString("0.00") : "ничего";
                        var aim = game.Pointer.Aim();
                        Say($"  прицел с z={z} на {sh.name}: {(aim != null ? aim.name : "null")}; луч: {hitName}");
                    }
                cc.enabled = true;
                // and as the player walks up: the capsule pushed into the counter, the dot on every sheet and sample
                foreach (var x in new[] { 1.4f, 2.1f, 2.8f })
                {
                    cc.enabled = false;
                    game.walker.transform.position = new Vector3(x, 0.02f, 1.6f);
                    cc.enabled = true;
                    for (int i = 0; i < 30; i++) cc.Move(Vector3.forward * 0.05f + Vector3.down * 0.01f);
                    foreach (var sh in game.sheets)
                    {
                        if (!sh.gameObject.activeSelf) continue;
                        foreach (var (what, at) in new[] { ("листок", sh.transform.position + sh.transform.forward * 0.06f + Vector3.up * 0.005f), ("кубик", sh.sample.transform.position + Vector3.up * sh.sample.Size * 0.5f) })
                        {
                            var to = at - game.eye.transform.position;
                            game.walker.transform.rotation = Quaternion.Euler(0, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, 0);
                            game.eye.transform.localRotation = Quaternion.Euler(Mathf.Atan2(-to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg, 0, 0);
                            var ray = game.eye.ViewportPointToRay(new Vector3(0.5f, 0.5f));
                            var hits = Physics.RaycastAll(ray, 3, ~0, QueryTriggerInteraction.Collide);
                            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                            var aim = game.Pointer.Aim();
                            Say($"  у стойки x={x} (стоит z={game.walker.transform.position.z:0.00}, глаз {game.eye.transform.position.y:0.00}) {what} {sh.name}: {(aim != null ? aim.name : "null")}; лучи: " +
                                string.Join(", ", hits.Take(3).Select(h => h.collider.name + " " + h.distance.ToString("0.00"))));
                        }
                    }
                }
            }
            if (game.day.Day == 1 && game.day.T > 2f && !SessionState.GetBool("lab3d.lineup", false))
            {   // every customer in a row, far from the room, front and three-quarter: what they are made of
                SessionState.SetBool("lab3d.lineup", true);
                Lineup(game);
            }
            if (game.day.Day == 4 && game.day.T > 10f && !SessionState.GetBool("lab3d.window", false))
            {   // a pane broken (as a thrown sample breaks it), seen from inside
                SessionState.SetBool("lab3d.window", true);
                var panes = Object.FindObjectsByType<WindowPane>(FindObjectsSortMode.None);
                var pane = System.Array.Find(panes, p => p.transform.position.z < -3);
                if (pane != null)
                {
                    pane.Break();
                    var at = pane.transform.position + pane.transform.forward * 1.6f;
                    var cc = game.walker.GetComponent<CharacterController>();
                    cc.enabled = false;
                    game.walker.transform.SetPositionAndRotation(new Vector3(at.x, 0.02f, at.z), Quaternion.LookRotation(-pane.transform.forward));
                    game.eye.transform.localRotation = Quaternion.Euler(-2, 0, 0);
                    cc.enabled = true;
                    Shoot(game.eye, "window_broken");
                }
            }
            if (game.awards != null && game.day.T > 6f && !SessionState.GetBool("lab3d.awards" + game.day.Day, false) && (game.day.Day != 2 || (Lab3DBot.idle && game.Hands.Empty)))
            {   // (on day 2 the bot first finishes what it carries: a certificate is taken with empty hands)   // the certificates (DESIGN.md 2б, «Грамоты»): where each hangs; on day 2 one close up, then moved to another wall
                SessionState.SetBool("lab3d.awards" + game.day.Day, true);
                Certs(game);
            }
            if (_gaveUp != null && game.day.T > _gaveUpAt + 4)
            {
                Say($"  через 4 с: листок активен {_gaveUp.gameObject.activeSelf}, образец активен {_gaveUp.sample.gameObject.activeSelf}, в руке {(game.Hands.Held != null ? game.Hands.Held.name : "ничего")}, ушли {game.Book.LostCount}, заказчик «{_gaveUpWho.Doing}»");
                _gaveUp = null;
            }
            if (game.day.Day == 1 && game.day.T > 25f && !SessionState.GetBool("lab3d.giveup", false))
            {   // a customer whose order is being worked on gives up: the order must leave the lab, the bot go on
                var who = game.customers.Find(c => c.gameObject.activeSelf && c.Order != null && c.Waiting && c.Order.gameObject.activeSelf && !c.Order.sent && game.Desk.BoxOrder != c.Order && !game.Desk.Sending
                                                           && c.Order.sample.At is Sample3D.Where.Hand or Sample3D.Where.Apparatus);
                if (who != null)
                {
                    SessionState.SetBool("lab3d.giveup", true);
                    var sh = who.Order;
                    Say($"  терпение кончилось у заказчика №{sh.number} ({sh.who}); листок активен {sh.gameObject.activeSelf}; образец: {sh.sample.At}, в руке: {(game.Hands.Held != null ? game.Hands.Held.name : "ничего")}");
                    who.RunOut();
                    Say($"  {who.name}: {who.Doing}, терпения осталось {who.Hurry:0.00}, активен {who.gameObject.activeSelf}");
                    _gaveUp = sh; _gaveUpWho = who; _gaveUpAt = game.day.T;
                }
            }
            if (game.G.Crew > 0 && game.day.T > 40 && !SessionState.GetBool("lab3d.crewShot", false))
            {
                SessionState.SetBool("lab3d.crewShot", true);
                Picture(game, 4, "helpers");
                foreach (var h in game.helpers) if (h.gameObject.activeSelf) Say($"  {h.name}: {h.Doing}");
            }
            // a sheet the bot is not busy with (it waits between two orders): its sample on it, the stamp on its pad
            // (and on a day with odd departments, a sheet with an odd row, a box ticked on it: one the bot has not got to)
            OddSheet(game);
            const bool oddDay = false;
            string shotKey = "lab3d.sheetShot";
            if (_sheetAt < 0) _shotSheet = game.sheets.Find(s => s.gameObject.activeSelf && !s.sent && !game.Line.Incoming(s) && !s.stamped && s.SampleHome);
            bool wantSheet = game.day.Day == 1 && !SessionState.GetBool(shotKey, false);
            if (_sheetAt < 0 && (_shotSheet == null || !wantSheet)) Lab3DBot.hold = false;
            if (wantSheet && _shotSheet != null)
            {   // a sheet picked up and read, as the player sees it: opened, and taken a picture of a little later
                if (game.day.T > 3f && _sheetAt < 0) Lab3DBot.hold = true;   // the bot waits between two orders meanwhile
                if (game.day.T > 3f && _sheetAt < 0 && Lab3DBot.idle)
                {
                    _sheetAt = Time.realtimeSinceStartup;
                    Place(game, 5);
                    game.Hands.Down(_shotSheet);
                }
                if (_sheetAt >= 0 && !_slideShot && !oddDay && Time.realtimeSinceStartup - _sheetAt > 0.17f) { _slideShot = true; Picture(game, 5, "sheet_slide"); }   // sliding out from under its sample
                if (_sheetAt >= 0 && Time.realtimeSinceStartup - _sheetAt > 1.6f)   // the sheet flies in real time: slides out, then up
                {
                    SessionState.SetBool(shotKey, true);
                    int oddRow = _shotSheet.tests.FindIndex(Oddity.Is);
                    if (oddDay && oddRow >= 0) _shotSheet.view.Tick(oddRow, 0);
                    Picture(game, 5, oddDay ? "sheet_odd" : "sheet");
                    if (oddDay && oddRow >= 0) _shotSheet.answers[oddRow] = "";   // the bot writes its own
                    var leaf = _shotSheet.leaf;
                    Say($"  листок: у глаза {game.eye.transform.InverseTransformPoint(leaf.position)}, масштаб {leaf.lossyScale}, активен {leaf.gameObject.activeInHierarchy}, глаз {game.eye.transform.position}");
                    if (game.Hands.Reading == _shotSheet) game.Hands.CloseSheet();   // only its own: the bot may hold another
                    Lab3DBot.hold = false;
                    Picture(game, 10, "door");
                    Picture(game, 12, "tables");
                    Picture(game, 13, "wall_foot");
                    Picture(game, 14, "ceiling");
                    _sheetAt = -1;
                }
            }
            // (after the sheet's picture, which lets the bot go every frame) the bot waits with empty hands: a certificate to move
            if (game.awards != null && game.day.Day == 2 && game.day.T > 6f && !SessionState.GetBool("lab3d.awards2", false)) Lab3DBot.hold = true;
            if (SessionState.GetBool("lab3d.grow", false) && game.day.T > 6f && SessionState.GetInt("lab3d.roomShot", 0) < game.day.Day)
            {   // the room as it is this day, from the counter's side, looking back at the temporary wall
                SessionState.SetInt("lab3d.roomShot", game.day.Day);
                Picture(game, 7, "room_" + game.day.Day);
                Say($"  стена: {(game.partition.gameObject.activeSelf ? "z = " + (game.partition.position.z + game.partition.localScale.z / 2).ToString("0.0") : "убрана")}");
            }
            // the desk's bureaucracy, once each: the stamp's mark, the box filled, the box going up the pipe
            foreach (var m in new[] { "stamped", "boxed", "packing", "packed", "sending", "paid" })
                if (Lab3DBot.moment == m && !SessionState.GetBool("lab3d.m." + m, false) && _sheetAt < 0)
                {
                    SessionState.SetBool("lab3d.m." + m, true);
                    Picture(game, 5, "desk_" + m);
                }
            // the odd departments at work, once each, from in front of their tables
            foreach (var m in OddMoments)
                if (Lab3DBot.moment == m && _sheetAt < 0)
                {
                    if (!SessionState.GetBool("lab3d.m." + m, false)) { SessionState.SetBool("lab3d.m." + m, true); OddPicture(game, m); }
                    Lab3DBot.moment = "";   // seen: the bot may go on
                }
            if (Lab3DBot.notes.Count > 0)
            {
                foreach (var n in Lab3DBot.notes) Say($"  {n}");
                Lab3DBot.notes.Clear();
            }
            var guest = game.customers.Find(c => c.gameObject.activeSelf);
            if (game.day.Day == 1 && guest != null && guest.Doing == "у окна" && !SessionState.GetBool("lab3d.ordersShot", false))
            {   // the day's first customer at the window, waiting to be served
                SessionState.SetBool("lab3d.ordersShot", true);
                Picture(game, 16, "customer_comes");
                foreach (var c in game.customers) if (c.gameObject.activeSelf) Say($"  {c.name}: {c.Order?.who} — {c.Doing}");
            }
            if (guest != null && guest.Doing == "ждёт" && !SessionState.GetBool("lab3d.waitShot", false))
            {
                SessionState.SetBool("lab3d.waitShot", true);
                Picture(game, 15, "customer_waits");
            }
            if (game.day.T > 44f && SessionState.GetInt("lab3d.shapesShot", 0) < game.day.Day)
            {   // the samples on the counter: their shapes, as their orders tell
                SessionState.SetInt("lab3d.shapesShot", game.day.Day);
                foreach (var sh in game.sheets) if (sh.gameObject.activeSelf) Say($"  заказ №{sh.number}: {sh.shape}, {sh.sample.volume:0.00} л, высота {sh.sample.Size * 100:0} см");
                Picture(game, 5, "shapes_" + game.day.Day);
            }

            var dyn = game.Dept("dyna")?.apparatus[0] as Dynamometer3D;
            var slope = game.Dept("incline")?.apparatus[0] as Incline3D;
            if (slope != null && slope.Active && _sheetAt < 0 && slope.Settled && !SessionState.GetBool("lab3d.slopeShot", false))
            {   // the sample has slid: the board stands at its angle against the protractor
                SessionState.SetBool("lab3d.slopeShot", true);
                Picture(game, 17, "incline");
                Say($"  наклонная плоскость: доска на {slope.Reading:0.0}°, на самом деле {slope.TrueAngle:0.0}°");
            }
            var bath = game.Dept("bath")?.apparatus[0] as Bath3D;
            if (bath != null && bath.Active && _sheetAt < 0 && bath.Calm > 0.1f && !SessionState.GetBool("lab3d.bathShot", false))
            {   // the water stood still against the scale
                SessionState.SetBool("lab3d.bathShot", true);
                Picture(game, 8, "bath");
                Say($"  ванночка: показывает {bath.Reading:0.00} л, на самом деле {bath.Volume:0.00} л");
            }
            if (game.calculator != null && game.calculator.activeSelf && game.day.T > 8 && !SessionState.GetBool("lab3d.calcShot", false))
            {
                SessionState.SetBool("lab3d.calcShot", true);
                Picture(game, 9, "calculator");
            }
            int dynaShot = SessionState.GetInt("lab3d.dynaShot", 0);
            if (dyn != null && _sheetAt < 0 && dyn.Calm > 0.1f && (dynaShot < 1 || (dynaShot < 2 && game.G.ScaleStep < 1)))
            {   // the pointer stopped against the scale: as the player reads it (and once more with the first finer scale)
                SessionState.SetInt("lab3d.dynaShot", dynaShot + 1);
                Picture(game, 6, "dyna_" + game.day.Day);
                var lc = GameObject.Find("LoupeCamera")?.GetComponent<Camera>();
                if (lc != null && lc.targetTexture != null)
                {   // what the magnifying glass shows
                    lc.Render();
                    var lrt = lc.targetTexture;
                    RenderTexture.active = lrt;
                    var ltex = new Texture2D(lrt.width, lrt.height, TextureFormat.RGB24, false);
                    ltex.ReadPixels(new Rect(0, 0, lrt.width, lrt.height), 0, 0);
                    RenderTexture.active = null;
                    File.WriteAllBytes($"Build/Lab3D/loupe_{game.day.Day}.png", ltex.EncodeToPNG());
                }
                Say($"  динамометр: показывает {dyn.Reading:0.00} Н, на самом деле {dyn.Force:0.00} Н");
            }
            if (game.day.T > 20 && SessionState.GetInt(Shot, 0) == game.day.Day - 1 && game.day.Day <= 3)
            {
                Picture(game, game.day.Day, "play_" + game.day.Day);
                SessionState.SetInt(Shot, game.day.Day);
            }
            if (game.day.Evening && game.day.Hour > 18.1f && SessionState.GetInt(Evened, -1) != game.day.Day)
            {
                SessionState.SetInt(Evened, game.day.Day);
                var g = game.G;
                Say($"{Now}  вечер дня {game.day.Day}: +{GameState.Fmt(g.LessonEarned[(int)Cur.Obs])} идей, +{GameState.Fmt(g.LessonEarned[(int)Cur.J])} Дж, " +
                    $"заказов {game.Book.Delivered} (всего {Lab3DBot.paid}, руками {Lab3DBot.byBot})");
                if (game.day.Day <= 2) Picture(game, 0, "evening_" + game.day.Day);
            }
        }

        static void Summary(GameState g)
        {
            Say($"\nитог за {Now}: дней {g.Lessons}, идей {GameState.Fmt(g.Cur[(int)Cur.Obs])} (всего {GameState.Fmt(g.Total[(int)Cur.Obs])}), " +
                $"Дж {GameState.Fmt(g.Cur[(int)Cur.J])} (всего {GameState.Fmt(g.Total[(int)Cur.J])})");
            Say("  теории: " + string.Join(", ", g.Proven));
            foreach (var id in g.Known) Say($"  {Defs.L(id).sym}: уровень {g.Level(id)}, значение {g.LetterValue(id)}");
            Say("  улучшения: " + string.Join(", ", g.Perks.Concat(g.Devices)));
            Say($"  заказов {Lab3DBot.paid}, монет всего {GameState.Fmt(g.Total[(int)Cur.Coin])}");
            Finish(0);
        }

        static float _oddSheetAt = -1;
        static OrderSheet _oddSheet;

        static void OddSheet(Lab3DGame game)
        {
            if (game.day.Day < 4 || SessionState.GetBool("lab3d.oddSheetShot", false)) return;
            if (_oddSheetAt < 0)
            {
                var open = game.sheets.FindAll(s => s.gameObject.activeSelf && !s.sent && !game.Line.Incoming(s) && !s.stamped && s.SampleHome);
                if (game.Hands.Reading != null) return;
                _oddSheet = open.FindLast(s => s.tests.Exists(Oddity.Is) && !s.Answered);
                if (_oddSheet == null) return;
                Place(game, 5);
                game.Hands.Down(_oddSheet);   // into the left hand
                _oddSheetAt = Time.realtimeSinceStartup;
                return;
            }
            if (Time.realtimeSinceStartup - _oddSheetAt < 1.6f) return;
            SessionState.SetBool("lab3d.oddSheetShot", true);
            int row = _oddSheet.tests.FindIndex(Oddity.Is);
            if (game.Hands.Reading == _oddSheet && row >= 0)
            {
                var had = _oddSheet.answers[row];
                _oddSheet.answers[row] = "";
                _oddSheet.view.Tick(row, 0);
                if (Oddity.Multi(_oddSheet.tests[row])) _oddSheet.view.Tick(row, 2);
                Picture(game, 5, "sheet_odd");
                _oddSheet.answers[row] = had;
                game.Hands.CloseSheet();
                Say($"  листок с крейзи-строкой: {string.Join(", ", _oddSheet.tests)}");
            }
            _oddSheetAt = -1;
        }

        static void Lineup(Lab3DGame game)
        {
            var root = new GameObject("Lineup").transform;
            root.position = new Vector3(40, 0, 0);
            var all = Persona.All;
            for (int i = 0; i < all.Length; i++)
            {
                var p = new GameObject(all[i]).transform;
                p.SetParent(root, false);
                p.localPosition = new Vector3((i % 9) * 0.8f, 0, (i / 9) * 6f);
                p.localRotation = Quaternion.Euler(0, 180, 0);   // facing -z, at the camera
                Persona.Build(all[i], p, game.kit);
            }
            var cc = game.walker.GetComponent<CharacterController>();
            var hud = game.GetComponentInChildren<Canvas>(); hud.enabled = false;
            for (int row = 0; row * 9 < all.Length; row++)
                foreach (var (yaw, tag) in new[] { (0f, "front"), (40f, "side") })
                {
                    cc.enabled = false;
                    var c = root.position + new Vector3(3.2f, 0, row * 6f);
                    var at = c + Quaternion.Euler(0, yaw, 0) * new Vector3(0, 0, -4.2f);
                    game.walker.transform.SetPositionAndRotation(new Vector3(at.x, 0.02f, at.z), Quaternion.LookRotation(c - at));
                    game.eye.transform.localRotation = Quaternion.Euler(5, 0, 0);
                    cc.enabled = true;
                    Shoot(game.eye, $"lineup_{row}_{tag}");
                }
            hud.enabled = true;
            Object.Destroy(root.gameObject);
            Say("  заказчики: " + string.Join(", ", all));
        }

        static readonly string[] OddMoments = { "carry", "taste", "dark", "bite", "fish", "mirror", "cat", "note", "tickle" };

        /// An odd department at work, from in front of its table (they stand where the order of buying put them).
        static void OddPicture(Lab3DGame game, string m)
        {
            if (m == "carry") { Picture(game, 5, "odd_carry"); return; }
            if (m == "dark") { Say($"  темнота: {game.Lights.Dark}, маска {game.eye.cullingMask}"); Picture(game, 5, "odd_dark"); return; }
            string dept = m is "bite" or "fish" ? "fish" : m;
            var st = game.Dept(dept)?.table;
            if (st == null) return;
            var (dist, pitch, up) = dept switch
            {
                "fish" => (1.75f, 24f, 0.5f), "mirror" => (1.15f, 6f, 1.45f), "taste" => (0.85f, 46f, 1.2f),
                "cat" => (0.85f, 38f, 1.25f), "note" => (1.0f, 40f, 1.2f), _ => (0.85f, 34f, 1.3f),
            };
            var at = st.TransformPoint(new Vector3(dept == "note" ? -0.1f : 0, 0, dist));
            var look = st.TransformPoint(new Vector3(dept == "note" ? -0.1f : 0, up, 0)) - new Vector3(at.x, 0, at.z);
            var hud = game.GetComponentInChildren<Canvas>();
            hud.renderMode = RenderMode.ScreenSpaceCamera;
            hud.worldCamera = game.eye;
            hud.planeDistance = 0.1f;
            var cc = game.walker.GetComponent<CharacterController>();
            cc.enabled = false;
            game.walker.transform.SetPositionAndRotation(new Vector3(at.x, 0.02f, at.z), Quaternion.Euler(0, Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg, 0));
            game.eye.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            Chalk3DText.FaceAll();
            cc.enabled = true;
            foreach (var mr in Object.FindObjectsByType<Mirror3D>(FindObjectsSortMode.None)) mr.Refresh();
            Shoot(game.eye, "odd_" + m);
            hud.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        /// A picture through the player's eyes with the HUD; the views go round the room.
        static OrderSheet _gaveUp;
        static Customer3D _gaveUpWho;
        static float _gaveUpAt;

        static void Picture(Lab3DGame game, int n, string name)
        {
            var hud = game.GetComponentInChildren<Canvas>();
            hud.renderMode = RenderMode.ScreenSpaceCamera;
            hud.worldCamera = game.eye;
            hud.planeDistance = 0.1f;
            Place(game, n);
            Shoot(game.eye, name);
            hud.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        /// The player stands where a picture is taken from.
        static void Place(Lab3DGame game, int n)
        {
            var views = new (Vector3 at, float yaw, float pitch)[]
            {
                (new Vector3(1.3f, 0.02f, 1.35f), 12, 10), (new Vector3(2.1f, 0.02f, 1.7f), 0, 25),
                (new Vector3(-1.2f, 0.02f, -0.5f), 180, 22), (new Vector3(1.9f, 0.02f, -0.6f), 180, 22),
                (new Vector3(0.1f, 0.02f, -0.2f), 300, 14),   // the room: the departments and the helpers
                (new Vector3(2.1f, 0.02f, 2.15f), 0, 32),     // the counter close up: the order sheets
                (new Vector3(0.25f, 0.02f, 2.55f), 0, 4),     // the dynamometer close up: its scale
                (new Vector3(2.5f, 0.02f, 2.9f), 225, 12),    // the room from the counter, to the temporary walls
                (new Vector3(-1.45f, 0.02f, 2.45f), 0, 22),   // the bath close up: the can and the measuring cylinder
                (new Vector3(0.85f, 0.02f, 2.4f), 0, 52),     // the calculator
                (Vector3.zero, 0, 8),                         // the door, from a step into the room (worked out below)
                (new Vector3(1.75f, 0.02f, 1.95f), 10, 2),    // the counter and its two pipes
                (new Vector3(-0.6f, 0.02f, 1.6f), 0, 18),     // the two tables, their signs
                (new Vector3(0.6f, 0.02f, 2.55f), 0, 55),     // the foot of the back wall under the tables
                (new Vector3(1.6f, 0.02f, 2.2f), 15, -40),    // up at the ceiling: the pipes' mounts and the bulb
                (new Vector3(0.2f, 0.02f, 1.5f), 75, 12),     // the closet from its left: the door, the counter, the customers
                (new Vector3(1.95f, 0.02f, 2.25f), 0, 2),     // the window over the counter, the customer at it
                (new Vector3(-0.6f, 0.02f, 0.75f), 180, 26),  // the inclined plane
            };
            var v = views[n % views.Length];
            if (n == 10 && game.door != null)
            {
                var db = game.door.GetComponent<Renderer>().bounds;
                var into = db.size.x < db.size.z ? new Vector3(-Mathf.Sign(db.center.x), 0, 0) : new Vector3(0, 0, -Mathf.Sign(db.center.z));
                var at = db.center + into * 1.3f;
                v = (new Vector3(at.x, 0.02f, at.z), Quaternion.LookRotation(-into).eulerAngles.y, 8);
            }
            var cc = game.walker.GetComponent<CharacterController>();
            cc.enabled = false;
            game.walker.transform.SetPositionAndRotation(v.at, Quaternion.Euler(0, v.yaw, 0));
            game.eye.transform.localRotation = Quaternion.Euler(v.pitch, 0, 0);
            Chalk3DText.FaceAll();
            cc.enabled = true;
        }

        /// A theory's screen as the player sees it before solving it: the sketch with its question marks.
        static void TheoryPicture(string id)
        {
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var t = typeof(GameRoot);
            var screen = t.GetField("_breakScreen", flags);
            var apply = t.GetMethod("ApplyPhase", flags);
            var alchemy = t.GetField("_alchemy", flags)?.GetValue(GameRoot.I) as Alchemy;
            if (screen == null || apply == null || alchemy == null) { Say("  снимок теории: не нашёл экран теорий"); return; }
            var was = screen.GetValue(GameRoot.I);
            screen.SetValue(GameRoot.I, System.Enum.Parse(screen.FieldType, "Alchemy"));
            apply.Invoke(GameRoot.I, null);
            alchemy.Select(id);
            foreach (var pop in alchemy.GetComponentsInChildren<Transform>(true)) if (pop.localScale == Vector3.zero) pop.localScale = Vector3.one;   // no pop-in for the shot
            MapPictureNamed("theory_" + id);
            screen.SetValue(GameRoot.I, was);
            apply.Invoke(GameRoot.I, null);
        }

        static void MapPictureNamed(string name)
        {
            var canvas = GameObject.Find("Canvas")?.GetComponent<Canvas>();
            var cam = Camera.main;
            if (canvas == null || cam == null) return;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1;
            Shoot(cam, name);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        /// The map between the days, with its canvas put in front of the camera for the shot.
        static void MapPicture(int n)
        {
            var canvas = GameObject.Find("Canvas")?.GetComponent<Canvas>();
            var cam = Camera.main;
            if (canvas == null || cam == null) return;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1;
            Shoot(cam, "map_" + n);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        static void Certs(Lab3DGame game)
        {
            var w = game.awards;
            Say($"{Now}  грамоты ({w.All.Count}): " + string.Join("; ", w.All.Select(c =>
                $"{c.data.id} {(c.gameObject.activeSelf ? $"({c.transform.position.x:0.00}, {c.transform.position.y:0.00}, {c.transform.position.z:0.00}) {c.data.wall}" : "не повешена")}")));
            void Look(Certificate c, float dist)
            {
                var cc = game.walker.GetComponent<CharacterController>();
                cc.enabled = false;
                var at = c.transform.position + c.transform.forward * dist;
                game.walker.transform.SetPositionAndRotation(new Vector3(at.x, 0.02f, at.z), Quaternion.LookRotation(-c.transform.forward));
                var to = c.transform.position - game.eye.transform.position;
                game.eye.transform.localRotation = Quaternion.Euler(Mathf.Atan2(-to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg, 0, 0);
                cc.enabled = true;
                Physics.SyncTransforms();
            }
            var first = w.All.FirstOrDefault(c => c.gameObject.activeSelf);
            if (first == null) return;
            Look(first, 0.75f);
            Shoot(game.eye, "award_close_d" + game.day.Day);
            Look(first, 2.0f);
            Shoot(game.eye, "award_wall_d" + game.day.Day);
            if (game.day.Day != 2) return;
            // taken off the wall: carried; then hung on another wall — the first free place along the cross wall or the right one
            Look(first, 1.2f);
            Lab3DBot.hold = true;
            var aim = game.Pointer.Aim();
            Say($"  прицел на грамоту: {(aim != null ? aim.name : "null")}");
            game.Hands.Down(aim);
            Shoot(game.eye, "award_carried");
            bool hung = false;
            foreach (var (p, n) in new[] { (new Vector3(2.0f, 1.7f, 1.05f), Vector3.forward), (new Vector3(1.6f, 1.7f, 1.05f), Vector3.forward), (new Vector3(2.95f, 1.7f, 3.0f), Vector3.left), (new Vector3(2.95f, 2.2f, 2.9f), Vector3.left) })
            {
                if (!w.HangNow(p, n)) { Say($"  грамоту не повесить в ({p.x}, {p.y}, {p.z}): {(w.Holding ? AwardWall.Why : "не в руках")}"); continue; }
                Say($"  грамота перевешена: ({first.transform.position.x:0.00}, {first.transform.position.y:0.00}, {first.transform.position.z:0.00}) {first.data.wall}");
                hung = true;
                break;
            }
            if (!hung) w.PutBack();
            Look(first, 1.6f);
            Shoot(game.eye, "award_moved");
            Lab3DBot.hold = false;
        }

        static void Shoot(Camera cam, string name)
        {
            Canvas.ForceUpdateCanvases();
            var rt = new RenderTexture(1600, 900, 24);
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
            RenderTexture.active = rt;
            var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            RenderTexture.active = null;
            Directory.CreateDirectory("Build/Lab3D");
            File.WriteAllBytes($"Build/Lab3D/{name}.png", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
            Say($"{Now}  снимок {name}");
        }

        static void Finish(int code)
        {
            File.WriteAllText(OutPath, SessionState.GetString(Log, ""));
            SessionState.SetBool(On, false);
            EditorApplication.update -= Step;
            EditorApplication.Exit(code);
        }
    }
}
