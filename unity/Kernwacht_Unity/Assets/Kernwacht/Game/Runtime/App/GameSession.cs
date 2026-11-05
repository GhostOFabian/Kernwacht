using System;
using System.Collections.Generic;
using System.Linq;
using Kernwacht.Game.UI;
using Kernwacht.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Kernwacht.Game
{
    // Übersetzung von app/GameSession.ts.
    // Spielsitzung: verbindet Simulation, Darstellung, Eingabe, HUD und Audio.
    // Die Simulation läuft mit festem Takt (sim.advance); die Darstellung liest nur.
    //
    // PORT: Zahlen aus der Simulation sind double; Kamera-/Bildschirmwerte (IsoCamera, Zeiger) float.
    // Optionale Felder von ViewUiState/HudView/Floater werden über N()/B() gelesen, damit es gleich ist,
    // ob der jeweilige Übersetzer sie als double/double? bzw. bool/bool? angelegt hat.

    public class GameSession : HudActions, HeroHudActions
    {
        static readonly Dictionary<string, string> SHOT_SFX = new Dictionary<string, string>
        {
            { "ranger", "shot_ranger" }, { "ember", "shot_ember" }, { "ballista", "shot_ballista" }, { "cannon", "shot_cannon" }, { "spore", "shot_spore" },
            { "rune", "beam" }, { "riftbinder", "rift" }, { "pylon", "zap" }, { "meteor", "shot_meteor" }, { "starfall", "shot_meteor" }, { "laser", "beam" }, { "gravity", "gravity" },
            { "glacier", "shot_spore" }, { "voidlance", "shot_ballista" }, { "dragon", "shot_ember" },
        };

        /// <summary>PORT: Kopie von PRIORITY_ORDER aus ui/text.ts (gleiche Reihenfolge), um keine Abhängigkeit vom Text-Modul zu erzeugen.</summary>
        static readonly string[] PRIORITY_ORDER = { "first", "exit", "strong", "weak", "close", "random" };

        static readonly int[][] NEIGHBORS = { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 }, new[] { 1, 1 }, new[] { -1, 1 }, new[] { 1, -1 }, new[] { -1, -1 } };

        public readonly Hud hud;
        readonly InputController input;
        readonly ViewUiState ui;
        readonly HudView hv;
        bool ended = false;
        double endTimer = 0;
        double fpsAcc = 0;
        double fpsFrames = 0;
        double lowFpsTime = 0;
        public double adaptiveScale = 1;
        int floaterCount = 0;
        double slowmo = 0;
        string lastPhase;
        bool keepBuilding = false;
        /// <summary>Helden-Endlos: HUD-Erweiterung und Zielwahl einer Fähigkeit (-1 = keine).</summary>
        public readonly HeroHud heroHud = null;
        int targeting = -1;
        bool champSelected = false;

        readonly GameApp app;
        public readonly GameSimulation sim;

        static double N(double? v) => v ?? 0;
        static bool B(bool? v) => v ?? false;
        static double Now() => Time.realtimeSinceStartupAsDouble * 1000.0;
        static double Rand() => UnityEngine.Random.value;

        static Transform Tr(object o)
        {
            if (o is Component c) return c != null ? c.transform : null;
            if (o is GameObject g) return g != null ? g.transform : null;
            return null;
        }

        public GameSession(GameApp app, GameSimulation sim)
        {
            this.app = app;
            this.sim = sim;
            // PORT: Schalter __KW_E2E_NOLIMIT der automatisierten Browsertests entfällt.
            var st = app.save.settings;
            ui = new ViewUiState
            {
                buildDef = null, hoverCell = null, preview = null, selectedTower = 0, showRanges = st.showRanges, showGrid = st.showGrid,
                reducedMotion = st.reducedMotion, particleCap = app.particleCap(), selectedHero = 0, heroPlacing = null, highlight = new List<double>(),
            };
            hv = new HudView
            {
                buildDef = null, selectedTower = 0, pendingCell = null, pendingCheck = null,
                // TS: matchMedia('(pointer: coarse)')
                touchMode = Application.isMobilePlatform,
                infoEnemy = null, showRanges = st.showRanges, placingHero = null, selectedHero = 0, selectedEnemy = 0, rotationLocked = st.lockRotation,
                miniPaused = false, selectedObstacle = -1, groupDef = null,
            };
            // PORT: TS übergibt zusätzlich app.icons (IconFactory); in Unity ist Icons statisch.
            hud = new Hud(sim, this, hv, sim.s.map.variant == "mazing");
            hud.onModInfo = (id) => app.toast($"{Modifiers.MODIFIERS[id].icon} {Modifiers.MODIFIERS[id].name}: {Modifiers.MODIFIERS[id].description}", "info", 4500);
            if (sim.s.he != null)
            {
                heroHud = new HeroHud(sim, this);
                attach(heroHud.resBar, hud.statsBox);
                attach(heroHud.dock, hud.stageLeft);
                attach(heroHud.win, hud.stage);
                // PORT: TS hud.root.classList.add('hero-endless') – CSS-Varianten übernimmt der HUD-Übersetzer anhand von sim.s.he.
            }
            attach(hud.root, app.hudLayer);
            // TS: hud.stage.prepend(app.canvas) – in Unity zeichnet die Kamera hinter dem Canvas über den ganzen Bildschirm.
            app.view.setMap(sim.s.map, app.detailLevel());
            app.view.onFloater = (f) => floater(f);
            app.view.onBossAppear = (b) =>
            {
                hud.bossBanner(b);
                app.audio.play("bossIntro");
            };
            input = new InputController(new InputHandlers
            {
                tap = (x, y, type, time) => tap(x, y, type, time),
                hover = (x, y) => hover(x, y),
                hoverEnd = () => setHover(null),
                pan = (dx, dy) => app.view.cam.Pan(dx, dy),
                zoom = (f, x, y) => app.view.cam.ZoomBy(f, new Vector2(x, y)),
                orbit = (dy, dp) => orbit(dy, dp),
                key = (e) => key(e),
                contextmenu = (x, y) => onContext(x, y),
            });
            lastPhase = sim.s.phase;
            app.resize();
            var cam = app.view.cam;
            cam.Fit();
            // 2.9: etwas näher starten – Felder und Lanes wirken größer (ganze Karte über den Knopf „Ganze Karte“)
            cam.zoom = Mathf.Max(cam.minZoom, cam.zoom * 0.62f);
            cam.Clamp();
            if (!st.tutorialSeen && sim.s.wave == 0 && sim.s.he == null) hud.startTutorial();
            if (sim.s.he != null)
            {
                var c = sim.s.he.champion;
                cam.tx = (float)c.x;
                cam.tz = (float)c.y;
                cam.Clamp();
                app.toast($"Vorbereitung: {Math.Ceiling(sim.s.he.autoTimer)} s bis zur ersten Welle. Klick/Rechtsklick bewegt den Helden, A/S/D/W wirken Fähigkeiten, Tab öffnet den Charakter, X den Laden.", "info", 7000);
            }
            app.audio.intensity = 0;
            saveResume();
        }

        static void attach(object child, object parent)
        {
            var c = Tr(child);
            var p = Tr(parent);
            if (c == null || p == null) return;
            if (c.parent != p) c.SetParent(p, false);
        }

        /// <summary>TS onContext (Rechtsklick auf die Zeichenfläche).</summary>
        void onContext(float px, float py)
        {
            if (movingTower != 0)
            {
                stopMove();
                return;
            }
            var he = sim.s.he;
            if (he != null && ui.buildDef == null && !hud.hasModal())
            {
                if (targeting >= 0)
                {
                    setTargeting(-1);
                    return;
                }
                var cell = app.view.pickCell(px, py, sim.s.map);
                if (cell != null) moveChampionTo(cell.x, cell.y);
                return;
            }
            cancel();
        }

        public void dispose()
        {
            input.dispose();
            app.view.cam.topDown = false;
            app.view.onFloater = null;
            app.view.onBossAppear = null;
            if (!ended) saveResume();
        }

        // ------------------------------------------------------------ Frame

        public void frame(double dt)
        {
            input.update();
            var s = sim.s;
            bool blocking = hud.tutorialActive;
            // Kurze Zeitlupe nach einem Bosssieg (rein darstellerisch: die Simulation bleibt im festen Takt).
            double slow = slowmo > 0 ? 0.3 : 1;
            slowmo = Math.Max(0, slowmo - dt);
            if (!blocking) sim.advance(dt * slow);
            var events = sim.drainEvents();
            if (events.Count > 0)
            {
                app.view.handleEvents(events, sim, ui);
                handleEvents(events);
            }
            if (s.phase != lastPhase) onPhaseChange(lastPhase, s.phase);
            // Angeklickter Gegner gestorben oder durchgebrochen?
            if (hv.selectedEnemy != 0 && !(sim.c.enemyByUid.TryGetValue(hv.selectedEnemy, out var selE) && selE.alive)) hv.selectedEnemy = 0;
            ui.selectedEnemy = hv.selectedEnemy;
            if (hv.selectedObstacle >= 0 && s.map.cells[(int)hv.selectedObstacle] != Cell.Blocked && s.he?.mining?.cell != hv.selectedObstacle) hv.selectedObstacle = -1;
            // Ausgewählter Turm wurde entfernt?
            if (ui.selectedTower != 0 && !sim.c.towerByUid.ContainsKey(ui.selectedTower)) select(0);
            // Mazing: Vorschau aktualisieren, wenn sich Gegner bewegen (Blockade-Prüfung).
            if (ui.buildDef != null && ui.hoverCell != null && s.map.variant == "mazing" && s.tick % 6 == 0) refreshPreview();
            app.audio.intensity = s.boss != null ? 2 : s.phase == "combat" ? 1 : 0;
            ui.particleCap = app.particleCap();
            // Helden: Auswahl/Platzierung gültig halten
            double selHeroUid = N(ui.selectedHero);
            if (selHeroUid != 0 && !s.heroes.Any((x) => x.uid == selHeroUid && x.state == "active")) selectHero(0);
            if (ui.heroPlacing != null)
            {
                var ph = s.heroes.FirstOrDefault((x) => x.id == ui.heroPlacing);
                if (ph == null || ph.state != "ready") setPlacing(null);
            }
            // Fusionspartner hervorheben: beim ausgewählten Turm und schon beim Platzieren eines neuen Turms
            TowerInstance sel = null;
            if (ui.selectedTower != 0) sim.c.towerByUid.TryGetValue(ui.selectedTower, out sel);
            if (sel != null && hv.groupDef == sel.def) ui.highlight = s.towers.Where((o) => o.def == sel.def && o != sel).Select((o) => o.uid).ToList();
            else if (sel != null) ui.highlight = EconomySystem.fusionOptions(sim, sel).Select((f) => f.partner.uid).ToList();
            else if (ui.buildDef != null && ui.hoverCell != null)
            {
                var fp = EconomySystem.fusionPreview(sim, ui.buildDef, ui.hoverCell.x, ui.hoverCell.y);
                ui.highlight = fp.SelectMany((f) => f.partners.Select((p) => p.uid)).ToList();
                ui.fusionRing = fp.Count > 0 ? Towers.FUSION_RANGE : 0;
            }
            else ui.highlight = new List<double>();
            if (ui.buildDef == null) ui.fusionRing = 0;
            var cam = app.view.cam;
            if (hud.followBoss && s.boss != null)
            {
                if (sim.c.enemyByUid.TryGetValue(s.boss.uid, out var b) && b != null && b.alive && !cam.Focusing)
                {
                    float k = (float)Math.Min(1, dt * 2.5);
                    cam.tx += ((float)b.x - cam.tx) * k;
                    cam.tz += ((float)b.y - cam.tz) * k;
                }
            }
            if (heroHud != null && s.he != null)
            {
                var c = s.he.champion;
                if (heroHud.follow && c.dead <= 0 && !cam.Focusing)
                {
                    float k = (float)Math.Min(1, dt * 4);
                    cam.tx += ((float)c.x - cam.tx) * k;
                    cam.tz += ((float)c.y - cam.tz) * k;
                }
                if (targeting >= 0)
                {
                    var chk = ChampionSystem.skillState(sim, targeting);
                    if (!chk.ok) setTargeting(-1);
                }
                ui.champSelected = champSelected;
            }
            app.view.frame(sim, ui, dt * slow);
            hud.camQuad = cameraQuad();
            hud.update();
            heroHud?.update();
            adapt(dt);
            if (sim.isOver() && !ended)
            {
                endTimer += dt;
                if (endTimer > 1.6) finish();
            }
        }

        /// <summary>Adaptive Auflösung: bei dauerhaft niedriger Bildrate wird die Pixeldichte reduziert.</summary>
        void adapt(double dt)
        {
            if (app.save.settings.quality != "auto") return;
            fpsAcc += dt;
            fpsFrames++;
            if (fpsAcc < 1) return;
            double fps = fpsFrames / fpsAcc;
            fpsAcc = 0;
            fpsFrames = 0;
            if (fps < 38) lowFpsTime++;
            else lowFpsTime = 0;
            if (lowFpsTime >= 3)
            {
                lowFpsTime = 0;
                // Erst Effekte (Kantenglättung, Schatten, Glühen) reduzieren, dann die Auflösung.
                if (!app.lowerAutoQuality() && adaptiveScale > 0.55)
                {
                    adaptiveScale = Math.Max(0.55, adaptiveScale - 0.15);
                    app.resize();
                }
            }
        }

        /// <summary>Kameraausschnitt in Weltkoordinaten (für die Minikarte). PORT: TS [number, number][] → List&lt;double[]&gt;.</summary>
        List<double[]> cameraQuad()
        {
            var c = app.view.cam;
            var pts = new List<double[]>();
            float[][] corners = { new[] { 0f, 0f }, new[] { c.width, 0f }, new[] { c.width, c.height }, new[] { 0f, c.height } };
            foreach (var xy in corners)
            {
                var g = c.ScreenToGround(new Vector2(xy[0], xy[1]), 0);
                if (!g.HasValue) return new List<double[]>();
                pts.Add(new double[] { g.Value.x, g.Value.z });
            }
            return pts;
        }

        void onPhaseChange(string prev, string next)
        {
            lastPhase = next;
            var he = sim.s.he;
            if (he != null && next == "build")
            {
                // Bestwelle wird sofort dauerhaft gesichert (nicht erst bei Partieende).
                var m = app.save.progress.heroMeta;
                m.bestWave = Math.Max(m.bestWave, sim.s.stats.wavesCleared);
                Progress.commitHeroProgress(app.save, sim.s);
                app.persist();
            }
            if (next == "build")
            {
                saveResume();
                if (prev == "choice") hud.forcePanelRefresh();
            }
        }

        void saveResume()
        {
            var s = sim.s;
            if (sim.isOver()) return;
            if (!SaveSystem.canSerializeRun(s)) return;
            app.save.resume = sim.serialize();
            app.persist();
        }

        void finish()
        {
            ended = true;
            var save = app.save;
            save.resume = null;
            var record = Progress.recordRun(save, sim.s);
            app.persist();
            if (record.newAchievements.Count > 0) app.audio.play("achievement");
            app.go("results", new ScreenParams { results = new ResultParams { state = sim.s, record = record } });
        }

        /// <summary>Simulation ohne Darstellung weiterrechnen (Tab im Hintergrund).</summary>
        public void simOnly(double dt)
        {
            if (hud.tutorialActive) return;
            double left = Math.Max(0, dt);
            while (left > 1e-6)
            {
                double step = Math.Min(0.1, left);
                left -= step;
                sim.advance(step);
                var events = sim.drainEvents();
                if (events.Count > 0) handleEvents(events);
                if (sim.s.phase != lastPhase) onPhaseChange(lastPhase, sim.s.phase);
                if (sim.isOver() || sim.s.paused) break;
            }
        }

        public void autoPause()
        {
            if (!sim.isOver() && sim.s.phase == "combat") sim.setPaused(true);
        }

        // ------------------------------------------------------------ Ereignisse → Audio/HUD

        void handleEvents(List<GameEvent> events)
        {
            var au = app.audio;
            foreach (var e in events)
            {
                switch (e.k)
                {
                    case "shot": { if (e.tower != null && SHOT_SFX.TryGetValue(e.tower, out var id)) au.play(id, 0.8f); break; }
                    case "hit": if (e.splash > 0.9) au.play("explosion", 0.6f); else au.play("hit", 0.5f); break;
                    case "chain": au.play("zap"); break;
                    case "pulse": au.play(e.tower == "frost" ? "pulse_frost" : "pulse_sonic"); break;
                    case "kill":
                        au.play("kill");
                        if (e.isBoss)
                        {
                            au.play("victory", 0.5f);
                            if (!app.save.settings.reducedMotion) slowmo = 1.4;
                        }
                        break;
                    case "leak": au.play("leak"); break;
                    case "reaction": au.play(e.id == "shatter" ? "freeze" : "reaction"); break;
                    case "gold": au.play("gold"); break;
                    case "build": au.play("build"); break;
                    case "sell": au.play("sell"); break;
                    case "upgrade": au.play("upgrade"); break;
                    case "waveStart":
                    {
                        au.play(e.isBoss ? "bossWarn" : "waveStart");
                        var w = sim.s.currentWave;
                        string total = sim.s.rules.endless ? "" : $" von {U.Fmt(sim.s.rules.waves)}";
                        string wave = U.Fmt(e.wave);
                        hud.banner($"Welle {wave}",
                            e.isBoss ? "Bosswelle!" : !string.IsNullOrEmpty(w?.@event) ? $"Geländeereignis aktiv{total}" : total != "" ? $"Welle {wave}{total}" : "Endlos",
                            e.isBoss ? "boss" : "wave", 1900);
                        break;
                    }
                    case "waveClear": au.play("waveClear"); hud.banner($"Welle {U.Fmt(e.wave)} überstanden", $"+{U.Fmt(e.bonus)} Gold", "clear", 1800); break;
                    case "bossPhase":
                        au.play("bossWarn");
                        if (e.phase > 0) hud.banner(e.name, Enemies.BOSSES[e.boss].name, "info", 2000);
                        break;
                    case "heroDeploy": au.play("heroDeploy"); break;
                    case "heroDown": au.play("heroDown"); app.toast($"{Heroes.HEROES[e.hero].name} ist gefallen und braucht länger zur Erholung.", "warn", 3200); break;
                    case "heroAbility": au.play("heroAbility", 0.8f); break;
                    case "heroAttack": if (Rand() < 0.5) au.play(e.melee ? "hit" : "shot_ranger", 0.4f); break;
                    case "heroLevel": au.play("levelUp"); break;
                    case "meteor": au.play("explosion", e.big ? 1f : 0.6f); break;
                    case "fusion": au.play("fusion"); hud.banner(Towers.TOWERS[e.tower].name, "Legendärer Turm erschaffen!", "win", 2200); break;
                    case "towerActive": au.play("towerActive"); break;
                    case "plateBreak": au.play("shatter"); break;
                    case "devour": au.play("devour"); break;
                    case "burst": au.play("explosion", 0.7f); break;
                    case "heal": au.play("heal", 0.5f); break;
                    case "gravity": au.play("gravity", 0.6f); break;
                    case "arc": if (Rand() < 0.3) au.play("zap", 0.4f); break;
                    case "ability":
                        if (e.stage == "warn") au.play("ability");
                        else if (e.stage == "interrupt") au.play("interrupt");
                        break;
                    case "teleport": au.play("teleport"); break;
                    case "victory": au.play("victory"); hud.banner("Sieg!", "Der Kern ist gerettet", "win", 3000); break;
                    case "defeat": au.play("defeat"); hud.banner("Niederlage", "Der Kern ist gefallen", "lose", 3000); break;
                    case "message": app.toast(e.text, e.severity ?? "info", e.severity == "danger" ? 4200 : 3000); break;
                    case "champAttack": if (Rand() < 0.5) au.play(e.melee ? "hit" : "shot_ranger", 0.4f); break;
                    case "champLevel": au.play("levelUp"); hud.banner($"Stufe {U.Fmt(e.level)}", "Neuer Fertigkeitspunkt – Tab öffnet den Skillbaum", "info", 1800); heroHud?.refresh(); break;
                    case "champDown": au.play("heroDown"); break;
                    case "champRespawn": au.play("heroDeploy"); break;
                    case "pickup":
                        if (e.kind == "item") { au.play("achievement"); app.toast($"Gefunden: {e.label}", "info", 2400); }
                        else if (e.kind != "xp") au.play("gold", 0.5f);
                        heroHud?.refresh();
                        break;
                    case "craft": app.toast($"Hergestellt: {e.label}", "info", 2200); break;
                    default: break;
                }
            }
        }

        /// <summary>Schwebende Zahl über der Welt (CSS .floater: steigt 42 px auf und verblasst in 1,1 s).</summary>
        void floater(Floater f)
        {
            if (floaterCount > 30) return;
            var parent = Tr(hud.floaters) as RectTransform;
            if (parent == null) parent = app.worldLayer;
            var p = app.view.project((float)f.x, (float)f.y, (float)f.z);
            // Bildschirm (oben links) → lokale Canvas-Koordinaten
            var screenPt = new Vector2((float)p.x, Screen.height - (float)p.y);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPt, null, out var local)) return;
            bool big = B(f.big);
            var t = Ui.Label(parent, f.text ?? "", big ? 23 : 17, parseColor(f.color), TextAnchor.MiddleCenter, FontStyle.Bold);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var r = t.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = new Vector2(0.5f, 0.5f);
            // Ankerpunkt in der Mitte des Elternrechtecks → Versatz relativ zum Mittelpunkt
            var pr = parent.rect;
            r.anchoredPosition = local - new Vector2(pr.center.x, pr.center.y);
            r.sizeDelta = new Vector2(240, 40);
            var sh = t.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, 0.9f);
            sh.effectDistance = new Vector2(1, -1);
            var ol = t.gameObject.AddComponent<Outline>();
            ol.effectColor = new Color(0, 0, 0, 0.6f);
            ol.effectDistance = new Vector2(1, 1);
            floaterCount++;
            var fx = t.gameObject.AddComponent<FloaterFx>();
            fx.reduced = app.save.settings.reducedMotion;
            fx.onDone = () => floaterCount--;
        }

        static Color parseColor(string css)
        {
            if (!string.IsNullOrEmpty(css) && ColorUtility.TryParseHtmlString(css, out var c)) return c;
            return Color.white;
        }

        // ------------------------------------------------------------ Eingabe

        void tap(float px, float py, string type, double time)
        {
            tapTime = time;
            hv.touchMode = type != "mouse";
            if (hud.hasModal()) return;
            var cell = app.view.pickCell(px, py, sim.s.map);
            if (ui.heroPlacing != null)
            {
                if (cell == null) return;
                var id = ui.heroPlacing;
                var r = sim.deployHero(id, cell.x, cell.y);
                if (!r.ok)
                {
                    app.audio.play("error");
                    app.toast(r.reason, "warn", 1800);
                    return;
                }
                setPlacing(null);
                var hero = sim.s.heroes.FirstOrDefault((x) => x.id == id);
                if (hero != null) selectHero(hero.uid);
                return;
            }
            // Legendären Turm versetzen: Zielfeld anklicken
            if (movingTower != 0)
            {
                if (cell == null) return;
                double uid = movingTower;
                var r = sim.moveTower(uid, cell.x, cell.y);
                if (!r.ok)
                {
                    app.audio.play("error");
                    app.toast(r.reason, "warn", 1800);
                    return;
                }
                stopMove();
                app.audio.play("build");
                select(uid);
                hud.forcePanelRefresh();
                return;
            }
            if (ui.buildDef != null)
            {
                if (cell == null) return;
                if (type == "mouse")
                {
                    setHover(cell);
                    tryBuild(cell);
                    return;
                }
                // Touch: erstes Tippen = Vorschau, zweites Tippen auf dasselbe Feld = Bauen
                var pc = hv.pendingCell;
                if (pc != null && pc.x == cell.x && pc.y == cell.y && hv.pendingCheck != null && hv.pendingCheck.ok)
                {
                    tryBuild(cell);
                }
                else
                {
                    setHover(cell);
                    hv.pendingCell = cell;
                }
                return;
            }
            // Gegner anklicken (Körper, nicht nur Bodenfeld) – nicht während der Ziel-Fähigkeit
            if (targeting < 0)
            {
                double foe = app.view.pickEnemy(px, py, sim);
                if (foe != 0)
                {
                    selectEnemy(foe);
                    app.audio.play("select");
                    return;
                }
            }
            // Türme über das ganze Modell anklickbar
            double picked = app.view.pickTower(px, py, sim);
            if (picked != 0)
            {
                sim.c.towerByUid.TryGetValue(picked, out var pt);
                if (pt != null && targeting >= 0)
                {
                    castAt(targeting, pt.cx + 0.5, pt.cy + 0.5);
                    return;
                }
                if (pt != null)
                {
                    selectHero(0);
                    hv.selectedEnemy = 0;
                    towerTap(picked);
                    app.audio.play("select");
                    return;
                }
            }
            // Helden-Endlos: Bäume/Felsen über das ganze Modell anklickbar (nicht nur das Bodenfeld)
            if (sim.s.he != null && targeting < 0)
            {
                double ob = app.view.pickObstacle(px, py, sim);
                var he = sim.s.he;
                if (ob >= 0 && ob != he.shop.y * sim.s.map.width + he.shop.x && !(cell != null && sim.towerAt(cell.x, cell.y) != null))
                {
                    select(0);
                    hv.selectedObstacle = ob;
                    hud.forcePanelRefresh();
                    app.audio.play("select");
                    return;
                }
            }
            if (cell == null)
            {
                select(0);
                return;
            }
            if (sim.s.he != null && heroTap(cell.x, cell.y)) return;
            var t = sim.towerAt(cell.x, cell.y);
            // Ausgewählter Held: Wegzelle antippen versetzt den Ankerpunkt
            double selUid = N(ui.selectedHero);
            var selHero = selUid != 0 ? sim.s.heroes.FirstOrDefault((x) => x.uid == selUid) : null;
            if (t == null && selHero != null && selHero.state == "active" && HeroSystem.canPlaceHero(sim, cell.x, cell.y).ok)
            {
                var r = sim.heroAnchor(selHero.id, cell.x, cell.y);
                if (r.ok && selHero.mode == "retreat") sim.heroMode(selHero.id, "hold");
                app.audio.play(r.ok ? "select" : "error");
                return;
            }
            // Held angetippt?
            var tappedHero = sim.s.heroes.FirstOrDefault((x) => x.state == "active" && Math.Floor(x.x) == cell.x && Math.Floor(x.y) == cell.y);
            if (t == null && tappedHero != null)
            {
                selectHero(tappedHero.uid == selUid ? 0 : tappedHero.uid);
                return;
            }
            if (t != null)
            {
                selectHero(0);
                towerTap(t.uid);
                app.audio.play("select");
            }
            else
            {
                selectHero(0);
                select(0);
                hv.infoEnemy = null;
            }
        }

        /// <summary>Letzte Zeigerposition über der Karte (für das Banner per Taste).</summary>
        float lastPx = -1, lastPy = -1;

        void hover(float px, float py)
        {
            lastPx = px; lastPy = py;
            hv.touchMode = false;
            if (movingTower != 0)
            {
                var c = app.view.pickCell(px, py, sim.s.map);
                ui.hoverCell = c;
                ui.preview = c != null ? sim.canMoveTower(movingTower, c.x, c.y) : null;
                return;
            }
            if (targeting >= 0)
            {
                ui.hoverCell = app.view.pickCell(px, py, sim.s.map);
                return;
            }
            if (ui.heroPlacing != null)
            {
                var c = app.view.pickCell(px, py, sim.s.map);
                ui.hoverCell = c;
                return;
            }
            if (ui.buildDef == null) return;
            setHover(app.view.pickCell(px, py, sim.s.map));
        }

        void setHover(GridPos cell)
        {
            var prev = ui.hoverCell;
            if (prev != null && cell != null && prev.x == cell.x && prev.y == cell.y) return;
            ui.hoverCell = cell;
            if (cell == null)
            {
                ui.preview = null;
                if (!hv.touchMode) hv.pendingCell = null;
                hv.pendingCheck = null;
                return;
            }
            if (!hv.touchMode) hv.pendingCell = cell;
            refreshPreview();
        }

        void refreshPreview()
        {
            var c = ui.hoverCell;
            if (c == null || ui.buildDef == null) return;
            BuildCheck chk = sim.canBuild(ui.buildDef, c.x, c.y, N(hv.buildSpec));
            ui.preview = chk;
            hv.pendingCheck = chk;
        }

        void tryBuild(GridPos cell)
        {
            var def = ui.buildDef;
            if (def == null) return;
            double spec = N(hv.buildSpec);
            var r = sim.build(def, cell.x, cell.y, spec);
            if (!r.ok)
            {
                app.audio.play("error");
                app.toast(r.reason, "warn", 1800);
                refreshPreview();
                return;
            }
            double cost = sim.canBuild(def, cell.x, cell.y, spec).cost;
            bool canAgain = sim.s.gold >= cost;
            if ((keepBuilding || def == "barricade") && canAgain)
            {
                ui.hoverCell = null;
                hv.pendingCell = null;
                hv.pendingCheck = null;
                ui.preview = null;
                setHover(cell);
            }
            else
            {
                selectBuild(null);
            }
            saveResumeIfBuild();
        }

        void saveResumeIfBuild()
        {
            // Bauaktionen in der Bauphase werden sofort gesichert (für Fortsetzen).
            if (sim.s.phase == "build") saveResume();
        }

        static bool isShiftDigit(KeyEvt e)
        {
            if (!e.shiftKey || e.code == null || e.code.Length != 6 || !e.code.StartsWith("Digit")) return false;
            char d = e.code[5];
            return d >= '1' && d <= '5';
        }

        void key(KeyEvt e)
        {
            if (e.ctrlKey || e.metaKey || e.altKey) return;
            keepBuilding = e.shiftKey;
            string raw = (e.key ?? "").ToLowerInvariant();
            // Eigene Tastenbelegung → Standardtaste der Aktion (Umschalt+Ziffer bleibt für Tränke)
            string tr = isShiftDigit(e) ? "" : Keys.translateKey(raw, sim.s.he != null ? "hero" : "lane");
            if (tr == null) return;
            string k = tr != "" ? tr : raw;
            if (hud.hasModal())
            {
                if ((k == "escape" || k == "p") && sim.s.paused)
                {
                    e.preventDefault();
                    togglePause();
                }
                return;
            }
            if (sim.s.he != null && heroKey(e, k)) return;
            bool mazing = sim.s.map.variant == "mazing";
            string tower = Towers.BUILD_ORDER.FirstOrDefault((id) =>
            {
                var d = Towers.TOWERS[id];
                return (d.hotkey == k || (k == "ß" && d.hotkey == "-")) && !(B(d.mazingOnly) && !mazing) && !(B(d.laneOnly) && mazing);
            });
            if (tower != null)
            {
                e.preventDefault();
                selectBuild(ui.buildDef == tower ? null : tower);
                return;
            }
            var heroes = sim.s.heroes;
            if ((k == "z" || k == "y" || k == "x") && heroes.Count > 0)
            {
                int idx = k == "x" ? 1 : 0;
                var hero = idx < heroes.Count ? heroes[idx] : null;
                if (hero != null)
                {
                    e.preventDefault();
                    heroCard(hero.id);
                }
                return;
            }
            if ((k == "a" || k == "s" || k == "d") && heroes.Count > 0)
            {
                var hero = focusHero();
                if (hero != null)
                {
                    e.preventDefault();
                    heroAbility(hero.id, k == "a" ? 0 : k == "s" ? 1 : 2);
                }
                return;
            }
            switch (k)
            {
                case "c":
                    if (ui.selectedTower != 0) towerActive(ui.selectedTower);
                    break;
                case "v":
                {
                    var hero = focusHero();
                    if (hero != null)
                    {
                        string[] order = { "hold", "advance", "retreat" };
                        heroMode(hero.id, order[(Array.IndexOf(order, hero.mode) + 1) % order.Length]);
                    }
                    break;
                }
                case " ":
                    e.preventDefault();
                    if (sim.s.phase == "build" && !sim.s.paused) startWave();
                    else miniPause();
                    break;
                case "escape":
                    if (movingTower != 0)
                    {
                        stopMove();
                        break;
                    }
                    if (targeting >= 0)
                    {
                        setTargeting(-1);
                        break;
                    }
                    if (heroHud != null && heroHud.close()) break;
                    if (hud.towerListOpen)
                    {
                        hud.toggleTowerList(false);
                        break;
                    }
                    if (hv.selectedEnemy != 0)
                    {
                        selectEnemy(0);
                        break;
                    }
                    if (ui.buildDef != null || ui.selectedTower != 0 || hv.infoEnemy != null || ui.heroPlacing != null || N(ui.selectedHero) != 0) cancel();
                    else togglePause();
                    break;
                case "p": togglePause(); break;
                case "o": hud.toggleTowerList(); break;
                case "enter":
                    if (sim.s.he != null)
                    {
                        e.preventDefault();
                        startWave();
                    }
                    break;
                case "f": cycleSpeed(); break;
                case "q": rotate(-1); break;
                case "e": rotate(1); break;
                case ",": orbit(-Mathf.PI / 12, 0); break;
                case ".": orbit(Mathf.PI / 12, 0); break;
                case "pageup": e.preventDefault(); orbit(0, (5 * Mathf.PI) / 180); break;
                case "pagedown": e.preventDefault(); orbit(0, (-5 * Mathf.PI) / 180); break;
                case "home": e.preventDefault(); resetView(); break;
                case "r": toggleRanges(); break;
                case "g":
                    ui.showGrid = !ui.showGrid;
                    app.save.settings.showGrid = ui.showGrid;
                    app.persist();
                    break;
                case "h": case "?": help(); break;
                case "u": if (ui.selectedTower != 0) upgrade(ui.selectedTower); break;
                case "t":
                {
                    if (sim.c.towerByUid.TryGetValue(ui.selectedTower, out var t) && t != null)
                    {
                        var opts = PRIORITY_ORDER.Where((p) => p != "random" || Towers.TOWERS[t.def].attack == "rift").ToList();
                        setPriority(t.uid, opts[(opts.IndexOf(t.priority) + 1) % opts.Count]);
                    }
                    break;
                }
                case "delete": case "backspace":
                    if (ui.selectedTower != 0) clickSellButton();
                    break;
                default: break;
            }
        }

        /// <summary>TS: hud.root.querySelector('[data-testid="sell"]').click() – zweistufiger Verkauf über den Knopf im Turmpanel.</summary>
        void clickSellButton()
        {
            hud.clickSell();
        }

        // ------------------------------------------------------------ HudActions

        public void selectBuild(string def, double? spec = null)
        {
            hv.buildSpec = def != null ? (spec ?? 0) : 0;
            if (def != null && sim.s.rules.bannedTowers.Contains(def)) return;
            if (def != null && sim.s.he != null && !PassiveTree.towerUnlocked(def, sim.s.he, sim.s.wave))
            {
                app.audio.play("error");
                app.toast($"{Towers.TOWERS[def].name} wird ab Welle {U.Fmt(PassiveTree.towerUnlockWave(def, sim.s.he))} freigeschaltet.", "info", 1800);
                return;
            }
            if (def != null)
            {
                setPlacing(null);
                selectHero(0);
            }
            ui.buildDef = def;
            hv.buildDef = def;
            hv.pendingCell = null;
            hv.pendingCheck = null;
            ui.preview = null;
            ui.hoverCell = null;
            if (def != null)
            {
                select(0);
                hv.infoEnemy = null;
                app.audio.play("ui");
            }
            hud.forcePanelRefresh();
        }

        public void confirmBuild()
        {
            var c = hv.pendingCell;
            if (c != null) tryBuild(c);
        }

        /// <summary>Versetzen eines legendären Turms beginnen: danach das Zielfeld anklicken (Esc/Rechtsklick bricht ab).</summary>
        public void startMove(double uid)
        {
            if (!sim.c.towerByUid.TryGetValue(uid, out var t) || t == null) return;
            selectBuild(null);
            movingTower = uid;
            ui.moveDef = t.def;
            app.toast("Zielfeld anklicken – Esc bricht ab. Danach kurz außer Gefecht.", "info", 2500);
        }

        public void stopMove()
        {
            movingTower = 0;
            ui.moveDef = null;
            ui.preview = null;
        }

        double movingTower = 0;

        public void cancel()
        {
            stopMove();
            selectBuild(null);
            select(0);
            setPlacing(null);
            selectHero(0);
            hv.infoEnemy = null;
            hv.selectedEnemy = 0;
            hv.selectedObstacle = -1;
            hud.forcePanelRefresh();
        }

        public void selectEnemy(double uid)
        {
            hv.selectedEnemy = uid;
            if (uid != 0)
            {
                select(0);
                hv.infoEnemy = null;
                ui.selectedHero = 0;
                hv.selectedHero = 0;
            }
            hud.forcePanelRefresh();
        }

        public void toggleRotationLock()
        {
            var st = app.save.settings;
            st.lockRotation = !st.lockRotation;
            hv.rotationLocked = st.lockRotation;
            app.view.cam.lockRotation = st.lockRotation;
            app.persist();
            app.toast(st.lockRotation ? "Kameradrehung gesperrt – Q/E drehen nicht mehr" : "Kameradrehung freigegeben", "info", 1400);
        }

        public void selectTower(double uid)
        {
            select(uid);
        }

        void select(double uid)
        {
            if (ui.selectedTower == uid) return;
            TowerInstance nt = null;
            if (uid != 0) sim.c.towerByUid.TryGetValue(uid, out nt);
            if (nt == null || nt.def != hv.groupDef) hv.groupDef = null;
            ui.selectedTower = uid;
            hv.selectedTower = uid;
            hud.disarmSell();
            if (uid != 0)
            {
                hv.infoEnemy = null;
                hv.selectedEnemy = 0;
                hv.selectedObstacle = -1;
                ui.selectedHero = 0;
                hv.selectedHero = 0;
            }
        }

        void result(CommandResult r, string sfxOk = null)
        {
            if (!r.ok)
            {
                app.audio.play("error");
                app.toast(r.reason ?? "Nicht möglich", "warn", 1800);
            }
            else if (sfxOk != null) app.audio.play(sfxOk);
            hud.forcePanelRefresh();
            saveResumeIfBuild();
        }

        public void upgrade(double uid) { result(sim.upgrade(uid)); }

        public void upgradeAll(List<double> uids = null)
        {
            var r = sim.upgradeAll(uids);
            result(r, "upgrade");
            if (r.ok && !string.IsNullOrEmpty(r.message)) app.toast(r.message, "info", 1600);
        }

        public void clearGroup() { hv.groupDef = null; hud.forcePanelRefresh(); }

        /// <summary>Doppelklick/-tipp auf einen Turm wählt alle Türme derselben Sorte.</summary>
        void towerTap(double uid)
        {
            // Zeitstempel des Klick-Ereignisses (nicht der Verarbeitung): Doppelklicks klappen auch bei niedriger Bildrate
            double now = tapTime;
            var last = lastTowerTap;
            lastTowerTap = new TowerTapInfo { uid = uid, t = now };
            sim.c.towerByUid.TryGetValue(uid, out var t);
            if (t != null && last != null && last.uid == uid && now - last.t < 420)
            {
                lastTowerTap = null;
                if (ui.selectedTower != uid) select(uid);
                hv.groupDef = t.def;
                int n = sim.s.towers.Count((o) => o.def == t.def);
                app.toast($"{n}× {Towers.TOWERS[t.def].name} ausgewählt – gemeinsam aufwerten", "info", 1400);
                hud.forcePanelRefresh();
                return;
            }
            select(uid == ui.selectedTower && hv.groupDef == null ? 0 : uid);
        }

        class TowerTapInfo { public double uid; public double t; }
        TowerTapInfo lastTowerTap = null;
        double tapTime = 0;

        public void specialize(double uid, double spec) { result(sim.specialize(uid, spec)); }
        public void star(double uid) { result(sim.star(uid), "upgrade"); }

        public void starAll(List<double> uids = null)
        {
            var r = sim.starAll(uids);
            result(r, "upgrade");
            if (r.ok && !string.IsNullOrEmpty(r.message)) app.toast(r.message, "info", 1600);
        }

        public void respec(double uid, double spec) { result(sim.respec(uid, spec), "upgrade"); }

        public void groupSpec(List<double> uids, double spec)
        {
            var r = sim.groupSpec(uids, spec);
            result(r, "upgrade");
            if (r.ok && !string.IsNullOrEmpty(r.message)) app.toast(r.message, "info", 2200);
        }

        /// <summary>Alle (bzw. die angegebenen) Türme verkaufen – nicht in der Mini-Pause.</summary>
        public void sellAll(List<double> uids = null)
        {
            if (sim.s.paused && hv.miniPaused)
            {
                app.audio.play("error");
                app.toast("In der Mini-Pause können keine Türme verkauft werden.", "warn", 1800);
                return;
            }
            var r = sim.sellAll(uids);
            result(r, "sell");
            if (r.ok && !string.IsNullOrEmpty(r.message)) app.toast(r.message, "info", 1800);
            select(0);
        }

        public void sell(double uid)
        {
            if (sim.s.paused && hv.miniPaused)
            {
                app.audio.play("error");
                app.toast("In der Mini-Pause können keine Türme verkauft werden.", "warn", 1800);
                return;
            }
            result(sim.sell(uid));
            select(0);
        }

        public void setPriority(double uid, string p) { result(sim.setPriority(uid, p), "ui"); }

        public void startWave()
        {
            if (sim.s.paused) sim.setPaused(false);
            hv.miniPaused = false;
            var r = sim.s.he != null ? sim.nextWave() : sim.startWave();
            if (!r.ok) app.toast(r.reason, "warn", 1500);
            selectBuild(null);
        }

        public void togglePause()
        {
            if (sim.isOver()) return;
            if (sim.s.paused && hv.miniPaused)
            {
                // Aus der Mini-Pause ins Pausenmenü wechseln
                hv.miniPaused = false;
                app.audio.play("ui");
                return;
            }
            hv.miniPaused = false;
            sim.setPaused(!sim.s.paused);
            app.audio.play("ui");
        }

        /// <summary>Mini-Pause: anhalten ohne Menü (Bauen, Aufwerten, Fähigkeiten bleiben bedienbar).</summary>
        public void miniPause()
        {
            if (sim.isOver()) return;
            if (sim.s.paused)
            {
                hv.miniPaused = false;
                sim.setPaused(false);
            }
            else
            {
                hv.miniPaused = true;
                sim.setPaused(true);
            }
            app.audio.play("ui");
        }

        public void cycleSpeed()
        {
            double s = sim.s.speed;
            var list = Balance.SIM.speeds;
            int i = list.IndexOf(s);
            sim.setSpeed(list[(i + 1) % list.Count]);
            app.audio.play("ui");
        }

        public void chooseModifier(string id)
        {
            result(sim.chooseModifier(id), "upgrade");
        }

        // ------------------------------------------------------------ Helden & Turmfähigkeiten

        /// <summary>Held, auf den sich A/S/D/V beziehen: ausgewählter oder einziger aktiver Held.</summary>
        HeroInstance focusHero()
        {
            var hs = sim.s.heroes;
            double selUid = N(ui.selectedHero);
            var sel = hs.FirstOrDefault((x) => x.uid == selUid && x.state == "active");
            if (sel != null) return sel;
            var active = hs.Where((x) => x.state == "active").ToList();
            return active.Count >= 1 ? active[0] : null;
        }

        void setPlacing(string id)
        {
            ui.heroPlacing = id;
            hv.placingHero = id;
            ui.hoverCell = null;
            hud.forcePanelRefresh();
        }

        void selectHero(double uid)
        {
            if (N(ui.selectedHero) == uid) return;
            ui.selectedHero = uid;
            hv.selectedHero = uid;
            if (uid != 0)
            {
                select(0);
                hv.infoEnemy = null;
            }
            hud.forcePanelRefresh();
        }

        public void heroCard(string id)
        {
            var hero = sim.s.heroes.FirstOrDefault((x) => x.id == id);
            if (hero == null) return;
            if (hero.state == "ready")
            {
                if (ui.heroPlacing == id)
                {
                    setPlacing(null);
                    return;
                }
                if (sim.s.riftEnergy < Heroes.HEROES[id].cost)
                {
                    app.audio.play("error");
                    app.toast($"Nicht genug Rissenergie ({U.Fmt(Math.Floor(sim.s.riftEnergy))}/{U.Fmt(Heroes.HEROES[id].cost)}).", "warn", 1800);
                    return;
                }
                selectBuild(null);
                select(0);
                selectHero(0);
                setPlacing(id);
                app.audio.play("ui");
                return;
            }
            if (hero.state == "active")
            {
                setPlacing(null);
                bool willSelect = N(ui.selectedHero) != hero.uid;
                selectHero(willSelect ? hero.uid : 0);
                if (willSelect)
                {
                    var c = app.view.cam;
                    c.tx = (float)hero.x;
                    c.tz = (float)hero.y;
                    c.Clamp();
                }
                app.audio.play("select");
                return;
            }
            app.toast($"{Heroes.HEROES[id].name} erholt sich noch {U.Fmt(Math.Ceiling(hero.cooldown))} s.", "info", 1500);
        }

        public void heroMode(string id, string mode) { result(sim.heroMode(id, mode), "ui"); }
        public void heroAbility(string id, double slot) { result(sim.heroAbility(id, slot)); }
        public void heroPerk(string id, double choice) { result(sim.heroPerk(id, choice), "upgrade"); }
        public void towerActive(double uid) { result(sim.towerActive(uid)); }

        public void fuse(double uid, double partner)
        {
            var r = sim.fuse(uid, partner);
            result(r);
            if (r.ok) select(uid);
        }

        public void lookAt(double x, double z)
        {
            var c = app.view.cam;
            c.tx = (float)x;
            c.tz = (float)z;
            c.Clamp();
        }

        public void rotate(double dir)
        {
            if (app.save.settings.lockRotation)
            {
                app.toast("Kameradrehung ist gesperrt (Schloss-Knopf rechts unten)", "info", 1200);
                return;
            }
            app.view.cam.Rotate((int)dir);
        }

        /// <summary>Freie Kameradrehung (mittlere Maustaste, Strg/Alt + Ziehen, Zwei-Finger-Drehung, Tasten , . Bild↑ Bild↓).</summary>
        public void orbit(double dYaw, double dPitch)
        {
            if (app.save.settings.lockRotation)
            {
                double now = Now();
                if (now - lockToastAt > 2500)
                {
                    lockToastAt = now;
                    app.toast("Kameradrehung ist gesperrt (Schloss-Knopf rechts unten)", "info", 1200);
                }
                return;
            }
            app.view.cam.Orbit((float)dYaw, (float)dPitch);
        }

        double lockToastAt = -1e9;

        public void resetView()
        {
            app.view.cam.ResetView();
            app.toast("Blickwinkel zurückgesetzt", "info", 900);
        }

        public void zoom(double f)
        {
            var c = app.view.cam;
            c.ZoomBy((float)f, new Vector2(c.width / 2, c.height / 2));
        }

        public void fit()
        {
            var c = app.view.cam;
            c.Fit();
            c.tx = (float)(sim.s.map.width / 2);
            c.tz = (float)(sim.s.map.height / 2);
        }

        public void toggleRanges()
        {
            ui.showRanges = !ui.showRanges;
            hv.showRanges = ui.showRanges;
            app.save.settings.showRanges = ui.showRanges;
            app.persist();
            app.toast(ui.showRanges ? "Alle Reichweiten sichtbar" : "Reichweiten ausgeblendet", "info", 1200);
        }

        public void restart()
        {
            var s = sim.s;
            ended = true;
            // Helden-Endlos: der laufende Lauf zählt als beendet (Freischaltungen bleiben erhalten).
            if (s.he != null)
            {
                Progress.recordRun(app.save, s);
                app.persist();
            }
            app.startRun(new RunSetup
            {
                heroEndless = s.he != null ? app.heroEndlessSetup(s.he.champion.id, app.save.progress.heroMeta.startKit, s.he.doctrine) : null,
                mode = s.mode, seedText = s.seed.text, variant = s.map.variant, biome = s.map.biome, difficulty = s.difficulty,
                missionId = s.missionId, customMap = s.mode == "custom" ? s.map : null, size = s.map.size ?? "normal", heroes = s.heroes.Select((x) => x.id).ToList(),
            });
        }

        public void quit()
        {
            saveResume();
            app.go("menu");
        }

        public void help()
        {
            if (!sim.s.paused && !sim.isOver()) sim.setPaused(true);
            overlayScreen("help");
        }

        public void settings()
        {
            overlayScreen("settings");
        }

        /// <summary>Hilfe/Einstellungen als Overlay über der laufenden (pausierten) Partie.</summary>
        void overlayScreen(string kind)
        {
            Overlay.openOverlay(app, kind, () =>
            {
                var st = app.save.settings;
                ui.reducedMotion = st.reducedMotion;
                app.applySettings();
                app.resize();
            });
        }

        // ------------------------------------------------------------ Helden-Endlos

        /// <summary>Linksklick in der Welt: Zielwahl, Held auswählen oder bewegen. true = verarbeitet.</summary>
        bool heroTap(double cx, double cy)
        {
            var he = sim.s.he;
            if (he == null) return false;
            var c = he.champion;
            if (targeting >= 0)
            {
                castAt(targeting, cx + 0.5, cy + 0.5);
                return true;
            }
            if (Math.Floor(c.x) == cx && Math.Floor(c.y) == cy && c.dead <= 0)
            {
                champSelected = !champSelected;
                select(0);
                app.audio.play("select");
                return true;
            }
            if (sim.towerAt(cx, cy) != null) return false; // Turm auswählen (Standardverhalten)
            // Hindernis anklicken: Abbau-Panel
            if (sim.s.map.cells[(int)(cy * sim.s.map.width + cx)] == Cell.Blocked && !(he.shop.x == cx && he.shop.y == cy))
            {
                select(0);
                hv.selectedObstacle = cy * sim.s.map.width + cx;
                hud.forcePanelRefresh();
                app.audio.play("select");
                return true;
            }
            if (he.shop.x == cx && he.shop.y == cy)
            {
                moveToShop();
                heroHud?.toggle("shop", "potions");
                return true;
            }
            if (ChampionSystem.championWalkable(sim, cx, cy))
            {
                select(0);
                moveChampionTo(cx, cy);
                return true;
            }
            return false;
        }

        double holdHintAt = -1e9;

        public void mine(double cell)
        {
            double w = sim.s.map.width;
            double cx = cell % w;
            double cy = Math.Floor(cell / w);
            var r = sim.mine(cx, cy);
            if (!r.ok)
            {
                app.audio.play("error");
                app.toast(r.reason, "warn", 1800);
                return;
            }
            if (r.walk == true)
            {
                // Zum nächsten begehbaren Nachbarfeld laufen; der Abbau beginnt bei Ankunft
                var he = sim.s.he;
                var c = he?.champion;
                var opts = NEIGHBORS
                    .Select((d) => new GridPos(cx + d[0], cy + d[1]))
                    .Where((p) => ChampionSystem.championWalkable(sim, p.x, p.y))
                    .OrderBy((p) => c != null ? Math.Pow(p.x + 0.5 - c.x, 2) + Math.Pow(p.y + 0.5 - c.y, 2) : 0)
                    .ToList();
                double target = he?.mineTarget ?? -1;
                bool moved = false;
                foreach (var p in opts)
                {
                    if (sim.moveChampion(p.x, p.y).ok)
                    {
                        moved = true;
                        break;
                    }
                }
                if (he != null) he.mineTarget = moved ? target : -1;
                if (!moved)
                {
                    app.toast("Das Hindernis ist für den Helden nicht erreichbar.", "warn", 1800);
                    return;
                }
                app.toast("Held läuft zum Hindernis – der Abbau beginnt bei Ankunft.", "info", 1600);
            }
            else app.toast("⛏ Abbau begonnen – nicht unterbrechbar.", "info", 1600);
            app.audio.play("build");
            hud.forcePanelRefresh();
        }

        /// <summary>Taktikladung zünden (Knopfleiste neben dem Heldendock).</summary>
        public void useCharge(string id)
        {
            var r = sim.useCharge(id);
            if (!r.ok)
            {
                app.audio.play("error");
                app.toast(r.reason ?? "Nicht möglich", "warn", 1500);
                return;
            }
            app.audio.play("heroAbility", 0.9f);
            heroHud?.refresh();
        }

        /// <summary>Befehlsbanner setzen: per Taste auf das Feld unter dem Mauszeiger, per Knopf an die Position des Helden.</summary>
        public void placeBanner(bool atCursor)
        {
            var he = sim.s.he;
            if (he == null) return;
            double x = he.champion.x, y = he.champion.y;
            if (atCursor)
            {
                var cell = ui.hoverCell ?? (lastPx >= 0 ? app.view.pickCell(lastPx, lastPy, sim.s.map) : null);
                if (cell != null) { x = cell.x + 0.5; y = cell.y + 0.5; }
            }
            var r = sim.placeBanner(x, y);
            if (!r.ok)
            {
                app.audio.play("error");
                app.toast(r.reason ?? "Nicht möglich", "warn", 1500);
                return;
            }
            app.audio.play("build");
            app.toast($"Banner gesetzt – Türme in der Nähe erhalten den halben Befehlsbonus ({U.Fmt(HeroCommand.BANNER_CD)} s bis zum Versetzen)", "info", 1800);
            heroHud?.refresh();
        }

        public void toggleHold()
        {
            var c = sim.s.he?.champion;
            if (c == null) return;
            bool on = !B(c.holdPos);
            sim.holdPosition(on);
            app.audio.play("ui");
            app.toast(on ? "⚓ Held festgesetzt – Klicks bewegen ihn nicht (Z löst)" : "Held folgt wieder Laufbefehlen", "info", 1500);
        }

        void moveChampionTo(double cx, double cy)
        {
            if (B(sim.s.he?.champion.holdPos))
            {
                double now = Now();
                if (now - holdHintAt > 2500)
                {
                    holdHintAt = now;
                    app.toast("Held ist festgesetzt (Z oder ⚓ löst)", "info", 1200);
                }
                return;
            }
            var r = sim.moveChampion(cx, cy);
            if (!r.ok)
            {
                app.audio.play("error");
                app.toast(r.reason, "warn", 1500);
            }
        }

        bool heroKey(KeyEvt e, string k)
        {
            var hh = heroHud;
            if (hh == null) return false;
            if (isShiftDigit(e))
            {
                e.preventDefault();
                var he = sim.s.he;
                int n = e.code[5] - '0';
                string p = null;
                if (n == 5) p = he?.champion.specialPotion?.potion;
                else if (he != null && n - 1 < he.champion.potions.Count) p = he.champion.potions[n - 1]?.potion;
                if (!string.IsNullOrEmpty(p)) usePotion(p);
                else app.toast(n == 5 ? "Kein Trank im Spezialslot." : $"Kein Trank auf Platz {n}.", "info", 1200);
                return true;
            }
            int slot = Array.IndexOf(new[] { "a", "s", "d", "w" }, k);
            if (slot >= 0)
            {
                e.preventDefault();
                championSkillKey(slot);
                return true;
            }
            switch (k)
            {
                case "tab": e.preventDefault(); hh.toggle("sheet"); return true;
                case "x": e.preventDefault(); hh.toggle("shop"); return true;
                case "y": e.preventDefault(); toggleFollow(); return true;
                case "v": e.preventDefault(); toggleTopDown(); return true;
                case "z": e.preventDefault(); toggleHold(); return true;
                case ".": e.preventDefault(); placeBanner(true); return true;
                default: return false;
            }
        }

        void setTargeting(int slot)
        {
            targeting = slot;
            if (heroHud != null) heroHud.targeting = slot;
            var he = sim.s.he;
            if (slot < 0 || he == null)
            {
                ui.abilityRadius = 0;
                ui.champCastRange = 0;
                return;
            }
            var sk = Champions.CHAMPIONS[he.champion.id].skills[slot];
            ui.abilityRadius = sk.target == "point" ? sk.radius : 0.6;
            ui.champCastRange = sk.castRange;
        }

        void castAt(int slot, double x, double y)
        {
            var r = sim.championSkill(slot, x, y);
            if (!r.ok)
            {
                app.audio.play("error");
                app.toast(r.reason, "warn", 1500);
                return;
            }
            app.audio.play("heroAbility", 0.8f);
            setTargeting(-1);
        }

        public void championSkillKey(double slotD)
        {
            int slot = (int)slotD;
            var he = sim.s.he;
            if (he == null) return;
            var skills = Champions.CHAMPIONS[he.champion.id].skills;
            if (slot < 0 || skills == null || slot >= skills.Length) return;
            var sk = skills[slot];
            if (sk == null) return;
            var chk = ChampionSystem.skillState(sim, slot);
            if (!chk.ok)
            {
                app.audio.play("error");
                app.toast($"{sk.name}: {chk.reason ?? "nicht bereit"}", "warn", 1400);
                return;
            }
            if (sk.target == "self")
            {
                castAt(slot, he.champion.x, he.champion.y);
                return;
            }
            // Zweiter Tastendruck während der Zielwahl: auf das Feld unter dem Mauszeiger wirken
            if (targeting == slot && ui.hoverCell != null)
            {
                castAt(slot, ui.hoverCell.x + 0.5, ui.hoverCell.y + 0.5);
                return;
            }
            selectBuild(null);
            setTargeting(slot);
            app.toast($"{sk.name}: {(sk.target == "tower" ? "Turm" : "Zielbereich")} anklicken (Esc/Rechtsklick bricht ab)", "info", 1800);
        }

        void heResult(CommandResult r, string sfx = "ui")
        {
            if (!r.ok)
            {
                app.audio.play("error");
                app.toast(r.reason ?? "Nicht möglich", "warn", 1800);
            }
            else app.audio.play(sfx);
            heroHud?.refresh();
            saveResumeIfBuild();
        }

        public void usePotion(string potion)
        {
            var r = sim.usePotion(potion);
            heResult(r, "heal");
            if (r.ok) app.toast($"{Items.POTIONS[potion].icon} {Items.POTIONS[potion].name}", "info", 900);
        }

        public void slotPotion(string potion) { heResult(sim.slotPotion(potion)); }
        public void learnSkill(string nodeId) { heResult(sim.learnSkill(nodeId), "upgrade"); }

        public void paragonPoint(string nodeId)
        {
            heResult(sim.paragonPoint(nodeId), "upgrade");
            Progress.commitHeroProgress(app.save, sim.s);
            app.persist();
        }

        public void socketRune(double towerUid, double runeUid)
        {
            result(sim.socketRune(towerUid, runeUid), "upgrade");
        }

        public void unsocketRune(double runeUid)
        {
            result(sim.unsocketRune(runeUid), "ui");
        }

        public void treeReset()
        {
            heResult(sim.treeReset(), "sell");
        }

        public void paragonReset()
        {
            heResult(sim.paragonReset(), "ui");
            Progress.commitHeroProgress(app.save, sim.s);
            app.persist();
        }

        public void equip(double uid) { heResult(sim.equip(uid), "build"); }
        public void unequip(double slot) { heResult(sim.unequip(slot)); }
        public void shopBuy(string offerId) { heResult(sim.shopBuy(offerId), "gold"); }
        public void shopBuyItem(string item) { heResult(sim.shopBuyItem(item), "gold"); }
        public void shopSell(double uid) { heResult(sim.shopSell(uid), "sell"); }
        public void craft(string recipeId) { heResult(sim.craft(recipeId), "fusion"); }
        public void upgradeItem(double uid) { heResult(sim.upgradeItem(uid), "upgrade"); }

        public void focusChampion()
        {
            var c = sim.s.he?.champion;
            if (c == null) return;
            champSelected = true;
            lookAt(c.dead > 0 ? sim.s.map.goal.x + 0.5 : c.x, c.dead > 0 ? sim.s.map.goal.y + 0.5 : c.y);
            app.audio.play("select");
        }

        public void toggleFollow()
        {
            if (heroHud == null) return;
            heroHud.follow = !heroHud.follow;
            app.toast(heroHud.follow ? "Kamera folgt dem Helden (Y)" : "Kamera frei", "info", 1100);
        }

        public void toggleTopDown()
        {
            bool on = app.view.cam.ToggleTopDown();
            app.toast(on ? "Draufsicht (V)" : "Isometrische Ansicht (V)", "info", 1000);
        }

        public void moveToShop()
        {
            var he = sim.s.he;
            if (he == null) return;
            var opts = NEIGHBORS
                .Select((d) => new GridPos(he.shop.x + d[0], he.shop.y + d[1]))
                .Where((p) => ChampionSystem.championWalkable(sim, p.x, p.y))
                .OrderBy((p) => Math.Pow(p.x + 0.5 - he.champion.x, 2) + Math.Pow(p.y + 0.5 - he.champion.y, 2))
                .ToList();
            foreach (var p in opts) if (sim.moveChampion(p.x, p.y).ok) return;
            app.toast("Der Laden ist gerade nicht erreichbar.", "warn", 1500);
        }

        public void tutorialDone()
        {
            app.save.settings.tutorialSeen = true;
            app.persist();
        }
    }

    /// <summary>Animation einer schwebenden Zahl (CSS @keyframes rise bzw. fadeOnly bei „Bewegung reduzieren“).</summary>
    public class FloaterFx : MonoBehaviour
    {
        public bool reduced;
        public Action onDone;
        float t;
        RectTransform rt;
        Graphic g;
        Vector2 start;
        bool done;

        void Start()
        {
            rt = (RectTransform)transform;
            g = GetComponent<Graphic>();
            start = rt.anchoredPosition;
        }

        void Update()
        {
            t += Time.unscaledDeltaTime;
            float dur = reduced ? 1f : 1.1f;
            float p = Mathf.Clamp01(t / dur);
            // ease-out
            float e = 1 - (1 - p) * (1 - p);
            if (!reduced) rt.anchoredPosition = start + new Vector2(0, 42 * e);
            if (g != null)
            {
                var c = g.color;
                c.a = 1 - e;
                g.color = c;
            }
            // TS: animationend bzw. Sicherheitszeitgeber nach 1,5 s
            if (p >= 1 || t > 1.5f) finish();
        }

        void finish()
        {
            if (done) return;
            done = true;
            onDone?.Invoke();
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (!done)
            {
                done = true;
                onDone?.Invoke();
            }
        }
    }
}
