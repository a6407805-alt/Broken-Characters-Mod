"""
Exports the built Duelist (DuelistMesh + DuelistRig) to duelist_model.bin for the RoR2 mod.
Run in Blender after build_duelist.py. Writes Documents/DuelistModels/duelist_model.bin.

Format (little-endian), converted to Unity space (x = -bx, y = bz, z = -by; winding flipped):
  "DUEL" int32 version
  int32 boneCount   { str name, float3 head, float3 tail }
  int32 paletteCount{ float r, g, b (sRGB), float emit }
  int32 partCount   { str bone, int32 vertCount, { float3 pos, float3 normal, int32 palette } * vertCount }
  str = int32 byteLength + utf8 bytes. Triangles are the vertices in order (non-indexed).
"""
import bpy, os, struct

OUT = os.path.join(os.path.expanduser("~"), "Documents", "DuelistModels", "duelist_model.bin")
body = bpy.data.objects["DuelistMesh"]
rig = bpy.data.objects["DuelistRig"]
import bmesh
# Work on a copy with every face turned to point outward (Blender draws both sides so flipped
# faces never showed there, but the game only draws the outside).
src = body.data
bm = bmesh.new()
bm.from_mesh(src)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
me = bpy.data.meshes.new("DuelistExportTmp")
bm.to_mesh(me)
bm.free()
for i, m in enumerate(src.materials):
    me.materials.append(m)


def conv(v):
    return (-v[0], v[2], -v[1])


def srgb(c):
    c = max(0.0, min(1.0, c))
    return c * 12.92 if c <= 0.0031308 else 1.055 * c ** (1 / 2.4) - 0.055


def mat_info(m):
    if m is None:
        return (1, 0, 1), 0.0
    col = tuple(m.diffuse_color[:3])
    emit = 0.0
    if m.node_tree and not any(n.bl_idname == "ShaderNodeShaderToRGB" for n in m.node_tree.nodes):
        em = next((n for n in m.node_tree.nodes if n.bl_idname == "ShaderNodeEmission"), None)
        emit = float(em.inputs[1].default_value) if em else 1.0
    return col, emit


palette = []
mat_to_pal = {}
for i, m in enumerate(me.materials):
    if m and m.name == "DU_Outline":
        continue
    col, emit = mat_info(m)
    mat_to_pal[i] = len(palette)
    palette.append((srgb(col[0]), srgb(col[1]), srgb(col[2]), emit))

groups = {vg.index: vg.name for vg in body.vertex_groups}
vbone = []
for v in src.vertices:              # same vertex order as the copy
    best = max(v.groups, key=lambda g: g.weight, default=None)
    vbone.append(groups.get(best.group, "root") if best else "root")

me.calc_loop_triangles()
try:
    cn = [c.vector.copy() for c in me.corner_normals]
except AttributeError:
    me.calc_normals_split()
    cn = [l.normal.copy() for l in me.loops]

parts = {}
for tri in me.loop_triangles:
    if tri.material_index not in mat_to_pal:
        continue
    pal = mat_to_pal[tri.material_index]
    bone = vbone[tri.vertices[0]]
    buf = parts.setdefault(bone, [])
    for li in (tri.loops[0], tri.loops[2], tri.loops[1]):     # stored (a, c, b); CustomModel.cs swaps back
        vi = me.loops[li].vertex_index
        buf.append((conv(me.vertices[vi].co), conv(cn[li]), pal))


def wstr(f, s):
    b = s.encode("utf-8")
    f.write(struct.pack("<i", len(b)))
    f.write(b)


with open(OUT, "wb") as f:
    f.write(b"DUEL")
    f.write(struct.pack("<i", 1))
    bones = rig.data.bones
    f.write(struct.pack("<i", len(bones)))
    for b in bones:
        wstr(f, b.name)
        f.write(struct.pack("<3f", *conv(b.head_local)))
        f.write(struct.pack("<3f", *conv(b.tail_local)))
    f.write(struct.pack("<i", len(palette)))
    for p in palette:
        f.write(struct.pack("<4f", *p))
    f.write(struct.pack("<i", len(parts)))
    for bone, verts in parts.items():
        wstr(f, bone)
        f.write(struct.pack("<i", len(verts)))
        for (p, n, pal) in verts:
            f.write(struct.pack("<6fi", p[0], p[1], p[2], n[0], n[1], n[2], pal))

bpy.data.meshes.remove(me)
total = sum(len(v) for v in parts.values()) // 3
print("DUELIST EXPORT:", OUT, "tris", total, "parts", {k: len(v) // 3 for k, v in parts.items()}, "palette", len(palette))
