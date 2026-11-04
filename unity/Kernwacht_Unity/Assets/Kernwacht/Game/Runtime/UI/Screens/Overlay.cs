// Übersetzung von ui/screens/overlay.ts – Hilfe oder Einstellungen als Overlay über der pausierten Partie.

using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Kernwacht.Game.UI
{
    public static class Overlay
    {
        static GameObject current;
        static Action closeCurrent;

        /// <summary>Bild, in dem das Overlay per Esc geschlossen wurde (InputController: Esc in diesem Bild ignorieren).</summary>
        public static int closedByEscFrame = -1;

        /// <summary>
        /// true, solange ein Overlay offen ist.
        /// PORT: Das Webspiel fängt Esc im Capture-Modus ab (stopPropagation) – der InputController sollte Esc ignorieren,
        /// solange isOpen() gilt oder Time.frameCount == closedByEscFrame.
        /// </summary>
        public static bool isOpen() => current != null;

        /// <summary>Schließt ein offenes Overlay (ruft dessen onClose).</summary>
        public static void close() => closeCurrent?.Invoke();

        /// <summary>Zeigt Hilfe oder Einstellungen als Overlay (kind: "help" | "settings") unter app.overlayLayer.</summary>
        public static void openOverlay(GameApp app, string kind, Action onClose)
        {
            if (current != null)
            {
                // TS: document.querySelector('.overlay-screen')?.remove() – ohne onClose des alten Overlays
                UnityEngine.Object.Destroy(current);
                current = null;
                closeCurrent = null;
            }
            Transform layer = app.overlayLayer != null ? (Transform)app.overlayLayer : (app.screenLayer != null ? (Transform)app.screenLayer : null);
            var go = Ui.Node("OverlayScreen_" + kind, layer);
            var root = Ui.Rt(go);
            Ui.Fill(root);
            var bg = go.AddComponent<Image>();
            bg.color = new Color(10 / 255f, 8 / 255f, 18 / 255f, 0.9f);
            GameObject el = go;
            bool closed = false;
            void doClose()
            {
                if (closed) return;
                closed = true;
                if (current == el) { current = null; closeCurrent = null; }
                UnityEngine.Object.Destroy(el);
                onClose?.Invoke();
            }
            current = el;
            closeCurrent = doClose;

            var body = Ui.Scroll(root, out var sr, 0, new RectOffset(12, 12, 12, 12));
            Ui.Fill(sr.GetComponent<RectTransform>());
            var bodyCol = body.GetComponent<VerticalLayoutGroup>();
            bodyCol.childAlignment = TextAnchor.UpperCenter;
            bodyCol.childForceExpandWidth = false;
            var inner = Ui.Panel(body, "ScreenInner", Common.InnerBg, true, Theme.Round);
            var innerCol = inner.gameObject.AddComponent<VerticalLayoutGroup>();
            innerCol.padding = new RectOffset(26, 26, 16, 26);
            innerCol.spacing = 8;
            innerCol.childControlWidth = true; innerCol.childControlHeight = true;
            innerCol.childForceExpandWidth = true; innerCol.childForceExpandHeight = false;
            var le = Ui.Size(inner, Common.INNER_W);
            le.minWidth = 600;
            le.flexibleWidth = 0;
            var head = Ui.Row(inner.transform, 12, TextAnchor.MiddleLeft, false);
            Common.iconOnlyButton(head.transform, "close", doClose);
            var t = Ui.Label(head.transform, kind == "help" ? "Hilfe & Kodex" : "Einstellungen", Common.H1, Theme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            Ui.Size(t, -1, -1, 1);
            if (kind == "help") HelpScreen.helpContent(inner.transform, "controls");
            else SettingsScreen.settingsContent(inner.transform, app, true);

            var esc = go.AddComponent<OverlayEsc>();
            esc.onEsc = () =>
            {
                closedByEscFrame = Time.frameCount;
                doClose();
            };
            go.AddComponent<TipView>();
            go.transform.SetAsLastSibling();
        }
    }

    /// <summary>Esc schließt das Overlay (TS: keydown-Listener im Capture-Modus).</summary>
    public class OverlayEsc : MonoBehaviour
    {
        public Action onEsc;
        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;
            if (SelectPopup.IsOpen) return;
            onEsc?.Invoke();
        }
    }
}
