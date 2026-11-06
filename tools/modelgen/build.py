"""python3 build.py [name ...]  – baut Modelle nach out/glb/<kategorie>/<name>.glb und schreibt out/manifest.json"""
import json, os, sys, time, traceback
sys.path.insert(0, os.path.dirname(__file__))
from dm import towers
from dm.gltf import write_glb
REGS = {}
for mod in ('towers', 'enemies', 'bosses', 'heroes', 'world', 'heroes2', 'enemies2', 'bosses2', 'towers2'):
    try:
        M = __import__('dm.' + mod, fromlist=['REG'])
        REGS.update({k: (mod, f) for k, f in M.REG.items()})
    except ModuleNotFoundError as e:
        if e.name != 'dm.' + mod: raise
want = sys.argv[1:] or list(REGS)
man_path = 'out/manifest.json'
man = json.load(open(man_path)) if os.path.exists(man_path) else {}
for name in want:
    mod, fn = REGS[name]
    t0 = time.time()
    try:
        m = fn()
    except Exception:
        traceback.print_exc(); continue
    d = f'out/glb/{m.category}'
    os.makedirs(d, exist_ok=True)
    size = write_glb(m, f'{d}/{m.name}.glb')
    os.makedirs('out/meta', exist_ok=True)
    man[m.name] = {'file': f'glb/{m.category}/{m.name}.glb', 'category': m.category, 'title': m.title, 'tris': int(m.tris),
                   'bytes': size, 'clips': list(m.clips), 'nodes': list(m.nodes) if not m.skinned else None, 'bones': len(m.nodes) if m.skinned else 0,
                   'materials': sorted({p.mat for p in m.parts}), 'notes': m.notes}
    json.dump(man[m.name], open(f'out/meta/{m.name}.json', 'w'), ensure_ascii=False)
    print(f'{m.name:16s} {m.tris:7d} Dreiecke {size/1e6:5.2f} MB {time.time()-t0:5.1f} s')
# Manifest aus allen Einzeldateien (parallele Läufe überschreiben sich nicht)
import glob
order = list(REGS)
allm = {}
for f in glob.glob('out/meta/*.json'):
    allm[os.path.basename(f)[:-5]] = json.load(open(f))
man = {k: allm[k] for k in order if k in allm}
json.dump(man, open(man_path, 'w'), ensure_ascii=False, indent=1)
