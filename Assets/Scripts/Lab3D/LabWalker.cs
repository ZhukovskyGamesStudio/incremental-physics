using UnityEngine;
using UnityEngine.InputSystem;

namespace ChalkPhysics.Lab3D
{
    /// First person in the lab: WASD walks, the mouse looks around. The first click catches the cursor (and does
    /// nothing else), Esc lets it go.
    [RequireComponent(typeof(CharacterController))]
    public class LabWalker : MonoBehaviour
    {
        public Camera eye;
        public float speed = 2.2f;
        public float look = 0.12f;

        CharacterController _cc;
        float _yaw, _pitch;

        public bool Looking => Cursor.lockState == CursorLockMode.Locked;
        /// Reading an order sheet: no walking, no looking round, and a click is the sheet's.
        [System.NonSerialized] public bool frozen;
        int _caughtFrame = -1;

        /// The cursor is caught again by the game (a sheet put down): this frame's click and Esc are not the walker's.
        public void Catch()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            _caughtFrame = Time.frameCount;
        }
        /// The click that caught the cursor: it must not also take a sample.
        public bool CaughtThisFrame { get; private set; }

        void Start()
        {
            _cc = GetComponent<CharacterController>();
            _yaw = transform.eulerAngles.y;
            _pitch = eye.transform.localEulerAngles.x;
            if (_pitch > 180) _pitch -= 360;
        }

        void Update()
        {
            var kb = Keyboard.current;
            var ms = Mouse.current;
            CaughtThisFrame = false;
            if (frozen) return;
            if (ms != null && !Looking && ms.leftButton.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                CaughtThisFrame = true;
            }
            if (kb != null && kb.escapeKey.wasPressedThisFrame && Time.frameCount != _caughtFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (Looking && ms != null)
            {
                var d = ms.delta.ReadValue();
                _yaw += d.x * look;
                _pitch = Mathf.Clamp(_pitch - d.y * look, -80, 80);
            }
            transform.rotation = Quaternion.Euler(0, _yaw, 0);
            eye.transform.localRotation = Quaternion.Euler(_pitch, 0, 0);

            var mv = Vector2.zero;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) mv.y += 1;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) mv.y -= 1;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) mv.x += 1;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) mv.x -= 1;
            }
            var dir = transform.right * mv.x + transform.forward * mv.y;
            _cc.SimpleMove(Vector3.ClampMagnitude(dir, 1) * speed);
        }
    }
}
