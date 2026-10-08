"""
Our Happy Home - props and background characters modelled directly in Blender
(used when no Rodin credits are left). Everything is built from primitives with
flat PBR colours, faces +Z in glTF (-Y in Blender), sits on the floor and is
exported as GLB.

Through Blender MCP (execute_blender_code):
  ns = {"__file__": r".../tools/blender/model_props.py"}
  exec(open(ns["__file__"]).read(), ns)
  ns["build_all"](r".../art/blender")            # every prop + person
  ns["build"]("boat", r".../art/blender/boat.glb")

People are built in a T-pose so tools/blender/rig_character.py can rig them.
"""

import math
import os

import bpy
from mathutils import Euler, Matrix, Vector

_materials = {}


# ------------------------------------------------------------------ helpers


def clear():
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for coll in (bpy.data.meshes, bpy.data.materials, bpy.data.images, bpy.data.armatures, bpy.data.actions):
        for block in list(coll):
            if block.users == 0 or coll is bpy.data.actions:
                coll.remove(block)
    _materials.clear()


def mat(hex_color, rough=0.7, metal=0.0, emit=0.0):
    key = (hex_color, rough, metal, emit)
    if key in _materials:
        return _materials[key]
    h = hex_color.lstrip("#")
    srgb = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    lin = [c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4 for c in srgb]
    m = bpy.data.materials.new(f"m{len(_materials)}_{h}")
    m.use_nodes = True
    bsdf = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = (*lin, 1.0)
    bsdf.inputs["Roughness"].default_value = rough
    bsdf.inputs["Metallic"].default_value = metal
    if emit > 0:
        for name in ("Emission Color", "Emission"):
            if name in bsdf.inputs:
                bsdf.inputs[name].default_value = (*lin, 1.0)
                break
        bsdf.inputs["Emission Strength"].default_value = emit
    _materials[key] = m
    return m


def _finish(obj, material, smooth):
    obj.data.materials.append(material)
    if smooth:
        for p in obj.data.polygons:
            p.use_smooth = True
    return obj


def box(size, loc, material, rot=(0, 0, 0), bevel=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel > 0:
        mod = o.modifiers.new("bevel", "BEVEL")
        mod.width = bevel
        mod.segments = 2
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return _finish(o, material, False)


def cyl(radius, depth, loc, material, rot=(0, 0, 0), verts=16, radius2=None):
    if radius2 is None:
        bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=depth, location=loc, rotation=rot)
    else:
        bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=radius, radius2=radius2, depth=depth, location=loc, rotation=rot)
    return _finish(bpy.context.active_object, material, True)


def cone(radius, depth, loc, material, rot=(0, 0, 0), verts=16):
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=radius, radius2=0, depth=depth, location=loc, rotation=rot)
    return _finish(bpy.context.active_object, material, True)


def sphere(radius, loc, material, scale=(1, 1, 1), segments=16):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=max(6, segments // 2), radius=radius, location=loc)
    o = bpy.context.active_object
    o.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return _finish(o, material, True)


def capsule(p0, p1, radius, material, verts=12):
    """A cylinder with round caps between two points."""
    a, b = Vector(p0), Vector(p1)
    d = b - a
    mid = (a + b) / 2
    rot = d.to_track_quat("Z", "Y").to_euler()
    parts = [cyl(radius, d.length, mid, material, rot=rot, verts=verts),
             sphere(radius, a, material, segments=verts), sphere(radius, b, material, segments=verts)]
    return parts


def join(objs, name):
    objs = [o for o in objs if o is not None]
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    if len(objs) > 1:
        bpy.ops.object.join()
    o = bpy.context.active_object
    o.name = name
    return o


def export(path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", use_selection=True, export_apply=True, export_yup=True)
    print("exported", path)


def all_parts():
    return [o for o in bpy.context.scene.objects if o.type == "MESH"]


# ------------------------------------------------------------------- props


def boat():
    """Jukung: a slim painted fishing boat with bamboo outriggers (front is -Y)."""
    white, blue, red, wood = mat("#F4F1E8"), mat("#2E6FBF"), mat("#D7263D"), mat("#9C6B3F", 0.85)
    parts = []
    hull = sphere(1, (0, 0, 0.35), blue, scale=(0.55, 2.6, 0.42), segments=24)
    bpy.context.view_layer.objects.active = hull
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="DESELECT")
    bpy.ops.object.mode_set(mode="OBJECT")
    for v in hull.data.vertices:  # flatten the top so the boat is open
        if v.co.z > 0.55:
            v.co.z = 0.55
    parts.append(hull)
    parts.append(box((1.0, 4.6, 0.06), (0, 0, 0.56), white))
    parts.append(box((1.02, 4.7, 0.08), (0, 0, 0.38), red))
    parts.append(box((0.9, 0.12, 0.05), (0, -0.8, 0.62), wood))
    parts.append(box((0.9, 0.12, 0.05), (0, 0.9, 0.62), wood))
    for y in (-1.0, 1.0):
        parts.append(cyl(0.035, 3.6, (0, y, 0.75), wood, rot=(0, math.pi / 2, 0), verts=8))
    for x in (-1.75, 1.75):
        parts.append(cyl(0.07, 3.2, (x, 0, 0.32), mat("#E9D8A6", 0.8), rot=(math.pi / 2, 0, 0), verts=10))
        for y in (-1.0, 1.0):
            parts.append(cyl(0.03, 0.55, (x * 0.93, y, 0.52), wood, rot=(0, math.radians(x * 12), 0), verts=6))
    # a little eye painted on the bow
    parts.append(sphere(0.09, (0.5, -2.0, 0.42), white, segments=10))
    parts.append(sphere(0.045, (0.55, -2.05, 0.42), mat("#1E1E1E"), segments=8))
    parts.append(cyl(0.035, 1.8, (0, -0.4, 1.4), wood, verts=8))
    return join(parts, "boat")


def lifeguard_tower():
    wood, red, white = mat("#B5835A", 0.85), mat("#E63946"), mat("#FFFFFF")
    parts = []
    for x in (-0.8, 0.8):
        for y in (-0.8, 0.8):
            parts.append(cyl(0.07, 2.2, (x, y, 1.1), wood, verts=8))
    parts.append(box((2.0, 2.0, 0.12), (0, 0, 2.25), wood))
    parts.append(box((1.6, 1.6, 1.0), (0, 0.1, 2.85), white))
    parts.append(box((1.62, 0.05, 0.35), (0, -0.71, 2.55), red))
    parts.append(box((1.0, 0.06, 0.55), (0, -0.71, 3.05), mat("#9FD8F2", 0.1)))
    parts.append(cone(1.45, 0.7, (0, 0.1, 3.7), red, verts=4))
    for i in range(6):  # ladder
        parts.append(box((0.7, 0.06, 0.05), (0, -1.15, 0.3 + i * 0.35), wood))
    parts.append(box((0.06, 0.06, 2.2), (-0.35, -1.15, 1.0), wood))
    parts.append(box((0.06, 0.06, 2.2), (0.35, -1.15, 1.0), wood))
    parts.append(cyl(0.025, 1.4, (0.75, 0.75, 4.4), mat("#DDDDDD", 0.3, 0.6), verts=6))
    parts.append(box((0.5, 0.02, 0.3), (1.0, 0.75, 4.9), red))
    return join(parts, "lifeguard-tower")


def deck_chair():
    """Beach umbrella with two striped deck chairs."""
    wood, cloth1, cloth2 = mat("#C8A165", 0.8), mat("#2A9D8F"), mat("#F4A261")
    parts = []
    for x, cloth in ((-0.55, cloth1), (0.55, cloth2)):
        parts.append(box((0.55, 1.1, 0.05), (x, 0.15, 0.32), cloth, rot=(math.radians(-8), 0, 0)))
        parts.append(box((0.55, 0.65, 0.05), (x, -0.55, 0.6), cloth, rot=(math.radians(55), 0, 0)))
        for dx in (-0.25, 0.25):
            parts.append(box((0.04, 1.5, 0.04), (x + dx, 0, 0.2), wood))
    parts.append(cyl(0.035, 2.4, (0, 0.6, 1.2), mat("#FFFFFF", 0.4), verts=8))
    for i in range(8):  # striped umbrella made of wedges
        a = i * math.pi / 4
        c = mat("#E63946") if i % 2 == 0 else mat("#FFFFFF")
        w = cone(1.3, 0.45, (0, 0.6, 2.3), c, verts=3)
        w.rotation_euler = (0, 0, a)
        parts.append(w)
    return join(parts, "deck-chair")


def dome_tent(color="#F4A259"):
    cloth, dark, trim = mat(color, 0.8), mat("#3B2F2A"), mat("#FFFFFF", 0.6)
    parts = []
    shell = sphere(1, (0, 0, 0), cloth, scale=(1.3, 1.6, 1.1), segments=20)
    door = sphere(1, (0, -1.05, 0), dark, scale=(0.5, 0.6, 0.72), segments=16)
    for o in (shell, door):
        for v in o.data.vertices:
            v.co.z = max(v.co.z, 0.0)
    parts += [shell, door]
    parts.append(sphere(0.12, (0, 0, 1.08), trim, segments=10))
    parts.append(box((2.8, 3.4, 0.03), (0, 0, 0.015), mat("#6B8E5A", 0.95)))
    return join(parts, "dome-tent")


def log_bench():
    bark, cut = mat("#7A5230", 0.95), mat("#D7B377", 0.8)
    parts = [cyl(0.22, 1.8, (0, 0, 0.42), bark, rot=(0, math.pi / 2, 0), verts=12)]
    parts.append(cyl(0.2, 0.02, (-0.91, 0, 0.42), cut, rot=(0, math.pi / 2, 0), verts=12))
    parts.append(cyl(0.2, 0.02, (0.91, 0, 0.42), cut, rot=(0, math.pi / 2, 0), verts=12))
    for x in (-0.6, 0.6):
        parts.append(cyl(0.16, 0.25, (x, 0, 0.12), bark, verts=10))
    return join(parts, "log-bench")


def rocks():
    stone = [mat("#8A8F98", 0.95), mat("#A2A8B0", 0.95), mat("#757B84", 0.95)]
    parts = []
    for i, (x, y, r, sz) in enumerate(((0, 0, 0.8, 0.65), (0.9, 0.3, 0.5, 0.45), (-0.7, 0.4, 0.45, 0.4), (0.3, -0.7, 0.35, 0.3))):
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=r, location=(x, y, sz * 0.5))
        o = bpy.context.active_object
        o.scale = (1.0, 0.85, sz / r * 0.8)
        bpy.ops.object.transform_apply(scale=True)
        for v in o.data.vertices:
            v.co *= 1 + 0.12 * math.sin(v.co.x * 7 + v.co.y * 5)
        parts.append(_finish(o, stone[i % 3], False))
    return join(parts, "rocks")


def flag_pole():
    """A flag pole with the red and white flag (camp ground ceremony)."""
    parts = [cyl(0.05, 5, (0, 0, 2.5), mat("#DDDDDD", 0.3, 0.6), verts=8),
             sphere(0.09, (0, 0, 5.05), mat("#F4C430", 0.3, 0.7), segments=10),
             box((0.9, 0.02, 0.3), (0.47, 0, 4.7), mat("#E63946", 0.6)),
             box((0.9, 0.02, 0.3), (0.47, 0, 4.4), mat("#FFFFFF", 0.6)),
             cyl(0.5, 0.25, (0, 0, 0.12), mat("#A0A0A0", 0.9), verts=12)]
    return join(parts, "flag-pole")


# ------------------------------------------------------------------- people

# skin, top, bottom, hair, extras
PEOPLE = {
    "tourist-man": ("#E2B48C", "#F28C38", "#2E86C1", "#3B2A20", "shirt-flowers sunhat"),
    "tourist-woman": ("#C99A72", "#F7B7CF", "#F7B7CF", "#2B1D14", "dress sunhat"),
    "tourist-kid": ("#E8B894", "#FFD23F", "#E63946", "#3B2A20", "swimring"),
    "scout": ("#D9A779", "#B08D57", "#6B4F2E", "#2B1D14", "scarf beret"),
    "scout-girl": ("#E2B48C", "#B08D57", "#6B4F2E", "#2B1D14", "scarf ponytail"),
    "hiker": ("#C99A72", "#3A7D44", "#5C5C5C", "#3B2A20", "backpack cap"),
}


def person(name, height=1.6):
    """A friendly stylised person in a T-pose (arms out along X, facing -Y)."""
    skin_hex, top_hex, bottom_hex, hair_hex, extras = PEOPLE[name]
    skin, top, bottom, hair = mat(skin_hex, 0.6), mat(top_hex, 0.75), mat(bottom_hex, 0.8), mat(hair_hex, 0.85)
    shoe, eye = mat("#3B2F2A", 0.6), mat("#1E1E1E", 0.3)
    child = name.endswith("kid")
    H = 1.0  # build at 1.0 and scale at the end
    head_r = 0.13 if not child else 0.155
    parts = []
    # legs
    hip_z, knee_z = 0.47, 0.25
    for s in (-1, 1):
        parts += capsule((s * 0.07, 0, hip_z), (s * 0.075, 0, knee_z), 0.05, bottom if "dress" not in extras else skin)
        parts += capsule((s * 0.075, 0, knee_z), (s * 0.075, 0, 0.05), 0.042, skin if "shirt-flowers" in extras or child else bottom)
        parts.append(box((0.09, 0.17, 0.06), (s * 0.075, -0.03, 0.03), shoe, bevel=0.015))
    # torso
    parts.append(sphere(1, (0, 0, 0.64), top, scale=(0.17, 0.11, 0.2), segments=16))
    parts.append(sphere(1, (0, 0, 0.49), bottom, scale=(0.15, 0.1, 0.08), segments=14))
    if "dress" in extras:
        parts.append(cyl(0.2, 0.32, (0, 0, 0.4), top, verts=16, radius2=0.12))
    # neck and head
    parts += capsule((0, 0, 0.78), (0, 0, 0.82), 0.035, skin)
    parts.append(sphere(head_r, (0, 0, 0.82 + head_r), skin, segments=18))
    hz = 0.82 + head_r
    for s in (-1, 1):
        parts.append(sphere(0.018, (s * 0.045, -head_r * 0.93, hz + 0.01), eye, segments=8))
    parts.append(box((0.05, 0.01, 0.012), (0, -head_r * 0.97, hz - 0.05), mat("#A0453A", 0.5)))
    if "hijab" in extras:
        parts.append(sphere(head_r * 1.12, (0, 0.01, hz + 0.01), mat("#7DA6C9", 0.8), scale=(1, 1, 1), segments=18))
        parts.append(sphere(head_r * 1.0, (0, -0.03, hz - 0.06), mat("#7DA6C9", 0.8), scale=(1.05, 0.9, 0.9), segments=14))
    else:
        parts.append(sphere(head_r * 1.05, (0, 0.025, hz + 0.035), hair, scale=(1, 1, 0.85), segments=16))
    # arms (T-pose)
    shoulder_z = 0.75
    for s in (-1, 1):
        parts += capsule((s * 0.15, 0, shoulder_z), (s * 0.3, 0, shoulder_z), 0.045, top)
        parts += capsule((s * 0.3, 0, shoulder_z), (s * 0.44, 0, shoulder_z), 0.036, skin)
        parts.append(sphere(0.04, (s * 0.48, 0, shoulder_z), skin, segments=10))
    # extras
    if "sunhat" in extras:
        parts.append(cyl(0.22, 0.015, (0, 0.01, hz + head_r * 0.75), mat("#F2DDA4", 0.9), verts=24))
        parts.append(cyl(0.11, 0.08, (0, 0.01, hz + head_r * 0.95), mat("#F2DDA4", 0.9), verts=16))
    if "cap" in extras:
        parts.append(sphere(head_r * 1.08, (0, 0.01, hz + 0.05), mat("#E63946"), scale=(1, 1, 0.7), segments=14))
        parts.append(box((0.14, 0.12, 0.012), (0, -head_r - 0.04, hz + 0.06), mat("#E63946")))
    if "ponytail" in extras:
        parts.append(sphere(0.06, (0, head_r + 0.03, hz - 0.02), hair, scale=(0.9, 1, 1.4), segments=12))
    if "beret" in extras:
        parts.append(sphere(head_r * 1.05, (0.02, 0.02, hz + head_r * 0.7), mat("#1E1E1E", 0.8), scale=(1.1, 1.1, 0.35), segments=14))
    if "scarf" in extras:
        parts.append(cyl(0.065, 0.04, (0, 0, 0.8), mat("#D7263D"), verts=12))
        parts.append(cone(0.05, 0.12, (0, -0.07, 0.72), mat("#D7263D"), rot=(math.pi, 0, 0), verts=3))
    if "backpack" in extras:
        parts.append(box((0.24, 0.12, 0.3), (0, 0.15, 0.66), mat("#D35400", 0.8), bevel=0.03))
        parts.append(cyl(0.05, 0.26, (0, 0.17, 0.85), mat("#2E4053"), rot=(0, math.pi / 2, 0), verts=10))
    if "shirt-flowers" in extras:
        for (x, z) in ((-0.08, 0.68), (0.07, 0.6), (0.1, 0.72), (-0.05, 0.56)):
            parts.append(sphere(0.022, (x, -0.105, z), mat("#FFFFFF"), segments=8))
    if "swimring" in extras:
        bpy.ops.mesh.primitive_torus_add(major_radius=0.16, minor_radius=0.05, location=(0, 0, 0.52))
        parts.append(_finish(bpy.context.active_object, mat("#FF5DA2", 0.4), True))
    body = join(parts, name)
    body.scale = (height, height, height)
    bpy.ops.object.transform_apply(scale=True)
    return body


# --------------------------------------------------------------------- build

BUILDERS = {
    "boat": boat,
    "lifeguard-tower": lifeguard_tower,
    "deck-chair": deck_chair,
    "dome-tent": dome_tent,
    "dome-tent-blue": lambda: dome_tent("#4A90D9"),
    "dome-tent-green": lambda: dome_tent("#5DAA45"),
    "log-bench": log_bench,
    "rocks": rocks,
    "flag-pole": flag_pole,
}

HEIGHTS = {"tourist-man": 1.72, "tourist-woman": 1.62, "tourist-kid": 1.15, "scout": 1.4, "scout-girl": 1.38, "hiker": 1.74}


def build(name, path):
    clear()
    if name in PEOPLE:
        person(name, HEIGHTS.get(name, 1.6))
    else:
        BUILDERS[name]()
    export(path)


def build_all(folder):
    for name in list(BUILDERS) + list(PEOPLE):
        build(name, os.path.join(folder, name + ".glb"))
