// Deterministische Zufallszahlen (Übersetzung von core/rng.ts). Alle Generatoren besitzen einen
// einzigen uint32-Zustand, damit sie vollständig serialisierbar sind. Getrennte Ströme werden über
// hashSeed(seed, label) abgeleitet, sodass z. B. kosmetische Zufallsänderungen keine Spielbalance beeinflussen.
//
// Bit-genau nach JS-Semantik: Math.imul = int-Multiplikation mit Überlauf, `>>> 0` = Umdeutung als uint,
// `x ^ y` = ToInt32-Operanden. Alle öffentlichen Zahlen sind double (wie TS number).

using System;
using System.Collections.Generic;
using System.Text;

namespace Kernwacht.Sim
{
    /// <summary>Freie Funktionen aus core/rng.ts.</summary>
    public static class RngUtil
    {
        /// <summary>32-Bit-Hash (FNV-1a + Avalanche) für Strings. Zeichen = UTF-16-Codeeinheiten wie charCodeAt.</summary>
        public static double hashString(string str)
        {
            unchecked
            {
                int h = (int)0x811c9dc5u;
                for (int i = 0; i < str.Length; i++)
                {
                    h ^= str[i];
                    h = h * 0x01000193;   // Math.imul(h, 0x01000193)
                }
                return mix32((double)(uint)h);
            }
        }

        /// <summary>Avalanche-Mischfunktion (murmur3 fmix32).</summary>
        public static double mix32(double x)
        {
            unchecked
            {
                uint u = U.ToUint32(x);
                u ^= u >> 16;
                u *= 0x85ebca6bu;
                u ^= u >> 13;
                u *= 0xc2b2ae35u;
                u ^= u >> 16;
                return u;
            }
        }

        /// <summary>Leitet einen unabhängigen Strom aus Basis-Seed und Bezeichner (Text) ab.</summary>
        public static double hashSeed(double @base, string label)
        {
            return hashSeedL(@base, hashString(label));
        }

        /// <summary>Leitet einen unabhängigen Strom aus Basis-Seed und Bezeichner (Zahl) ab.</summary>
        public static double hashSeed(double @base, double label)
        {
            return hashSeedL(@base, mix32(label + 0x9e3779b9));
        }

        private static double hashSeedL(double @base, double l)
        {
            unchecked
            {
                int b = U.ToInt32(@base);
                int m = U.ToInt32(l) * 0x27d4eb2d;   // Math.imul(l, 0x27d4eb2d)
                double r = mix32((double)(uint)(b ^ m));
                return r != 0 ? r : 0x1234567;
            }
        }

        /// <summary>Mulberry32 – ein Schritt. Gibt [Zufallswert 0..1, neuer Zustand] zurück.</summary>
        public static double[] step(double state)
        {
            double v, s;
            stepRaw(state, out v, out s);
            return new double[] { v, s };
        }

        /// <summary>Wie <see cref="step"/>, aber ohne Array-Allokation.</summary>
        public static void stepRaw(double state, out double v, out double s)
        {
            unchecked
            {
                uint su = U.ToUint32(state + 0x6d2b79f5);
                int t = (int)su;
                t = (t ^ (int)((uint)t >> 15)) * (t | 1);                 // Math.imul(t ^ (t >>> 15), t | 1)
                t ^= t + (t ^ (int)((uint)t >> 7)) * (t | 61);            // t ^= t + Math.imul(t ^ (t >>> 7), t | 61)
                uint w = (uint)(t ^ (int)((uint)t >> 14));                // (t ^ (t >>> 14)) >>> 0
                v = w / 4294967296.0;
                s = su;
            }
        }

        private const string SEED_ALPHABET = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        /// <summary>Erzeugt einen lesbaren Seed-Text (z. B. "K7QF-M9XA").</summary>
        public static string randomSeedText(double entropy)
        {
            double m = mix32(U.ToUint32(entropy));
            Rng r = new Rng(m != 0 ? m : 1);
            StringBuilder s = new StringBuilder();
            for (int i = 0; i < 8; i++)
            {
                if (i == 4) s.Append('-');
                s.Append(SEED_ALPHABET[(int)Math.Floor(r.next() * SEED_ALPHABET.Length)]);
            }
            return s.ToString();
        }

        /// <summary>Normalisiert Benutzereingaben für Seeds (Großschreibung, Trimmen, Länge begrenzen).</summary>
        public static string normalizeSeedText(string text)
        {
            string trimmed = JsTrim(text);
            string upper = JsToUpper(trimmed);
            // replace(/\s+/g, '-')
            StringBuilder sb = new StringBuilder();
            int i = 0;
            while (i < upper.Length)
            {
                if (IsJsWhitespace(upper[i]))
                {
                    while (i < upper.Length && IsJsWhitespace(upper[i])) i++;
                    sb.Append('-');
                }
                else
                {
                    sb.Append(upper[i]);
                    i++;
                }
            }
            string t = sb.ToString();
            if (t.Length > 32) t = t.Substring(0, 32);   // slice(0, 32) auf UTF-16-Codeeinheiten
            return t.Length > 0 ? t : "RIFT-0000";
        }

        /// <summary>TS: dailySeedText(date: Date) – erwartet die lokale Zeit (getFullYear/getMonth/getDate).</summary>
        public static string dailySeedText(DateTime date)
        {
            int y = date.Year;
            string m = date.Month.ToString(System.Globalization.CultureInfo.InvariantCulture).PadLeft(2, '0');
            string d = date.Day.ToString(System.Globalization.CultureInfo.InvariantCulture).PadLeft(2, '0');
            return "DAILY-" + y.ToString(System.Globalization.CultureInfo.InvariantCulture) + m + d;
        }

        // ---- JS-Zeichenklassen (String.prototype.trim und RegExp \s)

        /// <summary>JS WhiteSpace + LineTerminator (wie \s und trim()).</summary>
        private static bool IsJsWhitespace(char c)
        {
            switch (c)
            {
                case '\t': case '\n': case '\v': case '\f': case '\r': case ' ':
                case '\u00a0': case '\u1680': case '\u2028': case '\u2029': case '\u202f':
                case '\u205f': case '\u3000': case '\ufeff':
                    return true;
            }
            return c >= '\u2000' && c <= '\u200a';
        }

        private static string JsTrim(string s)
        {
            int a = 0, b = s.Length;
            while (a < b && IsJsWhitespace(s[a])) a++;
            while (b > a && IsJsWhitespace(s[b - 1])) b--;
            return s.Substring(a, b - a);
        }

        /// <summary>
        /// toUpperCase(): Invariante Großschreibung plus die Sonderfälle mit Längenänderung, die in Seeds
        /// realistisch vorkommen (ß → SS). PORT: weitere SpecialCasing-Fälle (Ligaturen ﬁ/ﬂ usw.) sind nicht abgebildet.
        /// </summary>
        private static string JsToUpper(string s)
        {
            string u = s.ToUpperInvariant();
            if (u.IndexOf('\u00df') >= 0) u = u.Replace("ß", "SS");
            return u;
        }
    }

    public class Rng
    {
        public double state;

        public Rng(double seed)
        {
            state = U.ToUint32(seed);
        }

        public double next()
        {
            double v, s;
            RngUtil.stepRaw(state, out v, out s);
            state = s;
            return v;
        }

        public double range(double min, double max)
        {
            return min + (max - min) * next();
        }

        /// <summary>TS: int(min, maxInclusive). Aufruf in C#: <c>rng.@int(a, b)</c>.</summary>
        public double @int(double min, double maxInclusive)
        {
            return min + Math.Floor(next() * (maxInclusive - min + 1));
        }

        public bool chance(double p)
        {
            return next() < p;
        }

        /// <summary>Funktioniert für List&lt;T&gt; und T[] (beide IReadOnlyList&lt;T&gt;).</summary>
        public T pick<T>(IReadOnlyList<T> arr)
        {
            if (arr.Count == 0) throw new Exception("Rng.pick: leeres Array");
            return arr[(int)Math.Floor(next() * arr.Count)];
        }

        public List<T> shuffle<T>(List<T> arr)
        {
            for (int i = arr.Count - 1; i > 0; i--)
            {
                int j = (int)Math.Floor(next() * (i + 1));
                T t = arr[i];
                arr[i] = arr[j];
                arr[j] = t;
            }
            return arr;
        }

        public T[] shuffle<T>(T[] arr)
        {
            for (int i = arr.Length - 1; i > 0; i--)
            {
                int j = (int)Math.Floor(next() * (i + 1));
                T t = arr[i];
                arr[i] = arr[j];
                arr[j] = t;
            }
            return arr;
        }

        public T weighted<T>(IReadOnlyList<T> items, Func<T, double> weight)
        {
            double total = 0;
            for (int i = 0; i < items.Count; i++) total += U.Max(0, weight(items[i]));
            if (total <= 0) return pick(items);
            double r = next() * total;
            for (int i = 0; i < items.Count; i++)
            {
                r -= U.Max(0, weight(items[i]));
                if (r <= 0) return items[i];
            }
            return items[items.Count - 1];
        }
    }
}

// Referenzwerte (Node, `node --import tsx` gegen src/core/rng.ts) – die C#-Ergebnisse müssen exakt gleich sein:
// hashString("KERNWACHT") = 772350587
// hashString("") = 2872998923
// hashString("Grüße-ß") = 2698663573
// mix32(0) = 0; mix32(123456789) = 3126909082; mix32(-1) = 2180083513; mix32(4294967296.7) = 0
// hashSeed(123, "layout") = 814437203
// hashSeed(123, 7) = 2642857347
// hashSeed(4294967295, "combat") = 4221233696
// step(0) = [0.26642920868471265,1831565813]
// new Rng(42).next() x5 = 0.6011037519201636, 0.44829055899754167, 0.8524657934904099, 0.6697340414393693, 0.17481389874592423; state danach = 567894515
// new Rng(hashString("KERNWACHT")): int(1,6) x5 = 4, 5, 2, 5, 1
// new Rng(7): range(2,5)=2.0351142594590783, chance(0.5)=true, pick([a..e])=e, shuffle([1..6])=6,1,4,2,3,5, weighted=z, state=3599190436
//   (weighted über ['x','y','z'] mit Gewichten 1/2/3; Aufrufe in genau dieser Reihenfolge auf demselben Rng)
// new Rng(-5).state = 4294967291
// randomSeedText(12345) = Y5CK-2H66; randomSeedText(0) = WAS9-8JVZ
// normalizeSeedText("  ab  cd\tef ") = AB-CD-EF; normalizeSeedText("   ") = RIFT-0000
// dailySeedText(2026-03-05) = DAILY-20260305
// Math.hypot(3,4)=5; Math.hypot(0.1,0.7)=0.7071067811865476; Math.hypot(1.3,2.9,0.2)=3.184336665618132
//   (naiv Math.sqrt(0.1*0.1+0.7*0.7)=0.7071067811865475 – deshalb immer U.Hypot statt eigener Formel)
