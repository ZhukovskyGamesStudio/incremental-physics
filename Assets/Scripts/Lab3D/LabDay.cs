using System;
using UnityEngine;

namespace ChalkPhysics.Lab3D
{
    /// The working day in the lab: from 8:00 for `hours` hours (to 18:00, longer with the perks of the map) in `length`
    /// seconds. The sun goes round through the windows and
    /// the wall clock turns. At six the evening comes: the apparatus stop and the day's sum is written up. The day ends
    /// at the door, and the map with its upgrades comes between the days (DESIGN.md 2б). The clock runs only while the
    /// player is in the room (the cursor caught), so a pause with Esc costs nothing.
    public class LabDay : MonoBehaviour
    {
        public float length = 120;
        public int hours = 10;
        public Light sun;
        public Transform hourHand, minuteHand;
        public LabWalker walker;
        /// The headless check runs the clock without a caught cursor.
        [NonSerialized] public bool autorun;

        public int Day = 1;   // the game's run: lessons so far and one
        public float T { get; private set; }
        public bool Evening { get; private set; }
        public float Hour => 8 + hours * Mathf.Clamp01(T / length) + (Evening ? _dusk : 0);
        public event Action OnEvening;
        /// The player is in the room (or the check runs it): the customers' patience runs with it, evening or not.
        public bool Running => walker.Looking || autorun;

        float _dusk;

        void Update()
        {
            if (!Evening && (walker.Looking || autorun))
            {
                T += Time.deltaTime;
                if (T >= length) { T = length; Evening = true; OnEvening?.Invoke(); }
            }
            if (Evening) _dusk = Mathf.MoveTowards(_dusk, 0.6f, Time.deltaTime * 0.3f);   // the clock creeps on a little into the dusk
            Pose();
        }

        void Pose()
        {
            float f = Mathf.Clamp01(T / length);
            // morning sun low from the left window, high at noon, low and weak at six, dim in the evening
            var morning = Quaternion.Euler(20, 55, 0);
            var noon = Quaternion.Euler(58, 15, 0);
            var six = Quaternion.Euler(16, -30, 0);
            sun.transform.rotation = f < 0.5f ? Quaternion.Slerp(morning, noon, f * 2) : Quaternion.Slerp(noon, six, f * 2 - 1);
            float light = f < 0.5f ? Mathf.Lerp(0.8f, 1.05f, f * 2) : Mathf.Lerp(1.05f, 0.65f, f * 2 - 1);
            sun.intensity = Evening ? Mathf.Lerp(0.65f, 0.4f, _dusk / 0.6f) : light;
            sun.color = Color.Lerp(Color.white, new Color(1f, 0.82f, 0.62f), Mathf.Clamp01(f * 2 - 1) + (Evening ? 0.3f : 0));

            float h = Hour;
            if (hourHand != null) hourHand.localRotation = Quaternion.Euler(0, 0, -(h % 12) / 12 * 360);
            if (minuteHand != null) minuteHand.localRotation = Quaternion.Euler(0, 0, -(h % 1) * 360);
        }

        void Start() => Pose();
    }
}
