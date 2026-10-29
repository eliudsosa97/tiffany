import json, math, os, sys
import bpy, bmesh
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
SPEC = json.load(open(os.path.join(HERE, "candle.json")))
FBX = os.path.join(HERE, "..", "..", "Assets", "Art", "Models", "candle.fbx")


def to_blender(p):
    return Vector((-p[0], -p[2], p[1]))


def linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


class Geometry:
    def __init__(self):
        self.vertices, self.faces, self.materials = [], [], []

    def lathe(self, profile, material, sides=None, center=(0, 0)):
        sides = sides or SPEC["sides"]
        rings = []
        for radius, height in profile:
            ring = []
            for i in range(sides):
                angle = 2 * math.pi * i / sides
                y = height[i] if isinstance(height, list) else height
                ring.append(len(self.vertices))
                self.vertices.append(to_blender((center[0] + radius * math.cos(angle), y,
                                                 center[1] + radius * math.sin(angle))))
            rings.append(ring)
        faces = [rings[0][::-1], rings[-1]]
        for lower, upper in zip(rings, rings[1:]):
            faces += [(lower[i], lower[(i + 1) % sides], upper[(i + 1) % sides], upper[i]) for i in range(sides)]
        self.faces.extend(faces)
        self.materials.extend([material] * len(faces))

    def finish(self, name, parent=None):
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata(self.vertices, [], self.faces)
        names = list(dict.fromkeys(self.materials))
        for material in names:
            mesh.materials.append(bpy.data.materials[material])
        for polygon, material in zip(mesh.polygons, self.materials):
            polygon.material_index = names.index(material)
        bm = bmesh.new()
        bm.from_mesh(mesh)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        bm.to_mesh(mesh)
        bm.free()
        obj = bpy.data.objects.new(name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        obj.parent = parent
        return obj


def build(name):
    family = name == "CandleFamily"
    cfg = SPEC["family" if family else "kindled"]
    radius, height = cfg["radius"], cfg["height"]
    body = Geometry()
    if family:
        thickness = cfg["glassThickness"]
        body.lathe([(radius, 0), (radius, height), (radius - thickness, height),
                    (radius - thickness, thickness)], "CandleGlass")
        wax_top = cfg["waxHeight"]
        body.lathe([(radius - thickness - 0.0005, thickness),
                    (radius - thickness - 0.0005, wax_top)], "CandleWax")
    else:
        wax_top = height - 0.008
        rim = [height + offset for offset in cfg["rimOffsets"]]
        body.lathe([(radius, 0), (radius, height * 0.7), (radius * 0.95, rim),
                    (radius * 0.35, wax_top)], "CandleWax")
        for drip in cfg["drips"]:
            angle = math.radians(drip["angleDeg"])
            center = (radius * 0.95 * math.cos(angle), radius * 0.95 * math.sin(angle))
            bottom = drip["bottom"]
            body.lathe([(0.0008, bottom), (0.003, bottom + 0.004),
                        (0.002, height - 0.006)], "CandleWax", sides=4, center=center)
    body.lathe([(0.0007, wax_top), (0.0007, wax_top + 0.006)], "CandleWick", sides=4)
    root = body.finish(name)
    cfg = SPEC["flame"]
    flame = Geometry()
    flame.lathe([(cfg["radius"] * 0.3, wax_top + 0.004),
                 (cfg["radius"], wax_top + cfg["height"] * 0.35),
                 (0.0001, wax_top + cfg["height"])], "CandleFlame", sides=cfg["sides"])
    flame.finish("Flame", root)
    triangles = sum(len(p.vertices) - 2 for obj in [root, *root.children] for p in obj.data.polygons)
    if triangles > 300:
        raise ValueError(f"[Tiffany] {name} excede el presupuesto: {triangles} triangulos")
    print(f"[Tiffany] {name}: {triangles} triangulos, {height:.3f} m de cuerpo")
    return root


def make_materials():
    for entry in SPEC["materials"]:
        h = entry["color"].lstrip("#")
        rgb = [linear(int(h[i:i + 2], 16) / 255) for i in (0, 2, 4)]
        alpha = int(h[6:8], 16) / 255 if len(h) == 8 else 1
        mat = bpy.data.materials.get(entry["name"]) or bpy.data.materials.new(entry["name"])
        mat.diffuse_color = (*rgb, alpha)
        if entry.get("unlit", False):
            mat.node_tree.nodes.clear()
            emission = mat.node_tree.nodes.new("ShaderNodeEmission")
            emission.inputs["Color"].default_value = (*rgb, 1)
            output = mat.node_tree.nodes.new("ShaderNodeOutputMaterial")
            mat.node_tree.links.new(emission.outputs[0], output.inputs["Surface"])
        else:
            bsdf = mat.node_tree.nodes["Principled BSDF"]
            bsdf.inputs["Base Color"].default_value = (*rgb, alpha)
            bsdf.inputs["Alpha"].default_value = alpha
            bsdf.inputs["Roughness"].default_value = 1 - entry["smoothness"]
        if entry.get("surface") == "transparent":
            mat.surface_render_method = "BLENDED"


def add(name, data, position, target=None):
    obj = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = to_blender(position)
    if target is not None:
        obj.rotation_euler = (to_blender(target) - obj.location).to_track_quat("-Z", "Y").to_euler()
    return obj


def render(folder, candles):
    os.makedirs(folder, exist_ok=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x, scene.render.resolution_y = 640, 800
    scene.view_settings.view_transform = "Standard"
    scene.world = bpy.data.worlds.new("Night")
    scene.world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.003, 0.003, 0.005, 1)
    ground = bpy.data.meshes.new("Ground")
    ground.from_pydata([(-1, -1, 0), (1, -1, 0), (1, 1, 0), (-1, 1, 0)], [], [(0, 1, 2, 3)])
    ground_mat = bpy.data.materials.new("Ground")
    ground_mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.015, 0.012, 0.009, 1)
    ground.materials.append(ground_mat)
    add("Ground", ground, (0, -0.001, 0))
    warm = bpy.data.lights.new("Warm", "AREA")
    warm.energy, warm.color, warm.shape, warm.size = 12, (1.0, 0.72, 0.42), "DISK", 0.4
    add("Warm", warm, (0.3, 0.4, 0.4), (0, 0.1, 0))
    moon = bpy.data.lights.new("Moon", "SUN")
    moon.energy, moon.color = 0.35, (0.55, 0.65, 1.0)
    add("Moon", moon, (-3, 8, -4), (0, 0, 0))
    cam = add("Camera", bpy.data.cameras.new("Camera"), (0, 0, 0))
    cam.data.angle, cam.data.clip_start = math.radians(24), 0.01
    scene.camera = cam
    views = {"front": (0, 0.06, 0.75), "threequarter": (0.45, 0.38, 0.5), "profile": (0.75, 0.06, 0)}
    for candle in candles:
        height = SPEC["family" if candle.name == "CandleFamily" else "kindled"]["height"]
        target = Vector((0, height * 0.5, 0))
        for other in candles:
            for obj in [other, *other.children]:
                obj.hide_render = other is not candle
        for view, offset in views.items():
            cam.location = to_blender(target + Vector(offset) * (height / SPEC["family"]["height"]))
            cam.rotation_euler = (to_blender(target) - cam.location).to_track_quat("-Z", "Y").to_euler()
            scene.render.filepath = os.path.join(folder, f"{candle.name}_{view}.png")
            bpy.ops.render.render(write_still=True)
    for candle in candles:
        for obj in [candle, *candle.children]:
            obj.hide_render = False


def export(candles):
    from io_scene_fbx.export_fbx_bin import fbx_utils
    original_name = fbx_utils.get_blenderID_name
    flame_names = {child.name for candle in candles for child in candle.children}
    # Blender exige nombres globales únicos, el FBX admite Flame bajo cada padre.
    fbx_utils.get_blenderID_name = lambda obj: "Flame" if original_name(obj) in flame_names else original_name(obj)
    try:
        os.makedirs(os.path.dirname(FBX), exist_ok=True)
        bpy.ops.export_scene.fbx(filepath=os.path.abspath(FBX), object_types={"MESH"}, axis_forward="-Z", axis_up="Y",
                                 apply_scale_options="FBX_SCALE_ALL", bake_space_transform=True, mesh_smooth_type="FACE",
                                 add_leaf_bones=False, bake_anim=False, use_custom_props=False)
    finally:
        fbx_utils.get_blenderID_name = original_name
    print(f"[Tiffany] Exportado {os.path.abspath(FBX)}")


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj)
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1
    make_materials()
    candles = [build(name) for name in ("CandleFamily", "CandleKindled")]
    bpy.context.view_layer.update()
    if "--no-export" not in args:
        export(candles)
    if "--render" in args:
        render(args[args.index("--render") + 1], candles)
    if not bpy.app.background:
        candles[1].location = to_blender((SPEC["preview"]["kindledOffsetX"], 0, 0))
        for area in (a for screen in bpy.data.screens for a in screen.areas if a.type == "VIEW_3D"):
            area.spaces.active.shading.color_type = "MATERIAL"


main()
