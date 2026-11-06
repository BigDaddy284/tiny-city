using System;
using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation.Memory;
using TinyCity.Simulation.Persistence;

namespace TinyCity.Simulation.Residents
{
    /// <summary>
    /// A resident as the player describes them in <c>residents.json</c>: who they are in a few lines. Everything
    /// else about them (what they do, think and remember) comes from the AI. Profiles only shape a new town.
    /// </summary>
    public sealed class ResidentProfile
    {
        public const int MaxNameLength = 24;
        public const int MaxAboutLength = 300;
        public const int MaxPersonalityLength = 200;
        public const int MinAge = 18;
        public const int MaxAge = 100;

        public ResidentProfile(string id, string firstName, string lastName, int age, string about, string ownsBusinessId = null, string personality = null)
        {
            Personality = personality ?? string.Empty;
            Id = id;
            FirstName = firstName;
            LastName = lastName;
            Age = age;
            About = about ?? string.Empty;
            OwnsBusinessId = string.IsNullOrWhiteSpace(ownsBusinessId) ? null : ownsBusinessId;
        }

        /// <summary>A starter resident's id (<c>res-0001</c> to <c>res-0015</c>), which fixes their look, home and job.</summary>
        public string Id { get; }

        public string FirstName { get; }

        public string LastName { get; }

        public int Age { get; }

        /// <summary>One sentence of who they are at heart, in "you" form, given to the AI as their personality (Tiny City 2).</summary>
        public string Personality { get; }

        /// <summary>A few lines of personality and background, given to the AI.</summary>
        public string About { get; }

        /// <summary>The business they start as boss of, or null.</summary>
        public string OwnsBusinessId { get; }
    }

    /// <summary>Reads and writes <c>residents.json</c>, checking every profile and saying what was wrong.</summary>
    public static class ResidentProfiles
    {
        public const string Format = "tiny-city-2-profiles";

        /// <summary>
        /// Reads profiles. <paramref name="knownIds"/> are the residents the city can hold and
        /// <paramref name="businessIds"/> the businesses that can be owned. A profile with a problem it can't
        /// repair is left out, with a reason in <paramref name="problems"/>; a business claimed twice keeps its
        /// first owner. Returns an empty list if the file can't be read at all.
        /// </summary>
        public static List<ResidentProfile> Parse(string json, IEnumerable<string> knownIds, IEnumerable<string> businessIds, out List<string> problems)
        {
            problems = new List<string>();
            var profiles = new List<ResidentProfile>();
            Dictionary<string, object> root;
            try
            {
                root = Json.Parse(json ?? string.Empty) as Dictionary<string, object>;
            }
            catch (FormatException e)
            {
                problems.Add($"residents.json is not valid JSON: {e.Message}");
                return profiles;
            }

            if (root == null || !(root.TryGetValue("residents", out var list) && list is List<object> items))
            {
                problems.Add("residents.json has no \"residents\" list.");
                return profiles;
            }

            var known = new HashSet<string>(knownIds ?? Enumerable.Empty<string>());
            var businesses = new HashSet<string>(businessIds ?? Enumerable.Empty<string>());
            var owned = new HashSet<string>();
            var seen = new HashSet<string>();
            var index = 0;
            foreach (var item in items)
            {
                index++;
                var p = item as Dictionary<string, object>;
                var id = Text(p, "id");
                var label = string.IsNullOrEmpty(id) ? $"profile {index}" : id;
                if (p == null || string.IsNullOrEmpty(id))
                {
                    problems.Add($"{label}: has no id, so it was left out.");
                    continue;
                }

                if (!known.Contains(id))
                {
                    problems.Add($"{label}: isn't one of the city's residents, so it was left out.");
                    continue;
                }

                if (!seen.Add(id))
                {
                    problems.Add($"{label}: appears more than once; only the first was used.");
                    continue;
                }

                var first = TextRules.Clean(Text(p, "firstName"), ResidentProfile.MaxNameLength);
                var last = TextRules.Clean(Text(p, "lastName"), ResidentProfile.MaxNameLength);
                if (first.Length == 0)
                {
                    problems.Add($"{label}: has no first name, so it was left out.");
                    continue;
                }

                var age = Number(p, "age", -1);
                if (age < ResidentProfile.MinAge || age > ResidentProfile.MaxAge || Math.Abs(age - Math.Round(age)) > 0)
                {
                    problems.Add($"{label}: age must be a whole number from {ResidentProfile.MinAge} to {ResidentProfile.MaxAge}, so it was left out.");
                    continue;
                }

                var about = TextRules.Clean(Text(p, "about"), ResidentProfile.MaxAboutLength);
                var owns = Text(p, "owns");
                if (!string.IsNullOrEmpty(owns))
                {
                    if (!businesses.Contains(owns))
                    {
                        problems.Add($"{label}: owns '{owns}', which isn't a business in the city, so they own nothing.");
                        owns = null;
                    }
                    else if (!owned.Add(owns))
                    {
                        problems.Add($"{label}: owns '{owns}', which someone else already owns, so they own nothing.");
                        owns = null;
                    }
                }

                profiles.Add(new ResidentProfile(id, first, last, (int)age, about, owns, TextRules.Clean(Text(p, "personality"), ResidentProfile.MaxPersonalityLength)));
            }

            return profiles;
        }

        /// <summary>The brief and roles in residents.json (Tiny City 2), or <see cref="WorldBrief.Default"/> when it has none or can't be read.</summary>
        public static WorldBrief ParseBrief(string json, out List<string> problems)
        {
            problems = new List<string>();
            try
            {
                return WorldBrief.Parse(Json.Parse(json ?? string.Empty) as Dictionary<string, object>, problems);
            }
            catch (FormatException)
            {
                return WorldBrief.Default;
            }
        }

        public static string Write(IEnumerable<ResidentProfile> profiles, WorldBrief brief = null) => Json.Write(new Dictionary<string, object>
        {
            ["format"] = Format,
            ["brief"] = (brief ?? WorldBrief.Default).Text,
            ["roles"] = (brief ?? WorldBrief.Default).ToJson()["roles"],
            ["residents"] = (profiles ?? Enumerable.Empty<ResidentProfile>()).Select(p =>
            {
                var entry = new Dictionary<string, object>
                {
                    ["id"] = p.Id,
                    ["firstName"] = p.FirstName,
                    ["lastName"] = p.LastName,
                    ["age"] = p.Age,
                    ["personality"] = p.Personality,
                    ["about"] = p.About,
                };
                if (p.OwnsBusinessId != null)
                {
                    entry["owns"] = p.OwnsBusinessId;
                }

                return (object)entry;
            }).ToList(),
        });

        private static string Text(Dictionary<string, object> p, string key) =>
            p != null && p.TryGetValue(key, out var v) && v is string s ? s.Trim() : null;

        private static double Number(Dictionary<string, object> p, string key, double fallback) =>
            p != null && p.TryGetValue(key, out var v) && v is double d && !double.IsNaN(d) && !double.IsInfinity(d) ? d : fallback;
    }
}
