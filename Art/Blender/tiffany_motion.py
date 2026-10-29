"""Clips de Tiffany: marcha por IK, gestos por curvas, y trenzas y falda simuladas al hornear."""

import json
import os
from math import asin, atan2, ceil, cos, degrees, hypot, pi, radians, sin, sqrt

import bpy
import numpy as np
from mathutils import Matrix, Quaternion, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
with open(os.path.join(HERE, "tiffany-provisional.json"), encoding="utf-8") as source:
    SPEC = json.load(source)
SAMPLES_PER_FRAME = 4
FPS = SPEC["fps"] * SAMPLES_PER_FRAME
CLIPS = {clip["name"]: dict(clip, frames=clip["frames"] * SAMPLES_PER_FRAME, key=clip["key"] * SAMPLES_PER_FRAME)
         for clip in SPEC["clips"]}
STRIP = {name: 1 + sum(c["frames"] + 10 * SAMPLES_PER_FRAME for c in list(CLIPS.values())[:i]) for i, name in enumerate(CLIPS)}
ARMS = set(SPEC["controller"]["layers"][1]["mask"]["bones"])
BONES = {bone["name"]: bone for bone in SPEC["skeleton"]}
SKIRT = ("Skirt.F", "Skirt.R", "Skirt.B", "Skirt.L")
LOG_PREFIX = "[Tiffany]"
RIGHT, FWD, UP = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
SIDES = (("L", -1), ("R", 1))
SUB = 2
GRID = 512
FOOT_FROM_BALL = Vector((0, -0.07, 0.05))
SOLE = (Vector((0, -0.045, -0.05)), Vector((0, 0.07, -0.05)), Vector((0, 0.11, -0.045)))
SIM = SPEC["secondary"]


def U(point):
    # El FBX conserva Z arriba y Unity convierte +Y de Blender en su frente +Z.
    return Vector((point[0], point[2], point[1]))


def clamp(value, low=0.0, high=1.0):
    return max(low, min(high, value))


def mix(a, b, t):
    return a * (1 - t) + b * t


def smooth(a, b, x):
    t = clamp((x - a) / (b - a))
    return t * t * (3 - 2 * t)


def bump(s, peak=0.5):
    if s <= 0 or s >= 1:
        return 0.0
    a = 2 * peak / (1 - peak)
    return (s ** a * (1 - s) ** 2) / (peak ** a * (1 - peak) ** 2)


def hermite(p0, v0, p1, v1, s, duration):
    s2, s3 = s * s, s * s * s
    return p0 * (2 * s3 - 3 * s2 + 1) + v0 * (duration * (s3 - 2 * s2 + s)) + p1 * (3 * s2 - 2 * s3) + v1 * (duration * (s3 - s2))


def rot(pitch=0.0, yaw=0.0, roll=0.0):
    """Grados: pitch se inclina al frente, yaw voltea a su izquierda y roll baja su lado derecho."""
    return Quaternion(UP, radians(yaw)) @ Quaternion(FWD, radians(roll)) @ Quaternion(RIGHT, radians(-pitch))


def foot_q(pitch, yaw):
    """Pitch positivo levanta el talón."""
    return Quaternion(UP, radians(yaw)) @ Quaternion(RIGHT, radians(-pitch))


def frame(direction, normal):
    d = direction.normalized()
    n = (normal - d * normal.dot(d)).normalized()
    return Matrix((d, n, d.cross(n))).transposed()


def keyed(keys, loop=True):
    """Curva Hermite por llaves (u, valor) con tangentes planas en los extremos y en los picos."""
    keys = sorted(keys)
    n = len(keys)

    def at(i):
        if loop:
            u, v = keys[i % n]
            return u + i // n, v
        return keys[min(max(i, 0), n - 1)]

    def slope(i):
        (u0, v0), (_, v1), (u2, v2) = at(i - 1), at(i), at(i + 1)
        if (v1 - v0) * (v2 - v1) <= 0 or u2 == u0:
            return 0.0
        return (v2 - v0) / (u2 - u0)

    def curve(u):
        if loop:
            u %= 1.0
        elif u <= keys[0][0]:
            return keys[0][1]
        elif u >= keys[-1][0]:
            return keys[-1][1]
        i = max((k for k in range(n) if keys[k][0] <= u), default=-1)
        (u0, v0), (u1, v1) = at(i), at(i + 1)
        return hermite(v0, slope(i), v1, slope(i + 1), (u - u0) / (u1 - u0), u1 - u0)
    return curve


def periodic(values):
    values = np.asarray(values, dtype=float)
    count = len(values)

    def sample(u):
        x = (u % 1.0) * count
        i = int(x)
        t = x - i
        return float(values[i % count] * (1 - t) + values[(i + 1) % count] * t)
    return sample


def spring(curve, period, hz, zeta):
    """Respuesta periódica de un resorte amortiguado que sigue a curve(u) durante el ciclo."""
    X = np.fft.rfft(np.array([curve(k / GRID) for k in range(GRID)]))
    w = 2 * pi * np.arange(len(X)) / period
    w0 = 2 * pi * hz
    X *= w0 * w0 / (w0 * w0 - w * w + 2j * zeta * w0 * w)
    return periodic(np.fft.irfft(X, GRID))


def lerp_keys(keys, u):
    """Mezcla de vectores por llaves (u, Vector) con la misma curva de keyed."""
    return Vector([keyed([(k, v[i]) for k, v in keys], loop=False)(u) for i in range(3)])


class Rig:
    def __init__(self):
        self.order = [bone["name"] for bone in SPEC["skeleton"]]
        self.parent = {bone["name"]: bone["parent"] for bone in SPEC["skeleton"]}
        self.head = {name: U(BONES[name]["head"]) for name in self.order}
        self.tail = {name: U(BONES[name]["tail"]) for name in self.order}
        self.dir = {name: (self.tail[name] - self.head[name]).normalized() for name in self.order}
        self.length = {name: (self.tail[name] - self.head[name]).length for name in self.order}


class Pose:
    def __init__(self, rig):
        self.rig, self.rot, self.loc, self.cache = rig, {}, Vector(), {}

    def set(self, name, q):
        self.rot[name] = q
        self.cache.clear()

    def root(self, location, q):
        self.loc = Vector(location)
        self.set("Hips", q)

    def matrix(self, name):
        m = self.cache.get(name)
        if m is None:
            parent = self.rig.parent[name]
            m = self.matrix(parent) if parent else Matrix.Identity(4)
            if name == "Hips":
                m = m @ Matrix.Translation(self.loc)
            h = self.rig.head[name]
            m = m @ Matrix.Translation(h) @ self.rot.get(name, Quaternion()).to_matrix().to_4x4() @ Matrix.Translation(-h)
            self.cache[name] = m
        return m

    def q(self, name):
        return self.matrix(name).to_quaternion()

    def point(self, name, p):
        return self.matrix(name) @ p

    def head(self, name):
        return self.point(name, self.rig.head[name])

    def tail(self, name):
        return self.point(name, self.rig.tail[name])

    def orient(self, name, q):
        parent = self.rig.parent[name]
        self.set(name, (self.q(parent).inverted() @ q) if parent else q)

    def chain(self, upper, lower, joint, end, twist_upper=Quaternion(), twist_lower=Quaternion()):
        rig = self.rig
        self.orient(upper, rig.dir[upper].rotation_difference(joint - self.head(upper)) @ twist_upper)
        self.orient(lower, rig.dir[lower].rotation_difference(end - joint) @ twist_lower)

    def ik(self, upper, lower, target, pole, twist_upper=Quaternion(), twist_lower=Quaternion()):
        start = self.head(upper)
        a, b = self.rig.length[upper], self.rig.length[lower]
        delta = target - start
        distance = delta.length
        reach = clamp(distance, abs(a - b) + 1e-4, (a + b) * 0.9995)
        axis = delta / distance
        bend = (pole - axis * pole.dot(axis)).normalized()
        along = (a * a - b * b + reach * reach) / (2 * reach)
        joint = start + axis * along + bend * sqrt(max(0.0, a * a - along * along))
        self.chain(upper, lower, joint, start + axis * reach, twist_upper, twist_lower)
        return abs(distance - reach)


class Motion:
    def __init__(self, rig, name):
        self.rig, self.name, self.clip = rig, name, CLIPS[name]
        self.period = self.clip["frames"] / FPS
        self.speed = self.clip.get("speed", 0.0)
        self.loop = self.clip["loop"]
        self.err = 0.0

    def pose(self):
        return Pose(self.rig)

    @staticmethod
    def plant(ball, pitch=0.0, yaw=0.0):
        q = foot_q(pitch, yaw)
        return ball + q @ FOOT_FROM_BALL, q

    @staticmethod
    def clear(ankle, q):
        low = min((q @ v).z for v in SOLE) + ankle.z
        return ankle + UP * max(0.0, -low)

    def leg(self, p, side, ankle, q, yaw=0.0):
        sign = -1 if side == "L" else 1
        pole = Quaternion(UP, radians(yaw)) @ (FWD + RIGHT * (0.12 * sign))
        error = p.ik("UpperLeg." + side, "LowerLeg." + side, ankle, pole,
                     Quaternion(UP, radians(0.4 * yaw)), Quaternion(UP, radians(0.75 * yaw)))
        p.orient("Foot." + side, q)
        self.err = max(self.err, error)

    def stand(self, p, width=0.065, reach=0.07, toe_out=5.0, heel=None):
        for side, sign in SIDES:
            pitch = heel[side] if heel else 0.0
            ankle, q = self.plant(Vector((sign * width, reach, 0)), pitch, -sign * toe_out)
            self.leg(p, side, ankle, q, -sign * toe_out)

    @staticmethod
    def look(p, q, neck=0.4):
        p.set("Neck", Quaternion().slerp(p.q("Chest").inverted() @ q, neck))
        p.orient("Head", q)

    @staticmethod
    def shoulder(p, side, up, forward=0.0):
        sign = -1 if side == "L" else 1
        p.set("Shoulder." + side, Quaternion(UP, radians(sign * forward)) @ Quaternion(FWD, radians(-sign * up)))

    def hand(self, p, side, direction, palm):
        hand, lower = "Hand." + side, "LowerArm." + side
        q = (frame(direction, palm) @ frame(self.rig.dir[hand], -UP).inverted()).to_quaternion()
        rel = p.q(lower).inverted() @ q
        axis = self.rig.dir[lower]
        twist = 2 * atan2(Vector((rel.x, rel.y, rel.z)).dot(axis), rel.w)
        twist = (twist + pi) % (2 * pi) - pi
        p.set(lower, p.rot.get(lower, Quaternion()) @ Quaternion(axis, 0.5 * twist))
        p.orient(hand, q)

    def arm(self, p, side, flex, abduct, elbow, inward=0.0, wrist=0.0):
        """Brazo por ángulos en el marco del pecho: flex al frente, abduct hacia afuera, elbow dobla."""
        sign = -1 if side == "L" else 1
        chest = p.q("Chest")
        q_upper = Quaternion(RIGHT, radians(flex)) @ Quaternion(FWD, radians(-sign * abduct))
        upper = q_upper @ -UP
        lower = Quaternion(upper, radians(-sign * inward)) @ (q_upper @ Quaternion(RIGHT, radians(elbow)) @ -UP)
        start = p.head("UpperArm." + side)
        joint = start + chest @ upper * self.rig.length["UpperArm." + side]
        end = joint + chest @ lower * self.rig.length["LowerArm." + side]
        p.chain("UpperArm." + side, "LowerArm." + side, joint, end)
        palm = chest @ (RIGHT * -sign)
        direction = chest @ lower
        self.hand(p, side, Quaternion(direction.cross(palm), radians(wrist)) @ direction, palm)

    def reach(self, p, side, wrist, pole, direction, palm):
        self.err = max(self.err, p.ik("UpperArm." + side, "LowerArm." + side, wrist, pole))
        self.hand(p, side, direction, palm)


class Gait(Motion):
    def __init__(self, rig, name):
        super().__init__(rig, name)
        g = self.g = SPEC["gaits"][name]
        self.contact = {"L": self.clip["contacts"]["left"], "R": self.clip["contacts"]["right"]}
        self.stride = self.speed * self.period * g["duty"]
        self.swing_pitch = keyed([(0.0, g["heelOff"]), (0.2, g["toeOff"]), (0.72, g["land"]), (1.0, 0.0)], loop=False)

    def stance(self, side, s):
        g, sign = self.g, -1 if side == "L" else 1
        ball = Vector((sign * g["width"], g["reach"] + self.stride * (0.5 - s), 0.0))
        return self.plant(ball, g["heelOff"] * smooth(g["heelStart"], 1.0, s), -sign * g["toeOut"])

    def foot(self, side, u):
        g = self.g
        duty, yaw = g["duty"], (1 if side == "L" else -1) * g["toeOut"]
        phase = (u - self.contact[side]) % 1.0
        if phase < duty:
            ankle, q = self.stance(side, phase / duty)
            return ankle, q, yaw
        s, e, T = (phase - duty) / (1 - duty), 1e-3, self.period
        p0 = self.stance(side, 1.0)[0]
        v0 = (p0 - self.stance(side, 1 - e)[0]) / (e * duty * T)
        p1 = self.stance(side, 0.0)[0]
        v1 = (self.stance(side, e)[0] - p1) / (e * duty * T)
        q = foot_q(self.swing_pitch(s), yaw)
        # El pie se queda atrás a medio vuelo.
        tuck = 28.9 * s ** 3 * (1 - s) ** 2 * g["tuck"]
        ankle = hermite(p0, v0, p1, v1 * 0.5, s, (1 - duty) * T) + UP * (g["lift"] * bump(s, g["liftPeak"])) - FWD * tuck
        return self.clear(ankle, q), q, yaw

    def body(self, u):
        g, p = self.g, self.pose()
        side = cos(2 * pi * (u - g["duty"] / 2))
        bounce = cos(4 * pi * (u - g["low"]))
        yaw = -g["hipYaw"] * cos(2 * pi * u)
        roll = g["hipRoll"] * side
        lean = g["lean"]
        p.root((-g["sway"] * side, 0.0, -g["crouch"] - g["bob"] * bounce), rot(pitch=0.35 * lean, yaw=yaw, roll=roll))
        p.set("Spine", rot(pitch=0.3 * lean + 0.6 * bounce, yaw=-0.55 * yaw, roll=-0.6 * roll))
        p.set("Chest", rot(pitch=0.35 * lean + 0.8 * bounce, yaw=-0.85 * yaw, roll=-0.5 * roll))
        self.look(p, rot(pitch=g["headPitch"] - 1.2 * bounce, yaw=0.15 * yaw))
        for name, sign in SIDES:
            self.shoulder(p, name, g["shrug"] + 1.2 * bounce, g["protract"])
            ankle, q, toe = self.foot(name, u)
            self.leg(p, name, ankle, q, toe)
            swing = cos(2 * pi * (u - (0.5 if name == "L" else 0.0) - g["armLag"]))
            self.arm(p, name, g["armBase"] + g["armSwing"] * swing, g["abduct"],
                     g["elbow"] + g["elbowSwing"] * max(0.0, swing), g["inward"], g["wrist"])
        return p


class Turn(Motion):
    def __init__(self, rig, name, sign):
        super().__init__(rig, name)
        self.g, self.sign = SPEC["gaits"]["Turn"], sign
        self.contact = {"L": self.clip["contacts"]["left"], "R": self.clip["contacts"]["right"]}
        self.sweep = sign * radians(self.clip["turnSpeed"]) * self.g["duty"] * self.period

    def foot(self, side, u):
        g, sign = self.g, -1 if side == "L" else 1
        duty, T = g["duty"], self.period
        phase = (u - self.contact[side]) % 1.0
        rate = -self.sweep / (duty * T)
        if phase < duty:
            angle, lift, pitch = self.sweep * (0.5 - phase / duty), 0.0, 0.0
        else:
            s = (phase - duty) / (1 - duty)
            angle = hermite(-self.sweep / 2, rate, self.sweep / 2, rate, s, (1 - duty) * T)
            lift, pitch = g["lift"] * bump(s, 0.45), g["toeOff"] * bump(s, 0.3)
        yaw = -sign * g["toeOut"] + degrees(angle)
        ball = Quaternion(UP, angle) @ Vector((sign * g["width"], g["reach"], 0)) + UP * lift
        ankle, q = self.plant(ball, pitch, yaw)
        return self.clear(ankle, q), q, yaw

    def body(self, u):
        g, p, k = self.g, self.pose(), self.sign
        side = cos(2 * pi * (u - g["duty"] / 2))
        bounce = cos(4 * pi * (u - 0.08))
        p.root((-g["sway"] * side, 0.0, -g["crouch"] - g["bob"] * bounce),
               rot(pitch=2, yaw=k * g["lead"] + 2 * sin(2 * pi * u), roll=1.5 * side))
        p.set("Spine", rot(pitch=1, yaw=k * g["lead"] * 0.6, roll=-1.0 * side))
        p.set("Chest", rot(pitch=1 + 0.5 * bounce, yaw=k * g["lead"] * 0.6, roll=-0.4 * side))
        self.look(p, rot(pitch=3, yaw=k * g["look"] + 2 * sin(2 * pi * u)))
        for name, sign in SIDES:
            self.shoulder(p, name, 2 + bounce, 2)
            ankle, q, yaw = self.foot(name, u)
            self.leg(p, name, ankle, q, yaw)
            swing = cos(2 * pi * (u - (0.5 if name == "L" else 0.0)))
            self.arm(p, name, 4 + 5 * swing, 12 + 2 * k * sign, 16 + 4 * swing, 4)
        return p


class Idle(Motion):
    def __init__(self, rig, name):
        super().__init__(rig, name)
        T = self.period
        look = keyed([(0.0, 0), (0.1, 0), (0.115, 36), (0.29, 36), (0.3, 12), (0.37, 12), (0.38, 0), (0.55, 0),
                      (0.565, -38), (0.72, -38), (0.73, -10), (0.8, -10), (0.81, 0)])
        weight = keyed([(0.0, -1), (0.4, -1), (0.5, 1), (0.86, 1), (0.96, -1)])
        self.head_yaw = spring(look, T, 1.7, 0.72)
        self.torso_yaw = spring(lambda u: 0.32 * look(u), T, 0.7, 0.85)
        self.hips_yaw = spring(lambda u: 0.1 * look(u), T, 0.45, 1.0)
        trail = spring(self.torso_yaw, T, 1.1, 0.4)
        self.arm_lag = lambda u: trail(u) - self.torso_yaw(u)
        self.shift = spring(weight, T, 0.55, 0.85)
        self.sway = spring(lambda u: 0.026 * weight(u), T, 0.45, 0.5)
        self.listen = spring(lambda u: min(1.0, abs(look(u)) / 30), T, 1.0, 0.8)
        self.fidget = {"R": keyed([(0.0, 0), (0.83, 0), (0.85, 20), (0.87, 6), (0.89, 18), (0.92, 0)]),
                       "L": keyed([(0.0, 0), (0.17, 0), (0.19, -14), (0.215, -2), (0.235, -12), (0.27, 0)])}

    def body(self, u):
        p = self.pose()
        f = 2 * pi * u
        w = self.shift(u)
        breath = sin(3 * f + 0.4)
        drift = sin(2 * f + 1.0) + 0.5 * sin(5 * f)
        hips, torso, head = self.hips_yaw(u), self.torso_yaw(u), self.head_yaw(u)
        p.root((self.sway(u) + 0.002 * drift, 0.004 * sin(f + 0.5), -0.012 - 0.007 * w * w + 0.0015 * breath),
               rot(pitch=1.2 + 0.3 * breath, yaw=hips, roll=-3.2 * w))
        p.set("Spine", rot(pitch=-0.6 * breath, yaw=0.45 * (torso - hips), roll=2.0 * w + 0.4 * drift))
        p.set("Chest", rot(pitch=0.8 - 1.0 * breath, yaw=0.55 * (torso - hips), roll=1.0 * w))
        listen = self.listen(u)
        self.look(p, rot(pitch=-1 - 4 * listen + 1.2 * sin(2 * f + 0.3), yaw=head + 1.5 * sin(4 * f),
                         roll=0.12 * head + 1.5 * sin(f)))
        for side, sign in SIDES:
            self.shoulder(p, side, 1.5 + 1.4 * breath, 1)
        self.stand(p, 0.07, 0.07, 6, {"L": 7 * clamp(w), "R": 7 * clamp(-w)})
        lag = self.arm_lag(u)
        for side, sign in SIDES:
            self.arm(p, side, 3 + sign * 0.5 * lag + 1.2 * drift, 11 + 0.8 * breath - sign * 1.5 * w, 13 + 3 * sin(f + sign), 3,
                     self.fidget[side](u) + 3 * sin(3 * f + sign))
        return p


class Hide(Motion):
    def __init__(self, rig, name):
        super().__init__(rig, name)
        T = self.period
        self.peek = spring(keyed([(0.0, 0), (0.38, 0), (0.44, 1), (0.62, 1), (0.68, 0)]), T, 0.9, 0.85)
        self.glance = spring(keyed([(0.0, 0), (0.46, 0), (0.48, 24), (0.53, 24), (0.55, -20), (0.6, -20), (0.62, 0)]), T, 1.8, 0.75)

    def body(self, u):
        g, p = SPEC["poses"]["Hide"], self.pose()
        f = 2 * pi * u
        breath = sin(4 * f)
        peek = self.peek(u)
        p.root((0.003 * sin(f), g["back"], g["drop"] + 0.004 * breath + 0.02 * peek),
               rot(pitch=g["hips"] - 6 * peek + 0.5 * breath))
        p.set("Spine", rot(pitch=g["spine"] - 6 * peek - 1.0 * breath))
        p.set("Chest", rot(pitch=g["chest"] - 6 * peek - 1.5 * breath))
        self.look(p, rot(pitch=g["head"] - 26 * peek + 1.5 * sin(2 * f), yaw=self.glance(u) * peek), 0.5)
        for side, sign in SIDES:
            self.shoulder(p, side, 6 + 2 * breath, 6)
        self.stand(p, g["width"], g["reach"], g["toeOut"])
        # Los antebrazos descansan sobre el paño de enfrente, que aquí casi no se mueve.
        skirt = Secondary(self.rig, self, [p])
        panel = skirt.panels[0]
        hips = p.matrix("Hips")
        angle = skirt.required(p, skirt.limbs(p))[0][0]
        head, tail = hips @ panel.h, panel.tail(hips, angle)
        along = (tail - head).normalized()
        normal = Quaternion(hips.to_quaternion() @ panel.a, pi / 2) @ along
        for side, sign in SIDES:
            wrist = head + along * g["front"] + normal * (g["over"] + (0.012 if side == "L" else 0.0)) - RIGHT * (sign * g["cross"])
            pole = RIGHT * sign + UP * 0.3
            self.reach(p, side, wrist, pole, Vector((-sign, 0, 0)) - normal * 0.5, -normal)
        return p


class Vanish(Motion):
    def body(self, u):
        g, p = SPEC["poses"]["Vanish"], self.pose()
        f = 2 * pi * u

        def tremor(k, phase):
            return sin(k * f + phase) + 0.6 * sin((k + 8) * f + 1.3 + phase) + 0.4 * sin((k + 17) * f + 2.1 + phase)

        sink = 0.5 - 0.5 * cos(f)
        breath = sin(3 * f)
        a = g["tremor"]
        p.root((0.0015 * tremor(27, 0.0), -0.01, g["drop"] - 0.015 * sink + 0.002 * breath),
               rot(pitch=g["hips"] + 2 * sink + a * tremor(29, 0.4), yaw=1.5 * sin(f), roll=a * tremor(31, 1.0)))
        p.set("Spine", rot(pitch=g["spine"] + 3 * sink))
        p.set("Chest", rot(pitch=g["chest"] + 3 * sink - 1.2 * breath + a * tremor(33, 2.0), roll=a * tremor(28, 0.7)))
        self.look(p, rot(pitch=g["head"] + 5 * sink + a * tremor(30, 1.7), yaw=3 * sin(f + 0.8), roll=4 + a * tremor(32, 0.2)), 0.5)
        for side, sign in SIDES:
            self.shoulder(p, side, g["shrug"] + 2 * sink + a * tremor(26, sign), 10)
        self.stand(p, 0.058, 0.07, -4)
        for side, sign in SIDES:
            local = Vector(g["wrist" + side])
            wrist = p.point("Chest", U(local)) + UP * (0.002 * tremor(34, sign))
            pole = p.q("Chest") @ (RIGHT * (0.7 * sign) - UP + FWD * 0.6)
            direction = p.q("Chest") @ Vector((-sign, -0.25, 0.15))
            self.reach(p, side, wrist, pole, direction, p.q("Chest") @ Vector((0, -1, 0)))
        return p


class Carry(Motion):
    def wrist(self, side, u):
        sign = -1 if side == "L" else 1
        f = 2 * pi * u
        return U((sign * 0.035 + 0.005 * sin(f), 0.85 + 0.003 * sin(2 * f + 0.6), 0.205 + 0.003 * cos(f)))

    def hold(self, p, side, wrist, direction, palm):
        sign = -1 if side == "L" else 1
        pole = U((sign * 0.31, 0.77, 0.04)) - p.head("UpperArm." + side)
        self.reach(p, side, wrist, pole, direction, palm)

    def body(self, u):
        p = self.pose()
        for side, sign in SIDES:
            self.hold(p, side, self.wrist(side, u), Vector((-sign * 0.045, 0.035, 0.015 + 0.004 * sin(2 * pi * u))),
                      Vector((-sign * 0.35, 0, 1)))
        return p


class Flash(Carry):
    def __init__(self, rig, name):
        super().__init__(rig, name)
        T = self.period
        self.times = [t / T for t in (0.0, 0.09, 0.17, 0.26, 0.4, 0.7)]

    def body(self, u):
        p = self.pose()
        for side, sign in SIDES:
            carry = self.wrist(side, 0.0)
            wrists = [carry, carry + Vector((sign * 0.01, -0.03, -0.015)), U((sign * 0.11, 0.93, 0.32)),
                      U((sign * 0.13, 0.95, 0.31)), U((sign * 0.07, 0.89, 0.17)), carry]
            dirs = [Vector((-sign * 0.045, 0.035, 0.015)), Vector((-sign * 0.045, 0.03, 0.02)), Vector((sign * 0.35, 0.5, 0.8)),
                    Vector((sign * 0.45, 0.45, 0.8)), Vector((-sign * 0.1, 0.6, 0.4)), Vector((-sign * 0.045, 0.035, 0.015))]
            palms = [Vector((-sign * 0.35, 0, 1)), Vector((-sign * 0.35, 0, 1)), Vector((0, 1, 0.1)),
                     Vector((sign * 0.1, 1, 0.1)), Vector((-sign * 0.3, 0.6, 0.7)), Vector((-sign * 0.35, 0, 1))]
            keys = list(zip(self.times, wrists))
            self.hold(p, side, lerp_keys(keys, u), lerp_keys(list(zip(self.times, dirs)), u),
                      lerp_keys(list(zip(self.times, palms)), u))
        return p


class Caught(Motion):
    def __init__(self, rig, name):
        super().__init__(rig, name)
        T = self.period

        def at(keys):
            return keyed([(t / T, v) for t, v in keys], loop=False)
        self.startle = at([(0.0, 0), (0.07, 1), (0.16, 0.85), (0.42, 0)])
        self.cover = at([(0.0, 0), (0.12, 0.25), (0.36, 1), (T, 1)])
        self.curl = at([(0.0, 0), (0.22, 0), (0.75, 0.7), (1.05, 1), (T, 1)])
        self.flinch = at([(0.0, 0), (0.09, 1), (0.3, 0)])
        self.T = T

    def kneel(self, t):
        s = clamp((t - 0.26) / 0.56)
        return s * s * (1.6 - 0.6 * s)

    def body(self, u):
        g, p, T = SPEC["poses"]["Caught"], self.pose(), self.T
        t = u * T
        k, startle, curl, cover = self.kneel(t), self.startle(u), self.curl(u), self.cover(u)
        impact = -0.018 * bump(clamp((t - 0.82) / 0.3), 0.25)
        live = 1 - smooth(1.05, 1.25, t)
        shiver = live * (sin(2 * pi * 9 * t) + 0.5 * sin(2 * pi * 13 * t + 1))
        p.root((0.0, mix(0.0, g["forward"], k), mix(-0.015, g["drop"], k) + 0.018 * startle + impact),
               rot(pitch=-5 * startle + mix(1, g["hips"], curl) + 0.3 * shiver))
        p.set("Spine", rot(pitch=-4 * startle + g["spine"] * curl))
        p.set("Chest", rot(pitch=-6 * startle + g["chest"] * curl + 0.4 * shiver))
        self.look(p, rot(pitch=-14 * startle + g["head"] * curl + 0.5 * shiver, roll=3 * curl), 0.5)
        for side, sign in SIDES:
            self.shoulder(p, side, 10 * startle + g["shrug"] * cover, 8 * cover)
        slide = smooth(0.1, 0.95, k)
        for side, sign in SIDES:
            ball = Vector((sign * mix(0.065, 0.075, k), mix(0.07, g["ball"], slide), 0.012 * bump(slide, 0.4)))
            yaw = -sign * mix(5, 12, k)
            ankle, q = self.plant(ball, g["toe"] * smooth(0.05, 0.85, k), yaw)
            self.leg(p, side, ankle, q, yaw)
        chest, head = p.q("Chest"), p.q("Head")
        for side, sign in SIDES:
            hang = p.point("Chest", U((sign * 0.15, 0.575, 0.03)))
            flinch = p.point("Chest", U((sign * 0.22, 0.86, 0.13)))
            shield = p.point("Head", U((-sign * g["cross"], 1.075, g["shield"])))
            a = startle * (1 - cover)
            wrist = hang * (1 - cover - a) + flinch * a + shield * cover
            pole = chest @ (RIGHT * sign - UP * 0.8 + FWD * 0.3)
            direction = (chest @ Vector((0, 0.1, -1))).lerp(head @ Vector((-sign * 0.5, 0.15, 1)), cover)
            palm = (chest @ Vector((-sign, 0, 0))).lerp(head @ Vector((0, -1, 0)), cover)
            self.reach(p, side, wrist, pole, direction, palm)
        return p


MOTIONS = {"Idle": Idle, "Slow": Gait, "Walk": Gait, "Run": Gait, "Hide": Hide, "CarryLight": Carry,
           "Flash": Flash, "Caught": Caught, "Vanish": Vanish}


def create_motion(rig, name):
    if name.startswith("Turn"):
        return Turn(rig, name, 1 if name == "TurnLeft" else -1)
    return MOTIONS[name](rig, name)


def dress_radius(height):
    rows = SPEC["body"]["dress"]
    for (y0, x0, z0), (y1, x1, z1) in zip(rows, rows[1:]):
        if height <= y1:
            t = clamp((height - y0) / (y1 - y0))
            return mix(x0, x1, t), mix(z0, z1, t)
    return rows[-1][1], rows[-1][2]


class Panel:
    """Paño de la falda: gira hacia afuera sobre su cabeza para dejar pasar las piernas."""

    def __init__(self, rig, name):
        self.name = name
        self.h, self.t = rig.head[name], rig.tail[name]
        v = self.t - self.h
        self.L = v.length
        self.o = Vector((v.x, v.y, 0)).normalized()
        self.a = self.o.cross(UP)
        self.alpha0 = atan2(v.dot(self.o), -v.z)
        self.azimuth = atan2(self.o.x, self.o.y)

    def weight(self, psi):
        d = abs((psi - self.azimuth + pi) % (2 * pi) - pi)
        return max(0.0, 1 - d / (pi / 2))

    def tail(self, hips, angle):
        return hips @ (self.h + Quaternion(self.a, angle) @ (self.t - self.h))


LEG_POINTS = (("thigh", 0.35, 0.045), ("thigh", 0.55, 0.043), ("thigh", 0.75, 0.04), ("thigh", 0.9, 0.036), ("thigh", 1.0, 0.035),
              ("shin", 0.12, 0.04), ("shin", 0.3, 0.044), ("shin", 0.5, 0.042), ("shin", 0.7, 0.038),
              ("shin", 0.9, 0.036), ("foot", 0.0, 0.035), ("foot", 0.6, 0.03), ("foot", 1.0, 0.025))


class Secondary:
    def __init__(self, rig, motion, poses):
        self.rig, self.motion, self.poses = rig, motion, poses
        self.n = len(poses) - 1
        self.dt = 1.0 / (FPS * SUB)
        self.panels = [Panel(rig, name) for name in SKIRT]

    def sequence(self, step, state):
        """Corre el sistema y devuelve el estado de cada pose: varias vueltas en bucle o un asentamiento antes de una vez."""
        n, motion = self.n, self.motion
        if motion.loop:
            for _ in range(max(3, ceil(SIM["settle"] / motion.period))):
                for j in range(n):
                    state = step(state, j)
        else:
            for _ in range(int(SIM["settle"] / self.dt)):
                state = step(state, 0, still=True)
        record = [state]
        for j in range(n):
            state = step(state, j)
            record.append(state)
        return record

    def limbs(self, p):
        inverse = p.matrix("Hips").inverted()
        points, arms = [], []
        for side, _ in SIDES:
            for bone, radius in (("UpperArm", 0.036), ("LowerArm", 0.032), ("Hand", 0.042)):
                head, tail = p.head(bone + "." + side), p.tail(bone + "." + side)
                arms += [(inverse @ head.lerp(tail, t), radius) for t in (0.0, 0.5, 1.0)]
            hip, knee, ankle = (p.head(bone + "." + side) for bone in ("UpperLeg", "LowerLeg", "Foot"))
            segments = {"thigh": (hip, knee), "shin": (knee, ankle), "foot": (ankle, p.tail("Foot." + side))}
            for part, t, radius in LEG_POINTS:
                points.append((inverse @ segments[part][0].lerp(segments[part][1], t), radius))
        return points, arms

    def bound(self, point, radius, sign):
        """Giro de cada paño que deja la tela justo afuera de una pierna (sign 1) o adentro de un brazo (-1)."""
        low = SPEC["body"]["dress"][0][0]
        hold = smooth(0.63, 0.54, point.z)
        # Arriba la tela pesa más a la cadera y casi no sigue a los paños.
        if not low - 0.06 < point.z < 0.62 or hold < 0.4:
            return []
        center = 0.02 * clamp((0.63 - point.z) / 0.3)
        psi = atan2(point.x, point.y - center)
        rx, rz = dress_radius(point.z)
        out = Vector((sin(psi), cos(psi), 0))
        edge = (1 - 0.035 * clamp((0.6 - point.z) / 0.2)) / sqrt((out.x / rx) ** 2 + (out.y / rz) ** 2)
        surface = Vector((out.x * edge, center + out.y * edge, point.z))
        gap = (hypot(point.x, point.y - center) - edge + sign * (radius + SIM["skirtMargin"])) * clamp((point.z - low + 0.06) / 0.03)
        moves = [(i, panel.weight(psi) * hold * panel.a.cross(surface - panel.h).dot(out)) for i, panel in enumerate(self.panels)]
        moves = [(i, m) for i, m in moves if m > 0.005]
        if not moves:
            return []
        if sign > 0:
            total = sum(m * m for _, m in moves)
            return [(i, gap * m / total) for i, m in moves]
        return [(i, gap / sum(m for _, m in moves)) for i, m in moves if self.panels[i].weight(psi) >= 0.2]

    def required(self, p, limbs):
        legs, arms = limbs
        need, limit = [-SIM["skirtIn"]] * 4, [SIM["skirtMax"]] * 4
        for point, radius in arms:
            for i, value in self.bound(point, radius, -1):
                limit[i] = min(limit[i], value)
        line = [None] * 4
        for point, radius in legs:
            for i, value in self.bound(point, radius, 1):
                need[i] = max(need[i], value)
            for i, panel in enumerate(self.panels[:1]):
                v = point - panel.h
                rho = hypot(v.dot(panel.o), v.z)
                if panel.weight(atan2(point.x, point.y - 0.02)) >= 0.3 and v.dot(panel.o) > 0 and rho > 0.15:
                    angle = atan2(v.dot(panel.o), -v.z) + asin(min(1.0, (radius + SIM["skirtMargin"]) / rho)) - panel.alpha0
                    line[i] = angle if line[i] is None else max(line[i], angle)
        # Al frente, la recta del paño queda dentro de la tela.
        need = [n if l is None or n < SIM["skirtLine"] else max(SIM["skirtLine"], min(n, l)) for n, l in zip(need, line)]
        hips = p.matrix("Hips")
        result = []
        for i, panel in enumerate(self.panels):
            n = min(need[i], SIM["skirtMax"])
            while n < 2.2 and panel.tail(hips, n).z < SIM["skirtFloor"]:
                n += 0.03
            result.append((n, max(n, limit[i])))
        return result

    def skirt(self):
        c, poses, dt = SIM["skirt"], self.poses, self.dt
        n = self.n
        w0 = 2 * pi * c["hz"]
        hips = [p.matrix("Hips") for p in poses]
        bounds = [self.required(p, self.limbs(p)) for p in poses]
        result = {}
        for index, panel in enumerate(self.panels):
            req, top = zip(*[b[index] for b in bounds])
            tails = [m @ panel.t for m in hips]
            targets, forcing = [], []
            for j in range(n + 1):
                inverse = hips[j].to_quaternion().inverted()
                down = inverse @ -UP
                ahead = inverse @ FWD
                targets.append(c["gravity"] * atan2(down.dot(panel.o), -down.z) - c["wind"] * self.motion.speed ** 2 * ahead.dot(panel.o))
                if self.motion.loop:
                    prev, nxt = tails[(j - 1) % n], tails[(j + 1) % n]
                else:
                    prev, nxt = tails[max(j - 1, 0)], tails[min(j + 1, n)]
                forcing.append(inverse @ ((nxt - 2 * tails[j] + prev) / (dt * dt)))

            def step(state, j, still=False):
                x, v, s, sv = state
                k = j if still else (j + 1) % (n + 1) if not self.motion.loop else (j + 1) % n
                alpha = panel.alpha0 + x
                tangent = panel.o * cos(alpha) + UP * sin(alpha)
                a = 0.0 if still else forcing[j].dot(tangent) / panel.L
                b = 0.0 if still else forcing[j].dot(panel.a) / panel.L
                v += (-w0 * w0 * (x - targets[j]) - 2 * c["zeta"] * w0 * v - a) * dt
                x += v * dt
                sv += (-w0 * w0 * s - 2 * c["zeta"] * w0 * sv - b) * dt
                s = clamp(s + sv * dt, -c["side"], c["side"])
                if x < req[k]:
                    x, v = req[k], 0.0 if still else max(0.0, (req[k] - req[j]) / dt)
                x = min(x, req[k] + c["open"], top[k])
                return x, v, s, sv

            start = max(req[0], min(targets[0], top[0]))
            record = self.sequence(step, (start, 0.0, 0.0, 0.0))
            if self.motion.loop:
                ex, es = record[-1][0] - record[0][0], record[-1][2] - record[0][2]
                record = [(x - ex * smooth(0, 1, j / n), v, s - es * smooth(0, 1, j / n), sv) for j, (x, v, s, sv) in enumerate(record)]
            result[panel.name] = [(max(min(x, top[j]), req[j]), s) for j, (x, v, s, sv) in enumerate(record)]
        for j, p in enumerate(poses):
            for panel in self.panels:
                x, s = result[panel.name][j]
                p.set(panel.name, Quaternion(panel.a, x) @ Quaternion(panel.o, s))

    def colliders(self, p):
        caps = [(p.head("Neck"), p.tail("Neck"), 0.034), (p.point("Head", U(SPEC["body"]["head"]["center"])),) * 2 + (0.112,)]
        for side, sign in SIDES:
            caps.append((p.point("Shoulder." + side, U((sign * 0.1, 0.913, 0))), p.point("UpperArm." + side, U((sign * 0.2, 0.913, 0))), 0.05))
            caps += [(p.head(b + "." + side), p.tail(b + "." + side), r) for b, r in (("UpperArm", 0.04), ("LowerArm", 0.032), ("Hand", 0.036))]
            caps.append((p.head("UpperLeg." + side), p.head("LowerLeg." + side), 0.1))
        caps += [(p.head(name), p.tail(name), 0.05) for name in SKIRT]
        caps += [(p.tail(a), p.tail(b), 0.05) for a, b in zip(SKIRT, SKIRT[1:] + SKIRT[:1])]
        frames = [(p.matrix(name), p.matrix(name).inverted(), low, high) for name, low, high in (("Spine", 0.66, 0.8), ("Chest", 0.8, 0.95))]
        return caps, frames

    @staticmethod
    def collide(x, radius, caps, frames):
        for a, b, r in caps:
            ab = b - a
            t = clamp((x - a).dot(ab) / max(ab.length_squared, 1e-9))
            d = x - (a + ab * t)
            if d.length < r + radius:
                x = a + ab * t + d.normalized() * (r + radius)
        for m, inverse, low, high in frames:
            local = inverse @ x
            if low <= local.z <= high:
                rx, rz = dress_radius(local.z)
                e = (local.x / (rx + radius)) ** 2 + (local.y / (rz + radius)) ** 2
                if 1e-6 < e < 1:
                    local.x, local.y = local.x / sqrt(e), local.y / sqrt(e)
                    x = m @ local
        x.z = max(x.z, radius)
        return x

    def braids(self):
        c, poses, dt, rig = SIM["braid"], self.poses, self.dt, self.rig
        wind = -FWD * self.motion.speed
        for side, _ in SIDES:
            bones = ["Braid.%s.%d" % (side, i) for i in (1, 2, 3)]
            joints = [rig.head[bones[0]]] + [rig.tail[b] for b in bones]
            lengths = [(b - a).length for a, b in zip(joints, joints[1:])]
            frames = []
            for p in poses:
                q = p.q("Chest").slerp(p.q("Head"), c["follow"])
                root = p.point("Head", joints[0])
                frames.append((root, [root + q @ (j - joints[0]) for j in joints[1:]], (q @ UP - UP) * c["gravity"], self.colliders(p)))

            def step(state, j, still=False):
                x, xp = [v.copy() for v in state[0]], [v.copy() for v in state[1]]
                root, goals, gravity, (caps, torso) = frames[j]
                for i in range(1, 4):
                    vel = (x[i] - xp[i]) / dt
                    acc = (goals[i - 1] - x[i]) * c["stiffness"][i - 1] + gravity - vel * c["damping"] + (wind - vel) * c["drag"]
                    xp[i], x[i] = x[i], x[i] + vel * dt + acc * (dt * dt)
                x[0] = xp[0] = root
                for _ in range(2):
                    for i in range(1, 4):
                        x[i] = x[i - 1] + (x[i] - x[i - 1]).normalized() * lengths[i - 1]
                        x[i] = self.collide(x[i], c["radius"], caps, torso)
                        middle = (x[i - 1] + x[i]) / 2
                        x[i] += (self.collide(middle.copy(), c["radius"], caps, torso) - middle) * 2
                    tip = x[3] + (x[3] - x[2]).normalized() * 0.035
                    x[3] += self.collide(tip.copy(), c["radius"], caps, torso) - tip
                return x, xp

            start = [frames[0][0]] + frames[0][1]
            record = self.sequence(step, (start, [v.copy() for v in start]))
            n = self.n
            if self.motion.loop:
                error = [e - s for e, s in zip(record[-1][0], record[0][0])]
                record = [([v - e * smooth(0, 1, j / n) for v, e in zip(x, error)], xp) for j, (x, xp) in enumerate(record)]
            for p, (x, _) in zip(poses, record):
                for i, bone in enumerate(bones):
                    parent = p.q(rig.parent[bone])
                    p.orient(bone, (parent @ rig.dir[bone]).rotation_difference(x[i + 1] - x[i]) @ parent)


def bake(rig, name):
    motion = create_motion(rig, name)
    steps = motion.clip["frames"] * SUB
    poses = [motion.body(j / steps) for j in range(steps + 1)]
    if not motion.clip.get("armsOnly"):
        secondary = Secondary(rig, motion, poses)
        secondary.skirt()
        secondary.braids()
    return motion, poses[::SUB]


def make_actions(arm):
    rig = Rig()
    rest = {bone.name: bone.matrix_local.to_quaternion() for bone in arm.data.bones}
    animation = arm.animation_data_create()
    actions = {}
    for name, clip in CLIPS.items():
        motion, poses = bake(rig, name)
        action = bpy.data.actions.new(name)
        action.use_fake_user = True
        animation.action = action
        bones = [bone for bone in rest if not clip.get("armsOnly") or bone in ARMS]
        rotation_keys = {bone: [[] for _ in range(4)] for bone in bones}
        location_keys, previous = [[], [], []], {}
        for frame_index, pose in enumerate(poses):
            for bone in bones:
                q = rest[bone].inverted() @ pose.rot.get(bone, Quaternion()) @ rest[bone]
                if bone in previous and previous[bone].dot(q) < 0:
                    q.negate()
                previous[bone] = q
                for i in range(4):
                    rotation_keys[bone][i] += [float(frame_index), q[i]]
            local = rest["Hips"].inverted() @ pose.loc
            for i in range(3):
                location_keys[i] += [float(frame_index), local[i]]
        curves = [(f'pose.bones["{bone}"].rotation_quaternion', i, bone, rotation_keys[bone][i]) for bone in bones for i in range(4)]
        if not clip.get("armsOnly"):
            curves += [('pose.bones["Hips"].location', i, "Hips", location_keys[i]) for i in range(3)]
        for path, index, group, keys in curves:
            curve = action.fcurve_ensure_for_datablock(arm, path, index=index, group_name=group)
            curve.keyframe_points.add(clip["frames"] + 1)
            curve.keyframe_points.foreach_set("co", keys)
            for key in curve.keyframe_points:
                key.interpolation = "LINEAR"
            curve.update()
        action.use_frame_range = True
        action.frame_start, action.frame_end = 0, clip["frames"]
        action.use_cyclic = clip["loop"]
        actions[name] = action
        print(f"{LOG_PREFIX} {name}: {clip['frames']} cuadros, {clip['frames'] / FPS:.2f} s, {clip.get('speed', 0):.1f} m/s, "
              f"error de IK {motion.err * 1000:.1f} mm")
    animation.action = None
    for bone in arm.pose.bones:
        bone.rotation_quaternion = Quaternion()
        bone.location = (0, 0, 0)
    for name, action in actions.items():
        track = animation.nla_tracks.new()
        track.name = name
        strip = track.strips.new(name, STRIP[name], action)
        strip.extrapolation = "NOTHING"
        if hasattr(strip, "action_slot") and strip.action_slot is None and len(action.slots):
            strip.action_slot = action.slots[0]
    return actions
