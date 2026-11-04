"""Gegnermodelle (Dark Fantasy) mit Skelett und den Clips Idle, Walk, Attack, Death.
Größe: normale Gegner 0,5–1,2 m, Blickrichtung +Z."""
from __future__ import annotations

import math

import numpy as np

from . import kit
from .anatomy import V, head_shape, humanoid_body, membrane_mesh, membrane_wing
from .geo import TAU, crystal, curve, cylinder, displace, extrude, icosphere, merge, rbox, sphere, spike, torus, tube
from .model import Model
from .rig import Chain, Crawler, Flyer, Floater, Humanoid, Quadruped
from .sdf import Sculpt, rot

REG = {}


def enemy(fn):
    REG[fn.__name__] = fn
    return fn


def E(name, title, cat='enemy'):
    return Model(name, cat, title, skinned=True)


def finish(m, sc, target=7000, voxel=0.01, max_dim=150):
    m.sculpt(sc, voxel=voxel, max_dim=max_dim, target=target)


def hold(m, H, mesh, mat, side='R', fwd=True, off=(0, 0, 0)):
    """Gegenstand in die Hand legen (Gegenstand zeigt in +Y; fwd dreht nach vorn)."""
    if fwd:
        mesh.rx(math.pi / 2)
    p = H.p('hand_' + side) + V(0, -0.035 * H.h, 0.012 * H.h) + V(*off)
    m.add(mesh.t(*p), mat, 'hand_' + side)


def on(m, H, bone, mesh, mat, at=(0, 0, 0)):
    m.add(mesh.t(*(H.p(bone) + V(*at))), mat, bone)


# ---------------------------------------------------------------------- Vierbeiner-Körper

def quad_body(sc, Q, mat, bulk=1.0, head='beast', snout=1.0, leg=1.0, tail=True, mat2=None):
    P = {k: V(*v) for k, v in Q.P.items()}
    L, H = Q.L, Q.H
    m2 = mat2 or mat
    sc.capsule(P['body'] + V(0, 0, -0.1 * L), P['chest'], 0.17 * H * bulk + 0.06 * L, 0.19 * H * bulk + 0.07 * L, mat, 'body', k=0.06)
    sc.ellipsoid(P['chest'] + V(0, -0.04 * H, 0.02 * L), (0.14 * L * bulk, 0.2 * H * bulk + 0.03, 0.16 * L), mat, 'chest', k=0.05)
    sc.capsule(P['chest'] + V(0, 0.02, 0.05 * L), P['head'] + V(0, -0.02, -0.04 * L), 0.1 * H * bulk + 0.03, 0.07 * H + 0.02, mat, 'neck', k=0.05)
    hc = P['head']
    sc.ellipsoid(hc, (0.09 * L * bulk, 0.08 * L, 0.1 * L), mat, 'head', k=0.03)
    sc.capsule(hc + V(0, 0.0, 0.03 * L), hc + V(0, -0.02 * L, 0.18 * L * snout), 0.055 * L, 0.04 * L, mat, 'head', k=0.03)
    sc.capsule(P['jaw'] + V(0, -0.02 * L, 0.0), P['jaw'] + V(0, -0.03 * L, 0.14 * L * snout), 0.04 * L, 0.028 * L, m2, 'jaw', k=0.02)
    for sg in (1, -1):
        sc.ellipsoid(hc + V(sg * 0.055 * L, 0.03 * L, 0.06 * L), 0.014 * L, 'glow_fire', 'head', k=0.002)
        for z in (0.1, 0.14):
            sc.capsule(hc + V(sg * 0.03 * L, -0.04 * L, z * L * snout), hc + V(sg * 0.03 * L, -0.07 * L, z * L * snout + 0.01), 0.008 * L, 0.001, 'bone', 'head', k=0.003)
    for k in ('FL', 'FR', 'BL', 'BR'):
        a, b, c = P['leg_' + k], P['shin_' + k], P['paw_' + k]
        th = (0.11 * H * bulk + 0.02) * leg
        sc.capsule(a + V(0, 0.04, 0), b, th, th * 0.65, mat, 'leg_' + k, k=0.05)
        if k[0] == 'B':
            sc.ellipsoid(a * 0.6 + b * 0.4 + V(0, 0.02, -0.02), (th * 0.9, th * 1.5, th * 1.2), mat, 'leg_' + k, k=0.04)
        sc.capsule(b, c + V(0, 0.03, 0), th * 0.6, th * 0.45, mat, 'shin_' + k, k=0.03)
        sc.ellipsoid(c + V(0, 0.0, 0.025), (th * 0.6, th * 0.35, th * 0.8), m2, 'paw_' + k, k=0.02)
        for dx in (-1, 0, 1):
            sc.capsule(c + V(dx * th * 0.35, -0.0, th * 0.6), c + V(dx * th * 0.4, -0.02, th * 1.1), th * 0.12, 0.002, 'bone', 'paw_' + k, k=0.004)
    if tail:
        t1, t2 = P['tail1'], P['tail2']
        sc.capsule(t1, t2, 0.05 * H * bulk + 0.01, 0.03 * H, mat, 'tail1', k=0.03)
        sc.capsule(t2, t2 + (t2 - t1) * 1.1 + V(0, -0.04, 0), 0.03 * H, 0.004, mat, 'tail2', k=0.02)
    return P


def crawler_body(sc, C, mat='chitin', abd='flesh', leg_mat=None, size=1.0, sac=0.0):
    P = {k: V(*v) for k, v in C.P.items()}
    s = C.size
    lm = leg_mat or mat
    sc.ellipsoid(P['body'], (0.2 * s, 0.12 * s, 0.24 * s), mat, 'body', k=0.04 * s)
    sc.ellipsoid(P['head'], (0.12 * s, 0.09 * s, 0.11 * s), mat, 'head', k=0.04 * s)
    sc.ellipsoid(P['abdomen'] + V(0, 0.03 * s, -0.12 * s), (0.27 * s * (1 + sac), 0.22 * s * (1 + sac), 0.32 * s * (1 + sac * 0.6)), abd, 'abdomen', k=0.06 * s)
    for sg in (1, -1):
        sc.ellipsoid(P['head'] + V(sg * 0.04 * s, 0.04 * s, 0.09 * s), 0.025 * s, 'crystal_red', 'head', k=0.003)
        sc.ellipsoid(P['head'] + V(sg * 0.075 * s, 0.05 * s, 0.06 * s), 0.016 * s, 'crystal_red', 'head', k=0.003)
        sc.chain([P['jaw'] + V(sg * 0.04 * s, 0.0, -0.03 * s), P['jaw'] + V(sg * 0.06 * s, -0.04 * s, 0.05 * s), P['jaw'] + V(sg * 0.02 * s, -0.09 * s, 0.08 * s)],
                 [0.025 * s, 0.018 * s, 0.003], lm, 'jaw', k=0.01 * s)
    for name, v in P.items():
        if name.startswith('leg'):
            k = name[3:]
            hip, knee, foot = v, P['knee' + k], P['foot' + k]
            sc.capsule(hip, knee, 0.035 * s, 0.028 * s, lm, 'leg' + k, k=0.02 * s)
            sc.capsule(knee, foot + V(0, 0.01, 0), 0.026 * s, 0.006, lm, 'knee' + k, k=0.015 * s)
    return P


# ====================================================================== Gegner

@enemy
def runner():
    m = E('runner', 'Flinkling')
    H = Humanoid(m, 0.72, w=0.85, legs=0.5, hunch=0.9)
    sc = Sculpt(detail=0.002, dfreq=35, seed=101)
    humanoid_body(sc, H, skin='flesh_grey', gaunt=0.9, muscle=0.3, head='ghoul', hands='claw', feet='claw', ears=1.4, tail=('flesh_grey', 1.2))
    p = H.p('hips')
    sc.ellipsoid(p + V(0, -0.02, 0.0), (0.1, 0.06, 0.08), 'leather_black', 'hips', k=0.01)
    finish(m, sc, 7000, 0.007)
    on(m, H, 'upperarm_L', kit.pauldron(0.045, 2), 'iron_rust', (0.01, 0.02, 0))
    H.anims('claw', speed=1.6)
    return m


@enemy
def brute():
    m = E('brute', 'Steinhäuter')
    H = Humanoid(m, 1.15, w=1.35, legs=0.42, arm=1.15, hunch=0.5)
    sc = Sculpt(detail=0.004, dfreq=14, seed=103)
    humanoid_body(sc, H, skin='rock', bulk=1.35, muscle=1.0, belly=0.6, head='brute', hands='hand', feet='hoof', neck=1.6, head_size=0.95, horns=('bone', 0.8, 0.4))
    for b, off, r in (('upperarm_L', (0.03, 0.05, 0), 0.09), ('upperarm_R', (-0.03, 0.05, 0), 0.09), ('chest', (0, 0.07, -0.08), 0.12), ('spine', (0, 0.02, -0.1), 0.1)):
        sc.ellipsoid(H.p(b) + V(*off), (r, r * 0.7, r), 'rock_red', b, k=0.03, noise=0.015)
    sc.ellipsoid(H.p('hips') + V(0, -0.03, 0), (0.17, 0.08, 0.13), 'leather', 'hips', k=0.02)
    finish(m, sc, 9000, 0.01)
    club = merge([cylinder(0.025, 0.5, 8).t(0, -0.1, 0), displace(icosphere(0.08, 2).s(1, 1.4, 1).t(0, 0.42, 0), 0.02, 12, 2)])
    hold(m, H, club, 'rock', 'R')
    m.add(kit.chain(H.p('chest') + V(0.15, 0.05, 0.1), H.p('hips') + V(-0.15, 0, 0.12), 8, 0.04, 0.02), 'iron_rust', 'chest')
    H.anims('brute', speed=0.8, heavy=True)
    return m


@enemy
def regen():
    m = E('regen', 'Moosling')
    H = Humanoid(m, 0.72, w=1.2, legs=0.38, hunch=0.6)
    sc = Sculpt(detail=0.004, dfreq=18, seed=105)
    humanoid_body(sc, H, skin='flesh_moss', bulk=1.25, muscle=0.2, belly=1.0, head='beast', hands='stump', feet='boot', head_size=1.15, skin2='bark')
    rng = np.random.default_rng(5)
    caps = []
    for i in range(6):
        a = rng.random() * math.pi - math.pi / 2
        c = H.p('chest') + V(math.sin(a) * 0.1, 0.02 + rng.random() * 0.08, -0.07 - rng.random() * 0.03)
        sc.capsule(c, c + V(0, 0.04, -0.03), 0.01, 0.008, 'bone', 'chest', k=0.01)
        sc.ellipsoid(c + V(0, 0.05, -0.035), (0.035, 0.015, 0.035), 'chitin_red', 'chest', k=0.008)
        caps.append(c + V(0, 0.058, -0.035))
    finish(m, sc, 7000, 0.007)
    m.add(merge([sphere(0.012, seg=6).t(*c) for c in caps]), 'glow_poison', 'chest')
    H.anims('claw', speed=0.9)
    return m


@enemy
def flyer():
    m = E('flyer', 'Aschenschwinge')
    F = Flyer(m, span=1.2, body=0.5, alt=0.9)
    P = {k: V(*v) for k, v in F.P.items()}
    sc = Sculpt(detail=0.002, dfreq=30, seed=107)
    wings = []
    sc.ellipsoid(P['body'], (0.09, 0.08, 0.17), 'scales_ash', 'body', k=0.03)
    sc.capsule(P['body'] + V(0, 0.02, 0.1), P['head'], 0.06, 0.04, 'scales_ash', 'neck', k=0.03)
    sc.ellipsoid(P['head'] + V(0, 0.01, 0.02), (0.045, 0.04, 0.06), 'scales_ash', 'head', k=0.02)
    sc.capsule(P['jaw'], P['jaw'] + V(0, -0.015, 0.07), 0.025, 0.012, 'scales_ash', 'jaw', k=0.01)
    for sg in (1, -1):
        sc.ellipsoid(P['head'] + V(sg * 0.03, 0.03, 0.05), 0.01, 'glow_fire', 'head', k=0.001)
        sc.chain([P['head'] + V(sg * 0.025, 0.04, -0.01), P['head'] + V(sg * 0.04, 0.08, -0.06), P['head'] + V(sg * 0.04, 0.1, -0.12)], [0.012, 0.008, 0.001], 'bone', 'head', k=0.006)
        sd = 'L' if sg > 0 else 'R'
        root, elbow, tips = membrane_wing(sc, P['wing_' + sd], 0.6, sg, 'wing_' + sd, 'wingtip_' + sd, 'scales_ash', 'flesh', h=0.9)
        wings.append((sd, membrane_mesh(P['wing_' + sd] + V(0, 0, 0.03), elbow, tips, P['body'] + V(sg * 0.05, 0, -0.15))))
        sc.capsule(P['leg_' + sd], P['leg_' + sd] + V(0, -0.1, 0.03), 0.02, 0.012, 'scales_ash', 'leg_' + sd, k=0.01)
    sc.chain([P['tail1'], P['tail2'], P['tail2'] + V(0, -0.02, -0.25)], [0.04, 0.025, 0.004], 'scales_ash', 'tail2', k=0.02)
    finish(m, sc, 7000, 0.008)
    for sd, w in wings:
        m.add(w, 'leather_black', 'wing_' + sd, blend=('wingtip_' + sd, 'body'))
    F.anims(speed=1.3)
    return m


@enemy
def shield():
    m = E('shield', 'Schildträger')
    H = Humanoid(m, 1.0, w=1.15, legs=0.47)
    sc = Sculpt(detail=0.0015, dfreq=40, seed=109)
    humanoid_body(sc, H, skin='flesh_grey', bulk=1.15, muscle=0.6, head='skull', hands='hand', feet='boot')
    # Rüstung als Bildhauer-Schalen
    sc.ellipsoid(H.p('chest') + V(0, 0.01, 0.01), (0.19, 0.15, 0.13), 'iron', 'chest', k=0.02)
    sc.ellipsoid(H.p('spine') + V(0, -0.02, 0.0), (0.15, 0.12, 0.11), 'leather_black', 'spine', k=0.02)
    for sd in 'LR':
        sc.capsule(H.p('shin_' + sd) + V(0, 0.03, 0.01), H.p('foot_' + sd) + V(0, 0.05, 0.02), 0.05, 0.042, 'iron', 'shin_' + sd, k=0.01)
        sc.capsule(H.p('forearm_' + sd), H.p('hand_' + sd) + V(0, 0.03, 0), 0.042, 0.035, 'iron', 'forearm_' + sd, k=0.01)
    sc.ellipsoid(H.p('head') + V(0, 0.085, -0.005), (0.072, 0.065, 0.08), 'iron', 'head', k=0.01)  # Helm
    sc.box(H.p('head') + V(0, 0.04, 0.03), (0.075, 0.01, 0.06), 'iron', 'head', k=0.01, sub=True)
    finish(m, sc, 9000, 0.008)
    for sd, sg in (('L', 1), ('R', -1)):
        on(m, H, 'upperarm_' + sd, kit.pauldron(0.075, 3), 'iron', (sg * 0.02, 0.03, 0))
    face, boss = kit.shield(0.26, 'kite')
    sh = merge([face]).ry(math.pi / 2 * 0).t(0, 0, 0)
    hold(m, H, face.ry(-math.pi / 2).ry(math.pi / 2), 'iron_rust', 'L', fwd=False, off=(0.04, 0.0, 0.08))
    hold(m, H, boss, 'bone', 'L', fwd=False, off=(0.04, 0.0, 0.1))
    spear = merge([cylinder(0.014, 1.2, 8).t(0, -0.45, 0), spike(0.03, 0.18, 4).t(0, 0.75, 0)])
    hold(m, H, spear, 'iron', 'R')
    cloth, _ = kit.banner(0.18, 0.3, pole=False)
    m.add(cloth.t(*(H.p('hips') + V(0, -0.02, 0.1))), 'cloth_red', 'hips')
    H.anims('melee', speed=0.85)
    return m


@enemy
def phase():
    m = E('phase', 'Phasenwandler')
    H = Humanoid(m, 1.05, w=0.9, legs=0.45, hover=0.18, hunch=0.5)
    sc = Sculpt(detail=0.003, dfreq=22, seed=111)
    c, hp = H.p('chest'), H.p('hips')
    sc.ellipsoid(c + V(0, 0.0, 0.0), (0.16, 0.14, 0.11), 'cloth_violet', 'chest', k=0.04)
    sc.capsule(c + V(0, 0.0, -0.01), hp + V(0, -0.05, 0), 0.13, 0.16, 'cloth_violet', 'spine', k=0.05)
    rng = np.random.default_rng(11)
    for i in range(9):  # zerfetzte Mantelbahnen
        a = TAU * i / 9
        top = hp + V(math.cos(a) * 0.1, -0.02, math.sin(a) * 0.08)
        bot = V(math.cos(a) * 0.2, 0.12 + rng.random() * 0.12, math.sin(a) * 0.16 - 0.03)
        sc.capsule(top, bot, 0.07, 0.01, 'cloth_violet', 'hips', k=0.05)
    for sd, sg in (('L', 1), ('R', -1)):
        sc.capsule(H.p('upperarm_' + sd), H.p('forearm_' + sd), 0.05, 0.055, 'cloth_violet', 'upperarm_' + sd, k=0.03)
        sc.capsule(H.p('forearm_' + sd), H.p('hand_' + sd) + V(0, 0.03, 0), 0.05, 0.07, 'cloth_violet', 'forearm_' + sd, k=0.02)
        from .anatomy import hand
        hand(sc, H.p('hand_' + sd), sg, H.h * 1.2, 'bone', 'hand_' + sd, claw=True)
    hd = H.p('head')
    sc.ellipsoid(hd + V(0, 0.085, -0.025), (0.1, 0.11, 0.1), 'cloth_violet', 'head', k=0.03)
    sc.ellipsoid(hd + V(0, 0.08, 0.07), (0.085, 0.09, 0.08), 'cloth_violet', 'head', sub=True, k=0.02)
    head_shape(sc, hd + V(0, 0.0, 0.01), H.h * 0.95, 'bone', kind='skull')
    finish(m, sc, 7500, 0.008)
    m.add(merge([torus(0.18 + 0.05 * k, 0.006, 30, 4).t(0, 0.25 + 0.15 * k, 0) for k in range(3)]), 'glow_arcane', 'hips')
    H.anims('caster', fly=True)
    return m


@enemy
def saboteur():
    m = E('saboteur', 'Funkenfresser')
    H = Humanoid(m, 0.68, w=1.0, legs=0.44, hunch=0.7)
    sc = Sculpt(detail=0.002, dfreq=35, seed=113)
    humanoid_body(sc, H, skin='flesh_moss', bulk=1.0, gaunt=0.4, muscle=0.3, head='human', hands='claw', feet='claw', ears=1.8, head_size=1.25)
    sc.box(H.p('chest') + V(0, 0.0, -0.11), (0.1, 0.12, 0.06), 'leather', 'chest', k=0.02, r=0.02)
    sc.ellipsoid(H.p('chest') + V(0, 0.0, 0.0), (0.11, 0.09, 0.08), 'leather_black', 'chest', k=0.02)
    hd = H.p('head')
    sc.ellipsoid(hd + V(0, 0.09, -0.02), (0.085, 0.08, 0.09), 'cloth_red', 'head', k=0.02)
    sc.capsule(hd + V(0, 0.13, -0.06), hd + V(0, 0.16, -0.16), 0.04, 0.008, 'cloth_red', 'head', k=0.02)
    for sg in (1, -1):
        sc.torus(hd + V(sg * 0.025, 0.085, 0.07), 0.016, 0.006, 'iron', 'head', k=0.004, R=rot(0, 90, 0))
        sc.ellipsoid(hd + V(sg * 0.025, 0.085, 0.072), (0.013, 0.013, 0.004), 'glow_storm', 'head', k=0.002)
    sc.ellipsoid(H.p('hips'), (0.11, 0.07, 0.09), 'cloth_green', 'hips', k=0.01)
    finish(m, sc, 6500, 0.006)
    pack = merge([cylinder(0.035, 0.18, 10).t(x, 0, 0) for x in (-0.05, 0.0, 0.05)])
    on(m, H, 'chest', pack, 'iron_rust', (0, -0.08, -0.15))
    on(m, H, 'chest', merge([sphere(0.025, seg=8).t(x, 0.11, 0) for x in (-0.05, 0.0, 0.05)]), 'glow_storm', (0, -0.08, -0.15))
    coil = tube(np.array([(math.cos(t * 20) * 0.02, t * 0.25, math.sin(t * 20) * 0.02) for t in np.linspace(0, 1, 60)]), 0.004, 4)
    on(m, H, 'chest', coil, 'gold', (0.06, 0.05, -0.16))
    torch = merge([cylinder(0.012, 0.22, 6).t(0, -0.05, 0)])
    hold(m, H, torch, 'wood', 'R')
    hold(m, H, sphere(0.03, seg=8).t(0, 0.17, 0), 'glow_storm', 'R')
    H.anims('melee', speed=1.4)
    return m


@enemy
def scout():
    m = E('scout', 'Pfadspäher')
    H = Humanoid(m, 0.95, w=0.85, legs=0.5)
    sc = Sculpt(detail=0.0015, dfreq=40, seed=115)
    humanoid_body(sc, H, skin='leather_black', bulk=0.85, gaunt=0.5, muscle=0.3, head='human', hands='hand', feet='boot', skin2='leather')
    hd = H.p('head')
    sc.ellipsoid(hd + V(0, 0.075, -0.01), (0.085, 0.09, 0.095), 'cloth_dark', 'head', k=0.02)
    sc.ellipsoid(hd + V(0, 0.07, 0.06), (0.06, 0.065, 0.06), 'cloth_dark', 'head', sub=True, k=0.015)
    sc.box(hd + V(0, 0.04, 0.065), (0.045, 0.025, 0.012), 'leather_black', 'head', k=0.01, r=0.008)  # Maske
    sc.ellipsoid(H.p('chest') + V(0, 0, -0.01), (0.16, 0.14, 0.12), 'leather_black', 'chest', k=0.02)
    sc.capsule(H.p('chest') + V(0, 0.08, -0.05), H.p('hips') + V(0, -0.25, -0.12), 0.12, 0.17, 'cloth_dark', 'spine', k=0.04)  # Umhang
    sc.box(H.p('hips') + V(0, -0.28, 0.1), (0.3, 0.3, 0.12), 'cloth_dark', 'hips', sub=True, k=0.03)
    finish(m, sc, 8000, 0.008)
    limbs, string = kit.bow(0.7)
    hold(m, H, limbs, 'wood_black', 'L', fwd=False)
    hold(m, H, string, 'leather_black', 'L', fwd=False)
    quiver = merge([cylinder(0.035, 0.3, 8)] + [cylinder(0.004, 0.12, 4).t(dx, 0.3, 0) for dx in (-0.015, 0.0, 0.015)])
    on(m, H, 'chest', quiver.rz(0.4), 'leather', (0.08, -0.12, -0.1))
    H.anims('ranged', speed=1.3)
    return m


@enemy
def broodmother():
    m = E('broodmother', 'Brutmutter')
    C = Crawler(m, size=1.0, legs=8, height=0.35)
    sc = Sculpt(detail=0.003, dfreq=18, seed=117)
    P = crawler_body(sc, C, 'chitin', 'flesh', sac=0.25)
    rng = np.random.default_rng(3)
    eggs = []
    for i in range(9):
        a = rng.random() * TAU
        b = rng.random() * 0.8 + 0.2
        c = P['abdomen'] + V(math.cos(a) * 0.24 * b, 0.12 + math.sin(b * 2) * 0.12, -0.12 + math.sin(a) * 0.28 * b)
        eggs.append(c)
    finish(m, sc, 9000, 0.012)
    m.add(merge([sphere(0.035, 0.045, 0.035, 8).t(*c) for c in eggs]), 'glow_poison', 'abdomen')
    C.anims(speed=0.8)
    return m


@enemy
def broodling():
    m = E('broodling', 'Brutling')
    C = Crawler(m, size=0.38, legs=6, height=0.35)
    sc = Sculpt(detail=0.0015, dfreq=50, seed=119)
    crawler_body(sc, C, 'chitin', 'flesh')
    finish(m, sc, 4000, 0.005)
    C.anims(speed=1.6)
    return m


@enemy
def golem():
    m = E('golem', 'Höhlengolem')
    H = Humanoid(m, 1.25, w=1.5, legs=0.38, arm=1.25, hunch=0.6)
    sc = Sculpt(detail=0.008, dfreq=8, seed=121)
    humanoid_body(sc, H, skin='rock', bulk=1.5, muscle=1.0, belly=0.3, head='brute', hands='stump', feet='hoof', neck=1.8, head_size=0.8)
    for b in ('upperarm_L', 'upperarm_R', 'chest', 'forearm_L', 'forearm_R', 'thigh_L', 'thigh_R'):
        sc.ellipsoid(H.p(b) + V(0, -0.02, 0), (0.11, 0.1, 0.11), 'rock', b, k=0.04, noise=0.03)
    finish(m, sc, 9000, 0.012)
    rng = np.random.default_rng(4)
    for b, n in (('chest', 4), ('upperarm_L', 2), ('upperarm_R', 2), ('spine', 2)):
        cl = kit.crystal_cluster(n + 1, 0.035, 0.18, 0.05, seed=n + len(b)).rx(-0.6)
        on(m, H, b, cl, 'crystal_cyan', (rng.normal() * 0.05, 0.08, -0.12))
    H.anims('brute', speed=0.7, heavy=True)
    return m


@enemy
def golemling():
    m = E('golemling', 'Golemsplitter')
    H = Humanoid(m, 0.55, w=1.4, legs=0.38, arm=1.2, hunch=0.5)
    sc = Sculpt(detail=0.005, dfreq=14, seed=123)
    humanoid_body(sc, H, skin='rock', bulk=1.4, muscle=0.8, head='brute', hands='stump', feet='hoof', neck=1.5, head_size=0.9)
    finish(m, sc, 4500, 0.008)
    on(m, H, 'chest', kit.crystal_cluster(3, 0.025, 0.12, 0.03, seed=2).rx(-0.6), 'crystal_cyan', (0, 0.04, -0.07))
    H.anims('brute', speed=1.0, heavy=True)
    return m


@enemy
def stalker():
    m = E('stalker', 'Schattenschleicher')
    Q = Quadruped(m, length=1.0, height=0.42, neck=0.2, tail=0.5)
    sc = Sculpt(detail=0.0025, dfreq=28, seed=125)
    P = quad_body(sc, Q, 'fur', bulk=1.15, snout=1.2, leg=1.0, mat2='void')
    for i in range(6):
        z = 0.25 - i * 0.12
        sc.capsule(V(0, 0.55 - i * 0.01, z), V(0, 0.65 - i * 0.015, z - 0.06), 0.02, 0.002, 'obsidian', 'body' if z < 0.1 else 'chest', k=0.01)
    finish(m, sc, 7500, 0.008)
    Q.anims(speed=1.4)
    return m


@enemy
def siege():
    m = E('siege', 'Belagerungsbestie')
    Q = Quadruped(m, length=1.5, height=0.62, neck=0.15, tail=0.3)
    sc = Sculpt(detail=0.004, dfreq=12, seed=127)
    P = quad_body(sc, Q, 'scales_ash', bulk=1.5, snout=0.7, leg=1.3, mat2='obsidian')
    hd = P['head']
    for sg in (1, -1):
        sc.chain([hd + V(sg * 0.08, 0.05, 0.05), hd + V(sg * 0.2, 0.1, 0.12), hd + V(sg * 0.22, 0.22, 0.22)], [0.045, 0.03, 0.004], 'bone', 'head', k=0.02)
    sc.capsule(hd + V(0, 0.0, 0.2), hd + V(0, 0.12, 0.34), 0.05, 0.005, 'bone', 'head', k=0.02)
    finish(m, sc, 10000, 0.014)
    # Panzerplatten und Sattelturm
    for z in (-0.25, 0.0, 0.25):
        plate = rbox(0.5, 0.05, 0.22, 0.02).rx(0.0)
        on(m, Q, 'body' if z < 0.1 else 'chest', plate, 'iron_rust', (0, 0.26, z - (0.0 if z < 0.1 else 0.37)))
    tower = merge([rbox(0.34, 0.25, 0.34, 0.02).t(0, 0.12, 0)] + [rbox(0.08, 0.08, 0.06, 0.01).t(x, 0.28, z) for x in (-0.13, 0.13) for z in (-0.13, 0.13)])
    on(m, Q, 'body', tower, 'wood_black', (0, 0.3, 0.0))
    on(m, Q, 'body', kit.skull(0.08), 'bone', (0, 0.42, 0.18))
    cloth, _ = kit.banner(0.16, 0.3, pole=False)
    on(m, Q, 'body', cloth.ry(math.pi / 2), 'cloth_red', (0.26, 0.3, 0))
    Q.anims(speed=0.6)
    return m


def on(m, R, bone, mesh, mat, at=(0, 0, 0)):  # noqa: F811 – für Humanoid und andere Rigs
    p = np.array(m.nodes[bone].pos)
    m.add(mesh.t(*(p + np.array(at, float))), mat, bone)


@enemy
def shaman():
    m = E('shaman', 'Heilerschamane')
    H = Humanoid(m, 0.95, w=0.95, legs=0.46, hunch=0.8)
    sc = Sculpt(detail=0.002, dfreq=30, seed=129)
    humanoid_body(sc, H, skin='flesh_grey', gaunt=0.7, muscle=0.2, head='ghoul', hands='claw', feet='boot', ears=0.8)
    c, hp = H.p('chest'), H.p('hips')
    sc.capsule(c + V(0, 0.02, 0), hp + V(0, -0.3, 0), 0.14, 0.2, 'cloth_green', 'spine', k=0.04, noise=0.01)
    sc.box(V(0, 0.0, 0), (0.4, 0.06, 0.4), 'cloth_green', 'hips', sub=True, k=0.02)
    sc.box(hp + V(0, -0.25, 0.18), (0.08, 0.2, 0.1), 'cloth_green', 'hips', sub=True, k=0.02)
    head_shape(sc, H.p('head') + V(0, 0.005, 0.012), H.h * 1.05, 'bone', kind='skull')
    finish(m, sc, 8500, 0.008)
    sh, claw, gem = kit.staff(1.0)
    hold(m, H, sh, 'wood_black', 'R', fwd=False)
    hold(m, H, claw, 'bone', 'R', fwd=False)
    hold(m, H, gem, 'crystal_green', 'R', fwd=False)
    hold(m, H, kit.skull(0.07).t(0, 0.62, 0.02), 'bone', 'R', fwd=False)
    beads = merge([sphere(0.012, seg=6).t(math.cos(a) * 0.11, -0.04 + 0.03 * math.cos(a), math.sin(a) * 0.09 + 0.03) for a in np.linspace(0, math.pi, 9)])
    on(m, H, 'chest', beads, 'bone', (0, 0.02, 0.02))
    H.anims('caster', speed=0.9)
    return m


@enemy
def tunneler():
    m = E('tunneler', 'Tunnelgräber')
    Q = Quadruped(m, length=0.9, height=0.3, neck=0.12, tail=0.2, low=True)
    sc = Sculpt(detail=0.003, dfreq=20, seed=131)
    P = quad_body(sc, Q, 'fur', bulk=1.6, snout=0.9, leg=1.1, mat2='chitin_red')
    for sd in ('FL', 'FR'):  # Grabklauen
        c = P['paw_' + sd]
        sg = 1 if sd[1] == 'L' else -1
        sc.ellipsoid(c + V(sg * 0.02, 0.03, 0.05), (0.07, 0.04, 0.07), 'chitin_red', 'paw_' + sd, k=0.02)
        for k in range(3):
            sc.capsule(c + V(sg * 0.02 + (k - 1) * 0.035, 0.02, 0.1), c + V(sg * 0.02 + (k - 1) * 0.04, -0.01, 0.2), 0.016, 0.002, 'bone', 'paw_' + sd, k=0.006)
    hd = P['head']
    for i in range(6):  # Rüsselkranz
        a = TAU * i / 6
        sc.capsule(hd + V(math.cos(a) * 0.025, math.sin(a) * 0.025 - 0.01, 0.15), hd + V(math.cos(a) * 0.04, math.sin(a) * 0.04 - 0.01, 0.2), 0.008, 0.002, 'flesh', 'head', k=0.005)
    finish(m, sc, 7000, 0.007)
    Q.anims(speed=1.0)
    return m


@enemy
def griffin():
    m = E('griffin', 'Sturmgreif')
    F = Flyer(m, span=1.8, body=0.75, alt=1.05)
    P = {k: V(*v) for k, v in F.P.items()}
    sc = Sculpt(detail=0.002, dfreq=26, seed=133)
    sc.ellipsoid(P['body'] + V(0, 0, -0.05), (0.14, 0.13, 0.26), 'fur', 'body', k=0.04)
    sc.ellipsoid(P['body'] + V(0, 0.02, 0.15), (0.13, 0.13, 0.13), 'fur_grey', 'body', k=0.04)
    sc.capsule(P['body'] + V(0, 0.06, 0.18), P['head'], 0.08, 0.06, 'fur_grey', 'neck', k=0.04)
    sc.ellipsoid(P['head'] + V(0, 0.02, 0.02), (0.065, 0.065, 0.08), 'fur_grey', 'head', k=0.02)
    sc.capsule(P['head'] + V(0, 0.01, 0.08), P['jaw'] + V(0, -0.04, 0.1), 0.03, 0.006, 'gold', 'head', k=0.01)  # Schnabel
    for sg in (1, -1):
        sd = 'L' if sg > 0 else 'R'
        sc.ellipsoid(P['head'] + V(sg * 0.04, 0.04, 0.06), 0.012, 'glow_storm', 'head', k=0.001)
        sc.capsule(P['wing_' + sd], P['wingtip_' + sd], 0.05, 0.025, 'fur_grey', 'wing_' + sd, k=0.03)
        for k in range(6):  # Federn
            t = k / 5
            base = P['wing_' + sd] * (1 - t) + P['wingtip_' + sd] * t
            sc.ellipsoid(base + V(sg * 0.05 * t, -0.01, -0.15 - 0.08 * t), (0.05 + 0.03 * t, 0.012, 0.16 + 0.06 * t), 'fur', 'wingtip_' + sd if t > 0.4 else 'wing_' + sd, k=0.03,
                         R=rot(sg * (10 + 25 * t), 0, 0))
        for zz, lb in ((0.15, 'leg_'), (-0.18, 'leg_')):
            top = P['body'] + V(sg * 0.08, -0.06, zz)
            sc.capsule(top, top + V(0, -0.2, 0.02), 0.035, 0.02, 'fur' if zz < 0 else 'gold', lb + sd, k=0.02)
    sc.chain([P['tail1'], P['tail2'], P['tail2'] + V(0, -0.05, -0.3)], [0.035, 0.022, 0.03], 'fur', 'tail2', k=0.02)
    finish(m, sc, 9000, 0.01)
    F.anims(speed=1.0)
    return m


@enemy
def swarm():
    m = E('swarm', 'Splitterschwarm')
    Fl = Floater(m, alt=0.7, orbits=6, radius=0.28)
    for i in range(6):
        p = V(*m.nodes[f'orbit{i}'].pos) + V(0, 0.08 * math.sin(i * 1.7), 0)
        sc = Sculpt(detail=0.001, dfreq=60, seed=140 + i)
        sc.ellipsoid(p, (0.035, 0.03, 0.06), 'chitin_blue', f'orbit{i}', k=0.01)
        sc.ellipsoid(p + V(0, 0.005, 0.055), 0.025, 'chitin_blue', f'orbit{i}', k=0.01)
        sc.capsule(p + V(0, 0, -0.04), p + V(0, -0.01, -0.11), 0.02, 0.003, 'chitin_blue', f'orbit{i}', k=0.008)
        finish(m, sc, 900, 0.004, 60)
        for sg in (1, -1):
            wing = crystal(0.012, 0.09, 4, 0.4, 0.2).rz(-sg * 1.2).t(*(p + V(sg * 0.02, 0.03, 0)))
            m.add(wing, 'crystal_cyan', f'orbit{i}')
    sc = Sculpt(detail=0.001, dfreq=50, seed=150)
    sc.ellipsoid(V(0, 0.7, 0), 0.07, 'crystal_cyan', 'core', k=0.01)
    finish(m, sc, 800, 0.006, 60)
    Fl.anims()
    for i in range(6):
        m.anim_pos('Idle', f'orbit{i}', [(t, (0, 0.05 * math.sin(TAU * t / 1.5 * 2 + i), 0)) for t in np.linspace(0, 3, 9)])
    return m
