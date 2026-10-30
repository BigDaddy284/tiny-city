using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinyCity.Simulation;
using TinyCity.Simulation.Configuration;
using TinyCity.Simulation.Content;
using TinyCity.Simulation.History;
using TinyCity.Simulation.Memory;
using TinyCity.Simulation.Persistence;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Time;
using TinyCity.Simulation.World;

namespace TinyCity.Tests.EditMode
{
    /// <summary>People moving to the city from out of town: how many, where to, and that it all saves and replays exactly.</summary>
    public class NewcomerTests
    {
        private static TownSimulation NewCity()
        {
            var sim = StarterTown.CreateNewGame();
            sim.NewcomersEnabled = true;
            return sim;
        }

        [Test]
        public void Newcomers_AreOffUnlessTurnedOn()
        {
            var sim = StarterTown.CreateNewGame();
            sim.Advance(8 * GameTime.MinutesPerDay);

            Assert.That(sim.Residents.Count, Is.EqualTo(10));
            Assert.That(new SimulationSettings().newcomers, Is.True, "the game itself turns them on by default");
        }

        [Test]
        public void SomeoneMovesIn_WithinTheFirstFortnight_AndSleepsAtTheirNewHome()
        {
            var sim = NewCity();
            var awayAtNight = new List<string>();
            for (var minute = 0; minute < 14 * GameTime.MinutesPerDay; minute++)
            {
                sim.Tick();
                if (sim.Time.Hour == 3 && sim.Time.Minute == 0)
                {
                    // Every night at 03:00 each newcomer is at home, so they found their way there from the edge of the city.
                    awayAtNight.AddRange(sim.Residents.Where(r => r.IsNewcomer && r.IsAlive && r.Location.MapId != r.HomeMapId).Select(r => $"{r.FullName} on {sim.Time}"));
                }
            }

            var newcomers = sim.Residents.Where(r => r.IsNewcomer).ToList();
            Assert.That(newcomers.Count, Is.GreaterThan(0));
            Assert.That(newcomers.Count, Is.LessThanOrEqualTo(PopulationRules.MaxNewcomers));
            Assert.That(awayAtNight, Is.Empty);

            var first = newcomers[0];
            Assert.That(first.Id, Is.EqualTo("res-0011"));
            Assert.That(first.MovedInDay, Is.GreaterThanOrEqualTo(PopulationRules.FirstArrivalDay));
            Assert.That(StarterHomes().Contains(first.HomeMapId), Is.False, "a home nobody lived in");
            Assert.That(sim.World.GetMap(first.HomeMapId).IsExterior, Is.False);
            Assert.That(first.AgeYears, Is.InRange(20, 61));
            Assert.That(first.Hobbies.Count, Is.EqualTo(2));
            Assert.That(sim.History.Events.Count(e => e.Kind == TownEventKind.MovedIn), Is.EqualTo(newcomers.Count));
            Assert.That(Journal.IsNotable(TownEventKind.MovedIn), Is.True);
            Assert.That(sim.Memories.ForResident(first.Id).Any(m => m.Type == MemoryType.LifeHistory && m.Tags.Contains("moved-in")), Is.True);
        }

        [Test]
        public void ANewcomer_ArrivesOnFootAtTheEdgeOfTheCity()
        {
            var sim = NewCity();
            var newcomer = sim.MoveInNewcomer();

            Assert.That(newcomer, Is.Not.Null);
            Assert.That(newcomer.Location, Is.EqualTo(new Location(StarterTown.VillageMapId, new GridPos(41, 38))));
            Assert.That(newcomer.MovedInDay, Is.EqualTo(sim.Time.Day));
            Assert.That(newcomer.WeeklyRentPence, Is.GreaterThan(0));
            Assert.That(newcomer.WeeklyIncomePence, Is.GreaterThan(0));
            Assert.That(newcomer.Job, Is.Null);
        }

        [Test]
        public void NoMoreThanTenNewcomers_EverMoveIn()
        {
            var sim = NewCity();
            var arrived = new List<Resident>();
            for (var i = 0; i < 15; i++)
            {
                var r = sim.MoveInNewcomer();
                if (r != null)
                {
                    arrived.Add(r);
                }
            }

            Assert.That(arrived.Count, Is.EqualTo(PopulationRules.MaxNewcomers));
            Assert.That(arrived.Select(r => r.HomeMapId).Distinct().Count(), Is.EqualTo(arrived.Count), "one home each");
            Assert.That(arrived.Any(r => r.IsHomeless), Is.False, "22 free homes for 10 newcomers");
            Assert.That(arrived.Select(r => r.Id).Distinct().Count(), Is.EqualTo(arrived.Count));
            Assert.That(arrived.Select(r => r.FullName).Distinct().Count(), Is.EqualTo(arrived.Count), "no two share a name");

            // A newcomer dying doesn't make room for another: ten is the most who can ever come.
            sim.Die(arrived[0], "old age");
            Assert.That(sim.MoveInNewcomer(), Is.Null);
        }

        [Test]
        public void WithEveryHomeTaken_ANewcomerSleepsOnABench_UntilAHomeComesFree()
        {
            var sim = NewCity();
            var homes = sim.FreeHomes().ToList();
            Assert.That(homes.Count, Is.EqualTo(22), "32 homes, 10 lived in");
            var lodgers = new List<Resident>();
            foreach (var home in homes)
            {
                lodgers.Add(Lodger(sim, $"res-9{lodgers.Count:000}", home.Id));
            }

            var newcomer = sim.MoveInNewcomer();
            Assert.That(newcomer, Is.Not.Null, "they come anyway");
            Assert.That(newcomer.IsHomeless, Is.True);
            Assert.That(newcomer.WeeklyRentPence, Is.EqualTo(0));
            Assert.That(sim.History.Events.Last().Summary, Does.Contain("park bench"));

            sim.Die(lodgers[3], "old age");
            sim.HouseTheHomeless();
            Assert.That(newcomer.IsHomeless, Is.False, "a home came free");
            Assert.That(newcomer.HomeMapId, Is.EqualTo(homes[3].Id));
            Assert.That(newcomer.WeeklyRentPence, Is.GreaterThan(0));
            Assert.That(sim.History.Events.Last().Kind, Is.EqualTo(TownEventKind.Housed));
        }

        [Test]
        public void SomeoneHomeless_SleepsOnABench_AndGetsBy()
        {
            var sim = NewCity();
            sim.NewcomersEnabled = false;
            var mabel = sim.GetResident(StarterTown.ResidentId);
            var lodgers = 0;
            foreach (var home in sim.FreeHomes().ToList())
            {
                Lodger(sim, $"res-9{lodgers++:000}", home.Id);
            }

            sim.MakeHomeless(mabel, "Test.");
            Lodger(sim, $"res-9{lodgers:000}", StarterTown.CottageMapId);
            Assert.That(sim.FreeHomes().Any(), Is.False);
            var benchNights = 0;
            for (var minute = 0; minute < 5 * GameTime.MinutesPerDay; minute++)
            {
                sim.Tick();
                if (sim.Time.Hour == 3 && sim.Time.Minute == 0)
                {
                    sim.World.TryGetObject(mabel.TargetObjectId ?? string.Empty, out var bench);
                    if (mabel.ActivityId == "sleep" && bench != null && bench.Type == ObjectType.Bench)
                    {
                        benchNights++;
                    }
                }

                if (minute > 120)
                {
                    Assert.That(mabel.Location.MapId == StarterTown.CottageMapId, Is.False, "she has left her old home and doesn't go back");
                }
            }

            Assert.That(benchNights, Is.EqualTo(5));
            Assert.That(mabel.IsAlive, Is.True);
            Assert.That(mabel.Needs[NeedType.Hunger], Is.GreaterThan(10));
            Assert.That(sim.Residents.Where(r => r.IsAlive && !r.IsHomeless).All(r => r.ActivityId != "leftovers"), Is.True, "leftovers are only for the homeless");

            var loaded = SaveSerializer.Load(SaveSerializer.Save(sim), StarterTown.CreateWorld());
            Assert.That(loaded.Success, Is.True, string.Join("; ", loaded.Errors));
            Assert.That(loaded.Warnings, Is.Empty);
            Assert.That(loaded.Simulation.GetResident(mabel.Id).IsHomeless, Is.True);
            Assert.That(SaveSerializer.Save(loaded.Simulation), Is.EqualTo(SaveSerializer.Save(sim)));
        }

        [Test]
        public void TheCity_NeverHoldsMoreThanFiftyLivingResidents()
        {
            var sim = NewCity();
            var lodgers = new List<Resident>();
            while (sim.LivingCount < PopulationRules.MaxLiving)
            {
                lodgers.Add(Lodger(sim, $"res-9{lodgers.Count:000}", StarterTown.VillageMapId));
            }

            Assert.That(sim.MoveInNewcomer(), Is.Null);

            sim.Die(lodgers[0], "old age");
            Assert.That(sim.MoveInNewcomer(), Is.Not.Null);
            Assert.That(sim.LivingCount, Is.EqualTo(PopulationRules.MaxLiving));
            Assert.That(sim.MoveInNewcomer(), Is.Null);
        }

        [Test]
        public void TheSameCity_GetsTheSameNewcomers()
        {
            string Arrivals()
            {
                var sim = NewCity();
                sim.Advance(21 * GameTime.MinutesPerDay);
                return string.Join("; ", sim.Residents.Where(r => r.IsNewcomer).Select(r => $"{r.Id} {r.FullName} {r.AgeYears} {r.HomeMapId} day {r.MovedInDay}"));
            }

            var first = Arrivals();
            Assert.That(first, Is.Not.Empty);
            Assert.That(Arrivals(), Is.EqualTo(first));
        }

        [Test]
        public void Newcomers_SaveAndLoad_AndTheCityCarriesOnExactly()
        {
            var sim = NewCity();
            var reference = NewCity();
            sim.Advance(9 * GameTime.MinutesPerDay);
            reference.Advance(9 * GameTime.MinutesPerDay);
            Assert.That(sim.NewcomerCount, Is.GreaterThan(0));

            var loaded = SaveSerializer.Load(SaveSerializer.Save(sim), StarterTown.CreateWorld());
            Assert.That(loaded.Success, Is.True, string.Join("; ", loaded.Errors));
            Assert.That(loaded.Warnings, Is.Empty);
            var again = loaded.Simulation;
            again.NewcomersEnabled = true;
            foreach (var r in sim.Residents.Where(r => r.IsNewcomer))
            {
                Assert.That(again.GetResident(r.Id).MovedInDay, Is.EqualTo(r.MovedInDay));
            }

            again.Advance(7 * GameTime.MinutesPerDay);
            reference.Advance(7 * GameTime.MinutesPerDay);
            Assert.That(SaveSerializer.Save(again), Is.EqualTo(SaveSerializer.Save(reference)));
        }

        /// <summary>Someone already living in a home (or, for the village map, nowhere in particular), to fill the city up.</summary>
        private static Resident Lodger(TownSimulation sim, string id, string homeMapId)
        {
            var bed = sim.World.Objects.FirstOrDefault(o => o.Location.MapId == homeMapId && o.Type == ObjectType.Bed);
            var where = bed?.Location ?? new Location(StarterTown.VillageMapId, new GridPos(41, 38));
            var r = new Resident(id, "Lodger", id, 40, 100, "#888888", null, homeMapId, bed?.Id, null, where);
            sim.AddResident(r);
            return r;
        }

        private static HashSet<string> StarterHomes() => new HashSet<string>(StarterTown.CreateResidents(StarterTown.CreateWorld()).Select(r => r.HomeMapId));
    }
}
