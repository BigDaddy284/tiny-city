using System;
using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation.Activities;
using TinyCity.Simulation.Economy;
using TinyCity.Simulation.Health;
using TinyCity.Simulation.History;
using TinyCity.Simulation.Memory;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Social;
using TinyCity.Simulation.Time;
using TinyCity.Simulation.World;

namespace TinyCity.Simulation.Persistence
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
        public const string Format = "tiny-city-save";

        /// <summary>What saves were called before the fresh start of 30 October 2026. They aren't loaded.</summary>
        public const string FormatBeforeFreshStart = "tiny-town-save";

        /// <summary>
        /// Bump this when the save layout changes, and teach <see cref="Load"/> to upgrade the older version so
        /// residents, memories and history survive.
        /// </summary>
        /// <remarks>
        /// Version 1 is the fresh start of 30 October 2026: residents (with money, health, hobbies, the day they moved
        /// in or were born, and their parents), memories, conversation history and conversations in progress,
        /// relationships and couples, the economy and the town history. Saves made before it used the format
        /// <see cref="FormatBeforeFreshStart"/> and aren't loaded.
        /// </remarks>
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
                ["memories"] = new Dictionary<string, object>
                {
                    ["nextNumber"] = simulation.Memories.NextNumber,
                    ["records"] = simulation.Memories.All.Select(WriteMemory).ToList(),
                },
                ["conversations"] = new Dictionary<string, object>
                {
                    ["nextNumber"] = simulation.Conversations.NextNumber,
                    ["records"] = simulation.Conversations.All.Select(WriteConversation).ToList(),
                },
                ["relationships"] = simulation.Relationships.All.Select(WriteRelationship).ToList(),
                ["activeConversations"] = simulation.ActiveConversations.Select(WriteSession).ToList(),
                ["economy"] = WriteEconomy(simulation.Economy),
                ["partnerships"] = simulation.Relationships.Partnerships.Select(p => (object)new Dictionary<string, object>
                {
                    ["firstId"] = p.FirstId,
                    ["secondId"] = p.SecondId,
                    ["stage"] = p.Stage.ToString(),
                    ["sinceMinutes"] = p.Since.TotalMinutes,
                    ["stageSinceMinutes"] = p.StageSince.TotalMinutes,
                }).ToList(),
                ["history"] = new Dictionary<string, object>
                {
                    ["nextNumber"] = simulation.History.NextNumber,
                    ["records"] = simulation.History.Events.Select(e => (object)new Dictionary<string, object>
                    {
                        ["id"] = e.Id,
                        ["gameMinutes"] = e.Time.TotalMinutes,
                        ["kind"] = e.Kind.ToString(),
                        ["summary"] = e.Summary,
                        ["residentIds"] = e.ResidentIds.Cast<object>().ToList(),
                    }).ToList(),
                },
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

            if (GetString(root, "format") == FormatBeforeFreshStart)
            {
                return Fail("This town was saved before Tiny City's fresh start and can't be loaded.");
            }

            if (GetString(root, "format") != Format)
            {
                return Fail("This is not a Tiny City save file.");
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

            var simulation = new TownSimulation(world, residents, new GameTime((long)minutes));
            ReadMemories(GetObject(root, "memories"), simulation, warnings);
            ReadConversations(GetObject(root, "conversations"), simulation, warnings);
            ReadRelationships(GetList(root, "relationships"), simulation, warnings);
            ReadSessions(GetList(root, "activeConversations"), simulation, warnings);
            ReadEconomy(GetObject(root, "economy"), simulation, warnings);
            ReadPartnerships(GetList(root, "partnerships"), simulation, warnings);
            ReadHistory(GetObject(root, "history"), simulation, warnings);

            ReconcileSocialState(simulation, warnings);

            return new SaveLoadResult(simulation, errors, warnings);

            SaveLoadResult Fail(string message) => new SaveLoadResult(null, new[] { message }, warnings);
        }

        private static object WriteResident(Resident r)
        {
            var deciding = r.State == ResidentState.Deciding;
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
                    ["workDays"] = r.Job.WorkDays,
                    ["hourlyWage"] = r.Job.HourlyWagePence,
                },
                ["money"] = r.MoneyPence,
                ["groceries"] = r.Groceries,
                ["weeklyRent"] = r.WeeklyRentPence,
                ["weeklyIncome"] = r.WeeklyIncomePence,
                ["incomeLabel"] = r.IncomeLabel,
                ["unpaidWorkMinutes"] = r.UnpaidWorkMinutes,
                ["health"] = r.Health,
                ["illness"] = r.IllnessId == null ? null : new Dictionary<string, object>
                {
                    ["id"] = r.IllnessId,
                    ["minutesLeft"] = r.IllnessMinutesLeft,
                    ["treated"] = r.IllnessTreated,
                },
                ["exposedDay"] = r.ExposedDay,
                ["sickDay"] = r.SickDay,
                ["death"] = r.DiedAt == null ? null : new Dictionary<string, object>
                {
                    ["gameMinutes"] = r.DiedAt.Value.TotalMinutes,
                    ["cause"] = r.CauseOfDeath,
                },
                ["hobbies"] = r.Hobbies.Cast<object>().ToList(),
                ["openToRomance"] = r.OpenToRomance,
                ["movedInDay"] = r.MovedInDay,
                ["bornDay"] = r.BornDay,
                ["parents"] = r.ParentIds.Cast<object>().ToList(),
                ["alive"] = r.IsAlive,
                ["needs"] = needs,
                ["location"] = new Dictionary<string, object>
                {
                    ["mapId"] = r.Location.MapId,
                    ["x"] = r.Location.Position.X,
                    ["y"] = r.Location.Position.Y,
                },
                // A resident waiting on the AI is saved as idle: unfinished AI requests are never saved as decisions.
                ["state"] = deciding ? nameof(ResidentState.Idle) : r.State.ToString(),
                ["activityId"] = deciding ? null : r.ActivityId,
                ["targetObjectId"] = deciding ? null : r.TargetObjectId,
                ["minutesRemaining"] = deciding ? 0 : r.MinutesRemaining,
                ["goal"] = deciding ? string.Empty : r.Goal,
                ["goalSource"] = deciding ? nameof(DecisionSource.Routine) : r.GoalSource.ToString(),
                ["targetResidentId"] = deciding ? null : r.TargetResidentId,
                ["conversationId"] = deciding ? null : r.ConversationId,
                ["lastReflectionDay"] = r.LastReflectionDay,
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
                    var days = GetInt(jobData, "workDays", -1);
                    if (days <= 0 || days > Job.EveryDay)
                    {
                        days = GetBool(jobData, "worksWeekends", false) ? Job.EveryDay : Job.Weekdays;
                    }

                    job = new Job(GetString(jobData, "title") ?? "Worker", GetString(jobData, "workObjectId"), start, end, days, Math.Max(0, GetLong(jobData, "hourlyWage", 0)));
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
            resident.LastReflectionDay = Math.Max(0, GetInt(data, "lastReflectionDay", 0));
            resident.MoneyPence = GetLong(data, "money", 0);
            resident.Groceries = Math.Max(0, Math.Min(GetInt(data, "groceries", 0), 1000));
            resident.WeeklyRentPence = Math.Max(0, GetLong(data, "weeklyRent", 0));
            resident.WeeklyIncomePence = Math.Max(0, GetLong(data, "weeklyIncome", 0));
            resident.IncomeLabel = TextRules.Clean(GetString(data, "incomeLabel"), 40);
            ReadHealth(resident, data, label, warnings);
            resident.UnpaidWorkMinutes = job == null ? 0 : Math.Max(0, Math.Min(GetInt(data, "unpaidWorkMinutes", 0), GameTime.MinutesPerDay));
            foreach (var entry in GetList(data, "log").OfType<Dictionary<string, object>>().Take(Resident.MaxLogEntries))
            {
                var minute = GetDouble(entry, "gameMinutes", -1);
                if (minute >= 0)
                {
                    resident.AddLog(new GameTime((long)minute), GetString(entry, "text"));
                }
            }

            RestoreActivity(resident, data, world, label, warnings);
            if (!resident.IsAlive)
            {
                resident.ResetActivity();
            }

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

            if (state == ResidentState.Idle || state == ResidentState.Deciding)
            {
                return;
            }

            var minutes = Math.Max(0, GetInt(data, "minutesRemaining", 0));
            if (state == ResidentState.Talking)
            {
                // Checked against the saved conversations once they are loaded; see ReconcileSocialState.
                resident.State = ResidentState.Talking;
                resident.ActivityId = ActivityCatalog.Chat.Id;
                resident.ConversationId = GetString(data, "conversationId");
                resident.Goal = GetString(data, "goal") ?? string.Empty;
                return;
            }

            var partnerId = GetString(data, "targetResidentId");
            if (state == ResidentState.Travelling && !string.IsNullOrEmpty(partnerId))
            {
                // Looking for someone to chat with; the partner is checked once every resident is loaded.
                resident.State = ResidentState.Travelling;
                resident.ActivityId = ActivityCatalog.Chat.Id;
                resident.TargetResidentId = partnerId;
                resident.MinutesRemaining = Math.Max(1, Math.Min(minutes, SocialRules.SeekMinutes));
                resident.Goal = GetString(data, "goal") ?? string.Empty;
                ReadGoalSource(resident, data);
                return;
            }

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
            ReadGoalSource(resident, data);
        }

        private static void ReadGoalSource(Resident resident, Dictionary<string, object> data)
        {
            if (Enum.TryParse(GetString(data, "goalSource") ?? string.Empty, out DecisionSource source) && Enum.IsDefined(typeof(DecisionSource), source))
            {
                resident.GoalSource = source;
            }
        }

        private static object WriteMemory(MemoryRecord m) => new Dictionary<string, object>
        {
            ["id"] = m.Id,
            ["residentId"] = m.ResidentId,
            ["gameMinutes"] = m.Time.TotalMinutes,
            ["type"] = m.Type.ToString(),
            ["importance"] = m.Importance,
            ["summary"] = m.Summary,
            ["participants"] = m.Participants.Cast<object>().ToList(),
            ["tags"] = m.Tags.Cast<object>().ToList(),
        };

        private static object WriteConversation(ConversationRecord c) => new Dictionary<string, object>
        {
            ["id"] = c.Id,
            ["kind"] = c.Kind.ToString(),
            ["startedMinutes"] = c.Started.TotalMinutes,
            ["participants"] = c.Participants.Cast<object>().ToList(),
            ["context"] = c.Context,
            ["lines"] = WriteLines(c.Lines),
        };

        private static List<object> WriteLines(IEnumerable<ConversationLine> lines) => lines.Select(l => (object)new Dictionary<string, object>
        {
            ["speakerId"] = l.SpeakerId,
            ["gameMinutes"] = l.Time.TotalMinutes,
            ["text"] = l.Text,
            ["tone"] = l.Tone.ToString(),
            ["fromAi"] = l.FromAi,
        }).ToList();

        private static List<ConversationLine> ReadLines(Dictionary<string, object> data, ICollection<string> participants)
        {
            var lines = new List<ConversationLine>();
            foreach (var l in GetList(data, "lines").OfType<Dictionary<string, object>>())
            {
                var speaker = GetString(l, "speakerId");
                var at = GetDouble(l, "gameMinutes", -1);
                var text = TextRules.Clean(GetString(l, "text"), ConversationLine.MaxTextLength);
                if (!Enum.TryParse(GetString(l, "tone") ?? string.Empty, out ConversationTone tone) || !Enum.IsDefined(typeof(ConversationTone), tone))
                {
                    tone = ConversationTone.Neutral;
                }

                if (participants.Contains(speaker) && at >= 0 && text.Length > 0)
                {
                    lines.Add(new ConversationLine(speaker, new GameTime((long)at), text, tone, GetBool(l, "fromAi", false)));
                }
            }

            return lines;
        }

        private static object WriteRelationship(Relationship r) => new Dictionary<string, object>
        {
            ["ownerId"] = r.OwnerId,
            ["otherId"] = r.OtherId,
            ["friendship"] = r.Friendship,
            ["trust"] = r.Trust,
            ["respect"] = r.Respect,
            ["dislike"] = r.Dislike,
            ["interactions"] = r.Interactions,
            ["lastInteractionMinutes"] = r.LastInteractionMinute,
            ["romance"] = r.Romance,
            ["brokeUpMinutes"] = r.BrokeUpMinute,
        };

        private static void ReadRelationships(List<object> list, TownSimulation simulation, List<string> warnings)
        {
            var skipped = 0;
            foreach (var d in list)
            {
                var data = d as Dictionary<string, object>;
                var owner = data == null ? null : GetString(data, "ownerId");
                var other = data == null ? null : GetString(data, "otherId");
                if (simulation.GetResident(owner) == null || simulation.GetResident(other) == null || owner == other
                    || simulation.Relationships.TryGet(owner, other, out _))
                {
                    skipped++;
                    continue;
                }

                var defaults = simulation.Relationships.Get(owner, other);
                defaults.Set(
                    GetDouble(data, "friendship", defaults.Friendship),
                    GetDouble(data, "trust", defaults.Trust),
                    GetDouble(data, "respect", defaults.Respect),
                    GetDouble(data, "dislike", defaults.Dislike),
                    GetDouble(data, "romance", 0));
                defaults.Interactions = Math.Max(0, GetInt(data, "interactions", 0));
                var last = GetDouble(data, "lastInteractionMinutes", Relationship.Never);
                defaults.LastInteractionMinute = last >= 0 && last <= simulation.Time.TotalMinutes && last == Math.Floor(last) ? (long)last : Relationship.Never;
                var brokeUp = GetDouble(data, "brokeUpMinutes", Relationship.Never);
                defaults.BrokeUpMinute = brokeUp >= 0 && brokeUp <= simulation.Time.TotalMinutes && brokeUp == Math.Floor(brokeUp) ? (long)brokeUp : Relationship.Never;
            }

            if (skipped > 0)
            {
                warnings.Add($"{skipped} damaged or duplicate relationship record(s) were skipped.");
            }
        }

        private static object WriteSession(ConversationSession c) => new Dictionary<string, object>
        {
            ["id"] = c.Id,
            ["participants"] = c.ParticipantIds.Cast<object>().ToList(),
            ["startedMinutes"] = c.Started.TotalMinutes,
            ["maxLines"] = c.MaxLines,
            ["place"] = c.Place,
            ["topic"] = c.Topic,
            ["nextLineMinutes"] = c.NextLineAt,
            ["waitedMinutes"] = c.WaitedMinutes,
            ["lines"] = WriteLines(c.Lines),
        };

        private static void ReadSessions(List<object> list, TownSimulation simulation, List<string> warnings)
        {
            var skipped = 0;
            foreach (var d in list)
            {
                var data = d as Dictionary<string, object>;
                var id = data == null ? null : GetString(data, "id");
                var participants = data == null ? new List<string>() : GetList(data, "participants").OfType<string>().ToList();
                var started = data == null ? -1 : GetDouble(data, "startedMinutes", -1);
                if (string.IsNullOrWhiteSpace(id) || simulation.GetConversation(id) != null || simulation.Conversations.All.Any(c => c.Id == id)
                    || participants.Count != 2 || participants[0] == participants[1]
                    || participants.Any(p => simulation.GetResident(p) == null || !simulation.GetResident(p).IsAlive)
                    || simulation.ActiveConversations.Any(c => c.ParticipantIds.Intersect(participants).Any())
                    || started < 0 || started > simulation.Time.TotalMinutes || started != Math.Floor(started))
                {
                    skipped++;
                    continue;
                }

                var maxLines = Math.Max(SocialRules.MinLines, Math.Min(SocialRules.MaxLines, GetInt(data, "maxLines", SocialRules.MinLines)));
                var session = new ConversationSession(id, participants[0], participants[1], new GameTime((long)started), maxLines, TextRules.Clean(GetString(data, "place"), 60));
                session.Lines.AddRange(ReadLines(data, participants).Take(maxLines - 1));
                session.Topic = SocialRules.CleanTopic(GetString(data, "topic"));
                var waited = GetDouble(data, "waitedMinutes", 0);
                session.WaitedMinutes = waited >= 0 && waited == Math.Floor(waited) && waited <= simulation.Time.TotalMinutes - started ? (long)waited : 0;
                var next = GetDouble(data, "nextLineMinutes", simulation.Time.TotalMinutes);
                session.NextLineAt = next >= 0 && next == Math.Floor(next) && next <= simulation.Time.TotalMinutes + SocialRules.MinutesPerLine
                    ? (long)next
                    : simulation.Time.TotalMinutes;
                simulation.RestoreConversation(session, NumberAfter(id));
            }

            if (skipped > 0)
            {
                warnings.Add($"{skipped} damaged conversation(s) in progress were dropped.");
            }
        }

        /// <summary>
        /// Makes residents and live conversations agree: a resident marked as talking must be in a loaded conversation,
        /// both partners of a conversation must be talking in it, and a resident looking for someone must look for a living
        /// neighbour. Anything that doesn't fit is dropped with a warning and those residents choose again.
        /// </summary>
        private static void ReconcileSocialState(TownSimulation simulation, List<string> warnings)
        {
            foreach (var session in simulation.ActiveConversations.ToList())
            {
                var members = session.ParticipantIds.Select(simulation.GetResident).ToList();
                if (members.Any(m => m.State != ResidentState.Talking || m.ConversationId != session.Id))
                {
                    simulation.DropConversation(session);
                    warnings.Add($"Conversation {session.Id} did not match the residents in it, so it was dropped.");
                }
            }

            foreach (var r in simulation.Residents)
            {
                if (r.State == ResidentState.Talking && simulation.GetConversation(r.ConversationId) == null)
                {
                    r.ResetActivity();
                    warnings.Add($"{r.FullName}: was talking in a conversation that couldn't be restored, so they will choose what to do next.");
                }

                if (r.TargetResidentId != null)
                {
                    var other = simulation.GetResident(r.TargetResidentId);
                    if (other == null || other == r || !other.IsAlive)
                    {
                        r.ResetActivity();
                        warnings.Add($"{r.FullName}: was looking for someone who isn't in the town, so they will choose what to do next.");
                    }
                }
            }
        }

        private static void ReadMemories(Dictionary<string, object> data, TownSimulation simulation, List<string> warnings)
        {
            if (data == null)
            {
                warnings.Add("Memories were missing, so residents start with no memories.");
                return;
            }

            var ids = new HashSet<string>();
            var skipped = 0;
            var next = Math.Max(1, (long)GetDouble(data, "nextNumber", 1));
            foreach (var item in GetList(data, "records"))
            {
                var m = item as Dictionary<string, object>;
                var id = m == null ? null : GetString(m, "id");
                var residentId = m == null ? null : GetString(m, "residentId");
                var minutes = m == null ? -1 : GetDouble(m, "gameMinutes", -1);
                var summary = m == null ? string.Empty : TextRules.Clean(GetString(m, "summary"), MemoryRecord.MaxSummaryLength);
                if (string.IsNullOrWhiteSpace(id) || !ids.Add(id) || simulation.GetResident(residentId) == null || minutes < 0 || summary.Length == 0
                    || !Enum.TryParse(GetString(m, "type") ?? string.Empty, out MemoryType type) || !Enum.IsDefined(typeof(MemoryType), type))
                {
                    skipped++;
                    continue;
                }

                var participants = GetList(m, "participants").OfType<string>().Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().ToList();
                if (!participants.Contains(residentId))
                {
                    participants.Insert(0, residentId);
                }

                var tags = GetList(m, "tags").OfType<string>().Where(t => !string.IsNullOrWhiteSpace(t)).Distinct().ToList();
                var record = new MemoryRecord(id, residentId, new GameTime((long)minutes), type, MemoryStore.ClampImportance(GetInt(m, "importance", 1)), summary, participants, tags);
                simulation.Memories.Restore(record, Math.Max(next, NumberAfter(id)));
            }

            if (skipped > 0)
            {
                warnings.Add($"{skipped} damaged memory record(s) were skipped.");
            }
        }

        private static void ReadConversations(Dictionary<string, object> data, TownSimulation simulation, List<string> warnings)
        {
            if (data == null)
            {
                warnings.Add("Conversation history was missing, so it starts empty.");
                return;
            }

            var ids = new HashSet<string>();
            var skipped = 0;
            var next = Math.Max(1, (long)GetDouble(data, "nextNumber", 1));
            foreach (var item in GetList(data, "records"))
            {
                var c = item as Dictionary<string, object>;
                var id = c == null ? null : GetString(c, "id");
                var started = c == null ? -1 : GetDouble(c, "startedMinutes", -1);
                var participants = c == null ? new List<string>() : GetList(c, "participants").OfType<string>().Where(p => simulation.GetResident(p) != null).Distinct().ToList();
                if (string.IsNullOrWhiteSpace(id) || !ids.Add(id) || started < 0 || participants.Count == 0
                    || !Enum.TryParse(GetString(c, "kind") ?? string.Empty, out ConversationKind kind) || !Enum.IsDefined(typeof(ConversationKind), kind))
                {
                    skipped++;
                    continue;
                }

                var lines = ReadLines(c, participants);
                if (lines.Count == 0)
                {
                    skipped++;
                    continue;
                }

                var record = new ConversationRecord(id, kind, new GameTime((long)started), participants, TextRules.Clean(GetString(c, "context"), 60), lines);
                simulation.Conversations.Restore(record, Math.Max(next, NumberAfter(id)));
            }

            simulation.Conversations.RestoreNextNumber(next);

            if (skipped > 0)
            {
                warnings.Add($"{skipped} damaged conversation record(s) were skipped.");
            }
        }

        /// <summary>Health, illness, death and hobbies. Missing values (older saves) give a well resident of their age.</summary>
        private static void ReadHealth(Resident resident, Dictionary<string, object> data, string label, List<string> warnings)
        {
            var health = GetDouble(data, "health", double.NaN);
            resident.Health = double.IsNaN(health) ? HealthRules.StartingHealthFor(resident.AgeYears) : Math.Max(0, Math.Min(HealthRules.MaxHealthFor(resident.AgeYears), health));

            var illness = GetObject(data, "illness");
            if (illness != null)
            {
                var kind = IllnessCatalog.Get(GetString(illness, "id"));
                if (kind == null)
                {
                    warnings.Add($"{label}: an unknown illness was dropped.");
                }
                else
                {
                    resident.IllnessId = kind.Id;
                    resident.IllnessMinutesLeft = Math.Max(1, Math.Min(GetInt(illness, "minutesLeft", kind.RecoveryMinutes), kind.RecoveryMinutes));
                    resident.IllnessTreated = kind.Serious && GetBool(illness, "treated", false);
                }
            }

            resident.ExposedDay = Math.Max(0, GetInt(data, "exposedDay", 0));
            resident.SickDay = Math.Max(0, GetInt(data, "sickDay", 0));
            resident.SetHobbies(GetList(data, "hobbies").OfType<string>().Where(h => ActivityCatalog.ForHobby(h) != null));
            resident.OpenToRomance = GetBool(data, "openToRomance", true);
            resident.MovedInDay = Math.Max(0, GetInt(data, "movedInDay", 0));
            resident.BornDay = Math.Max(0, GetInt(data, "bornDay", 0));
            resident.SetParents(GetList(data, "parents").OfType<string>());

            var death = GetObject(data, "death");
            if (!resident.IsAlive)
            {
                var minute = death == null ? -1 : GetDouble(death, "gameMinutes", -1);
                resident.DiedAt = new GameTime(minute >= 0 ? (long)minute : 0);
                resident.CauseOfDeath = TextRules.Clean(death == null ? null : GetString(death, "cause"), 40);
                if (resident.CauseOfDeath.Length == 0)
                {
                    resident.CauseOfDeath = "unknown causes";
                }

                resident.IllnessId = null;
                resident.IllnessMinutesLeft = 0;
                resident.IllnessTreated = false;
            }
        }

        private static object WriteEconomy(TownEconomy economy) => new Dictionary<string, object>
        {
            ["businesses"] = economy.Businesses.Select(b => (object)new Dictionary<string, object>
            {
                ["id"] = b.Business.Id,
                ["takings"] = b.TakingsPence,
                ["stock"] = b.Business.Items.ToDictionary(i => i.Id, i => (object)b.Stock(i.Id)),
            }).ToList(),
            ["ledger"] = new Dictionary<string, object>
            {
                ["nextNumber"] = economy.Ledger.NextNumber,
                ["records"] = economy.Ledger.All.Select(t => (object)new Dictionary<string, object>
                {
                    ["id"] = t.Id,
                    ["gameMinutes"] = t.Time.TotalMinutes,
                    ["residentId"] = t.ResidentId,
                    ["amount"] = t.AmountPence,
                    ["kind"] = t.Kind.ToString(),
                    ["description"] = t.Description,
                    ["businessId"] = t.BusinessId,
                }).ToList(),
            },
        };

        private static void ReadEconomy(Dictionary<string, object> data, TownSimulation simulation, List<string> warnings)
        {
            if (data == null)
            {
                warnings.Add("Shop stock and the money records were missing, so shops were restocked and the records start afresh.");
                return;
            }

            foreach (var b in GetList(data, "businesses").OfType<Dictionary<string, object>>())
            {
                var shop = simulation.Economy.GetBusiness(GetString(b, "id"));
                if (shop == null)
                {
                    continue;
                }

                shop.TakingsPence = Math.Max(0, GetLong(b, "takings", 0));
                var stock = GetObject(b, "stock");
                foreach (var item in shop.Business.Items)
                {
                    var amount = stock == null ? item.MaxStock : GetInt(stock, item.Id, item.MaxStock);
                    shop.SetStock(item.Id, Math.Max(0, Math.Min(amount, item.MaxStock)));
                }
            }

            var ledger = GetObject(data, "ledger");
            if (ledger == null)
            {
                return;
            }

            var next = Math.Max(1, GetLong(ledger, "nextNumber", 1));
            var skipped = 0;
            var seen = new HashSet<string>();
            foreach (var t in GetList(ledger, "records").Take(Ledger.MaxRecords))
            {
                var record = t as Dictionary<string, object>;
                var id = record == null ? null : GetString(record, "id");
                var minute = record == null ? -1 : GetDouble(record, "gameMinutes", -1);
                if (id == null || !id.StartsWith("txn-") || !seen.Add(id) || minute < 0 || minute > simulation.Time.TotalMinutes
                    || simulation.GetResident(GetString(record, "residentId")) == null
                    || !Enum.TryParse(GetString(record, "kind"), out TransactionKind kind) || !Enum.IsDefined(typeof(TransactionKind), kind))
                {
                    skipped++;
                    continue;
                }

                var transaction = new Transaction(id, new GameTime((long)minute), GetString(record, "residentId"), GetLong(record, "amount", 0), kind, TextRules.Clean(GetString(record, "description"), 80), GetString(record, "businessId"));
                simulation.Economy.Ledger.Restore(transaction, Math.Max(next, NumberAfter(id)));
            }

            simulation.Economy.Ledger.RestoreNextNumber(next);
            if (skipped > 0)
            {
                warnings.Add($"{skipped} damaged money record(s) were skipped.");
            }
        }

        /// <summary>Couples. Both must be living residents of this town, neither already in a couple, and the dates in the past.</summary>
        private static void ReadPartnerships(List<object> list, TownSimulation simulation, List<string> warnings)
        {
            var skipped = 0;
            foreach (var d in list)
            {
                var data = d as Dictionary<string, object>;
                var a = data == null ? null : simulation.GetResident(GetString(data, "firstId"));
                var b = data == null ? null : simulation.GetResident(GetString(data, "secondId"));
                var since = data == null ? -1 : GetDouble(data, "sinceMinutes", -1);
                var stageSince = data == null ? -1 : GetDouble(data, "stageSinceMinutes", since);
                if (a == null || b == null || a == b || !a.IsAlive || !b.IsAlive
                    || simulation.Relationships.PartnershipOf(a.Id) != null || simulation.Relationships.PartnershipOf(b.Id) != null
                    || !Enum.TryParse(GetString(data, "stage"), out RomanceStage stage) || !Enum.IsDefined(typeof(RomanceStage), stage)
                    || since < 0 || stageSince < since || stageSince > simulation.Time.TotalMinutes)
                {
                    skipped++;
                    continue;
                }

                var couple = simulation.Relationships.StartPartnership(a.Id, b.Id, new GameTime((long)since));
                couple.Stage = stage;
                couple.StageSince = new GameTime((long)stageSince);
            }

            if (skipped > 0)
            {
                warnings.Add($"{skipped} damaged couple record(s) were skipped.");
            }
        }

        private static void ReadHistory(Dictionary<string, object> data, TownSimulation simulation, List<string> warnings)
        {
            if (data == null)
            {
                warnings.Add("The town history was missing, so it starts afresh.");
                return;
            }

            var skipped = 0;
            foreach (var e in GetList(data, "records").Take(TownHistory.MaxEvents))
            {
                var record = e as Dictionary<string, object>;
                var id = record == null ? null : GetString(record, "id");
                var minute = record == null ? -1 : GetDouble(record, "gameMinutes", -1);
                if (id == null || !id.StartsWith("evt-") || simulation.History.Events.Any(x => x.Id == id) || minute < 0 || minute > simulation.Time.TotalMinutes
                    || !Enum.TryParse(GetString(record, "kind"), out TownEventKind kind) || !Enum.IsDefined(typeof(TownEventKind), kind))
                {
                    skipped++;
                    continue;
                }

                var ids = GetList(record, "residentIds").OfType<string>().Where(i => simulation.GetResident(i) != null);
                simulation.History.Restore(new TownEvent(id, new GameTime((long)minute), kind, TextRules.Clean(GetString(record, "summary"), TownHistory.MaxSummaryLength), ids));
            }

            simulation.History.RestoreNextNumber(Math.Max(1, GetLong(data, "nextNumber", 1)));
            if (skipped > 0)
            {
                warnings.Add($"{skipped} damaged town history record(s) were skipped.");
            }
        }

        /// <summary>For an id like "mem-000041", the number after it (42), so new ids never collide with loaded ones.</summary>
        private static long NumberAfter(string id)
        {
            var dash = id.LastIndexOf('-');
            return dash >= 0 && long.TryParse(id.Substring(dash + 1), out var n) && n >= 0 && n < long.MaxValue ? n + 1 : 1;
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

        private static long GetLong(Dictionary<string, object> obj, string key, long fallback)
        {
            var d = GetDouble(obj, key, double.NaN);
            const double limit = 9e15;
            return double.IsNaN(d) || d != Math.Floor(d) || d < -limit || d > limit ? fallback : (long)d;
        }

        private static bool GetBool(Dictionary<string, object> obj, string key, bool fallback) =>
            obj.TryGetValue(key, out var v) && v is bool b ? b : fallback;

        private static Dictionary<string, object> GetObject(Dictionary<string, object> obj, string key) =>
            obj.TryGetValue(key, out var v) ? v as Dictionary<string, object> : null;

        private static List<object> GetList(Dictionary<string, object> obj, string key) =>
            obj.TryGetValue(key, out var v) && v is List<object> list ? list : new List<object>();
    }
}
