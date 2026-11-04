"""Bausatz für harte Oberflächen (Türme, Waffen, Rüstung) – wiederverwendbare Teile im Dark-Fantasy-Stil."""
from __future__ import annotations

import math
from functools import lru_cache

import numpy as np

from .geo import (TAU, Mesh, crystal, curve, cylinder, displace, extrude, icosphere, lathe, merge, rbox, ring_array,
                  sphere, spike, torus, tube, weld_normals)
from .sdf import Sculpt, rot


def M(m: Mesh) -> Mesh:
    return m.copy()


# ------------------------------------------------------------------ Mauerwerk

def plinth(r=0.46, h=0.16, sides=8, bevel=0.025):
    """Achteckiger Sockel mit Fase und Stufe."""
    prof = [(0.0, 0.0), (r, 0.0), (r, h * 0.55), (r - bevel, h * 0.62), (r * 0.9, h * 0.62), (r * 0.9, h), (0.0, h)]
    return lathe(prof, sides, cap_bottom=False, cap_top=False, a0=math.pi / sides)


def body(r0=0.36, r1=0.3, h=0.8, sides=8, y=0.0, flare=0.0):
    """Turmschaft (leicht verjüngt), optional mit ausgestelltem Fuß."""
    prof = [(0.0, 0.0), (r0 + flare, 0.0), (r0, 0.08 + flare * 0.6), (r1, h), (0.0, h)]
    return lathe(prof, sides, cap_bottom=False, cap_top=False, a0=math.pi / sides).t(0, y, 0)


def band(r, y, h=0.035, t=0.02, sides=8):
    """Eisenring um den Schaft."""
    prof = [(r - 0.005, -h / 2), (r + t, -h / 2), (r + t + 0.005, 0), (r + t, h / 2), (r - 0.005, h / 2)]
    return lathe(prof, sides, cap_bottom=False, cap_top=False, a0=math.pi / sides, smooth_all=False).t(0, y, 0)


def crenels(r, y, n=8, w=0.11, hgt=0.12, d=0.09):
    """Zinnenkranz."""
    out = []
    for i in range(n):
        a = TAU * i / n
        b = rbox(w, hgt, d, 0.012).ry(-a + math.pi / 2).t(math.cos(a) * r, y + hgt / 2, math.sin(a) * r)
        out.append(b)
    return merge(out)


def parapet(r, y, h=0.07, t=0.07, sides=8):
    prof = [(r - t, 0), (r + 0.015, 0), (r + 0.02, h * 0.5), (r, h), (r - t, h)]
    return lathe(prof, sides, cap_bottom=False, cap_top=True, a0=math.pi / sides).t(0, y, 0)


def buttresses(r, n=4, h=0.5, w=0.08, d=0.12, phase=0.0, y=0.0):
    out = []
    for i in range(n):
        a = phase + TAU * i / n
        prof = np.array([(0, 0), (d, 0), (d * 0.35, h), (0, h * 1.05)])
        b = extrude(prof, w)  # Profil in XY (x = nach außen), Breite entlang Z
        b = b.ry(-a).t(math.cos(a) * (r - 0.03), y, math.sin(a) * (r - 0.03))
        out.append(b)
    return merge(out)


def slits(r, y, n=4, phase=0.0, w=0.035, h=0.14):
    out = []
    for i in range(n):
        a = phase + TAU * i / n
        out.append(rbox(w, h, 0.03, 0.008).ry(-a + math.pi / 2).t(math.cos(a) * r, y, math.sin(a) * r))
    return merge(out)


def roof(r=0.4, h=0.5, sides=8, y=0.0, overhang=0.05, curve_in=0.25):
    pts = []
    k = 8
    for i in range(k + 1):
        t = i / k
        rr = (r + overhang) * (1 - t) ** (1 + curve_in)
        pts.append((rr, y + h * t))
    pts = [(r * 0.85, y - 0.02)] + pts
    return lathe(pts, sides, cap_bottom=True, cap_top=False, a0=math.pi / sides)


def spikes_ring(r, y, n=8, h=0.14, rad=0.018, tilt=0.5, phase=0.0):
    out = []
    for i in range(n):
        a = phase + TAU * i / n
        s = spike(rad, h, 6).rz(-tilt).ry(-a).t(math.cos(a) * r, y, math.sin(a) * r)
        out.append(s)
    return merge(out)


def chain(p0, p1, links=8, sag=0.08, size=0.018):
    path = np.array(p0, float), np.array(p1, float)
    out = []
    for i in range(links):
        t = (i + 0.5) / links
        p = path[0] * (1 - t) + path[1] * t
        p[1] -= math.sin(t * math.pi) * sag
        lk = torus(size, size * 0.28, 10, 5).s(1, 1, 1.6)
        if i % 2:
            lk.rz(math.pi / 2)
        d = path[1] - path[0]
        lk.ry(math.atan2(d[0], d[2]))
        out.append(lk.t(*p))
    return merge(out)


def banner(w=0.16, h=0.32, wave=0.02, pole=True):
    """Hängendes Banner (leicht gewellt) mit Querstange; Ursprung oben mittig."""
    k = 8
    pts = []
    for j in range(k + 1):
        pass
    # als dünne, unterteilte Platte
    nu, nv = 6, 10
    P, N, UV, I = [], [], [], []
    for j in range(nv + 1):
        for i in range(nu + 1):
            u, v = i / nu, j / nv
            x = (u - 0.5) * w
            y = -v * h
            tipcut = 1.0
            if v > 0.82:  # Schwalbenschwanz
                tipcut = 1 - abs(u - 0.5) * 2
                y = -h * (0.82 + (v - 0.82) * (0.3 + 0.7 * abs(u - 0.5) * 2))
            z = math.sin(u * 3.2 + v * 4) * wave * v
            P.append((x, y, z)); N.append((0, 0, 1)); UV.append((x, y))
    for j in range(nv):
        for i in range(nu):
            a = j * (nu + 1) + i
            I += [(a, a + nu + 1, a + 1), (a + 1, a + nu + 1, a + nu + 2)]
    front = Mesh(P, N, UV, I)
    back = front.copy(); back.I = back.I[:, ::-1].copy(); back.N = -back.N
    front.P[:, 2] += 0.003; back.P[:, 2] -= 0.003
    cloth = weld_normals(merge([front, back]))
    pole_m = cylinder(0.008, w * 1.25, 6).rz(math.pi / 2).t(w * 0.62, 0.0, 0.0) if pole else None
    return cloth, pole_m


def brazier(r=0.12, y=0.0):
    bowl = lathe([(0.02, 0), (r * 0.5, 0.01), (r, 0.06), (r * 1.08, 0.1), (r * 0.95, 0.1), (r * 0.85, 0.07), (0.0, 0.05)], 12, cap_bottom=False, cap_top=False).t(0, y, 0)
    coals = displace(sphere(r * 0.82, r * 0.35, r * 0.82, 12).t(0, y + 0.08, 0), 0.012, 30)
    return bowl, coals


def crystal_cluster(n=5, r=0.06, h=0.35, spread=0.07, seed=1, tilt=0.35):
    rng = np.random.default_rng(seed)
    out = [crystal(r, h, 6, 0.3).t(0, 0, 0)]
    for i in range(n - 1):
        a = TAU * i / (n - 1) + rng.random()
        s = 0.45 + rng.random() * 0.4
        c = crystal(r * s, h * s, 6, 0.3).rz(tilt * (0.7 + rng.random() * 0.6)).ry(-a).t(math.cos(a) * spread, 0, math.sin(a) * spread)
        out.append(c)
    return merge(out)


def gem_socket(r=0.05):
    return lathe([(0, 0), (r * 1.4, 0), (r * 1.5, r * 0.4), (r * 1.1, r * 0.6), (0, r * 0.5)], 8, cap_bottom=False, cap_top=False)


def rune_stone(w=0.12, h=0.3, d=0.06, seed=3):
    return displace(rbox(w, h, d, 0.02, 2).t(0, h / 2, 0), 0.006, 14, seed)


def boulders(n=6, r=0.45, size=0.08, seed=5):
    rng = np.random.default_rng(seed)
    out = []
    for i in range(n):
        a = TAU * i / n + rng.random() * 0.5
        s = size * (0.6 + rng.random() * 0.8)
        b = displace(icosphere(s, 2).s(1, 0.7, 1), s * 0.25, 6 / s * 0.1, seed + i)
        out.append(b.t(math.cos(a) * r, s * 0.3, math.sin(a) * r))
    return merge(out)


def gear(r=0.1, t=0.03, teeth=10):
    pts = []
    for i in range(teeth * 4):
        a = TAU * i / (teeth * 4)
        rr = r if (i % 4) in (0, 1) else r * 0.8
        pts.append((math.cos(a) * rr, math.sin(a) * rr))
    g = extrude(pts, t)
    hub = cylinder(r * 0.3, t * 1.6, 10).rx(math.pi / 2).t(0, 0, -t * 0.8)
    return merge([g, hub])


def planks_platform(r=0.42, y=0.0, t=0.04, n=7):
    out = []
    w = 2 * r / n
    for i in range(n):
        x = -r + w * (i + 0.5)
        L = 2 * math.sqrt(max(r * r - x * x, 0.01))
        out.append(rbox(w * 0.92, t, L, 0.008).t(x, y, 0))
    return merge(out)


def beam(p0, p1, w=0.04):
    p0, p1 = np.asarray(p0, float), np.asarray(p1, float)
    d = p1 - p0
    L = float(np.linalg.norm(d))
    b = rbox(w, L, w, 0.008).t(0, L / 2, 0)
    # Y-Achse auf d drehen
    y = np.array([0, 1.0, 0])
    dn = d / L
    ax = np.cross(y, dn)
    s = np.linalg.norm(ax)
    if s > 1e-6:
        ax /= s
        ang = math.atan2(s, float(y @ dn))
        K = np.array([[0, -ax[2], ax[1]], [ax[2], 0, -ax[0]], [-ax[1], ax[0], 0]])
        R = np.eye(3) + math.sin(ang) * K + (1 - math.cos(ang)) * K @ K
        MM = np.eye(4); MM[:3, :3] = R
        b.apply(MM)
    elif dn[1] < 0:
        b.rx(math.pi)
    return b.t(*p0)


# ------------------------------------------------------------------ Bildhauer-Kleinteile (zwischengespeichert)

@lru_cache(maxsize=None)
def _skull_cache(scale_key):
    s = scale_key / 1000.0
    sc = Sculpt(detail=0.0015 * s / 0.1, dfreq=40)
    sc.ellipsoid((0, 0.06 * s / 0.1, 0), (0.05 * s / 0.1, 0.055 * s / 0.1, 0.06 * s / 0.1), 'bone', k=0.01)
    sc.box((0, 0.018 * s / 0.1, 0.03 * s / 0.1), (0.03 * s / 0.1, 0.02 * s / 0.1, 0.03 * s / 0.1), 'bone', k=0.012, r=0.008 * s / 0.1)
    for sg in (1, -1):
        sc.ellipsoid((sg * 0.021 * s / 0.1, 0.055 * s / 0.1, 0.055 * s / 0.1), 0.015 * s / 0.1, 'bone', sub=True, k=0.004)
    sc.ellipsoid((0, 0.035 * s / 0.1, 0.065 * s / 0.1), (0.007 * s / 0.1, 0.01 * s / 0.1, 0.01 * s / 0.1), 'bone', sub=True, k=0.003)
    parts = sc.build(voxel=max(0.005, s * 0.075), max_dim=40)
    return parts[0][1]


def skull(s=0.1):
    """Kleiner Schädel (Höhe ≈ 1,2·s), schaut nach +Z."""
    m = _skull_cache(int(round(s * 1000))).copy()
    return m


@lru_cache(maxsize=None)
def _horn_cache(L_key, r_key, curl_key):
    L, r, curl = L_key / 1000, r_key / 1000, curl_key / 100
    pts = curve([(0, 0, 0), (0, L * 0.35, -L * 0.15 * curl), (0, L * 0.65, -L * 0.1 * curl - L * 0.1), (0, L, L * 0.15 * curl)], 14)
    return tube(pts, [r, r * 0.7, r * 0.35, 0.0015], 8, cap=True)


def horn(L=0.2, r=0.03, curl=1.0):
    return _horn_cache(int(L * 1000), int(r * 1000), int(curl * 100)).copy()


def blade(L=0.6, w=0.05, t=0.012, guard=0.14, grip=0.14, curved=0.0):
    """Schwert: Klinge (+Y), Parierstange, Griff, Knauf. Ursprung = Griffmitte. → (Stahl, Leder/Griff, Gold)"""
    k = 10
    left, right = [], []
    for i in range(k + 1):
        t_ = i / k
        y = t_ * L
        ww = w * (1 - t_ ** 3 * 0.9) if t_ < 0.95 else w * 0.1 * (1 - t_) / 0.05
        off = curved * (t_ ** 2) * L * 0.2
        left.append((-ww / 2 + off, y))
        right.append((ww / 2 + off, y))
    poly = right + [(curved * L * 0.2, L * 1.04)] + left[::-1]
    bl = extrude(poly, t, bevel=0.55).t(0, grip * 0.5 + 0.02, 0)
    gd = rbox(guard, 0.028, 0.035, 0.01).t(0, grip * 0.5 + 0.01, 0)
    gp = cylinder(0.014, grip, 8).t(0, -grip * 0.5, 0)
    pm = sphere(0.022, seg=10).t(0, -grip * 0.5 - 0.015, 0)
    return bl, gp, merge([gd, pm])


def axe(L=0.7, head=0.18):
    haft = cylinder(0.016, L, 8).t(0, -L * 0.3, 0)
    pts = [(0, 0), (head * 0.9, head * 0.55), (head, 0.02), (head * 0.95, -head * 0.5), (0, -head * 0.12)]
    hd = extrude(pts, 0.018, bevel=0.5).t(0.01, L * 0.62, 0)
    back = spike(0.02, 0.09, 6).rz(math.pi / 2).t(0, L * 0.62, 0)
    return haft, merge([hd, back])


def staff(L=1.0, r=0.016, top='crystal'):
    sh = tube(curve([(0, -L * 0.35, 0), (0.01, 0, 0), (-0.01, L * 0.4, 0.005), (0, L * 0.65, 0)], 12), [r * 1.1, r, r * 0.9, r * 1.1], 8)
    claw = merge([spike(0.008, 0.12, 5).rz(0.5).ry(-a).t(math.cos(a) * 0.03, L * 0.65, math.sin(a) * 0.03) for a in (0, 2.1, 4.2)])
    gem = crystal(0.03, 0.12, 6).t(0, L * 0.66, 0)
    return sh, claw, gem


def bow(L=0.8):
    pts = curve([(0, -L / 2, 0), (0, -L * 0.25, 0.08), (0, 0, 0.05), (0, L * 0.25, 0.08), (0, L / 2, 0)], 20)
    limbs = tube(pts, [0.008, 0.016, 0.02, 0.016, 0.008], 6)
    string = tube(np.array([[0, -L / 2, 0], [0, L / 2, 0]]), 0.0025, 4, cap=False)
    return limbs, string


def shield(r=0.2, kind='round'):
    if kind == 'round':
        face = lathe([(0, 0.03), (r * 0.6, 0.025), (r, 0.0), (r, -0.012), (0, -0.01)], 20, cap_bottom=False, cap_top=False).rx(math.pi / 2)
        boss = sphere(r * 0.22, r * 0.22, r * 0.12, 12).t(0, 0, 0.03)
        return face, boss
    pts = [(-r * 0.8, r), (r * 0.8, r), (r * 0.85, 0), (0, -r * 1.3), (-r * 0.85, 0)]
    face = extrude(pts, 0.03, bevel=0.15)
    boss = skull(r * 0.6).t(0, -r * 0.2, 0.01)
    return face, boss


def pauldron(r=0.08, spikes=0):
    sh = sphere(r, r * 0.8, r, 12, 6)
    sh.P[:, 1] = np.maximum(sh.P[:, 1], -r * 0.1)
    out = [weld_normals(sh)]
    for i in range(spikes):
        out.append(spike(r * 0.18, r * 0.9, 6).rz(-0.4 + i * 0.4).t(0, r * 0.6, 0))
    return merge(out)
