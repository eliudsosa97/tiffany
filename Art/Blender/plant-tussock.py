import math
import os
import sys

from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from lowpoly import Mesh, run
from river_kit import for_export


def build(spec, rng):
    mesh = Mesh("Tussock")
    for i in range(spec["leaves"]):
        angle = i * math.pi * (3 - math.sqrt(5)) + rng.uniform(-0.2, 0.2)
        direction = Vector((math.cos(angle), 0, math.sin(angle)))
        side = Vector((-math.sin(angle), 0, math.cos(angle)))
        short = i < spec["shortLeaves"]
        height = spec["height"] * (rng.uniform(0.38, 0.58) if short else
                                   1 if i == spec["shortLeaves"] else rng.uniform(0.65, 0.98))
        reach = spec["radius"] * rng.uniform(0.58, 1)
        width = spec["leafWidth"] * rng.uniform(0.65, 1)
        root = direction * rng.uniform(0.025, 0.08)
        dry = i % 5 == 2
        material = "PlantTussockDryTips" if dry else "PlantTussockShade" if i % 4 == 0 else "PlantTussockLeaves"
        samples = (0, 0.7) if short else (0, 0.42, 0.72)
        points = []
        for t in samples:
            y = -spec["burial"] if t == 0 else height * math.sin(t * math.pi * 0.7)
            points.append(root + direction * (reach * t ** 1.65) + Vector((0, y, 0)))
        points.append(root + direction * reach + Vector((0, height * rng.uniform(0.57, 0.72), 0)))
        rings = []
        for row, t in enumerate(samples):
            tangent = (points[row + 1] - points[max(0, row - 1)]).normalized()
            normal = tangent.cross(side)
            center = points[row]
            half_width = width * (0.45 + 0.55 * math.sin(t * math.pi)) / 2
            fold = 0.0025 * (1 - t * 0.65)
            rings.append([mesh.vertex(center - side * half_width), mesh.vertex(center + side * half_width),
                          mesh.vertex(center + normal * fold)])
        mesh.face(reversed(rings[0]), material)
        mesh.bridge(rings, material, caps=False)
        tip = mesh.vertex(points[-1])
        for j in range(3):
            mesh.face((rings[-1][j], rings[-1][(j + 1) % 3], tip),
                      "PlantTussockDryTips" if dry or i % 3 == 0 else material)
    low = [min(point[i] for point in mesh.vertices) for i in (0, 2)]
    high = [max(point[i] for point in mesh.vertices) for i in (0, 2)]
    offset = Vector(((low[0] + high[0]) / 2, 0, (low[1] + high[1]) / 2))
    mesh.vertices = [point - offset for point in mesh.vertices]
    return for_export([mesh])


if __name__ == "__main__":
    run(__file__, build)
