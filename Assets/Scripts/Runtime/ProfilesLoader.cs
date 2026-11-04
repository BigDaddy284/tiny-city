using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TinyCity.Simulation.Content;
using TinyCity.Simulation.Residents;
using UnityEngine;

namespace TinyCity.Runtime
{
    /// <summary>
    /// Reads the player's resident profiles from <c>StreamingAssets/Configuration/residents.json</c> when a new
    /// town is made. A missing or unreadable file, or one with no usable profiles, starts the designed residents.
    /// </summary>
    public static class ProfilesLoader
    {
        public static string ProfilesPath =>
            Path.Combine(Application.streamingAssetsPath, "Configuration", "residents.json");

        public static IReadOnlyList<ResidentProfile> Load(out IReadOnlyList<string> problems)
        {
            var found = new List<string>();
            problems = found;
            try
            {
                if (!File.Exists(ProfilesPath))
                {
                    found.Add($"No residents.json at {ProfilesPath}, so the city starts with its usual residents.");
                    return null;
                }

                var businesses = StarterTown.CreateWorld().Businesses.Select(b => b.Id);
                var profiles = ResidentProfiles.Parse(File.ReadAllText(ProfilesPath), StarterTown.StarterResidentIds, businesses, out var parseProblems);
                found.AddRange(parseProblems);
                if (profiles.Count == 0)
                {
                    found.Add("residents.json had no usable profiles, so the city starts with its usual residents.");
                    return null;
                }

                return profiles;
            }
            catch (Exception e)
            {
                found.Add($"residents.json could not be read: {e.Message}");
                return null;
            }
            finally
            {
                foreach (var problem in found)
                {
                    Debug.LogWarning($"[TinyCity] {problem}");
                }
            }
        }
    }
}
