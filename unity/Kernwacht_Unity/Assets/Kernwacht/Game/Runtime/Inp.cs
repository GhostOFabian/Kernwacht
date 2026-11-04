using UnityEngine;

namespace Kernwacht.Game
{
    /// <summary>
    /// Eingabe-Hülle: funktioniert mit dem neuen Input System (Standard in Unity 6) und mit dem alten Input Manager.
    /// </summary>
    public static class Inp
    {
#if ENABLE_INPUT_SYSTEM
        static UnityEngine.InputSystem.Pointer P => UnityEngine.InputSystem.Pointer.current;
        static UnityEngine.InputSystem.Mouse M => UnityEngine.InputSystem.Mouse.current;
        static UnityEngine.InputSystem.Keyboard K => UnityEngine.InputSystem.Keyboard.current;

        public static Vector2 PointerPos => P != null ? P.position.ReadValue() : Vector2.zero;
        public static bool PrimaryDown => P != null && P.press.wasPressedThisFrame;
        public static bool PrimaryUp => P != null && P.press.wasReleasedThisFrame;
        public static bool PrimaryHeld => P != null && P.press.isPressed;
        public static bool SecondaryDown => M != null && M.rightButton.wasPressedThisFrame;
        public static bool PanHeld => M != null && (M.rightButton.isPressed || M.middleButton.isPressed);
        public static float Scroll => M != null ? M.scroll.ReadValue().y / 120f : 0f;
        public static int TouchCount => UnityEngine.InputSystem.Touchscreen.current != null ? CountTouches() : 0;

        static int CountTouches()
        {
            int n = 0;
            foreach (var t in UnityEngine.InputSystem.Touchscreen.current.touches) if (t.press.isPressed) n++;
            return n;
        }

        public static Vector2 TouchPos(int i)
        {
            var ts = UnityEngine.InputSystem.Touchscreen.current;
            if (ts == null) return Vector2.zero;
            int n = 0;
            foreach (var t in ts.touches)
            {
                if (!t.press.isPressed) continue;
                if (n == i) return t.position.ReadValue();
                n++;
            }
            return Vector2.zero;
        }

        public static bool Key(string k)
        {
            if (K == null) return false;
            switch (k)
            {
                case "esc": return K.escapeKey.wasPressedThisFrame;
                case "space": return K.spaceKey.wasPressedThisFrame;
                case "u": return K.uKey.wasPressedThisFrame;
                case "del": return K.deleteKey.wasPressedThisFrame || K.backspaceKey.wasPressedThisFrame;
                case "tab": return K.tabKey.wasPressedThisFrame;
                case "p": return K.pKey.wasPressedThisFrame;
                case "f": return K.fKey.wasPressedThisFrame;
            }
            return false;
        }

        public static bool Held(string k)
        {
            if (K == null) return false;
            switch (k)
            {
                case "w": return K.wKey.isPressed || K.upArrowKey.isPressed;
                case "s": return K.sKey.isPressed || K.downArrowKey.isPressed;
                case "a": return K.aKey.isPressed || K.leftArrowKey.isPressed;
                case "d": return K.dKey.isPressed || K.rightArrowKey.isPressed;
                case "q": return K.qKey.isPressed;
                case "e": return K.eKey.isPressed;
            }
            return false;
        }

        public static int DigitDown()
        {
            if (K == null) return -1;
            if (K.digit1Key.wasPressedThisFrame) return 1;
            if (K.digit2Key.wasPressedThisFrame) return 2;
            if (K.digit3Key.wasPressedThisFrame) return 3;
            if (K.digit4Key.wasPressedThisFrame) return 4;
            if (K.digit5Key.wasPressedThisFrame) return 5;
            if (K.digit6Key.wasPressedThisFrame) return 6;
            if (K.digit7Key.wasPressedThisFrame) return 7;
            if (K.digit8Key.wasPressedThisFrame) return 8;
            if (K.digit9Key.wasPressedThisFrame) return 9;
            if (K.digit0Key.wasPressedThisFrame) return 10;
            return -1;
        }
#else
        public static Vector2 PointerPos => Input.mousePosition;
        public static bool PrimaryDown => Input.GetMouseButtonDown(0);
        public static bool PrimaryUp => Input.GetMouseButtonUp(0);
        public static bool PrimaryHeld => Input.GetMouseButton(0);
        public static bool SecondaryDown => Input.GetMouseButtonDown(1);
        public static bool PanHeld => Input.GetMouseButton(1) || Input.GetMouseButton(2);
        public static float Scroll => Input.mouseScrollDelta.y;
        public static int TouchCount => Input.touchCount;
        public static Vector2 TouchPos(int i) => i < Input.touchCount ? Input.GetTouch(i).position : Vector2.zero;

        public static bool Key(string k)
        {
            switch (k)
            {
                case "esc": return Input.GetKeyDown(KeyCode.Escape);
                case "space": return Input.GetKeyDown(KeyCode.Space);
                case "u": return Input.GetKeyDown(KeyCode.U);
                case "del": return Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace);
                case "tab": return Input.GetKeyDown(KeyCode.Tab);
                case "p": return Input.GetKeyDown(KeyCode.P);
                case "f": return Input.GetKeyDown(KeyCode.F);
            }
            return false;
        }

        public static bool Held(string k)
        {
            switch (k)
            {
                case "w": return Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
                case "s": return Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
                case "a": return Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
                case "d": return Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
                case "q": return Input.GetKey(KeyCode.Q);
                case "e": return Input.GetKey(KeyCode.E);
            }
            return false;
        }

        public static int DigitDown()
        {
            for (int i = 1; i <= 9; i++) if (Input.GetKeyDown(KeyCode.Alpha0 + i)) return i;
            if (Input.GetKeyDown(KeyCode.Alpha0)) return 10;
            return -1;
        }
#endif
    }
}
