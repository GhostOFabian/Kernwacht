"""Geometrie-Grundbausteine (numpy). Alle Maße in Metern, Y nach oben, Vorderseite +Z.

Ein Mesh hält Positionen P, Normalen N, UVs (in Weltmetern, die Textur wiederholt sich je Meter)
und Dreiecksindizes I. Teile werden transformiert und zu Modellen zusammengesetzt.
"""
from __future__ import annotations

import math
import numpy as np

TAU = math.tau


class Mesh:
    def __init__(self, P, N, UV, I):
        self.P = np.asarray(P, dtype=np.float64).reshape(-1, 3)
        self.N = np.asarray(N, dtype=np.float64).reshape(-1, 3)
        self.UV = np.asarray(UV, dtype=np.float64).reshape(-1, 2)
        self.I = np.asarray(I, dtype=np.int64).reshape(-1, 3)
        self.C = None  # optionale Vertexfarbe (Okklusion), n×3

    def copy(self) -> "Mesh":
        m = Mesh(self.P.copy(), self.N.copy(), self.UV.copy(), self.I.copy())
        m.C = None if self.C is None else self.C.copy()
        return m

    # --- Transformationen (verändern das Mesh, geben es zurück)
    def apply(self, M: np.ndarray) -> "Mesh":
        """4×4-Matrix anwenden."""
        R = M[:3, :3]
        self.P = self.P @ R.T + M[:3, 3]
        Ninv = np.linalg.inv(R).T
        n = self.N @ Ninv.T
        self.N = n / np.maximum(np.linalg.norm(n, axis=1, keepdims=True), 1e-12)
        if np.linalg.det(R) < 0:
            self.I = self.I[:, ::-1].copy()
        return self

    def t(self, x=0.0, y=0.0, z=0.0) -> "Mesh":
        self.P = self.P + np.array([x, y, z])
        return self

    def s(self, x=1.0, y=None, z=None) -> "Mesh":
        y = x if y is None else y
        z = x if z is None else z
        return self.apply(np.diag([x, y, z, 1.0]))

    def rx(self, a) -> "Mesh":
        return self.apply(rot_x(a))

    def ry(self, a) -> "Mesh":
        return self.apply(rot_y(a))

    def rz(self, a) -> "Mesh":
        return self.apply(rot_z(a))

    def bounds(self):
        return self.P.min(axis=0), self.P.max(axis=0)

    @property
    def tris(self) -> int:
        return len(self.I)


def rot_x(a):
    c, s = math.cos(a), math.sin(a)
    return np.array([[1, 0, 0, 0], [0, c, -s, 0], [0, s, c, 0], [0, 0, 0, 1.0]])


def rot_y(a):
    c, s = math.cos(a), math.sin(a)
    return np.array([[c, 0, s, 0], [0, 1, 0, 0], [-s, 0, c, 0], [0, 0, 0, 1.0]])


def rot_z(a):
    c, s = math.cos(a), math.sin(a)
    return np.array([[c, -s, 0, 0], [s, c, 0, 0], [0, 0, 1, 0], [0, 0, 0, 1.0]])


def merge(meshes) -> Mesh:
    meshes = [m for m in meshes if m is not None and len(m.P)]
    if not meshes:
        return Mesh(np.zeros((0, 3)), np.zeros((0, 3)), np.zeros((0, 2)), np.zeros((0, 3)))
    P, N, UV, I, C = [], [], [], [], []
    off = 0
    anyc = any(m.C is not None for m in meshes)
    for m in meshes:
        P.append(m.P); N.append(m.N); UV.append(m.UV); I.append(m.I + off)
        if anyc:
            C.append(m.C if m.C is not None else np.ones((len(m.P), 3)))
        off += len(m.P)
    out = Mesh(np.vstack(P), np.vstack(N), np.vstack(UV), np.vstack(I))
    if anyc:
        out.C = np.vstack(C)
    return out


def _norm(v):
    return v / np.maximum(np.linalg.norm(v, axis=-1, keepdims=True), 1e-12)


# ------------------------------------------------------------------ Primitive

def _grid_indices(nu, nv, wrap_u=False):
    """Indizes für ein Gitter aus nu×nv Punkten (u läuft schnell)."""
    I = []
    cols = nu if wrap_u else nu - 1
    for j in range(nv - 1):
        for i in range(cols):
            i1 = (i + 1) % nu
            a = j * nu + i
            b = j * nu + i1
            c = (j + 1) * nu + i
            d = (j + 1) * nu + i1
            I.append((a, c, b))
            I.append((b, c, d))
    return np.array(I, dtype=np.int64).reshape(-1, 3)


def rbox(sx, sy, sz, r=0.03, seg=1) -> Mesh:
    """Quader (Kantenlängen sx, sy, sz) mit abgerundeten Kanten, Mittelpunkt im Ursprung."""
    h = np.array([sx, sy, sz]) / 2.0
    r = float(min(r, *(h * 0.98)))

    def coords(hh):
        if r <= 1e-6:
            return np.array([-hh, hh])
        ang = np.linspace(0, math.pi / 2, seg + 1)
        vals = np.concatenate([-hh + r - r * np.sin(ang), hh - r + r * np.sin(ang)])
        return np.unique(np.round(vals, 9))

    cs = [coords(h[0]), coords(h[1]), coords(h[2])]
    P, N, UV, I = [], [], [], []
    off = 0
    inner = h - r
    # Flächen: (Achse, Vorzeichen, Tangentialachsen)
    for axis in range(3):
        for sign in (-1, 1):
            ua, va = [(1, 2), (2, 0), (0, 1)][axis]
            us, vs = cs[ua], cs[va]
            U, V = np.meshgrid(us, vs)
            pts = np.zeros((U.size, 3))
            pts[:, axis] = sign * h[axis]
            pts[:, ua] = U.ravel()
            pts[:, va] = V.ravel()
            fn = np.zeros(3); fn[axis] = sign
            if r > 1e-6:
                cl = np.clip(pts, -inner, inner)
                d = pts - cl
                L = np.linalg.norm(d, axis=1, keepdims=True)
                n = np.where(L > 1e-12, d / np.maximum(L, 1e-12), fn)
                p = cl + n * r
            else:
                n = np.tile(fn, (len(pts), 1))
                p = pts
            uv = np.stack([pts[:, ua], pts[:, va]], axis=1)
            nu, nv = len(us), len(vs)
            idx = _grid_indices(nu, nv)
            # Wicklung nach außen
            if sign * (1 if axis != 1 else 1) < 0:
                idx = idx[:, ::-1]
            P.append(p); N.append(n); UV.append(uv); I.append(idx + off)
            off += len(p)
    m = Mesh(np.vstack(P), np.vstack(N), np.vstack(UV), np.vstack(I))
    fix_winding(m)
    return m


def fix_winding(m: Mesh) -> Mesh:
    """Dreiecke so drehen, dass ihre Flächennormale zur Vertexnormale passt."""
    a, b, c = m.P[m.I[:, 0]], m.P[m.I[:, 1]], m.P[m.I[:, 2]]
    fn = np.cross(b - a, c - a)
    vn = m.N[m.I[:, 0]] + m.N[m.I[:, 1]] + m.N[m.I[:, 2]]
    flip = (fn * vn).sum(axis=1) < 0
    m.I[flip] = m.I[flip][:, ::-1]
    return m


def box(sx, sy, sz) -> Mesh:
    return rbox(sx, sy, sz, r=0.0)


def lathe(profile, seg=24, cap_bottom=True, cap_top=True, sharp=(), a0=0.0, a1=None, smooth_all=False) -> Mesh:
    """Rotationskörper um die Y-Achse. profile: Liste (Radius, Höhe) von unten nach oben.
    sharp: Indizes von Profilpunkten mit harter Kante. Ohne smooth_all sind alle Knicke > 35° hart."""
    if a1 is None:
        a1 = a0 + TAU  # voller Umlauf ab a0 (vorher fehlte bei a0 > 0 ein Keil)
    pr = np.array(profile, dtype=np.float64)
    n = len(pr)
    seg_n = []
    for i in range(n - 1):
        dr, dy = pr[i + 1] - pr[i]
        L = math.hypot(dr, dy) or 1.0
        seg_n.append((dy / L, -dr / L))
    seg_n = np.array(seg_n)
    sharp = set(sharp)
    if not smooth_all:
        for i in range(1, n - 1):
            if float(np.dot(seg_n[i - 1], seg_n[i])) < math.cos(math.radians(35)):
                sharp.add(i)
    # in glatte Läufe aufteilen
    runs, cur = [], [0]
    for i in range(1, n):
        cur.append(i)
        if i in sharp and i != n - 1:
            runs.append(cur)
            cur = [i]
    runs.append(cur)
    nu = seg + 1  # Nahtspalte doppelt, damit die UVs nicht über die Naht springen
    ang = a0 + (a1 - a0) * np.arange(nu) / seg
    ca, sa = np.cos(ang), np.sin(ang)
    parts = []
    for run in runs:
        pts, nrm, vv = [], [], []
        acc = 0.0
        for k, i in enumerate(run):
            r, y = pr[i]
            # Normale am Punkt innerhalb des Laufs
            ns = []
            if k > 0:
                ns.append(seg_n[i - 1])
            if k < len(run) - 1:
                ns.append(seg_n[i])
            nn = np.mean(ns, axis=0)
            nn = nn / (np.linalg.norm(nn) or 1)
            if k > 0:
                acc += float(np.linalg.norm(pr[i] - pr[run[k - 1]]))
            for j in range(nu):
                pts.append((r * ca[j], y, r * sa[j]))
                nrm.append((nn[0] * ca[j], nn[1], nn[0] * sa[j]))
                vv.append((ang[j] * max(pr[:, 0].max(), 0.05), acc))
        P = np.array(pts); N = np.array(nrm); UV = np.array(vv)
        I = _grid_indices(nu, len(run))
        parts.append(fix_winding(Mesh(P, N, UV, I)))
    if cap_bottom and pr[0, 0] > 1e-6:
        parts.append(disc(pr[0, 0], seg, pr[0, 1], down=True, a0=a0, a1=a1))
    if cap_top and pr[-1, 0] > 1e-6:
        parts.append(disc(pr[-1, 0], seg, pr[-1, 1], down=False, a0=a0, a1=a1))
    return merge(parts)


def disc(r, seg=24, y=0.0, down=False, a0=0.0, a1=TAU) -> Mesh:
    full = abs((a1 - a0) - TAU) < 1e-9
    nu = seg if full else seg + 1
    ang = a0 + (a1 - a0) * np.arange(nu) / seg
    P = [(0, y, 0)] + [(r * math.cos(a), y, r * math.sin(a)) for a in ang]
    ny = -1.0 if down else 1.0
    N = [(0, ny, 0)] * len(P)
    UV = [(p[0], p[2]) for p in P]
    I = []
    cnt = nu if full else nu - 1
    for i in range(cnt):
        a, b = 1 + i, 1 + (i + 1) % nu
        I.append((0, a, b))
    return fix_winding(Mesh(P, N, UV, I))


def cylinder(r=0.5, h=1.0, seg=16, r2=None, bevel=0.0) -> Mesh:
    r2 = r if r2 is None else r2
    if bevel > 0:
        b = bevel
        prof = [(0.0, 0.0), (r - b, 0.0), (r, b), (r2, h - b), (r2 - b, h), (0.0, h)]
        return lathe(prof, seg, cap_bottom=False, cap_top=False)
    return lathe([(r, 0.0), (r2, h)], seg)


def cone(r=0.5, h=1.0, seg=16) -> Mesh:
    return lathe([(r, 0.0), (0.0, h)], seg, cap_top=False)


def sphere(rx=0.5, ry=None, rz=None, seg=16, rings=None) -> Mesh:
    ry = rx if ry is None else ry
    rz = rx if rz is None else rz
    rings = rings or max(6, seg // 2)
    P, N, UV = [], [], []
    for j in range(rings + 1):
        v = j / rings
        th = math.pi * v
        for i in range(seg + 1):
            u = i / seg
            ph = TAU * u
            x, y, z = math.sin(th) * math.cos(ph), -math.cos(th), math.sin(th) * math.sin(ph)
            P.append((x * rx, y * ry, z * rz))
            N.append((x / rx, y / ry, z / rz))
            UV.append((u * TAU * max(rx, rz), v * math.pi * ry))
    I = _grid_indices(seg + 1, rings + 1)
    m = Mesh(P, _norm(np.array(N)), UV, I)
    return fix_winding(m)


def capsule(r=0.1, h=0.4, seg=12) -> Mesh:
    """Kapsel entlang Y von 0 bis h (inklusive Kappen)."""
    prof = []
    k = 5
    for i in range(k + 1):
        a = -math.pi / 2 + (math.pi / 2) * i / k
        prof.append((r * math.cos(a), r + r * math.sin(a)))
    for i in range(k + 1):
        a = (math.pi / 2) * i / k
        prof.append((r * math.cos(a), h - r + r * math.sin(a)))
    prof[0] = (0.0, prof[0][1]); prof[-1] = (0.0, prof[-1][1])
    return lathe(prof, seg, cap_bottom=False, cap_top=False, smooth_all=True)


def torus(R=0.5, r=0.1, seg=24, seg2=8) -> Mesh:
    P, N, UV = [], [], []
    for j in range(seg2 + 1):
        b = TAU * j / seg2
        for i in range(seg + 1):
            a = TAU * i / seg
            cx, cz = math.cos(a), math.sin(a)
            nx, ny, nz = math.cos(b) * cx, math.sin(b), math.cos(b) * cz
            P.append(((R + r * math.cos(b)) * cx, r * math.sin(b), (R + r * math.cos(b)) * cz))
            N.append((nx, ny, nz))
            UV.append((a * R, b * r))
    I = _grid_indices(seg + 1, seg2 + 1)
    return fix_winding(Mesh(P, N, UV, I))


def tube(path, radius, sides=8, cap=True, twist=0.0) -> Mesh:
    """Röhre entlang eines Pfads (k×3). radius: Zahl oder Liste je Punkt (z. B. spitz zulaufend)."""
    path = np.asarray(path, dtype=np.float64)
    k = len(path)
    rad = np.full(k, radius, dtype=np.float64) if np.isscalar(radius) else np.asarray(radius, dtype=np.float64)
    if len(rad) != k:
        rad = np.interp(np.linspace(0, 1, k), np.linspace(0, 1, len(rad)), rad)
    T = np.gradient(path, axis=0)
    T = _norm(T)
    # Parallel Transport
    up = np.array([0, 1, 0.0]) if abs(T[0][1]) < 0.9 else np.array([1, 0, 0.0])
    Nf = _norm(np.cross(T[0], up))
    frames = []
    for i in range(k):
        if i > 0:
            Nf = Nf - T[i] * np.dot(Nf, T[i])
            Nf = Nf / (np.linalg.norm(Nf) or 1)
        B = np.cross(T[i], Nf)
        frames.append((Nf.copy(), B))
    P, N, UV = [], [], []
    acc = 0.0
    for i in range(k):
        if i > 0:
            acc += float(np.linalg.norm(path[i] - path[i - 1]))
        n0, b0 = frames[i]
        for j in range(sides + 1):
            a = TAU * j / sides + twist * i / max(1, k - 1)
            d = n0 * math.cos(a) + b0 * math.sin(a)
            P.append(path[i] + d * rad[i])
            N.append(d)
            UV.append((j / sides * TAU * max(rad.max(), 0.02), acc))
    I = _grid_indices(sides + 1, k)
    parts = [fix_winding(Mesh(P, N, UV, I))]
    if cap:
        for i, sgn in ((0, -1), (k - 1, 1)):
            if rad[i] > 1e-5:
                n0, b0 = frames[i]
                c = path[i]
                pts = [c] + [c + (n0 * math.cos(TAU * j / sides) + b0 * math.sin(TAU * j / sides)) * rad[i] for j in range(sides)]
                nn = [T[i] * sgn] * len(pts)
                uv = [(0, 0)] + [(math.cos(TAU * j / sides) * rad[i], math.sin(TAU * j / sides) * rad[i]) for j in range(sides)]
                ii = [(0, 1 + j, 1 + (j + 1) % sides) for j in range(sides)]
                parts.append(fix_winding(Mesh(pts, nn, uv, ii)))
    return merge(parts)


def _ear_clip(poly):
    """Ohrenschneiden für einfache Polygone (gegen den Uhrzeigersinn)."""
    pts = [tuple(p) for p in poly]
    idx = list(range(len(pts)))
    area = sum(pts[i][0] * pts[(i + 1) % len(pts)][1] - pts[(i + 1) % len(pts)][0] * pts[i][1] for i in range(len(pts)))
    if area < 0:
        idx.reverse()
    tris = []

    def inside(p, a, b, c):
        def s(p1, p2, p3):
            return (p1[0] - p3[0]) * (p2[1] - p3[1]) - (p2[0] - p3[0]) * (p1[1] - p3[1])
        d1, d2, d3 = s(p, a, b), s(p, b, c), s(p, c, a)
        neg = d1 < 0 or d2 < 0 or d3 < 0
        pos = d1 > 0 or d2 > 0 or d3 > 0
        return not (neg and pos)

    guard = 0
    while len(idx) > 3 and guard < 10000:
        guard += 1
        n = len(idx)
        for k in range(n):
            i0, i1, i2 = idx[(k - 1) % n], idx[k], idx[(k + 1) % n]
            a, b, c = pts[i0], pts[i1], pts[i2]
            cross = (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])
            if cross <= 1e-12:
                continue
            if any(inside(pts[j], a, b, c) for j in idx if j not in (i0, i1, i2)):
                continue
            tris.append((i0, i1, i2))
            idx.pop(k)
            break
        else:
            break
    if len(idx) == 3:
        tris.append(tuple(idx))
    return tris


def extrude(poly, depth=0.05, bevel=0.0) -> Mesh:
    """Polygon (k×2, XY-Ebene) entlang Z extrudieren, mittig um z=0.
    bevel>0 verjüngt die Deckflächen (Klingen-/Plattenkante)."""
    poly = np.asarray(poly, dtype=np.float64)
    k = len(poly)
    tris = _ear_clip(poly)
    hz = depth / 2
    parts = []
    c = poly.mean(axis=0)
    for sgn in (1, -1):
        pp = poly if bevel <= 0 else c + (poly - c) * (1 - bevel)
        P = np.column_stack([pp, np.full(k, sgn * hz)])
        N = np.tile([0, 0, sgn], (k, 1))
        UV = pp.copy()
        parts.append(fix_winding(Mesh(P, N, UV, np.array(tris))))
    # Seiten
    P, N, UV, I = [], [], [], []
    acc = 0.0
    inner = poly if bevel <= 0 else c + (poly - c) * (1 - bevel)
    for i in range(k):
        a, b = poly[i], poly[(i + 1) % k]
        ia, ib = inner[i], inner[(i + 1) % k]
        e = b - a
        L = float(np.linalg.norm(e)) or 1.0
        n2 = np.array([e[1], -e[0]]) / L
        if np.dot(n2, (a + b) / 2 - c) < 0:
            n2 = -n2
        base = len(P)
        if bevel <= 0:
            quad = [(a, -hz), (b, -hz), (b, hz), (a, hz)]
            for (q, z) in quad:
                P.append((q[0], q[1], z)); N.append((n2[0], n2[1], 0))
            UV += [(acc, -hz), (acc + L, -hz), (acc + L, hz), (acc, hz)]
            I += [(base, base + 1, base + 2), (base, base + 2, base + 3)]
        else:
            # Grat in der Mitte, angeschrägt zu den Deckflächen
            for (q, z, nz) in ((ia, -hz, -0.6), (ib, -hz, -0.6), (b, 0.0, -0.6), (a, 0.0, -0.6),
                               (a, 0.0, 0.6), (b, 0.0, 0.6), (ib, hz, 0.6), (ia, hz, 0.6)):
                nn = np.array([n2[0], n2[1], nz]); nn /= np.linalg.norm(nn)
                P.append((q[0], q[1], z)); N.append(tuple(nn))
            UV += [(acc, -hz), (acc + L, -hz), (acc + L, 0), (acc, 0), (acc, 0), (acc + L, 0), (acc + L, hz), (acc, hz)]
            I += [(base, base + 1, base + 2), (base, base + 2, base + 3), (base + 4, base + 5, base + 6), (base + 4, base + 6, base + 7)]
        acc += L
    parts.append(fix_winding(Mesh(P, N, UV, I)))
    return merge(parts)


def icosphere(r=0.5, sub=2) -> Mesh:
    t = (1 + 5 ** 0.5) / 2
    V = [(-1, t, 0), (1, t, 0), (-1, -t, 0), (1, -t, 0), (0, -1, t), (0, 1, t), (0, -1, -t), (0, 1, -t),
         (t, 0, -1), (t, 0, 1), (-t, 0, -1), (-t, 0, 1)]
    F = [(0, 11, 5), (0, 5, 1), (0, 1, 7), (0, 7, 10), (0, 10, 11), (1, 5, 9), (5, 11, 4), (11, 10, 2), (10, 7, 6), (7, 1, 8),
         (3, 9, 4), (3, 4, 2), (3, 2, 6), (3, 6, 8), (3, 8, 9), (4, 9, 5), (2, 4, 11), (6, 2, 10), (8, 6, 7), (9, 8, 1)]
    V = [np.array(v, dtype=float) / np.linalg.norm(v) for v in V]
    for _ in range(sub):
        cache = {}

        def mid(a, b):
            key = (min(a, b), max(a, b))
            if key not in cache:
                m = (V[a] + V[b]) / 2
                V.append(m / np.linalg.norm(m))
                cache[key] = len(V) - 1
            return cache[key]
        nf = []
        for a, b, c in F:
            ab, bc, ca = mid(a, b), mid(b, c), mid(c, a)
            nf += [(a, ab, ca), (b, bc, ab), (c, ca, bc), (ab, bc, ca)]
        F = nf
    P = np.array(V) * r
    N = np.array(V)
    UV = np.column_stack([np.arctan2(N[:, 2], N[:, 0]) * r, np.arcsin(np.clip(N[:, 1], -1, 1)) * r])
    return fix_winding(Mesh(P, N, UV, F))


def crystal(r=0.15, h=0.6, sides=6, tip=0.35, base_tip=0.15) -> Mesh:
    """Kristallsäule mit Spitze oben und kurzer Spitze unten, facettiert."""
    prof = [(0.0, -h * base_tip), (r, 0.0), (r, h * (1 - tip)), (0.0, h)]
    return lathe(prof, sides, cap_bottom=False, cap_top=False, sharp=(1, 2))


def spike(r=0.05, h=0.3, seg=6) -> Mesh:
    return lathe([(r, 0.0), (r * 0.6, h * 0.5), (0.0, h)], seg, cap_top=False, smooth_all=True)


# ------------------------------------------------------------------ Verformen

def _hash3(ix, iy, iz, seed):
    h = (ix * 73856093) ^ (iy * 19349663) ^ (iz * 83492791) ^ (seed * 2654435761)
    h = (h ^ (h >> 13)) * 1274126177
    h = h ^ (h >> 16)
    return (h & 0xFFFFFF) / float(0xFFFFFF)


def vnoise3(p: np.ndarray, seed=0) -> np.ndarray:
    p = np.asarray(p, dtype=np.float64)
    i = np.floor(p).astype(np.int64)
    f = p - i
    u = f * f * (3 - 2 * f)
    out = np.zeros(len(p))
    for dx in (0, 1):
        for dy in (0, 1):
            for dz in (0, 1):
                h = _hash3(i[:, 0] + dx, i[:, 1] + dy, i[:, 2] + dz, seed)
                w = (u[:, 0] if dx else 1 - u[:, 0]) * (u[:, 1] if dy else 1 - u[:, 1]) * (u[:, 2] if dz else 1 - u[:, 2])
                out += h * w
    return out


def fbm3(p, seed=0, oct=4):
    a, f, s, n = 0.5, 1.0, 0.0, 0.0
    for o in range(oct):
        s += a * vnoise3(p * f, seed + o * 17)
        n += a
        a *= 0.5
        f *= 2.03
    return s / n


def weld_normals(m: Mesh, tol=1e-5) -> Mesh:
    """Glatte Normalen über gleiche Positionen hinweg neu berechnen."""
    key = np.round(m.P / tol).astype(np.int64)
    _, inv = np.unique(key, axis=0, return_inverse=True)
    inv = inv.ravel()
    a, b, c = m.P[m.I[:, 0]], m.P[m.I[:, 1]], m.P[m.I[:, 2]]
    fn = np.cross(b - a, c - a)
    acc = np.zeros((inv.max() + 1, 3))
    for k in range(3):
        np.add.at(acc, inv[m.I[:, k]], fn)
    m.N = _norm(acc[inv])
    return m


def displace(m: Mesh, amp=0.02, freq=6.0, seed=1, oct=4, ridged=False) -> Mesh:
    """Organische Unebenheit entlang der Normalen (Fels, Fleisch, Rinde)."""
    n = fbm3(m.P * freq, seed, oct)
    if ridged:
        n = 1 - np.abs(n * 2 - 1)
    m.P = m.P + m.N * ((n - 0.5) * 2 * amp)[:, None]
    return weld_normals(m)


def bend(m: Mesh, k=0.5, axis_len=1.0) -> Mesh:
    """Biegt ein Teil entlang Y nach +Z (für Hörner, Krallen, Schwänze)."""
    y = m.P[:, 1] / max(axis_len, 1e-6)
    m.P[:, 2] += k * y * y * axis_len
    return weld_normals(m) if False else m


def taper(m: Mesh, top=0.5, y0=None, y1=None) -> Mesh:
    lo, hi = m.bounds()
    y0 = lo[1] if y0 is None else y0
    y1 = hi[1] if y1 is None else y1
    t = np.clip((m.P[:, 1] - y0) / max(y1 - y0, 1e-6), 0, 1)
    s = 1 + (top - 1) * t
    m.P[:, 0] *= s
    m.P[:, 2] *= s
    return m


def ring_array(make, count, radius, y=0.0, phase=0.0, face_out=True):
    """count Kopien eines Teils im Kreis (make() liefert ein frisches Mesh)."""
    out = []
    for i in range(count):
        a = phase + TAU * i / count
        m = make()
        if face_out:
            m.ry(-a + math.pi / 2)
        m.t(math.cos(a) * radius, y, math.sin(a) * radius)
        out.append(m)
    return merge(out)


def path_arc(p0, p1, lift=0.2, n=10):
    p0, p1 = np.asarray(p0, float), np.asarray(p1, float)
    t = np.linspace(0, 1, n)[:, None]
    p = p0 * (1 - t) + p1 * t
    p[:, 1] += np.sin(t[:, 0] * math.pi) * lift
    return p


def curve(points, n=16):
    """Catmull-Rom durch Stützpunkte."""
    pts = np.asarray(points, float)
    out = []
    P = np.vstack([pts[0], pts, pts[-1]])
    for i in range(1, len(P) - 2):
        p0, p1, p2, p3 = P[i - 1], P[i], P[i + 1], P[i + 2]
        for t in np.linspace(0, 1, max(2, n // (len(pts) - 1)), endpoint=False):
            t2, t3 = t * t, t * t * t
            out.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3))
    out.append(pts[-1])
    return np.array(out)


def facet(m: Mesh) -> Mesh:
    """Flache Schattierung: jede Fläche bekommt eigene Ecken mit Flächennormale (kantige Steine, Kristalle)."""
    P = m.P[m.I.reshape(-1)]
    UV = m.UV[m.I.reshape(-1)]
    a, b, c = P[0::3], P[1::3], P[2::3]
    fn = np.cross(b - a, c - a)
    fn /= np.maximum(np.linalg.norm(fn, axis=1, keepdims=True), 1e-12)
    N = np.repeat(fn, 3, axis=0)
    out = Mesh(P, N, UV, np.arange(len(P)).reshape(-1, 3))
    return out
