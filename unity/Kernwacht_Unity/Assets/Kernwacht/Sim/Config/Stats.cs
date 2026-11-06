// Alle numerischen Turmwerte (Übersetzung von config/stats.ts). Upgrades, Modifikatoren, Netzwerkboni und
// Terrain wirken ausschließlich über diese Schlüssel (datengetrieben). Boolesche Werte werden als 0/1 gespeichert.
//
// StatKey = keyof typeof ZERO_STATS → string. Stats = Record<StatKey, number> → Dictionary<string, double>.
//
// WICHTIG: ZERO_STATS ist geteilt und darf nie verändert werden. TS `{ ...ZERO_STATS }` wird in C# zu
// `new Dictionary<string, double>(Stats.ZERO_STATS)` (oder `Stats.zeroStats()`); `{ ...ZERO_STATS, ...def.base }`
// → Kopie anlegen und danach jeden Eintrag von def.base per `st[k] = v` darüberschreiben (Reihenfolge bleibt
// die von ZERO_STATS, neue Schlüssel kommen ans Ende – wie beim JS-Spread).

using System;
using System.Collections.Generic;

namespace Kernwacht.Sim
{
    public static partial class Stats
    {
        public static readonly Dictionary<string, double> ZERO_STATS = new Dictionary<string, double>
        {
            /** Minenwerfer: höchstens so viele scharfe Minen gleichzeitig. */
            { "mineMax", 0 },
            { "damage", 0 },
            { "range", 0 },
            { "cooldown", 1 },
            { "splash", 0 },
            { "projectileSpeed", 0 },
            { "hitsAir", 0 },
            { "hitsGround", 1 },
            { "chains", 0 },
            { "chainFalloff", 0.75 },
            { "chainRange", 2.2 },
            { "pierce", 0 },
            { "multishot", 1 },
            { "burnDps", 0 },
            { "burnDur", 0 },
            { "burnSpread", 0 },
            { "chillPct", 0 },
            { "chillDur", 0 },
            { "chillMax", 0.6 },
            { "freezeChance", 0 },
            { "freezeDur", 0 },
            { "poisonDps", 0 },
            { "poisonDur", 0 },
            { "poisonMaxStacks", 5 },
            { "poisonSlow", 0 },
            /** 3.2 Rüstungsbruch (Dornenhain): physischer Blutungsschaden je Stapel und −shredPct physische Resistenz je Stapel. */
            /** 3.3: Schadensfaktor gegen Flieger und Vorrang für Luftziele (Drachenhorst, Phönix). */
            { "airDmgMul", 1 },
            { "preferAir", 0 },
            { "bleedDps", 0 },
            { "bleedDur", 0 },
            { "bleedMaxStacks", 8 },
            { "shredPct", 0 },
            { "cloudDur", 0 },
            { "cloudRadius", 0 },
            { "markPct", 0 },
            { "markDur", 0 },
            { "markSpread", 0 },
            { "interrupt", 0 },
            { "knockback", 0 },
            { "silenceDur", 0 },
            { "stunDur", 0 },
            { "brittleDur", 0 },
            { "armorPierce", 0 },
            { "maxHpPct", 0 },
            { "rampPct", 0 },
            { "rampMax", 0 },
            { "critChance", 0 },
            { "critMult", 2 },
            { "fragments", 0 },
            { "income", 0 },
            { "killBounty", 0 },
            { "eliteBounty", 0 },
            { "bountyRange", 0 },
            { "linkRange", 0 },
            { "maxLinks", 0 },
            { "networkBonus", 0 },
            { "zapDamage", 0 },
            { "riftBack", 0 },
            { "riftDelay", 1.2 },
            { "riftTargets", 1 },
            { "rootDur", 0 },
            { "tetherPct", 0 },
            { "damageMul", 1 },
            { "attackSpeedMul", 1 },
            { "rangeMul", 1 },
            { "arcDps", 0 },
            { "arcRange", 0 },
            { "pullCells", 0 },
            { "laserRamp", 0 },
            { "auraDmg", 0 },
            { "auraSpeed", 0 },
            { "meteorDelay", 1.1 },
            { "meteorCount", 1 },
            // 4.0 neue Türme
            /** Ätzkessel: Zersetzung senkt alle Resistenzen um corrodePct je Stapel (bis corrodeMax Stapel). */
            { "corrodePct", 0 },
            { "corrodeMax", 4 },
            { "corrodeDur", 0 },
            /** Trifft eingegrabene Gegner und holt sie an die Oberfläche (Bebenhammer). */
            { "hitsBurrowed", 0 },
            /** Hinrichtung: Nicht-Bosse unter diesem Lebensanteil sterben sofort (Adlerhorst). */
            { "executePct", 0 },
            /** Sturmbatterie: getroffene Flieger gelten so lange als Bodenziel. */
            { "groundDur", 0 },
            /** Pestkessel: beim Tod springen Gift und Zersetzung auf so viele Nachbarn über. */
            { "contagion", 0 },
            /** Handelskontor: Zinssatz und Zinsgrenze je Welle. */
            { "interest", 0 },
            { "interestCap", 0 },
            /** Schildwerk: Nachladezeit des Schutzes je Turm, Rückwurf auf den Verursacher. */
            { "wardCd", 0 },
            { "wardReflect", 0 },
            /** Wachkaserne: Soldaten, Haltekraft (s), Rückkehr (s), Gegner je Soldat. */
            { "soldiers", 0 },
            { "soldierHold", 0 },
            { "soldierRespawn", 10 },
            { "soldierBlock", 1 },
            /** Bannkreis: Heilsperre (s). */
            { "sealDur", 0 },
            /** Siegelstein: Gegner mit Fähigkeit in Vorbereitung zuerst. */
            { "castPriority", 0 },
            /** Windwacht: zusätzliche Rückstoßfelder für Flieger. */
            { "airKnock", 0 },
        };

        /// <summary>Object.keys(ZERO_STATS) in Deklarationsreihenfolge.</summary>
        public static readonly List<string> STAT_KEYS = new List<string>(ZERO_STATS.Keys);

        /// <summary>Menschlich lesbare Bezeichnungen für Tooltips (Partial&lt;Record&lt;StatKey, string&gt;&gt;).</summary>
        public static readonly Dictionary<string, string> STAT_LABELS = new Dictionary<string, string>
        {
            { "damage", "Schaden" },
            { "range", "Reichweite" },
            { "cooldown", "Nachladezeit" },
            { "splash", "Explosionsradius" },
            { "chains", "Kettensprünge" },
            { "pierce", "Durchschlag" },
            { "multishot", "Ziele pro Schuss" },
            { "burnDps", "Brand/s" },
            { "burnDur", "Branddauer" },
            { "chillPct", "Verlangsamung" },
            { "freezeChance", "Einfrierchance" },
            { "poisonDps", "Gift/s je Stapel" },
            { "poisonMaxStacks", "Max. Giftstapel" },
            { "airDmgMul", "Schaden gegen Flieger ×" },
            { "bleedDps", "Blutung/s je Stapel (physisch)" },
            { "bleedMaxStacks", "Max. Rüstungsbruch-Stapel" },
            { "shredPct", "Rüstungsbruch je Stapel" },
            { "markPct", "Resistenzschwächung" },
            { "interrupt", "Unterbrechung" },
            { "knockback", "Rückstoß" },
            { "silenceDur", "Stille" },
            { "stunDur", "Betäubung" },
            { "armorPierce", "Panzerbrechend" },
            { "critChance", "Kritische Chance" },
            { "income", "Gold/Welle" },
            { "killBounty", "Kopfgeld" },
            { "linkRange", "Verbindungsreichweite" },
            { "maxLinks", "Max. Verbindungen" },
            { "networkBonus", "Netzwerkbonus" },
            { "riftBack", "Rückversetzung" },
            { "arcDps", "Lichtbogen/s" },
            { "arcRange", "Bogenreichweite" },
            { "pullCells", "Sog (Felder)" },
            { "laserRamp", "Strahlaufbau" },
            { "auraDmg", "Aura-Schaden" },
            { "auraSpeed", "Aura-Tempo" },
            { "meteorCount", "Meteore je Angriff" },
            { "corrodePct", "Zersetzung je Stapel" },
            { "corrodeMax", "Max. Zersetzungsstapel" },
            { "executePct", "Hinrichtung unter" },
            { "groundDur", "Zu Boden gedrückt" },
            { "contagion", "Ansteckung (Nachbarn)" },
            { "interest", "Zinssatz" },
            { "interestCap", "Zinsgrenze" },
            { "wardCd", "Schutz lädt nach" },
            { "soldiers", "Soldaten" },
            { "soldierHold", "Halten (s)" },
            { "soldierRespawn", "Rückkehr (s)" },
            { "soldierBlock", "Gegner je Soldat" },
            { "sealDur", "Heilsperre" },
            { "airKnock", "Rückstoß Flieger +" },
        };

        /// <summary>Frische, veränderbare Kopie von ZERO_STATS (TS: <c>{ ...ZERO_STATS }</c>). Zusatzhilfe, kein TS-Export.</summary>
        public static Dictionary<string, double> zeroStats()
        {
            return new Dictionary<string, double>(ZERO_STATS);
        }
    }
}
