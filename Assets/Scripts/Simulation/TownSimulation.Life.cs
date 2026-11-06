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
    /// <summary>Health and death. All of it follows <see cref="HealthRules"/>; nothing here is up to the AI.</summary>
    public sealed partial class TownSimulation
    {
        /// <summary>One minute of health after needs have changed.</summary>
        private void StepHealth(Resident r, ActivityDefinition activity)
        {
            if (!HealthRules.ApplyMinute(r, LowestHealth))
            {
                return;
            }

            // Nobody dies: they are left as weak as can be, and know it.
            if (!r.Log.Any(e => e.Time.Day == Time.Day && e.Text.StartsWith("Collapsed")))
            {
                r.AddLog(Time, "Collapsed from neglect, too weak to go on like this");
                Memories.Add(r.Id, Time, MemoryType.Event, 8, "I collapsed, as weak as I have ever been. I must eat, drink and look after myself.", tags: new[] { "health" });
            }
        }

        /// <summary>Called as an activity finishes normally, before the resident is freed.</summary>
        private void FinishActivity(Resident r, ActivityDefinition activity)
        {
            FinishFood(r, activity);
            FinishCrafting(r, activity);
            FinishBuilding(r, activity);
            FinishOpenLand(r, activity);
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
            r.Reaction = null;
            r.Sketch = null;
            r.IsAlive = false;
            r.DiedAt = Time;
            r.CauseOfDeath = cause;
            r.AddLog(Time, $"Died of {cause}");
            History.Add(Time, TownEventKind.Death, $"{r.FullName} died of {cause}, aged {r.AgeYears}.", new[] { r.Id });
            if (LivingCount == 0)
            {
                History.Add(Time, TownEventKind.WorldEnded, $"Everyone has died: the last was {r.FullName}. {World.TownName} lasted {Legacy.Days(Time.Day - Content.FlatLand.StartTime.Day + 1)}. What they lived through is written down for the next world.", residents.Select(o => o.Id).ToArray());
            }

            foreach (var other in residents.Where(o => o.IsAlive && o != r))
            {
                var met = Relationships.TryGet(other.Id, r.Id, out var rel) && rel.HaveMet;
                var close = met && rel.Friendship >= SocialRules.GoodFriendsAt;
                var summary = close ? $"My dear friend {r.FullName} died of {cause}. I'll miss them terribly."
                    : met ? $"{r.FullName} died of {cause}. I knew them a little."
                    : $"{r.FullName}, who lived in the city, died of {cause}.";
                Memories.Add(other.Id, Time, MemoryType.Event, close ? 9 : met ? 7 : 5, summary, new[] { r.Id }, new[] { r.Id, "death", "loss" });
            }
        }
    }
}
