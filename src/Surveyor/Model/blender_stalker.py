"""
The Shimmer Stalker in Blender: a towering, emaciated shadow horror for the Surveyor's raid boss.
Run inside Blender (Blender MCP). Builds collection "Stalker", renders previews and exports stalker_model.bin
(the same "DUEL" format the Surveyor uses) to ~/Documents/SurveyorModel. SvrStalkerModel.cs puts it on the
Imp Overlord skeleton (each piece rides the matching bone; anything unmatched rides the nearest bone).

Unity space (+Y up, +Z forward, +X = the creature's right) through U().
Look: pitch-black hide pulled over a bone cage, a violet heart burning between the ribs, a long blind skull with a
cluster of six red eyes and a jaw that splits to the chest (head@jaw hinges open when it screams), backward horns,
spine blades, arms down to the knees ending in scythe fingers, digitigrade legs with talons, a ragged shadow cloak.
"""
import bpy, bmesh, math, os, struct
from mathutils import Vector, Matrix

OUT_DIR = os.path.join(os.path.expanduser("~"), "Documents", "SurveyorModel")
os.makedirs(OUT_DIR, exist_ok=True)


def U(p):
    return Vector((-p[0], -p[2], p[1]))


def B2U(v):
    return (-v[0], v[2], -v[1])


for c in list(bpy.data.collections):
    if c.name == "Surveyor": c.hide_render = True; c.hide_viewport = True
COL = bpy.data.collections.get("Stalker")
if COL:
    for o in list(COL.objects):
        bpy.data.objects.remove(o, do_unlink=True)
else:
    COL = bpy.data.collections.new("Stalker")
    bpy.context.scene.collection.children.link(COL)
COL.hide_render = False; COL.hide_viewport = False
o = bpy.data.objects.get("Cube")
if o: bpy.data.objects.remove(o, do_unlink=True)


def mat(name, srgb, emit=0.0, rough=0.6, metal=0.0):
    lin = tuple(c ** 2.2 for c in srgb)
    m = bpy.data.materials.get("ST_" + name) or bpy.data.materials.new("ST_" + name)
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
    return m


HIDE = mat("Hide", (0.055, 0.045, 0.07))
FLESH = mat("Flesh", (0.13, 0.06, 0.1))
SINEW = mat("Sinew", (0.2, 0.08, 0.12))
BONE = mat("Bone", (0.72, 0.68, 0.6))
BONED = mat("BoneDark", (0.36, 0.32, 0.3))
TATTER = mat("Tatter", (0.07, 0.04, 0.1))
EYE = mat("Eye", (1.0, 0.12, 0.05), 4.0)
HEART = mat("Heart", (0.62, 0.16, 1.0), 3.0)
MAW = mat("Maw", (0.55, 0.03, 0.06), 1.4)
VEIN = mat("Vein", (0.5, 0.12, 0.9), 1.6)

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



def taper(name, part, pts, r0, r1, m, sides=8, **kw):
    """Tube along pts whose radius runs r0 -> r1 (a horn, claw, spike or finger)."""
    pts = [Vector(p) for p in pts]
    n = len(pts)
    rows = []
    for i, p in enumerate(pts):
        t = (pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]).normalized()
        d, u, v = basis(t)
        r = r0 + (r1 - r0) * (i / (n - 1)) ** 0.9
        rows.append([p + (u * math.cos(2 * math.pi * j / sides) + v * math.sin(2 * math.pi * j / sides)) * r for j in range(sides)])
    return grid(name, part, rows, m, **kw)


def bez(a, b, c, n=8):
    a, b, c = Vector(a), Vector(b), Vector(c)
    return [a * (1 - t) ** 2 + b * 2 * t * (1 - t) + c * t * t for t in (i / (n - 1) for i in range(n))]


def bez3(a, b, c, d, n=10):
    a, b, c, d = Vector(a), Vector(b), Vector(c), Vector(d)
    return [a * (1 - t) ** 3 + b * 3 * t * (1 - t) ** 2 + c * 3 * t * t * (1 - t) + d * t ** 3 for t in (i / (n - 1) for i in range(n))]


def spike(name, part, base, tip, r, m, sides=7):
    base, tip = Vector(base), Vector(tip)
    mid = base + (tip - base) * 0.5
    return taper(name, part, [base, mid, tip], r, 0.002, m, sides)


def ribbon(name, part, top, down, width, length, m, waves=3.0, seg=9, twist=0.0):
    """A ragged hanging strip (cloak tatter): two-sided flat quad strip with a torn, uneven bottom."""
    top = Vector(top); down = Vector(down).normalized(); side = Vector((1, 0, 0)) if abs(down.x) < 0.9 else Vector((0, 0, 1))
    side = (side - down * side.dot(down)).normalized()
    nrm = down.cross(side)
    verts, faces = [], []
    for i in range(seg):
        t = i / (seg - 1)
        w = width * (1 - 0.55 * t)
        off = nrm * (math.sin(t * math.pi * waves) * 0.05 + t * t * 0.12)
        c = top + down * (length * t) + off
        rot = side * math.cos(twist * t) + nrm * math.sin(twist * t)
        verts.append(c - rot * w); verts.append(c + rot * w)
    for i in range(seg - 1):
        a = i * 2
        faces.append((a, a + 1, a + 3, a + 2))
    return make(name, part, verts, faces, m, smooth=True)




# ------------------------------------------------------------------ skeleton: a true quadruped (dog-like), standing at rest
# z forward. Front legs hang under the shoulders, hind legs are digitigrade with a hock, a whip tail behind.

BONES = {
    "root": ((0, 0, 0), (0, 0, -0.3)),
    "hips": ((0, 1.72, -1.05), (0, 1.84, -0.25)),
    "spine": ((0, 1.84, -0.25), (0, 1.94, 0.55)),
    "chest": ((0, 1.94, 0.55), (0, 2.02, 1.15)),
    "neck": ((0, 2.02, 1.15), (0, 2.28, 1.62)),
    "head": ((0, 2.28, 1.62), (0, 2.26, 2.25)),
    "upper_arm.R": ((0.44, 1.86, 0.92), (0.5, 1.08, 1.02)),
    "forearm.R": ((0.5, 1.08, 1.02), (0.47, 0.2, 0.9)),
    "hand.R": ((0.47, 0.2, 0.9), (0.47, 0.02, 1.12)),
    "thigh.R": ((0.4, 1.68, -1.05), (0.44, 1.02, -0.66)),
    "shin.R": ((0.44, 1.02, -0.66), (0.43, 0.52, -1.1)),
    "foot.R": ((0.43, 0.52, -1.1), (0.43, 0.02, -0.92)),
}
for k in list(BONES):
    if k.endswith(".R"):
        h, t = BONES[k]
        BONES[k[:-2] + ".L"] = ((-h[0], h[1], h[2]), (-t[0], t[1], t[2]))
TAIL = [(0, 1.74, -1.25), (0, 1.62, -1.8), (0, 1.4, -2.32), (0, 1.12, -2.76), (0, 0.84, -3.12), (0, 0.6, -3.4)]
for i in range(5):
    BONES["tail%d" % i] = (TAIL[i], TAIL[i + 1])


def J(name, end=0):
    return Vector(BONES[name][end])


PIVOTS = {}

# ------------------------------------------------------------------ body: a starved hound's barrel of ribs

sq("Pelvis", "hips", (0, 1.7, -1.0), (0.32, 0.22, 0.34), HIDE, e1=0.7, e2=0.85, seg=26, rings=14)
sq("HipBoneR", "hips", (0.26, 1.82, -1.0), (0.1, 0.06, 0.2), BONED, e1=0.6, seg=14, rings=8)
sq("HipBoneL", "hips", (-0.26, 1.82, -1.0), (0.1, 0.06, 0.2), BONED, e1=0.6, seg=14, rings=8)
# tucked, starved belly running forward into the rib barrel
lathe("Waist", "spine", (0, 1.76, -0.8), (0, 1.86, 0.25), [(0, 0.24), (0.35, 0.17), (0.7, 0.18), (1.0, 0.26)], FLESH, seg=22)
sq("Barrel", "chest", (0, 1.82, 0.72), (0.36, 0.4, 0.5), HIDE, e1=0.8, e2=0.9, seg=30, rings=18)
sq("Heart", "chest", (0, 1.7, 0.75), (0.12, 0.13, 0.12), HEART, seg=16, rings=10)
for i in range(8):
    z = 0.28 + i * 0.12
    rr = 0.42 + 0.06 * math.sin(i / 7 * math.pi)
    for s in (1, -1):
        pts = []
        for k in range(10):
            a = math.radians(70 - k * 16)          # from the spine on top, round the flank, to the sternum below
            pts.append((s * math.cos(a) * rr, 1.86 + math.sin(a) * rr * 0.95, z - 0.03 * k / 9))
        taper("Rib%d%s" % (i, "R" if s > 0 else "L"), "chest" if z > 0.55 else "spine", pts, 0.024, 0.014, BONE, sides=6)
taper("Sternum", "chest", [(0, 1.36, 0.3), (0, 1.34, 0.7), (0, 1.4, 1.08)], 0.04, 0.028, BONE, sides=6)
# spine blades from the skull base to the tail root
for i in range(12):
    t = i / 11
    z = 1.05 - t * 2.15
    y = 2.0 + 0.06 * math.sin(t * math.pi) - 0.15 * t
    ln = 0.2 + 0.32 * math.sin(t * math.pi)
    bone = "chest" if z > 0.55 else ("spine" if z > -0.25 else "hips")
    spike("Blade%d" % i, bone, (0, y + 0.18, z), (0, y + 0.18 + ln, z - 0.25), 0.055, BONE)
# shoulder blades jutting through the hide
for s, S in ((1, ".R"), (-1, ".L")):
    sh = J("upper_arm" + S)
    sq("Scapula" + S, "chest", sh + Vector((-s * 0.12, 0.18, -0.12)), (0.06, 0.16, 0.26), BONED, e1=0.6, seg=14, rings=10,
       rot=Matrix.Rotation(math.radians(-s * 15), 3, 'Z'))
    for q in range(3):
        spike("ScapSpike%d%s" % (q, S), "chest", sh + Vector((-s * 0.1, 0.3, -0.05 - 0.12 * q)), sh + Vector((s * 0.18, 0.7 - 0.1 * q, -0.35 - 0.15 * q)), 0.035, BONE)
# glowing veins over the flanks
for k, s in enumerate((1, -1)):
    taper("Vein%d" % k, "chest", bez((s * 0.3, 1.6, 0.35), (s * 0.4, 1.85, 0.75), (s * 0.28, 2.05, 1.1), 8), 0.015, 0.006, VEIN, sides=5)
    taper("VeinB%d" % k, "spine", bez((s * 0.22, 1.66, -0.75), (s * 0.27, 1.8, -0.3), (s * 0.3, 1.7, 0.2), 8), 0.013, 0.006, VEIN, sides=5)

# ------------------------------------------------------------------ neck + long blind skull (reaching forward, low)

lathe("Neck", "neck", J("neck") + Vector((0, -0.05, -0.1)), J("head") + Vector((0, 0.0, 0.05)), [(0, 0.22), (0.5, 0.14), (1, 0.13)], SINEW, seg=18)
for i in range(4):
    p = J("neck") + (J("head") - J("neck")) * (i / 3)
    spike("NeckSpike%d" % i, "neck", p + Vector((0, 0.1, 0)), p + Vector((0, 0.42, -0.25)), 0.035, BONED)
HC = Vector((0, 2.32, 1.92))
sq("Skull", "head", HC, (0.13, 0.15, 0.36), HIDE, e1=0.85, e2=0.95, seg=28, rings=18,
   rot=Matrix.Rotation(math.radians(-14), 3, 'X'))
sq("Brow", "head", HC + Vector((0, 0.13, 0.12)), (0.14, 0.035, 0.16), BONED, e1=0.6, seg=20, rings=10)
sq("Crest", "head", HC + Vector((0, 0.13, -0.12)), (0.03, 0.09, 0.22), BONED, e1=0.6, seg=12, rings=10)
# six red eyes, in two uneven rows, sunk under the brow
for k, (x, y, z, r) in enumerate(((0.045, 0.05, 0.31, 0.022), (-0.05, 0.055, 0.305, 0.024), (0.095, 0.075, 0.24, 0.018),
                                  (-0.09, 0.085, 0.24, 0.02), (0.03, 0.1, 0.27, 0.014), (-0.035, 0.105, 0.265, 0.015))):
    sq("Eye%d" % k, "head", HC + Vector((x, y, z)), (r, r, r * 0.8), EYE, seg=12, rings=8)
# upper teeth along the snout
for k in range(9):
    a = -1 + k / 4
    p = HC + Vector((a * 0.12, -0.08, 0.2 + 0.12 * (1 - abs(a))))
    spike("UpperTooth%d" % k, "head", p, p + Vector((0, -0.11 - 0.05 * (1 - abs(a)), 0.02)), 0.015, BONE, sides=5)
# backward horns
for s, S in ((1, "R"), (-1, "L")):
    taper("Horn" + S, "head", bez3(HC + Vector((s * 0.12, 0.12, -0.02)), HC + Vector((s * 0.36, 0.34, -0.18)),
                                   HC + Vector((s * 0.34, 0.5, -0.62)), HC + Vector((s * 0.18, 0.38, -0.95)), 12), 0.06, 0.004, BONED, sides=8)
    taper("HornSmall" + S, "head", bez(HC + Vector((s * 0.15, 0.03, 0.02)), HC + Vector((s * 0.34, 0.08, -0.12)), HC + Vector((s * 0.32, 0.22, -0.35)), 8),
          0.03, 0.003, BONE, sides=6)

# the jaw: a hinge part (head@jaw) the game opens wide when it screams
JAW_H = HC + Vector((0, -0.07, -0.12))
PIVOTS["head@jaw"] = (tuple(JAW_H), tuple(JAW_H + Vector((1, 0, 0))))
JAW_OPEN = -22
sq("Jaw", "head@jaw", HC + Vector((0, -0.16, 0.14)), (0.13, 0.05, 0.25), BONED, e1=0.7, seg=20, rings=12,
   rot=Matrix.Rotation(math.radians(-8), 3, 'X'))
sq("Maw", "head@jaw", HC + Vector((0, -0.11, 0.14)), (0.1, 0.03, 0.2), MAW, seg=16, rings=10)
for k in range(9):
    a = -1 + k / 4
    p = HC + Vector((a * 0.1, -0.12, 0.2 + 0.12 * (1 - abs(a))))
    spike("LowerTooth%d" % k, "head@jaw", p, p + Vector((0, 0.1 + 0.04 * (1 - abs(a)), 0.02)), 0.013, BONE, sides=5)
for s in (1, -1):
    spike("Tusk%d" % s, "head@jaw", HC + Vector((s * 0.11, -0.13, 0.3)), HC + Vector((s * 0.17, 0.05, 0.44)), 0.025, BONE)



# ------------------------------------------------------------------ front legs: long, thin, elbows back, clawed paws

for s, S in ((1, ".R"), (-1, ".L")):
    a0, a1, a2, a3 = J("upper_arm" + S), J("forearm" + S), J("hand" + S), J("hand" + S, 1)
    lathe("UpperArm" + S, "upper_arm" + S, a0, a1, [(0, 0.15), (0.3, 0.12), (0.8, 0.075), (1, 0.08)], HIDE, seg=14)
    lathe("Forearm" + S, "forearm" + S, a1, a2, [(0, 0.08), (0.3, 0.065), (0.9, 0.05), (1, 0.055)], HIDE, seg=14)
    taper("ArmSinew" + S, "forearm" + S, [a1 + Vector((0, 0, 0.05)), (a1 + a2) * 0.5 + Vector((0, 0, 0.07)), a2 + Vector((0, 0, 0.04))], 0.03, 0.02, SINEW, sides=6)
    spike("ElbowSpike" + S, "upper_arm" + S, a1 + Vector((0, 0.05, -0.04)), a1 + Vector((s * 0.06, 0.28, -0.45)), 0.05, BONE)
    sq("Wrist" + S, "hand" + S, a2, (0.07, 0.07, 0.07), BONED, seg=12, rings=8)
    sq("Paw" + S, "hand" + S, a2 + Vector((0, -0.1, 0.1)), (0.11, 0.08, 0.17), HIDE, e1=0.7, seg=14, rings=10)
    for f in range(4):
        sp = (f - 1.5) * 0.06
        base = a2 + Vector((s * sp, -0.12, 0.22))
        taper("Claw%d%s" % (f, S), "hand" + S, bez(base, base + Vector((s * sp * 0.4, 0.04, 0.22)), base + Vector((s * sp * 0.8, -0.16, 0.36)), 8),
              0.026, 0.003, BONE, sides=6)
    spike("DewClaw" + S, "hand" + S, a2 + Vector((-s * 0.07, -0.05, 0.02)), a2 + Vector((-s * 0.12, -0.2, 0.2)), 0.02, BONE)

# ------------------------------------------------------------------ hind legs: a hound's (thigh forward, hock back), talons

for s, S in ((1, ".R"), (-1, ".L")):
    h, k, a, f = J("thigh" + S), J("shin" + S), J("foot" + S), J("foot" + S, 1)
    lathe("Thigh" + S, "thigh" + S, h, k, [(0, 0.2), (0.3, 0.17), (0.8, 0.1), (1, 0.09)], HIDE, seg=16)
    lathe("Shin" + S, "shin" + S, k, a, [(0, 0.09), (0.5, 0.06), (1, 0.06)], HIDE, seg=14)
    lathe("Hock" + S, "foot" + S, a, f + Vector((0, 0.04, -0.05)), [(0, 0.06), (1, 0.05)], HIDE, seg=12)
    spike("KneeSpike" + S, "thigh" + S, k, k + Vector((s * 0.04, 0.2, 0.32)), 0.05, BONE)
    spike("HockSpur" + S, "shin" + S, a, a + Vector((0, 0.12, -0.38)), 0.045, BONED)
    for t in range(3):
        sp = (t - 1) * 0.07
        base = f + Vector((s * sp, 0.03, 0.02))
        taper("Talon%d%s" % (t, S), "foot" + S, bez(base, base + Vector((s * sp, 0.03, 0.18)), base + Vector((s * sp * 1.3, -0.03, 0.3)), 7), 0.026, 0.003, BONE, sides=6)

# ------------------------------------------------------------------ the tail: a bony whip, each segment its own (physics) bone

for i in range(5):
    a, b = Vector(TAIL[i]), Vector(TAIL[i + 1])
    r0 = 0.13 * (1 - i / 6)
    r1 = 0.13 * (1 - (i + 1) / 6)
    lathe("Tail%d" % i, "tail%d" % i, a, b, [(0, r0), (0.5, (r0 + r1) * 0.55), (1, r1)], HIDE, seg=12)
    mid = (a + b) * 0.5
    spike("TailSpike%d" % i, "tail%d" % i, mid + Vector((0, r0 * 0.8, 0)), mid + Vector((0, r0 * 0.8 + 0.25 - i * 0.03, -0.15)), 0.03, BONE)
taper("TailBlade", "tail4", [Vector(TAIL[5]), Vector(TAIL[5]) + Vector((0, -0.12, -0.25)), Vector(TAIL[5]) + Vector((0, -0.32, -0.38))], 0.05, 0.003, BONE, sides=6)

# ------------------------------------------------------------------ a mane of shadow tatters (each strip swings on its own bone)

TAT = []
for i in range(9):
    t = (i - 4) / 4
    top = Vector((t * 0.36, 2.12 - 0.12 * abs(t), 1.05 - 0.35 * abs(t)))
    ln = 0.75 + 0.25 * math.sin(i * 1.9)
    name = "tatter%d" % i
    BONES[name] = (tuple(top), tuple(top + Vector((t * 0.25, -ln, -0.2))))
    TAT.append(name)
    ribbon("Tatter%d" % i, name, top, (t * 0.25, -1, -0.2), 0.07 + 0.02 * math.sin(i * 2.3), ln, TATTER, waves=2 + (i % 3), twist=0.4 * math.sin(i))
for i in range(6):
    s = 1 if i % 2 == 0 else -1
    z = -0.6 + (i // 2) * 0.45
    top = Vector((s * 0.34, 1.98, z))
    name = "tatter%d" % (9 + i)
    BONES[name] = (tuple(top), tuple(top + Vector((s * 0.15, -0.6, -0.1))))
    ribbon("TatterB%d" % i, name, top, (s * 0.25, -1, -0.15), 0.06, 0.6, TATTER, waves=2)

# ------------------------------------------------------------------ jaw hangs open at rest

def open_jaw(deg):
    piv = U(JAW_H)
    R = Matrix.Translation(piv) @ Matrix.Rotation(math.radians(deg), 4, (U((1, 0, 0)) - U((0, 0, 0))).normalized()) @ Matrix.Translation(-piv)
    for ob in COL.objects:
        if ob.get("part") == "head@jaw":
            ob.data.transform(R)
            ob.data.update()


open_jaw(JAW_OPEN)

# ------------------------------------------------------------------ export + previews


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


def render(path, yaw, pitch=6, dist=7.5, target=(0, 1.55, 0), lens=50, res=(1100, 800)):
    sc = bpy.context.scene
    cam = bpy.data.objects.get("ST_Cam")
    if not cam:
        cam = bpy.data.objects.new("ST_Cam", bpy.data.cameras.new("ST_Cam")); sc.collection.objects.link(cam)
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
    if sc.world: sc.world.color = (0.35, 0.36, 0.4)
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)


stats = export(os.path.join(OUT_DIR, "stalker_model.bin"))
render(os.path.join(OUT_DIR, "stalker_front.png"), 35, 8, 9.5, (0, 1.2, -0.4))
render(os.path.join(OUT_DIR, "stalker_side.png"), -88, 4, 10, (0, 1.2, -0.4))
render(os.path.join(OUT_DIR, "stalker_back.png"), 150, 12, 10, (0, 1.2, -0.4))
render(os.path.join(OUT_DIR, "stalker_face.png"), 20, 2, 2.4, (0, 2.25, 1.95), 50, (900, 900))
result = {"parts": stats[0], "palette": stats[1], "tris": sum(stats[0].values()), "out": OUT_DIR}
