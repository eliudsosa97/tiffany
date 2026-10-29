import math

from mathutils import Vector

from lowpoly import Mesh


def for_export(meshes):
    # La conversión de ejes a Blender refleja las caras de estas piezas.
    for mesh in meshes:
        mesh.faces = [tuple(reversed(face)) for face in mesh.faces]
    return meshes


def noise(x, y, z, seed):
    cell = [math.floor(value) for value in (x, y, z)]
    blend = [value - base for value, base in zip((x, y, z), cell)]
    blend = [value * value * (3 - 2 * value) for value in blend]
    result = 0
    for dx in (0, 1):
        for dy in (0, 1):
            for dz in (0, 1):
                value = ((cell[0] + dx) * 73856093 ^ (cell[1] + dy) * 19349663
                         ^ (cell[2] + dz) * 83492791 ^ seed * 2654435761) & 0xffffffff
                value = ((value ^ (value >> 13)) * 1274126177) & 0xffffffff
                weight = math.prod(t if bit else 1 - t for t, bit in zip(blend, (dx, dy, dz)))
                result += (((value ^ (value >> 16)) & 65535) / 32767.5 - 1) * weight
    return result


def stone(mesh, spec, rng, center=(0, 0, 0), materials=("RiverGraniteDry", "RiverGraniteWet")):
    sides, rows = spec.get("sides", 12), spec.get("rings", 12)
    seed = rng.randrange(65536)
    points = [(0, -1, 0)]
    for row in range(1, rows + 1):
        latitude = -math.pi / 2 + math.pi * row / (rows + 1)
        for side in range(sides):
            angle = math.tau * side / sides + rng.uniform(-0.1, 0.1)
            points.append((math.cos(latitude) * math.cos(angle), math.sin(latitude),
                           math.cos(latitude) * math.sin(angle)))
    points.append((0, 1, 0))
    shaped = []
    for x, y, z in points:
        radius = 1 + 0.24 * noise(x * 1.4, y * 1.4, z * 1.4, seed)
        radius += 0.065 * noise(x * 5.3, y * 5.3, z * 5.3, seed + 17)
        x, y, z = x * radius, max(-0.72, y * radius), z * radius
        lean = spec.get("lean", (0.12, -0.05))
        x, z = x + lean[0] * y, z + lean[1] * y
        if "splitPlane" in spec:
            x = min(x, spec["splitPlane"] + max(0, -y - 0.2) * 2 - 0.22 * z)
        shaped.append((x, y, z))
    low = [min(p[i] for p in shaped) for i in range(3)]
    span = [max(p[i] for p in shaped) - low[i] for i in range(3)]
    indices = []
    for point in shaped:
        scaled = [(point[i] - low[i]) / span[i] * spec["size"][i] for i in range(3)]
        scaled[0] -= spec["size"][0] / 2
        scaled[1] -= spec.get("burial", 0)
        scaled[2] -= spec["size"][2] / 2
        indices.append(mesh.vertex(tuple(scaled[i] + center[i] for i in range(3))))
    rings = [indices[1 + row * sides:1 + (row + 1) * sides] for row in range(rows)]
    waterline = center[1] - spec.get("burial", 0) + spec["size"][1] * 0.29

    def face(vertices):
        wet = sum(mesh.vertices[i].y for i in vertices) / len(vertices) < waterline
        mesh.face(vertices, materials[int(wet)])

    for side in range(sides):
        next_side = (side + 1) % sides
        face((indices[0], rings[0][side], rings[0][next_side]))
        face((indices[-1], rings[-1][next_side], rings[-1][side]))
    for row, (lower, upper) in enumerate(zip(rings, rings[1:])):
        for side in range(sides):
            next_side = (side + 1) % sides
            a, b, c, d = lower[side], upper[side], upper[next_side], lower[next_side]
            if (row + side) % 2:
                face((a, b, d))
                face((b, c, d))
            else:
                face((a, b, c))
                face((a, c, d))


def stepping_stone(mesh, spec, rng):
    sides = spec["sides"]
    width, height, depth = spec["size"]
    phase = rng.uniform(0, math.tau)
    outline = []
    for side in range(sides):
        angle = side * math.tau / sides + rng.uniform(-0.035, 0.035)
        radius = 1 + 0.035 * math.sin(2 * angle + phase) + 0.02 * math.cos(3 * angle - phase)
        radius += rng.uniform(-0.008, 0.008)
        if "notchAngle" in spec:
            delta = math.atan2(math.sin(angle - spec["notchAngle"]), math.cos(angle - spec["notchAngle"]))
            radius -= 0.16 * math.exp(-(delta / 0.28) ** 2)
        outline.append((math.cos(angle) * radius, math.sin(angle) * radius))
    low = [min(p[i] for p in outline) for i in range(2)]
    span = [max(p[i] for p in outline) - low[i] for i in range(2)]
    outline = [(((x - low[0]) / span[0] - 0.5) * width,
                ((z - low[1]) / span[1] - 0.5) * depth) for x, z in outline]
    tilt = [math.tan(math.radians(value)) for value in spec["topTiltDegrees"]]

    def top(x, z):
        return height + x * tilt[0] + z * tilt[1] + 0.0016 * math.sin(x * 4 + phase) * math.cos(z * 4)

    rings = []
    for level, radius in [(0, 0.97), (0.12, 1), (0.38, 0.98), (0.66, 0.94),
                          (0.88, 0.90), (0.97, 0.85), (1, 0.78), (1, 0.39)]:
        ring = []
        for side, (x, z) in enumerate(outline):
            rough = 1 + (rng.uniform(-0.008, 0.008) if 0.12 < level < 0.66 else 0)
            x, z = x * radius * rough, z * radius * rough
            y = top(x, z) - height * (1 - level) if level >= 0.66 else height * level
            if 0.12 < level < 0.66:
                y += 0.005 * math.sin(side * 1.7 + level * 9 + phase)
            ring.append(mesh.vertex((x, y, z)))
        rings.append(ring)
    for row, (lower, upper) in enumerate(zip(rings, rings[1:])):
        material = "RiverGraniteWet" if row < 2 else "RiverGraniteDry"
        for side in range(sides):
            next_side = (side + 1) % sides
            a, b, c, d = lower[side], upper[side], upper[next_side], lower[next_side]
            mesh.face((a, b, c), material)
            mesh.face((a, c, d), material)
    bottom, crown = mesh.vertex((0, 0, 0)), mesh.vertex((0, top(0, 0), 0))
    for side in range(sides):
        next_side = (side + 1) % sides
        mesh.face((bottom, rings[0][side], rings[0][next_side]), "RiverGraniteWet")
        mesh.face((crown, rings[-1][next_side], rings[-1][side]), "RiverGraniteDry")


def build_rock(spec, rng):
    mesh = Mesh("Stone")
    if spec.get("shape") == "step":
        stepping_stone(mesh, spec, rng)
    else:
        stone(mesh, spec, rng)
    return for_export([mesh])


def bank_height(inset, spec):
    t = min(1, max(0, inset / spec["slopeWidth"]))
    return -spec["depth"] * t * t * (3 - 2 * t)


def bend(point, radius):
    x, y, z = point
    if radius == 0:
        return x, y, z
    angle = z / radius
    return radius * (1 - math.cos(angle)) + x * math.cos(angle), y, (radius - x) * math.sin(angle)


def edge_shift(along, spec):
    envelope = min(1, along * 2, (spec["length"] - along) * 2)
    return 0.25 * envelope * math.tanh(2.5 * noise(along * 0.85, 0, 0, spec["seed"]))


def bank_lift(inset, along, spec):
    envelope = min(1, along * 2, (spec["length"] - along) * 2)
    edge = -0.26 + edge_shift(along, spec)
    ridge = max(0, 1 - abs(inset - edge) / 0.18)
    relief = 0.055 + 0.025 * math.tanh(2 * noise(along * 1.2, 0, 0, spec["seed"] + 9))
    return spec["lift"] + envelope * ridge * (relief - spec["lift"])


def build_bank(spec, rng):
    soil = Mesh("BankProfile", recalculate_normals=False)
    stones, roots, lip = (Mesh(name) for name in ("BankStones", "BankRoots", "BankErosion"))
    columns = [-spec["outside"], -0.75, -0.26, 0] + spec["profileInsets"] + [spec["inside"]]
    rows = []
    for row in range(spec["segments"] + 1):
        along = spec["length"] * row / spec["segments"]
        ring = []
        for column, inset in enumerate(columns):
            if column == 2:
                inset += edge_shift(along, spec)
            lift = bank_lift(inset, along, spec)
            z = along
            if 0 < row < spec["segments"]:
                rough = rng.uniform(0.02, 0.04)
                z += rng.choice((-1, 1)) * rough
                if 0 < column < len(columns) - 1 and column != 3:
                    inset += rng.uniform(-0.008, 0.008)
                lift = max(0.018, lift + rng.uniform(-rough, rough))
            if column == 2 and spec["erosionStart"] <= along <= spec["erosionEnd"]:
                lift = 0.018
            ring.append(soil.vertex((inset, bank_height(inset, spec) + lift, z)))
        rows.append(ring)
    for row, (lower, upper) in enumerate(zip(rows, rows[1:])):
        for column in range(len(columns) - 1):
            a, b, c, d = lower[column], upper[column], upper[column + 1], lower[column + 1]
            faces = ((a, b, c), (a, c, d)) if (row + column) % 2 else ((a, b, d), (b, c, d))
            for face in faces:
                wet = sum(soil.vertices[i].y for i in face) / 3 < spec["waterLevel"] + 0.1
                soil.face(face, "RiverBankWetEarth" if wet else "RiverBankEarth")
    low, high = 0, spec["slopeWidth"]
    for _ in range(24):
        middle = (low + high) / 2
        if bank_height(middle, spec) + spec["lift"] > spec["waterLevel"]:
            low = middle
        else:
            high = middle
    cluster = rng.uniform(2, spec["length"] - 2)
    positions = [cluster + offset for offset in (-0.34, -0.10, 0.23, 0.48)]
    for _ in range(512):
        if len(positions) == spec["stoneCount"]:
            break
        along = rng.uniform(0.35, spec["length"] - 0.35)
        if all(abs(along - other) > 0.48 for other in positions):
            positions.append(along)
    if len(positions) != spec["stoneCount"]:
        raise ValueError("No caben las piedras con la separación requerida.")
    for along in positions:
        scale = rng.uniform(0.12, 0.45)
        size = (scale, scale * rng.uniform(0.6, 0.85), scale * rng.uniform(0.7, 1))
        inset = (low + high) / 2 + rng.uniform(-0.025, 0.025) if rng.random() < 0.5 else rng.uniform(-0.08, 0.17)
        center = (inset, bank_height(inset, spec) + bank_lift(inset, along, spec) - size[1] * 0.5, along)
        stone(stones, {"size": size, "sides": 6, "rings": 4}, rng, center,
              ("RiverBankStone", "RiverBankStone"))
    for along in spec["roots"]:
        edge = -0.26 + edge_shift(along, spec)
        start = bank_lift(edge, along, spec)
        points = [(edge - 0.05, start - 0.02, along), (edge + 0.04, start + 0.055, along + 0.08),
                  (0.10, 0.06, along + 0.19), (0.23, -0.08, along + 0.16),
                  (0.30, -0.29, along + 0.24), (0.36, bank_height(0.36, spec) + 0.015, along + 0.32)]
        roots.tube(points, [(0.035, 0.035), (0.033, 0.033), (0.030, 0.030),
                            (0.027, 0.027), (0.024, 0.024), (0.020, 0.020)],
                   "RiverBankRoot", sides=5, ref=(0, 0, 1))
    shelves = []
    erosion_segments = math.ceil((spec["erosionEnd"] - spec["erosionStart"]) / 0.5)
    for row in range(erosion_segments + 1):
        along = spec["erosionStart"] + (spec["erosionEnd"] - spec["erosionStart"]) * row / erosion_segments
        edge = -0.26 + edge_shift(along, spec)
        width = 0.175 + 0.075 * math.tanh(2 * noise(along * 1.7, 0, 0, spec["seed"] + 31))
        height = bank_lift(edge, along, spec) + 0.025
        cross_section = [(-width, 0.014), (-width * 0.35, 0.034), (0, 0.04),
                         (0, height), (-width * 0.35, height), (-width * 0.7, 0.029), (-width, 0.024)]
        shelves.append([lip.vertex((edge + x, bank_height(edge + x, spec) + lift, along))
                        for x, lift in cross_section])
    for lower, upper in zip(shelves, shelves[1:]):
        for side in range(7):
            next_side = (side + 1) % 7
            a, b, c, d = lower[side], lower[next_side], upper[next_side], upper[side]
            lip.face((a, b, c), "RiverBankEarth")
            lip.face((a, c, d), "RiverBankEarth")
    for triangle in ((0, 1, 5), (1, 2, 5), (2, 3, 4), (2, 4, 5), (0, 5, 6)):
        lip.face([shelves[0][i] for i in reversed(triangle)], "RiverBankEarth")
        lip.face([shelves[-1][i] for i in triangle], "RiverBankEarth")
    for mesh in (soil, stones, roots, lip):
        mesh.vertices = [Vector(bend(point, spec["radius"])) for point in mesh.vertices]
    return for_export([soil, stones, roots, lip])
