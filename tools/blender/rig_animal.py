"""
Our Happy Home - quadruped (dog / cat) rigging and animation for Blender 5.2.

Takes a Rodin generated standing animal (.glb), and:
  1. optionally turns it (roll / turn in degrees) so it stands on Z and faces -Y
     (glTF +Z), cleans, decimates and shrinks the textures,
  2. stands it on the floor at the requested height (metres, to the top of the head),
  3. finds the feet, belly, back, head and tail from the mesh shape,
  4. builds a body / neck / head / tail / four-leg armature and skins the mesh,
  5. keyframes Idle, Walk, Run, Sit, Sleep and Bark,
  6. exports a skinned, animated .glb.

Reuses the helpers from rig_character.py.

Run headless:
  blender -b --python rig_animal.py -- <src.glb> <dst.glb> <height> [roll_x_degrees] [turn_z_degrees]
Or through Blender MCP (execute_blender_code):
  ns = {"__file__": r".../rig_animal.py"}; exec(open(ns["__file__"]).read(), ns); ns["process"](src, dst, height)
"""

import math
import os
import sys

import bpy
import numpy as np
from mathutils import Matrix, Vector

_here = os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals() else os.path.join(os.getcwd(), "tools", "blender")
exec(open(os.path.join(_here, "rig_character.py"), encoding="utf-8").read().split("\nif __name__ ==")[0], globals())

TARGET_FACES = 9000


# ----------------------------------------------------------------- landmarks


def find_animal_landmarks(co):
    H = co[:, 2].max()
    ymin, ymax = co[:, 1].min(), co[:, 1].max()
    feet = co[co[:, 2] < 0.07 * H]
    mid_y = float(np.median(feet[:, 1]))
    paws = {}
    for name, front, left in (("FL", True, True), ("FR", True, False), ("BL", False, True), ("BR", False, False)):
        sel = feet[((feet[:, 1] < mid_y) == front) & ((feet[:, 0] > 0) == left)]
        if len(sel) == 0:
            sel = feet[(feet[:, 1] < mid_y) == front]
        paws[name] = sel.mean(axis=0)
    front_y = (paws["FL"][1] + paws["FR"][1]) / 2
    back_y = (paws["BL"][1] + paws["BR"][1]) / 2

    # Back and belly between the legs, close to the middle line.
    band = co[(np.abs(co[:, 0]) < 0.08 * H) & (co[:, 1] > front_y) & (co[:, 1] < back_y)]
    upper = band[band[:, 2] > 0.2 * H]
    top_z = np.percentile(upper[:, 2], 97) if len(upper) else 0.6 * H
    belly_z = np.percentile(upper[:, 2], 3) if len(upper) else 0.35 * H
    spine_z = float((top_z + belly_z) / 2)

    # Head: the front end of the mesh above the shoulders.
    head = co[(co[:, 1] < front_y) & (co[:, 2] > spine_z)]
    if len(head) == 0:
        head = co[co[:, 1] < front_y]
    nose_y = float(head[:, 1].min())
    head_c = head.mean(axis=0)
    head_top = float(head[:, 2].max())

    # Tail: the back-most vertices above the rump.
    tail = co[co[:, 1] > back_y + 0.05 * H]
    tail_tip = tail[np.argmax(tail[:, 1] + 0.5 * tail[:, 2])] if len(tail) else np.array([0, ymax, spine_z])

    return {
        "H": float(H), "paws": paws, "front_y": float(front_y), "back_y": float(back_y),
        "spine_z": spine_z, "belly_z": float(belly_z), "nose_y": nose_y,
        "head_c": head_c, "head_top": head_top, "tail_tip": tail_tip, "ymax": float(ymax),
    }


def build_animal_armature(lm):
    H = lm["H"]
    sz = lm["spine_z"]
    arm_data = bpy.data.armatures.new("Rig")
    rig = bpy.data.objects.new("Rig", arm_data)
    bpy.context.scene.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode="EDIT")
    eb = arm_data.edit_bones

    def bone(name, head, tail, parent=None, connect=False):
        b = eb.new(name)
        b.head, b.tail = Vector(head), Vector(tail)
        if parent:
            b.parent = eb[parent]
            b.use_connect = connect
        return b

    hip = (0, lm["back_y"], sz)
    shoulder = (0, lm["front_y"], sz + 0.03 * H)
    hc = lm["head_c"]
    head_base = (0, (lm["front_y"] + hc[1]) / 2, (shoulder[2] + hc[2]) / 2 + 0.05 * H)
    bone("Body", hip, shoulder)
    bone("Neck", shoulder, head_base, "Body")
    bone("Head", head_base, (0, lm["nose_y"], hc[2]), "Neck", connect=True)
    tip = lm["tail_tip"]
    tail_base = (0, lm["back_y"] + 0.35 * (tip[1] - lm["back_y"]), sz + 0.05 * H)
    tail_mid = (0, (tail_base[1] + tip[1]) / 2, (tail_base[2] + tip[2]) / 2)
    bone("Tail1", tail_base, tail_mid, "Body")
    bone("Tail2", tail_mid, tuple(tip), "Tail1", connect=True)

    top = lm["belly_z"] + 0.04 * H
    for name, paw in lm["paws"].items():
        x = float(paw[0]) * 0.92
        py = float(paw[1])
        parent_y = lm["front_y"] if name[0] == "F" else lm["back_y"]
        knee_z = top * 0.48
        knee_y = py + (0.03 * H if name[0] == "F" else -0.03 * H)
        bone(f"Upper.{name}", (x, parent_y, top), (x, knee_y, knee_z), "Body")
        bone(f"Lower.{name}", (x, knee_y, knee_z), (x, py, 0.02 * H), f"Upper.{name}", connect=True)

    bpy.ops.object.mode_set(mode="OBJECT")
    return rig


# ---------------------------------------------------------------- animation


def make_animal_actions(rig, lm):
    a = Animator(rig)
    a.height = lm["H"]
    legs = ("FL", "FR", "BL", "BR")
    body_len = abs(lm["back_y"] - lm["front_y"])
    made = []

    def pose_legs(swing, bend=0.0):
        for leg in legs:
            a.set(f"Upper.{leg}", (X, swing.get(leg, 0.0)))
            a.set(f"Lower.{leg}", (X, bend if leg[0] == "F" else -bend))

    # Idle: breathe, slow tail wag, head looks around.
    a.new_action("Idle")
    for f, t in ((1, 0.0), (25, 0.5), (49, 1.0), (73, 1.5), (97, 2.0)):
        a.reset()
        s = math.sin(t * math.pi)
        a.lift("Body", 0.008 * s)
        a.set("Tail1", (Z, 18 * math.sin(t * math.pi * 2)), (X, -8))
        a.set("Tail2", (Z, 14 * math.sin(t * math.pi * 2 - 0.6)))
        a.set("Neck", (Z, 10 * math.sin(t * math.pi / 2)))
        a.set("Head", (X, -3 * s))
        a.key(f)
    made.append("Idle")

    # Walk / Run: diagonal pairs swing together.
    for name, frames, amp, bounce, wag in (("Walk", 24, 26, 0.015, 20), ("Run", 14, 42, 0.05, 30)):
        a.new_action(name)
        steps = 8
        for i in range(steps + 1):
            t = i / steps
            ph = t * 2 * math.pi
            a.reset()
            sw = {"FL": amp * math.sin(ph), "BR": amp * math.sin(ph),
                  "FR": -amp * math.sin(ph), "BL": -amp * math.sin(ph)}
            for leg in legs:
                a.set(f"Upper.{leg}", (X, sw[leg]))
                lift = max(0.0, math.sin(ph if leg in ("FL", "BR") else ph + math.pi))
                a.set(f"Lower.{leg}", (X, (-1 if leg[0] == "F" else 1) * 30 * lift))
            a.lift("Body", bounce * abs(math.sin(ph)))
            a.set("Body", (X, (4 if name == "Run" else 1.5) * math.sin(ph * 2)))
            a.set("Neck", (X, -3 * math.sin(ph * 2)))
            a.set("Tail1", (Z, wag * math.sin(ph)), (X, -12))
            a.set("Tail2", (Z, wag * 0.8 * math.sin(ph - 0.8)))
            a.key(1 + round(t * frames))
        made.append(name)

    # Sit: hips down, chest up, front legs straight.
    a.new_action("Sit")
    for f, wag in ((1, 0), (24, 1), (48, 0)):
        a.reset()
        tilt = 28
        a.set("Body", (X, -tilt))
        a.lift("Body", -body_len * math.sin(math.radians(tilt)) / lm["H"] * 0.9)
        for leg in ("FL", "FR"):
            a.set(f"Upper.{leg}", (X, tilt))
        for leg in ("BL", "BR"):
            a.set(f"Upper.{leg}", (X, -55 + tilt))
            a.set(f"Lower.{leg}", (X, 95))
        a.set("Neck", (X, tilt * 0.7))
        a.set("Tail1", (X, 40), (Z, 15 * wag))
        a.key(f)
    made.append("Sit")

    # Sleep: lying on the belly, legs tucked, head down, slow breathing.
    a.new_action("Sleep")
    leg_len = lm["belly_z"]
    for f, s in ((1, 0.0), (36, 1.0), (72, 0.0)):
        a.reset()
        a.lift("Body", -(leg_len * 0.8) / lm["H"] + 0.01 * s)
        for leg in ("FL", "FR"):
            a.set(f"Upper.{leg}", (X, -80))
            a.set(f"Lower.{leg}", (X, -10))
        for leg in ("BL", "BR"):
            a.set(f"Upper.{leg}", (X, 80))
            a.set(f"Lower.{leg}", (X, 10))
        a.set("Neck", (X, 22))
        a.set("Head", (X, 10))
        a.set("Tail1", (Z, 50), (X, 30))
        a.set("Tail2", (Z, 30))
        a.key(f)
    made.append("Sleep")

    # Bark: head snaps up, front bounces, tail wags fast.
    a.new_action("Bark")
    for i, f in enumerate((1, 5, 9, 13, 17, 21, 25)):
        a.reset()
        up = 1.0 if i % 2 == 1 else 0.0
        a.set("Body", (X, -6 * up))
        a.lift("Body", 0.02 * up)
        a.set("Neck", (X, -14 * up))
        a.set("Head", (X, -12 * up))
        a.set("Upper.FL", (X, -8 * up))
        a.set("Upper.FR", (X, -8 * up))
        a.set("Tail1", (Z, 30 if i % 2 else -30), (X, -20))
        a.key(f)
    made.append("Bark")

    rig.animation_data.action = bpy.data.actions["Idle"]
    for action in bpy.data.actions:
        if action.name not in made:
            continue
        for fc in getattr(action, "fcurves", []):
            for kp in fc.keyframe_points:
                kp.interpolation = "BEZIER"
    return made


# ------------------------------------------------------------------- process


def orient(mesh, roll_x, turn_z):
    mesh.data.transform(Matrix.Rotation(math.radians(roll_x), 4, "X"))
    mesh.data.transform(Matrix.Rotation(math.radians(turn_z), 4, "Z"))
    mesh.data.update()


def process(src, dst, height, roll_x=0.0, turn_z=0.0):
    clear_scene()
    mesh = import_mesh(src)
    orient(mesh, roll_x, turn_z)
    clean_and_decimate(mesh)
    shrink_textures()
    normalise(mesh, height)
    lm = find_animal_landmarks(coords(mesh))
    print("landmarks", {k: (round(v, 3) if isinstance(v, float) else v) for k, v in lm.items() if k != "paws"})
    rig = build_animal_armature(lm)
    skin(mesh, rig)
    actions = make_animal_actions(rig, lm)
    export(dst, mesh, rig)
    print(f"exported {dst} faces={len(mesh.data.polygons)} actions={actions}")
    return mesh, rig


if __name__ == "__main__" and "--" in sys.argv:
    argv = sys.argv[sys.argv.index("--") + 1:]
    process(argv[0], argv[1], float(argv[2]),
            float(argv[3]) if len(argv) > 3 else 0.0,
            float(argv[4]) if len(argv) > 4 else 0.0)
