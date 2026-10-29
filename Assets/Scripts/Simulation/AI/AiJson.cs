using System;
using System.Collections.Generic;
using System.Globalization;
using TinyCity.Simulation.Persistence;

namespace TinyCity.Simulation.AI
{
    /// <summary>Tolerant reading of JSON written by a language model, which may wrap it in prose or code fences.</summary>
    public static class AiJson
    {
        /// <summary>Longer answers are rejected outright; a valid answer is a few hundred characters.</summary>
        public const int MaxLength = 8000;

        /// <summary>Finds the outermost JSON object in the text and parses it.</summary>
        public static bool TryParseObject(string text, out Dictionary<string, object> result, out string error)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                error = "the answer was empty";
                return false;
            }

            if (text.Length > MaxLength)
            {
                error = $"the answer was too long ({text.Length} characters)";
                return false;
            }

            var start = text.IndexOf('{');
            var end = text.LastIndexOf('}');
            if (start < 0 || end <= start)
            {
                error = "the answer contained no JSON object";
                return false;
            }

            try
            {
                result = Json.Parse(text.Substring(start, end - start + 1)) as Dictionary<string, object>;
            }
            catch (FormatException e)
            {
                error = $"the answer was not valid JSON ({e.Message})";
                return false;
            }

            error = result == null ? "the answer was not a JSON object" : null;
            return result != null;
        }

        public static string GetString(Dictionary<string, object> obj, string key) =>
            obj != null && obj.TryGetValue(key, out var v) ? v as string : null;

        /// <summary>Reads true/false written either as a JSON boolean or as the text "true"/"false".</summary>
        public static bool TryGetBool(Dictionary<string, object> obj, string key, out bool value)
        {
            value = false;
            if (obj == null || !obj.TryGetValue(key, out var v))
            {
                return false;
            }

            if (v is bool b)
            {
                value = b;
                return true;
            }

            return v is string s && bool.TryParse(s.Trim(), out value);
        }

        /// <summary>Reads a whole number written either as a number or as numeric text.</summary>
        public static bool TryGetInt(Dictionary<string, object> obj, string key, out int value)
        {
            value = 0;
            if (obj == null || !obj.TryGetValue(key, out var v))
            {
                return false;
            }

            double d;
            if (v is double number)
            {
                d = number;
            }
            else if (!(v is string s && double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out d)))
            {
                return false;
            }

            if (double.IsNaN(d) || double.IsInfinity(d) || d < int.MinValue || d > int.MaxValue)
            {
                return false;
            }

            value = (int)Math.Round(d);
            return true;
        }
    }
}
