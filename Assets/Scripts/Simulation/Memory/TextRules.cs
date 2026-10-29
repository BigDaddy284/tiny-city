using System.Text;

namespace TinyCity.Simulation.Memory
{
    /// <summary>Cleans text that comes from the AI or a save file before the game stores or shows it.</summary>
    public static class TextRules
    {
        /// <summary>
        /// Removes control characters, collapses whitespace, strips wrapping quotes and shortens the text to at most
        /// <paramref name="maxLength"/> characters, cutting at a word boundary where possible.
        /// </summary>
        public static string Clean(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || maxLength <= 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder(text.Length);
            var lastWasSpace = true;
            foreach (var c in text)
            {
                if (char.IsWhiteSpace(c) || char.IsControl(c))
                {
                    if (!lastWasSpace)
                    {
                        sb.Append(' ');
                        lastWasSpace = true;
                    }

                    continue;
                }

                sb.Append(c);
                lastWasSpace = false;
            }

            var result = sb.ToString().Trim();
            while (result.Length >= 2 && IsQuote(result[0]) && IsQuote(result[result.Length - 1]))
            {
                result = result.Substring(1, result.Length - 2).Trim();
            }

            if (result.Length <= maxLength)
            {
                return result;
            }

            var cut = result.LastIndexOf(' ', maxLength - 1);
            result = cut > maxLength / 2 ? result.Substring(0, cut) : result.Substring(0, maxLength - 1);
            return result.TrimEnd(' ', ',', ';', ':') + "…";
        }

        private static bool IsQuote(char c) => c == '"' || c == '\'' || c == '“' || c == '”';
    }
}
