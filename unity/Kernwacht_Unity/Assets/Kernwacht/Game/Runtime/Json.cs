using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Kernwacht.Game
{
    /// <summary>
    /// Kleiner JSON-Leser für die exportierten Spieldaten (Objekte → Dictionary, Listen → List, Zahlen → double).
    /// JsonUtility kann keine Wörterbücher; daher dieser eigene Parser.
    /// </summary>
    public static class Json
    {
        public static object Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            int i = 0;
            var v = ReadValue(text, ref i);
            return v;
        }

        static void SkipWs(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        static object ReadValue(string s, ref int i)
        {
            SkipWs(s, ref i);
            if (i >= s.Length) return null;
            char c = s[i];
            if (c == '{') return ReadObject(s, ref i);
            if (c == '[') return ReadArray(s, ref i);
            if (c == '"') return ReadString(s, ref i);
            if (c == 't' && string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
            if (c == 'f' && string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
            if (c == 'n' && string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
            return ReadNumber(s, ref i);
        }

        static Dictionary<string, object> ReadObject(string s, ref int i)
        {
            var d = new Dictionary<string, object>();
            i++; // {
            while (true)
            {
                SkipWs(s, ref i);
                if (i >= s.Length) break;
                if (s[i] == '}') { i++; break; }
                if (s[i] == ',') { i++; continue; }
                string key = ReadString(s, ref i);
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == ':') i++;
                d[key] = ReadValue(s, ref i);
            }
            return d;
        }

        static List<object> ReadArray(string s, ref int i)
        {
            var l = new List<object>();
            i++; // [
            while (true)
            {
                SkipWs(s, ref i);
                if (i >= s.Length) break;
                if (s[i] == ']') { i++; break; }
                if (s[i] == ',') { i++; continue; }
                l.Add(ReadValue(s, ref i));
            }
            return l;
        }

        static string ReadString(string s, ref int i)
        {
            var sb = new StringBuilder();
            i++; // "
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') break;
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) break;
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        if (i + 4 <= s.Length)
                        {
                            sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            i += 4;
                        }
                        break;
                    default: sb.Append(e); break;
                }
            }
            return sb.ToString();
        }

        static object ReadNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            if (i == start) { i++; return null; }
            double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out double v);
            return v;
        }

        // ---------------------------------------------------------------- Zugriffshilfen

        public static Dictionary<string, object> Obj(this Dictionary<string, object> d, string key)
            => d != null && d.TryGetValue(key, out var v) ? v as Dictionary<string, object> : null;

        public static List<object> Arr(this Dictionary<string, object> d, string key)
            => d != null && d.TryGetValue(key, out var v) ? v as List<object> : null;

        public static string Str(this Dictionary<string, object> d, string key, string def = "")
            => d != null && d.TryGetValue(key, out var v) && v != null ? Convert.ToString(v, CultureInfo.InvariantCulture) : def;

        public static float Num(this Dictionary<string, object> d, string key, float def = 0f)
        {
            if (d == null || !d.TryGetValue(key, out var v) || v == null) return def;
            if (v is double x) return (float)x;
            if (v is bool b) return b ? 1f : 0f;
            return def;
        }

        public static int Int(this Dictionary<string, object> d, string key, int def = 0)
            => (int)Math.Round(d.Num(key, def));

        public static bool Bool(this Dictionary<string, object> d, string key, bool def = false)
        {
            if (d == null || !d.TryGetValue(key, out var v) || v == null) return def;
            if (v is bool b) return b;
            if (v is double x) return x != 0;
            return def;
        }

        public static object Get(this Dictionary<string, object> d, string key)
            => d != null && d.TryGetValue(key, out var v) ? v : null;

        public static float AsFloat(object v) => v is double x ? (float)x : 0f;
    }
}
