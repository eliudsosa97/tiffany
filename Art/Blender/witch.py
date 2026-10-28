import json, math, os, random, sys
import bpy, bmesh
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
SPEC = json.load(open(os.path.join(HERE, "witch.json")))
FBX = os.path.join(HERE, "..", "..", "Assets", "Art", "Models", "witch.fbx")
UP = Vector((0, 1, 0))
MAT = {m["name"]: i for i, m in enumerate(SPEC["materials"])}
SKIN, CLOTH, HAIR = MAT["WitchSkin"], MAT["WitchCloth"], MAT["WitchHair"]


def to_blender(p):
    # El json usa ejes de Unity (X derecha de ella, Y arriba, Z al frente); Blender la ve de frente en -Y.
    return Vector((-p.x, -p.z, p.y))


def linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def horizontal(v, fallback):
    h = Vector((v.x, 0, v.z))
    return h.normalized() if h.length > 1e-3 else fallback


class Pose:
    def __init__(self, name):
        self.name, self.p, self.b = name, SPEC["poses"][name], SPEC["body"]
        self.rng = random.Random(SPEC["seed"])
        self.bm = bmesh.new()

    def j(self, key):
        return Vector(self.p[key])

    def tube(self, points, sections, ref, mat, sides=6):
        rings = []
        for k, p in enumerate(points):
            t = (points[min(k + 1, len(points) - 1)] - points[max(k - 1, 0)]).normalized()
            side = ref - t * ref.dot(t)
            side = side.normalized() if side.length > 1e-4 else t.orthogonal().normalized()
            normal = t.cross(side)
            a, b = sections[k]
            rings.append([self.bm.verts.new(to_blender(
                p + side * (a * math.cos(2 * math.pi * (i + 0.5) / sides)) +
                normal * (b * math.sin(2 * math.pi * (i + 0.5) / sides)))) for i in range(sides)])
        faces = [self.bm.faces.new(rings[0]), self.bm.faces.new(rings[-1][::-1])]
        for r0, r1 in zip(rings, rings[1:]):
            faces += [self.bm.faces.new((r0[i], r0[(i + 1) % sides], r1[(i + 1) % sides], r1[i])) for i in range(sides)]
        for f in faces:
            f.material_index = mat

    def chain(self, joints, radii, mat, ref=Vector((1, 0, 0)), sides=6):
        points, sections = [joints[0]], [(radii[0], radii[0])]
        for k in range(1, len(joints)):
            r = (radii[k - 1] + radii[k]) * 0.5 * self.b["boneThinning"]
            points += [(joints[k - 1] + joints[k]) * 0.5, joints[k]]
            sections += [(r, r), (radii[k], radii[k])]
        self.tube(points, sections, ref, mat, sides)

    def ellipsoid(self, center, side, up, fwd, radii, mat):
        verts = bmesh.ops.create_icosphere(self.bm, subdivisions=2, radius=1.0)["verts"]
        for v in verts:
            c = v.co.copy()
            v.co = to_blender(center + side * (c.x * radii[0]) + up * (c.z * radii[1]) + fwd * (c.y * radii[2]))
        for f in {f for v in verts for f in v.link_faces}:
            f.material_index = mat

    def hang(self, anchor, out, length, width, thickness, clear):
        tangent = UP.cross(out)
        rings = 6
        points = [anchor, anchor + out * clear - UP * 0.05]
        for k in range(1, rings):
            p = points[1] - UP * (length * k / rings) + out * (0.03 * k / rings)
            p += tangent * self.rng.uniform(-0.02, 0.02)
            p.y = max(p.y, 0.02)
            points.append(p)
        sections = [(width * 0.5 * (1 - 0.85 * k / len(points)), thickness * 0.5) for k in range(len(points))]
        return points, sections, tangent

    def strip(self, anchor, out, length, cfg, mat, clear):
        points, sections, tangent = self.hang(anchor, out, length, cfg["width"], cfg["thickness"], clear)
        self.tube(points, sections, tangent, mat, sides=4)

    def build(self):
        b, j = self.b, self.j
        s = b["armLengthScale"]
        for side, sign in (("L", -1), ("R", 1)):
            sh = j("shoulder" + side)
            arm = [sh] + [sh + (j(k + side) - sh) * s for k in ("elbow", "wrist", "hand")]
            self.chain(arm[:3], b["armRadius"], SKIN)
            self.tube(arm[2:], [(b["armRadius"][2], b["armRadius"][2] * 0.6), (0.04, 0.014)], Vector((1, 0, 0)), SKIN)
            self.fingers(arm[3], Vector(self.p["fingerDir" + side]).normalized(), self.p["fingerCurl" + side])
            self.chain([j("hip" + side), j("knee" + side), j("ankle" + side)], b["legRadius"], SKIN)
            fr = b["footRadius"]
            self.tube([j("ankle" + side), j("toe" + side)], [(fr[0], fr[0]), (fr[1] * 1.4, fr[1])], UP.cross(Vector((0, 0, 1))), SKIN)
        spine = [j(k) for k in ("pelvis", "waist", "chest", "neck")]
        self.tube(spine, list(zip(b["torsoHalfWidth"], b["torsoHalfDepth"])), Vector((1, 0, 0)), CLOTH, sides=8)
        shoulder_line = j("shoulderR") - j("shoulderL")
        mid = (j("shoulderL") + j("shoulderR")) * 0.5
        rebozo = [spine[1].lerp(spine[2], 0.5), spine[2], mid, spine[3] + (spine[3] - spine[2]).normalized() * 0.06]
        self.tube(rebozo, list(zip(b["rebozoHalfWidth"], b["rebozoHalfDepth"])), shoulder_line, CLOTH, sides=8)
        self.head()
        rags = SPEC["rebozoRags"]
        self.rag_ring(rebozo[0], rebozo[1] - rebozo[0], shoulder_line, b["rebozoHalfWidth"][0], b["rebozoHalfDepth"][0], rags["chestStrips"], rags["chestLength"], rags)
        self.rag_ring(spine[0], spine[1] - spine[0], Vector((1, 0, 0)), b["torsoHalfWidth"][0] + 0.01, b["torsoHalfDepth"][0] + 0.01, rags["hipStrips"], rags["hipLength"], rags)
        return self.finish()

    def fingers(self, hand, direction, curl):
        b = self.b
        ref = UP if abs(direction.y) < 0.7 else Vector((1, 0, 0))
        spread = direction.cross(ref).normalized()
        palm = direction.cross(spread)
        for i in range(4):
            o = i - 1.5
            base = hand + spread * (o * b["fingerSpread"])
            tip = base + direction * b["fingerLength"] + spread * (o * b["fingerFan"])
            tip.y = max(tip.y, 0.015)
            joints = [base, base.lerp(tip, 0.4) + palm * curl, base.lerp(tip, 0.75) + palm * curl * 0.8, tip]
            r0, r1 = b["fingerRadius"]
            self.tube(joints, [(r, r) for r in (r0, r0 * 0.7, r0 * 0.8, r1)], spread, SKIN, sides=4)

    def head(self):
        rot = Matrix.Rotation(math.radians(self.p["headRollDeg"]), 3, "Z") @ Matrix.Rotation(math.radians(self.p["headPitchDeg"]), 3, "X")
        up, fwd = rot @ UP, rot @ Vector((0, 0, 1))
        side = up.cross(fwd)
        c, r = self.j("head"), self.b["headRadius"]
        self.ellipsoid(c, side, up, fwd, r, SKIN)
        self.tube([self.j("neck"), c - up * r[1] * 0.85], [(self.b["neckRadius"],) * 2] * 2, Vector((1, 0, 0)), SKIN)
        self.ellipsoid(c + up * 0.03 - fwd * 0.025, side, up, fwd, [x * 1.06 for x in r], HAIR)
        cfg = SPEC["hair"]
        for i in range(cfg["strands"]):
            theta = 2 * math.pi * i / cfg["strands"] + self.rng.uniform(-0.12, 0.12)
            el = self.rng.uniform(0.2, 1.0)
            d = side * (math.cos(el) * math.sin(theta) * r[0]) + fwd * (math.cos(el) * math.cos(theta) * r[2]) + up * (math.sin(el) * r[1])
            front = math.cos(theta) > 0.3
            length = self.rng.uniform(*cfg["frontLength" if front else "backLength"])
            self.strip(c + d * 1.05, horizontal(d, fwd), length, cfg, HAIR, 0.08)

    def rag_ring(self, center, axis, ref, a, b, count, length, cfg):
        t = axis.normalized()
        side = (ref - t * ref.dot(t)).normalized()
        normal = t.cross(side)
        for i in range(count):
            phi = 2 * math.pi * (i + self.rng.uniform(-0.3, 0.3)) / count
            anchor = center + side * (a * math.cos(phi)) + normal * (b * math.sin(phi))
            self.strip(anchor, horizontal(anchor - center, side), self.rng.uniform(*length), cfg, CLOTH, 0.03)

    def finish(self):
        bm = self.bm
        feet = to_blender((self.j("ankleL") + self.j("ankleR")) * 0.5)
        low = min(v.co.z for v in bm.verts)
        for v in bm.verts:
            v.co -= Vector((feet.x, feet.y, low))
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        mesh = bpy.data.meshes.new(self.name)
        bm.to_mesh(mesh)
        bm.free()
        for m in SPEC["materials"]:
            mesh.materials.append(bpy.data.materials[m["name"]])
        obj = bpy.data.objects.new(self.name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        tris = sum(len(p.vertices) - 2 for p in mesh.polygons)
        zs = [v.co.z for v in mesh.vertices]
        print(f"[Tiffany] {self.name}: {tris} triangulos, {max(zs) - min(zs):.3f} m de alto")
        return obj


def make_materials():
    for m in SPEC["materials"]:
        h = m["color"].lstrip("#")
        rgb = [linear(int(h[i:i + 2], 16) / 255) for i in (0, 2, 4)]
        mat = bpy.data.materials.new(m["name"])
        mat.diffuse_color = (*rgb, 1)
        bsdf = mat.node_tree.nodes["Principled BSDF"]
        bsdf.inputs["Base Color"].default_value = (*rgb, 1)
        bsdf.inputs["Roughness"].default_value = 1 - m["smoothness"]


def add(name, data, pos, target=None):
    obj = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = to_blender(pos)
    if target is not None:
        obj.rotation_euler = (to_blender(target) - obj.location).to_track_quat("-Z", "Y").to_euler()
    return obj


def render(folder, poses):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x, scene.render.resolution_y = 640, 800
    scene.view_settings.view_transform = "Standard"
    scene.world = bpy.data.worlds.new("Night")
    scene.world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.003, 0.003, 0.005, 1)
    ground = bpy.data.meshes.new("Ground")
    ground.from_pydata([(-6, -6, 0), (6, -6, 0), (6, 6, 0), (-6, 6, 0)], [], [(0, 1, 2, 3)])
    gmat = bpy.data.materials.new("Ground")
    gmat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.008, 0.008, 0.008, 1)
    ground.materials.append(gmat)
    add("Ground", ground, Vector((0, -0.001, 0)))
    warm = bpy.data.lights.new("Warm", "POINT")
    warm.energy, warm.color, warm.shadow_soft_size = 260, (1.0, 0.72, 0.42), 0.3
    add("Warm", warm, Vector((1.2, 0.8, 3.6)))
    moon = bpy.data.lights.new("Moon", "SUN")
    moon.energy, moon.color = 0.35, (0.55, 0.65, 1.0)
    add("Moon", moon, Vector((-3, 8, -4)), Vector((0, 0, 0)))
    cam = add("Camera", bpy.data.cameras.new("Camera"), Vector((0, 0, 0)))
    cam.data.angle = math.radians(24)
    scene.camera = cam
    el, az = math.radians(42), math.radians(30)
    views = {"front": Vector((0, 0.25, 7)), "threequarter": Vector((math.sin(az) * math.cos(el), math.sin(el), math.cos(az) * math.cos(el))) * 8, "profile": Vector((7, 0.25, 0))}
    for obj in poses:
        target = Vector((0, 1.1 if obj.name == "WitchStand" else 0.65, 0.15))
        for other in poses:
            other.hide_render = other is not obj
        for view, offset in views.items():
            cam.location = to_blender(target + offset)
            cam.rotation_euler = (to_blender(target) - cam.location).to_track_quat("-Z", "Y").to_euler()
            scene.render.filepath = os.path.join(folder, f"{obj.name}_{view}.png")
            bpy.ops.render.render(write_still=True)


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj)
    make_materials()
    poses = [Pose(name).build() for name in ("WitchStand", "WitchCrouch")]
    if "--no-export" not in args:
        os.makedirs(os.path.dirname(FBX), exist_ok=True)
        bpy.ops.export_scene.fbx(filepath=os.path.abspath(FBX), object_types={"MESH"}, axis_forward="-Z", axis_up="Y",
                                 apply_scale_options="FBX_SCALE_ALL", bake_space_transform=True, mesh_smooth_type="FACE",
                                 add_leaf_bones=False, bake_anim=False, use_custom_props=False)
        print(f"[Tiffany] Exportado {os.path.abspath(FBX)}")
    if "--render" in args:
        render(args[args.index("--render") + 1], poses)
    if not bpy.app.background:
        poses[1].location = to_blender(Vector((SPEC["preview"]["crouchOffsetX"], 0, 0)))
        for area in (a for s in bpy.data.screens for a in s.areas if a.type == "VIEW_3D"):
            area.spaces.active.shading.color_type = "MATERIAL"


main()
