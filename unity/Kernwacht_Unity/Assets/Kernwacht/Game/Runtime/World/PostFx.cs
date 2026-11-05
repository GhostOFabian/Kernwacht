using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kernwacht.Game
{
    /// <summary>
    /// Nachbearbeitung der Welt (URP-Volume, zur Laufzeit erzeugt): Glühen, neutrales Tonemapping, leichte Farbkorrektur
    /// (Kontrast, Sättigung, Tönung je Kartenstimmung) und eine dezente Vignette. Auf „Niedrig“ ganz aus.
    /// Ersetzt den bisherigen Vermerk „Nachbearbeitung nicht nachgebildet“ in WorldView.
    /// </summary>
    public static class PostFx
    {
        static Volume vol;
        static Bloom bloom;
        static Tonemapping tone;
        static ColorAdjustments grade;
        static Vignette vignette;
        static bool enabled = true;
        static Color moodFilter = Color.white;

        static void Ensure()
        {
            if (vol != null) return;
            var go = new GameObject("Kernwacht-Nachbearbeitung");
            Object.DontDestroyOnLoad(go);
            vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 10;
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            vol.sharedProfile = p;

            bloom = p.Add<Bloom>(true);
            bloom.threshold.Override(0.92f);
            bloom.intensity.Override(0.55f);
            bloom.scatter.Override(0.62f);
            bloom.highQualityFiltering.Override(false);

            tone = p.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.Neutral);

            grade = p.Add<ColorAdjustments>(true);
            grade.contrast.Override(10f);
            grade.saturation.Override(12f);
            grade.postExposure.Override(0.08f);
            grade.colorFilter.Override(Color.white);

            vignette = p.Add<Vignette>(true);
            vignette.intensity.Override(0.2f);
            vignette.smoothness.Override(0.45f);
            vignette.color.Override(new Color(0.05f, 0.03f, 0.08f));
        }

        /// <summary>Kamera für Nachbearbeitung freischalten (einmal beim Start).</summary>
        public static void Attach(Camera cam)
        {
            Ensure();
            if (cam == null) return;
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null) data.renderPostProcessing = enabled;
        }

        /// <summary>Qualitätsstufe: auf „Niedrig“ (kein Glühen) bleibt die Nachbearbeitung aus.</summary>
        public static void SetQuality(Camera cam, bool on)
        {
            Ensure();
            enabled = on;
            vol.enabled = on;
            if (cam != null)
            {
                var data = cam.GetUniversalAdditionalCameraData();
                if (data != null) data.renderPostProcessing = on;
            }
        }

        /// <summary>Tönung der Kartenstimmung (Morgenrot, Mondnacht …) leicht in die Farbkorrektur übernehmen.</summary>
        public static void SetMood(int tint, float amount)
        {
            Ensure();
            var c = new Color(((tint >> 16) & 255) / 255f, ((tint >> 8) & 255) / 255f, (tint & 255) / 255f);
            moodFilter = Color.Lerp(Color.white, c, Mathf.Clamp01(amount) * 0.35f);
            grade.colorFilter.Override(moodFilter);
        }
    }
}
