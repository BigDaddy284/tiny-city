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

        private static Resident Mabel(TownSimulation sim) => sim.GetResident(StarterTown.ResidentId);

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
        public void TheDead_NoLongerLive_PayRent_OrGetChattedTo()
        {
            var sim = StarterTown.CreateNewGame();
            var edith = Get(sim, StarterTown.EdithId);
            sim.Die(edith, "old age");
            var money = edith.MoneyPence;
            var needs = edith.Needs[NeedType.Hunger];
            var location = edith.Location;

            sim.Advance(8 * GameTime.MinutesPerDay);

            Assert.That(edith.MoneyPence, Is.EqualTo(money));
            Assert.That(edith.Needs[NeedType.Hunger], Is.EqualTo(needs));
            Assert.That(edith.Location, Is.EqualTo(location));
            Assert.That(sim.Conversations.All.Any(c => c.Participants.Contains(edith.Id) && c.Started.TotalMinutes > edith.DiedAt.Value.TotalMinutes), Is.False);
            Assert.That(sim.ApplyDecision(edith.Id, 1, "relax", "none", "x", null), Is.EqualTo(DecisionOutcome.NotAlive));
        }

        [Test]
        public void TheDead_MakeNoAiRequests()
        {
            var transport = FakeAiTransport.WorkingOllama(_ => "{\"action\":\"relax\",\"target_id\":\"none\",\"reason\":\"x\",\"say\":\"\"}");
            var service = new AiService(new TinyCity.Simulation.Configuration.OllamaSettings(), transport);
            var sim = StarterTown.CreateNewGame();
            foreach (var r in sim.Residents.Where(r => r.Id != StarterTown.ResidentId).ToList())
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
                HealthRules.ApplyMinute(r, resting: false, lowest: 0);
            }

            Assert.That(r.Health, Is.EqualTo(80 - (2 * HealthRules.NeglectDrainPerHour)).Within(0.01));

            r.Needs[NeedType.Hunger] = 90;
            r.Needs[NeedType.Thirst] = 90;
            for (var i = 0; i < 60; i++)
            {
                HealthRules.ApplyMinute(r, resting: true, lowest: 0);
            }

            Assert.That(r.Health, Is.EqualTo(80 - (2 * HealthRules.NeglectDrainPerHour) + HealthRules.RestingRecoveryPerHour).Within(0.01));
        }

        [Test]
        public void SaveAndLoad_KeepsHealthAndDeath()
        {
            var sim = StarterTown.CreateNewGame();
            var mabel = Mabel(sim);
            mabel.Health = 63.5;
            var edith = Get(sim, StarterTown.EdithId);
            sim.Advance(30);
            sim.Die(edith, "old age");

            var result = SaveSerializer.Load(SaveSerializer.Save(sim), StarterTown.CreateWorld());

            Assert.That(result.Success, Is.True, string.Join("; ", result.Errors));
            Assert.That(result.Warnings, Is.Empty, string.Join("; ", result.Warnings));
            var m = Mabel(result.Simulation);
            Assert.That(m.Health, Is.EqualTo(mabel.Health));
            var e = Get(result.Simulation, StarterTown.EdithId);
            Assert.That(e.IsAlive, Is.False);
            Assert.That(e.DiedAt, Is.EqualTo(edith.DiedAt));
            Assert.That(e.CauseOfDeath, Is.EqualTo("old age"));
        }

        [Test]
        public void DamagedHealth_IsRepaired()
        {
            var sim = StarterTown.CreateNewGame();
            var root = (Dictionary<string, object>)Json.Parse(SaveSerializer.Save(sim));
            var first = ((List<object>)root["residents"]).Cast<Dictionary<string, object>>().First();
            first["health"] = 5000.0;

            var result = SaveSerializer.Load(Json.Write(root), StarterTown.CreateWorld());

            Assert.That(result.Success, Is.True);
            Assert.That(Mabel(result.Simulation).Health, Is.EqualTo(HealthRules.MaxHealth));
        }

        [Test]
        public void TwelveWeeksInTheTown_SavesStayValid_AndTheLivingAreNeverStuck()
        {
            var sim = StarterTown.CreateNewGame();
            var lowSince = new Dictionary<(string, NeedType), long>();
            var worstStretch = 0L;
            var worst = string.Empty;
            for (var day = 1; day <= 84; day++)
            {
                for (var minute = 0; minute < GameTime.MinutesPerDay; minute++)
                {
                    sim.Tick();
                    foreach (var r in sim.Residents.Where(r => r.IsAlive))
                    {
                        foreach (var need in new[] { NeedType.Hunger, NeedType.Thirst, NeedType.Toilet })
                        {
                            var key = (r.Id, need);
                            if (r.Needs[need] > 5)
                            {
                                lowSince.Remove(key);
                                continue;
                            }

                            if (!lowSince.ContainsKey(key))
                            {
                                lowSince[key] = sim.Time.TotalMinutes;
                            }

                            var stretch = sim.Time.TotalMinutes - lowSince[key];
                            if (stretch > worstStretch)
                            {
                                worstStretch = stretch;
                                worst = $"{r.FirstName} {need} at {sim.Time}";
                            }
                        }
                    }
                }

                if (day % 14 == 0)
                {
                    var loaded = SaveSerializer.Load(SaveSerializer.Save(sim), StarterTown.CreateWorld());
                    Assert.That(loaded.Success, Is.True, $"day {day}: {string.Join("; ", loaded.Errors)}");
                    Assert.That(loaded.Warnings, Is.Empty, $"day {day}: {string.Join("; ", loaded.Warnings)}");
                    Assert.That(SaveSerializer.Save(loaded.Simulation), Is.EqualTo(SaveSerializer.Save(sim)), $"day {day}: the save round-trips exactly");
                }
            }

            Assert.That(worstStretch, Is.LessThan(3 * 60), $"a critical need stayed near zero too long: {worst}");
            Assert.That(sim.Residents.Count, Is.EqualTo(15), "nobody is ever removed from the records");
            Assert.That(sim.Residents.Where(r => r.IsAlive).All(r => r.Health > 0), Is.True);
            Assert.That(sim.Residents.Where(r => !r.IsAlive).All(r => r.DiedAt != null && r.CauseOfDeath.Length > 0), Is.True);
        }
    }
}
