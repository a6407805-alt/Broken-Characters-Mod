"""
The Surveyor's 3D model, built with meshkit (no Blender needed). Writes surveyor_model.bin (the "DUEL" format that
SvrModel.cs loads onto the Commando skeleton, one rigid piece per bone).

An original heavy-armour space suit: bulky olive plates over a white pressure suit, a rounded helmet with a flip-up
visor, a life-support backpack with a spinning LiDAR dome, and a rotary minigun with spinning barrels.
Part names are "<bone>" or "<bone>@<tag>". A tag makes it a pivot part: it gets its own transform at the pivot so the
game can animate it (visor = hinge, barrels = spin, lidar = spin).

Units: metres, +Y up, +Z = forward, +X = the character's right. Run: python3 build_surveyor.py <out.bin> [preview.png]
"""
import sys, os, math, struct
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from meshkit import *

# ------------------------------------------------------------------ helpers

def rot_from_to(u, v):
    u = np.array(u, float); v = np.array(v, float)
    u /= np.linalg.norm(u); v /= np.linalg.norm(v)
    c = float(np.dot(u, v))
    if c > 0.99999: return np.eye(3)
    if c < -0.99999:
        a = np.cross(u, [1, 0, 0]);
        if np.linalg.norm(a) < 1e-3: a = np.cross(u, [0, 1, 0])
        a /= np.linalg.norm(a)
        return 2 * np.outer(a, a) - np.eye(3)
    ax = np.cross(u, v); s = np.linalg.norm(ax)
    K = np.array([[0, -ax[2], ax[1]], [ax[2], 0, -ax[0]], [-ax[1], ax[0], 0]])
    return np.eye(3) + K + K @ K * ((1 - c) / (s * s))

def place(mesh, origin, direction, frm=(0, 0, 1)):
    """Rotate so frm points along direction, then move to origin."""
    P, N = mesh
    R = rot_from_to(frm, direction)
    return P @ R.T + np.array(origin, float), N @ R.T

def limb(a, b, r0, r1, seg=14):
    a = np.array(a, float); b = np.array(b, float)
    h = np.linalg.norm(b - a)
    return place(cylinder(r0, r1, h, seg), a, b - a, (0, 1, 0))

def ring(c, d, r, h, seg=18, r2=None):
    return place(cylinder(r, r if r2 is None else r2, h, seg), c, d, (0, 1, 0))

def rbox(sx, sy, sz, e=0.22, move=(0, 0, 0), rot=(0, 0, 0), seg=14, rings=10):
    return transform(superellipsoid((sx / 2, sy / 2, sz / 2), e, e, seg, rings), rot=rot, move=move)

def ell(r, move, e1=1.0, e2=1.0, rot=(0, 0, 0), seg=20, rings=14):
    return transform(superellipsoid(r, e1, e2, seg, rings), rot=rot, move=move)

def cut(mesh, keep):
    """Keep triangles whose centroid satisfies keep(c)."""
    P, N = mesh
    T = P.reshape(-1, 3, 3).mean(1)
    k = np.array([keep(c) for c in T])
    k3 = np.repeat(k, 3)
    return P[k3], N[k3]

def mirror(m): return flip(m, 0)

def loop(c, a, b, zfun, n=48, ex=4.0, off=0.0):
    """Closed loop of points around a rounded-rectangle window (half sizes a, b) following a surface z = zfun(x, y)."""
    pts = []
    for i in range(n + 1):
        t = 2 * math.pi * i / n
        x = a * np.sign(math.cos(t)) * abs(math.cos(t)) ** (2 / ex)
        y = b * np.sign(math.sin(t)) * abs(math.sin(t)) ** (2 / ex)
        pts.append((c[0] + x, c[1] + y, c[2] + zfun(x, y) + off))
    return pts

# ------------------------------------------------------------------ skeleton (right side; the left is mirrored)

BONES = {
    "root": ((0, 0, 0), (0, 0, -0.2)),
    "hips": ((0, 0.95, 0), (0, 1.05, 0)),
    "spine": ((0, 1.05, 0), (0, 1.28, 0)),
    "chest": ((0, 1.28, 0), (0, 1.48, 0)),
    "neck": ((0, 1.48, 0), (0, 1.60, 0)),
    "head": ((0, 1.60, 0), (0, 1.85, 0)),
    "upper_arm.R": ((0.25, 1.42, 0), (0.33, 1.17, 0.12)),
    "forearm.R": ((0.33, 1.17, 0.12), (0.26, 1.24, 0.37)),
    "hand.R": ((0.26, 1.24, 0.37), (0.221, 1.279, 0.509)),
    "thigh.R": ((0.095, 0.98, 0), (0.10, 0.55, 0.01)),
    "shin.R": ((0.10, 0.55, 0.01), (0.103, 0.10, 0)),
    "foot.R": ((0.103, 0.10, 0), (0.103, 0.03, 0.18)),
}
for k in list(BONES):
    if k.endswith(".R"):
        h, t = BONES[k]
        BONES[k[:-2] + ".L"] = ((-h[0], h[1], h[2]), (-t[0], t[1], t[2]))
SH = np.array(BONES["upper_arm.R"][0]); EL = np.array(BONES["forearm.R"][0]); WR = np.array(BONES["hand.R"][0]); HD = np.array(BONES["hand.R"][1])
HIP = np.array(BONES["thigh.R"][0]); KN = np.array(BONES["shin.R"][0]); AN = np.array(BONES["foot.R"][0])
GUN_DIR = (HD - WR) / np.linalg.norm(HD - WR)
HC = np.array([0.0, 1.76, 0.0])           # helmet centre = visor hinge

# ------------------------------------------------------------------ build

def build(visor_deg=0.0, spin_deg=0.0, barrel_deg=0.0):
    m = Model()
    ARMOR = m.color((0.30, 0.36, 0.25)); ARMOR_DK = m.color((0.19, 0.22, 0.17)); SUIT = m.color((0.78, 0.79, 0.76))
    SUIT_DK = m.color((0.50, 0.52, 0.50)); RUBBER = m.color((0.08, 0.09, 0.09)); METAL = m.color((0.52, 0.55, 0.58))
    ORANGE = m.color((0.95, 0.50, 0.08)); GLASS = m.color((0.03, 0.08, 0.07)); GREEN = m.color((0.35, 1.0, 0.25), 2.6)
    AMBER = m.color((1.0, 0.72, 0.15), 2.2); RED = m.color((1.0, 0.15, 0.1), 2.2)
    SKIN = m.color((0.80, 0.63, 0.53)); EYE = m.color((0.88, 0.94, 0.98), 0.9); DARKSKIN = m.color((0.35, 0.18, 0.16))
    GUNM = m.color((0.24, 0.26, 0.28))

    # ---------------- hips: pelvis block, belt, pouches, skirt plates
    m.add("hips", rbox(0.34, 0.20, 0.24, 0.3, (0, 0.98, 0)), SUIT)
    m.add("hips", rbox(0.38, 0.07, 0.28, 0.2, (0, 1.07, 0)), ARMOR_DK)
    m.add("hips", rbox(0.10, 0.08, 0.04, 0.2, (0, 1.07, 0.15)), ORANGE)
    for sx in (-1, 1):
        m.add("hips", rbox(0.09, 0.12, 0.10, 0.3, (0.17 * sx, 0.99, 0.10)), ARMOR)
        m.add("hips", rbox(0.06, 0.16, 0.20, 0.3, (0.20 * sx, 0.95, -0.02)), ARMOR)
        m.add("hips", rbox(0.10, 0.10, 0.08, 0.3, (0.12 * sx, 1.0, -0.15)), ARMOR_DK)
    m.add("hips", transform(prism([(-0.09, 0.0), (0.09, 0.0), (0.06, -0.20), (-0.06, -0.20)], 0.03, 0.01), move=(0, 0.97, 0.15)), ARMOR)   # codpiece plate

    # ---------------- spine: corrugated suit hose and a belly module
    m.add("spine", rbox(0.28, 0.24, 0.20, 0.4, (0, 1.17, 0)), SUIT)
    for i in range(5):
        m.add("spine", transform(torus(0.145, 0.02, 24, 8), move=(0, 1.07 + 0.045 * i, 0)), SUIT_DK)
    m.add("spine", rbox(0.16, 0.12, 0.05, 0.3, (0, 1.16, 0.12)), ARMOR_DK)
    for i in range(3):
        m.add("spine", rbox(0.025, 0.025, 0.012, 0.3, (-0.04 + 0.04 * i, 1.16, 0.15)), GREEN)

    # ---------------- chest: plates, yoke, collar, status lights
    m.add("chest", ell((0.31, 0.21, 0.21), (0, 1.40, 0.03), 0.6, 0.6), ARMOR)
    m.add("chest", ell((0.21, 0.15, 0.06), (0, 1.40, 0.215), 0.4, 0.4), ARMOR_DK)
    m.add("chest", rbox(0.30, 0.025, 0.04, 0.3, (0, 1.30, 0.22)), ORANGE)
    for sx in (-1, 1):
        m.add("chest", rbox(0.05, 0.12, 0.06, 0.3, (0.12 * sx, 1.40, 0.245)), METAL)
        m.add("chest", rbox(0.03, 0.03, 0.02, 0.3, (0.12 * sx, 1.47, 0.28)), GREEN if sx > 0 else AMBER)
    m.add("chest", rbox(0.60, 0.10, 0.30, 0.45, (0, 1.53, 0)), ARMOR_DK)                    # shoulder yoke
    for sx in (-1, 1):
        m.add("chest", limb((0.16 * sx, 1.56, 0.05), (0.12 * sx, 1.64, 0.02), 0.04, 0.035, 10), RUBBER)
    # backpack: life support box, twin tanks, hoses
    m.add("chest", rbox(0.36, 0.42, 0.20, 0.25, (0, 1.36, -0.25)), SUIT_DK)
    m.add("chest", rbox(0.30, 0.34, 0.05, 0.25, (0, 1.36, -0.37)), ARMOR_DK)
    for sx in (-1, 1):
        m.add("chest", ring((0.11 * sx, 1.13, -0.30), (0, 1, 0), 0.075, 0.46, 18), SUIT)
        m.add("chest", ell((0.075, 0.05, 0.075), (0.11 * sx, 1.60, -0.30)), SUIT)
        m.add("chest", ring((0.11 * sx, 1.28, -0.30), (0, 1, 0), 0.08, 0.04, 18), ORANGE)
        m.add("chest", ring((0.11 * sx, 1.60, -0.30), (0, 1, 0), 0.025, 0.07, 10), METAL)
        hose = [(0.11 * sx, 1.66, -0.30), (0.17 * sx, 1.74, -0.26), (0.22 * sx, 1.70, -0.12), (0.20 * sx, 1.62, 0.0)]
        m.add("chest", tube(hose, 0.02, 8), RUBBER)
    m.add("chest", rbox(0.20, 0.10, 0.06, 0.3, (0, 1.12, -0.36)), ARMOR_DK)
    for i in range(4):
        m.add("chest", rbox(0.03, 0.03, 0.02, 0.3, (-0.045 + 0.03 * i, 1.12, -0.395)), GREEN if i % 2 == 0 else RED)

    # ---------------- neck seal
    m.add("neck", ring((0, 1.46, 0), (0, 1, 0), 0.115, 0.12, 22), SUIT_DK)
    m.add("neck", transform(torus(0.118, 0.03, 26, 8), move=(0, 1.52, 0)), METAL)
    m.add("neck", transform(torus(0.112, 0.022, 26, 8), move=(0, 1.58, 0)), SUIT)

    # ---------------- head: helmet with a window, face inside, rim, pods
    a, b, c = 0.215, 0.235, 0.235
    dome = superellipsoid((a, b, c), 1, 1, 30, 20)
    dome_w = transform(dome, move=tuple(HC))
    def in_window(p, ax=0.165, ay_lo=-0.115, ay_hi=0.105):
        d = p - HC
        return d[2] > 0.07 and abs(d[0]) < ax and ay_lo < d[1] < ay_hi
    m.add("head", cut(dome_w, lambda p: not in_window(p)), ARMOR)
    inner = transform(superellipsoid((a * 0.94, b * 0.94, c * 0.94), 1, 1, 24, 16), move=tuple(HC))
    m.add("head", cut(inner, lambda p: not in_window(p, 0.175, -0.125, 0.115)), RUBBER)     # dark inside of the helmet, open at the window so the face shows
    zf = lambda x, y: c * math.sqrt(max(0.0, 1 - (x / a) ** 2 - (y / b) ** 2))
    wc = (0, HC[1] - 0.005, HC[2])
    m.add("head", tube(loop(wc, 0.165, 0.11, zf, 56, 4.0, 0.006), 0.013, 8), METAL)
    # brow guard, crown rail, chin guard, rear vents
    m.add("head", ell((0.19, 0.03, 0.085), (0, HC[1] + 0.14, 0.14), 0.6, 0.6, rot=(-14, 0, 0)), ARMOR_DK)
    m.add("head", rbox(0.06, 0.04, 0.26, 0.3, (0, HC[1] + 0.235, -0.02)), ARMOR_DK)
    m.add("head", rbox(0.03, 0.02, 0.05, 0.3, (0, HC[1] + 0.26, 0.08)), AMBER)
    m.add("head", ell((0.13, 0.045, 0.07), (0, HC[1] - 0.15, 0.17), 0.5, 0.5, rot=(25, 0, 0)), ARMOR_DK)
    for i in range(4):
        m.add("head", rbox(0.16, 0.015, 0.03, 0.3, (0, HC[1] - 0.05 + 0.04 * i, -0.225)), RUBBER)
    for sx in (-1, 1):
        m.add("head", ring((0.205 * sx, HC[1] - 0.02, 0.0), (sx, 0, 0), 0.07, 0.05, 18), METAL)           # comm pods
        m.add("head", ring((0.245 * sx, HC[1] - 0.02, 0.0), (sx, 0, 0), 0.045, 0.02, 14), ARMOR_DK)
        m.add("head", ell((0.012, 0.012, 0.012), (0.268 * sx, HC[1] - 0.02, 0.0)), GREEN if sx > 0 else AMBER)
        m.add("head", ring((0.215 * sx, HC[1] + 0.05, -0.06), (0, 1, 0), 0.012, 0.16, 8), METAL) if sx > 0 else None   # antenna
    # the face under the visor: pale skin, milky blind eyes
    m.add("head", ell((0.125, 0.15, 0.13), (0, HC[1] - 0.015, 0.045)), SKIN)
    m.add("head", ell((0.13, 0.06, 0.13), (0, HC[1] + 0.085, 0.05)), ARMOR_DK)              # padded cap over the brow
    for sx in (-1, 1):
        m.add("head", ell((0.026, 0.02, 0.012), (0.052 * sx, HC[1] + 0.02, 0.168), 1, 1, seg=12, rings=8), EYE)
        m.add("head", rbox(0.06, 0.012, 0.02, 0.3, (0.05 * sx, HC[1] + 0.055, 0.17), rot=(0, 0, -10 * sx)), DARKSKIN)
    m.add("head", place(cylinder(0.022, 0.006, 0.05, 8), (0, HC[1] - 0.01, 0.168), (0, -0.2, 1), (0, 1, 0)), SKIN)   # nose
    m.add("head", rbox(0.07, 0.012, 0.02, 0.3, (0, HC[1] - 0.085, 0.165)), DARKSKIN)                                # mouth

    # ---------------- visor (pivot part: hinge at the helmet centre, axis X)
    vs = tuple(HC)
    shell = transform(superellipsoid((a + 0.028, b + 0.028, c + 0.028), 1, 1, 30, 20), move=tuple(HC))
    keep = lambda p: in_window(p, 0.185, -0.13, 0.12)
    glass = cut(shell, keep)
    vis = []
    vis.append((glass, GLASS))
    zf2 = lambda x, y: (c + 0.03) * math.sqrt(max(0.0, 1 - (x / (a + 0.028)) ** 2 - (y / (b + 0.028)) ** 2))
    vis.append((tube(loop((0, HC[1] - 0.005, HC[2]), 0.185, 0.125, zf2, 56, 4.0, 0.0), 0.014, 8), METAL))
    # the scan line and HUD ticks glowing on the glass
    sl = []
    for i in range(25):
        x = -0.16 + 0.32 * i / 24
        y = 0.015
        z = (c + 0.034) * math.sqrt(max(0.0, 1 - (x / (a + 0.028)) ** 2 - (y / (b + 0.028)) ** 2))
        sl.append((x, HC[1] + y, z))
    vis.append((tube(sl, 0.006, 6), GREEN))
    for i in range(5):
        x = -0.12 + 0.06 * i
        y = -0.07
        z = (c + 0.032) * math.sqrt(max(0.0, 1 - (x / (a + 0.028)) ** 2 - (y / (b + 0.028)) ** 2))
        vis.append((transform(superellipsoid((0.012, 0.004, 0.004), 0.5, 0.5, 8, 6), move=(x, HC[1] + y, z)), GREEN))
    for sx in (-1, 1):
        vis.append((ring((0.232 * sx, HC[1], 0.0), (sx, 0, 0), 0.035, 0.02, 14), METAL))              # hinge knuckles
        vis.append((ring((0.205 * sx, HC[1], 0.0), (sx, 0, 0), 0.02, 0.05, 10), ARMOR_DK))
        vis.append((tube([(0.215 * sx, HC[1], 0.0), (0.205 * sx, HC[1] - 0.04, 0.12), (0.18 * sx, HC[1] - 0.1, 0.2)], 0.012, 6), METAL))   # arms to the glass
    for mesh, col in vis:
        m.add("head@visor", transform(mesh, rot=(0, 0, 0)) if visor_deg == 0 else _rot_about(mesh, vs, (1, 0, 0), visor_deg), col)
    m.pivots["head@visor"] = (vs, (vs[0] + 1, vs[1], vs[2]))

    # ---------------- spinning LiDAR dome on the backpack (pivot part, axis Y)
    lp = (0.0, 1.62, -0.31)
    lid = []
    lid.append((ring((0, 1.62, -0.31), (0, 1, 0), 0.105, 0.035, 20), ARMOR_DK))
    lid.append((ring((0, 1.655, -0.31), (0, 1, 0), 0.075, 0.07, 20), METAL))
    lid.append((ell((0.075, 0.05, 0.075), (0, 1.725, -0.31)), ARMOR_DK))
    lid.append((rbox(0.09, 0.035, 0.03, 0.3, (0, 1.69, -0.245)), GREEN))
    lid.append((rbox(0.03, 0.03, 0.03, 0.3, (0, 1.745, -0.245)), RED))
    lid.append((limb((0.0, 1.76, -0.31), (0.0, 1.88, -0.31), 0.008, 0.004, 6), METAL))
    for mesh, col in lid:
        m.add("chest@lidar", mesh if spin_deg == 0 else _rot_about(mesh, lp, (0, 1, 0), spin_deg), col)
    m.pivots["chest@lidar"] = (lp, (lp[0], lp[1] + 1, lp[2]))

    # ---------------- arms (right, then mirrored for the left)
    def arm(sx):
        sgn = 1 if sx > 0 else -1
        sh = np.array([SH[0] * sgn, SH[1], SH[2]]); el = np.array([EL[0] * sgn, EL[1], EL[2]]); wr = np.array([WR[0] * sgn, WR[1], WR[2]])
        suf = ".R" if sgn > 0 else ".L"
        m.add("upper_arm" + suf, limb(sh, el, 0.085, 0.072, 14), SUIT)
        m.add("upper_arm" + suf, ell((0.16, 0.115, 0.16), (sh[0] + 0.07 * sgn, sh[1] + 0.045, sh[2]), 0.55, 0.55, rot=(0, 0, -22 * sgn)), ARMOR)     # pauldron
        m.add("upper_arm" + suf, ring((sh[0] + 0.07 * sgn, sh[1] + 0.045, sh[2]), (0, 1, 0), 0.05, 0.14, 4) if False else ell((0.06, 0.02, 0.06), (sh[0] + 0.10 * sgn, sh[1] + 0.145, sh[2]), 1, 1, seg=14, rings=8), ORANGE)
        m.add("upper_arm" + suf, rbox(0.15, 0.04, 0.17, 0.3, (sh[0] + 0.08 * sgn, sh[1] - 0.045, sh[2]), rot=(0, 0, -25 * sgn)), ARMOR_DK)
        m.add("upper_arm" + suf, ell((0.09, 0.07, 0.09), tuple(el + np.array([0.0, 0.01, -0.015])), 0.6, 0.6), ARMOR_DK)   # elbow
        m.add("forearm" + suf, limb(el, wr, 0.07, 0.058, 14), SUIT)
        mid = (el + wr) / 2
        m.add("forearm" + suf, place(cylinder(0.082, 0.07, 0.17, 16), el + (wr - el) * 0.28, wr - el, (0, 1, 0)), ARMOR)
        m.add("forearm" + suf, place(cylinder(0.075, 0.072, 0.06, 16), wr - (wr - el) * 0.28, wr - el, (0, 1, 0)), ARMOR_DK)
        if sgn < 0:   # wrist computer on the left forearm
            top = mid + np.array([0.0, 0.07, 0.0])
            m.add("forearm" + suf, rbox(0.10, 0.03, 0.15, 0.25, tuple(top)), ARMOR_DK)
            m.add("forearm" + suf, rbox(0.075, 0.012, 0.11, 0.3, tuple(top + np.array([0, 0.018, 0]))), GREEN)
            m.add("forearm" + suf, rbox(0.075, 0.006, 0.012, 0.3, tuple(top + np.array([0, 0.026, 0.03]))), AMBER)
        # glove
        hd = np.array(BONES["hand" + suf][1])
        m.add("hand" + suf, ell((0.07, 0.065, 0.085), tuple(wr + (hd - wr) * 0.55), 0.5, 0.5), ARMOR_DK)
        for i in range(4):
            f0 = wr + (hd - wr) * 0.95 + np.array([(-0.03 + 0.02 * i) * sgn, -0.01 * (i % 2), 0.0])
            m.add("hand" + suf, limb(f0, f0 + (hd - wr) * 0.55, 0.014, 0.012, 6), RUBBER)
    arm(1); arm(-1)

    # ---------------- minigun in the right hand: body, grip, drum, shroud (hand.R) + spinning barrels (hand.R@barrels)
    g = GUN_DIR
    base = WR + (HD - WR) * 0.8
    def gun(mesh):   # modelled along +Z at the origin, then aimed along the hand and moved to the grip
        return place(mesh, base, g, (0, 0, 1))
    m.add("hand.R", gun(rbox(0.13, 0.15, 0.36, 0.25, (0, 0.03, 0.14))), GUNM)
    m.add("hand.R", gun(rbox(0.08, 0.15, 0.08, 0.3, (0, -0.09, 0.05), rot=(-12, 0, 0))), RUBBER)             # grip
    m.add("hand.R", gun(rbox(0.05, 0.035, 0.22, 0.3, (0, 0.13, 0.14))), METAL)                               # carry handle
    m.add("hand.R", gun(ring((0.15, 0.0, 0.1), (1, 0, 0), 0.11, 0.09, 20)), ARMOR)                           # ammo drum
    m.add("hand.R", gun(ring((0.15, 0.0, 0.1), (1, 0, 0), 0.075, 0.095, 16)), ORANGE)
    m.add("hand.R", gun(tube([(0.12, 0.02, 0.1), (0.06, 0.06, 0.14), (0.0, 0.03, 0.2)], 0.018, 6)), RUBBER)
    m.add("hand.R", gun(ring((0, 0.03, 0.31), (0, 0, 1), 0.075, 0.06, 18)), ARMOR_DK)                        # front collar
    m.add("hand.R", gun(rbox(0.07, 0.03, 0.10, 0.3, (-0.085, 0.07, 0.15))), GREEN)
    # barrels: six around the axis, with a muzzle clamp
    bars = []
    for i in range(6):
        ang = 2 * math.pi * i / 6 + math.radians(barrel_deg)
        x, y = 0.042 * math.cos(ang), 0.042 * math.sin(ang)
        bars.append((limb((x, y + 0.03, 0.34), (x, y + 0.03, 0.80), 0.016, 0.014, 8), METAL))
    bars.append((ring((0, 0.03, 0.62), (0, 0, 1), 0.062, 0.03, 18), ARMOR_DK))
    bars.append((ring((0, 0.03, 0.78), (0, 0, 1), 0.058, 0.03, 18), ARMOR_DK))
    bars.append((ring((0, 0.03, 0.34), (0, 0, 1), 0.052, 0.04, 14), METAL))
    pv = gun(np.array([[0, 0.03, 0.34]]), ) if False else (base + _rotv(g, np.array([0, 0.03, 0.34])))
    ax = pv + g
    for mesh, col in bars:
        mm = gun(mesh)
        if barrel_deg: mm = _rot_about(mm, tuple(pv), tuple(g), barrel_deg)
        m.add("hand.R@barrels", mm, col)
    m.pivots["hand.R@barrels"] = (tuple(pv), tuple(ax))

    # ---------------- legs (right, then mirrored)
    def leg(sgn):
        suf = ".R" if sgn > 0 else ".L"
        hp = np.array([HIP[0] * sgn, HIP[1], HIP[2]]); kn = np.array([KN[0] * sgn, KN[1], KN[2]]); an = np.array([AN[0] * sgn, AN[1], AN[2]])
        m.add("thigh" + suf, limb(hp, kn, 0.11, 0.085, 16), SUIT)
        m.add("thigh" + suf, rbox(0.14, 0.30, 0.07, 0.3, tuple((hp + kn) / 2 + np.array([0.0, 0.02, 0.09]))), ARMOR)          # thigh plate
        m.add("thigh" + suf, rbox(0.04, 0.22, 0.12, 0.3, tuple((hp + kn) / 2 + np.array([0.105 * sgn, 0.0, 0.0]))), ARMOR_DK)
        m.add("shin" + suf, ell((0.095, 0.075, 0.09), tuple(kn + np.array([0.0, 0.0, 0.045])), 0.6, 0.6), ARMOR)            # knee pad
        m.add("shin" + suf, ell((0.05, 0.02, 0.04), tuple(kn + np.array([0.0, 0.01, 0.115])), 1, 1, seg=12, rings=8), ORANGE)
        m.add("shin" + suf, limb(kn, an, 0.082, 0.062, 16), SUIT)
        m.add("shin" + suf, rbox(0.12, 0.30, 0.06, 0.3, tuple((kn + an) / 2 + np.array([0.0, 0.0, 0.065]))), ARMOR)           # greave
        m.add("shin" + suf, ring(tuple(an + np.array([0.0, 0.015, 0.0])), (0, 1, 0), 0.085, 0.07, 18), ARMOR_DK)
        m.add("foot" + suf, rbox(0.15, 0.12, 0.30, 0.3, tuple(an + np.array([0.0, -0.025, 0.06]))), ARMOR_DK)             # boot
        m.add("foot" + suf, rbox(0.16, 0.04, 0.32, 0.2, tuple(an + np.array([0.0, -0.085, 0.065]))), RUBBER)               # sole
        m.add("foot" + suf, ell((0.075, 0.05, 0.08), tuple(an + np.array([0.0, -0.01, 0.17])), 0.6, 0.6), ARMOR)           # toe cap
        m.add("foot" + suf, rbox(0.13, 0.05, 0.07, 0.3, tuple(an + np.array([0.0, 0.03, -0.065]))), ARMOR)                 # heel
    leg(1); leg(-1)
    return m


def _rotv(g, v):
    """The local vector v (modelled along +Z) as seen after aiming along g."""
    return rot_from_to((0, 0, 1), g) @ v


def _rot_about(mesh, point, axis, deg):
    P, N = mesh
    ax = np.array(axis, float); ax /= np.linalg.norm(ax)
    t = math.radians(deg)
    K = np.array([[0, -ax[2], ax[1]], [ax[2], 0, -ax[0]], [-ax[1], ax[0], 0]])
    R = np.eye(3) + math.sin(t) * K + (1 - math.cos(t)) * K @ K
    p = np.array(point, float)
    return (P - p) @ R.T + p, N @ R.T


def save(m, path):
    def wstr(f, s):
        b = s.encode(); f.write(struct.pack("<i", len(b))); f.write(b)
    names = list(m.parts.keys())
    bones = dict(BONES)
    for k, (h, t) in m.pivots.items():
        bones[k] = (h, t)
    with open(path, "wb") as f:
        f.write(b"DUEL"); f.write(struct.pack("<i", 3))
        f.write(struct.pack("<i", len(bones)))
        for n, (h, t) in bones.items():
            wstr(f, n); f.write(struct.pack("<3f", *h)); f.write(struct.pack("<3f", *t))
        f.write(struct.pack("<i", len(m.palette)))
        for p in m.palette: f.write(struct.pack("<4f", *p))
        f.write(struct.pack("<i", len(names)))
        for n in names:
            wstr(f, n)
            P = np.concatenate([x[0] for x in m.parts[n]]); N = np.concatenate([x[1] for x in m.parts[n]]); C = np.concatenate([x[2] for x in m.parts[n]])
            f.write(struct.pack("<i", len(P)))
            rec = np.zeros(len(P), dtype=[("p", "<f4", 3), ("n", "<f4", 3), ("c", "<i4")])
            rec["p"] = P; rec["n"] = N; rec["c"] = C
            f.write(rec.tobytes())
    return sum(len(np.concatenate([x[0] for x in m.parts[n]])) for n in names) // 3


if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else "surveyor_model.bin"
    m = build()
    tris = save(m, out)
    print("saved", out, "tris", tris, "parts", {k: sum(len(x[0]) for x in v) // 3 for k, v in m.parts.items()})
    if len(sys.argv) > 2:
        from preview import render
        render(m, sys.argv[2], views=((15, 12), (140, 12), (200, 15), (-80, 8)), size=520)
