using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Kernwacht.Game
{
    // Übersetzung von input/InputController.ts.
    // Zeiger-, Touch- und Tastatursteuerung für die Spielfläche.
    // Maus: Klick = Aktion, Ziehen (links/rechts/Mitte) = Schwenken, Mausrad = Zoom, Hover = Bauvorschau.
    // Touch: Tippen = Aktion, Ein-Finger-Ziehen = Schwenken, Zwei Finger = Zoomen/Schwenken.
    //
    // PORT: Der Browser liefert Ereignisse; das neue Input System wird hier einmal pro Bild abgefragt
    // (update(), von GameSession.frame aufgerufen) und daraus dieselben Ereignisse erzeugt.
    // Koordinaten wie im Browser: Bildschirmpixel, Ursprung oben links (Screen.height − y).
    // Zeiger über uGUI-Elementen (EventSystem) lösen keine Weltaktionen aus – wie im Webspiel, wo HUD-Elemente
    // über der Zeichenfläche liegen und die Ereignisse abfangen.

    /// <summary>TS-Interface InputHandlers (Rückrufe der Sitzung).</summary>
    public class InputHandlers
    {
        /// <summary>time = Zeitstempel des auslösenden Ereignisses in ms (für Doppelklicks, unabhängig von Bildrate/Last).</summary>
        public Action<float, float, string, double> tap;
        public Action<float, float> hover;
        public Action hoverEnd;
        public Action<float, float> pan;
        public Action<float, float, float> zoom;
        /// <summary>Freies Drehen der Kamera (Gier, Neigung in Bogenmaß).</summary>
        public Action<float, float> orbit;
        public Action<KeyEvt> key;
        /// <summary>PORT: Ersatz für das 'contextmenu'-Ereignis der Zeichenfläche (Rechtsklick ohne Ziehen).</summary>
        public Action<float, float> contextmenu;
    }

    public class InputController
    {
        class Ptr { public int id; public float x, y, sx, sy; public double t; public string type; public bool orbit; public int button; }

        // PORT: Map<number, Ptr> behält die Einfügereihenfolge – hier eine Liste.
        readonly List<Ptr> ptrs = new List<Ptr>();
        bool dragging;
        float pinchDist;
        Vector2 pinchMid;
        float pinchAng;
        readonly InputHandlers h;
        public bool enabled = true;
        bool disposed;

        const int MOUSE_ID = 1;
        bool mouseOverUiLast;
        Vector2 lastMousePos = new Vector2(float.NaN, float.NaN);
        double lastTouchTime = -1e9;
        readonly Dictionary<int, bool> touchActive = new Dictionary<int, bool>();

        // Tastenwiederholung wie im Browser (gedrückt halten → wiederholte keydown-Ereignisse)
        Key repeatKey = Key.None;
        double repeatAt;
        const double REPEAT_DELAY = 0.5, REPEAT_RATE = 0.033;

        public InputController(InputHandlers handlers)
        {
            h = handlers;
        }

        static double Now() => Time.realtimeSinceStartupAsDouble * 1000.0;

        /// <summary>Verhältnis Gerätepixel zu CSS-Pixeln (für Zieh-Schwellen).</summary>
        static float CssScale()
        {
            float dpi = Screen.dpi;
            if (dpi <= 0) return 1;
            return Mathf.Max(1, dpi / 96f);
        }

        // ------------------------------------------------------------ Abfrage

        /// <summary>Einmal pro Bild aufrufen: erzeugt Zeiger-, Rad- und Tastenereignisse.</summary>
        public void update()
        {
            if (disposed) return;
            pollTouch();
            pollMouse();
            pollKeys();
        }

        // ------------------------------------------------------------ UI-Test

        static readonly List<RaycastResult> rayHits = new List<RaycastResult>();
        static PointerEventData rayData;

        /// <summary>Liegt der Punkt (Pixel, Ursprung oben links) über einem uGUI-Element, das Strahlen abfängt?</summary>
        public static bool overUi(float x, float yTop)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            if (rayData == null || rayData.eventSystem != es) rayData = new PointerEventData(es);
            rayData.position = new Vector2(x, Screen.height - yTop);
            rayHits.Clear();
            es.RaycastAll(rayData, rayHits);
            return rayHits.Count > 0;
        }

        static bool insideScreen(Vector2 p) => p.x >= 0 && p.y >= 0 && p.x <= Screen.width && p.y <= Screen.height;

        // ------------------------------------------------------------ Maus

        void pollMouse()
        {
            var m = Mouse.current;
            if (m == null) return;
            // Nach einer Touch-Eingabe erzeugt der Browser Kompatibilitäts-Mausereignisse – ignorieren
            if (touchActive.Count > 0 || Now() - lastTouchTime < 600) return;
            Vector2 raw = m.position.ReadValue();
            var p = new Vector2(raw.x, Screen.height - raw.y);
            bool inside = insideScreen(raw);
            var ptr = find(MOUSE_ID);

            // Tasten gedrückt
            int pressed = m.leftButton.wasPressedThisFrame ? 0 : m.middleButton.wasPressedThisFrame ? 1 : m.rightButton.wasPressedThisFrame ? 2 : -1;
            if (pressed >= 0 && ptr == null && inside && !overUi(p.x, p.y))
            {
                var kb = Keyboard.current;
                bool ctrl = kb != null && kb.ctrlKey.isPressed;
                bool alt = kb != null && kb.altKey.isPressed;
                down(MOUSE_ID, p.x, p.y, "mouse", pressed, ctrl, alt);
                ptr = find(MOUSE_ID);
            }

            // Bewegung
            bool movedPos = float.IsNaN(lastMousePos.x) || (p - lastMousePos).sqrMagnitude > 0.01f;
            lastMousePos = p;
            if (ptr != null)
            {
                if (movedPos) move(MOUSE_ID, p.x, p.y, "mouse");
            }
            else
            {
                bool ui = !inside || overUi(p.x, p.y);
                if (ui)
                {
                    // pointerleave: Zeiger verlässt die Zeichenfläche (auf ein HUD-Element oder aus dem Fenster)
                    if (!mouseOverUiLast) h.hoverEnd?.Invoke();
                }
                else if (movedPos || mouseOverUiLast)
                {
                    if (enabled) h.hover?.Invoke(p.x, p.y);
                }
                mouseOverUiLast = ui;
            }

            // Mausrad (über der Zeichenfläche)
            float sy = m.scroll.ReadValue().y;
            if (sy != 0 && inside && !overUi(p.x, p.y))
            {
                h.zoom?.Invoke(Mathf.Exp(browserDeltaY(sy) * 0.0012f), p.x, p.y);
            }

            // Loslassen: erst wenn keine Taste mehr gedrückt ist (ein Zeiger pro Maus wie im Browser)
            ptr = find(MOUSE_ID);
            if (ptr != null)
            {
                bool any = m.leftButton.isPressed || m.middleButton.isPressed || m.rightButton.isPressed;
                if (!any)
                {
                    int released = m.leftButton.wasReleasedThisFrame ? 0 : m.middleButton.wasReleasedThisFrame ? 1 : m.rightButton.wasReleasedThisFrame ? 2 : ptr.button;
                    bool wasDragging = dragging;
                    up(MOUSE_ID, "mouse", released);
                    // PORT: Rechtsklick ohne Ziehen = 'contextmenu' (Browser: je nach System bei mousedown/mouseup)
                    if (released == 2 && !wasDragging && enabled) h.contextmenu?.Invoke(p.x, p.y);
                }
            }
        }

        /// <summary>
        /// Unity-Mausrad → Browser-WheelEvent.deltaY (positiv = nach unten = herauszoomen).
        /// PORT: Je nach Plattform liefert Unity ±120 je Raste (Windows) oder normierte ±1-Werte; eine Raste ≈ deltaY 100.
        /// </summary>
        static float browserDeltaY(float unityY)
        {
            float a = Mathf.Abs(unityY);
            float notch = a >= 10f ? unityY / 120f : unityY;
            return -notch * 100f;
        }

        // ------------------------------------------------------------ Touch

        void pollTouch()
        {
            var ts = Touchscreen.current;
            if (ts == null) return;
            foreach (var tc in ts.touches)
            {
                int tid = tc.touchId.ReadValue();
                if (tid == 0) continue;
                int id = 100 + tid;
                var phase = tc.phase.ReadValue();
                Vector2 raw = tc.position.ReadValue();
                var p = new Vector2(raw.x, Screen.height - raw.y);
                switch (phase)
                {
                    case UnityEngine.InputSystem.TouchPhase.Began:
                        if (touchActive.ContainsKey(id)) break;
                        touchActive[id] = true;
                        lastTouchTime = Now();
                        if (!overUi(p.x, p.y)) down(id, p.x, p.y, "touch", 0, false, false);
                        break;
                    case UnityEngine.InputSystem.TouchPhase.Moved:
                    case UnityEngine.InputSystem.TouchPhase.Stationary:
                        lastTouchTime = Now();
                        if (find(id) != null) move(id, p.x, p.y, "touch");
                        break;
                    case UnityEngine.InputSystem.TouchPhase.Ended:
                        if (!touchActive.ContainsKey(id)) break;
                        touchActive.Remove(id);
                        lastTouchTime = Now();
                        if (find(id) != null)
                        {
                            move(id, p.x, p.y, "touch");
                            up(id, "touch", 0);
                        }
                        break;
                    case UnityEngine.InputSystem.TouchPhase.Canceled:
                        if (!touchActive.ContainsKey(id)) break;
                        touchActive.Remove(id);
                        lastTouchTime = Now();
                        cancel(id);
                        break;
                }
            }
        }

        // ------------------------------------------------------------ Zeigerlogik (wie TS)

        Ptr find(int id)
        {
            for (int i = 0; i < ptrs.Count; i++) if (ptrs[i].id == id) return ptrs[i];
            return null;
        }

        void remove(int id)
        {
            for (int i = 0; i < ptrs.Count; i++) if (ptrs[i].id == id) { ptrs.RemoveAt(i); return; }
        }

        void down(int id, float x, float y, string type, int button, bool ctrl, bool alt)
        {
            if (!enabled) return;
            // Mittlere Maustaste oder Strg/Alt + Ziehen: Kamera frei drehen
            bool orbit = type == "mouse" && (button == 1 || ctrl || alt);
            remove(id);
            ptrs.Add(new Ptr { id = id, x = x, y = y, sx = x, sy = y, t = Now(), type = type, orbit = orbit, button = button });
            if (ptrs.Count == 2)
            {
                var a = ptrs[0];
                var b = ptrs[1];
                pinchAng = Mathf.Atan2(b.y - a.y, b.x - a.x);
                pinchDist = Hypot(a.x - b.x, a.y - b.y);
                pinchMid = new Vector2((a.x + b.x) / 2, (a.y + b.y) / 2);
                dragging = true;
            }
        }

        void move(int id, float px, float py, string type)
        {
            var ptr = find(id);
            if (ptr == null)
            {
                if (type == "mouse" && enabled) h.hover?.Invoke(px, py);
                return;
            }
            float dx = px - ptr.x;
            float dy = py - ptr.y;
            ptr.x = px;
            ptr.y = py;
            if (ptrs.Count >= 2)
            {
                var a = ptrs[0];
                var b = ptrs[1];
                float d = Hypot(a.x - b.x, a.y - b.y);
                var mid = new Vector2((a.x + b.x) / 2, (a.y + b.y) / 2);
                if (pinchDist > 0 && d > 0) h.zoom?.Invoke(pinchDist / d, mid.x, mid.y);
                h.pan?.Invoke(mid.x - pinchMid.x, mid.y - pinchMid.y);
                // Zwei Finger drehen: Kamera frei rotieren
                float ang = Mathf.Atan2(b.y - a.y, b.x - a.x);
                float da = ang - pinchAng;
                if (da > Mathf.PI) da -= Mathf.PI * 2;
                if (da < -Mathf.PI) da += Mathf.PI * 2;
                if (Mathf.Abs(da) < 0.5f) h.orbit?.Invoke(-da, 0);
                pinchAng = ang;
                pinchDist = d;
                pinchMid = mid;
                return;
            }
            float moved = Hypot(px - ptr.sx, py - ptr.sy);
            float threshold = (ptr.type == "mouse" ? 6 : 12) * CssScale();
            if (!dragging && moved > threshold) dragging = true;
            if (dragging && ptr.orbit && h.orbit != null) h.orbit(dx * 0.008f, dy * 0.006f);
            else if (dragging) h.pan?.Invoke(dx, dy);
            else if (type == "mouse") h.hover?.Invoke(px, py);
        }

        void up(int id, string type, int button)
        {
            var ptr = find(id);
            remove(id);
            if (ptr == null) return;
            if (!dragging && ptrs.Count == 0 && enabled)
            {
                if (type != "mouse" || button == 0) h.tap?.Invoke(ptr.x, ptr.y, ptr.type, Now());
            }
            if (ptrs.Count == 0) dragging = false;
        }

        void cancel(int id)
        {
            remove(id);
            if (ptrs.Count == 0) dragging = false;
        }

        static float Hypot(float a, float b) => Mathf.Sqrt(a * a + b * b);

        // ------------------------------------------------------------ Tastatur

        void pollKeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            // Texteingabe in einem Eingabefeld: keine Spieltasten (wie TS: INPUT/TEXTAREA/SELECT)
            if (TextFieldFocused())
            {
                repeatKey = Key.None;
                return;
            }
            double now = Time.realtimeSinceStartupAsDouble;
            foreach (var kc in kb.allKeys)
            {
                if (kc == null || !kc.wasPressedThisFrame) continue;
                var e = KeyMap.make(kb, kc, false);
                if (e == null) continue;
                if (!KeyMap.isModifier(kc.keyCode))
                {
                    repeatKey = kc.keyCode;
                    repeatAt = now + REPEAT_DELAY;
                }
                dispatch(e);
            }
            // Wiederholung
            if (repeatKey != Key.None)
            {
                var kc = kb[repeatKey];
                if (kc == null || !kc.isPressed) repeatKey = Key.None;
                else if (now >= repeatAt)
                {
                    repeatAt = now + REPEAT_RATE;
                    var e = KeyMap.make(kb, kc, true);
                    if (e != null) dispatch(e);
                }
            }
        }

        void dispatch(KeyEvt e)
        {
            if (!enabled) return;
            // PORT: Das Overlay (Hilfe/Einstellungen) fängt im Browser Escape in der Capture-Phase ab (stopPropagation).
            if (e.key == "escape" && GameApp.I != null && GameApp.I.overlayOpen()) return;
            try
            {
                h.key?.Invoke(e);
            }
            catch (Exception err)
            {
                Debug.LogException(err);
            }
        }

        public static bool TextFieldFocused()
        {
            var es = EventSystem.current;
            var sel = es != null ? es.currentSelectedGameObject : null;
            if (sel == null) return false;
            var inp = sel.GetComponent<UnityEngine.UI.InputField>();
            return inp != null && inp.isFocused;
        }

        public void dispose()
        {
            disposed = true;
            ptrs.Clear();
            touchActive.Clear();
            repeatKey = Key.None;
        }
    }

    /// <summary>
    /// Abbildung Unity-<see cref="Key"/> → Browser-<c>KeyboardEvent.key</c> (klein) und <c>KeyboardEvent.code</c>.
    /// Unity-Tasten sind physische Positionen (US-Belegung); das Zeichen der aktuellen Belegung liefert
    /// <c>KeyControl.displayName</c> (auf deutscher Tastatur z. B. Key.Y → "Z", Key.Minus → "ß").
    /// Mit Umschalt liefert der Browser das umgeschaltete Zeichen ("!" bei Umschalt+1) – hier per Tabelle für
    /// deutsche (QWERTZ) und US-Belegung nachgebildet.
    /// </summary>
    public static class KeyMap
    {
        public static bool isModifier(Key k) =>
            k == Key.LeftShift || k == Key.RightShift || k == Key.LeftCtrl || k == Key.RightCtrl ||
            k == Key.LeftAlt || k == Key.RightAlt || k == Key.LeftMeta || k == Key.RightMeta;

        /// <summary>Deutsche Belegung erkannt (Key.Y zeigt "Z").</summary>
        public static bool german(Keyboard kb)
        {
            try
            {
                var dn = kb[Key.Y].displayName;
                if (!string.IsNullOrEmpty(dn) && dn.ToLowerInvariant() == "z") return true;
                var lay = kb.keyboardLayout;
                return !string.IsNullOrEmpty(lay) && (lay.IndexOf("German", StringComparison.OrdinalIgnoreCase) >= 0 || lay.IndexOf("Deutsch", StringComparison.OrdinalIgnoreCase) >= 0);
            }
            catch { return false; }
        }

        static readonly Dictionary<Key, string> NAMED = new Dictionary<Key, string>
        {
            { Key.Space, " " }, { Key.Enter, "enter" }, { Key.NumpadEnter, "enter" }, { Key.Escape, "escape" }, { Key.Tab, "tab" },
            { Key.Backspace, "backspace" }, { Key.Delete, "delete" }, { Key.Insert, "insert" },
            { Key.UpArrow, "arrowup" }, { Key.DownArrow, "arrowdown" }, { Key.LeftArrow, "arrowleft" }, { Key.RightArrow, "arrowright" },
            { Key.PageUp, "pageup" }, { Key.PageDown, "pagedown" }, { Key.Home, "home" }, { Key.End, "end" },
            { Key.LeftShift, "shift" }, { Key.RightShift, "shift" }, { Key.LeftCtrl, "control" }, { Key.RightCtrl, "control" },
            { Key.LeftAlt, "alt" }, { Key.RightAlt, "altgraph" }, { Key.LeftMeta, "meta" }, { Key.RightMeta, "meta" },
            { Key.CapsLock, "capslock" }, { Key.NumLock, "numlock" }, { Key.ScrollLock, "scrolllock" }, { Key.PrintScreen, "printscreen" },
            { Key.Pause, "pause" }, { Key.ContextMenu, "contextmenu" },
            { Key.F1, "f1" }, { Key.F2, "f2" }, { Key.F3, "f3" }, { Key.F4, "f4" }, { Key.F5, "f5" }, { Key.F6, "f6" },
            { Key.F7, "f7" }, { Key.F8, "f8" }, { Key.F9, "f9" }, { Key.F10, "f10" }, { Key.F11, "f11" }, { Key.F12, "f12" },
        };

        static readonly Dictionary<Key, string> CODES = new Dictionary<Key, string>
        {
            { Key.Space, "Space" }, { Key.Enter, "Enter" }, { Key.NumpadEnter, "NumpadEnter" }, { Key.Escape, "Escape" }, { Key.Tab, "Tab" },
            { Key.Backspace, "Backspace" }, { Key.Delete, "Delete" }, { Key.Insert, "Insert" },
            { Key.UpArrow, "ArrowUp" }, { Key.DownArrow, "ArrowDown" }, { Key.LeftArrow, "ArrowLeft" }, { Key.RightArrow, "ArrowRight" },
            { Key.PageUp, "PageUp" }, { Key.PageDown, "PageDown" }, { Key.Home, "Home" }, { Key.End, "End" },
            { Key.LeftShift, "ShiftLeft" }, { Key.RightShift, "ShiftRight" }, { Key.LeftCtrl, "ControlLeft" }, { Key.RightCtrl, "ControlRight" },
            { Key.LeftAlt, "AltLeft" }, { Key.RightAlt, "AltRight" }, { Key.LeftMeta, "MetaLeft" }, { Key.RightMeta, "MetaRight" },
            { Key.CapsLock, "CapsLock" }, { Key.NumLock, "NumLock" }, { Key.ScrollLock, "ScrollLock" }, { Key.PrintScreen, "PrintScreen" },
            { Key.Pause, "Pause" }, { Key.ContextMenu, "ContextMenu" },
            { Key.Minus, "Minus" }, { Key.Equals, "Equal" }, { Key.Comma, "Comma" }, { Key.Period, "Period" }, { Key.Slash, "Slash" },
            { Key.Semicolon, "Semicolon" }, { Key.Quote, "Quote" }, { Key.LeftBracket, "BracketLeft" }, { Key.RightBracket, "BracketRight" },
            { Key.Backslash, "Backslash" }, { Key.Backquote, "Backquote" }, { Key.OEM1, "IntlBackslash" },
            { Key.NumpadPlus, "NumpadAdd" }, { Key.NumpadMinus, "NumpadSubtract" }, { Key.NumpadMultiply, "NumpadMultiply" },
            { Key.NumpadDivide, "NumpadDivide" }, { Key.NumpadPeriod, "NumpadDecimal" }, { Key.NumpadEquals, "NumpadEqual" },
        };

        /// <summary>Zeichen ohne Umschalt auf US-Belegung (Rückfall, wenn displayName nichts Brauchbares liefert).</summary>
        static readonly Dictionary<Key, string> US_PLAIN = new Dictionary<Key, string>
        {
            { Key.Minus, "-" }, { Key.Equals, "=" }, { Key.Comma, "," }, { Key.Period, "." }, { Key.Slash, "/" }, { Key.Semicolon, ";" },
            { Key.Quote, "'" }, { Key.LeftBracket, "[" }, { Key.RightBracket, "]" }, { Key.Backslash, "\\" }, { Key.Backquote, "`" },
        };
        static readonly Dictionary<Key, string> DE_PLAIN = new Dictionary<Key, string>
        {
            { Key.Minus, "ß" }, { Key.Equals, "´" }, { Key.Comma, "," }, { Key.Period, "." }, { Key.Slash, "-" }, { Key.Semicolon, "ö" },
            { Key.Quote, "ä" }, { Key.LeftBracket, "ü" }, { Key.RightBracket, "+" }, { Key.Backslash, "#" }, { Key.Backquote, "^" }, { Key.OEM1, "<" },
        };
        static readonly string[] US_SHIFT_DIGITS = { ")", "!", "@", "#", "$", "%", "^", "&", "*", "(" };
        static readonly string[] DE_SHIFT_DIGITS = { "=", "!", "\"", "§", "$", "%", "&", "/", "(", ")" };
        static readonly Dictionary<string, string> US_SHIFT = new Dictionary<string, string>
        {
            { "-", "_" }, { "=", "+" }, { ",", "<" }, { ".", ">" }, { "/", "?" }, { ";", ":" }, { "'", "\"" }, { "[", "{" }, { "]", "}" }, { "\\", "|" }, { "`", "~" },
        };
        static readonly Dictionary<string, string> DE_SHIFT = new Dictionary<string, string>
        {
            { "ß", "?" }, { "´", "`" }, { ",", ";" }, { ".", ":" }, { "-", "_" }, { "+", "*" }, { "#", "'" }, { "^", "°" }, { "<", ">" },
        };

        public static KeyEvt make(Keyboard kb, KeyControl kc, bool repeat)
        {
            Key k = kc.keyCode;
            if (k == Key.None) return null;
            bool shift = kb.shiftKey.isPressed;
            bool ctrl = kb.ctrlKey.isPressed;
            bool alt = kb.altKey.isPressed;
            bool meta = (kb.leftMetaKey != null && kb.leftMetaKey.isPressed) || (kb.rightMetaKey != null && kb.rightMetaKey.isPressed);
            string key = null;
            string code = null;
            bool de = german(kb);

            if (k >= Key.A && k <= Key.Z)
            {
                code = "Key" + (char)('A' + (k - Key.A));
                key = single(kc.displayName) ?? ((char)('a' + (k - Key.A))).ToString();
            }
            else if (k >= Key.Digit1 && k <= Key.Digit0)
            {
                int d = k == Key.Digit0 ? 0 : (k - Key.Digit1) + 1;
                code = "Digit" + d;
                key = shift ? (de ? DE_SHIFT_DIGITS[d] : US_SHIFT_DIGITS[d]) : d.ToString();
            }
            else if (k >= Key.Numpad0 && k <= Key.Numpad9)
            {
                int d = k - Key.Numpad0;
                code = "Numpad" + d;
                key = d.ToString();
            }
            else if (k == Key.NumpadPlus) { code = CODES[k]; key = "+"; }
            else if (k == Key.NumpadMinus) { code = CODES[k]; key = "-"; }
            else if (k == Key.NumpadMultiply) { code = CODES[k]; key = "*"; }
            else if (k == Key.NumpadDivide) { code = CODES[k]; key = "/"; }
            else if (k == Key.NumpadPeriod) { code = CODES[k]; key = de ? "," : "."; }
            else if (k == Key.NumpadEquals) { code = CODES[k]; key = "="; }
            else if (NAMED.TryGetValue(k, out var named))
            {
                key = named;
                code = CODES.TryGetValue(k, out var c) ? c : k.ToString();
            }
            else if (US_PLAIN.ContainsKey(k) || k == Key.OEM1)
            {
                code = CODES.TryGetValue(k, out var c) ? c : k.ToString();
                string plain = single(kc.displayName);
                if (plain == null) plain = de ? (DE_PLAIN.TryGetValue(k, out var dp) ? dp : null) : (US_PLAIN.TryGetValue(k, out var up) ? up : null);
                if (plain == null) return null;
                if (shift)
                {
                    var map = de ? DE_SHIFT : US_SHIFT;
                    key = map.TryGetValue(plain, out var s) ? s : plain;
                }
                else key = plain;
            }
            else
            {
                // Sonstige Tasten (OEM, IME …): Anzeigename, sofern ein Zeichen
                key = single(kc.displayName);
                if (key == null) return null;
                code = k.ToString();
            }

            return new KeyEvt
            {
                key = key.ToLowerInvariant(),
                code = code,
                shiftKey = shift,
                ctrlKey = ctrl,
                altKey = alt,
                metaKey = meta,
                repeat = repeat,
            };
        }

        /// <summary>Genau ein druckbares Zeichen (sonst null).</summary>
        static string single(string dn)
        {
            if (string.IsNullOrEmpty(dn)) return null;
            if (dn.Length == 1 && !char.IsWhiteSpace(dn[0])) return dn.ToLowerInvariant();
            return null;
        }
    }
}
