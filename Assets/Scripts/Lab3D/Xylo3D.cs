using UnityEngine;

namespace ChalkPhysics.Lab3D
{
    /// The xylophone (DESIGN.md 2б, «Крейзи-измерения»): the sample lies on a felt pad with a little mallet over it; a toy
    /// xylophone beside it, seven coloured bars from do to si, their names in chalk before them. Click the sample: the
    /// mallet strikes and the sample rings its note, trembling. Click the bars to find the one that sounds the same — by
    /// ear; the note's name goes on the sheet.
    public class Xylo3D : OddApparatus
    {
        Transform _mallet;
        readonly Transform[] _bars = new Transform[7];
        float _strike = -1, _ring = -1;
        readonly float[] _barHit = new float[7];
        /// The sample has been struck and has rung its note (the bot listens to it).
        public bool Rang { get; private set; }

        public override string PutVerb => "положить на подушечку";
        public override bool HasAction => Load != null;
        public override string Verb => "стукнуть молоточком";

        protected override Material Needs(PersonaKit kit) => kit.purple;

        protected override void Build(PersonaKit kit)
        {
            // the Blender model (Tools/Blender/odd.py, xylo): the felt pad, the mallet on its post, the xylophone — two
            // rails, seven bars on pins, longest (lowest) on the left of whoever faces it
            var m = ModelLibrary.Spawn("Odd_xylo", transform, kit)?.transform;
            if (m == null) return;
            seat = Art3D.Pivot(m, "Seat", transform);
            _mallet = Art3D.Pivot(m, "Mallet", transform);
            var names = Oddity.Options("note");
            var flat = Art3D.Flat(transform, new Vector3(0.2f, 0.003f, 0.2f), new Vector2(700, 120));
            for (int i = 0; i < 7; i++)
            {
                var bar = Art3D.Pivot(m, "Bar" + i, transform);
                bar.name = "Bar " + names[i];
                var col = bar.gameObject.AddComponent<BoxCollider>();
                col.size = new Vector3(0.066f, 0.048f, 0.26f - i * 0.017f);
                var key = bar.gameObject.AddComponent<XyloBar>();
                key.xylo = this;
                key.index = i;
                _bars[i] = bar;
                Art3D.Word(flat, names[i], new Vector2(-(bar.localPosition.x - 0.2f) * 1000, 0), 50, ChalkTex.White, 90);
            }
            Art3D.Grip("Grip", transform, new Vector3(-0.45f, 0.08f, 0.05f), new Vector3(0.26f, 0.16f, 0.26f));
        }

        public override void Down()
        {
            if (Load == null || _strike >= 0) return;
            _strike = 0;
        }

        protected override void OnTaken(Sample3D s) { _ring = -1; Rang = false; }
        protected override void OnPut(Sample3D s) { Rang = false; }

        /// A bar is struck: its note, and it dips under the stroke.
        public void Bar(int i)
        {
            _barHit[i] = 0.2f;
            Sfx.Play("xylo", 0.75f, Oddity.Pitch[i], 0, 0.02f);
        }

        void Update()
        {
            if (_mallet == null) return;
            if (_strike >= 0)
            {   // the mallet swings down onto the sample and back up
                _strike += Time.deltaTime;
                float a = _strike < 0.12f ? _strike / 0.12f : Mathf.Max(0, 1 - (_strike - 0.12f) / 0.25f);
                _mallet.localRotation = Quaternion.Euler(0, 0, -a * 55);
                if (_strike >= 0.12f && _strike - Time.deltaTime < 0.12f && Load != null)
                {
                    Sfx.Play("chime", 0.8f, Oddity.Pitch[Load.data != null ? Load.data.note : 0], 0, 0.02f);
                    _ring = 0;
                    Rang = true;
                    Tried = true;
                }
                if (_strike > 0.4f) { _strike = -1; _mallet.localRotation = Quaternion.identity; }
            }
            if (_ring >= 0 && Load != null)
            {   // the sample trembles as it rings
                _ring += Time.deltaTime;
                float k = Mathf.Max(0, 1 - _ring / 1.2f);
                Load.Driven = true;
                Load.transform.localRotation = Quaternion.Euler(Mathf.Sin(_ring * 90) * 3 * k, 0, Mathf.Sin(_ring * 77) * 3 * k);
                if (k <= 0) { _ring = -1; Load.Driven = false; }
            }
            for (int i = 0; i < 7; i++)
            {
                if (_bars[i] == null) continue;
                _barHit[i] = Mathf.Max(0, _barHit[i] - Time.deltaTime);
                _bars[i].localPosition = new Vector3(_bars[i].localPosition.x, 0.05f - _barHit[i] * 0.03f, 0);
            }
        }
    }
}
