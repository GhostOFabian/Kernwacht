# Kernwacht

Dark-Fantasy-Tower-Defense. Dieses Repository enthält alles zum Spiel:

| Ordner | Inhalt |
|---|---|
| `web/` | Aktuelles Browserspiel (TypeScript/WebGL). `npm install`, `node scripts/build.mjs`, Tests mit `node --import tsx --test tests/*.test.ts`. |
| `unity/Kernwacht_Unity/` | Unity-Projekt (Unity 6, URP, WebGL) mit allen 85 Dark-Fantasy-Modellen, Katalog und Einrichtungsskripten. Anleitung: `Assets/Kernwacht/README_UNITY.md`. |
| `tools/modelgen/` | Python-Generator der Modelle (`python build.py`), WebGL-Betrachter, Modellkatalog-Galerie und Blender-Importskript. |

## Unity-Projekt öffnen

1. Unity Hub → **Add → Add project from disk** → `unity/Kernwacht_Unity` wählen.
2. Mit Unity 6 (6000.0 LTS oder neuer) öffnen. glTFast und URP werden automatisch geladen.
3. Menü **Kernwacht → 1 · Modelle einrichten**, danach **2 · Vorschauszene erzeugen**.
4. Für den Browser: **File → Build Profiles → Web** wählen und bauen.
