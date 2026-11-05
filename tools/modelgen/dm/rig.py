"""Skelette und Grundanimationen (Idle, Walk, Attack, Death) für Figuren.

Konvention: Figur schaut nach +Z, links = +X, rechts = −X, Y oben. Knochen haben in Ruhelage keine
Drehung; Animationen drehen um Weltachsen der Ruhelage. Alle Clips heißen in jedem Modell gleich.
"""
from __future__ import annotations

import math

import numpy as np

from .model import Model, Part, qeuler


def skin_weights(model: Model, part: Part, jmap):
    n = len(part.mesh.P)
    J = np.zeros((n, 4), dtype=np.int64)
    W = np.zeros((n, 4), dtype=np.float64)
    if part.skin is not None:
        names, Wf = part.skin
        order = np.argsort(-Wf, axis=1)[:, :4]
        for c in range(min(4, Wf.shape[1])):
            J[:, c] = np.array([jmap[names[i]] for i in range(len(names))])[order[:, c]]
            W[:, c] = Wf[np.arange(n), order[:, c]]
        W[W < 0.03] = 0
        W /= np.maximum(W.sum(1, keepdims=True), 1e-9)
        return J, W.astype(np.float32)
    bones = (part.node,) + tuple(part.blend)
    if len(bones) == 1:
        J[:, 0] = jmap[part.node]
        W[:, 0] = 1.0
        return J, W.astype(np.float32)
    D = []
    for b in bones:
        nd = model.nodes[b]
        a = np.array(nd.pos)
        t = np.array(nd.tail) if nd.tail else a
        ab = t - a
        L2 = float(ab @ ab)
        if L2 < 1e-12:
            d = np.linalg.norm(part.mesh.P - a, axis=1)
        else:
            s = np.clip(((part.mesh.P - a) @ ab) / L2, 0, 1)
            d = np.linalg.norm(part.mesh.P - (a + s[:, None] * ab), axis=1)
        D.append(d)
    D = np.array(D).T
    w = 1.0 / (D * D + 1e-4) ** 2
    order = np.argsort(-w, axis=1)[:, :4]
    k = min(4, len(bones))
    for c in range(k):
        J[:, c] = [jmap[bones[i]] for i in order[:, c]]
        W[:, c] = w[np.arange(n), order[:, c]]
    W /= W.sum(1, keepdims=True)
    W[W < 0.02] = 0
    W /= W.sum(1, keepdims=True)
    return J, W.astype(np.float32)


def loop_keys(n, period, fn):
    """n+1 Schlüssel über eine Periode; fn(phase 0..1) → Winkel."""
    return [(period * i / n, fn(i / n)) for i in range(n + 1)]


S = math.sin
C = math.cos
PI2 = math.tau


# ====================================================================== Humanoid

class Humanoid:
    """Zweibeiner. h = Gesamthöhe, w = Breite (Schultern), legs = Beinanteil."""

    def __init__(self, m: Model, h=1.0, w=1.0, legs=0.48, arm=1.0, hover=0.0, hunch=0.0):
        self.m, self.h = m, h
        hip_y = h * legs + hover
        self.hip_y = hip_y
        sh = h * 0.82 + hover
        self.P = P = {
            'root': (0, 0, 0),
            'hips': (0, hip_y, 0),
            'spine': (0, hip_y + h * 0.12, 0),
            'chest': (0, hip_y + h * 0.24, hunch * 0.08 * h),
            'neck': (0, sh + h * 0.02, hunch * 0.15 * h),
            'head': (0, sh + h * 0.07, hunch * 0.2 * h),
            'upperarm_L': (0.17 * h * w, sh, 0), 'forearm_L': (0.19 * h * w, sh - 0.17 * h * arm, 0), 'hand_L': (0.2 * h * w, sh - 0.33 * h * arm, 0.01),
            'upperarm_R': (-0.17 * h * w, sh, 0), 'forearm_R': (-0.19 * h * w, sh - 0.17 * h * arm, 0), 'hand_R': (-0.2 * h * w, sh - 0.33 * h * arm, 0.01),
            'thigh_L': (0.085 * h * w, hip_y - 0.02 * h, 0), 'shin_L': (0.09 * h * w, hip_y * 0.52 + hover * 0.48, 0.01), 'foot_L': (0.09 * h * w, h * 0.05 + hover, 0),
            'thigh_R': (-0.085 * h * w, hip_y - 0.02 * h, 0), 'shin_R': (-0.09 * h * w, hip_y * 0.52 + hover * 0.48, 0.01), 'foot_R': (-0.09 * h * w, h * 0.05 + hover, 0),
        }
        par = {'root': None, 'hips': 'root', 'spine': 'hips', 'chest': 'spine', 'neck': 'chest', 'head': 'neck',
               'upperarm_L': 'chest', 'forearm_L': 'upperarm_L', 'hand_L': 'forearm_L',
               'upperarm_R': 'chest', 'forearm_R': 'upperarm_R', 'hand_R': 'forearm_R',
               'thigh_L': 'hips', 'shin_L': 'thigh_L', 'foot_L': 'shin_L',
               'thigh_R': 'hips', 'shin_R': 'thigh_R', 'foot_R': 'shin_R'}
        tails = {'hips': 'spine', 'spine': 'chest', 'chest': 'neck', 'neck': 'head', 'upperarm_L': 'forearm_L', 'forearm_L': 'hand_L',
                 'upperarm_R': 'forearm_R', 'forearm_R': 'hand_R', 'thigh_L': 'shin_L', 'shin_L': 'foot_L', 'thigh_R': 'shin_R', 'shin_R': 'foot_R'}
        for k, v in P.items():
            t = P[tails[k]] if k in tails else None
            if k == 'head':
                t = (v[0], v[1] + 0.12 * h, v[2])
            if k.startswith('hand'):
                t = (v[0], v[1] - 0.06 * h, v[2])
            if k.startswith('foot'):
                t = (v[0], 0, v[2] + 0.08 * h)
            m.node(k, par[k], v, t)

    def p(self, k):
        return np.array(self.P[k])

    def anims(self, style='melee', speed=1.0, heavy=False, fly=False):
        m, h = self.m, self.h
        amp = 0.7 if heavy else 1.0
        # ---- Idle (2 s)
        T = 2.0
        m.anim_rot('Idle', 'chest', loop_keys(8, T, lambda f: 2.5 * S(f * PI2)))
        m.anim_rot('Idle', 'head', loop_keys(8, T, lambda f: -2 * S(f * PI2 + 0.6)))
        m.anim_rot('Idle', 'upperarm_L', loop_keys(8, T, lambda f: 2 + 3 * S(f * PI2)), axis=(0, 0, 1))
        m.anim_rot('Idle', 'upperarm_R', loop_keys(8, T, lambda f: -2 - 3 * S(f * PI2)), axis=(0, 0, 1))
        m.anim_pos('Idle', 'hips', loop_keys(8, T, lambda f: (0, -0.008 * h * (1 + S(f * PI2 * 2 - 1.5)) + (0.02 * h * S(f * PI2) if fly else 0), 0)))
        # ---- Walk (Schritt-Zyklus)
        T = 0.9 / speed * (1.25 if heavy else 1)
        sw = 28 * amp
        if fly:
            m.anim_rot('Walk', 'hips', loop_keys(8, T, lambda f: 12 + 2 * S(f * PI2)))
            for side, sg in (('L', 1), ('R', -1)):
                m.anim_rot('Walk', 'thigh_' + side, loop_keys(8, T, lambda f, sg=sg: 15 + 6 * S(f * PI2 + (0 if sg > 0 else 1))))
                m.anim_rot('Walk', 'shin_' + side, loop_keys(8, T, lambda f: 25))
            m.anim_pos('Walk', 'hips', loop_keys(8, T, lambda f: (0, 0.03 * h * S(f * PI2), 0)))
        else:
            m.anim_rot('Walk', 'thigh_L', loop_keys(12, T, lambda f: -sw * S(f * PI2)))
            m.anim_rot('Walk', 'thigh_R', loop_keys(12, T, lambda f: sw * S(f * PI2)))
            m.anim_rot('Walk', 'shin_L', loop_keys(12, T, lambda f: 35 * amp * max(0, S(f * PI2 + 1.2)) + 4))
            m.anim_rot('Walk', 'shin_R', loop_keys(12, T, lambda f: 35 * amp * max(0, S(f * PI2 + 1.2 + math.pi)) + 4))
            m.anim_rot('Walk', 'foot_L', loop_keys(12, T, lambda f: 12 * S(f * PI2 - 0.6)))
            m.anim_rot('Walk', 'foot_R', loop_keys(12, T, lambda f: -12 * S(f * PI2 - 0.6)))
            m.anim_pos('Walk', 'hips', loop_keys(12, T, lambda f: (0, 0.025 * h * amp * abs(C(f * PI2)) - 0.02 * h, 0)))
            m.anim_rot('Walk', 'hips', loop_keys(12, T, lambda f: 4 * S(f * PI2)), axis=(0, 1, 0))
        m.anim_rot('Walk', 'chest', loop_keys(12, T, lambda f: (0, 1, 0) and -6 * S(f * PI2)), axis=(0, 1, 0))
        if style in ('melee', 'claw', 'brute'):
            m.anim_rot('Walk', 'upperarm_L', loop_keys(12, T, lambda f: 20 * amp * S(f * PI2)))
            m.anim_rot('Walk', 'upperarm_R', loop_keys(12, T, lambda f: -18 * amp * S(f * PI2) - 10))
        else:
            m.anim_rot('Walk', 'upperarm_L', loop_keys(12, T, lambda f: 16 * S(f * PI2)))
            m.anim_rot('Walk', 'upperarm_R', loop_keys(12, T, lambda f: -25 - 6 * S(f * PI2)))
            m.anim_rot('Walk', 'forearm_R', loop_keys(4, T, lambda f: -30))
        m.anim_rot('Walk', 'forearm_L', loop_keys(4, T, lambda f: -15))
        # ---- Attack (einmalig, ~0,9 s)
        if style in ('melee', 'brute'):
            ks = [(0, 0), (0.3, -150), (0.45, -160), (0.6, -40), (0.9, 0)]
            m.anim_rot('Attack', 'upperarm_R', ks)
            m.anim_rot('Attack', 'forearm_R', [(0, 0), (0.3, -50), (0.6, 0), (0.9, 0)])
            m.anim_rot('Attack', 'chest', [(0, (0, 1, 0), 0), (0.3, (0, 1, 0), 25), (0.6, (0, 1, 0), -30), (0.9, (0, 1, 0), 0)])
            m.anim_rot('Attack', 'spine', [(0, 0), (0.3, -8), (0.6, 14), (0.9, 0)])
            m.anim_rot('Attack', 'upperarm_L', [(0, 0), (0.3, -30), (0.6, 10), (0.9, 0)])
            m.anim_rot('Attack', 'thigh_R', [(0, 0), (0.6, 20), (0.9, 0)])
            m.anim_rot('Attack', 'thigh_L', [(0, 0), (0.6, -25), (0.9, 0)])
        elif style == 'claw':
            for side, sg, d in (('L', 1, 0.0), ('R', -1, 0.15)):
                m.anim_rot('Attack', 'upperarm_' + side, [(0, 0), (0.25 + d, -120), (0.5 + d, -30), (0.85, 0)])
                m.anim_rot('Attack', 'forearm_' + side, [(0, 0), (0.25 + d, -60), (0.5 + d, -5), (0.85, 0)])
            m.anim_rot('Attack', 'spine', [(0, 0), (0.25, -12), (0.55, 22), (0.85, 0)])
            m.anim_rot('Attack', 'head', [(0, 0), (0.25, -20), (0.55, 15), (0.85, 0)])
        elif style == 'ranged':
            m.anim_rot('Attack', 'upperarm_L', [(0, 0), (0.25, (0.2, 0, 1), -85), (0.7, (0.2, 0, 1), -85), (0.9, 0)], axis=(1, 0, 0))
            m.anim_rot('Attack', 'upperarm_R', [(0, 0), (0.25, -80), (0.55, -82), (0.6, -60), (0.9, 0)])
            m.anim_rot('Attack', 'forearm_R', [(0, 0), (0.25, -60), (0.55, -95), (0.6, -20), (0.9, 0)])
            m.anim_rot('Attack', 'chest', [(0, (0, 1, 0), 0), (0.25, (0, 1, 0), 35), (0.7, (0, 1, 0), 35), (0.9, (0, 1, 0), 0)])
        else:  # caster
            m.anim_rot('Attack', 'upperarm_R', [(0, 0), (0.25, -60), (0.45, -140), (0.65, -70), (0.9, 0)])
            m.anim_rot('Attack', 'upperarm_L', [(0, 0), (0.3, -70), (0.6, -90), (0.9, 0)])
            m.anim_rot('Attack', 'forearm_L', [(0, 0), (0.3, -50), (0.9, 0)])
            m.anim_rot('Attack', 'chest', [(0, 0), (0.3, -10), (0.6, 8), (0.9, 0)])
            m.anim_rot('Attack', 'head', [(0, 0), (0.3, -10), (0.6, 5), (0.9, 0)])
        # ---- Death (1,2 s, bleibt liegen)
        hy = self.hip_y
        m.anim_rot('Death', 'hips', [(0, 0), (0.25, 10), (0.7, -75), (1.0, -90), (1.2, -88)])
        m.anim_pos('Death', 'hips', [(0, (0, 0, 0)), (0.25, (0, -0.05 * h, 0)), (0.7, (0, -hy * 0.65, -0.15 * h)), (1.0, (0, -hy * 0.78, -0.25 * h)), (1.2, (0, -hy * 0.78, -0.25 * h))])
        m.anim_rot('Death', 'chest', [(0, 0), (0.3, 20), (1.0, -10), (1.2, -8)])
        m.anim_rot('Death', 'head', [(0, 0), (0.4, 25), (1.0, -20), (1.2, -18)])
        m.anim_rot('Death', 'upperarm_L', [(0, (0, 0, 1), 0), (0.6, (0, 0, 1), 60), (1.2, (0, 0, 1), 80)])
        m.anim_rot('Death', 'upperarm_R', [(0, (0, 0, 1), 0), (0.6, (0, 0, 1), -60), (1.2, (0, 0, 1), -75)])
        m.anim_rot('Death', 'thigh_L', [(0, 0), (0.5, -40), (1.2, -10)])
        m.anim_rot('Death', 'shin_L', [(0, 0), (0.5, 60), (1.2, 20)])
        m.anim_rot('Death', 'thigh_R', [(0, 0), (0.5, -20), (1.2, 5)])


# ====================================================================== Vierbeiner

class Quadruped:
    def __init__(self, m: Model, length=1.0, height=0.5, neck=0.25, tail=0.4, low=False):
        self.m = m
        L, H = length, height
        self.L, self.H = L, H
        P = self.P = {
            'root': (0, 0, 0),
            'body': (0, H, -0.1 * L),
            'chest': (0, H * 1.02, 0.25 * L),
            'neck': (0, H * 1.1, 0.42 * L),
            'head': (0, H * 1.1 + neck * 0.6, 0.42 * L + neck * 0.8),
            'jaw': (0, H * 1.1 + neck * 0.5, 0.42 * L + neck * 0.95),
            'tail1': (0, H * 1.02, -0.45 * L),
            'tail2': (0, H * 0.95, -0.45 * L - tail * 0.5),
        }
        par = {'root': None, 'body': 'root', 'chest': 'body', 'neck': 'chest', 'head': 'neck', 'jaw': 'head', 'tail1': 'body', 'tail2': 'tail1'}
        legs = {'FL': (0.16 * L, 0.3 * L, 'chest'), 'FR': (-0.16 * L, 0.3 * L, 'chest'), 'BL': (0.16 * L, -0.32 * L, 'body'), 'BR': (-0.16 * L, -0.32 * L, 'body')}
        for k, (x, z, pa) in legs.items():
            P['leg_' + k] = (x, H * 0.95, z)
            P['shin_' + k] = (x, H * 0.48, z + (0.04 if k[0] == 'F' else -0.05) * L)
            P['paw_' + k] = (x, 0.04, z)
            par['leg_' + k] = pa
            par['shin_' + k] = 'leg_' + k
            par['paw_' + k] = 'shin_' + k
        tails = {'body': 'chest', 'chest': 'neck', 'neck': 'head', 'tail1': 'tail2'}
        for k in list(legs):
            tails['leg_' + k] = 'shin_' + k
            tails['shin_' + k] = 'paw_' + k
        for k, v in P.items():
            t = P[tails[k]] if k in tails else None
            if k == 'head':
                t = (v[0], v[1], v[2] + 0.2 * L)
            if k == 'tail2':
                t = (v[0], v[1] - 0.05, v[2] - tail * 0.5)
            m.node(k, par[k], v, t)

    def anims(self, speed=1.0, bite=True):
        m = self.m
        T = 2.0
        m.anim_rot('Idle', 'chest', loop_keys(8, T, lambda f: 2 * S(f * PI2)))
        m.anim_rot('Idle', 'head', loop_keys(8, T, lambda f: 4 * S(f * PI2 + 1)))
        m.anim_rot('Idle', 'tail1', loop_keys(8, T, lambda f: 12 * S(f * PI2)), axis=(0, 1, 0))
        m.anim_pos('Idle', 'body', loop_keys(8, T, lambda f: (0, 0.01 * self.H * S(f * PI2 * 2), 0)))
        T = 0.7 / speed
        ph = {'FL': 0, 'BR': 0, 'FR': math.pi, 'BL': math.pi}
        for k, p in ph.items():
            m.anim_rot('Walk', 'leg_' + k, loop_keys(12, T, lambda f, p=p: -30 * S(f * PI2 + p)))
            m.anim_rot('Walk', 'shin_' + k, loop_keys(12, T, lambda f, p=p, k=k: (1 if k[0] == 'B' else -1) * 30 * max(0, S(f * PI2 + p + 1.3))))
        m.anim_pos('Walk', 'body', loop_keys(12, T, lambda f: (0, 0.03 * self.H * abs(S(f * PI2)), 0)))
        m.anim_rot('Walk', 'tail1', loop_keys(12, T, lambda f: 15 * S(f * PI2)), axis=(0, 1, 0))
        m.anim_rot('Walk', 'head', loop_keys(12, T, lambda f: 4 * S(f * PI2 * 2)))
        m.anim_rot('Attack', 'chest', [(0, 0), (0.3, -12), (0.5, 15), (0.8, 0)])
        m.anim_rot('Attack', 'neck', [(0, 0), (0.3, -25), (0.5, 25), (0.8, 0)])
        m.anim_rot('Attack', 'jaw', [(0, 0), (0.3, 35), (0.5, 0), (0.8, 0)])
        m.anim_pos('Attack', 'body', [(0, (0, 0, 0)), (0.3, (0, 0, -0.06 * self.L)), (0.5, (0, 0, 0.12 * self.L)), (0.8, (0, 0, 0))])
        m.anim_rot('Attack', 'leg_FL', [(0, 0), (0.3, -35), (0.5, 10), (0.8, 0)])
        m.anim_rot('Attack', 'leg_FR', [(0, 0), (0.3, -35), (0.5, 10), (0.8, 0)])
        m.anim_rot('Death', 'body', [(0, (0, 0, 1), 0), (0.5, (0, 0, 1), 50), (1.0, (0, 0, 1), 88), (1.2, (0, 0, 1), 90)])
        m.anim_pos('Death', 'body', [(0, (0, 0, 0)), (0.5, (0, -self.H * 0.3, 0)), (1.0, (-0.08 * self.L, -self.H * 0.62, 0)), (1.2, (-0.08 * self.L, -self.H * 0.62, 0))])
        for k in ph:
            m.anim_rot('Death', 'leg_' + k, [(0, 0), (0.8, -20 if k[0] == 'F' else 20), (1.2, -25 if k[0] == 'F' else 25)])
        m.anim_rot('Death', 'neck', [(0, 0), (0.8, 30), (1.2, 35)])
        m.anim_rot('Death', 'jaw', [(0, 0), (0.8, 25), (1.2, 25)])


# ====================================================================== Flieger

class Flyer:
    def __init__(self, m: Model, span=1.0, body=0.5, alt=0.9):
        self.m, self.span, self.alt = m, span, alt
        P = self.P = {
            'root': (0, 0, 0),
            'body': (0, alt, 0),
            'neck': (0, alt + 0.04, body * 0.4),
            'head': (0, alt + 0.08, body * 0.65),
            'jaw': (0, alt + 0.04, body * 0.72),
            'wing_L': (0.08 * span, alt + 0.06, 0.05), 'wingtip_L': (0.32 * span, alt + 0.1, 0.0),
            'wing_R': (-0.08 * span, alt + 0.06, 0.05), 'wingtip_R': (-0.32 * span, alt + 0.1, 0.0),
            'tail1': (0, alt, -body * 0.45), 'tail2': (0, alt - 0.03, -body * 0.85),
            'leg_L': (0.06, alt - 0.08, 0.0), 'leg_R': (-0.06, alt - 0.08, 0.0),
        }
        par = {'root': None, 'body': 'root', 'neck': 'body', 'head': 'neck', 'jaw': 'head', 'wing_L': 'body', 'wingtip_L': 'wing_L',
               'wing_R': 'body', 'wingtip_R': 'wing_R', 'tail1': 'body', 'tail2': 'tail1', 'leg_L': 'body', 'leg_R': 'body'}
        tails = {'wing_L': 'wingtip_L', 'wing_R': 'wingtip_R', 'neck': 'head', 'tail1': 'tail2', 'body': 'neck'}
        for k, v in P.items():
            t = P[tails[k]] if k in tails else None
            if k == 'wingtip_L':
                t = (0.5 * span, v[1], -0.05)
            if k == 'wingtip_R':
                t = (-0.5 * span, v[1], -0.05)
            if k == 'head':
                t = (0, v[1], v[2] + 0.15 * body)
            m.node(k, par[k], v, t)

    def anims(self, speed=1.0):
        m = self.m
        for clip, T, A, bob in (('Idle', 0.8, 30, 0.04), ('Walk', 0.55 / speed, 42, 0.06)):
            m.anim_rot(clip, 'wing_L', loop_keys(12, T, lambda f, A=A: A * S(f * PI2)), axis=(0, 0, 1))
            m.anim_rot(clip, 'wing_R', loop_keys(12, T, lambda f, A=A: -A * S(f * PI2)), axis=(0, 0, 1))
            m.anim_rot(clip, 'wingtip_L', loop_keys(12, T, lambda f, A=A: A * 0.6 * S(f * PI2 - 0.8)), axis=(0, 0, 1))
            m.anim_rot(clip, 'wingtip_R', loop_keys(12, T, lambda f, A=A: -A * 0.6 * S(f * PI2 - 0.8)), axis=(0, 0, 1))
            m.anim_pos(clip, 'body', loop_keys(12, T, lambda f, bob=bob: (0, -bob * self.alt * S(f * PI2), 0)))
            m.anim_rot(clip, 'tail1', loop_keys(12, T, lambda f: 8 * S(f * PI2 + 1)))
            m.anim_rot(clip, 'body', loop_keys(12, T, lambda f, clip=clip: (10 if clip == 'Walk' else 0) + 3 * S(f * PI2)))
        m.anim_rot('Attack', 'body', [(0, 0), (0.3, -20), (0.55, 35), (0.9, 0)])
        m.anim_pos('Attack', 'body', [(0, (0, 0, 0)), (0.3, (0, 0.1, -0.05)), (0.55, (0, -0.25 * self.alt, 0.2)), (0.9, (0, 0, 0))])
        m.anim_rot('Attack', 'wing_L', [(0, (0, 0, 1), 0), (0.3, (0, 0, 1), 60), (0.55, (0, 0, 1), -20), (0.9, (0, 0, 1), 0)])
        m.anim_rot('Attack', 'wing_R', [(0, (0, 0, 1), 0), (0.3, (0, 0, 1), -60), (0.55, (0, 0, 1), 20), (0.9, (0, 0, 1), 0)])
        m.anim_rot('Attack', 'jaw', [(0, 0), (0.3, 30), (0.55, 0), (0.9, 0)])
        m.anim_rot('Death', 'body', [(0, (1, 0, 0), 0), (0.4, (0.3, 0, 1), 60), (1.0, (0.3, 0, 1), 160), (1.2, (0.3, 0, 1), 170)])
        m.anim_pos('Death', 'body', [(0, (0, 0, 0)), (0.4, (0, -0.2 * self.alt, 0)), (1.0, (0, -self.alt * 0.92, 0)), (1.2, (0, -self.alt * 0.92, 0))])
        m.anim_rot('Death', 'wing_L', [(0, (0, 0, 1), 0), (1.0, (0, 0, 1), -50), (1.2, (0, 0, 1), -55)])
        m.anim_rot('Death', 'wing_R', [(0, (0, 0, 1), 0), (1.0, (0, 0, 1), 35), (1.2, (0, 0, 1), 40)])


# ====================================================================== Spinnenartige (6/8 Beine)

class Crawler:
    def __init__(self, m: Model, size=1.0, legs=8, height=0.35):
        self.m, self.size, self.n, self.H = m, size, legs, height
        P = {'root': (0, 0, 0), 'body': (0, height * size, 0), 'head': (0, height * size * 1.05, 0.32 * size),
             'jaw': (0, height * size * 0.9, 0.45 * size), 'abdomen': (0, height * size * 1.15, -0.35 * size)}
        par = {'root': None, 'body': 'root', 'head': 'body', 'jaw': 'head', 'abdomen': 'body'}
        tails = {'abdomen': (0, height * size * 1.1, -0.8 * size), 'head': (0, height * size, 0.5 * size)}
        self.legs = []
        per = legs // 2
        for i in range(per):
            z = (0.22 - 0.44 * i / max(1, per - 1)) * size
            for side, sg in (('L', 1), ('R', -1)):
                k = f'{i}{side}'
                hip = (sg * 0.14 * size, height * size, z)
                knee = (sg * 0.42 * size, height * size * 1.6, z * 1.3)
                foot = (sg * 0.7 * size, 0.0, z * 1.6)
                P['leg' + k] = hip; P['knee' + k] = knee; P['foot' + k] = foot
                par['leg' + k] = 'body'; par['knee' + k] = 'leg' + k; par['foot' + k] = 'knee' + k
                self.legs.append((k, i, sg))
        for k, v in P.items():
            t = tails.get(k)
            if k.startswith('leg'):
                t = P['knee' + k[3:]]
            if k.startswith('knee'):
                t = P['foot' + k[4:]]
            m.node(k, par[k], v, t)
        self.P = P

    def anims(self, speed=1.0):
        m = self.m
        T = 2.0
        m.anim_pos('Idle', 'body', loop_keys(8, T, lambda f: (0, 0.01 * self.size * S(f * PI2), 0)))
        m.anim_rot('Idle', 'abdomen', loop_keys(8, T, lambda f: 4 * S(f * PI2)))
        m.anim_rot('Idle', 'jaw', loop_keys(8, T, lambda f: 8 * max(0, S(f * PI2 * 2))))
        T = 0.6 / speed
        for k, i, sg in self.legs:
            p = (i % 2) * math.pi + (0 if sg > 0 else math.pi)
            m.anim_rot('Walk', 'leg' + k, loop_keys(12, T, lambda f, p=p: 22 * S(f * PI2 + p)), axis=(0, 1, 0))
            m.anim_rot('Walk', 'knee' + k, loop_keys(12, T, lambda f, p=p, sg=sg: sg * 14 * max(0, S(f * PI2 + p + 1.5))), axis=(0, 0, 1))
        m.anim_pos('Walk', 'body', loop_keys(12, T, lambda f: (0, 0.015 * self.size * abs(S(f * PI2 * 2)), 0)))
        m.anim_rot('Walk', 'abdomen', loop_keys(12, T, lambda f: 6 * S(f * PI2)), axis=(0, 1, 0))
        m.anim_rot('Attack', 'body', [(0, 0), (0.3, -18), (0.5, 12), (0.8, 0)])
        m.anim_rot('Attack', 'jaw', [(0, 0), (0.3, 35), (0.5, -5), (0.8, 0)])
        for k, i, sg in self.legs:
            if i == 0:
                m.anim_rot('Attack', 'leg' + k, [(0, (0, 0, 1), 0), (0.3, (0, 0, 1), sg * 45), (0.5, (0, 0, 1), sg * -10), (0.8, (0, 0, 1), 0)])
        m.anim_pos('Death', 'body', [(0, (0, 0, 0)), (0.6, (0, -self.H * self.size * 0.6, 0)), (1.2, (0, -self.H * self.size * 0.75, 0))])
        m.anim_rot('Death', 'body', [(0, (0, 0, 1), 0), (1.2, (0, 0, 1), 12)])
        for k, i, sg in self.legs:
            m.anim_rot('Death', 'knee' + k, [(0, (0, 0, 1), 0), (0.8, (0, 0, 1), -sg * 50), (1.2, (0, 0, 1), -sg * 60)])


# ====================================================================== Kette (Schlange, Wurm, Schwarm)

class Chain:
    """Gelenkkette entlang −Z (Kopf vorn bei +Z) für Würmer, Tentakel, Schwärme."""

    def __init__(self, m: Model, n=6, length=1.5, height=0.3, up=0.0):
        self.m, self.n = m, n
        m.node('root', None, (0, 0, 0))
        prev = 'root'
        self.bones = []
        for i in range(n):
            z = length * 0.5 - length * i / (n - 1)
            y = height + up * (1 - i / (n - 1))
            nm = f'seg{i}'
            zt = length * 0.5 - length * (i + 1) / (n - 1)
            m.node(nm, prev, (0, y, z), (0, height + up * (1 - (i + 1) / (n - 1)), zt))
            prev = nm
            self.bones.append(nm)

    def anims(self, speed=1.0, rear=20):
        m = self.m
        T = 2.2
        for i, b in enumerate(self.bones):
            m.anim_rot('Idle', b, loop_keys(8, T, lambda f, i=i: 6 * S(f * PI2 + i * 0.7)), axis=(0, 1, 0))
        T = 1.0 / speed
        for i, b in enumerate(self.bones):
            m.anim_rot('Walk', b, loop_keys(12, T, lambda f, i=i: 14 * S(f * PI2 + i * 0.9)), axis=(0, 1, 0))
        m.anim_rot('Attack', self.bones[0], [(0, 0), (0.35, -rear), (0.55, rear * 0.8), (0.9, 0)])
        m.anim_rot('Attack', self.bones[1], [(0, 0), (0.35, -rear * 0.6), (0.55, rear * 0.4), (0.9, 0)])
        for i, b in enumerate(self.bones):
            m.anim_rot('Death', b, [(0, (0, 1, 0), 0), (0.8, (0, 1, 0), 25 * (1 if i % 2 else -1)), (1.2, (0, 1, 0), 30 * (1 if i % 2 else -1))])
        m.anim_pos('Death', self.bones[0], [(0, (0, 0, 0)), (1.2, (0, -0.15, 0))])


# ====================================================================== Schwebende Konstrukte

class Floater:
    """Schwebendes Wesen mit Kern und umkreisenden Teilen (orbit0..n)."""

    def __init__(self, m: Model, alt=1.0, orbits=3, radius=0.5, arms=False):
        self.m, self.alt, self.k, self.R = m, alt, orbits, radius
        m.node('root', None, (0, 0, 0))
        m.node('core', 'root', (0, alt, 0), (0, alt + 0.3, 0))
        m.node('ring', 'core', (0, alt, 0))
        for i in range(orbits):
            a = math.tau * i / orbits
            m.node(f'orbit{i}', 'ring', (math.cos(a) * radius, alt, math.sin(a) * radius))
        self.arms = arms

    def anims(self):
        m, a = self.m, self.alt
        T = 3.0
        m.anim_pos('Idle', 'core', loop_keys(8, T, lambda f: (0, 0.06 * a * S(f * PI2), 0)))
        m.anim_rot('Idle', 'ring', loop_keys(8, T, lambda f: 360 * f), axis=(0, 1, 0))
        T = 1.5
        m.anim_pos('Walk', 'core', loop_keys(8, T, lambda f: (0, 0.04 * a * S(f * PI2 * 2), 0)))
        m.anim_rot('Walk', 'core', loop_keys(8, T, lambda f: 10 + 2 * S(f * PI2)))
        m.anim_rot('Walk', 'ring', loop_keys(8, T, lambda f: 360 * f), axis=(0, 1, 0))
        m.anim_scale('Attack', 'ring', [(0, 1), (0.3, 0.7), (0.5, 1.35), (0.9, 1)])
        m.anim_rot('Attack', 'ring', [(0, (0, 1, 0), 0), (0.3, (0, 1, 0), 120), (0.5, (0, 1, 0), 300), (0.9, (0, 1, 0), 359)])
        m.anim_pos('Attack', 'core', [(0, (0, 0, 0)), (0.3, (0, 0.1 * a, 0)), (0.5, (0, -0.05 * a, 0.05)), (0.9, (0, 0, 0))])
        m.anim_pos('Death', 'core', [(0, (0, 0, 0)), (0.5, (0, 0.1 * a, 0)), (1.2, (0, -a * 0.85, 0))])
        m.anim_rot('Death', 'core', [(0, (1, 0, 0), 0), (1.2, (1, 0.3, 0), 70)])
        m.anim_scale('Death', 'ring', [(0, 1), (0.6, 1.12), (1.2, 0.2)])
