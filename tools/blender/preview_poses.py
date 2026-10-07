"""Renders one frame of several actions of a rigged .glb, one PNG per pose.
blender -b --python preview_poses.py -- <src.glb> <out_prefix> <view_size_m> <Action:frame> [Action:frame ...]"""
import bpy, sys, mathutils
args = sys.argv[sys.argv.index("--") + 1:]
path, prefix, size, poses = args[0], args[1], float(args[2]), args[3:]
for o in list(bpy.data.objects): bpy.data.objects.remove(o)
bpy.ops.import_scene.gltf(filepath=path)
rig = [o for o in bpy.context.scene.objects if o.type == 'ARMATURE'][0]
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); bpy.context.scene.collection.objects.link(cam)
cam.data.type = 'ORTHO'; cam.data.ortho_scale = size
d = mathutils.Vector((1, -0.6, 0.25)).normalized()
cam.location = mathutils.Vector((0, 0, size * 0.4)) + d * 5
cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
sc = bpy.context.scene; sc.camera = cam
sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", 'SUN')); sc.collection.objects.link(sun); sun.rotation_euler = (0.8, 0.2, 0.3)
sc.render.engine = 'BLENDER_EEVEE'; sc.render.resolution_x = 320; sc.render.resolution_y = 320
for pose in poses:
    name, frame = pose.split(":")
    act = [a for a in bpy.data.actions if a.name.startswith(name)][0]
    rig.animation_data.action = act
    try: rig.animation_data.action_slot = act.slots[0]
    except Exception: pass
    sc.frame_set(int(frame))
    sc.render.filepath = f"{prefix}_{name}_{frame}.png"
    bpy.ops.render.render(write_still=True)
