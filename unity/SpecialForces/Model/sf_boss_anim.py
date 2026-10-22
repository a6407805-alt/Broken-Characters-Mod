"""The Hurricane's animations, keyed in code (forward kinematics: per-bone local rotations in degrees).

Clips (names = Animator states, see SpecialForcesBuilder.cs):
  Idle (loop)  breathing, swaying tail, the jaw working
  Walk (loop)  a heavy diagonal gait (front-left with back-right), body bobbing
  Claw         right claw raised and swept across (Thunder Claw; the hit lands at 45% of the clip)
  Slam         both fists raised and slammed down (Storm Roar's shockwave lands at 55%)
  Roar         rears up, head back, jaw wide (spawn, phase changes, Squall)
  Cast         rears up with arms spread, held (Lightning Barrage, Cyclone)
  Death        collapses onto its side and stays down

Pose dict: bone -> (x, y, z) degrees about the bone's own axes (rest pose = all zero), plus "base_z" (metres the
whole body moves up/down) and "base_rx" (degrees the whole body pitches; + = nose up).
"""
import math

import bpy
from mathutils import Euler

FPS = 30
TAIL_BONES = [f"tail.{i}" for i in range(1, 7)]


class Clip:
    def __init__(self, name, length, fn, loop=False):
        self.name, self.length, self.fn, self.loop = name, length, fn, loop


def tail_sway(t, amp=12.0, speed=0.6):
    return {b: (3 * math.sin(2 * math.pi * speed * t + i * 0.5), 0.0,
                amp * math.sin(2 * math.pi * speed * t - i * 0.7) * (0.5 + i * 0.15))
            for i, b in enumerate(TAIL_BONES)}


def merge(*dicts):
    out = {}
    for d in dicts:
        for k, v in d.items():
            if k in out and isinstance(v, tuple):
                out[k] = tuple(a + b for a, b in zip(out[k], v))
            else:
                out[k] = v
    return out


def keyed(keys, t):
    """keys: [(time, pose)]; smooth (ease in/out) interpolation between poses."""
    if t <= keys[0][0]:
        return keys[0][1]
    for (t0, p0), (t1, p1) in zip(keys, keys[1:]):
        if t <= t1:
            k = (t - t0) / max(1e-6, t1 - t0)
            k = k * k * (3 - 2 * k)
            out = {}
            for b in set(p0) | set(p1):
                a, c = p0.get(b, 0.0 if b.startswith("base") else (0, 0, 0)), p1.get(b, 0.0 if b.startswith("base") else (0, 0, 0))
                out[b] = a + (c - a) * k if isinstance(a, float) else tuple(x + (y - x) * k for x, y in zip(a, c))
            return out
    return keys[-1][1]


def idle(t):
    breath = math.sin(2 * math.pi * t / 3.0)
    return merge({
        "spine": (2 * breath, 0, 0), "chest": (2.5 * breath, 0, 0), "neck": (-2 * breath, 0, 3 * math.sin(2 * math.pi * t / 6.0)),
        "head": (-1.5 * breath, 0, 4 * math.sin(2 * math.pi * t / 6.0 + 1)), "jaw": (6 + 4 * math.sin(2 * math.pi * t / 1.5), 0, 0),
        "base_z": 0.06 * breath,
    }, tail_sway(t, 10, 1 / 3.0))


def walk(t):
    L = 2.4
    ph = 2 * math.pi * t / L
    s1, s2 = math.sin(ph), math.sin(ph + math.pi)
    lift1, lift2 = max(0.0, math.cos(ph)), max(0.0, math.cos(ph + math.pi))
    return merge({
        # Diagonal pairs: front-left with back-right.
        "upper_arm.l": (24 * s1, 0, 0), "lower_arm.l": (-18 * lift1, 0, 0), "hand.l": (-10 * s1, 0, 0),
        "upper_arm.r": (24 * s2, 0, 0), "lower_arm.r": (-18 * lift2, 0, 0), "hand.r": (-10 * s2, 0, 0),
        "thigh.r": (20 * s1, 0, 0), "calf.r": (16 * lift1, 0, 0),
        "thigh.l": (20 * s2, 0, 0), "calf.l": (16 * lift2, 0, 0),
        "spine": (0, 0, 4 * s1), "chest": (0, 0, -5 * s1), "neck": (0, 0, 3 * s1), "head": (2 * math.sin(2 * ph), 0, 0),
        "jaw": (8, 0, 0),
        "base_z": 0.18 * abs(math.sin(ph)) - 0.1,
    }, tail_sway(t, 14, 1 / L))


CLAW = [
    (0.0, {"jaw": (8, 0, 0)}),
    (0.35, {"upper_arm.r": (-75, 0, 25), "lower_arm.r": (-40, 0, 0), "hand.r": (-30, 0, 0), "chest": (-8, 0, 12),
            "spine": (-4, 0, 6), "neck": (0, 0, -10), "jaw": (25, 0, 0), "base_rx": 4.0}),
    (0.6, {"upper_arm.r": (40, 0, -35), "lower_arm.r": (-10, 0, 0), "hand.r": (20, 0, 0), "chest": (10, 0, -18),
           "spine": (5, 0, -8), "neck": (6, 0, 12), "jaw": (30, 0, 0), "base_rx": -3.0}),
    (1.0, {"upper_arm.r": (20, 0, -20), "lower_arm.r": (-8, 0, 0), "chest": (4, 0, -8), "jaw": (12, 0, 0)}),
    (1.4, {"jaw": (8, 0, 0)}),
]

SLAM = [
    (0.0, {}),
    (0.55, {"upper_arm.l": (-110, 0, -10), "upper_arm.r": (-110, 0, 10), "lower_arm.l": (-50, 0, 0), "lower_arm.r": (-50, 0, 0),
            "spine": (-14, 0, 0), "chest": (-14, 0, 0), "head": (-15, 0, 0), "jaw": (35, 0, 0), "base_rx": 14.0, "base_z": 0.5}),
    (0.95, {"upper_arm.l": (35, 0, 0), "upper_arm.r": (35, 0, 0), "lower_arm.l": (-5, 0, 0), "lower_arm.r": (-5, 0, 0),
            "spine": (10, 0, 0), "chest": (12, 0, 0), "head": (10, 0, 0), "jaw": (20, 0, 0), "base_rx": -6.0, "base_z": -0.4}),
    (1.4, {"upper_arm.l": (25, 0, 0), "upper_arm.r": (25, 0, 0), "chest": (8, 0, 0), "base_z": -0.25, "jaw": (12, 0, 0)}),
    (1.8, {}),
]

ROAR = [
    (0.0, {}),
    (0.5, {"spine": (-18, 0, 0), "chest": (-16, 0, 0), "neck": (-20, 0, 0), "head": (-25, 0, 0), "jaw": (42, 0, 0),
           "upper_arm.l": (-30, 0, -20), "upper_arm.r": (-30, 0, 20), "lower_arm.l": (-20, 0, 0), "lower_arm.r": (-20, 0, 0),
           "base_rx": 16.0, "base_z": 0.6}),
    (1.9, {"spine": (-20, 0, 0), "chest": (-18, 0, 0), "neck": (-22, 0, 0), "head": (-30, 0, 0), "jaw": (45, 0, 0),
           "upper_arm.l": (-35, 0, -25), "upper_arm.r": (-35, 0, 25), "lower_arm.l": (-22, 0, 0), "lower_arm.r": (-22, 0, 0),
           "base_rx": 18.0, "base_z": 0.7}),
    (2.5, {}),
]

CAST = [
    (0.0, {}),
    (0.45, {"spine": (-12, 0, 0), "chest": (-14, 0, 0), "neck": (-12, 0, 0), "head": (-28, 0, 0), "jaw": (30, 0, 0),
            "upper_arm.l": (-40, 0, -55), "upper_arm.r": (-40, 0, 55), "lower_arm.l": (-10, 0, 0), "lower_arm.r": (-10, 0, 0),
            "base_rx": 12.0, "base_z": 0.45}),
    (1.6, {"spine": (-13, 0, 0), "chest": (-15, 0, 0), "neck": (-12, 0, 0), "head": (-30, 0, 0), "jaw": (34, 0, 0),
           "upper_arm.l": (-45, 0, -60), "upper_arm.r": (-45, 0, 60), "lower_arm.l": (-12, 0, 0), "lower_arm.r": (-12, 0, 0),
           "base_rx": 12.0, "base_z": 0.45}),
    (2.0, {}),
]

DEATH = [
    (0.0, {}),
    (0.6, {"head": (-20, 0, 0), "jaw": (40, 0, 0), "spine": (-10, 0, 0), "base_rx": 10.0, "base_z": 0.3}),
    (1.6, {"upper_arm.l": (40, 0, 0), "upper_arm.r": (50, 0, 0), "lower_arm.l": (-60, 0, 0), "lower_arm.r": (-70, 0, 0),
           "thigh.l": (30, 0, 0), "thigh.r": (35, 0, 0), "calf.l": (40, 0, 0), "calf.r": (45, 0, 0),
           "head": (25, 0, 20), "neck": (15, 0, 15), "jaw": (30, 0, 0), "base_z": -2.2, "base_rx": -4.0, "base_ry": 18.0}),
    (3.0, {"upper_arm.l": (42, 0, 0), "upper_arm.r": (52, 0, 0), "lower_arm.l": (-62, 0, 0), "lower_arm.r": (-72, 0, 0),
           "thigh.l": (32, 0, 0), "thigh.r": (37, 0, 0), "calf.l": (42, 0, 0), "calf.r": (47, 0, 0),
           "head": (30, 0, 25), "neck": (18, 0, 18), "jaw": (34, 0, 0), "base_z": -2.4, "base_rx": -4.0, "base_ry": 22.0}),
]


def clips():
    def from_keys(keys, idle_mix=True):
        return lambda t: merge(keyed(keys, t), tail_sway(t, 8, 0.5) if idle_mix else {})
    return [
        Clip("Idle", 3.0, idle, True),
        Clip("Walk", 2.4, walk, True),
        Clip("Claw", 1.4, from_keys(CLAW)),
        Clip("Slam", 1.8, from_keys(SLAM)),
        Clip("Roar", 2.5, from_keys(ROAR)),
        Clip("Cast", 2.0, from_keys(CAST)),
        Clip("Death", 3.0, from_keys(DEATH, False)),
    ]


def apply_pose(armature, pose):
    for pb in armature.pose.bones:
        pb.rotation_quaternion = (1, 0, 0, 0)
        pb.location = (0, 0, 0)
    for name, value in pose.items():
        if name.startswith("base"):
            continue
        pb = armature.pose.bones.get(name)
        if pb is None:
            continue
        pb.rotation_quaternion = Euler(tuple(math.radians(v) for v in value), "XYZ").to_quaternion()
    base = armature.pose.bones["base"]
    # base bone points up (+Z): its local Y is world Z, local X is world X, local Z is world -Y.
    base.location = (0, pose.get("base_z", 0.0), 0)
    # + base_rx = nose up: about the base bone's local X that is a negative rotation (checked in preview renders).
    base.rotation_quaternion = Euler((-math.radians(pose.get("base_rx", 0.0)), 0, math.radians(pose.get("base_ry", 0.0))), "XYZ").to_quaternion()


def animate(armature, only=None):
    made = []
    armature.animation_data_create()
    for clip in clips():
        if only and clip.name not in only:
            continue
        action = bpy.data.actions.new(clip.name)
        action.use_fake_user = True
        armature.animation_data.action = action
        frames = int(round(clip.length * FPS))
        for f in range(frames + 1):
            t = f / FPS
            if clip.loop:
                t = t % clip.length if f < frames else 0.0
            apply_pose(armature, clip.fn(t))
            for pb in armature.pose.bones:
                pb.keyframe_insert("rotation_quaternion", frame=f + 1)
                if pb.name == "base":
                    pb.keyframe_insert("location", frame=f + 1)
        made.append((clip, action))
    armature.animation_data.action = None
    return made
