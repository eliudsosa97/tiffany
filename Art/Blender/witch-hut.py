import math
import os
import sys

from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from lowpoly import Mesh, run


def xyz(value):
    return tuple(value[axis] for axis in ("x", "y", "z"))


def box(mesh, center, size, material):
    vertices = [mesh.vertex(tuple(center[i] + size[i] * sign[i] / 2 for i in range(3)))
                for sign in ((-1, -1, -1), (1, -1, -1), (1, 1, -1), (-1, 1, -1),
                             (-1, -1, 1), (1, -1, 1), (1, 1, 1), (-1, 1, 1))]
    for face in ((0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4),
                 (3, 7, 6, 2), (0, 4, 7, 3), (1, 2, 6, 5)):
        mesh.face([vertices[i] for i in face], material)


def stick(mesh, points, radius=0.07, material="WitchHutWood", sides=5):
    mesh.tube(points, [(radius, radius)] * len(points), material, sides=sides)


def walls(mesh, spec, rng):
    for wall in spec["walls"]:
        center, size = xyz(wall["center"]), xyz(wall["size"])
        box(mesh, center, size, "WitchHutAdobe")
        axis = 2 if size[0] < size[2] else 0
        normal = 2 - axis
        for side in (-1, 1):
            for i in range(3):
                point = list(center)
                point[axis] += (i / 2 - 0.5) * (size[axis] - 0.3)
                point[normal] += side * (size[normal] / 2 + 0.012)
                height = rng.uniform(2.2, 2.6)
                stick(mesh, [(point[0], 0, point[2]), (point[0] + 0.035, height, point[2])], 0.055)
                point[1] = rng.uniform(0.45, 1.8)
                patch_size = [0.025, rng.uniform(0.3, 0.65), 0.025]
                patch_size[axis] = rng.uniform(0.25, 0.55)
                box(mesh, point, patch_size, "WitchHutPatch")
                point[axis] += 0.17
                point[normal] += side * 0.025
                crack = [tuple(point)]
                for _ in range(3):
                    point[axis] += rng.uniform(-0.15, 0.15)
                    point[1] += 0.15
                    crack.append(tuple(point))
                stick(mesh, crack, 0.018, "WitchHutCrack", sides=3)
    for x in (-2.1, 2.1):
        stick(mesh, [(x, 0, 4), (x + 0.05, 2.55, 4)], 0.11)
    stick(mesh, [(-2.2, 2.5, 4), (2.2, 2.4, 4)], 0.13)


def roof(mesh, rng):
    for z in (-4, 0, 4):
        stick(mesh, [(-4.45, 2.72, z), (0, 4.5, z), (4.45, 2.72, z)], 0.09)
    for z in (-2.9, 0.35):
        stick(mesh, [(-4, 2.65, z), (4, 2.65, z)], 0.07)
    stick(mesh, [(0, 4.5, -4.55), (0.1, 4.48, 4.55)], 0.12)
    for side in (-1, 1):
        for strip in range(42):
            z = -4.5 + strip * 9 / 41 + rng.uniform(-0.025, 0.025)
            width = rng.uniform(0.24, 0.32)
            for row in range(3):
                if ((side == -1 and row == 1 and z + width / 2 > -1.9 and z - width / 2 < -0.6)
                        or (side == 1 and row == 2 and z + width / 2 > 2.65 and z - width / 2 < 3.95)):
                    continue
                inner = max(0, row * 1.4 + rng.uniform(-0.06, 0.06))
                outer = min(4.75, inner + rng.uniform(1.65, 1.95))
                bend = outer - rng.uniform(0.23, 0.4)
                lift = (2 - row) * 0.045 + rng.uniform(-0.02, 0.03)
                y0, y1 = 4.5 - inner * 0.39 + lift, 4.5 - bend * 0.39 + lift
                lean = rng.uniform(-0.12, 0.12)
                points = [(side * inner, y0, z - width / 2),
                          (side * inner, y0 + 0.035, z + width / 2),
                          (side * bend, y1 + 0.035, z - width * 0.45 + lean),
                          (side * bend, y1 - 0.04, z + width * 0.4 + lean),
                          (side * outer, 4.5 - outer * 0.39 - rng.uniform(0.12, 0.22), z + lean)]
                vertices = [mesh.vertex(point) for point in points]
                material = "WitchHutPalmDry" if rng.random() < 0.8 else "WitchHutPalm"
                for indices in ((0, 1, 2), (1, 3, 2), (2, 3, 4)):
                    face = [vertices[i] for i in indices]
                    if side < 0:
                        face.reverse()
                    mesh.face(face, material)
                    mesh.face(reversed(face), material)


def clothing(mesh, x, material, dress):
    y, z = 2.08, -2.9
    outline = [(-0.18, 0), (-0.35, -0.12), (-0.46, -0.45), (-0.28, -0.49),
               (-0.19, -0.3), (-0.3 if dress else -0.22, -0.9),
               (0.3 if dress else 0.22, -0.9), (0.19, -0.3), (0.28, -0.49),
               (0.46, -0.45), (0.35, -0.12), (0.18, 0), (0, -0.09)]
    rings = [[mesh.vertex((x + dx, y + dy, z + depth)) for dx, dy in outline] for depth in (-0.025, 0.025)]
    mesh.bridge(rings, material)


def interior(mesh, spec, rng):
    for part in spec["table"]:
        box(mesh, xyz(part["center"]), xyz(part["size"]), "WitchHutWood")
    for x in (-2.8, -2.1, -1.4):
        box(mesh, (x, 1.438, -0.3), (0.015, 0.012, 1.55), "WitchHutCrack")
    box(mesh, (0, 1.6, -3.55), (3.8, 0.12, 0.5), "WitchHutWood")
    for x in (-1.5, 1.5):
        stick(mesh, [(x, 1.12, -3.8), (x, 1.54, -3.35)], 0.05)
    for i in range(7):
        x = -1.5 + i * 0.5
        height = rng.uniform(0.25, 0.48)
        mesh.tube([(x, 1.66, -3.5), (x, 1.66 + height * 0.75, -3.5), (x, 1.66 + height, -3.5)],
                  [(0.12, 0.12), (0.14, 0.14), (0.08, 0.08)], "WitchHutJar", sides=6)
        box(mesh, (x, 1.69 + height, -3.5), (0.19, 0.05, 0.19), "WitchHutWood")
    for x, z in ((-2.8, -2.9), (-1.8, 0.35), (1.3, -2.9), (2.6, 0.35)):
        stick(mesh, [(x, 2.65, z), (x, 2.2, z)], 0.02)
        for i in range(3):
            mesh.clump((x + (i - 1) * 0.13, 2.05, z + rng.uniform(-0.09, 0.09)),
                       (0.14, 0.32, 0.13), "WitchHutHerbs", rng, sides=5, rows=1)
    stick(mesh, [(-2.7, 2.25, -2.9), (1.6, 2.25, -2.9)], 0.025)
    for x in (-2.7, 1.6):
        stick(mesh, [(x, 2.65, -2.9), (x, 2.25, -2.9)], 0.02)
    for x in (-1.1, 0.3):
        stick(mesh, [(x, 2.25, -2.9), (x, 2.08, -2.9)], 0.02)
    clothing(mesh, -1.1, "WitchHutDress", True)
    clothing(mesh, 0.3, "WitchHutSweater", False)
    fire = xyz(spec["lightOffset"])
    for i in range(10):
        angle = i * math.tau / 10
        mesh.clump((fire[0] + math.cos(angle) * 0.6, 0.16, fire[2] + math.sin(angle) * 0.6),
                   (0.23, 0.16, 0.2), "WitchHutStone", rng, sides=6, rows=1)
    for i in range(8):
        angle = i * math.tau / 8
        mesh.clump((fire[0] + math.cos(angle) * 0.28, 0.12, fire[2] + math.sin(angle) * 0.28),
                   (0.19, 0.08, 0.12), "WitchHutEmber", rng, sides=5, rows=1)
    for i in range(5):
        x, z = 2.8 + rng.uniform(-0.4, 0.4), -3 + rng.uniform(-0.25, 0.25)
        stick(mesh, [(x, 0.08, z), (x - 0.4, 0.08, z + 0.28)], 0.05, "WitchHutBone")
        for dx, dz in ((0, 0), (-0.4, 0.28)):
            mesh.clump((x + dx, 0.1, z + dz), (0.09, 0.07, 0.09), "WitchHutBone", rng, sides=5, rows=1)
    mesh.clump((3.05, 0.2, -2.7), (0.22, 0.2, 0.25), "WitchHutBone", rng, sides=6, rows=2)
    for x in (2.96, 3.14):
        box(mesh, (x, 0.25, -2.47), (0.09, 0.1, 0.025), "WitchHutCrack")


def fence(mesh, spec, rng):
    anchors = []
    for part in spec["fence"]:
        center, size = xyz(part["center"]), xyz(part["size"])
        axis = 0 if size[0] > size[2] else 2
        count = math.ceil(size[axis] / 0.6)
        ends = []
        for i in range(count + 1):
            point = list(center)
            point[axis] += size[axis] * (i / count - 0.5)
            point[1] = 0
            tip = (point[0] + rng.uniform(-0.14, 0.14), rng.uniform(0.95, 1.65), point[2] + rng.uniform(-0.14, 0.14))
            stick(mesh, [tuple(point), tip], 0.055)
            if tip[1] >= 1.2:
                anchors.append(tip)
            if i in (0, count):
                ends.append((point[0], 0.75, point[2]))
        stick(mesh, ends, 0.045)
    for charm in spec["fenceCharms"]:
        start = len(mesh.vertices)
        stick(mesh, [(0, 0.62, -0.04), (0, 0.18, 0)], 0.012, sides=3)
        if charm["kind"] == "skull":
            outline = [(-0.23, 0.23), (-0.18, -0.03), (-0.07, -0.34),
                       (0.07, -0.34), (0.18, -0.03), (0.23, 0.23)]
            rings = [[mesh.vertex((x, y, depth)) for x, y in outline] for depth in (-0.06, 0.09)]
            mesh.bridge(rings, "WitchHutBone")
            for side in (-1, 1):
                mesh.tube([(side * 0.17, 0.2, 0), (side * 0.35, 0.35, -0.03), (side * 0.4, 0.61, -0.05)],
                          [(0.07, 0.07), (0.04, 0.04), (0.007, 0.007)], "WitchHutBone", sides=4)
                eye = [(side * 0.04, 0.12, 0.095), (side * 0.17, 0.12, 0.095), (side * 0.1, -0.02, 0.095)]
                if side > 0:
                    eye.reverse()
                mesh.face([mesh.vertex(point) for point in eye], "WitchHutCrack")
            mesh.face([mesh.vertex(point) for point in ((-0.04, -0.16, 0.095), (0, -0.25, 0.095),
                                                       (0.04, -0.16, 0.095))], "WitchHutCrack")
        else:
            mesh.clump((0, 0.13, 0), (0.09, 0.1, 0.075), "WitchHutWood", rng, sides=5, rows=1)
            stick(mesh, [(0, 0.05, 0), (0.015, -0.25, 0)], 0.035, sides=3)
            stick(mesh, [(-0.24, -0.12, 0), (0, -0.03, 0), (0.22, -0.09, 0.02)], 0.025, sides=3)
            for side in (-1, 1):
                stick(mesh, [(0.015, -0.23, 0), (side * 0.15, -0.48, 0.02)], 0.025, sides=3)
        angle = math.radians(charm["yaw"])
        target = xyz(charm["center"])
        anchor = min(anchors, key=lambda tip: sum((tip[i] - target[i] - (0.62 if i == 1 else 0)) ** 2 for i in range(3)))
        anchors.remove(anchor)
        center = (anchor[0] + math.sin(angle) * 0.04, anchor[1] - 0.62, anchor[2] + math.cos(angle) * 0.04)
        for i in range(start, len(mesh.vertices)):
            x, y, z = mesh.vertices[i]
            mesh.vertices[i] = Vector((center[0] + math.cos(angle) * x + math.sin(angle) * z,
                                       center[1] + y, center[2] - math.sin(angle) * x + math.cos(angle) * z))


def build(spec, rng):
    meshes = [Mesh(name, recalculate_normals=name != "HutRoof")
              for name in ("HutWalls", "HutRoof", "HutInterior", "HutFence")]
    walls(meshes[0], spec, rng)
    roof(meshes[1], rng)
    interior(meshes[2], spec, rng)
    fence(meshes[3], spec, rng)
    triangles = sum(len(face) - 2 for mesh in meshes for face in mesh.faces)
    if triangles > 6000:
        raise ValueError(f"El jacal excede el presupuesto: {triangles} triángulos")
    return meshes


if __name__ == "__main__":
    run(__file__, build)
