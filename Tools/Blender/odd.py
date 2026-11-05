# The odd departments' things (DESIGN.md 2б, «Крейзи-измерения»): the big tongue, the cat on its cushion, the pond
# with its rod and the five fish, the xylophone, the feather on its cushion, the mirror. One FBX each into
# <Models>/Odd/Odd_<name>.fbx; their scripts (Tongue3D, Cat3D, Rod3D, Xylo3D, Feather3D, Mirror3D) put them in at
# runtime through ModelLibrary and paint them by the colour in each part's name (see the conventions at the top of
# kit.py). A model's origin is its apparatus' transform: the table top (the pond: the floor), its front +z. What the
# game moves is a group "<Group>-<Part>.<colour>" with a marker <Group> where it turns (Art3D.Pivot); "Seat" marks
# where the sample goes.
#   blender -b --factory-startup -P Tools/Blender/odd.py -- <Models dir> [--preview <dir>] [--only tongue,cat]
import os, sys, math
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kit import *
from mathutils.bvhtree import BVHTree

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = os.path.join(argv[0] if argv and not argv[0].startswith("--") else os.path.join(os.getcwd(), "Assets", "Lab3D", "Models"), "Odd")
PREVIEW = argv[argv.index("--preview") + 1] if "--preview" in argv else None
ONLY = argv[argv.index("--only") + 1].split(",") if "--only" in argv else None


def surface_of(part):
    """The top of a part's surface: y at (x, z), found by a ray from above (None off it)."""
    tree = BVHTree.FromBMesh(part.bm)

    def y(x, z):
        for k in range(12):   # off its edge: the nearest point inwards
            hit = tree.ray_cast(Vector((x * 0.93 ** k, 2.0, z * 0.97 ** k)), Vector((0, -1, 0)))
            if hit[0] is not None:
                return hit[0].y
        return 0.0
    return y


def patch(part, cx, cz, r, surf, top=0.0025, bottom=-0.006, rings=5, segs=20, rz=None):
    """A flat lens lying on a curved surface (a taste's place on the tongue): its top `top` over the surface, its
    bottom sunk into it."""
    rz = rz or r
    upper, lower = [], []
    for k in range(rings + 1):
        if k == 0:
            y = surf(cx, cz)
            upper.append([part.v((cx, y + top, cz))])
            lower.append([part.v((cx, y + bottom, cz))])
            continue
        u, l = [], []
        for j in range(segs):
            a = 2 * math.pi * j / segs
            x, z = cx + r * k / rings * math.sin(a), cz + rz * k / rings * math.cos(a)
            y = surf(x, z)
            edge = top * (0.4 if k == rings else 1.0)
            u.append(part.v((x, y + edge, z)))
            l.append(part.v((x, y + bottom, z)))
        upper.append(u)
        lower.append(l)
    part._rings(upper, cap0=False, cap1=False)
    part._rings(lower, cap0=False, cap1=False)
    A, B = upper[-1], lower[-1]
    for j in range(segs):
        part.f((A[j], A[(j + 1) % segs], B[(j + 1) % segs], B[j]))


def disc(part, c, r, thick, segs=24, normal="y"):
    """A flat round disc (a coin of colour, a lily pad's body) round c, facing up (or +z)."""
    m = None if normal == "y" else rot((90, 0, 0))
    part.lathe([(-thick / 2, 0.0), (-thick / 2, r), (thick / 2, r), (thick / 2, 0.0)], segs=segs, at=c, m=m)


# ================================================================ the tongue

TASTE = ["yellow", "blue", "green", "purple", "orange"]   # sweet, salty, sour, bitter, umami (Tongue3D.Zones)
ZONE_AT = [(0, 0.19), (0.105, 0.04), (-0.105, 0.04), (0, -0.2), (0, -0.07)]
CARD_AT = [(0, 0.02, 0.31), (0.27, 0.02, 0.07), (-0.27, 0.02, 0.07), (0, 0.02, -0.33), (-0.27, 0.02, -0.17)]
CARD_LEAN = -28


def tongue(m):
    # a wooden board under it, wide enough for the cards round it
    m("Board", "brown").box((0, 0.01, -0.01), (0.72, 0.02, 0.7), bevel=0.006)
    # the tongue, tip to the front: one soft body, a groove down its middle, the root a little higher than the tip
    prof = [(-0.29, 0.0, 0.0), (-0.282, 0.07, 0.03), (-0.25, 0.135, 0.05), (-0.18, 0.158, 0.06), (-0.1, 0.154, 0.056),
            (-0.02, 0.15, 0.05), (0.06, 0.145, 0.046), (0.13, 0.132, 0.04), (0.19, 0.11, 0.033), (0.235, 0.078, 0.026),
            (0.262, 0.04, 0.018), (0.272, 0.0, 0.0)]
    body = m("Tongue-Body", "pink")
    body.lathe([(z, w, h) for z, w, h in prof], segs=40, wave=lambda a: -0.09 * math.exp(-((a - math.pi) / 0.22) ** 2),
               at=(0, 0.02, 0), m=rot((90, 0, 0)), smooth=2)
    surf = surface_of(body)
    m.mark("Tongue", (0, 0.02, 0))
    # the tastes' places: a lens of its colour lying on the tongue, and the seat just in it
    for i, (x, z) in enumerate(ZONE_AT):
        r = 0.034 if i == 0 else 0.038
        patch(m(f"Place{i}-Patch", TASTE[i]), x, z, r, surf, rz=r * (0.85 if i == 0 else 1))
        y = surf(x, z)
        m.mark(f"Place{i}", (x, y, z))
        m.mark(f"Seat{i}", (x, y - 0.006, z))
    # each taste's card: a small slate in a wooden frame standing on the board, leaning back on a strut; its colour as a
    # dot on the reader's left (the code writes the word on it)
    R = rot((CARD_LEAN, 0, 0))
    for i, at in enumerate(CARD_AT):
        g = f"Card{i}"
        m(f"{g}-Slate", "dark").box((0, 0.032, 0), (0.19, 0.064, 0.008), bevel=0.002)
        for x, y, w, h in ((0, 0.0, 0.196, 0.006), (0, 0.064, 0.196, 0.006), (-0.095, 0.032, 0.006, 0.07), (0.095, 0.032, 0.006, 0.07)):
            m(f"{g}-Frame", "brown").box((x, y, 0.001), (w, h, 0.011), bevel=0.0015)
        disc(m(f"{g}-Dot", TASTE[i]), (0.077, 0.032, 0.0045), 0.009, 0.0024, normal="z")
        m.apply(g + "-", R, at)
        top = Vector(at) + R @ Vector((0, 0.05, -0.004))
        m(f"{g}-Strut", "brown").tube([top, Vector((top.x, 0.02, top.z - 0.035))], 0.003, 6)
        m.mark(g, at)


# ================================================================ the cat

def cat(m):
    # the cushion, plump, with piping round it; the saucer in front of it
    cush = [(0.0, 0.0), (0.0, 0.235, 0.21), (0.012, 0.258, 0.232), (0.035, 0.262, 0.236), (0.055, 0.245, 0.22),
            (0.064, 0.17, 0.15), (0.066, 0.0)]
    m("Cushion", "red").lathe(cush, segs=36, n=2.6, at=(0.1, 0, -0.1), smooth=1)
    pts = []
    for k in range(48):
        a = 2 * math.pi * k / 48
        pts.append((0.1 + 0.262 * sq(math.sin(a), 2.6), 0.034, -0.1 + 0.236 * sq(math.cos(a), 2.6)))
    m("Piping", "yellow").loop(pts, 0.006, 6)
    sau = [(0.0, 0.0), (0.0, 0.055), (0.006, 0.062), (0.01, 0.09), (0.017, 0.118), (0.022, 0.126), (0.025, 0.122),
           (0.019, 0.11), (0.012, 0.08), (0.011, 0.0)]
    m("Saucer", "white").lathe(sau, segs=32, at=(-0.05, 0, 0.24))
    m.mark("Seat", (-0.05, 0.012, 0.24))

    # the cat itself, in its own space (Cat3D: the cat's origin is on the cushion, turned a little), lying curled up
    prof = [(-0.2, 0.0, 0.0, 0, -0.07), (-0.19, 0.075, 0.06, 0, -0.07), (-0.15, 0.115, 0.085, 0, -0.085),
            (-0.06, 0.125, 0.09, 0, -0.09), (0.04, 0.117, 0.086, 0, -0.088), (0.12, 0.09, 0.076, 0, -0.082),
            (0.16, 0.055, 0.055, 0, -0.08), (0.175, 0.0, 0.0, 0, -0.08)]
    up = rot((90, 0, 0))   # the lathe's axis along the body (+z), its ring's z down
    m("Body-Fur", "orange").lathe(prof, segs=28, n=2.2, m=up, smooth=2)
    for s in (-1, 1):   # the haunches
        m("Body-Fur", "orange").ellipsoid((s * 0.098, 0.062, -0.11), (0.05, 0.06, 0.085), 16, 10)
    m("Body-Chest", "white").ellipsoid((0, 0.075, 0.13), (0.066, 0.062, 0.048), 16, 10)
    # ginger stripes round the back, over the top only
    full = curve([(z, w, h, cx, cz) for z, w, h, cx, cz in prof], 2)

    def shape(y):
        return (interp(full, y, 1) * 1.0, interp(full, y, 2), 0.0, interp(full, y, 4))
    for z0 in (-0.13, -0.08, -0.03, 0.02, 0.07):
        m("Body-Stripes", "brown").sheet(shape, [z0, z0 + 0.011, z0 + 0.022], math.pi - 1.0, math.pi + 1.0, 0.0018, -0.006, segs=14, n=2.2)
    m.parts["Body-Stripes.brown"].apply(up)
    # the right front paw tucked under the chest; the left one (it bats) is its own piece
    m("Body-Fur", "orange").ellipsoid((0.06, 0.024, 0.165), (0.03, 0.022, 0.05), 12, 8)
    m("Body-Toes", "white").ellipsoid((0.06, 0.022, 0.205), (0.028, 0.019, 0.02), 12, 6)
    m("Paw-Fur", "orange").ellipsoid((-0.05, 0.027, 0.185), (0.03, 0.022, 0.05), 12, 8)
    m("Paw-Toes", "white").ellipsoid((-0.05, 0.024, 0.225), (0.028, 0.019, 0.02), 12, 6)
    m.mark("Paw", (-0.05, 0.03, 0.21))
    # the hissing back's fur: tufts along the ridge, shown only then
    for i in range(7):
        z = -0.15 + i * 0.045
        y = 0.09 + 0.085 * math.sqrt(max(0.0, 1 - ((z + 0.02) / 0.19) ** 2))
        m("Spikes-Fur", "orange").lathe([(0, 0.014), (0.05, 0.0)], segs=8, at=(0, y - 0.01, z), m=rot((-25 + i * 6, 0, (i % 2 - 0.5) * 30)))
    m.mark("Spikes", (0, 0, 0))
    # the tail, round the right haunch; it goes up when the cat hisses
    tail = [Vector(p) for p in [(0, 0, 0), (0.05, -0.025, -0.03), (0.11, -0.04, -0.02), (0.15, -0.045, 0.04), (0.16, -0.045, 0.12), (0.135, -0.045, 0.2)]]
    m("Tail-Fur", "orange").tube(tail[:5], [0.022, 0.021, 0.019, 0.018, 0.017], 10)
    m("Tail-Tip", "white").tube(tail[4:], [0.0172, 0.015], 10)
    m.apply("Tail-", None, (0.02, 0.06, -0.19))
    m.mark("Tail", (0.02, 0.06, -0.19))
    # the head, in its own space round its middle
    m("Head-Fur", "orange").ellipsoid((0, 0.006, 0), (0.075, 0.064, 0.068), 22, 14)
    m("Head-Fur", "orange").ellipsoid((0, -0.016, 0.01), (0.082, 0.048, 0.06), 20, 12)
    for s in (-1, 1):
        m("Head-Muzzle", "white").ellipsoid((s * 0.018, -0.026, 0.056), (0.024, 0.019, 0.019), 12, 8)
        for k in range(3):   # whiskers
            a = math.radians(-12 + k * 12)
            p0 = Vector((s * 0.034, -0.026 + k * 0.003, 0.062))
            m("Head-Whiskers", "white").tube([p0, p0 + Vector((s * 0.05 * math.cos(a), 0.05 * math.sin(a), -0.012)),
                                              p0 + Vector((s * 0.095 * math.cos(a), 0.095 * math.sin(a) - 0.008, -0.03))], 0.0013, 4)
        m("Head-Stripes", "brown").tube([(s * 0.012, 0.064, 0.02), (s * 0.016, 0.07, -0.01), (s * 0.016, 0.066, -0.04)], 0.004, 6)
    m("Head-Stripes", "brown").tube([(0, 0.067, 0.03), (0, 0.072, 0.0), (0, 0.068, -0.035)], 0.0045, 6)
    m("Head-Chin", "white").ellipsoid((0, -0.044, 0.046), (0.022, 0.012, 0.016), 10, 6)
    m("Head-Nose", "pink").ellipsoid((0, -0.009, 0.071), (0.011, 0.008, 0.007), 10, 6)
    m("Head-Mouth", "dark").tube([(-0.014, -0.034, 0.068), (-0.005, -0.038, 0.071), (0, -0.032, 0.073), (0.005, -0.038, 0.071), (0.014, -0.034, 0.068)], 0.0016, 5)
    for s, side in ((-1, "L"), (1, "R")):
        g = "Ear" + side
        tri = [(-0.026, 0.0), (0.026, 0.0), (s * 0.006, 0.05)]
        m(f"{g}-Ear", "orange").prism(tri, (0, 0, 0), (1, 0, 0), (0, 1, 0), 0.012)
        m(f"{g}-Inner", "pink").prism([(-0.016, 0.006), (0.016, 0.006), (s * 0.004, 0.036)], (0, 0, 0.0065), (1, 0, 0), (0, 1, 0), 0.002)
        ear = Vector((s * 0.045, 0.048, -0.008))
        m.apply(g + "-", rot((0, s * -10, s * -18)), ear)
        m.mark(g, ear)
        g = "Eye" + side
        m(f"{g}-Iris", "green").ellipsoid((0, 0, 0), (0.014, 0.012, 0.006), 12, 8)
        m(f"{g}-Pupil", "dark").ellipsoid((0, 0, 0.004), (0.003, 0.011, 0.004), 8, 6)
        eye = Vector((s * 0.032, 0.014, 0.058))
        m.apply(g + "-", rot((0, s * 24, 0)), eye)
        m.mark(g, eye)
    m.apply("Head-", None, (0, 0.14, 0.17))
    m.apply("Ear", None, (0, 0.14, 0.17), marks=("EarL", "EarR"))
    m.apply("Eye", None, (0, 0.14, 0.17), marks=("EyeL", "EyeR"))
    m.mark("Head", (0, 0.14, 0.17))
    m.mark("Body", (0, 0, 0))
    # the whole cat onto the cushion, turned a little (Cat3D: at (0.08, 0.06, -0.08), turned -20°)
    C, at = rot((0, -20, 0)), (0.08, 0.06, -0.08)
    for g in ("Body-", "Paw-", "Spikes-", "Tail-", "Head-", "Ear", "Eye"):
        m.apply(g, C, at)
    for k in ("Paw", "Spikes", "Tail", "Head", "Body", "EarL", "EarR", "EyeL", "EyeR"):
        m.marks[k] = C @ m.marks[k] + Vector(at)


# ================================================================ the fish

FISH = ["perch", "crucian", "pike", "catfish", "ruffe"]


def fish(m, kind):
    """A fish, head along +x, its middle at the origin; each unlike the others at a glance."""
    spec = {
        "perch": (0.2, 0.09, 0.5, "green"), "crucian": (0.17, 0.13, 0.42, "yellow"), "pike": (0.34, 0.06, 0.6, "green"),
        "catfish": (0.3, 0.09, 0.85, "dark"), "ruffe": (0.13, 0.07, 0.55, "brown"),
    }[kind]
    L, T, th, col = spec
    if kind == "catfish":
        prof = [(0.0, 0.18, 0.3), (0.1, 0.32, 0.4), (0.3, 0.62, 0.7), (0.55, 0.85, 1.0), (0.75, 0.95, 1.15), (0.88, 0.85, 1.2), (0.96, 0.55, 0.9), (1.0, 0.0, 0.0)]
    elif kind == "pike":
        prof = [(0.0, 0.2, 0.3), (0.12, 0.42, 0.5), (0.3, 0.85, 0.85), (0.55, 1.0, 1.0), (0.75, 0.9, 0.9), (0.86, 0.6, 0.7), (0.95, 0.3, 0.45), (1.0, 0.0, 0.0)]
    else:
        prof = [(0.0, 0.15, 0.3), (0.1, 0.28, 0.42), (0.25, 0.6, 0.75), (0.45, 0.92, 1.0), (0.6, 1.0, 1.0), (0.76, 0.88, 0.9), (0.9, 0.58, 0.62), (0.97, 0.28, 0.32), (1.0, 0.0, 0.0)]
    R = rot((0, 0, -90))   # the lathe's axis to +x
    ring = [(-L / 2 + s * L, T / 2 * h, T / 2 * th * t) for s, h, t in prof]
    m("Body", col).lathe(ring, segs=20, m=R, smooth=2)
    full = curve(ring, 2)

    def half(x):   # half-height and half-thickness at x along it
        return interp(full, x, 1), interp(full, x, 2)

    def side(x, y):   # the surface's z at x, y
        h, t = half(x)
        return t * math.sqrt(max(0.0, 1 - (y / max(h, 1e-4)) ** 2))
    plane = ((1, 0, 0), (0, 1, 0))
    fin = "orange" if kind == "crucian" else "red" if kind == "perch" else col
    # the tail: two lobes in a V (the catfish a round paddle)
    x0 = -L / 2 + 0.01
    if kind == "catfish":
        m("Fins", col).prism([(0, -0.012), (-0.06, -0.035), (-0.075, 0.0), (-0.06, 0.03), (0, 0.012)], (x0, 0, 0), *plane, 0.005)
    else:
        a = T * 0.55
        m("Fins", fin if kind != "ruffe" else col).prism([(0.005, 0.0), (-a * 0.9, a * 0.85), (-a * 0.62, 0.0), (-a * 0.9, -a * 0.85)], (x0, 0, 0), *plane, 0.005)
    # the back fin and the belly fin
    top = lambda x: half(x)[0]
    if kind in ("perch", "ruffe"):
        n = 7 if kind == "perch" else 9
        xa, xb = (-0.06 * L / 0.2, 0.08 * L / 0.2) if kind == "perch" else (-0.32 * L, 0.3 * L)
        hgt = T * (0.3 if kind == "perch" else 0.42)
        pts = [(xb, top(xb) - 0.006)]
        for k in range(n + 1):
            x = xb - (xb - xa) * k / n
            pts.append((x, top(x) + hgt * (1 if k % 2 == 0 else 0.72) * (1 - 0.35 * k / n)))
        pts.append((xa, top(xa) - 0.006))
        m("Spines", "dark").prism(pts, (0, 0, 0), *plane, 0.004)
        for k in range(0, n + 1, 2):   # the spines through it
            x = xb - (xb - xa) * k / n
            m("Spines", "dark").tube([(x, top(x) - 0.004, 0), (x - 0.006, top(x) + hgt * (1.08 - 0.35 * k / n), 0)], 0.0018, 4)
    elif kind == "pike":
        xa, xb = -0.38 * L, -0.24 * L
        m("Fins", col).prism([(xb, top(xb) - 0.004), (xa + 0.01, top(xa) + T * 0.5), (xa - 0.01, top(xa) - 0.004)], (0, 0, 0), *plane, 0.004)
        m("Fins", col).prism([(xb, -top(xb) + 0.004), (xa + 0.01, -top(xa) - T * 0.45), (xa - 0.01, -top(xa) + 0.004)], (0, 0, 0), *plane, 0.004)
    else:
        xa, xb = -0.25 * L, 0.15 * L
        m("Fins", fin if kind == "crucian" else col).prism([(xb, top(xb) - 0.005), (0.05 * L, top(0.05 * L) + T * 0.3), (xa, top(xa) + T * 0.18), (xa - 0.02, top(xa - 0.02) - 0.004)], (0, 0, 0), *plane, 0.004)
    if kind != "pike":
        xa, xb = -0.3 * L, -0.08 * L
        m("Fins", fin).prism([(xb, -top(xb) + 0.004), (-0.2 * L, -top(-0.2 * L) - T * 0.25), (xa, -top(xa) + 0.004)], (0, 0, 0), *plane, 0.004)
    # the side fins behind the gills
    for s in (-1, 1):
        x = 0.2 * L
        m("Fins", fin).prism([(0.0, 0.0), (-0.05 * L / 0.2, -0.018), (-0.04 * L / 0.2, 0.006)], (x, -T * 0.12, s * side(x, -T * 0.12)), (1, 0, 0), (0, math.cos(0.5), s * math.sin(0.5)), 0.003)
    # the eyes, set into the head's sides
    ex, ey = 0.3 * L, T * 0.12
    er = 0.013 if kind == "ruffe" else 0.008 if kind == "catfish" else 0.011
    if kind == "pike":
        ex, ey = 0.26 * L, T * 0.18
    for s in (-1, 1):
        z = side(ex, ey)
        m("Eyes", "white").ellipsoid((ex, ey, s * (z - er * 0.15)), (er, er, er * 0.55), 10, 6)
        m("Pupils", "dark").ellipsoid((ex + er * 0.12, ey, s * (z + er * 0.32)), (er * 0.5, er * 0.5, er * 0.3), 8, 5)
    # the mouth
    mx = L / 2 - 0.004
    m("Mouth", "dark").tube([(mx - 0.012, -T * 0.06, -0.008), (mx, -T * 0.04, 0), (mx - 0.012, -T * 0.06, 0.008)], 0.0018, 4)
    if kind == "perch":   # dark bands round the back and sides
        for k in range(5):
            x = -0.22 * L + k * 0.12 * L
            m("Bands", "dark").sheet(lambda y: (half(y)[0], half(y)[1], 0.0, 0.0), [x, x + 0.006, x + 0.012], math.pi - 0.6, 2 * math.pi + 0.6, 0.0012, -0.003, segs=12)
        m.parts["Bands.dark"].apply(R)
    elif kind == "pike":   # yellow spots on the sides, a long flat snout
        m("Body", col).ellipsoid((L / 2 - 0.01, -T * 0.06, 0), (0.045, T * 0.22, T * th * 0.42), 14, 8)
        for k in range(7):
            x = -0.32 * L + k * 0.1 * L
            y = ((k % 2) - 0.5) * T * 0.3
            for s in (-1, 1):
                m("Spots", "yellow").ellipsoid((x, y, s * (side(x, y) - 0.001)), (0.009, 0.006, 0.003), 8, 5)
    elif kind == "catfish":   # whiskers and a pale belly
        m("Belly", "grey").ellipsoid((0.05 * L, -T * 0.32, 0), (L * 0.33, T * 0.2, T * th * 0.45), 16, 8)
        for s in (-1, 1):
            p = Vector((mx - 0.01, -T * 0.08, s * 0.012))
            m("Whiskers", "dark").tube([p, p + Vector((0.03, -0.02, s * 0.03)), p + Vector((0.03, -0.06, s * 0.07)), p + Vector((0.0, -0.09, s * 0.09))], [0.0025, 0.002, 0.0016, 0.0012], 5)
            p = Vector((mx - 0.02, -T * 0.25, s * 0.01))
            m("Whiskers", "dark").tube([p, p + Vector((0.01, -0.03, s * 0.012)), p + Vector((-0.005, -0.05, s * 0.02))], [0.0018, 0.0014, 0.001], 5)
    elif kind == "ruffe":   # dark spots
        for k, (x, y) in enumerate([(-0.15, 0.1), (0.0, 0.18), (0.12, 0.05), (-0.25, -0.02), (0.05, -0.12)]):
            for s in (-1, 1):
                m("Spots", "dark").ellipsoid((x * L, y * T, s * (side(x * L, y * T) - 0.001)), (0.006, 0.005, 0.0025), 8, 5)
    m.mark("Head", (L / 2, 0, 0))


# ================================================================ the pond

WATER = 0.42


def pond(m):
    W, D, H, th = 1.5, 0.72, 0.5, 0.05
    # the tub: a wooden trough of planks with corner posts and two iron hoops, a dark bottom, the water's face
    m("Tub", "brown").box((0, 0.03, 0), (W, 0.06, D), bevel=0.008)
    ph = (H - 0.06) / 3
    for k in range(3):
        y = 0.06 + ph * (k + 0.5)
        for s in (-1, 1):
            m("Tub", "brown").box((0, y, s * (D / 2 - th / 2)), (W - 0.06, ph - 0.004, th), bevel=0.007)
            m("Tub", "brown").box((s * (W / 2 - th / 2), y, 0), (th, ph - 0.004, D - 0.06), bevel=0.007)
    for sx in (-1, 1):
        for sz in (-1, 1):
            m("Tub", "brown").box((sx * (W / 2 - 0.035), H / 2 + 0.01, sz * (D / 2 - 0.035)), (0.075, H + 0.02, 0.075), bevel=0.01)
    for y in (0.13, 0.37):
        for s in (-1, 1):
            m("Hoops", "metal").box((0, y, s * (D / 2 + 0.004)), (W + 0.012, 0.028, 0.008), bevel=0.002)
            m("Hoops", "metal").box((s * (W / 2 + 0.004), y, 0), (0.008, 0.028, D + 0.012), bevel=0.002)
    m("Bottom", "dark").box((0, 0.066, 0), (W - 2 * th, 0.012, D - 2 * th))
    m("Water", "water").box((0, WATER - 0.002, 0.0), (W - 2 * th, 0.004, D - 2 * th))
    # reeds in the back corners, standing out of the water, cattails on some; long leaves
    rnd = __import__("random").Random(9)
    for cx in (-0.6, 0.58):
        for i in range(5):
            x, z = cx + rnd.uniform(-0.07, 0.07), -0.24 + rnd.uniform(0, 0.08)
            hh = 0.35 + rnd.uniform(0, 0.32)
            lean = Vector((rnd.uniform(-0.06, 0.06), 0, rnd.uniform(-0.04, 0.03)))
            base = Vector((x, 0.07, z))
            tip = base + Vector((0, WATER - 0.07 + hh, 0)) + lean
            mid = base.lerp(tip, 0.6) + lean * -0.3
            m("Reeds", "green").tube([base, mid, tip], [0.007, 0.006, 0.003], 6)
            if i % 2 == 0:
                c = base.lerp(tip, 0.86)
                m("Cattails", "brown").ellipsoid(c, (0.014, 0.055, 0.014), 10, 8, m=Vector((0, 1, 0)).rotation_difference(tip - mid).to_matrix())
            else:   # a leaf: a long thin blade bending over
                a = rnd.uniform(0, 2 * math.pi)
                d = Vector((math.cos(a), 0, math.sin(a)))
                pts = [base + Vector((0, 0.1, 0)), base + Vector((0, 0.45, 0)) + d * 0.04, base + Vector((0, 0.65, 0)) + d * 0.14, base + Vector((0, 0.68, 0)) + d * 0.22]
                m("Reeds", "green").tube(pts, [0.008, 0.007, 0.005, 0.002], 5)
    # lily pads with a notch, a flower on one
    for x, z, r, ang in ((0.3, 0.12, 0.07, 20), (-0.4, -0.1, 0.055, 130), (0.45, -0.15, 0.045, 250)):
        pts = []
        for k in range(25):
            a = math.radians(ang + 22 + k * (360 - 44) / 24)
            pts.append((math.cos(a) * r, math.sin(a) * r))
        pts.append((0.0, 0.0))
        m("Pads", "green").prism(pts, (x, WATER + 0.003, z), (1, 0, 0), (0, 0, 1), 0.004)
    for k in range(6):
        a = 2 * math.pi * k / 6
        m("Flower", "pink").ellipsoid((0.3 + math.cos(a) * 0.016, WATER + 0.014, 0.12 + math.sin(a) * 0.016), (0.014, 0.008, 0.008), 8, 5,
                                      m=rot((0, -math.degrees(a), 25)))
    m("Flower", "yellow").ellipsoid((0.3, WATER + 0.018, 0.12), 0.009, 8, 6)
    # fish under the water: dark shapes going round (Rod3D moves them), +z ahead
    for i in range(3):
        g = f"Shadow{i}"
        s = 1 - i * 0.12
        m(f"{g}-Fish", "dark").ellipsoid((0, 0, 0), (0.026 * s, 0.012 * s, 0.068 * s), 12, 6)
        m(f"{g}-Fish", "dark").prism([(0.0, -0.06 * s), (-0.028 * s, -0.1 * s), (0.028 * s, -0.1 * s)], (0, 0, 0), (1, 0, 0), (0, 0, 1), 0.006)
        m.apply(g + "-", None, (0, WATER - 0.12 - i * 0.05, 0))
        m.mark(g, (0, WATER - 0.12 - i * 0.05, 0))
    # the post at the tub's front right, a foot on the floor and a fork at its top
    P = Vector((0.62, 0, 0.42))
    m("Post", "metal").lathe([(0, 0.0), (0, 0.075), (0.012, 0.075), (0.02, 0.03), (0.02, 0.022), (0.9, 0.02), (0.92, 0.0)], segs=20, at=P)
    for s in (-1, 1):
        m("Post", "metal").box(P + Vector((s * 0.026, 0.93, 0)), (0.008, 0.06, 0.03), bevel=0.003)
    # the rod on its pivot over the post: a cork grip, the reel, guides, a thin tip with the bell
    rp = Vector((0.62, 0.93, 0.42))
    m("Rod-Grip", "brown").lathe([(-0.2, 0.0), (-0.2, 0.014), (-0.18, 0.017), (0.02, 0.015), (0.04, 0.0)], segs=14)
    m("Rod-Blank", "brown").tube([(0, 0.0, 0), (0, 0.6, 0), (0, 1.29, 0)], [0.009, 0.006, 0.0028], 8)
    m("Rod-Reel", "metal").lathe([(-0.016, 0.0), (-0.016, 0.03), (0.016, 0.03), (0.016, 0.0)], segs=18, at=(0.034, -0.08, 0), m=rot((0, 0, 90)))
    m("Rod-Reel", "metal").tube([(0.0, -0.08, 0), (0.034, -0.08, 0)], 0.006, 6)
    m("Rod-Reel", "dark").tube([(0.052, -0.08, 0), (0.06, -0.08, 0.022)], 0.004, 6)
    m("Rod-Reel", "dark").ellipsoid((0.063, -0.08, 0.026), (0.007, 0.007, 0.007), 8, 5)
    for y, r in ((0.35, 0.013), (0.75, 0.01), (1.08, 0.008)):
        m("Rod-Guides", "metal").loop([(math.cos(2 * math.pi * k / 10) * r * 0.6, y + math.sin(2 * math.pi * k / 10) * r, r + 0.006) for k in range(10)], 0.0018, 4)
    m("Rod-Bell", "yellow").lathe([(-0.025, 0.0), (-0.025, 0.017), (-0.018, 0.016), (-0.006, 0.01), (0.0, 0.004), (0.004, 0.0)], segs=14, at=(0, 1.27, 0.016))
    m("Rod-Bell", "yellow").tube([(0, 1.275, 0.0), (0, 1.27, 0.016)], 0.0015, 4)
    m.apply("Rod-", None, rp)
    m.mark("Rod", rp)
    # the hook and the float (Rod3D moves them on the line)
    m("Hook-Hook", "metal").tube([(0, 0.0, 0), (0, -0.01, 0), (0.002, -0.016, 0), (0.007, -0.017, 0), (0.009, -0.012, 0), (0.007, -0.008, 0)], 0.0014, 5)
    m("Hook-Hook", "metal").loop([(math.sin(2 * math.pi * k / 8) * 0.0025, 0.0025 + math.cos(2 * math.pi * k / 8) * 0.0025, 0) for k in range(8)], 0.001, 4)
    m.mark("Hook", (0, 0, 0))
    m("Float-Top", "red").lathe([(0.0, 0.015), (0.008, 0.013), (0.018, 0.007), (0.024, 0.002), (0.026, 0.0)], segs=14)
    m("Float-Top", "red").tube([(0, 0.02, 0), (0, 0.04, 0)], 0.002, 5)
    m("Float-Bottom", "white").lathe([(-0.018, 0.0), (-0.016, 0.006), (-0.008, 0.012), (0.0, 0.0145), (0.0005, 0.0)], segs=14)
    m.mark("Float", (0, 0, 0))
    # the hourglass on the tub's front edge, by the post: its sand runs while the bait is in the water
    G = Vector((0.42, H, 0.335))
    for y in (0.005, 0.125):
        m("Hourglass", "brown").lathe([(-0.005, 0.0), (-0.005, 0.034), (0.005, 0.034), (0.005, 0.0)], segs=18, at=G + Vector((0, y, 0)))
    for k in range(3):
        a = 2 * math.pi * k / 3 + 0.5
        m("Hourglass", "brown").tube([G + Vector((math.cos(a) * 0.028, 0.008, math.sin(a) * 0.028)), G + Vector((math.cos(a) * 0.028, 0.122, math.sin(a) * 0.028))], 0.0035, 6)
    m("Glass", "clear").lathe([(0.01, 0.0), (0.01, 0.017), (0.025, 0.024), (0.045, 0.02), (0.058, 0.007), (0.065, 0.0035), (0.072, 0.007),
                                (0.085, 0.02), (0.105, 0.024), (0.12, 0.017), (0.12, 0.0)], segs=18, at=G, smooth=1)
    m("SandTop-Sand", "yellow").lathe([(0.0, 0.0), (0.0, 0.003), (0.008, 0.011), (0.022, 0.017), (0.03, 0.012), (0.034, 0.0)], segs=14, at=G + Vector((0, 0.067, 0)))
    m.mark("SandTop", G + Vector((0, 0.067, 0)))
    m("SandBottom-Sand", "yellow").lathe([(0.0, 0.0), (0.0, 0.017), (0.008, 0.015), (0.018, 0.006), (0.022, 0.0)], segs=14, at=G + Vector((0, 0.012, 0)))
    m.mark("SandBottom", G + Vector((0, 0.012, 0)))
    # the board behind the tub: a slate in a frame on two posts (Rod3D writes on it and puts the fish on it)
    z, y0, bw, bh = -0.37, 0.95, 1.46, 0.7
    for s in (-1, 1):
        m("Posts", "brown").box((s * 0.69, (y0 + bh) / 2 + 0.01, z - 0.02), (0.045, y0 + bh + 0.02, 0.03), bevel=0.006)
    m("Slate", "dark").box((0, y0 + bh / 2, z - 0.009), (bw, bh, 0.01), bevel=0.002)
    for x, y, w, h in ((0, y0, bw + 0.04, 0.025), (0, y0 + bh, bw + 0.04, 0.025), (-bw / 2, y0 + bh / 2, 0.025, bh + 0.04), (bw / 2, y0 + bh / 2, 0.025, bh + 0.04)):
        m("Frame", "brown").box((x, y, z - 0.008), (w, h, 0.022), bevel=0.004)


# ================================================================ the xylophone

def xylo(m):
    # the felt pad the sample lies on, and the mallet on its post (Xylo3D swings it about the post's top)
    m("Pad", "red").box((-0.45, 0.012, 0.05), (0.22, 0.024, 0.22), bevel=0.008)
    m.mark("Seat", (-0.45, 0.024, 0.05))
    P = Vector((-0.62, 0, -0.08))
    m("Post", "metal").lathe([(0, 0.0), (0, 0.04), (0.01, 0.04), (0.014, 0.008), (0.02, 0.008), (0.29, 0.008), (0.3, 0.0)], segs=16, at=P)
    for s in (-1, 1):
        m("Post", "metal").box(P + Vector((0, 0.3, s * 0.012)), (0.02, 0.03, 0.005), bevel=0.0015)
    stick = [Vector((0, 0, 0)), Vector((0.18, 0, 0.125))]
    d = (stick[1] - stick[0]).normalized()
    m("Mallet-Stick", "brown").tube([stick[0] - d * 0.012, stick[1] - d * 0.015], [0.006, 0.005], 8)
    m("Mallet-Head", "white").ellipsoid(stick[1], 0.021, 14, 10)
    m("Mallet-Stick", "metal").tube([(0, 0, -0.014), (0, 0, 0.014)], 0.005, 8)
    m.apply("Mallet-", None, P + Vector((0, 0.3, 0)))
    m.mark("Mallet", P + Vector((0, 0.3, 0)))
    # the xylophone: two rails on a felt strip, seven bars on pins, longest (lowest) on the left of whoever faces it
    for z in (-0.07, 0.07):
        m("Rails", "brown").box((0.2, 0.02, z), (0.62, 0.04, 0.03), bevel=0.005)
        m("Felt", "red").box((0.2, 0.0415, z), (0.6, 0.004, 0.018))
    colours = ["red", "orange", "yellow", "green", "blue", "purple", "pink"]
    for i in range(7):
        x, ln = 0.42 - i * 0.075, 0.26 - i * 0.017
        g = f"Bar{i}"
        m(f"{g}-Bar", colours[i]).box((x, 0.05, 0), (0.06, 0.016, ln), bevel=0.004)
        for z in (-0.07, 0.07):
            m(f"{g}-Pins", "metal").lathe([(0.0, 0.0), (0.0, 0.003), (0.012, 0.003), (0.012, 0.0)], segs=8, at=(x, 0.054, z))
        m.mark(g, (x, 0.05, 0))


# ================================================================ the feather

def feather(m):
    # the velvet cushion with tassels at its four sides
    m("Cushion", "purple").lathe([(0.0, 0.0), (0.0, 0.118), (0.012, 0.13), (0.03, 0.128), (0.04, 0.11), (0.044, 0.0)], segs=28, n=2.8, at=(0, 0, 0.06), smooth=1)
    for k in range(4):
        a = k * math.pi / 2
        c = Vector((math.cos(a) * 0.132, 0.022, 0.06 + math.sin(a) * 0.132))
        m("Tassels", "yellow").ellipsoid(c, 0.008, 8, 6)
        m("Tassels", "yellow").lathe([(-0.03, 0.0), (-0.028, 0.008), (-0.006, 0.004), (0.0, 0.0)], segs=8, at=c + Vector((math.cos(a) * 0.006, 0, math.sin(a) * 0.006)))
    m.mark("Seat", (0, 0.044, 0.06))
    # the feather in its own space: hanging along -y from its top, the vane flat in x-y, curving gently; then laid on
    # the table where it rests (Feather3D: at RestPos, turned RestRot)
    Lq = 0.32

    def at(t):
        return Vector((math.sin(t * 1.4) * 0.035, -t * Lq, 0))
    pts = [at(i / 16) for i in range(17)]
    m("Feather-Shaft", "white").tube(pts, [lerp(0.0032, 0.0012, i / 16) for i in range(17)], 6)
    for i in range(5):   # fluff at the bottom of the vane's start
        t = 0.1 + i * 0.022
        for s in (-1, 1):
            m("Feather-Fluff", "white").ellipsoid(at(t) + Vector((s * 0.009, 0, 0)), (0.011, 0.005, 0.0025), 8, 5, m=rot((0, 0, s * (35 + i * 6))))
    # the vane, on each side, in a few strips with splits between them, its edge combed into barbs
    for s in (-1, 1):
        cuts = [0.2, 0.42, 0.47, 0.7, 0.74, 0.96] if s < 0 else [0.2, 0.55, 0.6, 0.96]
        for a0, a1 in zip(cuts[0::2], cuts[1::2]):
            n = max(4, int((a1 - a0) * 40))
            outer, inner = [], []
            for k in range(n + 1):
                t = a0 + (a1 - a0) * k / n
                u = (t - 0.2) / 0.76
                w = 0.05 * math.sin(math.pi * max(u, 0) ** 0.6) + 0.006
                p = at(t)
                tg = (at(t + 0.01) - p).normalized()
                out = Vector((-tg.y, tg.x, 0)) * s
                barb = (out + tg * 0.75).normalized()   # the barbs slope down towards the point
                outer.append(p + barb * w * (1.0 if k % 2 == 0 else 0.86))
                inner.append(p + out * 0.002)
            poly = [(q.x, q.y) for q in inner] + [(q.x, q.y) for q in reversed(outer)]
            m("Feather-Vane", "white").prism(poly, (0, 0, 0), (1, 0, 0), (0, 1, 0), 0.0016)
    rest = rot((0, -25, 0)) @ rot((90, 0, 0)) @ rot((0, 0, 90))
    m.apply("Feather-", rest, (0.2, 0.006, -0.14))
    m.mark("Feather", (0.2, 0.006, -0.14))


# ================================================================ the mirror

def mirror(m):
    W, H, B, back = 0.5, 0.72, 0.08, -0.2
    cy = B + H / 2
    # a back board behind the glass, a carved frame round it with a gilt bead at the glass, a crest on top
    m("Back", "brown").box((0, cy, back - 0.012), (W + 0.06, H + 0.06, 0.016), bevel=0.003)
    for x, y, w, h in ((0, B - 0.025, W + 0.1, 0.05), (0, B + H + 0.025, W + 0.1, 0.05), (-W / 2 - 0.025, cy, 0.05, H + 0.1), (W / 2 + 0.025, cy, 0.05, H + 0.1)):
        m("Frame", "brown").box((x, y, back + 0.0), (w, h, 0.028), bevel=0.009, segments=3)
    bead = [(-W / 2 - 0.003, B - 0.003), (W / 2 + 0.003, B - 0.003), (W / 2 + 0.003, B + H + 0.003), (-W / 2 - 0.003, B + H + 0.003)]
    loop = []
    for (x0, y0), (x1, y1) in zip(bead, bead[1:] + bead[:1]):
        for k in range(6):
            loop.append((lerp(x0, x1, k / 6), lerp(y0, y1, k / 6), back + 0.006))
    m("Bead", "yellow").loop(loop, 0.005, 6)
    crest = [(-0.17, 0.0), (-0.12, 0.035), (-0.06, 0.05), (-0.025, 0.075), (0.0, 0.09), (0.025, 0.075), (0.06, 0.05), (0.12, 0.035), (0.17, 0.0)]
    m("Frame", "brown").prism(crest, (0, B + H + 0.045, back - 0.002), (1, 0, 0), (0, 1, 0), 0.022)
    m("Bead", "yellow").ellipsoid((0, B + H + 0.095, back + 0.008), (0.018, 0.018, 0.01), 10, 6)
    # two sled feet holding the frame up
    for s in (-1, 1):
        m("Feet", "brown").prism([(-0.1, 0.0), (0.1, 0.0), (0.085, 0.025), (0.018, 0.04), (0.012, B + 0.02), (-0.012, B + 0.02), (-0.018, 0.04), (-0.085, 0.025)],
                                 (s * (W / 2 + 0.025), 0, back - 0.004), (0, 0, 1), (0, 1, 0), 0.03)
    # the pedestal in front: a turned column on a foot, a round top
    m("Pedestal", "white").lathe([(0.0, 0.0), (0.0, 0.07), (0.012, 0.072), (0.022, 0.05), (0.03, 0.045), (0.095, 0.045), (0.103, 0.06),
                                  (0.112, 0.08), (0.122, 0.08), (0.128, 0.0)], segs=28, at=(0, 0, 0.12))
    m.mark("Seat", (0, 0.128, 0.12))


BUILD = {"tongue": tongue, "cat": cat, "pond": pond, "xylo": xylo, "feather": feather, "mirror": mirror}

clear()
shown = []
names = list(BUILD) + ["fish_" + f for f in FISH]
for name in names:
    if ONLY and name not in ONLY and not (name.startswith("fish_") and "fish" in ONLY):
        continue
    model = Model("Odd_" + name)
    if name.startswith("fish_"):
        fish(model, name[5:])
    else:
        BUILD[name](model)
    objs = model.export(OUT)
    if PREVIEW:
        shown.append((stash(objs, name, (len(shown) * 4.0, 0, 0)), name))
    else:
        clear()
if PREVIEW:
    for root, name in shown:
        for r, _ in shown:
            for o in [r] + list(r.children):
                o.hide_render = r is not root
        for yaw, pitch, tag in ((25, 30, "a"), (-35, 12, "b")):
            preview_fit(root, os.path.join(PREVIEW, f"odd_{name}_{tag}.png"), yaw, pitch)
print("done ->", OUT)
