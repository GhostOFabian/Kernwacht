// Übersetzung von config/modifiers.ts.
// Roguelite-Modifikatoren. Jeder Eintrag besitzt einen konkret implementierten Effekt,
// der über aggregateModifiers() von den Systemen gelesen wird.

using System;
using System.Collections.Generic;

namespace Kernwacht.Sim
{
    public class ModAggregate
    {
        public Dictionary<string, double> dmgType = new Dictionary<string, double>();   // Record<DamageType, number>
        public double allDamage;
        public double rangeMul;
        public double attackSpeedMul;
        public double chainsAdd;
        public double chillDurMul;
        public double killGoldMul;
        public double bossHpMul;
        public double incomeMul;
        public double mineCostMul;
        public double towerCostMul;
        public double poisonStacksAdd;
        public double poisonDurMul;
        public double markAdd;
        public double networkBonusMul;
        public bool netDischarge;
        public double waveGoldMul;
        public bool interest;
        public double heroCooldownMul;
        public double reactionEnergyMul;
    }

    public static partial class Modifiers
    {
        public static readonly Dictionary<string, RunModifier> MODIFIERS = new Dictionary<string, RunModifier>
        {
            ["chainReaction"] = new RunModifier { id = "chainReaction", icon = "⚡", name = "Kettenreaktion", tags = new List<string> { "lightning" }, requiresTower = new List<string> { "storm", "pylon" }, description = "Blitzangriffe springen auf 2 zusätzliche Ziele über." },
            ["wildfire"] = new RunModifier { id = "wildfire", icon = "🔥", name = "Flächenbrand", tags = new List<string> { "fire", "tradeoff" }, requiresTower = new List<string> { "ember" }, description = "Feuerschaden +40 %, aber alle Türme −10 % Reichweite." },
            ["bounty"] = new RunModifier { id = "bounty", icon = "💰", name = "Kopfgeldjagd", tags = new List<string> { "economy", "tradeoff" }, description = "Abschüsse geben +30 % Gold, Bosse haben jedoch +25 % Lebenspunkte." },
            ["netDischarge"] = new RunModifier { id = "netDischarge", icon = "🕸", name = "Netzentladung", tags = new List<string> { "network" }, requiresTower = new List<string> { "pylon" }, description = "Netzwerkverbindungen fügen Gegnern, die sie kreuzen, jede Sekunde Blitzschaden zu." },
            ["eternalIce"] = new RunModifier { id = "eternalIce", icon = "❄", name = "Ewiges Eis", tags = new List<string> { "frost", "tradeoff" }, requiresTower = new List<string> { "frost" }, description = "Kälte und Einfrieren halten 60 % länger, Frostschaden −40 %." },
            ["goldRush"] = new RunModifier { id = "goldRush", icon = "⛏", name = "Goldrausch", tags = new List<string> { "economy", "tradeoff" }, requiresTower = new List<string> { "goldmine" }, description = "Goldgräber fördern +50 % Gold, kosten aber +40 % mehr." },
            ["toxicMist"] = new RunModifier { id = "toxicMist", icon = "☠", name = "Giftnebel", tags = new List<string> { "poison" }, requiresTower = new List<string> { "spore" }, description = "Gift: +3 maximale Stapel und +30 % Dauer." },
            ["sharpBlades"] = new RunModifier { id = "sharpBlades", icon = "🗡", name = "Geschärfte Klingen", tags = new List<string> { "physical" }, description = "Physischer Schaden +20 %." },
            ["arcaneEcho"] = new RunModifier { id = "arcaneEcho", icon = "✦", name = "Arkanes Echo", tags = new List<string> { "arcane" }, requiresTower = new List<string> { "rune" }, description = "Runenmarkierungen senken Resistenzen um weitere 10 %." },
            ["rapidFire"] = new RunModifier { id = "rapidFire", icon = "⏩", name = "Schnellfeuer", tags = new List<string> { "tradeoff" }, description = "Alle Türme +12 % Angriffstempo, Baukosten +10 %." },
            ["glassCannon"] = new RunModifier { id = "glassCannon", icon = "💥", name = "Glaskanone", tags = new List<string> { "tradeoff" }, exclusive = new List<string> { "bulwark" }, description = "Gesamter Schaden +25 %, sofort −5 Kernenergie." },
            ["bulwark"] = new RunModifier { id = "bulwark", icon = "🏰", name = "Bollwerk", tags = new List<string> { "defense", "tradeoff" }, exclusive = new List<string> { "glassCannon" }, description = "+8 Kernenergie (auch Maximum), Wellenbonus −10 %." },
            ["farSight"] = new RunModifier { id = "farSight", icon = "🔭", name = "Weitblick", tags = new List<string> { "range" }, exclusive = new List<string> { "wildfire" }, description = "Alle Türme +12 % Reichweite." },
            ["resonanceAmp"] = new RunModifier { id = "resonanceAmp", icon = "📡", name = "Resonanzverstärker", tags = new List<string> { "network" }, requiresTower = new List<string> { "pylon" }, description = "Netzwerkbonus +50 %." },
            ["interest"] = new RunModifier { id = "interest", icon = "🏦", name = "Zinsen", tags = new List<string> { "economy" }, description = "Am Wellenende +5 % des aktuellen Goldes (max. 40)." },
            ["heroism"] = new RunModifier { id = "heroism", icon = "🛡", name = "Heldenmut", tags = new List<string> { "hero", "tradeoff" }, requiresHeroes = true, description = "Abklingzeit der Helden −30 %, aber alle Türme −5 % Schaden." },
            ["riftTap"] = new RunModifier { id = "riftTap", icon = "⚡", name = "Rissanzapfung", tags = new List<string> { "hero" }, requiresHeroes = true, description = "Elementarreaktionen liefern doppelte Rissenergie." },
        };

        public static readonly List<string> MODIFIER_IDS = U.Keys(MODIFIERS);

        public static ModAggregate aggregateModifiers(IEnumerable<string> ids)
        {
            ModAggregate a = new ModAggregate
            {
                dmgType = new Dictionary<string, double> { ["physical"] = 1, ["fire"] = 1, ["frost"] = 1, ["lightning"] = 1, ["poison"] = 1, ["arcane"] = 1, ["sonic"] = 1 },
                allDamage = 1, rangeMul = 1, attackSpeedMul = 1, chainsAdd = 0, chillDurMul = 1, killGoldMul = 1,
                bossHpMul = 1, incomeMul = 1, mineCostMul = 1, towerCostMul = 1, poisonStacksAdd = 0, poisonDurMul = 1,
                markAdd = 0, networkBonusMul = 1, netDischarge = false, waveGoldMul = 1, interest = false,
                heroCooldownMul = 1, reactionEnergyMul = 1,
            };
            foreach (string id in ids)
            {
                switch (id)
                {
                    case "chainReaction": a.chainsAdd += 2; break;
                    case "wildfire": a.dmgType["fire"] *= 1.4; a.rangeMul *= 0.9; break;
                    case "bounty": a.killGoldMul *= 1.3; a.bossHpMul *= 1.25; break;
                    case "netDischarge": a.netDischarge = true; break;
                    case "eternalIce": a.chillDurMul *= 1.6; a.dmgType["frost"] *= 0.6; break;
                    case "goldRush": a.incomeMul *= 1.5; a.mineCostMul *= 1.4; break;
                    case "toxicMist": a.poisonStacksAdd += 3; a.poisonDurMul *= 1.3; break;
                    case "sharpBlades": a.dmgType["physical"] *= 1.2; break;
                    case "arcaneEcho": a.markAdd += 0.1; break;
                    case "rapidFire": a.attackSpeedMul *= 1.12; a.towerCostMul *= 1.1; break;
                    case "glassCannon": a.allDamage *= 1.25; break;
                    case "bulwark": a.waveGoldMul *= 0.9; break;
                    case "farSight": a.rangeMul *= 1.12; break;
                    case "resonanceAmp": a.networkBonusMul *= 1.5; break;
                    case "interest": a.interest = true; break;
                    case "heroism": a.heroCooldownMul *= 0.7; a.allDamage *= 0.95; break;
                    case "riftTap": a.reactionEnergyMul *= 2; break;
                }
            }
            return a;
        }
    }
}
