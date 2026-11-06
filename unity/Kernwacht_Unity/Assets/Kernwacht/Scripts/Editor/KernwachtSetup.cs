using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kernwacht.Models;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Kernwacht.Models.EditorTools
{
    /// <summary>
    /// Menü „Kernwacht“: glTFast installieren, alle Modelle als Prefabs mit Animator einrichten,
    /// Modellkatalog erzeugen und eine Vorschauszene bauen. Alles ist wiederholbar (überschreibt die eigenen Ausgaben).
    /// </summary>
    public static class KernwachtSetup
    {
        const string Root = "Assets/Kernwacht";
        const string CatalogJson = Root + "/Data/catalog.json";
        const string CatalogAsset = Root + "/Data/ModelCatalog.asset";
        const string PrefabDir = Root + "/Prefabs";
        const string AnimDir = Root + "/Animations";
        const string ControllerDir = Root + "/Animators";
        const string SceneDir = Root + "/Scenes";
        const string GltfPackage = "com.unity.cloud.gltfast";

        [Serializable] class Item { public string id, title, category, path, notes; public int tris, bones; public string[] clips, nodes; }
        [Serializable] class Catalog { public int version; public float cellSize; public Item[] items; }

        // ------------------------------------------------------------------ 0 · glTFast
        static UnityEditor.PackageManager.Requests.AddRequest _add;

        [MenuItem("Kernwacht/0 · glTFast installieren (einmalig)", priority = 0)]
        public static void InstallGltf()
        {
            _add = UnityEditor.PackageManager.Client.Add(GltfPackage);
            EditorApplication.update += WaitAdd;
            Debug.Log("[Kernwacht] Installiere glTFast …");
        }

        static void WaitAdd()
        {
            if (_add == null || !_add.IsCompleted) return;
            EditorApplication.update -= WaitAdd;
            if (_add.Status == UnityEditor.PackageManager.StatusCode.Success)
                Debug.Log($"[Kernwacht] glTFast {_add.Result.version} installiert. Danach „Kernwacht → 1 · Modelle einrichten“ ausführen.");
            else
                Debug.LogError("[Kernwacht] glTFast konnte nicht installiert werden: " + _add.Error?.message);
            _add = null;
        }

        // ------------------------------------------------------------------ 1 · Modelle einrichten
        [MenuItem("Kernwacht/1 · Modelle einrichten (Prefabs, Animator, Katalog)", priority = 1)]
        public static void SetupAll()
        {
            var cat = LoadCatalog();
            if (cat == null) return;
            EnsureDir(PrefabDir); EnsureDir(AnimDir); EnsureDir(ControllerDir);
            int ok = 0, failed = 0;
            var entries = new List<ModelCatalog.Entry>();
            KernwachtTextures.Reset();
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var it in cat.items) ConfigureImporter(it.path);
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.Refresh();

            for (int i = 0; i < cat.items.Length; i++)
            {
                var it = cat.items[i];
                EditorUtility.DisplayProgressBar("Kernwacht", $"{it.title} ({i + 1}/{cat.items.Length})", (float)i / cat.items.Length);
                try
                {
                    var prefab = BuildPrefab(it);
                    if (prefab == null) { failed++; continue; }
                    entries.Add(new ModelCatalog.Entry { id = it.id, title = it.title, category = ParseCategory(it.category), prefab = prefab, triangles = it.tris, notes = it.notes });
                    ok++;
                }
                catch (Exception e)
                {
                    failed++;
                    Debug.LogError($"[Kernwacht] {it.id}: {e.Message}\n{e.StackTrace}");
                }
            }
            EditorUtility.ClearProgressBar();

            var catalog = AssetDatabase.LoadAssetAtPath<ModelCatalog>(CatalogAsset);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ModelCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogAsset);
            }
            catalog.entries = entries;
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Kernwacht] Fertig: {ok} Prefabs eingerichtet, {failed} fehlgeschlagen. Katalog: {CatalogAsset}. Texturen: {KernwachtTextures.exported} komprimiert, {KernwachtTextures.reused} doppelte zusammengefasst.");
            Selection.activeObject = catalog;
        }

        static Catalog LoadCatalog()
        {
            var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(CatalogJson);
            if (ta == null) { Debug.LogError($"[Kernwacht] {CatalogJson} fehlt."); return null; }
            return JsonUtility.FromJson<Catalog>(ta.text);
        }

        static ModelCategory ParseCategory(string c)
        {
            switch (c)
            {
                case "tower": return ModelCategory.Tower;
                case "enemy": return ModelCategory.Enemy;
                case "boss": return ModelCategory.Boss;
                case "hero": return ModelCategory.Hero;
                default: return ModelCategory.World;
            }
        }

        /// <summary>glTFast auf Mecanim-Animationen stellen (über SerializedObject, damit dieses Skript auch ohne glTFast kompiliert).</summary>
        static void ConfigureImporter(string path)
        {
            var imp = AssetImporter.GetAtPath(path);
            if (imp == null) return;
            if (imp.GetType().Name != "GltfImporter")
            {
                if (imp is ModelImporter mi)  // FBX-Weg (aus dem Blender-Skript)
                {
                    mi.animationType = ModelImporterAnimationType.Generic;
                    mi.importAnimation = true;
                    mi.SaveAndReimport();
                }
                return;
            }
            var so = new SerializedObject(imp);
            SerializedProperty p = null;
            foreach (var name in new[] { "importSettings.animationMethod", "m_ImportSettings.animationMethod", "ImportSettings.animationMethod", "m_ImportSettings.m_AnimationMethod" })
            {
                p = so.FindProperty(name);
                if (p != null) break;
            }
            if (p == null) { Debug.LogWarning($"[Kernwacht] Animationseinstellung im glTFast-Importer nicht gefunden ({path}). Bitte im Inspector „Animation Method: Mecanim“ wählen."); return; }
            if (p.propertyType == SerializedPropertyType.Enum || p.propertyType == SerializedPropertyType.Integer)
            {
                const int mecanim = 2; // glTFast: None = 0, Legacy = 1, Mecanim = 2
                if (p.intValue != mecanim)
                {
                    p.intValue = mecanim;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    imp.SaveAndReimport();
                }
            }
        }

        static GameObject BuildPrefab(Item it)
        {
            var src = AssetDatabase.LoadMainAssetAtPath(it.path) as GameObject;
            if (src == null)
            {
                Debug.LogError($"[Kernwacht] {it.path} lässt sich nicht als Modell laden – ist glTFast installiert?");
                return null;
            }
            // Animationen als eigene .anim kopieren (Schleifen einstellbar, unabhängig vom Importer)
            var clips = AssetDatabase.LoadAllAssetsAtPath(it.path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToList();
            var ctrl = clips.Count > 0 ? BuildController(it.id, clips) : null;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
            if (go == null) go = Object.Instantiate(src);
            go.name = it.id;
            try
            {
                var legacy = go.GetComponent<Animation>();
                if (legacy != null) Object.DestroyImmediate(legacy);
                var anim = Ensure<Animator>(go);
                anim.runtimeAnimatorController = ctrl;
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

                var info = Ensure<ModelInfo>(go);
                info.id = it.id; info.title = it.title; info.category = ParseCategory(it.category);
                info.footprint = it.id == "goldmine" || it.id == "treasury" || it.id == "deepdrill" ? 2 : 1;

                switch (info.category)
                {
                    case ModelCategory.Tower: Ensure<TowerView>(go); break;
                    case ModelCategory.World: Ensure<WorldPropView>(go); break;
                    default:
                        var u = Ensure<UnitView>(go);
                        u.walkSpeedReference = info.category == ModelCategory.Boss ? 0.8f : 1f;
                        break;
                }
                KernwachtTextures.Apply(go, it.id);
                AddPickCollider(go);
                var dir = $"{PrefabDir}/{it.category}";
                EnsureDir(dir);
                var path = $"{dir}/{it.id}.prefab";
                return PrefabUtility.SaveAsPrefabAsset(go, path);
            }
            finally { Object.DestroyImmediate(go); }
        }

        static AnimatorController BuildController(string id, List<AnimationClip> clips)
        {
            var dir = $"{AnimDir}/{id}";
            EnsureDir(dir);
            var copies = new List<AnimationClip>();
            foreach (var c in clips)
            {
                var p = $"{dir}/{c.name}.anim";
                var copy = Object.Instantiate(c);
                copy.name = c.name;
                copy.legacy = false;
                var s = AnimationUtility.GetAnimationClipSettings(copy);
                s.loopTime = c.name == "Idle" || c.name == "Walk";
                AnimationUtility.SetAnimationClipSettings(copy, s);
                if (AssetDatabase.LoadAssetAtPath<AnimationClip>(p) != null) AssetDatabase.DeleteAsset(p);
                AssetDatabase.CreateAsset(copy, p);
                copies.Add(copy);
            }
            var cp = $"{ControllerDir}/{id}.controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(cp) != null) AssetDatabase.DeleteAsset(cp);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(cp);
            var sm = ctrl.layers[0].stateMachine;
            AnimatorState idle = null;
            foreach (var c in copies)
            {
                var st = sm.AddState(c.name);
                st.motion = c;
                if (c.name == "Idle") idle = st;
            }
            if (idle != null) sm.defaultState = idle;
            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }

        static void AddPickCollider(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return;
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            var col = Ensure<BoxCollider>(go);
            col.center = go.transform.InverseTransformPoint(b.center);
            col.size = b.size;
        }

        /// <summary>Komponente holen oder anlegen (der Null-Operator scheitert im Editor an Unitys Fake-Null).</summary>
        static T Ensure<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c == null) c = go.AddComponent<T>();
            return c;
        }

        static void EnsureDir(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureDir(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // ------------------------------------------------------------------ 2 · Vorschauszene
        [MenuItem("Kernwacht/2 · Vorschauszene erzeugen", priority = 2)]
        public static void BuildPreviewScene()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ModelCatalog>(CatalogAsset);
            if (catalog == null || catalog.entries.Count == 0) { Debug.LogError("[Kernwacht] Erst „1 · Modelle einrichten“ ausführen."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Boden";
            ground.transform.localScale = new Vector3(6, 1, 4);
            ground.transform.position = new Vector3(14, 0, 10);
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            var mat = new Material(sh) { color = new Color(0.14f, 0.12f, 0.16f) };
            ground.GetComponent<Renderer>().sharedMaterial = mat;
            AssetDatabase.CreateAsset(mat, $"{Root}/Data/Vorschau_Boden.mat");

            var order = new[] { ModelCategory.Tower, ModelCategory.Enemy, ModelCategory.Boss, ModelCategory.Hero, ModelCategory.World };
            float z = 0;
            foreach (var c in order)
            {
                var row = new GameObject(c.ToString());
                float x = 0, rowDepth = 1.5f;
                foreach (var e in catalog.OfCategory(c))
                {
                    if (e.prefab == null) continue;
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(e.prefab, row.transform);
                    float step = c == ModelCategory.Boss ? 3.5f : (e.id == "goldmine" || e.id == "treasury" || e.id == "deepdrill" ? 2.5f : 1.6f);
                    inst.transform.position = new Vector3(x + step * 0.5f, 0, z);
                    inst.transform.rotation = Quaternion.Euler(0, 180, 0);
                    inst.AddComponent<DemoCycler>();
                    x += step;
                    rowDepth = Mathf.Max(rowDepth, step);
                }
                z += rowDepth + 0.8f;
            }
            var cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position = new Vector3(14, 14, -12);
                cam.transform.rotation = Quaternion.Euler(42, 0, 0);
                cam.backgroundColor = new Color(0.07f, 0.06f, 0.09f);
                cam.clearFlags = CameraClearFlags.SolidColor;
            }
            EnsureDir(SceneDir);
            EditorSceneManager.SaveScene(scene, $"{SceneDir}/Modellvorschau.unity");
            Debug.Log("[Kernwacht] Vorschauszene gespeichert: " + SceneDir + "/Modellvorschau.unity – auf Play drücken, die Modelle spielen ihre Animationen reihum ab.");
        }

        // ------------------------------------------------------------------ Hilfe
        [MenuItem("Kernwacht/Anleitung öffnen", priority = 50)]
        public static void OpenReadme()
        {
            var p = Root + "/README_UNITY.md";
            var a = AssetDatabase.LoadAssetAtPath<TextAsset>(p);
            if (a != null) AssetDatabase.OpenAsset(a);
            else Debug.Log("[Kernwacht] " + p + " nicht gefunden.");
        }
    }
}
