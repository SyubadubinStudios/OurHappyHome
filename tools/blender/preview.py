import bpy, sys, math, mathutils
args = sys.argv[sys.argv.index("--")+1:]
path, out = args[0], args[1]
for o in list(bpy.data.objects): bpy.data.objects.remove(o)
if path.endswith(".blend"):
    pass
bpy.ops.import_scene.gltf(filepath=path)
objs=[o for o in bpy.context.scene.objects if o.type=='MESH']
pts=[o.matrix_world@mathutils.Vector(c) for o in objs for c in o.bound_box]
mn=mathutils.Vector([min(p[i] for p in pts) for i in range(3)]); mx=mathutils.Vector([max(p[i] for p in pts) for i in range(3)])
c=(mn+mx)/2; size=max(mx-mn)
cam=bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); bpy.context.scene.collection.objects.link(cam)
cam.data.type='ORTHO'; cam.data.ortho_scale=size*1.15
view = args[2] if len(args)>2 else "front"
d = {"front":mathutils.Vector((0,-1,0)),"side":mathutils.Vector((1,0,0)),"back":mathutils.Vector((0,1,0))}[view]
cam.location=c+d*size*3
cam.rotation_euler=(-d).to_track_quat('-Z','Z').to_euler()
bpy.context.scene.camera=cam
sun=bpy.data.objects.new("sun", bpy.data.lights.new("sun",'SUN')); bpy.context.scene.collection.objects.link(sun); sun.rotation_euler=(0.8,0.2,0.3)
sc=bpy.context.scene
try: sc.render.engine='BLENDER_EEVEE'
except TypeError as e: print(e)
sc.render.resolution_x=512; sc.render.resolution_y=512
sc.render.filepath=out
w=bpy.data.worlds.new("w"); sc.world=w; w.color=(0.8,0.8,0.8)
bpy.ops.render.render(write_still=True)
