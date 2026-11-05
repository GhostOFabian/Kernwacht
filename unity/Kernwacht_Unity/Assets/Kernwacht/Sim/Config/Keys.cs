// Übersetzung von config/keys.ts.
// Frei belegbare Tasten. Jede Aktion hat eine Standardtaste; Spieler können sie in den Einstellungen
// ändern. Die Spielsitzung übersetzt gedrückte Tasten auf die Standardtaste der gebundenen Aktion,
// dadurch bleibt die gesamte bestehende Tastenlogik unverändert gültig.

using System;
using System.Collections.Generic;

namespace Kernwacht.Sim
{
    // KeyContext = 'all' | 'lane' | 'hero' (string)

    public class KeyAction
    {
        public string id;
        public string name;
        public string group;
        /// <summary>Standardtaste (KeyboardEvent.key in Kleinbuchstaben, ' ' = Leertaste).</summary>
        public string def;
        /// <summary>Wo die Aktion gilt: immer, nur mit Lane-Helden, nur im Helden-Endlosmodus. KeyContext</summary>
        public string ctx;
    }

    /// <summary>TS: Rückgabetyp von <c>rebind</c>: <c>{ map; swapped }</c>.</summary>
    public class RebindResult
    {
        public Dictionary<string, string> map;
        public string swapped;   // Aktions-ID oder null
    }

    public static partial class Keys
    {
        private static KeyAction A(string id, string name, string group, string def, string ctx = "all")
        {
            return new KeyAction { id = id, name = name, group = group, def = def, ctx = ctx };
        }

        public static readonly List<KeyAction> KEY_ACTIONS = buildKeyActions();

        private static List<KeyAction> buildKeyActions()
        {
            List<KeyAction> list = new List<KeyAction>
            {
                A("startWave", "Welle starten / Mini-Pause", "Allgemein", " "),
                A("pause", "Pause (Menü)", "Allgemein", "p"),
                A("speed", "Spieltempo", "Allgemein", "f"),
                A("rotL", "Kamera links drehen", "Allgemein", "q"),
                A("rotR", "Kamera rechts drehen", "Allgemein", "e"),
                A("orbitL", "Kamera frei drehen −15°", "Kamera", ","),
                A("orbitR", "Kamera frei drehen +15°", "Kamera", "."),
                A("tiltUp", "Kamera steiler", "Kamera", "pageup"),
                A("tiltDown", "Kamera flacher", "Kamera", "pagedown"),
                A("resetView", "Blickwinkel zurücksetzen", "Kamera", "home"),
                A("ranges", "Alle Reichweiten", "Allgemein", "r"),
                A("grid", "Raster", "Allgemein", "g"),
                A("help", "Hilfe", "Allgemein", "h"),
                A("towerList", "Turmübersicht", "Allgemein", "o"),
                A("upgrade", "Turm aufwerten", "Turm", "u"),
                A("priority", "Zielwahl wechseln", "Turm", "t"),
                A("towerActive", "Turmfähigkeit auslösen", "Turm", "c"),
                A("sell", "Turm verkaufen", "Turm", "delete"),
                A("hero1", "Held 1 einsetzen/auswählen", "Lane-Helden", "z", "lane"),
                A("hero2", "Held 2 einsetzen/auswählen", "Lane-Helden", "x", "lane"),
                A("heroA", "Fähigkeit 1", "Lane-Helden", "a", "lane"),
                A("heroS", "Fähigkeit 2", "Lane-Helden", "s", "lane"),
                A("heroD", "Ultimate", "Lane-Helden", "d", "lane"),
                A("heroMode", "Verhalten wechseln", "Lane-Helden", "v", "lane"),
                A("skill1", "Fähigkeit 1", "Helden-Endlos", "a", "hero"),
                A("skill2", "Fähigkeit 2", "Helden-Endlos", "s", "hero"),
                A("skill3", "Fähigkeit 3", "Helden-Endlos", "d", "hero"),
                A("skill4", "Ultimate", "Helden-Endlos", "w", "hero"),
                A("sheet", "Charakterbogen", "Helden-Endlos", "tab", "hero"),
                A("shop", "Laden & Schmiede", "Helden-Endlos", "x", "hero"),
                A("follow", "Kamera folgt dem Helden", "Helden-Endlos", "y", "hero"),
                A("topdown", "Draufsicht", "Helden-Endlos", "v", "hero"),
                A("hold", "Held festsetzen", "Helden-Endlos", "z", "hero"),
                A("banner", "Befehlsbanner setzen (Feld unter dem Mauszeiger)", "Helden-Endlos", ".", "hero"),
                A("nextWave", "Nächste Welle rufen", "Helden-Endlos", "enter", "hero"),
            };
            // Die Lane-Barrikade (3.8) teilt sich die Taste mit der Mazing-Barrikade – je nach Kartenvariante gibt es nur eine davon
            List<KeyAction> towers = new List<KeyAction>();
            foreach (string id in Towers.BUILD_ORDER)
            {
                if (id == "lanebarricade") continue;
                towers.Add(A($"tower:{id}", id == "barricade" ? "Barrikade wählen (Mazing) bzw. Lane-Barrikade" : $"{Towers.TOWERS[id].name} wählen", "Türme bauen", Towers.TOWERS[id].hotkey));
            }
            // An derselben Stelle wie der TS-Spread einfügen (nach 'sell').
            int at = list.FindIndex((x) => x.id == "sell") + 1;
            list.InsertRange(at, towers);
            return list;
        }

        /// <summary>Tasten, die nicht belegt werden dürfen (Escape bricht ab / öffnet die Pause).</summary>
        public static readonly HashSet<string> RESERVED_KEYS = new HashSet<string> { "escape" };

        private static Dictionary<string, string> overrides = new Dictionary<string, string>();

        public static void setKeyBindings(Dictionary<string, string> o)
        {
            overrides = new Dictionary<string, string>(o ?? new Dictionary<string, string>());
        }

        public static string boundKey(string actionId)
        {
            KeyAction a = KEY_ACTIONS.Find((x) => x.id == actionId);
            string v;
            if (overrides.TryGetValue(actionId, out v) && v != null) return v;
            return a?.def ?? "";
        }

        private static bool overlaps(string a, string b) { return a == "all" || b == "all" || a == b; }

        /// <summary>
        /// Übersetzt eine gedrückte Taste auf die Standardtaste der Aktion, an die sie gebunden ist.
        /// null = Taste ist keiner Aktion (mehr) zugeordnet; '' = Taste wird unverändert weitergereicht.
        /// </summary>
        public static string translateKey(string k, string ctx)
        {
            List<KeyAction> relevant = KEY_ACTIONS.FindAll((a) => a.ctx == "all" || a.ctx == ctx);
            KeyAction hit = relevant.Find((a) => boundKey(a.id) == k);
            if (hit != null) return hit.def;
            // Eine Standardtaste, deren Aktion umgelegt wurde, löst nichts mehr aus
            if (relevant.Exists((a) => a.def == k && overrides.ContainsKey(a.id))) return null;
            return "";
        }

        /// <summary>
        /// Belegt eine Aktion neu. Ist die Taste in einem überlappenden Kontext schon vergeben, tauschen
        /// beide Aktionen ihre Tasten. Rückgabe: neue Überschreibungen (nur Abweichungen vom Standard).
        /// </summary>
        public static RebindResult rebind(string actionId, string key, Dictionary<string, string> current)
        {
            Dictionary<string, string> map = new Dictionary<string, string>(current);
            KeyAction a = KEY_ACTIONS.Find((x) => x.id == actionId);
            if (a == null || RESERVED_KEYS.Contains(key)) return new RebindResult { map = map, swapped = null };
            string old = U.Get(map, actionId) ?? a.def;
            Func<KeyAction, string> get = (x) => U.Get(map, x.id) ?? x.def;
            KeyAction clash = KEY_ACTIONS.Find((x) => x.id != actionId && overlaps(x.ctx, a.ctx) && get(x) == key);
            Action<KeyAction, string> set = (x, k) =>
            {
                if (k == x.def) map = deleteKey(map, x.id);
                else
                {
                    // JS: Zuweisung an einen vorhandenen Schlüssel behält dessen Position, neuer Schlüssel kommt ans Ende.
                    map[x.id] = k;
                }
            };
            set(a, key);
            if (clash != null) set(clash, old);
            return new RebindResult { map = map, swapped = clash != null ? clash.id : null };
        }

        /// <summary>JS <c>delete map[k]</c> unter Erhalt der Einfügereihenfolge der übrigen Schlüssel (neues Dictionary).</summary>
        private static Dictionary<string, string> deleteKey(Dictionary<string, string> map, string k)
        {
            if (!map.ContainsKey(k)) return map;
            Dictionary<string, string> d = new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> kv in map) if (kv.Key != k) d[kv.Key] = kv.Value;
            return d;
        }

        public static string keyName(string k)
        {
            if (k == " ") return "Leertaste";
            if (k == "enter") return "Enter";
            if (k == "tab") return "Tab";
            if (k == "delete") return "Entf";
            if (k == "backspace") return "Rück";
            if (k == "pageup") return "Bild↑";
            if (k == "pagedown") return "Bild↓";
            if (k == "home") return "Pos1";
            if (k == ",") return ",";
            if (k == "-") return "−";
            if (k.StartsWith("arrow", StringComparison.Ordinal))
            {
                switch (k)
                {
                    case "arrowup": return "↑";
                    case "arrowdown": return "↓";
                    case "arrowleft": return "←";
                    case "arrowright": return "→";
                    default: return k;
                }
            }
            // PORT: JS toUpperCase ist gebietsschemaunabhängig → ToUpperInvariant (Sonderfälle wie '\u00df' → 'SS' weichen ab).
            return k.Length == 1 ? k.ToUpperInvariant() : (k.Length > 0 ? k.Substring(0, 1).ToUpperInvariant() : "") + (k.Length > 1 ? k.Substring(1) : "");
        }

        /// <summary>
        /// TS: <c>sanitizeKeys(raw: unknown)</c>. Akzeptiert Dictionary&lt;string, string&gt; sowie JSON-Objekte
        /// (IDictionary&lt;string, object&gt; aus <c>U.ParseJson</c>); alles andere ergibt ein leeres Ergebnis.
        /// </summary>
        public static Dictionary<string, string> sanitizeKeys(object raw)
        {
            Dictionary<string, string> out_ = new Dictionary<string, string>();
            List<KeyValuePair<string, object>> entries = new List<KeyValuePair<string, object>>();
            if (raw is IDictionary<string, string> sd) foreach (KeyValuePair<string, string> kv in sd) entries.Add(new KeyValuePair<string, object>(kv.Key, kv.Value));
            else if (raw is IDictionary<string, object> od) foreach (KeyValuePair<string, object> kv in od) entries.Add(kv);
            else return out_;
            foreach (KeyValuePair<string, object> e in entries)
            {
                string id = e.Key;
                string v = e.Value as string;
                if (v == null || v.Length == 0 || v.Length > 12 || RESERVED_KEYS.Contains(v)) continue;
                if (!KEY_ACTIONS.Exists((a) => a.id == id)) continue;
                out_[id] = v.ToLowerInvariant();
            }
            return out_;
        }
    }
}
