"""Erzeugt README.md mit Modelltabelle und das Auslieferungs-Zip."""
import json
import os
import shutil
import zipfile

man = json.load(open('out/manifest.json'))
CATS = [('tower', 'Türme'), ('enemy', 'Gegner'), ('boss', 'Bosse'), ('hero', 'Helden'), ('world', 'Welt')]
rows = []
for c, label in CATS:
    items = [(k, v) for k, v in man.items() if v['category'] == c]
    rows.append(f'\n### {label} ({len(items)})\n\n| Datei | Name | Dreiecke | Animationen | Hinweis |\n|---|---|---:|---|---|')
    for k, v in items:
        tri = f"{v['tris']:,}".replace(',', '.')
        rows.append(f"| `{v['file']}` | {v['title']} | {tri} | {', '.join(v['clips']) or '–'} | {v.get('notes') or ''} |")
total = sum(v['tris'] for v in man.values())
total_de = f'{total:,}'.replace(',', '.')

readme = f"""# KERNWACHT – Dark-Fantasy-Modelle

{len(man)} eigene 3D-Modelle für das Tower-Defense-Spiel: alle Türme inklusive Fusionen und Barrikaden, alle Gegner, alle Bosse,
die zwölf Helden (jeder mit eigenem Modell) sowie Kern, Rissportal, Laden und Hindernisse. Alle Modelle wurden für dieses
Projekt neu entworfen und per Code erzeugt; es wurden keine fremden Modelle, Scans oder Vorlagen verwendet.

## Ordner

| Ordner | Inhalt |
|---|---|
| `glb/<kategorie>/` | Die Modelle als **.glb** (glTF 2.0, Texturen eingebettet). Öffnen direkt in Blender, in Unity mit glTFast. |
| `blender/kernwacht_import.py` | Skript für Blender: importiert alle .glb, legt LOD-Stufen an, speichert **.blend** und **.fbx**. |
| `vorschau/` | Vorschaubild je Modell. |
| `manifest.json` | Maschinenlesbare Liste: Datei, Dreiecke, Animationen, Knoten, Materialien. |

## Maßstab, Ausrichtung, Benennung

- **1 Einheit = 1 Spielfeld (1 m).** Türme passen auf ein Feld (Grundfläche ≤ 0,95 m, der Goldgräber auf 2×2). Gegner 0,4–1,3 m, Bosse 2–3 m, Helden ≈ 1 m.
- **Y ist oben, Vorderseite +Z** (glTF-Standard). Unity dreht beim Import automatisch richtig.
- **Türme** haben drei Knoten:
  - `Base` (fest),
  - `Turret` (dreht sich zum Ziel, um Y),
  - `Muzzle` (Austrittspunkt der Geschosse).
  Clips: `Idle` (Schleife), `Attack` (einmalig).
- **Figuren** (Gegner, Bosse, Helden) haben ein Skelett. Zweibeiner nutzen immer dieselben Knochennamen (`hips`, `spine`, `chest`, `neck`, `head`, `upperarm_L/R`, `forearm_L/R`, `hand_L/R`, `thigh_L/R`, `shin_L/R`, `foot_L/R`). Damit lassen sich Animationen zwischen den Figuren austauschen. Vierbeiner, Flieger, Spinnen, Würmer und schwebende Wesen haben eigene, sinnvoll benannte Skelette.
- **Clips aller Figuren:** `Idle`, `Walk` (bei Fliegern: Flug), `Attack`, `Death` (endet liegend).

## In Unity verwenden

**Weg A – direkt als .glb (empfohlen für den Anfang)**
1. Package Manager → *Add package by name* → `com.unity.cloud.gltfast`.
2. Den Ordner `glb` ins Projekt ziehen. Modelle, Materialien und Animationen erscheinen sofort.
3. Für Mecanim-Clips in den Importeinstellungen „Animation: Mecanim“ wählen.

**Weg B – über Blender als .fbx mit LODs**
1. Blender (3.6 oder 4.x) installieren.
2. Im Terminal ausführen:
   `blender --background --python blender/kernwacht_import.py -- glb export`
   Alternativ in Blender unter *Scripting* das Skript öffnen und *Run Script* klicken.
3. Ergebnis:
   - `export/blend/…` (bearbeitbar)
   - `export/fbx/…` mit `_LOD0` (100 %), `_LOD1` (50 %) und `_LOD2` (25 %). Unity baut daraus automatisch eine LOD Group.
4. In Unity bei den Figuren *Rig → Generic* einstellen. Die Zweibeiner lassen sich auch als *Humanoid* zuordnen.

**WebGL-Tipps**
- Insgesamt {total_de} Dreiecke. Türme ≈ 3–15 Tsd., Gegner ≈ 2–23 Tsd., Bosse ≈ 4–32 Tsd., Helden ≈ 17–25 Tsd.
- Im Browser bei vielen Gegnern gleichzeitig LOD1/LOD2 verwenden.
- Texturen sind 512 px (JPEG).
- In Unity für WebGL die Kompression auf ASTC/ETC2 bzw. DXT stellen.
- Türme mit GPU-Instancing zeichnen.

## Materialien

PBR (Metallic/Roughness) mit Grundfarbe, Normal Map, ORM-Textur (Okklusion/Rauheit/Metall) und bei leuchtenden Teilen Emission.
Organische Teile tragen zusätzlich eine Vertexfarbe mit Umgebungsverdeckung (COLOR_0).

Materialfamilien:
- Mauerwerk, Fels, Obsidian
- Eisen, Rost, Stahl, Gold
- Holz, Rinde
- Leder, Stoff
- Knochen
- Fleisch, Chitin, Schuppen, Fell, Haut
- Eis, Glut, Leere
- Runen
- Kristalle in fünf Farben

## Grenzen (ehrlich)

- Die Modelle sind per Code modelliert: Distanzfelder, Marching Cubes, Polygonreduktion. Figuren haben seit Version 2 definierte Muskeln und Gesichtszüge sowie passgenaue Kleidungs- und Rüstungsschichten mit harten Kanten. Golems bestehen aus kantigen Felsbrocken. Der Look ist halbrealistisch und düster, aber nicht auf dem Niveau von Hand-Sculpts mit Fotostrukturen.
- Die Animationen sind einfache Grundbewegungen zum Testen. Für ein fertiges Spiel lohnt es sich, sie nachzuarbeiten, etwa über Mixamo oder eigene Animationen auf den gleichen Knochen.
- Das Blender-Skript wurde ohne Blender geschrieben, denn in der Erstellungsumgebung war keines verfügbar. Die .glb-Dateien selbst sind mit einem eigenen WebGL-Betrachter geprüft: Geometrie, Texturen, Skinning und Animationen.

## Modellliste
{''.join(chr(10) + r for r in rows)}
"""
os.makedirs('package', exist_ok=True)
open('package/README.md', 'w').write(readme)
shutil.copy('out/manifest.json', 'package/manifest.json')
z = zipfile.ZipFile('Kernwacht_DarkFantasy_Modelle.zip', 'w', zipfile.ZIP_DEFLATED)
z.write('package/README.md', 'Kernwacht_Modelle/README.md')
z.write('package/manifest.json', 'Kernwacht_Modelle/manifest.json')
z.write('blender/kernwacht_import.py', 'Kernwacht_Modelle/blender/kernwacht_import.py')
for k, v in man.items():
    z.write('out/' + v['file'], 'Kernwacht_Modelle/' + v['file'])
    t = f'gallery/thumbs/{k}.jpg'
    if os.path.exists(t):
        z.write(t, f'Kernwacht_Modelle/vorschau/{k}.jpg')
z.close()
print('ok', os.path.getsize('Kernwacht_DarkFantasy_Modelle.zip') / 1e6, 'MB')
