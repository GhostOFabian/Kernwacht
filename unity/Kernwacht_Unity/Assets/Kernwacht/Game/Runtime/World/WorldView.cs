// Übertragung von render/WorldView.ts (+ Rolle von render/Renderer.ts): übersetzt den Simulationszustand in die
// 3D-Welt (Gelände, Modelle, Effekte, Overlay). Liest den Zustand ausschließlich.
// Spielkoordinaten: x = Spalte, z = Zeile, y = oben; Unity-Position über IsoCamera.W(x, y, z).
using System;
using System.Collections.Generic;
using Kernwacht.Models;
using Kernwacht.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kernwacht.Game
{
    /// <summary>Darstellungszustand der Oberfläche (TS ViewUiState).</summary>
    public class ViewUiState
    {
        public string buildDef;              // TowerId | null
        /// <summary>2.9.5: legendärer Turm wird gerade versetzt (Vorschau am Zielfeld).</summary>
        public string moveDef;               // TowerId | null
        public GridPos hoverCell;            // null möglich
        public BuildCheck preview;           // null möglich
        public double selectedTower;
        public bool showRanges;
        public bool showGrid;
        public bool reducedMotion;
        public double particleCap;
        public double? selectedHero;
        public string heroPlacing;           // HeroId | null
        public double? abilityRadius;
        public List<double> highlight;
        /// <summary>Helden-Endlos: Wirkreichweite um den Helden während der Zielwahl.</summary>
        public double? champCastRange;
        /// <summary>Beim Platzieren: Umkreis, in dem Fusionspartner stehen müssen (0 = aus).</summary>
        public double? fusionRing;
        /// <summary>Angeklickter Gegner (UID) – Auswahlring und Infopanel.</summary>
        public double? selectedEnemy;
        /// <summary>Helden-Endlos: Held ausgewählt (zeigt Aura und Laufziel).</summary>
        public bool? champSelected;
        public bool? cinematic;
    }

    public class ViewSkin
    {
        public double tint = 0xffffff;
        public double tintAmount;
        public double stone = 0x8c8678;
    }

    /// <summary>Schwebende Zahl/Text über der Welt (Spielkoordinaten); die Sitzung zeigt sie im worldLayer an.</summary>
    public class Floater
    {
        public string text;
        public double x;
        public double y;
        public double z;
        public string color;   // CSS-Farbe '#rrggbb'
        public bool? big;
    }

    /// <summary>3.13: Stimmung je Biom – Himmelsverlauf, Horizontglühen, Wolkenschatten, Sterne, Sonnendunst.</summary>
    public class MoodDefinition
    {
        public double top, glow, clouds, stars, sun;
        public MoodDefinition(double top, double glow, double clouds, double stars, double sun) { this.top = top; this.glow = glow; this.clouds = clouds; this.stars = stars; this.sun = sun; }
    }

    public class WorldView
    {
        /// <summary>2.9: Modellmaßstab relativ zum Feld – Türme, Gegner und Helden etwas kleiner.</summary>
        public const float UNIT = WorldModels.UNIT;

        public static double cellTop(MapDefinition map, double x, double y) => Terrain.cellTop(map, x, y);

        public static readonly Dictionary<string, MoodDefinition> MOOD = new Dictionary<string, MoodDefinition>
        {
            ["forest"] = new MoodDefinition(0x3c5a5a, 0xffd08a, 0.3, 0, 0.11),
            ["volcano"] = new MoodDefinition(0x5a2418, 0xff7a30, 0.22, 0.25, 0.12),
            ["frozen"] = new MoodDefinition(0x4a6a8a, 0xbfe8ff, 0.26, 0.15, 0.1),
            ["crystal"] = new MoodDefinition(0x5a3a6a, 0xffb0e0, 0.22, 0.35, 0.11),
            ["sky"] = new MoodDefinition(0x6a8ab8, 0xffd8b0, 0.32, 0, 0.13),
            ["swamp"] = new MoodDefinition(0x2e4436, 0xc8e890, 0.3, 0.1, 0.07),
            ["cavern"] = new MoodDefinition(0x261e40, 0x60f0d0, 0, 0.7, 0.05),
        };

        static readonly Dictionary<string, object[]> REACTION_LABEL = new Dictionary<string, object[]>
        {
            ["steam"] = new object[] { "Dampfstoß", 0xe0f0ff }, ["thermalShock"] = new object[] { "Thermoschock", 0x90d8ff }, ["overload"] = new object[] { "Überladung", 0xffe860 },
            ["toxicBlaze"] = new object[] { "Giftbrand", 0xb0ff40 }, ["shatter"] = new object[] { "Resonanzbruch", 0x60e8d4 },
        };

        static readonly string[] ZONE_WINDUP_KINDS = { "fireZone", "nullPulse", "quake", "riftJump", "portalIn", "portalOut", "stomp", "siegeSlam", "devour" };
        static readonly string[] ZONE_WINDUP_ABIL = { "fireZones", "nullPulse", "quake", "riftJump", "riftPortals", "riftPortals", "stomp", "siegeSlam", "devour" };

        // Tageszeit-Schlüsselbilder (Lichtfarbe, Intensität, Höhenwinkel)
        static readonly float[][] TOD = { new[] { 0xffe2b8, 0.9f, 0.7f }, new[] { 0xffffff, 1.0f, 0.95f }, new[] { 0xffb070, 0.85f, 0.55f }, new[] { 0x9aa8ff, 0.62f, 0.75f } };

        const bool SPRITE_ADD = true;

        // ------------------------------------------------------------ öffentliche Felder
        public IsoCamera cam;
        public readonly Fx fx = new Fx();
        public readonly WorldDraw r;
        public int detail = 2;
        public ViewSkin skin = new ViewSkin();
        public Action<Floater> onFloater;
        public Action<string> onBossAppear;
        /// <summary>Hauptlicht (Richtungslicht der Szene); wird beim ersten setMap gesucht oder angelegt.</summary>
        public Light sun;
        /// <summary>Wurzelobjekt aller Weltobjekte (Gelände, Modelle).</summary>
        public readonly Transform root;

        // ------------------------------------------------------------ interne Zustände
        class Smooth
        {
            public float x, y, h, head, speed;
            public string def, boss;
            public bool elite;
            public float seen;
            public int stamp;
            public ModelInst inst;
        }

        class Corpse
        {
            public ModelInst inst;
            public float x, y, z, height, t, hold, max;
            public int color;
            public bool boss;
            public float scale;
            public float fall; // Flieger stürzen zu Boden
        }

        class Ambient { public float x, y, z, vx, vy, vz, life, max, seed; }

        class HeroSm
        {
            public float x, y, h, speed, born;
            public int stamp;
            public ModelInst inst;
            public string id;
        }

        class ChampSm
        {
            public float x, y, h, speed;
            public ModelInst inst;
            public string model;
        }

        class TowerVis
        {
            public ModelInst inst;
            public string def;
            public int stamp;
        }

        class SpawnPortal
        {
            public float x, y, yaw;
            public ModelInst inst;
        }

        class Lighting
        {
            public Vector3 light = new Vector3(-0.45f, 0.85f, -0.3f);
            public Vector3 lightColor;
            public Vector3 sky;
            public Vector3 ground;
            public int fog;
            public float fogStart, fogEnd;
        }

        readonly Dictionary<double, Smooth> smooth = new Dictionary<double, Smooth>();
        readonly Stack<Smooth> smoothPool = new Stack<Smooth>();
        ChampSm champSmooth;
        readonly Dictionary<double, HeroSm> heroSmooth = new Dictionary<double, HeroSm>();
        readonly List<Corpse> corpses = new List<Corpse>();
        readonly Dictionary<double, float> towerBorn = new Dictionary<double, float>();
        readonly Dictionary<double, TowerVis> towerVis = new Dictionary<double, TowerVis>();
        List<GlowPoint> glows = new List<GlowPoint>();
        readonly Ambient[] ambient = new Ambient[900];
        int ambientCount;
        readonly List<SpawnPortal> spawnYaw = new List<SpawnPortal>();
        readonly HashSet<double> bossSeen = new HashSet<double>();
        readonly List<double> removeBuf = new List<double>();
        readonly List<EnemyInstance> twins = new List<EnemyInstance>();
        float time;
        float shake;
        float flash;
        MapDefinition mapRef;
        Lighting lighting;
        Vector3 lightCol = Vector3.one;
        Vector3 lightDir = new Vector3(-0.45f, 0.85f, -0.3f);
        bool firstFrame = true;
        double mapRev;
        int stamp;
        string ambientKind;

        readonly ModelPool pool;
        readonly TerrainView terrain;
        readonly Material ghostMat;
        ModelInst coreInst, shopInst, previewInst, heroPreviewInst;
        string previewId, heroPreviewId;

        // ------------------------------------------------------------ Aufbau

        /// <summary>Welt mit eigener IsoCamera für die gegebene Kamera (null = Camera.main bzw. neue Kamera).</summary>
        public WorldView(Camera camera = null)
            : this(new IsoCamera(camera != null ? camera : FindCamera()))
        {
        }

        public WorldView(IsoCamera isoCam)
        {
            cam = isoCam;
            root = new GameObject("Kernwacht-Welt").transform;
            r = new WorldDraw();
            pool = new ModelPool(root);
            terrain = new TerrainView(root, r, pool);
            ghostMat = r.NewGhostMaterial();
            for (int i = 0; i < ambient.Length; i++) ambient[i] = new Ambient();
            var c = cam.cam;
            if (c != null)
            {
                c.clearFlags = CameraClearFlags.SolidColor;
                if (c.farClipPlane < 200) c.farClipPlane = 200;
            }
        }

        static Camera FindCamera()
        {
            var c = Camera.main;
            if (c != null) return c;
            var go = new GameObject("Kamera");
            go.tag = "MainCamera";
            return go.AddComponent<Camera>();
        }

        static float Rnd() => UnityEngine.Random.value;
        static int Ci(double v) => (int)v;
        static Vector3 Rgb(int hex) => new Vector3(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f);
        static int Hex(Vector3 c) => (Mathf.Clamp(Mathf.RoundToInt(c.x * 255), 0, 255) << 16) | (Mathf.Clamp(Mathf.RoundToInt(c.y * 255), 0, 255) << 8) | Mathf.Clamp(Mathf.RoundToInt(c.z * 255), 0, 255);

        float gy(MapDefinition map, double x, double y) => Terrain.topAt(map, x, y);

        /// <summary>Gelände neu aufbauen (nach Abbau eines Hindernisses), ohne Kamera und Effekte zurückzusetzen.</summary>
        public void rebuildTerrain(MapDefinition map)
        {
            mapRev = map.rev ?? 0;
            glows = terrain.build(map, detail);
        }

        public void setMap(MapDefinition map, double? detail = null)
        {
            mapRev = map.rev ?? 0;
            mapRef = map;
            if (detail.HasValue) this.detail = (int)detail.Value;
            cam.Resize(Screen.width, Screen.height);
            cam.SetMap((float)map.width, (float)map.height);
            ReleaseAllEntities();
            towerBorn.Clear();
            bossSeen.Clear();
            fx.clear();
            ambientCount = 0;
            firstFrame = true;
            glows = terrain.build(map, this.detail);

            // Portalausrichtung: quer zum ersten Wegstück
            foreach (var sp in spawnYaw) pool.Release(sp.inst);
            spawnYaw.Clear();
            int w = (int)map.width, h = (int)map.height;
            foreach (var sp in WorldGenerator.spawnsOf(map))
            {
                float yaw = 0;
                int[][] dirs = { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } };
                foreach (var dd in dirs)
                {
                    int nx = (int)sp.x + dd[0], ny = (int)sp.y + dd[1];
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    double c = map.cells[ny * w + nx];
                    if (c == Cell.Path || c == Cell.Goal || c == Cell.Open || c == Cell.NoBuild)
                    {
                        yaw = Mathf.Atan2(-dd[1], dd[0]);
                        if (c == Cell.Path) break;
                    }
                }
                var portal = new SpawnPortal { x = (float)sp.x + 0.5f, y = (float)sp.y + 0.5f, yaw = yaw };
                if (ModelPool.Has("portal"))
                {
                    portal.inst = pool.Acquire("portal", at: IsoCamera.W(portal.x, 0, portal.y));
                    portal.inst.SetHeading(-yaw);
                    portal.inst.Place(portal.x, 0.0f, portal.y, 1.55f);
                }
                spawnYaw.Add(portal);
            }
            if (coreInst != null) { pool.Release(coreInst); coreInst = null; }
            if (ModelPool.Has("core"))
            {
                float gx = (float)map.goal.x + 0.5f, gz = (float)map.goal.y + 0.5f;
                coreInst = pool.Acquire("core", at: IsoCamera.W(gx, 0.14f, gz));
                coreInst.Place(gx, 0.14f, gz, 1.5f);
            }
            if (shopInst != null) { pool.Release(shopInst); shopInst = null; }

            var biome = Biomes.BIOMES.TryGetValue(map.biome ?? "forest", out var bd) ? bd : Biomes.BIOMES["forest"];
            var pal = biome.palette;
            mood = Terrain.mood(map);
            PostFx.SetMood(mood.tint, mood.tintAmt);
            int skyHex = Col.Mix(Col.Mix((int)pal.sky, (int)pal.fog, 0.5), mood.tint, mood.tintAmt);
            float size = Mathf.Max((float)map.width, (float)map.height);
            lighting = new Lighting
            {
                light = new Vector3(-0.45f, 0.85f, -0.3f),
                lightColor = Rgb((int)pal.light) * 0.85f,
                sky = Rgb(Col.Mix((int)pal.sky, 0xffffff, 0.55)) * 0.55f,
                ground = Rgb((int)pal.ground) * 0.3f,
                fog = skyHex,
                fogStart = size * 0.62f,
                fogEnd = size * 0.62f + 14,
            };
            lightCol = lighting.lightColor;
            lightDir = lighting.light;
            // Erstes Bild: Licht sofort auf den Zielstand setzen. Vorher schwenkte die Sonne in den ersten Sekunden
            // vom Standardwinkel zur Tageszeit – alle Schatten wanderten mit und das Gras flackerte stark.
            lightSnap = true;
            SetupScene();
        }

        public void setSkin(ViewSkin skin)
        {
            this.skin = skin ?? new ViewSkin();
        }

        void ReleaseAllEntities()
        {
            foreach (var s in smooth.Values) { if (s.inst != null) pool.Release(s.inst); s.inst = null; smoothPool.Push(s); }
            smooth.Clear();
            foreach (var hs in heroSmooth.Values) if (hs.inst != null) pool.Release(hs.inst);
            heroSmooth.Clear();
            if (champSmooth != null && champSmooth.inst != null) pool.Release(champSmooth.inst);
            champSmooth = null;
            foreach (var c in corpses) if (c.inst != null) pool.Release(c.inst);
            corpses.Clear();
            foreach (var tv in towerVis.Values) if (tv.inst != null) pool.Release(tv.inst);
            towerVis.Clear();
            if (previewInst != null) { pool.Release(previewInst); previewInst = null; previewId = null; }
            if (heroPreviewInst != null) { pool.Release(heroPreviewInst); heroPreviewInst = null; heroPreviewId = null; }
        }

        static bool shadowDistanceSet;
        /// <summary>Stimmung der aktuellen Karte (aus Kartenaufbau/Seed).</summary>
        Terrain.MapMood mood;
        bool lightSnap;

        void SetupScene()
        {
            if (sun == null)
            {
                sun = RenderSettings.sun;
                if (sun == null)
                    foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                        if (l.type == LightType.Directional) { sun = l; break; }
                if (sun == null)
                {
                    var go = new GameObject("Sonne");
                    sun = go.AddComponent<Light>();
                    sun.type = LightType.Directional;
                }
                RenderSettings.sun = sun;
            }
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;
            sun.shadowBias = 0.03f;
            sun.shadowNormalBias = 0.3f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            // PORT: URP-Nebel ist mit orthografischer Kamera unzuverlässig (Tiefe aus dem Clipraum) und fällt im WebGL-Build
            // weg, wenn die Szene keinen Nebel nutzt. Den Dunst am Horizont übernimmt der Farbverlauf des Meeres.
            RenderSettings.fog = cam.cam != null && !cam.cam.orthographic;
            RenderSettings.fogMode = FogMode.Linear;
            if (!shadowDistanceSet)
            {
                shadowDistanceSet = true;
                // PORT: URP-Schattenreichweite muss die Kameradistanz (60) plus Karte abdecken – per Reflexion gesetzt,
                // falls das URP-Asset es erlaubt; sonst im Pipeline-Asset „Shadow Distance“ ≥ 110 einstellen.
                try
                {
                    var rp = GraphicsSettings.currentRenderPipeline;
                    var p = rp != null ? rp.GetType().GetProperty("shadowDistance") : null;
                    if (p != null && p.CanWrite)
                    {
                        float cur = Convert.ToSingle(p.GetValue(rp));
                        if (cur < 110f) p.SetValue(rp, 110f);
                    }
                }
                catch (Exception) { }
            }
            ApplyLighting();
        }

        // ------------------------------------------------------------ Ereignisse → Effekte

        public void handleEvents(List<GameEvent> events, GameSimulation sim, ViewUiState ui)
        {
            fx.intensity = ui.reducedMotion ? 0.5f : 1f;
            var map = sim.s.map;
            foreach (var e in events)
            {
                float ex = (float)e.x, ey = (float)e.y, etx = (float)e.tx, ety = (float)e.ty;
                switch (e.k)
                {
                    case "shot":
                    {
                        sim.c.towerByUid.TryGetValue(e.uid, out var t);
                        float top = t != null ? gy(map, e.x, e.y) + WorldModels.towerTop(t.def) * 0.85f * UNIT : 1;
                        float ty = gy(map, e.tx, e.ty) + 0.45f;
                        if (t != null && towerVis.TryGetValue(t.uid, out var tv) && tv.inst != null && tv.inst.tower != null) tv.inst.tower.Fire();
                        if (e.tower == "rune" || e.tower == "riftbinder")
                        {
                            int col = e.tower == "rune" ? 0xc070ff : 0xff60ff;
                            fx.beam(ex, top, ey, etx, ty, ety, col, 0.07f, 0.14f);
                            fx.glow(etx, ty, ety, 0.5f, col, 0.18f);
                        }
                        else if (e.tower == "minelayer" || e.tower == "sporemine")
                            fx.smoke(ex, top, ey, 0.35f, e.tower == "sporemine" ? 0x8ad040 : 0x6a6460, 0.35f, 0.6f);
                        else if (e.tower == "cannon")
                        {
                            fx.glow(ex, top, ey, 0.7f, 0xffc060, 0.12f);
                            fx.smoke(ex, top, ey, 0.5f, 0x6a6460, 0.4f, 0.8f);
                        }
                        else if (e.tower == "ember") fx.fire(ex, top, ey, 1.1f);
                        else if (e.tower == "meteor" || e.tower == "starfall")
                        {
                            fx.glow(ex, top + 0.4f, ey, 0.9f, 0xff8030, 0.3f);
                            for (int i = 0; i < 4; i++) fx.emit(ex, top + 0.3f, ey, 0.4f, 0xffc060, vy: 4 + Rnd() * 2, c1: 0xff4010, s0: 0.25f, s1: 0.05f, shape: SHAPE.spark, stretch: 0.2f);
                        }
                        else if (t != null && Towers.TOWERS[t.def].attack == "projectile")
                            fx.glow(ex, top, ey, 0.3f, Ci(Towers.TOWERS[t.def].color), 0.1f, 0.7f);
                        break;
                    }
                    case "hit":
                    {
                        int col = Palette.damageColor(e.type);
                        float y = gy(map, e.x, e.y) + 0.4f;
                        float splash = (float)e.splash;
                        if (splash > 0.3f)
                        {
                            if (e.type == "fire") fx.explosion(ex, gy(map, e.x, e.y), ey, splash * 0.8f, col, splash > 1.5f);
                            else
                            {
                                fx.ring(ex, y - 0.35f, ey, splash, col, 0.4f, 0.18f);
                                fx.glow(ex, y, ey, splash, col, 0.2f, 0.7f);
                                fx.sparks(ex, y, ey, col, 5);
                            }
                        }
                        else
                        {
                            switch (e.type)
                            {
                                case "fire": fx.fire(ex, y - 0.1f, ey, 0.8f); break;
                                case "frost": fx.sparks(ex, y, ey, 0xd8f4ff, 3, 0.7f, 0.1f); break;
                                case "poison": for (int i = 0; i < 2; i++) fx.emit(ex, y, ey, 0.4f, 0xb0f060, vx: Rnd() - 0.5f, vy: 1, vz: Rnd() - 0.5f, grav: 4, s0: 0.12f, s1: 0.04f); break;
                                case "arcane": fx.glow(ex, y, ey, 0.45f, col, 0.16f); break;
                                case "sonic": fx.ring(ex, y - 0.3f, ey, 0.5f, col, 0.25f); break;
                                default: fx.sparks(ex, y, ey, col, 2, 0.8f, 0.09f); break;
                            }
                        }
                        break;
                    }
                    case "chain":
                    {
                        var pts = e.pts;
                        if (pts == null) break;
                        for (int i = 0; i + 3 < pts.Count; i += 2)
                        {
                            float y0 = i == 0 ? gy(map, pts[0], pts[1]) + 1.25f : gy(map, pts[i], pts[i + 1]) + 0.45f;
                            float y1 = gy(map, pts[i + 2], pts[i + 3]) + 0.45f;
                            fx.bolt((float)pts[i], y0, (float)pts[i + 1], (float)pts[i + 2], y1, (float)pts[i + 3], 0xfff090, 0.05f, 0.2f, 1);
                            fx.glow((float)pts[i + 2], y1, (float)pts[i + 3], 0.45f, 0xfff090, 0.15f);
                        }
                        break;
                    }
                    case "arc":
                        fx.bolt(ex, 1.2f, ey, etx, 0.5f, ety, 0x9ae0ff, 0.05f, 0.16f, 1);
                        break;
                    case "pulse":
                    {
                        int col = e.tower == "frost" ? 0x9ae0ff : 0x60e8d4;
                        float y = gy(map, e.x, e.y);
                        float rr = (float)e.r;
                        fx.ring(ex, y, ey, rr, col, 0.5f, 0.1f);
                        if (e.tower == "frost")
                        {
                            for (int i = 0; i < 10 * fx.intensity; i++)
                            {
                                float a = Rnd() * Mathf.PI * 2;
                                fx.emit(ex + Mathf.Cos(a) * rr * 0.3f, y + 0.3f, ey + Mathf.Sin(a) * rr * 0.3f, 0.6f, 0xffffff, shape: SHAPE.star, vx: Mathf.Cos(a) * rr, vz: Mathf.Sin(a) * rr, vy: 0.3f, drag: 2, c1: 0x9ae0ff, s0: 0.14f, s1: 0.04f);
                            }
                        }
                        else fx.ring(ex, y, ey, rr * 0.7f, col, 0.6f, 0.06f);
                        break;
                    }
                    case "gravity":
                    {
                        float y = gy(map, e.x, e.y);
                        float rr = (float)e.r;
                        fx.emit(ex, y + 0.05f, ey, 0.5f, 0x8a6aff, shape: SHAPE.ring, flat: true, s0: rr, s1: 0.2f, seed: 0.15f);
                        for (int i = 0; i < 10 * fx.intensity; i++)
                        {
                            float a = Rnd() * Mathf.PI * 2;
                            float d = rr * (0.6f + Rnd() * 0.4f);
                            fx.emit(ex + Mathf.Cos(a) * d, y + 0.3f, ey + Mathf.Sin(a) * d, 0.5f, 0xb090ff, vx: -Mathf.Cos(a) * d * 1.8f, vz: -Mathf.Sin(a) * d * 1.8f, c1: 0x301060, s0: 0.2f, s1: 0.05f);
                        }
                        break;
                    }
                    case "breath":
                    {
                        const float t0 = 1.6f;
                        float dx = etx - ex, dz = ety - ey;
                        for (int i = 0; i < 14 * fx.intensity; i++)
                        {
                            float s = 1.6f + Rnd() * 1.2f;
                            fx.emit(ex, t0, ey, 0.5f, 0xfff0a0, vx: dx * s + (Rnd() - 0.5f), vy: -t0 * s * 0.8f, vz: dz * s + (Rnd() - 0.5f), c1: 0xc02000, s0: 0.18f, s1: 0.45f, drag: 1);
                        }
                        break;
                    }
                    case "meteor":
                        fx.explosion(ex, gy(map, e.x, e.y), ey, (float)e.r, 0xff6a20, e.big);
                        if (e.big)
                        {
                            Shake(ui, 0.25f);
                            flash = Mathf.Max(flash, 0.12f);
                        }
                        break;
                    case "burst":
                        fx.explosion(ex, gy(map, e.x, e.y), ey, (float)e.r, 0xff8030, e.r > 1.2);
                        Shake(ui, 0.1f);
                        break;
                    case "kill":
                        EmitFloater("+" + U.Fmt(e.gold), e.x, 0.9, e.y, "#ffd84a", e.isBoss || e.elite);
                        if (e.isBoss)
                        {
                            Shake(ui, 0.6f);
                            flash = Mathf.Max(flash, 0.5f);
                        }
                        break;
                    case "death":
                        addCorpse(sim, ex, ey, e.enemy, e.boss, Ci(e.color), (float)e.size);
                        break;
                    case "leak":
                        if (mapRef != null)
                        {
                            float gx = (float)mapRef.goal.x + 0.5f, gz = (float)mapRef.goal.y + 0.5f;
                            fx.glow(gx, 1, gz, 2.2f, 0xff3030, 0.4f);
                            fx.ring(gx, 0, gz, 2.2f, 0xff4040, 0.5f, 0.2f);
                            fx.sparks(gx, 1, gz, 0xff6060, 10, 1.4f);
                            EmitFloater("−" + U.Fmt(e.lives) + " ♥", gx, 1.6, gz, "#ff5a5a", true);
                            if (coreInst != null && coreInst.prop != null) coreInst.prop.Pulse();
                        }
                        Shake(ui, 0.2f);
                        break;
                    case "reaction":
                    {
                        string label = e.id;
                        int col = 0xffffff;
                        if (e.id != null && REACTION_LABEL.TryGetValue(e.id, out var rl)) { label = (string)rl[0]; col = (int)rl[1]; }
                        float y = gy(map, e.x, e.y);
                        fx.ring(ex, y, ey, 1.4f, col, 0.5f, 0.14f);
                        fx.glow(ex, y + 0.5f, ey, 1.0f, col, 0.25f);
                        if (e.id == "steam") for (int i = 0; i < 5; i++) fx.smoke(ex, y + 0.3f, ey, 0.7f, 0xe8f0ff, 0.45f, 1);
                        else fx.sparks(ex, y + 0.4f, ey, col, 6);
                        EmitFloater(label, e.x, 1.2, e.y, Col.Css(col), null);
                        break;
                    }
                    case "teleport":
                    {
                        for (int k = 0; k < 2; k++)
                        {
                            float x = k == 0 ? ex : (float)e.x2, z = k == 0 ? ey : (float)e.y2;
                            fx.ring(x, gy(map, x, z), z, 1.2f, 0xff60ff, 0.4f, 0.15f);
                            fx.glow(x, gy(map, x, z) + 0.5f, z, 1.0f, 0xff60ff, 0.3f);
                        }
                        foreach (var s in smooth.Values)
                        {
                            if (Mathf.Abs(s.x - ex) < 0.3f && Mathf.Abs(s.y - ey) < 0.3f)
                            {
                                s.x = (float)e.x2;
                                s.y = (float)e.y2;
                            }
                        }
                        break;
                    }
                    case "immune":
                        fx.ring(ex, gy(map, e.x, e.y) + 0.4f, ey, 0.7f, 0xffffff, 0.3f, 0.1f);
                        break;
                    case "build":
                    case "upgrade":
                    case "sell":
                    {
                        float y = gy(map, e.x, e.y);
                        for (int i = 0; i < 4; i++) fx.smoke(ex + (Rnd() - 0.5f) * 0.8f, y + 0.1f, ey + (Rnd() - 0.5f) * 0.8f, 0.5f, 0xb8a888, 0.4f, 0.8f);
                        if (e.k == "upgrade")
                        {
                            // Aufstieg: Goldwelle am Boden, drei aufsteigende Ringe, schmaler Lichtstrahl
                            fx.ring(ex, y, ey, 1.05f, 0xffd860, 0.5f, 0.07f, 0.35f);
                            fx.ring(ex, y, ey, 0.7f, 0xffffff, 0.35f, 0.05f, 0.5f);
                            for (int i = 0; i < 3; i++)
                                fx.emit(ex, y + 0.12f + i * 0.22f, ey, 0.55f + i * 0.12f, i == 1 ? 0xffffff : 0xffd860, shape: SHAPE.ring, flat: true,
                                    vy: 1.4f + i * 0.25f, s0: 0.5f - i * 0.06f, s1: 0.28f, a: 0.85f, seed: 0.06f);
                            fx.pillar(ex, y, ey, 2.0f, 0xffd860, 0.45f, 0.14f);
                            fx.sparks(ex, y + 0.8f, ey, 0xffd860, 10, 1.1f);
                        }
                        else fx.ring(ex, y, ey, 0.9f, 0xffffff, 0.35f, 0.12f);
                        if (e.k == "sell")
                        {
                            fx.sparks(ex, y + 0.4f, ey, 0xffd84a, 12, 1.2f);
                            fx.debrisBurst(ex, y, ey, 0x8a8070, 5);
                            EmitFloater("+" + U.Fmt(e.refund), e.x, 1, e.y, "#ffd84a", null);
                        }
                        if (e.k == "build") fx.debrisBurst(ex, y, ey, 0x9a9080, 3);
                        break;
                    }
                    case "fusion":
                    {
                        float y = gy(map, e.x, e.y);
                        fx.pillar(ex, y, ey, 5, 0xffe080, 1.1f, 0.5f);
                        fx.ring(ex, y, ey, 3, 0xffe080, 0.8f, 0.12f);
                        fx.ring(ex, y, ey, 2, 0xffffff, 0.6f, 0.08f);
                        for (int i = 0; i < 24 * fx.intensity; i++)
                            fx.emit(ex, y + 1, ey, 1, 0xffffff, shape: SHAPE.star, vx: (Rnd() - 0.5f) * 5, vy: Rnd() * 4, vz: (Rnd() - 0.5f) * 5, drag: 1.5f, c1: 0xffc040, s0: 0.25f, s1: 0.05f);
                        flash = Mathf.Max(flash, 0.35f);
                        Shake(ui, 0.2f);
                        double cell = Math.Floor(e.y) * map.width + Math.Floor(e.x);
                        towerBorn[sim.c.towerByCell.TryGetValue(cell, out var ft) ? ft.uid : -1] = time;
                        break;
                    }
                    case "towerActive":
                    {
                        float y = gy(map, e.x, e.y);
                        int col = Towers.TOWERS.TryGetValue(e.tower ?? "", out var td) ? Ci(td.color) : 0xffffff;
                        fx.pillar(ex, y, ey, 3, col, 0.6f, 0.35f);
                        fx.ring(ex, y, ey, e.active == "nova" ? 5 : 2, col, 0.6f, 0.12f);
                        if (e.active == "deepfreeze") fx.ring(ex, y, ey, 3.5f, 0xd8f4ff, 0.8f, 0.2f);
                        if (e.active == "goldrush") fx.sparks(ex, y + 1, ey, 0xffd84a, 20, 1.4f, 0.16f);
                        if (e.active == "nova")
                        {
                            flash = Mathf.Max(flash, 0.25f);
                            Shake(ui, 0.15f);
                        }
                        break;
                    }
                    case "plateBreak":
                    {
                        float y = gy(map, e.x, e.y) + 1.6f;
                        fx.glow(ex, y, ey, 1.6f, 0xff4040, 0.3f);
                        fx.debrisBurst(ex, y - 1, ey, 0xff5050, 8, 1);
                        fx.sparks(ex, y, ey, 0xff8080, 14, 1.4f);
                        flash = Mathf.Max(flash, 0.2f);
                        Shake(ui, 0.25f);
                        EmitFloater(e.left > 0 ? "Schwachpunkt zerstört!" : "Panzer gebrochen!", e.x, 3, e.y, "#ff7070", true);
                        break;
                    }
                    case "devour":
                    {
                        float y = gy(map, e.x, e.y);
                        fx.ring(ex, y, ey, 1.4f, 0x6a1030, 0.6f, 0.25f);
                        fx.debrisBurst(ex, y, ey, 0x8a8070, 8);
                        EmitFloater("Verschlungen!", e.x, 1.5, e.y, "#ff6080", true);
                        Shake(ui, 0.2f);
                        break;
                    }
                    case "heal":
                    {
                        float y = gy(map, e.x, e.y);
                        float rr = (float)e.r;
                        fx.ring(ex, y, ey, rr, 0x60ff90, 0.6f, 0.1f);
                        for (int i = 0; i < 8 * fx.intensity; i++)
                            fx.emit(ex + (Rnd() - 0.5f) * rr, y + 0.3f, ey + (Rnd() - 0.5f) * rr, 0.8f, 0xa0ffb0, shape: SHAPE.star, vy: 1.2f, s0: 0.14f, s1: 0.06f);
                        break;
                    }
                    case "burrow":
                    {
                        float y = gy(map, e.x, e.y);
                        for (int i = 0; i < 5; i++) fx.smoke(ex, y + 0.1f, ey, 0.5f, 0x6a5a40, 0.55f, 0.9f);
                        fx.debrisBurst(ex, y, ey, 0x6a5a40, 5);
                        break;
                    }
                    case "gold":
                        EmitFloater("+" + U.Fmt(e.amount), e.x, 1.1, e.y, "#ffd84a", null);
                        break;
                    case "ability":
                    {
                        float y = gy(map, e.x, e.y);
                        if (e.stage == "interrupt")
                        {
                            fx.ring(ex, y + 0.3f, ey, 1.8f, 0x60e8d4, 0.5f, 0.15f);
                            EmitFloater("Unterbrochen!", e.x, 1.6, e.y, "#60e8d4", null);
                        }
                        else if (e.stage == "cast")
                        {
                            float br = 0;
                            int bc = 0;
                            switch (e.id)
                            {
                                case "quake": br = 2.2f; bc = 0xffa040; break;
                                case "nullPulse": br = 4; bc = 0x40e0ff; break;
                                case "summonBrood": br = 2.2f; bc = 0xff6020; break;
                                case "stomp": br = 2.8f; bc = 0xffb060; break;
                                case "siegeSlam": br = 1; bc = 0xff8040; break;
                                case "heal": br = 2.5f; bc = 0x60ff90; break;
                                case "devour": br = 1.5f; bc = 0x80204a; break;
                                case "riftJump": br = 1.5f; bc = 0xd060ff; break;
                            }
                            if (br > 0)
                            {
                                fx.ring(ex, y, ey, br, bc, 0.55f, 0.2f);
                                fx.glow(ex, y + 0.5f, ey, br * 0.8f, bc, 0.25f, 0.6f);
                                if (e.id == "quake" || e.id == "stomp" || e.id == "siegeSlam")
                                {
                                    fx.debrisBurst(ex, y, ey, 0x6a5a48, 6);
                                    for (int i = 0; i < 4; i++) fx.smoke(ex + (Rnd() - 0.5f) * br, y + 0.1f, ey + (Rnd() - 0.5f) * br, 0.8f, 0x7a6a58, 0.4f);
                                }
                                Shake(ui, e.id == "stomp" ? 0.35f : 0.12f);
                            }
                        }
                        break;
                    }
                    case "bossPhase":
                    {
                        if (e.phase == 0) break; // Auftritt wird separat inszeniert
                        EnemyInstance boss = null;
                        foreach (var x in sim.s.enemies) if (x.boss == e.boss && x.alive) { boss = x; break; }
                        if (boss != null && Enemies.BOSSES.TryGetValue(e.boss, out var bdef))
                        {
                            float y = gy(map, boss.x, boss.y);
                            int bc = Ci(bdef.color);
                            fx.ring((float)boss.x, y, (float)boss.y, 4, bc, 0.9f, 0.2f);
                            fx.pillar((float)boss.x, y, (float)boss.y, 6, Col.Mix(bc, 0xffffff, 0.3), 0.9f, 0.7f);
                            fx.sparks((float)boss.x, y + 1.5f, (float)boss.y, bc, 24, 2);
                        }
                        flash = Mathf.Max(flash, 0.4f);
                        Shake(ui, 0.5f);
                        break;
                    }
                    case "heroDeploy":
                    {
                        if (!Heroes.HEROES.TryGetValue(e.hero ?? "", out var def)) break;
                        float y = gy(map, e.x, e.y);
                        fx.pillar(ex, y, ey, 4, Ci(def.accent), 0.6f, 0.3f);
                        fx.ring(ex, y, ey, 1.6f, Ci(def.color), 0.6f, 0.15f);
                        fx.sparks(ex, y + 0.5f, ey, Ci(def.accent), 14, 1.3f);
                        break;
                    }
                    case "heroDown":
                    {
                        float y = gy(map, e.x, e.y);
                        for (int i = 0; i < 6; i++) fx.smoke(ex, y + 0.3f, ey, 0.6f, 0x5a5a6a, 0.5f);
                        fx.sparks(ex, y + 0.5f, ey, 0xa0a0b0, 10, 1);
                        fx.ring(ex, y, ey, 1.2f, 0xff6060, 0.5f, 0.12f);
                        string nm = Heroes.HEROES.TryGetValue(e.hero ?? "", out var hd) ? hd.name : e.hero ?? "";
                        var parts = nm.Split(' ');
                        EmitFloater(parts[parts.Length - 1] + " fällt!", e.x, 1.5, e.y, "#ff8080", true);
                        foreach (var hs in heroSmooth.Values)
                            if (hs.id == e.hero && hs.inst != null)
                            {
                                corpses.Add(MakeCorpse(hs.inst, hs.x, hs.h, hs.y, 1.1f * 0.95f * UNIT, 0xa0a0b0, false));
                                hs.inst = null;
                            }
                        break;
                    }
                    case "heroLevel":
                    {
                        HeroInstance h = null;
                        foreach (var x in sim.s.heroes) if (x.id == e.hero) { h = x; break; }
                        if (h != null)
                        {
                            float y = gy(map, h.x, h.y);
                            float hx = (float)h.x, hz = (float)h.y;
                            fx.pillar(hx, y, hz, 3, 0xffd860, 0.6f, 0.3f);
                            fx.ring(hx, y, hz, 1.2f, 0xffd860, 0.5f, 0.15f);
                            for (int i = 0; i < 10; i++) fx.emit(hx, y + 0.8f, hz, 0.9f, 0xfff0a0, shape: SHAPE.star, vx: (Rnd() - 0.5f) * 2, vy: 1 + Rnd() * 2, vz: (Rnd() - 0.5f) * 2, drag: 1.2f, c1: 0xffb020, s0: 0.2f, s1: 0.05f);
                            EmitFloater("Stufe " + U.Fmt(e.level) + "!", h.x, 1.6, h.y, "#ffd860", true);
                        }
                        break;
                    }
                    case "heroAttack":
                    {
                        if (!Heroes.HEROES.TryGetValue(e.hero ?? "", out var def)) break;
                        float y = gy(map, e.tx, e.ty) + 0.45f;
                        int acc = Ci(def.accent);
                        if (e.melee)
                        {
                            fx.sparks(etx, y, ety, acc, 4, 0.9f, 0.1f);
                            fx.emit((ex + etx) / 2, y, (ey + ety) / 2, 0.15f, 0xffffff, shape: SHAPE.ring, s0: 0.2f, s1: 0.6f, a: 0.7f);
                        }
                        else
                        {
                            fx.beam(ex, gy(map, e.x, e.y) + 0.7f, ey, etx, y, ety, acc, 0.03f, 0.12f);
                            fx.glow(etx, y, ety, 0.35f, acc, 0.15f);
                        }
                        foreach (var hs in heroSmooth.Values) if (hs.id == e.hero && hs.inst != null && hs.inst.unit != null) hs.inst.unit.PlayAttack();
                        break;
                    }
                    case "heroAbility":
                    {
                        if (!Heroes.HEROES.TryGetValue(e.hero ?? "", out var def)) break;
                        float y = gy(map, e.x, e.y);
                        float rr = Mathf.Max(0.6f, (float)e.r);
                        int acc = Ci(def.accent);
                        fx.ring(ex, y, ey, rr, acc, 0.55f, 0.14f);
                        fx.glow(ex, y + 0.6f, ey, Mathf.Min(2.5f, rr), acc, 0.3f, 0.6f);
                        fx.sparks(ex, y + 0.6f, ey, acc, 8, 1.2f);
                        if (e.ability == "whirl") for (int k = 0; k < 3; k++) fx.ring(ex, y + 0.3f + k * 0.1f, ey, rr * (0.6f + k * 0.2f), 0xffffff, 0.3f + k * 0.08f, 0.05f);
                        if (rr >= 3) Shake(ui, 0.12f);
                        foreach (var hs in heroSmooth.Values) if (hs.id == e.hero && hs.inst != null && hs.inst.unit != null) hs.inst.unit.PlayAttack();
                        break;
                    }
                    case "champAttack":
                    {
                        float y = gy(map, e.tx, e.ty) + 0.45f;
                        int col = Ci(e.color);
                        if (e.melee)
                        {
                            fx.sparks(etx, y, ety, col, 4, 0.9f, 0.1f);
                            fx.emit((ex + etx) / 2, y, (ey + ety) / 2, 0.15f, 0xffffff, shape: SHAPE.ring, s0: 0.2f, s1: 0.6f, a: 0.7f);
                        }
                        else
                        {
                            fx.beam(ex, gy(map, e.x, e.y) + 0.7f, ey, etx, y, ety, col, 0.035f, 0.12f);
                            fx.glow(etx, y, ety, 0.35f, col, 0.15f);
                        }
                        if (champSmooth != null && champSmooth.inst != null && champSmooth.inst.unit != null) champSmooth.inst.unit.PlayAttack();
                        break;
                    }
                    case "champSkill":
                    {
                        float y = gy(map, e.tx, e.ty);
                        float rr = Mathf.Max(0.6f, (float)e.r);
                        int col = Ci(e.color);
                        fx.ring(etx, y, ety, rr, col, 0.55f, 0.14f);
                        fx.glow(etx, y + 0.6f, ety, Mathf.Min(2.5f, rr), col, 0.3f, 0.6f);
                        fx.sparks(etx, y + 0.6f, ety, col, 10, 1.2f);
                        if (e.skill == "charge" || e.skill == "pierce") fx.beam(ex, gy(map, e.x, e.y) + 0.6f, ey, etx, y + 0.6f, ety, col, 0.12f, 0.3f);
                        if (e.skill == "whirl" || e.skill == "bladestorm") for (int k = 0; k < 3; k++) fx.ring(etx, y + 0.3f + k * 0.1f, ety, rr * (0.6f + k * 0.2f), 0xffffff, 0.3f + k * 0.08f, 0.05f);
                        if (e.skill == "citadel" || e.skill == "inspire" || e.skill == "field") fx.pillar(etx, y, ety, 3, col, 0.7f, 0.4f);
                        if (rr >= 3) Shake(ui, 0.12f);
                        if (champSmooth != null && champSmooth.inst != null && champSmooth.inst.unit != null) champSmooth.inst.unit.PlayAttack();
                        break;
                    }
                    case "champLevel":
                    {
                        float y = gy(map, e.x, e.y);
                        fx.pillar(ex, y, ey, 3, 0xffd860, 0.6f, 0.3f);
                        fx.ring(ex, y, ey, 1.2f, 0xffd860, 0.5f, 0.15f);
                        for (int i = 0; i < 12; i++) fx.emit(ex, y + 0.8f, ey, 0.9f, 0xfff0a0, shape: SHAPE.star, vx: (Rnd() - 0.5f) * 2, vy: 1 + Rnd() * 2, vz: (Rnd() - 0.5f) * 2, drag: 1.2f, c1: 0xffb020, s0: 0.2f, s1: 0.05f);
                        EmitFloater("Stufe " + U.Fmt(e.level) + "!", e.x, 1.6, e.y, "#ffd860", true);
                        break;
                    }
                    case "champDown":
                        fx.ring(ex, gy(map, e.x, e.y), ey, 1.2f, 0xff6060, 0.5f, 0.12f);
                        EmitFloater("Held gefallen!", e.x, 1.5, e.y, "#ff8080", true);
                        if (champSmooth != null && champSmooth.inst != null)
                        {
                            corpses.Add(MakeCorpse(champSmooth.inst, champSmooth.x, champSmooth.h, champSmooth.y, 1.1f * 1.05f * UNIT, 0x9ad8ff, false));
                            champSmooth.inst = null;
                        }
                        break;
                    case "champRespawn":
                        fx.pillar(ex, gy(map, e.x, e.y), ey, 3, 0x9ad8ff, 0.7f, 0.35f);
                        break;
                    case "pickup":
                    {
                        int col = Ci(e.color);
                        fx.glow(ex, gy(map, e.x, e.y) + 0.4f, ey, 0.5f, col, 0.25f);
                        if (e.kind != "xp") EmitFloater(e.label, e.x, 1.2, e.y, Col.Css(col), e.kind == "item");
                        break;
                    }
                    case "mined":
                        fx.debrisBurst(ex, gy(map, e.x, e.y) + 0.3f, ey, 0x8a7a60, 14);
                        fx.ring(ex, gy(map, e.x, e.y) + 0.05f, ey, 1.0f, 0xffd860, 0.5f, 0.1f);
                        EmitFloater(e.label, e.x, 1.4, e.y, "#ffd860", true);
                        break;
                    case "mineStart":
                        fx.ring(ex, gy(map, e.x, e.y) + 0.05f, ey, 0.8f, 0xffd860, 0.4f, 0.08f);
                        break;
                    case "towerBuff":
                    {
                        int col = Ci(e.color);
                        fx.ring(ex, gy(map, e.x, e.y) + 0.05f, ey, 0.7f, col, 0.5f, 0.1f);
                        fx.sparks(ex, gy(map, e.x, e.y) + 0.9f, ey, col, 5, 0.6f);
                        break;
                    }
                    case "potion":
                    {
                        int col = Items.POTIONS.TryGetValue(e.potion ?? "", out var pd) ? Ci(pd.color) : 0xff6060;
                        fx.ring(ex, gy(map, e.x, e.y) + 0.05f, ey, 0.9f, col, 0.5f, 0.1f);
                        for (int i = 0; i < 8; i++) fx.emit(ex, gy(map, e.x, e.y) + 0.3f, ey, 0.7f, col, shape: SHAPE.glow, vx: Rnd() - 0.5f, vy: 1 + Rnd(), vz: Rnd() - 0.5f, s0: 0.18f, s1: 0.04f);
                        break;
                    }
                    case "victory":
                        flash = Mathf.Max(flash, 0.3f);
                        break;
                    default:
                        break;
                }
            }
        }

        void Shake(ViewUiState ui, float v)
        {
            if (!ui.reducedMotion) shake = Mathf.Max(shake, v);
        }

        void EmitFloater(string text, double x, double y, double z, string color, bool? big)
        {
            if (onFloater == null || text == null) return;
            onFloater(new Floater { text = text, x = x, y = y, z = z, color = color, big = big });
        }

        readonly HashSet<double> aliveUids = new HashSet<double>();

        Corpse MakeCorpse(ModelInst inst, float x, float y, float z, float height, int color, bool boss)
        {
            float hold = 0;
            if (inst.unit != null) hold = Mathf.Min(boss ? 1.6f : 1.0f, inst.unit.PlayDeath());
            // Tatsächlich angezeigte Größe übernehmen (Längenbegrenzung der Vierbeiner), damit der Körper beim Tod nicht springt
            if (inst.tr != null && inst.nativeH > 0.05f && inst.tr.localScale.x > 0) height = Mathf.Min(height, inst.tr.localScale.x * inst.nativeH);
            return new Corpse { inst = inst, x = x, y = y, z = z, height = height, t = 0, hold = hold, max = boss ? 1.6f : 0.55f, color = color, boss = boss, scale = height };
        }

        void addCorpse(GameSimulation sim, float x, float y, string enemy, string boss, int color, float size)
        {
            // passende Glättung (Blickrichtung, Höhe) über die Position suchen
            // Lebende Gegner derselben Sorte sind keine Kandidaten: nur Glättungen, deren Gegner nicht mehr existiert
            // (sonst wurde gelegentlich das Modell eines Nachbarn zur Leiche und der Tote blieb stehen)
            aliveUids.Clear();
            foreach (var en in sim.s.enemies) if (en.hp > 0) aliveUids.Add(en.uid);
            Smooth best = null;
            float bd = 1.5f;
            foreach (var kv in smooth)
            {
                var s = kv.Value;
                if (s.def != enemy || s.boss != boss || s.inst == null) continue;
                if (aliveUids.Contains(kv.Key)) continue;
                float d = Mathf.Abs(s.x - x) + Mathf.Abs(s.y - y);
                if (d < bd)
                {
                    bd = d;
                    best = s;
                }
            }
            float h = best != null ? best.h : 0;
            bool flying = enemy != null && Enemies.ENEMIES.TryGetValue(enemy, out var edef) && edef.flying;
            if (best != null && best.inst != null)
            {
                float height;
                if (boss != null && Enemies.BOSSES.TryGetValue(boss, out var bdef)) height = WorldModels.bossHeight(boss) * (float)bdef.size * UNIT;
                else height = size * UNIT; // size kommt aus dem Ereignis bereits verdoppelt (inkl. Elite) – vorher doppelt so groß
                var cp = MakeCorpse(best.inst, best.x, h + (flying ? 1.1f : 0), best.y, height, color, boss != null);
                cp.fall = flying ? 1.1f : 0;
                corpses.Add(cp);
                best.inst = null;
                if (corpses.Count > 60)
                {
                    var old = corpses[0];
                    if (old.inst != null) pool.Release(old.inst);
                    corpses.RemoveAt(0);
                }
            }
            int n = boss != null ? 40 : 7;
            for (int i = 0; i < n * fx.intensity; i++)
            {
                float a = Rnd() * Mathf.PI * 2;
                fx.emit(x + Mathf.Cos(a) * 0.2f * size, h + 0.3f + Rnd() * 0.5f * size, y + Mathf.Sin(a) * 0.2f * size, 0.5f + Rnd() * 0.4f, Col.Mix(color, 0xffffff, 0.4),
                    vx: Mathf.Cos(a) * 0.6f, vy: 0.8f + Rnd() * 1.5f, vz: Mathf.Sin(a) * 0.6f, drag: 1.5f, c1: color, s0: 0.14f, s1: 0.02f);
            }
            fx.decal(x, h, y, 0.35f * size + 0.2f, Col.Shade(color, 0.35), 0.35f, 4);
            if (boss != null)
            {
                fx.explosion(x, h, y, 2.5f, color, true);
                fx.pillar(x, h, y, 8, Col.Mix(color, 0xffffff, 0.5), 1.4f, 0.9f);
            }
        }

        // ------------------------------------------------------------ Teile zeichnen (Zusatzteile aus Grundkörpern)

        void drawParts(Part[] parts, float x, float y, float z, float yaw, float scale, int main, int accent, string pass, float alpha, int tint, float tintAmt, float emitBoost, float phase, float squash = 1)
        {
            float cy = Mathf.Cos(yaw), sy = Mathf.Sin(yaw);
            float t = time;
            for (int i = 0; i < parts.Length; i++)
            {
                var p = parts[i];
                float roll = p.roll;
                if (p.wing != 0) roll += Mathf.Sin(t * 13 + phase) * 0.7f * p.wing;
                float spin = p.spin != 0 ? t * p.spin : 0;
                float bob = p.bob != 0 ? Mathf.Sin(t * 2.5f + phase) * p.bob : 0;
                float ox = (p.x * cy + p.z * sy) * scale;
                float oz = (-p.x * sy + p.z * cy) * scale;
                int col = p.c == -1 ? main : p.c == -2 ? accent : p.c == -3 ? Ci(skin.stone) : p.c;
                if (tint >= 0 && tintAmt > 0) col = Col.Mix(col, tint, tintAmt);
                float wy = y + (p.y + bob) * scale * squash;
                r.add(pass, p.m, x + ox, wy, z + oz, p.sx * scale, p.sy * scale * squash, p.sz * scale, col, alpha, p.e + emitBoost, yaw + p.yaw + spin, p.pitch, roll);
                if (p.glow != 0 && alpha > 0.3f)
                {
                    float pulse = 0.85f + Mathf.Sin(t * 3 + phase) * 0.15f;
                    r.billboard(SPRITE_ADD, SHAPE.glow, x + ox, wy + (p.m == "cone8" || p.m == "cyl8" ? p.sy * scale * 0.5f : 0), z + oz, Mathf.Max(p.sx, p.sy) * scale * 1.5f * pulse, p.glow, 0.45f * alpha);
                }
            }
        }

        // ------------------------------------------------------------ Frame

        public void frame(GameSimulation sim, ViewUiState ui, double dtD)
        {
            if (lighting == null || mapRef == null || sim == null) return;
            float dt = (float)dtD;
            time += dt;
            stamp++;
            var s = sim.s;
            float pcap = ParticleCap(ui);
            fx.cap = (int)Math.Max(200, pcap * 2);
            fx.intensity = ui.reducedMotion ? 0.5f : 1f;

            // Kamera inkl. Wackeln (nur für dieses Bild, ohne Drift)
            cam.Resize(Screen.width, Screen.height);
            cam.spin = ui.cinematic == true ? 0.06f : 0;
            float sx = 0, sz = 0;
            if (shake > 0)
            {
                shake = Mathf.Max(0, shake - dt);
                float k = shake * 0.35f;
                sx = (Rnd() - 0.5f) * k;
                sz = (Rnd() - 0.5f) * k;
            }
            cam.tx += sx;
            cam.tz += sz;
            cam.Update(dt);
            cam.tx -= sx;
            cam.tz -= sz;
            r.begin(cam.cam, time);

            if (firstFrame)
            {
                foreach (var t in s.towers) towerBorn[t.uid] = -10;
                foreach (var e in s.enemies) if (e.boss != null) bossSeen.Add(e.uid);
                firstFrame = false;
            }

            if ((s.map.rev ?? 0) != mapRev) rebuildTerrain(s.map);
            updateLighting(sim, dt);
            drawWorldDynamics(sim, ui);
            drawNetwork(sim);
            drawTowers(sim, ui);
            drawSelection(sim, ui);
            drawZones(sim);
            drawEnemies(sim, ui, dt);
            drawCorpses(dt);
            drawHeroes(sim, ui, dt);
            if (s.he != null) drawHeroEndless(sim, ui, dt);
            else if (shopInst != null) { pool.Release(shopInst); shopInst = null; }
            drawProjectiles(sim);
            drawWeather(sim, dt, ui);
            fx.update(dt, r);

            flash = Mathf.Max(0, flash - dt * 1.6f);
            grade(sim, ui);
            r.end();
        }

        /// <summary>Partikelbudget: aus dem Oberflächenzustand, sonst GameApp.particleCap().</summary>
        static float ParticleCap(ViewUiState ui)
        {
            if (ui.particleCap > 0) return (float)ui.particleCap;
            var app = GameApp.I;
            return app != null ? (float)app.particleCap() : 700f;
        }

        void updateLighting(GameSimulation sim, float dt)
        {
            var L = lighting;
            var map = mapRef;
            if (L == null || map == null) return;
            var s = sim.s;
            var pal = Biomes.BIOMES.TryGetValue(map.biome ?? "forest", out var bd) ? bd.palette : Biomes.BIOMES["forest"].palette;
            // Tageszeit wandert mit den Wellen (10 Wellen = ein Tag)
            float tod = ((float)((s.wave % 10) / 10) * TOD.Length + (mood != null ? mood.todStart : 0)) % TOD.Length;
            int i0 = Mathf.FloorToInt(tod) % TOD.Length;
            int i1 = (i0 + 1) % TOD.Length;
            float f = tod - Mathf.Floor(tod);
            int c0 = (int)TOD[i0][0], c1 = (int)TOD[i1][0];
            float k0 = TOD[i0][1], e0 = TOD[i0][2], k1 = TOD[i1][1], e1 = TOD[i1][2];
            int col = Col.Mix(Col.Mix((int)pal.light, c0, 0.55), Col.Mix((int)pal.light, c1, 0.55), f);
            float inten = k0 + (k1 - k0) * f;
            float elev = e0 + (e1 - e0) * f;
            bool boss = bossAlive(sim);
            if (boss)
            {
                col = Col.Mix(col, 0xff7050, 0.3);
                inten *= 0.92f;
            }
            if (s.phase == "build") inten *= 1.04f;
            if (mood != null) col = Col.Mix(col, mood.tint, mood.tintAmt * 0.5f);
            var target = Rgb(col) * (0.88f * inten);
            float k = lightSnap ? 1 : Mathf.Min(1, dt * 1.2f);
            lightSnap = false;
            lightCol += (target - lightCol) * k;
            L.lightColor = lightCol;
            float az = -2.4f + (float)(s.wave % 10) * 0.12f;
            var want = new Vector3(Mathf.Cos(az) * 0.7f, elev, Mathf.Sin(az) * 0.7f).normalized;
            lightDir += (want - lightDir) * k;
            L.light = lightDir.normalized;
            int skyCol = Col.Mix(Col.Mix(Col.Mix((int)pal.sky, 0xffffff, 0.55), boss ? 0xff8060 : col, 0.25), mood != null ? mood.tint : 0xffffff, mood != null ? mood.tintAmt : 0);
            L.sky = Rgb(skyCol) * (0.55f * (0.7f + inten * 0.3f));
            string ev = s.currentWave != null ? s.currentWave.@event : null;
            float size = Mathf.Max((float)map.width, (float)map.height);
            float fogMul = ev == "fog" ? 0.45f : 1;
            L.fogStart = size * 0.62f * fogMul;
            L.fogEnd = size * 0.62f * fogMul + (ev == "fog" ? 9 : 14);
            ApplyLighting();
        }

        /// <summary>Licht, Umgebung und Nebel in die Unity-Szene übertragen (Rolle von Renderer.render mit Lighting).</summary>
        void ApplyLighting()
        {
            var L = lighting;
            if (L == null) return;
            if (sun != null)
            {
                var lc = L.lightColor;
                float mx = Mathf.Max(0.001f, Mathf.Max(lc.x, Mathf.Max(lc.y, lc.z)));
                sun.color = new Color(lc.x / mx, lc.y / mx, lc.z / mx);
                sun.intensity = mx * 1.35f;
                var d = new Vector3(L.light.x, L.light.y, -L.light.z);
                if (d.sqrMagnitude > 1e-6f) sun.transform.rotation = Quaternion.LookRotation(-d.normalized, Vector3.up);
            }
            var sky = new Color(L.sky.x, L.sky.y, L.sky.z);
            var gr = new Color(L.ground.x, L.ground.y, L.ground.z);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = sky * 1.25f;
            RenderSettings.ambientEquatorColor = Color.Lerp(sky, gr, 0.5f) * 1.25f;
            RenderSettings.ambientGroundColor = gr * 1.25f;
            var fogC = Col.ToColor(L.fog);
            RenderSettings.fogColor = fogC;
            // Kamera steht 60 Einheiten vor dem Blickpunkt; Nebel ab dem Inselrand in Blickrichtung
            float cp = Mathf.Cos(cam.pitch);
            RenderSettings.fogStartDistance = 60 + L.fogStart * cp;
            RenderSettings.fogEndDistance = 60 + L.fogEnd * cp;
            if (cam.cam != null) cam.cam.backgroundColor = fogC;
        }

        /// <summary>Farbstimmung (TS grade): Bildblitz und Vignette (bei niedrigen Leben pulsierend rot).</summary>
        void grade(GameSimulation sim, ViewUiState ui)
        {
            var s = sim.s;
            double low = s.maxLives > 0 ? s.lives / s.maxLives : 1;
            float vig = 0.16f;
            int vigCol = 0x000000;
            if (low < 0.3 && s.phase != "victory" && ui.cinematic != true)
            {
                vig += (float)(0.3 - low) * 1.4f * (0.75f + Mathf.Sin(time * 4) * 0.25f);
                vigCol = 0x5a0808;
            }
            float fl = ui.reducedMotion ? flash * 0.3f : flash;
            r.screenFx(fl * 0.8f, 0xffffff, vig, vigCol);
            // Glühen, Tonemapping und Farbkorrektur: siehe PostFx (URP-Volume, ab Qualität „Mittel“).
        }

        // ------------------------------------------------------------ Welt: Portale, Kern, Leuchtpunkte, Raster, Route

        void drawWorldDynamics(GameSimulation sim, ViewUiState ui)
        {
            var s = sim.s;
            var map = s.map;
            float t = time;
            // Leuchtpunkte (Kristalle, Lava, Pilze, Adern)
            for (int i = 0; i < glows.Count; i++)
            {
                var g = glows[i];
                float p = 0.75f + Mathf.Sin(t * 1.7f + g.phase) * 0.25f;
                r.billboard(SPRITE_ADD, SHAPE.glow, g.x, g.y, g.z, g.size * p, g.color, 0.2f);
            }
            var biome = Biomes.BIOMES.TryGetValue(map.biome ?? "forest", out var bd) ? bd : Biomes.BIOMES["forest"];
            var sea = Terrain.sea(biome.sea);
            // Wasser/Lava-Funkeln am Rand
            if (biome.sea == "lava" && Rnd() < 0.3f * fx.intensity)
            {
                float a = Rnd() * Mathf.PI * 2;
                float d = Mathf.Max((float)map.width, (float)map.height) * (0.55f + Rnd() * 0.3f);
                fx.emit((float)map.width / 2 + Mathf.Cos(a) * d, sea.y + 0.1f, (float)map.height / 2 + Mathf.Sin(a) * d, 1, 0xffc060, vy: 1.5f, grav: 2, c1: 0xff4010, s0: 0.2f, s1: 0.05f);
            }
            // Portale
            foreach (var sp in spawnYaw)
            {
                if (sp.inst == null)
                {
                    // Ersatz ohne Modell: Ring und Wirbelscheibe aus Grundkörpern
                    r.add("opaque", "torus", sp.x, 0.75f, sp.y, 1.25f, 1.25f, 1.6f, 0xc060ff, 1, 1.0f, sp.yaw + Mathf.PI / 2, 0, Mathf.PI / 2);
                    r.add("transparent", "disc", sp.x, 0.75f, sp.y, 0.58f, 1, 0.58f, 0x400a60, 0.85f, 1, sp.yaw + Mathf.PI / 2, t * 2, Mathf.PI / 2);
                }
                r.billboard(SPRITE_ADD, SHAPE.glow, sp.x, 0.75f, sp.y, 1.0f + Mathf.Sin(t * 3) * 0.1f, 0xb050ff, 0.35f);
                r.ground(SPRITE_ADD, SHAPE.ring, sp.x, 0.02f, sp.y, 0.9f + Mathf.Sin(t * 2) * 0.1f, 0xc060ff, 0.6f, t, 0.3f, 0.1f);
                // 3.13: wirbelnder Riss in der Portalebene, Runenkreis am Boden, Lichtsäule
                float px = Mathf.Sin(sp.yaw) * 0.6f;
                float pz = Mathf.Cos(sp.yaw) * 0.6f;
                float c1 = Mathf.Cos(t * 1.4f), s1 = Mathf.Sin(t * 1.4f);
                r.sprite(SPRITE_ADD, SHAPE.vortex, sp.x, 0.75f, sp.y, px * c1, 0.6f * s1, pz * c1, -px * s1, 0.6f * c1, -pz * s1, 0xd890ff, 0.9f, 0.3f, 0.15f, t * 1.4f);
                float c2 = Mathf.Cos(-t * 2.1f), s2 = Mathf.Sin(-t * 2.1f);
                r.sprite(SPRITE_ADD, SHAPE.vortex, sp.x, 0.75f, sp.y, -px * 0.8f * c2, 0.48f * s2, -pz * 0.8f * c2, px * 0.8f * s2, 0.48f * c2, pz * 0.8f * s2, 0x7040ff, 0.7f, 0.3f, 0.15f, -t * 2.1f);
                r.ground(SPRITE_ADD, SHAPE.star, sp.x, 0.03f, sp.y, 1.35f, 0x9a50ff, 0.18f, -t * 0.4f);
                r.ground(SPRITE_ADD, SHAPE.ring, sp.x, 0.03f, sp.y, 1.45f, 0xc080ff, 0.35f, t * 0.3f, 0.3f, 0.03f);
                r.line(sp.x, 0, sp.y, sp.x, 4.5f, sp.y, 0.35f + Mathf.Sin(t * 2.3f) * 0.05f, 0xb060ff, 0.14f);
                if (Rnd() < 0.35f * fx.intensity)
                {
                    float a = Rnd() * Mathf.PI * 2;
                    fx.emit(sp.x + Mathf.Cos(a) * 0.9f, 0.75f + Mathf.Sin(a) * 0.7f, sp.y + Mathf.Sin(a) * 0.3f, 0.55f, 0xe0a0ff, vx: -Mathf.Cos(a) * 1.5f, vy: -Mathf.Sin(a) * 1.2f, vz: -Mathf.Sin(a) * 0.4f, c1: 0x6010a0, s0: 0.12f, s1: 0.02f);
                }
            }
            // Kern
            var gp = map.goal;
            float health = (float)(s.lives / Math.Max(1, s.maxLives));
            int coreCol = Col.Mix(0xff3a3a, 0x60f0ff, health);
            float pulse = 1 + Mathf.Sin(t * 3) * 0.05f;
            float cx = (float)gp.x + 0.5f;
            float cz = (float)gp.y + 0.5f;
            float cyy = 1.0f + Mathf.Sin(t * 2) * 0.08f;
            if (coreInst != null)
            {
                coreInst.Tint(coreCol, 0.45f, 0.5f + Mathf.Sin(t * 3) * 0.15f);
            }
            else
            {
                r.add("opaque", "octa", cx, cyy, cz, 0.55f * pulse, 1.0f * pulse, 0.55f * pulse, coreCol, 1, 1.1f, t * 0.8f);
                for (int k = 0; k < 3; k++)
                {
                    float a = t * 1.3f + k / 3f * Mathf.PI * 2;
                    r.add("opaque", "gem", cx + Mathf.Cos(a) * 0.62f, cyy + Mathf.Sin(t * 2 + k) * 0.2f, cz + Mathf.Sin(a) * 0.62f, 0.14f, 0.14f, 0.14f, coreCol, 1, 1.2f, a);
                }
            }
            r.add("transparent", "sphere", cx, cyy, cz, 1.25f, 1.25f, 1.25f, coreCol, 0.12f, 1);
            r.billboard(SPRITE_ADD, SHAPE.glow, cx, cyy, cz, 1.2f * pulse, coreCol, 0.3f);
            r.ground(SPRITE_ADD, SHAPE.ring, cx, 0.14f, cz, 0.95f, coreCol, 0.5f, -t, 0.3f, 0.08f);
            // 3.13: Lichtsäule in den Himmel, Runenkreise und schwebende Ringe um den Kern
            r.line(cx, 0.2f, cz, cx, 7, cz, 0.22f + Mathf.Sin(t * 3.1f) * 0.03f, coreCol, 0.22f);
            r.line(cx, 0.2f, cz, cx, 4, cz, 0.08f, 0xffffff, 0.25f);
            r.ground(SPRITE_ADD, SHAPE.star, cx, 0.15f, cz, 1.6f, coreCol, 0.16f, t * 0.25f);
            r.ground(SPRITE_ADD, SHAPE.ring, cx, 0.15f, cz, 1.7f, coreCol, 0.35f, -t * 0.2f, 0.3f, 0.025f);
            r.add("transparent", "ringThin", cx, cyy, cz, 1.05f, 1, 1.05f, coreCol, 0.55f, 1, t * 0.6f, 0.5f + Mathf.Sin(t * 0.7f) * 0.2f, 0.3f);
            r.add("transparent", "ringThin", cx, cyy, cz, 0.85f, 1, 0.85f, coreCol, 0.45f, 1, -t * 0.9f, -0.6f, Mathf.Sin(t * 0.5f) * 0.4f);
            if (health < 0.35f && Rnd() < 0.3f) fx.sparks(cx, cyy, cz, 0xff5050, 1, 0.8f);

            // Bauraster
            bool building = ui.buildDef != null;
            int w = (int)map.width, h = (int)map.height;
            if (building || ui.showGrid)
            {
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        int i = y * w + x;
                        if (map.cells[i] != Cell.Open || sim.c.towerByCell.ContainsKey(i)) continue;
                        float top = (float)Terrain.cellTop(map, x, y);
                        r.ground(false, SHAPE.square, x + 0.5f, top + 0.012f, y + 0.5f, 0.45f, building ? 0x80ff90 : 0xffffff, building ? 0.16f : 0.07f);
                    }
                }
            }

            // Route im Mazing andeuten (Lichtpunkte, die zum Kern laufen)
            if (map.variant == "mazing" && s.phase == "build" && !(ui.buildDef != null && ui.hoverCell != null))
            {
                var route = currentRoute(sim);
                for (int i = 0; i < route.Count - 1; i++)
                {
                    float ph = ((t * 2 - i * 0.25f) % 2 + 2) % 2;
                    if (ph > 1) continue;
                    var a = route[i];
                    var b = route[i + 1];
                    r.billboard(SPRITE_ADD, SHAPE.glow, (float)(a.x + b.x) / 2 + 0.5f, (float)Terrain.cellTop(map, a.x, a.y) + 0.15f, (float)(a.y + b.y) / 2 + 0.5f, 0.22f, 0xfff0a0, 0.8f * (1 - Mathf.Abs(ph - 0.5f) * 2));
                }
            }
        }

        double routeV = double.NaN;
        readonly List<GridPos> routeCache = new List<GridPos>();

        List<GridPos> currentRoute(GameSimulation sim)
        {
            var towers = sim.s.towers;
            double v = towers.Count * 1000 + (towers.Count > 0 ? towers[towers.Count - 1].uid : 0);
            if (routeV == v) return routeCache;
            routeV = v;
            routeCache.Clear();
            var map = sim.s.map;
            int w = (int)map.width, h = (int)map.height;
            int cx = (int)map.spawn.x, cy = (int)map.spawn.y;
            routeCache.Add(new GridPos(cx, cy));
            var f = sim.c.field;
            if (f == null) return routeCache;
            for (int k = 0; k < 900; k++)
            {
                if (cx == (int)map.goal.x && cy == (int)map.goal.y) break;
                int bx = -1, by = -1;
                double bv = f[cy * w + cx];
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = cx + dx, ny = cy + dy;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        if (dx != 0 && dy != 0 && (f[cy * w + nx] >= 1e9 || f[ny * w + cx] >= 1e9)) continue;
                        double v2 = f[ny * w + nx];
                        if (v2 < bv - 1e-9)
                        {
                            bv = v2;
                            bx = nx; by = ny;
                        }
                    }
                if (bx < 0) break;
                routeCache.Add(new GridPos(bx, by));
                cx = bx; cy = by;
            }
            return routeCache;
        }

        // ------------------------------------------------------------ Netzwerk

        readonly HashSet<double> resSet = new HashSet<double>();

        void drawNetwork(GameSimulation sim)
        {
            var s = sim.s;
            if (s.network == null) return;
            resSet.Clear();
            foreach (var g in s.network.groups) if (g.resonances.Count > 0) foreach (var m in g.members) resSet.Add(m);
            bool surge = false;
            foreach (var b in s.buffs) if (b.id == "surge" || b.id == "resoSong") { surge = true; break; }
            foreach (var l in s.network.links)
            {
                if (!sim.c.towerByUid.TryGetValue(l.a, out var a) || !sim.c.towerByUid.TryGetValue(l.b, out var b)) continue;
                float ay = (float)Terrain.cellTop(s.map, a.cx, a.cy) + WorldModels.towerTop(a.def) * 0.9f;
                float by = (float)Terrain.cellTop(s.map, b.cx, b.cy) + WorldModels.towerTop(b.def) * 0.75f;
                bool resonant = resSet.Contains(a.uid) && resSet.Contains(b.uid);
                int col = resonant ? 0xffd860 : 0x5ab8ff;
                bool disrupted = a.disrupted > 0 || b.disrupted > 0;
                float wd = (surge ? 0.07f : 0.04f) * (disrupted ? 0.6f : 1);
                float alpha = disrupted ? 0.25f + Rnd() * 0.25f : 0.55f;
                float ax = (float)a.cx + 0.5f, az = (float)a.cy + 0.5f, bx = (float)b.cx + 0.5f, bz = (float)b.cy + 0.5f;
                r.line(ax, ay, az, bx, by, bz, wd * 2.5f, col, alpha * 0.4f);
                r.line(ax, ay, az, bx, by, bz, wd, Col.Mix(col, 0xffffff, 0.5), alpha);
                float tt = (float)((time * 0.8 + (l.a * 0.37 + l.b * 0.11)) % 1);
                r.billboard(SPRITE_ADD, SHAPE.glow, ax + (bx - ax) * tt, ay + (by - ay) * tt, az + (bz - az) * tt, 0.18f, col, 0.9f);
            }
            // Lichtbögen zwischen Tesla-Spulen
            foreach (var pair in TowerSystem.teslaPairs(sim))
            {
                var a = pair[0];
                var b = pair[1];
                float ay = (float)Terrain.cellTop(s.map, a.cx, a.cy) + 1.2f;
                float by = (float)Terrain.cellTop(s.map, b.cx, b.cy) + 1.2f;
                var pts = Fx.jagShared((float)a.cx + 0.5f, ay, (float)a.cy + 0.5f, (float)b.cx + 0.5f, by, (float)b.cy + 0.5f, out int n);
                float fl = 0.6f + Rnd() * 0.4f;
                for (int i = 0; i + 5 < n; i += 3)
                {
                    r.line(pts[i], pts[i + 1], pts[i + 2], pts[i + 3], pts[i + 4], pts[i + 5], 0.14f, 0x60c8ff, 0.3f * fl);
                    r.line(pts[i], pts[i + 1], pts[i + 2], pts[i + 3], pts[i + 4], pts[i + 5], 0.035f, 0xe0f8ff, 0.9f * fl);
                }
            }
        }

        // ------------------------------------------------------------ Türme

        static double StatOf(Dictionary<string, double> st, string key) => st != null && st.TryGetValue(key, out var v) ? v : 0;

        void drawTowers(GameSimulation sim, ViewUiState ui)
        {
            var s = sim.s;
            float t = time;
            bool hymn = false;
            foreach (var b in s.buffs) if (b.id == "hymn") { hymn = true; break; }
            var hl = ui.highlight;
            foreach (var tw in s.towers)
            {
                if (!Towers.TOWERS.TryGetValue(tw.def, out var def)) continue;
                float by = (float)Terrain.cellTop(s.map, tw.cx, tw.cy);
                float tsz = (float)Towers.towerSize(tw.def);
                float x = (float)tw.cx + tsz / 2, z = (float)tw.cy + tsz / 2;
                if (!towerBorn.TryGetValue(tw.uid, out float born))
                {
                    born = t;
                    towerBorn[tw.uid] = t;
                }
                float age = Mathf.Min(1, (t - born) / 0.45f);
                float grow = age >= 1 ? 1 : easeOutBack(age);
                bool stunned = tw.stunned > 0;
                bool legendary = def.legendary == true;
                float scale = (0.9f + (float)(tw.level - 1) * 0.035f + (tw.spec != 0 ? 0.04f : 0) + (legendary ? 0.08f : 0)) * grow * (tsz > 1 ? 1.75f : 1) * UNIT;
                float recoil = tw.fireAnim > 0 ? (float)tw.fireAnim : 0;
                float squash = 1 - recoil * 0.08f;
                float top = WorldModels.towerTop(tw.def);
                int defColor = Ci(def.color);
                float skinAmt = (float)skin.tintAmount;
                int main = skinAmt > 0 ? Col.Mix(defColor, Ci(skin.tint), skinAmt) : defColor;
                int accent = tw.spec == 1 ? 0xffa040 : tw.spec == 2 ? 0x60e0ff : 0xffffff;
                bool activeOn = tw.activeT > 0;
                float emitBoost = (recoil > 0 ? 0.35f * recoil : 0) + (activeOn ? 0.4f + Mathf.Sin(t * 10) * 0.2f : 0) + (tw.forged > s.time ? 0.25f : 0);

                // Modell
                if (!towerVis.TryGetValue(tw.uid, out var vis)) { vis = new TowerVis(); towerVis[tw.uid] = vis; }
                vis.stamp = stamp;
                if (vis.inst == null || vis.def != tw.def)
                {
                    if (vis.inst != null) pool.Release(vis.inst);
                    vis.inst = pool.Acquire(tw.def, defColor, PrimitiveType.Cylinder, true, IsoCamera.W(x, by, z));
                    vis.def = tw.def;
                    vis.inst.tr.rotation = Quaternion.identity;
                }
                var inst = vis.inst;
                // Türme größer und passend aufs Feld: Höhe so wählen, dass die Grundfläche ~88 % des Bauplatzes füllt
                // (mindestens 18 % größer als bisher, höchstens 60 %), damit schmale und breite Modelle gleich gut sitzen.
                float baseH = top * scale;
                float towerH = baseH * 1.18f;
                if (!inst.fallback && inst.nativeFoot > 0.05f && inst.nativeH > 0.05f)
                {
                    float lvlMul = (0.9f + (float)(tw.level - 1) * 0.035f + (tw.spec != 0 ? 0.04f : 0) + (legendary ? 0.08f : 0)) / 0.9f;
                    float fitH = 0.88f * tsz * grow * lvlMul * inst.nativeH / inst.nativeFoot;
                    towerH = Mathf.Clamp(fitH, baseH * 1.18f, baseH * 1.6f);
                }
                inst.Place(x, by, z, Mathf.Max(0.05f, towerH), squash, 0.94f * tsz);
                if (stunned) inst.Tint(0x505058, 0.6f, 0);
                else if (skinAmt > 0) inst.Tint(Ci(skin.tint), skinAmt, emitBoost);
                else inst.Tint(defColor, 0, emitBoost);
                if (inst.tower != null)
                {
                    if (def.attack != "none" && def.attack != "economy")
                    {
                        float aim = (float)tw.aim;
                        inst.tower.SetTarget(IsoCamera.W(x + Mathf.Cos(aim) * 4, by + towerH * 0.6f, z + Mathf.Sin(aim) * 4));
                    }
                    else inst.tower.ClearTarget();
                }

                if (def.attack != "none")
                {
                    if (tw.level >= 2)
                    {
                        // Stufenschmuck am echten Modell ausrichten: Fußkreis und Höhe aus den Modellmaßen
                        float kk = inst.tr.localScale.x;
                        float realTop = inst.fallback ? top * scale : inst.nativeH * kk;
                        float foot = inst.fallback ? 0.4f * scale : Mathf.Clamp(inst.nativeFoot * kk * 0.5f, 0.2f, 0.47f * tsz);
                        drawParts(WorldModels.levelParts((int)tw.level, top), x, by, z, 0, foot / 0.46f, main, accent, "opaque", 1, -1, 0, 0, (float)tw.uid);
                        int gold = 0xf0c040;
                        // Stufe 2: feiner goldener Bodenring
                        r.ground(SPRITE_ADD, SHAPE.ring, x, by + 0.035f, z, foot + 0.16f, gold, 0.45f, 0, 0.3f, 0.04f);
                        // Stufe 3: glatter Goldreif dicht um den Sockel
                        if (tw.level >= 3) r.add("opaque", "band", x, by + 0.05f, z, (foot + 0.07f) * 2, 1, (foot + 0.07f) * 2, 0xe0b040, 1, 0.35f, 0);
                        // Stufe 4: schwebender, leuchtender Heiligenschein über der Turmspitze
                        if (tw.level >= 4)
                        {
                            float hy = by + realTop * squash + 0.16f + Mathf.Sin(t * 2.2f + (float)tw.uid) * 0.03f;
                            int hc = tw.spec == 0 ? 0xffe080 : accent;
                            r.add("opaque", "band", x, hy, z, 0.44f, 1.4f, 0.44f, hc, 1, 1.4f, t * 1.2f);
                            r.billboard(SPRITE_ADD, SHAPE.glow, x, hy, z, 0.55f, hc, 0.3f);
                        }
                    }
                    if (tw.spec != 0) drawParts(WorldModels.specParts((int)tw.spec, top), x, by, z, 0, scale, main, accent, "opaque", 1, -1, 0, 0, (float)tw.uid);
                }
                // 3.6: Veredelung ★ – kleine Sterne über dem Turm
                int stars = tw.stars.HasValue ? (int)tw.stars.Value : 0;
                if (stars > 0)
                {
                    int shown = Mathf.Min(5, (stars + 1) / 2);
                    for (int k = 0; k < shown; k++)
                    {
                        float a = t * 0.8f + k * Mathf.PI * 2 / shown;
                        r.billboard(SPRITE_ADD, SHAPE.star, x + Mathf.Cos(a) * 0.32f, by + towerH + 0.25f, z + Mathf.Sin(a) * 0.32f, 0.11f, stars >= 10 ? 0xffe080 : 0xffd040, 0.9f);
                    }
                }
                if (age < 1) r.ground(SPRITE_ADD, SHAPE.ring, x, by + 0.03f, z, 0.3f + age * 0.6f, 0xffffff, 1 - age, 0, 0.3f, 0.12f);
                if (legendary)
                {
                    r.ground(SPRITE_ADD, SHAPE.ring, x, by + 0.04f, z, 0.62f, 0xffe080, 0.45f + Mathf.Sin(t * 2) * 0.15f, t, 0.3f, 0.08f);
                    if (Rnd() < 0.12f * fx.intensity) fx.emit(x + (Rnd() - 0.5f) * 0.8f, by + 0.3f, z + (Rnd() - 0.5f) * 0.8f, 0.9f, 0xfff0b0, shape: SHAPE.star, vy: 0.8f, c1: 0xffb040, s0: 0.12f, s1: 0.03f);
                }
                // Hinweis über dem Turm: Spezialisierung wählbar und bezahlbar (goldener Stern), Fähigkeit bereit (cyan Ring)
                if (inst.tower != null || inst.fallback)
                {
                    bool canSpec = def.specs.Count == 2 && tw.spec == 0 && def.legendary != true && EconomySystem.nextUpgradeCost(tw) == null
                        && s.gold >= Math.Min(EconomySystem.specCost(tw, 1), EconomySystem.specCost(tw, 2));
                    float realTopH = inst.fallback ? top * scale : inst.nativeH * inst.tr.localScale.x;
                    if (canSpec)
                    {
                        float pz = 0.5f + 0.5f * Mathf.Sin(t * 4 + (float)tw.uid);
                        float sy2 = by + realTopH + 0.42f + Mathf.Sin(t * 2.5f + (float)tw.uid) * 0.06f;
                        r.billboard(SPRITE_ADD, SHAPE.glow, x, sy2, z, 0.75f + pz * 0.2f, 0xffb030, 0.45f + pz * 0.25f);
                        r.billboard(false, SHAPE.star, x, sy2, z, 0.34f + pz * 0.05f, 0xffd040, 1f, t * 0.8f);
                        r.ground(SPRITE_ADD, SHAPE.ring, x, by + 0.04f, z, 0.55f + pz * 0.08f, 0xffb030, 0.55f + pz * 0.3f, -t, 0.3f, 0.07f);
                    }
                    else if (def.active != null && !activeOn && EconomySystem.activeState(sim, tw).available)
                    {
                        float pz = 0.5f + 0.5f * Mathf.Sin(t * 5 + (float)tw.uid);
                        r.ground(SPRITE_ADD, SHAPE.ring, x, by + 0.04f, z, 0.5f + pz * 0.06f, 0x60e0ff, 0.45f + pz * 0.35f, t, 0.3f, 0.06f);
                        r.billboard(SPRITE_ADD, SHAPE.spark, x, by + realTopH + 0.3f, z, 0.28f + pz * 0.06f, 0x80f0ff, 0.8f, t * 2);
                    }
                }
                if (activeOn)
                {
                    r.ground(SPRITE_ADD, SHAPE.ring, x, by + 0.04f, z, 0.75f + Mathf.Sin(t * 6) * 0.05f, defColor, 0.8f, -t * 2, 0.3f, 0.14f);
                    r.billboard(SPRITE_ADD, SHAPE.glow, x, by + top * 0.7f, z, 1.1f, defColor, 0.35f);
                }
                if (tw.forged > s.time && Rnd() < 0.25f) fx.sparks(x, by + 0.4f, z, 0xffa040, 1, 0.6f, 0.08f);
                if (hymn && Rnd() < 0.04f) fx.emit(x, by + top, z, 0.8f, 0xffe080, shape: SHAPE.star, vy: 0.6f, s0: 0.12f, s1: 0.04f);
                if (hl != null && hl.Contains(tw.uid))
                    r.ground(SPRITE_ADD, SHAPE.ring, x, by + 0.05f, z, 0.62f * tsz, 0xffd860, 0.6f + Mathf.Sin(t * 6) * 0.3f, 0, 0.3f, 0.14f);
                // Turmspezifische Belebung
                switch (tw.def)
                {
                    case "phoenix":
                    case "dragon":
                    {
                        float o = (float)tw.orbit;
                        float dx = x + Mathf.Cos(o) * 0.9f;
                        float dz = z + Mathf.Sin(o) * 0.9f;
                        float dy = by + top + 0.5f + Mathf.Sin(t * 3 + (float)tw.uid) * 0.1f;
                        float yaw = Mathf.Atan2(-Mathf.Cos(o), -Mathf.Sin(o));
                        drawParts(WorldModels.DRAGON, dx, dy, dz, yaw, 0.55f * scale, main, tw.spec == 2 ? 0x80c8ff : 0xffa040, "opaque", 1, -1, 0, 0, (float)tw.uid);
                        break;
                    }
                    case "gravity":
                        r.ground(false, SHAPE.disc, x, by + 0.03f, z, 0.6f, 0x100820, 0.35f, 0, 0.6f);
                        if (Rnd() < 0.2f * fx.intensity)
                        {
                            float a = Rnd() * Mathf.PI * 2;
                            fx.emit(x + Mathf.Cos(a) * 0.9f, by + 0.6f, z + Mathf.Sin(a) * 0.9f, 0.55f, 0xa080ff, vx: -Mathf.Cos(a) * 1.5f, vz: -Mathf.Sin(a) * 1.5f, c1: 0x200840, s0: 0.1f, s1: 0.03f);
                        }
                        break;
                    case "totem":
                    {
                        sim.c.towerStats.TryGetValue(tw.uid, out var st);
                        float range = (float)StatOf(st, "range");
                        if (range > 0) r.ground(SPRITE_ADD, SHAPE.ring, x, by + 0.03f, z, range, 0xffe080, 0.12f + Mathf.Sin(t * 2) * 0.05f, 0, 0.3f, 0.02f);
                        break;
                    }
                    case "meteor":
                    case "starfall":
                        if (Rnd() < 0.15f * fx.intensity) fx.fire(x, by + top - 0.3f, z, 0.6f, 0xff6020, false);
                        break;
                }
                // Laserstrahl
                if (def.attack == "laser" && tw.target != 0 && tw.charge > 0 && !stunned)
                {
                    sim.c.enemyByUid.TryGetValue(tw.target, out var e);
                    if (e == null) foreach (var q in s.enemies) if (q.uid == tw.target) { e = q; break; }
                    if (e != null && e.alive)
                    {
                        float ey = (float)Terrain.cellTop(s.map, Math.Floor(e.x), Math.Floor(e.y)) + (e.flying ? 1.4f : 0.45f);
                        float wd = 0.035f + (float)tw.charge * 0.07f;
                        float ty = by + top * 0.85f;
                        float ddx = (float)e.x - x;
                        float ddz = (float)e.y - z;
                        float len = Mathf.Sqrt(ddx * ddx + ddz * ddz);
                        if (len < 1e-6f) len = 1;
                        sim.c.towerStats.TryGetValue(tw.uid, out var st);
                        float reach = (st != null && st.ContainsKey("range") ? (float)st["range"] : len) + 0.3f;
                        float lx = x + ddx / len * reach;
                        float lz = z + ddz / len * reach;
                        r.line(x, ty, z, lx, ey, lz, wd * 4, defColor, 0.35f);
                        r.line(x, ty, z, lx, ey, lz, wd, 0xffffff, 0.95f);
                        r.billboard(SPRITE_ADD, SHAPE.glow, (float)e.x, ey, (float)e.y, 0.5f + (float)tw.charge * 0.4f, defColor, 0.8f);
                        r.billboard(SPRITE_ADD, SHAPE.glow, x, ty, z, 0.4f, 0xffffff, 0.7f);
                        if (Rnd() < 0.3f) fx.sparks((float)e.x, ey, (float)e.y, defColor, 1, 0.6f, 0.08f);
                    }
                }
                if (stunned)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        float a = t * 4 + k * 2.09f;
                        r.billboard(SPRITE_ADD, SHAPE.star, x + Mathf.Cos(a) * 0.3f, by + top + 0.2f, z + Mathf.Sin(a) * 0.3f, 0.16f, 0xffd040, 0.95f);
                    }
                }
                if (tw.disrupted > 0)
                {
                    r.overlayBox(x, by + top + 0.35f, z, 0.36f, 0.08f, 0xff4040, 1, Mathf.PI / 4);
                    r.overlayBox(x, by + top + 0.35f, z, 0.36f, 0.08f, 0xff4040, 1, -Mathf.PI / 4);
                }
                if (ui.showRanges && def.attack != "economy" && def.attack != "none" && tw.uid != ui.selectedTower)
                {
                    sim.c.towerStats.TryGetValue(tw.uid, out var st);
                    float range = (float)StatOf(st, "range");
                    if (range > 0) r.ground(SPRITE_ADD, SHAPE.ring, x, by + 0.03f, z, range, defColor, 0.3f, 0, 0.3f, 0.015f);
                }
            }
            // Abgerissene/verkaufte Türme freigeben
            removeBuf.Clear();
            foreach (var kv in towerVis) if (kv.Value.stamp != stamp) removeBuf.Add(kv.Key);
            foreach (var uid in removeBuf)
            {
                var v = towerVis[uid];
                if (v.inst != null) pool.Release(v.inst);
                towerVis.Remove(uid);
            }
        }

        void drawSelection(GameSimulation sim, ViewUiState ui)
        {
            var s = sim.s;
            var map = s.map;
            float t = time;
            TowerInstance sel = null;
            if (ui.selectedTower != 0) sim.c.towerByUid.TryGetValue(ui.selectedTower, out sel);
            if (sel != null)
            {
                sim.c.towerStats.TryGetValue(sel.uid, out var st);
                float by = (float)Terrain.cellTop(map, sel.cx, sel.cy);
                int col = Ci(Towers.TOWERS[sel.def].color);
                float ssz = (float)Towers.towerSize(sel.def);
                float scx = (float)sel.cx + ssz / 2, scz = (float)sel.cy + ssz / 2;
                r.ground(SPRITE_ADD, SHAPE.ring, scx, by + 0.05f, scz, 0.62f * ssz, 0xffffff, 0.9f, t, 0.3f, 0.12f);
                float range = (float)StatOf(st, "range");
                if (range > 0)
                {
                    r.ground(false, SHAPE.disc, scx, by + 0.025f, scz, range, col, 0.1f, 0, 0.06f);
                    r.ground(SPRITE_ADD, SHAPE.ring, scx, by + 0.035f, scz, range, col, 0.8f, 0, 0.3f, 0.025f);
                }
                if (sel.def == "pylon" && st != null) r.ground(SPRITE_ADD, SHAPE.ring, scx, by + 0.04f, scz, (float)StatOf(st, "linkRange"), 0x5ab8ff, 0.7f, 0, 0.3f, 0.02f);
            }

            // Bauvorschau
            string ghostDef = ui.buildDef ?? ui.moveDef;
            if (ghostDef != null && ui.hoverCell != null && Towers.TOWERS.TryGetValue(ghostDef, out var def))
            {
                int hx = (int)ui.hoverCell.x, hy = (int)ui.hoverCell.y;
                bool ok = ui.preview != null && ui.preview.ok;
                float top = (float)Terrain.cellTop(map, hx, hy);
                float gsz = (float)Towers.towerSize(ghostDef);
                float gc = gsz / 2;
                r.ground(false, SHAPE.square, hx + gc, top + 0.03f, hy + gc, (gsz - 0.02f) / 2, ok ? 0x60ff80 : 0xff4040, 0.5f);
                int defColor = Ci(def.color);
                if (previewInst == null || previewId != ghostDef)
                {
                    if (previewInst != null) pool.Release(previewInst);
                    previewInst = pool.Acquire(ghostDef, defColor, PrimitiveType.Cylinder, true, IsoCamera.W(hx + gc, top, hy + gc));
                    previewId = ghostDef;
                    previewInst.tr.rotation = Quaternion.identity;
                }
                // Vorschau in derselben (größeren) Größe wie der gebaute Turm
                float pBase = WorldModels.towerTop(ghostDef) * 0.9f * UNIT * (gsz > 1 ? 1.75f : 1);
                float pH = pBase * 1.18f;
                if (!previewInst.fallback && previewInst.nativeFoot > 0.05f && previewInst.nativeH > 0.05f)
                    pH = Mathf.Clamp(0.88f * gsz * previewInst.nativeH / previewInst.nativeFoot, pBase * 1.18f, pBase * 1.6f);
                previewInst.Place(hx + gc, top, hy + gc, pH, 1, 0.94f * gsz);
                previewInst.Ghost(true, ghostMat, ok ? Col.Mix(defColor, 0xffffff, 0.35) : 0xff3030, 0.55f);
                double baseRange = def.@base.TryGetValue("range", out var br) ? br : 0;
                float range = (float)(baseRange * (map.heights[hy * (int)map.width + hx] >= 2 ? 1.12 : 1) * (sim.c.mods != null ? sim.c.mods.rangeMul : 1));
                if (range > 0)
                {
                    r.ground(false, SHAPE.disc, hx + 0.5f, top + 0.02f, hy + 0.5f, range, defColor, 0.08f, 0, 0.06f);
                    r.ground(SPRITE_ADD, SHAPE.ring, hx + 0.5f, top + 0.03f, hy + 0.5f, range, ok ? 0xffffff : 0xff6060, 0.7f, 0, 0.3f, 0.025f);
                }
                if (ui.fusionRing.HasValue && ui.fusionRing.Value > 0)
                    r.ground(SPRITE_ADD, SHAPE.ring, hx + 0.5f, top + 0.04f, hy + 0.5f, (float)ui.fusionRing.Value, 0xffd84a, 0.75f, t * 0.5f, 0.3f, 0.03f);
                if (ghostDef == "pylon")
                {
                    float lr = def.@base.TryGetValue("linkRange", out var lrv) ? (float)lrv : 3;
                    r.ground(SPRITE_ADD, SHAPE.ring, hx + 0.5f, top + 0.035f, hy + 0.5f, lr, 0x5ab8ff, 0.7f, 0, 0.3f, 0.02f);
                }
                var route = ui.preview != null ? ui.preview.route : null;
                if (route != null && route.Count > 1)
                {
                    for (int i = 0; i < route.Count - 1; i++)
                    {
                        var a = route[i];
                        var b = route[i + 1];
                        r.line((float)a.x + 0.5f, (float)Terrain.cellTop(map, a.x, a.y) + 0.1f, (float)a.y + 0.5f, (float)b.x + 0.5f, (float)Terrain.cellTop(map, b.x, b.y) + 0.1f, (float)b.y + 0.5f, 0.06f, ok ? 0xfff0a0 : 0xff6060, 0.9f, true, true);
                    }
                }
            }
            else if (previewInst != null)
            {
                pool.Release(previewInst);
                previewInst = null;
                previewId = null;
            }

            // Heldenplatzierung
            if (ui.heroPlacing != null && ui.hoverCell != null && Heroes.HEROES.TryGetValue(ui.heroPlacing, out var hdef))
            {
                int hx = (int)ui.hoverCell.x, hy = (int)ui.hoverCell.y;
                bool ok = HeroSystem.canPlaceHero(sim, hx, hy).ok;
                float top = (float)Terrain.cellTop(map, hx, hy);
                int acc = Ci(hdef.accent);
                r.ground(SPRITE_ADD, SHAPE.ring, hx + 0.5f, top + 0.04f, hy + 0.5f, 0.55f, ok ? acc : 0xff4040, 0.9f, t, 0.3f, 0.15f);
                string mid = WorldModels.heroModel(ui.heroPlacing);
                if (heroPreviewInst == null || heroPreviewId != mid)
                {
                    if (heroPreviewInst != null) pool.Release(heroPreviewInst);
                    heroPreviewInst = pool.Acquire(mid, Ci(hdef.color), PrimitiveType.Capsule, true, IsoCamera.W(hx + 0.5f, top, hy + 0.5f));
                    heroPreviewId = mid;
                }
                heroPreviewInst.SetHeading(Mathf.PI / 4);
                heroPreviewInst.Place(hx + 0.5f, top, hy + 0.5f, 1.1f * 0.95f * UNIT);
                heroPreviewInst.Ghost(true, ghostMat, ok ? Col.Mix(Ci(hdef.color), 0xffffff, 0.3) : 0xff3030, 0.55f);
                float st = (float)heroStatsOfDef(ui.heroPlacing);
                if (st > 0) r.ground(SPRITE_ADD, SHAPE.ring, hx + 0.5f, top + 0.04f, hy + 0.5f, st, acc, 0.5f, 0, 0.3f, 0.02f);
            }
            else if (heroPreviewInst != null)
            {
                pool.Release(heroPreviewInst);
                heroPreviewInst = null;
                heroPreviewId = null;
            }
            // Zielvorschau für Heldenfähigkeiten
            if (ui.abilityRadius.HasValue && ui.abilityRadius.Value > 0 && ui.hoverCell != null)
            {
                int hx = (int)ui.hoverCell.x, hy = (int)ui.hoverCell.y;
                float top = (float)Terrain.cellTop(map, hx, hy);
                float ar = (float)ui.abilityRadius.Value;
                r.ground(SPRITE_ADD, SHAPE.ring, hx + 0.5f, top + 0.05f, hy + 0.5f, ar, 0xffd860, 0.85f, 0, 0.3f, 0.04f);
                r.ground(false, SHAPE.disc, hx + 0.5f, top + 0.04f, hy + 0.5f, ar, 0xffd860, 0.12f, 0, 0.2f);
            }
        }

        // ------------------------------------------------------------ Zonen

        static float ZoneWindup(string kind)
        {
            for (int i = 0; i < ZONE_WINDUP_KINDS.Length; i++)
                if (ZONE_WINDUP_KINDS[i] == kind)
                    return Enemies.ABILITIES.TryGetValue(ZONE_WINDUP_ABIL[i], out var a) ? (float)a.windup : 1.5f;
            return 1.5f;
        }

        void drawZones(GameSimulation sim)
        {
            float t = time;
            var map = sim.s.map;
            foreach (var z in sim.s.zones)
            {
                float zx = (float)z.x, zz = (float)z.y, zr = (float)z.r;
                float y = (float)Terrain.cellTop(map, Math.Floor(z.x), Math.Floor(z.y)) + 0.04f;
                if (z.kind == "mine")
                {
                    sim.c.towerByUid.TryGetValue(z.link, out var tw);
                    bool spore = tw != null && tw.def == "sporemine";
                    int body = spore ? 0x6a9a3a : 0x3a3a34;
                    int lamp = spore ? 0xb0ff60 : 0xff4030;
                    if (z.armed > 0)
                    {
                        // Wurfbahn vom Turm zur Landestelle
                        if (tw == null) continue;
                        float tsz = (float)Towers.towerSize(tw.def);
                        float tcx = (float)tw.cx + tsz / 2, tcz = (float)tw.cy + tsz / 2;
                        const float total = 0.55f;
                        float p = Mathf.Clamp01(1 - (float)z.armed / total);
                        float sy = (float)Terrain.cellTop(map, tw.cx, tw.cy) + WorldModels.towerTop(tw.def) * 0.9f * UNIT;
                        float mx = tcx + (zx - tcx) * p;
                        float mz = tcz + (zz - tcz) * p;
                        float my = sy + (y - sy) * p + Mathf.Sin(p * Mathf.PI) * 1.6f;
                        r.add("opaque", "sphere", mx, my, mz, 0.2f, 0.14f, 0.2f, body, 1, 0, t * 8, 0);
                        r.ground(false, SHAPE.disc, zx, y, zz, 0.25f * p, 0x000000, 0.25f, 0, 0.6f);
                        continue;
                    }
                    if (z.t <= 0) continue;
                    r.add("opaque", "cyl8", zx, y - 0.02f, zz, 0.3f, 0.07f, 0.3f, body);
                    r.add("opaque", "sphere", zx, y + 0.06f, zz, 0.18f, 0.08f, 0.18f, spore ? 0x8ad040 : 0x55554c);
                    bool blink = Mathf.Sin(t * 5 + (float)z.uid) > 0.4f;
                    r.add("opaque", "sphere", zx, y + 0.11f, zz, 0.06f, 0.06f, 0.06f, blink ? lamp : 0x301810, 1, blink ? 0.8f : 0);
                    if (blink) r.billboard(SPRITE_ADD, SHAPE.glow, zx, y + 0.12f, zz, 0.22f, lamp, 0.35f);
                    continue;
                }
                if (z.kind == "meteor" && z.armed > 0)
                {
                    // fallender Meteor mit Feuerschweif und Zielmarkierung
                    float hh = Mathf.Min(9, (float)z.armed * 7);
                    float mx = zx - hh * 0.35f;
                    float mz = zz - hh * 0.2f;
                    float my = y + hh;
                    bool big = z.r >= 1.5;
                    float size = big ? 0.55f : 0.32f;
                    r.add("opaque", "gem", mx, my, mz, size, size, size, 0x4a2a20, 1, 0.4f, t * 5, t * 3);
                    r.billboard(SPRITE_ADD, SHAPE.glow, mx, my, mz, size * 3, 0xff7a20, 0.9f);
                    if (Rnd() < 0.8f * fx.intensity)
                    {
                        fx.emit(mx, my, mz, 0.4f, 0xffd080, vx: 0.3f, vy: 0.6f, vz: 0.2f, c1: 0xc02000, s0: size * 1.6f, s1: 0.1f);
                        if (Rnd() < 0.4f) fx.smoke(mx, my, mz, size * 1.5f, 0x3a3030, 0.35f, 0.9f);
                    }
                    float p = 1 - Mathf.Min(1, (float)z.armed / 1.2f);
                    r.ground(SPRITE_ADD, SHAPE.ring, zx, y, zz, zr, 0xff5020, 0.5f + p * 0.4f, t, 0.3f, 0.08f);
                    r.ground(false, SHAPE.disc, zx, y, zz, zr * Mathf.Max(0.1f, p), 0xff3010, 0.2f, 0, 0.5f);
                    continue;
                }
                if (z.armed > 0)
                {
                    float blink = 0.35f + 0.25f * Mathf.Sin(t * 12);
                    int col = z.kind == "nullPulse" ? 0x40e0ff : z.kind == "riftJump" || (z.kind != null && z.kind.StartsWith("portal")) ? 0xd060ff : z.kind == "devour" ? 0xa02050 : 0xff3a20;
                    r.ground(false, SHAPE.disc, zx, y, zz, zr, col, blink * 0.5f, 0, 0.15f);
                    r.ground(SPRITE_ADD, SHAPE.ring, zx, y + 0.01f, zz, zr, col, 0.95f, 0, 0.3f, 0.06f);
                    float total = ZoneWindup(z.kind);
                    float p = 1 - Mathf.Max(0, (float)z.armed) / total;
                    r.ground(SPRITE_ADD, SHAPE.ring, zx, y + 0.02f, zz, Mathf.Max(0.05f, zr * p), 0xffffff, 0.9f, 0, 0.3f, 0.08f);
                    continue;
                }
                if (z.t <= 0) continue;
                switch (z.kind)
                {
                    case "poisonCloud":
                        for (int k = 0; k < 4; k++)
                        {
                            float a = k * 1.57f + t * 0.6f;
                            r.billboard(false, SHAPE.smoke, zx + Mathf.Cos(a) * zr * 0.4f, y + 0.35f + Mathf.Sin(t * 2 + k) * 0.06f, zz + Mathf.Sin(a) * zr * 0.4f, zr * 0.75f, 0x8ad040, 0.32f, a, 1, k * 3.1f + (float)z.uid);
                        }
                        r.ground(false, SHAPE.disc, zx, y, zz, zr, 0x6ab020, 0.25f, 0, 0.4f);
                        if (Rnd() < 0.3f * fx.intensity) fx.emit(zx + (Rnd() - 0.5f) * zr, y + 0.1f, zz + (Rnd() - 0.5f) * zr, 0.8f, 0xc0ff60, vy: 0.5f, s0: 0.08f, s1: 0.03f);
                        break;
                    case "fireZone":
                    case "burnTrap":
                    {
                        r.ground(SPRITE_ADD, SHAPE.glow, zx, y, zz, zr * 1.2f, 0xff5010, 0.55f, 0, 0.3f);
                        if (z.kind == "burnTrap") r.ground(SPRITE_ADD, SHAPE.ring, zx, y + 0.01f, zz, zr, 0xffa040, 0.7f, t, 0.3f, 0.06f);
                        int n = Mathf.RoundToInt(zr * 3 * fx.intensity);
                        for (int k = 0; k < n; k++)
                        {
                            if (Rnd() < 0.5f)
                            {
                                float a = Rnd() * Mathf.PI * 2;
                                float d = Mathf.Sqrt(Rnd()) * zr * 0.8f;
                                fx.fire(zx + Mathf.Cos(a) * d, y, zz + Mathf.Sin(a) * d, 0.8f);
                            }
                        }
                        break;
                    }
                    case "portalIn":
                    case "portalOut":
                    {
                        int col = z.kind == "portalIn" ? 0xc060ff : 0xff80ff;
                        r.add("opaque", "torus", zx, y + 0.65f, zz, 1.3f, 1.3f, 1.6f, col, 1, 1, t * 2, 0, Mathf.PI / 2);
                        r.add("transparent", "disc", zx, y + 0.65f, zz, 0.55f, 1, 0.55f, 0x401060, 0.85f, 1, t * 2, 0, Mathf.PI / 2);
                        r.billboard(SPRITE_ADD, SHAPE.glow, zx, y + 0.65f, zz, 1.3f, col, 0.5f);
                        break;
                    }
                    case "leyNode":
                        r.ground(SPRITE_ADD, SHAPE.ring, zx, y, zz, zr, 0xc070ff, 0.5f, t * 0.5f, 0.3f, 0.04f);
                        r.line(zx, y, zz, zx, y + 2.5f, zz, 0.18f, 0xc070ff, 0.35f + Mathf.Sin(t * 4) * 0.1f);
                        r.billboard(SPRITE_ADD, SHAPE.glow, zx, y + 0.3f, zz, 0.8f, 0xc070ff, 0.6f);
                        break;
                }
            }
        }

        // ------------------------------------------------------------ Gegner

        void RightVector(out float rx, out float rz)
        {
            rx = r.camRight.x; rz = r.camRight.z;
            float l = Mathf.Sqrt(rx * rx + rz * rz);
            if (l < 1e-5f) { rx = 1; rz = 0; return; }
            rx /= l; rz /= l;
        }

        void drawEnemies(GameSimulation sim, ViewUiState ui, float dt)
        {
            float k = 1 - Mathf.Exp(-dt * 22);
            RightVector(out float rx, out float rz);
            twins.Clear();
            var map = sim.s.map;
            foreach (var e in sim.s.enemies)
            {
                if (!e.alive) continue;
                if (!smooth.TryGetValue(e.uid, out var sm))
                {
                    sm = smoothPool.Count > 0 ? smoothPool.Pop() : new Smooth();
                    sm.x = (float)e.x; sm.y = (float)e.y; sm.h = Terrain.topAt(map, e.x, e.y); sm.head = 0; sm.speed = 0;
                    sm.def = e.def; sm.boss = e.boss; sm.elite = e.elite; sm.seen = time; sm.inst = null;
                    smooth[e.uid] = sm;
                    if (e.boss != null && !bossSeen.Contains(e.uid))
                    {
                        bossSeen.Add(e.uid);
                        if (!ui.reducedMotion && ui.cinematic != true) cam.Focus((float)e.x, (float)e.y, 0.5f, 3.2f);
                        flash = Mathf.Max(flash, 0.25f);
                        int bc = Enemies.BOSSES.TryGetValue(e.boss, out var bdef) ? Ci(bdef.color) : 0xff6060;
                        fx.pillar((float)e.x, 0, (float)e.y, 8, bc, 1.2f, 0.8f);
                        fx.ring((float)e.x, 0, (float)e.y, 3, bc, 1, 0.2f);
                        onBossAppear?.Invoke(e.boss);
                    }
                }
                sm.stamp = stamp;
                float dx = (float)e.x - sm.x;
                float dz = (float)e.y - sm.y;
                if (dx * dx + dz * dz > 2.5f)
                {
                    sm.x = (float)e.x;
                    sm.y = (float)e.y;
                }
                else
                {
                    sm.x += dx * k;
                    sm.y += dz * k;
                }
                float sp = Mathf.Sqrt(dx * dx + dz * dz) / Mathf.Max(dt, 1e-3f);
                sm.speed += (Mathf.Min(1, sp * 2) - sm.speed) * Mathf.Min(1, dt * 8);
                if (Mathf.Abs(dx) + Mathf.Abs(dz) > 0.002f)
                {
                    float target = Mathf.Atan2(dz, dx);
                    float d = target - sm.head;
                    while (d > Mathf.PI) d -= Mathf.PI * 2;
                    while (d < -Mathf.PI) d += Mathf.PI * 2;
                    sm.head += d * Mathf.Min(1, dt * 10);
                }
                float ground = Terrain.topAt(map, sm.x, sm.y);
                sm.h += (ground - sm.h) * Mathf.Min(1, dt * 10);
                drawEnemy(sim, e, sm, rx, rz, dt);
                if (ui.selectedEnemy.HasValue && ui.selectedEnemy.Value == e.uid)
                {
                    float size = EnemySize(e);
                    float rr = Mathf.Max(0.42f, size * 0.75f);
                    r.ground(SPRITE_ADD, SHAPE.ring, sm.x, sm.h + 0.06f, sm.y, rr, 0xff6060, 0.95f, time * 2, 0.3f, 0.12f);
                    r.billboard(SPRITE_ADD, SHAPE.star, sm.x, sm.h + size * 1.6f + 0.6f + Mathf.Sin(time * 5) * 0.06f, sm.y, 0.22f, 0xff8080, 0.9f, time);
                }
                if (e.boss == "twinSol" || e.boss == "twinLuna") twins.Add(e);
            }
            removeBuf.Clear();
            foreach (var kv in smooth) if (kv.Value.stamp != stamp) removeBuf.Add(kv.Key);
            foreach (var uid in removeBuf)
            {
                var s = smooth[uid];
                if (s.inst != null) pool.Release(s.inst);
                s.inst = null;
                smooth.Remove(uid);
                smoothPool.Push(s);
            }
            // Zwillingsband
            if (twins.Count == 2)
            {
                var a = twins[0];
                var b = twins[1];
                if ((a.x - b.x) * (a.x - b.x) + (a.y - b.y) * (a.y - b.y) <= 9 && smooth.TryGetValue(a.uid, out var sa) && smooth.TryGetValue(b.uid, out var sb))
                {
                    var pts = Fx.jagShared(sa.x, sa.h + 1.6f, sa.y, sb.x, sb.h + 1.6f, sb.y, out int n);
                    for (int i = 0; i + 5 < n; i += 3)
                    {
                        r.line(pts[i], pts[i + 1], pts[i + 2], pts[i + 3], pts[i + 4], pts[i + 5], 0.18f, 0xffe0a0, 0.3f);
                        r.line(pts[i], pts[i + 1], pts[i + 2], pts[i + 3], pts[i + 4], pts[i + 5], 0.04f, 0xffffff, 0.8f);
                    }
                }
            }
        }

        static float EnemySize(EnemyInstance e)
        {
            if (e.boss != null && Enemies.BOSSES.TryGetValue(e.boss, out var b)) return (float)b.size;
            return Enemies.ENEMIES.TryGetValue(e.def, out var d) ? (float)d.size * (e.elite ? 1.35f : 1) : 0.5f;
        }

        static bool Has(EnemyInstance e, string id)
        {
            var st = e.statuses;
            for (int i = 0; i < st.Count; i++) if (st[i].id == id) return true;
            return false;
        }

        void drawEnemy(GameSimulation sim, EnemyInstance e, Smooth sm, float rx, float rz, float dt)
        {
            float t = time;
            float yaw = -sm.head;
            bool frozen = Has(e, "freeze");
            bool stunned = Has(e, "stun");
            float moving = frozen || stunned || e.blockedBy != 0 ? 0 : sm.speed;
            float walk = t * (e.boss != null ? 5 : 10) + (float)e.uid;
            float hop = moving * Mathf.Abs(Mathf.Sin(walk)) * 0.04f;
            int tint = -1;
            float tintAmt = 0;
            if (frozen) { tint = 0xd8f4ff; tintAmt = 0.6f; }
            else if (Has(e, "chill")) { tint = 0x80c8ff; tintAmt = 0.35f; }
            else if (Has(e, "poison")) { tint = 0x9ae040; tintAmt = 0.22f; }
            if (e.enraged) { tint = 0xff4020; tintAmt = 0.25f + Mathf.Sin(t * 10) * 0.1f; }
            float emitBoost = 0;
            float squash = 1;
            if (e.hitFlash > 0)
            {
                tint = 0xffffff;
                tintAmt = Mathf.Min(0.7f, (float)e.hitFlash * 4);
                emitBoost = Mathf.Min(0.8f, (float)e.hitFlash * 5);
                squash = 1 - Mathf.Min(0.12f, (float)e.hitFlash * 0.8f);
            }
            bool phased = e.phaseShift > 0;
            bool hidden = e.stealthed;
            string pass = phased || hidden ? "transparent" : "opaque";
            float alpha = hidden ? 0.22f + Mathf.Sin(t * 6 + (float)e.uid) * 0.06f : phased ? 0.38f : 1;
            float height;
            float barW;
            float fly = e.flying ? 1.1f + Mathf.Sin(t * 3 + (float)e.uid) * 0.08f : 0;
            float baseY = sm.h + fly + hop;

            // Modell holen
            string modelId = e.boss ?? e.def;
            int bodyColor = e.boss != null && Enemies.BOSSES.TryGetValue(e.boss, out var bd0) ? Ci(bd0.color) : Enemies.ENEMIES.TryGetValue(e.def, out var ed0) ? Ci(ed0.color) : 0xa08070;
            if (sm.inst == null || sm.inst.id != modelId)
            {
                if (sm.inst != null) pool.Release(sm.inst);
                sm.inst = pool.Acquire(modelId, bodyColor, PrimitiveType.Capsule, true, IsoCamera.W(sm.x, baseY, sm.y));
                sm.inst.SetHeading(sm.head);
            }
            var inst = sm.inst;

            if (e.burrowed)
            {
                inst.SetActive(false);
                // Erdhügel statt Körper
                r.add("opaque", "sphere", sm.x, sm.h, sm.y, 0.6f, 0.28f, 0.6f, 0x6a5a40, 1, 0, t);
                if (Rnd() < dt * 6 * fx.intensity) fx.smoke(sm.x, sm.h + 0.1f, sm.y, 0.35f, 0x7a6a50, 0.45f, 0.7f);
                height = 0.4f;
                barW = 0.6f;
            }
            else if (e.boss != null)
            {
                inst.SetActive(true);
                var b = Enemies.BOSSES[e.boss];
                float sc = (float)b.size * UNIT * (e.challenge == true ? 1.25f : 1);
                int bcol = Ci(b.color);
                if (e.challenge == true)
                {
                    r.ground(SPRITE_ADD, SHAPE.ring, sm.x, sm.h + 0.05f, sm.y, (float)b.size * 1.1f, 0xff3030, 0.7f + Mathf.Sin(t * 5) * 0.2f, t * 2, 0.3f, 0.1f);
                    if (e.chEnraged == true && Rnd() < 0.4f * fx.intensity) fx.fire(sm.x + (Rnd() - 0.5f) * 0.6f, sm.h + 0.3f, sm.y + (Rnd() - 0.5f) * 0.6f, 0.9f);
                }
                ApplyEnemyLook(inst, sm, baseY, WorldModels.bossHeight(e.boss) * sc, squash, hidden || phased, alpha, bcol, tint, tintAmt, emitBoost);
                height = WorldModels.bossHeight(e.boss) * sc + 0.3f;
                barW = 1.8f;
                if (e.shield > 0)
                {
                    r.add("transparent", "sphere", sm.x, sm.h + height * 0.45f, sm.y, height * 1.05f, height * 1.05f, height * 1.05f, e.boss == "nullWarden" ? 0x40e0ff : 0x90b0ff, 0.16f, 1);
                    r.billboard(SPRITE_ADD, SHAPE.ring, sm.x, sm.h + height * 0.45f, sm.y, height * 0.55f, 0x90d0ff, 0.35f, t);
                }
                // Phasenaura
                if (e.phase >= 2)
                {
                    r.ground(SPRITE_ADD, SHAPE.glow, sm.x, sm.h + 0.04f, sm.y, 2.2f + Mathf.Sin(t * 3) * 0.2f, bcol, 0.45f, 0, 0.3f);
                    if (Rnd() < 0.4f * fx.intensity) fx.emit(sm.x + (Rnd() - 0.5f) * 1.6f, sm.h + 0.2f, sm.y + (Rnd() - 0.5f) * 1.6f, 0.8f, Col.Mix(bcol, 0xffffff, 0.4), vy: 1.5f, c1: bcol, s0: 0.16f, s1: 0.03f);
                }
                // Schwachpunkte
                if (e.plates.Count > 0)
                {
                    float cy = Mathf.Cos(yaw), sy = Mathf.Sin(yaw);
                    for (int i = 0; i < WorldModels.WEAKPOINT_POS.Length; i++)
                    {
                        double hp = i < e.plates.Count ? e.plates[i] : 0;
                        if (hp <= 0) continue;
                        var wp = WorldModels.WEAKPOINT_POS[i];
                        float ox = (wp[0] * cy + wp[2] * sy) * sc;
                        float oz = (-wp[0] * sy + wp[2] * cy) * sc;
                        float wy = baseY + wp[1] * sc;
                        float pulse = 1 + Mathf.Sin(t * 5 + i) * 0.12f;
                        r.add("opaque", "gem", sm.x + ox, wy, sm.y + oz, 0.32f * sc * pulse, 0.32f * sc * pulse, 0.32f * sc * pulse, 0xff3030, 1, 1.5f, t * 2);
                        r.billboard(SPRITE_ADD, SHAPE.glow, sm.x + ox, wy, sm.y + oz, 0.7f * sc, 0xff4040, 0.6f * (float)(hp / Math.Max(1, e.plateMax)) + 0.2f);
                    }
                }
                if (e.swallowed.Count > 0) r.billboard(SPRITE_ADD, SHAPE.glow, sm.x, baseY + 0.8f, sm.y, 1.2f, 0xffd060, 0.25f + Mathf.Sin(t * 4) * 0.1f);
                r.ground(false, SHAPE.disc, sm.x, sm.h + 0.03f, sm.y, (float)b.size * 0.9f * UNIT, 0x000000, 0.25f, 0, 0.6f);
            }
            else
            {
                inst.SetActive(true);
                Enemies.ENEMIES.TryGetValue(e.def, out var def);
                float sc = (def != null ? (float)def.size : 0.5f) * 2 * (e.elite ? 1.35f : 1) * UNIT;
                int dcol = def != null ? Ci(def.color) : 0xa08070;
                ApplyEnemyLook(inst, sm, baseY, sc, squash, hidden || phased, alpha, dcol, tint, tintAmt, emitBoost);
                height = (e.flying ? fly : 0) + sc * 0.95f + 0.2f;
                barW = e.elite ? 0.9f : 0.6f;
                if (e.elite)
                {
                    r.add("opaque", "torus", sm.x, baseY + sc * 0.95f + 0.05f, sm.y, 0.36f, 0.36f, 0.36f, 0xffd84a, alpha, 0.9f, t * 2);
                    r.ground(SPRITE_ADD, SHAPE.ring, sm.x, sm.h + 0.03f, sm.y, 0.45f * sc, 0xffd84a, 0.45f, t, 0.3f, 0.12f);
                }
                if (e.mutation != null && Enemies.MUTATIONS.TryGetValue(e.mutation, out var mut))
                {
                    int mc = Ci(mut.color);
                    r.ground(SPRITE_ADD, SHAPE.ring, sm.x, sm.h + 0.035f, sm.y, 0.55f * sc, mc, 0.7f, -t * 2, 0.3f, 0.1f);
                    switch (e.mutation)
                    {
                        case "armored":
                            r.add(pass, "rbox", sm.x, baseY + sc * 0.7f, sm.y, sc * 0.75f, sc * 0.18f, sc * 0.75f, 0x8a90a0, alpha, 0, yaw);
                            break;
                        case "hasty":
                            if (moving > 0.3f && Rnd() < 0.5f * fx.intensity) fx.emit(sm.x, baseY + sc * 0.4f, sm.y, 0.25f, 0xfff080, shape: SHAPE.spark, vx: -Mathf.Cos(sm.head) * 2, vz: -Mathf.Sin(sm.head) * 2, s0: 0.12f, s1: 0.04f, stretch: 0.25f);
                            break;
                        case "explosive":
                            r.billboard(SPRITE_ADD, SHAPE.glow, sm.x, baseY + sc * 0.5f, sm.y, sc * (0.9f + Mathf.Sin(t * 8 + (float)e.uid) * 0.2f), 0xff4020, 0.55f);
                            break;
                        case "splitting":
                            for (int kk = 0; kk < 2; kk++)
                            {
                                float a = t * 3 + kk * Mathf.PI;
                                r.add(pass, "sphereLo", sm.x + Mathf.Cos(a) * sc * 0.6f, baseY + sc * 0.5f, sm.y + Mathf.Sin(a) * sc * 0.6f, sc * 0.22f, sc * 0.22f, sc * 0.22f, mc, alpha, 0.4f);
                            }
                            break;
                        case "vampiric":
                            r.ground(SPRITE_ADD, SHAPE.glow, sm.x, sm.h + 0.04f, sm.y, sc * 1.1f, 0xc01840, 0.4f, 0, 0.3f);
                            break;
                    }
                }
                if (e.shield > 0) r.add("transparent", "sphere", sm.x, baseY + sc * 0.45f, sm.y, sc * 1.25f, sc * 1.25f, sc * 1.25f, 0x90b8ff, 0.2f, 1);
                if (e.flying) r.ground(false, SHAPE.disc, sm.x, sm.h + 0.03f, sm.y, 0.3f * sc, 0x000000, 0.25f, 0, 0.7f);
                if (hidden && Rnd() < dt * 4) fx.emit(sm.x, baseY + sc * 0.5f, sm.y, 0.5f, 0x8070a0, vy: 0.4f, s0: 0.15f, s1: 0.02f, a: 0.4f);
            }

            // Statuseffekte am Körper
            float mid = baseY + Mathf.Min(height, 2.4f) * 0.45f;
            if (Has(e, "burn") && Rnd() < dt * 14 * fx.intensity) fx.fire(sm.x + (Rnd() - 0.5f) * 0.3f, mid - 0.1f, sm.y + (Rnd() - 0.5f) * 0.3f, 0.7f);
            if (Has(e, "poison") && Rnd() < dt * 5 * fx.intensity) fx.emit(sm.x + (Rnd() - 0.5f) * 0.4f, mid, sm.y + (Rnd() - 0.5f) * 0.4f, 0.6f, 0xc0ff60, vy: 0.7f, c1: 0x60a020, s0: 0.1f, s1: 0.03f);
            if (Has(e, "wet") && Rnd() < dt * 4) fx.emit(sm.x + (Rnd() - 0.5f) * 0.3f, mid, sm.y + (Rnd() - 0.5f) * 0.3f, 0.4f, 0x80b0ff, vy: -0.5f, grav: 6, s0: 0.06f, s1: 0.03f, add: false, shape: SHAPE.disc, a: 0.8f);
            if (frozen) r.add("transparent", "rbox", sm.x, sm.h + Mathf.Min(height, 2) * 0.42f, sm.y, 0.85f, Mathf.Min(height, 2) * 0.9f, 0.85f, 0xd8f4ff, 0.35f, 0.5f, yaw);
            if (stunned)
            {
                for (int kk = 0; kk < 3; kk++)
                {
                    float a = t * 5 + kk * 2.09f;
                    r.billboard(SPRITE_ADD, SHAPE.star, sm.x + Mathf.Cos(a) * 0.3f, sm.h + height + 0.05f, sm.y + Mathf.Sin(a) * 0.3f, 0.15f, 0xffff80, 0.95f);
                }
            }
            if (Has(e, "root")) r.add("opaque", "torus", sm.x, sm.h + 0.08f, sm.y, 0.7f, 0.9f, 0.7f, 0x4a8a3a, 1, 0.2f, t);
            bool riftMark = Has(e, "riftMark");
            if (Has(e, "mark") || riftMark) r.ground(SPRITE_ADD, SHAPE.ring, sm.x, sm.h + 0.04f, sm.y, 0.5f, riftMark ? 0xff60ff : 0xc070ff, 0.75f, t * 2, 0.3f, 0.1f);
            if (Has(e, "voidMark")) r.billboard(SPRITE_ADD, SHAPE.glow, sm.x, mid, sm.y, 0.9f, 0x8a40ff, 0.4f + Mathf.Sin(t * 6) * 0.1f);
            if (Has(e, "pull") && Rnd() < dt * 8) fx.emit(sm.x, mid, sm.y, 0.3f, 0xa080ff, vx: (Rnd() - 0.5f) * 2, vz: (Rnd() - 0.5f) * 2, s0: 0.1f, s1: 0.02f);

            // Fähigkeiten-Ankündigung
            foreach (var a in e.abilities)
            {
                if (a.windup <= 0) continue;
                float total = Enemies.ABILITIES.TryGetValue(a.id, out var ad) ? (float)ad.windup : 1;
                float p = 1 - (float)a.windup / Mathf.Max(0.001f, total);
                r.ground(SPRITE_ADD, SHAPE.ring, sm.x, sm.h + 0.06f, sm.y, 0.8f * (1 - p) + 0.35f, 0xffa040, 0.95f, 0, 0.3f, 0.12f);
                r.billboard(SPRITE_ADD, SHAPE.glow, sm.x, mid, sm.y, 0.8f + p * 0.6f, 0xffa040, 0.3f + p * 0.3f);
                if (a.id == "netBite" || a.id == "siegeSlam" || a.id == "devour")
                    r.line(sm.x, sm.h + 0.5f, sm.y, (float)a.tx, sm.h + 0.6f, (float)a.ty, 0.05f, 0xff6040, 0.8f, true, true);
            }

            // Lebensbalken (Overlay)
            float by = sm.h + height + 0.15f;
            float f = Mathf.Max(0, (float)(e.hp / Math.Max(1e-9, e.maxHp)));
            float bx = sm.x;
            float bz = sm.y;
            r.overlayBox(bx, by, bz, barW + 0.05f, 0.11f, 0x101010, 0.85f);
            float fw = barW * f;
            float off = -(barW - fw) / 2;
            int col = e.boss != null ? 0xff5050 : f > 0.6f ? 0x60e060 : f > 0.3f ? 0xf0d040 : 0xf04040;
            r.overlayBox(bx + rx * off, by, bz + rz * off, Mathf.Max(0.001f, fw), 0.075f, col, 1);
            if (e.maxShield > 0 && e.shield > 0)
            {
                float sf = (float)(e.shield / e.maxShield);
                float sw = barW * sf;
                float so = -(barW - sw) / 2;
                r.overlayBox(bx + rx * so, by + 0.095f, bz + rz * so, Mathf.Max(0.001f, sw), 0.05f, 0x70c8ff, 1);
            }
            // Status-Symbole (Form + Farbe)
            int n = 0;
            foreach (var st in e.statuses)
            {
                if (st.id == null || !Palette.STATUS_GLYPH.TryGetValue(st.id, out var g)) continue;
                float off2 = -barW / 2 + 0.08f + n * 0.17f;
                r.overlayGlyph(g.mesh, bx + rx * off2, by + 0.2f, bz + rz * off2, 0.13f, (float)g.sy, Ci(g.color));
                n++;
                if (n >= 6) break;
            }
            if (e.def == "shield" && e.shield > 0)
            {
                int kk = 0;
                foreach (var kv in e.adapt)
                {
                    for (int i = 0; i < kv.Value; i++)
                    {
                        float o = barW / 2 - 0.06f - kk * 0.1f;
                        r.overlayGlyph("box", bx + rx * o, by - 0.12f, bz + rz * o, 0.08f, 1, Palette.damageColor(kv.Key));
                        kk++;
                    }
                }
            }
        }

        /// <summary>Gegnermodell setzen: Position, Größe (Zielhöhe), Ausrichtung, Tarnung/Phase als Geisterbild, Farbstich.</summary>
        void ApplyEnemyLook(ModelInst inst, Smooth sm, float baseY, float height, float squash, bool ghost, float alpha, int bodyColor, int tint, float tintAmt, float emit)
        {
            inst.Place(sm.x, baseY, sm.y, height, squash, sm.boss == null ? (sm.elite ? 1.1f : 0.92f) : 0);
            if (inst.unit == null) inst.SetHeading(sm.head);
            else inst.unit.FaceTowards(inst.tr.position + new Vector3(Mathf.Cos(sm.head), 0, -Mathf.Sin(sm.head)));
            if (ghost)
            {
                inst.Ghost(true, ghostMat, tint >= 0 ? Col.Mix(bodyColor, tint, tintAmt) : Col.Mix(bodyColor, 0x8070a0, 0.4), alpha);
            }
            else
            {
                inst.Ghost(false, null, 0, 0);
                if (tint >= 0 && tintAmt > 0) inst.Tint(tint, tintAmt, emit);
                else inst.Tint(0xffffff, 0, emit);
            }
        }

        void drawCorpses(float dt)
        {
            int w = 0;
            for (int i = 0; i < corpses.Count; i++)
            {
                var c = corpses[i];
                c.t += dt;
                if (c.t >= c.hold + c.max)
                {
                    if (c.inst != null) pool.Release(c.inst);
                    continue;
                }
                float p = Mathf.Max(0, c.t - c.hold) / c.max;
                float sink = p * (c.boss ? 1.2f : 0.35f);
                if (c.fall > 0) { float q = Mathf.Min(1, c.t / 0.45f); sink += c.fall * q * q; }
                if (c.inst != null)
                {
                    c.inst.Place(c.x, c.y - sink, c.z, c.height * (1 - p * 0.25f), 1 - p * 0.4f);
                    c.inst.Tint(0xffffff, 0.3f + p * 0.5f, 0.6f * (1 - p));
                }
                if (p > 0 && Rnd() < 0.6f * fx.intensity)
                    fx.emit(c.x + (Rnd() - 0.5f) * 0.5f * c.scale, c.y + Rnd() * c.scale, c.z + (Rnd() - 0.5f) * 0.5f * c.scale, 0.5f, Col.Mix(c.color, 0xffffff, 0.5), vy: 1.2f, c1: c.color, s0: 0.1f, s1: 0.02f);
                corpses[w++] = c;
            }
            if (w < corpses.Count) corpses.RemoveRange(w, corpses.Count - w);
        }

        // ------------------------------------------------------------ Helden

        void drawHeroes(GameSimulation sim, ViewUiState ui, float dt)
        {
            float t = time;
            var s = sim.s;
            RightVector(out float rx, out float rz);
            foreach (var h in s.heroes)
            {
                if (h.state != "active") continue;
                if (!heroSmooth.TryGetValue(h.uid, out var sm))
                {
                    sm = new HeroSm { x = (float)h.x, y = (float)h.y, h = Terrain.topAt(s.map, h.x, h.y), speed = 0, born = t, id = h.id };
                    heroSmooth[h.uid] = sm;
                }
                sm.stamp = stamp;
                float dx = (float)h.x - sm.x;
                float dz = (float)h.y - sm.y;
                float k = 1 - Mathf.Exp(-dt * 18);
                if (dx * dx + dz * dz > 4)
                {
                    sm.x = (float)h.x;
                    sm.y = (float)h.y;
                }
                else
                {
                    sm.x += dx * k;
                    sm.y += dz * k;
                }
                float sp = Mathf.Sqrt(dx * dx + dz * dz) / Mathf.Max(dt, 1e-3f);
                sm.speed += (Mathf.Min(1, sp * 2) - sm.speed) * Mathf.Min(1, dt * 8);
                sm.h += (Terrain.topAt(s.map, sm.x, sm.y) - sm.h) * Mathf.Min(1, dt * 10);
                drawHero(sim, h, sm, rx, rz, ui);
            }
            removeBuf.Clear();
            foreach (var kv in heroSmooth) if (kv.Value.stamp != stamp) removeBuf.Add(kv.Key);
            foreach (var uid in removeBuf)
            {
                var hs = heroSmooth[uid];
                if (hs.inst != null) pool.Release(hs.inst);
                heroSmooth.Remove(uid);
            }
        }

        void drawHero(GameSimulation sim, HeroInstance h, HeroSm sm, float rx, float rz, ViewUiState ui)
        {
            float t = time;
            if (!Heroes.HEROES.TryGetValue(h.id, out var def)) return;
            var st = HeroSystem.heroStatsOf(sim, h);
            float appear = Mathf.Min(1, (t - sm.born) / 0.35f);
            float scale = 0.95f * UNIT * (appear < 1 ? easeOutBack(appear) : 1);
            bool invuln = h.buffKind == "bulwark" && h.buffT > 0;
            int acc = Ci(def.accent);
            // Modell
            string mid = WorldModels.heroModel(h.id);
            if (sm.inst == null)
            {
                sm.inst = pool.Acquire(mid, Ci(def.color), PrimitiveType.Capsule, true, IsoCamera.W(sm.x, sm.h, sm.y));
                sm.inst.SetHeading((float)h.facing);
            }
            sm.inst.Place(sm.x, sm.h, sm.y, Mathf.Max(0.05f, 1.1f * scale));
            float fa = (float)h.facing;
            if (sm.inst.unit != null) sm.inst.unit.FaceTowards(IsoCamera.W(sm.x + Mathf.Cos(fa), sm.h, sm.y + Mathf.Sin(fa)));
            else sm.inst.SetHeading(fa);
            if (invuln) sm.inst.Tint(0xffe080, 0.35f, 0.4f);
            else sm.inst.Tint(0xffffff, 0, 0.05f);
            // Heldenkennzeichnung: Bodenring
            r.ground(SPRITE_ADD, SHAPE.ring, sm.x, sm.h + 0.04f, sm.y, 0.48f, acc, 0.8f, t, 0.3f, 0.14f);
            r.ground(false, SHAPE.disc, sm.x, sm.h + 0.03f, sm.y, 0.42f, 0x000000, 0.25f, 0, 0.6f);
            bool selected = ui.selectedHero.HasValue && ui.selectedHero.Value == h.uid;
            float aura = (float)st.auraRadius;
            if (aura > 0 && (def.support || selected))
            {
                r.ground(SPRITE_ADD, SHAPE.ring, sm.x, sm.h + 0.035f, sm.y, aura, acc, selected ? 0.55f : 0.28f, 0, 0.3f, 0.02f);
                r.ground(SPRITE_ADD, SHAPE.disc, sm.x, sm.h + 0.03f, sm.y, aura, acc, 0.05f + Mathf.Sin(t * 2) * 0.02f, 0, 0.5f);
            }
            if (def.support && Rnd() < 0.12f * fx.intensity)
            {
                float a = Rnd() * Mathf.PI * 2;
                float d = Rnd() * Mathf.Max(0.5f, aura);
                fx.emit(sm.x + Mathf.Cos(a) * d, sm.h + 0.2f, sm.y + Mathf.Sin(a) * d, 0.9f, acc, shape: SHAPE.star, vy: 0.6f, s0: 0.1f, s1: 0.03f);
            }
            if (invuln) r.add("transparent", "sphere", sm.x, sm.h + 0.55f, sm.y, 1.3f, 1.3f, 1.3f, 0xffe080, 0.18f, 1);
            if (h.blocking > 0) r.billboard(SPRITE_ADD, SHAPE.ring, sm.x, sm.h + 0.55f, sm.y, 0.6f, 0x9ab8ff, 0.3f, t);
            if (selected)
            {
                r.ground(SPRITE_ADD, SHAPE.ring, sm.x, sm.h + 0.05f, sm.y, 0.62f, 0xffffff, 0.9f, -t, 0.3f, 0.1f);
                // Ankerfahne
                float ax = (float)h.ax, az = (float)h.ay;
                float ay = Terrain.topAt(sim.s.map, h.ax, h.ay);
                r.add("opaque", "cyl8", ax, ay, az, 0.04f, 0.8f, 0.04f, 0xd0c8b8);
                r.add("opaque", "leaf", ax, ay + 0.72f, az, 0.3f, 1, 0.35f, acc, 1, 0.5f, Mathf.PI / 2, 0, Mathf.PI / 2 + Mathf.Sin(t * 4) * 0.2f);
                r.ground(SPRITE_ADD, SHAPE.ring, ax, ay + 0.04f, az, 0.35f, acc, 0.8f, t, 0.3f, 0.15f);
                if ((ax - sm.x) * (ax - sm.x) + (az - sm.y) * (az - sm.y) > 0.3f) r.line(sm.x, sm.h + 0.1f, sm.y, ax, ay + 0.1f, az, 0.03f, acc, 0.5f);
            }
            if (h.pendingPerk) r.billboard(SPRITE_ADD, SHAPE.star, sm.x, sm.h + 1.55f + Mathf.Sin(t * 5) * 0.08f, sm.y, 0.32f, 0xffd860, 1, t, 1, 0, true);
            // Balken: LP (Heldenfarbe) + Einsatzzeit
            float by = sm.h + 1.3f * UNIT + 0.1f;
            const float barW = 0.8f;
            r.overlayBox(sm.x, by, sm.y, barW + 0.05f, 0.17f, 0x101010, 0.85f);
            float f = Mathf.Max(0, (float)(h.hp / Math.Max(1, st.maxHp)));
            float fw = barW * f;
            float off = -(barW - fw) / 2;
            r.overlayBox(sm.x + rx * off, by + 0.03f, sm.y + rz * off, Mathf.Max(0.001f, fw), 0.07f, f > 0.35f ? 0x60e0a0 : 0xff6060, 1);
            float tf = Mathf.Clamp01((float)(h.activeLeft / Math.Max(1, st.activeTime)));
            float tw = barW * tf;
            float toff = -(barW - tw) / 2;
            r.overlayBox(sm.x + rx * toff, by - 0.045f, sm.y + rz * toff, Mathf.Max(0.001f, tw), 0.035f, 0x80b8ff, 1);
            for (int i = 0; i < h.level; i++)
            {
                float o = -barW / 2 + 0.06f + i * 0.13f;
                r.overlayGlyph("octa", sm.x + rx * o, by + 0.17f, sm.y + rz * o, 0.09f, 0.12f / 0.09f, 0xffd84a);
            }
        }

        // ------------------------------------------------------------ Helden-Endlos: Held, Laden, Beute, Zonen

        readonly HashSet<double> buffed = new HashSet<double>();

        void drawHeroEndless(GameSimulation sim, ViewUiState ui, float dt)
        {
            var he = sim.s.he;
            if (he == null) return;
            float t = time;
            var map = sim.s.map;
            var c = he.champion;
            if (c == null || !Champions.CHAMPIONS.TryGetValue(c.id, out var def)) return;
            int acc = Ci(def.accent);
            // Laden (Gebäude auf der Ladenzelle)
            if (he.shop != null)
            {
                float x = (float)he.shop.x + 0.5f;
                float z = (float)he.shop.y + 0.5f;
                float y = (float)Terrain.cellTop(map, he.shop.x, he.shop.y);
                if (shopInst == null && ModelPool.Has("shop"))
                    shopInst = pool.Acquire("shop", at: IsoCamera.W(x, y, z));
                if (shopInst != null)
                {
                    shopInst.Place(x, y, z, 1.45f);
                    r.add("opaque", "gem", x, y + 1.58f + Mathf.Sin(t * 2) * 0.06f, z, 0.14f, 0.22f, 0.14f, 0xffd860, 1, 0.5f, t);
                }
                else
                {
                    r.add("opaque", "rbox", x, y + 0.02f, z, 0.96f, 0.14f, 0.96f, 0x5a4a3a);
                    r.add("opaque", "rbox", x, y + 0.16f + 0.29f, z, 0.8f, 0.58f, 0.7f, 0x9a7650);
                    r.add("opaque", "box", x, y + 0.5f, z + 0.36f, 0.86f, 0.06f, 0.24f, 0xc84a3a, 1, 0, 0, -0.5f);
                    r.add("opaque", "cone4", x, y + 0.74f, z, 0.72f, 0.5f, 0.72f, 0x7a3a2a, 1, 0, Mathf.PI / 4);
                    r.add("opaque", "box", x, y + 0.24f, z + 0.36f, 0.26f, 0.34f, 0.03f, 0x302018);
                    r.add("opaque", "rbox", x - 0.28f, y + 0.16f, z + 0.44f, 0.18f, 0.18f, 0.18f, 0x7a5a38);
                    r.add("opaque", "sphereLo", x + 0.3f, y + 0.24f, z + 0.46f, 0.12f, 0.16f, 0.12f, 0x60c0ff, 1, 0.5f);
                    r.add("opaque", "cyl8", x + 0.46f, y + 0.14f, z - 0.3f, 0.03f, 1.1f, 0.03f, 0xd0c8b8);
                    r.add("opaque", "leaf", x + 0.46f, y + 1.1f, z - 0.3f, 0.3f, 1, 0.34f, 0xffd860, 1, 0.3f, Mathf.PI / 2, 0, Mathf.PI / 2 + Mathf.Sin(t * 3) * 0.25f);
                    r.add("opaque", "gem", x, y + 1.38f + Mathf.Sin(t * 2) * 0.06f, z, 0.14f, 0.22f, 0.14f, 0xffd860, 1, 0.5f, t);
                }
                bool inRange = (c.x - x) * (c.x - x) + (c.y - z) * (c.y - z) <= 2.2 * 2.2 && c.dead <= 0;
                r.ground(SPRITE_ADD, SHAPE.ring, x, y + 0.04f, z, 2.2f, 0xffd860, inRange ? 0.55f : 0.18f, t * 0.3f, 0.3f, 0.015f);
                if (inRange) r.ground(SPRITE_ADD, SHAPE.disc, x, y + 0.03f, z, 2.2f, 0xffd860, 0.06f, 0, 0.5f);
            }
            // Verstärkte Türme: kreisende Runen über dem Turm
            buffed.Clear();
            foreach (var b in he.towerBuffs) buffed.Add(b.tower);
            foreach (var kv in sim.c.champBuff) if (kv.Value.dmg + kv.Value.speed + kv.Value.range + kv.Value.crit > 0.001) buffed.Add(kv.Key);
            foreach (var uid in buffed)
            {
                if (!sim.c.towerByUid.TryGetValue(uid, out var tw)) continue;
                float tsz = (float)Towers.towerSize(tw.def);
                float x = (float)tw.cx + tsz / 2, z = (float)tw.cy + tsz / 2;
                float y = (float)Terrain.cellTop(map, tw.cx, tw.cy);
                bool strong = false;
                foreach (var b in he.towerBuffs) if (b.tower == uid) { strong = true; break; }
                r.ground(SPRITE_ADD, SHAPE.ring, x, y + 0.06f, z, 0.58f, acc, strong ? 0.75f : 0.35f, -t * 1.5f, 0.3f, 0.1f);
                if (strong)
                {
                    for (int kk = 0; kk < 3; kk++)
                    {
                        float a = t * 2 + kk * Mathf.PI * 2 / 3;
                        r.billboard(SPRITE_ADD, SHAPE.star, x + Mathf.Cos(a) * 0.42f, y + 1.25f + Mathf.Sin(t * 3 + kk) * 0.08f, z + Mathf.Sin(a) * 0.42f, 0.16f, acc, 0.9f, t);
                    }
                }
            }
            // Zonen (Fallen, Pfeilhagel)
            foreach (var zn in he.zones)
            {
                float y = Terrain.topAt(map, zn.x, zn.y);
                int col = zn.kind == "snare" ? 0x80ff90 : 0x9ad8ff;
                float zx = (float)zn.x, zz = (float)zn.y, zr = (float)zn.r;
                r.ground(SPRITE_ADD, SHAPE.ring, zx, y + 0.05f, zz, zr, col, 0.7f, t, 0.3f, 0.05f);
                r.ground(false, SHAPE.disc, zx, y + 0.04f, zz, zr, col, 0.1f, 0, 0.3f);
                if (zn.kind == "volley" && Rnd() < 0.4f * fx.intensity)
                {
                    float a = Rnd() * Mathf.PI * 2;
                    float d = Rnd() * zr;
                    float px = zx + Mathf.Cos(a) * d;
                    float pz = zz + Mathf.Sin(a) * d;
                    fx.beam(px - 0.2f, y + 2.2f, pz - 0.2f, px, y + 0.1f, pz, col, 0.03f, 0.12f);
                }
            }
            // Abbau: Fortschrittsring am Hindernis, Staub und Splitter
            int w = (int)map.width;
            if (he.mining != null)
            {
                var mi = he.mining;
                float mx = (float)(mi.cell % w) + 0.5f;
                float mz = (float)Math.Floor(mi.cell / w) + 0.5f;
                float my = Terrain.topAt(map, mx, mz);
                float p = 1 - Mathf.Max(0, (float)mi.t) / Mathf.Max(0.001f, (float)mi.total);
                r.ground(SPRITE_ADD, SHAPE.ring, mx, my + 0.05f, mz, 0.62f, 0x403020, 0.6f, 0, 0.3f, 0.12f);
                r.ground(SPRITE_ADD, SHAPE.ring, mx, my + 0.06f, mz, 0.62f * Mathf.Max(0.05f, p), 0xffd860, 0.9f, t, 0.3f, 0.12f);
                if (Rnd() < 0.25f * fx.intensity) fx.smoke(mx + (Rnd() - 0.5f) * 0.6f, my + 0.3f, mz + (Rnd() - 0.5f) * 0.6f, 0.3f, 0x9a8a70, 0.35f, 0.8f);
                if (Rnd() < 0.18f * fx.intensity) fx.sparks(mx, my + 0.5f, mz, 0xd0b080, 2, 0.6f, 0.08f);
            }
            else if (he.mineTarget.HasValue && he.mineTarget.Value >= 0)
            {
                float mx = (float)(he.mineTarget.Value % w) + 0.5f;
                float mz = (float)Math.Floor(he.mineTarget.Value / w) + 0.5f;
                r.ground(SPRITE_ADD, SHAPE.ring, mx, Terrain.topAt(map, mx, mz) + 0.05f, mz, 0.6f, 0xffd860, 0.6f + Mathf.Sin(t * 6) * 0.2f, t, 0.3f, 0.08f);
            }
            // Beute am Boden
            foreach (var p in he.pickups)
            {
                float px = (float)p.x, pz = (float)p.y;
                float y = Terrain.topAt(map, p.x, p.y) + 0.3f + Mathf.Sin(t * 3 + (float)p.uid) * 0.06f;
                float fade = p.t < 5 ? 0.4f + 0.6f * Mathf.Abs(Mathf.Sin(t * 6)) : 1;
                int col = 0x80ff9a;
                float size = 0.18f;
                if (p.kind == "resource") col = p.@ref != null && HeroEndless.RESOURCES.TryGetValue(p.@ref, out var rd) ? Ci(rd.color) : 0xffffff;
                else if (p.kind == "fragment" || p.kind == "item")
                {
                    string rar = p.@ref != null && Items.ITEMS.TryGetValue(p.@ref, out var it) ? it.rarity : "common";
                    col = Items.RARITY.TryGetValue(rar ?? "common", out var ra) ? ParseCss(ra.color) : 0xffffff;
                    size = p.kind == "item" ? 0.3f : 0.22f;
                }
                else if (p.kind == "potion") col = p.@ref != null && Items.POTIONS.TryGetValue(p.@ref, out var po) ? Ci(po.color) : 0xff6060;
                if (p.kind == "xp")
                {
                    r.billboard(SPRITE_ADD, SHAPE.glow, px, y, pz, size * (1 + Mathf.Min(1, (float)p.amount / 40)), col, 0.9f * fade, t);
                }
                else
                {
                    r.add("opaque", p.kind == "potion" ? "sphereLo" : "gem", px, y, pz, size * 0.6f, size, size * 0.6f, col, fade, 1.2f, t * 2 + (float)p.uid);
                    r.billboard(SPRITE_ADD, SHAPE.glow, px, y, pz, size * 2.4f, col, 0.45f * fade, t);
                    if (p.kind == "item") r.add("transparent", "cyl8", px, y - 0.3f, pz, 0.12f, 2.2f, 0.12f, col, 0.18f * fade, 1);
                }
            }
            // Held
            if (c.dead > 0)
            {
                if (champSmooth != null && champSmooth.inst != null) pool.Release(champSmooth.inst);
                champSmooth = null;
                var g = map.goal;
                r.billboard(SPRITE_ADD, SHAPE.ring, (float)g.x + 0.5f, (float)Terrain.cellTop(map, g.x, g.y) + 1.4f, (float)g.y + 0.5f, 0.5f, 0x9ad8ff, 0.4f + Mathf.Sin(t * 4) * 0.2f, t);
                return;
            }
            var sm = champSmooth;
            if (sm == null)
            {
                sm = new ChampSm { x = (float)c.x, y = (float)c.y, h = Terrain.topAt(map, c.x, c.y), speed = 0 };
                champSmooth = sm;
            }
            float dx = (float)c.x - sm.x;
            float dz = (float)c.y - sm.y;
            float k = 1 - Mathf.Exp(-dt * 18);
            if (dx * dx + dz * dz > 4)
            {
                sm.x = (float)c.x;
                sm.y = (float)c.y;
            }
            else
            {
                sm.x += dx * k;
                sm.y += dz * k;
            }
            float sp = Mathf.Sqrt(dx * dx + dz * dz) / Mathf.Max(dt, 1e-3f);
            sm.speed += (Mathf.Min(1, sp * 2) - sm.speed) * Mathf.Min(1, dt * 8);
            sm.h += (Terrain.topAt(map, sm.x, sm.y) - sm.h) * Mathf.Min(1, dt * 10);
            var st = ChampionSystem.championStats(he);
            bool storm = c.channel > 0;
            // Modell
            string mid = WorldModels.champModel(c.id);
            if (sm.inst == null || sm.model != mid)
            {
                if (sm.inst != null) pool.Release(sm.inst);
                sm.inst = pool.Acquire(mid, Ci(def.color), PrimitiveType.Capsule, true, IsoCamera.W(sm.x, sm.h, sm.y));
                sm.model = mid;
                sm.inst.SetHeading((float)c.facing);
            }
            sm.inst.Place(sm.x, sm.h, sm.y, 1.1f * 1.05f * UNIT);
            float face = storm ? -t * 14 : (float)c.facing;
            if (sm.inst.unit != null) sm.inst.unit.FaceTowards(IsoCamera.W(sm.x + Mathf.Cos(face), sm.h, sm.y + Mathf.Sin(face)));
            else sm.inst.SetHeading(face);
            sm.inst.Tint(0xffffff, 0, 0.12f);
            r.ground(SPRITE_ADD, SHAPE.ring, sm.x, sm.h + 0.04f, sm.y, 0.5f, acc, 0.9f, t, 0.3f, 0.16f);
            r.ground(false, SHAPE.disc, sm.x, sm.h + 0.03f, sm.y, 0.44f, 0x000000, 0.28f, 0, 0.6f);
            r.billboard(SPRITE_ADD, SHAPE.glow, sm.x, sm.h + 0.6f, sm.y, 1.1f, acc, 0.15f, t);
            if (storm) for (int q = 0; q < 2; q++) r.ground(SPRITE_ADD, SHAPE.ring, sm.x, sm.h + 0.3f + q * 0.15f, sm.y, 1.6f + q * 0.3f, 0xffffff, 0.35f, t * 8, 0.3f, 0.05f);
            bool champSel = ui.champSelected == true;
            float supR = (float)st.supportRadius;
            if (def.aura != null || champSel)
            {
                r.ground(SPRITE_ADD, SHAPE.ring, sm.x, sm.h + 0.035f, sm.y, supR, acc, champSel ? 0.5f : 0.22f, 0, 0.3f, 0.02f);
                r.ground(SPRITE_ADD, SHAPE.disc, sm.x, sm.h + 0.03f, sm.y, supR, acc, 0.04f + Mathf.Sin(t * 2) * 0.015f, 0, 0.5f);
            }
            if (champSel)
            {
                r.ground(SPRITE_ADD, SHAPE.ring, sm.x, sm.h + 0.05f, sm.y, 0.66f, 0xffffff, 0.9f, -t, 0.3f, 0.1f);
                r.ground(SPRITE_ADD, SHAPE.ring, sm.x, sm.h + 0.03f, sm.y, (float)st.pickupRadius, 0x80ff9a, 0.25f, 0, 0.3f, 0.015f);
            }
            if (ui.champCastRange.HasValue && ui.champCastRange.Value > 0) r.ground(SPRITE_ADD, SHAPE.ring, sm.x, sm.h + 0.05f, sm.y, (float)ui.champCastRange.Value, 0xffffff, 0.6f, 0, 0.3f, 0.02f);
            // Laufziel
            if (c.path.Count > 0)
            {
                double last = c.path[c.path.Count - 1];
                float lx = (float)(last % w) + 0.5f;
                float lz = (float)Math.Floor(last / w) + 0.5f;
                float ly = Terrain.topAt(map, lx, lz);
                r.ground(SPRITE_ADD, SHAPE.ring, lx, ly + 0.05f, lz, 0.32f + Mathf.Sin(t * 6) * 0.05f, acc, 0.85f, t * 2, 0.3f, 0.15f);
                float ppx = sm.x, ppz = sm.y, pph = sm.h;
                foreach (var ci in c.path)
                {
                    float nx = (float)(ci % w) + 0.5f;
                    float nz = (float)Math.Floor(ci / w) + 0.5f;
                    float nh = Terrain.topAt(map, nx, nz);
                    r.line(ppx, pph + 0.08f, ppz, nx, nh + 0.08f, nz, 0.03f, acc, 0.35f);
                    ppx = nx;
                    ppz = nz;
                    pph = nh;
                }
            }
            // Balken über dem Helden: LP, Mana
            RightVector(out float rx, out float rz);
            float by = sm.h + 1.45f * UNIT + 0.1f;
            const float barW = 0.95f;
            r.overlayBox(sm.x, by, sm.y, barW + 0.05f, 0.19f, 0x101010, 0.85f);
            float f = Mathf.Max(0, (float)(c.hp / Math.Max(1, st.maxHp)));
            float fw = barW * f;
            r.overlayBox(sm.x + rx * (-(barW - fw) / 2), by + 0.035f, sm.y + rz * (-(barW - fw) / 2), Mathf.Max(0.001f, fw), 0.08f, f > 0.35f ? 0x60e0a0 : 0xff6060, 1);
            float mf = st.maxMana > 0 ? Mathf.Max(0, (float)(c.mana / st.maxMana)) : 0;
            float mw = barW * mf;
            r.overlayBox(sm.x + rx * (-(barW - mw) / 2), by - 0.05f, sm.y + rz * (-(barW - mw) / 2), Mathf.Max(0.001f, mw), 0.04f, 0x5a9aff, 1);
            if (c.skillPoints > 0) r.billboard(SPRITE_ADD, SHAPE.star, sm.x, sm.h + 1.75f * UNIT + 0.15f + Mathf.Sin(t * 5) * 0.08f, sm.y, 0.3f, 0xffd860, 1, t, 1, 0, true);
        }

        static int ParseCss(string css)
        {
            if (string.IsNullOrEmpty(css)) return 0xffffff;
            string s = css[0] == '#' ? css.Substring(1) : css;
            if (s.Length == 3) s = new string(new[] { s[0], s[0], s[1], s[1], s[2], s[2] });
            return int.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out int v) ? v : 0xffffff;
        }

        // ------------------------------------------------------------ Geschosse

        void drawProjectiles(GameSimulation sim)
        {
            var map = sim.s.map;
            foreach (var p in sim.s.projectiles)
            {
                if (!p.alive) continue;
                float px = (float)p.x, pz = (float)p.y;
                float remaining = Mathf.Sqrt((float)((p.tx - p.x) * (p.tx - p.x) + (p.ty - p.y) * (p.ty - p.y)));
                float frac = (float)p.traveled / Mathf.Max(0.001f, (float)p.traveled + remaining);
                float bse = Terrain.topAt(map, p.x, p.y);
                bool lob = p.kind == "ember" || p.kind == "spore" || p.kind == "cannon";
                float y = bse + 0.75f + (lob ? Mathf.Sin(frac * Mathf.PI) * 1.3f : 0.25f * (1 - frac));
                float yaw = Mathf.Atan2(-(float)(p.ty - p.y), (float)(p.tx - p.x));
                int col = Palette.damageColor(p.payload != null ? p.payload.type : null);
                switch (p.kind)
                {
                    case "ranger":
                        r.add("opaque", "rbox", px, y, pz, 0.42f, 0.04f, 0.04f, 0xe8dcc0, 1, 0.3f, yaw);
                        r.billboard(SPRITE_ADD, SHAPE.spark, px, y, pz, 0.18f, 0xfff0d0, 0.4f, 0, 2);
                        break;
                    case "ballista":
                        r.add("opaque", "rbox", px, y, pz, 0.7f, 0.07f, 0.07f, 0xd0c0a0, 1, 0.3f, Mathf.Atan2(-(float)p.dirY, (float)p.dirX));
                        r.billboard(SPRITE_ADD, SHAPE.glow, px, y, pz, 0.3f, 0xffffff, 0.35f);
                        break;
                    case "ember":
                        r.add("opaque", "sphere", px, y, pz, 0.26f, 0.26f, 0.26f, 0xffa040, 1, 1.4f);
                        r.billboard(SPRITE_ADD, SHAPE.glow, px, y, pz, 0.7f, 0xff6020, 0.85f);
                        if (Rnd() < 0.8f * fx.intensity) fx.fire(px, y, pz, 0.6f);
                        break;
                    case "spore":
                        r.add("opaque", "sphere", px, y, pz, 0.22f, 0.22f, 0.22f, 0x9ae040, 1, 0.7f);
                        r.billboard(SPRITE_ADD, SHAPE.glow, px, y, pz, 0.45f, 0x9ae040, 0.5f);
                        if (Rnd() < 0.4f * fx.intensity) fx.emit(px, y, pz, 0.5f, 0xc0ff70, vy: -0.3f, s0: 0.1f, s1: 0.03f);
                        break;
                    case "cannon":
                        r.add("opaque", "sphere", px, y, pz, 0.24f, 0.24f, 0.24f, 0x2a2a2e, 1, 0);
                        if (Rnd() < 0.5f * fx.intensity) fx.smoke(px, y, pz, 0.25f, 0x8a8480, 0.3f, 0.6f);
                        break;
                    default:
                        r.add("opaque", "sphereLo", px, y, pz, 0.2f, 0.2f, 0.2f, col, 1, 1.2f);
                        r.billboard(SPRITE_ADD, SHAPE.glow, px, y, pz, 0.55f, col, 0.8f);
                        if (Rnd() < 0.5f * fx.intensity) fx.emit(px, y, pz, 0.3f, col, s0: 0.2f, s1: 0.03f);
                        break;
                }
            }
        }

        // ------------------------------------------------------------ Wetter

        void drawWeather(GameSimulation sim, float dt, ViewUiState ui)
        {
            var map = mapRef;
            if (map == null) return;
            var biome = Biomes.BIOMES.TryGetValue(map.biome ?? "forest", out var bd) ? bd : Biomes.BIOMES["forest"];
            string ev = sim.s.currentWave != null ? sim.s.currentWave.@event : null;
            drawSunRays(map);
            string kind = biome.weather;
            if (ev == "blizzard") kind = "blizzard";
            else if (ev == "fog") kind = "mist";
            else if (ev == "glowtide" || ev == "sporeBloom") kind = "spores";
            else if (ev == "heatwave") kind = "embers";
            if (kind != ambientKind) { ambientKind = kind; ambientCount = 0; }
            float bse = kind == "blizzard" ? 240 : kind == "snow" ? 140 : kind == "mist" ? 40 : 90;
            int want = Mathf.RoundToInt(bse * (ui.reducedMotion ? 0.4f : 1) * Mathf.Min(1, ParticleCap(ui) / 500) * (float)(map.width * map.height) / 308);
            want = Mathf.Clamp(want, 0, ambient.Length);
            float W = (float)map.width;
            float H = (float)map.height;
            while (ambientCount < want)
            {
                SpawnAmbient(ambient[ambientCount], true, kind, W, H);
                ambientCount++;
            }
            if (ambientCount > want) ambientCount = want;
            float t = time;
            for (int i = 0; i < ambientCount; i++)
            {
                var a = ambient[i];
                a.life -= dt;
                a.x += a.vx * dt;
                a.y += a.vy * dt;
                a.z += a.vz * dt;
                if (kind == "fireflies" || kind == "sparkles" || kind == "spores")
                {
                    a.x += Mathf.Sin(t * 0.7f + a.seed) * dt * 0.3f;
                    a.z += Mathf.Cos(t * 0.6f + a.seed * 1.3f) * dt * 0.3f;
                }
                if (kind == "snow") a.x += Mathf.Sin(t + a.seed) * dt * 0.3f;
                if (a.life <= 0 || a.y < -0.5f || a.x > W + 3 || a.z > H + 3)
                {
                    SpawnAmbient(a, false, kind, W, H);
                    continue;
                }
                float fade = Mathf.Min(1, Mathf.Min(a.life / 0.8f, (a.max - a.life) / 0.8f));
                switch (kind)
                {
                    case "fireflies":
                    {
                        float blink = Mathf.Max(0, Mathf.Sin(t * 2.2f + a.seed * 3));
                        r.billboard(SPRITE_ADD, SHAPE.glow, a.x, a.y, a.z, 0.16f, 0xd8ff80, blink * fade * 0.9f);
                        break;
                    }
                    case "embers":
                        r.billboard(SPRITE_ADD, SHAPE.glow, a.x, a.y, a.z, 0.1f + Mathf.Sin(t * 9 + a.seed) * 0.03f, 0xff8030, fade * 0.9f);
                        break;
                    case "snow":
                    case "blizzard":
                        r.billboard(false, SHAPE.disc, a.x, a.y, a.z, kind == "blizzard" ? 0.06f : 0.05f, 0xffffff, fade * 0.85f, 0, kind == "blizzard" ? 2.2f : 1);
                        break;
                    case "sparkles":
                        r.billboard(SPRITE_ADD, SHAPE.star, a.x, a.y, a.z, 0.12f, 0xfff0d0, fade * Mathf.Max(0, Mathf.Sin(t * 3 + a.seed * 5)) * 0.9f, t);
                        break;
                    case "wind":
                        r.billboard(SPRITE_ADD, SHAPE.spark, a.x, a.y, a.z, 0.35f, 0xffffff, fade * 0.25f, 0.3f, 4);
                        break;
                    case "mist":
                        r.billboard(false, SHAPE.smoke, a.x, a.y, a.z, 2.2f, 0xc8d8c8, fade * 0.16f, a.seed, 1.4f, a.seed);
                        break;
                    case "spores":
                        r.billboard(SPRITE_ADD, SHAPE.glow, a.x, a.y, a.z, 0.12f, ((int)a.seed) % 2 != 0 ? 0x60f0d0 : 0xc080ff, fade * 0.8f);
                        break;
                }
            }
        }

        static void SpawnAmbient(Ambient a, bool fresh, string kind, float W, float H)
        {
            a.x = -2 + Rnd() * (W + 4);
            a.z = -2 + Rnd() * (H + 4);
            a.seed = Rnd() * 100;
            a.max = 3 + Rnd() * 5;
            a.life = fresh ? Rnd() * a.max : a.max;
            switch (kind)
            {
                case "snow":
                case "blizzard":
                    a.y = fresh ? Rnd() * 7 : 7;
                    a.vy = kind == "blizzard" ? -2.2f : -0.7f - Rnd() * 0.4f;
                    a.vx = kind == "blizzard" ? 2.5f : 0.2f;
                    a.vz = kind == "blizzard" ? 0.8f : 0;
                    break;
                case "embers":
                    a.y = Rnd() * 2;
                    a.vy = 0.5f + Rnd() * 0.6f;
                    a.vx = (Rnd() - 0.5f) * 0.3f;
                    a.vz = (Rnd() - 0.5f) * 0.3f;
                    break;
                case "wind":
                    a.y = 0.5f + Rnd() * 3;
                    a.vx = 4 + Rnd() * 3;
                    a.vz = 1.5f;
                    a.vy = 0;
                    break;
                case "mist":
                    a.y = 0.2f + Rnd() * 0.6f;
                    a.vx = 0.15f;
                    a.vz = 0.05f;
                    a.vy = 0;
                    break;
                default:
                    a.y = 0.3f + Rnd() * 2.2f;
                    a.vx = (Rnd() - 0.5f) * 0.3f;
                    a.vz = (Rnd() - 0.5f) * 0.3f;
                    a.vy = kind == "spores" ? 0.15f : 0;
                    break;
            }
        }

        /// <summary>3.13: Lichtstrahlen (God Rays) schräg in Lichtrichtung über der Insel, langsam wandernd.</summary>
        void drawSunRays(MapDefinition map)
        {
            var L = lighting;
            if (L == null || !MOOD.TryGetValue(map.biome ?? "forest", out var m) || m.sun < 0.06) return;
            float t = time;
            var l = L.light;
            int col = Col.Mix((int)m.glow, 0xffffff, 0.35);
            float W = (float)map.width, H = (float)map.height;
            for (int k = 0; k < 7; k++)
            {
                float ph = k * 2.399f;
                float drift = (t * 0.05f + k / 7f) % 1;
                float gx = W * ((0.1f + 0.8f * ((Mathf.Sin(ph) * 0.5f + 0.5f) * 0.6f + drift * 0.4f)) % 1);
                float gz = H * (0.15f + 0.7f * (Mathf.Cos(ph * 1.7f) * 0.5f + 0.5f));
                float a = Mathf.Sin(drift * Mathf.PI) * (0.05f + 0.03f * Mathf.Sin(t * 0.6f + ph));
                const float len = 9;
                r.line(gx, 0, gz, gx + l.x * len, l.y * len, gz + l.z * len, 0.7f + (k % 3) * 0.35f, col, a);
            }
        }

        // ------------------------------------------------------------ Eingabehilfen

        /// <summary>Bildschirmposition (Pixel, oben links) → Zelle (berücksichtigt die Geländehöhe).</summary>
        public GridPos pickCell(double px, double py, MapDefinition map)
        {
            return Picking.pickGridCell(cam, map, px, py);
        }

        /// <summary>Weltposition (exakt) unter dem Zeiger – für Heldenanker.</summary>
        public Vec2 pickPoint(double px, double py, MapDefinition map)
        {
            var c = pickCell(px, py, map);
            if (c == null) return null;
            var hit = cam.ScreenToGround(new Vector2((float)px, (float)py), (float)Terrain.cellTop(map, c.x, c.y));
            return hit.HasValue ? new Vec2(hit.Value.x, hit.Value.z) : new Vec2(c.x + 0.5, c.y + 0.5);
        }

        /// <summary>
        /// Gegner unter dem Zeiger (Bildschirmabstand zum Körper, nicht nur zum Bodenfeld).
        /// Bei Überlappung gewinnt der Gegner, der der Kamera am nächsten ist.
        /// </summary>
        public double pickEnemy(double px, double py, GameSimulation sim)
        {
            double best = 0;
            float bd = float.PositiveInfinity;
            float ppu = (float)pixelsPerUnit();
            foreach (var e in sim.s.enemies)
            {
                if (!e.alive || e.stealthed) continue;
                smooth.TryGetValue(e.uid, out var sm);
                float x = sm != null ? sm.x : (float)e.x;
                float z = sm != null ? sm.y : (float)e.y;
                float h = sm != null ? sm.h : Terrain.topAt(sim.s.map, e.x, e.y);
                float size = EnemySize(e);
                float lift = e.flying ? 1.1f : 0;
                var a = cam.WorldToScreen(new Vector3(x, h + lift, z));
                var b = cam.WorldToScreen(new Vector3(x, h + lift + Mathf.Max(0.5f, size * 1.4f), z));
                float rad = Mathf.Max(16, ppu * Mathf.Max(0.35f, size * 0.6f));
                float d = segDist((float)px, (float)py, a.x, a.y, b.x, b.y);
                if (d > rad) continue;
                float score = d / rad - a.y * 1e-4f;
                if (score < bd)
                {
                    bd = score;
                    best = e.uid;
                }
            }
            return best;
        }

        /// <summary>Turm unter dem Zeiger: das ganze Modell (Sockel bis Spitze) ist anklickbar.</summary>
        public double pickTower(double px, double py, GameSimulation sim)
        {
            double best = 0;
            float bd = float.PositiveInfinity;
            float ppu = (float)pixelsPerUnit();
            foreach (var t in sim.s.towers)
            {
                float sz = (float)Towers.towerSize(t.def);
                float x = (float)t.cx + sz / 2, z = (float)t.cy + sz / 2;
                float bse = (float)Terrain.cellTop(sim.s.map, t.cx, t.cy);
                float top = bse + Mathf.Max(0.5f, WorldModels.towerTop(t.def) * UNIT * (sz > 1 ? 1.75f : 1) + 0.15f);
                var a = cam.WorldToScreen(new Vector3(x, bse, z));
                var b = cam.WorldToScreen(new Vector3(x, top, z));
                float rad = Mathf.Max(14, ppu * 0.42f * sz);
                float d = segDist((float)px, (float)py, a.x, a.y, b.x, b.y);
                if (d > rad) continue;
                // Näher an der Kamera (weiter unten im Bild) gewinnt bei Überlappung
                float score = d / rad - a.y * 1e-3f;
                if (score < bd)
                {
                    bd = score;
                    best = t.uid;
                }
            }
            return best;
        }

        /// <summary>
        /// Hindernis (Baum, Fels, Kristall …) über das ganze Modell anklicken – nicht nur über das Bodenfeld.
        /// Liefert den Zellindex oder -1. Lava (3) und Wolken (7) zählen nicht.
        /// </summary>
        public double pickObstacle(double px, double py, GameSimulation sim)
        {
            var map = sim.s.map;
            float ppu = (float)pixelsPerUnit();
            float rad = Mathf.Max(12, ppu * 0.42f);
            var d = map.decor;
            int w = (int)map.width;
            double best = -1;
            float bd = float.PositiveInfinity;
            for (int k = 0; k + 2 < d.Count; k += 3)
            {
                int idx = (int)d[k];
                int kind = (int)d[k + 1];
                if (kind == 3 || kind == 7 || idx < 0 || idx >= map.cells.Count || map.cells[idx] != Cell.Blocked) continue;
                float x = idx % w + 0.5f;
                float z = idx / w + 0.5f;
                float bse = (float)Terrain.cellTop(map, idx % w, idx / w);
                float h = (kind == 0 || kind == 5 ? 1.3f : kind == 1 ? 0.7f : 0.95f) * (float)Terrain.DECOR_SCALE;
                var a = cam.WorldToScreen(new Vector3(x, bse + 0.1f, z));
                var b = cam.WorldToScreen(new Vector3(x, bse + h, z));
                float dist = segDist((float)px, (float)py, a.x, a.y, b.x, b.y);
                if (dist > rad) continue;
                float score = dist / rad - a.y * 1e-3f;
                if (score < bd)
                {
                    bd = score;
                    best = idx;
                }
            }
            return best;
        }

        /// <summary>Bildschirmpixel je Welteinheit (horizontal, aktuelle Zoomstufe).</summary>
        public double pixelsPerUnit()
        {
            var tr = cam.cam.transform;
            float rx = tr.right.x, rz = -tr.right.z;
            float l = Mathf.Sqrt(rx * rx + rz * rz);
            if (l < 1e-5f) { rx = 1; rz = 0; l = 1; }
            rx /= l; rz /= l;
            var a = cam.WorldToScreen(new Vector3(cam.tx, 0, cam.tz));
            var b = cam.WorldToScreen(new Vector3(cam.tx + rx, 0, cam.tz + rz));
            return Vector2.Distance(a, b);
        }

        /// <summary>Spielpunkt → Bildschirm (Pixel, Ursprung oben links) – für Zahlen/Balken der Oberfläche.</summary>
        public Vector2 project(double x, double y, double z)
        {
            return cam.WorldToScreen(new Vector3((float)x, (float)y, (float)z));
        }

        // ------------------------------------------------------------ Hilfen

        static float easeOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1;
            return 1 + c3 * Mathf.Pow(t - 1, 3) + c1 * Mathf.Pow(t - 1, 2);
        }

        static double heroStatsOfDef(string id)
        {
            return Heroes.HEROES.TryGetValue(id, out var d) && d.@base != null ? d.@base.auraRadius : 0;
        }

        /// <summary>Abstand eines Punkts zu einer Strecke (Bildschirmkoordinaten).</summary>
        static float segDist(float px, float py, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax;
            float dy = by - ay;
            float l2 = dx * dx + dy * dy;
            float t = l2 > 0 ? Mathf.Clamp01(((px - ax) * dx + (py - ay) * dy) / l2) : 0;
            float qx = px - (ax + dx * t), qy = py - (ay + dy * t);
            return Mathf.Sqrt(qx * qx + qy * qy);
        }

        static bool bossAlive(GameSimulation sim)
        {
            foreach (var e in sim.s.enemies) if (e.alive && e.boss != null) return true;
            return false;
        }

        /// <summary>Welt ein-/ausblenden (z. B. wenn ein Bildschirm die Welt vollständig verdeckt).</summary>
        public void setVisible(bool on)
        {
            if (root.gameObject.activeSelf != on) root.gameObject.SetActive(on);
        }
    }
}
