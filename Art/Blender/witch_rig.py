"""Esqueleto, skin y poses de la bruja. Todo en ejes de Blender: ella mira a -Y, su izquierda es +X."""

import math
import bpy
from mathutils import Matrix, Quaternion, Vector

LEFT, BACK, UP, FWD = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1)), Vector((0, -1, 0))


def V(x, y, z):
    # Sus ejes (derecha, arriba, frente) en Blender.
    return Vector((-x, -z, y))


def bow(deg):
    return Quaternion(LEFT, math.radians(deg))


def lean(deg):
    # Positivo inclina hacia su derecha.
    return Quaternion(FWD, math.radians(deg))


def turn(deg):
    # Positivo gira hacia su izquierda.
    return Quaternion(UP, math.radians(deg))


def rot(pitch=0.0, roll=0.0, yaw=0.0):
    return turn(yaw) @ lean(roll) @ bow(pitch)


def frame(head, y, x):
    y = y.normalized()
    x = x - y * x.dot(y)
    if x.length < 1e-6:
        x = y.orthogonal()
    x.normalize()
    m = Matrix((x, y, x.cross(y))).transposed().to_4x4()
    m.translation = head
    return m


def resample(rope, arcs):
    out, acc = [], [0.0]
    for a, b in zip(rope, rope[1:]):
        acc.append(acc[-1] + (b - a).length)
    for s in arcs:
        if s >= acc[-1]:
            out.append(None)
            continue
        i = max(k for k in range(len(acc) - 1) if acc[k] <= s)
        out.append(rope[i].lerp(rope[i + 1], (s - acc[i]) / max(acc[i + 1] - acc[i], 1e-9)))
    return out


def averaged_chain(ropes, arcs, fallback):
    """Promedio pesado de cuerdas en longitudes de arco. Lo que ninguna alcanza sigue recto hacia fallback."""
    points = []
    for k, s in enumerate(arcs):
        total, acc = 0.0, Vector()
        for w, rope in ropes:
            p = resample(rope, [s])[0]
            if p is not None:
                acc += p * w
                total += w
        if total > 0:
            points.append(acc / total)
        else:
            step = arcs[k] - arcs[k - 1]
            d = (points[-1] - points[-2]).normalized() if len(points) > 1 else fallback
            points.append(points[-1] + d * step)
    return points


def bone_table(pose, place, direction, spec):
    """(nombre, cabeza, cola, eje X de referencia, padre) en ejes de Blender, padres antes que hijos."""
    m, j = pose.marks, pose.j
    spine = [place(p) for p in m["spine"]]
    side = direction(m["headUp"].cross(m["headFront"]))
    table = [("Hips", spine[0], spine[1], LEFT, None), ("Spine", spine[1], spine[2], LEFT, "Hips"),
             ("Chest", spine[2], spine[3], LEFT, "Spine"), ("Neck", spine[3], place(m["headBase"]), LEFT, "Chest"),
             ("Head", place(m["headBase"]), place(m["headTop"]), -side, "Neck"),
             ("Jaw", place(m["jaw"][0]), place(m["jaw"][1]), -side, "Head")]
    for s in ("L", "R"):
        sh, elbow, wrist, knuckles, tip = [place(p) for p in m["arm" + s]]
        hinge = (elbow - sh).cross(wrist - elbow).normalized()
        spread = direction(m["spread" + s])
        table += [("Clavicle." + s, place(m["clavicle" + s]), sh, FWD, "Chest"),
                  ("UpperArm." + s, sh, elbow, hinge, "Clavicle." + s), ("Forearm." + s, elbow, wrist, hinge, "UpperArm." + s),
                  ("Hand." + s, wrist, knuckles, spread, "Forearm." + s), ("Fingers." + s, knuckles, tip, spread, "Hand." + s)]
    for s in ("L", "R"):
        hip, knee, ankle = place(j("hip" + s)), place(j("knee" + s)), place(j("ankle" + s))
        hinge = (knee - hip).cross(ankle - knee).normalized()
        table += [("Thigh." + s, hip, knee, hinge, "Hips"), ("Shin." + s, knee, ankle, hinge, "Thigh." + s),
                  ("Foot." + s, ankle, place(m["ball" + s]), LEFT, "Shin." + s)]
    hair = spec["hair"]
    for name in hair["chains"]:
        ropes = [(near[name], [place(p) for p in rope]) for near, rope in m["strands"] if near.get(name, 0) > 0.3]
        pts = averaged_chain(ropes, [hair["bone"] * k for k in range(4)], -UP)
        for k in range(3):
            table.append((f"Hair{k + 1}.{name}", pts[k], pts[k + 1], LEFT, "Head" if k == 0 else f"Hair{k}.{name}"))
    cloth = spec["cloth"]
    for name, azimuth in cloth["chains"].items():
        ropes = []
        for center, rope in m["cloth"]:
            a = math.degrees(math.atan2(rope[0].x - center.x, rope[0].z - center.z))
            if abs((a - azimuth + 180) % 360 - 180) < 30:
                ropes.append((1.0, [place(p) for p in rope]))
        pts = averaged_chain(ropes, [cloth["bone"] * k for k in range(3)], -UP)
        center = place(m["cloth"][0][0])
        out = Vector((pts[0].x - center.x, pts[0].y - center.y, 0)).normalized() * cloth["outset"]
        pts = [p + out for p in pts]
        for k in range(2):
            table.append((f"Cloth{k + 1}.{name}", pts[k], pts[k + 1], LEFT, "Hips" if k == 0 else f"Cloth1.{name}"))
    table.append(("Shawl", place(m["shawl"][0]), place(m["shawl"][1]), LEFT, "Chest"))
    if len(table) > 40:
        raise ValueError("[Tiffany] El esqueleto supera los 40 huesos.")
    return table


def make_armature(table):
    data = bpy.data.armatures.new("WitchRig")
    ob = bpy.data.objects.new("WitchRig", data)
    bpy.context.scene.collection.objects.link(ob)
    ob.show_in_front = True
    data.display_type = "STICK"
    vl = bpy.context.view_layer
    vl.update()
    for o in bpy.context.scene.objects:
        o.select_set(False)
    vl.objects.active = ob
    ob.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    for name, head, tail, x, _ in table:
        eb = data.edit_bones.new(name)
        eb.head, eb.tail = head, tail
        y = (tail - head).normalized()
        eb.align_roll((x - y * x.dot(y)).normalized().cross(y))
    for name, _, _, _, parent in table:
        if parent:
            data.edit_bones[name].parent = data.edit_bones[parent]
    bpy.ops.object.mode_set(mode="OBJECT")
    for pb in ob.pose.bones:
        pb.rotation_mode = "QUATERNION"
    return ob


def skin(obj, arm, weights):
    groups = {}
    for i, w in enumerate(weights):
        top = sorted(((v, b) for b, v in w.items() if v > 0.01), reverse=True)[:4]
        total = sum(v for v, _ in top)
        for v, b in top:
            groups.setdefault(b, []).append((i, v / total))
    missing = set(groups) - set(arm.data.bones.keys())
    if missing:
        raise RuntimeError(f"[Tiffany] Pesos a huesos que no existen: {sorted(missing)}")
    for b, items in groups.items():
        vg = obj.vertex_groups.new(name=b)
        for i, v in items:
            vg.add([i], v, "REPLACE")
    obj.parent = arm
    obj.modifiers.new("Armature", "ARMATURE").object = arm


class Rig:
    def __init__(self, arm):
        bones = arm.data.bones
        self.order = [b.name for b in bones]
        self.parent = {b.name: b.parent.name if b.parent else None for b in bones}
        self.rest = {b.name: b.matrix_local.copy() for b in bones}
        self.inv = {n: m.inverted() for n, m in self.rest.items()}
        self.head = {b.name: b.head_local.copy() for b in bones}
        self.tail = {b.name: b.tail_local.copy() for b in bones}
        self.length = {b.name: b.length for b in bones}

    def local(self, bone, point):
        return self.inv[bone] @ point


class Poser:
    """Pose en espacio de la armadura. Cada hueso se coloca después de su padre."""

    def __init__(self, rig):
        self.rig, self.P = rig, {}

    def delta(self, bone):
        return self.P[bone] @ self.rig.inv[bone]

    def axis(self, bone, i):
        return self.P[bone].col[i].xyz.normalized()

    def fill(self):
        for b in self.rig.order:
            if b not in self.P:
                self.rel(b, Quaternion())

    def head(self, bone):
        p = self.rig.parent[bone]
        return self.delta(p) @ self.rig.head[bone] if p else self.rig.head[bone]

    def point(self, bone, rest_point):
        return self.delta(bone) @ rest_point

    def root(self, bone, target, q):
        h = self.rig.head[bone]
        self.P[bone] = Matrix.Translation(target) @ q.to_matrix().to_4x4() @ Matrix.Translation(-h) @ self.rig.rest[bone]

    def rel(self, bone, q):
        h, p = self.rig.head[bone], self.rig.parent[bone]
        self.P[bone] = self.delta(p) @ Matrix.Translation(h) @ q.to_matrix().to_4x4() @ Matrix.Translation(-h) @ self.rig.rest[bone]

    def aim(self, bone, y, x):
        self.P[bone] = frame(self.head(bone), y, x)

    def orient(self, bone, q):
        """Gira el hueso desde su reposo con q en ejes del mundo, sin mover su cabeza."""
        m = (q.to_matrix() @ self.rig.rest[bone].to_3x3()).to_4x4()
        m.translation = self.head(bone)
        self.P[bone] = m

    def aim_local(self, bone, local, world, x):
        """Orienta el hueso para que su vector local apunte a world, con su eje X cerca de x."""
        a1 = local.normalized()
        a0 = Vector((1, 0, 0)) - a1 * a1.x
        a0.normalize()
        b1 = world.normalized()
        b0 = x - b1 * x.dot(b1)
        b0.normalize()
        A = Matrix((a0, a1, a0.cross(a1))).transposed()
        B = Matrix((b0, b1, b0.cross(b1))).transposed()
        m = (B @ A.transposed()).to_4x4()
        m.translation = self.head(bone)
        self.P[bone] = m

    def tail(self, bone):
        return self.P[bone] @ Vector((0, self.rig.length[bone], 0))

    def ik(self, upper, lower, target, pole):
        a = self.head(upper)
        l1, l2 = self.rig.length[upper], self.rig.length[lower]
        d = target - a
        reach = l1 + l2 - 1e-4
        dist = min(max(d.length, abs(l1 - l2) + 1e-4), reach)
        dh = d.normalized()
        n0 = pole - dh * pole.dot(dh)
        n0.normalize()
        c = (l1 * l1 + dist * dist - l2 * l2) / (2 * l1 * dist)
        mid = a + dh * (l1 * c) + n0 * (l1 * math.sqrt(max(0.0, 1 - c * c)))
        hinge = n0.cross(dh)
        self.aim(upper, mid - a, hinge)
        self.aim(lower, a + dh * dist - mid, hinge)
        return max(0.0, d.length - reach)


def bake(arm, rig, name, samples, loop):
    """samples: [(cuadro, Poser)]. Rotaciones en cuaternión de todos los huesos y posición solo de Hips."""
    previous = bpy.data.actions.get(name)
    if previous is not None:
        generated = previous.get("tiffany_witch_clip") == name
        legacy = previous.use_fake_user and len(previous.slots) == 1 and previous.slots[0].identifier == "OBWitchRig"
        if not generated and not legacy:
            raise ValueError(f"[Tiffany] La acción '{name}' ya existe y no pertenece al generador de la bruja.")
        bpy.data.actions.remove(previous)
    act = bpy.data.actions.new(name)
    act["tiffany_witch_clip"] = name
    act.use_fake_user = True
    ad = arm.animation_data_create()
    ad.action = act
    rot = {b: [[] for _ in range(4)] for b in rig.order}
    loc = [[], [], []]
    prev = {}
    for f, pose in samples:
        for b in rig.order:
            p = rig.parent[b]
            basis = (rig.inv[b] @ rig.rest[p] @ pose.P[p].inverted() @ pose.P[b]) if p else rig.inv[b] @ pose.P[b]
            q = basis.to_quaternion()
            if b in prev and prev[b].dot(q) < 0:
                q.negate()
            prev[b] = q
            for i in range(4):
                rot[b][i] += [f, q[i]]
            if b == "Hips":
                for i in range(3):
                    loc[i] += [f, basis.translation[i]]
    for b in rig.order:
        for i in range(4):
            fc = act.fcurve_ensure_for_datablock(arm, f'pose.bones["{b}"].rotation_quaternion', index=i, group_name=b)
            fc.keyframe_points.add(len(samples))
            fc.keyframe_points.foreach_set("co", rot[b][i])
            for key in fc.keyframe_points:
                key.interpolation = "LINEAR"
            fc.update()
    for i in range(3):
        fc = act.fcurve_ensure_for_datablock(arm, 'pose.bones["Hips"].location', index=i, group_name="Hips")
        fc.keyframe_points.add(len(samples))
        fc.keyframe_points.foreach_set("co", loc[i])
        for key in fc.keyframe_points:
            key.interpolation = "LINEAR"
        fc.update()
    act.use_frame_range = True
    act.frame_start, act.frame_end = samples[0][0], samples[-1][0]
    act.use_cyclic = loop
    ad.action = None
    return act


def add_strips(arm, actions):
    """El exportador FBX aísla cada tira NLA al hornearla."""
    ad = arm.animation_data_create()
    while ad.nla_tracks:
        ad.nla_tracks.remove(ad.nla_tracks[0])
    for act in actions:
        tr = ad.nla_tracks.new()
        tr.name = act.name
        st = tr.strips.new(act.name, 0, act)
        st.action_frame_start, st.action_frame_end = act.frame_start, act.frame_end
        st.frame_start, st.frame_end = 0, act.frame_end - act.frame_start
        st.scale, st.repeat = 1.0, 1.0
        st.extrapolation = "NOTHING"
        if hasattr(st, "action_slot") and st.action_slot is None and len(act.slots):
            st.action_slot = act.slots[0]


def export(path, arm, mesh):
    vl = bpy.context.view_layer
    for o in bpy.context.scene.objects:
        o.select_set(False)
    arm.select_set(True)
    mesh.select_set(True)
    vl.objects.active = arm
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"ARMATURE", "MESH"}, axis_forward="-Z", axis_up="Y",
        apply_scale_options="FBX_SCALE_ALL", bake_space_transform=True, mesh_smooth_type="FACE", use_mesh_modifiers=False,
        add_leaf_bones=False, use_armature_deform_only=False, armature_nodetype="NULL", use_custom_props=False,
        bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=True, bake_anim_use_all_actions=False,
        bake_anim_force_startend_keying=True, bake_anim_step=1.0, bake_anim_simplify_factor=0.0)
