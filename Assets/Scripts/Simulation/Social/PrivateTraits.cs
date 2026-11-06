using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TinyCity.Simulation.Residents;

namespace TinyCity.Simulation.Social
{
    /// <summary>
    /// Owner, 6 November: everyone's personality is private, yet residents kept naming their own and each other's
    /// traits out loud. The prompts ask them not to, and this is the game's own check on what they say: a sentence
    /// that talks about personality, or calls someone (themselves included) by one of the residents' trait words, is
    /// left out of the line. The traits are the first sentence of each personality ("Nosy, chatty and opinionated.").
    /// </summary>
    public static class PrivateTraits
    {
        private static readonly Regex Sentences = new Regex(@"[^.!?]+[.!?]*", RegexOptions.CultureInvariant);

        private static readonly Regex AboutPersonality = new Regex(@"\b(personalit(y|ies)|traits?|my nature|by nature|your nature|(his|her|their) nature)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private const string Subjects = @"i'm|i am|im|i've always been|i was always|i've been|you're|youre|you are|you've always been|he's|he is|she's|she is|they're|they are|being|such an?|as an?|so|too|very|really";

        /// <summary>The trait words of one personality: the last word of each part of its first sentence, lower case.</summary>
        public static IReadOnlyList<string> TraitsOf(string outlook)
        {
            if (string.IsNullOrWhiteSpace(outlook))
            {
                return Array.Empty<string>();
            }

            var first = outlook.Split('.')[0];
            return Regex.Split(first, @",|\band\b|\bbut\b", RegexOptions.IgnoreCase)
                .Select(part => part.Trim().Split(' ').LastOrDefault()?.Trim().ToLowerInvariant())
                .Where(word => !string.IsNullOrEmpty(word) && word.Length > 2 && word.All(c => char.IsLetter(c) || c == '-'))
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// The text without any sentence that talks about personality or calls someone by a trait word of the given
        /// people. Returns an empty string when nothing is left.
        /// </summary>
        public static string WithoutTraitTalk(string text, IEnumerable<Resident> people)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var traits = (people ?? Enumerable.Empty<Resident>()).Where(p => p != null).SelectMany(p => TraitsOf(p.Outlook)).Distinct().ToList();
            var naming = traits.Count == 0 ? null
                : new Regex($@"\b({Subjects})\s+(?:[\w'-]+[\s,]+){{0,3}}?({string.Join("|", traits.Select(Regex.Escape))})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var kept = new StringBuilder();
            foreach (Match m in Sentences.Matches(text))
            {
                var sentence = m.Value;
                if (AboutPersonality.IsMatch(sentence) || (naming != null && naming.IsMatch(sentence)))
                {
                    continue;
                }

                kept.Append(sentence);
            }

            return kept.ToString().Trim();
        }
    }
}
