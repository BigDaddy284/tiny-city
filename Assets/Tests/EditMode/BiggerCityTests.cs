using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinyCity.Simulation;
using TinyCity.Simulation.Activities;
using TinyCity.Simulation.Content;
using TinyCity.Simulation.Persistence;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Time;
using TinyCity.Simulation.World;

namespace TinyCity.Tests.EditMode
{
    /// <summary>The city at twice the size: the apartment towers, new cottages and venues.</summary>
    public class BiggerCityTests
    {
        private static readonly ActivityDefinition[] Venues =
        {
            ActivityCatalog.PlayPool, ActivityCatalog.PlaySnooker, ActivityCatalog.Bowling, ActivityCatalog.PlayArcade, ActivityCatalog.GymWorkout, ActivityCatalog.BrowseMarket,
        };

        [Test]
        public void Village_HasTwiceTheRoom()
        {
            var world = StarterTown.CreateWorld();
            world.TryGetMap(StarterTown.VillageMapId, out var village);

            Assert.That(village.Width * village.Height, Is.GreaterThanOrEqualTo(2 * 44 * 28));
            Assert.That(village.Width, Is.EqualTo(64));
            Assert.That(village.Height, Is.EqualTo(40));
        }

        [Test]
        public void ThereAreThirtyTwoHomes_SixteenOfThemFlatsInTwoTowers()
        {
            var world = StarterTown.CreateWorld();
            var homes = world.Maps.Where(m => world.Objects.Any(o => o.Type == ObjectType.Bed && o.Location.MapId == m.Id)).ToList();
            var flats = homes.Where(m => m.Id.StartsWith(StarterTown.FlatMapPrefix, StringComparison.Ordinal)).ToList();

            Assert.That(homes.Count, Is.EqualTo(32));
            Assert.That(flats.Count, Is.EqualTo(16));
            foreach (var flat in flats)
            {
                var door = world.Portals.Single(p => p.To.MapId == flat.Id && p.From.MapId == StarterTown.VillageMapId);
                world.TryGetMap(StarterTown.VillageMapId, out var village);
                Assert.That(village.GetTile(door.From.Position), Is.EqualTo(TileType.Door), flat.Id);
                Assert.That(door.From.Position.Y, Is.LessThan(8), $"{flat.Id} is in a tower along the back of the city");
            }
        }

        [Test]
        public void EmptyHomes_ArePrivate()
        {
            var sim = StarterTown.CreateNewGame();
            var mabel = sim.Residents[0];
            var empty = sim.World.Objects.Where(o => o.Type == ObjectType.Bed && (o.Location.MapId.StartsWith(StarterTown.FlatMapPrefix, StringComparison.Ordinal) || o.Location.MapId == "map-cottage-11")).ToList();

            Assert.That(empty.Count, Is.EqualTo(17));
            Assert.That(empty.All(bed => sim.Residents.All(r => r.HomeMapId != bed.Location.MapId)), Is.True, "nobody lives in the new homes yet");
            Assert.That(empty.Any(bed => sim.Planner.IsAvailableTo(mabel, bed)), Is.False, "and nobody can use them");
        }

        [Test]
        public void EveryVenue_CanBeUsed_WhileOpen_AndNotAfterHours()
        {
            var sim = StarterTown.CreateNewGame();
            var r = sim.Residents[0];
            var afternoon = GameTime.FromDayAndTime(6, 14, 0);
            var lateNight = GameTime.FromDayAndTime(6, 3, 0);
            foreach (var activity in Venues)
            {
                var places = sim.World.Objects.Where(o => activity.UsableObjects.Contains(o.Type)).ToList();
                Assert.That(places, Is.Not.Empty, activity.Id);
                Assert.That(places.Any(o => sim.Planner.CanDo(r, activity, o, afternoon)), Is.True, $"{activity.Id} in the afternoon");
                if (activity != ActivityCatalog.BrowseMarket)
                {
                    Assert.That(places.Any(o => sim.Planner.CanDo(r, activity, o, lateNight)), Is.False, $"{activity.Id} at 3 in the morning");
                }
            }
        }

        [Test]
        public void OverTwoWeeks_ResidentsGoToEveryVenue()
        {
            var sim = StarterTown.CreateNewGame();
            var seen = new HashSet<string>();
            for (var minute = 0; minute < 14 * GameTime.MinutesPerDay; minute++)
            {
                sim.Tick();
                foreach (var r in sim.Residents.Where(r => r.State == ResidentState.Performing))
                {
                    seen.Add(r.ActivityId);
                }
            }

            foreach (var activity in Venues)
            {
                Assert.That(seen.Contains(activity.Id), Is.True, activity.Id);
            }
        }

        [Test]
        public void ABiggerCity_StillSavesAndLoadsExactly()
        {
            var sim = StarterTown.CreateNewGame();
            sim.Advance(2 * GameTime.MinutesPerDay);
            var saved = SaveSerializer.Save(sim);
            var loaded = SaveSerializer.Load(saved, StarterTown.CreateWorld());

            Assert.That(loaded.Success, Is.True, string.Join("; ", loaded.Errors));
            Assert.That(SaveSerializer.Save(loaded.Simulation), Is.EqualTo(saved));
        }

        private static readonly string[] OldInteriors =
        {
            "map-cottage-1", "map-cottage-2", "map-cottage-3", "map-cottage-4", "map-cottage-5", "map-cottage-6", "map-cottage-7",
            "map-cottage-8", "map-cottage-9", "map-cottage-10", "map-store", "map-cafe", "map-bakery", "map-clinic", "map-bar",
        };
    }
}
