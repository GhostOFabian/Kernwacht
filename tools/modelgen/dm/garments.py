"""Kleidung und Rüstung als passgenaue Schichten (sdf.Layer) über einem Körper-Sculpt."""
from __future__ import annotations

import numpy as np

from .anatomy2 import V
from .sdf import Layer, R


def _arms_cut(H, r=0.05):
    h = H.h
    return [R('capsule', a=H.p('upperarm_' + s) + V(0, -0.01 * h, 0), b=H.p('hand_' + s) + V(0, -0.1 * h, 0), r1=r * h, r2=r * h) for s in 'LR']


def _neck_cut(H, r=0.045):
    return R('capsule', a=H.p('neck') + V(0, 0.0, 0), b=H.p('head') + V(0, 0.2 * H.h, 0), r1=r * H.h, r2=r * H.h)


def cuirass(sc, H, mat='steel', off=0.014, lames=3, ridge=True, low=0.02):
    h = H.h
    top, bot = H.p('chest') + V(0, 0.1 * h, 0), H.p('hips') + V(0, low * h, 0)
    reg = [R('box', c=(top + bot) / 2, h=(0.3 * h, (top[1] - bot[1]) / 2, 0.25 * h), r=0.0)]
    cuts = _arms_cut(H, 0.052) + [_neck_cut(H, 0.05)]
    grooves = []
    sp = H.p('spine')
    for i in range(lames):
        y = sp[1] - 0.02 * h - i * 0.035 * h
        grooves.append(R('box', c=(0, y, 0), h=(0.3 * h, 0.0025 * h, 0.3 * h)))
    extra = []
    if ridge:
        extra.append(R('capsule', a=H.p('spine') + V(0, 0.0, 0.0), b=H.p('chest') + V(0, 0.05 * h, 0.0), r1=0.006 * h, r2=0.006 * h))
    L = Layer(sc, off * h, mat, reg, cuts, grooves, 0.004 * h, 0.003 * h)
    if ridge:  # Mittelgrat vorne: als Schnitt-Rille betonen
        L.grooves.append(R('box', c=H.p('chest') * 0 + V(0, H.p('spine')[1] + 0.06 * h, 0.2 * h), h=(0.002 * h, 0.12 * h, 0.12 * h)))
    return L


def tunic(sc, H, mat='cloth_red', off=0.01, hem=0.18, sleeves=False, collar=0.0):
    h = H.h
    top, bot = H.p('chest') + V(0, 0.1 * h, 0), H.p('hips') + V(0, -hem * h, 0)
    reg = [R('box', c=(top + bot) / 2, h=(0.3 * h, (top[1] - bot[1]) / 2, 0.25 * h))]
    cuts = [] if sleeves else _arms_cut(H, 0.04)
    cuts.append(_neck_cut(H, 0.042 - collar))
    return Layer(sc, off * h, mat, reg, cuts, [], 0.0, 0.002 * h, detail=0.0008 * h, dfreq=30)


def sleeves(sc, H, mat, off=0.006, to='forearm'):
    h = H.h
    regs = []
    for s in 'LR':
        end = H.p('forearm_' + s) if to == 'elbow' else H.p('hand_' + s) + V(0, 0.03 * h, 0)
        regs.append(R('capsule', a=H.p('upperarm_' + s) + V(0, 0.02 * h, 0), b=end, r1=0.07 * h, r2=0.06 * h))
    return Layer(sc, off * h, mat, regs, [], [], 0.0, 0.002 * h)


def pants(sc, H, mat='leather_black', off=0.006, to=0.35):
    h = H.h
    regs = [R('box', c=H.p('hips') + V(0, -0.02 * h, 0), h=(0.2 * h, 0.07 * h, 0.15 * h))]
    for s in 'LR':
        end = H.p('shin_' + s) * (1 - to) + H.p('foot_' + s) * to
        regs.append(R('capsule', a=H.p('thigh_' + s), b=end, r1=0.09 * h, r2=0.07 * h))
    return Layer(sc, off * h, mat, regs, [], [], 0.0, 0.002 * h)


def greaves(sc, H, mat='iron', off=0.012):
    h = H.h
    regs = [R('capsule', a=H.p('shin_' + s) + V(0, 0.03 * h, 0), b=H.p('foot_' + s) + V(0, 0.03 * h, 0), r1=0.08 * h, r2=0.07 * h) for s in 'LR']
    grooves = [R('box', c=H.p('shin_' + s) + V(0, -0.06 * h, 0), h=(0.1 * h, 0.002 * h, 0.1 * h)) for s in 'LR']
    return Layer(sc, off * h, mat, regs, [], grooves, 0.003 * h, 0.003 * h)


def boots(sc, H, mat='leather_black', off=0.012, height=0.1):
    h = H.h
    regs = [R('box', c=H.p('foot_' + s) + V(0, 0.0, 0.03 * h), h=(0.07 * h, (0.035 + height) * h, 0.12 * h)) for s in 'LR']
    return Layer(sc, off * h, mat, regs, [], [], 0.0, 0.002 * h)


def bracers(sc, H, mat='leather', off=0.009, hands=False):
    h = H.h
    regs = []
    for s in 'LR':
        a, b = H.p('forearm_' + s), H.p('hand_' + s)
        regs.append(R('capsule', a=a * 0.8 + b * 0.2, b=(b + V(0, -0.06 * h, 0)) if hands else b + V(0, 0.01 * h, 0), r1=0.06 * h, r2=0.05 * h))
    return Layer(sc, off * h, mat, regs, [], [], 0.0, 0.002 * h)


def gloves(sc, H, mat='leather_black', off=0.004):
    h = H.h
    regs = [R('capsule', a=H.p('hand_' + s) + V(0, 0.02 * h, 0), b=H.p('hand_' + s) + V(0, -0.11 * h, 0), r1=0.05 * h, r2=0.05 * h) for s in 'LR']
    return Layer(sc, off * h, mat, regs, [], [], 0.0, 0.0015 * h)


def pauldrons(sc, H, mat='iron', off=0.028, lames=2):
    h = H.h
    out = []
    for s, sg in (('L', 1), ('R', -1)):
        c = H.p('upperarm_' + s) + V(sg * 0.012 * h, 0.012 * h, 0)
        reg = [R('ellipsoid', c=c, r=(0.075 * h, 0.07 * h, 0.075 * h))]
        cut = [R('box', c=c + V(-sg * 0.11 * h, 0, 0), h=(0.06 * h, 0.2 * h, 0.2 * h))]
        grooves = [R('box', c=c + V(0, -0.012 * h - i * 0.025 * h, 0), h=(0.2 * h, 0.002 * h, 0.2 * h)) for i in range(lames)]
        out.append(Layer(sc, off * h, mat, reg, cut, grooves, 0.004 * h, 0.004 * h))
    return out


def helm(sc, H, mat='steel', off=0.014, face='open', crest=False):
    h = H.h
    hd = H.p('head')
    reg = [R('ellipsoid', c=hd + V(0, 0.09 * h, -0.01 * h), r=(0.1 * h, 0.08 * h if face == 'open' else 0.11 * h, 0.1 * h))]
    cuts, grooves = [], []
    if face == 'open':
        cuts.append(R('ellipsoid', c=hd + V(0, 0.055 * h, 0.085 * h), r=(0.035 * h, 0.04 * h, 0.04 * h)))
    else:
        grooves.append(R('box', c=hd + V(0, 0.072 * h, 0.08 * h), h=(0.05 * h, 0.004 * h, 0.05 * h)))
        grooves.append(R('box', c=hd + V(0, 0.04 * h, 0.08 * h), h=(0.003 * h, 0.025 * h, 0.05 * h)))
    return Layer(sc, off * h, mat, reg, cuts, grooves, 0.008 * h, 0.003 * h)


def hood(sc, H, mat='cloth_dark', off=0.02, shoulders=True):
    h = H.h
    hd = H.p('head')
    regs = [R('ellipsoid', c=hd + V(0, 0.07 * h, -0.02 * h), r=(0.12 * h, 0.13 * h, 0.13 * h))]
    if shoulders:
        regs.append(R('ellipsoid', c=H.p('chest') + V(0, 0.06 * h, -0.01 * h), r=(0.2 * h, 0.07 * h, 0.14 * h)))
    cuts = [R('ellipsoid', c=hd + V(0, 0.06 * h, 0.1 * h), r=(0.05 * h, 0.065 * h, 0.06 * h))]
    L = Layer(sc, off * h, mat, regs, cuts, [], 0.0, 0.006 * h, detail=0.001 * h, dfreq=25)
    return L


def wrap(sc, H, mat, off, regions, cuts=(), grooves=(), gd=0.003, edge=0.003, detail=0.0):
    h = H.h
    return Layer(sc, off * h, mat, regions, cuts, grooves, gd * h, edge * h, detail=detail * h, dfreq=25)
