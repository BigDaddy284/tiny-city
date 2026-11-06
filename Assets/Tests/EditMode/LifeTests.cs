using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinyCity.Simulation;
using TinyCity.Simulation.Activities;
using TinyCity.Simulation.AI;
using TinyCity.Simulation.Content;
using TinyCity.Simulation.Health;
using TinyCity.Simulation.Persistence;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Time;
using TinyCity.Simulation.World;

namespace TinyCity.Tests.EditMode
{
    /// <summary>Health and death.</summary>
    public class LifeTests
    {
        private static Resident Get(TownSimulation sim, string id) => sim.GetResident(id);

        private static Resident Mabel(TownSimulation sim) => sim.GetResident(StarterResidents.ResidentId);

        private static string Log(Resident r) => string.Join("\n", r.Log.Select(e => $"{e.Time} {e.Text}"));

        [Test]
        public void Chance_IsRepeatable_AndSpread()
        {
            Assert.That(Chance.Roll("res-0001", 12, 1), Is.EqualTo(Chance.Roll("res-0001", 12, 1)));
            Assert.That(Chance.Roll("res-0001", 12, 1), Is.Not.EqualTo(Chance.Roll("res-0001", 13, 1)));
            var rolls = Enumerable.Range(1, 2000).Select(d => Chance.Roll("res-0002", d, 7)).ToList();
            Assert.That(rolls.All(x => x >= 0 && x < 1), Is.True);
            Assert.That(rolls.Average(), Is.EqualTo(0.5).Within(0.05));
            Assert.That(rolls.Count(x => x < 0.1), Is.InRange(140, 260));
        }

        [Test]
        public void TheDead_NoLongerLive_OrGetChattedTo()
        {
            var sim = FlatLand.CreateNewGame();
            var edith = Get(sim, StarterResidents.EdithId);
            sim.Die(edith, "old age");
            var needs = edith.Needs[NeedType.Hunger];
            var location = edith.Location;

            sim.Advance(8 * GameTime.MinutesPerDay);

            Assert.That(edith.Needs[NeedType.Hunger], Is.EqualTo(needs));
            Assert.That(edith.Location, Is.EqualTo(location));
            Assert.That(sim.Conversations.All.Any(c => c.Participants.Contains(edith.Id) && c.Started.TotalMinutes > edith.DiedAt.Value.TotalMinutes), Is.False);
            Assert.That(sim.ApplyDecision(edith.Id, 1, "relax", "none", "x"), Is.EqualTo(DecisionOutcome.NotAlive));
        }

        [Test]
        public void TheDead_MakeNoAiRequests()
        {
            var transport = FakeAiTransport.WorkingOllama(_ => "{\"action\":\"relax\",\"target_id\":\"none\",\"reason\":\"x\",\"say\":\"\"}");
            var service = new AiService(new TinyCity.Simulation.Configuration.OllamaSettings(), transport);
            var sim = FlatLand.CreateNewGame();
            foreach (var r in sim.Residents.Where(r => r.Id != StarterResidents.ResidentId).ToList())
            {
                sim.Die(r, "a test");
            }

            service.Attach(sim);
            for (var i = 0; i < 600; i++)
            {
                service.Update(i * 0.5);
                sim.Tick();
            }

            var bodies = transport.Calls.Select(c => c.Body ?? string.Empty).ToList();
            Assert.That(bodies.Any(b => b.Contains("Mabel")), Is.True, "the living still use the AI");
            Assert.That(bodies.Any(b => b.Contains("You are Edith") || b.Contains("You are Arthur")), Is.False);
        }

        [Test]
        public void Neglect_WearsHealthDown_AndCareBringsItBack()
        {
            var sim = TestTown.WithResident(TestTown.WeekendAfternoon);
            var r = Mabel(sim);
            r.Health = 80;
            r.Needs[NeedType.Hunger] = 0;
            r.Needs[NeedType.Thirst] = 0;

            for (var i = 0; i < 60; i++)
            {
                HealthRules.ApplyMinute(r, lowest: 0);
            }

            Assert.That(r.Health, Is.EqualTo(80 - (2 * HealthRules.NeglectDrainPerHour)).Within(0.01));

            r.Needs[NeedType.Hunger] = 90;
            r.Needs[NeedType.Thirst] = 90;
            for (var i = 0; i < 60; i++)
            {
                HealthRules.ApplyMinute(r, lowest: 0);
            }

            Assert.That(r.Health, Is.EqualTo(80 - (2 * HealthRules.NeglectDrainPerHour) + HealthRules.RecoveryPerHour).Within(0.01));
        }

        [Test]
        public void SaveAndLoad_KeepsHealthAndDeath()
        {
            var sim = FlatLand.CreateNewGame();
            var mabel = Mabel(sim);
            mabel.Health = 63.5;
            var edith = Get(sim, StarterResidents.EdithId);
            sim.Advance(30);
            sim.Die(edith, "old age");

            var result = SaveSerializer.Load(SaveSerializer.Save(sim), FlatLand.CreateWorld());

            Assert.That(result.Success, Is.True, string.Join("; ", result.Errors));
            Assert.That(result.Warnings, Is.Empty, string.Join("; ", result.Warnings));
            var m = Mabel(result.Simulation);
            Assert.That(m.Health, Is.EqualTo(mabel.Health));
            var e = Get(result.Simulation, StarterResidents.EdithId);
            Assert.That(e.IsAlive, Is.False);
            Assert.That(e.DiedAt, Is.EqualTo(edith.DiedAt));
            Assert.That(e.CauseOfDeath, Is.EqualTo("old age"));
        }

        [Test]
        public void DamagedHealth_IsRepaired()
        {
            var sim = FlatLand.CreateNewGame();
            var root = (Dictionary<string, object>)Json.Parse(SaveSerializer.Save(sim));
            var first = ((List<object>)root["residents"]).Cast<Dictionary<string, object>>().First();
            first["health"] = 5000.0;

            var result = SaveSerializer.Load(Json.Write(root), FlatLand.CreateWorld());

            Assert.That(result.Success, Is.True);
            Assert.That(Mabel(result.Simulation).Health, Is.EqualTo(HealthRules.MaxHealth));
        }
    }
}
