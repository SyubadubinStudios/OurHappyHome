"""
Our Happy Home - character rigging and animation pipeline for Blender 5.2.

Takes a Rodin generated T-pose character (.glb), and:
  1. cleans and decimates the mesh, shrinks the textures,
  2. stands it on the floor at the requested height (metres),
  3. finds the joints from the mesh shape (arm line, neck, crotch, feet),
  4. builds a humanoid armature and skins the mesh (bone heat, distance fallback),
  5. keyframes the game's animation set (Idle, Walk, Run, Wave, Sit, Sleep,
     Cook, Cheer, Scared, Talk, Work, Read, Happy, Sad). The Jaw bone is
     never keyed: the game opens and closes it at runtime for lip-sync
     (ThreeNet has no morph targets, so faces are driven by bones),
  6. exports a skinned, animated .glb.

Run headless:
  blender -b --python rig_character.py -- <src.glb> <dst.glb> <height>
Or through Blender MCP (execute_blender_code):
  exec(open(r".../rig_character.py").read()); process(src, dst, height)
"""

import math
import sys

import bpy
import numpy as np
from mathutils import Matrix, Quaternion, Vector

FPS = 24
TARGET_FACES = 14000
TEXTURE_SIZE = 1024


# --------------------------------------------------------------------- scene


def clear_scene():
    """Empties the file without resetting add-ons (keeps the MCP add-on alive)."""
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for collection in (bpy.data.meshes, bpy.data.armatures, bpy.data.actions, bpy.data.materials,
                       bpy.data.images, bpy.data.cameras, bpy.data.lights):
        for block in list(collection):
            if block.users == 0 or collection in (bpy.data.actions,):
                collection.remove(block)


def import_mesh(path):
    bpy.ops.import_scene.gltf(filepath=path)
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    mesh = bpy.context.view_layer.objects.active
    # Drop import empties so the mesh is a root object with baked transforms.
    mesh.parent = None
    for o in list(bpy.context.scene.objects):
        if o != mesh:
            bpy.data.objects.remove(o, do_unlink=True)
    bpy.ops.object.select_all(action="DESELECT")
    mesh.select_set(True)
    bpy.context.view_layer.objects.active = mesh
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    mesh.name = "Body"
    return mesh


def clean_and_decimate(mesh):
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.remove_doubles(threshold=0.0001)
    bpy.ops.object.mode_set(mode="OBJECT")
    faces = len(mesh.data.polygons)
    if faces > TARGET_FACES:
        mod = mesh.modifiers.new("decimate", "DECIMATE")
        mod.ratio = TARGET_FACES / faces
        mod.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier=mod.name)
    for poly in mesh.data.polygons:
        poly.use_smooth = True


def shrink_textures():
    for image in bpy.data.images:
        if image.size[0] > TEXTURE_SIZE:
            image.scale(TEXTURE_SIZE, TEXTURE_SIZE)


def normalise(mesh, height):
    co = coords(mesh)
    mn, mx = co.min(axis=0), co.max(axis=0)
    scale = height / (mx[2] - mn[2])
    centre = Vector(((mn[0] + mx[0]) / 2, (mn[1] + mx[1]) / 2, mn[2]))
    mesh.data.transform(Matrix.Translation(-centre))
    mesh.data.transform(Matrix.Scale(scale, 4))
    mesh.data.update()


def coords(mesh):
    count = len(mesh.data.vertices)
    flat = np.empty(count * 3, dtype=np.float64)
    mesh.data.vertices.foreach_get("co", flat)
    return flat.reshape(count, 3)


# ----------------------------------------------------------------- landmarks


def find_landmarks(co):
    """Joint positions estimated from a T-pose mesh (Z up, facing -Y, feet at 0)."""
    H = co[:, 2].max()
    ax = np.abs(co[:, 0])
    max_x = ax.max()

    def band(z0, z1):
        return co[(co[:, 2] > z0) & (co[:, 2] < z1)]

    # Arm line: the hands are the widest points of a T-pose.
    arm_pts = co[ax > 0.72 * max_x]
    arm_z = float(np.median(arm_pts[:, 2]))

    # Torso half width a little below the armpits.
    torso = band(arm_z - 0.16 * H, arm_z - 0.08 * H)
    torso_hw = float(np.percentile(np.abs(torso[:, 0]), 97)) if len(torso) else 0.12 * H
    torso_y = float(np.median(torso[:, 1])) if len(torso) else 0.0

    def arm_centre(x):
        """Centre of the arm cross section at signed x."""
        sel = co[(np.abs(co[:, 0] - x) < 0.02 * H) & (co[:, 2] > arm_z - 0.12 * H) & (co[:, 2] < arm_z + 0.12 * H)]
        if len(sel) < 5:
            return arm_z, torso_y
        return float((sel[:, 2].min() + sel[:, 2].max()) / 2), float((sel[:, 1].min() + sel[:, 1].max()) / 2)

    # Neck: narrowest frontal slice just above the arm line.
    neck_z = arm_z + 0.06 * H
    best = None
    for z in np.arange(arm_z + 0.01 * H, arm_z + 0.16 * H, 0.004 * H):
        s = band(z, z + 0.01 * H)
        s = s[s[:, 1] < torso_y]  # front half: long hair hangs at the back
        if len(s) < 4:
            continue
        w = np.abs(s[:, 0]).max()
        if best is None or w < best:
            best, neck_z = w, float(z)
    neck_z = min(max(neck_z, arm_z + 0.02 * H), arm_z + 0.12 * H)

    # Crotch: first height where the gap between the legs closes.
    crotch = None
    for z in np.arange(0.2 * H, 0.6 * H, 0.005 * H):
        s = band(z, z + 0.006 * H)
        if np.sum(np.abs(s[:, 0]) < 0.012 * H) > 0:
            crotch = float(z)
            break
    if crotch is None or crotch < 0.34 * H or crotch > 0.55 * H:
        crotch = 0.44 * H  # skirts hide the gap: use a cartoon proportion

    feet = co[co[:, 2] < 0.06 * H]
    leg_x = float(np.median(np.abs(feet[:, 0]))) if len(feet) else 0.08 * H
    leg_x = min(max(leg_x, 0.04 * H), torso_hw * 0.8)
    toe_y = float(np.percentile(feet[:, 1], 3)) if len(feet) else -0.1 * H
    heel_y = float(np.percentile(feet[:, 1], 97)) if len(feet) else 0.05 * H

    # Face: the head is everything above the neck; the mouth sits in the lower front of it.
    chin_z = neck_z + 0.015 * H
    # Lower half of the head only, so cap brims and fringes do not count as the face;
    # its front-most point is the nose tip.
    lower = co[(co[:, 2] > chin_z) & (co[:, 2] < chin_z + 0.5 * (H - chin_z)) & (np.abs(co[:, 0]) < 0.06 * H)]
    head_y = float(np.median(lower[:, 1])) if len(lower) else torso_y
    face_y = float(np.percentile(lower[:, 1], 2)) if len(lower) else torso_y - 0.08 * H
    nose = lower[lower[:, 1] <= face_y] if len(lower) else lower
    mouth_z = float(np.median(nose[:, 2])) if len(nose) else chin_z + 0.25 * (H - chin_z)

    return dict(H=H, max_x=max_x, arm_z=arm_z, torso_hw=torso_hw, torso_y=torso_y,
                neck_z=neck_z, crotch=crotch, leg_x=leg_x, toe_y=toe_y, heel_y=heel_y,
                head_y=head_y, face_y=face_y, chin_z=chin_z, mouth_z=mouth_z,
                arm_centre=arm_centre)


# ------------------------------------------------------------------ armature


def build_armature(lm):
    H = lm["H"]
    y = lm["torso_y"]
    hip_z = lm["crotch"] + 0.05 * H
    chest_z = lm["arm_z"] - 0.1 * H
    neck_base = lm["arm_z"] + 0.005 * H

    arm_data = bpy.data.armatures.new("Rig")
    rig = bpy.data.objects.new("Rig", arm_data)
    bpy.context.scene.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode="EDIT")
    eb = arm_data.edit_bones

    def bone(name, head, tail, parent=None, roll=0.0, connect=False):
        b = eb.new(name)
        b.head, b.tail = Vector(head), Vector(tail)
        b.roll = roll
        if parent:
            b.parent = eb[parent]
            b.use_connect = connect
        return b

    bone("Hips", (0, y, hip_z), (0, y, hip_z + 0.08 * H))
    bone("Spine", (0, y, hip_z + 0.08 * H), (0, y, chest_z), "Hips", connect=True)
    bone("Chest", (0, y, chest_z), (0, y, neck_base), "Spine", connect=True)
    bone("Neck", (0, y, neck_base), (0, y, lm["neck_z"] + 0.03 * H), "Chest", connect=True)
    bone("Head", (0, y, lm["neck_z"] + 0.03 * H), (0, y, H), "Neck", connect=True)
    # Jaw: hinged level with the mouth, pointing forward and down to the chin.
    bone("Jaw", (0, lm["head_y"], lm["mouth_z"]), (0, lm["face_y"] + 0.01 * H, lm["chin_z"]), "Head")

    for side, s in (("L", 1.0), ("R", -1.0)):
        shoulder_x = lm["torso_hw"] * 0.78
        wrist_x = lm["max_x"] - 0.085 * H
        elbow_x = (shoulder_x + wrist_x) / 2
        sz, sy = lm["arm_centre"](s * (lm["torso_hw"] + 0.03 * H))
        ez, ey = lm["arm_centre"](s * elbow_x)
        wz, wy = lm["arm_centre"](s * wrist_x)
        hz, hy = lm["arm_centre"](s * (lm["max_x"] - 0.03 * H))
        bone(f"Shoulder.{side}", (s * 0.03 * H, y, neck_base - 0.02 * H), (s * shoulder_x, sy, sz), "Chest")
        bone(f"UpperArm.{side}", (s * shoulder_x, sy, sz), (s * elbow_x, ey, ez), f"Shoulder.{side}", connect=True)
        bone(f"ForeArm.{side}", (s * elbow_x, ey, ez), (s * wrist_x, wy, wz), f"UpperArm.{side}", connect=True)
        bone(f"Hand.{side}", (s * wrist_x, wy, wz), (s * (lm["max_x"] - 0.005 * H), hy, hz), f"ForeArm.{side}", connect=True)

        lx = lm["leg_x"]
        thigh_z = lm["crotch"] + 0.02 * H
        ankle_z = 0.06 * H
        knee_z = (thigh_z + ankle_z) / 2
        bone(f"Thigh.{side}", (s * lx, y, thigh_z), (s * lx, y - 0.01 * H, knee_z), "Hips")
        bone(f"Shin.{side}", (s * lx, y - 0.01 * H, knee_z), (s * lx, y + 0.005 * H, ankle_z), f"Thigh.{side}", connect=True)
        bone(f"Foot.{side}", (s * lx, y + 0.005 * H, ankle_z), (s * lx, lm["toe_y"], 0.015 * H), f"Shin.{side}", connect=True)

    bpy.ops.object.mode_set(mode="OBJECT")
    return rig


def skin(mesh, rig):
    """Bone heat weights; vertices the heat solver misses get distance weights."""
    bpy.ops.object.select_all(action="DESELECT")
    mesh.select_set(True)
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    try:
        bpy.ops.object.parent_set(type="ARMATURE_AUTO")
    except RuntimeError as error:
        print("auto weights failed:", error)
        bpy.ops.object.parent_set(type="ARMATURE_NAME")

    bones = [(b.name, Vector(b.head_local), Vector(b.tail_local)) for b in rig.data.bones]
    groups = {g.name: g for g in mesh.vertex_groups}
    for name, _, _ in bones:
        if name not in groups:
            groups[name] = mesh.vertex_groups.new(name=name)

    fixed = 0
    for v in mesh.data.vertices:
        total = sum(g.weight for g in v.groups)
        if total > 0.05:
            continue
        p = v.co
        dists = []
        for name, head, tail in bones:
            seg = tail - head
            t = max(0.0, min(1.0, (p - head).dot(seg) / max(seg.length_squared, 1e-9)))
            dists.append(((head + seg * t - p).length, name))
        dists.sort()
        d0 = dists[0][0] + 1e-4
        picks = [(name, (d0 / (d + 1e-4)) ** 4) for d, name in dists[:2]]
        norm = sum(w for _, w in picks)
        for name, w in picks:
            groups[name].add([v.index], w / norm, "REPLACE")
        fixed += 1
    print(f"distance weights for {fixed} of {len(mesh.data.vertices)} vertices")

    # Lower front of the face follows the Jaw (smooth falloff up to the mouth line).
    if "Jaw" in rig.data.bones and "Head" in groups:
        jaw = rig.data.bones["Jaw"]
        nose_z = jaw.head_local.z
        chin_z = jaw.tail_local.z
        face_y = jaw.tail_local.y
        head_y = jaw.head_local.y
        height = rig.data.bones["Head"].tail_local.z
        # Only below the gap between nose and mouth; full weight from the lower lip down.
        mouth_z = nose_z - 0.3 * (nose_z - chin_z)
        band = max(mouth_z - chin_z, 1e-3)
        jaw_group = groups["Jaw"]
        moved = 0
        for v in mesh.data.vertices:
            p = v.co
            if p.z < chin_z - 0.25 * band or p.z > mouth_z or abs(p.x) > 0.055 * height:
                continue
            front = (head_y - p.y) / max(head_y - face_y, 1e-3)
            if front < 0.45:
                continue
            w = min(1.0, (mouth_z - p.z) / (0.6 * band)) * min(1.0, (front - 0.45) / 0.3)
            w *= min(1.0, (0.055 * height - abs(p.x)) / (0.02 * height))
            if w <= 0.02:
                continue
            for g in v.groups:
                mesh.vertex_groups[g.group].add([v.index], g.weight * (1 - w), "REPLACE")
            jaw_group.add([v.index], w, "REPLACE")
            moved += 1
        print(f"jaw weights for {moved} vertices")

    # Keep every vertex to at most four normalised influences (glTF / CPU skinning).
    bpy.context.view_layer.objects.active = mesh
    bpy.ops.object.mode_set(mode="WEIGHT_PAINT")
    bpy.ops.object.vertex_group_limit_total(limit=4)
    bpy.ops.object.vertex_group_normalize_all(lock_active=False)
    bpy.ops.object.mode_set(mode="OBJECT")


# ---------------------------------------------------------------- animation

X = Vector((1, 0, 0))   # character's left
Y = Vector((0, 1, 0))   # character's back (front is -Y)
Z = Vector((0, 0, 1))   # up


def q(axis, degrees):
    return Quaternion(axis, math.radians(degrees))


class Animator:
    def __init__(self, rig):
        self.rig = rig
        self.rest = {b.name: b.matrix_local.to_quaternion() for b in rig.data.bones}
        self.height = rig.data.bones["Head"].tail_local.z

    def new_action(self, name):
        action = bpy.data.actions.new(name)
        action.use_fake_user = True
        self.rig.animation_data_create()
        self.rig.animation_data.action = action
        for pb in self.rig.pose.bones:
            pb.rotation_mode = "QUATERNION"
            pb.rotation_quaternion = Quaternion()
            pb.location = Vector()
        return action

    def set(self, bone, *rotations):
        """Rotations are (axis, degrees) in armature rest space, applied left to right."""
        total = Quaternion()
        for axis, degrees in rotations:
            total = q(axis, degrees) @ total
        rest = self.rest[bone]
        self.rig.pose.bones[bone].rotation_quaternion = rest.inverted() @ total @ rest

    def lift(self, bone, dz):
        rest = self.rest[bone]
        self.rig.pose.bones[bone].location = rest.inverted() @ Vector((0, 0, dz * self.height))

    def key(self, frame):
        for pb in self.rig.pose.bones:
            if pb.name == "Jaw":
                continue  # driven by the game at runtime
            pb.keyframe_insert("rotation_quaternion", frame=frame)
            pb.keyframe_insert("location", frame=frame)

    def reset(self):
        for pb in self.rig.pose.bones:
            pb.rotation_quaternion = Quaternion()
            pb.location = Vector()

    # Arms hang at the sides from the T-pose: +Y lowers the left arm, -Y the right.
    def arms_down(self, swing_l=0.0, swing_r=0.0, bend_l=12.0, bend_r=12.0, out=0.0):
        self.set("UpperArm.L", (Y, 72 - out), (X, -swing_l))
        self.set("UpperArm.R", (Y, -(72 - out)), (X, -swing_r))
        self.set("ForeArm.L", (Z, -bend_l))
        self.set("ForeArm.R", (Z, bend_r))


def make_actions(rig):
    a = Animator(rig)
    made = []

    def action(name):
        made.append(name)
        return a.new_action(name)

    # Idle: breathing and a slow weight shift (2 s loop).
    action("Idle")
    for f, t in ((1, 0.0), (25, 1.0), (49, 0.0)):
        a.reset()
        a.arms_down(bend_l=10 + 4 * t, bend_r=10 + 4 * t, out=2 * t)
        a.set("Chest", (X, 2 * t))
        a.set("Spine", (Y, 1.5 * t - 0.75))
        a.set("Head", (X, -2 * t), (Z, 3 * t - 1.5))
        a.key(f)

    def gait(name, length, stride, arm, knee, bob, lean):
        action(name)
        for i in range(5):
            phase = i / 4.0 * 2 * math.pi
            s = math.sin(phase)
            c = math.cos(phase)
            a.reset()
            a.arms_down(swing_l=-arm * s, swing_r=arm * s, bend_l=18 + 8 * max(0, -s), bend_r=18 + 8 * max(0, s))
            a.set("Thigh.L", (X, -stride * s))
            a.set("Thigh.R", (X, stride * s))
            a.set("Shin.L", (X, knee * max(0.0, c) + 6))
            a.set("Shin.R", (X, knee * max(0.0, -c) + 6))
            a.set("Foot.L", (X, -6 * s))
            a.set("Foot.R", (X, 6 * s))
            a.set("Spine", (X, lean), (Z, 4 * s))
            a.set("Chest", (Z, -6 * s))
            a.set("Head", (Z, 2 * s))
            a.lift("Hips", -bob * abs(c))
            a.key(1 + round(i * length / 4))

    gait("Walk", 24, 26, 22, 38, 0.012, 3)
    gait("Run", 16, 40, 45, 70, 0.025, 12)

    # Wave: right hand up, forearm swaying.
    action("Wave")
    for i, sway in enumerate((0, 25, -15, 25, -15, 0)):
        a.reset()
        a.arms_down(bend_l=10)
        a.set("UpperArm.R", (Y, 40), (X, -10))
        a.set("ForeArm.R", (Y, 60 + sway * 0.2), (X, sway))
        a.set("Head", (Z, -6), (Y, 4))
        a.key(1 + i * 6)

    # Sit: hips stay, thighs forward, shins down, hands on lap.
    action("Sit")
    for f, t in ((1, 0.0), (37, 1.0), (73, 0.0)):
        a.reset()
        a.arms_down(swing_l=35, swing_r=35, bend_l=55, bend_r=55)
        a.set("Thigh.L", (X, -88))
        a.set("Thigh.R", (X, -88))
        a.set("Shin.L", (X, 88))
        a.set("Shin.R", (X, 88))
        a.set("Chest", (X, 3 * t))
        a.set("Head", (X, -4 * t), (Z, 4 - 8 * t))
        a.key(f)

    # Sleep: relaxed, slow breathing (the game lays the character down).
    action("Sleep")
    for f, t in ((1, 0.0), (49, 1.0), (97, 0.0)):
        a.reset()
        a.arms_down(bend_l=20, bend_r=20, out=-4)
        a.set("Chest", (X, 3 * t))
        a.set("Head", (Z, 14), (X, 6))
        a.set("Thigh.L", (X, -6))
        a.set("Shin.L", (X, 12))
        a.key(f)

    # Cook: stirring with the right hand, left hand holding the pan.
    action("Cook")
    for i in range(9):
        ang = i / 8.0 * 2 * math.pi
        a.reset()
        a.set("UpperArm.L", (Y, 55), (X, -40))
        a.set("ForeArm.L", (Z, -70))
        a.set("UpperArm.R", (Y, -55 + 8 * math.sin(ang)), (X, -45 + 8 * math.cos(ang)))
        a.set("ForeArm.R", (Z, 70 + 10 * math.sin(ang)))
        a.set("Spine", (X, 8))
        a.set("Head", (X, 12))
        a.key(1 + i * 4)

    # Cheer: jump with both arms up.
    action("Cheer")
    for f, up, lift in ((1, 0.0, 0.0), (7, 1.0, 0.05), (13, 0.8, 0.0), (19, 1.0, 0.05), (25, 0.0, 0.0)):
        a.reset()
        a.set("UpperArm.L", (Y, 72 - 150 * up))
        a.set("UpperArm.R", (Y, -(72 - 150 * up)))
        a.set("ForeArm.L", (Z, -10))
        a.set("ForeArm.R", (Z, 10))
        a.set("Thigh.L", (X, -20 * (1 - up)))
        a.set("Thigh.R", (X, -20 * (1 - up)))
        a.set("Shin.L", (X, 35 * (1 - up)))
        a.set("Shin.R", (X, 35 * (1 - up)))
        a.set("Head", (X, -10 * up))
        a.lift("Hips", lift - 0.03 * (1 - up))
        a.key(f)

    # Scared: crouched, hands near the face, trembling.
    action("Scared")
    for i in range(7):
        j = 3 if i % 2 else -3
        a.reset()
        a.set("UpperArm.L", (Y, 50), (X, -60))
        a.set("UpperArm.R", (Y, -50), (X, -60))
        a.set("ForeArm.L", (Z, -120 + j))
        a.set("ForeArm.R", (Z, 120 + j))
        a.set("Spine", (X, 18))
        a.set("Head", (X, 10), (Z, j))
        a.set("Thigh.L", (X, -30))
        a.set("Thigh.R", (X, -30))
        a.set("Shin.L", (X, 55))
        a.set("Shin.R", (X, 55))
        a.lift("Hips", -0.05)
        a.key(1 + i * 4)

    # Talk: relaxed gestures.
    action("Talk")
    for f, t in ((1, 0.0), (13, 1.0), (25, 0.3), (37, 1.0), (49, 0.0)):
        a.reset()
        a.set("UpperArm.L", (Y, 62), (X, -20 * t))
        a.set("UpperArm.R", (Y, -66), (X, -10 - 20 * (1 - t)))
        a.set("ForeArm.L", (Z, -40 - 30 * t))
        a.set("ForeArm.R", (Z, 30 + 30 * (1 - t)))
        a.set("Head", (Z, 6 * t - 3), (X, 3 * t))
        a.set("Chest", (Z, -3 * t))
        a.key(f)

    # Work: hammering / repairing with the right arm.
    action("Work")
    for f, t in ((1, 0.0), (7, 1.0), (10, 0.0), (16, 1.0), (19, 0.0), (25, 0.0)):
        a.reset()
        a.set("UpperArm.L", (Y, 50), (X, -50))
        a.set("ForeArm.L", (Z, -40))
        a.set("UpperArm.R", (Y, -40), (X, -70 + 50 * t))
        a.set("ForeArm.R", (Z, 50 + 50 * t))
        a.set("Spine", (X, 20))
        a.set("Head", (X, 15))
        a.set("Thigh.L", (X, -15))
        a.set("Shin.L", (X, 25))
        a.key(f)

    # Read: both hands hold a book in front, head tilted down.
    action("Read")
    for f, t in ((1, 0.0), (37, 1.0), (73, 0.0)):
        a.reset()
        a.set("UpperArm.L", (Y, 62), (X, -38))
        a.set("UpperArm.R", (Y, -62), (X, -38))
        a.set("ForeArm.L", (Z, -95))
        a.set("ForeArm.R", (Z, 95))
        a.set("Head", (X, 18 - 3 * t), (Z, 5 * t))
        a.key(f)

    # Happy: a light bounce with relaxed, swinging arms and a tilting head (1 s loop).
    action("Happy")
    for f, t in ((1, 0.0), (7, 1.0), (13, 0.0), (19, 1.0), (25, 0.0)):
        a.reset()
        a.arms_down(swing_l=8 * t, swing_r=-8 * t, bend_l=14, bend_r=14, out=6)
        a.set("Chest", (X, -3))
        a.set("Head", (X, -4), (Y, 8 * t - 4))
        a.lift("Hips", 0.012 * t)
        a.key(f)

    # Sad: slumped shoulders, head down, slow breathing (3 s loop).
    action("Sad")
    for f, t in ((1, 0.0), (37, 1.0), (73, 0.0)):
        a.reset()
        a.arms_down(bend_l=4, bend_r=4, out=-5)
        a.set("Spine", (X, 6))
        a.set("Chest", (X, 8 + 2 * t))
        a.set("Head", (X, 22 + 3 * t))
        a.lift("Hips", -0.004 * t)
        a.key(f)

    rig.animation_data.action = bpy.data.actions["Idle"]
    a.reset()
    return made


# -------------------------------------------------------------------- export


def export(path, mesh, rig):
    bpy.ops.object.select_all(action="DESELECT")
    mesh.select_set(True)
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    options = dict(
        filepath=path,
        export_format="GLB",
        use_selection=True,
        export_animations=True,
        export_animation_mode="ACTIONS",
        export_skins=True,
        export_apply=False,
        export_yup=True,
        export_image_format="JPEG",
        export_jpeg_quality=85,
        export_force_sampling=True,
    )
    try:
        bpy.ops.export_scene.gltf(**options)
    except TypeError as error:
        print("exporter option fallback:", error)
        for key in ("export_jpeg_quality", "export_force_sampling", "export_animation_mode"):
            options.pop(key, None)
        bpy.ops.export_scene.gltf(**options)


def strip_channels(path, bones):
    """Removes animation channels of the given bones from a GLB (they stay at rest for the game to drive)."""
    import json
    import struct
    with open(path, "rb") as f:
        data = f.read()
    json_len = struct.unpack_from("<I", data, 12)[0]
    gltf = json.loads(data[20:20 + json_len])
    rest = data[20 + json_len:]
    names = {i: n.get("name") for i, n in enumerate(gltf.get("nodes", []))}
    removed = 0
    for anim in gltf.get("animations", []):
        keep = [c for c in anim["channels"] if names.get(c["target"].get("node")) not in bones]
        removed += len(anim["channels"]) - len(keep)
        anim["channels"] = keep
    text = json.dumps(gltf, separators=(",", ":")).encode("utf-8")
    text += b" " * ((4 - len(text) % 4) % 4)
    out = struct.pack("<III", 0x46546C67, 2, 12 + 8 + len(text) + len(rest)) + struct.pack("<II", len(text), 0x4E4F534A) + text + rest
    with open(path, "wb") as f:
        f.write(out)
    print(f"stripped {removed} channels of {sorted(bones)}")


def process(src, dst, height):
    clear_scene()
    mesh = import_mesh(src)
    clean_and_decimate(mesh)
    shrink_textures()
    normalise(mesh, height)
    lm = find_landmarks(coords(mesh))
    print("landmarks", {k: round(v, 3) for k, v in lm.items() if isinstance(v, float)})
    rig = build_armature(lm)
    skin(mesh, rig)
    actions = make_actions(rig)
    export(dst, mesh, rig)
    strip_channels(dst, {"Jaw"})
    print(f"exported {dst} faces={len(mesh.data.polygons)} actions={actions}")
    return mesh, rig


if __name__ == "__main__" and "--" in sys.argv:
    argv = sys.argv[sys.argv.index("--") + 1:]
    process(argv[0], argv[1], float(argv[2]))
