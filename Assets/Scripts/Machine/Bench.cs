using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChalkPhysics
{
    /// The live run (DESIGN.md, 2а): a run lasts a few seconds, and every instrument on the bench is worked by hand.
    /// The letters set the ceiling of an experiment; the hand decides how much of it is taken — a run done well pays the
    /// whole formula, a clumsy one no less than half.
    ///  · the dynamometer: a hung specimen stretches the spring with an overshoot that settles; measure the stretch —
    ///    draw a ruler from the zero mark down to where the hook is: the closer the measure, the more it gives;
    ///  · the shelf: lift the specimen up along the post by hand and let it go: it falls onto the peg — the higher it
    ///    was lifted (up to the top, the height h), the more it gives;
    ///  · the spring: pull it down by hand and let go — the specimen jumps, and the further it was pulled the higher;
    ///    at the top of the jump a chalk tick is left, and a ruler drawn from the plate up to it measures the jump;
    ///  · the pendulum: pull the weight aside and let go — the wider the swing, the more every swing pays;
    ///  · every other instrument: tap it; tapped again before it has settled, it is done in a hurry.
    /// Chalk helpers (brought by tasks) stand at the instruments: they carry specimens to empty ones, shut the shelf's
    /// doors, and run an instrument nobody touches — slowly and half well.
    /// Once a few formulas are known, the qualification exam comes to the bench: assemble a formula from memory.
    public partial class Apparatus
    {
        // ---------------- the clock ----------------
        bool Live => G.Phase == Phase.Lesson && G.TimeLeft > 0 && !_bellPending && _started;
        /// The clock waits while an instrument the player has never worked with stands untried on the bench
        /// (but not for ever: after half a minute it goes anyway).
        bool Teaching { get { if (_teachT > 30) return false; foreach (var st in G.Bench) if (StationBuilt(st) && G.IsNew(st)) return true; return false; } }
        float _teachT;
        float Elapsed => G.LevelSeconds - G.TimeLeft;

        const float RampT = 0.7f;            // how long an instrument takes to settle before it can be run well
        const float HelperWait = 1.5f;       // a helper runs an instrument nobody has touched for this long
        const float PendMax = 0.65f;
        const float PullSpring = 90f;        // how far down the spring's plate can be pulled

        float _springC, _pendPushT;
        bool _pushByHelper;
        float LiftY => ShelfY;
        float _lift0;                        // how high (0 .. 1 of the post) the last specimen was let go from
        const float FallSettleT = 1.6f;      // after the blow the weight bounces on the peg before it lies still
        float _fallSettle;
        float FallRest(Sample s) => O["fall"].y + PegTop + SampleSize(s) / 2;
        float FallTop(Sample s) => ShelfY + SampleSize(s) / 2;

        void UpdateBench(float dt)
        {
            if (G.Phase != Phase.Lesson) return;
            if (Live && Teaching) _teachT += dt;
            if (Live && !Teaching)
            {
                G.TimeLeft = Mathf.Max(0, G.TimeLeft - dt);
                G.Boiler = Mathf.Max(0, G.Boiler - G.BoilerCool * dt);
                if (G.TimeLeft <= 0)
                {   // the bell: what is running finishes, nothing new starts
                    CancelGesture();
                    _bellPending = true; _bellWait = 0;
                    CloseExam(false);
                }
            }
            foreach (var kv in _st)
            {
                var st = kv.Key; var s = kv.Value;
                if (!StationBuilt(st)) continue;
                if (Busy(st) || s.pressing) s.idle = 0; else s.idle += dt;
            }
            if (StationBuilt("dyna"))
            {   // a specimen hung on the hook (a new one, or the same one hung again) starts its stretch over
                var who = _st["dyna"].resident;
                if (who != _dynaWho) { _dynaWho = who; _dynaT = 0; } else _dynaT += dt;
            }
            _fallSettle = Mathf.Max(0, _fallSettle - dt);
            UpdateSpring(dt);
            UpdatePend(dt);
            UpdateCrew(dt);
            UpdateTasks();
            UpdateExam(dt);
            UpdateIntro(dt);
        }

        // ---------------- the start of a run: the tasks, then "Старт" ----------------
        bool _started;
        float _introT;
        RectTransform _intro;
        readonly List<RectTransform> _introLines = new List<RectTransform>();
        RectTransform _introBtn;

        void ShowIntro()
        {
            HideIntro();
            _started = false; _introT = 0;
            _intro = UIF.Rect("Intro", _fixed, Vector2.zero, new Vector2(FW, FH));
            var veil = _intro.gameObject.AddComponent<Image>();
            veil.color = new Color(ChalkTex.BoardColor.r, ChalkTex.BoardColor.g, ChalkTex.BoardColor.b, 0.55f);
            veil.raycastTarget = true;
            _intro.gameObject.AddComponent<Swallow>();
            float y0 = 150 + 30 * _tasks.Count;
            var head = UIF.Rect("Head", _intro, new Vector2(0, y0 + 40), new Vector2(1200, 80));
            UIF.Text(head, $"Эксперимент {G.Lessons + 1}", 58, ChalkTex.White, Vector2.zero, new Vector2(1200, 80));
            head.gameObject.AddComponent<CanvasGroup>().alpha = 0;
            _introLines.Add(head);
            for (int i = 0; i < _tasks.Count; i++)
            {
                var line = UIF.Rect("Task", _intro, new Vector2(0, y0 - 60 - 64 * i), new Vector2(1200, 60));
                var box = UIF.Shape(line, 395 + i);
                float tw = Mathf.Min(560, 22 * _tasks[i].text.Length / 2f + 40);
                box.Rect(new Vector2(-tw - 30, 0), new Vector2(26, 26), new Color(1, 1, 1, 0.7f), 2.5f);
                UIF.Text(line, _tasks[i].text, 38, ChalkTex.Yellow, Vector2.zero, new Vector2(1100, 56));
                line.gameObject.AddComponent<CanvasGroup>().alpha = 0;
                _introLines.Add(line);
            }
            _introBtn = UIF.Rect("Start", _intro, new Vector2(0, y0 - 100 - 64 * _tasks.Count), new Vector2(360, 90));
            UIF.Catcher(_introBtn);
            var frame = UIF.Shape(_introBtn, 399);
            frame.Rect(Vector2.zero, new Vector2(340, 80), ChalkTex.Yellow, 4.5f);
            UIF.Text(_introBtn, "▶ Старт", 44, ChalkTex.Yellow, Vector2.zero, new Vector2(340, 76));
            var c = _introBtn.gameObject.AddComponent<Clickable>();
            c.onClick = StartRun;
            c.onHover = h => _introBtn.localScale = Vector3.one * (h ? 1.06f : 1f);
            _introBtn.gameObject.AddComponent<CanvasGroup>().alpha = 0;
        }

        void UpdateIntro(float dt)
        {
            if (_intro == null) return;
            _introT += dt;
            for (int i = 0; i < _introLines.Count; i++)
            {   // the heading, then each task with a little punch, one after another
                float k = Mathf.Clamp01((_introT - 0.15f - 0.45f * i) / 0.25f);
                var cg = _introLines[i].GetComponent<CanvasGroup>();
                if (cg.alpha < 0.01f && k > 0) Sfx.Play(i == 0 ? "chalk" : "tick", 0.4f, 1f + 0.1f * i);
                cg.alpha = k;
                _introLines[i].localScale = Vector3.one * Mathf.Lerp(1.25f, 1f, k * k);
            }
            float kb = Mathf.Clamp01((_introT - 0.15f - 0.45f * _introLines.Count) / 0.3f);
            _introBtn.GetComponent<CanvasGroup>().alpha = kb;
            _introBtn.GetComponent<CanvasGroup>().blocksRaycasts = kb > 0.5f;
            if (kb >= 1) _introBtn.localScale = Vector3.one * (1 + 0.03f * Mathf.Sin(Time.time * 4));
        }

        void StartRun()
        {
            if (_started || _intro == null) return;
            _started = true;
            Sfx.Play("bell", 0.6f, 1.3f);
            HideIntro();
        }

        void HideIntro()
        {
            if (_intro != null) Destroy(_intro.gameObject);
            _intro = null; _introLines.Clear(); _introBtn = null;
        }

        /// For the bots: past the tasks, straight to the run.
        public void BotStart() { if (!_started && _intro != null) StartRun(); }

        // ---------------- what an instrument can do right now ----------------
        /// A specimen may be put on it (not while a run of it is under way).
        bool CanLoad(string st)
        {
            if (!StationBuilt(st)) return false;
            var s = _st[st];
            if (s.incoming > 0) return false;
            switch (st)
            {
                case "fall": return s.flights.Count == 0 && !s.pressing && _fallSettle <= 0;
                case "pend": return !s.pressing;
                case "arch": return s.flights.Count == 0;
                case "stand": return true;
                case "spring": return _sprPhase == 0 && !s.pressing;
                default: return s.anim <= 0 && !s.pressing;
            }
        }
        public bool Ready(string st) => CanLoad(st);

        /// Running (or waiting for its chores): it cannot be started again yet.
        bool Busy(string st)
        {
            var s = _st[st];
            if (s.incoming > 0) return true;
            switch (st)
            {
                case "fall": return s.flights.Count > 0 || s.resident == null || _fallSettle > 0;
                case "pend": return s.resident == null;
                case "arch": return s.flights.Count > 0 || s.resident == null;
                case "stand": return !CircuitReady || s.cooldown > 0 || s.anim > 0;
                case "engine": case "piston": return s.anim > 0 || s.cooldown > 0;
                case "spring": return s.resident == null || _sprPhase != 0;
                default: return s.anim > 0 || (NeedsSample(st) && s.resident == null);
            }
        }

        /// How well a tap would be done now: half when the instrument has only just stopped, all once it has settled.
        float RampQ(Station s) => 0.5f + 0.5f * Mathf.SmoothStep(0, 1, Mathf.Clamp01(s.idle / RampT));
        /// The dynamometer measures: the stretch of its spring is the weight. A hung specimen pulls it out with an
        /// overshoot that dies away; the measure is how far below the zero mark the hook is at the moment of letting go.
        const float DynaRulerX = 46f;        // the ruler's column, right of the spring
        float _dynaT, _dynaMeasureY, _dynaVis;   // seconds since the specimen was hung; where the hand is; the drawn stretch
        Sample _dynaWho;
        float DynaZeroY => O["dyna"].y + 237;
        static float DynaRest(Sample s) => 12 + 14 * Mathf.Clamp((float)s.massR, 0.5f, 3f);
        float DynaStretch()
        {
            var s = _st["dyna"];
            var smp = s.anim > 0 ? s.sample : s.resident;
            if (smp == null) return 0;
            // a slow, springy settle: half again as far on the first swing, still for good only after ~3 s
            return DynaRest(smp) * (1 - Mathf.Exp(-0.9f * _dynaT) * Mathf.Cos(4.2f * _dynaT));
        }
        /// A measure within 4 px of the truth is exact; 30 px off is half.
        static float DynaQ(float err) => 1f - 0.5f * Mathf.Clamp01((err - 4f) / 26f);
        /// The spring's squeeze while the hand pulls its plate down (0 .. 1).
        float SpringSqueeze(Station s) => s.charge;
        /// Instruments worked by a gesture rather than a tap.
        static bool Gesture(string st) => st == "fall" || st == "spring" || st == "pend" || st == "dyna";

        /// One run of an instrument, done this well (0.5 .. 1).
        bool Operate(string st, float q, bool helper = false)
        {
            if (!Live || !StationBuilt(st)) return false;
            var s = _st[st];
            if (Busy(st)) return false;
            s.q = Mathf.Clamp(q, 0.5f, 1f);
            var smp = NeedsSample(st) && st != "stand" ? s.resident : null;
            if (!Do(st, true, smp)) return false;
            s.idle = 0;
            if (!helper) { G.MarkUsed(st); Buddy.I?.Poke(); }
            return true;
        }

        /// A tap on an instrument.
        void Tap(string st)
        {
            if (!Live) return;
            var s = _st[st];
            if (Gesture(st)) { Sfx.Play("nope", 0.15f); _nudge[st] = 1; return; }   // these are pulled, not tapped
            if (Busy(st)) { Sfx.Play("nope", 0.15f); return; }
            Operate(st, RampQ(s));
        }

        // ---------------- gestures: the doors, the spring, the pendulum ----------------
        string _gesture;                     // the instrument being worked by a drag
        Vector2 _gStart;

        void BeginGesture(string st, Vector2 p)
        {
            if (!Live || !Gesture(st)) return;
            var s = _st[st];
            if ((st == "dyna" || st == "fall") && Busy(st)) return;
            if (st == "spring" && (s.resident == null || (_sprPhase > 0 && !_sprPeak))) return;   // pulled when still, measured once it has peaked
            if (st == "pend" && s.resident == null) return;
            if (st == "dyna" && (s.resident == null || s.anim > 0)) return;
            _gesture = st; _gStart = p;
            s.pressing = true; s.charge = 0;
            if (st == "dyna") _dynaMeasureY = p.y;
            if (st == "spring") _sprMeasureY = p.y;
            if (st == "fall") _liftY = FallRest(s.resident);
            if (st == "pend") { _pendTarget = _pendAngle; s.charge = Mathf.Abs(_pendAngle) / PendMax; }   // caught where it is
        }

        void MoveGesture(Vector2 p)
        {
            if (_gesture == null) return;
            var s = _st[_gesture];
            var d = p - _gStart;
            switch (_gesture)
            {
                case "fall":
                    {   // the specimen follows the hand up the post, no higher than its top
                        float lo = FallRest(s.resident), hi = FallTop(s.resident);
                        _liftY = Mathf.Clamp(p.y, lo, hi);
                        s.charge = Mathf.Clamp01((_liftY - lo) / Mathf.Max(1, hi - lo));
                        if (s.charge >= 0.97f) _liftY = hi;              // close enough to the top: it is at the top
                        break;
                    }
                case "spring":
                    if (_sprPhase == 0) s.charge = Mathf.Clamp01(-d.y / PullSpring);
                    else _sprMeasureY = p.y;
                    break;
                case "dyna": _dynaMeasureY = p.y; s.charge = Mathf.Clamp01((DynaZeroY - p.y) / 150f); break;
                case "pend":
                    {   // the weight follows the hand along its arc (eased in UpdatePend)
                        var piv = PendPivot;
                        float a = Mathf.Atan2(p.x - piv.x, piv.y - p.y);
                        _pendTarget = Mathf.Clamp(a, -PendMax, PendMax);
                        s.charge = Mathf.Abs(_pendTarget) / PendMax;
                        break;
                    }
            }
        }

        /// The hand lets go.
        void EndGesture()
        {
            if (_gesture == null) return;
            var st = _gesture; var s = _st[st];
            _gesture = null; s.pressing = false;
            switch (st)
            {
                case "fall":
                    if (s.charge < 0.12f || s.resident == null) break;      // hardly lifted: it is just put back down
                    _lift0 = s.charge; _dropY = _liftY - SampleSize(s.resident) / 2;
                    Operate("fall", s.charge >= 0.97f ? 1f : 0.5f + 0.5f * s.charge);
                    Sfx.Play("whoosh", 0.25f, 1.5f);
                    break;
                case "spring":
                    if (_sprPhase == 0) { if (s.charge >= 0.08f) LaunchSpring(s.charge); }
                    else if (_sprPeak) MeasureSpring(_sprMeasureY - O["spring"].y - SpringPlate(s.resident));
                    break;
                case "dyna":
                    {   // let go: the measure against the true stretch at this very moment
                        float measured = DynaZeroY - _dynaMeasureY;
                        if (measured < 8 || s.resident == null) break;
                        float err = Mathf.Abs(measured - DynaStretch());
                        if (Operate("dyna", DynaQ(err))) s.anim = 0.49f;   // weighed: a moment on the hook, then back to the box
                        break;
                    }
                case "pend":
                    if (s.charge < 0.1f) break;
                    _pendAngle = _pendTarget;
                    _pendAmp = Mathf.Abs(_pendAngle); _pendPhase = _pendAngle >= 0 ? 0 : Mathf.PI;
                    _prevAngle = _pendAngle; _lastRising = false;
                    G.MarkUsed("pend");
                    Sfx.Play("whoosh", 0.4f, 1.3f);
                    break;
            }
            s.charge = 0;
        }

        void EndGestureQuiet() { if (_gesture != null) { _st[_gesture].pressing = false; _st[_gesture].charge = 0; } _gesture = null; }
        void CancelGesture() { if (_gesture == null) return; EndGestureQuiet(); }
        float _pendTarget;

        // ---------------- the spring: a jump and its measure ----------------
        int _sprPhase;                       // 0 at rest, 1 in the air, 2 landed (the tick waits to be measured)
        float _sprT, _sprC, _sprApex, _sprWindow, _sprMeasureY, _sprComp, _sprWobble;
        bool _sprPeak, _sprBot;
        const float SpringWindow = 3f;       // after landing the tick waits this long for its measure
        /// Where the plate rests: the specimen on it presses it a little.
        float SpringPlate(Sample s) => 8 + 110 * (1 - (s != null ? 0.08f * Mathf.Clamp((float)s.massR, 0.5f, 2f) : 0));
        float SpringFlight => 0.9f + 0.9f * _sprC;
        /// How high the specimen is above the plate right now.
        float SpringJump() { if (_sprPhase != 1) return 0; float u = Mathf.Clamp01(_sprT / SpringFlight); return _sprApex * 4 * u * (1 - u); }

        bool LaunchSpring(float c)
        {
            var s = _st["spring"];
            if (!Live || s.resident == null || _sprPhase != 0) return false;
            _sprC = c; _sprApex = 50 + 170 * c; _sprT = 0; _sprPhase = 1; _sprPeak = false; _sprComp = c;
            s.idle = 0;
            G.MarkUsed("spring");
            Sfx.Play("boing", 0.45f, 1.1f, 0.1f, 0.1f);
            return true;
        }

        /// The hand's measure of the jump against its true height: within 4 px all of it, 30 px off half.
        void MeasureSpring(float measured)
        {
            if (measured < 8) return;
            float acc = 1f - Mathf.Clamp01((Mathf.Abs(measured - _sprApex) - 4f) / 26f);
            PaySpring(0.5f + 0.5f * _sprC * acc);
        }

        void PaySpring(float q)
        {
            var s = _st["spring"];
            s.q = Mathf.Clamp(q, 0.5f, 1f);
            if (s.resident != null) Fire("spring", O["spring"] + new Vector2(0, SpringPlate(s.resident) + _sprApex + 40), s.resident);
            _sprPhase = 0; _sprPeak = false; _sprBot = false; s.idle = 0;
        }

        void UpdateSpring(float dt)
        {
            _sprWobble = Mathf.Max(0, _sprWobble - dt * 6f);
            if (!StationBuilt("spring") || _sprPhase == 0) return;
            var s = _st["spring"];
            if (s.resident == null) { _sprPhase = 0; return; }
            if (_sprPhase == 1)
            {
                _sprT += dt;
                if (_sprT >= SpringFlight * 0.5f && !_sprPeak) { _sprPeak = true; Sfx.Play("tick", 0.3f, 1.6f); }
                if (_sprT >= SpringFlight) { _sprPhase = 2; _sprWindow = SpringWindow; _sprWobble = 6; Sfx.Play("thud", 0.25f, 1.4f); }
                return;
            }
            // landed: a bot measures at once; the hand has a few seconds, then the jump counts unmeasured (half)
            if (_sprBot) { PaySpring(0.5f + 0.5f * _sprC * 0.9f); return; }
            if (!s.pressing) _sprWindow -= dt;
            if (_sprWindow <= 0) PaySpring(0.5f);
        }

        // ---------------- the pendulum ----------------
        /// A helper's push: a nudge, never quite in time.
        bool PendPush(bool manual)
        {
            var s = _st["pend"];
            if (s.resident == null || s.pressing || Time.time - _pendPushT < 0.25f) return false;
            _pendPushT = Time.time;
            if (!_pushByHelper) G.MarkUsed("pend");
            if (_pendAmp < 0.05f) { _pendAmp = 0.25f; _pendPhase = 0; _prevAngle = _pendAmp; _lastRising = false; }
            else _pendAmp = Mathf.Min(PendMax, _pendAmp + 0.08f);
            Sfx.Play("whoosh", 0.2f, 1.4f);
            return true;
        }

        /// A damped swing; every swing pays the weight on the thread, the wider the swing the closer to all of it.
        void UpdatePend(float dt)
        {
            if (!StationBuilt("pend")) return;
            var s = _st["pend"];
            if (s.resident == null) { _pendAmp = 0; _pendAngle = Mathf.Lerp(_pendAngle, 0, dt * 4); return; }
            if (s.pressing) { _pendAngle = Mathf.Lerp(_pendAngle, _pendTarget, dt * 14f); _prevAngle = _pendAngle; return; }   // held by the hand
            if (_pendAmp > 0.02f)
            {
                _pendPhase += dt * Mathf.PI * 2 / G.SwingPeriod;
                _pendAmp *= Mathf.Exp(-dt * 0.25f);
                _pendAngle = _pendAmp * Mathf.Cos(_pendPhase);
                bool rising = _pendAngle >= _prevAngle;
                if (!_lastRising && rising && _pendAmp > 0.08f && G.TimeLeft > 0)
                {
                    s.q = 0.5f + 0.5f * Mathf.Clamp01(_pendAmp / 0.5f);
                    Fire("pend", PendPivot + PendDir * PendLen + new Vector2(0, -40), s.resident);
                    Sfx.Play("tock", 0.2f, 1f, 0.05f, 0.1f);
                }
                _lastRising = rising;
                _prevAngle = _pendAngle;
            }
            else { _pendAmp = 0; _pendAngle = Mathf.Lerp(_pendAngle, 0, dt * 4); }
        }

        // ---------------- input ----------------
        string _press;                       // the instrument the pointer went down on
        int _dragHelper = -1;
        Vector2 _dragAt;

        public void OnPointerDown(PointerEventData e)
        {
            var p = Local(e);
            _dragging = false; _press = null; _dragHelper = -1;
            _dragSample = -1; _dragFromStation = null; _dragSocket = -1;
            if (G.Phase != Phase.Lesson || _bellPending) return;
            int hi = HelperAt(p);
            if (hi >= 0) { _dragHelper = hi; _dragAt = p; Sfx.Play("chalk", 0.4f); return; }
            int si = SampleAt(p);
            if (si >= 0)
            {
                if (IsBusy(G.Samples[si])) { Sfx.Play("nope", 0.2f); return; }
                _dragSample = si; Sfx.Play("chalk", 0.4f); return;
            }
            var st = StationAt(p);
            if (st == null) return;
            var stn = _st[st];
            if (st == "stand")
            {   // a specimen can be pulled out of its socket; anywhere else the circuit is closed
                for (int i = 0; i < Sockets; i++)
                {
                    var smp = stn.sockets[i];
                    if (smp == null || Vector2.Distance(p, SocketPos(i)) > SampleSize(smp) / 2 + 12) continue;
                    _dragSample = IndexOf(smp); _dragFromStation = "stand"; _dragSocket = i; Sfx.Play("chalk", 0.4f); return;
                }
                _press = st;
                return;
            }
            // a double click on an instrument sends its specimen back to the box; any other press works the instrument
            if (stn.resident != null && st == _lastTapSt && Time.unscaledTime - _lastTapT < 0.35f) { _lastTapSt = null; SendHome(st); return; }
            _lastTapSt = st; _lastTapT = Time.unscaledTime;
            if (NeedsSample(st) && stn.resident == null && stn.incoming == 0 && !(st == "fall" && stn.flights.Count > 0)) { Sfx.Play("nope", 0.2f); _nudge[st] = 1; return; }
            _press = st;
            BeginGesture(st, p);
        }

        public void OnBeginDrag(PointerEventData e)
        {
            if (_dragHelper >= 0) { _dragging = true; return; }
            if (_gesture != null) { _dragging = true; return; }      // the gesture goes on; the specimen stays
            if (_dragSample >= 0) LiftSample();
        }

        /// The specimen leaves its instrument (or its socket) with the hand.
        void LiftSample()
        {
            PressCancel();
            if (_dragFromStation == "stand" && _dragSocket >= 0)
            {   // out of its socket: the circuit is open again
                var stn = _st["stand"]; var smp = stn.sockets[_dragSocket]; stn.sockets[_dragSocket] = null;
                if (smp != null) _busy.Remove(smp);
            }
            else if (_dragFromStation != null)
            {   // lifted off its instrument: it is free again until it lands somewhere
                var stn = _st[_dragFromStation]; var smp = stn.resident; stn.resident = null;
                if (smp != null) _busy.Remove(smp);
                if (_dragFromStation == "pend") { _pendAmp = 0; _pendAngle = 0; }
            }
            _dragging = true;
            _ghost.gameObject.SetActive(true);
            _ghost.SetAsLastSibling();
        }

        void PressCancel() { CancelGesture(); _press = null; }

        public void OnDrag(PointerEventData e)
        {
            if (!_dragging) return;
            var p = Local(e);
            if (_dragHelper >= 0) { _dragAt = p; return; }
            if (_gesture != null) { MoveGesture(p); return; }    // a gesture stays a gesture wherever the hand goes
            _ghost.anchoredPosition = p;
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (!_dragging) return;
            _dragging = false;
            var p = Local(e);
            var st = StationAt(p);
            if (_dragHelper >= 0)
            {
                AssignHelper(_dragHelper, st);
                _dragHelper = -1;
                return;
            }
            if (_gesture != null) { EndGesture(); _press = null; _dragSample = -1; _dragFromStation = null; return; }
            _ghost.gameObject.SetActive(false);
            if (_dragSample >= 0 && _dragSample < G.Samples.Count)
            {
                var sample = G.Samples[_dragSample];
                // either it flies onto the instrument, or it flies back home
                if (st == null || !Drop(st, sample, p)) Return(sample, p);
            }
            _dragSample = -1; _dragFromStation = null; _dragSocket = -1;
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (_dragging) return;
            if (_gesture != null) EndGesture();             // a press without a pull: nothing for the spring or the pendulum
            if (_press != null) Tap(_press);
            _press = null; _dragHelper = -1;
            _dragSample = -1; _dragFromStation = null; _dragSocket = -1;
        }

        string _lastTapSt;
        float _lastTapT;

        /// A double click: the specimen leaves the instrument for the box (not in the middle of a run).
        void SendHome(string st)
        {
            var s = _st[st];
            var smp = s.resident;
            if (smp == null || s.anim > 0 || s.flights.Count > 0 || s.incoming > 0 || InFlight(smp) || (st == "spring" && _sprPhase != 0)) { Sfx.Play("nope", 0.2f); return; }
            CancelGesture();
            var from = st == "pend" ? PendPivot + PendDir * (PendLen + SampleSize(smp) / 2) : LandingPos(st, smp);
            s.resident = null;
            if (st == "pend") { _pendAmp = 0; _pendAngle = 0; }
            Return(smp, from);
            Sfx.Play("chalk", 0.4f);
        }

        // ---------------- for the bots ----------------
        public bool CanWork(string st) => Live && StationBuilt(st) && !Busy(st) && (Gesture(st) || _st[st].idle >= RampT) && (st != "pend" || _pendAmp < 0.15f);
        public bool BotFeed(string st, Sample sample) { int i = IndexOf(sample); return Drop(st, sample, i >= 0 ? SamplePos(i) : new Vector2(TrayX, TrayY)); }
        /// The bot works an instrument fairly well: a reading near the mark, a well pulled spring, a wide swing.
        public bool BotWork(string st)
        {
            if (!CanWork(st)) return false;
            if (st == "fall") { var r = _st["fall"].resident; _lift0 = 0.85f; _dropY = Mathf.Lerp(FallRest(r), FallTop(r), 0.85f) - SampleSize(r) / 2; return Operate("fall", 0.925f); }
            if (st == "spring") { _sprBot = true; return LaunchSpring(0.85f); }
            if (st == "pend") { _pendAmp = 0.5f; _pendPhase = 0; _prevAngle = _pendAmp; _lastRising = false; G.MarkUsed("pend"); return true; }
            if (st == "dyna") { if (!Operate("dyna", 0.9f)) return false; _st["dyna"].anim = 0.49f; return true; }
            return Operate(st, 0.95f);
        }

        /// The bot answers the exam right most of the time.
        public void BotExam()
        {
            if (_examPanel == null) return;
            foreach (var kv in _examTiles)
            {
                if (!kv.Value.gameObject.activeSelf) continue;
                bool right = _examNeed.Contains(kv.Key) && !_examPlaced.Contains(kv.Key);
                if (right == (Random.value < 0.9f)) { ExamPick(kv.Key, kv.Value); return; }
            }
        }
        public void BotCrew() { }

        // ---------------- chalk helpers ----------------
        /// Where each helper of this run is: an instrument, or null (standing by the box).
        readonly List<string> _crew = new List<string>();
        readonly List<float> _crewArm = new List<float>();

        Vector2 HelperHome(int i) { var b = BoxRect(); return new Vector2(b.xMin - 34 - i * 38, b.yMax - 30); }
        Vector2 HelperAtStation(string st) { var r = StationRect(st); return new Vector2(r.xMin - 4, O[st].y + 24); }
        Vector2 HelperPos(int i) => _crew[i] != null && StationBuilt(_crew[i]) ? HelperAtStation(_crew[i]) : HelperHome(i);

        int HelperAt(Vector2 p)
        {
            for (int i = 0; i < _crew.Count; i++) if (Vector2.Distance(p, HelperPos(i)) < 28) return i;
            return -1;
        }

        void AssignHelper(int i, string st)
        {
            if (i < 0 || i >= _crew.Count) return;
            if (st != null && !StationBuilt(st)) st = null;
            if (st != null) for (int j = 0; j < _crew.Count; j++) if (j != i && _crew[j] == st) _crew[j] = null;   // one helper per instrument
            _crew[i] = st;
            Sfx.Play(st != null ? "tick" : "chalk", 0.4f, 1.4f);
            if (st != null) FX.I.Dust(Content, HelperAtStation(st), ChalkTex.White, 6, 120f);
        }

        /// At the start of a run the helpers walk to the instruments themselves: the newest first.
        void PlaceCrew()
        {
            int k = 0;
            for (int j = G.Bench.Count - 1; j >= 0 && k < _crew.Count; j--) { if (StationBuilt(G.Bench[j])) _crew[k++] = G.Bench[j]; }
        }

        /// What a helper does at its instrument: the chores first (a specimen for an empty instrument, the shelf's doors),
        /// then, if nobody has touched the instrument for a while, a run of it — half as well as the hand.
        void UpdateCrew(float dt)
        {
            for (int i = 0; i < _crewArm.Count; i++) _crewArm[i] = Mathf.Max(0, _crewArm[i] - dt * 3);
            if (!Live) return;
            for (int i = 0; i < _crew.Count; i++)
            {
                var st = _crew[i];
                if (st == null || !StationBuilt(st)) continue;
                var s = _st[st];
                s.helperT += dt;
                if (s.helperT < 0.8f || s.pressing) continue;
                bool done = false;
                if (st != "stand" && NeedsSample(st) && s.resident == null && s.incoming == 0 && CanLoad(st))
                {   // fetch a specimen from the box: an unmeasured one for a measuring instrument
                    var smp = G.BestSampleFor(MainFormula(st), Free);
                    if (smp != null) done = BotFeed(st, smp);
                }
                else if (st == "stand" && MissingSockets > 0)
                {
                    var smp = G.BestSampleFor(Defs.F("ohm"), Free);
                    if (smp != null) done = BotFeed(st, smp);
                }
                else if (st == "pend")
                {
                    if (s.resident != null && _pendAmp < 0.15f && s.helperT > 1.5f) { _pushByHelper = true; done = PendPush(false); _pushByHelper = false; }
                }
                else if (!Busy(st) && s.idle >= HelperWait)
                {
                    if (st == "spring") { _springC = 0.2f; done = Operate("spring", 0.5f, true); if (done) s.anim = 0.3f; }
                    else done = Operate(st, 0.5f, true);
                }
                if (done) { s.helperT = 0; _crewArm[i] = 1; }
            }
        }

        /// A little chalk stick with a face: the helpers, the teacher of new instruments, the picture of the crew.
        public static void Chalkie(ChalkShape S, Vector2 c, float k, Color col, float armL, float armR)
        {
            float w = 7 * k, h = 17 * k;
            var body = new List<Vector2>();
            for (int i = 0; i <= 20; i++)
            {   // a stick with rounded ends
                float a = i / 20f * Mathf.PI * 2;
                float x = Mathf.Cos(a) * w, y = Mathf.Sin(a) * w;
                y += y > 0 ? h - w : -(h - w);
                body.Add(c + new Vector2(x, y));
            }
            float lw = Mathf.Max(0.6f, k);
            S.Poly(body, col, 2.4f * lw, true, false);
            var dim = new Color(col.r, col.g, col.b, col.a * 0.8f);
            S.Line(c + new Vector2(-2.6f * k, 8 * k), c + new Vector2(-2.6f * k, 6 * k), col, 2.2f * lw);
            S.Line(c + new Vector2(2.6f * k, 8 * k), c + new Vector2(2.6f * k, 6 * k), col, 2.2f * lw);
            S.Arc(c + new Vector2(0, 4 * k), 2.6f * k, Mathf.PI * 1.15f, Mathf.PI * 1.85f, dim, 1.6f * lw);
            // arms: 0 hangs down, 1 is raised
            Vector2 Arm(float up) { float a = Mathf.Lerp(-Mathf.PI / 2 + 0.5f, Mathf.PI / 2 - 0.6f, up); return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 10 * k; }
            var sl = c + new Vector2(-w, 0); var sr = c + new Vector2(w, 0);
            var al = Arm(armL); var ar = Arm(armR);
            S.Line(sl, sl + new Vector2(-al.x, al.y), col, 2f * lw);
            S.Line(sr, sr + ar, col, 2f * lw);
            S.Line(c + new Vector2(-3 * k, -h), c + new Vector2(-4 * k, -h - 6 * k), col, 2f * lw);
            S.Line(c + new Vector2(3 * k, -h), c + new Vector2(4 * k, -h - 6 * k), col, 2f * lw);
        }

        void DrawCrew(ChalkShape D)
        {
            for (int i = 0; i < _crew.Count; i++)
            {
                if (_dragging && _dragHelper == i) continue;
                var at = HelperPos(i);
                float bob = Mathf.Sin(Time.time * 3 + i) * 1.5f;
                bool working = _crew[i] != null;
                Chalkie(D, at + new Vector2(0, bob), 1.1f, working ? ChalkTex.White : new Color(1, 1, 1, 0.7f), working ? _crewArm[i] : 0.1f, working ? 0.2f + 0.8f * _crewArm[i] : 0.1f);
            }
            if (_dragging && _dragHelper >= 0) Chalkie(D, _dragAt, 1.25f, ChalkTex.Yellow, 0.9f, 0.9f);
        }

        // ---------------- the rings: which instrument wants a hand ----------------
        readonly Dictionary<string, float> _nudge = new Dictionary<string, float>();
        readonly Dictionary<string, float> _emptyT = new Dictionary<string, float>();

        Vector2 RingPos(string st)
        {
            var r = StationRect(st);
            return new Vector2(r.center.x, r.yMax + 22);
        }

        void DrawRings(ChalkShape D)
        {
            if (G.Phase != Phase.Lesson) return;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 7);
            foreach (var st in G.Bench)
            {
                if (!StationBuilt(st)) continue;
                var s = _st[st];
                if (_nudge.TryGetValue(st, out var nd) && nd > 0) _nudge[st] = Mathf.Max(0, nd - Time.deltaTime * 2);
                float n = _nudge.TryGetValue(st, out var nn) ? nn : 0;
                // an empty instrument: after it has stood empty a few seconds (or the player tapped it), the place for
                // a specimen pulses — not the moment a specimen leaves, and not for a new instrument (the helper shows it)
                bool empty = st != "stand" && NeedsSample(st) && s.resident == null && s.incoming == 0 && !(st == "fall" && s.flights.Count > 0);
                _emptyT[st] = empty && !_dragging ? (_emptyT.TryGetValue(st, out var et) ? et : 0) + Time.deltaTime : 0;
                if (empty)
                {
                    if ((_emptyT[st] > 3f || n > 0) && !G.IsNew(st)) DashedBox(D, LandingPos(st, Probe), new Vector2(44 + 10 * n, 44 + 10 * n), new Color(0.45f, 0.86f, 1f, Mathf.Clamp01(_emptyT[st] - 3f) * (0.25f + 0.25f * pulse) + 0.5f * n));
                    continue;
                }
                // the instruments worked by gestures show it on themselves (the doors, the plate, the weight)
                if (Gesture(st) || st == "dyna") continue;
                if (Busy(st)) continue;
                var c = RingPos(st);
                float fill = Mathf.Clamp01(s.idle / RampT);
                var col = fill >= 1 ? ChalkTex.Yellow : new Color(1, 1, 1, 0.75f);
                D.Circle(c, 13, new Color(1, 1, 1, 0.18f), 2f, false);
                if (fill > 0.01f) D.Arc(c, 13, Mathf.PI / 2 - fill * Mathf.PI * 2, Mathf.PI / 2, col, 3.2f);
                if (fill >= 1) D.Circle(c, 5 + 2 * pulse, new Color(1f, 0.84f, 0.32f, 0.6f + 0.4f * pulse), 2.5f, false);
            }
            // the spring and the pendulum: a little arrow says which way to pull
            if (StationBuilt("spring") && Live && _sprPhase == 2 && !_st["spring"].pressing)
            {
                var c = new Vector2(O["spring"].x + DynaRulerX + 18, O["spring"].y + SpringPlate(_st["spring"].resident) + 20);
                float k = Mathf.Repeat(Time.time * 1.2f, 1f);
                D.Arrow(c + new Vector2(0, -10 + 20 * k), c + new Vector2(0, 16 + 20 * k), new Color(1f, 0.84f, 0.32f, 0.35f + 0.4f * (1 - k)), 2.5f, 8f);
            }
            if (StationBuilt("spring") && Live && !Busy("spring") && !_st["spring"].pressing)
            {
                var c = O["spring"] + new Vector2(46, 120);
                float k = Mathf.Repeat(Time.time * 1.2f, 1f);
                D.Arrow(c + new Vector2(0, 10 - 20 * k), c + new Vector2(0, -16 - 20 * k), new Color(1f, 0.84f, 0.32f, 0.35f + 0.4f * (1 - k)), 2.5f, 8f);
            }
            if (StationBuilt("fall") && Live && !Busy("fall") && !_st["fall"].pressing)
            {
                var r = _st["fall"].resident;
                var c = new Vector2(O["fall"].x - 10 - SampleSize(r) / 2 - 22, FallRest(r) + 6);
                float k = Mathf.Repeat(Time.time * 1.2f, 1f);
                D.Arrow(c + new Vector2(0, -10 + 20 * k), c + new Vector2(0, 16 + 20 * k), new Color(1f, 0.84f, 0.32f, 0.35f + 0.4f * (1 - k)), 2.5f, 8f);
            }
            if (StationBuilt("dyna") && Live && !Busy("dyna") && !_st["dyna"].pressing)
            {
                var c = new Vector2(O["dyna"].x + DynaRulerX + 18, DynaZeroY - 20);
                float k = Mathf.Repeat(Time.time * 1.2f, 1f);
                D.Arrow(c + new Vector2(0, 10 - 20 * k), c + new Vector2(0, -16 - 20 * k), new Color(1f, 0.84f, 0.32f, 0.35f + 0.4f * (1 - k)), 2.5f, 8f);
            }
            if (StationBuilt("pend") && Live && _st["pend"].resident != null && _pendAmp < 0.12f && !_st["pend"].pressing)
            {
                var c = PendPivot + new Vector2(0, -PendLen - 50);
                float k = Mathf.Repeat(Time.time * 1.2f, 1f);
                D.Arrow(c + new Vector2(10 + 24 * k, 0), c + new Vector2(36 + 24 * k, 8), new Color(1f, 0.84f, 0.32f, 0.35f + 0.4f * (1 - k)), 2.5f, 8f);
            }
            DrawCrew(D);
        }

        // ---------------- teaching a new instrument: no words, a chalk helper shows it ----------------
        void DrawTeach()
        {
            foreach (var kv in _hints) kv.Value.gameObject.SetActive(false);
            if (G.Phase != Phase.Lesson || !Live) return;
            var D = _dyn;
            float t = Time.time;
            var yc = ChalkTex.Yellow;
            foreach (var st in G.Bench)
            {
                if (!StationBuilt(st) || !G.IsNew(st)) continue;
                var s = _st[st];
                bool empty = st != "stand" && NeedsSample(st) && s.resident == null && s.incoming == 0 && !(st == "fall" && s.flights.Count > 0);
                bool sockets = st == "stand" && MissingSockets > 0;
                if (empty || sockets)
                {   // the helper carries a specimen from the box to the instrument, again and again
                    float k = Mathf.Repeat(t / 1.6f, 1f);
                    var from = SamplePos(0); var to = st == "stand" ? SocketPos(Mathf.Max(0, FreeSocket())) : LandingPos(st, Probe);
                    var p = Vector2.Lerp(from, to, Mathf.SmoothStep(0, 1, k)) + new Vector2(0, Mathf.Sin(k * Mathf.PI) * 50);
                    Chalkie(D, p + new Vector2(0, 26), 1f, new Color(1f, 0.84f, 0.32f, 0.85f), 1f, 1f);
                    DashedBox(D, p, new Vector2(30, 30), new Color(1f, 0.84f, 0.32f, 0.6f));
                    continue;
                }
                if (st == "fall")
                {   // a chalk helper lifts it up the post, and lets go
                    float k = Mathf.Repeat(t / 1.6f, 1f);
                    float up = k < 0.6f ? Mathf.SmoothStep(0, 1, k / 0.6f) : 0;
                    Chalkie(D, new Vector2(O["fall"].x - 10 - 46, Mathf.Lerp(O["fall"].y + 40, ShelfY, up)), 1.1f, yc, 0.95f, 0.95f);
                    continue;
                }
                if (st == "spring" || st == "dyna")
                {   // a chalk helper pulls it down, and lets go
                    float k = Mathf.Repeat(t / 1.4f, 1f);
                    float down = k < 0.6f ? k / 0.6f : 0;
                    var top = st == "spring" ? O["spring"] + new Vector2(52, 140) : new Vector2(O["dyna"].x + DynaRulerX + 20, DynaZeroY);
                    Chalkie(D, top + new Vector2(0, -60 * down), 1.1f, yc, 0.95f, 0.2f);
                    continue;
                }
                if (st == "pend")
                {   // the helper takes the weight aside, and lets go
                    float k = Mathf.Repeat(t / 1.6f, 1f);
                    float side = k < 0.6f ? k / 0.6f : 0;
                    var bob = PendPivot + new Vector2(Mathf.Sin(side * 0.5f), -Mathf.Cos(side * 0.5f)) * (PendLen + 30);
                    Chalkie(D, bob + new Vector2(30, 0), 1.1f, yc, 0.95f, 0.2f);
                    continue;
                }
                {   // a tap, where the ring over the instrument is
                    var at = RingPos(st) + new Vector2(46, 4);
                    bool down = Mathf.Repeat(t / 1.1f, 1f) < 0.25f;
                    Chalkie(D, at + new Vector2(0, down ? -5 : 0), 1.1f, yc, down ? 0.2f : 0.7f, 0.2f);
                    if (down) D.Circle(at + new Vector2(-46, -4), 14, new Color(1f, 0.84f, 0.32f, 0.8f), 2.5f, false);
                }
            }
        }

        // ---------------- tasks: a small goal inside the run; each one done adds a quarter to the run ----------------
        class LevelTask { public string id, text; public int need; public System.Func<int> have; public bool done; }
        readonly List<LevelTask> _tasks = new List<LevelTask>();
        readonly HashSet<Sample> _dropped = new HashSet<Sample>();
        readonly HashSet<string> _firedSt = new HashSet<string>();
        int _springFull, _swings, _exact, _circuitRuns, _tasksDone, _rankWas;
        bool _heavy, _top, _goldHere, _goldSeen, _hot;
        static double _prevJ;
        readonly List<Text> _taskTexts = new List<Text>();
        Text _rankText;

        void ResetLive()
        {
            _tasks.Clear(); _dropped.Clear(); _firedSt.Clear(); _crew.Clear(); _crewArm.Clear(); _nudge.Clear();
            _springFull = _swings = _exact = _circuitRuns = _tasksDone = 0;
            _heavy = _top = _goldSeen = _hot = false;
            _springC = 0; _pendPushT = -9; _teachT = 0; _dynaWho = null; _dynaT = 0;
            _gesture = null; _pegSink = 0; _pegHit = 0; _fallSettle = 0;
            _sprPhase = 0; _sprPeak = false; _sprBot = false; _sprComp = 0; _sprWobble = 0;
            _press = null; _dragHelper = -1;
            CloseExam(false);
            _examDone = false;
            if (G.Phase != Phase.Lesson) { _prevJ = G.LessonEarned[0]; HideIntro(); _started = false; return; }
            _rankWas = G.Rank;
            for (int i = 0; i < G.CrewSize; i++) { _crew.Add(null); _crewArm.Add(0); }
            PlaceCrew();
            _goldHere = G.Samples.Exists(x => x.golden);
            // the tasks of this run: from what is on the bench, never two of a kind
            var pool = new List<LevelTask>();
            void Add(string id, string text, int need, System.Func<int> have) => pool.Add(new LevelTask { id = id, text = text, need = need, have = have });
            bool On(string st) => StationBuilt(st);
            int n = G.Samples.Count;
            if (On("dyna")) Add("weigh", "Взвесь все образцы", n, () => G.Samples.FindAll(x => x.knowM).Count);
            if (On("fall")) { Add("drop3", "Урони 3 разных образца", Mathf.Min(3, n), () => _dropped.Count); Add("heavy", "Урони самый тяжёлый образец", 1, () => _heavy ? 1 : 0); Add("top", "Урони груз с самого верха", 1, () => _top ? 1 : 0); }
            if (On("spring")) Add("spring", "Сожми пружину до упора — 2 раза", 2, () => _springFull);
            if (On("pend")) Add("swing", "Маятник: 8 махов", 8, () => _swings);
            if (On("density")) Add("vol", "Измерь объём 3 образцов", Mathf.Min(3, n), () => G.Samples.FindAll(x => x.knowV).Count);
            if (On("stand")) Add("circuit", "Замкни схему 5 раз", 5, () => _circuitRuns);
            if (On("heater")) Add("hot", "Раскали котёл докрасна", 1, () => _hot ? 1 : 0);
            bool measure = false;
            foreach (var st in G.Bench) { var f = MainFormula(st); if (f != null && f.kind == FKind.Measure && st != "pend") measure = true; }
            if (measure) Add("exact", "3 точных замера", 3, () => _exact);
            if (G.Bench.Count >= 2) Add("all", "Опыт на каждом приборе", G.Bench.Count, () => _firedSt.Count);
            if (_goldHere) Add("gold", "Найди золотой образец", 1, () => _goldSeen ? 1 : 0);
            if (_prevJ > 0) { double goal = NiceAbove(_prevJ * 1.3); Add("energy", "Набери " + GameState.FmtPrice(goal, Cur.J), 1, () => G.LessonEarned[0] >= goal ? 1 : 0); }
            for (int k = 0; k < G.TaskSlots && pool.Count > 0; k++)
            {
                int i = Random.Range(0, pool.Count);
                _tasks.Add(pool[i]); pool.RemoveAt(i);
            }
            _examAt = G.QualificationOpen ? Random.Range(2.5f, Mathf.Max(3f, G.LevelSeconds * 0.4f)) : -1;
            ShowIntro();
        }

        int TasksDone => _tasksDone;

        static double NiceAbove(double v)
        {
            if (v < 10) return System.Math.Ceiling(v);
            double p = System.Math.Pow(10, System.Math.Floor(System.Math.Log10(v)) - 1);
            return System.Math.Ceiling(v / p) * p;
        }

        /// Every firing on the bench: the tasks watch it.
        void OnFired(FormulaDef f, Sample sample, double y, float q)
        {
            if (G.Phase != Phase.Lesson) return;
            _firedSt.Add(f.station);
            if (f.station == "fall" && sample != null)
            {
                _dropped.Add(sample);
                double max = 0; foreach (var x in G.Samples) max = System.Math.Max(max, x.massR);
                if (sample.massR >= max) _heavy = true;
                if (q >= 0.97f) _top = true;
            }
            if (f.id == "spring" && q >= 0.95f) _springFull++;
            if (f.id == "pend") _swings++;
            if (f.kind == FKind.Measure && f.station != "pend" && q >= 0.95f) _exact++;
            if (f.id == "ohm") _circuitRuns++;
            if (sample != null && sample.golden && sample.knowGold) _goldSeen = true;
        }

        void UpdateTasks()
        {
            if (G.Boiler >= 0.9f) _hot = true;
            foreach (var t in _tasks)
            {
                if (t.done || t.have() < t.need) continue;
                t.done = true; _tasksDone++;
                Sfx.Play("correct", 0.6f, 1.2f);
                FX.I.Text(_fixed, new Vector2(-560, 300), $"задание: итог +{GameState.TaskBonus * 100:0}%", ChalkTex.Yellow, 34, 1.8f);
                FX.I.Dust(_fixed, new Vector2(-760, 380 - 34 * _tasks.IndexOf(t)), ChalkTex.Yellow, 14, 200f);
                Buddy.I?.React(Buddy.Mood.Joy, 1.6f);
            }
        }

        // ---------------- the qualification exam: a formula from memory, right on the bench ----------------
        float _examAt, _examLeft;
        bool _examDone;
        FormulaDef _exam;
        readonly List<string> _examNeed = new List<string>();
        readonly List<string> _examPlaced = new List<string>();
        readonly Dictionary<string, RectTransform> _examTiles = new Dictionary<string, RectTransform>();
        RectTransform _examPanel;
        Text _examFormula;
        ChalkShape _examShape;

        void UpdateExam(float dt)
        {
            if (!Live || _examDone || _examAt < 0) return;
            if (_examPanel == null && Elapsed >= _examAt && !Teaching) OpenExam();
            if (_examPanel == null) return;
            if (_examLeft > 0)
            {
                _examLeft -= dt;
                if (_examLeft <= 0) { FX.I.Text(_fixed, new Vector2(0, 330), "не успел — звание то же", ChalkTex.Dim, 26, 1.4f); CloseExam(true); return; }
            }
            DrawExamFrame();
        }

        /// Seconds the exam waits for an answer: no limit at first, less and less with the rank.
        float ExamLimit => G.Rank < 2 ? 0 : Mathf.Max(5f, 12f - 2f * (G.Rank - 2));

        void OpenExam()
        {
            var pool = new List<FormulaDef>();
            foreach (var f in Defs.Formulas) if (G.Built(f.id) && f.station.Length > 0 && f.kind != FKind.Final) pool.Add(f);
            if (pool.Count == 0) { _examDone = true; return; }
            _exam = pool[Random.Range(0, pool.Count)];
            _examNeed.Clear(); _examPlaced.Clear(); _examTiles.Clear();
            foreach (var l in _exam.Slots()) if (!_examNeed.Contains(l)) _examNeed.Add(l);
            // the letters to choose from: the right ones and as many again from the formulas the player knows
            // (more with the rank)
            var other = new List<string>();
            foreach (var f in Defs.Formulas) if (G.Built(f.id)) foreach (var l in f.Slots()) if (!_examNeed.Contains(l) && !other.Contains(l)) other.Add(l);
            foreach (var l in G.Known) if (!_examNeed.Contains(l) && !other.Contains(l) && Defs.HasLetter(l)) other.Add(l);
            int extra = Mathf.RoundToInt(_examNeed.Count * (1 + 0.5f * G.Rank));
            var tiles = new List<string>(_examNeed);
            while (extra-- > 0 && other.Count > 0) { int i = Random.Range(0, other.Count); tiles.Add(other[i]); other.RemoveAt(i); }
            for (int i = tiles.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); var x = tiles[i]; tiles[i] = tiles[j]; tiles[j] = x; }

            _examPanel = UIF.Rect("Exam", _fixed, new Vector2(0, 360), new Vector2(Mathf.Max(520, tiles.Count * 76 + 60), 190));
            var img = _examPanel.gameObject.AddComponent<Image>();
            img.color = new Color(ChalkTex.BoardColor.r, ChalkTex.BoardColor.g, ChalkTex.BoardColor.b, 0.94f);
            img.raycastTarget = true;
            _examPanel.gameObject.AddComponent<Swallow>();       // a click on the card is not a click on the bench under it
            _examShape = UIF.Shape(_examPanel, 393);
            UIF.Text(_examPanel, $"Квалификация · {_exam.title}", 24, new Color(1f, 0.84f, 0.32f, 0.85f), new Vector2(0, 66), new Vector2(900, 34));
            _examFormula = UIF.Text(_examPanel, "", 44, ChalkTex.White, new Vector2(0, 20), new Vector2(900, 56));
            ChalkTex.Math(_examFormula);
            float x0 = -(tiles.Count - 1) * 76 / 2f;
            for (int i = 0; i < tiles.Count; i++)
            {
                var id = tiles[i];
                var tile = UIF.Rect("T", _examPanel, new Vector2(x0 + i * 76, -50), new Vector2(66, 66));
                _examTiles[id] = tile;
                UIF.Catcher(tile);
                var box = UIF.Shape(tile, 394 + i);
                box.Rect(Vector2.zero, new Vector2(60, 60), ChalkTex.White, 3f);
                var lt = UIF.Text(tile, Defs.L(id).sym, 36, ChalkTex.White, Vector2.zero, new Vector2(64, 60));
                ChalkTex.Math(lt);
                var c = tile.gameObject.AddComponent<Clickable>();
                c.onClick = () => ExamPick(id, tile);
                c.onHover = h => tile.localScale = Vector3.one * (h ? 1.1f : 1f);
            }
            _examLeft = ExamLimit;
            ShowExamFormula();
            Sfx.Play("ding", 0.5f, 1.4f);
        }

        void ShowExamFormula()
        {
            var sb = new System.Text.StringBuilder();
            var placed = new List<string>(_examPlaced);
            foreach (var p in _exam.parts)
            {
                if (!p.StartsWith("{")) { sb.Append(p.Replace("·", "")); continue; }
                var l = p.Substring(1, p.Length - 2);
                if (placed.Remove(l)) sb.Append($"<color=#8CFF99>{Defs.L(l).sym}</color>");
                else sb.Append("<color=#FFD752>?</color>");
            }
            _examFormula.text = sb.ToString();
        }

        void ExamPick(string id, RectTransform tile)
        {
            if (_examPanel == null || !Live) return;
            if (_examNeed.Contains(id) && !_examPlaced.Contains(id))
            {
                // a letter that appears twice in the formula fills both of its places
                foreach (var p in _exam.parts) if (p == "{" + id + "}") _examPlaced.Add(id);
                tile.gameObject.SetActive(false);
                Sfx.Play("tick", 0.5f, 1.3f);
                ShowExamFormula();
                if (_examNeed.TrueForAll(_examPlaced.Contains))
                {
                    G.RankUp();
                    Sfx.Play("correct", 0.8f, 1.2f);
                    FX.I.Text(_fixed, new Vector2(0, 300), $"{Cap(G.RankName)} ×{G.RankMult:0.##}", ChalkTex.Yellow, 40, 2.2f);
                    FX.I.Dust(_fixed, new Vector2(0, 360), ChalkTex.Yellow, 24, 260f);
                    Buddy.I?.React(Buddy.Mood.Wow, 2f);
                    CloseExam(true);
                }
                return;
            }
            // a mistake: the rank steps down
            G.RankDown();
            Sfx.Play("nope", 0.7f);
            FX.I.Text(_fixed, new Vector2(0, 300), $"ошибка — {G.RankName} ×{G.RankMult:0.##}", ChalkTex.Red, 34, 2f);
            Buddy.I?.React(Buddy.Mood.Oops, 1.6f);
            CloseExam(true);
        }

        static string Cap(string s) => s.Length > 0 ? char.ToUpper(s[0]) + s.Substring(1) : s;

        void CloseExam(bool done)
        {
            if (_examPanel != null) Destroy(_examPanel.gameObject);
            _examPanel = null;
            if (done) _examDone = true;
        }

        void DrawExamFrame()
        {
            _examShape.Clear();
            var size = _examPanel.sizeDelta;
            _examShape.Rect(Vector2.zero, size - new Vector2(8, 8), new Color(1f, 0.84f, 0.32f, 0.8f), 3f);
            if (ExamLimit > 0)
            {
                float k = Mathf.Clamp01(_examLeft / ExamLimit);
                _examShape.Line(new Vector2(-size.x / 2 + 20, -size.y / 2 + 12), new Vector2(-size.x / 2 + 20 + (size.x - 40) * k, -size.y / 2 + 12), k < 0.3f ? ChalkTex.Red : ChalkTex.Yellow, 3f);
            }
        }

        // ---------------- the head of the screen: the clock, the rank, the tasks, the guest ----------------
        void DrawClock()
        {
            float total = G.LevelSeconds, left = G.TimeLeft;
            bool waiting = Teaching && Live;
            _hudText.text = $"эксперимент {G.Lessons + 1}   ·   {Mathf.CeilToInt(left)} с";
            _hudText.color = waiting ? new Color(1, 1, 1, 0.45f) : left <= 3 ? ChalkTex.Yellow : ChalkTex.White;
            // the clock's chalk bar burns down from the right
            float x0 = -900, w = 420, y = 458;
            _hud.Line(new Vector2(x0, y), new Vector2(x0 + w, y), new Color(1, 1, 1, 0.15f), 3f);
            float k = Mathf.Clamp01(left / Mathf.Max(1, total));
            if (k > 0) _hud.Line(new Vector2(x0, y), new Vector2(x0 + w * k, y), waiting ? new Color(1, 1, 1, 0.4f) : left <= 3 ? ChalkTex.Yellow : ChalkTex.White, 4f);

            if (_rankText == null)
            {
                _rankText = UIF.Text(_fixed, "", 24, ChalkTex.Yellow, new Vector2(-560, 428), new Vector2(680, 32), TextAnchor.MiddleLeft);
                for (int i = 0; i < 3; i++) _taskTexts.Add(UIF.Text(_fixed, "", 24, ChalkTex.White, new Vector2(-530, 380 - 34 * i), new Vector2(620, 32), TextAnchor.MiddleLeft));
            }
            bool showRank = G.QualificationOpen || G.Rank > 0;
            _rankText.gameObject.SetActive(showRank);
            if (showRank) _rankText.text = $"{Cap(G.RankName)} ×{G.RankMult:0.##}" + (G.RankToNext > 0 ? $"   · до следующего звания: {G.RankToNext}" : "");
            for (int i = 0; i < _taskTexts.Count; i++)
            {
                var t = _taskTexts[i];
                bool on = i < _tasks.Count && _started;
                t.gameObject.SetActive(on);
                if (!on) continue;
                var task = _tasks[i];
                int have = Mathf.Min(task.need, task.have());
                t.text = task.text + (task.need > 1 && !task.done ? $"   {have}/{task.need}" : "");
                t.color = task.done ? ChalkTex.Green : new Color(1, 1, 1, 0.85f);
                var box = new Vector2(-880, 380 - 34 * i);
                _hud.Rect(box, new Vector2(20, 20), task.done ? ChalkTex.Green : new Color(1, 1, 1, 0.6f), 2.2f);
                if (task.done) { _hud.Line(box + new Vector2(-7, 0), box + new Vector2(-2, -6), ChalkTex.Green, 3f); _hud.Line(box + new Vector2(-2, -6), box + new Vector2(10, 10), ChalkTex.Green, 3f); }
            }
        }

        void HideBenchHud()
        {
            if (_rankText != null) _rankText.gameObject.SetActive(false);
            foreach (var t in _taskTexts) t.gameObject.SetActive(false);
        }

        /// The results card: what the tasks brought and where the exam left the rank.
        void SummaryRows(List<(string left, string right, Color col, int size)> rows)
        {
            if (_tasks.Count > 0) rows.Add(($"Задания: {_tasksDone} из {_tasks.Count}", _tasksDone > 0 ? $"итог +{_tasksDone * GameState.TaskBonus * 100:0}%" : "—", ChalkTex.Yellow, 29));
            if (G.Rank != _rankWas) rows.Add(("Квалификация", $"{G.RankName} ×{G.RankMult:0.##}", G.Rank > _rankWas ? ChalkTex.Green : ChalkTex.Red, 29));
        }

        static string Helpers(int n) => n % 10 == 1 && n % 100 != 11 ? "мелок" : n % 10 >= 2 && n % 10 <= 4 && (n % 100 < 12 || n % 100 > 14) ? "мелка" : "мелков";
    }
}
