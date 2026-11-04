// Gemeinsame Hilfen für die 1:1-Übersetzung TS → C# (siehe PORTING.md).
// Alles hier bildet JavaScript-Semantik exakt nach (Math.round, Math.min/max mit NaN/-0, Math.hypot wie V8,
// ToInt32/ToUint32/Math.imul, stabiles Array.prototype.sort, splice/slice, String(number), toFixed,
// toLocaleString('de-DE')). Zahlen ↔ Text laufen über exakte BigInteger-Algorithmen und hängen damit nicht
// von der Formatierungs-/Parse-Genauigkeit der Laufzeit (Mono/IL2CPP) ab.
//
// Ausnahme von „keine Reflection“: DeepClone und ToJson/FromJson nutzen Reflection über ÖFFENTLICHE
// Instanzfelder (IL2CPP-tauglich; ggf. per link.xml vor dem Stripping schützen).

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Text;

namespace Kernwacht.Sim
{
    public static class U
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // =====================================================================================
        // Truthiness und ||
        // =====================================================================================

        /// <summary>JS-Truthiness einer Zahl: x != 0 und nicht NaN.</summary>
        public static bool Truthy(double x) { return x != 0 && !double.IsNaN(x); }
        /// <summary>undefined/null → false, sonst Zahl-Truthiness.</summary>
        public static bool Truthy(double? x) { return x.HasValue && Truthy(x.Value); }
        public static bool Truthy(bool? x) { return x == true; }
        /// <summary>JS-Truthiness eines Strings: nicht null und nicht leer.</summary>
        public static bool Truthy(string s) { return !string.IsNullOrEmpty(s); }
        /// <summary>Allgemeine JS-Truthiness (null → false, Zahl/Bool/String wie JS, sonst true).</summary>
        public static bool Truthy(object o)
        {
            if (o == null) return false;
            if (o is double d) return Truthy(d);
            if (o is bool b) return b;
            if (o is string s) return s.Length > 0;
            if (o is int i) return i != 0;
            if (o is float f) return f != 0 && !float.IsNaN(f);
            if (o is long l) return l != 0;
            return true;
        }

        /// <summary>JS <c>x || y</c> für Zahlen.</summary>
        public static double Or(double x, double y) { return Truthy(x) ? x : y; }
        /// <summary>JS <c>x || y</c> mit optionalem x.</summary>
        public static double Or(double? x, double y) { return Truthy(x) ? x.Value : y; }
        public static double? Or(double? x, double? y) { return Truthy(x) ? x : y; }
        /// <summary>JS <c>s || t</c> für Strings ('' und null sind falsy).</summary>
        public static string Or(string x, string y) { return !string.IsNullOrEmpty(x) ? x : y; }
        /// <summary>JS <c>a || b</c> für Objekte/Listen (nur null ist falsy – leere Listen sind truthy).</summary>
        public static T Or<T>(T x, T y) where T : class { return x != null ? x : y; }
        /// <summary>JS <c>a || b</c> mit optionalem bool.</summary>
        public static bool Or(bool? x, bool y) { return x == true || y; }

        // =====================================================================================
        // Math
        // =====================================================================================

        public static bool IsNegZero(double x) { return x == 0 && BitConverter.DoubleToInt64Bits(x) < 0; }

        /// <summary>JS Math.round: .5 rundet immer nach oben (−2,5 → −2), −0,4 → −0. Nie Math.Round verwenden!</summary>
        public static double Round(double x)
        {
            if (double.IsNaN(x) || double.IsInfinity(x)) return x;
            double r = Math.Floor(x);
            if (x - r >= 0.5) r += 1;
            if (r == 0 && (x < 0 || IsNegZero(x))) return -0.0;
            return r;
        }

        /// <summary>JS Math.sign (NaN → NaN, ±0 bleibt ±0).</summary>
        public static double Sign(double x)
        {
            if (double.IsNaN(x)) return double.NaN;
            if (x > 0) return 1;
            if (x < 0) return -1;
            return x;
        }

        /// <summary>JS Math.trunc.</summary>
        public static double Trunc(double x) { return Math.Truncate(x); }

        /// <summary>JS Math.min mit zwei Argumenten (NaN gewinnt, −0 &lt; +0).</summary>
        public static double Min(double a, double b)
        {
            if (double.IsNaN(a) || double.IsNaN(b)) return double.NaN;
            if (a < b) return a;
            if (b < a) return b;
            return IsNegZero(a) ? a : b;
        }

        /// <summary>JS Math.max mit zwei Argumenten (NaN gewinnt, +0 &gt; −0).</summary>
        public static double Max(double a, double b)
        {
            if (double.IsNaN(a) || double.IsNaN(b)) return double.NaN;
            if (a > b) return a;
            if (b > a) return b;
            return IsNegZero(a) ? b : a;
        }

        /// <summary>JS Math.min(...v); ohne Argumente +Infinity.</summary>
        public static double Min(params double[] v)
        {
            double r = double.PositiveInfinity;
            for (int i = 0; i < v.Length; i++) r = Min(r, v[i]);
            return r;
        }

        /// <summary>JS Math.max(...v); ohne Argumente −Infinity.</summary>
        public static double Max(params double[] v)
        {
            double r = double.NegativeInfinity;
            for (int i = 0; i < v.Length; i++) r = Max(r, v[i]);
            return r;
        }

        /// <summary>Math.max(lo, Math.min(hi, x)) in JS-Semantik. Wo TS die Reihenfolge anders schreibt, wörtlich übersetzen.</summary>
        public static double Clamp(double x, double lo, double hi) { return Max(lo, Min(hi, x)); }

        /// <summary>Number.isFinite.</summary>
        public static bool IsFinite(double x) { return !double.IsNaN(x) && !double.IsInfinity(x); }
        /// <summary>Number.isFinite(undefined) = false.</summary>
        public static bool IsFinite(double? x) { return x.HasValue && IsFinite(x.Value); }

        /// <summary>
        /// Math.hypot exakt wie V8 (Normierung auf das Maximum + Kahan-Summation). Liefert teils andere Bits als
        /// Math.Sqrt(a*a+b*b) – deshalb immer diese Funktion verwenden.
        /// </summary>
        public static double Hypot(double a, double b)
        {
            bool nan = false;
            double max = 0, x0 = 0, x1 = 0;
            if (double.IsNaN(a)) nan = true; else { x0 = Math.Abs(a); if (x0 > max) max = x0; }
            if (double.IsNaN(b)) nan = true; else { x1 = Math.Abs(b); if (x1 > max) max = x1; }
            if (max == double.PositiveInfinity) return double.PositiveInfinity;
            if (nan) return double.NaN;
            if (max == 0) return 0;
            double sum = 0, compensation = 0;
            double n = x0 / max;
            double summand = n * n - compensation;
            double preliminary = sum + summand;
            compensation = (preliminary - sum) - summand;
            sum = preliminary;
            n = x1 / max;
            summand = n * n - compensation;
            preliminary = sum + summand;
            compensation = (preliminary - sum) - summand;
            sum = preliminary;
            return Math.Sqrt(sum) * max;
        }

        /// <summary>Math.hypot mit beliebig vielen Argumenten (V8-Algorithmus).</summary>
        public static double Hypot(params double[] v)
        {
            int length = v.Length;
            if (length == 0) return 0;
            double[] absValues = new double[length];
            bool oneArgIsNaN = false;
            double max = 0;
            for (int i = 0; i < length; i++)
            {
                double value = v[i];
                if (double.IsNaN(value)) oneArgIsNaN = true;
                else
                {
                    double absValue = Math.Abs(value);
                    absValues[i] = absValue;
                    if (absValue > max) max = absValue;
                }
            }
            if (max == double.PositiveInfinity) return double.PositiveInfinity;
            if (oneArgIsNaN) return double.NaN;
            if (max == 0) return 0;
            double sum = 0, compensation = 0;
            for (int i = 0; i < length; i++)
            {
                double n = absValues[i] / max;
                double summand = n * n - compensation;
                double preliminary = sum + summand;
                compensation = (preliminary - sum) - summand;
                sum = preliminary;
            }
            return Math.Sqrt(sum) * max;
        }

        // =====================================================================================
        // Ganzzahl-Bitoperationen (JS-Semantik)
        // =====================================================================================

        /// <summary>JS ToInt32 (x | 0): NaN/±∞ → 0, abschneiden, modulo 2^32, vorzeichenbehaftet.</summary>
        public static int ToInt32(double x)
        {
            if (double.IsNaN(x) || double.IsInfinity(x)) return 0;
            double t = Math.Truncate(x);
            if (t >= -2147483648.0 && t <= 2147483647.0) return (int)t;
            double m = t % 4294967296.0;
            if (m < 0) m += 4294967296.0;
            unchecked { return (int)(uint)m; }
        }

        /// <summary>JS ToUint32 (x &gt;&gt;&gt; 0).</summary>
        public static uint ToUint32(double x)
        {
            unchecked { return (uint)ToInt32(x); }
        }

        /// <summary>Math.imul.</summary>
        public static int Imul(double a, double b)
        {
            unchecked { return ToInt32(a) * ToInt32(b); }
        }

        /// <summary>JS <c>x &gt;&gt;&gt; n</c>.</summary>
        public static double Ushr(double x, double n) { return ToUint32(x) >> (int)(ToUint32(n) & 31); }
        /// <summary>JS <c>x &gt;&gt; n</c>.</summary>
        public static double Shr(double x, double n) { return ToInt32(x) >> (int)(ToUint32(n) & 31); }
        /// <summary>JS <c>x &lt;&lt; n</c>.</summary>
        public static double Shl(double x, double n) { unchecked { return ToInt32(x) << (int)(ToUint32(n) & 31); } }
        /// <summary>JS <c>a &amp; b</c>.</summary>
        public static double BitAnd(double a, double b) { return ToInt32(a) & ToInt32(b); }
        /// <summary>JS <c>a | b</c>.</summary>
        public static double BitOr(double a, double b) { return ToInt32(a) | ToInt32(b); }
        /// <summary>JS <c>a ^ b</c>.</summary>
        public static double BitXor(double a, double b) { return ToInt32(a) ^ ToInt32(b); }
        /// <summary>JS <c>~a</c>.</summary>
        public static double BitNot(double a) { return ~ToInt32(a); }

        // =====================================================================================
        // Sortieren (stabil wie Array.prototype.sort)
        // =====================================================================================

        /// <summary>
        /// Stabile Sortierung in place (wie JS Array.prototype.sort mit Vergleichsfunktion), gibt dieselbe Liste zurück.
        /// Ergebnis &gt; 0 → a hinter b; NaN zählt wie 0. Für konsistente Vergleicher ist das Ergebnis identisch mit JS.
        /// PORT: Bei inkonsistenten/zufälligen Vergleichern (oder Seiteneffekten im Vergleicher) kann die Reihenfolge
        /// von V8 (TimSort) abweichen, weil die Vergleiche in anderer Folge aufgerufen werden.
        /// </summary>
        public static List<T> StableSort<T>(List<T> list, Func<T, T, double> cmp)
        {
            int n = list.Count;
            if (n < 2) return list;
            T[] a = list.ToArray();
            T[] tmp = new T[n];
            MergeSort(a, tmp, 0, n, cmp);
            for (int i = 0; i < n; i++) list[i] = a[i];
            return list;
        }

        /// <summary>Stabile Sortierung eines Arrays in place.</summary>
        public static T[] StableSort<T>(T[] arr, Func<T, T, double> cmp)
        {
            if (arr.Length < 2) return arr;
            T[] tmp = new T[arr.Length];
            MergeSort(arr, tmp, 0, arr.Length, cmp);
            return arr;
        }

        private static void MergeSort<T>(T[] a, T[] tmp, int lo, int hi, Func<T, T, double> cmp)
        {
            int len = hi - lo;
            if (len < 2) return;
            if (len <= 12)
            {
                for (int i = lo + 1; i < hi; i++)
                {
                    T x = a[i];
                    int j = i - 1;
                    while (j >= lo && cmp(a[j], x) > 0)
                    {
                        a[j + 1] = a[j];
                        j--;
                    }
                    a[j + 1] = x;
                }
                return;
            }
            int mid = lo + (len >> 1);
            MergeSort(a, tmp, lo, mid, cmp);
            MergeSort(a, tmp, mid, hi, cmp);
            if (!(cmp(a[mid - 1], a[mid]) > 0)) return;   // schon geordnet
            Array.Copy(a, lo, tmp, lo, len);
            int l = lo, r = mid, k = lo;
            while (l < mid && r < hi)
            {
                if (cmp(tmp[l], tmp[r]) > 0) a[k++] = tmp[r++];
                else a[k++] = tmp[l++];
            }
            while (l < mid) a[k++] = tmp[l++];
            while (r < hi) a[k++] = tmp[r++];
        }

        /// <summary>JS <c>arr.sort()</c> ohne Vergleicher auf Strings: UTF-16-Codeeinheiten (ordinal).</summary>
        public static List<string> SortDefault(List<string> list)
        {
            return StableSort(list, (a, b) => string.CompareOrdinal(a, b));
        }

        /// <summary>JS <c>arr.sort()</c> ohne Vergleicher auf Zahlen: Achtung, JS vergleicht die Texte (10 &lt; 9)!</summary>
        public static List<double> SortDefault(List<double> list)
        {
            return StableSort(list, (a, b) => string.CompareOrdinal(Fmt(a), Fmt(b)));
        }

        // =====================================================================================
        // Array-Hilfen (JS-Semantik für negative/zu große Indizes)
        // =====================================================================================

        private static int RelIndex(double rel, int len)
        {
            double r = double.IsNaN(rel) ? 0 : Math.Truncate(rel);
            if (r < 0) return (int)Math.Max(len + r, 0);
            return (int)Math.Min(r, len);
        }

        /// <summary>JS <c>arr.splice(start)</c>: entfernt alles ab start, gibt die entfernten Elemente zurück.</summary>
        public static List<T> Splice<T>(List<T> list, double start)
        {
            return Splice(list, start, double.PositiveInfinity);
        }

        /// <summary>JS <c>arr.splice(start, deleteCount, ...items)</c>.</summary>
        public static List<T> Splice<T>(List<T> list, double start, double deleteCount, params T[] items)
        {
            int len = list.Count;
            int s = RelIndex(start, len);
            double dc = double.IsNaN(deleteCount) ? 0 : Math.Truncate(deleteCount);
            int d = (int)Math.Min(Math.Max(dc, 0), len - s);
            List<T> removed = list.GetRange(s, d);
            list.RemoveRange(s, d);
            if (items != null && items.Length > 0) list.InsertRange(s, items);
            return removed;
        }

        /// <summary>JS <c>arr.slice(start, end)</c> (neue Liste).</summary>
        public static List<T> Slice<T>(List<T> list, double start = 0, double? end = null)
        {
            int len = list.Count;
            int s = RelIndex(start, len);
            int e = end.HasValue ? RelIndex(end.Value, len) : len;
            return e > s ? list.GetRange(s, e - s) : new List<T>();
        }

        /// <summary>JS <c>str.slice(start, end)</c>.</summary>
        public static string Slice(string str, double start = 0, double? end = null)
        {
            int len = str.Length;
            int s = RelIndex(start, len);
            int e = end.HasValue ? RelIndex(end.Value, len) : len;
            return e > s ? str.Substring(s, e - s) : "";
        }

        /// <summary>JS <c>arr.shift()</c>; leere Liste → default (undefined).</summary>
        public static T Shift<T>(List<T> list)
        {
            if (list.Count == 0) return default(T);
            T x = list[0];
            list.RemoveAt(0);
            return x;
        }

        /// <summary>JS <c>arr.pop()</c>; leere Liste → default (undefined).</summary>
        public static T Pop<T>(List<T> list)
        {
            if (list.Count == 0) return default(T);
            T x = list[list.Count - 1];
            list.RemoveAt(list.Count - 1);
            return x;
        }

        /// <summary>JS <c>arr.unshift(...items)</c>; gibt die neue Länge zurück.</summary>
        public static double Unshift<T>(List<T> list, params T[] items)
        {
            list.InsertRange(0, items);
            return list.Count;
        }

        /// <summary>JS <c>arr.push(...items)</c>; gibt die neue Länge zurück.</summary>
        public static double Push<T>(List<T> list, params T[] items)
        {
            list.AddRange(items);
            return list.Count;
        }

        /// <summary>JS <c>new Array(n).fill(v)</c> / <c>Array.from({length: n}, () =&gt; v)</c> für Werte.</summary>
        public static List<T> Fill<T>(double n, T value)
        {
            int c = (int)n;
            List<T> l = new List<T>(c);
            for (int i = 0; i < c; i++) l.Add(value);
            return l;
        }

        /// <summary>JS <c>a.concat(b)</c> (neue Liste).</summary>
        public static List<T> Concat<T>(IEnumerable<T> a, IEnumerable<T> b)
        {
            List<T> l = new List<T>(a);
            l.AddRange(b);
            return l;
        }

        /// <summary>JS <c>arr.includes(x)</c> (SameValueZero: NaN wird gefunden, 0 == −0).</summary>
        public static bool Includes<T>(IEnumerable<T> list, T item)
        {
            if (list == null) return false;
            EqualityComparer<T> eq = EqualityComparer<T>.Default;
            foreach (T x in list) if (eq.Equals(x, item)) return true;
            return false;
        }

        /// <summary>JS <c>str.includes(sub)</c> (ordinal).</summary>
        public static bool Includes(string str, string sub)
        {
            return str.IndexOf(sub, StringComparison.Ordinal) >= 0;
        }

        /// <summary>JS <c>arr.indexOf(x)</c> (strikte Gleichheit); −1 wenn nicht vorhanden.</summary>
        public static double IndexOf<T>(IReadOnlyList<T> list, T item)
        {
            EqualityComparer<T> eq = EqualityComparer<T>.Default;
            for (int i = 0; i < list.Count; i++) if (eq.Equals(list[i], item)) return i;
            return -1;
        }

        /// <summary>JS <c>arr.indexOf(x)</c> für Zahlen (=== : NaN wird nie gefunden).</summary>
        public static double IndexOf(IReadOnlyList<double> list, double item)
        {
            for (int i = 0; i < list.Count; i++) if (list[i] == item) return i;
            return -1;
        }

        /// <summary>JS <c>arr.indexOf(s)</c> für Strings (ordinal).</summary>
        public static double IndexOf(IReadOnlyList<string> list, string item)
        {
            for (int i = 0; i < list.Count; i++) if (string.Equals(list[i], item, StringComparison.Ordinal)) return i;
            return -1;
        }

        /// <summary>JS <c>arr.lastIndexOf(x)</c>.</summary>
        public static double LastIndexOf<T>(IReadOnlyList<T> list, T item)
        {
            EqualityComparer<T> eq = EqualityComparer<T>.Default;
            for (int i = list.Count - 1; i >= 0; i--) if (eq.Equals(list[i], item)) return i;
            return -1;
        }

        /// <summary>JS <c>arr.join(sep)</c>: null → '', Zahlen wie String(x), bool als true/false.</summary>
        public static string Join<T>(IEnumerable<T> items, string sep = ",")
        {
            StringBuilder sb = new StringBuilder();
            bool first = true;
            foreach (T it in items)
            {
                if (!first) sb.Append(sep);
                first = false;
                sb.Append(JoinPart(it));
            }
            return sb.ToString();
        }

        private static string JoinPart(object o)
        {
            if (o == null) return "";
            if (o is string s) return s;
            if (o is double d) return Fmt(d);
            if (o is float f) return Fmt(f);
            if (o is bool b) return b ? "true" : "false";
            if (o is int i) return i.ToString(Inv);
            if (o is long l) return l.ToString(Inv);
            if (o is uint ui) return ui.ToString(Inv);
            return o.ToString();
        }

        /// <summary>JS <c>str.padStart(n, pad)</c>.</summary>
        public static string PadStart(string s, double targetLength, string pad = " ")
        {
            int n = (int)targetLength;
            if (s.Length >= n || string.IsNullOrEmpty(pad)) return s;
            int fill = n - s.Length;
            StringBuilder sb = new StringBuilder(n);
            while (sb.Length < fill) sb.Append(pad);
            sb.Length = fill;
            sb.Append(s);
            return sb.ToString();
        }

        /// <summary>JS <c>str.padEnd(n, pad)</c>.</summary>
        public static string PadEnd(string s, double targetLength, string pad = " ")
        {
            int n = (int)targetLength;
            if (s.Length >= n || string.IsNullOrEmpty(pad)) return s;
            StringBuilder sb = new StringBuilder(n);
            sb.Append(s);
            while (sb.Length < n) sb.Append(pad);
            sb.Length = n;
            return sb.ToString();
        }

        /// <summary>JS <c>str.repeat(n)</c>.</summary>
        public static string Repeat(string s, double count)
        {
            int c = (int)count;
            StringBuilder sb = new StringBuilder(s.Length * Math.Max(c, 0));
            for (int i = 0; i < c; i++) sb.Append(s);
            return sb.ToString();
        }

        // =====================================================================================
        // Dictionary-Hilfen
        // =====================================================================================

        /// <summary>TS <c>d[key] ?? def</c> (d darf null sein).</summary>
        public static V Get<K, V>(Dictionary<K, V> d, K key, V def = default(V))
        {
            V v;
            if (d != null && key != null && d.TryGetValue(key, out v)) return v;
            return def;
        }

        public static V Get<K, V>(OrderedDict<K, V> d, K key, V def = default(V))
        {
            V v;
            if (d != null && key != null && d.TryGetValue(key, out v)) return v;
            return def;
        }

        /// <summary>TS <c>d[key]</c> bei Zahlwerten: fehlender Schlüssel (oder d == null) → null (undefined).</summary>
        public static double? GetN<K>(Dictionary<K, double> d, K key)
        {
            double v;
            if (d != null && key != null && d.TryGetValue(key, out v)) return v;
            return null;
        }

        public static double? GetN<K>(OrderedDict<K, double> d, K key)
        {
            double v;
            if (d != null && key != null && d.TryGetValue(key, out v)) return v;
            return null;
        }

        /// <summary>TS <c>d[key] = (d[key] ?? 0) + v</c>; gibt den neuen Wert zurück.</summary>
        public static double AddTo<K>(Dictionary<K, double> d, K key, double v)
        {
            double cur;
            double nv = (d.TryGetValue(key, out cur) ? cur : 0) + v;
            d[key] = nv;
            return nv;
        }

        public static double AddTo<K>(OrderedDict<K, double> d, K key, double v)
        {
            double cur;
            double nv = (d.TryGetValue(key, out cur) ? cur : 0) + v;
            d[key] = nv;
            return nv;
        }

        /// <summary>Object.keys(d) als Schnappschuss (Einfügereihenfolge).</summary>
        public static List<K> Keys<K, V>(Dictionary<K, V> d)
        {
            return d == null ? new List<K>() : new List<K>(d.Keys);
        }

        /// <summary>Object.values(d) als Schnappschuss.</summary>
        public static List<V> Values<K, V>(Dictionary<K, V> d)
        {
            return d == null ? new List<V>() : new List<V>(d.Values);
        }

        /// <summary>Object.entries(d) als Schnappschuss.</summary>
        public static List<KeyValuePair<K, V>> Entries<K, V>(Dictionary<K, V> d)
        {
            return d == null ? new List<KeyValuePair<K, V>>() : new List<KeyValuePair<K, V>>(d);
        }

        // =====================================================================================
        // Zahl → Text (JS-exakt)
        // =====================================================================================

        /// <summary>JS <c>String(x)</c> / Template-String <c>${x}</c>: kürzeste Round-trip-Darstellung, Exponent ab 1e21 bzw. unter 1e-6.</summary>
        public static string Fmt(double x)
        {
            if (double.IsNaN(x)) return "NaN";
            if (x == 0) return "0";
            if (double.IsInfinity(x)) return x > 0 ? "Infinity" : "-Infinity";
            if (x == Math.Floor(x) && Math.Abs(x) < 9007199254740992.0) return ((long)x).ToString(Inv);
            string sign = "";
            if (x < 0) { sign = "-"; x = -x; }
            string digits;
            int n;
            Shortest(x, out digits, out n);
            int k = digits.Length;
            string r;
            if (k <= n && n <= 21) r = digits + new string('0', n - k);
            else if (0 < n && n <= 21) r = digits.Substring(0, n) + "." + digits.Substring(n);
            else if (-6 < n && n <= 0) r = "0." + new string('0', -n) + digits;
            else
            {
                int e = n - 1;
                string mant = k == 1 ? digits : digits.Substring(0, 1) + "." + digits.Substring(1);
                r = mant + "e" + (e < 0 ? "-" : "+") + Math.Abs(e).ToString(Inv);
            }
            return sign + r;
        }

        /// <summary>String(undefined) = "undefined".</summary>
        public static string Fmt(double? x) { return x.HasValue ? Fmt(x.Value) : "undefined"; }

        /// <summary>JS <c>x.toFixed(digits)</c> (exakt auf dem Binärwert, Halbe weg von 0; ab 1e21 wie String(x)).</summary>
        public static string Fixed(double x, double digits = 0)
        {
            int f = (int)digits;
            if (f < 0 || f > 100) throw new Exception("toFixed() digits argument must be between 0 and 100");
            if (double.IsNaN(x)) return "NaN";
            if (Math.Abs(x) >= 1e21 || double.IsInfinity(x)) return Fmt(x);
            string s = "";
            if (x < 0) { s = "-"; x = -x; }
            BigInteger m;
            int e;
            bool boundary;
            BigInteger n;
            if (x == 0) n = BigInteger.Zero;
            else
            {
                Decompose(x, out m, out e, out boundary);
                if (e >= 0) n = (m << e) * Pow10(f);
                else
                {
                    int k = -e;
                    n = ((m * Pow10(f)) * 2 + (BigInteger.One << k)) >> (k + 1);
                }
            }
            string ds = n.ToString(Inv);
            if (f > 0)
            {
                if (ds.Length <= f) ds = new string('0', f + 1 - ds.Length) + ds;
                ds = ds.Substring(0, ds.Length - f) + "." + ds.Substring(ds.Length - f);
            }
            return s + ds;
        }

        /// <summary>
        /// JS <c>x.toLocaleString('de-DE')</c> bzw. mit <c>{ maximumFractionDigits: maxFrac, minimumFractionDigits: 0 }</c>:
        /// Tausenderpunkt, Dezimalkomma, Rundung „halfExpand“ auf der kürzesten Dezimaldarstellung, −0 → "-0", ∞ → "∞".
        /// </summary>
        public static string De(double x, double maxFrac = 3)
        {
            if (double.IsNaN(x)) return "NaN";
            bool neg = x < 0 || IsNegZero(x);
            if (double.IsInfinity(x)) return neg ? "-∞" : "∞";
            int f = Math.Max(0, (int)maxFrac);
            double a = Math.Abs(x);
            string digits;
            int n;
            if (a == 0) { digits = "0"; n = 1; }
            else Shortest(a, out digits, out n);
            string intPart, fracPart;
            if (n <= 0) { intPart = "0"; fracPart = new string('0', -n) + digits; }
            else if (n >= digits.Length) { intPart = digits + new string('0', n - digits.Length); fracPart = ""; }
            else { intPart = digits.Substring(0, n); fracPart = digits.Substring(n); }
            if (fracPart.Length > f)
            {
                bool up = fracPart[f] >= '5';
                fracPart = fracPart.Substring(0, f);
                if (up)
                {
                    char[] all = (intPart + fracPart).ToCharArray();
                    int i = all.Length - 1;
                    while (i >= 0)
                    {
                        if (all[i] == '9') { all[i] = '0'; i--; }
                        else { all[i]++; break; }
                    }
                    string sAll = new string(all);
                    if (i < 0) sAll = "1" + sAll;
                    intPart = sAll.Substring(0, sAll.Length - f);
                    fracPart = sAll.Substring(sAll.Length - f);
                }
            }
            fracPart = fracPart.TrimEnd('0');
            intPart = intPart.TrimStart('0');
            if (intPart.Length == 0) intPart = "0";
            StringBuilder sb = new StringBuilder();
            if (neg) sb.Append('-');
            int lead = intPart.Length % 3;
            if (lead == 0) lead = 3;
            sb.Append(intPart, 0, lead);
            for (int p = lead; p < intPart.Length; p += 3)
            {
                sb.Append('.');
                sb.Append(intPart, p, 3);
            }
            if (fracPart.Length > 0) { sb.Append(','); sb.Append(fracPart); }
            return sb.ToString();
        }

        // ---- exakte Zahlenalgorithmen (BigInteger)

        private static readonly BigInteger[] P10Cache = new BigInteger[2200];

        private static BigInteger Pow10(int k)
        {
            if (k < P10Cache.Length)
            {
                BigInteger c = P10Cache[k];
                if (c.IsZero)
                {
                    c = BigInteger.Pow(10, k);
                    P10Cache[k] = c;
                }
                return c;
            }
            return BigInteger.Pow(10, k);
        }

        private static int BitLen(BigInteger v)
        {
            if (v.Sign <= 0) return 0;
            byte[] b = v.ToByteArray();   // little-endian, Zweierkomplement
            int top = b.Length - 1;
            while (top > 0 && b[top] == 0) top--;
            int bits = top * 8;
            int t = b[top];
            while (t > 0) { bits++; t >>= 1; }
            return bits;
        }

        /// <summary>x &gt; 0 endlich → x = m · 2^e; boundary = Mantisse ist Zweierpotenz (unterer Abstand halb so groß).</summary>
        private static void Decompose(double x, out BigInteger m, out int e, out bool boundary)
        {
            long bits = BitConverter.DoubleToInt64Bits(x);
            int be = (int)((bits >> 52) & 0x7FF);
            long frac = bits & 0xFFFFFFFFFFFFFL;
            if (be == 0)
            {
                m = new BigInteger(frac);
                e = -1074;
            }
            else
            {
                m = new BigInteger(frac | (1L << 52));
                e = be - 1075;
            }
            boundary = be > 1 && frac == 0;
        }

        /// <summary>
        /// Kürzeste Dezimalziffern wie ECMAScript Number::toString: digits (ohne Endnullen) und n, sodass
        /// x = 0.digits · 10^n. Bei mehreren Kandidaten gleicher Länge der nächstgelegene (Gleichstand: gerade).
        /// </summary>
        private static void Shortest(double x, out string digits, out int n)
        {
            BigInteger m;
            int e;
            bool boundary;
            Decompose(x, out m, out e, out boundary);
            BigInteger X, D, H, L;
            if (e >= 0)
            {
                BigInteger pe = BigInteger.One << e;
                X = (m << e) * 4;
                D = 4;
                H = pe * 2;
                L = boundary ? pe : H;
            }
            else
            {
                X = m * 4;
                D = (BigInteger.One << -e) * 4;
                H = 2;
                L = boundary ? BigInteger.One : new BigInteger(2);
            }
            bool inclusive = m.IsEven;
            int k = (int)Math.Floor(Math.Log10(x));
            while (!GePow10(X, D, k)) k--;
            while (GePow10(X, D, k + 1)) k++;
            for (int p = 1; p <= 17; p++)
            {
                int sh = k + 1 - p;
                BigInteger num = X, den = D, scale = BigInteger.One;
                if (sh >= 0) den = D * Pow10(sh);
                else { scale = Pow10(-sh); num = X * scale; }
                BigInteger sf = BigInteger.Divide(num, den);
                BigInteger hs = H * scale, ls = L * scale;
                bool found = false;
                BigInteger best = BigInteger.Zero, bestDist = BigInteger.Zero;
                for (int c = 0; c < 2; c++)
                {
                    BigInteger s = c == 0 ? sf : sf + 1;
                    if (s.IsZero) continue;
                    BigInteger diff = s * den - num;
                    bool ok;
                    if (diff.Sign >= 0) ok = inclusive ? diff <= hs : diff < hs;
                    else { BigInteger nd = -diff; ok = inclusive ? nd <= ls : nd < ls; }
                    if (!ok) continue;
                    BigInteger dist = BigInteger.Abs(diff);
                    if (!found || dist < bestDist || (dist == bestDist && s.IsEven))
                    {
                        best = s;
                        bestDist = dist;
                        found = true;
                    }
                }
                if (found)
                {
                    string ds = best.ToString(Inv);
                    n = ds.Length + sh;
                    digits = ds.TrimEnd('0');
                    if (digits.Length == 0) digits = "0";
                    return;
                }
            }
            // Unerreichbar (17 Stellen reichen immer) – Rückfall
            string r = x.ToString("E16", Inv);
            int ePos = r.IndexOf('E');
            digits = r.Substring(0, ePos).Replace(".", "").TrimEnd('0');
            n = int.Parse(r.Substring(ePos + 1), Inv) + 1;
        }

        private static bool GePow10(BigInteger X, BigInteger D, int kk)
        {
            return kk >= 0 ? X >= D * Pow10(kk) : X * Pow10(-kk) >= D;
        }

        // =====================================================================================
        // Text → Zahl (exakt, korrekt gerundet)
        // =====================================================================================

        private static readonly BigInteger Two53 = BigInteger.One << 53;
        private static readonly BigInteger Two54 = BigInteger.One << 54;

        /// <summary>num/den (beide &gt; 0) korrekt gerundet (round half even) als double.</summary>
        private static double RatioToDouble(BigInteger num, BigInteger den)
        {
            int s = BitLen(num) - BitLen(den) - 54;
            BigInteger q = RatioShift(num, den, s);
            while (q >= Two54) { s++; q = RatioShift(num, den, s); }
            while (q < Two53) { s--; q = RatioShift(num, den, s); }
            int e2 = s + 1;
            if (e2 < -1074) { s = -1075; e2 = -1074; }
            BigInteger rem;
            if (s >= 0) q = BigInteger.DivRem(num, den << s, out rem);
            else q = BigInteger.DivRem(num << -s, den, out rem);
            bool roundBit = !q.IsEven;
            BigInteger mant = q >> 1;
            bool sticky = !rem.IsZero;
            if (roundBit && (sticky || !mant.IsEven)) mant += 1;
            if (mant == Two53) { mant >>= 1; e2 += 1; }
            long mm = (long)mant;
            long bits;
            if (mm >= (1L << 52))
            {
                int be = e2 + 1075;
                if (be >= 2047) return double.PositiveInfinity;
                bits = ((long)be << 52) | (mm & 0xFFFFFFFFFFFFFL);
            }
            else bits = mm;   // subnormal
            return BitConverter.Int64BitsToDouble(bits);
        }

        private static BigInteger RatioShift(BigInteger num, BigInteger den, int s)
        {
            return s >= 0 ? BigInteger.Divide(num, den << s) : BigInteger.Divide(num << -s, den);
        }

        /// <summary>Ziffernfolge · 10^exp10 korrekt gerundet.</summary>
        private static double DecimalToDouble(bool negative, string digitStr, long exp10)
        {
            int a = 0;
            while (a < digitStr.Length && digitStr[a] == '0') a++;
            string ds = digitStr.Substring(a);
            double r;
            if (ds.Length == 0) r = 0;
            else
            {
                long mag = ds.Length + exp10;   // Wert < 10^mag
                if (mag < -330) r = 0;
                else if (mag - 1 > 309) r = double.PositiveInfinity;
                else
                {
                    BigInteger dm = BigInteger.Parse(ds, NumberStyles.None, Inv);
                    int e = (int)exp10;
                    r = e >= 0 ? RatioToDouble(dm * Pow10(e), BigInteger.One) : RatioToDouble(dm, Pow10(-e));
                }
            }
            return negative ? -r : r;
        }

        private static bool IsJsWs(char c)
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

        /// <summary>
        /// JS <c>Number(str)</c>: Leerraum wird abgeschnitten, '' → 0, "Infinity", 0x/0o/0b, sonst Dezimalliteral; ungültig → NaN.
        /// null → NaN (wie undefined).
        /// </summary>
        public static double ParseNumber(string str)
        {
            if (str == null) return double.NaN;
            int a = 0, b = str.Length;
            while (a < b && IsJsWs(str[a])) a++;
            while (b > a && IsJsWs(str[b - 1])) b--;
            string t = str.Substring(a, b - a);
            if (t.Length == 0) return 0;
            if (t == "Infinity" || t == "+Infinity") return double.PositiveInfinity;
            if (t == "-Infinity") return double.NegativeInfinity;
            if (t.Length > 2 && t[0] == '0')
            {
                int radix = 0;
                char c1 = t[1];
                if (c1 == 'x' || c1 == 'X') radix = 16;
                else if (c1 == 'o' || c1 == 'O') radix = 8;
                else if (c1 == 'b' || c1 == 'B') radix = 2;
                if (radix != 0)
                {
                    BigInteger v = BigInteger.Zero;
                    for (int i = 2; i < t.Length; i++)
                    {
                        int d = HexVal(t[i]);
                        if (d < 0 || d >= radix) return double.NaN;
                        v = v * radix + d;
                    }
                    return v.IsZero ? 0 : RatioToDouble(v, BigInteger.One);
                }
            }
            double res;
            int end;
            if (!TryScanDecimal(t, 0, out res, out end) || end != t.Length) return double.NaN;
            return res;
        }

        private static int HexVal(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return -1;
        }

        /// <summary>Liest [+-]Ziffern[.Ziffern][e[+-]Ziffern] ab pos (mindestens eine Mantissenziffer).</summary>
        private static bool TryScanDecimal(string s, int pos, out double value, out int end)
        {
            value = double.NaN;
            end = pos;
            int i = pos;
            bool neg = false;
            if (i < s.Length && (s[i] == '+' || s[i] == '-')) { neg = s[i] == '-'; i++; }
            StringBuilder digits = new StringBuilder();
            int intDigits = 0, fracDigits = 0;
            while (i < s.Length && s[i] >= '0' && s[i] <= '9') { digits.Append(s[i]); i++; intDigits++; }
            if (i < s.Length && s[i] == '.')
            {
                i++;
                while (i < s.Length && s[i] >= '0' && s[i] <= '9') { digits.Append(s[i]); i++; fracDigits++; }
            }
            if (intDigits + fracDigits == 0) return false;
            long exp = 0;
            if (i < s.Length && (s[i] == 'e' || s[i] == 'E'))
            {
                int j = i + 1;
                bool eneg = false;
                if (j < s.Length && (s[j] == '+' || s[j] == '-')) { eneg = s[j] == '-'; j++; }
                int startDigits = j;
                while (j < s.Length && s[j] >= '0' && s[j] <= '9')
                {
                    if (exp < 100000000) exp = exp * 10 + (s[j] - '0');
                    j++;
                }
                if (j > startDigits)
                {
                    if (eneg) exp = -exp;
                    i = j;
                }
                else exp = 0;   // kein gültiger Exponent → 'e' gehört nicht mehr zur Zahl
            }
            value = DecimalToDouble(neg, digits.ToString(), exp - fracDigits);
            end = i;
            return true;
        }

        // =====================================================================================
        // DeepClone (ersetzt JSON.parse(JSON.stringify(x)) und structuredClone)
        // =====================================================================================

        private static readonly Dictionary<Type, FieldInfo[]> FieldCache = new Dictionary<Type, FieldInfo[]>();
        private static readonly Dictionary<Type, MethodInfo> AddMethodCache = new Dictionary<Type, MethodInfo>();

        private static FieldInfo[] PublicFields(Type t)
        {
            FieldInfo[] fs;
            lock (FieldCache)
            {
                if (!FieldCache.TryGetValue(t, out fs))
                {
                    List<FieldInfo> l = new List<FieldInfo>();
                    foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (f.IsLiteral) continue;
                        l.Add(f);
                    }
                    fs = l.ToArray();
                    FieldCache[t] = fs;
                }
            }
            return fs;
        }

        private static FieldInfo FindField(Type t, string name)
        {
            FieldInfo[] fs = PublicFields(t);
            for (int i = 0; i < fs.Length; i++) if (fs[i].Name == name) return fs[i];
            return null;
        }

        private static bool IsGenericOf(Type t, Type genericDef)
        {
            return t.IsGenericType && t.GetGenericTypeDefinition() == genericDef;
        }

        private static MethodInfo HashSetAdd(Type t)
        {
            MethodInfo mi;
            lock (AddMethodCache)
            {
                if (!AddMethodCache.TryGetValue(t, out mi))
                {
                    mi = t.GetMethod("Add", new Type[] { t.GetGenericArguments()[0] });
                    AddMethodCache[t] = mi;
                }
            }
            return mi;
        }

        /// <summary>
        /// Tiefe Kopie über öffentliche Felder (Klassen), List, Dictionary, OrderedDict, HashSet, Arrays.
        /// Primitive, Strings und Enums werden übernommen. Zyklen werden nicht unterstützt (wie JSON).
        /// PORT: anders als ein JSON-Round-trip bleiben NaN/Infinity erhalten (JSON machte daraus null).
        /// </summary>
        public static T DeepClone<T>(T obj)
        {
            return (T)CloneAny(obj);
        }

        private static object CloneAny(object o)
        {
            if (o == null) return null;
            Type t = o.GetType();
            if (t.IsPrimitive || t.IsEnum || o is string || o is decimal) return o;
            if (t.IsValueType) return o;   // Structs: Wertkopie
            if (o is Delegate) return o;
            Array arr = o as Array;
            if (arr != null)
            {
                Type et = t.GetElementType();
                if (et.IsPrimitive || et == typeof(string)) return arr.Clone();
                Array c = Array.CreateInstance(et, arr.Length);
                for (int i = 0; i < arr.Length; i++) c.SetValue(CloneAny(arr.GetValue(i)), i);
                return c;
            }
            IOrderedDictAccess od = o as IOrderedDictAccess;
            if (od != null)
            {
                IOrderedDictAccess c = (IOrderedDictAccess)Activator.CreateInstance(t);
                foreach (KeyValuePair<object, object> kv in od.RawPairs()) c.RawSet(kv.Key, CloneAny(kv.Value));
                return c;
            }
            IDictionary dict = o as IDictionary;
            if (dict != null)
            {
                IDictionary c = (IDictionary)Activator.CreateInstance(t);
                foreach (DictionaryEntry e in dict) c[CloneAny(e.Key)] = CloneAny(e.Value);
                return c;
            }
            IList list = o as IList;
            if (list != null)
            {
                IList c = (IList)Activator.CreateInstance(t);
                for (int i = 0; i < list.Count; i++) c.Add(CloneAny(list[i]));
                return c;
            }
            if (IsGenericOf(t, typeof(HashSet<>)))
            {
                object c = Activator.CreateInstance(t);
                MethodInfo add = HashSetAdd(t);
                foreach (object x in (IEnumerable)o) add.Invoke(c, new object[] { CloneAny(x) });
                return c;
            }
            object copy = Activator.CreateInstance(t);
            FieldInfo[] fs = PublicFields(t);
            for (int i = 0; i < fs.Length; i++) fs[i].SetValue(copy, CloneAny(fs[i].GetValue(o)));
            return copy;
        }

        // =====================================================================================
        // JSON (Spielstände) – Format wie JSON.stringify, Lesen mit exakter Zahlenumwandlung
        // =====================================================================================

        /// <summary>
        /// Serialisiert wie JSON.stringify: Klassen → Objekt mit öffentlichen Feldern (null-Felder werden wie undefined
        /// weggelassen), List/Array/HashSet → Array, Dictionary/OrderedDict → Objekt (Zahlschlüssel als String(x)),
        /// Zahlen wie String(x), NaN/±Infinity → null.
        /// </summary>
        public static string ToJson(object o)
        {
            StringBuilder sb = new StringBuilder();
            WriteJson(sb, o);
            return sb.ToString();
        }

        private static void WriteJson(StringBuilder sb, object o)
        {
            if (o == null) { sb.Append("null"); return; }
            if (o is string str) { WriteJsonString(sb, str); return; }
            if (o is bool b) { sb.Append(b ? "true" : "false"); return; }
            if (o is double d) { WriteJsonNumber(sb, d); return; }
            if (o is float fl) { WriteJsonNumber(sb, fl); return; }
            if (o is int i32) { sb.Append(i32.ToString(Inv)); return; }
            if (o is long i64) { sb.Append(i64.ToString(Inv)); return; }
            if (o is uint u32) { sb.Append(u32.ToString(Inv)); return; }
            if (o is short || o is ushort || o is byte || o is sbyte || o is ulong || o is decimal)
            {
                WriteJsonNumber(sb, Convert.ToDouble(o, Inv));
                return;
            }
            if (o is char ch) { WriteJsonString(sb, ch.ToString()); return; }
            Type t = o.GetType();
            if (t.IsEnum) { sb.Append(Convert.ToInt64(o, Inv).ToString(Inv)); return; }
            if (o is Delegate) { sb.Append("null"); return; }
            IOrderedDictAccess od = o as IOrderedDictAccess;
            if (od != null)
            {
                sb.Append('{');
                bool first = true;
                foreach (KeyValuePair<object, object> kv in od.RawPairs())
                {
                    if (!first) sb.Append(',');
                    first = false;
                    WriteJsonString(sb, KeyToString(kv.Key));
                    sb.Append(':');
                    WriteJson(sb, kv.Value);
                }
                sb.Append('}');
                return;
            }
            IDictionary dict = o as IDictionary;
            if (dict != null)
            {
                sb.Append('{');
                bool first = true;
                foreach (DictionaryEntry e in dict)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    WriteJsonString(sb, KeyToString(e.Key));
                    sb.Append(':');
                    WriteJson(sb, e.Value);
                }
                sb.Append('}');
                return;
            }
            IEnumerable en = o as IEnumerable;
            if (en != null)
            {
                sb.Append('[');
                bool first = true;
                foreach (object x in en)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    WriteJson(sb, x);
                }
                sb.Append(']');
                return;
            }
            sb.Append('{');
            bool firstField = true;
            FieldInfo[] fs = PublicFields(t);
            for (int k = 0; k < fs.Length; k++)
            {
                object v = fs[k].GetValue(o);
                if (v == null || v is Delegate) continue;
                if (!firstField) sb.Append(',');
                firstField = false;
                WriteJsonString(sb, fs[k].Name);
                sb.Append(':');
                WriteJson(sb, v);
            }
            sb.Append('}');
        }

        private static string KeyToString(object key)
        {
            if (key == null) return "null";
            if (key is string s) return s;
            if (key is double d) return Fmt(d);
            if (key is float f) return Fmt(f);
            if (key is bool b) return b ? "true" : "false";
            return Convert.ToString(key, Inv);
        }

        private static void WriteJsonNumber(StringBuilder sb, double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d)) sb.Append("null");
            else sb.Append(Fmt(d));
        }

        private static void WriteJsonString(StringBuilder sb, string s)
        {
            sb.Append('"');
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) AppendU(sb, c);
                        else if (char.IsHighSurrogate(c))
                        {
                            if (i + 1 < s.Length && char.IsLowSurrogate(s[i + 1])) { sb.Append(c); sb.Append(s[i + 1]); i++; }
                            else AppendU(sb, c);
                        }
                        else if (char.IsLowSurrogate(c)) AppendU(sb, c);
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        private static void AppendU(StringBuilder sb, char c)
        {
            sb.Append("\\u");
            sb.Append(((int)c).ToString("x4", Inv));
        }

        /// <summary>
        /// Parst JSON in einen neutralen Baum: Objekt → Dictionary&lt;string, object&gt; (Einfügereihenfolge),
        /// Array → List&lt;object&gt;, Zahl → double (exakt), String, bool, null.
        /// </summary>
        public static object ParseJson(string json)
        {
            JsonReader r = new JsonReader(json);
            r.SkipWs();
            object v = r.ReadValue();
            r.SkipWs();
            if (r.Pos != json.Length) throw new Exception("JSON: unerwartete Zeichen an Position " + r.Pos);
            return v;
        }

        /// <summary>Deserialisiert JSON in den Typ T (öffentliche Felder; unbekannte Schlüssel werden ignoriert).</summary>
        public static T FromJson<T>(string json)
        {
            return (T)ConvertJson(ParseJson(json), typeof(T));
        }

        public static object FromJson(string json, Type t)
        {
            return ConvertJson(ParseJson(json), t);
        }

        /// <summary>Wandelt einen Baum aus <see cref="ParseJson"/> in den Zieltyp um.</summary>
        public static object ConvertJson(object node, Type t)
        {
            if (t == typeof(object)) return node;
            Type under = Nullable.GetUnderlyingType(t);
            if (node == null)
            {
                if (under != null || !t.IsValueType) return null;
                return Activator.CreateInstance(t);   // 0 / false (JS: null in Rechnungen wie 0)
            }
            if (under != null) t = under;
            if (t == typeof(double)) return NodeToDouble(node);
            if (t == typeof(float)) return (float)NodeToDouble(node);
            if (t == typeof(int)) return ToInt32(NodeToDouble(node));
            if (t == typeof(long)) { double dl = NodeToDouble(node); return IsFinite(dl) ? (long)dl : 0L; }
            if (t == typeof(uint)) return ToUint32(NodeToDouble(node));
            if (t == typeof(bool)) return node is bool nb ? nb : Truthy(node);
            if (t == typeof(string))
            {
                if (node is string ns) return ns;
                if (node is double nd) return Fmt(nd);
                if (node is bool nbb) return nbb ? "true" : "false";
                return null;
            }
            if (t.IsEnum) return Enum.ToObject(t, (long)NodeToDouble(node));
            if (t.IsPrimitive || t == typeof(decimal)) return Convert.ChangeType(NodeToDouble(node), t, Inv);
            if (t.IsArray)
            {
                List<object> src = node as List<object>;
                if (src == null) return null;
                Type et = t.GetElementType();
                Array arr = Array.CreateInstance(et, src.Count);
                for (int i = 0; i < src.Count; i++) arr.SetValue(ConvertJson(src[i], et), i);
                return arr;
            }
            if (t.IsGenericType)
            {
                Type gd = t.GetGenericTypeDefinition();
                Type[] ga = t.GetGenericArguments();
                if (gd == typeof(List<>))
                {
                    List<object> src = node as List<object>;
                    if (src == null) return null;
                    IList list = (IList)Activator.CreateInstance(t);
                    for (int i = 0; i < src.Count; i++) list.Add(ConvertJson(src[i], ga[0]));
                    return list;
                }
                if (gd == typeof(HashSet<>))
                {
                    List<object> src = node as List<object>;
                    if (src == null) return null;
                    object set = Activator.CreateInstance(t);
                    MethodInfo add = HashSetAdd(t);
                    for (int i = 0; i < src.Count; i++) add.Invoke(set, new object[] { ConvertJson(src[i], ga[0]) });
                    return set;
                }
                if (gd == typeof(Dictionary<,>))
                {
                    Dictionary<string, object> src = node as Dictionary<string, object>;
                    if (src == null) return null;
                    IDictionary dict = (IDictionary)Activator.CreateInstance(t);
                    foreach (KeyValuePair<string, object> kv in src) dict[ConvertKey(kv.Key, ga[0])] = ConvertJson(kv.Value, ga[1]);
                    return dict;
                }
                if (gd == typeof(OrderedDict<,>))
                {
                    Dictionary<string, object> src = node as Dictionary<string, object>;
                    if (src == null) return null;
                    IOrderedDictAccess dict = (IOrderedDictAccess)Activator.CreateInstance(t);
                    foreach (KeyValuePair<string, object> kv in src) dict.RawSet(ConvertKey(kv.Key, ga[0]), ConvertJson(kv.Value, ga[1]));
                    return dict;
                }
            }
            Dictionary<string, object> obj = node as Dictionary<string, object>;
            if (obj == null) return null;
            object res = Activator.CreateInstance(t);
            foreach (KeyValuePair<string, object> kv in obj)
            {
                FieldInfo f = FindField(t, kv.Key);
                if (f == null) continue;
                f.SetValue(res, ConvertJson(kv.Value, f.FieldType));
            }
            return res;
        }

        private static double NodeToDouble(object node)
        {
            if (node is double d) return d;
            if (node is bool b) return b ? 1 : 0;
            if (node is string s) return ParseNumber(s);
            return double.NaN;
        }

        private static object ConvertKey(string key, Type kt)
        {
            if (kt == typeof(string) || kt == typeof(object)) return key;
            if (kt == typeof(double)) return ParseNumber(key);
            if (kt == typeof(int)) return ToInt32(ParseNumber(key));
            if (kt == typeof(long)) return (long)ParseNumber(key);
            if (kt.IsEnum) return Enum.Parse(kt, key);
            return Convert.ChangeType(key, kt, Inv);
        }

        private sealed class JsonReader
        {
            private readonly string s;
            public int Pos;

            public JsonReader(string s) { this.s = s; Pos = 0; }

            public void SkipWs()
            {
                while (Pos < s.Length)
                {
                    char c = s[Pos];
                    if (c == ' ' || c == '\t' || c == '\n' || c == '\r') Pos++;
                    else break;
                }
            }

            private Exception Err(string msg) { return new Exception("JSON: " + msg + " an Position " + Pos); }

            public object ReadValue()
            {
                if (Pos >= s.Length) throw Err("unerwartetes Ende");
                char c = s[Pos];
                switch (c)
                {
                    case '{': return ReadObject();
                    case '[': return ReadArray();
                    case '"': return ReadString();
                    case 't': Expect("true"); return true;
                    case 'f': Expect("false"); return false;
                    case 'n': Expect("null"); return null;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9')) return ReadNumber();
                        throw Err("unerwartetes Zeichen '" + c + "'");
                }
            }

            private void Expect(string word)
            {
                if (string.CompareOrdinal(s, Pos, word, 0, word.Length) != 0) throw Err("erwartet " + word);
                Pos += word.Length;
            }

            private Dictionary<string, object> ReadObject()
            {
                Dictionary<string, object> d = new Dictionary<string, object>();
                Pos++;   // {
                SkipWs();
                if (Pos < s.Length && s[Pos] == '}') { Pos++; return d; }
                while (true)
                {
                    SkipWs();
                    if (Pos >= s.Length || s[Pos] != '"') throw Err("Schlüssel erwartet");
                    string key = ReadString();
                    SkipWs();
                    if (Pos >= s.Length || s[Pos] != ':') throw Err("':' erwartet");
                    Pos++;
                    SkipWs();
                    object v = ReadValue();
                    d[key] = v;
                    SkipWs();
                    if (Pos >= s.Length) throw Err("unerwartetes Ende");
                    if (s[Pos] == ',') { Pos++; continue; }
                    if (s[Pos] == '}') { Pos++; return d; }
                    throw Err("',' oder '}' erwartet");
                }
            }

            private List<object> ReadArray()
            {
                List<object> l = new List<object>();
                Pos++;   // [
                SkipWs();
                if (Pos < s.Length && s[Pos] == ']') { Pos++; return l; }
                while (true)
                {
                    SkipWs();
                    l.Add(ReadValue());
                    SkipWs();
                    if (Pos >= s.Length) throw Err("unerwartetes Ende");
                    if (s[Pos] == ',') { Pos++; continue; }
                    if (s[Pos] == ']') { Pos++; return l; }
                    throw Err("',' oder ']' erwartet");
                }
            }

            private string ReadString()
            {
                Pos++;   // "
                StringBuilder sb = new StringBuilder();
                while (true)
                {
                    if (Pos >= s.Length) throw Err("unbeendeter String");
                    char c = s[Pos++];
                    if (c == '"') return sb.ToString();
                    if (c == '\\')
                    {
                        if (Pos >= s.Length) throw Err("unbeendeter String");
                        char e = s[Pos++];
                        switch (e)
                        {
                            case '"': sb.Append('"'); break;
                            case '\\': sb.Append('\\'); break;
                            case '/': sb.Append('/'); break;
                            case 'b': sb.Append('\b'); break;
                            case 'f': sb.Append('\f'); break;
                            case 'n': sb.Append('\n'); break;
                            case 'r': sb.Append('\r'); break;
                            case 't': sb.Append('\t'); break;
                            case 'u':
                                {
                                    if (Pos + 4 > s.Length) throw Err("ungültiges \\u");
                                    int v = 0;
                                    for (int i = 0; i < 4; i++)
                                    {
                                        int h = HexVal(s[Pos + i]);
                                        if (h < 0) throw Err("ungültiges \\u");
                                        v = v * 16 + h;
                                    }
                                    Pos += 4;
                                    sb.Append((char)v);
                                    break;
                                }
                            default: throw Err("ungültige Escape-Sequenz");
                        }
                    }
                    else sb.Append(c);
                }
            }

            private double ReadNumber()
            {
                double v;
                int end;
                if (!TryScanDecimal(s, Pos, out v, out end)) throw Err("ungültige Zahl");
                Pos = end;
                return v;
            }
        }
    }

    /// <summary>Interner Zugriff für DeepClone/JSON auf <see cref="OrderedDict{K,V}"/>.</summary>
    internal interface IOrderedDictAccess
    {
        IEnumerable<KeyValuePair<object, object>> RawPairs();
        void RawSet(object key, object value);
    }

    /// <summary>
    /// Dictionary mit JS-Objekt-Reihenfolge auch nach Löschen: neue Schlüssel kommen immer ans Ende
    /// (System.Collections.Generic.Dictionary verwendet nach Remove freie Plätze wieder → Reihenfolge weicht ab).
    /// Iteration über einen Schnappschuss der Schlüssel; während der Iteration gelöschte Schlüssel werden übersprungen (wie JS for…in).
    /// </summary>
    public class OrderedDict<K, V> : IEnumerable<KeyValuePair<K, V>>, IOrderedDictAccess
    {
        private readonly Dictionary<K, V> map = new Dictionary<K, V>();
        private readonly List<K> order = new List<K>();

        public OrderedDict() { }

        public OrderedDict(IEnumerable<KeyValuePair<K, V>> src)
        {
            foreach (KeyValuePair<K, V> kv in src) this[kv.Key] = kv.Value;
        }

        public int Count { get { return order.Count; } }

        public V this[K key]
        {
            get { return map[key]; }
            set
            {
                if (!map.ContainsKey(key)) order.Add(key);
                map[key] = value;
            }
        }

        /// <summary>Für Collection-Initializer; wirft bei doppeltem Schlüssel.</summary>
        public void Add(K key, V value)
        {
            map.Add(key, value);
            order.Add(key);
        }

        public bool ContainsKey(K key) { return map.ContainsKey(key); }

        public bool TryGetValue(K key, out V value) { return map.TryGetValue(key, out value); }

        /// <summary>JS <c>delete d[key]</c>.</summary>
        public bool Remove(K key)
        {
            if (!map.Remove(key)) return false;
            order.Remove(key);
            return true;
        }

        public void Clear()
        {
            map.Clear();
            order.Clear();
        }

        /// <summary>Object.keys (Schnappschuss).</summary>
        public List<K> Keys { get { return new List<K>(order); } }

        /// <summary>Object.values (Schnappschuss).</summary>
        public List<V> Values
        {
            get
            {
                List<V> l = new List<V>(order.Count);
                for (int i = 0; i < order.Count; i++) l.Add(map[order[i]]);
                return l;
            }
        }

        public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
        {
            K[] snap = order.ToArray();
            for (int i = 0; i < snap.Length; i++)
            {
                V v;
                if (map.TryGetValue(snap[i], out v)) yield return new KeyValuePair<K, V>(snap[i], v);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }

        IEnumerable<KeyValuePair<object, object>> IOrderedDictAccess.RawPairs()
        {
            foreach (KeyValuePair<K, V> kv in this) yield return new KeyValuePair<object, object>(kv.Key, kv.Value);
        }

        void IOrderedDictAccess.RawSet(object key, object value)
        {
            this[(K)key] = (V)value;
        }
    }
}
