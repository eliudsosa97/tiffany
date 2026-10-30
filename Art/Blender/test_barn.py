import importlib.util
import json
import math
from pathlib import Path
import random
import re
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import MagicMock, patch


class BarnGeometryTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        source = Path(__file__).with_name("barn.py")
        module = importlib.util.spec_from_file_location("barn", source)
        cls.builder = importlib.util.module_from_spec(module)
        module.loader.exec_module(cls.builder)
        cls.spec = json.loads(source.with_suffix(".json").read_text())

    def test_budget_cutaway_door_and_mud_preserve_the_clearing(self):
        builder, spec = self.builder, self.spec
        meshes = builder.build(spec, random.Random(spec["seed"]))
        self.assertEqual(len(meshes), 6)
        counts = {mesh.name: sum(len(face) - 2 for face in mesh.faces) for mesh in meshes}
        self.assertLess(sum(counts.values()), 6000)
        self.assertEqual({entry["name"] for entry in spec["materials"]},
                         {material for mesh in meshes for material in mesh.materials})
        cutaway = next(mesh for mesh in meshes if mesh.name == "BarnCutaway")
        self.assertLessEqual(max(point[1] for point in cutaway.vertices), 1)
        for mesh in meshes:
            for face in mesh.faces:
                points = [mesh.vertices[index] for index in face]
                if max(point[1] for point in points) <= 0.3 or min(point[1] for point in points) >= 2:
                    continue
                if min(point[2] for point in points) <= -5 <= max(point[2] for point in points):
                    self.assertTrue(max(point[0] for point in points) <= -2 or min(point[0] for point in points) >= 2)
        mud = next(mesh for mesh in meshes if mesh.name == "BarnMud")
        self.assertTrue(all(point[1] == spec["mudHeight"] for point in mud.vertices))
        self.assertEqual(4, len(spec["bales"]))
        print(counts, "total:", sum(counts.values()))

    def test_walls_have_missing_and_short_boards_with_three_wood_shades(self):
        mesh = next(mesh for mesh in self.builder.build(self.spec, random.Random(self.spec["seed"]))
                    if mesh.name == "BarnWalls")
        count = len(mesh.vertices) // 8
        intact_count = sum(math.ceil(max(wall["size"]["x"], wall["size"]["z"]) / 0.42)
                           for wall in self.spec["walls"] if not wall["cameraFacing"])
        self.assertLess(count, intact_count)
        self.assertTrue(any(max(point[1] for point in mesh.vertices[i:i + 8])
                            - min(point[1] for point in mesh.vertices[i:i + 8]) < 4
                            for i in range(0, len(mesh.vertices), 8)))
        self.assertEqual(set(mesh.materials), {"BarnWood", "BarnWeathered", "BarnWoodLight"})

    def test_unity_wood_materials_match_the_grey_palette(self):
        palette = {"BarnWood": "#5E5A52", "BarnWeathered": "#45413B", "BarnWoodLight": "#77716A"}
        entries = {entry["name"]: entry for entry in self.spec["materials"]}
        folder = Path(__file__).resolve().parents[2] / "Assets/Art/Models/Materials"
        for name, color in palette.items():
            self.assertEqual(entries[name]["color"], color)
            self.assertEqual(entries[name]["smoothness"], 0)
            material = (folder / (name + ".mat")).read_text()
            for property_name in ("_BaseColor", "_Color"):
                match = re.search(rf"- {property_name}: \{{r: ([\d.]+), g: ([\d.]+), b: ([\d.]+), a: 1\}}", material)
                self.assertIsNotNone(match, name)
                for value, offset in zip(match.groups(), (1, 3, 5)):
                    self.assertAlmostEqual(float(value), int(color[offset:offset + 2], 16) / 255)

    def test_preview_faces_the_entrance_and_adds_a_roofless_view_at_35_degrees(self):
        with patch.object(self.builder, "render") as render:
            self.builder.preview("previews", "barn", (16.5, 7.24, 11))
        views = render.call_args.kwargs["views"]
        self.assertEqual(set(views), {"front", "threequarter", "entrance"})
        self.assertEqual(views["front"], (0, 0, -1))
        self.assertLess(views["threequarter"][0], 0)
        self.assertLess(views["threequarter"][2], 0)
        direction = views["entrance"]
        self.assertLess(direction[2], 0)
        self.assertAlmostEqual(math.degrees(math.atan2(direction[1], math.hypot(direction[0], direction[2]))), 35)
        self.assertEqual(render.call_args.kwargs["hide_for_view"], {"entrance": ("BarnRoof",)})

    def test_render_preserves_other_models_views_and_restores_the_roof_even_on_failure(self):
        renderer = sys.modules["lowpoly"]
        roof = SimpleNamespace(hide_render=False)
        bpy = MagicMock()
        bpy.data.objects = {"BarnRoof": roof}
        camera = SimpleNamespace(data=SimpleNamespace())
        frames = []

        def capture(write_still):
            self.assertTrue(write_still)
            frames.append((Path(bpy.context.scene.render.filepath).name, roof.hide_render))

        bpy.ops.render.render.side_effect = capture
        with tempfile.TemporaryDirectory() as folder, patch.object(renderer, "bpy", bpy), \
             patch.object(renderer, "add", return_value=camera), \
             patch.object(renderer, "make_materials", return_value={"PreviewGround": object()}):
            size = renderer.Vector((16.5, 7.24, 11))
            renderer.render(folder, "other", size)
            self.builder.preview(folder, "barn", size)
            self.assertEqual(frames, [("other_front.png", False), ("other_threequarter.png", False),
                                      ("barn_front.png", False), ("barn_threequarter.png", False),
                                      ("barn_entrance.png", True)])
            self.assertFalse(roof.hide_render)
            bpy.ops.render.render.side_effect = RuntimeError("render failed")
            with self.assertRaisesRegex(RuntimeError, "render failed"):
                renderer.render(folder, "barn", size, views={"entrance": (0, 1, -1)},
                                hide_for_view={"entrance": ("BarnRoof",)})
            self.assertFalse(roof.hide_render)


if __name__ == "__main__":
    unittest.main(argv=[__file__])
