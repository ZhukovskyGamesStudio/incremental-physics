using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ChalkPhysics.Lab3D
{
    /// A chalk helper (DESIGN.md 2б, «Мелки-помощники»): a little stick of chalk that works the orders by itself. It
    /// takes the oldest order off the counter whose test has a free apparatus, carries the sample to that department,
    /// hops onto the table and works the apparatus the way it can — half-heartedly: it lifts the shelf halfway, squeezes
    /// the spring three quarters, but pushes the pendulum in time — then carries the sample back to the counter. The
    /// player's own hands do it better; the helpers make the lab hum.
    public class Helper3D : MonoBehaviour
    {
        public Lab3DGame game;
        public Transform body;      // the visible chalk: it bobs as it walks and leans as it works
        public Transform carry;     // where a carried sample rides, over its head
        public Vector3 rest;        // where it waits, by the counter
        public float speed = 1.4f;
        /// What it is doing, for the check.
        public string Doing { get; private set; } = "";

        Sample3D _sample;
        Apparatus3D _app;
        float _bob;

        void OnEnable()
        {
            transform.position = rest;
            StartCoroutine(Run());   // in OnEnable: a script reload in play mode stops coroutines and calls it again
        }

        void OnDisable() => Drop();

        IEnumerator Run()
        {
            yield return null;
            while (true)
            {
                Doing = "ждёт";
                if (game.Closed || !game.Tags.FindJob(this, out _sample, out _app))
                {
                    yield return Walk(new List<Vector3> { rest }, true);
                    yield return new WaitForSeconds(0.25f);
                    continue;
                }
                var dept = game.Dept(_app.id);
                var o = _sample.order;
                // to the counter for the sample
                Doing = "за образцом";
                yield return Walk(new List<Vector3> { dept.path[0] }, false);
                if (!Still(o) || _sample.At != Sample3D.Where.Crate || _sample.transform.parent != _sample.home) { Drop(); continue; }
                _sample.MoveTo(carry, Vector3.zero, Sample3D.Where.Helper);
                // over to its department, round the tables, and up onto the table behind the apparatus
                Doing = "несёт";
                var way = new List<Vector3>(dept.path) { dept.Behind(_app) };
                yield return Walk(way, true);
                if (!Still(o)) { yield return Back(dept); continue; }
                if (_app.Load != null)
                {   // the player has put something on it meanwhile: another one of the department, or back
                    _app.helper = null;
                    _app = dept.Free();
                    if (_app == null) { yield return Back(dept); continue; }
                    _app.helper = this;
                    yield return Walk(new List<Vector3> { dept.Behind(_app) }, true);
                }
                yield return Hop(dept.Perch(_app), dept.table.forward);
                _app.Put(_sample);
                Doing = "работает";
                yield return Work(o);
                while (_app.Busy) yield return null;
                _app.Take();
                _sample.MoveTo(carry, Vector3.zero, Sample3D.Where.Helper);
                var down = dept.Behind(_app);
                _app.helper = null;
                _app = null;
                yield return Hop(down, null);
                yield return Back(dept);
            }
        }

        bool Still(Order o) => _sample != null && _sample.order == o && o.by == this && !game.Closed;

        /// Back along the department's path to the counter, and the sample back into its slot (which pays, if done).
        IEnumerator Back(Department dept)
        {
            Doing = "назад";
            var way = new List<Vector3>(dept.path);
            way.Reverse();
            yield return Walk(way, true);
            if (_sample != null && _sample.At == Sample3D.Where.Helper)
            {
                var s = _sample;
                Drop();
                s.GoHome();
                game.Tags.Returned(s);
            }
            else Drop();
        }

        /// Lets go of the order and the apparatus.
        void Drop()
        {
            if (_app != null && _app.helper == this) _app.helper = null;
            if (_sample != null && _sample.order != null && _sample.order.by == this) _sample.order.by = null;
            _app = null;
            _sample = null;
        }

        /// Works the apparatus until the order's test is done (or it is given up on: the evening, a long while).
        IEnumerator Work(Order o)
        {
            string test = _app.id;
            float until = Time.time + 15;
            bool Going() => _sample != null && _app != null && _app.Load == _sample && o.Wants(test) && !game.Closed && Time.time < until;
            switch (_app)
            {
                case Shelf3D shelf:
                    while (Going())
                    {
                        _app.Down();
                        yield return Lean(shelf.liftTime * 0.5f);
                        _app.Up();
                        while (_app.Busy) yield return null;
                        yield return new WaitForSeconds(0.4f);
                    }
                    break;
                case Launcher3D spring:
                    while (Going())
                    {
                        _app.Down();
                        yield return Lean(spring.squeezeTime * 0.7f);
                        _app.Up();
                        while (_app.Busy) yield return null;
                        yield return new WaitForSeconds(0.3f);
                    }
                    break;
                case Pendulum3D pend:
                    _app.Down();
                    while (Going())
                    {
                        if (pend.Amp < 0.6f && pend.Swing < 0.25f) { _app.Down(); yield return Lean(0.15f); }
                        yield return null;
                    }
                    break;
                default:   // the dynamometer reads itself
                    while (Going()) yield return null;
                    break;
            }
        }

        /// Leans towards the apparatus for a while: pressing on it.
        IEnumerator Lean(float t)
        {
            for (float k = 0; k < t; k += Time.deltaTime)
            {
                body.localRotation = Quaternion.Euler(Mathf.Min(1, k / 0.12f) * 14, 0, 0);
                yield return null;
            }
            body.localRotation = Quaternion.identity;
        }

        /// Walks along the points on the floor, hopping a little with every step, facing where it goes.
        IEnumerator Walk(List<Vector3> way, bool all)
        {
            foreach (var p0 in way)
            {
                var p = new Vector3(p0.x, 0, p0.z);
                while ((transform.position - p).sqrMagnitude > 0.0004f)
                {
                    var d = p - transform.position;
                    float step = speed * Time.deltaTime;
                    if (d.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d, Vector3.up), 1 - Mathf.Exp(-12 * Time.deltaTime));
                    transform.position = d.magnitude <= step ? p : transform.position + d.normalized * step;
                    _bob += Time.deltaTime * 11;
                    body.localPosition = Vector3.up * (0.025f * Mathf.Abs(Mathf.Sin(_bob)));
                    yield return null;
                }
                if (!all) break;
            }
            body.localPosition = Vector3.zero;
            if (_sample == null && (transform.position - rest).sqrMagnitude < 0.01f)   // waiting: face the room
                transform.rotation = Quaternion.LookRotation(Vector3.back + Vector3.left * 0.4f, Vector3.up);
        }

        /// A hop in an arc: up onto a table or down to the floor.
        IEnumerator Hop(Vector3 to, Vector3? face)
        {
            var from = transform.position;
            float t = 0.38f;
            var flat = new Vector3(to.x - from.x, 0, to.z - from.z);
            if (flat.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(flat, Vector3.up);
            for (float k = 0; k < 1; k += Time.deltaTime / t)
            {
                transform.position = Vector3.Lerp(from, to, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.35f);
                yield return null;
            }
            transform.position = to;
            if (face.HasValue) transform.rotation = Quaternion.LookRotation(face.Value, Vector3.up);
        }
    }
}
