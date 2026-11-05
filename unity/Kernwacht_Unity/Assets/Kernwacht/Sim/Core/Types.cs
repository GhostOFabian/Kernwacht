// Zentrale, typisierte Datenmodelle von KERNWACHT (Übersetzung von core/types.ts).
// Der gesamte Spielzustand (GameState) besteht aus reinen, JSON-serialisierbaren Daten.
// Abgeleitete Caches (Distanzfelder, Netzwerkgraph) liegen getrennt in SimCache.
//
// Konventionen dieser Datei (siehe PORTING.md):
// - number → double, optionale Zahl (x?: number) → double?, optionales bool → bool?.
// - String-Unions sind in C# einfach string (Wertebereich steht im Kommentar).
// - Record<K, V> / Partial<Record<K, V>> → Dictionary<string, V> (fehlender Schlüssel = undefined).
//   Ausnahme: Record<number, BossId> (MissionRules.bosses) → Dictionary<double, string>.
// - Partial<Stats>, Stats → Dictionary<string, double>; Partial<HeroStats>/Partial<ChampionStats> ebenfalls.
// - Nicht-optionale Listen/Dictionaries sind mit leeren Instanzen vorbelegt, optionale bleiben null.
// - Verschachtelte Objekt-Felder (Klassen) sind nicht vorbelegt (null), außer wo unten vermerkt.
// - Inline-Objekttypen aus TS haben hier benannte Klassen bekommen (siehe jeweiligen Kommentar).

using System;
using System.Collections.Generic;

namespace Kernwacht.Sim
{
    // ------------------------------------------------------------------ String-Unions (alle: string)
    // DamageType     = 'physical' | 'fire' | 'frost' | 'lightning' | 'poison' | 'arcane' | 'sonic'
    // TowerId        = 'ember' | 'frost' | 'storm' | 'spore' | 'ranger' | 'ballista'
    //                | 'rune' | 'resonator' | 'goldmine' | 'pylon' | 'cannon' | 'riftbinder' | 'barricade' | 'lanebarricade'
    //                | 'meteor' | 'tesla' | 'gravity' | 'dragon' | 'laser' | 'totem' | 'minelayer' | 'sporemine' | 'skyguard' | 'skywatch'
    //                | 'plasma' | 'glacier' | 'voidlance' | 'starfall'
    //                | 'thornwood' | 'thunderspire' | 'riftgate' | 'phoenix' | 'cathedral' | 'stormeye'
    // HeroId         = 'blade' | 'hunter' | 'riftwalker' | 'bard' | 'smith' | 'weather' | 'mystic' | 'prospector' | 'silence'
    // HeroMode       = 'hold' | 'advance' | 'retreat'
    // HeroState      = 'ready' | 'active' | 'cooldown' | 'down'
    // MutationId     = 'armored' | 'hasty' | 'explosive' | 'splitting' | 'vampiric'
    // MapSize        = 'normal' | 'large' | 'huge'
    // TowerActiveId  = 'overdrive' | 'deepfreeze' | 'overload' | 'goldrush' | 'surge' | 'rewind' | 'meteorstorm' | 'nova'
    // EnemyId        = 'runner' | 'swarm' | 'brute' | 'regen' | 'flyer' | 'shield' | 'phase'
    //                | 'saboteur' | 'scout' | 'broodmother' | 'broodling' | 'boss'
    //                | 'golem' | 'golemling' | 'stalker' | 'siege' | 'shaman' | 'tunneler' | 'griffin'
    // BossId         = 'riftColossus' | 'ashQueen' | 'nullWarden' | 'maw' | 'twinSol' | 'twinLuna' | 'worldbreaker'
    // TargetPriority = 'first' | 'exit' | 'strong' | 'weak' | 'close' | 'random'
    // MapVariant     = 'classic' | 'mazing'
    // BiomeId        = 'forest' | 'volcano' | 'frozen' | 'crystal' | 'sky' | 'swamp' | 'cavern'
    // GameMode       = 'quick' | 'campaign' | 'endless' | 'daily' | 'custom' | 'heroEndless'
    // Difficulty     = 'easy' | 'normal' | 'hard'
    // GamePhase      = 'build' | 'combat' | 'choice' | 'victory' | 'defeat'
    // AttackKind     = 'projectile' | 'pulse' | 'chain' | 'beam' | 'economy' | 'pylon' | 'rift' | 'none'
    //                | 'meteor' | 'tesla' | 'gravity' | 'dragon' | 'laser' | 'aura' | 'mines'
    // StatusId       = 'burn' | 'chill' | 'freeze' | 'freezeImmune' | 'wet' | 'poison' | 'mark'
    //                | 'brittle' | 'silence' | 'riftMark' | 'root' | 'stun' | 'tether' | 'weather'
    //                | 'reveal' | 'voidMark' | 'pull' | 'shred'
    // ModifierId     = 'chainReaction' | 'wildfire' | 'bounty' | 'netDischarge' | 'eternalIce'
    //                | 'goldRush' | 'toxicMist' | 'sharpBlades' | 'arcaneEcho' | 'rapidFire'
    //                | 'glassCannon' | 'bulwark' | 'farSight' | 'resonanceAmp' | 'interest'
    //                | 'heroism' | 'riftTap'
    // TerrainEventId = 'blizzard' | 'heatwave' | 'riftSurge' | 'sporeBloom' | 'crystalResonance' | 'fog' | 'glowtide'
    // AbilityId      = 'quake' | 'netBite' | 'phaseShift'
    //                | 'riftJump' | 'riftPortals' | 'summonBrood' | 'fireZones' | 'ashFury'
    //                | 'nullPulse' | 'reShield'
    //                | 'siegeSlam' | 'heal' | 'devour' | 'stomp' | 'twinBond'
    // ZoneKind       = 'poisonCloud' | 'fireZone' | 'portalIn' | 'portalOut' | 'quake' | 'nullPulse' | 'riftJump'
    //                | 'meteor' | 'burnTrap' | 'leyNode' | 'stomp' | 'siegeSlam' | 'devour' | 'mine'
    // GlobalBuffId   = 'hymn' | 'worldweb' | 'resoSong' | 'goldFever' | 'surge'
    // ChampionId     = 'lyra' | 'brann' | 'vela' | 'kaela' | 'varo' | 'nyx' | 'liora' | 'thorgrim' | 'sena' | 'oran' | 'mira' | 'ommar'
    // ItemId         = string
    // PotionId       = 'heal' | 'mana' | 'power' | 'haste' | 'swift' | 'stone' | 'focus' | 'towerTonic'
    // ResourceId     = 'metal' | 'crystal' | 'essence' | 'relic'
    // EquipSlot      = 'weapon' | 'armor' | 'helm' | 'artifact' | 'accessory' | 'special'
    // Rarity         = 'common' | 'rare' | 'epic' | 'legendary'
    // PickupKind     = 'xp' | 'resource' | 'fragment' | 'item' | 'potion'
    // AffixId (config/scaling.ts), RuneId (config/runes.ts), StatKey (config/stats.ts): ebenfalls string.
    // CellCode       = 0 | 1 | 2 | 3 | 4 | 5 (double, siehe Cell)
    // Stats          = Record<StatKey, number> → Dictionary<string, double>

    /// <summary>Freie Konstanten aus core/types.ts.</summary>
    public static class Types
    {
        public static readonly List<string> DAMAGE_TYPES = new List<string> { "physical", "fire", "frost", "lightning", "poison", "arcane", "sonic" };

        /// <summary>Zellcodes des Kartenrasters (identisch zur Namespace-Klasse <see cref="Kernwacht.Sim.Cell"/>).</summary>
        public static class Cell
        {
            public const double Open = 0;      // freie, bebaubare Fläche
            public const double Path = 1;      // Weg (Classic) – begehbar, nicht bebaubar
            public const double Blocked = 2;   // Hindernis (Fels, Wasser, Baum) – weder begehbar noch bebaubar
            public const double Spawn = 3;
            public const double Goal = 4;
            public const double NoBuild = 5;   // begehbar, aber nicht bebaubar (Pufferzone im Mazing)
        }
    }

    /// <summary>Zellcodes des Kartenrasters (TS: <c>Cell</c>). Auch als <c>Types.Cell</c> verfügbar.</summary>
    public static class Cell
    {
        public const double Open = 0;      // freie, bebaubare Fläche
        public const double Path = 1;      // Weg (Classic) – begehbar, nicht bebaubar
        public const double Blocked = 2;   // Hindernis (Fels, Wasser, Baum) – weder begehbar noch bebaubar
        public const double Spawn = 3;
        public const double Goal = 4;
        public const double NoBuild = 5;   // begehbar, aber nicht bebaubar (Pufferzone im Mazing)
    }

    public class GridPos
    {
        public double x;
        public double y;
        public GridPos() { }
        public GridPos(double x, double y) { this.x = x; this.y = y; }
    }

    public class Vec2
    {
        public double x;
        public double y;
        public Vec2() { }
        public Vec2(double x, double y) { this.x = x; this.y = y; }
    }

    public class StatMod
    {
        public string stat;   // StatKey
        public double? add;
        public double? mul;
    }

    public class UpgradeDefinition
    {
        public string id;
        public string name;
        public string description;
        public double cost;
        public List<StatMod> mods = new List<StatMod>();
    }

    public class TowerDefinition
    {
        public string id;                 // TowerId
        /// <summary>Kantenlänge der Grundfläche in Feldern (Standard 1; Goldgräber 2 = 2×2). Ankerfeld ist oben links.</summary>
        public double? size;
        public string name;
        public string role;
        public string description;
        public string damageType;         // DamageType | null
        public double cost;
        public string attack;             // AttackKind
        public string defaultPriority;    // TargetPriority
        public Dictionary<string, double> @base = new Dictionary<string, double>();   // Partial<Stats>
        /// <summary>Drei normale Stufen (Level 2, 3, 4). Leer nur bei der Barrikade (kein Turm).</summary>
        public List<UpgradeDefinition> upgrades = new List<UpgradeDefinition>();
        /// <summary>Zwei alternative Spezialisierungen (ab Level 4). Leer nur bei der Barrikade.</summary>
        public List<UpgradeDefinition> specs = new List<UpgradeDefinition>();
        /// <summary>Nur im Mazing-Modus verfügbar.</summary>
        public bool? mazingOnly;
        /// <summary>3.8: nur auf der Lane (Wegfelder) baubar, nicht im Mazing-Modus.</summary>
        public bool? laneOnly;
        public string synergies;
        public string counters;
        public bool networkable;
        public string hotkey;
        public string icon;
        public double color;
        /// <summary>Aktive Fähigkeit (ab Stufe 2 nutzbar, nur im Kampf). TowerActiveId oder null.</summary>
        public string active;
        /// <summary>Legendärer Fusionsturm – entsteht nur durch Fusion, nicht baubar.</summary>
        public bool? legendary;
        /// <summary>Erkennt getarnte Gegner in Reichweite.</summary>
        public bool? detector;
    }

    public class FusionRecipe
    {
        public string id;   // TowerId
        public string a;    // TowerId
        public string b;    // TowerId
        public double cost;
    }

    public class TowerActiveDefinition
    {
        public string id;   // TowerActiveId
        public string name;
        public string description;
        public double cooldown;
        public double duration;
    }

    /// <summary>TS: inline-Typ <c>EnemyDefinition.onDeathSpawn: { enemy: EnemyId; count: number }</c>.</summary>
    public class OnDeathSpawn
    {
        public string enemy;   // EnemyId
        public double count;
    }

    public class EnemyDefinition
    {
        public string id;   // EnemyId
        public string name;
        public string role;
        public string description;
        public string counter;
        public double hp;
        public double speed;           // Zellen pro Sekunde
        public double reward;
        public double threat;          // Kosten im Wellenbudget
        public double leak;            // Verlust an Kernenergie
        public bool flying;
        public Dictionary<string, double> resist = new Dictionary<string, double>();   // Partial<Record<DamageType, number>>
        public double shieldPct;       // Schild relativ zu HP
        public double regenPct;        // HP-Anteil pro Sekunde
        public List<string> abilities = new List<string>();   // AbilityId[]
        public string route;           // 'normal' | 'safe'
        public OnDeathSpawn onDeathSpawn;
        /// <summary>Getarnt, bis ein Detektor (Resonator, Runenweber, Totem, Held) ihn aufdeckt.</summary>
        public bool? stealth;
        /// <summary>Gräbt sich ein und taucht kurz vor dem Ziel wieder auf.</summary>
        public bool? burrow;
        /// <summary>Chance, Projektilen auszuweichen.</summary>
        public double? evasion;
        /// <summary>Kontaktschaden pro Sekunde gegen Helden.</summary>
        public double? contactDps;
        public double size;
        public double color;
        public string icon;
        public double minWave;
    }

    public class BossPhaseDefinition
    {
        /// <summary>Phase beginnt, wenn HP-Anteil &lt;= hpBelow.</summary>
        public double hpBelow;
        public string name;
        public List<string> abilities = new List<string>();   // AbilityId[]
        public double speedMul;
        /// <summary>Alternativer Auslöser statt HP-Schwelle ('shieldBroken' oder null).</summary>
        public string trigger;
    }

    public class BossDefinition
    {
        public string id;   // BossId
        public string name;
        public string title;
        public string description;
        public string counter;
        public double hp;
        public double speed;
        public Dictionary<string, double> resist = new Dictionary<string, double>();   // Partial<Record<DamageType, number>>
        public double poise;
        public double reward;
        public double leak;
        public List<BossPhaseDefinition> phases = new List<BossPhaseDefinition>();
        public double shieldPct;
        public double color;
        public double size;
        /// <summary>Anzahl Schwachpunkte (Weltenbrecher) – Kern nimmt erst danach vollen Schaden.</summary>
        public double? weakPoints;
        /// <summary>Partnerboss (Zwillinge), erscheint gemeinsam. BossId oder null.</summary>
        public string partner;
    }

    public class AbilityDefinition
    {
        public string id;   // AbilityId
        public string name;
        public string description;
        public double windup;
        public double cooldown;
        public bool interruptible;
    }

    public class StatusInstance
    {
        public string id;      // StatusId
        public double t;       // Restdauer in Sekunden
        public double stacks;
        public double power;   // z. B. DPS, Slow-Anteil, Resistenzreduktion
        public double src;     // Turm-UID oder 0
        public double flag;    // z. B. 1 = darf sich nicht weiter ausbreiten
    }

    public class ElementReaction
    {
        public string id;
        public string name;
        public string description;
        public string trigger;    // DamageType
        public string requires;   // StatusId
        public double cooldown;
    }

    public class StatusEffectDefinition
    {
        public string id;   // StatusId
        public string name;
        public string description;
        public string icon;
        public double color;
        public double maxStacks;
    }

    public class AbilityState
    {
        public string id;       // AbilityId
        public double cd;       // Abklingzeit
        public double windup;   // >0 = wird gerade gewirkt
        public double tx;       // Zielposition (Ankündigung)
        public double ty;
        public double r;
    }

    public class EnemyInstance
    {
        public double uid;
        public string def;      // EnemyId
        public string boss;     // BossId | null
        /// <summary>3.0: Affixe, Immunität und Weltstufe der Welle, in der der Gegner erschien. AffixId[] oder null.</summary>
        public List<string> affixes;
        public string immune;   // DamageType oder null (undefined)
        public double? tier;
        public bool? splitChild;
        public double? jamCd;
        /// <summary>3.1: Challenge-Boss mit Schildschüben, Beschwörungen und Raserei.</summary>
        public bool? challenge;
        public double? chShieldCd;
        public double? chSummonCd;
        public bool? chEnraged;
        public bool elite;
        public double x;
        public double y;
        public double nx;       // Zielzelle der aktuellen Bewegung
        public double ny;
        public double hp;
        public double maxHp;
        public double shield;
        public double maxShield;
        public double speed;
        public bool flying;
        public string route;    // 'normal' | 'safe'
        public double traveled;
        public double remaining;
        public List<StatusInstance> statuses = new List<StatusInstance>();
        public List<AbilityState> abilities = new List<AbilityState>();
        public double poise;
        public double maxPoise;
        public double phase;
        public double phaseShift;   // >0 = Phasenschild aktiv
        public Dictionary<string, double> adapt = new Dictionary<string, double>();       // Record<DamageType, number>
        public Dictionary<string, double> adaptHits = new Dictionary<string, double>();   // Record<DamageType, number>
        public double reward;
        public double leak;
        public bool alive;
        public bool rewarded;
        public double riftCd;
        public double knockCd;
        public Dictionary<string, double> reactCds = new Dictionary<string, double>();
        public double abilityMul;
        public double wave;
        public double spawnedAt;
        public double hitFlash;
        public bool reshieldUsed;
        public string mutation;   // MutationId | null
        public bool stealthed;
        public bool burrowed;
        public bool surfaced;
        public double spawnIndex;
        /// <summary>Schwachpunkte: verbleibende LP je Punkt (Weltenbrecher).</summary>
        public List<double> plates = new List<double>();
        public double plateMax;
        /// <summary>Verschluckte Türme (Schlund der Tiefe) – werden beim Tod zurückgegeben.</summary>
        public List<TowerInstance> swallowed = new List<TowerInstance>();
        /// <summary>Blockiert durch einen Helden (UID) oder 0.</summary>
        public double blockedBy;
        public bool enraged;
        /// <summary>Mini-Boss im Helden-Endlosmodus (mehr LP, mehr Beute).</summary>
        public bool? miniBoss;
    }

    public class TowerInstance
    {
        public double uid;
        public string def;   // TowerId
        public double cx;
        public double cy;
        public double level;          // 1..4
        public double spec;           // 0 | 1 | 2 (0 = keine Spezialisierung)
        /// <summary>3.6: Veredelung ★0–★10 (nur Endlos, voll ausgebaut).</summary>
        public double? stars;
        public double invested;
        public double builtWave;      // Anzahl gestarteter Wellen beim Bau
        public bool builtInPhase;     // in der aktuellen Bauphase gebaut → volle Erstattung
        public string priority;       // TargetPriority
        public double cooldown;
        public double target;         // Ziel-UID oder 0
        public double retarget;
        public double aim;
        public double stunned;        // durch Bossfähigkeiten deaktiviert
        public double disrupted;      // Netzwerk gestört
        public double rampTarget;
        public double rampStacks;
        public double damageDealt;
        public double kills;
        public double goldEarned;
        public double shots;
        public double fireAnim;
        public double activeCd;
        public double activeT;
        /// <summary>Feldschmiede: verstärkt bis Zeitpunkt.</summary>
        public double forged;
        /// <summary>Strahl-/Laseraufbau.</summary>
        public double charge;
        /// <summary>Drachenposition (Winkel) für den Drachenhorst.</summary>
        public double orbit;
    }

    public class HitPayload
    {
        public double damage;
        public string type;   // DamageType
        public double splash;
        /// <summary>Stats. PORT: bewusst nicht vorbelegt – TS setzt hier immer die (geteilte) Referenz der berechneten Turmwerte.</summary>
        public Dictionary<string, double> stats;
        public double tower;
        public string towerDef;   // TowerId
    }

    public class Projectile
    {
        public double uid;
        public double x;
        public double y;
        public double z;
        public double tx;
        public double ty;
        public double target;
        public double speed;
        public HitPayload payload;
        public string kind;   // TowerId
        public double pierceLeft;
        public double dirX;
        public double dirY;
        public double traveled;
        public double maxTravel;
        public List<double> hitList = new List<double>();
        public bool alive;
    }

    public class Zone
    {
        public double uid;
        public string kind;    // ZoneKind
        public double x;
        public double y;
        public double r;
        public double t;       // Restdauer
        public double armed;   // >0 = Ankündigungsphase (Telegraph)
        public double power;
        public double tick;
        public double link;    // Partnerzone (Portale) oder Quelle
        public double src;
    }

    public class MapDefinition
    {
        public double version;
        public double width;
        public double height;
        public string variant;   // MapVariant
        public string biome;     // BiomeId
        public List<double> cells = new List<double>();     // Cell-Codes
        public List<double> heights = new List<double>();   // 0..3, Anhöhe ab 2
        public List<double> veins = new List<double>();     // Zellindizes mit Goldadern
        /// <summary>Kristalladern (nur Helden-Endlos): Goldgräber fördert hier zusätzlich Kristall.</summary>
        public List<double> deposits;
        /// <summary>Änderungszähler (z. B. nach Abbau eines Hindernisses) – Darstellung baut das Gelände neu.</summary>
        public double? rev;
        public List<double> ley = new List<double>();       // Zellindizes mit Leylinien (+Schaden)
        public List<double> decor = new List<double>();     // [index, art, rotation*100] Tripel – rein kosmetisch
        public GridPos spawn;
        /// <summary>Alle Startpunkte (inkl. spawn). Große Karten können Weggabelungen mit mehreren Starts haben.</summary>
        public List<GridPos> spawns;
        public string size;           // MapSize oder null
        public GridPos goal;
        public string terrainEvent;   // TerrainEventId | null
        public bool fallback;
        public string name;
    }

    public class WorldSeed
    {
        public string text;   // sichtbarer, kopierbarer Seed
        public double hash;   // abgeleiteter uint32
    }

    public class SpawnEntry
    {
        public double at;
        public string enemy;      // EnemyId
        public bool elite;
        public string boss;       // BossId | null
        public string mutation;   // MutationId | null | undefined
        public bool? miniBoss;
        public bool? challenge;
    }

    public class WaveGroup
    {
        public string enemy;      // EnemyId
        public double count;
        public bool elite;
        public string boss;       // BossId | null
        public string mutation;   // MutationId | null | undefined
        public bool? miniBoss;
        public bool? challenge;
    }

    public class WaveDefinition
    {
        public double index;
        public List<WaveGroup> groups = new List<WaveGroup>();
        public double hpMul;
        public double speedMul;
        public double rewardMul;
        /// <summary>3.0: Weltstufe und Wellen-Affixe (nur Endlos).</summary>
        public double? tier;
        public List<string> affixes;   // AffixId[] oder null
        public string immune;          // DamageType oder null
        public string @event;          // TerrainEventId | null
        public bool isBoss;
    }

    public class RunModifier
    {
        public string id;   // ModifierId
        public string name;
        public string description;
        public List<string> tags = new List<string>();
        public List<string> exclusive;       // ModifierId[] oder null
        public List<string> requiresTower;   // TowerId[] oder null
        /// <summary>Nur anbieten, wenn Helden in der Partie sind.</summary>
        public bool? requiresHeroes;
        public string icon;
    }

    public class ResonanceDefinition
    {
        public string id;
        public string name;
        public string description;
        public List<string> requires = new List<string>();   // TowerId[]
        public List<StatMod> mods = new List<StatMod>();
    }

    public class MissionRules
    {
        public double waves;
        public double startGold;
        public double lives;
        /// <summary>TS: Record&lt;number, BossId&gt; → Schlüssel ist die Wellennummer als double. PORT: Zahlschlüssel statt string.</summary>
        public Dictionary<double, string> bosses = new Dictionary<double, string>();
        public Dictionary<string, double> enemyResistBonus = new Dictionary<string, double>();   // Partial<Record<DamageType, number>>
        /// <summary>3.3: Turmlimit abgeschaltet (nur automatisierte Tests).</summary>
        public bool? towerLimitOff;
        public double veinMul;
        public List<double> modifierWaves = new List<double>();
        public List<string> bannedTowers = new List<string>();     // TowerId[]
        public List<string> startModifiers = new List<string>();   // ModifierId[]
        public bool endless;
        /// <summary>Mission scheitert, wenn dieser Held fällt. HeroId oder null.</summary>
        public string heroMustSurvive;
        /// <summary>Vorgegebene Helden (Mission). HeroId[] oder null.</summary>
        public List<string> forcedHeroes;
    }

    public class RunStats
    {
        public double kills;
        public double leaks;
        public double livesLost;
        public double goldEarned;
        public double goldSpent;
        public List<double> goldPerWave = new List<double>();
        public Dictionary<string, double> damageByTower = new Dictionary<string, double>();   // Partial<Record<TowerId, number>>
        public Dictionary<string, double> builtByTower = new Dictionary<string, double>();    // Partial<Record<TowerId, number>>
        public List<string> bossesKilled = new List<string>();   // BossId[]
        public List<string> bossesFaced = new List<string>();    // BossId[]
        public Dictionary<string, double> reactions = new Dictionary<string, double>();
        public double wavesCleared;
        public double maxNetworkSize;
        public double duration;
        public double heroDeploys;
        public double heroKills;
        public double heroDowns;
        public double fusions;
        public double activesUsed;
        public double mutationsKilled;
    }

    public class Telegraph
    {
        public double uid;
        public string text;
        public double x;
        public double y;
        public double r;
        public double t;
        public double total;
        public string severity;   // 'info' | 'warn' | 'danger'
    }

    public class NetworkLink
    {
        public double a;
        public double b;
    }

    /// <summary>TS: inline-Typ <c>NetworkState.groups[i]: { members: number[]; bonus: number; resonances: string[] }</c>.</summary>
    public class NetworkGroup
    {
        public List<double> members = new List<double>();
        public double bonus;
        public List<string> resonances = new List<string>();
    }

    public class NetworkState
    {
        public List<NetworkLink> links = new List<NetworkLink>();
        public List<NetworkGroup> groups = new List<NetworkGroup>();
        public double version;
    }

    public class BossState
    {
        public double uid;
        public string id;   // BossId
        public double phase;
        public string phaseName;
    }

    /// <summary>TS: inline-Typ <c>GameState.rng: { combat: number; mods: number; loot?: number }</c>.</summary>
    public class RngState
    {
        public double combat;
        public double mods;
        public double? loot;
    }

    public class GameState
    {
        public double version;
        public WorldSeed seed;
        public string mode;         // GameMode
        public string difficulty;   // Difficulty
        public string missionId;    // string | null
        public MissionRules rules;
        public MapDefinition map;
        public double tick;
        public double time;
        public double gold;
        public double lives;
        public double maxLives;
        /// <summary>3.4: zusätzliche Turmplätze aus geschafften Bosswellen (Endlos).</summary>
        public double? bossSlots;
        /// <summary>3.8: Zeitpunkte (Spielzeit s), zu denen Lane-Barrikaden gebaut wurden – für die Abklingzeit.</summary>
        public List<double> laneBarricades;

        public double wave;                    // Anzahl gestarteter Wellen
        public string phase;                   // GamePhase
        public double waveTime;
        public WaveDefinition currentWave;     // null möglich
        public List<SpawnEntry> spawnQueue = new List<SpawnEntry>();
        public List<TowerInstance> towers = new List<TowerInstance>();
        public List<EnemyInstance> enemies = new List<EnemyInstance>();
        public List<Projectile> projectiles = new List<Projectile>();
        public List<Zone> zones = new List<Zone>();
        public List<Telegraph> telegraphs = new List<Telegraph>();
        public List<string> modifiers = new List<string>();   // ModifierId[]
        public List<string> modOffer;                         // ModifierId[] | null
        public RngState rng;
        public double nextUid;
        public BossState boss;                 // null möglich
        public NetworkState network;
        public RunStats stats;
        public double netZapTimer;
        public double speed;
        public bool paused;
        public double score;
        public List<HeroInstance> heroes = new List<HeroInstance>();
        /// <summary>Zustand des Helden-Endlosmodus (null in allen anderen Modi).</summary>
        public HeroEndlessState he;
        public double riftEnergy;
        /// <summary>Zeitlich begrenzte globale Effekte (Heldenfähigkeiten, Turmfähigkeiten).</summary>
        public List<GlobalBuff> buffs = new List<GlobalBuff>();
    }

    public class GlobalBuff
    {
        public string id;   // GlobalBuffId
        public double t;
        public double power;
        public double src;
    }

    /// <summary>
    /// Heldenwerte. Zusätzlich zum TS-Interface: Indexer <c>this[string]</c> für den dynamischen
    /// Zugriff <c>st[k as keyof HeroStats]</c> und <see cref="KEYS"/> (Deklarationsreihenfolge).
    /// PORT: Object.keys(...) auf einem TS-Objekt liefert die Reihenfolge des Objektliterals, nicht des Interfaces.
    /// </summary>
    public class HeroStats
    {
        public double damage;
        public double range;
        public double attackCd;
        public double maxHp;
        public double speed;
        public double auraRadius;
        public double auraPower;
        public double abilityCdMul;
        public double activeTime;
        public double blockCount;
        public double goldBonus;
        public double armor;

        public static readonly string[] KEYS = { "damage", "range", "attackCd", "maxHp", "speed", "auraRadius", "auraPower", "abilityCdMul", "activeTime", "blockCount", "goldBonus", "armor" };

        public double this[string key]
        {
            get
            {
                switch (key)
                {
                    case "damage": return damage;
                    case "range": return range;
                    case "attackCd": return attackCd;
                    case "maxHp": return maxHp;
                    case "speed": return speed;
                    case "auraRadius": return auraRadius;
                    case "auraPower": return auraPower;
                    case "abilityCdMul": return abilityCdMul;
                    case "activeTime": return activeTime;
                    case "blockCount": return blockCount;
                    case "goldBonus": return goldBonus;
                    case "armor": return armor;
                    default: throw new Exception("HeroStats: unbekannter Schlüssel " + key);
                }
            }
            set
            {
                switch (key)
                {
                    case "damage": damage = value; break;
                    case "range": range = value; break;
                    case "attackCd": attackCd = value; break;
                    case "maxHp": maxHp = value; break;
                    case "speed": speed = value; break;
                    case "auraRadius": auraRadius = value; break;
                    case "auraPower": auraPower = value; break;
                    case "abilityCdMul": abilityCdMul = value; break;
                    case "activeTime": activeTime = value; break;
                    case "blockCount": blockCount = value; break;
                    case "goldBonus": goldBonus = value; break;
                    case "armor": armor = value; break;
                    default: throw new Exception("HeroStats: unbekannter Schlüssel " + key);
                }
            }
        }

        /// <summary>Flache Kopie (TS: <c>{ ...st }</c>).</summary>
        public HeroStats Copy() { return (HeroStats)MemberwiseClone(); }
    }

    public class HeroPerk
    {
        public string id;
        public string name;
        public string description;
        public Dictionary<string, double> add;   // Partial<HeroStats> oder null
        public Dictionary<string, double> mul;   // Partial<HeroStats> oder null
    }

    public class HeroAbilityDefinition
    {
        public string id;
        public string name;
        public string description;
        public double cooldown;
    }

    public class HeroDefinition
    {
        public string id;   // HeroId
        public string name;
        public string title;
        /// <summary>'Blocker' | 'Fernkampf' | 'Assassine' | 'Unterstützung' | 'Reparatur' | 'Elementar' | 'Netzwerk' | 'Wirtschaft' | 'Kontrolle'</summary>
        public string role;
        public bool support;
        public string description;
        public string damageType;   // DamageType
        public double cost;         // Rissenergie für einen Einsatz
        public HeroStats @base;
        /// <summary>TS-Tupel [HeroAbilityDefinition, HeroAbilityDefinition] → Array der Länge 2.</summary>
        public HeroAbilityDefinition[] abilities;
        public HeroAbilityDefinition ultimate;
        /// <summary>TS: [HeroPerk, HeroPerk][] – je Stufe 2..5 eine Wahl; jedes Element ist ein Array der Länge 2.</summary>
        public List<HeroPerk[]> perks = new List<HeroPerk[]>();
        public double color;
        public double accent;
        public string icon;
        public string unlock;       // Missions-ID oder 'start'
    }

    public class HeroInstance
    {
        public string id;      // HeroId
        public double uid;
        public string state;   // HeroState
        public string mode;    // HeroMode
        public double x;
        public double y;
        public double ax;      // Ankerpunkt
        public double ay;
        public double hp;
        public double level;
        public double xp;
        public List<double> perks = new List<double>();   // gewählte Perks (0/1) je Stufe
        public bool pendingPerk;
        public double activeLeft;
        public double cooldown;
        /// <summary>TS-Tupel [number, number] → double[2].</summary>
        public double[] abilityCd = new double[2];
        public double ult;                     // 0..100
        public double attackCd;
        public double target;
        public double buffT;                   // z. B. Bollwerk, Brandpfeile
        public string buffKind;
        public double kills;
        public double damageDealt;
        public double treasureWave;
        public double facing;
        public double anim;
        public double blocking;
    }

    public class PlayerSettings
    {
        public double masterVolume;
        public double musicVolume;
        public double sfxVolume;
        public bool muted;
        public bool reducedMotion;
        public double uiScale;
        public string quality;          // 'auto' | 'low' | 'medium' | 'high'
        public bool showGrid;
        public bool showRanges;
        public bool tutorialSeen;
        public string lastVariant;      // MapVariant
        public string lastDifficulty;   // Difficulty
        public string lastBiome;        // BiomeId | 'random'
        public string lastSize;         // MapSize
        public List<string> lastHeroes = new List<string>();   // HeroId[]
        public string skin;
        // (TS-Kommentar, gehört zu lockRotation:) Kameradrehung (Q/E, Drehknöpfe) gesperrt.
        /// <summary>Eigene Tastenbelegung: Aktion → Taste (nur Abweichungen vom Standard).</summary>
        public Dictionary<string, string> keys = new Dictionary<string, string>();
        public bool lockRotation;
        /// <summary>3.13: Kino-Effekte (Tilt-Shift-Unschärfe, Filmkorn, Sonnendunst); fehlt = an.</summary>
        public bool? cinematicFx;
    }

    /// <summary>TS: inline-Typ <c>PlayerProgress.bestScores[i]</c>.</summary>
    public class BestScoreEntry
    {
        public string mode;   // GameMode
        public double score;
        public string seed;
        public string date;
        public double waves;
        public bool victory;
    }

    public class PlayerProgress
    {
        public double campaignUnlocked;                     // Anzahl freigeschalteter Missionen
        public List<string> campaignCompleted = new List<string>();
        public Dictionary<string, double> campaignStars = new Dictionary<string, double>();
        public Dictionary<string, double> bestEndlessWave = new Dictionary<string, double>();   // key: variant
        public List<BestScoreEntry> bestScores = new List<BestScoreEntry>();
        public Dictionary<string, double> dailyCompleted = new Dictionary<string, double>();    // Datum → Score
        public List<string> achievements = new List<string>();
        public double totalRuns;
        public double totalVictories;
        public double totalKills;
        public double totalBossKills;
        public List<string> bossesDefeated = new List<string>();   // BossId[]
        public HeroMetaProgress heroMeta;
    }

    /// <summary>TS: inline-Typ <c>HeroMetaProgress.heroLevel: { level: number; xp: number }</c>.</summary>
    public class HeroLevelProgress
    {
        public double level;
        public double xp;
    }

    public class HeroMetaProgress
    {
        /// <summary>Höchste je erreichte (abgeschlossene) Welle im Helden-Endlosmodus.</summary>
        public double bestWave;
        /// <summary>Dauerhaft freigeschaltete Items (Sammlung).</summary>
        public List<string> unlockedItems = new List<string>();   // ItemId[]
        /// <summary>Gesammelte Item-Fragmente je Item.</summary>
        public Dictionary<string, double> fragments = new Dictionary<string, double>();
        /// <summary>Einmalige Meilensteine, deren Belohnung bereits vergeben wurde.</summary>
        public List<double> claimedMilestones = new List<double>();
        /// <summary>Bereits verbuchte Partien (verhindert doppelte Gutschrift).</summary>
        public List<string> committedRuns = new List<string>();
        /// <summary>Gewählte Startausrüstung aus der Sammlung.</summary>
        public List<string> startKit = new List<string>();   // ItemId[]
        public string lastChampion;   // ChampionId
        public double runs;
        /// <summary>Passiver Baum: Knoten-ID → Rang.</summary>
        public Dictionary<string, double> passive = new Dictionary<string, double>();
        /// <summary>2.7: Skillbaum je Held (Stufe/EP darin nur noch zur Abwärtskompatibilität).</summary>
        public Dictionary<string, ChampionProgress> champions = new Dictionary<string, ChampionProgress>();
        /// <summary>2.9.5: gemeinsame Heldenstufe (bis 99) für alle Helden.</summary>
        public HeroLevelProgress heroLevel;
        /// <summary>3.0: Turm-Meisterschaft – dauerhafte Abschüsse je Turmtyp.</summary>
        public Dictionary<string, double> towerMastery = new Dictionary<string, double>();
        /// <summary>
        /// 2.7: Paragon, gemeinsam für alle Helden. TS: inline <c>{ level; xp; points: Record&lt;string, number&gt; }</c>,
        /// strukturgleich mit <c>ParagonProgress</c> aus config/paragon.ts.
        /// PORT: Typ ParagonProgress wird in Sim/Config/Paragon.cs (Namespace-Ebene) definiert.
        /// </summary>
        public ParagonProgress paragon;
    }

    public class RuneInstance
    {
        public double uid;
        public string rune;   // RuneId (config/runes.ts)
        public double tier;
        /// <summary>Veraltet (bis 3.2: einzelner Turm); wird beim Laden in def umgewandelt.</summary>
        public double tower;
        /// <summary>3.3: gesockelt in dieser Turmsorte (gilt für alle Türme der Sorte); fehlt (null) = im Beutel.</summary>
        public string def;    // TowerId oder null
    }

    public class ChampionProgress
    {
        public double level;
        public double xp;
        public Dictionary<string, double> tree = new Dictionary<string, double>();
    }

    /// <summary>TS: inline-Typ <c>SaveData.customMaps[i]: { name: string; code: string }</c>.</summary>
    public class CustomMapEntry
    {
        public string name;
        public string code;
    }

    public class SaveData
    {
        public double version;
        public PlayerSettings settings;
        public PlayerProgress progress;
        public List<CustomMapEntry> customMaps = new List<CustomMapEntry>();
        public string resume;   // serialisierter GameState (nur Bauphase) oder null
    }

    /// <summary>
    /// Diskriminierte Union GameEvent → eine Klasse; Diskriminator ist <see cref="k"/>.
    /// Zahlen-/Bool-Felder sind NICHT nullable (Standard 0/false), Strings/Listen null, wenn nicht gesetzt.
    /// PORT: Namenskonflikt beim Feld <c>boss</c>: In 'kill' und 'waveStart' ist es boolean → C# <see cref="isBoss"/>;
    /// in 'bossPhase' und 'death' ist es BossId (| null) → C# <see cref="boss"/> (string).
    /// Varianten (Felder):
    ///  shot{tower,x,y,tx,ty,uid} hit{type,x,y,amount,splash} chain{pts} pulse{tower,x,y,r}
    ///  kill{enemy,x,y,gold,elite,boss:boolean→isBoss} leak{enemy,lives} reaction{id,x,y} gold{amount,x,y}
    ///  build{tower,x,y} sell{tower,x,y,refund} upgrade{tower,x,y} waveStart{wave,boss:boolean→isBoss}
    ///  waveClear{wave,bonus} bossPhase{boss,phase,name} ability{id,x,y,stage:'warn'|'cast'|'interrupt'}
    ///  teleport{x,y,x2,y2} immune{x,y} victory{} defeat{} message{text,severity:'info'|'warn'|'danger'}
    ///  heroDeploy{hero,x,y} heroDown{hero,x,y} heroAbility{hero,ability,x,y,r} heroAttack{hero,x,y,tx,ty,melee}
    ///  heroLevel{hero,level} meteor{x,y,r,big} laser{x,y,tx,ty,power,uid} arc{x,y,tx,ty} gravity{x,y,r}
    ///  breath{x,y,tx,ty} fusion{tower,x,y} towerActive{tower,active,x,y} plateBreak{x,y,left} burst{x,y,r}
    ///  devour{tower,x,y} heal{x,y,r} death{enemy,boss,x,y,color,size} burrow{x,y,up}
    ///  champAttack{x,y,tx,ty,melee,color} champSkill{skill,x,y,tx,ty,r,color} champLevel{level,x,y}
    ///  champDown{x,y} champRespawn{x,y} pickup{kind,x,y,label,color} towerBuff{x,y,color}
    ///  potion{potion,x,y} craft{item,label} mined{x,y,label} mineStart{x,y,total}
    /// </summary>
    public class GameEvent
    {
        public string k;
        public string tower;      // TowerId
        public double x;
        public double y;
        public double tx;
        public double ty;
        public double uid;
        public string type;       // DamageType
        public double amount;
        public double splash;
        public List<double> pts;
        public double r;
        public string enemy;      // EnemyId
        public double gold;
        public bool elite;
        /// <summary>'kill' und 'waveStart': TS-Feld <c>boss: boolean</c>.</summary>
        public bool isBoss;
        public double lives;
        public string id;         // reaction: Reaktions-ID; ability: AbilityId
        public double refund;
        public double wave;
        public double bonus;
        /// <summary>'bossPhase' (BossId) und 'death' (BossId | null).</summary>
        public string boss;
        public double phase;
        public string name;
        public string stage;      // 'warn' | 'cast' | 'interrupt'
        public double x2;
        public double y2;
        public string text;
        public string severity;   // 'info' | 'warn' | 'danger'
        public string hero;       // HeroId
        public string ability;
        public bool melee;
        public double level;
        public bool big;
        public double power;
        public string active;     // TowerActiveId
        public double left;
        public double color;
        public double size;
        public bool up;
        public string skill;
        public string kind;       // PickupKind
        public string label;
        public string potion;     // PotionId
        public string item;       // ItemId
        public double total;
    }


    // ------------------------------------------------------------------ Helden-Endlosmodus

    /// <summary>
    /// Attribute eines frei steuerbaren Helden. Zusätzlich zum TS-Interface: Indexer <c>this[string]</c>
    /// (für <c>st[key as keyof ChampionStats]</c>) und <see cref="KEYS"/> in Deklarationsreihenfolge.
    /// </summary>
    public class ChampionStats
    {
        public double maxHp;
        public double hpRegen;
        public double maxMana;
        public double manaRegen;
        public double damage;
        public double attackSpeed;     // Angriffe pro Sekunde
        public double range;
        public double moveSpeed;       // Felder pro Sekunde
        public double armor;           // 0..0,8 Schadensreduktion
        public double critChance;
        public double critDamage;      // Multiplikator
        public double cdr;             // Abklingzeitreduktion 0..0,5
        public double abilityPower;    // Multiplikator für Fähigkeiten
        public double supportPower;    // Multiplikator für Turm-Verstärkungen
        public double supportRadius;   // Felder
        public double lootBonus;       // zusätzliche Dropchance (Faktor)
        public double pickupRadius;    // Felder
        public double lifesteal;
        public double cleave;          // Flächenanteil von Standardangriffen

        public static readonly string[] KEYS = { "maxHp", "hpRegen", "maxMana", "manaRegen", "damage", "attackSpeed", "range", "moveSpeed", "armor", "critChance", "critDamage", "cdr", "abilityPower", "supportPower", "supportRadius", "lootBonus", "pickupRadius", "lifesteal", "cleave" };

        public double this[string key]
        {
            get
            {
                switch (key)
                {
                    case "maxHp": return maxHp;
                    case "hpRegen": return hpRegen;
                    case "maxMana": return maxMana;
                    case "manaRegen": return manaRegen;
                    case "damage": return damage;
                    case "attackSpeed": return attackSpeed;
                    case "range": return range;
                    case "moveSpeed": return moveSpeed;
                    case "armor": return armor;
                    case "critChance": return critChance;
                    case "critDamage": return critDamage;
                    case "cdr": return cdr;
                    case "abilityPower": return abilityPower;
                    case "supportPower": return supportPower;
                    case "supportRadius": return supportRadius;
                    case "lootBonus": return lootBonus;
                    case "pickupRadius": return pickupRadius;
                    case "lifesteal": return lifesteal;
                    case "cleave": return cleave;
                    default: throw new Exception("ChampionStats: unbekannter Schlüssel " + key);
                }
            }
            set
            {
                switch (key)
                {
                    case "maxHp": maxHp = value; break;
                    case "hpRegen": hpRegen = value; break;
                    case "maxMana": maxMana = value; break;
                    case "manaRegen": manaRegen = value; break;
                    case "damage": damage = value; break;
                    case "attackSpeed": attackSpeed = value; break;
                    case "range": range = value; break;
                    case "moveSpeed": moveSpeed = value; break;
                    case "armor": armor = value; break;
                    case "critChance": critChance = value; break;
                    case "critDamage": critDamage = value; break;
                    case "cdr": cdr = value; break;
                    case "abilityPower": abilityPower = value; break;
                    case "supportPower": supportPower = value; break;
                    case "supportRadius": supportRadius = value; break;
                    case "lootBonus": lootBonus = value; break;
                    case "pickupRadius": pickupRadius = value; break;
                    case "lifesteal": lifesteal = value; break;
                    case "cleave": cleave = value; break;
                    default: throw new Exception("ChampionStats: unbekannter Schlüssel " + key);
                }
            }
        }

        /// <summary>Flache Kopie (TS: <c>{ ...def.base }</c>).</summary>
        public ChampionStats Copy() { return (ChampionStats)MemberwiseClone(); }
    }

    /// <summary>Zeitlich begrenzte Wirkung am Helden (Tränke, Fähigkeiten). Gleiche Quelle erneuert statt zu stapeln.</summary>
    public class ChampionBuff
    {
        public string src;
        public double t;
        public double dur;
        public Dictionary<string, double> add = new Dictionary<string, double>();   // Partial<ChampionStats>
    }

    public class ItemInstance
    {
        public double uid;
        public string item;   // ItemId
        /// <summary>Verstärkungsstufe 0..3 (je +15 % Itemwerte).</summary>
        public double plus;
    }

    public class PotionStack
    {
        public string potion;   // PotionId
        public double count;
    }

    /// <summary>Zeitlich begrenzte Turmverstärkung. Je Turm und Quelle höchstens ein Eintrag.</summary>
    public class TowerBuff
    {
        public double tower;
        public string src;
        public double t;
        public double dur;
        public double dmg;
        public double speed;
        public double range;
        public double crit;
    }

    public class ChampionState
    {
        public string id;   // ChampionId
        public double x;
        public double y;
        /// <summary>Haltepunkt (letzter Bewegungsbefehl)</summary>
        public double hx;
        public double hy;
        public List<double> path = new List<double>();   // Zellindizes des aktuellen Wegs
        public double hp;
        public double mana;
        public double level;
        public double xp;
        public double skillPoints;
        public Dictionary<string, double> tree = new Dictionary<string, double>();
        public List<double> skillCd = new List<double>();
        public double attackCd;
        public double target;
        public double facing;
        public double anim;
        public List<ChampionBuff> buffs = new List<ChampionBuff>();
        /// <summary>Item-Instanz-UID je Slot (6); TS (number | null)[] → List&lt;double?&gt;.</summary>
        public List<double?> equipment = new List<double?>();
        public PotionStack specialPotion;   // Trank im Spezialslot (null möglich)
        public List<ItemInstance> inventory = new List<ItemInstance>();
        public List<PotionStack> potions = new List<PotionStack>();
        public Dictionary<string, double> potionCd = new Dictionary<string, double>();   // Partial<Record<PotionId, number>>
        public double dead;          // >0 = Wiederbelebung in s
        public double channel;       // laufende Kanalisierung (Klingensturm)
        public double channelTick;
        public double kills;
        public double damageDealt;
        /// <summary>Position festgesetzt: Klicks bewegen den Helden nicht, er verfolgt keine Gegner.</summary>
        public bool? holdPos;
    }

    public class Pickup
    {
        public double uid;
        public string kind;   // PickupKind
        public double x;
        public double y;
        public double amount;
        public string @ref;   // Ressourcen-, Item- oder Trank-ID
        public double t;      // Restlebensdauer
    }

    /// <summary>Kurzlebige Heldenwirkungen am Boden (Fallen, Pfeilhagel).</summary>
    public class ChampionZone
    {
        public double uid;
        public string kind;   // 'snare' | 'volley'
        public double x;
        public double y;
        public double r;
        public double t;
        public double tick;
        public double power;
    }

    /// <summary>TS: inline-Typ <c>HeroEndlessState.paragon: { level; xp; points: Record&lt;string, number&gt;; rev }</c>.</summary>
    public class HeroEndlessParagon
    {
        public double level;
        public double xp;
        public Dictionary<string, double> points = new Dictionary<string, double>();
        public double rev;
    }

    /// <summary>TS: inline-Typ <c>HeroEndlessState.stats</c>.</summary>
    public class HeroEndlessRunStats
    {
        public double xpCollected;
        public double itemsCrafted;
        public double itemsFound;
        public double potionsUsed;
        public double resourcesEarned;
        public double levelUps;
    }

    /// <summary>TS: inline-Typ <c>HeroEndlessState.mining: { cell: number; t: number; total: number } | null</c>.</summary>
    public class MiningState
    {
        public double cell;
        public double t;
        public double total;
    }

    public class HeroEndlessState
    {
        /// <summary>3.0: Paragonpunkte aus Meilensteinen (Bestwelle beim Start).</summary>
        public double? paragonBonus;
        /// <summary>3.0: Turm-Meisterschaft – Abschüsse je Turmtyp (dauerhafter Stand beim Start + dieser Lauf).</summary>
        public Dictionary<string, double> mastery;
        /// <summary>3.0: Kampfdauer der aktuellen Welle für nachrückende Wellen.</summary>
        public double? overrunT;
        public double? overrunWave;
        /// <summary>Runen (2.7): gefundene Runen; tower = UID des Turms, in dem sie gesockelt ist, 0 = im Beutel.</summary>
        public List<RuneInstance> runes;
        /// <summary>Paragon (2.7): gemeinsamer, dauerhafter Stand aller Helden; rev steigt bei jeder Punktvergabe.</summary>
        public HeroEndlessParagon paragon;
        public string runId;
        public ChampionState champion;
        public List<Pickup> pickups = new List<Pickup>();
        public List<ChampionZone> zones = new List<ChampionZone>();
        public Dictionary<string, double> resources = new Dictionary<string, double>();   // Record<ResourceId, number>
        public List<TowerBuff> towerBuffs = new List<TowerBuff>();
        /// <summary>Befehlsbanner des Helden (null = nicht gesetzt) und Abklingzeit bis zum nächsten Versetzen (s).</summary>
        public HeroBanner banner;
        public double bannerCd;
        /// <summary>Taktikladungen: Vorrat, gemeinsame Abklingzeit, Restdauer von „Schwachstelle“, Anzahl genutzt.</summary>
        public double charges;
        public double chargeCd;
        public double exposeT;
        public double chargesUsed;
        /// <summary>Doktrin des Laufs (Doctrines.DOCTRINES).</summary>
        public string doctrine;
        /// <summary>TS: <c>{ x: number; y: number }</c>.</summary>
        public Vec2 shop;
        /// <summary>Countdown bis zum automatischen Wellenstart (s); &lt;0 = inaktiv</summary>
        public double autoTimer;
        /// <summary>Bei Partiebeginn bekannte, dauerhaft freigeschaltete Items.</summary>
        public List<string> metaOwned = new List<string>();
        /// <summary>In dieser Partie neu freigeschaltete Items (gedroppt oder hergestellt).</summary>
        public List<string> runUnlocks = new List<string>();
        /// <summary>Aktueller Fragmentbestand (Sammlung + Partie).</summary>
        public Dictionary<string, double> fragments = new Dictionary<string, double>();
        /// <summary>Veränderung des Fragmentbestands gegenüber Partiebeginn (wird beim Abschluss verbucht).</summary>
        public Dictionary<string, double> fragmentDelta = new Dictionary<string, double>();
        /// <summary>Aktive, dauerhafte Perks (bei Partiebeginn aus dem Fortschritt übernommen).</summary>
        public List<string> perks = new List<string>();
        public double bestWaveAtStart;
        public Dictionary<string, double> shopStock = new Dictionary<string, double>();
        public double stockWave;
        /// <summary>Profil der aktuellen bzw. nächsten Welle.</summary>
        public string profile;
        /// <summary>Höchste Welle, deren Wellenbelohnung (Ressourcen) bereits vergeben wurde.</summary>
        public double rewardedWave;
        public HeroEndlessRunStats stats;
        /// <summary>Passiver Baum (Schnappschuss bei Laufbeginn; dauerhafter Fortschritt).</summary>
        public Dictionary<string, double> passive;
        /// <summary>Laufender Abbau eines Hindernisses (Baum, Fels …) – nicht unterbrechbar. null = keiner.</summary>
        public MiningState mining;
        /// <summary>Hindernis, zu dem der Held läuft, um es abzubauen.</summary>
        public double? mineTarget;
        /// <summary>Abgebaute Hindernisse in diesem Lauf.</summary>
        public double? mined;
    }
}
