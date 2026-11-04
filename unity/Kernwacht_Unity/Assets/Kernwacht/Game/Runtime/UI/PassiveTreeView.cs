using System;
using System.Collections.Generic;
using Kernwacht.Sim;
using UnityEngine;
using UnityEngine.UI;

// Port von ui/passiveTreeView.ts: Darstellung des passiven Baums (Startbildschirm: bearbeitbar; im Lauf: nur Anzeige).

namespace Kernwacht.Game.UI
{
    public static class PassiveTreeView
    {
        private enum Kind { Pct, Num, Int }

        // Reihenfolge wie FX_LABEL in TS (Object.entries)
        private static readonly (string key, string name, Kind kind)[] FX_LABEL =
        {
            ("heroHpPct", "Helden-LP", Kind.Pct), ("heroDmgPct", "Heldenschaden", Kind.Pct), ("heroArmor", "Heldenrüstung", Kind.Pct), ("heroCdr", "Abklingzeitreduktion", Kind.Pct),
            ("heroCrit", "Krit-Chance Held", Kind.Pct), ("heroRegen", "LP-Regeneration", Kind.Num), ("manaRegen", "Manaregeneration", Kind.Num), ("supportPower", "Unterstützungsstärke", Kind.Pct),
            ("pickupRadius", "Aufsammelradius", Kind.Num), ("xpPct", "Erfahrung", Kind.Pct), ("towerDmgPct", "Turmschaden", Kind.Pct), ("towerSpeedPct", "Turmtempo", Kind.Pct),
            ("towerRange", "Turmreichweite", Kind.Num), ("startGold", "Startgold", Kind.Int), ("killGoldPct", "Kopfgeld", Kind.Pct), ("resourcePct", "Ressourcen", Kind.Pct),
            ("lootPct", "Beutechance", Kind.Pct), ("coreLives", "Kernenergie", Kind.Int), ("respawnPct", "Schnellere Wiederbelebung", Kind.Pct), ("unlockEarlier", "Türme früher (Wellen)", Kind.Int),
            ("heroAtkSpeed", "Angriffstempo Held", Kind.Pct), ("abilityPct", "Fähigkeitsstärke", Kind.Pct), ("moveSpeed", "Bewegungstempo", Kind.Num), ("lifesteal", "Lebensraub", Kind.Pct),
            ("towerCrit", "Krit-Chance Türme", Kind.Pct), ("towerPierce", "Panzerbrechung Türme", Kind.Pct), ("towerCostPct", "Baukosten Türme −", Kind.Pct), ("mineIncomePct", "Goldgräber-Ertrag", Kind.Pct), ("towerSplashPct", "Flächenradius Türme", Kind.Pct),
        };

        public static List<string> effectLines(PassiveEffects fx)
        {
            var @out = new List<string>();
            foreach (var (k, name, kind) in FX_LABEL)
            {
                double v = fx[k];
                if (!U.Truthy(v)) continue;
                @out.Add($"{name} +{(kind == Kind.Pct ? $"{HeroKit.fmtNum(v * 100, 1)} %" : kind == Kind.Int ? U.Fmt(U.Round(v)) : HeroKit.fmtNum(v, 2))}");
            }
            return @out;
        }

        /// <summary>
        /// Baut den Baum als neues, noch nicht eingehängtes Element (TS gibt ein HTMLElement zurück); der Aufrufer hängt es mit SetParent(.., false) ein.
        /// </summary>
        public static RectTransform renderPassiveTree(Dictionary<string, double> passive, double bestWave, bool editable, Action<string> onRaise = null, Action onReset = null)
        {
            return renderPassiveTree(null, passive, bestWave, editable, onRaise, onReset, 0);
        }

        /// <summary>Baut den Baum direkt unter <paramref name="parent"/> (Layout-Gruppe). width = verfügbare Breite (0 = aus dem Elternelement, sonst 820).</summary>
        public static RectTransform renderPassiveTree(Transform parent, Dictionary<string, double> passive, double bestWave, bool editable, Action<string> onRaise = null, Action onReset = null, float width = 0)
        {
            passive = passive ?? new Dictionary<string, double>();
            if (width <= 0)
            {
                var prt = parent as RectTransform;
                width = prt != null && prt.rect.width > 40 ? prt.rect.width : 820;
            }
            double points = PassiveTree.passivePoints(bestWave);
            double spent = PassiveTree.passiveSpent(passive);
            var fx = PassiveTree.passiveEffects(passive);
            var lines = effectLines(fx);
            var P = PassiveTree.PASSIVE;
            double? nextAt = points < P.maxPoints ? (points + 1) * P.wavesPerPoint : (double?)null;

            var col = Ui.Column(parent, 7);
            col.name = "PassiveTree";
            var root = col.GetComponent<RectTransform>();
            if (parent == null || parent.GetComponent<LayoutGroup>() == null)
            {
                var fit = col.gameObject.AddComponent<ContentSizeFitter>();
                fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                root.sizeDelta = new Vector2(width, root.sizeDelta.y);
            }

            var head = Ui.Row(col.transform, 8, TextAnchor.MiddleLeft, false);
            var ht = HeroKit.T(head.transform,
                $"<b>Passiver Baum</b> · Punkte <b>{U.Fmt(points - spent)} frei</b> ({U.Fmt(spent)}/{U.Fmt(points)} vergeben, max. {U.Fmt(P.maxPoints)} bei Bestwelle {U.Fmt(P.maxPoints * P.wavesPerPoint)})", 16, Theme.Text);
            Ui.Size(ht, -1, -1, 1);
            if (editable && spent > 0 && onReset != null)
            {
                var rb = HeroKit.Btn(head.transform, "Zurücksetzen", () => onReset());
                HeroKit.Tip(rb, "Alle Punkte kostenlos zurückerstatten");
            }
            HeroKit.P(col.transform,
                $"1 Punkt je {U.Fmt(P.wavesPerPoint)} Bestwellen{(U.Truthy(nextAt) ? $" · nächster Punkt bei Bestwelle {U.Fmt(nextAt.Value)}" : "")}. Wirkt automatisch in jedem Helden-Endloslauf (Stand beim Laufstart). {U.Fmt(PassiveTree.PASSIVE_TOTAL_RANKS)} Ränge insgesamt.",
                13, Theme.Muted);
            if (lines.Count > 0) HeroKit.P(col.transform, "Aktiv: " + string.Join(" · ", lines), 15, Theme.Good);

            for (int tier = 1; tier <= P.tierGate.Length; tier++)
            {
                double gate = P.tierGate[tier - 1];
                bool open = spent >= gate;
                var pt = Ui.Column(col.transform, 4);
                if (!open) pt.gameObject.AddComponent<CanvasGroup>().alpha = 0.55f;
                var line = Ui.Panel(pt.transform, "Line", Theme.Line, false, null);
                line.sprite = null;
                line.raycastTarget = false;
                Ui.Size(line, -1, 1);
                HeroKit.T(pt.transform, $"<b>Stufe {tier}</b>{(U.Truthy(gate) ? $"<size=13>{HeroKit.Col($" · ab {U.Fmt(gate)} vergebenen Punkten", Theme.Muted)}</size>" : "")}", 15, Theme.Text);
                var grid = new HeroGrid(pt.transform, width, 154, 5);
                foreach (var n in PassiveTree.PASSIVE_NODES)
                {
                    if (n.tier != tier) continue;
                    double rank = U.Get(passive, n.id, 0);
                    string why = PassiveTree.passiveRaiseReason(passive, bestWave, n.id);
                    bool noWhy = string.IsNullOrEmpty(why);
                    string nid = n.id;
                    string tip = $"{n.name}: {n.description} je Rang{(!noWhy && rank < n.maxRank ? $"\n{why}" : "")}";
                    var b = HeroHud.nodeButton(grid.Next(), HeroKit.IconName(n.icon), n.name, $"{U.Fmt(rank)}/{U.Fmt(n.maxRank)}", $"{n.description} je Rang", null,
                        rank > 0, rank >= n.maxRank, editable && noWhy, editable && noWhy, tip, () => onRaise?.Invoke(nid), 70);
                    // .pnode:disabled ist nicht abgedunkelt (nur .ptier.closed)
                    var cg = b.GetComponent<CanvasGroup>();
                    if (cg != null) cg.alpha = 1f;
                    grid.Fit(b);
                }
            }
            return root;
        }
    }
}
