"""Helden, Version 2: definierte Anatomie + harte Rüstung + Stoffbahnen."""
from __future__ import annotations

import math

import numpy as np

from . import kit
from .anatomy2 import (V, align, beard_locks, belt, body2, boot, bracer, cape_back, cloth_panel, cuirass, fauld, greave, hair_locks,
                       helmet, pauldron2, robe_skirt, shell, tabard)
from .enemies import hold
from . import garments as G
from .geo import TAU, crystal, curve, cylinder, lathe, merge, rbox, sphere, spike, torus, tube
from .model import Model
from .rig import Humanoid
from .sdf import Sculpt, rot

REG = {}


def hero(fn):
    REG[fn.__name__] = fn
    return fn


def Hm(name, title):
    return Model(name, 'hero', title, skinned=True)


def on(m, bone, mesh, mat, blend=()):
    m.add(mesh, mat, bone, blend)


def shoulders(m, H, mat, trim='gold', r=0.075, layers=3, spikes=0):
    for sd, sg in (('L', 1), ('R', -1)):
        pd, rim = pauldron2(r * H.h, layers, spikes)
        for mm in (pd, rim):
            if mm is None:
                continue
            mm.rz(-sg * 0.55).t(*(H.p('upperarm_' + sd) + V(sg * 0.025 * H.h, 0.02 * H.h, 0)))
        on(m, 'upperarm_' + sd, pd, mat)
        if rim is not None:
            on(m, 'upperarm_' + sd, rim, trim)


def limbs_armor(m, H, mat, bracers=True, greaves=True, boots='leather_black'):
    for sd in 'LR':
        if bracers:
            on(m, 'forearm_' + sd, bracer(H, sd), mat)
        if greaves:
            on(m, 'shin_' + sd, greave(H, sd), mat)
        if boots:
            on(m, 'foot_' + sd, boot(H, sd), boots, ('shin_' + sd,))


def skirt_plates(m, H, mat):
    for k, mesh in fauld(H).items():
        on(m, 'thigh_' + k if k in 'LR' else 'hips', mesh, mat, ('hips',) if k in 'LR' else ())


def sculpt_body(m, sc, target=8000, voxel=0.005):
    m.sculpt(sc, voxel=voxel, max_dim=260, target=target)


def dress(m, layers, voxel=0.0045, target=1600):
    from .sdf import build_cache
    for L in layers:
        if isinstance(L, list):
            dress(m, L, voxel, target)
            continue
        if getattr(L.base, '_cache', None) is None:
            build_cache(L.base, 0.004)
        m.sculpt(L, voxel=voxel, max_dim=260, target=target)


@hero
def brann():
    m = Hm('brann', 'Brann · Klingenrufer')
    H = Humanoid(m, 1.05, w=1.1, legs=0.49)
    sc = Sculpt(detail=0.0006, dfreq=60, seed=503)
    body2(sc, H, 'skin', build='athletic', head='human', eye='skin')
    sculpt_body(m, sc, 7000)
    dress(m, [G.pants(sc, H, 'leather_black'), G.tunic(sc, H, 'cloth_red', hem=0.2), G.cuirass(sc, H, 'steel'), G.pauldrons(sc, H, 'iron'),
              G.greaves(sc, H, 'iron'), G.boots(sc, H, 'leather_black'), G.bracers(sc, H, 'leather'), G.gloves(sc, H, 'leather_black')])
    on(m, 'hips', belt(H, 0.13 * H.h), 'leather_black')
    on(m, 'head', hair_locks(H, 9, 0.12, seed=3), 'fur')
    on(m, 'head', beard_locks(H, 7, 0.09), 'fur')
    on(m, 'chest', cape_back(H, 0.68, 0.34, 0.3), 'cloth_red', ('spine', 'hips'))
    bl, gp, gd = kit.blade(0.85, 0.07, 0.016, 0.24, 0.2)
    hold(m, H, bl, 'steel', 'R'); hold(m, H, gp, 'leather_black', 'R'); hold(m, H, gd, 'gold', 'R')
    H.anims('melee')
    return m


# ---------------------------------------------------------------------- Zubehör

def hat_witch(H, mat_r=1.0):
    h = H.h
    hd = H.p('head') + V(0, 0.12 * h, 0)
    brim = lathe([(0.04 * h, 0.006 * h), (0.16 * h, 0.0), (0.17 * h, -0.006 * h), (0.16 * h, -0.01 * h), (0.04 * h, -0.004 * h)], 28, cap_bottom=False, cap_top=False, smooth_all=True)
    cone = tube(curve([(0, 0, 0), (0, 0.12 * h, -0.01 * h), (-0.02 * h, 0.24 * h, -0.05 * h), (-0.06 * h, 0.3 * h, -0.11 * h)], 16), [0.075 * h, 0.05 * h, 0.025 * h, 0.003 * h], 14)
    band = torus(0.072 * h, 0.008 * h, 24, 4).t(0, 0.012 * h, 0)
    return merge([brim, cone]).t(*hd), band.t(*hd)


def hat_bard(H):
    h = H.h
    hd = H.p('head') + V(0, 0.115 * h, 0)
    brim = lathe([(0.05 * h, 0.004 * h), (0.12 * h, 0.012 * h), (0.13 * h, 0.006 * h), (0.12 * h, 0.0), (0.05 * h, -0.004 * h)], 24, cap_bottom=False, cap_top=False, smooth_all=True)
    crown = lathe([(0.068 * h, 0.0), (0.064 * h, 0.05 * h), (0.04 * h, 0.07 * h), (0.0, 0.072 * h)], 20, cap_bottom=False)
    feather = tube(curve([(0.05 * h, 0.03 * h, -0.02 * h), (0.11 * h, 0.12 * h, -0.08 * h), (0.14 * h, 0.2 * h, -0.17 * h)], 12), [0.012 * h, 0.016 * h, 0.002 * h], 6)
    return merge([brim, crown]).t(*hd), feather.t(*hd)


def mining_helmet(H):
    h = H.h
    hd = H.p('head') + V(0, 0.08 * h, -0.005 * h)
    dome = shell(0.074 * h, 0.07 * h, 0.08 * h, -0.05, 1.0, 20, 8, 0.005 * h)
    brim = lathe([(0.074 * h, 0), (0.1 * h, -0.006 * h), (0.098 * h, -0.012 * h), (0.072 * h, -0.006 * h)], 24, cap_bottom=False, cap_top=False)
    lamp = cylinder(0.018 * h, 0.03 * h, 10).rx(math.pi / 2).t(0, 0.035 * h, 0.07 * h)
    return merge([dome, brim, lamp]).t(*hd), sphere(0.014 * h, seg=8).t(*(hd + V(0, 0.035 * H.h, 0.1 * H.h)))


def ponytail(H, mat_r=1.0):
    h = H.h
    hd = H.p('head')
    return tube(curve([hd + V(0, 0.12 * h, -0.06 * h), hd + V(0, 0.08 * h, -0.12 * h), hd + V(0, -0.02 * h, -0.13 * h), hd + V(0, -0.1 * h, -0.1 * h)], 14), [0.022 * h, 0.02 * h, 0.014 * h, 0.002 * h], 8)


def staff_hold(m, H, L=1.15, head='crystal_violet', shaft='wood_black', claw='gold', orb=None):
    st, cl, gem = kit.staff(L)
    hold(m, H, st, shaft, 'R', fwd=False)
    hold(m, H, cl, claw, 'R', fwd=False)
    if orb:
        hold(m, H, sphere(0.055, seg=14).t(0, L * 0.68, 0), orb, 'R', fwd=False)
    else:
        hold(m, H, gem, head, 'R', fwd=False)


# ---------------------------------------------------------------------- Helden

@hero
def lyra():
    m = Hm('lyra', 'Lyra · Turmweberin')
    H = Humanoid(m, 1.0, w=0.86, legs=0.5)
    sc = Sculpt(detail=0.0005, dfreq=60, seed=601)
    body2(sc, H, 'skin', build='lean', female=True, eye='skin')
    sculpt_body(m, sc)
    dress(m, [G.tunic(sc, H, 'cloth_violet', hem=0.1, sleeves=True), G.sleeves(sc, H, 'cloth_violet', 0.009), G.gloves(sc, H, 'leather_black'), G.boots(sc, H, 'leather_black')])
    on(m, 'hips', robe_skirt(H, 0.95, 0.13 * H.h, 1.7, 16), 'cloth_violet', ('thigh_L', 'thigh_R'))
    on(m, 'hips', belt(H, 0.12 * H.h), 'gold')
    on(m, 'head', hair_locks(H, 11, 0.22, seed=5), 'leather_black')
    on(m, 'head', torus(0.06 * H.h, 0.004 * H.h, 24, 4).rx(0.15).t(*(H.p('head') + V(0, 0.1 * H.h, 0.0))), 'gold')
    on(m, 'chest', cape_back(H, 0.75, 0.28, 0.0, 8), 'cloth_dark', ('spine', 'hips'))
    staff_hold(m, H, 1.2, 'crystal_violet')
    hold(m, H, torus(0.08, 0.006, 24, 4).rx(math.pi / 2).t(0, 0.83, 0), 'glow_arcane', 'R', fwd=False)
    H.anims('caster')
    return m


@hero
def vela():
    m = Hm('vela', 'Vela · Sturmschützin')
    H = Humanoid(m, 1.0, w=0.9, legs=0.52)
    sc = Sculpt(detail=0.0005, dfreq=60, seed=603)
    body2(sc, H, 'skin', build='athletic', female=True, eye='skin')
    sculpt_body(m, sc)
    dress(m, [G.pants(sc, H, 'leather_black', to=0.5), G.tunic(sc, H, 'leather', hem=0.12), G.bracers(sc, H, 'leather_black'), G.boots(sc, H, 'leather', height=0.16),
              G.hood(sc, H, 'cloth_blue', 0.018)])
    on(m, 'chest', cape_back(H, 0.55, 0.3, 0.35), 'cloth_blue', ('spine',))
    on(m, 'head', ponytail(H), 'leather_black')
    on(m, 'hips', belt(H, 0.125 * H.h), 'leather_black')
    limbs, string = kit.bow(0.9)
    hold(m, H, limbs, 'wood_black', 'L', fwd=False)
    hold(m, H, string, 'crystal_cyan', 'L', fwd=False)
    q = merge([cylinder(0.034, 0.32, 10)] + [cylinder(0.004, 0.13, 4).t(dx, 0.32, 0) for dx in (-0.015, 0, 0.015)] + [crystal(0.008, 0.03, 4).t(dx, 0.45, 0) for dx in (-0.015, 0, 0.015)])
    on(m, 'chest', q.rz(-0.45).t(*(H.p('chest') + V(-0.08, -0.12, -0.12))), 'leather')
    H.anims('ranged')
    return m


@hero
def kaela():
    m = Hm('kaela', 'Kaela · Klingenwächterin')
    H = Humanoid(m, 1.02, w=0.98, legs=0.5)
    sc = Sculpt(detail=0.0005, dfreq=60, seed=605)
    body2(sc, H, 'skin', build='athletic', female=True, eye='skin')
    sculpt_body(m, sc)
    dress(m, [G.pants(sc, H, 'iron', 0.008, to=0.2), G.cuirass(sc, H, 'steel'), G.pauldrons(sc, H, 'steel', 0.026, 3), G.greaves(sc, H, 'steel'),
              G.bracers(sc, H, 'steel', 0.012, hands=True), G.boots(sc, H, 'steel', 0.016, 0.06), G.helm(sc, H, 'steel', 0.016, 'open')])
    on(m, 'hips', tabard(H, 0.36, 0.15), 'cloth_blue', ('thigh_L', 'thigh_R'))
    on(m, 'hips', belt(H, 0.135 * H.h), 'gold')
    on(m, 'head', tube(curve([H.p('head') + V(0, 0.19 * H.h, 0.03 * H.h), H.p('head') + V(0, 0.22 * H.h, -0.06 * H.h), H.p('head') + V(0, 0.12 * H.h, -0.2 * H.h)], 14), [0.018 * H.h, 0.022 * H.h, 0.004 * H.h], 8), 'cloth_red')
    on(m, 'chest', cape_back(H, 0.7, 0.32, 0.1), 'cloth_blue', ('spine', 'hips'))
    bl, gp, gd = kit.blade(0.62, 0.055, 0.012, 0.16, 0.13)
    hold(m, H, bl, 'steel', 'R'); hold(m, H, gp, 'leather_black', 'R'); hold(m, H, gd, 'gold', 'R')
    face, _ = kit.shield(0.24, 'kite')
    hold(m, H, face, 'steel', 'L', fwd=False, off=(0.045, 0.0, 0.08))
    hold(m, H, crystal(0.03, 0.1, 6).rx(math.pi / 2).t(0, -0.02, 0.02), 'crystal_cyan', 'L', fwd=False, off=(0.045, 0.0, 0.1))
    H.anims('melee')
    return m


@hero
def varo():
    m = Hm('varo', 'Varo · Aschenjäger')
    H = Humanoid(m, 1.0, w=0.95, legs=0.5)
    sc = Sculpt(detail=0.0005, dfreq=60, seed=607)
    body2(sc, H, 'skin_dark', build='lean', eye='glow_fire')
    sculpt_body(m, sc)
    dress(m, [G.pants(sc, H, 'leather_black'), G.tunic(sc, H, 'leather', hem=0.16), G.bracers(sc, H, 'leather_black'), G.gloves(sc, H, 'leather_black'),
              G.boots(sc, H, 'leather_black', height=0.15), G.hood(sc, H, 'cloth_dark', 0.02)])
    on(m, 'head', rbox(0.06 * H.h, 0.032 * H.h, 0.02 * H.h, 0.008 * H.h).t(*(H.p('head') + V(0, 0.038 * H.h, 0.075 * H.h))), 'iron')
    on(m, 'chest', cape_back(H, 0.72, 0.34, 0.5), 'cloth_dark', ('spine', 'hips'))
    on(m, 'hips', belt(H, 0.12 * H.h), 'leather_black')
    xb = merge([rbox(0.05, 0.05, 0.42, 0.01).t(0, 0, 0.1), tube(curve([(-0.22, 0, 0.25), (0, 0, 0.32), (0.22, 0, 0.25)], 10), [0.01, 0.018, 0.01], 6)])
    hold(m, H, xb, 'wood_black', 'R', fwd=False, off=(0, 0.02, 0.0))
    hold(m, H, cylinder(0.008, 0.3, 5).rx(math.pi / 2).t(0, 0.035, 0.05), 'glow_fire', 'R', fwd=False)
    on(m, 'hips', merge([cylinder(0.022, 0.07, 8).t(x, 0, 0) for x in (-0.06, 0.0, 0.06)]).t(*(H.p('hips') + V(0, -0.02, 0.12))), 'ember')
    H.anims('ranged')
    return m


@hero
def nyx():
    m = Hm('nyx', 'Nyx · Risswandler')
    H = Humanoid(m, 1.0, w=0.92, legs=0.53, hunch=0.15)
    sc = Sculpt(detail=0.0005, dfreq=60, seed=609)
    body2(sc, H, 'skin_pale', build='lean', eye='glow_arcane', horns=('obsidian', 0.7, 1.2))
    sculpt_body(m, sc)
    dress(m, [G.pants(sc, H, 'cloth_violet', 0.005, to=0.8), G.tunic(sc, H, 'cloth_violet', 0.006, hem=0.05, sleeves=True), G.sleeves(sc, H, 'cloth_violet', 0.005),
              G.gloves(sc, H, 'leather_black', 0.004), G.boots(sc, H, 'obsidian', 0.01, 0.12),
              G.wrap(sc, H, 'obsidian', 0.012, [G.R('ellipsoid', c=H.p('chest') + V(0, 0.0, 0.02), r=(0.16, 0.08, 0.12))], grooves=[G.R('box', c=H.p('chest') + V(0, 0.0, 0), h=(0.2, 0.003, 0.2))]),
              G.wrap(sc, H, 'obsidian', 0.008, [G.R('box', c=H.p('head') + V(0, 0.04, 0.06), h=(0.06, 0.022, 0.04))])])
    on(m, 'hips', belt(H, 0.115 * H.h), 'leather_black')
    on(m, 'chest', cape_back(H, 0.5, 0.22, 0.6, 4), 'cloth_dark', ('spine',))
    for sd in 'LR':
        bl, gp, gd = kit.blade(0.32, 0.04, 0.01, 0.07, 0.1, curved=0.5)
        hold(m, H, bl, 'void', sd); hold(m, H, gp, 'leather_black', sd); hold(m, H, gd, 'crystal_violet', sd)
    on(m, 'hips', torus(0.16, 0.005, 36, 4).t(*H.p('hips')), 'glow_arcane')
    H.anims('melee', speed=1.2)
    return m


@hero
def liora():
    m = Hm('liora', 'Liora · Runenbardin')
    H = Humanoid(m, 0.98, w=0.88, legs=0.5)
    sc = Sculpt(detail=0.0005, dfreq=60, seed=611)
    body2(sc, H, 'skin', build='lean', female=True, eye='skin')
    sculpt_body(m, sc)
    dress(m, [G.pants(sc, H, 'leather_black', to=0.6), G.tunic(sc, H, 'cloth_red', hem=0.22, sleeves=True), G.sleeves(sc, H, 'cloth_red', 0.009, 'elbow'),
              G.bracers(sc, H, 'leather'), G.boots(sc, H, 'leather', height=0.18)])
    on(m, 'hips', belt(H, 0.125 * H.h), 'gold')
    on(m, 'head', hair_locks(H, 11, 0.2, seed=7), 'gold')
    hat, feather = hat_bard(H)
    on(m, 'head', hat, 'leather'); on(m, 'head', feather, 'cloth_red')
    lute = merge([sphere(0.11, 0.13, 0.05, 16), rbox(0.04, 0.32, 0.02, 0.005).t(0, 0.25, 0), rbox(0.07, 0.06, 0.03, 0.01).t(0, 0.43, 0)])
    hold(m, H, lute.rz(0.6), 'wood', 'L', fwd=False, off=(-0.05, 0.08, 0.1))
    hold(m, H, sphere(0.05, 0.06, 0.02, 12).rz(0.6), 'runes', 'L', fwd=False, off=(-0.05, 0.08, 0.13))
    orb = merge([crystal(0.02, 0.07, 4).t(math.cos(a) * 0.25, 0.0, math.sin(a) * 0.25) for a in np.linspace(0, TAU, 5)[:-1]])
    on(m, 'chest', orb.t(*(H.p('chest') + V(0, 0.05, 0))), 'crystal_gold')
    H.anims('caster')
    return m


@hero
def thorgrim():
    m = Hm('thorgrim', 'Thorgrim · Schmiedemeister')
    H = Humanoid(m, 0.82, w=1.4, legs=0.42, arm=1.05)
    sc = Sculpt(detail=0.0005, dfreq=60, seed=613)
    body2(sc, H, 'skin', build='athletic', eye='skin', head_scale=1.12)
    sculpt_body(m, sc)
    dress(m, [G.pants(sc, H, 'leather_black'), G.tunic(sc, H, 'leather', 0.012, hem=0.26), G.gloves(sc, H, 'leather_black', 0.006), G.bracers(sc, H, 'iron', 0.012),
              G.boots(sc, H, 'iron', 0.016, 0.1), G.pauldrons(sc, H, 'iron', 0.03, 2), G.helm(sc, H, 'iron', 0.014, 'open')])
    on(m, 'head', beard_locks(H, 9, 0.2, 1.4), 'fur')
    on(m, 'hips', belt(H, 0.15 * H.h), 'leather_black')
    hammer = merge([cylinder(0.02, 0.5, 8).t(0, -0.15, 0), rbox(0.26, 0.13, 0.13, 0.02).t(0, 0.36, 0)])
    hold(m, H, hammer, 'iron', 'R')
    hold(m, H, torus(0.07, 0.012, 16, 5).rx(math.pi / 2).t(0, 0.36, 0), 'gold', 'R')
    tongs = merge([tube(curve([(0, -0.1, 0), (s * 0.02, 0.1, 0), (s * 0.03, 0.22, 0)], 8), 0.008, 5) for s in (1, -1)])
    hold(m, H, tongs, 'steel', 'L')
    on(m, 'chest', kit.crystal_cluster(1, 0.025, 0.07, 0.0).rx(math.pi / 2).t(*(H.p('chest') + V(0, 0, 0.15))), 'glow_fire')
    H.anims('melee', heavy=True)
    return m


@hero
def sena():
    m = Hm('sena', 'Sena · Wettermacherin')
    H = Humanoid(m, 1.0, w=0.86, legs=0.5)
    sc = Sculpt(detail=0.0005, dfreq=60, seed=615)
    body2(sc, H, 'skin_pale', build='lean', female=True, eye='glow_frost')
    sculpt_body(m, sc)
    dress(m, [G.tunic(sc, H, 'cloth_blue', hem=0.1, sleeves=True), G.sleeves(sc, H, 'cloth_blue', 0.01), G.boots(sc, H, 'leather_black')])
    on(m, 'hips', robe_skirt(H, 0.95, 0.13 * H.h, 1.75, 18), 'cloth_blue', ('thigh_L', 'thigh_R'))
    on(m, 'hips', belt(H, 0.12 * H.h), 'steel')
    on(m, 'head', hair_locks(H, 11, 0.24, seed=9), 'fur_grey')
    hat, band = hat_witch(H)
    on(m, 'head', hat, 'cloth_dark'); on(m, 'head', band, 'steel')
    on(m, 'chest', cape_back(H, 0.8, 0.34, 0.45, 8), 'cloth_dark', ('spine', 'hips'))
    staff_hold(m, H, 1.25, orb='glow_storm', claw='steel')
    hold(m, H, merge([torus(0.1, 0.006, 24, 4).rx(a).t(0, 0.85, 0) for a in (0.4, 1.6)]), 'crystal_cyan', 'R', fwd=False)
    H.anims('caster')
    return m


@hero
def oran():
    m = Hm('oran', 'Oran · Leylinien-Mystiker')
    H = Humanoid(m, 1.0, w=1.0, legs=0.5)
    sc = Sculpt(detail=0.0005, dfreq=60, seed=617)
    body2(sc, H, 'skin_dark', build='athletic', eye='glow_frost')
    sculpt_body(m, sc)
    dress(m, [G.tunic(sc, H, 'cloth_green', hem=0.1), G.bracers(sc, H, 'cloth_red', 0.008), G.boots(sc, H, 'leather', 0.01, 0.04),
              G.wrap(sc, H, 'cloth_red', 0.016, [G.R('capsule', a=H.p('upperarm_L') + V(0.0, 0.03, 0), b=H.p('hips') + V(-0.12, -0.02, 0), r1=0.05, r2=0.05)])])
    on(m, 'hips', robe_skirt(H, 0.9, 0.125 * H.h, 1.5, 14), 'cloth_green', ('thigh_L', 'thigh_R'))
    on(m, 'hips', belt(H, 0.125 * H.h, False), 'cloth_red')
    on(m, 'head', beard_locks(H, 5, 0.07, 0.9), 'fur_grey')
    beads = merge([sphere(0.013, seg=8).t(math.cos(a) * 0.11, -math.sin(a) * 0.14 + 0.02, 0.1 + 0.02 * math.sin(a)) for a in np.linspace(0.2, math.pi - 0.2, 11)])
    on(m, 'chest', beads.t(*(H.p('chest') + V(0, 0.06, 0))), 'wood')
    for i in range(3):
        a = TAU * i / 3
        on(m, 'chest', kit.rune_stone(0.05, 0.07, 0.025, seed=i).t(0, -0.035, 0).t(*(H.p('chest') + V(math.cos(a) * 0.3, 0.25, math.sin(a) * 0.3))), 'runes_cyan')
    staff_hold(m, H, 1.15, 'crystal_cyan', shaft='wood', claw='gold')
    H.anims('caster')
    return m


@hero
def mira():
    m = Hm('mira', 'Mira · Prospektorin')
    H = Humanoid(m, 0.95, w=0.95, legs=0.48)
    sc = Sculpt(detail=0.0005, dfreq=60, seed=619)
    body2(sc, H, 'skin', build='athletic', female=True, eye='skin')
    sculpt_body(m, sc)
    dress(m, [G.pants(sc, H, 'cloth_dark', to=0.6), G.tunic(sc, H, 'leather', hem=0.08), G.sleeves(sc, H, 'cloth_dark', 0.008, 'elbow'), G.gloves(sc, H, 'leather_black'),
              G.boots(sc, H, 'leather_black', 0.014, 0.16)])
    on(m, 'head', ponytail(H), 'fur')
    hm, lamp = mining_helmet(H)
    on(m, 'head', hm, 'iron_rust'); on(m, 'head', lamp, 'glow_white')
    for sg in (1, -1):
        on(m, 'head', torus(0.016 * H.h, 0.005 * H.h, 14, 4).rx(math.pi / 2).t(*(H.p('head') + V(sg * 0.024 * H.h, 0.074 * H.h, 0.07 * H.h))), 'gold')
    on(m, 'hips', belt(H, 0.13 * H.h), 'leather_black')
    pick = merge([cylinder(0.014, 0.6, 8).t(0, -0.2, 0), tube(curve([(-0.17, 0.38, 0), (0, 0.42, 0), (0.17, 0.38, 0)], 10), [0.006, 0.022, 0.006], 6)])
    hold(m, H, pick, 'iron', 'R')
    hold(m, H, merge([cylinder(0.035, 0.07, 8).t(0, -0.1, 0), torus(0.02, 0.004, 10, 4).t(0, -0.02, 0)]), 'gold', 'L', fwd=False)
    hold(m, H, sphere(0.025, seg=8).t(0, -0.065, 0), 'glow_fire', 'L', fwd=False)
    on(m, 'chest', rbox(0.18, 0.22, 0.1, 0.03).t(*(H.p('chest') + V(0, -0.03, -0.15))), 'leather')
    on(m, 'chest', kit.crystal_cluster(3, 0.025, 0.08, 0.03, seed=5).t(*(H.p('chest') + V(0, 0.09, -0.15))), 'crystal_gold')
    H.anims('melee')
    return m


@hero
def ommar():
    m = Hm('ommar', 'Ommar · Wächter der Stille')
    H = Humanoid(m, 1.12, w=0.95, legs=0.52)
    sc = Sculpt(detail=0.0005, dfreq=60, seed=621)
    body2(sc, H, 'skin_pale', build='gaunt', eye='glow_frost')
    sculpt_body(m, sc)
    dress(m, [G.tunic(sc, H, 'cloth_dark', hem=0.1, sleeves=True), G.sleeves(sc, H, 'cloth_dark', 0.012), G.gloves(sc, H, 'leather_black', 0.004),
              G.hood(sc, H, 'cloth_dark', 0.024), G.wrap(sc, H, 'bone', 0.012, [G.R('box', c=H.p('head') + V(0, 0.06, 0.06), h=(0.05, 0.05, 0.04))],
                                                         grooves=[G.R('box', c=H.p('head') + V(0, 0.075, 0.1), h=(0.035, 0.003, 0.04))])])
    on(m, 'hips', robe_skirt(H, 0.98, 0.125 * H.h, 1.6, 16), 'cloth_dark', ('thigh_L', 'thigh_R'))
    on(m, 'chest', kit.chain(H.p('chest') + V(0.13, 0.08, 0.1), H.p('hips') + V(-0.13, 0.0, 0.12), 10, 0.02, 0.015), 'iron')
    on(m, 'hips', belt(H, 0.12 * H.h), 'iron')
    scythe = merge([cylinder(0.015, 1.3, 8).t(0, -0.45, 0), tube(curve([(0, 0.8, 0), (0.09, 0.84, 0.12), (0.07, 0.76, 0.34)], 12), [0.02, 0.016, 0.002], 6)])
    hold(m, H, scythe, 'iron', 'R', fwd=False)
    hold(m, H, kit.chain((0, 0, 0), (0, -0.12, 0), 3, 0, 0.01), 'iron', 'L', fwd=False)
    hold(m, H, sphere(0.04, 0.05, 0.04, 10).t(0, -0.16, 0), 'gold', 'L', fwd=False)
    H.anims('melee', speed=0.9)
    return m
