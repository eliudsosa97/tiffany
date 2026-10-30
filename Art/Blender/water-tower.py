import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from lowpoly import Mesh, run


def build(spec, rng):
    mesh = Mesh("WaterTower")
    rust, metal = "WaterTowerRust", "WaterTowerMetal"
    mesh.tube([(0, 0, 0), (0, 0.18, 0)], [(2.5173, 2.5173)] * 2,
              metal, sides=4, ref=(1, 0, 1))

    def bar(start, end, radius=0.055, sides=4, material=metal):
        mesh.tube([start, end], [(radius, radius)] * 2, material, sides=sides)

    def cylinder(x, z, bottom, top, radius, material=metal, sides=12):
        mesh.tube([(x, bottom, z), (x, top, z)], [(radius, radius)] * 2,
                  material, sides=sides)

    cylinder(0, 0, 6, 9.5, 2, rust, 16)
    roof = [mesh.vertex((2.12 * math.cos(i * math.tau / 16), 9.5,
                         2.12 * math.sin(i * math.tau / 16))) for i in range(16)]
    tip = mesh.vertex((0, 10.05, 0))
    for i in range(16):
        mesh.face((roof[i], roof[(i + 1) % 16], tip), rust)
    for height in (6, 9.42):
        cylinder(0, 0, height, height + 0.08, 2.03)
    for x in (-1.4, 1.4):
        for z in (-1.4, 1.4):
            bar((x, 0, z), (x, 6, z), 0.18)
            cylinder(x, z, 0, 0.18, 0.38, sides=4)
    for side in (-1.4, 1.4):
        for low, high in ((0.4, 3), (3, 5.8)):
            for start, end in ((-1.4, 1.4), (1.4, -1.4)):
                bar((side, low, start), (side, high, end))
                bar((start, low, side), (end, high, side))
    for x in (-1.65, -1.15):
        bar((x, 0.2, -2.18), (x, 9.7, -2.18), 0.035)
    for step in range(31):
        height = 0.35 + step * 0.3
        bar((-1.65, height, -2.18), (-1.15, height, -2.18), 0.025)
    for height in (1, 3, 5.8):
        bar((-1.4, height, -1.4), (-1.4, height, -2.18), 0.035)
    mesh.tube([(0, 6, 0), (0, 5.5, 0), (0.6, 5.1, 0), (0.6, 0.15, 0)],
              [(0.14, 0.14)] * 4, metal, sides=6)
    cylinder(2.8, 0, 0, 0.12, 0.32, sides=8)
    cylinder(2.8, 0, 0.12, 0.95, 0.11, rust, 8)
    cylinder(2.8, 0, 0.87, 1.2, 0.07, sides=6)
    mesh.tube([(2.8, 0.8, 0), (2.8, 0.8, -0.35), (2.8, 0.68, -0.4)],
              [(0.065, 0.065)] * 3, metal, sides=6)
    bar((2.8, 1.18, 0), (3.3, 0.85, 0), 0.045)
    bar((3.3, 0.85, -0.12), (3.3, 0.85, 0.12), 0.05)
    assert sum(len(face) - 2 for face in mesh.faces) <= 3000
    return [mesh]


if __name__ == "__main__":
    run(__file__, build)
