"""Stormreach Valley: the Special Forces map as a heightfield (pure numpy, no Blender needed).

Coordinates: x east, y north, z up, metres; the map spans -500..500 on x and y (1 km square, ~4x a stage's area).
South to north: the outpost (control tower + lander pad) -> forest hills with Storm Nests -> the river (open valley
in the south, a mossy slot canyon with waterfalls in the middle, a grotto pool) -> waterfall cliff up to the golden
Storm Plain where the hurricane waits. A mountain rim closes the map.

Everything other scripts need comes from build(): heights, per-cell material ids, the river ribbon, waterfalls,
scatter points for trees and boulders, and named markers the mod reads at runtime.
"""
import math

import numpy as np

SIZE = 1000.0
HALF = SIZE / 2
STEP = 2.0                      # grid spacing (m)
N = int(SIZE / STEP) + 1        # 501 x 501 vertices

# Material ids per terrain cell; the names are the material slots the mod swaps for the game's own materials.
MATERIALS = ["matForestFloor", "matMossRock", "matCliff", "matRiverbed", "matPlainGrass", "matOutpostGround"]
FOREST, MOSSROCK, CLIFF, RIVERBED, PLAIN, OUTPOST = range(6)

# ---------------------------------------------------------------- layout

OUTPOST_CENTER = (0.0, -370.0)
OUTPOST_RADIUS = 90.0
OUTPOST_HEIGHT = 20.0
TOWER = (0.0, -385.0)
LANDER_PAD = (38.0, -355.0)
PLAIN_CENTER = (0.0, 365.0)
PLAIN_RADIUS = 125.0
PLAIN_HEIGHT = 50.0
BOSS_LAIR = (0.0, 395.0)

# name -> (x, y, clearing radius, tier)
NESTS = {
    "SF_Nest_T1_a": (-175.0, -215.0, 30.0, 1), "SF_Nest_T1_b": (185.0, -225.0, 30.0, 1),
    "SF_Nest_T2_a": (-265.0, -55.0, 32.0, 2), "SF_Nest_T2_b": (255.0, -40.0, 32.0, 2),
    "SF_Nest_T3_a": (-205.0, 120.0, 34.0, 3), "SF_Nest_T3_b": (225.0, 110.0, 34.0, 3),
    "SF_Nest_T4_a": (-285.0, 255.0, 36.0, 4), "SF_Nest_T4_b": (290.0, 245.0, 36.0, 4),
}
LAIRS = {"SF_Lair_A": (40.0, 20.0), "SF_Lair_B": (-110.0, 270.0)}
WAVE_GATES = {
    "SF_WaveGate_1": (0.0, 255.0), "SF_WaveGate_2": (-330.0, 120.0), "SF_WaveGate_3": (335.0, 100.0),
    "SF_WaveGate_4": (-320.0, -140.0), "SF_WaveGate_5": (320.0, -160.0),
}

# River centreline, north to south: a stream across the plain, the head waterfall, slot canyon, ford, grotto,
# slot canyon, then an open valley down to the lake.
RIVER_POINTS = [
    (0.0, 335.0), (6.0, 290.0), (12.0, 248.0), (14.0, 238.0), (30.0, 200.0), (14.0, 160.0), (-22.0, 120.0),
    (-28.0, 95.0), (-10.0, 62.0), (20.0, 36.0), (40.0, 20.0), (52.0, -2.0), (48.0, -30.0), (22.0, -62.0),
    (-12.0, -100.0), (-18.0, -140.0), (12.0, -180.0), (70.0, -220.0), (135.0, -258.0), (195.0, -296.0),
    (225.0, -318.0),
]
LAKE = (232.0, -326.0, 42.0)          # x, y, radius
GROTTO = (40.0, 20.0, 30.0)
HEAD_FALL_Y = 243.0                   # the river drops off the plain here


# ---------------------------------------------------------------- noise

def _hash_grads(seed, size=512):
    r = np.random.default_rng(seed)
    angles = r.uniform(0, 2 * math.pi, size)
    perm = r.permutation(size)
    return np.stack([np.cos(angles), np.sin(angles)], 1), np.concatenate([perm, perm])


def perlin(x, y, seed=0):
    grads, perm = _hash_grads(seed)
    xi, yi = np.floor(x).astype(int), np.floor(y).astype(int)
    xf, yf = x - xi, y - yi
    xi &= 511
    yi &= 511

    def g(ix, iy, dx, dy):
        h = perm[perm[ix] + iy] & 511
        return grads[h, 0] * dx + grads[h, 1] * dy

    def fade(v):
        return v * v * v * (v * (v * 6 - 15) + 10)

    n00 = g(xi, yi, xf, yf)
    n10 = g((xi + 1) & 511, yi, xf - 1, yf)
    n01 = g(xi, (yi + 1) & 511, xf, yf - 1)
    n11 = g((xi + 1) & 511, (yi + 1) & 511, xf - 1, yf - 1)
    u, v = fade(xf), fade(yf)
    return (n00 * (1 - u) + n10 * u) * (1 - v) + (n01 * (1 - u) + n11 * u) * v


def fbm(x, y, octaves=4, seed=0):
    total, amp, freq, norm = 0.0, 1.0, 1.0, 0.0
    for o in range(octaves):
        total = total + perlin(x * freq, y * freq, seed + o * 31) * amp
        norm += amp
        amp *= 0.5
        freq *= 2.0
    return total / norm


def smoothstep(e0, e1, v):
    t = np.clip((v - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


# ---------------------------------------------------------------- river geometry

def _river_polyline(spacing=2.0):
    """Densified centreline with arc length, half width, bed height and canyon factor per point."""
    pts = []
    for (x0, y0), (x1, y1) in zip(RIVER_POINTS, RIVER_POINTS[1:]):
        d = math.hypot(x1 - x0, y1 - y0)
        k = max(1, int(d / spacing))
        for i in range(k):
            f = i / k
            pts.append((x0 + (x1 - x0) * f, y0 + (y1 - y0) * f))
    pts.append(RIVER_POINTS[-1])
    pts = np.array(pts)
    # Smooth the corners a little (Chaikin-like moving average), keeping the ends.
    for _ in range(6):
        pts[1:-1] = 0.25 * pts[:-2] + 0.5 * pts[1:-1] + 0.25 * pts[2:]
    seg = np.linalg.norm(np.diff(pts, axis=0), axis=1)
    s = np.concatenate([[0], np.cumsum(seg)])
    y = pts[:, 1]

    # Bed height: a shallow stream on the plain, a 22 m drop at the head waterfall, then sloping down to the lake.
    bed = np.where(y > HEAD_FALL_Y, PLAIN_HEIGHT - 1.2 - (335 - y) * 0.01,
                   np.interp(-y, [-HEAD_FALL_Y, -60.0, 120.0, 330.0], [26.0, 17.0, 10.0, 3.0]))
    # The drop itself: 6 m long, so the waterfall face is near vertical.
    fall = (y <= HEAD_FALL_Y) & (y > HEAD_FALL_Y - 6)
    bed[fall] = np.interp(y[fall], [HEAD_FALL_Y - 6, HEAD_FALL_Y], [26.0, PLAIN_HEIGHT - 1.3])

    # Canyon factor (1 = slot canyon with sheer walls, 0 = open banks). Fords at y ~ 100 and around the grotto.
    canyon = np.zeros_like(y)
    for lo, hi in ((112.0, 238.0), (-58.0, -6.0), (40.0, 88.0)):
        canyon = np.maximum(canyon, smoothstep(lo - 1, lo + 14, y) * smoothstep(hi + 1, hi - 14, y))
    half_width = np.where(y > HEAD_FALL_Y, 4.0, 7.0 + 7.0 * (1 - canyon))
    return pts, s, half_width, bed, canyon


def _closest_on_polyline(px, py, pts):
    """For flat arrays of points: distance to the polyline and the index of the closest vertex."""
    best_d = np.full(px.shape, np.inf)
    best_i = np.zeros(px.shape, dtype=int)
    for i in range(0, len(pts), 1):
        d = (px - pts[i, 0]) ** 2 + (py - pts[i, 1]) ** 2
        m = d < best_d
        best_d[m] = d[m]
        best_i[m] = i
    return np.sqrt(best_d), best_i


# ---------------------------------------------------------------- height field

def _zones():
    """Flattened clearings: (x, y, radius, height or None = keep local average, falloff)."""
    z = [(OUTPOST_CENTER[0], OUTPOST_CENTER[1], OUTPOST_RADIUS, OUTPOST_HEIGHT, 45.0),
         (PLAIN_CENTER[0], PLAIN_CENTER[1], PLAIN_RADIUS, PLAIN_HEIGHT, 35.0)]
    for x, y, r, _tier in NESTS.values():
        z.append((x, y, r, None, 25.0))
    z.append((LAIRS["SF_Lair_B"][0], LAIRS["SF_Lair_B"][1], 34.0, None, 25.0))
    for x, y in WAVE_GATES.values():
        z.append((x, y, 18.0, None, 15.0))
    return z


def build():
    xs = np.linspace(-HALF, HALF, N)
    X, Y = np.meshgrid(xs, xs)          # X[j, i] = xs[i], Y[j, i] = xs[j]  (row j = north-south)

    # Rolling forest hills.
    H = 30.0 + 17.0 * fbm(X / 230.0, Y / 230.0, 4, 3) + 5.0 * fbm(X / 55.0, Y / 55.0, 3, 9)
    # The land rises a little towards the north (the plain is the high ground).
    H += np.interp(Y, [-HALF, 0, HALF], [-8.0, 4.0, 12.0])

    # Mountain rim (ridged noise), leaving the corners and edges unclimbable.
    edge = HALF - np.maximum(np.abs(X), np.abs(Y)) + 35.0 * fbm(X / 120.0, Y / 120.0, 3, 33)
    ridge = 1.0 - np.abs(fbm(X / 90.0, Y / 90.0, 4, 21))
    H += smoothstep(95.0, 8.0, edge) * (85.0 + 70.0 * ridge)

    # Clearings.
    for cx, cy, r, h, fall in _zones():
        d = np.hypot(X - cx, Y - cy)
        w = smoothstep(r + fall, r, d)
        target = h if h is not None else float(np.mean(H[(d < r)]))
        flat = target + 0.6 * fbm(X / 30.0, Y / 30.0, 2, 50)
        H = H * (1 - w) + flat * w
    # Plain: soft golden swells.
    d_plain = np.hypot(X - PLAIN_CENTER[0], Y - PLAIN_CENTER[1])
    H += smoothstep(PLAIN_RADIUS, PLAIN_RADIUS - 30, d_plain) * 1.5 * fbm(X / 40.0, Y / 40.0, 2, 77)

    # River.
    pts, s, half_w, bed, canyon = _river_polyline()
    dist, idx = _closest_on_polyline(X.ravel(), Y.ravel(), pts)
    dist = dist.reshape(X.shape)
    idx = idx.reshape(X.shape)
    b = bed[idx]
    w = half_w[idx]
    c = canyon[idx]
    # Canyon walls: the surrounding land is raised to at least bed + 22 m near slot sections, then a sheer wall.
    plateau = np.maximum(H, b + 22.0 + 6.0 * fbm(X / 25.0, Y / 25.0, 2, 61))
    H = np.where(c > 0.01, H * (1 - c * smoothstep(w + 70, w + 10, dist)) + plateau * c * smoothstep(w + 70, w + 10, dist), H)
    wall = smoothstep(w + 0.5, w + 3.5 + (1 - c) * 36.0, dist)     # 3 m wide wall in a slot, 40 m bank when open
    H = b * (1 - wall) + H * wall
    # The lake and the grotto pool are bowls.
    for cx, cy, r in (LAKE, GROTTO):
        d = np.hypot(X - cx, Y - cy)
        floor = 2.0 if (cx, cy, r) == LAKE else 12.0
        k = smoothstep(r + 6, r - 4, d)
        H = H * (1 - k) + floor * k

    # ---------------------------------------------------------- materials per cell
    gy, gx = np.gradient(H, STEP)
    slope = np.degrees(np.arctan(np.hypot(gx, gy)))
    mat_v = np.full(H.shape, FOREST, dtype=np.int8)
    mat_v[slope > 24] = MOSSROCK
    mat_v[slope > 42] = CLIFF
    mat_v[d_plain < PLAIN_RADIUS + 10] = PLAIN
    mat_v[(slope > 42) & (d_plain < PLAIN_RADIUS + 10)] = CLIFF
    mat_v[dist < w + 1.5] = RIVERBED
    for cx, cy, r in (LAKE, GROTTO):
        mat_v[np.hypot(X - cx, Y - cy) < r + 2] = RIVERBED
    d_out = np.hypot(X - OUTPOST_CENTER[0], Y - OUTPOST_CENTER[1])
    mat_v[(d_out < OUTPOST_RADIUS - 5) & (slope < 24)] = OUTPOST
    # One id per cell: the most "special" of its four corners.
    mat_c = np.maximum.reduce([mat_v[:-1, :-1], mat_v[1:, :-1], mat_v[:-1, 1:], mat_v[1:, 1:]])
    cliffy = np.maximum.reduce([slope[:-1, :-1], slope[1:, :-1], slope[:-1, 1:], slope[1:, 1:]]) > 42
    mat_c[cliffy & (mat_c != RIVERBED)] = CLIFF

    terrain = dict(xs=xs, H=H, slope=slope, mat=mat_c, river_dist=dist, river_w=w)
    water = _water(pts, half_w, bed)
    falls = _waterfalls(H, pts, bed, half_w, canyon)
    scatter = _scatter(X, Y, H, slope, dist, w, d_plain, d_out)
    markers = _markers(H)
    return terrain, water, falls, scatter, markers


def height_at(terrain, x, y):
    xs, H = terrain["xs"], terrain["H"]
    fx = (x + HALF) / STEP
    fy = (y + HALF) / STEP
    i0, j0 = int(np.clip(math.floor(fx), 0, N - 2)), int(np.clip(math.floor(fy), 0, N - 2))
    tx, ty = fx - i0, fy - j0
    h00, h10, h01, h11 = H[j0, i0], H[j0, i0 + 1], H[j0 + 1, i0], H[j0 + 1, i0 + 1]
    return float((h00 * (1 - tx) + h10 * tx) * (1 - ty) + (h01 * (1 - tx) + h11 * tx) * ty)


# ---------------------------------------------------------------- water, waterfalls

def _water(pts, half_w, bed):
    """River ribbon (left/right edge points with heights) plus the lake and grotto discs."""
    left, right, heights = [], [], []
    for i in range(len(pts)):
        a = pts[max(0, i - 1)]
        b = pts[min(len(pts) - 1, i + 1)]
        tx, ty = b[0] - a[0], b[1] - a[1]
        L = math.hypot(tx, ty) or 1.0
        nx, ny = -ty / L, tx / L
        ww = half_w[i] + 1.0
        left.append((pts[i, 0] + nx * ww, pts[i, 1] + ny * ww))
        right.append((pts[i, 0] - nx * ww, pts[i, 1] - ny * ww))
        heights.append(bed[i] + 0.55)
    return dict(left=np.array(left), right=np.array(right), z=np.array(heights),
                discs=[(LAKE[0], LAKE[1], LAKE[2] + 4, 2.6), (GROTTO[0], GROTTO[1], GROTTO[2] + 3, 12.6)])


def _waterfalls(H, pts, bed, half_w, canyon):
    """(name, x, y, top z, bottom z, width, facing angle). The head fall, a grotto cascade and canyon veils."""
    falls = []
    # Head fall: where the bed drops off the plain.
    i = int(np.argmin(np.abs(pts[:, 1] - (HEAD_FALL_Y - 3))))
    a, b = pts[max(0, i - 3)], pts[min(len(pts) - 1, i + 3)]
    ang = math.atan2(b[1] - a[1], b[0] - a[0])
    falls.append(("SF_Waterfall_Head", pts[i, 0], pts[i, 1], PLAIN_HEIGHT - 1.0, 26.5, 12.0, ang))
    # Grotto cascade: off the west rim of the grotto bowl (image 3).
    gx, gy, gr = GROTTO
    falls.append(("SF_Waterfall_Grotto", gx - gr + 2, gy + 6, 34.0, 12.8, 7.0, 0.0))
    # Veils dripping from the slot canyon rims (image 2), on alternating sides.
    side = 1
    for y_pick in (210.0, 168.0, 72.0, -30.0):
        i = int(np.argmin(np.abs(pts[:, 1] - y_pick)))
        a, b = pts[max(0, i - 2)], pts[min(len(pts) - 1, i + 2)]
        tx, ty = b[0] - a[0], b[1] - a[1]
        L = math.hypot(tx, ty) or 1.0
        nx, ny = -ty / L * side, tx / L * side
        wx, wy = pts[i, 0] + nx * (half_w[i] + 2.0), pts[i, 1] + ny * (half_w[i] + 2.0)
        top = float(H[int((wy + HALF) / STEP), int((wx + HALF) / STEP)])
        falls.append((f"SF_Waterfall_Veil{len(falls) - 1}", wx, wy, top - 0.5, bed[i] + 0.5, 3.0,
                      math.atan2(-ny, -nx)))
        side = -side
    return falls


# ---------------------------------------------------------------- trees, boulders

def _scatter(X, Y, H, slope, dist, w, d_plain, d_out):
    r = np.random.default_rng(404)
    trees, boulders = [], []
    nest_pts = [(x, y, rad) for x, y, rad, _t in NESTS.values()]

    def clear_of_zones(x, y, extra):
        if math.hypot(x - OUTPOST_CENTER[0], y - OUTPOST_CENTER[1]) < OUTPOST_RADIUS + extra:
            return False
        if math.hypot(x - PLAIN_CENTER[0], y - PLAIN_CENTER[1]) < PLAIN_RADIUS + extra * 0.5:
            return False
        for nx, ny, rad in nest_pts:
            if math.hypot(x - nx, y - ny) < rad + extra:
                return False
        for lx, ly in list(LAIRS.values()) + list(WAVE_GATES.values()):
            if math.hypot(x - lx, y - ly) < 30 + extra:
                return False
        return True

    def sample(x, y):
        i = int(np.clip((x + HALF) / STEP, 0, N - 1))
        j = int(np.clip((y + HALF) / STEP, 0, N - 1))
        return H[j, i], slope[j, i], dist[j, i] - w[j, i]

    # Trees: rejection sampling with a minimum spacing (a coarse Poisson disc), denser in the middle band.
    placed = []
    grid = {}
    cell = 7.0
    tries = 0
    while len(placed) < 1600 and tries < 120000:
        tries += 1
        x, y = r.uniform(-HALF + 70, HALF - 70), r.uniform(-HALF + 70, HALF - 70)
        h, sl, rd = sample(x, y)
        if sl > 30 or rd < 7 or not clear_of_zones(x, y, 8):
            continue
        key = (int(x // cell), int(y // cell))
        ok = True
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                for px, py in grid.get((key[0] + dx, key[1] + dy), []):
                    if (px - x) ** 2 + (py - y) ** 2 < cell * cell:
                        ok = False
        if not ok:
            continue
        grid.setdefault(key, []).append((x, y))
        placed.append((x, y))
        trees.append((x, y, h - 0.4, r.uniform(14, 26), r.uniform(0, 2 * math.pi), int(r.integers(0, 1 << 30))))

    # Boulders: anywhere not in clearings, more near the river and on slopes.
    for _ in range(420):
        for _attempt in range(20):
            x, y = r.uniform(-HALF + 60, HALF - 60), r.uniform(-HALF + 60, HALF - 60)
            h, sl, rd = sample(x, y)
            if sl > 38 or not clear_of_zones(x, y, 4):
                continue
            if rd < 2:
                continue
            boulders.append((x, y, h - 0.6, r.uniform(1.2, 4.5), r.uniform(0, 2 * math.pi), int(r.integers(0, 1 << 30))))
            break
    return dict(trees=trees, boulders=boulders)


# ---------------------------------------------------------------- markers

def _markers(H):
    def at(x, y, dz=0.0):
        i = int(np.clip(round((x + HALF) / STEP), 0, N - 1))
        j = int(np.clip(round((y + HALF) / STEP), 0, N - 1))
        return (x, y, float(H[j, i]) + dz)

    m = {"SF_Tower": at(*TOWER), "SF_LanderPad": at(*LANDER_PAD), "SF_BossLair": at(*BOSS_LAIR)}
    for name, (x, y, _r, _t) in NESTS.items():
        m[name] = at(x, y)
    for name, (x, y) in LAIRS.items():
        m[name] = at(x, y, 0.0 if name != "SF_Lair_A" else 0.6)
    for name, (x, y) in WAVE_GATES.items():
        m[name] = at(x, y)
    # Player spawns: two rows of six beside the lander pad (its ramp faces west, towards the tower).
    for k in range(12):
        row, col = divmod(k, 6)
        m[f"SF_PlayerSpawn_{k + 1:02d}"] = at(LANDER_PAD[0] - 18 - row * 4, LANDER_PAD[1] - 10 + col * 4)
    # Floodlights around the outpost.
    for k in range(6):
        a = k / 6 * 2 * math.pi
        m[f"SF_Floodlight_{k + 1}"] = at(TOWER[0] + math.cos(a) * 55, TOWER[1] + 15 + math.sin(a) * 55)
    return m


if __name__ == "__main__":
    import sys
    import time

    t0 = time.time()
    terrain, water, falls, scatter, markers = build()
    H = terrain["H"]
    print(f"built in {time.time() - t0:.1f} s; height {H.min():.1f}..{H.max():.1f}; "
          f"trees {len(scatter['trees'])}, boulders {len(scatter['boulders'])}, falls {len(falls)}, markers {len(markers)}")
    counts = np.bincount(terrain["mat"].ravel(), minlength=len(MATERIALS))
    print("cells per material: " + ", ".join(f"{n} {c}" for n, c in zip(MATERIALS, counts)))
    if len(sys.argv) > 1:
        import matplotlib
        matplotlib.use("Agg")
        import matplotlib.pyplot as plt
        from matplotlib.colors import LightSource, ListedColormap

        colors = ListedColormap(["#3f6b2e", "#5d6b46", "#4a4a44", "#3b6e78", "#c9a54e", "#8a8577"])
        ls = LightSource(azdeg=315, altdeg=40)
        shade = ls.hillshade(H, vert_exag=2, dx=STEP, dy=STEP)
        fig, ax = plt.subplots(1, 2, figsize=(20, 10))
        ax[0].imshow(H, origin="lower", extent=(-HALF, HALF, -HALF, HALF), cmap="terrain")
        ax[0].imshow(shade, origin="lower", extent=(-HALF, HALF, -HALF, HALF), cmap="gray", alpha=0.45)
        ax[1].imshow(terrain["mat"], origin="lower", extent=(-HALF, HALF, -HALF, HALF), cmap=colors,
                     vmin=0, vmax=len(MATERIALS) - 1, interpolation="nearest")
        ax[1].imshow(shade, origin="lower", extent=(-HALF, HALF, -HALF, HALF), cmap="gray", alpha=0.35)
        for a in ax:
            tx = [t[0] for t in scatter["trees"]]
            ty = [t[1] for t in scatter["trees"]]
            a.scatter(tx, ty, s=2, c="#103010")
            for name, (x, y, z) in markers.items():
                if "PlayerSpawn" in name or "Floodlight" in name:
                    continue
                a.plot(x, y, "o", color="red" if "Nest" in name else "yellow", ms=5)
                a.annotate(name.replace("SF_", ""), (x, y), color="white", fontsize=7)
            for f in falls:
                a.plot(f[1], f[2], "^", color="cyan", ms=7)
        plt.tight_layout()
        plt.savefig(sys.argv[1], dpi=70)
        print("preview ->", sys.argv[1])
