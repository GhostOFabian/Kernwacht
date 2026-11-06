// Port von audio/AudioSystem.ts.
// Eigene, vollständig synthetische Audioarchitektur. Keine fremden Assets.
// Getrennte "Busse" für Musik und Effekte, Gesamtlautstärke, Stummschaltung, Ratenbegrenzung.
//
// Umsetzung in Unity (WebGL-tauglich, ohne OnAudioFilterRead und ohne Audio-Effekte):
// - Jeder Effekt wird einmal (bzw. in wenigen Varianten, wenn Rauschen beteiligt ist) als AudioClip
//   aus berechneten Samples erzeugt (Oszillatoren mit PolyBLEP, exponentielle Hüllkurven, Biquad-Filter
//   nach WebAudio-Spezifikation, LCG-Rauschpuffer wie im Original). Hall und Echo werden eingebacken.
// - Musik: dieselbe generative Logik wie im TS (scheduleMusic mit 0,3 s Vorlauf); jede Note ist ein
//   zwischengespeicherter Clip, der per AudioSource.PlayScheduled zur dspTime abgespielt wird.
// - Lautstärken (Master/Musik/Effekte/Stumm) wirken über AudioSource.volume, auch auf laufende Klänge.
// - Der AudioContext des Browsers wird von Unity bei der ersten Nutzereingabe fortgesetzt; vor unlock()
//   werden keine Unity-Objekte angefasst (Konstruktor ist auch aus Feldinitialisierern sicher).

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kernwacht.Game
{
    /** Entspricht dem TS-Interface AudioSettings. */
    // PORT: Name überdeckt UnityEngine.AudioSettings innerhalb von Kernwacht.Game (bewusst, TS-Name). Wer die Unity-Klasse braucht: UnityEngine.AudioSettings voll qualifizieren.
    public class AudioSettings
    {
        public double master;
        public double music;
        public double sfx;
        public bool muted;
    }

    public class AudioSystem
    {
        // ------------------------------------------------------------ öffentlicher Zustand (wie TS)
        public double intensity = 0;   // 0 Ruhe, 1 Kampf, 2 Boss
        public AudioSettings settings = new AudioSettings { master = 0.8, music = 0.5, sfx = 0.8, muted = false };
        public bool available = true;

        // ------------------------------------------------------------ intern
        internal const int SR = 44100;        // Effekte und Rauschen
        internal const int SR_MUSIC = 22050;  // Musiknoten (alle tief-/mittelfrequent) – halber Speicher
        private const int SFX_VOICES = 24;
        private const int MUSIC_VOICES = 40;
        private const long MUSIC_CACHE_BUDGET = 6_000_000; // Samples

        private bool ctx;   // "AudioContext" angelegt (unlock aufgerufen)
        private GameObject host;
        private AudioSource[] sfxSources;
        private double[] sfxVol;
        private AudioSource[] musicSources;
        private double[] musicBusy;
        private int musicRR;

        private float[] noise;
        private readonly System.Random rnd = new System.Random();
        private readonly Dictionary<string, double> last = new Dictionary<string, double>();
        private readonly Dictionary<string, SfxEntry> sfxClips = new Dictionary<string, SfxEntry>();
        private readonly Dictionary<string, CachedClip> musicClips = new Dictionary<string, CachedClip>();
        private long musicCacheSamples;

        private bool musicTimer;
        private double nextBeat = 0;
        private int beat = 0;
        private double[] scale = { 0, 2, 4, 7, 9 };
        private double root = 57;
        private double listenerCheck;

        private class SfxEntry { public readonly List<AudioClip> clips = new List<AudioClip>(); public bool noisy; }
        private class CachedClip { public AudioClip clip; public int samples; public double lastEnd; }

        private readonly Queue<double> recent = new Queue<double>();
        private readonly Dictionary<string, double> repeatHeat = new Dictionary<string, double>();
        private readonly System.Random pitchRng = new System.Random();
        private static readonly HashSet<string> IMPORTANT = new HashSet<string> { "wave", "boss", "levelup", "defeat", "victory", "upgrade", "build", "select", "error", "click", "leak" };

        private static readonly Dictionary<string, double> minGap = new Dictionary<string, double>
        {
            // Unity 1.1: größere Mindestabstände für Dauerfeuer-Klänge (weniger Geknatter in vollen Wellen)
            ["hit"] = 0.09, ["shot_ranger"] = 0.1, ["zap"] = 0.11, ["kill"] = 0.07, ["pulse_frost"] = 0.14,
            ["pulse_sonic"] = 0.16, ["explosion"] = 0.14, ["gold"] = 0.1, ["beam"] = 0.12, ["reaction"] = 0.16,
        };

        private bool ready => ctx && host != null;
        private static double dspNow => UnityEngine.AudioSettings.dspTime;

        /** Muss aus einem Nutzer-Event heraus aufgerufen werden (idempotent). */
        public void unlock()
        {
            if (!available) return;
            try
            {
                if (!ctx)
                {
                    ctx = true;
                    host = new GameObject("KernwachtAudio");
                    UnityEngine.Object.DontDestroyOnLoad(host);
                    var runner = host.AddComponent<AudioSystemRunner>();
                    runner.sys = this;
                    sfxSources = new AudioSource[SFX_VOICES];
                    sfxVol = new double[SFX_VOICES];
                    for (int i = 0; i < SFX_VOICES; i++) sfxSources[i] = makeSource();
                    musicSources = new AudioSource[MUSIC_VOICES];
                    musicBusy = new double[MUSIC_VOICES];
                    for (int i = 0; i < MUSIC_VOICES; i++) musicSources[i] = makeSource();
                    // Rauschpuffer: 1 s, gleicher LCG wie im Original (JS-Doppelgenauigkeit nachgebildet)
                    noise = new float[SR];
                    double seed = 12345;
                    for (int i = 0; i < noise.Length; i++)
                    {
                        seed = lcg(seed);
                        noise[i] = (float)((seed / 0x7fffffff) * 2 - 1);
                    }
                    ensureListener();
                    applyVolumes();
                }
            }
            catch (Exception err)
            {
                Debug.LogWarning("[Audio] nicht verfügbar " + err);
                available = false;
            }
        }

        /** seed = (seed * 1103515245 + 12345) & 0x7fffffff mit JS-Semantik (Produkt als double). */
        private static double lcg(double seed)
        {
            double p = seed * 1103515245.0 + 12345.0;
            double m = p % 4294967296.0;
            if (m < 0) m += 4294967296.0;
            return (double)((uint)m & 0x7fffffffu);
        }

        private AudioSource makeSource()
        {
            var s = host.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = false;
            s.spatialBlend = 0f;
            s.dopplerLevel = 0f;
            s.priority = 64;
            return s;
        }

        /** Ohne AudioListener in der Szene wäre nichts zu hören: notfalls einen eigenen anhängen. */
        private void ensureListener()
        {
            if (host == null) return;
            var all = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            var own = host.GetComponent<AudioListener>();
            int others = 0;
            foreach (var l in all) if (l != own && l.enabled) others++;
            if (others == 0 && own == null) host.AddComponent<AudioListener>();
            else if (others > 0 && own != null) UnityEngine.Object.Destroy(own);
        }

        public void setSettings(AudioSettings s)
        {
            settings = new AudioSettings { master = s.master, music = s.music, sfx = s.sfx, muted = s.muted };
            applyVolumes();
        }

        private double masterGain => settings.muted ? 0 : settings.master;
        private double sfxGain => masterGain * settings.sfx * 0.7;
        private double musicGain => masterGain * settings.music * 0.55;

        private void applyVolumes()
        {
            if (!ready) return;
            for (int i = 0; i < sfxSources.Length; i++) sfxSources[i].volume = (float)Clamp01(sfxGain * sfxVol[i]);
            float mv = (float)Clamp01(musicGain);
            for (int i = 0; i < musicSources.Length; i++) musicSources[i].volume = mv;
        }

        private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;

        // ------------------------------------------------------------ Effekte

        /** Alias für den Vertrag in PORTING_UI.md (`sfx(id)`); identisch zu play. */
        public void sfx(string id, double volume = 1) => play(id, volume);

        public void play(string id, double volume = 1)
        {
            if (!ready || settings.muted || id == null) return;
            double now = dspNow;
            double prev = last.TryGetValue(id, out var p) ? p : -1;
            double gap = minGap.TryGetValue(id, out var g) ? g : 0.02;
            if (now - prev < gap) return;
            // Unity 1.1: höchstens 7 Effekte je 120 ms; gehäufte gleiche Klänge werden leiser
            while (recent.Count > 0 && now - recent.Peek() > 0.12) recent.Dequeue();
            if (recent.Count >= 7 && !IMPORTANT.Contains(id)) return;
            recent.Enqueue(now);
            double rep = repeatHeat.TryGetValue(id, out var rh) ? Math.Max(0, rh - (now - (prev < 0 ? now : prev)) * 2.5) : 0;
            repeatHeat[id] = rep + 1;
            volume *= 1 / (1 + 0.18 * rep);
            int voice = -1;
            for (int i = 0; i < sfxSources.Length; i++) if (!sfxSources[i].isPlaying) { voice = i; break; }
            if (voice < 0) return;   // entspricht "active > 48"
            last[id] = now;
            var clip = getSfxClip(Themes.SciFi && SCI_SFX.Contains(id) ? "sf_" + id : id);
            if (clip == null) return;
            var src = sfxSources[voice];
            sfxVol[voice] = volume;
            src.clip = clip;
            src.volume = (float)Clamp01(sfxGain * volume);
            // leichte Tonhöhenstreuung, damit Wiederholungen nicht mechanisch klingen
            src.pitch = IMPORTANT.Contains(id) ? 1f : 0.94f + (float)pitchRng.NextDouble() * 0.12f;
            src.Play();
        }

        private AudioClip getSfxClip(string id)
        {
            if (!sfxClips.TryGetValue(id, out var e))
            {
                e = new SfxEntry();
                sfxClips[id] = e;
            }
            int want = e.clips.Count == 0 || e.noisy ? 3 : 1;
            if (e.clips.Count < want)
            {
                var mix = new Mix(SR, noise, rnd);
                if (!recipe(id, mix)) { return null; }
                e.noisy = mix.usedNoise;
                if (e.clips.Count > 0 && !e.noisy) return e.clips[0];
                Dsp.Reverb(mix, 0.12, 1.3);
                var clip = mix.ToClip("sfx_" + id + "_" + e.clips.Count);
                e.clips.Add(clip);
                return clip;
            }
            return e.clips[rnd.Next(e.clips.Count)];
        }

        /** 4.1: Klänge mit eigener Sci-Fi-Fassung ("sf_" + id). */
        private static readonly HashSet<string> SCI_SFX = new HashSet<string>
        {
            "ui", "build", "upgrade", "sell", "shot_ranger", "shot_ember", "shot_ballista", "shot_cannon", "shot_spore", "zap", "beam", "rift",
            "pulse_frost", "pulse_sonic", "hit", "explosion", "kill", "gold", "waveStart", "waveClear", "shot_meteor", "gravity", "teleport", "towerActive", "freeze",
        };

        /** Die Rezepte aus play() (TS), bei now = 0 in einen Puffer gerendert, Lautstärke v = 1. */
        private static bool recipe(string id, Mix m)
        {
            const double now = 0, v = 1;
            switch (id)
            {
                // ---- 4.1 Sci-Fi-Fassungen
                case "sf_ui": m.tone(1480, now, 0.05, "sine", 0.12 * v); m.tone(2960, now, 0.03, "sine", 0.04 * v); break;
                case "sf_build": m.tone(330, now, 0.12, "square", 0.06 * v, filter: 2000, slide: 660); m.tone(990, now + 0.08, 0.1, "sine", 0.1 * v); m.noiseBurst(now, 0.1, 0.12 * v, "lowpass", 900); break;
                case "sf_upgrade": { double[] fs = { 659, 880, 1175, 1568 }; for (int i = 0; i < fs.Length; i++) m.tone(fs[i], now + i * 0.05, 0.12, "square", 0.05 * v, filter: 3000); break; }
                case "sf_sell": m.tone(1568, now, 0.06, "sine", 0.12 * v); m.tone(1046, now + 0.06, 0.1, "sine", 0.12 * v, slide: 784); break;
                case "sf_shot_ranger": m.tone(1900, now, 0.07, "square", 0.045 * v, slide: 600, filter: 5200); m.tone(950, now, 0.06, "sine", 0.06 * v, slide: 400); break;
                case "sf_shot_ember": m.tone(240, now, 0.26, "sawtooth", 0.1 * v, filter: 1500, slide: 560); m.noiseBurst(now, 0.22, 0.12 * v, "bandpass", 1400, 300); break;
                case "sf_shot_ballista": m.tone(2600, now, 0.2, "sawtooth", 0.06 * v, slide: 180, filter: 6500); m.noiseBurst(now, 0.12, 0.14 * v, "highpass", 4200); m.tone(80, now, 0.2, "sine", 0.2 * v, slide: 40); break;
                case "sf_shot_cannon": m.tone(130, now, 0.28, "square", 0.12 * v, filter: 800, slide: 50); m.noiseBurst(now, 0.22, 0.2 * v, "lowpass", 1600); m.tone(1500, now, 0.1, "sine", 0.05 * v, slide: 300); break;
                case "sf_shot_spore": m.tone(520, now, 0.12, "sine", 0.1 * v, slide: 950); m.noiseBurst(now, 0.12, 0.07 * v, "bandpass", 3200); break;
                case "sf_zap": m.noiseBurst(now, 0.1, 0.16 * v, "bandpass", 4800, 1600); m.tone(2400, now, 0.08, "square", 0.05 * v, slide: 600, filter: 6000); break;
                case "sf_beam": m.tone(1200, now, 0.14, "sawtooth", 0.035 * v, filter: 3200, slide: 1500); break;
                case "sf_rift": m.tone(200, now, 0.32, "sine", 0.12 * v, slide: 2200, attack: 0.03); m.tone(400, now, 0.3, "square", 0.03 * v, slide: 3000, filter: 3000); break;
                case "sf_teleport": m.tone(2400, now, 0.25, "sine", 0.08 * v, slide: 200); m.noiseBurst(now, 0.2, 0.05 * v, "highpass", 5000); break;
                case "sf_pulse_frost": m.tone(2800, now, 0.3, "sine", 0.05 * v, slide: 1400); m.noiseBurst(now, 0.28, 0.07 * v, "highpass", 6000); break;
                case "sf_freeze": m.tone(3000, now, 0.22, "sine", 0.06 * v, slide: 1800); m.tone(1500, now, 0.2, "square", 0.02 * v, filter: 4000); break;
                case "sf_pulse_sonic": m.tone(140, now, 0.36, "sine", 0.2 * v, slide: 70); m.tone(280, now, 0.3, "square", 0.04 * v, filter: 900); break;
                case "sf_hit": m.noiseBurst(now, 0.04, 0.07 * v, "bandpass", 2800); m.tone(1800, now, 0.03, "sine", 0.03 * v); break;
                case "sf_explosion": m.noiseBurst(now, 0.38, 0.28 * v, "lowpass", 2200, 150); m.tone(60, now, 0.32, "sine", 0.25 * v, slide: 30); m.tone(3000, now, 0.2, "sine", 0.04 * v, slide: 200); break;
                case "sf_kill": m.tone(900, now, 0.08, "square", 0.04 * v, filter: 3000, slide: 300); break;
                case "sf_gold": m.tone(1568, now, 0.06, "sine", 0.1 * v); m.tone(2093, now + 0.05, 0.08, "sine", 0.08 * v); m.tone(3136, now + 0.09, 0.05, "sine", 0.03 * v); break;
                case "sf_waveStart": m.tone(587, now, 0.16, "square", 0.07 * v, filter: 1800); m.tone(880, now + 0.16, 0.24, "square", 0.07 * v, filter: 1800); m.tone(220, now, 0.4, "sawtooth", 0.06 * v, filter: 600); break;
                case "sf_waveClear": { double[] fs = { 659, 784, 988, 1318 }; for (int i = 0; i < fs.Length; i++) m.tone(fs[i], now + i * 0.07, 0.2, "square", 0.05 * v, filter: 2600); break; }
                case "sf_shot_meteor": m.tone(3000, now, 0.6, "sine", 0.05 * v, slide: 300, attack: 0.05); m.tone(100, now, 0.6, "sawtooth", 0.12 * v, filter: 900, slide: 40); m.noiseBurst(now, 0.5, 0.1 * v, "bandpass", 2200, 400); break;
                case "sf_gravity": m.tone(80, now, 0.5, "sine", 0.24 * v, slide: 40, attack: 0.04); m.tone(160, now, 0.45, "sawtooth", 0.04 * v, slide: 60, filter: 500); break;
                case "sf_towerActive": m.tone(440, now, 0.35, "sawtooth", 0.07 * v, filter: 3000, slide: 1760); m.tone(880, now + 0.1, 0.3, "square", 0.05 * v, filter: 2400); break;
                case "ui": m.tone(880, now, 0.07, "triangle", 0.15 * v); break;
                case "select": m.tone(660, now, 0.06, "triangle", 0.12 * v); m.tone(990, now + 0.04, 0.06, "triangle", 0.1 * v); break;
                case "error": m.tone(180, now, 0.18, "square", 0.12 * v, filter: 900); break;
                case "build": m.noiseBurst(now, 0.15, 0.3 * v, "lowpass", 600); m.tone(220, now, 0.15, "triangle", 0.2 * v, slide: 330); break;
                case "upgrade": { double[] fs = { 523, 659, 784 }; for (int i = 0; i < fs.Length; i++) m.tone(fs[i], now + i * 0.06, 0.14, "triangle", 0.16 * v); break; }
                case "sell": m.tone(784, now, 0.08, "sine", 0.15 * v); m.tone(523, now + 0.07, 0.12, "sine", 0.15 * v); break;
                case "shot_ranger": m.noiseBurst(now, 0.07, 0.12 * v, "highpass", 2500); break;
                case "shot_ember": m.noiseBurst(now, 0.25, 0.18 * v, "bandpass", 500, 150); break;
                case "shot_ballista": m.tone(140, now, 0.12, "sawtooth", 0.15 * v, filter: 700, slide: 70); m.noiseBurst(now, 0.1, 0.15 * v, "highpass", 1800); break;
                case "shot_cannon": m.tone(90, now, 0.25, "sine", 0.35 * v, slide: 40); m.noiseBurst(now, 0.2, 0.25 * v, "lowpass", 900); break;
                case "shot_spore": m.tone(300, now, 0.12, "sine", 0.12 * v, slide: 180); break;
                case "zap": m.noiseBurst(now, 0.12, 0.18 * v, "bandpass", 3200, 1200); m.tone(1200, now, 0.08, "square", 0.05 * v, slide: 300, filter: 4000); break;
                case "beam": m.tone(740, now, 0.12, "sine", 0.08 * v, slide: 1100); break;
                case "rift": m.tone(300, now, 0.3, "sine", 0.12 * v, slide: 900, attack: 0.03); break;
                case "pulse_frost": m.tone(1600, now, 0.3, "sine", 0.06 * v, slide: 900); m.noiseBurst(now, 0.25, 0.06 * v, "highpass", 5000); break;
                case "pulse_sonic": m.tone(220, now, 0.35, "sine", 0.18 * v, slide: 110); m.tone(440, now, 0.3, "sine", 0.08 * v); break;
                case "hit": m.noiseBurst(now, 0.05, 0.08 * v, "bandpass", 1400); break;
                case "explosion": m.noiseBurst(now, 0.35, 0.3 * v, "lowpass", 1200, 120); m.tone(70, now, 0.3, "sine", 0.25 * v, slide: 35); break;
                case "kill": m.tone(520, now, 0.08, "triangle", 0.08 * v, slide: 260); break;
                case "gold": m.tone(1318, now, 0.08, "sine", 0.1 * v); m.tone(1760, now + 0.05, 0.1, "sine", 0.08 * v); break;
                case "leak": m.tone(160, now, 0.4, "sawtooth", 0.2 * v, filter: 800, slide: 80); break;
                case "reaction": m.tone(880, now, 0.12, "triangle", 0.12 * v, slide: 1320); m.noiseBurst(now, 0.15, 0.08 * v, "bandpass", 2400); break;
                case "freeze": m.tone(2000, now, 0.2, "sine", 0.08 * v, slide: 2600); break;
                case "interrupt": m.tone(600, now, 0.15, "square", 0.1 * v, filter: 2000, slide: 300); m.tone(300, now + 0.1, 0.2, "square", 0.08 * v, filter: 1500); break;
                case "teleport": m.tone(400, now, 0.25, "sine", 0.1 * v, slide: 1600); break;
                case "ability": m.tone(330, now, 0.3, "sawtooth", 0.1 * v, filter: 1200, slide: 440); break;
                case "bossWarn":
                    for (int i = 0; i < 3; i++) m.tone(i % 2 != 0 ? 330 : 440, now + i * 0.22, 0.2, "sawtooth", 0.14 * v, filter: 1600);
                    break;
                case "waveStart": m.tone(392, now, 0.18, "triangle", 0.18 * v); m.tone(523, now + 0.15, 0.25, "triangle", 0.18 * v); break;
                case "waveClear": { double[] fs = { 523, 659, 784, 1046 }; for (int i = 0; i < fs.Length; i++) m.tone(fs[i], now + i * 0.08, 0.22, "triangle", 0.14 * v); break; }
                case "victory": { double[] fs = { 523, 659, 784, 1046, 784, 1046, 1318 }; for (int i = 0; i < fs.Length; i++) m.tone(fs[i], now + i * 0.13, 0.35, "triangle", 0.18 * v); break; }
                case "defeat": { double[] fs = { 392, 349, 311, 262 }; for (int i = 0; i < fs.Length; i++) m.tone(fs[i], now + i * 0.25, 0.5, "sawtooth", 0.12 * v, filter: 1200); break; }
                case "shot_meteor": m.tone(160, now, 0.5, "sawtooth", 0.12 * v, filter: 700, slide: 600, attack: 0.05); m.noiseBurst(now, 0.4, 0.1 * v, "bandpass", 800, 2400); break;
                case "gravity": m.tone(110, now, 0.45, "sine", 0.22 * v, slide: 55, attack: 0.04); m.tone(220, now, 0.4, "triangle", 0.05 * v, slide: 70); break;
                case "bossIntro":
                    m.tone(55, now, 2.2, "sawtooth", 0.22 * v, filter: 300, attack: 0.3);
                    m.tone(82.4, now + 0.1, 2.0, "sawtooth", 0.14 * v, filter: 380, attack: 0.4);
                    m.noiseBurst(now, 1.6, 0.18 * v, "lowpass", 400, 80);
                    foreach (double d in new double[] { 0, 0.45, 0.9 }) m.tone(65, now + d, 0.35, "sine", 0.4 * v, slide: 35);
                    break;
                case "heroDeploy":
                    {
                        m.tone(523, now, 0.3, "triangle", 0.16 * v, slide: 1046, attack: 0.02); m.noiseBurst(now, 0.4, 0.08 * v, "highpass", 3000);
                        double[] fs = { 784, 1046 }; for (int i = 0; i < fs.Length; i++) m.tone(fs[i], now + 0.18 + i * 0.08, 0.3, "sine", 0.1 * v);
                        break;
                    }
                case "heroDown": { double[] fs = { 440, 370, 294 }; for (int i = 0; i < fs.Length; i++) m.tone(fs[i], now + i * 0.16, 0.4, "triangle", 0.14 * v, filter: 1600); break; }
                case "heroAbility": m.tone(392, now, 0.25, "sawtooth", 0.1 * v, filter: 2400, slide: 784); m.noiseBurst(now, 0.25, 0.1 * v, "bandpass", 1800, 600); break;
                case "levelUp": { double[] fs = { 523, 659, 784, 1046, 1318 }; for (int i = 0; i < fs.Length; i++) m.tone(fs[i], now + i * 0.07, 0.3, "triangle", 0.13 * v); break; }
                case "fusion":
                    {
                        double[] fs = { 261, 392, 523, 659, 784, 1046 };
                        for (int i = 0; i < fs.Length; i++) m.tone(fs[i], now + i * 0.09, 0.9, "triangle", 0.12 * v, attack: 0.03);
                        m.noiseBurst(now, 1.2, 0.08 * v, "highpass", 4000, 9000);
                        break;
                    }
                case "towerActive": m.tone(330, now, 0.35, "square", 0.08 * v, filter: 2400, slide: 990); m.tone(660, now + 0.1, 0.3, "triangle", 0.1 * v); break;
                case "shatter":
                    {
                        m.noiseBurst(now, 0.5, 0.25 * v, "highpass", 2500, 6000);
                        double[] fs = { 1760, 1480, 2093 }; for (int i = 0; i < fs.Length; i++) m.tone(fs[i], now + i * 0.05, 0.3, "sine", 0.07 * v);
                        break;
                    }
                case "devour": m.tone(90, now, 0.8, "sawtooth", 0.2 * v, filter: 500, slide: 45); m.noiseBurst(now, 0.6, 0.15 * v, "lowpass", 600, 100); break;
                case "heal": m.tone(660, now, 0.4, "sine", 0.07 * v, slide: 990, attack: 0.05); break;
                case "achievement": { double[] fs = { 784, 988, 1175, 1568 }; for (int i = 0; i < fs.Length; i++) m.tone(fs[i], now + i * 0.1, 0.5, "triangle", 0.13 * v); break; }
                default: return false;
            }
            return true;
        }

        // ------------------------------------------------------------ Musik (generativ)

        public void setMusicTheme(double root, IList<double> scale)
        {
            this.root = root;
            if (scale != null && scale.Count > 0)
            {
                var s = new double[scale.Count];
                for (int i = 0; i < s.Length; i++) s[i] = scale[i];
                this.scale = s;
            }
        }

        public void startMusic()
        {
            if (!ready || musicTimer) return;
            nextBeat = dspNow + 0.1;
            musicTimer = true;
        }

        public void stopMusic()
        {
            musicTimer = false;
        }

        private double midi(double n) => 440 * Math.Pow(2, (n - 69) / 12);

        private double deg(double d, double oct = 0)
        {
            var s = scale;
            double o = Math.Floor(d / s.Length);
            int i = (int)(((d % s.Length) + s.Length) % s.Length);
            return root + s[i] + 12 * (o + oct);
        }

        /** Vom Runner jedes Frame aufgerufen (ersetzt setInterval(100 ms) des Originals). */
        internal void tick()
        {
            if (!ready) return;
            double t = Time.unscaledTimeAsDouble;
            if (t >= listenerCheck) { listenerCheck = t + 1.5; ensureListener(); }
            if (musicTimer) scheduleMusic();
        }

        private void scheduleMusic()
        {
            double now = dspNow;
            if (settings.muted || settings.music <= 0.001)
            {
                nextBeat = now + 0.1;
                return;
            }
            // PORT: Schutz gegen lange Pausen (Tab im Hintergrund stoppt Update, dspTime läuft weiter): nicht alle verpassten Schläge nachholen.
            if (nextBeat < now - 0.5) nextBeat = now + 0.05;
            double bpm = intensity >= 2 ? 100 : intensity >= 1 ? 86 : 78;
            double spb = 60 / bpm;
            while (nextBeat < now + 0.3)
            {
                double t = nextBeat;
                int b = beat;
                int bar = (int)Math.Floor(b / 8.0) % 4;
                // Unity 1.1: zwei abwechselnde Akkordfolgen (8 Takte) statt einer Viertaktschleife
                int phrase = (int)Math.Floor(b / 32.0) % 4;
                int prog = (phrase % 2 == 0 ? new[] { 0, 3, 4, 2 } : new[] { 5, 3, 0, 4 })[bar];
                if (b % 8 == 0)
                {
                    // Flächenklang
                    bool sfm = Themes.SciFi;
                    foreach (int k in new[] { 0, 2, 4 }) musicTone(midi(deg(prog + k, -1)), t, spb * 8, sfm ? "sawtooth" : "triangle", sfm ? 0.03 : 0.05, attack: 0.6, filter: sfm ? 800 : 1200);
                    musicTone(midi(deg(prog, -2)), t, spb * 4, "sine", 0.12, attack: 0.05);
                }
                if (b % 8 == 4) musicTone(midi(deg(prog, -2)), t, spb * 4, "sine", 0.1, attack: 0.05);
                // Kampfmelodie: weicher (Dreieck statt Rechteck), leiser und nur in jeder zweiten Phrase; Muster wechselt
                if (intensity >= 1 && b % 2 == 0 && phrase != 1)
                {
                    int[] pat = phrase == 3 ? new[] { 4, 2, 0, 1, 2, 4, 5, 4 } : new[] { 0, 2, 4, 2, 1, 3, 4, 5 };
                    if (!(phrase == 2 && b % 8 >= 6)) musicTone(midi(deg(prog + pat[b % 8], 0)), t, spb * 0.9, Themes.SciFi ? "square" : "triangle", Themes.SciFi ? 0.012 : 0.018, filter: Themes.SciFi ? 1600 : 1100);
                }
                else if (intensity >= 1 && phrase == 1 && b % 4 == 2)
                {
                    int step = new[] { 4, 2, 5, 3 }[bar];
                    musicTone(midi(deg(prog + step, 1)), t, spb * 2, "sine", 0.026, attack: 0.05);
                }
                else if (intensity == 0 && b % 4 == 2)
                {
                    int step = new[] { 4, 2, 5, 3 }[bar];
                    musicTone(midi(deg(prog + step, 1)), t, spb * 2, "sine", 0.03, attack: 0.05);
                }
                if (intensity >= 1)
                {
                    if (b % 4 == 0) musicTone(55, t, 0.18, "sine", 0.14, slide: 40);
                    if (b % 4 == 3) musicNoise(t, 0.04, 0.014, "highpass", 6500);
                }
                if (intensity >= 2 && b % 8 == 0) musicTone(midi(deg(prog, -3)), t, spb * 8, "sawtooth", 0.04, filter: 400, attack: 1);
                if (intensity >= 2)
                {
                    // Kriegstrommeln im Bosskampf
                    if (b % 4 == 0 || b % 8 == 3) musicTone(48, t, 0.3, "sine", 0.2, slide: 30);
                    if (b % 4 == 2) musicNoise(t, 0.12, 0.045, "bandpass", 900);
                }
                // 4.1 Sci-Fi: pulsierender Synth-Bass in Achteln
                if (Themes.SciFi && intensity >= 1) musicTone(midi(deg(prog, -2)), t, spb * 0.4, "sawtooth", 0.035, filter: 500);
                // Arpeggio-Glitzer mit Echo (leise, in allen Phasen) – nur über die Echoleitung hörbar
                if (b % 4 == 1 && (bar + (int)Math.Floor(b / 32.0)) % 2 == 0)
                {
                    int step = new[] { 0, 2, 4, 6 }[(b >> 2) % 4];
                    musicTone(midi(deg(prog + step, 1)), t, spb * 0.8, "sine", 0.025, attack: 0.01, viaDelay: true);
                }
                beat++;
                nextBeat += spb / 2;
            }
        }

        private void musicTone(double freq, double start, double dur, string type, double gain,
            double? attack = null, double? filter = null, double? slide = null, bool viaDelay = false)
        {
            string key = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "t|{0}|{1:0.00}|{2:0.0000}|{3}|{4}|{5}|{6}|{7}", type, freq, dur, gain, attack, filter, slide, viaDelay);
            var cc = getMusicClip(key, () =>
            {
                var m = new Mix(SR_MUSIC, noise, rnd);
                m.tone(freq, 0, dur, type, gain, attack: attack, slide: slide, filter: filter);
                if (viaDelay) Dsp.Echo(m, 0.32, 0.28, 2200);
                Dsp.Reverb(m, 0.32, 2.0);
                return m;
            });
            schedule(cc, start);
        }

        private void musicNoise(double start, double dur, double gain, string filterType, double freq)
        {
            // Zufälliger Startversatz im Rauschpuffer wie im Original → einige Varianten
            int variant = rnd.Next(4);
            string key = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "n|{0}|{1}|{2}|{3}|{4}", dur, gain, filterType, freq, variant);
            var cc = getMusicClip(key, () =>
            {
                var m = new Mix(SR, noise, rnd);
                m.noiseBurst(0, dur, gain, filterType, freq);
                Dsp.Reverb(m, 0.32, 1.2);
                return m;
            });
            schedule(cc, start);
        }

        private CachedClip getMusicClip(string key, Func<Mix> render)
        {
            if (musicClips.TryGetValue(key, out var cc)) return cc;
            var m = render();
            cc = new CachedClip { clip = m.ToClip("mus_" + musicClips.Count), samples = m.len, lastEnd = 0 };
            musicClips[key] = cc;
            musicCacheSamples += cc.samples;
            evictMusic(key);
            return cc;
        }

        private void evictMusic(string keep)
        {
            if (musicCacheSamples <= MUSIC_CACHE_BUDGET) return;
            double now = dspNow;
            var order = new List<KeyValuePair<string, CachedClip>>(musicClips);
            order.Sort((a, b) => a.Value.lastEnd.CompareTo(b.Value.lastEnd));
            foreach (var kv in order)
            {
                if (musicCacheSamples <= MUSIC_CACHE_BUDGET) break;
                if (kv.Key == keep || kv.Value.lastEnd >= now) continue;
                // Sicherstellen, dass keine Quelle den Clip noch hält
                for (int i = 0; i < musicSources.Length; i++) if (musicSources[i].clip == kv.Value.clip) { musicSources[i].Stop(); musicSources[i].clip = null; }
                UnityEngine.Object.Destroy(kv.Value.clip);
                musicClips.Remove(kv.Key);
                musicCacheSamples -= kv.Value.samples;
            }
        }

        private void schedule(CachedClip cc, double start)
        {
            if (cc == null || cc.clip == null) return;
            double now = dspNow;
            int voice = -1;
            for (int n = 0; n < musicSources.Length; n++)
            {
                int i = (musicRR + n) % musicSources.Length;
                if (musicBusy[i] < now) { voice = i; break; }
            }
            if (voice < 0)
            {
                // alle belegt: die am frühesten endende Stimme übernehmen
                voice = 0;
                for (int i = 1; i < musicBusy.Length; i++) if (musicBusy[i] < musicBusy[voice]) voice = i;
            }
            musicRR = (voice + 1) % musicSources.Length;
            var src = musicSources[voice];
            double end = Math.Max(start, now) + cc.clip.length;
            musicBusy[voice] = end;
            cc.lastEnd = Math.Max(cc.lastEnd, end);
            src.clip = cc.clip;
            src.volume = (float)Clamp01(musicGain);
            src.PlayScheduled(start);
        }
    }

    /** Treibt die Musikplanung (statt window.setInterval). */
    internal sealed class AudioSystemRunner : MonoBehaviour
    {
        public AudioSystem sys;
        private void Update() { sys?.tick(); }
    }

    // ================================================================ Synthese

    /** Mono-Renderpuffer, der wächst; Teile werden additiv eingemischt (wie Knoten auf einem Bus). */
    internal sealed class Mix
    {
        public readonly int sr;
        public float[] buf = new float[0];
        public int len;
        public bool usedNoise;
        private readonly float[] noise;
        private readonly System.Random rnd;

        public Mix(int sr, float[] noise, System.Random rnd) { this.sr = sr; this.noise = noise; this.rnd = rnd; }

        public void Ensure(int n)
        {
            if (n > buf.Length) Array.Resize(ref buf, Math.Max(n, buf.Length * 2));
            if (n > len) len = n;
        }

        /** tone() aus dem TS: Oszillator + optionaler Tiefpass + exponentielle Hüllkurve. */
        public void tone(double freq, double start, double dur, string type, double gain,
            double? attack = null, double? slide = null, double? filter = null, double? q = null, double? detune = null)
        {
            int s0 = (int)Math.Round(start * sr);
            int n = (int)Math.Ceiling((dur + 0.05) * sr);
            Ensure(s0 + n);
            double detMul = detune.HasValue && detune.Value != 0 ? Math.Pow(2, detune.Value / 1200) : 1;
            double f = freq * detMul;
            bool hasSlide = slide.HasValue && slide.Value != 0;
            int nd = Math.Max(2, (int)Math.Round(dur * sr));
            double fMul = hasSlide ? Math.Pow(Math.Max(20, slide.Value) / freq, 1.0 / nd) : 1;
            double a = attack ?? 0.005;
            if (a >= dur) a = dur * 0.5;
            double G = Math.Max(0.0002, gain);
            int na = Math.Max(1, (int)Math.Round(a * sr));
            if (na >= nd) na = nd - 1;
            double eA = Math.Pow(G / 0.0001, 1.0 / na);
            double eD = Math.Pow(0.0001 / G, 1.0 / (nd - na));
            Biquad bq = null;
            if (filter.HasValue && filter.Value != 0) { bq = new Biquad(); bq.Set("lowpass", filter.Value, q ?? 0.7, sr); }
            int w = type == "sine" ? 0 : type == "square" ? 1 : type == "sawtooth" ? 2 : 3;
            double ph = 0, env = 0.0001;
            for (int i = 0; i < n; i++)
            {
                double dt = f / sr;
                if (dt > 0.5) dt = 0.5;
                double x;
                switch (w)
                {
                    case 0: x = Math.Sin(2 * Math.PI * ph); break;
                    case 1:
                        x = ph < 0.5 ? 1 : -1;
                        x += Dsp.PolyBlep(ph, dt);
                        x -= Dsp.PolyBlep((ph + 0.5) % 1.0, dt);
                        break;
                    case 2: x = 2 * ph - 1 - Dsp.PolyBlep(ph, dt); break;
                    default: x = ph < 0.25 ? 4 * ph : ph < 0.75 ? 2 - 4 * ph : 4 * ph - 4; break;
                }
                ph += dt;
                if (ph >= 1) ph -= 1;
                if (hasSlide && i < nd) f *= fMul;
                if (bq != null) x = bq.Process(x);
                buf[s0 + i] += (float)(x * env);
                if (i < na) env *= eA; else if (i < nd) env *= eD;
            }
        }

        /** noiseBurst() aus dem TS: Rauschpuffer (1 s, zufälliger Startversatz ≤ 0,5 s) → Biquad → Hüllkurve. */
        public void noiseBurst(double start, double dur, double gain, string filterType, double freq, double? slide = null)
        {
            usedNoise = true;
            int s0 = (int)Math.Round(start * sr);
            int n = (int)Math.Ceiling((dur + 0.05) * sr);
            Ensure(s0 + n);
            int nd = Math.Max(1, (int)Math.Round(dur * sr));
            bool hasSlide = slide.HasValue && slide.Value != 0;
            double fMul = hasSlide ? Math.Pow(slide.Value / freq, 1.0 / nd) : 1;
            double g = gain;
            double gMul = Math.Pow(0.0001 / Math.Max(1e-9, gain), 1.0 / nd);
            var bq = new Biquad();
            double f = freq;
            bq.Set(filterType, f, 1, sr);
            double offset = rnd.NextDouble() * 0.5 * AudioSystem.SR;
            double step = (double)AudioSystem.SR / sr;
            for (int i = 0; i < n; i++)
            {
                int idx = (int)(offset + i * step);
                double x = idx < noise.Length ? noise[idx] : 0;   // Puffer endet → Quelle verstummt (wie im Original)
                if (hasSlide && i < nd)
                {
                    f *= fMul;
                    if ((i & 15) == 0) bq.Set(filterType, f, 1, sr);
                }
                x = bq.Process(x);
                buf[s0 + i] += (float)(x * g);
                if (i < nd) g *= gMul;
            }
        }

        public AudioClip ToClip(string name)
        {
            // Stille am Ende abschneiden, hart begrenzen (statt Kompressor)
            int end = len;
            while (end > 1 && Math.Abs(buf[end - 1]) < 1e-4f) end--;
            if (end < 1) end = 1;
            var data = new float[end];
            for (int i = 0; i < end; i++)
            {
                float x = buf[i];
                data[i] = x > 1f ? 1f : x < -1f ? -1f : x;
            }
            var clip = AudioClip.Create(name, end, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }
    }

    /** Biquad nach WebAudio-Spezifikation (Lowpass/Highpass: Q in dB; Bandpass: Q linear). */
    internal sealed class Biquad
    {
        private double b0, b1, b2, a1, a2, z1, z2;

        public void Set(string type, double freq, double q, int sr)
        {
            double nyq = sr * 0.5;
            double f = Math.Min(Math.Max(freq, 10), nyq * 0.999);
            double w0 = 2 * Math.PI * f / sr;
            double cw = Math.Cos(w0), sw = Math.Sin(w0);
            double alpha, n0, n1, n2, d0, d1, d2;
            switch (type)
            {
                case "highpass":
                    alpha = sw / (2 * Math.Pow(10, q / 20));
                    n0 = (1 + cw) / 2; n1 = -(1 + cw); n2 = (1 + cw) / 2;
                    break;
                case "bandpass":
                    alpha = sw / (2 * Math.Max(1e-4, q));
                    n0 = alpha; n1 = 0; n2 = -alpha;
                    break;
                default: // lowpass
                    alpha = sw / (2 * Math.Pow(10, q / 20));
                    n0 = (1 - cw) / 2; n1 = 1 - cw; n2 = (1 - cw) / 2;
                    break;
            }
            d0 = 1 + alpha; d1 = -2 * cw; d2 = 1 - alpha;
            b0 = n0 / d0; b1 = n1 / d0; b2 = n2 / d0; a1 = d1 / d0; a2 = d2 / d0;
        }

        public double Process(double x)
        {
            double y = b0 * x + z1;
            z1 = b1 * x - a1 * y + z2;
            z2 = b2 * x - a2 * y;
            return y;
        }
    }

    internal static class Dsp
    {
        public static double PolyBlep(double t, double dt)
        {
            if (t < dt) { t /= dt; return t + t - t * t - 1; }
            if (t > 1 - dt) { t = (t - 1) / dt; return t * t + t + t + 1; }
            return 0;
        }

        /** Echoleitung des Originals: Delay 0,32 s → Tiefpass 2200 → Rückkopplung 0,28; nur das Echo ist hörbar. */
        public static void Echo(Mix m, double delaySec, double feedback, double lpFreq)
        {
            int D = Math.Max(1, (int)Math.Round(delaySec * m.sr));
            int inLen = m.len;
            int total = inLen + D * 7;
            var x = new float[inLen];
            Array.Copy(m.buf, x, inLen);
            var w = new double[total];
            var lp = new Biquad();
            lp.Set("lowpass", lpFreq, 1, m.sr);
            for (int n = 0; n < total; n++)
            {
                int k = n - D;
                double v = k >= 0 ? (k < inLen ? x[k] : 0) + feedback * w[k] : 0;
                w[n] = lp.Process(v);
            }
            m.buf = new float[total];
            m.len = total;
            for (int n = 0; n < total; n++) m.buf[n] = (float)w[n];
        }

        private static readonly int[] combD = { 1557, 1617, 1491, 1422, 1277, 1356 };
        private static readonly int[] apD = { 556, 441, 341, 225 };

        /**
         * Hall: ersetzt den ConvolverNode (Rausch-Impulsantwort 2,4 s, Abfall (1-t)^2.8) durch einen
         * Schroeder/Freeverb-Hall mit ähnlicher Nachhallzeit. Ergebnis = trocken + send · Hall.
         */
        public static void Reverb(Mix m, double send, double tailSec)
        {
            if (send <= 0) return;
            int sr = m.sr;
            double scale = sr / 44100.0;
            int inLen = m.len;
            m.Ensure(inLen + (int)(tailSec * sr));
            int total = m.len;
            const double rt60 = 1.7, damp = 0.3;
            int nc = combD.Length;
            var cBuf = new double[nc][]; var cIdx = new int[nc]; var cG = new double[nc]; var cIn = new double[nc]; var cStore = new double[nc];
            for (int c = 0; c < nc; c++)
            {
                int d = Math.Max(1, (int)(combD[c] * scale));
                cBuf[c] = new double[d];
                cG[c] = Math.Pow(10, -3.0 * d / (sr * rt60));
                cIn[c] = Math.Sqrt(1 - cG[c] * cG[c]) / Math.Sqrt(nc);
            }
            var aBuf = new double[apD.Length][]; var aIdx = new int[apD.Length];
            for (int a = 0; a < apD.Length; a++) aBuf[a] = new double[Math.Max(1, (int)(apD[a] * scale))];
            var dry = m.buf;
            for (int n = 0; n < total; n++)
            {
                double input = n < inLen ? dry[n] : 0;
                double acc = 0;
                for (int c = 0; c < nc; c++)
                {
                    var bb = cBuf[c];
                    double o = bb[cIdx[c]];
                    cStore[c] = o * (1 - damp) + cStore[c] * damp;
                    bb[cIdx[c]] = input * cIn[c] + cStore[c] * cG[c];
                    if (++cIdx[c] >= bb.Length) cIdx[c] = 0;
                    acc += o;
                }
                for (int a = 0; a < aBuf.Length; a++)
                {
                    var bb = aBuf[a];
                    double bo = bb[aIdx[a]];
                    double o = -acc + bo;
                    bb[aIdx[a]] = acc + bo * 0.5;
                    if (++aIdx[a] >= bb.Length) aIdx[a] = 0;
                    acc = o;
                }
                dry[n] += (float)(send * acc);
            }
        }
    }
}
