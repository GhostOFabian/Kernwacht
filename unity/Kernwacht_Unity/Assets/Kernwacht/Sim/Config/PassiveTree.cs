// Übersetzung von config/passiveTree.ts.
// Passiver Fähigkeitenbaum des Helden-Endlosmodus (dauerhaft, kontoweit).
// Punkte: 1 je 10 erreichte Bestwellen, höchstens 100 (= Bestwelle 1000).
// Die Summe aller Maximalränge ist genau 100: Wer Welle 1000 erreicht, hat den Baum vollständig.
// Werte je Rang sind bewusst klein gehalten; sie wirken automatisch in jedem Lauf.

using System;
using System.Collections.Generic;

namespace Kernwacht.Sim
{
    /// <summary>TS: inline-Objekt <c>PASSIVE</c>.</summary>
    public class PassiveConstants
    {
        public double wavesPerPoint;
        public double maxPoints;
        /// <summary>Benötigte, im Baum bereits ausgegebene Punkte je Stufe.</summary>
        public double[] tierGate;
    }

    /// <summary>
    /// Wirkungen des passiven Baums und des Paragons. Zusätzlich: Indexer <c>this[string]</c>
    /// (für <c>fx[k as keyof PassiveEffects]</c>), <see cref="KEYS"/> (Deklarationsreihenfolge) und <see cref="Copy"/>.
    /// </summary>
    public class PassiveEffects
    {
        public double heroHpPct;
        public double heroDmgPct;
        public double heroArmor;
        public double heroCdr;
        public double heroCrit;
        public double heroRegen;
        public double manaRegen;
        public double supportPower;
        public double pickupRadius;
        public double xpPct;
        public double towerDmgPct;
        public double towerSpeedPct;
        public double towerRange;
        public double startGold;
        public double killGoldPct;
        public double resourcePct;
        public double lootPct;
        public double coreLives;
        public double respawnPct;
        /// <summary>Türme werden so viele Wellen früher freigeschaltet.</summary>
        public double unlockEarlier;
        /// <summary>Paragon (2.7): Angriffstempo, Fähigkeitsstärke, Bewegungstempo, Lebensraub des Helden.</summary>
        public double heroAtkSpeed;
        public double abilityPct;
        public double moveSpeed;
        public double lifesteal;
        /// <summary>Paragon-Turmbaum (2.9): kritische Chance, Panzerbrechung, Baukostenrabatt, Goldgräber-Ertrag, Turm-Kopfgeld.</summary>
        public double towerCrit;
        public double towerPierce;
        public double towerCostPct;
        public double mineIncomePct;
        public double towerSplashPct;

        public static readonly string[] KEYS =
        {
            "heroHpPct", "heroDmgPct", "heroArmor", "heroCdr", "heroCrit", "heroRegen", "manaRegen", "supportPower", "pickupRadius",
            "xpPct", "towerDmgPct", "towerSpeedPct", "towerRange", "startGold", "killGoldPct", "resourcePct", "lootPct", "coreLives",
            "respawnPct", "unlockEarlier", "heroAtkSpeed", "abilityPct", "moveSpeed", "lifesteal",
            "towerCrit", "towerPierce", "towerCostPct", "mineIncomePct", "towerSplashPct",
        };

        public double this[string key]
        {
            get
            {
                switch (key)
                {
                    case "heroHpPct": return heroHpPct;
                    case "heroDmgPct": return heroDmgPct;
                    case "heroArmor": return heroArmor;
                    case "heroCdr": return heroCdr;
                    case "heroCrit": return heroCrit;
                    case "heroRegen": return heroRegen;
                    case "manaRegen": return manaRegen;
                    case "supportPower": return supportPower;
                    case "pickupRadius": return pickupRadius;
                    case "xpPct": return xpPct;
                    case "towerDmgPct": return towerDmgPct;
                    case "towerSpeedPct": return towerSpeedPct;
                    case "towerRange": return towerRange;
                    case "startGold": return startGold;
                    case "killGoldPct": return killGoldPct;
                    case "resourcePct": return resourcePct;
                    case "lootPct": return lootPct;
                    case "coreLives": return coreLives;
                    case "respawnPct": return respawnPct;
                    case "unlockEarlier": return unlockEarlier;
                    case "heroAtkSpeed": return heroAtkSpeed;
                    case "abilityPct": return abilityPct;
                    case "moveSpeed": return moveSpeed;
                    case "lifesteal": return lifesteal;
                    case "towerCrit": return towerCrit;
                    case "towerPierce": return towerPierce;
                    case "towerCostPct": return towerCostPct;
                    case "mineIncomePct": return mineIncomePct;
                    case "towerSplashPct": return towerSplashPct;
                    default: throw new Exception("PassiveEffects: unbekannter Schlüssel " + key);
                }
            }
            set
            {
                switch (key)
                {
                    case "heroHpPct": heroHpPct = value; break;
                    case "heroDmgPct": heroDmgPct = value; break;
                    case "heroArmor": heroArmor = value; break;
                    case "heroCdr": heroCdr = value; break;
                    case "heroCrit": heroCrit = value; break;
                    case "heroRegen": heroRegen = value; break;
                    case "manaRegen": manaRegen = value; break;
                    case "supportPower": supportPower = value; break;
                    case "pickupRadius": pickupRadius = value; break;
                    case "xpPct": xpPct = value; break;
                    case "towerDmgPct": towerDmgPct = value; break;
                    case "towerSpeedPct": towerSpeedPct = value; break;
                    case "towerRange": towerRange = value; break;
                    case "startGold": startGold = value; break;
                    case "killGoldPct": killGoldPct = value; break;
                    case "resourcePct": resourcePct = value; break;
                    case "lootPct": lootPct = value; break;
                    case "coreLives": coreLives = value; break;
                    case "respawnPct": respawnPct = value; break;
                    case "unlockEarlier": unlockEarlier = value; break;
                    case "heroAtkSpeed": heroAtkSpeed = value; break;
                    case "abilityPct": abilityPct = value; break;
                    case "moveSpeed": moveSpeed = value; break;
                    case "lifesteal": lifesteal = value; break;
                    case "towerCrit": towerCrit = value; break;
                    case "towerPierce": towerPierce = value; break;
                    case "towerCostPct": towerCostPct = value; break;
                    case "mineIncomePct": mineIncomePct = value; break;
                    case "towerSplashPct": towerSplashPct = value; break;
                    default: throw new Exception("PassiveEffects: unbekannter Schlüssel " + key);
                }
            }
        }

        /// <summary>Flache Kopie (TS: <c>{ ...ZERO_PASSIVE }</c>).</summary>
        public PassiveEffects Copy() { return (PassiveEffects)MemberwiseClone(); }
    }

    public class PassiveNode
    {
        public string id;
        public string name;
        public string icon;
        public double tier;           // 1..6
        public double maxRank;
        public Dictionary<string, double> per = new Dictionary<string, double>();   // Partial<PassiveEffects>
        public string description;    // Wirkung je Rang
    }

    public static partial class PassiveTree
    {
        public static readonly PassiveConstants PASSIVE = new PassiveConstants
        {
            wavesPerPoint = 10,
            maxPoints = 100,
            /** Benötigte, im Baum bereits ausgegebene Punkte je Stufe. */
            tierGate = new double[] { 0, 5, 15, 30, 50, 75 },
        };

        public static readonly PassiveEffects ZERO_PASSIVE = new PassiveEffects
        {
            heroHpPct = 0, heroDmgPct = 0, heroArmor = 0, heroCdr = 0, heroCrit = 0, heroRegen = 0, manaRegen = 0, supportPower = 0, pickupRadius = 0,
            xpPct = 0, towerDmgPct = 0, towerSpeedPct = 0, towerRange = 0, startGold = 0, killGoldPct = 0, resourcePct = 0, lootPct = 0, coreLives = 0,
            respawnPct = 0, unlockEarlier = 0, heroAtkSpeed = 0, abilityPct = 0, moveSpeed = 0, lifesteal = 0,
            towerCrit = 0, towerPierce = 0, towerCostPct = 0, mineIncomePct = 0, towerSplashPct = 0,
        };

        public static readonly List<PassiveNode> PASSIVE_NODES = new List<PassiveNode>
        {
            // Stufe 1 (frei)
            new PassiveNode { id = "p_vigor", name = "Zähigkeit", icon = "❤", tier = 1, maxRank = 5, per = new Dictionary<string, double> { ["heroHpPct"] = 0.03 }, description = "+3 % Helden-LP" },
            new PassiveNode { id = "p_might", name = "Kraft", icon = "⚔", tier = 1, maxRank = 5, per = new Dictionary<string, double> { ["heroDmgPct"] = 0.03 }, description = "+3 % Heldenschaden" },
            new PassiveNode { id = "p_learn", name = "Lernfähigkeit", icon = "📖", tier = 1, maxRank = 5, per = new Dictionary<string, double> { ["xpPct"] = 0.03 }, description = "+3 % Erfahrung" },
            new PassiveNode { id = "p_coin", name = "Sparstrumpf", icon = "🪙", tier = 1, maxRank = 5, per = new Dictionary<string, double> { ["startGold"] = 15 }, description = "+15 Startgold" },
            // Stufe 2 (5 Punkte im Baum)
            new PassiveNode { id = "p_towers", name = "Turmbaukunst", icon = "🗼", tier = 2, maxRank = 5, per = new Dictionary<string, double> { ["towerDmgPct"] = 0.02 }, description = "+2 % Turmschaden" },
            new PassiveNode { id = "p_tempo", name = "Taktfrequenz", icon = "⏱", tier = 2, maxRank = 5, per = new Dictionary<string, double> { ["towerSpeedPct"] = 0.015 }, description = "+1,5 % Turm-Angriffstempo" },
            new PassiveNode { id = "p_scav", name = "Plünderer", icon = "⛓", tier = 2, maxRank = 5, per = new Dictionary<string, double> { ["resourcePct"] = 0.04 }, description = "+4 % Ressourcen aus Beute und Wellen" },
            new PassiveNode { id = "p_magnet", name = "Magnetismus", icon = "🧲", tier = 2, maxRank = 3, per = new Dictionary<string, double> { ["pickupRadius"] = 0.1 }, description = "+0,1 Aufsammelradius" },
            // Stufe 3 (15)
            new PassiveNode { id = "p_plans", name = "Bauplan-Archiv", icon = "📐", tier = 3, maxRank = 3, per = new Dictionary<string, double> { ["unlockEarlier"] = 1 }, description = "Türme 1 Welle früher freigeschaltet" },
            new PassiveNode { id = "p_bounty", name = "Kopfgeldjäger", icon = "💰", tier = 3, maxRank = 5, per = new Dictionary<string, double> { ["killGoldPct"] = 0.03 }, description = "+3 % Kopfgeld" },
            new PassiveNode { id = "p_core", name = "Kernpanzerung", icon = "🛡", tier = 3, maxRank = 5, per = new Dictionary<string, double> { ["coreLives"] = 2 }, description = "+2 Kernenergie" },
            new PassiveNode { id = "p_runes", name = "Schutzrunen", icon = "🔰", tier = 3, maxRank = 5, per = new Dictionary<string, double> { ["heroArmor"] = 0.015 }, description = "+1,5 % Heldenrüstung" },
            // Stufe 4 (30)
            new PassiveNode { id = "p_calm", name = "Gelassenheit", icon = "🌀", tier = 4, maxRank = 5, per = new Dictionary<string, double> { ["heroCdr"] = 0.02 }, description = "+2 % Abklingzeitreduktion" },
            new PassiveNode { id = "p_range", name = "Weitsicht", icon = "🔭", tier = 4, maxRank = 5, per = new Dictionary<string, double> { ["towerRange"] = 0.05 }, description = "+0,05 Turmreichweite" },
            new PassiveNode { id = "p_luck", name = "Glücksfund", icon = "🍀", tier = 4, maxRank = 5, per = new Dictionary<string, double> { ["lootPct"] = 0.04 }, description = "+4 % Beutechance" },
            new PassiveNode { id = "p_revive", name = "Zähes Leben", icon = "✚", tier = 4, maxRank = 5, per = new Dictionary<string, double> { ["respawnPct"] = 0.06 }, description = "−6 % Wiederbelebungszeit" },
            // Stufe 5 (50)
            new PassiveNode { id = "p_patron", name = "Schutzpatron", icon = "✨", tier = 5, maxRank = 5, per = new Dictionary<string, double> { ["supportPower"] = 0.02 }, description = "+2 % Unterstützungsstärke (Turmverstärkungen)" },
            new PassiveNode { id = "p_spring", name = "Lebensquell", icon = "💚", tier = 5, maxRank = 5, per = new Dictionary<string, double> { ["heroRegen"] = 0.4 }, description = "+0,4 LP/s Regeneration" },
            new PassiveNode { id = "p_flow", name = "Manafluss", icon = "💧", tier = 5, maxRank = 5, per = new Dictionary<string, double> { ["manaRegen"] = 0.4 }, description = "+0,4 Mana/s" },
            new PassiveNode { id = "p_precise", name = "Präzision", icon = "🎯", tier = 5, maxRank = 5, per = new Dictionary<string, double> { ["heroCrit"] = 0.01 }, description = "+1 % kritische Trefferchance" },
            // Stufe 6 (75)
            new PassiveNode { id = "p_legend", name = "Legende des Kerns", icon = "👑", tier = 6, maxRank = 4, per = new Dictionary<string, double> { ["towerDmgPct"] = 0.03, ["heroDmgPct"] = 0.03, ["heroHpPct"] = 0.03 }, description = "+3 % Turmschaden, +3 % Heldenschaden, +3 % Helden-LP" },
        };

        public static readonly double PASSIVE_TOTAL_RANKS = sumMaxRank();
        private static double sumMaxRank()
        {
            double a = 0;
            foreach (PassiveNode n in PASSIVE_NODES) a = a + n.maxRank;
            return a;
        }

        public static double passivePoints(double bestWave)
        {
            return U.Max(0, U.Min(PASSIVE.maxPoints, Math.Floor(bestWave / PASSIVE.wavesPerPoint)));
        }

        public static double passiveSpent(Dictionary<string, double> passive)
        {
            double n = 0;
            foreach (PassiveNode node in PASSIVE_NODES) n += U.Max(0, U.Min(node.maxRank, U.GetN(passive, node.id) ?? 0));
            return n;
        }

        /// <summary>Prüft, ob ein Rang gekauft werden kann; Rückgabe: Grund oder null.</summary>
        public static string passiveRaiseReason(Dictionary<string, double> passive, double bestWave, string id)
        {
            PassiveNode node = PASSIVE_NODES.Find((n) => n.id == id);
            if (node == null) return "Unbekannter Knoten";
            double rank = U.GetN(passive, id) ?? 0;
            if (rank >= node.maxRank) return "Maximaler Rang";
            double spent = passiveSpent(passive);
            if (spent >= passivePoints(bestWave)) return $"Keine freien Punkte (1 Punkt je {U.Fmt(PASSIVE.wavesPerPoint)} Bestwellen)";
            double gate = PASSIVE.tierGate[(int)(node.tier - 1)];
            if (spent < gate) return $"Benötigt {U.Fmt(gate)} ausgegebene Punkte (Stufe {U.Fmt(node.tier)})";
            return null;
        }

        /// <summary>
        /// Bereinigt einen gespeicherten Baum: unbekannte Knoten, zu hohe Ränge, zu viele Punkte, Stufensperren.
        /// TS: <c>raw: unknown</c>. Akzeptiert Dictionary&lt;string, double&gt; und JSON-Objekte (IDictionary&lt;string, object&gt;).
        /// </summary>
        public static Dictionary<string, double> sanitizePassive(object raw, double bestWave)
        {
            Dictionary<string, double> out_ = new Dictionary<string, double>();
            if (!(raw is IDictionary<string, double>) && !(raw is IDictionary<string, object>)) return out_;
            // In Stufenreihenfolge neu aufbauen, damit Stufensperren und Punktelimit gelten.
            List<PassiveNode> nodes = U.StableSort(new List<PassiveNode>(PASSIVE_NODES), (a, b) => a.tier - b.tier);
            foreach (PassiveNode node in nodes)
            {
                double want = 0;
                if (raw is IDictionary<string, double> dd)
                {
                    if (dd.TryGetValue(node.id, out double w)) want = Math.Floor(w);
                }
                else if (raw is IDictionary<string, object> od)
                {
                    // typeof src[node.id] === 'number'
                    if (od.TryGetValue(node.id, out object w) && w is double wd) want = Math.Floor(wd);
                }
                for (int k = 0; k < U.Min(node.maxRank, U.Max(0, want)); k++)
                {
                    if (passiveRaiseReason(out_, bestWave, node.id) != null) break;
                    out_[node.id] = (U.GetN(out_, node.id) ?? 0) + 1;
                }
            }
            return out_;
        }

        public static PassiveEffects passiveEffects(Dictionary<string, double> passive)
        {
            PassiveEffects fx = ZERO_PASSIVE.Copy();
            if (passive == null) return fx;
            foreach (PassiveNode node in PASSIVE_NODES)
            {
                double r = U.Max(0, U.Min(node.maxRank, U.GetN(passive, node.id) ?? 0));
                if (!U.Truthy(r)) continue;
                foreach (KeyValuePair<string, double> kv in node.per) fx[kv.Key] += kv.Value * r;
            }
            return fx;
        }

        /// <summary>Paragon-Wirkungen (gemeinsam für alle Helden). <paramref name="into"/> = null → frische Kopie von ZERO_PASSIVE.</summary>
        public static PassiveEffects paragonEffects(Dictionary<string, double> points, PassiveEffects into = null)
        {
            if (into == null) into = ZERO_PASSIVE.Copy();
            if (points == null) return into;
            foreach (ParagonNode node in Paragon.PARAGON_NODES)
            {
                double r = U.Max(0, U.Min(node.maxRank, U.GetN(points, node.id) ?? 0));
                if (!U.Truthy(r)) continue;
                foreach (KeyValuePair<string, double> kv in node.per) into[kv.Key] += kv.Value * r;
            }
            return into;
        }

        /// <summary>TS: Werttyp des WeakMap-Caches <c>{ passive; rev; fx }</c>.</summary>
        private class FxCacheEntry
        {
            public Dictionary<string, double> passive;
            public double rev;
            public PassiveEffects fx;
        }

        // TS: WeakMap<object, …> → ConditionalWeakTable (schwache Schlüssel, Vergleich per Referenz).
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<HeroEndlessState, FxCacheEntry> fxCache =
            new System.Runtime.CompilerServices.ConditionalWeakTable<HeroEndlessState, FxCacheEntry>();

        /// <summary>Effekte des Laufs: passiver Baum (Schnappschuss beim Start) plus Paragon (im Lauf vergebbar, daher mit Revision).</summary>
        public static PassiveEffects runPassive(HeroEndlessState he)
        {
            if (he == null || (he.passive == null && he.paragon == null)) return ZERO_PASSIVE;
            double rev = he.paragon?.rev ?? 0;
            FxCacheEntry c;
            if (fxCache.TryGetValue(he, out c) && c != null && ReferenceEquals(c.passive, he.passive) && c.rev == rev) return c.fx;
            PassiveEffects fx = paragonEffects(he.paragon?.points, passiveEffects(he.passive));
            fxCache.Remove(he);
            fxCache.Add(he, new FxCacheEntry { passive = he.passive, rev = rev, fx = fx });
            return fx;
        }

        // ------------------------------------------------------------ Turmfreischaltung (Helden-Endlos)


        /// <summary>
        /// Welle, ab der ein Turm im Helden-Endlosmodus gebaut werden kann.
        /// Reihenfolge so gewählt, dass Konter rechtzeitig verfügbar sind (Luftabwehr vor Fliegern W5,
        /// Detektoren vor Schattenschleichern W9) und alle Elementarreaktionen sowie Fusionspartner
        /// paarweise freigeschaltet werden (Glut+Sturm, Frost+Kanone, Armbrust+Runenweber, Meteor+Laser).
        /// </summary>
        public static readonly Dictionary<string, double> TOWER_UNLOCKS = new Dictionary<string, double>
        {
            ["ranger"] = 1, ["ember"] = 1, ["frost"] = 1, ["goldmine"] = 1, ["barricade"] = 1, ["lanebarricade"] = 1,
            ["storm"] = 3, ["cannon"] = 3,
            ["spore"] = 5, ["ballista"] = 5,
            ["rune"] = 7, ["resonator"] = 7,
            ["pylon"] = 9, ["totem"] = 9,
            ["riftbinder"] = 11, ["tesla"] = 11,
            ["gravity"] = 14, ["meteor"] = 14,
            ["laser"] = 17, ["dragon"] = 17,
            ["minelayer"] = 6, ["sporemine"] = 0, ["skyguard"] = 5, ["skywatch"] = 0,
            ["plasma"] = 0, ["glacier"] = 0, ["voidlance"] = 0, ["starfall"] = 0, ["thornwood"] = 0, ["thunderspire"] = 0, ["riftgate"] = 0, ["phoenix"] = 0, ["cathedral"] = 0, ["stormeye"] = 0,
        };

        public static double towerUnlockWave(string def, HeroEndlessState he)
        {
            if (he == null) return 1;
            double @base = U.GetN(TOWER_UNLOCKS, def) ?? 1;
            if (@base <= 1) return 1;
            return U.Max(1, @base - runPassive(he).unlockEarlier);
        }

        /// <summary>Turm im aktuellen Lauf baubar? Maßgeblich ist die als Nächstes anstehende Welle.</summary>
        public static bool towerUnlocked(string def, HeroEndlessState he, double wave)
        {
            if (he == null) return true;
            return wave + 1 >= towerUnlockWave(def, he);
        }
    }
}
