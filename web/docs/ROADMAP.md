# KERNWACHT – Roadmap

Gesammelte Vorschläge vom 15. und 16.10.2026.

## Umsetzungsstand (16.10.2026): umgesetzt

| Punkt | Status | Umsetzung |
|---|---|---|
| A1.1 Schatten | ✔ | Schattenkarte der Sonne (2048², 3×3-PCF), Schattenkamera über die ganze Karte; Blob-Schatten unter Fliegern. |
| A1.2 Post-Processing | ✔ | MSAA, Bloom (Helligkeitsfilter + 3 Unschärfe-Durchgänge), Farbstimmung je Biom und Geländeereignis, Vignette, Blitz-Aufhellung. |
| A1.3 Lebendiges Gelände | ✔ | Weiche Kantenlippen an Abbrüchen, Gras/Schilf im Wind, Steinchen, Lavaritzen, Leuchtpilze; Wasser-, Lava-, Eis-, Sand-, Sumpf-, Wolken- und Abgrundmeer; Weg als Band mit Randsteinen und Fahrspuren. |
| A1.4 Biom-Wetter | ✔ | Glühwürmchen, Funken, Schnee, Glitzern, Wind, Nebel, Sporen; Schneesturm, Hitze, Nebel und Leuchtflut als sichtbare Geländeereignisse. |
| A1.5 Animierte Türme | ✔ | Rückstoß, Drehung zum Ziel, schwebende/rotierende Teile, Banner, Goldring, Spitzen und Krone je Ausbaustufe, Spezialisierungsmerkmale, Aufbauanimation. |
| A1.6 Trefferfeedback | ✔ | Trefferblitz mit Stauchen, Funken nach Schadensart, Todesauflösung mit Leuchtpartikeln und Fleck am Boden, Status am Körper (Flammen, Giftblasen, Eisblock, Tropfen, Sterne, Ranken). |
| A2.7 Effekt-Shader | ✔ | Verästelte, flackernde Blitze, Feuer mit Rauch, Portale mit Sog, Schildblasen, Meteore mit Schweif, Laser mit Aufladung. |
| A2.8 Bosse als Ereignis | ✔ | Kamerafahrt + Titelbanner mit Kinobalken, Bodenring und Lichtsäule beim Auftritt, Phasenwechsel mit Schockwelle, Aura ab Phase 2, Schwachpunkte, Zwillingsband. |
| A2.9 Lichtstimmung | ✔ | Tageszeit wandert mit den Wellen, Bosslicht, Bauphase etwas heller, Kernenergie-Warnung über Vignette. |
| A2.10 Three.js | ✖ bewusst | npm-Registry weiterhin nicht erreichbar; alle Effekte im eigenen Renderer. |
| A3.11 SVG-Symbole | ✔ | Eigene SVG-Symbole für Bedienelemente; Turm-, Gegner-, Boss- und Heldenbilder werden aus den 3D-Modellen gerendert. |
| A3.12 Eigene Schrift | ✔ | Cinzel (Überschriften) und Nunito Sans über Google Fonts mit Ersatzschriften offline. |
| A3.13 Übergänge, Banner, Goldzähler | ✔ | Bildschirm- und Dialoganimationen, Wellen-/Boss-/Sieg-Banner, Gold zählt hoch. |
| A3.14 3D-Turmvorschau | ✔ | Drehende Vorschau (Spritesheet) und Spezialisierungsbilder im Turmdetail; Bilder in Leiste und Kodex. |
| A3.15 Belebtes Hauptmenü | ✔ | Ein Bot spielt im Hintergrund eine echte Partie, die Kamera kreist. |
| A4 Audio, Erfolge, Skins, Inhalte | ✔ | Hall, Echo, Trommeln, 15 neue Effekte; 16 Erfolge, 6 Skins; 7 Biome, 7 Bosse, 10 Missionen. Playtests mit Menschen stehen weiter aus. |
| B1–B6 Heldensystem | ✔ | Loadout (2), Rissenergie, Einsatz auf dem Weg, Verhalten, Einsatz-/Abklingzeit, Niederlage, Stufen 1–5 mit Perks, 9 Helden, Auren, Duos, Heldenchor, Heldenmodifikatoren, Freischaltung über die Kampagne, Schutzmission, Z/X + A/S/D, Touch-Bedienung, Aura- und Zeitanzeige. |
| C1 Größere Karten | ✔ | Normal/Groß/Riesig, Weggabelungen mit mehreren Rissen, Minikarte, Zoom, Kamera folgt optional dem Boss. Gelände wird statisch gebatcht statt in Kacheln (bis 42×26 unkritisch). |
| C2 Unterschiedliche Helden | ✔ | Eigene Modelle mit Lauf- und Angriffsanimation, 2 Fähigkeiten + Ultimate, klar getrennte Rollen. |
| C3 Krassere Monster | ✔ | 6 neue Gegner, 5 Mutationen, 3 neue Bosse, größere animierte Modelle. |
| C4 Krassere Türme | ✔ | 6 neue Türme, 4 Fusionen, aktive Fähigkeiten, wachsende Modelle. |
| C5 Bessere Effekte | ✔ | Weiche Sprite-Partikel, Bloom, runde Modelle, MSAA, Dekale, Kamerawackeln und Zeitlupe beim Bosssieg (abschaltbar über „Bewegung reduzieren“). Toon-Konturen und Bewegungsunschärfe nicht umgesetzt (gestreckte Funken stattdessen). |

**Hinweis Leistung:** Auf echter GPU-Hardware wurde nicht gemessen. In der Testumgebung (Software-WebGL) ist der Renderer langsam; die automatische Qualität senkt deshalb stufenweise Kantenglättung, Schatten und Glühen, danach die Auflösung. „Niedrig“ schaltet zusätzlich den Bodenbewuchs ab.

---

---

## Teil A – Grafik & Präsentation

### Empfohlenes erstes Paket
Schatten + Bloom + eigene SVG-Icons statt Emojis + Biom-Wetter.

### A1. Großer Effekt, überschaubarer Aufwand
1. **Echte Schatten:** eine Schattenkarte (Shadow Map) für die Sonne. Zwischenschritt: weiche Kontaktschatten (Blob-Shadows) unter allen Objekten und Ambient Occlusion in den Ecken.
2. **Post-Processing:**
   - Bloom für Feuer, Blitze, Portal, Kern und Runen (die Emit-Werte gibt es bereits).
   - Farbkorrektur je Biom (Eis kühl, Vulkan warm).
   - Leichte Vignette.
3. **Lebendiges Gelände:**
   - Abgeschrägte Kanten, Mikro-Höhenunterschiede.
   - Gras- und Steinbüschel per Instancing.
   - Animiertes Wasser bzw. Lava an den Rändern.
   - Der Weg als durchgehendes Band mit Rändern, Spurrillen und Fußspuren.
4. **Biom-Wetter:**
   - Schneeflocken, Aschefunken, Glühwürmchen, Wolkenschatten.
   - Geländeereignisse sichtbar machen (Schneetreiben, Hitzeflimmern).
5. **Animierte Türme:**
   - Rückstoß, sichtbare Rohre und Sehnen.
   - Sturmrufer lädt sich vor dem Blitz auf, Frostwächter atmet Kältenebel aus.
   - Ausbaustufen verändern das Modell sichtbar (Teile, Banner, Leuchtrunen).
6. **Trefferfeedback:**
   - Knockback und Squash beim Treffer, Zerfall in Teile oder Rauch beim Tod.
   - Status am Körper sichtbar: Flammen, grüner Dunst, Eisschicht.

### A2. Mehr Aufwand, sehr wertig
7. **Effekt-Shader:** flüssige Flammen, wabernde Portale, schimmernde Schildblase, Hitzeverzerrung, verästelte Blitze.
8. **Bosse als Ereignis:**
   - Intro mit Kamerafahrt, Titel und Bodenbeben.
   - Mehrteilige, bewegliche Modelle.
   - Sichtbarer Phasenwechsel (Rüstung bricht ab, Farbwechsel, stärkere Aura).
9. **Lichtstimmung pro Welle:** Bosswellen in der Dämmerung, Türme als eigene Lichtquellen.
10. **Optional: Wechsel auf Three.js**, sobald die npm-Registry erreichbar ist (Schatten, Bloom und PBR fast geschenkt). Die Schnittstellen `WorldView.frame()` und `HudActions` bleiben bestehen.

### A3. Bedienoberfläche
11. Eigene SVG-Icons statt Emojis (größter optischer Gewinn an der Oberfläche).
12. Eigene Schrift mit Charakter für Logo und Überschriften.
13. Animierte Übergänge: Menüs gleiten ein, Gold-Zähler zählt hoch, großes Wellenbanner („Welle 7 – Bosswelle!“).
14. Rotierende 3D-Turmvorschau in Turmleiste und Kodex.
15. Hauptmenü mit Kamerafahrt über eine belebte Welt (Gegner laufen, Türme feuern).

### A4. Abseits der Grafik
- Echte, lizenzierte oder aufwendiger synthetisierte Musik und Effekte.
- Playtests mit Menschen für das Balancing.
- Erfolge und kosmetische Turm-Skins.
- Mehr Bosse, Biome und Kampagnenmissionen.

---

## Teil B – Heldensystem

### B1. Grundprinzip
- **Auswahl vor der Partie:** 1–2 Helden als Loadout.
- **Einsetzen:** Held wählen und eine Stelle auf dem Weg antippen; dort erscheint er aus einem Portal.
- **Verhalten** (per Knopf umschaltbar): „Halten“ (Ankerpunkt, verschiebbar), „Vorrücken“ (dem Gegnerstrom entgegen), „Rückzug zum Kern“.
- **Einsatzzeit** ca. 30–45 s, danach Rückzug und Abklingzeit von 1–2 Wellen. Der Held ist ein taktischer Joker, kein Dauerturm.
- **Rissenergie** als eigene Ressource: Sie füllt sich durch Abschüsse, Elementarreaktionen und überstandene Wellen. Ein Einsatz kostet Energie, kein Gold.
- **Wenn der Held fällt:** keine Kernenergie-Kosten, nur eine längere Abklingzeit.
- **Aufstieg in der Partie:** Stufen 1–5 über Erfahrung, bei jeder Stufe eine Wahl aus zwei Verbesserungen (wie die Turmspezialisierungen).

### B2. Kämpfende Helden
| Held | Rolle | Idee |
|---|---|---|
| Klingenwächterin | Blocker | Hält Gegner fest, Spott-Schrei zieht Aggro. Gegen Läufer-Durchbrüche. |
| Aschenjäger | Fernkampf | Feuerpfeile mit Flächenbrand, Synergie mit dem Glutwerfer. |
| Risswandler | Mobil | Teleportiert sich zu Lecks und erledigt Durchbrecher („Notfallknopf“). |

### B3. Unterstützer-Helden
| Held | Wirkung | Besonderheit |
|---|---|---|
| Runenbardin | Aura: Türme +20 % Angriffstempo. „Lied der Resonanz“ verdoppelt kurz den Netzwerkbonus. | Steht zwischen den Türmen. |
| Schmiedemeister Thorgrim | Repariert betäubte, verbrannte oder gestörte Türme sofort. „Feldschmiede“: ein Turm hat 20 s lang die Werte der nächsten Stufe. | Konter gegen Aschenkönigin und Saboteure. |
| Wettermacherin | Regen macht alle nass (Blitz +50 %), Frosthauch unterkühlt (Feuer erzeugt Dampf). | Schlüssel für Elementar-Combos. |
| Leylinien-Mystiker | Wandelnder Pylon, vernetzt Türme in der Nähe. „Ley-Knoten“ macht seinen Standort 30 s zur Leylinie (+15 % Schaden). | Netzwerke ohne Pylonbau. |
| Prospektorin | Abschüsse in der Nähe +50 % Gold, „Schatzfund“ als Goldbonus einmal pro Welle. | Wirtschafts-Held, kaum Kampfwert. |
| Wächter der Stille | Aura unterbricht Gegnerfähigkeiten, absorbiert eine Bossfähigkeit (Schild). | Spezialist für Bosskämpfe. |

### B4. Synergien
- **Elemente:** Heldenangriffe lösen Reaktionen aus (z. B. Aschenjäger + Frostwächter ergibt Dampfstoß).
- **Netzwerke:** Unterstützer zählen als Netzwerkknoten. Neue Resonanz „Heldenchor“ (Bardin + Feuer + Frost + Blitz).
- **Mazing:** Der Held kann Engstellen blockieren, verändert aber nie die Route (das Pathfinding bleibt einfach und fair).
- **Bosse:** Einzelne Bossfähigkeiten zielen auf Helden, z. B. ein Nullpuls, der Auren unterdrückt.
- **Neue Modifikatoren:** „Heldenmut“ (Abklingzeit −30 %, Türme −5 % Schaden), „Doppelte Rissenergie aus Reaktionen“.

### B5. Langzeitmotivation
- Neue Helden über die Kampagne freischalten. Keine dauerhafte Stärkesteigerung, damit neue Spieler nicht benachteiligt werden.
- Kosmetische Skins über Erfolge.
- Kampagnenmissionen mit Heldenfokus (z. B. „Beschütze die Bardin bis Welle 10“).

### B6. Bedienung
- **Desktop:** Heldenleiste über der Turmleiste. `Z`/`X` wählt einen Helden, Klick auf den Weg setzt ihn ein, Rechtsklick verschiebt den Ankerpunkt.
- **Touch:** Held antippen und dann den Weg antippen. Ziehen verschiebt den Ankerpunkt.
- **Anzeige:** Aura-Radius, Balken für die Einsatzzeit, Abklingzeit auf dem Heldenporträt.

### B7. Empfohlener Einstieg
Klingenwächterin, Runenbardin und Schmiedemeister, zusammen mit Rissenergie, Einsatz auf der Lane, Einsatzzeit, Abklingzeit und Heldenstufen. Danach Wettermacherin, Mystiker und die übrigen.

### B8. Technische Hinweise für die Umsetzung
- Neue Datei `src/config/heroes.ts` (datengetrieben, wie `towers.ts`).
- Neues `src/sim/HeroSystem.ts`. Helden als eigene Instanzen im `GameState` (serialisierbar, deterministisch) mit Befehlen `deployHero`, `setHeroMode` und `moveHeroAnchor` in `GameSimulation`.
- Auren über den bestehenden Stat-Mechanismus (`StatMod`) in `computeTowerStats` einbinden. Den Netzwerk- und Stat-Cache bei Heldenbewegung nur gedrosselt neu berechnen (Dirty-Flag).
- Tests: Rissenergie-Bilanz, Abklingzeiten, Auren, Mazing-Route bleibt unverändert, Speichern und Fortsetzen.

---

## Teil C – Wünsche vom 16.10.2026: größer, krasser, schöner

### C1. Größeres Spielfeld, mehr Freiraum
- Kartengrößen wählbar: **Normal** (bisher 22×14), **Groß** (ca. 34×22), **Riesig** (ca. 46×30, Desktop empfohlen).
- Classic: längere Wege mit mehreren Schleifen, optional **2 Spawns oder 2 Ziele** (Weggabelungen) auf großen Karten.
- Mazing: große offene Flächen mit wenigen Hindernisinseln, damit weite Labyrinthe möglich werden.
- Mehr Bauplätze abseits des Wegs für Pylon-Netzwerke, Minen und Unterstützer-Helden.
- Kamera: weiter herauszoomen, Minikarte in der Ecke (Tippen springt zur Stelle), Kamera folgt optional dem Boss.
- Technik: Gelände in Kacheln aufteilen und nur sichtbare Teile zeichnen; Wegfindung bleibt ein Distanzfeld (performant bis ca. 50×50).

### C2. Unterschiedliche Helden
Wie in Teil B. Zusätzlich sollen sie sich klar unterscheiden:
- **Eigene Silhouette und Animationen** je Held (Laufen, Angriff, Fähigkeit, Sieg).
- **Je Held 2 aktive Fähigkeiten + 1 Ultimate**, die sich mit Rissenergie auflädt (z. B. Klingenwächterin „Bollwerk“: 6 s Unverwundbarkeit, alle Gegner im Umkreis verspotten).
- **Rollen klar getrennt:** Blocker, Fernkämpfer, Assassine, Heiler und Reparierer der Türme, Buffer, Elementar-Magier, Wirtschaft.
- Mehrere Helden können kombiniert werden; Duo-Synergien wie „Bardin + Wettermacherin → Sturmlied“.

### C3. Krassere Monster
- **Größere, detailliertere Modelle** aus mehr Teilen mit Lauf- und Angriffsanimation statt starrer Blöcke.
- **Neue Gegner:**
  - Höhlengolem: zerfällt beim Tod in 3 kleine Golems.
  - Schattenschleicher: unsichtbar, bis ein Resonator oder Runenweber ihn aufdeckt.
  - Belagerungsbestie: greift Türme in Reichweite an und kann sie beschädigen.
  - Heilerschamane: heilt Gegner in der Nähe und muss priorisiert werden.
  - Tunnelgräber: taucht unter und kurz vor dem Ziel wieder auf.
  - Sturmgreif: fliegt schnell und weicht Projektilen aus.
- **Neue Elitemutationen** (zufällig, angekündigt): gepanzert, hastig, explodierend, spaltend, vampirisch.
- **Neue Bosse:**
  - Schlund der Tiefe: frisst Türme, die du zurückkaufen musst.
  - Zwillingswächter: zwei Bosse, die sich gegenseitig heilen, wenn sie nah beieinander stehen.
  - Weltenbrecher: mehrteiliger Riese mit Schwachpunkten, die man zerstört.

### C4. Krassere Türme
- **Imposantere Modelle**, die mit jeder Stufe sichtbar wachsen; die Spezialisierungen verändern die Form komplett.
- **Neue Türme:**
  - Meteorbeschwörer: langsamer, riesiger Flächeneinschlag.
  - Tesla-Spule: Dauerblitzbogen zwischen zwei Türmen.
  - Gravitationsbrunnen: zieht Gegner zusammen.
  - Drachenhorst: ein kleiner Drache fliegt Runden und speit Feuer.
  - Kristalllaser: Strahl, der mit der Zeit stärker wird und durch Gegner geht.
  - Totemschrein: Aura-Unterstützung.
- **Turm-Fusion:** zwei maximal ausgebaute Türme verschmelzen zu einem legendären Turm (z. B. Glutwerfer + Sturmrufer → Plasmawerfer).
- **Aktive Turmfähigkeiten** mit Abklingzeit, z. B. „Überladung“ beim Sturmrufer.

### C5. Bessere Effekte – weg vom Würfel-/Pixel-Look
- **Weiche, runde Partikel** statt kleiner Würfel: Sprites mit Leuchtkern und weichem Rand, additive Mischung.
- **Glühen (Bloom)** und leichte Bewegungsunschärfe bei Projektilen.
- **Glatte, rundere Modelle:** mehr Segmente, Fasen an Kanten, weiche Beleuchtung (Normalen statt reinem Flat-Shading), optional Toon-Shading mit Konturlinien.
- **Kantenglättung** (FXAA oder MSAA) und höhere Auflösung auf Hochleistungsgeräten.
- **Effekte:**
  - Blitze: verästelt und flackernd.
  - Feuer: flüssig, mit Rauch.
  - Frost: Eiskristalle wachsen am Gegner.
  - Gift: grüne Schwaden.
  - Explosionen: Druckwelle, Funken, Rauchsäule und Leuchtring am Boden.
- **Schatten** (siehe A1) und **Bodendekale** wie Brandflecken, Frostflächen und Krater, die langsam verblassen.
- **Kameraeffekte** in Maßen: kurzes Wackeln und Zeitlupe beim Bosskill, abschaltbar.
- Technik: Hier lohnt sich der **Wechsel auf Three.js**, das Schatten, Bloom und weiche Beleuchtung bereits mitbringt. Alternativ den eigenen Renderer erweitern (Sprite-Partikel, Normalen, Postprocessing-Pass).

### Empfohlene Reihenfolge
1. **Effekte und Grafik-Grundlage** (C5 + Schatten): das größte Upgrade beim Eindruck.
2. **Größere Karten + Minikarte** (C1).
3. **Heldensystem mit 3 Helden** (Teil B + C2).
4. **Neue Monster und Bosse** (C3).
5. **Neue Türme und Fusion** (C4).
