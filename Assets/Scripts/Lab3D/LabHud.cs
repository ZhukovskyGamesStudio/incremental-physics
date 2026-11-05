using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ChalkPhysics.Lab3D
{
    /// What is on the screen over the room: the coins in the top left corner (the coins of a delivered order fly into
    /// them), the little clock and the day's number in the top right with the goals under it (Career), the dot in the
    /// middle with a word or two of what a click does, the keys while a sheet is in hand, the cards of the morning and
    /// the evening, and the veil the day comes out of and goes back into.
    public class LabHud : MonoBehaviour
    {
        Lab3DGame _game;
        InkText _coins, _joules;
        Text _hint, _keys, _dayText, _cardTitle, _cardNote;
        ChalkShape _dot, _clock;
        InkText _cardSum;
        CanvasGroup _card, _fade;
        float _cardUntil, _coinPulse;
        readonly List<(string title, string sum, string note)> _cards = new List<(string, string, string)>();
        // the goals in the corner, under the clock: today's goal, the contract, the week
        readonly InkText[] _goalLines = new InkText[3];
        float _goalsAt;
        /// The coins the counter shows while the coins of an order are flying into it (not all at once).
        double _shown;
        bool _rolling;
        bool _debugAim;

        GameState G => _game.G;
        LabDay Day => _game.day;

        public void Init(Lab3DGame game) => _game = game;

        public void Build()
        {
            var go = new GameObject("Hud", typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(transform, false);
            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1;

            InkText Counter(float y)
            {
                var t = UIF.Text(go.transform, "", 44, ChalkTex.White, Vector2.zero, new Vector2(400, 60), TextAnchor.MiddleLeft);
                var rt = t.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0, 1);   // pivot stays in the middle: InkText draws the bulb from there
                rt.anchoredPosition = new Vector2(40 + 200, y);
                return InkText.On(t);
            }
            _coins = Counter(-60);
            _joules = Counter(-120);
            ShowMoney();

            _dot = UIF.Shape(go.transform, 3, "Dot");
            _hint = UIF.Text(go.transform, "", 42, ChalkTex.White, new Vector2(0, -70), new Vector2(800, 110), TextAnchor.UpperCenter);
            _keys = UIF.Text(go.transform, "", 36, ChalkTex.White, new Vector2(0, -470), new Vector2(1500, 64));
            foreach (var t in new[] { _hint, _keys })
            {   // a dark rim round the letters: readable over the chalk of any wall or table
                var o = t.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(0.04f, 0.06f, 0.05f, 0.95f);
                o.effectDistance = new Vector2(3, -3);
                var sh = t.gameObject.AddComponent<Shadow>();
                sh.effectColor = new Color(0.04f, 0.06f, 0.05f, 0.8f);
                sh.effectDistance = new Vector2(4, -5);
            }

            // the day: a little clock and the day's number in the top right corner
            var corner = UIF.Rect("Day", go.transform, Vector2.zero, new Vector2(220, 80));
            corner.anchorMin = corner.anchorMax = new Vector2(1, 1);
            corner.anchoredPosition = new Vector2(-150, -60);
            _clock = UIF.Shape(corner, 5, "Clock");
            _clock.rectTransform.anchoredPosition = new Vector2(-80, 0);
            _dayText = UIF.Text(corner, "", 36, ChalkTex.White, new Vector2(60, 0), new Vector2(160, 60), TextAnchor.MiddleLeft);
            for (int i = 0; i < _goalLines.Length; i++)
            {   // the goals between the days (Career), right under the clock
                var t = UIF.Text(go.transform, "", 30, ChalkTex.White, Vector2.zero, new Vector2(900, 44), TextAnchor.MiddleRight);
                var rt = t.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(1, 1);
                rt.anchoredPosition = new Vector2(-40 - 450, -128 - i * 40);
                var o = t.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(0.04f, 0.06f, 0.05f, 0.9f);
                o.effectDistance = new Vector2(2, -2);
                _goalLines[i] = InkText.On(t);
            }

            // the card of the evening and of the morning, high in the middle
            var card = UIF.Rect("Card", go.transform, new Vector2(0, 330), new Vector2(900, 220));
            _card = card.gameObject.AddComponent<CanvasGroup>();
            _card.alpha = 0;
            _cardTitle = UIF.Text(card, "", 64, ChalkTex.White, new Vector2(0, 60), new Vector2(900, 90));
            _cardSum = InkText.On(UIF.Text(card, "", 48, ChalkTex.White, new Vector2(0, -10), new Vector2(900, 70)));
            _cardNote = UIF.Text(card, "", 32, ChalkTex.Dim, new Vector2(0, -70), new Vector2(900, 50));

            var fade = UIF.Fill("Night", go.transform);
            var img = fade.gameObject.AddComponent<Image>();
            img.color = new Color(ChalkTex.BoardColor.r, ChalkTex.BoardColor.g, ChalkTex.BoardColor.b, 1);   // the map's veil: one colour
            img.raycastTarget = false;
            _fade = fade.gameObject.AddComponent<CanvasGroup>();
            _fade.alpha = 1;   // the day comes out of the map's veil (Dawn fades it)
        }

        /// The day comes out of the veil the map went into: the room fades up.
        public IEnumerator Dawn()
        {
            for (float t = 0; t < 1; t += Time.deltaTime / 0.9f) { _fade.alpha = 1 - Mathf.SmoothStep(0, 1, t); yield return null; }
            _fade.alpha = 0;
        }

        /// Out through the door: the room goes into the veil the map comes out of.
        public IEnumerator Dusk()
        {
            for (float t = 0; t < 1; t += Time.deltaTime / 0.7f) { _fade.alpha = Mathf.SmoothStep(0, 1, t); yield return null; }
            _fade.alpha = 1;
        }

        // ------------------------------------------------------------------ every frame

        /// The cards in turn, the goals, the coins' counter jumping (every frame, from the game, before the dot is aimed).
        public void Tick()
        {
            Cards();
            Goals();
            if (_coinPulse > 0) { _coinPulse = Mathf.Max(0, _coinPulse - Time.deltaTime * 2); _coins.transform.localScale = Vector3.one * (1 + 0.35f * _coinPulse); }
        }

        /// The dot and its hint, the clock, the card fading in and out (every frame, from the game, once the dot is aimed).
        public void Draw(Object target)
        {
            DrawDot(target);
            DrawClock();
            _card.alpha = Mathf.MoveTowards(_card.alpha, Time.time < _cardUntil ? 1 : 0, Time.deltaTime * 2);
        }

        /// The little clock: a circle with the hour hand going round from eight to six; the day's number by it.
        void DrawClock()
        {
            _clock.Clear();
            _clock.Circle(Vector2.zero, 26, Day.Evening ? ChalkTex.Yellow : ChalkTex.White, 3.5f);
            float h = Day.Hour;
            float ah = (h % 12) / 12 * Mathf.PI * 2, am = (h % 1) * Mathf.PI * 2;
            _clock.Line(Vector2.zero, new Vector2(Mathf.Sin(ah), Mathf.Cos(ah)) * 14, ChalkTex.White, 3.5f);
            _clock.Line(Vector2.zero, new Vector2(Mathf.Sin(am), Mathf.Cos(am)) * 21, ChalkTex.Dim, 2.5f);
            _dayText.text = "день " + Day.Day;
        }

        void DrawDot(Object target)
        {
            _dot.Clear();
            _hint.text = "";
            var r = _game.Hands.Reading;
            _keys.text = r == null || r.stamped ? ""
                : r.tests.Count > 1 ? "цифры — ответ    ·    Tab — следующее поле    ·    Esc — положить" : "цифры — ответ    ·    Esc — положить";
            if (!_game.walker.Looking) return;
            string hint = _game.Hands.Hint(target);
            if (hint.Length > 0) _dot.Circle(Vector2.zero, 14, ChalkTex.White, 3.5f);
            else _dot.Circle(Vector2.zero, 3, ChalkTex.Dim, 4f, false);
            // F3: what the line of sight runs into (to tell why something cannot be taken)
            var kb = Keyboard.current;
            if (kb != null && kb.f3Key.wasPressedThisFrame) _debugAim = !_debugAim;
            _hint.text = _debugAim ? hint + "\n[" + _game.Pointer.AimedAt + "]" : hint;
        }

        // ------------------------------------------------------------------ the cards

        /// A card told by something else in the room (a certificate): after the others, or at once.
        public void Tell((string title, string sum, string note) c, bool now = false)
        {
            if (now) Card(c.title, c.sum, c.note, 2f);
            else _cards.Add(c);
        }

        /// A card to show once the one on screen has had its time (the goals' news, one after another).
        public void Queue((string title, string sum, string note)? c) { if (c.HasValue) _cards.Add(c.Value); }

        void Cards()
        {
            if (_cards.Count == 0 || Time.time < _cardUntil + 0.3f) return;
            var c = _cards[0];
            _cards.RemoveAt(0);
            Card(c.title, c.sum, c.note, c.title == "Все заказы выполнены" ? 0 : 3.6f);   // that one stays up till the door
            Sfx.Play("correct", 0.4f);
        }

        void Goals()
        {
            if (_goalLines[0] == null || Time.time < _goalsAt) return;
            _goalsAt = Time.time + 0.4f;
            var lines = Career.DayLines(G, G.OrdersPerDay);
            for (int i = 0; i < _goalLines.Length; i++) _goalLines[i].Set(i < lines.Count ? lines[i] : "", ChalkTex.Yellow);
        }

        /// A card high in the middle for `seconds` (0 — until the door).
        public void Card(string title, string sum, string note, float seconds)
        {
            _cardTitle.text = title;
            _cardSum.Set(sum);
            _cardNote.text = note;
            _cardUntil = seconds > 0 ? Time.time + seconds : float.PositiveInfinity;
        }

        double DayCoins => G.LessonEarned[(int)Cur.Coin];
        double DayIdeas => G.LessonEarned[(int)Cur.Obs];
        double DayJoules => G.LessonEarned[(int)Cur.J];

        /// The day's earnings, in the coins there were any of.
        public string DaySum => "+" + GameState.Fmt(DayCoins) + " " + GameState.Coin
            + (DayIdeas > 0 ? "    +" + GameState.Fmt(DayIdeas) + " " + GameState.Bulb : "") + (DayJoules > 0 ? "    +" + GameState.Fmt(DayJoules) + " Дж" : "");

        // ------------------------------------------------------------------ the coins

        /// The coins in the corner (the orders pay them), and the joules once there are any.
        public void ShowMoney()
        {
            if (_rolling) { ShowMoneyText(); return; }
            _shown = G.Cur[(int)Cur.Coin];
            _coins.Set(GameState.Fmt(G.Cur[(int)Cur.Coin]) + " " + GameState.Coin);
            _coins.Label.color = ChalkTex.Green;
            _joules.Label.enabled = G.Total[(int)Cur.J] > 0;
            _joules.Set(GameState.Fmt(G.Cur[(int)Cur.J]) + " Дж");
            _joules.Label.color = ChalkTex.Yellow;
        }

        void ShowMoneyText()
        {
            _coins.Set(GameState.Fmt(_shown) + " " + GameState.Coin);
            _coins.Label.color = ChalkTex.Green;
        }

        /// The counter jumps.
        public void Pulse() => _coinPulse = 1;

        /// From now on the counter shows the coins as they arrive, not at once: it stands at `coins` until they do.
        public void Roll(double coins)
        {
            _rolling = true;
            _shown = coins;
        }

        /// The coins of an order fly out of the customer's hands (at `world`) across the screen into the counter in the
        /// corner; each that lands adds its share, with a tick, and the counter jumps.
        public void FlyCoins(Vector3 world, double from, double to, int coins) => StartCoroutine(Fly(world, from, to, coins));

        IEnumerator Fly(Vector3 world, double from, double to, int coins)
        {
            if (coins <= 0) { _rolling = false; ShowMoney(); yield break; }
            var hud = (RectTransform)_coins.transform.parent;
            Vector3 start = _game.eye.WorldToScreenPoint(world);
            if (start.z < 0) start = new Vector3(Screen.width / 2f, Screen.height / 2f, 0);
            Vector3 end = _coins.transform.position + new Vector3(30, 0, 0);
            int n = Mathf.Clamp(coins / 3, 4, 14);
            int landed = 0;
            for (int i = 0; i < n; i++) StartCoroutine(Coin(hud, start, end, i * 0.07f, () =>
            {
                landed++;
                _shown = from + (to - from) * landed / n;
                _coinPulse = 1;
                Sfx.Play("tick", 0.5f, 1.4f + landed * 0.05f, 0, 0.01f);
                ShowMoneyText();
            }));
            for (float t = 0; t < 3 && landed < n; t += Time.deltaTime) yield return null;
            _rolling = false;
            ShowMoney();
        }

        IEnumerator Coin(RectTransform hud, Vector3 from, Vector3 to, float delay, System.Action landed)
        {
            yield return new WaitForSeconds(delay);
            var t = UIF.Text(hud, "", 56, ChalkTex.Green, Vector2.zero, new Vector2(90, 90));
            InkText.On(t).Set(GameState.Coin, ChalkTex.Green);
            var rt = t.transform;
            var side = new Vector3(Random.Range(-160f, 160f), Random.Range(80f, 220f), 0);
            for (float k = 0; k < 1; k += Time.deltaTime / 0.6f)
            {
                float e = k * k;
                rt.position = Vector3.Lerp(Vector3.Lerp(from, from + side, k), Vector3.Lerp(from + side, to, k), e);   // a curve out and in
                rt.localScale = Vector3.one * Mathf.Lerp(1.2f, 0.6f, k);
                yield return null;
            }
            Destroy(t.gameObject);
            landed();
        }
    }
}
