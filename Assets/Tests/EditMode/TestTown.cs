using TinyTown.Simulation;
using TinyTown.Simulation.Content;
using TinyTown.Simulation.Residents;
using TinyTown.Simulation.Time;
using TinyTown.Simulation.World;

namespace TinyTown.Tests.EditMode
{
    /// <summary>Helpers for building the starter town with a resident in a chosen situation.</summary>
    internal static class TestTown
    {
        public static TownSimulation WithResident(GameTime time, Location? location = null, double needsLevel = 90, System.Action<Needs> adjust = null, Personality personality = null)
        {
            var world = StarterTown.CreateWorld();
            var template = StarterTown.CreateResident(world);
            var needs = new Needs(needsLevel);
            adjust?.Invoke(needs);
            var resident = new Resident(
                template.Id,
                template.FirstName,
                template.LastName,
                template.AgeYears,
                template.BirthdayDayOfYear,
                template.ColourHex,
                personality ?? template.Personality,
                template.HomeMapId,
                template.BedObjectId,
                template.Job,
                location ?? template.Location,
                needs);
            return new TownSimulation(world, new[] { resident }, time);
        }

        public static WorldObject Object(TownSimulation sim, string id)
        {
            sim.World.TryGetObject(id, out var obj);
            return obj;
        }

        /// <summary>Monday 10:00, a work hour.</summary>
        public static GameTime WorkHour => GameTime.FromDayAndTime(1, 10, 0);

        /// <summary>Saturday 14:00, free time.</summary>
        public static GameTime WeekendAfternoon => GameTime.FromDayAndTime(6, 14, 0);
    }
}
