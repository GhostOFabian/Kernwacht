# KERNWACHT – Welten unter Belagerung

> Hinweis: Der frühere Arbeitstitel „Riftbound“ wurde ersetzt, da er bereits von Riot Games (League of Legends Trading Card Game) verwendet wird. Spielstände unter dem alten Speicherschlüssel werden automatisch übernommen.

Ein isometrisches Tower-Defense-Spiel für den Browser (Desktop & Smartphone), vollständig auf Deutsch. Seit 3.9 gibt es nur noch den **Helden-Endlosmodus**: ein frei steuerbarer Held (zwölf Helden mit Skillbäumen, Paragon und Ausrüstung) verteidigt zusammen mit Türmen den Kern gegen endlose Wellen. Dazu kommen prozedurale Welten in drei Kartengrößen (Classic & Mazing), 20 Türme mit Ausbaustufen, Spezialisierungen und Veredelung, zwölf legendäre Fusionstürme, Runen, Elementarreaktionen, Turmnetzwerke, Challenge-Bosse und Erfolge mit Turm-Skins.

**Neu in 2.1 (17.10.2026): Helden-Endlosmodus** – ein frei steuerbarer Held (3 Archetypen mit Skillleiste und Skillbaum), Turmverstärkungen, EP- und Beutesystem, Fragmente und Herstellung, 6 Ausrüstungsslots, stapelbare Tränke, ein Laden auf jeder Karte, automatische Wellen nach 15 s Vorbereitung, Wellenprofile, Mini-Bosse und dauerhafte Perks und Meilensteine über die Bestwelle. Analyse, Systeme, Werte, Tests und Einschränkungen: [`docs/HERO_ENDLESS.md`](docs/HERO_ENDLESS.md).

**Neu in 3.13 (22.10.2026) – Grafik & Oberfläche:**
- **Lebendigere Karten:**
  - Das Gelände bekommt durchgehende Farbverläufe über die Felder hinweg, mit Gesteinsschichten an den Kanten.
  - Wolkenschatten ziehen über die Insel.
  - Wasser mit Wellen, Sonnenglitzern, Spiegelung und Schaum am Inselufer.
  - Lava mit dunkler Kruste.
- **Himmel und Licht:**
  - Himmelsverlauf mit Horizontglühen und Sternen (Höhle, Kristall, Vulkan).
  - Lichtstrahlen in Sonnenrichtung.
  - Eigene Stimmung je Biom.
- **Kino-Look:**
  - Tilt-Shift-Unschärfe am oberen und unteren Bildrand für eine Miniatur-Wirkung.
  - Kühle Schatten und warme Lichter, feines Filmkorn, Sonnendunst.
  - Abschaltbar unter Einstellungen → „Kino-Effekte“; ab Qualität „Mittel“.
- **Portale und Kern:**
  - Wirbelnder Riss im Portal mit Runenkreis und Lichtsäule.
  - Lichtsäule in den Himmel und kreisende Ringe um den Kern.
- **Oberfläche:**
  - Glasflächen mit Unschärfe und Lichtkanten.
  - Glanz auf Knöpfen und Turmkarten, die sich beim Überfahren anheben.
  - Pulsierende Auswahl, Banner mit Goldverlauf, schimmerndes Logo mit Lichtkranz, schwebende Funken im Menü.

**Neu in 3.12 (19.10.2026) – Flieger:**
- **Tempo-Obergrenze:** Flieger fliegen höchstens 2 Felder/s und bekommen von Tempo-Zuschlägen (Weltstufe, Affix „Flink“, Wellenprofile) nur die Hälfte. Bisher erreichten sie in späten Wellen bis 3 Felder/s.
- **Verlangsamung wirkt 30 % stärker auf Flieger** (Frost, Flak-Netze, Himmelswacht, Kälte-Runen), bis zur üblichen Obergrenze von 75 %.
- Hinweis zu 8×: Die Simulation läuft bei jedem Tempo mit demselben festen Takt. 8× ändert keine Regeln, es lässt nur weniger Zeit zum Reagieren.

**Neu in 3.11 (19.10.2026) – Balancing ab Welle 150:**
- **Flacheres Wachstum:** Ab Welle 150 steigen die Gegner-LP nur noch um 0,6 % je Welle statt 2 %. Welle 500 ist damit etwa 25-mal so schwer wie Welle 150 statt über 3.000-mal.
- **Keine Ausreißerwellen mehr:**
  - Ab Welle 100 werden die Gegnergruppen nach Lebenspunkten verteilt statt nach Anzahl. Splitter beim Tod (Golems, Brutmütter) zählen mit.
  - Vorher schwankten späte Wellen je nach Gegnertyp um bis zum 40-Fachen (z. B. 90 Höhlengolems); jetzt höchstens um etwa das Doppelte.
  - Der Durchschnitt bei Welle 120–199 bleibt etwa gleich schwer.
- **Bosse bleiben relevant:**
  - Ab Welle 150 wachsen Boss-LP genauso wie die Wellen.
  - Challenge-Bosse werden ab Welle 150 je 100 Wellen um weitere 3,5× stärker (Welle 250: ×7, Welle 500: ×15,75) und machen damit wieder einen großen Teil der Welle aus.
- **Mehr Spielerwachstum:**
  - Veredelung bis ★25. Ab ★11 wirkt jeder Stern halb so stark, wird dafür nur noch um 25 % teurer statt um 55 %.
  - Vierter Runensockel je Turmsorte ab Welle 500.

**Neu in 3.10 (19.10.2026) – Karteneditor für Helden-Endlos:**
- **Karteneditor wieder im Hauptmenü:** Wege, Spawn und Kern malen, Goldadern und Leylinien setzen, Vorlage aus Seed, Speichern sowie Export und Import als Kartencode.
- „▶ Mit Helden spielen“ öffnet den Helden-Endlos-Startbildschirm mit der Karte. Dort wählst du wie gewohnt Held und Ausrüstung.
- **Neues Feld „Karte“ im Startbildschirm:** Zufallswelt (Seed), die Karte direkt aus dem Editor oder eine gespeicherte Karte. Variante und Biom kommen bei eigenen Karten aus der Karte. Über „✎ Karteneditor öffnen“ geht es direkt in den Editor.

**Neu in 3.9 (18.10.2026) – nur noch Helden-Endlos:**
- **Entfernt:** Neue Partie (Schnellspiel), Kampagne, Endlosmodus, Tagesherausforderung und Karteneditor (der Editor ist seit 3.10 für Helden-Endlos zurück). Das Hauptmenü bietet Helden-Endlos, Erfolge, Hilfe und Einstellungen.
- Gespeicherte, nicht beendete Partien der alten Modi werden nicht mehr zum Fortsetzen angeboten. Helden-Endlos-Fortschritt bleibt vollständig erhalten.
- **Helden:** Bisher über die Kampagne freigeschaltete Helden werden jetzt allein über die Bestwelle freigeschaltet.
- **Erfolge:** Erfolge und Turm-Skins sind über erreichte Wellen im Helden-Endlos erreichbar (z. B. Welle 25, 50, 100, 200; Welle 30 auf Mazing; Welle 50 auf riesiger Karte; Welle 20 ohne Kernverlust). Bereits erspielte Erfolge bleiben erhalten.
- Die Simulation der alten Modi bleibt intern für die automatisierten Tests erhalten, ist im Spiel aber nicht mehr erreichbar.

**Neu in 3.8 (18.10.2026):**
- **Entfernt:** Bauplatzkauf, Kernausbau (Kernpanzer, Kernschild, Kernkanone) und das Zurückholen von Kernenergie (✚-Reparatur und Kernenergie nach Bosswellen). Kernenergie gibt es jetzt nur noch über den Skillbaum (Kernwall) und Paragon.
- **Paragon „Bollwerk“:** jetzt +2 Kernenergie je Punkt, bis 20 Punkte (= +40 Kernenergie).
- **Turmlimit:** Start mit **20** Bauplätzen, alle 5 Wellen +1, nach jeder Bosswelle +1.
- **Nicht zum Limit zählen:** Goldgräber, Leitpylon und Totemschrein.
- **Himmelswacht stark verbessert:**
  - 6 Ziele gleichzeitig, Reichweite 6,2, schnellere Geschosse.
  - Eisexplosionen bremsen auch Flieger daneben, bis 75 % Verlangsamung.
  - 15 % Einfrieren, Schaden ×4 gegen Flieger.
- **Neu: Lane-Barrikade** (🚧, Taste B, Endlosmodi mit Classic-Karte):
  - Wird direkt auf die Lane gesetzt und verlangsamt Bodengegner, die darüber laufen, um 50 %. Flieger fliegen darüber hinweg.
  - Kostenlos und ohne Turmlimit. Mindestens 4 Felder Abstand zu jedem Riss, nicht direkt am Kern.
  - Höchstens 2 Barrikaden je 10 Minuten Spielzeit; die Turmleiste zeigt Ladungen bzw. Restzeit.

**Neu in 3.7 (18.10.2026):**
- **Unterstützungstürme zählen nicht zum Turmlimit:** Leitpylon und Totemschrein verstärken andere Türme und lassen sich frei bauen. In der Turmleiste sind sie mit ∞ markiert; sie senken auch den Fokusbonus nicht.

**Neu in 3.6 (18.10.2026) – Mehr Sinn für Gold (Endlosmodi):**
- **Veredelung ★1–★10:**
  - Voll ausgebaute Türme (Stufe 4 und Spezialisierung bzw. legendär) lassen sich für Gold weiter verstärken.
  - Jeder Stern gibt +8 % Schaden und +2 % Tempo, dazu die wichtigsten Werte der Sorte: Frost Verlangsamung, Sturm Kettensprünge, Sporen Gift, Armbrust Panzerbrechung, Drachenhorst Schaden gegen Flieger, Goldgräber +8 % Einkommen usw.
  - Preis je Stern ×1,55.
  - **Einfach:** Ein großer ★-Knopf steht direkt unter dem Turmkopf (ohne Scrollen). „Alle ▲“ und die Gruppenauswahl veredeln nach dem Ausbau automatisch weiter.
  - In der Turmübersicht gibt es oben je Turmsorte einen Knopf, der alle Türme dieser Sorte aufwertet bzw. veredelt – kein Scrollen durch die Liste mehr.
- **Bauplätze kaufen (in 3.8 wieder entfernt):** In der 🏰-Erklärung „＋ Bauplatz kaufen“. Preis (800 + 60 × Welle) × 1,7 je bereits gekauftem Platz.
- **Kernausbau (in 3.8 wieder entfernt):** Ein Klick auf die Kernenergie öffnet den Kern:
  - **Kernpanzer** (10 Stufen): je +4 maximale Kernenergie.
  - **Kernschild** (5 Stufen): fängt je Welle die ersten 1–5 Durchbrüche ab (nicht bei Bossen).
  - **Kernkanone** (10 Stufen): schießt auf Gegner nahe dem Kern, auch auf Flieger. Der Schaden wächst mit der Welle.
  - Die Reparatur ist ebenfalls dort.

**Neu in 3.5 (18.10.2026) – Flugabwehr:**
- **Neuer Turm Flakbatterie** (🚀, ab Welle 5, Taste 9):
  - Reine Flugabwehr, Bodengegner ignoriert sie.
  - Zwillingsgeschütze treffen 2 Flieger gleichzeitig mit Netzgranaten, die verlangsamen (bis 45 %).
  - Spezialisierungen: *Flakgewitter* (4 Ziele) oder *Fangnetze* (bis 70 % Verlangsamung).
- **Neue Fusion Himmelswacht** (Flakbatterie + Frostwächter): legendär und reine Flugabwehr. 4 Ziele, 3-facher Schaden gegen Flieger, starke Verlangsamung und Einfrieren.
- **Flieger entschärft:**
  - Aschenschwinge: Tempo 1,75 → 1,45, LP 64 → 54.
  - Sturmgreif: Tempo 2,2 → 1,8, LP 125 → 105, Ausweichen 20 → 12 %.
- **Tasten:** Durch die Flakbatterie verschieben sich die Tasten ab dem Minenwerfer um eine Stelle (Minenwerfer 0, … Drachenhorst +).

**Neu in 3.4 (18.10.2026):**
- **Zurücksetzen:**
  - Der Skillbaum jedes Helden hat den Knopf „↺ Skillbaum zurücksetzen“ (Charakter → Skillbaum): alle Punkte kostenlos zurück, jederzeit, auch im Kampf.
  - Paragon lässt sich jetzt ebenfalls jederzeit zurücksetzen (vorher nur in der Bauphase).
  - Der dauerhafte Passivbaum hat seinen Knopf weiterhin im Helden-Endlos-Menü.
- **Kernenergie zurückholen (Endlosmodi, in 3.8 wieder entfernt):**
  - Der grüne ✚-Knopf an der Kernenergie repariert den Kern gegen Gold: +25 % der maximalen Kernenergie (mindestens 3).
  - Preis (60 + 12 × Welle) Gold, jede weitere Reparatur +25 %.
  - Zusätzlich gibt es nach jeder geschafften Bosswelle 10 % gratis zurück.
- **Nach jeder Bosswelle ein zusätzlicher Turmplatz.**
- **Fokusbonus verständlich:**
  - Der 🏰-Knopf in der Turmleiste zeigt belegt/Limit und den aktuellen Bonus. Ein Klick öffnet eine Erklärung mit Rechenbeispiel („10 freie Plätze × 2 % = +20 % Schaden für alle Türme“) und den Quellen neuer Plätze.
  - Das Turmpanel zeigt den aktuellen Bonus.
- **Laden, Beute und Rucksack nur für den aktuellen Helden:** Es werden nur Gegenstände angeboten, fallengelassen oder als Fragment vergeben, die der gewählte Held tragen kann.

**Neu in 3.3 (18.10.2026):**
- **Runen je Turmsorte:**
  - Runensockel gehören der Turmsorte (auch jeder Fusion), nicht dem einzelnen Turm.
  - Eine Rune wirkt auf alle Türme dieser Sorte, auch auf später gebaute; ein Wechsel gilt sofort für alle.
  - Sockel: 1 ab Welle 100, 2 ab Welle 250, 3 ab Welle 400; legendäre Fusionen je +1.
  - Beim Verkauf bleiben die Runen bei der Sorte. Alte Spielstände werden übernommen.
- **Weniger ist mehr – Turmlimit in den Endlosmodi:**
  - Zu Beginn 6 Türme, alle 5 Wellen +1 (Welle 50: 16, Welle 100: 26, Welle 200: 46). Barrikaden zählen nicht.
  - **Fokusbonus:** je freiem Bauplatz +2 % Turmschaden, höchstens +30 %.
  - Anzeige 🏰 n/Limit in der Kopfzeile. Fusionen sparen Plätze.
- **Konter passen zu den Resistenzen:**
  - Gegner- und Bossinfos zeigen „▲ Schwach gegen“ (mit passenden Türmen) und „▼ Meiden“, direkt aus den Resistenzwerten.
  - Die Turminfos zeigen unter „Synergien & Konter“, gegen welche Gegner die Schadensart stark ist und von welchen sie gedämpft wird.
  - Korrigiert: Schlund der Tiefe (kein Giftschutz mehr, passend zu „Gift und Feuer gegen die Brut“), Weltenbrecher (widersteht Feuer und Gift statt Armbrust und Leerenlanze) und die Kontertexte mehrerer Türme.
- **Drachenhorst und Phönixhorst** greifen Flieger zuerst an und verursachen an ihnen 2,5-fachen Schaden.
- **Skillbaum bis Stufe 99:**
  - Jeder Held hat 9 neue Endstufen-Knoten (Stufe 5–8, ab Heldenstufe 25/45/65/90) mit zusammen 89 Rängen.
  - Bis Stufe 99 lassen sich damit alle 117 Punkte ausgeben, mit etwas Auswahl.

**Neu in 3.2 (18.10.2026):**
- **Dornenhain ist jetzt physisch:** Pfeile durchbohren 3 Gegner und legen *Rüstungsbruch* an. Das sind bis zu 8 Stapel wie beim Gift: je Stapel physische Blutung über Zeit und −5 % physische Resistenz (bis −20 %). Das verstärkt alle physischen Türme und stoppt Regeneration.
- **Schadensart sichtbar:** Die Bauleiste und das Baupanel zeigen die Schadensart jedes Turms. Physisch sind Waldläufer, Armbrust, Kanone, Minenwerfer und Dornenhain.
- **Turmleiste nach Freischaltung sortiert:**
  - Reihenfolge: W1 Waldläufer, Glut, Frost, Goldgräber · W3 Sturm, Kanone · W5 Sporen, Armbrust · W6 Minenwerfer · W7 Runenweber, Resonator · W9 Pylon, Totem · W11 Rissbinder, Tesla · W14 Gravitation, Meteor · W17 Laser, Drache.
  - Tasten in dieser Reihenfolge: 1–0, dann - = i j k l m n #.
- **Spezialisierung wechseln:** Spezialisierte Türme können für 50 % des Preises auf die andere Spezialisierung umgestellt werden. Bei einer Gruppenauswahl (Doppelklick) stellen die Knöpfe „Ⓐ Alle“ und „Ⓑ Alle“ alle Türme der Sorte um. Fehlende Stufen werden dabei mit ausgebaut; reicht das Gold nicht, so viele wie möglich.
- **Spezialisierter Turmkauf schon ab Welle 150** (vorher 250).
- **Boss-LP erhöht:** allgemein ×1,5. Im Endlos wachsen sie nach Welle 40 steiler (+7 % statt +5 % je Welle, zusätzlich ×1,018 statt ×1,015 je Welle ab Welle 60). Welle 100 ≈ 1,9-mal, Welle 137 ≈ 2,3-mal so viel wie in 3.1.
- **Heldenschmiede:** Jeder der 12 Helden hat zwei eigene, nur herstellbare Gegenstände:
  - einen epischen Signaturgegenstand ab Welle 8 (24 Metall, 10 Kristall, 4 Essenz);
  - eine legendäre Signaturwaffe ab Welle 20 (aus dem Signaturgegenstand + 8 Essenz + 3 Relikte).

  Beide stehen in der Schmiede ganz oben.

**Neu in 3.1 (18.10.2026) – Challenge-Bosse und spezialisierter Turmkauf:**
- **⚔ Challenge-Bosse** in allen Endlosmodi: in Welle 50, 100, 150, 200 und 250, danach alle 25 Wellen (275, 300, …).
  - 3,5× LP und +15 % auf alle Resistenzen; Verlangsamung, Einfrieren, Betäubung und Wurzeln wirken nur zu 35 %.
  - Alle 15 s ein Schildschub (+8 % LP, bis 30 %), alle 12 s drei Elite-Begleiter, nach 60 s Raserei (+40 % Tempo).
  - Ein Durchbruch kostet mindestens 10 Kernenergie bzw. 25 % der maximalen Kernenergie.
  - Sieg: 5× Kopfgeld, 3 Relikte und eine Rune.
  - Angekündigt in der Wellenvorschau; größer, mit rotem Bodenring und eigener Bossleiste.
- **Spezialisierter Turmkauf ab Welle 250** (seit 3.2 ab Welle 150): Ein Klick auf einen Turm in der Bauleiste öffnet ein Auswahlmenü:
  - Normal (Stufe 1)
  - Spezialisierung A
  - Spezialisierung B

  Spezialisierte Türme stehen sofort auf Höchststufe. Sie kosten Grundpreis + alle Stufen + Spezialisierung, mit 25 % Aufpreis.
- **Runen:** Ein Klick zeigt die Wirkung (Infobox). Ein Doppelklick sockelt die Rune in den gewählten Turm; bei einer gesockelten Rune nimmt er sie heraus.

**Neu in 3.0 (18.10.2026) – Skalierung für späte Wellen:**
- **Gegner wachsen ab Welle 60 sanft exponentiell** (×1,02 je Welle zusätzlich). Welle 137 ist etwa 4,6-mal so hart wie in 2.9, Welle 200 etwa 16-mal.
- **Weltstufen** alle 50 Wellen: je Stufe +15 % LP, +3 % physische Rüstung und +3 % Tempo, dafür +20 % Kopfgeld. Wird angekündigt und in der Wellenleiste angezeigt.
- **Wellen-Affixe ab Welle 100** (1, ab 175 zwei, ab 250 drei), in der Vorschau sichtbar:
  - Regeneration
  - Schildträger
  - Immunität gegen einen Schadenstyp
  - Flink
  - Splitter (zerfallen in Schwarmlinge)
  - Störsender (Elite betäubt Türme)
  - Gepanzert
- **Mehr Druck ab Welle 100:** Elite in jeder Welle, Mini-Bosse alle 3 Wellen, durchbrechende Bosse kosten doppelt Kernenergie.
- **Nachrückende Wellen ab Welle 120** (Helden-Endlos): Nach 45 s Kampf rückt die nächste Welle nach, mit Countdown in der Wellenleiste.
- **Flachere EP-Kurve:** Stufe 99 nach rund 360 000 statt 1 Mio. EP; Paragonstufen kosten 8 000 statt 20 000 EP als Basis.
- **Vorab-Paragonpunkte:** ab Heldenstufe 50 alle 5 Stufen einer (10 bis Stufe 95).
- **Meilensteine** W100, 150, 200, 250 und 300: insgesamt +15 Paragonpunkte und bis +800 Startgold, dauerhaft.
- **Turm-Meisterschaft:** Jeder Turmtyp sammelt dauerhaft Abschüsse; Rang r ab 50·r² Abschüssen, je Rang +1 % Schaden, bis Rang 20. Angezeigt im Turmpanel.
- **Runensplitter** (Qualität I) schon ab Welle 100, volle Runen weiter ab 250.

**Neu in 2.9.5 (18.10.2026):**
- **Legendäre Türme versetzen:** Im Turmpanel „⇄ Versetzen“ wählen und ein freies Zielfeld anklicken. Esc oder Rechtsklick bricht ab. Kostenlos; Ausbau, Runen und Werte bleiben erhalten, danach ist der Turm 3 s außer Gefecht.
- **Sonnenlanze abgeschwächt:** Aufbau ×5 statt ×6, −15 % Schaden, Markierung −12 % statt −20 %.
- **Markierungen (alle Quellen)** senken nur noch vorhandene Resistenzen, höchstens bis 0. Zusammen mit Schwächen entsteht kein Zusatzschaden mehr.
- **Heldenstufe für alle Helden gemeinsam** (bis 99), danach Paragon. Jeder Held behält seinen eigenen Skillbaum. Alte Spielstände übernehmen die höchste bisher erreichte Stufe.
- **Tempo 5× und 8×** zusätzlich zu 1×/2×/3× (Taste `F` bzw. Tempo-Knopf).
- **Kein automatisches Pausieren** mehr bei Tab- oder Fensterwechsel; im Hintergrund rechnet das Spiel weiter. Der Browser kann Hintergrund-Tabs nach einigen Minuten stark drosseln.
- Bäume, Felsen und Kristalle sind etwas kleiner, damit sie Türme auf Nachbarfeldern, vor allem auf Anhöhen, nicht verdecken.

**Neu in 2.9.4 (18.10.2026):**
- **Fusionen in der Turmübersicht:** Stehen zwei passende Türme nah genug beieinander, zeigt die Übersicht oben einen Bereich „✦ Fusionen“ mit beiden Türmen, dem Ergebnis und dem Stand.
  - Ist das Paar bereit, kann man direkt dort verschmelzen.
  - Fehlt noch etwas, steht dort, was (Stufe, Spezialisierung), dazu ein Knopf „Zeigen“.
  - Türme mit Fusionspartner werden nie als „ausgebaut“ ausgeblendet.
- **Verschmolzen wird immer in Richtung Lane:** Der legendäre Turm entsteht auf dem Feld, das näher am Weg liegt (im Mazing: an der Gegnerroute), egal von welchem Turm aus man verschmilzt.

**Neu in 2.9.3 (18.10.2026):**
- **Turmübersicht, Turmpanel und Charakterfenster sind im Kampf wieder bedienbar.** Bisher wurden sie bei jeder Gold- oder Schadensänderung komplett neu aufgebaut. Die Knöpfe blinkten dadurch, und Klicks (auch auf die Spezialisierung) gingen verloren.
  - Neu aufgebaut wird nur noch bei strukturellen Änderungen, nie während Maustaste oder Finger gedrückt sind. Live-Werte werden direkt aktualisiert.
  - Die Scrollposition bleibt erhalten.
- **Tiefe Felder neben Anhöhen sind zuverlässig bebaubar.** Die Feldauswahl schneidet den Sichtstrahl jetzt exakt mit dem Gelände. Das alte Näherungsverfahren lieferte an Höhenkanten in rund 9 % der Klickpositionen das falsche Feld, jetzt sind es unter 1 % (Abweichungen nur auf Pixelebene).

**Neu in 2.9.2 (18.10.2026):**
- **„Alle verkaufen“** in der Turmübersicht (Taste `O` bzw. „Türme“) für alle Türme sowie in der Gruppenauswahl (Doppelklick auf einen Turm) für alle Türme einer Sorte.
- Der Knopf zeigt den Erlös; zur Sicherheit muss man innerhalb von 4 s zweimal klicken.
- In der Mini-Pause ist Verkaufen weiterhin gesperrt.
- Gesockelte Runen kehren in den Beutel zurück.
- Die Kopfleiste bricht nicht mehr um.

**Neu in 2.9.1 (18.10.2026):** Bäume, Felsen, Kristalle und Ruinen lassen sich über das ganze Modell anklicken (auch über die Baumkrone), nicht mehr nur über das Bodenfeld. Klarstellung: Die größeren Felder sind reine Darstellung. Reichweiten, Wege und alle Spielwerte bleiben in Feldern gemessen unverändert; ein Test sichert das ab.

**Neu in 2.9 (18.10.2026):**
- **Mehr Platz pro Feld:** Türme, Gegner, Bosse und Helden sind im Verhältnis zum Feld 20 % kleiner, Felder und Lanes wirken dadurch luftiger. Die Kamera startet näher dran, sodass die Felder größer erscheinen; „Ganze Karte“ zeigt weiterhin alles.
- **Paragon-Baum „Türme“** mit 8 Knoten, die nur auf Türme wirken:
  - Turmmacht, Turmtakt, Weitblick (Reichweite)
  - Zielwasser (kritische Chance), Durchschlag (Panzerbrechung)
  - Sprengkraft (Flächenradius), Baumeister (bis −10 % Baukosten), Goldader (Goldgräber-Ertrag)
  - Turmmacht und Turmtakt sind aus Offensive bzw. Utility hierher umgezogen; vergebene Punkte bleiben erhalten.
  - Paragon hat damit 4 Bäume, 22 Knoten und 805 Ränge.

**Neu in 2.8 (17.10.2026):**
- **Neuer Turm Minenwerfer** (Taste `#`, im Helden-Endlos ab Welle 6, 130 Gold):
  - Wirft im Umkreis Minen mitten auf die Lane. Sie liegen bis zu 60 s, auch über das Wellenende hinaus, und explodieren, sobald ein Bodengegner darauftritt. Flieger lösen sie nicht aus.
  - Höchstens 6 Minen gleichzeitig, mit Upgrades bis 9.
  - Upgrades: Schwarzpulver, Großes Magazin, Splitterladung.
  - Spezialisierungen: **Streuminen** (2 Minen je Wurf, mehr Minen gleichzeitig) und **Schockminen** (Betäubung, spröde Ziele).
- **Neue Fusion Minenwerfer + Sporenwerfer = Sporenminenfeld:** Sporenminen explodieren mit Giftschaden, tragen starke Giftstapel auf, verlangsamen und hinterlassen eine Sporenwolke, die weiter vergiftet.

**Neu in 2.7 (17.10.2026):**
- **Dauerhafte Heldenstufen bis 99:** Stufe, EP und Skillbaum jedes Helden bleiben über alle Läufe erhalten. Ab Stufe 30 wachsen die Grundwerte nur noch halb so schnell.
- **Paragon bis 500,** gemeinsam für alle Helden: Ab Stufe 99 fließen alle EP ins Paragon. Jede Stufe gibt einen Punkt in den Bäumen Offensive, Defensive oder Utility (16 Knoten, 620 Ränge).
- **Runen:**
  - nur findbar, ab Welle 250
  - 8 Arten in 3 Qualitäten
  - in Turmsockel einsetzbar: 1 ab Stufe 2, 2 ab Stufe 4, 3 bei legendären Türmen
  - kostenlos wieder herausnehmbar
- **Versionsnummer** im Spiel: rechts unten, im Pausenmenü und im Hauptmenü.
- **Freie Kamera:**
  - mittlere Maustaste oder Strg/Alt + Ziehen dreht beliebig und neigt (17°–80°)
  - `,`/`.` drehen um 15°, `Bild↑`/`Bild↓` neigen, `Pos1` bzw. ⟲ setzt zurück
  - Touch: mit zwei Fingern drehen

**Neu in 2.6 (17.10.2026):**
- **Goldgräber belegen 2×2 Felder.** Die Felder müssen frei und eben sein; der Ertrag von Goldader und Kristallvorkommen zählt, wenn eines der vier Felder darauf liegt.
- **„Alle ▲“:** wertet alle Türme bis Stufe 4 auf, solange das Gold reicht. Die günstigste Stufe kommt zuerst, eine Spezialisierung wird nie automatisch gewählt.
- **Doppelklick auf einen Turm** wählt alle Türme dieser Sorte; sie lassen sich dann gemeinsam aufwerten.
- **Kristalllaser abgeschwächt:**
  - Grundschaden 2,8 statt 3,5, Reiner Kristall +1,2 statt +1,5
  - −20 % je weiterem Laser in 3 Feldern (höchstens −60 %)
  - −35 % in höchstens 3 Feldern Abstand zu einem Riss
- **Turmübersicht:** Die Scrollposition bleibt erhalten, voll ausgebaute Türme werden ausgeblendet (umschaltbar), und es gibt einen Knopf „Alle aufwerten“.

**Neu in 2.5 (17.10.2026):**
- **Sechs neue Fusionen** (jetzt 10): Dornenhain, Donnerspitze, Rissportal, Phönixhorst, Klangkathedrale und Sturmauge. Mögliche Fusionen und Partner in Reichweite erscheinen schon beim Platzieren, mit einem goldenen Ring um das Feld.
- **Alle neun Kampagnenhelden im Helden-Endlos spielbar**, jeweils mit eigenen Werten, vier Fähigkeiten und einem Skillbaum. Freigeschaltet werden sie über die Kampagne oder über eine Bestwelle.
- **Kernwall in jedem Skillbaum:** +3 Kernenergie je Rang.
- **Brann überarbeitet:** hält 2 Gegner gleichzeitig auf, ist deutlich zäher und hat „Herausforderung“ (Wurzeln).
- **Abbau:** Der Held kann Bäume und Felsen abbauen. Das dauert eine Wellenlänge, lässt sich nicht unterbrechen und bringt zufällig Gold und Ressourcen; danach ist das Feld frei.
- **Spezialfelder:** Goldadern zahlen einen festen Zusatz; neue Kristallvorkommen geben dem Goldgräber Kristall.
- **Optik:** deutlich weniger Glühen und Bloom; Gegner, Bosse und Helden haben neu modellierte, detailliertere Modelle.
- **Tastenbelegung frei änderbar** unter Einstellungen.

**Neu in 2.4 (17.10.2026):** Heldenleiste rund 20 % größer, Charakterbogen und Laden breiter und größer. Ein Klick auf einen fusionsfähigen Turm zeigt jede mögliche Fusion mit allen Voraussetzungen (Stufe 4 und Spezialisierung beider Türme, Partner und Abstand, Gold) und was noch fehlt. In der Mini-Pause können keine Türme verkauft werden.

**Neu in 2.3 (17.10.2026):** Turmfähigkeiten lösen ab Stufe 2 automatisch aus. Flieger folgen dem Weg (dafür zäher und schneller). Mini-Pause ohne Menü (Leertaste im Kampf / „Mini“), in der weiter gebaut und aufgewertet werden kann. Im Helden-Endlos: Türme werden nach Wellen freigeschaltet (W1–W17, Konter und Fusionspartner rechtzeitig), Held festsetzen (Z/⚓), passiver Baum mit 1 Punkt je 10 Bestwellen bis Welle 1000 (100 Ränge).

**Neu in 2.2 (17.10.2026):** Nächste Welle per Knopf/Enter (im Helden-Endlos auch vorzeitig), Turmübersicht (Taste O) mit Aufwerten direkt im Fenster, Türme über das ganze Modell anklickbar, Gegner anklickbar mit Live-Werten (LP, Schild, Tempo, Resistenzen, Effekte), Sperre der Kameradrehung (Schloss-Knopf).

Stand 16.10.2026: Die komplette Roadmap (`docs/ROADMAP.md`, Teile A–C) ist umgesetzt – Details und Abweichungen stehen dort im Abschnitt „Umsetzungsstand“.

## Schnellstart

Voraussetzung: Node.js ≥ 20. Die einzigen Entwicklungsabhängigkeiten sind `esbuild`, `typescript`, `tsx`, `playwright` und `@types/node`. Das Spiel selbst hat **keine Laufzeitabhängigkeiten**.

```bash
npm install            # Abhängigkeiten installieren
npm run dev            # Entwicklungsserver mit Rebuild: http://localhost:5173/
npm run build          # Produktionsbuild nach dist/
npm run preview        # dist/ ausliefern: http://localhost:4173/
npm run typecheck      # strikte TypeScript-Prüfung
npm test               # Unit-, Integrations- und Gameplay-Tests (node:test, 110 Tests)
npm run e2e            # Browser-Tests gegen dist/ (vorher npm run build)
npm run balance        # Balance-Auswertung mit Bot-Spielern (optional: npm run balance -- 12 normal classic large)
npm run balance:hero   # Helden-Endlos gegen normales Endlos per Bot (optional: -- 6 45)
npm run check          # alles nacheinander: typecheck → test → build → e2e
```

`dist/` ist eine rein statische Seite und lässt sich auf jedem Webserver ausliefern. Über HTTPS registriert sie einen Service Worker und ist als PWA installierbar und offline spielbar.

## Umgesetzte Features

| Bereich | Umsetzung |
|---|---|
| Welten | Seed-basiert (kopierbar), 7 Biome (u. a. Nebelsumpf, Leuchtgrotte), drei Kartengrößen (Normal 22×14, Groß 32×20, Riesig 42×26) mit Weggabelungen aus mehreren Rissen, Minikarte, Höhenstufen (Anhöhe +0,4 Reichweite), Hindernisse, Goldadern (+50 % Minenertrag), Leylinien (+15 % Schaden), Geländeereignisse. Getrennte Zufallsströme für Layout, Ressourcen, Höhen, Deko, Wellen, Kampf und Modifikatoren. Validierung mit begrenzten Versuchen und kontrolliertem Fallback. |
| Classic / Mazing | Fester Weg mit Kurven bzw. freies Labyrinth: Türme und Barrikaden blockieren, die Route wird über Distanzfelder (Dijkstra) neu berechnet, Routenvorschau vor dem Bau, vollständige Blockade und eingesperrte Gegner werden verhindert. |
| Türme | 18 Türme + Barrikade (Mazing): Waldläuferposten, Glutwerfer, Frostwächter, Sturmrufer, Sporenwerfer, Belagerungsarmbrust, Runenweber, Resonator, Goldgräber, Leitpylon, Kanonenschmiede, Rissbinder, Meteorbeschwörer, Tesla-Spule (Lichtbögen zwischen Spulen), Gravitationsbrunnen, Drachenhorst, Kristalllaser, Totemschrein. Jeweils 3 Stufen + 2 Spezialisierungen, sichtbare Ausbaustufen am Modell, aktive Fähigkeiten ab Stufe 2 (automatisch nach Ablauf der Abklingzeit, Taste C von Hand), Fusion zweier voll ausgebauter Türme zu 4 legendären Türmen (Plasmawerfer, Gletscherkanone, Leerenlanze, Sternenfall). |
| Helden | 9 Helden (3 Kämpfer, 6 Unterstützer) mit je 2 Fähigkeiten + Ultimate, Stufen 1–5 mit Perk-Wahl, Auren, Duo-Synergien und Resonanz „Heldenchor“. Einsatz auf dem Weg mit Rissenergie, Einsatzzeit, Abklingzeit, längere Erholung nach einer Niederlage, Verhalten Halten/Vorrücken/Rückzug. Auswahl von bis zu 2 Helden vor der Partie, Freischaltung über die Kampagne. |
| Wirtschaft | Kopfgeld, Wellenbonus, Bossbelohnung, Minen (zahlen ab der Welle nach dem Bau), Zinsen-Modifikator. Verkauf 70 %, volle Erstattung nur in derselben Bauphase, also keine Geldvermehrung möglich. |
| Elemente | Dampfstoß, Thermoschock, Überladung, Giftbrand, Resonanzbruch. Abklingzeiten je Gegner, Reaktionsschaden löst keine weiteren Reaktionen aus, harte Obergrenzen für Verlangsamung und Markierung. |
| Netzwerke | Pylon-Verbindungen mit Reichweite und Verbindungslimit, sichtbaren Linien und Bonus des stärksten Pylons. 4 Resonanzen (z. B. Feuer+Frost+Blitz). Neuaufbau nur bei Änderungen (Dirty-Flag). |
| Gegner | Läufer, Schwarm, Gepanzerte, Regenerierer, Flieger, adaptive Schildträger, Phasenwandler, Netzwerk-Saboteure, Pfadspäher, Brutmütter sowie neu Höhlengolem (zerfällt), Schattenschleicher (getarnt, Detektoren), Belagerungsbestie (rammt Türme), Heilerschamane, Tunnelgräber (gräbt sich ein) und Sturmgreif (weicht aus). Elite-Varianten mit Erdstoß und 5 Mutationen (gepanzert, hastig, explosiv, spaltend, vampirisch). |
| Bosse | Risskoloss, Aschenkönigin, Nullhüter sowie neu Schlund der Tiefe (verschlingt Türme, gibt sie beim Tod zurück), Zwillingswächter (heilen sich gegenseitig, Raserei beim Tod des Partners) und Weltenbrecher (Schwachpunkte, Weltenstampfer). Bossauftritt mit Kamerafahrt und Titel, sichtbare Phasenwechsel. |
| Roguelite | 17 Modifikatoren mit implementierten Effekten (inkl. Heldenmut und Rissanzapfung, nur mit Helden). Angebot aus 3 vereinbaren Optionen, mindestens eine ist sofort nutzbar; Überspringen bringt +30 Gold. |
| Helden-Endlos | Frei beweglicher Held (Lyra/Brann/Vela) mit Raster-Pathfinding auf Feldern und Lane, Attributen, EP-Kurve, Skillleiste (A/S/D/W, Zielwahl) und Skillbaum; Turmverstärkungen in der Turmwertberechnung; EP-Kugeln, Ressourcen, Tränke, Fragmente, Items ohne Duplikate; Laden und Schmiede (atomare Herstellung, Verstärken, Kombinationen); Sammlung und Heldeninventar getrennt; Auto-Wellen mit Countdown, Wellenprofile, Mini-Bosse; Perks und Meilensteine über die Bestwelle; Draufsicht (V) und Heldenverfolgung (Y). Details: `docs/HERO_ENDLESS.md`. |
| Modi | Schnelle Partie (20 Wellen), Helden-Endlos, Kampagne (10 Missionen mit eigenen Regeln, Heldenvorgaben, Schutzmission und Epilog), Endlosmodus (begrenzt polynomielle Skalierung, Boss alle 10 Wellen), Tagesherausforderung (Tages-Seed, feste Regeln, offline), eigene Karten. |
| Speicherung | Versioniert (v3 mit Migration), Helden im Spielstand, feldweise validiert. Beschädigte Daten werden gesichert statt gelöscht. Fortsetzen ab der letzten Bauphase. Bestwerte, Kampagnenfortschritt, Tagesergebnisse und Einstellungen werden lokal gespeichert. |
| UI | Alle Oberflächen auf Deutsch, eigene SVG-Symbole und aus den 3D-Modellen gerenderte Turm-, Gegner- und Heldenbilder statt Emojis, drehende 3D-Turmvorschau, eigene Schrift (Cinzel/Nunito Sans mit Ersatzschriften), animierte Übergänge, Wellen- und Bossbanner, hochzählendes Gold, Heldenleiste, Minikarte, Erfolge und Skins. Tastatur- und Touch-Bedienung, responsive, skalierbare UI, Bewegung reduzierbar. |
| Grafik | WebGL2 mit weicher Beleuchtung (Normalen), Schattenkarte (PCF), MSAA, Bloom, Farbstimmung je Biom und Geländeereignis, Vignette, Tageszeit im Wellenverlauf, Bosslicht. Gelände mit weichen Kanten, Wegband mit Randsteinen und Fahrspuren, Gras im Wind, animiertes Wasser/Lava/Wolkenmeer, Biom-Wetter. Sprite-Partikel (Glühen, Funken, Rauch, Ringe), verästelte Blitze, Feuer mit Rauch, Meteore, Laser, Explosionen mit Trümmern, Bodendekale, Todesauflösung, Statuseffekte am Körper, Trefferblitz. Automatische Qualitätsstufen. |
| Menü | Lebendige Hintergrundwelt: Ein Bot spielt eine echte Partie mit Türmen, Helden und Wellen, die Kamera kreist langsam. |
| Audio | Vollständig synthetisch (Web Audio): generative Musik mit Kampf- und Bossintensität, Kriegstrommeln, Arpeggien mit Echo, Hall über eine erzeugte Impulsantwort, über 45 Effekte (u. a. Bossauftritt, Heldeneinsatz, Stufenaufstieg, Fusion), getrennte Regler für Gesamt, Musik und Effekte, Stummschaltung. Start erst nach einer Nutzeraktion. |
| Editor | Start und Ziel setzen, Weg malen, Bauflächen, Hindernisse, Kein-Bau-Zonen, Goldadern und Leylinien. Live-Validierung, Kartencode exportieren und importieren, lokal speichern, nur gültige Karten spielbar. |

## Steuerung

Alle hier genannten Tasten sind Standardtasten und lassen sich unter **Einstellungen → Tastenbelegung** ändern. Ist eine Taste schon vergeben, tauschen die beiden Aktionen ihre Tasten.

**Maus & Tastatur:** Mit `1`–`0`, `−`, `=`, `M`, `N`, `J`, `K`, `L`, `I` wählst du einen Turm, mit `B` eine Barrikade. Turmfähigkeiten lösen ab Stufe 2 automatisch aus; `C` löst sie beim ausgewählten Turm von Hand aus. Helden: `Z`/`X` Held 1/2 einsetzen bzw. auswählen (dann Wegzelle anklicken), `A`/`S` Fähigkeiten, `D` Ultimate, `V` Verhalten wechseln; eine angeklickte Wegzelle versetzt den Ankerpunkt. Klick baut, Umschalt+Klick baut mehrere. Ein Klick auf einen Turm öffnet seine Details. Weitere Tasten: `U` Aufwerten, `T` Zielwahl, `Entf` zweimal Verkaufen, `Leertaste` Welle starten bzw. im Kampf Mini-Pause (ohne Menü, Bauen und Aufwerten möglich), `P`/`Esc` Pause mit Menü, `F` Tempo (1×/2×/3×/5×/8×), `Q`/`E` Kamera drehen, Mausrad Zoom, Ziehen verschieben, `R` alle Reichweiten, `G` Raster, `H` Hilfe.

**Helden-Endlos:** Linksklick auf ein freies Feld oder Rechtsklick bewegt den Helden. `A`/`S`/`D`/`W` lösen Fähigkeiten aus (Ziel-Fähigkeiten: danach Ziel anklicken). `Umschalt`+`1`–`5` nutzt Tränke, `Tab` öffnet den Charakter, `X` Laden und Schmiede, `Y` die Kameraverfolgung, `V` die Draufsicht, `Enter` die nächste Welle (im Kampf vorzeitig). In allen Modi: `O` Turmübersicht, Klick auf einen Gegner zeigt seine Live-Werte, der Schloss-Knopf sperrt die Kameradrehung.

**Touch:** Turm in der Leiste antippen, dann ein Feld antippen. Es erscheint eine Vorschau mit Reichweite (im Mazing auch die neue Route). Bestätigen mit „Hier bauen“ oder durch erneutes Tippen auf dasselbe Feld. Ein Finger verschiebt die Karte, zwei Finger zoomen. Die Kameraknöpfe unten rechts (Drehen, Zoom, ganze Karte, Minikarte, Boss folgen, Reichweiten, Hilfe) stehen als Alternative bereit. Helden: Porträt antippen, dann eine Wegzelle; Fähigkeiten über die Knöpfe der Heldenkarte. Die Minikarte springt per Antippen.

## Technik und Begründung der Abweichungen

Der Master-Prompt empfiehlt TypeScript + Vite + Three.js + React. **In der Entwicklungsumgebung war die npm-Registry nicht erreichbar**, deshalb konnten Vite, Three.js und react-dom nicht installiert werden. Umgesetzt wurde deshalb:

- **TypeScript (strict)** mit `noUnusedLocals`/`noUnusedParameters`.
- **esbuild** als Bundler statt Vite. Er erfüllt dieselbe Rolle: ES-Module-Bundle, Minifizierung, Sourcemaps und Watch-Modus.
- **Eigener WebGL2-Renderer** statt Three.js (`src/render/`). Orthografische isometrische Kamera mit Schwenken, Zoomen, 90°-Drehung, Kamerafahrten und Bossverfolgung. Jede Mesh-Art wird pro Pass mit einem instanzierten Draw Call gezeichnet (eigener Instanzpuffer je Batch). Glatte Modelle mit Normalen (abgerundete Würfel, glatte Zylinder und Kugeln), Materialien für Wasser, Lava und Wind. Schattenkarte mit PCF, MSAA, Bloom und Farbkorrektur als Nachbearbeitung; Sprite-Partikel additiv bzw. mit Alpha. Statisches Gelände wird einmal gebatcht. Bei niedriger Bildrate senkt die automatische Qualität zuerst Kantenglättung, Schatten und Glühen, dann die Auflösung.
- **Leichtgewichtige DOM-UI** (`src/ui/dom.ts`) statt React. Das HUD rendert nur bei Signaturänderungen neu. Spiellogik liegt nie in der UI: Die UI liest den Zustand und ruft ausschließlich Simulationsbefehle auf.

Eine spätere Migration auf Three.js/React ist möglich, weil Simulation (`src/sim`), Darstellung (`src/render`) und UI (`src/ui`) vollständig getrennt sind.

## Architektur

```
src/
  main.ts                 Einstiegspunkt, Startfehleranzeige, Service Worker
  app/GameApplication.ts  Szenenwechsel, Lebenszyklus, Speicher, Audio, Hintergrundwelt
  app/GameSession.ts      Spielsitzung: Eingabe → Befehle, fester Takt, Ereignisse → Audio/HUD
  app/progress.ts         Ergebnis einer Partie → Fortschritt (rein, getestet)
  core/types.ts           Datenmodelle (TowerDefinition, EnemyInstance, GameState, SaveData …)
  core/rng.ts             deterministische, serialisierbare Zufallsströme, Seeds
  config/*.ts             zentrale Daten: Balance, Türme, Gegner, Bosse, Elemente, Modifikatoren, Biome, Kampagne
  sim/GameSimulation.ts   fester 30-Hz-Takt, Befehle, Dirty-Flag-Caches
  sim/WorldGenerator.ts   prozedurale Karten, Validierung, Fallback
  sim/Pathfinding.ts      Distanzfelder, Erreichbarkeit
  sim/TowerSystem.ts      Werteberechnung, Zielwahl, Angriffe
  sim/EnemySystem.ts      Bewegung, Fähigkeiten, Tod, Durchbruch
  sim/ProjectileSystem.ts · DamageSystem.ts · ElementSystem.ts · NetworkSystem.ts
  sim/EconomySystem.ts · WaveDirector.ts · BossSystem.ts · ZoneSystem.ts · RogueliteSystem.ts · Score.ts
  sim/Bot.ts              automatischer Testspieler (nutzt nur öffentliche Befehle)
  save/SaveSystem.ts      versionierte, validierte Speicherung
  audio/AudioSystem.ts    synthetische Musik und Effekte
  render/                 WebGL2-Renderer, Kamera, Geometrie, Modelle, Gelände, Effekte (fx), WorldView, IconFactory
  config/heroes.ts · achievements.ts   Helden, Duos, Erfolge und Skins
  sim/HeroSystem.ts       Helden: Einsatz, Bewegung, Blocken, Angriffe, Fähigkeiten, Auren, Stufen
  config/heroEndless.ts · champions.ts · items.ts   Helden-Endlos: Werte, Helden, Items, Tränke, Rezepte, Laden
  sim/ChampionSystem.ts · LootSystem.ts · InventorySystem.ts · HeroEndlessSystem.ts   Helden-Endlos-Systeme
  ui/HeroHud.ts · ui/screens/heroSetup.ts   Helden-Endlos-HUD, Laden, Charakterbogen, Startbildschirm
  input/InputController.ts  Maus, Touch, Pinch, Tastatur
  ui/                     HUD, Bildschirme, Texte, Kartenvorschau
  editor/                 Karteneditor und Kartencodes
tests/                    147 Tests (node:test)
scripts/                  build.mjs, serve.mjs, e2e.mjs, balance.ts, heroBalance.ts
public/                   index.html, Manifest, Service Worker, Icon
```

**Simulation:** Feste Schrittweite von 1/30 s. Die Spielgeschwindigkeit erhöht die Anzahl der Takte, nicht die Schrittweite. Pro Frame sind höchstens 12 × Tempo Takte erlaubt, damit es nach einem Tabwechsel nicht zu einer Takt-Lawine kommt. Ein versteckter Tab pausiert den Kampf automatisch. Der gesamte `GameState` ist reines JSON, abgeleitete Daten liegen getrennt im `SimCache`. Fehler in einem Takt werden gemeldet, statt die Simulation still anzuhalten. Damit ist der Zustand für eine spätere Koop-Architektur (z. B. Lockstep) vorbereitet.

## Tests – tatsächlich ausgeführt

**`npm test`: 147 von 147 bestanden** (Laufzeit ca. 80 s). Abgedeckt sind:

- Neu in 2.9 (`tests/v29.test.ts`): Paragon-Turmbaum wirkt nur auf Türme (Baukosten mit Deckel, kritische Chance, Panzerbrechung, Schaden, Goldgräber-Ertrag, sofort bei Vergabe im Lauf)

- Neu in 2.8 (`tests/v28.test.ts`): Minenwerfer wirft nur auf die Lane in Reichweite und nie mehr als erlaubt; Minen explodieren nur bei Bodengegnern und bleiben über das Wellenende liegen; Fusion zum Sporenminenfeld vergiftet und hinterlässt eine Sporenwolke

- Neu in 2.7 (`tests/v27.test.ts`): dauerhafte Heldenstufe mit Skillbaum und Kernwall, Stufendeckel 99 und Paragon bis 500, Paragonpunkte mit Wirkung, Bollwerk und Zurücksetzen, dauerhaftes Speichern ohne Rückschritt und Bereinigung, Runen erst ab Welle 250, Sockel nach Stufe, Runenwirkung, Gierrune, Rückgabe beim Verkauf, freie Kamera (Gier, Neigungsgrenzen, Zurücksetzen)

- Neu in 2.6: Alles aufwerten (Stufe 4, keine Spezialisierung, Gold reicht nicht für alles), Gruppen-Aufwerten, Kristalllaser-Dämpfung, Goldgräber 2×2

- Neu in 2.5 (11 Tests in `tests/v25.test.ts`): alle 10 Fusionen und die Fusionsvorschau vor dem Bau, alle vier Fähigkeiten aller 12 Helden, Freischaltung der Kampagnenhelden, Brann blockt mehrere Gegner, Kernwall erhöht die Kernenergie, Abbau (Dauer, nicht unterbrechbar, Ertrag, Feld danach frei und bebaubar, nicht abbaubare Felder), Goldader-Zusatz, Kristallvorkommen, Tastenbelegung (keine Standardkonflikte, Tauschen, Übersetzen, Bereinigen, Speichern)

- Helden-Endlos (21 Tests, Details in `docs/HERO_ENDLESS.md`): eigenständiger Start, Bewegung auf Feldern und Lane, Türme blockieren nicht, Auto-Welle nach 15 s, Pause, EP ohne Doppelgutschrift, Stufen und Attribute, Skills und Abklingzeiten, Turmverstärkung anwenden und entfernen, Kämpferangriff, Fragmente → Item, keine identischen Drops, Ausrüstung, Tränke, atomare Herstellung, Kaufprüfungen, Laden auf allen Kartenarten, Speichern der Freischaltungen, Laden ohne Duplikate, alte Modi unverändert

- Goldtransaktionen, Baukosten, Doppelplatzierung, Upgradekosten, Spezialisierung, Verkaufserstattung, kein Gewinn durch wiederholtes Kaufen und Verkaufen, Minenertrag, einmalige Todesbelohnung
- Resistenzen, Panzerbrechung, Markierungs-Obergrenze, alle Elementarreaktionen, Abklingzeiten, Obergrenze der Verlangsamung, adaptiver Schild, Phasenimmunität, Wirkung aller Kampftürme
- Netzwerkaktivierung und -deaktivierung, Resonanz, Verbindungslimit, kein Neuaufbau pro Frame
- 400 Seeds je Variante liefern gültige Karten, Seed-Reproduzierbarkeit, Fallback, Validierung defekter Karten; große und riesige Karten sind gültig und haben Weggabelungen, Gegner verteilen sich auf alle Risse
- Mazing über 50 Seeds mit je 160 zufälligen Bauversuchen: der Weg bleibt immer offen; Bau während laufender Wellen
- Bossphasen aller Bosse, Schildbruch, Unterbrechung, Schwachpunkte des Weltenbrechers, Zwillinge mit Raserei, Schlund gibt verschlungene Türme zurück
- Helden: Rissenergie, Platzierung nur auf dem Weg, Einsatzzeit und Abklingzeit nur im Kampf, Niederlage, Stufenaufstieg mit Perk-Wahl, Fähigkeiten und Ultimate, Duo-Synergie, Blocken, Schutzmission (Niederlage beim Fall), Freischaltung über die Kampagne, Heldenmodifikatoren, Heldenchor
- Fusion (nur voll ausgebaut und benachbart, legendäre Türme nicht baubar), aktive Turmfähigkeiten, Totem-Aura, Tarnung und Detektoren, Mutationen
- Speichern und Laden (inkl. Helden), beschädigte Daten, Versionsschutz, exakte Fortsetzung, Kartencodes, Fortschritt, Erfolge und Skins
- Pause, Spielgeschwindigkeit, Determinismus bei unterschiedlichen Frame-Längen, Tabwechsel
- Modifikatorangebot und -wirkung, Endlosskalierung, Tagesherausforderung
- Integration: komplette Partien bis zur Niederlage und bis zum Sieg (Bot), Bot auf großen und riesigen Karten mit Helden

**`npm run e2e`: 47 von 47 Prüfungen bestanden** (neu in 2.9.5: Tempo 5×/8×, Weiterlaufen im Hintergrund, legendären Turm versetzen; neu in 2.9.4: Fusion aus der Turmübersicht, Ergebnis am Weg; neu in 2.9.3: Turmübersicht und Turmpanel im laufenden Kampf mit langsamem Klick; neu in 2.9.2: Alle verkaufen mit Bestätigung und Mini-Pause-Sperre; neu in 2.9.1: Hindernis über die Modellspitze anklicken; neu in 2.8: Minenwerfer wirft Minen auf die Lane; die Schritte 2.6–2.8 laufen in einem frischen Browser-Kontext; neu in 2.7: Versionsanzeige, Stufe 99 und Paragon-Bäume, Runen sockeln/herausnehmen, freie Kamera per mittlerer Maustaste und Taste, Zurücksetzen; neu in 2.6: Doppelklick-Gruppe, Alles aufwerten, Turmliste scrollt und blendet Ausgebaute aus, Goldgräber 2×2; neu in 2.5: Kampagnenheld, Abbau, Fusionshinweis, Tastenbelegung; neu in 2.3: passiver Baum, Turmsperre, Mini-Pause mit Aufwerten, Held festsetzen; neu in 2.2: Turmklick auf die Modellspitze, Turmübersicht mit Aufwerten, Drehsperre, Gegnerinfo per Klick, vorzeitige nächste Welle; davon 7 für Helden-Endlos: Start mit Heldenwahl, Rechtsklick-Bewegung, Skills mit Zielwahl, Laden und Trank, Schmiede/Anlegen/Skillbaum, Pause und Auto-Welle, dauerhafte Freischaltung nach Neuladen) gegen den **Produktionsbuild** in Chromium (headless, Software-WebGL, Qualitätsstufe „Niedrig“ wegen der langsamen Software-Grafik):

- Desktop: Menü mit lebendiger Hintergrundwelt, Weltvorschau, Tutorial, Bau per Taste und Klick (HUD-Gold korrekt), Aufwerten, Verkauf mit Bestätigung, Welle, Tempo 3×, Pause, Held per Z einsetzen und Fähigkeit per A, aktive Turmfähigkeit per C, Speicherung zum Fortsetzen, Modifikatorauswahl, Bossanzeige, Niederlage mit Ergebnisbildschirm, Neustart mit gleichem Seed, Mazing mit Blockadeschutz, große Karte mit Weggabelung und Minikarten-Sprung, Kampagne mit Pause → Menü → Fortsetzen, Tagesherausforderung, Endlosmodus, Editor, Einstellungen nach Neuladen, Kodex (Türme, Helden, Fusion), Erfolge, Skins, keine Laufzeitfehler.
- Smartphone (844×390, Touch): Start per Tippen, Bauen über Vorschau und Bestätigen sowie doppeltes Tippen, Turmdetails, Welle starten, Hochformat 390×844, Bedienelemente ≥ 44 px, Pause, keine Laufzeitfehler.

**Balance** (`npm run balance -- 3 …`, 6 Bot-Profile mit Helden, 3 Seeds je Kombination):

| Kartengröße | Entspannt | Normal | Hart |
|---|---|---|---|
| Normal (Classic + Mazing) | 92 % | 36 % | 8 % |
| Groß + Riesig (Classic) | 89 % | 42 % | 25 % |

Angepasst wurden auf dieser Grundlage: LP-Faktor Normal 1,0 → 1,1 und Hart 1,12 → 1,25 (die neuen Gegner sind härter, Helden und Turmfähigkeiten aber stärker); Tunnelgräber tauchen nach 7 Feldern wieder auf; Schattenschleicher werden von jedem Turm aus nächster Nähe erkannt; Sturmgreif weicht 20 % statt 30 % aus; große Karten erhalten mehr Startgold und Kernenergie, das Wellenbudget wächst nur noch leicht. Die Bots bauen jetzt Detektoren und decken die Fluglinie der Flieger ab.

**Verbleibende Annahmen:** Die Bots sind einfache Spieler. Menschliche Spieler dürften höhere Siegquoten erreichen, besonders mit gezieltem Heldeneinsatz. Runenweber, Rissbinder, Pylon und Totem haben geringen Direktschaden pro Gold, weil ihr Wert in Schwächung, Kontrolle, Aufdeckung und Netzwerkboni liegt.

## Bekannte Einschränkungen

- Die Performance auf echter GPU-Hardware wurde nicht gemessen. In der Testumgebung lief nur Software-WebGL (SwiftShader); dort braucht ein Bild in hoher Qualität mehrere Sekunden, in „Niedrig“ unter einer Sekunde. Die Simulation bleibt dabei korrekt, weil sie unabhängig von der Bildrate ist. Für schwache Geräte senkt die automatische Qualität stufenweise MSAA, Schatten, Glühen und dann die Auflösung.
- Die Schriftarten kommen von Google Fonts. Ohne Netz greifen Ersatzschriften.
- Fortsetzen ist nur ab Bauphasen möglich. Ein Spieler kann eine Welle durch Beenden und Fortsetzen wiederholen.
- Das PWA-Icon liegt nur als SVG vor. Manche Android- und iOS-Versionen wünschen zusätzlich PNG-Icons.
- Einige Inhaltssymbole (Modifikatoren, Erfolge, Gegner in Tooltips ohne WebGL) sind weiterhin Emojis und sehen je nach Plattform leicht unterschiedlich aus. Bedienelemente nutzen eigene SVG-Symbole, Türme/Gegner/Helden gerenderte Bilder.
- Die Balance ist mit Bots kalibriert und nicht mit Playtests durch Menschen.

## Nicht implementiert (optional bzw. spätere Versionen)

- Koop und Multiplayer. Bewusst kein Schein-Multiplayer; die Architektur ist dafür vorbereitet.
- Online-Ranglisten. Ohne Backend gibt es keine vorgetäuschte globale Rangliste, Bestwerte sind lokal.
- Online-Teilen von Karten. Karten werden per Kartencode geteilt, ohne Server.

## Hinweise zum weiteren Ausbau

1. Playtests mit Menschen und Telemetrie, z. B. Export der Ergebnisstatistik als JSON.
2. Koop per Lockstep: Befehle (`build`, `upgrade` …) mit Tick-Stempel übertragen. Die Simulation ist bereits deterministisch und befehlsbasiert.
3. Weitere Bosse und Biome rein über `src/config/` ergänzen.
4. Weitere Helden, Fusionen und Erfolge rein über `src/config/` ergänzen.
5. Optional Three.js oder React einführen, sobald die Registry verfügbar ist. Die Schnittstellen `WorldView.frame()` und `HudActions` bleiben dabei unverändert.

Alle Inhalte (Namen, Karten, Modelle, Klänge, UI) sind eigenständig erstellt. Es werden keine fremden Assets verwendet.
