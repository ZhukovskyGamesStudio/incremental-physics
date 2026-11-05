using UnityEngine;

namespace ChalkPhysics.Lab3D
{
    /// The light of the room (DESIGN.md 2б, «Крейзи-измерения»): switched off by the switch at the door, the room goes
    /// black and the eye sees nothing but what glows — faint green chalk on black, the chalk outlines greenish and faint.
    /// The player's own body is never seen by the eye (only the mirror sees it).
    public class LabLights : MonoBehaviour
    {
        Lab3DGame _game;
        Material _lines;   // the chalk outlines' material
        int _litMask;
        Color _litBg, _litLine;

        /// The light is switched off: the eye sees nothing but what glows.
        public bool Dark { get; private set; }

        public void Init(Lab3DGame game) => _game = game;

        /// In the morning: what the eye sees with the light on.
        public void Setup()
        {
            var eye = _game.eye;
            _litMask = eye.cullingMask & ~(1 << Lab3DGame.BodyLayer);
            eye.cullingMask = _litMask;
            _litBg = eye.backgroundColor;
            _lines = _game.linesMat;
            if (_lines != null) _litLine = _lines.GetColor("_Line");
        }

        public void Toggle() => Set(!Dark);

        public void Set(bool on)
        {
            if (Dark == on) return;
            Dark = on;
            Sfx.Play("switch", 0.8f, on ? 0.9f : 1.05f, 0);
            var eye = _game.eye;
            eye.cullingMask = on ? 1 << Lab3DGame.GlowLayer : _litMask;
            eye.backgroundColor = on ? Color.black : _litBg;
            if (_lines != null) _lines.SetColor("_Line", on ? new Color(0.55f, 1f, 0.62f, 0.55f) : _litLine);
            foreach (var s in _game.samples) Shine(s);
        }

        /// A glowing sample in the dark: faint green chalk on black. In the light it looks like any other.
        public void Shine(Sample3D s)
        {
            var r = s.GetComponent<Renderer>();
            if (r == null || r.sharedMaterial == null) return;
            bool glow = Dark && s.data != null && s.data.glows;
            Art3D.Prop(r, "_Chalk", glow ? new Color(0.5f, 1f, 0.58f) : r.sharedMaterial.GetColor("_Chalk"));
            Art3D.Prop(r, "_Board", glow ? new Color(0.01f, 0.035f, 0.018f) : r.sharedMaterial.GetColor("_Board"));
        }

        void OnDestroy()
        {
            // the lines' material is an asset: not left dark
            if (_lines != null && Dark) _lines.SetColor("_Line", _litLine);
        }
    }
}
