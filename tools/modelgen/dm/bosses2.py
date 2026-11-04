"""Bosse, Version 2: klare Formen, passgenaue Rüstung, harte Segmentplatten."""
from __future__ import annotations

import math

import numpy as np

from . import garments as G
from . import kit
from .anatomy import membrane_mesh, membrane_wing
from .anatomy2 import V, belt, body2, cape_back, cloth_panel, hair_locks, robe_skirt, shell
from .enemies import hold
from .enemies2 import golem_body, rock_chunk, spine_spikes
from .geo import TAU, crystal, curve, cylinder, displace, icosphere, lathe, merge, rbox, sphere, spike, torus, tube
from .heroes2 import dress, on, sculpt_body
from .model import Model
from .rig import Chain, Floater, Humanoid, loop_keys
from .sdf import R, Sculpt, build_cache, rot

REG = {}


def boss(fn):
    REG[fn.__name__] = fn
    return fn


def B(name, title):
    return Model(name, 'boss', title, skinned=True)


def dress_big(m, layers, H):
    for L in layers:
        if isinstance(L, list):
            dress_big(m, L, H)
            continue
        if getattr(L.base, '_cache', None) is None:
            build_cache(L.base, 0.004 * H.h)
        m.sculpt(L, voxel=0.0048 * H.h, max_dim=280, target=2200)


@boss
def riftColossus():
    m = B('riftColossus', 'Der Risskoloss')
    H = Humanoid(m, 2.7, w=1.5, legs=0.38, arm=1.25, hunch=0.7)
    golem_body(m, H, 'rock', 'crystal_violet', 1.05, seed=61)
    h = H.h
    on(m, 'chest', sphere(0.09 * h, 0.13 * h, 0.04 * h, 18).t(*(H.p('chest') + V(0, 0.0, 0.17 * h))), 'void')
    on(m, 'chest', torus(0.1 * h, 0.012 * h, 24, 5).s(1, 1.4, 1).rx(math.pi / 2).t(*(H.p('chest') + V(0, 0.0, 0.17 * h))), 'glow_arcane')
    for b, off, s in (('chest', (0.18, 0.18, -0.15), 1.0), ('chest', (-0.2, 0.15, -0.12), 0.9), ('upperarm_L', (0.12, 0.12, 0), 0.8), ('upperarm_R', (-0.12, 0.12, 0), 0.8), ('spine', (0, 0.05, -0.2), 1.1)):
        on(m, b, kit.crystal_cluster(5, 0.03 * h * s, 0.22 * h * s, 0.04 * h * s, seed=int(abs(off[0]) * 100) + len(b)).rx(-0.4).t(*(H.p(b) + V(*off) * h)), 'crystal_violet')
    for sg in (1, -1):
        on(m, 'head', kit.horn(0.25 * h, 0.03 * h, 1.2).rz(-sg * 0.8).t(*(H.p('head') + V(sg * 0.05 * h, 0.1 * h, 0))), 'obsidian')
    H.anims('brute', speed=0.5, heavy=True)
    return m


@boss
def ashQueen():
    m = B('ashQueen', 'Die Aschenkönigin')
    H = Humanoid(m, 2.2, w=0.92, legs=0.51, hover=0.25, arm=1.08)
    h = H.h
    for sd, sg in (('L', 1), ('R', -1)):
        m.node('wing_' + sd, 'chest', (sg * 0.06 * h, 0.8 * h, -0.08 * h), (sg * 0.38 * h, 0.95 * h, -0.18 * h))
        m.node('wingtip_' + sd, 'wing_' + sd, (sg * 0.38 * h, 0.95 * h, -0.16 * h), (sg * 0.75 * h, 1.05 * h, -0.28 * h))
    sc = Sculpt(detail=0.0005 * h, dfreq=30, seed=803)
    wings = []
    for sd, sg in (('L', 1), ('R', -1)):
        root, elbow, tips = membrane_wing(sc, m.nodes['wing_' + sd].pos, 0.75 * h, sg, 'wing_' + sd, 'wingtip_' + sd, 'obsidian', 'ember', h=h * 0.9)
        wings.append((sd, membrane_mesh(V(*m.nodes['wing_' + sd].pos), elbow, tips, V(sg * 0.07 * h, 0.58 * h, -0.1 * h), 0.004 * h)))
    body2(sc, H, 'obsidian', build='athletic', female=True, head='demon', claws=True, feet='claw', eye='glow_fire', horns=('obsidian', 1.7, 1.4))
    sculpt_body(m, sc, 12000, 0.0052 * h)
    dress_big(m, [G.wrap(sc, H, 'gold', 0.012, [R('ellipsoid', c=H.p('chest') + V(0, -0.01 * h, 0.03 * h), r=(0.16 * h, 0.07 * h, 0.1 * h))],
                         grooves=[R('box', c=H.p('chest') + V(0, -0.01 * h, 0), h=(0.3 * h, 0.003 * h, 0.3 * h))]),
                  G.bracers(sc, H, 'gold', 0.01), G.wrap(sc, H, 'ember', 0.008, [R('box', c=H.p('hips') + V(0, 0, 0), h=(0.2 * h, 0.08 * h, 0.16 * h))])], H)
    ring = [H.p('hips') + V(math.cos(a) * 0.13 * h, 0.0, math.sin(a) * 0.1 * h) for a in np.linspace(0, TAU, 29)]
    on(m, 'hips', cloth_panel(ring, 0.42 * h, 12, 0.012 * h, 14, 1.8, jag=0.35), 'ember', ('thigh_L', 'thigh_R'))
    for sd, w in wings:
        m.add(w, 'ember', 'wing_' + sd, blend=('wingtip_' + sd,))
    crown = merge([spike(0.008 * h, (0.07 + 0.03 * (i % 2)) * h, 5).t(math.cos(a) * 0.05 * h, 0, math.sin(a) * 0.05 * h) for i, a in enumerate(np.linspace(0, TAU, 9)[:-1])] + [torus(0.05 * h, 0.006 * h, 24, 5)])
    on(m, 'head', crown.t(*(H.p('head') + V(0, 0.125 * h, 0))), 'gold')
    st, cl, gem = kit.staff(1.0)
    hold(m, H, st.s(h * 0.85), 'obsidian', 'R', fwd=False)
    hold(m, H, crystal(0.03 * h, 0.12 * h, 6).t(0, 0.56 * h, 0), 'glow_fire', 'R', fwd=False)
    H.anims('caster', speed=0.6, fly=True)
    for clip, T_, A in (('Idle', 2.0, 15), ('Walk', 1.2, 30)):
        m.anim_rot(clip, 'wing_L', loop_keys(8, T_, lambda f, A=A: A * math.sin(f * TAU)), axis=(0, 0, 1))
        m.anim_rot(clip, 'wing_R', loop_keys(8, T_, lambda f, A=A: -A * math.sin(f * TAU)), axis=(0, 0, 1))
    return m


@boss
def nullWarden():
    m = B('nullWarden', 'Der Nullhüter')
    Fl = Floater(m, alt=1.7, orbits=4, radius=1.05)
    c = V(0, 1.7, 0)
    # Robe als harte Bahnen um einen Kern, Kapuze mit Leere, Knochenhände
    ring = [c + V(math.cos(a) * 0.32, 0.55, math.sin(a) * 0.26) for a in np.linspace(0, TAU, 33)]
    on(m, 'core', cloth_panel(ring, 1.5, 14, 0.03, 16, 1.6, jag=0.4), 'cloth_dark')
    sc = Sculpt(detail=0.001, dfreq=20, seed=805)
    sc.ellipsoid(c + V(0, 0.75, -0.02), (0.3, 0.33, 0.3), 'cloth_dark', 'core', k=0.02)
    sc.ellipsoid(c + V(0, 0.7, 0.22), (0.2, 0.25, 0.2), 'cloth_dark', 'core', sub=True, k=0.02)
    sc.ellipsoid(c + V(0, 0.68, 0.08), (0.16, 0.2, 0.1), 'void', 'core', k=0.01)
    sc.ellipsoid(c + V(0, 0.5, 0), (0.42, 0.14, 0.3), 'cloth_dark', 'core', k=0.04)
    for sg in (1, -1):
        sc.capsule(c + V(sg * 0.36, 0.45, 0), c + V(sg * 0.62, 0.0, 0.25), 0.11, 0.13, 'cloth_dark', 'core', k=0.03)
    m.sculpt(sc, voxel=0.012, max_dim=200, target=6000)
    from .anatomy2 import hand2
    sc2 = Sculpt(detail=0.0, seed=806)
    for sg in (1, -1):
        sc2.capsule(c + V(sg * 0.62, 0.02, 0.26), c + V(sg * 0.64, -0.08, 0.28), 0.035, 0.03, 'bone', 'core', k=0.01)
        hand2(sc2, c + V(sg * 0.64, -0.06, 0.28), sg, 2.2, 'bone', 'core', claw=True)
    m.sculpt(sc2, voxel=0.005, max_dim=200, target=3000)
    on(m, 'core', merge([crystal(0.03, 0.12, 4).t(math.cos(a) * 0.17, 2.6, math.sin(a) * 0.17 + 0.02) for a in np.linspace(0, TAU, 7)[:-1]]), 'glow_arcane')
    on(m, 'core', torus(0.55, 0.03, 48, 6).t(0, 1.6, 0), 'gold')
    for i in range(4):
        p = V(*m.nodes[f'orbit{i}'].pos)
        on(m, f'orbit{i}', kit.rune_stone(0.2, 0.7, 0.14, seed=i).t(*(p + V(0, -0.35, 0))), 'runes')
        on(m, f'orbit{i}', crystal(0.06, 0.24, 5).t(*(p + V(0, 0.38, 0))), 'crystal_violet')
    Fl.anims()
    m.anim_rot('Attack', 'core', [(0, (1, 0, 0), 0), (0.3, (1, 0, 0), -12), (0.5, (1, 0, 0), 10), (0.9, (1, 0, 0), 0)])
    return m


@boss
def maw():
    m = B('maw', 'Der Schlund der Tiefe')
    C = Chain(m, n=9, length=2.6, height=0.4, up=1.3)
    P = [V(*m.nodes[b].pos) for b in C.bones]
    sc = Sculpt(detail=0.002, dfreq=14, seed=807)
    for i, b in enumerate(C.bones):
        r = 0.4 - i * 0.028
        sc.ellipsoid(P[i], (r, r * 0.95, 0.22), 'flesh', b, k=0.03)
        if i < len(P) - 1:
            sc.capsule(P[i], P[i + 1], r * 0.82, (r - 0.028) * 0.82, 'flesh', b, k=0.03)
    head = P[0]
    sc.torus(head + V(0, 0.0, 0.2), 0.3, 0.09, 'flesh', 'seg0', k=0.03, R=rot(0, 90, 0))
    sc.ellipsoid(head + V(0, 0.0, 0.32), (0.26, 0.26, 0.2), 'flesh', 'seg0', sub=True, k=0.03)
    sc.ellipsoid(head + V(0, 0.0, 0.1), (0.2, 0.2, 0.08), 'void', 'seg0', k=0.01)
    m.sculpt(sc, voxel=0.012, max_dim=260, target=12000)
    # harte Chitin-Ringe je Segment, Zähne in zwei Reihen, Augen
    for i, b in enumerate(C.bones):
        r = 0.4 - i * 0.028
        d = (P[i - 1] - P[i]) if i > 0 else (P[0] - P[1])
        ring = shell(r * 1.07, 0.11, r * 1.07, -0.7, 0.7, 22, 6, 0.012).t(0, 0.06, 0)
        from .anatomy2 import align
        rr = align(ring, P[i] - d / np.linalg.norm(d) * 0.06, P[i] + d / np.linalg.norm(d) * 0.06)
        on(m, b, rr, 'chitin_red')
        for k in range(5):
            a = math.pi * (0.15 + 0.7 * k / 4)
            on(m, b, spike(0.03, 0.12 - i * 0.008, 6).rz(a - math.pi / 2).t(*(P[i] + V(math.cos(a) * r * 1.05, math.sin(a) * r * 1.05, 0))), 'obsidian')
    teeth = []
    for row, (rr, z, L) in enumerate(((0.29, 0.26, 0.15), (0.2, 0.2, 0.11))):
        n = 16 - row * 5
        for k in range(n):
            a = TAU * k / n
            base = head + V(math.cos(a) * rr, math.sin(a) * rr, z)
            t = spike(0.025, L, 5)
            from .anatomy2 import align
            teeth.append(align(t, base, base + V(-math.cos(a) * L, -math.sin(a) * L, 0.03)))
    on(m, 'seg0', merge(teeth), 'bone')
    on(m, 'seg0', merge([sphere(0.035, seg=10).t(*(head + V(math.cos(a) * 0.38, math.sin(a) * 0.38, 0.05))) for a in np.linspace(0.4, math.pi - 0.4, 5)]), 'glow_poison')
    C.anims(speed=0.6, rear=30)
    return m


def _twin(name, title, skin, female):
    m = B(name, title)
    H = Humanoid(m, 2.0, w=1.0, legs=0.51, hover=0.3, arm=1.05)
    sc = Sculpt(detail=0.0005 * H.h, dfreq=30, seed=hash(name) % 997)
    body2(sc, H, skin, build='athletic', female=female, eye='glow_white' if not female else 'glow_frost')
    sculpt_body(m, sc, 11000, 0.0052 * H.h)
    return m, H, sc


@boss
def twinSol():
    m, H, sc = _twin('twinSol', 'Sol, der Glühende Zwilling', 'ember', False)
    h = H.h
    dress_big(m, [G.pants(sc, H, 'obsidian', 0.006, to=0.3), G.cuirass(sc, H, 'gold', 0.014), G.pauldrons(sc, H, 'gold', 0.026, 3),
                  G.greaves(sc, H, 'gold', 0.012), G.bracers(sc, H, 'obsidian', 0.01), G.boots(sc, H, 'obsidian', 0.014, 0.06)], H)
    on(m, 'hips', cloth_panel([H.p('hips') + V(x * h, 0.02 * h, 0.1 * h) for x in np.linspace(-0.08, 0.08, 7)], 0.36 * h, 3, 0.006 * h, 10, 1.25, jag=0.3), 'cloth_red', ('thigh_L', 'thigh_R'))
    halo = merge([torus(0.19 * h, 0.012 * h, 48, 6).rx(math.pi / 2)] + [spike(0.016 * h, 0.1 * h, 5).rz(a - math.pi / 2).t(math.cos(a) * 0.2 * h, math.sin(a) * 0.2 * h, 0) for a in np.linspace(0, TAU, 13)[:-1]])
    on(m, 'head', halo.t(*(H.p('head') + V(0, 0.09 * h, -0.1 * h))), 'gold')
    for sd in 'LR':
        bl, gp, gd = kit.blade(0.9, 0.08, 0.016, 0.2, 0.18, curved=0.35)
        hold(m, H, bl, 'glow_fire', sd); hold(m, H, gp, 'leather_black', sd); hold(m, H, gd, 'gold', sd)
    H.anims('melee', speed=0.7, fly=True)
    return m


@boss
def twinLuna():
    m, H, sc = _twin('twinLuna', 'Luna, die Kalte Zwillingin', 'skin_pale', True)
    h = H.h
    dress_big(m, [G.tunic(sc, H, 'cloth_blue', 0.01, hem=0.1, sleeves=True), G.sleeves(sc, H, 'cloth_blue', 0.01),
                  G.wrap(sc, H, 'steel', 0.018, [R('ellipsoid', c=H.p('chest') + V(0, 0.0, 0.02 * h), r=(0.17 * h, 0.09 * h, 0.12 * h))],
                         grooves=[R('box', c=H.p('chest') + V(0, y * h, 0), h=(0.3 * h, 0.003 * h, 0.3 * h)) for y in (-0.03, 0.02)]),
                  G.pauldrons(sc, H, 'ice', 0.024, 2), G.bracers(sc, H, 'steel', 0.012)], H)
    on(m, 'hips', robe_skirt(H, 0.95, 0.13 * h, 1.9, 20), 'cloth_blue', ('thigh_L', 'thigh_R'))
    on(m, 'head', hair_locks(H, 13, 0.3, seed=11), 'ice')
    moon = tube(np.array([(math.cos(a) * 0.2 * h, math.sin(a) * 0.2 * h, 0) for a in np.linspace(-1.9, 1.9, 28)]), np.concatenate([np.linspace(0.004, 0.03, 14), np.linspace(0.03, 0.004, 14)]) * h, 10)
    on(m, 'head', moon.rz(math.pi / 2).t(*(H.p('head') + V(0, 0.1 * h, -0.1 * h))), 'crystal_cyan')
    for sg in (1, -1):
        on(m, 'upperarm_' + ('L' if sg > 0 else 'R'), kit.crystal_cluster(3, 0.012 * h, 0.08 * h, 0.015 * h, seed=3 + sg).rz(-sg * 0.6).t(*(H.p('upperarm_' + ('L' if sg > 0 else 'R')) + V(sg * 0.04 * h, 0.04 * h, 0))), 'ice')
    st, cl, gem = kit.staff(1.0)
    hold(m, H, st.s(h * 0.9), 'steel', 'R', fwd=False)
    hold(m, H, crystal(0.035 * h, 0.15 * h, 6).t(0, 0.6 * h, 0), 'crystal_cyan', 'R', fwd=False)
    H.anims('caster', speed=0.7, fly=True)
    return m


@boss
def worldbreaker():
    m = B('worldbreaker', 'Der Weltenbrecher')
    H = Humanoid(m, 3.0, w=1.35, legs=0.43, arm=1.12, hunch=0.3)
    h = H.h
    sc = Sculpt(detail=0.0006 * h, dfreq=20, seed=811)
    body2(sc, H, 'flesh_grey', build='hulking', head='orc', eye='glow_fire', horns=('bone', 1.5, 0.8))
    sculpt_body(m, sc, 12000, 0.0055 * h)
    dress_big(m, [G.pants(sc, H, 'leather_black', 0.006), G.cuirass(sc, H, 'iron_rust', 0.016, 4), G.pauldrons(sc, H, 'iron', 0.03, 3),
                  G.greaves(sc, H, 'iron', 0.014), G.bracers(sc, H, 'iron', 0.014, hands=True), G.boots(sc, H, 'iron', 0.016, 0.08)], H)
    for sd, sg in (('L', 1), ('R', -1)):
        for k in range(3):
            on(m, 'upperarm_' + sd, spike(0.012 * h, 0.09 * h, 6).rz(-sg * (0.3 + 0.3 * k)).t(*(H.p('upperarm_' + sd) + V(sg * 0.05 * h, 0.08 * h, (k - 1) * 0.035 * h))), 'iron')
    on(m, 'hips', cloth_panel([H.p('hips') + V(x * h, 0.02 * h, 0.105 * h) for x in np.linspace(-0.09, 0.09, 8)], 0.3 * h, 3, 0.005 * h, 10, 1.2, jag=0.4), 'cloth_red', ('thigh_L', 'thigh_R'))
    on(m, 'chest', kit.skull(0.08 * h).t(*(H.p('chest') + V(0, 0.0, 0.17 * h))), 'bone')
    on(m, 'chest', kit.chain(H.p('chest') + V(0.15 * h, 0.06 * h, 0.15 * h), H.p('hips') + V(-0.15 * h, 0.02 * h, 0.15 * h), 14, 0.02 * h, 0.016 * h), 'iron_rust')
    spine_spikes(m, H, 5, 'iron', 0.08, 0.13)
    hammer = merge([cylinder(0.05, 1.8, 10).t(0, -0.6, 0), rbox(0.7, 0.42, 0.42, 0.05).t(0, 1.25, 0)] +
                   [spike(0.07, 0.22, 6).rz(math.pi / 2 * s).t(s * 0.35, 1.25, 0) for s in (1, -1)] + [spike(0.05, 0.18, 6).t(0, 1.46, 0)])
    hold(m, H, hammer, 'iron', 'R')
    hold(m, H, torus(0.22, 0.03, 24, 6).t(0, 1.25, 0), 'gold', 'R')
    H.anims('brute', speed=0.45, heavy=True)
    return m
