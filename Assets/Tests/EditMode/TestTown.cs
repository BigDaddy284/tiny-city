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
            CopyFinances(template, resident);
            return new TownSimulation(world, new[] { resident }, time);
        }

        /// <summary>
        /// The starter residents in a chosen situation. <paramref name="place"/> can move a resident (null keeps them in
        /// bed), <paramref name="adjust"/> sets their needs, and <paramref name="include"/> leaves residents out.
        /// </summary>
        public static TownSimulation WithResidents(GameTime time, System.Func<Resident, Location?> place = null, System.Action<Resident, Needs> adjust = null, System.Func<Resident, bool> include = null, double needsLevel = 90)
        {
            var world = StarterTown.CreateWorld();
            var residents = new System.Collections.Generic.List<Resident>();
            foreach (var template in StarterTown.CreateResidents(world))
            {
                if (include != null && !include(template))
                {
                    continue;
                }

                var needs = new Needs(needsLevel);
                adjust?.Invoke(template, needs);
                var resident = new Resident(
                    template.Id,
                    template.FirstName,
                    template.LastName,
                    template.AgeYears,
                    template.BirthdayDayOfYear,
                    template.ColourHex,
                    template.Personality,
                    template.HomeMapId,
                    template.BedObjectId,
                    template.Job,
                    place?.Invoke(template) ?? template.Location,
                    needs);
                CopyFinances(template, resident);
                residents.Add(resident);
            }

            return new TownSimulation(world, residents, time);
        }

        /// <summary>Mabel and Arthur standing next to each other on the village green, a little lonely.</summary>
        public static TownSimulation MabelAndArthurOnTheGreen(GameTime time, double social = 50)
        {
            return WithResidents(
                time,
                r => r.Id == StarterTown.ResidentId ? new Location(StarterTown.VillageMapId, 10, 9) : r.Id == StarterTown.ArthurId ? new Location(StarterTown.VillageMapId, 11, 9) : (Location?)null,
                (r, n) => n[NeedType.Social] = social,
                r => r.Id == StarterTown.ResidentId || r.Id == StarterTown.ArthurId);
        }

        /// <summary>Money, groceries, rent and income as the starter content gives them.</summary>
        public static void CopyFinances(Resident from, Resident to)
        {
            to.MoneyPence = from.MoneyPence;
            to.Groceries = from.Groceries;
            to.WeeklyRentPence = from.WeeklyRentPence;
            to.WeeklyIncomePence = from.WeeklyIncomePence;
            to.IncomeLabel = from.IncomeLabel;
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
