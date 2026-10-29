import os
import sys
from math import atan2, cos, pi, radians, sin

import bpy
import bmesh
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)
from spirit_materials import make_materials
from tiffany_motion import BONES, CLIPS, FPS, LOG_PREFIX, SKIRT, SPEC, STRIP, U, clamp, make_actions, mix, smooth
import tiffany_video

FBX = os.path.abspath(os.path.join(HERE, "..", "..", SPEC["modelPath"]))
MATS = [entry["name"] for entry in SPEC["materials"]]
HEAD = {"Head": 1.0}


def weights(height, knots):
    if height <= knots[0][0]:
        return {knots[0][1]: 1.0}
    for (a, first), (b, second) in zip(knots, knots[1:]):
        if height <= b:
            t = (height - a) / (b - a)
            return {first: 1.0} if first == second else {first: 1 - t, second: t}
    return {knots[-1][1]: 1.0}


def spline(points, count):
    points = [Vector(p) for p in points]
    padded = [points[0]] + points + [points[-1]]
    result = []
    for k in range(count):
        t = k / (count - 1) * (len(points) - 1)
        i = min(int(t), len(points) - 2)
        u = t - i
        p0, p1, p2, p3 = padded[i:i + 4]
        result.append(0.5 * (2 * p1 + (p2 - p0) * u + (2 * p0 - 5 * p1 + 4 * p2 - p3) * u * u + (3 * p1 - p0 - 3 * p2 + p3) * u ** 3))
    return result


class Part:
    def __init__(self):
        self.vertices, self.faces, self.weights, self.materials, self.away = [], [], [], [], []

    def vertex(self, point, weight):
        self.vertices.append(U(point))
        self.weights.append(weight(Vector(point)) if callable(weight) else weight)
        return len(self.vertices) - 1

    def face(self, indices, material, away):
        self.faces.append(tuple(indices))
        self.materials.append(MATS.index(material))
        self.away.append(U(away))

    def loft(self, rows, centers, material, cap_start=True, cap_end=True):
        for k, (first, second) in enumerate(zip(rows, rows[1:])):
            middle = (centers[k] + centers[k + 1]) / 2
            for i in range(len(first)):
                j = (i + 1) % len(first)
                self.face((first[i], first[j], second[j], second[i]), material, middle)
        if cap_start:
            self.face(rows[0][::-1], material, centers[1])
        if cap_end:
            self.face(rows[-1], material, centers[-2])

    def sweep(self, rings, sides, material, cap_start=True, cap_end=True):
        rows = [[self.vertex(center + across * cos(2 * pi * i / sides) + depth * sin(2 * pi * i / sides), weight)
                 for i in range(sides)] for center, across, depth, weight in rings]
        self.loft(rows, [Vector(ring[0]) for ring in rings], material, cap_start, cap_end)

    def surface(self, point, rows, sides, material, weight, center):
        top = self.vertex(point(0, 0), weight)
        bottom = self.vertex(point(pi, 0), weight)
        rings = [[self.vertex(point(pi * row / rows, 2 * pi * i / sides), weight) for i in range(sides)]
                 for row in range(1, rows)]
        for i in range(sides):
            j = (i + 1) % sides
            self.face((top, rings[0][j], rings[0][i]), material, center)
            self.face((bottom, rings[-1][i], rings[-1][j]), material, center)
            for first, second in zip(rings, rings[1:]):
                self.face((first[i], first[j], second[j], second[i]), material, center)

    def blob(self, center, radius, rows, sides, material, weight, axes=None):
        center, axes = Vector(center), axes or Matrix.Identity(3)
        self.surface(lambda theta, phi: center + axes @ Vector((radius[0] * sin(theta) * cos(phi), radius[1] * cos(theta),
                                                                radius[2] * sin(theta) * sin(phi))),
                     rows, sides, material, weight, center)

    def strand(self, points, width, thick, center, weight, material="TiffanyHair", sides=4):
        rings = []
        for i, point in enumerate(points):
            s = i / (len(points) - 1)
            tangent = (points[min(i + 1, len(points) - 1)] - points[max(i - 1, 0)]).normalized()
            out = point - Vector(center)
            out = (out - tangent * out.dot(tangent)).normalized()
            rings.append((point, tangent.cross(out) * width(s), out * thick(s), weight(s) if callable(weight) else weight))
        self.sweep(rings, sides, material)


class Skull:
    def __init__(self, part, first_vertex, first_face):
        self.center = Vector(SPEC["body"]["head"]["center"])
        faces = [tuple(i - first_vertex for i in face) for face in part.faces[first_face:]]
        self.bvh = BVHTree.FromPolygons(part.vertices[first_vertex:], faces)

    def along(self, direction, offset=0.0):
        direction = Vector(direction).normalized()
        return U(self.bvh.ray_cast(U(self.center), U(direction))[0]) + direction * offset

    def front(self, x, y, offset=0.0):
        hit, normal = self.bvh.ray_cast(U((x, y, 1.0)), U((0, 0, -1)))[:2]
        normal = U(normal)
        return U(hit) + (normal if normal.z > 0 else -normal) * offset

    def dome(self, part, x, y, rx, ry, lifts, material, sides):
        middle = part.vertex(self.front(x, y, lifts[0]), HEAD)
        rings = [[part.vertex(self.front(x + f * rx * cos(2 * pi * i / sides), y + f * ry * sin(2 * pi * i / sides), lift), HEAD)
                  for i in range(sides)] for f, lift in ((0.6, lifts[1]), (1.0, lifts[2]))]
        for i in range(sides):
            j = (i + 1) % sides
            part.face((middle, rings[0][i], rings[0][j]), material, self.center)
            part.face((rings[0][i], rings[1][i], rings[1][j], rings[0][j]), material, self.center)


def head_point(theta, phi):
    head = SPEC["body"]["head"]
    c, s = cos(theta), sin(theta)
    low, front = clamp(-c), clamp(sin(phi))
    # Mejillas llenas: no hay color de mejilla, se leen por la forma.
    cheek = front * abs(cos(phi)) ** 0.7 * max(0.0, 1 - ((c + 0.4) / 0.35) ** 2)
    radial = 1 + head["cheek"] * cheek
    x = head["radius"][0] * s * cos(phi) * (1 - head["jaw"] * low * low) * radial
    z = head["radius"][2] * s * sin(phi) * (1 - 0.5 * head["jaw"] * low * low * (1 - front)) * radial
    y = (head["radius"][1] if c > 0 else head["chin"]) * c
    return Vector(head["center"]) + Vector((x, y, z))


def hair_direction(a, b, sign):
    a, b = radians(a), radians(b)
    return Vector((sign * sin(b), cos(b) * sin(a), cos(b) * cos(a)))


def build_hair(part, skull):
    columns = 13
    for sign in (-1, 1):
        grid = []
        for c in range(columns):
            a = 30 + 195 * c / (columns - 1)
            gap = 3.5 * clamp((120 - a) / 60)
            column = []
            for k in range(8):
                b = gap + (72 - gap) * k / 7
                edge = min(1.0, (72 - b) / 14, (a - 30) / 18, (225 - a) / 22)
                groove = mix(clamp((b - gap) / 8), 1.0, clamp((a - 90) / 40))
                crown = max(0.0, 1 - ((a - 125) / 70) ** 2)
                lift = 0.0015 + (0.011 + 0.005 * crown) * smooth(0, 1, min(edge, groove))
                column.append(part.vertex(skull.along(hair_direction(a, b, sign), lift), HEAD))
            grid.append(column)
        for first, second in zip(grid, grid[1:]):
            for k in range(7):
                quad = (first[k], second[k], second[k + 1], first[k + 1])
                part.face(quad, "TiffanyHair", skull.center)
        locks = (((56, 5, 0.01), (36, 22, 0.007), (19, 46, 0.004), (7, 62, 0.003), (-4, 70, 0.0025)),
                 ((62, 50, 0.011), (36, 66, 0.006), (10, 73, 0.004), (-16, 74, 0.003)))
        for lock, width in zip(locks, (0.02, 0.013)):
            points = spline([skull.along(hair_direction(a, b, sign), lift) for a, b, lift in lock], 7)
            part.strand(points, lambda s, w=width: w * (0.7 + 0.3 * smooth(0, 0.3, s)) * (1 - 0.8 * smooth(0.5, 1, s)),
                        lambda s: 0.0026 * (1 - 0.5 * s), skull.center, HEAD)


def braid_weights(side, s, lengths):
    total = sum(lengths)
    joints = [sum(lengths[:i]) / total for i in range(1, 3)]
    knots = [(0.0, "Head"), (0.07, "Braid.%s.1" % side), (joints[0] - 0.04, "Braid.%s.1" % side),
             (joints[0] + 0.04, "Braid.%s.2" % side), (joints[1] - 0.04, "Braid.%s.2" % side),
             (joints[1] + 0.04, "Braid.%s.3" % side)]
    return weights(s, knots)


def build_braid(part, sign, side):
    chain = [BONES["Braid.%s.%d" % (side, i)] for i in (1, 2, 3)]
    joints = [Vector(chain[0]["head"])] + [Vector(bone["tail"]) for bone in chain]
    lengths = [(b - a).length for a, b in zip(joints, joints[1:])]
    part.blob(joints[0] + Vector((sign * 0.004, 0.004, -0.004)), (0.022, 0.026, 0.022), 4, 8, "TiffanyHair", HEAD)
    path = spline(joints, 23)
    rings = []
    for k, point in enumerate(path):
        s = k / (len(path) - 1)
        tangent = (path[min(k + 1, len(path) - 1)] - path[max(k - 1, 0)]).normalized()
        across = tangent.cross(Vector((0, 0, 1)) if abs(tangent.z) < 0.9 else Vector((1, 0, 0))).normalized()
        depth = across.cross(tangent).normalized()
        radius = mix(0.0165, 0.012, s)
        lobe = 1.0 if k % 2 else 0.8
        shift = (0.0025 if k % 4 == 1 else -0.0025 if k % 4 == 3 else 0.0) * radius / 0.0165
        rings.append((point + across * shift, across * radius * lobe * 1.1, depth * radius * lobe, braid_weights(side, s, lengths)))
    part.sweep(rings, 6, "TiffanyHair")
    end, tangent = path[-1], (path[-1] - path[-2]).normalized()
    across = tangent.cross(Vector((0, 0, 1))).normalized()
    depth = across.cross(tangent).normalized()
    tip = {"Braid.%s.3" % side: 1.0}
    part.sweep([(end + tangent * t, across * r, depth * r, tip)
                for t, r in ((-0.004, 0.0125), (0.006, 0.0125))], 8, "TiffanyClothes", cap_start=False, cap_end=False)
    for bow in (-1, 1):
        part.blob(end + tangent * 0.001 + across * bow * 0.015, (0.011, 0.007, 0.005), 3, 6, "TiffanyClothes", tip,
                  Matrix.Rotation(radians(bow * 25), 3, "Z"))
    part.sweep([(end + tangent * t, across * r, depth * r, tip)
                for t, r in ((0.004, 0.009), (0.016, 0.013), (0.03, 0.009), (0.04, 0.002))], 6, "TiffanyHair")


def dress_weights(point):
    base = weights(point.y, [(0.63, "Hips"), (0.75, "Spine"), (0.86, "Chest")])
    hem = smooth(0.63, 0.54, point.y)
    turn = (atan2(point.x, point.z - 0.02) / (pi / 2)) % 4
    i, u = int(turn) % 4, turn - int(turn)
    result = {name: value * (1 - hem) for name, value in base.items()}
    for name, value in ((SKIRT[i], 1 - u), (SKIRT[(i + 1) % 4], u)):
        result[name] = result.get(name, 0.0) + value * hem
    return {name: value for name, value in result.items() if value > 0}


def build_dress(part):
    body = SPEC["body"]
    sides, rows, centers = 16, [], []
    for r, (y, rx, rz) in enumerate(body["dress"]):
        forward = 0.02 * clamp((0.63 - y) / 0.3)
        pleat = 0.035 * clamp((0.6 - y) / 0.2)
        row = []
        for i in range(sides):
            phi = 2 * pi * i / sides
            fold = 1 + pleat * (1 if i % 2 else -1)
            lift = body["dressFrontLift"] * (1 + sin(phi)) / 2 if r == 0 else 0.0
            row.append(part.vertex((rx * fold * cos(phi), y + lift, forward + rz * fold * sin(phi)), dress_weights))
        rows.append(row)
        centers.append(Vector((0, y, forward)))
    part.loft(rows, centers, "TiffanyClothes", cap_start=False)
    for sign in (-1, 1):
        part.blob((sign * 0.027, 0.745, -0.087), (0.026, 0.016, 0.009), 3, 6, "TiffanyClothes", dress_weights,
                  Matrix.Rotation(radians(-sign * 12), 3, "Z"))
        part.blob((sign * 0.013, 0.705, -0.09), (0.008, 0.03, 0.005), 3, 6, "TiffanyClothes", dress_weights,
                  Matrix.Rotation(radians(sign * 14), 3, "Z"))
    part.blob((0, 0.745, -0.091), (0.009, 0.011, 0.008), 3, 6, "TiffanyClothes", dress_weights)


FOOT = [(-0.04, 0.03, 0.02, 0.018), (-0.015, 0.036, 0.025, 0.024), (0.02, 0.03, 0.028, 0.018),
        (0.06, 0.022, 0.03, 0.011), (0.095, 0.019, 0.026, 0.008), (0.11, 0.018, 0.016, 0.006)]


def foot_ring(z):
    for a, b in zip(FOOT, FOOT[1:]):
        if z <= b[0]:
            t = clamp((z - a[0]) / (b[0] - a[0]))
            return [mix(p, q, t) for p, q in zip(a[1:], b[1:])]
    return list(FOOT[-1][1:])


def build_limbs(part, sign, side):
    arm_knots = [(0.1, "Shoulder." + side), (0.17, "UpperArm." + side),
                 (0.29, "UpperArm." + side), (0.35, "LowerArm." + side), (0.47, "LowerArm." + side), (0.5, "Hand." + side)]
    part.sweep([(Vector((sign * x, 0.91, 0)), Vector((0, ry, 0)), Vector((0, 0, rz)), weights(x, arm_knots))
                for x, ry, rz in ((0.17, 0.033, 0.034), (0.235, 0.032, 0.032), (0.3, 0.026, 0.027), (0.33, 0.025, 0.026),
                                  (0.37, 0.028, 0.029), (0.43, 0.022, 0.024), (0.487, 0.017, 0.02))], 8, "TiffanySkin")
    part.sweep([(Vector((sign * x, 0.913, 0)), Vector((0, r, 0)), Vector((0, 0, r)), weights(x, arm_knots))
                for x, r in ((0.095, 0.04), (0.125, 0.05), (0.165, 0.048), (0.195, 0.039), (0.206, 0.034))], 8, "TiffanyClothes")
    hand = {"Hand." + side: 1.0}
    part.blob((sign * 0.52, 0.908, 0.002), (0.034, 0.012, 0.022), 4, 8, "TiffanyGlow", hand)
    part.blob((sign * 0.502, 0.903, 0.02), (0.015, 0.009, 0.008), 3, 6, "TiffanyGlow", hand,
              Matrix.Rotation(radians(-sign * 35), 3, "Y"))
    leg_knots = [(0.06, "Foot." + side), (0.11, "LowerLeg." + side), (0.3, "LowerLeg." + side),
                 (0.36, "UpperLeg." + side), (0.52, "UpperLeg." + side), (0.66, "Hips")]
    part.sweep([(Vector((sign * 0.065, y, z)), Vector((rx, 0, 0)), Vector((0, 0, rz)), weights(y, leg_knots))
                for y, rx, rz, z in ((0.05, 0.02, 0.021, 0), (0.12, 0.026, 0.028, -0.003), (0.2, 0.033, 0.036, -0.007),
                                     (0.27, 0.032, 0.034, -0.004), (0.33, 0.032, 0.034, 0), (0.4, 0.038, 0.04, 0),
                                     (0.5, 0.042, 0.044, 0), (0.59, 0.044, 0.046, 0))], 8, "TiffanySkin")
    foot = {"Foot." + side: 1.0}
    part.sweep([(Vector((sign * 0.065, y, z)), Vector((w, 0, 0)), Vector((0, h, 0)), foot) for z, y, w, h in FOOT],
               8, "TiffanySkin")
    part.blob((sign * 0.065, 0.006, 0.033), (0.031, 0.006, 0.08), 3, 8, "TiffanyClothes", foot)
    for z in (0.0, 0.058):
        rings = []
        for t in (-0.006, 0.006):
            y, w, h = foot_ring(z + t)
            rings.append((Vector((sign * 0.065, y, z + t)), Vector((w + 0.003, 0, 0)), Vector((0, h + 0.003, 0)), foot))
        part.sweep(rings, 8, "TiffanyClothes", cap_start=False, cap_end=False)


def build_mesh():
    part = Part()
    head = SPEC["body"]["head"]
    first_vertex, first_face = len(part.vertices), len(part.faces)
    part.surface(head_point, head["rows"], head["sides"], "TiffanySkin", HEAD, Vector(head["center"]))
    skull = Skull(part, first_vertex, first_face)
    build_hair(part, skull)
    for sign in (-1, 1):
        x = sign * 0.041
        skull.dome(part, x, 1.053, 0.0145, 0.0185, (0.0034, 0.003, 0.0006), "TiffanyHair", 12)
        skull.dome(part, x + 0.0055, 1.0605, 0.0042, 0.0042, (0.0047, 0.0046, 0.0042), "TiffanyGlow", 6)
        brow = spline([skull.front(x - sign * 0.012, 1.083, 0.0015), skull.front(x + sign * 0.002, 1.0875, 0.0015),
                       skull.front(x + sign * 0.015, 1.084, 0.0015)], 4)
        part.strand(brow, lambda s: 0.0016 * (1 - 0.5 * s), lambda s: 0.0008, skull.center, HEAD)
        ear = skull.along((sign, -0.17, -0.12), -0.004)
        part.blob(ear, (0.009, 0.019, 0.014), 3, 6, "TiffanySkin", HEAD)
    part.blob(skull.front(0, 1.024, -0.003), (0.008, 0.006, 0.007), 3, 6, "TiffanySkin", HEAD)
    mouth = spline([skull.front(-0.008, 1.0, 0.001), skull.front(0, 0.996, 0.001), skull.front(0.008, 1.0, 0.001)], 4)
    part.strand(mouth, lambda s: 0.0014, lambda s: 0.0008, skull.center, HEAD)
    part.sweep([(Vector((0, y, 0.002)), Vector((0.031, 0, 0)), Vector((0, 0, 0.029)), {"Neck": 1.0})
                for y in (0.925, 1.0)], 8, "TiffanySkin")
    build_dress(part)
    for sign, side in ((-1, "L"), (1, "R")):
        build_braid(part, sign, side)
        build_limbs(part, sign, side)
    return part


def make_armature():
    name = SPEC["rig"]["name"]
    arm = bpy.data.objects.new(name, bpy.data.armatures.new(name))
    bpy.context.scene.collection.objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    arm.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    for entry in SPEC["skeleton"]:
        bone = arm.data.edit_bones.new(entry["name"])
        bone.head, bone.tail, bone.roll = U(entry["head"]), U(entry["tail"]), SPEC["rig"]["roll"]
    for entry in SPEC["skeleton"]:
        if entry["parent"]:
            arm.data.edit_bones[entry["name"]].parent = arm.data.edit_bones[entry["parent"]]
    bpy.ops.object.mode_set(mode="OBJECT")
    for bone in arm.pose.bones:
        bone.rotation_mode = "QUATERNION"
    return arm


def make_mesh(part, arm):
    mesh = bpy.data.meshes.new("TiffanyProvisional")
    mesh.from_pydata(part.vertices, [], part.faces)
    mesh.polygons.foreach_set("material_index", part.materials)
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.normal_update()
    flip = [face for face, away in zip(bm.faces, part.away) if face.normal.dot(face.calc_center_median() - away) < 0]
    bmesh.ops.reverse_faces(bm, faces=flip)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    mesh.polygons.foreach_set("use_smooth", [True] * len(mesh.polygons))
    for name in MATS:
        mesh.materials.append(bpy.data.materials[name])
    obj = bpy.data.objects.new("TiffanyProvisional", mesh)
    bpy.context.scene.collection.objects.link(obj)
    groups = {entry["name"]: obj.vertex_groups.new(name=entry["name"]) for entry in SPEC["skeleton"]}
    for i, weight in enumerate(part.weights):
        if abs(sum(weight.values()) - 1) > 1e-6 or len(weight) > 4:
            raise ValueError(f"{LOG_PREFIX} Pesos inválidos en el vértice {i}")
        for name, value in weight.items():
            if value > 0:
                groups[name].add([i], value, "REPLACE")
    obj.parent = arm
    obj.modifiers.new("Armature", "ARMATURE").object = arm
    return obj


def add(name, data, position, target=None):
    obj = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = position
    if target is not None:
        obj.rotation_euler = (target - position).to_track_quat("-Z", "Y").to_euler()
    return obj


def flat_material(name, color, emission):
    material = bpy.data.materials.new(name)
    if hasattr(material, "use_nodes") and not material.use_nodes:
        material.use_nodes = True
    nodes, links = material.node_tree.nodes, material.node_tree.links
    if emission:
        shader = nodes.new("ShaderNodeEmission")
        shader.inputs["Color"].default_value = color
        links.new(shader.outputs["Emission"], nodes["Material Output"].inputs["Surface"])
    else:
        nodes["Principled BSDF"].inputs["Base Color"].default_value = color
        nodes["Principled BSDF"].inputs["Roughness"].default_value = 1.0
    return material


def render(folder):
    import numpy as np
    from bpy_extras.object_utils import world_to_camera_view
    os.makedirs(folder, exist_ok=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = scene.render.resolution_y = 480
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.view_settings.view_transform = "Standard"
    scene.world = bpy.data.worlds.new("Preview")
    if hasattr(scene.world, "use_nodes") and not scene.world.use_nodes:
        scene.world.use_nodes = True
    background = (0.012, 0.016, 0.024, 1)
    scene.world.node_tree.nodes["Background"].inputs["Color"].default_value = background
    light = bpy.data.lights.new("Key", "AREA")
    light.energy, light.size = 350, 4
    key = add("Key", light, U((2, 3, 3)), U((0, 0.6, 0)))
    fill = bpy.data.lights.new("Fill", "AREA")
    fill.energy, fill.size = 150, 3
    fill = add("Fill", fill, U((-2, 2, -2)), U((0, 0.6, 0)))
    ground = bpy.data.meshes.new("Ground")
    ground.from_pydata([(-40, -40, -0.002), (40, -40, -0.002), (40, 40, -0.002), (-40, 40, -0.002)], [], [(0, 1, 2, 3)])
    ground.materials.append(flat_material("PreviewGround", background, True))
    add("Ground", ground, Vector())
    camera = add("Camera", bpy.data.cameras.new("Camera"), Vector())
    camera.data.type, camera.data.ortho_scale = "ORTHO", 1.45
    scene.camera = camera
    target = U((0, 0.62, 0))
    angle, elevation = radians(35), radians(55)
    views = (("front", U((0, 0, 3))),
             ("threequarter", U((sin(angle) * cos(elevation), sin(elevation), -cos(angle) * cos(elevation))) * 3),
             ("profile", U((3, 0, 0))))

    def shoot(offset, path, aim=target):
        camera.location = aim + offset
        camera.rotation_euler = (-offset).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        image = bpy.data.images.load(path)
        pixels = np.array(image.pixels[:], dtype=np.float32).reshape(image.size[1], image.size[0], 4)
        bpy.data.images.remove(image)
        return pixels

    def save(pixels, name):
        image = bpy.data.images.new(name, pixels.shape[1], pixels.shape[0])
        image.pixels.foreach_set(pixels.ravel())
        image.filepath_raw, image.file_format = os.path.join(folder, name + ".png"), "PNG"
        image.save()
        bpy.data.images.remove(image)

    rows = []
    for name, clip in CLIPS.items():
        scene.frame_set(STRIP[name] + clip["key"])
        rows.insert(0, np.concatenate([shoot(offset, os.path.join(folder, f"{name}_{view}.png")) for view, offset in views], axis=1))
    save(np.concatenate(rows, axis=0), "sheet")
    frames = []
    for i in range(8):
        scene.frame_set(STRIP["Walk"] + round(CLIPS["Walk"]["frames"] * i / 8))
        frames.append(shoot(views[2][1], os.path.join(folder, f"Walk_{i:02d}.png")))
    save(np.concatenate(frames, axis=1), "strip_Walk")

    scene.frame_set(STRIP["Idle"] + CLIPS["Idle"]["key"])
    scene.render.resolution_x, scene.render.resolution_y = 768, 1024
    camera.data.ortho_scale = 1.36
    sheet = [shoot(views[0][1], os.path.join(folder, "approval_front.png"), U((0, 0.6, 0))),
             shoot(views[2][1], os.path.join(folder, "approval_profile.png"), U((0, 0.6, 0)))]
    scene.render.resolution_x = 1024
    camera.data.ortho_scale = 0.34
    sheet.append(shoot(views[0][1], os.path.join(folder, "approval_face.png"), U((0, 1.07, 0))))

    rig = bpy.data.objects[SPEC["rig"]["name"]]
    scene.frame_set(STRIP["Walk"] + CLIPS["Walk"]["key"])
    hand = rig.matrix_world @ rig.pose.bones["Hand.L"].tail
    key.hide_render = fill.hide_render = True
    ground.materials[0] = flat_material("NightGround", (0.03, 0.028, 0.025, 1), False)
    moon = bpy.data.lights.new("Moon", "SUN")
    moon.energy, moon.color, moon.angle = 2.0, (0.6, 0.7, 1.0), radians(1)
    add("Moon", moon, U((-8, 12, 5)), U((0, 0, 0)))
    glow = bpy.data.lights.new("Glow", "POINT")
    glow.energy, glow.color, glow.shadow_soft_size, glow.use_shadow = 25, (1.0, 0.8, 0.55), 0.03, False
    add("Glow", glow, hand + U((-0.05, 0, 0)))
    pitch, yaw = radians(42), radians(45)
    forward = Vector((cos(pitch) * sin(yaw), -sin(pitch), cos(pitch) * cos(yaw)))
    focus = Vector((0, 0.4, 0))
    camera.data.type, camera.data.sensor_fit, camera.data.angle = "PERSP", "VERTICAL", radians(24)
    scene.render.resolution_x, scene.render.resolution_y = 1600, 900
    game = shoot(U(-forward * 36), os.path.join(folder, "approval_game.png"), U(focus))
    bpy.context.view_layer.update()
    u, v, _ = world_to_camera_view(scene, camera, U((0, 0.6, 0)))
    x, y = int(round(u * 1600)), int(round(v * 900))
    crop = game[y - 200:y + 200, x - 200:x + 200]
    save(np.repeat(np.repeat(crop, 2, axis=0), 2, axis=1), "approval_game_crop")
    pad = np.zeros((1024 - 900, 1600, 4), dtype=np.float32)
    pad[:] = game[-1, 0]
    sheet.append(np.concatenate([game, pad], axis=0))
    save(np.concatenate(sheet, axis=1), "approval_sheet")


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for name in MATS:
        if name in bpy.data.materials:
            bpy.data.materials.remove(bpy.data.materials[name])
    scene = bpy.context.scene
    scene.unit_settings.system, scene.unit_settings.scale_length = "METRIC", 1.0
    scene.render.fps = FPS
    make_materials(SPEC["materials"])
    part = build_mesh()
    arm = make_armature()
    mesh = make_mesh(part, arm)
    triangles = len(mesh.data.polygons)
    height = max(vertex.co.z for vertex in mesh.data.vertices) - min(vertex.co.z for vertex in mesh.data.vertices)
    if triangles > 4000 or len(arm.data.bones) > 40 or not 1.15 <= height <= 1.25:
        raise ValueError(f"{LOG_PREFIX} Presupuesto inválido: {triangles} triángulos, {len(arm.data.bones)} huesos, {height:.3f} m")
    make_actions(arm)
    scene.frame_set(0)
    print(f"{LOG_PREFIX} Tiffany provisional: {triangles} triángulos, {len(arm.data.bones)} huesos, {height:.3f} m")
    if "--no-export" not in args:
        os.makedirs(os.path.dirname(FBX), exist_ok=True)
        bpy.ops.export_scene.fbx(filepath=FBX, object_types={"ARMATURE", "MESH"},
                                 axis_forward="Y", axis_up="Z", global_scale=1.0, apply_unit_scale=True,
                                 apply_scale_options="FBX_SCALE_ALL", bake_space_transform=False,
                                 mesh_smooth_type="FACE", use_mesh_modifiers=False, add_leaf_bones=False,
                                 use_armature_deform_only=False, armature_nodetype="NULL", bake_anim=True,
                                 bake_anim_use_all_bones=True, bake_anim_use_nla_strips=True, bake_anim_use_all_actions=False,
                                 bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0, use_custom_props=False)
        print(f"{LOG_PREFIX} Exportado {FBX}")
    if "--video" in args:
        tiffany_video.videos(args[args.index("--video") + 1], arm)
    if "--sheets" in args:
        tiffany_video.sheets(args[args.index("--sheets") + 1], arm)
    if "--render" in args:
        render(args[args.index("--render") + 1])
    if not bpy.app.background:
        scene.frame_set(STRIP["Idle"])


if __name__ == "__main__":
    main()
