"""Decimates a Rodin prop, shrinks its textures and re-exports a light GLB.
blender -b --python optimize_prop.py -- <src.glb> <dst.glb> [target_faces] [texture_size]"""
import bpy, sys

def optimize(src, dst, target_faces=6000, texture_size=1024):
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for img in list(bpy.data.images):
        if img.users == 0: bpy.data.images.remove(img)
    bpy.ops.import_scene.gltf(filepath=src)
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    for m in meshes:
        bpy.context.view_layer.objects.active = m
        faces = len(m.data.polygons)
        if faces > target_faces:
            mod = m.modifiers.new("dec", "DECIMATE")
            mod.ratio = target_faces / faces
            bpy.ops.object.modifier_apply(modifier=mod.name)
        for p in m.data.polygons: p.use_smooth = True
    for img in bpy.data.images:
        if img.size[0] > texture_size:
            img.scale(texture_size, texture_size)
    bpy.ops.export_scene.gltf(filepath=dst, export_format="GLB", export_image_format="JPEG",
                              export_jpeg_quality=85, export_apply=True)
    print("optimized", dst, sum(len(m.data.polygons) for m in meshes))

if "--" in sys.argv:
    a = sys.argv[sys.argv.index("--") + 1:]
    optimize(a[0], a[1], int(a[2]) if len(a) > 2 else 6000, int(a[3]) if len(a) > 3 else 1024)
