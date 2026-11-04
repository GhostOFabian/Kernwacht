"""KERNWACHT – Blender-Importskript (Blender 3.6 / 4.x)

Importiert alle .glb-Modelle, legt LOD-Stufen an und speichert je Modell
  * eine .blend-Datei   →  <Ausgabe>/blend/<kategorie>/<name>.blend
  * eine .fbx-Datei     →  <Ausgabe>/fbx/<kategorie>/<name>.fbx   (Texturen eingebettet, alle Animationen)

Aufruf (ohne Oberfläche):
  blender --background --python kernwacht_import.py -- <glb-Ordner> <Ausgabe-Ordner> [--no-lod] [--only name1,name2]

Oder in Blender: Reiter „Scripting“ → Datei öffnen → GLB_DIR/OUT_DIR unten anpassen → „Run Script“.

LOD: _LOD0 (Original), _LOD1 (50 %), _LOD2 (25 %). Unity erkennt die Endungen in der FBX und baut
automatisch eine LOD Group. Skelett, Gewichte und Animationen bleiben erhalten.
"""
import os
import sys
import traceback

import bpy

# Ordner hier anpassen, falls das Skript nicht im mitgelieferten Ordner „blender“ liegt:
HERE = os.path.dirname(os.path.abspath(__file__)) if '__file__' in globals() else os.getcwd()
GLB_DIR = os.path.join(HERE, '..', 'glb')
OUT_DIR = os.path.join(HERE, '..', 'export')
LODS = [(1, 0.5), (2, 0.25)]

argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
ONLY = None
if argv:
    pos = [a for a in argv if not a.startswith('--')]
    if len(pos) >= 1:
        GLB_DIR = pos[0]
    if len(pos) >= 2:
        OUT_DIR = pos[1]
    if '--no-lod' in argv:
        LODS = []
    if '--only' in argv:
        ONLY = set(argv[argv.index('--only') + 1].split(','))


def clear():
    if bpy.context.object and bpy.context.object.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for coll in (bpy.data.meshes, bpy.data.materials, bpy.data.images, bpy.data.actions, bpy.data.armatures, bpy.data.textures):
        for d in list(coll):
            coll.remove(d)


def make_lods(name):
    """Dupliziert jedes Mesh-Objekt und reduziert es mit dem Decimate-Modifikator."""
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    for o in meshes:
        base = o.name
        o.name = f'{base}_LOD0'
        for lvl, ratio in LODS:
            c = o.copy()
            c.data = o.data.copy()
            c.name = f'{base}_LOD{lvl}'
            for col in o.users_collection:
                col.objects.link(c)
            bpy.context.view_layer.objects.active = c
            for s in bpy.context.selected_objects:
                s.select_set(False)
            c.select_set(True)
            mod = c.modifiers.new('LOD', 'DECIMATE')
            mod.ratio = ratio
            mod.use_collapse_triangulate = True
            # Decimate vor den Armature-Modifikator schieben und anwenden (Gewichte bleiben erhalten)
            if len(c.modifiers) > 1:
                bpy.ops.object.modifier_move_to_index(modifier='LOD', index=0)
            bpy.ops.object.modifier_apply(modifier='LOD')


def stash_actions():
    """Alle importierten Aktionen behalten (Fake User), damit sie in .blend und FBX landen."""
    for a in bpy.data.actions:
        a.use_fake_user = True


def process(path, cat):
    name = os.path.splitext(os.path.basename(path))[0]
    clear()
    bpy.ops.import_scene.gltf(filepath=path, merge_vertices=True)
    stash_actions()
    if LODS:
        make_lods(name)
    for o in bpy.context.scene.objects:
        if o.type == 'MESH':
            for p in o.data.polygons:
                p.use_smooth = True
    bdir = os.path.join(OUT_DIR, 'blend', cat)
    fdir = os.path.join(OUT_DIR, 'fbx', cat)
    os.makedirs(bdir, exist_ok=True)
    os.makedirs(fdir, exist_ok=True)
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(bdir, name + '.blend'), compress=True)
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(fdir, name + '.fbx'),
        use_selection=False,
        apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z', axis_up='Y',
        add_leaf_bones=False,
        bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False, bake_anim_simplify_factor=0.5,
        path_mode='COPY', embed_textures=True,
        mesh_smooth_type='FACE', use_tspace=True,
    )
    return name


def main():
    done, failed = [], []
    for cat in sorted(os.listdir(GLB_DIR)):
        d = os.path.join(GLB_DIR, cat)
        if not os.path.isdir(d):
            continue
        for f in sorted(os.listdir(d)):
            if not f.endswith('.glb'):
                continue
            if ONLY and os.path.splitext(f)[0] not in ONLY:
                continue
            try:
                done.append(process(os.path.join(d, f), cat))
                print(f'[Kernwacht] fertig: {cat}/{f}')
            except Exception:
                traceback.print_exc()
                failed.append(f)
    print(f'[Kernwacht] {len(done)} Modelle exportiert nach {OUT_DIR}')
    if failed:
        print('[Kernwacht] Fehlgeschlagen:', ', '.join(failed))


main()
