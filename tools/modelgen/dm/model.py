"""Modellbeschreibung: Knoten (Türme) bzw. Skelett (Figuren), Teile, Animationen."""
from __future__ import annotations

import math
from dataclasses import dataclass, field

import numpy as np

from .geo import Mesh


@dataclass
class Node:
    name: str
    parent: str | None
    pos: tuple  # Ruhelage in Modellkoordinaten (Welt)
    tail: tuple | None = None  # nur für Knochen: Endpunkt (für Gewichte)


@dataclass
class Part:
    mesh: Mesh
    mat: str
    node: str            # starr an diesen Knoten/Knochen gebunden
    blend: tuple = ()    # optional weitere Knochen für weiche Gewichte
    skin: tuple | None = None  # (Knochennamen, Gewichte n×k) aus dem Bildhauer-Modul


@dataclass
class Channel:
    node: str
    path: str            # 'rotation' | 'translation' | 'scale'
    times: list
    values: list         # Quaternionen (x,y,z,w) bzw. Vektoren


@dataclass
class Model:
    name: str
    category: str        # tower | enemy | boss | hero | world
    title: str = ''
    skinned: bool = False
    nodes: dict = field(default_factory=dict)
    parts: list = field(default_factory=list)
    clips: dict = field(default_factory=dict)  # Name → [Channel]
    notes: str = ''

    def node(self, name, parent=None, pos=(0, 0, 0), tail=None):
        self.nodes[name] = Node(name, parent, tuple(float(v) for v in pos), tuple(float(v) for v in tail) if tail else None)
        return name

    def add(self, mesh: Mesh, mat: str, node: str | None = None, blend=()):
        node = node or next(iter(self.nodes))
        self.parts.append(Part(mesh, mat, node, tuple(blend)))
        return mesh

    def sculpt(self, sc, voxel=0.012, max_dim=150, node=None, target=None):
        """Bildhauer-Form einfügen; bei Figuren mit Gewichten aus den Knochen der Formen."""
        bones = sorted({p.bone for p in getattr(sc, 'wprims', sc.prims) if p.bone}) if self.skinned else None
        node = node or next(iter(self.nodes))
        for mat, mesh, w in sc.build(voxel, max_dim, bones, target):
            part = Part(mesh, mat, node)
            if w is not None:
                part.skin = (bones, w)
            self.parts.append(part)

    def world(self, name):
        return np.array(self.nodes[name].pos)

    @property
    def tris(self):
        return sum(p.mesh.tris for p in self.parts)

    # ---------------- Animation
    def clip(self, name):
        return self.clips.setdefault(name, [])

    def anim_rot(self, clip, node, keys, axis=(1, 0, 0)):
        """keys: Liste (Zeit s, Winkel °) um eine Achse; Achse auch je Schlüssel als (t, (ax,ay,az), °)."""
        times, vals = [], []
        for k in keys:
            if len(k) == 3:
                t, ax, deg = k
            else:
                (t, deg), ax = k, axis
            times.append(float(t))
            vals.append(quat(ax, deg))
        self.clip(clip).append(Channel(node, 'rotation', times, vals))

    def anim_rot_q(self, clip, node, keys):
        """keys: (t, Quaternion)"""
        self.clip(clip).append(Channel(node, 'rotation', [float(k[0]) for k in keys], [tuple(k[1]) for k in keys]))

    def anim_pos(self, clip, node, keys):
        """keys: (t, (dx,dy,dz)) relativ zur Ruhelage des Knotens."""
        base = self.local_pos(node)
        self.clip(clip).append(Channel(node, 'translation', [float(t) for t, _ in keys], [tuple(base + np.array(d, float)) for _, d in keys]))

    def anim_scale(self, clip, node, keys):
        self.clip(clip).append(Channel(node, 'scale', [float(t) for t, _ in keys], [tuple(np.broadcast_to(np.array(v, float), 3)) for _, v in keys]))

    def local_pos(self, name):
        n = self.nodes[name]
        p = np.array(n.pos)
        if n.parent:
            p = p - np.array(self.nodes[n.parent].pos)
        return p


def quat(axis, deg):
    ax = np.array(axis, dtype=float)
    ax = ax / (np.linalg.norm(ax) or 1)
    a = math.radians(deg) / 2
    s = math.sin(a)
    return (ax[0] * s, ax[1] * s, ax[2] * s, math.cos(a))


def qmul(a, b):
    ax, ay, az, aw = a
    bx, by, bz, bw = b
    return (aw * bx + ax * bw + ay * bz - az * by,
            aw * by - ax * bz + ay * bw + az * bx,
            aw * bz + ax * by - ay * bx + az * bw,
            aw * bw - ax * bx - ay * by - az * bz)


def qeuler(x=0.0, y=0.0, z=0.0):
    """Drehung in Grad: erst Z, dann X, dann Y (für Figuren gut lesbar)."""
    return qmul(quat((0, 1, 0), y), qmul(quat((1, 0, 0), x), quat((0, 0, 1), z)))
