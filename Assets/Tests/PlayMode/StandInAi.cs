using System.Linq;
using TinyCity.Simulation;
using TinyCity.Simulation.Activities;
using TinyCity.Simulation.AI;
using TinyCity.Simulation.Health;
using TinyCity.Simulation.Residents;

namespace TinyCity.Tests.PlayMode
{
    /// <summary>
    /// CI has no Ollama, and in Tiny City 2 nobody moves without it, so these tests make each waiting resident's choice
    /// themselves: a role in the morning plan, then an urgent need first, otherwise one of their options picked by <see cref="Chance"/>.
    /// </summary>
    internal static class StandInAi
    {
        public static void Answer(TownSimulation sim)
        {
            foreach (var r in sim.Residents)
            {
                if (!r.IsAlive || r.State != ResidentState.Deciding || r.PendingDecisionId <= 0)
                {
                    continue;
                }

                // Without a role nobody can fish, cook or touch the pond, so the stand-in takes one up in the morning plan
                // the way a real answer must.
                if (sim.NeedsDayPlan(r) && r.Role == null && sim.Brief.Roles.Count > 0)
                {
                    var roles = DayPlanPrompt.RoleChoices(sim, r).Cast<string>().ToList();
                    var role = roles[(int)(Chance.Roll(r.Id, sim.Time.Day, 13) * roles.Count) % roles.Count];
                    sim.ApplyDayPlan(r.Id, DayPlan.DayFor(sim.Time), new System.Collections.Generic.List<DayPlanEntry> { new DayPlanEntry(sim.Time.Hour, "Get on with it") }, role);
                }

                var options = sim.FreeTimeOptionsFor(r).Where(o => !o.IsGround).ToList();
                if (options.Count == 0)
                {
                    continue;
                }

                var urgent = ActivityPlanner.MostUrgentNeed(r);
                var pick = urgent == null ? null : options.FirstOrDefault(o => o.Activity?.PrimaryNeed == urgent);
                pick = pick ?? options[(int)(Chance.Roll(r.Id, sim.Time.TotalMinutes, 7) * options.Count) % options.Count];
                sim.ApplyDecision(r.Id, r.PendingDecisionId, pick.ActionId, pick.TargetId, "a stand-in choice");
            }
        }
    }
}
