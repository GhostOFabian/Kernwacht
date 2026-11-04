"""Bossmodelle (2–3 m) mit Skelett und Clips Idle, Walk, Attack, Death."""
from __future__ import annotations

import math

import numpy as np

from . import kit
from .anatomy import V, hand, head_shape, humanoid_body, membrane_mesh, membrane_wing
from .enemies import finish, hold, on
from .geo import TAU, crystal, curve, cylinder, displace, icosphere, merge, rbox, sphere, spike, torus, tube
from .model import Model
from .rig import Chain, Floater, Humanoid, loop_keys
from .sdf import Sculpt, rot

REG = {}


def boss(fn):
    REG[fn.__name__] = fn
    return fn


def B(name, title):
    return Model(name, 'boss', title, skinned=True)


@boss
def riftColossus():
    m = B('riftColossus', 'Der Risskoloss')
    H = Humanoid(m, 2.6, w=1.5, legs=0.4, arm=1.25, hunch=0.6)
    sc = Sculpt(detail=0.015, dfreq=5, seed=201)
    humanoid_body(sc, H, skin='rock', bulk=1.5, muscle=1.0, belly=0.2, head='brute', hands='hand', feet='hoof', neck=1.8, head_size=0.75,
                  horns=('obsidian', 1.1, 0.6))
    for b in ('upperarm_L', 'upperarm_R', 'chest', 'spine', 'thigh_L', 'thigh_R', 'forearm_L', 'forearm_R'):
        sc.ellipsoid(H.p(b) + V(0, 0, -0.05), (0.22, 0.2, 0.2), 'rock_red', b, k=0.08, noise=0.05)
    sc.ellipsoid(H.p('chest') + V(0, 0.0, 0.22), (0.16, 0.24, 0.08), 'void', 'chest', k=0.06)  # Riss in der Brust
    finish(m, sc, 18000, 0.02, 150)
    for b, off in (('chest', (0.2, 0.25, -0.25)), ('chest', (-0.25, 0.2, -0.2)), ('upperarm_L', (0.1, 0.15, -0.1)), ('upperarm_R', (-0.1, 0.15, -0.1)), ('spine', (0, 0.1, -0.3))):
        on(m, H, b, kit.crystal_cluster(5, 0.07, 0.4, 0.09, seed=len(b) + int(off[0] * 10)).rx(-0.5), 'crystal_violet', off)
    on(m, H, 'chest', crystal(0.08, 0.35, 6).rx(math.pi / 2).t(0, 0, 0.0), 'glow_arcane', (0, 0, 0.26))
    H.anims('brute', speed=0.5, heavy=True)
    return m


@boss
def ashQueen():
    m = B('ashQueen', 'Die Aschenkönigin')
    H = Humanoid(m, 2.2, w=0.9, legs=0.5, hover=0.25, arm=1.1)
    for sd, sg in (('L', 1), ('R', -1)):
        m.node('wing_' + sd, 'chest', (sg * 0.12, 1.75, -0.15), (sg * 0.8, 2.1, -0.4))
        m.node('wingtip_' + sd, 'wing_' + sd, (sg * 0.8, 2.1, -0.35), (sg * 1.6, 2.3, -0.6))
    sc = Sculpt(detail=0.003, dfreq=18, seed=203)
    humanoid_body(sc, H, skin='obsidian', bulk=0.9, gaunt=0.3, muscle=0.3, head='human', hands='claw', feet='claw', head_size=1.0,
                  horns=('obsidian', 1.6, 1.4))
    hp = H.p('hips')
    sc.capsule(hp + V(0, 0.05, 0), V(0, 0.3, -0.05), 0.24, 0.5, 'ember', 'hips', k=0.1, noise=0.03)  # Glutkleid
    sc.box(V(0, 0.1, 0), (0.7, 0.18, 0.7), 'ember', 'hips', sub=True, k=0.05)
    wings = []
    for sd, sg in (('L', 1), ('R', -1)):
        root, elbow, tips = membrane_wing(sc, m.nodes['wing_' + sd].pos, 1.6, sg, 'wing_' + sd, 'wingtip_' + sd, 'obsidian', 'ember', h=2.0)
        wings.append((sd, membrane_mesh(V(*m.nodes['wing_' + sd].pos), elbow, tips, V(sg * 0.15, 1.3, -0.2), 0.006)))
    finish(m, sc, 18000, 0.016, 160)
    for sd, w in wings:
        m.add(w, 'ember', 'wing_' + sd, blend=('wingtip_' + sd,))
    crown = merge([spike(0.02, 0.18 + 0.08 * (i % 2), 5).t(math.cos(a) * 0.11, 0, math.sin(a) * 0.11) for i, a in enumerate(np.linspace(0, TAU, 9)[:-1])] + [torus(0.11, 0.015, 24, 5)])
    on(m, H, 'head', crown, 'gold', (0, 0.26, 0))
    hold(m, H, kit.staff(1.9)[0], 'obsidian', 'R', fwd=False)
    hold(m, H, crystal(0.06, 0.25, 6).t(0, 1.25, 0), 'glow_fire', 'R', fwd=False)
    H.anims('caster', speed=0.6, fly=True)
    for clip, T_, A in (('Idle', 2.0, 15), ('Walk', 1.2, 30)):
        m.anim_rot(clip, 'wing_L', loop_keys(8, T_, lambda f, A=A: A * math.sin(f * TAU)), axis=(0, 0, 1))
        m.anim_rot(clip, 'wing_R', loop_keys(8, T_, lambda f, A=A: -A * math.sin(f * TAU)), axis=(0, 0, 1))
    return m


@boss
def nullWarden():
    m = B('nullWarden', 'Der Nullhüter')
    Fl = Floater(m, alt=1.6, orbits=4, radius=1.0)
    sc = Sculpt(detail=0.003, dfreq=10, seed=205)
    c = V(0, 1.6, 0)
    sc.capsule(c + V(0, -0.9, 0), c + V(0, 0.5, 0), 0.32, 0.38, 'cloth_dark', 'core', k=0.15, noise=0.02)  # Gewand
    sc.ellipsoid(c + V(0, 0.75, 0), (0.3, 0.34, 0.3), 'cloth_dark', 'core', k=0.1)  # Kapuze
    sc.ellipsoid(c + V(0, 0.7, 0.2), (0.22, 0.26, 0.2), 'cloth_dark', 'core', sub=True, k=0.05)
    sc.ellipsoid(c + V(0, 0.68, 0.08), (0.17, 0.2, 0.12), 'void', 'core', k=0.03)  # gesichtslose Leere
    for sg in (1, -1):
        sc.capsule(c + V(sg * 0.35, 0.35, 0), c + V(sg * 0.65, -0.1, 0.25), 0.12, 0.08, 'cloth_dark', 'core', k=0.08)
        hand(sc, c + V(sg * 0.67, -0.1, 0.3), sg, 1.8, 'bone', 'core', claw=True)
    sc.box(c + V(0, -1.0, 0), (0.6, 0.2, 0.6), 'cloth_dark', 'core', sub=True, k=0.05)
    finish(m, sc, 12000, 0.016, 150)
    on(m, Fl, 'core', merge([crystal(0.03, 0.12, 4).t(0, 0, 0).t(math.cos(a) * 0.16, 0.92, math.sin(a) * 0.16 + 0.02) for a in np.linspace(0, TAU, 7)[:-1]]), 'glow_arcane', (0, 0, 0))
    on(m, Fl, 'core', torus(0.5, 0.025, 40, 6).t(0, -0.2, 0), 'gold', (0, 0, 0))
    for i in range(4):  # umkreisende Obelisken
        ob = merge([kit.rune_stone(0.18, 0.6, 0.12, seed=i).t(0, -0.3, 0)])
        on(m, Fl, f'orbit{i}', ob, 'runes', (0, 0, 0))
        on(m, Fl, f'orbit{i}', crystal(0.05, 0.2, 5).t(0, 0.32, 0), 'crystal_violet', (0, 0, 0))
    Fl.anims()
    m.anim_rot('Attack', 'core', [(0, (1, 0, 0), 0), (0.3, (1, 0, 0), -12), (0.5, (1, 0, 0), 10), (0.9, (1, 0, 0), 0)])
    return m


@boss
def maw():
    m = B('maw', 'Der Schlund der Tiefe')
    C = Chain(m, n=8, length=2.4, height=0.35, up=1.2)
    sc = Sculpt(detail=0.006, dfreq=9, seed=207)
    P = [V(*m.nodes[b].pos) for b in C.bones]
    for i, b in enumerate(C.bones):
        r = 0.42 - i * 0.03
        sc.ellipsoid(P[i], (r, r * 0.95, 0.24), 'flesh', b, k=0.12)
        if i < 7:
            sc.capsule(P[i], P[i + 1], r * 0.85, (r - 0.03) * 0.85, 'flesh', b, k=0.1)
        sc.ellipsoid(P[i] + V(0, r * 0.55, -0.02), (r * 0.9, r * 0.4, 0.2), 'chitin_red', b, k=0.05)  # Rückenplatten
    head = P[0]
    sc.torus(head + V(0, 0.1, 0.2), 0.3, 0.1, 'flesh', 'seg0', k=0.06, R=rot(0, 90, 0))
    sc.ellipsoid(head + V(0, 0.1, 0.32), (0.26, 0.26, 0.2), 'flesh', 'seg0', sub=True, k=0.05)
    sc.ellipsoid(head + V(0, 0.1, 0.12), (0.2, 0.2, 0.08), 'void', 'seg0', k=0.03)
    for ring, (rr, z) in enumerate(((0.3, 0.27), (0.22, 0.2))):
        for k in range(14 - ring * 4):
            a = TAU * k / (14 - ring * 4)
            base = head + V(math.cos(a) * rr, 0.1 + math.sin(a) * rr, z)
            sc.capsule(base, base + V(-math.cos(a) * 0.12, -math.sin(a) * 0.12, 0.04), 0.03, 0.003, 'bone', 'seg0', k=0.01)
    finish(m, sc, 18000, 0.02, 150)
    eyes = merge([sphere(0.04, seg=8).t(*(head + V(math.cos(a) * 0.38, 0.1 + math.sin(a) * 0.38, 0.02))) for a in np.linspace(0.3, math.pi - 0.3, 5)])
    m.add(eyes, 'glow_poison', 'seg0')
    C.anims(speed=0.6, rear=30)
    return m


def _twin(name, title, body, halo, weapon):
    m = B(name, title)
    H = Humanoid(m, 2.0, w=1.0, legs=0.5, hover=0.3, arm=1.05)
    sc = Sculpt(detail=0.003, dfreq=16, seed=hash(name) % 1000)
    humanoid_body(sc, H, skin=body, bulk=1.0, muscle=0.6, head='human', hands='hand', feet='boot', head_size=1.0)
    hp = H.p('hips')
    sc.capsule(hp + V(0, 0.05, 0), V(0, 0.35, -0.05), 0.2, 0.36, body, 'hips', k=0.1, noise=0.02)
    sc.box(V(0, 0.2, 0), (0.5, 0.2, 0.5), body, 'hips', sub=True, k=0.05)
    finish(m, sc, 15000, 0.014, 160)
    return m, H


@boss
def twinSol():
    m, H = _twin('twinSol', 'Sol, der Glühende Zwilling', 'ember', 'gold', 'sword')
    halo = merge([torus(0.38, 0.025, 40, 6).rx(math.pi / 2)] + [spike(0.035, 0.22, 5).rz(a - math.pi / 2).t(math.cos(a) * 0.4, math.sin(a) * 0.4, 0) for a in np.linspace(0, TAU, 13)[:-1]])
    on(m, H, 'head', halo, 'gold', (0, 0.18, -0.2))
    for sd in 'LR':
        bl, gp, gd = kit.blade(0.9, 0.08, 0.016, 0.2, 0.18, curved=0.4)
        hold(m, H, bl, 'glow_fire', sd)
        hold(m, H, gp, 'leather_black', sd)
        hold(m, H, gd, 'gold', sd)
    H.anims('melee', speed=0.7, fly=True)
    return m


@boss
def twinLuna():
    m, H = _twin('twinLuna', 'Luna, die Kalte Zwillingin', 'ice', 'steel', 'staff')
    moon = tube(np.array([(math.cos(a) * 0.4, math.sin(a) * 0.4, 0) for a in np.linspace(-1.9, 1.9, 24)]), np.concatenate([np.linspace(0.01, 0.06, 12), np.linspace(0.06, 0.01, 12)]), 8)
    on(m, H, 'head', moon.rz(math.pi / 2), 'crystal_cyan', (0, 0.2, -0.2))
    sh, claw, gem = kit.staff(1.9)
    hold(m, H, sh, 'steel', 'R', fwd=False)
    hold(m, H, claw, 'ice', 'R', fwd=False)
    hold(m, H, gem.s(1.6), 'crystal_cyan', 'R', fwd=False)
    H.anims('caster', speed=0.7, fly=True)
    return m


@boss
def worldbreaker():
    m = B('worldbreaker', 'Der Weltenbrecher')
    H = Humanoid(m, 3.0, w=1.45, legs=0.42, arm=1.15, hunch=0.3)
    sc = Sculpt(detail=0.004, dfreq=8, seed=211)
    humanoid_body(sc, H, skin='flesh_grey', bulk=1.45, muscle=1.0, head='brute', hands='hand', feet='boot', neck=1.5, head_size=0.85,
                  horns=('bone', 1.3, 0.8))
    # Rüstung
    sc.ellipsoid(H.p('chest') + V(0, 0.04, 0.03), (0.72, 0.44, 0.48), 'iron_rust', 'chest', k=0.04)
    sc.ellipsoid(H.p('hips') + V(0, -0.02, 0), (0.62, 0.3, 0.44), 'iron', 'hips', k=0.04)
    for sd in 'LR':
        sc.capsule(H.p('shin_' + sd), H.p('foot_' + sd) + V(0, 0.12, 0), 0.2, 0.17, 'iron', 'shin_' + sd, k=0.03)
        sc.capsule(H.p('forearm_' + sd) + V(0, -0.05, 0), H.p('hand_' + sd) + V(0, 0.08, 0), 0.17, 0.15, 'iron', 'forearm_' + sd, k=0.03)
    finish(m, sc, 20000, 0.02, 160)
    for sd, sg in (('L', 1), ('R', -1)):
        on(m, H, 'upperarm_' + sd, kit.pauldron(0.24, 3).s(1, 1, 1), 'iron_rust', (sg * 0.06, 0.08, 0))
    on(m, H, 'chest', kit.skull(0.25), 'bone', (0, 0.05, 0.38))
    on(m, H, 'chest', kit.chain((0.4, 0.2, 0.35), (-0.4, -0.25, 0.35), 12, 0.05, 0.05).t(0, 0, 0), 'iron_rust', (0, 0, 0))
    hammer = merge([cylinder(0.05, 1.8, 10).t(0, -0.6, 0), rbox(0.7, 0.4, 0.4, 0.05).t(0, 1.25, 0)] + [spike(0.06, 0.2, 6).rz(math.pi / 2 * s).t(s * 0.35, 1.25, 0) for s in (1, -1)])
    hold(m, H, hammer, 'iron', 'R')
    hold(m, H, merge([torus(0.21, 0.03, 20, 6).t(0, 1.25, 0)]).rx(0), 'gold', 'R')
    m.add(merge([sphere(0.04, seg=8).t(*(H.p('head') + V(sg * 0.07, 0.17, 0.2))) for sg in (1, -1)]), 'glow_fire', 'head')
    H.anims('brute', speed=0.45, heavy=True)
    return m
