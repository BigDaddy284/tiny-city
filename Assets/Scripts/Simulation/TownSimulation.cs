using System;
using System.Collections.Generic;
using System.Linq;
using TinyTown.Simulation.Activities;
using TinyTown.Simulation.Residents;
using TinyTown.Simulation.Time;
using TinyTown.Simulation.World;

namespace TinyTown.Simulation
{
    /// <summary>
    /// Owns the town's live state and advances it one in-game minute per <see cref="Tick"/>.
    /// Given the same starting state it always produces the same result.
    /// </summary>
    public sealed class TownSimulation
    {
        /// <summary>How long a resident waits before re-planning after finding a destination unreachable.</summary>
        public const int UnreachableWaitMinutes = 15;

        /// <summary>How long a resident with nothing to do relaxes before checking again.</summary>
        public const int RelaxMinutes = 15;

        private readonly List<Resident> residents;

        public TownSimulation(TownWorld world, IEnumerable<Resident> residents, GameTime time)
        {
            World = world ?? throw new ArgumentNullException(nameof(world));
            this.residents = residents.ToList();
            if (this.residents.Select(r => r.Id).Distinct().Count() != this.residents.Count)
            {
                throw new ArgumentException("Resident ids must be unique.");
            }

            Time = time;
            Pathfinder = new Pathfinder(world);
            Planner = new ActivityPlanner(world, Pathfinder);
        }

        public TownWorld World { get; }

        public GameTime Time { get; private set; }

        public IReadOnlyList<Resident> Residents => residents;

        public Pathfinder Pathfinder { get; }

        public ActivityPlanner Planner { get; }

        public Resident GetResident(string id) => residents.FirstOrDefault(r => r.Id == id);

        public void Advance(int minutes)
        {
            for (var i = 0; i < minutes; i++)
            {
                Tick();
            }
        }

        public void Tick()
        {
            foreach (var resident in residents)
            {
                if (resident.IsAlive)
                {
                    Step(resident);
                }
            }

            Time = Time.AddMinutes(1);
        }

        private void Step(Resident r)
        {
            ActivityCatalog.TryGet(r.ActivityId, out var activity);
            NeedRules.ApplyMinute(r.Needs, r.Personality, r.State == ResidentState.Performing ? activity : null);

            switch (r.State)
            {
                case ResidentState.Waiting:
                    r.MinutesRemaining--;
                    if (r.MinutesRemaining <= 0)
                    {
                        r.ResetActivity();
                    }

                    break;

                case ResidentState.Performing:
                    if (r.MinutesRemaining > 0)
                    {
                        r.MinutesRemaining--;
                    }

                    if (Planner.IsComplete(r, activity, Time))
                    {
                        r.ResetActivity();
                    }
                    else if (Planner.ShouldInterrupt(r, activity, Time))
                    {
                        r.AddLog(Time, $"Stopped {activity.Verb}: something more important came up");
                        r.ResetActivity();
                    }

                    break;

                case ResidentState.Travelling:
                    if (Planner.ShouldInterrupt(r, activity, Time))
                    {
                        r.AddLog(Time, $"Changed plans on the way to {activity.Name.ToLowerInvariant()}");
                        r.ResetActivity();
                        break;
                    }

                    Move(r, activity);
                    break;

                default:
                    StartNextActivity(r);
                    break;
            }
        }

        private void StartNextActivity(Resident r)
        {
            var choice = Planner.Choose(r, Time);
            if (choice == null)
            {
                r.ResetActivity();
                r.State = ResidentState.Waiting;
                r.MinutesRemaining = RelaxMinutes;
                r.Goal = "Taking it easy";
                return;
            }

            r.ActivityId = choice.Activity.Id;
            r.TargetObjectId = choice.Target.Id;
            r.Goal = choice.Reason;
            r.State = ResidentState.Travelling;
            r.AddLog(Time, $"Decided to {choice.Activity.Name.ToLowerInvariant()} at {choice.Target.Name}: {choice.Reason}");
            if (r.Location == choice.Target.Location)
            {
                BeginPerforming(r, choice.Activity);
            }
        }

        private void Move(Resident r, ActivityDefinition activity)
        {
            if (activity == null || !World.TryGetObject(r.TargetObjectId, out var target))
            {
                r.ResetActivity();
                return;
            }

            if (r.Location == target.Location)
            {
                BeginPerforming(r, activity);
                return;
            }

            if (Pathfinder.TryGetNextStep(r.Location, target.Location, out var next))
            {
                if (next.MapId != r.Location.MapId)
                {
                    r.AddLog(Time, $"Went {(World.GetMap(next.MapId).IsExterior ? "outside" : "inside")} to {World.GetMap(next.MapId).Name}");
                }

                r.Location = next;
                return;
            }

            r.AddLog(Time, $"Couldn't find a way to {target.Name}; waiting before trying again");
            r.ResetActivity();
            r.State = ResidentState.Waiting;
            r.MinutesRemaining = UnreachableWaitMinutes;
            r.Goal = $"Waiting: no route to {target.Name}";
        }

        private void BeginPerforming(Resident r, ActivityDefinition activity)
        {
            r.State = ResidentState.Performing;
            r.MinutesRemaining = ActivityPlanner.DurationFor(r, activity, Time);
            r.AddLog(Time, $"Started {activity.Verb}");
        }
    }
}
