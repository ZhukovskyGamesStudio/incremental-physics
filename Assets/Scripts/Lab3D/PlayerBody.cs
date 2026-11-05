using UnityEngine;

namespace ChalkPhysics.Lab3D
{
    /// The player's own body (Blender models, Tools/Blender/body.py): chalk hands in white cuffs that show while they hold something — the right one under
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

        /// Chalk hands in white cuffs (Blender models): the right one under what it carries, the left one at the corner of
        /// the sheet it holds, a fist round the stamp's handle, two hands at the sides of the box.
        void MakeHands()
        {
            var kit = _game.kit;
            if (kit == null || kit.skin == null) return;
            Transform Hand(string name, Transform parent, string model, bool mirror = false)
            {
                var h = new GameObject(name).transform;
                h.SetParent(parent, false);
                var m = ModelLibrary.Spawn(model, h, kit);
                if (m != null && mirror) m.transform.localScale = new Vector3(-1, 1, 1);   // the left hand: the right one's mirror
                return h;
            }
            _rightHand = Hand("RightHand", _game.Hands.Anchor, "Body_hand_carry");
            _sheetHand = Hand("SheetHand", transform, "Body_hand_sheet");
            _stampFist = Hand("StampFist", transform, "Body_hand_fist");
            for (int i = 0; i < 2; i++) _boxHands[i] = Hand("BoxHand", transform, "Body_hand_box", i == 0);
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

        /// The player's own body, in a lab coat (a Blender model): the eye does not see it, the mirror does. Its arms turn at
        /// the shoulders.
        void Coat()
        {
            var kit = _game.kit;
            if (kit == null || kit.white == null) return;
            var root = new GameObject("Body").transform;
            root.SetParent(_game.walker.transform, false);
            var m = ModelLibrary.Spawn("Body_coat", root, kit);
            if (m == null) return;
            foreach (var x in m.GetComponentsInChildren<Transform>(true)) x.gameObject.layer = Lab3DGame.BodyLayer;
            _armL = Art3D.Pivot(m.transform, "ArmL", root);
            _armR = Art3D.Pivot(m.transform, "ArmR", root);
        }
    }
}
