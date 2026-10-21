"""Renders Wilbur before/after the makeover by reading BuildPig() from WilburPlugin.cs and wilbur_model.bin."""
import re, struct, math, os, sys
import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = open(os.path.join(HERE, '..', 'WilburPlugin.cs'), encoding='utf-8').read()
body = SRC[SRC.index('private static GameObject BuildPig()'):]
body = body[:body.index('return root;')]

def v3(s):
    s = s.strip()
    if s == 'Vector3.zero': return np.zeros(3)
    if s == 'Vector3.one': return np.ones(3)
    s = s[len('new Vector3('):-1] if s.startswith('new Vector3(') else s
    return np.array([float(x.strip().rstrip('f')) for x in s.split(',')])

mats = {}
for m in re.finditer(r'Material (\w+) = PigMat\(new Color\(([^)]*)\)', body):
    mats[m.group(1)] = np.array([float(x.strip().rstrip('f')) for x in m.group(2).split(',')][:3])

def rot(e):
    x, y, z = np.radians(e)
    Rx = np.array([[1, 0, 0], [0, math.cos(x), -math.sin(x)], [0, math.sin(x), math.cos(x)]])
    Ry = np.array([[math.cos(y), 0, math.sin(y)], [0, 1, 0], [-math.sin(y), 0, math.cos(y)]])
    Rz = np.array([[math.cos(z), -math.sin(z), 0], [math.sin(z), math.cos(z), 0], [0, 0, 1]])
    return Ry @ Rx @ Rz

def M(pos, e, s=(1, 1, 1)):
    T = np.eye(4); T[:3, :3] = rot(e) @ np.diag(s); T[:3, 3] = pos; return T

piv = {'': np.eye(4)}
V3 = r'(new Vector3\([^)]*\)|Vector3\.zero|Vector3\.one)'
for m in re.finditer(r'piv\["(\w+)"\] = MakePivot\(piv\["(\w*)"\], "\w+", ' + V3 + ', ' + V3 + r'\)', body):
    piv[m.group(1)] = piv[m.group(2)] @ M(v3(m.group(3)), v3(m.group(4)))

parts = []
for m in re.finditer(r'MakePart\(piv\["(\w*)"\], "(\w+)", PrimitiveType\.(\w+), ' + V3 + ', ' + V3 + ', ' + V3 + r', (\w+)\)', body):
    parts.append(dict(parent=m.group(1), name=m.group(2), type=m.group(3), pos=v3(m.group(4)), scale=v3(m.group(5)), euler=v3(m.group(6)), mat=m.group(7)))

def sphere(n=18):
    th = np.linspace(0, np.pi, n); ph = np.linspace(0, 2 * np.pi, 2 * n)
    V = np.array([(math.sin(t) * math.cos(p), math.cos(t), math.sin(t) * math.sin(p)) for t in th for p in ph]) * 0.5
    F = []
    for i in range(n - 1):
        for j in range(2 * n - 1):
            a = i * 2 * n + j; F += [[a, a + 1, a + 2 * n], [a + 1, a + 2 * n + 1, a + 2 * n]]
    return V, np.array(F)

def cylinder(n=24):
    V = []; F = []
    for k, y in enumerate((-1, 1)):
        for j in range(n):
            a = 2 * math.pi * j / n; V.append((0.5 * math.cos(a), y, 0.5 * math.sin(a)))
    for j in range(n):
        a, b = j, (j + 1) % n; F += [[a, b, a + n], [b, b + n, a + n]]
    V.append((0, -1, 0)); V.append((0, 1, 0)); c0, c1 = 2 * n, 2 * n + 1
    for j in range(n):
        F += [[c0, (j + 1) % n, j], [c1, j + n, (j + 1) % n + n]]
    return np.array(V, float), np.array(F)

PRIM = {'Sphere': sphere(), 'Cylinder': cylinder()}

def load_bin(path):
    d = open(path, 'rb').read(); o = 4
    ver, n = struct.unpack_from('<ii', d, o); o += 8
    out = {}
    def s():
        nonlocal o
        l = struct.unpack_from('<i', d, o)[0]; o += 4; r = d[o:o + l].decode(); o += l; return r
    for _ in range(n):
        name = s(); frame, nr = struct.unpack_from('<ii', d, o); o += 8
        rem = [s() for _ in range(nr)]
        vc = struct.unpack_from('<i', d, o)[0]; o += 4
        V = np.frombuffer(d, '<f4', vc * 3, o).reshape(-1, 3); o += vc * 12
        N = np.frombuffer(d, '<f4', vc * 3, o).reshape(-1, 3); o += vc * 12
        ic = struct.unpack_from('<i', d, o)[0]; o += 4
        I = np.frombuffer(d, '<i4', ic, o).reshape(-1, 3); o += ic * 4
        out[name] = (frame, rem, V, I)
    return out

def scene(after):
    tris, cols = [], []
    repl = load_bin(os.path.join(HERE, '..', 'wilbur_model.bin')) if after else {}
    removed = set(r for e in repl.values() for r in e[1])
    for p in parts:
        if p['name'] in removed or p['name'].startswith('Spark') or p['name'] == 'MouthOpen': continue
        if p['name'] in repl:
            frame, _, V, I = repl[p['name']]
            T = piv[p['parent']] @ (M(p['pos'], p['euler']) if frame == 1 else np.eye(4))
        else:
            V, I = PRIM[p['type']]
            T = piv[p['parent']] @ M(p['pos'], p['euler'], p['scale'])
        W = (np.c_[V, np.ones(len(V))] @ T.T)[:, :3]
        tris.append(W[I]); cols += [mats[p['mat']]] * len(I)
    return np.concatenate(tris), np.array(cols)

def render(T, C, yaw, pitch, size=560):
    lo, hi = T.reshape(-1, 3).min(0), T.reshape(-1, 3).max(0); centre = (lo + hi) / 2; radius = np.linalg.norm(hi - lo) / 2
    y, p = math.radians(yaw), math.radians(pitch)
    fwd = -np.array([math.sin(y) * math.cos(p), math.sin(p), math.cos(y) * math.cos(p)])
    right = np.cross(np.array([0, 1, 0]), fwd); right /= np.linalg.norm(right); up = np.cross(fwd, right)
    cam = centre - fwd * radius * 3.0
    rel = T - cam; x = rel @ right; yv = rel @ up; z = rel @ fwd
    f = size * 1.5; sx = size / 2 - f * x / z; sy = size / 2 - f * yv / z
    nrm = np.cross(T[:, 1] - T[:, 0], T[:, 2] - T[:, 0]); nrm /= np.maximum(np.linalg.norm(nrm, axis=1, keepdims=True), 1e-9)
    light = np.array([0.35, 0.85, 0.4]); light /= np.linalg.norm(light)
    lam = np.clip(nrm @ light, 0, 1)
    rim = np.clip(1 - np.abs(nrm @ fwd), 0, 1) ** 3
    im = Image.new('RGB', (size, size), (34, 30, 48)); d = ImageDraw.Draw(im)
    for i in np.argsort(-z.mean(1)):
        if (nrm[i] @ fwd) > 0.05: continue           # back faces
        c = C[i] * (0.45 + 0.6 * lam[i]) + 0.25 * rim[i]
        c = np.clip(c ** (1 / 2.2) * 255, 0, 255).astype(int)
        d.polygon([(sx[i, k], sy[i, k]) for k in range(3)], fill=tuple(c))
    return im

def sheet(path):
    rows = []
    for after in (False, True):
        T, C = scene(after)
        ims = [render(T, C, yaw, pitch) for yaw, pitch in ((30, 12), (150, 15), (90, 5))]
        row = Image.new('RGB', (560 * 3, 560)); [row.paste(im, (k * 560, 0)) for k, im in enumerate(ims)]
        rows.append(row)
    out = Image.new('RGB', (560 * 3, 1120)); out.paste(rows[0], (0, 0)); out.paste(rows[1], (0, 560))
    d = ImageDraw.Draw(out); d.text((12, 10), "BEFORE (primitives)", fill=(255, 255, 255)); d.text((12, 570), "AFTER (sculpted)", fill=(255, 255, 255))
    out.save(path); print("saved", path)

if __name__ == '__main__':
    sheet(sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, 'wilbur_preview.png'))
