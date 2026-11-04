// Übersetzung von config/mastery.ts.
// 3.0 Turm-Meisterschaft (Helden-Endlos): Jeder Turmtyp sammelt dauerhaft Abschüsse über alle Läufe.
// Rang r ab 50·r² Abschüssen (50, 200, 450, 800 …), höchstens Rang 20; je Rang +1 % Schaden für diesen Turmtyp.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace Kernwacht.Sim
{
    /// <summary>TS: Ad-hoc-Objekt <c>MASTERY</c> (config/mastery.ts).</summary>
    public class MasteryConstants
    {
        public double perRank;
        public double maxRank;
        public double killsBase;
    }

    public static partial class Mastery
    {
        public static readonly MasteryConstants MASTERY = new MasteryConstants { perRank = 0.01, maxRank = 20, killsBase = 50 };

        public static double masteryRank(double kills)
        {
            return U.Min(MASTERY.maxRank, Math.Floor(Math.Sqrt(U.Max(0, kills) / MASTERY.killsBase)));
        }

        public static double? masteryNext(double kills)
        {
            double r = masteryRank(kills);
            return r >= MASTERY.maxRank ? (double?)null : MASTERY.killsBase * Math.Pow(r + 1, 2);
        }

        /// <summary>
        /// TS: <c>sanitizeMastery(raw: unknown)</c>. Akzeptiert den neutralen JSON-Baum (Dictionary&lt;string, object&gt;),
        /// ein Dictionary&lt;string, double&gt; oder jedes andere IDictionary; alles andere ergibt ein leeres Ergebnis.
        /// PORT: Listen (JS-Arrays sind auch Objekte) werden mit Index-Schlüsseln "0", "1", … behandelt.
        /// </summary>
        public static Dictionary<string, double> sanitizeMastery(object raw)
        {
            Dictionary<string, double> @out = new Dictionary<string, double>();
            if (raw == null || raw is string || raw is double || raw is bool) return @out;
            List<KeyValuePair<string, object>> entries = new List<KeyValuePair<string, object>>();
            IDictionary dict = raw as IDictionary;
            IList list = raw as IList;
            if (dict != null)
            {
                foreach (DictionaryEntry e in dict) entries.Add(new KeyValuePair<string, object>(Convert.ToString(e.Key, CultureInfo.InvariantCulture), e.Value));
            }
            else if (list != null)
            {
                for (int i = 0; i < list.Count; i++) entries.Add(new KeyValuePair<string, object>(i.ToString(CultureInfo.InvariantCulture), list[i]));
            }
            else return @out;   // PORT: andere Objekttypen haben im C#-Port keine aufzählbaren Einträge
            foreach (KeyValuePair<string, object> kv in entries)
            {
                double n = Math.Floor(jsNumber(kv.Value));
                if (U.IsFinite(n) && n > 0) @out[kv.Key] = U.Min(1e9, n);
            }
            return @out;
        }

        /// <summary>JS <c>Number(v)</c> für Werte aus einem JSON-Baum.</summary>
        private static double jsNumber(object v)
        {
            if (v == null) return 0;   // Number(null) = 0
            if (v is double d) return d;
            if (v is int i) return i;
            if (v is long l) return l;
            if (v is float f) return f;
            if (v is bool b) return b ? 1 : 0;
            if (v is string s) return U.ParseNumber(s);
            IList list = v as IList;
            if (list != null)
            {
                // Number([]) = 0, Number([x]) = Number(String(x)), sonst NaN
                if (list.Count == 0) return 0;
                if (list.Count == 1)
                {
                    object x = list[0];
                    if (x == null) return 0;
                    if (x is string xs) return U.ParseNumber(xs);
                    if (x is double || x is int || x is long || x is float) return jsNumber(x);
                    return double.NaN;   // PORT: verschachtelte Werte – String(x) ergibt keine Zahl (Ausnahme [[n]] wird nicht nachgebildet)
                }
                return double.NaN;
            }
            return double.NaN;
        }
    }
}
