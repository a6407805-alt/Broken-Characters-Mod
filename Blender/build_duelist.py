"""
Duelist character model builder, v4 "Egyptian anime" pass (no hat, swept mane + circlet). Blender 4.x / 5.x.

Run it:  Blender > Scripting tab > Open this file > Run Script (Alt+P)
Re-running is safe: it deletes the previous Duelist first.

Original card-duelist in an anime style:
  * cel-shaded (toon) materials + black ink outlines
  * anime face: big eyes with iris/pupil/highlights, sharp brows, pointed chin
  * spiky two-tone hair (silver with blue tips) and bangs under the hat
  * wide-brim gambler hat with gold band, trim and a card in the band
  * long trench coat whose tails split and billow back, high flared collar,
    gold edging, double-breasted buttons, a big card-suit emblem on the back
  * crossed chest straps, belt chains, thigh card pouch, knee guards
  * layered shoulder plates, diamond pendant, card earring
  * revolver-style card launcher gauntlet with fins, glowing runes and loaded cards
  * a fan of cards in the right hand, cards orbiting him, summoning ring

Saves to Documents/DuelistModels: Duelist.blend, Duelist.fbx, Duelist_preview.png
View it with the viewport in Material Preview or Rendered mode (press Z) to see the toon look.
"""
import bpy
import bmesh
import math
import os
from mathutils import Vector, Matrix, Euler

OUT_DIR = os.path.join(os.path.expanduser("~"), "Documents", "DuelistModels")
os.makedirs(OUT_DIR, exist_ok=True)

# --------------------------------------------------------------- clean scene
if bpy.context.object and bpy.context.object.mode != "OBJECT":
    bpy.ops.object.mode_set(mode="OBJECT")
for c in list(bpy.data.collections):
    if c.name.startswith("Duelist"):
        for o in list(c.objects):
            bpy.data.objects.remove(o, do_unlink=True)
        bpy.data.collections.remove(c)
for o in list(bpy.data.objects):
    if o.name.startswith(("DU_", "Duelist")):
        bpy.data.objects.remove(o, do_unlink=True)

scene = bpy.context.scene
COL = bpy.data.collections.new("Duelist")
scene.collection.children.link(COL)
FX = bpy.data.collections.new("Duelist_FX")
scene.collection.children.link(FX)


def R(*deg):
    return Euler([math.radians(a) for a in deg]).to_matrix().to_4x4()


# ------------------------------------------------------- toon materials
def toon(name, color, shade=0.42, hi=1.25, emit=0.0):
    """Cel-shaded material: light is snapped into 3 flat bands (shadow / lit / highlight).
    emit > 0 makes it a flat glowing colour instead."""
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    try:
        m.use_nodes = True
    except Exception:
        pass
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    out.location = (600, 0)
    em = nt.nodes.new("ShaderNodeEmission")
    em.location = (400, 0)
    nt.links.new(em.outputs[0], out.inputs[0])
    if emit > 0:
        em.inputs[0].default_value = (*color, 1)
        em.inputs[1].default_value = emit
    else:
        dif = nt.nodes.new("ShaderNodeBsdfDiffuse")
        dif.location = (-400, 0)
        s2r = nt.nodes.new("ShaderNodeShaderToRGB")
        s2r.location = (-200, 0)
        ramp = nt.nodes.new("ShaderNodeValToRGB")
        ramp.location = (0, 0)
        ramp.color_ramp.interpolation = "CONSTANT"
        els = ramp.color_ramp.elements
        els[0].position = 0.0
        els[0].color = (shade, shade, shade * 1.08, 1)
        els[1].position = 0.32
        els[1].color = (1, 1, 1, 1)
        e3 = els.new(0.85)
        e3.color = (hi, hi, hi, 1)
        mix = nt.nodes.new("ShaderNodeMix")
        mix.location = (200, 0)
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
    return m


M = {
    "coat":    toon("DU_Coat",    (0.05, 0.11, 0.42)),          # lapis lazuli
    "coat2":   toon("DU_CoatDark",(0.03, 0.03, 0.06)),          # black
    "lining":  toon("DU_Lining",  (0.7, 0.16, 0.08)),           # carnelian
    "vest":    toon("DU_Vest",    (0.05, 0.05, 0.08)),
    "shirt":   toon("DU_Shirt",   (0.93, 0.9, 0.8), shade=0.6), # linen
    "leather": toon("DU_Leather", (0.3, 0.18, 0.09)),
    "gold":    toon("DU_Gold",    (1.0, 0.74, 0.2), shade=0.55, hi=1.5),
    "steel":   toon("DU_Steel",   (0.58, 0.62, 0.72), shade=0.5, hi=1.5),
    "turq":    toon("DU_Turquoise",(0.1, 0.75, 0.7), shade=0.55),
    "skin":    toon("DU_Skin",    (0.82, 0.58, 0.4), shade=0.7),
    "hair":    toon("DU_Hair",    (0.12, 0.1, 0.2), shade=0.55, hi=2.0),
    "hairtip": toon("DU_HairTip", (0.08, 0.8, 0.72), shade=0.5),
    "white":   toon("DU_EyeWhite",(1.0, 1.0, 1.0), shade=0.85),
    "iris":    toon("DU_Iris",    (1.0, 0.68, 0.12), emit=1.8),
    "ink":     toon("DU_Ink",     (0.02, 0.015, 0.03), emit=1.0),
    "shine":   toon("DU_Shine",   (1.0, 1.0, 1.0), emit=3.0),
    "lip":     toon("DU_Lip",     (0.55, 0.3, 0.25), shade=0.7),
    "glove":   toon("DU_Glove",   (0.93, 0.9, 0.8), shade=0.6),
    "glow":    toon("DU_Glow",    (0.2, 1.0, 0.85), emit=6.0),
    "gem":     toon("DU_Gem",     (1.0, 0.22, 0.12), emit=4.0),
    "card":    toon("DU_CardBack",(0.4, 0.2, 0.09)),
    "cardface":toon("DU_CardFace",(0.95, 0.9, 0.75), shade=0.7),
}
OUTLINE = bpy.data.materials.get("DU_Outline") or bpy.data.materials.new("DU_Outline")
try:
    OUTLINE.use_nodes = True
except Exception:
    pass
_nt = OUTLINE.node_tree
_nt.nodes.clear()
_o = _nt.nodes.new("ShaderNodeOutputMaterial")
_e = _nt.nodes.new("ShaderNodeEmission")
_e.inputs[0].default_value = (0.0, 0.0, 0.0, 1)
_nt.links.new(_e.outputs[0], _o.inputs[0])
OUTLINE.use_backface_culling = True
OUTLINE.diffuse_color = (0, 0, 0, 1)

# -------------------------------------------------------------- mesh helpers
PARTS = []   # (object, bone) -> joined into the rigged body


def link(obj, bone=None, fx=False):
    (FX if fx else COL).objects.link(obj)
    if bone:
        PARTS.append((obj, bone))
    return obj


def new_obj(name, bm, material_list, bone=None, fx=False, smooth=True):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for m in material_list:
        me.materials.append(m)
    if smooth:
        for p in me.polygons:
            p.use_smooth = True
    return link(bpy.data.objects.new(name, me), bone, fx)


def subsurf(obj, lv=1):
    if lv:
        s = obj.modifiers.new("Subsurf", "SUBSURF")
        s.levels = lv
        s.render_levels = lv
    return obj


def lathe(name, profile, seg=32, start_deg=None, sweep_deg=360.0, gap_deg=0.0, gap_dir=-90.0,
          sx=1.0, sy=1.0, mats=None, row_mat=None, bone=None, center=(0, 0), solid=0.0, sub=1, face_mat=None):
    """Revolve (radius, z) points around Z. Either a full turn, a gap (gap_deg centred on gap_dir),
    or an explicit arc (start_deg + sweep_deg). row_mat(j) -> material index for band j."""
    bm = bmesh.new()
    if start_deg is None:
        start = math.radians(gap_dir) + math.radians(gap_deg) / 2
        sweep = 2 * math.pi - math.radians(gap_deg)
        closed = gap_deg <= 0.0
    else:
        start, sweep = math.radians(start_deg), math.radians(sweep_deg)
        closed = sweep_deg >= 359.9
    n = seg if closed else seg + 1
    rings = []
    for (r, z) in profile:
        rings.append([bm.verts.new((center[0] + math.cos(start + sweep * i / seg) * r * sx,
                                    center[1] + math.sin(start + sweep * i / seg) * r * sy, z))
                      for i in range(n)])
    for j in range(len(rings) - 1):
        for i in range(seg):
            i2 = (i + 1) % n if closed else i + 1
            f = bm.faces.new((rings[j][i], rings[j][i2], rings[j + 1][i2], rings[j + 1][i]))
            if row_mat:
                f.material_index = row_mat(j)
            if face_mat:
                k = face_mat(j, i)
                if k is not None:
                    f.material_index = k
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    obj = new_obj(name, bm, mats or [M["coat"]], bone)
    if solid:
        s = obj.modifiers.new("Solidify", "SOLIDIFY")
        s.thickness = solid
        s.offset = -1
        s.material_offset = 1 if len(obj.data.materials) > 1 else 0
    return subsurf(obj, sub)


def prim(kind, name, loc, scale=(1, 1, 1), rot=(0, 0, 0), m="coat", bone=None, fx=False,
         bevel=0.0, sub=0, **kw):
    bm = bmesh.new()
    if kind == "cube":
        bmesh.ops.create_cube(bm, size=1.0)
    elif kind == "sphere":
        bmesh.ops.create_uvsphere(bm, u_segments=kw.get("u", 24), v_segments=kw.get("v", 14), radius=0.5)
    elif kind == "cyl":
        bmesh.ops.create_cone(bm, cap_ends=True, segments=kw.get("seg", 20),
                              radius1=0.5, radius2=0.5 * kw.get("taper", 1.0), depth=1.0)
    elif kind == "torus":
        Rr, r = kw.get("R", 0.5), kw.get("r", 0.08)
        seg, ring = kw.get("seg", 32), kw.get("ring", 8)
        vs = [[bm.verts.new(((Rr + r * math.cos(2 * math.pi * j / ring)) * math.cos(2 * math.pi * i / seg),
                             (Rr + r * math.cos(2 * math.pi * j / ring)) * math.sin(2 * math.pi * i / seg),
                             r * math.sin(2 * math.pi * j / ring))) for j in range(ring)] for i in range(seg)]
        for i in range(seg):
            for j in range(ring):
                bm.faces.new((vs[i][j], vs[(i + 1) % seg][j], vs[(i + 1) % seg][(j + 1) % ring], vs[i][(j + 1) % ring]))
    bmesh.ops.scale(bm, vec=scale, verts=bm.verts)
    bmesh.ops.rotate(bm, matrix=Euler([math.radians(a) for a in rot]).to_matrix(), verts=bm.verts)
    bmesh.ops.translate(bm, vec=loc, verts=bm.verts)
    mats = [M[x] for x in m] if isinstance(m, (list, tuple)) else [M[m]]
    obj = new_obj(name, bm, mats, bone, fx, smooth=kind != "cube")
    if bevel:
        b = obj.modifiers.new("Bevel", "BEVEL")
        b.width = bevel
        b.segments = 2
    return subsurf(obj, sub)


def limb(name, p1, p2, r1, r2, m="coat", bone=None, seg=16, sub=1, fx=False):
    """Tapered tube (or cone when r2 ~ 0) from p1 to p2."""
    p1, p2 = Vector(p1), Vector(p2)
    d = p2 - p1
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r1, radius2=max(r2, 0.0005), depth=d.length)
    bmesh.ops.translate(bm, vec=(0, 0, d.length / 2), verts=bm.verts)
    rot = d.to_track_quat("Z", "Y").to_matrix().to_4x4()
    bmesh.ops.transform(bm, matrix=Matrix.Translation(p1) @ rot, verts=bm.verts)
    return subsurf(new_obj(name, bm, [M[m]], bone, fx), sub)


def place(obj, mat4):
    obj.matrix_world = mat4 @ obj.matrix_world
    return obj


def card(name, loc, rot, size=1.0, bone=None, fx=False, glow=False):
    """Playing card: thin slab, card-back colour, gold border, light face on the -Y side."""
    w, h, t = 0.063 * size, 0.088 * size, 0.0025 * size
    body = prim("cube", name, (0, 0, 0), (w, t, h), m=["card", "gold", "cardface"], fx=fx)
    me = body.data
    bm = bmesh.new()
    bm.from_mesh(me)
    big = [f for f in bm.faces if abs(f.normal.y) > 0.9]
    for f in big:
        f.material_index = 1
    bmesh.ops.inset_individual(bm, faces=big, thickness=0.006 * size, use_even_offset=True)
    for f in big:
        f.material_index = 2 if f.normal.y < 0 else 0
    bm.to_mesh(me)
    bm.free()
    body.matrix_world = Matrix.Translation(loc) @ R(*rot)
    if glow:
        g = prim("cube", name + "_glow", (0, 0, 0), (w * 1.14, t * 0.5, h * 1.1), m="glow", fx=True)
        g.matrix_world = body.matrix_world.copy()
    if bone:
        PARTS.append((body, bone))
    return body


def chain(name, p0, p1, sag, n, bone, link_r=0.011):
    """Hanging chain of gold links between two points."""
    p0, p1 = Vector(p0), Vector(p1)
    for i in range(n):
        t = (i + 0.5) / n
        p = p0.lerp(p1, t)
        p.z -= sag * 4 * t * (1 - t)
        dt = 0.01
        a = p0.lerp(p1, t - dt); a.z -= sag * 4 * (t - dt) * (1 - t + dt)
        b = p0.lerp(p1, t + dt); b.z -= sag * 4 * (t + dt) * (1 - t - dt)
        d = (b - a).normalized()
        o = prim("torus", f"{name}_{i}", (0, 0, 0), (1.5, 1, 1), m="gold", bone=bone, R=link_r, r=link_r * 0.28, seg=10, ring=6)
        place(o, Matrix.Translation(p) @ d.to_track_quat("X", "Z").to_matrix().to_4x4() @ R(90 * (i % 2), 0, 0))


def strip(name, top, bottom, w_top, w_bot, rows, cols, mat_fn, mats, bone, normal_y=-1.0, bulge=0.0, solid=0.006):
    """Flat cloth panel from top centre to bottom centre; mat_fn(row, col) -> material index."""
    top, bottom = Vector(top), Vector(bottom)
    bm = bmesh.new()
    vs = []
    for r in range(rows + 1):
        t = r / rows
        c = top.lerp(bottom, t)
        w = w_top + (w_bot - w_top) * t
        row = []
        for k in range(cols + 1):
            u = k / cols - 0.5
            if normal_y == 0.0:      # panel faces sideways (X), width runs along Y
                row.append(bm.verts.new((c.x, c.y + u * w, c.z)))
            else:
                row.append(bm.verts.new((c.x + u * w, c.y + normal_y * bulge * (0.25 - u * u), c.z)))
        vs.append(row)
    for r in range(rows):
        for k in range(cols):
            f = bm.faces.new((vs[r][k], vs[r][k + 1], vs[r + 1][k + 1], vs[r + 1][k]))
            f.material_index = mat_fn(r, k)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = new_obj(name, bm, mats, bone, smooth=False)
    if solid:
        so = o.modifiers.new("Solidify", "SOLIDIFY")
        so.thickness = solid
    return o


def ankh(name, loc, size, bone, m="gold"):
    """The ankh: oval loop, crossbar, tapering stem."""
    x, y, z = loc
    prim("torus", name + "_loop", (x, y, z + 0.045 * size), (0.75, 1, 1.15), rot=(90, 0, 0), m=m, bone=bone, R=0.022 * size, r=0.006 * size, seg=20, ring=6)
    prim("cube", name + "_bar", (x, y, z + 0.016 * size), (0.07 * size, 0.012 * size, 0.013 * size), m=m, bone=bone, bevel=0.002)
    limb(name + "_stem", (x, y, z + 0.012 * size), (x, y, z - 0.06 * size), 0.007 * size, 0.012 * size, m, bone, seg=8, sub=0)


def spike(name, base, tip, r, bone="head"):
    """Anime hair spike: silver base, blue tip, sharp point."""
    base, tip = Vector(base), Vector(tip)
    mid = base.lerp(tip, 0.78)
    limb(name + "_a", base, mid, r, r * 0.3, "hair", bone, seg=10, sub=0)
    limb(name + "_b", mid, tip, r * 0.3, 0.0, "hairtip", bone, seg=10, sub=0)


# ===================================================================== LEGS
for side, x in (("R", -0.095), ("L", 0.095)):
    sgn = -1 if side == "R" else 1
    limb(f"DU_Thigh_{side}", (x, 0, 0.98), (x * 1.05, -0.01, 0.55), 0.088, 0.06, "vest", f"thigh.{side}")
    limb(f"DU_Shin_{side}", (x * 1.05, -0.01, 0.58), (x * 1.08, 0.0, 0.4), 0.058, 0.05, "vest", f"shin.{side}", sub=0, seg=16)
    # tall boots: shaft, folded cuff, foot with toe cap and heel, two gold buckled straps
    limb(f"DU_BootShaft_{side}", (x * 1.08, 0.0, 0.5), (x * 1.08, 0.0, 0.08), 0.07, 0.062, "leather", f"shin.{side}", seg=20, sub=0)
    lathe(f"DU_BootCuff_{side}", [(0.072, 0.47), (0.082, 0.5), (0.088, 0.54)], seg=20, center=(x * 1.08, 0),
          mats=[M["leather"], M["coat2"]], bone=f"shin.{side}", solid=0.006)
    prim("cube", f"DU_BootFoot_{side}", (x * 1.08, -0.06, 0.05), (0.11, 0.26, 0.1), m="leather", bone=f"foot.{side}", bevel=0.03, sub=1)
    prim("sphere", f"DU_BootToe_{side}", (x * 1.08, -0.17, 0.045), (0.1, 0.09, 0.07), m="gold", bone=f"foot.{side}")
    prim("cube", f"DU_BootSole_{side}", (x * 1.08, -0.06, 0.008), (0.118, 0.275, 0.016), m="ink", bone=f"foot.{side}", bevel=0.006)
    prim("cube", f"DU_BootHeel_{side}", (x * 1.08, 0.05, 0.03), (0.09, 0.07, 0.06), m="vest", bone=f"foot.{side}", bevel=0.008)
    for k, z in enumerate((0.2, 0.36)):
        prim("torus", f"DU_BootStrap_{side}{k}", (x * 1.08, 0, z), (1, 1, 1.8), m="leather", bone=f"shin.{side}", R=0.068, r=0.007)
        prim("cube", f"DU_BootBuckle_{side}{k}", (x * 1.08 + sgn * 0.066, -0.01, z), (0.012, 0.03, 0.026), m="gold", bone=f"shin.{side}", bevel=0.003)
    # knee guard
    prim("sphere", f"DU_Knee_{side}", (x * 1.05, -0.052, 0.57), (0.105, 0.045, 0.12), m="gold", bone=f"shin.{side}")
    prim("sphere", f"DU_KneeGem_{side}", (x * 1.05, -0.074, 0.57), (0.03, 0.012, 0.04), m="turq", bone=f"shin.{side}")
# thigh card pouch on the right leg
prim("torus", "DU_ThighStrap", (-0.1, -0.005, 0.8), (1, 1, 1.6), m="leather", bone="thigh.R", R=0.078, r=0.008)
prim("cube", "DU_ThighPouch", (-0.175, -0.01, 0.79), (0.03, 0.08, 0.11), m="leather", bone="thigh.R", bevel=0.008)
prim("cube", "DU_ThighPouchFlap", (-0.19, -0.01, 0.835), (0.012, 0.084, 0.03), m="gold", bone="thigh.R", bevel=0.003)
for i in range(3):
    card(f"DU_PouchCard_{i}", (-0.172, -0.035 + i * 0.022, 0.86), (0, 0, 90 + (i - 1) * 6), size=0.85, bone="thigh.R")

# ===================================================================== TORSO
lathe("DU_Torso", [(0.0, 0.92), (0.15, 0.93), (0.165, 1.02), (0.155, 1.12), (0.18, 1.28),
                   (0.2, 1.4), (0.15, 1.47), (0.07, 1.52), (0.0, 1.53)], seg=24, sy=0.68, mats=[M["vest"]], bone="spine")
# shirt front + cravat
lathe("DU_ShirtFront", [(0.155, 1.2), (0.18, 1.3), (0.198, 1.4), (0.15, 1.47), (0.08, 1.515)],
      start_deg=-90 - 22, sweep_deg=44, sy=0.7, mats=[M["shirt"]], bone="chest", sub=1)

# diamond pendant on a chain
prim("torus", "DU_NeckChain", (0, -0.03, 1.5), (1, 1.25, 1), rot=(22, 0, 0), m="gold", bone="chest", R=0.078, r=0.004, seg=40, ring=6)
ankh("DU_Ankh", (0, -0.14, 1.33), 1.25, "chest")
# vest buttons
for i, z in enumerate((1.3, 1.22, 1.14, 1.06)):
    prim("sphere", f"DU_Button_{i}", (0, -0.115, z), (0.016, 0.01, 0.016), m="gold", bone="spine")
# crossed chest straps (X harness) + centre buckle
for side, ang in (("R", 26), ("L", -26)):
    prim("torus", f"DU_ChestStrap_{side}", (0, 0.0, 1.24), (1, 0.72, 1), rot=(0, ang, 0), m="leather", bone="chest", R=0.188, r=0.01, seg=40, ring=6)
prim("cube", "DU_StrapBuckle", (0, -0.135, 1.24), (0.045, 0.012, 0.045), rot=(0, 45, 0), m="gold", bone="chest", bevel=0.005)
prim("cube", "DU_StrapGem", (0, -0.142, 1.24), (0.02, 0.006, 0.02), rot=(0, 45, 0), m="glow", bone="chest")
# belt, buckle, deck box, chains
prim("torus", "DU_Belt", (0, 0, 0.98), (1, 0.7, 2.2), m="gold", bone="hips", R=0.168, r=0.016, seg=40)
prim("torus", "DU_Belt2", (0, 0.0, 0.93), (1.05, 0.74, 1.6), rot=(6, 0, 0), m="coat", bone="hips", R=0.17, r=0.012, seg=40)
prim("cube", "DU_Buckle", (0, -0.122, 0.98), (0.07, 0.014, 0.05), m="gold", bone="hips", bevel=0.006)
prim("cube", "DU_BuckleGem", (0, -0.13, 0.98), (0.025, 0.006, 0.025), rot=(0, 45, 0), m="gem", bone="hips")
prim("cube", "DU_DeckBox", (0.165, -0.06, 0.92), (0.05, 0.085, 0.11), rot=(0, 0, 20), m="leather", bone="hips", bevel=0.008)
prim("cube", "DU_DeckBoxLid", (0.165, -0.06, 0.98), (0.054, 0.089, 0.02), rot=(0, 0, 20), m="gold", bone="hips", bevel=0.004)
prim("cube", "DU_DeckBoxGem", (0.185, -0.103, 0.925), (0.02, 0.006, 0.03), rot=(0, 0, 20), m="glow", bone="hips", bevel=0.003)
strip("DU_Apron", (0, -0.13, 0.95), (0, -0.2, 0.52), 0.14, 0.22, 6, 9,
      lambda r, k: 1 if r == 5 or r == 0 else (2 if k % 2 == 0 else 0),
      [M["shirt"], M["gold"], M["coat"]], "hips", bulge=0.04)
prim("sphere", "DU_ApronScarab", (0, -0.15, 0.86), (0.05, 0.016, 0.06), m="turq", bone="hips")
chain("DU_BeltChainA", (0.12, -0.1, 0.96), (0.15, 0.08, 0.95), 0.09, 13, "hips")
chain("DU_BeltChainB", (-0.13, -0.09, 0.95), (-0.12, 0.1, 0.96), 0.06, 11, "hips")

# ===================================================================== COAT
FRONT_GAP = 58.0
upper_prof = [(0.075, 1.53), (0.2, 1.49), (0.245, 1.44), (0.235, 1.33), (0.212, 1.18), (0.2, 1.05), (0.208, 0.96)]
lathe("DU_Coat", upper_prof, seg=40, gap_deg=FRONT_GAP, sy=0.74, mats=[M["coat"], M["lining"]], bone="spine", solid=0.014)
# coat tails: two panels split at the back, billowing out and back, gold hem
skirt_prof = [(0.208, 0.965), (0.235, 0.85), (0.27, 0.72), (0.31, 0.58), (0.35, 0.44), (0.385, 0.3),
              (0.4, 0.2), (0.402, 0.18)]
SLIT = 8.0
half = 180.0 - FRONT_GAP / 2 - SLIT / 2
for side, start in (("R", 90.0 + SLIT / 2), ("L", -90.0 + FRONT_GAP / 2)):
    tail = lathe(f"DU_CoatTail_{side}", skirt_prof, seg=24, start_deg=start, sweep_deg=half, sy=0.74,
                 mats=[M["coat"], M["lining"], M["gold"]],
                 row_mat=lambda j: 2 if j >= len(skirt_prof) - 2 else 0, bone="spine", solid=0.014,
                 face_mat=lambda j, i: (3 if i % 2 == 0 else 4) if j == len(skirt_prof) - 3 else None)
    tail.data.materials.append(M["turq"])
    tail.data.materials.append(M["lining"])
    for v in tail.data.vertices:
        t = max(0.0, min(1.0, (0.965 - v.co.z) / 0.785))
        ang = math.atan2(v.co.y, v.co.x)
        flare = 1 + 0.18 * t * t
        v.co.x *= flare
        v.co.y = v.co.y * flare + 0.2 * t * t                     # swept back like wind
        v.co.z += 0.035 * t * math.sin(ang * 3 + 0.6) + 0.06 * t * t * max(0.0, math.sin(ang))
# gold edging down the coat front
for side, sgn in (("R", -1), ("L", 1)):
    a = math.radians(-90 + sgn * FRONT_GAP / 2)
    pts = [Vector((math.cos(a) * r, math.sin(a) * r * 0.74, z)) for (r, z) in upper_prof[1:]]
    for (r, z) in skirt_prof[1:-1]:
        t = (0.965 - z) / 0.785
        f = 1 + 0.18 * t * t
        pts.append(Vector((math.cos(a) * r * f, math.sin(a) * r * 0.74 * f + 0.2 * t * t,
                           z + 0.035 * t * math.sin(a * 3 + 0.6))))
    for i in range(len(pts) - 1):
        limb(f"DU_CoatTrim_{side}_{i}", pts[i], pts[i + 1], 0.01, 0.01, "gold", "spine", seg=8, sub=0)
    prim("cube", f"DU_Lapel_{side}", (sgn * 0.09, -0.135, 1.38), (0.08, 0.014, 0.17),
         rot=(-12, sgn * -18, sgn * -24), m="coat2", bone="chest", bevel=0.01)
    prim("cube", f"DU_LapelTrim_{side}", (sgn * 0.093, -0.143, 1.38), (0.084, 0.004, 0.174),
         rot=(-12, sgn * -18, sgn * -24), m="gold", bone="chest")
    for i, z in enumerate((1.24, 1.12, 1.0)):
        b = math.radians(-90 + sgn * 34)
        prim("sphere", f"DU_CoatBtn_{side}{i}", (math.cos(b) * 0.218, math.sin(b) * 0.218 * 0.74 - 0.01, z),
             (0.022, 0.012, 0.022), m="gold", bone="spine")
# card-suit emblem on the back
prim("sphere", "DU_BackSun", (0, 0.178, 1.3), (0.07, 0.02, 0.07), m="gem", bone="spine")
prim("torus", "DU_BackSunRim", (0, 0.182, 1.3), (1, 1, 1), rot=(90, 0, 0), m="gold", bone="spine", R=0.037, r=0.006)
for sgn in (-1, 1):
    for k in range(4):
        prim("cube", f"DU_BackWing_{sgn}_{k}", (sgn * (0.07 + k * 0.02), 0.172 - k * 0.004, 1.305 - k * 0.012),
             (0.06, 0.006, 0.018 - k * 0.002), rot=(0, sgn * (-8 - k * 7), 0), m="gold" if k % 2 == 0 else "turq", bone="spine")
USEKH = [(0.085, 1.52), (0.12, 1.5), (0.16, 1.475), (0.2, 1.45), (0.235, 1.425), (0.265, 1.4), (0.285, 1.385)]
usekh_cols = [M["gold"], M["coat"], M["turq"], M["lining"], M["gold"], M["turq"]]
lathe("DU_Usekh", USEKH, seg=48, sy=0.78, mats=usekh_cols + [M["gold"]], row_mat=lambda j: j, bone="chest", solid=0.012, sub=1)
for i in range(36):
    a = 2 * math.pi * i / 36
    prim("sphere", f"DU_UsekhBead_{i}", (math.cos(a) * 0.29, math.sin(a) * 0.29 * 0.78, 1.372),
         (0.018, 0.018, 0.03), m="gold" if i % 2 else "lining", bone="chest", u=8, v=6)
# layered shoulder plates
for side, sgn in (("R", -1), ("L", 1)):
    b = f"upper_arm.{side}"
    prim("sphere", f"DU_Pauldron_{side}", (sgn * 0.24, 0.0, 1.43), (0.19, 0.22, 0.11), rot=(0, sgn * 18, 0), m="gold", bone=b)
    prim("sphere", f"DU_Pauldron2_{side}", (sgn * 0.27, 0.0, 1.405), (0.17, 0.2, 0.09), rot=(0, sgn * 30, 0), m="coat", bone=b)
    prim("torus", f"DU_PauldronRim_{side}", (sgn * 0.24, 0.0, 1.43), (1, 1.15, 1), rot=(0, sgn * 18, 0), m="gold", bone=b, R=0.093, r=0.009)
    prim("torus", f"DU_PauldronRim2_{side}", (sgn * 0.272, 0.0, 1.39), (1, 1.15, 1), rot=(0, sgn * 30, 0), m="gold", bone=b, R=0.083, r=0.007)
    prim("cube", f"DU_PauldronGem_{side}", (sgn * 0.285, -0.02, 1.49), (0.032, 0.032, 0.032), rot=(45, sgn * 18, 45), m="turq", bone=b)

# ===================================================================== HEAD
HC = Vector((0, -0.005, 1.665))      # head centre
limb("DU_Neck", (0, 0, 1.5), (0, -0.005, 1.6), 0.048, 0.044, "skin", "neck")
head = prim("sphere", "DU_Head", (0, 0, 0), (0.18, 0.2, 0.23), m="skin", bone="head", u=32, v=20)
for v in head.data.vertices:             # anime jaw: narrow, pointed chin, flatter face
    z = v.co.z / 0.115
    if z < 0:
        t = -z
        v.co.x *= 1 - 0.42 * t * t
        if v.co.y < 0:
            v.co.y *= 1 + 0.12 * t
    if v.co.y < -0.06 and z > -0.3:
        v.co.y *= 0.97
    v.co += HC
for side, sgn in (("R", -1), ("L", 1)):
    prim("sphere", f"DU_Ear_{side}", (sgn * 0.088, 0.005, 1.66), (0.025, 0.04, 0.06), rot=(0, 0, sgn * -15), m="skin", bone="head")
    # eye: white, violet iris, pupil, two highlights, thick upper lash line, sharp brow
    ex, ez = sgn * 0.04, 1.672
    ey = -0.093
    yaw = sgn * 22
    prim("sphere", f"DU_EyeWhite_{side}", (ex, ey, ez), (0.042, 0.01, 0.028), rot=(0, sgn * 6, yaw), m="white", bone="head")
    prim("sphere", f"DU_Iris_{side}", (ex - sgn * 0.003, ey - 0.0045, ez - 0.002), (0.022, 0.006, 0.026), rot=(0, 0, yaw), m="iris", bone="head")
    prim("sphere", f"DU_Pupil_{side}", (ex - sgn * 0.003, ey - 0.0075, ez - 0.002), (0.009, 0.004, 0.014), rot=(0, 0, yaw), m="ink", bone="head")
    prim("sphere", f"DU_Shine_{side}", (ex - sgn * 0.009, ey - 0.0095, ez + 0.006), (0.007, 0.003, 0.007), m="shine", bone="head")
    prim("sphere", f"DU_Shine2_{side}", (ex + sgn * 0.002, ey - 0.0095, ez - 0.009), (0.004, 0.003, 0.004), m="shine", bone="head")
    prim("cube", f"DU_Lash_{side}", (ex, ey - 0.004, ez + 0.0135), (0.05, 0.007, 0.006), rot=(0, sgn * -12, yaw), m="ink", bone="head", bevel=0.002)
    prim("cube", f"DU_LashFlick_{side}", (ex + sgn * 0.034, ey + 0.012, ez + 0.008), (0.034, 0.005, 0.005), rot=(0, sgn * -8, yaw + sgn * 25), m="ink", bone="head")
    prim("cube", f"DU_LowerKohl_{side}", (ex, ey - 0.002, ez - 0.0135), (0.04, 0.005, 0.0035), rot=(0, sgn * 6, yaw), m="ink", bone="head")
    prim("cube", f"DU_CheekMark_{side}", (ex - sgn * 0.004, ey + 0.002, ez - 0.03), (0.004, 0.004, 0.026), rot=(0, sgn * -12, yaw), m="hairtip", bone="head")
    prim("cube", f"DU_Brow_{side}", (ex + sgn * 0.002, ey - 0.002, ez + 0.033), (0.046, 0.007, 0.007), rot=(0, sgn * -16, yaw), m="hairtip", bone="head", bevel=0.002)
    # card earring
    prim("torus", f"DU_EarRing_{side}", (sgn * 0.094, -0.002, 1.6), (1, 1, 1), rot=(0, 90, 0), m="gold", bone="head", R=0.022, r=0.004, seg=16, ring=6)

limb("DU_Nose", (0, -0.096, 1.66), (0, -0.108, 1.637), 0.008, 0.002, "skin", "head", seg=6, sub=0)
prim("cube", "DU_NoseShade", (0.006, -0.1, 1.645), (0.004, 0.004, 0.022), rot=(-25, 0, 0), m="lip", bone="head")
prim("cube", "DU_Mouth", (0.004, -0.098, 1.612), (0.03, 0.004, 0.0035), rot=(0, -8, 0), m="ink", bone="head")
prim("cube", "DU_MouthSmirk", (0.02, -0.096, 1.615), (0.01, 0.004, 0.003), rot=(0, -35, 18), m="ink", bone="head")

# spiky hair (silver -> blue tips): bangs over the forehead, big spikes out the back and sides
for side, sgn in (("R", -1), ("L", 1)):
    strip(f"DU_Lappet_{side}", (sgn * 0.118, -0.005, 1.7), (sgn * 0.15, -0.06, 1.47), 0.05, 0.078, 10, 1,
          lambda r, k: (2 if r == 9 else r % 2), [M["gold"], M["coat"], M["gold"]], "head", solid=0.008)
    spike(f"DU_Sideburn_{side}", (sgn * 0.083, -0.04, 1.71), (sgn * 0.09, -0.065, 1.6), 0.018)
    spike(f"DU_BangOut_{side}", (sgn * 0.06, -0.06, 1.775), (sgn * 0.088, -0.1, 1.64), 0.024)
    spike(f"DU_BangIn_{side}", (sgn * 0.025, -0.075, 1.78), (sgn * 0.03, -0.107, 1.712), 0.02)
spike("DU_BangMid", (0.0, -0.07, 1.785), (-0.008, -0.112, 1.665), 0.022)

# ============================================================= HAIR TOP + CIRCLET
# hair cap over the scalp: hairline sits above the brows at the front, lower at the back
cap = lathe("DU_HairCap", [(0.098, 1.695), (0.099, 1.72), (0.096, 1.745), (0.087, 1.77), (0.065, 1.792), (0.035, 1.804), (0.0, 1.807)],
            seg=32, sy=1.12, center=(0, 0.004), mats=[M["hair"]], bone="head", sub=1)
for v in cap.data.vertices:
    if v.co.z < 1.75:
        k = (1.75 - v.co.z) / 0.055
        yn = (v.co.y - 0.004) / 0.11
        v.co.z += k * (0.032 * max(0.0, -yn) - 0.06 * max(0.0, yn))
# anime mane: thick locks rooted all over the scalp, each swept back along the head
RX, RY, RZ = 0.1, 0.112, 0.124
HCc = Vector((0, 0.0, 1.672))
locks = []
for e, step, a_lim in ((-14, 25, 75), (15, 30, 120), (38, 30, 135), (60, 36, 180), (80, 90, 180)):
    a = -a_lim
    while a <= a_lim + 0.1:
        locks.append((e, a))
        a += step
for i, (e, a) in enumerate(locks):
    er, ar = math.radians(e), math.radians(a)
    P = HCc + Vector((math.sin(ar) * math.cos(er) * RX, math.cos(ar) * math.cos(er) * RY, math.sin(er) * RZ))
    n = Vector(((P.x - HCc.x) / RX ** 2, (P.y - HCc.y) / RY ** 2, (P.z - HCc.z) / RZ ** 2)).normalized()
    back = max(0.0, math.cos(ar))                       # 1 at the back, 0 at the sides
    front = max(0.0, -math.cos(ar))                      # 1 at the front
    sweep = Vector((0.0, 1.0, 0.25 + 0.5 * front))      # back and a little up
    if e < 0:
        sweep = Vector((0.0, 0.6, -0.8))                 # nape locks hang down the neck
    d = (n * (0.5 - 0.2 * back) + sweep * (0.95 + 0.35 * back)).normalized()
    L = 0.13 + 0.12 * back + 0.05 * (e / 80.0)
    spike(f"DU_Lock_{i}", P - n * 0.025, P + d * L, 0.05 + 0.012 * back)
# a few long hero spikes for the silhouette
for i, (x, y, z) in enumerate(((0.0, 0.33, 1.86), (-0.2, 0.26, 1.83), (0.2, 0.26, 1.83), (-0.12, 0.3, 1.94), (0.12, 0.3, 1.94))):
    spike(f"DU_HeroSpike_{i}", (x * 0.3, 0.03, 1.76), (x, y, z), 0.06)
# gold circlet with a winged sun disk on the brow
prim("torus", "DU_Circlet", (0, 0.004, 1.74), (1, 1.12, 1), rot=(10, 0, 0), m="gold", bone="head", R=0.101, r=0.007, seg=40, ring=8)
prim("sphere", "DU_BrowSun", (0, -0.118, 1.726), (0.034, 0.014, 0.034), m="gem", bone="head")
prim("torus", "DU_BrowSunRim", (0, -0.12, 1.726), (1, 1, 1), rot=(90, 0, 0), m="gold", bone="head", R=0.018, r=0.004, seg=20, ring=6)
for sgn in (-1, 1):
    for k in range(4):
        prim("cube", f"DU_BrowWing_{sgn}_{k}", (sgn * (0.03 + k * 0.012), -0.113 + k * 0.006, 1.73 - k * 0.003),
             (0.03, 0.004, 0.01 - k * 0.0015), rot=(0, sgn * (-4 - k * 6), sgn * (18 + k * 8)),
             m="gold" if k % 2 == 0 else "turq", bone="head")
    prim("cube", f"DU_CircletGem_{sgn}", (sgn * 0.1, -0.03, 1.738), (0.006, 0.02, 0.02), rot=(45, 0, 0), m="turq", bone="head")

# ===================================================================== ARMS
ARMS = {
    "R": dict(sh=(-0.25, 0.0, 1.42), el=(-0.33, -0.12, 1.17), wr=(-0.26, -0.37, 1.24)),
    "L": dict(sh=(0.25, 0.0, 1.42), el=(0.34, -0.06, 1.15), wr=(0.3, -0.33, 1.16)),
}
for side, a in ARMS.items():
    sh, el, wr = (Vector(a[k]) for k in ("sh", "el", "wr"))
    limb(f"DU_UpperArm_{side}", sh, el, 0.078, 0.066, "coat", f"upper_arm.{side}")
    limb(f"DU_Forearm_{side}", el, wr, 0.067, 0.072, "coat", f"forearm.{side}")
    d = (wr - el).normalized()
    limb(f"DU_Cuff_{side}", wr - d * 0.12, wr - d * 0.004, 0.074, 0.08, "gold", f"forearm.{side}", sub=0, seg=20)
    for k in range(3):
        limb(f"DU_CuffBand_{side}{k}", wr - d * (0.1 - k * 0.035), wr - d * (0.088 - k * 0.035), 0.079, 0.081,
             "turq" if k != 1 else "lining", f"forearm.{side}", sub=0, seg=20)
    hand = wr + d * 0.07
    hb = f"hand.{side}"
    prim("sphere", f"DU_Hand_{side}", hand, (0.075, 0.1, 0.06), m="glove", bone=hb)
    prim("cube", f"DU_Knuckles_{side}", hand + d * 0.03 + Vector((0, 0, 0.024)), (0.07, 0.03, 0.014), m="steel", bone=hb, bevel=0.004)
    prim("cube", f"DU_GloveBack_{side}", hand + Vector((0, 0, 0.026)), (0.04, 0.04, 0.012), rot=(0, 0, 45), m="gold", bone=hb, bevel=0.003)
    thumb_x = 0.035 if side == "R" else -0.035
    prim("sphere", f"DU_Thumb_{side}", hand + Vector((thumb_x, -0.02, 0.0)), (0.026, 0.06, 0.026),
         rot=(0, 0, 30 if side == "R" else -30), m="glove", bone=hb)
    for f in range(4):
        base = hand + d * 0.05 + Vector(((f - 1.5) * 0.017, 0, -0.004))
        limb(f"DU_Finger_{side}{f}", base, base + d * 0.055 + Vector((0, 0, -0.012)), 0.0095, 0.008, "skin" if side == "L" else "glove", hb, seg=8)

for side, a in ARMS.items():
    sh, el_ = Vector(a["sh"]), Vector(a["el"])
    for k in range(3):
        p = sh.lerp(el_, 0.45 + k * 0.1)
        o = prim("torus", f"DU_ArmBand_{side}{k}", (0, 0, 0), (1, 1, 1), m="turq" if k == 1 else "gold",
                 R=0.076, r=0.008, seg=20, ring=6)
        o.matrix_world = Matrix.Translation(p) @ (el_ - sh).to_track_quat("Z", "Y").to_matrix().to_4x4()
        PARTS.append((o, f"upper_arm.{side}"))

# fan of five cards in the right hand
rw, re = Vector(ARMS["R"]["wr"]), Vector(ARMS["R"]["el"])
rh = rw + (rw - re).normalized() * 0.1
for i in range(5):
    card(f"DU_FanCard_{i}", rh + Vector(((i - 2) * 0.013, -0.01 - i * 0.002, 0.065)), (8, 0, (i - 2) * 15), size=1.2, bone="hand.R")

# ========================================== CARD REVOLVER GAUNTLET (left arm)
el, wr = Vector(ARMS["L"]["el"]), Vector(ARMS["L"]["wr"])
d = (wr - el).normalized()
mid = el.lerp(wr, 0.55)
basis = d.to_track_quat("Y", "Z").to_matrix().to_4x4()
ARMB = Matrix.Translation(mid) @ basis


def onarm(kind, name, local, scale, m, rot=(0, 0, 0), bevel=0.005, **kw):
    o = prim(kind, name, (0, 0, 0), scale, rot=rot, m=m, bevel=bevel if kind == "cube" else 0.0, **kw)
    place(o, ARMB @ Matrix.Translation(local))
    PARTS.append((o, "forearm.L"))
    return o


for i, y in enumerate((-0.09, 0.0, 0.09)):                 # 3 overlapping armour plates
    onarm("cube", f"DU_GPlate_{i}", (0, y, 0.07 - i * 0.003), (0.125 - i * 0.008, 0.1, 0.035), "coat", rot=(-6, 0, 0))
    onarm("cube", f"DU_GPlateTrim_{i}", (0, y + 0.05, 0.072 - i * 0.003), (0.128 - i * 0.008, 0.008, 0.038), "gold")
onarm("cube", "DU_GRail_L", (-0.055, 0.03, 0.1), (0.016, 0.32, 0.022), "gold")
onarm("cube", "DU_GRail_R", (0.055, 0.03, 0.1), (0.016, 0.32, 0.022), "gold")
onarm("cube", "DU_GTray", (0, 0.03, 0.097), (0.09, 0.3, 0.008), "vest")
onarm("cube", "DU_GRunes", (0, 0.03, 0.103), (0.012, 0.26, 0.003), "glow", bevel=0.0)
for i in range(5):
    onarm("cube", f"DU_GRune_{i}", (0, -0.08 + i * 0.05, 0.104), (0.05, 0.008, 0.003), "glow", bevel=0.0)
for side, sgn in (("a", -1), ("b", 1)):                     # side fins sweeping back
    for k in range(2):
        onarm("cube", f"DU_GFin_{side}{k}", (sgn * (0.085 + k * 0.02), -0.02 - k * 0.07, 0.06),
              (0.012, 0.13, 0.05 - k * 0.012), "coat2", rot=(0, sgn * 25, sgn * 14))
        onarm("cube", f"DU_GFinEdge_{side}{k}", (sgn * (0.09 + k * 0.02), -0.02 - k * 0.07, 0.088 - k * 0.006),
              (0.014, 0.13, 0.006), "gold", rot=(0, sgn * 25, sgn * 14), bevel=0.0)
# revolver cylinder at the back with 6 glowing card slots
onarm("cyl", "DU_GDrum", (0, -0.17, 0.085), (0.12, 0.07, 0.12), "gold", rot=(90, 0, 0), seg=24)
onarm("cyl", "DU_GDrumCap", (0, -0.207, 0.085), (0.07, 0.01, 0.07), "gold", rot=(90, 0, 0), seg=24)
onarm("sphere", "DU_GScarab", (0, -0.214, 0.085), (0.05, 0.014, 0.06), "turq")
onarm("sphere", "DU_GScarabHead", (0, -0.214, 0.122), (0.024, 0.012, 0.018), "turq")
onarm("cube", "DU_GScarabLine", (0, -0.222, 0.08), (0.003, 0.004, 0.05), "gold", bevel=0.0)
for i in range(6):
    a = 2 * math.pi * i / 6
    onarm("cube", f"DU_GSlot_{i}", (math.cos(a) * 0.05, -0.17, 0.085 + math.sin(a) * 0.05),
          (0.03, 0.074, 0.006), "glow", rot=(0, -math.degrees(a), 0), bevel=0.0)
# muzzle with prongs
onarm("cube", "DU_GMuzzle", (0, 0.19, 0.09), (0.13, 0.035, 0.055), "gold")
for sgn in (-1, 1):
    onarm("cube", f"DU_GProng_{sgn}", (sgn * 0.05, 0.235, 0.095), (0.016, 0.07, 0.03), "gold", rot=(0, 0, sgn * -10))
onarm("cube", "DU_GMuzzleGlow", (0, 0.209, 0.095), (0.07, 0.004, 0.022), "glow", bevel=0.0)
onarm("cube", "DU_GStrap1", (0, -0.08, 0.0), (0.155, 0.026, 0.155), "leather", bevel=0.02)
onarm("cube", "DU_GStrap2", (0, 0.09, 0.0), (0.155, 0.026, 0.155), "leather", bevel=0.02)
for i in range(3):
    c = card(f"DU_LoadedCard_{i}", (0, 0, 0), (90, 0, 0), size=1.05)
    place(c, ARMB @ Matrix.Translation((0, 0.03 + i * 0.055, 0.108 + i * 0.004)))
    PARTS.append((c, "forearm.L"))

# ======================================================================= RIG
arm_data = bpy.data.armatures.new("DuelistArmature")
rig = bpy.data.objects.new("DuelistRig", arm_data)
COL.objects.link(rig)
for o in bpy.context.view_layer.objects:
    o.select_set(False)
bpy.context.view_layer.objects.active = rig
rig.select_set(True)
bpy.ops.object.mode_set(mode="EDIT")
eb = arm_data.edit_bones


def bone(name, head, tail, parent=None):
    b = eb.new(name)
    b.head, b.tail = Vector(head), Vector(tail)
    if parent:
        b.parent = eb[parent]
        b.use_connect = False
    return b


bone("root", (0, 0, 0), (0, 0.2, 0))
bone("hips", (0, 0, 0.95), (0, 0, 1.05), "root")
bone("spine", (0, 0, 1.05), (0, 0, 1.28), "hips")
bone("chest", (0, 0, 1.28), (0, 0, 1.48), "spine")
bone("neck", (0, 0, 1.48), (0, 0, 1.6), "chest")
bone("head", (0, 0, 1.6), (0, 0, 1.85), "neck")
for side, a in ARMS.items():
    sh, el_, wr_ = Vector(a["sh"]), Vector(a["el"]), Vector(a["wr"])
    bone(f"upper_arm.{side}", sh, el_, "chest")
    bone(f"forearm.{side}", el_, wr_, f"upper_arm.{side}")
    bone(f"hand.{side}", wr_, wr_ + (wr_ - el_).normalized() * 0.15, f"forearm.{side}")
for side, x in (("R", -0.095), ("L", 0.095)):
    bone(f"thigh.{side}", (x, 0, 0.98), (x * 1.05, -0.01, 0.55), "hips")
    bone(f"shin.{side}", (x * 1.05, -0.01, 0.55), (x * 1.08, 0, 0.1), f"thigh.{side}")
    bone(f"foot.{side}", (x * 1.08, 0, 0.1), (x * 1.08, -0.18, 0.03), f"shin.{side}")
bpy.ops.object.mode_set(mode="OBJECT")

# bake modifiers, one 100%-weight vertex group per part, join into one skinned mesh
dg = bpy.context.evaluated_depsgraph_get()
baked = []
for obj, bname in PARTS:
    ev = obj.evaluated_get(dg)
    me = bpy.data.meshes.new_from_object(ev, preserve_all_data_layers=True, depsgraph=dg)
    me.transform(obj.matrix_world)
    nob = bpy.data.objects.new(obj.name + "_baked", me)
    COL.objects.link(nob)
    nob.vertex_groups.new(name=bname).add(list(range(len(me.vertices))), 1.0, "REPLACE")
    baked.append(nob)
for obj, _ in PARTS:
    if obj.name in bpy.data.objects:
        bpy.data.objects.remove(obj, do_unlink=True)
for o in bpy.context.view_layer.objects:
    o.select_set(False)
for o in baked:
    o.select_set(True)
bpy.context.view_layer.objects.active = baked[0]
with bpy.context.temp_override(active_object=baked[0], selected_editable_objects=baked, selected_objects=baked):
    bpy.ops.object.join()
body = bpy.context.view_layer.objects.active
body.name = "DuelistMesh"
body.data.name = "DuelistMesh"
body.parent = rig
body.modifiers.new("Armature", "ARMATURE").object = rig
# ink outline (inverted hull): only shows in the preview, not exported
body.data.materials.append(OUTLINE)
ol = body.modifiers.new("Outline", "SOLIDIFY")
ol.thickness = 0.0045
ol.offset = 1.0
ol.use_flip_normals = True
ol.use_rim = False
ol.material_offset = len(body.data.materials)

# ================================================================ FX CARDS
for i in range(8):
    a = 2 * math.pi * i / 8
    r = 0.62 + 0.06 * math.sin(i * 1.7)
    card(f"DU_OrbitCard_{i}", (math.cos(a) * r, math.sin(a) * r, 1.0 + 0.35 * math.sin(a * 2 + 0.5)),
         (90 - 70 * math.sin(a), 15 * math.cos(a * 3), math.degrees(a) + 90), size=1.6, fx=True, glow=True)
card("DU_ShotCard", (0.3, -0.85, 1.2), (90, 0, 8), size=1.3, fx=True, glow=True)
prim("torus", "DU_SummonRing", (0, 0, 0.005), (1, 1, 0.08), m="glow", fx=True, R=0.75, r=0.02, seg=64)
prim("torus", "DU_SummonRing2", (0, 0, 0.005), (1, 1, 0.08), m="gold", fx=True, R=0.6, r=0.012, seg=64)
for i in range(6):   # runes around the ring
    a = 2 * math.pi * i / 6
    prim("cube", f"DU_SummonRune_{i}", (math.cos(a) * 0.68, math.sin(a) * 0.68, 0.006), (0.06, 0.06, 0.004),
         rot=(0, 0, math.degrees(a) + 45), m="glow", fx=True)

# ======================================================== preview + export
def look_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


cam_data = bpy.data.cameras.new("DU_Cam")
cam_data.lens = 50
cam = bpy.data.objects.new("DU_Camera", cam_data)
FX.objects.link(cam)
cam.location = (1.9, -3.6, 1.55)
look_at(cam, (0, 0, 1.0))
scene.camera = cam


def light(name, kind, loc, energy, color, size=1.0):
    ld = bpy.data.lights.new(name, kind)
    ld.energy = energy
    ld.color = color
    if hasattr(ld, "size"):
        ld.size = size
    lo = bpy.data.objects.new(name, ld)
    FX.objects.link(lo)
    lo.location = loc
    look_at(lo, (0, 0, 1.0))
    return lo


light("DU_Key", "SUN", (2.5, -2.5, 3.0), 4.0, (1.0, 0.95, 0.9))
light("DU_Rim", "AREA", (-2.0, 2.5, 2.6), 300, (0.45, 0.6, 1.0), 1.5)

world = scene.world or bpy.data.worlds.new("World")
scene.world = world
try:
    world.use_nodes = True
except Exception:
    pass
bg = world.node_tree.nodes.get("Background") if world.node_tree else None
if bg:
    bg.inputs[0].default_value = (0.03, 0.02, 0.05, 1)
    bg.inputs[1].default_value = 1.0

for eng in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
    try:
        scene.render.engine = eng
        break
    except Exception:
        pass
scene.render.resolution_x = 1080
scene.render.resolution_y = 1350
try:
    scene.view_settings.view_transform = "Standard"   # flat anime colours (no filmic wash-out)
except Exception:
    pass

blend_path = os.path.join(OUT_DIR, "Duelist.blend")
fbx_path = os.path.join(OUT_DIR, "Duelist.fbx")
png_path = os.path.join(OUT_DIR, "Duelist_preview.png")

for o in bpy.context.view_layer.objects:
    o.select_set(False)
rig.select_set(True)
body.select_set(True)
bpy.context.view_layer.objects.active = rig
try:
    ol.show_render = ol.show_viewport = False
    bpy.ops.export_scene.fbx(filepath=fbx_path, use_selection=True, object_types={"ARMATURE", "MESH"},
                             use_mesh_modifiers=False, apply_scale_options="FBX_SCALE_ALL", add_leaf_bones=False,
                             bake_anim=False, axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE")
    fbx_ok = True
except Exception as e:
    fbx_ok = str(e)
finally:
    ol.show_render = ol.show_viewport = True

scene.render.filepath = png_path
try:
    bpy.ops.render.render(write_still=True)
    render_ok = True
except Exception as e:
    render_ok = str(e)

# switch any 3D view to Material Preview so the toon look shows right away
try:
    for win in bpy.context.window_manager.windows:
        for area in win.screen.areas:
            if area.type == "VIEW_3D":
                for sp in area.spaces:
                    if sp.type == "VIEW_3D":
                        sp.shading.type = "MATERIAL"
except Exception:
    pass

bpy.ops.wm.save_as_mainfile(filepath=blend_path)

result = {
    "out_dir": OUT_DIR,
    "tris": sum(len(p.vertices) - 2 for p in body.data.polygons),
    "bones": len(arm_data.bones),
    "materials": len(body.data.materials),
    "fbx": fbx_ok,
    "render": render_ok,
}
print("DUELIST BUILD:", result)
