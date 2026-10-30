using System.Collections.Generic;
using NUnit.Framework;
using System.Linq;
using TinyCity.Simulation;
using TinyCity.Simulation.Configuration;
using TinyCity.Simulation.Content;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Time;

namespace TinyCity.Tests.EditMode
{
    /// <summary>The sums behind the camera panel test: stepping through panel counts, timing frames and suggesting a limit.</summary>
    public class PanelTestTests
    {
        /// <summary>Runs the test with frames that take <paramref name="msFor"/>(panels) milliseconds each.</summary>
        private static PanelTest Run(int largest, System.Func<int, double> msFor)
        {
            var test = new PanelTest(largest);
            var counts = new List<int> { test.PanelCount };
            for (var i = 0; i < 100000 && !test.Finished; i++)
            {
                if (test.AddFrame(msFor(test.PanelCount) / 1000.0) && !test.Finished)
                {
                    counts.Add(test.PanelCount);
                }
            }

            Assert.That(test.Finished, Is.True);
            Assert.That(counts, Is.EqualTo(new List<int>(System.Linq.Enumerable.Range(1, largest))), "one panel more each step");
            return test;
        }

        [Test]
        public void ItStepsFromOnePanelToTheLargest_AndSuggestsTheMostThatStaySmooth()
        {
            // Each panel costs 2 ms: 8 panels take 16 ms a frame (smooth at 60 fps), 9 take 18 ms.
            var test = Run(12, panels => 2.0 * panels);

            Assert.That(test.Results.Count, Is.EqualTo(12));
            Assert.That(test.Results[7].AverageMilliseconds, Is.EqualTo(16.0).Within(0.001));
            Assert.That(test.Results[7].IsSmooth, Is.True);
            Assert.That(test.Results[8].IsSmooth, Is.False);
            Assert.That(test.SuggestedLimit(), Is.EqualTo(8));
            Assert.That(test.Report("test machine"), Does.Contain("Suggested limit: 8 panels."));
        }

        [Test]
        public void TheSettlingFrames_AreNotCounted()
        {
            var test = new PanelTest(1);
            // A slow first second while the panel's pictures are made, then steady 10 ms frames.
            test.AddFrame(1.0);
            while (!test.Finished)
            {
                test.AddFrame(0.010);
            }

            Assert.That(test.Results[0].AverageMilliseconds, Is.EqualTo(10.0).Within(0.001));
        }

        [Test]
        public void OccasionalStutters_MakeACountUnsmooth_EvenWithAGoodAverage()
        {
            var frames = new List<double>();
            for (var i = 0; i < 100; i++)
            {
                frames.Add(i % 10 == 0 ? 60.0 : 8.0);
            }

            var result = PanelTestResult.Of(4, frames);
            Assert.That(result.AverageMilliseconds, Is.LessThan(PanelTest.SmoothMilliseconds));
            Assert.That(result.SlowMilliseconds, Is.EqualTo(60.0));
            Assert.That(result.IsSmooth, Is.False);
        }

        [Test]
        public void EvenWhenNothingIsSmooth_OnePanelIsSuggested()
        {
            Assert.That(Run(3, _ => 50.0).SuggestedLimit(), Is.EqualTo(1));
        }

        [Test]
        public void TheCrowdedTestTown_HasFiftyPeople_WhoLiveTheirLives()
        {
            var sim = StarterTown.CreateCrowdedTown(PanelTest.Largest);

            Assert.That(sim.LivingCount, Is.EqualTo(50));
            Assert.That(sim.Residents.Select(r => r.Id).Distinct().Count(), Is.EqualTo(50));
            Assert.That(sim.Residents.Count(r => !r.IsHomeless), Is.EqualTo(32), "every home taken, the rest on benches");
            Assert.That(StarterTown.CreateCrowdedTown(80).LivingCount, Is.EqualTo(PopulationRules.MaxLiving));

            sim.Advance(GameTime.MinutesPerDay);
            Assert.That(sim.Residents.All(r => r.IsAlive), Is.True);
        }
    }
}
