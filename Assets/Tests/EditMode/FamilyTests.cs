using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinyCity.Art;
using TinyCity.Simulation;
using TinyCity.Simulation.Activities;
using TinyCity.Simulation.Content;
using TinyCity.Simulation.History;
using TinyCity.Simulation.Persistence;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Social;
using TinyCity.Simulation.Time;
using TinyCity.Simulation.World;

namespace TinyCity.Tests.EditMode
{
    /// <summary>Babies, children growing up, moving out, and the limits on all of it.</summary>
    public class FamilyTests
    {
        /// <summary>A city where Mabel and Tom are a committed couple.</summary>
        private static TownSimulation CityWithACouple(out Resident mabel, out Resident tom)
        {
            var sim = StarterTown.CreateNewGame();
            sim.BabiesEnabled = true;
            mabel = sim.GetResident(StarterTown.ResidentId);
            tom = sim.GetResident(StarterTown.TomId);
            foreach (var (a, b) in new[] { (mabel, tom), (tom, mabel) })
            {
                sim.Relationships.Get(a.Id, b.Id).Change(friendship: 90, trust: 90, respect: 80, romance: 95);
            }

            sim.Relationships.StartPartnership(mabel.Id, tom.Id, sim.Time).Stage = RomanceStage.Committed;
            return sim;
        }

        [Test]
        public void ABaby_IsBornToACouple_AndCarriedAboutByAParent()
        {
            var sim = CityWithACouple(out var mabel, out var tom);
            var baby = sim.HaveBaby(mabel, tom);

            Assert.That(baby, Is.Not.Null);
            Assert.That(baby.Id, Is.EqualTo("res-0011"));
            Assert.That(baby.AgeYears, Is.EqualTo(0));
            Assert.That(baby.IsBaby, Is.True);
            Assert.That(baby.LifeStage, Is.EqualTo(LifeStage.Baby));
            Assert.That(baby.LastName, Is.EqualTo(mabel.LastName));
            Assert.That(baby.HomeMapId, Is.EqualTo(mabel.HomeMapId));
            Assert.That(baby.ParentIds, Is.EquivalentTo(new[] { mabel.Id, tom.Id }));
            Assert.That(baby.BornDay, Is.EqualTo(sim.Time.Day));
            Assert.That(sim.History.Events.Last().Kind, Is.EqualTo(TownEventKind.Born));
            Assert.That(Journal.IsNotable(TownEventKind.Born), Is.True);
            Assert.That(sim.CanHaveBaby(mabel, tom), Is.False, "not while they have a baby");

            for (var minute = 0; minute < GameTime.MinutesPerDay; minute++)
            {
                sim.Tick();
                Assert.That(baby.Location, Is.EqualTo(mabel.Location), "carried by Mabel, who lives with her");
                Assert.That(baby.ActivityId, Is.Null);
                Assert.That(SocialRules.CanBeApproached(baby, sim.Time, true), Is.False, "babies don't chat");
            }

            foreach (NeedType need in System.Enum.GetValues(typeof(NeedType)))
            {
                Assert.That(baby.Needs[need], Is.GreaterThanOrEqualTo(80), "looked after: " + need);
            }
        }

        [Test]
        public void Children_AgeTwiceAsFastAsAdults_AndWalkFromThree()
        {
            var sim = CityWithACouple(out var mabel, out var tom);
            var baby = sim.HaveBaby(mabel, tom);
            sim.Advance(PopulationRules.ChildDaysPerYear * GameTime.MinutesPerDay);
            Assert.That(baby.AgeYears, Is.EqualTo(1));

            sim.Advance(2 * PopulationRules.ChildDaysPerYear * GameTime.MinutesPerDay);
            Assert.That(baby.AgeYears, Is.EqualTo(PopulationRules.WalkingAge));
            Assert.That(baby.IsBaby, Is.False);
            Assert.That(baby.LifeStage, Is.EqualTo(LifeStage.Child));

            var activities = new HashSet<string>();
            var homeAtNight = 0;
            for (var minute = 0; minute < 2 * GameTime.MinutesPerDay; minute++)
            {
                sim.Tick();
                if (baby.ActivityId != null)
                {
                    activities.Add(baby.ActivityId);
                }

                if (sim.Time.Hour == 3 && sim.Time.Minute == 0 && baby.Location.MapId == baby.HomeMapId && baby.ActivityId == ActivityCatalog.Sleep.Id)
                {
                    homeAtNight++;
                }
            }

            Assert.That(activities.Count, Is.GreaterThan(2), "a day of their own: " + string.Join(", ", activities));
            Assert.That(activities.Contains(ActivityCatalog.Work.Id), Is.False);
            Assert.That(homeAtNight, Is.EqualTo(2), "asleep at home, sharing the bed");
            Assert.That(SleepSchedule.BedtimeOf(baby), Is.LessThan(SleepSchedule.BedtimeOf(mabel)), "an earlier bedtime");
        }

        [Test]
        public void AtEighteen_AChildMovesIntoAHomeOfTheirOwn()
        {
            var sim = CityWithACouple(out var mabel, out var tom);
            var child = sim.HaveBaby(mabel, tom);
            child.AgeYears = PopulationRules.AdultAge - 1;
            sim.Advance(PopulationRules.ChildDaysPerYear * GameTime.MinutesPerDay);

            Assert.That(child.AgeYears, Is.EqualTo(PopulationRules.AdultAge));
            Assert.That(child.IsMinor, Is.False);
            Assert.That(child.HomeMapId, Is.Not.EqualTo(mabel.HomeMapId));
            Assert.That(child.IsHomeless, Is.False);
            Assert.That(child.WeeklyRentPence, Is.GreaterThan(0));
            Assert.That(child.WeeklyIncomePence, Is.GreaterThan(0));
            Assert.That(sim.History.Events.Any(e => e.Kind == TownEventKind.MovedOut && e.ResidentIds.Contains(child.Id)), Is.True);

            // From now on, birthdays come once a year like everyone else's.
            sim.Advance(7 * GameTime.MinutesPerDay);
            Assert.That(child.AgeYears, Is.EqualTo(PopulationRules.AdultAge));
        }

        [Test]
        public void AtEighteen_WithNoFreeHome_TheyGoToAParkBench()
        {
            var sim = CityWithACouple(out var mabel, out var tom);
            var child = sim.HaveBaby(mabel, tom);
            var lodgers = 0;
            foreach (var home in sim.FreeHomes().ToList())
            {
                sim.AddResident(new Resident($"res-9{lodgers++:000}", "Lodger", "Lodger", 40, 100, "#888888", null, home.Id, null, null, mabel.Location));
            }

            child.AgeYears = PopulationRules.AdultAge - 1;
            sim.Advance(PopulationRules.ChildDaysPerYear * GameTime.MinutesPerDay);

            Assert.That(child.IsHomeless, Is.True);
            Assert.That(sim.History.Events.Any(e => e.Kind == TownEventKind.Homeless && e.ResidentIds.Contains(child.Id)), Is.True);
        }

        [Test]
        public void NoBabyIsBorn_WhileFiftyPeopleAreAlive()
        {
            var sim = CityWithACouple(out var mabel, out var tom);
            var lodgers = new List<Resident>();
            while (sim.LivingCount < PopulationRules.MaxLiving)
            {
                var lodger = new Resident($"res-9{lodgers.Count:000}", "Lodger", "Lodger", 40, 100, "#888888", null, StarterTown.VillageMapId, null, null, new Location(StarterTown.VillageMapId, new GridPos(41, 38)));
                sim.AddResident(lodger);
                lodgers.Add(lodger);
            }

            Assert.That(sim.CanHaveBaby(mabel, tom), Is.False);
            Assert.That(sim.HaveBaby(mabel, tom), Is.Null);

            sim.Die(lodgers[0], "old age");
            Assert.That(sim.HaveBaby(mabel, tom), Is.Not.Null, "someone died, so there's room");
            Assert.That(sim.LivingCount, Is.EqualTo(PopulationRules.MaxLiving));
        }

        [Test]
        public void ACouple_HasNoMoreThanThreeChildren_AndNotPastFifty()
        {
            var sim = CityWithACouple(out var mabel, out var tom);
            for (var i = 0; i < PopulationRules.MaxChildrenPerCouple; i++)
            {
                var child = sim.HaveBaby(mabel, tom);
                Assert.That(child, Is.Not.Null);
                child.AgeYears = PopulationRules.WalkingAge;
            }

            Assert.That(sim.CanHaveBaby(mabel, tom), Is.False);

            var other = CityWithACouple(out var m2, out var t2);
            m2.AgeYears = PopulationRules.MaxParentAge + 1;
            Assert.That(other.CanHaveBaby(m2, t2), Is.False);
        }

        [Test]
        public void Family_NeverFallForEachOther_AndChildrenStayOutOfTheBar()
        {
            var sim = CityWithACouple(out var mabel, out var tom);
            var first = sim.HaveBaby(mabel, tom);
            first.AgeYears = PopulationRules.WalkingAge;
            var second = sim.HaveBaby(mabel, tom);
            Assert.That(first.IsFamilyOf(second), Is.True);
            Assert.That(first.IsFamilyOf(mabel), Is.True);
            Assert.That(mabel.IsFamilyOf(first), Is.True);
            Assert.That(first.IsFamilyOf(sim.GetResident(StarterTown.ArthurId)), Is.False);

            first.AgeYears = 25;
            second.AgeYears = 24;
            first.OpenToRomance = second.OpenToRomance = true;
            Assert.That(RomanceRules.Compatible(first, second), Is.False, "brother and sister");

            second.AgeYears = 10;
            sim.World.TryGetObject("obj-bar-table-1", out var barTable);
            Assert.That(sim.Planner.IsAvailableTo(second, barTable), Is.False, "the bar is for adults");
            Assert.That(sim.Planner.IsAvailableTo(mabel, barTable), Is.True);
            Assert.That(RomanceRules.Compatible(second, sim.GetResident(StarterTown.FelixId)), Is.False, "children aren't open to romance");
        }

        [Test]
        public void Births_HappenOnTheirOwn_WhenTurnedOn()
        {
            var sim = CityWithACouple(out var mabel, out var tom);
            var off = CityWithACouple(out _, out _);
            off.BabiesEnabled = false;
            sim.Advance(40 * GameTime.MinutesPerDay);
            off.Advance(40 * GameTime.MinutesPerDay);

            Assert.That(sim.Residents.Any(r => r.BornDay > 0), Is.True);
            Assert.That(off.Residents.Any(r => r.BornDay > 0), Is.False);
        }

        [Test]
        public void Families_SaveAndLoad_AndCarryOnExactly()
        {
            var sim = CityWithACouple(out var mabel, out var tom);
            var reference = CityWithACouple(out var m2, out var t2);

            // The babies here are made by hand, so none arrives by chance in between.
            sim.BabiesEnabled = reference.BabiesEnabled = false;
            var walker = sim.HaveBaby(mabel, tom);
            walker.AgeYears = PopulationRules.WalkingAge;
            reference.HaveBaby(m2, t2).AgeYears = PopulationRules.WalkingAge;
            sim.Advance(9 * GameTime.MinutesPerDay + 600);
            reference.Advance(9 * GameTime.MinutesPerDay + 600);
            var baby = sim.HaveBaby(mabel, tom);
            reference.HaveBaby(m2, t2);
            Assert.That(walker.IsBaby, Is.False);
            Assert.That(baby, Is.Not.Null);

            var loaded = SaveSerializer.Load(SaveSerializer.Save(sim), StarterTown.CreateWorld());
            Assert.That(loaded.Success, Is.True, string.Join("; ", loaded.Errors));
            Assert.That(loaded.Warnings, Is.Empty);
            var again = loaded.Simulation;
            Assert.That(again.GetResident(baby.Id).ParentIds, Is.EquivalentTo(new[] { mabel.Id, tom.Id }));
            Assert.That(again.GetResident(baby.Id).BornDay, Is.EqualTo(baby.BornDay));

            again.Advance(4 * GameTime.MinutesPerDay);
            reference.Advance(4 * GameTime.MinutesPerDay);
            Assert.That(SaveSerializer.Save(again), Is.EqualTo(SaveSerializer.Save(reference)));
        }

        [Test]
        public void Children_AreDrawnSmaller_AndBabiesAsABundle()
        {
            var pose = new ResidentPose(ResidentAction.Stand, Stance.Standing, Facing.DownRight, 0);
            var adult = ResidentLook.For("res-0031", 30);
            var child = ResidentLook.For("res-0031", 7);
            var teen = ResidentLook.For("res-0031", 15);
            var baby = ResidentLook.For("res-0031", 1);

            Assert.That(Top(ResidentArt.Render(child, pose)), Is.GreaterThan(Top(ResidentArt.Render(teen, pose))));
            Assert.That(Top(ResidentArt.Render(teen, pose)), Is.GreaterThan(Top(ResidentArt.Render(adult, pose))));
            Assert.That(Top(ResidentArt.Render(baby, pose)), Is.GreaterThan(ResidentArt.AnchorY - ResidentArt.BabyHeight - 3));
            Assert.That(ResidentArt.HeadTop(child, pose), Is.LessThan(ResidentArt.HeadTop(adult, pose)));
            Assert.That(child.Beard, Is.False);
        }

        /// <summary>The highest row with anything drawn in it.</summary>
        private static int Top(PixelCanvas canvas)
        {
            for (var y = 0; y < canvas.Height; y++)
            {
                for (var x = 0; x < canvas.Width; x++)
                {
                    if (!canvas.Get(x, y).IsClear)
                    {
                        return y;
                    }
                }
            }

            return canvas.Height;
        }
    }
}
