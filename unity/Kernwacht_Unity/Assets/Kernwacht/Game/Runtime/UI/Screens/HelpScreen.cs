// Übersetzung von ui/screens/help.ts – Hilfe & Kodex. Alle Werte stammen direkt aus der zentralen Konfiguration.

using System;
using System.Collections.Generic;
using System.Linq;
using Kernwacht.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Kernwacht.Game.UI
{
    public static class HelpScreen
    {
        static readonly (string id, string label)[] TABS =
        {
            ("controls", "Steuerung"), ("heroEndless", "Helden-Endlos"), ("towers", "Türme"), ("legend", "Fusion"), ("enemies", "Gegner"), ("bosses", "Bosse"),
            ("elements", "Elemente"), ("network", "Netzwerke"), ("mods", "Modifikatoren"), ("economy", "Wirtschaft"),
        };

        static string F(double x) => U.Fmt(x);
        static string R(double x) => U.Fmt(U.Round(x));

        static string resist(Dictionary<string, double> r)
        {
            var parts = r.Where((kv) => U.Truthy(kv.Value)).Select((kv) => $"{U.Get(TextUtil.DAMAGE_NAMES, kv.Key)} {(kv.Value > 0 ? "−" : "+")}{R(Math.Abs(kv.Value) * 100)} %").ToList();
            return parts.Count > 0 ? string.Join(", ", parts) : "keine";
        }

        static Action<Transform> tIc(string id) => (t) => Common.modelIcon(t, id, 40);
        static Action<Transform> eIc(string id) => (t) => Common.modelIcon(t, id, 40);
        static Action<Transform> bIc(string id) => (t) => Common.modelIcon(t, id, 40);

        static void P(Transform t, string text, string cls = "") => Common.para(t, text, cls);
        static void H3(Transform t, string text) => Common.h3(t, text);
        static void H2(Transform t, string text) => Common.h2(t, text);
        static void UL(Transform t, IEnumerable<string> items, string cls = "") => Common.bullets(t, items, cls);
        static string B(string s) => $"<b>{s}</b>";

        static void entry(Transform t, Action<Transform> icon, string summary, Action<Transform> body)
        {
            Common.details(t, Common.summaryText(summary, icon), body);
        }

        static void tabContent(Transform root, string tab)
        {
            switch (tab)
            {
                case "heroEndless":
                    {
                        var HE = HeroEndless.HERO_ENDLESS;
                        var SC = Scaling.SCALING;
                        var CH = Scaling.CHALLENGE;
                        var TL = Scaling.TOWER_LIMIT;
                        var LB = Scaling.LANE_BARRICADE;
                        var PA = Paragon.PARAGON;
                        var RR = Runes.RUNE_RULES;
                        var EC = Balance.ECONOMY;
                        P(root, $"Im Helden-Endlosmodus steuerst du einen frei beweglichen Helden zusätzlich zu deinen Türmen. Nach {F(HE.prepTime)} s Vorbereitung startet die erste Welle automatisch, danach folgt jede Welle nach {F(HE.transitionTime)} s (mit Leertaste früher). Pause (P/Esc) hält alles an.");
                        H3(root, "Steuerung");
                        UL(root, new[]
                        {
                            "Linksklick auf ein freies Feld oder Rechtsklick irgendwo: Held läuft dorthin (auch über die Gegnerbahn und durch Türme). Linksklick auf einen Turm wählt ihn aus.",
                            "A / S / D / W: Fähigkeiten. Ziel-Fähigkeiten: Taste drücken, dann Ziel anklicken (oder Taste erneut drücken). Esc/Rechtsklick bricht ab.",
                            "Umschalt + 1–4: Tränke der Schnellleiste, Umschalt + 5: Trank im Spezialslot.",
                            "Tab: Charakter (Ausrüstung, Werte, Skillbaum, Sammlung, Fortschritt) · X: Laden & Schmiede · Y: Kamera folgt · V: Draufsicht.",
                            "Enter oder Startknopf: nächste Welle sofort starten – im Kampf vorzeitig rufen, sobald die laufende Welle vollständig erschienen ist (+Gold je Gegner auf dem Feld, nicht im Bosskampf).",
                            "Z oder ⚓: Held festsetzen – Klicks bewegen ihn nicht mehr, er verfolgt keine Gegner (erneut drücken löst). Leertaste im Kampf oder „Mini“: Mini-Pause ohne Menü, Bauen und Aufwerten bleiben möglich.",
                            "O oder „Türme“: Turmübersicht mit Aufwerten, Fähigkeit und Info. Türme sind über das ganze Modell anklickbar, Gegner per Klick für Live-Werte (LP, Tempo, Resistenzen, Effekte). Schloss-Knopf: Kameradrehung sperren.",
                            "Alle Tasten lassen sich unter Einstellungen → Tastenbelegung ändern (die Angaben hier sind die Standardtasten).",
                        }, "small");
                        H3(root, "Späte Wellen (ab 3.0)");
                        UL(root, new[]
                        {
                            $"Ab Welle {F(SC.expFrom)} wachsen die Gegner-LP zusätzlich um {R((SC.expRate - 1) * 100)} % je Welle.",
                            $"Weltstufen: alle {F(SC.tierEvery)} Wellen eine Stufe – je Stufe +{R(SC.tierHp * 100)} % LP, mehr Rüstung und Tempo, aber +{R(SC.tierReward * 100)} % Kopfgeld.",
                            $"🏰 Bauplätze (Endlos): zu Beginn {F(TL.@base)} Türme (Barrikaden, Goldgräber sowie die Unterstützungstürme Leitpylon und Totemschrein zählen nicht), alle {F(TL.perWaves)} Wellen und nach jeder geschafften Bosswelle ein Platz mehr. Fokusbonus – weniger ist mehr: jeder freie Bauplatz macht ALLE Türme um {R(TL.focusPer * 100)} % stärker (höchstens +{R(TL.focusMax * 100)} %). Beispiel: 30 Plätze, 20 Türme gebaut → 10 frei → +20 % Schaden.",
                            $"🚧 Lane-Barrikade (Endlos, Classic): direkt auf die Lane setzen – Bodengegner werden darüber um 50 % langsamer. Kostenlos, ohne Turmlimit, mindestens {F(LB.minSpawnDist)} Felder vom Riss entfernt, höchstens {F(LB.maxCharges)} je {F(LB.window / 60)} Minuten Spielzeit.",
                            $"⚔ Challenge-Bosse: alle {F(CH.every)} Wellen bis Welle {F(CH.switchAt)}, danach alle {F(CH.everyLate)} Wellen. {F(CH.hpMul).Replace('.', ',')}× LP (ab Welle 150 je 100 Wellen noch einmal so viel, z. B. Welle 250 ×7), +{R(CH.resist * 100)} % Resistenzen, Kontrolleffekte nur zu {R(CH.ccMul * 100)} %, Schildschub alle {F(CH.shieldEvery)} s, {F(CH.summonCount)} Elite-Begleiter alle {F(CH.summonEvery)} s, Raserei nach {F(CH.enrageAfter)} s. Ein Durchbruch kostet mindestens {F(CH.leakMin)} bzw. {R(CH.leakPct * 100)} % der Kernenergie; der Sieg bringt {F(CH.rewardMul)}× Kopfgeld, Relikte und eine Rune.",
                            $"Spezialisierter Turmkauf ab Welle {F(EconomySystem.PRESPEC.from)}: Ein Klick auf einen Turm in der Bauleiste öffnet die Auswahl Normal / Spezialisierung A / B. Spezialisierte Türme werden direkt auf Höchststufe gebaut und kosten Grundpreis + alle Stufen + Spezialisierung, +{R((EconomySystem.PRESPEC.premium - 1) * 100)} % Aufpreis.",
                            $"Wellen-Affixe ab Welle {F(SC.affixFrom)} (1, ab {F(SC.affixFrom + SC.affixStep)} zwei, ab {F(SC.affixFrom + 2 * SC.affixStep)} drei): {string.Join(" · ", Scaling.AFFIX_ORDER.Select((a) => $"{Scaling.AFFIXES[a].icon} {Scaling.AFFIXES[a].name} – {Scaling.AFFIXES[a].description}"))}. Sie stehen vor jeder Welle in der Wellenvorschau.",
                            $"Ab Welle {F(SC.pressureFrom)}: Elitegegner in jeder Welle, Mini-Bosse alle {F(SC.miniBossEveryLate)} Wellen, durchbrechende Bosse kosten doppelt Kernenergie.",
                            $"Ab Welle {F(SC.overrunFrom)} rückt nach {F(SC.overrunAfter)} s Kampf die nächste Welle automatisch nach (Countdown in der Wellenleiste).",
                            "Turm-Meisterschaft: Jeder Turmtyp sammelt dauerhaft Abschüsse – Rang r ab 50·r² Abschüssen, je Rang +1 % Schaden (max. Rang 20).",
                            "Meilensteine W100/150/200/250/300 geben dauerhaft Paragonpunkte (insgesamt 15) und Startgold.",
                        }, "small");
                        H3(root, "Heldenstufe & Paragon");
                        P(root, $"Heldenstufe und Skillbaum jedes Helden bleiben dauerhaft erhalten (höchstens Stufe {F(PA.heroMaxLevel)}; ab Stufe {F(PA.softcapLevel)} wachsen die Grundwerte nur noch halb so schnell). Ab Stufe 50 gibt es alle 5 Stufen einen Vorab-Paragonpunkt (10 bis Stufe 95). Ab Stufe {F(PA.heroMaxLevel)} fließt jede Erfahrung ins Paragon – gemeinsam für alle Helden, bis Paragon {F(PA.maxLevel)}. Jede Paragonstufe gibt einen Punkt für einen von drei Bäumen: {string.Join(" · ", Paragon.PARAGON_TREES.Select((t) => $"{t.icon} {t.name} ({string.Join(", ", Paragon.PARAGON_NODES.Where((n) => n.tree == t.id).Select((n) => n.name))})"))}. Vergeben unter Charakter → Paragon, kostenlos zurücksetzbar in der Bauphase.", "small");
                        H3(root, "Runen");
                        P(root, $"Runen sind nur findbar – Runensplitter ab Welle {F(RR.splinterFrom)}, volle Runen ab Welle {F(RR.fromWave)} (Gegner, vor allem Elite, Mini-Bosse und Bosse, sowie Wellenabschluss). Drei Qualitäten ({string.Join(", ", Runes.RUNE_TIERS.Select((t) => $"{t.name} ×{F(t.mul).Replace('.', ',')}"))}). Runensockel gehören der Turmsorte (auch jeder Fusion): 1 ab Welle 100, 2 ab Welle 250, 3 ab Welle 400, 4 ab Welle 500, legendäre Fusionen je +1. Eine gesockelte Rune wirkt auf alle Türme dieser Sorte, auch auf später gebaute; ein Wechsel gilt sofort für alle. Im Turmpanel zeigt ein Klick auf eine Rune ihre Wirkung, ein Doppelklick sockelt sie bzw. nimmt sie wieder heraus (kostenlos, auch per Knopf in der Infobox). Beim Verkauf bleiben die Runen bei der Sorte. {string.Join(" · ", Runes.RUNE_ORDER.Select((r) => $"{Runes.RUNES[r].icon} {Runes.RUNES[r].name}: {Runes.RUNES[r].description}"))}.", "small");
                        H3(root, "Abbauen");
                        P(root, $"Bäume, Felsen, Kristalle und andere Hindernisse (nicht Lava und Wolken) anklicken und „Abbauen“ wählen. Der Held läuft hin und baut so lange ab, wie eine Welle im Schnitt dauert ({F(HE.mineTimeMin)}–{F(HE.mineTimeMax)} s, anfangs {F(HE.mineTimeDefault)} s). Der Abbau lässt sich nicht unterbrechen: Der Held bewegt sich nicht, kämpft nicht und wirkt keine Fähigkeiten, nimmt aber auch keinen Schaden. Ausbeute: zufällig Gold (steigt mit der Welle) und meist Metall, oft Kristall, selten Essenz oder ein Relikt. Danach ist das Feld frei und bebaubar.", "small");
                        H3(root, "Spezialfelder");
                        P(root, $"Goldadern (gelb): Goldgräber zahlen +{R((EC.veinMul - 1) * 100)} % und zusätzlich {F(EC.veinFlat)} + {F(EC.veinFlatPerWave)} × Welle Gold. Kristallvorkommen (blau, nur Helden-Endlos, {F(HE.depositCount[0])}–{F(HE.depositCount[1])} je Karte): ein Goldgräber darauf fördert jede Welle zusätzlich Kristall. Beim Platzieren eines Turms zeigt die Bauleiste das Feld und alle möglichen Fusionen samt Partnern in Reichweite.", "small");
                        H3(root, "Helden");
                        P(root, "Zwölf Helden sind spielbar – weitere werden über die Bestwelle freigeschaltet. Jeder Skillbaum enthält den Knoten „Kernwall“: +3 Kernenergie je Rang (bis +15), also mehr Gegner, die den Kern erreichen dürfen.", "small");
                        UL(root, Champions.CHAMPION_ORDER.Select((id) =>
                        {
                            var c = Champions.CHAMPIONS[id];
                            string lockTxt = Champions.championUnlocked(id, new List<string>(), 0, new List<string>()) ? "" : $" – {Champions.championUnlockText(id)}";
                            return $"{B($"{c.name} ({c.role})")}{lockTxt}: {c.description} Fähigkeiten: {string.Join(", ", c.skills.Select((s) => $"{s.name} ({F(s.cooldown)} s, {F(s.mana)} Mana)"))}.";
                        }), "small");
                        H3(root, "Beute, Ressourcen, Herstellung");
                        P(root, $"Gegner lassen Erfahrung fallen, die der Held im Aufsammelradius einsammelt. Dazu Ressourcen ({string.Join(", ", HeroEndless.RESOURCE_ORDER.Select((r) => $"{HeroEndless.RESOURCES[r].icon} {HeroEndless.RESOURCES[r].name}"))}), Tränke, Fragmente und selten ganze Items. Elite, Mini-Bosse (ab Welle {F(HE.miniBossFrom)} alle {F(HE.miniBossEvery)} Wellen) und Bosse haben bessere Beute. Jede Welle bringt zusätzlich Ressourcen.", "small");
                        P(root, $"Fragmente ergeben in der Schmiede ein Item ({string.Join(", ", Items.RARITY.Values.Select((r) => $"{r.name}: {F(r.fragments)}"))}). Bereits freigeschaltete Items werden nie erneut als identischer Drop vergeben; überzählige Fragmente werden zu Essenz. Items lassen sich bis +3 verstärken und kombinieren (legendär).", "small");
                        P(root, $"{Items.ITEMS.Count} Items in 6 Slots, {Items.POTIONS.Count} Tränke (stapelbar). Laden, Schmiede und Verkauf nur in Reichweite des Ladens nahe dem Kern.", "small");
                        H3(root, "Türme nach und nach");
                        {
                            var acc = new List<string>();
                            var entries = PassiveTree.TOWER_UNLOCKS.Where((kv) => kv.Value > 0 && Towers.TOWERS[kv.Key].legendary != true).ToList();
                            entries = U.StableSort(entries, (a, b) => a.Value - b.Value);
                            foreach (var kv in entries)
                            {
                                string last = acc.Count > 0 ? acc[acc.Count - 1] : null;
                                string name = Towers.TOWERS[kv.Key].name;
                                if (last != null && last.StartsWith($"W{F(kv.Value)}:", StringComparison.Ordinal)) acc[acc.Count - 1] = $"{last}, {name}";
                                else acc.Add($"W{F(kv.Value)}: {name}");
                            }
                            P(root, $"Im Helden-Endlosmodus kommen Türme mit den Wellen hinzu: {string.Join(" · ", acc)}. Luftabwehr kommt vor den ersten Fliegern, Detektoren vor den Schattenschleichern, Fusionspartner paarweise. Turmfähigkeiten lösen ab Stufe 2 automatisch aus.", "small");
                        }
                        H3(root, "Passiver Baum");
                        var PS = PassiveTree.PASSIVE;
                        P(root, $"1 Punkt je {F(PS.wavesPerPoint)} Bestwellen, höchstens {F(PS.maxPoints)} (Bestwelle {F(PS.maxPoints * PS.wavesPerPoint)}). {PassiveTree.PASSIVE_NODES.Count} Knoten in {PS.tierGate.Length} Stufen, insgesamt {F(PassiveTree.PASSIVE_TOTAL_RANKS)} Ränge; Punkte werden im Startbildschirm vergeben und können kostenlos zurückgesetzt werden. Wirkt automatisch: {string.Join(", ", PassiveTree.PASSIVE_NODES.Select((n) => n.name))}.", "small");
                        H3(root, "Wellenprofile");
                        P(root, string.Join(" · ", HeroEndless.WAVE_PROFILES.Select((p) => p.name)), "small");
                        H3(root, "Dauerhafter Fortschritt");
                        UL(root, HeroEndless.PERKS.Select((p) => $"{p.icon} {p.name} (Welle {F(p.wave)}): {p.description}"), "small");
                        P(root, "Meilensteine: " + string.Join(" · ", HeroEndless.MILESTONES.Select((m) => $"W{F(m.wave)} {m.name}")), "small");
                        P(root, "Freischaltungen eines Laufs werden bei Partieende gespeichert (auch bei Neustart oder wenn ein fortsetzbarer Lauf durch einen neuen ersetzt wird). Die Bestwelle wird nach jeder Welle gespeichert.", "small muted");
                        break;
                    }
                case "heroes":
                    {
                        var HR = Heroes.HERO_RULES;
                        P(root, $"Helden werden mit Rissenergie (⚡) auf einer Wegzelle eingesetzt (Taste Z/X oder Porträt antippen). Rissenergie (max. {F(HR.maxEnergy)}) erhältst du für Abschüsse, Elitegegner, Bosse, Elementarreaktionen und überstandene Wellen. Ein Einsatz dauert eine begrenzte Zeit, danach erholt sich der Held {F(HR.cooldown)} s; fällt er, dauert es {F(HR.downCooldown)} s. Rückzug zum Kern erstattet {R(HR.retreatRefund * 100)} % der Abklingzeit.");
                        P(root, $"Helden sammeln Erfahrung durch Abschüsse in ihrer Nähe und steigen bis Stufe {F(HR.maxLevel)} auf. Jede Stufe bietet eine Wahl zwischen zwei Verbesserungen. Zwei Fähigkeiten (A/S) und eine Ultimate (D), die sich im Kampf auflädt. Verhalten: Halten, Vorrücken, Rückzug (V wechselt).");
                        var codex = Ui.Column(root, 6).transform;
                        foreach (var id in Heroes.HERO_ORDER)
                        {
                            var d = Heroes.HEROES[id];
                            var m = Campaign.CAMPAIGN.FirstOrDefault((x) => x.id == d.unlock);
                            entry(codex, tIc(id), $"{d.name} – {d.title} · {d.role}{(d.support ? " (Unterstützer)" : "")} · {F(d.cost)} ⚡", (t) =>
                            {
                                P(t, d.description);
                                P(t, $"LP {F(d.@base.maxHp)} · Schaden {F(d.@base.damage)} · Reichweite {F(d.@base.range)}{(U.Truthy(d.@base.auraRadius) ? $" · Aura {F(d.@base.auraRadius)} Felder" : "")}{(U.Truthy(d.@base.blockCount) ? $" · hält {F(d.@base.blockCount)} Gegner fest" : "")} · Einsatz {F(d.@base.activeTime)} s", "small");
                                var lines = d.abilities.Select((a, i) => $"{B($"{(i != 0 ? "S" : "A")} · {a.name}")} ({F(a.cooldown)} s): {a.description}").ToList();
                                lines.Add($"{B($"D · {d.ultimate.name}")}: {d.ultimate.description}");
                                UL(t, lines, "small");
                                P(t, B("Perks: ") + string.Join(" · ", d.perks.Select((pair, i) => $"St. {i + 2}: {pair[0].name} / {pair[1].name}")), "small");
                                P(t, d.unlock == "start" ? "Von Beginn an verfügbar." : $"Freischaltung: Mission {(m != null ? $"{F(m.index + 1)} „{m.name}“" : d.unlock)} abschließen.", "small muted");
                            });
                        }
                        H2(root, "Duo-Synergien");
                        UL(root, Heroes.DUOS.Select((d) => $"{B(d.name)} ({Heroes.HEROES[d.a].name.Split(' ').Last()} + {Heroes.HEROES[d.b].name.Split(' ').Last()}, höchstens {F(HR.duoRadius)} Felder entfernt): {d.description}"));
                        break;
                    }
                case "legend":
                    {
                        P(root, $"Zwei voll ausgebaute (Stufe 4 und spezialisiert) passende Türme, die höchstens {F(Towers.FUSION_RANGE)} Felder auseinander stehen, lassen sich zu einem legendären Turm verschmelzen. Der zweite Turm geht dabei in den ersten über. Legendäre Türme können nicht direkt gebaut werden.");
                        var codex = Ui.Column(root, 6).transform;
                        foreach (var f in Towers.FUSIONS)
                        {
                            var d = Towers.TOWERS[f.id];
                            entry(codex, tIc(f.id), $"{d.name} = {Towers.TOWERS[f.a].name} + {Towers.TOWERS[f.b].name} · {F(f.cost)} Gold", (t) =>
                            {
                                P(t, d.description);
                                Common.statsTable(t, TextUtil.statLines(TextUtil.baseStatsOf(f.id), d));
                                if (!string.IsNullOrEmpty(d.active)) P(t, $"{B($"Aktiv: {Towers.TOWER_ACTIVES[d.active].name}")} – {Towers.TOWER_ACTIVES[d.active].description}", "small");
                            });
                        }
                        H2(root, "Aktive Turmfähigkeiten");
                        P(root, $"Ab Stufe {F(Towers.ACTIVE_MIN_LEVEL)} lösen viele Türme während einer Welle automatisch eine Fähigkeit aus, sobald die Abklingzeit abgelaufen ist und Gegner in Reichweite sind. Mit Taste C oder dem Knopf in den Turmdetails geht es auch von Hand.", "small");
                        var all = Towers.TOWER_ORDER.Concat(Towers.LEGENDARY_ORDER).ToList();
                        UL(root, Towers.TOWER_ACTIVES.Values.Select((a) => $"{B(a.name)} ({F(a.cooldown)} s): {a.description} – {string.Join(", ", all.Where((id) => Towers.TOWERS[id].active == a.id).Select((id) => Towers.TOWERS[id].name))}"), "small");
                        break;
                    }
                case "towers":
                    {
                        var codex = Ui.Column(root, 6).transform;
                        foreach (var id in Towers.TOWER_ORDER)
                        {
                            var d = Towers.TOWERS[id];
                            entry(codex, tIc(id), $"{d.name} – {d.role} · {F(d.cost)} Gold · Taste {TextUtil.keyLabel(d.hotkey)}", (t) =>
                            {
                                P(t, d.description);
                                if (!string.IsNullOrEmpty(d.active)) P(t, $"{B($"Aktiv (ab Stufe {F(Towers.ACTIVE_MIN_LEVEL)}): {Towers.TOWER_ACTIVES[d.active].name}")} – {Towers.TOWER_ACTIVES[d.active].description}", "small");
                                if (d.detector == true) P(t, "Detektor: enttarnt getarnte Gegner in Reichweite.", "small good");
                                Common.statsTable(t, TextUtil.statLines(TextUtil.baseStatsOf(id), d));
                                P(t, B("Stufen: ") + string.Join(" · ", d.upgrades.Select((u, i) => $"{i + 2}. {u.name} ({F(u.cost)}): {u.description}")), "small");
                                P(t, B("Spezialisierungen: ") + string.Join(" · ", d.specs.Select((s, i) => $"{(i != 0 ? "Ⓑ" : "Ⓐ")} {s.name} ({F(s.cost)}): {s.description}")), "small");
                                P(t, "+ " + d.synergies, "small");
                                P(t, "× " + d.counters, "small");
                            });
                        }
                        var bar = Towers.TOWERS["barricade"];
                        entry(codex, tIc("barricade"), $"{bar.name} – nur Mazing · {F(bar.cost)} Gold · Taste B", (t) => P(t, bar.description));
                        break;
                    }
                case "enemies":
                    {
                        var codex = Ui.Column(root, 6).transform;
                        foreach (var id in Enemies.WAVE_ENEMIES)
                        {
                            var d = Enemies.ENEMIES[id];
                            entry(codex, eIc(id), $"{d.name} – {d.role} (ab Welle {F(d.minWave)})", (t) =>
                            {
                                P(t, d.description);
                                P(t, $"LP {F(d.hp)} · Tempo {F(d.speed)} · Kopfgeld {F(d.reward)} · Resistenzen: {resist(d.resist)}{(d.flying ? " · fliegt" : "")}{(d.stealth == true ? " · getarnt (Detektoren: Runenobelisk, Resonator, Totem)" : "")}{(d.burrow == true ? " · gräbt sich ein" : "")}{(U.Truthy(d.evasion) ? $" · weicht {R(d.evasion.Value * 100)} % der Einzeltreffer aus" : "")}", "small");
                                if (d.abilities.Count > 0) P(t, string.Join(" ", d.abilities.Select((a) => $"{Enemies.ABILITIES[a].name}: {Enemies.ABILITIES[a].description}")), "small");
                                P(t, "Konter: " + d.counter, "small");
                            });
                        }
                        entry(codex, null, "Elitevarianten", (t) => P(t, "Elitegegner (goldener Ring) haben dreifache Lebenspunkte, +10 % Resistenzen und wirken „Erdstoß“: Stehen mindestens drei Türme dicht beieinander, kündigen sie einen Stoß an (Ring), der diese Türme 3 s betäubt. Resonatoren unterbrechen das, verteilte Türme verhindern es."));
                        entry(codex, null, "Mutationen", (t) =>
                        {
                            P(t, "Ab Welle 8 können Elitegegner mutiert sein (farbiger Ring am Boden, orange markiert in der Wellenvorschau):");
                            UL(t, Enemies.MUTATION_IDS.Select((m) => $"{Enemies.MUTATIONS[m].icon} {B(Enemies.MUTATIONS[m].name)}: {Enemies.MUTATIONS[m].description}"));
                        });
                        entry(codex, null, "Adaptive Schilde", (t) => P(t, "Schildträger passen ihren Schild an Schadensarten an, die sie wiederholt treffen (sichtbar als farbige Würfel über dem Lebensbalken, max. −45 %). Gemischte Schadensarten umgehen das. Die Anpassung ist begrenzt und gilt nur für den Schild."));
                        break;
                    }
                case "bosses":
                    {
                        var codex = Ui.Column(root, 8).transform;
                        foreach (var b in Enemies.BOSSES.Values)
                        {
                            var c = Common.card(codex);
                            Common.iconRow(c, bIc(b.id), b.name, Common.H3, FontStyle.Bold);
                            P(c, b.title, "muted");
                            P(c, b.description);
                            UL(c, b.phases.Select((p) => B(p.name) + (p.abilities.Count > 0 ? $": {string.Join(" ", p.abilities.Select((a) => $"{Enemies.ABILITIES[a].name} – {Enemies.ABILITIES[a].description}"))}" : "")));
                            P(c, $"Resistenzen: {resist(b.resist)} · Belohnung {F(b.reward)} Gold", "small");
                            P(c, "Konter: " + b.counter);
                        }
                        P(root, "Bossfähigkeiten werden mit Ringen und Warnungen angekündigt. Schall- und Frostschaden senken die Standfestigkeit; bricht sie, wird die aktuelle Fähigkeit abgebrochen und der Boss taumelt.", "small muted");
                        break;
                    }
                case "elements":
                    {
                        H2(root, "Reaktionen");
                        UL(root, Elements.REACTIONS.Select((r) => $"{B(r.name)} – {r.description}{(U.Truthy(r.cooldown) ? $" (Abklingzeit {F(r.cooldown)} s je Gegner)" : "")}"));
                        P(root, "Reaktionsschaden löst keine weiteren Reaktionen aus. Kälte ist auf 75 % Verlangsamung begrenzt, Markierungen auf 60 % Resistenzsenkung; nach dem Einfrieren folgt 3 s Immunität.", "small muted");
                        H2(root, "Zustände");
                        UL(root, Elements.STATUS_DEFS.Values.Where((s) => s.id != "weather" && s.id != "freezeImmune").Select((s) => $"{s.icon} {s.name}: {s.description}"), "small");
                        break;
                    }
                case "network":
                    {
                        P(root, "Leitpylone verbinden Türme in ihrer Verbindungsreichweite (Linien). Verbundene Türme erhalten den Netzwerkbonus des stärksten Pylons im Netz als Schaden und Angriffstempo. Ein Turm kann maximal zwei Pylonverbindungen haben; Pylone haben eine begrenzte Anzahl Verbindungen.");
                        P(root, "Netzwerke werden nur bei Bau, Verkauf, Ausbau oder Störung neu berechnet. Funkenfresser und der Nullhüter stören Türme (rotes ✖) – gestörte Türme fallen aus dem Netz.");
                        H2(root, "Resonanzen");
                        var lines = Elements.RESONANCES.Select((r) => $"{B(r.name)} ({string.Join(" + ", r.requires.Select((t) => Towers.TOWERS[t].name))}): {r.description}").ToList();
                        lines.Add($"{B("Heldenchor")} (Runenbardin in Aurareichweite eines Netzes mit Feuer-, Frost- und Blitzturm): alle Türme dieses Netzes +20 % Schaden.");
                        UL(root, lines);
                        P(root, "Verbindungslinien leuchten golden, wenn eine Resonanz aktiv ist.", "small muted");
                        break;
                    }
                case "mods":
                    {
                        P(root, "Nach bestimmten Wellen wählst du einen von drei Riss-Modifikatoren (oder +30 Gold). Unvereinbare Kombinationen werden nie gemeinsam angeboten; mindestens ein Angebot ist sofort nutzbar.");
                        UL(root, Modifiers.MODIFIER_IDS.Select((id) => $"{Modifiers.MODIFIERS[id].icon} {B(Modifiers.MODIFIERS[id].name)}: {Modifiers.MODIFIERS[id].description}"));
                        break;
                    }
                case "economy":
                    {
                        var EC = Balance.ECONOMY;
                        UL(root, new[]
                        {
                            $"Wellenbonus: {F(EC.waveBonusBase)} + {F(EC.waveBonusPerWave)} × Welle Gold (Bosswellen +{F(EC.bossWaveBonus)}).",
                            "Abschüsse geben Kopfgeld, das mit der Welle leicht steigt.",
                            $"Verkauf: {R(Towers.SELL_REFUND * 100)} % der Investition. In derselben Bauphase gebaute Türme werden voll erstattet (Rückgängig) – das ermöglicht keine Geldvermehrung.",
                            "Goldgräber zahlen am Ende jeder Welle, frühestens ab der Welle nach dem Bau. Auf Goldadern +50 % und ein fester Zusatzbetrag, der mit der Welle wächst. Amortisation ca. 5 Wellen auf Goldadern, sonst ca. 8 Wellen – zu viele Minen schwächen die frühe Verteidigung.",
                            $"Anhöhen: +{F(EC.highgroundRangeAdd)} Reichweite. Leylinien: +{R((EC.leyDamageMul - 1) * 100)} % Schaden.",
                        });
                        break;
                    }
                default:
                    {
                        H2(root, "Ziel");
                        P(root, "Halte die Gegnerwellen auf, bevor sie den Kern erreichen. Baue Türme mit Gold, werte sie auf und nutze Elementarreaktionen, Netzwerke und Modifikatoren.");
                        H2(root, "Maus & Tastatur");
                        UL(root, new[]
                        {
                            "Turm wählen: Klick in der Turmleiste oder Tasten 1–0, −, = (Barrikade: B).",
                            "Bauen: Klick auf ein freies Feld (grün = erlaubt, rot = nicht erlaubt). Umschalt+Klick baut mehrere.",
                            "Turm anklicken: Details, Aufwerten (U), Zielwahl (T), Verkaufen (Entf, zweimal).",
                            "Doppelklick auf einen Turm wählt alle Türme dieser Sorte (gold markiert) – „Alle aufwerten“ baut sie gemeinsam aus. „Alle ▲“ oben bzw. in der Turmübersicht wertet alle Türme bis Stufe 4 auf, solange das Gold reicht (günstigste zuerst, Spezialisierung wählst du selbst).",
                            "Goldgräber belegen 2×2 Felder (das gewählte Feld ist oben links). Die Turmübersicht blendet voll ausgebaute Türme aus (umschaltbar).",
                            "Abbrechen: Rechtsklick oder Esc. Pause mit Menü: P oder Esc. Mini-Pause ohne Menü (weiter bauen/aufwerten): Leertaste im Kampf oder „Mini“. Tempo (1×/2×/3×/5×/8×): F. Welle starten: Leertaste. Das Spiel pausiert nicht automatisch beim Tab- oder Fensterwechsel.",
                            "Kamera: Ziehen = verschieben, Mausrad = zoomen, Q/E = um 90° drehen. R = alle Reichweiten, G = Raster, H = Hilfe.",
                            "Freie Kamera: mittlere Maustaste (oder Strg/Alt + linke Maustaste) ziehen – links/rechts dreht beliebig, hoch/runter neigt. Tasten , und . drehen um 15°, Bild↑/Bild↓ neigen, Pos1 bzw. ⟲ setzt den Blickwinkel zurück. Touch: mit zwei Fingern drehen.",
                            "Turmfähigkeit des ausgewählten Turms: C. Fusion über die Turmdetails.",
                            "Minikarte (oben rechts, bei großen Karten automatisch): anklicken, um dorthin zu springen.",
                            "Alle Tasten sind unter Einstellungen → Tastenbelegung frei belegbar; doppelt vergebene Tasten werden getauscht, „Standard wiederherstellen“ setzt alles zurück.",
                        });
                        H2(root, "Touch");
                        UL(root, new[]
                        {
                            "Turm in der Leiste antippen, dann ein Feld antippen: Vorschau mit Reichweite (und Route im Mazing) erscheint.",
                            "Bestätigen mit „Hier bauen“ in der Seitenleiste oder erneutem Tippen auf dasselbe Feld.",
                            "Ein Finger ziehen = verschieben, zwei Finger = zoomen. Kamerasteuerung auch über die Knöpfe unten rechts.",
                            "Querformat wird empfohlen; im Hochformat bleibt das Spiel bedienbar.",
                        });
                        H2(root, "Simulation");
                        P(root, $"Die Simulation läuft mit festem Takt ({F(Balance.SIM.tickRate)} Hz) unabhängig von der Bildrate. Schnellvorlauf erhöht nur die Anzahl der Takte. Bei verstecktem Tab pausiert der Kampf automatisch.", "small muted");
                        break;
                    }
            }
        }

        /// <summary>Hilfe-Inhalt mit Reitern (auch im Overlay). TS: helpContent(initial, icons) – Symbole kommen aus Icons.</summary>
        public static RectTransform helpContent(Transform parent, string initial)
        {
            var root = Ui.Column(parent, 8);
            root.gameObject.name = "Help";
            var tabs = Ui.Row(root.transform, 5, TextAnchor.MiddleLeft, false);
            var body = Ui.Column(root.transform, 6);
            body.gameObject.name = "HelpBody";
            var bodyRt = body.GetComponent<RectTransform>();
            var buttons = new List<(string id, Button b)>();
            void show(string tab)
            {
                foreach (var (id, b) in buttons) Ui.SetKind(b, id == tab ? Ui.BtnKind.On : Ui.BtnKind.Normal);
                Common.clear(bodyRt);
                tabContent(bodyRt, tab);
            }
            foreach (var (id, label) in TABS)
            {
                string tid = id;
                var b = Common.btn(tabs.transform, label, () => show(tid), Ui.BtnKind.Normal, null, true);
                buttons.Add((tid, b));
            }
            show(initial ?? "controls");
            return root.GetComponent<RectTransform>();
        }

        /// <summary>Bildschirm „Hilfe & Kodex“ (unter app.screenLayer).</summary>
        public static RectTransform renderHelp(GameApp app, string tab, string back)
        {
            return Common.shell(app, "Hilfe & Kodex", back ?? "menu", (p) => helpContent(p, tab ?? "controls"));
        }

        /// <summary>Alias (Auftragsbeschreibung).</summary>
        public static RectTransform helpScreen(GameApp app, string tab = "controls", string back = "menu") => renderHelp(app, tab, back);
    }
}
