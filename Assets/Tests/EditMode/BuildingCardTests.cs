using System.Linq;
using NUnit.Framework;
using TinyCity.Art;
using TinyCity.Simulation;
using TinyCity.Simulation.Content;
using TinyCity.Simulation.Time;
using TinyCity.Simulation.World;

namespace TinyCity.Tests.EditMode
{
    /// <summary>The card shown when the pointer is over a building, and finding which building that is.</summary>
    public sealed class BuildingCardTests
    {
        [Test]
        public void EveryBuilding_IsFoundJustAboveItsFrontDoor()
        {
            var sim = StarterTown.CreateNewGame();
            var map = sim.World.GetMap(StarterTown.VillageMapId);
            var art = MapArt.Build(sim.World, map, Palette.For(Season.Spring));
            var plans = art.Buildings.Select(b => b.plan).ToList();
            Assert.That(plans.Count, Is.GreaterThan(10));
            foreach (var plan in plans)
            {
                // Half a tile up from the front door's centre is the wall over the door. (A tower's side doors can be
                // hidden behind the other tower, which then rightly wins.)
                var door = plan.Door;
                var found = art.BuildingAt((door.X - door.Y) * IsoPainter.HalfTileWidth, ((door.X + door.Y) * IsoPainter.HalfTileHeight) - 24);
                Assert.That(found, Is.SameAs(plan), $"over the door at {door.X},{door.Y} of {plan.InteriorMapId}, found {found?.InteriorMapId}");
            }
        }

        [Test]
        public void OpenGround_IsNoBuilding()
        {
            var sim = StarterTown.CreateNewGame();
            var map = sim.World.GetMap(StarterTown.VillageMapId);
            var art = MapArt.Build(sim.World, map, Palette.For(Season.Spring));
            var fountain = sim.World.Objects.First(o => o.Id == StarterTown.FountainId).Location.Position;
            Assert.That(art.BuildingAt((fountain.X - fountain.Y) * IsoPainter.HalfTileWidth, (fountain.X + fountain.Y) * IsoPainter.HalfTileHeight), Is.Null);
            Assert.That(art.BuildingAt(-100000, -100000), Is.Null);
        }

        [Test]
        public void Home_SaysWhoLivesThereAndWhoIsInside()
        {
            var sim = StarterTown.CreateNewGame();
            var r = sim.Residents.First(x => x.IsAlive && x.HomeMapId != null && x.Location.MapId == x.HomeMapId);
            var info = PlaceInfo.Of(sim, new[] { r.HomeMapId });
            Assert.That(info.Title, Is.EqualTo(sim.World.GetMap(r.HomeMapId).Name));
            Assert.That(info.Lines[0], Does.StartWith("Home of ").And.Contains(r.FullName));
            Assert.That(info.Lines[1], Does.StartWith("Inside: ").And.Contains(r.FullName));
            Assert.That(info.EnterMapId, Is.EqualTo(r.HomeMapId));
        }

        [Test]
        public void EmptyTower_IsNamedForTheTowerAndOpensItsFirstFlat()
        {
            var sim = StarterTown.CreateNewGame();
            var flats = sim.World.Maps.Where(m => m.Id.StartsWith(StarterTown.FlatMapPrefix + "b")).Select(m => m.Id).Reverse().ToList();
            Assert.That(flats.Count, Is.EqualTo(8));
            var info = PlaceInfo.Of(sim, flats);
            Assert.That(info.Title, Is.EqualTo("Aurora Tower"));
            Assert.That(info.Lines, Is.EqualTo(new[] { "Nobody lives here", "Nobody inside" }));
            Assert.That(sim.World.GetMap(info.EnterMapId).Name, Is.EqualTo("Aurora Tower, flat 1"));
        }

        [Test]
        public void Business_SaysWhetherItIsOpen()
        {
            var sim = StarterTown.CreateNewGame();
            var bar = sim.World.Businesses.First(b => b.AdultsOnly);
            var info = PlaceInfo.Of(sim, new[] { bar.MapId });
            Assert.That(info.Title, Is.EqualTo(bar.Name));
            Assert.That(info.Lines[0], Does.StartWith("Closed"));
            Assert.That(PlaceInfo.Of(sim, new[] { "no-such-map" }), Is.Null);
            Assert.That(PlaceInfo.Of(sim, new[] { StarterTown.VillageMapId }), Is.Null, "outdoors is not a building");
        }
    }
}
