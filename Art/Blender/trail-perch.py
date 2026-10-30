import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from lowpoly import Mesh, run


def build(spec, rng):
    mesh = Mesh("TrailPerch")
    wood = "TrailPerchWood"
    mesh.tube([(0, 0, 0), (0.04, 0.25, 0.01), (-0.035, 0.7, 0),
               (0.025, 1.15, -0.015), (-0.03, 1.6, 0.005), (0, 1.9, -0.03)],
              [(0.09, 0.085), (0.08, 0.075), (0.065, 0.06),
               (0.055, 0.05), (0.04, 0.035), (0.018, 0.015)], wood, sides=7)
    mesh.tube([(0.025, 1.15, -0.015), (0, 1.39, 0.14), (0, 1.5, 0.25), (0, 1.5, 0.48)],
              [(0.045, 0.045), (0.04, 0.04), (0.035, 0.035), (0.035, 0.035)], wood, sides=6)
    mesh.tube([(-0.035, 0.7, 0), (-0.19, 0.88, -0.05), (-0.24, 1.02, -0.08)],
              [(0.035, 0.035), (0.025, 0.025), (0.012, 0.012)], wood, sides=5)
    for x, z in [(0.15, 0.05), (-0.13, 0.08), (0, -0.15)]:
        mesh.tube([(x, 0.025, z), (0, 0.23, 0)],
                  [(0.025, 0.025), (0.04, 0.04)], wood, sides=4)
    assert sum(len(face) - 2 for face in mesh.faces) <= 250
    return [mesh]


if __name__ == "__main__":
    run(__file__, build)
