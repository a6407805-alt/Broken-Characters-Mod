"""
Builds the Duelist's five partners as polished, original models and exports each one for the game.
Run in Blender (Scripting tab / Python console). Re-running replaces the previous build.

  Featherpuff  - a round cloud-owlet spirit: feather tufts, owl face disc, two pairs of feathered
                 wings, gold halo, sun-disk necklace relic
  Tock         - a brass pocket-watch sprite: enamel dial with ticking hands, gear wings,
                 winding crown, little arms holding an hourglass, orbiting ring relic
  Hollow Idol  - a floating stone guardian idol: striped headdress, cracked stone mask with
                 glowing hollow eyes, open chest with a soul core, detached stone hands, orbiting shards
  Jinx         - a harlequin marionette: diamond-pattern body, porcelain mask, three-point hat
                 with bells, ruffled collar, orbiting cards, gem rod relic
  Gloop        - a glossy jelly sprite: drippy teardrop body, highlights, cute face, a golden
                 balance-scale relic floating inside, orbiting droplets

Writes Documents/DuelistModels/partner_<Name>.bin (one per partner) and partners_preview.png.
Parts named wingL/wingR/wingL2/wingR2, Gear*, Hands, Halo, Orbit* are separate so the game animates them.
"""
import bpy, bmesh, math, os, struct
from mathutils import Vector, Matrix, Euler

OUT = os.path.join(os.path.expanduser("~"), "Documents", "DuelistModels")
os.makedirs(OUT, exist_ok=True)

# ------------------------------------------------------------------ cleanup
for c in list(bpy.data.collections):
    if c.name.startswith("Partner_") or c.name == "PartnerPreview":
        for o in list(c.objects):
            bpy.data.objects.remove(o, do_unlink=True)
        bpy.data.collections.remove(c)
scene = bpy.context.scene


def R(*deg):
    return Euler([math.radians(a) for a in deg]).to_matrix().to_4x4()


# ------------------------------------------------------------------ toon materials (preview look + export colours)
def toon(name, color, shade=0.45, hi=1.3, emit=0.0):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    try:
        m.use_nodes = True
    except Exception:
        pass
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    em = nt.nodes.new("ShaderNodeEmission")
    nt.links.new(em.outputs[0], out.inputs[0])
    if emit > 0:
        em.inputs[0].default_value = (*color, 1)
        em.inputs[1].default_value = emit
    else:
        dif = nt.nodes.new("ShaderNodeBsdfDiffuse")
        s2r = nt.nodes.new("ShaderNodeShaderToRGB")
        ramp = nt.nodes.new("ShaderNodeValToRGB")
        ramp.color_ramp.interpolation = "CONSTANT"
        els = ramp.color_ramp.elements
        els[0].position = 0.0
        els[0].color = (shade, shade, shade * 1.06, 1)
        els[1].position = 0.32
        els[1].color = (1, 1, 1, 1)
        els.new(0.86).color = (hi, hi, hi, 1)
        mix = nt.nodes.new("ShaderNodeMix")
        mix.data_type = "RGBA"
        mix.blend_type = "MULTIPLY"
        sock = {i.identifier: i for i in mix.inputs}
        outs = {o.identifier: o for o in mix.outputs}
        mix.inputs[0].default_value = 1.0
        sock.get("A_Color", mix.inputs[6]).default_value = (*color, 1)
        nt.links.new(dif.outputs[0], s2r.inputs[0])
        nt.links.new(s2r.outputs[0], ramp.inputs[0])
        nt.links.new(ramp.outputs[0], sock.get("B_Color", mix.inputs[7]))
        nt.links.new(outs.get("Result_Color", mix.outputs[2]), em.inputs[0])
    m.diffuse_color = (*color, 1)
    m["emit"] = float(emit)
    return m


M = {
    "fluff":   toon("PT_Fluff", (0.97, 0.94, 0.88), shade=0.68),
    "fluff2":  toon("PT_Fluff2", (0.86, 0.9, 0.98), shade=0.62),
    "sky":     toon("PT_Sky", (0.35, 0.65, 1.0), shade=0.55),
    "gold":    toon("PT_Gold", (1.0, 0.74, 0.2), shade=0.55, hi=1.6),
    "goldDk":  toon("PT_GoldDark", (0.62, 0.4, 0.1), shade=0.55),
    "brass":   toon("PT_Brass", (0.88, 0.62, 0.25), shade=0.5, hi=1.6),
    "copper":  toon("PT_Copper", (0.75, 0.38, 0.2), shade=0.5, hi=1.5),
    "enamel":  toon("PT_Enamel", (0.98, 0.96, 0.9), shade=0.75),
    "ink":     toon("PT_Ink", (0.03, 0.03, 0.05), emit=1.0),
    "white":   toon("PT_White", (1, 1, 1), shade=0.85),
    "shine":   toon("PT_Shine", (1, 1, 1), emit=3.0),
    "eyeBlue": toon("PT_EyeBlue", (0.25, 0.6, 1.0), emit=1.6),
    "eyeAmber":toon("PT_EyeAmber", (1.0, 0.7, 0.15), emit=2.5),
    "beak":    toon("PT_Beak", (1.0, 0.62, 0.22), shade=0.6),
    "teal":    toon("PT_Teal", (0.1, 0.42, 0.5), shade=0.5),
    "glass":   toon("PT_Glass", (0.75, 0.92, 1.0), shade=0.8),
    "sand":    toon("PT_Sand", (1.0, 0.82, 0.45), emit=1.2),
    "stone":   toon("PT_Stone", (0.55, 0.52, 0.6), shade=0.5),
    "stoneDk": toon("PT_StoneDark", (0.3, 0.27, 0.36), shade=0.5),
    "violet":  toon("PT_Violet", (0.45, 0.2, 0.8), shade=0.5),
    "soul":    toon("PT_Soul", (0.75, 0.35, 1.0), emit=4.0),
    "red":     toon("PT_Red", (0.85, 0.1, 0.16), shade=0.5),
    "black":   toon("PT_Black", (0.07, 0.06, 0.09), shade=0.6),
    "porcelain": toon("PT_Porcelain", (0.98, 0.95, 0.94), shade=0.78),
    "rose":    toon("PT_Rose", (1.0, 0.35, 0.45), emit=1.4),
    "gem":     toon("PT_Gem", (1.0, 0.2, 0.3), emit=3.0),
    "card":    toon("PT_Card", (0.42, 0.2, 0.1), shade=0.55),
    "cardFace":toon("PT_CardFace", (0.95, 0.9, 0.75), shade=0.7),
    "jelly":   toon("PT_Jelly", (0.35, 0.95, 0.45), shade=0.55, hi=1.25),
    "jellyDk": toon("PT_JellyDark", (0.15, 0.6, 0.25), shade=0.55),
    "jellyHi": toon("PT_JellyHi", (0.85, 1.0, 0.85), emit=1.6),
    "core":    toon("PT_Core", (0.6, 1.0, 0.5), emit=2.2),
}

# ------------------------------------------------------------------ geometry helpers
CUR = {"col": None, "partner": None}
PIVOTS = {}   # (partner, part) -> Vector pivot in partner space


def begin(partner, origin):
    col = bpy.data.collections.new("Partner_" + partner)
    scene.collection.children.link(col)
    CUR["col"] = col
    CUR["partner"] = partner
    CUR["origin"] = Vector(origin)
    return col


def pivot(part, at):
    PIVOTS[(CUR["partner"], part)] = Vector(at)


def finish(obj, part, mats, smooth=True, sub=0, bevel=0.0, solid=0.0):
    obj["part"] = part
    for m in mats:
        obj.data.materials.append(M[m])
    if smooth:
        for p in obj.data.polygons:
            p.use_smooth = True
    if solid:
        s = obj.modifiers.new("Solidify", "SOLIDIFY")
        s.thickness = solid
        s.offset = 0
    if bevel:
        b = obj.modifiers.new("Bevel", "BEVEL")
        b.width = bevel
        b.segments = 2
    if sub:
        s = obj.modifiers.new("Subsurf", "SUBSURF")
        s.levels = sub
        s.render_levels = sub
    obj.location += CUR["origin"]
    return obj


def mesh_obj(name, bm):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(name, me)
    CUR["col"].objects.link(obj)
    return obj


def sphere(name, loc, scale, m, part="body", rot=(0, 0, 0), sub=1, u=24, v=16, deform=None):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=u, v_segments=v, radius=0.5)
    if deform:
        for vert in bm.verts:
            deform(vert.co)
    bmesh.ops.scale(bm, vec=scale, verts=bm.verts)
    bmesh.ops.rotate(bm, matrix=Euler([math.radians(a) for a in rot]).to_matrix(), verts=bm.verts)
    bmesh.ops.translate(bm, vec=loc, verts=bm.verts)
    return finish(mesh_obj(name, bm), part, [m] if isinstance(m, str) else m, sub=sub)


def cube(name, loc, scale, m, part="body", rot=(0, 0, 0), bevel=0.01, sub=0):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=scale, verts=bm.verts)
    bmesh.ops.rotate(bm, matrix=Euler([math.radians(a) for a in rot]).to_matrix(), verts=bm.verts)
    bmesh.ops.translate(bm, vec=loc, verts=bm.verts)
    return finish(mesh_obj(name, bm), part, [m] if isinstance(m, str) else m, smooth=False, bevel=bevel, sub=sub)


def cone(name, p1, p2, r1, r2, m, part="body", seg=16, sub=0, smooth=True):
    p1, p2 = Vector(p1), Vector(p2)
    d = p2 - p1
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r1, radius2=max(r2, 0.0005), depth=d.length)
    bmesh.ops.translate(bm, vec=(0, 0, d.length / 2), verts=bm.verts)
    bmesh.ops.transform(bm, matrix=Matrix.Translation(p1) @ d.to_track_quat("Z", "Y").to_matrix().to_4x4(), verts=bm.verts)
    return finish(mesh_obj(name, bm), part, [m] if isinstance(m, str) else m, sub=sub, smooth=smooth)


def torus(name, loc, R_, r, m, part="body", rot=(0, 0, 0), scale=(1, 1, 1), seg=40, ring=10):
    bm = bmesh.new()
    vs = [[bm.verts.new(((R_ + r * math.cos(2 * math.pi * j / ring)) * math.cos(2 * math.pi * i / seg),
                         (R_ + r * math.cos(2 * math.pi * j / ring)) * math.sin(2 * math.pi * i / seg),
                         r * math.sin(2 * math.pi * j / ring))) for j in range(ring)] for i in range(seg)]
    for i in range(seg):
        for j in range(ring):
            bm.faces.new((vs[i][j], vs[(i + 1) % seg][j], vs[(i + 1) % seg][(j + 1) % ring], vs[i][(j + 1) % ring]))
    bmesh.ops.scale(bm, vec=scale, verts=bm.verts)
    bmesh.ops.rotate(bm, matrix=Euler([math.radians(a) for a in rot]).to_matrix(), verts=bm.verts)
    bmesh.ops.translate(bm, vec=loc, verts=bm.verts)
    return finish(mesh_obj(name, bm), part, [m] if isinstance(m, str) else m)


def lathe(name, profile, m, part="body", seg=32, sx=1.0, sy=1.0, center=(0, 0, 0), face_mat=None, sub=1, cap=True, rot=(0, 0, 0)):
    """Revolve (radius, z) around Z; closes the ends when the profile reaches radius 0."""
    bm = bmesh.new()
    rings = [[bm.verts.new((math.cos(2 * math.pi * i / seg) * r * sx, math.sin(2 * math.pi * i / seg) * r * sy, z)) for i in range(seg)]
             for (r, z) in profile]
    for j in range(len(rings) - 1):
        for i in range(seg):
            f = bm.faces.new((rings[j][i], rings[j][(i + 1) % seg], rings[j + 1][(i + 1) % seg], rings[j + 1][i]))
            if face_mat:
                f.material_index = face_mat(j, i)
    if cap and profile[0][0] > 0.001:
        bm.faces.new(list(reversed(rings[0])))
    if cap and profile[-1][0] > 0.001:
        bm.faces.new(rings[-1])
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bmesh.ops.rotate(bm, matrix=Euler([math.radians(a) for a in rot]).to_matrix(), verts=bm.verts)
    bmesh.ops.translate(bm, vec=center, verts=bm.verts)
    mats = [m] if isinstance(m, str) else m
    return finish(mesh_obj(name, bm), part, mats, sub=sub)


def blade(name, root, tip, width, m, part, curl=0.0, thick=0.012, normal=(0, -1, 0), seg=6):
    """A flat tapered feather/leaf/fin from root to tip, curling slightly."""
    root, tip = Vector(root), Vector(tip)
    n = Vector(normal).normalized()
    d = tip - root
    side = d.cross(n).normalized()
    bm = bmesh.new()
    left, right = [], []
    for k in range(seg + 1):
        t = k / seg
        c = root + d * t + n * (curl * math.sin(t * math.pi))
        w = width * math.sin(min(1.0, t * 1.25 + 0.15) * math.pi) * (1.0 - 0.3 * t)
        left.append(bm.verts.new(c - side * w * 0.5))
        right.append(bm.verts.new(c + side * w * 0.5))
    for k in range(seg):
        bm.faces.new((left[k], right[k], right[k + 1], left[k + 1]))
    obj = mesh_obj(name, bm)
    obj = finish(obj, part, [m] if isinstance(m, str) else m, solid=thick)
    return obj


def eye(prefix, at, size, iris, part="body", facing=(0, -1, 0), yaw=0.0):
    """Glossy cartoon eye: white, coloured iris, pupil, two highlights, dark rim."""
    at = Vector(at)
    f = Vector(facing).normalized()
    sphere(prefix + "_white", at, (size, size * 0.45, size * 1.15), "white", part, rot=(0, 0, yaw))
    sphere(prefix + "_iris", at + f * size * 0.18, (size * 0.72, size * 0.3, size * 0.85), iris, part, rot=(0, 0, yaw))
    sphere(prefix + "_pupil", at + f * size * 0.27, (size * 0.36, size * 0.2, size * 0.5), "ink", part, rot=(0, 0, yaw))
    sphere(prefix + "_shine", at + f * size * 0.34 + Vector((size * 0.18, 0, size * 0.22)), (size * 0.2, size * 0.1, size * 0.2), "shine", part)
    sphere(prefix + "_shine2", at + f * size * 0.33 + Vector((-size * 0.12, 0, -size * 0.2)), (size * 0.1, size * 0.06, size * 0.1), "shine", part)


def arc(name, center, radius, m, part="body", start=200, end=340, thick=0.012, squash=0.6, seg=10, depth_dir=-1):
    """A curved line (smile) in the front plane: a chain of tiny tubes along an arc."""
    c = Vector(center)
    pts = []
    for k in range(seg + 1):
        a = math.radians(start + (end - start) * k / seg)
        pts.append(c + Vector((math.cos(a) * radius, depth_dir * 0.004 * math.sin(math.pi * k / seg), math.sin(a) * radius * squash)))
    for k in range(seg):
        cone(f"{name}{k}", pts[k], pts[k + 1], thick, thick, m, part, seg=6)


def card(name, loc, rot, size, part):
    w, h, t = 0.13 * size, 0.18 * size, 0.008 * size
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=(w, t, h), verts=bm.verts)
    big = [f for f in bm.faces if abs(f.normal.y) > 0.9]
    for f in big:
        f.material_index = 1
    bmesh.ops.inset_individual(bm, faces=big, thickness=0.012 * size, use_even_offset=True)
    for f in big:
        f.material_index = 2 if f.normal.y < 0 else 0
    bmesh.ops.rotate(bm, matrix=Euler([math.radians(a) for a in rot]).to_matrix(), verts=bm.verts)
    bmesh.ops.translate(bm, vec=loc, verts=bm.verts)
    return finish(mesh_obj(name, bm), part, ["card", "gold", "cardFace"], smooth=False)


def fuzzy(seed):
    """Deterministic little bumps for organic surfaces."""
    def f(co):
        n = math.sin(co.x * 31 + seed) * math.sin(co.y * 27 + seed * 2) * math.sin(co.z * 23 + seed * 3)
        co *= 1.0 + 0.035 * n
    return f


# =================================================================== 1. FEATHERPUFF
def build_featherpuff(origin):
    begin("Featherpuff", origin)
    # round fluffy body + overlapping feather tufts
    sphere("FP_body", (0, 0, 0), (0.78, 0.74, 0.74), "fluff", deform=fuzzy(1.0), sub=2, u=32, v=20)
    for i in range(22):
        a = i * 2.39996
        zz = 0.38 * math.cos(i * 0.9) - 0.05
        r = math.sqrt(max(0.0, 0.39 ** 2 - zz * zz)) + 0.02
        base = Vector((math.cos(a) * r * 0.9, math.sin(a) * r * 0.9, zz))
        if base.y < -0.2 and abs(zz) < 0.22:   # keep the face clear
            continue
        tip = base * 1.16 + Vector((0, 0, -0.03))
        sphere(f"FP_tuft{i}", base * 1.02, (0.22, 0.22, 0.16), "fluff" if i % 3 else "fluff2", sub=1, u=12, v=8,
               rot=(math.degrees(math.atan2(base.z, 0.4)), 0, math.degrees(a)))
    # owl face disc
    sphere("FP_face", (0, -0.27, 0.03), (0.56, 0.2, 0.46), "fluff2", sub=1)
    eye("FP_eyeL", (-0.13, -0.37, 0.07), 0.18, "eyeBlue", yaw=-14)
    eye("FP_eyeR", (0.13, -0.37, 0.07), 0.18, "eyeBlue", yaw=14)
    sphere("FP_blushL", (-0.24, -0.33, -0.04), (0.08, 0.03, 0.05), "rose", sub=0)
    sphere("FP_blushR", (0.24, -0.33, -0.04), (0.08, 0.03, 0.05), "rose", sub=0)
    cone("FP_beak", (0, -0.36, -0.04), (0, -0.46, -0.1), 0.05, 0.0, "beak", seg=10, sub=1)
    # crest tufts
    blade("FP_crest1", (-0.04, -0.04, 0.33), (-0.16, -0.1, 0.56), 0.12, "fluff2", "body", curl=0.05, normal=(1, 0, 0.2))
    blade("FP_crest2", (0.04, -0.04, 0.33), (0.17, -0.08, 0.55), 0.12, "fluff", "body", curl=0.05, normal=(1, 0, -0.2))
    blade("FP_crest3", (0, 0.0, 0.35), (0.0, 0.06, 0.62), 0.13, "sky", "body", curl=0.06, normal=(1, 0, 0))
    # little gold talons
    for s in (-1, 1):
        for k in (-1, 0, 1):
            cone(f"FP_talon{s}{k}", (s * 0.12 + k * 0.03, -0.12, -0.33), (s * 0.12 + k * 0.05, -0.2, -0.43), 0.025, 0.0, "gold", seg=8)
    # wings: soft layered feathers fanning out, white with sky-blue outer feathers (pivots at the shoulders)
    def feather(nm, root, ang_deg, length, width, m, part, side, y):
        a = math.radians(ang_deg)
        d = Vector((side * math.cos(a), 0, math.sin(a)))
        c = root + d * (length * 0.5) + Vector((0, y, 0))
        theta = -ang_deg if side > 0 else 180 + ang_deg
        sphere(nm, c, (length, 0.045, width), m, part, rot=(0, theta, 0), sub=1, u=16, v=10)

    for side, sx, names in (("L", -1, ("wingL", "wingL2")), ("R", 1, ("wingR", "wingR2"))):
        root = Vector((sx * 0.32, 0.08, 0.1))
        pivot(names[0], root)
        for k in range(6):
            feather(f"FP_wing{side}{k}", root, 10 + k * 14, 0.62 - k * 0.05, 0.17, "sky" if k >= 4 else ("fluff" if k % 2 else "fluff2"), names[0], sx, 0.02 * k)
        root2 = Vector((sx * 0.3, 0.12, -0.1))
        pivot(names[1], root2)
        for k in range(4):
            feather(f"FP_wingB{side}{k}", root2, -12 - k * 15, 0.42 - k * 0.04, 0.13, "sky" if k == 3 else "fluff2", names[1], sx, 0.02 * k)
    # halo
    pivot("Halo", (0, 0, 0.78))
    torus("FP_halo", (0, 0, 0.78), 0.26, 0.022, "gold", "Halo", rot=(8, 0, 0))
    for i in range(8):
        a = i / 8 * math.tau
        sphere(f"FP_haloGem{i}", (math.cos(a) * 0.26, math.sin(a) * 0.26 * 0.99, 0.78 + math.sin(a) * 0.036), (0.04, 0.04, 0.04), "sky", "Halo", sub=0, u=10, v=8)
    # relic: sun-disk necklace
    torus("FP_chain", (0, -0.05, -0.08), 0.33, 0.012, "gold", rot=(78, 0, 0), scale=(1, 1, 1))
    sphere("FP_pendant", (0, -0.43, -0.24), (0.13, 0.04, 0.13), "gold", sub=1)
    sphere("FP_pendantGem", (0, -0.455, -0.24), (0.07, 0.02, 0.07), "sky", sub=0)
    cone("FP_pendantDrop", (0, -0.43, -0.3), (0, -0.43, -0.42), 0.035, 0.0, "gold", seg=10, sub=1)


# =================================================================== 2. TOCK
def build_tock(origin):
    begin("Tock", origin)
    # pocket-watch body facing the front (-Y)
    lathe("TK_case", [(0.0, -0.16), (0.42, -0.16), (0.47, -0.12), (0.5, -0.04), (0.5, 0.04), (0.47, 0.12), (0.42, 0.15), (0.0, 0.15)],
          "brass", seg=48, rot=(-90, 0, 0), sub=1)
    torus("TK_bezel", (0, -0.155, 0), 0.43, 0.035, "gold", rot=(90, 0, 0), seg=48)
    for i in range(36):
        a = i / 36 * math.tau
        cube(f"TK_knurl{i}", (math.cos(a) * 0.505, 0, math.sin(a) * 0.505), (0.03, 0.26, 0.02), "goldDk", rot=(0, -math.degrees(a), 0), bevel=0.004)
    lathe("TK_dial", [(0.0, -0.165), (0.4, -0.165), (0.4, -0.17), (0.0, -0.17)], "enamel", seg=48, rot=(-90, 0, 0), sub=0)
    for i in range(12):
        a = i / 12 * math.tau
        big = i % 3 == 0
        cube(f"TK_tick{i}", (math.cos(a) * 0.33, -0.18, math.sin(a) * 0.33), (0.025 if big else 0.012, 0.01, 0.08 if big else 0.05),
             "ink", rot=(0, -math.degrees(a) + 90, 0), bevel=0)
    # cute face on the dial
    eye("TK_eyeL", (-0.14, -0.2, 0.1), 0.15, "eyeAmber", facing=(0, -1, 0))
    eye("TK_eyeR", (0.14, -0.2, 0.1), 0.15, "eyeAmber", facing=(0, -1, 0))
    arc("TK_smile", (0, -0.2, -0.04), 0.08, "ink", thick=0.011)
    # hands (spin around the dial centre)
    pivot("Hands", (0, -0.2, 0))
    cube("TK_hourHand", (0, -0.2, 0.09), (0.03, 0.01, 0.18), "goldDk", "Hands", bevel=0.004)
    cube("TK_minHand", (0.12, -0.205, 0), (0.24, 0.01, 0.022), "goldDk", "Hands", bevel=0.004)
    sphere("TK_hub", (0, -0.21, 0), (0.05, 0.03, 0.05), "gold", "Hands", sub=0)
    # crown + bow on top
    lathe("TK_stem", [(0.0, 0.5), (0.06, 0.5), (0.06, 0.6), (0.09, 0.6), (0.09, 0.68), (0.0, 0.68)], "gold", seg=24, sub=0)
    torus("TK_bow", (0, 0, 0.78), 0.1, 0.022, "gold", rot=(90, 0, 0))
    # gear wings
    for side, sx, nm in (("L", -1, "GearL"), ("R", 1, "GearR")):
        c = Vector((sx * 0.62, 0.08, 0.2))
        pivot(nm, c)
        lathe(f"TK_{nm}", [(0.08, -0.025), (0.24, -0.025), (0.24, 0.025), (0.08, 0.025)], "copper", nm, seg=40, rot=(90, 0, 0),
              center=c, sub=0)
        for i in range(14):
            a = i / 14 * math.tau
            cube(f"TK_{nm}t{i}", c + Vector((math.cos(a) * 0.27, 0, math.sin(a) * 0.27)), (0.06, 0.05, 0.05), "copper", nm,
                 rot=(0, -math.degrees(a), 0), bevel=0.006)
        for i in range(4):
            a = i / 4 * math.tau
            cube(f"TK_{nm}s{i}", c + Vector((math.cos(a) * 0.12, 0, math.sin(a) * 0.12)), (0.16, 0.03, 0.03), "brass", nm,
                 rot=(0, -math.degrees(a), 0), bevel=0.004)
        c2 = Vector((sx * 0.55, 0.12, -0.18))
        nm2 = nm + "b"
        pivot(nm2, c2)
        lathe(f"TK_{nm2}", [(0.05, -0.02), (0.14, -0.02), (0.14, 0.02), (0.05, 0.02)], "brass", nm2, seg=32, rot=(90, 0, 0), center=c2, sub=0)
        for i in range(10):
            a = i / 10 * math.tau
            cube(f"TK_{nm2}t{i}", c2 + Vector((math.cos(a) * 0.16, 0, math.sin(a) * 0.16)), (0.045, 0.04, 0.04), "brass", nm2,
                 rot=(0, -math.degrees(a), 0), bevel=0.004)
    # little arms holding an hourglass below
    for s in (-1, 1):
        cone(f"TK_arm{s}", (s * 0.3, -0.05, -0.35), (s * 0.14, -0.2, -0.52), 0.035, 0.03, "brass", seg=10, sub=1)
        sphere(f"TK_mitt{s}", (s * 0.12, -0.21, -0.53), (0.09, 0.09, 0.09), "enamel", sub=1)
    hg = Vector((0, -0.25, -0.58))
    for dz in (-0.14, 0.14):
        lathe(f"TK_hgCap{dz}", [(0.0, -0.015), (0.1, -0.015), (0.1, 0.015), (0.0, 0.015)], "gold", seg=20, center=hg + Vector((0, 0, dz)), sub=0)
    for k in range(3):
        a = k / 3 * math.tau
        cone(f"TK_hgRod{k}", hg + Vector((math.cos(a) * 0.085, math.sin(a) * 0.085, -0.14)), hg + Vector((math.cos(a) * 0.085, math.sin(a) * 0.085, 0.14)),
             0.01, 0.01, "gold", seg=6)
    lathe("TK_hgGlass", [(0.0, -0.13), (0.07, -0.12), (0.075, -0.05), (0.012, 0.0), (0.075, 0.05), (0.07, 0.12), (0.0, 0.13)], "glass",
          seg=20, center=hg, sub=1)
    lathe("TK_hgSand", [(0.0, -0.12), (0.06, -0.11), (0.05, -0.06), (0.0, -0.05)], "sand", seg=16, center=hg, sub=0)
    # relic: a gold ring orbiting
    pivot("Orbit", (0, 0, 0))
    torus("TK_ring", (0.7, -0.1, 0.55), 0.09, 0.022, "gold", "Orbit", rot=(70, 20, 0))
    sphere("TK_ringGem", (0.7, -0.19, 0.58), (0.05, 0.04, 0.05), "eyeAmber", "Orbit", sub=0)


# =================================================================== 3. HOLLOW IDOL
def build_idol(origin):
    begin("HollowIdol", origin)
    # tapered stone torso, hollow chest ring with a soul core
    lathe("HI_torso", [(0.0, -0.75), (0.16, -0.72), (0.24, -0.5), (0.3, -0.2), (0.36, 0.05), (0.33, 0.18), (0.0, 0.2)],
          "stone", seg=6, sx=1.0, sy=0.7, sub=1)
    torus("HI_chestRing", (0, -0.2, -0.1), 0.16, 0.05, "gold", rot=(90, 0, 0), seg=32)
    sphere("HI_hollow", (0, -0.17, -0.1), (0.26, 0.12, 0.26), "stoneDk", sub=1)
    sphere("HI_core", (0, -0.24, -0.1), (0.17, 0.17, 0.17), "soul", sub=1)
    # head: stone mask
    sphere("HI_head", (0, 0, 0.45), (0.6, 0.56, 0.74), "stone", sub=2, deform=fuzzy(4.0))
    sphere("HI_jaw", (0, -0.07, 0.24), (0.42, 0.4, 0.26), "stone", sub=1)
    for s in (-1, 1):
        sphere(f"HI_socket{s}", (s * 0.12, -0.24, 0.48), (0.15, 0.08, 0.1), "stoneDk", sub=1)
        sphere(f"HI_glow{s}", (s * 0.12, -0.27, 0.48), (0.09, 0.04, 0.055), "soul", sub=0)
        blade(f"HI_crack{s}", (s * 0.07, -0.27, 0.57), (s * 0.2, -0.22, 0.66), 0.02, "stoneDk", "body", thick=0.01, normal=(0, -1, 0.3))
    cube("HI_nose", (0, -0.28, 0.4), (0.06, 0.07, 0.13), "stone", rot=(-10, 0, 0), bevel=0.015)
    cube("HI_mouth", (0, -0.25, 0.28), (0.16, 0.02, 0.025), "stoneDk", bevel=0.005)
    # striped headdress hugging the head, lappets falling to the shoulders (violet/gold bands)
    lathe("HI_crown", [(0.33, 0.58), (0.34, 0.66), (0.31, 0.75), (0.24, 0.83), (0.12, 0.88), (0.0, 0.89)], ["violet", "gold"], seg=40,
          sx=1.0, sy=0.95, face_mat=lambda j, i: (i // 2) % 2, sub=1)
    torus("HI_band", (0, 0, 0.6), 0.335, 0.03, "gold", scale=(1, 0.95, 1), seg=40, ring=8)
    for s in (-1, 1):
        for k in range(8):
            cube(f"HI_lappet{s}{k}", (s * 0.34, -0.02, 0.56 - k * 0.07), (0.12, 0.22, 0.065), "violet" if k % 2 == 0 else "gold",
                 rot=(0, s * 6, 0), bevel=0.01)
    # relic: eye emblem on the band
    sphere("HI_relicEye", (0, -0.33, 0.62), (0.2, 0.05, 0.1), "gold", sub=1)
    sphere("HI_relicIris", (0, -0.355, 0.62), (0.075, 0.03, 0.075), "soul", sub=0)
    for s in (-1, 1):
        cone(f"HI_relicRay{s}", (s * 0.1, -0.34, 0.62), (s * 0.24, -0.32, 0.57), 0.016, 0.0, "gold", seg=6)
    # shoulders with gold collar
    lathe("HI_collar", [(0.18, 0.16), (0.36, 0.12), (0.44, 0.06), (0.46, 0.02)], ["gold", "violet"], seg=32, face_mat=lambda j, i: (i // 2) % 2, sub=1)
    # detached stone hands (they float and bob with the body)
    for s, nm in ((-1, "handL"), (1, "handR")):
        c = Vector((s * 0.62, -0.1, -0.15))
        pivot(nm, c)
        sphere(f"HI_{nm}", c, (0.2, 0.15, 0.24), "stone", nm, sub=1, deform=fuzzy(2.0 + s))
        for f_ in range(4):
            cone(f"HI_{nm}f{f_}", c + Vector(((f_ - 1.5) * 0.045, -0.04, -0.1)), c + Vector(((f_ - 1.5) * 0.05, -0.08, -0.25)), 0.03, 0.022, "stone", nm, seg=8, sub=1)
        torus(f"HI_{nm}band", c + Vector((0, 0, 0.1)), 0.11, 0.02, "gold", nm, scale=(1, 0.8, 1), seg=24, ring=6)
    # orbiting stone shards
    pivot("Orbit", (0, 0, 0))
    for i in range(6):
        a = i / 6 * math.tau
        cube(f"HI_shard{i}", (math.cos(a) * 0.75, math.sin(a) * 0.75, -0.3 + 0.12 * math.sin(a * 2)), (0.07, 0.07, 0.16), "stoneDk" if i % 2 else "violet",
             "Orbit", rot=(25, 40 * i, 30), bevel=0.01)


# =================================================================== 4. JINX
def build_jinx(origin):
    begin("Jinx", origin)
    # harlequin body: diamond pattern (red / black) tapering into a floating tail
    lathe("JX_body", [(0.0, -0.75), (0.08, -0.62), (0.2, -0.4), (0.3, -0.15), (0.32, 0.05), (0.26, 0.18), (0.12, 0.24), (0.0, 0.25)],
          ["red", "black"], seg=16, face_mat=lambda j, i: (i + j) % 2, sub=1)
    # ruffled collar
    lathe("JX_ruff", [(0.14, 0.22), (0.42, 0.2), (0.44, 0.24), (0.15, 0.3)], "porcelain", seg=48, sub=1)
    obj = bpy.data.objects["JX_ruff"]
    for v in obj.data.vertices:
        a = math.atan2(v.co.y, v.co.x)
        r = math.hypot(v.co.x, v.co.y)
        if r > 0.3:
            v.co.z += 0.04 * math.sin(a * 12)
    # porcelain mask head
    sphere("JX_head", (0, 0, 0.5), (0.4, 0.38, 0.46), "porcelain", sub=2)
    for s in (-1, 1):
        sphere(f"JX_eyeHole{s}", (s * 0.09, -0.17, 0.54), (0.1, 0.05, 0.07), "black", sub=1, rot=(0, s * -15, 0))
        sphere(f"JX_eyeGlow{s}", (s * 0.09, -0.19, 0.54), (0.05, 0.025, 0.035), "rose", sub=0)
    blade("JX_tear", (0.09, -0.19, 0.48), (0.1, -0.18, 0.4), 0.035, "black", "body", thick=0.008)
    arc("JX_grin", (0, -0.205, 0.47), 0.09, "red", start=190, end=350, thick=0.014, squash=0.75)
    sphere("JX_cheekL", (-0.17, -0.12, 0.45), (0.07, 0.03, 0.05), "rose", sub=0)
    sphere("JX_cheekR", (0.17, -0.12, 0.45), (0.07, 0.03, 0.05), "rose", sub=0)
    # three-point jester hat with gold bells
    lathe("JX_hatCap", [(0.205, 0.6), (0.215, 0.66), (0.18, 0.72), (0.08, 0.76), (0.0, 0.77)], "black", seg=32, sub=1)
    torus("JX_hatBand", (0, 0, 0.6), 0.205, 0.022, "gold", seg=32, ring=8)
    tips = [(-0.5, 0.0, 0.9), (0.0, 0.1, 1.15), (0.5, 0.0, 0.9)]
    for k, tip in enumerate(tips):
        tip = Vector(tip)
        mid = Vector((tip.x * 0.55, 0.05, 0.85 + 0.1 * (k == 1)))
        cone(f"JX_hat{k}a", (tip.x * 0.15, 0.0, 0.68), mid, 0.16, 0.09, "red" if k != 1 else "black", seg=12, sub=1)
        end = tip + Vector((0, 0, -0.18 if k != 1 else 0.0))
        cone(f"JX_hat{k}b", mid, end, 0.09, 0.0, "red" if k != 1 else "black", seg=12, sub=1)
        sphere(f"JX_bell{k}", end + Vector((0, 0, -0.03)), (0.09, 0.09, 0.09), "gold", sub=1)
    # floating sleeves + gloves
    for s in (-1, 1):
        cone(f"JX_sleeve{s}", (s * 0.28, 0, 0.08), (s * 0.5, -0.1, -0.12), 0.08, 0.13, "black" if s < 0 else "red", seg=12, sub=1)
        sphere(f"JX_glove{s}", (s * 0.55, -0.14, -0.2), (0.11, 0.1, 0.12), "porcelain", sub=1)
    # relic: gem rod in the right hand
    cone("JX_rod", (0.57, -0.15, -0.45), (0.53, -0.13, 0.12), 0.02, 0.02, "gold", seg=10)
    sphere("JX_rodGem", (0.53, -0.13, 0.17), (0.09, 0.09, 0.12), "gem", sub=1)
    torus("JX_rodRing", (0.53, -0.13, 0.17), 0.08, 0.012, "gold", rot=(90, 0, 0), seg=20, ring=6)
    # orbiting cards
    pivot("Orbit", (0, 0, 0))
    for i in range(5):
        a = i / 5 * math.tau
        card(f"JX_card{i}", (math.cos(a) * 0.75, math.sin(a) * 0.75, 0.1 + 0.1 * math.sin(a * 2)), (10, 0, math.degrees(a) + 90), 1.0, "Orbit")


# =================================================================== 5. GLOOP
def build_gloop(origin):
    begin("Gloop", origin)
    # drippy teardrop jelly body
    prof = [(0.0, -0.42), (0.36, -0.42), (0.5, -0.33), (0.53, -0.15), (0.48, 0.08), (0.36, 0.28), (0.18, 0.44), (0.05, 0.55), (0.0, 0.58)]
    body = lathe("GL_body", prof, "jelly", seg=40, sub=2)
    for v in body.data.vertices:   # wobbly base with drips
        x, y = v.co.x, v.co.y
        a = math.atan2(y, x)
        z = v.co.z
        if z < -0.3:
            v.co.z -= 0.06 * max(0.0, math.sin(a * 5)) ** 3
        v.co.x += 0.02 * math.sin(a * 3) * (0.6 - z)
    for i in range(6):
        a = i / 6 * math.tau + 0.3
        sphere(f"GL_drip{i}", (math.cos(a) * 0.46, math.sin(a) * 0.46, -0.42 - 0.04 * (i % 2)), (0.12, 0.12, 0.09), "jellyDk", sub=1)
    # inner glow + golden scale relic floating inside
    sphere("GL_inner", (0, 0.05, -0.05), (0.62, 0.55, 0.6), "jellyDk", sub=1)
    sphere("GL_core", (0, 0.05, -0.1), (0.14, 0.14, 0.14), "core", sub=1)
    cone("GL_scalePost", (0, 0.05, -0.28), (0, 0.05, 0.05), 0.012, 0.012, "gold", seg=8)
    cube("GL_scaleBeam", (0, 0.05, 0.05), (0.32, 0.02, 0.02), "gold", bevel=0.004)
    for s in (-1, 1):
        cone(f"GL_pan{s}", (s * 0.15, 0.05, -0.04), (s * 0.15, 0.05, -0.08), 0.06, 0.07, "gold", seg=16)
    # highlights
    sphere("GL_hi1", (-0.22, -0.36, 0.25), (0.14, 0.05, 0.2), "jellyHi", sub=1, rot=(0, -25, -20))
    sphere("GL_hi2", (-0.3, -0.3, 0.08), (0.05, 0.03, 0.06), "jellyHi", sub=0)
    # face
    eye("GL_eyeL", (-0.14, -0.45, 0.1), 0.12, "ink", facing=(0, -1, 0), yaw=-10)
    eye("GL_eyeR", (0.14, -0.45, 0.1), 0.12, "ink", facing=(0, -1, 0), yaw=10)
    arc("GL_smile", (0, -0.5, 0.02), 0.08, "ink", thick=0.013)
    sphere("GL_blushL", (-0.25, -0.41, 0.0), (0.07, 0.03, 0.04), "rose", sub=0)
    sphere("GL_blushR", (0.25, -0.41, 0.0), (0.07, 0.03, 0.04), "rose", sub=0)
    # antenna droplet
    cone("GL_stalk", (0, 0, 0.55), (0.06, 0.02, 0.72), 0.04, 0.02, "jelly", seg=10, sub=1)
    sphere("GL_bulb", (0.07, 0.02, 0.77), (0.11, 0.11, 0.13), "jellyHi", sub=1)
    # orbiting droplets
    pivot("Orbit", (0, 0, 0))
    for i in range(5):
        a = i / 5 * math.tau
        sphere(f"GL_orb{i}", (math.cos(a) * 0.72, math.sin(a) * 0.72, -0.1 + 0.15 * math.sin(a * 2)), (0.09, 0.09, 0.12), "jelly", "Orbit", sub=1)


BUILDERS = [("Featherpuff", build_featherpuff), ("Tock", build_tock), ("HollowIdol", build_idol), ("Jinx", build_jinx), ("Gloop", build_gloop)]
ORIGINS = {}
for i, (name, fn) in enumerate(BUILDERS):
    o = ((i - 2) * 2.2, 0, 1.0)
    ORIGINS[name] = Vector(o)
    fn(o)

# ------------------------------------------------------------------ export each partner
def conv(v):
    return (-v[0], v[2], -v[1])


def srgb(c):
    c = max(0.0, min(1.0, c))
    return c * 12.92 if c <= 0.0031308 else 1.055 * c ** (1 / 2.4) - 0.055


def wstr(f, s):
    b = s.encode("utf-8")
    f.write(struct.pack("<i", len(b)))
    f.write(b)


dg = bpy.context.evaluated_depsgraph_get()
report = {}
for name, _ in BUILDERS:
    col = bpy.data.collections["Partner_" + name]
    origin = ORIGINS[name]
    palette, pal_index = [], {}
    parts = {}
    for obj in col.objects:
        if obj.type != "MESH":
            continue
        part = obj.get("part", "body")
        ev = obj.evaluated_get(dg)
        me = bpy.data.meshes.new_from_object(ev, preserve_all_data_layers=True, depsgraph=dg)
        me.transform(obj.matrix_world)
        bm = bmesh.new()
        bm.from_mesh(me)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        bm.to_mesh(me)
        bm.free()
        me.calc_loop_triangles()
        try:
            cn = [c.vector.copy() for c in me.corner_normals]
        except AttributeError:
            me.calc_normals_split()
            cn = [l.normal.copy() for l in me.loops]
        pv = PIVOTS.get((name, part), Vector((0, 0, 0)))
        buf = parts.setdefault(part, [])
        for tri in me.loop_triangles:
            mat = obj.data.materials[tri.material_index] if tri.material_index < len(obj.data.materials) else obj.data.materials[0]
            if mat.name not in pal_index:
                pal_index[mat.name] = len(palette)
                c = mat.diffuse_color
                palette.append((srgb(c[0]), srgb(c[1]), srgb(c[2]), float(mat.get("emit", 0.0))))
            pal = pal_index[mat.name]
            for li in tri.loops:                          # natural order: the axis mirror already flips handedness
                p = me.vertices[me.loops[li].vertex_index].co - origin
                buf.append((conv(p), conv(cn[li]), pal))
        bpy.data.meshes.remove(me)

    path = os.path.join(OUT, f"partner_{name}.bin")
    with open(path, "wb") as f:
        f.write(b"DUEL")
        f.write(struct.pack("<i", 2))
        f.write(struct.pack("<i", len(parts)))
        for part in parts:
            pv = PIVOTS.get((name, part), Vector((0, 0, 0)))
            wstr(f, part)
            f.write(struct.pack("<3f", *conv(pv)))
            f.write(struct.pack("<3f", *conv(pv + Vector((0, 0, 0.1)))))
        f.write(struct.pack("<i", len(palette)))
        for p in palette:
            f.write(struct.pack("<4f", *p))
        f.write(struct.pack("<i", len(parts)))
        for part, verts in parts.items():
            wstr(f, part)
            f.write(struct.pack("<i", len(verts)))
            for (p, n, pal) in verts:
                f.write(struct.pack("<6fi", p[0], p[1], p[2], n[0], n[1], n[2], pal))
    report[name] = {k: len(v) // 3 for k, v in parts.items()}

# ------------------------------------------------------------------ preview render
prev = bpy.data.collections.new("PartnerPreview")
scene.collection.children.link(prev)
cam_data = bpy.data.cameras.new("PT_Cam")
cam_data.lens = 35
cam = bpy.data.objects.new("PT_Camera", cam_data)
prev.objects.link(cam)
cam.location = (0.0, -15.5, 2.6)
cam.rotation_euler = (Vector((0, 0, 1.0)) - cam.location).to_track_quat("-Z", "Y").to_euler()
sun = bpy.data.objects.new("PT_Sun", bpy.data.lights.new("PT_Sun", "SUN"))
sun.data.energy = 4.0
sun.rotation_euler = (math.radians(50), math.radians(-20), math.radians(-30))
prev.objects.link(sun)
old_cam = scene.camera
scene.camera = cam
hidden = []
for o in scene.objects:            # render only the partners (no Duelist, no default cube)
    in_partner = any(c.name.startswith("Partner") for c in o.users_collection)
    if not in_partner and not o.hide_render and o.type in ("MESH", "CURVE", "EMPTY"):
        o.hide_render = True
        hidden.append(o)
scene.render.resolution_x, scene.render.resolution_y = 2000, 640
scene.render.filepath = os.path.join(OUT, "partners_preview.png")
try:
    bpy.ops.render.render(write_still=True)
finally:
    for o in hidden:
        o.hide_render = False
    scene.camera = old_cam
print("PARTNERS:", report)
result = report
