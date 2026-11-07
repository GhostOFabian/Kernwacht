using System.Linq;
// Erweiterung 4.0: acht neue Grundtürme und dreizehn neue Fusionen.
// Sie schließen Lücken im Gegnerkonter: Fähigkeiten unterbrechen (Siegelstein), Resistenzen zersetzen (Ätzkessel),
// eingegrabene Gegner treffen (Bebenhammer), zurückstoßen (Windwacht), Eliten jagen (Adlerhorst), Lane halten
// (Wachkaserne), Zinsen (Handelskontor) und Türme vor Betäubung schützen (Schildwerk).
// Balance: Schaden je Gold liegt im Band der bestehenden Türme gleicher Rolle; Kontrolleffekte haben je Gegner
// eigene Sperrzeiten (Rückstoß, Siegel) bzw. Obergrenzen (Zersetzung), damit sie sich nicht beliebig stapeln.

using System.Collections.Generic;

namespace Kernwacht.Sim
{
    public static class TowersNew
    {
        static UpgradeDefinition Up(string id, string name, string desc, double cost, params StatMod[] mods)
            => new UpgradeDefinition { id = id, name = name, description = desc, cost = cost, mods = new List<StatMod>(mods) };

        static StatMod Add(string stat, double v) => new StatMod { stat = stat, add = v };
        static StatMod Mul(string stat, double v) => new StatMod { stat = stat, mul = v };

        static Dictionary<string, double> B(params object[] kv)
        {
            var d = new Dictionary<string, double>();
            for (int i = 0; i + 1 < kv.Length; i += 2) d[(string)kv[i]] = System.Convert.ToDouble(kv[i + 1]);
            return d;
        }

        /// <summary>Goldgräber und ihre Fusionen (2×2, Einkommen, Goldadern).</summary>
        public static readonly HashSet<string> MINES = new HashSet<string> { "goldmine", "treasury", "deepdrill" };
        /// <summary>Türme mit Kopfgeld-Werten (Abschüsse in der Nähe bringen Gold).</summary>
        public static readonly HashSet<string> BOUNTY = new HashSet<string> { "goldmine", "bank", "treasury" };
        /// <summary>Schildwerke (schützen Türme in Reichweite vor Betäubung).</summary>
        public static readonly HashSet<string> WARDS = new HashSet<string> { "ward", "shieldnet" };

        public static Dictionary<string, TowerDefinition> Add(Dictionary<string, TowerDefinition> d)
        {
            // ================================================================ Grundtürme

            d["seal"] = new TowerDefinition
            {
                id = "seal", active = "nova", name = "Siegelstein", role = "Arkan · Unterbrechung", icon = "🔯", hotkey = "", color = 0x9a7cff,
                description = "Ein Siegelstrahl trifft bevorzugt Gegner, die gerade eine Fähigkeit vorbereiten: Er bricht sie ab und lässt das Ziel kurz verstummen. Bosse verlieren Standfestigkeit.",
                damageType = "arcane", cost = 115, attack = "chain", defaultPriority = "first", networkable = true,
                @base = B("damage", 12, "range", 3.6, "cooldown", 1.1, "chains", 0, "chainFalloff", 1, "chainRange", 2.2, "interrupt", 40, "silenceDur", 1.5, "castPriority", 1, "hitsAir", 1),
                upgrades = new List<UpgradeDefinition>
                {
                    Up("seal1", "Schwere Siegel", "Stille hält 1 s länger", 60, Add("silenceDur", 1)),
                    Up("seal2", "Bannrunen", "+40 % Unterbrechung, +6 Schaden", 85, Mul("interrupt", 1.4), Add("damage", 6)),
                    Up("seal3", "Fernsiegel", "+0,5 Reichweite, +20 % Tempo", 120, Add("range", 0.5), Mul("cooldown", 0.83)),
                },
                specs = new List<UpgradeDefinition>
                {
                    Up("sealA", "Siegelkette", "Der Strahl springt auf 2 weitere Gegner über", 210, Add("chains", 2), Add("damage", 4)),
                    Up("sealB", "Schweigegelübde", "Stille 4 s, doppelte Unterbrechung (Bosse), +30 % Schaden", 210, Add("silenceDur", 1.5), Mul("interrupt", 2), Mul("damage", 1.3)),
                },
                synergies = "Konter gegen Heilerschamanen, Belagerungsbestien und Boss-Fähigkeiten. Mit dem Resonator wird er zur Stillezitadelle, mit dem Totemschrein zum Bannkreis.",
                counters = "Wenig Schaden – braucht Türme daneben. Fähigkeitslose Gegner sind ihm egal.",
            };

            d["acid"] = new TowerDefinition
            {
                id = "acid", active = "overdrive", name = "Ätzkessel", role = "Gift · Zersetzung", icon = "⚗️", hotkey = "", color = 0x9ad040,
                description = "Kippt Säure auf die Lane. Jeder Spritzer zersetzt das Ziel: −5 % auf alle Resistenzen je Stapel (bis 4 Stapel). Damit treffen alle anderen Türme härter – auch Steinhäuter und Schildträger.",
                damageType = "poison", cost = 110, attack = "projectile", defaultPriority = "first", networkable = true,
                @base = B("damage", 10, "range", 3.2, "cooldown", 1.4, "projectileSpeed", 6, "splash", 0.8, "corrodePct", 0.05, "corrodeMax", 4, "corrodeDur", 5, "poisonDps", 3, "poisonDur", 3),
                upgrades = new List<UpgradeDefinition>
                {
                    Up("acid1", "Konzentrat", "Zersetzung −6 % je Stapel", 60, Add("corrodePct", 0.01)),
                    Up("acid2", "Breiter Guss", "Spritzradius +0,3, +0,3 Reichweite", 85, Add("splash", 0.3), Add("range", 0.3)),
                    Up("acid3", "Kesseldruck", "+25 % Tempo, +6 Schaden", 120, Mul("cooldown", 0.8), Add("damage", 6)),
                },
                specs = new List<UpgradeDefinition>
                {
                    Up("acidA", "Auflösung", "Bis 6 Zersetzungsstapel, hält 3 s länger", 210, Add("corrodeMax", 2), Add("corrodeDur", 3)),
                    Up("acidB", "Säurebad", "Hinterlässt eine Säurepfütze (3 s), die vergiftet und zersetzt; +40 % Schaden", 210, Add("cloudDur", 3), Add("cloudRadius", 0.9), Mul("damage", 1.4)),
                },
                synergies = "Verstärkt jeden anderen Turm. Zersetzung wirkt auf alle Schadensarten – ideal vor Armbrust, Kanone, Blitz und Feuer.",
                counters = "Trifft keine Flieger. Eigener Schaden gering. Zersetzung endet bei −20 % Resistenz.",
            };

            d["quake"] = new TowerDefinition
            {
                id = "quake", active = "overdrive", name = "Bebenhammer", role = "Physisch · Erdstoß", icon = "🔨", hotkey = "", color = 0xa08060,
                description = "Ein Dampfhammer schlägt auf den Boden: Die Welle trifft alle Bodengegner in Reichweite, lässt sie kurz taumeln – und trifft auch eingegrabene Tunnelgräber, die dabei an die Oberfläche geschleudert werden.",
                damageType = "physical", cost = 125, attack = "pulse", defaultPriority = "first", networkable = true,
                @base = B("damage", 34, "range", 2.2, "cooldown", 2.2, "stunDur", 0.2, "armorPierce", 0.15, "hitsBurrowed", 1),
                upgrades = new List<UpgradeDefinition>
                {
                    Up("quake1", "Schwerer Kolben", "+10 Schaden", 70, Add("damage", 10)),
                    Up("quake2", "Tiefer Stoß", "+0,4 Reichweite", 90, Add("range", 0.4)),
                    Up("quake3", "Dampfdruck", "+20 % Tempo, Taumeln +0,1 s", 130, Mul("cooldown", 0.83), Add("stunDur", 0.1)),
                },
                specs = new List<UpgradeDefinition>
                {
                    Up("quakeA", "Erdspalter", "Schaden ×1,7, Ziele 2 s spröde", 230, Mul("damage", 1.7), Add("brittleDur", 2)),
                    Up("quakeB", "Nachbeben", "Taumeln 0,6 s, +0,4 Reichweite", 230, Add("stunDur", 0.3), Add("range", 0.4)),
                },
                synergies = "Einziger Grundturm, der Tunnelgräber unter der Erde trifft. Bündelt mit Minen und Kanonen.",
                counters = "Trifft keine Flieger. Kurze Reichweite – direkt an die Lane stellen. Bosse taumeln nicht, verlieren aber Standfestigkeit.",
            };

            d["gale"] = new TowerDefinition
            {
                id = "gale", active = "overdrive", name = "Windwacht", role = "Kontrolle · Rückstoß", icon = "🌬️", hotkey = "", color = 0xb8d8e8,
                description = "Windsegel erzeugen Böen, die alle Gegner in Reichweite ein Feld zurückwerfen – Flieger zwei Felder. Jeder Gegner kann nur alle 2,5 s zurückgestoßen werden. Bosse werden nur gebremst.",
                damageType = "physical", cost = 120, attack = "pulse", defaultPriority = "first", networkable = true,
                @base = B("damage", 6, "range", 2.4, "cooldown", 2.4, "knockback", 1, "airKnock", 1, "chillPct", 0.1, "chillDur", 1.2, "chillMax", 0.3, "hitsAir", 1),
                upgrades = new List<UpgradeDefinition>
                {
                    Up("gale1", "Weite Segel", "+0,4 Reichweite", 60, Add("range", 0.4)),
                    Up("gale2", "Böenkamm", "+6 Schaden, +15 % Tempo", 85, Add("damage", 6), Mul("cooldown", 0.87)),
                    Up("gale3", "Sturmlauf", "Flieger werden ein weiteres Feld zurückgeworfen", 110, Add("airKnock", 1)),
                },
                specs = new List<UpgradeDefinition>
                {
                    Up("galeA", "Orkan", "Rückstoß 2 Felder, dafür 30 % langsamer", 230, Add("knockback", 1), Mul("cooldown", 1.3)),
                    Up("galeB", "Scherwind", "+12 Schaden, Ziele 3 s spröde (+30 % physischer Schaden)", 230, Add("damage", 12), Add("brittleDur", 3)),
                },
                synergies = "Hält Gegner länger im Feuer – gut vor Meteoren, Minen und Kanonen. Stark gegen Sturmgreife und Flinklinge.",
                counters = "Kaum Schaden. Bosse werden nur verlangsamt. Sperrzeit je Gegner verhindert Dauerrückstoß.",
            };

            d["eyrie"] = new TowerDefinition
            {
                id = "eyrie", active = "overdrive", name = "Adlerhorst", role = "Physisch · Scharfschütze", icon = "🦅", hotkey = "", color = 0xc8a060,
                description = "Ein Scharfschütze mit riesiger Reichweite. Zielt auf den stärksten Gegner und richtet Nicht-Bosse unter 10 % Leben sofort hin. Bosse unter 30 % Leben erleiden zusätzlichen Schaden.",
                damageType = "physical", cost = 150, attack = "projectile", defaultPriority = "strong", networkable = true,
                @base = B("damage", 95, "range", 6.5, "cooldown", 3.2, "projectileSpeed", 26, "executePct", 0.1, "armorPierce", 0.3, "hitsAir", 1),
                upgrades = new List<UpgradeDefinition>
                {
                    Up("eyrie1", "Fernrohr", "+1 Reichweite", 80, Add("range", 1)),
                    Up("eyrie2", "Schwere Bolzen", "+30 Schaden", 110, Add("damage", 30)),
                    Up("eyrie3", "Ruhige Hand", "+20 % Tempo, 10 % kritische Treffer", 150, Mul("cooldown", 0.83), Add("critChance", 0.1)),
                },
                specs = new List<UpgradeDefinition>
                {
                    Up("eyrieA", "Henker", "Hinrichtung schon unter 18 % Leben", 250, Add("executePct", 0.08)),
                    Up("eyrieB", "Doppelschuss", "Trifft 2 Ziele gleichzeitig", 250, Add("multishot", 1)),
                },
                synergies = "Erledigt Eliten, Schamanen und Bossgefolge aus großer Entfernung. Mit der Armbrust wird er zum Falkenauge, mit dem Kristalllaser zur Sonnenlanze.",
                counters = "Sehr langsam – schwach gegen Schwärme.",
            };

            d["barracks"] = new TowerDefinition
            {
                id = "barracks", active = "overdrive", name = "Wachkaserne", role = "Physisch · Lane halten", icon = "🛡️", hotkey = "", color = 0xb05040,
                description = "Schickt 2 Soldaten auf die Lane. Jeder hält einen Bodengegner auf und kämpft – bis seine Kraft (6 s Halten) erschöpft ist. Dann kehrt er nach 10 s zurück. Eliten zehren doppelt, Bosse werden nicht aufgehalten, sondern nur gebremst.",
                damageType = "physical", cost = 130, attack = "barracks", defaultPriority = "first", networkable = true,
                @base = B("damage", 8, "range", 2.6, "cooldown", 0.8, "soldiers", 2, "soldierHold", 6, "soldierRespawn", 10, "soldierBlock", 1),
                upgrades = new List<UpgradeDefinition>
                {
                    Up("bar1", "Kettenhemden", "Halten +3 s", 70, Add("soldierHold", 3)),
                    Up("bar2", "Drill", "+4 Schaden, Rückkehr 2 s schneller", 95, Add("damage", 4), Add("soldierRespawn", -2)),
                    Up("bar3", "Dritte Lanze", "+1 Soldat", 130, Add("soldiers", 1)),
                },
                specs = new List<UpgradeDefinition>
                {
                    Up("barA", "Schildwall", "Halten ×2, jeder Soldat hält 2 Gegner", 230, Mul("soldierHold", 2), Add("soldierBlock", 1)),
                    Up("barB", "Klingenmeister", "Schaden ×1,8, Treffer machen 2 s spröde", 230, Mul("damage", 1.8), Add("brittleDur", 2)),
                },
                synergies = "Hält Gegner im Feuer von Kanonen, Frost und Meteoren fest. Mit dem Totemschrein entsteht die Ordensburg.",
                counters = "Flieger und eingegrabene Gegner laufen vorbei. Schwärme verbrauchen die Soldaten schnell.",
            };

            d["bank"] = new TowerDefinition
            {
                id = "bank", active = "goldrush", name = "Handelskontor", role = "Wirtschaft · Zinsen", icon = "🏦", hotkey = "", color = 0xe0b050,
                description = "Zahlt nach jeder überstandenen Welle 4 % deines ungenutzten Golds als Zinsen – höchstens 18 Gold plus 0,6 je Welle. Zahlt erst ab der Welle nach dem Bau.",
                damageType = null, cost = 120, attack = "economy", defaultPriority = "first", networkable = false,
                @base = B("interest", 0.04, "interestCap", 18, "range", 0),
                upgrades = new List<UpgradeDefinition>
                {
                    Up("bank1", "Tresor", "Zinsgrenze +8", 65, Add("interestCap", 8)),
                    Up("bank2", "Buchhalter", "Zinssatz +1 %", 80, Add("interest", 0.01)),
                    Up("bank3", "Handelsrechte", "Zinsgrenze +10", 110, Add("interestCap", 10)),
                },
                specs = new List<UpgradeDefinition>
                {
                    Up("bankA", "Großbank", "Zinsgrenze ×1,6", 200, Mul("interestCap", 1.6)),
                    Up("bankB", "Wechselstube", "Abschüsse im Umkreis von 3 Feldern: +1 Gold, Elite +10", 200, Add("killBounty", 1), Add("eliteBounty", 10), Add("bountyRange", 3)),
                },
                synergies = "Belohnt Sparen: Gold für die nächste große Ausbaustufe liegen lassen. Mehrere Kontore teilen sich den Goldvorrat – jedes verzinst einen eigenen Teil. Mit dem Goldgräber entsteht die Schatzkammer.",
                counters = "Kein Kampfwert. Nur mit Goldreserve lohnend – wer alles sofort ausgibt, verdient nichts.",
            };

            d["ward"] = new TowerDefinition
            {
                id = "ward", active = null, name = "Schildwerk", role = "Unterstützung · Schutz", icon = "🔰", hotkey = "", color = 0x70c8e8,
                description = "Eine Kristallkuppel schützt Türme in 2,5 Feldern: Die nächste Betäubung oder Lähmung durch Gegner, Bosse oder Explosionen prallt ab. Danach lädt der Schutz des betroffenen Turms 14 s nach.",
                damageType = null, cost = 100, attack = "economy", defaultPriority = "first", networkable = false,
                @base = B("range", 2.5, "wardCd", 14),
                upgrades = new List<UpgradeDefinition>
                {
                    Up("ward1", "Verstärkte Kuppel", "+0,5 Reichweite", 60, Add("range", 0.5)),
                    Up("ward2", "Schnelle Aufladung", "Nachladen 3 s schneller", 80, Add("wardCd", -3)),
                    Up("ward3", "Kristallgitter", "Nachladen 2 s schneller, +0,3 Reichweite", 110, Add("wardCd", -2), Add("range", 0.3)),
                },
                specs = new List<UpgradeDefinition>
                {
                    Up("wardA", "Bollwerk", "+1 Reichweite", 190, Add("range", 1)),
                    Up("wardB", "Spiegelschild", "Abgewehrte Fähigkeiten betäuben den Verursacher 1,5 s (Bosse: Standfestigkeit)", 190, Add("wardReflect", 1)),
                },
                synergies = "Gegen Belagerungsbestien, Erdstöße, Weltenstampfer und Feuerzonen. Mehrere Schildwerke überlappen nicht – jeder Turm hat einen Schutz. Mit dem Leitpylon entsteht das Schildnetz.",
                counters = "Kein eigener Angriff. Schützt nicht gegen Netzwerkstörung und Verschlingen.",
            };

            // ================================================================ Fusionen

            TowerDefinition L(string id, string name, string role, string icon, double color, string desc, string type, string attack, string prio, string active, Dictionary<string, double> b, string syn, string cnt, bool detector = false)
                => new TowerDefinition
                {
                    id = id, legendary = true, active = active, name = name, role = role, icon = icon, hotkey = "", color = color, description = desc,
                    damageType = type, cost = 0, attack = attack, defaultPriority = prio, networkable = attack != "economy", @base = b,
                    upgrades = new List<UpgradeDefinition>(), specs = new List<UpgradeDefinition>(), synergies = syn, counters = cnt, detector = detector ? true : (bool?)null,
                };

            d["silencehall"] = L("silencehall", "Stillezitadelle", "Legendär · Schall & Stille", "🔕", 0x80e0f0,
                "Fusion aus Siegelstein und Resonator: Jeder Glockenschlag bricht im ganzen Radius alle Fähigkeiten ab und lässt Gegner 3 s verstummen. Deckt Getarnte auf.",
                "sonic", "pulse", "first", "nova",
                B("damage", 65, "range", 3.4, "cooldown", 1.6, "interrupt", 90, "silenceDur", 3, "markPct", 0.12, "markDur", 3, "hitsAir", 1),
                "Schaltet Heilerschamanen und ganze Bossphasen ab.", "Geringer Einzelschaden.", true);

            d["banecircle"] = L("banecircle", "Bannkreis", "Legendär · Arkan & Bann", "⭕", 0xb070ff,
                "Fusion aus Siegelstein und Totemschrein: Ein Runenkreis bannt alle Gegner darin – sie verstummen, werden 3 s lang nicht geheilt und verlieren 20 % Resistenz.",
                "arcane", "pulse", "first", "nova",
                B("damage", 45, "range", 3.0, "cooldown", 1.2, "silenceDur", 2.5, "sealDur", 3, "interrupt", 30, "markPct", 0.2, "markDur", 3, "hitsAir", 1),
                "Stoppt Heilung, Regeneration und Zwillingsbund.", "Kleiner Radius.", true);

            d["acidmortar"] = L("acidmortar", "Säuremörser", "Legendär · Gift & Artillerie", "🧪", 0xa8e040,
                "Fusion aus Ätzkessel und Kanonenschmiede: Säuregranaten aus großer Entfernung. Der Einschlag zersetzt ganze Gruppen (bis 4 Stapel) und hinterlässt eine ätzende Pfütze.",
                "poison", "projectile", "first", "overdrive",
                B("damage", 60, "range", 5, "cooldown", 2.2, "projectileSpeed", 7, "splash", 1.7, "corrodePct", 0.06, "corrodeMax", 4, "corrodeDur", 6, "cloudDur", 3, "cloudRadius", 1.0, "poisonDps", 8, "poisonDur", 3, "poisonMaxStacks", 5),
                "Bricht Panzer und Schilde für alle anderen Türme.", "Trifft keine Flieger.");

            d["plaguepot"] = L("plaguepot", "Pestkessel", "Legendär · Seuche", "☣️", 0x8ad030,
                "Fusion aus Ätzkessel und Sporenwerfer: Seuchenschleim vergiftet stark und zersetzt. Stirbt ein Gegner, springen Gift und Zersetzung auf bis zu 3 Nachbarn über.",
                "poison", "projectile", "first", "overdrive",
                B("damage", 70, "range", 3.8, "cooldown", 1.0, "projectileSpeed", 7, "splash", 1.1, "poisonDps", 28, "poisonDur", 6, "poisonMaxStacks", 10, "corrodePct", 0.05, "corrodeMax", 5, "corrodeDur", 6, "contagion", 3),
                "Verheerend gegen dichte Wellen und Brutmütter. Feuer auf Gift löst Giftbrand aus.", "Flieger, giftresistente Gegner.");

            d["quakemaw"] = L("quakemaw", "Bebenschlund", "Legendär · Erdbeben", "🌋", 0xc08050,
                "Fusion aus Bebenhammer und Gravitationsbrunnen: Zieht Bodengegner zusammen und schlägt dann zu – Taumeln 0,5 s, trifft und entgräbt Tunnelgräber.",
                "physical", "gravity", "first", "rewind",
                B("damage", 70, "range", 3.2, "cooldown", 2.2, "pullCells", 1, "stunDur", 0.5, "hitsBurrowed", 1, "armorPierce", 0.3),
                "Bündelt Gegner für Minen, Meteore und Kanonen.", "Trifft keine Flieger. Bosse werden nicht gezogen.");

            d["cyclone"] = L("cyclone", "Zyklonturm", "Legendär · Sturm & Wind", "🌪️", 0xa8d8ff,
                "Fusion aus Windwacht und Sturmrufer: Gewitterböen springen zwischen Gegnern und werfen jeden getroffenen ein Feld zurück (Flieger drei).",
                "lightning", "chain", "first", "overload",
                B("damage", 95, "range", 3.6, "cooldown", 1.3, "chains", 4, "chainFalloff", 0.85, "chainRange", 2.3, "knockback", 1, "airKnock", 2, "hitsAir", 1),
                "Nasse Ziele erleiden Überladung.", "Blitzresistente Saboteure. Bosse werden nicht zurückgestoßen.");

            d["stormbattery"] = L("stormbattery", "Sturmbatterie", "Legendär · Luftabwehr", "🛩️", 0x90b8d8,
                "Fusion aus Windwacht und Flakbatterie: Fallwind-Granaten drücken Flieger 3 s zu Boden – dann treffen sie auch alle Bodentürme. 3 Ziele, ×2 Schaden gegen Flieger.",
                "physical", "projectile", "first", "overdrive",
                B("damage", 90, "range", 5.2, "cooldown", 0.5, "projectileSpeed", 26, "multishot", 3, "splash", 0.8, "hitsAir", 1, "hitsGround", 0, "groundDur", 3, "airDmgMul", 2),
                "Macht Kanonen, Sporen und Minen gegen Flieger nutzbar.", "Trifft keine Bodengegner.");

            d["falcon"] = L("falcon", "Falkenauge", "Legendär · Präzision", "🎯", 0xd8b070,
                "Fusion aus Adlerhorst und Belagerungsarmbrust: Ein durchschlagender Bolzen trifft alles in einer Linie und richtet Nicht-Bosse unter 14 % Leben hin.",
                "physical", "projectile", "strong", "overdrive",
                B("damage", 250, "range", 8, "cooldown", 2.8, "projectileSpeed", 30, "pierce", 4, "executePct", 0.14, "armorPierce", 0.8, "critChance", 0.1, "critMult", 2.5, "hitsAir", 1),
                "Jagt Eliten und Bosse quer über die Karte.", "Langsam gegen Schwärme.");

            d["sunlance"] = L("sunlance", "Sonnenlanze", "Legendär · Himmelsstrahl", "☀️", 0xffd070,
                "Fusion aus Adlerhorst und Kristalllaser: Bündelt Sonnenlicht und schlägt mit einem Strahl vom Himmel auf das stärkste Ziel in 9 Feldern ein. Richtet Nicht-Bosse unter 12 % hin.",
                "fire", "meteor", "strong", "meteorstorm",
                B("damage", 420, "range", 9, "cooldown", 4.5, "splash", 0.9, "meteorDelay", 0.5, "meteorCount", 1, "executePct", 0.12, "hitsAir", 1),
                "Größte Reichweite aller Türme.", "Lange Nachladezeit. Feuerresistente Bosse.");

            d["order"] = L("order", "Ordensburg", "Legendär · Ritterorden", "🏰", 0xd06048,
                "Fusion aus Wachkaserne und Totemschrein: 3 Ordensritter halten je 2 Gegner bis zu 14 s auf, kehren nach 7 s zurück und machen Getroffene spröde.",
                "physical", "barracks", "first", "overdrive",
                B("damage", 75, "range", 3, "cooldown", 0.8, "soldiers", 3, "soldierHold", 14, "soldierRespawn", 7, "soldierBlock", 2, "brittleDur", 1.5),
                "Ein Bollwerk auf der Lane für Kanonen und Frost.", "Flieger und Tunnelgräber laufen vorbei.");

            d["treasury"] = L("treasury", "Schatzkammer", "Legendär · Wirtschaft", "💰", 0xffd060,
                "Fusion aus Goldgräber und Handelskontor: fördert 40 Gold je Welle (Goldader +50 %), zahlt 6 % Zinsen bis 50 Gold (+0,6 je Welle) und Kopfgeld für Abschüsse in 3 Feldern.",
                null, "economy", "first", "goldrush",
                B("income", 40, "interest", 0.06, "interestCap", 50, "killBounty", 2, "eliteBounty", 15, "bountyRange", 3, "range", 0),
                "Lohnt sich früh – amortisiert sich nach wenigen Wellen.", "Kein Kampfwert.");
            d["treasury"].size = 2;

            d["deepdrill"] = L("deepdrill", "Tiefbohrer", "Legendär · Bergbau & Beben", "⛏️", 0xc89850,
                "Fusion aus Goldgräber und Bebenhammer: fördert 55 Gold je Welle (Goldader +50 %), und jeder Bohrschlag lässt Bodengegner in 2,4 Feldern taumeln – auch eingegrabene.",
                "physical", "pulse", "first", "goldrush",
                B("income", 55, "damage", 30, "range", 2.4, "cooldown", 2.5, "stunDur", 0.3, "hitsBurrowed", 1, "armorPierce", 0.2),
                "Wirtschaft und Kontrolle in einem.", "Trifft keine Flieger.");
            d["deepdrill"].size = 2;

            d["shieldnet"] = L("shieldnet", "Schildnetz", "Legendär · Schutzschirm", "🛡️", 0x80e0ff,
                "Fusion aus Schildwerk und Leitpylon: Ein Schutzschirm über 4,2 Feldern lädt je Turm in 8 s nach und wirft abgewehrte Fähigkeiten auf den Verursacher zurück (1,5 s Betäubung).",
                null, "economy", "first", null,
                B("range", 4.2, "wardCd", 8, "wardReflect", 1),
                "Schützt ganze Turmgruppen gegen Weltenstampfer und Aschenkönigin.", "Kein eigener Angriff.");

            // 4.1: Jeder Turm hat nur noch EINE feste Spezialisierung (Stufe 5). Die zweite Variante entfällt –
            // weniger Entscheidungen beim Ausbauen, und Fusionen brauchen keine Spezialisierung mehr.
            foreach (var t in d.Values)
            {
                if (t.specs == null || t.specs.Count < 2) continue;
                var keep = t.specs.FirstOrDefault((u) => SPEC_KEEP.Contains(u.id + ":" + t.id)) ?? t.specs[0];
                t.specs = new List<UpgradeDefinition> { keep };
            }
            return d;
        }

        /** 4.1: Die verbleibende Spezialisierung je Turm (Spezialisierungs-ID:Turm-ID); fehlt ein Eintrag, bleibt die erste. */
        static readonly HashSet<string> SPEC_KEEP = new HashSet<string>
        {
            "rangerA:ranger", "emberA:ember", "frostA:frost", "stormA:storm", "sporeA:spore", "ballistaB:ballista",
            "runeA:rune", "resonB:resonator", "mineA:goldmine", "pylonB:pylon", "cannonA:cannon", "riftA:riftbinder",
            "meteorB:meteor", "teslaA:tesla", "gravA:gravity", "dragonA:dragon", "laserB:laser", "totemA:totem",
            "mineA:minelayer", "skyA:skyguard", "sealB:seal", "acidA:acid", "quakeA:quake", "galeB:gale",
            "eyrieA:eyrie", "barA:barracks", "bankA:bank", "wardB:ward",
        };

        public static readonly List<FusionRecipe> FUSIONS = new List<FusionRecipe>
        {
            new FusionRecipe { id = "silencehall", a = "seal", b = "resonator", cost = 400 },
            new FusionRecipe { id = "banecircle", a = "seal", b = "totem", cost = 400 },
            new FusionRecipe { id = "acidmortar", a = "acid", b = "cannon", cost = 420 },
            new FusionRecipe { id = "plaguepot", a = "acid", b = "spore", cost = 380 },
            new FusionRecipe { id = "quakemaw", a = "quake", b = "gravity", cost = 440 },
            new FusionRecipe { id = "cyclone", a = "gale", b = "storm", cost = 420 },
            new FusionRecipe { id = "stormbattery", a = "gale", b = "skyguard", cost = 380 },
            new FusionRecipe { id = "falcon", a = "eyrie", b = "ballista", cost = 460 },
            new FusionRecipe { id = "sunlance", a = "eyrie", b = "laser", cost = 480 },
            new FusionRecipe { id = "order", a = "barracks", b = "totem", cost = 420 },
            new FusionRecipe { id = "treasury", a = "goldmine", b = "bank", cost = 360 },
            new FusionRecipe { id = "deepdrill", a = "goldmine", b = "quake", cost = 380 },
            new FusionRecipe { id = "shieldnet", a = "ward", b = "pylon", cost = 400 },
        };

        public static readonly List<string> LEGENDARY = new List<string> { "silencehall", "banecircle", "acidmortar", "plaguepot", "quakemaw", "cyclone", "stormbattery", "falcon", "sunlance", "order", "treasury", "deepdrill", "shieldnet" };
    }
}
