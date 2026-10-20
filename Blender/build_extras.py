"""
The four Extra Deck toolkit monsters, built with meshkit (no Blender needed):
  extra_ScrapChimera.bin   Fusion  - a scrap beast welded from two machines, purple fusion seams
  extra_TunedColossus.bin  Synchro - a white armored giant inside three green tuning rings
  extra_OverlaySentinel.bin XYZ    - a floating obsidian knight circled by golden overlay units
  extra_LinkArbiter.bin    Link    - a geometric arbiter in front of a wheel of link arrows
Part names drive the in-game animation: SpinX*/SpinY*/SpinZ* turn on that axis, Float* bobs.
Models face +Z (toward the enemy), feet on y = 0.  Run: python3 build_extras.py <out_dir>
"""
import sys, os, math
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from meshkit import *
from preview import render

OUT = sys.argv[1] if len(sys.argv) > 1 else "."


def mirror_pair(m, part, mesh, col):
    m.add(part, mesh, col)
    m.add(part, flip(mesh, 0), col)


# ============================================================ Tuned Colossus (Synchro)
def tuned_colossus():
    m = Model()
    WHITE = m.color((0.92, 0.93, 0.96)); STEEL = m.color((0.46, 0.5, 0.58)); DARK = m.color((0.14, 0.15, 0.2))
    GOLD = m.color((0.95, 0.76, 0.32)); GLOW = m.color((0.35, 1.0, 0.55), 2.5); VISOR = m.color((0.45, 1.0, 0.7), 3.0)
    B = "Body"
    # legs
    for sx in (-1, 1):
        x = 0.34 * sx
        m.add(B, transform(superellipsoid((0.2, 0.18, 0.34), 0.35, 0.35), move=(x, 0.12, 0.08)), STEEL)          # foot
        m.add(B, transform(cylinder(0.17, 0.24, 0.72, 16), move=(x, 0.18, 0)), WHITE)                             # shin
        m.add(B, transform(superellipsoid((0.2, 0.14, 0.12), 0.4, 0.4), move=(x, 0.62, 0.17)), GOLD)            # shin guard
        m.add(B, transform(sphere(0.16, 14, 10), move=(x, 0.92, 0.05)), DARK)                                     # knee
        m.add(B, transform(superellipsoid((0.15, 0.1, 0.12), 0.5, 0.5), move=(x, 0.95, 0.17)), WHITE)           # knee cap
        m.add(B, transform(superellipsoid((0.22, 0.34, 0.24), 0.5, 0.6), move=(x, 1.24, 0)), WHITE)             # thigh
    # hips and waist
    m.add(B, transform(superellipsoid((0.46, 0.18, 0.3), 0.4, 0.4), move=(0, 1.5, 0)), STEEL)
    for sx in (-1, 1):
        m.add(B, transform(prism([(-0.16, 0.2), (0.16, 0.2), (0.12, -0.28), (-0.12, -0.28)], 0.06, 0.02), rot=(8, 0, 0), move=(0.3 * sx, 1.4, 0.3)), WHITE)
        m.add(B, transform(prism([(-0.14, 0.2), (0.14, 0.2), (0.1, -0.24), (-0.1, -0.24)], 0.06, 0.02), rot=(-8, 0, 0), move=(0.3 * sx, 1.4, -0.3)), WHITE)
    m.add(B, transform(prism([(-0.14, 0.18), (0.14, 0.18), (0.08, -0.34), (-0.08, -0.34)], 0.06, 0.02), rot=(6, 0, 0), move=(0, 1.38, 0.32)), GOLD)
    m.add(B, transform(cylinder(0.3, 0.36, 0.3, 18), move=(0, 1.6, 0)), DARK)                                     # waist
    # chest
    m.add(B, transform(superellipsoid((0.6, 0.44, 0.4), 0.45, 0.4), move=(0, 2.1, 0)), WHITE)
    m.add(B, transform(superellipsoid((0.38, 0.22, 0.12), 0.5, 0.4), move=(0, 2.18, 0.33)), STEEL)              # chest plate
    m.add(B, transform(torus(0.17, 0.035, 24, 8), rot=(90, 0, 0), move=(0, 2.16, 0.45)), GOLD)
    m.add(B, transform(sphere(0.13, 16, 12), move=(0, 2.16, 0.44)), GLOW)                                       # tuning core
    for sx in (-1, 1):
        m.add(B, transform(superellipsoid((0.2, 0.03, 0.03), 0.6, 0.6), rot=(0, 0, -18 * sx), move=(0.38 * sx, 1.9, 0.36)), GLOW)  # light lines
    # back: twin thrusters and a spine fin (the side the camera sees most)
    for sx in (-1, 1):
        m.add(B, transform(cylinder(0.13, 0.1, 0.62, 16), rot=(180, 0, 0), move=(0.22 * sx, 2.42, -0.42)), STEEL)
        m.add(B, transform(cylinder(0.15, 0.15, 0.08, 16), move=(0.22 * sx, 2.42, -0.42)), DARK)
        m.add(B, transform(cylinder(0.09, 0.0, 0.18, 12), rot=(180, 0, 0), move=(0.22 * sx, 1.8, -0.42)), GLOW)
    m.add(B, transform(prism([(0, 0.0), (0.0, 0.62), (-0.36, 0.1)], 0.06, 0.015), rot=(0, 90, 0), move=(0, 2.0, -0.38)), GOLD)
    # shoulders and arms
    for sx in (-1, 1):
        x = 0.86 * sx
        m.add(B, transform(superellipsoid((0.36, 0.26, 0.36), 0.45, 0.5), move=(x, 2.38, 0)), WHITE)            # pauldron
        m.add(B, transform(superellipsoid((0.38, 0.06, 0.38), 0.5, 0.5), move=(x, 2.27, 0)), GOLD)              # trim
        m.add(B, transform(cylinder(0.08, 0.0, 0.34, 10), rot=(0, 0, -40 * sx), move=(x + 0.12 * sx, 2.55, 0)), GOLD)   # horn
        m.add(B, transform(cylinder(0.12, 0.14, 0.5, 14), rot=(180, 0, 8 * sx), move=(x * 0.97, 2.25, 0)), DARK)  # upper arm
        m.add(B, transform(sphere(0.13, 12, 8), move=(x * 1.02, 1.74, 0.02)), DARK)                              # elbow
        m.add(B, transform(superellipsoid((0.2, 0.36, 0.22), 0.45, 0.5), move=(x * 1.03, 1.44, 0.06)), WHITE)  # gauntlet
        m.add(B, transform(superellipsoid((0.21, 0.05, 0.23), 0.5, 0.5), move=(x * 1.03, 1.62, 0.06)), GLOW)  # glow cuff
        m.add(B, transform(superellipsoid((0.17, 0.16, 0.18), 0.5, 0.5), move=(x * 1.03, 1.0, 0.08)), STEEL)   # fist
    # head
    m.add(B, transform(cylinder(0.12, 0.14, 0.18, 12), move=(0, 2.46, 0)), DARK)
    m.add(B, transform(superellipsoid((0.21, 0.24, 0.23), 0.7, 0.75), move=(0, 2.78, 0)), WHITE)
    m.add(B, transform(superellipsoid((0.17, 0.045, 0.06), 0.4, 0.4), move=(0, 2.8, 0.19)), VISOR)
    m.add(B, transform(prism([(0, 0), (0.02, 0.42), (-0.32, 0.14)], 0.05, 0.012), rot=(0, 90, 0), move=(0, 2.9, 0.05)), GOLD)  # crest
    for sx in (-1, 1):
        m.add(B, transform(cylinder(0.035, 0.0, 0.34, 8), rot=(0, 0, -55 * sx), move=(0.16 * sx, 2.86, 0.08)), GOLD)
    # the three synchro rings
    for k, (h, R, tilt) in enumerate(((0.75, 1.15, 6), (1.65, 1.3, -5), (2.55, 1.15, 4))):
        name = f"SpinYRing{k + 1}"
        m.add(name, transform(torus(R, 0.035, 64, 8), rot=(tilt, 0, 0), move=(0, h, 0)), GLOW)
        for a in range(3):
            ang = a * 120 + k * 40
            px, pz = R * math.cos(math.radians(ang)), R * math.sin(math.radians(ang))
            m.add(name, transform(superellipsoid((0.08, 0.08, 0.08), 2, 2), move=(px, h, pz)), VISOR)
        m.pivot(name, (0, h, 0))
    return m


# ============================================================ Scrap Chimera (Fusion)
def scrap_chimera():
    m = Model()
    RUST = m.color((0.66, 0.34, 0.17)); IRON = m.color((0.32, 0.34, 0.38)); DARK = m.color((0.11, 0.11, 0.13))
    BRASS = m.color((0.84, 0.62, 0.26)); FUSE = m.color((0.78, 0.38, 1.0), 2.8); EYE = m.color((1.0, 0.32, 0.2), 3.0)
    TEAL = m.color((0.2, 0.55, 0.55))
    B = "Body"
    # torso: two welded halves (front rusty, back iron) with a glowing purple seam
    m.add(B, transform(superellipsoid((0.56, 0.44, 0.55), 0.45, 0.45), move=(0, 1.15, 0.38)), RUST)
    m.add(B, transform(superellipsoid((0.5, 0.42, 0.6), 0.5, 0.5), move=(0, 1.12, -0.4)), IRON)
    m.add(B, transform(torus(0.47, 0.04, 40, 8), rot=(90, 0, 0), move=(0, 1.13, 0.0)), FUSE)
    for z in (-0.65, -0.2, 0.25, 0.65):                                       # rivets along the spine
        m.add(B, transform(sphere(0.05, 8, 6), move=(0, 1.58, z)), BRASS)
    # spine exhausts (back, seen by the camera)
    for i, (x, z, lean) in enumerate(((-0.24, -0.55, -15), (0.24, -0.55, 15), (-0.2, -0.15, -10), (0.2, -0.15, 10))):
        m.add(B, transform(cylinder(0.07, 0.09, 0.5, 12), rot=(-25, 0, lean), move=(x, 1.42, z)), IRON)
        m.add(B, transform(cylinder(0.1, 0.1, 0.06, 12), rot=(-25, 0, lean), move=(x + math.sin(math.radians(-lean)) * 0.48, 1.42 + 0.44, z - 0.2)), DARK)
        m.add(B, transform(sphere(0.06, 10, 8), move=(x + math.sin(math.radians(-lean)) * 0.5, 1.9, z - 0.22)), FUSE)
    # legs: back legs digitigrade iron, front-left a heavy piston, front-right a clawed rust leg
    def leg(x, z, col, heavy=False):
        top = np.array([x, 1.0, z]); knee = np.array([x * 1.15, 0.6, z + (0.12 if z > 0 else -0.16)]); foot = np.array([x * 1.15, 0.12, z + 0.05])
        r = 0.15 if heavy else 0.11
        m.add(B, tube([top, knee], r, 12), col)
        m.add(B, tube([knee, foot], r * 0.85, 12), DARK if not heavy else IRON)
        m.add(B, transform(sphere(r * 1.1, 12, 8), move=knee), BRASS)
        m.add(B, transform(superellipsoid((0.16, 0.08, 0.2), 0.4, 0.4), move=(foot[0], 0.08, foot[2] + 0.06)), IRON)
        for c in (-1, 0, 1):
            m.add(B, transform(cylinder(0.035, 0.0, 0.16, 8), rot=(80, c * 22, 0), move=(foot[0] + c * 0.08, 0.06, foot[2] + 0.2)), BRASS)
        if heavy:
            m.add(B, transform(cylinder(0.05, 0.05, 0.42, 10), rot=(15, 0, 0), move=(x * 1.35, 0.35, z)), BRASS)
    leg(-0.42, 0.6, RUST, heavy=True)
    leg(0.42, 0.6, RUST)
    leg(-0.4, -0.62, IRON)
    leg(0.4, -0.62, IRON)
    # head 1: mechanical lion (left), mane of blades
    hx, hy, hz = -0.24, 1.55, 1.0
    m.add(B, tube([(-0.12, 1.35, 0.75), (hx, hy, hz - 0.1)], 0.16, 12), IRON)
    m.add(B, transform(superellipsoid((0.24, 0.22, 0.3), 0.45, 0.45), move=(hx, hy, hz)), RUST)
    m.add(B, transform(superellipsoid((0.18, 0.08, 0.24), 0.5, 0.5), move=(hx, hy - 0.18, hz + 0.1)), DARK)   # jaw
    for sx in (-1, 1):
        m.add(B, transform(sphere(0.045, 8, 6), move=(hx + 0.11 * sx, hy + 0.06, hz + 0.27)), EYE)
    for a in range(9):
        ang = math.radians(-90 + a * 22.5)
        d = np.array([math.cos(ang) * 0.9, math.sin(ang) * 0.9 + 0.4, -0.35])
        d = d / np.linalg.norm(d)
        tip = np.array([hx, hy, hz - 0.12]) + d * 0.44
        m.add(B, tube([np.array([hx, hy, hz - 0.12]) + d * 0.16, tip], 0.0, 6, radii=[0.07, 0.005]), BRASS)
    # head 2: serpent on a long cable neck (right)
    neck = [(0.18, 1.35, 0.75), (0.42, 1.65, 0.95), (0.52, 2.05, 1.0), (0.46, 2.32, 1.12)]
    m.add(B, tube(neck, 0.1, 10, radii=[0.12, 0.1, 0.09, 0.08]), TEAL)
    m.add(B, transform(superellipsoid((0.15, 0.12, 0.2), 0.8, 0.8), move=(0.46, 2.36, 1.2)), TEAL)                 # skull
    m.add(B, transform(cylinder(0.11, 0.03, 0.3, 12), rot=(90, 0, 0), move=(0.46, 2.33, 1.3)), TEAL)                # snout
    m.add(B, transform(cylinder(0.08, 0.02, 0.26, 10), rot=(100, 0, 0), move=(0.46, 2.25, 1.28)), DARK)              # jaw
    for sx in (-1, 1):
        m.add(B, transform(sphere(0.035, 8, 6), move=(0.46 + 0.09 * sx, 2.42, 1.3)), EYE)
        m.add(B, transform(cylinder(0.03, 0.0, 0.22, 6), rot=(-60, 0, 25 * sx), move=(0.46 + 0.06 * sx, 2.44, 1.16)), BRASS)
    # tail: segmented cable rising behind, ending in a spinning saw
    tail = [(0, 1.2, -0.95), (0, 1.35, -1.3), (0, 1.75, -1.48), (0, 2.15, -1.38), (0, 2.38, -1.12)]
    m.add(B, tube(tail, 0.07, 10, radii=[0.1, 0.09, 0.08, 0.07, 0.06]), IRON)
    for p in tail[1:-1]:
        m.add(B, transform(torus(0.1, 0.025, 16, 6), rot=(60, 0, 0), move=p), BRASS)
    saw = np.array(tail[-1]) + np.array([0, 0.12, 0.12])
    m.add("SpinXSaw", transform(torus(0.2, 0.03, 32, 6), rot=(0, 0, 90), move=saw), IRON)
    m.add("SpinXSaw", transform(cylinder(0.06, 0.06, 0.08, 12), rot=(0, 0, 90), move=saw + np.array([0.04, 0, 0])), FUSE)
    for k in range(10):
        a = k * 36
        off = np.array([0, math.cos(math.radians(a)) * 0.24, math.sin(math.radians(a)) * 0.24])
        m.add("SpinXSaw", tube([saw + off * 0.85, saw + off * 1.25], 0.0, 4, radii=[0.035, 0.002]), BRASS)
    m.pivot("SpinXSaw", saw)
    # shoulder gear (left)
    gp = np.array([-0.6, 1.4, 0.45])
    m.add("SpinXGear", transform(cylinder(0.2, 0.2, 0.06, 20), rot=(0, 0, 90), move=gp + np.array([-0.03, 0, 0])), BRASS)
    for k in range(10):
        a = math.radians(k * 36)
        m.add("SpinXGear", transform(superellipsoid((0.03, 0.045, 0.045), 0.4, 0.4), move=gp + np.array([0, math.cos(a) * 0.23, math.sin(a) * 0.23])), BRASS)
    m.add("SpinXGear", transform(sphere(0.07, 10, 8), move=gp + np.array([-0.04, 0, 0])), FUSE)
    m.pivot("SpinXGear", gp)
    return m


# ============================================================ Overlay Sentinel (XYZ)
def overlay_sentinel():
    m = Model()
    OBS = m.color((0.09, 0.09, 0.12)); SLATE = m.color((0.24, 0.22, 0.3)); GOLD = m.color((0.95, 0.76, 0.3))
    GLOW = m.color((1.0, 0.86, 0.35), 3.0); CLOTH = m.color((0.32, 0.12, 0.4))
    F = "FloatBody"
    # tapering phantom lower body
    m.add(F, lathe([(0.0, 0.45), (0.18, 0.55), (0.34, 0.85), (0.42, 1.2), (0.4, 1.45)], 24), CLOTH)
    m.add(F, transform(torus(0.41, 0.04, 30, 8), move=(0, 1.3, 0)), GOLD)
    m.add(F, transform(cylinder(0.04, 0.0, 0.2, 10), rot=(180, 0, 0), move=(0, 0.47, 0)), GOLD)
    # torso
    m.add(F, transform(superellipsoid((0.5, 0.42, 0.34), 0.35, 0.4), move=(0, 1.8, 0)), OBS)
    m.add(F, transform(prism([(-0.3, 0.32), (0.3, 0.32), (0.0, -0.3)], 0.05, 0.015), move=(0, 1.86, 0.32)), GOLD)
    m.add(F, transform(sphere(0.08, 12, 8), move=(0, 1.86, 0.36)), GLOW)
    # back monoliths (what the camera sees from behind): three tall panels like a throne of cards
    for k, (x, h, rz) in enumerate(((-0.42, 1.1, 14), (0, 1.35, 0), (0.42, 1.1, -14))):
        m.add(F, transform(superellipsoid((0.16, h / 2, 0.04), 0.2, 0.2), rot=(-8, 0, rz), move=(x, 1.75 + h * 0.25, -0.38)), SLATE)
        m.add(F, transform(superellipsoid((0.12, h / 2 - 0.08, 0.02), 0.2, 0.2), rot=(-8, 0, rz), move=(x, 1.75 + h * 0.25, -0.42)), OBS)
        m.add(F, transform(superellipsoid((0.025, h / 2 - 0.16, 0.02), 0.5, 0.5), rot=(-8, 0, rz), move=(x, 1.75 + h * 0.25, -0.445)), GLOW)
    # shoulders with gold rims
    for sx in (-1, 1):
        m.add(F, transform(superellipsoid((0.3, 0.2, 0.3), 0.4, 0.5), move=(0.62 * sx, 2.12, 0)), OBS)
        m.add(F, transform(torus(0.27, 0.025, 24, 6), rot=(0, 0, 10 * sx), move=(0.62 * sx, 2.05, 0)), GOLD)
        m.add(F, tube([(0.66 * sx, 1.98, 0), (0.74 * sx, 1.55, 0.12), (0.72 * sx, 1.25, 0.32)], 0.1, 10), SLATE)
    # kite shield (left) and lance (right)
    shield = transform(prism([(0, 0.55), (0.3, 0.3), (0.24, -0.25), (0, -0.62), (-0.24, -0.25), (-0.3, 0.3)], 0.08, 0.03), rot=(0, -12, 0), move=(-0.8, 1.35, 0.42))
    m.add(F, shield, OBS)
    m.add(F, transform(prism([(0, 0.42), (0.22, 0.22), (0.17, -0.2), (0, -0.48), (-0.17, -0.2), (-0.22, 0.22)], 0.1, 0.02), rot=(0, -12, 0), move=(-0.8, 1.35, 0.44)), GOLD)
    m.add(F, transform(sphere(0.07, 10, 8), move=(-0.79, 1.4, 0.5)), GLOW)
    m.add(F, tube([(0.72, 0.55, 0.45), (0.72, 2.7, 0.2)], 0.045, 8), GOLD)
    m.add(F, transform(cylinder(0.1, 0.0, 0.5, 4), rot=(-6, 45, 0), move=(0.72, 2.66, 0.21)), SLATE)
    # helm: tall crowned helmet, one glowing slit
    m.add(F, transform(superellipsoid((0.19, 0.26, 0.21), 0.4, 0.5), move=(0, 2.48, 0)), OBS)
    m.add(F, transform(superellipsoid((0.13, 0.025, 0.04), 0.4, 0.4), move=(0, 2.48, 0.18)), GLOW)
    for k in range(5):
        a = math.radians(-60 + k * 30)
        m.add(F, transform(cylinder(0.04, 0.0, 0.28 if k == 2 else 0.2, 6), rot=(-10, 0, math.degrees(-a) * 0.6), move=(math.sin(a) * 0.14, 2.68, -0.02)), GOLD)
    # overlay units (XYZ materials) orbiting
    for k in range(3):
        a = math.radians(k * 120)
        p = (math.cos(a) * 0.95, 1.55 + 0.25 * math.sin(a * 2), math.sin(a) * 0.95)
        m.add("SpinYOrbit", transform(sphere(0.11, 14, 10), move=p), GLOW)
        m.add("SpinYOrbit", transform(torus(0.16, 0.012, 20, 6), rot=(70, k * 60, 0), move=p), GOLD)
    m.pivot("SpinYOrbit", (0, 1.55, 0))
    # rune ring under it
    m.add("SpinYRune", transform(torus(0.75, 0.02, 48, 6), move=(0, 0.22, 0)), GLOW)
    for k in range(8):
        a = math.radians(k * 45)
        m.add("SpinYRune", transform(superellipsoid((0.06, 0.012, 0.06), 2, 2), move=(math.cos(a) * 0.75, 0.22, math.sin(a) * 0.75)), GOLD)
    m.pivot("SpinYRune", (0, 0.22, 0))
    m.pivot(F, (0, 1.4, 0))
    return m


# ============================================================ Link Arbiter (Link)
def link_arbiter():
    m = Model()
    NAVY = m.color((0.1, 0.16, 0.34)); STEEL = m.color((0.38, 0.48, 0.68)); WHITE = m.color((0.9, 0.93, 0.98))
    CYAN = m.color((0.3, 0.88, 1.0), 2.8); RED = m.color((1.0, 0.25, 0.25), 2.6); DARK = m.color((0.07, 0.09, 0.16))
    B = "Body"
    # pedestal
    m.add(B, lathe([(0.0, 0.0), (0.62, 0.0), (0.62, 0.1), (0.46, 0.18), (0.3, 0.42), (0.34, 0.5), (0.0, 0.5)], 6, flat=True), NAVY)
    m.add(B, transform(torus(0.55, 0.025, 6, 4), move=(0, 0.12, 0)), CYAN)
    F = "FloatBody"
    # angular torso: hexagonal prisms stacked, cyan circuit lines
    m.add(F, transform(prism([(math.cos(math.radians(a)) * 0.36, math.sin(math.radians(a)) * 0.42) for a in range(30, 390, 60)], 0.42, 0.06), move=(0, 1.55, 0)), STEEL)
    m.add(F, transform(prism([(math.cos(math.radians(a)) * 0.24, math.sin(math.radians(a)) * 0.26) for a in range(30, 390, 60)], 0.46, 0.04), move=(0, 1.55, 0)), NAVY)
    m.add(F, transform(superellipsoid((0.16, 0.16, 0.16), 2, 2), move=(0, 1.58, 0.26)), CYAN)          # core gem
    m.add(F, transform(cylinder(0.2, 0.08, 0.36, 6), rot=(180, 0, 0), move=(0, 1.2, 0)), NAVY)         # waist taper
    m.add(F, transform(superellipsoid((0.1, 0.12, 0.1), 2, 2), move=(0, 0.88, 0)), CYAN)
    # shoulder blades and floating arms
    for sx in (-1, 1):
        blade = transform(prism([(0, 0), (0.5, 0.18), (0.42, -0.1)], 0.14, 0.03), move=(0.3, 1.86, 0))
        m.add(F, blade if sx > 0 else flip(blade, 0), WHITE)
        m.add(F, transform(superellipsoid((0.09, 0.3, 0.09), 2, 2), rot=(0, 0, 12 * sx), move=(0.78 * sx, 1.45, 0.08)), STEEL)
        m.add(F, transform(superellipsoid((0.05, 0.22, 0.05), 2, 2), rot=(0, 0, 12 * sx), move=(0.86 * sx, 0.98, 0.12)), CYAN)
    # head: diamond with a visor crown
    m.add(F, transform(superellipsoid((0.17, 0.24, 0.17), 2, 2), move=(0, 2.28, 0)), WHITE)
    m.add(F, transform(superellipsoid((0.14, 0.03, 0.05), 0.4, 0.4), move=(0, 2.3, 0.13)), CYAN)
    for k in range(3):
        m.add(F, transform(cylinder(0.035, 0.0, 0.22, 4), rot=(0, 0, (k - 1) * 30), move=((k - 1) * 0.08, 2.46, 0)), STEEL)
    m.pivot(F, (0, 1.5, 0))
    # the link wheel behind it: 8 arrows on a ring, turning in its own plane
    W = "SpinZWheel"
    c = np.array([0, 1.75, -0.45])
    m.add(W, transform(torus(0.95, 0.04, 8, 6), rot=(90, 22.5, 0), move=c), STEEL)       # octagon frame
    m.add(W, transform(torus(0.72, 0.02, 48, 6), rot=(90, 0, 0), move=c), CYAN)
    for k in range(8):
        a = k * 45
        ca, sa = math.cos(math.radians(a)), math.sin(math.radians(a))
        arrow = prism([(-0.16, -0.1), (0.16, -0.1), (0, 0.16)], 0.06, 0.012)
        arrow = transform(arrow, rot=(0, 0, a - 90), move=c + np.array([ca * 1.12, sa * 1.12, 0]))
        m.add(W, arrow, RED)
        m.add(W, tube([c + np.array([ca * 0.74, sa * 0.74, 0]), c + np.array([ca * 0.93, sa * 0.93, 0])], 0.025, 6), DARK)
    m.pivot(W, c)
    return m


BUILD = {
    "ScrapChimera": scrap_chimera,
    "TunedColossus": tuned_colossus,
    "OverlaySentinel": overlay_sentinel,
    "LinkArbiter": link_arbiter,
}

if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    for name, fn in BUILD.items():
        m = fn()
        lo, hi = m.bounds()
        tris = sum(len(p) for L in m.parts.values() for (p, _, _) in L) // 3
        m.save(os.path.join(OUT, f"extra_{name}.bin"))
        render(m, os.path.join(OUT, f"extra_{name}.png"), views=((200, 22), (25, 15)))
        print(name, tris, "tris, size", np.round(hi - lo, 2))
