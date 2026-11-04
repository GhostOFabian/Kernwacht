// Übertragung von render/fx.ts: weiche Sprite-Partikel (Glühen, Funken, Rauch, Ringe, Sterne), Trümmerstücke,
// verästelte Blitze, Strahlen und Bodendekale. Rein visuell (UnityEngine.Random, nicht deterministisch).
// Unity: alle Puffer sind vorab angelegt (Pools), emit() fordert keinen Speicher an.
using UnityEngine;

namespace Kernwacht.Game
{
    public class Fx
    {
        public struct SpriteParticle
        {
            public int shape;
            public bool add;
            public float x, y, z;
            public float vx, vy, vz;
            public float life, max;
            public float s0, s1;
            public int c0, c1;
            public float a;
            public float grav;
            public float drag;
            public float rot, vrot;
            public float stretch;   // >0: entlang der Bildschirmgeschwindigkeit gestreckt
            public bool flat;       // liegt auf dem Boden
            public bool top;        // über allem
            public float fadeIn;
            public float seed;
        }

        struct Debris
        {
            public string mesh;
            public float x, y, z, vx, vy, vz, life, max, s;
            public int color;
            public float emit, yaw, vyaw, floor;
        }

        class Bolt
        {
            public readonly float[] pts = new float[48];
            public int n;   // Anzahl Floats
            public float life, max;
            public int color;
            public float width;
        }

        struct Decal
        {
            public float x, y, z, size;
            public int color;
            public float a, life, max;
            public int shape;
            public float rot;
            public bool add;
        }

        const int SP_MAX = 4000, DEBRIS_MAX = 220, BOLT_MAX = 120, DECAL_MAX = 90;
        public SpriteParticle[] sp = new SpriteParticle[SP_MAX];
        public int spCount;
        readonly Debris[] debris = new Debris[DEBRIS_MAX];
        int debrisCount;
        readonly Bolt[] bolts = new Bolt[BOLT_MAX];
        int boltCount;
        readonly Decal[] decals = new Decal[DECAL_MAX];
        int decalCount;
        static readonly float[] jagBuf = new float[48];

        public int cap = 1600;
        /// <summary>0 = aus (Bewegung reduzieren) … 1 = voll.</summary>
        public float intensity = 1;

        public Fx()
        {
            for (int i = 0; i < BOLT_MAX; i++) bolts[i] = new Bolt();
        }

        static float R() => Random.value;

        public void clear()
        {
            spCount = 0;
            debrisCount = 0;
            boltCount = 0;
            decalCount = 0;
        }

        /// <summary>Partikel ausstoßen (Standardwerte wie fx.ts; c1 = -1 → wie c0, max = -1 → wie life, seed = -1 → zufällig).</summary>
        public void emit(float x, float y, float z, float life, int c0,
            int shape = SHAPE.glow, bool add = true, float vx = 0, float vy = 0, float vz = 0, float max = -1,
            float s0 = 0.3f, float s1 = 0.3f, int c1 = -1, float a = 1, float grav = 0, float drag = 0,
            float rot = 0, float vrot = 0, float stretch = 0, bool flat = false, bool top = false, float fadeIn = 0, float seed = -1)
        {
            int c = Mathf.Min(cap, SP_MAX);
            if (spCount >= c) return;
            ref var p = ref sp[spCount++];
            p.shape = shape; p.add = add;
            p.x = x; p.y = y; p.z = z;
            p.vx = vx; p.vy = vy; p.vz = vz;
            p.life = life; p.max = max < 0 ? life : max;
            p.s0 = s0; p.s1 = s1;
            p.c0 = c0; p.c1 = c1 < 0 ? c0 : c1;
            p.a = a; p.grav = grav; p.drag = drag;
            p.rot = rot; p.vrot = vrot; p.stretch = stretch;
            p.flat = flat; p.top = top; p.fadeIn = fadeIn;
            p.seed = seed < 0 ? R() * 100 : seed;
        }

        /// <summary>Funkenregen.</summary>
        public void sparks(float x, float y, float z, int color, float n, float speed = 1, float size = 0.12f)
        {
            for (int i = 0; i < n; i++)
            {
                float a = R() * Mathf.PI * 2;
                float v = (1 + R() * 2.5f) * speed;
                emit(x, y, z, 0.3f + R() * 0.35f, Col.Mix(color, 0xffffff, 0.5), shape: SHAPE.spark, vx: Mathf.Cos(a) * v, vy: (1 + R() * 2.5f) * speed, vz: Mathf.Sin(a) * v,
                    c1: color, s0: size, s1: size * 0.3f, grav: 9, drag: 1.5f, stretch: 0.12f);
            }
        }

        /// <summary>Flammen mit aufsteigendem Rauch.</summary>
        public void fire(float x, float y, float z, float scale = 1, int color = 0xff7a20, bool smoke = true)
        {
            emit(x + (R() - 0.5f) * 0.2f * scale, y, z + (R() - 0.5f) * 0.2f * scale, 0.35f + R() * 0.3f, Col.Mix(color, 0xfff0a0, 0.6),
                vy: 0.9f + R() * 0.8f, vx: (R() - 0.5f) * 0.3f, vz: (R() - 0.5f) * 0.3f, c1: Col.Mix(color, 0x801000, 0.5), s0: 0.24f * scale, s1: 0.05f * scale, fadeIn: 0.1f);
            if (smoke && R() < 0.45f)
            {
                emit(x, y + 0.3f * scale, z, 0.9f + R() * 0.6f, 0x3a3430, shape: SHAPE.smoke, add: false, vy: 0.5f + R() * 0.4f, vx: (R() - 0.5f) * 0.25f, vz: (R() - 0.5f) * 0.25f,
                    c1: 0x6a6460, a: 0.32f, s0: 0.15f * scale, s1: 0.55f * scale, fadeIn: 0.2f, vrot: R() - 0.5f);
            }
        }

        public void smoke(float x, float y, float z, float size, int color = 0x5a5450, float alpha = 0.4f, float life = 1.2f)
        {
            emit(x, y, z, life * (0.7f + R() * 0.6f), color, shape: SHAPE.smoke, add: false, vy: 0.35f + R() * 0.3f, vx: (R() - 0.5f) * 0.4f, vz: (R() - 0.5f) * 0.4f,
                c1: Col.Mix(color, 0x9a9490, 0.4), a: alpha, s0: size * 0.5f, s1: size * 1.4f, fadeIn: 0.15f, vrot: (R() - 0.5f) * 0.8f, drag: 0.8f);
        }

        public void glow(float x, float y, float z, float size, int color, float life = 0.25f, float alpha = 1)
        {
            emit(x, y, z, life, color, s0: size, s1: size * 1.3f, a: alpha);
        }

        /// <summary>Bodenring (Schockwelle).</summary>
        public void ring(float x, float y, float z, float r, int color, float life = 0.45f, float width = 0.12f, float from = 0.15f)
        {
            emit(x, y + 0.03f, z, life, color, shape: SHAPE.ring, flat: true, s0: Mathf.Max(0.05f, r * from), s1: r, a: 0.95f, seed: width);
        }

        /// <summary>Aufrechter Lichtstrahl (Einsatz, Fusion).</summary>
        public void pillar(float x, float y, float z, float height, int color, float life = 0.6f, float width = 0.3f)
        {
            var b = NewBolt();
            if (b == null) return;
            b.pts[0] = x; b.pts[1] = y; b.pts[2] = z; b.pts[3] = x; b.pts[4] = y + height; b.pts[5] = z;
            b.n = 6;
            b.life = life; b.max = life; b.color = color; b.width = -width;
        }

        public void explosion(float x, float y, float z, float r, int color = 0xff7a20, bool big = false)
        {
            float k = intensity;
            glow(x, y + 0.3f, z, r * 1.6f, Col.Mix(color, 0xffffff, 0.4), 0.22f);
            ring(x, y, z, r * 1.2f, color, 0.45f, 0.2f);
            int n = Mathf.RoundToInt((big ? 22 : 10) * k);
            for (int i = 0; i < n; i++)
            {
                float a = R() * Mathf.PI * 2;
                float v = R() * r * 2.2f;
                emit(x + Mathf.Cos(a) * 0.1f, y + 0.2f, z + Mathf.Sin(a) * 0.1f, 0.35f + R() * 0.35f, 0xfff0a0, vx: Mathf.Cos(a) * v, vy: 0.8f + R() * 2.2f, vz: Mathf.Sin(a) * v,
                    c1: Col.Mix(color, 0x400800, 0.4), s0: 0.32f * r, s1: 0.06f, drag: 3);
            }
            int sm = Mathf.RoundToInt((big ? 8 : 4) * k);
            for (int i = 0; i < sm; i++) smoke(x + (R() - 0.5f) * r, y + 0.3f, z + (R() - 0.5f) * r, 0.6f * r, 0x403830, 0.45f, 1.6f);
            sparks(x, y + 0.2f, z, color, Mathf.RoundToInt((big ? 14 : 6) * k), 1.2f);
            if (big) debrisBurst(x, y, z, 0x4a4038, 6);
            decal(x, y, z, r * 1.1f, 0x14100c, 0.5f, 6);
        }

        public void debrisBurst(float x, float y, float z, int color, float n, float emit = 0)
        {
            for (int i = 0; i < n * intensity; i++)
            {
                if (debrisCount >= DEBRIS_MAX)
                {
                    // älteste verwerfen (wie splice am Anfang)
                    System.Array.Copy(debris, 1, debris, 0, DEBRIS_MAX - 1);
                    debrisCount = DEBRIS_MAX - 1;
                }
                float a = R() * Mathf.PI * 2;
                float v = 1 + R() * 2;
                ref var d = ref debris[debrisCount++];
                d.mesh = R() < 0.5f ? "rbox" : "gem";
                d.x = x; d.y = y + 0.2f; d.z = z;
                d.vx = Mathf.Cos(a) * v; d.vy = 2 + R() * 3; d.vz = Mathf.Sin(a) * v;
                d.life = 0.9f + R() * 0.5f; d.max = 1.4f; d.s = 0.08f + R() * 0.1f; d.color = color; d.emit = emit;
                d.yaw = R() * 6; d.vyaw = (R() - 0.5f) * 12; d.floor = y;
            }
        }

        public void decal(float x, float y, float z, float size, int color, float alpha, float life, int shape = SHAPE.smoke, bool add = false)
        {
            if (decalCount >= DECAL_MAX)
            {
                System.Array.Copy(decals, 1, decals, 0, DECAL_MAX - 1);
                decalCount = DECAL_MAX - 1;
            }
            ref var d = ref decals[decalCount++];
            d.x = x; d.y = y + 0.015f; d.z = z; d.size = size; d.color = color; d.a = alpha; d.life = life; d.max = life; d.shape = shape; d.rot = R() * 6; d.add = add;
        }

        Bolt NewBolt()
        {
            if (boltCount >= BOLT_MAX)
            {
                // älteste verwerfen
                var first = bolts[0];
                System.Array.Copy(bolts, 1, bolts, 0, BOLT_MAX - 1);
                bolts[BOLT_MAX - 1] = first;
                boltCount = BOLT_MAX - 1;
            }
            return bolts[boltCount++];
        }

        /// <summary>Verästelter Blitz.</summary>
        public void bolt(float x0, float y0, float z0, float x1, float y1, float z1, int color, float width = 0.06f, float life = 0.18f, int branches = 1)
        {
            var main = NewBolt();
            main.n = jag(x0, y0, z0, x1, y1, z1, main.pts);
            main.life = life; main.max = life; main.color = color; main.width = width;
            for (int b = 0; b < branches; b++)
            {
                if (main.n < 12) break;
                int idx = 1 + Mathf.FloorToInt(R() * (main.n / 3 - 2));
                float bx = main.pts[idx * 3], by = main.pts[idx * 3 + 1], bz = main.pts[idx * 3 + 2];
                float dx = x1 - x0, dy = y1 - y0, dz = z1 - z0;
                float len = Mathf.Sqrt(dx * dx + dy * dy + dz * dz) * (0.25f + R() * 0.25f);
                float a = R() * Mathf.PI * 2;
                float hl = Mathf.Sqrt(dx * dx + dz * dz);
                if (hl < 1e-6f) hl = 1;
                float ex = bx + dx / hl * len * 0.5f + Mathf.Cos(a) * len * 0.6f;
                float ez = bz + dz / hl * len * 0.5f + Mathf.Sin(a) * len * 0.6f;
                var br = NewBolt();
                br.n = jag(bx, by, bz, ex, by - len * 0.3f, ez, br.pts);
                br.life = life * 0.8f; br.max = life; br.color = color; br.width = width * 0.6f;
            }
        }

        /// <summary>Gerader, ausblendender Strahl.</summary>
        public void beam(float x0, float y0, float z0, float x1, float y1, float z1, int color, float width, float life)
        {
            var b = NewBolt();
            b.pts[0] = x0; b.pts[1] = y0; b.pts[2] = z0; b.pts[3] = x1; b.pts[4] = y1; b.pts[5] = z1;
            b.n = 6;
            b.life = life; b.max = life; b.color = color; b.width = width;
        }

        public void update(float dt, WorldDraw r)
        {
            // Dekale
            int w = 0;
            for (int i = 0; i < decalCount; i++)
            {
                var d = decals[i];
                d.life -= dt;
                if (d.life <= 0) continue;
                float f = Mathf.Min(1, d.life / (d.max * 0.4f));
                r.ground(d.add, d.shape, d.x, d.y, d.z, d.size, d.color, d.a * f, d.rot, 0.5f, 0.15f);
                decals[w++] = d;
            }
            decalCount = w;

            // Sprite-Partikel
            var rr = r.camRight;
            var uu = r.camUp;
            w = 0;
            for (int i = 0; i < spCount; i++)
            {
                var p = sp[i];
                p.life -= dt;
                if (p.life <= 0) continue;
                if (p.drag > 0)
                {
                    float k = Mathf.Max(0, 1 - p.drag * dt);
                    p.vx *= k; p.vy *= k; p.vz *= k;
                }
                p.vy -= p.grav * dt;
                p.x += p.vx * dt;
                p.y += p.vy * dt;
                p.z += p.vz * dt;
                if (p.grav > 0 && p.y < 0)
                {
                    p.y = 0;
                    p.vy *= -0.35f;
                    p.vx *= 0.6f;
                    p.vz *= 0.6f;
                }
                p.rot += p.vrot * dt;
                float t = 1 - p.life / p.max;
                float size = p.s0 + (p.s1 - p.s0) * t;
                float a = p.a * (1 - t * t);
                if (p.fadeIn > 0 && t < p.fadeIn) a *= t / p.fadeIn;
                int col = p.c0 == p.c1 ? p.c0 : Col.Mix(p.c0, p.c1, t);
                if (p.flat)
                {
                    r.ground(p.add, p.shape, p.x, p.y, p.z, size, col, a, p.rot, 0.3f, p.shape == SHAPE.ring ? p.seed : 0.15f);
                }
                else if (p.stretch > 0)
                {
                    float sx = p.vx * rr.x + p.vy * rr.y + p.vz * rr.z;
                    float sy = p.vx * uu.x + p.vy * uu.y + p.vz * uu.z;
                    float spd = Mathf.Sqrt(sx * sx + sy * sy);
                    r.billboard(p.add, p.shape, p.x, p.y, p.z, size, col, a, Mathf.Atan2(sy, sx), 1 + spd * p.stretch * 4, p.seed, p.top);
                }
                else
                {
                    r.billboard(p.add, p.shape, p.x, p.y, p.z, size, col, a, p.rot, 1, p.seed, p.top);
                }
                sp[w++] = p;
            }
            spCount = w;

            // Trümmer
            w = 0;
            for (int i = 0; i < debrisCount; i++)
            {
                var d = debris[i];
                d.life -= dt;
                if (d.life <= 0) continue;
                d.vy -= 12 * dt;
                d.x += d.vx * dt;
                d.y += d.vy * dt;
                d.z += d.vz * dt;
                d.yaw += d.vyaw * dt;
                if (d.y < d.floor)
                {
                    d.y = d.floor;
                    d.vy *= -0.3f;
                    d.vx *= 0.5f;
                    d.vz *= 0.5f;
                    d.vyaw *= 0.5f;
                }
                float s = d.s * Mathf.Min(1, d.life / 0.3f);
                r.add("opaque", d.mesh, d.x, d.y + s / 2, d.z, s, s, s, d.color, 1, d.emit, d.yaw, d.yaw * 0.7f);
                debris[w++] = d;
            }
            debrisCount = w;

            // Blitze und Strahlen
            w = 0;
            for (int i = 0; i < boltCount; i++)
            {
                var b = bolts[i];
                b.life -= dt;
                if (b.life <= 0) { continue; }
                float f = b.life / b.max;
                float flick = b.width < 0 ? 1 : 0.65f + R() * 0.35f;
                float width = Mathf.Abs(b.width) * (b.width < 0 ? f : 1);
                var pts = b.pts;
                int hi = Col.Mix(b.color, 0xffffff, 0.7);
                for (int k = 0; k + 5 < b.n; k += 3)
                {
                    r.line(pts[k], pts[k + 1], pts[k + 2], pts[k + 3], pts[k + 4], pts[k + 5], width * 3.2f, b.color, 0.35f * f * flick);
                    r.line(pts[k], pts[k + 1], pts[k + 2], pts[k + 3], pts[k + 4], pts[k + 5], width, hi, f * flick);
                }
                // überlebende Blitze nach vorn tauschen (Objekte bleiben im Pool)
                if (w != i) { var tmp = bolts[w]; bolts[w] = b; bolts[i] = tmp; }
                w++;
            }
            boltCount = w;
        }

        /// <summary>Gezackte Linie (Blitz) – schreibt Punkte (x,y,z …) in buf, liefert die Anzahl Floats.</summary>
        public static int jag(float x0, float y0, float z0, float x1, float y1, float z1, float[] buf)
        {
            float dx = x1 - x0, dy = y1 - y0, dz = z1 - z0;
            float len = Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
            int n = Mathf.Max(2, Mathf.Min(14, Mathf.RoundToInt(len / 0.32f)));
            float amp = Mathf.Min(0.32f, len * 0.1f);
            int o = 0;
            buf[o++] = x0; buf[o++] = y0; buf[o++] = z0;
            for (int i = 1; i < n; i++)
            {
                float t = i / (float)n;
                buf[o++] = x0 + dx * t + (R() - 0.5f) * amp * 2;
                buf[o++] = y0 + dy * t + (R() - 0.5f) * amp * 1.4f;
                buf[o++] = z0 + dz * t + (R() - 0.5f) * amp * 2;
            }
            buf[o++] = x1; buf[o++] = y1; buf[o++] = z1;
            return o;
        }

        /// <summary>Gezackte Linie in einen gemeinsamen Puffer (für Tesla-/Zwillingsbögen pro Bild).</summary>
        public static float[] jagShared(float x0, float y0, float z0, float x1, float y1, float z1, out int n)
        {
            n = jag(x0, y0, z0, x1, y1, z1, jagBuf);
            return jagBuf;
        }
    }
}
