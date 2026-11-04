"""Weltobjekte: Kern, Rissportal, Laden und Hindernisse (Bäume, Felsen, Kristalle …)."""
from __future__ import annotations

import math

import numpy as np

from . import kit
from .geo import TAU, crystal, curve, cylinder, displace, icosphere, lathe, merge, rbox, sphere, spike, torus, tube
from .model import Model
from .sdf import Sculpt, rot

REG = {}


def world(fn):
    REG[fn.__name__] = fn
    return fn


def W(name, title):
    m = Model(name, 'world', title)
    m.node('Base', None, (0, 0, 0))
    return m


def sc_add(m, sc, node='Base', target=6000, voxel=0.012):
    m.sculpt(sc, voxel=voxel, max_dim=120, node=node, target=target)


@world
def core():
    m = W('core', 'Kern (Ziel)')
    m.node('Crystal', 'Base', (0, 1.1, 0))
    m.node('Ring', 'Base', (0, 1.1, 0))
    m.add(kit.plinth(0.48, 0.18, 8), 'stone_dark', 'Base')
    m.add(cylinder(0.38, 0.12, 8, 0.33, bevel=0.02).t(0, 0.18, 0), 'runes_cyan', 'Base')
    for i in range(4):
        a = TAU * i / 4 + math.pi / 4
        m.add(lathe([(0.06, 0), (0.05, 0.6), (0.07, 0.64), (0, 0.8)], 6).t(math.cos(a) * 0.38, 0.18, math.sin(a) * 0.38), 'obsidian', 'Base')
        m.add(crystal(0.025, 0.1, 5).t(math.cos(a) * 0.38, 0.98, math.sin(a) * 0.38), 'crystal_cyan', 'Base')
    m.add(crystal(0.16, 0.7, 6, 0.35, 0.35).t(0, 0.8, 0), 'crystal_cyan', 'Crystal')
    m.add(merge([torus(0.3, 0.012, 40, 5).rx(a) for a in (0.3, 1.9)]), 'gold', 'Ring')
    m.nodes['Ring'].pos = (0, 1.1, 0)
    m.parts[-1].mesh.t(0, 1.1, 0)
    m.anim_pos('Idle', 'Crystal', [(t, (0, 0.05 * math.sin(TAU * t / 3), 0)) for t in np.linspace(0, 3, 9)])
    m.anim_rot('Idle', 'Crystal', [(t, (0, 1, 0), 120 * t) for t in np.linspace(0, 3, 4)])
    m.anim_rot('Idle', 'Ring', [(t, (0, 1, 0), -120 * t) for t in np.linspace(0, 3, 4)])
    m.anim_scale('Attack', 'Crystal', [(0, 1), (0.15, 0.85), (0.5, 1)])
    m.notes = 'Attack = Treffer am Kern (Kernenergie verloren).'
    return m


@world
def portal():
    m = W('portal', 'Rissportal (Gegner-Spawn)')
    m.node('Vortex', 'Base', (0, 0.8, 0))
    m.add(cylinder(0.45, 0.08, 16, 0.42, bevel=0.02), 'obsidian', 'Base')
    for sg in (1, -1):
        m.add(spike(0.07, 1.25, 6).rz(sg * 0.12).t(sg * 0.42, 0.05, 0), 'obsidian', 'Base')
        m.add(kit.chain((sg * 0.4, 0.9, 0), (sg * 0.15, 1.25, 0), 5, 0.04, 0.016), 'iron_rust', 'Base')
    m.add(torus(0.42, 0.05, 40, 8).rx(math.pi / 2).t(0, 0.8, 0), 'chitin', 'Base')
    m.add(kit.skull(0.1).t(0, 1.2, 0.03), 'bone', 'Base')
    m.add(sphere(0.38, 0.38, 0.03, 32).t(0, 0.8, 0), 'void', 'Vortex')
    m.add(torus(0.36, 0.01, 40, 4).rx(math.pi / 2).t(0, 0.8, 0), 'glow_arcane', 'Vortex')
    m.anim_rot('Idle', 'Vortex', [(t, (0, 0, 1), 90 * t) for t in np.linspace(0, 4, 5)])
    m.anim_scale('Attack', 'Vortex', [(0, 1), (0.2, 1.2), (0.6, 1)])
    m.notes = 'Attack = Gegner tritt aus dem Portal.'
    return m


@world
def shop():
    m = W('shop', 'Laden')
    m.node('Sign', 'Base', (0.46, 1.1, -0.3))
    m.add(kit.planks_platform(0.48, 0.03, 0.04, 9), 'wood_black', 'Base')
    m.add(rbox(0.8, 0.6, 0.7, 0.02).t(0, 0.35, -0.05), 'stone_blocks', 'Base')
    m.add(extrude_roof(), 'wood_black', 'Base')
    m.add(rbox(0.86, 0.05, 0.25, 0.01).rx(-0.5).t(0, 0.62, 0.36), 'cloth_red', 'Base')
    m.add(rbox(0.26, 0.4, 0.02, 0.0).t(0, 0.25, 0.3), 'wood', 'Base')
    for x, z in ((-0.3, 0.42), (0.28, 0.44)):
        m.add(rbox(0.16, 0.16, 0.16, 0.02).t(x, 0.11, z), 'wood', 'Base')
    m.add(sphere(0.08, seg=12).t(0.28, 0.27, 0.44), 'crystal_violet', 'Base')
    m.add(cylinder(0.02, 1.2, 6).t(0.46, 0.0, -0.3), 'wood', 'Base')
    cl, _ = kit.banner(0.2, 0.3, pole=False)
    m.add(cl.ry(math.pi / 2).t(0.47, 1.12, -0.3), 'cloth_violet', 'Sign')
    m.add(kit.skull(0.07).t(0, 0.85, 0.32), 'bone', 'Base')
    m.add(merge([lathe([(0.04, 0), (0.05, 0.05), (0.03, 0.1), (0.015, 0.14), (0, 0.14)], 8).t(x, 0.65, 0.2) for x in (-0.25, -0.15)]), 'glow_poison', 'Base')
    m.anim_rot('Idle', 'Sign', [(0, (0, 1, 0), 0), (1.5, (0, 1, 0), 10), (3, (0, 1, 0), 0), (4.5, (0, 1, 0), -10), (6, (0, 1, 0), 0)])
    return m


def extrude_roof():
    from .geo import extrude
    return extrude([(-0.5, 0), (0.5, 0), (0, 0.45)], 0.85).t(0, 0.65, -0.05)


def _tree(name, title, leaf, trunk='bark', dead=False, seed=0):
    m = W(name, title)
    sc = Sculpt(detail=0.006, dfreq=10, seed=seed)
    rng = np.random.default_rng(seed)
    sc.capsule((0, -0.05, 0), (0.03, 0.9, 0.0), 0.11, 0.06, trunk, k=0.05, noise=0.01)
    for i in range(4):
        a = TAU * i / 4 + rng.random()
        sc.capsule((0, 0.15, 0), (math.cos(a) * 0.28, -0.02, math.sin(a) * 0.28), 0.06, 0.02, trunk, k=0.05)
    for i in range(5):
        a = TAU * i / 5 + rng.random()
        p1 = (math.cos(a) * 0.35, 1.1 + rng.random() * 0.2, math.sin(a) * 0.35)
        sc.capsule((0.02, 0.75, 0), p1, 0.045, 0.012, trunk, k=0.04)
        if not dead:
            sc.ellipsoid(p1, (0.24, 0.18, 0.24), leaf, k=0.1, noise=0.05)
    if not dead:
        sc.ellipsoid((0.02, 1.25, 0), (0.32, 0.25, 0.32), leaf, k=0.12, noise=0.05)
    sc_add(m, sc, target=7000, voxel=0.016)
    m.anim_rot('Idle', 'Base', [(0, (0, 0, 1), 0), (2, (0, 0, 1), 1.5), (4, (0, 0, 1), 0), (6, (0, 0, 1), -1.5), (8, (0, 0, 1), 0)])
    return m


@world
def tree():
    return _tree('tree', 'Hindernis: Laubbaum (dunkel)', 'flesh_moss', seed=401)


@world
def tree_dead():
    m = _tree('tree_dead', 'Hindernis: Toter Baum', None, 'bark', dead=True, seed=402)
    m.add(kit.skull(0.07).t(0.05, 0.6, 0.1), 'bone', 'Base')
    return m


@world
def willow():
    m = W('willow', 'Hindernis: Trauerweide')
    sc = Sculpt(detail=0.006, dfreq=10, seed=403)
    sc.capsule((0, -0.05, 0), (0, 0.8, 0), 0.12, 0.08, 'bark', k=0.05, noise=0.01)
    sc.ellipsoid((0, 1.0, 0), (0.42, 0.22, 0.42), 'flesh_moss', k=0.1, noise=0.04)
    for i in range(10):
        a = TAU * i / 10
        sc.capsule((math.cos(a) * 0.3, 0.95, math.sin(a) * 0.3), (math.cos(a) * 0.42, 0.3, math.sin(a) * 0.42), 0.05, 0.015, 'flesh_moss', k=0.05)
    sc_add(m, sc, target=7000, voxel=0.016)
    return m


@world
def rock():
    m = W('rock', 'Hindernis: Fels')
    sc = Sculpt(detail=0.02, dfreq=4, seed=404)
    sc.ellipsoid((0, 0.18, 0), (0.42, 0.3, 0.36), 'rock', k=0.08, noise=0.05)
    sc.ellipsoid((0.25, 0.1, -0.2), (0.2, 0.16, 0.18), 'rock', k=0.08, noise=0.04)
    sc.ellipsoid((-0.1, 0.35, 0.05), (0.2, 0.1, 0.2), 'flesh_moss', k=0.05, noise=0.02)
    sc_add(m, sc, target=4000, voxel=0.015)
    return m


@world
def crystals():
    m = W('crystals', 'Hindernis: Kristallgruppe')
    m.add(kit.boulders(5, 0.3, 0.08, seed=4), 'rock', 'Base')
    m.add(kit.crystal_cluster(7, 0.09, 0.8, 0.18, seed=6), 'crystal_violet', 'Base')
    return m


@world
def lavapool():
    m = W('lavapool', 'Hindernis: Lavabecken')
    m.add(lathe([(0.0, 0.02), (0.36, 0.02), (0.4, 0.04), (0.46, 0.1), (0.48, 0.04), (0.46, 0.0), (0, 0)], 20, cap_bottom=False, cap_top=False), 'rock_red', 'Base')
    m.add(displace(sphere(0.37, 0.03, 0.37, 24).t(0, 0.04, 0), 0.006, 10), 'ember', 'Base')
    m.add(kit.boulders(6, 0.45, 0.07, seed=7), 'obsidian', 'Base')
    return m


@world
def iceblock():
    m = W('iceblock', 'Hindernis: Eisblock')
    sc = Sculpt(detail=0.004, dfreq=8, seed=406)
    sc.box((0, 0.3, 0), (0.33, 0.3, 0.28), 'ice', k=0.05, r=0.06, noise=0.02, R=rot(15, 0, 4))
    sc_add(m, sc, target=3000, voxel=0.015)
    m.add(kit.crystal_cluster(4, 0.06, 0.45, 0.08, seed=2).t(0.15, 0.4, -0.05), 'ice', 'Base')
    return m


@world
def ruin():
    m = W('ruin', 'Hindernis: Ruine')
    m.add(lathe([(0.12, 0), (0.12, 0.85), (0.15, 0.88), (0.15, 0.95), (0, 0.95)], 10).t(-0.2, 0, 0), 'stone_pale', 'Base')
    m.add(rbox(0.36, 0.08, 0.36, 0.02).t(-0.2, 0.99, 0), 'stone_pale', 'Base')
    m.add(cylinder(0.11, 0.6, 10).rz(math.pi / 2).ry(0.6).t(0.15, 0.11, 0.18), 'stone_pale', 'Base')
    m.add(rbox(0.4, 0.24, 0.26, 0.03).t(0.22, 0.12, -0.2), 'stone_blocks', 'Base')
    m.add(kit.boulders(5, 0.38, 0.05, seed=9), 'rock', 'Base')
    return m


@world
def mushroom():
    m = W('mushroom', 'Hindernis: Riesenpilz')
    sc = Sculpt(detail=0.004, dfreq=12, seed=408)
    sc.capsule((0, 0, 0), (0.03, 0.5, 0), 0.1, 0.07, 'bone', k=0.04)
    sc.ellipsoid((0.03, 0.55, 0), (0.42, 0.16, 0.42), 'chitin_red', k=0.06)
    sc.ellipsoid((0.03, 0.46, 0), (0.38, 0.06, 0.38), 'flesh', k=0.03)
    sc.capsule((0.25, 0, 0.15), (0.27, 0.22, 0.16), 0.05, 0.04, 'bone', k=0.03)
    sc.ellipsoid((0.27, 0.24, 0.16), (0.14, 0.06, 0.14), 'chitin_red', k=0.03)
    sc_add(m, sc, target=4000, voxel=0.012)
    return m


@world
def glowshroom():
    m = W('glowshroom', 'Hindernis: Leuchtpilz')
    sc = Sculpt(detail=0.003, dfreq=14, seed=409)
    for x, z, h, r in ((0, 0, 0.6, 0.3), (0.28, 0.18, 0.3, 0.15), (-0.22, 0.2, 0.22, 0.12)):
        sc.capsule((x, 0, z), (x, h, z), r * 0.25, r * 0.18, 'bone', k=0.03)
        sc.ellipsoid((x, h + 0.03, z), (r, r * 0.35, r), 'crystal_cyan', k=0.03)
    sc_add(m, sc, target=4000, voxel=0.012)
    return m


@world
def cloudbank():
    m = W('cloudbank', 'Hindernis: Nebelbank (Himmelsinseln)')
    sc = Sculpt(detail=0.01, dfreq=5, seed=410)
    for x, y, z, r in ((0, 0.25, 0, 0.35), (0.25, 0.3, 0.1, 0.25), (-0.25, 0.2, -0.05, 0.25), (0.05, 0.45, -0.05, 0.22)):
        sc.ellipsoid((x, y, z), (r, r * 0.7, r), 'glow_white', k=0.15)
    sc_add(m, sc, target=3000, voxel=0.02)
    m.anim_pos('Idle', 'Base', [(t, (0, 0.04 * math.sin(TAU * t / 5), 0)) for t in np.linspace(0, 5, 9)])
    return m
