using UnityEngine;

namespace ChalkPhysics.Lab3D
{
    /// The light switch by the door (DESIGN.md 2б, «Крейзи-измерения»): off — the room goes black, and only what glows is
    /// faintly seen, green; on — the room is back.
    public class LightSwitch : Pressable
    {
        public Lab3DGame game;
        public Transform toggle;
        public override string Hint => game.Lights.Dark ? "включить свет" : "выключить свет";
        public override void Press() => game.Lights.Toggle();

        void Update()
        {
            if (toggle == null) return;
            var want = Quaternion.Euler(game.Lights.Dark ? 18 : -18, 0, 0);
            toggle.localRotation = Quaternion.Slerp(toggle.localRotation, want, 1 - Mathf.Exp(-Time.deltaTime * 30));
        }
    }
}
