using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Kernwacht.Models.EditorTools
{
    /// <summary>
    /// Holt die eingebetteten glTF-Texturen als PNG-Assets heraus, fasst identische Texturen zusammen und lässt Unity
    /// sie je Zielplattform komprimieren (WebGL: DXT/ASTC, Crunch). Die Prefabs bekommen Materialkopien mit diesen Texturen.
    /// Ohne diesen Schritt landen alle Texturen unkomprimiert im Build (rund 350 MB).
    /// </summary>
    public static class KernwachtTextures
    {
        const string TexDir = "Assets/Kernwacht/Texturen";
        const string MatDir = "Assets/Kernwacht/Materialien";
        public const int MaxSize = 512;

        static readonly Dictionary<Texture, Texture2D> texCache = new Dictionary<Texture, Texture2D>();
        static readonly Dictionary<string, Texture2D> hashCache = new Dictionary<string, Texture2D>();
        static readonly Dictionary<Material, Material> matCache = new Dictionary<Material, Material>();
        public static int exported, reused;

        public static void Reset()
        {
            texCache.Clear(); hashCache.Clear(); matCache.Clear();
            exported = 0; reused = 0;
        }

        /// <summary>Alle Renderer des Objekts auf optimierte Materialkopien umstellen.</summary>
        public static void Apply(GameObject go, string id)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    var c = CopyMaterial(mats[i], id);
                    if (c != mats[i]) { mats[i] = c; changed = true; }
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        static string Safe(string n)
        {
            foreach (var ch in Path.GetInvalidFileNameChars()) n = n.Replace(ch, '_');
            return n.Replace(' ', '_');
        }

        static Material CopyMaterial(Material src, string id)
        {
            if (matCache.TryGetValue(src, out var m)) return m;
            // Material liegt schon als eigenes Asset (nicht im .glb)? Dann unverändert lassen.
            var srcPath = AssetDatabase.GetAssetPath(src);
            if (!string.IsNullOrEmpty(srcPath) && !srcPath.EndsWith(".glb") && !srcPath.EndsWith(".gltf")) { matCache[src] = src; return src; }

            var dir = $"{MatDir}/{id}";
            EnsureDir(dir);
            var copy = new Material(src) { name = src.name };
            foreach (var prop in src.GetTexturePropertyNames())
            {
                var t = src.GetTexture(prop) as Texture2D;
                if (t == null) continue;
                var nt = ExportTexture(t);
                if (nt != null) copy.SetTexture(prop, nt);
            }
            var path = $"{dir}/{Safe(src.name)}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(copy, path);
            matCache[src] = copy;
            return copy;
        }

        static Texture2D ExportTexture(Texture2D t)
        {
            if (texCache.TryGetValue(t, out var cached)) return cached;
            bool srgb = t.isDataSRGB;
            int w = Mathf.Min(t.width, MaxSize), h = Mathf.Min(t.height, MaxSize);
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, srgb ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear);
            var prev = RenderTexture.active;
            Graphics.Blit(t, rt);
            RenderTexture.active = rt;
            var tmp = new Texture2D(w, h, TextureFormat.RGBA32, false, !srgb);
            tmp.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tmp.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            var png = tmp.EncodeToPNG();
            Object.DestroyImmediate(tmp);

            string hash;
            using (var md5 = MD5.Create()) hash = System.BitConverter.ToString(md5.ComputeHash(png)).Replace("-", "").Substring(0, 16).ToLowerInvariant();
            hash += srgb ? "_c" : "_l";
            if (hashCache.TryGetValue(hash, out var same)) { texCache[t] = same; reused++; return same; }

            EnsureDir(TexDir);
            var path = $"{TexDir}/{Safe(t.name)}_{hash}.png";
            if (!File.Exists(path)) File.WriteAllBytes(path, png);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null)
            {
                imp.textureType = TextureImporterType.Default;
                imp.sRGBTexture = srgb;
                imp.alphaSource = TextureImporterAlphaSource.FromInput;
                imp.mipmapEnabled = true;
                imp.maxTextureSize = MaxSize;
                imp.textureCompression = TextureImporterCompression.Compressed;
                imp.crunchedCompression = true;
                imp.compressionQuality = 60;
                imp.wrapMode = TextureWrapMode.Repeat;
                imp.SaveAndReimport();
            }
            var asset = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            texCache[t] = asset;
            hashCache[hash] = asset;
            exported++;
            return asset;
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
