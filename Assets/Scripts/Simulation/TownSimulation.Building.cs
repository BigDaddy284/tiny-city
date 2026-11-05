using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation.Activities;
using TinyCity.Simulation.Building;
using TinyCity.Simulation.Economy;
using TinyCity.Simulation.History;
using TinyCity.Simulation.Memory;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.World;

namespace TinyCity.Simulation
{
    /// <summary>Tiny City 2's block building: a resident plans a building for an empty plot (the AI sketches it, the game checks it), then lays it block by block.</summary>
    public sealed partial class TownSimulation
    {
        private long nextSketchId = 1;

        private bool BuildingAllows(Resident r, ActivityDefinition a, WorldObject plot)
        {
            if (plot == null || plot.Type != ObjectType.BuildPlot)
            {
                return false;
            }

            var project = Construction.At(plot.Id);
            if (a == ActivityCatalog.PlanBuilding)
            {
                return project == null && Construction.Of(r.Id) == null && r.Sketch == null
                    && !residents.Any(o => o.IsAlive && o.Sketch != null && o.Sketch.PlotId == plot.Id);
            }

            return project != null && project.OwnerId == r.Id && project.Next is BlockCell next && r.Belongings.Count(next.Material) > 0;
        }

        private void StartSketch(Resident r)
        {
            r.Sketch = new PendingSketch(nextSketchId++, r.TargetObjectId, Time.TotalMinutes);
        }

        private void StepSketches()
        {
            if (!FoodMustBeMade)
            {
                return;
            }

            foreach (var r in residents)
            {
                if (r.Sketch != null && Time.TotalMinutes - r.Sketch.AskedMinute >= BuildingRules.SketchWaitMinutes)
                {
                    r.AddLog(Time, "Gave up on planning a building for now");
                    r.Sketch = null;
                }
            }
        }

        /// <summary>
        /// The AI's plan for the plot a resident is planning: accepted only if it passes <see cref="BuildPlan.TryParse"/>
        /// and the plot is still free; <paramref name="error"/> says what was wrong so the question can be asked again.
        /// </summary>
        public DecisionOutcome ApplySketch(string residentId, long sketchId, string name, int height, IReadOnlyList<string> rows, out string error)
        {
            error = null;
            var r = GetResident(residentId);
            if (r == null)
            {
                return DecisionOutcome.UnknownResident;
            }

            if (!r.IsAlive)
            {
                return DecisionOutcome.NotAlive;
            }

            if (r.Sketch == null || r.Sketch.Id != sketchId)
            {
                return DecisionOutcome.Stale;
            }

            var plotId = r.Sketch.PlotId;
            if (!World.TryGetObject(plotId, out var plot) || Construction.At(plotId) != null || Construction.Of(r.Id) != null)
            {
                r.Sketch = null;
                return DecisionOutcome.Stale;
            }

            if (!BuildPlan.TryParse(name, height, rows, out var plan, out error))
            {
                return DecisionOutcome.InvalidText;
            }

            r.Sketch = null;
            Construction.Add(new Project(plotId, r.Id, plan, 0, Time.Day));
            var needs = string.Join(", ", plan.Needs().Select(n => $"{n.count} {CraftingRules.Name(n.material)}"));
            r.AddLog(Time, $"Planned a {plan.Name} on the {plot.Name.ToLowerInvariant()}: {plan.Cells.Count} blocks ({needs})");
            Memories.Add(r.Id, Time, MemoryType.Event, 5, $"I planned a {plan.Name} on the {plot.Name.ToLowerInvariant()}. It will take {needs}.", tags: new[] { "building", plotId });
            return DecisionOutcome.Applied;
        }

        public bool DropSketch(string residentId, long sketchId)
        {
            var r = GetResident(residentId);
            if (r?.Sketch == null || r.Sketch.Id != sketchId)
            {
                return false;
            }

            r.Sketch = null;
            return true;
        }

        private void FinishBuilding(Resident r, ActivityDefinition activity)
        {
            if (!FoodMustBeMade || activity != ActivityCatalog.Build)
            {
                return;
            }

            var project = Construction.At(r.TargetObjectId);
            if (project == null || project.OwnerId != r.Id || project.IsFinished)
            {
                return;
            }

            var laid = Construction.LayBlocks(project, r.Belongings, BuildingRules.BlocksPerSession, at => residents.Any(o => o.IsAlive && o.Location == at), out var stoppedBy);
            if (laid.Count > 0)
            {
                Pathfinder.Forget();
            }

            var what = laid.Count == 0 ? "Laid no blocks" : $"Laid {laid.Count} block{(laid.Count == 1 ? string.Empty : "s")} of the {project.Plan.Name} ({project.Placed}/{project.Plan.Cells.Count})";
            r.AddLog(Time, stoppedBy != null && !project.IsFinished ? $"{what}: {stoppedBy}" : what);
            if (project.IsFinished)
            {
                World.TryGetObject(project.PlotId, out var plot);
                Memories.Add(r.Id, Time, MemoryType.Event, 7, $"I finished building my {project.Plan.Name}, all {project.Plan.Cells.Count} blocks of it.", tags: new[] { "building", "built", project.PlotId });
                History.Add(Time, TownEventKind.Built, $"{r.FullName} finished building a {project.Plan.Name} on the {plot?.Name.ToLowerInvariant() ?? "plot"}.", new[] { r.Id });
            }
        }
    }
}
