using UnityEngine;
using UnityEngine.UI;

namespace ChalkPhysics.Lab3D
{
    /// The calculator on the weighing table (DESIGN.md 2б, «Ванночка и плотность»): it comes with the density. Its keys
    /// are pressed with the dot and the left button: digits, the comma, ÷ × − +, = and C. Worked left to right, as a
    /// pocket calculator does; the display shows what is typed or the result.
    public class Calculator3D : MonoBehaviour
    {
        public Transform display;       // its face: the writing goes on it
        Text _shown;
        string _entry = "";
        double _acc;
        string _op = "";
        bool _result;

        void Start()
        {
            // the writing: one canvas lying on the calculator's top, the display's number and every key's label
            var rt = Art3D.WorldCanvas("Writing", transform);
            const float px = 0.0004f;   // metres a canvas unit
            rt.sizeDelta = new Vector2(400, 600);
            rt.localScale = Vector3.one * px;
            rt.localRotation = Quaternion.Euler(90, 0, 0);
            rt.localPosition = Vector3.up * 0.029f;   // just over the keys' tops
            var d = display.localPosition;
            _shown = UIF.Text(rt, "0", 56, ChalkTex.Green, new Vector2(0, d.z / px), new Vector2(340, 70), TextAnchor.MiddleRight);
            foreach (var k in GetComponentsInChildren<CalcKey>())
            {
                var p = k.transform.localPosition;
                UIF.Text(rt, k.key, 40, ChalkTex.White, new Vector2(p.x / px, p.z / px), new Vector2(80, 60));
                k.calc = this;
            }
            Show();
        }

        public void Press(CalcKey key)
        {
            string k = key.key;
            Sfx.Play("tick", 0.45f, 1.5f);
            StartCoroutine(Push(key.transform));
            if (k == "C") { _entry = ""; _acc = 0; _op = ""; _result = false; }
            else if (k == "tg")
            {   // the tangent of what is shown, in degrees (μ = tg α)
                double x = _entry.Length > 0 ? Entry : _acc;
                _acc = System.Math.Tan(x * System.Math.PI / 180);
                _entry = ""; _op = ""; _result = true;
            }
            else if (k == "=") { Apply(); _op = ""; _result = true; _entry = ""; }
            else if ("÷×−+".Contains(k)) { if (_entry.Length > 0 || _result) Apply(); _op = k; _entry = ""; _result = false; }
            else
            {
                if (_result) { _acc = 0; _result = false; }
                if (k == ",") { if (!_entry.Contains(",")) _entry += _entry.Length == 0 ? "0," : ","; }
                else if (_entry.Length < 9) _entry += k;
            }
            Show();
        }

        double Entry => _entry.Length > 0 && double.TryParse(_entry.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;

        void Apply()
        {
            double b = Entry;
            if (_entry.Length == 0 && _op.Length > 0) return;
            _acc = _op switch
            {
                "÷" => b == 0 ? double.NaN : _acc / b,
                "×" => _acc * b,
                "−" => _acc - b,
                "+" => _acc + b,
                _ => _entry.Length > 0 ? b : _acc,
            };
        }

        void Show()
        {
            if (_shown == null) return;
            if (_entry.Length > 0) _shown.text = _entry;
            else if (double.IsNaN(_acc)) _shown.text = "ошибка";
            else _shown.text = _acc.ToString("0.###", System.Globalization.CultureInfo.GetCultureInfo("ru-RU"));
        }

        System.Collections.IEnumerator Push(Transform t)
        {
            var rest = t.localPosition;
            t.localPosition = rest + Vector3.down * 0.004f;
            yield return new WaitForSeconds(0.09f);
            t.localPosition = rest;
        }
    }
}
