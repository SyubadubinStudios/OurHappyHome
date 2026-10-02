import bpy, sys
path = sys.argv[sys.argv.index("--")+1]
for o in list(bpy.data.objects): bpy.data.objects.remove(o)
bpy.ops.import_scene.gltf(filepath=path)
for o in bpy.context.scene.objects:
    info=f"{o.name} {o.type} parent={o.parent.name if o.parent else None} rot={tuple(round(v,3) for v in o.rotation_euler)} scale={tuple(round(v,3) for v in o.scale)}"
    if o.type=='MESH':
        me=o.data
        ws=[o.matrix_world@v.co for v in me.vertices]
        mn=[min(p[i] for p in ws) for i in range(3)]; mx=[max(p[i] for p in ws) for i in range(3)]
        info+=f" verts={len(me.vertices)} faces={len(me.polygons)} min={[round(x,3) for x in mn]} max={[round(x,3) for x in mx]} mats={[m.name for m in me.materials]}"
    print("OBJ", info)
for img in bpy.data.images: print("IMG", img.name, img.size[:])
