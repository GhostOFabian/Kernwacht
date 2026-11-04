"""Entfernt COLOR_0 aus geskinnten Primitiven (Unity 6.6 + glTFast: 'Skinned mesh attributes use wrong streams').
Aufruf: python strip_skin_colors.py <ordner mit .glb> ..."""
import json, struct, sys, pathlib

def fix(path):
    b = path.read_bytes()
    magic, ver, length = struct.unpack_from('<III', b, 0)
    jlen, jtype = struct.unpack_from('<II', b, 12)
    js = json.loads(b[20:20 + jlen])
    rest = b[20 + jlen:]
    n = 0
    for m in js.get('meshes', []):
        for p in m.get('primitives', []):
            a = p.get('attributes', {})
            if 'JOINTS_0' in a and 'COLOR_0' in a:
                del a['COLOR_0']; n += 1
    if not n:
        return 0
    jb = json.dumps(js, separators=(',', ':')).encode()
    jb += b' ' * ((4 - len(jb) % 4) % 4)
    out = struct.pack('<III', magic, ver, 12 + 8 + len(jb) + len(rest)) + struct.pack('<II', len(jb), jtype) + jb + rest
    path.write_bytes(out)
    return n

total = 0
for d in sys.argv[1:]:
    for f in sorted(pathlib.Path(d).rglob('*.glb')):
        k = fix(f)
        if k: print(f.name, k); total += 1
print('geändert:', total)
