using UnityEngine;

namespace ChalkPhysics.Lab3D
{
    /// The blackboard on the wall: the formulas of the apparatus in the room, each with what it pays. Nothing is bought
    /// here: the upgrades are on the map, between the days (DESIGN.md 2б).
    public class Board3D : MonoBehaviour
    {
        public Vector2 size = new Vector2(2.9f, 1.3f);    // metres of writing space
        const float Px = 0.0015f;                           // metres per canvas unit

        public void Build(Lab3DGame game)
        {
            var go = new GameObject("Writing", typeof(Canvas));
            go.transform.SetParent(transform, false);
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var canvas = (RectTransform)go.transform;
            canvas.sizeDelta = size / Px;
            canvas.localScale = Vector3.one * Px;
            canvas.localPosition = new Vector3(0, 1.6f, -0.036f);   // just off the board's face, which is 0.9–2.3 m up

            float w = canvas.sizeDelta.x * 0.6f, y = canvas.sizeDelta.y / 2 - 140;
            foreach (var a in game.apparatus)
            {
                if (!a.station.activeSelf || a.copy != 0) continue;   // a department once, however many it has
                bool ideas = Defs.F(a.id).kind == FKind.Measure;
                var t = UIF.Text(canvas, "", 96, ChalkTex.White, new Vector2(0, y), new Vector2(w, 130), TextAnchor.MiddleLeft);
                // the order sheets pay coins for what is read off the apparatus: the board has only its formula then
                InkText.On(t).Set(GameState.Day3D ? a.formula : a.formula + "   " + (ideas ? GameState.Bulb : "Дж"), ideas ? ChalkTex.Cyan : ChalkTex.Yellow);
                y -= 150;
            }
        }
    }
}
