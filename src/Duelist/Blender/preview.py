"""Quick painter's-algorithm preview of a meshkit Model (two-tone cel shading like the game's shader)."""
import math
import numpy as np
from PIL import Image, ImageDraw


def render(model, path, views=((200, 18), (25, 18)), size=520, bg=(26, 20, 40)):
    imgs = []
    allP, allN, allC = [], [], []
    for name, L in model.parts.items():
        pv = np.zeros(3)
        for P, N, C in L:
            allP.append(P); allN.append(N); allC.append(C)
    P = np.concatenate(allP); N = np.concatenate(allN); C = np.concatenate(allC)
    lo, hi = P.min(0), P.max(0)
    centre = (lo + hi) / 2
    radius = np.linalg.norm(hi - lo) / 2
    pal = np.array([p[:3] for p in model.palette]); emit = np.array([p[3] for p in model.palette])
    tris = P.reshape(-1, 3, 3); nrm = N.reshape(-1, 3, 3).mean(1); col = C.reshape(-1, 3)[:, 0]
    for yaw, pitch in views:
        y, p = math.radians(yaw), math.radians(pitch)
        # camera looks at centre from direction (sin(yaw), sin(pitch), cos(yaw))
        fwd = -np.array([math.sin(y) * math.cos(p), math.sin(p), math.cos(y) * math.cos(p)])
        right = np.cross(np.array([0, 1, 0]), fwd); right /= np.linalg.norm(right)
        up = np.cross(fwd, right)
        cam = centre - fwd * radius * 3.2
        rel = tris - cam
        x = rel @ right; yv = rel @ up; z = rel @ fwd
        f = size * 1.45
        sx = size / 2 + f * x / z; sy = size / 2 - f * yv / z
        depth = z.mean(1)
        light = np.array([0.4, 0.8, 0.45]); light /= np.linalg.norm(light)
        lam = nrm @ light
        facing = (nrm @ -fwd)
        order = np.argsort(-depth)
        im = Image.new("RGB", (size, size), bg)
        d = ImageDraw.Draw(im)
        for i in order:
            c = pal[col[i]]
            shade = 1.0 if lam[i] > 0.15 else 0.62
            rim = 0.15 if facing[i] < 0.25 else 0.0
            rgb = c * shade + rim * 0.5
            if emit[col[i]] > 0: rgb = c * (1.1 + 0.3 * emit[col[i]])
            rgb = np.clip(rgb ** (1 / 2.2) * 255, 0, 255).astype(int)
            d.polygon([(sx[i, k], sy[i, k]) for k in range(3)], fill=tuple(rgb))
        imgs.append(im)
    out = Image.new("RGB", (size * len(imgs), size), bg)
    for k, im in enumerate(imgs): out.paste(im, (k * size, 0))
    out.save(path)
    return out
