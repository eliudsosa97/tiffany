import importlib.util
from math import cos, pi, radians, sin, tan
from pathlib import Path
import unittest
from unittest.mock import patch


def load_builder():
    spec = importlib.util.spec_from_file_location("tyler", Path(__file__).with_name("tyler.py"))
    builder = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(builder)
    return builder


class TylerGeometryTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.builder = load_builder()
        cls.tufts = []
        tuft = cls.builder.Part.tuft

        def record_tuft(part, *args):
            cls.tufts.append((len(part.v), args))
            tuft(part, *args)

        with patch.object(cls.builder.Part, "tuft", record_tuft):
            cls.part, _ = cls.builder.build_mesh()
        neighbors = [set() for _ in cls.part.v]
        for face in cls.part.f:
            for vertex in face:
                neighbors[vertex].update(face)
        unseen, cls.components = set(range(len(neighbors))), []
        while unseen:
            component, pending = set(), [unseen.pop()]
            while pending:
                vertex = pending.pop()
                component.add(vertex)
                adjacent = neighbors[vertex] & unseen
                unseen.difference_update(adjacent)
                pending.extend(adjacent)
            cls.components.append(component)

    def test_mesh_respects_budget_and_skin_weights(self):
        bones = self.builder.bone_table()
        self.assertLessEqual(sum(len(face) - 2 for face in self.part.f), 3000)
        self.assertLessEqual(len(bones), 30)
        self.assertEqual(len(self.part.v), len(self.part.w))
        for weights in self.part.w:
            self.assertAlmostEqual(sum(weights.values()), 1)
            self.assertGreaterEqual(min(weights.values()), 0)
            self.assertLessEqual(len(weights), 4)
            self.assertLessEqual(set(weights), set(bones))

    def test_sparse_fur_spans_multiple_pixels_at_game_distance(self):
        tufts = [component for component in self.components if len(component) == 4]
        self.assertGreaterEqual(len(tufts), 20)
        self.assertLessEqual(len(tufts), 30)
        vector = self.builder.Vector
        pitch, yaw = radians(55), pi / 4
        offset = vector((sin(yaw) * cos(pitch), -cos(yaw) * cos(pitch), sin(pitch))) * 8
        forward = -offset.normalized()
        right = forward.cross(vector((0, 0, 1))).normalized()
        up = right.cross(forward)
        camera = vector((0, 0, 0.21)) + offset
        scale = 900 / (2 * tan(radians(24) / 2))
        for component in tufts:
            points = []
            for index in component:
                delta = self.part.v[index] - camera
                points.append(vector((delta.dot(right), delta.dot(up))) * (scale / delta.dot(forward)))
            self.assertGreaterEqual(max((a - b).length for a in points for b in points), 4)
            weights = [self.part.w[index] for index in component]
            self.assertTrue(all(weight == weights[0] for weight in weights))

    def test_fur_stays_short_wide_and_buried_at_the_base(self):
        for start, args in self.tufts:
            root, normal = args[:2]
            normal = self.builder.U(root + normal.normalized()) - self.builder.U(root)
            root = self.builder.U(root)
            tip = self.part.v[start + 3] - root
            self.assertLessEqual(tip.length, 0.015)
            self.assertLessEqual(tip.dot(normal), 0.0035)
            self.assertGreater(tip.dot(normal), 0)
            self.assertGreaterEqual((self.part.v[start + 1] - self.part.v[start]).length, 2 * tip.length)
            for index in range(start, start + 3):
                self.assertLess((self.part.v[index] - root).dot(normal), 0)

    def test_fur_tips_form_pairs_or_triples_without_single_spikes(self):
        unseen = set(range(len(self.tufts)))
        while unseen:
            group, pending = set(), [unseen.pop()]
            while pending:
                index = pending.pop()
                group.add(index)
                start, args = self.tufts[index]
                tip, normal = self.part.v[start + 3], args[1]
                neighbors = {other for other in unseen
                             if (self.part.v[self.tufts[other][0] + 3] - tip).length <= 0.025
                             and normal.normalized().dot(self.tufts[other][1][1].normalized()) > 0.5}
                unseen.difference_update(neighbors)
                pending.extend(neighbors)
            self.assertIn(len(group), (2, 3), group)

    def test_ears_have_closed_edges_and_filled_cross_sections(self):
        ear = self.builder.B["ear"]
        for side in ("L", "R"):
            component, = [component for component in self.components
                          if any(self.part.w[index].get("Ear." + side, 0) > 0 for index in component)]
            faces = [face for face in self.part.f if set(face) <= component]
            edges = {}
            for face in faces:
                for a, b in zip(face, face[1:] + face[:1]):
                    edge = tuple(sorted((a, b)))
                    edges[edge] = edges.get(edge, 0) + 1
            self.assertTrue(all(count == 2 for count in edges.values()))
            sides = len(component) // ear["rings"]
            ids = sorted(component)
            for ring in range(ear["rings"]):
                points = [self.part.v[index] for index in ids[ring * sides:(ring + 1) * sides]]
                center = sum(points, self.builder.Vector()) / sides
                thickness = 2 * min((a - center).cross(b - center).length / (b - a).length
                                    for a, b in zip(points, points[1:] + points[:1]))
                self.assertGreaterEqual(thickness, 0.01 if ring == 0 else 0.0065)

    def test_ear_fold_has_no_hollow_arch_in_profile(self):
        ear = self.builder.B["ear"]
        for sign, side in ((1, "L"), (-1, "R")):
            faces = [face for face in self.part.f
                     if any(self.part.w[index].get("Ear." + side, 0) > 0 for index in face)]
            points = [self.builder.at_head(sign * x, y + (ear["foldAsymmetry"] * sin(pi * i / (len(ear["points"]) - 1))
                                                        if sign < 0 else 0), z)
                      for i, (x, y, z) in enumerate(ear["points"])]
            outer = self.builder.spline(points, ear["rings"])
            axis = self.builder.head_axes()[0]
            for k in (2, 3):
                inner = points[0].lerp(points[-1], k / (ear["rings"] - 1))
                center = (outer[k] + inner) / 2
                hit = self.part.surface(center - axis * 0.1, axis, faces)
                self.assertLess((hit - center).length, 0.025)

    def test_muzzle_freckles_face_outward_and_follow_head(self):
        spots = [component for component in self.components if len(component) == 3]
        self.assertEqual(len(spots), 6)
        for component in spots:
            face_index = next(i for i, face in enumerate(self.part.f) if set(face) == component)
            a, b, c = (self.part.v[index] for index in self.part.f[face_index])
            self.assertGreater((b - a).cross(c - a).x * (a.x + b.x + c.x), 0)
            self.assertEqual(self.part.m[face_index], self.builder.DARK)
            for index in component:
                self.assertEqual(self.part.w[index], {"Head": 1.0})

    def test_game_render_keeps_requested_camera_scale(self):
        camera = self.builder.SPEC["preview"]["gameCamera"]
        self.assertEqual(camera["pitchDeg"], 55)
        self.assertEqual(camera["distanceM"], 8)
        self.assertEqual(camera["verticalFovDeg"], 24)
        self.assertEqual((camera["width"], camera["height"]), (1600, 900))


if __name__ == "__main__":
    unittest.main(argv=[__file__])
