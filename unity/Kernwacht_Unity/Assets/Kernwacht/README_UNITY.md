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

## Spielbares Kernwacht (Szene `Scenes/Kernwacht.unity`)

4. **Kernwacht → 3 · Spielszene erzeugen**
   - Legt Materialien und `Resources/KernwachtGameAssets.asset` an.
   - Baut die Szene `Scenes/Kernwacht.unity` und setzt sie an die erste Stelle der Build-Liste.
   - Stellt die WebGL-Einstellungen ein (Gzip mit Fallback, Daten-Caching).
   - **Play** startet das Menü: Karte (7 Biome), Schwierigkeit und Modus (20 Wellen oder Endlos) wählen.
5. **Kernwacht → 4 · WebGL-Build erstellen**
   - Baut das Browserspiel nach `Builds/WebGL` (nötig: Modul „Web Build Support“ im Unity Hub).

**Was schon drin ist (Werte 1:1 aus dem Webspiel):**
- 7 Karten mit Wegen, Anhöhen, Leylinien, Goldadern und Hindernis-Modellen.
- 30 vorberechnete Wellen je Karte, im Endlosmodus fortgesetzt.
- 20 baubare Türme mit 3 Ausbaustufen und 2 Spezialisierungen, dazu 12 legendäre Fusionen.
- Angriffsarten: Geschoss, Fläche, Puls, Kette, Strahl, Laser, Tesla, Meteor, Minen, Riss, Gravitation. Totem und Pylon verstärken, der Goldgräber bringt Einnahmen.
- Status und Reaktionen: Brand, Gift, Kälte, Frost, Markierung, Spröde und Betäubung; außerdem Dampfstoß, Überladung, Thermoschock, Resonanzbruch und Giftbrand.
- Alle 19 Gegner, Elite-Gegner, Mutationen, Schilde, Regeneration, Phasenverschiebung und Heiler.
- 7 Bosse mit Phasen, Standfestigkeit, Brut, Rissprung, Zwillingsbund, Nullschild und Schwachpunkten.

**Steuerung**
- **Maus:** Linksklick baut oder wählt aus, Rechtsklick bricht ab. Rechte oder mittlere Maustaste ziehen bewegt die Kamera, das Mausrad zoomt.
- **Kamera:** Q/E drehen, WASD verschiebt.
- **Tasten:** Leertaste startet die Welle, 1–0 wählt einen Turm, U baut aus, Entf verkauft, F ändert das Tempo, P pausiert.

**Noch nicht übertragen**
- Helden
- Runen
- Netzwerke und Resonanzen
- Roguelite-Modifikatoren
- Kampagne
- Mazing-Modus
- Speicherstände

Die Spieldaten erzeugt `web/scripts/export_unity.ts`. Aufruf im Webspiel-Ordner: `node --import tsx scripts/export_unity.ts <Zielordner>`. Danach die JSON-Dateien nach `Assets/Kernwacht/Resources/KernwachtData/` kopieren.

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
