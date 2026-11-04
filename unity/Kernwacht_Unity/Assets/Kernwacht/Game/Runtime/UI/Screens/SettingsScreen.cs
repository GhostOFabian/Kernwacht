// Übersetzung von ui/screens/settings.ts – Einstellungen (auch als Overlay im Spiel nutzbar).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Kernwacht.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace Kernwacht.Game.UI
{
    public static class SettingsScreen
    {
        /// <summary>
        /// true, solange die Tastenbelegung auf eine Taste wartet (und noch im Bild des Tastendrucks).
        /// PORT: Der InputController sollte währenddessen keine Spieltasten auswerten (TS: stopImmediatePropagation im Capture).
        /// </summary>
        public static bool isCapturing() => KeyCapture.Active;

        /// <summary>Einstellungsinhalte in <paramref name="parent"/> (auch als Overlay im Spiel nutzbar).</summary>
        public static RectTransform settingsContent(Transform parent, GameApp app, bool inGame)
        {
            var s = app.save.settings;
            void commit()
            {
                app.applySettings();
                app.persist();
            }
            var root = Ui.Column(parent, 6);
            root.gameObject.name = "Settings";
            var R = root.transform;

            void slider(string label, Func<double> get, Action<double> set)
            {
                var f = Common.field(R, label);
                var row = Ui.Row(f, 10, TextAnchor.MiddleLeft, false);
                var holder = Ui.Node("SliderHolder", row.transform);
                Ui.Size(Ui.Rt(holder), 420, 28);
                var output = Common.label(row.transform, $"{U.Fmt(U.Round(get() * 100))} %", Common.FS, Theme.Text, FontStyle.Normal, TextAnchor.MiddleLeft);
                Ui.Size(output, 70);
                Slider sl = null;
                bool ready = false;
                sl = Ui.Slider(holder.transform, (float)get(), (v) =>
                {
                    if (!ready) return;
                    set(v / 100.0);
                    output.text = $"{U.Fmt(U.Round(get() * 100))} %";
                    commit();
                });
                Ui.Fill(sl.GetComponent<RectTransform>());
                sl.maxValue = 100;
                sl.wholeNumbers = true;
                sl.SetValueWithoutNotify((float)U.Round(get() * 100));
                ready = true;
            }
            void check(string label, Func<bool> get, Action<bool> set, string hint = null)
            {
                Ui.Check(R, TextUtil.sym(label), get(), (v) => { set(v); commit(); }, hint);
            }
            Button resetBtn = null;
            bool armed = false;

            Common.h2(R, "Audio");
            slider("Gesamtlautstärke", () => s.masterVolume, (v) => s.masterVolume = v);
            slider("Musik", () => s.musicVolume, (v) => s.musicVolume = v);
            slider("Effekte", () => s.sfxVolume, (v) => s.sfxVolume = v);
            check("Stumm", () => s.muted, (v) => s.muted = v);
            Common.h2(R, "Darstellung & Bedienung");
            check("Bewegung reduzieren", () => s.reducedMotion, (v) => s.reducedMotion = v, "kein Kamerawackeln, ruhigere Effekte");
            check("Bauraster immer anzeigen", () => s.showGrid, (v) => s.showGrid = v);
            check("Alle Turmreichweiten anzeigen", () => s.showRanges, (v) => s.showRanges = v);
            check("Kino-Effekte", () => s.cinematicFx != false, (v) => s.cinematicFx = v, "Tiefenunschärfe am Bildrand, Filmkorn, Sonnendunst (ab Qualität Mittel)");
            Common.seg(Common.field(R, "UI-Größe"), new List<(string, string)> { ("0.85", "Klein"), ("1", "Normal"), ("1.15", "Groß"), ("1.3", "Sehr groß") }, U.Fmt(s.uiScale),
                (v) => { s.uiScale = double.Parse(v, CultureInfo.InvariantCulture); commit(); }, "UI-Größe");
            Common.seg(Common.field(R, "Grafikqualität"), new List<(string, string)> { ("auto", "Automatisch"), ("low", "Niedrig"), ("medium", "Mittel"), ("high", "Hoch") }, s.quality,
                (v) => { s.quality = v; commit(); app.resize(); }, "Grafikqualität");
            Common.para(R, "Automatisch passt die Auflösung an, wenn die Bildrate dauerhaft sinkt. Niedrig schaltet Schatten, Kantenglättung, Glühen und Bodenbewuchs ab.", "muted small");
            Common.h2(R, "Tastenbelegung");
            keyBinder(R, app);
            Common.h2(R, "Turm-Skin");
            skinPicker(R, app);
            if (!inGame)
            {
                Common.h2(R, "Daten");
                var row = Ui.Row(R, 10, TextAnchor.MiddleLeft, false);
                Common.btn(row.transform, "Einführung erneut zeigen", () =>
                {
                    s.tutorialSeen = false;
                    commit();
                    app.toast("Die Einführung wird in der nächsten Partie erneut gezeigt.");
                });
                resetBtn = Common.btn(row.transform, "Fortschritt zurücksetzen", null, Ui.BtnKind.Danger, null, false, 330);
                resetBtn.onClick.AddListener(() =>
                {
                    if (armed)
                    {
                        app.save.progress = SaveSystem.defaultProgress();
                        app.save.resume = null;
                        app.persist();
                        app.toast("Fortschritt wurde zurückgesetzt.", "info");
                        armed = false;
                        Ui.SetText(resetBtn, "Fortschritt zurücksetzen");
                    }
                    else
                    {
                        armed = true;
                        Ui.SetText(resetBtn, "Wirklich alles löschen? Erneut tippen");
                    }
                });
            }
            return root.GetComponent<RectTransform>();
        }

        static RectTransform keyBinder(Transform parent, GameApp app)
        {
            var root = Ui.Column(parent, 6);
            root.gameObject.name = "Keybinds";
            var rootRt = root.GetComponent<RectTransform>();
            var cap = root.gameObject.AddComponent<KeyCapture>();
            string capturing = null;
            var open = new Dictionary<string, bool>();
            Action render = null;
            Action<string> start = null;
            render = () =>
            {
                var s = app.save.settings;
                Common.clear(rootRt);
                Common.para(rootRt, "Aktion anklicken und danach die gewünschte Taste drücken (Esc bricht ab). Ist die Taste schon vergeben, tauschen beide Aktionen ihre Tasten. Esc bleibt fest für Abbrechen/Pause, Umschalt+1–5 für Tränke.", "muted small");
                var groups = new List<string>();
                foreach (var a in Keys.KEY_ACTIONS) if (!groups.Contains(a.group)) groups.Add(a.group);
                foreach (var g in groups)
                {
                    string grp = g;
                    bool isOpen = open.TryGetValue(grp, out var o) ? o : grp == "Allgemein";
                    Common.details(rootRt, Common.summaryText(grp), (body) =>
                    {
                        var acts = Keys.KEY_ACTIONS.Where((a) => a.group == grp).ToList();
                        Common.gridRows(body, acts.Count, 3, (row, i) =>
                        {
                            var a = acts[i];
                            string k = s.keys.TryGetValue(a.id, out var kk) && kk != null ? kk : a.def;
                            bool changed = s.keys.ContainsKey(a.id);
                            var cell = Ui.Row(row, 8, TextAnchor.MiddleLeft, false);
                            var name = Common.label(cell.transform, a.name, Common.SMALL, Theme.Text, FontStyle.Normal, TextAnchor.MiddleLeft);
                            Ui.Size(name, -1, -1, 1);
                            var b = Common.btn(cell.transform, capturing == a.id ? "Taste drücken …" : Keys.keyName(k), () => start(a.id),
                                capturing == a.id ? Ui.BtnKind.On : Ui.BtnKind.Normal, null, true, 150);
                            if (changed && capturing != a.id) b.GetComponent<Outline>().effectColor = Theme.Accent;
                            Ui.Tip(b, () => changed ? $"Standard: {Keys.keyName(a.def)}" : "Klicken zum Ändern");
                            return cell.GetComponent<RectTransform>();
                        }, 4);
                    }, isOpen, (v) => open[grp] = v);
                }
                var r = Ui.Row(rootRt, 8, TextAnchor.MiddleLeft, false);
                Common.btn(r.transform, "Standard wiederherstellen", () =>
                {
                    s.keys = new Dictionary<string, string>();
                    app.applySettings();
                    app.persist();
                    app.toast("Tastenbelegung auf Standard zurückgesetzt.");
                    render();
                });
            };
            start = (id) =>
            {
                cap.Stop(false);
                capturing = id;
                cap.Begin((k) =>
                {
                    var s = app.save.settings;
                    if (!Keys.RESERVED_KEYS.Contains(k))
                    {
                        var r = Keys.rebind(id, k, s.keys);
                        s.keys = r.map;
                        app.applySettings();
                        app.persist();
                        if (r.swapped != null)
                        {
                            var other = Keys.KEY_ACTIONS.FirstOrDefault((a) => a.id == r.swapped);
                            string otherKey = s.keys.TryGetValue(r.swapped, out var ok) && ok != null ? ok : (other != null ? other.def : "");
                            app.toast($"Getauscht: „{other?.name}“ liegt jetzt auf {Keys.keyName(otherKey)}.");
                        }
                    }
                }, () =>
                {
                    capturing = null;
                    render();
                });
                render();
            };
            render();
            return rootRt;
        }

        static RectTransform skinPicker(Transform parent, GameApp app)
        {
            var root = Ui.Column(parent, 6);
            root.gameObject.name = "Skins";
            var rootRt = root.GetComponent<RectTransform>();
            Action render = null;
            render = () =>
            {
                var s = app.save.settings;
                Common.clear(rootRt);
                var skins = Achievements.SKINS;
                Common.gridRows(rootRt, skins.Count, 6, (row, i) =>
                {
                    var k = skins[i];
                    bool ok = Achievements.skinUnlocked(k, app.save.progress.achievements);
                    var req = Achievements.ACHIEVEMENTS.FirstOrDefault((a) => a.id == k.requires);
                    bool on = s.skin == k.id;
                    var t = Common.tile(row, () =>
                    {
                        if (!ok) return;
                        s.skin = k.id;
                        app.applySettings();
                        app.persist();
                        render();
                    }, on ? Theme.Gold : (Color?)null);
                    Common.setDisabled(t.btn, !ok);
                    Ui.Tip(t.btn, () => ok ? k.description : $"Gesperrt – Erfolg „{(req != null ? req.name : k.requires)}“: {(req != null ? req.description : "")}");
                    // .sw: diagonal geteiltes Farbfeld (Stein | Tönung)
                    var sw = Ui.Node("Swatch", t.col);
                    Ui.Size(Ui.Rt(sw), -1, 34);
                    var left = Ui.Panel(sw.transform, "Stone", Theme.Hex(k.stone), false, Theme.RoundSmall);
                    var lr = left.rectTransform; lr.anchorMin = Vector2.zero; lr.anchorMax = new Vector2(0.5f, 1); lr.offsetMin = lr.offsetMax = Vector2.zero;
                    var right = Ui.Panel(sw.transform, "Tint", k.tintAmount > 0 ? Theme.Hex(k.tint) : Theme.Hex(0xb77bff), false, Theme.RoundSmall);
                    var rr = right.rectTransform; rr.anchorMin = new Vector2(0.5f, 0); rr.anchorMax = Vector2.one; rr.offsetMin = rr.offsetMax = Vector2.zero;
                    left.raycastTarget = right.raycastTarget = false;
                    if (ok && on)
                    {
                        // PORT: TS zeigt den Turm „ember“ Stufe 3 im gewählten Skin; Icons.Model rendert das Katalogmodell ohne Skin-Tönung.
                        var holder = Ui.Row(t.col, 0, TextAnchor.MiddleCenter, false);
                        Common.modelIcon(holder.transform, "ember", 48);
                    }
                    Common.para(t.col, $"<b>{k.name}</b>", "", TextAnchor.UpperCenter);
                    Common.para(t.col, ok ? k.description : $"Gesperrt · {(req != null ? req.name : "")}", "tiny muted", TextAnchor.UpperCenter);
                    return t.rt;
                });
            };
            render();
            return rootRt;
        }

        /// <summary>Bildschirm „Einstellungen“ (unter app.screenLayer).</summary>
        public static RectTransform renderSettings(GameApp app, string back)
        {
            return Common.shell(app, "Einstellungen", back ?? "menu", (p) => settingsContent(p, app, false));
        }

        /// <summary>Alias (Auftragsbeschreibung).</summary>
        public static RectTransform settingsScreen(GameApp app, string back = "menu") => renderSettings(app, back);
    }

    /// <summary>
    /// Wartet auf den nächsten Tastendruck (neues Input System) und liefert ihn wie KeyboardEvent.key.toLowerCase().
    /// Umschalt/Strg/Alt/Meta allein werden ignoriert (wie im Webspiel).
    /// </summary>
    public class KeyCapture : MonoBehaviour
    {
        static int activeCount;
        static int releaseFrame = -1;
        /// <summary>Wartet gerade eine Tastenbelegung (oder wurde sie in diesem Bild beendet)?</summary>
        public static bool Active => activeCount > 0 || Time.frameCount <= releaseFrame;

        Action<string> onKey;
        Action onStop;
        bool running;
        int startFrame;

        public void Begin(Action<string> key, Action stop)
        {
            onKey = key;
            onStop = stop;
            if (!running) activeCount++;
            running = true;
            startFrame = Time.frameCount;
        }

        /// <summary>Beendet das Warten; notify=true ruft den Stop-Rückruf (TS: stop()).</summary>
        public void Stop(bool notify = true)
        {
            if (!running) return;
            running = false;
            activeCount = Mathf.Max(0, activeCount - 1);
            releaseFrame = Time.frameCount + 1;
            var s = onStop;
            onKey = null;
            onStop = null;
            if (notify) s?.Invoke();
        }

        void Update()
        {
            if (!running || Time.frameCount == startFrame) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            foreach (var kc in kb.allKeys)
            {
                if (kc == null || !kc.wasPressedThisFrame) continue;
                string k = KeyString(kc);
                if (k == null) continue;   // reine Umschalttasten
                var cb = onKey;
                cb?.Invoke(k);
                Stop(true);
                return;
            }
        }

        void OnDisable() => Stop(false);
        void OnDestroy() => Stop(false);

        /// <summary>
        /// Taste → KeyboardEvent.key.toLowerCase(). Druckbare Tasten über displayName (berücksichtigt das Tastaturlayout,
        /// z. B. „ß“ auf deutschen Tastaturen), sonst feste Namen. null = Modifikatortaste.
        /// PORT: Muss mit der Umsetzung im InputController (KeyEvt.key) übereinstimmen.
        /// </summary>
        public static string KeyString(KeyControl kc)
        {
            switch (kc.keyCode)
            {
                case Key.LeftShift: case Key.RightShift: case Key.LeftCtrl: case Key.RightCtrl:
                case Key.LeftAlt: case Key.RightAlt: case Key.LeftMeta: case Key.RightMeta: case Key.ContextMenu:
                    return null;
                case Key.Space: return " ";
                case Key.Enter: case Key.NumpadEnter: return "enter";
                case Key.Tab: return "tab";
                case Key.Escape: return "escape";
                case Key.Backspace: return "backspace";
                case Key.Delete: return "delete";
                case Key.Insert: return "insert";
                case Key.UpArrow: return "arrowup";
                case Key.DownArrow: return "arrowdown";
                case Key.LeftArrow: return "arrowleft";
                case Key.RightArrow: return "arrowright";
                case Key.PageUp: return "pageup";
                case Key.PageDown: return "pagedown";
                case Key.Home: return "home";
                case Key.End: return "end";
                case Key.CapsLock: return "capslock";
                case Key.NumLock: return "numlock";
                case Key.ScrollLock: return "scrolllock";
                case Key.PrintScreen: return "printscreen";
                case Key.Pause: return "pause";
                case Key.NumpadPlus: return "+";
                case Key.NumpadMinus: return "-";
                case Key.NumpadMultiply: return "*";
                case Key.NumpadDivide: return "/";
                case Key.NumpadPeriod: return ",";
                case Key.NumpadEquals: return "=";
                case Key.Numpad0: return "0";
                case Key.Numpad1: return "1";
                case Key.Numpad2: return "2";
                case Key.Numpad3: return "3";
                case Key.Numpad4: return "4";
                case Key.Numpad5: return "5";
                case Key.Numpad6: return "6";
                case Key.Numpad7: return "7";
                case Key.Numpad8: return "8";
                case Key.Numpad9: return "9";
            }
            if (kc.keyCode >= Key.F1 && kc.keyCode <= Key.F12) return "f" + (kc.keyCode - Key.F1 + 1);
            if (kc.keyCode >= Key.Digit1 && kc.keyCode <= Key.Digit9) return ((char)('1' + (kc.keyCode - Key.Digit1))).ToString();
            if (kc.keyCode == Key.Digit0) return "0";
            string dn = kc.displayName;
            if (!string.IsNullOrEmpty(dn) && dn.Length == 1) return dn.ToLowerInvariant();
            switch (kc.keyCode)
            {
                case Key.Comma: return ",";
                case Key.Period: return ".";
                case Key.Minus: return "-";
                case Key.Equals: return "=";
                case Key.Slash: return "/";
                case Key.Backslash: return "\\";
                case Key.Semicolon: return ";";
                case Key.Quote: return "'";
                case Key.LeftBracket: return "[";
                case Key.RightBracket: return "]";
                case Key.Backquote: return "`";
            }
            if (kc.keyCode >= Key.A && kc.keyCode <= Key.Z) return ((char)('a' + (kc.keyCode - Key.A))).ToString();
            return string.IsNullOrEmpty(dn) ? null : dn.ToLowerInvariant();
        }
    }
}
