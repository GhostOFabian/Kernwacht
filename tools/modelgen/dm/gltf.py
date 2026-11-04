"""GLB-Export (glTF 2.0): Meshes mit Tangenten, PBR-Materialien mit eingebetteten Texturen,
Knotenhierarchie, Skins (Skelett) und Animationen."""
from __future__ import annotations

import json
import struct

import numpy as np

from . import tex
from .geo import Mesh, merge
from .model import Model

F32, U8, U16, U32 = 5126, 5121, 5123, 5125
ARRAY, ELEMENT = 34962, 34963


def tangents(m: Mesh):
    P, N, UV, I = m.P, m.N, m.UV, m.I
    t = np.zeros_like(P)
    b = np.zeros_like(P)
    p0, p1, p2 = P[I[:, 0]], P[I[:, 1]], P[I[:, 2]]
    u0, u1, u2 = UV[I[:, 0]], UV[I[:, 1]], UV[I[:, 2]]
    e1, e2 = p1 - p0, p2 - p0
    d1, d2 = u1 - u0, u2 - u0
    r = d1[:, 0] * d2[:, 1] - d2[:, 0] * d1[:, 1]
    r = np.where(np.abs(r) < 1e-12, 1e-12, r)
    sd = (e1 * d2[:, 1:2] - e2 * d1[:, 1:2]) / r[:, None]
    td = (e2 * d1[:, 0:1] - e1 * d2[:, 0:1]) / r[:, None]
    for k in range(3):
        np.add.at(t, I[:, k], sd)
        np.add.at(b, I[:, k], td)
    t = t - N * (t * N).sum(1, keepdims=True)
    L = np.linalg.norm(t, axis=1, keepdims=True)
    # Ersatz, wo UVs entartet sind
    alt = np.cross(N, np.where(np.abs(N[:, 1:2]) < 0.9, [[0, 1, 0]], [[1, 0, 0]]))
    t = np.where(L > 1e-9, t / np.maximum(L, 1e-12), alt / np.maximum(np.linalg.norm(alt, axis=1, keepdims=True), 1e-12))
    w = np.where((np.cross(N, t) * b).sum(1) < 0, -1.0, 1.0)
    return np.column_stack([t, w])


class GLB:
    def __init__(self):
        self.bin = bytearray()
        self.j = {'asset': {'version': '2.0', 'generator': 'Kernwacht Dark-Fantasy Modellgenerator'},
                  'buffers': [], 'bufferViews': [], 'accessors': [], 'meshes': [], 'nodes': [],
                  'materials': [], 'textures': [], 'images': [], 'samplers': [{'magFilter': 9729, 'minFilter': 9987, 'wrapS': 10497, 'wrapT': 10497}],
                  'scenes': [{'nodes': []}], 'scene': 0}

    def view(self, data: bytes, target=None):
        while len(self.bin) % 4:
            self.bin.append(0)
        off = len(self.bin)
        self.bin += data
        v = {'buffer': 0, 'byteOffset': off, 'byteLength': len(data)}
        if target:
            v['target'] = target
        self.j['bufferViews'].append(v)
        return len(self.j['bufferViews']) - 1

    def acc(self, arr, ctype, atype, target=None, minmax=False, normalized=False):
        dt = {F32: np.float32, U8: np.uint8, U16: np.uint16, U32: np.uint32}[ctype]
        a = np.ascontiguousarray(arr, dtype=dt)
        v = self.view(a.tobytes(), target)
        count = a.shape[0]
        d = {'bufferView': v, 'componentType': ctype, 'count': int(count), 'type': atype}
        if normalized:
            d['normalized'] = True
        if minmax:
            flat = a.reshape(count, -1)
            d['min'] = [float(x) for x in flat.min(0)]
            d['max'] = [float(x) for x in flat.max(0)]
        self.j['accessors'].append(d)
        return len(self.j['accessors']) - 1

    def image(self, jpeg: bytes):
        v = self.view(jpeg)
        self.j['images'].append({'bufferView': v, 'mimeType': 'image/jpeg'})
        self.j['textures'].append({'sampler': 0, 'source': len(self.j['images']) - 1})
        return len(self.j['textures']) - 1

    def finish(self) -> bytes:
        while len(self.bin) % 4:
            self.bin.append(0)
        self.j['buffers'] = [{'byteLength': len(self.bin)}]
        for k in ('textures', 'images', 'materials', 'meshes', 'accessors', 'bufferViews'):
            if not self.j[k]:
                del self.j[k]
        js = json.dumps(self.j, separators=(',', ':')).encode()
        while len(js) % 4:
            js += b' '
        total = 12 + 8 + len(js) + 8 + len(self.bin)
        return (struct.pack('<III', 0x46546C67, 2, total) + struct.pack('<II', len(js), 0x4E4F534A) + js
                + struct.pack('<II', len(self.bin), 0x004E4942) + bytes(self.bin))


EMIS_STRENGTH = {'ember': 3.0, 'void': 2.0}


def write_glb(model: Model, path: str):
    g = GLB()
    J = g.j
    # ---------------- Materialien
    mat_index = {}
    for p in model.parts:
        if p.mat in mat_index:
            continue
        t = tex.build(p.mat)
        m = {'name': p.mat,
             'pbrMetallicRoughness': {'baseColorTexture': {'index': g.image(t['base'])},
                                      'metallicRoughnessTexture': {'index': None}, 'metallicFactor': 1.0, 'roughnessFactor': 1.0}}
        orm = g.image(t['orm'])
        m['pbrMetallicRoughness']['metallicRoughnessTexture']['index'] = orm
        m['occlusionTexture'] = {'index': orm, 'strength': 0.6}
        m['normalTexture'] = {'index': g.image(t['normal']), 'scale': 1.0}
        if t['emis'] is not None:
            m['emissiveTexture'] = {'index': g.image(t['emis'])}
            s = EMIS_STRENGTH.get(p.mat, 1.0)
            m['emissiveFactor'] = [1.0, 1.0, 1.0]
            if s != 1.0:
                m.setdefault('extensions', {})['KHR_materials_emissive_strength'] = {'emissiveStrength': s}
                J.setdefault('extensionsUsed', [])
                if 'KHR_materials_emissive_strength' not in J['extensionsUsed']:
                    J['extensionsUsed'].append('KHR_materials_emissive_strength')
        J['materials'].append(m)
        mat_index[p.mat] = len(J['materials']) - 1

    # ---------------- Knoten
    names = list(model.nodes)
    node_idx = {}
    root = {'name': model.name, 'children': []}
    J['nodes'].append(root)
    J['scenes'][0]['nodes'] = [0]
    for nm in names:
        n = model.nodes[nm]
        lp = model.local_pos(nm)
        d = {'name': nm}
        if np.any(np.abs(lp) > 1e-9):
            d['translation'] = [float(v) for v in lp]
        J['nodes'].append(d)
        node_idx[nm] = len(J['nodes']) - 1
    for nm in names:
        n = model.nodes[nm]
        if n.parent:
            J['nodes'][node_idx[n.parent]].setdefault('children', []).append(node_idx[nm])
        else:
            root['children'].append(node_idx[nm])

    def prim(mesh: Mesh, mat: str, joints=None, weights=None):
        m = mesh
        uv = m.UV * tex.uv_scale(mat)
        mm = Mesh(m.P, m.N, uv, m.I)
        mm.C = m.C
        tg = tangents(mm)
        a = {'POSITION': g.acc(m.P, F32, 'VEC3', ARRAY, minmax=True),
             'NORMAL': g.acc(m.N, F32, 'VEC3', ARRAY),
             'TANGENT': g.acc(tg, F32, 'VEC4', ARRAY),
             'TEXCOORD_0': g.acc(np.column_stack([uv[:, 0], 1 - uv[:, 1]]), F32, 'VEC2', ARRAY)}
        if m.C is not None:
            a['COLOR_0'] = g.acc(np.clip(m.C, 0, 1), F32, 'VEC3', ARRAY)
        if joints is not None:
            a['JOINTS_0'] = g.acc(joints, U8 if joints.max() < 256 else U16, 'VEC4', ARRAY)
            a['WEIGHTS_0'] = g.acc(weights, F32, 'VEC4', ARRAY)
        it = U16 if len(m.P) < 65536 else U32
        return {'attributes': a, 'indices': g.acc(m.I.reshape(-1), it, 'SCALAR', ELEMENT), 'material': mat_index[mat]}

    if not model.skinned:
        # je Knoten ein Mesh, Teile in Knotenkoordinaten
        by_node = {}
        for p in model.parts:
            by_node.setdefault(p.node, {}).setdefault(p.mat, []).append(p.mesh)
        for nm, mats in by_node.items():
            off = model.world(nm)
            prims = []
            for mat, ms in mats.items():
                mm = merge([x.copy() for x in ms])
                mm.P = mm.P - off
                prims.append(prim(mm, mat))
            J['meshes'].append({'name': nm, 'primitives': prims})
            J['nodes'][node_idx[nm]]['mesh'] = len(J['meshes']) - 1
    else:
        from .rig import skin_weights
        joints_order = names
        jmap = {nm: i for i, nm in enumerate(joints_order)}
        prims = []
        by_mat = {}
        for p in model.parts:
            by_mat.setdefault(p.mat, []).append(p)
        for mat, ps in by_mat.items():
            meshes, J4, W4 = [], [], []
            for p in ps:
                jj, ww = skin_weights(model, p, jmap)
                meshes.append(p.mesh.copy()); J4.append(jj); W4.append(ww)
            mm = merge(meshes)
            prims.append(prim(mm, mat, np.vstack(J4), np.vstack(W4)))
        J['meshes'].append({'name': model.name + '_mesh', 'primitives': prims})
        ibm = []
        for nm in joints_order:
            w = model.world(nm)
            M = np.eye(4)
            M[:3, 3] = -w
            ibm.append(M.T.reshape(-1))  # spaltenweise
        ibm_acc = g.acc(np.array(ibm), F32, 'MAT4')
        J['skins'] = [{'name': 'Armature', 'joints': [node_idx[n] for n in joints_order], 'inverseBindMatrices': ibm_acc,
                       'skeleton': node_idx[joints_order[0]]}]
        J['nodes'].append({'name': model.name + '_mesh', 'mesh': 0, 'skin': 0})
        root['children'].append(len(J['nodes']) - 1)

    # ---------------- Animationen
    anims = []
    for cname, chans in model.clips.items():
        samplers, channels = [], []
        for ch in chans:
            if ch.node not in node_idx:
                continue
            ti = g.acc(np.array(ch.times, dtype=np.float32), F32, 'SCALAR', minmax=True)
            vals = np.array(ch.values, dtype=np.float32)
            if ch.path == 'rotation':
                vals = vals / np.linalg.norm(vals, axis=1, keepdims=True)
            vo = g.acc(vals, F32, 'VEC4' if ch.path == 'rotation' else 'VEC3')
            samplers.append({'input': ti, 'output': vo, 'interpolation': 'LINEAR'})
            channels.append({'sampler': len(samplers) - 1, 'target': {'node': node_idx[ch.node], 'path': ch.path}})
        if channels:
            anims.append({'name': cname, 'samplers': samplers, 'channels': channels})
    if anims:
        J['animations'] = anims
    J['nodes'][0]['extras'] = {'category': model.category, 'title': model.title, 'triangles': int(model.tris)}
    data = g.finish()
    with open(path, 'wb') as f:
        f.write(data)
    return len(data)
