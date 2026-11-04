# Kernwacht: TypeScript → C# – Übersetzungsregeln

Quelle: `web/src` (Kopie unter `/home/claude/kernwacht/src`). Ziel: `unity/Kernwacht_Unity/Assets/Kernwacht/Sim/`.
Die C#-Simulation muss **exakt dieselben Ergebnisse** liefern wie die TS-Simulation (gleicher Seed → gleiche Karte, Wellen, Beute, Kämpfe).
Deshalb wird **wörtlich** übersetzt: gleiche Struktur, gleiche Reihenfolge, gleiche Formeln, gleiche Namen. Nicht „verbessern“, nicht vereinfachen, nichts weglassen.

## Projekt
- Assembly `Kernwacht.Sim` (`Sim/Kernwacht.Sim.asmdef`, `noEngineReferences: true`). **Kein `UnityEngine`** in der Simulation – nur `System`, `System.Collections.Generic`, `System.Linq`, `System.Text`, `System.Globalization`.
- Namespace `Kernwacht.Sim` für alles.
- C# 9 (Unity 6). Keine `record`-Typen mit init-only (IL2CPP-sicher halten), keine `dynamic`, keine Reflection in der Simulation.

## Dateien und Namen
- Jede TS-Datei wird zu **einer** C#-Datei mit **einer** `public static partial class`, benannt nach der Datei in PascalCase:
  - `config/towers.ts` → `Sim/Config/Towers.cs`, `public static partial class Towers`
  - `config/heroEndless.ts` → `Sim/Config/HeroEndless.cs`, `class HeroEndless`
  - `sim/TowerSystem.ts` → `Sim/Systems/TowerSystem.cs`, `class TowerSystem`
  - `core/rng.ts` → `Sim/Core/Rng.cs` (die TS-Klasse `Rng` wird die C#-Klasse `Rng`; freie Funktionen der Datei in `public static class RngUtil`)
  - `core/types.ts` → `Sim/Core/Types.cs` (Typen auf Namespace-Ebene; freie Konstanten/Funktionen in `public static class Types`)
- **Alle Bezeichner bleiben exakt wie in TS**: exportierte Konstanten (`TOWERS`, `HERO_ENDLESS`), Funktionen (`computeTowerStats`), Interfaces (`TowerDefinition`), Felder (`maxHp`, `he`, `champion`). Methoden also in camelCase. Dadurch lässt sich jeder TS-Import vorhersagbar auflösen:
  `import { computeTowerStats } from './TowerSystem.ts'` → `TowerSystem.computeTowerStats(...)`
  `import { TOWERS } from '../config/towers.ts'` → `Towers.TOWERS`
- Nicht exportierte (modulinterne) Funktionen/Konstanten: `private`/`internal static` in derselben Klasse, Name unverändert.
- C#-Schlüsselwörter als Namen (`base`, `params`, `event`, `object`, `fixed`, `default`, `in`, `out`, `ref`, `checked`, `lock`, `operator`, `string`): mit `@` voranstellen (`@base`, `@params`).
- Namenskonflikt Klasse/Member (z. B. Klasse `Towers` und Feld `towers`): erlaubt, da Groß-/Kleinschreibung unterschiedlich. Konflikt Klasse/Methode gleichen Namens: Methode behält den TS-Namen, Klasse bekommt das Suffix `Module` (nur wenn wirklich nötig, in dieser Datei vermerken).

## Typen
| TS | C# |
|---|---|
| `number` | **`double`** (immer, auch für Zähler/IDs/Wellen in Zustand und Signaturen). Lokale Schleifenindizes dürfen `int` sein. Listenindex: `list[(int)x]`. |
| `string`, String-Union (`'fire' \| 'frost'`) | `string` |
| `boolean` | `bool` |
| `T[]`, `readonly T[]` | `List<T>` (Zustand und Rückgaben); feste Konstantenlisten dürfen `T[]` sein, wenn TS sie nie verändert |
| `Record<K, V>`, `Partial<Record<K, V>>`, `{ [k: string]: V }` | `Dictionary<string, V>` (fehlender Schlüssel = TS `undefined`) |
| `Map<K,V>` / `Set<T>` | `Dictionary<K,V>` / `HashSet<T>` (Zahl-Schlüssel: `double`) |
| `Float64Array`/`Float32Array`/`Int32Array`/`Uint8Array` | `double[]`/`float[]`→`double[]`/`int[]`/`byte[]` |
| Interface / Objekttyp | `public class` mit **öffentlichen Feldern** (keine Properties), gleiche Feldnamen |
| optionales Feld `x?: number` | `double? x` ; `x?: boolean` → `bool? x` ; `x?: string`/Objekt → Referenztyp, `null` = undefined |
| `x: T \| null` | Referenztyp mit `null`; bei Zahlen `double?` |
| Diskriminierte Union (`{k:'a',...} \| {k:'b',...}`) | **eine** Klasse mit allen Feldern (optional/nullable) und dem Diskriminator-Feld |
| `Partial<Stats>` / `Stats` (`Record<StatKey, number>`) | `Dictionary<string, double>` |
| Tupel `[number, number]` | `double[]` (Länge 2) |
| Funktionstyp `(x: A) => B` | `Func<A,B>` / `Action<A>` |

Objektliterale mit Ad-hoc-Form, die zurückgegeben werden: kleine `public class` in derselben Datei (Name sinnvoll, z. B. `FusionOption`). Wenn `core/types.ts` schon einen passenden Typ hat, diesen nehmen.

## Semantik genau erhalten
- `a ?? b`, `a?.b` → identisch in C# (bei `double?` → `(a ?? b)`).
- **Truthiness**: TS `if (x)` bei Zahlen = `x != 0 && !NaN`, bei Strings = `!string.IsNullOrEmpty`, bei Objekten = `!= null`. Explizit ausschreiben. `x || y` bei Zahlen: `(x != 0 ? x : y)` – Hilfsfunktion `U.Or(x, y)`.
- `Math.floor/ceil/round/abs/min/max/sqrt/pow/hypot/sign/trunc` → `Math.Floor/...`. **`Math.round` in JS rundet .5 immer nach oben** (auch bei negativen Zahlen: −2,5 → −2): **immer `U.Round(x)`** benutzen, nie `Math.Round`.
- `Math.min/max` mit vielen Argumenten → verschachteln oder `U.Min(...)`.
- `x % y` – gleiches Vorzeichenverhalten wie JS (C# `%` auf double ist identisch).
- `Number.isFinite` → `U.IsFinite`, `isNaN` → `double.IsNaN`.
- Ganzzahl-Bitoperationen (`|0`, `>>> 0`, `Math.imul`, `^`, `<<`) → `U.ToInt32`, `U.ToUint32`, `U.Imul` in `unchecked`-Blöcken exakt nach JS-Semantik.
- `arr.sort(cmp)`: JS-Sort ist **stabil** → `U.StableSort(list, cmp)` (nicht `List.Sort`, die ist instabil!). Comparer gibt double zurück → Vorzeichen auswerten.
- `arr.filter/map/find/findIndex/some/every/reduce/includes/indexOf/slice/splice/push/pop/shift/unshift/concat/join/reverse/flatMap` → LINQ oder `U`-Hilfen (`U.Splice`, `U.Slice`, `U.Shift` …). Reihenfolge erhalten.
- `for (const x of list)` über eine Liste, die in der Schleife wächst (`push`): JS iteriert neu hinzugefügte Elemente **mit**! In C# → Index-Schleife `for (int i = 0; i < list.Count; i++)` (Count jedes Mal neu lesen). `foreach` nur, wenn sich die Liste sicher nicht ändert.
- `Object.keys/values/entries` über Dictionaries: Einfügereihenfolge wie JS (`Dictionary` ohne Löschen hält die Reihenfolge). Nach `delete` + erneutem Einfügen kann sie abweichen – dann `U.OrderedDict` verwenden (in U vorhanden) oder eine Schlüsselliste mitführen.
- Ganzzahlschlüssel in JS-Objekten (`{ 10: 'ashQueen' }`) sortiert JS aufsteigend vor Stringschlüsseln – beim Iterieren beachten.
- `JSON.parse(JSON.stringify(x))` → `U.DeepClone(x)`.
- `structuredClone` → `U.DeepClone`.
- Template-Strings → `$"..."`. Zahlenformat in Texten: `U.Fmt(x)` (wie JS `String(x)`), `x.toFixed(n)` → `U.Fixed(x, n)`, `toLocaleString('de-DE')` → `U.De(x)`.
- `throw new Error(..)` → `throw new Exception(..)`.
- Getter/Setter in Klassen → Methoden oder Properties, Name unverändert.
- Default-Parameter (`function f(a, b = 1)`) → C#-Default-Parameter (`double b = 1`).
- Überladene Vergleiche `a === b` bei Strings → `==` (C# vergleicht Inhalte).

## Gemeinsame Hilfen
`Sim/Core/U.cs` (Klasse `U`) enthält die Hilfen oben. Fehlt eine Hilfe, sie **in derselben Datei, die sie braucht,** als `private static` Funktion anlegen (nicht in U.cs schreiben – andere arbeiten parallel).

## Ausgabe
- Datei vollständig übersetzen, **kein TODO, kein „…“, keine ausgelassenen Fälle**. Kommentare der Quelle sinngemäß übernehmen (deutsch).
- Am Ende der Datei keinen Testcode.
- Unsicherheiten (z. B. unklare Typen aus einer anderen Datei) mit `// PORT:`-Kommentar markieren und im Bericht nennen.
