# Shared modelling kit for the lab's Blender scripts that build models the game instantiates at runtime
# (customers.py; the odd departments and desk items next). Import it from a script run by Blender:
#     sys.path.insert(0, os.path.dirname(os.path.abspath(__file__))); from kit import *
#
# CONVENTIONS (the game relies on them: Assets/Scripts/Lab3D/ModelLibrary.cs, PersonaKit.Paint)
# - Coordinates. Every helper here takes Unity's own axes in metres: x right, y up, z forward (where the thing looks).
#   Model.export turns them into Blender's (x -> -x, y -> z, z -> -y), and the FBX export (as in lab3d.py) brings
#   them back into Unity unchanged: a point (0.1, 1.5, 0.2) here is at localPosition (0.1, 1.5, 0.2) of the instance.
#   The model's origin is its pivot: a figure stands on it, feet at y = 0, facing +z.
# - Files. One model a file: <Models>/<Group>/<Group>_<name>.fbx, e.g. Assets/Lab3D/Models/Customers/Persona_sailor.fbx.
#   Unity's ModelLibrary holds every FBX under Assets/Lab3D/Models by its file name ("Persona_sailor"), so file
#   names must be unique across the folders.
# - Colours. Every mesh object is named "<Part>.<colour>", e.g. "Coat.blue", "Skin.skin", "Helmet.clear"; at runtime
#   PersonaKit.Paint gives it the chalk material of that colour. The colours are the kit's: skin, dark, white, red,
#   blue, green, yellow, brown, grey, orange, purple, metal, clear, pink, water, line (COLOURS below; add one there
#   and in PersonaKit together). A name without a known colour keeps the material it has.
# - Markers. Empties are exported too and come in as plain child transforms: "Top" marks a customer's head-top
#   height (where the "!" and the stars go), "Seat" where a sample is put. Every model also has at least two
#   top-level objects (a marker counts), so that Unity keeps each object's own name instead of folding a single one
#   into the file's root.
# - Moving pieces. A piece the game moves, turns or scales (a cat's head, a flap of a lid, a key of a calculator) is
#   one or more parts named "<Group>-<Part>.<colour>" ("Head-Fur.orange", "FlapL-Board.brown"), modelled where it
#   rests, and a marker named <Group> at the point it moves about. At runtime Art3D.Pivot makes that marker the
#   pivot and puts the group's parts under it, so the code moves the marker (Model.apply builds a group in its own
#   space and then puts it in place).
# - Shading. Faces are smooth; edges sharper than SHARP degrees are split, so the chalk outlines (drawn where depth
#   breaks and where the surface turns) fall on silhouettes and real creases only. Keep forms simple and closed:
#   every piece made here is a closed solid (normals are recomputed outward), pieces may sink into each other.
#
# Preview: preview(models, path, yaw) renders the models side by side with Workbench (object colours, outlines),
# to look at them without Unity.
import bpy, bmesh, math, os, sys
from mathutils import Vector, Matrix, Euler

SHARP = 38.0   # degrees: edges turning more than this are creases

# the kit's colours (for the preview only; the game uses its chalk materials of the same names)
COLOURS = {
    "skin": (1.0, 0.82, 0.68), "dark": (0.3, 0.29, 0.29), "white": (0.93, 0.93, 0.89), "red": (1.0, 0.45, 0.4),
    "blue": (0.5, 0.65, 1.0), "green": (0.5, 0.88, 0.55), "yellow": (1.0, 0.88, 0.4), "brown": (0.78, 0.55, 0.36),
    "grey": (0.7, 0.7, 0.7), "orange": (1.0, 0.62, 0.3), "purple": (0.78, 0.58, 1.0), "metal": (0.62, 0.74, 0.85),
    "clear": (0.8, 0.92, 1.0), "pink": (1.0, 0.62, 0.72), "water": (0.4, 0.68, 0.92), "line": (0.95, 0.95, 0.95),
}

U2B = Matrix(((-1, 0, 0), (0, 0, -1), (0, 1, 0)))   # Unity axes -> Blender axes (a mirror: faces are re-oriented)


def clear():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()
    for m in list(bpy.data.meshes):
        bpy.data.meshes.remove(m)


def V(*p):
    return Vector(p[0]) if len(p) == 1 else Vector(p)


def lerp(a, b, t):
    return a + (b - a) * t


def rot(euler_deg):
    """A rotation matrix from Unity's Euler angles (degrees; z, then x, then y, as Quaternion.Euler)."""
    return Euler([math.radians(a) for a in euler_deg], "ZXY").to_matrix()


def interp(prof, y, k):
    """Value k of a profile [(y, ...), ...] sorted by y, at height y (clamped at the ends)."""
    if y <= prof[0][0]:
        return prof[0][k]
    for a, b in zip(prof, prof[1:]):
        if y <= b[0]:
            t = (y - a[0]) / max(b[0] - a[0], 1e-9)
            return lerp(a[k], b[k], t)
    return prof[-1][k]


def sq(s, n):
    """A superellipse's coordinate: n = 2 a circle, more is boxier."""
    return s if n == 2 else math.copysign(abs(s) ** (2.0 / n), s)


def curve(prof, k):
    """A profile with k more points between every two, on a Catmull-Rom curve through them (radii kept >= 0)."""
    full = []
    for it in prof:
        rx = it[1]
        rz = it[2] if len(it) > 2 and it[2] is not None else rx
        full.append((it[0], rx, rz, it[3] if len(it) > 3 else 0, it[4] if len(it) > 4 else 0))
    out = []
    for i in range(len(full) - 1):
        p0, p1, p2, p3 = full[max(i - 1, 0)], full[i], full[i + 1], full[min(i + 2, len(full) - 1)]
        out.append(p1)
        for j in range(1, k + 1):
            t = j / (k + 1)
            c = [0.5 * (2 * b + (-a + cc) * t + (2 * a - 5 * b + 4 * cc - d) * t * t + (-a + 3 * b - 3 * cc + d) * t ** 3)
                 for a, b, cc, d in zip(p0, p1, p2, p3)]
            c[1], c[2] = max(c[1], 1e-5), max(c[2], 1e-5)
            out.append(tuple(c))
    out.append(full[-1])
    return out


class Part:
    """One mesh object of a model: built in Unity axes into a bmesh, every helper adding a closed solid."""

    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()

    def v(self, p):
        return self.bm.verts.new(Vector(p))

    def f(self, vs):
        if len(set(vs)) < 3:
            return None
        try:
            return self.bm.faces.new(vs)
        except ValueError:
            return None

    def _rings(self, rings, cap0=True, cap1=True, loop=True):
        """Faces between consecutive rings of verts (a ring of one vert is a pole); flat caps on open ends."""
        for A, B in zip(rings, rings[1:]):
            if len(A) == 1 and len(B) == 1:
                continue
            if len(A) == 1:
                for k in range(len(B) - (0 if loop else 1)):
                    self.f((A[0], B[k], B[(k + 1) % len(B)]))
            elif len(B) == 1:
                for k in range(len(A) - (0 if loop else 1)):
                    self.f((A[k], A[(k + 1) % len(A)], B[0]))
            else:
                for k in range(len(A) - (0 if loop else 1)):
                    self.f((A[k], A[(k + 1) % len(A)], B[(k + 1) % len(B)], B[k]))
        if cap0 and len(rings[0]) > 2:
            self.f(list(rings[0]))
        if cap1 and len(rings[-1]) > 2:
            self.f(list(rings[-1]))

    def lathe(self, prof, segs=24, n=2.0, wave=None, at=(0, 0, 0), m=None, smooth=0):
        """A solid turned round the y axis: prof is [(y, rx[, rz[, cx, cz]]), ...] from bottom to top; a radius of 0
        is a pole, otherwise the end is capped flat. n > 2 makes the section boxier; wave(a) adds to the radius at
        angle a (0 = front, +z). m rotates it about `at`. smooth = k puts k rings more between every two, on a
        curve through them (a rounded body rather than a turned one)."""
        if smooth:
            prof = curve(prof, smooth)
        rings, made = [], []
        for it in prof:
            y, rx = it[0], it[1]
            rz = it[2] if len(it) > 2 and it[2] is not None else rx
            cx = it[3] if len(it) > 3 else 0
            cz = it[4] if len(it) > 4 else 0
            if rx <= 1e-6:
                ring = [self.v((cx, y, cz))]
            else:
                ring = []
                for k in range(segs):
                    a = 2 * math.pi * k / segs
                    w = 1 + (wave(a) if wave else 0)
                    ring.append(self.v((cx + rx * w * sq(math.sin(a), n), y, cz + rz * w * sq(math.cos(a), n))))
            rings.append(ring)
            made += ring
        self._rings(rings)
        self._place(made, at, m)
        return made

    def ellipsoid(self, c, r, segs=16, rows=10, m=None):
        r = (r, r, r) if isinstance(r, (int, float)) else r
        prof = [(-r[1] * math.cos(math.pi * i / rows), r[0] * math.sin(math.pi * i / rows), r[2] * math.sin(math.pi * i / rows))
                for i in range(rows + 1)]
        prof[0] = (prof[0][0], 0, 0)
        prof[-1] = (prof[-1][0], 0, 0)
        return self.lathe(prof, segs, at=c, m=m)

    def _place(self, verts, at, m):
        at = Vector(at)
        for v in verts:
            p = v.co if m is None else m @ v.co
            v.co = p + at

    def tube(self, pts, radii, sides=12, caps=True):
        """A solid tube through points with a radius at each (a number for all), its ends rounded."""
        pts = [Vector(p) for p in pts]
        radii = [radii] * len(pts) if isinstance(radii, (int, float)) else list(radii)
        n = len(pts)
        T = [(pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]).normalized() for i in range(n)]
        ref = Vector((0, 1, 0)) if abs(T[0].y) < 0.9 else Vector((1, 0, 0))
        u = T[0].cross(ref).normalized()
        frames = []
        for i in range(n):
            if i > 0:
                u = T[i - 1].rotation_difference(T[i]) @ u
                u = (u - T[i] * u.dot(T[i])).normalized()
            frames.append((u, T[i].cross(u).normalized()))

        def ring(c, fr, r):
            return [self.v(c + (fr[0] * math.cos(2 * math.pi * k / sides) + fr[1] * math.sin(2 * math.pi * k / sides)) * r)
                    for k in range(sides)]

        rings = []
        if caps:   # a rounded start
            for th in (90, 60, 30):
                t = math.radians(th)
                if th == 90:
                    rings.append([self.v(pts[0] - T[0] * radii[0])])
                else:
                    rings.append(ring(pts[0] - T[0] * radii[0] * math.cos(t), frames[0], radii[0] * math.sin(t)))
        for i in range(n):
            rings.append(ring(pts[i], frames[i], radii[i]))
        if caps:
            for th in (30, 60, 90):
                t = math.radians(th)
                if th == 90:
                    rings.append([self.v(pts[-1] + T[-1] * radii[-1])])
                else:
                    rings.append(ring(pts[-1] + T[-1] * radii[-1] * math.sin(t), frames[-1], radii[-1] * math.cos(t)))
        self._rings(rings)

    def loop(self, pts, r, sides=6):
        """A closed ring of wire through points (a glasses' rim, a buckle)."""
        pts = [Vector(p) for p in pts]
        n = len(pts)
        rings = []
        for i in range(n):
            t = (pts[(i + 1) % n] - pts[i - 1]).normalized()
            up = Vector((0, 1, 0)) if abs(t.y) < 0.9 else Vector((1, 0, 0))
            a = t.cross(up).normalized()
            b = t.cross(a).normalized()
            rings.append([self.v(pts[i] + (a * math.cos(2 * math.pi * k / sides) + b * math.sin(2 * math.pi * k / sides)) * r)
                          for k in range(sides)])
        for i in range(n):
            A, B = rings[i], rings[(i + 1) % n]
            for k in range(sides):
                self.f((A[k], A[(k + 1) % sides], B[(k + 1) % sides], B[k]))

    def box(self, c, size, bevel=0.0, m=None, segments=2):
        """A box of `size` round its centre c, its edges rounded by `bevel` metres."""
        before = set(self.bm.verts)   # what is there already: the cube's verts and the bevel's are the new ones
        r = bmesh.ops.create_cube(self.bm, size=1.0)
        for v in r["verts"]:
            v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
        if bevel > 0:
            edges = list({e for v in r["verts"] for e in v.link_edges})
            bmesh.ops.bevel(self.bm, geom=edges, offset=bevel, segments=segments, profile=0.5, affect="EDGES",
                            clamp_overlap=True)
        c = Vector(c)
        for v in self.bm.verts:
            if v not in before:
                v.co = (v.co if m is None else m @ v.co) + c

    def prism(self, pts2, origin, ax_u, ax_v, thick):
        """A flat polygon (2D points in the plane of ax_u, ax_v through origin) given a thickness: a fin, a tail,
        a visor."""
        o, u, w = Vector(origin), Vector(ax_u).normalized(), Vector(ax_v).normalized()
        nrm = u.cross(w).normalized() * (thick / 2)
        top = [self.v(o + u * x + w * y + nrm) for x, y in pts2]
        bot = [self.v(o + u * x + w * y - nrm) for x, y in pts2]
        self.f(top)
        self.f(list(reversed(bot)))
        k = len(pts2)
        for i in range(k):
            self.f((top[i], bot[i], bot[(i + 1) % k], top[(i + 1) % k]))

    def cap(self, c, r, phimax, thick, down=False, segs=28, rows=8):
        """A shell over part of an ellipsoid (hair, a headscarf, a beard): from its top pole (or bottom, `down`) out to
        the polar angle phimax(a) at each angle a round it (0 = front), `thick` thick inwards."""
        c = Vector(c)
        r = Vector(r)
        sy = -1 if down else 1

        def p(phi, a, k):
            d = Vector((math.sin(phi) * math.sin(a), sy * math.cos(phi), math.sin(phi) * math.cos(a)))
            return c + Vector((d.x * (r.x - k), d.y * (r.y - k), d.z * (r.z - k)))

        outer, inner = [[self.v(p(0, 0, 0))]], [[self.v(p(0, 0, thick))]]
        for i in range(1, rows + 1):
            o, n = [], []
            for k in range(segs):
                a = 2 * math.pi * k / segs
                phi = phimax(a) * i / rows
                o.append(self.v(p(phi, a, 0)))
                n.append(self.v(p(phi, a, thick)))
            outer.append(o)
            inner.append(n)
        self._rings(outer, cap0=False, cap1=False)
        self._rings(inner, cap0=False, cap1=False)
        A, B = outer[-1], inner[-1]
        for k in range(segs):
            self.f((A[k], A[(k + 1) % segs], B[(k + 1) % segs], B[k]))

    def sheet(self, shape, ys, a0, a1, out, inn, segs=24, n=2.0):
        """A layer lying on a turned body (an apron, a cape, a collar, a stripe round a vest): shape(y) gives the
        body's (rx, rz, cx, cz) at height y; the layer covers angles a0..a1 (radians, 0 = front, a full turn closes
        it round) between the heights ys, from `inn` to `out` metres off the body."""
        full = abs((a1 - a0) - 2 * math.pi) < 1e-6
        cols = segs if full else segs + 1

        def grid(off):
            g = []
            for y in ys:
                rx, rz, cx, cz = shape(y)
                row = []
                for k in range(cols):
                    a = a0 + (a1 - a0) * k / segs
                    row.append(self.v((cx + (rx + off) * sq(math.sin(a), n), y, cz + (rz + off) * sq(math.cos(a), n))))
                g.append(row)
            return g

        O, I = grid(out), grid(inn)
        for G in (O, I):
            for A, B in zip(G, G[1:]):
                for k in range(cols - (0 if full else 1)):
                    self.f((A[k], A[(k + 1) % cols], B[(k + 1) % cols], B[k]))
        for row in (0, len(ys) - 1):   # top and bottom edges
            for k in range(cols - (0 if full else 1)):
                self.f((O[row][k], O[row][(k + 1) % cols], I[row][(k + 1) % cols], I[row][k]))
        if not full:   # the two side edges
            for k in (0, cols - 1):
                for j in range(len(ys) - 1):
                    self.f((O[j][k], O[j + 1][k], I[j + 1][k], I[j][k]))

    def strip(self, rows, surface, out, inn):
        """A flat band laid on a surface (a tie, a strap, a tape): rows are (x, y, half-width) down its length,
        surface(x, y, off) the point on the body at x, y lifted `off` off it."""
        O = [[self.v(surface(x - w, y, out)), self.v(surface(x + w, y, out))] for x, y, w in rows]
        I = [[self.v(surface(x - w, y, inn)), self.v(surface(x + w, y, inn))] for x, y, w in rows]
        for G in (O, I):
            for A, B in zip(G, G[1:]):
                self.f((A[0], A[1], B[1], B[0]))
        for j in range(len(rows) - 1):
            for k in (0, 1):
                self.f((O[j][k], O[j + 1][k], I[j + 1][k], I[j][k]))
        for j in (0, len(rows) - 1):
            self.f((O[j][0], O[j][1], I[j][1], I[j][0]))

    def apply(self, M=None, off=(0, 0, 0)):
        """Moves everything made so far: turned by the matrix M (about the origin), then shifted by off."""
        off = Vector(off)
        for v in self.bm.verts:
            v.co = (v.co if M is None else M @ v.co) + off

    def finish(self):
        """Into a Blender object: Blender axes, normals outward, smooth with creases split."""
        bm = self.bm
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
        for v in bm.verts:
            v.co = U2B @ v.co
        ngons = [f for f in bm.faces if len(f.verts) > 4]
        if ngons:
            bmesh.ops.triangulate(bm, faces=ngons)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        for f in bm.faces:
            f.smooth = True
        lim = math.radians(SHARP)
        for e in bm.edges:
            if len(e.link_faces) == 2 and e.calc_face_angle(0) > lim:
                e.smooth = False
        me = bpy.data.meshes.new(self.name)
        bm.normal_update()
        bm.to_mesh(me)
        bm.free()
        o = bpy.data.objects.new(self.name, me)
        bpy.context.collection.objects.link(o)
        col = COLOURS.get(self.name.split(".")[1] if "." in self.name else "", (0.9, 0.9, 0.9))
        o.color = (col[0], col[1], col[2], 0.35 if self.name.endswith(".clear") else 1.0)
        return o


class Model:
    """A model of several parts, one per name ("Coat.blue"), and markers (empties) at points in Unity axes."""

    def __init__(self, name):
        self.name = name
        self.parts = {}
        self.marks = {}

    def __call__(self, part, colour):
        key = f"{part}.{colour}"
        if key not in self.parts:
            self.parts[key] = Part(key)
        return self.parts[key]

    def mark(self, name, at):
        self.marks[name] = Vector(at)

    def apply(self, prefix, M=None, off=(0, 0, 0), marks=()):
        """Moves every part whose name starts with prefix (and the markers named in marks): a group built in its own
        space put in place."""
        for key, p in self.parts.items():
            if key.startswith(prefix):
                p.apply(M, off)
        for name in marks:
            self.marks[name] = (self.marks[name] if M is None else M @ self.marks[name]) + Vector(off)

    def build(self):
        objs = [p.finish() for p in self.parts.values()]
        for name, at in self.marks.items():
            e = bpy.data.objects.new(name, None)
            e.location = U2B @ at
            e.empty_display_size = 0.05
            bpy.context.collection.objects.link(e)
            objs.append(e)
        return objs

    def export(self, folder):
        """Builds it and writes <folder>/<name>.fbx; returns its Blender objects (kept for a preview)."""
        os.makedirs(folder, exist_ok=True)
        objs = self.build()
        bpy.ops.object.select_all(action="DESELECT")
        for o in objs:
            o.select_set(True)
        bpy.context.view_layer.objects.active = objs[0]
        bpy.ops.export_scene.fbx(filepath=os.path.join(folder, self.name + ".fbx"), use_selection=True,
                                 object_types={"MESH", "EMPTY"}, apply_scale_options="FBX_SCALE_ALL",
                                 axis_forward="-Z", axis_up="Y", bake_space_transform=True,
                                 mesh_smooth_type="FACE", use_mesh_modifiers=True, add_leaf_bones=False)
        tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in objs if o.type == "MESH")
        print(f"exported {self.name}: {tris} tris")
        return objs


def stash(objs, tag, offset):
    """Keeps a built model aside for the preview: renamed (so the next model gets clean names) and moved to `offset`
    (Unity axes) under an empty that can be turned."""
    root = bpy.data.objects.new("root:" + tag, None)
    bpy.context.collection.objects.link(root)
    root.location = U2B @ Vector(offset)
    for o in objs:
        o.name = tag + ":" + o.name
        if o.type == "MESH":
            o.data.name = o.name
        o.parent = root
    return root


def preview(roots, path, yaw=0.0, size=(2400, 1400), height=2.4, ortho=None, look=None, pitch=0.0):
    """Workbench picture of the stashed models (their roots turned by yaw degrees), from the front (and pitch
    degrees from above)."""
    sc = bpy.context.scene
    try:
        sc.render.engine = "BLENDER_WORKBENCH"
    except TypeError:
        sc.render.engine = "BLENDER_EEVEE"
    sh = sc.display.shading
    sh.light = "STUDIO"
    sh.color_type = "OBJECT"
    sh.show_object_outline = True
    sh.object_outline_color = (0.05, 0.05, 0.05)
    sh.show_cavity = True
    sh.cavity_type = "WORLD"
    sh.show_backface_culling = True
    sc.render.resolution_x, sc.render.resolution_y = size
    sc.render.resolution_percentage = 100
    sc.render.film_transparent = False
    if sc.world is None:
        sc.world = bpy.data.worlds.new("World")
    sc.world.color = (0.12, 0.14, 0.13)
    for r in roots:
        r.rotation_euler = (0, 0, math.radians(-yaw))
    xs = [r.location.x for r in roots]
    cx = (min(xs) + max(xs)) / 2
    cam_data = bpy.data.cameras.new("cam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = ortho or (max(xs) - min(xs) + 1.2)
    cam = bpy.data.objects.new("cam", cam_data)
    bpy.context.collection.objects.link(cam)
    ys = [r.location.y for r in roots]
    p = math.radians(pitch)
    lz = look if look is not None else height / 2 + 0.1
    cam.location = (cx, min(ys) - 10 * math.cos(p), lz + 10 * math.sin(p))
    cam.rotation_euler = (math.radians(90 - pitch), 0, 0)
    sc.camera = cam
    sc.render.filepath = path
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cam)
    print("preview", path)


def preview_fit(root, path, yaw=25.0, pitch=25.0, size=(1200, 900), margin=1.15):
    """Workbench picture of one stashed model, turned by yaw and seen pitch degrees from above, filling the picture."""
    root.rotation_euler = (0, 0, math.radians(-yaw))
    bpy.context.view_layer.update()
    p = math.radians(pitch)
    fwd = Vector((0, math.cos(p), -math.sin(p)))      # the way the camera looks (Blender axes)
    up = Vector((0, math.sin(p), math.cos(p)))
    pts = [o.matrix_world @ Vector(c) for o in root.children if o.type == "MESH" for c in o.bound_box]
    xs = [q.x for q in pts]
    us = [q.dot(up) for q in pts]
    c = Vector(((min(xs) + max(xs)) / 2, 0, 0))
    cu = (min(us) + max(us)) / 2
    w, h = max(xs) - min(xs), max(us) - min(us)
    mid = sum(pts, Vector()) / len(pts)
    target = Vector((c.x, mid.y, mid.z)) + up * (cu - mid.dot(up))
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"
    sh = sc.display.shading
    sh.light, sh.color_type, sh.show_object_outline = "STUDIO", "OBJECT", True
    sh.object_outline_color = (0.05, 0.05, 0.05)
    sh.show_cavity, sh.cavity_type, sh.show_backface_culling = True, "WORLD", True
    sc.render.resolution_x, sc.render.resolution_y = size
    sc.render.resolution_percentage = 100
    if sc.world is None:
        sc.world = bpy.data.worlds.new("World")
    sc.world.color = (0.12, 0.14, 0.13)
    cam_data = bpy.data.cameras.new("cam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = margin * max(w, h * size[0] / size[1])
    cam = bpy.data.objects.new("cam", cam_data)
    bpy.context.collection.objects.link(cam)
    cam.location = target - fwd * 20
    cam.rotation_euler = (math.radians(90 - pitch), 0, 0)
    cam_data.clip_end = 100
    sc.camera = cam
    sc.render.filepath = path
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cam)
    print("preview", path)
