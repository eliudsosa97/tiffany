"""Animación en su lugar con apoyos por IK y pelo y tela horneados."""

import math
import numpy as np
from mathutils import Quaternion, Vector
from witch_rig import LEFT, BACK, UP, FWD, V, Poser, bow, lean, turn, rot, frame

FPS = 30
SUB = 8
GRID = 480
GRAVITY = Vector((0, 0, -9.81))


def clamp(x, a=0.0, b=1.0):
    return a if x < a else b if x > b else x


def smooth(a, b, x):
    t = clamp((x - a) / (b - a))
    return t * t * (3 - 2 * t)


def hermite(p0, v0, p1, v1, s, dur):
    s2, s3 = s * s, s * s * s
    return p0 * (2 * s3 - 3 * s2 + 1) + v0 * (dur * (s3 - 2 * s2 + s)) + p1 * (3 * s2 - 2 * s3) + v1 * (dur * (s3 - s2))


def bump(s, a=2.0, b=2.0):
    peak = a / (a + b)
    return (s ** a * (1 - s) ** b) / (peak ** a * (1 - peak) ** b)


def loop_curve(keys):
    """Curva periódica en u de 0 a 1 con tangentes Catmull-Rom. keys: [(u, valor)]."""
    keys = sorted(keys)
    n = len(keys)

    def at(i):
        u, v = keys[i % n]
        return u + (i // n), v

    def tangent(i):
        (u0, v0), (u1, v1) = at(i - 1), at(i + 1)
        return (v1 - v0) / (u1 - u0)

    def f(u):
        u %= 1.0
        i = max([k for k in range(n) if keys[k][0] <= u], default=-1)
        u0, v0 = at(i)
        u1, v1 = at(i + 1)
        d = u1 - u0
        return hermite(v0, tangent(i), v1, tangent(i + 1), (u - u0) / d, d)
    return f


class Signal:
    """Señal periódica muestreada en GRID puntos, escalar o de vectores."""

    def __init__(self, values):
        self.v = np.asarray(values, dtype=float)

    def __call__(self, u):
        n = len(self.v)
        f = (u % 1.0) * n
        i = int(f)
        t = f - i
        x = self.v[i % n] * (1 - t) + self.v[(i + 1) % n] * t
        return Vector(x) if self.v.ndim == 2 else float(x)


def spring(values, period, hz, zeta):
    """Respuesta periódica de un resorte amortiguado a la señal (por componente)."""
    values = np.asarray(values, dtype=float)
    X = np.fft.rfft(values, axis=0)
    w = 2 * math.pi * np.arange(X.shape[0]) / period
    w0 = 2 * math.pi * hz
    H = w0 * w0 / (w0 * w0 - w * w + 2j * zeta * w0 * w)
    X *= H[:, None] if X.ndim == 2 else H
    return Signal(np.fft.irfft(X, len(values), axis=0))


def integrate2(values, period):
    """Doble integral periódica de media cero: de aceleración a posición."""
    X = np.fft.rfft(np.asarray(values, dtype=float))
    w = 2 * math.pi * np.arange(len(X)) / period
    X[0] = 0
    X[1:] /= -(w[1:] ** 2)
    return np.fft.irfft(X, len(values))


def pulse(u, at, width):
    d = (u - at + 0.5) % 1.0 - 0.5
    return 1.0 if 0 <= d < width else 0.0


def window(u, start, end, ramp_in, ramp_out):
    """1 entre start y end con rampas suaves, periódica."""
    d = (u - start) % 1.0
    length = (end - start) % 1.0
    return smooth(0, ramp_in, d) * (1 - smooth(length, length + ramp_out, d)) if d < length + ramp_out else 0.0


def side_out(s):
    return LEFT if s == "L" else -LEFT


class Steps:
    """Pisadas de una extremidad en el ciclo: [(td, lo, datos)] con td en [0, 1) y lo > td."""

    def __init__(self, steps):
        self.steps = sorted(steps, key=lambda st: st[0])

    def at(self, u):
        n = len(self.steps)
        for i, (td, lo, data) in enumerate(self.steps):
            d = (u - td) % 1.0
            if d < lo - td:
                return "stance", i, d / (lo - td)
        for i, (td, lo, data) in enumerate(self.steps):
            nxt = self.steps[(i + 1) % n][0] + (1.0 if i + 1 >= n else 0.0)
            d = (u - lo) % 1.0
            if d < nxt - lo:
                return "swing", i, d / (nxt - lo)
        raise ValueError(u)


class Motion:
    """Base de un clip: cuerpo en body(u), cadenas simuladas y muestreo."""

    def __init__(self, rig, ctx, cfg, frames, speed):
        self.rig, self.ctx, self.cfg = rig, ctx, cfg
        self.frames, self.period, self.speed = frames, frames / FPS, speed
        self.loop = True
        self.lead = None
        self.err = 0.0
        self.misses = {}
        self.foot_vec = {s: ctx["toe"][s] - rig.head["Foot." + s] for s in "LR"}
        local = {s: rig.inv["Fingers." + s] @ ctx["fingertip"][s] for s in "LR"}
        self.chord = {s: local[s].normalized() for s in "LR"}
        self.chord_len = {s: local[s].length for s in "LR"}
        self.hand_len = {s: rig.length["Hand." + s] for s in "LR"}

    def rest_pole(self, upper, lower):
        a, m, c = self.rig.head[upper], self.rig.head[lower], self.rig.tail[lower]
        d = (c - a).normalized()
        return (m - (a + d * (m - a).dot(d))).normalized()

    def head_to(self, p, q):
        """Cabeza con orientación q respecto a erguida y mirando al frente."""
        p.orient("Head", q @ self.ctx["skull"].inverted())

    def miss(self, limb, e):
        self.misses[limb] = max(self.misses.get(limb, 0.0), e)
        self.err = max(self.err, e)

    def leg(self, p, s, ankle, q_foot, pole):
        floor = min((q_foot @ v).z for v in self.ctx["sole"][s])
        ankle = ankle + UP * max(0.0, 0.002 - ankle.z - floor)
        self.miss("Leg." + s, p.ik("Thigh." + s, "Shin." + s, ankle, pole))
        p.orient("Foot." + s, q_foot)

    def arm(self, p, s, wrist, hand_dir, chord_dir, pole, x=LEFT):
        self.miss("Arm." + s, p.ik("UpperArm." + s, "Forearm." + s, wrist, pole))
        p.aim("Hand." + s, hand_dir, x)
        p.aim_local("Fingers." + s, self.chord[s], chord_dir, x)

    def curl(self, bone, deg):
        return Quaternion(self.rig.rest[bone].col[0].xyz, math.radians(deg))

    def grid(self, fn):
        return [fn(k / GRID) for k in range(GRID)]

    def gait_effector(self, steps, u, stance_fn, swing_fn):
        """Efector de una pierna: en apoyo lo da el contacto; en vuelo, Hermite con la velocidad del apoyo."""
        phase, i, s = steps.at(u)
        T = self.period
        if phase == "stance":
            return stance_fn(i, s)
        n = len(steps.steps)
        td0, lo0, _ = steps.steps[i]
        td1, lo1, _ = steps.steps[(i + 1) % n]
        dur = ((td1 + (1.0 if i + 1 >= n else 0.0)) - lo0) * T
        e = 1e-3
        p0 = stance_fn(i, 1.0)[0]
        v0 = (p0 - stance_fn(i, 1.0 - e)[0]) / (e * (lo0 - td0) * T)
        p1 = stance_fn((i + 1) % n, 0.0)[0]
        v1 = (stance_fn((i + 1) % n, e)[0] - p1) / (e * (lo1 - td1) * T)
        return swing_fn(i, s, hermite(p0, v0, p1, v1, s, dur))

    def contact(self, td_point, s, stance):
        return td_point - FWD * (self.speed * s * stance * self.period)

    def sample_times(self, step):
        n = int(round(self.frames / step))
        return [k * step for k in range(n + 1)]

    def wind(self, u):
        return FWD * self.speed

    def run(self, chains, step):
        frames = self.sample_times(step)
        recorded = chains.simulate(self, [f / FPS for f in frames])
        out = []
        for f, state in zip(frames, recorded):
            p = self.body(f / self.frames)
            chains.apply(p, state)
            p.fill()
            out.append((f, p))
        return out


class Idle(Motion):
    def __init__(self, rig, ctx, cfg, frames):
        super().__init__(rig, ctx, cfg, frames, 0.0)
        c, T = cfg, self.period
        self.twitch = spring(self.grid(lambda u: window(u, c["twitch"][0], c["twitch"][1], 0.004, 0.14)), T, 3.0, 0.3)
        self.hitch = spring(self.grid(lambda u: window(u, c["twitch"][0] + 0.008, c["twitch"][1] + 0.03, 0.004, 0.18)), T, 3.6, 0.35)
        self.clench = spring(self.grid(lambda u: window(u, c["clench"][0], c["clench"][1], 0.01, 0.2)), T, 2.2, 0.5)
        self.glance = spring(self.grid(lambda u: window(u, c["glance"][0], c["glance"][1], 0.006, 0.1)), T, 3.4, 0.32)
        self.poles = {s: self.rest_pole("UpperArm." + s, "Forearm." + s) for s in "LR"}
        self.knees = {s: self.rest_pole("Thigh." + s, "Shin." + s) for s in "LR"}
        self.shoulders = Signal(self.grid(lambda u: tuple(self.torso(Poser(rig), u).head("UpperArm.L"))))
        self.hang = rig.tail["Forearm.L"] - rig.head["UpperArm.L"]
        self.hang_l = spring([tuple(Vector(x) + self.hang) for x in self.shoulders.v], T, 0.6, 0.3)
        right = rig.tail["Forearm.R"]
        self.raise_r = spring(self.grid(lambda u: tuple(self.torso(Poser(rig), u).point("Chest", right))), T, 2.0, 0.45)

    def tremor(self, u, k, amp):
        return amp * (math.sin(2 * math.pi * k * u) + 0.6 * math.sin(2 * math.pi * (k + 6) * u + 1.3) + 0.4 * math.sin(2 * math.pi * (k + 13) * u + 2.1))

    def torso(self, p, u):
        c = self.cfg
        f = 2 * math.pi * u
        breath = math.sin(2 * f) + 0.25 * math.sin(3 * f + 0.7)
        e, h = self.twitch(u), self.hitch(u)
        hips = self.rig.head["Hips"]
        p.root("Hips", hips + V(c["sway"] * math.sin(f), 0.004 * breath, 0.6 * c["sway"] * math.sin(f + 1.1)),
               rot(pitch=0.6 * breath, roll=1.5 * math.sin(f + 0.3), yaw=2.0 * math.sin(f + 0.5)))
        p.rel("Spine", rot(pitch=-0.8 * breath, roll=-1.0 * math.sin(f + 0.3)))
        p.rel("Chest", rot(pitch=-1.4 * breath - 2 * e, roll=-3 * e, yaw=4 * e))
        p.rel("Clavicle.R", lean(-c["hitchDeg"] * h))
        p.rel("Clavicle.L", lean(0.8 * breath))
        return p

    def body(self, u):
        c, rig = self.cfg, self.rig
        p = self.torso(Poser(rig), u)
        e, g = self.twitch(u), self.glance(u)
        p.rel("Neck", rot(pitch=4 * e + self.tremor(u, 23, 0.25), roll=-c["twitchRoll"] * 0.4 * e, yaw=6 * e - 5 * g))
        p.rel("Head", rot(pitch=c["twitchPitch"] * e + self.tremor(u, 29, 0.4), roll=-c["twitchRoll"] * e + self.tremor(u, 31, 0.3),
                          yaw=8 * e - 14 * g))
        p.rel("Jaw", bow(2 + 2 * e + self.tremor(u, 37, 0.3)))
        for s in "LR":
            self.leg(p, s, rig.head["Foot." + s], Quaternion(), self.knees[s])
        self.err = max(self.err, p.ik("UpperArm.L", "Forearm.L", self.hang_l(u), self.poles["L"]))
        self.err = max(self.err, p.ik("UpperArm.R", "Forearm.R", self.raise_r(u), p.delta("Chest").to_3x3() @ self.poles["R"]))
        for s, amp in (("L", 0.8), ("R", 1.2)):
            p.rel("Hand." + s, Quaternion(LEFT, math.radians(self.tremor(u, 41, amp))) @ Quaternion(UP, math.radians(self.tremor(u, 43, amp))))
        p.rel("Fingers.R", self.curl("Fingers.R", c["clenchDeg"] * self.clench(u) + self.tremor(u, 47, 1.5)))
        p.rel("Fingers.L", self.curl("Fingers.L", self.tremor(u, 53, 1.0)))
        return p


class Suspicious(Motion):
    def __init__(self, rig, ctx, cfg, frames):
        super().__init__(rig, ctx, cfg, frames, 0.0)
        c, T = cfg, self.period
        self.scan = loop_curve([tuple(k) for k in c["scan"]])
        rise, release = c["sniffRiseSeconds"] / T, c["sniffReleaseSeconds"] / T
        self.sniff = spring(self.grid(lambda u: sum(window(u, at, at + rise, rise, release) for at in c["sniffs"])), T, *c["sniffSpring"])
        self.clench = spring(self.grid(lambda u: window(u, *c["clench"], *c["clenchRamps"])), T, *c["clenchSpring"])
        self.knees = {s: self.rest_pole("Thigh." + s, "Shin." + s) for s in "LR"}
        self.poles = {s: self.rest_pole("UpperArm." + s, "Forearm." + s) for s in "LR"}

    def body(self, u):
        c, rig = self.cfg, self.rig
        p = Poser(rig)
        sniff = self.sniff(u)
        scan = c["scanDeg"] * (2 * smooth(-1, 1, self.scan(u)) - 1)
        breath = c["breathDeg"] * math.sin(2 * math.pi * u) + c["sniffChestDeg"] * sniff
        tremor = self.ctx["motions"]["Idle"].tremor
        cycles, amp = c["tremorCycles"], c["tremorDeg"]
        p.root("Hips", rig.head["Hips"] - UP * c["hipDrop"] + FWD * (c["sniffReach"] * sniff), bow(c["bow"][0]))
        p.rel("Spine", bow(c["bow"][1]))
        p.rel("Chest", bow(c["bow"][2] - breath))
        p.rel("Clavicle.R", lean(-c["shoulderHitch"]))
        p.rel("Clavicle.L", Quaternion())
        p.rel("Neck", rot(pitch=c["neckPitch"] + tremor(u, cycles["neck"], amp["neck"]), yaw=scan * c["neckShare"]))
        self.head_to(p, rot(pitch=c["headPitch"] - c["sniffDeg"] * sniff,
                            roll=c["headRoll"] + tremor(u, cycles["head"], amp["head"]), yaw=scan))
        p.rel("Jaw", bow(c["jawDeg"] + c["sniffJawDeg"] * sniff))
        for s in "LR":
            self.leg(p, s, rig.head["Foot." + s], Quaternion(), self.knees[s])
            wrist = p.point("Chest", rig.tail["Forearm." + s]) + V(*c["handOffset"][s])
            self.miss("Arm." + s, p.ik("UpperArm." + s, "Forearm." + s, wrist, p.delta("Chest").to_3x3() @ self.poles[s]))
            p.rel("Hand." + s, bow(tremor(u, cycles["hand"], amp["hand"])))
            curl = c["fingerCurl"] + c["clenchDeg"] * self.clench(u) if s == "R" else 0.0
            p.rel("Fingers." + s, self.curl("Fingers." + s, curl + tremor(u, cycles["fingers"], amp["fingers"])))
        return p


class Stalking(Motion):
    def __init__(self, rig, ctx, cfg, frames):
        super().__init__(rig, ctx, cfg, frames, cfg["speed"])
        T = self.period
        self.feet = {s: Steps([(st["td"], st["lo"], st) for st in cfg["steps"][s]]) for s in "LR"}
        self.knees = {s: self.rest_pole("Thigh." + s, "Shin." + s) for s in "LR"}
        self.poles = {s: self.rest_pole("UpperArm." + s, "Forearm." + s) for s in "LR"}
        stance = {s: self.grid(lambda u, s=s: 1.0 if self.feet[s].at(u)[0] == "stance" else 0.0) for s in "LR"}
        both = [a * b for a, b in zip(stance["L"], stance["R"])]
        strikes = self.grid(lambda u: sum(st["load"] * pulse(u, st["td"], 0.03) for s in "LR" for st in cfg["steps"][s]))
        self.bob = spring([-cfg["bob"] * d - 0.05 * k for d, k in zip(both, strikes)], T, 2.4, 0.55)
        self.lurch = spring([k for k in strikes], T, 2.8, 0.3)
        side = [(0.7 * (self.foot_x("R") if r and not l else self.foot_x("L") if l and not r else 0.0)) for l, r in zip(stance["L"], stance["R"])]
        self.sway = spring(side, T, 1.6, 0.6)
        self.drop = spring([(1 - l) - (1 - r) for l, r in zip(stance["L"], stance["R"])], T, 3.0, 0.6)
        self.twitch = spring(self.grid(lambda u: sum(sign * window(u, a, a + 0.035, 0.004, 0.06) for a, sign in cfg["twitches"])), T, 5.0, 0.42)
        self.hesitation = spring(self.grid(lambda u: sum(window(u, a, b, 0.018, 0.045) for a, b in cfg["pauses"])), T, 5.5, 0.7)
        self.ahead = spring(self.grid(lambda u: self.foot_z("L", u) - self.foot_z("R", u)), T, 2.5, 0.7)
        arm = rig.tail["Forearm.R"]
        self.raise_r = spring(self.grid(lambda u: tuple(self.torso(Poser(rig), u).point("Chest", arm))), T, 2.2, 0.45)
        shoulder = self.grid(lambda u: tuple(self.torso(Poser(rig), u).head("UpperArm.L")))
        self.shoulder_l = Signal(shoulder)
        self.hang_l = spring(shoulder, T, 0.8, 0.35)

    def foot_x(self, s):
        return self.cfg["steps"][s][0]["x"]

    def foot_z(self, s, u):
        phase, i, t = self.feet[s].at(u)
        st = self.feet[s].steps[i][2]
        if phase == "stance":
            return st["z"] - self.speed * t * (st["lo"] - st["td"]) * self.period
        nxt = self.feet[s].steps[(i + 1) % len(self.feet[s].steps)][2]
        lo_z = st["z"] - self.speed * (st["lo"] - st["td"]) * self.period
        return lo_z + (nxt["z"] - lo_z) * smooth(0, 1, t)

    def torso(self, p, u):
        c = self.cfg
        lurch = self.lurch(u) - 0.45 * self.hesitation(u) + 0.35 * self.twitch(u)
        p.root("Hips", V(c["pelvis"][0] + self.sway(u), c["pelvis"][1] + self.bob(u), c["pelvis"][2] - 0.035 * self.hesitation(u)),
               rot(pitch=c["bow"][0] + c["lurchDeg"] * 0.4 * lurch, roll=c["hipDrop"] * self.drop(u), yaw=-c["hipYaw"] * self.ahead(u)))
        p.rel("Spine", rot(pitch=c["bow"][1] + c["lurchDeg"] * 0.6 * lurch, yaw=0.5 * c["hipYaw"] * self.ahead(u)))
        p.rel("Chest", rot(pitch=c["bow"][2] + c["lurchDeg"] * lurch, yaw=0.6 * c["hipYaw"] * self.ahead(u), roll=-0.5 * c["hipDrop"] * self.drop(u)))
        p.rel("Clavicle.R", lean(-c["shoulderHitch"]))
        p.rel("Clavicle.L", lean(c["shoulderHitch"] * 0.5 + 3 * lurch))
        return p

    def foot_stance(self, s, i, t):
        st = self.feet[s].steps[i][2]
        tip = self.contact(V(st["x"], 0.007, st["z"]), t, st["lo"] - st["td"])
        phi = st["toeIn"] * (1 - smooth(0, 0.25, t)) + st["heelOff"] * smooth(0.55, 1, t)
        q = turn(st["yaw"]) @ bow(phi)
        return tip - q @ self.foot_vec[s], q

    def foot_swing(self, s, i, t, ankle):
        steps = self.feet[s].steps
        st, nxt = steps[i][2], steps[(i + 1) % len(steps)][2]
        phi = st["heelOff"] + (nxt["toeIn"] - st["heelOff"]) * smooth(0, 1, t) + st["swingPitch"] * bump(t)
        yaw = st["yaw"] + (nxt["yaw"] - st["yaw"]) * t
        return ankle + UP * (st["lift"] * bump(t, 2, 2.5)), turn(yaw) @ bow(phi)

    def body(self, u):
        c, rig = self.cfg, self.rig
        p = self.torso(Poser(rig), u)
        tw = self.twitch(u)
        p.rel("Neck", rot(pitch=c["neckDeg"] - 4 * self.lurch(u - 0.025), yaw=5 * tw))
        self.head_to(p, turn(c["scanDeg"] * math.sin(2 * math.pi * u + 0.4) + 6 * tw) @ lean(c["headRoll"] + 14 * tw) @ bow(c["headPitch"] + 8 * abs(tw)))
        p.rel("Jaw", bow(5 + 3 * abs(tw)))
        for s in "LR":
            ankle, q = self.gait_effector(self.feet[s], u, lambda i, t, s=s: self.foot_stance(s, i, t),
                                          lambda i, t, a, s=s: self.foot_swing(s, i, t, a))
            pole = p.delta("Hips").to_3x3() @ self.knees[s]
            self.leg(p, s, ankle, q, pole)
        shoulder = Vector(self.shoulder_l(u))
        lag = self.hang_l(u) - shoulder
        wrist = self.hang_l(u) + V(0, -c["hang"], -c["trail"])
        drift = lag * 1.5
        hand = (-UP + BACK * 0.35 - drift).normalized()
        self.arm(p, "L", wrist, hand, (-UP + BACK * 0.7 - drift * 2).normalized(), BACK + side_out("L") * 0.4)
        self.err = max(self.err, p.ik("UpperArm.R", "Forearm.R", self.raise_r(u), p.delta("Chest").to_3x3() @ self.poles["R"]))
        p.rel("Hand.R", bow(-6 * math.sin(2 * math.pi * 2 * u)))
        p.rel("Fingers.R", self.curl("Fingers.R", c["claw"] + 12 * math.sin(2 * math.pi * 2 * u - 0.8)))
        return p


class Sprinting(Motion):
    def __init__(self, rig, ctx, cfg, frames):
        super().__init__(rig, ctx, cfg, frames, cfg["speed"])
        c, T = cfg, self.period
        self.limbs = {k: Steps([(c["phase"][k], c["phase"][k] + c["duty"][k[0]], None)]) for k in c["phase"]}
        self.flex = loop_curve([tuple(k) for k in c["flex"]])
        self.foot_pitch = loop_curve([tuple(k) for k in c["hind"]["pitch"]])
        self.hand_pitch = loop_curve([tuple(k) for k in c["fore"]["hand"]])
        self.finger_pitch = loop_curve([tuple(k) for k in c["fore"]["fingers"]])
        self.reach = loop_curve([(0.0, 1.0), (c["duty"]["F"], -1.0), (0.55, -0.4), (0.85, 1.0)])
        F = {k: self.grid(lambda u, k=k: self.force(k, u)) for k in c["phase"]}
        hind = [a + b for a, b in zip(F["HL"], F["HR"])]
        fore = [a + b for a, b in zip(F["FL"], F["FR"])]
        g = 9.81
        lh, lf, r = c["bodyHalf"], c["bodyHalf"], c["gyration"]
        acc = [(h + f) * g - g for h, f in zip(hind, fore)]
        tor = [(lh * h - lf * f) * g / (r * r) for h, f in zip(hind, fore)]
        self.heave = Signal(integrate2(acc, T) * c["dynamicGain"])
        self.pitch = Signal(integrate2(tor, T) * c["dynamicGain"])
        surge = [c["surge"] * g * (self.force("HL", u) + self.force("HR", u) - (self.force("FL", u) + self.force("FR", u)) * c["duty"]["H"] / c["duty"]["F"])
                 for u in (k / GRID for k in range(GRID))]
        self.surge = Signal(integrate2(surge, T))

    def force(self, k, u):
        """Fracción del peso que carga la extremidad: media senoide en su apoyo, impulso total de medio cuerpo por par."""
        duty = self.cfg["duty"][k[0]]
        d = (u - self.cfg["phase"][k]) % 1.0
        if d >= duty:
            return 0.0
        return math.pi / (8 * duty) * math.sin(math.pi * d / duty)

    def torso(self, p, u):
        c = self.cfg
        fl = self.flex(u)
        dy, th, dz = self.heave(u), self.pitch(u), self.surge(u)
        pelvis = V(0, c["pelvis"][1] + dy + c["bodyHalf"] * th, c["pelvis"][2] + dz + c["flexReach"] * fl)
        neck_height = c["neckHeight"] + dy - c["bodyHalf"] * th
        yaw = c["twist"] * math.sin(2 * math.pi * (u - 0.05))
        spine = rot(pitch=c["bend"][0] + c["flexDeg"][0] * fl, yaw=-0.5 * yaw)
        chest = rot(pitch=c["bend"][1] + c["flexDeg"][1] * fl, yaw=-0.7 * yaw, roll=c["chestLean"] + c["roll"] * math.sin(2 * math.pi * (u - 0.5)))

        def place(pitch):
            p.root("Hips", pelvis, rot(pitch=pitch, yaw=yaw, roll=0.6 * c["roll"] * math.sin(2 * math.pi * u)))
            p.rel("Spine", spine)
            p.rel("Chest", chest)
            return p.tail("Chest").z - neck_height

        a, b = 60.0, 80.0
        fa, fb = place(a), place(b)
        for _ in range(6):
            m = b - fb * (b - a) / (fb - fa) if abs(fb - fa) > 1e-9 else b
            a, fa, b, fb = b, fb, m, place(m)
        for s, k in (("L", "FL"), ("R", "FR")):
            d = (u - c["phase"][k]) % 1.0
            drop = turn(c["rightDrop"]) if s == "R" else Quaternion()
            p.rel("Clavicle." + s, drop @ lean((1 if s == "L" else -1) * c["protract"] * self.reach(d)))
        return p

    def hind_stance(self, s, t):
        c = self.cfg
        x, z = c["hind"]["x"], c["hind"]["z"]
        tip = self.contact(V(x if s == "R" else -x, 0.007, z), t, c["duty"]["H"])
        q = turn(c["hind"]["yaw"] * (-1 if s == "R" else 1)) @ bow(self.foot_pitch(t * c["duty"]["H"]))
        return tip - q @ self.foot_vec[s], q

    def hind_swing(self, s, t, ankle):
        c = self.cfg
        u = c["duty"]["H"] + t * (1 - c["duty"]["H"])
        q = turn(c["hind"]["yaw"] * (-1 if s == "R" else 1)) @ bow(self.foot_pitch(u))
        return ankle + UP * (c["hind"]["lift"] * bump(t, 1.6, 2.6)) + side_out(s) * (c["hind"]["outward"] * bump(t)), q

    def fore_dirs(self, s, u):
        c = self.cfg["fore"]
        yaw = turn(c["yaw"] * (1 if s == "L" else -1))
        hand = yaw @ bow(self.hand_pitch(u)) @ FWD
        chord = yaw @ bow(self.finger_pitch(u)) @ FWD
        return hand, chord

    def fore_stance(self, s, t):
        c = self.cfg
        x, z = c["fore"]["x"], c["fore"]["z"][s]
        tip = self.contact(V(x if s == "R" else -x, 0.012, z), t, c["duty"]["F"])
        hand, chord = self.fore_dirs(s, t * c["duty"]["F"])
        knuckles = tip - chord * self.chord_len[s]
        return knuckles - hand * self.hand_len[s], hand, chord

    def fore_swing(self, s, t, wrist):
        c = self.cfg
        hand, chord = self.fore_dirs(s, c["duty"]["F"] + t * (1 - c["duty"]["F"]))
        return (wrist + UP * (c["fore"]["lift"] * bump(t, 1.8, 2.2)) + BACK * (c["fore"]["tuck"] * bump(t, 1.5, 3.0))
                + side_out(s) * (c["fore"]["outward"] * bump(t, 1.5, 4.5))), hand, chord

    def body(self, u):
        c, rig = self.cfg, self.rig
        p = self.torso(Poser(rig), u)
        dy = self.heave(u)
        target = V(0, c["head"][1] + c["headBob"] * dy, c["head"][2] + 0.5 * self.surge(u))
        p.aim("Neck", target - p.head("Neck"), p.axis("Chest", 0))
        self.head_to(p, lean(c["headRoll"]) @ bow(c["headPitch"] + 3 * math.sin(2 * math.pi * (u - 0.2))))
        p.rel("Jaw", bow(c["jaw"] + 4 * math.sin(2 * math.pi * (u - 0.35))))
        for s in "LR":
            k = "H" + s
            ankle, q = self.gait_effector(self.limbs[k], u, lambda i, t, s=s: self.hind_stance(s, t),
                                          lambda i, t, a, s=s: self.hind_swing(s, t, a))
            self.leg(p, s, ankle, q, (FWD + UP * 0.25 + side_out(s) * c["knee"]).normalized())
            k = "F" + s
            wrist, hand, chord = self.gait_effector(self.limbs[k], u, lambda i, t, s=s: self.fore_stance(s, t),
                                                    lambda i, t, a, s=s: self.fore_swing(s, t, a))
            self.arm(p, s, wrist, hand, chord, (BACK + UP * 0.3 + side_out(s) * c["elbow"]).normalized())
        return p


class Gesture(Motion):
    def __init__(self, rig, ctx, cfg, frames):
        super().__init__(rig, ctx, cfg, frames, 0.0)
        self.loop = False
        self.lead = ctx["motions"]["Idle"]
        self.idle = self.lead.body(0.0)
        self.walk = ctx["motions"]["Stalking"].body(0.0)
        self.sprint = ctx["motions"]["Sprinting"].body(0.0)
        for p in (self.idle, self.walk, self.sprint):
            p.fill()

    def blend(self, a, b, t, lift=0.0):
        p = Poser(self.rig)
        a.fill()
        b.fill()
        for bone in self.rig.order:
            parent = self.rig.parent[bone]
            ma = a.P[parent].inverted() @ a.P[bone] if parent else a.P[bone]
            mb = b.P[parent].inverted() @ b.P[bone] if parent else b.P[bone]
            m = ma.to_quaternion().slerp(mb.to_quaternion(), t).to_matrix().to_4x4()
            m.translation = ma.translation.lerp(mb.translation, t)
            p.P[bone] = p.P[parent] @ m if parent else m
        for s in "LR":
            foot, hand, fingers = "Foot." + s, "Hand." + s, "Fingers." + s
            ankle = a.head(foot).lerp(b.head(foot), t) + UP * (lift * bump(t))
            q = a.delta(foot).to_quaternion().slerp(b.delta(foot).to_quaternion(), t)
            knee = (a.head("Shin." + s) - a.head("Thigh." + s)).lerp(b.head("Shin." + s) - b.head("Thigh." + s), t)
            self.leg(p, s, ankle, q, knee)
            wrist = a.head(hand).lerp(b.head(hand), t)
            hand_dir = p.axis(hand, 1)
            chord = p.P[fingers].to_3x3() @ self.chord[s]
            pole = (a.head("Forearm." + s) - a.head("UpperArm." + s)).lerp(b.head("Forearm." + s) - b.head("UpperArm." + s), t)
            self.arm(p, s, wrist, hand_dir, chord, pole, p.axis(hand, 0))
        return p

    def crouch(self, height):
        c = self.ctx["crouch"]
        p = Poser(self.rig)
        drop = height - c["pelvis"][1]

        def point(name):
            return V(*c[name]) + self.ctx["shift"] + UP * drop

        p.P["Hips"] = frame(point("pelvis"), point("waist") - point("pelvis"), LEFT)
        p.aim("Spine", point("chest") - point("waist"), LEFT)
        p.aim("Chest", point("neck") - point("chest"), LEFT)
        p.aim("Neck", point("head") - p.head("Neck"), LEFT)
        self.head_to(p, lean(-c["headRollDeg"]) @ bow(c["headPitchDeg"]))
        p.rel("Jaw", bow(8))
        for s in "LR":
            p.aim("Clavicle." + s, point("shoulder" + s) - p.head("Clavicle." + s), FWD)
            foot = "Foot." + s
            self.leg(p, s, self.walk.head(foot), self.walk.delta(foot).to_quaternion(), FWD + side_out(s) * 0.8)
            hand = (FWD - UP * 0.3).normalized()
            tip = V(c["hand" + s][0], 0.012, c["hand" + s][2])
            wrist = tip - FWD * self.chord_len[s] - hand * self.hand_len[s]
            self.arm(p, s, wrist, hand, FWD, BACK + side_out(s) * 0.8)
        p.fill()
        return p


class WindUp(Gesture):
    def __init__(self, rig, ctx, cfg, frames):
        super().__init__(rig, ctx, cfg, frames)
        self.lead = ctx["motions"]["Stalking"]
        self.loaded = self.crouch(cfg["hipHeight"])

    def body(self, u):
        u = clamp(u)
        load = sum(weight * smooth(a, b, u) for a, b, weight in self.cfg["jerks"])
        return self.blend(self.walk, self.loaded, load)


class Recovering(Gesture):
    def __init__(self, rig, ctx, cfg, frames):
        super().__init__(rig, ctx, cfg, frames)
        self.lead = ctx["motions"]["Sprinting"]
        self.brace = self.blend(self.sprint, self.crouch(0.7), 0.65, cfg["braceLift"])
        self.brace.P["Hips"].translation += FWD * cfg["brakeForward"] - UP * cfg["brakeDrop"]
        for bone in rig.order:
            if bone != "Hips":
                self.brace.P[bone].translation += FWD * cfg["brakeForward"] - UP * cfg["brakeDrop"]
        for s in "LR":
            foot = "Foot." + s
            self.leg(self.brace, s, self.sprint.head(foot), self.sprint.delta(foot).to_quaternion(), FWD + side_out(s) * 0.8)

    def wind(self, u):
        return FWD * (self.lead.speed * (1 - smooth(0, 0.45, u)))

    def body(self, u):
        u = clamp(u)
        if u < 0.3:
            return self.blend(self.sprint, self.brace, smooth(0, 0.3, u))
        return self.blend(self.brace, self.walk, smooth(0.3, 1, u), self.cfg["braceLift"])


class Stunned(Gesture):
    def __init__(self, rig, ctx, cfg, frames):
        super().__init__(rig, ctx, cfg, frames)
        self.loop = True
        self.lead = None

    def body(self, u):
        c, rig = self.cfg, self.rig
        p = Poser(rig)
        f = 2 * math.pi * u
        shake = c["tremorDeg"] * (math.sin(11 * f) + 0.35 * math.sin(17 * f))
        p.root("Hips", rig.head["Hips"] + V(c["sway"] * math.sin(f), -0.065 + 0.008 * math.sin(2 * f), 0), rot(5, 3 * math.sin(f)))
        p.rel("Spine", rot(8, -2 * math.sin(f), 3 * math.sin(f - 0.3)))
        p.rel("Chest", rot(-4 + shake * 0.3, 2 * math.sin(f - 0.5), -8))
        p.rel("Neck", rot(-6, shake * 0.3, -8))
        p.rel("Head", rot(-8 + shake, -12 + shake * 0.5, -10))
        p.rel("Jaw", bow(9 + shake))
        p.rel("Clavicle.R", lean(-12))
        p.rel("Clavicle.L", lean(4 * math.sin(f)))
        for s in "LR":
            self.leg(p, s, rig.head["Foot." + s], Quaternion(), FWD + side_out(s) * 0.4)
        face = p.point("Head", self.ctx["face"])
        self.arm(p, "R", face + V(*c["coverOffset"]), LEFT, (LEFT - UP * 0.4).normalized(), -LEFT + FWD)
        wrist = p.head("UpperArm.L") - UP * 0.95 + LEFT * (0.18 + 0.04 * math.sin(f - 0.6))
        self.arm(p, "L", wrist, -UP, (-UP + FWD * 0.3).normalized(), BACK + LEFT)
        return p


class Caught(Gesture):
    def __init__(self, rig, ctx, cfg, frames):
        super().__init__(rig, ctx, cfg, frames)
        self.lead = ctx["motions"]["Sprinting"]
        self.reaching = self.crouch(0.74)

    def wind(self, u):
        return FWD * (self.lead.speed * (1 - smooth(0, 0.35, u)))

    def body(self, u):
        u, c = clamp(u), self.cfg
        reach = smooth(0.02, 0.4, u)
        grip = smooth(0.3, 0.7, u)
        p = self.blend(self.sprint, self.reaching, smooth(0, 0.65, u))
        for s in "LR":
            wrist = self.sprint.head("Hand." + s).lerp(V(c["spread"] if s == "R" else -c["spread"], c["height"], c["reach"] - 0.06 * grip), reach)
            hand = self.sprint.axis("Hand." + s, 1).lerp(FWD, reach).normalized()
            chord = (p.P["Fingers." + s].to_3x3() @ self.chord[s]).lerp(bow(c["clenchDeg"] * grip) @ FWD, reach).normalized()
            pole = (self.sprint.head("Forearm." + s) - self.sprint.head("UpperArm." + s)).lerp(BACK + side_out(s), reach)
            self.arm(p, s, wrist, hand, chord, pole)
        return p


class Landing(Gesture):
    def __init__(self, rig, ctx, cfg, frames):
        super().__init__(rig, ctx, cfg, frames)
        self.lead = None
        self.loaded = self.crouch(0.62)
        self.impact = self.crouch(0.62 - cfg["impact"])

    def body(self, u):
        u = clamp(u)
        if u < 0.16:
            return self.blend(self.loaded, self.impact, smooth(0, 0.16, u))
        return self.blend(self.impact, self.idle, smooth(0.16, 0.92, u), 0.045)


class TakingOff(Gesture):
    def __init__(self, rig, ctx, cfg, frames):
        super().__init__(rig, ctx, cfg, frames)
        self.loaded = self.crouch(0.56)

    def body(self, u):
        u, c = clamp(u), self.cfg
        if u < 0.42:
            return self.blend(self.idle, self.loaded, smooth(0, 0.42, u), 0.045)
        launch = smooth(0.42, 0.7, u)
        p = self.blend(self.loaded, self.idle, launch)
        rise = c["jumpHeight"] * math.sin(math.pi * 0.5 * smooth(0.48, 1, u))
        for bone in self.rig.order:
            p.P[bone].translation += UP * rise
        for s in "LR":
            foot = "Foot." + s
            q = p.delta(foot).to_quaternion()
            ankle = p.head(foot) + (UP * c["tuck"] + BACK * 0.12) * smooth(0.65, 0.95, u)
            self.leg(p, s, ankle, q, FWD + side_out(s) * 0.6)
        return p


CLIPS = {"Idle": Idle, "Stalking": Stalking, "Sprinting": Sprinting, "WindUp": WindUp, "Recovering": Recovering,
         "Stunned": Stunned, "Caught": Caught, "Landing": Landing, "TakingOff": TakingOff, "Suspicious": Suspicious}


def create_motions(rig, ctx, spec):
    ctx["motions"] = motions = {}
    for name, motion_type in CLIPS.items():
        clip = next(c for c in spec["clips"] if c["name"] == name)
        motion = motion_type(rig, ctx, spec["motion"][name], clip["frames"])
        motion.loop = clip["loop"]
        motions[name] = motion
    return motions


class Chains:
    """Pelo y tela: cadenas de partículas con largo fijo, memoria de forma, gravedad, aire y choques."""

    def __init__(self, rig, cfg):
        self.rig, self.cfg = rig, cfg
        self.chains = []
        for name in rig.order:
            if name.startswith(("Hair1.", "Cloth1.")):
                kind = "hair" if name.startswith("Hair") else "cloth"
                side = name.split(".")[1]
                n = 3 if kind == "hair" else 2
                bones = [f"{'Hair' if kind == 'hair' else 'Cloth'}{k + 1}.{side}" for k in range(n)]
                joints = [rig.head[b] for b in bones] + [rig.tail[bones[-1]]]
                self.chains.append((kind, bones, rig.parent[bones[0]], joints))
        self.chains.append(("cloth", ["Shawl"], "Chest", [rig.head["Shawl"], rig.tail["Shawl"]]))

    def colliders(self, p):
        r = self.rig
        caps = [(p.head("Spine"), p.tail("Chest"), 0.13), (p.head("Hips"), p.tail("Hips"), 0.15),
                (p.head("Neck"), p.tail("Neck"), 0.05), (p.point("Head", r.head["Head"] * 0.4 + r.tail["Head"] * 0.6), p.point("Head", r.head["Head"] * 0.4 + r.tail["Head"] * 0.6), 0.13)]
        for s in "LR":
            caps += [(p.head("Thigh." + s), p.tail("Thigh." + s), 0.085), (p.head("Shin." + s), p.tail("Shin." + s), 0.06),
                     (p.head("UpperArm." + s), p.tail("UpperArm." + s), 0.06)]
        return caps

    def step(self, state, p, dt, wind, phase):
        caps = self.colliders(p)
        for (kind, bones, parent, joints), (x, xp) in zip(self.chains, state):
            c = self.cfg[kind]
            goal = [p.point(parent, j) for j in joints]
            x[0] = goal[0]
            xp[0] = goal[0]
            for i in range(1, len(x)):
                vel = (x[i] - xp[i]) / dt
                rel = vel + wind
                acc = GRAVITY * c["gravity"] - rel * c["drag"] - rel * (rel.length * c["quadratic"]) + (goal[i] - x[i]) * (c["stiffness"] * c["falloff"] ** (i - 1))
                if kind == "hair":
                    acc += LEFT * ((goal[i] - x[i]).dot(LEFT) * c["lateralStiffness"] - vel.dot(LEFT) * c["lateralDamping"])
                else:
                    openness = abs(p.axis(parent, 1).dot(FWD))
                    flutter = 1 + c["flutter"] * math.sin(2 * math.pi * (2 * phase - i * 0.22))
                    acc += UP * (c["lift"] * wind.length_squared * openness * flutter)
                nxt = x[i] + (x[i] - xp[i]) * (1 - c["damping"]) + acc * (dt * dt)
                xp[i] = x[i]
                x[i] = nxt
            for _ in range(2):
                for i in range(1, len(x)):
                    length = (joints[i] - joints[i - 1]).length
                    x[i] = x[i - 1] + (x[i] - x[i - 1]).normalized() * length
                    for a, b, rad in caps:
                        ab = b - a
                        t = clamp((x[i] - a).dot(ab) / max(ab.length_squared, 1e-9))
                        q = a + ab * t
                        d = x[i] - q
                        if d.length < rad + c["radius"]:
                            x[i] = q + d.normalized() * (rad + c["radius"])
                    x[i].z = max(x[i].z, c["radius"])

    def simulate(self, motion, times):
        dt = 1.0 / (FPS * SUB)
        T = motion.period
        steps = int(round(T / dt))
        lead = motion if motion.loop else motion.lead
        p = (lead or motion).body(0.0)
        state = [([p.point(parent, j) for j in joints], [p.point(parent, j) for j in joints]) for _, _, parent, joints in self.chains]
        if lead:
            lead_steps = int(round(lead.period / dt))
            warm = max(2, int(math.ceil(self.cfg["settle"] / lead.period))) * lead_steps
            for n in range(warm):
                u = ((n + 1) % lead_steps) / lead_steps
                self.step(state, lead.body(u), dt, lead.wind(u), u)
        else:
            for _ in range(int(self.cfg["settle"] / dt)):
                self.step(state, p, dt, motion.wind(0.0), 0.0)
        marks = {int(round(t / dt)): k for k, t in enumerate(times)}
        recorded = [None] * len(times)
        start = [[v.copy() for v in x] for x, _ in state]
        for k in range(steps + 1):
            if k in marks:
                recorded[marks[k]] = [[v.copy() for v in x] for x, _ in state]
            if k == steps:
                break
            u = (k + 1) / steps
            self.step(state, motion.body(u), dt, motion.wind(u), u)
        end = [[v.copy() for v in x] for x, _ in state]
        if motion.loop:
            for k, t in enumerate(times):
                w = smooth(0, 1, t / T)
                recorded[k] = [[v - (e - s0) * w for v, s0, e in zip(rec, s, en)] for rec, s, en in zip(recorded[k], start, end)]
        return recorded

    def apply(self, p, state):
        for (kind, bones, parent, joints), x in zip(self.chains, state):
            ref = p.delta(parent).to_3x3() @ self.rig.rest[bones[0]].col[0].xyz
            for i, b in enumerate(bones):
                p.aim(b, x[i + 1] - x[i], ref)
                ref = p.axis(b, 0)
