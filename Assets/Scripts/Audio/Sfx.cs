using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChalkPhysics
{
    /// Procedurally synthesized sound effects: cozy, a little silly. A few that synthesis does badly (paper, a spring,
    /// water, a wooden cube picked up) are recorded ones from Resources/Sfx (CC0, see LICENSE.txt there): "paper_2.ogg"
    /// is one of the set "paper", played at random, and a set wins over a synthesized sound of the same name.
    public class Sfx : MonoBehaviour
    {
        public static Sfx I;
        public static bool Muted;

        const int Rate = 44100;
        readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, List<AudioClip>> _sets = new Dictionary<string, List<AudioClip>>();
        readonly Dictionary<string, float> _last = new Dictionary<string, float>();
        readonly List<AudioSource> _pool = new List<AudioSource>();
        System.Random _rnd = new System.Random(5);

        public static void Play(string name, float vol = 1f, float pitch = 1f, float pitchVar = 0.06f, float minGap = 0.045f)
        {
            if (I != null) I.PlayInternal(name, vol, pitch, pitchVar, minGap);
        }

        void Awake()
        {
            I = this;
            for (int i = 0; i < 20; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0;
                _pool.Add(s);
            }
            Build();
            BuildOdd();
            foreach (var c in Resources.LoadAll<AudioClip>("Sfx"))
            {
                int cut = c.name.LastIndexOf('_');
                string set = cut > 0 ? c.name.Substring(0, cut) : c.name;
                if (!_sets.TryGetValue(set, out var list)) _sets[set] = list = new List<AudioClip>();
                list.Add(c);
            }
        }

        void PlayInternal(string name, float vol, float pitch, float pitchVar, float minGap)
        {
            AudioClip clip = null;
            if (_sets.TryGetValue(name, out var set)) clip = set[UnityEngine.Random.Range(0, set.Count)];
            if (Muted || (clip == null && !_clips.TryGetValue(name, out clip))) return;
            float now = Time.unscaledTime;
            if (_last.TryGetValue(name, out var t) && now - t < minGap) return;
            _last[name] = now;
            AudioSource src = null;
            foreach (var s in _pool) if (!s.isPlaying) { src = s; break; }
            if (src == null) return;
            src.clip = clip;
            src.volume = vol * 0.8f;
            src.pitch = pitch * (1f + UnityEngine.Random.Range(-pitchVar, pitchVar));
            src.Play();
        }

        // ---------------- synthesis ----------------

        float Noise() => (float)(_rnd.NextDouble() * 2 - 1);

        AudioClip Make(string name, float seconds, Func<float, float> f)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)Rate), -1f, 1f);
            // tiny fade-in/out against clicks
            int fade = Mathf.Min(200, n / 4);
            for (int i = 0; i < fade; i++) { float k = i / (float)fade; data[i] *= k; data[n - 1 - i] *= k; }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            _clips[name] = clip;
            return clip;
        }

        static float Sin(float ph) => Mathf.Sin(ph * Mathf.PI * 2);
        static float Env(float t, float attack, float decay) => t < attack ? t / attack : Mathf.Exp(-(t - attack) / decay);

        void Build()
        {
            // chalk scratch: filtered noise with jittery amplitude
            float lp = 0, hp = 0;
            Make("chalk", 0.16f, t =>
            {
                float n = Noise();
                lp += (n - lp) * 0.35f;
                hp = n - lp;
                float amp = Env(t, 0.01f, 0.05f) * (0.6f + 0.4f * Sin(t * 90));
                return hp * amp * 0.35f;
            });

            // soft wooden tick
            Make("tick", 0.08f, t => (Sin(t * 1400) * 0.5f + Sin(t * 2300) * 0.25f) * Env(t, 0.001f, 0.012f) * 0.6f);

            // ball landing thud
            Make("thud", 0.22f, t => (Sin(t * (140 - 60 * t)) * 0.8f + Noise() * 0.25f * Env(t, 0.001f, 0.01f)) * Env(t, 0.002f, 0.05f) * 0.7f);

            // payout ding (two soft partials)
            Make("ding", 0.35f, t => (Sin(t * 1318) * 0.5f + Sin(t * 1976) * 0.3f + Sin(t * 2637) * 0.1f) * Env(t, 0.003f, 0.09f) * 0.35f);

            // spring boing: pitch wobbles
            Make("boing", 0.6f, t =>
            {
                float f = 190 + 120 * Mathf.Exp(-t * 5) * Mathf.Sin(t * 45);
                return Sin(PhaseInt("boing", t, f)) * Env(t, 0.005f, 0.2f) * 0.5f;
            });

            // bowling strike: clatter of clacks + rumble
            Make("strike", 0.7f, t =>
            {
                float clack = 0;
                for (int k = 0; k < 7; k++)
                {
                    float t0 = k * 0.045f + (k % 3) * 0.013f;
                    if (t > t0) clack += Sin((t - t0) * (900 + k * 170)) * Env(t - t0, 0.001f, 0.02f);
                }
                return (clack * 0.35f + Noise() * 0.15f * Env(t, 0.002f, 0.15f)) * 0.8f;
            });
            Make("knock", 0.12f, t => (Sin(t * 700) * 0.4f + Noise() * 0.2f) * Env(t, 0.001f, 0.025f) * 0.6f);

            // rubber duck squeak
            Make("squeak", 0.32f, t =>
            {
                float f = 1100 + 500 * Mathf.Sin(Mathf.Min(1, t / 0.3f) * Mathf.PI) + 60 * Mathf.Sin(t * 180);
                float ph = PhaseInt("squeak", t, f);
                float saw = (ph % 1f) * 2 - 1;
                return (saw * 0.35f + Sin(ph) * 0.4f) * Env(t, 0.02f, 0.12f) * 0.45f;
            });

            // splash
            float lp2 = 0;
            Make("splash", 0.45f, t => { lp2 += (Noise() - lp2) * 0.25f; return lp2 * Env(t, 0.005f, 0.12f) * 1.2f; });

            // whoosh
            float lp3 = 0;
            Make("whoosh", 0.5f, t => { float k = 0.03f + 0.3f * Mathf.Sin(t / 0.5f * Mathf.PI); lp3 += (Noise() - lp3) * k; return lp3 * Mathf.Sin(t / 0.5f * Mathf.PI) * 0.9f; });

            // correct: happy arpeggio C-E-G-C
            Make("correct", 0.7f, t =>
            {
                float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
                float s = 0;
                for (int k = 0; k < 4; k++)
                {
                    float t0 = k * 0.07f;
                    if (t > t0) s += (Sin((t - t0) * notes[k]) + 0.3f * Sin((t - t0) * notes[k] * 2)) * Env(t - t0, 0.005f, 0.18f);
                }
                return s * 0.22f;
            });

            // placed letter: short chalk tap + tone
            Make("place", 0.18f, t => (Sin(t * 880) * 0.4f * Env(t, 0.002f, 0.05f) + Noise() * 0.15f * Env(t, 0.001f, 0.02f)) * 0.8f);

            // wrong: comedic "wah-wah"
            Make("wrong", 0.55f, t =>
            {
                float f = t < 0.25f ? 330 : 262;
                float lt = t < 0.25f ? t : t - 0.25f;
                float wob = 1 + 0.04f * Mathf.Sin(t * 40);
                float ph = PhaseInt("wrong", t, f * wob);
                float sq = Mathf.Sign(Sin(ph)) * 0.25f + Sin(ph) * 0.3f;
                return sq * Env(lt, 0.01f, 0.12f) * 0.35f;
            });

            // upgrade blip
            Make("up", 0.1f, t => Sin(PhaseInt("up", t, 600 + 6000 * t)) * Env(t, 0.002f, 0.04f) * 0.3f);

            // denied / not enough energy
            Make("nope", 0.14f, t => Sin(PhaseInt("nope", t, 220 - 400 * t)) * Env(t, 0.003f, 0.05f) * 0.35f);

            // electric zap
            Make("zap", 0.18f, t => (Mathf.Sign(Sin(t * 120 + Noise() * 0.3f)) * 0.3f + Noise() * 0.4f) * Env(t, 0.001f, 0.04f) * 0.3f);

            // steam hiss
            float lp4 = 0;
            Make("hiss", 0.5f, t => { float n = Noise(); lp4 += (n - lp4) * 0.6f; return (n - lp4) * Env(t, 0.05f, 0.18f) * 0.5f; });

            // pendulum tock
            Make("tock", 0.1f, t => (Sin(t * 520) * 0.5f + Sin(t * 1300) * 0.2f) * Env(t, 0.001f, 0.02f) * 0.5f);

            // bell for reveals / new module
            Make("bell", 1.4f, t => (Sin(t * 660) * 0.5f + Sin(t * 1650) * 0.25f + Sin(t * 2640) * 0.12f + Sin(t * 3960) * 0.06f) * Env(t, 0.003f, 0.35f) * 0.35f);

            // revolution: warm swelling chord
            Make("revolution", 3f, t =>
            {
                float[] f = { 130.8f, 196f, 261.6f, 329.6f, 392f };
                float s = 0;
                for (int k = 0; k < f.Length; k++) s += Sin(t * f[k] * (1 + 0.002f * k)) / (k + 1.5f);
                float env = Mathf.Min(1, t / 1.2f) * Mathf.Min(1, (3 - t) / 0.8f);
                return s * env * 0.35f;
            });

            // collapse drone: slowly rising, then silence
            Make("collapse", 7f, t =>
            {
                float f = 55 * Mathf.Pow(2, t / 2.2f);
                float ph = PhaseInt("collapse", t, f);
                float s = Sin(ph) * 0.5f + Sin(ph * 1.5f) * 0.25f + Sin(ph * 2.01f) * 0.2f;
                float env = Mathf.Min(1, t / 2f) * (t > 6.4f ? Mathf.Max(0, (7 - t) / 0.6f) : 1);
                return s * env * 0.3f;
            });

            // big bang / final pop
            Make("bang", 2.5f, t => (Noise() * 0.6f * Env(t, 0.002f, 0.4f) + Sin(t * (60 - 20 * t)) * 0.8f * Env(t, 0.002f, 0.6f)) * 0.5f);

            // click on a button
            Make("click", 0.06f, t => (Sin(t * 1800) * 0.5f + Noise() * 0.2f) * Env(t, 0.001f, 0.01f) * 0.5f);

            // balloon pop-in
            Make("balloon", 0.3f, t => Sin(PhaseInt("balloon", t, 300 + 900 * t)) * Env(t, 0.01f, 0.08f) * 0.3f);

            // ---- the 3D lab's order desk: the bureaucracy has to feel good in the hands ----
            // a rubber stamp coming down on paper on a wooden desk: a deep soft thump, the paper's crisp slap, the desk's
            // knock under it, and the little tick of the stamp lifting off
            float lp5 = 0;
            Make("stamp", 0.4f, t =>
            {
                float n = Noise();
                lp5 += (n - lp5) * 0.5f;
                float thump = Sin(PhaseInt("stamp", t, 120 - 70 * Mathf.Min(1, t * 8))) * Env(t, 0.0015f, 0.06f);
                float slap = (n - lp5) * Env(t, 0.0004f, 0.009f);
                float knock = (Sin(t * 410) * 0.6f + Sin(t * 1020) * 0.3f) * Env(t, 0.001f, 0.035f);
                float lift = t > 0.19f ? (Sin((t - 0.19f) * 2400) * 0.5f + Noise() * 0.3f) * Env(t - 0.19f, 0.001f, 0.008f) : 0;
                return (thump * 0.95f + slap * 0.7f + knock * 0.45f + lift * 0.25f) * 0.85f;
            });
            // the lid of the dispatch box clapping shut
            Make("lid", 0.16f, t => (Sin(t * 760) * 0.5f + Sin(t * 1530) * 0.2f + Noise() * 0.35f * Env(t, 0.0005f, 0.006f)) * Env(t, 0.001f, 0.03f) * 0.7f);
            // a big round button pressed home: a click and a soft clunk
            Make("press", 0.14f, t => (Sin(t * 2100) * 0.35f * Env(t, 0.0005f, 0.006f) + Sin(t * (210 - 300 * t)) * 0.7f * Env(t, 0.002f, 0.035f)) * 0.7f);
            // the pneumatic tube taking the box: a suck that rises in pitch and rushes away up the pipe
            float lp6 = 0, bp6 = 0;
            Make("tube", 1.1f, t =>
            {
                float n = Noise();
                float k = Mathf.Lerp(0.03f, 0.45f, Mathf.Clamp01(t / 0.7f));
                lp6 += (n - lp6) * k;
                bp6 += (lp6 - bp6) * 0.08f;
                float rush = (lp6 - bp6) * Mathf.Sin(Mathf.Clamp01(t / 1.1f) * Mathf.PI) * 1.6f;
                float pop = Sin(PhaseInt("tubepop", t, 70 + 40 * t)) * Env(t, 0.004f, 0.08f) * 0.8f;
                float whistle = Sin(PhaseInt("tubewh", t, 500 + 1400 * t * t)) * 0.06f * Mathf.Sin(Mathf.Clamp01(t / 1.1f) * Mathf.PI);
                return (rush + pop + whistle) * 0.7f;
            });
            // the reward: coins tumbling out one after another, bright and a little uneven
            Make("coins", 0.9f, t =>
            {
                float[] at = { 0, 0.07f, 0.13f, 0.21f, 0.27f, 0.36f, 0.44f };
                float[] f = { 2900, 3350, 3100, 3650, 2750, 3450, 3900 };
                float s = 0;
                for (int i = 0; i < at.Length; i++)
                {
                    float u = t - at[i];
                    if (u < 0) continue;
                    s += (Sin(u * f[i]) + Sin(u * f[i] * 2.76f) * 0.5f + Sin(u * f[i] * 5.4f) * 0.2f) * Env(u, 0.0008f, 0.07f) * (1 - i * 0.08f);
                }
                return s * 0.22f;
            });
        }

        // ---- the odd measurements of the 3D lab (DESIGN.md 2б, «Крейзи-измерения») ----
        void BuildOdd()
        {
            // a wet lick: a soft noise swelling and a little smack
            float a1 = 0;
            Make("lick", 0.35f, t =>
            {
                a1 += (Noise() - a1) * (0.08f + 0.25f * t);
                float smack = t > 0.26f ? (Sin((t - 0.26f) * 900) * 0.5f + Noise() * 0.3f) * Env(t - 0.26f, 0.001f, 0.012f) : 0;
                return a1 * Mathf.Sin(Mathf.Clamp01(t / 0.26f) * Mathf.PI) * 0.9f + smack * 0.5f;
            });
            // sour: a squeaky rising «ии»
            Make("sour", 0.45f, t => (Sin(PhaseInt("sour", t, 700 + 900 * t)) * 0.5f + Sin(PhaseInt("sour2", t, 1400 + 1800 * t)) * 0.15f) * Env(t, 0.02f, 0.18f) * 0.4f);
            // bitter: a low wobbly «бе-е»
            Make("bleh", 0.5f, t =>
            {
                float f = 170 - 60 * t + 12 * Mathf.Sin(t * 50);
                float ph = PhaseInt("bleh", t, f);
                return (Sin(ph) * 0.5f + Sin(ph * 2) * 0.3f + Sin(ph * 3) * 0.15f) * Env(t, 0.02f, 0.2f) * 0.5f;
            });
            // umami: a pleased low «мммм»
            Make("mmm", 0.8f, t =>
            {
                float ph = PhaseInt("mmm", t, 150 + 25 * Mathf.Sin(t * 5));
                return (Sin(ph) * 0.6f + Sin(ph * 2) * 0.2f) * Mathf.Min(1, t / 0.08f) * Mathf.Min(1, (0.8f - t) / 0.3f) * 0.45f;
            });
            // the light switch: a crisp plastic click
            Make("switch", 0.08f, t => (Sin(t * 3100) * 0.4f + Noise() * 0.5f) * Env(t, 0.0005f, 0.006f) * 0.8f);
            // the little bell on the rod: two bright tings
            Make("tinkle", 0.6f, t =>
            {
                float s = 0;
                foreach (float t0 in new[] { 0f, 0.12f })
                    if (t > t0) s += (Sin((t - t0) * 2350) + Sin((t - t0) * 5180) * 0.4f) * Env(t - t0, 0.001f, 0.12f);
                return s * 0.3f;
            });
            // the reel: a ratchet's quick clicks
            Make("reel", 0.6f, t => { float u = t * 28 % 1f; return (Sin(u * 60) * 0.4f + Noise() * 0.3f) * Mathf.Exp(-u * 30) * 0.6f; });
            // the cat's purr: a low rumble that pulses with its breath
            float lp7 = 0;
            Make("purr", 2.4f, t =>
            {
                lp7 += (Noise() - lp7) * 0.04f;
                float pulse = 0.55f + 0.45f * Mathf.Sin(t * 26) * Mathf.Sin(t * 26);
                float breath = 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(t * 2.6f));
                return lp7 * pulse * breath * Mathf.Min(1, t / 0.2f) * Mathf.Min(1, (2.4f - t) / 0.4f) * 3f;
            });
            // a short meow: a rising and falling «мя-у»
            Make("meow", 0.5f, t =>
            {
                float f = 550 + 350 * Mathf.Sin(Mathf.Clamp01(t / 0.45f) * Mathf.PI);
                float ph = PhaseInt("meow", t, f);
                return (Sin(ph) * 0.45f + Sin(ph * 2) * 0.25f + Sin(ph * 3) * 0.1f) * Env(t, 0.03f, 0.2f) * 0.45f;
            });
            // a yawn: a long falling sigh
            float lp8 = 0;
            Make("yawn", 1.1f, t =>
            {
                lp8 += (Noise() - lp8) * 0.15f;
                float ph = PhaseInt("yawn", t, 420 - 260 * t);
                return (Sin(ph) * 0.25f + lp8 * 0.4f) * Mathf.Sin(Mathf.Clamp01(t / 1.1f) * Mathf.PI) * 0.6f;
            });
            // the sample rings its note (do, played higher for the others): a round bell-like tone that hangs on
            // the same voice as the xylophone's bar (so a note is matched by ear, not told apart by its timbre), ringing longer
            Make("chime", 2.4f, t => (Sin(t * 523.25f) + Sin(t * 2093f) * 0.25f + Noise() * 0.1f * Env(t, 0.0005f, 0.004f)) * Env(t, 0.002f, 0.9f) * 0.4f);
            // a xylophone bar (do, played higher for the others): bright and short
            Make("xylo", 2.0f, t => (Sin(t * 523.25f) + Sin(t * 2093f) * 0.25f + Noise() * 0.1f * Env(t, 0.0005f, 0.004f)) * Env(t, 0.002f, 0.7f) * 0.4f);
            // a window pane shattering: a crack, a burst of glass, tinkling pieces after it
            Make("crash", 1.2f, t =>
            {
                float s = Noise() * Env(t, 0.001f, 0.08f) * 0.8f + Sin(t * 180) * Env(t, 0.001f, 0.03f) * 0.5f;
                for (int k = 0; k < 9; k++)
                {
                    float t0 = 0.05f + k * 0.09f + (k % 3) * 0.02f;
                    if (t > t0) s += Sin((t - t0) * (3200 + k * 470)) * Env(t - t0, 0.001f, 0.06f) * 0.25f;
                }
                return s * 0.7f;
            });
            // a giggle: «хи-хи-хи», quick high blips
            Make("giggle", 0.55f, t =>
            {
                float u = t / 0.13f;
                int k = (int)u;
                if (k > 3) return 0;
                float f = 780 - k * 40;
                float lt = (u - k) * 0.13f;
                return (Sin(lt * f) * 0.5f + Sin(lt * f * 2.2f) * 0.2f + Noise() * 0.15f) * Env(lt, 0.006f, 0.035f) * 0.5f;
            });
        }

        // integrates frequency over time for sweeps (phase in cycles)
        readonly Dictionary<string, (float t, float ph)> _phase = new Dictionary<string, (float, float)>();
        float PhaseInt(string key, float t, float freq)
        {
            if (!_phase.TryGetValue(key, out var s) || t < s.t) s = (0, 0);
            float ph = s.ph + (t - s.t) * freq;
            _phase[key] = (t, ph);
            return ph;
        }
    }
}
