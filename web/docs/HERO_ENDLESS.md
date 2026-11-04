# Helden-Endlosmodus – Analyse, Umsetzung, Tests

Stand: 17.10.2026, Version 2.1.0. Dieses Dokument beschreibt die Erweiterung von KERNWACHT um den Modus **„Helden-Endlos“** (`mode: 'heroEndless'`) gemäß dem Erweiterungs-Prompt. Es trennt klar zwischen umgesetzt und getestet, umgesetzt aber nur durch Bots oder Screenshots geprüft, und nicht umgesetzt.

## 1. Analyse der bestehenden Codebasis (Ausgangslage)

| Bereich | Vorgefunden | Entscheidung für den neuen Modus |
|---|---|---|
| Spielschleife | `GameSimulation` mit festem 30-Hz-Takt, reiner JSON-Zustand `GameState`, abgeleitete Daten im `SimCache` (Dirty-Flags) | Wiederverwendet. Der komplette Modus-Zustand liegt in `GameState.he` (in allen anderen Modi `null`). |
| Pause | `s.paused` stoppt `advance()` vollständig | Wiederverwendet. Countdown, Held, Beute, Zonen und Abklingzeiten laufen im Takt und stehen deshalb in der Pause automatisch still. |
| Wellen | `WaveDirector.planWave` (deterministisch je Seed und Welle), Endlosregeln: Boss alle 10, Modifikator alle 4 Wellen | Wiederverwendet. Für den neuen Modus gibt es zusätzlich Wellenprofile und Mini-Bosse. |
| Turmwerte | `computeTowerStats` addiert bereits Helden-Auren (`sim.c.heroAura`) | Um `sim.c.champBuff` erweitert, also dieselbe Stelle und dieselbe Rechenregel. |
| Schaden/Kill | `dealDamage`, `killEnemy` als einziger Todespunkt mit `e.rewarded`-Schutz | Beute wird ausschließlich in `killEnemy` erzeugt, dadurch gibt es keine doppelte Beute. |
| Helden | Lane-Helden (`HeroSystem`): Einsatz nur auf dem Weg, zeitlich begrenzt | Bleiben unverändert. Der frei bewegliche Held ist ein eigenes System (`ChampionSystem`) und nutzt die vorhandenen 3D-Modelle. |
| Speicherung | `SaveSystem` mit feldweiser Validierung, Fortsetzen ab Bauphase | Um `heroMeta` (dauerhaft) und die Validierung von `he` (Lauf) erweitert. Die Speicherversion bleibt bei 3, weil fehlende Felder mit Standardwerten ergänzt werden. |
| UI | DOM-HUD (`Hud`), Bildschirme, `h()`-Helfer, WebGL-Welt (`WorldView`) | `HeroHud` hängt sich in Kopfzeile und Bühne des vorhandenen HUD. Neuer Startbildschirm, neuer Hilfe-Tab, Ergänzungen im Ergebnisbildschirm. |
| Kamera | Isometrisch-orthografisch mit Drehen, Zoom, Schwenken, Boss-Verfolgung | Um eine umschaltbare steile Draufsicht (Taste V) und eine Heldenverfolgung (Taste Y) erweitert. |

## 2. Neue und geänderte Dateien

**Neu**

| Datei | Inhalt |
|---|---|
| `src/config/heroEndless.ts` | Alle einstellbaren Werte: Wellentakt, EP-Kurve, Wiederbelebung, Beute-Chancen, Ressourcen, Wellenprofile, Perks, Meilensteine, Obergrenzen für Turmverstärkungen |
| `src/config/champions.ts` | 3 Helden (Lyra = Unterstützer/Turmverstärker, Brann = Kämpfer, Vela = Fernkämpferin) mit Grundwerten, Zuwachs je Stufe, je 4 Skills und datengetriebenem Skillbaum |
| `src/config/items.ts` | 6 Slots, 4 Seltenheiten, 25 Items, 8 Tränke, Rezepte, Verstärkungskosten, Ladenangebote, Verkaufswerte |
| `src/sim/ChampionSystem.ts` | Wertberechnung, Pathfinding (8 Richtungen, Türme blockieren nicht), Bewegung, Angriff, Skills, EP/Stufen, Skillbaum, Turmverstärkungen, Zonen, Tod und Wiederbelebung |
| `src/sim/LootSystem.ts` | Drops beim Tod, Besitzprüfung (keine identischen Drops), Aufsammeln im Radius, Fragmente, Ressourcen, Tränke, Item-Instanzen |
| `src/sim/InventorySystem.ts` | Ausrüsten und Ablegen, Spezialslot, Tränke, Laden (Kauf, Bestand, Sammlungsitems, Verkauf), atomare Herstellung, Verstärken |
| `src/sim/HeroEndlessSystem.ts` | Startzustand, Ladenplatzierung auf jeder Karte, automatischer Wellentakt, Wellenbelohnung, Ladenbestand |
| `src/ui/HeroHud.ts` | Heldenleiste, Skillleiste, Trankleiste, Ressourcen und Bestwelle in der Kopfzeile, Fenster für Laden/Schmiede und Charakterbogen |
| `src/ui/screens/heroSetup.ts` | Startbildschirm: Heldenwahl (mit Sperren), Startausrüstung, Karte, Fortschrittsübersicht |
| `tests/heroEndless.test.ts` | 21 Tests für alle Pflichtfälle aus Phase 5 |
| `scripts/heroBalance.ts` | Bot-Vergleich normaler Endlosmodus gegen Helden-Endlos für alle drei Helden |
| `docs/HERO_ENDLESS.md` | dieses Dokument |

**Geändert (nur Erweiterungen, keine Ersetzungen)**

`core/types.ts` (neue Typen, `GameMode` + `'heroEndless'`, `GameState.he`, `rng.loot`, `miniBoss`, neue Ereignisse, `PlayerProgress.heroMeta`) · `sim/GameSimulation.ts` (Befehle, Takt-Haken, Cache-Felder) · `sim/setup.ts` (`RunSetup.heroEndless`, Regeln wie Endlos, keine Lane-Helden) · `sim/DamageSystem.ts` (Beute in `killEnemy`) · `sim/TowerSystem.ts` (Turmverstärkung in der Wertberechnung) · `sim/WaveDirector.ts` (Profile, Mini-Bosse) · `save/SaveSystem.ts` (`heroMeta`-Standardwerte und -Validierung, Validierung von `he` beim Fortsetzen) · `app/progress.ts` (`commitHeroRun`) · `app/GameApplication.ts` (Bildschirm, Setup-Erzeugung, Verbuchen offener Läufe) · `app/GameSession.ts` (Eingabe, Zielwahl, HUD-Anbindung, Ereignisse) · `render/WorldView.ts` (Held, Laden, Beute, Zonen, Verstärkungsmarker, Effekte) · `render/Camera.ts` (Draufsicht) · `ui/Hud.ts` (Container, Countdown) · `ui/screens/menu.ts`, `results.ts`, `help.ts` · `ui/text.ts` · `styles.css` · `scripts/e2e.mjs` (+7 Schritte) · `README.md` · `package.json`.

## 3. Spielsysteme

### Wellentakt und Pause
- Nach dem Start läuft ein Countdown von **15 s** (`prepTime`). Danach startet Welle 1 automatisch. Nach jeder Welle folgt ein Übergang von **8 s** (`transitionTime`). Die Leertaste startet früher.
- Der Countdown steht auf der Startschaltfläche („Welle 4 · 6 s“). Während der Modifikatorauswahl hält er an.
- Pause: Alles läuft im Simulationstakt, also stehen Countdown, Held, Abklingzeiten, Beute-Lebensdauer, Gegner und Türme in der Pause still. Das ist per Test belegt.

### Schwierigkeit
- Die vorhandene Endlosskalierung bleibt (LP-Kurve, Budget, Bosse alle 10, Modifikatoren alle 4 Wellen).
- **Wellenprofile** ab Welle 3, deterministisch je Seed: Gemischt, Schwarm, Panzerzug, Luftangriff, Schatten (ab W9), Elitetrupp (ab W8), Ansturm. Sie verändern Gegnergewichte, Budget, Menge, Tempo und Zusatz-Eliten.
- **Mini-Bosse** ab Welle 7 alle 5 Wellen (7, 12, 17 …, nie in Bosswellen): ein Elitegegner mit ×4 LP und ×4 Beute, ab W10 mit Mutation.
- Der Schaden der Gegner am Helden wächst um 5 % pro Welle.

### Held
- **Bewegung:** Links- oder Rechtsklick auf ein Feld. Der Weg wird per Dijkstra auf dem Raster berechnet (8 Richtungen, keine Abkürzung über Hindernis-Ecken). Begehbar sind alle Felder außer Hindernissen und dem Ladengebäude, also auch die Gegnerbahn und Turmfelder. Der Held kann nichts zerstören.
- **Archetypen:** Lyra (Unterstützer, passive Turmaura und 4 Turmverstärkungs-Skills), Brann (Nahkämpfer mit Flächenschlag, Sturmangriff mit Betäubung, Klingensturm), Vela (Fernkampf, Durchschlag, Falle, Pfeilhagel; ab Bestwelle 15). Ein neuer Held ist ein weiterer Eintrag in `CHAMPIONS` mit Skills und Baum.
- **Attribute:** 19 Werte, darunter LP, Mana, Regeneration, Schaden, Tempo, Reichweite, Rüstung, Krit, Abklingzeitreduktion, Fähigkeitsstärke, Unterstützungsstärke, Auraradius, Beutebonus, Aufsammelradius, Lebensraub und Flächenschlag. Sie werden immer vollständig neu berechnet: Basis + Stufe + Skillbaum + Ausrüstung + Buffs + Perks, danach mit Obergrenzen (`STAT_LIMITS`).
- **EP und Stufen:** benötigte EP = `30 · L^1,45 + 20 · L`. Pro Stufe gibt es 1 Fertigkeitspunkt, alle 5 Stufen einen Bonuspunkt. Beim Aufstieg werden der Zuwachs der Maximalwerte und 25 % LP aufgefüllt. Rückmeldung: Lichtsäule, Banner, Klang.
- **Skillleiste:** Symbol, Name und Beschreibung (Tooltip), Abklingzeit als Füllstand und Sekunden, Manakosten, Taste (A/S/D/W), Klick. Zielarten: selbst, Turm, Bereich. Die Zielwahl zeigt Wirkradius und Reichweite, Esc oder Rechtsklick bricht ab, ein zweiter Tastendruck wirkt auf das Feld unter der Maus.
- **Skillbaum:** 11 Knoten je Held in 4 Stufen mit Rängen, Voraussetzungen (Knoten und Rang), Mindeststufe und Meisterknoten (Perk „Meisterschaft“).
- **Tod:** Wiederbelebung neben dem Kern nach `6 s + 0,5 s · Stufe`. Buffs gehen dabei verloren, Ausrüstung bleibt.

### Turmverstärkung
- Integriert in die vorhandene Turmwertberechnung (`computeTowerStats`). Werte: Schaden (%), Angriffstempo (%), Reichweite (Felder), Krit-Chance.
- **Regeln:**
  - Je Turm und Quelle gibt es genau einen Eintrag.
  - Erneutes Anwenden setzt die Dauer neu (maximal die Grunddauer, keine Verlängerung) und übernimmt jeweils den höheren Wert.
  - Verschiedene Quellen addieren sich mit der passiven Aura.
  - Die Summe ist gedeckelt (`towerBuffCap`: +120 % Schaden, +80 % Tempo, +1,5 Reichweite, +40 % Krit).
  - Abgelaufene Einträge und Einträge verkaufter Türme werden im nächsten Takt entfernt. Danach rechnen die Turmwerte neu (Signaturvergleich → `statsDirty`).
- **Sichtbar:** Ring am Turm, kreisende Runen bei aktiver Fähigkeit und Effekt beim Anwenden.

### Beute, Ressourcen, Inventare
- **EP-Kugeln** fallen bei jedem Kill. Der Held sammelt im Aufsammelradius mit leichtem Sog ein. Beim Einsammeln wird die Kugel sofort entfernt, eine doppelte Gutschrift ist ausgeschlossen (Test). Die Beute verschwindet nach einer Lebensdauer.
- **Ressourcen:** Metall, Kristall, Essenz und Relikt. Sie kommen als Drop je nach Gegnertyp und zusätzlich als Wellenbelohnung (genau einmal pro Welle), die mit der Welle wächst. Anzeige in der Kopfzeile.
- **Items und Fragmente:**
  - Die Dropchance hängt von der Stufe ab (normal, Elite, Mini-Boss, Boss) und vom Beutebonus.
  - Items haben eindeutige IDs. Jedes Item existiert in einer Partie höchstens einmal.
  - Besitzprüfung `ownedItems()` umfasst Sammlung, Freischaltungen der Partie, Inventar und Items am Boden.
  - Was schon besessen wird, fällt nie erneut identisch. Stattdessen gibt es Fragmente fehlender Items oder, wenn alles freigeschaltet ist, Essenz.
  - Fragmente bereits besessener Items werden zu Essenz.
- **Ausrüstung:** 6 Slots (Waffe, Rüstung, Helm, Artefakt, Accessoire, Spezial), 4 Seltenheiten, teils heldengebunden. Der Spezialslot nimmt alternativ einen Trankstapel als Schnellzugriff (Umschalt+5) auf. Beim An- und Ablegen behalten LP und Mana ihren Anteil.
- **Zwei Inventare:**
  - **Sammlung** (`heroMeta.unlockedItems`, dauerhaft).
  - **Heldeninventar** (`he.champion.inventory`, nur im Lauf, 18 Plätze).
  - Regel für die Mitnahme: Bis zu 0–3 Sammlungsitems kommen kostenlos in den Lauf (Rucksack-Perks bei Welle 8/18/30). Jedes weitere Sammlungsitem kann im Laden für Ressourcen gekauft werden, einmal pro Lauf.
- **Tränke:** 8 Sorten, stapelbar bis zur jeweiligen Obergrenze, höchstens 6 Stapel.
  - Nutzung über die Trankleiste, Umschalt+1–4 oder den Spezialslot (Umschalt+5).
  - Abklingzeit je Sorte plus 0,8 s global.
  - Zeitbuffs erneuern die Dauer und stapeln nicht. Prozentwerte beziehen sich auf die Werte ohne den eigenen Trank.
  - Das Turmelixier verstärkt Türme im Umkreis über dieselbe Turmverstärkungs-Logik.

### Laden und Schmiede
- Der Laden steht auf **jeder** Karte: ein Hindernisfeld nahe dem Kern mit begehbarem Nachbarn. Als Rückfall wird ein freies Feld umgewandelt, aber nur, wenn alle Wege offen bleiben. Getestet für Classic und Mazing, 3 Größen und mehrere Seeds.
- Kaufen, Verkaufen, Herstellen und Verstärken sind nur in Reichweite möglich (2,2 Felder). Ansehen geht jederzeit, mit einem Knopf „Zum Laden laufen“.
- **Kategorien:** Tränke, Ausrüstung (Sammlung), Fragmente, Materialien, Spezial; dazu Schmiede und Verkaufen.
- **Prüfungen:** Preis, Bestand (wird alle 5 Wellen aufgefüllt), Perk-Voraussetzung, Platz im Inventar. Rückmeldung über Ton, Hinweis und deaktivierte Knöpfe mit Grund.
- **Herstellung (atomar):** Erst werden alle Bedingungen geprüft (Perk, Welle, Fragmente, Zutaten, Kosten, Platz, Eindeutigkeit, Reichweite), erst danach wird verbraucht. Rezepte:
  - Fragment-Rezepte für jedes nicht nur herstellbare Item.
  - 3 legendäre Kombinationen (Perk „Meisterschmied“, ab Welle 15).
  - Verstärken +1 bis +3 (je +15 % Werte).

### Dauerhafter Fortschritt
- **Bestwelle:** Wird nach jeder überstandenen Welle in den Fortschritt geschrieben und mit gespeichert (spätestens beim nächsten Speichern). Bei Partieende wird sie erneut verbucht.
- **15 Perks** schalten über die Bestwelle frei, z. B. Sammlerhand (W3), Startbeutel (W5), Vorrat (W6/16), Rucksack (W8/18/30), Alchemie (W10), Meisterschmied (W12), Händlergilde (W14), Vela (W15), Meisterschaft (W20), Weisheit (W22), Kriegskasse (W25), Glückspilz (W35). Sie gelten ab dem nächsten Lauf.
- **Meilensteine** werden einmalig vergeben (`claimedMilestones`): W5, 10, 20, 30, 40, 50.
- **Verbuchung** (`commitHeroRun`):
  - Idempotent über die `runId`.
  - Läuft bei Partieende und Neustart. Ein fortsetzbarer Lauf, der durch einen neuen Start ersetzt wird, wird vorher verbucht.
  - Fragmente werden als Differenz zum Start verbucht und können nie negativ werden.
  - Neue Items kommen ohne Duplikate in die Sammlung.

### Kamera und HUD
- **Kamera:**
  - V schaltet zwischen Isometrie (35°) und steiler Draufsicht (62°) um, mit weichem Übergang.
  - Y lässt die Kamera dem Helden folgen.
  - Ein Klick auf das Porträt zentriert die Kamera.
  - Drehen, Zoom, Minikarte und Boss-Verfolgung bleiben wie bisher.
- **HUD:**
  - Kopfzeile: Gold, Kernenergie, Welle, 4 Ressourcen, Bestwelle, nächstes Wellenprofil; Countdown auf der Startschaltfläche.
  - Heldenleiste: Porträt mit Stufe, LP/Mana/EP-Balken mit Zahlen, Buff-Chips mit Restzeit, Skillleiste, Trankleiste und die Knöpfe Held (Tab), Laden (X), Folgen (Y), Draufsicht (V).
  - In der Welt: Balken über dem Helden, Sternmarker bei freien Punkten, Laufweg, Ziel, Aura- und Aufsammelradius.

## 4. Steuerung im Modus

| Eingabe | Wirkung |
|---|---|
| Linksklick freies Feld / Rechtsklick | Held läuft dorthin |
| Linksklick Turm | Turm auswählen (Standardverhalten) |
| Linksklick Laden | Held läuft zum Laden, Ladenfenster öffnet |
| A / S / D / W | Skills 1–4 (Ziel-Skills: danach Ziel anklicken) |
| Umschalt + 1–4 / 5 | Trank aus Schnellleiste / Spezialslot |
| Tab | Charakterbogen |
| X | Laden & Schmiede |
| Y | Kamera folgt dem Helden |
| V | Draufsicht umschalten |
| Esc | Zielwahl abbrechen → Fenster schließen → Pause |

Alle bisherigen Tasten (Türme, Tempo, Pause, Drehen …) funktionieren unverändert. A/S/D/X/Y/V sind im normalen Spiel nur mit Lane-Helden belegt, die es in diesem Modus nicht gibt. Es gibt also keine Konflikte.

## 5. Einstellbare Werte (Auswahl)

| Wert | Ort | Standard |
|---|---|---|
| Vorbereitungszeit / Übergangszeit | `HERO_ENDLESS.prepTime` / `transitionTime` | 15 s / 8 s |
| EP-Kurve | `xpBase`, `xpExp`, `xpLinear` | 30, 1,45, 20 |
| Punkte je Stufe / Bonus | `pointsPerLevel`, `bonusPointEvery` | 1 / alle 5 |
| Wiederbelebung | `respawnBase`, `respawnPerLevel` | 6 s + 0,5 s je Stufe |
| Gegnerschaden am Helden | `enemyDamagePerWave` | +5 %/Welle |
| Lebensdauer der Beute | `pickupLife` | 40–90 s |
| Ladenradius / Bestand | `shopRange`, `stockEvery` | 2,2 Felder / 5 Wellen |
| Inventar / Trankstapel / globale Trank-Abklingzeit | `inventorySlots`, `potionStacks`, `potionGlobalCd` | 12 (+6 Ausrüstung) / 6 / 0,8 s |
| Mini-Bosse | `miniBossFrom`, `miniBossEvery`, `miniBossHpMul` | ab W7, alle 5, ×4 LP |
| Obergrenzen Turmverstärkung | `towerBuffCap` | +120 % / +80 % / +1,5 / +40 % |
| Wellenressourcen | `waveResources(w)` | Metall 2+w/3, Kristall 1+w/5, Essenz alle 5, Relikt alle 10 |
| EP und Dropchancen | `LOOT` (`xpPerThreat`, `resourceChance`, `potionChance`, `fragmentChance`, `itemChance`) | je Stufe normal/Elite/Mini-Boss/Boss |
| Profile, Perks, Meilensteine | `WAVE_PROFILES`, `PERKS`, `MILESTONES` | siehe Datei |
| Items, Tränke, Rezepte, Laden | `src/config/items.ts` | 25 Items, 8 Tränke, 3 Kombinationen |
| Helden, Skills, Skillbäume | `src/config/champions.ts` | 3 Helden, je 4 Skills, 11 Knoten |

## 6. Tests – tatsächlich ausgeführt

**Unit/Integration (`npm test`): 102 von 102 bestanden**, davon 21 neu in `tests/heroEndless.test.ts`. Die Pflichtfälle aus Phase 5 und ihre Abdeckung:

| Pflichtfall | Test |
|---|---|
| Modus startet unabhängig | Start ohne Lane-Helden, eigener Zustand, Schnellspiel hat `he = null` |
| Bewegung auf normalen Feldern und auf der Lane | Lauf auf eine Wegzelle bis ins Ziel |
| Türme blockieren nicht | Lauf auf ein Turmfeld (Classic) und Turmfelder im Mazing begehbar; Hindernisse nicht |
| Erste Welle nach 15 s | bei 14,5 s noch Welle 0, bei 15,2 s Welle 1; Folgewelle nach 8 s |
| Pause stoppt alles | `advance()` bewegt in der Pause weder Countdown noch Abklingzeit noch Gegner |
| Erfahrung korrekt | EP-Drop → Einsammeln → genau einmal gutgeschrieben, zweiter Kill desselben Gegners ohne Beute |
| Stufenaufstieg erhöht Attribute | LP und Schaden steigen, Skillpunkt, wachsende Kurve, Baum-Voraussetzungen |
| Skills und Abklingzeit | Manaverbrauch, exakte Abklingzeit, Sperre bis Ablauf, Stufensperre, Manamangel |
| Tower-Buffs anwenden und entfernen | Schaden steigt, kein Stapeln derselben Quelle, Ablauf stellt Werte wieder her, Verkauf entfernt |
| Kämpfer greift an | Nahkampfschaden an einem stehenden Gegner |
| Fragmente → Item | zu wenig = Ablehnung, genug = Item; Fragmente und Kristall exakt verbraucht |
| Keine identischen Drops | 400 Kills mit fast vollständiger Sammlung: kein Drop eines besessenen Items |
| Ausrüstung verändert Werte | exakter Schadenszuwachs, falscher Slot und falscher Held werden abgelehnt |
| Tränke stapeln und nutzen | Stapel, Heilung, Abklingzeit, Spezialslot, kein Aufschaukeln des Krafttranks |
| Crafting verbraucht korrekt | siehe oben; fehlgeschlagene Herstellung verbraucht nichts; nur am Laden |
| Käufe ohne Ressourcen verhindert | Gold bleibt unverändert, Reichweite, Bestand, Perk |
| Laden auf jeder Karte | Classic/Mazing × Normal/Groß/Riesig × 3 Seeds: vorhanden und zu Fuß erreichbar |
| Freischaltungen gespeichert | Sammlung, Fragmente, Bestwelle, Meilensteine; Speichern/Laden identisch; zweiter Abschluss ändert nichts |
| Laden ohne Duplikate | Fortsetzen erhält Inventar exakt; manipuliertes Duplikat wird bereinigt; Abschluss nur einmal |
| Bestehende Modi | Schnellspiel und Endlos laufen ohne Fehler und ohne `he`; alle 81 alten Tests weiter grün |
| zusätzlich | Wellenbelohnung genau einmal, Mini-Boss ab Welle 7, Vela-Sperre, 90-s-Selbstlauf aller drei Helden ohne Fehler |

**Browser-E2E (`npm run e2e`) gegen den Produktionsbuild: 33 von 33 bestanden**, davon 7 neu:

- Startbildschirm mit Sperre von Vela, Heldenwahl, Lauf startet mit Countdown, Skillleiste und Ressourcen.
- Rechtsklick bewegt den Helden über die Lane.
- Skill per Taste sowie Ziel-Skill per Taste und Klick, nach Stufenaufstieg durch EP-Beute.
- Laden: Kauf gesperrt ohne Gold, Kauf mit Gold, danach Heiltrank per Umschalt+1.
- Schmiede stellt aus Fragmenten ein Item her, Anlegen im Charakterbogen, Skillpunkt ausgeben.
- Pause friert Takt und Countdown ein, danach startet die Welle automatisch.
- Laufende Freischaltungen sind nach Laufende und Neuladen dauerhaft und ohne Duplikat gespeichert.

**Bot-Balance** (`node --import tsx scripts/heroBalance.ts 6 45`, Karte Groß, Normal, Bot „Ausgewogen“, Held mit einfacher Automatik, bis höchstens Welle 45):

| Seed | Endlos (ohne Held) | Lyra | Brann | Vela |
|---|---|---|---|---|
| HB-0 | 17 | 45 | 32 | 32 |
| HB-1 | 25 | 20 | 20 | 45 |
| HB-2 | 45 | 16 | 16 | 45 |
| HB-3 | 45 | 45 | 45 | 45 |
| HB-4 | 17 | 37 | 27 | 27 |
| HB-5 | 14 | 45 | 21 | 45 |

Auf dieser Grundlage wurden die Mini-Bosse entschärft (vorher ab W5 mit ×6 LP, was bei einem Bot in W5 8 Kernenergie kostete) und die Obergrenzen der Turmverstärkung gesenkt (vorher +150 %/+100 %). Die Streuung ist hoch, weil Bot und Helden-Automatik einfach sind. Eine Kalibrierung mit menschlichen Spielern steht aus.

## 7. Bekannte Einschränkungen

- **Grafikleistung** des neuen Modus auf echter GPU wurde nicht gemessen (nur Software-WebGL in der Testumgebung). Der zusätzliche Aufwand ist gering: ein Held, Beute als Sprites, ein Ladengebäude.
- **Fortsetzen** nur ab Bauphasen. Wird mitten in einer Welle ins Menü gewechselt, geht der Fortschritt dieser Welle verloren, wie in den anderen Modi. Was bis zur letzten Bauphase gefunden wurde, bleibt im Fortsetzungsstand und wird spätestens beim nächsten Laufstart verbucht.
- **Keine eigene Lauf- oder Kampfanimation** für den Helden über das bestehende Modellsystem hinaus. Er nutzt die Modelle der Lane-Helden (Lyra = Mystikerin, Brann = Klinge, Vela = Jägerin) mit eigenen Farben.
- Gegner greifen den Helden über **Kontaktschaden** an (Nähe), nicht über eigene Zielwahl. Gegner bleiben auf ihrem Weg zum Kern.
- Item-Symbole sind **Emojis**. Seltenheit wird über Farbe und Rahmen gezeigt.
- **Touch:** Bewegung per Antippen funktioniert, Rechtsklick gibt es auf Touch nicht. Die Skill-Zielwahl per Antippen ist implementiert, aber nicht per E2E auf dem Smartphone getestet.
- **Balance** nur per Bot kalibriert, siehe oben.

## 8. Offene Aufgaben / Vorschläge

1. Playtests: EP-Kurve, Dropchancen und Ladenpreise mit Menschen kalibrieren.
2. Eigene Heldenmodelle und Animationen, Item-Symbole aus dem IconFactory-Renderer.
3. Gegner mit Aggro auf den Helden (z. B. Elite verlässt kurz den Weg).
4. E2E-Schritte für Touch (Ziel-Skills per Antippen).
5. Weitere Helden, Items und Profile rein über `src/config/` ergänzen. Der Code muss dafür nicht angepasst werden, außer bei neuen Skill-Effekten in `useChampionSkill`.

## 9. Starten und Testen

```bash
npm install
npm run dev                 # http://localhost:5173/ → Hauptmenü → „Helden-Endlos“
npm test                    # 102 Unit-/Integrationstests
npm run build && npm run e2e   # 33 Browser-Prüfungen
node --import tsx scripts/heroBalance.ts 6 45   # Bot-Vergleich
```

Manuell testen: Im Menü „Helden-Endlos“ wählen, einen Helden auswählen und den Lauf starten. Während der 15 s Vorbereitung Türme bauen und den Helden per Rechtsklick bewegen. Mit Tab den Charakterbogen und mit X den Laden öffnen; das geht nur nahe dem Gebäude mit dem roten Vordach neben dem Kern.

## 10. Update 2.2 (17.10.2026)

| Wunsch | Umsetzung | Getestet |
|---|---|---|
| Nächste Welle per Knopfdruck | Startknopf und `Enter`: in der Bauphase sofort; im Kampf vorzeitig, sobald die laufende Welle vollständig erschienen ist (nicht im Bosskampf). Die laufende Welle zählt dann als überstanden (Gold- und Ressourcenbelohnung genau einmal), Restgegner bleiben auf dem Feld, +4 Gold je verbliebenem Gegner. `callNextWave` in `WaveDirector.ts`. | Unit-Test + E2E |
| Türme besser anklickbar | `WorldView.pickTower`: Abstand des Klicks zur Bildschirm-Strecke Sockel → Turmspitze (`TOWER_TOP`), bei Überlappung gewinnt der vordere Turm. Gilt in allen Modi und auch für Turm-Ziel-Fähigkeiten. | E2E (Klick auf die Turmspitze) |
| Fenster mit allen Türmen | „Türme“-Knopf oben bzw. Taste `O`: Liste aller Türme (sortierbar nach Typ, Stufe, Schaden) mit Werten, Abschüssen, Gesamtschaden, Aufwerten, aktiver Fähigkeit und „Info“ (öffnet Details inkl. Spezialisierung und fährt die Kamera hin). | E2E (Aufwerten im Fenster) |
| Drehung fixieren | Schloss-Knopf in der Kameraleiste; gesperrt drehen weder Q/E noch die Drehknöpfe. Wird in den Einstellungen gespeichert (`lockRotation`). | Unit-Test (Speichern) + E2E |
| Gegner anklickbar | `WorldView.pickEnemy` (Körper statt Bodenfeld). Panel mit Live-Werten: LP/Schild, aktuelles Tempo inkl. Verlangsamung, Kernschaden, Kopfgeld, Schaden am Helden, effektive Resistenzen, aktive Effekte mit Stapeln/Restzeit, Fähigkeiten mit Abklingzeit, Mutation, Elite/Mini-Boss/Bossphase, Konter. Roter Auswahlring am Gegner. | E2E (Klick zeigt LP) |

Einschränkung: Getarnte Gegner sind erst anklickbar, wenn sie aufgedeckt sind. Auf Touchgeräten ist das Antippen kleiner, schneller Gegner naturgemäß schwieriger (Trefferradius mindestens 16 px).

## 11. Update 2.3 (17.10.2026)

| Wunsch | Umsetzung | Getestet |
|---|---|---|
| Türme nach und nach freischalten | Nur im Helden-Endlos (`TOWER_UNLOCKS` in `src/config/passiveTree.ts`): W1 Waldläufer, Glutwerfer, Frostwächter, Goldgräber (+Barrikade im Mazing) · W3 Sturmrufer, Kanonenschmiede · W5 Sporenwerfer, Belagerungsarmbrust · W7 Runenweber, Resonator · W9 Leitpylon, Totemschrein · W11 Rissbinder, Tesla-Spule · W14 Gravitationsbrunnen, Meteorbeschwörer · W17 Kristalllaser, Drachenhorst. Maßgeblich ist die nächste anstehende Welle. Gesperrte Türme sind in der Leiste ausgegraut mit „W5“ usw., beim Freischalten erscheint ein Hinweis. | Unit (Bausperre, Freischaltung, Konter, Fusionspartner) + E2E |
| Kombinationen bleiben möglich | Luftabwehr ab W1 (vor Fliegern W5), Detektoren ab W7 (vor Schattenschleichern W9), Elementreaktionen ab W1/W3 (Feuer+Frost, Blitz ab W3), Fusionspartner paarweise: Glut+Sturm (W3), Frost+Kanone (W3), Armbrust+Runenweber (W7), Meteor+Laser (W17). | Unit |
| Mini-Pause mit Aufwerten | Knopf „Mini“ oder Leertaste im Kampf: Simulation steht, kein Pausenmenü; Bauen, Aufwerten, Turmübersicht, Laden und Heldenfähigkeiten bleiben bedienbar. Hinweisband oben. P/Esc wechselt ins Pausenmenü. Gilt in allen Modi. | E2E |
| Held an Position festsetzen | Taste Z oder Anker-Knopf: Laufbefehle (Links-/Rechtsklick) werden ignoriert, Nahkämpfer verfolgen keine Gegner mehr; Fähigkeiten wirken weiter. Zustand wird mitgespeichert (`champion.holdPos`). | Unit + E2E |
| Flieger laufen den Weg entlang | Flieger folgen demselben Distanzfeld wie Bodengegner (auch im Mazing-Labyrinth) und werden vom Rissbinder entlang des Wegs zurückversetzt. Ausgleich, weil der Weg länger als die Luftlinie ist: Flieger 64 statt 40 LP und Tempo 1,75 statt 1,4; Sturmgreif 125 statt 80 LP und 2,2 statt 1,85. Gilt in allen Modi. | Unit (Flieger verlässt den Weg nicht) |
| Passiver Baum | `src/config/passiveTree.ts`: 1 Punkt je 10 Bestwellen, höchstens 100 (Bestwelle 1000). 21 Knoten in 6 Stufen (Freischaltung nach 0/5/15/30/50/75 vergebenen Punkten), genau 100 Ränge. Wirkt automatisch: Helden-LP/-Schaden/-Rüstung/Krit/Abklingzeit/Regeneration, EP, Turmschaden/-tempo/-reichweite, Startgold, Kopfgeld, Ressourcen, Beutechance, Kernenergie, schnellere Wiederbelebung, frühere Turmfreischaltung. Voll ausgebaut z. B. +22 % Turmschaden, +27 % Helden-LP. Vergabe im Startbildschirm, kostenloses Zurücksetzen; im Lauf als Tab „Passiv“ (Stand bei Laufbeginn). Gespeichert in `heroMeta.passive`, beim Laden bereinigt. | Unit (Punkte, Stufen, 100 Ränge, Wirkung im Lauf, Bereinigung) + E2E |
| Turmfähigkeiten automatisch | Ab Stufe 2 (legendäre Türme immer) lösen Fähigkeiten im Kampf selbst aus, sobald die Abklingzeit abgelaufen ist und ein Gegner in Reichweite ist (Goldrausch und Netzflut in jeder Welle). C bzw. der Knopf funktionieren weiterhin. Gilt in allen Modi. | Unit (Auslösen und erneutes Auslösen) |

**Bot-Balance nach dem Update** (`scripts/heroBalance.ts 4 70`): Endlos ohne Held W34–70, Helden-Endlos alle drei Helden W62–70 (Obergrenze 70). Durch automatische Turmfähigkeiten und den längeren Flugweg ist das Spiel leichter geworden; die Flieger wurden deshalb verstärkt. Bestwellen jenseits von 100 sind für den passiven Baum gedacht, aber nicht per Bot erprobt. Normaler Modus (Bot, 2 Seeds): 46 % Siege auf Normal, gleich wie vorher.

Behoben: Der Bot versuchte legendäre Türme zu spezialisieren (Absturz in langen Läufen); `specCost` ist jetzt abgesichert.

## 12. Update 2.4 (17.10.2026)

- **Heldenleiste rund 20 % größer:** `zoom: 1.2`, auf kleinen Bildschirmen 1.05. Charakterbogen und Laden sind breiter (864 statt 720 px) und haben eine größere Schrift.
- **Fusionsanzeige:** Das Turmpanel zeigt für jedes Rezept, an dem der Turm beteiligt ist, eine Checkliste:
  - der eigene Turm: Stufe 4 und spezialisiert
  - der Partner: vorhanden, nah genug (höchstens 2,3 Felder), Stufe 4 und spezialisiert; im Helden-Endlos auch die Welle, ab der er freigeschaltet wird
  - die Goldkosten
  - Ist alles erfüllt, erscheint „Verschmelzen“.
- **Kein Turmverkauf in der Mini-Pause:** Der Verkaufsknopf ist gesperrt, auch die Entf-Taste und direkte Aufrufe werden abgewiesen. Bauen, Aufwerten und Fusionieren bleiben erlaubt. Getestet per E2E.

## 13. Update 2.5 (17.10.2026)

- **Fusionen:** Es gibt sechs neue Rezepte, insgesamt jetzt 10. Im Helden-Endlos sind alle legendären Türme ab Welle 0 erlaubt; es zählt nur die Freischaltung der Partner.

  | Ergebnis | Partner | Gold |
  |---|---|---|
  | Dornenhain | Waldläuferposten + Sporenwerfer | 340 |
  | Donnerspitze | Tesla-Spule + Leitpylon | 420 |
  | Rissportal | Rissbinder + Gravitationsbrunnen | 440 |
  | Phönixhorst | Drachenhorst + Glutwerfer | 460 |
  | Klangkathedrale | Resonator + Totemschrein | 400 |
  | Sturmauge | Sturmrufer + Frostwächter | 380 |

  Beim Platzieren zeigt die Bauleiste die möglichen Fusionen mit Partnern in Reichweite (`fusionPreview`). Ein goldener Ring mit 2,3 Feldern Radius markiert das Feld, und passende Partner leuchten auf.
- **Kampagnenhelden:** Neun Helden sind neu (`CHAMPION_ORDER`, jetzt 12). Ihre Fähigkeiten sind datengetrieben: `effects`, ausgeführt von `applySkillEffects` (nova, line, blink, execute, towers, allTowers, self, strikes, statusAll, gold, taunt, interrupt, zone). Jeder bekommt einen eigenen Baum mit 11 Knoten aus `campaignTree`.

  | Held | Frei ab |
  |---|---|
  | Kaela | sofort |
  | Liora | sofort |
  | Thorgrim | sofort |
  | Varo | Bestwelle 10 |
  | Sena | Bestwelle 15 |
  | Mira | Bestwelle 20 |
  | Nyx | Bestwelle 25 |
  | Oran | Bestwelle 30 |
  | Ommar | Bestwelle 40 |

  Alternativ schaltet der passende Kampagnenheld frei.
- **Kernwall:** In jedem Baum, Stufe 1, 5 Ränge. Jeder Rang gibt +3 auf `lives` und `maxLives`, sofort.
- **Brann:**
  - `blockCount` 2, 820 LP, 18 % Rüstung, 35 % Spaltschaden, 4 % Lebensraub.
  - „Herausforderung“ verwurzelt alle Gegner im Umkreis von 3 Feldern für 2 s.
  - Wirbel, Ansturm und Klingensturm sind stärker; der Klingensturm verlangsamt.
  - Steht der Held still, hält er bis zu `blockCount` Gegner (`blockedBy`).
- **Abbau (`MiningSystem`):**
  - Dauer: mittlere Wellendauer, mindestens 25 s, höchstens 60 s, anfangs 35 s.
  - Nicht unterbrechbar: Bewegung, Fähigkeiten und ein zweiter Abbau werden abgelehnt. Der Held regeneriert Mana, sammelt Beute ein und kämpft nicht.
  - Ausbeute, zufällig:
    - Gold: (20 + 4 × Welle) × 0,5–1,5
    - Metall: 75 %
    - Kristall: 35 %, bei Kristall- und Eis-Dekor 70 %
    - Essenz: 15 %
    - Relikt: 4 %
  - Danach wird das Feld `Open`; `map.rev` lässt das Gelände neu aufbauen.
  - Lava, Wolken und der Laden lassen sich nicht abbauen.
- **Spezialfelder:**
  - Goldader: +50 % und zusätzlich `veinFlat` 8 + 1,5 × Welle.
  - Kristallvorkommen (`map.deposits`): 2–4 je Karte, deterministisch aus dem Seed. Ein Goldgräber darauf gibt je Welle zusätzlich Kristall.
- **Optik:**
  - Additive Sprites haben nur noch die halbe Alpha.
  - Die Emission wird begrenzt, die Bloom-Schwelle liegt bei 0,86 statt 0,72, die Bloom-Stärke bei 0,38 statt 0,85.
  - Alle Gegner-, Boss- und Heldenmodelle sind neu und detaillierter (Gesichter, Rüstung, Umhänge, Hörner) und glühen kaum noch.
- **Tastenbelegung (`config/keys.ts`):**
  - Jede Aktion hat eine Standardtaste und einen Kontext (alle, Lane-Helden, Helden-Endlos).
  - `PlayerSettings.keys` speichert nur Abweichungen.
  - `GameSession.key` übersetzt die gedrückte Taste auf die Standardtaste der gebundenen Aktion, so bleibt die Tastenlogik unverändert.
  - Bei Konflikten werden die Tasten getauscht.
  - Fest belegt bleiben Esc und Umschalt+1–5.
- **Tests:** `tests/v25.test.ts` deckt Folgendes ab:
  - alle 10 Fusionen und die Fusionsvorschau
  - die vier Fähigkeiten aller 12 Helden
  - Freischaltung, Brann-Blocken und Kernwall
  - Abbau: Dauer, Unterbrechung, Ertrag, Feld frei und bebaubar
  - Goldader und Vorkommen
  - Tastenbelegung

  Dazu kommen E2E-Schritte für Kampagnenheld, Abbau-Panel, Fusionshinweis und Tastenbelegung.

## 14. Update 2.6 (17.10.2026)

- **Goldgräber 2×2:** `TowerDefinition.size`.
  - Hilfsfunktionen in `config/towers.ts`: `towerSize`, `footprintCells`, `towerCenter`.
  - `reindex` belegt alle Felder in `towerByCell` und, im Mazing, in `blocked`.
  - `canBuildHere` prüft: alle Felder frei, bebaubar und gleich hoch, innerhalb der Karte; im Mazing die Wegprüfung mit allen vier Feldern.
  - Goldader, Kristallvorkommen und Ertrag (`onVein`/`onDeposit`) zählen, wenn eines der vier Felder darauf liegt.
  - Darstellung in der Mitte der Grundfläche, ×1,75 skaliert; Auswahl- und Vorschau-Rahmen 2×2.
- **Alles aufwerten:** `upgradeAllTowers(sim, uids?)` wertet immer die günstigste bezahlbare Stufe auf, bis kein Gold mehr reicht oder alles auf Stufe 4 ist; nie eine Spezialisierung. `upgradeAllCost` zeigt die Gesamtkosten. Aufrufbar über:
  - den Knopf „Alle ▲“ in der Kopfleiste
  - die Turmübersicht
  - die Gruppenauswahl
- **Gruppenauswahl:** Ein Doppelklick bzw. Doppeltipp (< 420 ms) auf denselben Turm setzt `groupDef`. Alle Türme der Sorte sind gold markiert, und das Panel zeigt „▦ N× … ausgewählt“ mit „Alle aufwerten“ und „Nur diesen“.
- **Kristalllaser:** `laserPenalty` in `TowerSystem` (Werte in `LASER`).
  - Grundschaden 2,8 statt 3,5, Upgrade +1,2 statt +1,5.
  - −20 % je weiterem Laser in 3 Feldern, höchstens −60 %; zusätzlich ×0,65 in höchstens 3 Feldern zu einem Riss.
  - Drei gestapelte Laser am Riss machen damit insgesamt nur noch rund 30 % des früheren Schadens je Turm.
- **Turmübersicht:** Die Scrollposition bleibt beim Neuzeichnen erhalten; vorher sprang die Liste bei jeder Goldänderung nach oben. Voll ausgebaute Türme (Stufe 4 und spezialisiert bzw. nicht spezialisierbar) sind standardmäßig ausgeblendet.
- **Tests:**
  - `tests/v26.test.ts`: Alles aufwerten, Gruppen-Aufwerten, Laser-Dämpfung.
  - `economy.test.ts`: Goldgräber 2×2 (Belegung, Überlappung, Verkauf, Kartenrand).
  - E2E-Schritt 2.6: Doppelklick, Gruppen-Aufwerten, Alles aufwerten, Scrollposition, Ausblenden, 2×2.

## 15. Update 2.7 (17.10.2026)

Entscheidungen auf Rückfrage: Runen ab Welle 250, Paragon gemeinsam für alle Helden, Heldenstufen dauerhaft.

- **Dauerhafte Heldenstufen** (`HeroMetaProgress.champions[id] = {level, xp, tree}`):
  - Beim Start übernimmt `initHeroEndless` Stufe, EP und Skillbaum. Freie Punkte = `skillPointsForLevel(level)` − vergebene Ränge; Kernwall-Ränge wirken sofort auf die Kernenergie.
  - Höchststufe 99. Die Grundwerte wachsen bis Stufe 30 voll, danach halb so schnell (`effectiveLevelSteps`).
  - `commitHeroProgress` sichert nach jeder Welle, bei Paragon-Änderungen und beim Laufende absolute Werte; ein älterer Stand überschreibt nie einen neueren (Vergleich über die Gesamt-EP).
- **Paragon** (`config/paragon.ts`, gemeinsam für alle Helden, `HeroMetaProgress.paragon`):
  - Ab Stufe 99 fließen alle EP in Paragon-EP: 20 000 × (1 + P/250) je Stufe, höchstens Paragon 500.
  - Je Stufe 1 Punkt für einen der drei Bäume:

    | Baum | Knoten |
    |---|---|
    | Offensive | Wucht, Präzision, Hast, Zauberkraft, Turmmacht |
    | Defensive | Vitalität, Panzerung, Regeneration, Lebensraub, Bollwerk (+1 Kernenergie, 10 Ränge) |
    | Utility | Gier, Beutejäger, Fokus, Eile, Magnetismus, Turmtakt |

  - 620 Ränge, also mehr als die 500 Punkte.
  - Die Wirkungen laufen über `runPassive` (passiver Baum + Paragon, zwischengespeichert mit `paragon.rev`).
  - Punkte werden im Charakterbogen unter „Paragon“ vergeben und lassen sich in der Bauphase kostenlos zurücksetzen.
  - `sanitizeParagon` kürzt die Punkte auf die vorhandene Stufe.
- **Runen** (`config/runes.ts`, `sim/RuneSystem.ts`):
  - Fallen nur ab Welle 250: normal 0,15 %, Elite 4 %, Mini-Boss 25 %, Boss 60 %, zusätzlich 12 % je abgeschlossener Welle. Unter Welle 250 wird kein Zufallswert verbraucht, die Determinismus-Tests bleiben unverändert.
  - Qualitäten: Splitter ×1, Rune ×1,8, Uralte Rune ×3; Uralte Runen werden mit höherer Welle häufiger.
  - Arten: Kraft, Hast, Weite, Präzision, Bruch, Frost, Gift, Gier.
  - Sockel: Stufe 2 → 1, Stufe 4 → 2, legendär → 3; nicht bei Goldgräber und Barrikade.
  - Die Wirkung kommt nach Stufen und Spezialisierung in `computeTowerStats`; die Gierrune zahlt Gold beim Abschuss durch den Turm.
  - Verkauf und Fusion geben Runen in den Beutel zurück (höchstens 40 ungesockelte, bei vollem Beutel ersetzt eine bessere die schwächste).
  - Runen gelten nur für den laufenden Lauf.
- **Versionsanzeige:** `v<Version>` rechts unten im Spiel, im Pausenmenü und im Hauptmenü.
- **Freie Kamera:**
  - `IsoCamera.freeYaw` und `pitchAdj`, Neigung 17°–80°.
  - Eingabe über `InputController.orbit`: mittlere Maustaste oder Strg/Alt + Ziehen, Zwei-Finger-Drehung.
  - Tasten (frei belegbar unter „Kamera“): `,`/`.` drehen um ±15°, `Bild↑`/`Bild↓` neigen, `Pos1` setzt zurück.
  - Die Drehsperre gilt auch für die freie Drehung.

## 16. Update 2.8 (17.10.2026)

- **Minenwerfer** (`minelayer`, Angriffsart `mines`, Taste `#`, 130 Gold, Helden-Endlos ab Welle 6):
  - Wurf, solange Gegner unterwegs sind: alle 1,6 s auf ein freies Lane-Feld in Reichweite (Classic: Weg; Mazing: begehbare, offene Felder).
  - Vorzug für Felder nahe anrückender Bodengegner, mindestens 0,8 Felder Abstand zu anderen Minen.
  - Wurfbahn 0,55 s, danach liegt die Mine scharf, Zone `mine`.
  - Höchstens `mineMax` Minen je Turm: 6, mit „Großes Magazin“ 9, mit Streuminen +5.
  - Auslöser: ein Bodengegner in 0,5 Feldern (Flieger und eingegrabene Gegner nicht). Explosion mit `splash`-Radius und vollem Turm-Payload (Gift, Betäubung, Sprödigkeit, Panzerbrechung).
  - Minen bleiben über das Wellenende liegen (`WaveDirector` behält scharfe Minen) und verfallen nach 60 s. Bei verkauftem Turm explodieren sie mit der gespeicherten Ladung.
- **Sporenminenfeld** (`sporemine`, Fusion aus Minenwerfer und Sporenwerfer, 360 Gold):
  - 70 Giftschaden, Explosionsradius 1,3, bis zu 12 Minen.
  - Gift 14/s je Stapel, bis zu 8 Stapel, 15 % Verlangsamung.
  - Jede Explosion hinterlässt 4 s eine Sporenwolke (Radius 1,3), die weiter Giftstapel aufträgt.
- **Darstellung:**
  - Mörser-Modell mit Ersatzminen.
  - Sichtbarer Wurfbogen.
  - Scharfe Minen blinken, rot bzw. grün bei Sporenminen.
  - Rauch beim Abschuss, Explosion als Druckwelle.
- **Tests:** `tests/v28.test.ts`; Inhaltszählung jetzt 19 Türme, 11 legendäre, 11 Fusionen.

## 17. Update 2.9 (18.10.2026)

Entscheidung auf Rückfrage: „größere Felder“ heißt mehr Platz pro Feld, nicht breitere Lanes.

- **Mehr Platz pro Feld:** `UNIT = 0.8` in `WorldView`.
  - Alle Modelle von Türmen, Gegnern, Bossen, Lane-Helden, Champion und Vorschau sind 20 % kleiner, die Felder bleiben gleich groß.
  - Lebensbalken, Abschusshöhen und die Trefferfläche der Turmauswahl sind angepasst.
  - Startzoom: 62 % der Ganzkarten-Ansicht, im Helden-Endlos auf den Helden zentriert. Der Knopf „Ganze Karte“ zeigt weiterhin alles.
- **Paragon-Baum „Türme“** (4. Baum), nur Turm-Wirkungen:

  | Knoten | Wirkung je Rang | Ränge |
  |---|---|---|
  | Turmmacht | +0,4 % Schaden | 50 |
  | Turmtakt | +0,3 % Tempo | 40 |
  | Weitblick | +0,01 Reichweite | 30 |
  | Zielwasser | +0,2 % kritische Chance | 40 |
  | Durchschlag | +0,3 % Panzerbrechung | 30 |
  | Sprengkraft | +0,4 % Flächenradius | 30 |
  | Baumeister | −0,4 % Baukosten (höchstens −10 %) | 25 |
  | Goldader | +0,5 % Goldgräber-Ertrag | 30 |

  - Turmmacht und Turmtakt behalten ihre IDs, vergebene Punkte bleiben also erhalten.
  - Neue Kennzahlen in `PassiveEffects`: `towerCrit`, `towerPierce`, `towerCostPct`, `mineIncomePct`, `towerSplashPct`.
  - Insgesamt 4 Bäume, 22 Knoten, 805 Ränge.

## 18. Update 2.9.1 (18.10.2026)

- **Hindernisse über das ganze Modell anklickbar:** `WorldView.pickObstacle` prüft je Dekor-Hindernis (ohne Lava und Wolken) den senkrechten Bildschirmabschnitt vom Boden bis zur Modellhöhe: Baum/Ruine 1,3, Fels 0,7, sonst 0,95. Der Radius beträgt 0,42 Felder in Pixeln. Im Helden-Endlos wird das vor der feldbasierten Auswahl ausgewertet; Laden und Türme haben Vorrang.
- **Reichweite unverändert:** `UNIT` (Modellmaßstab) liegt nur in der Darstellung. Simulation und Werte nutzen weiter Felder. Neuer Test: Die Reichweite eines Waldläufers auf ebenem Feld entspricht genau dem Grundwert.

## 19. Update 2.9.2 (18.10.2026)

- **Alle verkaufen:**
  - `sellAllTowers(sim, uids?)` verkauft über `sellTower`; Erstattung wie beim Einzelverkauf, Runen kehren in den Beutel zurück.
  - `sellAllValue` liefert die Erlösvorschau.
  - Bedienung: Knopf in der Turmübersicht für alle Türme und in der Gruppenauswahl für eine Sorte, mit zweistufiger Bestätigung innerhalb von 4 s.
  - In der Mini-Pause gesperrt, wie der Einzelverkauf.
- Die Kopfleiste bricht nicht mehr um (`white-space: nowrap`).
- Tests: ein Unit-Test für Erlös, Teilmenge, „nichts mehr da“; ein E2E-Schritt für Bestätigung, Mini-Pause-Sperre und Gold.

## 20. Update 2.9.3 (18.10.2026)

- **Klickbarkeit der Fenster im Kampf:**
  - Turmübersicht: Neuaufbau nur noch bei Strukturänderungen (Türme, Stufen, Spezialisierung, Sortierung, Filter, Phase, Mini-Pause, Bestätigung). Gold, Schaden, Abschüsse, Abklingzeiten und die Sperrzustände der Knöpfe werden live in die bestehenden Elemente geschrieben (`tlLive`).
  - Turmpanel: Strukturteil (Gold zählt nur als „bezahlbar ja/nein“ der Kosten im Panel) wird sofort neu aufgebaut, der Live-Teil höchstens einmal pro Sekunde; die Scrollposition bleibt erhalten.
  - Panel, Turmübersicht und Helden-Fenster werden nie neu aufgebaut, solange dort eine Maustaste oder ein Finger gedrückt ist (`pointerHeld`).
- **Feld-Picking** (`render/picking.ts`): Der Sichtstrahl wird von oberhalb der höchsten Höhenstufe bis unter die Wege abgetastet (320 Schritte); das erste Feld, dessen Oberfläche erreicht wird, gilt. Klippenflächen zählen zum höheren Feld.
  - Gemessen gegen eine fein abgetastete Referenz an rund 70 000 Klickpunkten: altes Verfahren 8,8 % falsch, neues 0,3 % (nur Rundung an Feldgrenzen).
  - Test: `tests/picking.test.ts` (Fehlerquote unter 1 %, alle vier Kameradrehungen).
- **E2E:**
  - Neuer Schritt: Turmübersicht und Turmpanel im laufenden Kampf mit langsamem Klick; Gold tickt ständig, Aufwerten, „Spezial.“ und Spezialisierung müssen ankommen.
  - Assertions vergleichen keine ElementHandles mehr; die Formatierung einer fehlgeschlagenen Meldung hatte Node den Speicher gesprengt (Ursache der früheren Abbrüche).

## 21. Update 2.9.4 (18.10.2026)

- **Fusionen in der Turmübersicht:** `fusionPairs(sim)` liefert jedes Paar einmal, mit Rezept, Bereitschaft (beide Stufe 4 und spezialisiert), Bezahlbarkeit und Zielturm. Die Übersicht zeigt die Paare oben; „Verschmelzen“ bei bereiten Paaren, sonst die fehlenden Voraussetzungen und „Zeigen“. Türme in einem Paar werden nicht als „ausgebaut“ ausgeblendet.
- **Richtung Lane:**
  - `laneDistance(sim, x, y)` misst den Abstand zur nächsten Wegzelle (Classic) bzw. Routenzelle (Mazing).
  - `fuseTowers` setzt den legendären Turm auf das Feld des Partners, wenn dieses näher an der Lane liegt; bei Gleichstand bleibt er am ausgewählten Turm.
  - Das andere Feld wird frei; Runen des entfernten Turms kehren in den Beutel zurück.
- Tests: ein Unit-Test (Paarerkennung, Bereitschaft, Ergebnis am Weg) und ein E2E-Schritt (Verschmelzen in der Übersicht).

## 22. Update 2.9.5 (18.10.2026)

- **Versetzen legendärer Türme:**
  - `canMoveTower` und `moveTower` in `EconomySystem`: nur legendäre Türme, Ziel frei und bebaubar; im Mazing mit Weg- und Einsperr-Prüfung.
  - Kostenlos; `invested`, Runen, Stufe und Priorität bleiben, der Turm ist danach 3 s betäubt (`MOVE_STUN`).
  - Bedienung über `startMove` im Turmpanel: Vorschau mit Gültigkeit am Zielfeld, Klick versetzt, Esc oder Rechtsklick bricht ab.
- **Balance:**
  - Sonnenlanze: `laserRamp` +1 statt +2, Schaden ×0,85, `markPct` 0,12 statt 0,2.
  - `dealDamage`: Markierungen senken nur positive Resistenzen und höchstens bis 0. Vorher konnten sie Schwächen um bis zu 40 Punkte vertiefen.
- **Gemeinsame Heldenstufe:**
  - `HeroMetaProgress.heroLevel {level, xp}` gilt für alle Helden; `champions[id].tree` bleibt je Held.
  - `commitHeroProgress` aktualisiert die gemeinsame Stufe nur nach vorn.
  - `sanitizeHeroLevel` übernimmt bei alten Ständen den weitesten Helden.
  - Freie Skillpunkte je Held = Punkte der gemeinsamen Stufe − eigene Ränge.
- **Tempo:** `SIM.speeds = [1, 2, 3, 5, 8]`; der Knopf und `F` schalten der Reihe nach weiter.
- **Hintergrund:**
  - Statt `autoPause` beim Ausblenden startet ein 250-ms-Zeitgeber, der die Simulation ohne Darstellung weiterrechnet (`GameSession.simOnly`, in 0,1-s-Schritten, bis 60 s Rückstand).
  - Beim Einblenden übernimmt wieder `requestAnimationFrame`.
- **Darstellung:** Hindernis-Dekor mit `DECOR_SCALE = 0.82` (Lava und Wolken unverändert); die Klickhöhen in `pickObstacle` sind angepasst.
- **Tests:**
  - Unit: gemeinsame Stufe inkl. Migration, Versetzen.
  - E2E: Tempo-Zyklus, Hintergrund ohne Pause, Versetzen per Klick.

## 23. Update 3.0 (18.10.2026) – Skalierung für späte Wellen

Anlass: In Welle 137 kam nichts mehr durch. Ab Welle 30 wuchsen die Gegner-LP nur noch linear (Welle 137 hatte das 92-fache der Grund-LP), während die Türme schneller skalierten; Paragon begann erst nach rund 1 Mio. EP.

Alle Werte stehen in `config/scaling.ts` (`SCALING`).

- **Exponentieller Anteil:** `waveHpMul` × 1,02^(Welle − 60), `bossHpMul` × 1,015^(Welle − 60), nur im Endlosmodus.
- **Weltstufen:** `worldTier = floor(Welle / 50)`. Je Stufe:
  - Gegner-LP +15 %
  - physische Resistenz +3 % (bis 30 %)
  - Tempo +3 % (bis 30 %)
  - Kopfgeld +20 %
  - Meldung beim Erreichen; Anzeige als Chip in der Wellenleiste.
- **Affixe:** `planWave` würfelt 1–3 Affixe aus einem eigenen Zufallsstrom (`affix-<Welle>`); frühere Wellen bleiben dadurch identisch.

  | Affix | Wirkung |
  |---|---|
  | Regeneration | 1 % LP/s, durch Gift/Brand unterbunden |
  | Schildträger | Schild in Höhe von 25 % der LP |
  | Immunität | ein Schadenstyp, Resistenz mindestens 85 % |
  | Flink | +20 % Tempo |
  | Splitter | 2 Schwarmlinge beim Tod; Kinder splitten nicht, Bosse und Mini-Bosse nie |
  | Störsender | Elite betäubt alle 8 s Türme in 1,5 Feldern für 1 s |
  | Gepanzert | +20 % physische Resistenz |

  Die Affixe werden beim Erscheinen am Gegner gespeichert und gelten so auch bei überlappenden Wellen.
- **Druck ab Welle 100:** Elitegruppe in jeder Welle, Mini-Bosse alle 3 Wellen (Helden-Endlos), Boss-Durchbruch ×2.
- **Nachrücken** (Helden-Endlos, ab Welle 120): Läuft der Kampf 45 s und sind alle Gegner der Welle erschienen (kein Bosskampf), wird die nächste Welle wie beim vorzeitigen Ruf gestartet (`callNextWave`).
- **EP-Kurve:**
  - `xpToNext` ab Stufe 25 × (25/Stufe)^1,2; Summe bis 99 ≈ 360 000 statt 1,03 Mio.
  - Paragon `xpBase` 8 000 statt 20 000.
- **Paragon-Budget** (`paragonBudget`) = Paragonstufe + `paragonPrePoints(Heldenstufe)` (ab 50 alle 5 Stufen, 10 Punkte) + `milestoneParagon(Bestwelle)`. `sanitizeParagon` kürzt auf dieses Budget.
- **Meilensteine** mit `paragon`/`startGold`:

  | Welle | Paragonpunkte | Startgold |
  |---|---|---|
  | 100 | +2 | +150 |
  | 150 | +2 | +150 |
  | 200 | +3 | +200 |
  | 250 | +3 | – |
  | 300 | +5 | +300 |

  Die Belohnungen leiten sich aus der Bestwelle ab, eine Gutschrift ist nicht nötig.
- **Turm-Meisterschaft** (`config/mastery.ts`, `HeroMetaProgress.towerMastery`):
  - Abschüsse je Turmtyp werden dauerhaft gezählt; `commitHeroProgress` speichert sie als absoluten Wert, nur nach oben, ohne Doppelzählung.
  - Rang = floor(√(Abschüsse/50)), höchstens 20; je Rang +1 % Schaden (`TowerSystem`).
- **Runen:** `splinterFrom` 100 (nur Qualität I), volle Runen weiter ab 250.
- **Tests:** `tests/v30.test.ts` für Stufen und Affixe (Determinismus), Affix-Wirkungen, Nachrücken, Boss-Durchbruch, EP-Kurve, Vorab-Punkte, Meilensteine und Meisterschaft. Die Skalierungs- und Rune-Tests sind an die neuen Regeln angepasst.

## 24. Update 3.1 (18.10.2026) – Challenge-Bosse, spezialisierter Turmkauf, Runenbedienung

- **Challenge-Bosse** (`config/scaling.ts` `CHALLENGE`, `sim/ChallengeSystem.ts`):
  - `isChallengeWave`: Endlos, Welle ≤ 250 alle 50, danach alle 25.
  - `planWave` setzt die Bossgruppe auf `challenge: true`. Ohne regulären Boss in dieser Welle wird ein Endlos-Boss gewählt.
  - `makeChallenge` beim Erscheinen: LP ×3,5, Schild skaliert, Kopfgeld ×5, Durchbruch = max(10, 25 % der maximalen Kernenergie).
  - `updateChallenge`: Schildschub alle 15 s (+8 %, höchstens 30 % der LP), 3 Elite-Begleiter alle 12 s, Raserei nach 60 s (Tempo ×1,4).
  - `resistOf` +15 % auf alle Typen; `addStatus` verkürzt Kälte, Frost, Betäubung und Wurzeln auf 35 %.
  - Beim Sieg: Meldung, 3 Relikte und `dropRune`.
- **Spezialisierter Kauf** (`EconomySystem` `PRESPEC`, `prespecAvailable`, `prespecCost`):
  - `canBuildHere`/`buildTower`/`sim.build`/`sim.canBuild` nehmen optional `spec` (1/2).
  - Möglich ab Welle 250 im Endlos für Türme mit 3 Stufen und 2 Spezialisierungen (nicht legendär).
  - Preis = (Grundpreis inkl. Modifikatoren + Summe der Stufen + Spezialisierung) × 1,25.
  - Der Turm steht auf Stufe 4 mit Spezialisierung; `invested` = Preis, sodass die Verkaufserstattung korrekt bleibt.
  - UI: ★ an den Bauleistenknöpfen; ein Klick öffnet `prespec-pop` (Normal/A/B mit Preisen). Die Wahl landet in `HudView.buildSpec`.
- **Runen:**
  - Ein Klick wählt die Rune und zeigt die Infobox (`rune-info`) mit Wirkung und Knopf „Sockeln“ bzw. „Herausnehmen“.
  - Ein zweiter Klick auf dieselbe Rune innerhalb von 450 ms (Doppelklick) führt die Aktion aus. Die Erkennung läuft über Zeitstempel und funktioniert auch, wenn das Panel dazwischen neu aufgebaut wird.
  - Der Beutel bleibt auch bei vollen Sockeln sichtbar, damit man die Wirkung nachlesen kann.
- **Tests:** `tests/v31.test.ts` deckt Zeitplan, Mechaniken, Erscheinen im Spiel und Kaufpreis/Bau ab. E2E-Schritte 3.1 prüfen Wellenvorschau, Auswahlmenü und Bau sowie Runen per Klick/Doppelklick.

## 25. Update 3.2 (18.10.2026) – Rüstungsbruch, Turmreihenfolge, Spezialisierungswechsel, Heldenschmiede

- **Rüstungsbruch:**
  - Neue Turmwerte `bleedDps`, `bleedDur`, `bleedMaxStacks` (Standard 8) und `shredPct`; neuer Status `shred`.
  - Jeder Treffer legt einen Stapel an und erneuert die Dauer. Je Stapel entsteht physischer Schaden über Zeit (`tickStatuses`).
  - `dealDamage` senkt die physische Resistenz um `shredPct` je Stapel, höchstens bis `STATUS_RULES.shredFloor` (−20 %).
  - Rüstungsbruch stoppt Regeneration.
  - Dornenhain: physisch, 38 Schaden, Durchschlag 3, Blutung 11/s je Stapel, 5 % je Stapel.
- **Turmreihenfolge:** `TOWER_ORDER` folgt `TOWER_UNLOCKS`; die Standardtasten sind positional vergeben. Eigene Tastenbelegungen bleiben erhalten.
- **Spezialisierung wechseln** (`EconomySystem`):
  - `respecCost` (50 % des Preises der neuen Spezialisierung) und `respecTower`.
  - `specTargetCost` = Restkosten bis Stufe 4 + Spezialisierung bzw. Wechsel.
  - `groupSpecialize`: günstigste zuerst, so viele wie das Gold erlaubt.
  - UI: „⇄ Wechseln zu …“ im Turmpanel; `group-spec-1`/`group-spec-2` in der Gruppenauswahl.
- **Spezialisierter Kauf:** `PRESPEC.from` = 150.
- **Boss-LP:** `BOSS_HP_GLOBAL` = 1,5; Endlos-Zuwachs nach Welle 40 +0,07 je Welle; `SCALING.bossExpRate` = 1,018.
- **Heldenschmiede** (`config/items.ts`):
  - Je Champion zwei `craftOnly`-Items mit `champions: [id]`: episch (Artefakt, Accessoire, Helm oder Spezial) und eine legendäre Waffe.
  - Rezepte `kind: 'hero'` mit `champion`, `HERO_RECIPE_COST` und `HERO_RECIPE_WAVE`.
  - `recipeState` sperrt fremde Helden. Die Sammlung zeigt nur Gegenstände, die der aktuelle Held tragen kann (oder die er besitzt).
- **Tests:** `tests/v32.test.ts`; E2E-Schritte 3.2 für die Gruppenspezialisierung und die Heldenschmiede.

## 26. Update 3.3 (18.10.2026) – Runen je Sorte, Turmlimit, Konter, Drachenhorst, Endstufen-Knoten

- **Runen** (`RuneSystem`):
  - `RuneInstance.def` = gesockelt in dieser Turmsorte; `tower` ist veraltet und wird von `runesOf` in `def` umgewandelt. Was die Sockelzahl übersteigt, kommt in den Beutel.
  - `runeSockets(def, wave)` mit `RUNE_SLOT_WAVES` [100, 250, 400], legendär +1.
  - `towerRunes` liefert die Runen der Sorte; `releaseRunes` ist ohne Wirkung (Runen bleiben gesockelt).
- **Turmlimit** (`config/scaling.ts` `TOWER_LIMIT`):
  - `towerLimit(wave, endless)` = 6 + ⌊Welle/5⌋, sonst unbegrenzt.
  - `canBuildHere` prüft `countedTowers` (ohne Barrikaden).
  - `focusBonus` = min(30 %, freie Plätze × 2 %) auf den Turmschaden (nicht Wirtschaft).
  - `rules.towerLimitOff` existiert nur für automatisierte Tests.
- **Konter:**
  - `ui/text.ts` `damageMatchups(type)` (Schwäche ≤ −10 %, Resistenz ≥ 20 %) und `towersOfType`.
  - Ausgabe in `resistLine` (Gegner/Bosse) und `matchupLines` (Türme).
  - Datenänderungen: `maw` {physisch 15 %, arkan 10 %}, `worldbreaker` {Feuer 20 %, Gift 20 %}.
- **Luftabwehr:** Turmwerte `airDmgMul` (in `applyHit` bei fliegenden Zielen) und `preferAir` (`pickTargets` wählt dann nur Flieger). Drachenhorst/Phönix: ×2,5 und Vorrang.
- **Skillbaum:** `endgameNodes(id)` wird an jeden Champion-Baum angehängt (Stufen 5–8, 89 Ränge). Gesamtränge ≥ `skillPointsForLevel(99)` = 117.
- **Tests:** `tests/v33.test.ts`; der Runentest in `v27` ist an Sortenrunen angepasst. E2E-Schritt 3.3 prüft Turmlimit und Sortenhinweis.

## 27. Update 3.4 (18.10.2026) – Zurücksetzen, Kernreparatur, Boss-Bauplätze, Heldenfilter

- **Skillbaum:** `resetSkillTree` (`sim.treeReset`) gibt alle Ränge als Punkte zurück und zieht die Kernwall-Kernenergie ab. Gespeichert wird absolut über `commitHeroProgress`.
- **Paragon:** `resetParagon` ist jederzeit möglich (vorher nur in der Bauphase).
- **Kernreparatur** (`EconomySystem` `CORE_REPAIR`, `coreRepairState`, `repairCore`):
  - Nur Endlos. Stellt max(3, 25 % von `maxLives`) wieder her.
  - Kosten (60 + 12·Welle)·(1 + 0,25·`coreRepairs`).
  - UI: ✚-Knopf (`repair-core`) an der Kernenergie.
- **Bosswellen:** `completeWave` erhöht nach Bosswellen im Endlos `bossSlots` und heilt max(2, 10 % von `maxLives`). `currentTowerLimit` = `towerLimit` + `bossSlots`.
- **Fokusbonus-Erklärung:** Der 🏰-Knopf (`tower-limit-btn`) öffnet `limit-pop` mit Belegung, Rechenbeispiel und Quellen neuer Plätze; im Turmpanel steht `focus-line`.
- **Heldenfilter:**
  - `missingItems` (Beute, Fragmente, Fragmentbündel) und `collectionOffers` (Ladenausrüstung) liefern nur, was `canEquipItem` für den aktuellen Helden erlaubt.
  - Der Rucksack im Startbildschirm zeigt nur passende Gegenstände.
- **Tests:** `tests/v34.test.ts`; Ladentest angepasst. E2E-Schritt 3.4.

## 28. Update 3.5 (18.10.2026) – Flugabwehr

- **Analyse:** Kälte wirkte auch auf Flieger. Fast alle anderen Kontrollmittel (Minen, Gift-Verlangsamung, Wurzeln, Gravitation-Zug) betreffen aber nur Bodengegner, und schnelle Flieger verließen die kurze Frostreichweite sofort.
- **`skyguard` Flakbatterie:**
  - Physisch, `hitsGround: 0`, `multishot` 2, Kälte 12 % je Treffer (max. 45 %).
  - Freischaltung W5 (wie die ersten Flieger), Taste 9.
- **`skywatch` Himmelswacht:** Fusion `skyguard` + `frost`; Frost, `hitsGround: 0`, `multishot` 4, `airDmgMul` 3, Kälte bis 70 %, 8 % Einfrieren.
- **Flieger:** `flyer` 54 LP / 1,45 Tempo, `griffin` 105 LP / 1,8 Tempo / 12 % Ausweichen.
- **Reihenfolge/Tasten:** `TOWER_ORDER` weiter nach Freischaltung, die Tasten positional bis `+`.
- **Tests:** `tests/v35.test.ts`; Inhaltszähler (20 Türme, 12 legendäre, 12 Fusionen) angepasst. E2E-Schritt 3.5.

## 29. Update 3.6 (18.10.2026) – Goldverwendung: Veredelung, Bauplätze, Kernausbau

- **Konfiguration:** `config/goldsinks.ts` (`STARS`, `STAR_KEYS`, `starMods`, `starText`, `SLOT_BUY`, `CORE`, `CORE_UPGRADES`).
- **Veredelung:**
  - `TowerInstance.stars`; `computeTowerStats` wendet `starMods` an.
  - Kämpfende Türme: `damageMul` +0,08 und `attackSpeedMul` +0,02 je Stern, dazu die Sortenwerte. `mul`-Werte wirken als Potenz (z. B. Goldgräber 1,08^n).
  - `starCost` = Wert des voll ausgebauten Turms × 0,6 × 1,55^n (legendär: Basis 900).
  - `starState`/`starTower`/`starAllTowers`. `upgradeAllTowers` veredelt im Endlos nach den Stufen weiter.
  - UI: `quickActions` (Aufwerten, Spezialisierung oder ★ direkt unter dem Kopf), `group-star`, Sortenraster `tl-types` in der Turmübersicht, `tl-star-<uid>`.
- **Bauplätze:** `slotCost`/`buySlot`, `GameState.boughtSlots`; `currentTowerLimit` enthält gekaufte Plätze. Kaufknopf `buy-slot` in `limit-pop`.
- **Kern** (`sim/CoreSystem.ts`, `GameState.core`):
  - Panzer +4 `maxLives` je Stufe.
  - Schild: `coreShieldLeft` wird je Welle auf die Stufe gesetzt; `leak` wird abgefangen (nicht bei Bossen).
  - Kanone: alle 0,9 s Schaden 9 × Stufe × `waveHpMul` (Bosse halb) auf den nächsten Gegner im Umkreis von 3,6 um `map.goal`.
  - UI: `core-btn` öffnet `core-pop`.
- **Tests:** `tests/v36.test.ts`; E2E-Schritt 3.6.

## 30. Update 3.7 (18.10.2026) – Unterstützungstürme ohne Limit

- `config/scaling.ts` `LIMIT_FREE` = Barrikade, Leitpylon, Totemschrein; `countsTowardLimit(def)`.
- `countedTowers` und die Limitprüfung in `canBuildHere` zählen diese Türme nicht mit. Sie senken damit auch den Fokusbonus nicht.
- UI: ∞-Markierung (`.tbtn.limitfree`), Hinweis im Tooltip, in der 🏰-Erklärung und in der Hilfe.
- Test: `tests/v37.test.ts`.

## 31. Update 3.8 (18.10.2026) – Rückbau Kern/Bauplätze, Turmlimit 20, Lane-Barrikaden

- **Entfernt:** `sim/CoreSystem.ts`, `CORE`/`CORE_UPGRADES`/`SLOT_BUY` (goldsinks), `CORE_REPAIR`/`repairCore`/`buySlot`/`buyCoreUpgrade` (EconomySystem), die Heilung nach Bosswellen und die zugehörigen Zustandsfelder und Bedienelemente. Bosswellen geben weiterhin +1 Turmplatz.
- **Turmlimit:** `TOWER_LIMIT.base` = 20. `LIMIT_FREE` ergänzt um `goldmine` und `lanebarricade`.
- **Paragon:** `pg_core` hat 20 Ränge mit je +2 Kernenergie.
- **Himmelswacht:** Schaden 80, Reichweite 6,2, Abklingzeit 0,32, 6 Ziele, Splash 1,1, Kälte 40 %/4 s bis 75 %, Einfrieren 15 %/1,5 s, `airDmgMul` 4.
- **Lane-Barrikade** (`TowerId` `lanebarricade`, `laneOnly`):
  - `canBuildLane`: nur Endlos, nicht Mazing, nur auf `Cell.Path`, Abstand ≥ `LANE_BARRICADE.minSpawnDist` (4) zu allen Spawns, nicht neben dem Kern.
  - Ladungen über `GameState.laneBarricades` (Spielzeiten): höchstens 2 je 600 s (`laneCharges`).
  - `updateLaneBarricades` (alle 3 Ticks) legt Kälte 50 %/1,2 s auf Bodengegner im Radius 0,85.
  - Keine Wegblockade, denn `blocked` gilt nur im Mazing.
  - Die Taste B teilt sie sich mit der Mazing-Barrikade; welche gilt, hängt von der Variante ab.
- **Tests:** `tests/v38.test.ts`; Limit-, Paragon-, Reparatur- und Kernausbautests angepasst bzw. entfernt. E2E-Schritt 3.8.

## 32. Update 3.9 (18.10.2026) – nur noch Helden-Endlos

- **Entfernt:**
  - Die Bildschirme `newgame`, `campaign` und `editor` (Dateien `ui/screens/newgame.ts`, `ui/screens/campaign.ts`, `editor/MapEditor.ts`) samt Menüeinträgen.
  - Im Ergebnisbildschirm „Nächste Mission“; „Neuer Lauf“ führt zur Heldenwahl.
  - Der Hilfe-Reiter zu den Lane-Helden.
- **Fortsetzen:** `canResume` bietet nur Läufe mit `mode === 'heroEndless'` an.
- **Helden-Freischaltung:** `championUnlockText` nennt nur die Bestwelle.
- **Erfolge:** Die bestehenden IDs werden über `heWave(s, n)` auch im Helden-Endlos erreicht.
- **Simulation:** Modi und Missionsdaten bleiben intern erhalten (Unit-Tests nutzen den Schnellmodus); `editor/mapCode.ts` bleibt für den Speichertest.
- **E2E:** Die Schritte für Schnellspiel, Kampagne, Tages-/Endlosmodus und Editor sind entfernt. Der Smartphone-Block startet Helden-Endlos. Neuer Schritt 3.9 prüft das Menü.

## 33. Update 3.10 (19.10.2026) – Karteneditor für Helden-Endlos

- `editor/MapEditor.ts` ist zurück (Bildschirm `editor`, Menüeintrag).
- „Spielen“ ruft `app.go('heroSetup', { heroMap })` statt einen eigenen Modus. Die Schwierigkeitswahl im Editor entfällt, sie gibt es im Startbildschirm.
- `renderHeroSetup(app, heroMap)`:
  - Auswahl `map-select`: Zufallswelt, Editorkarte, gespeicherte Karten (`save.customMaps`, per `decodeMap` und `validateMap` gefiltert).
  - Eine gewählte Karte landet in `RunSetup.customMap`; Variante und Biom werden aus ihr übernommen.
- Der Helden-Endlos-Start (Laden, Held, Wellen) funktioniert auf Editorkarten, weil `setup.ts` `customMap` modusunabhängig nutzt.
- **Tests:** `tests/v310.test.ts` (Editorkarte und Mazing-Vorlage im Helden-Endlos); E2E-Schritt 3.10 (Export/Import, Spielen, Kartenwahl).

## 34. Update 3.11 (19.10.2026) – Balancing Welle 150–500

**Prüfung vorher** (Rechenmodell aus echten Wellen- und Turmdaten; Gold, Turmlimit, Bossplätze und Veredelung):

| Welle | Gegner-LP (150 = 1) | Spieler-DPS (150 = 1) |
|---|---|---|
| 200 | 3,9 | 1,33 |
| 300 | 50 | 2,1 |
| 500 | 5.600 | 3,8 |

Zusätzlich schwankten späte Wellen an der Mengenobergrenze nur durch den Gegnertyp um bis zum 40-Fachen.

**Änderungen:**
- `waveHpMul`: `SCALING.expRate` (1,02) nur bis `expSlowFrom` (150), danach `expRateLate` (1,006).
- `bossHpMul` (Endlos, ab 150): wächst proportional zu `waveHpMul`. `challengeHpMul(w)` = 3,5 · (1 + (w−150)/100).
- `planWave` (Endlos, ab `smoothFrom` 100): Gruppengröße = `cap · smoothHpPerSlot (90) / Typen / effektive LP`; die effektiven LP enthalten die Splitter beim Tod. Kalibriert auf den bisherigen Durchschnitt (W120–199 vorher Ø ≈ 8.000 Grund-LP mit Spannweite 1.000–40.000, jetzt Ø ≈ 5.800 mit Spannweite 4.200–8.500).
- `STARS.max` 25: Wirkung ab ★11 halb (`softcap`), Preis ab ★11 ×1,25 (`lateGrowth`).
- `RUNE_SLOT_WAVES` um 500 ergänzt.

**Prüfung nachher:**

| Welle | Gegner-LP (150 = 1) | Spieler-DPS (150 = 1) |
|---|---|---|
| 200 | 1,8 | 1,33 |
| 300 | 4,8 | 2,1 |
| 500 | 26 | 3,8 |

Die Wand verschiebt sich stetig statt abrupt. Paragon, Meisterschaft, Runen, Fokusbonus und Sterne über ★10 schließen den Rest.

**Tests:** `tests/v311.test.ts`; Skalierungstest in `sim.test.ts` angepasst.

## 35. Update 3.12 (19.10.2026) – Flieger

- **Messung vorher:** Flieger Ø 1,9 Felder/s bei Welle 200 und Ø 2,3 bei Welle 300, maximal 3,1 (Weltstufe +3 %/Stufe, „Flink“ ×1,2 und Profile multiplizieren sich).
- `FLYER` (scaling): `maxSpeed` 2,0, `speedMulShare` 0,5, `slowMul` 1,3.
- `makeEnemy`: Für Flieger wirkt nur die Hälfte des Wellen-Tempozuschlags, gedeckelt auf 2,0. `slowFactor`: Verlangsamung × 1,3 für Flieger, vor der harten Obergrenze von 75 %.
- **Messung nachher:** Flieger Ø 1,7–1,9 Felder/s, maximal 2,0.
- Die Simulation läuft tempounabhängig (feste Ticks; `advance` erhöht nur die Tickzahl).
- **Tests:** `tests/v312.test.ts`.

## 36. Update 3.13 (22.10.2026) – Grafik & Oberfläche

**Renderer (`src/render/Renderer.ts`):**
- **Material `MAT.terrain` (5):** wird wie Standard beleuchtet. Dazu kommen drei Rauschschichten in Weltkoordinaten, damit die Farbe über Feldgrenzen weiterläuft, plus warme und kühle Flecken und Gesteinsschichten an Seitenflächen. `terrain.ts` setzt es für Wegband, Fahrspuren, Sockel, Deckschicht, Lippen, Inselsockel und Randinseln.
- **Wasser:** Die Normalen kommen aus einem animierten Höhenfeld (Finite Differenzen), statt flacher Dreiecksnormalen. Dazu Sonnenglitzern (Exponent 140), Fresnel-Spiegelung des Himmels und Uferschaum über den Abstand zum Inselrechteck (`Lighting.island`).
- **Lava:** dunkle Kruste aus Rauschen; dort keine Emission.
- **Wolkenschatten:** `Lighting.clouds` legt ein langsam wanderndes Rauschen auf den Schattenfaktor.
- **Rand- und Nebellicht:** Das Randlicht nimmt zur Sonne hin die Lichtfarbe an. Die Nebelfarbe verläuft vom unteren zum oberen Bildrand (`fog` → `fogTop`).
- **Himmel (`SKY_FS`):** Er wird vor der Szene gezeichnet: Verlauf mit denselben Farben wie der Nebel, Horizontglühen, Wolkenbänder und Sterne (`Lighting.skyGlow`, `stars`).
- **Nachbearbeitung:**
  - Tilt-Shift aus einer viertelgroßen, zweifach weichgezeichneten Kopie: oben bis 85 %, unten 60 %.
  - Leichte Farbsäume am Rand.
  - Split-Toning und weicher Lichter-Schulterverlauf.
  - Sonnendunst an der projizierten Lichtrichtung, Filmkorn.
  - Steuerung über `Grade.dof`, `split`, `sun` und `sunColor`.
  - `RenderQuality.dof` ist nur aktiv, wo auch Glühen läuft, und abschaltbar über `PlayerSettings.cinematicFx`.
- **Sprite-Form `SHAPE.vortex` (7):** spiralförmiger Wirbel.

**Welt (`WorldView.ts`):**
- `MOOD` je Biom: Himmelsfarbe oben, Glühfarbe, Wolkenschatten, Sterne, Sonnendunst (höchstens 0,13, damit das Glühen reduziert bleibt).
- Portale mit zwei gegenläufigen Wirbeln, Runenstern, Ring und Lichtsäule.
- Kern mit Lichtsäule, Runenkreis und zwei kreisenden Ringen.
- Lichtstrahlen in Sonnenrichtung (`drawSunRays`).

**Oberfläche (`styles.css`, Block „Glas & Glut“):**
- Glasflächen mit `backdrop-filter`, Verlaufslinien an Kopf- und Turmleiste.
- Knöpfe mit Glanzkante und Lichtschein; Hauptknopf mit wandernder Glanzspur.
- Turmkarten heben sich beim Überfahren an; Auswahl pulsiert.
- Boss- und Lebensbalken mit Glanz; Banner mit Verlaufsschrift und Lichtschein.
- Logo mit Schimmer und rotierendem Lichtkranz; Funken im Menü.
- „Bewegung reduzieren“ stoppt alle neuen Animationen.

**Tests:** `tests/v313.test.ts`; E2E-Schritt 3.13 (Kino-Effekte ab Mittel, abschaltbar, Glasflächen). Spiellogik unverändert.
