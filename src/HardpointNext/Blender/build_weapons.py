"""Hardpoint NEXT weapon models (Blender).

Run:  blender -b -P build_weapons.py            (or python3 with the bpy module)
Out:  next_weapons.bin.gz next to this script, plus weapons_preview.png

Every weapon is modelled at mech scale (metres) with the grip at the origin and the barrel pointing down -Y
(Unity +Z after export). An empty named "<WEAPON>_muzzle" marks where shots, flashes and tracers start.

File format "NXGW" v1 (little endian):
  "NXGW" int version
  int palette_count, then palette_count x (r, g, b, emission) floats (sRGB)
  int weapon_count, then per weapon:
    str name, vec3 muzzle, int vert_count, vert_count x (vec3 pos, vec3 normal, int palette_index)
  Triangles are consecutive vertex triples, already wound for Unity.
  str = int byte length + utf8 bytes. Positions are in Unity space: x = -bx, y = bz, z = -by.
"""
import bpy
import bmesh
import gzip
import math
import os
import struct
import sys

OUT_DIR = os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals() else os.getcwd()
for a in sys.argv:
    if a.startswith("--out="):
        OUT_DIR = a[6:]

bpy.ops.wm.read_factory_settings(use_empty=True)

# ---------------------------------------------------------------- materials (palette)

PALETTE = {
    "gunmetal": ((0.10, 0.11, 0.13), 0.0),
    "steel": ((0.32, 0.34, 0.38), 0.0),
    "light": ((0.62, 0.64, 0.68), 0.0),
    "navy": ((0.13, 0.20, 0.42), 0.0),       # matches the mech's blue armour
    "pink": ((1.00, 0.30, 0.62), 1.6),       # the mech's pink trim, glowing
    "cyan": ((0.35, 0.92, 1.00), 2.4),       # energy weapons
    "orange": ((1.00, 0.55, 0.15), 1.4),
    "hazard": ((0.95, 0.75, 0.15), 0.0),
    "black": ((0.03, 0.03, 0.035), 0.0),
    "gold": ((0.85, 0.65, 0.25), 0.0),
}
MATS = {}
for name, (col, emit) in PALETTE.items():
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*col, 1.0)
    m["emit"] = emit
    MATS[name] = m


# ---------------------------------------------------------------- modelling helpers

def _finish(obj, mat, bevel):
    obj.data.materials.append(MATS[mat])
    if bevel > 0:
        mod = obj.modifiers.new("bevel", "BEVEL")
        mod.width = bevel
        mod.segments = 2
        mod.limit_method = "ANGLE"
    return obj


def box(parts, mat, size, loc, rot=(0, 0, 0), bevel=0.012):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=[math.radians(r) for r in rot])
    o = bpy.context.active_object
    o.scale = size
    bpy.ops.object.transform_apply(scale=True)
    parts.append(_finish(o, mat, bevel))
    return o


def cyl(parts, mat, r, length, loc, axis="y", verts=20, bevel=0.006, r2=None):
    """Cylinder (or cone when r2 is given) along an axis."""
    rot = {"x": (0, 90, 0), "y": (90, 0, 0), "z": (0, 0, 0)}[axis]
    if r2 is None:
        bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=length, location=loc,
                                            rotation=[math.radians(a) for a in rot])
    else:
        bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r, radius2=r2, depth=length, location=loc,
                                        rotation=[math.radians(a) for a in rot])
    o = bpy.context.active_object
    parts.append(_finish(o, mat, bevel))
    return o


def ring(parts, mat, r, thick, loc, axis="y"):
    rot = {"x": (0, 90, 0), "y": (90, 0, 0), "z": (0, 0, 0)}[axis]
    bpy.ops.mesh.primitive_torus_add(major_radius=r, minor_radius=thick, major_segments=24, minor_segments=8,
                                     location=loc, rotation=[math.radians(a) for a in rot])
    o = bpy.context.active_object
    parts.append(_finish(o, mat, 0))
    return o


def grip(parts, y=0.0, mat="gunmetal"):
    """Pistol grip at the origin (the hand holds it)."""
    box(parts, mat, (0.07, 0.10, 0.22), (0, y + 0.02, -0.08), rot=(-14, 0, 0))
    box(parts, "steel", (0.02, 0.07, 0.03), (0, y - 0.07, -0.02))  # trigger guard


WEAPONS = []   # (name, [objects], muzzle_location)


def weapon(name, muzzle):
    def deco(fn):
        parts = []
        fn(parts)
        WEAPONS.append((name, parts, muzzle))
        return fn
    return deco


# ---------------------------------------------------------------- arm weapons
# Barrel = -Y. Hand/grip at the origin; receivers sit just above it.

@weapon("BURSTRIFLE", (0, -1.32, 0.10))
def burst_rifle(p):
    grip(p)
    box(p, "navy", (0.14, 0.95, 0.20), (0, -0.25, 0.10))                 # receiver
    box(p, "gunmetal", (0.12, 0.45, 0.14), (0, 0.42, 0.06))              # stock
    box(p, "gunmetal", (0.08, 0.14, 0.24), (0, -0.12, -0.10), rot=(8, 0, 0))   # magazine
    cyl(p, "steel", 0.035, 0.45, (0, -0.95, 0.10))                        # barrel
    cyl(p, "gunmetal", 0.05, 0.12, (0, -1.22, 0.10))                      # muzzle brake
    box(p, "black", (0.06, 0.22, 0.07), (0, -0.20, 0.25))                 # optic
    box(p, "pink", (0.145, 0.30, 0.02), (0, -0.40, 0.17), bevel=0)        # trim
    for i in range(3):                                                    # burst vents
        box(p, "gunmetal", (0.15, 0.03, 0.06), (0, -0.62 - i * 0.07, 0.10), bevel=0)


@weapon("ASSAULTRIFLE", (0, -1.55, 0.10))
def assault_rifle(p):
    grip(p)
    box(p, "gunmetal", (0.15, 0.80, 0.22), (0, -0.18, 0.10))
    box(p, "navy", (0.16, 0.55, 0.18), (0, -0.82, 0.10))                  # handguard
    for i in range(5):
        box(p, "black", (0.17, 0.035, 0.08), (0, -0.62 - i * 0.09, 0.12), bevel=0)
    box(p, "steel", (0.10, 0.50, 0.12), (0, 0.48, 0.05))                  # stock
    box(p, "navy", (0.12, 0.10, 0.20), (0, 0.72, 0.03))                   # butt pad
    box(p, "gunmetal", (0.08, 0.13, 0.30), (0, -0.16, -0.14), rot=(18, 0, 0))  # curved mag (angled)
    box(p, "gunmetal", (0.08, 0.12, 0.14), (0, -0.22, -0.32), rot=(32, 0, 0))
    cyl(p, "steel", 0.03, 0.40, (0, -1.28, 0.10))
    cyl(p, "gunmetal", 0.045, 0.10, (0, -1.50, 0.10))
    box(p, "steel", (0.04, 0.6, 0.03), (0, -0.4, 0.23), bevel=0.004)      # top rail
    box(p, "pink", (0.03, 0.03, 0.03), (0.08, -0.02, 0.14), bevel=0)


@weapon("SMG", (0, -0.95, 0.09))
def smg(p):
    grip(p)
    box(p, "gunmetal", (0.13, 0.62, 0.18), (0, -0.20, 0.09))
    box(p, "navy", (0.135, 0.25, 0.12), (0, -0.55, 0.09))
    box(p, "gunmetal", (0.06, 0.09, 0.42), (0, -0.20, -0.20))             # long stick mag
    cyl(p, "steel", 0.028, 0.25, (0, -0.80, 0.09))
    cyl(p, "black", 0.04, 0.10, (0, -0.92, 0.09))
    box(p, "steel", (0.02, 0.35, 0.02), (0.05, 0.25, 0.10), bevel=0)      # folded wire stock
    box(p, "steel", (0.02, 0.35, 0.02), (-0.05, 0.25, 0.10), bevel=0)
    box(p, "pink", (0.14, 0.06, 0.02), (0, -0.05, 0.19), bevel=0)


@weapon("SHOTGUN", (0, -1.30, 0.12))
def shotgun(p):
    grip(p)
    box(p, "gunmetal", (0.22, 0.70, 0.26), (0, -0.10, 0.11))              # chunky receiver
    box(p, "steel", (0.14, 0.40, 0.18), (0, 0.45, 0.06))
    for x in (-0.055, 0.055):                                             # double barrel
        cyl(p, "steel", 0.055, 0.70, (x, -0.80, 0.14))
        cyl(p, "black", 0.04, 0.02, (x, -1.16, 0.14), verts=16, bevel=0)
    box(p, "navy", (0.24, 0.30, 0.10), (0, -0.72, 0.02))                  # pump
    for i in range(4):
        box(p, "black", (0.25, 0.03, 0.11), (0, -0.62 - i * 0.07, 0.02), bevel=0)
    box(p, "hazard", (0.23, 0.08, 0.27), (0, 0.18, 0.11), bevel=0.004)
    box(p, "pink", (0.02, 0.4, 0.03), (0.115, -0.15, 0.18), bevel=0)


@weapon("LINEAR", (0, -2.20, 0.12))
def linear(p):
    grip(p)
    box(p, "gunmetal", (0.16, 0.85, 0.24), (0, -0.05, 0.10))              # capacitor housing
    box(p, "navy", (0.18, 0.40, 0.30), (0, 0.10, 0.12))
    for x in (-0.07, 0.07):                                               # twin rails
        box(p, "steel", (0.04, 1.35, 0.10), (x, -1.20, 0.12), bevel=0.006)
    box(p, "cyan", (0.06, 1.25, 0.02), (0, -1.18, 0.12), bevel=0)         # glowing rail core
    for i in range(5):                                                    # coils
        ring(p, "cyan" if i % 2 == 0 else "steel", 0.11, 0.022, (0, -0.70 - i * 0.26, 0.12))
    box(p, "black", (0.07, 0.30, 0.08), (0, -0.25, 0.30))                 # scope
    cyl(p, "cyan", 0.03, 0.02, (0, -0.10, 0.30), bevel=0)
    box(p, "steel", (0.10, 0.40, 0.12), (0, 0.55, 0.06))


@weapon("PULSEGUN", (0, -1.18, 0.10))
def pulse_gun(p):
    grip(p)
    box(p, "light", (0.16, 0.60, 0.22), (0, -0.12, 0.10), bevel=0.04)     # rounded energy body
    cyl(p, "gunmetal", 0.09, 0.40, (0, -0.62, 0.10))
    for i in range(3):
        ring(p, "cyan", 0.10, 0.018, (0, -0.50 - i * 0.12, 0.10))
    cyl(p, "steel", 0.14, 0.12, (0, -0.90, 0.10), r2=0.07)                # emitter flare
    cyl(p, "cyan", 0.06, 0.03, (0, -0.97, 0.10), bevel=0)                 # emitter lens
    box(p, "cyan", (0.17, 0.25, 0.02), (0, -0.05, 0.215), bevel=0)        # charge window
    box(p, "navy", (0.10, 0.22, 0.16), (0, 0.30, 0.06), bevel=0.03)       # cell


@weapon("BAZOOKA", (0, -1.55, 0.22))
def bazooka(p):
    grip(p)
    cyl(p, "navy", 0.16, 1.90, (0, -0.45, 0.22), verts=24)               # launch tube
    cyl(p, "gunmetal", 0.20, 0.30, (0, -1.40, 0.22), r2=0.17, verts=24)    # front flare
    cyl(p, "gunmetal", 0.17, 0.25, (0, 0.55, 0.22), r2=0.21, verts=24)     # rear exhaust
    cyl(p, "black", 0.13, 0.02, (0, -1.56, 0.22), verts=24, bevel=0)
    ring(p, "hazard", 0.165, 0.02, (0, -0.95, 0.22))
    ring(p, "hazard", 0.165, 0.02, (0, 0.10, 0.22))
    box(p, "gunmetal", (0.06, 0.20, 0.14), (0, -0.25, 0.44))              # sight
    box(p, "orange", (0.02, 0.05, 0.05), (0, -0.33, 0.52), bevel=0)
    box(p, "gunmetal", (0.08, 0.10, 0.20), (0, -0.55, 0.0), rot=(-10, 0, 0))   # fore grip


@weapon("GATLING", (0, -1.50, 0.14))
def gatling(p):
    grip(p)
    box(p, "gunmetal", (0.26, 0.55, 0.30), (0, 0.05, 0.12))               # motor housing
    box(p, "hazard", (0.27, 0.06, 0.31), (0, -0.22, 0.12), bevel=0)
    box(p, "steel", (0.20, 0.35, 0.30), (0.20, 0.10, -0.05))              # ammo box on the side
    box(p, "gold", (0.05, 0.40, 0.05), (0.13, -0.05, 0.02), bevel=0)      # belt
    cyl(p, "steel", 0.15, 0.10, (0, -0.30, 0.14), verts=24)               # front plate
    for i in range(6):                                                    # six barrels
        a = i * math.pi / 3
        cyl(p, "steel", 0.03, 1.10, (math.cos(a) * 0.095, -0.88, 0.14 + math.sin(a) * 0.095), verts=10)
    cyl(p, "gunmetal", 0.15, 0.06, (0, -0.70, 0.14), verts=24)            # barrel clamps
    cyl(p, "gunmetal", 0.15, 0.06, (0, -1.38, 0.14), verts=24)
    box(p, "pink", (0.27, 0.15, 0.02), (0, 0.15, 0.275), bevel=0)


# ---------------------------------------------------------------- back units
# Mount point at the origin (the shoulder pod); barrel = -Y.

@weapon("GRENADE", (0, -2.05, 0.30))
def grenade_cannon(p):
    box(p, "gunmetal", (0.40, 0.55, 0.40), (0, 0.10, 0.20))               # breech block
    box(p, "hazard", (0.41, 0.08, 0.41), (0, -0.12, 0.20), bevel=0)
    cyl(p, "navy", 0.20, 1.50, (0, -0.95, 0.30), verts=24)                # thick barrel
    cyl(p, "gunmetal", 0.26, 0.35, (0, -1.80, 0.30), verts=24)            # muzzle shroud
    for i in range(4):
        box(p, "black", (0.54, 0.04, 0.10), (0, -1.70 - i * 0.07, 0.30), bevel=0)
    cyl(p, "black", 0.17, 0.02, (0, -1.98, 0.30), verts=24, bevel=0)
    box(p, "steel", (0.16, 0.30, 0.30), (0, 0.10, -0.08))                  # mount arm
    box(p, "orange", (0.42, 0.04, 0.04), (0, 0.38, 0.38), bevel=0)


@weapon("LASERCANNON", (0, -2.35, 0.28))
def laser_cannon(p):
    box(p, "light", (0.30, 0.80, 0.34), (0, 0.05, 0.26), bevel=0.05)      # generator body
    box(p, "cyan", (0.31, 0.30, 0.04), (0, 0.05, 0.43), bevel=0)          # glow vent
    cyl(p, "gunmetal", 0.10, 1.60, (0, -1.35, 0.28), verts=16)            # barrel
    for i in range(4):                                                    # cooling fins
        box(p, "steel", (0.34, 0.05, 0.34), (0, -0.70 - i * 0.30, 0.28), bevel=0.004)
    cyl(p, "cyan", 0.05, 1.50, (0, -1.35, 0.28), verts=12, bevel=0)        # visible core
    cyl(p, "steel", 0.14, 0.18, (0, -2.22, 0.28), r2=0.09, verts=16)       # lens housing
    cyl(p, "cyan", 0.08, 0.03, (0, -2.33, 0.28), verts=16, bevel=0)
    box(p, "steel", (0.14, 0.30, 0.30), (0, 0.15, -0.05))


# ---------------------------------------------------------------- export

def conv(v):
    return (-v[0], v[2], -v[1])


def srgb(c):
    c = max(0.0, min(1.0, c))
    return c * 12.92 if c <= 0.0031308 else 1.055 * c ** (1 / 2.4) - 0.055


PAL_NAMES = list(PALETTE.keys())


def weapon_tris(parts):
    deps = bpy.context.evaluated_depsgraph_get()
    out = []
    for o in parts:
        ev = o.evaluated_get(deps)
        me = ev.to_mesh()
        bm = bmesh.new()
        bm.from_mesh(me)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        bmesh.ops.triangulate(bm, faces=bm.faces)
        bm.to_mesh(me)
        bm.free()
        mw = o.matrix_world
        nm = mw.to_3x3().inverted().transposed()
        pal = PAL_NAMES.index(o.data.materials[0].name)
        me.calc_loop_triangles()
        smooth_cut = math.radians(35)
        for tri in me.loop_triangles:
            fn = (nm @ tri.normal).normalized()
            for vi in (tri.vertices[0], tri.vertices[2], tri.vertices[1]):   # flip winding for the mirror
                co = mw @ me.vertices[vi].co
                vn = (nm @ me.vertices[vi].normal).normalized()
                n = vn if vn.angle(fn, 0) < smooth_cut else fn               # smooth round parts, keep hard edges
                out.append((conv(co), conv(n), pal))
        ev.to_mesh_clear()
    return out


def wstr(f, s):
    b = s.encode("utf-8")
    f.write(struct.pack("<i", len(b)))
    f.write(b)


def export(path):
    raw = bytearray()

    class W:
        def write(self, b):
            raw.extend(b)
    f = W()
    f.write(b"NXGW")
    f.write(struct.pack("<i", 1))
    f.write(struct.pack("<i", len(PAL_NAMES)))
    for n in PAL_NAMES:
        (r, g, b), e = PALETTE[n]
        f.write(struct.pack("<4f", srgb(r), srgb(g), srgb(b), e))
    f.write(struct.pack("<i", len(WEAPONS)))
    total = 0
    for name, parts, muzzle in WEAPONS:
        verts = weapon_tris(parts)
        total += len(verts) // 3
        wstr(f, name)
        f.write(struct.pack("<3f", *conv(muzzle)))
        f.write(struct.pack("<i", len(verts)))
        for (p, n, pal) in verts:
            f.write(struct.pack("<6fi", p[0], p[1], p[2], n[0], n[1], n[2], pal))
    with gzip.open(path, "wb") as g:
        g.write(bytes(raw))
    print(f"wrote {path}: {len(WEAPONS)} weapons, {total} triangles, {os.path.getsize(path)} bytes")


def preview(path):
    """Lay the weapons out in a row and render a quick look (Workbench, flat colours)."""
    for i, (name, parts, muzzle) in enumerate(WEAPONS):
        for o in parts:
            o.location.x += (i % 5) * 1.3 - 2.6
            o.location.z += -(i // 5) * 1.2
        bpy.ops.object.empty_add(location=(muzzle[0] + (i % 5) * 1.3 - 2.6, muzzle[1], muzzle[2] - (i // 5) * 1.2))
        bpy.context.active_object.name = name + "_muzzle"
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.color_type = "MATERIAL"
    scene.render.resolution_x, scene.render.resolution_y = 1600, 900
    bpy.ops.object.camera_add(location=(4.5, 3.2, 3.0))
    cam = bpy.context.active_object
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 7.5
    direction = (-0.3 - cam.location.x, -1.0 - cam.location.y, -0.6 - cam.location.z)
    from mathutils import Vector
    cam.rotation_euler = Vector(direction).to_track_quat("-Z", "Y").to_euler()
    scene.camera = cam
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print("preview", path)


export(os.path.join(OUT_DIR, "next_weapons.bin.gz"))
if "--preview" in sys.argv:
    preview(os.path.join(OUT_DIR, "weapons_preview.png"))
