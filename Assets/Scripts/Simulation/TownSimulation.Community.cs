using System.Linq;
using TinyCity.Simulation.Activities;
using TinyCity.Simulation.History;
using TinyCity.Simulation.Memory;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Time;

namespace TinyCity.Simulation
{
    /// <summary>Community events on the village calendar: who came, what they remember, and the town's record of it.</summary>
    public sealed partial class TownSimulation
    {
        public const int EventMemoryImportance = 5;

        /// <summary>Whether the calendar's community events take place.</summary>
        public bool CommunityEventsEnabled
        {
            get => Planner.CommunityEventsEnabled;
            set => Planner.CommunityEventsEnabled = value;
        }

        /// <summary>Each resident remembers each event they go to once, with whoever was already there.</summary>
        private void ArriveAtEvent(Resident r)
        {
            World.TryGetObject(r.TargetObjectId, out var place);
            var evt = Planner.EventAt(place, Time);
            if (evt == null)
            {
                return;
            }

            var tag = evt.OccurrenceId(Time.Day);
            if (Memories.HasMemoryWithTag(r.Id, tag, GameTime.FromDayAndTime(Time.Day, 0, 0)))
            {
                return;
            }

            var others = residents
                .Where(o => o != r && o.IsAlive && o.State == ResidentState.Performing && o.ActivityId == ActivityCatalog.AttendEvent.Id && o.TargetObjectId == place.Id)
                .OrderBy(o => o.Id, System.StringComparer.Ordinal)
                .ToList();
            var company = others.Count == 0 ? "I was one of the first there."
                : others.Count <= 3 ? $"{string.Join(", ", others.Select(o => o.FirstName))} {(others.Count == 1 ? "was" : "were")} there too."
                : $"{others.Count} neighbours were already there.";
            Memories.Add(r.Id, Time, MemoryType.Event, EventMemoryImportance, $"I went to the {evt.Name.ToLowerInvariant()} at the {place.Name.ToLowerInvariant()}. {company}",
                others.Select(o => o.Id).Take(6), new[] { tag, evt.Id, "community", "event", "social" });
        }

        /// <summary>When an event ends, the town history notes who came.</summary>
        private void StepCommunityEvents()
        {
            if (!CommunityEventsEnabled)
            {
                return;
            }

            foreach (var evt in World.Calendar.OnDay(Time.Day).Where(e => e.EndMinute == Time.MinuteOfDay))
            {
                var tag = evt.OccurrenceId(Time.Day);
                var came = residents.Where(r => Memories.HasMemoryWithTag(r.Id, tag, GameTime.FromDayAndTime(Time.Day, 0, 0))).ToList();
                World.TryGetObject(evt.PlaceObjectId, out var place);
                var summary = came.Count == 0
                    ? $"Nobody came to the {evt.Name.ToLowerInvariant()} at the {place?.Name.ToLowerInvariant()}."
                    : $"The {evt.Name.ToLowerInvariant()} at the {place?.Name.ToLowerInvariant()}: {string.Join(", ", came.Select(r => r.FirstName))} came.";
                History.Add(Time, TownEventKind.Gathering, summary, came.Select(r => r.Id));
            }
        }
    }
}
