using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ChalkPhysics.Lab3D
{
    /// The writing on an order sheet (DESIGN.md 2б, «Листки-заказы»), on the paper itself: the order's number, its story,
    /// the reward, a table — one row a measurement (up to three), each with a box to write the number in — and at the
    /// bottom right a place for the stamp. It reads on the counter already; picked up, the sheet itself comes into the
    /// left hand, at the left of the view, and goes about the room with the player. The keyboard writes the number (a
    /// comma or a point for the fraction) into the chosen row; Tab or Enter chooses the next one; Esc, or a click on the
    /// counter with empty hands, puts it back on the counter. Stamped, it goes into the dispatch box with its sample.
    public class SheetView : MonoBehaviour
    {
        Lab3DGame _game;
        OrderSheet _sheet;
        Text _title, _story, _note;
        InkText _rewardInk, _noteInk;
        readonly Text[] _rowName = new Text[3], _answer = new Text[3];
        readonly RectTransform[] _box = new RectTransform[3];
        // the odd rows (Oddity): the options in a line under the row's name, each with a box to tick and its number in it
        const int MaxOptions = 7;
        readonly Text[,] _opt = new Text[3, MaxOptions], _num = new Text[3, MaxOptions];
        readonly float[] _top = new float[3], _h = new float[3];
        float _bottom = -85;
        RectTransform _paperRt, _stampRt;
        ChalkShape _table, _stampMark;
        Text _word;
        int _focus;
        float _k;               // 0 lying on the counter, 1 held before the eyes
        float _stampT = -1;     // the stamp's mark coming in
        bool _open;
        Vector3 _restPos;
        Quaternion _restRot;
        public bool Open => _open;
        public OrderSheet Sheet => _sheet;
        /// The game carries the sheet itself (in the hand, in the box): it does not fly back to the counter.
        [System.NonSerialized] public bool away;

        const float Width = 600, Height = 800;   // canvas units on the paper
        const float HoldAt = 0.36f;             // metres before the eyes, at the left of the view: the dot stays free
        static readonly float[] RowY = { -55, -125, -195 };

        /// The writing on a sheet's leaf: a canvas lying on the paper, its top away from the one who reads it.
        public static SheetView Build(OrderSheet sheet, Lab3DGame game)
        {
            var rt = Art3D.WorldCanvas("Writing", sheet.leaf);
            rt.sizeDelta = new Vector2(Width, Height);
            rt.localScale = Vector3.one * (sheet.paperSize.x / Width);
            rt.localPosition = Vector3.up * 0.0034f;
            rt.localRotation = Quaternion.Euler(90, 0, 0);   // flat on the paper, read from above
            var v = sheet.leaf.gameObject.AddComponent<SheetView>();
            v._game = game;
            v._sheet = sheet;
            v._paperRt = rt;
            v._restPos = sheet.leaf.localPosition;
            v._restRot = sheet.leaf.localRotation;
            var t = rt.transform;

            v._title = UIF.Text(t, "", 56, ChalkTex.White, new Vector2(0, 335), new Vector2(560, 70));
            v._title.resizeTextForBestFit = true;   // a long trade («хранитель музея») shrinks the line, never wraps onto the story
            v._title.resizeTextMinSize = 28;
            v._title.resizeTextMaxSize = 56;
            v._title.horizontalOverflow = HorizontalWrapMode.Wrap;
            v._title.verticalOverflow = VerticalWrapMode.Truncate;
            v._story = UIF.Text(t, "", 34, ChalkTex.White, new Vector2(0, 205), new Vector2(540, 180), TextAnchor.UpperLeft);
            v._rewardInk = InkText.On(UIF.Text(t, "", 38, ChalkTex.Green, new Vector2(-110, 75), new Vector2(320, 56), TextAnchor.MiddleLeft));
            v._noteInk = InkText.On(UIF.Text(t, "", 24, ChalkTex.Dim, new Vector2(0, 44), new Vector2(540, 30), TextAnchor.MiddleLeft));
            v._table = UIF.Shape(t, 32, "Table");
            UIF.Text(t, "Что узнать", 26, ChalkTex.Dim, new Vector2(-120, 5), new Vector2(260, 40), TextAnchor.MiddleLeft);
            UIF.Text(t, "Ответ", 26, ChalkTex.Dim, new Vector2(145, 5), new Vector2(240, 40));
            for (int i = 0; i < 3; i++)
            {
                v._rowName[i] = UIF.Text(t, "", 28, ChalkTex.White, new Vector2(-120, RowY[i]), new Vector2(270, 56), TextAnchor.MiddleLeft);
                v._box[i] = UIF.Rect("Box" + i, t, new Vector2(145, RowY[i]), new Vector2(220, 56));
                v._answer[i] = UIF.Text(t, "", 40, ChalkTex.Yellow, new Vector2(145, RowY[i]), new Vector2(210, 56));
                for (int k = 0; k < MaxOptions; k++)
                {
                    v._opt[i, k] = UIF.Text(t, "", 26, ChalkTex.White, Vector2.zero, new Vector2(100, 40), TextAnchor.MiddleLeft);
                    v._opt[i, k].horizontalOverflow = HorizontalWrapMode.Overflow;
                    v._num[i, k] = UIF.Text(t, "", 18, ChalkTex.Dim, Vector2.zero, new Vector2(26, 26));
                }
            }
            v._note = UIF.Text(t, "", 24, ChalkTex.Dim, new Vector2(-120, -300), new Vector2(330, 120), TextAnchor.UpperLeft);
            // the place for the stamp, bottom right: a faint frame until it is stamped
            v._stampRt = UIF.Rect("Stamp", t, new Vector2(160, -322), new Vector2(230, 130));
            v._stampMark = UIF.Shape(v._stampRt, 34, "Mark");
            v._word = UIF.Text(v._stampRt, "", 34, ChalkTex.Red, Vector2.zero, new Vector2(230, 50));
            v._word.transform.localRotation = Quaternion.Euler(0, 0, -12);
            return v;
        }

        /// The order is written on the sheet (in the morning).
        public void Fill()
        {
            _focus = 0;
            _stampT = -1;
            away = false;
            _note.text = "";
            _title.text = "Заказ №" + _sheet.number + " · " + Persona.Name(_sheet.who) + (_sheet.contract ? " · контракт" : "");
            _noteInk.Set(_sheet.note ?? "", ChalkTex.Yellow);
            _story.text = _sheet.story;
            _rewardInk.Set("Награда до " + _sheet.reward + " " + GameState.Coin);
            int rows = _sheet.tests.Count;
            Layout();
            for (int i = 0; i < 3; i++)
            {
                _rowName[i].text = i < rows ? OrderSheet.RowName(_sheet.tests[i]) : "";
                _answer[i].text = "";
            }
            DrawTable();
            DrawStamp();
            _k = 0;
            _sheet.leaf.localPosition = _restPos;
            _sheet.leaf.localRotation = _restRot;
            _sheet.leaf.localScale = Vector3.one;
        }

        bool Odd(int i) => i < _sheet.tests.Count && Oddity.Is(_sheet.tests[i]);

        /// The rows one under another: a number's row is 70 high, its name on the left and its box on the right; an odd
        /// row is 100, its name over the whole width and its options in a line under it.
        /// The options of an odd row in columns: two, three or four a line, so that no word runs into the next box.
        static int Cols(int n) => n <= 2 ? n : n == 4 || n == 7 ? 4 : 3;
        static int Lines(int n) => (n + Cols(n) - 1) / Mathf.Max(1, Cols(n));

        void Layout()
        {
            float y = -15;
            for (int i = 0; i < 3; i++)
            {
                bool odd = Odd(i);
                _top[i] = y;
                _h[i] = i < _sheet.tests.Count ? (odd ? 52 + 44 * Lines(Oddity.Options(_sheet.tests[i]).Length) : 70) : 0;
                var name = (RectTransform)_rowName[i].transform;
                name.anchoredPosition = odd ? new Vector2(0, y - 26) : new Vector2(-120, y - 35);
                name.sizeDelta = odd ? new Vector2(530, 46) : new Vector2(270, 56);
                _box[i].anchoredPosition = new Vector2(145, y - 35);
                ((RectTransform)_answer[i].transform).anchoredPosition = new Vector2(145, y - 35);
                var opts = odd ? Oddity.Options(_sheet.tests[i]) : null;
                int n = opts?.Length ?? 0;
                for (int k = 0; k < MaxOptions; k++)
                {
                    bool on = k < n;
                    _opt[i, k].text = on ? opts[k] : "";
                    _num[i, k].text = on ? (k + 1).ToString() : "";
                    if (!on) continue;
                    var box = OptionBox(i, k, n);
                    ((RectTransform)_num[i, k].transform).anchoredPosition = box;
                    float end = box.x - 20 + 540f / Cols(n);
                    var rt = (RectTransform)_opt[i, k].transform;
                    rt.sizeDelta = new Vector2(end - box.x - 24, 40);
                    rt.anchoredPosition = new Vector2((box.x + 24 + end) / 2, box.y);
                }
                y -= _h[i];
            }
            _bottom = y;
        }

        Vector2 OptionBox(int i, int k, int n) => new Vector2(-270 + 540f * (k % Cols(n)) / Cols(n) + 24, _top[i] - 70 - 44 * (k / Cols(n)));

        void DrawTable()
        {
            int rows = _sheet.tests.Count;
            float bottom = _bottom;
            _table.Clear();
            _table.Rect(new Vector2(0, (25 + bottom) / 2), new Vector2(540, 25 - bottom), ChalkTex.White, 3f);
            _table.Line(new Vector2(-270, -15), new Vector2(270, -15), ChalkTex.White, 2.5f);
            for (int i = 1; i < rows; i++) _table.Line(new Vector2(-270, _top[i]), new Vector2(270, _top[i]), ChalkTex.White, 2f);
            _table.Line(new Vector2(20, -15), new Vector2(20, 25), ChalkTex.White, 2.5f);
            for (int i = 0; i < rows; i++)
            {
                bool focus = _open && i == _focus && !_sheet.stamped;
                var col = focus ? ChalkTex.Yellow : new Color(1f, 0.84f, 0.32f, 0.5f);
                if (!Odd(i))
                {
                    _table.Line(new Vector2(20, _top[i]), new Vector2(20, _top[i] - _h[i]), ChalkTex.White, 2.5f);
                    _table.Rect(_box[i].anchoredPosition, new Vector2(220, 50), col, focus ? 3f : 2f);
                    continue;
                }
                // the options' boxes; a ticked one gets a tick over its number
                int n = Oddity.Options(_sheet.tests[i]).Length, got = Oddity.Ticked(_sheet.answers[i]);
                for (int k = 0; k < n; k++)
                {
                    var b = OptionBox(i, k, n);
                    _table.Rect(b, new Vector2(26, 26), col, focus ? 2.5f : 2f);
                    bool tick = ((got >> k) & 1) != 0;
                    _num[i, k].enabled = !tick;
                    if (tick)
                    {
                        _table.Line(b + new Vector2(-11, 2), b + new Vector2(-3, -9), ChalkTex.Yellow, 4f);
                        _table.Line(b + new Vector2(-3, -9), b + new Vector2(14, 16), ChalkTex.Yellow, 4f);
                    }
                }
            }
        }

        void DrawStamp()
        {
            _stampMark.Clear();
            if (!_sheet.stamped)
            {
                _word.text = "";
                _stampMark.Rect(Vector2.zero, new Vector2(220, 130), new Color(1, 1, 1, 0.22f), 2f);
                return;
            }
            // the stamp's mark: a double ring and the word, a little askew, swelling in as it lands
            float k = _stampT < 0 ? 1 : Mathf.Clamp01(_stampT / 0.18f);
            float s = Mathf.Lerp(1.5f, 1f, k);
            var col = new Color(1f, 0.42f, 0.42f, Mathf.Lerp(0.2f, 0.95f, k));
            _stampMark.Circle(Vector2.zero, 62 * s, col, 5f);
            _stampMark.Circle(Vector2.zero, 52 * s, col, 2.5f);
            _word.text = "ПРИНЯТО";
            _word.color = col;
            _word.transform.localScale = Vector3.one * s;
        }

        /// The stamp has come down on the sheet: its mark swells in.
        public void Stamped()
        {
            _stampT = 0;
            _note.text = "";
            DrawStamp();
        }

        public void Show()
        {
            _open = true;
            away = false;
            Grip(false);
            _focus = 0;
            while (_focus < _sheet.tests.Count - 1 && _sheet.Filled(_focus)) _focus++;
            if (Keyboard.current != null) Keyboard.current.onTextInput += Type;
            DrawTable();
        }

        /// Put back down on the counter.
        public void Hide()
        {
            _open = false;   // its collider comes back once it lies on the counter (Fly): in the air it would push the player
            if (Keyboard.current != null) Keyboard.current.onTextInput -= Type;
            _note.text = "";
            ShowAnswers(false);
            DrawTable();
        }

        /// Into the dispatch box: the game moves it from here on.
        public void Away()
        {
            _open = false;
            away = true;
            if (Keyboard.current != null) Keyboard.current.onTextInput -= Type;
            _note.text = "";
            ShowAnswers(false);
            DrawTable();
        }

        /// The paper's collider: off while it is in the hand (it is not in the way of the dot), on when it lies.
        bool _gripOn = true;

        void Grip(bool on)
        {
            _gripOn = on;
            foreach (var c in _sheet.leaf.GetComponentsInChildren<Collider>()) c.enabled = on;
        }

        void OnDisable()
        {
            if (Keyboard.current != null) Keyboard.current.onTextInput -= Type;
        }

        void Type(char c)
        {
            if (_sheet.stamped) return;   // a stamped sheet is signed and sealed
            if (Odd(_focus)) { Tick(_focus, c - '1'); return; }
            string a = _sheet.answers[_focus] ?? "";
            if (a.Length >= 7) return;
            if (char.IsDigit(c)) a += c;
            else if ((c == ',' || c == '.') && !a.Contains(",")) a += a.Length == 0 ? "0," : ",";
            else return;
            _sheet.answers[_focus] = a;
        }

        /// Option k of an odd row ticked (or, ticked already, unticked): one of the taste's several, the only one elsewhere.
        public void Tick(int row, int k)
        {
            if (row < 0 || row >= _sheet.tests.Count || !Odd(row)) return;
            var test = _sheet.tests[row];
            if (k < 0 || k >= Oddity.Options(test).Length) return;
            int got = Oddity.Ticked(_sheet.answers[row]);
            got = Oddity.Multi(test) ? got ^ (1 << k) : (got == 1 << k ? 0 : 1 << k);
            string s = "";
            for (int j = 0; j < 9; j++) if (((got >> j) & 1) != 0) s += (char)('1' + j);
            _sheet.answers[row] = s;
            Sfx.Play("chalk", 0.5f, 1.1f);
            DrawTable();
        }

        void ShowAnswers(bool caret)
        {
            for (int i = 0; i < _sheet.tests.Count; i++)
                if (Odd(i)) _answer[i].text = "";
                else _answer[i].text = (_sheet.answers[i] ?? "") + (caret && i == _focus && !_sheet.stamped ? "|" : "");
        }

        void Update()
        {
            if (_stampT >= 0) { _stampT += Time.unscaledDeltaTime; DrawStamp(); if (_stampT > 0.3f) _stampT = -1; }
            Fly();
            if (!_open) return;
            var kb = Keyboard.current;
            int rows = _sheet.tests.Count;
            if (kb != null)
            {
                if (kb.backspaceKey.wasPressedThisFrame && !_sheet.stamped)
                {
                    var a = _sheet.answers[_focus] ?? "";
                    if (Odd(_focus)) { _sheet.answers[_focus] = ""; DrawTable(); }
                    else if (a.Length > 0) _sheet.answers[_focus] = a.Substring(0, a.Length - 1);
                }
                if (kb.tabKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) { _focus = (_focus + 1) % rows; DrawTable(); }
                if (kb.escapeKey.wasPressedThisFrame) { _game.Hands.CloseSheet(); return; }
            }
            ShowAnswers(Time.unscaledTime % 1f < 0.55f);
            _note.text = _sheet.stamped ? "Принято — в ящик у трубы, с образцом"
                : !_sheet.Answered ? (Odd(_focus) ? "цифры — отметить, Tab — строка" : "Tab — строка, Esc — положить")
                : "Готово — положи на стойку и поставь печать";
        }

        /// The sheet itself flies: up to just before the eyes and turned to them when held, back onto the counter when
        /// put down. In the hand or in the box the game moves it.
        void Fly()
        {
            if (away) return;
            var leaf = _sheet.leaf;
            float target = _open ? 1 : 0;
            if (Mathf.Abs(_k - target) < 0.0001f && !_open) { if (!_gripOn) Grip(true); return; }
            if (_k <= 0.0001f) _slide = _sheet.SampleHome;   // decided as it leaves the table: is its sample lying on it?
            _k = Mathf.MoveTowards(_k, target, Time.unscaledDeltaTime / (_slide ? 0.6f : 0.35f));
            float e = _k * _k * (3 - 2 * _k);
            var parent = _sheet.transform;
            var restPos = parent.TransformPoint(_restPos);
            var restRot = parent.rotation * _restRot;
            var eye = _game.eye.transform;
            // in the left hand, at the left of the view, turned a little towards the middle: it goes about with the player
            var holdPos = eye.position + eye.forward * HoldAt - eye.right * 0.2f - eye.up * 0.03f;
            var holdRot = eye.rotation * Quaternion.Euler(0, -18, 0) * Quaternion.Inverse(Quaternion.Euler(90, 0, 0));
            // with its sample on it, the sheet first slides out from under the sample, along the table towards the one who
            // takes it; then it rises to the eyes in a little arc (and back down the same way)
            const float Split = 0.4f;
            var outPos = SlidOut();
            if (_slide && e < Split)
            {
                float f = e / Split;
                leaf.position = Vector3.Lerp(restPos, outPos, f * (2 - f));
                leaf.rotation = restRot;
                return;
            }
            float u = _slide ? (e - Split) / (1 - Split) : e;
            var from = _slide ? outPos : restPos;
            leaf.position = Vector3.Lerp(from, holdPos, u) + Vector3.up * (Mathf.Sin(u * Mathf.PI) * 0.05f);
            leaf.rotation = Quaternion.Slerp(restRot, holdRot, u);
        }

        bool _slide;

        /// Where the sheet is once slid out from under its sample: pulled towards the reader along the table, past the
        /// sample, a hair above the sheets it slides over.
        public Vector3 SlidOut() => _sheet.transform.TransformPoint(_restPos + _restRot * new Vector3(0, 0.004f, -SlideBy));
        public const float SlideBy = 0.3f;

        /// Back on the counter at once, on its own place (a sheet taken back out of the hand or the box).
        public void Rest()
        {
            away = false;
            _k = 0;
            _sheet.leaf.SetParent(_sheet.transform, true);
            _sheet.leaf.localPosition = _restPos;
            _sheet.leaf.localRotation = _restRot;
        }
    }
}
