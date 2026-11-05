using UnityEngine;

namespace ChalkPhysics.Lab3D
{
    /// The player's own body: simple chalk hands in white cuffs that show while they hold something — the right one under
    /// what it carries, the left one at the corner of the sheet it holds, a fist round the stamp's handle, two hands at
    /// the sides of the box — and a body in a lab coat that the eye does not see and the mirror does, its arms coming up
    /// in front while they carry something.
    public class PlayerBody : MonoBehaviour
    {
        Lab3DGame _game;
        Transform _rightHand, _sheetHand, _stampFist, _armL, _armR;
        readonly Transform[] _boxHands = new Transform[2];

        public void Init(Lab3DGame game) => _game = game;

        /// The body in its coat, then the hands.
        public void Build()
        {
            Coat();
            MakeHands();
        }

        /// The hands at the box are gone (the box goes to its customer, or back on the counter).
        public void HideBoxHands()
        {
            foreach (var h in _boxHands) if (h != null) h.gameObject.SetActive(false);
        }

        /// Simple chalk hands in white cuffs: the right one under what it carries, the left one at the corner of the sheet
        /// it holds, a fist round the stamp's handle, two hands at the sides of the box.
        void MakeHands()
        {
            var kit = _game.kit;
            if (kit == null || kit.skin == null) return;
            Transform Hand(string name, Transform parent)
            {
                var h = new GameObject(name).transform;
                h.SetParent(parent, false);
                return h;
            }
            Transform P(Transform h, PrimitiveType t, Material m, Vector3 at, Vector3 size, Vector3 euler = default)
                => Art3D.Part(t, m, h, at, size, euler);
            // the right hand, palm up under the sample, its fingers curled up in front, the thumb at the side
            _rightHand = Hand("RightHand", _game.Hands.Anchor);
            P(_rightHand, PrimitiveType.Sphere, kit.skin, new Vector3(0, -0.016f, -0.005f), new Vector3(0.085f, 0.028f, 0.1f));
            for (int i = 0; i < 4; i++)
                P(_rightHand, PrimitiveType.Capsule, kit.skin, new Vector3(-0.027f + i * 0.018f, -0.002f, 0.047f), new Vector3(0.017f, 0.02f, 0.017f), new Vector3(-35, 0, 0));
            P(_rightHand, PrimitiveType.Capsule, kit.skin, new Vector3(-0.046f, -0.002f, 0.012f), new Vector3(0.019f, 0.024f, 0.019f), new Vector3(0, 0, 40));
            P(_rightHand, PrimitiveType.Cylinder, kit.white, new Vector3(0.012f, -0.05f, -0.1f), new Vector3(0.065f, 0.07f, 0.065f), new Vector3(60, 0, 0));
            // the left hand at the sheet's lower left corner: the thumb on the paper, the palm behind it
            _sheetHand = Hand("SheetHand", transform);
            P(_sheetHand, PrimitiveType.Sphere, kit.skin, new Vector3(-0.095f, -0.016f, -0.135f), new Vector3(0.08f, 0.024f, 0.09f));
            P(_sheetHand, PrimitiveType.Capsule, kit.skin, new Vector3(-0.085f, 0.007f, -0.115f), new Vector3(0.019f, 0.03f, 0.012f), new Vector3(90, 0, -35));
            P(_sheetHand, PrimitiveType.Cylinder, kit.white, new Vector3(-0.115f, -0.03f, -0.22f), new Vector3(0.065f, 0.07f, 0.065f), new Vector3(70, 0, 15));
            // the fist round the stamp's handle
            _stampFist = Hand("StampFist", transform);
            P(_stampFist, PrimitiveType.Sphere, kit.skin, new Vector3(0, 0.07f, 0), new Vector3(0.06f, 0.055f, 0.065f));
            P(_stampFist, PrimitiveType.Capsule, kit.skin, new Vector3(-0.03f, 0.075f, 0.012f), new Vector3(0.018f, 0.022f, 0.018f), new Vector3(0, 0, 60));
            P(_stampFist, PrimitiveType.Cylinder, kit.white, new Vector3(0.02f, 0.07f, -0.09f), new Vector3(0.065f, 0.07f, 0.065f), new Vector3(75, 0, 0));
            // two hands holding the box at its sides
            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? -1 : 1;
                _boxHands[i] = Hand("BoxHand", transform);
                P(_boxHands[i], PrimitiveType.Sphere, kit.skin, new Vector3(s * 0.15f, 0.08f, -0.09f), new Vector3(0.03f, 0.09f, 0.08f));
                P(_boxHands[i], PrimitiveType.Cylinder, kit.white, new Vector3(s * 0.17f, 0.04f, -0.2f), new Vector3(0.065f, 0.08f, 0.065f), new Vector3(70, 0, -s * 10));
            }
            foreach (var h in new[] { _rightHand, _sheetHand, _stampFist, _boxHands[0], _boxHands[1] })
            {
                foreach (var x in h.GetComponentsInChildren<Transform>(true)) x.gameObject.layer = Lab3DGame.ViewLayer;   // not in the mirror
                h.gameObject.SetActive(false);
            }
        }

        /// The hands show while they hold something, and go with it (every frame, from the game).
        public void Tick()
        {
            if (_rightHand == null) return;
            var g = _game;
            var hands = g.Hands;
            var desk = g.Desk;
            bool boxInHand = desk.BoxInHand;
            var reading = hands.ReadingView;
            _rightHand.gameObject.SetActive(hands.Held != null && !boxInHand && !g.Closed);
            // the body's arms (seen in the mirror) come up in front while they carry something
            float k = 1 - Mathf.Exp(-Time.deltaTime * 10);
            bool cert = g.awards != null && g.awards.Holding;
            bool right = hands.Held != null || desk.StampHeld || boxInHand || cert, left = (reading != null && reading.Open) || boxInHand || cert;
            if (_armR != null) _armR.localRotation = Quaternion.Slerp(_armR.localRotation, Quaternion.Euler(right ? -75 : 0, 0, 0), k);
            if (_armL != null) _armL.localRotation = Quaternion.Slerp(_armL.localRotation, Quaternion.Euler(left ? -75 : 0, 0, 0), k);
            bool sheet = reading != null && reading.Open;
            _sheetHand.gameObject.SetActive(sheet);
            if (sheet && _sheetHand.parent != reading.Sheet.leaf) { _sheetHand.SetParent(reading.Sheet.leaf, false); _sheetHand.localPosition = Vector3.zero; _sheetHand.localRotation = Quaternion.identity; }
            var stamp = g.stamp;
            bool fist = desk.StampHeld && stamp != null;
            _stampFist.gameObject.SetActive(fist);
            if (fist && _stampFist.parent != stamp) { _stampFist.SetParent(stamp, false); _stampFist.localPosition = Vector3.zero; _stampFist.localRotation = Quaternion.identity; }
            for (int i = 0; i < 2; i++)
            {
                _boxHands[i].gameObject.SetActive(boxInHand && !desk.Sending);   // handed over the sill: the box flies off alone
                if (boxInHand && _boxHands[i].parent != g.dispatchBox) { _boxHands[i].SetParent(g.dispatchBox, false); _boxHands[i].localPosition = Vector3.zero; _boxHands[i].localRotation = Quaternion.identity; }
            }
        }

        /// The player's own body, in a lab coat: the eye does not see it, the mirror does.
        void Coat()
        {
            var kit = _game.kit;
            if (kit == null || kit.white == null) return;
            var root = new GameObject("Body").transform;
            root.SetParent(_game.walker.transform, false);
            Transform P(PrimitiveType t, Material m, Vector3 at, Vector3 size)
            {
                var go = Art3D.Bare(t);
                go.layer = Lab3DGame.BodyLayer;
                go.transform.SetParent(root, false);
                go.transform.localPosition = at;
                go.transform.localScale = size;
                go.GetComponent<Renderer>().sharedMaterial = m;
                return go.transform;
            }
            P(PrimitiveType.Cylinder, kit.dark, new Vector3(-0.09f, 0.42f, -0.05f), new Vector3(0.12f, 0.42f, 0.12f));
            P(PrimitiveType.Cylinder, kit.dark, new Vector3(0.09f, 0.42f, -0.05f), new Vector3(0.12f, 0.42f, 0.12f));
            P(PrimitiveType.Capsule, kit.white, new Vector3(0, 1.08f, -0.06f), new Vector3(0.44f, 0.4f, 0.28f));
            foreach (float sx in new[] { -1f, 1f })
            {
                var shoulder = new GameObject("Shoulder").transform;   // the arm turns at the shoulder
                shoulder.SetParent(root, false);
                shoulder.localPosition = new Vector3(sx * 0.27f, 1.36f, -0.04f);
                var arm = P(PrimitiveType.Capsule, kit.white, Vector3.zero, new Vector3(0.1f, 0.26f, 0.1f));
                arm.SetParent(shoulder, false);
                arm.localPosition = new Vector3(0, -0.24f, 0.02f);
                arm.localRotation = Quaternion.Euler(-10, 0, sx * 6);
                if (sx > 0) _armR = shoulder; else _armL = shoulder;
                P(PrimitiveType.Sphere, kit.dark, new Vector3(sx * 0.045f, 1.62f, 0.035f), new Vector3(0.03f, 0.035f, 0.02f));
                P(PrimitiveType.Cylinder, kit.dark, new Vector3(sx * 0.045f, 1.62f, 0.05f), new Vector3(0.065f, 0.003f, 0.065f)).localRotation = Quaternion.Euler(90, 0, 0);
            }
            P(PrimitiveType.Cylinder, kit.skin, new Vector3(0, 1.43f, -0.05f), new Vector3(0.1f, 0.05f, 0.1f));
            P(PrimitiveType.Sphere, kit.skin, new Vector3(0, 1.6f, -0.06f), new Vector3(0.23f, 0.26f, 0.23f));
            P(PrimitiveType.Sphere, kit.brown, new Vector3(0, 1.67f, -0.08f), new Vector3(0.25f, 0.18f, 0.25f));
            P(PrimitiveType.Sphere, kit.skin, new Vector3(0, 1.585f, 0.06f), new Vector3(0.04f, 0.05f, 0.04f));
        }
    }
}
