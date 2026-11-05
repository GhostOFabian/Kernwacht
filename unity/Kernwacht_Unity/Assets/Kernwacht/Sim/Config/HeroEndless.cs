// Übersetzung von config/heroEndless.ts.
// Konfiguration des Helden-Endlosmodus. Alle Zahlen hier sind bewusst zentral gesammelt,
// damit Wellentakt, Erfahrungskurve, Beute und Fortschritt ohne Codeänderung angepasst werden können.

using System;
using System.Collections.Generic;

namespace Kernwacht.Sim
{
    /// <summary>TS: inline-Typ <c>HERO_ENDLESS.towerBuffCap</c>.</summary>
    public class TowerBuffCapDefinition
    {
        public double dmg;
        public double speed;
        public double range;
        public double crit;
    }

    /// <summary>TS: inline-Objekt <c>HERO_ENDLESS</c> (mit Methode <see cref="waveResources"/>).</summary>
    public class HeroEndlessConstants
    {
        /// <summary>Vorbereitungszeit vor der ersten Welle (s).</summary>
        public double prepTime;
        /// <summary>Übergangszeit zwischen zwei Wellen (s).</summary>
        public double transitionTime;
        /// <summary>Startgold-Faktor gegenüber dem normalen Endlosmodus.</summary>
        public double startGoldMul;
        /// <summary>Erfahrungskurve: benötigte EP für Stufe L → L+1 = xpBase · L^xpExp + xpLinear · L</summary>
        public double xpBase;
        public double xpExp;
        public double xpLinear;
        /// <summary>Skillpunkte je Stufenaufstieg, zusätzlich ein Bonuspunkt alle bonusPointEvery Stufen.</summary>
        public double pointsPerLevel;
        public double bonusPointEvery;
        /// <summary>Wiederbelebung: Basis + je Stufe (s).</summary>
        public double respawnBase;
        public double respawnPerLevel;
        /// <summary>Gegnerschaden am Helden wächst mit der Welle.</summary>
        public double enemyDamagePerWave;
        public double contactRange;
        /// <summary>Lebensdauer von Beute am Boden (s).</summary>
        public Dictionary<string, double> pickupLife = new Dictionary<string, double>();
        /// <summary>Laden: Interaktionsradius (Felder) und Auffrischung des Bestands alle n Wellen.</summary>
        public double shopRange;
        public double stockEvery;
        /// <summary>Inventarplätze (Ausrüstung) und maximale Trankstapel.</summary>
        public double inventorySlots;
        public double potionStacks;
        public double potionGlobalCd;
        /// <summary>Fragmente, die eine freigeschaltete Itemfamilie überzählig umwandelt, ergeben je Fragment Essenz.</summary>
        public double fragmentToEssence;
        /// <summary>Mini-Boss alle n Wellen ab miniBossFrom (Versatz, damit er nie mit Bosswellen zusammenfällt): 7, 12, 17, …</summary>
        public double miniBossEvery;
        public double miniBossFrom;
        public double miniBossHpMul;
        /// <summary>Abbau von Hindernissen: Dauer ≈ mittlere Wellendauer (s), Goldausbeute.</summary>
        public double mineTimeDefault;
        public double mineTimeMin;
        public double mineTimeMax;
        public double mineGoldBase;
        public double mineGoldPerWave;
        /// <summary>Spezialfelder: Kristalladern je Karte (Goldgräber fördert dort zusätzlich Kristall). TS-Tupel → double[2].</summary>
        public double[] depositCount;
        /// <summary>Turmverstärkungen: Obergrenzen der Summe aller Quellen.</summary>
        public TowerBuffCapDefinition towerBuffCap;
        /// <summary>Startausrüstung: Grundanzahl mitnehmbarer Sammlungsitems (Perks erhöhen).</summary>
        public double baseCarry;

        /// <summary>Ressourcen als Wellenbelohnung.</summary>
        public Dictionary<string, double> waveResources(double wave)
        {
            return new Dictionary<string, double>
            {
                ["metal"] = 2 + Math.Floor(wave / 3),
                ["crystal"] = 1 + Math.Floor(wave / 5),
                ["essence"] = wave % 5 == 0 ? 2 + Math.Floor(wave / 10) : 0,
                ["relic"] = wave % 10 == 0 ? 1 : 0,
            };
        }
    }

    /// <summary>TS: inline-Objekt <c>LOOT</c> (mit Methoden <see cref="resourceOf"/> und <see cref="resourceAmount"/>).</summary>
    public class LootConstants
    {
        /// <summary>Erfahrung je Bedrohungspunkt des Gegners, wächst mit der Welle.</summary>
        public double xpPerThreat;
        public double xpWaveGrowth;
        public double eliteXpMul;
        public double miniBossXpMul;
        public double bossXpMul;
        /// <summary>Dropchancen (vor Glücksbonus). Schlüssel: 'normal' | 'elite' | 'miniBoss' | 'boss'.</summary>
        public double resourceChance;
        public double potionChance;
        public Dictionary<string, double> fragmentChance = new Dictionary<string, double>();
        public Dictionary<string, double> itemChance = new Dictionary<string, double>();

        /// <summary>Ressource je Gegnertyp (Standard: Metall).</summary>
        public string resourceOf(string enemy)
        {
            switch (enemy)
            {
                case "flyer": case "griffin": case "phase": case "shaman": case "stalker": return "crystal";
                case "broodmother": case "saboteur": return "essence";
                default: return "metal";
            }
        }

        public double resourceAmount(double wave)
        {
            return 1 + Math.Floor(wave / 8);
        }
    }

    public class ResourceDefinition
    {
        public string id;   // ResourceId
        public string name;
        public double color;
        public string icon;
        public string description;
    }

    public class WaveProfile
    {
        public string id;
        public string name;
        /// <summary>Gewichtsfaktoren für Gegnertypen. Partial&lt;Record&lt;EnemyId, number&gt;&gt;</summary>
        public Dictionary<string, double> weights = new Dictionary<string, double>();
        public double budgetMul;
        public double capMul;
        public double extraTypes;
        public double speedMul;
        public double extraElites;
    }

    public class PerkDefinition
    {
        public string id;
        public string name;
        public string description;
        /// <summary>Freischaltung über die höchste erreichte Welle.</summary>
        public double wave;
        /// <summary>Optionale Voraussetzung (anderer Perk). null = keine.</summary>
        public string requires;
        public string icon;
    }

    /// <summary>TS: inline-Typ <c>MilestoneDefinition.reward</c>.</summary>
    public class MilestoneReward
    {
        public List<string> items;                     // ItemId[] oder null
        public Dictionary<string, double> fragments;   // Partial<Record<ItemId, number>> oder null
        public double? paragon;
        public double? startGold;
    }

    public class MilestoneDefinition
    {
        public double wave;
        public string name;
        public MilestoneReward reward;
        public string description;
    }

    public static partial class HeroEndless
    {
        public static readonly HeroEndlessConstants HERO_ENDLESS = new HeroEndlessConstants
        {
            /** Vorbereitungszeit vor der ersten Welle (s). */
            prepTime = 15,
            /** Übergangszeit zwischen zwei Wellen (s). */
            transitionTime = 8,
            /** Startgold-Faktor gegenüber dem normalen Endlosmodus. */
            startGoldMul = 1,
            /** Erfahrungskurve: benötigte EP für Stufe L → L+1 = xpBase · L^xpExp + xpLinear · L */
            xpBase = 30,
            xpExp = 1.45,
            xpLinear = 20,
            /** Skillpunkte je Stufenaufstieg, zusätzlich ein Bonuspunkt alle bonusPointEvery Stufen. */
            pointsPerLevel = 1,
            bonusPointEvery = 5,
            /** Wiederbelebung: Basis + je Stufe (s). */
            respawnBase = 6,
            respawnPerLevel = 0.5,
            /** Gegnerschaden am Helden wächst mit der Welle. */
            enemyDamagePerWave = 0.05,
            contactRange = 0.65,
            /** Lebensdauer von Beute am Boden (s). */
            pickupLife = new Dictionary<string, double> { ["xp"] = 40, ["resource"] = 45, ["fragment"] = 60, ["item"] = 90, ["potion"] = 60 },
            /** Laden: Interaktionsradius (Felder) und Auffrischung des Bestands alle n Wellen. */
            shopRange = 2.2,
            stockEvery = 5,
            /** Inventarplätze (Ausrüstung) und maximale Trankstapel. */
            inventorySlots = 12,
            potionStacks = 6,
            potionGlobalCd = 0.8,
            /** Fragmente, die eine freigeschaltete Itemfamilie überzählig umwandelt, ergeben je Fragment Essenz. */
            fragmentToEssence = 2,
            /** Mini-Boss alle n Wellen ab miniBossFrom (Versatz, damit er nie mit Bosswellen zusammenfällt): 7, 12, 17, … */
            miniBossEvery = 5,
            miniBossFrom = 7,
            miniBossHpMul = 4,
            /** Abbau von Hindernissen: Dauer ≈ mittlere Wellendauer (s), Goldausbeute. */
            mineTimeDefault = 35,
            mineTimeMin = 25,
            mineTimeMax = 60,
            mineGoldBase = 20,
            mineGoldPerWave = 4,
            /** Spezialfelder: Kristalladern je Karte (Goldgräber fördert dort zusätzlich Kristall). */
            depositCount = new double[] { 2, 4 },
            /** Turmverstärkungen: Obergrenzen der Summe aller Quellen. */
            towerBuffCap = new TowerBuffCapDefinition { dmg = 1.2, speed = 0.8, range = 1.5, crit = 0.4 },
            /** Startausrüstung: Grundanzahl mitnehmbarer Sammlungsitems (Perks erhöhen). */
            baseCarry = 0,
            // waveResources(wave): siehe Methode HeroEndlessConstants.waveResources
        };

        public static double xpToNext(double level)
        {
            double @base = HERO_ENDLESS.xpBase * Math.Pow(level, HERO_ENDLESS.xpExp) + HERO_ENDLESS.xpLinear * level;
            // 3.0: ab Stufe 25 flacher – Stufe 99 nach rund 360 000 statt 1 Mio. EP
            return U.Round(level <= 25 ? @base : @base * Math.Pow(25 / level, 1.2));
        }

        // ------------------------------------------------------------ Beute

        public static readonly LootConstants LOOT = new LootConstants
        {
            /** Erfahrung je Bedrohungspunkt des Gegners, wächst mit der Welle. */
            xpPerThreat = 4,
            xpWaveGrowth = 0.06,
            eliteXpMul = 3,
            miniBossXpMul = 10,
            bossXpMul = 25,
            /** Dropchancen (vor Glücksbonus). */
            resourceChance = 0.16,
            potionChance = 0.025,
            fragmentChance = new Dictionary<string, double> { ["normal"] = 0.025, ["elite"] = 0.25, ["miniBoss"] = 0.6, ["boss"] = 1 },
            itemChance = new Dictionary<string, double> { ["normal"] = 0, ["elite"] = 0.04, ["miniBoss"] = 0.25, ["boss"] = 0.5 },
            // resourceOf(enemy), resourceAmount(wave): siehe Methoden von LootConstants
        };

        public static readonly Dictionary<string, ResourceDefinition> RESOURCES = new Dictionary<string, ResourceDefinition>
        {
            ["metal"] = new ResourceDefinition { id = "metal", name = "Metall", color = 0xb8c0cc, icon = "⛓", description = "Häufig. Für Verstärkungen und Ausrüstung." },
            ["crystal"] = new ResourceDefinition { id = "crystal", name = "Kristall", color = 0x7ae0ff, icon = "◆", description = "Von Fliegern und Magiern. Für bessere Gegenstände." },
            ["essence"] = new ResourceDefinition { id = "essence", name = "Essenz", color = 0xd080ff, icon = "✧", description = "Von Elitegegnern und Bossen. Für Seltenes und Tränke." },
            ["relic"] = new ResourceDefinition { id = "relic", name = "Relikt", color = 0xffd060, icon = "✦", description = "Sehr selten. Für legendäre Herstellung." },
        };
        public static readonly List<string> RESOURCE_ORDER = new List<string> { "metal", "crystal", "essence", "relic" };

        // ------------------------------------------------------------ Wellenprofile

        public static readonly List<WaveProfile> WAVE_PROFILES = new List<WaveProfile>
        {
            new WaveProfile { id = "mixed", name = "Gemischt", weights = new Dictionary<string, double>(), budgetMul = 1, capMul = 1, extraTypes = 1, speedMul = 1, extraElites = 0 },
            new WaveProfile { id = "swarm", name = "Schwarm", weights = new Dictionary<string, double> { ["swarm"] = 4, ["runner"] = 3, ["broodmother"] = 2 }, budgetMul = 1.05, capMul = 1.5, extraTypes = 1, speedMul = 1.05, extraElites = 0 },
            new WaveProfile { id = "armor", name = "Panzerzug", weights = new Dictionary<string, double> { ["brute"] = 4, ["shield"] = 3, ["golem"] = 3, ["siege"] = 3 }, budgetMul = 1.1, capMul = 0.8, extraTypes = 1, speedMul = 0.95, extraElites = 0 },
            new WaveProfile { id = "air", name = "Luftangriff", weights = new Dictionary<string, double> { ["flyer"] = 5, ["griffin"] = 5 }, budgetMul = 0.95, capMul = 1, extraTypes = 1, speedMul = 1, extraElites = 0 },
            new WaveProfile { id = "shadow", name = "Schatten", weights = new Dictionary<string, double> { ["stalker"] = 4, ["phase"] = 3, ["tunneler"] = 3, ["saboteur"] = 2 }, budgetMul = 1, capMul = 1, extraTypes = 1, speedMul = 1.05, extraElites = 0 },
            new WaveProfile { id = "elite", name = "Elitetrupp", weights = new Dictionary<string, double> { ["shaman"] = 3 }, budgetMul = 0.85, capMul = 0.8, extraTypes = 1, speedMul = 1, extraElites = 2 },
            new WaveProfile { id = "rush", name = "Ansturm", weights = new Dictionary<string, double> { ["runner"] = 3, ["scout"] = 2, ["griffin"] = 2 }, budgetMul = 0.9, capMul = 1.2, extraTypes = 1, speedMul = 1.2, extraElites = 0 },
        };

        /// <summary>Deterministische Profilwahl je Welle (keine reine Lineare – Profile wechseln, ab Welle 3).</summary>
        public static WaveProfile profileForWave(double seedHash, double wave)
        {
            if (wave < 3) return WAVE_PROFILES[0];
            double h = U.Ushr(U.BitXor(seedHash, U.Imul(wave, 0x9e3779b1)), 0) % 1000;
            // Elitetrupps erst ab Welle 8
            List<WaveProfile> pool = new List<WaveProfile>();
            foreach (WaveProfile p in WAVE_PROFILES) if (p.id != "elite" || wave >= 8) pool.Add(p);
            List<WaveProfile> pool2 = new List<WaveProfile>();
            foreach (WaveProfile p in pool) if (p.id != "shadow" || wave >= 9) pool2.Add(p);
            pool = pool2;
            return pool[(int)(h % pool.Count)];
        }

        // ------------------------------------------------------------ Dauerhafte Perks & Meilensteine

        public static readonly List<PerkDefinition> PERKS = new List<PerkDefinition>
        {
            new PerkDefinition { id = "magnet1", wave = 3, icon = "🧲", name = "Sammlerhand", description = "+0,3 Aufsammelradius." },
            new PerkDefinition { id = "pouch1", wave = 5, icon = "🧪", name = "Startbeutel", description = "Start mit 2 Heiltränken." },
            new PerkDefinition { id = "supply1", wave = 6, icon = "⛓", name = "Vorrat I", description = "Start mit 6 Metall und 2 Kristall." },
            new PerkDefinition { id = "carry1", wave = 8, icon = "🎒", name = "Rucksack I", description = "Ein Item aus der Sammlung kostenlos mitnehmen." },
            new PerkDefinition { id = "alchemy", wave = 10, icon = "⚗", name = "Alchemie", description = "Fokus- und Turmelixier werden verfügbar (Beute und Laden)." },
            new PerkDefinition { id = "smith", wave = 12, icon = "⚒", name = "Meisterschmied", description = "Kombinationsrezepte für legendäre Items." },
            new PerkDefinition { id = "trader", wave = 14, icon = "🏪", name = "Händlergilde", description = "Der Laden tauscht Relikte und verkauft Fertigkeitsfolianten." },
            new PerkDefinition { id = "vela", wave = 15, icon = "🏹", name = "Neue Heldin: Vela", description = "Die Sturmschützin Vela wird wählbar." },
            new PerkDefinition { id = "supply2", wave = 16, requires = "supply1", icon = "⛓", name = "Vorrat II", description = "Start mit zusätzlich 8 Metall, 4 Kristall und 2 Essenz." },
            new PerkDefinition { id = "carry2", wave = 18, requires = "carry1", icon = "🎒", name = "Rucksack II", description = "Ein weiteres Item mitnehmen." },
            new PerkDefinition { id = "mastery", wave = 20, icon = "★", name = "Meisterschaft", description = "Schaltet die Meisterknoten in allen Fähigkeitenbäumen frei." },
            new PerkDefinition { id = "wisdom", wave = 22, icon = "📖", name = "Weisheit", description = "+10 % Erfahrung." },
            new PerkDefinition { id = "gold", wave = 25, icon = "🪙", name = "Kriegskasse", description = "+80 Startgold." },
            new PerkDefinition { id = "carry3", wave = 30, requires = "carry2", icon = "🎒", name = "Rucksack III", description = "Ein drittes Item mitnehmen." },
            new PerkDefinition { id = "fortune", wave = 35, icon = "🍀", name = "Glückspilz", description = "+25 % Dropchance für Items und Fragmente." },
        };

        /// <summary>Einmalige Belohnungen beim ersten Erreichen einer Welle. Danach alle 25 Wellen ein Folgemeilenstein.</summary>
        public static readonly List<MilestoneDefinition> MILESTONES = new List<MilestoneDefinition>
        {
            new MilestoneDefinition { wave = 5, name = "Erste Bewährung", reward = new MilestoneReward { fragments = new Dictionary<string, double> { ["leather"] = 3 } }, description = "Fragmente für einen Lederwams." },
            new MilestoneDefinition { wave = 10, name = "Wellenbrecher", reward = new MilestoneReward { items = new List<string> { "luckyCharm" } }, description = "Schaltet den Glücksbringer frei." },
            new MilestoneDefinition { wave = 20, name = "Unbeugsam", reward = new MilestoneReward { items = new List<string> { "towerSeal" } }, description = "Schaltet das Turmsiegel frei." },
            new MilestoneDefinition { wave = 30, name = "Veteran", reward = new MilestoneReward { items = new List<string> { "starMantle" } }, description = "Schaltet den Sternenmantel frei." },
            new MilestoneDefinition { wave = 40, name = "Legende", reward = new MilestoneReward { items = new List<string> { "runeCrown" } }, description = "Schaltet die Runenkrone frei." },
            new MilestoneDefinition { wave = 50, name = "Unsterblich", reward = new MilestoneReward { }, description = "Titel „Unsterblich“." },
            new MilestoneDefinition { wave = 100, name = "Hundertschaft", reward = new MilestoneReward { paragon = 2, startGold = 150 }, description = "Dauerhaft +2 Paragonpunkte und +150 Startgold." },
            new MilestoneDefinition { wave = 150, name = "Weltenwächter", reward = new MilestoneReward { paragon = 2, startGold = 150 }, description = "Dauerhaft +2 Paragonpunkte und +150 Startgold." },
            new MilestoneDefinition { wave = 200, name = "Unbezwingbar", reward = new MilestoneReward { paragon = 3, startGold = 200 }, description = "Dauerhaft +3 Paragonpunkte und +200 Startgold." },
            new MilestoneDefinition { wave = 250, name = "Runenmeister", reward = new MilestoneReward { paragon = 3 }, description = "Dauerhaft +3 Paragonpunkte." },
            new MilestoneDefinition { wave = 300, name = "Kernlegende", reward = new MilestoneReward { paragon = 5, startGold = 300 }, description = "Dauerhaft +5 Paragonpunkte und +300 Startgold." },
        };

        /// <summary>3.0: dauerhafte Paragonpunkte aus erreichten Meilensteinen (Bestwelle).</summary>
        public static double milestoneParagon(double bestWave)
        {
            double a = 0;
            foreach (MilestoneDefinition m in MILESTONES) if (m.wave <= bestWave) a = a + (m.reward.paragon ?? 0);
            return a;
        }

        /// <summary>3.0: dauerhaftes Startgold aus erreichten Meilensteinen (Bestwelle).</summary>
        public static double milestoneGold(double bestWave)
        {
            double a = 0;
            foreach (MilestoneDefinition m in MILESTONES) if (m.wave <= bestWave) a = a + (m.reward.startGold ?? 0);
            return a;
        }

        public static bool perkUnlocked(string id, double bestWave)
        {
            PerkDefinition p = PERKS.Find((x) => x.id == id);
            if (p == null) return false;
            if (bestWave < p.wave) return false;
            return !string.IsNullOrEmpty(p.requires) ? perkUnlocked(p.requires, bestWave) : true;
        }

        public static List<string> activePerks(double bestWave)
        {
            List<string> out_ = new List<string>();
            foreach (PerkDefinition p in PERKS) if (perkUnlocked(p.id, bestWave)) out_.Add(p.id);
            return out_;
        }

        public static double carrySlots(IReadOnlyList<string> perks)
        {
            return HERO_ENDLESS.baseCarry + (U.Includes(perks, "carry1") ? 1 : 0) + (U.Includes(perks, "carry2") ? 1 : 0) + (U.Includes(perks, "carry3") ? 1 : 0);
        }

        public static readonly Dictionary<string, string> POTIONS_BY_PERK = new Dictionary<string, string> { ["focus"] = "alchemy", ["towerTonic"] = "alchemy" };
    }
}
