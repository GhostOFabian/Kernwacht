"""Bildhauer-Modul: organische Formen aus Distanzfeldern (SDF), weich verschmolzen und mit
Marching Cubes in ein Mesh verwandelt. Liefert je Material ein Mesh mit Box-Projektions-UVs,
Vertex-Okklusion (COLOR_0) und – für Figuren – Knochengewichte aus der Zugehörigkeit zu den Formen.
"""
from __future__ import annotations

import math

import numpy as np
from skimage.measure import marching_cubes

from .geo import Mesh, fbm3

_EPS = 1e-9


def _rot_inv(R):
    return None if R is None else np.asarray(R, float)[:3, :3].T


def rot(yaw=0.0, pitch=0.0, roll=0.0):
    """Drehmatrix (Grad): Roll um Z, Pitch um X, Yaw um Y."""
    y, p, r = (math.radians(v) for v in (yaw, pitch, roll))
    Ry = np.array([[math.cos(y), 0, math.sin(y)], [0, 1, 0], [-math.sin(y), 0, math.cos(y)]])
    Rx = np.array([[1, 0, 0], [0, math.cos(p), -math.sin(p)], [0, math.sin(p), math.cos(p)]])
    Rz = np.array([[math.cos(r), -math.sin(r), 0], [math.sin(r), math.cos(r), 0], [0, 0, 1]])
    return Ry @ Rx @ Rz


class Prim:
    def __init__(self, kind, mat, bone=None, k=0.05, sub=False, noise=0.0, nfreq=8.0, **p):
        self.kind, self.mat, self.bone, self.k, self.sub, self.noise, self.nfreq, self.p = kind, mat, bone, k, sub, noise, nfreq, p

    def dist(self, P):
        p = self.p
        k = self.kind
        if k == 'capsule':
            a, b = np.asarray(p['a'], float), np.asarray(p['b'], float)
            r1, r2 = p['r1'], p['r2']
            ab = b - a
            L2 = float(ab @ ab) + _EPS
            t = np.clip(((P - a) @ ab) / L2, 0, 1)
            d = np.linalg.norm(P - (a + t[:, None] * ab), axis=1)
            return d - (r1 + (r2 - r1) * t)
        if k == 'ellipsoid':
            q = P - np.asarray(p['c'], float)
            Ri = _rot_inv(p.get('R'))
            if Ri is not None:
                q = q @ Ri.T
            r = np.asarray(p['r'], float)
            k0 = np.linalg.norm(q / r, axis=1)
            k1 = np.linalg.norm(q / (r * r), axis=1)
            return k0 * (k0 - 1.0) / (k1 + _EPS)
        if k == 'box':
            q = P - np.asarray(p['c'], float)
            Ri = _rot_inv(p.get('R'))
            if Ri is not None:
                q = q @ Ri.T
            b = np.asarray(p['h'], float) - p.get('r', 0.0)
            qq = np.abs(q) - b
            return np.linalg.norm(np.maximum(qq, 0), axis=1) + np.minimum(qq.max(axis=1), 0) - p.get('r', 0.0)
        if k == 'torus':
            q = P - np.asarray(p['c'], float)
            Ri = _rot_inv(p.get('R'))
            if Ri is not None:
                q = q @ Ri.T
            xz = np.hypot(q[:, 0], q[:, 2]) - p['R0']
            return np.hypot(xz, q[:, 1]) - p['r']
        if k == 'cone':  # gerundeter Kegel von a (Radius r1) nach b (r2) – wie capsule, aber flache Enden
            return Prim('capsule', self.mat, a=p['a'], b=p['b'], r1=p['r1'], r2=p['r2']).dist(P)
        raise ValueError(k)

    def bounds(self):
        p = self.p
        if self.kind in ('capsule', 'cone'):
            a, b = np.asarray(p['a'], float), np.asarray(p['b'], float)
            r = max(p['r1'], p['r2'])
            return np.minimum(a, b) - r, np.maximum(a, b) + r
        if self.kind == 'ellipsoid':
            c = np.asarray(p['c'], float)
            r = max(p['r'])
            return c - r, c + r
        if self.kind == 'box':
            c = np.asarray(p['c'], float)
            r = float(np.linalg.norm(p['h']))
            return c - r, c + r
        if self.kind == 'torus':
            c = np.asarray(p['c'], float)
            r = p['R0'] + p['r']
            return c - r, c + r


def smin(a, b, k):
    if k <= 0:
        return np.minimum(a, b)
    h = np.clip(0.5 + 0.5 * (b - a) / k, 0, 1)
    return b * (1 - h) + a * h - k * h * (1 - h)


def smax(a, b, k):
    return -smin(-a, -b, k)


class Sculpt:
    def __init__(self, detail=0.0, dfreq=10.0, seed=1):
        self.prims: list[Prim] = []
        self.detail, self.dfreq, self.seed = detail, dfreq, seed

    # ---- Formen
    def capsule(self, a, b, r1, r2=None, mat='skin', bone=None, k=0.04, sub=False, noise=0.0):
        self.prims.append(Prim('capsule', mat, bone, k, sub, noise, a=a, b=b, r1=r1, r2=r1 if r2 is None else r2))
        return self

    def ellipsoid(self, c, r, mat='skin', bone=None, k=0.04, R=None, sub=False, noise=0.0):
        r = (r, r, r) if np.isscalar(r) else r
        self.prims.append(Prim('ellipsoid', mat, bone, k, sub, noise, c=c, r=r, R=R))
        return self

    def box(self, c, h, mat='skin', bone=None, k=0.02, R=None, r=0.01, sub=False, noise=0.0):
        self.prims.append(Prim('box', mat, bone, k, sub, noise, c=c, h=h, R=R, r=r))
        return self

    def torus(self, c, R0, r, mat='skin', bone=None, k=0.02, R=None, sub=False, noise=0.0):
        self.prims.append(Prim('torus', mat, bone, k, sub, noise, c=c, R0=R0, r=r, R=R))
        return self

    def chain(self, pts, radii, mat='skin', bone=None, k=0.03, noise=0.0, bones=None):
        """Folge von Kapseln durch Punkte (Hörner, Schwänze, Tentakel, Finger)."""
        pts = [np.asarray(p, float) for p in pts]
        if np.isscalar(radii):
            radii = [radii] * len(pts)
        for i in range(len(pts) - 1):
            b = bones[i] if bones else bone
            self.capsule(pts[i], pts[i + 1], radii[i], radii[i + 1], mat, b, k, noise=noise)
        return self

    # ---- Auswertung
    def eval(self, P, want_ids=False):
        d = np.full(len(P), 1e9)
        ids = np.zeros(len(P), dtype=np.int64)
        best = np.full(len(P), 1e9)
        nz = None
        if self.detail > 0 or any(pr.noise > 0 for pr in self.prims):
            nz = (fbm3(P * self.dfreq, self.seed, 4) - 0.5) * 2
        for i, pr in enumerate(self.prims):
            di = pr.dist(P)
            if nz is not None and pr.noise > 0:
                di = di - nz * pr.noise
            if pr.sub:
                d = smax(d, -di, pr.k)
            else:
                d = smin(d, di, pr.k)
                if want_ids:
                    m = di < best
                    best = np.where(m, di, best)
                    ids = np.where(m, i, ids)
        if nz is not None and self.detail > 0:
            d = d - nz * self.detail
        return (d, ids) if want_ids else d

    def bounds(self):
        lo = np.array([1e9] * 3)
        hi = np.array([-1e9] * 3)
        for pr in self.prims:
            if pr.sub:
                continue
            a, b = pr.bounds()
            lo, hi = np.minimum(lo, a), np.maximum(hi, b)
        pad = 0.03 + self.detail * 2 + max([pr.noise for pr in self.prims] + [0]) * 2
        return lo - pad, hi + pad

    def mesh(self, voxel=0.012, max_dim=150, target=None):
        lo, hi = self.bounds()
        size = hi - lo
        vx = max(voxel, float(size.max()) / max_dim)
        n = np.ceil(size / vx).astype(int) + 2
        xs = lo[0] + np.arange(n[0]) * vx
        ys = lo[1] + np.arange(n[1]) * vx
        zs = lo[2] + np.arange(n[2]) * vx
        G = np.stack(np.meshgrid(xs, ys, zs, indexing='ij'), axis=-1).reshape(-1, 3)
        D = np.empty(len(G))
        CH = 400000
        for s in range(0, len(G), CH):
            D[s:s + CH] = self.eval(G[s:s + CH])
        vol = D.reshape(n[0], n[1], n[2])
        verts, faces, normals, _ = marching_cubes(vol, level=0.0, spacing=(vx, vx, vx))
        verts = verts + lo
        faces = faces[:, ::-1].copy()  # skimage-Wicklung → gegen den Uhrzeigersinn von außen
        verts, faces = drop_islands(verts, faces)
        if target and len(faces) > target:
            from .decimate import decimate
            _, ids0 = self.eval(verts, want_ids=True)
            mats0 = np.array([self.prims[i].mat for i in ids0])
            fm = mats0[faces]
            border = (fm[:, 0] != fm[:, 1]) | (fm[:, 1] != fm[:, 2])
            lock = np.zeros(len(verts), bool)
            lock[faces[border].ravel()] = True
            verts, faces, _ = decimate(verts, faces, target, lock=lock)
        # Normalen aus dem Feldgradienten (glatter als die MC-Normalen)
        e = vx * 0.5
        gx = self.eval(verts + [e, 0, 0]) - self.eval(verts - [e, 0, 0])
        gy = self.eval(verts + [0, e, 0]) - self.eval(verts - [0, e, 0])
        gz = self.eval(verts + [0, 0, e]) - self.eval(verts - [0, 0, e])
        N = np.column_stack([gx, gy, gz])
        N /= np.maximum(np.linalg.norm(N, axis=1, keepdims=True), 1e-12)
        # Okklusion aus dem Feld
        ao = np.ones(len(verts))
        for k, step in enumerate((0.015, 0.035, 0.07, 0.12)):
            dd = self.eval(verts + N * step)
            ao -= np.clip(step - dd, 0, None) / step * (0.5 ** k) * 0.55
        ao = np.clip(ao, 0.25, 1.0)
        _, ids = self.eval(verts, want_ids=True)
        return verts, faces, N, ao, ids, vx

    def build(self, voxel=0.012, max_dim=150, bones=None, target=None):
        """→ Liste (Material, Mesh, Gewichte|None). bones: Knochennamen für Gewichte. target: Dreiecksbudget."""
        V, F, N, AO, ids, vx = self.mesh(voxel, max_dim, target)
        mats = np.array([self.prims[i].mat for i in ids])
        # Material je Dreieck: Mehrheit der Ecken
        fm = mats[F]
        tri_mat = np.where(fm[:, 1] == fm[:, 2], fm[:, 1], fm[:, 0])
        W = None
        if bones:
            W = self._weights(V, bones, vx)
        out = []
        for mname in sorted(set(tri_mat)):
            sel = F[tri_mat == mname]
            used, inv = np.unique(sel.ravel(), return_inverse=True)
            ff = inv.reshape(-1, 3)
            P, NN, ao = V[used], N[used], AO[used]
            m = boxmap(P, NN, ff)
            # Okklusion als Vertexfarbe
            aoi = m.extra_src
            m.C = np.repeat(ao[aoi][:, None], 3, axis=1)
            w = W[used][aoi] if W is not None else None
            del m.extra_src
            out.append((mname, m, w))
        return out

    def _weights(self, V, bones, vx):
        bidx = {b: i for i, b in enumerate(bones)}
        acc = np.zeros((len(V), len(bones)))
        for pr in getattr(self, 'wprims', self.prims):
            if pr.sub or pr.bone is None:
                continue
            d = pr.dist(V)
            w = np.exp(-np.maximum(d, 0) / max(0.015, pr.k * 0.8 + 0.01)) ** 2
            acc[:, bidx[pr.bone]] = np.maximum(acc[:, bidx[pr.bone]], w)
        acc += 1e-9
        return acc / acc.sum(1, keepdims=True)


def boxmap(P, N, F) -> Mesh:
    """Würfelprojektion: jedes Dreieck bekommt die Ebene seiner Hauptnormalen; Ecken werden je Ebene dupliziert."""
    a, b, c = P[F[:, 0]], P[F[:, 1]], P[F[:, 2]]
    fn = np.cross(b - a, c - a)
    ax = np.argmax(np.abs(fn), axis=1)
    sg = np.sign(fn[np.arange(len(F)), ax])
    plane = ax * 2 + (sg > 0)
    key = F * 6 + plane[:, None]
    uniq, inv = np.unique(key.ravel(), return_inverse=True)
    src = uniq // 6
    pl = uniq % 6
    PP = P[src]
    UV = np.zeros((len(src), 2))
    axis = pl // 2
    pos = pl % 2
    for k, (u, v) in enumerate(((2, 1), (0, 2), (0, 1))):
        msk = axis == k
        su = np.where(pos[msk] == 1, 1.0, -1.0) if k != 1 else np.ones(msk.sum())
        UV[msk, 0] = PP[msk, u] * (su if k != 1 else 1)
        UV[msk, 1] = PP[msk, v]
    m = Mesh(PP, N[src], UV, inv.reshape(-1, 3))
    m.extra_src = src
    return m


def drop_islands(V, F, keep=0.01):
    """Entfernt abgetrennte Kleinteile (< keep der Dreiecke) aus Marching-Cubes-Ergebnissen."""
    from scipy.sparse import coo_matrix
    from scipy.sparse.csgraph import connected_components
    n = len(V)
    r = np.concatenate([F[:, 0], F[:, 1], F[:, 2]])
    c = np.concatenate([F[:, 1], F[:, 2], F[:, 0]])
    g = coo_matrix((np.ones(len(r)), (r, c)), shape=(n, n))
    k, lab = connected_components(g, directed=False)
    if k <= 1:
        return V, F
    fl = lab[F[:, 0]]
    cnt = np.bincount(fl, minlength=k)
    ok = cnt[fl] >= max(30, keep * len(F))
    F = F[ok]
    used, inv = np.unique(F.ravel(), return_inverse=True)
    return V[used], inv.reshape(-1, 3)


def _cache_eval(sc, P):
    """Distanzfeld aus einem vorab berechneten Gitter (trilinear) – stark beschleunigt für Schichten."""
    from scipy.ndimage import map_coordinates
    lo, vx, vol = sc._cache
    idx = ((P - lo) / vx).T
    d = map_coordinates(vol, idx, order=1, mode='nearest')
    # außerhalb des Gitters: grobe Schätzung über den Abstand zur Box
    hi = lo + (np.array(vol.shape) - 1) * vx
    out = np.linalg.norm(np.maximum(np.maximum(lo - P, P - hi), 0), axis=1)
    return np.where(out > 0, d + out, d)


def build_cache(sc, voxel=0.004):
    lo, hi = Sculpt.bounds(sc)
    lo, hi = lo - 0.05, hi + 0.05
    n = np.ceil((hi - lo) / voxel).astype(int) + 1
    xs, ys, zs = (lo[i] + np.arange(n[i]) * voxel for i in range(3))
    G = np.stack(np.meshgrid(xs, ys, zs, indexing='ij'), axis=-1).reshape(-1, 3)
    D = np.empty(len(G))
    for s in range(0, len(G), 400000):
        D[s:s + 400000] = Sculpt.eval(sc, G[s:s + 400000])
    sc._cache = (lo, voxel, D.reshape(n[0], n[1], n[2]))
    return sc


class Layer(Sculpt):
    """Kleidungs-/Rüstungsschicht: folgt der Körperform eines Basis-Sculpts im Abstand offset,
    begrenzt auf Regionen (mit sauberer Kante), mit Ausschnitten und flachen Zierrillen.
    So sitzt Rüstung passgenau, hat aber harte Ränder statt verschmolzener Klumpen."""

    def __init__(self, base: Sculpt, offset, mat, regions, cuts=(), grooves=(), groove_depth=0.004, edge=0.003, extra=(), detail=0.0, dfreq=20, seed=7):
        super().__init__(detail=detail, dfreq=dfreq, seed=seed)
        self.base, self.off, self.mat = base, offset, mat
        self.regions, self.cuts, self.grooves, self.gd, self.edge, self.extra = list(regions), list(cuts), list(grooves), groove_depth, edge, list(extra)
        self.prims = [Prim('ellipsoid', mat, None, c=(0, 0, 0), r=(1, 1, 1))]
        self.wprims = base.prims

    def eval(self, P, want_ids=False):
        d = (_cache_eval(self.base, P) if getattr(self.base, '_cache', None) is not None else self.base.eval(P)) - self.off
        r = np.full(len(P), 1e9)
        for g in self.regions:
            r = np.minimum(r, g.dist(P))
        d = smax(d, r, self.edge)
        for c in self.cuts:
            d = smax(d, -c.dist(P), self.edge)
        for g in self.grooves:
            s = -g.dist(P)
            d = np.maximum(d, np.minimum(s, d + self.gd))
        for e in self.extra:  # zusätzliche harte Formen (Grate, Nieten) im selben Material
            d = np.minimum(d, e.dist(P))
        if self.detail > 0:
            d = d - (fbm3(P * self.dfreq, self.seed, 3) - 0.5) * 2 * self.detail
        if want_ids:
            return d, np.zeros(len(P), dtype=np.int64)
        return d

    def bounds(self):
        lo = np.array([1e9] * 3)
        hi = np.array([-1e9] * 3)
        for g in self.regions + self.extra:
            a, b = g.bounds()
            lo, hi = np.minimum(lo, a), np.maximum(hi, b)
        blo, bhi = Sculpt.bounds(self.base)
        lo, hi = np.maximum(lo, blo - self.off), np.minimum(hi, bhi + self.off)
        return lo - self.off - 0.01, hi + self.off + 0.01

    def _weights(self, V, bones, vx):
        return Sculpt._weights(self, V, bones, vx)


def R(kind, mat='skin', **p):
    """Kurzform für eine Region/Schnitt-Form."""
    return Prim(kind, mat, None, 0.0, **p)
