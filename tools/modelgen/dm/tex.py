"""Prozedurale, kachelbare PBR-Texturen (Dark Fantasy). Alle Muster wiederholen sich nahtlos.

Jedes Material liefert: Grundfarbe (sRGB), Normal Map (OpenGL, +Y), ORM (R=Okklusion, G=Rauheit,
B=Metall) und optional Emission. Ergebnisse werden zwischengespeichert (JPEG-Bytes).
"""
from __future__ import annotations

import io
import math
from functools import lru_cache

import numpy as np
from PIL import Image

S = 512  # Texturgröße


# ------------------------------------------------------------------ Rauschen (kachelbar)

def _grid(size=S):
    y, x = np.mgrid[0:size, 0:size].astype(np.float64)
    return x / size, y / size


def vnoise(cells, seed, size=S):
    rng = np.random.default_rng(seed)
    lat = rng.random((cells, cells))
    x, y = _grid(size)
    fx, fy = x * cells, y * cells
    ix, iy = np.floor(fx).astype(int), np.floor(fy).astype(int)
    tx, ty = fx - ix, fy - iy
    tx = tx * tx * (3 - 2 * tx)
    ty = ty * ty * (3 - 2 * ty)
    x0, y0 = ix % cells, iy % cells
    x1, y1 = (ix + 1) % cells, (iy + 1) % cells
    a = lat[y0, x0] * (1 - tx) + lat[y0, x1] * tx
    b = lat[y1, x0] * (1 - tx) + lat[y1, x1] * tx
    return a * (1 - ty) + b * ty


def fbm(cells=4, oct=5, seed=0, gain=0.5, size=S):
    out = np.zeros((size, size))
    amp, tot = 1.0, 0.0
    c = cells
    for o in range(oct):
        out += vnoise(c, seed + o * 131, size) * amp
        tot += amp
        amp *= gain
        c *= 2
    return out / tot


def ridged(cells=4, oct=5, seed=0, size=S):
    out = np.zeros((size, size))
    amp, tot, c = 1.0, 0.0, cells
    for o in range(oct):
        n = vnoise(c, seed + o * 77, size)
        out += (1 - np.abs(n * 2 - 1)) ** 2 * amp
        tot += amp
        amp *= 0.5
        c *= 2
    return out / tot


def worley(cells, seed, size=S, jitter=1.0):
    """F1, F2 (in Zellmaßen) und Zellkennung, nahtlos."""
    rng = np.random.default_rng(seed)
    pts = rng.random((cells, cells, 2)) * jitter + (1 - jitter) / 2
    ids = rng.random((cells, cells))
    x, y = _grid(size)
    fx, fy = x * cells, y * cells
    ix, iy = np.floor(fx).astype(int), np.floor(fy).astype(int)
    f1 = np.full((size, size), 9.0)
    f2 = np.full((size, size), 9.0)
    cid = np.zeros((size, size))
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            cx, cy = ix + dx, iy + dy
            p = pts[cy % cells, cx % cells]
            d = np.hypot(cx + p[..., 0] - fx, cy + p[..., 1] - fy)
            closer = d < f1
            f2 = np.where(closer, f1, np.minimum(f2, d))
            cid = np.where(closer, ids[cy % cells, cx % cells], cid)
            f1 = np.where(closer, d, f1)
    return f1, f2, cid


def lerp(a, b, t):
    t = np.asarray(t)[..., None] if np.ndim(a) else t
    return a * (1 - t) + b * t


def col(hexv):
    return np.array([(hexv >> 16) & 255, (hexv >> 8) & 255, hexv & 255], dtype=np.float64) / 255.0


def mix(c1, c2, t):
    """Farbverlauf zwischen zwei Farben (3,) nach Maske t (H×W)."""
    t = np.clip(t, 0, 1)[..., None]
    return c1 * (1 - t) + c2 * t


def smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


def normal_from_height(h, strength=4.0):
    dx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * strength
    dy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * strength
    n = np.stack([-dx, dy, np.ones_like(h)], axis=-1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return n * 0.5 + 0.5


def ao_from_height(h, k=1.6):
    blur = h.copy()
    for s in (4, 8, 16):
        blur = (blur + np.roll(blur, s, 0) + np.roll(blur, -s, 0) + np.roll(blur, s, 1) + np.roll(blur, -s, 1)) / 5
    return np.clip(1 - (blur - h) * k, 0.35, 1)


# ------------------------------------------------------------------ Materialrezepte
# Jedes Rezept: () -> dict(base=H×W×3, height=H×W, rough=H×W, metal=H×W, emis=H×W×3|None)

def _stone_blocks(seed=1, tint=0x4a4650, rows=6, mortar=0x1c1a20):
    x, y = _grid()
    ry = y * rows
    row = np.floor(ry)
    off = (row % 2) * 0.5
    cols = rows / 2
    rx = x * cols + off
    bx, by = rx - np.floor(rx), ry - row
    bid = (np.floor(rx) % cols) * 13.7 + row * 7.1
    rnd = (np.sin(bid * 12.9898) * 43758.5453) % 1.0
    edge = np.minimum(np.minimum(bx, 1 - bx) * (1 / cols) * rows, np.minimum(by, 1 - by))
    n = fbm(8, 5, seed)
    chip = fbm(16, 4, seed + 3)
    e = edge + (chip - 0.5) * 0.08
    groove = smooth(0.02, 0.09, e)
    h = groove * (0.75 + 0.25 * n) + (rnd * 0.08)
    c = col(tint) * (0.78 + 0.35 * rnd[..., None]) * (0.8 + 0.4 * n[..., None])
    moss = smooth(0.62, 0.78, fbm(5, 4, seed + 9)) * (1 - groove * 0.6)
    c = mix(c, col(0x2c3424), moss * 0.55)
    c = mix(col(mortar), c, groove)
    grime = fbm(3, 3, seed + 21)
    c *= (0.75 + 0.35 * grime)[..., None]
    rough = 0.78 + 0.18 * (1 - groove) * 0 + 0.15 * n
    return dict(base=c, height=h, rough=np.clip(rough, 0, 1), metal=np.zeros_like(h), nstr=5.0)


def _rock(seed=2, tint=0x55505a, dark=0x26232a):
    n = fbm(4, 6, seed)
    f1, f2, cid = worley(6, seed + 1)
    cracks = smooth(0.0, 0.08, f2 - f1)
    h = n * 0.7 + cracks * 0.3 + cid * 0.08
    c = mix(col(dark), col(tint), n * 1.2 - 0.1) * (0.85 + 0.3 * cid[..., None])
    c = mix(col(0x141216), c, cracks * 0.8 + 0.2)
    return dict(base=c, height=h, rough=0.85 + 0.1 * n, metal=np.zeros_like(h), nstr=6.0)


def _iron(seed=3, tint=0x3a3b40, rust=0.0):
    n = fbm(6, 5, seed)
    f1, f2, cid = worley(10, seed + 5)
    dents = smooth(0.0, 0.5, f1)
    x, y = _grid()
    scratches = np.clip(ridged(3, 3, seed + 7) ** 6 * 1.4, 0, 1)
    h = dents * 0.25 + n * 0.15 - scratches * 0.05
    c = col(tint) * (0.85 + 0.25 * n[..., None]) * (0.9 + 0.15 * cid[..., None])
    c = c + scratches[..., None] * 0.18
    metal = np.full_like(h, 0.95)
    rough = 0.42 + 0.25 * n - scratches * 0.2
    if rust > 0:
        rm = smooth(0.62 - rust * 0.12, 0.72 - rust * 0.1, fbm(5, 5, seed + 11) + (1 - dents) * 0.08)
        rust_c = mix(col(0x2e1a12), col(0x6a3a20), fbm(16, 3, seed + 13))
        c = mix(c, rust_c, rm)
        metal = metal * (1 - rm)
        rough = rough * (1 - rm) + 0.9 * rm
        h = h + rm * fbm(24, 3, seed + 17) * 0.2
    return dict(base=c, height=h, rough=np.clip(rough, 0.15, 1), metal=metal, nstr=3.0)


def _gold(seed=4):
    n = fbm(6, 5, seed)
    f1, f2, _ = worley(12, seed)
    tarnish = smooth(0.4, 0.8, fbm(4, 4, seed + 3))
    c = mix(col(0x9a7a3c), col(0x4a3a22), tarnish * 0.8) * (0.85 + 0.3 * n[..., None])
    h = smooth(0, 0.6, f1) * 0.2 + n * 0.1
    return dict(base=c, height=h, rough=0.3 + 0.35 * tarnish, metal=np.full_like(h, 1.0), nstr=2.5)


def _wood(seed=5, tint=0x3a2a1e):
    x, y = _grid()
    planks = 5
    px = x * planks
    pid = np.floor(px)
    rnd = (np.sin(pid * 91.7 + seed) * 43758.5) % 1
    warp = fbm(4, 4, seed) * 0.5
    grain = 0.5 + 0.5 * np.sin((x * planks * 9 + warp * 3 + rnd * 7) * math.pi * 2 + fbm(16, 3, seed + 2) * 2)
    gap = smooth(0.0, 0.04, np.minimum(px - pid, 1 - (px - pid)))
    knots = smooth(0.12, 0.0, worley(5, seed + 3)[0])
    h = gap * (0.6 + 0.3 * grain) - knots * 0.3
    c = col(tint) * (0.65 + 0.45 * grain[..., None]) * (0.8 + 0.4 * rnd[..., None])
    c = mix(c, col(0x140c08), (1 - gap) + knots * 0.6)
    return dict(base=c, height=h, rough=0.75 + 0.15 * grain, metal=np.zeros_like(h), nstr=4.0)


def _leather(seed=6, tint=0x3e2a1e):
    f1, f2, cid = worley(18, seed)
    crease = smooth(0.0, 0.12, f2 - f1)
    n = fbm(6, 4, seed + 1)
    wear = smooth(0.55, 0.8, fbm(3, 4, seed + 2))
    h = crease * 0.4 + n * 0.3
    c = col(tint) * (0.75 + 0.35 * n[..., None]) * (0.85 + 0.2 * crease[..., None])
    c = mix(c, c * 1.5, wear * 0.4)
    return dict(base=c, height=h, rough=0.7 - wear * 0.2 + 0.1 * n, metal=np.zeros_like(h), nstr=3.0)


def _cloth(seed=7, tint=0x5a1418):
    x, y = _grid()
    weave = (np.sin(x * S * math.pi / 2) * np.sin(y * S * math.pi / 2)) * 0.5 + 0.5
    n = fbm(6, 5, seed)
    dirt = smooth(0.5, 0.85, fbm(3, 4, seed + 5))
    h = weave * 0.2 + n * 0.5
    c = col(tint) * (0.7 + 0.4 * n[..., None]) * (0.9 + 0.1 * weave[..., None])
    c = mix(c, col(0x1a1410), dirt * 0.5)
    return dict(base=c, height=h, rough=np.full_like(h, 0.92), metal=np.zeros_like(h), nstr=2.0)


def _bone(seed=8):
    n = fbm(5, 5, seed)
    f1, f2, _ = worley(7, seed + 2)
    cracks = smooth(0.0, 0.05, f2 - f1)
    pores = smooth(0.06, 0.0, worley(40, seed + 3)[0]) * 0.4
    h = n * 0.6 + cracks * 0.2 - pores * 0.2
    c = mix(col(0x6a5e48), col(0xc8b896), n * 1.3 - 0.15)
    c = mix(col(0x2a2418), c, cracks * 0.85 + 0.15) * (1 - pores[..., None] * 0.4)
    return dict(base=c, height=h, rough=0.65 + 0.2 * n, metal=np.zeros_like(h), nstr=3.5)


def _flesh(seed=9, tint=0x5a1e24, vein=0x1e0a14):
    n = fbm(5, 5, seed)
    veins = ridged(3, 5, seed + 1)
    v = smooth(0.55, 0.85, veins)
    f1, _, _ = worley(14, seed + 2)
    bumps = smooth(0.0, 0.6, f1)
    h = bumps * 0.4 + n * 0.4 + v * 0.2
    c = col(tint) * (0.7 + 0.5 * n[..., None])
    c = mix(c, col(vein), v * 0.8)
    return dict(base=c, height=h, rough=0.45 + 0.25 * (1 - n), metal=np.zeros_like(h), nstr=4.0)


def _chitin(seed=10, tint=0x231c2a, edge=0x5a4a6a):
    f1, f2, cid = worley(7, seed, jitter=0.7)
    plate = smooth(0.0, 0.1, f2 - f1)
    n = fbm(8, 4, seed + 1)
    h = plate * (0.6 + 0.4 * (1 - f1)) + n * 0.1
    c = col(tint) * (0.8 + 0.4 * cid[..., None])
    c = mix(col(edge) * 0.6, c, plate)
    c = c + (1 - f1)[..., None] * 0.05
    return dict(base=c, height=h, rough=0.28 + 0.2 * n, metal=np.full_like(h, 0.15), nstr=5.0)


def _skin(seed=11, tint=0xb08a74):
    n = fbm(8, 5, seed)
    pores = smooth(0.08, 0.0, worley(60, seed + 1)[0])
    blotch = fbm(3, 3, seed + 2)
    h = n * 0.3 - pores * 0.1
    c = col(tint) * (0.9 + 0.15 * blotch[..., None]) * (0.95 + 0.08 * n[..., None])
    c = mix(c, c * np.array([1.05, 0.85, 0.85]), smooth(0.55, 0.8, blotch) * 0.4)
    return dict(base=c, height=h, rough=0.55 + 0.15 * n, metal=np.zeros_like(h), nstr=1.5)


def _fur(seed=12, tint=0x2a2420):
    x, y = _grid()
    strands = fbm(64, 2, seed) * 0.6 + vnoise(8, seed + 1) * 0.4
    streak = np.abs(np.sin((x * 90 + fbm(4, 3, seed + 2) * 6) * math.pi))
    h = strands * 0.6 + streak * 0.4
    c = col(tint) * (0.6 + 0.6 * h[..., None])
    return dict(base=c, height=h, rough=np.full_like(h, 0.9), metal=np.zeros_like(h), nstr=3.0)


def _crystal(seed=13, tint=0x8a40ff, glow=0xc890ff):
    f1, f2, cid = worley(5, seed, jitter=0.9)
    facets = smooth(0.0, 0.05, f2 - f1)
    n = fbm(4, 5, seed + 1)
    inner = smooth(0.35, 0.75, n)
    h = facets * 0.5 + cid * 0.3
    c = col(tint) * (0.35 + 0.5 * cid[..., None])
    em = col(glow) * (inner * 0.8 + (1 - facets) * 0.6)[..., None]
    return dict(base=c, height=h, rough=0.08 + 0.1 * n, metal=np.zeros_like(h), emis=em, nstr=4.0)


def _ember(seed=14):
    f1, f2, cid = worley(9, seed)
    crack = smooth(0.09, 0.0, f2 - f1)
    n = fbm(6, 5, seed + 1)
    h = (1 - crack) * (0.6 + 0.4 * n)
    c = mix(col(0x16100e), col(0x30201a), n)
    c = mix(c, col(0x6a1e08), crack)
    heat = crack * (0.6 + 0.6 * n) + smooth(0.7, 0.95, n) * 0.25
    em = mix(col(0xff3a08), col(0xffc060), crack * n) * heat[..., None]
    return dict(base=c, height=h, rough=0.85 - crack * 0.4, metal=np.zeros_like(h), emis=em, nstr=6.0)


def _runes(seed=15, tint=0x2c2834, glow=0x9a60ff):
    base = _stone_blocks(seed, tint, rows=4)
    rng = np.random.default_rng(seed)
    x, y = _grid()
    g = 8
    gx, gy = x * g, y * g
    cx, cy = gx - np.floor(gx), gy - np.floor(gy)
    gid = (np.floor(gx) + np.floor(gy) * g).astype(int)
    rune = np.zeros_like(x)
    strokes = rng.random((g * g, 4, 4))
    show = rng.random(g * g) < 0.35
    for k in range(4):
        s = strokes[gid, k]
        x0, y0, x1, y1 = 0.25 + s[..., 0] * 0.5, 0.2 + s[..., 1] * 0.2, 0.25 + s[..., 2] * 0.5, 0.6 + s[..., 3] * 0.2
        px, py = cx - x0, cy - y0
        dx, dy = x1 - x0, y1 - y0
        t = np.clip((px * dx + py * dy) / (dx * dx + dy * dy + 1e-6), 0, 1)
        d = np.hypot(px - dx * t, py - dy * t)
        rune = np.maximum(rune, smooth(0.05, 0.02, d))
    rune *= show[gid]
    base['height'] = base['height'] - rune * 0.25
    base['base'] = mix(base['base'], col(glow) * 0.4, rune)
    base['emis'] = col(glow)[None, None, :] * (rune * 0.9)[..., None]
    return base


def _bark(seed=16, tint=0x2e241a):
    x, y = _grid()
    r = ridged(4, 5, seed)
    strip = np.abs(np.sin((x * 14 + fbm(3, 4, seed + 1) * 2.5) * math.pi))
    h = strip * 0.5 + r * 0.5
    c = col(tint) * (0.55 + 0.6 * h[..., None])
    moss = smooth(0.55, 0.75, fbm(4, 5, seed + 3)) * smooth(0.3, 0.6, h)
    c = mix(c, col(0x3a4a22), moss * 0.7)
    return dict(base=c, height=h, rough=np.full_like(h, 0.92), metal=np.zeros_like(h), nstr=7.0)


def _ice(seed=17):
    n = fbm(4, 6, seed)
    f1, f2, _ = worley(5, seed + 1)
    cracks = smooth(0.0, 0.03, f2 - f1)
    h = n * 0.4 + cracks * 0.2
    c = mix(col(0x5a8aa8), col(0xc8e8f4), n)
    c = mix(col(0xeaf8ff), c, cracks * 0.7 + 0.3)
    em = col(0x60a8d8) * (smooth(0.5, 0.9, n) * 0.15)[..., None]
    return dict(base=c, height=h, rough=0.12 + 0.15 * n, metal=np.zeros_like(h), emis=em, nstr=2.0)


def _void(seed=18):
    n = fbm(3, 6, seed)
    stars = smooth(0.04, 0.0, worley(48, seed + 1)[0]) * (vnoise(48, seed + 2) > 0.7)
    swirl = smooth(0.45, 0.8, fbm(4, 5, seed + 3))
    c = mix(col(0x07050c), col(0x241238), swirl)
    em = col(0x7040c0) * (swirl * 0.35)[..., None] + col(0xe0d0ff) * stars[..., None]
    return dict(base=c, height=n * 0.2, rough=np.full_like(n, 0.2), metal=np.zeros_like(n), emis=em, nstr=1.0)


def _obsidian(seed=19):
    n = fbm(5, 5, seed)
    f1, f2, _ = worley(6, seed + 1)
    facets = smooth(0.0, 0.06, f2 - f1)
    c = col(0x0e0c12) * (0.8 + 0.5 * n[..., None])
    c = c + (1 - facets)[..., None] * 0.04
    return dict(base=c, height=facets * 0.4 + n * 0.2, rough=0.12 + 0.2 * n, metal=np.full_like(n, 0.1), nstr=5.0)


def _steel(seed=20, tint=0x5a5c64):
    m = _iron(seed, tint)
    m['rough'] = np.clip(m['rough'] - 0.15, 0.12, 1)
    return m


def _scales(seed=21, tint=0x2a3424, edge=0x10140c):
    x, y = _grid()
    k = 8
    sx, sy = x * k, y * k
    row = np.floor(sy)
    off = (row % 2) * 0.5
    cx = (sx + off) - np.floor(sx + off) - 0.5
    cy = sy - row
    d = np.hypot(cx, cy * 1.2)
    sc = smooth(0.55, 0.35, d)
    n = fbm(6, 4, seed)
    h = sc * (1 - cy) * 0.8 + n * 0.2
    c = col(tint) * (0.8 + 0.8 * h[..., None])
    c = mix(col(edge), c, sc * 0.85 + 0.15)
    return dict(base=c, height=h, rough=0.4 + 0.2 * n, metal=np.full_like(h, 0.05), nstr=5.0)


def _glow_plain(seed=22, tint=0xffffff):
    n = fbm(4, 4, seed)
    c = col(tint)[None, None, :] * (0.7 + 0.3 * n[..., None])
    return dict(base=c * 0.6, height=n * 0.1, rough=np.full_like(n, 0.3), metal=np.zeros_like(n), emis=c, nstr=0.5)


# ---------------------------------------------------------------- Sci-Fi (Thema „Sternenwacht“)

def _hull(seed=60, tint=0xd8dce4, seam=0x3a3e48, metal=0.35, rough=0.45, stripes=False):
    """Rumpfplatten: unregelmäßige Plattenfelder mit Fugen, Nieten und leichter Gebrauchsspur."""
    rng = np.random.default_rng(seed)
    x, y = _grid()
    h = np.zeros_like(x)
    seams = np.zeros_like(x)
    # zwei Teilungsebenen: große Platten, dann zufällige Unterteilung
    for k, g in ((0, 4), (1, 8)):
        gx, gy = x * g, y * g
        fx, fy = gx - np.floor(gx), gy - np.floor(gy)
        gid = (np.floor(gx) + np.floor(gy) * g).astype(int)
        keep = rng.random(g * g) < (1.0 if k == 0 else 0.35)
        ex = np.minimum(fx, 1 - fx)
        ey = np.minimum(fy, 1 - fy)
        e = np.minimum(ex, ey)
        line = smooth(0.03, 0.012, e) * keep[gid]
        seams = np.maximum(seams, line)
        # Nieten in den Ecken großer Platten
        if k == 0:
            rv = np.hypot(np.minimum(fx, 1 - fx) - 0.06, np.minimum(fy, 1 - fy) - 0.06)
            h += smooth(0.025, 0.012, rv) * 0.35
        h += rng.random(g * g)[gid] * 0.04
    n = fbm(8, 4, seed)
    wear = smooth(0.62, 0.8, fbm(5, 5, seed + 3))
    h = h + n * 0.05 - seams * 0.5
    c = col(tint) * (0.9 + 0.12 * n[..., None]) * (1 - 0.18 * wear[..., None])
    if stripes:
        st = np.floor((x + y) * 18) % 2
        band = ((y > 0.86) | (y < 0.06))[..., None]
        c = np.where(band, mix(col(0x1a1a1a), col(0xf0b020), st), c)
    c = mix(c, col(seam), seams * 0.85)
    return dict(base=c, height=h, rough=np.clip(rough + 0.25 * wear - 0.1 * seams, 0.1, 1), metal=np.full_like(h, metal), nstr=4.0)


def _circuit(seed=66, tint=0x1a1e26, glow=0x40e0ff):
    """Dunkle Platine mit leuchtenden Leiterbahnen."""
    base = _hull(seed, tint, 0x0a0c10, 0.5, 0.35)
    rng = np.random.default_rng(seed)
    x, y = _grid()
    g = 16
    gx, gy = x * g, y * g
    fx, fy = gx - np.floor(gx), gy - np.floor(gy)
    gid = (np.floor(gx) + np.floor(gy) * g).astype(int)
    hor = rng.random(g * g) < 0.45
    ver = rng.random(g * g) < 0.45
    pad = rng.random(g * g) < 0.12
    tr = np.maximum(smooth(0.07, 0.03, np.abs(fy - 0.5)) * hor[gid], smooth(0.07, 0.03, np.abs(fx - 0.5)) * ver[gid])
    tr = np.maximum(tr, smooth(0.2, 0.12, np.hypot(fx - 0.5, fy - 0.5)) * pad[gid])
    base['base'] = mix(base['base'], col(glow) * 0.35, tr)
    base['emis'] = col(glow)[None, None, :] * (tr * 0.85)[..., None]
    base['height'] = base['height'] + tr * 0.1
    return base


def _alien_skin(seed=70, tint=0x3a5a4a, spot=0x9aff70):
    """Feuchte Alienhaut mit leuchtenden Flecken."""
    x, y = _grid()
    f1, f2, cid = worley(10, seed)
    n = fbm(6, 5, seed + 1)
    w = np.clip(f1, 0, 1)
    cells = smooth(0.22, 0.05, f1) * (cid > 0.55)
    h = n * 0.6 + (1 - w) * 0.4
    c = col(tint) * (0.7 + 0.5 * n[..., None])
    c = mix(c, col(spot) * 0.5, cells * 0.8)
    return dict(base=c, height=h, rough=np.clip(0.25 + 0.2 * n, 0.1, 1), metal=np.zeros_like(h), emis=col(spot)[None, None, :] * (cells * 0.6)[..., None], nstr=4.0)


def _glass(seed=74, tint=0x6ad0ff):
    n = fbm(4, 3, seed)
    c = col(tint)[None, None, :] * (0.35 + 0.15 * n[..., None])
    return dict(base=c, height=n * 0.05, rough=np.full_like(n, 0.08), metal=np.full_like(n, 0.2), emis=col(tint)[None, None, :] * 0.25, nstr=0.5)


# Materialbibliothek: Name → (Rezept, Parameter, UV-Wiederholungen je Meter, Faktoren)
LIB = {
    'stone_blocks': (_stone_blocks, dict(seed=1), 1.6),
    'stone_dark': (_stone_blocks, dict(seed=31, tint=0x302c36, rows=5), 1.6),
    'stone_pale': (_stone_blocks, dict(seed=32, tint=0x7a7268, rows=6, mortar=0x2a2620), 1.6),
    'rock': (_rock, dict(seed=2), 1.8),
    'rock_red': (_rock, dict(seed=33, tint=0x5a3a30, dark=0x22140f), 1.2),
    'iron': (_iron, dict(seed=3, tint=0x6a6c72), 2.0),
    'iron_rust': (_iron, dict(seed=34, tint=0x5e5a56, rust=0.7), 2.0),
    'steel': (_steel, dict(seed=20, tint=0x92949c), 2.5),
    'gold': (_gold, dict(seed=4), 3.0),
    'wood': (_wood, dict(seed=5), 1.6),
    'wood_black': (_wood, dict(seed=35, tint=0x1e1612), 1.6),
    'leather': (_leather, dict(seed=6), 2.5),
    'leather_black': (_leather, dict(seed=36, tint=0x1c1614), 2.5),
    'cloth_red': (_cloth, dict(seed=7, tint=0x5a1418), 2.0),
    'cloth_violet': (_cloth, dict(seed=37, tint=0x34184a), 2.0),
    'cloth_dark': (_cloth, dict(seed=38, tint=0x1e1c22), 2.0),
    'cloth_green': (_cloth, dict(seed=39, tint=0x1e3020), 2.0),
    'cloth_blue': (_cloth, dict(seed=40, tint=0x182a48), 2.0),
    'bone': (_bone, dict(seed=8), 2.0),
    'flesh': (_flesh, dict(seed=9), 3.0),
    'flesh_grey': (_flesh, dict(seed=41, tint=0x4a4648, vein=0x1a1218), 3.0),
    'flesh_moss': (_flesh, dict(seed=42, tint=0x2a3222, vein=0x141a0c), 3.0),
    'chitin': (_chitin, dict(seed=10), 2.2),
    'chitin_red': (_chitin, dict(seed=43, tint=0x3a1410, edge=0x8a3a20), 2.2),
    'chitin_blue': (_chitin, dict(seed=44, tint=0x141c2c, edge=0x3a5a7a), 2.2),
    'skin': (_skin, dict(seed=11, tint=0x8e766a), 4.0),
    'skin_dark': (_skin, dict(seed=45, tint=0x5e4838), 4.0),
    'skin_pale': (_skin, dict(seed=46, tint=0x6c6a74), 4.0),
    'fur': (_fur, dict(seed=12), 2.5),
    'fur_grey': (_fur, dict(seed=47, tint=0x4a4648), 2.5),
    'crystal_violet': (_crystal, dict(seed=13), 2.0),
    'crystal_cyan': (_crystal, dict(seed=48, tint=0x2a8a9a, glow=0x70f0ff), 2.0),
    'crystal_red': (_crystal, dict(seed=49, tint=0x8a1a1a, glow=0xff5a3a), 2.0),
    'crystal_green': (_crystal, dict(seed=50, tint=0x2a6a2a, glow=0x90ff70), 2.0),
    'crystal_gold': (_crystal, dict(seed=51, tint=0x8a6a1a, glow=0xffd860), 2.0),
    'ember': (_ember, dict(seed=14), 1.5),
    'runes': (_runes, dict(seed=15), 1.4),
    'runes_cyan': (_runes, dict(seed=52, tint=0x24282e, glow=0x50e0ff), 1.4),
    'bark': (_bark, dict(seed=16), 1.8),
    'ice': (_ice, dict(seed=17), 1.2),
    'void': (_void, dict(seed=18), 1.0),
    'obsidian': (_obsidian, dict(seed=19), 1.5),
    'scales': (_scales, dict(seed=21), 1.4),
    'scales_ash': (_scales, dict(seed=53, tint=0x3a3430, edge=0x120e0c), 1.4),
    'glow_white': (_glow_plain, dict(seed=22, tint=0xf0f0ff), 1.0),
    'glow_fire': (_glow_plain, dict(seed=54, tint=0xff7a20), 1.0),
    'glow_arcane': (_glow_plain, dict(seed=55, tint=0xb070ff), 1.0),
    'glow_frost': (_glow_plain, dict(seed=56, tint=0x80d8ff), 1.0),
    'glow_poison': (_glow_plain, dict(seed=57, tint=0x90ff50), 1.0),
    'glow_storm': (_glow_plain, dict(seed=58, tint=0x9ad0ff), 1.0),
    'glow_blood': (_glow_plain, dict(seed=59, tint=0xff2a2a), 1.0),
    # Sci-Fi
    'hull_white': (_hull, dict(seed=60), 1.4),
    'hull_dark': (_hull, dict(seed=61, tint=0x30343e, seam=0x101218, metal=0.55, rough=0.4), 1.4),
    'hull_blue': (_hull, dict(seed=62, tint=0x2a4a7a, seam=0x0e1a2e, metal=0.45), 1.4),
    'hull_orange': (_hull, dict(seed=63, tint=0xd8742a, seam=0x40200a, metal=0.3), 1.4),
    'hull_hazard': (_hull, dict(seed=64, tint=0x5a5e68, seam=0x202228, metal=0.5, stripes=True), 1.4),
    'gunmetal': (_iron, dict(seed=65, tint=0x3a3e48), 2.4),
    'circuit': (_circuit, dict(seed=66), 1.2),
    'circuit_red': (_circuit, dict(seed=67, glow=0xff4a3a), 1.2),
    'circuit_green': (_circuit, dict(seed=68, glow=0x70ff60), 1.2),
    'circuit_violet': (_circuit, dict(seed=69, glow=0xc070ff), 1.2),
    'alien_skin': (_alien_skin, dict(seed=70), 2.5),
    'alien_violet': (_alien_skin, dict(seed=71, tint=0x4a2a5a, spot=0xff70e0), 2.5),
    'alien_shell': (_chitin, dict(seed=72, tint=0x1a2a30, edge=0x40d0c0), 2.2),
    'glass': (_glass, dict(seed=74), 1.0),
    'glass_green': (_glass, dict(seed=75, tint=0x70ff90), 1.0),
    'glow_cyan': (_glow_plain, dict(seed=76, tint=0x40f0ff), 1.0),
    'glow_magenta': (_glow_plain, dict(seed=77, tint=0xff40c0), 1.0),
    'glow_orange': (_glow_plain, dict(seed=78, tint=0xffa030), 1.0),
}


def uv_scale(name: str) -> float:
    return LIB[name][2]


def _enc(arr, quality=88, mode='RGB'):
    a = np.clip(arr * 255 + 0.5, 0, 255).astype(np.uint8)
    im = Image.fromarray(a, mode)
    b = io.BytesIO()
    im.save(b, 'JPEG', quality=quality, subsampling=0 if quality >= 90 else 2, optimize=True)
    return b.getvalue()


@lru_cache(maxsize=None)
def build(name: str):
    """Liefert dict mit JPEG-Bytes: base, normal, orm, emis (oder None) sowie Faktoren."""
    fn, kw, _ = LIB[name]
    r = fn(**kw)
    h = r['height']
    base = np.clip(r['base'], 0, 1)
    ao = ao_from_height(h)
    # Okklusion in die Grundfarbe leicht einrechnen (wirkt in jeder Engine)
    base = base * (0.75 + 0.25 * ao[..., None])
    nrm = normal_from_height(h, r.get('nstr', 4.0))
    orm = np.stack([ao, np.clip(r['rough'], 0.04, 1), np.clip(r['metal'], 0, 1)], axis=-1)
    em = r.get('emis')
    out = dict(
        base=_enc(base ** (1 / 1.0)),
        normal=_enc(nrm, 92),
        orm=_enc(orm, 90),
        emis=_enc(np.clip(em, 0, 1)) if em is not None else None,
    )
    return out


def preview_png(name: str) -> bytes:
    b = build(name)
    return b['base']
