import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from lowpoly import Mesh, run


def build(spec, rng):
    bush = Mesh("Bush")
    wall = spec["wall"]
    angles = [math.radians(angle) for angle in wall["angles"]]
    points = [(math.sin(angle) * wall["radius"], wall["height"] / 2,
               math.cos(angle) * wall["radius"]) for angle in angles]
    bush.tube(points, [(wall["height"] / 2, wall["thickness"] / 2)] * len(points),
              "BougainvilleaLeaves", ref=(0, 1, 0))
    for center in spec["roof"]["centers"]:
        bush.clump(center, spec["roof"]["radii"], "BougainvilleaLeaves", rng)
    for index in range(spec["flowers"]["count"]):
        angle = angles[index % len(angles)]
        radius = wall["radius"] + wall["thickness"] * 0.35
        y = rng.uniform(1.1, 1.65)
        bush.clump((math.sin(angle) * radius, y, math.cos(angle) * radius),
                   spec["flowers"]["radii"], "BougainvilleaFlowers", rng, rows=1)
    for x in (-0.5, 0.35):
        bush.clump((x, 1.95, -0.3), (0.24, 0.07, 0.28), "BougainvilleaFlowers", rng, rows=1)
    return [bush]


if __name__ == "__main__":
    run(__file__, build)
