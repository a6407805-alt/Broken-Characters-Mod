"""
Lodestar: original gravity caster model for Gravitos NEXT. Blender 4.x / 5.x.

Run: Blender > Scripting > Open > Run Script (re-running replaces the previous build).
Writes to Documents/LodestarModels:
  lodestar_model.bin  rigid body parts, one per bone (DUEL v1, same as the Duelist body)
  lodestar_fx.bin     floating parts with pivots: Ring0..2 (orbit the torso), ShardsL/ShardsR (hover at the hands) (DUEL v2)
  Lodestar.blend, Lodestar_preview.png

Design (original): slim armored caster that floats with legs trailing, smooth faceless dome with one
vertical violet slit, three dark iron rings orbiting the torso, magnetic shards hovering around the hands.
Gunmetal + charcoal, violet emissive seams, thin cyan accents. Faces -Y (Unity +Z).
"""
import bpy, bmesh, math, os, struct
from mathutils import Vector, Matrix, Euler

OUT_DIR = os.path.join(os.path.expanduser("~"), "Documents", "LodestarModels")
os.makedirs(OUT_DIR, exist_ok=True)

# ------------------------------------------------------------------ clean
if bpy.context.object and bpy.context.object.mode != "OBJECT":
    bpy.ops.object.mode_set(mode="OBJECT")
for o in list(bpy.data.objects):
    if o.name.startswith(("LS_", "Lodestar")):
        bpy.data.objects.remove(o, do_unlink=True)
for c in list(bpy.data.collections):
    if c.name.startswith("Lodestar"):
        bpy.data.collections.remove(c)
scene = bpy.context.scene
COL = bpy.data.collections.new("Lodestar")
FX = bpy.data.collections.new("Lodestar_FX")
scene.collection.children.link(COL)
scene.collection.children.link(FX)


# -------------------------------------------------------------- materials
def toon(name, color, shade=0.42, hi=1.3, emit=0.0):
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
        els[0].position, els[0].color = 0.0, (shade, shade, shade * 1.1, 1)
        els[1].position, els[1].color = 0.32, (1, 1, 1, 1)
        els.new(0.85).color = (hi, hi, hi, 1)
        mix = nt.nodes.new("ShaderNodeMix")
        mix.data_type, mix.blend_type = "RGBA", "MULTIPLY"
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
    "metal":  toon("LS_Gunmetal", (0.16, 0.17, 0.21), shade=0.5, hi=1.6),
    "dark":   toon("LS_Charcoal", (0.055, 0.055, 0.07)),
    "plate":  toon("LS_Plate",    (0.26, 0.27, 0.32), shade=0.5, hi=1.6),
    "cloth":  toon("LS_Cloth",    (0.11, 0.06, 0.2)),
    "iron":   toon("LS_Iron",     (0.2, 0.18, 0.2), shade=0.45, hi=1.5),
    "dome":   toon("LS_Dome",     (0.33, 0.34, 0.4), shade=0.45, hi=1.8),
    "violet": toon("LS_Violet",   (0.42, 0.3, 1.0), emit=1.4),
    "cyan":   toon("LS_Cyan",     (0.25, 0.9, 1.0), emit=1.2),
    "shard":  toon("LS_Shard",    (0.5, 0.4, 1.0), emit=1.1),
}
OUTLINE = bpy.data.materials.get("LS_Outline") or bpy.data.materials.new("LS_Outline")
try:
    OUTLINE.use_nodes = True
except Exception:
    pass
_nt = OUTLINE.node_tree
_nt.nodes.clear()
_e = _nt.nodes.new("ShaderNodeEmission")
_e.inputs[0].default_value = (0, 0, 0, 1)
_nt.links.new(_e.outputs[0], _nt.nodes.new("ShaderNodeOutputMaterial").inputs[0])
OUTLINE.use_backface_culling = True
OUTLINE.diffuse_color = (0, 0, 0, 1)

# ----------------------------------------------------------------- helpers
PARTS = []      # (obj, bone)   -> rigged body
FXPARTS = []    # (obj, part)   -> lodestar_fx.bin


def new_obj(name, bm, mats, bone=None, fx=None, smooth=True):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for m in mats:
        me.materials.append(m)
    for p in me.polygons:
        p.use_smooth = smooth
    o = bpy.data.objects.new(name, me)
    (FX if fx else COL).objects.link(o)
    if bone:
        PARTS.append((o, bone))
    if fx:
        FXPARTS.append((o, fx))
    return o


def subsurf(o, lv=1):
    if lv:
        s = o.modifiers.new("Sub", "SUBSURF")
        s.levels = s.render_levels = lv
    return o


def mats_of(m):
    return [M[x] for x in m] if isinstance(m, (list, tuple)) else [M[m]]


def prim(kind, name, loc, scale=(1, 1, 1), rot=(0, 0, 0), m="metal", bone=None, fx=None, bevel=0.0, sub=0, **kw):
    bm = bmesh.new()
    if kind == "cube":
        bmesh.ops.create_cube(bm, size=1.0)
    elif kind == "sphere":
        bmesh.ops.create_uvsphere(bm, u_segments=kw.get("u", 24), v_segments=kw.get("v", 14), radius=0.5)
    elif kind == "cyl":
        bmesh.ops.create_cone(bm, cap_ends=True, segments=kw.get("seg", 20), radius1=0.5,
                              radius2=0.5 * kw.get("taper", 1.0), depth=1.0)
    elif kind == "oct":   # elongated crystal
        bmesh.ops.create_cone(bm, cap_ends=True, segments=kw.get("seg", 5), radius1=0.5, radius2=0.0005, depth=0.5)
        bmesh.ops.translate(bm, vec=(0, 0, 0.25), verts=bm.verts)
        bot = bmesh.new()
        bmesh.ops.create_cone(bot, cap_ends=True, segments=kw.get("seg", 5), radius1=0.0005, radius2=0.5, depth=0.5)
        bmesh.ops.translate(bot, vec=(0, 0, -0.25), verts=bot.verts)
        tmp = bpy.data.meshes.new("tmp"); bot.to_mesh(tmp); bot.free(); bm.from_mesh(tmp); bpy.data.meshes.remove(tmp)
    elif kind == "torus":
        Rr, r, seg, ring = kw.get("R", 0.5), kw.get("r", 0.08), kw.get("seg", 40), kw.get("ring", 8)
        sq = kw.get("square", 1.0)    # >1 flattens the tube into a band
        vs = [[bm.verts.new(((Rr + r * math.cos(2 * math.pi * j / ring)) * math.cos(2 * math.pi * i / seg),
                             (Rr + r * math.cos(2 * math.pi * j / ring)) * math.sin(2 * math.pi * i / seg),
                             r * sq * math.sin(2 * math.pi * j / ring))) for j in range(ring)] for i in range(seg)]
        for i in range(seg):
            for j in range(ring):
                bm.faces.new((vs[i][j], vs[(i + 1) % seg][j], vs[(i + 1) % seg][(j + 1) % ring], vs[i][(j + 1) % ring]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.scale(bm, vec=scale, verts=bm.verts)
    bmesh.ops.rotate(bm, matrix=Euler([math.radians(a) for a in rot]).to_matrix(), verts=bm.verts)
    bmesh.ops.translate(bm, vec=loc, verts=bm.verts)
    o = new_obj(name, bm, mats_of(m), bone, fx, smooth=kind not in ("cube", "oct"))
    if bevel:
        b = o.modifiers.new("Bevel", "BEVEL")
        b.width, b.segments = bevel, 2
    return subsurf(o, sub)


def limb(name, p1, p2, r1, r2, m="metal", bone=None, seg=14, sub=1, fx=None):
    p1, p2 = Vector(p1), Vector(p2)
    d = p2 - p1
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r1, radius2=max(r2, 0.0005), depth=d.length)
    bmesh.ops.translate(bm, vec=(0, 0, d.length / 2), verts=bm.verts)
    rot = d.to_track_quat("Z", "Y").to_matrix().to_4x4()
    bmesh.ops.transform(bm, matrix=Matrix.Translation(p1) @ rot, verts=bm.verts)
    return subsurf(new_obj(name, bm, mats_of(m), bone, fx), sub)


def lathe(name, profile, seg=28, sx=1.0, sy=1.0, center=(0, 0, 0), m="metal", bone=None, sub=1):
    """Revolve (radius, z) points around Z, scaled by sx/sy (oval cross-sections)."""
    bm = bmesh.new()
    rings = [[bm.verts.new((center[0] + math.cos(2 * math.pi * i / seg) * r * sx,
                            center[1] + math.sin(2 * math.pi * i / seg) * r * sy, center[2] + z)) for i in range(seg)]
             for (r, z) in profile]
    for j in range(len(rings) - 1):
        for i in range(seg):
            bm.faces.new((rings[j][i], rings[j][(i + 1) % seg], rings[j + 1][(i + 1) % seg], rings[j + 1][i]))
    if profile[0][0] > 0.001:
        bm.faces.new(list(reversed(rings[0])))
    if profile[-1][0] > 0.001:
        bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return subsurf(new_obj(name, bm, mats_of(m), bone), sub)


def mirror(fn):
    """Run fn(side, s) for R (-x) and L (+x); Duelist rig convention."""
    for side, s in (("R", -1.0), ("L", 1.0)):
        fn(side, s)


# ======================================================== skeleton layout
# Floating pose: slight A-pose arms, legs hanging loose with toes pointing down.
ARMS = {}
for side, s in (("R", -1.0), ("L", 1.0)):
    ARMS[side] = {"sh": Vector((0.2 * s, 0.0, 1.45)), "el": Vector((0.3 * s, 0.02, 1.18)),
                  "wr": Vector((0.36 * s, -0.04, 0.95))}
LEGS = {}
for side, s in (("R", -1.0), ("L", 1.0)):
    LEGS[side] = {"hip": Vector((0.09 * s, 0.0, 0.98)), "kn": Vector((0.1 * s, -0.03, 0.56)),
                  "an": Vector((0.11 * s, 0.03, 0.14)), "toe": Vector((0.11 * s, -0.08, 0.02))}

# ================================================================== HEAD
# Smooth faceless dome: an egg, slightly longer front-to-back, one vertical violet slit on the face.
lathe("LS_Dome", [(0.0005, 1.58), (0.06, 1.585), (0.095, 1.62), (0.112, 1.68), (0.114, 1.74), (0.104, 1.8),
                  (0.08, 1.85), (0.045, 1.88), (0.0005, 1.892)], sx=0.92, sy=1.08, m="dome", bone="head", sub=2)
prim("cube", "LS_Slit", (0, -0.118, 1.72), (0.014, 0.012, 0.15), m="violet", bone="head", bevel=0.004)
prim("cube", "LS_SlitFrame", (0, -0.112, 1.72), (0.03, 0.01, 0.18), m="dark", bone="head", bevel=0.005)
# swept crest from the brow over the top, and two small fins behind the jaw
for i in range(7):
    t = i / 6.0
    a = math.radians(35 + 140 * t)          # 35 deg = brow, 175 deg = back of the skull
    prim("cube", f"LS_Crest_{i}", (0, -math.cos(a) * 0.128, 1.73 + math.sin(a) * 0.125),
         (0.014, 0.055, 0.03 + 0.012 * t), rot=(90 - math.degrees(a), 0, 0), m="plate", bone="head", bevel=0.004)
mirror(lambda side, s: prim("cube", f"LS_JawFin.{side}", (0.1 * s, 0.03, 1.63), (0.015, 0.09, 0.05),
                            rot=(-25, 0, 8 * s), m="plate", bone="head", bevel=0.006))
mirror(lambda side, s: prim("cube", f"LS_EarSeam.{side}", (0.103 * s, 0.0, 1.7), (0.006, 0.07, 0.008),
                            m="cyan", bone="head"))
# neck: dark segmented column
limb("LS_Neck", (0, 0.01, 1.48), (0, 0.0, 1.6), 0.055, 0.045, m="dark", bone="neck")
for i in range(3):
    prim("torus", f"LS_NeckRing_{i}", (0, 0.005, 1.505 + i * 0.03), (1, 1.05, 1), m="metal", bone="neck",
         R=0.055, r=0.008, seg=20, ring=6)

# ================================================================= TORSO
# Breastplate: oval lathe, broad at the chest, tight at the waist; violet core diamond on the sternum.
lathe("LS_Chest", [(0.09, 1.26), (0.13, 1.3), (0.165, 1.36), (0.18, 1.42), (0.165, 1.47), (0.11, 1.5),
                   (0.06, 1.505)], sx=1.0, sy=0.72, m="plate", bone="chest", sub=1)
lathe("LS_ChestUnder", [(0.085, 1.27), (0.15, 1.35), (0.16, 1.43), (0.1, 1.49)], sx=1.02, sy=0.76, m="dark",
      bone="chest", sub=1)
prim("oct", "LS_Core", (0, -0.13, 1.39), (0.05, 0.05, 0.11), rot=(0, 0, 45), m="violet", bone="chest", seg=4)
prim("oct", "LS_CoreFrame", (0, -0.124, 1.39), (0.08, 0.04, 0.16), rot=(0, 0, 45), m="dark", bone="chest", seg=4)
mirror(lambda side, s: prim("cube", f"LS_ChestSeam.{side}", (0.08 * s, -0.115, 1.36), (0.008, 0.006, 0.12),
                            rot=(-10, 0, -30 * s), m="violet", bone="chest"))
prim("cube", "LS_Collar", (0, 0.0, 1.49), (0.24, 0.17, 0.04), m="metal", bone="chest", bevel=0.015)
# back emitter: a disc the rings "hang" from
prim("cyl", "LS_BackDisc", (0, 0.12, 1.39), (0.16, 0.16, 0.03), rot=(90, 0, 0), m="metal", bone="chest", seg=24)
prim("torus", "LS_BackGlow", (0, 0.137, 1.39), (1, 1, 1), rot=(90, 0, 0), m="violet", bone="chest",
     R=0.055, r=0.008, seg=24, ring=6)

# abdomen: three stacked dark bands with thin cyan seams
for i in range(3):
    z = 1.08 + i * 0.065
    lathe(f"LS_Ab_{i}", [(0.085, z), (0.1, z + 0.02), (0.095, z + 0.05), (0.08, z + 0.058)], sx=1.0, sy=0.75,
          m="dark", bone="spine", sub=1)
    prim("torus", f"LS_AbSeam_{i}", (0, 0, z + 0.06), (1, 0.75, 1), m="cyan" if i == 1 else "metal", bone="spine",
         R=0.085, r=0.004, seg=28, ring=5)

# pelvis plate + long cloth tabards (front / back) that trail like the legs
lathe("LS_Pelvis", [(0.09, 0.93), (0.15, 0.96), (0.16, 1.01), (0.13, 1.07), (0.09, 1.09)], sx=1.0, sy=0.78,
      m="plate", bone="hips", sub=1)
prim("cube", "LS_Belt", (0, 0, 1.02), (0.33, 0.25, 0.03), m="dark", bone="hips", bevel=0.01)
prim("cube", "LS_BeltGem", (0, -0.13, 1.02), (0.04, 0.012, 0.04), rot=(0, 45, 0), m="violet", bone="hips")


def tabard(name, y, length, w_top, w_bot, sway):
    bm = bmesh.new()
    rows, cols = 6, 4
    vs = []
    for r in range(rows + 1):
        t = r / rows
        w = w_top + (w_bot - w_top) * t
        z = 0.99 - length * t
        yy = y + sway * t * t
        vs.append([bm.verts.new(((c / cols - 0.5) * w, yy, z)) for c in range(cols + 1)])
    for r in range(rows):
        for c in range(cols):
            bm.faces.new((vs[r][c], vs[r][c + 1], vs[r + 1][c + 1], vs[r + 1][c]))
    o = new_obj(name, bm, [M["cloth"], M["violet"]], bone="hips")
    s = o.modifiers.new("Solid", "SOLIDIFY")
    s.thickness, s.offset = 0.012, 0
    return subsurf(o, 1)


tabard("LS_TabardFront", -0.125, 0.52, 0.16, 0.1, -0.03)
tabard("LS_TabardBack", 0.12, 0.62, 0.2, 0.12, 0.08)
prim("cube", "LS_TabardRune", (0, -0.133, 0.75), (0.02, 0.006, 0.14), m="violet", bone="hips")

# ================================================================== ARMS


def arm(side, s):
    a = ARMS[side]
    sh, el, wr = a["sh"], a["el"], a["wr"]
    # pauldron: layered rounded shells + a fin that sweeps up and back
    prim("sphere", f"LS_Pauldron.{side}", sh + Vector((0.03 * s, 0.0, 0.03)), (0.17, 0.16, 0.12),
         rot=(0, 18 * s, 0), m="plate", bone=f"upper_arm.{side}")
    prim("sphere", f"LS_PauldronUnder.{side}", sh + Vector((0.035 * s, 0, -0.015)), (0.15, 0.14, 0.09),
         rot=(0, 25 * s, 0), m="dark", bone=f"upper_arm.{side}")
    prim("cube", f"LS_PauldronFin.{side}", sh + Vector((0.06 * s, 0.05, 0.1)), (0.012, 0.13, 0.08),
         rot=(-35, 0, -10 * s), m="metal", bone=f"upper_arm.{side}", bevel=0.005)
    prim("cube", f"LS_PauldronSeam.{side}", sh + Vector((0.1 * s, 0.0, 0.04)), (0.006, 0.1, 0.008),
         rot=(0, 30 * s, 0), m="violet", bone=f"upper_arm.{side}")
    limb(f"LS_UpperArm.{side}", sh, el, 0.042, 0.036, m="dark", bone=f"upper_arm.{side}")
    prim("sphere", f"LS_Elbow.{side}", el, (0.075, 0.075, 0.075), m="metal", bone=f"forearm.{side}")
    # forearm: slim, with a long flared bracer carrying a cyan line
    limb(f"LS_Forearm.{side}", el, wr, 0.036, 0.03, m="dark", bone=f"forearm.{side}")
    mid = el.lerp(wr, 0.55)
    d = (wr - el).normalized()
    lb = limb(f"LS_Bracer.{side}", el.lerp(wr, 0.2), el.lerp(wr, 0.92), 0.045, 0.052, m="plate",
              bone=f"forearm.{side}", seg=8, sub=0)
    limb(f"LS_BracerLine.{side}", el.lerp(wr, 0.25) + Vector((0.045 * s, 0, 0)),
         el.lerp(wr, 0.88) + Vector((0.052 * s, 0, 0)), 0.006, 0.006, m="cyan", bone=f"forearm.{side}", seg=6, sub=0)
    # hand: open palm facing in, long fingers held together
    hd = d
    palm = wr + hd * 0.045
    prim("cube", f"LS_Palm.{side}", palm, (0.035, 0.07, 0.08), rot=(0, 0, 0), m="dark", bone=f"hand.{side}",
         bevel=0.012)
    for k in range(3):
        base = palm + hd * 0.04 + Vector((0, -0.022 + k * 0.022, 0))
        limb(f"LS_Finger{k}.{side}", base, base + hd * 0.07 + Vector((0, 0, -0.005 * k)), 0.011, 0.006,
             m="metal", bone=f"hand.{side}", seg=8, sub=0)
    limb(f"LS_Thumb.{side}", palm + Vector((0, -0.04, 0.0)), palm + Vector((-0.005 * s, -0.075, -0.035)),
         0.011, 0.007, m="metal", bone=f"hand.{side}", seg=8, sub=0)
    prim("cube", f"LS_PalmGlow.{side}", palm + Vector((-0.02 * s, 0, 0)), (0.004, 0.035, 0.035), rot=(0, 0, 45),
         m="violet", bone=f"hand.{side}")


mirror(arm)

# ================================================================== LEGS


def leg(side, s):
    l = LEGS[side]
    hip, kn, an, toe = l["hip"], l["kn"], l["an"], l["toe"]
    limb(f"LS_Thigh.{side}", hip, kn, 0.065, 0.048, m="dark", bone=f"thigh.{side}")
    prim("sphere", f"LS_ThighPlate.{side}", hip.lerp(kn, 0.4) + Vector((0.02 * s, -0.03, 0)), (0.1, 0.09, 0.22),
         rot=(4, 0, 0), m="plate", bone=f"thigh.{side}")
    prim("sphere", f"LS_Knee.{side}", kn + Vector((0, -0.02, 0)), (0.085, 0.09, 0.09), m="metal", bone=f"shin.{side}")
    prim("cube", f"LS_KneeGlow.{side}", kn + Vector((0, -0.065, 0)), (0.02, 0.006, 0.02), rot=(0, 45, 0),
         m="violet", bone=f"shin.{side}")
    limb(f"LS_Shin.{side}", kn, an, 0.045, 0.03, m="dark", bone=f"shin.{side}")
    # shin guard: long blade-like plate down the front, with a violet seam
    limb(f"LS_ShinGuard.{side}", kn.lerp(an, 0.1) + Vector((0, -0.025, 0)), an + Vector((0, -0.02, 0.04)),
         0.05, 0.03, m="plate", bone=f"shin.{side}", seg=6, sub=0)
    limb(f"LS_ShinSeam.{side}", kn.lerp(an, 0.2) + Vector((0, -0.07, 0)), kn.lerp(an, 0.85) + Vector((0, -0.05, 0)),
         0.006, 0.005, m="violet", bone=f"shin.{side}", seg=6, sub=0)
    # pointed foot: toes down (he never walks)
    limb(f"LS_Foot.{side}", an, toe, 0.035, 0.008, m="plate", bone=f"foot.{side}", seg=8, sub=0)
    prim("sphere", f"LS_Ankle.{side}", an, (0.06, 0.06, 0.06), m="metal", bone=f"foot.{side}")


mirror(leg)

# ============================================================ FX PARTS
# Three iron rings orbiting the torso. Each ring is built flat around the origin with notches + a violet
# inlay; the pivot (where the game places it) is the chest centre. Tilts are applied in game.
RING_PIVOT = Vector((0, 0, 1.3))
for i, (R, w) in enumerate(((0.31, 0.018), (0.37, 0.015), (0.43, 0.013))):
    prim("torus", f"LS_Ring{i}", RING_PIVOT, (1, 1, 1), m="iron", fx=f"Ring{i}", R=R, r=w, seg=56, ring=8, square=1.6)
    prim("torus", f"LS_RingInlay{i}", RING_PIVOT, (1, 1, 1), m="violet", fx=f"Ring{i}", R=R, r=w * 1.12, seg=56,
         ring=8, square=0.28)
    n = 6 + i * 2
    for k in range(n):
        a = 2 * math.pi * k / n
        prim("cube", f"LS_RingNotch{i}_{k}", RING_PIVOT + Vector((math.cos(a) * R, math.sin(a) * R, 0)),
             (w * 2.6, w * 1.2, w * 3.2), rot=(0, 0, math.degrees(a)), m="iron", fx=f"Ring{i}", bevel=0.003)
# hand shards: four crystals around each palm, pivot at the palm
for side, s in (("R", -1.0), ("L", 1.0)):
    wr, el = ARMS[side]["wr"], ARMS[side]["el"]
    pv = wr + (wr - el).normalized() * 0.06
    for k in range(4):
        a = 2 * math.pi * k / 4 + 0.4
        off = Vector((math.cos(a) * 0.11, math.sin(a) * 0.11, 0.03 * math.sin(a * 2)))
        prim("oct", f"LS_Shard{side}_{k}", pv + off, (0.028, 0.028, 0.075 - 0.01 * (k % 2)),
             rot=(20 * math.sin(a), 15, math.degrees(a)), m="shard", fx=f"Shards{side}", seg=4)

# ================================================================== RIG
arm_data = bpy.data.armatures.new("LodestarRig")
rig = bpy.data.objects.new("LodestarRig", arm_data)
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
bone("hips", (0, 0, 0.95), (0, 0, 1.06), "root")
bone("spine", (0, 0, 1.06), (0, 0, 1.27), "hips")
bone("chest", (0, 0, 1.27), (0, 0, 1.48), "spine")
bone("neck", (0, 0, 1.48), (0, 0, 1.6), "chest")
bone("head", (0, 0, 1.6), (0, 0, 1.86), "neck")
for side, a in ARMS.items():
    bone(f"upper_arm.{side}", a["sh"], a["el"], "chest")
    bone(f"forearm.{side}", a["el"], a["wr"], f"upper_arm.{side}")
    bone(f"hand.{side}", a["wr"], a["wr"] + (a["wr"] - a["el"]).normalized() * 0.15, f"forearm.{side}")
for side, l in LEGS.items():
    bone(f"thigh.{side}", l["hip"], l["kn"], "hips")
    bone(f"shin.{side}", l["kn"], l["an"], f"thigh.{side}")
    bone(f"foot.{side}", l["an"], l["toe"], f"shin.{side}")
bpy.ops.object.mode_set(mode="OBJECT")

# bake + join body parts (one 100% vertex group each)
dg = bpy.context.evaluated_depsgraph_get()


def bake(obj):
    ev = obj.evaluated_get(dg)
    me = bpy.data.meshes.new_from_object(ev, preserve_all_data_layers=True, depsgraph=dg)
    me.transform(obj.matrix_world)
    return me


baked = []
for obj, bname in PARTS:
    me = bake(obj)
    nob = bpy.data.objects.new(obj.name + "_b", me)
    COL.objects.link(nob)
    nob.vertex_groups.new(name=bname).add(list(range(len(me.vertices))), 1.0, "REPLACE")
    baked.append(nob)
for obj, _ in PARTS:
    bpy.data.objects.remove(obj, do_unlink=True)
for o in bpy.context.view_layer.objects:
    o.select_set(False)
for o in baked:
    o.select_set(True)
bpy.context.view_layer.objects.active = baked[0]
with bpy.context.temp_override(active_object=baked[0], selected_editable_objects=baked, selected_objects=baked):
    bpy.ops.object.join()
body = bpy.context.view_layer.objects.active
body.name = body.data.name = "LodestarMesh"
body.parent = rig
body.modifiers.new("Armature", "ARMATURE").object = rig

# ================================================================ EXPORT


def conv(v):
    return (-v[0], v[2], -v[1])


def srgb(c):
    c = max(0.0, min(1.0, c))
    return c * 12.92 if c <= 0.0031308 else 1.055 * c ** (1 / 2.4) - 0.055


PAL, PAL_IDX = [], {}


def pal_of(m):
    if m is None:
        return 0
    if m.name not in PAL_IDX:
        col = m.diffuse_color[:3]
        emit = 0.0
        em = next((n for n in m.node_tree.nodes if n.bl_idname == "ShaderNodeEmission"), None) if m.node_tree else None
        if em and not any(n.bl_idname == "ShaderNodeShaderToRGB" for n in m.node_tree.nodes):
            emit = float(em.inputs[1].default_value)
        PAL_IDX[m.name] = len(PAL)
        PAL.append((srgb(col[0]), srgb(col[1]), srgb(col[2]), emit))
    return PAL_IDX[m.name]


def tris_of(me, mats, buf_for):
    """Append (pos, normal, pal) triangles in Unity space, winding flipped for the axis mirror."""
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
    for tri in me.loop_triangles:
        m = mats[tri.material_index] if tri.material_index < len(mats) else None
        p = pal_of(m)
        buf = buf_for(tri)
        for li in (tri.loops[0], tri.loops[2], tri.loops[1]):
            vi = me.loops[li].vertex_index
            buf.append((conv(me.vertices[vi].co), conv(cn[li]), p))


def wstr(f, s):
    b = s.encode("utf-8")
    f.write(struct.pack("<i", len(b)))
    f.write(b)


# body (v1): parts per bone
groups = {vg.index: vg.name for vg in body.vertex_groups}
vbone = [groups.get(max(v.groups, key=lambda g: g.weight).group, "root") if v.groups else "root"
         for v in body.data.vertices]
bme = body.data.copy()
body_parts = {}
tris_of(bme, list(body.data.materials), lambda tri: body_parts.setdefault(vbone[tri.vertices[0]], []))
bpy.data.meshes.remove(bme)

# fx (v2): named parts with pivots
fx_parts = {}
for obj, part in FXPARTS:
    me = bake(obj)
    buf = fx_parts.setdefault(part, [])
    tris_of(me, list(obj.data.materials), lambda tri: buf)
    bpy.data.meshes.remove(me)
fx_pivots = {}
for side in ("R", "L"):
    wr, el = ARMS[side]["wr"], ARMS[side]["el"]
    fx_pivots[f"Shards{side}"] = wr + (wr - el).normalized() * 0.06
for i in range(3):
    fx_pivots[f"Ring{i}"] = RING_PIVOT


def write_parts(f, parts):
    f.write(struct.pack("<i", len(parts)))
    for name, verts in parts.items():
        wstr(f, name)
        f.write(struct.pack("<i", len(verts)))
        for (p, n, pl) in verts:
            f.write(struct.pack("<6fi", p[0], p[1], p[2], n[0], n[1], n[2], pl))


body_path = os.path.join(OUT_DIR, "lodestar_model.bin")
with open(body_path, "wb") as f:
    f.write(b"DUEL")
    f.write(struct.pack("<i", 1))
    bones = rig.data.bones
    f.write(struct.pack("<i", len(bones)))
    for b in bones:
        wstr(f, b.name)
        f.write(struct.pack("<3f", *conv(b.head_local)))
        f.write(struct.pack("<3f", *conv(b.tail_local)))
    f.write(struct.pack("<i", len(PAL)))
    for p in PAL:
        f.write(struct.pack("<4f", *p))
    write_parts(f, body_parts)

fx_path = os.path.join(OUT_DIR, "lodestar_fx.bin")
with open(fx_path, "wb") as f:
    f.write(b"DUEL")
    f.write(struct.pack("<i", 2))
    f.write(struct.pack("<i", len(fx_parts)))
    for name in fx_parts:
        pv = conv(fx_pivots[name])
        wstr(f, name)
        f.write(struct.pack("<3f", *pv))
        f.write(struct.pack("<3f", pv[0], pv[1] + 0.1, pv[2]))
    f.write(struct.pack("<i", len(PAL)))
    for p in PAL:
        f.write(struct.pack("<4f", *p))
    write_parts(f, fx_parts)

# outline for the preview only (added after export so it never ships)
body.data.materials.append(OUTLINE)
ol = body.modifiers.new("Outline", "SOLIDIFY")
ol.thickness, ol.offset, ol.use_flip_normals, ol.use_rim = 0.004, 1.0, True, False
ol.material_offset = len(body.data.materials)
# tilt the rings for the preview the way the game will
for obj, part in FXPARTS:
    if part.startswith("Ring"):
        i = int(part[-1])
        obj.rotation_mode = "XYZ"
        obj.location = -RING_PIVOT
        e = Euler((math.radians((18, -24, 9)[i]), math.radians((8, 14, -30)[i]), math.radians(i * 40)))
        obj.matrix_world = Matrix.Translation(RING_PIVOT) @ e.to_matrix().to_4x4() @ Matrix.Translation(-RING_PIVOT)

# ================================================================ PREVIEW


def look_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


cam = bpy.data.objects.new("LS_Camera", bpy.data.cameras.new("LS_Cam"))
FX.objects.link(cam)
cam.location = (1.6, -3.4, 1.45)
look_at(cam, (0, 0, 1.0))
scene.camera = cam
for name, kind, loc, en, colr in (("LS_Key", "SUN", (2.5, -2.5, 3.0), 4.0, (1, 0.96, 0.92)),
                                   ("LS_Rim", "AREA", (-2.0, 2.5, 2.6), 300, (0.55, 0.45, 1.0))):
    ld = bpy.data.lights.new(name, kind)
    ld.energy, ld.color = en, colr
    lo = bpy.data.objects.new(name, ld)
    FX.objects.link(lo)
    lo.location = loc
    look_at(lo, (0, 0, 1.0))
world = scene.world or bpy.data.worlds.new("World")
scene.world = world
try:
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    bg.inputs[0].default_value = (0.03, 0.025, 0.06, 1)
except Exception:
    pass
for eng in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
    try:
        scene.render.engine = eng
        break
    except Exception:
        pass
scene.render.resolution_x, scene.render.resolution_y = 1080, 1350
try:
    scene.view_settings.view_transform = "Standard"
except Exception:
    pass
png = os.path.join(OUT_DIR, "Lodestar_preview.png")
scene.render.filepath = png
try:
    bpy.ops.render.render(write_still=True)
    render_ok = True
except Exception as e:
    render_ok = str(e)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT_DIR, "Lodestar.blend"))

result = {"out": OUT_DIR, "render": render_ok,
          "body_tris": {k: len(v) // 3 for k, v in body_parts.items()},
          "fx_tris": {k: len(v) // 3 for k, v in fx_parts.items()}, "palette": len(PAL)}
print("LODESTAR BUILD:", result)
