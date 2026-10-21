"""
A small mesh kit for building Duelist models without Blender, writing the same "DUEL" files the
Blender exporter writes (Unity space: +Y up, +Z = the way the model faces).
Shapes are triangle soups with per-vertex normals and a palette index per vertex.
"""
import math, struct
import numpy as np


class Model:
    def __init__(self):
        self.palette = []          # (r, g, b, emit)
        self.pal_index = {}
        self.parts = {}            # name -> list of (P[n,3], N[n,3], pal[n])
        self.pivots = {}

    def color(self, rgb, emit=0.0):
        key = (round(rgb[0], 4), round(rgb[1], 4), round(rgb[2], 4), emit)
        if key not in self.pal_index:
            self.pal_index[key] = len(self.palette)
            self.palette.append(key)
        return self.pal_index[key]

    def add(self, part, mesh, col):
        P, N = mesh
        self.parts.setdefault(part, []).append((P.astype(np.float32), N.astype(np.float32), np.full(len(P), col, np.int32)))

    def pivot(self, part, at):
        self.pivots[part] = np.array(at, np.float32)

    def bounds(self):
        allp = np.concatenate([p for L in self.parts.values() for (p, _, _) in L])
        return allp.min(0), allp.max(0)

    def save(self, path):
        with open(path, "wb") as f:
            f.write(b"DUEL")
            f.write(struct.pack("<i", 2))
            names = list(self.parts.keys())
            f.write(struct.pack("<i", len(names)))
            for n in names:
                b = n.encode()
                f.write(struct.pack("<i", len(b))); f.write(b)
                pv = self.pivots.get(n, np.zeros(3, np.float32))
                f.write(struct.pack("<3f", *pv)); f.write(struct.pack("<3f", pv[0], pv[1] + 0.1, pv[2]))
            f.write(struct.pack("<i", len(self.palette)))
            for p in self.palette:
                f.write(struct.pack("<4f", *p))
            f.write(struct.pack("<i", len(names)))
            for n in names:
                b = n.encode()
                f.write(struct.pack("<i", len(b))); f.write(b)
                P = np.concatenate([x[0] for x in self.parts[n]])
                N = np.concatenate([x[1] for x in self.parts[n]])
                C = np.concatenate([x[2] for x in self.parts[n]])
                f.write(struct.pack("<i", len(P)))
                rec = np.zeros(len(P), dtype=[("p", "<f4", 3), ("n", "<f4", 3), ("c", "<i4")])
                rec["p"] = P; rec["n"] = N; rec["c"] = C
                f.write(rec.tobytes())


# ------------------------------------------------------------------ helpers

def _norm(v):
    l = np.linalg.norm(v, axis=-1, keepdims=True)
    return v / np.maximum(l, 1e-9)


def grid_mesh(V, NV, flat=False):
    """V, NV: [rows, cols, 3] grid of positions and normals -> triangle soup (rows x cols quads, wrapping cols)."""
    R, C = V.shape[0], V.shape[1]
    P, N = [], []
    for i in range(R - 1):
        for j in range(C - 1):
            a, b, c, d = (i, j), (i, j + 1), (i + 1, j + 1), (i + 1, j)
            for tri in ((a, b, c), (a, c, d)):
                pts = np.array([V[t] for t in tri])
                if np.linalg.norm(np.cross(pts[1] - pts[0], pts[2] - pts[0])) < 1e-10:
                    continue
                P.extend(pts)
                if flat:
                    fn = _norm(np.cross(pts[1] - pts[0], pts[2] - pts[0]))
                    N.extend([fn, fn, fn])
                else:
                    N.extend([NV[t] for t in tri])
    return np.array(P), np.array(N)


def fix_winding(mesh, inside_point=None):
    """Orders each triangle so it faces the way its normals point."""
    P, N = mesh
    P = P.copy(); N = N.copy()
    for k in range(0, len(P), 3):
        a, b, c = P[k], P[k + 1], P[k + 2]
        fn = np.cross(b - a, c - a)
        avg = N[k] + N[k + 1] + N[k + 2]
        # Unity: a triangle is front-facing when cross(b-a, c-a) points toward the viewer (along the normal)
        if np.dot(fn, avg) < 0:
            P[k + 1], P[k + 2] = c.copy(), b.copy()
            N[k + 1], N[k + 2] = N[k + 2].copy(), N[k + 1].copy()
    return P, N


def transform(mesh, scale=(1, 1, 1), rot=(0, 0, 0), move=(0, 0, 0)):
    """Scale, then rotate (degrees, X then Y then Z), then move."""
    P, N = mesh
    s = np.array(scale, float)
    rx, ry, rz = [math.radians(a) for a in rot]
    Rx = np.array([[1, 0, 0], [0, math.cos(rx), -math.sin(rx)], [0, math.sin(rx), math.cos(rx)]])
    Ry = np.array([[math.cos(ry), 0, math.sin(ry)], [0, 1, 0], [-math.sin(ry), 0, math.cos(ry)]])
    Rz = np.array([[math.cos(rz), -math.sin(rz), 0], [math.sin(rz), math.cos(rz), 0], [0, 0, 1]])
    R = Rz @ Ry @ Rx
    P2 = (P * s) @ R.T + np.array(move, float)
    N2 = _norm((N / np.where(s == 0, 1, s)) @ R.T)
    return P2, N2


def merge(*meshes):
    return np.concatenate([m[0] for m in meshes]), np.concatenate([m[1] for m in meshes])


# ------------------------------------------------------------------ shapes

def superellipsoid(radii=(1, 1, 1), e1=1.0, e2=1.0, seg=24, rings=16, flat=False):
    """Rounded boxes (e≈0.2-0.4), pillows, gems (e≈2). Centered at the origin."""
    def sp(w, m):
        return np.sign(w) * np.abs(w) ** m
    us = np.linspace(-math.pi / 2, math.pi / 2, rings + 1)
    vs = np.linspace(-math.pi, math.pi, seg + 1)
    U, Vv = np.meshgrid(us, vs, indexing="ij")
    cu, su, cv, sv = np.cos(U), np.sin(U), np.cos(Vv), np.sin(Vv)
    x = radii[0] * sp(cu, e1) * sp(cv, e2)
    z = radii[2] * sp(cu, e1) * sp(sv, e2)
    y = radii[1] * sp(su, e1)
    V = np.stack([x, y, z], -1)
    nx = sp(cu, 2 - e1) * sp(cv, 2 - e2) / radii[0]
    nz = sp(cu, 2 - e1) * sp(sv, 2 - e2) / radii[2]
    ny = sp(su, 2 - e1) / radii[1]
    NV = _norm(np.stack([nx, ny, nz], -1))
    return fix_winding(grid_mesh(V, NV, flat))


def sphere(r=1.0, seg=20, rings=14, flat=False):
    return superellipsoid((r, r, r), 1, 1, seg, rings, flat)


def lathe(profile, seg=24, flat=False, smooth=True):
    """profile: list of (radius, y) from bottom to top, revolved around +Y."""
    prof = np.array(profile, float)
    vs = np.linspace(0, 2 * math.pi, seg + 1)
    V = np.zeros((len(prof), seg + 1, 3))
    NV = np.zeros_like(V)
    # profile normals (2D) from neighbours
    d = np.gradient(prof, axis=0)
    pn = np.stack([d[:, 1], -d[:, 0]], -1)           # outward for counter-clockwise profile going up
    pn = pn / np.maximum(np.linalg.norm(pn, axis=1, keepdims=True), 1e-9)
    for i, (r, y) in enumerate(prof):
        V[i, :, 0] = r * np.cos(vs); V[i, :, 2] = r * np.sin(vs); V[i, :, 1] = y
        NV[i, :, 0] = pn[i, 0] * np.cos(vs); NV[i, :, 2] = pn[i, 0] * np.sin(vs); NV[i, :, 1] = pn[i, 1]
    return fix_winding(grid_mesh(V, NV, flat or not smooth))


def cylinder(r1=1.0, r2=1.0, h=1.0, seg=20, caps=True, flat=False):
    """Frustum standing on y=0."""
    m = lathe([(r1, 0), (r2, h)], seg, flat=True)
    # side normals: horizontal-ish, smooth around
    P, N = m
    if not flat:
        slope = (r1 - r2) / max(h, 1e-6)
        horiz = _norm(P * np.array([1, 0, 1]))
        N = _norm(horiz + np.array([0, slope, 0]))
    parts = [(P, N)]
    if caps:
        parts.append(disk(r1, 0, seg, down=True))
        if r2 > 1e-4:
            parts.append(disk(r2, h, seg, down=False))
    return fix_winding(merge(*parts))


def disk(r, y, seg=20, down=False):
    vs = np.linspace(0, 2 * math.pi, seg + 1)
    P, N = [], []
    n = np.array([0, -1 if down else 1, 0], float)
    for i in range(seg):
        a = np.array([r * math.cos(vs[i]), y, r * math.sin(vs[i])])
        b = np.array([r * math.cos(vs[i + 1]), y, r * math.sin(vs[i + 1])])
        c = np.array([0, y, 0])
        P += [c, a, b]; N += [n, n, n]
    return fix_winding((np.array(P), np.array(N)))


def torus(R=1.0, r=0.2, seg=36, sides=12, flat=False):
    us = np.linspace(0, 2 * math.pi, seg + 1)
    vs = np.linspace(0, 2 * math.pi, sides + 1)
    U, Vv = np.meshgrid(us, vs, indexing="ij")
    x = (R + r * np.cos(Vv)) * np.cos(U)
    z = (R + r * np.cos(Vv)) * np.sin(U)
    y = r * np.sin(Vv)
    V = np.stack([x, y, z], -1)
    NV = _norm(np.stack([np.cos(Vv) * np.cos(U), np.sin(Vv), np.cos(Vv) * np.sin(U)], -1))
    return fix_winding(grid_mesh(V, NV, flat))


def prism(poly, depth, bevel=0.0, flat=True):
    """Extrude a 2D polygon (x, y list, any winding) along Z, centered on z=0. Optional chamfer."""
    poly = np.array(poly, float)
    c = poly.mean(0)
    layers = []
    if bevel > 0:
        inset = c + (poly - c) * (1 - bevel / max(np.linalg.norm(poly - c, axis=1).mean(), 1e-6))
        layers = [(inset, -depth / 2), (poly, -depth / 2 + bevel), (poly, depth / 2 - bevel), (inset, depth / 2)]
    else:
        layers = [(poly, -depth / 2), (poly, depth / 2)]
    P, N = [], []
    n = len(poly)
    for li in range(len(layers) - 1):
        (pa, za), (pb, zb) = layers[li], layers[li + 1]
        for i in range(n):
            j = (i + 1) % n
            q = [np.array([*pa[i], za]), np.array([*pa[j], za]), np.array([*pb[j], zb]), np.array([*pb[i], zb])]
            for tri in ((q[0], q[1], q[2]), (q[0], q[2], q[3])):
                fn = _norm(np.cross(tri[1] - tri[0], tri[2] - tri[0]))
                mid = sum(tri) / 3
                out = mid - np.array([c[0], c[1], mid[2]])
                if np.dot(fn, out) < 0: fn = -fn
                P += list(tri); N += [fn] * 3
    for (pp, z), sgn in ((layers[0], -1), (layers[-1], 1)):
        cc = np.array([*pp.mean(0), z])
        for i in range(n):
            j = (i + 1) % n
            P += [cc, np.array([*pp[i], z]), np.array([*pp[j], z])]
            N += [np.array([0, 0, sgn], float)] * 3
    return fix_winding((np.array(P), np.array(N)))


def tube(points, radius, sides=10, radii=None):
    """A smooth tube along a polyline (cables, tails, horns). radii: per-point radius."""
    pts = np.array(points, float)
    rs = np.array(radii if radii is not None else [radius] * len(pts), float)
    V = np.zeros((len(pts), sides + 1, 3)); NV = np.zeros_like(V)
    up = np.array([0, 1, 0], float)
    for i in range(len(pts)):
        t = pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]
        t = t / max(np.linalg.norm(t), 1e-9)
        a = up if abs(np.dot(t, up)) < 0.9 else np.array([1, 0, 0], float)
        u = _norm(np.cross(t, a)); v = np.cross(t, u)
        for k in range(sides + 1):
            ang = 2 * math.pi * k / sides
            d = math.cos(ang) * u + math.sin(ang) * v
            V[i, k] = pts[i] + d * rs[i]; NV[i, k] = d
    m = grid_mesh(V, NV)
    return fix_winding(m)


def flip(mesh, axis=0):
    """Mirror across an axis (0 = x): left/right copies."""
    P, N = mesh
    s = np.ones(3); s[axis] = -1
    return fix_winding((P * s, N * s))
