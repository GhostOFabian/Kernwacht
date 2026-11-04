using System.Collections.Generic;
using Kernwacht.Models;
using UnityEngine;

namespace Kernwacht.Game
{
    public class Status
    {
        public string id;
        public float t, power;
        public int stacks = 1, flag;
        public Tower src;
    }

    public class Enemy
    {
        public int uid;
        public EnemyDef def;
        public BossDef boss;
        public bool elite, flying, alive = true, rewarded, leaked;
        public float x, y;             // Position im Zellraster (Zellmitte = +0,5)
        public int nx, ny;             // nächste Zielzelle
        public float hp, maxHp, shield, maxShield, speed, reward, leak, size;
        public float poise, maxPoise, phaseShift, phaseCd = 6f, abilityCd = 4f, staggerT, spawnedAt;
        public int phase;
        public List<float> plates = new List<float>();
        public float plateMax;
        public readonly List<Status> statuses = new List<Status>();
        public readonly Dictionary<string, int> adapt = new Dictionary<string, int>();
        public readonly Dictionary<string, int> adaptHits = new Dictionary<string, int>();
        public readonly Dictionary<string, float> reactCd = new Dictionary<string, float>();
        public float remaining;        // Restweg zum Kern (Zellen) – für die Zielwahl „Erster“
        public float hitFlash;
        public string mutation;
        public bool reshieldUsed, enraged, splitChild;

        // Darstellung
        public GameObject view;
        public UnitView unit;
        public float viewHeight = 1f;
        public float deathTime = -1f;

        public string Name => boss != null ? boss.name : def.name;
        public string ModelId => boss != null ? boss.id : def.id;
        public Vector2 Pos => new Vector2(x, y);
        public bool IsBoss => boss != null;

        public Status Get(string id)
        {
            for (int i = 0; i < statuses.Count; i++) if (statuses[i].id == id) return statuses[i];
            return null;
        }

        public bool Has(string id) => Get(id) != null;

        public void Remove(string id)
        {
            for (int i = statuses.Count - 1; i >= 0; i--) if (statuses[i].id == id) statuses.RemoveAt(i);
        }

        /// <summary>Status setzen/auffrischen (wie addStatus im Webspiel): längere Dauer, stärkere Wirkung, Stapel bis maxStacks.</summary>
        public Status Add(string id, float dur, float power, Tower src, int maxStacks = 1)
        {
            var s = Get(id);
            if (s == null)
            {
                s = new Status { id = id, t = dur, power = power, src = src, stacks = 1 };
                statuses.Add(s);
                return s;
            }
            s.t = Mathf.Max(s.t, dur);
            s.power = Mathf.Max(s.power, power);
            if (s.stacks < maxStacks) s.stacks++;
            if (src != null) s.src = src;
            return s;
        }
    }

    public class Tower
    {
        public int uid;
        public TowerDef def;
        public int cx, cy, level = 1, spec, invested;
        public string priority = "first";
        public float cd, charge, damageDealt, laserRamp, auraDmg, auraSpeed, pylonBonus;
        public Enemy target, laserTarget;
        public int rampHits, kills;
        public Enemy rampTarget;
        public int minesOut;
        public bool high, ley;
        public Dictionary<string, float> st = new Dictionary<string, float>();

        public GameObject view;
        public TowerView tview;

        public float S(string k) => st.TryGetValue(k, out var v) ? v : 0f;
        public Vector2 Center => new Vector2(cx + def.size * 0.5f, cy + def.size * 0.5f);
        public bool MaxLevel => level >= 1 + def.upgrades.Count;
        public bool CanSpec => MaxLevel && spec == 0 && def.specs.Count > 0;
    }

    public class Projectile
    {
        public Tower tower;
        public Enemy target;
        public Vector2 pos, aim;
        public float z, speed, mult = 1f;
        public int pierceLeft;
        public bool dead;
        public HashSet<Enemy> hit;
        public GameObject view;
    }

    public class Mine
    {
        public Tower tower;
        public Vector2 pos;
        public float arm = 0.6f;
        public bool dead;
        public GameObject view;
    }

    public class Cloud
    {
        public Tower tower;
        public Vector2 pos;
        public float r, t;
        public GameObject view;
    }

    public class Meteor
    {
        public Tower tower;
        public Vector2 pos;
        public float t, total;
        public GameObject view;
    }

    public class SpawnEntry
    {
        public float at;
        public string enemy, boss, mutation;
        public bool elite, miniBoss;
    }
}
