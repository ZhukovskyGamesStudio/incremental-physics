# The desk's and the room's small things (DESIGN.md 2б): the stamp and its ink pad, the dispatch box with its two
# flaps and its tape, the calculator, a certificate's frame, the signs hung on the tables, the bare bulb, the door
# knob, the overflow can with its measuring cylinder, the inclined plane, the light switch, a cardboard box and a
# cobweb. One FBX each into <Models>/Desk/Desk_<name>.fbx; Lab3DBuilder puts them in the scene and paints them by the
# colour in each part's name (some by the part's own name: the paper, the keys, the glass — see Lab3DBuilder.Model).
# Conventions at the top of kit.py: Unity axes, the origin where the builder puts the thing, "<Group>-<Part>.<colour>"
# with a marker <Group> for what the game moves (Art3D.Pivot).
#   blender -b --factory-startup -P Tools/Blender/desk.py -- <Models dir> [--preview <dir>] [--only stamp,box]
import os, sys, math
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kit import *

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = os.path.join(argv[0] if argv and not argv[0].startswith("--") else os.path.join(os.getcwd(), "Assets", "Lab3D", "Models"), "Desk")
PREVIEW = argv[argv.index("--preview") + 1] if "--preview" in argv else None
ONLY = argv[argv.index("--only") + 1].split(",") if "--only" in argv else None


def stamp(m):
    """The rubber stamp, standing on its rubber at the origin: a wooden block, a brass ferrule, a turned handle."""
    m("Rubber", "red").box((0, 0.002, 0), (0.068, 0.004, 0.044), bevel=0.001)
    m("Block", "brown").box((0, 0.014, 0), (0.075, 0.02, 0.05), bevel=0.004)
    m("Ferrule", "metal").lathe([(0.023, 0.0), (0.023, 0.017), (0.027, 0.018), (0.033, 0.016), (0.034, 0.0)], segs=20)
    m("Handle", "brown").lathe([(0.03, 0.0), (0.03, 0.0135), (0.045, 0.0115), (0.065, 0.0095), (0.08, 0.011), (0.088, 0.0)], segs=20, smooth=1)
    m("Handle", "brown").ellipsoid((0, 0.104, 0), (0.023, 0.021, 0.023), 18, 12)


def inkpad(m):
    """The ink pad: a tin with a red pad in it, its lid lying open behind it; the origin under its middle."""
    m("Tin", "metal").box((0, 0.005, 0), (0.13, 0.01, 0.095), bevel=0.0025)
    m("Pad", "red").box((0, 0.0105, 0), (0.116, 0.003, 0.082), bevel=0.001)
    m("Tin", "metal").box((0, 0.003, 0.1), (0.13, 0.006, 0.095), bevel=0.002)


def box(m):
    """The dispatch box: a bottom, four walls of two boards each, corner battens, a label; two lid flaps hinged on the
    side walls (shut, as modelled; DispatchDesk opens them) and the tape over the shut lid."""
    W, D, H, t = 0.27, 0.34, 0.2, 0.01
    m("Box", "brown").box((0, 0.005, 0), (W, 0.01, D), bevel=0.002)
    for k in range(2):
        y = 0.005 + H / 4 + k * H / 2
        for s in (-1, 1):
            m("Box", "brown").box((0, y, s * (D / 2 - t / 2)), (W, H / 2 - 0.002, t), bevel=0.0018)
            m("Box", "brown").box((s * (W / 2 - t / 2 - 0.005), y, 0), (t, H / 2 - 0.002, D - 2 * t), bevel=0.0018)
    for sx in (-1, 1):
        for sz in (-1, 1):
            m("Battens", "brown").box((sx * (W / 2 - 0.004), 0.005 + H / 2, sz * (D / 2 + 0.002)), (0.018, H, 0.008), bevel=0.002)
    m("Label", "white").box((0, 0.07, -D / 2 - 0.002), (0.09, 0.045, 0.003), bevel=0.001)
    top = 0.005 + H
    for g, s in (("FlapL", -1), ("FlapR", 1)):
        m(f"{g}-Board", "brown").box((s * 0.068, top + 0.004, 0), (0.134, 0.008, D), bevel=0.002)
        m.mark(g, (s * 0.135, top, 0))
    m("Tape-Strip", "yellow").box((0, top + 0.0095, 0), (0.06, 0.003, D + 0.012), bevel=0.0008)
    m("Tape-Strip", "yellow").box((0, top - 0.035, -D / 2 - 0.0075), (0.06, 0.087, 0.003), bevel=0.0008)
    m.mark("Tape", (0, 0, 0))


KEYS = ["7", "8", "9", "÷", "4", "5", "6", "×", "1", "2", "3", "−", "C", "0", ",", "=", "tg"]


def calculator(m):
    """The pocket calculator, lying on its prop: a body, the display in its bezel, seventeen keys (each its own piece
    with a marker: Calculator3D presses them)."""
    m("Body", "grey").box((0, 0.01, 0), (0.17, 0.02, 0.25), bevel=0.008, segments=3)
    m("Body", "grey").prism([(0.05, 0.002), (0.11, 0.002), (0.11, -0.027)], (0, 0, 0), (0, 0, 1), (0, 1, 0), 0.14)
    m("Bezel", "dark").box((0, 0.0205, 0.085), (0.156, 0.003, 0.058), bevel=0.0015)
    m("Display-Screen", "dark").box((0, 0.0215, 0.085), (0.14, 0.002, 0.044), bevel=0.0008)
    m.mark("Display", (0, 0.0215, 0.085))
    for i in range(len(KEYS)):
        c, r = i % 4, i // 4
        p = (-0.054 + 0.036 * c, 0.024, 0.035 - 0.036 * r)
        m(f"Key{i:02d}-Cap", "white").box(p, (0.031, 0.008, 0.029), bevel=0.0025)
        m.mark(f"Key{i:02d}", p)


def certificate(m):
    """A certificate: a sheet of paper in a gilt frame, hung by a cord from a nail; its face looks along +z."""
    m("Paper", "white").box((0, 0, 0), (0.3, 0.4, 0.006))
    fw, fd = 0.022, 0.02
    for x, y, w, h in ((0, 0.2 + fw / 2, 0.3 + 2 * fw, fw), (0, -0.2 - fw / 2, 0.3 + 2 * fw, fw), (-0.15 - fw / 2, 0, fw, 0.4), (0.15 + fw / 2, 0, fw, 0.4)):
        m("Frame", "yellow").box((x, y, 0.004), (w, h, fd), bevel=0.006, segments=3)
    for sx in (-1, 1):
        for sy in (-1, 1):
            m("Frame", "yellow").ellipsoid((sx * (0.15 + fw / 2), sy * (0.2 + fw / 2), 0.015), (0.012, 0.012, 0.005), 10, 6)
        m("Cord", "white").tube([(sx * 0.12, 0.215, -0.004), (sx * 0.06, 0.255, -0.004), (0, 0.29, -0.004)], 0.0018, 5)
    m("Nail", "metal").lathe([(-0.02, 0.0), (-0.02, 0.0018), (0.0, 0.0018), (0.0, 0.006), (0.003, 0.006), (0.003, 0.0)], segs=12, at=(0, 0.29, 0.0), m=rot((90, 0, 0)))


def sign(m, w, h, up):
    """A board hung on a table's front on two cords from screw eyes under its top; the origin at the board's middle.
    The game writes on its -z face. `up`: from the board's top edge up to the eyes."""
    m("Board", "white").box((0, 0, 0), (w, h, 0.018), bevel=0.003)
    for s in (-1, 1):
        x = s * w * 0.42
        m("Cords-Cord", "white").tube([(x, h / 2 - 0.012, 0), (x, h / 2 + up - 0.006, 0)], 0.0028, 6)
        m("Cords-Eyes", "metal").loop([(x + math.sin(2 * math.pi * k / 10) * 0.006, h / 2 + up + math.cos(2 * math.pi * k / 10) * 0.006, 0) for k in range(10)], 0.0016, 4)
        m("Cords-Eyes", "metal").tube([(x, h / 2 + up + 0.006, 0), (x, h / 2 + up + 0.03, 0)], 0.0018, 5)


def sign_counter(m):
    sign(m, 0.62, 0.2, 0.07)


def sign_dept(m):
    sign(m, 0.5, 0.16, 0.04)


def lamp(m):
    """The bare bulb on its cord from a ceiling rose; the origin on the floor under it, the ceiling at 3 m."""
    m("Rose", "white").lathe([(2.968, 0.0), (2.968, 0.04), (2.975, 0.054), (3.0, 0.056), (3.0, 0.0)], segs=24, smooth=1)
    m("Rose", "white").lathe([(2.935, 0.0), (2.935, 0.018), (2.968, 0.023), (2.97, 0.0)], segs=16)
    m("Cord", "dark").tube([(0, 2.94, 0), (0.004, 2.8, 0.002), (-0.002, 2.65, -0.003), (0, 2.515, 0)], 0.004, 6)
    m("Socket", "metal").lathe([(2.468, 0.0), (2.468, 0.016), (2.48, 0.018), (2.49, 0.0165), (2.5, 0.018), (2.512, 0.017), (2.518, 0.008), (2.52, 0.0)], segs=18)
    m("Bulb", "yellow").lathe([(2.37, 0.0), (2.373, 0.016), (2.385, 0.03), (2.405, 0.037), (2.425, 0.034), (2.445, 0.022), (2.46, 0.014), (2.47, 0.0)], segs=22, smooth=2)


def doorknob(m):
    """A round door knob on a rose, standing out of the door along +z from the origin on its face."""
    R = rot((90, 0, 0))
    m("Knob", "metal").lathe([(0.0, 0.0), (0.0, 0.034), (0.004, 0.035), (0.007, 0.03), (0.008, 0.0)], segs=24, m=R)
    m("Knob", "metal").lathe([(0.006, 0.0), (0.006, 0.013), (0.03, 0.011), (0.04, 0.0)], segs=16, m=R)
    m("Knob", "metal").ellipsoid((0, 0, 0.062), (0.029, 0.029, 0.025), 22, 14)
    m.mark("Tip", (0, 0, 0.087))


def overflow(m):
    """The bath's glass and metal (Bath3D draws the water): an overflow can on a stand, open to the front like a drawing
    of one, its spout over a measuring cylinder on the table, the hose the cylinder drains back by."""
    x0, w, hgt, dp, wall, stand, cx = -0.1, 0.34, 0.3, 0.26, 0.008, 0.2, 0.215
    floor = stand + 0.012
    m("Stand", "metal").box((x0, stand - 0.01, 0), (w + 0.06, 0.02, dp + 0.06), bevel=0.004)
    for lx in (-1, 1):
        for lz in (-1, 1):
            p = Vector((x0 + lx * (w / 2 + 0.01), 0, lz * (dp / 2 + 0.01)))
            m("Stand", "metal").tube([p + Vector((0, 0.008, 0)), p + Vector((0, stand - 0.015, 0))], 0.0105, 10)
            m("Stand", "metal").ellipsoid(p + Vector((0, 0.006, 0)), (0.016, 0.006, 0.016), 12, 6)
    for a, b in (((-1, -1), (1, -1)), ((1, -1), (1, 1)), ((1, 1), (-1, 1)), ((-1, 1), (-1, -1))):
        pa = Vector((x0 + a[0] * (w / 2 + 0.01), 0.06, a[1] * (dp / 2 + 0.01)))
        pb = Vector((x0 + b[0] * (w / 2 + 0.01), 0.06, b[1] * (dp / 2 + 0.01)))
        m("Stand", "metal").tube([pa, pb], 0.0055, 8, caps=False)
    m("Can", "clear").box((x0, stand + 0.006, 0), (w + 2 * wall, 0.012, dp + wall), bevel=0.0015)
    m("Can", "clear").box((x0, floor + hgt / 2, -dp / 2 - wall / 2), (w + 2 * wall, hgt, wall), bevel=0.0015)
    for s in (-1, 1):
        m("Can", "clear").box((x0 + s * (w / 2 + wall / 2), floor + hgt / 2, 0), (wall, hgt, dp), bevel=0.0015)
        m("Can", "clear").box((x0 + s * (w / 2 + wall / 2), floor + hgt / 2, dp / 2 + 0.003), (0.007, hgt, 0.007), bevel=0.0015)
    for y in (floor, floor + hgt):
        m("Can", "clear").box((x0, y, dp / 2 + 0.003), (w + 2 * wall, 0.007, 0.007), bevel=0.0015)
    spout = 0.2
    a, b = Vector((x0 + w / 2 - 0.004, floor + spout - 0.004, 0)), Vector((cx - 0.012, floor + spout - 0.016, 0))
    m("Spout", "clear").tube([a, a.lerp(b, 0.5) + Vector((0, 0.002, 0)), b], [0.013, 0.012, 0.011], 14)
    m("Spout", "clear").loop([b + Vector((0.006, math.sin(2 * math.pi * k / 14) * 0.012, math.cos(2 * math.pi * k / 14) * 0.012)) for k in range(14)], 0.0025, 5)
    # the measuring cylinder: a foot, the glass (a hundred square centimetres inside, ten centimetres a litre), its rim
    r, ch = 0.0564, 0.37
    m("Foot", "clear").lathe([(0.0, 0.0), (0.0, 0.074), (0.006, 0.075), (0.012, 0.068), (0.013, 0.0)], segs=6, at=(cx, 0, 0))
    m("Tube", "clear").lathe([(0.012, 0.0), (0.012, r + 0.004), (0.012 + ch, r + 0.004), (0.012 + ch, r + 0.0015), (0.016, r + 0.0015), (0.016, 0.0)], segs=32, at=(cx, 0, 0))
    m("Rim", "clear").loop([(cx + math.sin(2 * math.pi * k / 32) * (r + 0.005), 0.012 + ch, math.cos(2 * math.pi * k / 32) * (r + 0.005)) for k in range(32)], 0.003, 6)
    m("Rim", "clear").prism([(-0.012, 0.0), (0.012, 0.0), (0.0, 0.018)], (cx - r - 0.004, 0.012 + ch - 0.002, 0), (0, 0, 1), (-1, 0.35, 0), 0.004)
    # the hose: out of the cylinder's foot, round and up into the can's bottom
    m("Hose", "grey").tube([(cx - 0.05, 0.012, 0.05), (cx - 0.065, 0.016, 0.085), (cx - 0.11, 0.03, 0.105), (0.06, 0.09, 0.1),
                            (x0 + w / 2 - 0.02, 0.16, 0.07), (x0 + w / 2 - 0.02, stand + 0.006, 0.05)], 0.0075, 10)
    m("Hose", "metal").lathe([(0.0, 0.0), (0.0, 0.011), (0.012, 0.011), (0.012, 0.0)], segs=12, at=(cx - 0.05, 0.0, 0.05))


def incline(m):
    """The inclined plane: a base, a hinge block with its barrel, the board (it turns about the hinge: the group
    Board, with its stop and the needle along its front edge), a clear protractor round the hinge on a bar."""
    hx, hy = 0.3, 0.06
    m("Base", "brown").box((0, 0.01, 0), (0.7, 0.02, 0.24), bevel=0.004)
    m("Hinge", "metal").box((hx, 0.035, 0), (0.05, 0.05, 0.22), bevel=0.004)
    m("Hinge", "metal").tube([(hx, hy, -0.11), (hx, hy, 0.11)], 0.011, 14)
    m("Board-Plank", "brown").box((-0.3, 0.01, 0), (0.6, 0.02, 0.2), bevel=0.003)
    m("Board-Stop", "metal").box((-0.015, 0.035, 0), (0.015, 0.05, 0.2), bevel=0.002)
    m("Board-Needle", "white").box((-0.1175, 0.021, 0.112), (0.235, 0.003, 0.003))
    m.apply("Board-", None, (hx, hy, 0))
    m.mark("Board", (hx, hy, 0))
    m("Stand", "metal").box((hx - 0.1, 0.03, 0.125), (0.26, 0.04, 0.01), bevel=0.002)
    pts = []
    for k in range(16):
        a = math.radians(75 * k / 15)
        pts.append((-math.cos(a) * 0.275, math.sin(a) * 0.275))
    for k in range(15, -1, -1):
        a = math.radians(75 * k / 15)
        pts.append((-math.cos(a) * 0.165, math.sin(a) * 0.165))
    m("Protractor", "clear").prism(pts, (hx, hy, 0.106), (1, 0, 0), (0, 1, 0), 0.004)


def switch(m):
    """The light switch: a plate, its rocker (the group Toggle: LightSwitch tips it), the green dot seen in the dark."""
    m("Plate", "white").box((0, 0, 0), (0.09, 0.13, 0.012), bevel=0.004)
    m("Toggle-Key", "metal").box((0, 0.01, 0.014), (0.035, 0.055, 0.016), bevel=0.005)
    m.mark("Toggle", (0, 0.01, 0.006))
    m("Glow-Dot", "green").ellipsoid((0, -0.045, 0.008), (0.006, 0.006, 0.004), 10, 6)


def cardbox(m):
    """A cardboard box a metre each way (the builder scales it): its lid's two flaps meeting in the middle, tape over
    the seam and down two sides; the origin under its bottom."""
    m("Box", "brown").box((0, 0.49, 0), (1.0, 0.98, 1.0), bevel=0.02)
    for s in (-1, 1):
        m("Box", "brown").box((s * 0.25, 0.985, 0), (0.494, 0.03, 0.99), bevel=0.012)
    m("Tape", "yellow").box((0, 1.0, 0), (0.18, 0.012, 1.006), bevel=0.003)
    for s in (-1, 1):
        m("Tape", "yellow").box((0, 0.86, s * 0.502), (0.18, 0.28, 0.008), bevel=0.003)


def sill(m):
    """The customers' sill outside the window: a board on two brackets; the origin at the board's middle."""
    m("Board", "brown").box((0, 0, 0), (1.25, 0.03, 0.36), bevel=0.006)
    for s in (-1, 1):
        m("Brackets", "brown").prism([(0.06, -0.012), (0.18, -0.012), (0.17, -0.04), (0.06, -0.155)], (s * 0.45, 0, 0), (0, 0, 1), (0, 1, 0), 0.03)


def cobweb(m):
    """A cobweb across an upper corner, a metre across (the builder scales it): threads from its middle out to the two
    walls and down the corner, rings round them. The corner is at the origin, the walls along +x and +z."""
    u, v, d = Vector((1, 0, 0)), Vector((0, 0, 1)), Vector((0, -1, 0))
    pts = [u, (u + v) * 0.45 + d * 0.35, v, (v + d) * 0.5, d, (u + d) * 0.5]
    mid = (pts[0] + pts[2] + pts[4]) / 3
    r = 0.004
    for p in pts:
        m("Web", "white").tube([mid, p], r, 4, caps=False)
    for p in (pts[0], pts[2], pts[4]):
        m("Web", "white").tube([p, Vector((0, 0, 0))], r, 4, caps=False)
    for k in range(1, 5):
        f = k / 5
        for i in range(len(pts)):
            a, b = mid.lerp(pts[i], f), mid.lerp(pts[(i + 1) % len(pts)], f)
            sag = (a + b) / 2 + (mid - (a + b) / 2) * 0.12
            m("Web", "white").tube([a, sag, b], r * 0.8, 4, caps=False)
    m.mark("Middle", mid)


BUILD = {"stamp": stamp, "inkpad": inkpad, "box": box, "calculator": calculator, "certificate": certificate,
         "sign_counter": sign_counter, "sign_dept": sign_dept, "lamp": lamp, "doorknob": doorknob, "overflow": overflow,
         "incline": incline, "switch": switch, "cardbox": cardbox, "cobweb": cobweb, "sill": sill}

clear()
shown = []
for name, make in BUILD.items():
    if ONLY and name not in ONLY:
        continue
    model = Model("Desk_" + name)
    make(model)
    objs = model.export(OUT)
    if PREVIEW:
        shown.append((stash(objs, name, (len(shown) * 6.0, 0, 0)), name))
    else:
        clear()
if PREVIEW:
    for root, name in shown:
        for r, _ in shown:
            for o in [r] + list(r.children):
                o.hide_render = r is not root
        preview_fit(root, os.path.join(PREVIEW, f"desk_{name}.png"), 25, 25)
print("done ->", OUT)
