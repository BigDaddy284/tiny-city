using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using TinyCity.Simulation;
using TinyCity.Simulation.Community;
using TinyCity.Simulation.Content;
using TinyCity.Simulation.Economy;
using TinyCity.Simulation.Persistence;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.World;

namespace TinyCity.Tools.StressTest
{
    public static partial class Program
    {
        private const int Districts = 10;

        /// <summary>
        /// One city ten times the size of the starter town: ten copies of every map, object, door, business and
        /// event side by side (not joined up), and ten copies of the residents, 100 people in one simulation.
        /// District 1 keeps the starter ids; the others add "-d2" … "-d10".
        /// </summary>
        internal static TownSimulation CreateBigCity()
        {
            var starter = StarterTown.CreateWorld();
            var people = StarterTown.CreateResidents(starter);
            var maps = new List<TileMap>();
            var objects = new List<WorldObject>();
            var portals = new List<Portal>();
            var businesses = new List<Business>();
            var events = new List<CommunityEvent>();
            var residents = new List<Resident>();

            for (var d = 0; d < Districts; d++)
            {
                string Id(string id) => d == 0 || id == null ? id : $"{id}-d{d + 1}";
                string Name(string name) => d == 0 ? name : $"{name} ({d + 1})";
                Location Place(Location l) => new Location(Id(l.MapId), l.Position);

                foreach (var map in starter.Maps)
                {
                    var tiles = new TileType[map.Width * map.Height];
                    for (var y = 0; y < map.Height; y++)
                    {
                        for (var x = 0; x < map.Width; x++)
                        {
                            tiles[(y * map.Width) + x] = map.GetTile(new GridPos(x, y));
                        }
                    }

                    maps.Add(new TileMap(Id(map.Id), Name(map.Name), map.IsExterior, map.Width, map.Height, tiles));
                }

                objects.AddRange(starter.Objects.Select(o => new WorldObject(Id(o.Id), o.Name, o.Type, Place(o.Location))));
                portals.AddRange(starter.Portals.Select(p => new Portal(Id(p.Id), Place(p.From), Place(p.To))));
                businesses.AddRange(starter.Businesses.Select(b => new Business(Id(b.Id), Name(b.Name), Id(b.MapId), b.OpenMinuteOfDay, b.CloseMinuteOfDay, b.OpenDays, b.Items)));
                events.AddRange(starter.Calendar.Events.Select(e => new CommunityEvent(Id(e.Id), e.Name, Id(e.PlaceObjectId), e.StartMinute, e.EndMinute, e.Days, e.YearDay, e.Turnout)));

                for (var i = 0; i < people.Count; i++)
                {
                    var p = people[i];
                    var job = p.Job == null ? null : new Job(p.Job.Title, Id(p.Job.WorkObjectId), p.Job.StartMinuteOfDay, p.Job.EndMinuteOfDay, p.Job.WorkDays, p.Job.HourlyWagePence);
                    var r = new Resident(
                        $"res-{(d * 100) + i + 1:0000}",
                        p.FirstName,
                        d == 0 ? p.LastName : $"{p.LastName} {d + 1}",
                        p.AgeYears,
                        p.BirthdayDayOfYear,
                        p.ColourHex,
                        p.Personality,
                        Id(p.HomeMapId),
                        Id(p.BedObjectId),
                        job,
                        Place(p.Location),
                        new Needs(90));
                    r.MoneyPence = p.MoneyPence;
                    r.Groceries = p.Groceries;
                    r.WeeklyRentPence = p.WeeklyRentPence;
                    r.WeeklyIncomePence = p.WeeklyIncomePence;
                    r.IncomeLabel = p.IncomeLabel;
                    r.SetHobbies(p.Hobbies);
                    r.Health = p.Health;
                    r.OpenToRomance = p.OpenToRomance;
                    residents.Add(r);
                }
            }

            var world = new TownWorld("town-big-city", "Big Tiny City", maps, objects, portals, businesses, new CommunityCalendar(events));
            return new TownSimulation(world, residents, StarterTown.StartTime);
        }

        private static bool BigCity(int section, int days)
        {
            var probe = CreateBigCity();
            var about = $"The owner's test: the starter town copied ten times side by side into one city ({probe.World.Maps.Count()} maps, {probe.World.Objects.Count} objects, {probe.World.Businesses.Count} businesses), " +
                $"with {probe.Residents.Count} residents in one simulation and one AI queue, all served by one model. The districts are not joined up, so people only meet their own neighbours. " +
                "Copied residents start without the starter residents' background memories.";
            var ok = AiAtEachSpeed($"## {section}. A city ten times the size: 100 residents and one qwen2.5:0.5b", AiProfile.All.Last(), days, CreateBigCity, about);

            // The game ticks once per game minute; at 10x that is ten ticks a real second, all on the main thread.
            var sim = CreateBigCity();
            var ticks = new List<double>();
            for (var minute = 0; minute < 2 * 1440; minute++)
            {
                var t = Stopwatch.GetTimestamp();
                sim.Tick();
                ticks.Add(Ms(Stopwatch.GetTimestamp() - t));
            }

            ticks.Sort();
            var s = Stopwatch.StartNew();
            var json = SaveSerializer.Save(sim);
            var save = s.Elapsed.TotalMilliseconds;
            s.Restart();
            var loaded = SaveSerializer.Load(json, CreateBigCity().World);
            var load = s.Elapsed.TotalMilliseconds;
            Line($"Without the AI, two game days of the 100-resident city: ticks take {ticks.Average():0.00} ms on average, 99.9% under {ticks[(int)(ticks.Count * 0.999)]:0.0} ms, slowest {ticks[ticks.Count - 1]:0.0} ms. " +
                $"At 10x that is about {ticks.Average():0.0}% of one core. The save is {json.Length / 1024} KB; saving takes {save:0} ms and loading {load:0} ms.");
            Line();
            if (!loaded.Success)
            {
                Line("**The big city's save did not load.** " + string.Join("; ", loaded.Errors));
                ok = false;
            }

            return ok;
        }
    }
}
