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
    /// <summary>Tiny City 2's block building: plans the AI sketches and the game checks, built wherever residents choose, and blocks laid one go at a time.</summary>
    public class BuildingTests
    {
        private static readonly string[] Hut = { "WWWWWWWWWW", "W........W", "W.K......W", "W........W", "W........W", "W........W", "W........W", "WWWWDWWWWW" };

        /// <summary>Where the tests build: open ground in the land's right quarter.</summary>
        private const int SpotX = 31;
        private const int SpotY = 10;

        private static TownSimulation Town()
        {
            var sim = FlatLand.CreateNewGame();
            // Anyone may do anything here: these tests are about the work, not who may do it.
            sim.Brief = TinyCity.Simulation.Residents.WorldBrief.Empty;
            sim.AiDecisionsEnabled = true;
            return sim;
        }

        private static FreeTimeOption Option(TownSimulation sim, ActivityDefinition activity) =>
            sim.FreeTimeOptionsFor(sim.Residents[0]).FirstOrDefault(o => o.Activity == activity);

        private static void Do(TownSimulation sim, ActivityDefinition activity, int? x = null, int? y = null)
        {
            var r = sim.Residents[0];
            for (var i = 0; i < 5 && r.State != ResidentState.Deciding; i++)
            {
                sim.Tick();
            }

            var option = Option(sim, activity);
            Assert.That(option, Is.Not.Null, $"{activity.Id} should be an option");
            Assert.That(sim.ApplyDecision(r.Id, r.PendingDecisionId, option.ActionId, option.TargetId, "Building", null, x, y), Is.EqualTo(DecisionOutcome.Applied));
            for (var i = 0; i < 900 && r.State != ResidentState.Deciding; i++)
            {
                sim.Tick();
            }

            Assert.That(r.State, Is.EqualTo(ResidentState.Deciding), $"{activity.Id} should have finished");
        }

        private static Location Spot(int dx = 0, int dy = 0) => new Location(FlatLand.MapId, SpotX + dx, SpotY + dy);

        /// <summary>Plans a hut at the spot, as if the AI had answered.</summary>
        private static Project PlanHut(TownSimulation sim, string[] rows = null, int height = 4)
        {
            var r = sim.Residents[0];
            Do(sim, ActivityCatalog.PlanBuilding, SpotX, SpotY);
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
            Assert.That((plan.Width, plan.Depth), Is.EqualTo((5, 4)), "the ground it takes, in tiles");
            Assert.That(plan.DoorTile, Is.EqualTo(((int, int)?)(2, 3)));
            Assert.That(plan.Cells.Count, Is.EqualTo(30 * 4), "the doorway clears a whole tile, so one more wall block goes");
            Assert.That(plan.Cells.Take(30).All(c => c.Level == 0), Is.True, "the bottom layer first");
            Assert.That(plan.Needs().ToList(), Is.EqualTo(new List<(MaterialKind, int)> { (MaterialKind.Wood, 30) }), "one wood makes four blocks");
            Assert.That(plan.Furniture.Select(f => (f.Type, f.X, f.Y)), Is.EqualTo(new[] { (ObjectType.Kitchen, 1, 1) }), "a stove on its own tile");

            Assert.That(BuildPlan.TryParse("hut", 4, Hut.Select(row => row.ToLowerInvariant()).ToList(), out plan, out error), Is.True, "lower case is fine");
            Assert.That(plan.Cells.Count, Is.EqualTo(30 * 4));
            Assert.That(BuildPlan.TryParse("tall hut", 6, Hut, out plan, out error), Is.True, error);
            Assert.That(plan.Cells.Any(c => c.X == 5 && c.Y == 7 && c.Level == 0), Is.False, "the doorway clears the wall beside it");
            Assert.That(plan.Cells.Any(c => c.X == 5 && c.Y == 7 && c.Level == BuildingRules.DoorHeight), Is.True, "with a lintel above it");
            Assert.That(BuildPlan.TryParse("mixed", 2, new[] { "WSHITC" }, out plan, out error), Is.True, error);
            Assert.That(plan.Furniture, Is.Empty);
            Assert.That((plan.Width, plan.Depth), Is.EqualTo((3, 1)));

            var box = new[] { "WWWWWW", "W....W", "W....W", "W....W", "W....W", "WWWWWW" };
            Assert.That(BuildPlan.TryParse("box", 2, box, out _, out error), Is.False, "a shut-in room");
            Assert.That(error, Does.Contain("doorway"));
            Assert.That(BuildPlan.TryParse("hut", 0, Hut, out _, out _), Is.False);
            Assert.That(BuildPlan.TryParse("hut", BuildingRules.MaxHeight + 1, Hut, out _, out _), Is.False);
            Assert.That(BuildPlan.TryParse("x", 1, new[] { "WWQ" }, out _, out _), Is.False, "Q isn't a block");
            Assert.That(BuildPlan.TryParse("x", 1, new[] { "WW" }, out _, out _), Is.False, "too few blocks");
            Assert.That(BuildPlan.TryParse("x", 1, new[] { new string('W', BuildingRules.GridSize + 1) }, out _, out _), Is.False, "too wide");
            Assert.That(BuildPlan.TryParse("x", 1, Enumerable.Repeat("W", BuildingRules.GridSize + 1).ToList(), out _, out _), Is.False, "too deep");
        }

        [Test]
        public void TheyBuild_WhereverTheyChoose_OnFreeGround()
        {
            var sim = Town();
            var r = sim.Residents[0];
            sim.Tick();
            Assert.That(sim.World.ObjectsOfType(ObjectType.BuildPlot).Any(), Is.False, "no plots laid out for them");
            var ground = Option(sim, ActivityCatalog.PlanBuilding);
            Assert.That(ground.IsGround, Is.True);
            Assert.That(Option(sim, ActivityCatalog.Build), Is.Null, "nothing to build yet");

            var bench = sim.World.ObjectsOfType(ObjectType.Bench).First();
            foreach (var (x, y, why) in new[] { (22, 31, "isn't open ground"), (0, 5, "isn't open ground"), (-3, 5, "off the land"), (bench.Location.Position.X, bench.Location.Position.Y, "already there") })
            {
                Assert.That(sim.GroundProblem(r, new TinyCity.Simulation.World.Location(r.Location.MapId, x, y)), Does.Contain(why), $"({x}, {y})");
            }

            Assert.That(sim.ApplyDecision(r.Id, r.PendingDecisionId, "plan-building", "ground", "Home", null, SpotX, SpotY), Is.EqualTo(DecisionOutcome.Applied));
            var site = sim.Construction.Sites.Single();
            Assert.That((site.OwnerId, site.Corner), Is.EqualTo((r.Id, Spot())));
            Assert.That(r.TargetObjectId, Is.EqualTo(site.Id), "they go there to plan it");
        }

        [Test]
        public void APlan_MustFitTheGround_AndShutNothingIn()
        {
            var sim = Town();
            var r = sim.Residents[0];
            var bench = sim.World.ObjectsOfType(ObjectType.Bench).OrderBy(o => o.Location.Position.Y).ThenBy(o => o.Location.Position.X).First();
            BuildPlan.TryParse("hut", 1, Hut, out var hut, out _);
            Assert.That(sim.Construction.CheckPlacement(new Location(FlatLand.MapId, bench.Location.Position.X - 1, bench.Location.Position.Y - 1), hut, null, _ => false, r.Location, out _), Does.Contain("already"), "not over a camp bench");
            Assert.That(sim.Construction.CheckPlacement(new Location(FlatLand.MapId, 19, 28), hut, null, _ => false, r.Location, out _), Does.Contain("open ground"), "not in the pond");

            // A wall across the land's top corner would shut in the ground behind it, though the plan alone looks fine.
            var corner = new[] { "....WW", "....WW", "....WW", "....WW", "WWWWWW", "WWWWWW" };
            Assert.That(BuildPlan.TryParse("wall", 2, corner, out var wall, out var error), Is.True, error);
            Assert.That(sim.Construction.CheckPlacement(new Location(FlatLand.MapId, 1, 1), wall, null, _ => false, r.Location, out _), Does.Contain("shut in"));

            Assert.That(sim.Construction.CheckPlacement(Spot(), hut, null, _ => false, r.Location, out var stand), Is.Null);
            Assert.That(stand, Is.EqualTo(Spot(2, 3)), "the builder stands in the doorway");
            BuildPlan.TryParse("shed", 1, new[] { "WWWW", "WWWW" }, out var shed, out _);
            Assert.That(sim.Construction.CheckPlacement(Spot(), shed, null, _ => false, r.Location, out stand), Is.Null);
            Assert.That(stand, Is.EqualTo(Spot(0, -1)), "or beside a building with no door");

            var project = PlanHut(sim);
            Assert.That(sim.Construction.CheckPlacement(Spot(3, 2), hut, null, _ => false, r.Location, out _), Does.Contain("already"), "not over someone else's building");
            Assert.That(sim.GroundProblem(r, Spot(1, 1)), Is.Not.Null, "nobody plants or builds inside it either");
            Assert.That(project.Footprint.Count(), Is.EqualTo(20));
        }

        [Test]
        public void APlannedBuilding_IsLaidBlockByBlock_AndBlocksTheWay()
        {
            var sim = Town();
            var r = sim.Residents[0];
            var project = PlanHut(sim);
            Assert.That(project, Is.Not.Null);
            Assert.That(Option(sim, ActivityCatalog.Build), Is.Null, "no wood yet");
            Assert.That(Option(sim, ActivityCatalog.PlanBuilding), Is.Not.Null, "they may plan another building too");

            var wall = BuildingRules.TileOf(project.Corner, project.Plan.Cells[1]);
            var inside = BuildingRules.TileOf(project.Corner, 4, 4);
            var westOfHut = Spot(-1, 2);
            var before = sim.Pathfinder.Distance(westOfHut, inside);
            Assert.That(before, Is.EqualTo(3), "straight in, before the walls");

            r.Belongings.Add(MaterialKind.Wood, 10);
            Do(sim, ActivityCatalog.Build);
            Assert.That(r.Location, Is.EqualTo(Spot(2, 3)), "building from the doorway");
            Assert.That(project.Placed, Is.EqualTo(BuildingRules.BlocksPerSession));
            Assert.That(r.Belongings.Count(MaterialKind.Wood), Is.EqualTo(2), "32 blocks: the 30 of the bottom layer took 8 wood, and the 8th wood's last two blocks start the next");
            Assert.That(sim.Construction.IsBlocked(wall), Is.True);
            Assert.That(sim.Pathfinder.Distance(westOfHut, inside), Is.GreaterThan(before), "they walk round to the doorway");

            r.Belongings.Add(MaterialKind.Wood, 30);
            for (var i = 0; i < 6 && !project.IsFinished; i++)
            {
                Do(sim, ActivityCatalog.Build);
            }

            Assert.That(project.IsFinished, Is.True);
            Assert.That(r.Belongings.Count(MaterialKind.Wood), Is.EqualTo(10), "30 wood in all");
            Assert.That(Option(sim, ActivityCatalog.Build), Is.Null, "nothing left to lay");
            Assert.That(sim.History.Events.Any(e => e.Kind == TownEventKind.Built && e.ResidentIds.Contains(r.Id) && e.Summary.Contains("finished building")), Is.True);
            Assert.That(sim.Pathfinder.Distance(westOfHut, inside), Is.Not.EqualTo(Pathfinder.Unreachable), "the doorway lets them in");

            Assert.That(sim.World.TryGetObject(Construction.FurnitureIdOf(project.SiteId, project.Plan, 0), out var stove), Is.True, "a finished building has its stove");
            Assert.That(stove.Location, Is.EqualTo(Spot(1, 1)));
            Assert.That(sim.Planner.IsAvailableTo(r, stove), Is.True);

            var map = sim.World.GetMap(FlatLand.MapId);
            var pieces = BlockArt.Pieces(sim.World, sim.Construction, map, Palette.For(TinyCity.Simulation.Time.Season.Spring));
            var tiles = project.Plan.Cells.Select(c => (c.X / BuildingRules.CellsPerTile, c.Y / BuildingRules.CellsPerTile)).Distinct().Count();
            Assert.That(pieces.Count, Is.EqualTo(tiles + 1), "one picture per tile of blocks, and the stove (the site marker goes once building starts)");
            Assert.That(MapArt.Build(sim.World, map, Palette.For(TinyCity.Simulation.Time.Season.Spring)).Pieces.Any(p => p.Name == stove.Name), Is.False, "the stove is drawn with the blocks, not the map");
        }

        [Test]
        public void ABuiltStove_IsAnyonesWhoFindsItFree()
        {
            var sim = Town();
            var r = sim.Residents[0];
            var project = PlanHut(sim, height: 1);
            r.Belongings.Add(MaterialKind.Wood, 8);
            Do(sim, ActivityCatalog.Build);
            Assert.That(project.IsFinished, Is.True);
            sim.World.TryGetObject(Construction.FurnitureIdOf(project.SiteId, project.Plan, 0), out var stove);
            var other = sim.Residents[1];
            Assert.That(sim.Planner.IsAvailableTo(other, stove), Is.True, "nothing belongs to anyone");

            r.TargetObjectId = stove.Id;
            r.ActivityId = ActivityCatalog.Cook.Id;
            r.State = ResidentState.Performing;
            Assert.That(sim.Planner.IsAvailableTo(other, stove), Is.False, "but one cook at a time");
        }

        [Test]
        public void SomeoneElse_CanHelpBuild_WithTheirOwnMaterials_AndBothAreRemembered()
        {
            var sim = Town();
            var owner = sim.Residents[0];
            var project = PlanHut(sim, height: 1);
            var helper = sim.Residents[1];
            helper.Belongings.Add(MaterialKind.Wood, 8);
            var site = sim.Construction.SiteOf(project.SiteId);
            sim.World.TryGetObject(project.SiteId, out var siteObject);
            Assert.That(sim.Planner.CanDo(helper, ActivityCatalog.Build, siteObject, sim.Time), Is.True, $"anyone may help: {ActivityCatalog.Build.CanUse(siteObject)} {sim.Planner.IsAvailableTo(helper, siteObject)} {FoodSupply.Allows(sim.Food, helper, ActivityCatalog.Build, siteObject, sim.Time.Day)}");

            owner.Location = site.StandAt;
            helper.ResetActivity();
            helper.Location = site.StandAt;
            helper.TargetObjectId = siteObject.Id;
            helper.ActivityId = ActivityCatalog.Build.Id;
            helper.State = ResidentState.Performing;
            helper.MinutesRemaining = 1;
            sim.Tick();
            Assert.That(project.IsFinished, Is.True, string.Join(" | ", helper.Log.Select(l => l.Text)));
            Assert.That(owner.Belongings.Count(MaterialKind.Wood), Is.EqualTo(0), "the owner's own wood wasn't touched");
            Assert.That(helper.Belongings.Count(MaterialKind.Wood), Is.EqualTo(0), "the helper's wood built it");
            Assert.That(project.Builders, Is.EqualTo(new[] { helper.Id }));
            Assert.That(sim.Memories.ForResident(helper.Id).Any(m => m.Summary.StartsWith("I helped build")), Is.True);

            var loaded = SaveSerializer.Load(SaveSerializer.Save(sim), FlatLand.CreateWorld()).Simulation;
            Assert.That(loaded.Construction.At(project.SiteId).Builders, Is.EqualTo(new[] { helper.Id }));
        }

        [Test]
        public void APlan_CanHoldFurniture_OrBeFurnitureAlone()
        {
            var cabin = new[] { "WWWWWWWWWWWW", "W..........W", "W.K..K..X..W", "W..........W", "W..........W", "W..........W", "W..X..M..N.W", "W..........W", "WWWWDWWWWWWW" };
            Assert.That(BuildPlan.TryParse("cabin", 2, cabin, out var plan, out var error), Is.True, error);
            Assert.That(plan.Furniture.Select(f => f.Type), Is.EqualTo(new[] { ObjectType.Kitchen, ObjectType.Kitchen, ObjectType.Workbench, ObjectType.Workbench, ObjectType.MarketStall, ObjectType.Bench }));
            Assert.That(BuildPlan.TryParse("bedroom", 2, new[] { "WWWWWW", "W.B..W", "WWDWWW" }, out _, out error), Is.False, "there are no beds");

            Assert.That(BuildPlan.TryParse("fish stall", 1, new[] { "M" }, out var stall, out error), Is.True, "furniture alone, no blocks: " + error);
            Assert.That(stall.Cells, Is.Empty);

            var sim = Town();
            var r = sim.Residents[0];
            Do(sim, ActivityCatalog.PlanBuilding, SpotX, SpotY);
            var siteId = r.Sketch.SiteId;
            Assert.That(sim.ApplySketch(r.Id, r.Sketch.Id, "fish stall", 1, new[] { "M" }, out error), Is.EqualTo(DecisionOutcome.Applied), error);
            Assert.That(sim.Construction.At(siteId).IsFinished, Is.True, "there as soon as it is planned");
            var made = sim.World.Objects.Single(o => o.Type == ObjectType.MarketStall);
            Assert.That((made.Name, made.Location), Is.EqualTo(("Fish stall", Spot())));
            Assert.That(BuildingRules.IsBuilt(made), Is.True);
            Assert.That(sim.History.Events.Any(e => e.Kind == TownEventKind.Built), Is.True);

            r.Food.Add(FoodKind.Fish, 3, sim.Time.Day + 2);
            Assert.That(sim.Planner.FreeTimeOptions(r).Any(o => o.ActionId == ActivityCatalog.RunStall.Id && o.TargetId == made.Id), Is.True, "and they can sell from it");
            var loaded = SaveSerializer.Load(SaveSerializer.Save(sim), FlatLand.CreateWorld()).Simulation;
            Assert.That(loaded.World.Objects.Count(o => o.Type == ObjectType.MarketStall), Is.EqualTo(1));
        }

        [Test]
        public void ABadPlan_IsRefused_AndAnUnplannedSiteIsLetGo()
        {
            var sim = Town();
            var r = sim.Residents[0];
            Do(sim, ActivityCatalog.PlanBuilding, SpotX, SpotY);
            var siteId = r.Sketch.SiteId;
            Assert.That(sim.ApplySketch(r.Id, r.Sketch.Id, "box", 2, new[] { "WWWWWW", "W....W", "W....W", "W....W", "W....W", "WWWWWW" }, out var error), Is.EqualTo(DecisionOutcome.InvalidText));
            Assert.That(error, Is.Not.Null);
            Assert.That(r.Sketch, Is.Not.Null, "still waiting for a better plan");
            Assert.That(sim.Construction.At(siteId), Is.Null);
            Assert.That(sim.ApplySketch(r.Id, r.Sketch.Id + 1, "hut", 1, Hut, out _), Is.EqualTo(DecisionOutcome.Stale));

            sim.Advance(BuildingRules.SketchWaitMinutes + 1);
            Assert.That(r.Sketch, Is.Null, "given up on");
            Assert.That(sim.Construction.Sites, Is.Empty, "and the ground is free again");
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

            var result = SaveSerializer.Load(SaveSerializer.Save(sim), FlatLand.CreateWorld());
            Assert.That(result.Success, Is.True);
            Assert.That(result.Warnings, Is.Empty);
            var loaded = result.Simulation;
            var lp = loaded.Construction.At(project.SiteId);
            Assert.That(lp, Is.Not.Null);
            Assert.That((lp.OwnerId, lp.Plan.Name, lp.Plan.Height, lp.Placed, lp.Corner), Is.EqualTo((r.Id, "Little hut", 1, 20, Spot())));
            Assert.That(lp.Plan.Rows, Is.EqualTo(project.Plan.Rows));
            Assert.That(loaded.Construction.SiteOf(project.SiteId).StandAt, Is.EqualTo(Spot(2, 3)));
            Assert.That(loaded.Construction.NextSiteNumber, Is.EqualTo(sim.Construction.NextSiteNumber));
            foreach (var cell in project.PlacedCells)
            {
                Assert.That(loaded.Construction.IsBlocked(BuildingRules.TileOf(project.Corner, cell)), Is.True);
            }

            var prompt = DecisionPrompt.UserText(loaded, loaded.Residents[0], loaded.FreeTimeOptionsFor(loaded.Residents[0]), new TinyCity.Simulation.Memory.MemoryRecord[0]);
            Assert.That(prompt, Does.Contain($"Your building: a Little hut at ({SpotX}, {SpotY}), 20 of 30 blocks laid; it still needs 3 wood."));
            var other = loaded.Residents[1];
            Assert.That(DecisionPrompt.UserText(loaded, other, loaded.FreeTimeOptionsFor(other), new TinyCity.Simulation.Memory.MemoryRecord[0]),
                Does.Contain($"{r.FullName}'s Little hut at ({SpotX}, {SpotY}): 20 of 30 blocks laid, still needs 3 wood; you could help build it."));
            Assert.That(prompt, Does.Contain("your Little hut at x 31-35, y 10-13"));

            r.Belongings.Add(MaterialKind.Wood, 3);
            Do(sim, ActivityCatalog.Build);
            Assert.That(project.IsFinished, Is.True);
            var stoveId = Construction.FurnitureIdOf(project.SiteId, project.Plan, 0);
            result = SaveSerializer.Load(SaveSerializer.Save(sim), FlatLand.CreateWorld());
            Assert.That(result.Success, Is.True);
            Assert.That(result.Warnings, Is.Empty);
            var home = result.Simulation.GetResident(r.Id);
            Assert.That(result.Simulation.World.TryGetObject(stoveId, out var stove), Is.True, "the stove is back with the building");
            Assert.That(result.Simulation.Planner.IsAvailableTo(home, stove), Is.True);
        }

        [Test]
        public void AiService_AsksForAPlan_AndTheAnswerBecomesAProject()
        {
            var sim = Town();
            var r = sim.Residents[0];
            Do(sim, ActivityCatalog.PlanBuilding, SpotX, SpotY);
            var text = SketchPrompt.UserText(sim, r, r.Sketch);
            Assert.That(text, Does.Contain("W = wood").And.Contain("D for a doorway").And.Contain($"({SpotX}, {SpotY})").And.Contain("about 8 by 8 paces"));

            // Everything else gets an unusable answer, so only the plan goes through.
            var transport = FakeAiTransport.WorkingOllama(body => body.Contains("D for a doorway") ? "{\"name\":\"stone shed\",\"height\":2,\"rows\":[\"SSS\",\"S.S\",\"SDS\"]}" : "{}");
            var ai = new AiService(new OllamaSettings(), transport);
            ai.Attach(sim);
            for (var i = 0; i < 2000 && sim.Construction.At(r.TargetObjectId) == null; i++)
            {
                ai.Update(i * 0.1);
            }

            var project = sim.Construction.Of(r.Id);
            Assert.That(project?.Plan.Name, Is.EqualTo("stone shed"));
            Assert.That(project.Corner, Is.EqualTo(Spot()));
            Assert.That(project.Plan.Cells.Count, Is.EqualTo(12), "the doorway clears its whole tile");
            Assert.That(r.Sketch, Is.Null);
        }
    }
}
