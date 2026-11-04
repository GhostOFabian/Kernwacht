// Übersetzung von config/elements.ts.
// Statuseffekte und Elementarreaktionen.
// Kombinationsregeln:
//  * Brand: kein Stapeln – stärkster DPS-Wert gewinnt, Dauer wird erneuert.
//  * Kälte: bis zu 3 Stapel, Gesamtverlangsamung durch chillMax des Turms begrenzt (hart max. 75 %).
//  * Gift: stapelbar bis poisonMaxStacks, jeder neue Treffer erneuert die Dauer.
//  * Markierung: stärkster Wert gilt, kein Stapeln.
//  * Einfrieren: danach 3 s Einfrier-Immunität (verhindert Dauerkontrolle).
// Reaktionsschaden löst niemals weitere Reaktionen aus; jede Reaktion besitzt eine
// Abklingzeit pro Gegner. Dadurch sind Endlosketten ausgeschlossen.

using System;
using System.Collections.Generic;

namespace Kernwacht.Sim
{
    /// <summary>TS: Ad-hoc-Objekt <c>STATUS_RULES</c> (config/elements.ts).</summary>
    public class StatusRulesConstants
    {
        public double chillHardCap;
        public double freezeImmuneDur;
        public double wetDur;
        public double brittleBonus;
        public double overloadMul;
        public double steamMul;
        public double shatterMul;
        public double shatterMaxHpPct;
        public double toxicBlazeRadius;
        public double markHardCap;
        public double bossTetherPct;
        public double poisonSlowCap;
        /** Rüstungsbruch senkt die physische Resistenz höchstens bis zu diesem Wert (leicht negativ = verwundbar). */
        public double shredFloor;
    }

    public static partial class Elements
    {
        public static readonly Dictionary<string, StatusEffectDefinition> STATUS_DEFS = new Dictionary<string, StatusEffectDefinition>
        {
            ["burn"] = new StatusEffectDefinition { id = "burn", name = "Brennend", description = "Erleidet Feuerschaden über Zeit. Stoppt Regeneration.", icon = "🔥", color = 0xff7030, maxStacks = 1 },
            ["chill"] = new StatusEffectDefinition { id = "chill", name = "Unterkühlt", description = "Verlangsamt (stapelbar bis 3).", icon = "❄", color = 0x80d0ff, maxStacks = 3 },
            ["freeze"] = new StatusEffectDefinition { id = "freeze", name = "Eingefroren", description = "Kann sich nicht bewegen und keine Fähigkeiten wirken.", icon = "🧊", color = 0xc0f0ff, maxStacks = 1 },
            ["freezeImmune"] = new StatusEffectDefinition { id = "freezeImmune", name = "Aufgetaut", description = "Kurzzeitig immun gegen Einfrieren.", icon = "·", color = 0x6090a0, maxStacks = 1 },
            ["wet"] = new StatusEffectDefinition { id = "wet", name = "Nass", description = "Erleidet +50 % Blitzschaden (Überladung).", icon = "💧", color = 0x4080ff, maxStacks = 1 },
            ["poison"] = new StatusEffectDefinition { id = "poison", name = "Vergiftet", description = "Giftschaden je Stapel. Stoppt Regeneration.", icon = "☠", color = 0x90e040, maxStacks = 8 },
            ["mark"] = new StatusEffectDefinition { id = "mark", name = "Runenmarkiert", description = "Alle Resistenzen gesenkt.", icon = "✦", color = 0xc080ff, maxStacks = 1 },
            ["brittle"] = new StatusEffectDefinition { id = "brittle", name = "Spröde", description = "+30 % physischer Schaden.", icon = "◇", color = 0xd0d0d0, maxStacks = 1 },
            ["silence"] = new StatusEffectDefinition { id = "silence", name = "Verstummt", description = "Kann keine Fähigkeiten wirken.", icon = "🔇", color = 0x60e0d0, maxStacks = 1 },
            ["riftMark"] = new StatusEffectDefinition { id = "riftMark", name = "Rissmal", description = "Wird gleich zurückversetzt.", icon = "◎", color = 0xe060e0, maxStacks = 1 },
            ["root"] = new StatusEffectDefinition { id = "root", name = "Gefesselt", description = "Bewegungsunfähig.", icon = "⛓", color = 0xe060e0, maxStacks = 1 },
            ["stun"] = new StatusEffectDefinition { id = "stun", name = "Betäubt", description = "Bewegungsunfähig, keine Fähigkeiten.", icon = "✷", color = 0xffff80, maxStacks = 1 },
            ["tether"] = new StatusEffectDefinition { id = "tether", name = "Rissfessel", description = "Erleidet zusätzlichen Schaden.", icon = "⚯", color = 0xff60ff, maxStacks = 1 },
            ["reveal"] = new StatusEffectDefinition { id = "reveal", name = "Aufgedeckt", description = "Getarnt oder eingegraben, aber sichtbar und angreifbar.", icon = "👁", color = 0xffffff, maxStacks = 1 },
            ["voidMark"] = new StatusEffectDefinition { id = "voidMark", name = "Leerenmal", description = "Erleidet zusätzlichen Schaden.", icon = "✧", color = 0xa060ff, maxStacks = 1 },
            ["pull"] = new StatusEffectDefinition { id = "pull", name = "Gravitation", description = "Wird zurückgezogen und verlangsamt.", icon = "◉", color = 0x8a6aff, maxStacks = 1 },
            ["shred"] = new StatusEffectDefinition { id = "shred", name = "Rüstungsbruch", description = "Blutet (physischer Schaden je Stapel) und verliert physische Resistenz je Stapel. Stoppt Regeneration.", icon = "🩸", color = 0xd04040, maxStacks = 8 },
            ["weather"] = new StatusEffectDefinition { id = "weather", name = "Wetter", description = "Vom Geländeereignis betroffen.", icon = "☁", color = 0xa0a0c0, maxStacks = 1 },
        };

        public static readonly List<ElementReaction> REACTIONS = new List<ElementReaction>
        {
            new ElementReaction { id = "steam", name = "Dampfstoß", trigger = "fire", requires = "chill", cooldown = 1, description = "Feuer auf unterkühlte Ziele: Kälte verdampft, +40 % Trefferschaden, Ziel wird 5 s nass." },
            new ElementReaction { id = "thermalShock", name = "Thermoschock", trigger = "frost", requires = "burn", cooldown = 1.5, description = "Frost auf brennende Ziele: löscht den Brand, verursacht den verbleibenden Brandschaden sofort und macht spröde." },
            new ElementReaction { id = "overload", name = "Überladung", trigger = "lightning", requires = "wet", cooldown = 0, description = "Blitz auf nasse Ziele: +50 % Schaden und +1 Kettensprung." },
            new ElementReaction { id = "toxicBlaze", name = "Giftbrand", trigger = "fire", requires = "poison", cooldown = 2, description = "Feuer auf vergiftete Ziele: das Gift explodiert und trifft Gegner im Umkreis von 1,2 Zellen." },
            new ElementReaction { id = "shatter", name = "Resonanzbruch", trigger = "sonic", requires = "freeze", cooldown = 1, description = "Schall auf gefrorene Ziele: zerschmettert das Eis, +100 % Schaden plus 6 % der max. LP." },
        };

        public static readonly Dictionary<string, ElementReaction> REACTION_BY_ID = buildReactionById();

        private static Dictionary<string, ElementReaction> buildReactionById()
        {
            // TS: Object.fromEntries(REACTIONS.map((r) => [r.id, r]))
            Dictionary<string, ElementReaction> d = new Dictionary<string, ElementReaction>();
            foreach (ElementReaction r in REACTIONS) d[r.id] = r;
            return d;
        }

        public static readonly StatusRulesConstants STATUS_RULES = new StatusRulesConstants
        {
            chillHardCap = 0.75,
            freezeImmuneDur = 3,
            wetDur = 5,
            brittleBonus = 0.3,
            overloadMul = 1.5,
            steamMul = 1.4,
            shatterMul = 2.0,
            shatterMaxHpPct = 0.06,
            toxicBlazeRadius = 1.2,
            markHardCap = 0.6,
            bossTetherPct = 0.15,
            poisonSlowCap = 0.3,
            /** Rüstungsbruch senkt die physische Resistenz höchstens bis zu diesem Wert (leicht negativ = verwundbar). */
            shredFloor = -0.2,
        };

        // Netzwerkresonanzen: Wenn alle geforderten Turmtypen im selben aktiven Netzwerk sind,
        // erhalten alle Mitglieder des Netzwerks die Modifikatoren.
        public static readonly List<ResonanceDefinition> RESONANCES = new List<ResonanceDefinition>
        {
            new ResonanceDefinition { id = "elemental", name = "Elementarresonanz", requires = new List<string> { "ember", "frost", "storm" }, description = "Feuer, Frost und Blitz im selben Netz: +20 % Schaden, +10 % Statusdauer.", mods = new List<StatMod> { new StatMod { stat = "damageMul", mul = 1.2 }, new StatMod { stat = "burnDur", mul = 1.1 }, new StatMod { stat = "chillDur", mul = 1.1 } } },
            new ResonanceDefinition { id = "arcaneCircle", name = "Arkankreis", requires = new List<string> { "rune", "riftbinder", "resonator" }, description = "Rune, Rissbinder und Resonator: Markierungen +50 % länger, Unterbrechung +50 %, Reichweite +0,3.", mods = new List<StatMod> { new StatMod { stat = "markDur", mul = 1.5 }, new StatMod { stat = "interrupt", mul = 1.5 }, new StatMod { stat = "range", add = 0.3 } } },
            new ResonanceDefinition { id = "siegeLink", name = "Belagerungsverbund", requires = new List<string> { "ballista", "cannon", "ranger" }, description = "Armbrust, Kanone und Waldläufer: +20 % Panzerbrechung, +10 % Tempo.", mods = new List<StatMod> { new StatMod { stat = "armorPierce", add = 0.2 }, new StatMod { stat = "attackSpeedMul", mul = 1.1 } } },
            new ResonanceDefinition { id = "blight", name = "Fäulnisnetz", requires = new List<string> { "spore", "ember", "rune" }, description = "Sporen, Glut und Rune: Gift und Brand +25 % stärker.", mods = new List<StatMod> { new StatMod { stat = "poisonDps", mul = 1.25 }, new StatMod { stat = "burnDps", mul = 1.25 } } },
            new ResonanceDefinition { id = "cosmic", name = "Kosmische Konvergenz", requires = new List<string> { "meteor", "gravity", "laser" }, description = "Meteor, Gravitation und Laser: +25 % Schaden, +0,3 Reichweite.", mods = new List<StatMod> { new StatMod { stat = "damageMul", mul = 1.25 }, new StatMod { stat = "range", add = 0.3 } } },
            new ResonanceDefinition { id = "dragonfire", name = "Drachenglut", requires = new List<string> { "dragon", "ember", "meteor" }, description = "Drache, Glut und Meteor: Brand +40 %, +10 % Tempo.", mods = new List<StatMod> { new StatMod { stat = "burnDps", mul = 1.4 }, new StatMod { stat = "attackSpeedMul", mul = 1.1 } } },
            new ResonanceDefinition { id = "stormgrid", name = "Gewitternetz", requires = new List<string> { "tesla", "storm", "pylon" }, description = "Tesla, Sturmrufer und Pylon: Lichtbogen +50 %, +1 Kettensprung.", mods = new List<StatMod> { new StatMod { stat = "arcDps", mul = 1.5 }, new StatMod { stat = "chains", add = 1 } } },
        };

        /** Maximale Pylonverbindungen eines Nicht-Pylon-Turms (verhindert Mehrfachstapelung). */
        public const double TOWER_MAX_PYLON_LINKS = 2;
    }
}
