using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation;
using TinyCity.Simulation.Activities;
using TinyCity.Simulation.AI;
using TinyCity.Simulation.Content;
using TinyCity.Simulation.Health;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Time;
using TinyCity.Simulation.World;

namespace TinyCity.Tests.EditMode
{
    /// <summary>Helpers for building the open land with residents in a chosen situation, and a stand-in AI that makes their choices.</summary>
    internal static class TestTown
    {
        /// <summary>A spot on the open land.</summary>
        public static Location At(int x, int y) => new Location(FlatLand.MapId, x, y);

        /// <summary>The first resident alone on the open land, at the camp unless <paramref name="location"/> says otherwise.</summary>
        public static TownSimulation WithResident(GameTime time, Location? location = null, double needsLevel = 90, System.Action<Needs> adjust = null, Personality personality = null)
        {
            var world = FlatLand.CreateWorld();
            var template = FlatLand.CreateResidents()[0];
            var needs = new Needs(needsLevel);
            adjust?.Invoke(needs);
            var resident = new Resident(template.Id, template.FirstName, template.LastName, template.AgeYears, personality ?? template.Personality,
                location ?? template.Location, needs) { Outlook = template.Outlook };
            return new TownSimulation(world, new[] { resident }, time);
        }

        /// <summary>
        /// The starter residents in a chosen situation. <paramref name="place"/> can move a resident (null keeps them at
        /// their starting spot), <paramref name="adjust"/> sets their needs, and <paramref name="include"/> leaves residents out.
        /// </summary>
        public static TownSimulation WithResidents(GameTime time, System.Func<Resident, Location?> place = null, System.Action<Resident, Needs> adjust = null, System.Func<Resident, bool> include = null, double needsLevel = 90)
        {
            var world = FlatLand.CreateWorld();
            var residents = new List<Resident>();
            foreach (var template in FlatLand.CreateResidents())
            {
                if (include != null && !include(template))
                {
                    continue;
                }

                var needs = new Needs(needsLevel);
                adjust?.Invoke(template, needs);
                residents.Add(new Resident(template.Id, template.FirstName, template.LastName, template.AgeYears, template.Personality,
                    place?.Invoke(template) ?? template.Location, needs) { Outlook = template.Outlook });
            }

            return new TownSimulation(world, residents, time);
        }

        /// <summary>The first two residents standing next to each other in the open, a little lonely.</summary>
        public static TownSimulation TwoSideBySide(GameTime time, double social = 50)
        {
            return WithResidents(
                time,
                r => r.Id == StarterResidents.ResidentId ? At(40, 15) : r.Id == StarterResidents.ArthurId ? At(41, 15) : (Location?)null,
                (r, n) => n[NeedType.Social] = social,
                r => r.Id == StarterResidents.ResidentId || r.Id == StarterResidents.ArthurId);
        }

        /// <summary>Starts a chat between the first two residents where they stand, as if one had walked over.</summary>
        public static TinyCity.Simulation.Social.ConversationSession StartChat(TownSimulation sim)
        {
            sim.StartConversation(sim.Residents[0], sim.Residents[1]);
            return sim.ActiveConversations[sim.ActiveConversations.Count - 1];
        }

        public static WorldObject Object(TownSimulation sim, string id)
        {
            sim.World.TryGetObject(id, out var obj);
            return obj;
        }

        /// <summary>Saturday 14:00.</summary>
        public static GameTime WeekendAfternoon => GameTime.FromDayAndTime(6, 14, 0);

        /// <summary>
        /// A stand-in for the AI: answers every waiting choice with an option picked by <see cref="Chance"/> (nothing done
        /// on open ground, which needs a spot), the first that meets an urgent need, goes over to say hello to someone they notice now and then, and writes every awaited chat line.
        /// The same town always gets the same answers, so runs replay exactly.
        /// </summary>
        public static void Answer(TownSimulation sim)
        {
            foreach (var r in sim.Residents)
            {
                if (!r.IsAlive || r.State != ResidentState.Deciding || r.PendingDecisionId <= 0)
                {
                    continue;
                }

                // Owner, 6 November: without a role nobody can fish, cook or touch the pond, so the stand-in takes one up
                // in the morning plan the way a real answer must.
                if (sim.NeedsDayPlan(r) && r.Role == null && sim.Brief.Roles.Count > 0)
                {
                    var roles = DayPlanPrompt.RoleChoices(sim, r).Cast<string>().ToList();
                    var role = roles[(int)(Chance.Roll(r.Id, sim.Time.Day, 13) * roles.Count) % roles.Count];
                    sim.ApplyDayPlan(r.Id, DayPlan.DayFor(sim.Time), new System.Collections.Generic.List<DayPlanEntry> { new DayPlanEntry(sim.Time.Hour, "Get on with it") }, role);
                }

                var options = sim.FreeTimeOptionsFor(r).Where(o => !o.IsGround).ToList();
                var urgent = ActivityPlanner.MostUrgentNeed(r);
                var pick = urgent == null ? null : options.FirstOrDefault(o => o.Activity?.PrimaryNeed == urgent);
                pick = pick ?? options[(int)(Chance.Roll(r.Id, sim.Time.TotalMinutes, 7) * options.Count) % options.Count];
                sim.ApplyDecision(r.Id, r.PendingDecisionId, pick.ActionId, pick.TargetId, "a stand-in choice");
            }

            foreach (var r in sim.Residents)
            {
                if (r.IsAlive && r.Reaction != null)
                {
                    sim.ApplyReaction(r.Id, r.Reaction.Id, Chance.Roll(r.Id, sim.Time.TotalMinutes, 11) < 0.3, "a stand-in reaction");
                }
            }

            foreach (var session in sim.ActiveConversations.ToList())
            {
                if (session.AwaitingLine)
                {
                    sim.ApplyConversationLine(session.Id, session.LineRequestId, session.SpeakerId, "Nice to see you.", "warm", "the day", false);
                }
            }
        }

        /// <summary>Runs the town <paramref name="minutes"/> minutes with <see cref="Answer"/> making every choice.</summary>
        public static void Run(TownSimulation sim, int minutes)
        {
            sim.AiDecisionsEnabled = true;
            for (var i = 0; i < minutes; i++)
            {
                Answer(sim);
                sim.Tick();
            }
        }
    }
}
