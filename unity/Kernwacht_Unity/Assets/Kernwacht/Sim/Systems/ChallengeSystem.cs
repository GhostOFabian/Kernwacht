// Übersetzung von sim/ChallengeSystem.ts.
// 3.1 Challenge-Bosse: stark verstärkte Bosse mit eigenen Mechaniken – eine echte Prüfung für den Aufbau.
//  · 3,5× LP, +15 % auf alle Resistenzen, Kontrolleffekte wirken nur zu 35 %
//  · alle 15 s Schildschub (+8 % LP, höchstens 30 %), alle 12 s 3 Elite-Begleiter
//  · nach 60 s Raserei (+40 % Tempo)
//  · Durchbruch kostet mindestens 10 bzw. 25 % der maximalen Kernenergie; Sieg: 5× Kopfgeld

using System;
using System.Collections.Generic;

namespace Kernwacht.Sim
{
    public static partial class ChallengeSystem
    {
        public static void makeChallenge(GameSimulation sim, EnemyInstance e)
        {
            e.challenge = true;
            double mul = Scaling.challengeHpMul(sim.s.wave);
            e.maxHp *= mul;
            e.hp = e.maxHp;
            e.shield *= mul;
            e.maxShield = U.Max(e.maxShield * mul, e.shield);
            e.reward *= Scaling.CHALLENGE.rewardMul;
            e.leak = U.Max(Scaling.CHALLENGE.leakMin, Math.Ceiling(sim.s.maxLives * Scaling.CHALLENGE.leakPct), e.leak);
            e.chShieldCd = Scaling.CHALLENGE.shieldEvery;
            e.chSummonCd = Scaling.CHALLENGE.summonEvery * 0.5;
            string name = !string.IsNullOrEmpty(e.boss) ? Enemies.BOSSES[e.boss].name : "Boss";
            sim.emit(new GameEvent { k = "message", text = $"⚔ CHALLENGE: {name} – Schildschübe, Begleiter, Raserei nach {U.Fmt(Scaling.CHALLENGE.enrageAfter)} s. Ein Durchbruch kostet {U.Fmt(e.leak)} Kernenergie!", severity = "danger" });
        }

        private static readonly List<string> SUMMONS = new List<string> { "brute", "shield", "runner", "golem" };

        public static void updateChallenge(GameSimulation sim, EnemyInstance e, double dt)
        {
            if (e.challenge != true || !e.alive) return;
            e.chShieldCd = (e.chShieldCd ?? Scaling.CHALLENGE.shieldEvery) - dt;
            if (e.chShieldCd <= 0)
            {
                e.chShieldCd = Scaling.CHALLENGE.shieldEvery;
                double cap = e.maxHp * Scaling.CHALLENGE.shieldMax;
                e.shield = U.Min(cap, e.shield + e.maxHp * Scaling.CHALLENGE.shieldPct);
                e.maxShield = U.Max(e.maxShield, e.shield);
                sim.emit(new GameEvent { k = "burst", x = e.x, y = e.y, r = 1.4 });
            }
            e.chSummonCd = (e.chSummonCd ?? Scaling.CHALLENGE.summonEvery) - dt;
            if (e.chSummonCd <= 0)
            {
                e.chSummonCd = Scaling.CHALLENGE.summonEvery;
                for (int k = 0; k < Scaling.CHALLENGE.summonCount; k++)
                {
                    string id = SUMMONS[(int)((sim.s.tick + k) % SUMMONS.Count)];
                    EnemyInstance m = EnemySystem.spawnEnemyAt(sim, id, true, e);
                    m.x += (k - 1) * 0.3;
                }
                sim.emit(new GameEvent { k = "message", text = "Der Challenge-Boss ruft Begleiter!", severity = "warn" });
            }
            if (e.chEnraged != true && sim.s.time - e.spawnedAt >= Scaling.CHALLENGE.enrageAfter)
            {
                e.chEnraged = true;
                e.speed *= Scaling.CHALLENGE.enrageSpeed;
                sim.emit(new GameEvent { k = "message", text = "Der Challenge-Boss rast!", severity = "danger" });
            }
        }
    }
}
