"""
The Surveyor in Blender: a classic NASA-style EVA astronaut (white / orange / blue) with a rotary minigun.
Run inside Blender (Scripting tab, or the Blender MCP). It builds collection "Surveyor", renders previews and
exports surveyor_model.bin (the "DUEL" format SvrModel.cs loads onto the Commando skeleton) to OUT_DIR.

Everything is built in Unity space (+Y up, +Z forward, +X = the character's right) through U(), which maps it to
Blender space. Each object's "part" property is the bone it rides on ("thigh.R" ...) or "<bone>@<tag>" for the moving
pieces: head@visor (hinge), hand.R@barrels (spin), chest@lidar (spin).
"""
import bpy, bmesh, math, os, struct
from mathutils import Vector, Matrix

OUT_DIR = os.path.join(os.path.expanduser("~"), "Documents", "SurveyorModel")
os.makedirs(OUT_DIR, exist_ok=True)


def U(p):
    return Vector((-p[0], -p[2], p[1]))


def B2U(v):
    return (-v[0], v[2], -v[1])


# ------------------------------------------------------------------ scene

COL = bpy.data.collections.get("Surveyor")
if COL:
    for o in list(COL.objects):
        bpy.data.objects.remove(o, do_unlink=True)
else:
    COL = bpy.data.collections.new("Surveyor")
    bpy.context.scene.collection.children.link(COL)
for n in ("Cube",):
    o = bpy.data.objects.get(n)
    if o: bpy.data.objects.remove(o, do_unlink=True)

MATS = {}


def mat(name, srgb, emit=0.0, rough=0.55, metal=0.0):
    lin = tuple(c ** 2.2 for c in srgb)
    m = bpy.data.materials.get("SV_" + name) or bpy.data.materials.new("SV_" + name)
    m.diffuse_color = (*lin, 1.0)
    m["emit"] = float(emit)
    try:
        m.use_nodes = True
        b = m.node_tree.nodes.get("Principled BSDF")
        if b:
            b.inputs["Base Color"].default_value = (*lin, 1.0)
            b.inputs["Roughness"].default_value = rough
            b.inputs["Metallic"].default_value = metal
            if emit > 0:
                b.inputs["Emission Color"].default_value = (*lin, 1.0)
                b.inputs["Emission Strength"].default_value = emit
    except Exception:
        pass
    MATS[name] = m
    return m


WHITE = mat("White", (0.94, 0.94, 0.93)); FABRIC = mat("Fabric", (0.84, 0.85, 0.86)); GREY = mat("Grey", (0.62, 0.64, 0.67))
BLUE = mat("Blue", (0.09, 0.29, 0.66)); ORANGE = mat("Orange", (1.0, 0.45, 0.05)); METAL = mat("Metal", (0.66, 0.69, 0.73), metal=0.8, rough=0.3)
DARK = mat("Dark", (0.11, 0.12, 0.14)); GLASS = mat("Glass", (0.03, 0.06, 0.15), rough=0.05)
GLOWB = mat("GlowBlue", (0.35, 0.78, 1.0), 2.6); GLOWO = mat("GlowOrange", (1.0, 0.55, 0.1), 2.2); GLOWR = mat("GlowRed", (1.0, 0.2, 0.08), 2.2)
SKIN = mat("Skin", (0.78, 0.59, 0.49)); LIPS = mat("Lips", (0.64, 0.43, 0.39)); BROW = mat("Brow", (0.27, 0.2, 0.16))
EYE = mat("Eye", (0.70, 0.74, 0.77), 0.25); CAP = mat("Cap", (0.24, 0.2, 0.17)); GUN = mat("Gun", (0.24, 0.26, 0.31), metal=0.5, rough=0.4)

# ------------------------------------------------------------------ math helpers (Unity space)


def V3(*a): return Vector(a)


def basis(d):
    d = Vector(d).normalized()
    ref = Vector((0, 1, 0)) if abs(d.y) < 0.9 else Vector((1, 0, 0))
    u = d.cross(ref).normalized()
    v = d.cross(u)
    return d, u, v


def rot_from_to(a, b):
    return Vector(a).normalized().rotation_difference(Vector(b).normalized()).to_matrix()


def euler(rx=0, ry=0, rz=0):
    return (Matrix.Rotation(math.radians(rz), 3, 'Z') @ Matrix.Rotation(math.radians(ry), 3, 'Y') @ Matrix.Rotation(math.radians(rx), 3, 'X'))


# ------------------------------------------------------------------ mesh builders


def make(name, part, verts, faces, m, smooth=True, subsurf=0, mats=None, fidx=None):
    me = bpy.data.meshes.new(name)
    me.from_pydata([U(v) for v in verts], [], faces)
    me.validate()
    ob = bpy.data.objects.new(name, me)
    COL.objects.link(ob)
    ob["part"] = part
    for mm in (mats or [m]):
        me.materials.append(mm)
    if fidx:
        for p, i in zip(me.polygons, fidx): p.material_index = i
    bm = bmesh.new(); bm.from_mesh(me)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = smooth
    if subsurf:
        md = ob.modifiers.new("ss", 'SUBSURF'); md.levels = subsurf; md.render_levels = subsurf
    return ob


def grid(name, part, rows, m, close=True, caps=True, **kw):
    R, C = len(rows), len(rows[0])
    verts = [p for row in rows for p in row]
    faces = []
    cols = C if close else C - 1
    for i in range(R - 1):
        for j in range(cols):
            j2 = (j + 1) % C
            faces.append((i * C + j, i * C + j2, (i + 1) * C + j2, (i + 1) * C + j))
    if caps and close:
        faces.append(tuple(range(C))[::-1])
        faces.append(tuple((R - 1) * C + j for j in range(C)))
    return make(name, part, verts, faces, m, **kw)


def sq(name, part, c, r, m, e1=1.0, e2=1.0, rot=None, seg=32, rings=20, xf=None, **kw):
    """Superellipsoid (e=1 ellipsoid, e~0.3 rounded box)."""
    def sp(w, p): return math.copysign(abs(w) ** p, w)
    R = rot or Matrix.Identity(3)
    rows = []
    for i in range(1, rings):
        u = -math.pi / 2 + math.pi * i / rings
        row = []
        for j in range(seg):
            v = -math.pi + 2 * math.pi * j / seg
            p = Vector((r[0] * sp(math.cos(u), e1) * sp(math.cos(v), e2), r[1] * sp(math.sin(u), e1), r[2] * sp(math.cos(u), e1) * sp(math.sin(v), e2)))
            q = Vector(c) + R @ p
            row.append(xf(q) if xf else q)
        rows.append(row)
    return grid(name, part, rows, m, **kw)


def lathe(name, part, a, b, prof, m, seg=28, xf=None, **kw):
    """Round tube from a to b; prof = [(t 0..1, radius), ...]."""
    a = Vector(a); b = Vector(b)
    d, u, v = basis(b - a)
    L = (b - a).length
    rows = []
    for t, r in prof:
        row = []
        for j in range(seg):
            ang = 2 * math.pi * j / seg
            q = a + d * (t * L) + (u * math.cos(ang) + v * math.sin(ang)) * r
            row.append(xf(q) if xf else q)
        rows.append(row)
    return grid(name, part, rows, m, **kw)


def ring(name, part, c, axis, R, r, m, seg=36, sides=10, xf=None, **kw):
    d, u, v = basis(axis)
    rows = []
    for i in range(seg):
        a = 2 * math.pi * i / seg
        cen = Vector(c) + (u * math.cos(a) + v * math.sin(a)) * R
        rad = (u * math.cos(a) + v * math.sin(a))
        row = []
        for j in range(sides):
            b2 = 2 * math.pi * j / sides
            q = cen + (rad * math.cos(b2) + d * math.sin(b2)) * r
            row.append(xf(q) if xf else q)
        rows.append(row)
    R_, C = len(rows), len(rows[0])
    verts = [p for row in rows for p in row]
    faces = []
    for i in range(R_):
        for j in range(C):
            i2, j2 = (i + 1) % R_, (j + 1) % C
            faces.append((i * C + j, i * C + j2, i2 * C + j2, i2 * C + j))
    return make(name, part, verts, faces, m, **kw)


def tube(name, part, pts, r, m, sides=10, **kw):
    pts = [Vector(p) for p in pts]
    rows = []
    for i, p in enumerate(pts):
        t = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        d, u, v = basis(t)
        rows.append([p + (u * math.cos(2 * math.pi * j / sides) + v * math.sin(2 * math.pi * j / sides)) * r for j in range(sides)])
    return grid(name, part, rows, m, **kw)


def cut(ob, keep):
    """Delete faces whose centre (Unity space) fails keep()."""
    bm = bmesh.new(); bm.from_mesh(ob.data)
    kill = [f for f in bm.faces if not keep(Vector(B2U(f.calc_center_median())))]
    bmesh.ops.delete(bm, geom=kill, context='FACES')
    bm.to_mesh(ob.data); bm.free()
    return ob


# ------------------------------------------------------------------ skeleton (Unity space, matches the Commando mapping)

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


def J(name, end=0):
    return Vector(BONES[name][end])


HC = Vector((0, 1.78, 0.0))       # helmet centre = visor hinge
PIVOTS = {}

# ------------------------------------------------------------------ legs: puffy EVA pants with knee bellows, big boots

for s, S in ((1, ".R"), (-1, ".L")):
    hp, kn, an = J("thigh" + S), J("shin" + S), J("foot" + S)
    up = (hp - kn).normalized()
    lathe("Thigh" + S, "thigh" + S, hp + up * 0.05, kn, [(0, 0.12), (0.06, 0.145), (0.3, 0.15), (0.7, 0.132), (0.92, 0.122), (1.0, 0.118)], FABRIC)
    lathe("ThighBearing" + S, "thigh" + S, hp + up * 0.03, hp - up * 0.015, [(0, 0.15), (1, 0.15)], BLUE)
    lathe("ThighStripe" + S, "thigh" + S, hp + (kn - hp) * 0.72, hp + (kn - hp) * 0.82, [(0, 0.136), (1, 0.132)], ORANGE)
    tm = hp + (kn - hp) * 0.42
    sq("ThighPlate" + S, "thigh" + S, tm + Vector((0.012 * s, 0, 0.115)), (0.105, 0.15, 0.05), WHITE, 0.45, 0.45, rot=euler(-4, 12 * s, 0))
    sq("ThighPlateEdge" + S, "thigh" + S, tm + Vector((0.012 * s, -0.135, 0.13)), (0.09, 0.016, 0.035), BLUE, 0.5, 0.5, rot=euler(-4, 12 * s, 0))
    sq("HipPlate" + S, "thigh" + S, hp + Vector((0.11 * s, -0.02, 0.0)), (0.06, 0.12, 0.12), WHITE, 0.5, 0.5, rot=euler(0, 0, -6 * s))
    dn = (an - kn).normalized()
    knee = [(0, 0.124), (0.05, 0.132), (0.1, 0.12), (0.15, 0.13), (0.2, 0.118), (0.25, 0.126), (0.32, 0.114), (0.6, 0.104), (0.86, 0.097), (1.0, 0.095)]
    lathe("Shin" + S, "shin" + S, kn - dn * 0.05, an + dn * 0.03, knee, FABRIC)
    lathe("ShinStripe" + S, "shin" + S, kn + (an - kn) * 0.52, kn + (an - kn) * 0.61, [(0, 0.11), (1, 0.107)], ORANGE)
    sq("KneePad" + S, "shin" + S, kn + Vector((0, 0.0, 0.1)), (0.085, 0.085, 0.05), WHITE, 0.6, 0.6)
    lathe("KneeDisc" + S, "shin" + S, kn + Vector((0, 0, 0.14)), kn + Vector((0, 0, 0.155)), [(0, 0.04), (1, 0.035)], ORANGE, seg=20)
    sm = kn + (an - kn) * 0.42
    sq("ShinGuard" + S, "shin" + S, sm + Vector((0, 0, 0.088)), (0.08, 0.15, 0.045), WHITE, 0.45, 0.45, rot=euler(4, 0, 0))
    sq("ShinGuardEdge" + S, "shin" + S, sm + Vector((0, -0.13, 0.1)), (0.07, 0.014, 0.03), BLUE, 0.5, 0.5)
    f = an
    sq("Boot" + S, "foot" + S, (f.x, 0.085, 0.06), (0.088, 0.088, 0.175), WHITE, 0.5, 0.55)
    sq("Sole" + S, "foot" + S, (f.x, 0.02, 0.062), (0.095, 0.028, 0.185), GREY, 0.3, 0.45)
    sq("Toe" + S, "foot" + S, (f.x, 0.075, 0.175), (0.074, 0.06, 0.06), GREY, 0.65, 0.65)
    lathe("BootCuff" + S, "foot" + S, (f.x, 0.12, 0.0), (f.x, 0.185, 0.0), [(0, 0.108), (1, 0.104)], BLUE)
    sq("AnklePlate" + S, "foot" + S, (f.x + 0.07 * s, 0.13, 0.0), (0.03, 0.05, 0.07), WHITE, 0.5, 0.5)

# ------------------------------------------------------------------ hips and waist

sq("Brief", "hips", (0, 0.985, 0), (0.235, 0.14, 0.17), WHITE, 0.6, 0.75)
ring("WaistBearing", "hips", (0, 1.07, 0), (0, 1, 0), 0.215, 0.026, BLUE)
sq("Codpiece", "hips", (0, 0.97, 0.15), (0.09, 0.09, 0.04), WHITE, 0.45, 0.45)
sq("Waist", "spine", (0, 1.17, 0), (0.22, 0.13, 0.16), FABRIC, 0.7, 0.8)
for i in range(3):
    ring("WaistBellow%d" % i, "spine", (0, 1.115 + 0.045 * i, 0), (0, 1, 0), 0.215, 0.016, GREY)
sq("AbPlate", "spine", (0, 1.17, 0.15), (0.13, 0.08, 0.035), WHITE, 0.45, 0.45)

# ------------------------------------------------------------------ upper torso: hard shell, chest control box, backpack

sq("HUT", "chest", (0, 1.37, 0.0), (0.275, 0.18, 0.185), WHITE, 0.55, 0.6)
for s in (1, -1):
    sq("Shoulder%d" % s, "chest", (0.23 * s, 1.44, 0.0), (0.12, 0.095, 0.13), WHITE, 0.8, 0.8)
sq("ChestPlate", "chest", (0, 1.39, 0.15), (0.22, 0.14, 0.06), WHITE, 0.45, 0.5)
sq("ChestPlateEdge", "chest", (0, 1.27, 0.17), (0.19, 0.016, 0.04), BLUE, 0.5, 0.5)
sq("DCM", "chest", (0, 1.31, 0.215), (0.115, 0.07, 0.045), WHITE, 0.3, 0.35)
sq("DCMFace", "chest", (0, 1.31, 0.26), (0.098, 0.055, 0.008), BLUE, 0.3, 0.35)
sq("DCMScreen", "chest", (-0.035, 1.325, 0.268), (0.04, 0.02, 0.004), GLOWB, 0.3, 0.3)
for i, (x, mm) in enumerate(((0.035, ORANGE), (0.065, GREY), (0.05, GLOWO))):
    lathe("Knob%d" % i, "chest", (x, 1.30 + 0.012 * i, 0.263), (x, 1.30 + 0.012 * i, 0.283), [(0, 0.012), (1, 0.011)], mm, seg=16)
sq("Stripe", "chest", (0, 1.47, 0.155), (0.17, 0.014, 0.035), ORANGE, 0.5, 0.5, rot=euler(-25, 0, 0))
# backpack (PLSS) with blue side panels and lights
sq("PLSS", "chest", (0, 1.33, -0.29), (0.24, 0.29, 0.13), WHITE, 0.35, 0.45)
for s in (1, -1):
    sq("PLSSPanel%d" % s, "chest", (0.235 * s, 1.33, -0.29), (0.014, 0.24, 0.095), BLUE, 0.4, 0.4)
sq("PLSSBack", "chest", (0, 1.30, -0.42), (0.17, 0.2, 0.014), GREY, 0.4, 0.4)
for i, mm in enumerate((GLOWB, GLOWO, GLOWR)):
    sq("PLSSLight%d" % i, "chest", (-0.05 + 0.05 * i, 1.08, -0.415), (0.014, 0.014, 0.01), mm)
for s in (1, -1):
    tube("Hose%d" % s, "chest", [(0.19 * s, 1.3, 0.17), (0.29 * s, 1.24, 0.0), (0.27 * s, 1.2, -0.19), (0.19 * s, 1.22, -0.27)], 0.022, DARK)

# ------------------------------------------------------------------ neck seal

ring("NeckRing", "neck", (0, 1.525, 0), (0, 1, 0), 0.14, 0.032, BLUE)
lathe("Collar", "neck", (0, 1.46, 0), (0, 1.53, 0), [(0, 0.16), (1, 0.14)], WHITE)

# ------------------------------------------------------------------ arms: puffy sleeves, bearings, bellows, gloves

for s, S in ((1, ".R"), (-1, ".L")):
    sh, el, wr, hd = J("upper_arm" + S), J("forearm" + S), J("hand" + S), J("hand" + S, 1)
    lathe("ShoulderBearing" + S, "upper_arm" + S, sh, sh + (el - sh) * 0.1, [(0, 0.115), (1, 0.115)], BLUE)
    lathe("UpperArm" + S, "upper_arm" + S, sh, el, [(0, 0.108), (0.3, 0.112), (0.7, 0.102), (1.0, 0.094)], FABRIC)
    lathe("ArmStripe" + S, "upper_arm" + S, sh + (el - sh) * 0.5, sh + (el - sh) * 0.62, [(0, 0.108), (1, 0.104)], ORANGE)
    out = Vector((s, 0.25, 0)).normalized()
    sq("Pauldron" + S, "upper_arm" + S, sh + out * 0.07 + Vector((0, 0.03, 0)), (0.15, 0.1, 0.16), WHITE, 0.55, 0.55, rot=euler(0, 0, -28 * s))
    sq("PauldronEdge" + S, "upper_arm" + S, sh + out * 0.07 + Vector((0.02 * s, -0.045, 0)), (0.14, 0.018, 0.15), BLUE, 0.5, 0.5, rot=euler(0, 0, -28 * s))
    sq("PauldronMark" + S, "upper_arm" + S, sh + out * 0.07 + Vector((0.045 * s, 0.075, 0)), (0.05, 0.012, 0.05), ORANGE, 0.6, 0.6, rot=euler(0, 0, -28 * s))
    sq("ElbowPad" + S, "upper_arm" + S, el + Vector((0.0, 0.01, -0.06)), (0.07, 0.07, 0.06), WHITE, 0.6, 0.6)
    dn = (wr - el).normalized()
    lathe("Forearm" + S, "forearm" + S, el - dn * 0.04, wr, [(0, 0.096), (0.06, 0.103), (0.12, 0.093), (0.18, 0.1), (0.26, 0.09), (0.6, 0.084), (1.0, 0.077)], FABRIC)
    lathe("Gauntlet" + S, "forearm" + S, el + (wr - el) * 0.4, wr - dn * 0.02, [(0, 0.098), (0.15, 0.104), (1.0, 0.094)], WHITE)
    lathe("GauntletBand" + S, "forearm" + S, el + (wr - el) * 0.62, el + (wr - el) * 0.7, [(0, 0.107), (1, 0.105)], ORANGE)
    lathe("WristBearing" + S, "forearm" + S, wr - dn * 0.03, wr + dn * 0.005, [(0, 0.088), (1, 0.088)], BLUE)
    hdir = (hd - wr).normalized()
    sq("Glove" + S, "hand" + S, wr + hdir * 0.06, (0.062, 0.054, 0.07), WHITE, 0.6, 0.6, rot=rot_from_to((0, 0, 1), hdir))
    d, u, v = basis(hdir)
    for i in range(4):
        f0 = wr + hdir * 0.1 + u * (-0.03 + 0.02 * i) - v * 0.01
        lathe("Finger%d" % i + S, "hand" + S, f0, f0 + hdir * 0.06 - v * 0.022, [(0, 0.015), (1, 0.014)], GREY, seg=10)
    if s < 0:   # wrist checklist / display on the left forearm
        mid = el + (wr - el) * 0.55
        sq("WristPad", "forearm.L", mid + Vector((0, 0.1, 0)), (0.05, 0.014, 0.075), WHITE, 0.4, 0.4)
        sq("WristScreen", "forearm.L", mid + Vector((0, 0.115, 0)), (0.04, 0.004, 0.06), GLOWB, 0.4, 0.4)

# ------------------------------------------------------------------ helmet: a big round bubble with an oval visor window

a, b, c = 0.25, 0.265, 0.26
WIN = (0.18, 0.165, -0.01)


def in_window(p, ax, ay, cy):
    d = p - HC
    return d.z > 0.05 and (d.x / ax) ** 2 + ((d.y - cy) / ay) ** 2 < 1.0


cut(sq("Helmet", "head", HC, (a, b, c), WHITE, seg=72, rings=48), lambda p: not in_window(p, *WIN))
cut(sq("HelmetInner", "head", HC, (a * 0.95, b * 0.95, c * 0.95), DARK, seg=48, rings=32), lambda p: not in_window(p, WIN[0] - 0.012, WIN[1] - 0.012, WIN[2]))


def on_surf(x, y, k=0.0):
    rr = 1 - (x / (a + k)) ** 2 - (y / (b + k)) ** 2
    return HC + Vector((x, y, (c + k) * math.sqrt(max(0.0, rr))))


def oval(ax, ay, cy, k, n=64):
    return [on_surf(ax * math.cos(2 * math.pi * i / n), cy + ay * math.sin(2 * math.pi * i / n), k) for i in range(n + 1)]


tube("HelmetRim", "head", oval(WIN[0] + 0.004, WIN[1] + 0.004, WIN[2], 0.008), 0.014, METAL, caps=False)
ring("HelmetBase", "head", HC + Vector((0, -0.24, 0)), (0, 1, 0), 0.15, 0.03, METAL)
for s in (1, -1):    # helmet lamps
    p = on_surf(0.19 * s, 0.1, 0.0)
    lathe("Lamp%d" % s, "head", p, p + Vector((0.02 * s, 0.01, 0.045)), [(0, 0.028), (1, 0.026)], GREY, seg=20)
    sq("LampGlow%d" % s, "head", p + Vector((0.02 * s, 0.01, 0.048)), (0.022, 0.022, 0.004), GLOWB if s > 0 else GLOWO, rot=rot_from_to((0, 0, 1), (0.4 * s, 0.2, 1)))

# ------------------------------------------------------------------ the astronaut inside: a realistic head and a comm cap

FC = HC + Vector((0, -0.022, 0.028))
HS = 1.24          # head scale: a real head fills more of the bubble


def gauss(dx, dy, sx, sy):
    return math.exp(-(dx * dx) / (2 * sx * sx) - (dy * dy) / (2 * sy * sy))


def head_point(u, v):
    """u = latitude (-pi/2..pi/2), v = longitude (0 = straight ahead)."""
    x = 0.074 * math.cos(u) * math.sin(v)
    y = 0.112 * math.sin(u)
    z = 0.098 * math.cos(u) * math.cos(v)
    if y < 0:                                     # jaw: narrower and less deep toward the chin
        k = min(1.0, -y / 0.112)
        x *= 1 - 0.32 * k * k
        z *= 1 - 0.12 * k * k if z > 0 else 1 - 0.3 * k
    front = max(0.0, z) / 0.098
    x *= 1 - 0.1 * front                          # flatter face
    bump = 0.0
    bump += 0.015 * gauss(x, y + 0.022, 0.009, 0.019) * front        # nose
    bump += 0.007 * gauss(x, y - 0.03, 0.045, 0.008) * front         # brow ridge
    bump -= 0.01 * (gauss(x - 0.032, y - 0.012, 0.016, 0.011) + gauss(x + 0.032, y - 0.012, 0.016, 0.011)) * front   # eye sockets
    bump += 0.006 * (gauss(x - 0.048, y + 0.012, 0.016, 0.016) + gauss(x + 0.048, y + 0.012, 0.016, 0.016)) * front  # cheekbones
    bump += 0.005 * gauss(x, y + 0.066, 0.022, 0.008) * front        # lips
    bump += 0.008 * gauss(x, y + 0.098, 0.022, 0.012) * front        # chin
    n = Vector((x / 0.074 ** 2, y / 0.112 ** 2, z / 0.098 ** 2)).normalized()
    return FC + (Vector((x, y, z)) + n * bump) * HS


def head_region(p):
    d = (p - FC) / HS
    if d.z < 0.04: return 0
    if abs(d.y + 0.066) < 0.007 and abs(d.x) < 0.024: return 1        # lips
    if abs(d.y - 0.035) < 0.006 and 0.012 < abs(d.x) < 0.05: return 2  # eyebrows
    return 0


rings, segs = 48, 64
verts, faces, fidx = [], [], []
for i in range(1, rings):
    u = -math.pi / 2 + math.pi * i / rings
    for j in range(segs):
        verts.append(head_point(u, -math.pi + 2 * math.pi * j / segs))
for i in range(rings - 2):
    for j in range(segs):
        j2 = (j + 1) % segs
        f = (i * segs + j, i * segs + j2, (i + 1) * segs + j2, (i + 1) * segs + j)
        faces.append(f)
        cen = sum((verts[k] for k in f), Vector()) / 4
        fidx.append(head_region(cen))
faces.append(tuple(range(segs))[::-1]); fidx.append(0)
faces.append(tuple((rings - 2) * segs + j for j in range(segs))); fidx.append(0)
make("Face", "head", verts, faces, SKIN, mats=[SKIN, LIPS, BROW], fidx=fidx)
for s in (1, -1):
    eye = FC + Vector((0.031 * s, 0.012, 0.08)) * HS
    sq("Eye%d" % s, "head", eye, (0.011 * HS, 0.0085 * HS, 0.008 * HS), EYE, seg=16, rings=10)
    lid = sq("Lid%d" % s, "head", eye + Vector((0, 0.002, 0.001)) * HS, (0.0125 * HS, 0.0105 * HS, 0.0095 * HS), SKIN, seg=16, rings=10)
    cut(lid, lambda p, e=eye: (p - e).y > 0.003 * HS)          # heavy upper eyelids: tired, half-closed blind eyes
    sq("Ear%d" % s, "head", FC + Vector((0.074 * s, 0.0, -0.005)) * HS, (0.012 * HS, 0.03 * HS, 0.02 * HS), SKIN, seg=12, rings=8)
# comm cap ("snoopy cap"): covers the top, back and ears, with ear cups and a mic
cap = sq("CommCap", "head", FC + Vector((0, 0.012, -0.006)) * HS, (0.083 * HS, 0.118 * HS, 0.106 * HS), CAP, seg=48, rings=32)
cut(cap, lambda p: not ((p - FC).z > 0.02 * HS and (p - FC).y < 0.06 * HS) and (p - FC).y > -0.065 * HS)
for s in (1, -1):
    lathe("EarCup%d" % s, "head", FC + Vector((0.078 * s, 0.0, -0.005)) * HS, FC + Vector((0.1 * s, 0.0, -0.005)) * HS, [(0, 0.03 * HS), (1, 0.028 * HS)], DARK, seg=20)
tube("Mic", "head", [FC + Vector((0.1, -0.01, 0.0)) * HS, FC + Vector((0.09, -0.06, 0.06)) * HS, FC + Vector((0.04, -0.075, 0.095)) * HS], 0.004, DARK, caps=False)

# ------------------------------------------------------------------ visor (pivot part): the glass dome, rim, scan line

g = sq("VisorGlass", "head@visor", HC, (a + 0.028, b + 0.028, c + 0.028), GLASS, seg=72, rings=48, caps=False)
cut(g, lambda p: in_window(p, WIN[0] + 0.022, WIN[1] + 0.022, WIN[2]))
tube("VisorRim", "head@visor", oval(WIN[0] + 0.022, WIN[1] + 0.022, WIN[2], 0.028), 0.015, METAL, caps=False)
line = [on_surf(x, 0.025 - 0.02 * (x / 0.15) ** 2, 0.034) for x in [(-0.15 + 0.3 * i / 30) for i in range(31)]]
tube("ScanLine", "head@visor", line, 0.004, GLOWB, caps=False)
for s in (1, -1):
    lathe("Hinge%d" % s, "head@visor", HC + Vector((0.255 * s, 0, 0)), HC + Vector((0.285 * s, 0, 0)), [(0, 0.04), (1, 0.038)], METAL, seg=24)
    tube("VisorArm%d" % s, "head@visor", [HC + Vector((0.27 * s, 0, 0)), HC + Vector((0.255 * s, -0.05, 0.12)), HC + Vector((0.2 * s, -0.1, 0.205))], 0.011, METAL, caps=False)
PIVOTS["head@visor"] = (HC, HC + Vector((1, 0, 0)))

# ------------------------------------------------------------------ LiDAR dome on the backpack (pivot part)

LP = Vector((0, 1.6, -0.27))
lathe("LidarBase", "chest@lidar", LP, LP + Vector((0, 0.035, 0)), [(0, 0.1), (1, 0.095)], BLUE)
lathe("LidarDrum", "chest@lidar", LP + Vector((0, 0.035, 0)), LP + Vector((0, 0.095, 0)), [(0, 0.075), (1, 0.07)], METAL)
sq("LidarDome", "chest@lidar", LP + Vector((0, 0.1, 0)), (0.07, 0.05, 0.07), WHITE)
sq("LidarWindow", "chest@lidar", LP + Vector((0, 0.07, 0.07)), (0.04, 0.016, 0.006), GLOWB)
PIVOTS["chest@lidar"] = (LP, LP + Vector((0, 1, 0)))

# ------------------------------------------------------------------ rotary minigun in the right hand

WR, HD = J("hand.R"), J("hand.R", 1)
GD = (HD - WR).normalized()
GBASE = WR + (HD - WR) * 0.75 + Vector((0, -0.02, 0))
GR = rot_from_to((0, 0, 1), GD)


def gx(p):  # gun space (+Z along the barrel) -> model space
    return GBASE + GR @ Vector(p)


sq("GunBody", "hand.R", (0, 0.03, 0.16), (0.085, 0.095, 0.24), GUN, 0.35, 0.4, xf=gx)
sq("GunPlate", "hand.R", (0, 0.03, 0.17), (0.089, 0.06, 0.2), WHITE, 0.3, 0.3, xf=gx)
sq("GunTop", "hand.R", (0, 0.145, 0.14), (0.03, 0.03, 0.15), BLUE, 0.4, 0.4, xf=gx)
lathe("GunHandle", "hand.R", (0, 0.13, 0.03), (0, 0.2, 0.08), [(0, 0.016), (1, 0.016)], DARK, xf=gx)
lathe("GunHandle2", "hand.R", (0, 0.2, 0.08), (0, 0.2, 0.25), [(0, 0.016), (1, 0.016)], DARK, xf=gx)
lathe("GunHandle3", "hand.R", (0, 0.2, 0.25), (0, 0.13, 0.28), [(0, 0.016), (1, 0.016)], DARK, xf=gx)
sq("GunGrip", "hand.R", (0, -0.1, 0.04), (0.04, 0.085, 0.045), DARK, 0.5, 0.5, rot=euler(-14, 0, 0), xf=gx)
sq("ForeGrip", "hand.R", (0, -0.11, 0.33), (0.035, 0.08, 0.04), DARK, 0.5, 0.5, rot=euler(12, 0, 0), xf=gx)
lathe("Drum", "hand.R", (0.09, -0.01, 0.12), (0.21, -0.01, 0.12), [(0, 0.12), (0.1, 0.135), (0.9, 0.135), (1, 0.12)], WHITE, xf=gx)
lathe("DrumBand", "hand.R", (0.14, -0.01, 0.12), (0.165, -0.01, 0.12), [(0, 0.139), (1, 0.139)], ORANGE, xf=gx)
lathe("DrumCap", "hand.R", (0.21, -0.01, 0.12), (0.225, -0.01, 0.12), [(0, 0.07), (1, 0.05)], BLUE, xf=gx)
tube("FeedBelt", "hand.R", [gx((0.12, 0.06, 0.12)), gx((0.07, 0.11, 0.16)), gx((0.02, 0.09, 0.2))], 0.022, GREY)
lathe("GunCollar", "hand.R", (0, 0.03, 0.38), (0, 0.03, 0.44), [(0, 0.095), (1, 0.088)], BLUE, xf=gx)
sq("GunLight", "hand.R", (-0.088, 0.06, 0.18), (0.004, 0.014, 0.07), GLOWB, xf=gx)
sq("GunLight2", "hand.R", (0.088, 0.06, 0.26), (0.004, 0.014, 0.03), GLOWO, xf=gx)
FG = gx((0, -0.12, 0.33))
BP = gx((0, 0.03, 0.44))
for i in range(6):
    ang = 2 * math.pi * i / 6
    o = (0.055 * math.cos(ang), 0.03 + 0.055 * math.sin(ang))
    lathe("Barrel%d" % i, "hand.R@barrels", (o[0], o[1], 0.44), (o[0], o[1], 0.86), [(0, 0.021), (1, 0.019)], METAL, seg=14, xf=gx)
lathe("BarrelCore", "hand.R@barrels", (0, 0.03, 0.44), (0, 0.03, 0.84), [(0, 0.03), (1, 0.03)], DARK, seg=14, xf=gx)
for z in (0.6, 0.8):
    lathe("Clamp%.2f" % z, "hand.R@barrels", (0, 0.03, z), (0, 0.03, z + 0.04), [(0, 0.085), (1, 0.085)], BLUE, xf=gx)
lathe("Muzzle", "hand.R@barrels", (0, 0.03, 0.84), (0, 0.03, 0.88), [(0, 0.088), (1, 0.082)], DARK, xf=gx)
PIVOTS["foregrip"] = (FG, FG + GD)
PIVOTS["hand.R@barrels"] = (BP, BP + GD)

# ------------------------------------------------------------------ export


def srgb(cl):
    cl = max(0.0, min(1.0, cl))
    return cl * 12.92 if cl <= 0.0031308 else 1.055 * cl ** (1 / 2.4) - 0.055


def export(path):
    dg = bpy.context.evaluated_depsgraph_get()
    palette, pidx, parts = [], {}, {}
    for ob in COL.objects:
        if ob.type != 'MESH' or "part" not in ob: continue
        ev = ob.evaluated_get(dg)
        me = ev.to_mesh()
        me.calc_loop_triangles()
        try:
            cn = [cc.vector.copy() for cc in me.corner_normals]
        except AttributeError:
            me.calc_normals_split(); cn = [l.normal.copy() for l in me.loops]
        M = ob.matrix_world; N3 = M.to_3x3().inverted().transposed()
        buf = parts.setdefault(ob["part"], [])
        for tri in me.loop_triangles:
            mm = me.materials[tri.material_index] if me.materials else None
            key = mm.name if mm else "none"
            if key not in pidx:
                col = mm.diffuse_color if mm else (1, 0, 1, 1)
                pidx[key] = len(palette)
                palette.append((srgb(col[0]), srgb(col[1]), srgb(col[2]), float(mm.get("emit", 0.0)) if mm else 0.0))
            pi = pidx[key]
            for li in (tri.loops[0], tri.loops[2], tri.loops[1]):
                p = M @ me.vertices[me.loops[li].vertex_index].co
                n = (N3 @ cn[li]).normalized()
                buf.append((B2U(p), B2U(n), pi))
        ev.to_mesh_clear()
    bones = dict(BONES)
    for k, (h, t) in PIVOTS.items(): bones[k] = (tuple(h), tuple(t))

    def wstr(f, s):
        bb = s.encode(); f.write(struct.pack("<i", len(bb))); f.write(bb)
    with open(path, "wb") as f:
        f.write(b"DUEL"); f.write(struct.pack("<i", 4))
        f.write(struct.pack("<i", len(bones)))
        for n, (h, t) in bones.items():
            wstr(f, n); f.write(struct.pack("<3f", *h)); f.write(struct.pack("<3f", *t))
        f.write(struct.pack("<i", len(palette)))
        for p in palette: f.write(struct.pack("<4f", *p))
        f.write(struct.pack("<i", len(parts)))
        for n, vs in parts.items():
            wstr(f, n); f.write(struct.pack("<i", len(vs)))
            f.write(b"".join(struct.pack("<6fi", *p, *nn, pi) for (p, nn, pi) in vs))
    return {k: len(v) // 3 for k, v in parts.items()}, len(palette)


# ------------------------------------------------------------------ preview renders (workbench, material colours)


def set_visor(deg):
    for ob in COL.objects:
        if ob.get("part") == "head@visor":
            if "base" not in ob: ob["base"] = 1
            ob.matrix_world = Matrix.Translation(U(HC)) @ Matrix.Rotation(math.radians(-deg), 4, U((1, 0, 0)) - U((0, 0, 0))) @ Matrix.Translation(-U(HC))


def render(path, yaw, pitch=8, dist=4.2, target=(0, 0.95, 0), lens=50, res=(900, 1100)):
    sc = bpy.context.scene
    cam = bpy.data.objects.get("SV_Cam")
    if not cam:
        cam = bpy.data.objects.new("SV_Cam", bpy.data.cameras.new("SV_Cam")); sc.collection.objects.link(cam)
    cam.data.lens = lens
    t = U(target)
    y, p = math.radians(yaw), math.radians(pitch)
    off = U((math.sin(y) * math.cos(p) * dist, math.sin(p) * dist, math.cos(y) * math.cos(p) * dist))
    cam.location = t + off
    cam.rotation_euler = (t - cam.location).to_track_quat('-Z', 'Y').to_euler()
    sc.camera = cam
    sc.render.engine = 'BLENDER_WORKBENCH'
    sh = sc.display.shading
    sh.light = 'STUDIO'; sh.color_type = 'MATERIAL'; sh.show_shadows = True; sh.show_cavity = True
    try: sh.cavity_type = 'BOTH'
    except Exception: pass
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.film_transparent = False
    if sc.world: sc.world.color = (0.02, 0.02, 0.035)
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)


stats = export(os.path.join(OUT_DIR, "surveyor_model.bin"))
render(os.path.join(OUT_DIR, "front.png"), 25)
render(os.path.join(OUT_DIR, "back.png"), 155)
render(os.path.join(OUT_DIR, "side.png"), -85)
set_visor(-105)
render(os.path.join(OUT_DIR, "face.png"), 18, 4, 1.2, (0, 1.75, 0.05), 50, (900, 900))
set_visor(0)
result = {"parts": stats[0], "palette": stats[1], "tris": sum(stats[0].values()), "out": OUT_DIR}
