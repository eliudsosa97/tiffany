import importlib.util
import json
import math
from pathlib import Path
import random
import unittest
from unittest.mock import patch


class WitchHutGeometryTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        source = Path(__file__).with_name("witch-hut.py")
        spec = importlib.util.spec_from_file_location("witch_hut", source)
        cls.builder = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(cls.builder)
        cls.spec = json.loads(source.with_suffix(".json").read_text())
        cls.meshes = cls.builder.build(cls.spec, random.Random(cls.spec["seed"]))
        cls.roof = cls.meshes[1]

    def test_four_meshes_stay_within_budget_and_use_the_declared_materials(self):
        self.assertEqual([mesh.name for mesh in self.meshes], ["HutWalls", "HutRoof", "HutInterior", "HutFence"])
        counts = {mesh.name: sum(len(face) - 2 for face in mesh.faces) for mesh in self.meshes}
        self.assertLessEqual(sum(counts.values()), 6000)
        self.assertEqual({material for mesh in self.meshes for material in mesh.materials},
                         {entry["name"] for entry in self.spec["materials"]})
        for mesh in self.meshes:
            self.assertTrue(all(math.isfinite(value) for point in mesh.vertices for value in point))
            self.assertEqual(len(mesh.faces), len(mesh.materials))
        points = [point for mesh in self.meshes for point in mesh.vertices]
        self.assertAlmostEqual(min(point[1] for point in points), 0, delta=0.02)
        for axis in (0, 2):
            self.assertLessEqual(max(point[axis] for point in points) - min(point[axis] for point in points), 11)
        print(counts, "total:", sum(counts.values()))

    def test_roof_has_hundreds_of_narrow_bent_double_sided_palm_strips(self):
        faces = [face for face, material in zip(self.roof.faces, self.roof.materials) if "Palm" in material]
        neighbors = {}
        for face in faces:
            for index in face:
                neighbors.setdefault(index, set()).update(face)
        unseen, strips = set(neighbors), []
        while unseen:
            pending, component = [unseen.pop()], set()
            while pending:
                index = pending.pop()
                component.add(index)
                adjacent = neighbors[index] & unseen
                unseen.difference_update(adjacent)
                pending.extend(adjacent)
            strips.append(component)
        self.assertGreater(len(strips), 200)
        self.assertFalse(self.roof.recalculate_normals)
        for indices in strips:
            self.assertEqual(len(indices), 5)
            points = [self.roof.vertices[index] for index in indices]
            self.assertLess(max(point[2] for point in points) - min(point[2] for point in points), 0.5)
            tip = max(points, key=lambda point: abs(point[0]))
            self.assertLess(tip[1], 4.5 - abs(tip[0]) * 0.39 - 0.1)
        for face in faces:
            self.assertIn(tuple(reversed(face)), faces)
            a, b, c = (self.roof.vertices[index] for index in face)
            self.assertGreater((b - a).cross(c - a).length, 0.0001)

    def test_both_roof_holes_stay_open_and_neighboring_palm_stays_covered(self):
        def covered(x, z):
            for face, material in zip(self.roof.faces, self.roof.materials):
                if "Palm" not in material:
                    continue
                for i in range(1, len(face) - 1):
                    a, b, c = [self.roof.vertices[index] for index in (face[0], face[i], face[i + 1])]
                    if abs((b[0] - a[0]) * (c[2] - a[2]) - (b[2] - a[2]) * (c[0] - a[0])) < 0.000001:
                        continue
                    edges = [(q[0] - p[0]) * (z - p[2]) - (q[2] - p[2]) * (x - p[0])
                             for p, q in ((a, b), (b, c), (c, a))]
                    if min(edges) >= 0 or max(edges) <= 0:
                        return True
            return False
        for x, z in ((-2.2, -1.25), (3.85, 3.3)):
            self.assertFalse(covered(x, z))
        for x, z in ((-2.2, 0.9), (3.85, 1.8)):
            self.assertTrue(covered(x, z))

    def test_pale_straw_dominates_with_darker_patches(self):
        dry = self.roof.materials.count("WitchHutPalmDry")
        dark = self.roof.materials.count("WitchHutPalm")
        self.assertGreater(dry / (dry + dark), 0.7)
        self.assertGreater(dark, 0)
        colors = {entry["name"]: entry["color"] for entry in self.spec["materials"]}
        pale, shaded = colors["WitchHutPalmDry"], colors["WitchHutPalm"]
        self.assertGreater(int(pale[1:3], 16), 210)
        self.assertGreater(int(pale[3:5], 16), 180)
        self.assertGreater(int(pale[5:7], 16), 120)
        self.assertLess(int(shaded[3:5], 16), int(pale[3:5], 16))

    def test_three_horned_skulls_and_three_dolls_hang_from_distinct_fence_posts(self):
        charms = self.spec.get("fenceCharms", [])
        self.assertEqual([charm["kind"] for charm in charms].count("skull"), 3)
        self.assertEqual([charm["kind"] for charm in charms].count("doll"), 3)
        ropes, posts, horns = [], [], []
        stick = self.builder.stick
        def record(mesh, points, radius=0.07, material="WitchHutWood", sides=5):
            if mesh.name == "HutFence":
                if radius == 0.012:
                    ropes.append(len(mesh.vertices))
                elif radius == 0.055:
                    posts.append(points[-1])
            stick(mesh, points, radius, material, sides)
        mesh = self.builder.Mesh("HutFence")
        tube = mesh.tube
        def record_tube(points, sections, material, sides=8, ref=(1, 0, 0)):
            if material == "WitchHutBone":
                horns.append(points)
            tube(points, sections, material, sides, ref)
        with patch.object(self.builder, "stick", side_effect=record), patch.object(mesh, "tube", side_effect=record_tube):
            self.builder.fence(mesh, self.spec, random.Random(self.spec["seed"]))
        self.assertEqual(len(horns), 6)
        self.assertEqual(len(ropes), 6)
        anchors = []
        for index in ropes:
            anchor = tuple(sum(mesh.vertices[index + i][axis] for i in range(3)) / 3 for axis in range(3))
            self.assertTrue(any(math.dist(anchor, post) < 0.00001 for post in posts))
            self.assertTrue(all(math.dist(anchor, other) > 0.1 for other in anchors))
            anchors.append(anchor)


if __name__ == "__main__":
    unittest.main(argv=[__file__])
