# Kernwacht-Modelle in Unity

Dieses Paket enthält:
- alle 85 Modelle (34 Türme, 18 Gegner, 7 Bosse, 12 Helden, 14 Weltobjekte),
- Skripte, die daraus fertige Prefabs mit Animator machen,
- einen Modellkatalog (Spiel-Kennung → Prefab),
- eine Vorschauszene.

Die Kennungen sind dieselben wie im Browser-Spiel (`ranger`, `runner`, `ashQueen`, `lyra` …).

## 1. Projekt
- **Empfohlen:** Unity 6 oder 2022.3 LTS mit der Vorlage **„Universal 3D“ (URP)**. Das passt am besten für WebGL.
- **Ordner kopieren:** `Assets/Kernwacht` aus dem Zip in den `Assets`-Ordner deines Projekts.

## 2. Einrichten (einmalig, über das Menü „Kernwacht“)
1. **Kernwacht → 0 · glTFast installieren**
   - Installiert den offiziellen glTF-Importer `com.unity.cloud.gltfast`.
   - Warten, bis die Konsole „installiert“ meldet.
2. **Kernwacht → 1 · Modelle einrichten**
   - Stellt alle Modelle auf Mecanim-Animationen um.
   - Kopiert die Clips nach `Animations/<id>/`. Idle und Walk laufen dabei in Schleife.
   - Legt je Modell einen Animator Controller an.
   - Erzeugt Prefabs unter `Prefabs/<kategorie>/<id>.prefab` und den Katalog `Data/ModelCatalog.asset`.
3. **Kernwacht → 2 · Vorschauszene erzeugen**
   - Stellt alle Modelle in Reihen auf, gespeichert als `Scenes/Modellvorschau.unity`.
   - Mit **Play** spielen alle Modelle reihum ihre Animationen ab.

Alle Schritte lassen sich gefahrlos wiederholen, zum Beispiel nach einem Modell-Update. Dann einfach die .glb-Dateien in `Models/` ersetzen und Schritt 2 erneut ausführen.

## 3. Im Spielcode verwenden
```csharp
using Kernwacht.Models;

public ModelCatalog catalog;   // Data/ModelCatalog.asset im Inspector zuweisen

// Turm bauen
var tower = catalog.Spawn("ranger", cellCenter, Quaternion.identity);
var tv = tower.GetComponent<TowerView>();
tv.SetTarget(enemy.transform);          // Aufsatz (Knoten „Turret“) dreht sich zum Ziel
if (tv.IsAimed()) {
    Vector3 from = tv.Fire();           // Rückstoß-Animation + Austrittspunkt („Muzzle“)
    SpawnProjectile(from, enemy);
}

// Gegner
var e = catalog.Spawn("runner", spawnPos, Quaternion.identity);
var uv = e.GetComponent<UnitView>();
// Laufen/Stehen und Blickrichtung erkennt UnitView automatisch aus der Bewegung des Objekts.
uv.PlayAttack();                         // z. B. wenn der Gegner den Kern erreicht
float t = uv.PlayDeath();                // bleibt liegen; nach t Sekunden entfernen oder ausblenden

// Weltobjekte
catalog.Spawn("core", goalPos, Quaternion.identity).GetComponent<WorldPropView>().Pulse();  // Treffer am Kern
```

## 4. Maße und Benennung
- **Maßstab:** 1 Einheit = 1 Spielfeld. Türme passen auf ein Feld, der Goldgräber belegt 2×2 (`ModelInfo.footprint = 2`).
- **Blickrichtung:** Alle Modelle schauen nach +Z.
- **Türme** haben die Knoten `Base`, `Turret` und `Muzzle`. Clips: `Idle`, `Attack`.
- **Figuren** haben ein Skelett. Clips: `Idle`, `Walk`, `Attack`, `Death`.
  - Zweibeiner nutzen einheitliche Knochennamen (`hips`, `spine`, `chest`, `head`, `upperarm_L` …). Animationen sind deshalb zwischen ihnen austauschbar.
- **Klick-Collider:** Jedes Prefab hat einen BoxCollider zum Anklicken.

## 5. WebGL-Tipps
- **Player Settings → WebGL:**
  - Compression Format: **Brotli**
  - Texture Compression: **ASTC** (Handy) oder **DXT** (Desktop)
  - Managed Stripping Level: **High**
- **Detailstufen:** Viele Figuren gleichzeitig? Dann eine LOD-Stufe einsetzen. Das Blender-Skript `Tools/kernwacht_import.py` erzeugt FBX-Dateien mit `_LOD0`/`_LOD1`/`_LOD2`. Legst du sie statt der .glb in `Models/`, richtet Schritt 2 sie ebenfalls ein.
- **Animator-Culling:** Steht auf „Cull Update Transforms“. Figuren außerhalb des Bildes kosten damit kaum Rechenzeit.

## 6. Wenn etwas nicht klappt
| Problem | Lösung |
|---|---|
| Menü „Kernwacht“ fehlt | Konsole auf Kompilierfehler prüfen. Die Skripte liegen in `Scripts/`. |
| Modelle sind rosa | Render-Pipeline passt nicht zum Shader. glTFast erkennt URP oder Built-in automatisch, nach einem Pipeline-Wechsel Schritt 2 wiederholen. |
| Figuren bewegen sich nicht | Am .glb im Inspector „Animation Method: Mecanim“ wählen, Apply, dann Schritt 2 erneut. |
| Warnung „Animationseinstellung nicht gefunden“ | Wie oben, die Einstellung von Hand setzen. |
