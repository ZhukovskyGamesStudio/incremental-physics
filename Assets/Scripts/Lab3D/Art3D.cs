using UnityEngine;
using UnityEngine.UI;

namespace ChalkPhysics.Lab3D
{
    /// Little makers for what the lab builds at runtime: a scale's divisions, the loupe, the player's hands, chalk lines and
    /// writing in the room — and the pivots of the Blender models' moving pieces (the odd departments' things, the desk's).
    public static class Art3D
    {
        /// A moving piece of a Blender model (Tools/Blender/kit.py, «Moving pieces»): the model's marker `group` becomes
        /// its pivot, under `parent`, turned `turn` there (none: as the parent); every part of the model named
        /// "<group>-…" goes under it, staying where it is. The code then moves, turns or scales the pivot. A model without
        /// that marker gets a new pivot at its origin.
        public static Transform Pivot(Transform model, string group, Transform parent, Quaternion? turn = null)
        {
            var p = model.Find(group);
            if (p == null) { p = new GameObject(group).transform; p.SetParent(model, false); }
            p.SetParent(parent, true);
            p.localRotation = turn ?? Quaternion.identity;
            string prefix = group + "-";
            var parts = new System.Collections.Generic.List<Transform>();
            foreach (Transform c in model) if (c.name.StartsWith(prefix)) parts.Add(c);
            foreach (var c in parts) c.SetParent(p, true);
            return p;
        }

        /// A shape the dot passes through (its collider gone); named, or with the name the engine gives it.
        public static GameObject Bare(PrimitiveType type, string name = null)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.Destroy(go.GetComponent<Collider>());
            if (name != null) go.name = name;
            return go;
        }

        public static Transform Part(PrimitiveType type, Material m, Transform parent, Vector3 at, Vector3 size, Vector3? euler = null, string name = null)
        {
            var go = Bare(type, name ?? type.ToString());
            var t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = at;
            t.localScale = size;
            if (euler.HasValue) t.localRotation = Quaternion.Euler(euler.Value);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return t;
        }

        public static Transform Empty(string name, Transform parent, Vector3 at, Vector3? euler = null)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = at;
            if (euler.HasValue) t.localRotation = Quaternion.Euler(euler.Value);
            return t;
        }

        /// A box the dot can find (an empty with only a collider).
        public static BoxCollider Grip(string name, Transform parent, Vector3 center, Vector3 size)
        {
            var t = Empty(name, parent, center);
            var c = t.gameObject.AddComponent<BoxCollider>();
            c.size = size;
            return c;
        }

        /// A material of plain colour, no light on it (a loupe's rim, a customer's patience clock).
        public static Material Unlit() => new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        public static Material Unlit(Color c) => new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = c };
        /// The picture of a camera laid on glass (the loupe's, the mirror's).
        public static Material Unlit(Texture picture) => new Material(Shader.Find("Universal Render Pipeline/Unlit")) { mainTexture = picture };

        static MaterialPropertyBlock _props;

        /// One renderer's own value of its chalk material's property (the glow under the dot, a sample's slant of
        /// hatching), the material itself still shared.
        public static void Prop(Renderer r, string name, float v)
        {
            _props ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(_props);
            _props.SetFloat(name, v);
            r.SetPropertyBlock(_props);
        }

        public static void Prop(Renderer r, string name, Color v)
        {
            _props ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(_props);
            _props.SetColor(name, v);
            r.SetPropertyBlock(_props);
        }

        /// A canvas in the room (a sheet's writing, a tag, a word in the air): placed in the world, not on the screen.
        public static RectTransform WorldCanvas(string name, Transform parent = null)
        {
            var go = new GameObject(name, typeof(Canvas));
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform;
            if (parent != null) rt.SetParent(parent, false);
            return rt;
        }

        /// Chalk writing lying flat on a table, read by whoever stands at its front (+z): a canvas a millimetre a unit.
        public static RectTransform Flat(Transform parent, Vector3 at, Vector2 sizeMm)
        {
            var rt = WorldCanvas("Writing", parent);
            rt.sizeDelta = sizeMm;
            rt.localScale = Vector3.one * 0.001f;
            rt.localPosition = at;
            rt.localRotation = Quaternion.Euler(90, 180, 0);
            return rt;
        }

        /// Chalk writing standing up, facing +z (a poster, a board).
        public static RectTransform Upright(Transform parent, Vector3 at, Vector2 sizeMm)
        {
            var rt = Flat(parent, at, sizeMm);
            rt.localRotation = Quaternion.Euler(0, 180, 0);
            return rt;
        }

        /// Chalk writing on the face of a board hung up in the room (its face looks along -z, at the one who reads it):
        /// «заказы» on the counter, an apparatus' name and formula on its table.
        public static void Signboard(Transform board, string text, int fontSize = 110)
        {
            var rt = WorldCanvas("Writing", board);
            var size = board.localScale;
            rt.sizeDelta = new Vector2(600, 600 * size.y / size.x);
            rt.localScale = new Vector3(1 / 600f, size.x / 600f / size.y, 1);   // undo the board's own stretch
            rt.localPosition = new Vector3(0, 0, -0.51f);                         // just off its face
            UIF.Text(rt, text, fontSize, ChalkTex.White, Vector2.zero, rt.sizeDelta);
        }

        public static Text Word(RectTransform canvas, string s, Vector2 at, int size, Color c, float width = 300)
        {
            var t = UIF.Text(canvas, s, size, c, at, new Vector2(width, size * 1.6f));
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }

        /// A chalk line in the room (a fishing line, a whisker): a thin line renderer of the kit's plain white.
        public static LineRenderer Line(string name, Transform parent, Material m, float width)
        {
            var l = LocalLine(name, parent, m, width);
            l.useWorldSpace = true;
            l.positionCount = 2;
            return l;
        }

        /// A chalk line drawn in its parent's own space (a ring round a place, a clock's arc): it goes where the parent goes.
        public static LineRenderer LocalLine(string name, Transform parent, Material m, float width)
        {
            var l = new GameObject(name).AddComponent<LineRenderer>();
            l.transform.SetParent(parent, false);
            l.useWorldSpace = false;
            l.widthMultiplier = width;
            l.sharedMaterial = m;
            l.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return l;
        }
    }
}
