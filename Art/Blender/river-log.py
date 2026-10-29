import math
import os
import sys

from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from lowpoly import Mesh, run
from river_kit import for_export, noise


def growth_rings(mesh, rim, center, bands):
    outer = [mesh.vertices[index] - center for index in rim]
    for scale, material in bands:
        inner = [mesh.vertex(center + point * scale) for point in outer]
        mesh.bridge([rim, inner], material, caps=False)
        rim = inner
    mesh.face(rim, "RiverLogWood")


def build(spec, rng):
    mesh = Mesh("Log")
    length, sides = spec["length"], spec["sides"]
    rings, centers = [], []
    angles = [i * math.tau / sides + rng.uniform(-0.025, 0.025) for i in range(sides)]
    roughness = [rng.uniform(0.965, 1.035) for _ in angles]
    for row, along in enumerate(spec["sections"]):
        t = along + 0.5
        radius = (spec["tipDiameter"] + (spec["diameter"] - spec["tipDiameter"]) * t) / 2
        center = Vector((0.065 * math.sin(t * math.pi), 0.225 + 0.006 * math.sin(t * math.tau), along * length))
        centers.append(center)
        section = []
        for i, angle in enumerate(angles):
            radial = radius * roughness[i] * (1 + 0.018 * noise(i, t * 3.5, 0, spec["seed"]))
            splinter = rng.uniform(-0.045, 0.12) if row == 0 else 0
            section.append(Vector((math.cos(angle) * radial, math.sin(angle) * radial, splinter)))
        low, high = min(p.y for p in section), max(p.y for p in section)
        scale = radius * 2 / (high - low)
        rings.append([mesh.vertex(center + Vector((p.x * scale, (p.y - (low + high) / 2) * scale, p.z)))
                      for p in section])
    for row, (lower, upper) in enumerate(zip(rings, rings[1:])):
        for i, angle in enumerate(angles):
            j = (i + 1) % sides
            a, b, c, d = lower[i], lower[j], upper[j], upper[i]
            moss = math.sin(angle + math.pi / sides) > 0.3 and noise(i * 0.8, row * 0.8, 2, spec["seed"]) > -0.12
            mesh.face((a, b, c), "RiverLogMoss" if moss else "RiverLogBark")
            mesh.face((a, c, d), "RiverLogMoss" if moss and (i + row) % 3 else "RiverLogBark")
    growth_rings(mesh, rings[-1], centers[-1] + Vector((0.012, -0.009, 0)),
                 [(0.82, "RiverLogWood"), (0.76, "RiverLogRing"),
                  (0.49, "RiverLogWood"), (0.43, "RiverLogRing")])
    broken = []
    for i in range(sides):
        delta = mesh.vertices[rings[0][i]] - centers[0]
        broken.append(mesh.vertex(centers[0] + Vector((delta.x * 0.64, delta.y * 0.64, rng.uniform(0.07, 0.16)))))
    mesh.bridge([list(reversed(rings[0])), list(reversed(broken))], "RiverLogWood", caps=False)
    heart = mesh.vertex(centers[0] + Vector((0.014, -0.008, 0.18)))
    for i in range(sides):
        mesh.face((heart, broken[(i + 1) % sides], broken[i]), "RiverLogWood")
    for side in (0, 3, 5):
        ridge = []
        for row in (1, 2, 4, 6, 7):
            a, b = (mesh.vertices[rings[row][i % sides]] for i in (side, side + 1))
            center = (a + b) / 2
            outward = (center - centers[row]).normalized()
            peak = center + outward * (0.012 + 0.008 * math.sin(row * 1.7 + side))
            ridge.append([mesh.vertex(a.lerp(b, 0.35) - outward * 0.001), mesh.vertex(peak),
                          mesh.vertex(a.lerp(b, 0.65) - outward * 0.001)])
        mesh.bridge(ridge, "RiverLogRidges")
    for stub in spec["stubs"]:
        start, end = Vector(stub["start"]), Vector(stub["end"])
        tangent = (end - start).normalized()
        side = Vector((0, 0, 1))
        normal = tangent.cross(side)
        branch = []
        for t, radius in [(0, stub["radius"]), (0.6, stub["radius"] * 0.82), (1, stub["tipRadius"])]:
            center = start.lerp(end, t) + side * (0.025 * math.sin(t * math.pi))
            branch.append([mesh.vertex(center + (side * math.cos(i * math.tau / 6)
                                                 + normal * math.sin(i * math.tau / 6)) * radius) for i in range(6)])
        mesh.bridge(branch, "RiverLogBark", caps=False)
        mesh.face(reversed(branch[0]), "RiverLogBark")
        growth_rings(mesh, branch[-1], end, [(0.74, "RiverLogWood")])
    return for_export([mesh])


if __name__ == "__main__":
    run(__file__, build)
