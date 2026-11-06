using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinyCity.Simulation;
using TinyCity.Simulation.AI;
using TinyCity.Simulation.Configuration;
using TinyCity.Simulation.History;
using TinyCity.Simulation.Memory;
using TinyCity.Simulation.Persistence;

namespace TinyCity.Tests.EditMode
{
    /// <summary>Tiny City 2: once enough has happened, residents draw insights from their memories, citing them.</summary>
    public class InsightTests
    {
        private static TownSimulation TownWithMemories(int count, int importance)
        {
            var sim = TestTown.WithResident(TestTown.WeekendAfternoon);
            var r = sim.Residents[0];
            foreach (var m in sim.Memories.ForResident(r.Id).ToList())
            {
                sim.Memories.RateImportance(m.Id, 1);
            }

            for (var i = 0; i < count; i++)
            {
                sim.Memories.Add(r.Id, sim.Time, MemoryType.Event, importance, $"Something happened, number {i}");
            }

            return sim;
        }

        [Test]
        public void Due_OnlyOnceImportanceAddsUp()
        {
            var quiet = TownWithMemories(0, 1);
            Assert.That(quiet.IsInsightDue(quiet.Residents[0]), Is.False);

            var busy = TownWithMemories(10, 7);
            Assert.That(busy.IsInsightDue(busy.Residents[0]), Is.True);
            Assert.That(busy.InsightEvidence(busy.Residents[0].Id).Count, Is.LessThanOrEqualTo(TownSimulation.InsightEvidenceCount));
        }

        [Test]
        public void ApplyInsights_KeepsCitedOnes_AndResetsTheCount()
        {
            var sim = TownWithMemories(10, 7);
            var r = sim.Residents[0];
            var evidence = sim.InsightEvidence(r.Id);
            var ids = evidence.Select(m => m.Id).ToList();

            var outcome = sim.ApplyInsights(r.Id, ids, new[]
            {
                new InsightProposal("Busy days make me happy", new[] { ids[0], ids[1] }),
                new InsightProposal("No evidence here", new string[0]),
                new InsightProposal("Cites something not offered", new[] { "mem-999999" }),
            });

            Assert.That(outcome, Is.EqualTo(DecisionOutcome.Applied));
            var insights = sim.Memories.ForResident(r.Id).Where(m => m.Type == MemoryType.Insight).ToList();
            Assert.That(insights.Count, Is.EqualTo(1));
            Assert.That(insights[0].Tags.Contains(ids[0]) && insights[0].Tags.Contains("insight"), Is.True);
            Assert.That(sim.IsInsightDue(r), Is.False);
            Assert.That(sim.ApplyInsights(r.Id, ids, new[] { new InsightProposal("Again", new[] { ids[0] }) }), Is.EqualTo(DecisionOutcome.Stale));
        }

        [Test]
        public void ApplyInsights_WithNothingUsable_IsInvalid()
        {
            var sim = TownWithMemories(10, 7);
            var ids = sim.InsightEvidence(sim.Residents[0].Id).Select(m => m.Id).ToList();
            Assert.That(sim.ApplyInsights(sim.Residents[0].Id, ids, new[] { new InsightProposal("  ", new[] { ids[0] }) }), Is.EqualTo(DecisionOutcome.InvalidText));
        }

        [Test]
        public void Prompt_NumbersMemories_AndParseMapsNumbersBack()
        {
            var sim = TownWithMemories(3, 7);
            var evidence = sim.InsightEvidence(sim.Residents[0].Id);
            Assert.That(InsightPrompt.UserText(evidence), Does.Contain("1. Day 6: Something happened, number 0"));

            const string content = "{\"insights\":[{\"insight\":\"I keep busy\",\"because\":[1,3,9]},{\"insight\":\"\",\"because\":[1]},{\"insight\":\"Unbacked\",\"because\":[]}]}";
            Assert.That(InsightPrompt.TryParse(content, evidence, out var insights, out _), Is.True);
            Assert.That(insights.Count, Is.EqualTo(1));
            Assert.That(insights[0].EvidenceIds, Is.EqualTo(new List<string> { evidence[0].Id, evidence[2].Id }));
            Assert.That(InsightPrompt.TryParse("{\"insights\":[]}", evidence, out _, out _), Is.False);
            Assert.That(InsightPrompt.TryParse("nope", evidence, out _, out _), Is.False);
        }

        [Test]
        public void Insights_GoIntoTomorrowsPlan_TheTimeline_AndSurviveASave()
        {
            var sim = TownWithMemories(10, 7);
            var r = sim.Residents[0];
            var ids = sim.InsightEvidence(r.Id).Select(m => m.Id).ToList();
            sim.ApplyInsights(r.Id, ids, new[] { new InsightProposal("I should rest more", new[] { ids[0] }) });

            Assert.That(DayPlanPrompt.UserText(sim, r, 1), Does.Contain("Things you've realised lately: I should rest more"));
            Assert.That(LifeStory.Timeline(sim, r.Id).Any(e => e.Source == TimelineSource.AiReflection && e.Text == "I should rest more"), Is.True);

            var loaded = SaveSerializer.Load(SaveSerializer.Save(sim), sim.World);
            Assert.That(loaded.Success, Is.True);
            Assert.That(loaded.Simulation.Memories.ForResident(r.Id).Any(m => m.Type == MemoryType.Insight && m.Summary == "I should rest more"), Is.True);
        }

        [Test]
        public void AiService_AsksForInsights_WhenDue()
        {
            var sim = TownWithMemories(10, 7);
            var transport = FakeAiTransport.WorkingOllama(body => body.Contains("What do they tell you") ? "{\"insights\":[{\"insight\":\"Life is full\",\"because\":[1,2]}]}" : null);
            var ai = new AiService(new OllamaSettings(), transport);
            ai.Attach(sim);
            for (var i = 0; i < 40 && sim.LatestInsight(sim.Residents[0].Id) == null; i++)
            {
                ai.Update(i * 0.1);
            }

            Assert.That(sim.LatestInsight(sim.Residents[0].Id)?.Summary, Is.EqualTo("Life is full"));
        }
    }
}
