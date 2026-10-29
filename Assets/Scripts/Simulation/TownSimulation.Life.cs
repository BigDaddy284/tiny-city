using System.Linq;
using TinyCity.Simulation.Activities;
using TinyCity.Simulation.Health;
using TinyCity.Simulation.History;
using TinyCity.Simulation.Memory;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Social;
using TinyCity.Simulation.Time;

namespace TinyCity.Simulation
{
    /// <summary>Birthdays, health, illness and death. All of it follows <see cref="HealthRules"/>; nothing here is up to the AI.</summary>
    public sealed partial class TownSimulation
    {
        /// <summary>Once-a-day events: birthdays at midnight, old age in the small hours, falling ill before breakfast.</summary>
        private void StepLifeEvents()
        {
            var minute = Time.MinuteOfDay;
            if (minute != 0 && minute != HealthRules.OldAgeCheckMinute && minute != HealthRules.IllnessCheckMinute)
            {
                return;
            }

            foreach (var r in residents.Where(r => r.IsAlive).ToList())
            {
                if (minute == 0 && HealthRules.IsBirthday(r, Time.Day))
                {
                    CelebrateBirthday(r);
                }
                else if (minute == HealthRules.OldAgeCheckMinute && HealthRules.DiesOfOldAge(r, Time.Day))
                {
                    Die(r, "old age");
                }
                else if (minute == HealthRules.IllnessCheckMinute)
                {
                    var illness = HealthRules.CatchesIllness(r, Time.Day);
                    if (illness != null)
                    {
                        FallIll(r, illness);
                    }
                }
            }
        }

        private void CelebrateBirthday(Resident r)
        {
            var stage = r.LifeStage;
            r.AgeYears++;
            r.AddLog(Time, $"Turned {r.AgeYears} today");
            Memories.Add(r.Id, Time, MemoryType.Event, 4, $"It was my {Ordinal(r.AgeYears)} birthday.", tags: new[] { "birthday", "age" });
            History.Add(Time, TownEventKind.Birthday, $"{r.FullName} turned {r.AgeYears}.", new[] { r.Id });
            if (r.LifeStage != stage)
            {
                Memories.Add(r.Id, Time, MemoryType.Event, 6, LifeStageSummary(r.LifeStage), tags: new[] { "age", "life-stage" });
                History.Add(Time, TownEventKind.LifeStage, $"{r.FullName} is now {StageWords(r.LifeStage)}.", new[] { r.Id });
            }

            r.Health = System.Math.Min(r.Health, HealthRules.MaxHealthFor(r.AgeYears));
        }

        /// <summary>Starts an illness. Used by the daily check, and by tests and future events.</summary>
        internal void FallIll(Resident r, Illness illness)
        {
            r.IllnessId = illness.Id;
            r.IllnessMinutesLeft = illness.RecoveryMinutes;
            r.IllnessTreated = false;
            r.AddLog(Time, $"Woke up with {illness.Name}");
            Memories.Add(r.Id, Time, MemoryType.Event, illness.Serious ? 7 : 4, $"I came down with {illness.Name}.", tags: new[] { "health", "ill", illness.Id });
            History.Add(Time, illness.Serious ? TownEventKind.SeriousIllness : TownEventKind.Illness, $"{r.FullName} came down with {illness.Name}.", new[] { r.Id });
        }

        /// <summary>One minute of health after needs have changed. Sleeping and resting in bed help recovery.</summary>
        private void StepHealth(Resident r, ActivityDefinition activity)
        {
            var resting = r.State == ResidentState.Performing && (activity == ActivityCatalog.Sleep || activity == ActivityCatalog.Rest);
            var illness = IllnessCatalog.Get(r.IllnessId);
            switch (HealthRules.ApplyMinute(r, resting))
            {
                case HealthEvent.Recovered:
                    r.AddLog(Time, $"Felt better: over {illness.Name}");
                    Memories.Add(r.Id, Time, MemoryType.Event, illness.Serious ? 6 : 3, $"I got over {illness.Name}.", tags: new[] { "health", "recovered", illness.Id });
                    History.Add(Time, TownEventKind.Recovered, $"{r.FullName} got over {illness.Name}.", new[] { r.Id });
                    break;
                case HealthEvent.Worsened:
                    r.AddLog(Time, $"Got worse: {illness.Name} turned into {IllnessCatalog.Get(r.IllnessId).Name}");
                    Memories.Add(r.Id, Time, MemoryType.Event, 7, $"My {illness.Name} turned into {IllnessCatalog.Get(r.IllnessId).Name}; I need to see the doctor.", tags: new[] { "health", "ill", r.IllnessId });
                    History.Add(Time, TownEventKind.SeriousIllness, $"{r.FullName}'s {illness.Name} turned into {IllnessCatalog.Get(r.IllnessId).Name}.", new[] { r.Id });
                    break;
                case HealthEvent.Died:
                    Die(r, illness != null ? IllnessCatalog.Get(r.IllnessId)?.Name ?? illness.Name : "neglect");
                    break;
            }
        }

        /// <summary>Called as an activity finishes normally, before the resident is freed.</summary>
        private void FinishActivity(Resident r, ActivityDefinition activity)
        {
            if (activity == ActivityCatalog.SeeDoctor && HealthRules.NeedsDoctor(r))
            {
                r.IllnessTreated = true;
                r.AddLog(Time, "The doctor treated them; recovery can begin");
                Memories.Add(r.Id, Time, MemoryType.Event, 6, $"The doctor at the clinic treated my {IllnessCatalog.Get(r.IllnessId).Name}.", tags: new[] { "health", "clinic", r.IllnessId });
            }
        }

        /// <summary>Too unwell for work: said once a day, and it costs a day's wages.</summary>
        private void NoteSickDay(Resident r)
        {
            if (ActivityPlanner.IsWorkTime(r, Time) && !HealthRules.CanWork(r) && r.SickDay != Time.Day)
            {
                r.SickDay = Time.Day;
                r.AddLog(Time, $"Called in sick ({IllnessCatalog.Get(r.IllnessId).Name})");
            }
        }

        /// <summary>Contagious illnesses can pass on through a chat.</summary>
        private void NoteExposure(Resident a, Resident b)
        {
            if (IllnessCatalog.Get(b.IllnessId)?.Contagious == true)
            {
                a.ExposedDay = Time.Day;
            }

            if (IllnessCatalog.Get(a.IllnessId)?.Contagious == true)
            {
                b.ExposedDay = Time.Day;
            }
        }

        /// <summary>
        /// A resident dies: the record is kept for good (date and cause), they stop living and using the AI, and
        /// everyone else remembers them, more deeply if they were friends.
        /// </summary>
        internal void Die(Resident r, string cause)
        {
            if (!r.IsAlive)
            {
                return;
            }

            var session = GetConversation(r.ConversationId);
            if (session != null)
            {
                EndConversation(session, r, "they were taken ill");
            }

            r.ResetActivity();
            r.ClearPlan();
            r.IsAlive = false;
            r.DiedAt = Time;
            r.CauseOfDeath = cause;
            r.UnpaidWorkMinutes = 0;
            r.AddLog(Time, $"Died of {cause}");
            var partnerId = Relationships.PartnerOf(r.Id);
            Widow(r);
            History.Add(Time, TownEventKind.Death, $"{r.FullName} died of {cause}, aged {r.AgeYears}.", new[] { r.Id });

            foreach (var other in residents.Where(o => o.IsAlive && o != r && o.Id != partnerId))
            {
                var met = Relationships.TryGet(other.Id, r.Id, out var rel) && rel.HaveMet;
                var close = met && rel.Friendship >= SocialRules.GoodFriendsAt;
                var summary = close ? $"My dear friend {r.FullName} died of {cause}. I'll miss them terribly."
                    : met ? $"{r.FullName} died of {cause}. I knew them a little."
                    : $"{r.FullName}, who lived in the city, died of {cause}.";
                Memories.Add(other.Id, Time, MemoryType.Event, close ? 9 : met ? 7 : 5, summary, new[] { r.Id }, new[] { r.Id, "death", "loss" });
            }
        }

        private static string Ordinal(int n)
        {
            var suffix = (n % 100) is 11 or 12 or 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
            return n + suffix;
        }

        private static string LifeStageSummary(LifeStage stage)
        {
            switch (stage)
            {
                case LifeStage.Adult:
                    return "I'm thirty now: properly grown up, apparently.";
                case LifeStage.MiddleAged:
                    return "I've reached middle age. Where did the years go?";
                case LifeStage.Elderly:
                    return "I'm officially elderly now, and I intend to enjoy it.";
                default:
                    return "A new chapter of life has begun.";
            }
        }

        private static string StageWords(LifeStage stage)
        {
            switch (stage)
            {
                case LifeStage.Adult:
                    return "a grown adult";
                case LifeStage.MiddleAged:
                    return "middle-aged";
                case LifeStage.Elderly:
                    return "elderly";
                default:
                    return "a young adult";
            }
        }
    }
}
