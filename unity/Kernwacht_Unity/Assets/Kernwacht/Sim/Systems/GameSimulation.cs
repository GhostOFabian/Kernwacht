// Übersetzung von sim/GameSimulation.ts.
// Deterministische Spielsimulation mit festem Takt (SIM.tickRate).

using System;
using System.Collections.Generic;
using System.Linq;

namespace Kernwacht.Sim
{
    /// <summary>TS: <c>{ dmg: number; speed: number; range: number; crit: number }</c> – aggregierte Turmverstärkung des Helden-Endlosmodus.</summary>
    // PORT: Name frei gewählt (TS-Inline-Typ); ChampionSystem.rebuildChampionBuffs muss diese Klasse verwenden.
    public class ChampTowerBuff
    {
        public double dmg;
        public double speed;
        public double range;
        public double crit;
    }

    /// <summary>Abgeleitete, nicht serialisierte Daten. Werden bei Bedarf (Dirty-Flags) neu berechnet.</summary>
    public class SimCache
    {
        public double[] field;
        public double[] safeField;
        public byte[] blocked;
        public bool fieldDirty;
        public bool networkDirty;
        public bool statsDirty;
        public Dictionary<double, Dictionary<string, double>> towerStats = new Dictionary<double, Dictionary<string, double>>();
        public Dictionary<double, TowerInstance> towerByCell = new Dictionary<double, TowerInstance>();
        public Dictionary<double, TowerInstance> towerByUid = new Dictionary<double, TowerInstance>();
        public Dictionary<double, EnemyInstance> enemyByUid = new Dictionary<double, EnemyInstance>();
        public ModAggregate mods;
        public Dictionary<double, double> linked = new Dictionary<double, double>();       // Turm-UID → Netzwerkgruppen-Index
        public Dictionary<double, List<string>> resonanceByTower = new Dictionary<double, List<string>>();
        public Dictionary<double, double> networkBonusByTower = new Dictionary<double, double>();
        public Dictionary<double, HeroAura> heroAura = new Dictionary<double, HeroAura>();
        public string heroAuraSig;
        public double heroAuraTimer;
        /// <summary>Aggregierte Turmverstärkungen des Helden-Endlosmodus (Fähigkeiten + Aura).</summary>
        public Dictionary<double, ChampTowerBuff> champBuff = new Dictionary<double, ChampTowerBuff>();
        public string champBuffSig;
        public double champBuffTimer;
    }

    /// <summary>TS: <c>{ ok: true; message?: string } | { ok: false; reason: string }</c>.</summary>
    public class CommandResult
    {
        public bool ok;
        public string message;
        public string reason;
        public static CommandResult Ok(string message = null) => new CommandResult { ok = true, message = message };
        public static CommandResult Fail(string reason) => new CommandResult { ok = false, reason = reason };
    }

    /// <summary>TS: <c>CommandResult &amp; { walk?: boolean }</c> (MiningSystem.orderMine).</summary>
    public class MineResult : CommandResult
    {
        public bool? walk;
        public static new MineResult Ok(string message = null) => new MineResult { ok = true, message = message };
        public static new MineResult Fail(string reason) => new MineResult { ok = false, reason = reason };
    }

    /// <summary>TS: inline-Typ <c>{ uid: number; id: TowerActiveId }</c> von <c>pendingActives</c>.</summary>
    public class PendingActive
    {
        public double uid;
        public string id;   // TowerActiveId
    }

    /// <summary>
    /// Deterministische Spielsimulation mit festem Takt (SIM.tickRate).
    /// Die Darstellung liest den Zustand nur; alle Änderungen laufen über Befehle oder step().
    /// </summary>
    public class GameSimulation
    {
        public readonly GameState s;
        public readonly SimCache c;
        public List<GameEvent> events = new List<GameEvent>();
        /// <summary>In diesem Takt ausgelöste aktive Turmfähigkeiten (werden im nächsten Takt ausgeführt).</summary>
        public List<PendingActive> pendingActives = new List<PendingActive>();
        private double acc = 0;
        /// <summary>Fehler in einem Tick werden protokolliert statt die Simulation still anzuhalten.</summary>
        public string lastError = null;

        public GameSimulation(GameState state)
        {
            this.s = state;
            double n = state.map.width * state.map.height;
            this.c = new SimCache
            {
                field = new double[(int)n],
                safeField = new double[(int)n],
                blocked = new byte[(int)n],
                fieldDirty = true,
                networkDirty = true,
                statsDirty = true,
                towerStats = new Dictionary<double, Dictionary<string, double>>(),
                towerByCell = new Dictionary<double, TowerInstance>(),
                towerByUid = new Dictionary<double, TowerInstance>(),
                enemyByUid = new Dictionary<double, EnemyInstance>(),
                mods = Modifiers.aggregateModifiers(state.modifiers),
                linked = new Dictionary<double, double>(),
                resonanceByTower = new Dictionary<double, List<string>>(),
                networkBonusByTower = new Dictionary<double, double>(),
                heroAura = new Dictionary<double, HeroAura>(),
                heroAuraSig = "",
                heroAuraTimer = 0,
                champBuff = new Dictionary<double, ChampTowerBuff>(),
                champBuffSig = "",
                champBuffTimer = 0,
            };
            this.reindex();
            this.refreshCaches();
        }

        public static GameSimulation create(RunSetup setup)
        {
            return new GameSimulation(Setup.createInitialState(setup));
        }

        // ------------------------------------------------------------ Hilfsfunktionen

        public double uid()
        {
            return this.s.nextUid++;
        }

        public double rand()
        {
            double[] r = RngUtil.step(this.s.rng.combat);
            double v = r[0];
            double st = r[1];
            this.s.rng.combat = st;
            return v;
        }

        public void emit(GameEvent e)
        {
            if (this.events.Count < 2000) this.events.Add(e);
        }

        public List<GameEvent> drainEvents()
        {
            List<GameEvent> e = this.events;
            this.events = new List<GameEvent>();
            return e;
        }

        public void reindex()
        {
            this.c.towerByCell.Clear();
            this.c.towerByUid.Clear();
            Array.Clear(this.c.blocked, 0, this.c.blocked.Length);
            foreach (TowerInstance t in this.s.towers)
            {
                foreach (double i in Towers.footprintCells(t.def, t.cx, t.cy, this.s.map.width))
                {
                    this.c.towerByCell[i] = t;
                    if (this.s.map.variant == "mazing") this.c.blocked[(int)i] = 1;
                }
                this.c.towerByUid[t.uid] = t;
            }
            this.c.enemyByUid.Clear();
            foreach (EnemyInstance e in this.s.enemies) this.c.enemyByUid[e.uid] = e;
        }

        /// <summary>Aktualisiert alle Caches, deren Dirty-Flag gesetzt ist.</summary>
        public void refreshCaches()
        {
            if (this.c.networkDirty)
            {
                NetworkSystem.rebuildNetwork(this);
                this.c.networkDirty = false;
                this.c.statsDirty = true;
            }
            if (this.c.statsDirty)
            {
                this.c.mods = Modifiers.aggregateModifiers(this.s.modifiers);
                TowerSystem.recomputeAllStats(this);
                this.c.statsDirty = false;
            }
            if (this.c.fieldDirty)
            {
                this.c.field = Pathfinding.computeField(this.s.map, this.c.blocked);
                this.c.safeField = this.s.map.variant == "mazing" ? computeSafeField(this) : this.c.field;
                this.c.fieldDirty = false;
            }
        }

        public Dictionary<string, double> stats(TowerInstance t)
        {
            Dictionary<string, double> st = U.Get(this.c.towerStats, t.uid);
            if (st == null)
            {
                this.c.statsDirty = true;
                this.refreshCaches();
                st = U.Get(this.c.towerStats, t.uid);
            }
            if (st == null) throw new Exception($"Keine Werte für Turm {U.Fmt(t.uid)}");
            return st;
        }

        public TowerInstance towerAt(double x, double y)
        {
            return U.Get(this.c.towerByCell, y * this.s.map.width + x);
        }

        // ------------------------------------------------------------ Befehle (UI/Bot)

        public BuildCheck canBuild(string def, double cx, double cy, double spec = 0)
        {
            this.refreshCaches();
            return EconomySystem.canBuildHere(this, def, cx, cy, spec);
        }

        public CommandResult build(string def, double cx, double cy, double spec = 0)
        {
            if (this.isOver()) return CommandResult.Fail("Partie beendet");
            this.refreshCaches();
            CommandResult r = EconomySystem.buildTower(this, def, cx, cy, spec);
            this.refreshCaches();
            return r;
        }

        public CommandResult sell(double uid)
        {
            if (this.isOver()) return CommandResult.Fail("Partie beendet");
            CommandResult r = EconomySystem.sellTower(this, uid);
            this.refreshCaches();
            return r;
        }

        /// <summary>Legendären Turm versetzen (kostenlos, danach kurz außer Gefecht).</summary>
        public CommandResult moveTower(double uid, double cx, double cy)
        {
            if (this.isOver()) return CommandResult.Fail("Partie beendet");
            CommandResult r = EconomySystem.moveTower(this, uid, cx, cy);
            this.refreshCaches();
            return r;
        }

        public BuildCheck canMoveTower(double uid, double cx, double cy)
        {
            return EconomySystem.canMoveTower(this, uid, cx, cy);
        }

        /// <summary>Alle (bzw. die angegebenen) Türme verkaufen.</summary>
        public CommandResult sellAll(List<double> uids = null)
        {
            if (this.isOver()) return CommandResult.Fail("Partie beendet");
            CommandResult r = EconomySystem.sellAllTowers(this, uids);
            this.refreshCaches();
            return r;
        }

        public CommandResult upgrade(double uid)
        {
            if (this.isOver()) return CommandResult.Fail("Partie beendet");
            CommandResult r = EconomySystem.upgradeTower(this, uid);
            this.refreshCaches();
            return r;
        }

        /// <summary>Alle (bzw. die angegebenen) Türme bis zur höchsten Stufe aufwerten, solange das Gold reicht.</summary>
        public CommandResult upgradeAll(List<double> uids = null)
        {
            if (this.isOver()) return CommandResult.Fail("Partie beendet");
            CommandResult r = EconomySystem.upgradeAllTowers(this, uids);
            this.refreshCaches();
            return r;
        }

        public CommandResult specialize(double uid, double spec)
        {
            if (this.isOver()) return CommandResult.Fail("Partie beendet");
            CommandResult r = EconomySystem.specializeTower(this, uid, spec);
            this.refreshCaches();
            return r;
        }

        /// <summary>3.6: Veredelung, Bauplätze, Kernausbau.</summary>
        public CommandResult star(double uid)
        {
            if (this.isOver()) return CommandResult.Fail("Partie beendet");
            CommandResult r = EconomySystem.starTower(this, uid);
            this.refreshCaches();
            return r;
        }

        public CommandResult starAll(List<double> uids = null)
        {
            if (this.isOver()) return CommandResult.Fail("Partie beendet");
            CommandResult r = EconomySystem.starAllTowers(this, uids);
            this.refreshCaches();
            return r;
        }

        public CommandResult respec(double uid, double spec)
        {
            if (this.isOver()) return CommandResult.Fail("Partie beendet");
            CommandResult r = EconomySystem.respecTower(this, uid, spec);
            this.refreshCaches();
            return r;
        }

        public CommandResult groupSpec(List<double> uids, double spec)
        {
            if (this.isOver()) return CommandResult.Fail("Partie beendet");
            CommandResult r = EconomySystem.groupSpecialize(this, uids, spec);
            this.refreshCaches();
            return r;
        }

        public CommandResult setPriority(double uid, string p)
        {
            TowerInstance t = U.Get(this.c.towerByUid, uid);
            if (t == null) return CommandResult.Fail("Turm nicht gefunden");
            t.priority = p;
            t.target = 0;
            t.retarget = 0;
            return CommandResult.Ok();
        }

        public CommandResult fuse(double uid, double partnerUid)
        {
            if (this.isOver()) return CommandResult.Fail("Partie beendet");
            CommandResult r = EconomySystem.fuseTowers(this, uid, partnerUid);
            this.refreshCaches();
            return r;
        }

        public CommandResult towerActive(double uid)
        {
            if (this.isOver()) return CommandResult.Fail("Partie beendet");
            return EconomySystem.triggerActive(this, uid);
        }

        public CommandResult deployHero(string id, double cx, double cy)
        {
            if (this.isOver()) return CommandResult.Fail("Partie beendet");
            this.refreshCaches();
            return HeroSystem.deployHero(this, id, cx, cy);
        }

        public CommandResult heroMode(string id, string mode)
        {
            return HeroSystem.setHeroMode(this, id, mode);
        }

        public CommandResult heroAnchor(string id, double cx, double cy)
        {
            return HeroSystem.moveHeroAnchor(this, id, cx, cy);
        }

        public CommandResult heroAbility(string id, double slot)
        {
            if (this.isOver()) return CommandResult.Fail("Partie beendet");
            CommandResult r = HeroSystem.useHeroAbility(this, id, slot);
            this.refreshCaches();
            return r;
        }

        public CommandResult heroPerk(string id, double choice)
        {
            CommandResult r = HeroSystem.chooseHeroPerk(this, id, choice);
            this.refreshCaches();
            return r;
        }

        // ------------------------------------------------------------ Helden-Endlosmodus

        private CommandResult heCmd(Func<CommandResult> fn)
        {
            if (this.s.he == null) return CommandResult.Fail("Nur im Helden-Endlosmodus");
            if (this.isOver()) return CommandResult.Fail("Partie beendet");
            CommandResult r = fn();
            // Turmverstärkungen sofort wirksam machen (nicht erst im nächsten Takt)
            if (this.c.champBuffSig == "") ChampionSystem.rebuildChampionBuffs(this);
            this.refreshCaches();
            return r;
        }

        public CommandResult moveChampion(double cx, double cy)
        {
            return this.heCmd(() => ChampionSystem.orderMove(this, cx, cy));
        }

        /// <summary>Hindernis abbauen (Helden-Endlos). walk = Held muss erst hinlaufen.</summary>
        public MineResult mine(double cx, double cy)
        {
            if (this.s.he == null) return MineResult.Fail("Nur im Helden-Endlosmodus");
            if (this.isOver()) return MineResult.Fail("Partie beendet");
            return MiningSystem.orderMine(this, cx, cy);
        }

        public CommandResult holdPosition(bool hold)
        {
            return this.heCmd(() => ChampionSystem.setHoldPosition(this, hold));
        }

        public CommandResult championSkill(double slot, double tx, double ty)
        {
            return this.heCmd(() =>
            {
                var r = ChampionSystem.useChampionSkill(this, slot, tx, ty);
                // Ultimate (vierter Skill) ist zugleich ein Befehl: Sammelruf für alle Affinitätstürme
                if (r.ok && slot == 3) HeroCommand.ultimateCommand(this);
                return r;
            });
        }

        /// <summary>Taktikladung zünden (Überlast, Brennpunkt, Kältewelle, Schwachstelle).</summary>
        public CommandResult useCharge(string id)
        {
            return this.heCmd(() => TacticalCharges.use(this, id));
        }

        /// <summary>Befehlsbanner des Helden setzen bzw. versetzen.</summary>
        public CommandResult placeBanner(double x, double y)
        {
            return this.heCmd(() => HeroCommand.placeBanner(this, x, y));
        }

        public CommandResult learnSkill(string nodeId)
        {
            return this.heCmd(() => ChampionSystem.spendSkillPoint(this, nodeId));
        }

        /// <summary>Paragonpunkt vergeben (2.7).</summary>
        public CommandResult paragonPoint(string nodeId)
        {
            return this.heCmd(() => ChampionSystem.spendParagonPoint(this, nodeId));
        }

        public CommandResult paragonReset()
        {
            return this.heCmd(() => ChampionSystem.resetParagon(this));
        }

        /// <summary>3.4: Skillbaum des Helden zurücksetzen.</summary>
        public CommandResult treeReset()
        {
            return this.heCmd(() => ChampionSystem.resetSkillTree(this));
        }

        /// <summary>Rune in einen Turm sockeln bzw. zurück in den Beutel legen (2.7).</summary>
        public CommandResult socketRune(double towerUid, double runeUid)
        {
            return this.heCmd(() => RuneSystem.socketRune(this, towerUid, runeUid));
        }

        public CommandResult unsocketRune(double runeUid)
        {
            return this.heCmd(() => RuneSystem.unsocketRune(this, runeUid));
        }

        public CommandResult usePotion(string potion)
        {
            return this.heCmd(() => InventorySystem.usePotion(this, potion));
        }

        public CommandResult slotPotion(string potion)
        {
            return this.heCmd(() => InventorySystem.slotPotion(this, potion));
        }

        public CommandResult equip(double uid, double? slot = null)
        {
            return this.heCmd(() => InventorySystem.equipItem(this, uid, slot));
        }

        public CommandResult unequip(double slot)
        {
            return this.heCmd(() => InventorySystem.unequipSlot(this, slot));
        }

        public CommandResult shopBuy(string offerId)
        {
            return this.heCmd(() => InventorySystem.buyOffer(this, offerId));
        }

        public CommandResult shopBuyItem(string item)
        {
            return this.heCmd(() => InventorySystem.buyCollectionItem(this, item));
        }

        public CommandResult shopSell(double uid)
        {
            return this.heCmd(() => InventorySystem.sellItem(this, uid));
        }

        public CommandResult craft(string recipeId)
        {
            return this.heCmd(() => InventorySystem.craftRecipe(this, recipeId));
        }

        public CommandResult upgradeItem(double uid)
        {
            return this.heCmd(() => InventorySystem.upgradeItem(this, uid));
        }

        /// <summary>Beendet einen Endloslauf freiwillig (Ergebnis wird wie eine Niederlage gewertet).</summary>
        public CommandResult abandon()
        {
            if (this.isOver()) return CommandResult.Fail("Partie beendet");
            this.s.phase = "defeat";
            this.emit(new GameEvent { k = "defeat" });
            return CommandResult.Ok();
        }

        /// <summary>Helden-Endlos: nächste Welle sofort (in der Bauphase) bzw. vorzeitig (im Kampf).</summary>
        public CommandResult nextWave()
        {
            if (this.isOver()) return CommandResult.Fail("Partie beendet");
            this.refreshCaches();
            string r = WaveDirector.callNextWave(this);
            if (this.s.he != null) this.s.he.autoTimer = -1;
            return U.Truthy(r) ? CommandResult.Fail(r) : CommandResult.Ok();
        }

        public CommandResult startWave()
        {
            if (this.s.phase != "build") return CommandResult.Fail("Eine Welle läuft bereits");
            this.refreshCaches();
            WaveDirector.startNextWave(this);
            return CommandResult.Ok();
        }

        public CommandResult chooseModifier(string id)
        {
            if (this.s.phase != "choice" || this.s.modOffer == null) return CommandResult.Fail("Keine Auswahl offen");
            if (id != null && !this.s.modOffer.Contains(id)) return CommandResult.Fail("Modifikator nicht im Angebot");
            RogueliteSystem.applyModifierChoice(this, id);
            this.refreshCaches();
            return CommandResult.Ok();
        }

        public void setSpeed(double speed)
        {
            this.s.speed = U.Includes(Balance.SIM.speeds, speed) ? speed : 1;
        }

        public void setPaused(bool p)
        {
            this.s.paused = p;
        }

        public bool isOver()
        {
            return this.s.phase == "victory" || this.s.phase == "defeat";
        }

        /// <summary>Vorschau der Route im Mazing, falls an (cx, cy) ein Turm stünde.</summary>
        public List<GridPos> previewRoute(double cx, double cy)
        {
            BuildCheck chk = this.canBuild("ranger", cx, cy);
            return chk.route ?? new List<GridPos>();
        }

        // ------------------------------------------------------------ Takt

        /// <summary>
        /// Schreitet um reale Zeit voran. Spielgeschwindigkeit erhöht die Anzahl fester Ticks,
        /// nicht die Schrittweite – dadurch bleiben Schaden und Statusdauern exakt.
        /// </summary>
        public double advance(double realDt)
        {
            if (this.s.paused || this.isOver())
            {
                this.acc = 0;
                return 0;
            }
            double clamped = U.Min(U.Max(realDt, 0), 0.25);
            this.acc += clamped * this.s.speed;
            double ticks = 0;
            double max = Balance.SIM.maxTicksPerFrame * this.s.speed;
            // Zeitbudget je Bild: Bei hohem Tempo (5×/8×) und vielen Türmen würde die Simulation sonst mehr Zeit
            // brauchen, als ein Bild hat – jedes Bild wird länger, es staut sich immer mehr auf (Ruckeln bis Standbild).
            // Reicht das Budget nicht, läuft das Spiel stattdessen etwas langsamer als eingestellt, bleibt aber flüssig.
            budgetWatch.Restart();
            bool overBudget = false;
            while (this.acc >= Balance.SIM.dt && ticks < max)
            {
                string before = this.s.phase;
                this.step();
                // Bauphase erreicht: sofort melden (sie kann nur einen Takt dauern, wenn die nächste Welle gleich startet)
                if (this.s.phase == "build" && before != "build") this.onEnterBuild?.Invoke();
                this.acc -= Balance.SIM.dt;
                ticks++;
                if (budgetWatch.Elapsed.TotalMilliseconds > FRAME_BUDGET_MS) { overBudget = true; break; }
            }
            if (ticks >= max || overBudget) this.acc = U.Min(this.acc, Balance.SIM.dt);
            this.lastOverBudget = overBudget;
            return ticks;
        }

        /// <summary>Wird direkt nach dem Takt aufgerufen, in dem die Bauphase beginnt (Spielstand sichern).</summary>
        public Action onEnterBuild;

        /// <summary>Höchstens so viele Millisekunden Simulation je Bild.</summary>
        public const double FRAME_BUDGET_MS = 11;
        private static readonly System.Diagnostics.Stopwatch budgetWatch = new System.Diagnostics.Stopwatch();
        /// <summary>Lief die Simulation im letzten Bild ins Zeitbudget (Spiel läuft langsamer als eingestellt)?</summary>
        public bool lastOverBudget;

        /// <summary>Ein fester Simulationsschritt.</summary>
        public void step()
        {
            if (this.isOver()) return;
            try
            {
                this.refreshCaches();
                double dt = Balance.SIM.dt;
                this.s.tick++;
                this.s.time += dt;
                if (this.s.phase == "combat")
                {
                    this.s.waveTime += dt;
                    this.s.stats.duration += dt;
                    WaveDirector.updateSpawns(this, dt);
                }
                EnemySystem.updateEnemies(this, dt);
                BossSystem.updateBosses(this, dt);
                HeroSystem.updateHeroes(this, dt);
                if (this.s.he != null)
                {
                    ChampionSystem.updateChampion(this, dt);
                    MiningSystem.updateMining(this, dt);
                }
                this.refreshCaches();
                TowerSystem.updateTowers(this, dt);
                ProjectileSystem.updateProjectiles(this, dt);
                ZoneSystem.updateZones(this, dt);
                NetworkSystem.updateNetworkDischarge(this, dt);
                this.cleanup(dt);
                if (this.s.phase == "combat") WaveDirector.checkWaveEnd(this);
                if (this.s.he != null && !this.isOver()) HeroEndlessSystem.updateHeroEndless(this, dt);
            }
            catch (Exception err)
            {
                // Fehler werden sichtbar gemacht, die Simulation läuft im nächsten Tick weiter.
                this.lastError = $"{err.Message}\n{err.StackTrace ?? ""}";
                this.emit(new GameEvent { k = "message", text = "Interner Simulationsfehler – siehe Konsole", severity = "danger" });
                // PORT: console.error('[Simulation]', err) – ohne UnityEngine nur über System.Console.
                Console.Error.WriteLine("[Simulation] " + err);
            }
        }

        private void cleanup(double dt)
        {
            GameState s = this.s;
            if (s.enemies.Any((e) => !e.alive))
            {
                s.enemies = s.enemies.Where((e) => e.alive).ToList();
                this.c.enemyByUid.Clear();
                foreach (EnemyInstance e in s.enemies) this.c.enemyByUid[e.uid] = e;
            }
            if (s.projectiles.Any((p) => !p.alive)) s.projectiles = s.projectiles.Where((p) => p.alive).ToList();
            if (s.zones.Any((z) => z.t <= 0 && z.armed <= 0))
            {
                if (s.zones.Any((z) => z.kind == "leyNode" && z.t <= 0 && z.armed <= 0)) this.c.statsDirty = true;
                s.zones = s.zones.Where((z) => z.t > 0 || z.armed > 0).ToList();
            }
            foreach (Telegraph t in s.telegraphs) t.t -= dt;
            if (s.telegraphs.Any((t) => t.t <= 0)) s.telegraphs = s.telegraphs.Where((t) => t.t > 0).ToList();
        }

        /// <summary>Kopie des Zustands als JSON (für Speicherstände).</summary>
        public string serialize()
        {
            return U.ToJson(this.s);
        }

        /// <summary>Gefahrenfeld: Pfadspäher meiden Zellen, die von Türmen abgedeckt werden.</summary>
        private static double[] computeSafeField(GameSimulation sim)
        {
            MapDefinition map = sim.s.map;
            double n = map.width * map.height;
            // PORT: TS Float32Array → double[]; jede Zuweisung wird auf float gerundet (Math.fround-Semantik),
            // damit die Werte bitgleich zu den Float32-Einträgen in TS sind.
            double[] danger = new double[(int)n];
            foreach (TowerInstance t in sim.s.towers)
            {
                Dictionary<string, double> st = U.Get(sim.c.towerStats, t.uid);
                double r = st != null ? st["range"] : 3;
                if (st == null || st["damage"] <= 0) continue;
                double r2 = r * r;
                double x0 = Math.Max(0, Math.Floor(t.cx - r));
                double x1 = Math.Min(map.width - 1, Math.Ceiling(t.cx + r));
                double y0 = Math.Max(0, Math.Floor(t.cy - r));
                double y1 = Math.Min(map.height - 1, Math.Ceiling(t.cy + r));
                for (double y = y0; y <= y1; y++)
                {
                    for (double x = x0; x <= x1; x++)
                    {
                        double dx = x - t.cx;
                        double dy = y - t.cy;
                        if (dx * dx + dy * dy <= r2)
                        {
                            int idx = (int)(y * map.width + x);
                            danger[idx] = (double)(float)(danger[idx] + 0.6);
                        }
                    }
                }
            }
            double[] f = Pathfinding.computeField(map, sim.c.blocked, danger);
            // Sicherheitsnetz: nur nutzen, wenn ein Weg existiert.
            return WorldGenerator.spawnsOf(map).All((sp) => f[(int)(sp.y * map.width + sp.x)] < Pathfinding.INF) ? f : sim.c.field;
        }
    }
}
