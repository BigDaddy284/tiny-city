using System;
using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation.Activities;
using TinyCity.Simulation.Economy;
using TinyCity.Simulation.History;
using TinyCity.Simulation.Memory;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.World;

namespace TinyCity.Simulation
{
    /// <summary>One resident's turn at the town meeting, waiting for the AI (not saved).</summary>
    public sealed class PendingMeetingTurn
    {
        public PendingMeetingTurn(long id) => Id = id;

        public long Id { get; }
    }

    /// <summary>The town meeting in progress: who came, what they said and how they voted (not saved).</summary>
    public sealed class TownMeeting
    {
        internal TownMeeting(int day) => Day = day;

        public int Day { get; }

        /// <summary>Everyone who joined, in the order they arrived.</summary>
        public List<string> Attendees { get; } = new List<string>();

        /// <summary>What each speaker said, in the order they spoke.</summary>
        public List<(string residentId, string said)> Said { get; } = new List<(string, string)>();

        /// <summary>Each voter's yes or no on each request, by the requester's id.</summary>
        public Dictionary<string, Dictionary<string, bool>> Votes { get; } = new Dictionary<string, Dictionary<string, bool>>(StringComparer.Ordinal);

        internal Dictionary<string, PendingMeetingTurn> Turns { get; } = new Dictionary<string, PendingMeetingTurn>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Roles that matter and the weekly town meeting (owner, 5 November). The work a role lists (<see cref="RoleOption.Skills"/>) is only open to its
    /// holders; everyone else can drink and wash in the dirty pond, eat raw food, fish by hand and pick up branches
    /// and stones. A first role is taken freely; changing it later is a request put to the next meeting, held a week
    /// after the start and every week after (<see cref="FirstMeetingDay"/>), where it passes only if more than half of
    /// those present vote for it.
    /// </summary>
    public sealed partial class TownSimulation
    {
        public const int MeetingStartMinute = 10 * 60;

        /// <summary>People can still join until this time; the meeting is closed and the votes counted at <see cref="MeetingCloseMinute"/>.</summary>
        public const int MeetingJoinUntilMinute = 11 * 60;

        public const int MeetingCloseMinute = 12 * 60;

        /// <summary>The longest thing one person says at the meeting.</summary>
        public const int MeetingSayLength = 200;

        /// <summary>Turns are asked for from this time, once people have gathered.</summary>
        public const int MeetingTurnsFromMinute = 10 * 60 + 30;

        private readonly Dictionary<string, string> roleRequests = new Dictionary<string, string>(StringComparer.Ordinal);
        private long nextMeetingTurnId = 1;

        /// <summary>The day of the first weekly meeting (a week after the start), or 0 for a town with no meetings. Saved.</summary>
        public int FirstMeetingDay { get; set; }

        /// <summary>Who has asked to change role, and to what (<see cref="RoleNone"/> to give it up), for the next meeting. Saved.</summary>
        public IReadOnlyDictionary<string, string> RoleRequests => roleRequests;

        /// <summary>The meeting under way, or null.</summary>
        public TownMeeting Meeting { get; private set; }

        public bool IsMeetingDay(int day) => FirstMeetingDay > 0 && day >= FirstMeetingDay && (day - FirstMeetingDay) % 7 == 0;

        /// <summary>The next meeting's day on or after <paramref name="day"/>, or 0 for none.</summary>
        public int NextMeetingDay(int day)
        {
            if (FirstMeetingDay <= 0)
            {
                return 0;
            }

            if (day <= FirstMeetingDay)
            {
                return FirstMeetingDay;
            }

            var after = (day - FirstMeetingDay) % 7;
            return after == 0 ? day : day + 7 - after;
        }

        /// <summary>Where meetings are held: the land's gathering spot.</summary>
        public WorldObject MeetingSpot => World.Objects.FirstOrDefault(o => o.Type == ObjectType.GatheringSpot);

        /// <summary>The turn this resident is waiting to take at the meeting, or null.</summary>
        public PendingMeetingTurn MeetingTurnOf(string residentId) =>
            Meeting != null && residentId != null && Meeting.Turns.TryGetValue(residentId, out var turn) ? turn : null;

        internal void SetRoleRequest(string residentId, string role) => roleRequests[residentId] = role;

        private bool MeetingAllows(Resident r, WorldObject spot)
        {
            var minute = Time.MinuteOfDay;
            return IsMeetingDay(Time.Day) && minute >= MeetingStartMinute && minute < MeetingJoinUntilMinute
                && spot != null && spot.Type == ObjectType.GatheringSpot && Meeting?.Attendees.Contains(r.Id) != true;
        }

        /// <summary>
        /// A resident's choice of role while roles matter: a first role is theirs at once, but a change (or giving it
        /// up) waits for a vote at the next meeting.
        /// </summary>
        private bool RequestRoleChange(Resident r, RoleOption role, bool none)
        {
            if (FirstMeetingDay <= 0 || r.Role == null)
            {
                return false;
            }

            var wanted = none ? RoleNone : role.Name;
            if (roleRequests.TryGetValue(r.Id, out var asked) && asked == wanted)
            {
                return true;
            }

            roleRequests[r.Id] = wanted;
            var what = none ? $"give up being the {r.Role}" : $"stop being the {r.Role} and become the {role.Name}";
            r.AddLog(Time, $"Wants to {what}: it goes to a vote at the next town meeting");
            Memories.Add(r.Id, Time, MemoryType.Event, 5, $"I want to {what}. I'll have to ask everyone at the next town meeting, on day {NextMeetingDay(Time.Day)}, and they will vote on it.", tags: new[] { "role", "meeting" });
            return true;
        }

        /// <summary>Runs the meeting each minute of meeting day: who has come, their turns to speak, and the count at the close.</summary>
        private void StepMeeting()
        {
            if (!IsMeetingDay(Time.Day))
            {
                Meeting = null;
                return;
            }

            var minute = Time.MinuteOfDay;
            if (minute == MeetingStartMinute - 60)
            {
                foreach (var r in residents.Where(o => o.IsAlive))
                {
                    r.AddLog(Time, "The town meeting starts in an hour, at the meeting circle");
                }
            }

            if (minute < MeetingStartMinute)
            {
                return;
            }

            if (minute >= MeetingCloseMinute)
            {
                if (Meeting != null && Meeting.Day == Time.Day)
                {
                    CloseMeeting(Meeting);
                    Meeting = new TownMeeting(-1);
                }

                return;
            }

            if (Meeting == null || Meeting.Day != Time.Day)
            {
                if (Meeting?.Day == -1)
                {
                    return;
                }

                Meeting = new TownMeeting(Time.Day);
            }

            foreach (var r in residents)
            {
                if (r.IsAlive && r.State == ResidentState.Performing && r.ActivityId == ActivityCatalog.AttendMeeting.Id && !Meeting.Attendees.Contains(r.Id))
                {
                    Meeting.Attendees.Add(r.Id);
                    Meeting.Turns[r.Id] = new PendingMeetingTurn(nextMeetingTurnId++);
                }
            }
        }

        /// <summary>What the AI said for one resident at the meeting, and their votes by the requester's id. Stale once the turn is gone.</summary>
        public DecisionOutcome ApplyMeetingTurn(string residentId, long turnId, string said, IReadOnlyDictionary<string, bool> votes)
        {
            var turn = MeetingTurnOf(residentId);
            if (turn == null || turn.Id != turnId)
            {
                return DecisionOutcome.Stale;
            }

            var r = GetResident(residentId);
            if (r == null || !r.IsAlive)
            {
                return DecisionOutcome.NotAlive;
            }

            Meeting.Turns.Remove(residentId);
            var clean = Social.PrivateTraits.WithoutTraitTalk(TextRules.Clean(said, MeetingSayLength), residents);
            if (clean.Length > 0)
            {
                Meeting.Said.Add((residentId, clean));
                r.AddLog(Time, $"Said at the meeting: \"{clean}\"");
            }

            var mine = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (var requester in roleRequests.Keys.Where(Meeting.Attendees.Contains))
            {
                if (votes != null && votes.TryGetValue(requester, out var yes))
                {
                    mine[requester] = yes;
                }
                else if (requester == residentId)
                {
                    mine[requester] = true;
                }
            }

            Meeting.Votes[residentId] = mine;
            return DecisionOutcome.Applied;
        }

        /// <summary>A turn the AI couldn't give: they stay quiet and don't vote.</summary>
        public void DropMeetingTurn(string residentId, long turnId)
        {
            var turn = MeetingTurnOf(residentId);
            if (turn != null && turn.Id == turnId)
            {
                Meeting.Turns.Remove(residentId);
            }
        }

        private void CloseMeeting(TownMeeting meeting)
        {
            var present = meeting.Attendees.Select(GetResident).Where(o => o != null && o.IsAlive).ToList();
            if (present.Count == 0)
            {
                History.Add(Time, TownEventKind.Meeting, "Nobody came to the town meeting.", Array.Empty<string>());
                return;
            }

            var outcomes = new List<string>();
            foreach (var requester in present.Where(o => roleRequests.ContainsKey(o.Id)).ToList())
            {
                var wanted = roleRequests[requester.Id];
                var yes = meeting.Votes.Values.Count(v => v.TryGetValue(requester.Id, out var y) && y);
                var no = meeting.Votes.Values.Count(v => v.TryGetValue(requester.Id, out var y) && !y);
                var passed = yes * 2 > present.Count;
                var none = wanted == RoleNone;
                var change = none ? $"to give up being the {requester.Role}" : $"to become the {wanted}";
                outcomes.Add($"{requester.FirstName} asked {change}: {yes} for, {no} against, so it {(passed ? "passed" : "failed")}");
                roleRequests.Remove(requester.Id);
                if (passed)
                {
                    var old = requester.Role;
                    requester.Role = none ? null : wanted;
                    requester.AddLog(Time, none ? $"The meeting voted to let them give up being the {old}" : $"The meeting voted them in as the {wanted}");
                }
                else
                {
                    requester.AddLog(Time, $"The meeting voted against their asking {change}");
                }
            }

            var heard = meeting.Said.Take(3).Select(s => $"{GetResident(s.residentId)?.FirstName} said \"{s.said}\"");
            var votesText = outcomes.Count == 0 ? "Nobody asked to change role." : string.Join("; ", outcomes) + ".";
            var summary = $"At the town meeting, with {present.Count} of us there: {votesText} {string.Join(" ", heard)}";
            foreach (var r in present)
            {
                Memories.Add(r.Id, Time, MemoryType.Event, outcomes.Count > 0 ? 6 : 4, summary, present.Where(o => o != r).Select(o => o.Id), new[] { "meeting", "role" });
            }

            History.Add(Time, TownEventKind.Meeting, $"The town meeting ({present.Count} came). {votesText}", present.Select(o => o.Id).ToArray());
        }

        /// <summary>The open land's costs and gains as an activity finishes: dirty water and raw food hurt, cleaning water fills their flasks.</summary>
        private void FinishOpenLand(Resident r, ActivityDefinition activity)
        {
            if (activity == ActivityCatalog.DrinkFromPond || activity == ActivityCatalog.EatRaw || activity == ActivityCatalog.WashInPond)
            {
                var cost = activity == ActivityCatalog.EatRaw ? FoodRules.RawFoodHealthCost
                    : activity == ActivityCatalog.WashInPond ? FoodRules.PondWashHealthCost
                    : FoodRules.DirtyWaterHealthCost;
                r.Health = Math.Max(LowestHealth, r.Health - cost);
                var felt = activity == ActivityCatalog.EatRaw ? "The raw food made me feel sick"
                    : activity == ActivityCatalog.WashInPond ? "Washing in the dirty pond left my skin itching"
                    : "The dirty pond water made me feel sick";
                r.AddLog(Time, felt);

                // Remembered once a day for each, so it comes back to them when they next think about drinking or eating.
                var tag = activity.Id;
                if (!Memories.ForResident(r.Id).Any(m => m.Time.Day == Time.Day && m.Tags.Contains(tag) && m.Tags.Contains("felt-ill")))
                {
                    Memories.Add(r.Id, Time, MemoryType.Event, 5, felt + ". Clean water and cooked food don't do that.", tags: new[] { tag, "felt-ill", "health", "water", "food" });
                }
            }
            else if (activity == ActivityCatalog.PurifyWater)
            {
                var made = Math.Min(FoodRules.PurifyPortions, FoodRules.MaxCleanWater - r.CleanWater);
                r.CleanWater += made;
                r.AddLog(Time, made > 0 ? $"Cleaned {made} portion{(made == 1 ? string.Empty : "s")} of water" : "Cleaned water, but couldn't carry any more");
            }
        }
    }
}
