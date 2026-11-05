using System.Collections.Generic;
using UnityEngine;

namespace ChalkPhysics.Lab3D
{
    /// The lab is as big as its departments need (DESIGN.md 2б, «Каморка»): the temporary walls across and along the
    /// room, the clutter in their corners, and the place of every department's table. Grown since yesterday, the walls
    /// slide back this morning before the player's eyes, with a card; the stage reached is kept in the save.
    public class LabRoom : MonoBehaviour
    {
        /// Where the temporary walls stand — the cross wall's face (z) and the side wall's (x) — by how many departments
        /// the lab has: one — a closet by the door, 3.6 × 2.5 m: the counter and the weighing table against the back wall;
        /// two — the room up to the tables by the windows; three or more — the walls are gone (DESIGN.md 2б, «Каморка»).
        public static readonly float[] WallZ = { 1.0f, 1.0f, -0.6f, -2.1f, -3.7f };
        public static readonly float[] WallX = { -0.6f, -2.4f, -2.4f, -3.15f, -3.15f };
        /// The room's own right wall (x) and back wall (z).
        public const float RightWall = 3.0f, BackWall = 3.5f;
        /// The last stage: no temporary walls left.
        public static int LastStage => WallZ.Length - 1;
        /// Where the departments' tables stand, in the order they are bought (DESIGN.md 2б, «Крейзи-измерения»): the
        /// k-th department bought takes place k, which is inside the room once it has grown k times. The table's front
        /// (its local +z) looks into the room.
        public static readonly (Vector3 at, float yaw)[] Spots =
        {
            (new Vector3(0.25f, 0, 3.1f), 180), (new Vector3(-1.4f, 0, 3.1f), 180), (new Vector3(-0.6f, 0, -0.1f), 0),
            (new Vector3(2.58f, 0, -0.55f), -90), (new Vector3(-2.58f, 0, -1.8f), 90), (new Vector3(0f, 0, -3.1f), 0),
            (new Vector3(2.58f, 0, -2.2f), -90), (new Vector3(-2.58f, 0, 1.7f), 90), (new Vector3(0.95f, 0, -1.5f), 0),
        };

        Lab3DGame _game;
        int _growFrom, _growTo = -1;
        float _growT;
        GameState G => _game.G;

        /// How far the room has grown today (0 — the closet, LastStage — no temporary walls).
        public int Stage { get; private set; }

        public void Init(Lab3DGame game) => _game = game;

        /// Every department's table goes to its place before anything starts (the bath's water is laid out where it
        /// stands): the places in the order they were bought, remembered in the save.
        public void PlaceTables()
        {
            var taken = new Dictionary<string, int>();
            foreach (var u in G.Used)
                if (u.StartsWith("3d:spot:")) { var kv = u.Substring(8).Split('='); if (kv.Length == 2 && int.TryParse(kv[1], out var k)) taken[kv[0]] = k; }
            foreach (var d in _game.departments)
            {
                if (d.table == null || !_game.Owns(d.id) || taken.ContainsKey(d.id)) continue;
                int k = d.id == "dyna" ? 0 : 1;
                while (taken.ContainsValue(k)) k++;
                taken[d.id] = k;
                G.MarkUsed("3d:spot:" + d.id + "=" + k);
            }
            foreach (var d in _game.departments)
            {
                if (d.table == null || !taken.TryGetValue(d.id, out var k)) continue;
                var (at, yaw) = Spots[Mathf.Clamp(k, 0, Spots.Length - 1)];
                d.table.localPosition = at;
                d.table.localRotation = Quaternion.Euler(0, yaw, 0);
            }
        }

        /// The morning: the walls where they stood yesterday; if the lab has grown since, they slide back in a moment.
        public void Open()
        {
            if (_game.partition == null) return;
            int built = 0;
            foreach (var d in _game.departments) if (d.table != null && _game.Owns(d.id)) built++;
            int stage = Mathf.Clamp(built - 1, 0, WallZ.Length - 1);
            Stage = stage;
            int seen = 0;
            while (seen < stage && G.Used.Contains("3d:room" + (seen + 1))) seen++;
            Place(WallZ[seen], WallX[seen]);
            if (stage > seen) { _growFrom = seen; _growTo = stage; _growT = -1.5f; }   // a moment into the morning
        }

        /// The temporary walls at the edges z (the cross wall) and x (the side wall), the clutter in their corners.
        public void Place(float z, float x)
        {
            Walls(_game.partition, _game.partitionSide, _game.cornerFL, _game.cornerFR, _game.cornerBL, z, x);
            _game.partition.gameObject.SetActive(z > WallZ[WallZ.Length - 1] + 0.01f);
            _game.partitionSide.gameObject.SetActive(x > -3.05f);
        }

        /// Shared with the scene builder: the walls' size and place from the edges, the corners where they meet.
        public static void Walls(Transform cross, Transform side, Transform fl, Transform fr, Transform bl, float z, float x)
        {
            const float t = 0.12f, h = 3.0f;
            cross.localPosition = new Vector3((x + RightWall) / 2, h / 2, z - t / 2);
            cross.localScale = new Vector3(RightWall - x + t, h, t);
            side.localPosition = new Vector3(x - t / 2, h / 2, (z + BackWall) / 2);
            side.localScale = new Vector3(t, h, BackWall - z);
            // the clutter stands in the corner the walls make, never past the room's own walls (x −3, z −3.5)
            if (fl != null) fl.localPosition = new Vector3(Mathf.Max(x, -3.0f), 0, Mathf.Max(z, -3.5f));
            if (fr != null) fr.localPosition = new Vector3(RightWall, 0, Mathf.Max(z, -3.5f));
            if (bl != null) bl.localPosition = new Vector3(Mathf.Max(x, -3.0f), 0, BackWall);
        }

        /// The wall slides back to where the new departments fit, slowly enough to be seen (every frame, from the game).
        public void Tick()
        {
            if (_growTo < 0) return;
            float was = _growT;
            _growT += Time.deltaTime / 2.5f;
            if (was < 0 && _growT >= 0) _game.Hud.Card("Лаборатория выросла", "", "", 3f);
            if (_growT < 0) return;
            float t = Mathf.Clamp01(_growT), e = t * t * (3 - 2 * t);
            var partition = _game.partition;
            var partitionSide = _game.partitionSide;
            partition.gameObject.SetActive(true);
            partitionSide.gameObject.SetActive(true);
            Walls(partition, partitionSide, _game.cornerFL, _game.cornerFR, _game.cornerBL, Mathf.Lerp(WallZ[_growFrom], WallZ[_growTo], e), Mathf.Lerp(WallX[_growFrom], WallX[_growTo], e));
            if (t < 1) return;
            Place(WallZ[_growTo], WallX[_growTo]);
            for (int s = _growFrom + 1; s <= _growTo; s++) G.MarkUsed("3d:room" + s);
            _growTo = -1;
        }
    }
}
