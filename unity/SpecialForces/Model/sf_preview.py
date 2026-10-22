"""Preview renders (Workbench, headless).

  blender -b --factory-startup -P sf_preview.py -- <out.png> boss [Clip:frame ...]
  blender -b --factory-startup -P sf_preview.py -- <out.png> prop <Name>
  blender -b --factory-startup -P sf_preview.py -- <out.png> map <x> <y> <z> <look_x> <look_y> <look_z>
"""
import math
import os
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
argv = sys.argv[sys.argv.index("--") + 1:]
out, kind = argv[0], argv[1]

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.engine = "BLENDER_WORKBENCH"
scene.display.shading.light = "STUDIO"
scene.display.shading.color_type = "MATERIAL"
scene.display.shading.show_shadows = os.environ.get("SF_SHADOWS", "1") == "1"
scene.display.shading.show_cavity = True
scene.render.resolution_x, scene.render.resolution_y = 1200, 800
scene.world = bpy.data.worlds.new("w")
scene.world.color = (0.12, 0.13, 0.16)


def camera(loc, target, lens=35):
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    cam.data.lens = lens
    cam.data.clip_end = 5000
    scene.collection.objects.link(cam)
    cam.location = loc
    cam.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    scene.camera = cam


def render(path):
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


if kind == "boss":
    import sf_boss
    import sf_boss_anim
    arm, meshes = sf_boss.build()
    shots = argv[2:] or ["rest"]
    made = dict((c.name, a) for c, a in sf_boss_anim.animate(arm))
    tiles = []
    for i, shot in enumerate(shots):
        if shot != "rest":
            name, frame = shot.split(":")
            arm.animation_data.action = made[name]
            scene.frame_set(int(frame))
        for angle, tag in ((-35, "a"), (90, "b")):
            a = math.radians(angle)
            camera((math.sin(a) * 22, -math.cos(a) * 22, 7), (0, 1.5, 3.0))
            p = out.replace(".png", f"_{i}{tag}.png")
            render(p)
            tiles.append(p)
    print("TILES", ",".join(tiles))
elif kind == "prop":
    import sf_props
    root = sf_props.BUILDERS[argv[2]]()
    objs = [o for o in root.children_recursive if o.type == "MESH"]
    lo = Vector((min(v[0] for o in objs for v in o.bound_box), min(v[1] for o in objs for v in o.bound_box), min(v[2] for o in objs for v in o.bound_box)))
    hi = Vector((max(v[0] for o in objs for v in o.bound_box), max(v[1] for o in objs for v in o.bound_box), max(v[2] for o in objs for v in o.bound_box)))
    size = (hi - lo).length
    c = (lo + hi) / 2
    camera(c + Vector((size * 0.8, -size * 1.0, size * 0.5)), c)
    render(out)
elif kind == "map":
    import sf_map
    sf_map.build()
    x, y, z, lx, ly, lz = (float(v) for v in argv[2:8])
    camera((x, y, z), (lx, ly, lz), 24)
    scene.display.shading.light = "STUDIO"
    render(out)
