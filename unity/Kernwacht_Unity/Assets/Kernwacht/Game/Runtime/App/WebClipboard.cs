// 4.1: Zwischenablage für Text und Bilder. Im WebGL-Build über KernwachtClipboard.jslib (navigator.clipboard),
// im Editor/Desktop über GUIUtility.systemCopyBuffer bzw. eine PNG-Datei.
using System.Runtime.InteropServices;
using UnityEngine;

namespace Kernwacht.Game
{
    public static class WebClipboard
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int KW_CopyText(string text);
        [DllImport("__Internal")] static extern int KW_CopyPng(byte[] data, int len, string name);
#endif

        public static bool CopyText(string text)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try { return KW_CopyText(text ?? "") == 1; } catch { return false; }
#else
            GUIUtility.systemCopyBuffer = text ?? "";
            return true;
#endif
        }

        /// <summary>PNG in die Zwischenablage legen. Rückgabe: 1 = Zwischenablage, 2 = heruntergeladen, 0 = Fehler.</summary>
        public static int CopyPng(byte[] png, string fileName)
        {
            if (png == null || png.Length == 0) return 0;
#if UNITY_WEBGL && !UNITY_EDITOR
            try { return KW_CopyPng(png, png.Length, fileName); } catch { return 0; }
#else
            try
            {
                string path = System.IO.Path.Combine(Application.persistentDataPath, fileName);
                System.IO.File.WriteAllBytes(path, png);
                Debug.Log("[Kernwacht] Statistikbild gespeichert: " + path);
                return 2;
            }
            catch { return 0; }
#endif
        }
    }
}
