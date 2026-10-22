"""Shared geometry helpers (copied from the Nidus / Infested General pipeline).

Conventions (Blender): Z up, characters face -Y, their left side is +X. Units are metres.
Geometry is collected per material by MeshBuilder; each vertex carries bone weights (or none for
static props), and one mesh object is made per material so Unity gets one renderer per material.
"""
import math

import bpy
import bmesh
from mathutils import Matrix, Vector


def make_material(name, rgb, emission=0.0, roughness=0.6, metallic=0.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    if emission > 0:
        key = "Emission Color" if "Emission Color" in bsdf.inputs else "Emission"
        bsdf.inputs[key].default_value = (*rgb, 1.0)
        bsdf.inputs["Emission Strength"].default_value = emission
    mat.diffuse_color = (*rgb, 1.0)
    return mat


class MeshBuilder:
    """Collects geometry per material, with optional per-vertex bone weights."""

    def __init__(self, material_names, mesh_names):
        self.mesh_names = mesh_names
        self.parts = {name: {"v": [], "f": [], "w": [], "smooth": []} for name in material_names}

    def add(self, mat, verts, faces, weights=None, smooth=False):
        part = self.parts[mat]
        start = len(part["v"])
        part["v"].extend(Vector(v) for v in verts)
        part["f"].extend(tuple(i + start for i in f) for f in faces)
        part["smooth"].extend([smooth] * len(faces))
        if weights is None:
            part["w"].extend({} for _ in verts)
        elif callable(weights):
            part["w"].extend(weights(Vector(v)) for v in verts)
        elif isinstance(weights, str):
            part["w"].extend({weights: 1.0} for _ in verts)
        else:
            part["w"].extend(dict(weights) for _ in verts)

    def build(self, materials, armature_obj=None, parent=None):
        objects = []
        for mat_name, part in self.parts.items():
            if not part["v"]:
                continue
            name = self.mesh_names[mat_name]
            mesh = bpy.data.meshes.new(name)
            mesh.from_pydata([tuple(v) for v in part["v"]], [], part["f"])
            # The generators don't keep a consistent winding, and the game culls back faces: point every
            # face outward or inside-out parts show their far side (the model looks see-through).
            bm = bmesh.new()
            bm.from_mesh(mesh)
            bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
            bm.to_mesh(mesh)
            bm.free()
            mesh.update()
            for poly, smooth in zip(mesh.polygons, part["smooth"]):
                poly.use_smooth = smooth
            mesh.materials.append(materials[mat_name])

            obj = bpy.data.objects.new(name, mesh)
            bpy.context.scene.collection.objects.link(obj)
            if armature_obj is not None:
                obj.parent = armature_obj
                mod = obj.modifiers.new("Armature", "ARMATURE")
                mod.object = armature_obj
                groups = {}
                for i, weights in enumerate(part["w"]):
                    total = sum(weights.values()) or 1.0
                    for bone, w in weights.items():
                        if w <= 0:
                            continue
                        if bone not in groups:
                            groups[bone] = obj.vertex_groups.new(name=bone)
                        groups[bone].add([i], w / total, "REPLACE")
            elif parent is not None:
                obj.parent = parent
            objects.append(obj)
        return objects


# ----------------------------------------------------------------------------- primitives

def frame_from_dir(d):
    d = d.normalized()
    up = Vector((0, 0, 1)) if abs(d.z) < 0.9 else Vector((1, 0, 0))
    a = d.cross(up).normalized()
    b = d.cross(a).normalized()
    return a, b


def tube(points, radii, sides=6, cap=True):
    """Generalised cylinder through a polyline. radii: one per point."""
    points = [Vector(p) for p in points]
    verts, faces = [], []
    prev_a = None
    for i, p in enumerate(points):
        if i == 0:
            d = points[1] - points[0]
        elif i == len(points) - 1:
            d = points[-1] - points[-2]
        else:
            d = points[i + 1] - points[i - 1]
        d.normalize()
        if prev_a is None:
            a, b = frame_from_dir(d)
        else:
            a = (prev_a - d * prev_a.dot(d)).normalized()
            b = d.cross(a).normalized()
        prev_a = a
        r = radii[i]
        for s in range(sides):
            ang = 2 * math.pi * s / sides
            verts.append(p + (a * math.cos(ang) + b * math.sin(ang)) * r)
    for i in range(len(points) - 1):
        for s in range(sides):
            n = (s + 1) % sides
            faces.append((i * sides + s, i * sides + n, (i + 1) * sides + n, (i + 1) * sides + s))
    if cap:
        faces.append(tuple(reversed(range(sides))))
        last = (len(points) - 1) * sides
        faces.append(tuple(last + s for s in range(sides)))
    return verts, faces


def cone(base, tip, radius, sides=6):
    """A spike from base to tip."""
    base, tip = Vector(base), Vector(tip)
    a, b = frame_from_dir(tip - base)
    verts = [base + (a * math.cos(2 * math.pi * s / sides) + b * math.sin(2 * math.pi * s / sides)) * radius
             for s in range(sides)]
    verts.append(tip)
    faces = [(s, (s + 1) % sides, sides) for s in range(sides)]
    faces.append(tuple(reversed(range(sides))))
    return verts, faces


def ellipsoid(center, radii, seg=12, rings=8, lump=0.0, seed=0):
    """Ellipsoid; lump > 0 adds organic bumps (fraction of the radius)."""
    cx, cy, cz = center
    rx, ry, rz = radii

    def bump(phi, th):
        if lump <= 0:
            return 1.0
        return 1.0 + lump * (math.sin(th * 3 + seed) * math.sin(phi * 4 + seed * 1.7) +
                             0.5 * math.sin(th * 7 + phi * 5 + seed * 0.3))

    verts = [Vector((cx, cy, cz + rz))]
    for j in range(1, rings):
        phi = math.pi * j / rings
        for i in range(seg):
            th = 2 * math.pi * i / seg
            k = bump(phi, th)
            verts.append(Vector((cx + rx * k * math.sin(phi) * math.cos(th),
                                 cy + ry * k * math.sin(phi) * math.sin(th),
                                 cz + rz * k * math.cos(phi))))
    verts.append(Vector((cx, cy, cz - rz)))
    faces = []
    for i in range(seg):
        faces.append((0, 1 + (i + 1) % seg, 1 + i))
    for j in range(rings - 2):
        for i in range(seg):
            a = 1 + j * seg + i
            b = 1 + j * seg + (i + 1) % seg
            faces.append((a, b, b + seg, a + seg))
    last = len(verts) - 1
    base = 1 + (rings - 2) * seg
    for i in range(seg):
        faces.append((last, base + i, base + (i + 1) % seg))
    return verts, faces


def box(center, size, rot=None):
    c = Vector(center)
    sx, sy, sz = (s / 2 for s in size)
    corners = [Vector((x, y, z)) for x in (-sx, sx) for y in (-sy, sy) for z in (-sz, sz)]
    if rot is not None:
        corners = [rot @ v for v in corners]
    verts = [c + v for v in corners]
    faces = [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)]
    return verts, faces


def bevel_box(center, size, bevel=0.2, rot=None):
    """A box with chamfered edges (an octagonal prism stretched in X/Y), reads as armour plating."""
    c = Vector(center)
    sx, sy, sz = (s / 2 for s in size)
    bx, by = sx * bevel, sy * bevel
    ring = [(-sx + bx, -sy), (sx - bx, -sy), (sx, -sy + by), (sx, sy - by),
            (sx - bx, sy), (-sx + bx, sy), (-sx, sy - by), (-sx, -sy + by)]
    verts = []
    for z, shrink in ((-sz, 0.9), (-sz * 0.6, 1.0), (sz * 0.6, 1.0), (sz, 0.88)):
        for x, y in ring:
            v = Vector((x * shrink, y * shrink, z))
            if rot is not None:
                v = rot @ v
            verts.append(c + v)
    n = len(ring)
    faces = []
    for k in range(3):
        for i in range(n):
            j = (i + 1) % n
            faces.append((k * n + i, k * n + j, (k + 1) * n + j, (k + 1) * n + i))
    faces.append(tuple(reversed(range(n))))
    faces.append(tuple(3 * n + i for i in range(n)))
    return verts, faces


def cylinder(center, axis, radius, length, sides=10):
    c, axis = Vector(center), Vector(axis).normalized()
    return tube([c - axis * length / 2, c + axis * length / 2], [radius, radius], sides)


def torus(center, axis, major, minor, seg=8, sides=4):
    c = Vector(center)
    axis = Vector(axis).normalized()
    a, b = frame_from_dir(axis)
    verts, faces = [], []
    for i in range(seg):
        t = 2 * math.pi * i / seg
        ring_c = c + (a * math.cos(t) + b * math.sin(t)) * major
        radial = (a * math.cos(t) + b * math.sin(t))
        for j in range(sides):
            p = 2 * math.pi * j / sides
            verts.append(ring_c + (radial * math.cos(p) + axis * math.sin(p)) * minor)
    for i in range(seg):
        for j in range(sides):
            n_i, n_j = (i + 1) % seg, (j + 1) % sides
            faces.append((i * sides + j, n_i * sides + j, n_i * sides + n_j, i * sides + n_j))
    return verts, faces


def mirror_x(verts, faces):
    verts = [Vector((-v.x, v.y, v.z)) for v in verts]
    faces = [tuple(reversed(f)) for f in faces]
    return verts, faces


def transform(verts, matrix):
    return [matrix @ Vector(v) for v in verts]


def rot(axis, degrees):
    return Matrix.Rotation(math.radians(degrees), 3, axis)


def curve(points, segments):
    """Catmull-Rom points through control points (for tentacles and claws)."""
    pts = [Vector(p) for p in points]
    out = []
    ext = [pts[0] * 2 - pts[1]] + pts + [pts[-1] * 2 - pts[-2]]
    for i in range(1, len(ext) - 2):
        p0, p1, p2, p3 = ext[i - 1], ext[i], ext[i + 1], ext[i + 2]
        for s in range(segments):
            t = s / segments
            t2, t3 = t * t, t * t * t
            out.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 +
                              (-p0 + 3 * p1 - 3 * p2 + p3) * t3))
    out.append(pts[-1])
    return out


def nearest_bone_weights(segments, power=4.0, falloff=0.02):
    """Weights from distance to bone segments: smooth blending at joints.
    segments: list of (bone_name, head, tail)."""
    segs = [(n, Vector(h), Vector(t)) for n, h, t in segments]

    def weights(v):
        w = {}
        for name, h, t in segs:
            d = t - h
            k = max(0.0, min(1.0, (v - h).dot(d) / max(d.length_squared, 1e-9)))
            dist = (v - (h + d * k)).length
            w[name] = w.get(name, 0.0) + 1.0 / (dist + falloff) ** power
        total = sum(w.values())
        # drop tiny influences so each vertex has at most a few bones
        top = sorted(w.items(), key=lambda kv: -kv[1])[:3]
        return {n: x / total for n, x in top if x / total > 0.02}

    return weights
