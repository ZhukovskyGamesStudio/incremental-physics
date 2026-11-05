using System.Collections.Generic;
using UnityEngine;

namespace ChalkPhysics.Lab3D
{
    /// The divisions of an apparatus' scale, laid at runtime (the map's upgrades make them finer): from the zero tick
    /// downwards every `step` units, `perUnit` metres apart; whole units longer and numbered beside the plate. Shared by
    /// the dynamometer (newtons) and the shelf's impact scale (joules).
    public static class ScaleTicks
    {
        public static void Build(Transform owner, Renderer zeroMark, Vector3 outward, float perUnit, double step, float max,
                                 Material tickMat, Camera eye, List<GameObject> into, float labelHeight = 0.022f)
        {
            foreach (var t in into) Object.Destroy(t);
            into.Clear();
            var b = zeroMark.bounds;
            bool alongX = b.size.x >= b.size.z;
            float len = alongX ? b.size.x : b.size.z;
            int count = Mathf.FloorToInt((float)(max / step) + 0.001f);
            for (int i = 1; i <= count; i++)
            {
                double v = i * step;
                bool whole = System.Math.Abs(v - System.Math.Round(v)) < 1e-6;
                bool half = !whole && System.Math.Abs(v * 2 - System.Math.Round(v * 2)) < 1e-6;
                float k = whole ? 1 : half ? 0.75f : 0.5f;
                var at = b.center + Vector3.down * (float)(v * perUnit);
                var tick = Art3D.Bare(PrimitiveType.Cube, "Tick " + v.ToString("0.##"));
                // short ticks start where the long ones do: at the plate's edge by the pointer
                tick.transform.position = at - outward * (len * (1 - k) / 2);
                tick.transform.rotation = Quaternion.identity;
                tick.transform.localScale = alongX ? new Vector3(len * k, b.size.y * 0.8f, b.size.z) : new Vector3(b.size.x, b.size.y * 0.8f, len * k);
                tick.transform.SetParent(owner, true);
                tick.GetComponent<Renderer>().sharedMaterial = tickMat;
                into.Add(tick);
                if (whole)
                {
                    var label = Chalk3DText.Label(owner, owner.InverseTransformPoint(at + outward * (len / 2 + 0.014f)), ((int)System.Math.Round(v)).ToString(), ChalkTex.White, labelHeight, eye);
                    into.Add(label.gameObject);
                }
            }
        }
    }
}
