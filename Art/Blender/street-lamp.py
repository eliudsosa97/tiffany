import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from lowpoly import Mesh, run


def box(mesh, center, size, material):
    vertices = [mesh.vertex(tuple(center[i] + size[i] * sign[i] * 0.5 for i in range(3)))
                for sign in ((-1, -1, -1), (1, -1, -1), (1, 1, -1), (-1, 1, -1),
                             (-1, -1, 1), (1, -1, 1), (1, 1, 1), (-1, 1, 1))]
    for face in ((0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4),
                 (3, 7, 6, 2), (0, 4, 7, 3), (1, 2, 6, 5)):
        mesh.face([vertices[i] for i in face], material)


def build(spec, rng):
    pole, fixture, bulb = (Mesh(name) for name in ("Pole", "Fixture", "Bulb"))
    height, sides = spec["trunkCollider"]["height"], spec["sides"]
    pedestal, neck = spec["pedestal"], spec["neck"]
    pedestal_radius, tip_radius = pedestal["diameter"] * 0.5, spec["tipDiameter"] * 0.5
    stem_top = height - neck["radius"] - tip_radius
    pole.tube([(0, 0, 0), (0, pedestal["height"] - 0.03, 0), (0, pedestal["height"], 0)],
              [(pedestal_radius, pedestal_radius)] * 2 + [(pedestal_radius * 0.9, pedestal_radius * 0.9)],
              "StreetLampConcrete", sides=sides)
    pole.tube([(0, pedestal["height"] - 0.02, 0), (0, stem_top, 0)],
              [(spec["diameter"] * 0.5,) * 2, (tip_radius,) * 2], "StreetLampMetal", sides=sides)
    rings = []
    for step in range(neck["segments"] + 1):
        angle = math.radians(neck["angle"]) * step / neck["segments"]
        y = stem_top + neck["radius"] * math.sin(angle)
        z = neck["radius"] * (1 - math.cos(angle))
        rings.append([fixture.vertex((tip_radius * math.cos(i * math.tau / sides),
                                      y + tip_radius * math.sin(i * math.tau / sides) * math.sin(angle),
                                      z - tip_radius * math.sin(i * math.tau / sides) * math.cos(angle)))
                      for i in range(sides)])
    fixture.bridge(rings, "StreetLampMetal")
    offset = spec["lightOffset"]
    x, y, z = offset["x"], offset["y"], offset["z"]
    housing = spec["housing"]
    box(fixture, (x, y + housing["height"] * 0.5 + 0.004, z),
        (housing["width"], housing["height"], housing["length"]), "StreetLampHousing")
    box(bulb, (x, y, z), (housing["width"] - 0.04, 0.012, housing["length"] - 0.04), "StreetLampBulb")
    meshes = [pole, fixture, bulb]
    triangles = sum(len(face) - 2 for mesh in meshes for face in mesh.faces)
    if triangles > 600:
        raise ValueError(f"El poste excede el presupuesto: {triangles} triángulos")
    return meshes


if __name__ == "__main__":
    run(__file__, build)
