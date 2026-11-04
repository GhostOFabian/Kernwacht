// Übersetzung von ui/screens/records.ts – Bestwerte & Erfolge.

using System.Linq;
using Kernwacht.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Kernwacht.Game.UI
{
    public static class RecordsScreen
    {
        public static RectTransform renderRecords(GameApp app)
        {
            var p = app.save.progress;
            int got = p.achievements.Count;
            return Common.shell(app, "Bestwerte & Erfolge", "menu",
                (t) => Common.para(t, "Alle Werte werden nur auf diesem Gerät gespeichert. Es gibt bewusst keine Online-Rangliste.", "muted"),
                (t) => Common.h2(t, $"Erfolge ({got}/{Achievements.ACHIEVEMENTS.Count})"),
                (t) => Common.para(t, "Erfolge schalten ausschließlich kosmetische Turm-Skins frei (Einstellungen → Turm-Skin).", "small muted"),
                (t) =>
                {
                    var list = Achievements.ACHIEVEMENTS;
                    Common.gridRows(t, list.Count, 4, (row, i) =>
                    {
                        var a = list[i];
                        bool done = p.achievements.Contains(a.id);
                        var skin = Achievements.SKINS.FirstOrDefault((k) => k.requires == a.id);
                        var img = Ui.Panel(row, "Ach", done ? new Color(1f, 216 / 255f, 74 / 255f, 0.12f) : new Color(40 / 255f, 30 / 255f, 64 / 255f, 0.4f), true, Theme.Round);
                        img.GetComponent<Outline>().effectColor = done ? new Color(1f, 216 / 255f, 74 / 255f, 0.6f) : Theme.Line;
                        var h = img.gameObject.AddComponent<HorizontalLayoutGroup>();
                        h.padding = new RectOffset(12, 12, 8, 8);
                        h.spacing = 10;
                        h.childAlignment = TextAnchor.MiddleLeft;
                        h.childControlWidth = true; h.childControlHeight = true;
                        h.childForceExpandWidth = false; h.childForceExpandHeight = false;
                        // .ach-ic: Emoji → Pokal-Symbol (grau/halbtransparent, wenn offen)
                        Common.image(img.transform, Icons.Get("trophy"), 30, done ? Theme.Gold : new Color(0.6f, 0.6f, 0.6f, 0.5f));
                        var col = Ui.Column(img.transform, 2);
                        Ui.Size(col, -1, -1, 1);
                        Common.para(col.transform, $"<b>{a.name}{(done ? " <color=#6be38a>√</color>" : "")}</b>");
                        Common.para(col.transform, a.description, "small muted");
                        if (skin != null) Common.para(col.transform, $"Belohnung: Skin „{skin.name}“", "tiny good");
                        return img.rectTransform;
                    }, 8);
                },
                (t) =>
                {
                    var cols = Common.columns(t, 12, 1, 1);
                    var c1 = Common.card(cols[0]);
                    Common.h3(c1, "Allgemein");
                    Common.para(c1, $"Partien: {U.Fmt(p.totalRuns)} · Siege: {U.Fmt(p.totalVictories)}");
                    Common.para(c1, $"Besiegte Gegner: {TextUtil.fmt(p.totalKills)} · Bosse: {U.Fmt(p.totalBossKills)}");
                    string bosses = string.Join(", ", p.bossesDefeated.Select((b) => Enemies.BOSSES.TryGetValue(b, out var bd) && bd != null ? bd.name : b));
                    Common.para(c1, $"Bestiarium: {p.bossesDefeated.Count}/{Enemies.BOSSES.Count} Bossarten – {(bosses.Length > 0 ? bosses : "noch keine")}", "small");
                    var c2 = Common.card(cols[1]);
                    Common.h3(c2, "Helden-Endlos");
                    Common.para(c2, $"Bestwelle: {U.Fmt(p.heroMeta.bestWave)} · Läufe: {U.Fmt(p.heroMeta.runs)}");
                    Common.para(c2, $"Heldenstufe: {U.Fmt(p.heroMeta.heroLevel.level)} · Paragon: {U.Fmt(p.heroMeta.paragon.level)}");
                },
                (t) => Common.h2(t, "Beste Partien"),
                (t) =>
                {
                    if (p.bestScores.Count == 0)
                    {
                        Common.para(t, "Noch keine Partie abgeschlossen.", "muted");
                        return;
                    }
                    var table = Ui.Column(t, 2);
                    float[] w = { 50, 130, 230, 90, 160, 190, -1 };
                    void cellOf(Transform row, int c, string text, bool head)
                    {
                        var l = Common.label(row, text, head ? Common.SMALL : 16, head ? Theme.Muted : Theme.Text, head ? FontStyle.Bold : FontStyle.Normal, TextAnchor.MiddleLeft);
                        if (w[c] > 0) Ui.Size(l, w[c]); else Ui.Size(l, -1, -1, 1);
                    }
                    var hr = Ui.Row(table.transform, 8, TextAnchor.MiddleLeft, false);
                    string[] heads = { "#", "Punkte", "Modus", "Wellen", "Ergebnis", "Seed", "Datum" };
                    for (int c = 0; c < heads.Length; c++) cellOf(hr.transform, c, heads[c], true);
                    var line = Ui.Panel(table.transform, "Line", Theme.Line, false, null);
                    Ui.Size(line, -1, 1);
                    int n = 0;
                    foreach (var b in p.bestScores.Take(20))
                    {
                        n++;
                        var rowBg = Ui.Panel(table.transform, "Row", n % 2 == 0 ? new Color(1, 1, 1, 0.03f) : new Color(0, 0, 0, 0), false, Theme.RoundSmall);
                        var r = rowBg.gameObject.AddComponent<HorizontalLayoutGroup>();
                        r.spacing = 8; r.padding = new RectOffset(0, 0, 2, 2);
                        r.childAlignment = TextAnchor.MiddleLeft;
                        r.childControlWidth = true; r.childControlHeight = true;
                        r.childForceExpandWidth = false; r.childForceExpandHeight = false;
                        cellOf(r.transform, 0, n.ToString(), false);
                        cellOf(r.transform, 1, TextUtil.fmt(b.score), false);
                        cellOf(r.transform, 2, TextUtil.modeName(b.mode), false);
                        cellOf(r.transform, 3, U.Fmt(b.waves), false);
                        cellOf(r.transform, 4, b.victory ? "<color=#6be38a>√ Sieg</color>" : "<color=#ff6b6b>× Niederlage</color>", false);
                        string seed = b.seed;
                        var sb = Common.btn(r.transform, seed, () => Common.copyText(app, seed), Ui.BtnKind.Normal, null, true);
                        Ui.Size(sb, w[5]);
                        Ui.Tip(sb, () => "Seed kopieren");
                        cellOf(r.transform, 6, b.date, false);
                    }
                });
        }

        /// <summary>Alias (Auftragsbeschreibung).</summary>
        public static RectTransform recordsScreen(GameApp app) => renderRecords(app);
    }
}
