"""Anatomie-Bausteine für den Bildhauer: Körper, Köpfe, Hände, Krallen, Hörner, Flügel.
Alle Funktionen schreiben Formen in ein Sculpt und binden sie an Knochen eines Skeletts."""
from __future__ import annotations

import math

import numpy as np

from .sdf import Sculpt, rot


def V(*a):
    return np.array(a, dtype=float)


def humanoid_body(sc: Sculpt, H, skin='skin', bulk=1.0, gaunt=0.0, muscle=0.6, belly=0.0, head='human', hands='hand',
                  feet='boot', neck=1.0, head_size=1.0, claws=False, horns=None, ears=None, tail=None, skin2=None):
    """Organischer Zweibeiner auf Basis eines rig.Humanoid. gaunt (0–1) = ausgemergelt, muscle = Muskelrelief."""
    P = {k: V(*v) for k, v in H.P.items()}
    h = H.h
    w = bulk
    s2 = skin2 or skin
    # Rumpf
    sc.ellipsoid(P['chest'] + V(0, 0.02 * h, 0.0), (0.15 * h * w, 0.12 * h, 0.1 * h * w * (1 - 0.2 * gaunt)), skin, 'chest', k=0.06 * h)
    sc.ellipsoid(P['chest'] + V(0, -0.02 * h, 0.04 * h), (0.11 * h * w, 0.08 * h, 0.06 * h), skin, 'chest', k=0.04 * h)  # Brust
    sc.ellipsoid(P['spine'] + V(0, 0, 0.01 * h + belly * 0.04 * h), (0.12 * h * w * (1 - 0.25 * gaunt) + belly * 0.04 * h, 0.1 * h, 0.085 * h * w + belly * 0.06 * h), skin, 'spine', k=0.06 * h)
    sc.ellipsoid(P['hips'] + V(0, 0.01 * h, 0), (0.13 * h * w, 0.08 * h, 0.09 * h * w), skin, 'hips', k=0.05 * h)
    if gaunt > 0.3:  # Rippen
        for i in range(4):
            y = P['chest'][1] - 0.03 * h - i * 0.03 * h
            for sg in (1, -1):
                sc.capsule(V(sg * 0.03 * h, y, 0.085 * h), V(sg * 0.12 * h * w, y - 0.01 * h, 0.03 * h), 0.008 * h, 0.006 * h, skin, 'chest', k=0.015 * h)
    # Schultern / Nacken
    for sg, sd in ((1, 'L'), (-1, 'R')):
        sc.ellipsoid(P['upperarm_' + sd] + V(-sg * 0.02 * h, 0.01 * h, 0), (0.06 * h * w, 0.05 * h, 0.06 * h), skin, 'upperarm_' + sd, k=0.04 * h)
        sc.capsule(P['neck'] + V(0, -0.02 * h, -0.01 * h), P['upperarm_' + sd] + V(-sg * 0.03 * h, 0.02 * h, -0.01 * h), 0.035 * h * w, 0.03 * h, skin, 'chest', k=0.04 * h)
    sc.capsule(P['neck'] + V(0, -0.03 * h, 0), P['head'] + V(0, 0.01 * h, 0.005 * h), 0.038 * h * neck, 0.034 * h * neck, skin, 'neck', k=0.03 * h)
    # Arme
    for sg, sd in ((1, 'L'), (-1, 'R')):
        ua, fa, ha = P['upperarm_' + sd], P['forearm_' + sd], P['hand_' + sd]
        sc.capsule(ua, fa, 0.042 * h * w, 0.034 * h * w, skin, 'upperarm_' + sd, k=0.03 * h)
        if muscle > 0:
            mid = ua * 0.55 + fa * 0.45
            sc.ellipsoid(mid + V(0, 0, 0.012 * h), (0.034 * h * w * (0.7 + muscle * 0.5), 0.06 * h, 0.032 * h * w * (0.7 + muscle * 0.5)), skin, 'upperarm_' + sd, k=0.025 * h)
        sc.capsule(fa, ha, 0.034 * h * w, 0.024 * h * w * (1 - 0.2 * gaunt), skin, 'forearm_' + sd, k=0.025 * h)
        if muscle > 0:
            sc.ellipsoid(fa * 0.7 + ha * 0.3 + V(0, 0, 0.005 * h), (0.03 * h * w, 0.05 * h, 0.028 * h * w), skin, 'forearm_' + sd, k=0.02 * h)
        if hands == 'hand' or hands == 'claw':
            hand(sc, ha, sg, h, s2, 'hand_' + sd, claw=(hands == 'claw') or claws)
        elif hands == 'stump':
            sc.ellipsoid(ha + V(0, -0.03 * h, 0), (0.03 * h, 0.04 * h, 0.03 * h), s2, 'hand_' + sd)
    # Beine
    for sg, sd in ((1, 'L'), (-1, 'R')):
        th, sh, ft = P['thigh_' + sd], P['shin_' + sd], P['foot_' + sd]
        sc.capsule(th + V(0, 0.02 * h, 0), sh, 0.065 * h * w * (1 - 0.3 * gaunt), 0.045 * h * w, skin, 'thigh_' + sd, k=0.045 * h)
        if muscle > 0:
            sc.ellipsoid(th * 0.5 + sh * 0.5 + V(0, 0.02 * h, 0.02 * h), (0.05 * h * w, 0.09 * h, 0.045 * h * w), skin, 'thigh_' + sd, k=0.03 * h)
        sc.capsule(sh, ft + V(0, 0.02 * h, 0), 0.043 * h * w * (1 - 0.2 * gaunt), 0.03 * h * w, skin, 'shin_' + sd, k=0.03 * h)
        sc.ellipsoid(sh * 0.65 + ft * 0.35 + V(0, 0, -0.022 * h), (0.035 * h * w, 0.07 * h, 0.03 * h * w), skin, 'shin_' + sd, k=0.025 * h)  # Wade
        if feet == 'claw':
            for k in (-1, 0, 1):
                tip = ft + V(k * 0.025 * h, -0.04 * h, 0.12 * h)
                sc.capsule(ft + V(0, -0.02 * h, 0.02 * h), tip, 0.022 * h, 0.008 * h, s2, 'foot_' + sd, k=0.015 * h)
                sc.capsule(tip, tip + V(0, -0.012 * h, 0.03 * h), 0.008 * h, 0.002 * h, 'bone', 'foot_' + sd, k=0.004 * h)
            sc.capsule(ft + V(0, -0.02 * h, 0), ft + V(0, -0.04 * h, -0.05 * h), 0.02 * h, 0.01 * h, s2, 'foot_' + sd, k=0.01 * h)
        elif feet == 'hoof':
            sc.capsule(ft, ft + V(0, -0.04 * h, 0.02 * h), 0.04 * h, 0.045 * h, 'obsidian', 'foot_' + sd, k=0.01 * h)
        else:
            sc.ellipsoid(ft + V(0, -0.025 * h, 0.045 * h), (0.04 * h * w, 0.03 * h, 0.08 * h), s2, 'foot_' + sd, k=0.02 * h)
    # Kopf
    head_shape(sc, P['head'], h * head_size, skin, kind=head, horns=horns, ears=ears)
    if tail:
        mat, L = tail
        pts = [P['hips'] + V(0, 0.0, -0.08 * h), P['hips'] + V(0, -0.1 * h, -0.25 * h * L), P['hips'] + V(0, -0.25 * h, -0.45 * h * L), P['hips'] + V(0.05 * h, -0.4 * h, -0.6 * h * L)]
        sc.chain(pts, [0.05 * h, 0.035 * h, 0.02 * h, 0.006 * h], mat, 'hips', k=0.03 * h)
    return P


def hand(sc, ha, sg, h, mat, bone, claw=False):
    palm = ha + V(-sg * 0.002 * h, -0.035 * h, 0.008 * h)
    sc.box(palm, (0.017 * h, 0.032 * h, 0.03 * h), mat, bone, k=0.015 * h, r=0.012 * h)
    for i in range(4):
        z = palm[2] + (i - 1.5) * 0.015 * h
        base = V(palm[0], palm[1] - 0.03 * h, z)
        tip = base + V(-sg * 0.004 * h, -0.05 * h * (1.25 if claw else 1), 0.012 * h)
        sc.capsule(base, tip, 0.0075 * h, 0.0055 * h, mat, bone, k=0.006 * h)
        if claw:
            sc.capsule(tip, tip + V(0, -0.025 * h, 0.02 * h), 0.006 * h, 0.0015 * h, 'bone', bone, k=0.003 * h)
    thumb = V(palm[0] - sg * 0.0, palm[1] + 0.005 * h, palm[2] + 0.03 * h)
    sc.capsule(thumb, thumb + V(sg * 0.005 * h, -0.035 * h, 0.022 * h), 0.009 * h, 0.006 * h, mat, bone, k=0.006 * h)


def head_shape(sc, c, h, mat, kind='human', horns=None, ears=None):
    """kind: human | ghoul | beast | skull | brute | insect"""
    c = V(*c)
    if kind == 'human':
        sc.ellipsoid(c + V(0, 0.065 * h, 0), (0.06 * h, 0.075 * h, 0.07 * h), mat, 'head', k=0.02 * h)
        sc.ellipsoid(c + V(0, 0.035 * h, 0.03 * h), (0.045 * h, 0.05 * h, 0.05 * h), mat, 'head', k=0.02 * h)  # Kiefer
        sc.capsule(c + V(0, 0.07 * h, 0.065 * h), c + V(0, 0.045 * h, 0.08 * h), 0.009 * h, 0.012 * h, mat, 'head', k=0.008 * h)  # Nase
        sc.capsule(c + V(-0.03 * h, 0.085 * h, 0.06 * h), c + V(0.03 * h, 0.085 * h, 0.06 * h), 0.011 * h, 0.011 * h, mat, 'head', k=0.012 * h)  # Brauen
        for sg in (1, -1):
            sc.ellipsoid(c + V(sg * 0.022 * h, 0.072 * h, 0.064 * h), 0.011 * h, mat, 'head', sub=True, k=0.008 * h)  # Augenhöhlen
            sc.ellipsoid(c + V(sg * 0.022 * h, 0.072 * h, 0.058 * h), 0.009 * h, 'glow_white' if False else mat, 'head', k=0.002 * h)
            sc.ellipsoid(c + V(sg * 0.06 * h, 0.065 * h, 0.0), (0.008 * h, 0.018 * h, 0.012 * h), mat, 'head', k=0.008 * h)  # Ohren
    elif kind == 'ghoul':
        sc.ellipsoid(c + V(0, 0.06 * h, 0), (0.055 * h, 0.07 * h, 0.065 * h), mat, 'head', k=0.02 * h)
        sc.ellipsoid(c + V(0, 0.025 * h, 0.04 * h), (0.04 * h, 0.035 * h, 0.05 * h), mat, 'head', k=0.015 * h)
        sc.capsule(c + V(-0.035 * h, 0.085 * h, 0.055 * h), c + V(0.035 * h, 0.085 * h, 0.055 * h), 0.014 * h, 0.014 * h, mat, 'head', k=0.01 * h)
        for sg in (1, -1):
            sc.ellipsoid(c + V(sg * 0.024 * h, 0.07 * h, 0.06 * h), 0.014 * h, mat, 'head', sub=True, k=0.006 * h)
            sc.ellipsoid(c + V(sg * 0.024 * h, 0.07 * h, 0.052 * h), 0.008 * h, 'glow_blood', 'head', k=0.001 * h)
            sc.capsule(c + V(sg * 0.055 * h, 0.07 * h, -0.0), c + V(sg * 0.1 * h, 0.1 * h, -0.03 * h), 0.012 * h, 0.002 * h, mat, 'head', k=0.008 * h)
        for i in range(5):  # Zähne
            x = (i - 2) * 0.012 * h
            sc.capsule(c + V(x, 0.03 * h, 0.085 * h), c + V(x, 0.012 * h, 0.088 * h), 0.004 * h, 0.001 * h, 'bone', 'head', k=0.002 * h)
    elif kind == 'beast':
        sc.ellipsoid(c + V(0, 0.06 * h, 0), (0.065 * h, 0.065 * h, 0.07 * h), mat, 'head', k=0.02 * h)
        sc.capsule(c + V(0, 0.05 * h, 0.04 * h), c + V(0, 0.035 * h, 0.13 * h), 0.04 * h, 0.03 * h, mat, 'head', k=0.02 * h)  # Schnauze
        sc.capsule(c + V(0, 0.015 * h, 0.04 * h), c + V(0, 0.005 * h, 0.12 * h), 0.03 * h, 0.022 * h, mat, 'head', k=0.015 * h)
        for sg in (1, -1):
            sc.ellipsoid(c + V(sg * 0.03 * h, 0.08 * h, 0.07 * h), 0.008 * h, 'glow_fire', 'head', k=0.001 * h)
            sc.capsule(c + V(sg * 0.012 * h, 0.02 * h, 0.125 * h), c + V(sg * 0.014 * h, -0.005 * h, 0.13 * h), 0.006 * h, 0.001 * h, 'bone', 'head', k=0.002 * h)
    elif kind == 'skull':
        sc.ellipsoid(c + V(0, 0.07 * h, -0.005 * h), (0.058 * h, 0.065 * h, 0.07 * h), 'bone', 'head', k=0.015 * h)
        sc.box(c + V(0, 0.025 * h, 0.035 * h), (0.035 * h, 0.022 * h, 0.035 * h), 'bone', 'head', k=0.015 * h, r=0.01 * h)
        for sg in (1, -1):
            sc.ellipsoid(c + V(sg * 0.024 * h, 0.065 * h, 0.06 * h), 0.017 * h, 'bone', 'head', sub=True, k=0.005 * h)
            sc.ellipsoid(c + V(sg * 0.024 * h, 0.065 * h, 0.05 * h), 0.007 * h, 'glow_arcane', 'head', k=0.001 * h)
        sc.ellipsoid(c + V(0, 0.045 * h, 0.07 * h), (0.008 * h, 0.012 * h, 0.01 * h), 'bone', 'head', sub=True, k=0.004 * h)
    elif kind == 'brute':
        sc.ellipsoid(c + V(0, 0.055 * h, 0.01 * h), (0.07 * h, 0.06 * h, 0.07 * h), mat, 'head', k=0.03 * h)
        sc.ellipsoid(c + V(0, 0.02 * h, 0.045 * h), (0.06 * h, 0.04 * h, 0.05 * h), mat, 'head', k=0.02 * h)
        sc.capsule(c + V(-0.045 * h, 0.075 * h, 0.06 * h), c + V(0.045 * h, 0.075 * h, 0.06 * h), 0.018 * h, 0.018 * h, mat, 'head', k=0.012 * h)
        for sg in (1, -1):
            sc.ellipsoid(c + V(sg * 0.025 * h, 0.065 * h, 0.075 * h), 0.006 * h, 'glow_fire', 'head', k=0.001 * h)
            sc.capsule(c + V(sg * 0.03 * h, 0.01 * h, 0.08 * h), c + V(sg * 0.035 * h, 0.05 * h, 0.095 * h), 0.009 * h, 0.002 * h, 'bone', 'head', k=0.003 * h)
    elif kind == 'insect':
        sc.ellipsoid(c + V(0, 0.05 * h, 0.02 * h), (0.05 * h, 0.05 * h, 0.07 * h), mat, 'head', k=0.02 * h)
        for sg in (1, -1):
            sc.ellipsoid(c + V(sg * 0.035 * h, 0.065 * h, 0.06 * h), (0.02 * h, 0.022 * h, 0.018 * h), 'crystal_red', 'head', k=0.004 * h)
            sc.chain([c + V(sg * 0.02 * h, 0.02 * h, 0.08 * h), c + V(sg * 0.04 * h, 0.0, 0.12 * h), c + V(sg * 0.015 * h, -0.01 * h, 0.15 * h)], [0.012 * h, 0.008 * h, 0.002 * h], 'chitin', 'head', k=0.004 * h)
    if horns:
        mat_h, L, curl = horns
        for sg in (1, -1):
            b = c + V(sg * 0.04 * h, 0.11 * h, 0.01 * h)
            pts = [b, b + V(sg * 0.05 * h * L, 0.05 * h * L, -0.02 * h * L), b + V(sg * 0.09 * h * L, 0.08 * h * L, -0.08 * h * L * curl),
                   b + V(sg * 0.1 * h * L, 0.13 * h * L, -0.14 * h * L * curl)]
            sc.chain(pts, [0.022 * h, 0.017 * h, 0.01 * h, 0.002 * h], mat_h, 'head', k=0.01 * h)
    if ears:
        for sg in (1, -1):
            sc.capsule(c + V(sg * 0.055 * h, 0.07 * h, 0), c + V(sg * 0.11 * h * ears, 0.12 * h, -0.03 * h), 0.014 * h, 0.002 * h, mat, 'head', k=0.01 * h)


def membrane_wing(sc, root, span, sg, bone, tip_bone, mat_bone='bone', mat_mem='flesh', h=1.0, fingers=3):
    """Fledermaus-/Drachenflügel: Knochenfinger + dünne Haut dazwischen (aus Kapseln)."""
    root = V(*root)
    elbow = root + V(sg * span * 0.4, 0.06 * h, -0.02 * h)
    sc.capsule(root, elbow, 0.03 * h, 0.022 * h, mat_bone, bone, k=0.015 * h)
    tips = []
    for i in range(fingers):
        a = math.radians(10 - i * 28)
        tip = elbow + V(sg * span * 0.6 * math.cos(a), span * 0.25 * math.sin(a), -0.05 * h - i * 0.12 * span)
        sc.capsule(elbow, tip, 0.016 * h, 0.004 * h, mat_bone, tip_bone, k=0.01 * h)
        tips.append(tip)
    return root, elbow, tips


def membrane_mesh(root, elbow, tips, back, thick=0.004):
    """Flügelhaut als dünnes, doppelseitiges Dreiecksnetz zwischen Arm und Fingern (Bogen zwischen den Spitzen)."""
    from .geo import Mesh, merge, weld_normals
    root, elbow, back = V(*root), V(*elbow), V(*back)
    outline = [root, elbow]
    for i, t in enumerate(tips):
        outline.append(V(*t))
        nxt = V(*tips[i + 1]) if i + 1 < len(tips) else back
        outline.append((V(*t) + nxt) * 0.5 * 0.75 + elbow * 0.25)
    outline.append(back)
    P = [elbow * 0.5 + (root + back) * 0.25] + outline
    I = [(0, i, i + 1) for i in range(1, len(P) - 1)] + [(0, len(P) - 1, 1)]
    P = np.array(P)
    m = Mesh(P, np.tile([0, 1, 0], (len(P), 1)), P[:, [0, 2]], I)
    m = weld_normals(m)
    top = m.copy(); top.P = top.P + top.N * thick
    bot = m.copy(); bot.I = bot.I[:, ::-1].copy(); bot.N = -bot.N; bot.P = bot.P - m.N * thick
    return merge([top, bot])
