using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinyCity.Art;
using TinyCity.Simulation;
using TinyCity.Simulation.Activities;
using TinyCity.Simulation.AI;
using TinyCity.Simulation.Building;
using TinyCity.Simulation.Configuration;
using TinyCity.Simulation.Content;
using TinyCity.Simulation.Economy;
using TinyCity.Simulation.History;
using TinyCity.Simulation.Persistence;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.World;

namespace TinyCity.Tests.EditMode
{
    /// <summary>Tiny City 2's block building: plans the AI sketches and the game checks, plots on the open land, and blocks laid one go at a time.</summary>
    public class BuildingTests
    {
        private static readonly string[] Hut = { "WWWWWWWWWW", "W........W", "W.B......W", "W........W", "W........W", "W........W", "W........W", "WWWWDWWWWW" };

        private static TownSimulation Town()
        {
            var sim = TestTown.WithResident(TestTown.WeekendAfternoon);
            sim.AiRunsEverything = true;
            sim.FoodMustBeMade = true;
            sim.AiDecisionsEnabled = true;
            return sim;
        }

        private static FreeTimeOption Option(TownSimulation sim, ActivityDefinition activity) =>
            sim.Planner.FreeTimeOptions(sim.Residents[0], everything: true).FirstOrDefault(o => o.Activity == activity);

        private static void Do(TownSimulation sim, ActivityDefinition activity)
        {
            var r = sim.Residents[0];
            for (var i = 0; i < 5 && r.State != ResidentState.Deciding; i++)
            {
                sim.Tick();
            }

            var option = Option(sim, activity);
            Assert.That(option, Is.Not.Null, $"{activity.Id} should be an option");
            Assert.That(sim.ApplyDecision(r.Id, r.PendingDecisionId, option.ActionId, option.TargetId, "Building", "Right then"), Is.EqualTo(DecisionOutcome.Applied));
            for (var i = 0; i < 900 && r.State != ResidentState.Deciding; i++)
            {
                sim.Tick();
            }

            Assert.That(r.State, Is.EqualTo(ResidentState.Deciding), $"{activity.Id} should have finished");
        }

        /// <summary>Plans a hut on the nearest plot, as if the AI had answered.</summary>
        private static Project PlanHut(TownSimulation sim, string[] rows = null, int height = 4)
        {
            var r = sim.Residents[0];
            Do(sim, ActivityCatalog.PlanBuilding);
            Assert.That(r.Sketch, Is.Not.Null, "waiting on the AI's plan");
            Assert.That(sim.ApplySketch(r.Id, r.Sketch.Id, "Little hut", height, rows ?? Hut, out var error), Is.EqualTo(DecisionOutcome.Applied), error);
            return sim.Construction.Of(r.Id);
        }

        [Test]
        public void APlan_IsChecked()
        {
            Assert.That(BuildPlan.TryParse("hut", 4, Hut, out var plan, out var error), Is.True, error);
            Assert.That(plan.Rows.Count, Is.EqualTo(BuildingRules.GridSize));
            Assert.That(plan.Rows.All(row => row.Length == BuildingRules.GridSize), Is.True);
            Assert.That(plan.Cells.Count, Is.EqualTo(30 * 4), "the doorway clears a whole tile, so one more wall block goes");
            Assert.That(plan.Cells.Take(30).All(c => c.Level == 0), Is.True, "the bottom layer first");
            Assert.That(plan.Needs().ToList(), Is.EqualTo(new List<(MaterialKind, int)> { (MaterialKind.Wood, 30) }), "one wood makes four blocks");
            Assert.That(plan.BedTile, Is.EqualTo(((int, int)?)(1, 1)));
            Assert.That(plan.IsHome, Is.True);

            Assert.That(BuildPlan.TryParse("hut", 4, Hut.Select(row => row.ToLowerInvariant()).ToList(), out plan, out error), Is.True, "lower case is fine");
            Assert.That(plan.Cells.Count, Is.EqualTo(30 * 4));
            Assert.That(BuildPlan.TryParse("tall hut", 6, Hut, out plan, out error), Is.True, error);
            Assert.That(plan.Cells.Any(c => c.X == 5 && c.Y == 7 && c.Level == 0), Is.False, "the doorway clears the wall beside it");
            Assert.That(plan.Cells.Any(c => c.X == 5 && c.Y == 7 && c.Level == BuildingRules.DoorHeight), Is.True, "with a lintel above it");
            Assert.That(BuildPlan.TryParse("mixed", 2, new[] { "WSHITC" }, out plan, out error), Is.True, error);
            Assert.That(plan.IsHome, Is.False, "no bed");

            var box = new[] { "WWWWWW", "W....W", "W....W", "W....W", "W....W", "WWWWWW" };
            Assert.That(BuildPlan.TryParse("box", 2, box, out _, out error), Is.False, "a shut-in room");
            Assert.That(error, Does.Contain("doorway"));
            Assert.That(BuildPlan.TryParse("hut", 0, Hut, out _, out _), Is.False);
            Assert.That(BuildPlan.TryParse("hut", BuildingRules.MaxHeight + 1, Hut, out _, out _), Is.False);
            Assert.That(BuildPlan.TryParse("x", 1, new[] { "WWX" }, out _, out _), Is.False, "X isn't a block");
            Assert.That(BuildPlan.TryParse("x", 1, new[] { "WW" }, out _, out _), Is.False, "too few blocks");
            Assert.That(BuildPlan.TryParse("x", 1, new[] { new string('W', BuildingRules.GridSize + 1) }, out _, out _), Is.False, "too wide");
            Assert.That(BuildPlan.TryParse("x", 1, Enumerable.Repeat("W", BuildingRules.GridSize + 1).ToList(), out _, out _), Is.False, "too deep");
        }

        [Test]
        public void TheOpenLand_HasPlots_OnClearGround_ThroughTheGate()
        {
            var sim = Town();
            var world = sim.World;
            var plots = world.ObjectsOfType(ObjectType.BuildPlot).ToList();
            Assert.That(plots.Count, Is.EqualTo(6));
            var footprints = new HashSet<Location>();
            foreach (var plot in plots)
            {
                Assert.That(plot.Location.MapId, Is.EqualTo(StarterTown.OpenLandMapId));
                foreach (var tile in BuildingRules.Footprint(plot))
                {
                    Assert.That(world.GetMap(tile.MapId).GetTile(tile.Position), Is.EqualTo(TileType.Grass), $"{plot.Id} {tile}");
                    Assert.That(world.Objects.Any(o => o.Location == tile), Is.False, $"{plot.Id} {tile} is clear");
                    Assert.That(footprints.Add(tile), Is.True, "plots don't overlap");
                }

                Assert.That(sim.Pathfinder.Distance(sim.Residents[0].Location, plot.Location), Is.Not.EqualTo(Pathfinder.Unreachable), plot.Id);
            }

            Assert.That(footprints.Overlaps(plots.Select(p => p.Location)), Is.False, "markers stand outside the plots");
            Assert.That(TestTown.WithResident(TestTown.WeekendAfternoon).Planner.FreeTimeOptions(sim.Residents[0], everything: true)
                .Any(o => o.Activity != null && BuildingRules.IsBuilding(o.Activity)), Is.False, "v1 towns never build");
        }

        [Test]
        public void APlannedBuilding_IsLaidBlockByBlock_AndBlocksTheWay()
        {
            var sim = Town();
            var r = sim.Residents[0];
            var project = PlanHut(sim);
            Assert.That(project, Is.Not.Null);
            Assert.That(Option(sim, ActivityCatalog.PlanBuilding), Is.Null, "one building at a time");
            Assert.That(Option(sim, ActivityCatalog.Build), Is.Null, "no wood yet");

            sim.World.TryGetObject(project.PlotId, out var plot);
            var wall = BuildingRules.TileOf(plot, project.Plan.Cells[1]);
            var inside = BuildingRules.TileOf(plot, 4, 4);
            var corner = BuildingRules.Corner(plot);
            var westOfHut = new Location(corner.MapId, corner.Position.X - 1, corner.Position.Y + 2);
            var before = sim.Pathfinder.Distance(westOfHut, inside);
            Assert.That(before, Is.EqualTo(3), "straight in, before the walls");

            r.Belongings.Add(MaterialKind.Wood, 10);
            Do(sim, ActivityCatalog.Build);
            Assert.That(project.Placed, Is.EqualTo(BuildingRules.BlocksPerSession));
            Assert.That(r.Belongings.Count(MaterialKind.Wood), Is.EqualTo(2), "32 blocks: the 30 of the bottom layer took 8 wood, and the 8th wood's last two blocks start the next");
            Assert.That(sim.Construction.IsBlocked(wall), Is.True);
            Assert.That(sim.Pathfinder.Distance(westOfHut, inside), Is.GreaterThan(before), "they walk round to the doorway");

            var oldBed = r.BedObjectId;
            r.Belongings.Add(MaterialKind.Wood, 30);
            for (var i = 0; i < 6 && !project.IsFinished; i++)
            {
                Do(sim, ActivityCatalog.Build);
            }

            Assert.That(project.IsFinished, Is.True);
            Assert.That(r.Belongings.Count(MaterialKind.Wood), Is.EqualTo(10), "30 wood in all");
            Assert.That(Option(sim, ActivityCatalog.Build), Is.Null, "nothing left to lay");
            Assert.That(sim.History.Events.Any(e => e.Kind == TownEventKind.Built && e.ResidentIds.Contains(r.Id) && e.Summary.Contains("moved in")), Is.True);
            Assert.That(sim.Pathfinder.Distance(plot.Location, inside), Is.Not.EqualTo(Pathfinder.Unreachable), "the doorway lets them in");

            Assert.That(sim.World.TryGetObject(Construction.BedIdOf(plot.Id), out var bed), Is.True, "a finished home has its bed");
            Assert.That(bed.Location, Is.EqualTo(BuildingRules.TileOf(plot, 2, 2)));
            Assert.That(r.BedObjectId, Is.EqualTo(bed.Id), "and they live there");
            Assert.That(r.HomeMapId, Is.EqualTo(StarterTown.OpenLandMapId));
            Assert.That(r.WeeklyRentPence, Is.EqualTo(0), "no rent on a home they built");
            Assert.That(oldBed, Is.Not.EqualTo(bed.Id));
            Assert.That(sim.Planner.IsAvailableTo(r, bed), Is.True);

            var pieces = BlockArt.Pieces(sim.World, sim.Construction, sim.World.GetMap(StarterTown.OpenLandMapId), Palette.For(TinyCity.Simulation.Time.Season.Spring));
            var tiles = project.Plan.Cells.Select(c => (c.X / BuildingRules.CellsPerTile, c.Y / BuildingRules.CellsPerTile)).Distinct().Count();
            Assert.That(pieces.Count, Is.EqualTo(tiles + 1), "one picture per tile of blocks, and the bed");
            Assert.That(MapArt.Build(sim.World, sim.World.GetMap(StarterTown.OpenLandMapId), Palette.For(TinyCity.Simulation.Time.Season.Spring)).Pieces.Any(p => p.Name == bed.Name), Is.False, "the bed is drawn with the blocks, not the map");
        }

        [Test]
        public void SomeoneElsesBuiltBed_IsNotTheirs()
        {
            var sim = Town();
            var r = sim.Residents[0];
            var project = PlanHut(sim, height: 1);
            r.Belongings.Add(MaterialKind.Wood, 8);
            Do(sim, ActivityCatalog.Build);
            Assert.That(project.IsFinished, Is.True);
            sim.World.TryGetObject(Construction.BedIdOf(project.PlotId), out var bed);

            var other = TestTown.WithResident(TestTown.WeekendAfternoon);
            Assert.That(other.Planner.IsAvailableTo(other.Residents[0], bed), Is.False, "a bed in someone's built home is theirs alone");
        }

        [Test]
        public void ABadPlan_IsRefused_AndThePlotStaysFree()
        {
            var sim = Town();
            var r = sim.Residents[0];
            Do(sim, ActivityCatalog.PlanBuilding);
            var plotId = r.Sketch.PlotId;
            Assert.That(sim.ApplySketch(r.Id, r.Sketch.Id, "box", 2, new[] { "WWWWWW", "W....W", "W....W", "W....W", "W....W", "WWWWWW" }, out var error), Is.EqualTo(DecisionOutcome.InvalidText));
            Assert.That(error, Is.Not.Null);
            Assert.That(r.Sketch, Is.Not.Null, "still waiting for a better plan");
            Assert.That(sim.Construction.At(plotId), Is.Null);
            Assert.That(sim.ApplySketch(r.Id, r.Sketch.Id + 1, "hut", 1, Hut, out _), Is.EqualTo(DecisionOutcome.Stale));

            sim.Advance(BuildingRules.SketchWaitMinutes + 1);
            Assert.That(r.Sketch, Is.Null, "given up on");
        }

        [Test]
        public void Buildings_SurviveASave_AndStillBlockTheWay()
        {
            var sim = Town();
            var r = sim.Residents[0];
            var project = PlanHut(sim, height: 1);
            r.Belongings.Add(MaterialKind.Wood, 5);
            Do(sim, ActivityCatalog.Build);
            Assert.That(project.Placed, Is.EqualTo(20), "five wood make twenty blocks");

            var result = SaveSerializer.Load(SaveSerializer.Save(sim), sim.World);
            Assert.That(result.Success, Is.True);
            var loaded = result.Simulation;
            var lp = loaded.Construction.At(project.PlotId);
            Assert.That(lp, Is.Not.Null);
            Assert.That((lp.OwnerId, lp.Plan.Name, lp.Plan.Height, lp.Placed), Is.EqualTo((r.Id, "Little hut", 1, 20)));
            Assert.That(lp.Plan.Rows, Is.EqualTo(project.Plan.Rows));
            sim.World.TryGetObject(project.PlotId, out var plot);
            foreach (var cell in project.PlacedCells)
            {
                Assert.That(loaded.Construction.IsBlocked(BuildingRules.TileOf(plot, cell)), Is.True);
            }

            loaded.FoodMustBeMade = true;
            var prompt = DecisionPrompt.UserText(loaded, loaded.Residents[0], loaded.Planner.FreeTimeOptions(loaded.Residents[0], everything: true), new TinyCity.Simulation.Memory.MemoryRecord[0]);
            Assert.That(prompt, Does.Contain("Your building: a Little hut on the open land, 20 of 30 blocks laid; it still needs 3 wood. It has a bed, so it will be your home once finished."));

            r.Belongings.Add(MaterialKind.Wood, 3);
            Do(sim, ActivityCatalog.Build);
            Assert.That(project.IsFinished, Is.True);
            var bedId = Construction.BedIdOf(project.PlotId);
            Assert.That(r.BedObjectId, Is.EqualTo(bedId));
            result = SaveSerializer.Load(SaveSerializer.Save(sim), sim.World);
            Assert.That(result.Success, Is.True);
            Assert.That(result.Warnings, Is.Empty);
            var home = result.Simulation.GetResident(r.Id);
            Assert.That((home.HomeMapId, home.BedObjectId, home.WeeklyRentPence), Is.EqualTo((StarterTown.OpenLandMapId, bedId, 0L)));
            Assert.That(result.Simulation.World.TryGetObject(bedId, out var bed), Is.True, "the bed is back with the building");
            Assert.That(result.Simulation.Planner.IsAvailableTo(home, bed), Is.True);
        }

        [Test]
        public void AiService_AsksForAPlan_AndTheAnswerBecomesAProject()
        {
            var sim = Town();
            var r = sim.Residents[0];
            Do(sim, ActivityCatalog.PlanBuilding);
            Assert.That(SketchPrompt.UserText(sim, r, r.Sketch), Does.Contain("W = wood").And.Contain("D for a doorway"));

            var transport = FakeAiTransport.WorkingOllama(body => body.Contains("D for a doorway") ? "{\"name\":\"stone shed\",\"height\":2,\"rows\":[\"SSS\",\"S.S\",\"SDS\"]}" : null);
            var ai = new AiService(new OllamaSettings(), transport);
            ai.Attach(sim);
            for (var i = 0; i < 40 && sim.Construction.Of(r.Id) == null; i++)
            {
                ai.Update(i * 0.1);
            }

            var project = sim.Construction.Of(r.Id);
            Assert.That(project?.Plan.Name, Is.EqualTo("stone shed"));
            Assert.That(project.Plan.Cells.Count, Is.EqualTo(12), "the doorway clears its whole tile");
            Assert.That(r.Sketch, Is.Null);
        }
    }
}
