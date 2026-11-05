# The player's own hands and body (DESIGN.md 2б): the hands in white cuffs that hold things before the eyes — the
# right one under a sample, the left one at a sheet's corner, a fist round the stamp, a hand at the side of the box or
# of a certificate — and the body in a lab coat that only the mirror sees. One FBX each into
# <Models>/Body/Body_<name>.fbx; PlayerBody and AwardWall put them in at runtime through ModelLibrary (conventions at
# the top of kit.py). A hand's origin is the point PlayerBody hangs it on (the hands' anchor, the sheet's leaf, the
# stamp, the box); the body stands on its origin facing +z, its arms the groups ArmL and ArmR, turning at the shoulders.
#   blender -b --factory-startup -P Tools/Blender/body.py -- <Models dir> [--preview <dir>] [--only coat]
import os, sys, math
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kit import *

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = os.path.join(argv[0] if argv and not argv[0].startswith("--") else os.path.join(os.getcwd(), "Assets", "Lab3D", "Models"), "Body")
PREVIEW = argv[argv.index("--preview") + 1] if "--preview" in argv else None
ONLY = argv[argv.index("--only") + 1].split(",") if "--only" in argv else None


def cuff(m, wrist, back):
    """The wrist out of a white cuff: skin from the hand back to `back`, a sleeve's end round it."""
    wrist, back = Vector(wrist), Vector(back)
    d = (back - wrist).normalized()
    m("Wrist", "skin").tube([wrist, wrist + d * 0.04], 0.021, 12)
    m("Cuff", "white").tube([wrist + d * 0.03, back], [0.031, 0.034], 14)


def finger(m, pts, r=0.0085):
    m("Fingers", "skin").tube([Vector(p) for p in pts], r, 8)


def hand_carry(m):
    """The right hand, palm up under what it carries, the fingers curled up in front, the thumb at the side."""
    m("Palm", "skin").ellipsoid((0, -0.016, -0.005), (0.043, 0.014, 0.05), 16, 8)
    for i in range(4):
        x = -0.027 + i * 0.018
        ln = 1.0 - abs(i - 1.3) * 0.08
        finger(m, [(x, -0.014, 0.036), (x, -0.006, 0.058 * ln), (x, 0.01, 0.07 * ln), (x, 0.022, 0.066 * ln)], 0.0082)
    finger(m, [(-0.036, -0.016, 0.0), (-0.052, -0.006, 0.016), (-0.056, 0.006, 0.032)], 0.0095)
    cuff(m, (0.004, -0.022, -0.045), (0.018, -0.07, -0.16))


def hand_sheet(m):
    """The left hand at a sheet's lower left corner: the thumb on the paper, the palm and fingers behind it."""
    m("Palm", "skin").ellipsoid((-0.1, -0.016, -0.14), (0.04, 0.012, 0.046), 16, 8, m=rot((0, -30, 0)))
    for i in range(4):
        a = math.radians(-30)
        base = Vector((-0.1, -0.016, -0.14)) + rot((0, -30, 0)) @ Vector((-0.022 + i * 0.015, 0, 0.04))
        finger(m, [base, base + rot((0, -30, 0)) @ Vector((0, -0.006, 0.025)), base + rot((0, -30, 0)) @ Vector((0, -0.016, 0.035))], 0.0078)
    finger(m, [(-0.075, -0.012, -0.15), (-0.07, 0.0, -0.128), (-0.075, 0.006, -0.108)], 0.0095)
    cuff(m, (-0.11, -0.022, -0.18), (-0.13, -0.045, -0.29))


def hand_fist(m):
    """A fist round the stamp's handle (the origin is the stamp's)."""
    m("Palm", "skin").ellipsoid((0.004, 0.07, -0.004), (0.033, 0.03, 0.036), 16, 10)
    for i in range(4):
        y = 0.088 - i * 0.013
        m("Fingers", "skin").ellipsoid((0.0, y, 0.028), (0.026, 0.0075, 0.012), 10, 6)
    finger(m, [(-0.028, 0.06, 0.01), (-0.034, 0.075, 0.025), (-0.022, 0.085, 0.034)], 0.0095)
    cuff(m, (0.012, 0.068, -0.035), (0.026, 0.07, -0.15))


def hand_box(m):
    """The right hand flat on the right side of the box, its fingers over the edge (the left one is its mirror)."""
    m("Palm", "skin").ellipsoid((0.15, 0.08, -0.09), (0.014, 0.045, 0.04), 14, 8)
    for i in range(4):
        z = -0.115 + i * 0.016
        finger(m, [(0.152, 0.115, z), (0.148, 0.14, z), (0.135, 0.152, z)], 0.0078)
    finger(m, [(0.16, 0.06, -0.06), (0.16, 0.07, -0.035), (0.155, 0.085, -0.025)], 0.0092)
    cuff(m, (0.155, 0.052, -0.125), (0.172, 0.02, -0.23))


def hand_cert(m):
    """The right hand at the right side of a certificate held up (the left one is its mirror)."""
    m("Palm", "skin").ellipsoid((0.18, -0.06, -0.012), (0.014, 0.045, 0.038), 14, 8)
    for i in range(4):
        z = -0.03 + i * 0.012
        finger(m, [(0.183, -0.03 + i * 0.004, z - 0.01), (0.181, -0.02 + i * 0.004, z + 0.012), (0.172, -0.018 + i * 0.004, z + 0.024)], 0.0072)
    finger(m, [(0.17, -0.07, 0.012), (0.162, -0.05, 0.018), (0.158, -0.035, 0.016)], 0.009)
    cuff(m, (0.188, -0.095, -0.045), (0.205, -0.145, -0.125))


def coat(m):
    """The player in a white lab coat, trousers and shoes, glasses; the arms hang from the shoulders (ArmL, ArmR)."""
    for s in (-1, 1):
        m("Legs", "dark").tube([(s * 0.09, 0.82, -0.05), (s * 0.092, 0.45, -0.045), (s * 0.092, 0.08, -0.05)], [0.07, 0.062, 0.056], 12)
        m("Shoes", "dark").ellipsoid((s * 0.092, 0.045, -0.02), (0.055, 0.045, 0.11), 14, 8)
    prof = [(0.5, 0.25, 0.17), (0.75, 0.215, 0.15), (0.95, 0.2, 0.14), (1.15, 0.215, 0.145), (1.3, 0.225, 0.14), (1.38, 0.2, 0.125),
            (1.42, 0.14, 0.1), (1.44, 0.07, 0.065), (1.445, 0.0)]
    m("Coat", "white").lathe([(y, rx, rz, 0, -0.06) for y, rx, rz in prof[:-1]] + [(1.445, 0.0, 0.0, 0, -0.06)], segs=28, n=2.4, smooth=1)
    for s in (-1, 1):   # lapels, pockets
        m("Lapels", "white").prism([(0.0, 0.0), (s * 0.075, -0.04), (s * 0.06, -0.2)], (s * 0.03, 1.42, 0.075), (1, 0, 0), (0, 1, 0), 0.008)
        m("Pockets", "white").box((s * 0.11, 0.9, 0.096), (0.11, 0.1, 0.008), bevel=0.003)
    for y in (1.2, 1.05, 0.9, 0.75):
        m("Buttons", "grey").ellipsoid((0.0, y, 0.088), (0.012, 0.012, 0.006), 8, 5)
    m("Skin", "skin").tube([(0, 1.41, -0.06), (0, 1.5, -0.06)], 0.05, 12)
    m("Skin", "skin").ellipsoid((0, 1.6, -0.06), (0.11, 0.13, 0.115), 20, 12)
    m("Skin", "skin").ellipsoid((0, 1.585, 0.06), (0.02, 0.025, 0.022), 10, 6)
    m("Hair", "brown").cap((0, 1.61, -0.065), (0.12, 0.138, 0.124), lambda a: lerp(1.45, 0.75, math.cos(a)) if math.cos(a) >= 0 else lerp(1.45, 1.9, -math.cos(a)), 0.02)
    for s in (-1, 1):
        c = Vector((s * 0.045, 1.62, 0.055))
        m("Glasses", "dark").loop([c + Vector((math.cos(2 * math.pi * k / 14) * 0.03, math.sin(2 * math.pi * k / 14) * 0.026, 0)) for k in range(14)], 0.004, 5)
        m("Glasses", "dark").tube([c + Vector((s * 0.03, 0, 0)), Vector((s * 0.11, 1.625, -0.04))], 0.003, 5)
        m("Eyes", "dark").ellipsoid((s * 0.042, 1.62, 0.04), (0.012, 0.016, 0.008), 8, 5)
    m("Glasses", "dark").tube([(-0.015, 1.622, 0.058), (0.015, 1.622, 0.058)], 0.003, 5)
    for s, g in ((-1, "ArmL"), (1, "ArmR")):
        sh = Vector((s * 0.25, 1.36, -0.05))
        m(f"{g}-Sleeve", "white").tube([sh, sh + Vector((s * 0.035, -0.25, 0.01)), sh + Vector((s * 0.045, -0.5, 0.04))], [0.06, 0.052, 0.046], 12)
        m(f"{g}-Hand", "skin").ellipsoid(sh + Vector((s * 0.045, -0.57, 0.045)), (0.028, 0.055, 0.04), 12, 8)
        m.mark(g, sh)


BUILD = {"hand_carry": hand_carry, "hand_sheet": hand_sheet, "hand_fist": hand_fist, "hand_box": hand_box,
         "hand_cert": hand_cert, "coat": coat}

clear()
shown = []
for name, make in BUILD.items():
    if ONLY and name not in ONLY:
        continue
    model = Model("Body_" + name)
    make(model)
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
        preview_fit(root, os.path.join(PREVIEW, f"body_{name}.png"), 25, 20)
print("done ->", OUT)
