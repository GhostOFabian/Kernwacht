"""Anatomie 2: definierte Körper mit klar getrennten Muskeln und Knochenpunkten (kleine Verschmelzung),
ausgearbeitete Köpfe – dazu harte Rüstungsteile und Stoffbahnen als echte Meshes mit Kanten.

Leitidee gegen „Knetklumpen“: jede Form bleibt als eigene Form lesbar (Verschmelzung k klein),
Silhouetten durch harte Teile (Rüstung, Stoff, Waffen), klare Materialtrennung."""
from __future__ import annotations

import math

import numpy as np

from .geo import TAU, Mesh, cylinder, curve, extrude, lathe, merge, rbox, sphere, spike, torus, tube, weld_normals
from .sdf import Sculpt, rot


def V(*a):
    return np.array(a, dtype=float)


def align(mesh: Mesh, p0, p1, length=None):
    """Mesh, das entlang +Y von 0 bis L gebaut ist, auf die Strecke p0→p1 ausrichten."""
    p0, p1 = np.asarray(p0, float), np.asarray(p1, float)
    d = p1 - p0
    L = float(np.linalg.norm(d)) or 1.0
    dn = d / L
    y = np.array([0, 1.0, 0])
    ax = np.cross(y, dn)
    s = float(np.linalg.norm(ax))
    M = np.eye(4)
    if s > 1e-6:
        ax /= s
        a = math.atan2(s, float(y @ dn))
        K = np.array([[0, -ax[2], ax[1]], [ax[2], 0, -ax[0]], [-ax[1], ax[0], 0]])
        M[:3, :3] = np.eye(3) + math.sin(a) * K + (1 - math.cos(a)) * K @ K
    elif dn[1] < 0:
        M[:3, :3] = np.diag([1, -1, -1])
    mesh.apply(M)
    return mesh.t(*p0)


# ====================================================================== Körper

def body2(sc: Sculpt, H, skin='skin', build='athletic', head='human', hands='hand', feet='bare', female=False,
          head_scale=1.0, neck_len=1.0, claws=False, gaunt=0.0, horns=None, ears=0.0, tail=None, eye='glow_white',
          teeth=False, k=1.0, nails='bone'):
    """build: lean | athletic | heavy | hulking | gaunt.  Liefert Punkte P (Knochen) zurück."""
    P = {kk: V(*v) for kk, v in H.P.items()}
    h = H.h
    B = {'lean': (0.85, 0.5, 0.0), 'athletic': (1.0, 0.85, 0.0), 'heavy': (1.18, 1.0, 0.5), 'hulking': (1.4, 1.25, 0.7), 'gaunt': (0.82, 0.35, 0.0)}[build]
    w, mu, fat = B
    g = max(gaunt, 0.7 if build == 'gaunt' else 0.0)
    K = lambda x: x * h * k  # Verschmelzung
    ch, sp, hp, nk, hd = P['chest'], P['spine'], P['hips'], P['neck'], P['head']
    # --- Rumpf: Brustkorb, Brustmuskeln, Bauch, Becken
    sc.ellipsoid(ch + V(0, 0.01 * h, -0.005 * h), (0.135 * h * w, 0.12 * h, 0.088 * h * w), skin, 'chest', k=K(0.02))
    if female:
        for sg in (1, -1):
            sc.ellipsoid(ch + V(sg * 0.05 * h, -0.02 * h, 0.06 * h), (0.045 * h, 0.042 * h, 0.04 * h), skin, 'chest', k=K(0.02))
    elif mu > 0.3:
        for sg in (1, -1):
            sc.ellipsoid(ch + V(sg * 0.055 * h * w, 0.0, 0.06 * h * w), (0.06 * h * w, 0.05 * h, 0.032 * h * w), skin, 'chest', k=K(0.01), R=rot(0, 0, sg * -12))
    sc.capsule(ch + V(0, -0.05 * h, 0.0), hp + V(0, 0.04 * h, 0), 0.09 * h * w * (1 - 0.2 * g) + fat * 0.03 * h, 0.095 * h * w + fat * 0.04 * h, skin, 'spine', k=K(0.02))
    if fat > 0:
        sc.ellipsoid(sp + V(0, -0.03 * h, 0.04 * h), (0.1 * h * w, 0.09 * h, 0.07 * h * fat + 0.03 * h), skin, 'spine', k=K(0.03))
    if mu > 0.7 and fat < 0.6:  # Bauchmuskeln
        for i in range(3):
            for sg in (1, -1):
                sc.ellipsoid(sp + V(sg * 0.022 * h, 0.04 * h - i * 0.035 * h, 0.065 * h * w), (0.02 * h, 0.016 * h, 0.012 * h), skin, 'spine', k=K(0.006))
    sc.ellipsoid(hp + V(0, 0.0, -0.005 * h), (0.115 * h * w + (0.015 * h if female else 0), 0.075 * h, 0.08 * h * w), skin, 'hips', k=K(0.02))
    for sg in (1, -1):  # Gesäß
        sc.ellipsoid(hp + V(sg * 0.045 * h, -0.03 * h, -0.045 * h), (0.055 * h * w, 0.06 * h, 0.045 * h), skin, 'hips', k=K(0.015))
    if g > 0.3:  # Rippen und Wirbel
        for i in range(5):
            y = ch[1] + 0.03 * h - i * 0.028 * h
            for sg in (1, -1):
                sc.capsule(V(sg * 0.02 * h, y, 0.08 * h * w), V(sg * 0.12 * h * w, y - 0.015 * h, 0.01 * h), 0.008 * h, 0.006 * h, skin, 'chest', k=K(0.006))
        for i in range(7):
            y = nk[1] - 0.02 * h - i * 0.045 * h
            sc.ellipsoid(V(0, y, -0.085 * h * w - 0.005 * h), (0.012 * h, 0.012 * h, 0.012 * h), skin, 'chest' if i < 3 else 'spine', k=K(0.006))
    # --- Nacken, Trapez, Schultern
    sc.capsule(nk + V(0, -0.03 * h, -0.005 * h), hd + V(0, 0.02 * h, -0.005 * h), 0.034 * h * (w if build in ('heavy', 'hulking') else 1), 0.03 * h, skin, 'neck', k=K(0.012))
    for sg, sd in ((1, 'L'), (-1, 'R')):
        ua = P['upperarm_' + sd]
        sc.capsule(nk + V(sg * 0.015 * h, -0.01 * h, -0.02 * h), ua + V(-sg * 0.02 * h, 0.025 * h, -0.01 * h), 0.03 * h * (0.8 + 0.4 * mu), 0.026 * h, skin, 'chest', k=K(0.015))
        sc.ellipsoid(ua + V(sg * 0.012 * h, -0.012 * h, 0.0), (0.045 * h * w, 0.055 * h, 0.048 * h * w), skin, 'upperarm_' + sd, k=K(0.012))  # Deltamuskel
    # --- Arme
    for sg, sd in ((1, 'L'), (-1, 'R')):
        ua, fa, ha = P['upperarm_' + sd], P['forearm_' + sd], P['hand_' + sd]
        r = 0.03 * h * (0.85 + 0.25 * w) * (1 - 0.3 * g)
        sc.capsule(ua, fa, r, r * 0.85, skin, 'upperarm_' + sd, k=K(0.008))
        if mu > 0.2:
            sc.ellipsoid(ua * 0.45 + fa * 0.55 + V(0, 0, 0.016 * h), (r * 0.9, 0.055 * h, r * 0.85), skin, 'upperarm_' + sd, k=K(0.008))  # Bizeps
            sc.ellipsoid(ua * 0.5 + fa * 0.5 + V(0, 0, -0.014 * h), (r * 0.85, 0.06 * h, r * 0.8), skin, 'upperarm_' + sd, k=K(0.008))  # Trizeps
        sc.ellipsoid(fa, r * 0.75, skin, 'forearm_' + sd, k=K(0.006))  # Ellbogen
        sc.capsule(fa, ha + V(0, 0.01 * h, 0), r * 0.95, r * 0.62, skin, 'forearm_' + sd, k=K(0.008))
        if mu > 0.2:
            sc.ellipsoid(fa * 0.72 + ha * 0.28 + V(sg * 0.004 * h, 0, 0.006 * h), (r * 0.95, 0.05 * h, r * 0.85), skin, 'forearm_' + sd, k=K(0.008))
        hand2(sc, ha, sg, h * (1.08 if build == 'hulking' else 1), skin, 'hand_' + sd, claw=(hands == 'claw') or claws, nails=nails)
    # --- Beine
    for sg, sd in ((1, 'L'), (-1, 'R')):
        th, sh, ft = P['thigh_' + sd], P['shin_' + sd], P['foot_' + sd]
        r = 0.05 * h * (0.8 + 0.3 * w) * (1 - 0.35 * g)
        sc.capsule(th + V(0, 0.01 * h, 0), sh + V(0, 0.01 * h, 0), r, r * 0.66, skin, 'thigh_' + sd, k=K(0.012))
        if mu > 0.2:
            sc.ellipsoid(th * 0.45 + sh * 0.55 + V(-sg * 0.004 * h, 0.02 * h, 0.02 * h), (r * 0.72, 0.09 * h, r * 0.7), skin, 'thigh_' + sd, k=K(0.01))  # Quadrizeps
        sc.ellipsoid(sh + V(0, 0.005 * h, 0.022 * h), (0.022 * h, 0.022 * h, 0.016 * h), skin, 'shin_' + sd, k=K(0.006))  # Knie
        rc = r * 0.62
        sc.capsule(sh, ft + V(0, 0.025 * h, 0), rc, rc * 0.62, skin, 'shin_' + sd, k=K(0.008))
        sc.ellipsoid(sh * 0.7 + ft * 0.3 + V(0, 0.01 * h, -0.02 * h), (rc * 1.0, 0.065 * h, rc * 0.95), skin, 'shin_' + sd, k=K(0.008))  # Wade
        if feet == 'claw':
            for kk in (-1, 0, 1):
                base = ft + V(kk * 0.02 * h, -0.025 * h, 0.03 * h)
                tip = ft + V(kk * 0.03 * h, -0.045 * h, 0.12 * h)
                sc.capsule(base, tip, 0.016 * h, 0.009 * h, skin, 'foot_' + sd, k=K(0.006))
                sc.capsule(tip, tip + V(0, -0.006 * h, 0.035 * h), 0.009 * h, 0.001 * h, nails, 'foot_' + sd, k=K(0.002))
            sc.capsule(ft + V(0, -0.02 * h, 0.0), ft + V(0, -0.04 * h, -0.055 * h), 0.016 * h, 0.006 * h, skin, 'foot_' + sd, k=K(0.005))
        elif feet == 'hoof':
            sc.capsule(ft + V(0, 0.0, 0.01 * h), ft + V(0, -0.04 * h, 0.03 * h), 0.04 * h, 0.048 * h, 'obsidian', 'foot_' + sd, k=K(0.004))
        else:
            sc.ellipsoid(ft + V(0, -0.028 * h, 0.04 * h), (0.034 * h, 0.022 * h, 0.075 * h), skin, 'foot_' + sd, k=K(0.008))
    head2(sc, hd, h * head_scale, skin, kind=head, female=female, eye=eye, teeth=teeth, horns=horns, ears=ears, k=k)
    if tail:
        mat, L = tail
        pts = [hp + V(0, 0.0, -0.07 * h), hp + V(0, -0.08 * h, -0.22 * h * L), hp + V(0.03 * h, -0.2 * h, -0.38 * h * L), hp + V(0.06 * h, -0.32 * h, -0.5 * h * L)]
        sc.chain(pts, [0.035 * h, 0.025 * h, 0.014 * h, 0.003 * h], mat, 'hips', k=K(0.01))
    return P


def hand2(sc, ha, sg, h, mat, bone, claw=False, nails='bone'):
    """Hand mit Daumenballen und vier Fingern in zwei Gliedern."""
    palm = ha + V(-sg * 0.002 * h, -0.032 * h, 0.006 * h)
    sc.box(palm, (0.013 * h, 0.03 * h, 0.028 * h), mat, bone, k=0.008 * h, r=0.009 * h)
    sc.ellipsoid(palm + V(0, 0.005 * h, 0.022 * h), (0.012 * h, 0.018 * h, 0.012 * h), mat, bone, k=0.006 * h)
    for i in range(4):
        z = palm[2] + (i - 1.5) * 0.0135 * h
        b0 = V(palm[0], palm[1] - 0.03 * h, z)
        L = (0.026 + 0.004 * (1.5 - abs(i - 1.5))) * h * (1.3 if claw else 1)
        b1 = b0 + V(-sg * 0.002 * h, -L, 0.004 * h)
        b2 = b1 + V(-sg * 0.004 * h, -L * 0.8, 0.008 * h)
        sc.capsule(b0, b1, 0.0062 * h, 0.0055 * h, mat, bone, k=0.003 * h)
        sc.capsule(b1, b2, 0.0055 * h, 0.0045 * h, mat, bone, k=0.003 * h)
        if claw:
            sc.capsule(b2, b2 + V(0, -0.02 * h, 0.016 * h), 0.0045 * h, 0.0008 * h, nails, bone, k=0.002 * h)
    t0 = V(palm[0], palm[1] + 0.004 * h, palm[2] + 0.025 * h)
    t1 = t0 + V(sg * 0.006 * h, -0.025 * h, 0.016 * h)
    sc.capsule(t0, t1, 0.0085 * h, 0.0065 * h, mat, bone, k=0.004 * h)
    sc.capsule(t1, t1 + V(0, -0.018 * h, 0.006 * h), 0.0062 * h, 0.005 * h, mat, bone, k=0.003 * h)


def head2(sc, c, h, mat, kind='human', female=False, eye='glow_white', teeth=False, horns=None, ears=0.0, k=1.0):
    """Ausgearbeiteter Kopf: Schädel, Stirn, Wangenknochen, Kiefer, Nase, Augenhöhlen mit Augen, Lippen, Ohren.
    kind: human | ghoul | orc | skull | beast | demon"""
    c = V(*c)
    K = lambda x: x * h * k
    s = h
    if kind == 'skull':
        sc.ellipsoid(c + V(0, 0.072 * s, -0.008 * s), (0.056 * s, 0.062 * s, 0.068 * s), 'bone', 'head', k=K(0.008))
        sc.box(c + V(0, 0.03 * s, 0.03 * s), (0.034 * s, 0.018 * s, 0.03 * s), 'bone', 'head', k=K(0.01), r=0.01 * s)
        for sg in (1, -1):
            sc.ellipsoid(c + V(sg * 0.042 * s, 0.05 * s, 0.035 * s), (0.014 * s, 0.012 * s, 0.022 * s), 'bone', 'head', k=K(0.006))  # Wangenknochen
            sc.ellipsoid(c + V(sg * 0.023 * s, 0.067 * s, 0.062 * s), 0.016 * s, 'bone', 'head', sub=True, k=K(0.004))
            sc.ellipsoid(c + V(sg * 0.023 * s, 0.066 * s, 0.05 * s), 0.0065 * s, eye, 'head', k=K(0.001))
        sc.ellipsoid(c + V(0, 0.044 * s, 0.068 * s), (0.008 * s, 0.012 * s, 0.012 * s), 'bone', 'head', sub=True, k=K(0.003))
        for i in range(6):
            x = (i - 2.5) * 0.009 * s
            sc.box(c + V(x, 0.017 * s, 0.058 * s), (0.0035 * s, 0.006 * s, 0.004 * s), 'bone', 'head', k=K(0.001), r=0.001 * s)
        return
    jaw = {'human': 1.0, 'ghoul': 0.85, 'orc': 1.25, 'beast': 1.1, 'demon': 1.05}[kind]
    fem = 0.9 if female else 1.0
    # Schädel und Hinterkopf
    sc.ellipsoid(c + V(0, 0.075 * s, -0.012 * s), (0.056 * s * fem, 0.062 * s, 0.068 * s), mat, 'head', k=K(0.01))
    # Stirn / Brauenwulst
    brow = 0.012 * s * (1.6 if kind in ('orc', 'ghoul', 'demon') else (0.7 if female else 1.0))
    sc.capsule(c + V(-0.032 * s, 0.087 * s, 0.054 * s), c + V(0.032 * s, 0.087 * s, 0.054 * s), brow, brow, mat, 'head', k=K(0.008))
    # Wangenknochen
    for sg in (1, -1):
        sc.ellipsoid(c + V(sg * 0.038 * s, 0.06 * s, 0.04 * s), (0.016 * s, 0.012 * s, 0.02 * s), mat, 'head', k=K(0.008))
    # Kiefer und Kinn
    sc.box(c + V(0, 0.032 * s, 0.03 * s), (0.036 * s * jaw * fem, 0.024 * s, 0.036 * s), mat, 'head', k=K(0.015), r=0.016 * s, R=rot(0, -10, 0))
    sc.ellipsoid(c + V(0, 0.018 * s, 0.06 * s), (0.016 * s * jaw, 0.014 * s, 0.012 * s), mat, 'head', k=K(0.008))
    # Nase
    if kind in ('human', 'orc', 'demon'):
        sc.capsule(c + V(0, 0.078 * s, 0.068 * s), c + V(0, 0.054 * s, 0.083 * s), 0.0065 * s, 0.009 * s * (1.4 if kind == 'orc' else 1), mat, 'head', k=K(0.005))
        for sg in (1, -1):
            sc.ellipsoid(c + V(sg * 0.008 * s, 0.052 * s, 0.077 * s), 0.006 * s, mat, 'head', k=K(0.004))
    elif kind == 'ghoul':
        for sg in (1, -1):
            sc.ellipsoid(c + V(sg * 0.006 * s, 0.056 * s, 0.075 * s), (0.004 * s, 0.008 * s, 0.006 * s), mat, 'head', sub=True, k=K(0.002))
    # Lippen / Maul
    if kind in ('human', 'demon'):
        sc.capsule(c + V(-0.014 * s, 0.038 * s, 0.068 * s), c + V(0.014 * s, 0.038 * s, 0.068 * s), 0.005 * s, 0.005 * s, mat, 'head', k=K(0.004))
    else:
        sc.box(c + V(0, 0.034 * s, 0.07 * s), (0.026 * s * jaw, 0.006 * s, 0.012 * s), mat, 'head', sub=True, k=K(0.003))
        if teeth or kind in ('ghoul', 'orc', 'beast'):
            for i in range(6):
                x = (i - 2.5) * 0.009 * s * jaw
                ln = 0.016 * s if (kind == 'orc' and i in (0, 5)) else 0.008 * s
                up = kind == 'orc' and i in (0, 5)
                a = c + V(x, 0.03 * s if up else 0.04 * s, 0.07 * s)
                sc.capsule(a, a + V(0, ln if up else -ln, 0.002 * s), 0.003 * s, 0.0008 * s, 'bone', 'head', k=K(0.001))
    # Augenhöhlen + Augen
    for sg in (1, -1):
        sc.ellipsoid(c + V(sg * 0.022 * s, 0.071 * s, 0.062 * s), (0.013 * s, 0.009 * s, 0.01 * s), mat, 'head', sub=True, k=K(0.004))
        sc.ellipsoid(c + V(sg * 0.022 * s, 0.071 * s, 0.056 * s), 0.0085 * s, eye, 'head', k=K(0.0008))
    # Ohren
    for sg in (1, -1):
        if ears > 0:
            sc.capsule(c + V(sg * 0.054 * s, 0.07 * s, -0.005 * s), c + V(sg * (0.06 + 0.05 * ears) * s, (0.09 + 0.03 * ears) * s, -0.03 * s), 0.011 * s, 0.002 * s, mat, 'head', k=K(0.006))
        else:
            sc.ellipsoid(c + V(sg * 0.056 * s, 0.067 * s, -0.004 * s), (0.006 * s, 0.016 * s, 0.011 * s), mat, 'head', k=K(0.005))
    if horns:
        mat_h, L, curl = horns
        for sg in (1, -1):
            b = c + V(sg * 0.035 * s, 0.112 * s, 0.015 * s)
            pts = [b, b + V(sg * 0.04 * s * L, 0.045 * s * L, -0.025 * s * L), b + V(sg * 0.07 * s * L, 0.07 * s * L, -0.08 * s * L * curl),
                   b + V(sg * 0.075 * s * L, 0.12 * s * L, -0.13 * s * L * curl)]
            sc.chain(pts, [0.017 * s, 0.013 * s, 0.008 * s, 0.0015 * s], mat_h, 'head', k=K(0.004))
            for t in (0.3, 0.55, 0.75):  # Rillen
                p = pts[1] * (1 - t) + pts[2] * t
                sc.torus(p, 0.012 * s * (1 - t * 0.4), 0.0025 * s, mat_h, 'head', k=K(0.002))


# ====================================================================== Harte Teile (Meshes)

def shell(rx, ry, rz, y0=-1.0, y1=1.0, seg=20, rings=12, thick=0.006):
    """Ausschnitt einer Ellipsoidschale zwischen y0..y1 (normiert −1..1), außen und innen."""
    out = []
    for sgn, rr in ((1, 1.0), (-1, 1.0 - thick / max(rx, rz))):
        P, N, UV, I = [], [], [], []
        a0, a1 = math.asin(max(-1, min(1, y0))), math.asin(max(-1, min(1, y1)))
        for j in range(rings + 1):
            th = a0 + (a1 - a0) * j / rings
            for i in range(seg + 1):
                ph = TAU * i / seg
                x, y, z = math.cos(th) * math.cos(ph), math.sin(th), math.cos(th) * math.sin(ph)
                P.append((x * rx * rr, y * ry * rr, z * rz * rr))
                n = np.array([x / rx, y / ry, z / rz]); n /= np.linalg.norm(n)
                N.append(n * sgn)
                UV.append((ph * rx, th * ry))
        for j in range(rings):
            for i in range(seg):
                a = j * (seg + 1) + i
                q = (a, a + 1, a + seg + 2, a + seg + 1)
                if sgn > 0:
                    I += [(q[0], q[3], q[1]), (q[1], q[3], q[2])]
                else:
                    I += [(q[0], q[1], q[3]), (q[1], q[2], q[3])]
        from .geo import fix_winding
        out.append(fix_winding(Mesh(P, N, UV, I)))
    return merge(out)


def cuirass(H, front_ridge=True, scale=1.0, y_lo=None, y_hi=None):
    """Brustpanzer um den Oberkörper (offene Schale vom Bauch bis zur Brust)."""
    h = H.h * scale
    ch = H.p('chest')
    m = shell(0.155 * h, 0.15 * h, 0.115 * h, -0.75, 0.62, 22, 12, 0.008 * h).t(*(ch + V(0, -0.03 * H.h, 0.005 * H.h)))
    parts = [m]
    if front_ridge:
        parts.append(align(cylinder(0.006 * h, 0.17 * h, 6), ch + V(0, -0.12 * H.h, 0.115 * h), ch + V(0, 0.06 * H.h, 0.11 * h)))
    return merge(parts)


def fauld(H, n=7, mat_len=0.12, spread=0.13):
    """Gürtelplatten (Schurz) um die Hüfte – je Seite an den Oberschenkel gebunden."""
    h = H.h
    hp = H.p('hips')
    out = {'L': [], 'R': [], 'hips': []}
    for i in range(n):
        a = math.pi * 0.15 + (math.pi * 0.7) * i / (n - 1)  # vorne Halbkreis
        for front in (True, False):
            ang = a if front else a + math.pi
            x, z = math.cos(ang) * spread * h, math.sin(ang) * spread * h * 0.8
            plate = rbox(0.05 * h, mat_len * h, 0.008 * h, 0.003 * h).t(0, -mat_len * h / 2, 0)
            plate.rx(0.25 if front else -0.25).ry(-ang + math.pi / 2)
            plate.t(hp[0] + x, hp[1] - 0.02 * h, hp[2] + z)
            key = 'L' if x > 0.03 * h else 'R' if x < -0.03 * h else 'hips'
            out[key].append(plate)
    return {k: merge(v) for k, v in out.items() if v}


def pauldron2(r, layers=3, spikes=0, trim=True):
    """Geschichtete Schulterplatte, gebaut um +Y (Spitze nach oben), später an die Schulter gesetzt."""
    out, rims = [], []
    for i in range(layers):
        rr = r * (1 - i * 0.13)
        s = shell(rr, rr * 0.7, rr * 0.95, -0.15, 1.0, 16, 7, 0.006).t(0, -i * r * 0.32, 0)
        out.append(s)
        if trim:
            rims.append(torus(rr * 0.99, r * 0.05, 18, 4).s(1, 1, 0.95).t(0, -i * r * 0.32 - rr * 0.7 * 0.15, 0))
    for i in range(spikes):
        out.append(spike(r * 0.14, r * 0.8, 6).rz(0.5 - i * 0.5).t(0, r * 0.55, (i - (spikes - 1) / 2) * r * 0.35))
    return merge(out), merge(rims) if rims else None


def bracer(H, side, mat_r=1.0, upper=False):
    h = H.h
    a, b = (H.p('upperarm_' + side), H.p('forearm_' + side)) if upper else (H.p('forearm_' + side), H.p('hand_' + side))
    L = float(np.linalg.norm(b - a))
    r0, r1 = 0.036 * h * mat_r, 0.03 * h * mat_r
    t = lathe([(r0 * 0.9, 0.0), (r0, 0.02 * L), (r1, 0.85 * L), (r1 * 1.1, 0.9 * L), (r1 * 0.95, L)], 14, cap_bottom=False, cap_top=False)
    return align(t, a + (b - a) * 0.12, b)


def greave(H, side, mat_r=1.0):
    h = H.h
    a, b = H.p('shin_' + side), H.p('foot_' + side)
    L = float(np.linalg.norm(b - a))
    r0, r1 = 0.046 * h * mat_r, 0.036 * h * mat_r
    t = lathe([(r1 * 1.05, 0.0), (r1, 0.06 * L), (r0, 0.75 * L), (r0 * 1.1, 0.92 * L), (r0 * 0.9, L)], 14, cap_bottom=False, cap_top=False)
    knee = shell(0.03 * h, 0.032 * h, 0.022 * h, -0.4, 1.0, 12, 6, 0.004).t(0, 0, 0)
    g = align(t, b + V(0, 0.02 * h, 0), a + V(0, 0.01 * h, 0))
    return merge([g, knee.t(*(a + V(0, 0.01 * h, 0.03 * h)))])


def boot(H, side, mat_r=1.0, cuff=True):
    h = H.h
    ft, sh = H.p('foot_' + side), H.p('shin_' + side)
    sole = rbox(0.072 * h, 0.022 * h, 0.17 * h, 0.01 * h).t(*(ft + V(0, -0.04 * h, 0.04 * h)))
    shaft = align(lathe([(0.04 * h, 0), (0.042 * h, 0.1 * h), (0.048 * h, 0.13 * h), (0.045 * h, 0.14 * h)], 12, cap_bottom=False, cap_top=False), ft + V(0, -0.02 * h, 0), sh)
    toe = sphere(0.036 * h, 0.028 * h, 0.07 * h, 12).t(*(ft + V(0, -0.022 * h, 0.06 * h)))
    return merge([sole, shaft, toe])


def helmet(H, kind='great', crest=None):
    """kind: great (geschlossen mit Sehschlitz), open (Nasal), hood (Stoffkapuze als Mesh)"""
    h = H.h
    hd = H.p('head')
    c = hd + V(0, 0.075 * h, -0.005 * h)
    out = []
    if kind == 'great':
        out.append(shell(0.07 * h, 0.08 * h, 0.08 * h, -0.85, 1.0, 20, 10, 0.006 * h).t(*c))
        out.append(rbox(0.11 * h, 0.012 * h, 0.03 * h, 0.004 * h).t(*(c + V(0, 0.0, 0.072 * h))))
    else:
        out.append(shell(0.066 * h, 0.072 * h, 0.075 * h, -0.1, 1.0, 20, 8, 0.006 * h).t(*c))
        out.append(rbox(0.012 * h, 0.06 * h, 0.012 * h, 0.004 * h).t(*(c + V(0, -0.02 * h, 0.075 * h))))
        for sg in (1, -1):
            out.append(rbox(0.012 * h, 0.06 * h, 0.05 * h, 0.006 * h).t(*(c + V(sg * 0.06 * h, -0.045 * h, 0.02 * h))))
    return merge(out)


def belt(H, r=None, buckle=True):
    h = H.h
    hp = H.p('hips')
    r = r or 0.118 * h
    b = torus(r, 0.012 * h, 28, 4).s(1, 1.6, 0.82).t(*(hp + V(0, 0.035 * h, 0)))
    parts = [b]
    if buckle:
        parts.append(rbox(0.04 * h, 0.032 * h, 0.01 * h, 0.004 * h).t(*(hp + V(0, 0.035 * h, r * 0.82 + 0.004 * h))))
    return merge(parts)


def cloth_panel(top_pts, length, folds=5, depth=0.012, nv=10, flare=1.25, back=False, jag=0.0):
    """Stoffbahn: hängt von einer oberen Kante (Liste von Punkten) nach unten, mit Falten und optional gezacktem Saum."""
    top = [np.asarray(p, float) for p in top_pts]
    nu = len(top)
    c = np.mean(top, axis=0)
    P, N, UV, I = [], [], [], []
    rng = np.random.default_rng(len(top) * 7 + int(length * 1000))
    cut = rng.random(nu) * jag
    for j in range(nv + 1):
        v = j / nv
        for i, p in enumerate(top):
            out = (p - c) * (1 + (flare - 1) * v)
            q = c + out
            q = q + V(0, -length * v * (1 - cut[i] * v), 0)
            nrm = np.array([out[0], 0, out[2]])
            ln = np.linalg.norm(nrm)
            nrm = nrm / ln if ln > 1e-6 else np.array([0, 0, -1.0 if back else 1.0])
            q = q + nrm * math.sin(i / max(1, nu - 1) * folds * math.pi) * depth * (0.3 + v)
            if back:
                q = q + V(0, 0, -0.04 * length * v * v)
            P.append(q); N.append(nrm); UV.append((i * 0.05, -length * v))
    for j in range(nv):
        for i in range(nu - 1):
            a = j * nu + i
            I += [(a, a + nu, a + 1), (a + 1, a + nu, a + nu + 1)]
    m = Mesh(P, N, UV, I)
    m = weld_normals(m)
    b = m.copy(); b.I = b.I[:, ::-1].copy(); b.N = -b.N
    m.P = m.P + m.N * 0.002; b.P = b.P - m.N * 0.002
    return merge([m, b])


def robe_skirt(H, length=0.9, r_top=None, flare=1.6, folds=14, mat_depth=0.012):
    """Gewandrock als Rotationskörper mit Längsfalten, von der Hüfte bis knapp über den Boden."""
    h = H.h
    hp = H.p('hips')
    r0 = r_top or 0.125 * h
    y0 = hp[1] + 0.03 * h
    y1 = max(0.02 * h, hp[1] - length * hp[1])
    prof = []
    for t in np.linspace(0, 1, 9):
        prof.append((r0 * (1 + (flare - 1) * t ** 1.3), y0 + (y1 - y0) * t))
    m = lathe(prof[::-1], 40, cap_bottom=False, cap_top=False, smooth_all=True)
    a = np.arctan2(m.P[:, 2], m.P[:, 0])
    t = np.clip((y0 - m.P[:, 1]) / max(y0 - y1, 1e-6), 0, 1)
    rr = 1 + np.sin(a * folds) * mat_depth * t / max(r0, 1e-6) * 3
    m.P[:, 0] *= rr * 1.0
    m.P[:, 2] *= rr * 0.85
    m = weld_normals(m)
    b = m.copy(); b.I = b.I[:, ::-1].copy(); b.N = -b.N
    b.P = b.P - m.N * 0.003
    return merge([m, b])


def hair_locks(H, n=9, length=0.14, spread=0.055, mat_r=1.0, top=True, back=True, seed=1):
    """Haarsträhnen als spitz zulaufende Röhren über den Hinterkopf."""
    h = H.h
    hd = H.p('head') + V(0, 0.075 * h, -0.01 * h)
    rng = np.random.default_rng(seed)
    out = []
    for i in range(n):
        a = -math.pi * 0.9 + math.pi * 1.8 * i / max(1, n - 1)
        root = hd + V(math.sin(a) * 0.055 * h, 0.05 * h, -math.cos(a) * 0.03 * h + 0.02 * h)
        mid = hd + V(math.sin(a) * 0.072 * h, -0.01 * h, -math.cos(a) * 0.06 * h - 0.01 * h)
        end = hd + V(math.sin(a) * (0.07 + rng.random() * 0.02) * h, -length * h, -math.cos(a) * 0.05 * h - 0.04 * h)
        out.append(tube(curve([root, mid, end], 10), [0.016 * h * mat_r, 0.013 * h * mat_r, 0.002 * h], 6))
    cap = shell(0.062 * h, 0.066 * h, 0.072 * h, 0.05, 1.0, 18, 7, 0.004 * h).t(*hd)
    return merge(out + [cap])


def beard_locks(H, n=7, length=0.08, mat_r=1.0, seed=2):
    h = H.h
    hd = H.p('head')
    rng = np.random.default_rng(seed)
    out = []
    for i in range(n):
        x = (i - (n - 1) / 2) * 0.012 * h
        root = hd + V(x, 0.045 * h - abs(x) * 0.4, 0.066 * h - abs(x) * 0.6)
        end = hd + V(x * 1.2, 0.045 * h - length * h * (0.8 + 0.4 * rng.random()), 0.08 * h)
        out.append(tube(curve([root, (root + end) / 2 + V(0, 0, 0.012 * h), end], 8), [0.012 * h * mat_r, 0.01 * h * mat_r, 0.002 * h], 6))
    for sg in (1, -1):  # Schnurrbart
        out.append(tube(curve([hd + V(0, 0.048 * h, 0.08 * h), hd + V(sg * 0.02 * h, 0.042 * h, 0.08 * h), hd + V(sg * 0.035 * h, 0.025 * h, 0.07 * h)], 8), [0.006 * h, 0.005 * h, 0.001 * h], 5))
    return merge(out)


def cape_back(H, length=0.7, width=0.3, jag=0.25, folds=6):
    h = H.h
    ch = H.p('chest')
    top = [ch + V(x * h, 0.085 * h, -0.095 * h - abs(x) * 0.15 * h) for x in np.linspace(-width / 2, width / 2, 13)]
    return cloth_panel(top, length * h, folds, 0.012 * h, 12, 1.35, back=True, jag=jag)


def tabard(H, length=0.35, width=0.13):
    h = H.h
    hp = H.p('hips')
    top = [hp + V(x * h, 0.03 * h, 0.1 * h) for x in np.linspace(-width / 2, width / 2, 7)]
    return cloth_panel(top, length * h, 2, 0.004 * h, 8, 1.1)
