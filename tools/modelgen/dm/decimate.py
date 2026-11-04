"""Polygonreduktion (Quadrik-Fehlermetrik nach Garland/Heckbert) mit Kantenkollaps.
Behält gesperrte Ecken (Materialgrenzen), verhindert umklappende Dreiecke und Nicht-Mannigfaltigkeiten.
Innere Schleife in reinem Python (Listen statt kleiner numpy-Arrays) – deutlich schneller."""
from __future__ import annotations

import heapq

import numpy as np


def decimate(V, F, target, attrs=(), lock=None, max_err=None):
    V = np.asarray(V, np.float64)
    F = np.asarray(F, np.int64)
    n, m = len(V), len(F)
    if m <= target:
        return V.copy(), F.copy(), list(attrs)
    a, b, c = V[F[:, 0]], V[F[:, 1]], V[F[:, 2]]
    fn = np.cross(b - a, c - a)
    area = np.linalg.norm(fn, axis=1)
    fn = fn / np.maximum(area[:, None], 1e-20)
    d = -(fn * a).sum(1)
    A, Bq, Cq, D = fn[:, 0], fn[:, 1], fn[:, 2], d
    wq = np.maximum(area, 1e-12)
    comps = np.stack([A * A, A * Bq, A * Cq, A * D, Bq * Bq, Bq * Cq, Bq * D, Cq * Cq, Cq * D, D * D], axis=1) * wq[:, None]
    Q = np.zeros((n, 10))
    for k in range(3):
        np.add.at(Q, F[:, k], comps)
    Ql = Q.tolist()
    Vl = V.tolist()
    Fl = F.tolist()
    vf = [set() for _ in range(n)]
    for i, (x, y, z) in enumerate(Fl):
        vf[x].add(i); vf[y].add(i); vf[z].add(i)
    alive = [True] * m
    locked = [False] * n if lock is None else [bool(x) for x in lock]
    dead = [False] * n
    stamp = [0] * n

    def qerr(q, x, y, z):
        return (q[0] * x * x + 2 * q[1] * x * y + 2 * q[2] * x * z + 2 * q[3] * x + q[4] * y * y + 2 * q[5] * y * z + 2 * q[6] * y
                + q[7] * z * z + 2 * q[8] * z + q[9])

    def cost(i, j):
        q = [Ql[i][k] + Ql[j][k] for k in range(10)]
        pi, pj = Vl[i], Vl[j]
        cand = []
        if locked[i]:
            cand = [pi]
        elif locked[j]:
            cand = [pj]
        else:
            cand = [pi, pj, [(pi[0] + pj[0]) * 0.5, (pi[1] + pj[1]) * 0.5, (pi[2] + pj[2]) * 0.5]]
        best, bp = None, None
        for p in cand:
            e = qerr(q, p[0], p[1], p[2])
            if best is None or e < best:
                best, bp = e, p
        return best, bp

    heap = []
    seen = set()
    for x, y, z in Fl:
        for u, v in ((x, y), (y, z), (z, x)):
            key = (u, v) if u < v else (v, u)
            if key in seen:
                continue
            seen.add(key)
            if locked[key[0]] and locked[key[1]]:
                continue
            e, _ = cost(*key)
            heap.append((e, key[0], key[1], 0, 0))
    heapq.heapify(heap)
    faces_left = m

    def tri_normal(p0, p1, p2):
        ux, uy, uz = p1[0] - p0[0], p1[1] - p0[1], p1[2] - p0[2]
        vx, vy, vz = p2[0] - p0[0], p2[1] - p0[1], p2[2] - p0[2]
        return (uy * vz - uz * vy, uz * vx - ux * vz, ux * vy - uy * vx)

    while faces_left > target and heap:
        e, u, v, su, sv = heapq.heappop(heap)
        if dead[u] or dead[v] or stamp[u] != su or stamp[v] != sv:
            continue
        if max_err is not None and e > max_err:
            break
        if locked[u] and locked[v]:
            continue
        _, p = cost(u, v)
        if locked[v] and not locked[u]:
            keep, kill = v, u
        else:
            keep, kill = u, v
        shared = vf[u] & vf[v]
        if not shared:
            continue
        nu = set()
        for f in vf[u]:
            nu.update(Fl[f])
        nv = set()
        for f in vf[v]:
            nv.update(Fl[f])
        if len((nu & nv) - {u, v}) > 2:
            continue
        bad = False
        for w in (u, v):
            for f in vf[w]:
                if f in shared:
                    continue
                t0, t1, t2 = Fl[f]
                q0 = p if t0 in (u, v) else Vl[t0]
                q1 = p if t1 in (u, v) else Vl[t1]
                q2 = p if t2 in (u, v) else Vl[t2]
                nn = tri_normal(q0, q1, q2)
                on = tri_normal(Vl[t0], Vl[t1], Vl[t2])
                dot = nn[0] * on[0] + nn[1] * on[1] + nn[2] * on[2]
                if dot <= 0:
                    bad = True
                    break
            if bad:
                break
        if bad:
            continue
        Vl[keep] = list(p)
        Ql[keep] = [Ql[u][k] + Ql[v][k] for k in range(10)]
        for f in shared:
            alive[f] = False
            faces_left -= 1
            for t in Fl[f]:
                vf[t].discard(f)
        for f in list(vf[kill]):
            tri = Fl[f]
            for k in range(3):
                if tri[k] == kill:
                    tri[k] = keep
            vf[keep].add(f)
        vf[kill] = set()
        dead[kill] = True
        stamp[keep] += 1
        nb = set()
        for f in vf[keep]:
            nb.update(Fl[f])
        nb.discard(keep)
        for t in nb:
            if locked[keep] and locked[t]:
                continue
            i, j = (keep, t) if keep < t else (t, keep)
            ee, _ = cost(i, j)
            heapq.heappush(heap, (ee, i, j, stamp[i], stamp[j]))
    Fa = np.array([Fl[i] for i in range(m) if alive[i]], dtype=np.int64)
    Va = np.array(Vl)
    used, inv = np.unique(Fa.ravel(), return_inverse=True)
    return Va[used], inv.reshape(-1, 3), [np.asarray(at)[used] for at in attrs]
