import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from lowpoly import Mesh, render, run


def xyz(value):
    return tuple(value[axis] for axis in ("x", "y", "z"))


def box(mesh, center, size, material):
    vertices = [mesh.vertex(tuple(center[i] + size[i] * sign[i] / 2 for i in range(3)))
                for sign in ((-1, -1, -1), (1, -1, -1), (1, 1, -1), (-1, 1, -1),
                             (-1, -1, 1), (1, -1, 1), (1, 1, 1), (-1, 1, 1))]
    for face in ((0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4),
                 (3, 7, 6, 2), (0, 4, 7, 3), (1, 2, 6, 5)):
        mesh.face([vertices[i] for i in face], material)


def boards(mesh, center, size, rng, damaged=False):
    axis = 0 if size[0] > size[2] else 2
    count = math.ceil(size[axis] / 0.42)
    for i in range(count):
        wear = rng.random() if damaged and 0 < i < count - 1 else 1
        if wear < 0.08:
            continue
        point, plank = list(center), list(size)
        point[axis] += (i + 0.5) * size[axis] / count - size[axis] / 2
        plank[axis] = size[axis] / count - 0.025
        plank[1] -= rng.uniform(0, min(0.12, size[1] / 8))
        if wear < 0.3:
            plank[1] *= rng.uniform(0.5, 0.8)
        point[1] = plank[1] / 2 + center[1] - size[1] / 2
        if wear < 0.3 and rng.random() < 0.5:
            point[1] += size[1] - plank[1]
        shade = rng.random()
        material = "BarnWood" if shade < 0.6 else "BarnWeathered" if shade < 0.85 else "BarnWoodLight"
        box(mesh, point, plank, material)


def build(spec, rng):
    meshes = {name: Mesh(name) for name in
              ("BarnWalls", "BarnCutaway", "BarnRoof", "BarnInterior", "BarnFence", "BarnMud")}
    for wall in spec["walls"]:
        center, size = xyz(wall["center"]), list(xyz(wall["size"]))
        if wall["cameraFacing"]:
            center = (center[0], 0.5, center[2])
            size[1] = 1
        boards(meshes["BarnCutaway" if wall["cameraFacing"] else "BarnWalls"], center, size, rng, damaged=True)
    roof = meshes["BarnRoof"]
    for side in (-1, 1):
        for sheet in range(11):
            for row in range(2):
                if (side, sheet, row) in ((-1, 3, 1), (1, 7, 0), (1, 8, 0)):
                    continue
                for rib in range(8):
                    z0 = -5.4 + sheet + rib / 8
                    vertices = [roof.vertex((side * x, 7.2 - x * 0.3 + lift, z))
                                for x, z, lift in ((row * 3.7, z0, 0), ((row + 1) * 3.7, z0, 0),
                                                   ((row + 1) * 3.7, z0 + 0.0625, 0.04),
                                                   (row * 3.7, z0 + 0.0625, 0.04),
                                                   ((row + 1) * 3.7, z0 + 0.125, 0), (row * 3.7, z0 + 0.125, 0))]
                    for face in ((0, 1, 2, 3), (3, 2, 4, 5)):
                        roof.face([vertices[i] for i in (reversed(face) if side > 0 else face)], "BarnRust")
    interior = meshes["BarnInterior"]
    for bale in spec["bales"]:
        center, size = xyz(bale["center"]), xyz(bale["size"])
        box(interior, center, size, "BarnStraw")
        for dx in (-0.4, 0.4):
            box(interior, (center[0] + dx, center[1], center[2]),
                (0.045, size[1] + 0.015, size[2] + 0.015), "BarnWeathered")
    for part in spec["loft"]:
        box(interior, xyz(part["center"]), xyz(part["size"]), "BarnWood")
    center, size = xyz(spec["trough"]["center"]), xyz(spec["trough"]["size"])
    box(interior, (center[0], 0.16, center[2]), (size[0], 0.2, size[2]), "BarnWeathered")
    for side in (-1, 1):
        box(interior, (center[0] + side * (size[0] / 2 - 0.08), center[1], center[2]),
            (0.16, size[1], size[2]), "BarnWood")
        box(interior, (center[0], center[1], center[2] + side * (size[2] / 2 - 0.08)),
            (size[0], size[1], 0.16), "BarnWood")
    boards(interior, (-3.5, 0.12, -5.6), (2.2, 0.16, 3.6), rng)
    boards(meshes["BarnCutaway"], (2.15, 0.5, -4), (0.16, 1, 2), rng)
    for part in spec["fence"]:
        center, size = xyz(part["center"]), xyz(part["size"])
        axis = 0 if size[0] > size[2] else 2
        ends = []
        for side in (-1, 1):
            point = list(center)
            point[axis] += side * size[axis] / 2
            ends.append(point)
            meshes["BarnFence"].tube([(point[0], 0, point[2]), (point[0] + 0.04, 1.2, point[2])],
                                      [(0.12, 0.12)] * 2, "BarnWeathered", sides=5)
        for height in (0.4, 0.85):
            meshes["BarnFence"].tube([(p[0], height, p[2]) for p in ends],
                                      [(0.08, 0.08)] * 2, "BarnWood", sides=5)
    mud = meshes["BarnMud"]
    center = mud.vertex((0, spec["mudHeight"], -3.5))
    ring = [mud.vertex((math.cos(i * math.tau / 16) * rng.uniform(1.65, 1.9), spec["mudHeight"],
                        -3.5 + math.sin(i * math.tau / 16) * 2.3)) for i in range(16)]
    for i in range(16):
        mud.face((center, ring[(i + 1) % 16], ring[i]), "BarnMud")
    triangles = sum(len(face) - 2 for mesh in meshes.values() for face in mesh.faces)
    if triangles >= 6000:
        raise ValueError(f"El establo excede el presupuesto: {triangles} triángulos")
    return list(meshes.values())


def preview(folder, model_id, size, center=None):
    azimuth = math.radians(35)

    def direction(elevation):
        elevation = math.radians(elevation)
        return (-math.sin(azimuth) * math.cos(elevation), math.sin(elevation),
                -math.cos(azimuth) * math.cos(elevation))

    render(folder, model_id, size, center,
           views={"front": (0, 0, -1), "threequarter": direction(42), "entrance": direction(35)},
           hide_for_view={"entrance": ("BarnRoof",)})


if __name__ == "__main__":
    run(__file__, build, preview=preview)
