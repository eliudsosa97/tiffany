import json, math, os, random, sys
import bpy, bmesh
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
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


class Pose:
    def __init__(self, name):
        self.name, self.p, self.b = name, SPEC["poses"][name], SPEC["body"]
        self.rng = random.Random(SPEC["seed"])
        self.bm = bmesh.new()
        self.colliders = []

    def j(self, key):
        return Vector(self.p[key])

    def verts(self, points):
        return [self.bm.verts.new(to_blender(p)) for p in points]

    def face(self, verts, mat):
        self.bm.faces.new(verts).material_index = mat

    def tube(self, points, sections, ref, mat, sides=6):
        rings = []
        for k, p in enumerate(points):
            side, fwd = frame((points[min(k + 1, len(points) - 1)] - points[max(k - 1, 0)]).normalized(), ref)
            a, b = sections[k]
            angles = [2 * math.pi * (i + 0.5) / sides for i in range(sides)]
            rings.append(self.verts([p + side * (a * math.cos(w)) + fwd * (b * math.sin(w)) for w in angles]))
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

    def strip(self, anchor, out, length, cfg, mat, clear):
        points = self.rope(anchor, out, length, 6, clear, 0.02)
        n = len(points)
        sections = [(cfg["width"] * 0.5 * (1 - 0.85 * k / n), cfg["thickness"] * 0.5) for k in range(n)]
        self.tube(points, sections, UP.cross(out), mat, sides=cfg["sides"])

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
        self.torso(spine)
        for side in ("L", "R"):
            self.arm(side, spine)
            self.leg(side)
        self.head()
        self.rebozo(shawl)
        self.enagua(spine)
        return self.finish()

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
        self.tube([spline(spine, s) for s in rows], [(along(b["torsoHalfWidth"], s), along(b["torsoHalfDepth"], s)) for s in rows], RIGHT, SKIN, t["sides"])

        def surface(s, phi, sign, out=1.02):
            side, fwd = frame(tangent(spine, s), RIGHT)
            return spline(spine, s) + side * (sign * along(b["torsoHalfWidth"], s) * out * math.sin(phi)) + fwd * (along(b["torsoHalfDepth"], s) * out * math.cos(phi))

        for k in range(t["ribs"]):
            s = lerp(*t["ribRange"], k / (t["ribs"] - 1))
            for sign in (1, -1):
                arc = [surface(s, math.radians(a), sign) - tangent(spine, s) * (t["ribDroop"] * i / 3) for i, a in enumerate(t["ribArcDeg"])]
                self.tube(arc, [(t["ribRadius"],) * 2] * len(arc), tangent(spine, s), SKIN, sides=3)
        sternum = [surface(lerp(*t["sternum"], i / 2), 0, 1) for i in range(3)]
        self.tube(sternum, [(t["sternumRadius"],) * 2] * 3, RIGHT, SKIN, sides=3)
        self.notch = sternum[0]

    def arm(self, side, spine):
        b = self.b
        sh, elbow, wrist, hand = self.arm_joints(side)
        neck = spline(spine, 0.93)
        self.tube([neck, neck.lerp(sh, 0.5) + UP * 0.015, sh], [tuple(x) for x in b["trapezius"]], RIGHT, SKIN)
        r = b["clavicleRadius"]
        tip = sh + FRONT * 0.02
        self.tube([self.notch, self.notch.lerp(tip, 0.5) + FRONT * 0.015 + UP * 0.01, tip], [(r, r)] * 3, UP, SKIN, sides=4)
        self.chain([sh, elbow, wrist], b["armRadius"], SKIN)
        self.hand(wrist, hand, side)

    def hand(self, wrist, knuckles, side):
        h = SPEC["hands"]
        d = Vector(self.p["fingerDir" + side]).normalized()
        spread = d.cross(UP if abs(d.y) < 0.7 else RIGHT).normalized()
        spread = Matrix.Rotation(math.radians(self.p["palmTwistDeg" + side]), 3, d) @ spread
        palm = d.cross(spread)
        self.tube([wrist, wrist.lerp(knuckles, 0.5), knuckles], [tuple(x) for x in h["palm"]], spread, SKIN)
        bend = math.radians(self.p["fingerBendDeg" + side])
        for i, scale in enumerate(h["fingerScale"]):
            o = i - 1.5
            self.finger(knuckles + spread * (o * h["spread"]), tilt(d, spread, math.radians(h["fanDeg"] * o)), spread,
                        [x * h["length"] * scale for x in h["phalanges"]], bend)
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

    def leg(self, side):
        f, j = SPEC["feet"], self.j
        ankle, toe = j("ankle" + side), j("toe" + side)
        self.chain([j("hip" + side), j("knee" + side), ankle], self.b["legRadius"], SKIN)
        fwd = horizontal(toe - ankle, FRONT)
        lat = UP.cross(fwd)
        heel, ball = ankle - fwd * f["heelBack"], toe - fwd * f["ballBack"]
        heel.y, ball.y = f["heelHeight"], f["ballHeight"]
        arch = ankle.lerp(ball, 0.5)
        arch.y = lerp(ankle.y, ball.y, 0.7)
        self.tube([heel, ankle, arch, ball], [tuple(x) for x in f["sections"]], lat, SKIN)
        inner = 1 if side == "L" else -1
        for i, length in enumerate(f["toes"]):
            base = ball + lat * (inner * (2 - i) * f["toeSpread"]) + fwd * 0.01
            tip = base + fwd * length + lat * (inner * (2 - i) * 0.004)
            tip.y = 0.007
            knuckle = base.lerp(tip, 0.45) + UP * 0.006
            r = f["toeRadius"] * (1.4 if i == 0 else 1)
            self.tube([base, knuckle, tip], [(r, r * 0.8), (r * 0.85, r * 0.7), (r * 0.6, r * 0.45)], lat, SKIN, sides=4)

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
        verts = [self.verts([at(p) for p, _ in row]) for row in grid]
        poles = self.verts([at(skull(direction(math.pi / 2, 0))[0]), at(skull(direction(-math.pi / 2, 0))[0])])
        for k in range(seg):
            n = (k + 1) % seg
            self.face((poles[0], verts[0][n], verts[0][k]), SKIN)
            self.face((poles[1], verts[-1][k], verts[-1][n]), SKIN)
            for i in range(rings - 1):
                dent = (grid[i][k][1] + grid[i][n][1] + grid[i + 1][k][1] + grid[i + 1][n][1]) / 4
                self.face((verts[i][k], verts[i][n], verts[i + 1][n], verts[i + 1][k]), HAIR if dent < f["cavity"] else SKIN)
        self.tube([self.j("neck"), c - up * (r[1] * 0.55) - fwd * (r[2] * 0.35)], [(self.b["neckRadius"],) * 2] * 2, RIGHT, SKIN)

        h = SPEC["hair"]

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
            self.strip(anchor, out, length, h, HAIR, 0.08 + 0.06 * front)

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
            return self.rope(p, horizontal(p - center, fwd), length, cfg["rows"], 0.0, 0.01, cfg["drift"]), lateral

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
        zs = [v.co.z for v in mesh.vertices]
        print(f"[Tiffany] {self.name}: {tris} triangulos, {max(zs) - min(zs):.3f} m de alto")
        self.obj, self.shift = obj, Vector((-feet.x, -feet.y, -low))
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


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj)
    make_materials()
    poses = [Pose(name) for name in ("WitchStand", "WitchCrouch")]
    for pose in poses:
        pose.build()
    if "--no-export" not in args:
        os.makedirs(os.path.dirname(FBX), exist_ok=True)
        bpy.ops.export_scene.fbx(filepath=os.path.abspath(FBX), object_types={"MESH"}, axis_forward="-Z", axis_up="Y",
                                 apply_scale_options="FBX_SCALE_ALL", bake_space_transform=True, mesh_smooth_type="FACE",
                                 add_leaf_bones=False, bake_anim=False, use_custom_props=False)
        print(f"[Tiffany] Exportado {os.path.abspath(FBX)}")
    if "--render" in args:
        render(args[args.index("--render") + 1], poses)
    if not bpy.app.background:
        poses[1].obj.location = to_blender(Vector((SPEC["preview"]["crouchOffsetX"], 0, 0)))
        for area in (a for s in bpy.data.screens for a in s.areas if a.type == "VIEW_3D"):
            area.spaces.active.shading.color_type = "MATERIAL"


main()
