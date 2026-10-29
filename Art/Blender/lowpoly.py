import argparse
import json
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Vector


def to_blender(point):
    return Vector((-point[0], -point[2], point[1]))


def linear(channel):
    return channel / 12.92 if channel <= 0.04045 else ((channel + 0.055) / 1.055) ** 2.4


def make_materials(entries):
    materials = {}
    for entry in entries:
        name = entry["name"]
        material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        material.use_nodes = True
        color = entry["color"].lstrip("#")
        rgba = tuple(linear(int(color[i:i + 2], 16) / 255) for i in (0, 2, 4)) + (1,)
        material.diffuse_color = rgba
        shader = material.node_tree.nodes["Principled BSDF"]
        shader.inputs["Base Color"].default_value = rgba
        shader.inputs["Roughness"].default_value = 1 - entry["smoothness"]
        materials[name] = material
    return materials


class Mesh:
    def __init__(self, name, recalculate_normals=True):
        self.name = name
        self.vertices, self.faces, self.materials = [], [], []
        self.recalculate_normals = recalculate_normals

    def vertex(self, point):
        self.vertices.append(Vector(point))
        return len(self.vertices) - 1

    def face(self, indices, material):
        self.faces.append(tuple(indices))
        self.materials.append(material)

    def bridge(self, rings, material, caps=True):
        sides = len(rings[0])
        for lower, upper in zip(rings, rings[1:]):
            for i in range(sides):
                j = (i + 1) % sides
                self.face((lower[i], lower[j], upper[j], upper[i]), material)
        if caps:
            self.face(reversed(rings[0]), material)
            self.face(rings[-1], material)

    def tube(self, points, sections, material, sides=8, ref=(1, 0, 0)):
        points, ref = [Vector(p) for p in points], Vector(ref)
        rings = []
        for k, point in enumerate(points):
            tangent = (points[min(k + 1, len(points) - 1)] - points[max(k - 1, 0)]).normalized()
            side = ref - tangent * ref.dot(tangent)
            side = side.normalized() if side.length > 1e-4 else tangent.orthogonal().normalized()
            normal = tangent.cross(side)
            a, b = sections[k]
            rings.append([self.vertex(point + side * (a * math.cos(i * math.tau / sides))
                                      + normal * (b * math.sin(i * math.tau / sides))) for i in range(sides)])
        self.bridge(rings, material)

    def clump(self, center, radii, material, rng, sides=8, rows=3):
        center = Vector(center)
        bottom = self.vertex(center - Vector((0, radii[1], 0)))
        rings = []
        for row in range(1, rows + 1):
            latitude = -math.pi / 2 + math.pi * row / (rows + 1)
            ring = []
            for i in range(sides):
                angle = i * math.tau / sides
                roughness = 1 if i % 2 == 0 else rng.uniform(0.82, 1)
                ring.append(self.vertex(center + Vector((radii[0] * math.cos(latitude) * math.cos(angle) * roughness,
                                                         radii[1] * math.sin(latitude),
                                                         radii[2] * math.cos(latitude) * math.sin(angle) * roughness))))
            rings.append(ring)
        self.bridge(rings, material, caps=False)
        top = self.vertex(center + Vector((0, radii[1], 0)))
        for i in range(sides):
            j = (i + 1) % sides
            self.face((bottom, rings[0][j], rings[0][i]), material)
            self.face((top, rings[-1][i], rings[-1][j]), material)


def finish(meshes, materials, pivot=None):
    points = [point for mesh in meshes for point in mesh.vertices]
    low = Vector(tuple(min(point[i] for point in points) for i in range(3)))
    high = Vector(tuple(max(point[i] for point in points) for i in range(3)))
    offset = Vector(pivot) if pivot is not None else Vector(((low.x + high.x) / 2, low.y, (low.z + high.z) / 2))
    objects = []
    for source in meshes:
        mesh = bpy.data.meshes.new(source.name)
        mesh.from_pydata([to_blender(point - offset) for point in source.vertices], [], source.faces)
        names = list(dict.fromkeys(source.materials))
        for name in names:
            mesh.materials.append(materials[name])
        for polygon, name in zip(mesh.polygons, source.materials):
            polygon.material_index = names.index(name)
            polygon.use_smooth = False
        if source.recalculate_normals:
            bm = bmesh.new()
            bm.from_mesh(mesh)
            bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
            bm.to_mesh(mesh)
            bm.free()
        mesh.update()
        obj = bpy.data.objects.new(source.name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        objects.append(obj)
        triangles = sum(len(face) - 2 for face in source.faces)
        print(f"[Tiffany] {source.name}: {triangles} triangulos")
    print(f"[Tiffany] Altura: {high.y - low.y:.3f} m")
    return objects, high - low


def add(name, data, position, target=None):
    obj = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = to_blender(position)
    if target is not None:
        obj.rotation_euler = (to_blender(target) - obj.location).to_track_quat("-Z", "Y").to_euler()
    return obj


def render(folder, model_id, size, center=None):
    os.makedirs(folder, exist_ok=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x, scene.render.resolution_y = 800, 900
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.view_settings.view_transform = "Standard"
    scene.world = bpy.data.worlds.new("Night")
    scene.world.use_nodes = True
    scene.world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.003, 0.003, 0.005, 1)
    span = max(size)
    target = Vector(center) if center is not None else Vector((0, size.y / 2, 0))
    ground = bpy.data.meshes.new("Ground")
    ground.from_pydata([(-span, -span, 0), (span, -span, 0), (span, span, 0), (-span, span, 0)], [], [(0, 1, 2, 3)])
    ground.materials.append(make_materials([{"name": "PreviewGround", "color": "#17171C", "smoothness": 0}])["PreviewGround"])
    add("Ground", ground, (target.x, target.y - size.y / 2 - 0.01, target.z))
    warm = bpy.data.lights.new("Warm", "POINT")
    warm.energy, warm.color, warm.shadow_soft_size = 100 * span * span, (1.0, 0.72, 0.42), span * 0.12
    add("Warm", warm, target + Vector((span * 0.5, span * 0.25 - size.y / 2, span * 1.2)))
    moon = bpy.data.lights.new("Moon", "SUN")
    moon.energy, moon.color = 0.35, (0.55, 0.65, 1.0)
    add("Moon", moon, (-3, 8, -4), (0, 0, 0))
    camera = add("Camera", bpy.data.cameras.new("Camera"), (0, 0, 0))
    camera.data.type, camera.data.ortho_scale = "ORTHO", span * 1.7
    scene.camera = camera
    elevation, azimuth = math.radians(42), math.radians(35)
    views = {"front": Vector((0, 0, 1)),
             "threequarter": Vector((math.sin(azimuth) * math.cos(elevation), math.sin(elevation),
                                      math.cos(azimuth) * math.cos(elevation)))}
    for view, direction in views.items():
        camera.location = to_blender(target + direction * span * 3)
        camera.rotation_euler = (to_blender(target) - camera.location).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = os.path.join(folder, f"{model_id}_{view}.png")
        bpy.ops.render.render(write_still=True)


def run(script, build):
    parser = argparse.ArgumentParser()
    parser.add_argument("--render")
    parser.add_argument("--no-export", action="store_true")
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
    folder = os.path.dirname(os.path.abspath(script))
    model_id = os.path.splitext(os.path.basename(script))[0]
    with open(os.path.join(folder, model_id + ".json"), encoding="utf-8") as source:
        spec = json.load(source)
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    scene = bpy.context.scene
    scene.unit_settings.system, scene.unit_settings.scale_length = "METRIC", 1
    meshes = build(spec, random.Random(spec["seed"]))
    pivot = spec.get("pivot")
    objects, size = finish(meshes, make_materials(spec["materials"]), pivot)
    if not args.no_export:
        path = os.path.abspath(os.path.join(folder, "..", "..", "Assets", "Art", "Models", model_id + ".fbx"))
        os.makedirs(os.path.dirname(path), exist_ok=True)
        bpy.ops.export_scene.fbx(filepath=path, object_types={"MESH"}, axis_forward="-Z", axis_up="Y",
                                 apply_scale_options="FBX_SCALE_ALL", bake_space_transform=True, mesh_smooth_type="FACE",
                                 add_leaf_bones=False, bake_anim=False, use_custom_props=False)
        print(f"[Tiffany] Exportado {path}")
    if args.render:
        center = None
        if pivot is not None:
            points = [point for mesh in meshes for point in mesh.vertices]
            center = Vector(tuple((min(p[i] for p in points) + max(p[i] for p in points)) / 2 - pivot[i]
                                  for i in range(3)))
        render(args.render, model_id, size, center)
    if not bpy.app.background:
        for obj in objects:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = objects[0]
        for area in (a for screen in bpy.data.screens for a in screen.areas if a.type == "VIEW_3D"):
            area.spaces.active.shading.color_type = "MATERIAL"
