using System.Collections.Generic;
using UnityEngine;

namespace Kernwacht.Game
{
    /// <summary>
    /// Spieloberfläche (IMGUI – läuft ohne weitere Assets im Editor und im WebGL-Build):
    /// Startmenü, obere Leiste, Turmleiste, Turmdetails mit Ausbau/Spezialisierung/Fusion, Bossleiste, Meldungen.
    /// </summary>
    public class GameHud : MonoBehaviour
    {
        public GameRoot root;

        const float RefH = 760f;
        float scale = 1f;
        readonly List<Rect> uiRects = new List<Rect>();
        readonly List<Rect> nextRects = new List<Rect>();
        GUIStyle box, title, label, small, button, bigButton, center, tagStyle;
        Texture2D panelTex, panelHi, barBg, white;
        Vector2 towerScroll, infoScroll;
        int menuMap, menuDiff = 1;
        bool menuEndless;
        bool showHelp;
        List<TowerDef> buildable;

        static readonly string[] Diffs = { "easy", "normal", "hard" };

        public List<TowerDef> Buildable
        {
            get
            {
                if (buildable == null)
                {
                    buildable = new List<TowerDef>();
                    foreach (var t in root.data.towerOrder)
                        if (!t.legendary && !t.mazingOnly && t.attack != "none") buildable.Add(t);
                }
                return buildable;
            }
        }

        static Texture2D Tex(Color c)
        {
            var t = new Texture2D(1, 1) { hideFlags = HideFlags.DontSave };
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        void InitStyles()
        {
            if (box != null) return;
            panelTex = Tex(new Color(0.07f, 0.06f, 0.09f, 0.88f));
            panelHi = Tex(new Color(0.22f, 0.17f, 0.28f, 0.95f));
            barBg = Tex(new Color(0f, 0f, 0f, 0.6f));
            white = Tex(Color.white);
            box = new GUIStyle(GUI.skin.box) { padding = new RectOffset(10, 10, 8, 8) };
            box.normal.background = panelTex;
            title = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, richText = true };
            title.normal.textColor = new Color(0.96f, 0.86f, 0.62f);
            label = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true, wordWrap = true };
            label.normal.textColor = new Color(0.9f, 0.88f, 0.92f);
            small = new GUIStyle(label) { fontSize = 12 };
            small.normal.textColor = new Color(0.72f, 0.7f, 0.78f);
            center = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontSize = 15 };
            tagStyle = new GUIStyle(label) { fontSize = 12, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            button = new GUIStyle(GUI.skin.button) { fontSize = 13, richText = true, wordWrap = true, padding = new RectOffset(6, 6, 4, 4) };
            button.normal.background = Tex(new Color(0.16f, 0.13f, 0.2f, 0.95f));
            button.hover.background = panelHi;
            button.active.background = Tex(new Color(0.35f, 0.25f, 0.45f, 1f));
            button.normal.textColor = button.hover.textColor = button.active.textColor = new Color(0.95f, 0.92f, 0.98f);
            bigButton = new GUIStyle(button) { fontSize = 17, fontStyle = FontStyle.Bold };
        }

        /// <summary>Liegt der Zeiger über einer Oberfläche? (Bildschirmkoordinaten, Ursprung unten links)</summary>
        public bool PointerOverUi(Vector2 screen)
        {
            if (root == null || root.sim == null) return true;
            var p = new Vector2(screen.x / scale, (Screen.height - screen.y) / scale);
            foreach (var r in uiRects) if (r.Contains(p)) return true;
            return false;
        }

        Rect Panel(Rect r)
        {
            nextRects.Add(r);
            GUI.Box(r, GUIContent.none, box);
            return r;
        }

        void OnGUI()
        {
            if (root == null) return;
            InitStyles();
            scale = Mathf.Clamp(Screen.height / RefH, 0.6f, 2.2f);
            if (Screen.width / scale < 900f) scale = Mathf.Max(0.5f, Screen.width / 900f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float W = Screen.width / scale, H = Screen.height / scale;
            if (Event.current.type == EventType.Layout) nextRects.Clear();

            if (root.sim == null) DrawMenu(W, H);
            else
            {
                DrawWorldOverlays();
                DrawTop(W);
                DrawBossBar(W);
                DrawTowerBar(W, H);
                if (root.selected != null) DrawTowerInfo(W, H);
                else if (root.buildDef != null) DrawBuildInfo(W, H);
                DrawMessages(H);
                if (root.sim.phase == Phase.Victory || root.sim.phase == Phase.Defeat) DrawEnd(W, H);
                else if (root.paused) DrawPause(W, H);
                if (showHelp) DrawHelp(W, H);
            }
            if (Event.current.type == EventType.Repaint)
            {
                uiRects.Clear();
                uiRects.AddRange(nextRects);
            }
        }

        // ================================================================== Menü

        void DrawMenu(float W, float H)
        {
            GUI.DrawTexture(new Rect(0, 0, W, H), Tex2(new Color(0.05f, 0.04f, 0.07f, 1f)));
            float w = Mathf.Min(760f, W - 40f);
            var r = Panel(new Rect((W - w) * 0.5f, 40f, w, H - 80f));
            GUILayout.BeginArea(new Rect(r.x + 16, r.y + 12, r.width - 32, r.height - 24));
            GUILayout.Label("<size=38>KERNWACHT</size>", title);
            GUILayout.Label("Dark-Fantasy-Tower-Defense · Unity-Fassung (spielbarer Kern)", small);
            GUILayout.Space(14);
            GUILayout.Label("<b>Karte</b>", label);
            var maps = root.data.maps;
            int cols = W > 700 ? 4 : 2;
            for (int i = 0; i < maps.Count; i += cols)
            {
                GUILayout.BeginHorizontal();
                for (int k = i; k < i + cols && k < maps.Count; k++)
                {
                    var m = maps[k];
                    string txt = (menuMap == k ? "<color=#ffd27a>» </color>" : "") + $"<b>{m.biomeName}</b>\n<size=11>{BossLine(m)}</size>";
                    if (GUILayout.Button(txt, button, GUILayout.Height(54))) menuMap = k;
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(10);
            GUILayout.Label("<b>Schwierigkeit</b>", label);
            GUILayout.BeginHorizontal();
            for (int i = 0; i < Diffs.Length; i++)
            {
                string n = root.data.difficulty.TryGetValue(Diffs[i], out var d) ? d.name : Diffs[i];
                if (GUILayout.Button((menuDiff == i ? "<color=#ffd27a>» </color>" : "") + n, button, GUILayout.Height(34))) menuDiff = i;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(10);
            GUILayout.Label("<b>Modus</b>", label);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button((!menuEndless ? "<color=#ffd27a>» </color>" : "") + "Schnelle Partie · 20 Wellen", button, GUILayout.Height(34))) menuEndless = false;
            if (GUILayout.Button((menuEndless ? "<color=#ffd27a>» </color>" : "") + "Endlos", button, GUILayout.Height(34))) menuEndless = true;
            GUILayout.EndHorizontal();
            GUILayout.Space(16);
            if (maps.Count == 0)
                GUILayout.Label("<color=#ff8080>Keine Kartendaten gefunden (Resources/KernwachtData/maps.json).</color>", label);
            else if (GUILayout.Button("Spiel starten", bigButton, GUILayout.Height(52)))
                root.StartGame(maps[Mathf.Clamp(menuMap, 0, maps.Count - 1)], Diffs[menuDiff], menuEndless);
            GUILayout.Space(10);
            if (root.assets == null || root.assets.catalog == null)
                GUILayout.Label("<color=#ffb070>Hinweis: Modellkatalog fehlt – im Editor „Kernwacht → 1 · Modelle einrichten“ und „3 · Spielszene erzeugen“ ausführen. Bis dahin erscheinen Ersatzformen.</color>", small);
            GUILayout.Label("Steuerung: Linksklick bauen/auswählen · Rechtsklick abbrechen · Rechte/mittlere Maustaste ziehen = Kamera · Mausrad = Zoom · Q/E drehen · WASD verschieben · Leertaste = nächste Welle · 1–0 Türme · U ausbauen · Entf verkaufen · F Tempo · P Pause", small);
            GUILayout.EndArea();
        }

        string BossLine(MapDef m)
        {
            var parts = new List<string>();
            foreach (var kv in m.bosses)
                if (root.data.bosses.TryGetValue(kv.Value, out var b)) parts.Add($"W{kv.Key}: {b.name.Replace("Der ", "").Replace("Die ", "")}");
            return string.Join(" · ", parts);
        }

        readonly Dictionary<Color, Texture2D> colorTex = new Dictionary<Color, Texture2D>();
        Texture2D Tex2(Color c)
        {
            if (!colorTex.TryGetValue(c, out var t)) { t = Tex(c); colorTex[c] = t; }
            return t;
        }

        // ================================================================== Leisten

        void DrawTop(float W)
        {
            var s = root.sim;
            var r = Panel(new Rect(8, 8, W - 16, 46));
            GUILayout.BeginArea(new Rect(r.x + 10, r.y + 6, r.width - 20, r.height - 10));
            GUILayout.BeginHorizontal();
            string waves = s.endless ? $"{s.wave}" : $"{s.wave}/{s.totalWaves}";
            GUILayout.Label($"<b><color=#ffd27a>{s.gold}</color></b> Gold   <b><color=#ff8a8a>{s.lives}</color></b> Leben   Welle <b>{waves}</b>" +
                            (s.phase == Phase.Combat ? $"   <size=12>noch {s.RemainingInWave} Gegner</size>" : ""), label, GUILayout.Width(Mathf.Max(260, W * 0.36f)));
            GUILayout.FlexibleSpace();
            GUILayout.Label($"<size=12>{root.mapName}</size>", small, GUILayout.Width(Mathf.Min(220, W * 0.2f)));
            for (int sp = 1; sp <= 3; sp++)
                if (GUILayout.Button((root.speed == sp ? "<color=#ffd27a>" : "") + $"{sp}×" + (root.speed == sp ? "</color>" : ""), button, GUILayout.Width(40), GUILayout.Height(32))) root.speed = sp;
            if (GUILayout.Button(root.paused ? "Weiter" : "Pause", button, GUILayout.Width(64), GUILayout.Height(32))) root.paused = !root.paused;
            if (GUILayout.Button("?", button, GUILayout.Width(32), GUILayout.Height(32))) showHelp = !showHelp;
            GUI.enabled = s.CanStartWave;
            string next = s.CanStartWave ? NextWaveLabel() : "Kampf läuft";
            if (GUILayout.Button(next, bigButton, GUILayout.Width(190), GUILayout.Height(32))) s.StartWave();
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        string NextWaveLabel()
        {
            var s = root.sim;
            var w = s.WaveFor(s.wave + 1);
            return w != null && w.isBoss ? "» Bosswelle starten" : $"» Welle {s.wave + 1} starten";
        }

        void DrawBossBar(float W)
        {
            var e = root.sim.bossEnemy;
            if (e == null || !e.alive || e.boss == null) return;
            float w = Mathf.Min(520f, W - 40f);
            var r = Panel(new Rect((W - w) * 0.5f, 60, w, 52));
            GUI.Label(new Rect(r.x + 10, r.y + 4, r.width - 20, 20), $"<b>{e.boss.name}</b> <size=11>– {root.sim.bossPhaseName}</size>", label);
            var bar = new Rect(r.x + 10, r.y + 28, r.width - 20, 14);
            GUI.DrawTexture(bar, barBg);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(e.hp / e.maxHp), bar.height), Tex2(e.boss.color));
            if (e.maxShield > 0 && e.shield > 0)
                GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(e.shield / e.maxShield), 4), Tex2(new Color(0.5f, 0.9f, 1f)));
            if (e.plates.Count > 0)
                GUI.Label(new Rect(bar.x, bar.y - 2, bar.width, 18), $"<size=11>Schwachpunkte: {e.plates.Count}</size>", tagStyle);
        }

        void DrawTowerBar(float W, float H)
        {
            var list = Buildable;
            float bw = 92f, bh = 58f;
            int perRow = Mathf.Max(4, Mathf.FloorToInt((W - 32f) / (bw + 4f)));
            int rows = Mathf.CeilToInt(list.Count / (float)perRow);
            float ph = rows * (bh + 4f) + 14f;
            var r = Panel(new Rect(8, H - ph - 8, W - 16, ph));
            for (int i = 0; i < list.Count; i++)
            {
                var t = list[i];
                int row = i / perRow, col = i % perRow;
                var br = new Rect(r.x + 8 + col * (bw + 4f), r.y + 7 + row * (bh + 4f), bw, bh);
                bool afford = root.sim.gold >= t.cost;
                bool sel = root.buildDef == t;
                string hk = i < 10 ? $"<size=10><color=#9a90a8>{(i + 1) % 10}</color></size> " : "";
                string txt = $"{hk}<b>{Short(t.name)}</b>\n<color={(afford ? "#ffd27a" : "#a06060")}>{t.cost}</color>";
                if (sel) GUI.DrawTexture(new Rect(br.x - 2, br.y - 2, br.width + 4, br.height + 4), Tex2(new Color(1f, 0.82f, 0.45f, 0.9f)));
                if (GUI.Button(br, txt, button))
                {
                    root.buildDef = sel ? null : t;
                    root.selected = null;
                }
                GUI.DrawTexture(new Rect(br.x + 3, br.y + 3, 6, br.height - 6), Tex2(t.color));
            }
        }

        static string Short(string n)
        {
            if (n.Length <= 16) return n;
            int sp = n.IndexOf(' ');
            return sp > 0 && sp <= 16 ? n.Substring(0, sp) : n.Substring(0, 15) + "…";
        }

        float TowerBarHeight(float W)
        {
            float bw = 92f, bh = 58f;
            int perRow = Mathf.Max(4, Mathf.FloorToInt((W - 32f) / (bw + 4f)));
            int rows = Mathf.CeilToInt(Buildable.Count / (float)perRow);
            return rows * (bh + 4f) + 14f;
        }

        void DrawBuildInfo(float W, float H)
        {
            var t = root.buildDef;
            float w = Mathf.Min(330f, W * 0.4f);
            float top = 62f, bottom = H - TowerBarHeight(W) - 16f;
            var r = Panel(new Rect(W - w - 8, top, w, Mathf.Min(300f, bottom - top)));
            GUILayout.BeginArea(new Rect(r.x + 10, r.y + 8, r.width - 20, r.height - 16));
            GUILayout.Label($"<b>{t.name}</b>", title);
            GUILayout.Label($"<color=#c9b8e8>{t.role}</color> · <color=#ffd27a>{t.cost} Gold</color>", small);
            GUILayout.Label(t.description, label);
            GUILayout.Label(StatLine(t.stats, t), small);
            if (!string.IsNullOrEmpty(t.counters)) GUILayout.Label("<color=#ff9f8a>Schwächen:</color> " + t.counters, small);
            if (root.hoverReason != null && root.hoverCell.x >= 0) GUILayout.Label("<color=#ff8080>" + root.hoverReason + "</color>", small);
            GUILayout.Label("<size=11>Linksklick auf ein freies Feld neben dem Weg baut · Rechtsklick bricht ab</size>", small);
            GUILayout.EndArea();
        }

        string StatLine(Dictionary<string, float> st, TowerDef def)
        {
            float G(string k) => st.TryGetValue(k, out var v) ? v : root.data.Zero(k);
            var parts = new List<string>();
            if (def.attack == "economy") parts.Add($"Einnahmen {G("income"):0}/Welle");
            else if (def.attack == "aura") parts.Add($"Aura +{G("auraDmg") * 100:0} % Schaden, +{G("auraSpeed") * 100:0} % Tempo");
            else if (def.attack == "pylon") parts.Add($"Netz +{G("networkBonus") * 100:0} % für {G("maxLinks"):0} Türme");
            else
            {
                float dmg = G("damage") * G("damageMul");
                if (def.attack == "laser") parts.Add($"Strahl {dmg / Mathf.Max(0.05f, G("cooldown")):0}/s");
                else parts.Add($"Schaden {dmg:0.#}");
                if (def.attack != "laser") parts.Add($"alle {G("cooldown") / Mathf.Max(0.1f, G("attackSpeedMul")):0.##} s");
            }
            if (G("range") > 0) parts.Add($"Reichweite {G("range") * G("rangeMul"):0.#}");
            if (G("splash") > 0) parts.Add($"Fläche {G("splash"):0.#}");
            if (G("hitsAir") > 0) parts.Add(G("hitsGround") > 0 ? "Boden + Luft" : "nur Luft"); else if (def.attack != "economy" && def.attack != "aura") parts.Add("nur Boden");
            return string.Join(" · ", parts);
        }

        static string TypeName(string t)
        {
            switch (t)
            {
                case "physical": return "Physisch";
                case "fire": return "Feuer";
                case "frost": return "Frost";
                case "lightning": return "Blitz";
                case "poison": return "Gift";
                case "arcane": return "Arkan";
                case "sonic": return "Schall";
            }
            return "–";
        }

        static readonly string[] Priorities = { "first", "last", "strong", "close" };
        static string PrioName(string p)
        {
            switch (p) { case "last": return "Letzter"; case "strong": return "Stärkster"; case "close": return "Nächster"; }
            return "Erster";
        }

        void DrawTowerInfo(float W, float H)
        {
            var s = root.sim;
            var t = root.selected;
            float w = Mathf.Min(350f, W * 0.42f);
            float top = 62f, bottom = H - TowerBarHeight(W) - 16f;
            var r = Panel(new Rect(W - w - 8, top, w, bottom - top));
            GUILayout.BeginArea(new Rect(r.x + 10, r.y + 8, r.width - 20, r.height - 16));
            infoScroll = GUILayout.BeginScrollView(infoScroll);
            string lvl = t.def.legendary ? "<color=#ffb347>Legendär</color>" : $"Stufe {t.level}/{1 + t.def.upgrades.Count}" + (t.spec > 0 ? $" · {t.def.specs[t.spec - 1].name}" : "");
            GUILayout.Label($"<b>{t.def.name}</b>", title);
            GUILayout.Label($"{lvl} · <color=#c9b8e8>{TypeName(t.def.damageType)}</color>", small);
            GUILayout.Label(StatLine(t.st, t.def), small);
            GUILayout.Label($"Schaden verursacht: <b>{t.damageDealt:0}</b> · Besiegt: <b>{t.kills}</b>" +
                            (t.auraDmg > 0 || t.pylonBonus > 0 ? $" · Verstärkt +{(t.auraDmg + t.pylonBonus) * 100:0} %" : ""), small);
            GUILayout.Space(6);

            var up = s.NextUpgrade(t);
            if (up != null)
            {
                GUI.enabled = s.gold >= up.cost;
                if (GUILayout.Button($"<b>Ausbau: {up.name}</b> <color=#ffd27a>{up.cost}</color>\n<size=11>{up.description}</size>", button, GUILayout.MinHeight(46)))
                    Report(s.Upgrade(t));
                GUI.enabled = true;
            }
            else if (t.CanSpec)
            {
                GUILayout.Label("<b>Spezialisierung wählen</b>", label);
                for (int i = 0; i < t.def.specs.Count; i++)
                {
                    var sp = t.def.specs[i];
                    GUI.enabled = s.gold >= sp.cost;
                    if (GUILayout.Button($"<b>{sp.name}</b> <color=#ffd27a>{sp.cost}</color>\n<size=11>{sp.description}</size>", button, GUILayout.MinHeight(50)))
                        Report(s.Specialize(t, i + 1));
                    GUI.enabled = true;
                }
            }

            var fus = s.FusionOptions(t);
            if (fus.Count > 0)
            {
                GUILayout.Space(4);
                GUILayout.Label("<b>Fusion</b>", label);
                foreach (var f in fus)
                {
                    string nm = root.data.towers.TryGetValue(f.recipe.id, out var fd) ? fd.name : f.recipe.id;
                    GUI.enabled = f.ready && s.gold >= f.recipe.cost;
                    string hint = f.ready ? "" : "\n<size=11>Beide voll ausbauen und spezialisieren</size>";
                    if (GUILayout.Button($"Mit {f.partner.def.name} → <b>{nm}</b> <color=#ffd27a>{f.recipe.cost}</color>{hint}", button, GUILayout.MinHeight(40)))
                        Report(s.Fuse(t, f));
                    GUI.enabled = true;
                }
            }

            if (t.def.attack != "economy" && t.def.attack != "aura")
            {
                GUILayout.Space(4);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Ziel:", small, GUILayout.Width(36));
                foreach (var p in Priorities)
                    if (GUILayout.Button((t.priority == p ? "<color=#ffd27a>" : "") + PrioName(p) + (t.priority == p ? "</color>" : ""), button, GUILayout.Height(26))) t.priority = p;
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(6);
            if (GUILayout.Button($"Verkaufen (+{s.RefundValue(t)} Gold)", button, GUILayout.Height(32))) { s.Sell(t); root.selected = null; }
            if (!string.IsNullOrEmpty(t.def.synergies)) GUILayout.Label("<color=#9fe0a0>Synergien:</color> " + t.def.synergies, small);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void Report(string err)
        {
            if (err != null) root.Message(err, 1);
        }

        void DrawMessages(float H)
        {
            float y = 62f;
            if (root.sim.bossEnemy != null) y = 118f;
            for (int i = 0; i < root.messages.Count; i++)
            {
                var m = root.messages[i];
                string c = m.sev >= 2 ? "#ff9a7a" : m.sev == 1 ? "#ffd27a" : "#e6e0f0";
                float tw = Mathf.Min(720f, small.CalcSize(new GUIContent(m.text)).x + 16f);
                var rr = new Rect(12, y, Mathf.Max(200f, tw), 24);
                GUI.DrawTexture(rr, barBg);
                GUI.Label(new Rect(rr.x + 6, rr.y + 2, rr.width - 12, rr.height), $"<color={c}>{m.text}</color>", small);
                y += 26;
            }
        }

        void DrawWorldOverlays()
        {
            var cam = root.Cam;
            if (cam == null) return;
            // Lebensbalken über den Gegnern
            foreach (var e in root.sim.enemies)
            {
                if (!e.alive || e.view == null) continue;
                var wp = e.view.transform.position + Vector3.up * (e.viewHeight + 0.12f);
                var sp = cam.WorldToScreenPoint(wp);
                if (sp.z <= 0) continue;
                float x = sp.x / scale, y = (Screen.height - sp.y) / scale;
                float w = e.IsBoss ? 64f : e.elite ? 40f : 28f;
                var r = new Rect(x - w * 0.5f, y, w, 5f);
                GUI.DrawTexture(new Rect(r.x - 1, r.y - 1, r.width + 2, r.height + 2), barBg);
                float hp = Mathf.Clamp01(e.hp / Mathf.Max(1f, e.maxHp));
                Color c = hp > 0.6f ? new Color(0.45f, 0.9f, 0.35f) : hp > 0.3f ? new Color(1f, 0.8f, 0.25f) : new Color(1f, 0.3f, 0.25f);
                GUI.DrawTexture(new Rect(r.x, r.y, r.width * hp, r.height), Tex2(c));
                if (e.maxShield > 0 && e.shield > 0) GUI.DrawTexture(new Rect(r.x, r.y - 3, r.width * Mathf.Clamp01(e.shield / e.maxShield), 2), Tex2(new Color(0.5f, 0.85f, 1f)));
                string st = StatusIcons(e);
                if (st.Length > 0) GUI.Label(new Rect(x - 40, y - 16, 80, 16), st, tagStyle);
            }
            foreach (var f in root.floats)
            {
                var sp = cam.WorldToScreenPoint(f.pos);
                if (sp.z <= 0) continue;
                var c = f.color; c.a = Mathf.Clamp01(f.t * 1.5f);
                var old = GUI.color;
                GUI.color = c;
                GUI.Label(new Rect(sp.x / scale - 40, (Screen.height - sp.y) / scale - 10, 80, 20), "<b>" + f.text + "</b>", tagStyle);
                GUI.color = old;
            }
        }

        static string StatusIcons(Enemy e)
        {
            var sb = new System.Text.StringBuilder();
            if (e.Has("burn")) sb.Append("<color=#ff8040>●</color>");
            if (e.Has("poison")) sb.Append("<color=#a0e040>●</color>");
            if (e.Has("chill")) sb.Append("<color=#80d0ff>●</color>");
            if (e.Has("freeze")) sb.Append("<color=#c0f0ff>■</color>");
            if (e.Has("mark")) sb.Append("<color=#c080ff>◆</color>");
            if (e.Has("stun") || e.staggerT > 0) sb.Append("<color=#ffff80>★</color>");
            if (e.phaseShift > 0) sb.Append("<color=#80ffff>◌</color>");
            return sb.ToString();
        }

        void DrawEnd(float W, float H)
        {
            var s = root.sim;
            bool win = s.phase == Phase.Victory;
            var r = Panel(new Rect(W * 0.5f - 200, H * 0.5f - 120, 400, 240));
            GUILayout.BeginArea(new Rect(r.x + 16, r.y + 14, r.width - 32, r.height - 28));
            GUILayout.Label(win ? "<size=30>Sieg!</size>" : "<size=30>Niederlage</size>", title);
            GUILayout.Label(win ? "Der Kern hält stand." : $"Der Kern fiel in Welle {s.wave}.", label);
            GUILayout.Label($"Besiegte Gegner: {s.kills} · Durchgelassen: {s.leaks} · Türme: {s.towers.Count}", small);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Nochmal", bigButton, GUILayout.Height(44))) root.StartGame(s.map, s.diff.id, s.endless);
            if (GUILayout.Button("Hauptmenü", bigButton, GUILayout.Height(44))) root.QuitToMenu();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        void DrawPause(float W, float H)
        {
            var r = Panel(new Rect(W * 0.5f - 160, H * 0.5f - 90, 320, 180));
            GUILayout.BeginArea(new Rect(r.x + 16, r.y + 14, r.width - 32, r.height - 28));
            GUILayout.Label("<size=26>Pause</size>", title);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Weiter", bigButton, GUILayout.Height(40))) root.paused = false;
            if (GUILayout.Button("Hauptmenü", button, GUILayout.Height(32))) root.QuitToMenu();
            GUILayout.EndArea();
        }

        void DrawHelp(float W, float H)
        {
            float w = Mathf.Min(520f, W - 40f);
            var r = Panel(new Rect((W - w) * 0.5f, 120, w, 330));
            GUILayout.BeginArea(new Rect(r.x + 14, r.y + 10, r.width - 28, r.height - 20));
            GUILayout.Label("<b>So spielst du</b>", title);
            GUILayout.Label("• Türme unten wählen und auf freie Felder neben dem Weg setzen. Anhöhen (hellere Felder) geben +0,4 Reichweite, Leylinien +15 % Schaden.\n" +
                            "• Türme anklicken: dreimal ausbauen, dann eine von zwei Spezialisierungen wählen. Zwei passende, voll ausgebaute Türme nebeneinander lassen sich zu legendären Türmen verschmelzen.\n" +
                            "• Elemente reagieren: Feuer auf Kälte = Dampfstoß (nass), Blitz auf nass = Überladung, Frost auf Brand = Thermoschock, Schall auf gefroren = Resonanzbruch, Feuer auf Gift = Giftbrand.\n" +
                            "• Bosse haben Phasen. Resonatoren und Frost brechen ihre Standfestigkeit.\n" +
                            "• Nach jeder Welle gibt es Gold, Goldgräber bringen zusätzlich Einnahmen.", small);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Schließen", button, GUILayout.Height(30))) showHelp = false;
            GUILayout.EndArea();
        }
    }
}
