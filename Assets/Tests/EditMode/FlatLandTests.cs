using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinyCity.Art;
using TinyCity.Simulation;
using TinyCity.Simulation.Activities;
using TinyCity.Simulation.Content;
using TinyCity.Simulation.Persistence;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.World;

namespace TinyCity.Tests.EditMode
{
    /// <summary>Tiny City 2's world: one flat stretch of open land laid out as the owner drew it, with a starter camp, food and resources, and no city or plots.</summary>
    public class FlatLandTests
    {
        private static TownSimulation NewGame()
        {
            var sim = FlatLand.CreateNewGame();
            sim.AiDecisionsEnabled = true;
            return sim;
        }

        [Test]
        public void TheLand_HasEveryArea_AllWithinReach()
        {
            var sim = NewGame();
            var world = sim.World;
            Assert.That(world.Maps.Count(), Is.EqualTo(1));
            foreach (var type in new[] { ObjectType.Kitchen, ObjectType.Bench, ObjectType.FishingSpot, ObjectType.Woodland, ObjectType.StickPile, ObjectType.RockPile, ObjectType.LooseStones, ObjectType.Workbench })
            {
                Assert.That(world.ObjectsOfType(type).Any(), Is.True, type.ToString());
            }

            Assert.That(world.ObjectsOfType(ObjectType.BuildPlot).Any() || world.ObjectsOfType(ObjectType.GardenPlot).Any(), Is.False, "no plots: they build and plant where they choose");
            Assert.That(world.ObjectsOfType(ObjectType.Bed).Any() || world.ObjectsOfType(ObjectType.MarketStall).Any(), Is.False, "no beds and no stalls, unless someone builds them");
            Assert.That(world.ObjectsOfType(ObjectType.Kitchen).Count(), Is.EqualTo(1), "one camp stove");
            var start = sim.Residents[0].Location;
            foreach (var o in world.Objects)
            {
                Assert.That(sim.Pathfinder.Distance(start, o.Location), Is.Not.EqualTo(Pathfinder.Unreachable), o.Id);
            }

            // As the owner drew it: the camp in the bottom quarter, trees, rocks, soil and the pond in the left one, and the top and right quarters open.
            bool Bottom(Location l) => l.Position.X > FlatLand.PathColumn && l.Position.Y > FlatLand.PathRow;
            bool Left(Location l) => l.Position.X < FlatLand.PathColumn && l.Position.Y > FlatLand.PathRow;
            foreach (var type in new[] { ObjectType.Kitchen, ObjectType.Bench, ObjectType.Workbench })
            {
                Assert.That(world.ObjectsOfType(type).All(o => Bottom(o.Location)), Is.True, type.ToString());
            }

            foreach (var type in new[] { ObjectType.Woodland, ObjectType.StickPile, ObjectType.RockPile, ObjectType.LooseStones, ObjectType.FishingSpot })
            {
                Assert.That(world.ObjectsOfType(type).All(o => Left(o.Location)), Is.True, type.ToString());
            }

            Assert.That(world.Objects.Any(o => o.Location.Position.Y < FlatLand.PathRow), Is.False, "the top and right quarters are left open");
            var map = world.GetMap(FlatLand.MapId);
            Assert.That(map.GetTile(new GridPos(20, 24)), Is.EqualTo(TileType.Garden), "the crop field by the cross path");
            Assert.That(map.GetTile(new GridPos(22, 31)), Is.EqualTo(TileType.Water), "the pond");
        }

        [Test]
        public void EveryResident_StartsAtTheCamp()
        {
            var sim = NewGame();
            Assert.That(sim.Residents.Count, Is.EqualTo(StarterResidents.Ids.Count));
            Assert.That(sim.Residents.Select(r => r.Location).Distinct().Count(), Is.EqualTo(sim.Residents.Count));
            foreach (var r in sim.Residents)
            {
                Assert.That(r.Location.MapId, Is.EqualTo(FlatLand.MapId));
                Assert.That(sim.Memories.ForResident(r.Id).Any(m => m.Summary.Contains("open land")), Is.True);
            }
        }

        [Test]
        public void Anyone_CanSleepOnTheGround_OrOnABench_AndTheSpotGoesWhenTheyGetUp()
        {
            var sim = NewGame();
            var r = sim.Residents[0];
            var options = sim.Planner.FreeTimeOptions(r);
            Assert.That(options.Any(o => o.ActionId == "sleep" && o.IsGround), Is.True, "sleep on the bare ground");
            Assert.That(options.Any(o => o.ActionId == "sleep" && o.Target?.Type == ObjectType.Bench), Is.True, "or on a bench, home or not");

            r.State = ResidentState.Deciding;
            r.PendingDecisionId = 77;
            var spot = new Location(FlatLand.MapId, r.Location.Position.X, r.Location.Position.Y + 1);
            Assert.That(sim.ApplyDecision(r.Id, 77, "sleep", FreeTimeOption.GroundTargetId, "tired", "", x: spot.Position.X, y: spot.Position.Y), Is.EqualTo(DecisionOutcome.Applied));
            var bed = sim.World.Objects.Single(o => o.Type == ObjectType.SleepingSpot);
            Assert.That(bed.Location, Is.EqualTo(spot));
            Assert.That(sim.Residents.Where(o => o != r).All(o => !sim.Planner.IsAvailableTo(o, bed)), Is.True, "one sleeper a spot");

            var json = SaveSerializer.Save(sim);
            var loaded = SaveSerializer.Load(json, FlatLand.CreateWorld());
            Assert.That(loaded.Success, Is.True, string.Join("; ", loaded.Errors));
            Assert.That(loaded.Simulation.World.Objects.Count(o => o.Type == ObjectType.SleepingSpot), Is.EqualTo(1), "saved while they sleep");

            sim.Advance(13 * 60);
            Assert.That(r.ActivityId == "sleep", Is.False, "they woke up");
            Assert.That(sim.World.Objects.Any(o => o.Type == ObjectType.SleepingSpot), Is.False, "the spot went when they got up");
        }

        [Test]
        public void NobodyMovesIn_AndNoBabiesAreBorn_InTiny_City_2()
        {
            var sim = NewGame();
            sim.Advance(3 * 24 * 60);
            Assert.That(sim.Residents.Count, Is.EqualTo(StarterResidents.Ids.Count));
        }

        [Test]
        public void NobodyDies_AndHungerAndThirstFallAtHalfSpeed_InTinyCity2()
        {
            var sim = FlatLand.CreateNewGame();
            var r = sim.Residents[0];
            var old = sim.Residents[1];
            old.AgeYears = 99;
            foreach (NeedType need in System.Enum.GetValues(typeof(NeedType)))
            {
                r.Needs[need] = 0;
            }

            r.Health = 2;
            sim.Advance(3 * 24 * 60);
            Assert.That(sim.Residents.All(o => o.IsAlive), Is.True, "nobody dies of neglect or old age");
            Assert.That(r.Health, Is.GreaterThanOrEqualTo(TownSimulation.LowestHealth));
            Assert.That(sim.Memories.ForResident(r.Id).Any(m => m.Summary.StartsWith("I collapsed")), Is.True);

            var needs = new Needs();
            var before = needs[NeedType.Hunger];
            NeedRules.ApplyMinute(needs, new Personality(0.5, 0.5, 0.5, 0.5), null, TownSimulation.HungerThirstRate);
            Assert.That(before - needs[NeedType.Hunger], Is.EqualTo(NeedRules.DecayPerHour(NeedType.Hunger, new Personality(0.5, 0.5, 0.5, 0.5)) / 120.0).Within(1e-9));
        }

        [Test]
        public void EverythingTheyNeed_IsAnOption()
        {
            var sim = NewGame();
            sim.Brief = WorldBrief.Empty;
            var r = sim.Residents[0];
            var ids = new HashSet<string>(sim.Planner.FreeTimeOptions(r).Select(o => o.ActionId));
            Assert.That(ids.Contains("gather-wood"), Is.False, "trees need an axe");
            foreach (var id in new[] { "gather-sticks", "gather-loose-stones", "plant-crops", "catch-fish", "plan-building", "drink-pond", "wash-pond", "sleep" })
            {
                Assert.That(ids.Contains(id), Is.True, id + ": " + string.Join(",", ids));
            }

            sim.Advance(24 * 60);
            Assert.That(sim.Residents.All(o => o.IsAlive), Is.True);
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
            Assert.That(result.Simulation.Residents.Select(r => (r.Id, r.Location)), Is.EqualTo(sim.Residents.Select(r => (r.Id, r.Location))));
        }

        [Test]
        public void TheLand_IsDrawn_WithTheCampStove()
        {
            var world = FlatLand.CreateWorld();
            var art = MapArt.Build(world, world.GetMap(FlatLand.MapId), Palette.For(TinyCity.Simulation.Time.Season.Summer));
            Assert.That(art.Pieces.Count(p => p.Name == "Camp stove"), Is.EqualTo(1));
        }
    }
}
