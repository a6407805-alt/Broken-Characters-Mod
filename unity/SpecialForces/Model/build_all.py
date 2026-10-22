"""Builds every Special Forces model and exports the FBX files Unity needs.

    blender -b --factory-startup -P build_all.py -- [output_dir] [only...]

Writes StormValley.fbx (the map), ControlTower/StormNest/BarrierWall/SupplyDepot/Lander/Floodlight.fbx (props) and
Hurricane.fbx (the boss, rigged, with every animation). Default output: ../Unity/.
"""
import os
import sys
import time

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import sf_boss  # noqa: E402
import sf_boss_anim  # noqa: E402
import sf_map  # noqa: E402
import sf_props  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
out_dir = argv[0] if argv else os.path.join(HERE, "..", "Unity")
only = set(argv[1:])
os.makedirs(out_dir, exist_ok=True)

COMMON = dict(
    use_selection=True,
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Z",
    axis_up="Y",
    use_mesh_modifiers=False,
    mesh_smooth_type="FACE",
    add_leaf_bones=False,
    primary_bone_axis="Y",
    secondary_bone_axis="X",
    use_armature_deform_only=False,
)


def select_only(objects):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]


def wanted(name):
    return not only or name in only


if wanted("StormValley"):
    t0 = time.time()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    root, objs = sf_map.build()
    select_only(objs)
    path = os.path.join(out_dir, "StormValley.fbx")
    bpy.ops.export_scene.fbx(filepath=path, object_types={"EMPTY", "MESH"}, bake_anim=False, **COMMON)
    print(f"[sf] exported {path} ({len(objs)} objects, {time.time() - t0:.0f} s)")

for name, builder in sf_props.BUILDERS.items():
    if not wanted(name):
        continue
    bpy.ops.wm.read_factory_settings(use_empty=True)
    root = builder()
    objects = [root, *root.children_recursive]
    select_only(objects)
    path = os.path.join(out_dir, name + ".fbx")
    bpy.ops.export_scene.fbx(filepath=path, object_types={"EMPTY", "MESH"}, bake_anim=False, **COMMON)
    print(f"[sf] exported {path} ({len(objects)} objects)")

if wanted("Hurricane"):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.render.fps = sf_boss_anim.FPS
    armature, meshes = sf_boss.build()
    made = sf_boss_anim.animate(armature)
    select_only([armature, *meshes])
    path = os.path.join(out_dir, "Hurricane.fbx")
    bpy.ops.export_scene.fbx(filepath=path, object_types={"ARMATURE", "MESH"}, bake_anim=True,
                             bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
                             bake_anim_force_startend_keying=True, bake_anim_step=1.0, bake_anim_simplify_factor=0.0,
                             **COMMON)
    print(f"[sf] exported {path} with {len(made)} animations: " + ", ".join(c.name for c, _ in made))
