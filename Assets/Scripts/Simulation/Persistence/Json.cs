using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TinyTown.Simulation.Persistence
{
    /// <summary>
    /// Minimal JSON reader and writer so save files can be produced and tested without Unity.
    /// Objects become <see cref="Dictionary{TKey,TValue}"/> of string to object, arrays become
    /// <see cref="List{T}"/> of object, numbers become double, plus string, bool and null.
    /// </summary>
    public static class Json
    {
        /// <summary>Guards against hostile or corrupt files nesting without end.</summary>
        public const int MaxDepth = 64;

        public static object Parse(string text)
        {
            if (text == null)
            {
                throw new FormatException("JSON text is null.");
            }

            var reader = new Reader(text);
            reader.SkipWhitespace();
            var value = reader.ReadValue(0);
            reader.SkipWhitespace();
            if (!reader.AtEnd)
            {
                throw reader.Error("Unexpected text after the JSON value");
            }

            return value;
        }

        public static string Write(object value)
        {
            var sb = new StringBuilder();
            WriteValue(sb, value, 0);
            sb.Append('\n');
            return sb.ToString();
        }

        private static void WriteValue(StringBuilder sb, object value, int indent)
        {
            switch (value)
            {
                case null:
                    sb.Append("null");
                    break;
                case string s:
                    WriteString(sb, s);
                    break;
                case bool b:
                    sb.Append(b ? "true" : "false");
                    break;
                case double d:
                    WriteNumber(sb, d);
                    break;
                case int i:
                    sb.Append(i.ToString(CultureInfo.InvariantCulture));
                    break;
                case long l:
                    sb.Append(l.ToString(CultureInfo.InvariantCulture));
                    break;
                case IDictionary<string, object> obj:
                    WriteObject(sb, obj, indent);
                    break;
                case IList<object> list:
                    WriteArray(sb, list, indent);
                    break;
                default:
                    throw new ArgumentException($"Cannot write {value.GetType().Name} as JSON.");
            }
        }

        private static void WriteNumber(StringBuilder sb, double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d))
            {
                throw new ArgumentException("JSON cannot represent NaN or infinity.");
            }

            sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void WriteObject(StringBuilder sb, IDictionary<string, object> obj, int indent)
        {
            if (obj.Count == 0)
            {
                sb.Append("{}");
                return;
            }

            sb.Append("{\n");
            var first = true;
            foreach (var pair in obj)
            {
                if (!first)
                {
                    sb.Append(",\n");
                }

                first = false;
                sb.Append(' ', (indent + 1) * 2);
                WriteString(sb, pair.Key);
                sb.Append(": ");
                WriteValue(sb, pair.Value, indent + 1);
            }

            sb.Append('\n').Append(' ', indent * 2).Append('}');
        }

        private static void WriteArray(StringBuilder sb, IList<object> list, int indent)
        {
            if (list.Count == 0)
            {
                sb.Append("[]");
                return;
            }

            sb.Append("[\n");
            for (var i = 0; i < list.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(",\n");
                }

                sb.Append(' ', (indent + 1) * 2);
                WriteValue(sb, list[i], indent + 1);
            }

            sb.Append('\n').Append(' ', indent * 2).Append(']');
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (var c in s)
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
                        {
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.Append(c);
                        }

                        break;
                }
            }

            sb.Append('"');
        }

        private sealed class Reader
        {
            private readonly string text;
            private int pos;

            public Reader(string text)
            {
                this.text = text;
            }

            public bool AtEnd => pos >= text.Length;

            public FormatException Error(string message) => new FormatException($"{message} at character {pos}.");

            public void SkipWhitespace()
            {
                while (!AtEnd && (text[pos] == ' ' || text[pos] == '\t' || text[pos] == '\n' || text[pos] == '\r' || text[pos] == '﻿'))
                {
                    pos++;
                }
            }

            public object ReadValue(int depth)
            {
                if (depth > MaxDepth)
                {
                    throw Error("JSON is nested too deeply");
                }

                if (AtEnd)
                {
                    throw Error("Unexpected end of JSON");
                }

                switch (text[pos])
                {
                    case '{': return ReadObject(depth);
                    case '[': return ReadArray(depth);
                    case '"': return ReadString();
                    case 't': Expect("true"); return true;
                    case 'f': Expect("false"); return false;
                    case 'n': Expect("null"); return null;
                    default: return ReadNumber();
                }
            }

            private Dictionary<string, object> ReadObject(int depth)
            {
                var result = new Dictionary<string, object>();
                pos++;
                SkipWhitespace();
                if (!AtEnd && text[pos] == '}')
                {
                    pos++;
                    return result;
                }

                while (true)
                {
                    SkipWhitespace();
                    if (AtEnd || text[pos] != '"')
                    {
                        throw Error("Expected a property name");
                    }

                    var key = ReadString();
                    SkipWhitespace();
                    if (AtEnd || text[pos] != ':')
                    {
                        throw Error("Expected ':'");
                    }

                    pos++;
                    SkipWhitespace();
                    result[key] = ReadValue(depth + 1);
                    SkipWhitespace();
                    if (AtEnd)
                    {
                        throw Error("Unterminated object");
                    }

                    if (text[pos] == ',')
                    {
                        pos++;
                        continue;
                    }

                    if (text[pos] == '}')
                    {
                        pos++;
                        return result;
                    }

                    throw Error("Expected ',' or '}'");
                }
            }

            private List<object> ReadArray(int depth)
            {
                var result = new List<object>();
                pos++;
                SkipWhitespace();
                if (!AtEnd && text[pos] == ']')
                {
                    pos++;
                    return result;
                }

                while (true)
                {
                    SkipWhitespace();
                    result.Add(ReadValue(depth + 1));
                    SkipWhitespace();
                    if (AtEnd)
                    {
                        throw Error("Unterminated array");
                    }

                    if (text[pos] == ',')
                    {
                        pos++;
                        continue;
                    }

                    if (text[pos] == ']')
                    {
                        pos++;
                        return result;
                    }

                    throw Error("Expected ',' or ']'");
                }
            }

            private string ReadString()
            {
                var sb = new StringBuilder();
                pos++;
                while (true)
                {
                    if (AtEnd)
                    {
                        throw Error("Unterminated string");
                    }

                    var c = text[pos++];
                    if (c == '"')
                    {
                        return sb.ToString();
                    }

                    if (c != '\\')
                    {
                        sb.Append(c);
                        continue;
                    }

                    if (AtEnd)
                    {
                        throw Error("Unterminated escape");
                    }

                    var e = text[pos++];
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
                            if (pos + 4 > text.Length
                                || !int.TryParse(text.Substring(pos, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
                            {
                                throw Error("Invalid \\u escape");
                            }

                            sb.Append((char)code);
                            pos += 4;
                            break;
                        default:
                            throw Error($"Invalid escape '\\{e}'");
                    }
                }
            }

            private double ReadNumber()
            {
                var start = pos;
                while (!AtEnd && "+-0123456789.eE".IndexOf(text[pos]) >= 0)
                {
                    pos++;
                }

                if (start == pos
                    || !double.TryParse(text.Substring(start, pos - start), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                    || double.IsInfinity(value))
                {
                    pos = start;
                    throw Error("Invalid value");
                }

                return value;
            }

            private void Expect(string word)
            {
                if (string.CompareOrdinal(text, pos, word, 0, word.Length) != 0)
                {
                    throw Error($"Expected '{word}'");
                }

                pos += word.Length;
            }
        }
    }
}
