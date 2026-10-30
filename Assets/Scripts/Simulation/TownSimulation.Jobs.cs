using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation.Content;
using TinyCity.Simulation.History;
using TinyCity.Simulation.Memory;
using TinyCity.Simulation.Residents;

namespace TinyCity.Simulation
{
    /// <summary>
    /// Jobs waiting to be taken (<c>TownWorld.JobOpenings</c>, such as running the venues). Each morning
    /// at <see cref="PopulationRules.HousingMinute"/> anyone on jobseeker's allowance takes an opening nobody alive
    /// has, oldest resident id first, and their allowance stops. A job comes free again when its worker dies.
    /// </summary>
    public sealed partial class TownSimulation
    {
        /// <summary>Openings nobody alive has, in the order the town lists them.</summary>
        public IEnumerable<Residents.Job> Vacancies()
        {
            var taken = new HashSet<string>(residents.Where(r => r.IsAlive && r.Job != null).Select(r => r.Job.WorkObjectId));
            return World.JobOpenings.Where(j => !taken.Contains(j.WorkObjectId));
        }

        /// <summary>Whether this resident is out of work and looking: a living adult on jobseeker's allowance.</summary>
        public static bool IsJobseeker(Resident r) =>
            r.IsAlive && !r.IsMinor && r.Job == null && r.IncomeLabel == Newcomers.AllowanceLabel;

        /// <summary>Gives each jobseeker a vacancy, while there are any. Returns how many were hired.</summary>
        internal int FillVacancies()
        {
            var hired = 0;
            foreach (var r in residents.Where(IsJobseeker).OrderBy(r => r.Id, System.StringComparer.Ordinal).ToList())
            {
                var job = Vacancies().FirstOrDefault();
                if (job == null)
                {
                    break;
                }

                var place = World.TryGetObject(job.WorkObjectId, out var obj) && World.TryGetMap(obj.Location.MapId, out var map) ? map.Name : job.Title;
                r.Job = job;
                r.WeeklyIncomePence = 0;
                r.IncomeLabel = string.Empty;
                r.AddLog(Time, $"Started work as {job.Title} at {place}");
                Memories.Add(r.Id, Time, MemoryType.Event, 7, $"I got a job: {job.Title} at {place}, {job.DaysText()}. No more jobseeker's allowance.", tags: new[] { obj?.Location.MapId ?? job.WorkObjectId, "work", "money" });
                History.Add(Time, TownEventKind.NewJob, $"{r.FullName} started work as {job.Title} at {place}.", new[] { r.Id });
                hired++;
            }

            return hired;
        }
    }
}
