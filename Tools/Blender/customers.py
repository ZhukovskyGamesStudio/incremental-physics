# The customers behind the counter's window (DESIGN.md 2б, «Заказчики за окном»): people of the trade their order
# speaks of and a few creatures, as smooth one-piece figures in the kit's colours. One FBX each into
# <Models>/Customers/Persona_<who>.fbx; Persona.cs instantiates them through ModelLibrary and paints them by the
# colour in each part's name (see the conventions at the top of kit.py). A figure stands on its origin facing +z
# (Unity axes); its "Top" marker is where the "!" and the stars go over it.
#   blender -b --factory-startup -P Tools/Blender/customers.py -- <Models dir> [--preview <dir>] [--only sailor,cook]
# --preview also renders Workbench pictures of them all (front and three-quarter) into <dir>.
import os, sys, math
sys.dont_write_bytecode = True   # no __pycache__ beside the scripts
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from kit import *

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = os.path.join(argv[0] if argv and not argv[0].startswith("--") else os.path.join(os.getcwd(), "Assets", "Lab3D", "Models"), "Customers")
PREVIEW = argv[argv.index("--preview") + 1] if "--preview" in argv else None
ONLY = argv[argv.index("--only") + 1].split(",") if "--only" in argv else None

ALL = ["driver", "engineer", "cosmonaut", "librarian", "baker", "granny", "kid", "mechanic", "postman", "strongman",
       "teacher", "robot", "curator", "blacksmith", "sailor", "tailor", "fish", "gnome", "beaver", "duck", "jeweler",
       "cook", "fisherman", "vampire", "musician", "clown"]

# the head: (height over its centre, radius), chin to crown
HEAD = [(-0.135, 0.0), (-0.128, 0.045), (-0.11, 0.077), (-0.075, 0.099), (-0.03, 0.112), (0.02, 0.118),
        (0.07, 0.11), (0.105, 0.08), (0.127, 0.042), (0.135, 0.0)]


class Figure:
    """A person: legs, a coat (the trade's colour) with arms along it and hands, a neck, a head with a face. Keeps
    the old proportions (Persona.cs): hips at 0.82, shoulders at 1.42, the head's centre at 1.6 of its height."""

    def __init__(self, m, coat, legs="dark", tall=1.0, wide=1.0, skin="skin", sleeves=None, shoes="dark", skirt=False,
                 left=None, right=None, mouth=True, brows="dark", eyes="dark"):
        self.m, self.coat, self.skin = m, coat, skin
        t, w = tall, wide
        self.t, self.w = t, w
        self.hip, self.sh, self.hd = 0.82 * t, 1.42 * t, 1.6 * t
        self.hs = 1.0 if t >= 0.95 else 0.96
        hip, sh, hd = self.hip, self.sh, self.hd
        dz = 1 + (w - 1) * 0.45   # a wide figure is deeper too
        # the coat, bottom to top: (y, rx, rz, cz)
        bottom = [(0.3, 0.25 * w, 0.2 * dz, 0.0), (0.5, 0.22 * w, 0.17 * dz, 0.0)] if skirt else [(hip - 0.1, 0.168 * w, 0.118 * dz, 0.0)]
        self.prof = bottom + [
            (hip + 0.04, 0.173 * w, 0.121 * dz, 0.0), (hip + 0.2 * t, 0.166 * w, 0.12 * dz, 0.005),
            (sh - 0.2 * t, 0.19 * w, 0.13 * dz, 0.005), (sh - 0.075, 0.2 * w, 0.125 * dz, 0.0),
            (sh - 0.02, 0.178 * w, 0.11 * dz, -0.005), (sh + 0.018, 0.12 * w, 0.085, -0.008),
            (sh + 0.036, 0.06, 0.055, -0.008)]
        self.n = 2.4
        m("Coat", coat).lathe([(y, rx, rz, 0, cz) for y, rx, rz, cz in self.prof] + [(sh + 0.042, 0)], segs=28, n=self.n, smooth=1)
        # legs and shoes
        lw = 1 + (w - 1) * 0.6
        for s in (-1, 1):
            x = s * 0.085 * w
            m("Legs", legs).tube([(x, hip, 0), (x, hip * 0.55, 0.008), (x, 0.09, 0)], [0.064 * lw, 0.056 * lw, 0.05 * lw], 12)
            m("Shoes", shoes).ellipsoid((x, 0.045, 0.035), (0.052 * lw, 0.046, 0.095), 14, 8)
        # arms: down along the coat, or bent to hold something to the chest (left / right = (x, y) of the hand)
        self.hands = {}
        for s, hold in ((-1, left), (1, right)):
            self.arm(s, hold, sleeves or coat)
        # neck, head, face
        hsk = self.hs
        m("Skin", skin).tube([(0, sh - 0.03, -0.008), (0, hd - 0.07 * hsk, 0.0)], 0.047 * hsk, 12)
        self.head = [(dy * hsk, r * hsk) for dy, r in HEAD]
        m("Skin", skin).lathe([(hd + dy, r) for dy, r in self.head], segs=24)
        for s in (-1, 1):   # ears
            m("Skin", skin).ellipsoid((s * 0.112 * hsk, hd - 0.005, -0.005), (0.02, 0.033, 0.027), 10, 6)
        m("Skin", skin).ellipsoid(self.face(0, hd - 0.022, -0.012), (0.021 * hsk, 0.03 * hsk, 0.03), 12, 8)   # nose
        for s in (-1, 1):
            m("Eyes", eyes).ellipsoid(self.face(s * 0.042, hd + 0.02, -0.006), (0.0145, 0.02, 0.009), 10, 6)
            if brows:
                m("Brows", brows).tube([self.face(s * 0.02, hd + 0.053, 0.003), self.face(s * 0.043, hd + 0.059, 0.003),
                                        self.face(s * 0.064, hd + 0.052, 0.003)], [0.005, 0.0065, 0.004], 6)
        if mouth:
            self.mouth()

    # ---------------------------------------------------------------- places on it

    def shape(self, y):
        """The coat's section at height y: (rx, rz, cx, cz), for layers laid on it."""
        return (interp(self.prof, y, 1), interp(self.prof, y, 2), 0.0, interp(self.prof, y, 3))

    def body(self, x, y, off=0.0, back=False):
        """The point of the coat's front (or back) at x, y, lifted `off` off it."""
        rx, rz, cx, cz = self.shape(y)
        s = max(-0.999, min(0.999, (x - cx) / (rx + off)))
        sa = math.copysign(abs(s) ** (self.n / 2), s)
        ca = math.sqrt(max(0.0, 1 - sa * sa))
        z = (rz + off) * sq(ca, self.n)
        return Vector((x, y, cz - z if back else cz + z))

    def face(self, x, y, off=0.0, back=False):
        """The point of the head's front (or back) at x, y, lifted `off` off it."""
        r = interp([(self.hd + dy, rr) for dy, rr in self.head], y, 1) + off
        z = math.sqrt(max(r * r - x * x, 0.0))
        return Vector((x, y, -z if back else z))

    def head_at(self, y, a, off=0.0):
        """The head's surface at height y and angle a (0 = front, +x at π/2)."""
        r = interp([(self.hd + dy, rr) for dy, rr in self.head], y, 1) + off
        return Vector((r * math.sin(a), y, r * math.cos(a)))

    # ---------------------------------------------------------------- parts of it

    def arm(self, s, hold, colour):
        m, w, t, sh, hip = self.m, self.w, self.t, self.sh, self.hip
        aw = 1 + (w - 1) * 0.7
        S = (s * (0.19 * w - 0.035), sh - 0.065, -0.005)
        J = (s * 0.205 * w, sh - 0.085, -0.004)
        E = (s * (0.217 * w + 0.012 * aw), sh - 0.32 * t, -0.012)
        if hold is None:
            W = Vector((s * (0.207 * w + 0.016 * aw), hip + 0.03 * t, 0.03))
            m("Sleeves", colour).tube([S, J, E, W], [0.054 * aw, 0.055 * aw, 0.047 * aw, 0.041 * aw], 12)
            hand = W + Vector((0, -0.062, 0.012))
            m("Hands", self.skin).ellipsoid(hand, (0.03 * aw, 0.062, 0.043), 12, 8, m=rot((4, 0, s * -3)))
            m("Hands", self.skin).ellipsoid(hand + Vector((-s * 0.02, 0.012, 0.03)), (0.014, 0.03, 0.014), 8, 6, m=rot((-20, 0, s * 20)))
        else:
            hx, hy = hold
            H = self.body(s * hx, hy, 0.05)
            mid = self.body(s * (abs(hx) + 0.6 * (0.2 * w - abs(hx))), (E[1] + hy) / 2, 0.055)
            m("Sleeves", colour).tube([S, J, E, mid, H], [0.054 * aw, 0.055 * aw, 0.047 * aw, 0.044 * aw, 0.04 * aw], 12)
            hand = H + Vector((-s * 0.04, 0.0, 0.0))
            m("Hands", self.skin).ellipsoid(hand, (0.055, 0.03, 0.04), 12, 8, m=rot((0, 0, s * 15)))
        self.hands[s] = hand

    def mouth(self, colour="dark", wide=0.028):
        hd = self.hd
        self.m("Mouth", colour).tube([self.face(-wide, hd - 0.058, 0.002), self.face(0, hd - 0.066, 0.002), self.face(wide, hd - 0.058, 0.002)],
                                     [0.0045, 0.0055, 0.0045], 6)

    def hair(self, colour, front=0.62, side=1.45, back=1.95, peak=0.0, r=1.075, up=0.006):
        """Hair over the crown: down to `front` radians from the top over the forehead, `side` over the ears,
        `back` at the nape; a widow's peak adds to the front's middle."""
        hd, hs = self.hd, self.hs

        def phimax(a):
            c = math.cos(a)
            base = lerp(side, front, c) if c >= 0 else lerp(side, back, -c)
            return base + peak * math.exp(-(a if a < math.pi else a - 2 * math.pi) ** 2 / 0.05)
        self.m("Hair", colour).cap((0, hd + up, -0.004), (0.118 * hs * r, 0.135 * hs * r, 0.118 * hs * r), phimax, 0.02)

    def beard(self, colour, long=0.0):
        hd = self.hd

        def phimax(a):
            c = math.cos(a)
            return lerp(1.75, 1.2, c) if c >= 0 else lerp(1.75, 0.55, -c)
        self.m("Beard", colour).cap((0, hd - 0.035 - long * 0.5, 0.012), (0.126, 0.16 + long, 0.127), phimax, 0.025, down=True)

    def moustache(self, colour="dark", curl=False):
        hd = self.hd
        xs = [-0.058, -0.032, 0.0, 0.032, 0.058]
        ys = [-0.06, -0.049, -0.046, -0.049, -0.06]
        rr = [0.006, 0.0125, 0.011, 0.0125, 0.006]
        pts = [self.face(x, hd + y, 0.004) for x, y in zip(xs, ys)]
        if curl:
            pts = [self.face(-0.074, hd - 0.035, 0.0), self.face(-0.07, hd - 0.055, 0.004)] + pts + [self.face(0.07, hd - 0.055, 0.004), self.face(0.074, hd - 0.035, 0.0)]
            rr = [0.005, 0.007] + rr + [0.007, 0.005]
        self.m("Moustache", colour).tube(pts, rr, 8)

    def glasses(self, colour="dark"):
        hd = self.hd
        y = hd + 0.02
        for s in (-1, 1):
            c = self.face(s * 0.042, y, 0.014)
            self.m("Glasses", colour).loop([c + Vector((math.cos(2 * math.pi * k / 16) * 0.029, math.sin(2 * math.pi * k / 16) * 0.025, 0)) for k in range(16)], 0.0035)
            temple = [c + Vector((s * 0.029, 0, 0))] + [self.head_at(y, s * a, 0.004) for a in (1.05, 1.3, 1.55)]
            self.m("Glasses", colour).tube(temple, 0.003, 5, caps=True)
        self.m("Glasses", colour).tube([self.face(-0.013, y, 0.016), self.face(0, y + 0.004, 0.018), self.face(0.013, y, 0.016)], 0.003, 5)

    def bowtie(self, colour, y=None):
        y = self.sh + 0.006 if y is None else y
        c = self.body(0, y, 0.012)
        for s in (-1, 1):
            self.m("Bow", colour).ellipsoid(c + Vector((s * 0.03, 0, 0)), (0.03, 0.022, 0.012), 10, 6, m=rot((0, 0, s * 8)))
        self.m("Bow", colour).ellipsoid(c + Vector((0, 0, 0.004)), (0.013, 0.016, 0.013), 8, 6)

    def buttons(self, colour, xs, ys, r=0.013):
        for x in xs:
            for y in ys:
                self.m("Buttons", colour).ellipsoid(self.body(x, y, 0.002), (r, r, r * 0.5), 10, 6)

    def band(self, colour, y0, y1, off=0.004, name="Band"):
        """A stripe all round the coat between two heights."""
        self.m(name, colour).sheet(self.shape, [y0, (y0 + y1) / 2, y1], 0, 2 * math.pi, off, -0.01, segs=28, n=self.n)

    def strip(self, colour, rows, name="Strip", off=0.006):
        self.m(name, colour).strip(rows, lambda x, y, o: self.body(x, y, o), off, -0.004)

    def top(self, y):
        self.m.mark("Top", (0, y, 0))


def lathe_on_head(fig, m, colour, prof, name="Hat", segs=28, wave=None, cz=0.0, smooth=0):
    """A hat turned round the head's axis: prof is [(height over the head's centre, radius[, rz]), ...]."""
    m(name, colour).lathe([(fig.hd + p[0], p[1], p[2] if len(p) > 2 else p[1], 0, cz) for p in prof], segs=segs, wave=wave, smooth=smooth)


def visor(fig, m, colour, y, reach=0.075, width=0.1, tilt=12, back=False, name="Hat"):
    s = -1 if back else 1
    m(name, colour).ellipsoid((0, fig.hd + y, s * 0.118), (width, 0.011, reach), 20, 6, m=rot((s * tilt, 0, 0)))


# ================================================================ the people

def driver(m):
    f = Figure(m, "blue")
    f.hair("dark", side=1.6, back=1.9)
    lathe_on_head(f, m, "blue", [(0.02, 0.126), (0.065, 0.13), (0.1, 0.12), (0.128, 0.085), (0.142, 0)], cz=0.006)
    visor(f, m, "blue", 0.035)
    f.moustache()
    f.top(f.hd + 0.2)


def engineer(m):
    f = Figure(m, "orange")
    f.band("white", f.hip + 0.12, f.hip + 0.16)
    f.band("white", f.sh - 0.2, f.sh - 0.16)
    lathe_on_head(f, m, "yellow", [(0.04, 0.0), (0.04, 0.135), (0.09, 0.133), (0.13, 0.105), (0.16, 0.055), (0.17, 0.0)])
    m("Hat", "yellow").lathe([(f.hd + 0.03, 0.0), (f.hd + 0.03, 0.158, 0.168, 0, 0.015), (f.hd + 0.045, 0.158, 0.168, 0, 0.015), (f.hd + 0.045, 0.0)], segs=28)
    m("Hat", "yellow").tube([(0, f.hd + 0.12, 0.09), (0, f.hd + 0.165, 0.035), (0, f.hd + 0.17, -0.03), (0, f.hd + 0.14, -0.09)], 0.012, 8)
    f.glasses()
    f.top(f.hd + 0.2)


def cosmonaut(m):
    f = Figure(m, "white", legs="white", shoes="grey")
    f.hair("brown", side=1.5)
    m("Helmet", "clear").ellipsoid((0, f.hd + 0.01, 0.005), (0.19, 0.2, 0.19), 24, 14)
    m("Collar", "white").loop([(math.sin(2 * math.pi * k / 24) * 0.15, f.sh + 0.03, math.cos(2 * math.pi * k / 24) * 0.14 - 0.005) for k in range(24)], 0.025, 8)
    m("Pack", "white").box((0, f.sh - 0.2, -0.17), (0.3, 0.38, 0.12), bevel=0.03)
    m("Panel", "blue").box(f.body(0, f.sh - 0.2, 0.012), (0.14, 0.09, 0.03), bevel=0.008)
    for i, c in enumerate(("red", "yellow", "green")):
        m("Lamps", c).ellipsoid(f.body(-0.04 + 0.04 * i, f.sh - 0.2, 0.03), (0.012, 0.012, 0.008), 8, 5)
    m("Patch", "red").ellipsoid(f.body(-0.2, f.sh - 0.13, -0.01) + Vector((-0.04, 0, 0)), (0.012, 0.04, 0.035), 10, 6)
    f.top(f.hd + 0.2)


def librarian(m):
    f = Figure(m, "green", left=(0.06, 1.12))
    f.hair("brown", side=1.65, back=2.05)
    m("Hair", "brown").ellipsoid((0, f.hd + 0.07, -0.13), (0.06, 0.055, 0.05), 14, 8)
    f.glasses()
    f.buttons("dark", [0.0], [f.hip + 0.05, f.hip + 0.17])
    book = f.body(-0.04, 1.14, 0.03)
    m("Book", "red").box(book, (0.17, 0.23, 0.04), bevel=0.006, m=rot((-8, 0, 12)))
    m("Pages", "white").box(book + Vector((0.006, 0, 0.0)), (0.165, 0.215, 0.03), m=rot((-8, 0, 12)))
    f.top(f.hd + 0.2)


def baker(m):
    f = Figure(m, "white")
    lathe_on_head(f, m, "white", [(0.05, 0.121), (0.1, 0.124)])
    lathe_on_head(f, m, "white", [(0.095, 0.122), (0.13, 0.15), (0.18, 0.158), (0.215, 0.13), (0.235, 0.07), (0.24, 0.0)],
                  wave=lambda a: 0.045 * math.cos(7 * a), smooth=2)
    f.moustache()
    f.buttons("dark", [0.0], [f.hip + 0.06, f.hip + 0.2, f.hip + 0.34])
    # a loaf under the right arm, pressed to the side
    loaf = Vector((0.15, f.hip + 0.27, 0.06))
    m("Loaf", "brown").ellipsoid(loaf, (0.05, 0.05, 0.2), 14, 10, m=rot((25, -15, 0)))
    for k in (-1, 0, 1):
        m("Loaf", "white").ellipsoid(loaf + rot((25, -15, 0)) @ Vector((0, 0.04, k * 0.09)), (0.022, 0.011, 0.008), 8, 5, m=rot((25, -15, 30)))
    f.top(f.hd + 0.24)


def granny(m):
    f = Figure(m, "purple", wide=1.1, skirt=True, brows="grey")
    hd = f.hd

    def phimax(a):
        c = math.cos(a)
        return lerp(1.95, 0.8, c) if c >= 0 else lerp(1.95, 2.25, -c)
    m("Scarf", "red").cap((0, hd + 0.005, -0.006), (0.132, 0.148, 0.133), phimax, 0.02)
    m("Scarf", "red").ellipsoid((0, hd - 0.155, 0.05), (0.034, 0.028, 0.03), 10, 6)
    for s in (-1, 1):
        m("Scarf", "red").ellipsoid((s * 0.03, hd - 0.2, 0.065), (0.022, 0.05, 0.012), 10, 6, m=rot((-15, 0, s * 25)))
    f.glasses()
    f.buttons("white", [0.0], [f.sh - 0.12, f.sh - 0.24, f.sh - 0.36])
    f.top(f.hd + 0.2)


def kid(m):
    f = Figure(m, "red", legs="blue", tall=0.82)
    f.hair("orange", front=0.7, side=1.55)
    lathe_on_head(f, m, "red", [(0.03, 0.125), (0.07, 0.128), (0.105, 0.11), (0.128, 0.072), (0.14, 0)])
    visor(f, m, "red", 0.04, back=True)
    m("Mouth", "dark").tube([f.face(-0.035, f.hd - 0.05, 0.002), f.face(-0.015, f.hd - 0.066, 0.002), f.face(0.015, f.hd - 0.066, 0.002), f.face(0.035, f.hd - 0.05, 0.002)], 0.005, 6)
    f.band("white", f.hip + 0.1, f.hip + 0.14)
    f.top(f.hd + 0.2)


def mechanic(m):
    f = Figure(m, "blue")
    f.hair("dark", side=1.55)
    lathe_on_head(f, m, "red", [(0.025, 0.126), (0.07, 0.13), (0.105, 0.115), (0.128, 0.078), (0.142, 0)])
    visor(f, m, "red", 0.035)
    m("Smudge", "dark").ellipsoid(f.face(0.06, f.hd - 0.03, -0.004), (0.022, 0.012, 0.006), 10, 5, m=rot((0, 30, 20)))
    # the overalls' bib with a pocket, a spanner in it
    pocket = f.body(-0.07, f.sh - 0.2, 0.008)
    m("Pocket", "blue").box(pocket, (0.1, 0.09, 0.02), bevel=0.005)
    m("Spanner", "metal").box(pocket + Vector((0.015, 0.07, 0.0)), (0.018, 0.12, 0.008), bevel=0.003, m=rot((0, 0, -10)))
    m("Spanner", "metal").loop([pocket + Vector((0.025 + math.sin(2 * math.pi * k / 10) * 0.018, 0.14 + math.cos(2 * math.pi * k / 10) * 0.018, 0)) for k in range(10)], 0.006, 6)
    for s in (-1, 1):
        f.strip("blue", [(s * 0.1, f.sh - 0.01, 0.02), (s * 0.1, f.sh - 0.15, 0.02), (s * 0.08, f.sh - 0.27, 0.02)], name="Straps", off=0.008)
        m("Buttons", "metal").ellipsoid(f.body(s * 0.08, f.sh - 0.26, 0.012), (0.014, 0.014, 0.007), 8, 5)
    f.top(f.hd + 0.2)


def postman(m):
    f = Figure(m, "blue")
    f.hair("brown", side=1.55)
    lathe_on_head(f, m, "blue", [(0.02, 0.122), (0.085, 0.124), (0.1, 0.148), (0.122, 0.153), (0.132, 0.12), (0.137, 0.0)])
    visor(f, m, "dark", 0.03, reach=0.065)
    m("Badge", "yellow").ellipsoid(f.face(0, f.hd + 0.07, 0.012), (0.016, 0.016, 0.008), 10, 6)
    f.moustache("brown")
    # a satchel at the right hip on a strap from the left shoulder
    bag = f.body(0.12, f.hip - 0.02, 0.045)
    m("Bag", "brown").box(bag, (0.22, 0.18, 0.07), bevel=0.012, m=rot((0, -12, 0)))
    m("Bag", "brown").box(bag + Vector((0, 0.04, 0.035)), (0.22, 0.1, 0.012), bevel=0.004, m=rot((0, -12, 0)))
    m("Letters", "white").box(bag + Vector((-0.03, 0.1, -0.005)), (0.1, 0.06, 0.012), m=rot((0, -12, 8)))
    f.strip("brown", [(-0.15, f.sh - 0.02, 0.022), (-0.06, f.sh - 0.17, 0.022), (0.04, f.sh - 0.33, 0.022), (0.12, f.hip + 0.08, 0.022)], name="Strap", off=0.01)
    f.top(f.hd + 0.2)


def strongman(m):
    # bare arms and shoulders, a broad chest; a striped singlet over the belly on two straps, a wide belt, a curled moustache
    f = Figure(m, "skin", tall=1.08, wide=1.32, sleeves="skin", brows="dark")
    f.hair("dark", front=0.45, side=1.25, back=1.75, r=1.06)
    f.moustache(curl=True)
    top = f.sh - 0.19
    f.m("Singlet", "red").sheet(f.shape, [f.hip - 0.09, (f.hip + top) / 2, top], 0, 2 * math.pi, 0.007, -0.01, segs=28, n=f.n)
    y = f.hip + 0.04
    while y < top - 0.03:   # white stripes round it
        f.m("Stripes", "white").sheet(f.shape, [y, y + 0.012, y + 0.024], 0, 2 * math.pi, 0.0095, -0.004, segs=28, n=f.n)
        y += 0.07
    for s in (-1, 1):   # the straps over the shoulders, and big arms
        m("Singlet", "red").tube([f.body(s * 0.1, top + 0.02, 0.008), f.body(s * 0.115, f.sh - 0.08, 0.012), f.body(s * 0.12, f.sh + 0.015, 0.016),
                                  f.body(s * 0.12, f.sh + 0.015, 0.016, back=True), f.body(s * 0.115, f.sh - 0.08, 0.012, back=True),
                                  f.body(s * 0.1, top + 0.02, 0.008, back=True)], 0.017, 8)
        m("Muscles", "skin").ellipsoid((s * 0.27, f.sh - 0.08, -0.005), (0.06, 0.065, 0.065), 14, 8)   # the shoulder
        m("Muscles", "skin").ellipsoid((s * 0.285, f.sh - 0.22, 0.014), (0.056, 0.08, 0.058), 14, 8)   # the biceps
    f.band("dark", f.hip - 0.02, f.hip + 0.07, off=0.012, name="Belt")
    m("Buckle", "yellow").box(f.body(0, f.hip + 0.025, 0.02), (0.09, 0.075, 0.016), bevel=0.006)
    f.top(f.hd + 0.2)


def teacher(m):
    f = Figure(m, "brown", brows="grey")
    f.hair("grey", front=0.7, side=1.55)
    f.glasses()
    f.strip("white", [(0, f.sh + 0.02, 0.05), (0, f.sh - 0.1, 0.035), (0, f.sh - 0.22, 0.005)], name="Shirt", off=0.003)
    f.strip("red", [(0, f.sh + 0.012, 0.016), (0, f.sh - 0.02, 0.012), (0, f.sh - 0.1, 0.022), (0, f.sh - 0.3, 0.03), (0, f.sh - 0.36, 0.002)], name="Tie", off=0.01)
    f.buttons("dark", [0.035], [f.hip + 0.08, f.hip + 0.2])
    f.top(f.hd + 0.2)


def curator(m):
    f = Figure(m, "dark", brows="grey")
    f.hair("grey", side=1.6, front=0.9)
    lathe_on_head(f, m, "dark", [(0.075, 0.0), (0.075, 0.108), (0.12, 0.107), (0.3, 0.118), (0.315, 0.12), (0.318, 0.0)])
    lathe_on_head(f, m, "red", [(0.085, 0.112), (0.125, 0.112)], name="HatBand")
    m("Hat", "dark").lathe([(f.hd + 0.068, 0.0), (f.hd + 0.068, 0.172, 0.155), (f.hd + 0.082, 0.172, 0.155), (f.hd + 0.082, 0.0)], segs=28)
    eye = f.face(0.042, f.hd + 0.02, 0.008)
    m("Monocle", "yellow").loop([eye + Vector((math.cos(2 * math.pi * k / 14) * 0.026, math.sin(2 * math.pi * k / 14) * 0.026, 0)) for k in range(14)], 0.004)
    f.strip("white", [(0, f.sh + 0.02, 0.055), (0, f.sh - 0.1, 0.04), (0, f.sh - 0.25, 0.005)], name="Shirt", off=0.003)
    f.bowtie("red")
    f.moustache("grey")
    f.top(f.hd + 0.34)


def blacksmith(m):
    f = Figure(m, "grey", brows="brown")
    f.hair("brown", side=1.5)
    f.beard("brown", long=0.03)
    f.moustache("brown")

    def apron(y):
        rx, rz, cx, cz = f.shape(max(y, f.hip - 0.1))
        if y < f.hip - 0.1:
            rz += (f.hip - 0.1 - y) * 0.15
        return (rx, rz, cx, cz)
    m("Apron", "brown").sheet(apron, [f.sh - 0.16, f.sh - 0.3, f.hip + 0.1, f.hip - 0.1, f.hip - 0.25, 0.42], -1.05, 1.05, 0.016, 0.004, segs=16, n=f.n)
    m("Apron", "brown").tube([f.body(-0.12, f.sh - 0.16, 0.012), f.body(-0.1, f.sh + 0.02, 0.008), (0, f.sh + 0.045, -0.06), f.body(0.1, f.sh + 0.02, 0.008), f.body(0.12, f.sh - 0.16, 0.012)], 0.011, 6)
    m("Hammer", "metal").box(f.body(0.0, f.hip - 0.03, 0.04), (0.11, 0.05, 0.05), bevel=0.008, m=rot((0, 0, 10)))
    f.top(f.hd + 0.2)


def sailor(m):
    f = Figure(m, "white", sleeves="white", brows="grey")
    # the striped vest: blue rings round the body and the arms
    y = f.hip - 0.07
    while y < f.sh - 0.06:
        f.band("blue", y, y + 0.022, off=0.0035, name="Stripes")
        y += 0.052
    for s in (-1, 1):
        S = Vector((s * (0.19 - 0.03), f.sh - 0.045, -0.005))
        E = Vector((s * (0.217 + 0.012), f.sh - 0.32, -0.012))
        W = Vector((s * (0.207 + 0.016), f.hip + 0.03, 0.03))
        for k in range(6):
            t0 = 0.06 + k * 0.165
            a, b = (S.lerp(E, t0 * 2), S.lerp(E, t0 * 2 + 0.06)) if t0 < 0.5 else (E.lerp(W, t0 * 2 - 1), E.lerp(W, t0 * 2 - 1 + 0.06))
            r = 0.055 if t0 < 0.5 else lerp(0.047, 0.041, t0 * 2 - 1)
            m("Stripes", "blue").tube([a, b], r + 0.0035, 12, caps=False)
    # the cap without a peak (бескозырка): a dark band, a white top, two ribbons down the back
    lathe_on_head(f, m, "dark", [(0.03, 0.124), (0.078, 0.126)], name="CapBand")
    lathe_on_head(f, m, "white", [(0.074, 0.128), (0.09, 0.155), (0.108, 0.157), (0.118, 0.11), (0.12, 0.0)], name="Cap")
    for s in (-1, 1):
        m("Ribbons", "dark").tube([f.face(s * 0.02, f.hd + 0.04, 0.012, back=True), f.face(s * 0.035, f.hd - 0.02, 0.008, back=True), f.face(s * 0.045, f.hd - 0.08, 0.004, back=True)], [0.008, 0.007, 0.006], 6)
    f.beard("grey")
    f.moustache("grey")
    f.top(f.hd + 0.2)


def tailor(m):
    f = Figure(m, "yellow")
    f.hair("dark", side=1.55, front=0.66)
    f.glasses()
    for s in (-1, 1):   # a tape measure round the neck, down the front
        f.strip("white", [(s * 0.05, f.sh + 0.04, 0.014), (s * 0.075, f.sh - 0.02, 0.014), (s * 0.085, f.sh - 0.2, 0.014), (s * 0.08, f.sh - 0.36, 0.014)], name="Tape", off=0.008)
    m("Tape", "white").tube([(-0.05, f.sh + 0.045, -0.02), (0, f.sh + 0.05, -0.055), (0.05, f.sh + 0.045, -0.02)], 0.01, 6)
    m("Cushion", "red").ellipsoid(f.hands[-1] + Vector((-0.035, 0.03, 0)), (0.02, 0.035, 0.035), 10, 6)
    f.buttons("dark", [0.0], [f.hip + 0.06, f.hip + 0.2, f.hip + 0.34])
    f.top(f.hd + 0.2)


def jeweler(m):
    f = Figure(m, "purple", sleeves="white", brows="grey")
    f.hair("grey", side=1.6, front=0.95)
    f.moustache("grey")
    eye = f.face(0.042, f.hd + 0.02, -0.004)
    m("Loupe", "dark").tube([eye, eye + Vector((0, 0, 0.045))], [0.026, 0.03], 14, caps=False)
    m("Loupe", "dark").lathe([(0, 0.0), (0, 0.027), (0.045, 0.031), (0.045, 0.0)], segs=14, at=eye, m=rot((90, 0, 0)))
    m("Lens", "clear").ellipsoid(eye + Vector((0, 0, 0.047)), (0.026, 0.026, 0.006), 12, 6)
    f.strip("white", [(0, f.sh + 0.02, 0.06), (0, f.sh - 0.1, 0.045), (0, f.sh - 0.22, 0.005)], name="Shirt", off=0.003)
    f.bowtie("yellow")
    f.buttons("yellow", [0.0], [f.hip + 0.05, f.hip + 0.15, f.hip + 0.25])
    m("Chain", "yellow").tube([f.body(0.0, f.hip + 0.2, 0.006), f.body(0.06, f.hip + 0.16, 0.006), f.body(0.11, f.hip + 0.18, 0.006)], 0.004, 5)
    f.top(f.hd + 0.2)


def cook(m):
    f = Figure(m, "white")
    lathe_on_head(f, m, "white", [(0.05, 0.12), (0.17, 0.124)])
    lathe_on_head(f, m, "white", [(0.165, 0.123), (0.21, 0.162), (0.3, 0.175), (0.37, 0.15), (0.405, 0.09), (0.415, 0.0)],
                  wave=lambda a: 0.05 * math.cos(8 * a), smooth=2)
    f.moustache()
    m("Scarf", "red").loop([(math.sin(2 * math.pi * k / 20) * 0.065, f.sh + 0.03 - 0.015 * math.cos(2 * math.pi * k / 20), math.cos(2 * math.pi * k / 20) * 0.06) for k in range(20)], 0.02, 8)
    m("Scarf", "red").prism([(-0.05, 0.0), (0.05, 0.0), (0.0, -0.08)], f.body(0, f.sh + 0.02, 0.012), (1, 0, 0), (0, 1, 0), 0.014)
    f.buttons("dark", [-0.06, 0.06], [f.hip + 0.12, f.hip + 0.25, f.hip + 0.38])
    f.top(f.hd + 0.44)


def fisherman(m):
    f = Figure(m, "green", brows="grey")
    f.beard("grey")
    f.moustache("grey")
    lathe_on_head(f, m, "green", [(0.04, 0.0), (0.04, 0.128), (0.11, 0.126), (0.15, 0.1), (0.16, 0.0)])
    lathe_on_head(f, m, "green", [(0.075, 0.12), (0.03, 0.2), (0.018, 0.2), (0.064, 0.117)])
    f.buttons("dark", [0.0], [f.hip + 0.06, f.hip + 0.2, f.hip + 0.34], r=0.015)
    # a rod held in the right hand, resting on the right shoulder
    h = f.hands[1]
    a, b = h + Vector((0.0, -0.05, 0.0)), Vector((0.27, f.hd + 0.42, -0.22))
    d = (b - a)
    m("Rod", "brown").tube([a, a + d * 0.5, b], [0.011, 0.008, 0.004], 8)
    m("Reel", "metal").tube([a + d * 0.06 + Vector((0.025, 0, 0)), a + d * 0.06 + Vector((-0.012, 0, 0))], 0.022, 12, caps=False)
    m("Reel", "metal").lathe([(0, 0.0), (0, 0.022), (0.035, 0.022), (0.035, 0.0)], segs=12, at=a + d * 0.06 + Vector((-0.012, 0, 0)), m=rot((0, 0, -90)))
    f.top(f.hd + 0.2)


def vampire(m):
    f = Figure(m, "dark", skin="white", eyes="red", mouth=False)
    f.hair("dark", front=0.42, side=1.3, back=1.85, peak=0.28, r=1.06)
    hd = f.hd
    m("Mouth", "dark").tube([f.face(-0.03, hd - 0.06, 0.002), f.face(0, hd - 0.058, 0.002), f.face(0.03, hd - 0.06, 0.002)], 0.0045, 6)
    for s in (-1, 1):
        m("Fangs", "white").lathe([(0.0, 0.007), (-0.022, 0.0)], segs=8, at=f.face(s * 0.017, hd - 0.062, 0.001))

    def cape(y):
        rx, rz, cx, cz = f.shape(max(y, f.hip - 0.1))
        k = max(0.0, f.sh - 0.06 - y)
        return (rx + 0.06 + k * 0.05, rz + 0.05 + k * 0.08, cx, cz)
    m("Cape", "dark").sheet(cape, [f.sh + 0.03, f.sh - 0.06, f.sh - 0.25, f.hip, f.hip - 0.3, 0.12], 1.25, 2 * math.pi - 1.25, 0.012, 0.0, segs=20, n=f.n)

    def collar(y):
        k = (y - f.sh) / 0.2
        return (0.09 + k * 0.1, 0.075 + k * 0.08, 0.0, -0.01)
    m("Collar", "red").sheet(collar, [f.sh + 0.01, f.sh + 0.1, f.sh + 0.2], 1.05, 2 * math.pi - 1.05, 0.012, 0.0, segs=18)
    m("Brooch", "red").ellipsoid(f.body(0, f.sh + 0.0, 0.01), (0.02, 0.02, 0.012), 10, 6)
    f.top(f.hd + 0.2)


def musician(m):
    f = Figure(m, "blue", left=(0.03, 1.13))
    f.hair("brown", side=1.55, front=0.72, r=1.09)
    for k in range(16):   # curls over the crown
        a = 2 * math.pi * k / 16
        for phi in (0.45, 0.95):
            if phi > 0.6 and math.cos(a) > 0.75:
                continue
            d = Vector((math.sin(phi) * math.sin(a), math.cos(phi), math.sin(phi) * math.cos(a)))
            m("Hair", "brown").ellipsoid(Vector((0, f.hd + 0.006, -0.004)) + Vector((d.x * 0.128, d.y * 0.146, d.z * 0.128)), 0.03, 10, 6)
    f.bowtie("red")
    # a violin held to the chest by the left hand, its neck up towards the right shoulder: built lying in its own space
    # (x across, y along it to the scroll, z out of its top), then put in place
    ctrl = [(-0.18, 0.0), (-0.172, 0.055), (-0.15, 0.088), (-0.1, 0.102), (-0.05, 0.094), (-0.015, 0.07), (0.015, 0.058),
            (0.045, 0.064), (0.08, 0.08), (0.115, 0.082), (0.15, 0.064), (0.172, 0.03), (0.178, 0.0)]
    pts = curve([(y, w) for y, w in ctrl], 2)
    outline = [(w, y) for y, w, *_ in pts] + [(-w, y) for y, w, *_ in reversed(pts)]
    m("ViolinBody", "brown").prism(outline, (0, 0, 0.0), (1, 0, 0), (0, 1, 0), 0.036)
    m("ViolinTop", "orange").prism([(x * 0.93, y * 0.95) for x, y in outline], (0, 0, 0.019), (1, 0, 0), (0, 1, 0), 0.006)
    for s in (-1, 1):   # the f-holes
        m("ViolinHoles", "dark").ellipsoid((s * 0.036, -0.005, 0.022), (0.004, 0.035, 0.004), 8, 6, m=rot((0, 0, s * 8)))
        for dy in (-0.035, 0.028):
            m("ViolinHoles", "dark").ellipsoid((s * (0.036 + dy * 0.12), -0.005 + dy, 0.022), 0.006, 8, 5)
    m("ViolinNeck", "brown").box((0, 0.27, 0.0), (0.026, 0.2, 0.024), bevel=0.006)
    m("ViolinNeck", "brown").box((0, 0.395, -0.002), (0.026, 0.07, 0.02), bevel=0.005)
    m("ViolinNeck", "brown").lathe([(-0.012, 0.0), (-0.012, 0.016), (0.012, 0.016), (0.012, 0.0)], segs=14, at=(0, 0.44, -0.002), m=rot((0, 0, 90)))
    for k in range(4):   # pegs
        s = -1 if k % 2 else 1
        m("ViolinPegs", "dark").tube([(0, 0.37 + k * 0.016, 0), (s * 0.035, 0.37 + k * 0.016, 0)], 0.004, 6)
    m("ViolinBoard", "dark").box((0, 0.18, 0.026), (0.034, 0.28, 0.007), bevel=0.002)
    m("ViolinBoard", "dark").prism([(-0.02, -0.18), (0.02, -0.18), (0.012, -0.09), (-0.012, -0.09)], (0, 0, 0.026), (1, 0, 0), (0, 1, 0), 0.008)
    m("ViolinBoard", "dark").ellipsoid((0.045, -0.15, 0.026), (0.035, 0.03, 0.01), 10, 6)
    m("ViolinBridge", "white").prism([(-0.025, 0.0), (0.025, 0.0), (0.02, 0.018), (-0.02, 0.018)], (0, -0.03, 0.022), (1, 0, 0), (0, 0, 1), 0.004)
    for k in range(4):
        x = -0.012 + k * 0.008
        m("ViolinStrings", "white").tube([(x * 0.9, -0.09, 0.032), (x, -0.03, 0.041), (x * 0.7, 0.32, 0.031)], 0.0012, 4)
    c = f.body(-0.02, 1.1, 0.03)
    m.apply("Violin", rot((-10, 0, -32)), c)
    # the bow in the right hand, down along the leg
    h = f.hands[1]
    a, b = h + Vector((0.004, 0.02, 0.01)), h + Vector((0.06, -0.62, 0.11))
    m("Bow", "brown").tube([a, b], [0.006, 0.0045], 6)
    m("Bow", "dark").box(a + Vector((0, -0.03, 0.012)), (0.016, 0.03, 0.02), bevel=0.004)
    m("Bow", "white").tube([a + Vector((0.0, -0.04, 0.022)), b + Vector((0.0, 0.02, 0.02))], 0.0025, 4)
    f.top(f.hd + 0.2)


def clown(m):
    f = Figure(m, "yellow", brows="dark", mouth=False)
    hd = f.hd
    m("Nose", "red").ellipsoid(f.face(0, hd - 0.025, 0.02), 0.036, 14, 10)
    m("Mouth", "red").tube([f.face(-0.05, hd - 0.04, 0.002), f.face(-0.025, hd - 0.075, 0.003), f.face(0.025, hd - 0.075, 0.003), f.face(0.05, hd - 0.04, 0.002)], [0.007, 0.01, 0.01, 0.007], 8)
    for s in (-1, 1):
        m("Hair", "orange").ellipsoid((s * 0.118, hd + 0.04, -0.03), (0.07, 0.075, 0.085), 14, 10)
        m("Hair", "orange").ellipsoid((s * 0.09, hd + 0.1, -0.05), (0.06, 0.05, 0.065), 12, 8)
    lathe_on_head(f, m, "green", [(0.12, 0.0), (0.12, 0.085), (0.13, 0.085), (0.132, 0.06), (0.21, 0.055), (0.215, 0.0)], cz=-0.01)
    m("Ruff", "white").lathe([(f.sh - 0.03, 0.05), (f.sh - 0.02, 0.2, 0.18), (f.sh + 0.015, 0.235, 0.21), (f.sh + 0.05, 0.2, 0.18), (f.sh + 0.06, 0.05)], segs=48,
                             wave=lambda a: 0.11 * math.cos(16 * a), smooth=1)
    for i, c in enumerate(("red", "green", "blue")):
        m("Buttons", c).ellipsoid(f.body(0, f.hip + 0.34 - i * 0.13, 0.004), (0.026, 0.026, 0.012), 12, 6)
    f.top(f.hd + 0.24)


# ================================================================ the creatures

def robot(m):
    for s in (-1, 1):
        m("Legs", "metal").tube([(s * 0.09, 0.85, 0), (s * 0.09, 0.08, 0)], 0.055, 12)
        m("Feet", "metal").box((s * 0.09, 0.04, 0.03), (0.12, 0.08, 0.2), bevel=0.02)
        for y in (0.3, 0.55):
            m("Joints", "dark").tube([(s * 0.09, y, 0), (s * 0.09, y + 0.02, 0)], 0.062, 12, caps=False)
    m("Body", "metal").box((0, 1.12, 0), (0.46, 0.52, 0.3), bevel=0.04)
    m("Panel", "yellow").box((0, 1.18, 0.15), (0.22, 0.13, 0.02), bevel=0.008)
    for i, c in enumerate(("red", "green", "blue")):
        m("Lamps", c).ellipsoid((-0.06 + 0.06 * i, 1.02, 0.153), (0.018, 0.018, 0.01), 10, 6)
    for s in (-1, 1):
        m("Arms", "metal").box((s * 0.265, 1.1, 0.0), (0.07, 0.42, 0.08), bevel=0.02)
        m("Arms", "metal").tube([(s * 0.25, 1.32, 0), (s * 0.29, 1.32, 0)], 0.05, 12)
        m("Hands", "dark").box((s * 0.265, 0.85, 0.0), (0.08, 0.08, 0.1), bevel=0.02)
    m("Neck", "dark").tube([(0, 1.36, 0), (0, 1.46, 0)], 0.05, 12)
    m("Head", "metal").box((0, 1.58, 0), (0.32, 0.26, 0.26), bevel=0.035)
    for s in (-1, 1):
        m("Eyes", "blue").lathe([(0, 0.0), (0, 0.034), (0.02, 0.034), (0.02, 0.0)], segs=16, at=(s * 0.07, 1.61, 0.128), m=rot((90, 0, 0)))
        m("Ears", "dark").lathe([(0, 0.0), (0, 0.04), (0.03, 0.04), (0.03, 0.0)], segs=14, at=(s * 0.158, 1.58, 0), m=rot((0, 0, s * -90)))
    for k in range(4):
        m("Mouth", "dark").box((-0.06 + 0.04 * k, 1.5, 0.131), (0.022, 0.03, 0.012), bevel=0.004)
    m("Antenna", "metal").tube([(0, 1.7, 0), (0, 1.82, 0)], 0.012, 8)
    m("Antenna", "red").ellipsoid((0, 1.85, 0), 0.03, 12, 8)
    m.mark("Top", (0, 1.9, 0))


def fish(m):
    m("Stand", "metal").lathe([(0.0, 0.0), (0.0, 0.18), (0.03, 0.17), (0.04, 0.05), (1.23, 0.03), (1.28, 0.1), (1.3, 0.0)], segs=24)
    m("Bowl", "clear").ellipsoid((0, 1.55, 0), 0.31, 28, 16)
    m("Rim", "metal").loop([(math.sin(2 * math.pi * k / 28) * 0.17, 1.81, math.cos(2 * math.pi * k / 28) * 0.17) for k in range(28)], 0.012, 8)
    m("Water", "water").lathe([(1.27, 0.0), (1.27, 0.12), (1.33, 0.2), (1.45, 0.285), (1.55, 0.3), (1.65, 0.285), (1.7, 0.255), (1.7, 0.0)], segs=28)
    m("Pebbles", "grey").lathe([(1.255, 0.0), (1.26, 0.12), (1.285, 0.14), (1.3, 0.0)], segs=20, wave=lambda a: 0.06 * math.sin(5 * a))
    # a big round goldfish filling the ball, sideways and turned a little to the window, so its face and side both show
    R = rot((0, -35, 0))
    c = Vector((0, 1.53, 0.02))
    L, T = 0.27, 0.19
    prof = [(0.0, 0.2, 0.3), (0.12, 0.45, 0.55), (0.35, 0.88, 0.9), (0.55, 1.0, 1.0), (0.75, 0.9, 0.95), (0.9, 0.62, 0.7), (1.0, 0.0, 0.0)]
    m("Fish", "orange").lathe([(-L / 2 + s * L, T / 2 * h, T / 2 * 0.62 * t) for s, h, t in prof], segs=22, m=R @ rot((0, 0, -90)), at=c, smooth=2)
    ax, up = R @ Vector((1, 0, 0)), R @ Vector((0, 1, 0))
    m("Fins", "orange").prism([(-0.12, 0.0), (-0.2, 0.1), (-0.25, 0.08), (-0.215, 0.0), (-0.25, -0.08), (-0.2, -0.1)], c, ax, up, 0.008)
    m("Fins", "orange").prism([(-0.07, 0.07), (-0.03, 0.135), (0.05, 0.11), (0.06, 0.08)], c, ax, up, 0.008)
    m("Fins", "orange").prism([(-0.05, -0.07), (-0.09, -0.12), (-0.02, -0.085)], c, ax, up, 0.008)
    side = R @ Vector((0, 0, 1))
    for s in (-1, 1):
        m("Fins", "orange").prism([(0.0, 0.0), (-0.055, -0.03), (-0.045, 0.0)], c + ax * 0.05 + up * -0.03 + side * s * 0.05, ax, up * 0.8 + side * s * 0.6, 0.006)
        e = c + ax * 0.075 + up * 0.03 + side * s * 0.045
        m("Eyes", "white").ellipsoid(e, (0.026, 0.026, 0.016), 12, 8, m=R)
        m("Pupils", "dark").ellipsoid(e + side * s * 0.013 + ax * 0.004, (0.013, 0.014, 0.007), 10, 6, m=R)
    m("Lips", "red").ellipsoid(c + ax * 0.137 - up * 0.012, (0.012, 0.016, 0.022), 10, 6, m=R)
    for k in range(3):   # bubbles going up from its mouth
        m("Bubbles", "clear").ellipsoid(c + ax * 0.16 + up * (0.05 + k * 0.05) + side * 0.01 * k, 0.01 + 0.004 * k, 10, 6)
    m.mark("Top", (0, 1.86, 0))


def duck(m):
    # a runner duck: standing upright, a bottle-shaped body leaning forward, a long neck, a flat wide bill, wings folded
    # along its sides, a pointed tail; orange legs and webbed feet
    for s in (-1, 1):
        m("Legs", "orange").tube([(s * 0.085, 0.42, -0.06), (s * 0.085, 0.22, -0.02), (s * 0.085, 0.04, 0.02)], [0.04, 0.032, 0.028], 10)
        m("Feet", "orange").prism([(-0.02, -0.03), (-0.08, 0.13), (-0.03, 0.1), (0.0, 0.15), (0.03, 0.1), (0.08, 0.13), (0.02, -0.03)], (s * 0.085, 0.012, 0.03), (1, 0, 0), (0, 0, 1), 0.018)
    B = rot((22, 0, 0))   # the body leans forward
    o = Vector((0, 0.3, -0.06))
    body = [(0.0, 0.0), (0.04, 0.09), (0.14, 0.18), (0.32, 0.225), (0.5, 0.215), (0.66, 0.18), (0.8, 0.13), (0.9, 0.1), (0.97, 0.0)]
    m("Body", "yellow").lathe([(y, r, r * 0.93) for y, r in body], segs=28, m=B, at=o, smooth=2)
    m("Body", "yellow").lathe([(0.0, 0.05), (0.13, 0.0)], segs=12, at=o + B @ Vector((0, 0.04, -0.12)), m=B @ rot((-125, 0, 0)))   # the tail
    m("Body", "yellow").tube([o + B @ Vector((0, 0.86, 0.0)), Vector((0, 1.38, 0.17)), Vector((0, 1.5, 0.15))], [0.09, 0.072, 0.07], 14)   # the neck
    m("Head", "yellow").ellipsoid((0, 1.6, 0.16), (0.1, 0.1, 0.125), 20, 12)
    m("Head", "yellow").ellipsoid((0, 1.56, 0.17), (0.085, 0.07, 0.1), 16, 10)
    # the bill: a flat spatula, upper and lower, a little down from the face
    out = [(-0.042, 0.0), (0.042, 0.0), (0.05, 0.05), (0.054, 0.1), (0.044, 0.135), (0.0, 0.148), (-0.044, 0.135), (-0.054, 0.1), (-0.05, 0.05)]
    D = rot((12, 0, 0))
    m("Bill", "orange").prism(out, Vector((0, 1.575, 0.25)), D @ Vector((1, 0, 0)), D @ Vector((0, 0, 1)), 0.024)
    m("Bill", "orange").prism([(x * 0.9, y * 0.85) for x, y in out], Vector((0, 1.552, 0.25)) + D @ Vector((0, 0, 0.005)), D @ Vector((1, 0, 0)), D @ Vector((0, 0, 1)), 0.012)
    for s in (-1, 1):
        m("Nostrils", "dark").ellipsoid(Vector((s * 0.016, 1.582, 0.29)) + D @ Vector((0, 0.012, 0)), (0.005, 0.003, 0.009), 8, 5, m=D)
        m("Eyes", "white").ellipsoid((s * 0.074, 1.635, 0.215), (0.026, 0.03, 0.018), 12, 6, m=rot((0, s * 30, 0)))
        m("Eyes", "dark").ellipsoid((s * 0.084, 1.637, 0.226), (0.013, 0.017, 0.009), 10, 6, m=rot((0, s * 30, 0)))
        # a folded wing lying on the side, its feathers' tips to the back and down, a blue patch on it
        w = o + B @ Vector((s * 0.19, 0.42, -0.02))
        W = B @ rot((-12, s * 6, s * -4))
        m("Wings", "yellow").ellipsoid(w, (0.045, 0.26, 0.13), 18, 12, m=W)
        m("Wings", "yellow").prism([(0.0, 0.1), (-0.05, -0.18), (0.0, -0.32), (0.05, -0.18)], w + W @ Vector((0.0, 0, -0.05)), W @ Vector((0, 0, 1)), W @ Vector((0, 1, 0)), 0.03)
        m("Patch", "blue").ellipsoid(w + W @ Vector((s * 0.03, -0.06, -0.02)), (0.022, 0.07, 0.05), 12, 6, m=W)
    m.mark("Top", (0, 1.8, 0))


def beaver(m):
    # a stout beaver, its belly out and its head forward, a log held to its chest in both paws; the flat tail on the floor
    prof = [(0.06, 0.0, 0.0, 0, 0.0), (0.08, 0.17, 0.15, 0, 0.0), (0.25, 0.3, 0.27, 0, 0.03), (0.5, 0.335, 0.31, 0, 0.05),
            (0.78, 0.31, 0.28, 0, 0.03), (1.0, 0.26, 0.23, 0, 0.01), (1.16, 0.2, 0.18, 0, 0.03), (1.28, 0.14, 0.14, 0, 0.06), (1.34, 0.0, 0.0, 0, 0.07)]
    m("Body", "brown").lathe(prof, segs=28, smooth=3)
    m("Belly", "orange").ellipsoid((0, 0.62, 0.24), (0.22, 0.33, 0.11), 20, 12)
    m("Tail", "dark").lathe([(0, 0.0), (0, 0.15, 0.25), (0.045, 0.15, 0.25), (0.045, 0.0)], segs=24, at=(0, 0.04, -0.46), m=rot((-10, 0, 0)))
    for k in range(-3, 4):   # the tail's scales: a cross-hatch of grooves
        m("TailLines", "brown").box((k * 0.035, 0.087, -0.47), (0.004, 0.006, 0.4), m=rot((-10, 0, 0)))
        m("TailLines", "brown").box((0, 0.087 + k * 0.006, -0.47 + k * 0.035), (0.24, 0.006, 0.004), m=rot((-10, 0, 0)))
    for s in (-1, 1):
        m("Feet", "dark").ellipsoid((s * 0.15, 0.04, 0.13), (0.085, 0.045, 0.14), 12, 8)
        for k in range(-1, 2):
            m("Claws", "white").ellipsoid((s * 0.15 + k * 0.03, 0.03, 0.27), (0.01, 0.01, 0.018), 8, 5)
    # the log, and the arms from the shoulders to the paws on it
    log = (Vector((-0.16, 1.02, 0.31)), Vector((0.16, 1.05, 0.31)))
    m("Log", "brown").tube([log[0], log[1]], 0.05, 14, caps=False)
    for p in log:
        m("LogEnds", "orange").lathe([(-0.004, 0.0), (-0.004, 0.049), (0.004, 0.049), (0.004, 0.0)], segs=14, at=p, m=rot((0, 0, 90)))
    m("Log", "brown").tube([(0.05, 1.08, 0.31), (0.08, 1.13, 0.3)], [0.012, 0.004], 6)   # a twig
    m("Log", "green").ellipsoid((0.088, 1.14, 0.3), (0.025, 0.012, 0.018), 8, 5, m=rot((0, 0, -35)))
    for s in (-1, 1):
        m("Arms", "brown").tube([(s * 0.17, 1.17, 0.06), (s * 0.22, 1.07, 0.17), (s * 0.12, 1.05, 0.31)], [0.058, 0.05, 0.044], 10)
        m("Paws", "dark").ellipsoid((s * 0.1, 1.06, 0.33), (0.04, 0.055, 0.035), 10, 6)
    # the head, forward on the shoulders
    m("Head", "brown").ellipsoid((0, 1.45, 0.09), (0.17, 0.155, 0.17), 22, 12)
    for s in (-1, 1):
        m("Cheeks", "orange").ellipsoid((s * 0.045, 1.405, 0.215), (0.06, 0.05, 0.05), 12, 8)
        m("Ears", "brown").ellipsoid((s * 0.125, 1.59, 0.03), (0.04, 0.04, 0.022), 10, 6, m=rot((0, s * 30, 0)))
        m("Ears", "dark").ellipsoid((s * 0.127, 1.59, 0.042), (0.024, 0.024, 0.01), 8, 5, m=rot((0, s * 30, 0)))
        m("Eyes", "dark").ellipsoid((s * 0.07, 1.51, 0.22), (0.02, 0.024, 0.012), 10, 6)
        for k in range(2):
            m("Whiskers", "white").tube([(s * 0.08, 1.41 - k * 0.012, 0.24), (s * 0.15, 1.42 - k * 0.025, 0.23)], 0.0015, 4)
    m("Nose", "dark").ellipsoid((0, 1.44, 0.268), (0.03, 0.02, 0.018), 10, 6)
    for s in (-1, 1):
        m("Teeth", "white").box((s * 0.014, 1.345, 0.245), (0.026, 0.05, 0.014), bevel=0.005)
    m.mark("Top", (0, 1.72, 0))


def gnome(m):
    # a small gnome, so he stands on a crate to see over the counter
    m("Crate", "brown").box((0, 0.3, 0), (0.5, 0.6, 0.45), bevel=0.01)
    for y in (0.1, 0.3, 0.5):
        m("Planks", "brown").box((0, y, 0.228), (0.5, 0.012, 0.008))
    for s in (-1, 1):
        m("Planks", "brown").box((s * 0.235, 0.3, 0.228), (0.03, 0.6, 0.012))
        m("Boots", "dark").ellipsoid((s * 0.08, 0.64, 0.05), (0.055, 0.045, 0.09), 12, 8)
        m("Legs", "dark").tube([(s * 0.08, 0.66, 0), (s * 0.08, 0.76, 0)], 0.05, 10)
    m("Coat", "blue").lathe([(0.72, 0.0), (0.72, 0.21, 0.17), (0.9, 0.2, 0.17), (1.1, 0.19, 0.16), (1.27, 0.17, 0.14), (1.33, 0.1, 0.08), (1.35, 0.0)], segs=28, n=2.3, smooth=2)
    m("Belt", "dark").sheet(lambda y: (interp([(0.9, 0.2), (1.1, 0.19)], y, 1), interp([(0.9, 0.17), (1.1, 0.16)], y, 1), 0, 0), [0.92, 0.96, 1.0], 0, 2 * math.pi, 0.008, -0.01, segs=28)
    m("Buckle", "yellow").box((0, 0.96, 0.17), (0.07, 0.07, 0.015), bevel=0.005)
    for s in (-1, 1):
        m("Sleeves", "blue").tube([(s * 0.16, 1.27, 0), (s * 0.22, 1.12, 0.0), (s * 0.21, 0.98, 0.05)], [0.05, 0.045, 0.04], 10)
        m("Hands", "skin").ellipsoid((s * 0.21, 0.93, 0.06), (0.03, 0.05, 0.04), 10, 6)
    m("Skin", "skin").tube([(0, 1.3, 0), (0, 1.42, 0)], 0.045, 10)
    m("Skin", "skin").lathe([(1.5 + dy, r) for dy, r in HEAD], segs=24)
    m("Skin", "skin").ellipsoid((0, 1.48, 0.125), (0.036, 0.034, 0.035), 12, 8)
    for s in (-1, 1):
        m("Eyes", "dark").ellipsoid((s * 0.042, 1.525, 0.104), (0.014, 0.019, 0.009), 10, 6)
        m("Skin", "skin").ellipsoid((s * 0.112, 1.495, -0.005), (0.02, 0.033, 0.027), 10, 6)
        m("Brows", "white").tube([(s * 0.02, 1.553, 0.108), (s * 0.044, 1.562, 0.104), (s * 0.066, 1.555, 0.095)], [0.006, 0.008, 0.005], 6)

    def phimax(a):
        c = math.cos(a)
        return lerp(1.75, 1.22, c) if c >= 0 else lerp(1.75, 0.5, -c)
    m("Beard", "white").cap((0, 1.42, 0.03), (0.13, 0.26, 0.135), phimax, 0.03, down=True)
    m("Beard", "white").tube([(-0.06, 1.445, 0.112), (0, 1.455, 0.125), (0.06, 1.445, 0.112)], [0.012, 0.016, 0.012], 8)
    # a tall pointed red hat, its tip bent back
    m("Hat", "red").lathe([(1.56, 0.0), (1.56, 0.135), (1.6, 0.13), (1.7, 0.1, 0.1, 0, -0.01), (1.8, 0.07, 0.07, 0, -0.03),
                           (1.9, 0.04, 0.04, 0, -0.07), (1.97, 0.018, 0.018, 0, -0.12), (2.0, 0.0, 0.0, 0, -0.16)], segs=24, smooth=2)
    m("Hat", "red").lathe([(1.555, 0.0), (1.555, 0.14), (1.6, 0.14), (1.6, 0.0)], segs=24)
    m.mark("Top", (0, 2.0, 0))


BUILD = {name: globals()[name] for name in ALL}

clear()
roots = []
for i, who in enumerate(ALL):
    if ONLY and who not in ONLY:
        continue
    model = Model("Persona_" + who)
    BUILD[who](model)
    objs = model.export(OUT)
    if PREVIEW:
        k = len(roots)
        roots.append(stash(objs, who, ((k % 9) * 0.85, 0, -(k // 9) * 4.0)))
    else:
        clear()
if PREVIEW and roots:
    rows = [roots[i:i + 9] for i in range(0, len(roots), 9)]
    # one picture a row, front and three-quarter; the other rows are hidden meanwhile
    for j, row in enumerate(rows):
        for r in roots:
            hide = r not in row
            for o in [r] + list(r.children):
                o.hide_render = hide
        for yaw, tag in ((0, "front"), (35, "side")):
            preview(row, os.path.join(PREVIEW, f"customers_{j}_{tag}.png"), yaw, size=(2600, 900), ortho=max(len(row) * 0.85 + 0.4, 3.2), look=1.1)
print("done ->", OUT)
