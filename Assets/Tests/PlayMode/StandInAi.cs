using System.Linq;
using TinyCity.Simulation;
using TinyCity.Simulation.Activities;
using TinyCity.Simulation.Health;
using TinyCity.Simulation.Residents;

namespace TinyCity.Tests.PlayMode
{
    /// <summary>
    /// CI has no Ollama, and in Tiny City 2 nobody moves without it, so these tests make each waiting resident's choice
    /// themselves: an urgent need first, otherwise one of their options picked by <see cref="Chance"/>.
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
