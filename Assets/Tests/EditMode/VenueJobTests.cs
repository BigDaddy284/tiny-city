using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinyCity.Simulation;
using TinyCity.Simulation.Activities;
using TinyCity.Simulation.Content;
using TinyCity.Simulation.History;
using TinyCity.Simulation.Persistence;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Time;
using TinyCity.Simulation.World;

namespace TinyCity.Tests.EditMode
{
    /// <summary>The venues' jobs: jobseekers take them, the venue then opens only while its worker is in, and it saves.</summary>
    public class VenueJobTests
    {
        /// <summary>A newcomer moves in and is put on jobseeker's allowance (or, with <paramref name="remote"/>, works remotely).</summary>
        private static Resident Newcomer(TownSimulation sim, bool remote = false)
        {
            var r = sim.MoveInNewcomer();
            Assert.That(r, Is.Not.Null);
            r.WeeklyIncomePence = remote ? Newcomers.RemoteWorkIncome : Newcomers.AllowanceIncome;
            r.IncomeLabel = remote ? Newcomers.RemoteWorkLabel : Newcomers.AllowanceLabel;
            return r;
        }

        /// <summary>A new city whose venue staff have all given up their jobs, so the four jobs are free.</summary>
        private static TownSimulation CityWithFreeJobs()
        {
            var sim = StarterTown.CreateNewGame();
            foreach (var r in sim.Residents.Where(r => r.Job != null && StarterTown.VenueJobs.Any(j => j.WorkObjectId == r.Job.WorkObjectId)))
            {
                r.Job = null;
            }

            Assert.That(sim.Vacancies().Count(), Is.EqualTo(4));
            return sim;
        }

        /// <summary>Runs on to the next morning's hiring.</summary>
        private static void ToNextHiring(TownSimulation sim)
        {
            var minutes = (int)(((PopulationRules.HousingMinute - sim.Time.MinuteOfDay) + GameTime.MinutesPerDay) % GameTime.MinutesPerDay);
            sim.Advance(minutes == 0 ? GameTime.MinutesPerDay : minutes + 1);
        }

        [Test]
        public void TheCity_HasFourVenueJobs_EachAtItsVenuesCounter_WithTheVenuesHours()
        {
            var world = StarterTown.CreateWorld();
            Assert.That(world.JobOpenings.Count, Is.EqualTo(4));
            foreach (var job in world.JobOpenings)
            {
                Assert.That(world.TryGetObject(job.WorkObjectId, out var counter), Is.True, job.Title);
                var business = world.BusinessOnMap(counter.Location.MapId);
                Assert.That(business, Is.Not.Null, job.Title);
                Assert.That(business.OpenMinuteOfDay, Is.EqualTo(job.StartMinuteOfDay), business.Name);
                Assert.That(business.CloseMinuteOfDay, Is.EqualTo(job.EndMinuteOfDay), business.Name);
                Assert.That(business.OpenDays, Is.EqualTo(job.WorkDays), business.Name);
                Assert.That(job.HourlyWagePence, Is.GreaterThan(0), job.Title);
            }
        }

        [Test]
        public void ANewCity_StartsWithEveryVenueJobTaken()
        {
            var sim = StarterTown.CreateNewGame();
            Assert.That(sim.Vacancies().Any(), Is.False);
            foreach (var job in StarterTown.VenueJobs)
            {
                Assert.That(sim.Residents.Count(r => r.Job?.WorkObjectId == job.WorkObjectId), Is.EqualTo(1), job.Title);
            }

            var seeker = Newcomer(sim);
            ToNextHiring(sim);
            Assert.That(seeker.Job, Is.Null, "no job is free");
        }

        [Test]
        public void AJobseeker_TakesAVenueJob_TheNextMorning_AndTheAllowanceStops()
        {
            var sim = CityWithFreeJobs();
            var r = Newcomer(sim);
            Assert.That(TownSimulation.IsJobseeker(r), Is.True);

            ToNextHiring(sim);

            Assert.That(r.Job, Is.Not.Null);
            Assert.That(StarterTown.VenueJobs.Select(j => j.WorkObjectId).Contains(r.Job.WorkObjectId), Is.True);
            Assert.That(r.WeeklyIncomePence, Is.EqualTo(0));
            Assert.That(r.IncomeLabel, Is.Empty);
            Assert.That(TownSimulation.IsJobseeker(r), Is.False);
            Assert.That(sim.History.Events.Any(e => e.Kind == TownEventKind.NewJob && e.ResidentIds.Contains(r.Id)), Is.True);
            Assert.That(Journal.IsNotable(TownEventKind.NewJob), Is.True);
        }

        [Test]
        public void RemoteWorkersAndChildren_DontTakeJobs()
        {
            var sim = CityWithFreeJobs();
            var remote = Newcomer(sim, remote: true);
            var young = Newcomer(sim);
            young.AgeYears = 12;

            ToNextHiring(sim);

            Assert.That(remote.Job, Is.Null);
            Assert.That(remote.IncomeLabel, Is.EqualTo(Newcomers.RemoteWorkLabel));
            Assert.That(young.Job, Is.Null);
        }

        [Test]
        public void EachJob_GoesToOnePerson_AndComesFreeWhenTheyDie()
        {
            var sim = CityWithFreeJobs();
            var seekers = Enumerable.Range(0, 5).Select(_ => Newcomer(sim)).ToList();

            ToNextHiring(sim);

            var working = seekers.Where(r => r.Job != null).ToList();
            Assert.That(working.Count, Is.EqualTo(4));
            Assert.That(working.Select(r => r.Job.WorkObjectId).Distinct().Count(), Is.EqualTo(4));
            Assert.That(sim.Vacancies().Any(), Is.False);
            var waiting = seekers.Single(r => r.Job == null);
            Assert.That(waiting.IncomeLabel, Is.EqualTo(Newcomers.AllowanceLabel), "still on the allowance");

            var leaver = working[0];
            sim.Die(leaver, "an accident");
            Assert.That(sim.Vacancies().Single().WorkObjectId, Is.EqualTo(leaver.Job.WorkObjectId));

            ToNextHiring(sim);

            Assert.That(waiting.Job, Is.Not.Null);
            Assert.That(waiting.Job.WorkObjectId, Is.EqualTo(leaver.Job.WorkObjectId));
        }

        [Test]
        public void TheVenue_OpensOnlyWhileItsWorkerIsIn_AndKeepsToTheClockWithNobodyEmployed()
        {
            var sim = StarterTown.CreateNewGame();
            var gym = sim.World.Businesses.Single(b => b.Id == StarterTown.GymId);
            var opening = new GameTime(GameTime.MinutesPerDay + gym.OpenMinuteOfDay + 1);
            Assert.That(gym.IsOpen(opening), Is.True, "the test needs the gym's opening time on a working day");
            var marcus = sim.GetResident(StarterTown.MarcusId);
            Assert.That(marcus.Job.WorkObjectId, Is.EqualTo("obj-gym-desk"));
            sim.World.TryGetObject(marcus.BedObjectId, out var bed);
            marcus.Location = bed.Location;

            Assert.That(sim.Economy.IsOpen(gym, opening), Is.False, "its instructor isn't in");

            marcus.Job = null;
            Assert.That(sim.Economy.IsOpen(gym, opening), Is.True, "nobody works there now, so it keeps to its hours");
        }

        [Test]
        public void AVenueJob_IsKeptBySaveAndLoad()
        {
            var sim = CityWithFreeJobs();
            var r = Newcomer(sim);
            ToNextHiring(sim);
            var job = r.Job;
            Assert.That(job, Is.Not.Null);

            var loaded = SaveSerializer.Load(SaveSerializer.Save(sim), StarterTown.CreateWorld()).Simulation;
            var again = loaded.GetResident(r.Id);

            Assert.That(again.Job.Title, Is.EqualTo(job.Title));
            Assert.That(again.Job.WorkObjectId, Is.EqualTo(job.WorkObjectId));
            Assert.That(again.Job.WorkDays, Is.EqualTo(job.WorkDays));
            Assert.That(again.Job.HourlyWagePence, Is.EqualTo(job.HourlyWagePence));
            Assert.That(loaded.Vacancies().Count(), Is.EqualTo(3));
            Assert.That(SaveSerializer.Save(loaded), Is.EqualTo(SaveSerializer.Save(sim)));
        }

        [Test]
        public void AnOpeningMustBeAtAnObjectOfItsOwn()
        {
            var world = StarterTown.CreateWorld();
            var bad = new Job("Nowhere", "obj-no-such-thing", 60, 120, Job.EveryDay, 100);
            Assert.Throws<ArgumentException>(() => new TownWorld("t", "T", world.Maps, world.Objects, world.Portals, null, null, new[] { bad }));
            var twice = StarterTown.VenueJobs[0];
            Assert.Throws<ArgumentException>(() => new TownWorld("t", "T", world.Maps, world.Objects, world.Portals, null, null, new[] { twice, twice }));
        }
    }
}
