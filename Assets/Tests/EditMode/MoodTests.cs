using NUnit.Framework;
using TinyTown.Simulation.Activities;
using TinyTown.Simulation.Content;
using TinyTown.Simulation.Health;
using TinyTown.Simulation.Residents;

namespace TinyTown.Tests.EditMode
{
    /// <summary>Phase 6, build 2: moods are read from the simulation's own state and never stored.</summary>
    public class MoodTests
    {
        private static void AllNeeds(Resident r, double level)
        {
            for (var i = 0; i < Needs.Count; i++)
            {
                r.Needs[(NeedType)i] = level;
            }
        }

        [Test]
        public void ContentResident_IsHappy_AndAnOrdinaryOneShowsNothing()
        {
            var sim = StarterTown.CreateNewGame();
            var r = sim.Residents[0];
            AllNeeds(r, 90);
            Assert.That(Moods.Of(sim, r), Is.EqualTo(Mood.Happy));

            r.Needs[NeedType.Fun] = 50;
            Assert.That(Moods.Of(sim, r), Is.EqualTo(Mood.None));
        }

        [TestCase(NeedType.Hunger, Mood.Hungry)]
        [TestCase(NeedType.Thirst, Mood.Thirsty)]
        [TestCase(NeedType.Energy, Mood.Tired)]
        [TestCase(NeedType.Hygiene, Mood.Grubby)]
        [TestCase(NeedType.Social, Mood.Lonely)]
        [TestCase(NeedType.Fun, Mood.Bored)]
        [TestCase(NeedType.Toilet, Mood.NeedsToilet)]
        public void LowNeed_ShowsItsWorry(NeedType need, Mood expected)
        {
            var sim = StarterTown.CreateNewGame();
            var r = sim.Residents[0];
            AllNeeds(r, 60);
            r.Needs[need] = 10;
            Assert.That(Moods.Of(sim, r), Is.EqualTo(expected));
        }

        [Test]
        public void MostPressingWorry_Wins_AndIllnessComesFirst()
        {
            var sim = StarterTown.CreateNewGame();
            var r = sim.Residents[0];
            AllNeeds(r, 10);
            Assert.That(Moods.Of(sim, r), Is.EqualTo(Mood.NeedsToilet));

            r.IllnessId = IllnessCatalog.Cold.Id;
            Assert.That(Moods.Of(sim, r), Is.EqualTo(Mood.Ill));
        }

        [Test]
        public void Sleeping_ShowsAsleep_AndTheDeadShowNothing()
        {
            var sim = StarterTown.CreateNewGame();
            var r = sim.Residents[0];
            r.State = ResidentState.Performing;
            r.ActivityId = ActivityCatalog.Sleep.Id;
            AllNeeds(r, 5);
            Assert.That(Moods.Of(sim, r), Is.EqualTo(Mood.Asleep));

            r.IsAlive = false;
            Assert.That(Moods.Of(sim, r), Is.EqualTo(Mood.None));
            Assert.That(Moods.Of(sim, null), Is.EqualTo(Mood.None));
        }

        [Test]
        public void ADayInTheVillage_EveryoneSleepsAtNight_AndMoodsNeverChangeTheGame()
        {
            var watched = StarterTown.CreateNewGame();
            var control = StarterTown.CreateNewGame();
            var asleep = 0;
            for (var minute = 0; minute < 24 * 60; minute++)
            {
                watched.Tick();
                control.Tick();
                foreach (var r in watched.Residents)
                {
                    if (Moods.Of(watched, r) == Mood.Asleep)
                    {
                        asleep++;
                    }
                }
            }

            Assert.That(asleep, Is.GreaterThan(10 * 6 * 60), "ten residents asleep for hours");
            for (var i = 0; i < watched.Residents.Count; i++)
            {
                Assert.That(watched.Residents[i].Location, Is.EqualTo(control.Residents[i].Location));
                Assert.That(watched.Residents[i].Needs[NeedType.Hunger], Is.EqualTo(control.Residents[i].Needs[NeedType.Hunger]));
            }
        }
    }
}
