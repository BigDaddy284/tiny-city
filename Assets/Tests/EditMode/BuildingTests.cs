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
        private static readonly string[] Hut = { "WWWW", "W..W", "WDWW" };

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
        private static Project PlanHut(TownSimulation sim, string[] rows = null, int height = 2)
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
            Assert.That(BuildPlan.TryParse("hut", 2, Hut, out var plan, out var error), Is.True, error);
            Assert.That(plan.Rows.Count, Is.EqualTo(BuildingRules.PlotSize));
            Assert.That(plan.Rows.All(row => row.Length == BuildingRules.PlotSize), Is.True);
            Assert.That(plan.Cells.Count, Is.EqualTo(9 * 2));
            Assert.That(plan.Cells.Take(9).All(c => c.Level == 0), Is.True, "the bottom layer first");
            Assert.That(plan.Needs().ToList(), Is.EqualTo(new List<(MaterialKind, int)> { (MaterialKind.Wood, 18) }));

            Assert.That(BuildPlan.TryParse("tower", 3, new[] { "sss", "s.s", "sds" }, out plan, out error), Is.True, "lower case is fine");
            Assert.That(plan.Cells.Count, Is.EqualTo(7 * 3));
            Assert.That(BuildPlan.TryParse("mixed", 1, new[] { "WSHITC" }, out plan, out error), Is.True, error);

            Assert.That(BuildPlan.TryParse("box", 2, new[] { "WWW", "W.W", "WWW" }, out _, out error), Is.False, "a shut-in room");
            Assert.That(error, Does.Contain("doorway"));
            Assert.That(BuildPlan.TryParse("hut", 0, Hut, out _, out _), Is.False);
            Assert.That(BuildPlan.TryParse("hut", BuildingRules.MaxHeight + 1, Hut, out _, out _), Is.False);
            Assert.That(BuildPlan.TryParse("x", 1, new[] { "WWX" }, out _, out _), Is.False, "X isn't a block");
            Assert.That(BuildPlan.TryParse("x", 1, new[] { "WW" }, out _, out _), Is.False, "too few blocks");
            Assert.That(BuildPlan.TryParse("x", 1, new[] { "WWWWWWW" }, out _, out _), Is.False, "too wide");
            Assert.That(BuildPlan.TryParse("x", 1, Enumerable.Repeat("W", 7).ToList(), out _, out _), Is.False, "too deep");
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
            var inside = BuildingRules.TileOf(plot, new BlockCell(1, 1, 0, MaterialKind.Wood));
            var corner = BuildingRules.Corner(plot);
            var westOfHut = new Location(corner.MapId, corner.Position.X - 1, corner.Position.Y + 1);
            var before = sim.Pathfinder.Distance(westOfHut, inside);
            Assert.That(before, Is.EqualTo(2), "straight in, before the walls");

            r.Belongings.Add(MaterialKind.Wood, 10);
            Do(sim, ActivityCatalog.Build);
            Assert.That(project.Placed, Is.EqualTo(BuildingRules.BlocksPerSession));
            Assert.That(r.Belongings.Count(MaterialKind.Wood), Is.EqualTo(10 - BuildingRules.BlocksPerSession));
            Assert.That(sim.Construction.IsBlocked(wall), Is.True);
            Assert.That(sim.Pathfinder.Distance(westOfHut, inside), Is.GreaterThan(before), "they walk round to the doorway");

            r.Belongings.Add(MaterialKind.Wood, 30);
            for (var i = 0; i < 5 && !project.IsFinished; i++)
            {
                Do(sim, ActivityCatalog.Build);
            }

            Assert.That(project.IsFinished, Is.True);
            Assert.That(Option(sim, ActivityCatalog.Build), Is.Null, "nothing left to lay");
            Assert.That(sim.History.Events.Any(e => e.Kind == TownEventKind.Built && e.ResidentIds.Contains(r.Id)), Is.True);
            Assert.That(sim.Pathfinder.Distance(plot.Location, inside), Is.Not.EqualTo(Pathfinder.Unreachable), "the doorway lets them in");

            var pieces = BlockArt.Pieces(sim.World, sim.Construction, sim.World.GetMap(StarterTown.OpenLandMapId), Palette.For(TinyCity.Simulation.Time.Season.Spring));
            Assert.That(pieces.Count, Is.EqualTo(9), "one picture per column");
        }

        [Test]
        public void ABadPlan_IsRefused_AndThePlotStaysFree()
        {
            var sim = Town();
            var r = sim.Residents[0];
            Do(sim, ActivityCatalog.PlanBuilding);
            var plotId = r.Sketch.PlotId;
            Assert.That(sim.ApplySketch(r.Id, r.Sketch.Id, "box", 2, new[] { "WWW", "W.W", "WWW" }, out var error), Is.EqualTo(DecisionOutcome.InvalidText));
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
            Assert.That(project.Placed, Is.EqualTo(5));

            var result = SaveSerializer.Load(SaveSerializer.Save(sim), sim.World);
            Assert.That(result.Success, Is.True);
            var loaded = result.Simulation;
            var lp = loaded.Construction.At(project.PlotId);
            Assert.That(lp, Is.Not.Null);
            Assert.That((lp.OwnerId, lp.Plan.Name, lp.Plan.Height, lp.Placed), Is.EqualTo((r.Id, "Little hut", 1, 5)));
            Assert.That(lp.Plan.Rows, Is.EqualTo(project.Plan.Rows));
            sim.World.TryGetObject(project.PlotId, out var plot);
            foreach (var cell in project.PlacedCells)
            {
                Assert.That(loaded.Construction.IsBlocked(BuildingRules.TileOf(plot, cell)), Is.True);
            }

            loaded.FoodMustBeMade = true;
            var prompt = DecisionPrompt.UserText(loaded, loaded.Residents[0], loaded.Planner.FreeTimeOptions(loaded.Residents[0], everything: true), new TinyCity.Simulation.Memory.MemoryRecord[0]);
            Assert.That(prompt, Does.Contain("Your building: a Little hut on the open land, 5 of 9 blocks laid; it still needs 4 wood."));
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
            Assert.That(project.Plan.Cells.Count, Is.EqualTo(14));
            Assert.That(r.Sketch, Is.Null);
        }
    }
}
