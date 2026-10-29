"""Video y hojas de cuadros de cada clip: vista de lado y cámara del juego a 42°, de noche y con la luz en sus manos."""

import glob
import os
import shutil
from math import ceil, cos, radians, sin, tan

import bpy
import numpy as np
from mathutils import Vector

from tiffany_motion import CLIPS, FPS, LOG_PREFIX, U

VIDEO_FPS = 30
SECONDS = 4.0
VIEW = (640, 720)
SHEET_TILES = 8
# Los clips de brazos se ven sobre la pose base que tendrían en el juego.
BASE = {"CarryLight": "Idle", "Flash": "Idle"}


def material(name, rgb, emission=0.0):
    mat = bpy.data.materials.new(name)
    if hasattr(mat, "use_nodes") and not mat.use_nodes:
        mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*rgb, 1)
    bsdf.inputs["Roughness"].default_value = 0.95
    if emission:
        bsdf.inputs["Emission Color"].default_value = (*rgb, 1)
        bsdf.inputs["Emission Strength"].default_value = emission
    return mat


def box(name, center, size, mat, made):
    mesh = bpy.data.meshes.new(name)
    hx, hy, hz = (s / 2 for s in size)
    vs = [(center.x + sx * hx, center.y + sy * hy, center.z + sz * hz) for sx in (-1, 1) for sy in (-1, 1) for sz in (-1, 1)]
    mesh.from_pydata(vs, [], [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)])
    mesh.materials.append(mat)
    return link(name, mesh, made)


def link(name, data, made, parent=None):
    obj = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(obj)
    obj.parent = parent
    made.append(obj)
    return obj


def aim(camera, target):
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()


class Stage:
    def __init__(self, arm):
        scene = bpy.context.scene
        self.scene, self.arm, self.made = scene, arm, []
        self.hidden = [obj for obj in scene.objects if obj is not arm and obj.parent is not arm and not obj.hide_render]
        for obj in self.hidden:
            obj.hide_render = True
        scene.render.engine = "BLENDER_EEVEE"
        scene.eevee.taa_render_samples = 16
        scene.view_settings.view_transform = "Standard"
        scene.render.film_transparent = False
        self.world = scene.world
        scene.world = bpy.data.worlds.new("Night")
        if hasattr(scene.world, "use_nodes") and not scene.world.use_nodes:
            scene.world.use_nodes = True
        scene.world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.004, 0.005, 0.009, 1)
        made = self.made
        box("Ground", Vector((0, 10, -0.05)), (40, 80, 0.1), material("NightGround", (0.022, 0.021, 0.02)), made)
        stripe = material("NightMark", (0.07, 0.065, 0.058))
        for k in range(-20, 61):
            box(f"Mark{k}", Vector((0, k * 0.5, 0.0005)), (1.2 if k % 2 else 2.4, 0.03, 0.002), stripe, made)
        moon = bpy.data.lights.new("Moon", "SUN")
        moon.energy, moon.color, moon.angle = 2.2, (0.55, 0.65, 1.0), radians(2)
        link("Moon", moon, made).rotation_euler = (radians(50), 0, radians(210))
        for side in "LR":
            warm = bpy.data.lights.new("Warm" + side, "POINT")
            warm.energy, warm.color, warm.shadow_soft_size = 3.5, (1.0, 0.72, 0.42), 0.02
            lamp = link("Warm" + side, warm, made, arm)
            lamp.parent_type, lamp.parent_bone = "BONE", "Hand." + side
        self.follow = link("Follow", None, made)
        self.mover = link("Mover", None, made, self.follow)
        self.parent = arm.parent
        arm.parent = self.mover
        self.cameras = {}
        focus = U((0, 0.62, 0))
        side = link("SideCamera", bpy.data.cameras.new("SideCamera"), made, self.follow)
        side.location = focus + U((3.4, 0, 0))
        aim(side, focus)
        pitch, yaw = radians(42), radians(45)
        ahead = U((sin(yaw) * cos(pitch), -sin(pitch), cos(yaw) * cos(pitch)))
        game = link("GameCamera", bpy.data.cameras.new("GameCamera"), made, self.follow)
        game.location = U((0, 0.6, 0)) - ahead * 5.5
        aim(game, U((0, 0.6, 0)))
        for camera, fov in ((side, 26), (game, 24)):
            camera.data.sensor_fit, camera.data.angle = "VERTICAL", radians(fov)
        self.cameras = {"side": side, "game": game}
        label = bpy.data.curves.new("Label", "FONT")
        label.size = 0.045
        label.materials.append(material("LabelInk", (0.9, 0.86, 0.78), 2.0))
        self.label = link("Label", label, made, side)
        half = 1.0 * tan(radians(13))
        self.label.location = (-half * VIEW[0] / VIEW[1] + 0.03, half - 0.07, -1.0)
        self.tracks = list(arm.animation_data.nla_tracks)

    def play(self, name):
        animation = self.arm.animation_data
        for track in self.tracks:
            track.mute = True
        self.temp = []
        for clip in ([BASE[name]] if name in BASE else []) + [name]:
            action = bpy.data.actions[clip]
            frames = CLIPS[clip]["frames"]
            track = animation.nla_tracks.new()
            strip = track.strips.new(clip, 1, action)
            if hasattr(strip, "action_slot") and strip.action_slot is None and len(action.slots):
                strip.action_slot = action.slots[0]
            strip.repeat = ceil(SECONDS * FPS / frames) + 1 if CLIPS[clip]["loop"] or clip == "Flash" else 1
            strip.extrapolation = "HOLD"
            self.temp.append(track)
        self.label.data.body = name

    def stop(self):
        for track in self.temp:
            self.arm.animation_data.nla_tracks.remove(track)
        for track in self.tracks:
            track.mute = False

    def place(self, name, t):
        clip = CLIPS[name]
        self.follow.location = Vector((0, clip.get("speed", 0.0) * t, 0))
        sign = 1 if name == "TurnLeft" else -1 if name == "TurnRight" else 0
        self.mover.rotation_euler = (0, 0, sign * radians(clip.get("turnSpeed", 0.0)) * t)
        f = 1 + t * FPS
        self.scene.frame_set(int(f), subframe=f - int(f))

    def shot(self, view, path):
        self.scene.camera = self.cameras[view]
        self.label.hide_render = view != "side"
        self.scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        image = bpy.data.images.load(path)
        pixels = np.array(image.pixels[:], dtype=np.float32).reshape(image.size[1], image.size[0], 4)
        bpy.data.images.remove(image)
        return pixels

    def pair(self, path, percentage=100):
        scene = self.scene
        scene.render.resolution_x, scene.render.resolution_y = VIEW
        scene.render.resolution_percentage = percentage
        scene.render.image_settings.media_type = "IMAGE"
        scene.render.image_settings.file_format = "PNG"
        return np.concatenate([self.shot(view, path) for view in ("side", "game")], axis=1)

    def close(self):
        self.arm.parent = self.parent
        for obj in self.made:
            bpy.data.objects.remove(obj, do_unlink=True)
        for obj in self.hidden:
            obj.hide_render = False
        self.scene.world = self.world


def save(pixels, path):
    image = bpy.data.images.new(os.path.basename(path), pixels.shape[1], pixels.shape[0], alpha=False)
    image.pixels.foreach_set(pixels.ravel())
    image.filepath_raw, image.file_format = path, "PNG"
    image.save()
    bpy.data.images.remove(image)


def encode(folder, files, path):
    edit = bpy.data.scenes.new("TiffanyEdit")
    edit.render.resolution_x, edit.render.resolution_y = VIEW[0] * 2, VIEW[1]
    edit.render.resolution_percentage = 100
    edit.render.fps, edit.render.fps_base = VIDEO_FPS, 1.0
    strip = edit.sequence_editor_create().strips.new_image("Frames", os.path.join(folder, files[0]), 1, 1)
    for name in files[1:]:
        strip.elements.append(name)
    edit.frame_start, edit.frame_end = 1, len(files)
    settings = edit.render.image_settings
    settings.media_type = "VIDEO"
    settings.file_format = "FFMPEG"
    edit.render.ffmpeg.format = "MPEG4"
    edit.render.ffmpeg.codec = "H264"
    edit.render.ffmpeg.constant_rate_factor = "HIGH"
    base = path[:-4]
    edit.render.filepath = base + "_"
    bpy.ops.render.render(animation=True, scene=edit.name)
    made = sorted(glob.glob(base + "_*.mp4"), key=os.path.getmtime)
    os.replace(made[-1], path)
    bpy.data.scenes.remove(edit)
    print(f"{LOG_PREFIX} Video {path}")


def sheets(folder, arm, stage=None):
    os.makedirs(folder, exist_ok=True)
    own = stage is None
    stage = stage or Stage(arm)
    tile = os.path.join(folder, "_tile.png")
    for name, clip in CLIPS.items():
        stage.play(name)
        period = clip["frames"] / FPS
        count = SHEET_TILES
        tiles = []
        for i in range(count):
            stage.place(name, period * i / (count if clip["loop"] else count - 1))
            tiles.append(stage.pair(tile, 50))
        stage.stop()
        sides = [t[:, :t.shape[1] // 2] for t in tiles]
        games = [t[:, t.shape[1] // 2:] for t in tiles]
        sheet = np.concatenate([np.concatenate(games, axis=1), np.concatenate(sides, axis=1)], axis=0)
        save(sheet, os.path.join(folder, f"sheet_{name}.png"))
        print(f"{LOG_PREFIX} Hoja {os.path.join(folder, f'sheet_{name}.png')}")
    os.remove(tile)
    if own:
        stage.close()


def videos(folder, arm):
    os.makedirs(folder, exist_ok=True)
    frames = os.path.join(folder, "frames")
    shutil.rmtree(frames, ignore_errors=True)
    os.makedirs(frames)
    stage = Stage(arm)
    tile = os.path.join(folder, "_tile.png")
    every, index = [], 0
    for name in CLIPS:
        stage.play(name)
        files = []
        for i in range(int(SECONDS * VIDEO_FPS)):
            stage.place(name, i / VIDEO_FPS)
            index += 1
            files.append(f"{index:05d}.png")
            save(stage.pair(tile), os.path.join(frames, files[-1]))
        stage.stop()
        encode(frames, files, os.path.join(folder, f"{name}.mp4"))
        every += files
    encode(frames, every, os.path.join(folder, "tiffany_clips.mp4"))
    os.remove(tile)
    sheets(folder, arm, stage)
    stage.close()
