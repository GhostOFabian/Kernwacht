// Übersetzung von sim/RogueliteSystem.ts.
// Roguelite-Auswahl: drei Modifikatoren aus eigenem Zufallsstrom (state.rng.mods).
// Regeln: keine Duplikate, keine unvereinbaren Kombinationen (exclusive), und mindestens ein
// Angebot ist sofort nutzbar (generisch oder passend zu gebauten Türmen).

using System;
using System.Collections.Generic;
using System.Linq;

namespace Kernwacht.Sim
{
    public static partial class RogueliteSystem
    {
        public const double SKIP_GOLD = 30;

        private static bool usable(GameState s, string id)
        {
            List<string> req = Modifiers.MODIFIERS[id].requiresTower;
            if (req == null) return true;
            return s.towers.Any((t) => req.Contains(t.def));
        }

        public static List<string> eligibleModifiers(GameState s)
        {
            return Modifiers.MODIFIER_IDS.Where((id) =>
            {
                if (s.modifiers.Contains(id)) return false;
                if (Modifiers.MODIFIERS[id].requiresHeroes == true && s.heroes.Count == 0) return false;
                List<string> ex = Modifiers.MODIFIERS[id].exclusive ?? new List<string>();
                if (ex.Any((x) => s.modifiers.Contains(x))) return false;
                if (s.modifiers.Any((m) => (Modifiers.MODIFIERS[m].exclusive ?? new List<string>()).Contains(id))) return false;
                return true;
            }).ToList();
        }

        public static List<string> rollOffer(GameSimulation sim)
        {
            GameState s = sim.s;
            Rng rng = new Rng(s.rng.mods);
            List<string> pool = eligibleModifiers(s);
            List<string> offer = new List<string>();
            List<string> avail = new List<string>(pool);
            while (offer.Count < 3 && avail.Count > 0)
            {
                string pick = rng.weighted(avail, (id) => (Modifiers.MODIFIERS[id].requiresTower != null ? (usable(s, id) ? 3 : 0.5) : 2));
                // Gegenseitig ausschließende Angebote vermeiden
                avail.RemoveAt(avail.IndexOf(pick));
                List<string> ex = Modifiers.MODIFIERS[pick].exclusive ?? new List<string>();
                if (offer.Any((o) => ex.Contains(o) || (Modifiers.MODIFIERS[o].exclusive ?? new List<string>()).Contains(pick))) continue;
                offer.Add(pick);
            }
            if (offer.Count > 0 && !offer.Any((id) => usable(s, id)))
            {
                string generic = avail.FirstOrDefault((id) => Modifiers.MODIFIERS[id].requiresTower == null);
                if (!string.IsNullOrEmpty(generic)) offer[offer.Count - 1] = generic;
            }
            s.rng.mods = rng.state;
            return offer;
        }

        public static void applyModifierChoice(GameSimulation sim, string id)
        {
            GameState s = sim.s;
            if (id == null)
            {
                s.gold += SKIP_GOLD;
                s.stats.goldEarned += SKIP_GOLD;
                sim.emit(new GameEvent { k = "message", text = $"Kein Modifikator gewählt: +{U.Fmt(SKIP_GOLD)} Gold", severity = "info" });
            }
            else
            {
                s.modifiers.Add(id);
                if (id == "glassCannon") s.lives = U.Max(1, s.lives - 5);
                if (id == "bulwark")
                {
                    s.lives += 8;
                    s.maxLives += 8;
                }
                sim.emit(new GameEvent { k = "message", text = $"Modifikator aktiv: {Modifiers.MODIFIERS[id].name}", severity = "info" });
            }
            s.modOffer = null;
            s.phase = "build";
            sim.c.statsDirty = true;
            sim.c.networkDirty = true;
        }
    }
}
