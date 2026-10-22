"""The Hurricane: the Special Forces final boss, a storm given a body (after the user's image).

A hunched quadruped of dark storm cloud: massive shoulders, long front arms ending in three hooked claws, short
digitigrade hind legs, a long whipping tail with a spiked tip, a low horned head with an open fanged jaw and
glowing blue eyes. Lightning veins (emissive tubes) run over the body, skinned to the same bones.

Conventions (Blender): Z up, faces -Y, its left is +X, metres. Authored ~6.5 m tall at the shoulders; the mod scales
it up (config "Boss Height", 24 m). Bones the mod uses: head (eyes), jaw, hand.l/.r (claws), tail.6 (tip), chest.
"""
import math
import random

import bpy
from mathutils import Euler, Matrix, Vector

from sf_mesh import (MeshBuilder, cone, curve, ellipsoid, make_material, nearest_bone_weights, transform, tube)

# ----------------------------------------------------------------------------- skeleton

SHOULDER = Vector((1.5, -1.1, 4.6))
ELBOW = Vector((2.1, -1.9, 2.6))
WRIST = Vector((1.9, -2.5, 0.55))
HAND_TIP = Vector((1.9, -3.2, 0.05))
HIP = Vector((1.1, 1.6, 3.0))
KNEE = Vector((1.35, 0.7, 1.6))
ANKLE = Vector((1.25, 2.0, 0.7))
TOE = Vector((1.25, 1.4, 0.05))
TAIL = [(0, 2.2, 3.1), (0, 3.4, 2.9), (0, 4.7, 2.5), (0, 6.0, 2.3), (0, 7.2, 2.4), (0, 8.3, 2.8), (0, 9.2, 3.4)]


def mx(v, side):
    return (abs(v[0]) * side, v[1], v[2])


def side_bones(s, side):
    return [
        (f"upper_arm.{s}", mx(SHOULDER, side), mx(ELBOW, side), "chest"),
        (f"lower_arm.{s}", mx(ELBOW, side), mx(WRIST, side), f"upper_arm.{s}"),
        (f"hand.{s}", mx(WRIST, side), mx(HAND_TIP, side), f"lower_arm.{s}"),
        (f"thigh.{s}", mx(HIP, side), mx(KNEE, side), "pelvis"),
        (f"calf.{s}", mx(KNEE, side), mx(ANKLE, side), f"thigh.{s}"),
        (f"foot.{s}", mx(ANKLE, side), mx(TOE, side), f"calf.{s}"),
    ]


BONES = [
    ("base", (0, 0, 0), (0, 0, 0.6), None),
    ("pelvis", (0, 1.6, 3.2), (0, 0.6, 3.7), "base"),
    ("spine", (0, 0.6, 3.7), (0, -0.4, 4.5), "pelvis"),
    ("chest", (0, -0.4, 4.5), (0, -1.3, 4.9), "spine"),
    ("neck", (0, -1.3, 4.9), (0, -2.3, 4.3), "chest"),
    ("head", (0, -2.3, 4.3), (0, -3.6, 3.9), "neck"),
    ("jaw", (0, -2.5, 3.7), (0, -3.5, 3.2), "head"),
    *side_bones("l", 1),
    *side_bones("r", -1),
]
_prev = "pelvis"
for i in range(len(TAIL) - 1):
    BONES.append((f"tail.{i + 1}", TAIL[i], TAIL[i + 1], _prev))
    _prev = f"tail.{i + 1}"

MATERIALS = {
    # name: (base colour, emission, roughness, metallic)
    "matHurricaneCloud": ((0.17, 0.19, 0.23), 0.0, 0.85, 0.0),
    "matHurricaneDark": ((0.07, 0.08, 0.10), 0.0, 0.7, 0.0),
    "matHurricaneBone": ((0.55, 0.57, 0.60), 0.0, 0.4, 0.0),
    "matHurricaneLightning": ((0.55, 0.80, 1.00), 7.0, 0.3, 0.0),
    "matHurricaneEyes": ((0.65, 0.90, 1.00), 10.0, 0.2, 0.0),
}
MESH_NAMES = {m: m.replace("mat", "", 1) for m in MATERIALS}


def seg(name):
    for n, h, t, _ in BONES:
        if n == name:
            return (n, Vector(h), Vector(t))
    raise KeyError(name)


def w(*names, power=4.0, falloff=0.05):
    return nearest_bone_weights([seg(n) for n in names], power, falloff)


def cloud(mb, rnd, center, radii, weights, lump=0.22, seg_n=16, rings=10, mat="matHurricaneCloud"):
    """A billowing cloud mass: a lumpy ellipsoid plus a few smaller puffs on its surface."""
    mb.add(mat, *ellipsoid(center, radii, seg_n, rings, lump=lump, seed=rnd.uniform(0, 10)), weights, smooth=True)
    c = Vector(center)
    for _ in range(3):
        d = Vector((rnd.uniform(-1, 1), rnd.uniform(-1, 1), rnd.uniform(-0.3, 1))).normalized()
        off = Vector((d.x * radii[0], d.y * radii[1], d.z * radii[2])) * 0.75
        r = min(radii) * rnd.uniform(0.35, 0.55)
        mb.add(mat, *ellipsoid(c + off, (r, r, r * 0.85), 10, 7, lump=0.25, seed=rnd.uniform(0, 10)), weights, smooth=True)


def lightning_vein(mb, rnd, a, b, weights, jag=0.25, r=0.05):
    """A jagged emissive bolt from a to b over the body surface."""
    a, b = Vector(a), Vector(b)
    pts = [a]
    n = 6
    for i in range(1, n):
        p = a.lerp(b, i / n) + Vector((rnd.uniform(-jag, jag), rnd.uniform(-jag, jag), rnd.uniform(-jag, jag)))
        pts.append(p)
    pts.append(b)
    mb.add("matHurricaneLightning", *tube(pts, [r] * len(pts), 4), weights)
    if rnd.random() < 0.6:   # a fork
        k = rnd.randint(2, n - 2)
        tip = pts[k] + Vector((rnd.uniform(-0.8, 0.8), rnd.uniform(-0.8, 0.8), rnd.uniform(-0.6, 0.6)))
        mb.add("matHurricaneLightning", *tube([pts[k], pts[k].lerp(tip, 0.5) + Vector((0.1, 0, 0.1)), tip],
                                               [r * 0.8] * 3, 4), weights)


def build_body(mb, rnd):
    torso = w("pelvis", "spine", "chest")
    cloud(mb, rnd, (0, -0.6, 4.6), (2.5, 2.0, 1.8), w("chest", "spine"), 0.25)          # shoulder hump
    cloud(mb, rnd, (0, 0.8, 3.7), (1.6, 1.7, 1.2), torso, 0.22)                           # back
    cloud(mb, rnd, (0, 1.9, 3.2), (1.3, 1.2, 1.0), w("pelvis"), 0.2)                      # hips
    cloud(mb, rnd, (0, -0.2, 3.2), (1.3, 1.6, 0.9), torso, 0.2, mat="matHurricaneDark")   # belly
    for side in (1, -1):
        cloud(mb, rnd, (1.5 * side, -1.0, 4.4), (1.1, 1.1, 1.0), w("chest", f"upper_arm.{'l' if side > 0 else 'r'}"), 0.25)
    # Dorsal spines: dark bone ridges rising out of the cloud along the back (the image's crest).
    for i in range(7):
        t = i / 6
        base = Vector((0, -1.4 + t * 3.6, 5.6 - t * 1.6))
        tip = base + Vector((0, 0.5, 1.1 - t * 0.4))
        mb.add("matHurricaneBone", *cone(base, tip, 0.28 - t * 0.1, 6), w("chest", "spine", "pelvis"))
    for i in range(5):
        a = Vector((rnd.uniform(-1.5, 1.5), rnd.uniform(-1.5, 2.0), rnd.uniform(3.3, 5.3)))
        b = a + Vector((rnd.uniform(-1.2, 1.2), rnd.uniform(-1.5, 1.5), rnd.uniform(-1.0, 0.6)))
        lightning_vein(mb, rnd, a, b, torso)


def build_head(mb, rnd):
    head = w("head")
    jaw = w("jaw")
    cloud(mb, rnd, (0, -2.0, 4.6), (0.9, 0.9, 0.8), w("neck", "chest"), 0.2)               # neck
    mb.add("matHurricaneDark", *ellipsoid((0, -3.0, 4.1), (1.1, 1.4, 0.75), 14, 9, lump=0.08, seed=3), head, smooth=True)
    mb.add("matHurricaneCloud", *ellipsoid((0, -2.6, 4.5), (1.3, 1.2, 0.7), 14, 9, lump=0.2, seed=4), head, smooth=True)
    mb.add("matHurricaneDark", *ellipsoid((0, -3.1, 3.35), (0.9, 1.15, 0.38), 12, 7, lump=0.05, seed=5), jaw, smooth=True)
    # Fangs: upper row on the head, lower row on the jaw.
    for i in range(6):
        x = -0.5 + i * 0.2
        y = -3.55 + abs(x) * 0.5
        mb.add("matHurricaneBone", *cone((x, y, 3.75), (x * 1.05, y - 0.05, 3.35), 0.07, 5), head)
        mb.add("matHurricaneBone", *cone((x * 0.9, y + 0.1, 3.45), (x * 0.9, y + 0.05, 3.8), 0.06, 5), jaw)
    # Horns sweeping back, and cheek spikes.
    for side in (1, -1):
        horn = curve([(0.5 * side, -2.6, 4.6), (1.0 * side, -2.2, 5.3), (1.3 * side, -1.4, 5.7), (1.2 * side, -0.6, 5.6)], 3)
        mb.add("matHurricaneBone", *tube(horn, [0.22 - 0.2 * i / (len(horn) - 1) + 0.02 for i in range(len(horn))], 6), head)
        mb.add("matHurricaneBone", *cone((0.75 * side, -2.9, 4.0), (1.3 * side, -2.5, 3.9), 0.12, 5), head)
        # Glowing eyes.
        mb.add("matHurricaneEyes", *ellipsoid((0.42 * side, -3.55, 4.2), (0.16, 0.08, 0.1), 8, 6), head, smooth=True)
    lightning_vein(mb, rnd, (0.3, -2.4, 4.8), (0.6, -3.2, 4.4), head, 0.12, 0.04)
    lightning_vein(mb, rnd, (-0.3, -2.4, 4.8), (-0.6, -3.2, 4.4), head, 0.12, 0.04)


def build_arm(mb, rnd, side):
    s = "l" if side > 0 else "r"
    sh, el, wr, tip = (Vector(mx(v, side)) for v in (SHOULDER, ELBOW, WRIST, HAND_TIP))
    upper = w(f"upper_arm.{s}", f"chest")
    lower = w(f"lower_arm.{s}", f"upper_arm.{s}")
    hand = w(f"hand.{s}")
    for k in range(4):
        p = sh.lerp(el, k / 3.5)
        cloud(mb, rnd, tuple(p), (0.75 - k * 0.07, 0.7, 0.7), upper, 0.25, 12, 8)
    for k in range(4):
        p = el.lerp(wr, k / 3.5)
        cloud(mb, rnd, tuple(p), (0.55 - k * 0.05, 0.5, 0.55), lower, 0.22, 12, 8)
    mb.add("matHurricaneDark", *ellipsoid(tuple(wr + Vector((0, -0.2, -0.15))), (0.55, 0.6, 0.35), 12, 8, lump=0.1, seed=side), hand, smooth=True)
    # Three hooked claws.
    for k, dx in enumerate((-0.35, 0.0, 0.35)):
        start = wr + Vector((dx * side, -0.45, -0.25))
        c = curve([start, start + Vector((dx * 0.3 * side, -0.6, -0.15)), start + Vector((dx * 0.4 * side, -0.9, -0.55))], 3)
        mb.add("matHurricaneBone", *tube(c, [0.13 - 0.12 * i / (len(c) - 1) + 0.01 for i in range(len(c))], 6), hand)
    # Elbow spikes.
    mb.add("matHurricaneBone", *cone(tuple(el + Vector((0.2 * side, 0.3, 0.1))), tuple(el + Vector((0.6 * side, 1.2, 0.5))), 0.18, 6), lower)
    lightning_vein(mb, rnd, sh + Vector((0.3 * side, -0.3, 0.2)), el + Vector((0.2 * side, -0.3, 0)), upper)
    lightning_vein(mb, rnd, el + Vector((0.1 * side, -0.3, 0)), wr + Vector((0, -0.3, 0.2)), lower, 0.18, 0.04)


def build_leg(mb, rnd, side):
    s = "l" if side > 0 else "r"
    hip, knee, ankle, toe = (Vector(mx(v, side)) for v in (HIP, KNEE, ANKLE, TOE))
    thigh = w(f"thigh.{s}", "pelvis")
    calf = w(f"calf.{s}")
    foot = w(f"foot.{s}")
    for k in range(3):
        cloud(mb, rnd, tuple(hip.lerp(knee, k / 2.5)), (0.7 - k * 0.08, 0.7, 0.7), thigh, 0.25, 12, 8)
    mb.add("matHurricaneDark", *tube([knee, knee.lerp(ankle, 0.5), ankle], [0.42, 0.35, 0.3], 8), calf, smooth=True)
    mb.add("matHurricaneDark", *ellipsoid(tuple((ankle + toe) / 2), (0.45, 0.55, 0.3), 10, 7, lump=0.1, seed=side + 3), foot, smooth=True)
    for dx in (-0.25, 0.0, 0.25):
        base = toe + Vector((dx * side, 0.0, 0.15))
        mb.add("matHurricaneBone", *cone(base, base + Vector((dx * 0.3 * side, -0.55, -0.12)), 0.1, 5), foot)


def build_tail(mb, rnd):
    pts = [Vector(p) for p in TAIL]
    for i in range(len(pts) - 1):
        name = f"tail.{i + 1}"
        weights = w(name, f"tail.{i}" if i > 0 else "pelvis", f"tail.{i + 2}" if i < len(pts) - 2 else name)
        t = i / (len(pts) - 1)
        r = 0.75 * (1 - t) + 0.12
        cloud(mb, rnd, tuple(pts[i].lerp(pts[i + 1], 0.5)), (r, r * 1.3, r), weights, 0.22, 10, 7)
        if i % 2 == 0:
            lightning_vein(mb, rnd, pts[i] + Vector((0.2, 0, r * 0.8)), pts[i + 1] + Vector((-0.2, 0, r * 0.6)), weights, 0.15, 0.04)
    # Spiked tip: a fan of bone blades (the image's tail end).
    tip_w = w(f"tail.{len(pts) - 1}")
    end = pts[-1]
    for k in range(5):
        a = (k - 2) * 0.35
        mb.add("matHurricaneBone", *cone(end, end + Vector((math.sin(a) * 0.9, 0.9 + math.cos(a) * 0.3, 0.6 + math.cos(a) * 0.4)), 0.16, 5), tip_w)


def build_armature():
    arm = bpy.data.armatures.new("HurricaneArmature")
    obj = bpy.data.objects.new("HurricaneArmature", arm)
    bpy.context.scene.collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    for name, head, tail, parent in BONES:
        b = arm.edit_bones.new(name)
        b.head = head
        b.tail = tail
        b.roll = 0.0
        if parent:
            b.parent = arm.edit_bones[parent]
            b.use_connect = False
    bpy.ops.object.mode_set(mode="OBJECT")
    for pb in obj.pose.bones:
        pb.rotation_mode = "QUATERNION"
    return obj


def build(seed=31):
    rnd = random.Random(seed)
    materials = {name: make_material(name, rgb, em, rough, metal) for name, (rgb, em, rough, metal) in MATERIALS.items()}
    armature = build_armature()
    mb = MeshBuilder(MATERIALS.keys(), MESH_NAMES)
    build_body(mb, rnd)
    build_head(mb, rnd)
    for side in (1, -1):
        build_arm(mb, rnd, side)
        build_leg(mb, rnd, side)
    build_tail(mb, rnd)
    meshes = mb.build(materials, armature_obj=armature)
    return armature, meshes
