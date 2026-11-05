using UnityEngine;

namespace ChalkPhysics.Lab3D
{
    /// A word of chalk in the room, facing the camera: a popup rises, fades and is gone; a label stays put.
    public class Chalk3DText : MonoBehaviour
    {
        Camera _eye;
        CanvasGroup _g;
        float _t, _life;
        Vector3 _from;

        static Chalk3DText Make(string s, Color c, float height, Camera eye)
        {
            var rt = Art3D.WorldCanvas("ChalkWord");
            var go = rt.gameObject;
            var group = go.AddComponent<CanvasGroup>();
            rt.sizeDelta = new Vector2(900, 100);
            rt.localScale = Vector3.one * (height / 60f);
            var t = UIF.Text(go.transform, "", 60, c, Vector2.zero, new Vector2(900, 100));
            InkText.On(t).Set(s, c);
            var w = go.AddComponent<Chalk3DText>();
            w._eye = eye;
            w._g = group;
            return w;
        }

        public static Chalk3DText Pop(Vector3 at, string s, Color c, float height, Camera eye, float life = 1.6f)
        {
            var w = Make(s, c, height, eye);
            w.transform.position = w._from = at;
            w._life = life;
            w.Face();
            return w;
        }

        /// A word that stays over `parent` (the formula over an apparatus).
        public static Chalk3DText Label(Transform parent, Vector3 local, string s, Color c, float height, Camera eye)
        {
            var w = Make(s, c, height, eye);
            w.transform.SetParent(parent, false);
            w.transform.localPosition = local;
            w.transform.localScale = Vector3.one * (height / 60f);
            w._life = float.PositiveInfinity;
            return w;
        }

        /// Turns every word in the room to the camera at once (a picture taken from somewhere else).
        public static void FaceAll() { foreach (var w in FindObjectsByType<Chalk3DText>(FindObjectsSortMode.None)) w.Face(); }

        void Face()
        {
            if (_eye != null) transform.rotation = Quaternion.LookRotation(transform.position - _eye.transform.position, Vector3.up);
        }

        void LateUpdate()
        {
            Face();
            if (float.IsPositiveInfinity(_life)) return;
            _t += Time.deltaTime;
            transform.position = _from + Vector3.up * (0.06f * Mathf.Sqrt(_t));
            _g.alpha = Mathf.Clamp01((_life - _t) / 0.5f);
            if (_t > _life) Destroy(gameObject);
        }
    }
}
