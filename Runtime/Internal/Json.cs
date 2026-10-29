using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BerTaDev.DiscordRichPresence.Internal
{
    /// <summary>
    /// Tiny JSON reader/writer so the package has no dependencies.
    /// Objects parse to Dictionary&lt;string, object&gt;, arrays to List&lt;object&gt;,
    /// integers to long and other numbers to double.
    /// </summary>
    internal static class Json
    {
        public static void WriteString(StringBuilder sb, string value)
        {
            if (value == null)
            {
                sb.Append("null");
                return;
            }

            sb.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20)
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        public static object Parse(string json) => new Parser(json).ParseRoot();

        public static string GetString(this Dictionary<string, object> obj, string key) =>
            obj != null && obj.TryGetValue(key, out object value) ? value as string : null;

        public static long GetLong(this Dictionary<string, object> obj, string key, long fallback = 0)
        {
            if (obj == null || !obj.TryGetValue(key, out object value)) return fallback;
            if (value is long l) return l;
            if (value is double d) return (long)d;
            return fallback;
        }

        public static Dictionary<string, object> GetObject(this Dictionary<string, object> obj, string key) =>
            obj != null && obj.TryGetValue(key, out object value) ? value as Dictionary<string, object> : null;

        sealed class Parser
        {
            readonly string s;
            int i;

            public Parser(string s)
            {
                this.s = s ?? throw new ArgumentNullException(nameof(s));
            }

            public object ParseRoot()
            {
                object value = ReadValue();
                SkipWhitespace();
                if (i != s.Length) throw Error("Unexpected trailing characters");
                return value;
            }

            object ReadValue()
            {
                SkipWhitespace();
                if (i >= s.Length) throw Error("Unexpected end of input");

                char c = s[i];
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
                        throw Error("Unexpected character '" + c + "'");
                }
            }

            Dictionary<string, object> ReadObject()
            {
                var obj = new Dictionary<string, object>();
                i++; // {
                SkipWhitespace();
                if (Peek() == '}')
                {
                    i++;
                    return obj;
                }

                while (true)
                {
                    SkipWhitespace();
                    if (Peek() != '"') throw Error("Expected property name");
                    string key = ReadString();
                    SkipWhitespace();
                    if (Next() != ':') throw Error("Expected ':'");
                    obj[key] = ReadValue();
                    SkipWhitespace();
                    char c = Next();
                    if (c == '}') return obj;
                    if (c != ',') throw Error("Expected ',' or '}'");
                }
            }

            List<object> ReadArray()
            {
                var list = new List<object>();
                i++; // [
                SkipWhitespace();
                if (Peek() == ']')
                {
                    i++;
                    return list;
                }

                while (true)
                {
                    list.Add(ReadValue());
                    SkipWhitespace();
                    char c = Next();
                    if (c == ']') return list;
                    if (c != ',') throw Error("Expected ',' or ']'");
                }
            }

            string ReadString()
            {
                i++; // opening quote
                var sb = new StringBuilder();
                while (i < s.Length)
                {
                    char c = s[i++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\')
                    {
                        sb.Append(c);
                        continue;
                    }

                    if (i >= s.Length) break;
                    char e = s[i++];
                    switch (e)
                    {
                        case '"':
                        case '\\':
                        case '/': sb.Append(e); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 > s.Length) throw Error("Invalid unicode escape");
                            sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            i += 4;
                            break;
                        default: throw Error("Invalid escape '\\" + e + "'");
                    }
                }
                throw Error("Unterminated string");
            }

            object ReadNumber()
            {
                int start = i;
                while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
                string number = s.Substring(start, i - start);
                if (long.TryParse(number, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long l)) return l;
                return double.Parse(number, NumberStyles.Float, CultureInfo.InvariantCulture);
            }

            void Expect(string literal)
            {
                if (string.CompareOrdinal(s, i, literal, 0, literal.Length) != 0) throw Error("Expected '" + literal + "'");
                i += literal.Length;
            }

            void SkipWhitespace()
            {
                while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            }

            char Peek() => i < s.Length ? s[i] : '\0';

            char Next() => i < s.Length ? s[i++] : '\0';

            FormatException Error(string message) => new FormatException(message + " at position " + i + ".");
        }
    }
}
