import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from lowpoly import Mesh, run


def build(spec, rng):
    trunk, canopy = Mesh("Trunk"), Mesh("Canopy")
    trunk.tube(spec["trunk"], [(r, r) for r in spec["trunkRadii"]], "OakBark")
    for index, crown in enumerate(spec["crowns"]):
        x, y, z = crown["center"]
        trunk.tube([spec["trunk"][2], (x * 0.55, y - 1.1, z * 0.55), (x, y, z)],
                   [(0.22, 0.22), (0.14, 0.14), (0.06, 0.06)], "OakBark", sides=6)
        canopy.clump(crown["center"], crown["radii"], "OakLeaves" if index % 3 else "OakTips", rng)
    return [trunk, canopy]


if __name__ == "__main__":
    run(__file__, build)
