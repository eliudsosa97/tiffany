import argparse, json, math, os, random, sys
import bpy, bmesh
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import importlib  # noqa: E402
import witch_rig, witch_motion, witch_video  # noqa: E402
for module in (witch_rig, witch_motion, witch_video):
    importlib.reload(module)
SPEC = json.load(open(os.path.join(HERE, "witch.json")))
FBX = os.path.join(HERE, "..", "..", "Assets", "Art", "Models", "witch.fbx")
UP, RIGHT, FRONT = Vector((0, 1, 0)), Vector((1, 0, 0)), Vector((0, 0, 1))
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


def lerp(a, b, t):
    return a + (b - a) * t


def along(values, s):
    f = min(max(s, 0), 1) * (len(values) - 1)
    i = min(int(f), len(values) - 2)
    return lerp(values[i], values[i + 1], f - i)


def spline(points, s):
    n = len(points) - 1
    f = min(max(s, 0), 1) * n
    i = min(int(f), n - 1)
    t = f - i
    p0, p1, p2, p3 = points[max(i - 1, 0)], points[i], points[i + 1], points[min(i + 2, n)]
    return 0.5 * (2 * p1 + (p2 - p0) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (3 * p1 - p0 - 3 * p2 + p3) * t ** 3)


def tangent(points, s):
    return (spline(points, s + 0.01) - spline(points, s - 0.01)).normalized()


def frame(t, ref):
    side = ref - t * ref.dot(t)
    side = side.normalized() if side.length > 1e-4 else t.orthogonal().normalized()
    return side, side.cross(t)


def tilt(d, toward, angle):
    return (d * math.cos(angle) + toward.normalized() * math.sin(angle)).normalized()


def smoothstep(a, b, x):
    t = min(max((x - a) / (b - a), 0), 1)
    return t * t * (3 - 2 * t)


def knots(x, points):
    """Pesos que pasan de un hueso al siguiente entre nudos (x, hueso) con x ascendente."""
    points = [(x0, {b: 1.0} if isinstance(b, str) else b) for x0, b in points]
    if x <= points[0][0]:
        return dict(points[0][1])
    for (x0, b0), (x1, b1) in zip(points, points[1:]):
        if x <= x1:
            return mix(b0, b1, smoothstep(x0, x1, x))
    return dict(points[-1][1])


def mix(a, b, t):
    out = {k: v * (1 - t) for k, v in a.items()}
    for k, v in b.items():
        out[k] = out.get(k, 0) + v * t
    return out


def blend(parts):
    out = {}
    for w, a in parts:
        for k, v in a.items():
            out[k] = out.get(k, 0) + v * w
    return out


def polyline_weights(points, bones, pre, ends):
    """Pesos de un punto por su proyección sobre una cadena de articulaciones.
    bones[i] va de points[i] a points[i + 1]. ends[i] es el medio ancho de la mezcla en points[i]."""
    lengths = [(b - a).length for a, b in zip(points, points[1:])]
    starts = [sum(lengths[:i]) for i in range(len(lengths))]

    def weights(p):
        best, at = 1e9, 0.0
        for a, b, s0, length in zip(points, points[1:], starts, lengths):
            t = min(max((p - a).dot(b - a) / (length * length), 0), 1)
            d = (a.lerp(b, t) - p).length
            if d < best:
                best, at = d, s0 + t * length
        chain = [(starts[0] + ends[0], bones[0])] if pre is None else [(starts[0] - ends[0], pre), (starts[0] + ends[0], bones[0])]
        for i in range(1, len(bones)):
            chain += [(starts[i] - ends[i], bones[i - 1]), (starts[i] + ends[i], bones[i])]
        return knots(at, chain)
    return weights


class Pose:
    def __init__(self, name):
        self.name, self.p, self.b = name, SPEC["poses"][name], SPEC["body"]
        self.rng = random.Random(SPEC["seed"])
        self.bm = bmesh.new()
        self.colliders = []
        self.weights = []
        self.bind = lambda p: {"Hips": 1.0}
        self.marks = {"strands": [], "cloth": []}

    def j(self, key):
        return Vector(self.p[key])

    def verts(self, points, w=None):
        for k, p in enumerate(points):
            self.weights.append(w[k] if isinstance(w, list) else w if w is not None else self.bind(p))
        return [self.bm.verts.new(to_blender(p)) for p in points]

    def face(self, verts, mat):
        self.bm.faces.new(verts).material_index = mat

    def tube(self, points, sections, ref, mat, sides=6, ring_w=None):
        rings = []
        for k, p in enumerate(points):
            side, fwd = frame((points[min(k + 1, len(points) - 1)] - points[max(k - 1, 0)]).normalized(), ref)
            a, b = sections[k]
            angles = [2 * math.pi * (i + 0.5) / sides for i in range(sides)]
            rings.append(self.verts([p + side * (a * math.cos(w)) + fwd * (b * math.sin(w)) for w in angles], ring_w(k) if ring_w else None))
        self.face(rings[0], mat)
        self.face(rings[-1][::-1], mat)
        for r0, r1 in zip(rings, rings[1:]):
            for i in range(sides):
                self.face((r0[i], r0[(i + 1) % sides], r1[(i + 1) % sides], r1[i]), mat)

    def chain(self, joints, radii, mat, ref=RIGHT, sides=6):
        points, sections = [joints[0]], [(radii[0], radii[0])]
        for k in range(1, len(joints)):
            r = (radii[k - 1] + radii[k]) * 0.5 * self.b["boneThinning"]
            points += [(joints[k - 1] + joints[k]) * 0.5, joints[k]]
            sections += [(r, r), (radii[k], radii[k])]
        self.tube(points, sections, ref, mat, sides)

    def sheet(self, cols, mat, thickness):
        # Grosor y bordes cerrados: Unity descarta la cara trasera de un plano suelto.
        C, R = len(cols), len(cols[0])
        outer, inner = [], []
        for c, col in enumerate(cols):
            offsets = []
            for r, p in enumerate(col):
                n = (cols[min(c + 1, C - 1)][r] - cols[max(c - 1, 0)][r]).cross(col[min(r + 1, R - 1)] - col[max(r - 1, 0)])
                offsets.append(n.normalized() * thickness * 0.5 if n.length > 1e-9 else Vector())
            outer.append(self.verts([p + o for p, o in zip(col, offsets)]))
            inner.append(self.verts([p - o for p, o in zip(col, offsets)]))
        for c in range(C - 1):
            for r in range(R - 1):
                self.face((outer[c][r], outer[c + 1][r], outer[c + 1][r + 1], outer[c][r + 1]), mat)
                self.face((inner[c][r + 1], inner[c + 1][r + 1], inner[c + 1][r], inner[c][r]), mat)
            for r in (0, R - 1):
                self.face((outer[c][r], inner[c][r], inner[c + 1][r], outer[c + 1][r]), mat)
        for c in (0, C - 1):
            for r in range(R - 1):
                self.face((outer[c][r], outer[c][r + 1], inner[c][r + 1], inner[c][r]), mat)

    def collider(self, p0, p1, a, b):
        axis = p1 - p0
        side, fwd = frame(axis.normalized(), RIGHT)
        self.colliders.append((p0, axis.normalized(), axis.length, side, fwd, a, b))

    def push(self, p):
        for p0, axis, length, side, fwd, a, b in self.colliders:
            w = p - p0
            h = w.dot(axis)
            ws, wf = w.dot(side), w.dot(fwd)
            rho = math.hypot(ws / a, wf / b)
            if 0 <= h <= length and 1e-4 < rho < 1:
                p = p + side * (ws / rho - ws) + fwd * (wf / rho - wf)
        return p

    def rope(self, anchor, out, length, steps, clear, wiggle, drift=0.03):
        points = [anchor, self.push(anchor + out * clear - UP * 0.03)]
        step = length / steps
        for _ in range(steps - 1):
            prev = points[-1]
            q = self.push(prev - UP * step + out * (drift / steps) + UP.cross(out) * self.rng.uniform(-wiggle, wiggle))
            q = prev + (q - prev).normalized() * step
            q.y = max(q.y, 0.015)
            points.append(q)
        return points

    def strip(self, anchor, out, length, cfg, mat, clear, arc_w=None):
        points = self.rope(anchor, out, length, 6, clear, 0.02)
        n = len(points)
        sections = [(cfg["width"] * 0.5 * (1 - 0.85 * k / n), cfg["thickness"] * 0.5) for k in range(n)]
        arcs = [sum((b - a).length for a, b in zip(points[:k], points[1:k + 1])) for k in range(n)]
        self.tube(points, sections, UP.cross(out), mat, sides=cfg["sides"], ring_w=(lambda k: arc_w(arcs[k])) if arc_w else None)
        return points

    def panels(self, count, tears, column, closed, thickness):
        cuts = sorted(self.rng.sample(range(2, count - 2), tears))
        edges = list(zip(cuts, cuts[1:] + [cuts[0] + count])) if closed else list(zip([0] + cuts, cuts + [count - 1]))
        for a, z in edges:
            panel = []
            for c in range(a, z + 1):
                col, lateral = column(c % count if closed else c)
                if c in (a, z) and (closed or 0 < c < count - 1):
                    sign = 1 if c == a else -1
                    col = [q + lateral * (sign * max(0, k - len(col) // 2) / len(col)) for k, q in enumerate(col)]
                panel.append(col)
            self.sheet(panel, CLOTH, thickness)

    def build(self):
        j = self.j
        spine = [j(k) for k in ("pelvis", "waist", "chest", "neck")]
        mid = (j("shoulderL") + j("shoulderR")) * 0.5
        shawl = [spine[1], spine[2], mid, spine[3] + (spine[3] - spine[2]).normalized() * 0.06]
        self.add_colliders(spine, shawl)
        self.marks["spine"] = spine
        self.torso(spine)
        for side in ("L", "R"):
            self.arm(side, spine)
            self.leg(side)
        self.head()
        self.bind = self.cloth_weights(spine)
        self.rebozo(shawl)
        self.enagua(spine)
        return self.finish()

    @staticmethod
    def torso_weights(s):
        return knots(s, [(0.22, "Hips"), (0.44, "Spine"), (0.56, "Spine"), (0.76, "Chest")])

    def cloth_weights(self, spine):
        # La tela arriba del anillo sigue al torso. Abajo cuelga de las cadenas Cloth por acimut y profundidad.
        cfg = SPEC["rig"]["cloth"]
        heights = [p.y for p in spine]
        center = spine[0].lerp(spine[1], 0.5)

        def body(p):
            k = min(range(3), key=lambda i: abs(p.y - (heights[i] + heights[i + 1]) / 2))
            t = min(max((p.y - heights[k]) / (heights[k + 1] - heights[k]), 0), 1)
            s = (k + t) / 3
            side = "R" if p.x > center.x else "L"
            b = smoothstep(0.1, 0.22, abs(p.x - center.x)) * smoothstep(0.55, 0.85, s) * 0.7
            return mix(self.torso_weights(s), {"Clavicle." + side: 1.0}, b)

        def weights(p):
            a = math.degrees(math.atan2(p.x - center.x, p.z - center.z))
            chains = {}
            for name, at in cfg["chains"].items():
                gap = abs((a - at + 180) % 360 - 180)
                if gap < 90:
                    chains[name] = 1 - smoothstep(0, 90, gap)
            total = sum(chains.values())
            lower = {n: w / total for n, w in chains.items()}
            d = cfg["anchorHeight"] - p.y
            return knots(d, [(-0.02, body(p)), (0.08, {"Cloth1." + n: w for n, w in lower.items()}),
                             (0.22, {"Cloth1." + n: w for n, w in lower.items()}), (0.36, {"Cloth2." + n: w for n, w in lower.items()})])
        return weights

    def arm_joints(self, side):
        sh, s = self.j("shoulder" + side), self.b["armLengthScale"]
        return [sh] + [sh + (self.j(k + side) - sh) * s for k in ("elbow", "wrist", "hand")]

    def add_colliders(self, spine, shawl):
        b = self.b
        for k in range(3):
            self.collider(spine[k], spine[k + 1], b["torsoHalfWidth"][k] + 0.02, b["torsoHalfDepth"][k] + 0.02)
            self.collider(shawl[k], shawl[k + 1], b["rebozoHalfWidth"][k] + 0.01, b["rebozoHalfDepth"][k] + 0.01)
        for side in ("L", "R"):
            arm = self.arm_joints(side)
            for k in range(2):
                r = b["armRadius"][k] + 0.02
                self.collider(arm[k], arm[k + 1], r, r)
            leg = [self.j(k + side) for k in ("hip", "knee", "ankle")]
            for k in range(2):
                r = b["legRadius"][k] + 0.02
                self.collider(leg[k], leg[k + 1], r, r)

    def torso(self, spine):
        b, t = self.b, SPEC["torso"]
        rows = [k / (t["rows"] - 1) for k in range(t["rows"])]
        self.tube([spline(spine, s) for s in rows], [(along(b["torsoHalfWidth"], s), along(b["torsoHalfDepth"], s)) for s in rows], RIGHT, SKIN, t["sides"],
                  ring_w=lambda k: self.torso_weights(rows[k]))

        def surface(s, phi, sign, out=1.02):
            side, fwd = frame(tangent(spine, s), RIGHT)
            return spline(spine, s) + side * (sign * along(b["torsoHalfWidth"], s) * out * math.sin(phi)) + fwd * (along(b["torsoHalfDepth"], s) * out * math.cos(phi))

        for k in range(t["ribs"]):
            s = lerp(*t["ribRange"], k / (t["ribs"] - 1))
            self.bind = lambda p, s=s: self.torso_weights(s)
            for sign in (1, -1):
                arc = [surface(s, math.radians(a), sign) - tangent(spine, s) * (t["ribDroop"] * i / 3) for i, a in enumerate(t["ribArcDeg"])]
                self.tube(arc, [(t["ribRadius"],) * 2] * len(arc), tangent(spine, s), SKIN, sides=3)
        sternum = [surface(lerp(*t["sternum"], i / 2), 0, 1) for i in range(3)]
        self.tube(sternum, [(t["sternumRadius"],) * 2] * 3, RIGHT, SKIN, sides=3,
                  ring_w=lambda k: self.torso_weights(lerp(*t["sternum"], k / 2)))
        self.notch = sternum[0]

    def arm(self, side, spine):
        b = self.b
        sh, elbow, wrist, hand = self.arm_joints(side)
        neck = spline(spine, 0.93)
        clavicle, upper = "Clavicle." + side, "UpperArm." + side
        trapezius = [{"Chest": 0.7, "Neck": 0.3}, {"Chest": 0.5, clavicle: 0.5}, {clavicle: 0.75, upper: 0.25}]
        self.tube([neck, neck.lerp(sh, 0.5) + UP * 0.015, sh], [tuple(x) for x in b["trapezius"]], RIGHT, SKIN, ring_w=lambda k: trapezius[k])
        r = b["clavicleRadius"]
        tip = sh + FRONT * 0.02
        bone = [{"Chest": 1.0}, {"Chest": 0.4, clavicle: 0.6}, {clavicle: 1.0}]
        self.tube([self.notch, self.notch.lerp(tip, 0.5) + FRONT * 0.015 + UP * 0.01, tip], [(r, r)] * 3, UP, SKIN, sides=4, ring_w=lambda k: bone[k])
        d = Vector(self.p["fingerDir" + side]).normalized()
        reach = SPEC["hands"]["length"] * sum(SPEC["hands"]["phalanges"]) + SPEC["hands"]["nailLength"]
        self.marks["arm" + side] = [sh, elbow, wrist, hand, hand + d * reach]
        self.marks["clavicle" + side] = spline(spine, 0.9).lerp(sh, 0.15)
        self.bind = polyline_weights(self.marks["arm" + side], [upper, "Forearm." + side, "Hand." + side, "Fingers." + side], clavicle, [0.06, 0.07, 0.04, 0.025])
        self.chain([sh, elbow, wrist], b["armRadius"], SKIN)
        self.hand(wrist, hand, side)

    def hand(self, wrist, knuckles, side):
        h = SPEC["hands"]
        d = Vector(self.p["fingerDir" + side]).normalized()
        spread = d.cross(UP if abs(d.y) < 0.7 else RIGHT).normalized()
        spread = Matrix.Rotation(math.radians(self.p["palmTwistDeg" + side]), 3, d) @ spread
        palm = d.cross(spread)
        self.marks["spread" + side] = spread
        self.tube([wrist, wrist.lerp(knuckles, 0.5), knuckles], [tuple(x) for x in h["palm"]], spread, SKIN)
        bend = math.radians(self.p["fingerBendDeg" + side])
        for i, scale in enumerate(h["fingerScale"]):
            o = i - 1.5
            tip = self.finger(knuckles + spread * (o * h["spread"]), tilt(d, spread, math.radians(h["fanDeg"] * o)), spread,
                              [x * h["length"] * scale for x in h["phalanges"]], bend)
            if scale == 1.0:
                self.marks["fingertip" + side] = tip
        sign = -1 if side == "L" else 1
        self.finger(wrist.lerp(knuckles, h["thumbBase"]) + spread * (sign * 0.03) + palm * 0.01, tilt(d, spread * sign, math.radians(h["thumbAngleDeg"])),
                    spread, [x * h["length"] * h["thumbScale"] for x in h["phalanges"][1:]], bend * 0.5)
        if side == "L":
            self.hand_view = knuckles + d * 0.1

    def finger(self, base, d, spread, lengths, bend):
        h = SPEC["hands"]
        r0, r1 = h["radius"]
        points, sections, p = [base], [(r0 * h["knuckle"],) * 2], base
        for k, length in enumerate(lengths):
            d = tilt(d, d.cross(spread), bend)
            r = lerp(r0, r1, (k + 1) / len(lengths))
            points += [p + d * (length * 0.5), p + d * length]
            sections += [(r * 0.65, r * 0.6), (r * h["knuckle"] * 0.85, r)]
            p = points[-1]
        d = tilt(d, d.cross(spread), bend * 0.6)
        points += [p + d * (h["nailLength"] * 0.5), p + d * h["nailLength"]]
        sections += [(r1 * 0.9, r1 * 0.45), (r1 * 0.15, r1 * 0.1)]
        for q in points:
            q.y = max(q.y, 0.006)
        self.tube(points, sections, spread, SKIN, sides=4)
        return points[-1]

    def leg(self, side):
        f, j = SPEC["feet"], self.j
        ankle, toe = j("ankle" + side), j("toe" + side)
        self.bind = polyline_weights([j("hip" + side), j("knee" + side), ankle], ["Thigh." + side, "Shin." + side], "Hips", [0.08, 0.07])
        self.chain([j("hip" + side), j("knee" + side), ankle], self.b["legRadius"], SKIN)
        foot = "Foot." + side
        fwd = horizontal(toe - ankle, FRONT)
        lat = UP.cross(fwd)
        heel, ball = ankle - fwd * f["heelBack"], toe - fwd * f["ballBack"]
        heel.y, ball.y = f["heelHeight"], f["ballHeight"]
        arch = ankle.lerp(ball, 0.5)
        arch.y = lerp(ankle.y, ball.y, 0.7)
        self.tube([heel, ankle, arch, ball], [tuple(x) for x in f["sections"]], lat, SKIN,
                  ring_w=lambda k: {foot: 0.75, "Shin." + side: 0.25} if k == 1 else {foot: 1.0})
        self.marks["ball" + side] = ball
        self.bind = lambda p: {foot: 1.0}
        inner = 1 if side == "L" else -1
        for i, length in enumerate(f["toes"]):
            base = ball + lat * (inner * (2 - i) * f["toeSpread"]) + fwd * 0.01
            tip = base + fwd * length + lat * (inner * (2 - i) * 0.004)
            tip.y = 0.007
            knuckle = base.lerp(tip, 0.45) + UP * 0.006
            r = f["toeRadius"] * (1.4 if i == 0 else 1)
            self.tube([base, knuckle, tip], [(r, r * 0.8), (r * 0.85, r * 0.7), (r * 0.6, r * 0.45)], lat, SKIN, sides=4)
            if i == 0:
                self.marks["toe" + side] = tip

    def head(self):
        rot = Matrix.Rotation(math.radians(self.p["headRollDeg"]), 3, "Z") @ Matrix.Rotation(math.radians(self.p["headPitchDeg"]), 3, "X")
        up, fwd = rot @ UP, rot @ FRONT
        side = up.cross(fwd)
        c, r, f = self.j("head"), self.b["headRadius"], SPEC["face"]
        self.face_view = (c, fwd)

        def at(v):
            return c + side * v.x + up * v.y + fwd * v.z

        def direction(lat, lon):
            return Vector((math.sin(lon) * math.cos(lat), math.sin(lat), math.cos(lon) * math.cos(lat)))

        def skull(d, features=True):
            dent = dark = 0
            for ft in f["features"] if features else []:
                (cx, cy, cz), (sx, sy, sz) = ft["center"], ft["size"]
                for m in ((1, -1) if cx else (1,)):
                    g = ft["amount"] * math.exp(-((d.x - m * cx) / sx) ** 2 - ((d.y - cy) / sy) ** 2 - ((d.z - cz) / sz) ** 2)
                    dent += g
                    dark += g if ft.get("dark") else 0
            low, front = max(-d.y, 0), max(d.z, 0)
            v = Vector((d.x * r[0] * (1 - f["jawNarrow"] * low),
                        d.y * r[1] - r[1] * f["jawLength"] * low * low * (0.4 + 0.6 * front),
                        d.z * r[2] * (f["backOfSkull"] if d.z < 0 else 1) + r[2] * f["chinForward"] * low * front))
            return v * (1 + dent), dark

        seg, rings = f["segments"], f["rings"]
        lons = [math.pi * math.copysign(abs(u) ** f["frontDensity"], u) for u in (-1 + 2 * k / seg for k in range(seg))]
        lats = [math.pi / 2 - math.pi * (i + 1) / (rings + 1) for i in range(rings)]
        grid = [[skull(direction(lat, lon)) for lon in lons] for lat in lats]

        def jaw(d):
            w = smoothstep(-0.2, -0.55, d.y) * smoothstep(-0.35, 0.15, d.z)
            return {"Head": 1 - w, "Jaw": w}

        verts = [self.verts([at(p) for p, _ in row], [jaw(direction(lat, lon)) for lon in lons]) for row, lat in zip(grid, lats)]
        poles = self.verts([at(skull(direction(math.pi / 2, 0))[0]), at(skull(direction(-math.pi / 2, 0))[0])],
                           [{"Head": 1.0}, jaw(direction(-math.pi / 2, 0))])
        for k in range(seg):
            n = (k + 1) % seg
            self.face((poles[0], verts[0][n], verts[0][k]), SKIN)
            self.face((poles[1], verts[-1][k], verts[-1][n]), SKIN)
            for i in range(rings - 1):
                dent = (grid[i][k][1] + grid[i][n][1] + grid[i + 1][k][1] + grid[i + 1][n][1]) / 4
                self.face((verts[i][k], verts[i][n], verts[i + 1][n], verts[i + 1][k]), HAIR if dent < f["cavity"] else SKIN)
        base = c - up * (r[1] * 0.55) - fwd * (r[2] * 0.35)
        neck = [{"Chest": 0.5, "Neck": 0.5}, {"Neck": 1.0}, {"Neck": 0.3, "Head": 0.7}]
        self.tube([self.j("neck"), self.j("neck").lerp(base, 0.5), base], [(self.b["neckRadius"],) * 2] * 3, RIGHT, SKIN, ring_w=lambda k: neck[k])
        self.marks.update(headCenter=c, headUp=up, headFront=fwd, headBase=base, headTop=c + up * r[1],
                          jaw=(c - up * (r[1] * 0.3) - fwd * (r[2] * 0.1), c - up * (r[1] * 1.05) + fwd * (r[2] * 0.75)))
        self.bind = lambda p: {"Head": 1.0}

        h = SPEC["hair"]
        chains = SPEC["rig"]["hair"]["chains"]

        def hair_weights(theta):
            near = {}
            for name, at_deg in chains.items():
                gap = abs((math.degrees(theta) - at_deg + 180) % 360 - 180)
                if gap < 130:
                    near[name] = 1 - gap / 130
            total = sum(near.values())
            near = {n: w / total for n, w in near.items()}
            return lambda s: blend([(w, knots(s, [(0.0, "Head"), (0.08, f"Hair1.{n}"), (0.22, f"Hair1.{n}"), (0.34, f"Hair2.{n}"),
                                                  (0.5, f"Hair2.{n}"), (0.62, f"Hair3.{n}")])) for n, w in near.items()]), near

        def hairline(lon):
            return math.asin(lerp(h["hairlineFront"], h["hairlineBack"], (1 - math.cos(lon)) / 2))

        def scalp(lat, lon):
            return at(skull(direction(lat, lon), False)[0] * h["capScale"] + Vector((0, h["capLift"] * max(0, math.sin(lat)), 0)))

        cap = []
        for k in range(h["capColumns"] + 1):
            lon = -math.pi + 2 * math.pi * k / h["capColumns"]
            cap.append([scalp(lerp(math.radians(84), hairline(lon), i / (h["capRows"] - 1)), lon) for i in range(h["capRows"])])
        self.sheet(cap, HAIR, 0.01)

        gap, gap_center = math.radians(h["faceGapDeg"]) / 2, math.radians(h["faceGapOffsetDeg"])
        for i in range(h["strands"]):
            theta = 2 * math.pi * i / h["strands"] + self.rng.uniform(-0.12, 0.12)
            el = self.rng.uniform(0.2, 1.0)
            off = (theta - gap_center + math.pi) % (2 * math.pi) - math.pi
            if abs(off) < gap and i % h["strayEvery"]:
                theta = gap_center + math.copysign(gap + abs(off) * 0.8, off)
                off = math.copysign(gap, off)
            el = max(el, hairline(theta) + 0.05)
            anchor = scalp(el, theta)
            length = self.rng.uniform(*h["frontLength" if math.cos(theta) > 0.3 else "backLength"])
            front = max(0, math.cos(off)) * h["parting"]
            out = horizontal((anchor - c).normalized().lerp(side * math.copysign(1, off), front), fwd)
            arc_w, near = hair_weights(theta)
            self.marks["strands"].append((near, self.strip(anchor, out, length, h, HAIR, 0.08 + 0.06 * front, arc_w)))

    def rebozo(self, shawl):
        b, cfg, rags = self.b, SPEC["rebozo"], SPEC["rebozoRags"]
        shoulder = (self.j("shoulderR") - self.j("shoulderL")).normalized()

        def shell(u, s):
            t = tangent(shawl, s)
            side, fwd = frame(t, RIGHT)
            side = side.lerp(shoulder, max(0, (s - cfg["tiltFrom"]) / (1 - cfg["tiltFrom"]))).normalized()
            opening = math.radians(lerp(cfg["openBottomDeg"], cfg["openTopDeg"], s))
            phi = opening + (2 * math.pi - 2 * opening) * u
            fold = 1 + cfg["foldDepth"] * (1 - s) * math.sin(cfg["folds"] * phi)
            a, d = along(b["rebozoHalfWidth"], s) * fold, along(b["rebozoHalfDepth"], s) * fold
            return spline(shawl, s) + side * (a * math.sin(phi)) + fwd * (d * math.cos(phi)), phi

        bottom = spline(shawl, 0)
        rows, count = cfg["rows"], cfg["columns"]
        self.marks["shawl"] = [shell(0.5, 1)[0], shell(0.5, 0)[0]]
        body_weights = self.bind

        def shawl_weights(p):
            height = smoothstep(bottom.y, shawl[-1].y, p.y)
            back = 1 - smoothstep(-0.1, 0.04, p.z - spline(shawl, height).z)
            release = (1 - smoothstep(0.6, 1.0, height)) * smoothstep(bottom.y - 0.3, bottom.y, p.y)
            return mix(body_weights(p), {"Shawl": 1.0}, back * release * 0.9)

        self.bind = shawl_weights

        def column(c):
            u = c / (count - 1)
            p, phi = shell(u, 0)
            length = lerp(cfg["frontLength"], cfg["backLength"], (1 - math.cos(phi)) / 2) * self.rng.uniform(cfg["ragged"], 1)
            hang = self.rope(p, horizontal(p - bottom, FRONT), length, cfg["hangRows"], 0.0, 0.01)[1:]
            lateral = (shell(min(u + 0.01, 1), 0)[0] - shell(max(u - 0.01, 0), 0)[0]).normalized() * cfg["tearGap"]
            return [shell(u, 1 - k / (rows - 1))[0] for k in range(rows)] + hang, lateral

        self.panels(count, cfg["tears"], column, False, cfg["thickness"])
        knobs = cfg["vertebrae"] * 2 + 1
        spine = [lerp(cfg["spineFrom"], cfg["spineTo"], k / (knobs - 1)) for k in range(knobs)]
        points = [shell(0.5, s)[0] for s in spine]
        points = [p + (p - spline(shawl, s)).normalized() * 0.004 for p, s in zip(points, spine)]
        self.tube(points, [(cfg["knob"][k % 2],) * 2 for k in range(knobs)], RIGHT, CLOTH, sides=4)
        for _ in range(rags["chestStrips"]):
            p = shell(self.rng.uniform(0, 1), self.rng.uniform(0, rags["chestHeight"]))[0]
            self.strip(p, horizontal(p - bottom, FRONT), self.rng.uniform(*rags["chestLength"]), rags, CLOTH, 0.02)
        self.bind = body_weights

    def enagua(self, spine):
        b, cfg, rags = self.b, SPEC["enagua"], SPEC["rebozoRags"]
        center = spine[0].lerp(spine[1], cfg["waist"])
        side, fwd = frame((spine[1] - spine[0]).normalized(), RIGHT)
        a = lerp(b["torsoHalfWidth"][0], b["torsoHalfWidth"][1], cfg["waist"]) + cfg["clearance"]
        d = lerp(b["torsoHalfDepth"][0], b["torsoHalfDepth"][1], cfg["waist"]) + cfg["clearance"]
        count = cfg["columns"]

        def ring(phi):
            fold = 1 + cfg["foldDepth"] * math.sin(cfg["folds"] * phi)
            return center + side * (a * fold * math.sin(phi)) + fwd * (d * fold * math.cos(phi))

        def column(c):
            phi = 2 * math.pi * c / count
            p = ring(phi)
            length = cfg["length"] * self.rng.uniform(cfg["ragged"], 1)
            lateral = (side * (a * math.cos(phi)) - fwd * (d * math.sin(phi))).normalized() * cfg["tearGap"]
            rope = self.rope(p, horizontal(p - center, fwd), length, cfg["rows"], 0.0, 0.01, cfg["drift"])
            self.marks["cloth"].append((center, rope))
            return rope, lateral

        self.panels(count, cfg["tears"], column, True, cfg["thickness"])
        for _ in range(rags["hipStrips"]):
            p = ring(self.rng.uniform(0, 2 * math.pi))
            self.strip(p, horizontal(p - center, fwd), self.rng.uniform(*rags["hipLength"]), rags, CLOTH, 0.02)

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
        if tris > 6000:
            raise ValueError("[Tiffany] La malla supera los 6000 triángulos.")
        zs = [v.co.z for v in mesh.vertices]
        print(f"[Tiffany] {self.name}: {tris} triangulos, {max(zs) - min(zs):.3f} m de alto")
        self.obj, self.shift = obj, Vector((-feet.x, -feet.y, -low))
        return obj

    def place(self, p):
        return to_blender(p) + self.shift


def make_materials():
    for m in SPEC["materials"]:
        h = m["color"].lstrip("#")
        rgb = [linear(int(h[i:i + 2], 16) / 255) for i in (0, 2, 4)]
        mat = bpy.data.materials.new(m["name"])
        mat.diffuse_color = (*rgb, 1)
        bsdf = mat.node_tree.nodes["Principled BSDF"]
        bsdf.inputs["Base Color"].default_value = (*rgb, 1)
        bsdf.inputs["Roughness"].default_value = 1 - m["smoothness"]
        bsdf.inputs["Metallic"].default_value = m["metallic"]


def add(name, data, pos, target=None):
    obj = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = to_blender(pos)
    if target is not None:
        obj.rotation_euler = (to_blender(target) - obj.location).to_track_quat("-Z", "Y").to_euler()
    return obj


def render(folder, poses):
    scene, pv = bpy.context.scene, SPEC["preview"]
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
    offsets = {"front": Vector((0, 0.25, 7)), "threequarter": Vector((math.sin(az) * math.cos(el), math.sin(el), math.cos(az) * math.cos(el))) * 8, "profile": Vector((7, 0.25, 0))}
    for pose in poses:
        shift = Vector((-pose.shift.x, pose.shift.z, -pose.shift.y))
        target = Vector((0, 1.1 if pose.name == "WitchStand" else 0.65, 0.15))
        views = {k: (target, target + o) for k, o in offsets.items()}
        face, fwd = pose.face_view[0] + shift, pose.face_view[1]
        views["face"] = (face, face + (horizontal(fwd, FRONT) + UP * 0.15).normalized() * pv["faceDistance"])
        hand = pose.hand_view + shift
        views["hand"] = (hand, hand + Vector(pv["handCamera"]).normalized() * pv["handDistance"])
        for other in poses:
            other.obj.hide_render = other is not pose
        for view, (look, eye) in views.items():
            cam.location = to_blender(eye)
            cam.rotation_euler = (to_blender(look) - cam.location).to_track_quat("-Z", "Y").to_euler()
            scene.render.filepath = os.path.join(folder, f"{pose.name}_{view}.png")
            bpy.ops.render.render(write_still=True)


def context(pose, rig):
    m = pose.marks
    side = m["headUp"].cross(m["headFront"])
    skull = Matrix((to_blender(-side), to_blender(m["headUp"]), to_blender(m["headFront"]))).transposed()
    upright = Matrix((witch_rig.LEFT, witch_rig.UP, witch_rig.FWD)).transposed()
    return {"skull": (skull @ upright.inverted()).to_quaternion(),
            "toe": {s: pose.place(m["toe" + s]) for s in "LR"}, "fingertip": {s: pose.place(m["fingertip" + s]) for s in "LR"},
            "sole": {s: [v.co - rig.head["Foot." + s] for v, w in zip(pose.obj.data.vertices, pose.weights)
                         if w.get("Foot." + s, 0) >= 0.5] for s in "LR"},
            "crouch": SPEC["poses"]["WitchCrouch"], "shift": pose.shift, "face": pose.place(m["headCenter"])}


def animate(pose, only):
    table = witch_rig.bone_table(pose, pose.place, to_blender, SPEC["rig"])
    arm = witch_rig.make_armature(table)
    witch_rig.skin(pose.obj, arm, pose.weights)
    rig = witch_rig.Rig(arm)
    ctx = context(pose, rig)
    chains = witch_motion.Chains(rig, SPEC["rig"]["sim"])
    motions = witch_motion.create_motions(rig, ctx, SPEC)
    actions = []
    for clip in SPEC["clips"]:
        if only and clip["name"] not in only:
            continue
        motion = motions[clip["name"]]
        samples = motion.run(chains, clip["step"])
        actions.append(witch_rig.bake(arm, rig, clip["name"], samples, clip["loop"]))
        print(f"[Tiffany] {clip['name']}: {clip['frames']} cuadros, {motion.period:.2f} s, error de IK {motion.err * 100:.1f} cm")
    witch_rig.add_strips(arm, actions)
    print(f"[Tiffany] Esqueleto: {len(arm.data.bones)} huesos")
    return arm, actions


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--video", metavar="FOLDER")
    parser.add_argument("--stills", nargs="?", const=os.path.join(HERE, "..", "..", "Builds", "WitchStills"), metavar="FOLDER")
    parser.add_argument("--clips")
    parser.add_argument("--render", metavar="FOLDER")
    parser.add_argument("--no-export", action="store_true")
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
    only = args.clips.split(",") if args.clips else None
    if only and set(only) - {c["name"] for c in SPEC["clips"]}:
        parser.error("--clips contiene un nombre que no está en witch.json")
    scene = bpy.context.scene
    scene.render.fps, scene.render.fps_base = witch_motion.FPS, 1.0
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj)
    make_materials()
    pose = Pose("WitchStand")
    pose.build()
    pose.obj.name = pose.obj.data.name = "Witch"
    arm, actions = animate(pose, only)
    if not args.no_export:
        os.makedirs(os.path.dirname(FBX), exist_ok=True)
        witch_rig.export(os.path.abspath(FBX), arm, pose.obj)
        print(f"[Tiffany] Exportado {os.path.abspath(FBX)}")
    for track in arm.animation_data.nla_tracks:
        track.mute = True
    if args.render:
        os.makedirs(args.render, exist_ok=True)
        arm.data.pose_position = "REST"
        render(args.render, [pose])
        arm.data.pose_position = "POSE"
    speeds = {name: SPEC["motion"][name].get("speed", 0.0) for name in SPEC["motion"]}
    if args.stills:
        witch_video.stills(args.stills, arm, actions, {"Idle": 10, "Stalking": 12, "Sprinting": 12, "WindUp": 10, "Recovering": 10, "Suspicious": 10})
    if args.video:
        witch_video.videos(args.video, arm, actions, speeds)
    if not bpy.app.background:
        arm.animation_data.action = actions[0]
        bpy.context.scene.frame_start, bpy.context.scene.frame_end = 0, int(actions[0].frame_end)
        scene.frame_set(0)
        for area in (a for s in bpy.data.screens for a in s.areas if a.type == "VIEW_3D"):
            area.spaces.active.shading.color_type = "MATERIAL"


main()
