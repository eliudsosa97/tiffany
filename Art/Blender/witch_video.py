"""Videos y hojas de cuadros de los clips: fondo casi negro, luz cálida baja y luna azul tenue."""

import glob
import math
import os
import bpy
import numpy as np
from mathutils import Vector
from witch_rig import V
from witch_motion import FPS

SECONDS = 4
# La cámara del juego mira con 42° de picado.
VIEWS = {"side": (90, 8, 7.5, 30), "top": (35, 42, 10.0, 30)}
TRACK = (70, 18, 7.0, 32)


def material(name, rgb):
    mat = bpy.data.materials.new(name)
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*rgb, 1)
    bsdf.inputs["Roughness"].default_value = 0.9
    return mat


def box(name, center, size, mat, parent=None):
    me = bpy.data.meshes.new(name)
    hx, hy, hz = (s / 2 for s in size)
    vs = [(center.x + sx * hx, center.y + sy * hy, center.z + sz * hz) for sx in (-1, 1) for sy in (-1, 1) for sz in (-1, 1)]
    me.from_pydata(vs, [], [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)])
    me.materials.append(mat)
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    ob.parent = parent
    return ob


def stage(marks, keep):
    scene = bpy.context.scene
    for ob in list(scene.objects):
        if ob not in keep:
            bpy.data.objects.remove(ob)
    scene.render.engine = "BLENDER_EEVEE"
    scene.eevee.taa_render_samples = 16
    scene.view_settings.view_transform = "Standard"
    scene.render.fps = FPS
    scene.render.fps_base = 1.0
    scene.render.resolution_percentage = 100
    scene.world = bpy.data.worlds.new("Night")
    scene.world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.004, 0.004, 0.006, 1)
    box("Ground", Vector((0, -60, -0.05)), (40, 200, 0.1), material("GroundDark", (0.012, 0.012, 0.012)))
    if marks:
        stripe = material("GroundMark", (0.06, 0.055, 0.05))
        for k in range(-30, 400):
            box(f"Mark{k}", Vector((0, -k * 0.5, 0.0005)), (3.0 if k % 2 == 0 else 1.2, 0.05, 0.002), stripe)
    rig = Vector()
    pivot = bpy.data.objects.new("Pivot", None)
    scene.collection.objects.link(pivot)
    mover = bpy.data.objects.new("Mover", None)
    scene.collection.objects.link(mover)
    pivot.parent = mover
    warm = bpy.data.lights.new("Warm", "POINT")
    warm.energy, warm.color, warm.shadow_soft_size = 500, (1.0, 0.66, 0.36), 0.25
    lamp = bpy.data.objects.new("Warm", warm)
    scene.collection.objects.link(lamp)
    lamp.parent = pivot
    lamp.location = rig + V(2.2, 0.7, 2.2)
    moon = bpy.data.lights.new("Moon", "SUN")
    moon.energy, moon.color = 1.4, (0.5, 0.62, 1.0)
    sun = bpy.data.objects.new("Moon", moon)
    scene.collection.objects.link(sun)
    sun.rotation_euler = (math.radians(55), 0, math.radians(200))
    cam = bpy.data.objects.new("Camera", bpy.data.cameras.new("Camera"))
    scene.collection.objects.link(cam)
    cam.parent = pivot
    scene.camera = cam
    return mover, cam


def aim(cam, view, target):
    az, el, dist, fov = TRACK if view == "track" else VIEWS[view]
    a, e = math.radians(az), math.radians(el)
    cam.data.angle = math.radians(fov)
    cam.location = target + V(math.sin(a) * math.cos(e), math.sin(e), math.cos(a) * math.cos(e)) * dist
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()


def play(arm, act, start=1, repeat=1.0):
    ad = arm.animation_data_create()
    ad.action = None
    for tr in ad.nla_tracks:
        tr.mute = True
    tr = ad.nla_tracks.new()
    tr.name = "Video"
    st = tr.strips.new(act.name, start, act)
    if hasattr(st, "action_slot") and st.action_slot is None and len(act.slots):
        st.action_slot = act.slots[0]
    st.repeat = repeat
    st.extrapolation = "HOLD"
    return tr


def move(ob, speed, start, end):
    ob.animation_data_clear()
    ob.location = Vector()
    if speed == 0:
        return
    for f in (start, end):
        ob.location = Vector((0, -speed * (f - start) / FPS, 0))
        ob.keyframe_insert("location", frame=f)
    fc = ob.animation_data.action.fcurve_ensure_for_datablock(ob, "location", index=1)
    for k in fc.keyframe_points:
        k.interpolation = "LINEAR"


def encode(scene, path):
    r = scene.render
    r.image_settings.media_type = "VIDEO"
    r.image_settings.file_format = "FFMPEG"
    r.ffmpeg.format = "MPEG4"
    r.ffmpeg.codec = "H264"
    r.ffmpeg.constant_rate_factor = "HIGH"
    base = path[:-4]
    r.filepath = base + "_"
    bpy.ops.render.render(animation=True)
    made = sorted(glob.glob(base + "_*.mp4"), key=os.path.getmtime)
    os.replace(made[-1], path)
    print(f"[Tiffany] Video {path}")


def videos(folder, arm, actions, speeds, res=(1280, 720)):
    os.makedirs(folder, exist_ok=True)
    scene = bpy.context.scene
    mover, cam = stage(True, [arm] + list(arm.children))
    arm.parent = mover
    scene.render.resolution_x, scene.render.resolution_y = res
    for act in actions:
        length = act.frame_end - act.frame_start
        total = max(FPS * SECONDS, math.ceil(length) + 1)
        scene.frame_start, scene.frame_end = 1, total
        tr = play(arm, act, 1, math.ceil(total / length) + 1 if act.use_cyclic else 1)
        shots = [(v, False) for v in VIEWS] + [("track", True)]
        for view, moving in shots:
            move(mover, speeds[act.name] if moving else 0.0, 1, total)
            aim(cam, view, V(0, 0.7 if view == "track" else 0.9, 0.2))
            encode(scene, os.path.join(folder, f"{act.name}_{view}.mp4"))
        arm.animation_data.nla_tracks.remove(tr)
    move(mover, 0.0, 1, total)
    arm.parent = None


def stills(folder, arm, actions, counts, res=(400, 400), cols=6):
    """Hojas con cuadros del clip, una por vista, para revisar poses sin video."""
    os.makedirs(folder, exist_ok=True)
    scene = bpy.context.scene
    mover, cam = stage(False, [arm] + list(arm.children))
    scene.render.resolution_x, scene.render.resolution_y = res
    scene.render.image_settings.media_type = "IMAGE"
    scene.render.image_settings.file_format = "PNG"
    for act in actions:
        n = counts.get(act.name, 8)
        length = act.frame_end - act.frame_start
        tr = play(arm, act, 0, 1)
        for view in VIEWS:
            aim(cam, view, V(0, 0.9, 0.25))
            cam.location = V(0, 0.9, 0.25) + (cam.location - V(0, 0.9, 0.25)) * 0.72
            tiles = []
            for i in range(n):
                f = length * i / (n if act.use_cyclic else n - 1)
                scene.frame_set(int(f), subframe=f - int(f))
                path = os.path.join(folder, "_tile.png")
                scene.render.filepath = path
                bpy.ops.render.render(write_still=True)
                im = bpy.data.images.load(path)
                tiles.append(np.array(im.pixels[:], dtype=np.float32).reshape(im.size[1], im.size[0], 4))
                bpy.data.images.remove(im)
            while len(tiles) % cols:
                tiles.append(np.zeros_like(tiles[0]))
            rows = [np.concatenate(tiles[r:r + cols], axis=1) for r in range(0, len(tiles), cols)]
            sheet = np.concatenate(rows[::-1], axis=0)
            out = bpy.data.images.new(f"sheet_{act.name}_{view}", sheet.shape[1], sheet.shape[0], alpha=False)
            out.pixels.foreach_set(sheet.ravel())
            out.filepath_raw = os.path.join(folder, f"{act.name}_{view}.png")
            out.file_format = "PNG"
            out.save()
            print(f"[Tiffany] Hoja {out.filepath_raw}")
            bpy.data.images.remove(out)
        arm.animation_data.nla_tracks.remove(tr)
