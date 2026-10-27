using System;
using System.Collections.Generic;
using System.Linq;
using TinyTown.Simulation.Activities;
using TinyTown.Simulation.Residents;
using TinyTown.Simulation.Time;
using TinyTown.Simulation.World;

namespace TinyTown.Simulation.Persistence
{
    public sealed class SaveLoadResult
    {
        internal SaveLoadResult(TownSimulation simulation, IReadOnlyList<string> errors, IReadOnlyList<string> warnings)
        {
            Simulation = simulation;
            Errors = errors;
            Warnings = warnings;
        }

        /// <summary>The restored town, or null when <see cref="Errors"/> is not empty.</summary>
        public TownSimulation Simulation { get; }

        public bool Success => Simulation != null;

        /// <summary>Problems that stopped the save from loading.</summary>
        public IReadOnlyList<string> Errors { get; }

        /// <summary>Problems that were repaired while loading.</summary>
        public IReadOnlyList<string> Warnings { get; }
    }

    /// <summary>
    /// Converts the town's logical state to and from versioned JSON. Only stable ids and plain values are
    /// stored; the world layout comes from content, and paths are recomputed after loading.
    /// </summary>
    public static class SaveSerializer
    {
        public const string Format = "tiny-town-save";

        /// <summary>
        /// Bump this when the save layout changes, and teach <see cref="Load"/> to upgrade the older version so
        /// residents, memories and history survive.
        /// </summary>
        public const int CurrentVersion = 1;

        public static string Save(TownSimulation simulation)
        {
            var root = new Dictionary<string, object>
            {
                ["format"] = Format,
                ["version"] = CurrentVersion,
                ["townId"] = simulation.World.TownId,
                ["gameMinutes"] = simulation.Time.TotalMinutes,
                ["residents"] = simulation.Residents.Select(WriteResident).Cast<object>().ToList(),
            };

            return Json.Write(root);
        }

        public static SaveLoadResult Load(string json, TownWorld world)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            Dictionary<string, object> root;
            try
            {
                root = Json.Parse(json) as Dictionary<string, object>;
            }
            catch (FormatException e)
            {
                return Fail($"The save file is not valid JSON: {e.Message}");
            }

            if (root == null)
            {
                return Fail("The save file does not contain a save object.");
            }

            if (GetString(root, "format") != Format)
            {
                return Fail("This is not a Tiny Town save file.");
            }

            var version = GetInt(root, "version", -1);
            if (version < 1)
            {
                return Fail("The save file has no valid version number.");
            }

            if (version > CurrentVersion)
            {
                return Fail($"The save file is from a newer version of the game (save version {version}, this game reads up to {CurrentVersion}).");
            }

            if (GetString(root, "townId") != world.TownId)
            {
                return Fail($"The save file is for a different town ('{GetString(root, "townId")}').");
            }

            var minutes = GetDouble(root, "gameMinutes", -1);
            if (minutes < 0 || minutes > long.MaxValue / 2 || minutes != Math.Floor(minutes))
            {
                return Fail("The save file has an invalid game time.");
            }

            if (!(root.TryGetValue("residents", out var residentsValue) && residentsValue is List<object> residentList) || residentList.Count == 0)
            {
                return Fail("The save file has no residents.");
            }

            var residents = new List<Resident>();
            for (var i = 0; i < residentList.Count; i++)
            {
                if (!(residentList[i] is Dictionary<string, object> data))
                {
                    errors.Add($"Resident #{i + 1} is not a valid record.");
                    continue;
                }

                var resident = ReadResident(data, world, i, errors, warnings);
                if (resident == null)
                {
                    continue;
                }

                if (residents.Any(r => r.Id == resident.Id))
                {
                    errors.Add($"Resident id '{resident.Id}' appears more than once.");
                    continue;
                }

                residents.Add(resident);
            }

            if (errors.Count > 0)
            {
                return new SaveLoadResult(null, errors, warnings);
            }

            return new SaveLoadResult(new TownSimulation(world, residents, new GameTime((long)minutes)), errors, warnings);

            SaveLoadResult Fail(string message) => new SaveLoadResult(null, new[] { message }, warnings);
        }

        private static object WriteResident(Resident r)
        {
            var needs = new Dictionary<string, object>();
            for (var i = 0; i < Needs.Count; i++)
            {
                var need = (NeedType)i;
                needs[NeedKey(need)] = r.Needs[need];
            }

            return new Dictionary<string, object>
            {
                ["id"] = r.Id,
                ["firstName"] = r.FirstName,
                ["lastName"] = r.LastName,
                ["ageYears"] = r.AgeYears,
                ["birthdayDayOfYear"] = r.BirthdayDayOfYear,
                ["colour"] = r.ColourHex,
                ["personality"] = new Dictionary<string, object>
                {
                    ["active"] = r.Personality.Active,
                    ["sociable"] = r.Personality.Sociable,
                    ["tidy"] = r.Personality.Tidy,
                    ["bookish"] = r.Personality.Bookish,
                },
                ["homeMapId"] = r.HomeMapId,
                ["bedObjectId"] = r.BedObjectId,
                ["job"] = r.Job == null ? null : new Dictionary<string, object>
                {
                    ["title"] = r.Job.Title,
                    ["workObjectId"] = r.Job.WorkObjectId,
                    ["startMinute"] = r.Job.StartMinuteOfDay,
                    ["endMinute"] = r.Job.EndMinuteOfDay,
                    ["worksWeekends"] = r.Job.WorksWeekends,
                },
                ["alive"] = r.IsAlive,
                ["needs"] = needs,
                ["location"] = new Dictionary<string, object>
                {
                    ["mapId"] = r.Location.MapId,
                    ["x"] = r.Location.Position.X,
                    ["y"] = r.Location.Position.Y,
                },
                ["state"] = r.State.ToString(),
                ["activityId"] = r.ActivityId,
                ["targetObjectId"] = r.TargetObjectId,
                ["minutesRemaining"] = r.MinutesRemaining,
                ["goal"] = r.Goal,
                ["log"] = r.Log.Select(e => (object)new Dictionary<string, object>
                {
                    ["gameMinutes"] = e.Time.TotalMinutes,
                    ["text"] = e.Text,
                }).ToList(),
            };
        }

        private static Resident ReadResident(Dictionary<string, object> data, TownWorld world, int index, List<string> errors, List<string> warnings)
        {
            var id = GetString(data, "id");
            if (string.IsNullOrWhiteSpace(id))
            {
                errors.Add($"Resident #{index + 1} has no id.");
                return null;
            }

            var name = $"{GetString(data, "firstName")} {GetString(data, "lastName")}".Trim();
            var label = string.IsNullOrEmpty(name) ? id : name;

            var personalityData = GetObject(data, "personality");
            var personality = personalityData == null
                ? new Personality(0.5, 0.5, 0.5, 0.5)
                : new Personality(
                    GetDouble(personalityData, "active", 0.5),
                    GetDouble(personalityData, "sociable", 0.5),
                    GetDouble(personalityData, "tidy", 0.5),
                    GetDouble(personalityData, "bookish", 0.5));
            if (personalityData == null)
            {
                warnings.Add($"{label}: personality was missing, so average traits were used.");
            }

            Job job = null;
            var jobData = GetObject(data, "job");
            if (jobData != null)
            {
                var start = GetInt(jobData, "startMinute", -1);
                var end = GetInt(jobData, "endMinute", -1);
                if (start >= 0 && end > start && end <= GameTime.MinutesPerDay)
                {
                    job = new Job(GetString(jobData, "title") ?? "Worker", GetString(jobData, "workObjectId"), start, end, GetBool(jobData, "worksWeekends", false));
                    if (!world.TryGetObject(job.WorkObjectId, out _))
                    {
                        warnings.Add($"{label}: workplace '{job.WorkObjectId}' no longer exists, so they can't go to work.");
                    }
                }
                else
                {
                    warnings.Add($"{label}: job hours were invalid, so the job was dropped.");
                }
            }

            var needs = new Needs(75);
            var needsData = GetObject(data, "needs");
            if (needsData == null)
            {
                warnings.Add($"{label}: needs were missing, so they were reset.");
            }

            for (var i = 0; i < Needs.Count; i++)
            {
                var need = (NeedType)i;
                if (needsData != null && needsData.TryGetValue(NeedKey(need), out var v) && v is double d)
                {
                    needs[need] = d;
                }
                else if (needsData != null)
                {
                    warnings.Add($"{label}: {need} was missing, so it was reset.");
                }
            }

            var homeMapId = GetString(data, "homeMapId");
            var bedObjectId = GetString(data, "bedObjectId");
            var resident = new Resident(
                id,
                GetString(data, "firstName"),
                GetString(data, "lastName"),
                GetInt(data, "ageYears", 30),
                GetInt(data, "birthdayDayOfYear", 1),
                GetString(data, "colour"),
                personality,
                homeMapId,
                bedObjectId,
                job,
                RecoverLocation(data, world, bedObjectId, label, warnings),
                needs);

            resident.IsAlive = GetBool(data, "alive", true);
            foreach (var entry in GetList(data, "log").OfType<Dictionary<string, object>>().Take(Resident.MaxLogEntries))
            {
                var minute = GetDouble(entry, "gameMinutes", -1);
                if (minute >= 0)
                {
                    resident.AddLog(new GameTime((long)minute), GetString(entry, "text"));
                }
            }

            RestoreActivity(resident, data, world, label, warnings);
            return resident;
        }

        private static Location RecoverLocation(Dictionary<string, object> data, TownWorld world, string bedObjectId, string label, List<string> warnings)
        {
            var loc = GetObject(data, "location");
            var mapId = loc == null ? null : GetString(loc, "mapId");
            if (mapId != null)
            {
                var saved = new Location(mapId, GetInt(loc, "x", -1), GetInt(loc, "y", -1));
                if (world.IsWalkable(saved))
                {
                    return saved;
                }
            }

            // Logged recovery: the only time a resident is moved without walking.
            if (world.TryGetObject(bedObjectId, out var bed))
            {
                warnings.Add($"{label}: saved position was invalid, so they were moved home to bed.");
                return bed.Location;
            }

            var fallback = world.Objects.First().Location;
            warnings.Add($"{label}: saved position was invalid and they have no bed, so they were moved to {world.Objects.First().Name}.");
            return fallback;
        }

        private static void RestoreActivity(Resident resident, Dictionary<string, object> data, TownWorld world, string label, List<string> warnings)
        {
            resident.ResetActivity();
            if (!Enum.TryParse(GetString(data, "state") ?? string.Empty, out ResidentState state) || !Enum.IsDefined(typeof(ResidentState), state))
            {
                warnings.Add($"{label}: unknown activity state, so they will choose what to do next.");
                return;
            }

            if (state == ResidentState.Idle)
            {
                return;
            }

            var minutes = Math.Max(0, GetInt(data, "minutesRemaining", 0));
            if (state == ResidentState.Waiting)
            {
                resident.State = ResidentState.Waiting;
                resident.MinutesRemaining = Math.Min(minutes, TownSimulation.UnreachableWaitMinutes);
                resident.Goal = GetString(data, "goal") ?? string.Empty;
                return;
            }

            var activityId = GetString(data, "activityId");
            var targetId = GetString(data, "targetObjectId");
            if (!ActivityCatalog.TryGet(activityId, out var activity) || !world.TryGetObject(targetId, out var target) || !activity.CanUse(target))
            {
                warnings.Add($"{label}: their saved activity was no longer valid, so they will choose what to do next.");
                return;
            }

            resident.ActivityId = activity.Id;
            resident.TargetObjectId = target.Id;
            resident.Goal = GetString(data, "goal") ?? string.Empty;
            if (state == ResidentState.Performing && resident.Location != target.Location)
            {
                warnings.Add($"{label}: was not at {target.Name}, so they will walk there first.");
                state = ResidentState.Travelling;
            }

            resident.State = state;
            resident.MinutesRemaining = state == ResidentState.Performing ? minutes : 0;
        }

        private static string NeedKey(NeedType need) => char.ToLowerInvariant(need.ToString()[0]) + need.ToString().Substring(1);

        private static string GetString(Dictionary<string, object> obj, string key) =>
            obj.TryGetValue(key, out var v) ? v as string : null;

        private static double GetDouble(Dictionary<string, object> obj, string key, double fallback) =>
            obj.TryGetValue(key, out var v) && v is double d ? d : fallback;

        private static int GetInt(Dictionary<string, object> obj, string key, int fallback)
        {
            var d = GetDouble(obj, key, double.NaN);
            return double.IsNaN(d) || d != Math.Floor(d) || d < int.MinValue || d > int.MaxValue ? fallback : (int)d;
        }

        private static bool GetBool(Dictionary<string, object> obj, string key, bool fallback) =>
            obj.TryGetValue(key, out var v) && v is bool b ? b : fallback;

        private static Dictionary<string, object> GetObject(Dictionary<string, object> obj, string key) =>
            obj.TryGetValue(key, out var v) ? v as Dictionary<string, object> : null;

        private static List<object> GetList(Dictionary<string, object> obj, string key) =>
            obj.TryGetValue(key, out var v) && v is List<object> list ? list : new List<object>();
    }
}
