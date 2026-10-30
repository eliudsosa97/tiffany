import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from lowpoly import Mesh, run


def build(spec, rng):
    mesh = Mesh("Ball", smooth=True)
    radius = spec["diameter"] / 2
    sides = 24
    rings = []
    levels = (-0.8, -0.6, -0.4, -0.2, 0, 0, 0.2, 0.4, 0.6, 0.8)
    for row, level in enumerate(levels):
        sign = -1 if row < 5 else 1
        ring = []
        for i in range(sides):
            angle = i * math.tau / sides
            seam = 1.05 * math.cos(2 * angle) + sign * 0.04
            latitude = seam * (1 - abs(level)) + sign * math.pi / 2 * abs(level)
            ring.append(mesh.vertex((radius * math.cos(latitude) * math.cos(angle),
                                     radius * math.cos(latitude) * math.sin(angle),
                                     radius * math.sin(latitude))))
        rings.append(ring)
    for row, (lower, upper) in enumerate(zip(rings, rings[1:])):
        mesh.bridge([lower, upper], "BallWhite" if row == 4 else "BallBlue", caps=False)
    bottom = mesh.vertex((0, 0, -radius))
    top = mesh.vertex((0, 0, radius))
    for i in range(sides):
        j = (i + 1) % sides
        mesh.face((bottom, rings[0][j], rings[0][i]), "BallBlue")
        mesh.face((top, rings[-1][i], rings[-1][j]), "BallBlue")
    assert 300 <= sum(len(face) - 2 for face in mesh.faces) <= 800
    return [mesh]


if __name__ == "__main__":
    run(__file__, build)
