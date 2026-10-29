import json, os, sys
from math import acos, atan2, cos, exp, pi, radians, sin
import bpy, bmesh
from mathutils import Matrix, Quaternion, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)
from spirit_materials import make_materials

SPEC = json.load(open(os.path.join(HERE, "tyler.json")))
FBX = os.path.join(HERE, "..", "..", SPEC["modelPath"])
B, REST = SPEC["body"], SPEC["rest"]
MATS = [m["name"] for m in SPEC["materials"]]
FUR, TAN, PATCH, DARK = (MATS.index(n) for n in ("TylerFur", "TylerTan", "TylerPatch", "TylerNose"))
FPS = SPEC["fps"]
CLIPS = {c["name"]: c for c in SPEC["clips"]}
STRIP = {name: 1 + sum(c["frames"] + 40 for c in list(CLIPS.values())[:i]) for i, name in enumerate(CLIPS)}
LEGS = {"FL": ("FrontUpperLeg.L", "FrontLowerLeg.L", "FrontPaw.L"), "FR": ("FrontUpperLeg.R", "FrontLowerLeg.R", "FrontPaw.R"),
        "HL": ("HindUpperLeg.L", "HindLowerLeg.L", "HindPaw.L"), "HR": ("HindUpperLeg.R", "HindLowerLeg.R", "HindPaw.R")}
X, UP, FWD = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
FLIP = Quaternion((0, 0, 1), pi)
# Pivote en el suelo, a medio camino entre las patas delanteras y las traseras.
SHIFT = Vector((0, 0, (REST["front"][-1][2] + REST["hind"][-1][2]) / 2))


def U(p, rig=False):
    # El rig mira a -Y y la malla se exporta a +Y para que Unity la deje en +Z: FLIP pasa de uno a otro.
    p = Vector(p) - SHIFT
    return Vector((-p.x, -p.z, p.y)) if rig else Vector((p.x, p.z, p.y))


def clamp(x, a=0.0, b=1.0):
    return a if x < a else b if x > b else x


def smooth(a, b, x):
    t = clamp((x - a) / (b - a))
    return t * t * (3 - 2 * t)


def lerp(a, b, t):
    return a + (b - a) * t


def interp(knots, s):
    if s <= knots[0][0]:
        return knots[0][1:]
    for k0, k1 in zip(knots, knots[1:]):
        if s <= k1[0]:
            return [lerp(a, b, (s - k0[0]) / (k1[0] - k0[0])) for a, b in zip(k0[1:], k1[1:])]
    return knots[-1][1:]


def blend(s, knots):
    if s <= knots[0][0]:
        return {knots[0][1]: 1.0}
    for (s0, b0), (s1, b1) in zip(knots, knots[1:]):
        if s <= s1:
            t = (s - s0) / (s1 - s0)
            return {b0: 1.0} if b0 == b1 else {b0: 1 - t, b1: t}
    return {knots[-1][1]: 1.0}


def spline(ctrl, n):
    ctrl = [Vector(p) for p in ctrl]
    pts = [2 * ctrl[0] - ctrl[1]] + ctrl + [2 * ctrl[-1] - ctrl[-2]]
    dense = []
    for s in range(len(ctrl) - 1):
        p0, p1, p2, p3 = pts[s:s + 4]
        for j in range(20):
            t = j / 20
            dense.append(0.5 * (2 * p1 + (p2 - p0) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t
                                + (3 * p1 - p0 - 3 * p2 + p3) * t ** 3))
    dense.append(ctrl[-1])
    acc = [0.0]
    for a, b in zip(dense, dense[1:]):
        acc.append(acc[-1] + (b - a).length)
    out, j = [], 0
    for i in range(n):
        target = acc[-1] * i / (n - 1)
        while j < len(acc) - 2 and acc[j + 1] < target:
            j += 1
        seg = acc[j + 1] - acc[j]
        out.append(dense[j].lerp(dense[j + 1], 0.0 if seg < 1e-9 else clamp((target - acc[j]) / seg)))
    return out


def frames(pts, ref):
    out = []
    for k, p in enumerate(pts):
        t = (pts[min(k + 1, len(pts) - 1)] - pts[max(k - 1, 0)]).normalized()
        side = ref - t * ref.dot(t)
        side = side.normalized() if side.length > 1e-4 else t.orthogonal().normalized()
        out.append((p, side, t.cross(side)))
    return out


def head_axes():
    rot = Matrix.Rotation(radians(REST["headPitchDeg"]), 3, "X")
    return rot @ X, rot @ UP, rot @ FWD


def at_head(x, y, z):
    side, up, fwd = head_axes()
    return Vector(REST["head"]) + side * x + up * y + fwd * z


class Part:
    def __init__(self):
        self.v, self.w, self.f, self.m = [], [], [], []
        self.flat = set()

    def vert(self, p, w):
        self.v.append(U(p))
        self.w.append(w)
        return len(self.v) - 1

    def sweep(self, rings, sides, matfn, facets=False):
        # rings: (centro, ex, ey, pesos). El angulo pi/2 queda del lado de ey.
        ids = [[self.vert(c + ex * cos(2 * pi * (i + 0.5) / sides) + ey * sin(2 * pi * (i + 0.5) / sides), w)
                for i in range(sides)] for c, ex, ey, w in rings]
        for k, (r0, r1) in enumerate(zip(ids, ids[1:])):
            for i in range(sides):
                s, a = (k + 0.5) / (len(rings) - 1), 2 * pi * (i + 1) / sides
                if facets and 0.08 < s < 0.68 and sin(a) > 0.4:
                    self.flat.add(len(self.f))
                self.f.append((r0[i], r0[(i + 1) % sides], r1[(i + 1) % sides], r1[i]))
                self.m.append(matfn(s, a))
        for ring, s in ((ids[0], 0.0), (ids[-1][::-1], 1.0)):
            self.f.append(tuple(ring))
            self.m.append(matfn(s, pi / 2))

    def tube(self, ctrl, rings, radius, sides, matfn, weight, ref=X):
        pts = spline(ctrl, rings)
        n = len(pts) - 1
        rings = [(p, side * radius(k / n)[0], nn * radius(k / n)[1], weight(k / n))
                 for k, (p, side, nn) in enumerate(frames(pts, ref))]
        self.sweep(rings, sides, matfn)
        return rings

    def tuft(self, root, normal, flow, length, width, mat, weights):
        normal, flow = normal.normalized(), flow.normalized()
        side = normal.cross(flow)
        side = side.normalized() if side.length > 1e-4 else normal.orthogonal().normalized()
        along = side.cross(normal)
        base = root - normal * width * 0.3
        points = (base - side * width / 2, base + side * width / 2, base - along * width * 0.35,
                  root + along * length + normal * length * 0.25)
        a, b, c, tip = [self.vert(p, weights) for p in points]
        self.f += [(a, c, b), (a, b, tip), (b, c, tip), (c, a, tip)]
        self.m += [mat] * 4

    def surface(self, origin, direction, faces):
        ray = U(direction) - U((0, 0, 0))
        start, distance = U(origin), None
        for face in faces:
            a = self.v[face[0]]
            for b, c in zip(face[1:], face[2:]):
                e1, e2 = self.v[b] - a, self.v[c] - a
                p = ray.cross(e2)
                det = e1.dot(p)
                if abs(det) < 1e-12:
                    continue
                t = start - a
                u, q = t.dot(p) / det, t.cross(e1)
                v, hit = ray.dot(q) / det, e2.dot(q) / det
                if u >= -1e-6 and v >= -1e-6 and u + v <= 1 + 1e-6 and hit >= 0:
                    distance = max(distance if distance is not None else 0, hit)
        if distance is None:
            raise ValueError("[Tiffany] El detalle del hocico no encuentra su superficie")
        return origin + direction * (distance + 0.0002)

    def blob(self, center, axes, radii, rows, cols, matfn, w, top_limit=1.0):
        # Polos sobre el segundo eje. matfn recibe la direccion local del centro de cada cara.
        def point(th, ph):
            return Vector((sin(th) * cos(ph), cos(th), sin(th) * sin(ph)))

        def vert(d):
            shape = (d.x, min(d.y, top_limit), d.z)
            return self.vert(center + sum((a * (shape[i] * radii[i]) for i, a in enumerate(axes)), Vector()), w)

        dirs = [[point(pi * r / rows, 2 * pi * c / cols) for c in range(cols)] for r in range(1, rows)]
        top, bottom = vert(point(0, 0)), vert(point(pi, 0))
        ids = [[vert(d) for d in row] for row in dirs]
        for c in range(cols):
            c2 = (c + 1) % cols
            self.f += [(top, ids[0][c2], ids[0][c]), (bottom, ids[-1][c], ids[-1][c2])]
            self.m += [matfn(dirs[0][c]), matfn(dirs[-1][c])]
            for r in range(rows - 2):
                self.f.append((ids[r][c], ids[r][c2], ids[r + 1][c2], ids[r + 1][c]))
                self.m.append(matfn((dirs[r][c] + dirs[r + 1][c2]).normalized()))


def saddle(s, a):
    up = sin(a) + B["patchWobble"] * sin(19 * s + 3 * a)
    edge = B["patchEdgeWobble"] * (sin(3 * a + 0.7) + 0.4 * sin(7 * a))
    for patch in B["patches"]:
        if patch["from"] + edge <= s <= patch["to"] - edge and up > patch["above"] and cos(a) * patch["side"] >= 0:
            return PATCH
    return FUR


def add_fur(part, rings, region, matfn, sides):
    coat = SPEC["coat"][region]
    for row, s in enumerate(coat["rows"]):
        k = min(int(s * (len(rings) - 1)), len(rings) - 2)
        t = s * (len(rings) - 1) - k
        c, ex, ey = [a.lerp(b, t) for a, b in zip(rings[k][:3], rings[k + 1][:3])]
        w0, w1 = rings[k][3], rings[k + 1][3]
        weights = {bone: lerp(w0.get(bone, 0), w1.get(bone, 0), t) for bone in w0.keys() | w1.keys()}
        for col, angle in enumerate(coat["angles"]):
            jitter = sin(7.13 * (row * len(coat["angles"]) + col + 1))
            a = radians(angle) + 0.09 * jitter
            normal = ex.normalized() * cos(a) + ey.normalized() * sin(a)
            surface = cos(pi / sides) / cos((a + pi / sides) % (2 * pi / sides) - pi / sides)
            root = c + surface * (ex * cos(a) + ey * sin(a)) - normal.normalized() * 0.0005
            part.tuft(root, normal, Vector(coat["flow"]), coat["length"] * (1 + 0.15 * jitter),
                      coat["width"], matfn(s, a), weights)


def build_mesh():
    part = Part()
    pts = spline(REST["spine"], B["torsoRings"])
    rings = []
    for k, (p, side, n) in enumerate(frames(pts, X)):
        s = k / (len(pts) - 1)
        up, down, hw = interp(B["torso"], s)
        rings.append((p + n * ((up - down) / 2), side * hw, n * ((up + down) / 2),
                      blend(s, [(0.3, "Hips"), (0.45, "Spine"), (0.62, "Spine"), (0.78, "Chest")])))
    part.sweep(rings, B["sides"], saddle, facets=True)
    withers = max((c + ey).y for c, _, ey, _ in rings)
    add_fur(part, rings, "chest", saddle, B["sides"])
    add_fur(part, rings, "belly", saddle, B["sides"])
    neck_mat = lambda s, a: FUR
    rings = part.tube(REST["neck"], B["neckRings"], lambda s: interp(B["neckProfile"], s), B["neckSides"], neck_mat,
                      lambda s: blend(s, [(0.0, "Chest"), (0.3, "Neck"), (0.8, "Neck"), (1.0, "Head")]))
    add_fur(part, rings, "neck", neck_mat, B["neckSides"])
    build_head(part)
    for sign, sd in ((1, "L"), (-1, "R")):
        build_leg(part, REST["front"], B["frontRadius"], sign, "F" + sd)
        build_leg(part, REST["hind"], B["hindRadius"], sign, "H" + sd)
    tr = B["tailRadius"]
    tail_mat = lambda s, a: PATCH if s < B["tailPatch"] else FUR
    rings = part.tube(REST["tail"], B["tailRings"], lambda s: (lerp(tr[0], tr[1], s),) * 2, B["tailSides"], tail_mat,
                      lambda s: blend(s, [(0.0, "Hips"), (0.1, "Tail1"), (0.3, "Tail1"), (0.4, "Tail2"), (0.63, "Tail2"), (0.72, "Tail3")]))
    return part, withers


def build_head(part):
    h, axes, head = B["head"], head_axes(), {"Head": 1.0}

    def skull(d):
        if d.y < h["eyeLine"] or (d.z > -0.2 and abs(d.x) < h["blaze"] * (1 + 0.5 * max(0, -d.y))):
            return FUR
        return TAN

    part.blob(Vector(REST["head"]), axes, h["radius"], h["rows"], h["cols"], skull, head, top_limit=h["flatTop"])
    for key, w in (("muzzle", head), ("jaw", {"Jaw": 1.0})):
        start = len(part.f)
        rings = [(at_head(0, y, z), axes[0] * hw, axes[1] * hh, w) for z, y, hw, hh in B[key]]
        part.sweep(rings, B["sides"] if key == "muzzle" else 8, lambda s, a: FUR)
        if key == "muzzle":
            muzzle_faces = part.f[start:]
    nose, eye, ear = B["nose"], B["eye"], B["ear"]
    center = at_head(*nose["at"])
    nx, ny, nz = [axis * radius for axis, radius in zip(axes, nose["radius"])]
    back = [part.vert(center + nx * x * 0.85 + ny * y * 0.8 - nz * 0.6, head) for x, y, z in nose["outline"]]
    front = [part.vert(center + nx * x + ny * y + nz * z, head) for x, y, z in nose["outline"]]
    tip = part.vert(center + ny * 0.1 + nz * 1.1, head)
    part.f.append(tuple(back[::-1]))
    part.m.append(DARK)
    for i in range(len(front)):
        j = (i + 1) % len(front)
        part.f += [(back[i], back[j], front[j], front[i]), (front[i], front[j], tip)]
        part.m += [DARK, DARK]
    widths = [[k / (len(ear["halfWidth"]) - 1), w] for k, w in enumerate(ear["halfWidth"])]
    for sign, sd in ((1, "L"), (-1, "R")):
        for key in ("cheek", "lip"):
            start = len(part.f)
            x, y, z = B[key]["at"]
            mat = (lambda d: TAN if d.y > 0.1 else FUR) if key == "cheek" else (lambda d: FUR)
            part.blob(at_head(-sign * x, y, z), axes, B[key]["radius"], 3, 8, mat, head)
            if key == "lip":
                lip_faces = part.f[start:]
        ex, ey, ez = eye["at"]
        part.blob(at_head(-sign * ex, ey, ez), axes, eye["radius"], 4, 8, lambda d: PATCH if d.z > 0.45 else DARK, head)
        part.blob(at_head(-sign * ex, ey, ez + 0.0055), axes, (0.0038, 0.005, 0.0015), 2, 6, lambda d: DARK, head)
        gx, gy, gz = eye["glintOffset"]
        part.blob(at_head(-sign * (ex + gx), ey + gy, ez + gz), axes, eye["glintRadius"], 2, 4, lambda d: FUR, head)
        for upper in (True, False):
            lid = [at_head(-sign * (ex + dx), ey + dy, ez + dz) for dx, dy, dz in
                   ((-0.01, 0.002, 0), (0, 0.006 if upper else -0.008, 0.005), (0.01, -0.003, 0))]
            part.tube(lid, 4, lambda s: (eye["lidRadius"],) * 2, 4,
                      lambda s, a: TAN if upper else FUR, lambda s: head, ref=axes[2])
        cheek = SPEC["coat"]["cheek"]
        for x, y, z in cheek["points"]:
            part.tuft(at_head(-sign * x, y, z), -sign * axes[0], -axes[1] * 0.4 - axes[2],
                      cheek["length"], cheek["width"], FUR, head)
        whiskers = SPEC["coat"]["whiskers"]
        for z in whiskers["rows"]:
            y, hw, hh = interp(B["muzzle"], z)
            root = part.surface(at_head(0, y - hh * 0.2, z), -sign * axes[0], muzzle_faces + lip_faces)
            part.tuft(root, -sign * axes[0] + axes[2] * 0.2,
                      -axes[1] - axes[2] * 0.5, whiskers["length"], whiskers["width"], FUR, head)
        for z, y, size in B["freckles"]:
            points = ((y + size, z), (y - size, z - size), (y - size, z + size))
            ids = [part.vert(part.surface(at_head(0, y, z), -sign * axes[0], lip_faces), head) for y, z in points]
            part.f.append(tuple(ids[::-1] if sign > 0 else ids))
            part.m.append(DARK)
        ear_points = [at_head(sign * x, y + (ear["foldAsymmetry"] * sin(pi * i / (len(ear["points"]) - 1))
                                            if sign < 0 else 0), z) for i, (x, y, z) in enumerate(ear["points"])]
        rings = []
        for k, (p, side, n) in enumerate(frames(spline(ear_points, ear["rings"]), axes[0])):
            s = k / (ear["rings"] - 1)
            inner = ear_points[0].lerp(ear_points[-1], s)
            rings.append(((p + inner) / 2, side * interp(widths, s)[0],
                          (p - inner) / 2 + n * ear["halfThickness"] * lerp(1, 0.6, s),
                          blend(s, [(0.0, "Head"), (0.3, "Ear." + sd)])))
        part.sweep(rings, ear["sides"], lambda s, a: PATCH if abs(cos(a)) > 0.8 else TAN)


def leg_joints(joints, sign):
    return [Vector((x * sign, y, z)) for x, y, z in joints]


def build_leg(part, joints, radii, sign, key):
    up, lo, paw_bone = LEGS[key]
    joints = leg_joints(joints, sign)
    paw = joints[-1]
    lengths = [0.0]
    for a, b in zip(joints, joints[1:]):
        lengths.append(lengths[-1] + (b - a).length)
    sK, sA = lengths[1] / lengths[-1], lengths[2] / lengths[-1]
    region = "front" if key[0] == "F" else "hind"
    knots = [[l / lengths[-1], *r] for l, r in zip(lengths, radii)]
    knots.insert(1, [sK * 0.45, *B[region + "Muscle"]])
    knots.insert(-2, [sA - 0.09, radii[2][0] * 0.7, radii[2][1] * 0.65])
    parent = "Chest" if key[0] == "F" else "Hips"
    samples = [i / (B["legRings"] - 1) for i in range(B["legRings"])]
    for s in (sK * 0.45, sK, sA - 0.09, sA):
        samples[min(range(1, len(samples) - 1), key=lambda i: abs(samples[i] - s))] = s
    path = [[l / lengths[-1], *p] for l, p in zip(lengths, joints[:-1] + [paw + UP * 0.008])]
    points = [Vector(interp(path, s)) for s in samples]
    rings = [(p, side * interp(knots, s)[0], n * interp(knots, s)[1],
              blend(s, [(0.0, parent), (0.12, up), (sK - 0.04, up), (sK + 0.04, lo),
                        (sA - 0.03, lo), (sA + 0.03, paw_bone)])) for s, (p, side, n) in zip(samples, frames(points, X))]
    part.sweep(rings, B["legSides"], lambda s, a: FUR)
    if region == "hind":
        add_fur(part, rings, region, lambda s, a: FUR, B["legSides"])
    part.blob(paw, (X, UP, FWD), B["paw"], B["pawRows"], B["pawCols"], lambda d: FUR, {paw_bone: 1.0})
    toes = B["toes"]
    for toe in (-1, 0, 1):
        center = paw + Vector(toes["offset"]) + X * toe * toes["spread"]
        part.blob(center, (X, UP, FWD), toes["radius"], 2, 6, lambda d: FUR, {paw_bone: 1.0})


def bone_table(rig=False):
    spine = spline(REST["spine"], 21)
    neck, tail = REST["neck"], spline(REST["tail"], 10)
    hip = Vector(REST["hind"][0])
    t = {"Root": (Vector((0, 0, 0)) + SHIFT, FWD * 0.1 + SHIFT, None),
         "Hips": (Vector((0, hip.y, hip.z)), spine[8], "Root"),
         "Spine": (spine[8], spine[14], "Hips"),
         "Chest": (spine[14], Vector(neck[0]), "Spine"),
         "Neck": (Vector(neck[0]), Vector(neck[-1]), "Chest"),
         "Head": (Vector(neck[-1]), at_head(0, 0.05, 0.01), "Neck"),
         "Jaw": (at_head(0, -0.032, 0.0), at_head(0, -0.038, 0.096), "Head")}
    ear = B["ear"]["points"]
    for sign, sd in ((1, "L"), (-1, "R")):
        t["Ear." + sd] = (at_head(sign * ear[0][0], *ear[0][1:]), at_head(sign * ear[-1][0], *ear[-1][1:]), "Head")
    parent = "Hips"
    for k, (a, b) in enumerate(((0, 3), (3, 6), (6, 9))):
        t["Tail%d" % (k + 1)] = (tail[a], tail[b], parent)
        parent = "Tail%d" % (k + 1)
    for key, names in LEGS.items():
        joints = leg_joints(REST["front" if key[0] == "F" else "hind"], 1 if key[1] == "L" else -1)
        toe = Vector((joints[-1].x, 0.004, joints[-1].z + B["paw"][2]))
        parent = "Chest" if key[0] == "F" else "Hips"
        for name, h, tl in zip(names, joints[:3], joints[1:3] + [toe]):
            t[name] = (h, tl, parent)
            parent = name
    return {k: (U(h, rig), U(tl, rig), p) for k, (h, tl, p) in t.items()}


def make_armature(table):
    arm = bpy.data.objects.new("TylerRig", bpy.data.armatures.new("TylerRig"))
    bpy.context.scene.collection.objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    for name, (h, tl, _) in table.items():
        eb = arm.data.edit_bones.new(name)
        eb.head, eb.tail, eb.roll = h, tl, 0.0
    for name, (_, _, parent) in table.items():
        if parent:
            arm.data.edit_bones[name].parent = arm.data.edit_bones[parent]
    bpy.ops.object.mode_set(mode="OBJECT")
    return arm


def make_mesh(part, arm):
    mesh = bpy.data.meshes.new("Tyler")
    mesh.from_pydata([tuple(v) for v in part.v], [], part.f)
    mesh.polygons.foreach_set("material_index", part.m)
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    mesh.shade_smooth()
    for i in part.flat:
        mesh.polygons[i].use_smooth = False
    mesh.set_sharp_from_angle(angle=radians(70))
    for name in MATS:
        mesh.materials.append(bpy.data.materials[name])
    obj = bpy.data.objects.new("Tyler", mesh)
    bpy.context.scene.collection.objects.link(obj)
    groups = {}
    for i, w in enumerate(part.w):
        for bone, value in w.items():
            if value > 1e-4:
                groups.setdefault(bone, {}).setdefault(round(value, 4), []).append(i)
    for bone in sorted(groups):
        vg = obj.vertex_groups.new(name=bone)
        for value, ids in groups[bone].items():
            vg.add(ids, value, "REPLACE")
    obj.parent = arm
    obj.modifiers.new("Armature", "ARMATURE").object = arm
    return obj


AXES = {"X": Vector((1, 0, 0)), "Y": Vector((0, 1, 0)), "Z": Vector((0, 0, 1))}


def arm_q(rot):
    q = Quaternion()
    for ax in ("X", "Y", "Z"):
        if rot.get(ax):
            q = Quaternion(AXES[ax], radians(rot[ax])) @ q
    return q


def sang(r, d):
    return atan2(r.x * d.y - r.y * d.x, r.x * d.x + r.y * d.y)


def rot2(v, a):
    return Vector((v.x * cos(a) - v.y * sin(a), v.x * sin(a) + v.y * cos(a)))


def yz(v):
    return Vector((v.y, v.z))


class Rig:
    """FK con giros en ejes de la armadura e IK de dos huesos por pata en el plano YZ."""

    def __init__(self, table):
        self.head = {k: v[0] for k, v in table.items()}
        self.tail = {k: v[1] for k, v in table.items()}
        self.parent = {k: v[2] for k, v in table.items()}
        self.order = list(table)
        self.bend = {}
        for key, (up, lo, pw) in LEGS.items():
            ha = yz(self.head[pw]) - yz(self.head[up])
            hk = yz(self.head[lo]) - yz(self.head[up])
            self.bend[key] = 1.0 if ha.x * hk.y - ha.y * hk.x > 0 else -1.0

    def fk(self, rots, loc):
        W = {}
        for n in self.order:
            p = self.parent[n]
            base = W[p] if p else Matrix.Identity(4)
            if n == "Hips":
                base = base @ Matrix.Translation(loc)
            h = self.head[n]
            W[n] = base @ Matrix.Translation(h) @ rots.get(n, Quaternion()).to_matrix().to_4x4() @ Matrix.Translation(-h)
        return W

    def solve_leg(self, key, W, dy, dz, fang):
        up, lo, pw = LEGS[key]
        inv = W[self.parent[up]].inverted()
        H, K, A, T = self.head[up], self.head[lo], self.head[pw], self.tail[pw]
        fw = Quaternion((1, 0, 0), radians(fang)) @ (T - A).normalized()
        P = inv @ (T + Vector((0, dy, dz)))
        f = (inv.to_3x3() @ fw).normalized()
        At = yz(P) - yz(f) * (T - A).length
        a, b = (yz(K) - yz(H)).length, (yz(A) - yz(K)).length
        dv = At - yz(H)
        d0 = dv.length
        d = clamp(d0, abs(a - b) + 1e-4, a + b - 1e-4)
        ang = atan2(dv.y, dv.x) + self.bend[key] * acos(clamp((a * a + d * d - b * b) / (2 * a * d), -1, 1))
        d1 = Vector((cos(ang), sin(ang)))
        d2 = (yz(H) + dv.normalized() * d - (yz(H) + d1 * a)).normalized()
        t1 = sang((yz(K) - yz(H)).normalized(), d1)
        t2 = sang(rot2(yz(A) - yz(K), t1).normalized(), d2)
        t3 = sang(rot2(yz(T) - yz(A), t1 + t2).normalized(), yz(f).normalized())
        return (t1, t2, t3), max(0.0, d0 - (a + b))

    def resolve(self, pose):
        rots = {b: arm_q(r) for b, r in pose.items() if b not in ("loc", "paws")}
        loc = Vector(pose.get("loc", (0, 0, 0)))
        W = self.fk(rots, loc)
        err = 0.0
        for key in LEGS:
            th, e = self.solve_leg(key, W, *pose.get("paws", {}).get(key, (0.0, 0.0, 0.0)))
            err = max(err, e)
            for n, t in zip(LEGS[key], th):
                rots[n] = Quaternion((1, 0, 0), t) @ rots.get(n, Quaternion())
        return rots, loc, err


def merge(*poses):
    out = {}
    for p in poses:
        for b, rot in p.items():
            if b == "loc":
                out["loc"] = tuple(a + c for a, c in zip(out.get("loc", (0, 0, 0)), rot))
            elif b == "paws":
                pw = out.setdefault("paws", {})
                for k, v in rot.items():
                    pw[k] = tuple(a + c for a, c in zip(pw.get(k, (0, 0, 0)), v))
            else:
                d = out.setdefault(b, {})
                for ax, v in rot.items():
                    d[ax] = d.get(ax, 0) + v
    return out


def bump(p, c, w):
    d = (p - c + 0.5) % 1.0 - 0.5
    return exp(-(d / w) ** 2)


def wag(f, amp, k=1.0, lag=0.65):
    return {"Tail%d" % (i + 1): {"Z": amp * (1 + 0.2 * i) * sin(k * f - lag * i)} for i in range(3)}


def ears(x, y=0.0):
    return {"Ear.L": {"X": x, "Y": y}, "Ear.R": {"X": x, "Y": -y}}


def gait(u, duty, stride, lift, flex, roll):
    stride *= duty
    if u < duty:
        t = u / duty
        return -stride / 2 + stride * t, 0.0, roll * smooth(0.6, 1.0, t)
    v = (u - duty) / (1 - duty)
    return stride / 2 - stride * smooth(0.0, 1.0, v), lift * sin(pi * v), \
        roll * (1 - smooth(0.0, 0.35, v)) + flex * sin(pi * min(1.0, v * 1.15))


def gait_paws(p, g):
    return {key: gait((p - g["phase"][key]) % 1.0, g["duty"], g["stride"], g["lift"][key[0]],
                      g["flex"][key[0]], g["roll"]) for key in LEGS}


def pose_idle(p):
    f = 2 * pi * p
    b = sin(f)
    P = {"loc": (0, 0, 0.002 * b), "Spine": {"X": 1.0 * b}, "Chest": {"X": -1.2 * b},
         "Neck": {"X": 1.5 * sin(f + 0.5)}, "Head": {"X": -1.0 * b, "Z": 8 * sin(f + 0.3) * smooth(0.2, 0.6, abs(sin(f + 0.3)))}}
    E = {"Ear.L": {"X": -16 * bump(p, 0.55, 0.03), "Y": 4 * bump(p, 0.55, 0.03)},
         "Ear.R": {"X": -12 * bump(p, 0.2, 0.03), "Y": -4 * bump(p, 0.2, 0.03)}}
    return merge(P, E, wag(2 * f, 6))


def pose_walk(p):
    f = 2 * pi * (p + 0.25)
    P = {"loc": (0, 0, -0.03 - 0.003 * cos(2 * f)),
         "Hips": {"X": 1.5 * sin(f)}, "Chest": {"X": -1.5 * sin(f)},
         "Neck": {"X": 3 + 3 * cos(2 * f)}, "Head": {"X": -2 - 2 * cos(2 * f), "Z": -3 * sin(f)},
         "Tail1": {"X": -6},
         "paws": gait_paws(p, SPEC["gaits"]["Walk"])}
    return merge(P, ears(6 * sin(2 * f - 1.2)), wag(f, 14))


def pose_trot(p):
    f = 2 * pi * p
    P = {"loc": (0, 0, -0.045 - 0.007 * cos(2 * f)),
         "Spine": {"X": 1.5 * cos(2 * f)}, "Chest": {"X": -1.5 * cos(2 * f)},
         "Neck": {"X": 5 + 2 * cos(2 * f)}, "Head": {"X": -4 - 2 * cos(2 * f)},
         "paws": gait_paws(p, SPEC["gaits"]["Trot"])}
    return merge(P, ears(8 * sin(2 * f - 0.8)), wag(f, 9))


def pose_run(p):
    f = 2 * pi * (p + 0.5)
    P = {"loc": (0, 0, -0.055 + 0.012 * sin(f + 1.2)),
         "Hips": {"X": 4 * sin(f + 0.6)}, "Spine": {"X": -2 * sin(f + 0.6)}, "Chest": {"X": -3 * sin(f + 0.9)},
         "Neck": {"X": 22 + 5 * sin(f + 1.5)}, "Head": {"X": -8 - 5 * sin(f + 1.5)}, "Jaw": {"X": 10},
         "Tail1": {"X": -30 + 8 * sin(f + 2)}, "Tail2": {"X": -10 + 8 * sin(f + 1.4)}, "Tail3": {"X": -6 + 10 * sin(f + 0.8)},
         "paws": gait_paws(p, SPEC["gaits"]["Run"])}
    return merge(P, ears(-30 + 10 * sin(f + 2.2), 6))


def pose_sit(p):
    f = 2 * pi * p
    b = sin(f)
    P = {"Spine": {"X": -0.8 * b}, "Chest": {"X": -1.2 * b}, "loc": (0, 0, 0.0015 * b),
         "Head": {"X": -1.0 * b, "Z": 6 * sin(f + 0.4)}}
    E = {"Ear.L": {"X": -14 * bump(p, 0.7, 0.03)}, "Ear.R": {"X": -10 * bump(p, 0.3, 0.03)}}
    return merge(SPEC["sit"], P, E, wag(f, 4))


def pose_bark(p):
    e = max(0.0, sin(pi * clamp((p - 0.05) / 0.5))) ** 1.5
    late = max(0.0, sin(pi * clamp((p - 0.2) / 0.6))) ** 2
    P = {"loc": (0, -0.006 * e, 0.003 * e), "Hips": {"X": -5 * e}, "Chest": {"X": -3 * e},
         "Neck": {"X": -12 * e}, "Head": {"X": -8 * e}, "Jaw": {"X": 32 * e},
         "Tail1": {"X": 10 * e},
         "paws": {"FL": (0.0, 0.025 * e, -12 * e), "FR": (0.0, 0.025 * e, -12 * e)}}
    return merge(P, ears(-14 * e + 10 * late), wag(2 * pi * p, 12 * e, k=2))


def pose_whine(p):
    f = 2 * pi * p
    voice = 0.5 - 0.5 * cos(2 * f)
    P = {"loc": (0, 0, -0.012 + 0.0008 * sin(16 * f)),
         "Chest": {"X": 0.4 * sin(16 * f)}, "Neck": {"X": -30},
         "Head": {"X": -32 - 2 * voice}, "Jaw": {"X": 2 + 6 * voice},
         "Tail1": {"X": -160}, "Tail2": {"X": -15}, "Tail3": {"X": -10}}
    return merge(P, ears(38, 10))


def pose_growl(p):
    f = 2 * pi * p
    tremor = sin(18 * f)
    P = {"loc": (0, -0.018, -0.025 + 0.0008 * tremor),
         "Hips": {"X": 3}, "Chest": {"X": 4 + 0.5 * tremor},
         "Neck": {"X": 12}, "Head": {"X": 8}, "Jaw": {"X": 9 + 1.5 * sin(3 * f)},
         "FrontUpperLeg.L": {"Y": -10}, "FrontUpperLeg.R": {"Y": 10},
         "Tail1": {"X": 16}, "Tail2": {"X": 8}, "Tail3": {"X": 4}}
    return merge(P, ears(-22, -5))


POSES = {"Idle": pose_idle, "Walk": pose_walk, "Trot": pose_trot, "Run": pose_run,
         "Sit": pose_sit, "Whine": pose_whine, "Growl": pose_growl, "Bark": pose_bark}


def make_actions(arm, rig):
    ad = arm.animation_data_create()
    rest = {pb.name: pb.bone.matrix_local.to_quaternion() for pb in arm.pose.bones}
    acts = {}
    for name, clip in CLIPS.items():
        nf = clip["frames"]
        act = bpy.data.actions.new(name)
        act.use_fake_user = True
        ad.action = act
        rot = {b: [[] for _ in range(4)] for b in rest}
        loc, prev, err = [[], [], []], {}, 0.0
        for fr in range(nf + 1):
            rots, lv, e = rig.resolve(POSES[name](fr / nf))
            err = max(err, e)
            for b in rest:
                q = rest[b].inverted() @ FLIP @ rots.get(b, Quaternion()) @ FLIP.inverted() @ rest[b]
                if b in prev and prev[b].dot(q) < 0:
                    q.negate()
                prev[b] = q
                for i in range(4):
                    rot[b][i] += [float(fr), q[i]]
            lh = rest["Hips"].inverted() @ (FLIP @ lv)
            for i in range(3):
                loc[i] += [float(fr), lh[i]]
        curves = [('pose.bones["%s"].rotation_quaternion' % b, i, b, rot[b][i]) for b in rest for i in range(4)]
        curves += [('pose.bones["Hips"].location', i, "Hips", loc[i]) for i in range(3)]
        for path, i, group, keys in curves:
            fc = act.fcurve_ensure_for_datablock(arm, path, index=i, group_name=group)
            fc.keyframe_points.add(nf + 1)
            fc.keyframe_points.foreach_set("co", keys)
            for key in fc.keyframe_points:
                key.interpolation = "LINEAR"
            fc.update()
        act.use_frame_range = True
        act.frame_start, act.frame_end = 0, nf
        act.use_cyclic = clip["loop"]
        acts[name] = act
        g = SPEC["gaits"].get(name)
        if g and abs(g["stride"] * FPS / nf - clip["speed"]) > 1e-6:
            raise ValueError(f"[Tiffany] {name}: la zancada no coincide con speed")
        if g and err > 0.002:
            raise ValueError(f"[Tiffany] {name}: las patas no alcanzan el apoyo por {err:.3f} m")
        speed = f", {clip['speed']:.2f} m/s" if g else ""
        print(f"[Tiffany] {name}: {nf / FPS:.2f} s, patas sin alcanzar {err:.3f} m{speed}")
    ad.action = None
    for name in CLIPS:
        track = ad.nla_tracks.new()
        track.name = name
        strip = track.strips.new(name, STRIP[name], acts[name])
        strip.extrapolation = "NOTHING"
        if hasattr(strip, "action_slot") and strip.action_slot is None and len(acts[name].slots):
            strip.action_slot = acts[name].slots[0]


def add(name, data, pos, target=None):
    obj = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = pos
    if target is not None:
        obj.rotation_euler = (target - pos).to_track_quat("-Z", "Y").to_euler()
    return obj


def shoot(scene, cam, target, offset, path):
    cam.location = target + offset
    cam.rotation_euler = (-offset).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    import numpy as np
    img = bpy.data.images.load(path)
    px = np.array(img.pixels[:], dtype=np.float32).reshape(img.size[1], img.size[0], 4)
    bpy.data.images.remove(img)
    return px


def save_png(px, path):
    out = bpy.data.images.new("Sheet", px.shape[1], px.shape[0])
    out.pixels.foreach_set(px.ravel())
    out.filepath_raw, out.file_format = path, "PNG"
    out.save()
    bpy.data.images.remove(out)


def render(folder, arm):
    import numpy as np
    os.makedirs(folder, exist_ok=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x, scene.render.resolution_y = 640, 480
    scene.view_settings.view_transform = "Standard"
    scene.world = bpy.data.worlds.new("Night")
    scene.world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.003, 0.003, 0.005, 1)
    ground = bpy.data.meshes.new("Ground")
    ground.from_pydata([(-6, -6, 0), (6, -6, 0), (6, 6, 0), (-6, 6, 0)], [], [(0, 1, 2, 3)])
    gmat = bpy.data.materials.new("Ground")
    gmat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.008, 0.008, 0.008, 1)
    ground.materials.append(gmat)
    add("Ground", ground, Vector((0, 0, -0.001)))
    warm = bpy.data.lights.new("Warm", "POINT")
    warm.energy, warm.color, warm.shadow_soft_size = 30, (1.0, 0.72, 0.42), 0.1
    add("Warm", warm, U((0.45, 0.5, 1.4)))
    moon = bpy.data.lights.new("Moon", "SUN")
    moon.energy, moon.color = 0.35, (0.55, 0.65, 1.0)
    add("Moon", moon, U((-3, 8, -4)), U((0, 0, 0)))
    cam = add("Camera", bpy.data.cameras.new("Camera"), Vector((0, 0, 0)))
    cam.data.angle = radians(24)
    scene.camera = cam
    el, az = radians(42), radians(35)
    body = Vector((0, 0, 0.21))
    views = (("front", False, U((0, 0.15, 1.8)) - U((0, 0, 0))),
             ("threequarter", False, (U((sin(az) * cos(el), sin(el), cos(az) * cos(el))) - U((0, 0, 0))) * 2.0),
             ("profile", False, U((1.8, 0.12, 0)) - U((0, 0, 0))),
             ("head", True, U((0.3, 0.08, 0.5)) - U((0, 0, 0))))
    rows = []
    for name, clip in CLIPS.items():
        scene.frame_set(STRIP[name] + clip["key"])
        head = arm.matrix_world @ arm.pose.bones["Head"].tail + U((0, -0.04, 0.03)) - U((0, 0, 0))
        row = [shoot(scene, cam, head if on_head else body, offset, os.path.join(folder, f"{name}_{view}.png"))
               for view, on_head, offset in views]
        rows.insert(0, np.concatenate(row, axis=1)[::2, ::2])
    save_png(np.concatenate(rows, axis=0), os.path.join(folder, "sheet.png"))
    for name in SPEC["gaits"]:
        nf = CLIPS[name]["frames"]
        cells = []
        for k in range(8):
            scene.frame_set(STRIP[name] + round(k * nf / 8))
            cells.append(shoot(scene, cam, body, views[2][2], os.path.join(folder, "_strip.png"))[::2, ::2])
        save_png(np.concatenate(cells, axis=1), os.path.join(folder, f"strip_{name}.png"))
    os.remove(os.path.join(folder, "_strip.png"))
    game = SPEC["preview"]["gameCamera"]
    scene.render.resolution_x, scene.render.resolution_y = game["width"], game["height"]
    scene.render.resolution_percentage = 100
    scene.render.pixel_aspect_x = scene.render.pixel_aspect_y = 1
    cam.data.sensor_fit = "VERTICAL"
    cam.data.angle_y = radians(game["verticalFovDeg"])
    el, az = radians(game["pitchDeg"]), radians(game["yawDeg"])
    offset = Vector((sin(az) * cos(el), -cos(az) * cos(el), sin(el))) * game["distanceM"]
    for name, clip in CLIPS.items():
        scene.frame_set(STRIP[name] + clip["key"])
        shoot(scene, cam, body, offset, os.path.join(folder, f"{name}_game.png"))


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj)
    make_materials(SPEC["materials"])
    part, withers = build_mesh()
    table = bone_table()
    triangles = sum(len(face) - 2 for face in part.f)
    if triangles > 3000 or len(table) > 30:
        raise ValueError(f"[Tiffany] Tyler excede el presupuesto: {triangles} triangulos, {len(table)} huesos")
    arm = make_armature(table)
    mesh = make_mesh(part, arm)
    bpy.context.scene.render.fps = FPS
    make_actions(arm, Rig(bone_table(rig=True)))
    co = [v.co for v in mesh.data.vertices]
    size = [max(c[i] for c in co) - min(c[i] for c in co) for i in range(3)]
    print(f"[Tiffany] Tyler: {sum(len(p.vertices) - 2 for p in mesh.data.polygons)} triangulos, "
          f"{len(table)} huesos, {size[2]:.3f} m de alto, {size[1]:.3f} m de largo, cruz a {withers:.3f} m")
    if "--no-export" not in args:
        # Ejes nativos de Blender: con huesos, Unity convierte los ejes al importar (ModelImportRules).
        os.makedirs(os.path.dirname(FBX), exist_ok=True)
        bpy.ops.export_scene.fbx(filepath=os.path.abspath(FBX), object_types={"ARMATURE", "MESH"},
                                 axis_forward="Y", axis_up="Z", apply_scale_options="FBX_SCALE_ALL",
                                 bake_space_transform=False, mesh_smooth_type="FACE", use_mesh_modifiers=False,
                                 add_leaf_bones=False, use_armature_deform_only=False, armature_nodetype="NULL",
                                 bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=True,
                                 bake_anim_use_all_actions=False, bake_anim_force_startend_keying=True,
                                 bake_anim_simplify_factor=0.0, use_custom_props=False)
        print(f"[Tiffany] Exportado {os.path.abspath(FBX)}")
    if "--render" in args:
        render(args[args.index("--render") + 1], arm)
    if not bpy.app.background:
        bpy.context.scene.frame_set(STRIP["Idle"] + SPEC["preview"]["frame"])
        for area in (a for s in bpy.data.screens for a in s.areas if a.type == "VIEW_3D"):
            area.spaces.active.shading.type = "MATERIAL"


if __name__ == "__main__":
    main()
