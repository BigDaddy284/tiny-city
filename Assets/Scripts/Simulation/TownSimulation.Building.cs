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
    /// <summary>Tiny City 2's block building: a resident picks a spot and plans a building there (the AI sketches it, the game checks it fits), then lays it block by block.</summary>
    public sealed partial class TownSimulation
    {
        private long nextSketchId = 1;

        private bool BuildingAllows(Resident r, ActivityDefinition a, WorldObject obj)
        {
            if (a == ActivityCatalog.PlanBuilding)
            {
                // Anywhere they choose (no object; a site they chose before and never planned is let go), or back at that site.
                return r.Sketch == null && (obj == null
                    || (Construction.SiteOf(obj.Id) is Site site && site.OwnerId == r.Id && Construction.At(site.Id) == null));
            }

            var project = Construction.At(obj?.Id);
            return project != null && project.OwnerId == r.Id && project.Next is BlockCell next && (!project.NextTakesMaterial || r.Belongings.Count(next.Material) > 0);
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
                    DropUnplannedSite(r);
                    r.Sketch = null;
                }
            }
        }

        /// <summary>A site chosen but never planned is let go, so the ground is free again.</summary>
        private void DropUnplannedSite(Resident r)
        {
            var site = Construction.UnplannedOf(r.Id);
            if (site != null && !(r.TargetObjectId == site.Id && (r.State == ResidentState.Travelling || r.State == ResidentState.Performing)))
            {
                Construction.RemoveSite(site.Id);
            }
        }

        /// <summary>
        /// The AI's plan for the site a resident is planning: accepted only if it passes <see cref="BuildPlan.TryParse"/>
        /// and fits on the ground there (<see cref="Construction.CheckPlacement"/>); <paramref name="error"/> says what was
        /// wrong so the question can be asked again.
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

            var site = Construction.SiteOf(r.Sketch.SiteId);
            if (site == null || site.OwnerId != r.Id || Construction.At(site.Id) != null)
            {
                r.Sketch = null;
                return DecisionOutcome.Stale;
            }

            if (!BuildPlan.TryParse(name, height, rows, out var plan, out error))
            {
                return DecisionOutcome.InvalidText;
            }

            error = Construction.CheckPlacement(site.Corner, plan, site.Id, _ => false, r.Location, out var standAt);
            if (error != null)
            {
                return DecisionOutcome.InvalidText;
            }

            r.Sketch = null;
            Construction.Add(new Project(site.Id, r.Id, site.Corner, plan, 0, Time.Day), standAt);
            var needs = string.Join(", ", plan.Needs().Select(n => $"{n.count} {CraftingRules.Name(n.material)}"));
            var where = $"({site.Corner.Position.X}, {site.Corner.Position.Y})";
            r.AddLog(Time, $"Planned a {plan.Name} at {where}: {plan.Cells.Count} blocks ({needs})");
            Memories.Add(r.Id, Time, MemoryType.Event, 5, $"I planned a {plan.Name} at {where} on the {World.GetMap(site.Corner.MapId).Name.ToLowerInvariant()}. It will take {needs}.", tags: new[] { "building", site.Id });
            return DecisionOutcome.Applied;
        }

        public bool DropSketch(string residentId, long sketchId)
        {
            var r = GetResident(residentId);
            if (r?.Sketch == null || r.Sketch.Id != sketchId)
            {
                return false;
            }

            DropUnplannedSite(r);
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
                Memories.Add(r.Id, Time, MemoryType.Event, 7, $"I finished building my {project.Plan.Name}, all {project.Plan.Cells.Count} blocks of it.", tags: new[] { "building", "built", project.SiteId });
                var movedIn = MoveIntoBuilding(r, project);
                History.Add(Time, TownEventKind.Built, $"{r.FullName} finished building a {project.Plan.Name} on the {World.GetMap(project.Corner.MapId).Name.ToLowerInvariant()}{(movedIn ? " and moved in" : string.Empty)}.", new[] { r.Id });
            }
        }

        /// <summary>
        /// A finished building with a bed becomes its owner's home: they sleep in its bed and pay no rent, and children
        /// who shared their bed come too. The home they leave is free for anyone else.
        /// </summary>
        private bool MoveIntoBuilding(Resident r, Project project)
        {
            var bedId = Construction.BedIdOf(project.SiteId);
            if (!project.Plan.IsHome || !World.TryGetObject(bedId, out var bed) || r.BedObjectId == bedId)
            {
                return false;
            }

            var oldBed = r.BedObjectId;
            var oldHome = r.HomeMapId;
            foreach (var mover in residents.Where(o => o == r || (o.IsAlive && o.IsMinor && o.BedObjectId == oldBed && o.ParentIds.Contains(r.Id))).ToList())
            {
                mover.HomeMapId = bed.Location.MapId;
                mover.BedObjectId = bedId;
                mover.WeeklyRentPence = 0;
            }

            r.AddLog(Time, $"Moved into the {project.Plan.Name}");
            Memories.Add(r.Id, Time, MemoryType.Event, 8, $"I moved into the {project.Plan.Name} I built myself{(oldHome == null ? ", after having nowhere to live" : string.Empty)}. It's my home now.", tags: new[] { "home", "building", project.SiteId });
            return true;
        }
    }
}
