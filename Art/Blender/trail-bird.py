import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from lowpoly import Mesh, run


def build(spec, rng):
    mesh = Mesh("TrailBird")
    mesh.clump((0, 0.064, -0.008), (0.037, 0.046, 0.062), "TrailBirdYellow", rng, sides=8, rows=3)
    mesh.clump((0, 0.108, 0.047), (0.027, 0.028, 0.029), "TrailBirdYellow", rng, sides=8, rows=2)
    for side in (-1, 1):
        mesh.clump((side * 0.032, 0.065, -0.013), (0.01, 0.024, 0.045),
                   "TrailBirdWing", rng, sides=6, rows=2)
        mesh.clump((side * 0.024, 0.114, 0.059), (0.003, 0.003, 0.003),
                   "TrailBirdBeak", rng, sides=4, rows=1)
        x = side * 0.016
        mesh.tube([(x, 0.026, 0.004), (x, 0.006, 0.006)],
                  [(0.0025, 0.0025), (0.002, 0.002)], "TrailBirdBeak", sides=4)
        mesh.tube([(x, 0.003, -0.006), (x, 0.003, 0.029)],
                  [(0.003, 0.003), (0.002, 0.002)], "TrailBirdBeak", sides=4)
    mesh.tube([(0, 0.045, -0.055), (0, 0.054, -0.114)],
              [(0.023, 0.007), (0.018, 0.003)], "TrailBirdWing", sides=4)
    mesh.tube([(0, 0.108, 0.072), (0, 0.105, 0.106)],
              [(0.011, 0.009), (0.0007, 0.0007)], "TrailBirdBeak", sides=4)
    assert sum(len(face) - 2 for face in mesh.faces) <= 300
    return [mesh]


if __name__ == "__main__":
    run(__file__, build)
