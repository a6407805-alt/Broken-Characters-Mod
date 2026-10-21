"""
Wilbur's makeover: sculpts his primitive pig into smooth, blended cartoon shapes (signed distance fields +
marching cubes, then Taubin smoothing) and writes wilbur_model.bin, which WilburMod embeds and swaps in at load.

Every mesh is baked in the local frame of the part it replaces, so all of Wilbur's existing animation
(head bobs, ear flops, blinking lids, snout sniffs, jowl pumps, trotting legs, Super Piggy gold) keeps working.
Coordinates are Unity's: x right, y up, z forward (snout), feet at y = 0.

Run:  python3 build_wilbur.py   (needs numpy + scikit-image; writes ../wilbur_model.bin and previews)
"""
import struct, sys, os
import numpy as np
from skimage import measure

HERE = os.path.dirname(os.path.abspath(__file__))

# ------------------------------------------------------------------ SDF helpers

def ell(p, c, r):
    """Approximate signed distance to an ellipsoid (centre c, semi-axes r)."""
    q = (p - np.array(c)) / np.array(r)
    k0 = np.linalg.norm(q, axis=-1)
    k1 = np.linalg.norm(q / np.array(r), axis=-1)
    return k0 * (k0 - 1.0) / np.maximum(k1, 1e-6)

def smin(a, b, k):
    h = np.clip(0.5 + 0.5 * (b - a) / k, 0.0, 1.0)
    return b * (1 - h) + a * h - k * h * (1 - h)

def smax(a, b, k):
    return -smin(-a, -b, k)

def seg_dist(p, a, b):
    a = np.array(a, float); b = np.array(b, float)
    ab = b - a
    t = np.clip(((p - a) @ ab) / max(ab @ ab, 1e-9), 0, 1)
    return np.linalg.norm(p - (a + t[..., None] * ab), axis=-1), t

def tube(p, pts, r0, r1):
    """Distance to a tapering tube along a polyline."""
    d = np.full(p.shape[:-1], 1e9)
    n = len(pts) - 1
    for i in range(n):
        di, t = seg_dist(p, pts[i], pts[i + 1])
        r = r0 + (r1 - r0) * ((i + t) / n)
        d = np.minimum(d, di - r)
    return d

def rounded_cyl(p, c, rad, half_h, bevel, axis=1):
    q = p - np.array(c)
    radial = np.linalg.norm(np.delete(q, axis, axis=-1), axis=-1)
    dx = radial - (rad - bevel)
    dy = np.abs(q[..., axis]) - (half_h - bevel)
    outside = np.linalg.norm(np.stack([np.maximum(dx, 0), np.maximum(dy, 0)], -1), axis=-1)
    return outside + np.minimum(np.maximum(dx, dy), 0) - bevel

# ------------------------------------------------------------------ meshing

def mesh_sdf(f, lo, hi, step, smooth=6):
    lo = np.array(lo, float); hi = np.array(hi, float)
    n = np.ceil((hi - lo) / step).astype(int) + 1
    xs = [lo[i] + np.arange(n[i]) * step for i in range(3)]
    P = np.stack(np.meshgrid(*xs, indexing='ij'), -1)
    V = f(P)
    if V.min() >= 0 or V.max() <= 0:
        raise ValueError("surface not inside the grid")
    verts, faces, _, _ = measure.marching_cubes(V, 0.0, spacing=(step, step, step))
    verts = verts + lo
    verts = taubin(verts, faces, smooth)
    # outward = SDF gradient; make cross(b-a, c-a) point outward (Unity's front face)
    e = 1e-3
    g = np.stack([f(verts + np.eye(3)[i] * e) - f(verts - np.eye(3)[i] * e) for i in range(3)], -1)
    a, b, c = verts[faces[:, 0]], verts[faces[:, 1]], verts[faces[:, 2]]
    fn = np.cross(b - a, c - a)
    flip = (fn * (g[faces[:, 0]] + g[faces[:, 1]] + g[faces[:, 2]])).sum(-1) < 0
    faces[flip] = faces[flip][:, [0, 2, 1]]
    normals = vertex_normals(verts, faces)
    return verts.astype(np.float32), normals.astype(np.float32), faces.astype(np.int32)

def taubin(v, f, iters, lam=0.5, mu=-0.53):
    n = len(v)
    i = np.concatenate([f[:, 0], f[:, 1], f[:, 2], f[:, 1], f[:, 2], f[:, 0]])
    j = np.concatenate([f[:, 1], f[:, 2], f[:, 0], f[:, 0], f[:, 1], f[:, 2]])
    deg = np.bincount(i, minlength=n).astype(float)
    for _ in range(iters):
        for k in (lam, mu):
            s = np.zeros_like(v)
            np.add.at(s, i, v[j])
            avg = s / np.maximum(deg, 1)[:, None]
            v = v + k * (avg - v)
    return v

def vertex_normals(v, f):
    fn = np.cross(v[f[:, 1]] - v[f[:, 0]], v[f[:, 2]] - v[f[:, 0]])
    vn = np.zeros_like(v)
    for k in range(3):
        np.add.at(vn, f[:, k], fn)
    return vn / np.maximum(np.linalg.norm(vn, axis=-1, keepdims=True), 1e-9)

def mirror_x(mesh):
    v, n, f = mesh
    v = v.copy(); n = n.copy()
    v[:, 0] *= -1; n[:, 0] *= -1
    return v, n, f[:, [0, 2, 1]].copy()

# ------------------------------------------------------------------ Wilbur, part by part

def body_sdf(p):
    """Torso in the pig's root frame: the old body/rump/chest/neck/hams/shoulders, blended into one chubby form."""
    d = ell(p, (0, 0.62, 0), (0.45, 0.38, 0.525))
    d = smin(d, ell(p, (0, 0.64, -0.26), (0.43, 0.37, 0.34)), 0.10)
    d = smin(d, ell(p, (0, 0.63, 0.24), (0.41, 0.36, 0.33)), 0.10)
    d = smin(d, ell(p, (0, 0.76, 0.43), (0.30, 0.27, 0.20)), 0.10)
    for sx in (-1, 1):
        d = smin(d, ell(p, (0.27 * sx, 0.50, -0.30), (0.17, 0.21, 0.22)), 0.08)   # hams
        d = smin(d, ell(p, (0.27 * sx, 0.52, 0.30), (0.155, 0.185, 0.185)), 0.08)  # shoulders
    # a round, slightly saggy belly
    d = smin(d, ell(p, (0, 0.44, 0.04), (0.37, 0.25, 0.43)), 0.12)
    return d

def belly_sdf(p):
    """The lighter belly: a skin-thin layer over the torso, only underneath."""
    shell = body_sdf(p) - 0.010
    region = ell(p, (0, 0.40, 0.05), (0.33, 0.22, 0.42))
    return smax(shell, region, 0.03)

def spot_sdf(c, r):
    def f(p):
        return smax(body_sdf(p) - 0.008, ell(p, c, r), 0.015)
    return f

def head_sdf(p):
    """Head in HeadPivot space: round skull, chubby cheeks, a soft brow and the snout bridge, one surface."""
    d = ell(p, (0, 0.04, 0.12), (0.37, 0.345, 0.335))
    for sx in (-1, 1):
        d = smin(d, ell(p, (0.20 * sx, -0.05, 0.22), (0.15, 0.13, 0.12)), 0.07)    # cheeks
    d = smin(d, ell(p, (0, 0.27, 0.30), (0.24, 0.07, 0.09)), 0.06)                  # brow ridge
    d = smin(d, ell(p, (0, -0.03, 0.36), (0.14, 0.11, 0.12)), 0.07)                 # snout bridge
    return d

def jowls_sdf(p):
    """Jowls in their own frame (they puff while Wilbur sucks): a soft double chin."""
    d = ell(p, (0, 0, 0), (0.30, 0.19, 0.26))
    d = smin(d, ell(p, (0, -0.06, 0.05), (0.22, 0.12, 0.18)), 0.06)
    return d

def snout_sdf(p):
    """Snout puck in the snout's own (rotated) frame: local y is depth, x width, z height."""
    q = p / np.array([1.0, 1.0, 0.11 / 0.15])           # oval face: 0.30 wide, 0.22 tall
    d = rounded_cyl(q, (0, 0, 0), 0.15, 0.085, 0.045, axis=1) * 0.85
    # nostril dents in the front face (+y is forward after the 90-degree tilt)
    for sx in (-1, 1):
        d = smax(d, -ell(p, (0.055 * sx, 0.09, 0.0), (0.035, 0.03, 0.05)), 0.012)
    return d

def ear_sdf(p):
    """Left ear in its pivot frame: a floppy, cupped, rounded triangle whose tip folds forward."""
    y = p[..., 1]
    t = np.clip(y / 0.30, 0, 1)
    w = 0.135 * (1 - t) ** 0.75 + 0.012
    cup = 0.05 * (p[..., 0] / np.maximum(w, 1e-3)) ** 2
    fold = 0.10 * np.clip((t - 0.45) / 0.55, 0, 1) ** 2
    zc = p[..., 2] - cup - fold
    d_plane = np.abs(zc) - 0.022 * (1 - 0.5 * t)
    d_shape = np.abs(p[..., 0]) - w
    d_y = np.maximum(-y - 0.02, y - 0.31)
    return smax(smax(d_plane, d_shape, 0.02), d_y, 0.02)

def ear_inner_sdf(p):
    y = p[..., 1]
    t = np.clip(y / 0.30, 0, 1)
    w = 0.085 * (1 - t) ** 0.8 + 0.006
    cup = 0.05 * (p[..., 0] / np.maximum(w / 0.63, 1e-3)) ** 2
    fold = 0.10 * np.clip((t - 0.45) / 0.55, 0, 1) ** 2
    zc = p[..., 2] - cup - fold - 0.018
    d_plane = np.abs(zc) - 0.008
    d_shape = np.abs(p[..., 0]) - w
    d_y = np.maximum(0.03 - y, y - 0.25)
    return smax(smax(d_plane, d_shape, 0.012), d_y, 0.012)

def tail_sdf(p):
    """Curly corkscrew tail in the tail pivot frame."""
    pts = []
    for i in range(29):
        a = i / 28 * 2.2 * np.pi
        r = 0.065 * (1 - 0.35 * i / 28)
        pts.append((np.sin(a) * r, 0.02 + 0.10 * i / 28 + 0.03 * np.sin(a * 0.5), -0.02 - np.cos(a) * r * 0.8 - 0.09 * i / 28))
    d = tube(p, [(0, -0.03, 0.05)] + pts, 0.042, 0.022)
    d = smin(d, ell(p, pts[-1], (0.028, 0.028, 0.028)), 0.01)
    return d

def tuft_sdf(p):
    """A little curl of hair on top of the head (HeadPivot space)."""
    pts = []
    for i in range(18):
        a = i / 17 * 1.6 * np.pi
        pts.append((0.0 + 0.02 * np.sin(a * 2), 0.33 + 0.07 * np.sin(a) + i * 0.004, 0.12 + 0.07 * np.cos(a) - 0.02))
    d = tube(p, [(0, 0.30, 0.13)] + pts, 0.034, 0.014)
    for sx in (-1, 1):
        d = smin(d, tube(p, [(0.0, 0.31, 0.12), (0.06 * sx, 0.39, 0.10), (0.085 * sx, 0.41, 0.06)], 0.022, 0.01), 0.02)
    return d

def leg_sdf(p):
    """Leg in its pivot frame: chunky at the top (tucked into the body), tapering to a slim ankle."""
    d = tube(p, [(0, 0.06, 0), (0, -0.16, 0.005), (0, -0.33, 0.012)], 0.112, 0.078)
    d = smin(d, ell(p, (0, -0.30, 0.012), (0.085, 0.05, 0.085)), 0.03)                # little fetlock
    return d

def hoof_sdf(p):
    """Cloven trotter: two rounded toes with a notch, a little shine of the old hooves' colour."""
    d = 1e9
    for sx in (-1, 1):
        t = ell(p, (0.04 * sx, -0.362, 0.03), (0.052, 0.05, 0.075))
        d = np.minimum(d, t) if np.isscalar(d) else smin(d, t, 0.005)
    flat = -(p[..., 1] + 0.398)                                                         # flat sole
    return smax(d, flat, 0.01)

# ------------------------------------------------------------------ ears (parametric: thin shapes need few triangles)

def _ear_point(u, v, inner=False):
    # a broad, rounded pig ear: wide base, soft round tip, gently cupped, the top third flopping forward
    w = 0.15 * np.clip(1 - v ** 1.7, 0, 1) ** 0.55 + 0.008
    x = u * w
    y = v * 0.27
    zmid = 0.045 * u * u + 0.06 * np.clip((v - 0.5) / 0.5, 0, 1) ** 2
    t = 0.024 * (1 - 0.45 * v) * np.sqrt(np.clip(1 - u * u, 0, 1))
    return x, y, zmid, t

def grid(nu, nv, fn, flip=False):
    us = np.linspace(-1, 1, nu); vs = np.linspace(0, 1, nv)
    V = np.array([fn(u, v) for v in vs for u in us])
    F = []
    for j in range(nv - 1):
        for i in range(nu - 1):
            a = j * nu + i; b = a + 1; c = a + nu; d = c + 1
            F += [[a, c, b], [b, c, d]] if not flip else [[a, b, c], [b, d, c]]
    return V, np.array(F)

def _orient(v, f, outward):
    a, b, c = v[f[:, 0]], v[f[:, 1]], v[f[:, 2]]
    fn = np.cross(b - a, c - a)
    if (fn @ outward).mean() < 0: f = f[:, [0, 2, 1]]
    return f

def ear_mesh():
    """Two curved faces (front cupped, back) meeting at a rounded rim; the base hides inside the head."""
    def front(u, v):
        x, y, z, t = _ear_point(u, v); return (x, y, z + t)
    def back(u, v):
        x, y, z, t = _ear_point(u, v); return (x, y, z - t)
    vf, ff = grid(17, 15, front); vb, fb = grid(17, 15, back)
    ff = _orient(vf, ff, np.array([0, 0, 1.0])); fb = _orient(vb, fb, np.array([0, 0, -1.0]))
    v = np.concatenate([vf, vb]); f = np.concatenate([ff, fb + len(vf)])
    v = v.astype(np.float32)
    return v, vertex_normals(v, f).astype(np.float32), f.astype(np.int32)

def ear_inner_mesh():
    def front(u, v):
        uu = u * 0.62; vv = 0.06 + v * 0.74
        x, y, z, t = _ear_point(uu, vv); return (x, y, z + t + 0.004)
    v, f = grid(11, 11, front)
    f = _orient(v, f, np.array([0, 0, 1.0]))
    v = v.astype(np.float32)
    return v, vertex_normals(v, f).astype(np.float32), f.astype(np.int32)

# ------------------------------------------------------------------ build + write

def build():
    S = 0.018
    parts = []   # (name, frame, mesh, removes)  frame 0 = bake in parent frame (reset transform), 1 = own frame (scale 1)
    print("body..."); parts.append(("Pig_Body", 0, mesh_sdf(body_sdf, (-0.62, 0.12, -0.78), (0.62, 1.12, 0.74), 0.05, 24),
                                    ["Pig_Rump", "Pig_Chest", "Pig_Neck", "Pig_HamL", "Pig_HamR", "Pig_ShoulderL", "Pig_ShoulderR"]))
    print("belly..."); parts.append(("Pig_Belly", 0, mesh_sdf(belly_sdf, (-0.5, 0.12, -0.5), (0.5, 0.7, 0.62), 0.04, 16), []))
    print("spots..."); parts.append(("Spot0", 0, mesh_sdf(spot_sdf((0.12, 0.98, -0.22), (0.10, 0.08, 0.09)), (-0.05, 0.8, -0.4), (0.3, 1.12, -0.05), 0.02, 4), []))
    parts.append(("Spot1", 0, mesh_sdf(spot_sdf((-0.16, 0.95, 0.02), (0.065, 0.07, 0.06)), (-0.3, 0.8, -0.12), (-0.02, 1.12, 0.15), 0.02, 4), []))
    print("head..."); parts.append(("Pig_Head", 0, mesh_sdf(head_sdf, (-0.48, -0.36, -0.28), (0.48, 0.44, 0.52), 0.042, 22),
                                    ["Pig_SnoutBridge", "Pig_Tuft1", "Pig_Tuft2"]))
    print("tuft..."); parts.append(("Pig_Tuft0", 0, mesh_sdf(tuft_sdf, (-0.15, 0.24, -0.02), (0.15, 0.48, 0.25), 0.018, 3), []))
    print("jowls..."); parts.append(("Pig_Jowls", 1, mesh_sdf(jowls_sdf, (-0.36, -0.28, -0.32), (0.36, 0.24, 0.34), 0.04, 16), []))
    print("snout..."); parts.append(("Pig_Snout", 1, mesh_sdf(snout_sdf, (-0.2, -0.13, -0.16), (0.2, 0.13, 0.16), 0.02, 4), []))
    print("ears..."); ear = ear_mesh(); inner = ear_inner_mesh()
    parts.append(("Pig_EarL", 0, ear, [])); parts.append(("EarInnerL", 0, inner, []))
    parts.append(("Pig_EarR", 0, mirror_x(ear), [])); parts.append(("EarInnerR", 0, mirror_x(inner), []))
    print("tail..."); parts.append(("Pig_Tail0", 0, mesh_sdf(tail_sdf, (-0.14, -0.1, -0.24), (0.14, 0.2, 0.12), 0.018, 3),
                                    ["Pig_Tail%d" % i for i in range(1, 8)]))
    print("legs..."); leg = mesh_sdf(leg_sdf, (-0.16, -0.40, -0.15), (0.16, 0.2, 0.16), 0.03, 6)
    hoof = mesh_sdf(hoof_sdf, (-0.13, -0.42, -0.08), (0.13, -0.29, 0.13), 0.016, 3)
    for i in range(4):
        parts.append(("Pig_Leg%d" % i, 0, leg, []))
        parts.append(("HoofA%d" % i, 0, hoof, ["HoofB%d" % i]))
    return parts

def write(parts, path):
    def s(x):
        b = x.encode('utf-8'); return struct.pack('<i', len(b)) + b
    out = bytearray(b'WPIG') + struct.pack('<ii', 1, len(parts))
    tris = 0
    for name, frame, (v, n, f), removes in parts:
        out += s(name) + struct.pack('<ii', frame, len(removes))
        for r in removes: out += s(r)
        out += struct.pack('<i', len(v)) + v.astype('<f4').tobytes() + n.astype('<f4').tobytes()
        out += struct.pack('<i', f.size) + f.astype('<i4').tobytes()
        tris += len(f)
        print(f"  {name:12s} {len(v):6d} verts {len(f):6d} tris")
    open(path, 'wb').write(out)
    print(f"wrote {path}: {len(parts)} parts, {tris} triangles, {len(out)/1024:.0f} KB")

if __name__ == '__main__':
    parts = build()
    write(parts, os.path.join(HERE, '..', 'wilbur_model.bin'))
