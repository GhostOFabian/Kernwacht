// Modelle der Welt: Prefab-Pool (Türme, Gegner, Bosse, Helden, Weltobjekte aus dem ModelCatalog), Größennormierung
// auf die Maße aus render/models.ts, Einfärben per MaterialPropertyBlock und Geisterbilder.
// Außerdem die Kenngrößen aus models.ts (TOWER_TOP, BOSS_HEIGHT, WEAKPOINT_POS) und die kleinen Zusatzteile
// (Ausbaustufen, Spezialisierung, Drache), die weiterhin aus Grundkörpern gezeichnet werden.
using System.Collections.Generic;
using Kernwacht.Models;
using Kernwacht.Sim;
using UnityEngine;

namespace Kernwacht.Game
{
    /// <summary>Grundkörper-Teil eines Modells (models.ts Part). c: −1 Haupt-, −2 Akzent-, −3 Sockelfarbe.</summary>
    public struct Part
    {
        public string m;
        public float x, y, z, sx, sy, sz;
        public int c;
        public float e, yaw, pitch, roll, spin, bob, wing;
        public int glow;
        public Part(string m, float x, float y, float z, float sx, float sy, float sz, int c, float e = 0, float yaw = 0, float pitch = 0, float roll = 0, float spin = 0, float bob = 0, float wing = 0, int glow = 0)
        {
            this.m = m; this.x = x; this.y = y; this.z = z; this.sx = sx; this.sy = sy; this.sz = sz; this.c = c;
            this.e = e; this.yaw = yaw; this.pitch = pitch; this.roll = roll; this.spin = spin; this.bob = bob; this.wing = wing; this.glow = glow;
        }
    }

    /// <summary>Eine Prefab-Instanz aus dem Pool.</summary>
    public class ModelInst
    {
        public string id;
        /// <summary>4.1: Pool-/Prefab-Kennung (im Sci-Fi-Thema "sf_"+id).</summary>
        public string key;
        public GameObject go;
        public Transform tr;
        public TowerView tower;
        public UnitView unit;
        public WorldPropView prop;
        public Animator anim;
        public bool fallback;
        public float nativeH = 1, nativeMinY;
        /// <summary>Mitte (x/z) und größte waagerechte Ausdehnung des Modells bei Maßstab 1, ungedreht.</summary>
        public float nativeCX, nativeCZ, nativeFoot = 1;
        internal Renderer[] rs;
        internal Material[][] orig;
        internal Material[][] ghostMats;
        internal int[][] colorProp;     // je Renderer/Material: Index in PROPS oder −1
        internal Color[][] origColor;
        internal int[][] emitProp;
        int tintKey = int.MinValue;
        bool ghost;
        int ghostKey = int.MinValue;
        static readonly MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        internal static readonly int[] COLOR_PROPS = { Shader.PropertyToID("baseColorFactor"), Shader.PropertyToID("_BaseColor"), Shader.PropertyToID("_Color") };
        internal static readonly int[] EMIT_PROPS = { Shader.PropertyToID("emissiveFactor"), Shader.PropertyToID("_EmissionColor") };
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>Spielposition (Unterkante des Modells bei y), Zielhöhe in Welteinheiten, optional gestaucht.</summary>
        public void Place(float x, float y, float z, float height, float squash = 1, float maxFoot = 0)
        {
            float k = Mathf.Max(0.001f, height) / Mathf.Max(0.05f, nativeH);
            // Lange Modelle (Vierbeiner) nicht breiter als den Weg werden lassen
            if (maxFoot > 0 && nativeFoot * k > maxFoot) k = maxFoot / Mathf.Max(0.05f, nativeFoot);
            tr.localScale = new Vector3(k, k * squash, k);
            var p = IsoCamera.W(x, y - nativeMinY * k * squash, z);
            // Figuren um die Körpermitte statt um den Modellursprung (bei Vierbeinern das Hinterteil) platzieren
            if (unit != null && (nativeCX != 0 || nativeCZ != 0))
            {
                var off = tr.rotation * new Vector3(nativeCX * k, 0, nativeCZ * k);
                p.x -= off.x;
                p.z -= off.z;
            }
            tr.position = p;
        }

        /// <summary>Blickrichtung aus einem Spielwinkel (Richtung (cos a, sin a) in x/z-Spielkoordinaten).</summary>
        public void SetHeading(float a)
        {
            var d = new Vector3(Mathf.Cos(a), 0, -Mathf.Sin(a));
            tr.rotation = Quaternion.LookRotation(d, Vector3.up);
            if (unit != null) unit.FaceTowards(tr.position + d);
        }

        public void SetActive(bool on)
        {
            if (go.activeSelf != on) go.SetActive(on);
        }

        /// <summary>Farbstich (amt 0 = aus) und Leuchten. Änderungen werden nur bei Bedarf übertragen.</summary>
        public void Tint(int color, float amt, float emit)
        {
            if (ghost) return;
            int qa = Mathf.Clamp(Mathf.RoundToInt(amt * 40), 0, 40), qe = Mathf.Clamp(Mathf.RoundToInt(emit * 40), 0, 80);
            int key = qa == 0 && qe == 0 ? 0 : ((color & 0xffffff) * 31 + qa) * 97 + qe;
            if (key == tintKey) return;
            tintKey = key;
            var tc = Col.ToColor(color);
            for (int r = 0; r < rs.Length; r++)
            {
                var rend = rs[r];
                if (rend == null) continue;
                if (key == 0) { rend.SetPropertyBlock(null); continue; }
                int nm = orig[r].Length;
                for (int i = 0; i < nm; i++)
                {
                    mpb.Clear();
                    int cp = colorProp[r][i];
                    if (cp >= 0 && qa > 0)
                    {
                        var o = origColor[r][i];
                        var c = Color.Lerp(o, new Color(tc.r, tc.g, tc.b, o.a), qa / 40f);
                        mpb.SetColor(COLOR_PROPS[cp], c);
                    }
                    int ep = emitProp[r][i];
                    if (ep >= 0 && qe > 0) mpb.SetColor(EMIT_PROPS[ep], tc * (qe / 40f) * 0.8f);
                    if (nm == 1) rend.SetPropertyBlock(mpb);
                    else rend.SetPropertyBlock(mpb, i);
                }
            }
        }

        /// <summary>Geisterbild (durchscheinend, unbeleuchtet) an/aus – Bauvorschau, Tarnung, Phasenschild.</summary>
        public void Ghost(bool on, Material ghostMat, int color, float alpha)
        {
            if (!on)
            {
                if (!ghost) return;
                ghost = false;
                ghostKey = int.MinValue;
                for (int r = 0; r < rs.Length; r++)
                {
                    if (rs[r] == null) continue;
                    rs[r].sharedMaterials = orig[r];
                    rs[r].SetPropertyBlock(null);
                }
                tintKey = int.MinValue;
                return;
            }
            if (ghostMat == null) return;
            if (!ghost)
            {
                ghost = true;
                for (int r = 0; r < rs.Length; r++)
                {
                    if (rs[r] == null) continue;
                    var gm = ghostMats[r];
                    for (int i = 0; i < gm.Length; i++) gm[i] = ghostMat;
                    rs[r].sharedMaterials = gm;
                }
            }
            int key = (color & 0xffffff) * 64 + Mathf.Clamp(Mathf.RoundToInt(alpha * 63), 0, 63);
            if (key == ghostKey) return;
            ghostKey = key;
            mpb.Clear();
            mpb.SetColor(BaseColorId, Col.ToColor(color, alpha));
            mpb.SetColor(COLOR_PROPS[2], Col.ToColor(color, alpha));
            for (int r = 0; r < rs.Length; r++) if (rs[r] != null) rs[r].SetPropertyBlock(mpb);
        }

        internal void ResetLook()
        {
            Ghost(false, null, 0, 0);
            Tint(0, 0, 0);
        }
    }

    /// <summary>Pool für Prefab-Instanzen je Modellkennung.</summary>
    public class ModelPool
    {
        readonly Dictionary<string, Stack<ModelInst>> pool = new Dictionary<string, Stack<ModelInst>>();
        readonly Dictionary<string, Vector4> measured = new Dictionary<string, Vector4>();
        readonly Dictionary<string, float> measuredFoot = new Dictionary<string, float>();
        public readonly Transform root;
        readonly Transform parked;
        Material fallbackBase;

        public ModelPool(Transform parent)
        {
            root = new GameObject("Modelle").transform;
            root.SetParent(parent, false);
            parked = new GameObject("Pool").transform;
            parked.SetParent(parent, false);
            parked.gameObject.SetActive(false);
        }

        public static ModelCatalog Catalog
        {
            get
            {
                var ga = GameAssets.Instance;
                return ga != null ? ga.catalog : null;
            }
        }

        public static bool Has(string id)
        {
            var c = Catalog;
            return c != null && id != null && c.Prefab(Themes.Model(id)) != null;
        }

        /// <summary>Instanz holen (Prefab oder Ersatzform in der Farbe fallbackColor).</summary>
        public ModelInst Acquire(string id, int fallbackColor = 0x9a9080, PrimitiveType fallbackShape = PrimitiveType.Cube, bool animate = true, Vector3? at = null)
        {
            ModelInst inst = null;
            string key = Themes.Model(id) ?? "";
            if (pool.TryGetValue(key, out var st))
                while (st.Count > 0 && inst == null)
                {
                    inst = st.Pop();
                    if (inst.go == null) inst = null;
                }
            if (inst == null) inst = Create(id, key, fallbackColor, fallbackShape);
            // Vor dem Aktivieren an die Zielstelle setzen (UnitView merkt sich in OnEnable die Startposition)
            if (at.HasValue) inst.tr.position = at.Value;
            inst.tr.SetParent(root, true);
            if (inst.anim != null) inst.anim.enabled = animate;
            inst.SetActive(true);
            return inst;
        }

        public void Release(ModelInst inst)
        {
            if (inst == null || inst.go == null) return;
            inst.ResetLook();
            if (inst.tower != null) inst.tower.ClearTarget();
            inst.tr.SetParent(parked, false);
            string pk = inst.key ?? inst.id ?? "";
            if (!pool.TryGetValue(pk, out var st)) pool[pk] = st = new Stack<ModelInst>();
            st.Push(inst);
        }

        ModelInst Create(string id, string pkey, int fallbackColor, PrimitiveType shape)
        {
            var cat = Catalog;
            GameObject prefab = cat != null && id != null ? cat.Prefab(pkey) : null;
            GameObject go;
            bool fb = prefab == null;
            if (!fb)
            {
                go = Object.Instantiate(prefab, root);
                go.name = id;
            }
            else
            {
                go = GameObject.CreatePrimitive(shape);
                go.name = "Ersatz_" + id;
                go.transform.SetParent(root, false);
                var col = go.GetComponent<Collider>();
                if (col != null) Object.Destroy(col);
                if (fallbackBase == null)
                {
                    var ga = GameAssets.Instance;
                    fallbackBase = ga != null && ga.lit != null ? ga.lit : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                }
                var m = new Material(fallbackBase) { color = Col.ToColor(fallbackColor) };
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Col.ToColor(fallbackColor));
                go.GetComponent<Renderer>().sharedMaterial = m;
            }
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            var inst = new ModelInst
            {
                id = id,
                key = pkey,
                go = go,
                tr = go.transform,
                tower = go.GetComponent<TowerView>(),
                unit = go.GetComponent<UnitView>(),
                prop = go.GetComponent<WorldPropView>(),
                anim = go.GetComponent<Animator>(),
                fallback = fb,
            };
            inst.rs = go.GetComponentsInChildren<Renderer>(true);
            int n = inst.rs.Length;
            inst.orig = new Material[n][];
            inst.ghostMats = new Material[n][];
            inst.colorProp = new int[n][];
            inst.origColor = new Color[n][];
            inst.emitProp = new int[n][];
            for (int r = 0; r < n; r++)
            {
                var rend = inst.rs[r];
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                rend.receiveShadows = true;
                var mats = rend.sharedMaterials;
                inst.orig[r] = mats;
                inst.ghostMats[r] = new Material[mats.Length];
                inst.colorProp[r] = new int[mats.Length];
                inst.origColor[r] = new Color[mats.Length];
                inst.emitProp[r] = new int[mats.Length];
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    inst.colorProp[r][i] = -1;
                    inst.emitProp[r][i] = -1;
                    inst.origColor[r][i] = Color.white;
                    if (m == null) continue;
                    for (int p = 0; p < ModelInst.COLOR_PROPS.Length; p++)
                        if (m.HasProperty(ModelInst.COLOR_PROPS[p])) { inst.colorProp[r][i] = p; inst.origColor[r][i] = m.GetColor(ModelInst.COLOR_PROPS[p]); break; }
                    for (int p = 0; p < ModelInst.EMIT_PROPS.Length; p++)
                        if (m.HasProperty(ModelInst.EMIT_PROPS[p])) { inst.emitProp[r][i] = p; break; }
                }
            }
            // Maße einmal je Kennung messen (Prefab in Ruhelage, Maßstab 1)
            string key = pkey ?? id ?? "";
            if (!measured.TryGetValue(key, out var hm))
            {
                go.transform.position = Vector3.zero;
                go.transform.rotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                bool any = false;
                var b = new Bounds();
                foreach (var rend in inst.rs)
                {
                    if (rend is ParticleSystemRenderer) continue;
                    if (!any) { b = rend.bounds; any = true; }
                    else b.Encapsulate(rend.bounds);
                }
                float h = any ? b.size.y : 1f;
                float minY = any ? b.min.y : 0f;
                if (h < 0.05f) h = 0.05f;
                hm = new Vector4(h, minY, any ? b.center.x : 0, any ? b.center.z : 0);
                measured[key] = hm;
                measuredFoot[key] = any ? Mathf.Max(0.05f, Mathf.Max(b.size.x, b.size.z)) : 1f;
            }
            inst.nativeH = hm.x;
            inst.nativeMinY = hm.y;
            inst.nativeCX = hm.z;
            inst.nativeCZ = hm.w;
            inst.nativeFoot = measuredFoot.TryGetValue(key, out var ft) ? ft : 1f;
            // Blickrichtung kommt vom Spiel (Laufrichtung), nicht aus dem Positionsunterschied
            if (inst.unit != null) inst.unit.SetAutoFacing(false);
            go.SetActive(false);
            return inst;
        }
    }

    public static class WorldModels
    {
        /// <summary>2.9: Modellmaßstab relativ zum Feld (WorldView.UNIT).</summary>
        public const float UNIT = 0.8f;

        /// <summary>Höhe für Netzwerkverbindungen, Symbole und Effekte je Turm.</summary>
        public static readonly Dictionary<string, float> TOWER_TOP = new Dictionary<string, float>
        {
            ["ranger"] = 1.3f, ["ember"] = 1.2f, ["frost"] = 1.5f, ["storm"] = 1.4f, ["spore"] = 1.05f, ["ballista"] = 0.8f, ["rune"] = 1.7f,
            ["resonator"] = 1.0f, ["goldmine"] = 0.85f, ["pylon"] = 1.5f, ["cannon"] = 0.95f, ["riftbinder"] = 1.25f, ["barricade"] = 0.6f, ["lanebarricade"] = 0.5f,
            ["meteor"] = 1.6f, ["tesla"] = 1.3f, ["gravity"] = 1.0f, ["dragon"] = 1.0f, ["laser"] = 1.2f, ["totem"] = 1.6f,
            ["plasma"] = 1.6f, ["glacier"] = 1.5f, ["voidlance"] = 1.4f, ["starfall"] = 2.1f,
            ["thornwood"] = 1.5f, ["thunderspire"] = 2.2f, ["riftgate"] = 1.5f, ["phoenix"] = 1.4f, ["cathedral"] = 1.6f, ["stormeye"] = 1.7f, ["minelayer"] = 1.1f, ["sporemine"] = 1.2f, ["skyguard"] = 1.0f, ["skywatch"] = 1.5f,
            ["seal"] = 1.5f, ["acid"] = 1.0f, ["quake"] = 1.3f, ["gale"] = 1.5f, ["eyrie"] = 1.8f, ["barracks"] = 1.1f, ["bank"] = 1.0f, ["ward"] = 1.0f,
            ["silencehall"] = 1.8f, ["banecircle"] = 1.3f, ["acidmortar"] = 1.2f, ["plaguepot"] = 1.2f, ["quakemaw"] = 1.4f, ["cyclone"] = 1.8f, ["stormbattery"] = 1.5f, ["falcon"] = 2.0f, ["sunlance"] = 2.1f, ["order"] = 1.5f, ["treasury"] = 0.95f, ["deepdrill"] = 1.2f, ["shieldnet"] = 1.6f,
        };

        public static float towerTop(string def) => def != null && TOWER_TOP.TryGetValue(def, out var v) ? v : 1f;

        public static readonly float[][] WEAKPOINT_POS = { new[] { 0.55f, 1.4f, 0.4f }, new[] { 0.55f, 1.4f, -0.4f }, new[] { -0.2f, 2.3f, 0f } };

        public static readonly Dictionary<string, float> BOSS_HEIGHT = new Dictionary<string, float>
        {
            ["riftColossus"] = 2.6f, ["ashQueen"] = 2.4f, ["nullWarden"] = 2.4f, ["maw"] = 1.9f, ["twinSol"] = 2.2f, ["twinLuna"] = 2.2f, ["worldbreaker"] = 2.9f,
        };

        public static float bossHeight(string boss) => boss != null && BOSS_HEIGHT.TryGetValue(boss, out var v) ? v : 2.2f;

        /// <summary>Lane-Held (HeroId) → Modellkennung im Katalog (die Heldenmodelle tragen die Namen der Champions).</summary>
        static readonly Dictionary<string, string> HERO_MODEL = new Dictionary<string, string>
        {
            ["blade"] = "kaela", ["hunter"] = "varo", ["riftwalker"] = "nyx", ["bard"] = "liora", ["smith"] = "thorgrim",
            ["weather"] = "sena", ["mystic"] = "oran", ["prospector"] = "mira", ["silence"] = "ommar",
        };

        public static string heroModel(string heroId)
        {
            if (heroId == null) return null;
            if (ModelPool.Has(heroId)) return heroId;
            return HERO_MODEL.TryGetValue(heroId, out var m) ? m : heroId;
        }

        /// <summary>Champion (Helden-Endlos) → Modell: eigenes Modell (lyra, brann …), sonst das Modell des Lane-Helden.</summary>
        public static string champModel(string champId)
        {
            if (champId == null) return null;
            if (ModelPool.Has(champId)) return champId;
            return Champions.CHAMPIONS.TryGetValue(champId, out var d) ? heroModel(d.model) : champId;
        }

        /// <summary>Dekorart (0 Baum … 9 Leuchtpilz) → Weltobjekt-Modell.</summary>
        public static string propFor(int kind, int variant)
        {
            switch (kind)
            {
                case 0: return variant % 7 == 3 ? "tree_dead" : "tree";
                case 1: return "rock";
                case 2: return "crystals";
                case 3: return "lavapool";
                case 4: return "iceblock";
                case 5: return "ruin";
                case 6: return "mushroom";
                case 7: return "cloudbank";
                case 8: return "willow";
                case 9: return "glowshroom";
                default: return "rock";
            }
        }

        /// <summary>Höhe der Dekorart im Webspiel (Maßstab 1).</summary>
        public static float propHeight(int kind)
        {
            switch (kind)
            {
                case 0: return 1.25f;
                case 1: return 0.7f;
                case 2: return 1.05f;
                case 3: return 0.36f;
                case 4: return 0.9f;
                case 5: return 1.1f;
                case 6: return 0.6f;
                case 7: return 0.6f;
                case 8: return 1.4f;
                case 9: return 0.9f;
                default: return 0.8f;
            }
        }

        // ------------------------------------------------------------ Zusatzteile (models.ts levelParts / specParts)

        const int WOOD_D = 0x56402a, GOLD = 0xf0c040, T = -1, A = -2;
        static readonly Dictionary<long, Part[]> levelCache = new Dictionary<long, Part[]>();
        static readonly Dictionary<long, Part[]> specCache = new Dictionary<long, Part[]>();

        /// <summary>Zusatzteile je Ausbaustufe (kumulativ): Banner, Goldringe, leuchtende Krone.</summary>
        public static Part[] levelParts(int level, float top)
        {
            bool sf = Themes.SciFi;
            long key = level * 100000L + Mathf.RoundToInt(top * 1000) + (sf ? 1000000000L : 0);
            if (levelCache.TryGetValue(key, out var c)) return c;
            var o = new List<Part>();
            if (sf)
            {
                // Sci-Fi: Antennen mit Positionslichtern, ab Stufe 4 vier Energiepfeiler
                if (level >= 2)
                {
                    o.Add(new Part("cyl8", 0.42f, 0.18f, 0.42f, 0.04f, 0.8f, 0.04f, 0x5a6470));
                    o.Add(new Part("sphereLo", 0.42f, 0.98f, 0.42f, 0.07f, 0.07f, 0.07f, 0xff4050, e: 2f, glow: 0xff4050));
                }
                if (level >= 3)
                {
                    o.Add(new Part("cyl8", -0.42f, 0.18f, -0.42f, 0.04f, 0.8f, 0.04f, 0x5a6470));
                    o.Add(new Part("sphereLo", -0.42f, 0.98f, -0.42f, 0.07f, 0.07f, 0.07f, 0x40e0ff, e: 2f, glow: 0x40e0ff));
                }
                if (level >= 4)
                    for (int k = 0; k < 4; k++)
                    {
                        float a = k / 4f * Mathf.PI * 2 + Mathf.PI / 4;
                        o.Add(new Part("box", Mathf.Cos(a) * 0.48f, 0.2f, Mathf.Sin(a) * 0.48f, 0.08f, 0.3f, 0.08f, 0x3a4048, yaw: a));
                        o.Add(new Part("box", Mathf.Cos(a) * 0.48f, 0.36f, Mathf.Sin(a) * 0.48f, 0.05f, 0.06f, 0.05f, 0x60e0ff, e: 1.5f, yaw: a));
                    }
                var sa = o.ToArray();
                levelCache[key] = sa;
                return sa;
            }
            if (level >= 2)
            {
                o.Add(new Part("cyl8", 0.42f, 0.18f, 0.42f, 0.05f, 0.75f, 0.05f, WOOD_D));
                o.Add(new Part("rbox", 0.42f, 0.78f, 0.5f, 0.03f, 0.2f, 0.18f, T, wing: 0.15f));
            }
            if (level >= 3)
            {
                o.Add(new Part("cyl8", -0.42f, 0.18f, -0.42f, 0.05f, 0.75f, 0.05f, WOOD_D));
                o.Add(new Part("rbox", -0.42f, 0.78f, -0.34f, 0.03f, 0.2f, 0.18f, T, wing: 0.15f));
            }
            if (level >= 4)
            {
                for (int k = 0; k < 4; k++)
                {
                    float a = k / 4f * Mathf.PI * 2 + Mathf.PI / 4;
                    o.Add(new Part("cone4", Mathf.Cos(a) * 0.48f, 0.2f, Mathf.Sin(a) * 0.48f, 0.12f, 0.34f, 0.12f, GOLD, e: 0.5f));
                }
            }
            var arr = o.ToArray();
            levelCache[key] = arr;
            return arr;
        }

        /// <summary>Spezialisierungsmerkmale: A = glühende Krone (orange), B = kreisende Runen (cyan).</summary>
        public static Part[] specParts(int spec, float top)
        {
            long key = spec * 100000L + Mathf.RoundToInt(top * 1000);
            if (specCache.TryGetValue(key, out var c)) return c;
            Part[] arr;
            if (spec == 1)
            {
                arr = new[]
                {
                    new Part("torus", 0, top + 0.25f, 0, 0.5f, 0.3f, 0.5f, 0xffa040, e: 1.2f, spin: 1.2f, glow: 0xffa040),
                    new Part("cone4", 0, top + 0.3f, 0, 0.16f, 0.36f, 0.16f, 0xffc060, e: 1.2f),
                };
            }
            else if (spec == 2)
            {
                arr = new Part[3];
                for (int k = 0; k < 3; k++)
                {
                    float a = k / 3f * Mathf.PI * 2;
                    arr[k] = new Part("gem", Mathf.Cos(a) * 0.55f, top * 0.7f, Mathf.Sin(a) * 0.55f, 0.14f, 0.2f, 0.14f, 0x60e0ff, e: 1.5f, spin: 1.8f, bob: 0.08f, glow: 0x60e0ff);
                }
            }
            else arr = new Part[0];
            specCache[key] = arr;
            return arr;
        }

        /// <summary>Kleiner Drache, der den Drachenhorst umkreist (Blickrichtung +X).</summary>
        public static readonly Part[] DRAGON =
        {
            new Part("sphere", 0, 0, 0, 0.9f, 0.5f, 0.5f, -1),
            new Part("sphere", 0.5f, 0.15f, 0, 0.38f, 0.32f, 0.32f, -1),
            new Part("cone8", 0.62f, 0.2f, 0, 0.14f, 0.3f, 0.14f, 0xf0e0c0, pitch: -Mathf.PI / 2),
            new Part("sphereLo", 0.62f, 0.22f, 0.1f, 0.07f, 0.07f, 0.07f, -2, e: 2),
            new Part("sphereLo", 0.62f, 0.22f, -0.1f, 0.07f, 0.07f, 0.07f, -2, e: 2),
            new Part("leaf", 0, 0.1f, 0.15f, 1.1f, 1, 1.1f, -1, wing: 1),
            new Part("leaf", 0, 0.1f, -0.15f, 1.1f, 1, -1.1f, -1, wing: -1),
            new Part("cone8", -0.4f, 0, 0, 0.2f, 0.7f, 0.2f, -1, pitch: Mathf.PI / 2),
        };

        /// <summary>4.1 Sci-Fi: Kampfdrohne mit vier Rotoren statt des Drachen.</summary>
        public static readonly Part[] DRONE =
        {
            new Part("sphere", 0, 0, 0, 0.62f, 0.26f, 0.5f, 0xd8dee6),
            new Part("sphereLo", 0.24f, -0.02f, 0, 0.2f, 0.13f, 0.2f, -2, e: 2.5f, glow: 0x60e0ff),
            new Part("box", 0, 0.04f, 0, 1.05f, 0.05f, 0.07f, 0x3a4048, yaw: Mathf.PI / 4),
            new Part("box", 0, 0.04f, 0, 1.05f, 0.05f, 0.07f, 0x3a4048, yaw: -Mathf.PI / 4),
            new Part("cyl8", 0.37f, 0.06f, 0.37f, 0.12f, 0.08f, 0.12f, 0x2a3038),
            new Part("cyl8", -0.37f, 0.06f, 0.37f, 0.12f, 0.08f, 0.12f, 0x2a3038),
            new Part("cyl8", 0.37f, 0.06f, -0.37f, 0.12f, 0.08f, 0.12f, 0x2a3038),
            new Part("cyl8", -0.37f, 0.06f, -0.37f, 0.12f, 0.08f, 0.12f, 0x2a3038),
            new Part("box", 0.37f, 0.11f, 0.37f, 0.44f, 0.012f, 0.05f, 0xa0b0c0, spin: 40),
            new Part("box", -0.37f, 0.11f, 0.37f, 0.44f, 0.012f, 0.05f, 0xa0b0c0, spin: -40),
            new Part("box", 0.37f, 0.11f, -0.37f, 0.44f, 0.012f, 0.05f, 0xa0b0c0, spin: -40),
            new Part("box", -0.37f, 0.11f, -0.37f, 0.44f, 0.012f, 0.05f, 0xa0b0c0, spin: 40),
            new Part("cyl8", 0.1f, -0.16f, 0, 0.08f, 0.14f, 0.08f, 0x2a3038, pitch: Mathf.PI / 2),
            new Part("sphereLo", -0.28f, 0, 0, 0.1f, 0.08f, 0.16f, -1, e: 1.5f),
        };
    }
}
