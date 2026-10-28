using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinyTown.Simulation.Content;
using TinyTown.Simulation.World;

namespace TinyTown.Tests.EditMode
{
    public class NavigationTests
    {
        private static readonly Dictionary<char, TileType> Legend = new Dictionary<char, TileType>
        {
            ['.'] = TileType.Grass,
            ['#'] = TileType.Wall,
            ['D'] = TileType.Door,
        };

        private static TownWorld TwoRooms()
        {
            var a = TileMap.FromRows("a", "A", true, new[] { "....", ".##.", "...D" }, Legend);
            var b = TileMap.FromRows("b", "B", false, new[] { "D..", "#.#", "..." }, Legend);
            var objects = new[]
            {
                new WorldObject("a-start", "Start", ObjectType.Bench, new Location("a", 0, 0)),
                new WorldObject("b-bed", "Bed", ObjectType.Bed, new Location("b", 0, 2)),
            };
            var portals = new[]
            {
                new Portal("ab", new Location("a", 3, 2), new Location("b", 0, 0)),
                new Portal("ba", new Location("b", 0, 0), new Location("a", 3, 2)),
            };
            return new TownWorld("test", "Test", new[] { a, b }, objects, portals);
        }

        [Test]
        public void Distance_SameMap_GoesAroundWalls()
        {
            var pf = new Pathfinder(TwoRooms());

            Assert.That(pf.Distance(new Location("a", 0, 1), new Location("a", 3, 1)), Is.EqualTo(5));
        }

        [Test]
        public void Distance_AcrossMaps_IncludesDoorStep()
        {
            var pf = new Pathfinder(TwoRooms());

            // (0,0)->(3,2) is 5 steps, 1 to go through the door, then (0,0)->(1,0)->(1,1)->(1,2)->(0,2) is 4 steps.
            Assert.That(pf.Distance(new Location("a", 0, 0), new Location("b", 0, 2)), Is.EqualTo(10));
        }

        [Test]
        public void Walls_And_OutOfBounds_AreUnreachable()
        {
            var pf = new Pathfinder(TwoRooms());

            Assert.That(pf.Distance(new Location("a", 0, 0), new Location("a", 1, 1)), Is.EqualTo(Pathfinder.Unreachable));
            Assert.That(pf.Distance(new Location("a", 0, 0), new Location("a", 9, 9)), Is.EqualTo(Pathfinder.Unreachable));
            Assert.That(pf.Distance(new Location("a", 0, 0), new Location("missing", 0, 0)), Is.EqualTo(Pathfinder.Unreachable));
        }

        [Test]
        public void FollowingNextSteps_ReachesTarget_OnlyThroughWalkableTilesAndDoors()
        {
            var world = TwoRooms();
            var pf = new Pathfinder(world);
            var here = new Location("a", 0, 0);
            var target = new Location("b", 0, 2);
            var steps = 0;

            while (here != target && steps < 50)
            {
                Assert.That(pf.TryGetNextStep(here, target, out var next), Is.True);
                Assert.That(world.IsWalkable(next), Is.True, $"Stepped onto a blocked tile {next}");
                if (next.MapId == here.MapId)
                {
                    var dx = System.Math.Abs(next.Position.X - here.Position.X);
                    var dy = System.Math.Abs(next.Position.Y - here.Position.Y);
                    Assert.That(dx + dy, Is.EqualTo(1), "Moved more than one tile at once");
                }
                else
                {
                    Assert.That(world.Portals.Any(p => p.From == here && p.To == next), Is.True, "Changed map without using a door");
                }

                here = next;
                steps++;
            }

            Assert.That(here, Is.EqualTo(target));
            Assert.That(steps, Is.EqualTo(10));
        }

        [Test]
        public void NextStep_AtTarget_ReturnsFalse()
        {
            var pf = new Pathfinder(TwoRooms());
            var spot = new Location("a", 0, 0);

            Assert.That(pf.TryGetNextStep(spot, spot, out _), Is.False);
        }

        [Test]
        public void StarterTown_EveryObjectIsReachableFromBed()
        {
            var world = StarterTown.CreateWorld();
            var pf = new Pathfinder(world);
            world.TryGetObject(StarterTown.BedId, out var bed);

            foreach (var obj in world.Objects)
            {
                Assert.That(pf.Distance(bed.Location, obj.Location), Is.Not.EqualTo(Pathfinder.Unreachable), obj.Id);
            }
        }

        [Test]
        public void Furniture_IsWalkedAround_AndOnlySteppedOnToUseIt()
        {
            // A cooker in the middle of a 3 x 3 room: crossing the room goes round it, not over it.
            var room = TileMap.FromRows("r", "R", false, new[] { "...", "...", "..." }, Legend);
            var world = new TownWorld("t", "T", new[] { room }, new[]
            {
                new WorldObject("cooker", "Cooker", ObjectType.Kitchen, new Location("r", 1, 1)),
                new WorldObject("spot", "Gathering spot", ObjectType.GatheringSpot, new Location("r", 1, 0)),
            }, new Portal[0]);
            var pf = new Pathfinder(world);

            Assert.That(world.IsBlocked(new Location("r", 1, 1)), Is.True);
            Assert.That(world.IsBlocked(new Location("r", 1, 0)), Is.False, "open ground can be crossed");
            Assert.That(pf.Distance(new Location("r", 1, 2), new Location("r", 1, 0)), Is.EqualTo(4), "round the cooker, not 2 steps over it");
            Assert.That(pf.Distance(new Location("r", 1, 2), new Location("r", 1, 1)), Is.EqualTo(1), "the cooker itself can be reached to use it");
            Assert.That(pf.Distance(new Location("r", 1, 1), new Location("r", 0, 0)), Is.EqualTo(2), "and stepped off again");

            var here = new Location("r", 1, 2);
            var walked = new List<Location>();
            while (pf.TryGetNextStep(here, new Location("r", 1, 0), out var next))
            {
                walked.Add(next);
                here = next;
            }

            Assert.That(walked.Contains(new Location("r", 1, 1)), Is.False);
            Assert.That(here, Is.EqualTo(new Location("r", 1, 0)));
        }

        [Test]
        public void StarterTown_FurnitureNeverBlocksTheWay()
        {
            // From every open tile, every object and every door can still be reached.
            var world = StarterTown.CreateWorld();
            var pf = new Pathfinder(world);
            foreach (var map in world.Maps)
            {
                for (var y = 0; y < map.Height; y++)
                {
                    for (var x = 0; x < map.Width; x++)
                    {
                        var from = new Location(map.Id, x, y);
                        if (!world.IsWalkable(from) || world.IsBlocked(from))
                        {
                            continue;
                        }

                        foreach (var target in world.Objects.Select(o => o.Location).Concat(world.Portals.Select(p => p.From)))
                        {
                            Assert.That(pf.Distance(from, target), Is.Not.EqualTo(Pathfinder.Unreachable), $"{from} to {target}");
                        }
                    }
                }
            }
        }

        [Test]
        public void Map_WithRaggedRows_IsRejected()
        {
            Assert.That(() => TileMap.FromRows("bad", "Bad", true, new[] { "...", ".." }, Legend), Throws.ArgumentException);
        }

        [Test]
        public void Object_OnBlockedTile_IsRejected()
        {
            var map = TileMap.FromRows("m", "M", true, new[] { ".#" }, Legend);

            Assert.That(
                () => new TownWorld("t", "T", new[] { map }, new[] { new WorldObject("o", "O", ObjectType.Bench, new Location("m", 1, 0)) }, new Portal[0]),
                Throws.ArgumentException);
        }
    }
}
