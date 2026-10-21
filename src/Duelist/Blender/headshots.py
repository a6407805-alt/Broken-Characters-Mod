"""Renders close-up headshots (front / side / back-3/4) of the Duelist into Documents/DuelistModels."""
import bpy, os, math
from mathutils import Vector

OUT = os.path.join(os.path.expanduser("~"), "Documents", "DuelistModels")
scene = bpy.context.scene
old_cam, old_path = scene.camera, scene.render.filepath
old_res = (scene.render.resolution_x, scene.render.resolution_y)
cd = bpy.data.cameras.new("DU_HeadCamData")
cd.lens = 85
cam = bpy.data.objects.new("DU_HeadCam", cd)
scene.collection.objects.link(cam)
scene.camera = cam
scene.render.resolution_x = scene.render.resolution_y = 700
fx = bpy.data.collections.get("Duelist_FX")
hidden = []
if fx:
    for o in fx.objects:
        if o.type == "MESH" and not o.hide_render:
            o.hide_render = True
            hidden.append(o)
target = Vector((0, 0, 1.72))
shots = {"front": (0, -1.6, 1.75), "side": (1.6, 0.0, 1.75), "back34": (-1.1, 1.1, 1.85)}
for name, loc in shots.items():
    cam.location = loc
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = os.path.join(OUT, f"head_{name}.png")
    bpy.ops.render.render(write_still=True)
for o in hidden:
    o.hide_render = False
scene.camera = old_cam
scene.render.filepath = old_path
scene.render.resolution_x, scene.render.resolution_y = old_res
bpy.data.objects.remove(cam, do_unlink=True)
print("HEADSHOTS DONE")
