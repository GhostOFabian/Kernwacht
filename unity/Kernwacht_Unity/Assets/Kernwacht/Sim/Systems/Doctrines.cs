// Doktrinen (Umbau Schritt 5).
// Vor dem Lauf gewählte Spielweise mit klarem Vorteil und spürbarem Preis. Sie verschiebt das Gewicht
// zwischen Türmen, Befehl des Helden, Heldenschaden und Taktikladungen – Türme bleiben in jeder Doktrin die Hauptquelle.

using System.Collections.Generic;

namespace Kernwacht.Sim
{
    public class DoctrineDefinition
    {
        public string id;
        public string name;
        public string description;
        /// <summary>Faktor auf den Befehlsbonus des Helden.</summary>
        public double commandMul = 1;
        /// <summary>Zusätzlicher Befehlsradius (Felder).</summary>
        public double commandRadius;
        /// <summary>Faktor auf den Heldenschaden.</summary>
        public double heroDmgMul = 1;
        /// <summary>Schadensbonus aller Türme, solange der Held nahe am Kern steht (Baumeister).</summary>
        public double coreGuardDmg;
        /// <summary>Schadensabzug für Türme außerhalb von Befehl und Banner (Feldherr).</summary>
        public double outsidePenalty;
        /// <summary>Taktikladungen: Höchstvorrat und Abstand in Wellen.</summary>
        public double chargeMax = TacticalCharges.MAX_STORED;
        public double chargeEvery = TacticalCharges.EVERY;
    }

    public static class Doctrines
    {
        public const string DEFAULT = "balance";
        /// <summary>Abstand zum Kern, in dem der Baumeister-Bonus gilt.</summary>
        public const double CORE_GUARD_RADIUS = 3;

        public static readonly Dictionary<string, DoctrineDefinition> DOCTRINES = new Dictionary<string, DoctrineDefinition>
        {
            ["balance"] = new DoctrineDefinition { id = "balance", name = "Gleichgewicht", description = "Keine Besonderheiten – Türme, Held und Ladungen wie gewohnt." },
            ["builder"] = new DoctrineDefinition { id = "builder", name = "Baumeister", commandMul = 0.4, heroDmgMul = 0.6, coreGuardDmg = 0.1, description = "Für reine Turmspieler: alle Türme +10 % Schaden, solange der Held höchstens 3 Felder vom Kern entfernt steht. Befehl 40 %, Heldenschaden 60 %." },
            ["warlord"] = new DoctrineDefinition { id = "warlord", name = "Feldherr", commandMul = 1.35, commandRadius = 0.5, outsidePenalty = 0.1, description = "Befehl +35 % und +0,5 Felder Radius – Türme außerhalb von Held und Banner verlieren 10 % Schaden. Stellung zählt doppelt." },
            ["tactician"] = new DoctrineDefinition { id = "tactician", name = "Taktiker", commandMul = 0.75, chargeMax = 5, chargeEvery = 4, description = "Taktikladungen alle 4 Wellen, bis zu 5 gespeichert. Befehl nur 75 %." },
            ["blade"] = new DoctrineDefinition { id = "blade", name = "Klingenmeister", commandMul = 0.5, heroDmgMul = 1.6, description = "Der Held kämpft selbst: +60 % Heldenschaden, dafür nur halber Befehl." },
        };
        public static readonly List<string> ORDER = new List<string> { "balance", "builder", "warlord", "tactician", "blade" };

        public static DoctrineDefinition of(HeroEndlessState he)
        {
            string id = he?.doctrine;
            return id != null && DOCTRINES.TryGetValue(id, out var d) ? d : DOCTRINES[DEFAULT];
        }

        /// <summary>Steht der Held nahe genug am Kern für den Baumeister-Bonus?</summary>
        public static bool heroAtCore(GameSimulation sim)
        {
            var he = sim.s.he;
            if (he == null || he.champion.dead > 0) return false;
            var g = sim.s.map.goal;
            double dx = he.champion.x - (g.x + 0.5), dy = he.champion.y - (g.y + 0.5);
            return dx * dx + dy * dy <= CORE_GUARD_RADIUS * CORE_GUARD_RADIUS;
        }
    }
}
