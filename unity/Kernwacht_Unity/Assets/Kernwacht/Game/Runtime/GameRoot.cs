using System.Collections.Generic;
using Kernwacht.Models;
using UnityEngine;

namespace Kernwacht.Game
{
    /// <summary>
    /// Hauptobjekt der Spielszene: startet Menü und Partie, baut die Karte aus den Modellen auf, verbindet Simulation und
    /// Darstellung (IGameView) und verarbeitet Kamera und Eingabe. Die Oberfläche zeichnet GameHud.
    /// </summary>
    public class GameRoot : MonoBehaviour, IGameView
    {
        public GameAssets assets;
        [Tooltip("Höhe einer Geländestufe in Einheiten.")] public float stepHeight = 0.12f;

        [System.NonSerialized] public GameSim sim;
        [System.NonSerialized] public GameData data;
        public int speed = 1;
        public bool paused;
        [System.NonSerialized] public TowerDef buildDef;
        [System.NonSerialized] public Tower selected;
        public Vector2Int hoverCell = new Vector2Int(-1, -1);
        public bool hoverValid;
        public string hoverReason;
        public readonly List<(string text, int sev, float t)> messages = new List<(string, int, float)>();
        public readonly List<(Vector3 pos, string text, Color color, float t)> floats = new List<(Vector3, string, Color, float)>();
        public GameHud hud;

        Camera cam;
        Light sun;
        Transform world, dynamicRoot;
        float accum;
        Vector3 camFocus;
        float camDist = 16f, camYaw = 0f;
        const float CamPitch = 56f;
        Vector2 lastPointer;
        bool dragging;
        float pinchStart;
        GameObject hoverTile, rangeDisc, selectDisc;
        readonly Dictionary<Tower, GameObject> towerViews = new Dictionary<Tower, GameObject>();
        readonly List<(LineRenderer lr, float t)> beams = new List<(LineRenderer, float)>();
        readonly Stack<LineRenderer> beamPool = new Stack<LineRenderer>();
        readonly List<(Transform tr, Renderer r, float t, float dur, float radius, Color c)> bursts = new List<(Transform, Renderer, float, float, float, Color)>();
        readonly List<(GameObject go, float t)> dying = new List<(GameObject, float)>();
        MaterialPropertyBlock mpb;
        public string mapName = "";

        void Awake()
        {
            if (assets == null) assets = GameAssets.Instance;
            data = GameData.Instance;
            mpb = new MaterialPropertyBlock();
            cam = Camera.main;
            if (cam == null)
            {
                var cgo = new GameObject("Kamera");
                cam = cgo.AddComponent<Camera>();
                cgo.tag = "MainCamera";
            }
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            sun = FindAnyObjectByType<Light>();
            if (sun == null)
            {
                var l = new GameObject("Sonne");
                sun = l.AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            sun.shadows = LightShadows.Soft;
            hud = GetComponent<GameHud>();
            if (hud == null) hud = gameObject.AddComponent<GameHud>();
            hud.root = this;
            Application.targetFrameRate = 60;
        }

        // ================================================================== Partie

        public void StartGame(MapDef map, string difficulty, bool endless)
        {
            ClearWorld();
            sim = new GameSim(data, map, difficulty, endless, map.seed.GetHashCode());
            sim.view = this;
            mapName = map.name;
            speed = 1;
            paused = false;
            selected = null;
            buildDef = null;
            messages.Clear();
            BuildWorld(map);
            Message($"{map.name}: Baue Türme neben dem Weg und starte die erste Welle.", 0);
        }

        public void QuitToMenu()
        {
            ClearWorld();
            sim = null;
        }

        void ClearWorld()
        {
            if (world != null) Destroy(world.gameObject);
            towerViews.Clear();
            beams.Clear();
            beamPool.Clear();
            bursts.Clear();
            dying.Clear();
            floats.Clear();
            world = new GameObject("Welt").transform;
            dynamicRoot = new GameObject("Dynamisch").transform;
            dynamicRoot.SetParent(world, false);
        }

        public float GroundY(int x, int y)
        {
            if (sim == null || !sim.InBounds(x, y)) return 0f;
            int c = sim.CellAt(x, y);
            if (Cell.Walkable(c)) return 0.02f;
            return 0.1f + sim.HeightAt(x, y) * stepHeight;
        }

        public Vector3 WorldPos(Vector2 p, float lift = 0f)
        {
            int cx = Mathf.Clamp(Mathf.FloorToInt(p.x), 0, sim.map.width - 1), cy = Mathf.Clamp(Mathf.FloorToInt(p.y), 0, sim.map.height - 1);
            return new Vector3(p.x, GroundY(cx, cy) + lift, p.y);
        }

        // Verweise, damit der WebGL-Build die Collider der Grundformen nicht wegoptimiert
        static readonly System.Type[] KeepTypes = { typeof(SphereCollider), typeof(CapsuleCollider), typeof(BoxCollider), typeof(MeshCollider) };

        GameObject Prim(PrimitiveType type, Transform parent, Material mat, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<Renderer>();
            if (mat != null) r.sharedMaterial = mat;
            SetColor(r, color);
            return go;
        }

        void SetColor(Renderer r, Color c)
        {
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", c);
            mpb.SetColor("_Color", c);
            r.SetPropertyBlock(mpb);
        }

        static readonly string[] DecorModels = { "tree", "rock", "crystals", "lavapool", "iceblock", "ruin", "mushroom", "cloudbank", "willow", "glowshroom" };

        void BuildWorld(MapDef map)
        {
            var pal = map;
            Color ground = pal.Pal("ground", new Color(0.3f, 0.5f, 0.25f));
            Color ground2 = pal.Pal("ground2", ground * 1.08f);
            Color path = pal.Pal("path", new Color(0.7f, 0.6f, 0.4f));
            Color cliff = pal.Pal("cliff", new Color(0.35f, 0.28f, 0.2f));
            Color high = pal.Pal("high", ground * 1.15f);
            Color obstacle = pal.Pal("obstacle", ground * 0.7f);
            Color sky = pal.Pal("sky", new Color(0.1f, 0.12f, 0.15f));
            Color fog = pal.Pal("fog", sky);
            Color light = pal.Pal("light", Color.white);
            Color accent = pal.Pal("accent", Color.cyan);

            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = sky;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = fog;
            RenderSettings.fogStartDistance = 30f;
            RenderSettings.fogEndDistance = 80f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.Lerp(sky, light, 0.75f);
            RenderSettings.ambientEquatorColor = Color.Lerp(Color.Lerp(fog, ground, 0.5f), light, 0.35f);
            RenderSettings.ambientGroundColor = Color.Lerp(cliff, light, 0.15f);
            sun.color = light;
            sun.intensity = 2.1f;
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

            var terrain = new GameObject("Gelände").transform;
            terrain.SetParent(world, false);
            var lit = assets != null ? assets.lit : null;

            // Sockel unter der ganzen Karte
            var basePlate = Prim(PrimitiveType.Cube, terrain, lit, Color.Lerp(cliff, Color.black, 0.35f));
            basePlate.name = "Sockel";
            basePlate.transform.localScale = new Vector3(map.width + 1.2f, 1.2f, map.height + 1.2f);
            basePlate.transform.localPosition = new Vector3(map.width * 0.5f, -0.6f, map.height * 0.5f);

            for (int y = 0; y < map.height; y++)
                for (int x = 0; x < map.width; x++)
                {
                    int i = y * map.width + x;
                    int c = map.cells[i];
                    float top = GroundY(x, y);
                    Color col;
                    if (Cell.Walkable(c)) col = Color.Lerp(path, path * 0.85f, Hash(x, y));
                    else if (c == Cell.Blocked) col = Color.Lerp(obstacle, ground, 0.35f);
                    else
                    {
                        col = Color.Lerp(ground, ground2, Hash(x, y));
                        if (map.heights[i] >= 2) col = Color.Lerp(col, high, 0.6f);
                    }
                    if (map.ley.Contains(i)) col = Color.Lerp(col, accent, 0.25f);
                    var tile = Prim(PrimitiveType.Cube, terrain, lit, col);
                    tile.name = $"Feld {x},{y}";
                    float h = top + 0.3f;
                    tile.transform.localScale = new Vector3(Cell.Walkable(c) ? 1f : 0.985f, h, Cell.Walkable(c) ? 1f : 0.985f);
                    tile.transform.localPosition = new Vector3(x + 0.5f, top - h * 0.5f, y + 0.5f);
                    if (map.veins.Contains(i))
                    {
                        var v = Prim(PrimitiveType.Sphere, terrain, lit, new Color(1f, 0.8f, 0.25f));
                        v.transform.localScale = new Vector3(0.18f, 0.08f, 0.18f);
                        v.transform.localPosition = new Vector3(x + 0.3f, top + 0.02f, y + 0.7f);
                    }
                }

            // Hindernisse als Modelle (decor: Index, Art, Rotation×100)
            for (int k = 0; k + 2 < map.decor.Count; k += 3)
            {
                int i = map.decor[k], kind = map.decor[k + 1];
                float rot = map.decor[k + 2] / 100f * Mathf.Rad2Deg;
                int x = i % map.width, y = i / map.width;
                string id = kind >= 0 && kind < DecorModels.Length ? DecorModels[kind] : "rock";
                if (id == "tree" && map.biome == "volcano") id = "tree_dead";
                var pos = new Vector3(x + 0.5f, GroundY(x, y), y + 0.5f);
                var go = SpawnModel(id, pos, Quaternion.Euler(0, rot, 0), terrain);
                if (go != null) go.transform.localScale = Vector3.one * (0.85f + Hash(x * 3, y * 7) * 0.3f);
            }

            // Portal(e) und Kern
            foreach (var s in map.spawns)
            {
                var p = new Vector3(s.x + 0.5f, GroundY(s.x, s.y), s.y + 0.5f);
                var dir = FirstStep(s);
                SpawnModel("portal", p, Quaternion.LookRotation(new Vector3(dir.x, 0, dir.y)), terrain);
            }
            var g = map.goal;
            SpawnModel("core", new Vector3(g.x + 0.5f, GroundY(g.x, g.y), g.y + 0.5f), Quaternion.identity, terrain);

            // Markierungen
            hoverTile = Prim(PrimitiveType.Cube, world, assets != null ? assets.transparent : null, new Color(0.3f, 1f, 0.4f, 0.35f));
            hoverTile.name = "Bauvorschau";
            hoverTile.SetActive(false);
            rangeDisc = Prim(PrimitiveType.Cylinder, world, assets != null ? assets.transparent : null, new Color(0.4f, 0.8f, 1f, 0.18f));
            rangeDisc.name = "Reichweite";
            rangeDisc.SetActive(false);
            selectDisc = Prim(PrimitiveType.Cylinder, world, assets != null ? assets.transparent : null, new Color(1f, 0.85f, 0.3f, 0.16f));
            selectDisc.name = "Auswahl";
            selectDisc.SetActive(false);

            camFocus = new Vector3(map.width * 0.5f, 0, map.height * 0.5f - 0.6f);
            camYaw = 0f;
            camDist = Mathf.Max(map.width, map.height * 1.6f) * 0.95f;
            ApplyCamera();
        }

        Vector2Int FirstStep(Vector2Int s)
        {
            var path = sim.Trace(s);
            if (path.Count > 1) return path[1] - s;
            return new Vector2Int(1, 0);
        }

        static float Hash(int x, int y)
        {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return (h & 0xffff) / 65535f;
        }

        GameObject SpawnModel(string id, Vector3 pos, Quaternion rot, Transform parent)
        {
            GameObject go = null;
            if (assets != null && assets.catalog != null && assets.catalog.Prefab(id) != null)
                go = Instantiate(assets.catalog.Prefab(id), pos, rot, parent);
            if (go == null)
            {
                // Ersatzform, falls das Modell fehlt (z. B. vor „Modelle einrichten“)
                go = Prim(PrimitiveType.Capsule, parent, assets != null ? assets.lit : null, Color.magenta);
                go.transform.position = pos + Vector3.up * 0.4f;
                go.transform.localScale = Vector3.one * 0.5f;
            }
            go.name = id;
            return go;
        }

        // ================================================================== Schleife

        void Update()
        {
            if (sim == null) return;
            HandleCamera();
            HandlePointer();
            HandleKeys();

            if (!paused && sim.phase != Phase.Victory && sim.phase != Phase.Defeat)
            {
                accum += Mathf.Min(Time.deltaTime, 0.1f) * speed;
                int ticks = 0;
                while (accum >= GameSim.Dt && ticks < 40)
                {
                    sim.Step();
                    accum -= GameSim.Dt;
                    ticks++;
                }
            }
            SyncViews();
            UpdateFx();
            for (int i = messages.Count - 1; i >= 0; i--) if (Time.unscaledTime - messages[i].t > 7f) messages.RemoveAt(i);
        }

        void HandleKeys()
        {
            if (Inp.Key("esc")) { if (buildDef != null || selected != null) { buildDef = null; selected = null; } else paused = !paused; }
            if (Inp.Key("space") && sim.CanStartWave) sim.StartWave();
            if (Inp.Key("p")) paused = !paused;
            if (Inp.Key("f")) speed = speed >= 3 ? 1 : speed + 1;
            int d = Inp.DigitDown();
            if (d > 0)
            {
                var list = hud.Buildable;
                if (d - 1 < list.Count) { buildDef = list[d - 1]; selected = null; }
            }
            if (selected != null && Inp.Key("u")) sim.Upgrade(selected);
            if (selected != null && Inp.Key("del")) { sim.Sell(selected); selected = null; }
        }

        void ApplyCamera()
        {
            var rot = Quaternion.Euler(CamPitch, camYaw, 0);
            cam.transform.rotation = rot;
            cam.transform.position = camFocus - rot * Vector3.forward * camDist;
        }

        void HandleCamera()
        {
            float dt = Time.unscaledDeltaTime;
            Vector3 fwd = Quaternion.Euler(0, camYaw, 0) * Vector3.forward, right = Quaternion.Euler(0, camYaw, 0) * Vector3.right;
            float pan = camDist * 0.9f * dt;
            if (Inp.Held("w")) camFocus += fwd * pan;
            if (Inp.Held("s")) camFocus -= fwd * pan;
            if (Inp.Held("a")) camFocus -= right * pan;
            if (Inp.Held("d")) camFocus += right * pan;
            if (Inp.Held("q")) camYaw += 60f * dt;
            if (Inp.Held("e")) camYaw -= 60f * dt;
            float sc = Inp.Scroll;
            if (Mathf.Abs(sc) > 0.01f && !hud.PointerOverUi(Inp.PointerPos)) camDist = Mathf.Clamp(camDist * (1f - sc * 0.12f), 5f, 40f);

            var pp = Inp.PointerPos;
            if (Inp.PanHeld)
            {
                if (dragging)
                {
                    var delta = pp - lastPointer;
                    float k = camDist / Mathf.Max(400f, Screen.height) * 1.6f;
                    camFocus -= right * delta.x * k + fwd * delta.y * k;
                }
                dragging = true;
            }
            else dragging = false;

            // Zwei Finger: verschieben und zoomen
            if (Inp.TouchCount >= 2)
            {
                var a = Inp.TouchPos(0);
                var b = Inp.TouchPos(1);
                float dist = Vector2.Distance(a, b);
                if (pinchStart > 0) camDist = Mathf.Clamp(camDist * pinchStart / Mathf.Max(1f, dist), 6f, 40f);
                pinchStart = dist;
            }
            else pinchStart = 0;

            lastPointer = pp;
            if (sim != null)
            {
                camFocus.x = Mathf.Clamp(camFocus.x, -2f, sim.map.width + 2f);
                camFocus.z = Mathf.Clamp(camFocus.z, -4f, sim.map.height + 2f);
            }
            ApplyCamera();
        }

        bool RayCell(Vector2 screen, out Vector2Int cell, out Vector3 hitPoint)
        {
            cell = new Vector2Int(-1, -1);
            hitPoint = Vector3.zero;
            var ray = cam.ScreenPointToRay(screen);
            // Gelände ist gestuft: die Ebene in passender Höhe schneiden (zwei Durchgänge)
            float yPlane = 0.12f;
            for (int pass = 0; pass < 2; pass++)
            {
                var plane = new Plane(Vector3.up, new Vector3(0, yPlane, 0));
                if (!plane.Raycast(ray, out float t)) return false;
                hitPoint = ray.GetPoint(t);
                cell = new Vector2Int(Mathf.FloorToInt(hitPoint.x), Mathf.FloorToInt(hitPoint.z));
                if (!sim.InBounds(cell.x, cell.y)) return false;
                yPlane = GroundY(cell.x, cell.y);
            }
            return true;
        }

        void HandlePointer()
        {
            var pp = Inp.PointerPos;
            bool overUi = hud.PointerOverUi(pp);
            hoverCell = new Vector2Int(-1, -1);
            if (!overUi && RayCell(pp, out var cell, out _)) hoverCell = cell;

            if (buildDef != null && hoverCell.x >= 0)
            {
                hoverReason = sim.CanBuild(buildDef, hoverCell.x, hoverCell.y);
                hoverValid = hoverReason == null;
            }
            else hoverReason = null;

            if (Inp.SecondaryDown) { buildDef = null; }
            if (Inp.PrimaryUp && !overUi && Inp.TouchCount < 2)
            {
                if (hoverCell.x < 0) { selected = null; return; }
                if (buildDef != null)
                {
                    var t = sim.Build(buildDef, hoverCell.x, hoverCell.y);
                    if (t == null) Message(hoverReason ?? "Hier kann nicht gebaut werden", 1);
                    else if (sim.gold < buildDef.cost) buildDef = null;
                }
                else
                {
                    selected = sim.TowerAt(hoverCell.x, hoverCell.y);
                }
            }
        }

        // ================================================================== Darstellung synchronisieren

        void SyncViews()
        {
            // Bauvorschau
            if (buildDef != null && hoverCell.x >= 0)
            {
                hoverTile.SetActive(true);
                float s = buildDef.size;
                float y = GroundY(hoverCell.x, hoverCell.y);
                hoverTile.transform.position = new Vector3(hoverCell.x + s * 0.5f, y + 0.03f, hoverCell.y + s * 0.5f);
                hoverTile.transform.localScale = new Vector3(s * 0.96f, 0.06f, s * 0.96f);
                SetColor(hoverTile.GetComponent<Renderer>(), hoverValid ? new Color(0.3f, 1f, 0.4f, 0.45f) : new Color(1f, 0.25f, 0.2f, 0.45f));
                float r = sim.PreviewRange(buildDef, hoverCell.x, hoverCell.y);
                ShowDisc(rangeDisc, new Vector3(hoverCell.x + s * 0.5f, y + 0.02f, hoverCell.y + s * 0.5f), r);
            }
            else
            {
                if (hoverTile != null) hoverTile.SetActive(false);
                if (rangeDisc != null) rangeDisc.SetActive(false);
            }
            if (selected != null && !sim.towers.Contains(selected)) selected = null;
            if (selected != null)
            {
                var c = selected.Center;
                ShowDisc(selectDisc, new Vector3(c.x, GroundY(selected.cx, selected.cy) + 0.02f, c.y), selected.S("range") * Mathf.Max(0.01f, selected.S("rangeMul")));
            }
            else if (selectDisc != null) selectDisc.SetActive(false);

            // Gegner
            foreach (var e in sim.enemies)
            {
                if (e.view == null) continue;
                float lift = e.flying ? 0.9f + Mathf.Sin(Time.time * 3f + e.uid) * 0.06f : 0f;
                var target = WorldPos(e.Pos, lift);
                e.view.transform.position = target;
                if (e.unit != null) e.unit.FaceTowards(new Vector3(e.nx + 0.5f, target.y, e.ny + 0.5f));
            }
            // Geschosse
            foreach (var p in sim.projectiles)
                if (p.view != null) p.view.transform.position = WorldPos(p.pos, p.z);
        }

        void ShowDisc(GameObject disc, Vector3 pos, float radius)
        {
            if (disc == null) return;
            disc.SetActive(radius > 0.05f);
            disc.transform.position = pos;
            disc.transform.localScale = new Vector3(radius * 2f, 0.01f, radius * 2f);
        }

        void UpdateFx()
        {
            float dt = Time.deltaTime;
            for (int i = beams.Count - 1; i >= 0; i--)
            {
                var b = beams[i];
                b.t -= dt;
                if (b.t <= 0) { b.lr.gameObject.SetActive(false); beamPool.Push(b.lr); beams.RemoveAt(i); }
                else beams[i] = b;
            }
            for (int i = bursts.Count - 1; i >= 0; i--)
            {
                var b = bursts[i];
                b.t += dt;
                float k = b.t / b.dur;
                if (k >= 1f) { Destroy(b.tr.gameObject); bursts.RemoveAt(i); continue; }
                float r = Mathf.Lerp(0.2f, b.radius, Mathf.Sqrt(k)) * 2f;
                b.tr.localScale = new Vector3(r, 0.02f + 0.3f * (1 - k), r);
                var c = b.c; c.a = 0.45f * (1 - k);
                SetColor(b.r, c);
                bursts[i] = b;
            }
            for (int i = dying.Count - 1; i >= 0; i--)
            {
                var d = dying[i];
                d.t -= dt;
                if (d.t <= 0) { if (d.go != null) Destroy(d.go); dying.RemoveAt(i); }
                else
                {
                    if (d.t < 0.5f && d.go != null) d.go.transform.position += Vector3.down * dt * 0.6f;
                    dying[i] = d;
                }
            }
            for (int i = floats.Count - 1; i >= 0; i--)
            {
                var f = floats[i];
                f.t -= dt;
                if (f.t <= 0) floats.RemoveAt(i);
                else { f.pos += Vector3.up * dt * 0.8f; floats[i] = f; }
            }
        }

        // ================================================================== IGameView

        public void EnemySpawned(Enemy e)
        {
            var pos = WorldPos(e.Pos);
            var go = SpawnModel(e.ModelId, pos, Quaternion.identity, dynamicRoot);
            float s = (e.IsBoss ? 1f : 1.35f) * (e.elite ? data.eliteSize : 1f);
            if (e.mutation != null) s *= 1.05f;
            go.transform.localScale = Vector3.one * s;
            e.view = go;
            e.unit = go.GetComponent<UnitView>();
            if (e.unit != null) e.unit.SetAutoMovement(true);
            var rs = go.GetComponentsInChildren<Renderer>();
            float top = 0.8f;
            if (rs.Length > 0)
            {
                var b = rs[0].bounds;
                foreach (var r in rs) b.Encapsulate(r.bounds);
                top = b.max.y - pos.y;
            }
            e.viewHeight = Mathf.Max(0.4f, top);
            if (e.elite)
                foreach (var r in rs) { r.GetPropertyBlock(mpb); mpb.SetColor("_EmissionColor", new Color(0.35f, 0.1f, 0.05f)); r.SetPropertyBlock(mpb); }
        }

        public void EnemyDied(Enemy e, bool leaked)
        {
            if (e.view == null) return;
            if (leaked) { Destroy(e.view); e.view = null; Burst(new Vector2(sim.map.goal.x + 0.5f, sim.map.goal.y + 0.5f), 1f, new Color(1f, 0.2f, 0.2f)); return; }
            float len = e.unit != null ? e.unit.PlayDeath() : 0f;
            dying.Add((e.view, Mathf.Max(0.6f, len + 0.8f)));
            e.view = null;
        }

        public void TowerChanged(Tower t, bool removed)
        {
            if (towerViews.TryGetValue(t, out var old) && old != null) Destroy(old);
            towerViews.Remove(t);
            t.view = null;
            t.tview = null;
            if (removed) return;
            var c = t.Center;
            var go = SpawnModel(t.def.id, new Vector3(c.x, GroundY(t.cx, t.cy), c.y), Quaternion.Euler(0, 180, 0), world);
            // Ausbaustufen leicht sichtbar machen
            go.transform.localScale = Vector3.one * (1f + (t.level - 1) * 0.04f + (t.spec > 0 ? 0.04f : 0f));
            t.view = go;
            t.tview = go.GetComponent<TowerView>();
            towerViews[t] = go;
        }

        public void ProjectileSpawned(Projectile p)
        {
            var go = Prim(PrimitiveType.Sphere, dynamicRoot, assets != null ? assets.unlit : null, Color.Lerp(p.tower.def.color, Color.white, 0.35f));
            float s = p.tower.S("splash") > 0 ? 0.18f : 0.1f;
            go.transform.localScale = Vector3.one * s;
            var tv = p.tower.tview;
            go.transform.position = tv != null ? tv.MuzzlePosition : WorldPos(p.pos, p.z);
            var trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.18f;
            trail.startWidth = s * 0.9f;
            trail.endWidth = 0f;
            if (assets != null && assets.unlit != null) trail.sharedMaterial = assets.unlit;
            var tc = p.tower.def.color;
            trail.startColor = tc;
            trail.endColor = new Color(tc.r, tc.g, tc.b, 0f);
            p.view = go;
        }

        public void ProjectileEnded(Projectile p)
        {
            if (p.view != null) Destroy(p.view);
            p.view = null;
        }

        public void MineChanged(Mine m, bool removed)
        {
            if (removed) { if (m.view != null) Destroy(m.view); m.view = null; return; }
            var go = Prim(PrimitiveType.Cylinder, dynamicRoot, assets != null ? assets.lit : null, Color.Lerp(m.tower.def.color, Color.black, 0.3f));
            go.transform.localScale = new Vector3(0.22f, 0.03f, 0.22f);
            go.transform.position = WorldPos(m.pos, 0.03f);
            m.view = go;
        }

        public void CloudChanged(Cloud c, bool removed)
        {
            if (removed) { if (c.view != null) Destroy(c.view); c.view = null; return; }
            var go = Prim(PrimitiveType.Sphere, dynamicRoot, assets != null ? assets.transparent : null, new Color(0.55f, 0.9f, 0.3f, 0.22f));
            go.transform.localScale = new Vector3(c.r * 2f, 0.5f, c.r * 2f);
            go.transform.position = WorldPos(c.pos, 0.15f);
            c.view = go;
        }

        public void MeteorChanged(Meteor m, bool removed)
        {
            if (removed) { if (m.view != null) Destroy(m.view); m.view = null; return; }
            var go = Prim(PrimitiveType.Cylinder, dynamicRoot, assets != null ? assets.transparent : null, new Color(1f, 0.35f, 0.15f, 0.3f));
            float r = Mathf.Max(0.6f, m.tower.S("splash"));
            go.transform.localScale = new Vector3(r * 2f, 0.01f, r * 2f);
            go.transform.position = WorldPos(m.pos, 0.03f);
            m.view = go;
            Beam(m.pos + new Vector2(-1.5f, -1.5f), 8f, m.pos, new Color(1f, 0.6f, 0.2f), 0.18f, m.total);
        }

        public void Beam(Vector2 from, float fromZ, Vector2 to, Color color, float width, float duration)
        {
            LineRenderer lr = beamPool.Count > 0 ? beamPool.Pop() : null;
            if (lr == null)
            {
                var go = new GameObject("Strahl");
                go.transform.SetParent(dynamicRoot, false);
                lr = go.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.numCapVertices = 2;
                if (assets != null && assets.unlit != null) lr.sharedMaterial = assets.unlit;
            }
            lr.gameObject.SetActive(true);
            lr.startWidth = width;
            lr.endWidth = width * 0.6f;
            lr.startColor = Color.Lerp(color, Color.white, 0.4f);
            lr.endColor = color;
            lr.SetPosition(0, WorldPos(from, fromZ));
            lr.SetPosition(1, WorldPos(to, 0.45f));
            beams.Add((lr, duration));
        }

        public void Burst(Vector2 at, float radius, Color color)
        {
            if (bursts.Count > 60) return;
            var go = Prim(PrimitiveType.Cylinder, dynamicRoot, assets != null ? assets.transparent : null, color);
            go.transform.position = WorldPos(at, 0.05f);
            bursts.Add((go.transform, go.GetComponent<Renderer>(), 0f, 0.35f, Mathf.Max(0.3f, radius), color));
        }

        public void Shot(Tower t, Vector2 at) { }

        public void FloatText(Vector2 at, string text, Color color)
        {
            if (floats.Count > 40) return;
            floats.Add((WorldPos(at, 0.9f), text, color, 1.0f));
        }

        public void Message(string text, int severity)
        {
            messages.Add((text, severity, Time.unscaledTime));
            if (messages.Count > 6) messages.RemoveAt(0);
        }

        public Camera Cam => cam;
    }
}
