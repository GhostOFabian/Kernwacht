using System.IO;
using Kernwacht.Models;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Kernwacht.Game.EditorTools
{
    /// <summary>
    /// Menü „Kernwacht → 3 · Spielszene erzeugen“ und „4 · WebGL-Build erstellen“.
    /// Legt Materialien und Spiel-Assets an, baut die Szene Kernwacht.unity, trägt sie in die Build-Liste ein
    /// und stellt die WebGL-Einstellungen für ein Browserspiel ein.
    /// </summary>
    public static class GameSetup
    {
        const string Root = "Assets/Kernwacht";
        const string CatalogAsset = Root + "/Data/ModelCatalog.asset";
        const string ResDir = Root + "/Resources";
        const string MatDir = Root + "/Data/Materialien";
        const string ScenePath = Root + "/Scenes/Kernwacht.unity";

        [MenuItem("Kernwacht/3 · Spielszene erzeugen", priority = 3)]
        public static void BuildGameScene()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ModelCatalog>(CatalogAsset);
            if (catalog == null || catalog.entries.Count == 0)
                Debug.LogWarning("[Kernwacht] Modellkatalog fehlt oder ist leer – erst „1 · Modelle einrichten“ ausführen. Die Szene wird trotzdem angelegt (Ersatzformen).");

            EnsureDir(ResDir);
            EnsureDir(MatDir);
            EnsureDir(Root + "/Scenes");

            var lit = LitMaterial();
            var unlit = FxMaterial("Kernwacht_Effekt");
            var transparent = FxMaterial("Kernwacht_Transparent");

            var ga = AssetDatabase.LoadAssetAtPath<GameAssets>(ResDir + "/KernwachtGameAssets.asset");
            if (ga == null)
            {
                ga = ScriptableObject.CreateInstance<GameAssets>();
                AssetDatabase.CreateAsset(ga, ResDir + "/KernwachtGameAssets.asset");
            }
            ga.catalog = catalog;
            ga.lit = lit;
            ga.unlit = unlit;
            ga.transparent = transparent;
            EditorUtility.SetDirty(ga);
            AssetDatabase.SaveAssets();

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            // Einziger Einstiegspunkt: GameApp baut Kamera, Licht, Canvas (UI-Ebenen) und EventSystem zur Laufzeit auf.
            var go = new GameObject("Kernwacht");
            go.AddComponent<GameApp>();
            var cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position = new Vector3(11, 16, -6);
                cam.transform.rotation = Quaternion.Euler(56, 0, 0);
                cam.orthographic = true;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0x12 / 255f, 0x0f / 255f, 0x1c / 255f);
            }
            EditorSceneManager.SaveScene(scene, ScenePath);

            // Szene an erste Stelle der Build-Liste
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
            foreach (var s in EditorBuildSettings.scenes) if (s.path != ScenePath) list.Add(new EditorBuildSettingsScene(s.path, false));
            EditorBuildSettings.scenes = list.ToArray();

            ConfigureWebGL();
            AssetDatabase.SaveAssets();
            Debug.Log("[Kernwacht] Spielszene erzeugt: " + ScenePath + " – auf Play drücken zum Spielen. Für den Browser: „Kernwacht → 4 · WebGL-Build erstellen“.");
        }

        static Material LitMaterial()
        {
            var path = MatDir + "/Kernwacht_Gelaende.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            else m.shader = sh;
            m.enableInstancing = true;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.15f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Unbeleuchtetes, durchscheinendes Material mit Vertexfarbe (Strahlen, Spuren, Kreise).</summary>
        static Material FxMaterial(string name)
        {
            var path = MatDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            bool urp = sh != null;
            if (!urp) sh = Shader.Find("Sprites/Default");
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            else m.shader = sh;
            if (urp)
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            }
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        static void ConfigureWebGL()
        {
            PlayerSettings.productName = "Kernwacht";
            PlayerSettings.companyName = "GhostOFabian";
            PlayerSettings.runInBackground = true;
            // Bezugsauflösung der Oberfläche (CanvasScaler 1920×1080)
            PlayerSettings.defaultWebScreenWidth = 1920;
            PlayerSettings.defaultWebScreenHeight = 1080;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.template = Directory.Exists("Assets/WebGLTemplates/Kernwacht") ? "PROJECT:Kernwacht" : "APPLICATION:Default";
            // 4.1: CPU schonen – Emscripten/Binaryen (Linken, wasm-opt) nur mit 2 Kernen statt allen
            System.Environment.SetEnvironmentVariable("EMCC_CORES", "2");
            System.Environment.SetEnvironmentVariable("BINARYEN_CORES", "2");
            // 4.1: kürzere Build-Zeit (weniger CPU-Volllast beim Linken; das Spiel wird etwas größer)
            try
            {
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    var t = asm.GetType("UnityEditor.WebGL.UserBuildSettings");
                    if (t == null) continue;
                    var p = t.GetProperty("codeOptimization", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (p == null) break;
                    var names = System.Enum.GetNames(p.PropertyType);
                    string want = System.Array.IndexOf(names, "BuildTimes") >= 0 ? "BuildTimes" : null;
                    if (want != null) p.SetValue(null, System.Enum.Parse(p.PropertyType, want));
                    Debug.Log($"[Kernwacht] WebGL-Codeoptimierung: {p.GetValue(null)} (verfügbar: {string.Join(", ", names)})");
                    break;
                }
            }
            catch (System.Exception e) { Debug.LogWarning("[Kernwacht] Codeoptimierung nicht gesetzt: " + e.Message); }
        }

        [MenuItem("Kernwacht/4 · WebGL-Build erstellen", priority = 4)]
        public static void BuildWebGL()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            {
                EditorUtility.DisplayDialog("Kernwacht", "Das Modul „Web Build Support“ ist nicht installiert.\n\nUnity Hub → Installs → Zahnrad bei deiner Unity-Version → Add modules → „Web Build Support“ anhaken.", "OK");
                return;
            }
            if (!File.Exists(ScenePath)) BuildGameScene();
            ConfigureWebGL();
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/WebGL",
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.Log($"[Kernwacht] WebGL-Build fertig: {Path.GetFullPath("Builds/WebGL")} ({report.summary.totalSize / (1024 * 1024)} MB). Zum Testen: File → Build And Run oder den Ordner auf einen Webserver legen.");
            }
            else Debug.LogError("[Kernwacht] WebGL-Build fehlgeschlagen: " + report.summary.result);
        }

        static void EnsureDir(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureDir(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
