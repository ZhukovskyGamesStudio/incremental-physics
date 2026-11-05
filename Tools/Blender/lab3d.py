# The 3D lab's models, built by code so they can be rebuilt and tweaked: one room (floor of planks, walls with two
# windows and a door, ceiling, a lamp), a table, a crate of samples and the first apparatus, the dynamometer.
# Every piece is a separate FBX in Assets/Lab3D/Models; Unity gives them the chalk material by object name.
#   blender -b --factory-startup -P Tools/Blender/lab3d.py -- <out dir>
# Blender is Z-up in metres; the export turns it into Unity's Y-up. Pivots are where Unity needs them: the spring
# hangs from its top, the hook from its eye, everything else stands on its base.
import bpy, bmesh, math, os, sys
from mathutils import Vector, Matrix

OUT = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else os.path.join(os.getcwd(), "Assets", "Lab3D", "Models")
os.makedirs(OUT, exist_ok=True)


def clear():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()
    for m in list(bpy.data.meshes):
        bpy.data.meshes.remove(m)


def obj(name, bm):
    me = bpy.data.meshes.new(name)
    bm.normal_update()
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(o)
    return o


def box(bm, x0, y0, z0, x1, y1, z1):
    """An axis-aligned box from corner to corner, added to bm."""
    r = bmesh.ops.create_cube(bm, size=1.0)
    s = Vector((x1 - x0, y1 - y0, z1 - z0))
    c = Vector(((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2))
    bmesh.ops.transform(bm, matrix=Matrix.Translation(c) @ Matrix.Diagonal(s.to_4d()), verts=r["verts"])


def tube(bm, path, r, sides=6, closed=False):
    """A wire of radius r along a polyline: rings at every point, turned with the path."""
    rings = []
    n = len(path)
    for i, p in enumerate(path):
        a = path[max(i - 1, 0)] if not closed else path[(i - 1) % n]
        b = path[min(i + 1, n - 1)] if not closed else path[(i + 1) % n]
        t = (b - a).normalized()
        up = Vector((0, 0, 1)) if abs(t.z) < 0.9 else Vector((1, 0, 0))
        u = t.cross(up).normalized()
        v = t.cross(u).normalized()
        ring = [bm.verts.new(p + (u * math.cos(k / sides * 2 * math.pi) + v * math.sin(k / sides * 2 * math.pi)) * r)
                for k in range(sides)]
        rings.append(ring)
    segs = n if closed else n - 1
    for i in range(segs):
        A, B = rings[i], rings[(i + 1) % n]
        for k in range(sides):
            bm.faces.new((A[k], A[(k + 1) % sides], B[(k + 1) % sides], B[k]))
    if not closed:
        bm.faces.new(list(reversed(rings[0])))
        bm.faces.new(rings[-1])


def cylinder(bm, x, y, z0, z1, r, sides=12):
    tube(bm, [Vector((x, y, z0)), Vector((x, y, z1))], r, sides)


def export(name, objs):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, name + ".fbx"), use_selection=True,
                             object_types={"MESH"}, apply_scale_options="FBX_SCALE_ALL",
                             axis_forward="-Z", axis_up="Y", bake_space_transform=True,
                             mesh_smooth_type="FACE", use_mesh_modifiers=True, add_leaf_bones=False)
    print("exported", name)


# ---------------------------------------------------------------- room: 6 m wide (x), 7 m deep (y), 3 m high
W, D, H, T = 6.0, 7.0, 3.0, 0.15
X0, X1, Y0, Y1 = -W / 2, W / 2, -D / 2, D / 2
WIN_W, WIN_H, SILL = 1.3, 1.5, 0.9      # windows in the far wall (y = Y1)
WINDOWS = [-1.4, 1.4]
DOOR_W, DOOR_H, DOOR_Y = 0.95, 2.1, -2.0   # door in the left wall (x = X0)


def wall_with_holes(bm, along, fixed, thick, lo, hi, holes):
    """A wall from lo to hi along one axis ('x' or 'y'), its inner face at `fixed`, outward by `thick`;
    holes are (centre, bottom, width, height). Built of boxes around the holes, so no booleans."""
    cuts = sorted(holes)
    u = lo
    for c, b, w, h in cuts:
        segs = [(u, c - w / 2, 0, H), (c - w / 2, c + w / 2, 0, b), (c - w / 2, c + w / 2, b + h, H)]
        for a0, a1, z0, z1 in segs:
            if a1 - a0 < 1e-4 or z1 - z0 < 1e-4:
                continue
            if along == "x":
                box(bm, a0, fixed, z0, a1, fixed + thick, z1)
            else:
                box(bm, fixed, a0, z0, fixed + thick, a1, z1)
        u = c + w / 2
    if along == "x":
        box(bm, u, fixed, 0, hi, fixed + thick, H)
    else:
        box(bm, fixed, u, 0, fixed + thick, hi, H)


def build_room():
    clear()
    objs = []

    bm = bmesh.new()   # floor: planks along y with thin gaps; the slab under them shows through as lines
    box(bm, X0, Y0, -0.05, X1, Y1, -0.01)
    pw, gap = 0.30, 0.008
    x = X0
    while x < X1 - 1e-4:
        box(bm, x + gap / 2, Y0, -0.01, min(x + pw, X1) - gap / 2, Y1, 0.0)
        x += pw
    objs.append(obj("Floor", bm))

    bm = bmesh.new()
    box(bm, X0 - T, Y0 - T, H, X1 + T, Y1 + T, H + 0.1)
    objs.append(obj("Ceiling", bm))

    bm = bmesh.new()
    wall_with_holes(bm, "x", Y1, T, X0 - T, X1 + T, [(c, SILL, WIN_W, WIN_H) for c in WINDOWS])   # far wall
    wall_with_holes(bm, "x", Y0 - T, T, X0 - T, X1 + T, [(-1.95, 1.21, 1.15, 0.95)])             # near wall: the customers' window over the counter
    wall_with_holes(bm, "y", X0 - T, T, Y0, Y1, [(DOOR_Y, 0, DOOR_W, DOOR_H)])                  # left wall, door
    wall_with_holes(bm, "y", X1, T, Y0, Y1, [(0.6, SILL, WIN_W, WIN_H)])                         # right wall, a window
    objs.append(obj("Walls", bm))

    bm = bmesh.new()   # skirting board along the walls, broken at the door
    sk, st = 0.10, 0.02
    box(bm, X0, Y1 - st, 0, X1, Y1, sk)
    box(bm, X0, Y0, 0, X1, Y0 + st, sk)
    box(bm, X1 - st, Y0, 0, X1, Y1, sk)
    box(bm, X0, Y0, 0, X0 + st, DOOR_Y - DOOR_W / 2, sk)
    box(bm, X0, DOOR_Y + DOOR_W / 2, 0, X0 + st, Y1, sk)
    # no skirting board: in the shade under the tables its chalked top read as a stray plane along the wall
    bm.free()

    bm = bmesh.new()   # window frames: a frame, a cross of mullions and a sill, in every opening
    f, fd = 0.06, 0.10

    def window_x(c):
        a, b, z0, z1, y = c - WIN_W / 2, c + WIN_W / 2, SILL, SILL + WIN_H, Y1 + T / 2
        box(bm, a, y - fd / 2, z0, a + f, y + fd / 2, z1)
        box(bm, b - f, y - fd / 2, z0, b, y + fd / 2, z1)
        box(bm, a, y - fd / 2, z1 - f, b, y + fd / 2, z1)
        box(bm, a, y - fd / 2, z0, b, y + fd / 2, z0 + f)
        box(bm, c - 0.02, y - 0.03, z0, c + 0.02, y + 0.03, z1)
        box(bm, a, y - 0.03, z0 + WIN_H * 0.62 - 0.02, b, y + 0.03, z0 + WIN_H * 0.62 + 0.02)
        box(bm, a - 0.08, Y1 - 0.12, z0 - 0.04, b + 0.08, Y1 + 0.02, z0)

    def window_y(c):
        a, b, z0, z1, x = c - WIN_W / 2, c + WIN_W / 2, SILL, SILL + WIN_H, X1 + T / 2
        box(bm, x - fd / 2, a, z0, x + fd / 2, a + f, z1)
        box(bm, x - fd / 2, b - f, z0, x + fd / 2, b, z1)
        box(bm, x - fd / 2, a, z1 - f, x + fd / 2, b, z1)
        box(bm, x - fd / 2, a, z0, x + fd / 2, b, z0 + f)
        box(bm, x - 0.03, c - 0.02, z0, x + 0.03, c + 0.02, z1)
        box(bm, x - 0.03, a, z0 + WIN_H * 0.62 - 0.02, x + 0.03, b, z0 + WIN_H * 0.62 + 0.02)
        box(bm, X1 - 0.12, a - 0.08, z0 - 0.04, X1 + 0.02, b + 0.08, z0)

    for c in WINDOWS:
        window_x(c)
    window_y(0.6)
    objs.append(obj("WindowFrames", bm))

    bm = bmesh.new()   # door frame (architrave) on the inner face
    a, b, x = DOOR_Y - DOOR_W / 2, DOOR_Y + DOOR_W / 2, X0
    box(bm, x, a - 0.08, 0, x + 0.03, a, DOOR_H + 0.08)
    box(bm, x, b, 0, x + 0.03, b + 0.08, DOOR_H + 0.08)
    box(bm, x, a - 0.08, DOOR_H, x + 0.03, b + 0.08, DOOR_H + 0.08)
    objs.append(obj("DoorFrame", bm))

    bm = bmesh.new()   # door leaf, plain: sunk panels drew half their edges in chalk and read as a stray stripe
    dx = X0 - T / 2
    box(bm, dx - 0.02, a, 0, dx + 0.02, b, DOOR_H)
    # the knob is the scene builder's (a round one, Lab3DBuilder): a lever here read as a stray stick
    objs.append(obj("Door", bm))

    bm = bmesh.new()   # lamp over the table: cord, cup and a cone shade, with an inside to it (seen from below)
    cylinder(bm, 0, 0, 2.2, H, 0.008, 6)
    outer = bmesh.ops.create_cone(bm, cap_ends=False, segments=24, radius1=0.28, radius2=0.06, depth=0.22)
    bmesh.ops.translate(bm, vec=Vector((0, 0, 2.12)), verts=outer["verts"])
    inner = bmesh.ops.create_cone(bm, cap_ends=False, segments=24, radius1=0.272, radius2=0.054, depth=0.21)
    bmesh.ops.translate(bm, vec=Vector((0, 0, 2.125)), verts=inner["verts"])
    bm.faces.ensure_lookup_table()
    inner_faces = list({f for v in inner["verts"] for f in v.link_faces})
    bmesh.ops.reverse_faces(bm, faces=inner_faces)
    # a rim joining the two at the bottom edge
    rim = [Vector((math.cos(a) * 0.276, math.sin(a) * 0.276, 2.012)) for a in [2 * math.pi * i / 24 for i in range(24)]]
    tube(bm, rim, 0.006, 6, closed=True)
    cylinder(bm, 0, 0, 2.2, 2.27, 0.05, 12)
    cylinder(bm, 0, 0, 2.13, 2.2, 0.02, 10)   # the socket
    objs.append(obj("Lamp", bm))

    bm = bmesh.new()   # the bulb in the socket: a pear of glass
    ball = bmesh.ops.create_uvsphere(bm, u_segments=16, v_segments=10, radius=0.042)
    bmesh.ops.translate(bm, vec=Vector((0, 0, 2.085)), verts=ball["verts"])
    cylinder(bm, 0, 0, 2.11, 2.135, 0.018, 10)
    objs.append(obj("Bulb", bm))

    export("Room", objs)


def build_table():
    clear()
    bm = bmesh.new()
    L, Wd, Ht, th, leg = 1.6, 0.8, 1.21, 0.05, 0.06   # tall lab tables: the apparatus at the eyes, not the knees
    box(bm, -L / 2, -Wd / 2, Ht - th, L / 2, Wd / 2, Ht)
    for sx in (-1, 1):
        for sy in (-1, 1):
            x, y = sx * (L / 2 - 0.08), sy * (Wd / 2 - 0.08)
            box(bm, x - leg / 2, y - leg / 2, 0, x + leg / 2, y + leg / 2, Ht - th)
    for sy in (-1, 1):   # aprons under the top and a low stretcher
        y = sy * (Wd / 2 - 0.08)
        box(bm, -L / 2 + 0.08, y - 0.01, Ht - th - 0.1, L / 2 - 0.08, y + 0.01, Ht - th)
    for sx in (-1, 1):
        x = sx * (L / 2 - 0.08)
        box(bm, x - 0.01, -Wd / 2 + 0.08, Ht - th - 0.1, x + 0.01, Wd / 2 - 0.08, Ht - th)
    # low stretchers: one across each end, joining its two legs, and one down the middle joining those
    for sx in (-1, 1):
        x = sx * (L / 2 - 0.08)
        box(bm, x - 0.015, -Wd / 2 + 0.08, 0.18, x + 0.015, Wd / 2 - 0.08, 0.21)
    box(bm, -L / 2 + 0.08, -0.015, 0.18, L / 2 - 0.08, 0.015, 0.21)
    export("Table", [obj("Table", bm)])


def build_crate():
    clear()
    bm = bmesh.new()
    L, Wd, Ht, t = 0.40, 0.26, 0.09, 0.012
    box(bm, -L / 2, -Wd / 2, 0, L / 2, Wd / 2, t)
    box(bm, -L / 2, -Wd / 2, t, L / 2, -Wd / 2 + t, Ht)
    box(bm, -L / 2, Wd / 2 - t, t, L / 2, Wd / 2, Ht)
    box(bm, -L / 2, -Wd / 2 + t, t, -L / 2 + t, Wd / 2 - t, Ht)
    box(bm, L / 2 - t, -Wd / 2 + t, t, L / 2, Wd / 2 - t, Ht)
    export("Crate", [obj("Crate", bm)])


def build_sample():
    clear()
    bm = bmesh.new()
    box(bm, -0.5, -0.5, 0, 0.5, 0.5, 1)   # unit cube standing on its base; Unity scales it by volume
    export("Sample", [obj("Sample", bm)])


SPRING_LEN, SPRING_R, COILS = 0.16, 0.022, 11


def build_dynamometer():
    clear()
    objs = []
    post_x, top = -0.12, 0.76   # tall enough for a big sample at the bottom of the scale

    bm = bmesh.new()   # stand: heavy base, post, arm, a clamp at the arm's end
    box(bm, -0.18, -0.10, 0, 0.10, 0.10, 0.03)
    cylinder(bm, post_x, 0, 0.03, top + 0.03, 0.012, 10)
    box(bm, post_x - 0.025, -0.025, 0.03, post_x + 0.025, 0.025, 0.06)
    box(bm, post_x - 0.02, -0.012, top - 0.01, 0.095, 0.012, top + 0.01)   # the arm, out over the scale too
    box(bm, -0.012, -0.016, top - 0.025, 0.012, 0.016, top + 0.012)
    box(bm, post_x - 0.025, -0.02, top - 0.02, post_x + 0.025, 0.02, top + 0.02)
    objs.append(obj("Stand", bm))

    bm = bmesh.new()   # the scale: a plate behind the spring, standing on the base and held by a bracket off the arm;
    sx, sy = 0.065, 0.05    # its divisions are laid on it by the game (they get finer with the upgrades)
    zero = top - 0.025 - SPRING_LEN - 0.03
    box(bm, sx - 0.022, sy, 0.03, sx + 0.022, sy + 0.008, top - 0.01)
    box(bm, sx - 0.03, sy - 0.006, 0.03, sx + 0.03, sy + 0.02, 0.045)        # its foot on the base
    box(bm, sx - 0.012, 0.0, top - 0.012, sx + 0.012, sy + 0.008, top + 0.008)  # the bracket from the arm to its top
    objs.append(obj("Scale", bm))

    bm = bmesh.new()   # the zero tick, separate so Unity can chalk it green
    box(bm, sx - 0.022, sy - 0.004, zero - 0.002, sx + 0.002, sy, zero + 0.002)
    objs.append(obj("ZeroMark", bm))

    bm = bmesh.new()   # spring: hangs from its top (origin), coils down to -SPRING_LEN; Unity stretches it in Y
    pts = []
    steps = COILS * 14
    pts.append(Vector((0, 0, 0)))
    for i in range(steps + 1):
        a = i / steps
        ang = a * COILS * 2 * math.pi
        pts.append(Vector((math.cos(ang) * SPRING_R, math.sin(ang) * SPRING_R, -0.012 - a * (SPRING_LEN - 0.024))))
    pts.append(Vector((0, 0, -SPRING_LEN)))
    tube(bm, pts, 0.0024, 6)
    o = obj("Spring", bm)
    o.location = (0, 0, top - 0.025)
    objs.append(o)

    bm = bmesh.new()   # hook: an eye at the top (origin), a shank and a J
    eye = [Vector((0, math.sin(k / 12 * 2 * math.pi) * 0.008, -0.008 + math.cos(k / 12 * 2 * math.pi) * 0.008))
           for k in range(12)]
    tube(bm, eye, 0.002, 6, closed=True)
    j = [Vector((0, 0, -0.016)), Vector((0, 0, -0.045))]
    for k in range(1, 10):
        a = math.pi * k / 9 * 1.15
        j.append(Vector((0, -0.012 + math.cos(a) * 0.012, -0.045 - math.sin(a) * 0.012)))
    tube(bm, j, 0.0022, 6)
    box(bm, 0, -0.0015, -0.0315, sx - 0.02, 0.0015, -0.0285)   # the pointer: an L out to the scale, zero at rest
    box(bm, sx - 0.023, -0.0015, -0.0315, sx - 0.02, sy - 0.004, -0.0285)
    o = obj("Hook", bm)
    o.location = (0, 0, top - 0.025 - SPRING_LEN)
    objs.append(o)

    export("Dynamometer", objs)


def helix(bm, r, length, coils, wire, down):
    """A coil spring from z = 0 to -length (down) or +length (up), with straight ends on the axis."""
    sgn = -1 if down else 1
    steps = coils * 14
    pts = [Vector((0, 0, 0))]
    for i in range(steps + 1):
        a = i / steps
        ang = a * coils * 2 * math.pi
        pts.append(Vector((math.cos(ang) * r, math.sin(ang) * r, sgn * (0.012 + a * (length - 0.024)))))
    pts.append(Vector((0, 0, sgn * length)))
    tube(bm, pts, wire, 6)


def ruler(bm, x, y, z0, z1, n, w=0.025):
    """A plate standing behind an apparatus (its front at y) with n ticks, every fifth longer."""
    box(bm, x - w, y, z0, x + w, y + 0.008, z1)
    for i in range(1, n + 1):
        z = z0 + (z1 - z0) * i / (n + 1)
        tw = 0.022 if i % 5 == 0 else 0.012
        box(bm, x - w, y - 0.003, z - 0.0015, x - w + tw, y, z + 0.0015)


SHELF_REST, SHELF_TOP = 0.17, 0.76   # the carriage rests on the stake's top (0.15) and rises to the top bar


def build_shelf():
    """The drop shelf (E = mgh): a carriage rides up two posts with the sample on it and falls onto a stake."""
    clear()
    objs = []
    bm = bmesh.new()
    box(bm, -0.15, -0.10, 0, 0.15, 0.10, 0.03)
    for sx in (-1, 1):
        cylinder(bm, sx * 0.07, 0, 0.03, 0.88, 0.009, 10)
    box(bm, -0.09, -0.015, 0.86, 0.09, 0.015, 0.89)
    box(bm, -0.04, -0.04, 0.03, 0.04, 0.04, 0.05)            # the block the stake is driven into
    objs.append(obj("ShelfStand", bm))

    bm = bmesh.new()   # the impact scale: a narrow plate beside the stake, from the block up past the stake's top;
    # its divisions are laid on it by the game, in joules, down from the zero at the stake's top
    box(bm, 0.028, 0.012, 0.03, 0.062, 0.018, 0.175)
    objs.append(obj("ImpactScale", bm))
    bm = bmesh.new()
    box(bm, 0.028, 0.008, 0.149, 0.048, 0.012, 0.151)
    objs.append(obj("ImpactZero", bm))

    bm = bmesh.new()
    ruler(bm, 0, 0.07, 0.10, 0.86, 9)
    objs.append(obj("Ruler", bm))

    bm = bmesh.new()   # stake: its top is the origin, the rest is in the block; a blow drives it down along the scale
    cylinder(bm, 0, 0, -0.12, 0, 0.013, 10)
    o = obj("Stake", bm)
    o.location = (0, 0, 0.15)
    objs.append(o)

    bm = bmesh.new()   # carriage: a platform (top at the origin) with a sleeve round each post
    box(bm, -0.055, -0.05, -0.012, 0.055, 0.05, 0)
    for sx in (-1, 1):
        box(bm, sx * 0.07 - 0.016, -0.016, -0.03, sx * 0.07 + 0.016, 0.016, 0.004)
        box(bm, min(sx * 0.055, sx * 0.07), -0.008, -0.010, max(sx * 0.055, sx * 0.07), 0.008, -0.002)
    o = obj("Carriage", bm)
    o.location = (0, 0, SHELF_REST)
    objs.append(o)
    export("Shelf", objs)


def build_launcher():
    """The spring launcher (E = kx²/2): press the plate down, let go, the sample jumps up along a ruler."""
    clear()
    objs = []
    bm = bmesh.new()
    box(bm, -0.12, -0.12, 0, 0.12, 0.12, 0.04)
    box(bm, -0.08, -0.08, 0.04, 0.08, 0.08, 0.05)
    objs.append(obj("LauncherStand", bm))

    bm = bmesh.new()
    ruler(bm, 0, 0.11, 0.06, 0.95, 14, 0.03)
    box(bm, -0.04, 0.10, 0.95, 0.04, 0.13, 0.97)
    objs.append(obj("Ruler", bm))

    bm = bmesh.new()   # spring: stands on its bottom (origin), Unity squeezes it in Y
    helix(bm, 0.045, 0.18, 7, 0.0045, down=False)
    o = obj("LaunchSpring", bm)
    o.location = (0, 0, 0.05)
    objs.append(o)

    bm = bmesh.new()   # plate: its top is the origin
    box(bm, -0.065, -0.065, -0.012, 0.065, 0.065, 0)
    o = obj("Plate", bm)
    o.location = (0, 0, 0.05 + 0.18 + 0.012)
    objs.append(o)
    export("Launcher", objs)


PEND_PIVOT = 0.88


def build_pendulum():
    """The pendulum (T = 2π√(l/g)): a thread from a pivot on an arm, a protractor behind it."""
    clear()
    objs = []
    bm = bmesh.new()
    box(bm, -0.56, -0.12, 0, 0.12, 0.12, 0.03)   # the post stands well aside: a full swing must not reach it
    cylinder(bm, -0.5, 0, 0.03, PEND_PIVOT + 0.03, 0.013, 10)
    box(bm, -0.52, -0.015, PEND_PIVOT + 0.005, 0.025, 0.015, PEND_PIVOT + 0.03)
    box(bm, -0.012, -0.02, PEND_PIVOT - 0.01, 0.012, 0.02, PEND_PIVOT + 0.03)
    objs.append(obj("PendStand", bm))

    bm = bmesh.new()   # protractor: an arc of ±60° under the pivot, with a tick every 15°
    arc = [Vector((math.sin(a) * 0.16, 0.03, PEND_PIVOT - math.cos(a) * 0.16))
           for a in [math.radians(-60 + 120 * i / 24) for i in range(25)]]
    tube(bm, arc, 0.003, 6)
    for k in range(-4, 5):
        a = math.radians(15 * k)
        p0 = Vector((math.sin(a) * 0.16, 0.03, PEND_PIVOT - math.cos(a) * 0.16))
        p1 = Vector((math.sin(a) * 0.18, 0.03, PEND_PIVOT - math.cos(a) * 0.18))
        tube(bm, [p0, p1], 0.002, 4)
    tube(bm, [Vector((0, 0.03, PEND_PIVOT)), Vector((0, 0.03, PEND_PIVOT - 0.16))], 0.0015, 4)
    objs.append(obj("Protractor", bm))

    bm = bmesh.new()   # thread: one metre down from the pivot (origin); Unity sets its length
    tube(bm, [Vector((0, 0, 0)), Vector((0, 0, -1))], 0.0018, 4)
    cylinder(bm, 0, 0, -0.012, 0.012, 0.008, 8)
    o = obj("Thread", bm)
    o.location = (0, 0, PEND_PIVOT)
    objs.append(o)
    export("Pendulum", objs)


def build_board():
    """The blackboard on the wall: the letters are written on it and bought there. Its back is the origin, the
    writing side faces +y (into the room)."""
    clear()
    objs = []
    bm = bmesh.new()
    box(bm, -1.5, 0, 0.9, 1.5, 0.03, 2.3)
    objs.append(obj("BoardPanel", bm))
    bm = bmesh.new()
    f = 0.05
    box(bm, -1.5 - f, 0, 0.9 - f, 1.5 + f, 0.05, 0.9)
    box(bm, -1.5 - f, 0, 2.3, 1.5 + f, 0.05, 2.3 + f)
    box(bm, -1.5 - f, 0, 0.9, -1.5, 0.05, 2.3)
    box(bm, 1.5, 0, 0.9, 1.5 + f, 0.05, 2.3)
    box(bm, -1.4, 0.05, 0.86, 1.4, 0.11, 0.875)          # chalk tray and its lip
    box(bm, -1.4, 0.10, 0.875, 1.4, 0.11, 0.89)
    for x in (-0.9, -0.82, 0.6):
        tube(bm, [Vector((x, 0.08, 0.882)), Vector((x + 0.07, 0.085, 0.882))], 0.006, 6)
    objs.append(obj("BoardFrame", bm))
    export("Board", objs)


def build_clock():
    """The wall clock over the board: the day's time. Its back is the origin, the face looks +y (into the room);
    the hands turn round their own origins, the middle of the face."""
    clear()
    objs = []
    bm = bmesh.new()
    rim = [Vector((math.sin(a) * 0.2, 0.03, math.cos(a) * 0.2)) for a in [2 * math.pi * i / 40 for i in range(40)]]
    tube(bm, rim, 0.014, 6, closed=True)
    tube(bm, [Vector((0, 0, 0)), Vector((0, 0.02, 0))], 0.19, 32)        # the dial
    for i in range(12):
        a = 2 * math.pi * i / 12
        l = 0.035 if i % 3 == 0 else 0.018
        c, s = math.cos(a), math.sin(a)
        p0, p1 = Vector((s * 0.17, 0.024, c * 0.17)), Vector((s * (0.17 - l), 0.024, c * (0.17 - l)))
        tube(bm, [p0, p1], 0.004, 4)
    objs.append(obj("ClockFace", bm))
    for name, length, w, y in (("HourHand", 0.1, 0.007, 0.03), ("MinuteHand", 0.15, 0.004, 0.036)):
        bm = bmesh.new()
        box(bm, -w, -0.002, -0.02, w, 0.002, length)
        o = obj(name, bm)
        o.location = (0, y, 0)
        objs.append(o)
    bm = bmesh.new()
    tube(bm, [Vector((0, 0.02, 0)), Vector((0, 0.042, 0))], 0.01, 8)
    objs.append(obj("ClockPin", bm))
    export("Clock", objs)


def build_chalkie():
    """A chalk helper: a stick of chalk standing on end, worn round at the top, with two eyes and two thin arms.
    Its base is the origin; it looks along -y (Unity's +z)."""
    clear()
    objs = []
    bm = bmesh.new()
    r, h = 0.045, 0.34
    cylinder(bm, 0, 0, 0, h, r, 14)
    tube(bm, [Vector((0, 0, h)), Vector((0, 0, h + 0.022))], r * 0.78, 14)
    for sx in (-1, 1):
        tube(bm, [Vector((sx * r * 0.9, 0, h * 0.6)), Vector((sx * (r + 0.035), -0.012, h * 0.42)), Vector((sx * (r + 0.045), -0.02, h * 0.3))], 0.008, 5)
    objs.append(obj("Body", bm))
    bm = bmesh.new()
    for sx in (-1, 1):
        box(bm, sx * 0.016 - 0.0055, -r - 0.004, h * 0.74 - 0.011, sx * 0.016 + 0.0055, -r + 0.003, h * 0.74 + 0.011)
    objs.append(obj("Eyes", bm))
    export("Chalkie", objs)


build_room()
build_table()
build_crate()
build_sample()
build_dynamometer()
build_shelf()
build_launcher()
build_pendulum()
build_board()
build_clock()
build_chalkie()
print("done ->", OUT)
