"""Turmmodelle (Dark Fantasy). Jede Funktion liefert ein Model mit den Knoten
Base (statisch), Turret (dreht sich zum Ziel, Y-Achse) und Muzzle (Austrittspunkt der Geschosse).
Animationen: Idle (Schleife), Attack (einmalig). Maße: 1 Einheit = 1 Spielfeld (1 m), Grundfläche ≤ 0,95 m."""
from __future__ import annotations

import math

import numpy as np

from . import kit
from .geo import (TAU, crystal, curve, cylinder, displace, extrude, icosphere, lathe, merge, rbox, ring_array, sphere,
                  spike, torus, tube)
from .model import Model
from .sdf import Sculpt, rot

REG = {}


def tower(fn):
    REG[fn.__name__] = fn
    return fn


def T(name, title, turret_y=0.8, muzzle=(0, 1.0, 0.3)):
    m = Model(name, 'tower', title)
    m.node('Base', None, (0, 0, 0))
    m.node('Turret', 'Base', (0, turret_y, 0))
    m.node('Muzzle', 'Turret', muzzle)
    return m


def recoil(m, node='Turret', back=0.06, t=0.5):
    m.anim_pos('Attack', node, [(0, (0, 0, 0)), (0.06, (0, 0, -back)), (t, (0, 0, 0))])


def bob(m, node, amp=0.03, T_=3.0, spin=0.0):
    m.anim_pos('Idle', node, [(T_ * i / 8, (0, amp * math.sin(TAU * i / 8), 0)) for i in range(9)])
    if spin:
        m.anim_rot('Idle', node, [(T_ * i / 4, (0, 1, 0), spin * 90 * i) for i in range(5)])


def idle_scan(m, node='Turret', deg=25, T_=6.0):
    m.anim_rot('Idle', node, [(0, (0, 1, 0), 0), (T_ * 0.25, (0, 1, 0), deg), (T_ * 0.75, (0, 1, 0), -deg), (T_, (0, 1, 0), 0)])


def pulse(m, node, lo=0.92, hi=1.08, T_=1.6, clip='Idle'):
    m.anim_scale(clip, node, [(0, lo), (T_ / 2, hi), (T_, lo)])


def stone_base(m, r=0.44, h=0.14, mat='stone_dark', sides=8, trim='iron'):
    m.add(kit.plinth(r, h, sides), mat, 'Base')
    m.add(kit.boulders(7, r * 0.98, 0.055, seed=len(m.name)), 'rock', 'Base')


# ====================================================================== Grundtürme

@tower
def ranger():
    m = T('ranger', 'Waldläuferposten', 0.98, (0, 0.3, 0.32))
    stone_base(m, 0.44, 0.16, 'stone_dark')
    # Steinfuß und Holzgerüst
    m.add(kit.body(0.3, 0.27, 0.32, 8, 0.15, flare=0.03), 'stone_blocks', 'Base')
    m.add(kit.band(0.285, 0.46, 0.03, 0.015), 'iron', 'Base')
    for i in range(4):
        a = TAU * i / 4 + math.pi / 4
        p0 = (math.cos(a) * 0.27, 0.44, math.sin(a) * 0.27)
        p1 = (math.cos(a) * 0.22, 0.95, math.sin(a) * 0.22)
        m.add(kit.beam(p0, p1, 0.06), 'wood_black', 'Base')
        b = TAU * (i + 1) / 4 + math.pi / 4
        q1 = (math.cos(b) * 0.24, 0.72, math.sin(b) * 0.24)
        m.add(kit.beam(p0, q1, 0.035), 'wood_black', 'Base')
    m.add(kit.planks_platform(0.36, 0.97, 0.045, 8), 'wood', 'Base')
    m.add(kit.band(0.36, 0.97, 0.05, 0.012), 'iron_rust', 'Base')
    # drehende Plattform: Brüstung, Dach, Bogenständer
    rail = []
    for i in range(12):
        a = TAU * i / 12
        rail.append(rbox(0.035, 0.18, 0.035, 0.006).t(math.cos(a) * 0.33, 0.1, math.sin(a) * 0.33))
    m.add(merge(rail).t(0, 0.98, 0), 'wood_black', 'Turret')
    m.add(torus(0.33, 0.018, 24, 6).t(0, 1.18, 0), 'wood', 'Turret')
    for i in range(4):
        a = TAU * i / 4 + math.pi / 4
        m.add(kit.beam((math.cos(a) * 0.3, 0.98, math.sin(a) * 0.3), (math.cos(a) * 0.26, 1.45, math.sin(a) * 0.26), 0.04), 'wood_black', 'Turret')
    m.add(kit.roof(0.36, 0.42, 8, 1.44, 0.06, 0.6), 'wood_black', 'Turret')
    m.add(spike(0.02, 0.18, 6).t(0, 1.84, 0), 'iron', 'Turret')
    m.add(kit.skull(0.07).t(0, 1.78, 0.0).t(0, 0, 0.0), 'bone', 'Turret')
    # Bogen auf Gestell
    limbs, string = kit.bow(0.55)
    m.add(limbs.rz(math.pi / 2).t(0, 1.25, 0.3), 'wood', 'Turret')
    m.add(string.rz(math.pi / 2).t(0, 1.25, 0.3), 'leather_black', 'Turret')
    m.add(cylinder(0.006, 0.35, 5).rx(math.pi / 2).t(0, 1.25, 0.14), 'wood', 'Turret')
    m.add(spike(0.012, 0.06, 4).rx(math.pi / 2).t(0, 1.25, 0.48), 'iron', 'Turret')
    for a in (math.pi * 0.25, math.pi * 1.25):
        cloth, _ = kit.banner(0.12, 0.3)
        m.add(cloth.ry(math.pi / 2 - a).t(math.cos(a) * 0.375, 0.955, math.sin(a) * 0.375), 'cloth_red', 'Base')
    m.nodes['Muzzle'].pos = (0, 1.25, 0.5)
    idle_scan(m)
    recoil(m, back=0.03, t=0.35)
    return m


@tower
def ember():
    m = T('ember', 'Glutwerfer', 0.82, (0, 1.2, 0.2))
    stone_base(m, 0.45, 0.14, 'stone_dark')
    m.add(kit.body(0.36, 0.3, 0.6, 8, 0.13, flare=0.05), 'rock_red', 'Base')
    m.add(kit.band(0.33, 0.35, 0.04, 0.018), 'iron_rust', 'Base')
    m.add(kit.band(0.305, 0.68, 0.04, 0.018), 'iron_rust', 'Base')
    m.add(kit.slits(0.33, 0.45, 4, math.pi / 4, 0.05, 0.12), 'ember', 'Base')
    m.add(kit.buttresses(0.36, 4, 0.42, 0.09, 0.12, 0.0, 0.13), 'stone_dark', 'Base')
    # Glutofen-Kuppel
    m.add(lathe([(0.31, 0.0), (0.33, 0.05), (0.26, 0.17), (0.12, 0.23), (0.0, 0.24)], 12).t(0, 0.72, 0), 'iron', 'Base')
    m.add(cylinder(0.07, 0.28, 8, 0.06).t(0.17, 0.82, -0.12), 'stone_dark', 'Base')
    m.add(cylinder(0.05, 0.06, 8).t(0.17, 1.1, -0.12), 'ember', 'Base')
    # Wurfarm (Turret): Lager, Arm, Glutkorb
    m.add(rbox(0.36, 0.08, 0.16, 0.02).t(0, 0.98, 0), 'iron', 'Turret')
    for sg in (1, -1):
        m.add(rbox(0.05, 0.24, 0.08, 0.015).t(sg * 0.15, 1.08, 0), 'iron_rust', 'Turret')
    arm = kit.beam((0, 1.15, -0.25), (0, 1.25, 0.25), 0.05)
    m.add(arm, 'wood_black', 'Turret')
    bowl, coals = kit.brazier(0.09, 0.0)
    m.add(bowl.t(0, 1.24, 0.26), 'iron', 'Turret')
    m.add(coals.t(0, 1.24, 0.26), 'ember', 'Turret')
    m.add(sphere(0.07, seg=10).t(0, 1.12, -0.28), 'iron_rust', 'Turret')  # Gegengewicht
    m.add(kit.spikes_ring(0.31, 0.74, 8, 0.12, 0.016, 0.9), 'iron', 'Base')
    idle_scan(m, deg=15)
    m.anim_rot('Attack', 'Turret', [(0, (1, 0, 0), 0), (0.12, (1, 0, 0), -25), (0.5, (1, 0, 0), 0)])
    return m


@tower
def frost():
    m = T('frost', 'Frostwächter', 1.18, (0, 1.4, 0))
    stone_base(m, 0.44, 0.14, 'stone_pale')
    m.add(kit.body(0.3, 0.2, 0.95, 6, 0.13, flare=0.05), 'stone_pale', 'Base')
    m.add(kit.band(0.27, 0.45, 0.03, 0.012, 6), 'steel', 'Base')
    m.add(kit.band(0.22, 0.98, 0.03, 0.012, 6), 'steel', 'Base')
    m.add(kit.slits(0.27, 0.6, 3, 0.3, 0.04, 0.2), 'runes_cyan', 'Base')
    m.add(kit.crystal_cluster(6, 0.07, 0.4, 0.12, seed=3).t(0.27, 0.12, 0.18), 'ice', 'Base')
    m.add(kit.crystal_cluster(5, 0.05, 0.3, 0.08, seed=4).t(-0.3, 0.12, -0.08), 'ice', 'Base')
    m.add(lathe([(0.2, 0), (0.24, 0.04), (0.16, 0.1), (0.0, 0.12)], 6).t(0, 1.08, 0), 'stone_pale', 'Base')
    # Eiszapfen
    ic = []
    for i in range(9):
        a = TAU * i / 9
        ic.append(spike(0.018, 0.12 + 0.05 * (i % 3), 5).rx(math.pi).t(math.cos(a) * 0.235, 1.0, math.sin(a) * 0.235))
    m.add(merge(ic), 'ice', 'Base')
    # schwebender Frostkern mit Splitterring (Turret)
    m.add(crystal(0.09, 0.42, 6, 0.35, 0.35).t(0, 1.25, 0), 'crystal_cyan', 'Turret')
    shards = []
    for i in range(6):
        a = TAU * i / 6
        shards.append(crystal(0.03, 0.16, 5, 0.4, 0.4).rz(0.5).ry(-a).t(math.cos(a) * 0.24, 0.18, math.sin(a) * 0.24).t(0, 1.18, 0))
    m.add(merge(shards), 'ice', 'Turret')
    m.add(torus(0.24, 0.008, 32, 4).t(0, 1.36, 0), 'crystal_cyan', 'Turret')
    bob(m, 'Turret', 0.03, 3.0, spin=1)
    pulse(m, 'Turret', 0.8, 1.15, 0.6, 'Attack')
    return m


@tower
def storm():
    m = T('storm', 'Sturmrufer', 1.35, (0, 1.53, 0))
    stone_base(m, 0.44, 0.14, 'stone_dark')
    m.add(kit.body(0.27, 0.16, 1.15, 8, 0.13, flare=0.06), 'stone_dark', 'Base')
    for y in (0.4, 0.75, 1.08):
        m.add(kit.band(0.27 - (y - 0.13) / 1.15 * 0.11, y, 0.04, 0.02), 'iron', 'Base')
    # Kupferspulen
    coil = []
    for i in range(40):
        t = i / 39
        coil.append((math.cos(t * TAU * 5) * (0.25 - t * 0.08), 0.3 + t * 0.75, math.sin(t * TAU * 5) * (0.25 - t * 0.08)))
    m.add(tube(np.array(coil), 0.012, 6), 'gold', 'Base')
    m.add(kit.buttresses(0.28, 4, 0.5, 0.08, 0.11, math.pi / 4, 0.13), 'stone_dark', 'Base')
    # Krone aus drei Zinken + Sturmkugel
    prongs = []
    for i in range(3):
        a = TAU * i / 3
        p = curve([(math.cos(a) * 0.12, 0, math.sin(a) * 0.12), (math.cos(a) * 0.2, 0.18, math.sin(a) * 0.2), (math.cos(a) * 0.1, 0.38, math.sin(a) * 0.1)], 12)
        prongs.append(tube(p, [0.025, 0.018, 0.004], 6))
    m.add(merge(prongs).t(0, 1.27, 0), 'iron', 'Base')
    m.add(sphere(0.085, seg=16).t(0, 1.53, 0), 'glow_storm', 'Turret')
    m.add(torus(0.14, 0.01, 24, 5).rx(0.6).t(0, 1.53, 0), 'crystal_cyan', 'Turret')
    m.add(torus(0.17, 0.008, 24, 5).rz(0.9).t(0, 1.53, 0), 'crystal_cyan', 'Turret')
    bob(m, 'Turret', 0.02, 1.2, spin=2)
    pulse(m, 'Turret', 0.85, 1.25, 0.4, 'Attack')
    return m


def sculpt_into(m, sc, node='Base', voxel=0.012, max_dim=110, target=None):
    m.sculpt(sc, voxel=voxel, max_dim=max_dim, node=node, target=target or 6000)


@tower
def spore():
    m = T('spore', 'Sporenwerfer', 0.9, (0, 1.25, 0.25))
    stone_base(m, 0.43, 0.12, 'stone_dark')
    sc = Sculpt(detail=0.004, dfreq=14, seed=5)
    sc.capsule((0, 0.1, 0), (0, 0.85, 0), 0.3, 0.2, 'bark', k=0.08, noise=0.02)
    for i in range(5):  # Wurzeln
        a = TAU * i / 5 + 0.3
        sc.capsule((math.cos(a) * 0.15, 0.4, math.sin(a) * 0.15), (math.cos(a) * 0.42, 0.13, math.sin(a) * 0.42), 0.09, 0.03, 'bark', k=0.06)
    caps = [(0.22, 0.55, 0.12, 0.13), (-0.2, 0.42, 0.16, 0.1), (0.05, 0.62, -0.25, 0.11), (-0.12, 0.78, -0.1, 0.08)]
    for x, y, z, r in caps:
        sc.capsule((x * 0.6, y - 0.08, z * 0.6), (x, y, z), 0.025, 0.02, 'bone', k=0.02)
        sc.ellipsoid((x, y + 0.02, z), (r, r * 0.4, r), 'flesh_moss', k=0.015)
    sculpt_into(m, sc, 'Base', 0.012)
    pods = []
    for i in range(7):
        a = TAU * i / 7
        pods.append(sphere(0.035, seg=8).t(math.cos(a) * 0.24, 0.3 + (i % 3) * 0.15, math.sin(a) * 0.24))
    m.add(merge(pods), 'glow_poison', 'Base')
    # Werfer: Pilzhut-Mörser
    sc2 = Sculpt(detail=0.003, dfreq=20, seed=6)
    sc2.capsule((0, 0.88, 0), (0, 1.05, 0.08), 0.13, 0.11, 'flesh_moss', k=0.04)
    sc2.ellipsoid((0, 1.12, 0.12), (0.2, 0.08, 0.2), 'chitin_red', k=0.03, R=rot(0, -25, 0))
    sc2.ellipsoid((0, 1.15, 0.2), (0.06, 0.05, 0.06), 'flesh', k=0.01, sub=True)
    sculpt_into(m, sc2, 'Turret', 0.011)
    m.add(sphere(0.055, seg=10).t(0, 1.17, 0.21), 'glow_poison', 'Turret')
    idle_scan(m, deg=20)
    pulse(m, 'Turret', 0.96, 1.04, 2.2)
    m.anim_scale('Attack', 'Turret', [(0, 1), (0.1, (1.15, 0.85, 1.15)), (0.25, (0.92, 1.12, 0.92)), (0.6, 1)])
    return m


@tower
def ballista():
    m = T('ballista', 'Belagerungsarmbrust', 0.62, (0, 0.82, 0.55))
    stone_base(m, 0.45, 0.14, 'stone_dark')
    m.add(kit.body(0.38, 0.34, 0.4, 8, 0.13, flare=0.03), 'stone_blocks', 'Base')
    m.add(kit.crenels(0.33, 0.53, 8, 0.12, 0.1, 0.08), 'stone_blocks', 'Base')
    m.add(kit.band(0.36, 0.3, 0.04, 0.015), 'iron', 'Base')
    # Drehlager
    m.add(cylinder(0.22, 0.08, 16, bevel=0.015).t(0, 0.55, 0), 'iron', 'Turret')
    m.add(kit.gear(0.12, 0.03, 12).ry(math.pi / 2).t(0.16, 0.72, -0.1), 'iron_rust', 'Turret')
    m.add(kit.gear(0.12, 0.03, 12).ry(math.pi / 2).t(-0.16, 0.72, -0.1), 'iron_rust', 'Turret')
    for sg in (1, -1):
        m.add(rbox(0.05, 0.22, 0.24, 0.015).t(sg * 0.12, 0.72, 0), 'wood_black', 'Turret')
    m.add(rbox(0.14, 0.08, 0.85, 0.015).t(0, 0.82, 0.12), 'wood', 'Turret')  # Schiene
    # Bogenarme (Stahl) und Sehne
    for sg in (1, -1):
        p = curve([(0, 0.82, 0.42), (sg * 0.22, 0.84, 0.38), (sg * 0.4, 0.86, 0.22)], 12)
        m.add(tube(p, [0.03, 0.022, 0.014], 6), 'steel', 'Turret')
    m.add(tube(np.array([[0.4, 0.86, 0.22], [0, 0.84, -0.05], [-0.4, 0.86, 0.22]]), 0.004, 4, cap=False), 'leather_black', 'Turret')
    m.add(cylinder(0.014, 0.6, 6).rx(math.pi / 2).t(0, 0.88, -0.05), 'wood', 'Turret')
    m.add(spike(0.03, 0.1, 4).rx(math.pi / 2).t(0, 0.88, 0.55), 'steel', 'Turret')
    m.add(kit.skull(0.08).t(0, 0.85, 0.5), 'bone', 'Turret')
    m.add(kit.spikes_ring(0.2, 0.6, 6, 0.1, 0.014, 1.0), 'iron', 'Turret')
    idle_scan(m, deg=18)
    recoil(m, back=0.05, t=0.6)
    return m


@tower
def rune():
    m = T('rune', 'Runenweber', 1.2, (0, 1.42, 0))
    stone_base(m, 0.42, 0.12, 'stone_dark', sides=6)
    ob = lathe([(0.0, 0.0), (0.2, 0.0), (0.16, 0.9), (0.1, 1.05), (0.0, 1.12)], 4, cap_bottom=False, cap_top=False, a0=math.pi / 4).t(0, 0.12, 0)
    m.add(ob, 'runes', 'Base')
    m.add(kit.band(0.19, 0.25, 0.04, 0.012, 4), 'gold', 'Base')
    for i in range(6):  # stehende Runensteine
        a = TAU * i / 6
        m.add(kit.rune_stone(0.09, 0.24 + 0.06 * (i % 2), 0.05, seed=i).ry(-a + math.pi / 2).t(math.cos(a) * 0.37, 0.12, math.sin(a) * 0.37), 'runes', 'Base')
    m.add(kit.crystal_cluster(1, 0.08, 0.3, 0.0).t(0, 1.3, 0), 'crystal_violet', 'Turret')
    orb = []
    for i in range(4):
        a = TAU * i / 4
        orb.append(kit.rune_stone(0.06, 0.12, 0.03, seed=10 + i).t(0, -0.06, 0).ry(-a).t(math.cos(a) * 0.27, 1.38, math.sin(a) * 0.27))
    m.add(merge(orb), 'runes', 'Turret')
    m.add(torus(0.27, 0.006, 40, 4).t(0, 1.38, 0), 'glow_arcane', 'Turret')
    bob(m, 'Turret', 0.03, 3.0, spin=1)
    pulse(m, 'Turret', 0.9, 1.15, 0.5, 'Attack')
    return m


@tower
def resonator():
    m = T('resonator', 'Resonator', 1.05, (0, 0.85, 0))
    stone_base(m, 0.44, 0.13, 'stone_dark')
    for sg in (1, -1):  # Säulen
        m.add(lathe([(0.07, 0), (0.08, 0.05), (0.06, 0.1), (0.055, 0.85), (0.08, 0.92), (0.08, 0.97), (0, 0.97)], 10).t(sg * 0.3, 0.13, 0), 'stone_pale', 'Base')
    arch = tube(curve([(-0.3, 1.08, 0), (-0.2, 1.25, 0), (0, 1.32, 0), (0.2, 1.25, 0), (0.3, 1.08, 0)], 16), 0.05, 8)
    m.add(arch, 'stone_pale', 'Base')
    m.add(kit.skull(0.08).t(0, 1.3, 0.03), 'bone', 'Base')
    # Glocke (Turret = schwingt)
    bell = lathe([(0.0, 1.05), (0.06, 1.04), (0.1, 0.98), (0.13, 0.82), (0.19, 0.7), (0.2, 0.67), (0.17, 0.67), (0.1, 0.8), (0.0, 0.8)], 20, cap_bottom=False, cap_top=False, smooth_all=True)
    m.add(bell, 'gold', 'Turret')
    m.add(sphere(0.04, seg=10).t(0, 0.69, 0), 'iron', 'Turret')
    m.add(kit.chain((0, 1.28, 0), (0, 1.05, 0), 4, 0.0, 0.014).rz(0), 'iron', 'Base')
    for sg in (1, -1):  # Stimmgabeln
        f = merge([cylinder(0.015, 0.25, 6).t(0, 0.13, 0)] + [cylinder(0.01, 0.3, 6).t(k * 0.04, 0.38, 0) for k in (-1, 1)]
                  + [tube(curve([(-0.04, 0.38, 0), (0, 0.34, 0), (0.04, 0.38, 0)], 8), 0.01, 5)])
        m.add(f.t(sg * 0.33, 0, 0.25), 'steel', 'Base')
    m.anim_rot('Idle', 'Turret', [(0, (0, 0, 1), 0), (1, (0, 0, 1), 3), (2, (0, 0, 1), 0), (3, (0, 0, 1), -3), (4, (0, 0, 1), 0)])
    m.anim_rot('Attack', 'Turret', [(0, (0, 0, 1), 0), (0.12, (0, 0, 1), 22), (0.32, (0, 0, 1), -16), (0.5, (0, 0, 1), 8), (0.8, (0, 0, 1), 0)])
    m.nodes['Turret'].pos = (0, 1.05, 0)
    return m


@tower
def goldmine():
    m = T('goldmine', 'Goldgräber (2×2)', 0.9, (0, 1.0, 0))
    m.notes = 'Belegt 2×2 Felder (Grundfläche ≈ 1,9 m).'
    sc = Sculpt(detail=0.01, dfreq=6, seed=11)
    sc.ellipsoid((0.15, 0.12, -0.2), (0.85, 0.45, 0.7), 'rock', k=0.15, noise=0.03)
    sc.ellipsoid((-0.45, 0.1, 0.25), (0.45, 0.32, 0.5), 'rock', k=0.15, noise=0.03)
    sc.box((0.0, 0.25, 0.45), (0.22, 0.26, 0.4), 'rock', k=0.04, sub=True)
    sculpt_into(m, sc, 'Base', 0.02, 120)
    m.add(kit.planks_platform(0.9, 0.02, 0.03, 12).s(1, 1, 0.98), 'wood_black', 'Base')
    # Stollenrahmen
    for x in (-0.24, 0.24):
        m.add(rbox(0.07, 0.55, 0.07, 0.01).t(x, 0.29, 0.62), 'wood', 'Base')
    m.add(rbox(0.6, 0.08, 0.09, 0.01).t(0, 0.6, 0.62), 'wood', 'Base')
    m.add(rbox(0.36, 0.5, 0.02, 0.0).t(0, 0.29, 0.5), 'void', 'Base')
    m.add(kit.skull(0.08).t(0, 0.66, 0.66), 'bone', 'Base')
    # Gleise und Lore
    for x in (-0.09, 0.09):
        m.add(rbox(0.02, 0.02, 0.9, 0.004).t(x, 0.05, 0.95), 'iron_rust', 'Base')
    for i in range(6):
        m.add(rbox(0.26, 0.02, 0.05, 0.004).t(0, 0.035, 0.6 + i * 0.13), 'wood_black', 'Base')
    cart = merge([rbox(0.24, 0.14, 0.3, 0.015).t(0, 0.15, 1.05)] + [cylinder(0.04, 0.02, 10).rz(math.pi / 2).t(sg * 0.13, 0.08, 1.05 + dz) for sg in (1, -1) for dz in (-0.09, 0.09)])
    m.add(cart, 'iron_rust', 'Base')
    m.add(kit.crystal_cluster(6, 0.04, 0.12, 0.06, seed=2).t(0, 0.22, 1.05), 'crystal_gold', 'Base')
    m.add(kit.crystal_cluster(5, 0.05, 0.18, 0.07, seed=7).t(-0.6, 0.25, 0.55), 'crystal_gold', 'Base')
    # Förderturm mit Rad (Turret)
    for x in (-0.12, 0.12):
        m.add(kit.beam((x + 0.35, 0.35, -0.35), (x * 0.4 + 0.35, 1.05, -0.35), 0.05), 'wood', 'Base')
    wheel = merge([torus(0.18, 0.02, 24, 6).rx(math.pi / 2)] + [rbox(0.02, 0.36, 0.02, 0).rz(a) for a in (0, 1.05, 2.1)] + [cylinder(0.03, 0.08, 10).rx(math.pi / 2).t(0, 0, -0.04)])
    m.add(wheel.t(0.35, 1.0, -0.35), 'iron', 'Turret')
    m.nodes['Turret'].pos = (0.35, 1.0, -0.35)
    m.anim_rot('Idle', 'Turret', [(0, (0, 0, 1), 0), (2, (0, 0, 1), 180), (4, (0, 0, 1), 359)])
    m.anim_rot('Attack', 'Turret', [(0, (0, 0, 1), 0), (0.5, (0, 0, 1), 270), (1.0, (0, 0, 1), 359)])
    return m


@tower
def pylon():
    m = T('pylon', 'Leitpylon', 1.4, (0, 1.55, 0))
    stone_base(m, 0.38, 0.12, 'stone_dark', sides=6)
    for i in range(3):
        a = TAU * i / 3
        m.add(kit.beam((math.cos(a) * 0.3, 0.12, math.sin(a) * 0.3), (math.cos(a) * 0.05, 1.35, math.sin(a) * 0.05), 0.05), 'iron', 'Base')
    for y in (0.5, 0.9, 1.2):
        r = 0.3 - (y - 0.12) / 1.23 * 0.25
        m.add(torus(r, 0.015, 3, 5).ry(math.pi / 6).t(0, y, 0), 'iron', 'Base')
        m.add(crystal(0.03, 0.1, 6, 0.3, 0.3).t(0, y - 0.05, 0).t(r * 0.0, 0, 0), 'crystal_cyan', 'Base')
    m.add(kit.crystal_cluster(4, 0.07, 0.3, 0.05, seed=9).t(0, 1.42, 0), 'crystal_cyan', 'Turret')
    m.add(torus(0.17, 0.012, 32, 5).t(0, 1.58, 0), 'glow_storm', 'Turret')
    bob(m, 'Turret', 0.03, 2.4, spin=1)
    pulse(m, 'Turret', 0.85, 1.2, 0.4, 'Attack')
    return m


@tower
def cannon():
    m = T('cannon', 'Kanonenschmiede', 0.62, (0, 0.82, 0.5))
    stone_base(m, 0.46, 0.14, 'stone_dark')
    m.add(kit.body(0.42, 0.4, 0.38, 8, 0.13, flare=0.02), 'stone_blocks', 'Base')
    m.add(kit.crenels(0.38, 0.51, 8, 0.15, 0.12, 0.09), 'stone_blocks', 'Base')
    m.add(kit.band(0.41, 0.3, 0.05, 0.015), 'iron', 'Base')
    for x, z in ((0.28, 0.25), (-0.3, 0.2), (0.25, -0.3)):
        keg = lathe([(0.06, 0), (0.075, 0.06), (0.06, 0.12), (0, 0.12)], 10).t(x, 0.51, z)
        m.add(keg, 'wood', 'Base')
    # Mörser
    m.add(cylinder(0.2, 0.06, 16, bevel=0.015).t(0, 0.52, 0), 'iron', 'Turret')
    for sg in (1, -1):
        m.add(rbox(0.04, 0.2, 0.22, 0.012).t(sg * 0.15, 0.66, 0), 'iron', 'Turret')
    barrel = lathe([(0.0, -0.25), (0.13, -0.25), (0.15, -0.2), (0.12, -0.1), (0.1, 0.25), (0.12, 0.27), (0.12, 0.33), (0.08, 0.33), (0.08, 0.0)], 16, cap_top=False)
    barrel.rx(-math.pi / 2 + 0.5).t(0, 0.72, 0.0)
    m.add(barrel, 'iron', 'Turret')
    m.add(kit.skull(0.1).t(0, -0.02, 0).rx(-0.2).t(0, 0.82, -0.05), 'bone', 'Turret')
    m.add(torus(0.115, 0.015, 16, 5).rx(math.pi / 2 - 0.5).t(0, 0.72 + 0.12 * math.sin(0.5), 0.12 * math.cos(0.5)), 'gold', 'Turret')
    m.nodes['Muzzle'].pos = (0, 0.72 + 0.33 * math.sin(0.5), 0.33 * math.cos(0.5))
    idle_scan(m, deg=12)
    recoil(m, back=0.08, t=0.7)
    return m


@tower
def riftbinder():
    m = T('riftbinder', 'Rissbinder', 0.95, (0, 0.95, 0.1))
    stone_base(m, 0.44, 0.14, 'obsidian')
    m.add(cylinder(0.32, 0.1, 16, 0.28, bevel=0.02).t(0, 0.14, 0), 'runes', 'Base')
    for sg in (1, -1):
        m.add(spike(0.05, 0.9, 6).t(sg * 0.3, 0.2, 0), 'obsidian', 'Base')
        m.add(kit.chain((sg * 0.3, 0.9, 0), (sg * 0.12, 0.95, 0), 4, 0.03, 0.016), 'iron', 'Base')
    m.add(torus(0.3, 0.035, 32, 8).rx(math.pi / 2).t(0, 0.95, 0), 'obsidian', 'Turret')
    m.add(torus(0.22, 0.02, 32, 6).rx(math.pi / 2).t(0, 0.95, 0), 'gold', 'Turret')
    m.add(sphere(0.21, 0.21, 0.03, 24).t(0, 0.95, 0), 'void', 'Turret')
    for i in range(6):
        a = TAU * i / 6
        m.add(crystal(0.025, 0.1, 5).rz(math.pi / 2 - a).rx(math.pi / 2).t(math.cos(a) * 0.33, 0.95 + math.sin(a) * 0.33, 0), 'crystal_violet', 'Turret')
    m.anim_rot('Idle', 'Turret', [(0, (0, 0, 1), 0), (3, (0, 0, 1), 180), (6, (0, 0, 1), 359)])
    pulse(m, 'Turret', 0.85, 1.15, 0.5, 'Attack')
    return m


@tower
def meteor():
    m = T('meteor', 'Meteorbeschwörer', 1.15, (0, 1.4, 0))
    stone_base(m, 0.45, 0.14, 'stone_dark')
    m.add(kit.body(0.34, 0.28, 0.6, 8, 0.13, flare=0.04), 'stone_blocks', 'Base')
    m.add(kit.parapet(0.3, 0.73, 0.06, 0.06), 'stone_dark', 'Base')
    m.add(kit.band(0.32, 0.45, 0.04, 0.015), 'gold', 'Base')
    # Armillarsphäre
    for i, (ax, r) in enumerate((((1, 0, 0), 0.32), ((0, 0, 1), 0.3), ((1, 0, 1), 0.28))):
        t = torus(r, 0.013, 40, 5)
        if i == 0:
            t.rx(math.pi / 2)
        elif i == 1:
            t.rz(math.pi / 2)
        else:
            t.rx(math.pi / 4).rz(math.pi / 4)
        m.add(t.t(0, 1.15, 0), 'gold', 'Turret')
    for i in range(3):
        a = TAU * i / 3
        m.add(kit.beam((math.cos(a) * 0.28, 0.75, math.sin(a) * 0.28), (math.cos(a) * 0.12, 0.9, math.sin(a) * 0.12), 0.03), 'iron', 'Base')
    rock = displace(icosphere(0.14, 3), 0.025, 9, 4).t(0, 1.15, 0)
    m.add(rock, 'ember', 'Turret')
    bob(m, 'Turret', 0.04, 3.2, spin=1)
    pulse(m, 'Turret', 0.85, 1.2, 0.5, 'Attack')
    return m


@tower
def tesla():
    m = T('tesla', 'Tesla-Spule', 1.45, (0, 1.5, 0))
    stone_base(m, 0.42, 0.12, 'stone_dark')
    m.add(lathe([(0.3, 0), (0.3, 0.1), (0.2, 0.16), (0.12, 0.3), (0.12, 1.2), (0.18, 1.25), (0, 1.25)], 12).t(0, 0.12, 0), 'iron', 'Base')
    coil = [(math.cos(t * TAU * 9) * 0.145, 0.45 + t * 0.8, math.sin(t * TAU * 9) * 0.145) for t in np.linspace(0, 1, 120)]
    m.add(tube(np.array(coil), 0.014, 6), 'gold', 'Base')
    for i in range(4):
        a = TAU * i / 4 + math.pi / 4
        m.add(crystal(0.05, 0.25, 6).t(math.cos(a) * 0.28, 0.12, math.sin(a) * 0.28), 'crystal_cyan', 'Base')
    m.add(torus(0.22, 0.07, 32, 12).t(0, 1.47, 0), 'steel', 'Turret')
    m.add(sphere(0.08, seg=14).t(0, 1.47, 0), 'glow_storm', 'Turret')
    m.add(spike(0.02, 0.14, 6).t(0, 1.5, 0), 'gold', 'Turret')
    m.anim_rot('Idle', 'Turret', [(0, (0, 1, 0), 0), (2, (0, 1, 0), 180), (4, (0, 1, 0), 359)])
    pulse(m, 'Turret', 0.9, 1.15, 0.3, 'Attack')
    return m


@tower
def gravity():
    m = T('gravity', 'Gravitationsbrunnen', 0.9, (0, 0.95, 0))
    stone_base(m, 0.45, 0.12, 'stone_dark')
    well = lathe([(0.38, 0), (0.4, 0.05), (0.38, 0.36), (0.4, 0.4), (0.32, 0.42), (0.3, 0.35), (0.3, 0.1), (0.0, 0.1)], 16, cap_bottom=False, cap_top=False).t(0, 0.12, 0)
    m.add(well, 'stone_blocks', 'Base')
    m.add(sphere(0.29, 0.02, 0.29, 24).t(0, 0.3, 0), 'void', 'Base')
    m.add(kit.band(0.395, 0.45, 0.03, 0.01, 16), 'gold', 'Base')
    m.add(sphere(0.13, seg=18).t(0, 0.95, 0), 'void', 'Turret')
    m.add(torus(0.18, 0.006, 40, 4).rx(0.4).t(0, 0.95, 0), 'glow_arcane', 'Turret')
    rocks = []
    for i in range(5):
        a = TAU * i / 5
        rocks.append(displace(icosphere(0.04 + 0.015 * (i % 2), 1), 0.01, 20, i).t(math.cos(a) * 0.3, 0.95 + 0.08 * math.sin(a * 2), math.sin(a) * 0.3))
    m.add(merge(rocks), 'rock', 'Turret')
    bob(m, 'Turret', 0.03, 4.0, spin=-1)
    m.anim_scale('Attack', 'Turret', [(0, 1), (0.2, 0.75), (0.5, 1.1), (0.8, 1)])
    return m


@tower
def dragon():
    m = T('dragon', 'Drachenhorst', 1.0, (0, 1.2, 0.35))
    sc = Sculpt(detail=0.008, dfreq=7, seed=21)
    sc.capsule((0, 0.0, 0), (0, 0.85, 0), 0.42, 0.22, 'rock_red', k=0.1, noise=0.03)
    for i in range(4):
        a = TAU * i / 4 + 0.6
        sc.capsule((math.cos(a) * 0.2, 0.5, math.sin(a) * 0.2), (math.cos(a) * 0.35, 1.05, math.sin(a) * 0.35), 0.06, 0.015, 'rock_red', k=0.06)
    sc.torus((0, 0.92, 0), 0.24, 0.07, 'wood_black', k=0.03, noise=0.01)  # Nest
    sculpt_into(m, sc, 'Base', 0.016, 110)
    eggs = merge([sphere(0.05, 0.07, 0.05, 10).t(math.cos(a) * 0.1, 0.97, math.sin(a) * 0.1) for a in (0.5, 2.6, 4.4)])
    m.add(eggs, 'crystal_red', 'Base')
    bones = merge([tube(curve([(0.3, 0.2, 0.25), (0.36, 0.3, 0.1), (0.3, 0.42, -0.05)], 8), 0.02, 6),
                   tube(curve([(-0.3, 0.3, 0.2), (-0.38, 0.38, 0.0), (-0.32, 0.5, -0.15)], 8), 0.02, 6)])
    m.add(bones, 'bone', 'Base')
    # Drachenkopf-Schädel (Turret), Maul nach +Z
    sk = Sculpt(detail=0.002, dfreq=25, seed=22)
    sk.ellipsoid((0, 1.12, 0.02), (0.12, 0.1, 0.13), 'bone', k=0.03)
    sk.capsule((0, 1.12, 0.05), (0, 1.1, 0.32), 0.07, 0.045, 'bone', k=0.04)
    sk.capsule((0, 1.04, 0.08), (0, 1.03, 0.3), 0.045, 0.03, 'bone', k=0.03)
    for sg in (1, -1):
        sk.ellipsoid((sg * 0.06, 1.15, 0.12), 0.035, 'bone', k=0.01, sub=True)
        sk.chain([(sg * 0.07, 1.18, -0.05), (sg * 0.15, 1.26, -0.15), (sg * 0.16, 1.33, -0.3)], [0.03, 0.02, 0.004], 'bone', k=0.015)
        for z in (0.18, 0.24, 0.29):
            sk.capsule((sg * 0.035, 1.07, z), (sg * 0.035, 1.03, z + 0.01), 0.008, 0.002, 'bone', k=0.003)
    sculpt_into(m, sk, 'Turret', 0.008, 90)
    m.add(sphere(0.05, seg=10).t(0, 1.09, 0.25), 'glow_fire', 'Turret')
    for sg in (1, -1):
        m.add(sphere(0.022, seg=8).t(sg * 0.06, 1.15, 0.13), 'glow_fire', 'Turret')
    idle_scan(m, deg=30, T_=5)
    m.anim_rot('Attack', 'Turret', [(0, (1, 0, 0), 0), (0.15, (1, 0, 0), -15), (0.35, (1, 0, 0), 10), (0.7, (1, 0, 0), 0)])
    return m


@tower
def laser():
    m = T('laser', 'Kristalllaser', 0.9, (0, 1.32, 0.2))
    stone_base(m, 0.42, 0.12, 'stone_dark', sides=6)
    m.add(kit.body(0.26, 0.2, 0.62, 6, 0.12, flare=0.05), 'stone_dark', 'Base')
    m.add(kit.band(0.25, 0.3, 0.04, 0.015, 6), 'steel', 'Base')
    m.add(kit.slits(0.23, 0.5, 6, 0.0, 0.03, 0.18), 'crystal_cyan', 'Base')
    m.add(cylinder(0.2, 0.06, 6, bevel=0.01).t(0, 0.74, 0), 'steel', 'Turret')
    for i in range(3):
        a = TAU * i / 3
        claw = tube(curve([(math.cos(a) * 0.17, 0.78, math.sin(a) * 0.17), (math.cos(a) * 0.2, 1.0, math.sin(a) * 0.2), (math.cos(a) * 0.08, 1.25, math.sin(a) * 0.08)], 10), [0.03, 0.022, 0.008], 6)
        m.add(claw, 'steel', 'Turret')
    m.add(crystal(0.09, 0.6, 6, 0.3, 0.3).rx(0.5).t(0, 0.95, 0), 'crystal_cyan', 'Turret')
    m.add(torus(0.08, 0.012, 20, 5).rx(math.pi / 2 + 0.5).t(0, 1.2, 0.12), 'gold', 'Turret')
    idle_scan(m, deg=35, T_=5)
    recoil(m, back=0.015, t=0.3)
    return m


@tower
def totem():
    m = T('totem', 'Totemschrein', 1.0, (0, 1.4, 0))
    stone_base(m, 0.42, 0.12, 'stone_dark')
    sc = Sculpt(detail=0.004, dfreq=12, seed=31)
    sc.capsule((0, 0.12, 0), (0, 1.35, 0), 0.17, 0.13, 'wood_black', k=0.03)
    for y, kind in ((0.45, 0), (0.8, 1), (1.15, 0)):
        sc.ellipsoid((0, y, 0.12), (0.12, 0.13, 0.08), 'wood', k=0.04)  # Maske
        for sg in (1, -1):
            sc.ellipsoid((sg * 0.05, y + 0.03, 0.19), 0.025, 'wood', k=0.01, sub=True)
        sc.box((0, y - 0.07, 0.19), (0.05, 0.015, 0.03), 'wood', k=0.01, sub=True)
        if kind:
            for sg in (1, -1):
                sc.capsule((sg * 0.16, y + 0.05, 0.0), (sg * 0.36, y + 0.12, 0.02), 0.04, 0.01, 'wood', k=0.03)
    sculpt_into(m, sc, 'Base', 0.009, 120)
    for y in (0.45, 0.8, 1.15):
        m.add(merge([sphere(0.016, seg=8).t(sg * 0.05, y + 0.03, 0.185) for sg in (1, -1)]), 'glow_blood', 'Base')
    m.add(kit.horn(0.25, 0.035, 1.2).rz(-0.6).t(0.1, 1.38, 0), 'bone', 'Turret')
    m.add(kit.horn(0.25, 0.035, 1.2).rz(0.6).t(-0.1, 1.38, 0), 'bone', 'Turret')
    m.add(kit.skull(0.12).t(0, 1.36, 0.02), 'bone', 'Turret')
    m.add(torus(0.3, 0.006, 40, 4).t(0, 0.2, 0), 'glow_blood', 'Base')
    pulse(m, 'Turret', 0.97, 1.03, 2.0)
    m.anim_scale('Attack', 'Turret', [(0, 1), (0.2, 1.12), (0.6, 1)])
    return m


@tower
def minelayer():
    m = T('minelayer', 'Minenwerfer', 0.55, (0, 0.85, 0.3))
    stone_base(m, 0.45, 0.13, 'stone_dark')
    m.add(lathe([(0.42, 0), (0.42, 0.25), (0.36, 0.38), (0.0, 0.42)], 8, a0=math.pi / 8).t(0, 0.13, 0), 'stone_blocks', 'Base')
    m.add(kit.slits(0.38, 0.3, 4, math.pi / 4, 0.12, 0.05), 'void', 'Base')
    mines = []
    for i, (x, z) in enumerate(((0.3, 0.3), (-0.32, 0.26), (0.3, -0.3), (-0.28, -0.3))):
        mn = merge([sphere(0.06, seg=10)] + [spike(0.012, 0.05, 4).rz(a).ry(b) for a, b in ((0, 0), (1.6, 0), (1.6, 1.6), (1.6, 3.1), (1.6, 4.7), (3.1, 0))])
        mines.append(mn.t(x, 0.2, z))
    m.add(merge(mines), 'iron_rust', 'Base')
    m.add(cylinder(0.2, 0.06, 12, bevel=0.01).t(0, 0.55, 0), 'iron', 'Turret')
    for i in range(3):  # Mörserrohre
        x = (i - 1) * 0.12
        t = lathe([(0.05, 0), (0.06, 0.02), (0.05, 0.3), (0.06, 0.32), (0.045, 0.32), (0.045, 0.05)], 10, cap_top=False).rx(-0.75).t(x, 0.6, -0.05)
        m.add(t, 'iron', 'Turret')
    idle_scan(m, deg=10)
    recoil(m, back=0.04, t=0.5)
    return m


@tower
def skyguard():
    m = T('skyguard', 'Flakbatterie', 0.62, (0, 1.1, 0.25))
    stone_base(m, 0.45, 0.13, 'stone_dark')
    m.add(kit.body(0.38, 0.36, 0.38, 8, 0.13, flare=0.03), 'stone_blocks', 'Base')
    m.add(kit.spikes_ring(0.36, 0.5, 8, 0.16, 0.018, -0.3), 'iron', 'Base')
    m.add(cylinder(0.24, 0.07, 16, bevel=0.015).t(0, 0.52, 0), 'iron', 'Turret')
    for sg in (1, -1):
        m.add(rbox(0.05, 0.3, 0.2, 0.012).t(sg * 0.2, 0.72, 0), 'iron', 'Turret')
    m.add(cylinder(0.12, 0.22, 12).rz(math.pi / 2).t(0.11, 0.8, 0), 'steel', 'Turret')
    for i in range(6):  # Rohrbündel, steil nach oben
        a = TAU * i / 6
        b = cylinder(0.022, 0.5, 8).t(math.cos(a) * 0.07, 0, math.sin(a) * 0.07).rx(0.75).t(0, 0.78, 0)
        m.add(b, 'iron', 'Turret')
    m.add(torus(0.1, 0.015, 16, 5).rx(0.75).t(0, 0.78 + 0.45 * math.cos(0.75), 0.45 * math.sin(0.75)), 'gold', 'Turret')
    m.nodes['Muzzle'].pos = (0, 0.78 + 0.5 * math.cos(0.75), 0.5 * math.sin(0.75))
    idle_scan(m, deg=30, T_=4)
    m.anim_rot('Attack', 'Turret', [(0, (0, 1, 0), 0), (0.05, (0, 1, 0), 3), (0.1, (0, 1, 0), -3), (0.15, (0, 1, 0), 2), (0.3, (0, 1, 0), 0)])
    return m


@tower
def barricade():
    m = T('barricade', 'Barrikade', 0.3, (0, 0.3, 0))
    m.add(kit.boulders(5, 0.38, 0.06, seed=3), 'rock', 'Base')
    for z in (-0.25, 0.0, 0.25):  # spanischer Reiter
        m.add(cylinder(0.04, 0.9, 8).rz(math.pi / 2).t(0.45, 0.32, z), 'wood_black', 'Base')
    for x in (-0.3, 0.0, 0.3):
        for sg in (1, -1):
            m.add(kit.beam((x, 0.0, sg * 0.32), (x, 0.62, -sg * 0.32), 0.035), 'wood', 'Base')
            m.add(spike(0.018, 0.08, 4).rx(sg * -0.9).t(x, 0.62, -sg * 0.32), 'iron', 'Base')
    m.add(kit.chain((-0.4, 0.42, 0.3), (0.4, 0.42, 0.3), 10, 0.06, 0.016), 'iron_rust', 'Base')
    m.add(kit.skull(0.08).t(0.0, 0.6, 0.04), 'bone', 'Base')
    return m


@tower
def lanebarricade():
    m = T('lanebarricade', 'Lane-Barrikade', 0.2, (0, 0.2, 0))
    m.notes = 'Wird auf den Weg gebaut; Gegner werden verlangsamt.'
    for i in range(5):
        x = -0.4 + i * 0.2
        m.add(cylinder(0.035, 0.45 + 0.08 * (i % 2), 6).t(x, 0, 0.0).rx(0.15 * (1 if i % 2 else -1)), 'wood_black', 'Base')
        m.add(spike(0.035, 0.1, 6).t(x, 0.45 + 0.08 * (i % 2), 0.0), 'wood_black', 'Base')
    m.add(kit.chain((-0.42, 0.3, 0.05), (0.42, 0.3, 0.05), 12, 0.1, 0.015), 'iron_rust', 'Base')
    m.add(kit.chain((-0.42, 0.18, -0.05), (0.42, 0.18, -0.05), 12, 0.06, 0.015), 'iron_rust', 'Base')
    for x in (-0.3, 0.1, 0.35):
        m.add(merge([spike(0.02, 0.12, 4).rz(a) for a in (0.6, -0.6)] + [spike(0.02, 0.12, 4).rx(a) for a in (0.6, -0.6)]).t(x, 0.0, 0.3), 'iron', 'Base')
    m.add(kit.skull(0.07).t(0.2, 0.0, 0.25), 'bone', 'Base')
    m.add(kit.boulders(4, 0.4, 0.05, seed=8).s(1, 1, 0.5), 'rock', 'Base')
    return m


# ====================================================================== Fusionstürme (legendär)

def legendary_base(m, r=0.47, mat='obsidian', trim='gold'):
    m.add(kit.plinth(r, 0.16, 8), mat, 'Base')
    m.add(kit.band(r * 0.9, 0.16, 0.03, 0.012), trim, 'Base')
    m.add(kit.boulders(8, r * 0.98, 0.05, seed=len(m.name) + 3), 'rock', 'Base')


@tower
def plasma():
    m = T('plasma', 'Plasmawerfer', 0.8, (0, 1.12, 0.45))
    legendary_base(m)
    m.add(kit.body(0.36, 0.3, 0.55, 8, 0.16, flare=0.04), 'stone_dark', 'Base')
    m.add(kit.buttresses(0.36, 4, 0.45, 0.09, 0.12, math.pi / 4, 0.16), 'obsidian', 'Base')
    m.add(kit.band(0.33, 0.45, 0.04, 0.015), 'gold', 'Base')
    m.add(cylinder(0.24, 0.08, 16, bevel=0.015).t(0, 0.72, 0), 'steel', 'Turret')
    for sg in (1, -1):
        m.add(rbox(0.05, 0.28, 0.3, 0.015).t(sg * 0.18, 0.92, 0), 'steel', 'Turret')
    m.add(sphere(0.12, seg=18).t(0, 1.0, 0.05), 'glow_storm', 'Turret')
    for i, a in enumerate((0, 1.0, 2.1)):
        t = torus(0.16, 0.014, 28, 6).rx(math.pi / 2).ry(a)
        m.add(t.t(0, 1.0, 0.05), 'gold', 'Turret')
    barrel = lathe([(0.07, 0), (0.08, 0.05), (0.05, 0.35), (0.07, 0.38), (0.04, 0.38)], 10, cap_top=False).rx(math.pi / 2).t(0, 1.05, 0.1)
    m.add(barrel, 'steel', 'Turret')
    m.add(merge([torus(0.06, 0.01, 14, 5).t(0, 1.05, 0.2 + k * 0.08) for k in range(3)]).rx(0), 'crystal_cyan', 'Turret')
    idle_scan(m, deg=20)
    recoil(m, back=0.05, t=0.45)
    pulse(m, 'Muzzle', 0.9, 1.1, 0.8)
    return m


@tower
def glacier():
    m = T('glacier', 'Gletscherkanone', 0.78, (0, 1.05, 0.5))
    legendary_base(m, mat='stone_pale', trim='steel')
    sc = Sculpt(detail=0.005, dfreq=9, seed=41)
    sc.capsule((0, 0.42, 0), (0, 0.62, 0), 0.36, 0.3, 'ice', k=0.06, noise=0.03)
    rng = np.random.default_rng(4)
    for i in range(7):
        a = TAU * i / 7 + rng.random()
        sc.capsule((math.cos(a) * 0.25, 0.3, math.sin(a) * 0.25), (math.cos(a) * 0.42, 0.75 + rng.random() * 0.3, math.sin(a) * 0.42), 0.07, 0.01, 'ice', k=0.04)
    sculpt_into(m, sc, 'Base', 0.014)
    m.add(cylinder(0.22, 0.07, 16, bevel=0.015).t(0, 0.72, 0), 'steel', 'Turret')
    barrel = lathe([(0.0, -0.22), (0.15, -0.22), (0.17, -0.15), (0.13, 0.0), (0.11, 0.42), (0.14, 0.45), (0.14, 0.5), (0.09, 0.5), (0.09, 0.0)], 14, cap_top=False)
    m.add(barrel.rx(-math.pi / 2 + 0.25).t(0, 0.92, 0.0), 'steel', 'Turret')
    m.add(kit.crystal_cluster(5, 0.05, 0.22, 0.07, seed=5).t(0, 0.98, -0.12), 'crystal_cyan', 'Turret')
    m.add(merge([spike(0.02, 0.1, 5).rx(math.pi).t(math.cos(a) * 0.13, 0.86, math.sin(a) * 0.13 + 0.25) for a in np.linspace(0, TAU, 7)[:-1]]), 'ice', 'Turret')
    idle_scan(m, deg=15)
    recoil(m, back=0.08, t=0.7)
    return m


@tower
def voidlance():
    m = T('voidlance', 'Leerenlanze', 1.25, (0, 1.4, 0.45))
    legendary_base(m)
    spire = lathe([(0.3, 0), (0.26, 0.2), (0.12, 0.9), (0.06, 1.08), (0.0, 1.12)], 5, cap_bottom=False, cap_top=False, a0=0.3).t(0, 0.16, 0)
    m.add(spire, 'obsidian', 'Base')
    for i in range(5):
        a = TAU * i / 5 + 0.3
        m.add(spike(0.04, 0.5, 5).rz(-0.35).ry(-a).t(math.cos(a) * 0.26, 0.16, math.sin(a) * 0.26), 'obsidian', 'Base')
    m.add(kit.slits(0.2, 0.6, 5, 0.3, 0.03, 0.3), 'void', 'Base')
    # schwebende Lanze
    lance = merge([lathe([(0.0, -0.45), (0.035, -0.35), (0.03, 0.2), (0.07, 0.25), (0.0, 0.55)], 6, cap_bottom=False, cap_top=False)])
    m.add(lance.rx(math.pi / 2).t(0, 1.42, 0.05), 'void', 'Turret')
    m.add(merge([torus(0.08, 0.01, 18, 5).t(0, 1.42, 0.05 + z) for z in (-0.25, 0.0)]), 'gold', 'Turret')
    m.add(sphere(0.06, seg=12).t(0, 1.42, 0.05), 'glow_arcane', 'Turret')
    bob(m, 'Turret', 0.03, 3)
    recoil(m, back=-0.15, t=0.6)
    return m


@tower
def starfall():
    m = T('starfall', 'Sternenfall', 1.2, (0, 1.45, 0))
    legendary_base(m, mat='stone_dark')
    m.add(kit.body(0.32, 0.22, 0.85, 8, 0.16, flare=0.05), 'stone_dark', 'Base')
    m.add(kit.band(0.3, 0.4, 0.04, 0.015), 'gold', 'Base')
    m.add(kit.band(0.24, 0.98, 0.04, 0.015), 'gold', 'Base')
    m.add(kit.slits(0.27, 0.7, 4, 0.0, 0.04, 0.16), 'crystal_gold', 'Base')
    m.add(lathe([(0.24, 0), (0.28, 0.05), (0.18, 0.12), (0.0, 0.14)], 8).t(0, 1.01, 0), 'gold', 'Base')
    star = merge([crystal(0.08, 0.3, 4, 0.5, 0.5).t(0, -0.1, 0).rz(a) for a in (0, math.pi / 2)] + [crystal(0.08, 0.3, 4, 0.5, 0.5).t(0, -0.1, 0).rx(math.pi / 2)])
    m.add(star.t(0, 1.45, 0), 'crystal_gold', 'Turret')
    sh = []
    for i in range(5):
        a = TAU * i / 5
        sh.append(crystal(0.025, 0.1, 4, 0.5, 0.5).t(math.cos(a) * 0.3, 1.45 + 0.05 * math.sin(a * 3), math.sin(a) * 0.3))
    m.add(merge(sh), 'crystal_gold', 'Turret')
    m.add(torus(0.3, 0.005, 48, 4).t(0, 1.45, 0), 'glow_white', 'Turret')
    bob(m, 'Turret', 0.04, 4, spin=1)
    pulse(m, 'Turret', 0.85, 1.2, 0.6, 'Attack')
    return m


@tower
def thornwood():
    m = T('thornwood', 'Dornenhain', 1.0, (0, 1.2, 0))
    m.add(kit.boulders(8, 0.42, 0.06, seed=9), 'rock', 'Base')
    sc = Sculpt(detail=0.006, dfreq=10, seed=51)
    rng = np.random.default_rng(7)
    sc.capsule((0, 0.0, 0), (0.03, 0.75, -0.02), 0.2, 0.11, 'bark', k=0.06, noise=0.02)
    for i in range(6):  # Wurzeln
        a = TAU * i / 6 + rng.random() * 0.4
        sc.chain([(math.cos(a) * 0.1, 0.25, math.sin(a) * 0.1), (math.cos(a) * 0.3, 0.1, math.sin(a) * 0.3), (math.cos(a) * 0.45, 0.0, math.sin(a) * 0.45)], [0.08, 0.05, 0.02], 'bark', k=0.05)
    for i in range(5):  # verdrehte Äste
        a = TAU * i / 5 + 0.5
        p0 = (0.03 + math.cos(a) * 0.06, 0.7, math.sin(a) * 0.06)
        p1 = (math.cos(a) * 0.3, 1.0 + rng.random() * 0.15, math.sin(a) * 0.3)
        p2 = (math.cos(a + 0.6) * 0.42, 1.25 + rng.random() * 0.2, math.sin(a + 0.6) * 0.42)
        sc.chain([p0, p1, p2], [0.07, 0.04, 0.01], 'bark', k=0.04)
    sculpt_into(m, sc, 'Base', 0.012, target=8000)
    # Dornen
    th = []
    for i in range(40):
        y = 0.15 + rng.random() * 0.75
        a = rng.random() * TAU
        r = 0.2 - y * 0.1
        th.append(spike(0.014, 0.08, 4).rz(-1.3).ry(-a).t(math.cos(a) * r, y, math.sin(a) * r))
    m.add(merge(th), 'bone', 'Base')
    # rotes Herz (Turret)
    m.add(displace(icosphere(0.11, 2), 0.015, 12, 3).t(0, 1.12, 0), 'crystal_red', 'Turret')
    vines = tube(np.array([(math.cos(t * 9) * 0.14, 1.04 + t * 0.18, math.sin(t * 9) * 0.14) for t in np.linspace(0, 1, 40)]), 0.012, 5)
    m.add(vines, 'flesh_moss', 'Turret')
    pulse(m, 'Turret', 0.93, 1.07, 1.1)
    m.anim_scale('Attack', 'Turret', [(0, 1), (0.15, 1.25), (0.5, 1)])
    return m


@tower
def thunderspire():
    m = T('thunderspire', 'Donnerspitze', 1.6, (0, 1.78, 0))
    legendary_base(m, mat='stone_dark', trim='steel')
    m.add(kit.body(0.3, 0.12, 1.45, 6, 0.16, flare=0.06), 'stone_dark', 'Base')
    for y in (0.45, 0.85, 1.25):
        m.add(kit.band(0.3 - (y - 0.16) / 1.45 * 0.18, y, 0.04, 0.02, 6), 'steel', 'Base')
    for i in range(6):  # Seitenzinken
        a = TAU * i / 6
        p = curve([(math.cos(a) * 0.25, 0.5, math.sin(a) * 0.25), (math.cos(a) * 0.4, 0.9, math.sin(a) * 0.4), (math.cos(a) * 0.3, 1.25, math.sin(a) * 0.3)], 12)
        m.add(tube(p, [0.03, 0.02, 0.004], 6), 'iron', 'Base')
        m.add(sphere(0.025, seg=8).t(math.cos(a) * 0.3, 1.25, math.sin(a) * 0.3), 'glow_storm', 'Base')
    m.add(sphere(0.12, seg=18).t(0, 1.78, 0), 'glow_storm', 'Turret')
    for a in (0.3, 1.4, 2.4):
        m.add(torus(0.2, 0.01, 30, 5).rx(a).rz(a * 0.7).t(0, 1.78, 0), 'crystal_cyan', 'Turret')
    bob(m, 'Turret', 0.03, 1.6, spin=2)
    pulse(m, 'Turret', 0.85, 1.3, 0.35, 'Attack')
    return m


@tower
def riftgate():
    m = T('riftgate', 'Rissportal', 0.75, (0, 0.85, 0.1))
    legendary_base(m)
    m.add(cylinder(0.36, 0.08, 8, 0.32, bevel=0.02).t(0, 0.16, 0), 'runes', 'Base')
    arch = []
    for sg in (1, -1):
        arch.append(lathe([(0.08, 0), (0.07, 0.1), (0.06, 0.9), (0.08, 0.95), (0, 0.95)], 6).t(sg * 0.33, 0.24, 0))
    m.add(merge(arch), 'obsidian', 'Base')
    top = tube(curve([(-0.33, 1.15, 0), (-0.15, 1.38, 0), (0.0, 1.45, 0), (0.15, 1.38, 0), (0.33, 1.15, 0)], 16), [0.07, 0.06, 0.05, 0.06, 0.07], 8)
    m.add(top, 'obsidian', 'Base')
    m.add(crystal(0.06, 0.2, 5).t(0, 1.43, 0), 'crystal_violet', 'Base')
    m.add(kit.skull(0.09).t(0, 1.33, 0.05), 'bone', 'Base')
    m.add(sphere(0.25, 0.4, 0.025, 24).t(0, 0.78, 0), 'void', 'Turret')
    m.add(torus(0.27, 0.012, 40, 5).s(1, 1.6, 1).rx(math.pi / 2).t(0, 0.78, 0), 'glow_arcane', 'Turret')
    stones = merge([kit.rune_stone(0.05, 0.08, 0.03, seed=i).t(0, -0.04, 0).t(math.cos(a) * 0.42, 0.78 + math.sin(a) * 0.5, 0.0) for i, a in enumerate(np.linspace(0, TAU, 7)[:-1])])
    m.add(stones, 'runes', 'Turret')
    m.anim_rot('Idle', 'Turret', [(0, (0, 0, 1), 0), (4, (0, 0, 1), 180), (8, (0, 0, 1), 359)])
    m.nodes['Turret'].pos = (0, 0.78, 0)
    pulse(m, 'Turret', 0.85, 1.12, 0.5, 'Attack')
    return m


@tower
def phoenix():
    m = T('phoenix', 'Phönixhorst', 1.05, (0, 1.4, 0.2))
    legendary_base(m, mat='rock_red', trim='gold')
    m.add(kit.body(0.34, 0.26, 0.7, 8, 0.16, flare=0.05), 'rock_red', 'Base')
    m.add(kit.band(0.3, 0.55, 0.04, 0.015), 'gold', 'Base')
    m.add(kit.slits(0.3, 0.4, 4, math.pi / 4, 0.05, 0.16), 'ember', 'Base')
    bowl, coals = kit.brazier(0.3, 0.86)
    m.add(bowl, 'gold', 'Base')
    m.add(coals, 'ember', 'Base')
    # Phönix (Bildhauer): Körper, Hals, Kopf, ausgebreitete Flügel
    sc = Sculpt(detail=0.002, dfreq=30, seed=61)
    y = 1.22
    sc.ellipsoid((0, y, 0), (0.08, 0.12, 0.09), 'glow_fire', k=0.03, R=rot(0, 20, 0))
    sc.capsule((0, y + 0.08, 0.04), (0, y + 0.22, 0.1), 0.04, 0.03, 'glow_fire', k=0.03)
    sc.ellipsoid((0, y + 0.25, 0.12), (0.04, 0.04, 0.05), 'glow_fire', k=0.02)
    sc.capsule((0, y + 0.24, 0.16), (0, y + 0.22, 0.22), 0.012, 0.002, 'gold', k=0.005)
    for sg in (1, -1):
        for k in range(6):  # Schwungfedern als flache Ellipsoide, fächerförmig nach oben
            t = k / 5
            c = (sg * (0.1 + 0.2 * t), y + 0.12 + 0.18 * math.sin(t * 2.2), -0.04 - 0.05 * t)
            sc.ellipsoid(c, (0.11, 0.018, 0.05), 'crystal_red', k=0.03, R=rot(0, 0, sg * (35 - 30 * t)))
        sc.ellipsoid((sg * 0.06, y + 0.04, 0.0), (0.06, 0.05, 0.05), 'crystal_red', k=0.03)
        sc.ellipsoid((sg * 0.05, y - 0.16, -0.16), (0.03, 0.12, 0.02), 'crystal_red', k=0.03, R=rot(sg * 20, 50, 0))
    sculpt_into(m, sc, 'Turret', 0.008, target=5000)
    bob(m, 'Turret', 0.04, 2.0)
    m.anim_scale('Attack', 'Turret', [(0, 1), (0.2, 1.15), (0.6, 1)])
    return m


@tower
def cathedral():
    m = T('cathedral', 'Klangkathedrale', 1.25, (0, 1.2, 0))
    legendary_base(m, mat='stone_dark')
    m.add(rbox(0.5, 0.55, 0.62, 0.02).t(0, 0.44, 0), 'stone_blocks', 'Base')
    roof = extrude([(-0.28, 0), (0.28, 0), (0, 0.32)], 0.64).t(0, 0.71, 0)
    m.add(roof, 'stone_dark', 'Base')
    for x, z in ((0.27, 0.33), (-0.27, 0.33), (0.27, -0.33), (-0.27, -0.33)):
        m.add(lathe([(0.07, 0), (0.07, 0.75), (0.09, 0.78), (0.0, 1.0)], 8).t(x, 0.16, z), 'stone_blocks', 'Base')
    m.add(rbox(0.16, 0.3, 0.02, 0.01).t(0, 0.45, 0.32), 'crystal_violet', 'Base')  # Rosettenfenster
    m.add(torus(0.08, 0.015, 16, 5).rx(math.pi / 2).t(0, 0.55, 0.33), 'gold', 'Base')
    pipes = merge([cylinder(0.025, 0.25 + 0.05 * k, 8).t(-0.12 + 0.06 * k, 0.72, 0.25) for k in range(5)])
    m.add(pipes, 'gold', 'Base')
    # Glockenturm (Turret)
    m.add(lathe([(0.12, 0), (0.12, 0.3), (0.14, 0.32), (0.0, 0.62)], 8).t(0, 1.0, 0), 'stone_dark', 'Base')
    bell = lathe([(0.0, 0.14), (0.05, 0.13), (0.08, 0.06), (0.1, 0.0), (0.0, 0.03)], 16, smooth_all=True).t(0, 1.1, 0)
    m.add(bell, 'gold', 'Turret')
    m.nodes['Turret'].pos = (0, 1.24, 0)
    m.anim_rot('Idle', 'Turret', [(0, (1, 0, 0), 0), (1.5, (1, 0, 0), 4), (3, (1, 0, 0), 0), (4.5, (1, 0, 0), -4), (6, (1, 0, 0), 0)])
    m.anim_rot('Attack', 'Turret', [(0, (1, 0, 0), 0), (0.15, (1, 0, 0), 25), (0.4, (1, 0, 0), -18), (0.7, (1, 0, 0), 8), (1.0, (1, 0, 0), 0)])
    return m


@tower
def stormeye():
    m = T('stormeye', 'Sturmauge', 1.25, (0, 1.25, 0.15))
    legendary_base(m)
    m.add(lathe([(0.18, 0), (0.2, 0.05), (0.08, 0.3), (0.07, 0.75), (0.14, 0.82), (0.0, 0.84)], 8).t(0, 0.16, 0), 'obsidian', 'Base')
    for i in range(3):
        a = TAU * i / 3
        m.add(tube(curve([(math.cos(a) * 0.1, 0.9, math.sin(a) * 0.1), (math.cos(a) * 0.3, 1.1, math.sin(a) * 0.3), (math.cos(a) * 0.22, 1.42, math.sin(a) * 0.22)], 12), [0.03, 0.02, 0.005], 6), 'iron', 'Base')
    sc = Sculpt(detail=0.0015, dfreq=40, seed=71)
    sc.ellipsoid((0, 1.25, 0), (0.15, 0.15, 0.15), 'bone', k=0.02)
    sc.ellipsoid((0, 1.25, 0.12), (0.07, 0.07, 0.04), 'crystal_cyan', k=0.01)
    sc.ellipsoid((0, 1.25, 0.15), (0.022, 0.05, 0.02), 'void', k=0.004)
    for sg in (1, -1):
        sc.ellipsoid((0, 1.25 + sg * 0.09, 0.08), (0.14, 0.04, 0.08), 'flesh', k=0.02, R=rot(0, sg * 25, 0))
    sculpt_into(m, sc, 'Turret', 0.007, target=4000)
    m.add(torus(0.25, 0.01, 30, 5).rx(0.3).t(0, 1.25, 0), 'glow_storm', 'Turret')
    idle_scan(m, deg=40, T_=4)
    m.anim_scale('Attack', 'Turret', [(0, 1), (0.1, 1.1), (0.4, 1)])
    return m


@tower
def sporemine():
    m = T('sporemine', 'Sporenminenfeld', 0.3, (0, 0.4, 0))
    sc = Sculpt(detail=0.008, dfreq=8, seed=81)
    sc.ellipsoid((0, 0.0, 0), (0.46, 0.2, 0.46), 'flesh_moss', k=0.1, noise=0.02)
    rng = np.random.default_rng(3)
    spots = []
    for i in range(7):
        a = TAU * i / 7 + rng.random() * 0.4
        r = 0.18 + rng.random() * 0.2
        x, z = math.cos(a) * r, math.sin(a) * r
        y = 0.16 - r * 0.3
        sc.capsule((x, y - 0.05, z), (x, y + 0.08, z), 0.025, 0.02, 'bone', k=0.02)
        sc.ellipsoid((x, y + 0.11, z), (0.07, 0.035, 0.07), 'chitin_red', k=0.015)
        spots.append((x, y + 0.14, z))
    sculpt_into(m, sc, 'Base', 0.012, target=7000)
    m.add(merge([sphere(0.03, seg=8).t(*p) for p in spots]), 'glow_poison', 'Base')
    m.add(merge([sphere(0.045, seg=10).t(math.cos(a) * 0.33, 0.05, math.sin(a) * 0.33) for a in np.linspace(0, TAU, 6)[:-1]]), 'glow_poison', 'Turret')
    pulse(m, 'Turret', 0.85, 1.15, 1.6)
    m.anim_scale('Attack', 'Turret', [(0, 1), (0.1, 1.5), (0.5, 1)])
    m.nodes['Turret'].pos = (0, 0.05, 0)
    return m


@tower
def skywatch():
    m = T('skywatch', 'Himmelswacht', 1.3, (0, 1.55, 0.3))
    legendary_base(m, mat='stone_pale', trim='steel')
    m.add(kit.body(0.32, 0.2, 1.1, 6, 0.16, flare=0.05), 'stone_pale', 'Base')
    m.add(kit.band(0.29, 0.5, 0.04, 0.015, 6), 'steel', 'Base')
    m.add(kit.band(0.22, 1.18, 0.04, 0.015, 6), 'steel', 'Base')
    m.add(kit.crystal_cluster(6, 0.07, 0.42, 0.1, seed=12).t(0.28, 0.16, 0.12), 'ice', 'Base')
    m.add(kit.crystal_cluster(5, 0.05, 0.3, 0.08, seed=13).t(-0.25, 0.16, -0.2), 'ice', 'Base')
    m.add(cylinder(0.22, 0.06, 12, bevel=0.012).t(0, 1.26, 0), 'steel', 'Turret')
    for sg in (1, -1):  # zwei Eisballisten, steil nach oben
        x = sg * 0.13
        m.add(rbox(0.06, 0.06, 0.5, 0.012).rx(-0.7).t(x, 1.45, 0.05), 'steel', 'Turret')
        bow = tube(curve([(x - 0.15, 1.52, 0.2), (x, 1.62, 0.28), (x + 0.15, 1.52, 0.2)], 10), [0.012, 0.02, 0.012], 6)
        m.add(bow, 'ice', 'Turret')
        m.add(crystal(0.02, 0.3, 5).rx(math.pi / 2 - 0.7).t(x, 1.5, 0.12), 'crystal_cyan', 'Turret')
    m.add(crystal(0.08, 0.35, 6, 0.3).t(0, 1.32, -0.08), 'crystal_cyan', 'Turret')
    idle_scan(m, deg=30, T_=4)
    recoil(m, back=0.04, t=0.4)
    return m
