"""Sci-Fi-Thema „Sternenwacht“: Turmmodelle (Gegenstücke zu allen Fantasy-Türmen, gleiche Knoten und Clips).
Kennung = 'sf_' + Fantasy-Kennung. Gleicher Aufbau wie dm/towers.py: Base (fest), Turret (dreht zum Ziel um Y),
Muzzle (Austritt). Drehende Teile hängen an eigenen Kindknoten (der Spielcode setzt die Turret-Drehung beim Zielen).
Alles steht auf y ≥ 0."""
from __future__ import annotations

import math

import numpy as np

from . import kit
from .geo import TAU, crystal, curve, cylinder, displace, icosphere, lathe, merge, rbox, sphere, spike, torus, tube
from .model import Model
from .towers import bob, idle_scan, pulse, recoil

REG = {}


def tower(fn):
    REG['sf_' + fn.__name__] = fn
    return fn


def T(name, title, turret_y=0.8, muzzle=(0, 1.0, 0.3)):
    m = Model('sf_' + name, 'tower', title)
    m.node('Base', None, (0, 0, 0))
    m.node('Turret', 'Base', (0, turret_y, 0))
    m.node('Muzzle', 'Turret', muzzle)
    return m


def spin(m, node, T_=4.0, axis=(0, 1, 0), clip='Idle', turns=1):
    m.anim_rot(clip, node, [(T_ * i / 4, axis, turns * 90 * i) for i in range(5)])


# ---------------------------------------------------------------- Bauteile

def base(m, r=0.43, h=0.12, mat='hull_dark', glow='glow_cyan', sides=6, feet=True):
    """Sechseckiger Sockel mit Leuchtfuge und Stützfüßen."""
    m.add(kit.plinth(r, h, sides, bevel=0.02), mat, 'Base')
    m.add(kit.band(r * 0.93, h * 0.6, 0.018, 0.008, sides), glow, 'Base')
    if feet:
        for i in range(sides):
            a = TAU * (i + 0.5) / sides
            m.add(rbox(0.07, 0.05, 0.12, 0.01).ry(-a).t(math.cos(a) * r * 0.98, 0.025, math.sin(a) * r * 0.98), 'gunmetal', 'Base')


def lbase(m, r=0.47, mat='hull_white', glow='glow_magenta'):
    """Sockel der Fusionen: zweistufig, mit Schubdüsen an den Ecken."""
    m.add(kit.plinth(r, 0.1, 8, bevel=0.02), 'hull_dark', 'Base')
    m.add(kit.plinth(r * 0.86, 0.16, 8, bevel=0.02), mat, 'Base')
    m.add(kit.band(r * 0.95, 0.06, 0.02, 0.008), glow, 'Base')
    m.add(kit.band(r * 0.8, 0.15, 0.015, 0.008), glow, 'Base')
    for i in range(4):
        a = TAU * i / 4 + math.pi / 4
        m.add(cylinder(0.05, 0.09, 10, bevel=0.01).t(math.cos(a) * r * 0.9, 0.0, math.sin(a) * r * 0.9), 'gunmetal', 'Base')
        m.add(cylinder(0.03, 0.01, 10).t(math.cos(a) * r * 0.9, 0.09, math.sin(a) * r * 0.9), glow, 'Base')


def column(r0, r1, h, y=0.0, sides=6):
    return kit.body(r0, r1, h, sides, y, flare=0.02)


def ring(y, r, t=0.015, mat_mesh=True):
    return torus(r, t, 32, 5).t(0, y, 0)


def barrel(L=0.4, r=0.035, brake=True):
    """Lauf entlang +Z (Ursprung = Verschluss)."""
    prof = [(r * 1.4, 0.0), (r * 1.4, 0.06), (r, 0.08), (r, L * 0.85)]
    if brake:
        prof += [(r * 1.5, L * 0.86), (r * 1.5, L), (r * 0.7, L)]
    else:
        prof += [(r * 0.8, L)]
    return lathe(prof, 12, cap_top=False).rx(math.pi / 2)


def coil(r=0.12, y0=0.0, y1=0.4, n=5, t=0.018):
    return merge([torus(r, t, 24, 5).t(0, y0 + (y1 - y0) * i / max(1, n - 1), 0) for i in range(n)])


def dish(r=0.2, d=0.08):
    return lathe([(0.0, 0.0), (r * 0.35, 0.005), (r * 0.7, d * 0.4), (r, d), (r * 0.97, d * 1.05), (r * 0.66, d * 0.5), (0.0, 0.02)], 20)


def tank(r=0.08, h=0.25):
    return sphere(r, h / 2, r, 14).t(0, h / 2, 0)


def antenna(h=0.3, r=0.008):
    return merge([cylinder(r, h, 6), sphere(r * 2.4, seg=8).t(0, h, 0)])


def pod(w=0.3, h=0.18, d=0.34):
    return rbox(w, h, d, 0.04)


def vents(r, y, n=4, phase=0.0, w=0.04, h=0.1):
    return kit.slits(r, y, n, phase, w, h)


def lid_head(m, y, w=0.3, h=0.16, d=0.34, mat='hull_white', node='Turret'):
    m.add(pod(w, h, d).t(0, y + h / 2, 0), mat, node)


# ================================================================ Grundtürme

@tower
def ranger():
    m = T('ranger', 'Pulsgeschütz', 0.72, (0, 0.82, 0.42))
    base(m)
    m.add(column(0.27, 0.23, 0.55, 0.12), 'hull_white', 'Base')
    m.add(vents(0.255, 0.35, 3, 0.4), 'glow_cyan', 'Base')
    m.add(kit.band(0.25, 0.62, 0.03, 0.012, 6), 'gunmetal', 'Base')
    m.add(cylinder(0.2, 0.06, 16, bevel=0.012).t(0, 0.67, 0), 'gunmetal', 'Turret')
    m.add(pod(0.3, 0.17, 0.3).t(0, 0.82, -0.02), 'hull_white', 'Turret')
    m.add(rbox(0.32, 0.03, 0.06, 0.008).t(0, 0.9, 0.12), 'glow_cyan', 'Turret')
    for sx in (-0.07, 0.07):
        m.add(barrel(0.34, 0.025).t(sx, 0.82, 0.1), 'gunmetal', 'Turret')
    m.add(rbox(0.08, 0.05, 0.08, 0.01).t(0.13, 0.94, -0.1), 'hull_dark', 'Turret')
    m.add(antenna(0.18).t(-0.12, 0.9, -0.12), 'gunmetal', 'Turret')
    idle_scan(m)
    recoil(m, back=0.03, t=0.3)
    return m


@tower
def ember():
    m = T('ember', 'Plasmaspeier', 0.72, (0, 1.0, 0.3))
    base(m, glow='glow_orange')
    m.add(column(0.34, 0.3, 0.5, 0.12, 8), 'hull_orange', 'Base')
    m.add(kit.band(0.31, 0.5, 0.04, 0.015), 'gunmetal', 'Base')
    m.add(vents(0.33, 0.3, 4, math.pi / 4, 0.05, 0.12), 'glow_orange', 'Base')
    for a in (0.8, 2.4, 4.0):
        m.add(tank(0.07, 0.3).t(math.cos(a) * 0.3, 0.12, math.sin(a) * 0.3), 'glass', 'Base')
        m.add(cylinder(0.04, 0.2, 8).t(math.cos(a) * 0.3, 0.15, math.sin(a) * 0.3), 'glow_orange', 'Base')
    m.add(cylinder(0.24, 0.07, 16, bevel=0.012).t(0, 0.64, 0), 'gunmetal', 'Turret')
    mort = lathe([(0.0, 0.0), (0.13, 0.0), (0.15, 0.06), (0.11, 0.3), (0.13, 0.32), (0.08, 0.32), (0.08, 0.06)], 14, cap_top=False)
    m.add(mort.rx(-0.5).t(0, 0.72, 0.0), 'hull_dark', 'Turret')
    m.add(cylinder(0.08, 0.01, 12).t(0, 0.3, 0).rx(-0.5).t(0, 0.72, 0), 'glow_orange', 'Turret')
    m.add(merge([tube(curve([(sg * 0.1, 0.72, -0.15), (sg * 0.2, 0.85, -0.05), (sg * 0.1, 0.9, 0.1)], 8), 0.02, 6) for sg in (1, -1)]), 'gunmetal', 'Turret')
    idle_scan(m, deg=15)
    m.anim_rot('Attack', 'Turret', [(0, (1, 0, 0), 0), (0.1, (1, 0, 0), -12), (0.5, (1, 0, 0), 0)])
    return m


@tower
def frost():
    m = T('frost', 'Kryoemitter', 1.0, (0, 1.25, 0))
    base(m, mat='hull_white', glow='glow_cyan')
    m.add(column(0.26, 0.16, 0.85, 0.12, 6), 'hull_white', 'Base')
    m.add(coil(0.2, 0.3, 0.8, 4, 0.016), 'glass', 'Base')
    m.add(vents(0.22, 0.55, 3, 0.3, 0.04, 0.2), 'glow_cyan', 'Base')
    for i in range(3):
        a = TAU * i / 3
        m.add(tank(0.06, 0.32).t(math.cos(a) * 0.3, 0.12, math.sin(a) * 0.3), 'glass', 'Base')
    m.add(dish(0.24, 0.06).rx(math.pi).t(0, 1.06, 0), 'gunmetal', 'Base')
    m.node('Core', 'Turret', (0, 1.22, 0))
    m.add(crystal(0.08, 0.32, 6, 0.35, 0.35).t(0, 1.08, 0), 'glass', 'Core')
    m.add(torus(0.2, 0.012, 32, 5).t(0, 1.22, 0), 'glow_cyan', 'Core')
    m.add(torus(0.14, 0.01, 32, 5).rx(math.pi / 2).t(0, 1.22, 0), 'glow_cyan', 'Core')
    spin(m, 'Core', 3.0)
    bob(m, 'Turret', 0.02, 3.0)
    pulse(m, 'Turret', 0.85, 1.15, 0.6, 'Attack')
    return m


@tower
def storm():
    m = T('storm', 'Lichtbogenmast', 1.25, (0, 1.45, 0))
    base(m, glow='glow_storm')
    m.add(column(0.22, 0.12, 1.0, 0.12, 6), 'gunmetal', 'Base')
    m.add(coil(0.16, 0.3, 1.0, 6, 0.014), 'steel', 'Base')
    for i in range(3):
        a = TAU * i / 3 + 0.3
        m.add(kit.beam((math.cos(a) * 0.36, 0.1, math.sin(a) * 0.36), (math.cos(a) * 0.12, 0.8, math.sin(a) * 0.12), 0.035), 'hull_white', 'Base')
    m.add(sphere(0.09, seg=14).t(0, 1.22, 0), 'glow_storm', 'Turret')
    prongs = [kit.beam((0, 1.18, 0), (math.cos(TAU * k / 4) * 0.16, 1.4, math.sin(TAU * k / 4) * 0.16), 0.018) for k in range(4)]
    m.add(merge(prongs), 'steel', 'Turret')
    m.add(merge([sphere(0.025, seg=8).t(math.cos(TAU * k / 4) * 0.16, 1.4, math.sin(TAU * k / 4) * 0.16) for k in range(4)]), 'glow_storm', 'Turret')
    bob(m, 'Turret', 0.02, 2.0)
    pulse(m, 'Turret', 0.9, 1.15, 0.4, 'Attack')
    return m


@tower
def spore():
    m = T('spore', 'Toxinwerfer', 0.75, (0, 0.95, 0.3))
    base(m, glow='glow_poison')
    m.add(column(0.3, 0.28, 0.45, 0.12, 8), 'hull_dark', 'Base')
    for a in (0.0, 2.1, 4.2):
        m.add(tank(0.08, 0.36).t(math.cos(a) * 0.26, 0.15, math.sin(a) * 0.26), 'glass_green', 'Base')
        m.add(merge([torus(0.08, 0.01, 12, 4).t(math.cos(a) * 0.26, y, math.sin(a) * 0.26) for y in (0.22, 0.42)]), 'gunmetal', 'Base')
    m.add(cylinder(0.22, 0.07, 16, bevel=0.012).t(0, 0.6, 0), 'gunmetal', 'Turret')
    m.add(pod(0.26, 0.16, 0.26).t(0, 0.75, -0.02), 'hull_white', 'Turret')
    m.add(merge([barrel(0.25, 0.04, False).t(sx, 0.78, 0.08) for sx in (-0.07, 0.07)]), 'hull_dark', 'Turret')
    m.add(merge([cylinder(0.03, 0.01, 10).rx(math.pi / 2).t(sx, 0.78, 0.33) for sx in (-0.07, 0.07)]), 'glow_poison', 'Turret')
    m.add(tank(0.05, 0.14).t(0, 0.83, -0.12), 'glass_green', 'Turret')
    idle_scan(m)
    recoil(m, back=0.04, t=0.4)
    return m


@tower
def ballista():
    m = T('ballista', 'Railgun', 0.6, (0, 0.75, 0.7))
    base(m, mat='hull_hazard')
    m.add(column(0.32, 0.3, 0.34, 0.12, 6), 'hull_dark', 'Base')
    m.add(cylinder(0.26, 0.07, 16, bevel=0.012).t(0, 0.52, 0), 'gunmetal', 'Turret')
    m.add(pod(0.26, 0.18, 0.36).t(0, 0.68, -0.08), 'hull_white', 'Turret')
    for sx in (-0.055, 0.055):
        m.add(rbox(0.03, 0.06, 0.75, 0.008).t(sx, 0.74, 0.32), 'gunmetal', 'Turret')
    m.add(merge([rbox(0.14, 0.02, 0.04, 0.004).t(0, 0.74, 0.05 + k * 0.12) for k in range(6)]), 'glow_cyan', 'Turret')
    m.add(rbox(0.12, 0.12, 0.12, 0.02).t(0, 0.7, -0.28), 'hull_dark', 'Turret')
    m.add(merge([cylinder(0.025, 0.08, 8).rz(math.pi / 2).t(sg * 0.17, 0.68, -0.08) for sg in (1, -1)]), 'glow_cyan', 'Turret')
    idle_scan(m, deg=20)
    recoil(m, back=0.08, t=0.6)
    return m


@tower
def rune():
    m = T('rune', 'Psi-Emitter', 1.2, (0, 1.32, 0))
    base(m, glow='glow_magenta')
    m.add(column(0.24, 0.16, 0.8, 0.12, 6), 'circuit_violet', 'Base')
    for i in range(3):
        a = TAU * i / 3
        m.add(rbox(0.06, 0.6, 0.1, 0.015).ry(-a).t(math.cos(a) * 0.27, 0.42, math.sin(a) * 0.27), 'hull_white', 'Base')
    m.add(dish(0.2, 0.05).rx(math.pi).t(0, 0.98, 0), 'hull_white', 'Base')
    m.node('Prism', 'Turret', (0, 1.25, 0))
    m.add(crystal(0.1, 0.35, 4, 0.5, 0.5).t(0, 1.08, 0), 'glow_magenta', 'Prism')
    m.add(torus(0.2, 0.01, 32, 4).rx(0.4).t(0, 1.25, 0), 'hull_white', 'Prism')
    spin(m, 'Prism', 4.0)
    bob(m, 'Turret', 0.03, 3.0)
    pulse(m, 'Turret', 0.9, 1.12, 0.4, 'Attack')
    return m


@tower
def resonator():
    m = T('resonator', 'Schallkanone', 0.85, (0, 0.92, 0.25))
    base(m, mat='hull_white', glow='glow_frost')
    m.add(column(0.3, 0.26, 0.6, 0.12, 8), 'hull_white', 'Base')
    m.add(coil(0.29, 0.25, 0.6, 3, 0.012), 'gunmetal', 'Base')
    m.add(cylinder(0.22, 0.07, 16, bevel=0.012).t(0, 0.72, 0), 'gunmetal', 'Turret')
    for k, r in enumerate((0.22, 0.17, 0.12)):
        m.add(dish(r, 0.06).rx(-math.pi / 2).t(0, 0.92, -0.02 + k * 0.07), 'hull_dark' if k % 2 else 'hull_white', 'Turret')
    m.add(sphere(0.05, seg=10).t(0, 0.92, 0.14), 'glow_frost', 'Turret')
    idle_scan(m)
    pulse(m, 'Turret', 0.9, 1.1, 0.4, 'Attack')
    return m


@tower
def goldmine():
    m = T('goldmine', 'Erzförderer (2×2)', 0.9, (0, 1.0, 0))
    m.notes = 'Belegt 2×2 Felder (Grundfläche ≈ 1,9 m).'
    m.add(rbox(1.86, 0.06, 1.86, 0.02).t(0, 0.03, 0), 'hull_hazard', 'Base')
    m.add(rbox(1.0, 0.4, 0.7, 0.04).t(-0.3, 0.26, -0.45), 'hull_white', 'Base')
    m.add(rbox(1.04, 0.05, 0.74, 0.01).t(-0.3, 0.48, -0.45), 'hull_dark', 'Base')
    m.add(merge([rbox(0.18, 0.06, 0.02, 0.004).t(-0.6 + k * 0.3, 0.32, -0.09) for k in range(3)]), 'glow_orange', 'Base')
    # Bohrturm
    for x, z in ((0.35, -0.2), (0.75, -0.2), (0.35, -0.6), (0.75, -0.6)):
        m.add(kit.beam((x, 0.06, z), (0.55 + (x - 0.55) * 0.3, 1.1, -0.4 + (z + 0.4) * 0.3), 0.04), 'gunmetal', 'Base')
    m.add(rbox(0.2, 0.08, 0.2, 0.01).t(0.55, 1.12, -0.4), 'hull_orange', 'Base')
    m.node('Drill', 'Turret', (0.55, 0.6, -0.4))
    m.add(cylinder(0.05, 0.9, 10).t(0.55, 0.15, -0.4), 'steel', 'Drill')
    m.add(lathe([(0.12, 0.0), (0.0, -0.18)], 10, cap_bottom=False).t(0.55, 0.18, -0.4), 'gold', 'Drill')
    m.add(tube(curve([(0.62, 0.3, -0.4), (0.55, 0.45, -0.33), (0.48, 0.6, -0.4), (0.55, 0.75, -0.47), (0.62, 0.9, -0.4)], 12), 0.02, 5), 'hull_orange', 'Drill')
    # Erzcontainer und Förderband
    for (x, z) in ((-0.55, 0.45), (-0.15, 0.55)):
        m.add(rbox(0.34, 0.22, 0.28, 0.02).t(x, 0.17, z), 'hull_orange', 'Base')
        m.add(kit.crystal_cluster(5, 0.035, 0.11, 0.06, seed=int(x * 10 + 20)).t(x, 0.28, z), 'crystal_gold', 'Base')
    m.add(rbox(0.16, 0.04, 0.9, 0.01).rx(-0.3).t(0.55, 0.32, 0.25), 'hull_dark', 'Base')
    m.add(kit.crystal_cluster(6, 0.04, 0.14, 0.08, seed=5).t(0.6, 0.08, 0.68), 'crystal_gold', 'Base')
    m.add(antenna(0.3).t(-0.7, 0.5, -0.65), 'gunmetal', 'Base')
    m.nodes['Turret'].pos = (0, 0.0, 0)
    spin(m, 'Drill', 1.2)
    m.anim_pos('Attack', 'Turret', [(0, (0, 0, 0)), (0.12, (0, -0.12, 0)), (0.8, (0, 0, 0))])
    return m


@tower
def pylon():
    m = T('pylon', 'Relaisknoten', 1.3, (0, 1.5, 0))
    base(m, r=0.38, glow='glow_storm')
    for i in range(3):
        a = TAU * i / 3
        m.add(kit.beam((math.cos(a) * 0.3, 0.12, math.sin(a) * 0.3), (math.cos(a) * 0.05, 1.3, math.sin(a) * 0.05), 0.045), 'hull_white', 'Base')
    for y in (0.5, 0.9):
        r = 0.27 - (y - 0.12) / 1.2 * 0.22
        m.add(torus(r, 0.012, 24, 4).t(0, y, 0), 'glow_storm', 'Base')
    m.node('Dish', 'Turret', (0, 1.4, 0))
    m.add(dish(0.16, 0.05).rx(-0.4).t(0, 1.4, 0), 'hull_white', 'Dish')
    m.add(antenna(0.12).t(0, 1.4, 0.04), 'glow_storm', 'Dish')
    spin(m, 'Dish', 5.0)
    pulse(m, 'Turret', 0.9, 1.15, 0.4, 'Attack')
    return m


@tower
def cannon():
    m = T('cannon', 'Massetreiber', 0.62, (0, 0.8, 0.55))
    base(m, r=0.46, mat='hull_hazard')
    m.add(column(0.4, 0.37, 0.36, 0.12, 8), 'hull_dark', 'Base')
    m.add(vents(0.39, 0.3, 4, math.pi / 4, 0.05, 0.1), 'glow_orange', 'Base')
    m.add(cylinder(0.3, 0.08, 16, bevel=0.015).t(0, 0.48, 0), 'gunmetal', 'Turret')
    m.add(pod(0.36, 0.22, 0.4).t(0, 0.67, -0.08), 'hull_white', 'Turret')
    m.add(barrel(0.5, 0.075).rx(-0.15).t(0, 0.72, 0.05), 'gunmetal', 'Turret')
    m.add(merge([rbox(0.05, 0.16, 0.3, 0.01).t(sg * 0.2, 0.66, -0.08) for sg in (1, -1)]), 'hull_dark', 'Turret')
    idle_scan(m, deg=15)
    recoil(m, back=0.08, t=0.6)
    return m


@tower
def riftbinder():
    m = T('riftbinder', 'Phasenprojektor', 1.0, (0, 1.05, 0.25))
    base(m, glow='glow_magenta')
    m.add(column(0.26, 0.2, 0.5, 0.12, 6), 'hull_dark', 'Base')
    for sg in (1, -1):
        m.add(kit.beam((sg * 0.22, 0.6, 0), (sg * 0.28, 1.0, 0), 0.05), 'hull_white', 'Turret')
    m.node('Ring', 'Turret', (0, 1.05, 0))
    m.add(torus(0.26, 0.03, 32, 8).rx(math.pi / 2).t(0, 1.05, 0), 'hull_white', 'Ring')
    m.add(cylinder(0.23, 0.01, 24).rx(math.pi / 2).t(0, 1.05, 0.005), 'glow_magenta', 'Turret')
    spin(m, 'Ring', 4.0, axis=(0, 0, 1))
    idle_scan(m, deg=15)
    pulse(m, 'Turret', 0.9, 1.15, 0.5, 'Attack')
    return m


@tower
def meteor():
    m = T('meteor', 'Orbital-Uplink', 1.0, (0, 1.4, 0))
    base(m, r=0.45, mat='hull_white', glow='glow_orange')
    m.add(column(0.32, 0.24, 0.7, 0.12, 8), 'hull_white', 'Base')
    m.add(kit.band(0.27, 0.6, 0.04, 0.015), 'hull_orange', 'Base')
    m.add(vents(0.28, 0.45, 4, 0, 0.04, 0.14), 'glow_orange', 'Base')
    m.add(cylinder(0.12, 0.2, 10).t(0, 0.82, 0), 'gunmetal', 'Turret')
    m.node('Dish', 'Turret', (0, 1.05, 0))
    m.add(dish(0.36, 0.12).rx(0.5).t(0, 1.0, 0), 'hull_white', 'Dish')
    m.add(merge([kit.beam((0, 1.04, 0), (0, 1.38, 0.15), 0.02)]), 'gunmetal', 'Dish')
    m.add(sphere(0.04, seg=10).t(0, 1.38, 0.15), 'glow_orange', 'Dish')
    idle_scan(m, node='Dish', deg=30, T_=9)
    pulse(m, 'Turret', 0.95, 1.08, 0.8, 'Attack')
    return m


@tower
def tesla():
    m = T('tesla', 'Tesla-Generator', 1.1, (0, 1.3, 0))
    base(m, glow='glow_storm')
    m.add(column(0.3, 0.26, 0.3, 0.12, 8), 'hull_dark', 'Base')
    m.add(cylinder(0.12, 0.6, 12).t(0, 0.42, 0), 'glass', 'Base')
    m.add(coil(0.15, 0.48, 0.98, 7, 0.016), 'gold', 'Base')
    m.add(lathe([(0.0, 0.0), (0.22, 0.04), (0.24, 0.08), (0.12, 0.1), (0.0, 0.1)], 20).t(0, 1.02, 0), 'gunmetal', 'Base')
    m.add(sphere(0.11, seg=16).t(0, 1.22, 0), 'glow_storm', 'Turret')
    m.add(torus(0.16, 0.012, 24, 4).t(0, 1.22, 0), 'steel', 'Turret')
    pulse(m, 'Turret', 0.9, 1.15, 0.8)
    pulse(m, 'Turret', 0.8, 1.3, 0.3, 'Attack')
    return m


@tower
def gravity():
    m = T('gravity', 'Gravitonmaschine', 0.95, (0, 0.95, 0))
    base(m, glow='glow_magenta')
    for i in range(3):
        a = TAU * i / 3
        m.add(kit.beam((math.cos(a) * 0.34, 0.1, math.sin(a) * 0.34), (math.cos(a) * 0.3, 0.75, math.sin(a) * 0.3), 0.05), 'hull_white', 'Base')
    m.node('G1', 'Turret', (0, 0.9, 0))
    m.node('G2', 'Turret', (0, 0.9, 0))
    m.add(torus(0.28, 0.022, 32, 6).t(0, 0.9, 0), 'gunmetal', 'G1')
    m.add(torus(0.22, 0.02, 32, 6).rx(math.pi / 2).t(0, 0.9, 0), 'hull_white', 'G2')
    m.add(sphere(0.1, seg=16).t(0, 0.9, 0), 'void', 'Turret')
    m.add(torus(0.12, 0.006, 24, 4).t(0, 0.9, 0), 'glow_magenta', 'Turret')
    spin(m, 'G1', 3.0, axis=(1, 0, 0))
    spin(m, 'G2', 2.2, axis=(0, 1, 0))
    pulse(m, 'Turret', 0.85, 1.15, 0.5, 'Attack')
    return m


@tower
def dragon():
    m = T('dragon', 'Drohnenhangar', 0.85, (0, 1.0, 0.2))
    base(m, r=0.46, glow='glow_orange')
    m.add(lathe([(0.42, 0.0), (0.42, 0.3), (0.3, 0.52), (0.0, 0.58)], 12, a0=0).t(0, 0.12, 0), 'hull_white', 'Base')
    m.add(rbox(0.36, 0.26, 0.05, 0.02).t(0, 0.25, 0.38), 'hull_dark', 'Base')
    m.add(rbox(0.38, 0.03, 0.06, 0.006).t(0, 0.4, 0.4), 'glow_orange', 'Base')
    # Drohne über dem Hangar (Turret)
    m.add(pod(0.18, 0.08, 0.26).t(0, 0.9, 0), 'hull_dark', 'Turret')
    for sx, sz in ((0.17, 0.15), (-0.17, 0.15), (0.17, -0.15), (-0.17, -0.15)):
        m.add(torus(0.07, 0.008, 16, 4).t(sx, 0.92, sz), 'gunmetal', 'Turret')
        m.add(kit.beam((0, 0.9, 0), (sx, 0.92, sz), 0.015), 'gunmetal', 'Turret')
    m.add(sphere(0.03, seg=8).t(0, 0.9, 0.14), 'glow_orange', 'Turret')
    bob(m, 'Turret', 0.05, 2.0)
    m.anim_pos('Attack', 'Turret', [(0, (0, 0, 0)), (0.2, (0, 0.1, 0.08)), (0.7, (0, 0, 0))])
    return m


@tower
def laser():
    m = T('laser', 'Laserbatterie', 0.85, (0, 1.0, 0.45))
    base(m, glow='glow_magenta')
    m.add(column(0.28, 0.24, 0.55, 0.12, 6), 'hull_white', 'Base')
    m.add(vents(0.26, 0.4, 3, 0, 0.05, 0.12), 'glow_magenta', 'Base')
    m.add(cylinder(0.22, 0.06, 16, bevel=0.012).t(0, 0.68, 0), 'gunmetal', 'Turret')
    m.add(pod(0.26, 0.2, 0.3).t(0, 0.84, -0.04), 'hull_dark', 'Turret')
    m.add(lathe([(0.07, 0.0), (0.07, 0.3), (0.1, 0.32), (0.1, 0.36), (0.05, 0.36)], 14, cap_top=False).rx(math.pi / 2).t(0, 0.88, 0.08), 'hull_white', 'Turret')
    m.add(cylinder(0.05, 0.01, 12).rx(math.pi / 2).t(0, 0.88, 0.44), 'glow_magenta', 'Turret')
    m.add(merge([rbox(0.03, 0.14, 0.2, 0.006).t(sg * 0.15, 0.86, -0.04) for sg in (1, -1)]), 'glow_magenta', 'Turret')
    idle_scan(m)
    recoil(m, back=0.02, t=0.3)
    return m


@tower
def totem():
    m = T('totem', 'Feldverstärker', 1.2, (0, 1.4, 0))
    base(m, r=0.38, glow='glow_cyan')
    m.add(column(0.2, 0.12, 1.0, 0.12, 6), 'hull_white', 'Base')
    m.add(vents(0.17, 0.5, 3, 0, 0.035, 0.3), 'glow_cyan', 'Base')
    m.add(merge([torus(0.16 - k * 0.025, 0.01, 24, 4).t(0, 0.35 + k * 0.25, 0) for k in range(3)]), 'gunmetal', 'Base')
    m.node('Holo', 'Turret', (0, 1.32, 0))
    m.add(torus(0.18, 0.008, 32, 4).t(0, 1.25, 0), 'glow_cyan', 'Holo')
    m.add(torus(0.12, 0.008, 32, 4).t(0, 1.38, 0), 'glow_cyan', 'Holo')
    m.add(crystal(0.05, 0.18, 6).t(0, 1.22, 0), 'glass', 'Turret')
    spin(m, 'Holo', 5.0)
    bob(m, 'Turret', 0.03, 2.5)
    return m


@tower
def minelayer():
    m = T('minelayer', 'Minenleger', 0.6, (0, 0.75, 0.35))
    base(m, mat='hull_hazard', glow='glow_orange')
    m.add(rbox(0.6, 0.26, 0.52, 0.04).t(0, 0.25, 0), 'hull_dark', 'Base')
    for sg in (1, -1):
        m.add(rbox(0.12, 0.16, 0.56, 0.04).t(sg * 0.32, 0.16, 0), 'gunmetal', 'Base')
    m.add(cylinder(0.2, 0.06, 16, bevel=0.012).t(0, 0.38, 0), 'gunmetal', 'Turret')
    m.add(pod(0.3, 0.18, 0.28).t(0, 0.53, 0), 'hull_orange', 'Turret')
    for sx in (-0.08, 0.08):
        for sy in (0.0, 0.08):
            m.add(cylinder(0.035, 0.12, 10).rx(-math.pi / 2 + 0.4).t(sx, 0.56 + sy, 0.12), 'hull_dark', 'Turret')
    m.add(merge([lathe([(0.0, 0.0), (0.06, 0.0), (0.06, 0.02), (0.03, 0.04), (0.0, 0.04)], 10).t(-0.18 + k * 0.12, 0.38, -0.2) for k in range(4)]), 'hull_hazard', 'Base')
    idle_scan(m, deg=20)
    recoil(m, back=0.04, t=0.4)
    return m


@tower
def skyguard():
    m = T('skyguard', 'Flak-Phalanx', 0.7, (0, 1.0, 0.3))
    base(m, mat='hull_white')
    m.add(column(0.32, 0.28, 0.45, 0.12, 8), 'hull_white', 'Base')
    m.add(kit.band(0.3, 0.5, 0.03, 0.012), 'gunmetal', 'Base')
    m.add(cylinder(0.24, 0.07, 16, bevel=0.012).t(0, 0.6, 0), 'gunmetal', 'Turret')
    m.add(pod(0.32, 0.22, 0.26).t(0, 0.78, -0.05), 'hull_dark', 'Turret')
    for sx in (-0.1, 0.1):
        m.add(merge([barrel(0.36, 0.02, False).t(dx, dy, 0) for dx in (-0.02, 0.02) for dy in (-0.02, 0.02)]).rx(-0.6).t(sx, 0.82, 0.05), 'gunmetal', 'Turret')
    m.add(dish(0.08, 0.03).rx(-math.pi / 2).t(0, 0.95, -0.12), 'hull_white', 'Turret')
    idle_scan(m, deg=35, T_=5)
    recoil(m, back=0.03, t=0.2)
    return m


@tower
def barricade():
    m = T('barricade', 'Energiebarriere', 0.5, (0, 0.6, 0))
    m.add(rbox(0.86, 0.08, 0.3, 0.02).t(0, 0.04, 0), 'hull_hazard', 'Base')
    for x in (-0.36, 0.36):
        m.add(rbox(0.1, 0.55, 0.12, 0.02).t(x, 0.35, 0), 'hull_dark', 'Base')
        m.add(sphere(0.04, seg=8).t(x, 0.64, 0), 'glow_cyan', 'Base')
    m.add(rbox(0.62, 0.4, 0.02, 0.0).t(0, 0.33, 0), 'glass', 'Turret')
    m.add(merge([rbox(0.62, 0.012, 0.025, 0).t(0, 0.18 + k * 0.1, 0) for k in range(4)]), 'glow_cyan', 'Turret')
    m.nodes['Turret'].pos = (0, 0.33, 0)
    pulse(m, 'Turret', 0.97, 1.03, 1.2)
    return m


@tower
def lanebarricade():
    m = T('lanebarricade', 'Sperrfeld', 0.4, (0, 0.5, 0))
    for x in (-0.36, 0.36):
        m.add(cylinder(0.08, 0.06, 10, bevel=0.01).t(x, 0.0, 0), 'gunmetal', 'Base')
        m.add(cylinder(0.04, 0.42, 8).t(x, 0.06, 0), 'hull_hazard', 'Base')
        m.add(sphere(0.045, seg=10).t(x, 0.5, 0), 'glow_orange', 'Base')
    m.add(rbox(0.66, 0.36, 0.015, 0.0).t(0, 0.28, 0), 'glass', 'Turret')
    m.add(merge([rbox(0.66, 0.01, 0.02, 0).t(0, 0.14 + k * 0.09, 0) for k in range(4)]), 'glow_orange', 'Turret')
    m.nodes['Turret'].pos = (0, 0.28, 0)
    pulse(m, 'Turret', 0.96, 1.04, 0.8)
    return m


# 4.0-Grundtürme

@tower
def seal():
    m = T('seal', 'Störsender', 1.05, (0, 1.35, 0))
    base(m, glow='glow_magenta')
    m.add(column(0.22, 0.16, 0.9, 0.12, 4).ry(math.pi / 4), 'circuit_violet', 'Base')
    for i in range(4):
        a = TAU * i / 4
        m.add(antenna(0.5).t(math.cos(a) * 0.3, 0.12, math.sin(a) * 0.3), 'gunmetal', 'Base')
    m.node('Arr', 'Turret', (0, 1.1, 0))
    m.add(merge([rbox(0.04, 0.22, 0.02, 0.004).t(math.cos(TAU * k / 6) * 0.18, 1.1, math.sin(TAU * k / 6) * 0.18).ry(0) for k in range(6)]), 'hull_white', 'Arr')
    m.add(torus(0.18, 0.01, 32, 4).t(0, 1.22, 0), 'glow_magenta', 'Arr')
    m.add(sphere(0.06, seg=12).t(0, 1.1, 0), 'glow_magenta', 'Turret')
    spin(m, 'Arr', 3.0)
    pulse(m, 'Turret', 0.9, 1.15, 0.4, 'Attack')
    return m


@tower
def acid():
    m = T('acid', 'Säurereaktor', 0.55, (0, 0.85, 0.25))
    base(m, glow='glow_poison')
    m.add(tank(0.26, 0.55).t(0, 0.12, 0), 'glass_green', 'Base')
    m.add(merge([torus(0.255, 0.018, 24, 5).t(0, y, 0) for y in (0.25, 0.45)]), 'gunmetal', 'Base')
    for i in range(3):
        a = TAU * i / 3
        m.add(kit.beam((math.cos(a) * 0.36, 0.1, math.sin(a) * 0.36), (math.cos(a) * 0.24, 0.62, math.sin(a) * 0.24), 0.04), 'hull_white', 'Base')
    m.add(cylinder(0.2, 0.06, 16, bevel=0.012).t(0, 0.62, 0), 'gunmetal', 'Turret')
    m.add(barrel(0.3, 0.05, False).rx(-0.3).t(0, 0.74, 0.0), 'hull_dark', 'Turret')
    m.add(merge([sphere(0.03 + 0.01 * (k % 2), seg=8).t(math.cos(k * 2.1) * 0.12, 0.28 + k * 0.05, math.sin(k * 2.1) * 0.12) for k in range(5)]), 'glow_poison', 'Base')
    idle_scan(m, deg=15)
    recoil(m, back=0.04, t=0.4)
    return m


@tower
def quake():
    m = T('quake', 'Seismohammer', 0.9, (0, 0.3, 0))
    base(m, r=0.45, mat='hull_hazard', glow='glow_orange')
    m.add(cylinder(0.26, 0.06, 16, bevel=0.012).t(0, 0.12, 0), 'gunmetal', 'Base')
    for x in (-0.3, 0.3):
        m.add(rbox(0.1, 1.15, 0.16, 0.02).t(x, 0.68, 0), 'hull_dark', 'Base')
        m.add(rbox(0.03, 0.9, 0.04, 0.004).t(x * 0.82, 0.7, 0.08), 'glow_orange', 'Base')
    m.add(rbox(0.72, 0.12, 0.2, 0.02).t(0, 1.28, 0), 'hull_white', 'Base')
    m.add(cylinder(0.05, 0.3, 10).t(0, 1.0, 0), 'steel', 'Base')
    m.add(cylinder(0.04, 0.5, 8).t(0, 0.45, 0), 'steel', 'Turret')
    m.add(cylinder(0.2, 0.2, 16, bevel=0.02).t(0, 0.24, 0), 'gunmetal', 'Turret')
    m.add(cylinder(0.21, 0.02, 16).t(0, 0.3, 0), 'glow_orange', 'Turret')
    m.nodes['Turret'].pos = (0, 0.0, 0)
    m.anim_pos('Idle', 'Turret', [(0, (0, 0.2, 0)), (1.5, (0, 0.24, 0)), (3, (0, 0.2, 0))])
    m.anim_pos('Attack', 'Turret', [(0, (0, 0.32, 0)), (0.08, (0, -0.06, 0)), (0.18, (0, 0.0, 0)), (0.7, (0, 0.2, 0))])
    return m


@tower
def gale():
    m = T('gale', 'Druckwellenturbine', 1.0, (0, 1.05, 0.3))
    base(m, mat='hull_white', glow='glow_frost')
    m.add(column(0.25, 0.2, 0.75, 0.12, 6), 'hull_white', 'Base')
    m.add(vents(0.23, 0.45, 3, 0, 0.04, 0.2), 'glow_frost', 'Base')
    m.add(cylinder(0.18, 0.06, 16).t(0, 0.86, 0), 'gunmetal', 'Turret')
    m.add(lathe([(0.24, 0.0), (0.26, 0.04), (0.24, 0.3), (0.2, 0.32)], 20, cap_bottom=False, cap_top=False).rx(math.pi / 2).t(0, 1.05, -0.1), 'hull_dark', 'Turret')
    m.node('Fan', 'Turret', (0, 1.05, 0.02))
    blades = merge([rbox(0.05, 0.2, 0.012, 0.004).t(0, 0.11, 0).rz(TAU * k / 7).ry(0.3) for k in range(7)])
    m.add(blades.t(0, 1.05, 0.02), 'steel', 'Fan')
    m.add(sphere(0.05, seg=10).t(0, 1.05, 0.05), 'glow_frost', 'Fan')
    spin(m, 'Fan', 0.8, axis=(0, 0, 1))
    spin(m, 'Fan', 0.25, axis=(0, 0, 1), clip='Attack')
    idle_scan(m, deg=20, T_=8)
    return m


@tower
def eyrie():
    m = T('eyrie', 'Scharfschützenkuppel', 1.4, (0, 0.2, 0.55))
    base(m, r=0.42)
    m.add(column(0.18, 0.12, 1.2, 0.12, 6), 'hull_white', 'Base')
    for i in range(3):
        a = TAU * i / 3
        m.add(kit.beam((math.cos(a) * 0.36, 0.1, math.sin(a) * 0.36), (math.cos(a) * 0.12, 1.1, math.sin(a) * 0.12), 0.035), 'gunmetal', 'Base')
    m.add(cylinder(0.28, 0.06, 16, bevel=0.012).t(0, 1.32, 0), 'hull_dark', 'Base')
    m.add(lathe([(0.24, 0.0), (0.22, 0.12), (0.12, 0.22), (0.0, 0.24)], 16).t(0, 1.38, 0), 'glass', 'Turret')
    m.add(rbox(0.05, 0.05, 0.7, 0.01).t(0, 1.45, 0.25), 'gunmetal', 'Turret')
    m.add(cylinder(0.035, 0.12, 10).rx(math.pi / 2).t(0, 1.5, 0.05), 'hull_white', 'Turret')
    m.add(cylinder(0.02, 0.01, 8).rx(math.pi / 2).t(0, 1.45, 0.6), 'glow_cyan', 'Turret')
    m.nodes['Turret'].pos = (0, 1.38, 0)
    m.nodes['Muzzle'].pos = (0, 1.45, 0.62)
    idle_scan(m, deg=30, T_=9)
    recoil(m, back=0.04, t=0.6)
    return m


@tower
def barracks():
    m = T('barracks', 'Klonbunker', 0.85, (0, 0.9, 0.3))
    base(m, r=0.46, mat='hull_hazard', sides=4)
    m.add(lathe([(0.4, 0.0), (0.4, 0.3), (0.34, 0.42), (0.0, 0.46)], 4, a0=math.pi / 4).t(0, 0.12, 0), 'hull_dark', 'Base')
    m.add(rbox(0.24, 0.24, 0.04, 0.02).t(0, 0.26, 0.3), 'hull_white', 'Base')
    m.add(rbox(0.26, 0.02, 0.05, 0.005).t(0, 0.4, 0.31), 'glow_cyan', 'Base')
    m.add(merge([rbox(0.14, 0.03, 0.03, 0.005).t(sg * 0.25, 0.42, 0.2) for sg in (1, -1)]), 'glow_orange', 'Base')
    m.add(cylinder(0.14, 0.06, 12).t(0, 0.58, 0), 'gunmetal', 'Turret')
    m.add(pod(0.18, 0.1, 0.18).t(0, 0.68, 0), 'hull_white', 'Turret')
    m.add(barrel(0.22, 0.02, False).t(0, 0.68, 0.05), 'gunmetal', 'Turret')
    m.add(antenna(0.3).t(0.08, 0.72, -0.05), 'gunmetal', 'Turret')
    idle_scan(m, deg=25)
    recoil(m, back=0.02, t=0.25)
    return m


@tower
def bank():
    m = T('bank', 'Kreditbörse', 0.95, (0, 1.05, 0))
    base(m, r=0.44, mat='hull_white', glow='glow_orange', sides=4)
    m.add(rbox(0.56, 0.5, 0.5, 0.04).t(0, 0.37, 0), 'hull_white', 'Base')
    m.add(rbox(0.58, 0.04, 0.52, 0.01).t(0, 0.6, 0), 'glow_orange', 'Base')
    m.add(rbox(0.3, 0.2, 0.02, 0.01).t(0, 0.38, 0.26), 'glass', 'Base')
    m.add(merge([rbox(0.2, 0.018, 0.01, 0.002).t(0, 0.32 + k * 0.05, 0.272) for k in range(3)]), 'glow_orange', 'Base')
    m.node('Holo', 'Turret', (0, 0.95, 0))
    m.add(cylinder(0.12, 0.02, 20).t(0, 0.66, 0), 'gunmetal', 'Turret')
    coin = merge([cylinder(0.12, 0.025, 24).rx(math.pi / 2), torus(0.12, 0.012, 24, 4).rx(math.pi / 2)])
    m.add(coin.t(0, 0.95, 0), 'crystal_gold', 'Holo')
    spin(m, 'Holo', 3.0)
    bob(m, 'Turret', 0.03, 2.4)
    m.anim_scale('Attack', 'Holo', [(0, 1.0), (0.3, 1.4), (1.0, 1.0)])
    return m


@tower
def ward():
    m = T('ward', 'Schildgenerator', 0.75, (0, 0.8, 0))
    base(m, r=0.44, mat='hull_white', glow='glow_cyan')
    for i in range(4):
        a = TAU * i / 4 + math.pi / 4
        m.add(rbox(0.08, 0.5, 0.08, 0.015).t(math.cos(a) * 0.32, 0.37, math.sin(a) * 0.32), 'hull_dark', 'Base')
        m.add(sphere(0.035, seg=8).t(math.cos(a) * 0.32, 0.64, math.sin(a) * 0.32), 'glow_cyan', 'Base')
    m.add(lathe([(0.34, 0.0), (0.32, 0.12), (0.24, 0.26), (0.12, 0.33), (0.0, 0.35)], 24, cap_bottom=False, cap_top=False).t(0, 0.3, 0), 'glass', 'Base')
    m.add(cylinder(0.1, 0.18, 10).t(0, 0.12, 0), 'gunmetal', 'Base')
    m.node('Core', 'Turret', (0, 0.48, 0))
    m.add(icosphere(0.08, 1).t(0, 0.48, 0), 'glow_cyan', 'Core')
    m.add(torus(0.13, 0.008, 24, 4).rx(0.6).t(0, 0.48, 0), 'hull_white', 'Core')
    m.nodes['Turret'].pos = (0, 0.48, 0)
    spin(m, 'Core', 4.0)
    pulse(m, 'Core', 0.9, 1.3, 0.5, 'Attack')
    return m


# ================================================================ Fusionen

@tower
def plasma():
    m = T('plasma', 'Fusionsbrenner', 0.8, (0, 1.1, 0.45))
    lbase(m, glow='glow_orange')
    m.add(column(0.34, 0.3, 0.5, 0.16, 8), 'hull_dark', 'Base')
    m.add(coil(0.33, 0.3, 0.6, 3, 0.014), 'glow_storm', 'Base')
    m.add(cylinder(0.26, 0.08, 16, bevel=0.015).t(0, 0.66, 0), 'gunmetal', 'Turret')
    m.add(pod(0.34, 0.24, 0.36).t(0, 0.86, -0.06), 'hull_white', 'Turret')
    m.add(lathe([(0.08, 0.0), (0.1, 0.3), (0.14, 0.34), (0.06, 0.36)], 14, cap_top=False).rx(math.pi / 2).t(0, 0.9, 0.1), 'hull_orange', 'Turret')
    m.add(coil(0.1, 0, 0.2, 3, 0.012).rx(math.pi / 2).t(0, 0.9, 0.25), 'glow_storm', 'Turret')
    m.add(sphere(0.06, seg=10).t(0, 0.9, 0.46), 'glow_orange', 'Turret')
    idle_scan(m)
    recoil(m, back=0.05, t=0.45)
    return m


@tower
def glacier():
    m = T('glacier', 'Nullpunktkanone', 0.7, (0, 0.95, 0.6))
    lbase(m, glow='glow_cyan')
    m.add(column(0.36, 0.32, 0.4, 0.16, 8), 'hull_white', 'Base')
    for a in (0.6, 2.2, 3.8, 5.4):
        m.add(tank(0.07, 0.34).t(math.cos(a) * 0.36, 0.16, math.sin(a) * 0.36), 'glass', 'Base')
    m.add(cylinder(0.28, 0.08, 16, bevel=0.015).t(0, 0.58, 0), 'gunmetal', 'Turret')
    m.add(pod(0.34, 0.24, 0.4).t(0, 0.78, -0.1), 'hull_white', 'Turret')
    m.add(barrel(0.55, 0.07).t(0, 0.86, 0.05), 'hull_dark', 'Turret')
    m.add(coil(0.1, 0, 0.3, 4, 0.012).rx(math.pi / 2).t(0, 0.86, 0.15), 'glow_cyan', 'Turret')
    idle_scan(m, deg=15)
    recoil(m, back=0.08, t=0.6)
    return m


@tower
def voidlance():
    m = T('voidlance', 'Antimaterielanze', 0.9, (0, 1.1, 0.75))
    lbase(m)
    m.add(column(0.3, 0.24, 0.6, 0.16, 6), 'hull_dark', 'Base')
    m.add(vents(0.27, 0.45, 3, 0, 0.05, 0.2), 'glow_magenta', 'Base')
    m.add(cylinder(0.24, 0.07, 16, bevel=0.015).t(0, 0.76, 0), 'gunmetal', 'Turret')
    m.add(pod(0.24, 0.2, 0.4).t(0, 0.94, -0.1), 'hull_white', 'Turret')
    for sx in (-0.07, 0.07):
        m.add(rbox(0.035, 0.08, 0.85, 0.01).t(sx, 1.0, 0.35), 'hull_dark', 'Turret')
    m.add(merge([torus(0.05, 0.01, 12, 4).rx(math.pi / 2).t(0, 1.0, 0.15 + k * 0.15) for k in range(5)]), 'glow_magenta', 'Turret')
    m.add(sphere(0.05, seg=10).t(0, 1.0, -0.02), 'void', 'Turret')
    idle_scan(m)
    recoil(m, back=0.08, t=0.6)
    return m


@tower
def starfall():
    m = T('starfall', 'Orbitalbombardement', 1.2, (0, 1.9, 0))
    lbase(m, glow='glow_orange')
    m.add(column(0.32, 0.22, 1.1, 0.16, 8), 'hull_white', 'Base')
    m.add(kit.band(0.27, 0.7, 0.04, 0.015), 'hull_orange', 'Base')
    m.add(vents(0.27, 0.9, 4, 0, 0.04, 0.2), 'glow_orange', 'Base')
    m.add(dish(0.4, 0.14).t(0, 1.26, 0), 'hull_white', 'Turret')
    m.add(merge([kit.beam((math.cos(TAU * k / 3) * 0.3, 1.36, math.sin(TAU * k / 3) * 0.3), (0, 1.75, 0), 0.02) for k in range(3)]), 'gunmetal', 'Turret')
    m.add(sphere(0.06, seg=12).t(0, 1.78, 0), 'glow_orange', 'Turret')
    spin(m, 'Turret', 12.0)
    pulse(m, 'Turret', 0.95, 1.08, 0.8, 'Attack')
    return m


@tower
def thornwood():
    m = T('thornwood', 'Flechette-Batterie', 0.8, (0, 1.0, 0.45))
    lbase(m, glow='glow_poison')
    m.add(column(0.36, 0.32, 0.5, 0.16, 8), 'hull_hazard', 'Base')
    m.add(cylinder(0.28, 0.08, 16, bevel=0.015).t(0, 0.66, 0), 'gunmetal', 'Turret')
    m.add(pod(0.4, 0.26, 0.34).t(0, 0.87, -0.05), 'hull_dark', 'Turret')
    for sx in (-0.12, 0.0, 0.12):
        for sy in (-0.06, 0.06):
            m.add(barrel(0.3, 0.022, False).t(sx, 0.87 + sy, 0.1), 'gunmetal', 'Turret')
    m.add(rbox(0.42, 0.02, 0.04, 0.004).t(0, 1.01, 0.12), 'glow_poison', 'Turret')
    idle_scan(m)
    recoil(m, back=0.03, t=0.25)
    return m


@tower
def thunderspire():
    m = T('thunderspire', 'Hochspannungsturm', 1.6, (0, 2.0, 0))
    lbase(m, glow='glow_storm')
    for i in range(4):
        a = TAU * i / 4 + math.pi / 4
        m.add(kit.beam((math.cos(a) * 0.36, 0.16, math.sin(a) * 0.36), (math.cos(a) * 0.06, 1.7, math.sin(a) * 0.06), 0.045), 'gunmetal', 'Base')
    for y in (0.5, 0.9, 1.3):
        r = 0.31 - (y - 0.16) / 1.54 * 0.25
        m.add(torus(r, 0.014, 4, 4).ry(math.pi / 4).t(0, y, 0), 'hull_white', 'Base')
        m.add(merge([lathe([(0.0, 0.0), (0.04, 0.01), (0.04, 0.06), (0.0, 0.07)], 8).t(math.cos(TAU * k / 4) * r, y - 0.07, math.sin(TAU * k / 4) * r) for k in range(4)]), 'glass', 'Base')
    m.add(sphere(0.12, seg=16).t(0, 1.85, 0), 'glow_storm', 'Turret')
    m.add(torus(0.18, 0.015, 24, 5).t(0, 1.85, 0), 'steel', 'Turret')
    pulse(m, 'Turret', 0.85, 1.2, 0.6)
    pulse(m, 'Turret', 0.8, 1.35, 0.3, 'Attack')
    return m


@tower
def riftgate():
    m = T('riftgate', 'Wurmlochtor', 1.0, (0, 1.05, 0.2))
    lbase(m)
    for sg in (1, -1):
        m.add(rbox(0.12, 1.1, 0.16, 0.03).t(sg * 0.36, 0.7, 0), 'hull_white', 'Base')
        m.add(rbox(0.03, 0.9, 0.04, 0.005).t(sg * 0.3, 0.7, 0.09), 'glow_magenta', 'Base')
    m.add(rbox(0.86, 0.12, 0.18, 0.03).t(0, 1.28, 0), 'hull_dark', 'Base')
    m.node('Ring', 'Turret', (0, 0.75, 0))
    m.add(torus(0.28, 0.035, 36, 8).rx(math.pi / 2).t(0, 0.75, 0), 'gunmetal', 'Ring')
    m.add(merge([rbox(0.05, 0.05, 0.05, 0.01).t(math.cos(TAU * k / 8) * 0.28, 0.75 + math.sin(TAU * k / 8) * 0.28, 0) for k in range(8)]), 'glow_magenta', 'Ring')
    m.add(cylinder(0.25, 0.01, 32).rx(math.pi / 2).t(0, 0.75, 0), 'void', 'Turret')
    m.nodes['Turret'].pos = (0, 0.0, 0)
    spin(m, 'Ring', 3.0, axis=(0, 0, 1))
    pulse(m, 'Turret', 0.95, 1.05, 0.5, 'Attack')
    return m


@tower
def phoenix():
    m = T('phoenix', 'Kampfträger', 0.95, (0, 1.2, 0.3))
    lbase(m, glow='glow_orange')
    m.add(lathe([(0.44, 0.0), (0.44, 0.3), (0.34, 0.5), (0.0, 0.56)], 12).t(0, 0.16, 0), 'hull_dark', 'Base')
    m.add(rbox(0.4, 0.26, 0.05, 0.02).t(0, 0.3, 0.4), 'hull_white', 'Base')
    m.add(rbox(0.42, 0.03, 0.06, 0.006).t(0, 0.46, 0.42), 'glow_orange', 'Base')
    m.add(pod(0.2, 0.1, 0.4).t(0, 1.05, 0), 'hull_white', 'Turret')
    for sg in (1, -1):
        m.add(rbox(0.35, 0.02, 0.18, 0.01).rz(sg * 0.15).t(sg * 0.24, 1.05, -0.04), 'hull_orange', 'Turret')
        m.add(cylinder(0.04, 0.12, 10).rx(math.pi / 2).t(sg * 0.1, 1.04, -0.24), 'gunmetal', 'Turret')
        m.add(cylinder(0.03, 0.01, 10).rx(math.pi / 2).t(sg * 0.1, 1.04, -0.25), 'glow_orange', 'Turret')
    bob(m, 'Turret', 0.05, 2.5)
    m.anim_pos('Attack', 'Turret', [(0, (0, 0, 0)), (0.2, (0, 0.12, 0.1)), (0.7, (0, 0, 0))])
    return m


@tower
def cathedral():
    m = T('cathedral', 'Infraschall-Dom', 1.2, (0, 1.4, 0))
    lbase(m, glow='glow_frost')
    m.add(column(0.36, 0.3, 0.6, 0.16, 8), 'hull_white', 'Base')
    m.add(lathe([(0.34, 0.0), (0.32, 0.15), (0.22, 0.32), (0.0, 0.38)], 20).t(0, 0.76, 0), 'glass', 'Base')
    for i in range(4):
        a = TAU * i / 4
        m.add(dish(0.12, 0.04).rx(-math.pi / 2).ry(-a + math.pi / 2).t(math.cos(a) * 0.36, 0.55, math.sin(a) * 0.36), 'hull_dark', 'Base')
    m.node('Ring', 'Turret', (0, 1.25, 0))
    m.add(torus(0.3, 0.012, 36, 5).t(0, 1.25, 0), 'glow_frost', 'Ring')
    m.add(sphere(0.08, seg=12).t(0, 1.1, 0), 'glow_frost', 'Turret')
    spin(m, 'Ring', 5.0)
    pulse(m, 'Turret', 0.9, 1.2, 0.5, 'Attack')
    return m


@tower
def stormeye():
    m = T('stormeye', 'Ionensturm-Auge', 1.3, (0, 1.6, 0))
    lbase(m, glow='glow_storm')
    m.add(column(0.3, 0.2, 1.1, 0.16, 8), 'gunmetal', 'Base')
    m.add(coil(0.27, 0.4, 1.1, 5, 0.012), 'glow_cyan', 'Base')
    m.node('Eye', 'Turret', (0, 1.5, 0))
    m.add(sphere(0.16, seg=18).t(0, 1.5, 0), 'glass', 'Eye')
    m.add(sphere(0.08, seg=12).t(0, 1.5, 0.1), 'glow_storm', 'Eye')
    m.add(torus(0.2, 0.014, 32, 5).rx(math.pi / 2).t(0, 1.5, 0), 'hull_white', 'Eye')
    spin(m, 'Eye', 6.0)
    pulse(m, 'Turret', 0.9, 1.15, 0.4, 'Attack')
    return m


@tower
def skywatch():
    m = T('skywatch', 'Abfangstation', 1.0, (0, 1.4, 0.3))
    lbase(m)
    m.add(column(0.36, 0.3, 0.7, 0.16, 8), 'hull_white', 'Base')
    m.add(kit.band(0.31, 0.7, 0.04, 0.015), 'glow_cyan', 'Base')
    m.add(cylinder(0.28, 0.08, 16, bevel=0.015).t(0, 0.86, 0), 'gunmetal', 'Turret')
    m.add(pod(0.36, 0.24, 0.3).t(0, 1.06, -0.05), 'hull_dark', 'Turret')
    for sx in (-0.13, 0.13):
        for k in range(3):
            m.add(cylinder(0.035, 0.22, 10).rx(-0.8).t(sx, 1.08 + k * 0.06, 0.05), 'hull_white', 'Turret')
    m.add(dish(0.14, 0.05).rx(-0.4).t(0, 1.25, -0.12), 'hull_white', 'Turret')
    idle_scan(m, deg=35, T_=5)
    recoil(m, back=0.03, t=0.2)
    return m


@tower
def sporemine():
    m = T('sporemine', 'Toxinminenfeld', 0.75, (0, 0.9, 0))
    lbase(m, glow='glow_poison')
    m.add(lathe([(0.38, 0.0), (0.36, 0.2), (0.24, 0.36), (0.0, 0.4)], 16).t(0, 0.16, 0), 'hull_dark', 'Base')
    for i in range(5):
        a = TAU * i / 5
        m.add(tank(0.05, 0.16).t(math.cos(a) * 0.34, 0.16, math.sin(a) * 0.34), 'glass_green', 'Base')
    m.add(cylinder(0.14, 0.08, 14).t(0, 0.56, 0), 'gunmetal', 'Turret')
    m.add(merge([cylinder(0.04, 0.2, 10).rx(-0.7).ry(TAU * k / 3).t(math.sin(TAU * k / 3) * 0.06, 0.62, math.cos(TAU * k / 3) * 0.06) for k in range(3)]), 'hull_white', 'Turret')
    m.add(sphere(0.05, seg=10).t(0, 0.68, 0), 'glow_poison', 'Turret')
    idle_scan(m, deg=40, T_=6)
    recoil(m, back=0.02, t=0.3)
    return m


@tower
def silencehall():
    m = T('silencehall', 'Funkstille-Zitadelle', 1.1, (0, 1.3, 0))
    lbase(m, glow='glow_frost')
    m.add(column(0.36, 0.3, 0.7, 0.16, 8), 'circuit', 'Base')
    for i in range(4):
        a = TAU * i / 4 + math.pi / 4
        m.add(antenna(0.7).t(math.cos(a) * 0.3, 0.86, math.sin(a) * 0.3), 'gunmetal', 'Base')
    m.add(dish(0.3, 0.1).rx(math.pi).t(0, 1.0, 0), 'hull_white', 'Base')
    m.node('Ring', 'Turret', (0, 1.15, 0))
    m.add(torus(0.32, 0.012, 36, 5).t(0, 1.15, 0), 'glow_frost', 'Ring')
    m.add(sphere(0.07, seg=12).t(0, 1.15, 0), 'glow_frost', 'Turret')
    spin(m, 'Ring', 6.0)
    pulse(m, 'Turret', 0.9, 1.2, 0.5, 'Attack')
    return m


@tower
def banecircle():
    m = T('banecircle', 'Nullfeld', 0.9, (0, 1.0, 0))
    lbase(m)
    for i in range(6):
        a = TAU * i / 6
        m.add(rbox(0.08, 0.4, 0.06, 0.01).ry(-a).t(math.cos(a) * 0.36, 0.36, math.sin(a) * 0.36), 'hull_dark', 'Base')
        m.add(sphere(0.03, seg=8).t(math.cos(a) * 0.36, 0.58, math.sin(a) * 0.36), 'glow_magenta', 'Base')
    m.add(cylinder(0.32, 0.02, 24).t(0, 0.16, 0), 'circuit_violet', 'Base')
    m.add(column(0.1, 0.07, 0.6, 0.16, 6), 'hull_white', 'Base')
    m.node('Ring', 'Turret', (0, 1.0, 0))
    m.add(torus(0.24, 0.01, 32, 4).t(0, 1.0, 0), 'glow_magenta', 'Ring')
    m.add(icosphere(0.07, 1).t(0, 1.0, 0), 'void', 'Turret')
    m.nodes['Turret'].pos = (0, 1.0, 0)
    spin(m, 'Ring', 4.0, turns=-1)
    bob(m, 'Turret', 0.04, 3.0)
    pulse(m, 'Ring', 0.8, 1.4, 0.5, 'Attack')
    return m


@tower
def acidmortar():
    m = T('acidmortar', 'Korrosionsartillerie', 0.55, (0, 1.0, 0.25))
    lbase(m, glow='glow_poison')
    m.add(column(0.4, 0.38, 0.3, 0.16, 8), 'hull_hazard', 'Base')
    m.add(cylinder(0.3, 0.08, 16, bevel=0.015).t(0, 0.46, 0), 'gunmetal', 'Turret')
    mort = lathe([(0.0, -0.05), (0.16, -0.05), (0.2, 0.05), (0.15, 0.2), (0.13, 0.42), (0.16, 0.45), (0.16, 0.5), (0.1, 0.5), (0.1, 0.05)], 14, cap_top=False)
    m.add(mort.rx(-0.6).t(0, 0.62, 0.0), 'hull_dark', 'Turret')
    m.add(cylinder(0.1, 0.01, 12).t(0, 0.47, 0).rx(-0.6).t(0, 0.62, 0), 'glow_poison', 'Turret')
    m.add(tank(0.12, 0.32).t(0, 0.58, -0.25), 'glass_green', 'Turret')
    m.add(merge([torus(0.12, 0.012, 16, 4).t(0, y, -0.25) for y in (0.66, 0.84)]), 'gunmetal', 'Turret')
    idle_scan(m, deg=18, T_=7)
    recoil(m, back=0.08, t=0.7)
    return m


@tower
def plaguepot():
    m = T('plaguepot', 'Nanoseuche-Reaktor', 0.45, (0, 0.9, 0.2))
    lbase(m, glow='glow_poison')
    m.add(tank(0.34, 0.6).t(0, 0.16, 0), 'glass_green', 'Base')
    m.add(merge([torus(0.335, 0.02, 24, 5).t(0, y, 0) for y in (0.3, 0.55)]), 'gunmetal', 'Base')
    m.add(merge([sphere(0.03 + 0.012 * (k % 3), seg=8).t(math.cos(k * 2.4) * 0.18, 0.3 + (k % 4) * 0.07, math.sin(k * 2.4) * 0.18) for k in range(8)]), 'glow_poison', 'Base')
    m.add(cylinder(0.16, 0.06, 14).t(0, 0.74, 0), 'gunmetal', 'Turret')
    m.add(barrel(0.26, 0.05, False).rx(-0.4).t(0, 0.82, 0.0), 'hull_white', 'Turret')
    m.add(merge([antenna(0.2).t(math.cos(TAU * k / 3) * 0.12, 0.78, math.sin(TAU * k / 3) * 0.12) for k in range(3)]), 'gunmetal', 'Turret')
    idle_scan(m, deg=20)
    recoil(m, back=0.04, t=0.4)
    return m


@tower
def quakemaw():
    m = T('quakemaw', 'Gravitationshammer', 1.0, (0, 0.4, 0))
    lbase(m)
    m.add(torus(0.3, 0.06, 24, 8).t(0, 0.22, 0), 'hull_dark', 'Base')
    m.add(cylinder(0.24, 0.02, 20).t(0, 0.18, 0), 'void', 'Base')
    m.add(torus(0.2, 0.01, 32, 4).t(0, 0.22, 0), 'glow_magenta', 'Base')
    head = merge([cylinder(0.22, 0.24, 16, bevel=0.03).t(0, 0.98, 0), cylinder(0.24, 0.03, 16).t(0, 0.98, 0), cylinder(0.24, 0.03, 16).t(0, 1.19, 0)])
    m.add(head, 'gunmetal', 'Turret')
    m.add(cylinder(0.23, 0.02, 16).t(0, 1.09, 0), 'glow_magenta', 'Turret')
    m.node('Orbit', 'Turret', (0, 1.1, 0))
    m.add(merge([icosphere(0.04, 1).t(math.cos(TAU * k / 4) * 0.34, 1.1, math.sin(TAU * k / 4) * 0.34) for k in range(4)]), 'hull_white', 'Orbit')
    m.nodes['Turret'].pos = (0, 0.0, 0)
    spin(m, 'Orbit', 3.0)
    m.anim_pos('Idle', 'Turret', [(0, (0, 0, 0)), (1.5, (0, 0.06, 0)), (3, (0, 0, 0))])
    m.anim_pos('Attack', 'Turret', [(0, (0, 0.12, 0)), (0.1, (0, -0.55, 0)), (0.25, (0, -0.5, 0)), (0.9, (0, 0, 0))])
    return m


@tower
def cyclone():
    m = T('cyclone', 'Ionensturm-Turbine', 1.2, (0, 1.3, 0.3))
    lbase(m, glow='glow_storm')
    m.add(column(0.28, 0.22, 0.95, 0.16, 6), 'hull_white', 'Base')
    m.add(coil(0.25, 0.4, 0.95, 4, 0.012), 'glow_storm', 'Base')
    m.add(cylinder(0.2, 0.06, 16).t(0, 1.1, 0), 'gunmetal', 'Turret')
    m.add(lathe([(0.28, 0.0), (0.3, 0.04), (0.28, 0.34), (0.24, 0.36)], 20, cap_bottom=False, cap_top=False).rx(math.pi / 2).t(0, 1.3, -0.12), 'hull_dark', 'Turret')
    m.node('Fan', 'Turret', (0, 1.3, 0.02))
    m.add(merge([rbox(0.06, 0.24, 0.012, 0.004).t(0, 0.13, 0).rz(TAU * k / 8).ry(0.3) for k in range(8)]).t(0, 1.3, 0.02), 'steel', 'Fan')
    m.add(sphere(0.06, seg=10).t(0, 1.3, 0.06), 'glow_storm', 'Fan')
    spin(m, 'Fan', 0.6, axis=(0, 0, 1))
    spin(m, 'Fan', 0.2, axis=(0, 0, 1), clip='Attack')
    idle_scan(m, deg=15, T_=9)
    return m


@tower
def stormbattery():
    m = T('stormbattery', 'Abfangturbine', 1.0, (0, 1.2, 0.4))
    lbase(m)
    m.add(column(0.34, 0.3, 0.75, 0.16, 8), 'hull_white', 'Base')
    m.add(cylinder(0.27, 0.08, 16, bevel=0.015).t(0, 0.92, 0), 'gunmetal', 'Turret')
    for sg in (1, -1):
        m.add(barrel(0.4, 0.045).rx(-0.5).t(sg * 0.12, 1.08, -0.05), 'gunmetal', 'Turret')
        m.add(rbox(0.07, 0.18, 0.24, 0.015).t(sg * 0.12, 1.06, -0.05), 'hull_dark', 'Turret')
    m.node('Fan', 'Turret', (0, 1.12, -0.24))
    m.add(merge([rbox(0.04, 0.14, 0.01, 0.003).t(0, 0.08, 0).rz(TAU * k / 6) for k in range(6)]).t(0, 1.12, -0.24), 'steel', 'Fan')
    spin(m, 'Fan', 0.8, axis=(0, 0, 1))
    idle_scan(m, deg=30, T_=6)
    recoil(m, back=0.03, t=0.25)
    return m


@tower
def falcon():
    m = T('falcon', 'Gauss-Scharfschütze', 1.5, (0, 0.2, 0.7))
    lbase(m, glow='glow_cyan')
    m.add(column(0.26, 0.16, 1.3, 0.16, 6), 'hull_white', 'Base')
    m.add(vents(0.22, 0.9, 3, 0.5, 0.035, 0.3), 'glow_cyan', 'Base')
    m.add(cylinder(0.28, 0.06, 16, bevel=0.012).t(0, 1.46, 0), 'hull_dark', 'Base')
    m.add(pod(0.22, 0.16, 0.36).t(0, 1.6, -0.1), 'hull_white', 'Turret')
    for sx in (-0.04, 0.04):
        m.add(rbox(0.025, 0.05, 0.9, 0.006).t(sx, 1.62, 0.32), 'gunmetal', 'Turret')
    m.add(merge([torus(0.05, 0.008, 12, 4).rx(math.pi / 2).t(0, 1.62, 0.1 + k * 0.14) for k in range(5)]), 'glow_cyan', 'Turret')
    m.nodes['Turret'].pos = (0, 1.5, 0)
    m.nodes['Muzzle'].pos = (0, 1.62, 0.78)
    idle_scan(m, deg=25, T_=9)
    recoil(m, back=0.06, t=0.6)
    return m


@tower
def sunlance():
    m = T('sunlance', 'Orbitallaser', 1.5, (0, 1.9, 0))
    lbase(m, glow='glow_orange')
    m.add(column(0.3, 0.2, 1.25, 0.16, 8), 'hull_white', 'Base')
    m.add(kit.band(0.26, 0.6, 0.04, 0.015), 'hull_orange', 'Base')
    m.add(vents(0.25, 0.95, 4, 0, 0.04, 0.2), 'glow_orange', 'Base')
    m.node('Petals', 'Turret', (0, 1.55, 0))
    m.add(merge([rbox(0.1, 0.3, 0.015, 0.006).t(0, 0.15, 0).rx(-0.5).ry(-TAU * k / 6).t(math.cos(TAU * k / 6) * 0.14, 1.45, math.sin(TAU * k / 6) * 0.14) for k in range(6)]), 'glass', 'Petals')
    m.add(sphere(0.1, seg=14).t(0, 1.75, 0), 'glow_orange', 'Turret')
    m.add(torus(0.13, 0.015, 24, 5).t(0, 1.75, 0), 'gunmetal', 'Turret')
    m.nodes['Turret'].pos = (0, 1.5, 0)
    spin(m, 'Petals', 10.0)
    pulse(m, 'Turret', 0.95, 1.08, 0.6, 'Attack')
    return m


@tower
def order():
    m = T('order', 'Mech-Garnison', 1.0, (0, 1.1, 0.3))
    lbase(m, mat='hull_hazard')
    m.add(lathe([(0.42, 0.0), (0.42, 0.36), (0.36, 0.48), (0.0, 0.52)], 8, a0=math.pi / 8).t(0, 0.16, 0), 'hull_dark', 'Base')
    m.add(rbox(0.3, 0.26, 0.04, 0.02).t(0, 0.32, 0.42), 'hull_white', 'Base')
    m.add(rbox(0.32, 0.02, 0.05, 0.005).t(0, 0.47, 0.43), 'glow_cyan', 'Base')
    for sg in (1, -1):
        m.add(cylinder(0.08, 0.5, 10).t(sg * 0.34, 0.16, 0.3), 'hull_white', 'Base')
        m.add(sphere(0.04, seg=8).t(sg * 0.34, 0.68, 0.3), 'glow_orange', 'Base')
    m.add(cylinder(0.16, 0.08, 14).t(0, 0.68, 0), 'gunmetal', 'Turret')
    m.add(pod(0.26, 0.16, 0.26).t(0, 0.84, 0), 'hull_white', 'Turret')
    m.add(merge([barrel(0.26, 0.03).t(sg * 0.08, 0.84, 0.1) for sg in (1, -1)]), 'gunmetal', 'Turret')
    m.add(antenna(0.35).t(0.1, 0.92, -0.1), 'gunmetal', 'Turret')
    idle_scan(m, deg=25)
    recoil(m, back=0.03, t=0.3)
    return m


@tower
def treasury():
    m = T('treasury', 'Zentralbank (2×2)', 0.95, (0, 1.2, 0))
    m.notes = 'Belegt 2×2 Felder (Grundfläche ≈ 1,9 m).'
    m.add(rbox(1.86, 0.08, 1.86, 0.02).t(0, 0.04, 0), 'hull_dark', 'Base')
    m.add(kit.band(0.9, 0.08, 0.02, 0.008, 4).ry(math.pi / 4), 'glow_orange', 'Base')
    m.add(rbox(1.2, 0.6, 1.1, 0.05).t(0, 0.38, -0.1), 'hull_white', 'Base')
    m.add(rbox(1.24, 0.05, 1.14, 0.01).t(0, 0.7, -0.1), 'glow_orange', 'Base')
    m.add(lathe([(0.46, 0.0), (0.44, 0.12), (0.34, 0.3), (0.18, 0.4), (0.0, 0.43)], 24).t(0, 0.72, -0.1), 'glass', 'Base')
    m.add(cylinder(0.24, 0.38, 24, bevel=0.02).rx(math.pi / 2).t(0, 0.34, 0.42), 'gunmetal', 'Base')
    m.add(merge([rbox(0.05, 0.02, 0.02, 0.003).rz(TAU * k / 6).t(0, 0.34, 0.62) for k in range(6)]), 'glow_orange', 'Base')
    for (x, z) in ((0.65, 0.65), (-0.65, 0.6)):
        m.add(rbox(0.3, 0.2, 0.3, 0.02).t(x, 0.18, z), 'hull_orange', 'Base')
        m.add(kit.crystal_cluster(5, 0.035, 0.12, 0.06, seed=int(abs(x) * 10 + z * 10)).t(x, 0.28, z), 'crystal_gold', 'Base')
    m.node('Holo', 'Turret', (0, 1.25, -0.1))
    coin = merge([cylinder(0.14, 0.03, 24).rx(math.pi / 2), torus(0.14, 0.014, 24, 4).rx(math.pi / 2)])
    m.add(coin.t(0, 1.25, -0.1), 'crystal_gold', 'Holo')
    m.nodes['Turret'].pos = (0, 0.0, 0)
    spin(m, 'Holo', 3.0)
    m.anim_scale('Attack', 'Holo', [(0, 1.0), (0.3, 1.4), (1.0, 1.0)])
    return m


@tower
def deepdrill():
    m = T('deepdrill', 'Kernbohrer (2×2)', 0.95, (0, 0.3, 0))
    m.notes = 'Belegt 2×2 Felder (Grundfläche ≈ 1,9 m).'
    m.add(rbox(1.86, 0.08, 1.86, 0.02).t(0, 0.04, 0), 'hull_hazard', 'Base')
    for i in range(4):
        a = TAU * i / 4 + math.pi / 4
        m.add(kit.beam((math.cos(a) * 0.65, 0.08, math.sin(a) * 0.65), (math.cos(a) * 0.14, 1.6, math.sin(a) * 0.14), 0.07), 'hull_white', 'Base')
    for y in (0.6, 1.1):
        r = 0.65 - (y - 0.08) / 1.52 * 0.51
        m.add(torus(r, 0.02, 4, 4).ry(math.pi / 4).t(0, y, 0), 'glow_orange', 'Base')
    m.add(rbox(0.4, 0.12, 0.4, 0.02).t(0, 1.64, 0), 'hull_dark', 'Base')
    m.add(rbox(0.36, 0.22, 0.3, 0.02).t(0.6, 0.19, 0.6), 'hull_orange', 'Base')
    m.add(kit.crystal_cluster(6, 0.04, 0.14, 0.07, seed=3).t(0.6, 0.3, 0.6), 'crystal_gold', 'Base')
    m.node('Drill', 'Turret', (0, 0.6, 0))
    m.add(cylinder(0.09, 1.0, 12).t(0, 0.5, 0), 'steel', 'Drill')
    m.add(lathe([(0.2, 0.0), (0.0, -0.34)], 12, cap_bottom=False).t(0, 0.52, 0), 'gold', 'Drill')
    m.add(tube(curve([(0.13, 0.5, 0), (0, 0.65, 0.13), (-0.13, 0.8, 0), (0, 0.95, -0.13), (0.13, 1.1, 0)], 16), 0.03, 5), 'hull_orange', 'Drill')
    m.nodes['Turret'].pos = (0, 0.0, 0)
    spin(m, 'Drill', 1.2)
    m.anim_pos('Attack', 'Turret', [(0, (0, 0, 0)), (0.12, (0, -0.25, 0)), (0.8, (0, 0, 0))])
    return m


@tower
def shieldnet():
    m = T('shieldnet', 'Schildmatrix', 1.25, (0, 1.4, 0))
    lbase(m, glow='glow_cyan')
    for i in range(3):
        a = TAU * i / 3
        m.add(kit.beam((math.cos(a) * 0.36, 0.16, math.sin(a) * 0.36), (math.cos(a) * 0.06, 1.25, math.sin(a) * 0.06), 0.055), 'hull_white', 'Base')
    m.add(lathe([(0.38, 0.0), (0.36, 0.12), (0.28, 0.26), (0.15, 0.34), (0.0, 0.36)], 24, cap_bottom=False, cap_top=False).t(0, 0.16, 0), 'glass', 'Base')
    m.node('Ring', 'Turret', (0, 1.38, 0))
    m.add(torus(0.2, 0.012, 32, 5).rx(0.5).t(0, 1.38, 0), 'glow_cyan', 'Ring')
    m.add(torus(0.15, 0.01, 32, 5).rz(0.6).t(0, 1.38, 0), 'hull_white', 'Ring')
    m.add(icosphere(0.08, 1).t(0, 1.38, 0), 'glow_cyan', 'Turret')
    m.nodes['Turret'].pos = (0, 1.25, 0)
    spin(m, 'Ring', 3.0)
    bob(m, 'Turret', 0.03, 2.6)
    pulse(m, 'Ring', 0.9, 1.35, 0.5, 'Attack')
    return m


# ================================================================ Lane-Einheiten

def trooper(m, s=1.0, armor='hull_white', visor='glow_cyan', mech=False):
    for x in (-0.07, 0.07):
        m.add(rbox(0.07 * s, 0.36 * s, 0.08 * s, 0.02 * s).t(x * s, 0.18 * s, 0), 'hull_dark' if not mech else 'gunmetal', 'Base')
        m.add(rbox(0.08 * s, 0.05 * s, 0.13 * s, 0.01).t(x * s, 0.025 * s, 0.02 * s), 'gunmetal', 'Base')
    m.add(rbox(0.22 * s, 0.1 * s, 0.14 * s, 0.03 * s).t(0, 0.38 * s, 0), 'hull_dark', 'Base')
    m.add(rbox(0.26 * s, 0.28 * s, 0.17 * s, 0.05 * s).t(0, 0.56 * s, 0), armor, 'Turret')
    m.add(rbox(0.12 * s, 0.03 * s, 0.01, 0.004).t(0, 0.62 * s, 0.09 * s), visor, 'Turret')
    for x in (-0.17, 0.17):
        m.add(sphere(0.06 * s, 0.05 * s, 0.06 * s, 10).t(x * s, 0.66 * s, 0), armor, 'Turret')
    m.add(sphere(0.085 * s, 0.09 * s, 0.09 * s, 12).t(0, 0.8 * s, 0), armor, 'Turret')
    m.add(rbox(0.13 * s, 0.035 * s, 0.03 * s, 0.01).t(0, 0.81 * s, 0.075 * s), visor, 'Turret')
    m.add(rbox(0.05 * s, 0.07 * s, 0.4 * s, 0.01).t(0.13 * s, 0.55 * s, 0.14 * s), 'gunmetal', 'Turret')
    m.add(cylinder(0.015 * s, 0.01, 8).rx(math.pi / 2).t(0.13 * s, 0.56 * s, 0.34 * s), visor, 'Turret')
    if mech:
        m.add(rbox(0.16 * s, 0.18 * s, 0.1 * s, 0.03 * s).t(0, 0.62 * s, -0.12 * s), 'gunmetal', 'Turret')
        m.add(merge([cylinder(0.025 * s, 0.1 * s, 8).rx(math.pi / 2).t(sg * 0.05 * s, 0.76 * s, -0.12 * s) for sg in (1, -1)]), 'hull_orange', 'Turret')
        m.add(antenna(0.2 * s).t(-0.08 * s, 0.86 * s, -0.08 * s), 'gunmetal', 'Turret')
    m.nodes['Turret'].pos = (0, 0.4 * s, 0)
    m.nodes['Muzzle'].pos = (0.13 * s, 0.56 * s, 0.36 * s)
    m.anim_pos('Idle', 'Turret', [(0, (0, 0, 0)), (0.8, (0, 0.012 * s, 0)), (1.6, (0, 0, 0))])
    m.anim_pos('Attack', 'Turret', [(0, (0, 0, 0)), (0.06, (0, 0, -0.03 * s)), (0.3, (0, 0, 0))])


@tower
def soldier():
    m = T('soldier', 'Klonsoldat', 0.4, (0, 0.9, 0.25))
    m.notes = 'Lane-Einheit des Klonbunkers (kein baubarer Turm).'
    trooper(m, 1.0)
    return m


@tower
def knight():
    m = T('knight', 'Kampfmech', 0.4, (0, 0.9, 0.25))
    m.notes = 'Lane-Einheit der Mech-Garnison (kein baubarer Turm).'
    trooper(m, 1.15, armor='hull_orange', visor='glow_orange', mech=True)
    return m


# ================================================================ Kartenobjekte (Kategorie world)

def W(name, title):
    m = Model('sf_' + name, 'world', title)
    m.node('Base', None, (0, 0, 0))
    return m


def world(fn):
    REG['sf_' + fn.__name__] = fn
    return fn


@world
def core():
    m = W('core', 'Fusionsreaktor (Kern)')
    m.node('Crystal', 'Base', (0, 1.1, 0))
    m.node('Ring', 'Base', (0, 1.1, 0))
    m.add(kit.plinth(0.5, 0.14, 8, bevel=0.02), 'hull_dark', 'Base')
    m.add(kit.band(0.46, 0.08, 0.02, 0.008), 'glow_cyan', 'Base')
    for i in range(4):
        a = TAU * i / 4 + math.pi / 4
        m.add(rbox(0.12, 0.9, 0.14, 0.03).ry(-a).t(math.cos(a) * 0.36, 0.55, math.sin(a) * 0.36), 'hull_white', 'Base')
        m.add(rbox(0.03, 0.7, 0.05, 0.005).ry(-a).t(math.cos(a) * 0.3, 0.55, math.sin(a) * 0.3), 'glow_cyan', 'Base')
    m.add(cylinder(0.2, 0.3, 16, bevel=0.02).t(0, 0.14, 0), 'gunmetal', 'Base')
    m.add(cylinder(0.14, 0.8, 16).t(0, 0.44, 0), 'glass', 'Base')
    m.add(icosphere(0.18, 2).t(0, 1.1, 0), 'glow_cyan', 'Crystal')
    m.add(torus(0.3, 0.025, 36, 6).rx(0.4).t(0, 1.1, 0), 'hull_white', 'Ring')
    m.add(torus(0.24, 0.012, 36, 5).rz(0.6).t(0, 1.1, 0), 'glow_cyan', 'Ring')
    m.anim_pos('Idle', 'Crystal', [(t, (0, 0.05 * math.sin(TAU * t / 3), 0)) for t in np.linspace(0, 3, 9)])
    m.anim_rot('Idle', 'Crystal', [(t, (0, 1, 0), 120 * t) for t in np.linspace(0, 3, 4)])
    m.anim_rot('Idle', 'Ring', [(t, (0, 1, 0), -120 * t) for t in np.linspace(0, 3, 4)])
    m.anim_scale('Attack', 'Crystal', [(0, 1), (0.15, 0.85), (0.5, 1)])
    return m


@world
def portal():
    m = W('portal', 'Sprungtor (Spawn)')
    m.node('Vortex', 'Base', (0, 0.8, 0))
    m.add(kit.plinth(0.5, 0.1, 8, bevel=0.02), 'hull_dark', 'Base')
    m.add(torus(0.55, 0.08, 40, 10).rx(math.pi / 2).t(0, 0.8, 0), 'hull_white', 'Base')
    m.add(merge([rbox(0.1, 0.1, 0.12, 0.02).t(math.cos(TAU * k / 8) * 0.55, 0.8 + math.sin(TAU * k / 8) * 0.55, 0) for k in range(8)]), 'gunmetal', 'Base')
    m.add(merge([sphere(0.03, seg=8).t(math.cos(TAU * k / 8 + 0.4) * 0.55, 0.8 + math.sin(TAU * k / 8 + 0.4) * 0.55, 0.08) for k in range(8)]), 'glow_magenta', 'Base')
    for sg in (1, -1):
        m.add(rbox(0.14, 0.32, 0.2, 0.03).t(sg * 0.5, 0.2, 0), 'hull_dark', 'Base')
    m.add(cylinder(0.5, 0.01, 40).rx(math.pi / 2).t(0, 0.8, 0), 'void', 'Vortex')
    m.add(merge([rbox(0.4, 0.02, 0.01, 0).rz(TAU * k / 6).t(0, 0.8, 0.01) for k in range(6)]), 'glow_magenta', 'Vortex')
    m.anim_rot('Idle', 'Vortex', [(t, (0, 0, 1), 90 * t) for t in np.linspace(0, 4, 5)])
    m.anim_scale('Attack', 'Vortex', [(0, 1), (0.2, 1.2), (0.6, 1)])
    return m


@world
def shop():
    m = W('shop', 'Versorgungsdepot')
    m.node('Sign', 'Base', (0.46, 1.1, -0.3))
    m.add(rbox(1.0, 0.08, 0.8, 0.02).t(0, 0.04, 0), 'hull_hazard', 'Base')
    m.add(rbox(0.8, 0.6, 0.6, 0.05).t(0, 0.38, 0), 'hull_white', 'Base')
    m.add(lathe([(0.42, 0.0), (0.4, 0.1), (0.3, 0.2), (0.0, 0.24)], 4, a0=math.pi / 4).s(1.0, 1, 0.75).t(0, 0.68, 0), 'hull_dark', 'Base')
    m.add(rbox(0.36, 0.3, 0.02, 0.01).t(0, 0.3, 0.31), 'glass', 'Base')
    m.add(rbox(0.82, 0.04, 0.62, 0.01).t(0, 0.6, 0), 'glow_cyan', 'Base')
    m.add(cylinder(0.02, 0.6, 6).t(0.46, 0.6, -0.3), 'gunmetal', 'Base')
    m.add(rbox(0.3, 0.16, 0.02, 0.01).t(0.46, 1.1, -0.3), 'glow_cyan', 'Sign')
    m.anim_rot('Idle', 'Sign', [(0, (0, 1, 0), 0), (1.5, (0, 1, 0), 15), (3, (0, 1, 0), 0), (4.5, (0, 1, 0), -15), (6, (0, 1, 0), 0)])
    return m


def _alien_tree(name, title, cap='alien_skin', stem='alien_shell', dead=False, seed=0):
    m = W(name, title)
    rng = np.random.default_rng(seed)
    pts = curve([(0, 0, 0), (0.05, 0.4, 0.02), (-0.04, 0.8, 0.0), (0.02, 1.15, -0.03)], 14)
    m.add(tube(pts, [0.1, 0.07, 0.05, 0.035], 8), stem, 'Base')
    for k in range(3):
        a = TAU * k / 3 + seed
        m.add(tube(curve([(0, 0.08, 0), (math.cos(a) * 0.2, 0.03, math.sin(a) * 0.2), (math.cos(a) * 0.32, 0.0, math.sin(a) * 0.32)], 8), [0.05, 0.03, 0.015], 6), stem, 'Base')
    if not dead:
        for (y, r) in ((1.15, 0.36), (0.8, 0.26), (0.5, 0.2)):
            m.add(lathe([(0.0, 0.0), (r, -0.02), (r * 1.05, 0.03), (r * 0.6, 0.1), (0.0, 0.12)], 12).t(rng.normal() * 0.03, y, rng.normal() * 0.03), cap, 'Base')
        m.add(merge([sphere(0.03, seg=6).t(math.cos(TAU * k / 7) * 0.3, 1.15, math.sin(TAU * k / 7) * 0.3) for k in range(7)]), 'glow_cyan', 'Base')
    else:
        for k in range(4):
            a = TAU * k / 4 + seed
            m.add(tube(curve([(0, 0.9, 0), (math.cos(a) * 0.18, 1.05, math.sin(a) * 0.18), (math.cos(a) * 0.3, 1.0, math.sin(a) * 0.3)], 8), [0.03, 0.02, 0.01], 5), stem, 'Base')
    m.anim_rot('Idle', 'Base', [(0, (0, 0, 1), 0), (2, (0, 0, 1), 1.5), (4, (0, 0, 1), 0), (6, (0, 0, 1), -1.5), (8, (0, 0, 1), 0)])
    return m


@world
def tree():
    return _alien_tree('tree', 'Xenobaum', seed=1)


@world
def tree_dead():
    return _alien_tree('tree_dead', 'Verdorrter Xenobaum', dead=True, seed=2)


@world
def willow():
    m = W('willow', 'Tentakelpflanze')
    for k in range(6):
        a = TAU * k / 6
        h = 0.9 + 0.3 * (k % 3) / 2
        pts = curve([(math.cos(a) * 0.06, 0, math.sin(a) * 0.06), (math.cos(a) * 0.15, h * 0.5, math.sin(a) * 0.15), (math.cos(a) * 0.3, h, math.sin(a) * 0.3), (math.cos(a) * 0.4, h * 0.85, math.sin(a) * 0.4)], 14)
        m.add(tube(pts, [0.07, 0.05, 0.03, 0.01], 7), 'alien_violet', 'Base')
        m.add(sphere(0.04, seg=8).t(math.cos(a) * 0.4, h * 0.85, math.sin(a) * 0.4), 'glow_magenta', 'Base')
    m.add(sphere(0.16, 0.1, 0.16, 12).t(0, 0.05, 0), 'alien_violet', 'Base')
    m.anim_rot('Idle', 'Base', [(0, (0, 0, 1), 0), (2.5, (0, 0, 1), 2), (5, (0, 0, 1), 0), (7.5, (0, 0, 1), -2), (10, (0, 0, 1), 0)])
    return m


@world
def rock():
    m = W('rock', 'Meteoritengestein')
    from .sdf import Sculpt
    sc = Sculpt(detail=0.02, dfreq=5, seed=7)
    sc.ellipsoid((0, 0.15, 0), (0.4, 0.3, 0.34), 'hull_dark', k=0.1, noise=0.05)
    sc.ellipsoid((0.25, 0.08, -0.15), (0.2, 0.16, 0.18), 'hull_dark', k=0.1, noise=0.05)
    sc.box((0, -1.0, 0), (3, 1.0, 3), 'hull_dark', k=0.004, sub=True)
    m.sculpt(sc, voxel=0.02, max_dim=80, node='Base', target=1500)
    m.add(kit.crystal_cluster(3, 0.03, 0.1, 0.04, seed=3).t(-0.15, 0.28, 0.12), 'glow_cyan', 'Base')
    return m


@world
def crystals():
    m = W('crystals', 'Energiekristalle')
    m.add(kit.crystal_cluster(7, 0.1, 0.9, 0.14, seed=5), 'crystal_cyan', 'Base')
    m.add(cylinder(0.3, 0.05, 12).t(0, 0.0, 0), 'hull_dark', 'Base')
    m.add(torus(0.28, 0.01, 24, 4).t(0, 0.05, 0), 'glow_cyan', 'Base')
    return m


@world
def lavapool():
    m = W('lavapool', 'Plasmabecken')
    m.add(lathe([(0.42, 0.0), (0.44, 0.06), (0.38, 0.08), (0.0, 0.05)], 16, cap_bottom=False), 'hull_dark', 'Base')
    m.add(cylinder(0.36, 0.02, 20).t(0, 0.05, 0), 'glow_orange', 'Base')
    for k in range(4):
        a = TAU * k / 4 + 0.4
        m.add(rbox(0.08, 0.12, 0.08, 0.01).t(math.cos(a) * 0.43, 0.06, math.sin(a) * 0.43), 'hull_hazard', 'Base')
    return m


@world
def iceblock():
    m = W('iceblock', 'Kryobehälter')
    m.add(rbox(0.6, 0.06, 0.5, 0.02).t(0, 0.03, 0), 'gunmetal', 'Base')
    m.add(cylinder(0.2, 0.7, 14).rz(math.pi / 2).t(-0.3, 0.26, 0), 'glass', 'Base')
    m.add(merge([torus(0.2, 0.02, 16, 4).rz(math.pi / 2).t(x, 0.26, 0) for x in (-0.25, 0.0, 0.25)]), 'hull_white', 'Base')
    m.add(rbox(0.12, 0.3, 0.3, 0.02).t(0.36, 0.2, 0), 'hull_white', 'Base')
    m.add(rbox(0.02, 0.06, 0.12, 0.004).t(0.43, 0.25, 0), 'glow_cyan', 'Base')
    return m


@world
def ruin():
    m = W('ruin', 'Mech-Wrack')
    m.add(rbox(0.5, 0.3, 0.4, 0.05).rz(0.3).t(0, 0.18, 0), 'hull_dark', 'Base')
    m.add(rbox(0.2, 0.18, 0.2, 0.04).t(0.25, 0.42, 0.05), 'hull_orange', 'Base')
    m.add(rbox(0.12, 0.03, 0.02, 0.004).t(0.25, 0.44, 0.16), 'glow_orange', 'Base')
    m.add(kit.beam((-0.2, 0.05, 0.25), (-0.45, 0.3, 0.4), 0.06), 'gunmetal', 'Base')
    m.add(kit.beam((0.1, 0.05, -0.25), (0.35, 0.7, -0.3), 0.07), 'gunmetal', 'Base')
    m.add(barrel(0.4, 0.03).rx(-0.3).t(-0.1, 0.32, 0.1), 'gunmetal', 'Base')
    return m


@world
def mushroom():
    m = W('mushroom', 'Alienkapsel')
    for (x, z, s) in ((0, 0, 1.0), (0.22, 0.1, 0.6), (-0.18, -0.14, 0.5)):
        m.add(lathe([(0.0, 0.0), (0.12 * s, 0.02), (0.2 * s, 0.2 * s), (0.14 * s, 0.42 * s), (0.0, 0.5 * s)], 12).t(x, 0, z), 'alien_shell', 'Base')
        m.add(sphere(0.05 * s, seg=8).t(x, 0.46 * s, z), 'glow_poison', 'Base')
    return m


@world
def glowshroom():
    m = W('glowshroom', 'Leuchtqualle')
    m.add(tube(curve([(0, 0, 0), (0.02, 0.3, 0), (-0.02, 0.6, 0.02)], 10), [0.04, 0.03, 0.02], 6), 'alien_violet', 'Base')
    m.add(lathe([(0.0, 0.0), (0.3, -0.04), (0.32, 0.02), (0.2, 0.16), (0.0, 0.2)], 16).t(0, 0.6, 0), 'glass', 'Base')
    m.add(sphere(0.12, seg=10).t(0, 0.66, 0), 'glow_magenta', 'Base')
    m.add(merge([tube(curve([(math.cos(TAU * k / 6) * 0.25, 0.58, math.sin(TAU * k / 6) * 0.25), (math.cos(TAU * k / 6) * 0.28, 0.4, math.sin(TAU * k / 6) * 0.28)], 6), 0.01, 4) for k in range(6)]), 'glow_magenta', 'Base')
    return m


@world
def cloudbank():
    m = W('cloudbank', 'Gasnebel')
    for k in range(5):
        a = TAU * k / 5
        m.add(icosphere(0.18 + 0.04 * (k % 2), 2).s(1, 0.6, 1).t(math.cos(a) * 0.2, 0.2 + 0.05 * (k % 3), math.sin(a) * 0.2), 'glass_green', 'Base')
    m.anim_pos('Idle', 'Base', [(t, (0, 0.04 * math.sin(TAU * t / 5), 0)) for t in np.linspace(0, 5, 9)])
    return m
