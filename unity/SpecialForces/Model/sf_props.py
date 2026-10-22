"""Static props for Special Forces (Blender 4.0): control tower, storm nest, barrier wall, supply depot, lander, floodlight.

Convention (same as the survivors' props): z up, the prop's front faces -Y, metres. Each build returns a root empty
whose children are meshes; material slot names are what SFAssets.cs converts (plain colours on the game's shader,
emissive ones glow). Anchors the mod uses are empties:
  ControlTower: TowerCore (the glowing orb: lightning and the shield ring start here)
  StormNest:    NestCore
  Lander:       LanderRamp (the ramp mesh's parent, pivot on the hinge), Seat_01..Seat_12, CamInterior, EngineL/R
"""
import math
import random

import bmesh
import bpy
from mathutils import Matrix, Vector

COLORS = {
    "matTowerDark": ((0.11, 0.12, 0.13), 0.0), "matTowerWhite": ((0.82, 0.83, 0.82), 0.0),
    "matTowerPanel": ((0.18, 0.62, 0.60), 1.2), "matTowerGlow": ((0.35, 0.85, 1.00), 4.0),
    "matTowerBrass": ((0.55, 0.46, 0.34), 0.0),
    "matNestRock": ((0.13, 0.13, 0.17), 0.0), "matNestCrystal": ((0.40, 0.65, 1.00), 3.0),
    "matNestFlesh": ((0.22, 0.20, 0.30), 0.0),
    "matWallConcrete": ((0.55, 0.55, 0.52), 0.0), "matWallMetal": ((0.32, 0.33, 0.34), 0.0),
    "matDepotMetal": ((0.36, 0.40, 0.34), 0.0), "matDepotTank": ((0.62, 0.60, 0.55), 0.0),
    "matDepotLight": ((0.30, 1.00, 0.45), 3.0), "matCrate": ((0.42, 0.33, 0.20), 0.0),
    "matLanderHull": ((0.34, 0.38, 0.32), 0.0), "matLanderDark": ((0.16, 0.17, 0.18), 0.0),
    "matLanderWindow": ((0.20, 0.45, 0.60), 1.5), "matLanderGlow": ((1.00, 0.55, 0.20), 4.0),
    "matLanderBay": ((0.25, 0.26, 0.27), 0.0), "matLanderLight": ((1.00, 0.15, 0.10), 3.0),
    "matFloodPole": ((0.30, 0.31, 0.33), 0.0), "matFloodLamp": ((1.00, 0.95, 0.80), 5.0),
}


def mat(name):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    color, glow = COLORS[name]
    m.diffuse_color = (*color, 1.0)
    return m


class Part:
    """Collects geometry per material, then becomes one mesh object with one slot per material."""

    def __init__(self):
        self.bms = {}

    def bm(self, material):
        return self.bms.setdefault(material, bmesh.new())

    def build(self, name, parent):
        combined = bmesh.new()
        order = list(self.bms.keys())
        for k, (m, b) in enumerate(self.bms.items()):
            for f in b.faces:
                f.material_index = k
            mesh = bpy.data.meshes.new("tmp")
            b.to_mesh(mesh)
            b.free()
            combined.from_mesh(mesh)
            bpy.data.meshes.remove(mesh)
        bmesh.ops.recalc_face_normals(combined, faces=combined.faces[:])
        mesh = bpy.data.meshes.new(name)
        combined.to_mesh(mesh)
        combined.free()
        for m in order:
            mesh.materials.append(mat(m))
        obj = bpy.data.objects.new(name, mesh)
        bpy.context.collection.objects.link(obj)
        obj.parent = parent
        return obj


def transform(verts, m):
    for v in verts:
        v.co = m @ v.co


def box(b, center, size, rot_z=0.0, rot_x=0.0):
    res = bmesh.ops.create_cube(b, size=1.0)
    m = Matrix.Translation(center) @ Matrix.Rotation(rot_z, 4, "Z") @ Matrix.Rotation(rot_x, 4, "X") @ \
        Matrix.Diagonal((*size, 1.0))
    transform(res["verts"], m)
    return res["verts"]


def cyl(b, center, r1, r2, depth, segs=12, rot=None):
    res = bmesh.ops.create_cone(b, cap_ends=True, cap_tris=False, segments=segs, radius1=r1, radius2=r2, depth=depth)
    m = Matrix.Translation(center) @ (rot if rot else Matrix.Identity(4))
    transform(res["verts"], m)
    return res["verts"]


def sphere(b, center, r, subdiv=2, squash=(1, 1, 1)):
    res = bmesh.ops.create_icosphere(b, subdivisions=subdiv, radius=r)
    transform(res["verts"], Matrix.Translation(center) @ Matrix.Diagonal((*squash, 1.0)))
    return res["verts"]


def torus(b, center, major, minor, segs=32, rings=8):
    verts = []
    grid = []
    for i in range(segs):
        a = i / segs * 2 * math.pi
        row = []
        for j in range(rings):
            c = j / rings * 2 * math.pi
            r = major + minor * math.cos(c)
            row.append(b.verts.new((center[0] + r * math.cos(a), center[1] + r * math.sin(a), center[2] + minor * math.sin(c))))
        grid.append(row)
    for i in range(segs):
        for j in range(rings):
            b.faces.new((grid[i][j], grid[(i + 1) % segs][j], grid[(i + 1) % segs][(j + 1) % rings], grid[i][(j + 1) % rings]))
    return verts


def strut(b, p0, p1, r=0.15, segs=6):
    p0, p1 = Vector(p0), Vector(p1)
    d = p1 - p0
    rot = d.to_track_quat("Z", "Y").to_matrix().to_4x4()
    return cyl(b, (p0 + p1) / 2, r, r, d.length, segs, rot)


def anchor(name, parent, location):
    e = bpy.data.objects.new(name, None)
    e.location = location
    e.empty_display_size = 0.5
    bpy.context.collection.objects.link(e)
    e.parent = parent
    return e


def root(name):
    r = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(r)
    return r


# ---------------------------------------------------------------- control tower (~30 m)

def arc_panel(b, z0, z1, r0, r1, a0, a1, thick=0.15, segs=10, flare=0.0):
    """A curved panel around the Z axis: angles a0..a1, from z0 (radius r0) to z1 (radius r1); flare bows it outward."""
    rows = 6
    outer, inner = [], []
    for i in range(rows + 1):
        v = i / rows
        z = z0 + (z1 - z0) * v
        r = r0 + (r1 - r0) * v + flare * math.sin(v * math.pi)
        ro, ri = [], []
        for j in range(segs + 1):
            a = a0 + (a1 - a0) * j / segs
            ro.append(b.verts.new((math.cos(a) * (r + thick), math.sin(a) * (r + thick), z)))
            ri.append(b.verts.new((math.cos(a) * r, math.sin(a) * r, z)))
        outer.append(ro)
        inner.append(ri)
    for i in range(rows):
        for j in range(segs):
            b.faces.new((outer[i][j], outer[i][j + 1], outer[i + 1][j + 1], outer[i + 1][j]))
            b.faces.new((inner[i][j], inner[i + 1][j], inner[i + 1][j + 1], inner[i][j + 1]))
    for i in range(rows):                                   # side edges
        for j in (0, segs):
            b.faces.new((outer[i][j], outer[i + 1][j], inner[i + 1][j], inner[i][j]))
    for i in (0, rows):                                     # top and bottom edges
        for j in range(segs):
            b.faces.new((outer[i][j], inner[i][j], inner[i][j + 1], outer[i][j + 1]))


def control_tower():
    """Styled after C&C3's Tiberium Spike (the user's reference): an octagonal armoured base gripped by clamp struts
    with glowing teal hex panels, a stacked black-and-white segmented column ringed by curved teal panel fins, and an
    open claw pod on top cradling the storm core. Original geometry, ~30 m tall."""
    r = root("ControlTower")
    p = Part()
    dk, wt, pn, g, br = (p.bm(n) for n in ("matTowerDark", "matTowerWhite", "matTowerPanel", "matTowerGlow", "matTowerBrass"))
    # Octagonal base: a stepped plinth.
    cyl(dk, (0, 0, 0.6), 7.0, 6.6, 1.2, 8)
    cyl(dk, (0, 0, 2.1), 6.2, 4.6, 1.8, 8)
    cyl(wt, (0, 0, 3.15), 4.6, 4.4, 0.3, 8)
    for k in range(8):                                     # teal hex panels on the sloped faces, light strips between
        a = k * math.pi / 4 + math.pi / 8
        box(pn, (math.cos(a) * 5.55, math.sin(a) * 5.55, 2.05), (2.8, 0.25, 1.3), a + math.pi / 2, -0.62 if False else 0.0)
        a2 = k * math.pi / 4
        box(wt, (math.cos(a2) * 6.75, math.sin(a2) * 6.75, 1.25), (0.25, 0.9, 0.12), a2)
    # Four clamp struts: bent arms from the column down to feet on the ground.
    for k in range(4):
        a = k * math.pi / 2 + math.pi / 4
        c, s_ = math.cos(a), math.sin(a)
        strut(dk, (c * 2.2, s_ * 2.2, 7.5), (c * 5.0, s_ * 5.0, 4.0), 0.45)
        strut(dk, (c * 5.0, s_ * 5.0, 4.0), (c * 7.6, s_ * 7.6, 0.6), 0.45)
        box(dk, (c * 7.8, s_ * 7.8, 0.35), (1.6, 1.6, 0.7), a)
        box(wt, (c * 7.8, s_ * 7.8, 0.75), (1.2, 1.2, 0.12), a)
        box(pn, (c * 5.0, s_ * 5.0, 4.0), (0.9, 0.9, 0.9), a)
    # The column, bottom to top: (height, radius, material) stacked segments.
    stack = [(2.0, 2.7, "dk"), (0.6, 2.9, "wt"), (2.4, 2.2, "dk"), (0.5, 2.4, "wt"), (1.6, 1.9, "dk"),
             (0.3, 2.3, "fin"), (0.3, 2.3, "fin"), (0.3, 2.3, "fin"), (2.6, 1.6, "dk"), (0.6, 1.9, "wt"),
             (2.6, 1.5, "dk"), (0.5, 1.8, "wt"), (2.6, 1.45, "dk"), (0.3, 2.0, "fin"), (0.3, 2.0, "fin"),
             (2.2, 1.35, "dk"), (0.7, 1.7, "wt"), (1.4, 1.2, "dk")]
    z = 3.3
    mats = {"dk": dk, "wt": wt, "fin": dk}
    for h, rad, m in stack:
        cyl(mats[m], (0, 0, z + h / 2), rad, rad, h, 16)
        if m == "fin":                                     # radiator discs read as ribbed segments
            cyl(wt, (0, 0, z + h * 0.15), rad * 0.85, rad * 0.85, h * 0.3, 16)
        z += h
    top = z
    # Curved teal panel fins at three heights, two per level, turning 60 degrees per level.
    for level, zc in enumerate((8.6, 13.4, 18.4)):
        for k in range(2):
            a0 = k * math.pi + level * math.pi / 3
            arc_panel(pn, zc - 1.7, zc + 1.7, 2.7, 2.6, a0, a0 + math.radians(95), 0.18, 10, 0.25)
            # Dark frame rails at the panel's top and bottom.
            arc_panel(dk, zc + 1.7, zc + 1.95, 2.55, 2.55, a0 - 0.05, a0 + math.radians(95) + 0.05, 0.3, 10)
            arc_panel(dk, zc - 1.95, zc - 1.7, 2.6, 2.6, a0 - 0.05, a0 + math.radians(95) + 0.05, 0.3, 10)
            box(dk, (math.cos(a0 + 0.83) * 2.0, math.sin(a0 + 0.83) * 2.0, zc), (0.8, 0.3, 0.3), a0 + 0.83)
    # Pod on top: collar, then four claw petals opening outward around the glowing core.
    cyl(wt, (0, 0, top + 0.4), 1.6, 1.9, 0.8, 16)
    cyl(dk, (0, 0, top + 1.0), 1.9, 1.9, 0.4, 16)
    for k in range(4):
        a0 = k * math.pi / 2 + math.radians(8)
        arc_panel(br, top + 1.1, top + 4.4, 1.6, 3.9, a0, a0 + math.radians(52), 0.22, 8, -0.2)
        arc_panel(dk, top + 1.1, top + 3.0, 1.45, 2.7, a0 + 0.06, a0 + math.radians(52) - 0.06, 0.12, 8, -0.15)
    sphere(g, (0, 0, top + 3.0), 1.35, 3)                   # the storm core
    torus(g, (0, 0, top + 1.25), 1.75, 0.08, 24, 6)
    p.build("TowerMesh", r)
    anchor("TowerCore", r, (0, 0, top + 3.0))
    return r


# ---------------------------------------------------------------- storm nest (~10 m)

def storm_nest():
    r = root("StormNest")
    p = Part()
    rock, cr, fl = p.bm("matNestRock"), p.bm("matNestCrystal"), p.bm("matNestFlesh")
    rnd = random.Random(7)
    sphere(fl, (0, 0, 0.8), 4.5, 2, (1, 1, 0.35))                  # swollen base mound
    for k in range(9):                                            # jagged spires leaning outwards
        a = k / 9 * 2 * math.pi + rnd.uniform(-0.2, 0.2)
        h = rnd.uniform(5.5, 10.0)
        lean = Matrix.Rotation(rnd.uniform(0.15, 0.4), 4, Vector((-math.sin(a), math.cos(a), 0)))
        cyl(rock, (math.cos(a) * 2.6, math.sin(a) * 2.6, h / 2 + 0.5), rnd.uniform(0.7, 1.2), 0.05, h, 5, lean)
    cyl(rock, (0, 0, 6.0), 1.4, 0.1, 11.0, 6)                       # central spire
    for k in range(7):                                            # glowing crystal shards
        a = rnd.uniform(0, 2 * math.pi)
        rad = rnd.uniform(1.0, 3.5)
        h = rnd.uniform(1.2, 3.0)
        tilt = Matrix.Rotation(rnd.uniform(0.2, 0.7), 4, Vector((-math.sin(a), math.cos(a), 0)))
        cyl(cr, (math.cos(a) * rad, math.sin(a) * rad, h / 2 + 1.0), 0.35, 0.02, h, 4, tilt)
    sphere(cr, (0, 0, 4.2), 0.9, 2)                                 # core
    p.build("NestMesh", r)
    anchor("NestCore", r, (0, 0, 4.2))
    return r


# ---------------------------------------------------------------- barrier wall (8 x 1 x 3.5 m)

def barrier_wall():
    r = root("BarrierWall")
    p = Part()
    c, m = p.bm("matWallConcrete"), p.bm("matWallMetal")
    box(c, (0, 0, 1.2), (8.0, 1.2, 2.4))
    box(c, (0, 0, 2.85), (8.0, 0.8, 0.9))
    for x in (-3.9, 0, 3.9):
        box(m, (x, 0, 1.75), (0.35, 1.4, 3.5))
    box(m, (0, 0, 3.4), (8.2, 0.9, 0.18))
    p.build("WallMesh", r)
    return r


# ---------------------------------------------------------------- supply depot (~6 x 6 m)

def supply_depot():
    r = root("SupplyDepot")
    p = Part()
    m, t, l, cr = p.bm("matDepotMetal"), p.bm("matDepotTank"), p.bm("matDepotLight"), p.bm("matCrate")
    box(m, (0, 0, 0.25), (6.0, 6.0, 0.5))
    for x in (-1.5, 1.5):
        cyl(t, (x, 1.2, 2.2), 1.2, 1.2, 3.4, 14)
        sphere(t, (x, 1.2, 3.9), 1.2, 2, (1, 1, 0.5))
        torus(m, (x, 1.2, 1.4), 1.25, 0.1, 18, 5)
        torus(m, (x, 1.2, 3.0), 1.25, 0.1, 18, 5)
    strut(m, (-1.5, 1.2, 3.0), (1.5, 1.2, 3.0), 0.2)
    for k, (x, y, z) in enumerate(((-1.8, -1.6, 0.9), (-0.6, -1.7, 0.9), (-1.2, -1.6, 1.9), (1.6, -1.8, 0.9))):
        box(cr, (x, y, z), (1.1, 1.1, 1.0), 0.1 * k)
    strut(m, (2.5, 2.5, 0.5), (2.5, 2.5, 7.0), 0.1)                # antenna
    sphere(l, (2.5, 2.5, 7.2), 0.3, 2)
    p.build("DepotMesh", r)
    return r


# ---------------------------------------------------------------- lander (~24 m)

def lander():
    """Military dropship. Nose towards -Y; the rear ramp (+Y end) is its own child object hinged at the bay floor."""
    r = root("Lander")
    p = Part()
    hull, dark, win, glow, bay, light = (p.bm(n) for n in ("matLanderHull", "matLanderDark", "matLanderWindow",
                                                           "matLanderGlow", "matLanderBay", "matLanderLight"))
    # Fuselage: a hollow shell (floor, roof, side walls, front bulkhead; the back is the ramp) so the bay shot can
    # see the squad. 1.0.1's solid box hid them. Tapered nose and a raised spine.
    box(hull, (0, 0, 1.15), (7.0, 16.0, 0.3))                       # belly
    box(hull, (0, 0, 6.85), (7.0, 16.0, 0.3))                       # roof
    for x in (-3.35, 3.35):
        box(hull, (x, 0, 4.0), (0.3, 16.0, 6.0))                    # side walls
    box(hull, (0, -7.85, 4.0), (7.0, 0.3, 6.0))                     # front bulkhead
    box(dark, (0, 8.0, 7.0), (7.0, 0.4, 0.4))                       # rear frame over the ramp
    nose = box(hull, (0, -10.0, 4.4), (5.6, 4.5, 4.6))
    for v in nose:
        if v.co.y < -11.0:
            v.co.x *= 0.55
            v.co.z = 4.4 + (v.co.z - 4.4) * 0.6
    box(win, (0, -11.6, 5.6), (3.6, 1.6, 1.4), 0, -0.5)
    box(hull, (0, 2.0, 7.6), (3.0, 12.0, 1.4))                      # spine
    # Bay interior: floor, side benches (darker), red bay lights.
    box(bay, (0, 4.0, 1.2), (6.2, 13.0, 0.3))
    for x in (-2.7, 2.7):
        box(dark, (x, 4.0, 1.9), (0.8, 12.0, 0.4))
        for k in range(3):
            sphere(light, (x * 1.15, -1.0 + k * 5.0, 6.6), 0.2, 1)
    # Wings with engine nacelles (pointing down for the landing).
    for side in (-1, 1):
        box(hull, (side * 6.5, 0.0, 5.2), (6.0, 5.5, 0.8))
        cyl(dark, (side * 10.0, 0.0, 4.6), 1.6, 1.4, 4.0, 14)
        cyl(glow, (side * 10.0, 0.0, 2.5), 1.3, 1.3, 0.3, 14)
        box(hull, (side * 2.6, 10.5, 7.8), (0.4, 3.5, 3.0), 0, 0.3)     # tail fins
    for side in (-1, 1):                                          # landing skids
        box(dark, (side * 3.0, 0.0, 0.25), (0.5, 14.0, 0.5))
        for y in (-5, 0, 5):
            strut(dark, (side * 3.0, y, 0.3), (side * 3.0, y, 1.2), 0.15)
    p.build("LanderMesh", r)

    # Ramp: pivot on the hinge at the rear bottom edge of the bay (y = 8, z = 1.2). Closed = upright.
    hinge = anchor("LanderRamp", r, (0, 8.0, 1.2))
    rp = Part()
    box(rp.bm("matLanderHull"), (0, 0.3, 2.8), (6.4, 0.5, 5.6))
    box(rp.bm("matLanderDark"), (0, 0.0, 2.8), (6.0, 0.15, 5.2))
    ramp = rp.build("RampMesh", hinge)
    ramp.location = (0, 0, 0)

    for k in range(12):                                           # two rows of six facing inwards
        row, col = divmod(k, 6)
        x = -1.8 if row == 0 else 1.8
        anchor(f"Seat_{k + 1:02d}", r, (x, -1.5 + col * 1.6, 1.35))
    anchor("CamInterior", r, (0, 7.5, 4.2))
    anchor("EngineL", r, (-10.0, 0.0, 2.3))
    anchor("EngineR", r, (10.0, 0.0, 2.3))
    return r


def floodlight():
    r = root("Floodlight")
    p = Part()
    pole, lamp = p.bm("matFloodPole"), p.bm("matFloodLamp")
    cyl(pole, (0, 0, 5.0), 0.25, 0.18, 10.0, 8)
    box(pole, (0, 0, 10.2), (2.4, 0.4, 0.4))
    for x in (-0.9, 0.9):
        box(pole, (x, -0.2, 10.0), (0.8, 0.6, 0.6), 0, 0.5)
        box(lamp, (x, -0.52, 9.85), (0.6, 0.05, 0.45), 0, 0.5)
    box(pole, (0, 0, 0.3), (1.2, 1.2, 0.6))
    p.build("FloodlightMesh", r)
    anchor("Lamp", r, (0, -0.6, 9.8))
    return r


BUILDERS = {
    "ControlTower": control_tower, "StormNest": storm_nest, "BarrierWall": barrier_wall,
    "SupplyDepot": supply_depot, "Lander": lander, "Floodlight": floodlight,
}
