import bpy, sys, math, mathutils
args = sys.argv[sys.argv.index("--")+1:]
path, out, action, frame = args[0], args[1], args[2], int(args[3])
for o in list(bpy.data.objects): bpy.data.objects.remove(o)
bpy.ops.import_scene.gltf(filepath=path)
rig=[o for o in bpy.context.scene.objects if o.type=='ARMATURE'][0]
print("actions", [a.name for a in bpy.data.actions])
act=[a for a in bpy.data.actions if a.name.startswith(action)][0]
rig.animation_data.action=act
try:
    rig.animation_data.action_slot = act.slots[0]
except Exception as e: print(e)
bpy.context.scene.frame_set(frame)
cam=bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); bpy.context.scene.collection.objects.link(cam)
cam.data.type='ORTHO'; cam.data.ortho_scale=2.2
d=mathutils.Vector((-0.7,-1,0.15)).normalized()
cam.location=mathutils.Vector((0,0,0.9))+d*5
cam.rotation_euler=(-d).to_track_quat('-Z','Y').to_euler()
bpy.context.scene.camera=cam
sun=bpy.data.objects.new("sun", bpy.data.lights.new("sun",'SUN')); bpy.context.scene.collection.objects.link(sun); sun.rotation_euler=(0.8,0.2,0.3)
sc=bpy.context.scene; sc.render.engine='BLENDER_EEVEE'
sc.render.resolution_x=400; sc.render.resolution_y=400; sc.render.filepath=out
bpy.ops.render.render(write_still=True)
