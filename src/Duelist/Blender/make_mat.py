"""
Paints the Duelist playmat: duel_mat.jpg (colour) and duel_mat_glow.png (what glows).
The layout matches DuelBoard's zone coordinates exactly (board units -> pixels), so the
3D cards land inside the printed zones. Run:  python3 make_mat.py <out_dir>
"""
import math, random, sys, os
from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageChops
import numpy as np

OUT = sys.argv[1] if len(sys.argv) > 1 else "."
# mat extent in board units (DuelBoard local space: +x right, +z toward the enemy)
X0, X1 = -15.4, 15.4
Z0, Z1 = -2.8, 26.4
SS = 2                       # supersampling
W = 3072 * SS
H = int(round(W * (Z1 - Z0) / (X1 - X0)))
PPU = W / (X1 - X0)          # pixels per board unit

FONT = "/usr/share/texmf/fonts/opentype/public/tex-gyre/texgyrebonum-bold.otf"
if not os.path.exists(FONT):
    FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSerif-Bold.ttf"

def P(x, z):
    return ((x - X0) * PPU, (Z1 - z) * PPU)

def U(v):
    return v * PPU

random.seed(7)
rng = np.random.default_rng(7)

# ------------------------------------------------------------ background
yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
cx, cy = P(0, 11.0)
r = np.sqrt((xx - cx) ** 2 + ((yy - cy) * 1.05) ** 2) / (W * 0.62)
r = np.clip(r, 0, 1)
inner = np.array([34, 22, 66], np.float32)
outer = np.array([8, 6, 18], np.float32)
bg = inner[None, None, :] * (1 - r[..., None] ** 1.4) + outer[None, None, :] * (r[..., None] ** 1.4)
# a faint warm tint on your half, a faint crimson on theirs
side = np.clip((yy - cy) / (H * 0.5), -1, 1)[..., None]
bg += np.where(side > 0, side * np.array([10, 6, -4], np.float32), -side * np.array([16, -2, -2], np.float32))
# rubber-mat grain
grain = rng.normal(0, 3.2, (H // 4, W // 4)).astype(np.float32)
grain = np.array(Image.fromarray(grain).resize((W, H), Image.BILINEAR))
bg += grain[..., None]
img = Image.fromarray(np.clip(bg, 0, 255).astype(np.uint8), "RGB").convert("RGBA")
glow = Image.new("RGBA", (W, H), (0, 0, 0, 255))

GOLD = (226, 184, 92)
GOLD_D = (150, 112, 48)
TEAL = (70, 196, 184)
ORANGE = (236, 150, 64)
PURPLE = (176, 116, 236)
GREY = (170, 170, 186)
GREEN = (110, 200, 120)
BLUE = (120, 140, 230)
RED = (230, 90, 80)

def layer():
    return Image.new("RGBA", (W, H), (0, 0, 0, 0))

def comp(base, top):
    base.alpha_composite(top)

def font(px):
    return ImageFont.truetype(FONT, max(8, int(px)))

# ------------------------------------------------------------ arcane circle in the middle
def arcane(cxu, czu, rad, color, alpha, width, glow_alpha):
    lay, gl = layer(), layer()
    d, g = ImageDraw.Draw(lay), ImageDraw.Draw(gl)
    cxp, cyp = P(cxu, czu)
    R = U(rad)
    col = color + (alpha,)
    gcol = color + (glow_alpha,)
    for k, f in enumerate([1.0, 0.93, 0.62, 0.55, 0.30]):
        rr = R * f
        w = width * (1.6 if k in (0, 2) else 1.0)
        for dd, c in ((d, col), (g, gcol)):
            dd.ellipse([cxp - rr, cyp - rr, cxp + rr, cyp + rr], outline=c, width=int(w))
    # 8-point star and an inner square pair
    star = [(cxp + R * 0.93 * math.cos(a), cyp + R * 0.93 * math.sin(a)) for a in [i * 2 * math.pi / 8 - math.pi / 2 for i in range(8)]]
    for i in range(8):
        a, b = star[i], star[(i + 3) % 8]
        for dd, c in ((d, col), (g, gcol)):
            dd.line([a, b], fill=c, width=int(width))
    for off in (0, math.pi / 4):
        sq = [(cxp + R * 0.55 * math.cos(off + i * math.pi / 2), cyp + R * 0.55 * math.sin(off + i * math.pi / 2)) for i in range(4)]
        for dd, c in ((d, col), (g, gcol)):
            dd.polygon(sq, outline=c, width=int(width))
    # rune ticks between the two outer rings
    for i in range(72):
        a = i * 2 * math.pi / 72
        r1, r2 = R * 0.935, R * (0.995 if i % 6 == 0 else 0.965)
        p1 = (cxp + r1 * math.cos(a), cyp + r1 * math.sin(a))
        p2 = (cxp + r2 * math.cos(a), cyp + r2 * math.sin(a))
        d.line([p1, p2], fill=col, width=int(width))
        if i % 6 == 0:
            g.line([p1, p2], fill=gcol, width=int(width))
            dot = width * 2.2
            pc = (cxp + R * 0.78 * math.cos(a + math.pi / 72 * 3), cyp + R * 0.78 * math.sin(a + math.pi / 72 * 3))
            d.ellipse([pc[0] - dot, pc[1] - dot, pc[0] + dot, pc[1] + dot], fill=col)
    comp(img, lay)
    comp(glow, gl)

arcane(0, 11.0, 9.6, GOLD, 34, U(0.05), 26)
arcane(0, 11.0, 3.0, GOLD, 60, U(0.045), 70)

# ------------------------------------------------------------ border + stitching
def rrect(d, x0, z0, x1, z1, rad, **kw):
    a, b = P(x0, z1), P(x1, z0)
    d.rounded_rectangle([a[0], a[1], b[0], b[1]], radius=U(rad), **kw)

lay, gl = layer(), layer()
d, g = ImageDraw.Draw(lay), ImageDraw.Draw(gl)
rrect(d, X0 + 0.55, Z0 + 0.55, X1 - 0.55, Z1 - 0.55, 1.0, outline=GOLD + (230,), width=int(U(0.09)))
rrect(d, X0 + 0.80, Z0 + 0.80, X1 - 0.80, Z1 - 0.80, 0.8, outline=GOLD_D + (200,), width=int(U(0.035)))
rrect(g, X0 + 0.55, Z0 + 0.55, X1 - 0.55, Z1 - 0.55, 1.0, outline=GOLD + (110,), width=int(U(0.09)))
comp(img, lay)
comp(glow, gl)

# stitched edge: short light dashes all round, like a real playmat
lay = layer()
d = ImageDraw.Draw(lay)
inset = 0.22
x0p, y0p = P(X0 + inset, Z1 - inset)
x1p, y1p = P(X1 - inset, Z0 + inset)
dash, gap = U(0.22), U(0.13)
def dashes(a, b):
    L = math.dist(a, b)
    n = int(L // (dash + gap))
    for i in range(n):
        t0 = i * (dash + gap) / L
        t1 = (i * (dash + gap) + dash) / L
        p = (a[0] + (b[0] - a[0]) * t0, a[1] + (b[1] - a[1]) * t0)
        q = (a[0] + (b[0] - a[0]) * t1, a[1] + (b[1] - a[1]) * t1)
        d.line([p, q], fill=(200, 196, 210, 150), width=int(U(0.045)))
dashes((x0p, y0p), (x1p, y0p)); dashes((x1p, y0p), (x1p, y1p))
dashes((x1p, y1p), (x0p, y1p)); dashes((x0p, y1p), (x0p, y0p))
comp(img, lay)

# ------------------------------------------------------------ midline band
lay, gl = layer(), layer()
d, g = ImageDraw.Draw(lay), ImageDraw.Draw(gl)
for dz, w, a in ((0.0, 0.05, 200), (0.16, 0.022, 150), (-0.16, 0.022, 150)):
    p, q = P(X0 + 1.4, 11 + dz), P(X1 - 1.4, 11 + dz)
    d.line([p, q], fill=GOLD + (a,), width=int(U(w)))
    g.line([p, q], fill=GOLD + (a // 2,), width=int(U(w)))
for x in (-13.4, 13.4):
    c = P(x, 11)
    s = U(0.42)
    d.polygon([(c[0], c[1] - s), (c[0] + s, c[1]), (c[0], c[1] + s), (c[0] - s, c[1])], fill=GOLD + (220,))
comp(img, lay)
comp(glow, gl)

# ------------------------------------------------------------ zones
def glyph(d, kind, cx, cy, s, col):
    w = max(2, int(s * 0.07))
    if kind == "monster":      # crossed blades
        for sgn in (-1, 1):
            d.line([(cx - s * 0.42 * sgn, cy + s * 0.42), (cx + s * 0.36 * sgn, cy - s * 0.38)], fill=col, width=w * 2)
            d.line([(cx - s * 0.30 * sgn, cy + s * 0.20), (cx - s * 0.12 * sgn, cy + s * 0.38)], fill=col, width=w * 2)
    elif kind == "st":         # four-point star
        pts = []
        for i in range(8):
            a = i * math.pi / 4 - math.pi / 2
            rr = s * (0.48 if i % 2 == 0 else 0.14)
            pts.append((cx + rr * math.cos(a), cy + rr * math.sin(a)))
        d.polygon(pts, outline=col, width=w)
    elif kind == "deck":       # stacked cards
        for i in range(3):
            o = i * s * 0.09
            d.rounded_rectangle([cx - s * 0.28 + o, cy - s * 0.40 - o, cx + s * 0.28 + o, cy + s * 0.40 - o], radius=s * 0.05, outline=col, width=w)
    elif kind == "grave":      # headstone
        d.rounded_rectangle([cx - s * 0.30, cy - s * 0.42, cx + s * 0.30, cy + s * 0.42], radius=s * 0.28, outline=col, width=w)
        d.line([(cx, cy - s * 0.22), (cx, cy + s * 0.18)], fill=col, width=w)
        d.line([(cx - s * 0.13, cy - s * 0.08), (cx + s * 0.13, cy - s * 0.08)], fill=col, width=w)
    elif kind == "extra":      # hexagon
        pts = [(cx + s * 0.45 * math.cos(a), cy + s * 0.45 * math.sin(a)) for a in [i * math.pi / 3 + math.pi / 6 for i in range(6)]]
        d.polygon(pts, outline=col, width=w)
        pts2 = [(cx + s * 0.25 * math.cos(a), cy + s * 0.25 * math.sin(a)) for a in [i * math.pi / 3 + math.pi / 6 for i in range(6)]]
        d.polygon(pts2, outline=col, width=w)
    elif kind == "field":      # mountains
        d.line([(cx - s * 0.48, cy + s * 0.30), (cx - s * 0.15, cy - s * 0.30), (cx + s * 0.05, cy), (cx + s * 0.22, cy - s * 0.18), (cx + s * 0.48, cy + s * 0.30)], fill=col, width=w, joint="curve")
        d.line([(cx - s * 0.52, cy + s * 0.30), (cx + s * 0.52, cy + s * 0.30)], fill=col, width=w)
    elif kind == "banish":     # void ring
        d.ellipse([cx - s * 0.42, cy - s * 0.42, cx + s * 0.42, cy + s * 0.42], outline=col, width=w)
        d.ellipse([cx - s * 0.20, cy - s * 0.20, cx + s * 0.20, cy + s * 0.20], outline=col, width=w)
    elif kind == "emz":        # link arrows
        for a in range(4):
            ang = a * math.pi / 2
            px, py = cx + s * 0.36 * math.cos(ang), cy + s * 0.36 * math.sin(ang)
            tip = (cx + s * 0.50 * math.cos(ang), cy + s * 0.50 * math.sin(ang))
            l = (px + s * 0.12 * math.cos(ang + math.pi / 2), py + s * 0.12 * math.sin(ang + math.pi / 2))
            r_ = (px - s * 0.12 * math.cos(ang + math.pi / 2), py - s * 0.12 * math.sin(ang + math.pi / 2))
            d.polygon([tip, l, r_], fill=col)
        d.rounded_rectangle([cx - s * 0.2, cy - s * 0.2, cx + s * 0.2, cy + s * 0.2], radius=s * 0.04, outline=col, width=w)

def zone(x, z, w, h, color, label, kind, flip=False, faint=False):
    lay, gl = layer(), layer()
    d, g = ImageDraw.Draw(lay), ImageDraw.Draw(gl)
    a = 0.45 if faint else 1.0
    # darkened well with a soft colour wash
    rrect(d, x - w / 2, z - h / 2, x + w / 2, z + h / 2, 0.22, fill=(4, 3, 10, int(150 * a)))
    rrect(d, x - w / 2, z - h / 2, x + w / 2, z + h / 2, 0.22, fill=color + (int(26 * a),))
    # double frame: accent outside, gold inside
    rrect(d, x - w / 2, z - h / 2, x + w / 2, z + h / 2, 0.22, outline=color + (int(235 * a),), width=int(U(0.06)))
    rrect(d, x - w / 2 + 0.13, z - h / 2 + 0.13, x + w / 2 - 0.13, z + h / 2 - 0.13, 0.14, outline=GOLD + (int(150 * a),), width=int(U(0.022)))
    rrect(g, x - w / 2, z - h / 2, x + w / 2, z + h / 2, 0.22, outline=color + (int(200 * a),), width=int(U(0.06)))
    # corner diamonds
    for sx in (-1, 1):
        for sz in (-1, 1):
            c = P(x + sx * (w / 2 - 0.02), z + sz * (h / 2 - 0.02))
            s = U(0.13)
            d.polygon([(c[0], c[1] - s), (c[0] + s, c[1]), (c[0], c[1] + s), (c[0] - s, c[1])], fill=GOLD + (int(240 * a),))
    # watermark glyph
    c = P(x, z + 0.25 * (-1 if flip else 1))
    glyph(d, kind, c[0], c[1], U(min(w, h) * 0.62), color + (int(52 * a),))
    comp(img, lay)
    comp(glow, gl)
    # label along the near edge (rotated for the enemy side)
    if label:
        size = U(0.30 if len(label) < 12 else 0.24)
        f = font(size)
        lines = label.split("\n")
        tw = max(f.getbbox(l)[2] for l in lines)
        th = int(size * 1.15) * len(lines)
        t = Image.new("RGBA", (int(tw + size), int(th + size * 0.6)), (0, 0, 0, 0))
        td = ImageDraw.Draw(t)
        for i, l in enumerate(lines):
            lw = f.getbbox(l)[2]
            td.text(((t.width - lw) / 2, size * 0.2 + i * size * 1.15), l, font=f, fill=GOLD + (int(225 * a),))
        tg = t.copy()
        if flip:
            t = t.rotate(180)
            tg = tg.rotate(180)
        ly = z - h / 2 + 0.46 if not flip else z + h / 2 - 0.46
        cp = P(x, ly)
        pos = (int(cp[0] - t.width / 2), int(cp[1] - t.height / 2))
        img.alpha_composite(t, pos)
        gl2 = layer()
        gl2.alpha_composite(tg, pos)
        r_, g_, b_, al = gl2.split()
        al = al.point(lambda v: v * 0.45)
        glow.alpha_composite(Image.merge("RGBA", (r_, g_, b_, al)))

SP = 3.6
MW, MH = 2.8, 3.5        # monster zone
SW, SH = 2.8, 3.4        # spell / trap zone
for i in range(5):
    x = (i - 2) * SP
    zone(x, 7.0, MW, MH, ORANGE, "MONSTER ZONE", "monster")
    zone(x, 3.2, SW, SH, TEAL, "SPELL & TRAP\nZONE", "st")
    zone(x, 15.0, MW, MH, ORANGE, "MONSTER ZONE", "monster", flip=True)
    zone(x, 19.0, SW, SH, TEAL, "SPELL & TRAP\nZONE", "st", flip=True)
# your side (like a real mat): field top-left, graveyard top-right, extra bottom-left, deck bottom-right
zone(-12.4, 7.0, MW, MH, GREEN, "FIELD ZONE", "field")
zone(12.4, 7.0, MW, MH, GREY, "GRAVEYARD", "grave")
zone(-12.4, 3.2, SW, SH, PURPLE, "EXTRA DECK", "extra")
zone(12.4, 3.2, SW, SH, GOLD, "DECK", "deck")
zone(12.4, -0.75, SW, 2.4, BLUE, "BANISHED", "banish")
# their side, turned round
zone(12.4, 15.0, MW, MH, GREEN, "FIELD ZONE", "field", flip=True)
zone(-12.4, 15.0, MW, MH, GREY, "GRAVEYARD", "grave", flip=True)
zone(12.4, 19.0, SW, SH, PURPLE, "EXTRA DECK", "extra", flip=True)
zone(-12.4, 19.0, SW, SH, GOLD, "DECK", "deck", flip=True)
# extra monster zones on the midline
zone(-3.6, 11.0, 2.6, 3.0, PURPLE, "EXTRA\nMONSTER ZONE", "emz", faint=True)
zone(3.6, 11.0, 2.6, 3.0, PURPLE, "EXTRA\nMONSTER ZONE", "emz", faint=True)

# ------------------------------------------------------------ duelist circles
def circle_mark(x, z, rad, label, flip, color):
    lay, gl = layer(), layer()
    d, g = ImageDraw.Draw(lay), ImageDraw.Draw(gl)
    c = P(x, z)
    for f, w, a in ((1.0, 0.06, 210), (0.86, 0.025, 150)):
        R = U(rad * f)
        d.ellipse([c[0] - R, c[1] - R, c[0] + R, c[1] + R], outline=color + (a,), width=int(U(w)))
        g.ellipse([c[0] - R, c[1] - R, c[0] + R, c[1] + R], outline=color + (a // 2,), width=int(U(w)))
    comp(img, lay)
    comp(glow, gl)
    f = font(U(0.34))
    tw = f.getbbox(label)[2]
    t = Image.new("RGBA", (int(tw + 40), int(U(0.6))), (0, 0, 0, 0))
    ImageDraw.Draw(t).text((20, 4), label, font=f, fill=color + (230,))
    if flip:
        t = t.rotate(180)
    pz = z - rad + 0.42 if not flip else z + rad - 0.42
    p = P(x, pz)
    img.alpha_composite(t, (int(p[0] - t.width / 2), int(p[1] - t.height / 2)))

circle_mark(0, -0.9, 1.15, "DUELIST", False, GOLD)
circle_mark(0, 23.6, 2.15, "COMMANDER", True, RED)

# ------------------------------------------------------------ finish
img = img.convert("RGB")
glow = glow.convert("RGB")
glow = Image.blend(glow, glow.filter(ImageFilter.GaussianBlur(U(0.05))), 0.5)
fw, fh = W // SS, H // SS
img = img.resize((fw, fh), Image.LANCZOS)
glow = glow.resize((fw, fh), Image.LANCZOS)
img.save(os.path.join(OUT, "duel_mat.jpg"), quality=90, optimize=True)
glow.save(os.path.join(OUT, "duel_mat_glow.png"), optimize=True)
prev = img.resize((fw // 3, fh // 3), Image.LANCZOS)
prev.save(os.path.join(OUT, "duel_mat_preview.jpg"), quality=88)
print("mat", fw, fh)
