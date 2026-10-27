using NUnit.Framework;
using TinyTown.Simulation.Activities;
using TinyTown.Simulation.Content;
using TinyTown.Simulation.Residents;
using TinyTown.Simulation.Time;

namespace TinyTown.Tests.EditMode
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
                NeedRules.ApplyMinute(a, Average, i % 3 == 0 ? ActivityCatalog.Read : null);
                NeedRules.ApplyMinute(b, Average, i % 3 == 0 ? ActivityCatalog.Read : null);
            }

            for (var i = 0; i < Needs.Count; i++)
            {
                Assert.That(a[(NeedType)i], Is.EqualTo(b[(NeedType)i]));
            }
        }

        [TestCase(NeedType.Toilet, "toilet")]
        [TestCase(NeedType.Thirst, "drink")]
        [TestCase(NeedType.Hunger, "eat")]
        [TestCase(NeedType.Energy, "sleep")]
        public void UrgentNeed_BeatsWork(NeedType need, string expectedActivity)
        {
            var sim = TestTown.WithResident(TestTown.WorkHour, adjust: n => n[need] = 5);

            var choice = sim.Planner.Choose(sim.Residents[0], sim.Time);

            Assert.That(choice.Activity.Id, Is.EqualTo(expectedActivity));
        }

        [Test]
        public void TheMostUrgentNeedWins()
        {
            var sim = TestTown.WithResident(TestTown.WeekendAfternoon, adjust: n =>
            {
                n[NeedType.Hunger] = 15;
                n[NeedType.Toilet] = 8;
            });

            Assert.That(sim.Planner.Choose(sim.Residents[0], sim.Time).Activity, Is.SameAs(ActivityCatalog.UseToilet));
        }

        [Test]
        public void DuringShift_WithNeedsMet_GoesToWork()
        {
            var sim = TestTown.WithResident(TestTown.WorkHour);

            var choice = sim.Planner.Choose(sim.Residents[0], sim.Time);

            Assert.That(choice.Activity, Is.SameAs(ActivityCatalog.Work));
            Assert.That(choice.Target.Id, Is.EqualTo(StarterTown.GardenPlotId));
        }

        [Test]
        public void OnWeekends_DoesNotWork()
        {
            var sim = TestTown.WithResident(TestTown.WeekendAfternoon, needsLevel: 50);

            Assert.That(sim.Planner.Choose(sim.Residents[0], sim.Time).Activity, Is.Not.SameAs(ActivityCatalog.Work));
        }

        [Test]
        public void AtBedtime_GoesToOwnBed()
        {
            var sim = TestTown.WithResident(GameTime.FromDayAndTime(2, 23, 0));

            var choice = sim.Planner.Choose(sim.Residents[0], sim.Time);

            Assert.That(choice.Activity, Is.SameAs(ActivityCatalog.Sleep));
            Assert.That(choice.Target.Id, Is.EqualTo(StarterTown.BedId));
        }

        [Test]
        public void LowHunger_InFreeTime_Eats()
        {
            var sim = TestTown.WithResident(TestTown.WeekendAfternoon, adjust: n => n[NeedType.Hunger] = 40);

            Assert.That(sim.Planner.Choose(sim.Residents[0], sim.Time).Activity, Is.SameAs(ActivityCatalog.Eat));
        }

        [Test]
        public void Personality_ChangesLeisureChoice()
        {
            var bookish = TestTown.WithResident(TestTown.WeekendAfternoon, needsLevel: 70, personality: new Personality(0, 0, 0.5, 1));
            var sporty = TestTown.WithResident(TestTown.WeekendAfternoon, needsLevel: 70, personality: new Personality(1, 0, 0.5, 0));

            // Hobbies are favourites whatever the personality; leave them out to see personality alone.
            bookish.Residents[0].SetHobbies(null);
            sporty.Residents[0].SetHobbies(null);

            Assert.That(bookish.Planner.Choose(bookish.Residents[0], bookish.Time).Activity, Is.SameAs(ActivityCatalog.Read));
            Assert.That(sporty.Planner.Choose(sporty.Residents[0], sporty.Time).Activity, Is.SameAs(ActivityCatalog.Exercise));
        }

        [Test]
        public void WhenEverythingIsFull_TakesItEasy()
        {
            var sim = TestTown.WithResident(TestTown.WeekendAfternoon, needsLevel: 100);

            Assert.That(sim.Planner.Choose(sim.Residents[0], sim.Time), Is.Null);
        }

        [Test]
        public void UrgentNeed_InterruptsWork_ButWorkDoesNotInterruptUrgentCare()
        {
            var sim = TestTown.WithResident(TestTown.WorkHour);
            var r = sim.Residents[0];

            r.Needs[NeedType.Thirst] = 10;
            Assert.That(sim.Planner.ShouldInterrupt(r, ActivityCatalog.Work, sim.Time), Is.True);
            Assert.That(sim.Planner.ShouldInterrupt(r, ActivityCatalog.Drink, sim.Time), Is.False);
        }

        [Test]
        public void ShiftStart_InterruptsLeisure()
        {
            var sim = TestTown.WithResident(TestTown.WorkHour);

            Assert.That(sim.Planner.ShouldInterrupt(sim.Residents[0], ActivityCatalog.Read, sim.Time), Is.True);
        }
    }
}
