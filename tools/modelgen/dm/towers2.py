"""Turmmodelle der Erweiterung 4.0 (acht Grundtürme, dreizehn Fusionen, Soldat und Ritter der Wachkaserne).
Gleicher Aufbau wie dm/towers.py: Knoten Base (fest), Turret (dreht sich zum Ziel um Y), Muzzle (Austritt).
Drehende Teile (Ringe, Rotoren) hängen an eigenen Kindknoten, weil der Spielcode die Turret-Drehung beim Zielen setzt."""
from __future__ import annotations

import math

import numpy as np

from . import kit
from .geo import TAU, crystal, curve, cylinder, displace, icosphere, lathe, merge, rbox, sphere, spike, torus, tube
from .model import Model
from .towers import T, bob, idle_scan, legendary_base, pulse, recoil, stone_base

REG = {}


def tower(fn):
    REG[fn.__name__] = fn
    return fn


def spin(m, node, T_=4.0, axis=(0, 1, 0), clip='Idle', turns=1):
    m.anim_rot(clip, node, [(T_ * i / 4, axis, turns * 90 * i) for i in range(5)])


def ring_of(make, n, r, y=0.0, phase=0.0):
    out = []
    for i in range(n):
        a = TAU * i / n + phase
        out.append(make(i).ry(-a).t(math.cos(a) * r, y, math.sin(a) * r))
    return merge(out)


def sail_rotor(m, node, r=0.42, n=4, mat='cloth_red', frame='wood_black'):
    """Windmühlenrotor in der X/Y-Ebene (Achse Z) am Knoten node."""
    arms, sails = [], []
    for i in range(n):
        a = TAU * i / n
        ca, sa = math.cos(a), math.sin(a)
        arms.append(rbox(0.03, r, 0.03, 0.006).t(0, r / 2, 0).rz(a))
        sails.append(rbox(0.13, r * 0.78, 0.012, 0.004).t(0.075, r * 0.55, 0).rz(a))
    o = m.nodes[node].pos
    m.add(merge(arms).t(*o), frame, node)
    m.add(merge(sails).t(*o), mat, node)
    m.add(cylinder(0.05, 0.1, 10).rx(math.pi / 2).t(0, 0, -0.06).t(*o), 'iron', node)


def kettle(r=0.24, h=0.26):
    return lathe([(0.05, 0.0), (r * 0.7, 0.02), (r, h * 0.35), (r * 1.02, h * 0.7), (r * 0.9, h), (r * 0.95, h * 1.03), (r * 0.86, h * 1.03), (r * 0.8, h * 0.95)], 16, cap_top=False)


def figure(m, scale=1.0, armor='iron', tabard='cloth_red', helm='steel', knight=False):
    """Starre Soldatenfigur (Lane-Einheit). Base = Beine, Turret = Oberkörper mit Waffe."""
    s = scale
    for x in (-0.07, 0.07):
        m.add(capsule_leg(0.045 * s, 0.36 * s).t(x * s, 0.0, 0), 'leather_black', 'Base')
        m.add(rbox(0.07 * s, 0.05 * s, 0.11 * s, 0.01).t(x * s, 0.025 * s, 0.02 * s), 'iron', 'Base')
    m.add(rbox(0.22 * s, 0.1 * s, 0.14 * s, 0.03).t(0, 0.38 * s, 0), tabard, 'Base')
    # Oberkörper
    m.add(lathe([(0.1 * s, 0.0), (0.13 * s, 0.1 * s), (0.14 * s, 0.22 * s), (0.08 * s, 0.3 * s), (0.0, 0.31 * s)], 10).s(1, 1, 0.75).t(0, 0.4 * s, 0), armor, 'Turret')
    m.add(rbox(0.2 * s, 0.24 * s, 0.03 * s, 0.01).t(0, 0.5 * s, 0.085 * s), tabard, 'Turret')
    for x in (-0.15, 0.15):
        m.add(kit.pauldron(0.06 * s, 2 if knight else 0).t(x * s, 0.66 * s, 0), armor, 'Turret')
    m.add(sphere(0.085 * s, 0.095 * s, 0.085 * s, 12).t(0, 0.8 * s, 0), 'skin_dark', 'Turret')
    hl = lathe([(0.0, 0.17 * s), (0.06 * s, 0.155 * s), (0.095 * s, 0.1 * s), (0.1 * s, 0.0), (0.0, 0.0)], 12)
    m.add(hl.t(0, 0.76 * s, 0), helm, 'Turret')
    m.add(rbox(0.16 * s, 0.02 * s, 0.02 * s, 0.004).t(0, 0.82 * s, 0.09 * s), 'void', 'Turret')
    if knight:
        m.add(curve_plume(s).t(0, 0.93 * s, -0.02 * s), 'cloth_red', 'Turret')
        bl = kit.blade(0.55 * s, 0.045 * s, 0.01 * s, 0.13 * s, 0.1 * s)
        for part, mat in zip(bl, ('steel', 'leather_black', 'gold')):
            m.add(part.rx(-math.pi / 2 + 0.3).t(0.17 * s, 0.55 * s, 0.12 * s), mat, 'Turret')
        face, boss = kit.shield(0.16 * s, 'kite')
        m.add(face.t(-0.19 * s, 0.6 * s, 0.1 * s), 'cloth_red', 'Turret')
        m.add(boss.t(-0.19 * s, 0.6 * s, 0.12 * s), 'gold', 'Turret')
    else:
        m.add(cylinder(0.012 * s, 0.95 * s, 6).t(0.17 * s, 0.2 * s, 0.05 * s), 'wood', 'Turret')
        m.add(spike(0.025 * s, 0.13 * s, 6).t(0.17 * s, 1.15 * s, 0.05 * s), 'steel', 'Turret')
        face, boss = kit.shield(0.14 * s, 'round')
        m.add(face.t(-0.18 * s, 0.58 * s, 0.09 * s), 'wood', 'Turret')
        m.add(boss.t(-0.18 * s, 0.58 * s, 0.09 * s), 'iron', 'Turret')
    m.nodes['Turret'].pos = (0, 0.4 * s, 0)
    m.nodes['Muzzle'].pos = (0.17 * s, 0.9 * s, 0.25 * s)
    m.anim_pos('Idle', 'Turret', [(0, (0, 0, 0)), (0.8, (0, 0.012 * s, 0)), (1.6, (0, 0, 0))])
    m.anim_rot('Attack', 'Turret', [(0, (1, 0, 0), 0), (0.1, (1, 0, 0), 14), (0.35, (1, 0, 0), 0)])
    m.anim_pos('Attack', 'Turret', [(0, (0, 0, 0)), (0.1, (0, 0, 0.06 * s)), (0.35, (0, 0, 0))])


def capsule_leg(r, h):
    from .geo import capsule
    return capsule(r, h, 8)


def curve_plume(s):
    pts = curve([(0, 0, 0.04 * s), (0, 0.08 * s, -0.02 * s), (0, 0.05 * s, -0.12 * s), (0, -0.04 * s, -0.17 * s)], 10)
    return tube(pts, [0.03 * s, 0.035 * s, 0.025 * s, 0.01 * s], 6)


# ====================================================================== Grundtürme

@tower
def seal():
    m = T('seal', 'Siegelstein', 1.05, (0, 1.42, 0))
    stone_base(m, 0.42, 0.14, 'stone_dark')
    obel = lathe([(0.2, 0.0), (0.17, 0.12), (0.12, 0.95), (0.0, 1.12)], 4, cap_bottom=False, cap_top=False, sharp=(1, 2), a0=math.pi / 4).t(0, 0.14, 0)
    m.add(obel, 'runes', 'Base')
    m.add(kit.band(0.19, 0.24, 0.05, 0.02, 4), 'iron', 'Base')
    for i in range(4):
        a = TAU * i / 4
        m.add(kit.rune_stone(0.1, 0.22, 0.06, seed=i + 3).ry(-a).t(math.cos(a) * 0.33, 0.12, math.sin(a) * 0.33), 'stone_dark', 'Base')
        m.add(crystal(0.025, 0.08, 5).t(math.cos(a) * 0.33, 0.37, math.sin(a) * 0.33), 'crystal_violet', 'Base')
    m.node('RingA', 'Turret', (0, 1.05, 0))
    m.node('RingB', 'Turret', (0, 1.05, 0))
    m.add(torus(0.3, 0.018, 36, 6).rx(math.pi / 2 - 0.35).t(0, 1.05, 0), 'gold', 'RingA')
    m.add(merge([rbox(0.05, 0.05, 0.02, 0.005).t(0, 0.3, 0).rz(TAU * k / 6) for k in range(6)]).rx(-0.35).t(0, 1.05, 0), 'glow_arcane', 'RingA')
    m.add(torus(0.22, 0.014, 32, 6).rz(math.pi / 2 + 0.5).t(0, 1.05, 0), 'gold', 'RingB')
    m.add(crystal(0.07, 0.26, 6, 0.4).t(0, 1.3, 0), 'crystal_violet', 'Turret')
    m.add(sphere(0.05, seg=10).t(0, 1.05, 0), 'glow_arcane', 'Turret')
    spin(m, 'RingA', 5.0)
    spin(m, 'RingB', 3.4, axis=(0, 1, 0), turns=-1)
    bob(m, 'Turret', 0.02, 3.0)
    pulse(m, 'Turret', 0.92, 1.12, 0.45, 'Attack')
    return m


@tower
def acid():
    m = T('acid', 'Ätzkessel', 0.5, (0, 0.82, 0.25))
    stone_base(m, 0.44, 0.13, 'stone_dark')
    for i in range(3):
        a = TAU * i / 3 + 0.3
        m.add(kit.beam((math.cos(a) * 0.34, 0.12, math.sin(a) * 0.34), (math.cos(a) * 0.17, 0.52, math.sin(a) * 0.17), 0.05), 'iron_rust', 'Base')
    m.add(kit.brazier(0.14, 0.13)[0].s(1.2, 1, 1.2), 'iron', 'Base')
    m.add(kit.brazier(0.14, 0.13)[1].s(1.2, 1, 1.2), 'ember', 'Base')
    m.add(tube(curve([(0.3, 0.13, -0.2), (0.36, 0.45, -0.22), (0.2, 0.7, -0.1)], 12), 0.025, 6), 'iron', 'Base')
    m.add(cylinder(0.08, 0.3, 10, bevel=0.01).t(0.3, 0.13, -0.22), 'iron_rust', 'Base')
    m.add(sphere(0.06, seg=10).t(0.3, 0.46, -0.22), 'glow_poison', 'Base')
    # Kessel (Turret, kippt beim Angriff)
    m.add(kettle(0.25, 0.3).t(0, 0.5, 0), 'iron_rust', 'Turret')
    m.add(kit.band(0.255, 0.68, 0.035, 0.015, 16), 'iron', 'Turret')
    m.add(cylinder(0.21, 0.02, 16).t(0, 0.74, 0), 'glow_poison', 'Turret')
    m.add(merge([sphere(0.03 + 0.012 * (k % 3), seg=8).t(math.cos(k * 2.1) * 0.12, 0.765, math.sin(k * 2.1) * 0.12) for k in range(6)]), 'crystal_green', 'Turret')
    m.add(tube(curve([(0, 0.78, 0.22), (0, 0.8, 0.32), (0, 0.76, 0.36)], 8), 0.03, 6), 'iron', 'Turret')
    m.add(kit.skull(0.06).t(0, 0.62, 0.25), 'bone', 'Turret')
    m.anim_rot('Idle', 'Turret', [(0, (0, 0, 1), 0), (1.5, (0, 0, 1), 2.5), (3, (0, 0, 1), 0), (4.5, (0, 0, 1), -2.5), (6, (0, 0, 1), 0)])
    m.anim_rot('Attack', 'Turret', [(0, (1, 0, 0), 0), (0.15, (1, 0, 0), 30), (0.6, (1, 0, 0), 0)])
    return m


@tower
def quake():
    m = T('quake', 'Bebenhammer', 0.95, (0, 0.3, 0))
    stone_base(m, 0.45, 0.14, 'stone_dark')
    m.add(cylinder(0.3, 0.08, 16, bevel=0.015).t(0, 0.14, 0), 'iron', 'Base')
    m.add(displace(cylinder(0.24, 0.04, 16).t(0, 0.21, 0), 0.008, 18), 'rock', 'Base')
    for x in (-0.3, 0.3):
        m.add(kit.beam((x, 0.14, 0), (x * 0.8, 1.25, 0), 0.07), 'wood_black', 'Base')
        m.add(kit.beam((x, 0.14, 0.18), (x * 0.8, 0.9, 0), 0.04), 'wood_black', 'Base')
        m.add(kit.beam((x, 0.14, -0.18), (x * 0.8, 0.9, 0), 0.04), 'wood_black', 'Base')
    m.add(rbox(0.6, 0.09, 0.1, 0.015).t(0, 1.25, 0), 'wood', 'Base')
    m.add(cylinder(0.06, 0.22, 10).t(0, 1.12, 0), 'steel', 'Base')
    m.add(kit.gear(0.1, 0.03, 10).rx(math.pi / 2).t(0.3, 1.12, 0.07), 'iron', 'Base')
    m.add(sphere(0.05, seg=10).t(0.22, 1.32, 0), 'ember', 'Base')
    # Hammer (Turret: hebt sich, schlägt zu)
    m.add(cylinder(0.035, 0.55, 8).t(0, 0.45, 0), 'steel', 'Turret')
    head = merge([rbox(0.34, 0.2, 0.26, 0.03).t(0, 0.35, 0), rbox(0.38, 0.05, 0.3, 0.015).t(0, 0.24, 0)])
    m.add(head, 'iron', 'Turret')
    m.add(merge([rbox(0.04, 0.12, 0.27, 0.01).t(sg * 0.15, 0.35, 0) for sg in (1, -1)]), 'gold', 'Turret')
    m.add(kit.skull(0.07).t(0, 0.38, 0.14), 'bone', 'Turret')
    m.nodes['Turret'].pos = (0, 0.0, 0)
    m.anim_pos('Idle', 'Turret', [(0, (0, 0.18, 0)), (1.5, (0, 0.22, 0)), (3, (0, 0.18, 0))])
    m.anim_pos('Attack', 'Turret', [(0, (0, 0.3, 0)), (0.08, (0, -0.02, 0)), (0.18, (0, 0.0, 0)), (0.7, (0, 0.18, 0))])
    return m


@tower
def gale():
    m = T('gale', 'Windwacht', 1.15, (0, 1.25, 0.3))
    stone_base(m, 0.42, 0.14, 'stone_pale')
    m.add(kit.body(0.27, 0.19, 1.0, 8, 0.14, flare=0.04), 'stone_pale', 'Base')
    m.add(kit.band(0.24, 0.5, 0.04, 0.015), 'iron', 'Base')
    m.add(kit.slits(0.23, 0.75, 4, 0.0, 0.035, 0.12), 'glow_storm', 'Base')
    m.add(kit.roof(0.23, 0.22, 8, 1.14, 0.03, 0.1), 'wood_black', 'Base')
    # drehbarer Kopf mit Rotor
    m.add(rbox(0.16, 0.16, 0.3, 0.03).t(0, 1.2, 0.0), 'wood', 'Turret')
    m.add(kit.banner(0.08, 0.18)[0].ry(math.pi / 2).t(0, 1.33, -0.2), 'cloth_blue', 'Turret')
    m.node('Rotor', 'Turret', (0, 1.22, 0.2))
    sail_rotor(m, 'Rotor', 0.44, 4, 'cloth_dark')
    m.add(merge([rbox(0.012, 0.3, 0.012, 0).t(0, 0.17, 0).rz(TAU * k / 4 + math.pi / 4) for k in range(4)]).t(0, 1.22, 0.2), 'steel', 'Rotor')
    spin(m, 'Rotor', 3.0, axis=(0, 0, 1))
    spin(m, 'Rotor', 0.6, axis=(0, 0, 1), clip='Attack')
    idle_scan(m, deg=20, T_=8)
    return m


@tower
def eyrie():
    m = T('eyrie', 'Adlerhorst', 1.42, (0, 0.2, 0.55))
    stone_base(m, 0.44, 0.13, 'stone_dark')
    for i in range(4):
        a = TAU * i / 4 + math.pi / 4
        m.add(kit.beam((math.cos(a) * 0.34, 0.12, math.sin(a) * 0.34), (math.cos(a) * 0.2, 1.3, math.sin(a) * 0.2), 0.055), 'wood_black', 'Base')
        b = a + TAU / 4
        m.add(kit.beam((math.cos(a) * 0.3, 0.5, math.sin(a) * 0.3), (math.cos(b) * 0.25, 0.95, math.sin(b) * 0.25), 0.03), 'wood_black', 'Base')
    m.add(kit.planks_platform(0.32, 1.3, 0.04, 8), 'wood', 'Base')
    m.add(tube(curve([(0.05, 0.1, 0.32), (0.05, 0.7, 0.28), (0.04, 1.3, 0.24)], 10), 0.012, 5), 'wood', 'Base')
    # Hütte + Armbrust (Turret)
    rail = [rbox(0.03, 0.14, 0.03, 0.005).t(math.cos(TAU * i / 10) * 0.29, 1.4, math.sin(TAU * i / 10) * 0.29) for i in range(10)]
    m.add(merge(rail), 'wood_black', 'Turret')
    m.add(torus(0.29, 0.014, 24, 5).t(0, 1.47, 0), 'wood', 'Turret')
    for sg in (1, -1):
        m.add(kit.beam((sg * 0.22, 1.32, -0.18), (sg * 0.2, 1.75, -0.16), 0.035), 'wood_black', 'Turret')
    m.add(kit.roof(0.32, 0.32, 6, 1.75, 0.05, 0.4), 'wood_black', 'Turret')
    m.add(kit.banner(0.1, 0.24)[0].t(0, 2.1, 0), 'cloth_red', 'Turret')
    m.add(cylinder(0.008, 0.25, 5).t(0, 2.0, 0), 'iron', 'Turret')
    m.add(rbox(0.06, 0.06, 0.75, 0.01).t(0, 1.42, 0.18), 'wood', 'Turret')
    limbs, string = kit.bow(0.6)
    m.add(limbs.rz(math.pi / 2).t(0, 1.43, 0.45), 'steel', 'Turret')
    m.add(string.rz(math.pi / 2).t(0, 1.43, 0.45), 'leather_black', 'Turret')
    m.add(spike(0.014, 0.08, 4).rx(math.pi / 2).t(0, 1.46, 0.53), 'iron', 'Turret')
    m.nodes['Turret'].pos = (0, 1.3, 0)
    m.nodes['Muzzle'].pos = (0, 1.46, 0.6)
    idle_scan(m, deg=30, T_=9)
    recoil(m, back=0.05, t=0.6)
    return m


@tower
def barracks():
    m = T('barracks', 'Wachkaserne', 0.95, (0, 1.0, 0.3))
    stone_base(m, 0.46, 0.12, 'stone_dark', sides=4)
    m.add(kit.body(0.44, 0.42, 0.55, 4, 0.12, flare=0.02).ry(math.pi / 4), 'stone_blocks', 'Base')
    m.add(kit.crenels(0.4, 0.67, 8, 0.13, 0.11, 0.08).ry(math.pi / 8), 'stone_blocks', 'Base')
    m.add(kit.band(0.43, 0.4, 0.04, 0.015, 4).ry(math.pi / 4), 'iron', 'Base')
    m.add(rbox(0.22, 0.3, 0.04, 0.02).t(0, 0.27, 0.305), 'wood_black', 'Base')
    m.add(merge([rbox(0.24, 0.025, 0.02, 0.004).t(0, 0.18 + k * 0.1, 0.33) for k in range(3)]), 'iron', 'Base')
    for sg in (1, -1):
        cloth, _ = kit.banner(0.13, 0.3)
        m.add(cloth.t(sg * 0.22, 0.6, 0.315), 'cloth_red', 'Base')
        bowl, coals = kit.brazier(0.05)
        m.add(bowl.t(sg * 0.36, 0.67, 0.36), 'iron', 'Base')
        m.add(coals.t(sg * 0.36, 0.67, 0.36), 'ember', 'Base')
    m.add(merge([rbox(0.04, 0.2, 0.04, 0.008).rx(0.2).t(sg * 0.12, 0.1, 0.42) for sg in (-1, 0, 1)]), 'wood', 'Base')
    # Fahnenmast (Turret)
    m.add(cylinder(0.02, 0.7, 6).t(0, 0.67, 0), 'wood', 'Turret')
    m.add(spike(0.03, 0.1, 6).t(0, 1.37, 0), 'gold', 'Turret')
    m.node('Flag', 'Turret', (0, 1.3, 0))
    flag = kit.banner(0.3, 0.2, 0.04, pole=False)[0].rz(math.pi / 2).t(0.16, 1.3, 0)
    m.add(flag, 'cloth_red', 'Flag')
    m.add(kit.skull(0.05).t(0, 1.15, 0.03), 'bone', 'Turret')
    m.anim_rot('Idle', 'Flag', [(0, (0, 1, 0), -8), (1.2, (0, 1, 0), 8), (2.4, (0, 1, 0), -8)])
    m.anim_rot('Attack', 'Flag', [(0, (0, 1, 0), 0), (0.2, (0, 1, 0), 25), (0.6, (0, 1, 0), 0)])
    return m


@tower
def bank():
    m = T('bank', 'Handelskontor', 0.95, (0, 1.1, 0))
    stone_base(m, 0.45, 0.12, 'stone_dark', sides=4)
    m.add(rbox(0.66, 0.42, 0.56, 0.03).t(0, 0.33, -0.03), 'stone_blocks', 'Base')
    m.add(rbox(0.7, 0.05, 0.6, 0.015).t(0, 0.55, -0.03), 'wood_black', 'Base')
    m.add(kit.roof(0.46, 0.3, 4, 0.56, 0.04, 0.0).ry(math.pi / 4).s(1.0, 1, 0.85), 'wood_black', 'Base')
    m.add(rbox(0.2, 0.26, 0.03, 0.02).t(0, 0.25, 0.26), 'wood', 'Base')
    m.add(merge([rbox(0.22, 0.02, 0.02, 0.003).t(0, 0.17 + k * 0.08, 0.28) for k in range(3)]), 'iron', 'Base')
    m.add(sphere(0.02, seg=8).t(0.06, 0.25, 0.29), 'gold', 'Base')
    # Kisten und Fässer
    m.add(merge([rbox(0.14, 0.14, 0.14, 0.01).t(-0.28, 0.19, 0.3), rbox(0.12, 0.12, 0.12, 0.01).t(-0.27, 0.32, 0.3).ry(0.3)]), 'wood', 'Base')
    m.add(merge([cylinder(0.07, 0.17, 10, bevel=0.01).t(0.28, 0.12, 0.3), cylinder(0.065, 0.15, 10, bevel=0.01).t(0.32, 0.12, 0.13)]), 'wood_black', 'Base')
    m.add(merge([torus(0.072, 0.008, 12, 4).t(0.28, y, 0.3) for y in (0.15, 0.26)]), 'iron', 'Base')
    m.add(kit.crystal_cluster(4, 0.03, 0.08, 0.04, seed=4).t(-0.27, 0.38, 0.3), 'crystal_gold', 'Base')
    # Waage (Turret)
    m.add(cylinder(0.02, 0.32, 6).t(0, 0.78, 0), 'gold', 'Turret')
    m.node('Beam', 'Turret', (0, 1.1, 0))
    m.add(rbox(0.5, 0.025, 0.025, 0.005).t(0, 1.1, 0), 'gold', 'Beam')
    for sg in (1, -1):
        m.add(merge([cylinder(0.004, 0.14, 4).t(sg * 0.24, 0.96, 0)]), 'iron', 'Beam')
        m.add(lathe([(0.0, 0.0), (0.08, 0.0), (0.09, 0.025)], 12, cap_top=False).t(sg * 0.24, 0.95, 0), 'gold', 'Beam')
    m.add(merge([cylinder(0.025, 0.012, 10).t(-0.24 + dx, 0.975 + k * 0.013, dz) for k, (dx, dz) in enumerate([(0, 0), (0.02, 0.01), (-0.01, 0.02)])]), 'gold', 'Beam')
    m.anim_rot('Idle', 'Beam', [(0, (0, 0, 1), -6), (2, (0, 0, 1), 6), (4, (0, 0, 1), -6)])
    m.anim_rot('Attack', 'Beam', [(0, (0, 0, 1), 0), (0.3, (0, 0, 1), -18), (1.0, (0, 0, 1), 0)])
    return m


@tower
def ward():
    m = T('ward', 'Schildwerk', 0.75, (0, 0.8, 0))
    stone_base(m, 0.44, 0.12, 'stone_pale')
    m.add(lathe([(0.4, 0.0), (0.42, 0.05), (0.38, 0.14), (0.3, 0.14), (0.3, 0.02)], 24).t(0, 0.12, 0), 'stone_pale', 'Base')
    for i in range(4):
        a = TAU * i / 4 + math.pi / 4
        m.add(kit.beam((math.cos(a) * 0.34, 0.2, math.sin(a) * 0.34), (math.cos(a) * 0.3, 0.62, math.sin(a) * 0.3), 0.07), 'stone_pale', 'Base')
        m.add(crystal(0.035, 0.12, 5).t(math.cos(a) * 0.3, 0.62, math.sin(a) * 0.3), 'crystal_cyan', 'Base')
    m.add(lathe([(0.34, 0.0), (0.32, 0.12), (0.24, 0.26), (0.12, 0.33), (0.0, 0.35)], 24, cap_bottom=False, cap_top=False).t(0, 0.28, 0), 'ice', 'Base')
    m.add(kit.band(0.34, 0.27, 0.03, 0.012, 16), 'gold', 'Base')
    m.add(cylinder(0.08, 0.14, 8).t(0, 0.26, 0), 'iron', 'Base')
    m.node('Core', 'Turret', (0, 0.48, 0))
    m.add(crystal(0.07, 0.24, 6, 0.4, 0.4).t(0, 0.38, 0), 'crystal_cyan', 'Core')
    m.add(torus(0.13, 0.008, 24, 4).t(0, 0.48, 0), 'glow_frost', 'Core')
    m.nodes['Turret'].pos = (0, 0.48, 0)
    spin(m, 'Core', 4.0)
    bob(m, 'Turret', 0.02, 2.4)
    pulse(m, 'Core', 0.9, 1.3, 0.5, 'Attack')
    return m


# ====================================================================== Fusionen

@tower
def silencehall():
    m = T('silencehall', 'Stillezitadelle', 1.0, (0, 1.2, 0))
    legendary_base(m, mat='stone_dark', trim='gold')
    m.add(kit.body(0.36, 0.3, 0.7, 8, 0.16, flare=0.04), 'stone_blocks', 'Base')
    m.add(kit.band(0.32, 0.5, 0.04, 0.015), 'gold', 'Base')
    m.add(kit.slits(0.32, 0.6, 4, math.pi / 4, 0.04, 0.14), 'glow_frost', 'Base')
    for i in range(4):
        a = TAU * i / 4 + math.pi / 4
        m.add(rbox(0.07, 0.48, 0.07, 0.015).t(math.cos(a) * 0.24, 1.1, math.sin(a) * 0.24), 'stone_dark', 'Base')
    m.add(kit.roof(0.36, 0.4, 8, 1.33, 0.06, 0.4), 'obsidian', 'Base')
    m.add(spike(0.03, 0.22, 6).t(0, 1.7, 0), 'gold', 'Base')
    m.add(lathe([(0.0, 0.0), (0.04, 0.0), (0.1, 0.03), (0.13, 0.12), (0.16, 0.24), (0.19, 0.26), (0.0, 0.26)], 16, cap_bottom=False).rx(math.pi).t(0, 1.26, 0), 'gold', 'Turret')
    m.add(sphere(0.04, seg=8).t(0, 0.98, 0), 'iron', 'Turret')
    m.node('Ring', 'Turret', (0, 1.1, 0))
    m.add(torus(0.3, 0.012, 36, 5).t(0, 1.1, 0), 'glow_frost', 'Ring')
    m.add(merge([rbox(0.04, 0.04, 0.015, 0.004).t(math.cos(TAU * k / 8) * 0.3, 1.1, math.sin(TAU * k / 8) * 0.3) for k in range(8)]), 'gold', 'Ring')
    m.nodes['Turret'].pos = (0, 1.26, 0)
    m.anim_rot('Idle', 'Turret', [(0, (1, 0, 0), -4), (1.5, (1, 0, 0), 4), (3, (1, 0, 0), -4)])
    m.anim_rot('Attack', 'Turret', [(0, (1, 0, 0), 0), (0.15, (1, 0, 0), 22), (0.4, (1, 0, 0), -14), (0.8, (1, 0, 0), 0)])
    spin(m, 'Ring', 6.0)
    return m


@tower
def banecircle():
    m = T('banecircle', 'Bannkreis', 0.9, (0, 1.1, 0))
    legendary_base(m, mat='obsidian', trim='gold')
    m.add(cylinder(0.4, 0.02, 24).t(0, 0.16, 0), 'runes', 'Base')
    for i in range(7):
        a = TAU * i / 7
        m.add(kit.rune_stone(0.1, 0.32 + 0.06 * (i % 2), 0.07, seed=20 + i).ry(-a).t(math.cos(a) * 0.36, 0.16, math.sin(a) * 0.36), 'stone_dark', 'Base')
        m.add(crystal(0.02, 0.07, 5).t(math.cos(a) * 0.36, 0.52 + 0.06 * (i % 2), math.sin(a) * 0.36), 'crystal_violet', 'Base')
    pole = lathe([(0.07, 0.0), (0.06, 0.4), (0.075, 0.45), (0.05, 0.75), (0.0, 0.8)], 8).t(0, 0.16, 0)
    m.add(pole, 'wood_black', 'Base')
    m.add(kit.skull(0.08).t(0, 0.66, 0.05), 'bone', 'Base')
    m.add(merge([kit.horn(0.14, 0.02, 1.0).rz(sg * 1.2).t(sg * 0.07, 0.72, 0) for sg in (1, -1)]), 'bone', 'Base')
    m.node('Ring', 'Turret', (0, 1.1, 0))
    m.add(torus(0.24, 0.01, 32, 4).t(0, 1.1, 0), 'glow_arcane', 'Ring')
    m.add(merge([crystal(0.02, 0.07, 4).rz(math.pi / 2).ry(-TAU * k / 6).t(math.cos(TAU * k / 6) * 0.24, 1.1, math.sin(TAU * k / 6) * 0.24) for k in range(6)]), 'crystal_violet', 'Ring')
    m.add(sphere(0.06, seg=10).t(0, 1.1, 0), 'glow_arcane', 'Turret')
    m.nodes['Turret'].pos = (0, 1.1, 0)
    spin(m, 'Ring', 4.0, turns=-1)
    bob(m, 'Turret', 0.04, 3.0)
    pulse(m, 'Ring', 0.8, 1.4, 0.5, 'Attack')
    return m


@tower
def acidmortar():
    m = T('acidmortar', 'Säuremörser', 0.55, (0, 1.0, 0.25))
    legendary_base(m, mat='stone_dark', trim='gold')
    m.add(kit.body(0.42, 0.4, 0.3, 8, 0.16, flare=0.02), 'stone_blocks', 'Base')
    m.add(kit.crenels(0.38, 0.46, 8, 0.13, 0.09, 0.08), 'stone_blocks', 'Base')
    m.add(cylinder(0.3, 0.08, 16, bevel=0.015).t(0, 0.46, 0), 'iron', 'Turret')
    barrel = lathe([(0.0, -0.05), (0.16, -0.05), (0.2, 0.05), (0.15, 0.2), (0.13, 0.42), (0.16, 0.45), (0.16, 0.5), (0.1, 0.5), (0.1, 0.05)], 14, cap_top=False)
    m.add(barrel.rx(-0.6).t(0, 0.62, 0.0), 'steel', 'Turret')
    m.add(cylinder(0.1, 0.01, 12).t(0, 0.47, 0).rx(-0.6).t(0, 0.62, 0), 'glow_poison', 'Turret')
    tank = merge([sphere(0.12, 0.18, 0.12, 14).t(0, 0.75, -0.25)])
    m.add(tank, 'crystal_green', 'Turret')
    m.add(merge([torus(0.12, 0.012, 16, 4).t(0, y, -0.25) for y in (0.66, 0.84)]), 'gold', 'Turret')
    m.add(tube(curve([(0, 0.9, -0.25), (0, 1.0, -0.1), (0, 0.82, 0.05)], 10), 0.022, 6), 'iron', 'Turret')
    for sg in (1, -1):
        m.add(rbox(0.05, 0.22, 0.24, 0.012).t(sg * 0.2, 0.6, 0.0), 'iron_rust', 'Turret')
    idle_scan(m, deg=18, T_=7)
    recoil(m, back=0.08, t=0.7)
    return m


@tower
def plaguepot():
    m = T('plaguepot', 'Pestkessel', 0.4, (0, 0.85, 0.2))
    legendary_base(m, mat='obsidian', trim='gold')
    m.add(displace(cylinder(0.36, 0.18, 14).t(0, 0.16, 0), 0.015, 9), 'rock', 'Base')
    bowl, coals = kit.brazier(0.22, 0.2)
    m.add(coals, 'ember', 'Base')
    m.add(kettle(0.32, 0.36).t(0, 0.32, 0), 'iron', 'Turret')
    m.add(kit.band(0.33, 0.55, 0.04, 0.015, 16), 'gold', 'Turret')
    m.add(cylinder(0.28, 0.02, 18).t(0, 0.66, 0), 'glow_poison', 'Turret')
    for i in range(7):
        a = TAU * i / 7 + 0.4
        r = 0.3 + 0.03 * (i % 2)
        h = 0.08 + 0.04 * (i % 3)
        m.add(cylinder(0.015, h, 6).t(math.cos(a) * r, 0.66, math.sin(a) * r), 'bone', 'Turret')
        m.add(lathe([(0.0, 0.0), (0.05, 0.0), (0.045, 0.025), (0.0, 0.04)], 10).t(math.cos(a) * r, 0.66 + h, math.sin(a) * r), 'flesh_moss', 'Turret')
    m.add(merge([sphere(0.035 + 0.01 * (k % 2), seg=8).t(math.cos(k * 2.4) * 0.15, 0.69, math.sin(k * 2.4) * 0.15) for k in range(7)]), 'crystal_green', 'Turret')
    m.add(kit.skull(0.08).t(0, 0.5, 0.33), 'bone', 'Turret')
    m.nodes['Turret'].pos = (0, 0.32, 0)
    m.anim_rot('Idle', 'Turret', [(0, (0, 0, 1), 0), (2, (0, 0, 1), 2), (4, (0, 0, 1), 0), (6, (0, 0, 1), -2), (8, (0, 0, 1), 0)])
    m.anim_rot('Attack', 'Turret', [(0, (1, 0, 0), 0), (0.15, (1, 0, 0), 18), (0.6, (1, 0, 0), 0)])
    return m


@tower
def quakemaw():
    m = T('quakemaw', 'Bebenschlund', 1.1, (0, 0.4, 0))
    legendary_base(m, mat='stone_dark', trim='gold')
    crater = displace(torus(0.33, 0.1, 24, 8).s(1, 0.7, 1).t(0, 0.2, 0), 0.02, 7)
    m.add(crater, 'rock', 'Base')
    m.add(cylinder(0.26, 0.02, 20).t(0, 0.16, 0), 'void', 'Base')
    m.add(torus(0.22, 0.012, 32, 4).t(0, 0.19, 0), 'glow_arcane', 'Base')
    for i in range(5):
        a = TAU * i / 5 + 0.2
        m.add(displace(icosphere(0.06, 1).t(math.cos(a) * 0.38, 0.32, math.sin(a) * 0.38), 0.01, 10), 'rock', 'Base')
    head = merge([rbox(0.36, 0.22, 0.28, 0.035).t(0, 1.1, 0), rbox(0.4, 0.05, 0.32, 0.015).t(0, 0.98, 0), rbox(0.4, 0.05, 0.32, 0.015).t(0, 1.22, 0)])
    m.add(head, 'obsidian', 'Turret')
    m.add(merge([rbox(0.04, 0.14, 0.29, 0.01).t(sg * 0.16, 1.1, 0) for sg in (1, -1)]), 'gold', 'Turret')
    m.add(sphere(0.06, seg=10).t(0, 1.1, 0.15), 'glow_arcane', 'Turret')
    m.node('Orbit', 'Turret', (0, 1.1, 0))
    m.add(merge([displace(icosphere(0.04, 1).t(math.cos(TAU * k / 4) * 0.32, 1.1, math.sin(TAU * k / 4) * 0.32), 0.008, 10) for k in range(4)]), 'rock', 'Orbit')
    m.nodes['Turret'].pos = (0, 0.0, 0)
    spin(m, 'Orbit', 3.0)
    m.anim_pos('Idle', 'Turret', [(0, (0, 0, 0)), (1.5, (0, 0.06, 0)), (3, (0, 0, 0))])
    m.anim_pos('Attack', 'Turret', [(0, (0, 0.12, 0)), (0.1, (0, -0.55, 0)), (0.25, (0, -0.5, 0)), (0.9, (0, 0, 0))])
    return m


@tower
def cyclone():
    m = T('cyclone', 'Zyklonturm', 1.3, (0, 1.6, 0.2))
    legendary_base(m, mat='stone_pale', trim='gold')
    m.add(kit.body(0.3, 0.2, 1.15, 8, 0.16, flare=0.05), 'stone_pale', 'Base')
    m.add(kit.band(0.27, 0.55, 0.04, 0.015), 'gold', 'Base')
    m.add(kit.slits(0.26, 0.8, 4, 0.0, 0.04, 0.16), 'glow_storm', 'Base')
    m.add(kit.roof(0.24, 0.22, 8, 1.3, 0.03, 0.1), 'obsidian', 'Base')
    m.add(rbox(0.17, 0.17, 0.32, 0.03).t(0, 1.36, 0.0), 'steel', 'Turret')
    m.node('Rotor', 'Turret', (0, 1.38, 0.21))
    sail_rotor(m, 'Rotor', 0.48, 4, 'cloth_blue', 'gold')
    m.node('Cloud', 'Turret', (0, 1.95, 0))
    cl = merge([displace(icosphere(0.14 + 0.03 * (k % 2), 2).t(math.cos(TAU * k / 5) * 0.18, 1.95 + 0.03 * (k % 3), math.sin(TAU * k / 5) * 0.18), 0.02, 6, seed=k) for k in range(5)])
    m.add(cl, 'stone_dark', 'Cloud')
    m.add(merge([crystal(0.02, 0.18, 4).rx(math.pi).t(math.cos(TAU * k / 3) * 0.12, 1.86, math.sin(TAU * k / 3) * 0.12) for k in range(3)]), 'glow_storm', 'Cloud')
    spin(m, 'Rotor', 2.4, axis=(0, 0, 1))
    spin(m, 'Rotor', 0.5, axis=(0, 0, 1), clip='Attack')
    spin(m, 'Cloud', 8.0)
    idle_scan(m, deg=15, T_=9)
    return m


@tower
def stormbattery():
    m = T('stormbattery', 'Sturmbatterie', 1.0, (0, 1.2, 0.4))
    legendary_base(m, mat='stone_dark', trim='steel')
    m.add(kit.body(0.36, 0.3, 0.8, 8, 0.16, flare=0.03), 'stone_pale', 'Base')
    m.add(kit.band(0.33, 0.5, 0.04, 0.015), 'steel', 'Base')
    m.add(kit.crenels(0.31, 0.96, 8, 0.12, 0.1, 0.07), 'stone_pale', 'Base')
    m.add(cylinder(0.27, 0.08, 16, bevel=0.015).t(0, 0.98, 0), 'steel', 'Turret')
    for sg in (1, -1):
        b = lathe([(0.05, 0.0), (0.045, 0.45), (0.06, 0.48), (0.035, 0.48)], 10, cap_top=False).rx(math.pi / 2 - 0.5).t(sg * 0.1, 1.12, -0.05)
        m.add(b, 'steel', 'Turret')
        m.add(rbox(0.06, 0.16, 0.22, 0.015).t(sg * 0.1, 1.1, -0.05), 'iron', 'Turret')
    m.add(rbox(0.12, 0.14, 0.18, 0.02).t(0, 1.12, -0.1), 'iron_rust', 'Turret')
    m.node('Rotor', 'Turret', (0, 1.2, -0.26))
    sail_rotor(m, 'Rotor', 0.3, 4, 'cloth_blue', 'steel')
    spin(m, 'Rotor', 1.5, axis=(0, 0, 1))
    idle_scan(m, deg=30, T_=6)
    recoil(m, back=0.04, t=0.25)
    return m


@tower
def falcon():
    m = T('falcon', 'Falkenauge', 1.5, (0, 0.2, 0.6))
    legendary_base(m, mat='stone_dark', trim='gold')
    m.add(kit.body(0.32, 0.22, 1.3, 6, 0.16, flare=0.04), 'stone_blocks', 'Base')
    m.add(kit.buttresses(0.32, 3, 0.6, 0.08, 0.12, 0.0, 0.16), 'stone_dark', 'Base')
    m.add(kit.band(0.27, 0.8, 0.04, 0.015, 6), 'gold', 'Base')
    m.add(kit.slits(0.26, 1.0, 3, 0.5, 0.035, 0.14), 'glow_fire', 'Base')
    m.add(cylinder(0.3, 0.05, 6, bevel=0.01).t(0, 1.45, 0), 'gold', 'Base')
    m.add(kit.crenels(0.28, 1.5, 6, 0.12, 0.1, 0.07), 'stone_blocks', 'Turret')
    m.add(rbox(0.08, 0.08, 0.85, 0.012).t(0, 1.62, 0.15), 'wood_black', 'Turret')
    for y in (1.6, 1.68):
        limbs, string = kit.bow(0.7)
        m.add(limbs.rz(math.pi / 2).t(0, y, 0.5), 'gold', 'Turret')
        m.add(string.rz(math.pi / 2).t(0, y, 0.5), 'leather_black', 'Turret')
    m.add(merge([crystal(0.03, 0.3, 4, 0.5, 0.3).rz(sg * 1.2).t(sg * 0.12, 1.75, -0.12) for sg in (1, -1)]), 'gold', 'Turret')
    m.add(kit.skull(0.07).t(0, 1.74, -0.05), 'bone', 'Turret')
    m.nodes['Turret'].pos = (0, 1.5, 0)
    m.nodes['Muzzle'].pos = (0, 1.64, 0.62)
    idle_scan(m, deg=25, T_=9)
    recoil(m, back=0.06, t=0.6)
    return m


@tower
def sunlance():
    m = T('sunlance', 'Sonnenlanze', 1.5, (0, 1.9, 0))
    legendary_base(m, mat='stone_pale', trim='gold')
    m.add(kit.body(0.3, 0.2, 1.25, 8, 0.16, flare=0.05), 'stone_pale', 'Base')
    m.add(kit.band(0.27, 0.6, 0.04, 0.015), 'gold', 'Base')
    m.add(kit.band(0.22, 1.2, 0.04, 0.015), 'gold', 'Base')
    m.add(kit.slits(0.26, 0.9, 4, 0.0, 0.04, 0.18), 'crystal_gold', 'Base')
    m.add(lathe([(0.2, 0.0), (0.28, 0.06), (0.24, 0.1), (0.0, 0.11)], 12).t(0, 1.4, 0), 'gold', 'Base')
    m.node('Petals', 'Turret', (0, 1.6, 0))
    pet = merge([rbox(0.1, 0.32, 0.015, 0.006).t(0, 0.16, 0).rx(-0.5).ry(-TAU * k / 6).t(math.cos(TAU * k / 6) * 0.14, 1.5, math.sin(TAU * k / 6) * 0.14) for k in range(6)])
    m.add(pet, 'steel', 'Petals')
    m.add(torus(0.13, 0.02, 24, 6).t(0, 1.82, 0), 'gold', 'Turret')
    m.add(sphere(0.12, 0.04, 0.12, 16).t(0, 1.82, 0), 'crystal_gold', 'Turret')
    m.add(sphere(0.05, seg=10).t(0, 1.95, 0), 'glow_fire', 'Turret')
    m.nodes['Turret'].pos = (0, 1.5, 0)
    spin(m, 'Petals', 10.0)
    pulse(m, 'Turret', 0.95, 1.08, 0.6, 'Attack')
    return m


@tower
def order():
    m = T('order', 'Ordensburg', 1.05, (0, 1.2, 0.3))
    legendary_base(m, mat='stone_dark', trim='gold')
    m.add(kit.body(0.38, 0.36, 0.62, 4, 0.16, flare=0.02).ry(math.pi / 4), 'stone_blocks', 'Base')
    m.add(kit.crenels(0.34, 0.78, 8, 0.12, 0.1, 0.07).ry(math.pi / 8), 'stone_blocks', 'Base')
    for sx, sz in ((0.3, 0.3), (-0.3, 0.3)):
        m.add(cylinder(0.11, 0.8, 10).t(sx, 0.16, sz), 'stone_blocks', 'Base')
        m.add(kit.roof(0.13, 0.25, 10, 0.96, 0.03, 0.3).t(sx, 0, sz), 'cloth_red', 'Base')
    m.add(rbox(0.2, 0.3, 0.04, 0.02).t(0, 0.31, 0.37), 'wood_black', 'Base')
    m.add(merge([rbox(0.22, 0.025, 0.02, 0.004).t(0, 0.22 + k * 0.09, 0.4) for k in range(3)]), 'gold', 'Base')
    for sg in (1, -1):
        cloth, _ = kit.banner(0.12, 0.3)
        m.add(cloth.t(sg * 0.2, 0.7, 0.385), 'cloth_red', 'Base')
    # Kapellenturm mit Glocke (Turret)
    m.add(rbox(0.22, 0.32, 0.22, 0.02).t(0, 0.95, -0.08), 'stone_pale', 'Turret')
    m.add(kit.roof(0.18, 0.35, 4, 1.11, 0.03, 0.2).ry(math.pi / 4).t(0, 0, -0.08), 'obsidian', 'Turret')
    m.add(merge([rbox(0.015, 0.16, 0.015, 0).t(0, 1.55, -0.08), rbox(0.09, 0.015, 0.015, 0).t(0, 1.58, -0.08)]), 'gold', 'Turret')
    m.node('Flag', 'Turret', (0, 1.0, 0.04))
    m.add(kit.banner(0.14, 0.24)[0].t(0, 1.08, 0.04), 'cloth_violet', 'Flag')
    m.anim_rot('Idle', 'Flag', [(0, (1, 0, 0), -5), (1.4, (1, 0, 0), 5), (2.8, (1, 0, 0), -5)])
    m.anim_rot('Attack', 'Flag', [(0, (1, 0, 0), 0), (0.2, (1, 0, 0), -20), (0.6, (1, 0, 0), 0)])
    return m


@tower
def treasury():
    m = T('treasury', 'Schatzkammer (2×2)', 0.95, (0, 1.2, 0))
    m.notes = 'Belegt 2×2 Felder (Grundfläche ≈ 1,9 m).'
    m.add(kit.plinth(0.92, 0.14, 4).ry(math.pi / 4), 'obsidian', 'Base')
    m.add(kit.band(0.85, 0.14, 0.03, 0.012, 4).ry(math.pi / 4), 'gold', 'Base')
    m.add(rbox(1.2, 0.62, 1.1, 0.04).t(0, 0.45, -0.1), 'stone_blocks', 'Base')
    m.add(rbox(1.26, 0.06, 1.16, 0.015).t(0, 0.78, -0.1), 'gold', 'Base')
    m.add(lathe([(0.48, 0.0), (0.46, 0.12), (0.36, 0.3), (0.2, 0.42), (0.0, 0.46)], 24).t(0, 0.8, -0.1), 'gold', 'Base')
    m.add(spike(0.04, 0.25, 6).t(0, 1.25, -0.1), 'gold', 'Base')
    m.add(rbox(0.36, 0.44, 0.05, 0.03).t(0, 0.36, 0.46), 'iron', 'Base')
    m.add(torus(0.08, 0.02, 16, 6).rx(math.pi / 2).t(0, 0.38, 0.5), 'gold', 'Base')
    for x in (-0.45, 0.45):
        m.add(cylinder(0.07, 0.7, 10, bevel=0.01).t(x, 0.14, 0.47), 'stone_pale', 'Base')
    # Goldhaufen
    for (x, z, s) in ((0.6, 0.7, 1.0), (-0.65, 0.65, 0.8), (0.75, 0.3, 0.6)):
        m.add(displace(sphere(0.18 * s, 0.1 * s, 0.18 * s, 12).t(x, 0.16, z), 0.01, 20), 'gold', 'Base')
        m.add(kit.crystal_cluster(4, 0.03 * s, 0.12 * s, 0.05 * s, seed=int(abs(x) * 10 + z * 10) + 5).t(x, 0.2, z), 'crystal_gold', 'Base')
    # Truhe mit Deckel (Turret)
    m.add(rbox(0.3, 0.18, 0.2, 0.02).t(-0.55, 0.23, 0.3), 'wood', 'Base')
    m.node('Lid', 'Turret', (-0.55, 0.32, 0.2))
    m.add(rbox(0.31, 0.06, 0.21, 0.02).t(-0.55, 0.35, 0.3), 'wood_black', 'Lid')
    m.add(sphere(0.07, seg=10).t(-0.55, 0.3, 0.3), 'glow_fire', 'Turret')
    m.nodes['Turret'].pos = (0, 0.0, 0)
    m.anim_rot('Idle', 'Lid', [(0, (1, 0, 0), 0), (2.5, (1, 0, 0), -20), (5, (1, 0, 0), 0)])
    m.anim_rot('Attack', 'Lid', [(0, (1, 0, 0), 0), (0.3, (1, 0, 0), -60), (1.2, (1, 0, 0), 0)])
    return m


@tower
def deepdrill():
    m = T('deepdrill', 'Tiefbohrer (2×2)', 0.95, (0, 0.3, 0))
    m.notes = 'Belegt 2×2 Felder (Grundfläche ≈ 1,9 m).'
    m.add(kit.plinth(0.92, 0.12, 4).ry(math.pi / 4), 'stone_dark', 'Base')
    m.add(kit.boulders(10, 0.9, 0.07, seed=31), 'rock', 'Base')
    m.add(kit.planks_platform(0.8, 0.13, 0.04, 12), 'wood_black', 'Base')
    for i in range(4):
        a = TAU * i / 4 + math.pi / 4
        m.add(kit.beam((math.cos(a) * 0.62, 0.15, math.sin(a) * 0.62), (math.cos(a) * 0.12, 1.55, math.sin(a) * 0.12), 0.06), 'wood', 'Base')
    for y in (0.55, 1.0):
        r = 0.62 - (y - 0.15) / 1.4 * 0.5
        m.add(torus(r, 0.02, 4, 4).ry(math.pi / 4).t(0, y, 0), 'iron', 'Base')
    m.add(rbox(0.36, 0.1, 0.36, 0.02).t(0, 1.58, 0), 'iron_rust', 'Base')
    m.add(kit.gear(0.14, 0.04, 12).rx(math.pi / 2).t(0.25, 1.62, 0.0), 'iron', 'Base')
    # Förderband und Lore
    m.add(rbox(0.2, 0.05, 0.8, 0.01).rx(-0.35).t(0.6, 0.35, 0.25), 'leather_black', 'Base')
    cart = merge([rbox(0.24, 0.14, 0.3, 0.015).t(0.6, 0.23, 0.72)] + [cylinder(0.04, 0.02, 10).rz(math.pi / 2).t(0.6 + sg * 0.13, 0.17, 0.72 + dz) for sg in (1, -1) for dz in (-0.09, 0.09)])
    m.add(cart, 'iron_rust', 'Base')
    m.add(kit.crystal_cluster(6, 0.04, 0.12, 0.06, seed=3).t(0.6, 0.3, 0.72), 'crystal_gold', 'Base')
    m.add(kit.crystal_cluster(5, 0.05, 0.16, 0.07, seed=8).t(-0.62, 0.15, 0.55), 'crystal_gold', 'Base')
    # Bohrstange (Turret: hebt und senkt sich, dreht)
    m.node('Drill', 'Turret', (0, 0.6, 0))
    m.add(cylinder(0.08, 1.0, 12).t(0, 0.5, 0), 'steel', 'Drill')
    m.add(merge([lathe([(0.18, 0.0), (0.0, -0.32)], 10, cap_bottom=False)]).t(0, 0.52, 0), 'gold', 'Drill')
    m.add(cylinder(0.16, 0.08, 12, bevel=0.01).t(0, 1.4, 0), 'iron', 'Drill')
    m.add(tube(curve([(0.12, 0.45, 0), (0, 0.6, 0.12), (-0.12, 0.75, 0), (0, 0.9, -0.12), (0.12, 1.05, 0)], 16), 0.03, 5), 'iron_rust', 'Drill')
    m.nodes['Turret'].pos = (0, 0.0, 0)
    spin(m, 'Drill', 1.2)
    m.anim_pos('Attack', 'Turret', [(0, (0, 0, 0)), (0.12, (0, -0.25, 0)), (0.8, (0, 0, 0))])
    return m


@tower
def shieldnet():
    m = T('shieldnet', 'Schildnetz', 1.25, (0, 1.4, 0))
    legendary_base(m, mat='stone_pale', trim='gold')
    for i in range(3):
        a = TAU * i / 3
        m.add(kit.beam((math.cos(a) * 0.36, 0.16, math.sin(a) * 0.36), (math.cos(a) * 0.06, 1.25, math.sin(a) * 0.06), 0.06), 'gold', 'Base')
    m.add(lathe([(0.38, 0.0), (0.36, 0.12), (0.28, 0.26), (0.15, 0.34), (0.0, 0.36)], 24, cap_bottom=False, cap_top=False).t(0, 0.16, 0), 'ice', 'Base')
    for y in (0.5, 0.85):
        r = 0.3 - (y - 0.16) / 1.1 * 0.22
        m.add(torus(r, 0.014, 24, 5).t(0, y, 0), 'glow_frost', 'Base')
    m.add(kit.crystal_cluster(5, 0.06, 0.28, 0.06, seed=12).t(0, 1.25, 0), 'crystal_cyan', 'Turret')
    m.node('Ring', 'Turret', (0, 1.4, 0))
    m.add(torus(0.2, 0.012, 32, 5).rx(0.5).t(0, 1.4, 0), 'glow_frost', 'Ring')
    m.add(torus(0.16, 0.01, 32, 5).rz(0.6).t(0, 1.4, 0), 'gold', 'Ring')
    m.nodes['Turret'].pos = (0, 1.25, 0)
    spin(m, 'Ring', 3.0)
    bob(m, 'Turret', 0.03, 2.6)
    pulse(m, 'Ring', 0.9, 1.35, 0.5, 'Attack')
    return m


# ====================================================================== Lane-Einheiten

@tower
def soldier():
    m = T('soldier', 'Kasernensoldat', 0.4, (0, 0.9, 0.25))
    m.notes = 'Lane-Einheit der Wachkaserne (kein baubarer Turm).'
    figure(m, 1.0, armor='iron', tabard='cloth_red', helm='iron')
    return m


@tower
def knight():
    m = T('knight', 'Ordensritter', 0.4, (0, 0.9, 0.25))
    m.notes = 'Lane-Einheit der Ordensburg (kein baubarer Turm).'
    figure(m, 1.1, armor='steel', tabard='cloth_violet', helm='steel', knight=True)
    return m
