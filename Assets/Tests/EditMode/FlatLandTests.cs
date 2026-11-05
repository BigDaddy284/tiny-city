using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinyCity.Art;
using TinyCity.Simulation;
using TinyCity.Simulation.Activities;
using TinyCity.Simulation.Building;
using TinyCity.Simulation.Content;
using TinyCity.Simulation.Persistence;
using TinyCity.Simulation.World;

namespace TinyCity.Tests.EditMode
{
    /// <summary>Tiny City 2's world: one flat stretch of open land with a starter camp, food, resources and building plots, and no city.</summary>
    public class FlatLandTests
    {
        private static TownSimulation NewGame()
        {
            var sim = FlatLand.CreateNewGame();
            sim.AiRunsEverything = true;
            sim.FoodMustBeMade = true;
            sim.AiDecisionsEnabled = true;
            return sim;
        }

        [Test]
        public void TheLand_HasEveryArea_AllWithinReach()
        {
            var sim = NewGame();
            var world = sim.World;
            Assert.That(world.Maps.Count(), Is.EqualTo(1));
            Assert.That(world.Businesses, Is.Empty, "no shops: someone has to open a stall");
            Assert.That(world.JobOpenings, Is.Empty);
            foreach (var type in new[] { ObjectType.Bed, ObjectType.Kitchen, ObjectType.Bench, ObjectType.MarketStall, ObjectType.GardenPlot, ObjectType.FishingSpot, ObjectType.Woodland, ObjectType.RockPile, ObjectType.Workbench, ObjectType.BuildPlot })
            {
                Assert.That(world.ObjectsOfType(type).Any(), Is.True, type.ToString());
            }

            Assert.That(world.ObjectsOfType(ObjectType.BuildPlot).Count(), Is.EqualTo(12));
            Assert.That(world.ObjectsOfType(ObjectType.Bed).Count(), Is.EqualTo(FlatLand.CampBeds));
            var start = sim.Residents[0].Location;
            foreach (var o in world.Objects)
            {
                Assert.That(sim.Pathfinder.Distance(start, o.Location), Is.Not.EqualTo(Pathfinder.Unreachable), o.Id);
            }

            var footprints = new HashSet<Location>();
            foreach (var plot in world.ObjectsOfType(ObjectType.BuildPlot))
            {
                foreach (var tile in BuildingRules.Footprint(plot))
                {
                    Assert.That(world.GetMap(tile.MapId).GetTile(tile.Position), Is.EqualTo(TileType.Grass), $"{plot.Id} {tile}");
                    Assert.That(world.Objects.Any(o => o.Location == tile), Is.False, $"{plot.Id} {tile} is clear");
                    Assert.That(footprints.Add(tile), Is.True, "plots don't overlap");
                }
            }

            Assert.That(footprints.Overlaps(world.Objects.Select(o => o.Location)), Is.False);
        }

        [Test]
        public void EveryResident_StartsInACampBedOfTheirOwn_WithNoJobOrRent()
        {
            var sim = NewGame();
            Assert.That(sim.Residents.Count, Is.EqualTo(StarterTown.StarterResidentIds.Count));
            Assert.That(sim.Residents.Select(r => r.BedObjectId).Distinct().Count(), Is.EqualTo(sim.Residents.Count));
            foreach (var r in sim.Residents)
            {
                Assert.That(sim.World.TryGetObject(r.BedObjectId, out var bed), Is.True, r.Id);
                Assert.That(r.Location, Is.EqualTo(bed.Location));
                Assert.That(r.HomeMapId, Is.EqualTo(FlatLand.MapId));
                Assert.That(r.Job, Is.Null, r.Id);
                Assert.That((r.WeeklyRentPence, r.WeeklyIncomePence, r.OwnsBusinessId), Is.EqualTo((0L, 0L, (string)null)), r.Id);
                Assert.That(sim.Planner.IsAvailableTo(r, bed), Is.True);
                Assert.That(sim.Residents.Where(o => o != r).All(o => !sim.Planner.IsAvailableTo(o, bed)), Is.True, "nobody else sleeps in it");
                Assert.That(sim.Memories.ForResident(r.Id).Any(m => m.Summary.Contains("open land")), Is.True);
            }
        }

        [Test]
        public void EverythingTheyNeed_IsAnOption()
        {
            var sim = NewGame();
            var r = sim.Residents[0];
            var ids = new HashSet<string>(sim.Planner.FreeTimeOptions(r, everything: true).Select(o => o.ActionId));
            foreach (var id in new[] { "gather-wood", "plant-crops", "catch-fish", "plan-building" })
            {
                Assert.That(ids.Contains(id), Is.True, id + ": " + string.Join(",", ids));
            }

            sim.Advance(24 * 60);
            Assert.That(sim.Residents.All(o => o.IsAlive), Is.True);
        }

        [Test]
        public void ANewcomer_TakesAFreeCampBed()
        {
            var sim = NewGame();
            var newcomer = sim.MoveInNewcomer();
            Assert.That(newcomer, Is.Not.Null);
            Assert.That(newcomer.IsHomeless, Is.False);
            Assert.That(sim.World.TryGetObject(newcomer.BedObjectId, out var bed) && bed.Type == ObjectType.Bed, Is.True);
            Assert.That(sim.Residents.Count(o => o.BedObjectId == newcomer.BedObjectId), Is.EqualTo(1));
        }

        [Test]
        public void TheLand_SurvivesASave_AndCityAndOldSavesAreRefused()
        {
            var sim = NewGame();
            sim.Advance(90);
            var json = SaveSerializer.Save(sim);
            var result = SaveSerializer.Load(json, FlatLand.CreateWorld());
            Assert.That(result.Success, Is.True, string.Join("; ", result.Errors));
            Assert.That(result.Warnings, Is.Empty);
            Assert.That(result.Simulation.Residents.Select(r => (r.Id, r.BedObjectId, r.Location)), Is.EqualTo(sim.Residents.Select(r => (r.Id, r.BedObjectId, r.Location))));

            Assert.That(SaveSerializer.Load(SaveSerializer.Save(StarterTown.CreateNewGame()), FlatLand.CreateWorld()).Success, Is.False, "a city save is for a different town");
        }

        [Test]
        public void TheLand_IsDrawn_WithCampBedsAndEveryObject()
        {
            var world = FlatLand.CreateWorld();
            var art = MapArt.Build(world, world.GetMap(FlatLand.MapId), Palette.For(TinyCity.Simulation.Time.Season.Summer));
            Assert.That(art.Pieces.Count(p => p.Name == "Camp bed"), Is.EqualTo(FlatLand.CampBeds));
        }
    }
}
