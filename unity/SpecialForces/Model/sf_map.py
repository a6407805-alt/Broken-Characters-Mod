"""Builds Stormreach Valley in Blender from sf_terrain.build(): terrain chunks, water, waterfalls, trees, boulders,
outpost dressing and the marker empties the mod reads.

Object names matter (the mod finds things by name at runtime):
  Terrain_<i>_<j>   ground, 100 x 100 m chunks          -> mesh colliders, "World" layer
  Boulders_<i>_<j>  rocks per chunk                      -> mesh colliders
  Trunks_<i>_<j>    tree trunks per chunk                -> mesh colliders
  Canopy_<i>_<j>    tree crowns per chunk                -> no collision
  Water_*           river ribbon, lake, grotto pool      -> no collision (the bed is walkable under 0.55 m of water)
  Waterfall_*       falling-water sheets                 -> no collision
  Outpost_*         concrete pad, lander pad, sandbags   -> mesh colliders
  SF_*              empties: tower, nests, lairs, gates, spawns, waterfalls (mist), floodlights
Material slot names are the ones the mod swaps for the game's own materials (see SFMap.cs).
"""
import math
import random

import bmesh
import bpy
import numpy as np

import sf_terrain as T

CHUNK = 50  # cells per chunk side (100 m)


def material(name, color):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1.0)
    return mat


MATS = {
    "matForestFloor": (0.25, 0.38, 0.18), "matMossRock": (0.33, 0.38, 0.27), "matCliff": (0.28, 0.30, 0.27),
    "matRiverbed": (0.35, 0.33, 0.28), "matPlainGrass": (0.72, 0.58, 0.28), "matOutpostGround": (0.45, 0.43, 0.38),
    "matWater": (0.16, 0.34, 0.33), "matWaterfall": (0.75, 0.85, 0.88), "matTreeTrunk": (0.22, 0.20, 0.15),
    "matCanopy": (0.16, 0.33, 0.12), "matBoulder": (0.35, 0.37, 0.32), "matConcrete": (0.55, 0.55, 0.52),
    "matPadMarking": (0.85, 0.65, 0.10), "matSandbag": (0.52, 0.47, 0.33), "matMetal": (0.30, 0.32, 0.34),
}


def new_object(name, bm, mats, parent=None):
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    for m in mats:
        mesh.materials.append(material(m, MATS[m]))
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    if parent:
        obj.parent = parent
    return obj


def fix_normals(bm):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])


# ---------------------------------------------------------------- terrain

def build_terrain(terrain, root):
    H, xs, mat = terrain["H"], terrain["xs"], terrain["mat"]
    chunks = (T.N - 1) // CHUNK
    objs = []
    for cj in range(chunks):
        for ci in range(chunks):
            bm = bmesh.new()
            i0, j0 = ci * CHUNK, cj * CHUNK
            verts = {}
            for j in range(j0, j0 + CHUNK + 1):
                for i in range(i0, i0 + CHUNK + 1):
                    verts[(i, j)] = bm.verts.new((xs[i], xs[j], H[j, i]))
            used = sorted(set(int(v) for v in np.unique(mat[j0:j0 + CHUNK, i0:i0 + CHUNK])))
            slot = {m: k for k, m in enumerate(used)}
            for j in range(j0, j0 + CHUNK):
                for i in range(i0, i0 + CHUNK):
                    # Counter-clockwise seen from above: the face normal points up.
                    f = bm.faces.new((verts[(i, j)], verts[(i + 1, j)], verts[(i + 1, j + 1)], verts[(i, j + 1)]))
                    f.material_index = slot[int(mat[j, i])]
                    f.smooth = True
            obj = new_object(f"Terrain_{ci}_{cj}", bm, [T.MATERIALS[m] for m in used], root)
            objs.append(obj)
    return objs


# ---------------------------------------------------------------- water

def build_water(water, root):
    bm = bmesh.new()
    L, R, Z = water["left"], water["right"], water["z"]
    prev = None
    s = 0.0
    uv_layer = bm.loops.layers.uv.new()
    for k in range(len(Z)):
        a = bm.verts.new((L[k, 0], L[k, 1], Z[k]))
        b = bm.verts.new((R[k, 0], R[k, 1], Z[k]))
        if prev:
            pa, pb, ps = prev
            # Skip the steep drop at the head fall (the waterfall sheet covers it).
            if abs(Z[k] - pa.co.z) < 3.0:
                f = bm.faces.new((pa, pb, b, a))
                for loop, uv in zip(f.loops, ((0, ps), (1, ps), (1, s), (0, s))):
                    loop[uv_layer].uv = (uv[0], uv[1] / 20.0)
        if k > 0:
            s += math.dist(L[k], L[k - 1])
        prev = (a, b, s)
    fix_normals(bm)
    for f in bm.faces:
        if f.normal.z < 0:
            f.normal_flip()
    objs = [new_object("Water_River", bm, ["matWater"], root)]
    for n, (cx, cy, r, z) in enumerate(water["discs"]):
        bm = bmesh.new()
        bmesh.ops.create_circle(bm, cap_ends=True, segments=48, radius=r)
        bmesh.ops.translate(bm, verts=bm.verts, vec=(cx, cy, z))
        for f in bm.faces:
            if f.normal.z < 0:
                f.normal_flip()
        objs.append(new_object(["Water_Lake", "Water_Grotto"][n], bm, ["matWater"], root))
    return objs


def build_waterfalls(falls, root):
    objs = []
    for name, x, y, top, bottom, width, ang in falls:
        bm = bmesh.new()
        uv_layer = bm.loops.layers.uv.new()
        rows, cols = 10, 6
        dx, dy = math.cos(ang + math.pi / 2), math.sin(ang + math.pi / 2)   # across the fall
        ox, oy = math.cos(ang), math.sin(ang)                               # outwards (downstream)
        grid = []
        for r in range(rows + 1):
            v = r / rows
            z = top + (bottom - top) * v
            push = 1.5 * math.sin(v * math.pi * 0.5)                         # the water arcs outwards as it falls
            row = []
            for c in range(cols + 1):
                u = c / cols - 0.5
                bulge = 0.4 * math.cos(u * math.pi)
                row.append(bm.verts.new((x + dx * u * width + ox * (push + bulge),
                                         y + dy * u * width + oy * (push + bulge), z)))
            grid.append(row)
        for r in range(rows):
            for c in range(cols):
                f = bm.faces.new((grid[r][c], grid[r][c + 1], grid[r + 1][c + 1], grid[r + 1][c]))
                for loop, uv in zip(f.loops, ((c / cols, r / rows), ((c + 1) / cols, r / rows),
                                              ((c + 1) / cols, (r + 1) / rows), (c / cols, (r + 1) / rows))):
                    loop[uv_layer].uv = (uv[0], uv[1] * (top - bottom) / 6.0)
        objs.append(new_object(name.replace("SF_", ""), bm, ["matWaterfall"], root))
    return objs


# ---------------------------------------------------------------- trees and boulders

def lumpy_sphere(bm, center, radius, rnd, squash=1.0, subdiv=2):
    res = bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=radius)
    verts = res["verts"]
    ph = rnd.uniform(0, 10)
    for v in verts:
        n = v.co.normalized()
        k = 1.0 + 0.18 * math.sin(n.x * 4 + ph) * math.cos(n.y * 5 - ph) + 0.08 * math.sin(n.z * 9 + ph * 2)
        v.co = n * radius * k
        v.co.z *= squash
        v.co += center
    return verts


def add_tree(bm_trunk, bm_crown, x, y, z, height, rot, seed):
    rnd = random.Random(seed)
    from mathutils import Matrix, Vector
    sides = 7
    r0 = height * 0.035 + 0.35
    rings = []
    lean = Vector((rnd.uniform(-0.12, 0.12), rnd.uniform(-0.12, 0.12), 1.0))
    # Buttress roots: a flared base (rainforest trees).
    for k, (h, rad) in enumerate(((0.0, r0 * 2.1), (0.08, r0 * 1.3), (0.35, r0), (0.7, r0 * 0.75), (1.0, r0 * 0.5))):
        ring = []
        cz = h * height * 0.78
        cx, cy = lean.x * cz, lean.y * cz
        for s in range(sides):
            a = s / sides * 2 * math.pi + rot
            flare = 1.0 + (0.35 * math.sin(a * 3 + seed) if k == 0 else 0.0)
            ring.append(bm_trunk.verts.new((x + cx + math.cos(a) * rad * flare, y + cy + math.sin(a) * rad * flare,
                                            z + cz)))
        rings.append(ring)
    for a_ring, b_ring in zip(rings, rings[1:]):
        for s in range(sides):
            bm_trunk.faces.new((a_ring[s], a_ring[(s + 1) % sides], b_ring[(s + 1) % sides], b_ring[s]))
    bm_trunk.faces.new(list(reversed(rings[-1])))
    # Crown: a few lumpy blobs around the top.
    top = Vector((x + lean.x * height * 0.78, y + lean.y * height * 0.78, z + height * 0.78))
    blobs = rnd.randint(3, 5)
    for b in range(blobs):
        a = b / blobs * 2 * math.pi + rnd.uniform(-0.4, 0.4)
        off = Vector((math.cos(a), math.sin(a), 0)) * height * rnd.uniform(0.12, 0.22)
        rad = height * rnd.uniform(0.18, 0.26)
        lumpy_sphere(bm_crown, top + off + Vector((0, 0, rnd.uniform(-0.5, 2.5))), rad, rnd, 0.62, 1)
    lumpy_sphere(bm_crown, top + Vector((0, 0, height * 0.1)), height * 0.24, rnd, 0.6, 1)


def add_boulder(bm, x, y, z, size, rot, seed):
    from mathutils import Vector
    rnd = random.Random(seed)
    verts = lumpy_sphere(bm, Vector((x, y, z + size * 0.25)), size, rnd, rnd.uniform(0.45, 0.8), 2)
    for v in verts:
        dx, dy = v.co.x - x, v.co.y - y
        v.co.x = x + dx * math.cos(rot) - dy * math.sin(rot) * 1.3
        v.co.y = y + dx * math.sin(rot) + dy * math.cos(rot)


def chunk_of(x, y):
    size = CHUNK * T.STEP
    return (int(min(9, max(0, (x + T.HALF) // size))), int(min(9, max(0, (y + T.HALF) // size))))


def build_scatter(scatter, root):
    trunks, crowns, rocks = {}, {}, {}
    for (x, y, z, h, rot, seed) in scatter["trees"]:
        key = chunk_of(x, y)
        bt = trunks.setdefault(key, bmesh.new())
        bc = crowns.setdefault(key, bmesh.new())
        add_tree(bt, bc, x, y, z, h, rot, seed)
    for (x, y, z, size, rot, seed) in scatter["boulders"]:
        key = chunk_of(x, y)
        add_boulder(rocks.setdefault(key, bmesh.new()), x, y, z, size, rot, seed)
    objs = []
    for key, bm in trunks.items():
        fix_normals(bm)
        objs.append(new_object(f"Trunks_{key[0]}_{key[1]}", bm, ["matTreeTrunk"], root))
    for key, bm in crowns.items():
        fix_normals(bm)
        for f in bm.faces:
            f.smooth = True
        objs.append(new_object(f"Canopy_{key[0]}_{key[1]}", bm, ["matCanopy"], root))
    for key, bm in rocks.items():
        fix_normals(bm)
        for f in bm.faces:
            f.smooth = True
        objs.append(new_object(f"Boulders_{key[0]}_{key[1]}", bm, ["matBoulder"], root))
    return objs


# ---------------------------------------------------------------- outpost dressing

def box(bm, cx, cy, cz, sx, sy, sz, rot=0.0):
    res = bmesh.ops.create_cube(bm, size=1.0)
    for v in res["verts"]:
        x, y, z = v.co.x * sx, v.co.y * sy, v.co.z * sz
        v.co = (cx + x * math.cos(rot) - y * math.sin(rot), cy + x * math.sin(rot) + y * math.cos(rot), cz + z)
    return res["verts"]


def build_outpost(terrain, root):
    tx, ty = T.TOWER
    tz = T.height_at(terrain, tx, ty)
    # Concrete ring pad under the tower.
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=32, radius1=20, radius2=19, depth=1.4)
    bmesh.ops.translate(bm, verts=bm.verts, vec=(tx, ty, tz + 0.2))
    fix_normals(bm)
    objs = [new_object("Outpost_TowerPad", bm, ["matConcrete"], root)]
    # Lander pad: a square slab with yellow edge markings.
    lx, ly = T.LANDER_PAD
    lz = T.height_at(terrain, lx, ly)
    bm = bmesh.new()
    box(bm, lx, ly, lz + 0.1, 30, 30, 1.2)
    fix_normals(bm)
    for f in bm.faces:
        f.material_index = 0
    mk = bmesh.new()
    for k in range(4):
        a = k * math.pi / 2
        box(mk, lx + math.cos(a) * 13.5, ly + math.sin(a) * 13.5, lz + 0.72, 1.2 if k % 2 == 0 else 26, 26 if k % 2 == 0 else 1.2, 0.05)
    box(mk, lx, ly, lz + 0.72, 10, 1.2, 0.05)
    box(mk, lx, ly, lz + 0.72, 1.2, 10, 0.05)
    fix_normals(mk)
    objs.append(new_object("Outpost_LanderPad", bm, ["matConcrete"], root))
    objs.append(new_object("Outpost_PadMarkings", mk, ["matPadMarking"], root))
    # Sandbag arcs and supply containers around the edge of the outpost (cover; the build zone is inside).
    bm = bmesh.new()
    rnd = random.Random(5)
    for k in range(10):
        a = k / 10 * 2 * math.pi + 0.2
        cx, cy = tx + math.cos(a) * 70, ty + 15 + math.sin(a) * 70
        cz = T.height_at(terrain, cx, cy)
        for s in range(-2, 3):
            b = a + math.pi / 2
            box(bm, cx + math.cos(b) * s * 1.9, cy + math.sin(b) * s * 1.9, cz + 0.45, 1.8, 0.9, 0.9, b)
            box(bm, cx + math.cos(b) * (s + 0.5) * 1.9, cy + math.sin(b) * (s + 0.5) * 1.9, cz + 1.3, 1.8, 0.9, 0.8, b)
    fix_normals(bm)
    objs.append(new_object("Outpost_Sandbags", bm, ["matSandbag"], root))
    bm = bmesh.new()
    for k in range(6):
        a = k / 6 * 2 * math.pi + rnd.uniform(0, 0.4)
        cx, cy = tx + math.cos(a) * 38, ty + 15 + math.sin(a) * 38
        if math.hypot(cx - lx, cy - ly) < 25:
            continue
        cz = T.height_at(terrain, cx, cy)
        box(bm, cx, cy, cz + 1.3, 6.0, 2.5, 2.6, a)
    fix_normals(bm)
    objs.append(new_object("Outpost_Containers", bm, ["matMetal"], root))
    return objs


# ---------------------------------------------------------------- markers

def build_markers(markers, falls, root):
    objs = []
    for name, (x, y, z) in markers.items():
        e = bpy.data.objects.new(name, None)
        e.location = (x, y, z)
        e.empty_display_size = 3
        bpy.context.collection.objects.link(e)
        e.parent = root
        objs.append(e)
    for name, x, y, top, bottom, width, ang in falls:
        e = bpy.data.objects.new(name, None)
        e.location = (x, y, bottom)
        bpy.context.collection.objects.link(e)
        e.parent = root
        objs.append(e)
    return objs


def build():
    terrain, water, falls, scatter, markers = T.build()
    root = bpy.data.objects.new("StormValley", None)
    bpy.context.collection.objects.link(root)
    objs = [root]
    objs += build_terrain(terrain, root)
    objs += build_water(water, root)
    objs += build_waterfalls(falls, root)
    objs += build_scatter(scatter, root)
    objs += build_outpost(terrain, root)
    objs += build_markers(markers, falls, root)
    print(f"[sf] map: {len(objs)} objects")
    return root, objs
