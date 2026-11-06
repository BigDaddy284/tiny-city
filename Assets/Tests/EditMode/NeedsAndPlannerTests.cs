using NUnit.Framework;
using TinyCity.Simulation.Activities;
using TinyCity.Simulation.Content;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Time;

namespace TinyCity.Tests.EditMode
{
    public class NeedsAndPlannerTests
    {
        private static readonly Personality Average = new Personality(0.5, 0.5, 0.5, 0.5);

        [Test]
        public void Needs_AreClamped_AndNaNBecomesMiddle()
        {
            var needs = new Needs(50);
            needs[NeedType.Hunger] = 150;
            needs[NeedType.Thirst] = -20;
            needs[NeedType.Fun] = double.NaN;

            Assert.That(needs[NeedType.Hunger], Is.EqualTo(100));
            Assert.That(needs[NeedType.Thirst], Is.EqualTo(0));
            Assert.That(needs[NeedType.Fun], Is.EqualTo(50));
        }

        [Test]
        public void OneHourIdle_DecaysEveryNeed_ByItsHourlyRate()
        {
            var needs = new Needs(80);
            for (var i = 0; i < 60; i++)
            {
                NeedRules.ApplyMinute(needs, Average, null);
            }

            for (var i = 0; i < Needs.Count; i++)
            {
                var need = (NeedType)i;
                Assert.That(needs[need], Is.EqualTo(80 - NeedRules.DecayPerHour(need, Average)).Within(1e-9), need.ToString());
            }
        }

        [Test]
        public void Sleeping_RestoresEnergy_AndSlowsHunger()
        {
            var asleep = new Needs(40);
            var awake = new Needs(40);
            for (var i = 0; i < 60; i++)
            {
                NeedRules.ApplyMinute(asleep, Average, ActivityCatalog.Sleep);
                NeedRules.ApplyMinute(awake, Average, null);
            }

            Assert.That(asleep[NeedType.Energy], Is.EqualTo(52.5).Within(1e-9));
            Assert.That(asleep[NeedType.Hunger], Is.GreaterThan(awake[NeedType.Hunger]));
        }

        [Test]
        public void ActivePeople_NeedExerciseSooner()
        {
            Assert.That(
                NeedRules.DecayPerHour(NeedType.Exercise, new Personality(1, 0.5, 0.5, 0.5)),
                Is.GreaterThan(NeedRules.DecayPerHour(NeedType.Exercise, new Personality(0, 0.5, 0.5, 0.5))));
        }

        [Test]
        public void SameStartingNeeds_GiveIdenticalResults()
        {
            var a = new Needs(70);
            var b = new Needs(70);
            for (var i = 0; i < 1000; i++)
            {
                NeedRules.ApplyMinute(a, Average, i % 3 == 0 ? ActivityCatalog.Chat : null);
                NeedRules.ApplyMinute(b, Average, i % 3 == 0 ? ActivityCatalog.Chat : null);
            }

            for (var i = 0; i < Needs.Count; i++)
            {
                Assert.That(a[(NeedType)i], Is.EqualTo(b[(NeedType)i]));
            }
        }
    }
}
