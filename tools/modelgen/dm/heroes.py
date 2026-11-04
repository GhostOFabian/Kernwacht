"""Heldenmodelle (12, ~1 m) mit Skelett und Clips Idle, Walk, Attack, Death. Jeder Held hat ein eigenes Modell."""
from __future__ import annotations

import math

import numpy as np

from . import kit
from .anatomy import V, humanoid_body
from .enemies import finish, hold, on
from .geo import TAU, crystal, curve, cylinder, displace, icosphere, merge, rbox, sphere, spike, torus, tube
from .model import Model
from .rig import Humanoid
from .sdf import Sculpt, rot

REG = {}


def hero(fn):
    REG[fn.__name__] = fn
    return fn


def Hm(name, title):
    return Model(name, 'hero', title, skinned=True)


# ---------------------------------------------------------------------- Kleidung

def robe(sc, H, mat, length=0.95, flare=1.0):
    c, hp = H.p('chest'), H.p('hips')
    h = H.h
    sc.ellipsoid(c + V(0, 0.0, 0.0), (0.16 * h, 0.14 * h, 0.115 * h), mat, 'chest', k=0.03 * h)
    sc.capsule(c, hp, 0.13 * h, 0.14 * h, mat, 'spine', k=0.04 * h)
    bottom = h * (1 - length) * 0.5 + 0.03 * h
    for sd, sg in (('L', 1), ('R', -1)):
        sc.capsule(hp + V(sg * 0.06 * h, 0, 0), V(sg * 0.1 * h * flare, bottom, 0.0), 0.11 * h, 0.13 * h * flare, mat, 'thigh_' + sd, k=0.05 * h)
    sc.capsule(hp + V(0, 0, -0.03 * h), V(0, bottom, -0.08 * h), 0.12 * h, 0.15 * h * flare, mat, 'hips', k=0.05 * h)


def cloak(sc, H, mat, length=0.75, hood=False):
    h = H.h
    c = H.p('chest')
    top = c + V(0, 0.05 * h, -0.09 * h)
    bot = V(0, h * (1 - length), -0.16 * h)
    sc.capsule(top, (top + bot) / 2 + V(0, 0, -0.03 * h), 0.14 * h, 0.17 * h, mat, 'chest', k=0.04 * h)
    sc.capsule((top + bot) / 2 + V(0, 0, -0.03 * h), bot, 0.17 * h, 0.2 * h, mat, 'spine', k=0.04 * h)
    sc.box(V(0, h * 0.45, 0.06 * h), (0.3 * h, h * 0.5, 0.08 * h), mat, 'spine', sub=True, k=0.03 * h)
    if hood:
        hd = H.p('head')
        sc.ellipsoid(hd + V(0, 0.02 * h, -0.07 * h), (0.09 * h, 0.07 * h, 0.06 * h), mat, 'neck', k=0.03 * h)


def hair(sc, H, mat, style='long'):
    h = H.h
    hd = H.p('head')
    sc.ellipsoid(hd + V(0, 0.09 * h, -0.012 * h), (0.064 * h, 0.06 * h, 0.073 * h), mat, 'head', k=0.01 * h)
    if style in ('long', 'pony'):
        if style == 'long':
            for x in (-0.04, 0.0, 0.04):
                sc.capsule(hd + V(x * h, 0.08 * h, -0.05 * h), hd + V(x * 1.4 * h, -0.12 * h, -0.07 * h), 0.03 * h, 0.012 * h, mat, 'neck', k=0.02 * h)
        else:
            sc.chain([hd + V(0, 0.12 * h, -0.07 * h), hd + V(0, 0.07 * h, -0.13 * h), hd + V(0, -0.06 * h, -0.13 * h)], [0.022 * h, 0.018 * h, 0.006 * h], mat, 'head', k=0.01 * h)
    elif style == 'bun':
        sc.ellipsoid(hd + V(0, 0.14 * h, -0.05 * h), 0.03 * h, mat, 'head', k=0.01 * h)


def beard(sc, H, mat, L=1.0):
    h = H.h
    hd = H.p('head')
    sc.capsule(hd + V(0, 0.04 * h, 0.05 * h), hd + V(0, -0.06 * h * L, 0.07 * h), 0.045 * h, 0.02 * h, mat, 'head', k=0.02 * h)
    for sg in (1, -1):
        sc.capsule(hd + V(sg * 0.04 * h, 0.06 * h, 0.03 * h), hd + V(sg * 0.035 * h, 0.0, 0.06 * h), 0.02 * h, 0.02 * h, mat, 'head', k=0.015 * h)


def plate(sc, H, mat, chest=True, arms=True, legs=True, belt='leather_black'):
    h = H.h
    if chest:
        sc.ellipsoid(H.p('chest') + V(0, 0.01 * h, 0.012 * h), (0.175 * h, 0.15 * h, 0.125 * h), mat, 'chest', k=0.015 * h)
        sc.ellipsoid(H.p('spine') + V(0, 0, 0.01 * h), (0.14 * h, 0.1 * h, 0.105 * h), mat, 'spine', k=0.015 * h)
        sc.torus(H.p('hips') + V(0, 0.03 * h, 0), 0.13 * h, 0.018 * h, belt, 'hips', k=0.008 * h)
    for sd in 'LR':
        if arms:
            sc.capsule(H.p('forearm_' + sd) + V(0, -0.02 * h, 0), H.p('hand_' + sd) + V(0, 0.02 * h, 0), 0.042 * h, 0.036 * h, mat, 'forearm_' + sd, k=0.008 * h)
        if legs:
            sc.capsule(H.p('shin_' + sd) + V(0, 0.02 * h, 0.01 * h), H.p('foot_' + sd) + V(0, 0.03 * h, 0.01 * h), 0.052 * h, 0.044 * h, mat, 'shin_' + sd, k=0.008 * h)
            sc.ellipsoid(H.p('foot_' + sd) + V(0, -0.02 * h, 0.05 * h), (0.045 * h, 0.035 * h, 0.085 * h), mat, 'foot_' + sd, k=0.01 * h)


def tunic(sc, H, mat, skirt=0.25):
    h = H.h
    sc.ellipsoid(H.p('chest') + V(0, 0.0, 0.005 * h), (0.165 * h, 0.145 * h, 0.12 * h), mat, 'chest', k=0.02 * h)
    sc.capsule(H.p('chest'), H.p('hips'), 0.125 * h, 0.13 * h, mat, 'spine', k=0.03 * h)
    for sd, sg in (('L', 1), ('R', -1)):
        sc.capsule(H.p('hips') + V(sg * 0.05 * h, 0, 0), H.p('thigh_' + sd) + V(sg * 0.02 * h, -skirt * h, 0), 0.1 * h, 0.08 * h, mat, 'thigh_' + sd, k=0.03 * h)


def pauldrons(m, H, mat, r=0.075, spikes=0):
    for sd, sg in (('L', 1), ('R', -1)):
        on(m, H, 'upperarm_' + sd, kit.pauldron(r * H.h, spikes), mat, (sg * 0.02 * H.h, 0.03 * H.h, 0))


def sword(m, H, side='R', L=0.62, mats=('steel', 'leather_black', 'gold'), curved=0.0, w=0.055):
    bl, gp, gd = kit.blade(L, w, 0.012, 0.15, 0.13, curved)
    hold(m, H, bl, mats[0], side)
    hold(m, H, gp, mats[1], side)
    hold(m, H, gd, mats[2], side)


def body(sc, H, skin='skin', **kw):
    return humanoid_body(sc, H, skin=skin, **kw)


# ====================================================================== Helden

@hero
def lyra():
    m = Hm('lyra', 'Lyra · Turmweberin')
    H = Humanoid(m, 1.0, w=0.88, legs=0.5)
    sc = Sculpt(detail=0.001, dfreq=40, seed=301)
    body(sc, H, 'skin', bulk=0.88, muscle=0.2, gaunt=0.1)
    robe(sc, H, 'cloth_violet', 0.95, 1.1)
    hair(sc, H, 'leather_black', 'long')
    sc.torus(H.p('hips') + V(0, 0.03, 0), 0.13, 0.015, 'gold', 'hips', k=0.005)
    finish(m, sc, 9000, 0.007)
    st, claw, gem = kit.staff(1.15)
    hold(m, H, st, 'wood_black', 'R', fwd=False)
    hold(m, H, claw, 'gold', 'R', fwd=False)
    hold(m, H, gem, 'crystal_violet', 'R', fwd=False)
    hold(m, H, torus(0.08, 0.006, 24, 4).rx(math.pi / 2).t(0, 0.78, 0), 'glow_arcane', 'R', fwd=False)
    on(m, H, 'chest', torus(0.09, 0.012, 20, 5).s(1, 1, 0.6).t(0, 0.08, 0.0), 'gold', (0, 0, 0))
    H.anims('caster')
    return m


@hero
def brann():
    m = Hm('brann', 'Brann · Klingenrufer')
    H = Humanoid(m, 1.05, w=1.12, legs=0.48)
    sc = Sculpt(detail=0.0015, dfreq=35, seed=303)
    body(sc, H, 'skin', bulk=1.12, muscle=1.0)
    plate(sc, H, 'iron')
    beard(sc, H, 'fur', 1.2)
    hair(sc, H, 'fur', 'short')
    sc.ellipsoid(H.p('chest') + V(0, 0.09, -0.06), (0.22, 0.09, 0.12), 'fur', 'chest', k=0.03, noise=0.01)  # Pelzkragen
    finish(m, sc, 10000, 0.007)
    pauldrons(m, H, 'iron_rust', 0.08, 2)
    bl, gp, gd = kit.blade(0.85, 0.075, 0.016, 0.22, 0.2)
    hold(m, H, bl, 'steel', 'R')
    hold(m, H, gp, 'leather_black', 'R')
    hold(m, H, gd, 'iron', 'R')
    cl, _ = kit.banner(0.34, 0.55, 0.03, pole=False)
    on(m, H, 'chest', cl.ry(math.pi), 'cloth_red', (0, 0.12, -0.12))
    H.anims('melee')
    return m


@hero
def vela():
    m = Hm('vela', 'Vela · Sturmschützin')
    H = Humanoid(m, 1.0, w=0.9, legs=0.52)
    sc = Sculpt(detail=0.0012, dfreq=40, seed=305)
    body(sc, H, 'skin', bulk=0.9, muscle=0.4, gaunt=0.2)
    tunic(sc, H, 'leather', 0.18)
    hair(sc, H, 'leather_black', 'pony')
    cloak(sc, H, 'cloth_blue', 0.6, hood=False)
    plate(sc, H, 'leather_black', chest=False, arms=True, legs=True)
    finish(m, sc, 9000, 0.007)
    limbs, string = kit.bow(0.85)
    hold(m, H, limbs, 'wood_black', 'L', fwd=False)
    hold(m, H, string, 'crystal_cyan', 'L', fwd=False)
    quiver = merge([cylinder(0.035, 0.32, 8)] + [cylinder(0.004, 0.12, 4).t(dx, 0.32, 0) for dx in (-0.015, 0, 0.015)])
    on(m, H, 'chest', quiver.rz(-0.4), 'leather', (-0.08, -0.13, -0.11))
    H.anims('ranged')
    return m


@hero
def kaela():
    m = Hm('kaela', 'Kaela · Klingenwächterin')
    H = Humanoid(m, 1.02, w=1.0, legs=0.5)
    sc = Sculpt(detail=0.001, dfreq=40, seed=307)
    body(sc, H, 'skin', bulk=1.0, muscle=0.6)
    plate(sc, H, 'steel')
    tunic(sc, H, 'cloth_blue', 0.3)
    hd = H.p('head')
    sc.ellipsoid(hd + V(0, 0.085, -0.005), (0.072, 0.07, 0.082), 'steel', 'head', k=0.01)  # Helm
    sc.box(hd + V(0, 0.06, 0.07), (0.05, 0.008, 0.03), 'steel', 'head', k=0.005, sub=True)
    sc.capsule(hd + V(0, 0.15, -0.02), hd + V(0, 0.12, -0.18), 0.02, 0.008, 'cloth_red', 'head', k=0.02)  # Helmbusch
    finish(m, sc, 10000, 0.007)
    pauldrons(m, H, 'steel', 0.08)
    sword(m, H, 'R', 0.62)
    face, boss = kit.shield(0.24, 'kite')
    hold(m, H, face, 'steel', 'L', fwd=False, off=(0.04, 0.0, 0.08))
    hold(m, H, crystal(0.03, 0.1, 6).rx(math.pi / 2).t(0, -0.02, 0.02), 'crystal_cyan', 'L', fwd=False, off=(0.04, 0.0, 0.1))
    H.anims('melee')
    return m


@hero
def varo():
    m = Hm('varo', 'Varo · Aschenjäger')
    H = Humanoid(m, 1.0, w=0.95, legs=0.5)
    sc = Sculpt(detail=0.0012, dfreq=40, seed=309)
    body(sc, H, 'skin_dark', bulk=0.95, muscle=0.5)
    tunic(sc, H, 'leather_black', 0.2)
    cloak(sc, H, 'cloth_dark', 0.7, hood=True)
    hd = H.p('head')
    sc.ellipsoid(hd + V(0, 0.08, -0.015), (0.085, 0.085, 0.09), 'cloth_dark', 'head', k=0.02)
    sc.ellipsoid(hd + V(0, 0.07, 0.06), (0.06, 0.065, 0.06), 'cloth_dark', 'head', sub=True, k=0.015)
    sc.box(hd + V(0, 0.035, 0.06), (0.045, 0.025, 0.015), 'iron', 'head', k=0.008, r=0.01)  # Atemmaske
    plate(sc, H, 'leather', chest=False)
    finish(m, sc, 9000, 0.007)
    xb = merge([rbox(0.05, 0.05, 0.42, 0.01).t(0, 0, 0.1), tube(curve([(-0.22, 0, 0.25), (0, 0, 0.32), (0.22, 0, 0.25)], 10), [0.01, 0.018, 0.01], 6)])
    hold(m, H, xb, 'wood_black', 'R', fwd=False, off=(0, 0.02, 0.0))
    hold(m, H, cylinder(0.008, 0.3, 5).rx(math.pi / 2).t(0, 0.035, 0.05), 'glow_fire', 'R', fwd=False)
    on(m, H, 'hips', merge([cylinder(0.025, 0.08, 8).t(x, 0, 0) for x in (-0.06, 0.06)]), 'ember', (0, -0.04, 0.12))
    H.anims('ranged')
    return m


@hero
def nyx():
    m = Hm('nyx', 'Nyx · Risswandler')
    H = Humanoid(m, 1.0, w=0.92, legs=0.52, hunch=0.15)
    sc = Sculpt(detail=0.0012, dfreq=40, seed=311)
    body(sc, H, 'skin_pale', bulk=0.92, muscle=0.5, gaunt=0.3, horns=('obsidian', 0.6, 1.0))
    tunic(sc, H, 'cloth_violet', 0.25)
    plate(sc, H, 'obsidian', chest=False)
    sc.box(H.p('head') + V(0, 0.04, 0.065), (0.05, 0.022, 0.012), 'obsidian', 'head', k=0.008, r=0.01)  # Maske
    hair(sc, H, 'cloth_dark', 'short')
    finish(m, sc, 9000, 0.007)
    for sd in 'LR':
        bl, gp, gd = kit.blade(0.32, 0.04, 0.01, 0.07, 0.1, curved=0.5)
        hold(m, H, bl, 'void', sd)
        hold(m, H, gp, 'leather_black', sd)
        hold(m, H, gd, 'crystal_violet', sd)
    on(m, H, 'hips', torus(0.15, 0.006, 30, 4), 'glow_arcane', (0, 0.0, 0))
    H.anims('melee', speed=1.2)
    return m


@hero
def liora():
    m = Hm('liora', 'Liora · Runenbardin')
    H = Humanoid(m, 0.98, w=0.88, legs=0.5)
    sc = Sculpt(detail=0.001, dfreq=40, seed=313)
    body(sc, H, 'skin', bulk=0.88, muscle=0.2)
    tunic(sc, H, 'cloth_red', 0.32)
    hair(sc, H, 'gold', 'long')
    hd = H.p('head')
    sc.ellipsoid(hd + V(0, 0.13, 0), (0.13, 0.02, 0.12), 'leather', 'head', k=0.01)  # Hutkrempe
    sc.ellipsoid(hd + V(0, 0.16, -0.01), (0.065, 0.05, 0.07), 'leather', 'head', k=0.015)
    sc.capsule(hd + V(0.05, 0.17, -0.02), hd + V(0.13, 0.28, -0.12), 0.012, 0.002, 'cloth_red', 'head', k=0.005)  # Feder
    finish(m, sc, 9000, 0.007)
    lute = merge([sphere(0.11, 0.13, 0.05, 14).t(0, 0, 0), rbox(0.04, 0.32, 0.02, 0.005).t(0, 0.25, 0), rbox(0.07, 0.06, 0.03, 0.01).t(0, 0.43, 0)])
    hold(m, H, lute.rz(0.6), 'wood', 'L', fwd=False, off=(-0.05, 0.08, 0.1))
    hold(m, H, sphere(0.05, 0.06, 0.02, 10).rz(0.6), 'runes', 'L', fwd=False, off=(-0.05, 0.08, 0.13))
    orb = merge([crystal(0.02, 0.07, 4).t(math.cos(a) * 0.25, 0.0, math.sin(a) * 0.25) for a in np.linspace(0, TAU, 5)[:-1]])
    on(m, H, 'chest', orb, 'crystal_gold', (0, 0.05, 0))
    H.anims('caster')
    return m


@hero
def thorgrim():
    m = Hm('thorgrim', 'Thorgrim · Schmiedemeister')
    H = Humanoid(m, 0.82, w=1.45, legs=0.4, arm=1.05)
    sc = Sculpt(detail=0.0015, dfreq=35, seed=315)
    body(sc, H, 'skin', bulk=1.45, muscle=1.0, belly=0.8, head_size=1.15)
    beard(sc, H, 'fur', 2.2)
    sc.box(H.p('spine') + V(0, -0.03, 0.08), (0.12, 0.2, 0.03), 'leather', 'spine', k=0.02, r=0.01)  # Schürze
    plate(sc, H, 'iron', chest=False)
    hd = H.p('head')
    sc.ellipsoid(hd + V(0, 0.1, 0), (0.08, 0.05, 0.085), 'iron', 'head', k=0.01)
    finish(m, sc, 10000, 0.0065)
    pauldrons(m, H, 'iron', 0.1)
    hammer = merge([cylinder(0.018, 0.5, 8).t(0, -0.15, 0), rbox(0.24, 0.12, 0.12, 0.02).t(0, 0.36, 0)])
    hold(m, H, hammer, 'iron', 'R')
    hold(m, H, lathe_tongs(), 'steel', 'L')
    on(m, H, 'chest', kit.crystal_cluster(1, 0.03, 0.08, 0.0).rx(math.pi / 2), 'glow_fire', (0, 0.0, 0.14))
    H.anims('melee', heavy=True)
    return m


def lathe_tongs():
    return merge([tube(curve([(0, -0.1, 0), (s * 0.02, 0.1, 0), (s * 0.03, 0.22, 0)], 8), 0.008, 5) for s in (1, -1)])


@hero
def sena():
    m = Hm('sena', 'Sena · Wettermacherin')
    H = Humanoid(m, 1.0, w=0.86, legs=0.5)
    sc = Sculpt(detail=0.0012, dfreq=40, seed=317)
    body(sc, H, 'skin_pale', bulk=0.86, gaunt=0.3, muscle=0.2)
    robe(sc, H, 'cloth_blue', 0.9, 1.15)
    hair(sc, H, 'fur_grey', 'long')
    hd = H.p('head')
    sc.ellipsoid(hd + V(0, 0.135, 0), (0.17, 0.018, 0.17), 'cloth_dark', 'head', k=0.01)  # Hexenhut
    sc.capsule(hd + V(0, 0.14, 0), hd + V(-0.03, 0.42, -0.1), 0.07, 0.005, 'cloth_dark', 'head', k=0.03)
    finish(m, sc, 9000, 0.007)
    st, claw, gem = kit.staff(1.2)
    hold(m, H, st, 'wood_black', 'R', fwd=False)
    hold(m, H, claw, 'steel', 'R', fwd=False)
    hold(m, H, sphere(0.06, seg=12).t(0, 0.83, 0), 'glow_storm', 'R', fwd=False)
    hold(m, H, merge([torus(0.1, 0.006, 20, 4).rx(a).t(0, 0.83, 0) for a in (0.4, 1.6)]), 'crystal_cyan', 'R', fwd=False)
    H.anims('caster')
    return m


@hero
def oran():
    m = Hm('oran', 'Oran · Leylinien-Mystiker')
    H = Humanoid(m, 1.0, w=1.0, legs=0.5)
    sc = Sculpt(detail=0.001, dfreq=40, seed=319)
    body(sc, H, 'skin_dark', bulk=1.0, muscle=0.5)
    robe(sc, H, 'cloth_green', 0.9, 1.0)
    sc.ellipsoid(H.p('chest') + V(0.05, 0.03, 0.0), (0.12, 0.16, 0.13), 'cloth_red', 'chest', k=0.02, R=rot(0, 0, 30))  # Schärpe
    beard(sc, H, 'fur_grey', 0.8)
    finish(m, sc, 9000, 0.007)
    beads = merge([sphere(0.014, seg=6).t(math.cos(a) * 0.11, -math.sin(a) * 0.14 + 0.02, 0.1 + 0.02 * math.sin(a)) for a in np.linspace(0.2, math.pi - 0.2, 11)])
    on(m, H, 'chest', beads, 'wood', (0, 0.06, 0))
    for i in range(3):
        a = TAU * i / 3
        on(m, H, 'chest', kit.rune_stone(0.05, 0.07, 0.025, seed=i).t(0, -0.035, 0).t(math.cos(a) * 0.3, 0.25, math.sin(a) * 0.3), 'runes_cyan', (0, 0, 0))
    st, claw, gem = kit.staff(1.1)
    hold(m, H, st, 'wood', 'R', fwd=False)
    hold(m, H, gem, 'crystal_cyan', 'R', fwd=False)
    H.anims('caster')
    return m


@hero
def mira():
    m = Hm('mira', 'Mira · Prospektorin')
    H = Humanoid(m, 0.95, w=0.95, legs=0.48)
    sc = Sculpt(detail=0.0012, dfreq=40, seed=321)
    body(sc, H, 'skin', bulk=0.95, muscle=0.4)
    tunic(sc, H, 'leather', 0.15)
    plate(sc, H, 'leather_black', chest=False)
    hair(sc, H, 'fur', 'bun')
    hd = H.p('head')
    sc.ellipsoid(hd + V(0, 0.1, 0.0), (0.075, 0.055, 0.085), 'iron_rust', 'head', k=0.01)  # Grubenhelm
    sc.ellipsoid(hd + V(0, 0.08, 0.0), (0.11, 0.012, 0.11), 'iron_rust', 'head', k=0.008)
    for sg in (1, -1):
        sc.torus(hd + V(sg * 0.026, 0.075, 0.07), 0.017, 0.006, 'gold', 'head', k=0.004, R=rot(0, 90, 0))
    finish(m, sc, 9000, 0.007)
    on(m, H, 'head', sphere(0.022, seg=8), 'glow_white', (0, 0.12, 0.08))
    pick = merge([cylinder(0.014, 0.6, 8).t(0, -0.2, 0), tube(curve([(-0.17, 0.38, 0), (0, 0.42, 0), (0.17, 0.38, 0)], 10), [0.006, 0.022, 0.006], 6)])
    hold(m, H, pick, 'iron', 'R')
    lantern = merge([cylinder(0.035, 0.07, 6).t(0, -0.1, 0)])
    hold(m, H, lantern, 'gold', 'L', fwd=False)
    hold(m, H, sphere(0.025, seg=8).t(0, -0.065, 0), 'glow_fire', 'L', fwd=False)
    pack = rbox(0.18, 0.22, 0.1, 0.03)
    on(m, H, 'chest', pack, 'leather', (0, -0.03, -0.14))
    on(m, H, 'chest', kit.crystal_cluster(3, 0.025, 0.08, 0.03, seed=5), 'crystal_gold', (0, 0.09, -0.14))
    H.anims('melee')
    return m


@hero
def ommar():
    m = Hm('ommar', 'Ommar · Wächter der Stille')
    H = Humanoid(m, 1.1, w=0.95, legs=0.5)
    sc = Sculpt(detail=0.0012, dfreq=40, seed=323)
    body(sc, H, 'skin_pale', bulk=0.95, gaunt=0.4, muscle=0.3)
    robe(sc, H, 'cloth_dark', 0.98, 1.05)
    hd = H.p('head')
    sc.ellipsoid(hd + V(0, 0.075, -0.0), (0.085, 0.095, 0.09), 'cloth_dark', 'head', k=0.02)  # verhüllter Kopf
    sc.box(hd + V(0, 0.075, 0.07), (0.045, 0.012, 0.03), 'cloth_dark', 'head', sub=True, k=0.006)  # Sehschlitz
    finish(m, sc, 9000, 0.007)
    on(m, H, 'head', merge([sphere(0.012, seg=6).t(s * 0.022, 0.075, 0.075) for s in (1, -1)]), 'glow_frost', (0, 0, 0))
    on(m, H, 'chest', kit.chain((0.12, 0.08, 0.1), (-0.12, -0.1, 0.11), 8, 0.02, 0.014), 'iron', (0, 0, 0))
    scythe = merge([cylinder(0.015, 1.25, 8).t(0, -0.45, 0), tube(curve([(0, 0.78, 0), (0.08, 0.82, 0.12), (0.06, 0.74, 0.32)], 12), [0.02, 0.016, 0.002], 6)])
    hold(m, H, scythe, 'iron', 'R', fwd=False)
    bell = merge([kit.chain((0, 0, 0), (0, -0.12, 0), 3, 0, 0.01)])
    hold(m, H, bell, 'iron', 'L', fwd=False)
    hold(m, H, sphere(0.04, 0.05, 0.04, 10).t(0, -0.16, 0), 'gold', 'L', fwd=False)
    H.anims('melee', speed=0.9)
    return m
