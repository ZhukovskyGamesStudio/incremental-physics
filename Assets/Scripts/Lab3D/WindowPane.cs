using System.Collections;
using UnityEngine;

namespace ChalkPhysics.Lab3D
{
    /// One of the four panes of a window (DESIGN.md 2б): a sample thrown at it breaks it — the glass is gone, shards fly
    /// into the room and fall, a few jagged ones stay in the frame — and five coins go on the glazier (LabHands.Throw).
    /// Mended by the next morning.
    public class WindowPane : Pressable
    {
        public Lab3DGame game;
        public Material shardMat;
        public Vector2 size = new Vector2(0.6f, 0.71f);
        [System.NonSerialized] public bool broken;

        public override string Hint => !broken && game != null && game.Hands.Held != null ? "бросить образец в окно" : "";
        public override void Press() { if (!broken && game != null) game.Hands.Throw(this); }

        public void Break()
        {
            if (broken) return;
            broken = true;
            foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            StartCoroutine(Shards());
        }

        IEnumerator Shards()
        {
            // jagged slivers left in the frame, along its edges
            for (int i = 0; i < 10; i++)
            {
                float u = Random.value;
                int side = i % 4;
                var at = side switch
                {
                    0 => new Vector3((u - 0.5f) * size.x, size.y / 2 - 0.02f, 0),
                    1 => new Vector3((u - 0.5f) * size.x, -size.y / 2 + 0.02f, 0),
                    2 => new Vector3(-size.x / 2 + 0.02f, (u - 0.5f) * size.y, 0),
                    _ => new Vector3(size.x / 2 - 0.02f, (u - 0.5f) * size.y, 0),
                };
                Sliver(at, Random.Range(0.04f, 0.1f), Random.Range(0, 180f));
            }
            // and the rest flying into the room, falling, gone
            var flying = new Transform[16];
            var vel = new Vector3[16];
            for (int i = 0; i < flying.Length; i++)
            {
                flying[i] = Sliver(new Vector3((Random.value - 0.5f) * size.x * 0.8f, (Random.value - 0.5f) * size.y * 0.8f, 0), Random.Range(0.03f, 0.08f), Random.Range(0, 180f));
                vel[i] = transform.forward * Random.Range(0.4f, 1.4f) + Random.insideUnitSphere * 0.6f + Vector3.up * 0.4f;
            }
            for (float t = 0; t < 1.6f; t += Time.deltaTime)
            {
                for (int i = 0; i < flying.Length; i++)
                {
                    if (flying[i] == null) continue;
                    vel[i] += Vector3.down * 9.8f * Time.deltaTime;
                    var p = flying[i].position + vel[i] * Time.deltaTime;
                    if (p.y < 0.01f) { p.y = 0.01f; vel[i] = Vector3.zero; }
                    flying[i].position = p;
                    flying[i].Rotate(300 * Time.deltaTime, 200 * Time.deltaTime, 0);
                    if (t > 1.1f) flying[i].localScale *= 1 - Time.deltaTime * 4;
                }
                yield return null;
            }
            foreach (var f in flying) if (f != null) Destroy(f.gameObject);
        }

        Transform Sliver(Vector3 local, float len, float angle)
        {
            var s = Art3D.Part(PrimitiveType.Cube, shardMat, transform, local, new Vector3(len, 0.004f, 0.003f), new Vector3(0, 0, angle), "Shard");
            return s;
        }
    }
}
