# Kernwacht – Darstellung, Oberfläche, Eingabe: TypeScript → Unity

Die Spiellogik ist bereits fertig übertragen (`Assets/Kernwacht/Sim`, Namespace `Kernwacht.Sim`, Regeln in `PORTING.md`) und
**bitgenau** mit dem Webspiel abgeglichen. Jetzt wird die Präsentationsschicht übertragen: `web/src/app`, `web/src/ui`, `web/src/input`,
`web/src/render`, `web/src/audio` (Kopie unter `/home/claude/kernwacht/src`). Ziel: `unity/Kernwacht_Unity/Assets/Kernwacht/Game/Runtime/`.

Ziel ist ein Unity-WebGL-Browserspiel, das **sich genauso bedient und anfühlt wie das Webspiel**: gleiche Bildschirme, gleiche Texte (deutsch),
gleiche Tasten, gleiche Maus-/Touchsteuerung, gleiche Kamera, gleiche Einstellungen, gleiche Rückmeldungen (Toasts, Banner, Zahlen), gleiche Abläufe.
Bezugsauflösung **1920×1080** (CanvasScaler, skaliert für andere Größen; Einstellung „UI-Größe“ multipliziert).

## Grundregeln
- Namespace `Kernwacht.Game` (Unterordner frei). Unity-APIs erlaubt. Assembly `Kernwacht.Game` referenziert `Kernwacht.Sim`, `Kernwacht.Models`, `Unity.InputSystem`, `UnityEngine.UI`.
- **Gleiche Klassen- und Methodennamen wie in TS** (Methoden camelCase wie im Original, z. B. `GameSession.toggleTopDown()`, `Hud.update(...)`), damit die Teile, die parallel übertragen werden, zusammenpassen. Die Signatur leitest du aus der TS-Quelle ab (Zahlen aus dem Sim sind `double`; reine Darstellungswerte dürfen `float` sein).
- Simulations-Zugriff nur über `Kernwacht.Sim` (z. B. `sim.s.he.champion`, `ChampionSystem.championStats(he)`, `Champions.CHAMPIONS[id]`, Befehle `sim.moveChampion(x, y)`). Namen wie in TS (siehe PORTING.md). Die Sim-Klassen nicht ändern.
- Nichts weglassen, was der Spieler sieht oder bedienen kann. DOM-spezifische Technik (CSS-Animationen, backdrop-filter) sinngemäß nachbilden (Farbe, Transparenz, einfache Tweens).
- Keine Bilddateien voraussetzen. Grafiken prozedural (Theme-Sprites in `UI/Ui.cs`) oder aus den 3D-Modellen (Symbole, siehe unten).
- Texte: Unity-UI `Text` mit Rich Text. `Theme.Font` (LegacyRuntime). Emojis gibt es in dieser Schrift nicht: wo TS Emojis anzeigt, das zugehörige Symbol (Icons, s. u.) oder ein kurzes Textzeichen verwenden.

## Feste Schnittstellen (verbindlich für alle)

### `GameApp` (Port von `app/GameApplication.ts`, Datei `App/GameApp.cs`) – **MonoBehaviour, einziger Einstiegspunkt**
- `public static GameApp I` (Singleton).
- Felder: `public SaveData save;` `public WorldView view;` `public AudioSystem audio;` `public GameSession session;` `public SaveSystem saveSystem;`
- UI-Ebenen (alle `RectTransform` unter einem gemeinsamen Canvas, Reihenfolge von hinten nach vorn):
  `public RectTransform worldLayer` (Lebensbalken, Zahlen über der Welt), `hudLayer` (Spiel-HUD), `screenLayer` (Menüs/Bildschirme), `overlayLayer` (Dialoge über allem), `toastLayer`, `tooltipLayer`.
- Methoden wie TS: `persist()`, `applySettings()`, `resize()`, `toast(string text, string severity = "info", double ms = 3200)`, `go(string screen, ScreenParams p = null)`, `startRun(RunSetup)`, `canResume()`, `resumeRun()`, `heroEndlessSetup(string champion, List<string> startKit)`, `detailLevel()`, `particleCap()`.
- `ScreenParams` = Klasse mit den Feldern des TS-Interfaces.
- Die Hauptschleife: `Update()` → wie `loop()` im TS (Hintergrundwelt im Menü, `session.frame(dt)`).
- Speicher: `PlayerPrefsStorage : ISaveStorage` (PlayerPrefs, `PlayerPrefs.Save()` nach jedem Schreiben – in WebGL landet das in IndexedDB).

### Tastatur: `KeyEvt` (Datei `Input/KeyEvt.cs`, vom App-/Session-Übersetzer angelegt)
`public class KeyEvt { public string key; public bool shiftKey, ctrlKey, altKey, metaKey, repeat; public bool defaultPrevented; public void preventDefault(); }`
`key` ist **wie `KeyboardEvent.key.toLowerCase()` im Browser**: `"a"`…`"z"`, `"0"`…`"9"`, `" "`, `"escape"`, `"enter"`, `"tab"`, `"delete"`, `"backspace"`, `"arrowup"`/`"arrowdown"`/`"arrowleft"`/`"arrowright"`, `"pageup"`, `"pagedown"`, `"home"`, `"end"`, `","`, `"."`, `"-"`, `"+"`, `"="`, `"f1"`… Bei Umschalt+Ziffer liefert der Browser auf deutscher Tastatur z. B. `"!"` – das Webspiel prüft dafür `e.code`; deshalb zusätzlich `public string code` (`"Digit1"`, `"KeyA"`, …).
`InputController` (Port von `input/InputController.ts`, Datei `Input/InputController.cs`) erzeugt diese Ereignisse aus `UnityEngine.InputSystem.Keyboard` und ruft `handlers.key(e)` auf. Zeigerkoordinaten wie im Browser: **Pixel, Ursprung oben links** (`Screen.height - y`). Über UI-Elementen (EventSystem) keine Weltaktionen.

### Welt: `WorldView` (Port von `render/WorldView.ts` + `render/Renderer.ts`-Rolle, Datei `World/WorldView.cs`)
- `public IsoCamera cam` (bereits vorhanden: `Game/Runtime/IsoCamera.cs`; Spielkoordinaten x = Spalte, z = Zeile; `IsoCamera.W(x,y,z)` wandelt in Unity-Weltkoordinaten).
- Öffentliche Methoden mit den Namen aus WorldView.ts, die GameSession aufruft (z. B. `setState`/`sync`/`render(sim, dt)`, `setHover`, `setPreview`, `setSelection`, `flash`, `pick(px, py)` …) – Namen aus der TS-Quelle übernehmen.
- Modelle: `GameAssets.Instance.catalog` (ModelCatalog: `Prefab(id)`, `Spawn(id, pos, rot, parent)`), Komponenten `TowerView` (Turret drehen, `Fire()`), `UnitView` (Laufen, `PlayAttack()`, `PlayDeath()`), `WorldPropView`. Gegner-/Turm-/Boss-/Helden-IDs = Modell-IDs. Champions: `Champions.CHAMPIONS[id].model` ist die Modell-ID.

### Symbole: `Icons` (Datei `UI/Icons.cs`, vom Bildschirm-Übersetzer angelegt)
`public static Sprite Icons.Get(string name)` – für die Symbolnamen aus `ui/icons.ts` (prozedural gezeichnete Sprites, 64×64, weiß, eingefärbt per Image.color) und
`public static Sprite Icons.Model(string modelId)` – Porträt eines 3D-Modells (Turm, Gegner, Held), zur Laufzeit per Render-Textur erzeugt und zwischengespeichert (wie `render/IconFactory.ts`).

### Audio: `AudioSystem` (Port von `audio/AudioSystem.ts`, Datei `Audio/AudioSystem.cs`)
Gleiche öffentliche Methoden (`sfx(id)`, Musik-Steuerung, Lautstärken …). Klänge prozedural erzeugen (AudioClip.Create mit berechneten Samples), wie die WebAudio-Synthese im Original.

### UI-Baukasten
`Game/Runtime/UI/Ui.cs` (`Theme`, `Ui.Panel/Label/Button/Row/Column/Grid/Scroll/Bar/Slider/Check/Seg/Tip`, `TipSource`). DOM-Helfer `h(...)` → diese Bausteine. Erweitern erlaubt nur in **eigenen** Dateien (z. B. `UI/HudKit.cs`), nicht in Ui.cs.
Layout: Anker/Pivot wie das CSS des Webspiels (`web/src/styles.css`), Größen in Bezugspixeln bei 1920×1080 (CSS-px ≈ 1:1, Grundschrift 18 px).

## Arbeitsteilung (jeder schreibt nur seine Dateien)
- **App/Session**: `App/GameApp.cs`, `App/PlayerPrefsStorage.cs`, `App/GameSession.cs`, `Input/InputController.cs`, `Input/KeyEvt.cs`
- **Welt**: `World/*.cs` (WorldView, Gelände, Effekte, Picking, Paletten)
- **Hud**: `UI/Hud.cs` (+ eigene Hilfsdateien `UI/Hud*.cs`)
- **Heldenleiste**: `UI/HeroHud.cs`, `UI/PassiveTreeView.cs`, `UI/MiniMap.cs`
- **Bildschirme**: `UI/Screens/*.cs`, `UI/Text.cs`, `UI/Icons.cs`
- **Audio**: `Audio/AudioSystem.cs`

Aufrufe in fremde Teile über die TS-Namen. Unsicherheiten mit `// PORT:` markieren und im Bericht nennen.
