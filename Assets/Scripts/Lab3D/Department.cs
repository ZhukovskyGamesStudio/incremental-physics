using System.Collections.Generic;
using UnityEngine;

namespace ChalkPhysics.Lab3D
{
    /// A department of the lab (DESIGN.md 2б, «Отделы»): the table of one formula with its apparatus — one at first, more
    /// bought on the map — and the way the chalk helpers walk to it from the counter, round the other tables.
    public class Department : MonoBehaviour
    {
        public string id;
        public Transform table;
        public List<Apparatus3D> apparatus = new List<Apparatus3D>();
        public List<Vector3> path = new List<Vector3>();   // on the floor, from the counter to the back of the table
        public const float Top = 1.21f;   // the tables' height (Tools/Blender/lab3d.py, build_table)

        /// An apparatus a helper may take an order to: there today, empty and not promised to another helper.
        public Apparatus3D Free()
        {
            foreach (var a in apparatus) if (a.Active && a.Load == null && a.helper == null) return a;
            return null;
        }

        /// Behind the table, on the floor, level with the apparatus: where a helper hops up from.
        public Vector3 Behind(Apparatus3D a) => table.TransformPoint(new Vector3(table.InverseTransformPoint(a.transform.position).x, 0, -0.75f));
        /// On the table top, behind the apparatus: where a helper works it.
        public Vector3 Perch(Apparatus3D a) => table.TransformPoint(new Vector3(table.InverseTransformPoint(a.transform.position).x + 0.12f, Top, -0.32f));
    }
}
