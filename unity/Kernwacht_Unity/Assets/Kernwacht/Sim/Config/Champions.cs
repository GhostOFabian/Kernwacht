// Übersetzung von config/champions.ts.
// Frei steuerbare Helden des Helden-Endlosmodus: Grundwerte, Stufenwachstum, Fähigkeiten und
// Fähigkeitenbäume. Neue Helden werden ausschließlich hier ergänzt (plus Fähigkeitslogik im ChampionSystem).

using System;
using System.Collections.Generic;

namespace Kernwacht.Sim
{
    // SkillTarget = 'self' | 'tower' | 'point' (string)

    public class ChampionSkill
    {
        public string id;
        public string name;
        public string description;
        public string icon;          // SVG-Symbolname (ui/icons.ts)
        public double cooldown;
        public double mana;
        public string target;        // SkillTarget
        /// <summary>Maximale Wirkentfernung vom Helden (0 = am Helden).</summary>
        public double castRange;
        /// <summary>Wirkungsradius (für Vorschau).</summary>
        public double radius;
        public double unlockLevel;
        /// <summary>Datengetriebene Wirkung (neue Helden). Ohne Angabe (null): eigene Logik im ChampionSystem.</summary>
        public List<SkillEffect> effects;
    }

    public class StatusSpec
    {
        public string id;   // StatusId
        public double dur;
        public double? power;
        public double? stacks;
    }

    /// <summary>
    /// Bausteine für Heldenfähigkeiten. Schaden = (base + scale × Heldenschaden) × Fähigkeitsstärke,
    /// Turmwerte × Unterstützungsstärke. 'at' = Wirkort (Held oder Zielpunkt).
    /// Diskriminierte Union → eine Klasse; Diskriminator ist <see cref="kind"/>. Varianten:
    ///  nova{at,radius,base,scale,status?,air?} line{length,width,base,scale,status?} blink{}
    ///  execute{count,pct,bossPct} towers{at,radius,dmg?,speed?,range?,crit?,dur,repair?,src}
    ///  allTowers{dmg?,speed?,range?,crit?,dur,src} self{add?,pct?→pctStats,dur,heal?,src}
    ///  strikes{at,radius,count,base,scale,status?} statusAll{radius,status,at?} gold{base,perWave,resource?,amount?}
    ///  taunt{radius,dur} interrupt{radius,power} zone{zone,radius,dur,power,scale?}
    /// PORT: Namenskonflikt beim Feld <c>pct</c>: bei 'execute' ist es eine Zahl → <see cref="pct"/> (double?);
    /// bei 'self' ist es Partial&lt;Record&lt;keyof ChampionStats, number&gt;&gt; → <see cref="pctStats"/>.
    /// <c>scale</c> ist nullable (bei 'zone' optional); Felder, die in allen ihren Varianten Pflicht sind, sind einfache double (Standard 0).
    /// </summary>
    public class SkillEffect
    {
        public string kind;   // 'nova' | 'line' | 'blink' | 'execute' | 'towers' | 'allTowers' | 'self' | 'strikes' | 'statusAll' | 'gold' | 'taunt' | 'interrupt' | 'zone'
        public string at;     // 'self' | 'target' (bei statusAll optional → null)
        public double radius;
        public double @base;
        public double? scale;
        public StatusSpec status;
        public bool? air;
        public double length;
        public double width;
        public double count;
        /// <summary>'execute': Anteil der max. LP.</summary>
        public double? pct;
        public double bossPct;
        public double? dmg;
        public double? speed;
        public double? range;
        public double? crit;
        public double dur;
        public bool? repair;
        public string src;
        public Dictionary<string, double> add;        // Partial<ChampionStats> oder null
        /// <summary>'self': TS-Feld <c>pct</c> (Partial&lt;Record&lt;keyof ChampionStats, number&gt;&gt;) oder null.</summary>
        public Dictionary<string, double> pctStats;
        public double? heal;
        public double perWave;
        public string resource;   // 'metal' | 'crystal' | 'essence' | 'relic' oder null
        public double? amount;
        public string zone;       // 'snare' | 'volley'
        public double power;
    }

    /// <summary>
    /// Fähigkeitsmodifikatoren aus dem Baum (additiv je Rang). Zusätzlich: Indexer <c>this[string]</c>
    /// (für <c>out[k as keyof SkillMods]</c>), <see cref="KEYS"/> und <see cref="Copy"/>.
    /// </summary>
    public class SkillMods
    {
        public double buffDuration;
        public double buffRadius;
        public double extraTargets;
        public double synergy;
        public double auraDmg;
        public double auraSpeed;
        public double auraRange;
        public double auraCrit;
        public double whirlRadius;
        public double dashStun;
        public double pierceWidth;
        public double trapSlow;
        public double volleyPulses;

        public static readonly string[] KEYS = { "buffDuration", "buffRadius", "extraTargets", "synergy", "auraDmg", "auraSpeed", "auraRange", "auraCrit", "whirlRadius", "dashStun", "pierceWidth", "trapSlow", "volleyPulses" };

        public double this[string key]
        {
            get
            {
                switch (key)
                {
                    case "buffDuration": return buffDuration;
                    case "buffRadius": return buffRadius;
                    case "extraTargets": return extraTargets;
                    case "synergy": return synergy;
                    case "auraDmg": return auraDmg;
                    case "auraSpeed": return auraSpeed;
                    case "auraRange": return auraRange;
                    case "auraCrit": return auraCrit;
                    case "whirlRadius": return whirlRadius;
                    case "dashStun": return dashStun;
                    case "pierceWidth": return pierceWidth;
                    case "trapSlow": return trapSlow;
                    case "volleyPulses": return volleyPulses;
                    default: throw new Exception("SkillMods: unbekannter Schlüssel " + key);
                }
            }
            set
            {
                switch (key)
                {
                    case "buffDuration": buffDuration = value; break;
                    case "buffRadius": buffRadius = value; break;
                    case "extraTargets": extraTargets = value; break;
                    case "synergy": synergy = value; break;
                    case "auraDmg": auraDmg = value; break;
                    case "auraSpeed": auraSpeed = value; break;
                    case "auraRange": auraRange = value; break;
                    case "auraCrit": auraCrit = value; break;
                    case "whirlRadius": whirlRadius = value; break;
                    case "dashStun": dashStun = value; break;
                    case "pierceWidth": pierceWidth = value; break;
                    case "trapSlow": trapSlow = value; break;
                    case "volleyPulses": volleyPulses = value; break;
                    default: throw new Exception("SkillMods: unbekannter Schlüssel " + key);
                }
            }
        }

        /// <summary>Flache Kopie (TS: <c>{ ...ZERO_SKILL_MODS }</c>).</summary>
        public SkillMods Copy() { return (SkillMods)MemberwiseClone(); }
    }

    /// <summary>TS: inline-Typ <c>SkillNode.requires[i]: { id: string; rank: number }</c>.</summary>
    public class SkillNodeRequirement
    {
        public string id;
        public double rank;
    }

    public class SkillNode
    {
        public string id;
        public string name;
        public string description;
        public double tier;
        public double maxRank;
        /// <summary>Wirkung je Rang. Partial&lt;ChampionStats&gt; oder null.</summary>
        public Dictionary<string, double> stats;
        /// <summary>Partial&lt;SkillMods&gt; oder null.</summary>
        public Dictionary<string, double> mods;
        public List<SkillNodeRequirement> requires;
        public double? minLevel;
        /// <summary>Dauerhafter Perk, der den Knoten freischaltet.</summary>
        public string perk;
        /// <summary>Erhöht Kernenergie (max. und aktuell) je Rang – wirkt sofort beim Lernen.</summary>
        public double? coreLives;
    }

    /// <summary>TS: inline-Typ <c>ChampionDefinition.aura: { dmg: number; speed: number }</c>.</summary>
    public class ChampionAura
    {
        public double dmg;
        public double speed;
    }

    public class ChampionDefinition
    {
        public string id;   // ChampionId
        public string name;
        public string title;
        public string role;   // 'Unterstützer' | 'Kämpfer' | 'Fernkämpferin'
        public string description;
        public string model;  // HeroId – vorhandenes 3D-Modell
        public double color;
        public double accent;
        public string damageType;   // DamageType
        public bool melee;
        public ChampionStats @base;
        public Dictionary<string, double> perLevel = new Dictionary<string, double>();   // Partial<ChampionStats>
        /// <summary>TS-Tupel [ChampionSkill × 4] → Array der Länge 4.</summary>
        public ChampionSkill[] skills;
        public List<SkillNode> tree = new List<SkillNode>();
        /// <summary>Passive Turmaura (Grundwerte, × Unterstützungsstärke). null = keine.</summary>
        public ChampionAura aura;
        /// <summary>Dauerhafter Perk, der den Helden freischaltet (null = von Beginn an).</summary>
        public string unlockPerk;
        /// <summary>Kampagnenheld: frei, sobald er in der Kampagne freigeschaltet ist – oder ab Bestwelle unlockWave.</summary>
        public string campaignHero;   // HeroId oder null
        public double? unlockWave;
        /// <summary>Hält bis zu n Bodengegner in Nahkampfreichweite fest (Blocker).</summary>
        public double? blockCount;
    }

    /// <summary>TS: inline-Parametertyp <c>o</c> von <c>campaignTree</c>.</summary>
    public class CampaignTreeOptions
    {
        public Dictionary<string, double> s1;
        public string s1n;
        public string s1d;
        public Dictionary<string, double> s2;
        public string s2n;
        public string s2d;
        public Dictionary<string, double> s3;
        public string s3n;
        public string s3d;
        public Dictionary<string, double> m1;
        public string m1n;
        public string m1d;
    }

    public static partial class Champions
    {
        public static readonly SkillMods ZERO_SKILL_MODS = new SkillMods
        {
            buffDuration = 0, buffRadius = 0, extraTargets = 0, synergy = 0, auraDmg = 0, auraSpeed = 0, auraRange = 0, auraCrit = 0,
            whirlRadius = 0, dashStun = 0, pierceWidth = 0, trapSlow = 0, volleyPulses = 0,
        };

        /// <summary>Für alle Helden: Kernwall (+3 Kernenergie je Rang) – die Zahl erlaubter Durchbrüche ist skillbar.</summary>
        private static SkillNode coreNode(string p)
        {
            return new SkillNode { id = $"{p}_core", name = "Kernwall", tier = 1, maxRank = 5, coreLives = 3, description = "+3 Kernenergie (maximal und sofort) je Rang – mehr Gegner dürfen durchbrechen." };
        }

        /// <summary>Einheitlicher Baum für Kampagnenhelden (11 Knoten, Stufe 4 mit Meisterknoten).</summary>
        private static List<SkillNode> campaignTree(string p, CampaignTreeOptions o)
        {
            return new List<SkillNode>
            {
                new SkillNode { id = $"{p}_a", name = o.s1n, tier = 1, maxRank = 5, stats = o.s1, description = $"{o.s1d} je Rang." },
                new SkillNode { id = $"{p}_hp", name = "Zähigkeit", tier = 1, maxRank = 5, stats = new Dictionary<string, double> { ["maxHp"] = 35, ["armor"] = 0.008 }, description = "+35 LP und +0,8 % Rüstung je Rang." },
                coreNode(p),
                new SkillNode { id = $"{p}_b", name = o.s2n, tier = 2, maxRank = 3, stats = o.s2, requires = new List<SkillNodeRequirement> { new SkillNodeRequirement { id = $"{p}_a", rank = 2 } }, description = $"{o.s2d} je Rang." },
                new SkillNode { id = $"{p}_ap", name = "Fähigkeitsfokus", tier = 2, maxRank = 3, stats = new Dictionary<string, double> { ["abilityPower"] = 0.08, ["cdr"] = 0.03 }, requires = new List<SkillNodeRequirement> { new SkillNodeRequirement { id = $"{p}_hp", rank = 2 } }, description = "+8 % Fähigkeitsstärke und +3 % Abklingzeitreduktion je Rang." },
                new SkillNode { id = $"{p}_mana", name = "Manaquelle", tier = 2, maxRank = 3, stats = new Dictionary<string, double> { ["maxMana"] = 15, ["manaRegen"] = 0.8 }, description = "+15 Mana und +0,8 Manaregeneration je Rang." },
                new SkillNode { id = $"{p}_c", name = o.s3n, tier = 3, maxRank = 3, stats = o.s3, requires = new List<SkillNodeRequirement> { new SkillNodeRequirement { id = $"{p}_b", rank = 1 } }, minLevel = 5, description = $"{o.s3d} je Rang." },
                new SkillNode { id = $"{p}_life", name = "Lebenswille", tier = 3, maxRank = 3, stats = new Dictionary<string, double> { ["hpRegen"] = 1.5, ["lifesteal"] = 0.02 }, requires = new List<SkillNodeRequirement> { new SkillNodeRequirement { id = $"{p}_ap", rank = 1 } }, description = "+1,5 LP/s und +2 % Lebensraub je Rang." },
                new SkillNode { id = $"{p}_move", name = "Leichtfuß", tier = 3, maxRank = 2, stats = new Dictionary<string, double> { ["moveSpeed"] = 0.25, ["pickupRadius"] = 0.15 }, description = "+0,25 Tempo und +0,15 Aufsammelradius je Rang." },
                new SkillNode { id = $"{p}_master", name = o.m1n, tier = 4, maxRank = 1, stats = o.m1, requires = new List<SkillNodeRequirement> { new SkillNodeRequirement { id = $"{p}_c", rank = 2 } }, minLevel = 10, perk = "mastery", description = $"Meisterknoten: {o.m1d}." },
                new SkillNode { id = $"{p}_wall", name = "Unerschütterlich", tier = 4, maxRank = 1, stats = new Dictionary<string, double> { ["maxHp"] = 180, ["armor"] = 0.06 }, requires = new List<SkillNodeRequirement> { new SkillNodeRequirement { id = $"{p}_life", rank = 1 } }, minLevel = 10, perk = "mastery", description = "Meisterknoten: +180 LP und +6 % Rüstung." },
            };
        }

        private static readonly ChampionStats BASE = new ChampionStats
        {
            maxHp = 400, hpRegen = 1, maxMana = 100, manaRegen = 4, damage = 10, attackSpeed = 1, range = 1, moveSpeed = 2.6,
            armor = 0, critChance = 0.05, critDamage = 1.6, cdr = 0, abilityPower = 1, supportPower = 1, supportRadius = 2.5,
            lootBonus = 0, pickupRadius = 0.9, lifesteal = 0, cleave = 0,
        };

        /// <summary>TS: <c>{ ...src, ...o }</c> für ChampionStats.</summary>
        private static ChampionStats ext(ChampionStats src, Dictionary<string, double> o)
        {
            ChampionStats st = src.Copy();
            foreach (KeyValuePair<string, double> kv in o) st[kv.Key] = kv.Value;
            return st;
        }

        public static readonly Dictionary<string, ChampionDefinition> CHAMPIONS = new Dictionary<string, ChampionDefinition>
        {
            ["lyra"] = new ChampionDefinition
            {
                id = "lyra", name = "Lyra", title = "Turmweberin", role = "Unterstützer", model = "mystic", color = 0x6a4ab0, accent = 0xffd860,
                description = "Verstärkt Türme mit Auren und gezielten Runen. Schwach im direkten Kampf, aber mit klugem Stellungsspiel kann sie ganze Verteidigungslinien tragen.",
                damageType = "arcane", melee = false, aura = new ChampionAura { dmg = 0.05, speed = 0.12 },
                @base = ext(BASE, new Dictionary<string, double> { ["maxHp"] = 360, ["damage"] = 9, ["attackSpeed"] = 0.9, ["range"] = 3, ["supportRadius"] = 2.6, ["maxMana"] = 130, ["manaRegen"] = 5 }),
                perLevel = new Dictionary<string, double> { ["maxHp"] = 26, ["damage"] = 1.2, ["maxMana"] = 8, ["supportPower"] = 0.03 },
                skills = new ChampionSkill[]
                {
                    new ChampionSkill
                    {
                        id = "inspire", name = "Runensegen", icon = "up", cooldown = 8, mana = 25, target = "tower", castRange = 6, radius = 0.6, unlockLevel = 1,
                        description = "Ein Turm erhält 8 s lang +50 % Schaden und +30 % Angriffstempo.",
                    },
                    new ChampionSkill
                    {
                        id = "field", name = "Kraftfeld", icon = "range", cooldown = 14, mana = 35, target = "point", castRange = 7, radius = 2.5, unlockLevel = 2,
                        description = "Türme im Feld erhalten 10 s lang +35 % Angriffstempo und +0,5 Reichweite.",
                    },
                    new ChampionSkill
                    {
                        id = "pulse", name = "Resonanzpuls", icon = "bolt", cooldown = 20, mana = 40, target = "self", castRange = 0, radius = 4, unlockLevel = 4,
                        description = "Türme im Umkreis feuern sofort erneut und erhalten 6 s lang +15 % kritische Trefferchance.",
                    },
                    new ChampionSkill
                    {
                        id = "citadel", name = "Zitadelle", icon = "crown", cooldown = 60, mana = 70, target = "point", castRange = 7, radius = 3.5, unlockLevel = 6,
                        description = "Ultimate: Türme im Bereich erhalten 12 s lang +50 % Schaden, +40 % Tempo, +0,6 Reichweite und +10 % Krit.",
                    },
                },
                tree = new List<SkillNode>
                {
                    new SkillNode { id = "l_dmg", name = "Turmmacht", tier = 1, maxRank = 5, mods = new Dictionary<string, double> { ["auraDmg"] = 0.04 }, description = "Aura: +4 % Turmschaden je Rang." },
                    new SkillNode { id = "l_spd", name = "Taktgeber", tier = 1, maxRank = 5, mods = new Dictionary<string, double> { ["auraSpeed"] = 0.04 }, description = "Aura: +4 % Turmangriffstempo je Rang." },
                    coreNode("l"),
                    new SkillNode { id = "l_mana", name = "Runenfluss", tier = 1, maxRank = 3, stats = new Dictionary<string, double> { ["maxMana"] = 15, ["manaRegen"] = 1 }, description = "+15 Mana und +1 Manaregeneration je Rang." },
                    new SkillNode { id = "l_range", name = "Weitblick", tier = 2, maxRank = 3, mods = new Dictionary<string, double> { ["auraRange"] = 0.2 }, requires = new List<SkillNodeRequirement> { new SkillNodeRequirement { id = "l_dmg", rank = 2 } }, description = "Aura: +0,2 Turmreichweite je Rang." },
                    new SkillNode { id = "l_dur", name = "Langer Atem", tier = 2, maxRank = 3, mods = new Dictionary<string, double> { ["buffDuration"] = 0.2 }, requires = new List<SkillNodeRequirement> { new SkillNodeRequirement { id = "l_spd", rank = 2 } }, description = "+20 % Dauer aller Turmverstärkungen je Rang." },
                    new SkillNode { id = "l_radius", name = "Weite Kreise", tier = 2, maxRank = 3, mods = new Dictionary<string, double> { ["buffRadius"] = 0.15 }, stats = new Dictionary<string, double> { ["supportRadius"] = 0.3 }, description = "+15 % Wirkradius und +0,3 Aurareichweite je Rang." },
                    new SkillNode { id = "l_multi", name = "Doppelsegen", tier = 3, maxRank = 2, mods = new Dictionary<string, double> { ["extraTargets"] = 1 }, requires = new List<SkillNodeRequirement> { new SkillNodeRequirement { id = "l_dur", rank = 2 } }, minLevel = 6, description = "Runensegen trifft je Rang einen weiteren Turm in der Nähe (halbe Stärke)." },
                    new SkillNode { id = "l_synergy", name = "Elementarchor", tier = 3, maxRank = 3, mods = new Dictionary<string, double> { ["synergy"] = 0.04 }, requires = new List<SkillNodeRequirement> { new SkillNodeRequirement { id = "l_range", rank = 2 } }, description = "Kraftfeld: +4 % Schaden je verschiedener Schadensart im Feld, je Rang." },
                    new SkillNode { id = "l_crit", name = "Präzisionsrunen", tier = 3, maxRank = 3, mods = new Dictionary<string, double> { ["auraCrit"] = 0.04 }, requires = new List<SkillNodeRequirement> { new SkillNodeRequirement { id = "l_radius", rank = 1 } }, description = "Aura: +4 % kritische Trefferchance für Türme je Rang." },
                    new SkillNode { id = "l_master", name = "Erzweberin", tier = 4, maxRank = 1, stats = new Dictionary<string, double> { ["supportPower"] = 0.25 }, requires = new List<SkillNodeRequirement> { new SkillNodeRequirement { id = "l_multi", rank = 1 } }, minLevel = 10, perk = "mastery", description = "Meisterknoten: alle Turmverstärkungen +25 %." },
                    new SkillNode { id = "l_aura", name = "Ewige Aura", tier = 4, maxRank = 1, stats = new Dictionary<string, double> { ["supportRadius"] = 1 }, mods = new Dictionary<string, double> { ["auraDmg"] = 0.1, ["auraSpeed"] = 0.1 }, requires = new List<SkillNodeRequirement> { new SkillNodeRequirement { id = "l_crit", rank = 1 } }, minLevel = 10, perk = "mastery", description = "Meisterknoten: Aura +1 Feld, +10 % Schaden und Tempo." },
                },
            },
            ["brann"] = new ChampionDefinition
            {
                id = "brann", name = "Brann", title = "Klingenrufer", role = "Kämpfer", model = "blade", color = 0xb04a3a, accent = 0xffc080,
                description = "Berserker und Wellenbrecher: hält bis zu zwei Gegner im Nahkampf fest, trifft mit jedem Hieb Umstehende, heilt sich durch Treffer und fordert ganze Gruppen heraus.",
                damageType = "physical", melee = true, blockCount = 2,
                @base = ext(BASE, new Dictionary<string, double> { ["maxHp"] = 820, ["hpRegen"] = 3, ["damage"] = 32, ["attackSpeed"] = 1.25, ["range"] = 1.05, ["armor"] = 0.18, ["moveSpeed"] = 2.8, ["cleave"] = 0.35, ["lifesteal"] = 0.04 }),
                perLevel = new Dictionary<string, double> { ["maxHp"] = 70, ["damage"] = 3.6, ["armor"] = 0.006, ["hpRegen"] = 0.25 },
                skills = new ChampionSkill[]
                {
                    new ChampionSkill
                    {
                        id = "whirl", name = "Wirbelschlag", icon = "sword", cooldown = 6, mana = 20, target = "self", castRange = 0, radius = 1.7, unlockLevel = 1,
                        description = "Trifft alle Gegner im Umkreis von 1,7 Feldern (60 + 150 % Schaden).",
                    },
                    new ChampionSkill
                    {
                        id = "charge", name = "Sturmangriff", icon = "advance", cooldown = 9, mana = 25, target = "point", castRange = 5, radius = 0.8, unlockLevel = 2,
                        description = "Stürmt zu einem Punkt, verletzt Gegner auf dem Weg (80 + 170 % Schaden) und betäubt sie 1,5 s.",
                    },
                    new ChampionSkill
                    {
                        id = "warcry", name = "Herausforderung", icon = "shield", cooldown = 16, mana = 30, target = "self", castRange = 0, radius = 3, unlockLevel = 4,
                        description = "Fesselt Bodengegner im Umkreis von 3 Feldern 2 s; 6 s lang +40 % Angriffstempo und +20 % Rüstung, heilt 15 % der LP.",
                    },
                    new ChampionSkill
                    {
                        id = "bladestorm", name = "Klingensturm", icon = "crown", cooldown = 45, mana = 60, target = "self", castRange = 0, radius = 2, unlockLevel = 6,
                        description = "Ultimate: 5 s lang alle 0,5 s Schaden im Umkreis von 2 Feldern (35 + 80 % Schaden), getroffene Gegner werden verlangsamt.",
                    },
                },
                tree = new List<SkillNode>
                {
                    new SkillNode { id = "b_dmg", name = "Schärfe", tier = 1, maxRank = 5, stats = new Dictionary<string, double> { ["damage"] = 4 }, description = "+4 Schaden je Rang." },
                    new SkillNode { id = "b_hp", name = "Zähigkeit", tier = 1, maxRank = 5, stats = new Dictionary<string, double> { ["maxHp"] = 60, ["armor"] = 0.012 }, description = "+60 LP und +1,2 % Rüstung je Rang." },
                    new SkillNode { id = "b_spd", name = "Schnelle Hiebe", tier = 1, maxRank = 5, stats = new Dictionary<string, double> { ["attackSpeed"] = 0.07 }, description = "+0,07 Angriffe/s je Rang." },
                    coreNode("b"),
                    new SkillNode { id = "b_crit", name = "Schwachstellen", tier = 2, maxRank = 3, stats = new Dictionary<string, double> { ["critChance"] = 0.06 }, requires = new List<SkillNodeRequirement> { new SkillNodeRequirement { id = "b_dmg", rank = 2 } }, description = "+6 % kritische Trefferchance je Rang." },
                    new SkillNode { id = "b_life", name = "Blutdurst", tier = 2, maxRank = 3, stats = new Dictionary<string, double> { ["lifesteal"] = 0.05 }, requires = new List<SkillNodeRequirement> { new SkillNodeRequirement { id = "b_hp", rank = 2 } }, description = "+5 % Lebensraub je Rang." },
                    new SkillNode { id = "b_cleave", name = "Spaltschlag", tier = 2, maxRank = 3, stats = new Dictionary<string, double> { ["cleave"] = 0.15 }, requires = new List<SkillNodeRequirement> { new SkillNodeRequirement { id = "b_spd", rank = 2 } }, description = "Angriffe treffen Umstehende mit +15 % Schaden je Rang." },
                    new SkillNode { id = "b_whirl", name = "Wirbelmeister", tier = 3, maxRank = 3, stats = new Dictionary<string, double> { ["abilityPower"] = 0.12 }, mods = new Dictionary<string, double> { ["whirlRadius"] = 0.15 }, minLevel = 5, requires = new List<SkillNodeRequirement> { new SkillNodeRequirement { id = "b_cleave", rank = 1 } }, description = "+15 % Wirbelradius und +12 % Fähigkeitsstärke je Rang." },
                    new SkillNode { id = "b_dash", name = "Erschütterung", tier = 3, maxRank = 2, mods = new Dictionary<string, double> { ["dashStun"] = 0.5 }, requires = new List<SkillNodeRequirement> { new SkillNodeRequirement { id = "b_whirl", rank = 1 } }, description = "Sturmangriff betäubt 0,5 s länger je Rang." },
                    new SkillNode { id = "b_regen", name = "Regeneration", tier = 3, maxRank = 3, stats = new Dictionary<string, double> { ["hpRegen"] = 2.5 }, requires = new List<SkillNodeRequirement> { new SkillNodeRequirement { id = "b_life", rank = 1 } }, description = "+2,5 LP/s je Rang." },
                    new SkillNode { id = "b_master", name = "Kriegsherr", tier = 4, maxRank = 1, stats = new Dictionary<string, double> { ["damage"] = 16, ["critDamage"] = 0.5 }, requires = new List<SkillNodeRequirement> { new SkillNodeRequirement { id = "b_crit", rank = 2 } }, minLevel = 10, perk = "mastery", description = "Meisterknoten: +16 Schaden, +50 % kritischer Schaden." },
                    new SkillNode { id = "b_wall", name = "Bollwerk", tier = 4, maxRank = 1, stats = new Dictionary<string, double> { ["armor"] = 0.12, ["maxHp"] = 260 }, requires = new List<SkillNodeRequirement> { new SkillNodeRequirement { id = "b_regen", rank = 1 } }, minLevel = 10, perk = "mastery", description = "Meisterknoten: +12 % Rüstung, +260 LP." },
                },
            },
            ["vela"] = new ChampionDefinition
            {
                id = "vela", name = "Vela", title = "Sturmschützin", role = "Fernkämpferin", model = "hunter", color = 0x2a8a70, accent = 0x9affd8,
                description = "Schnelle Fernkämpferin mit durchschlagenden Pfeilen, Fallen und Pfeilhagel. Wird über die Welle 15 freigeschaltet.",
                damageType = "physical", melee = false, unlockPerk = "vela",
                @base = ext(BASE, new Dictionary<string, double> { ["maxHp"] = 440, ["damage"] = 15, ["attackSpeed"] = 1.35, ["range"] = 4, ["critChance"] = 0.08, ["moveSpeed"] = 2.9 }),
                perLevel = new Dictionary<string, double> { ["maxHp"] = 32, ["damage"] = 2, ["attackSpeed"] = 0.02 },
                skills = new ChampionSkill[]
                {
                    new ChampionSkill
                    {
                        id = "pierce", name = "Durchschlag", icon = "advance", cooldown = 7, mana = 25, target = "point", castRange = 7, radius = 0.4, unlockLevel = 1,
                        description = "Ein Pfeil durchbohrt alle Gegner auf einer Linie (50 + 140 % Schaden).",
                    },
                    new ChampionSkill
                    {
                        id = "trap", name = "Fallensaat", icon = "range", cooldown = 12, mana = 30, target = "point", castRange = 6, radius = 1.3, unlockLevel = 2,
                        description = "Eine Falle verlangsamt Gegner 6 s lang stark und fügt Schaden über Zeit zu.",
                    },
                    new ChampionSkill
                    {
                        id = "focus", name = "Fokus", icon = "star", cooldown = 20, mana = 30, target = "self", castRange = 0, radius = 0, unlockLevel = 4,
                        description = "8 s lang +35 % kritische Trefferchance und +20 % Angriffstempo.",
                    },
                    new ChampionSkill
                    {
                        id = "volley", name = "Pfeilhagel", icon = "crown", cooldown = 45, mana = 60, target = "point", castRange = 8, radius = 2.2, unlockLevel = 6,
                        description = "Ultimate: drei Salven auf ein Gebiet (je 30 + 80 % Schaden).",
                    },
                },
                tree = new List<SkillNode>
                {
                    new SkillNode { id = "v_dmg", name = "Spitzen", tier = 1, maxRank = 5, stats = new Dictionary<string, double> { ["damage"] = 2.5 }, description = "+2,5 Schaden je Rang." },
                    new SkillNode { id = "v_spd", name = "Schnellschuss", tier = 1, maxRank = 5, stats = new Dictionary<string, double> { ["attackSpeed"] = 0.07 }, description = "+0,07 Angriffe/s je Rang." },
                    coreNode("v"),
                    new SkillNode { id = "v_range", name = "Adlerblick", tier = 1, maxRank = 3, stats = new Dictionary<string, double> { ["range"] = 0.3 }, description = "+0,3 Reichweite je Rang." },
                    new SkillNode { id = "v_crit", name = "Tödliche Präzision", tier = 2, maxRank = 3, stats = new Dictionary<string, double> { ["critChance"] = 0.06, ["critDamage"] = 0.1 }, requires = new List<SkillNodeRequirement> { new SkillNodeRequirement { id = "v_dmg", rank = 2 } }, description = "+6 % Krit und +10 % kritischer Schaden je Rang." },
                    new SkillNode { id = "v_pierce", name = "Breitspitzen", tier = 2, maxRank = 3, stats = new Dictionary<string, double> { ["abilityPower"] = 0.08 }, mods = new Dictionary<string, double> { ["pierceWidth"] = 0.1 }, requires = new List<SkillNodeRequirement> { new SkillNodeRequirement { id = "v_spd", rank = 2 } }, description = "Durchschlag breiter, +8 % Fähigkeitsstärke je Rang." },
                    new SkillNode { id = "v_trap", name = "Fallenkunde", tier = 2, maxRank = 3, mods = new Dictionary<string, double> { ["trapSlow"] = 0.1 }, requires = new List<SkillNodeRequirement> { new SkillNodeRequirement { id = "v_range", rank = 1 } }, description = "Fallen verlangsamen 10 % stärker je Rang." },
                    new SkillNode { id = "v_volley", name = "Salvenmeisterin", tier = 3, maxRank = 2, mods = new Dictionary<string, double> { ["volleyPulses"] = 1 }, requires = new List<SkillNodeRequirement> { new SkillNodeRequirement { id = "v_pierce", rank = 1 } }, minLevel = 6, description = "Pfeilhagel: eine Salve mehr je Rang." },
                    new SkillNode { id = "v_move", name = "Leichtfuß", tier = 3, maxRank = 3, stats = new Dictionary<string, double> { ["moveSpeed"] = 0.25 }, description = "+0,25 Bewegungstempo je Rang." },
                    new SkillNode { id = "v_mana", name = "Konzentration", tier = 3, maxRank = 3, stats = new Dictionary<string, double> { ["manaRegen"] = 1, ["cdr"] = 0.03 }, requires = new List<SkillNodeRequirement> { new SkillNodeRequirement { id = "v_trap", rank = 1 } }, description = "+1 Manaregeneration, +3 % Abklingzeitreduktion je Rang." },
                    new SkillNode { id = "v_master", name = "Sturmherrin", tier = 4, maxRank = 1, stats = new Dictionary<string, double> { ["attackSpeed"] = 0.3, ["abilityPower"] = 0.2 }, requires = new List<SkillNodeRequirement> { new SkillNodeRequirement { id = "v_crit", rank = 2 } }, minLevel = 10, perk = "mastery", description = "Meisterknoten: +0,3 Angriffe/s, +20 % Fähigkeitsstärke." },
                },
            },
            // ---------------------------------------------------------------- Kampagnenhelden
            ["kaela"] = new ChampionDefinition
            {
                id = "kaela", name = "Kaela", title = "Klingenwächterin", role = "Kämpfer", model = "blade", color = Heroes.HEROES["blade"].color, accent = Heroes.HEROES["blade"].accent,
                description = "Die Schildträgerin der Kampagne: hält bis zu vier Gegner fest, verspottet ganze Gruppen und wird mit dem Bollwerk nahezu unverwundbar.",
                damageType = "physical", melee = true, blockCount = 4, campaignHero = "blade", unlockWave = 1,
                @base = ext(BASE, new Dictionary<string, double> { ["maxHp"] = 900, ["hpRegen"] = 3, ["damage"] = 20, ["attackSpeed"] = 1.0, ["range"] = 1.05, ["armor"] = 0.25, ["moveSpeed"] = 2.5, ["cleave"] = 0.2 }),
                perLevel = new Dictionary<string, double> { ["maxHp"] = 80, ["damage"] = 2.2, ["armor"] = 0.008, ["hpRegen"] = 0.3 },
                skills = new ChampionSkill[]
                {
                    new ChampionSkill
                    {
                        id = "k_taunt", name = "Spottschrei", icon = "shield", cooldown = 12, mana = 25, target = "self", castRange = 0, radius = 3, unlockLevel = 1,
                        description = "Fesselt Bodengegner im Umkreis von 3 Feldern 2 s; 6 s +25 % Rüstung.",
                        effects = new List<SkillEffect> { new SkillEffect { kind = "taunt", radius = 3, dur = 2 }, new SkillEffect { kind = "self", add = new Dictionary<string, double> { ["armor"] = 0.25 }, dur = 6, src = "k_taunt" } },
                    },
                    new ChampionSkill
                    {
                        id = "k_whirl", name = "Wirbelklinge", icon = "sword", cooldown = 7, mana = 20, target = "self", castRange = 0, radius = 1.6, unlockLevel = 2,
                        description = "Trifft alle Gegner im Umkreis von 1,6 Feldern (50 + 130 % Schaden).",
                        effects = new List<SkillEffect> { new SkillEffect { kind = "nova", at = "self", radius = 1.6, @base = 50, scale = 1.3 } },
                    },
                    new ChampionSkill
                    {
                        id = "k_wall", name = "Schildwall", icon = "hold", cooldown = 18, mana = 30, target = "self", castRange = 0, radius = 0, unlockLevel = 4,
                        description = "Heilt 20 % der LP, 6 s +35 % Rüstung.",
                        effects = new List<SkillEffect> { new SkillEffect { kind = "self", add = new Dictionary<string, double> { ["armor"] = 0.35 }, dur = 6, heal = 0.2, src = "k_wall" } },
                    },
                    new ChampionSkill
                    {
                        id = "k_bulwark", name = "Bollwerk", icon = "crown", cooldown = 55, mana = 60, target = "self", castRange = 0, radius = 3, unlockLevel = 6,
                        description = "Ultimate: 6 s maximale Rüstung, alle Bodengegner im Umkreis von 3 Feldern werden 4 s gefesselt.",
                        effects = new List<SkillEffect> { new SkillEffect { kind = "self", add = new Dictionary<string, double> { ["armor"] = 0.8 }, dur = 6, src = "k_bulwark" }, new SkillEffect { kind = "taunt", radius = 3, dur = 4 } },
                    },
                },
                tree = campaignTree("k", new CampaignTreeOptions { s1 = new Dictionary<string, double> { ["armor"] = 0.015 }, s1n = "Plattenrüstung", s1d = "+1,5 % Rüstung", s2 = new Dictionary<string, double> { ["damage"] = 4 }, s2n = "Scharfe Klinge", s2d = "+4 Schaden", s3 = new Dictionary<string, double> { ["cleave"] = 0.12, ["abilityPower"] = 0.06 }, s3n = "Klingentanz", s3d = "+12 % Flächenschlag, +6 % Fähigkeitsstärke", m1 = new Dictionary<string, double> { ["maxHp"] = 300, ["hpRegen"] = 4 }, m1n = "Unbezwingbar", m1d = "+300 LP, +4 LP/s" }),
            },
            ["varo"] = new ChampionDefinition
            {
                id = "varo", name = "Varo", title = "Aschenjäger", role = "Fernkämpferin", model = "hunter", color = Heroes.HEROES["hunter"].color, accent = Heroes.HEROES["hunter"].accent,
                description = "Flammenpfeile, Glutfallen und ein Feuerregen: Varo setzt ganze Wellen in Brand.",
                damageType = "fire", melee = false, campaignHero = "hunter", unlockWave = 10,
                @base = ext(BASE, new Dictionary<string, double> { ["maxHp"] = 430, ["damage"] = 17, ["attackSpeed"] = 1.2, ["range"] = 4.2, ["critChance"] = 0.06, ["moveSpeed"] = 2.8 }),
                perLevel = new Dictionary<string, double> { ["maxHp"] = 30, ["damage"] = 2.2, ["attackSpeed"] = 0.015 },
                skills = new ChampionSkill[]
                {
                    new ChampionSkill
                    {
                        id = "o_arrow", name = "Brandpfeil", icon = "advance", cooldown = 7, mana = 20, target = "point", castRange = 7, radius = 0.5, unlockLevel = 1,
                        description = "Ein Pfeil durchbohrt eine Linie (40 + 120 % Schaden) und setzt Gegner 3 s in Brand.",
                        effects = new List<SkillEffect> { new SkillEffect { kind = "line", length = 7, width = 0.5, @base = 40, scale = 1.2, status = new StatusSpec { id = "burn", dur = 3, power = 12 } } },
                    },
                    new ChampionSkill
                    {
                        id = "o_trap", name = "Glutfalle", icon = "range", cooldown = 10, mana = 25, target = "point", castRange = 6, radius = 1.3, unlockLevel = 2,
                        description = "Explodiert im Ziel (30 + 60 % Schaden) und lässt Gegner 6 s brennen.",
                        effects = new List<SkillEffect> { new SkillEffect { kind = "nova", at = "target", radius = 1.3, @base = 30, scale = 0.6, status = new StatusSpec { id = "burn", dur = 6, power = 10 } } },
                    },
                    new ChampionSkill
                    {
                        id = "o_fever", name = "Jagdfieber", icon = "star", cooldown = 18, mana = 30, target = "self", castRange = 0, radius = 0, unlockLevel = 4,
                        description = "8 s +40 % Angriffstempo und +15 % kritische Trefferchance.",
                        effects = new List<SkillEffect> { new SkillEffect { kind = "self", pctStats = new Dictionary<string, double> { ["attackSpeed"] = 0.4 }, add = new Dictionary<string, double> { ["critChance"] = 0.15 }, dur = 8, src = "o_fever" } },
                    },
                    new ChampionSkill
                    {
                        id = "o_rain", name = "Feuerregen", icon = "crown", cooldown = 45, mana = 60, target = "point", castRange = 8, radius = 2.5, unlockLevel = 6,
                        description = "Ultimate: 8 Feuerbrocken auf Gegner im Gebiet (je 60 + 80 % Schaden, Brand).",
                        effects = new List<SkillEffect> { new SkillEffect { kind = "strikes", at = "target", radius = 2.5, count = 8, @base = 60, scale = 0.8, status = new StatusSpec { id = "burn", dur = 4, power = 14 } } },
                    },
                },
                tree = campaignTree("o", new CampaignTreeOptions { s1 = new Dictionary<string, double> { ["damage"] = 2.5 }, s1n = "Glühende Spitzen", s1d = "+2,5 Schaden", s2 = new Dictionary<string, double> { ["attackSpeed"] = 0.08 }, s2n = "Schnellschuss", s2d = "+0,08 Angriffe/s", s3 = new Dictionary<string, double> { ["range"] = 0.3, ["critChance"] = 0.04 }, s3n = "Adlerauge", s3d = "+0,3 Reichweite, +4 % Krit", m1 = new Dictionary<string, double> { ["damage"] = 12, ["critDamage"] = 0.4 }, m1n = "Meisterschütze", m1d = "+12 Schaden, +40 % kritischer Schaden" }),
            },
            ["nyx"] = new ChampionDefinition
            {
                id = "nyx", name = "Nyx", title = "Risswandler", role = "Kämpfer", model = "riftwalker", color = Heroes.HEROES["riftwalker"].color, accent = Heroes.HEROES["riftwalker"].accent,
                description = "Assassine zwischen den Welten: springt durch Risse, markiert Gegner und richtet die Gefährlichsten hin.",
                damageType = "arcane", melee = true, campaignHero = "riftwalker", unlockWave = 25,
                @base = ext(BASE, new Dictionary<string, double> { ["maxHp"] = 520, ["damage"] = 26, ["attackSpeed"] = 1.45, ["range"] = 1.05, ["critChance"] = 0.12, ["critDamage"] = 1.9, ["moveSpeed"] = 3.2 }),
                perLevel = new Dictionary<string, double> { ["maxHp"] = 40, ["damage"] = 3.2, ["critChance"] = 0.004 },
                skills = new ChampionSkill[]
                {
                    new ChampionSkill
                    {
                        id = "n_step", name = "Rissschritt", icon = "advance", cooldown = 7, mana = 20, target = "point", castRange = 6, radius = 1, unlockLevel = 1,
                        description = "Teleportiert zum Ziel: 70 + 160 % Schaden im Umkreis von 1 Feld, betäubt 0,6 s.",
                        effects = new List<SkillEffect> { new SkillEffect { kind = "blink" }, new SkillEffect { kind = "nova", at = "target", radius = 1, @base = 70, scale = 1.6, status = new StatusSpec { id = "stun", dur = 0.6 } } },
                    },
                    new ChampionSkill
                    {
                        id = "n_mark", name = "Leerenmal", icon = "range", cooldown = 11, mana = 25, target = "point", castRange = 6, radius = 2, unlockLevel = 2,
                        description = "Gegner im Umkreis von 2 Feldern erleiden 5 s +30 % Schaden.",
                        effects = new List<SkillEffect> { new SkillEffect { kind = "statusAll", at = "target", radius = 2, status = new StatusSpec { id = "voidMark", dur = 5, power = 0.3 } } },
                    },
                    new ChampionSkill
                    {
                        id = "n_veil", name = "Schattenschleier", icon = "star", cooldown = 16, mana = 30, target = "self", castRange = 0, radius = 0, unlockLevel = 4,
                        description = "6 s +30 % kritische Trefferchance, +1 Tempo, +20 % Rüstung.",
                        effects = new List<SkillEffect> { new SkillEffect { kind = "self", add = new Dictionary<string, double> { ["critChance"] = 0.3, ["moveSpeed"] = 1, ["armor"] = 0.2 }, dur = 6, src = "n_veil" } },
                    },
                    new ChampionSkill
                    {
                        id = "n_exec", name = "Hinrichtung", icon = "crown", cooldown = 50, mana = 60, target = "self", castRange = 0, radius = 0, unlockLevel = 6,
                        description = "Ultimate: die 5 Gegner nächst am Kern verlieren 30 % ihrer max. LP (Bosse 6 %).",
                        effects = new List<SkillEffect> { new SkillEffect { kind = "execute", count = 5, pct = 0.3, bossPct = 0.06 } },
                    },
                },
                tree = campaignTree("n", new CampaignTreeOptions { s1 = new Dictionary<string, double> { ["damage"] = 3.5 }, s1n = "Leerenklinge", s1d = "+3,5 Schaden", s2 = new Dictionary<string, double> { ["critChance"] = 0.05 }, s2n = "Schwachpunkt", s2d = "+5 % Krit", s3 = new Dictionary<string, double> { ["attackSpeed"] = 0.1, ["moveSpeed"] = 0.15 }, s3n = "Flinke Hände", s3d = "+0,1 Angriffe/s, +0,15 Tempo", m1 = new Dictionary<string, double> { ["critDamage"] = 0.6, ["damage"] = 10 }, m1n = "Todesbote", m1d = "+60 % kritischer Schaden, +10 Schaden" }),
            },
            ["liora"] = new ChampionDefinition
            {
                id = "liora", name = "Liora", title = "Runenbardin", role = "Unterstützer", model = "bard", color = Heroes.HEROES["bard"].color, accent = Heroes.HEROES["bard"].accent,
                description = "Ihre Lieder treiben Türme an: Angriffstempo für ganze Turmgruppen und eine Hymne, die die gesamte Verteidigung beschleunigt.",
                damageType = "sonic", melee = false, campaignHero = "bard", unlockWave = 1, aura = new ChampionAura { dmg = 0.04, speed = 0.1 },
                @base = ext(BASE, new Dictionary<string, double> { ["maxHp"] = 380, ["damage"] = 9, ["attackSpeed"] = 1.0, ["range"] = 3.2, ["supportRadius"] = 3, ["maxMana"] = 130, ["manaRegen"] = 5 }),
                perLevel = new Dictionary<string, double> { ["maxHp"] = 26, ["damage"] = 1.1, ["maxMana"] = 8, ["supportPower"] = 0.03 },
                skills = new ChampionSkill[]
                {
                    new ChampionSkill
                    {
                        id = "li_insp", name = "Inspiration", icon = "up", cooldown = 10, mana = 25, target = "self", castRange = 0, radius = 3, unlockLevel = 1,
                        description = "Türme im Umkreis von 3 Feldern: 6 s +30 % Schaden.",
                        effects = new List<SkillEffect> { new SkillEffect { kind = "towers", at = "self", radius = 3, dmg = 0.3, dur = 6, src = "li_insp" } },
                    },
                    new ChampionSkill
                    {
                        id = "li_reso", name = "Lied der Resonanz", icon = "range", cooldown = 16, mana = 35, target = "self", castRange = 0, radius = 4.5, unlockLevel = 2,
                        description = "Türme im Umkreis von 4,5 Feldern: 8 s +35 % Angriffstempo.",
                        effects = new List<SkillEffect> { new SkillEffect { kind = "towers", at = "self", radius = 4.5, speed = 0.35, dur = 8, src = "li_reso" } },
                    },
                    new ChampionSkill
                    {
                        id = "li_lull", name = "Schlaflied", icon = "bolt", cooldown = 14, mana = 30, target = "point", castRange = 6, radius = 2, unlockLevel = 4,
                        description = "Gegner im Umkreis von 2 Feldern werden stark verlangsamt, Liora heilt 20 % ihrer LP.",
                        effects = new List<SkillEffect> { new SkillEffect { kind = "statusAll", at = "target", radius = 2, status = new StatusSpec { id = "chill", dur = 3, power = 0.2, stacks = 3 } }, new SkillEffect { kind = "self", dur = 0, heal = 0.2, src = "li_lull" } },
                    },
                    new ChampionSkill
                    {
                        id = "li_hymn", name = "Hymne der Welten", icon = "crown", cooldown = 60, mana = 70, target = "self", castRange = 0, radius = 0, unlockLevel = 6,
                        description = "Ultimate: alle Türme 8 s +50 % Angriffstempo.",
                        effects = new List<SkillEffect> { new SkillEffect { kind = "allTowers", speed = 0.5, dur = 8, src = "li_hymn" } },
                    },
                },
                tree = campaignTree("li", new CampaignTreeOptions { s1 = new Dictionary<string, double> { ["supportPower"] = 0.04 }, s1n = "Kraftvolle Stimme", s1d = "+4 % Unterstützungsstärke", s2 = new Dictionary<string, double> { ["supportRadius"] = 0.4 }, s2n = "Weiter Klang", s2d = "+0,4 Auraradius", s3 = new Dictionary<string, double> { ["supportPower"] = 0.06, ["cdr"] = 0.03 }, s3n = "Chorleiterin", s3d = "+6 % Unterstützungsstärke, +3 % Abklingzeitreduktion", m1 = new Dictionary<string, double> { ["supportPower"] = 0.25 }, m1n = "Meisterlied", m1d = "+25 % Unterstützungsstärke" }),
            },
            ["thorgrim"] = new ChampionDefinition
            {
                id = "thorgrim", name = "Thorgrim", title = "Schmiedemeister", role = "Unterstützer", model = "smith", color = Heroes.HEROES["smith"].color, accent = Heroes.HEROES["smith"].accent,
                description = "Repariert und verbessert Türme direkt im Feld und haut mit dem Hammer Gegner um. Hält einen Gegner fest.",
                damageType = "physical", melee = true, blockCount = 1, campaignHero = "smith", unlockWave = 1, aura = new ChampionAura { dmg = 0.05, speed = 0.03 },
                @base = ext(BASE, new Dictionary<string, double> { ["maxHp"] = 640, ["hpRegen"] = 2, ["damage"] = 18, ["attackSpeed"] = 0.95, ["range"] = 1.05, ["armor"] = 0.15, ["supportRadius"] = 2.6, ["moveSpeed"] = 2.5 }),
                perLevel = new Dictionary<string, double> { ["maxHp"] = 55, ["damage"] = 2.4, ["armor"] = 0.006, ["supportPower"] = 0.02 },
                skills = new ChampionSkill[]
                {
                    new ChampionSkill
                    {
                        id = "t_forge", name = "Feldschmiede", icon = "up", cooldown = 14, mana = 25, target = "tower", castRange = 5, radius = 0.6, unlockLevel = 1,
                        description = "Ein Turm erhält 20 s +35 % Schaden und +0,5 Reichweite.",
                        effects = new List<SkillEffect> { new SkillEffect { kind = "towers", at = "target", radius = 0.7, dmg = 0.35, range = 0.5, dur = 20, src = "t_forge" } },
                    },
                    new ChampionSkill
                    {
                        id = "t_repair", name = "Reparaturwelle", icon = "range", cooldown = 16, mana = 30, target = "self", castRange = 0, radius = 5, unlockLevel = 2,
                        description = "Beseitigt Betäubung und Störung aller Türme im Umkreis von 5 Feldern, 6 s +15 % Tempo.",
                        effects = new List<SkillEffect> { new SkillEffect { kind = "towers", at = "self", radius = 5, speed = 0.15, dur = 6, repair = true, src = "t_repair" } },
                    },
                    new ChampionSkill
                    {
                        id = "t_hammer", name = "Hammerschlag", icon = "sword", cooldown = 9, mana = 20, target = "self", castRange = 0, radius = 1.4, unlockLevel = 4,
                        description = "Erdbeben im Umkreis von 1,4 Feldern (60 + 140 % Schaden), betäubt 1 s.",
                        effects = new List<SkillEffect> { new SkillEffect { kind = "nova", at = "self", radius = 1.4, @base = 60, scale = 1.4, status = new StatusSpec { id = "stun", dur = 1 } } },
                    },
                    new ChampionSkill
                    {
                        id = "t_master", name = "Meisterwerk", icon = "crown", cooldown = 60, mana = 70, target = "self", castRange = 0, radius = 4, unlockLevel = 6,
                        description = "Ultimate: Türme im Umkreis von 4 Feldern 15 s +40 % Schaden, +0,5 Reichweite, +10 % Krit.",
                        effects = new List<SkillEffect> { new SkillEffect { kind = "towers", at = "self", radius = 4, dmg = 0.4, range = 0.5, crit = 0.1, dur = 15, src = "t_master" } },
                    },
                },
                tree = campaignTree("t", new CampaignTreeOptions { s1 = new Dictionary<string, double> { ["supportPower"] = 0.04, ["damage"] = 1 }, s1n = "Fleiß", s1d = "+4 % Unterstützungsstärke, +1 Schaden", s2 = new Dictionary<string, double> { ["armor"] = 0.03 }, s2n = "Kettenhemd", s2d = "+3 % Rüstung", s3 = new Dictionary<string, double> { ["supportRadius"] = 0.4, ["supportPower"] = 0.05 }, s3n = "Weite Werkstatt", s3d = "+0,4 Auraradius, +5 % Unterstützungsstärke", m1 = new Dictionary<string, double> { ["supportPower"] = 0.2, ["damage"] = 10 }, m1n = "Großmeister", m1d = "+20 % Unterstützungsstärke, +10 Schaden" }),
            },
            ["sena"] = new ChampionDefinition
            {
                id = "sena", name = "Sena", title = "Wettermacherin", role = "Fernkämpferin", model = "weather", color = Heroes.HEROES["weather"].color, accent = Heroes.HEROES["weather"].accent,
                description = "Lässt es regnen, damit Blitze doppelt treffen, friert Gruppen ein und ruft ein Gewitter herab.",
                damageType = "lightning", melee = false, campaignHero = "weather", unlockWave = 15,
                @base = ext(BASE, new Dictionary<string, double> { ["maxHp"] = 400, ["damage"] = 14, ["attackSpeed"] = 0.95, ["range"] = 3.8, ["maxMana"] = 140, ["manaRegen"] = 5.5, ["abilityPower"] = 1.1 }),
                perLevel = new Dictionary<string, double> { ["maxHp"] = 28, ["damage"] = 1.8, ["maxMana"] = 8, ["abilityPower"] = 0.02 },
                skills = new ChampionSkill[]
                {
                    new ChampionSkill
                    {
                        id = "s_rain", name = "Regenguss", icon = "bolt", cooldown = 15, mana = 25, target = "self", castRange = 0, radius = 0, unlockLevel = 1,
                        description = "Alle Gegner werden 6 s nass (Blitzschaden +50 %).",
                        effects = new List<SkillEffect> { new SkillEffect { kind = "statusAll", radius = 0, status = new StatusSpec { id = "wet", dur = 6 } } },
                    },
                    new ChampionSkill
                    {
                        id = "s_frost", name = "Frosthauch", icon = "range", cooldown = 10, mana = 30, target = "self", castRange = 0, radius = 4, unlockLevel = 2,
                        description = "Gegner im Umkreis von 4 Feldern: 20 + 60 % Schaden, stark unterkühlt.",
                        effects = new List<SkillEffect> { new SkillEffect { kind = "nova", at = "self", radius = 4, @base = 20, scale = 0.6, status = new StatusSpec { id = "chill", dur = 3, power = 0.25, stacks = 3 }, air = true } },
                    },
                    new ChampionSkill
                    {
                        id = "s_bolt", name = "Blitzschlag", icon = "advance", cooldown = 8, mana = 25, target = "point", castRange = 7, radius = 1.5, unlockLevel = 4,
                        description = "Drei Blitze im Zielgebiet (je 50 + 100 % Schaden).",
                        effects = new List<SkillEffect> { new SkillEffect { kind = "strikes", at = "target", radius = 1.5, count = 3, @base = 50, scale = 1 } },
                    },
                    new ChampionSkill
                    {
                        id = "s_storm", name = "Gewittersturm", icon = "crown", cooldown = 55, mana = 70, target = "self", castRange = 0, radius = 9, unlockLevel = 6,
                        description = "Ultimate: 12 Blitzeinschläge (je 70 + 80 % Schaden) auf Gegner im weiten Umkreis, alle werden nass.",
                        effects = new List<SkillEffect> { new SkillEffect { kind = "statusAll", radius = 9, status = new StatusSpec { id = "wet", dur = 4 } }, new SkillEffect { kind = "strikes", at = "self", radius = 9, count = 12, @base = 70, scale = 0.8 } },
                    },
                },
                tree = campaignTree("s", new CampaignTreeOptions { s1 = new Dictionary<string, double> { ["abilityPower"] = 0.05 }, s1n = "Sturmkraft", s1d = "+5 % Fähigkeitsstärke", s2 = new Dictionary<string, double> { ["range"] = 0.3 }, s2n = "Sturmauge", s2d = "+0,3 Reichweite", s3 = new Dictionary<string, double> { ["cdr"] = 0.05, ["manaRegen"] = 1 }, s3n = "Monsun", s3d = "+5 % Abklingzeitreduktion, +1 Mana/s", m1 = new Dictionary<string, double> { ["abilityPower"] = 0.3 }, m1n = "Blitzableiterin", m1d = "+30 % Fähigkeitsstärke" }),
            },
            ["oran"] = new ChampionDefinition
            {
                id = "oran", name = "Oran", title = "Leylinien-Mystiker", role = "Unterstützer", model = "mystic", color = Heroes.HEROES["mystic"].color, accent = Heroes.HEROES["mystic"].accent,
                description = "Setzt Ley-Knoten, die Türme dauerhaft stärken, feuert arkane Ströme und webt ein Netz über alle Türme.",
                damageType = "arcane", melee = false, campaignHero = "mystic", unlockWave = 30, aura = new ChampionAura { dmg = 0.08, speed = 0 },
                @base = ext(BASE, new Dictionary<string, double> { ["maxHp"] = 380, ["damage"] = 12, ["attackSpeed"] = 0.9, ["range"] = 3.4, ["supportRadius"] = 2.6, ["maxMana"] = 140, ["manaRegen"] = 5 }),
                perLevel = new Dictionary<string, double> { ["maxHp"] = 26, ["damage"] = 1.5, ["maxMana"] = 8, ["supportPower"] = 0.03 },
                skills = new ChampionSkill[]
                {
                    new ChampionSkill
                    {
                        id = "or_node", name = "Ley-Knoten", icon = "up", cooldown = 20, mana = 30, target = "point", castRange = 6, radius = 2, unlockLevel = 1,
                        description = "Türme im Umkreis von 2 Feldern um das Ziel: 30 s +15 % Schaden.",
                        effects = new List<SkillEffect> { new SkillEffect { kind = "towers", at = "target", radius = 2, dmg = 0.15, dur = 30, src = "or_node" } },
                    },
                    new ChampionSkill
                    {
                        id = "or_beam", name = "Leystrom", icon = "advance", cooldown = 9, mana = 30, target = "point", castRange = 8, radius = 0.5, unlockLevel = 2,
                        description = "Arkaner Strahl: 120 + 120 % Schaden auf einer Linie, markiert 4 s.",
                        effects = new List<SkillEffect> { new SkillEffect { kind = "line", length = 8, width = 0.5, @base = 120, scale = 1.2, status = new StatusSpec { id = "mark", dur = 4, power = 0.2 } } },
                    },
                    new ChampionSkill
                    {
                        id = "or_flow", name = "Runenfluss", icon = "bolt", cooldown = 20, mana = 10, target = "self", castRange = 0, radius = 0, unlockLevel = 4,
                        description = "8 s +6 Mana/s und +20 % Abklingzeitreduktion.",
                        effects = new List<SkillEffect> { new SkillEffect { kind = "self", add = new Dictionary<string, double> { ["manaRegen"] = 6, ["cdr"] = 0.2 }, dur = 8, src = "or_flow" } },
                    },
                    new ChampionSkill
                    {
                        id = "or_web", name = "Weltennetz", icon = "crown", cooldown = 60, mana = 70, target = "self", castRange = 0, radius = 0, unlockLevel = 6,
                        description = "Ultimate: alle Türme 10 s +25 % Schaden.",
                        effects = new List<SkillEffect> { new SkillEffect { kind = "allTowers", dmg = 0.25, dur = 10, src = "or_web" } },
                    },
                },
                tree = campaignTree("or", new CampaignTreeOptions { s1 = new Dictionary<string, double> { ["supportPower"] = 0.04 }, s1n = "Starker Strom", s1d = "+4 % Unterstützungsstärke", s2 = new Dictionary<string, double> { ["abilityPower"] = 0.06 }, s2n = "Arkaner Fokus", s2d = "+6 % Fähigkeitsstärke", s3 = new Dictionary<string, double> { ["supportRadius"] = 0.4, ["supportPower"] = 0.05 }, s3n = "Weites Netz", s3d = "+0,4 Auraradius, +5 % Unterstützungsstärke", m1 = new Dictionary<string, double> { ["supportPower"] = 0.22, ["abilityPower"] = 0.1 }, m1n = "Weltenweber", m1d = "+22 % Unterstützung, +10 % Fähigkeitsstärke" }),
            },
            ["mira"] = new ChampionDefinition
            {
                id = "mira", name = "Mira", title = "Prospektorin", role = "Unterstützer", model = "prospector", color = Heroes.HEROES["prospector"].color, accent = Heroes.HEROES["prospector"].accent,
                description = "Findet Gold und Ressourcen, sprengt Gegnergruppen mit Dynamit und hat das Glück auf ihrer Seite.",
                damageType = "physical", melee = false, campaignHero = "prospector", unlockWave = 20,
                @base = ext(BASE, new Dictionary<string, double> { ["maxHp"] = 430, ["damage"] = 13, ["attackSpeed"] = 1.0, ["range"] = 3.4, ["lootBonus"] = 0.15, ["pickupRadius"] = 1.3 }),
                perLevel = new Dictionary<string, double> { ["maxHp"] = 30, ["damage"] = 1.7, ["lootBonus"] = 0.01 },
                skills = new ChampionSkill[]
                {
                    new ChampionSkill
                    {
                        id = "mi_find", name = "Schatzfund", icon = "coins", cooldown = 30, mana = 25, target = "self", castRange = 0, radius = 0, unlockLevel = 1,
                        description = "Findet 30 + 4 × Welle Gold und 3 Metall.",
                        effects = new List<SkillEffect> { new SkillEffect { kind = "gold", @base = 30, perWave = 4, resource = "metal", amount = 3 } },
                    },
                    new ChampionSkill
                    {
                        id = "mi_luck", name = "Glückssträhne", icon = "star", cooldown = 22, mana = 25, target = "self", castRange = 0, radius = 0, unlockLevel = 2,
                        description = "10 s +60 % Beutebonus und +20 % kritische Trefferchance.",
                        effects = new List<SkillEffect> { new SkillEffect { kind = "self", add = new Dictionary<string, double> { ["lootBonus"] = 0.6, ["critChance"] = 0.2 }, dur = 10, src = "mi_luck" } },
                    },
                    new ChampionSkill
                    {
                        id = "mi_tnt", name = "Dynamit", icon = "advance", cooldown = 10, mana = 25, target = "point", castRange = 6, radius = 1.5, unlockLevel = 4,
                        description = "Explosion im Ziel: 70 + 120 % Schaden, betäubt 0,5 s.",
                        effects = new List<SkillEffect> { new SkillEffect { kind = "nova", at = "target", radius = 1.5, @base = 70, scale = 1.2, status = new StatusSpec { id = "stun", dur = 0.5 } } },
                    },
                    new ChampionSkill
                    {
                        id = "mi_lode", name = "Mutterader", icon = "crown", cooldown = 70, mana = 60, target = "self", castRange = 0, radius = 0, unlockLevel = 6,
                        description = "Ultimate: sofort 120 + 8 × Welle Gold und 4 Kristall.",
                        effects = new List<SkillEffect> { new SkillEffect { kind = "gold", @base = 120, perWave = 8, resource = "crystal", amount = 4 } },
                    },
                },
                tree = campaignTree("mi", new CampaignTreeOptions { s1 = new Dictionary<string, double> { ["lootBonus"] = 0.04 }, s1n = "Spürnase", s1d = "+4 % Beutebonus", s2 = new Dictionary<string, double> { ["pickupRadius"] = 0.2, ["damage"] = 1.5 }, s2n = "Weiter Blick", s2d = "+0,2 Aufsammelradius, +1,5 Schaden", s3 = new Dictionary<string, double> { ["lootBonus"] = 0.06, ["critChance"] = 0.03 }, s3n = "Glückspilz", s3d = "+6 % Beutebonus, +3 % Krit", m1 = new Dictionary<string, double> { ["lootBonus"] = 0.3 }, m1n = "Goldregen", m1d = "+30 % Beutebonus" }),
            },
            ["ommar"] = new ChampionDefinition
            {
                id = "ommar", name = "Ommar", title = "Wächter der Stille", role = "Unterstützer", model = "silence", color = Heroes.HEROES["silence"].color, accent = Heroes.HEROES["silence"].accent,
                description = "Bricht Bossfähigkeiten ab, deckt Getarnte auf, fesselt Gegner und lässt die ganze Welle verstummen.",
                damageType = "sonic", melee = true, blockCount = 1, campaignHero = "silence", unlockWave = 40,
                @base = ext(BASE, new Dictionary<string, double> { ["maxHp"] = 600, ["hpRegen"] = 2, ["damage"] = 15, ["attackSpeed"] = 1.0, ["range"] = 1.05, ["armor"] = 0.12, ["moveSpeed"] = 2.6 }),
                perLevel = new Dictionary<string, double> { ["maxHp"] = 48, ["damage"] = 1.8, ["armor"] = 0.006 },
                skills = new ChampionSkill[]
                {
                    new ChampionSkill
                    {
                        id = "om_ban", name = "Schweigebann", icon = "shield", cooldown = 14, mana = 25, target = "self", castRange = 0, radius = 6, unlockLevel = 1,
                        description = "Unterbricht Fähigkeiten im Umkreis von 6 Feldern (Bosse verlieren viel Standfestigkeit), Gegner im Umkreis von 4 Feldern verstummen 3 s.",
                        effects = new List<SkillEffect> { new SkillEffect { kind = "interrupt", radius = 6, power = 200 }, new SkillEffect { kind = "statusAll", radius = 4, status = new StatusSpec { id = "silence", dur = 3 } } },
                    },
                    new ChampionSkill
                    {
                        id = "om_unveil", name = "Enthüllung", icon = "range", cooldown = 12, mana = 20, target = "self", castRange = 0, radius = 5, unlockLevel = 2,
                        description = "Deckt 8 s alle Getarnten und Eingegrabenen im Umkreis von 5 Feldern auf.",
                        effects = new List<SkillEffect> { new SkillEffect { kind = "statusAll", radius = 5, status = new StatusSpec { id = "reveal", dur = 8 } } },
                    },
                    new ChampionSkill
                    {
                        id = "om_bind", name = "Stille Fessel", icon = "hold", cooldown = 10, mana = 25, target = "point", castRange = 6, radius = 1.6, unlockLevel = 4,
                        description = "Fesselt Bodengegner im Umkreis von 1,6 Feldern 2 s.",
                        effects = new List<SkillEffect> { new SkillEffect { kind = "statusAll", at = "target", radius = 1.6, status = new StatusSpec { id = "root", dur = 2 } } },
                    },
                    new ChampionSkill
                    {
                        id = "om_silence", name = "Absolute Stille", icon = "crown", cooldown = 60, mana = 70, target = "self", castRange = 0, radius = 0, unlockLevel = 6,
                        description = "Ultimate: alle Gegner verstummen 6 s, alle Fähigkeiten werden unterbrochen.",
                        effects = new List<SkillEffect> { new SkillEffect { kind = "statusAll", radius = 0, status = new StatusSpec { id = "silence", dur = 6 } }, new SkillEffect { kind = "interrupt", radius = 999, power = 999 } },
                    },
                },
                tree = campaignTree("om", new CampaignTreeOptions { s1 = new Dictionary<string, double> { ["armor"] = 0.012, ["damage"] = 1 }, s1n = "Steinruhe", s1d = "+1,2 % Rüstung, +1 Schaden", s2 = new Dictionary<string, double> { ["cdr"] = 0.04 }, s2n = "Askese", s2d = "+4 % Abklingzeitreduktion", s3 = new Dictionary<string, double> { ["abilityPower"] = 0.08, ["maxHp"] = 40 }, s3n = "Tiefe Stille", s3d = "+8 % Fähigkeitsstärke, +40 LP", m1 = new Dictionary<string, double> { ["cdr"] = 0.12, ["armor"] = 0.06 }, m1n = "Leere", m1d = "+12 % Abklingzeitreduktion, +6 % Rüstung" }),
            },
        };

        public static readonly List<string> CHAMPION_ORDER = new List<string> { "lyra", "brann", "vela", "kaela", "liora", "thorgrim", "varo", "sena", "mira", "nyx", "oran", "ommar" };

        /// <summary>
        /// Freischaltung: Perk (Vela) bzw. Kampagnenheld – frei, wenn der Held in der Kampagne freigeschaltet
        /// wurde ODER die Bestwelle unlockWave erreicht ist.
        /// </summary>
        public static bool championUnlocked(string id, IReadOnlyList<string> perks, double bestWave = 0, IReadOnlyList<string> campaignDone = null)
        {
            if (campaignDone == null) campaignDone = new List<string>();
            ChampionDefinition d;
            if (id == null || !CHAMPIONS.TryGetValue(id, out d) || d == null) return false;
            if (!string.IsNullOrEmpty(d.unlockPerk) && !U.Includes(perks, d.unlockPerk)) return false;
            if (!string.IsNullOrEmpty(d.campaignHero)) return Heroes.isHeroUnlocked(d.campaignHero, campaignDone) || bestWave >= (d.unlockWave ?? 1);
            return true;
        }

        public static string championUnlockText(string id)
        {
            ChampionDefinition d = CHAMPIONS[id];
            if (!string.IsNullOrEmpty(d.unlockPerk)) return "Freischaltung über Bestwelle 15";
            if (!string.IsNullOrEmpty(d.campaignHero))
            {
                string m = Heroes.HEROES[d.campaignHero].unlock;
                return m == "start" ? "Von Beginn an spielbar" : $"Freischaltung ab Bestwelle {U.Fmt(d.unlockWave ?? 1)}";
            }
            return "";
        }

        /// <summary>
        /// 3.3: Endstufen-Knoten für alle Helden (Stufe 5–8, ab Heldenstufe 25/45/65/90). Damit reichen die Ränge des Baums
        /// für alle Punkte bis Heldenstufe 99 (117 Punkte) – es bleibt aber eine Wahl, weil der Baum etwas mehr Ränge hat.
        /// </summary>
        public static List<SkillNode> endgameNodes(string p)
        {
            return new List<SkillNode>
            {
                new SkillNode { id = $"{p}_e_power", name = "Veteranenkraft", tier = 5, maxRank = 10, minLevel = 25, stats = new Dictionary<string, double> { ["damage"] = 1.5, ["abilityPower"] = 0.015 }, description = "+1,5 Schaden und +1,5 % Fähigkeitsstärke je Rang." },
                new SkillNode { id = $"{p}_e_hide", name = "Eisenhaut", tier = 5, maxRank = 10, minLevel = 25, stats = new Dictionary<string, double> { ["maxHp"] = 30, ["armor"] = 0.003 }, description = "+30 LP und +0,3 % Rüstung je Rang." },
                new SkillNode { id = $"{p}_e_mind", name = "Geistesquell", tier = 5, maxRank = 10, minLevel = 25, stats = new Dictionary<string, double> { ["maxMana"] = 6, ["manaRegen"] = 0.25 }, description = "+6 Mana und +0,25 Manaregeneration je Rang." },
                new SkillNode { id = $"{p}_e_blade", name = "Klingenkunst", tier = 6, maxRank = 10, minLevel = 45, requires = new List<SkillNodeRequirement> { new SkillNodeRequirement { id = $"{p}_e_power", rank = 3 } }, stats = new Dictionary<string, double> { ["critChance"] = 0.004, ["critDamage"] = 0.02 }, description = "+0,4 % kritische Chance und +2 % kritischer Schaden je Rang." },
                new SkillNode { id = $"{p}_e_life", name = "Lebensstrom", tier = 6, maxRank = 10, minLevel = 45, requires = new List<SkillNodeRequirement> { new SkillNodeRequirement { id = $"{p}_e_hide", rank = 3 } }, stats = new Dictionary<string, double> { ["hpRegen"] = 0.5, ["lifesteal"] = 0.003 }, description = "+0,5 LP/s und +0,3 % Lebensraub je Rang." },
                new SkillNode { id = $"{p}_e_flow", name = "Zeitfluss", tier = 6, maxRank = 10, minLevel = 45, requires = new List<SkillNodeRequirement> { new SkillNodeRequirement { id = $"{p}_e_mind", rank = 3 } }, stats = new Dictionary<string, double> { ["cdr"] = 0.003, ["attackSpeed"] = 0.01 }, description = "+0,3 % Abklingzeitreduktion und +0,01 Angriffe/s je Rang." },
                new SkillNode { id = $"{p}_e_bond", name = "Turmbund", tier = 7, maxRank = 12, minLevel = 65, requires = new List<SkillNodeRequirement> { new SkillNodeRequirement { id = $"{p}_e_flow", rank = 3 } }, stats = new Dictionary<string, double> { ["supportPower"] = 0.015, ["supportRadius"] = 0.03 }, description = "+1,5 % Unterstützungsstärke und +0,03 Auraradius je Rang." },
                new SkillNode { id = $"{p}_e_heart", name = "Heldenmut", tier = 7, maxRank = 12, minLevel = 65, requires = new List<SkillNodeRequirement> { new SkillNodeRequirement { id = $"{p}_e_blade", rank = 3 } }, stats = new Dictionary<string, double> { ["damage"] = 1, ["maxHp"] = 20, ["abilityPower"] = 0.01 }, description = "+1 Schaden, +20 LP und +1 % Fähigkeitsstärke je Rang." },
                new SkillNode { id = $"{p}_e_legend", name = "Ewige Legende", tier = 8, maxRank = 5, minLevel = 90, requires = new List<SkillNodeRequirement> { new SkillNodeRequirement { id = $"{p}_e_heart", rank = 5 } }, stats = new Dictionary<string, double> { ["damage"] = 3, ["maxHp"] = 60, ["abilityPower"] = 0.03, ["supportPower"] = 0.03 }, description = "Krönung: +3 Schaden, +60 LP, +3 % Fähigkeits- und Unterstützungsstärke je Rang." },
            };
        }

        /// <summary>Grenzen der Heldenwerte (verhindert unkontrollierte Werteexplosionen). TS-Tupel [min, max] → double[2].</summary>
        public static readonly Dictionary<string, double[]> STAT_LIMITS = new Dictionary<string, double[]>
        {
            ["armor"] = new double[] { 0, 0.8 }, ["critChance"] = new double[] { 0, 0.85 }, ["cdr"] = new double[] { 0, 0.5 }, ["lifesteal"] = new double[] { 0, 0.4 }, ["cleave"] = new double[] { 0, 0.8 }, ["attackSpeed"] = new double[] { 0.2, 5 }, ["moveSpeed"] = new double[] { 0.8, 6 },
        };

        public static readonly Dictionary<string, string> STAT_NAMES = new Dictionary<string, string>
        {
            ["maxHp"] = "Lebenspunkte", ["hpRegen"] = "LP-Regeneration", ["maxMana"] = "Mana", ["manaRegen"] = "Manaregeneration", ["damage"] = "Schaden",
            ["attackSpeed"] = "Angriffe/s", ["range"] = "Reichweite", ["moveSpeed"] = "Bewegungstempo", ["armor"] = "Rüstung", ["critChance"] = "Kritische Chance",
            ["critDamage"] = "Kritischer Schaden", ["cdr"] = "Abklingzeitreduktion", ["abilityPower"] = "Fähigkeitsstärke", ["supportPower"] = "Unterstützungsstärke",
            ["supportRadius"] = "Auraradius", ["lootBonus"] = "Beutebonus", ["pickupRadius"] = "Aufsammelradius", ["lifesteal"] = "Lebensraub", ["cleave"] = "Flächenschlag",
        };

        /// <summary>Prozentuale Darstellung je Attribut.</summary>
        public static readonly HashSet<string> STAT_PCT = new HashSet<string> { "armor", "critChance", "cdr", "lootBonus", "lifesteal", "cleave" };
        public static readonly HashSet<string> STAT_MULT = new HashSet<string> { "abilityPower", "supportPower", "critDamage" };

        static Champions()
        {
            // TS: for (const c of Object.values(CHAMPIONS)) c.tree.push(...endgameNodes(c.id));
            // (Läuft nach allen Feldinitialisierern dieser Klasse.)
            foreach (ChampionDefinition c in CHAMPIONS.Values) c.tree.AddRange(endgameNodes(c.id));
        }
    }
}
