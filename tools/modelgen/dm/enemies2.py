"""Gegner, Version 2: klare Formen (definierte Anatomie, harte Rüstung/Platten, Felsbrocken-Golems).
Brutmutter, Brutling und Splitterschwarm bleiben aus Version 1."""
from __future__ import annotations

import math

import numpy as np

from . import garments as G
from . import kit
from .anatomy2 import V, align, belt, body2, cape_back, cloth_panel, hair_locks, robe_skirt, shell, tabard
from .anatomy import membrane_mesh, membrane_wing
from .enemies import hold
from .geo import TAU, crystal, curve, cylinder, displace, extrude, icosphere, lathe, merge, rbox, sphere, spike, torus, tube
from .heroes2 import dress, on, sculpt_body
from .model import Model
from .rig import Flyer, Humanoid, Quadruped
from .sdf import R, Sculpt, rot

REG = {}


def enemy(fn):
    REG[fn.__name__] = fn
    return fn


def E(name, title, cat='enemy'):
    return Model(name, cat, title, skinned=True)


# ---------------------------------------------------------------------- Bausteine

def spine_spikes(m, H, n=6, mat='bone', size=0.05, back=0.1):
    h = H.h
    for i in range(n):
        t = i / max(1, n - 1)
        bone = 'chest' if t < 0.5 else 'spine'
        p = H.p('neck') * (1 - t) + H.p('hips') * t + V(0, 0.0, -back * h)
        s = size * h * (1.0 - 0.4 * abs(t - 0.4))
        on(m, bone, spike(s * 0.3, s, 6).rx(-0.9).t(*p), mat)


def rock_chunk(a, b, r, seed, flat=1.0):
    """Kantiger Felsbrocken entlang einer Strecke: grobe Facetten mit flacher Schattierung und Bruchkanten."""
    from .geo import facet
    a, b = np.asarray(a, float), np.asarray(b, float)
    L = float(np.linalg.norm(b - a))
    rng = np.random.default_rng(seed)
    m = icosphere(1.0, 1)
    m.P = m.P * (1 + (rng.random((len(m.P), 1)) - 0.5) * 0.45)
    m.P[:, 0] *= r * 1.05; m.P[:, 2] *= r * flat; m.P[:, 1] = (m.P[:, 1] * 0.55 + 0.5) * L
    # flache Bruchflächen
    for _ in range(3):
        n = rng.normal(size=3); n /= np.linalg.norm(n)
        c = np.array([0, L / 2, 0]) + n * r * 0.7
        d = (m.P - c) @ n
        m.P = m.P - np.maximum(d, 0)[:, None] * n
    return align(facet(m), a, b)


def golem_body(m, H, rock='rock', glow='crystal_cyan', bulk=1.0, seed=1):
    """Golem aus einzelnen Felsbrocken je Knochen, dazwischen leuchtende Kristallgelenke."""
    h = H.h
    P = lambda k: H.p(k)
    on(m, 'chest', rock_chunk(P('spine') + V(0, 0.04 * h, 0), P('neck') + V(0, 0.02 * h, 0), 0.2 * h * bulk, seed, 0.8), rock)
    on(m, 'hips', rock_chunk(P('hips') + V(0, -0.06 * h, 0), P('spine') + V(0, 0.02 * h, 0), 0.15 * h * bulk, seed + 1, 0.8), rock)
    on(m, 'head', rock_chunk(P('head') + V(0, 0.0, 0.02 * h), P('head') + V(0, 0.13 * h, 0.03 * h), 0.075 * h, seed + 2, 0.9), rock)
    on(m, 'head', rbox(0.09 * h, 0.012 * h, 0.03 * h, 0.004 * h).t(*(P('head') + V(0, 0.07 * h, 0.085 * h))), glow)
    for sd, sg in (('L', 1), ('R', -1)):
        on(m, 'upperarm_' + sd, rock_chunk(P('upperarm_' + sd) + V(sg * 0.04 * h, 0.08 * h, 0), P('upperarm_' + sd) + V(sg * 0.06 * h, -0.06 * h, 0), 0.11 * h * bulk, seed + 3 + sg), rock)
        on(m, 'upperarm_' + sd, rock_chunk(P('upperarm_' + sd) + V(sg * 0.01 * h, -0.04 * h, 0), P('forearm_' + sd), 0.065 * h * bulk, seed + 5 + sg), rock)
        on(m, 'forearm_' + sd, rock_chunk(P('forearm_' + sd) + V(0, 0.0, 0), P('hand_' + sd), 0.09 * h * bulk, seed + 7 + sg), rock)
        on(m, 'hand_' + sd, rock_chunk(P('hand_' + sd) + V(0, 0.01 * h, 0), P('hand_' + sd) + V(0, -0.12 * h, 0.02 * h), 0.08 * h * bulk, seed + 9 + sg), rock)
        on(m, 'thigh_' + sd, rock_chunk(P('thigh_' + sd), P('shin_' + sd), 0.09 * h * bulk, seed + 11 + sg), rock)
        on(m, 'shin_' + sd, rock_chunk(P('shin_' + sd), P('foot_' + sd) + V(0, 0.02 * h, 0), 0.085 * h * bulk, seed + 13 + sg), rock)
        on(m, 'foot_' + sd, rock_chunk(P('foot_' + sd) + V(0, -0.04 * h, -0.03 * h), P('foot_' + sd) + V(0, -0.03 * h, 0.12 * h), 0.06 * h * bulk, seed + 15 + sg), rock)
        for j in ('forearm_', 'shin_'):
            on(m, j + sd, crystal(0.045 * h, 0.11 * h, 6, 0.4, 0.4).t(0, -0.04 * h, 0).t(*P(j + sd)), glow)
    on(m, 'chest', kit.crystal_cluster(4, 0.03 * h, 0.16 * h, 0.04 * h, seed=seed).rx(-0.4).t(*(P('chest') + V(0, 0.02 * h, 0.12 * h))), glow)


def quad2(sc, Q, mat, bulk=1.0, snout=1.0, leg=1.0, k=1.0, eye='glow_fire', teeth=True, ears=True, mat2=None):
    """Vierbeiner mit klar abgesetzten Muskeln (Schulter, Keule), Brustkorb, Kopf mit Brauen und Kiefer."""
    P = {kk: V(*v) for kk, v in Q.P.items()}
    L, H = Q.L, Q.H
    K = lambda x: x * L * k
    m2 = mat2 or mat
    sc.ellipsoid(P['chest'] + V(0, -0.02 * H, 0.0), (0.12 * L * bulk, 0.16 * H * bulk + 0.04 * L, 0.15 * L), mat, 'chest', k=K(0.02))
    sc.capsule(P['chest'] + V(0, 0.02 * L, -0.05 * L), P['body'] + V(0, 0.0, -0.12 * L), 0.12 * L * bulk, 0.1 * L * bulk, mat, 'body', k=K(0.025))
    sc.ellipsoid(P['body'] + V(0, -0.03 * L, 0.05 * L), (0.075 * L * bulk, 0.06 * L, 0.12 * L), mat, 'body', k=K(0.02))  # Bauch eingezogen
    for sg in (1, -1):
        sc.ellipsoid(P['chest'] + V(sg * 0.08 * L * bulk, 0.0, 0.05 * L), (0.05 * L * bulk, 0.11 * H + 0.03 * L, 0.07 * L), mat, 'chest', k=K(0.012))  # Schulter
        sc.ellipsoid(P['body'] + V(sg * 0.07 * L * bulk, 0.0, -0.18 * L), (0.055 * L * bulk, 0.12 * H + 0.03 * L, 0.09 * L), mat, 'body', k=K(0.012))  # Keule
    sc.capsule(P['chest'] + V(0, 0.04 * L, 0.06 * L), P['head'] + V(0, -0.01 * L, -0.04 * L), 0.07 * L * bulk, 0.05 * L, mat, 'neck', k=K(0.02))
    hc = P['head']
    sc.ellipsoid(hc, (0.06 * L * bulk, 0.055 * L, 0.07 * L), mat, 'head', k=K(0.012))
    sc.capsule(hc + V(0, 0.01 * L, 0.03 * L), hc + V(0, -0.005 * L, 0.17 * L * snout), 0.04 * L, 0.026 * L, mat, 'head', k=K(0.01))
    sc.capsule(hc + V(-0.035 * L, 0.04 * L, 0.05 * L), hc + V(0.035 * L, 0.04 * L, 0.05 * L), 0.014 * L, 0.014 * L, mat, 'head', k=K(0.006))  # Brauen
    sc.capsule(P['jaw'] + V(0, -0.025 * L, -0.01 * L), P['jaw'] + V(0, -0.03 * L, 0.13 * L * snout), 0.028 * L, 0.018 * L, m2, 'jaw', k=K(0.008))
    for sg in (1, -1):
        sc.ellipsoid(hc + V(sg * 0.036 * L, 0.03 * L, 0.07 * L), 0.011 * L, eye, 'head', k=K(0.001))
        if ears:
            sc.capsule(hc + V(sg * 0.035 * L, 0.04 * L, -0.02 * L), hc + V(sg * 0.06 * L, 0.11 * L, -0.06 * L), 0.016 * L, 0.003 * L, mat, 'head', k=K(0.006))
        if teeth:
            for z in (0.08, 0.11, 0.14):
                a = hc + V(sg * 0.022 * L, -0.03 * L, z * L * snout)
                sc.capsule(a, a + V(0, -0.025 * L if z > 0.13 else -0.012 * L, 0.003 * L), 0.005 * L, 0.001 * L, 'bone', 'head', k=K(0.001))
    for kk in ('FL', 'FR', 'BL', 'BR'):
        a, b, c = P['leg_' + kk], P['shin_' + kk], P['paw_' + kk]
        th = 0.07 * L * bulk * leg
        sc.capsule(a + V(0, 0.03 * L, 0), b, th, th * 0.6, mat, 'leg_' + kk, k=K(0.012))
        sc.capsule(b, c + V(0, 0.02 * L, 0), th * 0.55, th * 0.42, mat, 'shin_' + kk, k=K(0.008))
        sc.ellipsoid(c + V(0, 0.0, 0.02 * L), (th * 0.6, th * 0.32, th * 0.85), m2, 'paw_' + kk, k=K(0.006))
        for dx in (-1, 0, 1):
            sc.capsule(c + V(dx * th * 0.32, 0, th * 0.65), c + V(dx * th * 0.36, -0.015 * L, th * 1.2), th * 0.11, 0.001, 'bone', 'paw_' + kk, k=K(0.002))
    t1, t2 = P['tail1'], P['tail2']
    sc.capsule(t1, t2, 0.035 * L * bulk, 0.022 * L, mat, 'tail1', k=K(0.01))
    sc.capsule(t2, t2 + (t2 - t1) * 1.1 + V(0, -0.04 * L, 0), 0.022 * L, 0.003 * L, mat, 'tail2', k=K(0.006))
    return P


def quad_finish(m, sc, target=9000, voxel=0.006):
    m.sculpt(sc, voxel=voxel, max_dim=260, target=target)


# ====================================================================== Gegner

@enemy
def runner():
    m = E('runner', 'Flinkling')
    H = Humanoid(m, 0.74, w=0.85, legs=0.5, hunch=1.0, arm=1.15)
    sc = Sculpt(detail=0.0006, dfreq=60, seed=701)
    body2(sc, H, 'flesh_grey', build='gaunt', head='ghoul', claws=True, feet='claw', ears=1.2, eye='glow_blood', tail=('flesh_grey', 1.0))
    sculpt_body(m, sc, 7000)
    dress(m, [G.wrap(sc, H, 'cloth_dark', 0.008, [R('box', c=H.p('hips') + V(0, -0.03, 0), h=(0.14, 0.05, 0.12))])])
    on(m, 'hips', cloth_panel([H.p('hips') + V(x, -0.0, 0.085) for x in np.linspace(-0.05, 0.05, 6)], 0.14, 2, 0.003, 8, 1.2, jag=0.5), 'cloth_dark', ('thigh_L', 'thigh_R'))
    spine_spikes(m, H, 6, 'bone', 0.06, 0.08)
    for sd in 'LR':
        on(m, 'forearm_' + sd, torus(0.03, 0.009, 14, 5).t(*(H.p('hand_' + sd) + V(0, 0.03, 0))), 'iron_rust')
    H.anims('claw', speed=1.6)
    return m


@enemy
def brute():
    m = E('brute', 'Steinhäuter')
    H = Humanoid(m, 1.2, w=1.35, legs=0.42, arm=1.18, hunch=0.5)
    sc = Sculpt(detail=0.0008, dfreq=40, seed=703)
    body2(sc, H, 'flesh_grey', build='hulking', head='orc', eye='glow_fire', horns=('bone', 0.6, 0.3))
    sculpt_body(m, sc, 9000, 0.006)
    h = H.h
    plates = [R('ellipsoid', c=H.p('upperarm_' + s) + V(sg * 0.03 * h, 0.03 * h, 0), r=(0.1 * h, 0.09 * h, 0.1 * h)) for s, sg in (('L', 1), ('R', -1))]
    plates += [R('ellipsoid', c=H.p('chest') + V(0, 0.06 * h, -0.1 * h), r=(0.17 * h, 0.12 * h, 0.07 * h))]
    plates += [R('capsule', a=H.p('forearm_' + s), b=H.p('hand_' + s), r1=0.06 * h, r2=0.05 * h) for s in 'LR']
    grooves = [R('box', c=H.p('chest') + V(0, y * h, 0), h=(0.4 * h, 0.003 * h, 0.4 * h)) for y in (0.0, 0.05, 0.1)]
    grooves += [R('box', c=H.p('upperarm_L') + V(0, 0, 0), h=(0.003 * h, 0.2 * h, 0.2 * h)), R('box', c=H.p('upperarm_R'), h=(0.003 * h, 0.2 * h, 0.2 * h))]
    dress(m, [G.wrap(sc, H, 'rock', 0.025, plates, grooves=grooves, gd=0.008),
              G.wrap(sc, H, 'leather', 0.01, [R('box', c=H.p('hips') + V(0, -0.03 * h, 0), h=(0.2 * h, 0.07 * h, 0.16 * h))])])
    on(m, 'hips', cloth_panel([H.p('hips') + V(x * h, -0.02 * h, 0.1 * h) for x in np.linspace(-0.08, 0.08, 7)], 0.2 * h, 3, 0.005, 8, 1.15, jag=0.3), 'leather', ('thigh_L', 'thigh_R'))
    club = merge([cylinder(0.03, 0.55, 8).t(0, -0.12, 0), displace(icosphere(0.09, 2).s(1, 1.5, 1).t(0, 0.45, 0), 0.025, 12, 2)] +
                 [spike(0.015, 0.08, 4).rz(a).t(math.cos(a) * 0.08, 0.45 + math.sin(a) * 0.08, 0) for a in np.linspace(0, TAU, 7)[:-1]])
    hold(m, H, club, 'rock', 'R')
    on(m, 'chest', kit.chain(H.p('chest') + V(0.16 * h, 0.06 * h, 0.1 * h), H.p('hips') + V(-0.16 * h, 0.02 * h, 0.12 * h), 10, 0.03, 0.02), 'iron_rust')
    H.anims('brute', speed=0.8, heavy=True)
    return m


@enemy
def regen():
    m = E('regen', 'Moosling')
    H = Humanoid(m, 0.78, w=1.2, legs=0.4, hunch=0.6, arm=1.15)
    sc = Sculpt(detail=0.0012, dfreq=30, seed=705)
    body2(sc, H, 'bark', build='heavy', head='beast', eye='glow_poison', horns=('bark', 0.9, 0.2), claws=True, nails='bark')
    sculpt_body(m, sc, 8000, 0.0055)
    h = H.h
    moss = [R('ellipsoid', c=H.p('chest') + V(0, 0.07 * h, -0.06 * h), r=(0.17 * h, 0.08 * h, 0.12 * h)),
            R('ellipsoid', c=H.p('upperarm_L') + V(0.02 * h, 0.03 * h, 0), r=(0.06 * h, 0.05 * h, 0.06 * h)),
            R('ellipsoid', c=H.p('upperarm_R') + V(-0.02 * h, 0.03 * h, 0), r=(0.06 * h, 0.05 * h, 0.06 * h)),
            R('ellipsoid', c=H.p('head') + V(0, 0.12 * h, -0.02 * h), r=(0.07 * h, 0.04 * h, 0.07 * h))]
    dress(m, [G.wrap(sc, H, 'flesh_moss', 0.012, moss, detail=0.006)])
    rng = np.random.default_rng(5)
    for i in range(7):
        a = rng.random() * math.pi - math.pi / 2
        c = H.p('chest') + V(math.sin(a) * 0.12 * h, 0.06 * h + rng.random() * 0.06 * h, -0.1 * h)
        cap = lathe([(0.0, 0.04 * h), (0.035 * h, 0.03 * h), (0.04 * h, 0.022 * h), (0.006 * h, 0.022 * h), (0.006 * h, 0.0)], 12, smooth_all=True)
        on(m, 'chest', cap.t(*c), 'chitin_red')
        on(m, 'chest', sphere(0.006 * h, seg=6).t(*(c + V(0.015 * h, 0.04 * h, 0))), 'glow_poison')
    H.anims('claw', speed=0.9)
    return m


@enemy
def flyer():
    m = E('flyer', 'Aschenschwinge')
    F = Flyer(m, span=1.25, body=0.55, alt=0.9)
    P = {k: V(*v) for k, v in F.P.items()}
    sc = Sculpt(detail=0.0006, dfreq=60, seed=707)
    sc.ellipsoid(P['body'] + V(0, 0, 0.02), (0.075, 0.07, 0.15), 'scales_ash', 'body', k=0.01)
    sc.ellipsoid(P['body'] + V(0, -0.01, 0.1), (0.06, 0.065, 0.07), 'scales_ash', 'body', k=0.01)
    sc.capsule(P['body'] + V(0, 0.02, 0.12), P['head'] + V(0, 0, -0.02), 0.04, 0.03, 'scales_ash', 'neck', k=0.01)
    hc = P['head']
    sc.ellipsoid(hc + V(0, 0.01, 0.01), (0.04, 0.035, 0.05), 'scales_ash', 'head', k=0.008)
    sc.capsule(hc + V(0, 0.005, 0.03), hc + V(0, -0.005, 0.1), 0.024, 0.014, 'scales_ash', 'head', k=0.006)
    sc.capsule(P['jaw'] + V(0, -0.012, -0.01), P['jaw'] + V(0, -0.02, 0.06), 0.016, 0.01, 'scales_ash', 'jaw', k=0.004)
    for sg in (1, -1):
        sd = 'L' if sg > 0 else 'R'
        sc.ellipsoid(hc + V(sg * 0.022, 0.022, 0.045), 0.008, 'glow_fire', 'head', k=0.0008)
        sc.chain([hc + V(sg * 0.02, 0.03, -0.01), hc + V(sg * 0.04, 0.06, -0.06), hc + V(sg * 0.045, 0.07, -0.12)], [0.01, 0.007, 0.001], 'bone', 'head', k=0.003)
        for z in (0.06, 0.08):
            sc.capsule(hc + V(sg * 0.012, -0.018, z), hc + V(sg * 0.012, -0.03, z), 0.003, 0.0006, 'bone', 'head', k=0.001)
        sc.capsule(P['leg_' + sd], P['leg_' + sd] + V(0, -0.08, 0.02), 0.016, 0.01, 'scales_ash', 'leg_' + sd, k=0.005)
        for d in (-1, 0, 1):
            sc.capsule(P['leg_' + sd] + V(d * 0.008, -0.08, 0.02), P['leg_' + sd] + V(d * 0.012, -0.1, 0.045), 0.004, 0.001, 'bone', 'leg_' + sd, k=0.002)
    sc.chain([P['tail1'], P['tail2'], P['tail2'] + V(0, -0.02, -0.28)], [0.03, 0.018, 0.003], 'scales_ash', 'tail2', k=0.008)
    for i in range(6):
        z = 0.12 - i * 0.06
        b = 'neck' if z > 0.1 else 'body'
        sc.capsule(V(0, P['body'][1] + 0.06, z), V(0, P['body'][1] + 0.1, z - 0.03), 0.01, 0.001, 'obsidian', b, k=0.004)
    wings = []
    for sd, sg in (('L', 1), ('R', -1)):
        root, elbow, tips = membrane_wing(sc, P['wing_' + sd], 0.62, sg, 'wing_' + sd, 'wingtip_' + sd, 'scales_ash', 'flesh', h=0.75)
        wings.append((sd, membrane_mesh(P['wing_' + sd] + V(0, 0, 0.03), elbow, tips, P['body'] + V(sg * 0.04, 0, -0.14))))
    m.sculpt(sc, voxel=0.0045, max_dim=300, target=7000)
    for sd, w in wings:
        m.add(w, 'leather_black', 'wing_' + sd, blend=('wingtip_' + sd, 'body'))
    F.anims(speed=1.3)
    return m


@enemy
def shield():
    m = E('shield', 'Schildträger')
    H = Humanoid(m, 1.0, w=1.1, legs=0.48)
    sc = Sculpt(detail=0.0005, dfreq=60, seed=709)
    body2(sc, H, 'flesh_grey', build='athletic', head='skull', eye='glow_arcane')
    sculpt_body(m, sc, 7000)
    dress(m, [G.pants(sc, H, 'leather_black'), G.cuirass(sc, H, 'iron_rust'), G.pauldrons(sc, H, 'iron_rust', 0.028, 3), G.greaves(sc, H, 'iron_rust'),
              G.bracers(sc, H, 'iron_rust', 0.012, hands=True), G.boots(sc, H, 'iron_rust', 0.015, 0.06), G.helm(sc, H, 'iron', 0.016, 'closed')])
    on(m, 'hips', tabard(H, 0.4, 0.16), 'cloth_red', ('thigh_L', 'thigh_R'))
    on(m, 'hips', belt(H, 0.135), 'leather_black')
    for sd, sg in (('L', 1), ('R', -1)):
        on(m, 'upperarm_' + sd, spike(0.012, 0.07, 6).rz(-sg * 0.6).t(*(H.p('upperarm_' + sd) + V(sg * 0.05, 0.07, 0))), 'iron')
    face, boss = kit.shield(0.28, 'kite')
    hold(m, H, face, 'iron_rust', 'L', fwd=False, off=(0.045, 0.0, 0.09))
    hold(m, H, boss, 'bone', 'L', fwd=False, off=(0.045, 0.0, 0.11))
    spear = merge([cylinder(0.014, 1.25, 8).t(0, -0.45, 0), lathe([(0.0, 0.0), (0.03, 0.04), (0.02, 0.14), (0.0, 0.22)], 4).t(0, 0.78, 0)])
    hold(m, H, spear, 'iron', 'R')
    on(m, 'chest', cape_back(H, 0.6, 0.3, 0.6), 'cloth_red', ('spine', 'hips'))
    H.anims('melee', speed=0.85)
    return m


@enemy
def phase():
    m = E('phase', 'Phasenwandler')
    H = Humanoid(m, 1.05, w=0.9, legs=0.46, hover=0.2, hunch=0.5, arm=1.15)
    sc = Sculpt(detail=0.0005, dfreq=60, seed=711)
    body2(sc, H, 'flesh_grey', build='gaunt', head='skull', claws=True, eye='glow_arcane', nails='bone')
    sculpt_body(m, sc, 6000)
    dress(m, [G.tunic(sc, H, 'cloth_violet', 0.01, hem=0.05, sleeves=True), G.sleeves(sc, H, 'cloth_violet', 0.012), G.hood(sc, H, 'cloth_violet', 0.026)])
    h = H.h
    ring = [H.p('hips') + V(math.cos(a) * 0.14 * h, 0.03 * h, math.sin(a) * 0.11 * h) for a in np.linspace(0, TAU, 25)]
    on(m, 'hips', cloth_panel(ring, 0.42 * h, 10, 0.012 * h, 12, 1.5, jag=0.6), 'cloth_violet', ('thigh_L', 'thigh_R'))
    for sd in 'LR':
        a, b = H.p('forearm_' + sd), H.p('hand_' + sd)
        on(m, 'forearm_' + sd, cloth_panel([a + V(math.cos(t) * 0.05 * h, 0, math.sin(t) * 0.05 * h) for t in np.linspace(0, TAU, 13)], 0.16 * h, 4, 0.005 * h, 6, 1.4, jag=0.6), 'cloth_violet', ('hand_' + sd,))
    on(m, 'hips', merge([torus((0.2 + 0.06 * k) * h, 0.005 * h, 36, 4).t(*(V(0, 0.1 * h + 0.14 * k * h, 0))) for k in range(3)]), 'glow_arcane')
    H.anims('caster', fly=True)
    return m


@enemy
def saboteur():
    m = E('saboteur', 'Funkenfresser')
    H = Humanoid(m, 0.7, w=1.0, legs=0.44, hunch=0.7, arm=1.1)
    sc = Sculpt(detail=0.0006, dfreq=60, seed=713)
    body2(sc, H, 'flesh_moss', build='lean', head='orc', ears=1.9, claws=True, feet='claw', eye='glow_fire', head_scale=1.2)
    sculpt_body(m, sc, 7000)
    h = H.h
    harness = [R('capsule', a=H.p('upperarm_L') + V(-0.02, 0.02, 0), b=H.p('hips') + V(-0.1 * h, 0, 0), r1=0.02, r2=0.02),
               R('capsule', a=H.p('upperarm_R') + V(0.02, 0.02, 0), b=H.p('hips') + V(0.1 * h, 0, 0), r1=0.02, r2=0.02)]
    dress(m, [G.pants(sc, H, 'cloth_dark', to=0.4), G.wrap(sc, H, 'leather', 0.009, harness), G.gloves(sc, H, 'leather_black')])
    on(m, 'hips', belt(H, 0.125 * h), 'leather')
    for sg in (1, -1):
        on(m, 'head', torus(0.017 * h, 0.006 * h, 14, 4).rx(math.pi / 2).t(*(H.p('head') + V(sg * 0.025 * h, 0.088 * h, 0.075 * h))), 'iron')
        on(m, 'head', sphere(0.014 * h, 0.014 * h, 0.005 * h, 10).t(*(H.p('head') + V(sg * 0.025 * h, 0.088 * h, 0.078 * h))), 'glow_storm')
    pack = merge([cylinder(0.035, 0.17, 10).t(x, 0, 0) for x in (-0.05, 0.0, 0.05)] + [rbox(0.17, 0.03, 0.06, 0.01).t(0, 0.07, 0)])
    on(m, 'chest', pack.t(*(H.p('chest') + V(0, -0.1, -0.13))), 'iron_rust')
    on(m, 'chest', merge([sphere(0.022, seg=8).t(x, 0.0, 0) for x in (-0.05, 0.0, 0.05)]).t(*(H.p('chest') + V(0, 0.08, -0.13))), 'glow_storm')
    coil = tube(np.array([(math.cos(t * 20) * 0.018, t * 0.22, math.sin(t * 20) * 0.018) for t in np.linspace(0, 1, 60)]), 0.004, 4)
    on(m, 'chest', coil.t(*(H.p('chest') + V(0.07, -0.05, -0.15))), 'gold')
    hold(m, H, cylinder(0.012, 0.22, 6).t(0, -0.05, 0), 'wood', 'R')
    hold(m, H, sphere(0.03, seg=8).t(0, 0.17, 0), 'glow_storm', 'R')
    H.anims('melee', speed=1.4)
    return m


@enemy
def scout():
    m = E('scout', 'Pfadspäher')
    H = Humanoid(m, 0.95, w=0.85, legs=0.52)
    sc = Sculpt(detail=0.0005, dfreq=60, seed=715)
    body2(sc, H, 'skin_pale', build='lean', eye='glow_blood')
    sculpt_body(m, sc, 6500)
    dress(m, [G.pants(sc, H, 'leather_black'), G.tunic(sc, H, 'leather_black', hem=0.14, sleeves=True), G.sleeves(sc, H, 'leather_black'),
              G.bracers(sc, H, 'leather'), G.gloves(sc, H, 'leather'), G.boots(sc, H, 'leather', height=0.16), G.hood(sc, H, 'cloth_red', 0.02),
              G.wrap(sc, H, 'bone', 0.01, [R('box', c=H.p('head') + V(0, 0.05, 0.06), h=(0.05, 0.035, 0.04))], grooves=[R('box', c=H.p('head') + V(0, 0.07, 0.1), h=(0.04, 0.003, 0.04))])])
    on(m, 'chest', cape_back(H, 0.6, 0.3, 0.6), 'cloth_red', ('spine', 'hips'))
    on(m, 'hips', belt(H, 0.12 * H.h), 'leather')
    limbs, string = kit.bow(0.75)
    hold(m, H, limbs, 'wood_black', 'L', fwd=False)
    hold(m, H, string, 'leather_black', 'L', fwd=False)
    q = merge([cylinder(0.032, 0.3, 10)] + [cylinder(0.004, 0.12, 4).t(dx, 0.3, 0) for dx in (-0.014, 0, 0.014)])
    on(m, 'chest', q.rz(0.45).t(*(H.p('chest') + V(0.08, -0.12, -0.12))), 'leather')
    H.anims('ranged', speed=1.3)
    return m


@enemy
def golem():
    m = E('golem', 'Höhlengolem')
    H = Humanoid(m, 1.3, w=1.5, legs=0.38, arm=1.25, hunch=0.6)
    golem_body(m, H, 'rock', 'crystal_cyan', 1.0, seed=21)
    H.anims('brute', speed=0.7, heavy=True)
    return m


@enemy
def golemling():
    m = E('golemling', 'Golemsplitter')
    H = Humanoid(m, 0.58, w=1.4, legs=0.38, arm=1.2, hunch=0.5)
    golem_body(m, H, 'rock', 'crystal_cyan', 1.0, seed=41)
    H.anims('brute', speed=1.0, heavy=True)
    return m


@enemy
def stalker():
    m = E('stalker', 'Schattenschleicher')
    Q = Quadruped(m, length=1.05, height=0.45, neck=0.2, tail=0.55)
    sc = Sculpt(detail=0.0008, dfreq=50, seed=717)
    P = quad2(sc, Q, 'fur', bulk=1.0, snout=1.15, leg=1.0, eye='glow_arcane', mat2='fur')
    for i in range(8):  # Rückenstacheln
        z = 0.34 - i * 0.08
        b = 'chest' if z > 0.1 else 'body'
        y = 0.62 - abs(z - 0.15) * 0.15
        sc.capsule(V(0, y - 0.03, z), V(0, y + 0.06 - i * 0.004, z - 0.06), 0.012, 0.001, 'obsidian', b, k=0.004)
    quad_finish(m, sc, 9000)
    Q.anims(speed=1.4)
    return m


@enemy
def siege():
    m = E('siege', 'Belagerungsbestie')
    Q = Quadruped(m, length=1.5, height=0.62, neck=0.15, tail=0.3)
    sc = Sculpt(detail=0.0012, dfreq=30, seed=719)
    P = quad2(sc, Q, 'flesh_grey', bulk=1.5, snout=0.75, leg=1.35, eye='glow_fire', ears=False, mat2='obsidian')
    hc = P['head']
    for sg in (1, -1):
        sc.chain([hc + V(sg * 0.07, -0.02, 0.12), hc + V(sg * 0.15, 0.04, 0.2), hc + V(sg * 0.16, 0.16, 0.24)], [0.035, 0.022, 0.003], 'bone', 'head', k=0.006)
    sc.capsule(hc + V(0, 0.03, 0.2), hc + V(0, 0.15, 0.3), 0.04, 0.004, 'bone', 'head', k=0.008)
    quad_finish(m, sc, 10000, 0.008)
    for z, b in ((0.32, 'chest'), (0.12, 'chest'), (-0.1, 'body'), (-0.32, 'body')):
        plate = shell(0.27, 0.22, 0.14, 0.25, 1.0, 16, 6, 0.012)
        on(m, b, plate.t(0, 0.8, z), 'iron_rust')
        on(m, b, merge([spike(0.02, 0.09, 6).t(x, 1.0, z) for x in (-0.12, 0.12)]), 'iron')
    tower = merge([rbox(0.36, 0.26, 0.36, 0.02).t(0, 1.12, -0.05)] + [rbox(0.08, 0.08, 0.06, 0.01).t(x, 1.29, z - 0.05) for x in (-0.14, 0.14) for z in (-0.14, 0.14)])
    on(m, 'body', tower, 'wood_black')
    on(m, 'body', kit.skull(0.08).t(0, 1.22, 0.14), 'bone')
    cl, _ = kit.banner(0.16, 0.32, pole=False)
    on(m, 'body', cl.ry(math.pi / 2).t(0.2, 1.22, -0.05), 'cloth_red')
    on(m, 'chest', kit.chain((0.25, 0.58, 0.45), (-0.25, 0.58, 0.45), 10, 0.08, 0.02), 'iron_rust')
    Q.anims(speed=0.6)
    return m


@enemy
def shaman():
    m = E('shaman', 'Heilerschamane')
    H = Humanoid(m, 0.95, w=0.95, legs=0.46, hunch=0.85)
    sc = Sculpt(detail=0.0005, dfreq=60, seed=721)
    body2(sc, H, 'flesh_grey', build='gaunt', head='ghoul', claws=True, ears=0.9, eye='glow_poison')
    sculpt_body(m, sc, 6500)
    dress(m, [G.tunic(sc, H, 'cloth_green', 0.01, hem=0.05), G.hood(sc, H, 'cloth_green', 0.024, shoulders=True),
              G.wrap(sc, H, 'bone', 0.012, [R('box', c=H.p('head') + V(0, 0.06, 0.06), h=(0.055, 0.055, 0.04))], grooves=[R('box', c=H.p('head') + V(0, 0.07, 0.1), h=(0.04, 0.004, 0.04))])])
    on(m, 'hips', robe_skirt(H, 0.95, 0.12, 1.6, 12), 'cloth_green', ('thigh_L', 'thigh_R'))
    beads = merge([sphere(0.013, seg=8).t(math.cos(a) * 0.12, -math.sin(a) * 0.12 + 0.02, 0.1 + 0.02 * math.sin(a)) for a in np.linspace(0.2, math.pi - 0.2, 9)])
    on(m, 'chest', beads.t(*(H.p('chest') + V(0, 0.04, 0))), 'bone')
    for sg in (1, -1):
        on(m, 'head', kit.horn(0.12, 0.018, 1.0).rz(-sg * 0.7).t(*(H.p('head') + V(sg * 0.04, 0.12, -0.01))), 'bone')
    sh, claw, gem = kit.staff(1.05)
    hold(m, H, sh, 'wood_black', 'R', fwd=False); hold(m, H, claw, 'bone', 'R', fwd=False); hold(m, H, gem, 'crystal_green', 'R', fwd=False)
    hold(m, H, kit.skull(0.07).t(0, 0.64, 0.02), 'bone', 'R', fwd=False)
    H.anims('caster', speed=0.9)
    return m


@enemy
def tunneler():
    m = E('tunneler', 'Tunnelgräber')
    Q = Quadruped(m, length=0.95, height=0.32, neck=0.12, tail=0.2, low=True)
    sc = Sculpt(detail=0.0008, dfreq=45, seed=723)
    P = quad2(sc, Q, 'fur', bulk=1.6, snout=0.9, leg=1.2, eye='glow_fire', ears=False, mat2='chitin_red')
    for sd in ('FL', 'FR'):
        c = P['paw_' + sd]
        sg = 1 if sd[1] == 'L' else -1
        sc.ellipsoid(c + V(sg * 0.02, 0.025, 0.04), (0.06, 0.035, 0.06), 'chitin_red', 'paw_' + sd, k=0.006)
        for kk in range(3):
            sc.capsule(c + V(sg * 0.02 + (kk - 1) * 0.032, 0.02, 0.08), c + V(sg * 0.02 + (kk - 1) * 0.036, -0.012, 0.19), 0.014, 0.001, 'bone', 'paw_' + sd, k=0.003)
    hc = P['head']
    for i in range(8):
        a = TAU * i / 8
        sc.capsule(hc + V(math.cos(a) * 0.022, math.sin(a) * 0.022, 0.15), hc + V(math.cos(a) * 0.035, math.sin(a) * 0.035, 0.2), 0.006, 0.0015, 'flesh', 'head', k=0.002)
    quad_finish(m, sc, 8000, 0.0055)
    for z, b in ((0.18, 'chest'), (0.0, 'body'), (-0.18, 'body')):
        on(m, b, shell(0.19, 0.14, 0.11, 0.3, 1.0, 14, 5, 0.01).t(0, 0.36, z), 'chitin_red')
    Q.anims(speed=1.0)
    return m


@enemy
def griffin():
    m = E('griffin', 'Sturmgreif')
    F = Flyer(m, span=1.8, body=0.75, alt=1.05)
    P = {k: V(*v) for k, v in F.P.items()}
    sc = Sculpt(detail=0.0006, dfreq=50, seed=725)
    sc.ellipsoid(P['body'] + V(0, 0, -0.08), (0.12, 0.11, 0.22), 'fur', 'body', k=0.012)
    sc.ellipsoid(P['body'] + V(0, 0.02, 0.13), (0.12, 0.12, 0.12), 'fur_grey', 'body', k=0.012)
    sc.capsule(P['body'] + V(0, 0.06, 0.16), P['head'] + V(0, 0, -0.02), 0.07, 0.05, 'fur_grey', 'neck', k=0.012)
    hc = P['head']
    sc.ellipsoid(hc + V(0, 0.02, 0.01), (0.055, 0.055, 0.07), 'fur_grey', 'head', k=0.008)
    sc.capsule(hc + V(-0.03, 0.05, 0.04), hc + V(0.03, 0.05, 0.04), 0.014, 0.014, 'fur_grey', 'head', k=0.004)
    sc.chain([hc + V(0, 0.025, 0.06), hc + V(0, 0.01, 0.12), hc + V(0, -0.03, 0.14)], [0.03, 0.018, 0.003], 'gold', 'head', k=0.004)  # Schnabel
    for sg in (1, -1):
        sd = 'L' if sg > 0 else 'R'
        sc.ellipsoid(hc + V(sg * 0.034, 0.04, 0.05), 0.01, 'glow_storm', 'head', k=0.0008)
        sc.capsule(hc + V(sg * 0.03, 0.06, -0.03), hc + V(sg * 0.05, 0.1, -0.09), 0.012, 0.002, 'fur_grey', 'head', k=0.004)
        for zz in (0.14, -0.2):
            top = P['body'] + V(sg * 0.07, -0.05, zz)
            sc.capsule(top, top + V(0, -0.12, 0.03 if zz > 0 else -0.03), 0.03, 0.02, 'fur', 'leg_' + sd, k=0.008)
            sc.capsule(top + V(0, -0.12, 0.03 if zz > 0 else -0.03), top + V(0, -0.2, 0.06), 0.018, 0.014, 'gold' if zz > 0 else 'fur', 'leg_' + sd, k=0.005)
        sc.capsule(P['wing_' + sd], P['wingtip_' + sd], 0.04, 0.02, 'fur_grey', 'wing_' + sd, k=0.012)
    sc.chain([P['tail1'], P['tail2'], P['tail2'] + V(0, -0.05, -0.3)], [0.03, 0.02, 0.012], 'fur', 'tail2', k=0.006)
    m.sculpt(sc, voxel=0.0055, max_dim=300, target=8000)
    # Federn als harte, flache Formen entlang der Flügel
    for sd, sg in (('L', 1), ('R', -1)):
        for k in range(9):
            t = k / 8
            base = V(*m.nodes['wing_' + sd].pos) * (1 - t) + V(*m.nodes['wingtip_' + sd].pos) * t
            Lf = 0.22 + 0.16 * t
            feather = extrude([(0, 0), (0.035, 0.03), (0.03, Lf * 0.8), (0.0, Lf), (-0.02, Lf * 0.7), (-0.025, 0.04)], 0.006, bevel=0.3)
            feather.rx(math.pi / 2).ry(math.pi + sg * (0.15 + 0.35 * t)).t(*(base + V(0, -0.01, -0.02)))
            on(m, 'wingtip_' + sd if t > 0.45 else 'wing_' + sd, feather, 'fur' if k % 2 else 'fur_grey')
    F.anims(speed=1.0)
    return m
