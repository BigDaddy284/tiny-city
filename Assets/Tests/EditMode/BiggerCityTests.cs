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
    /// <summary>The city at twice the size: the apartment towers, new cottages and venues, and older saves still fitting.</summary>
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
        public void TheTownBeforeItGrew_IsWhereItWas_SoOlderSavesFit()
        {
            // Worked out with the code from before the city grew; older saves refer to these places by position.
            Assert.That(OldTownLayout(StarterTown.CreateWorld()), Is.EqualTo(7991904535593017144UL));
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

        /// <summary>
        /// The town before it grew: every tile of the village's top-left 43 x 27 (open ground, trees, water, houses or
        /// doors), every object there and in the older interiors, and every door. Saves refer to these places by position, so they must never move.
        /// </summary>
        private static ulong OldTownLayout(TownWorld world)
        {
            var hash = 14695981039346656037UL;
            void Add(string text)
            {
                foreach (var c in text)
                {
                    hash ^= c;
                    hash *= 1099511628211UL;
                }
            }

            world.TryGetMap(StarterTown.VillageMapId, out var village);
            for (var y = 0; y < 27; y++)
            {
                for (var x = 0; x < 43; x++)
                {
                    // Grass and paths are both open ground, so laying a new path over grass doesn't move anything.
                    var tile = village.GetTile(new GridPos(x, y));
                    Add((tile == TileType.Path ? TileType.Grass : tile).ToString());
                }
            }

            bool Old(Location l) => l.MapId == StarterTown.VillageMapId ? l.Position.X < 43 && l.Position.Y < 27 : OldInteriors.Contains(l.MapId);
            foreach (var o in world.Objects.Where(o => Old(o.Location)).OrderBy(o => o.Id, StringComparer.Ordinal))
            {
                Add($"{o.Id} {o.Type} {o.Location.MapId} {o.Location.Position.X},{o.Location.Position.Y};");
            }

            foreach (var p in world.Portals.Where(p => Old(p.From) && Old(p.To)).OrderBy(p => p.Id, StringComparer.Ordinal))
            {
                Add($"{p.Id} {p.From.MapId} {p.From.Position.X},{p.From.Position.Y} {p.To.MapId} {p.To.Position.X},{p.To.Position.Y};");
            }

            return hash;
        }

        private static readonly string[] OldInteriors =
        {
            "map-cottage-1", "map-cottage-2", "map-cottage-3", "map-cottage-4", "map-cottage-5", "map-cottage-6", "map-cottage-7",
            "map-cottage-8", "map-cottage-9", "map-cottage-10", "map-store", "map-cafe", "map-bakery", "map-clinic", "map-bar",
        };
    }
}
