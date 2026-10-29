using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinyCity.Simulation;
using TinyCity.Simulation.Activities;
using TinyCity.Simulation.Community;
using TinyCity.Simulation.Configuration;
using TinyCity.Simulation.Content;
using TinyCity.Simulation.History;
using TinyCity.Simulation.Persistence;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Social;
using TinyCity.Simulation.Time;
using TinyCity.Simulation.World;

namespace TinyCity.Tests.EditMode
{
    /// <summary>Romance, community events, outings and the town history.</summary>
    public class CommunityTests
    {
        private const string Ivy = StarterTown.IvyId;
        private const string Felix = StarterTown.FelixId;

        private static Resident Get(TownSimulation sim, string id) => sim.GetResident(id);

        /// <summary>Gives a pair the given feelings for each other, both ways.</summary>
        private static void Feel(TownSimulation sim, string a, string b, double romance, double friendship = 70, double dislike = 0)
        {
            sim.Relationships.Get(a, b).Set(friendship, 50, 50, dislike, romance);
            sim.Relationships.Get(b, a).Set(friendship, 50, 50, dislike, romance);
        }

        private static void Review(TownSimulation sim, string a, string b) => sim.UpdateCouple(Get(sim, a), Get(sim, b));

        [Test]
        public void Compatibility_NeedsTwoLivingAdultsWhoAreOpenToIt_AndCloseInAge()
        {
            var sim = StarterTown.CreateNewGame();
            Assert.That(RomanceRules.Compatible(Get(sim, Ivy), Get(sim, Felix)), Is.True);
            Assert.That(RomanceRules.Compatible(Get(sim, StarterTown.ResidentId), Get(sim, StarterTown.ArthurId)), Is.False, "Arthur isn't looking, and the age gap is too big");
            Assert.That(RomanceRules.Compatible(Get(sim, StarterTown.ResidentId), Get(sim, StarterTown.SamuelId)), Is.False, "20 years apart");
            Assert.That(RomanceRules.Compatible(Get(sim, Ivy), Get(sim, Ivy)), Is.False);

            var felix = Get(sim, Felix);
            felix.OpenToRomance = false;
            Assert.That(RomanceRules.Compatible(Get(sim, Ivy), felix), Is.False);
            felix.OpenToRomance = true;
            felix.AgeYears = 17;
            Assert.That(RomanceRules.Compatible(Get(sim, Ivy), felix), Is.False, "only adults");
            felix.AgeYears = 19;
            sim.Die(felix, "test");
            Assert.That(RomanceRules.Compatible(Get(sim, Ivy), felix), Is.False);
        }

        [Test]
        public void Chemistry_IsFixedForAPair_CanBeOneSided_AndIsUncommon()
        {
            var sim = StarterTown.CreateNewGame();
            var people = sim.Residents.ToList();
            var pairs = people.SelectMany(a => people.Where(b => b != a).Select(b => (a, b))).ToList();
            Assert.That(pairs.All(p => RomanceRules.Spark(p.a, p.b) == RomanceRules.Spark(p.a, p.b)), Is.True);
            Assert.That(pairs.All(p => RomanceRules.Spark(p.a, p.b) >= 0 && RomanceRules.Spark(p.a, p.b) <= 1), Is.True);
            var withSpark = pairs.Count(p => RomanceRules.HasChemistry(p.a, p.b));
            Assert.That(withSpark, Is.InRange(pairs.Count / 5, pairs.Count * 3 / 5));
            Assert.That(pairs.Any(p => RomanceRules.HasChemistry(p.a, p.b) && !RomanceRules.HasChemistry(p.b, p.a)), Is.True, "feelings can be one-sided");
            Assert.That(RomanceRules.FeelingsCeiling(0), Is.EqualTo(0));
            Assert.That(RomanceRules.FeelingsCeiling(1), Is.EqualTo(100));
        }

        [Test]
        public void WarmChats_GrowFeelings_OnlyWhereThereIsChemistry()
        {
            var place = new Dictionary<string, Location>
            {
                [Ivy] = new Location(StarterTown.VillageMapId, 10, 9),
                [Felix] = new Location(StarterTown.VillageMapId, 11, 9),
            };
            var sim = TestTown.WithResidents(TestTown.WeekendAfternoon, r => place.TryGetValue(r.Id, out var l) ? l : (Location?)null, (r, n) => n[NeedType.Social] = 40, r => place.ContainsKey(r.Id));
            Assume.That(RomanceRules.HasChemistry(Get(sim, Ivy), Get(sim, Felix)) && RomanceRules.HasChemistry(Get(sim, Felix), Get(sim, Ivy)), Is.True);
            Feel(sim, Ivy, Felix, romance: 0, friendship: 40);

            sim.Advance(120);

            Assert.That(sim.Relationships.Get(Ivy, Felix).Interactions, Is.GreaterThan(0), "they should have chatted");
            Assert.That(sim.Relationships.Get(Ivy, Felix).Romance, Is.GreaterThan(0));
            Assert.That(sim.Relationships.Get(Felix, Ivy).Romance, Is.GreaterThan(0));

            var friends = TestTown.MabelAndArthurOnTheGreen(TestTown.WeekendAfternoon, social: 40);
            Feel(friends, StarterTown.ResidentId, StarterTown.ArthurId, romance: 0, friendship: 60);
            friends.Advance(120);
            Assert.That(friends.Relationships.Get(StarterTown.ResidentId, StarterTown.ArthurId).Interactions, Is.GreaterThan(0));
            Assert.That(friends.Relationships.Get(StarterTown.ResidentId, StarterTown.ArthurId).Romance, Is.EqualTo(0), "Arthur isn't a match for Mabel");
            Assert.That(friends.Relationships.Get(StarterTown.ArthurId, StarterTown.ResidentId).Romance, Is.EqualTo(0));
        }

        [Test]
        public void MutualFeelings_StartDating_WithMemoriesAndHistory()
        {
            var sim = StarterTown.CreateNewGame();
            Feel(sim, Ivy, Felix, romance: 49, friendship: 60);
            Review(sim, Ivy, Felix);
            Assert.That(sim.Relationships.PartnerOf(Ivy), Is.Null, "not quite yet");

            Feel(sim, Ivy, Felix, romance: 55, friendship: 60);
            Review(sim, Ivy, Felix);

            Assert.That(sim.Relationships.PartnerOf(Ivy), Is.EqualTo(Felix));
            Assert.That(sim.Relationships.PartnerOf(Felix), Is.EqualTo(Ivy));
            Assert.That(sim.Relationships.PartnershipOf(Ivy).Stage, Is.EqualTo(RomanceStage.Dating));
            Assert.That(sim.Memories.ForResident(Ivy).Any(m => m.Tags.Contains("dating") && m.Importance >= 8), Is.True);
            Assert.That(sim.Memories.ForResident(Felix).Any(m => m.Tags.Contains("dating")), Is.True);
            Assert.That(sim.History.Events.Single().Kind, Is.EqualTo(TownEventKind.Couple));
            Assert.That(sim.History.Events.Single().Id, Is.EqualTo("evt-000001"));
        }

        [Test]
        public void OneSidedFeelings_SomeoneTaken_OrRomanceOff_MeanNoCouple()
        {
            var sim = StarterTown.CreateNewGame();
            sim.Relationships.Get(Ivy, Felix).Set(70, 50, 50, 0, 90);
            sim.Relationships.Get(Felix, Ivy).Set(70, 50, 50, 0, 10);
            Review(sim, Ivy, Felix);
            Assert.That(sim.Relationships.Partnerships, Is.Empty);

            Feel(sim, Ivy, Felix, romance: 80);
            Feel(sim, Ivy, StarterTown.NellId, romance: 80);
            Review(sim, Ivy, StarterTown.NellId);
            Review(sim, Ivy, Felix);
            Assert.That(sim.Relationships.PartnerOf(Ivy), Is.EqualTo(StarterTown.NellId));
            Assert.That(sim.Relationships.PartnerOf(Felix), Is.Null, "Ivy is already with Nell");

            var off = StarterTown.CreateNewGame();
            off.RomanceEnabled = false;
            Feel(off, Ivy, Felix, romance: 80);
            Review(off, Ivy, Felix);
            Assert.That(off.Relationships.Partnerships, Is.Empty);
        }

        [Test]
        public void DatingCouple_CommitsAfterTwoWeeks_IfFeelingsAreStrong()
        {
            var sim = StarterTown.CreateNewGame();
            Feel(sim, Ivy, Felix, romance: 90, friendship: 80);
            Review(sim, Ivy, Felix);
            Review(sim, Ivy, Felix);
            Assert.That(sim.Relationships.PartnershipOf(Ivy).Stage, Is.EqualTo(RomanceStage.Dating), "too soon");

            sim.Advance(RomanceRules.DaysBeforeCommitment * GameTime.MinutesPerDay);
            Feel(sim, Ivy, Felix, romance: 90, friendship: 80);
            Review(sim, Ivy, Felix);

            var couple = sim.Relationships.PartnershipOf(Ivy);
            Assert.That(couple, Is.Not.Null);
            Assert.That(couple.Stage, Is.EqualTo(RomanceStage.Committed));
            Assert.That(sim.History.Events.Any(e => e.Kind == TownEventKind.Committed), Is.True);
            Assert.That(sim.Memories.ForResident(Ivy).Any(m => m.Tags.Contains("commitment")), Is.True);
        }

        [Test]
        public void FadingFeelings_EndInABreakUp_AndOnlyLaterAReconciliation()
        {
            var sim = StarterTown.CreateNewGame();
            Feel(sim, Ivy, Felix, romance: 60);
            Review(sim, Ivy, Felix);
            sim.Relationships.Get(Felix, Ivy).Set(70, 50, 50, 0, 20);
            Review(sim, Ivy, Felix);

            Assert.That(sim.Relationships.Partnerships, Is.Empty);
            Assert.That(sim.Relationships.Get(Ivy, Felix).BrokeUpMinute, Is.EqualTo(sim.Time.TotalMinutes));
            Assert.That(sim.Memories.ForResident(Ivy).Any(m => m.Summary.Contains("Felix broke up with me")), Is.True);
            Assert.That(sim.History.Events.Last().Kind, Is.EqualTo(TownEventKind.BreakUp));

            Feel(sim, Ivy, Felix, romance: 70);
            Review(sim, Ivy, Felix);
            Assert.That(sim.Relationships.Partnerships, Is.Empty, "too soon after breaking up");

            sim.Advance(RomanceRules.DaysBeforeReconciling * GameTime.MinutesPerDay);
            Feel(sim, Ivy, Felix, romance: 70);
            Review(sim, Ivy, Felix);
            Assert.That(sim.Relationships.PartnerOf(Ivy), Is.EqualTo(Felix));
            Assert.That(sim.History.Events.Last().Kind, Is.EqualTo(TownEventKind.Reconciled));
        }

        [Test]
        public void TooMuchResentment_EndsACouple()
        {
            var sim = StarterTown.CreateNewGame();
            Feel(sim, Ivy, Felix, romance: 90);
            Review(sim, Ivy, Felix);
            Feel(sim, Ivy, Felix, romance: 90, dislike: RomanceRules.BreakUpDislike);
            Review(sim, Ivy, Felix);
            Assert.That(sim.Relationships.Partnerships, Is.Empty);
        }

        [Test]
        public void APartnersDeath_EndsTheCouple_AndIsTheirMostImportantMemory()
        {
            var sim = StarterTown.CreateNewGame();
            Feel(sim, Ivy, Felix, romance: 80);
            Review(sim, Ivy, Felix);

            sim.Die(Get(sim, Felix), "pneumonia");

            Assert.That(sim.Relationships.Partnerships, Is.Empty);
            var grief = sim.Memories.ForResident(Ivy).Where(m => m.Tags.Contains("death")).ToList();
            Assert.That(grief.Count, Is.EqualTo(1));
            Assert.That(grief[0].Importance, Is.EqualTo(10));
            Assert.That(grief[0].Summary, Does.Contain("my partner"));
            Assert.That(sim.History.Events.Last().Kind, Is.EqualTo(TownEventKind.Death));
            Assert.That(sim.History.Events.Last().ResidentIds.ToList(), Is.EqualTo(new List<string> { Felix }));
        }

        [Test]
        public void Prompts_MentionACouple_AndASoftSpot()
        {
            var sim = StarterTown.CreateNewGame();
            Feel(sim, Ivy, Felix, romance: 80);
            sim.Relationships.Get(Ivy, StarterTown.TomId).Set(50, 50, 50, 0, 40);
            sim.Relationships.Get(Ivy, StarterTown.TomId).Interactions = 2;
            sim.Relationships.Get(Ivy, Felix).Interactions = 5;
            Review(sim, Ivy, Felix);

            Assert.That(TinyCity.Simulation.AI.PromptText.Relationship(sim, Get(sim, Ivy), Get(sim, Felix)), Does.Contain("you are dating"));
            Assert.That(TinyCity.Simulation.AI.PromptText.Relationship(sim, Get(sim, Ivy), Get(sim, StarterTown.TomId)), Does.Contain("soft spot"));
        }

        [Test]
        public void Calendar_HasWeeklyAndYearlyEvents_AndTheFeteReplacesTheGetTogether()
        {
            var calendar = StarterTown.CreateWorld().Calendar;
            Assert.That(calendar.OnAt(GameTime.FromDayAndTime(7, 15, 30))?.Id, Is.EqualTo("get-together"), "day 7 is a Sunday");
            Assert.That(calendar.OnAt(GameTime.FromDayAndTime(7, 17, 0)), Is.Null, "it's over at five");
            Assert.That(calendar.OnAt(GameTime.FromDayAndTime(3, 18, 30))?.Id, Is.EqualTo("quiz-night"), "day 3 is a Wednesday");
            Assert.That(calendar.OnAt(GameTime.FromDayAndTime(1, 18, 30)), Is.Null);
            Assert.That(calendar.OnAt(GameTime.FromDayAndTime(StarterTown.FeteDayOfYear, 15, 30))?.Id, Is.EqualTo("village-fete"));
            Assert.That(calendar.OnAt(GameTime.FromDayAndTime(StarterTown.FeteDayOfYear + 28, 14, 0))?.Id, Is.EqualTo("village-fete"), "every year");
            Assert.That(calendar.OnDay(StarterTown.FeteDayOfYear).Select(e => e.Id).ToList(), Is.EqualTo(new List<string> { "village-fete" }));

            var next = calendar.Next(GameTime.FromDayAndTime(1, 7, 0));
            Assert.That(next.HasValue, Is.True);
            Assert.That(next.Value.evt.Id, Is.EqualTo("quiz-night"));
            Assert.That(next.Value.start, Is.EqualTo(GameTime.FromDayAndTime(3, 18, 0)));
        }

        [Test]
        public void BadEvents_AreRejected()
        {
            Assert.Throws<ArgumentException>(() => new CommunityEvent("x", "X", "obj", 600, 500, days: 1));
            Assert.Throws<ArgumentException>(() => new CommunityEvent("x", "X", "obj", 600, 700));
            Assert.Throws<ArgumentException>(() => new CommunityEvent("x", "X", "obj", 600, 700, days: 1, yearDay: 3));
            Assert.Throws<ArgumentException>(() => new CommunityCalendar(new[] { new CommunityEvent("x", "X", "obj", 600, 700, days: 1), new CommunityEvent("x", "Y", "obj", 600, 700, days: 2) }));
        }

        [Test]
        public void GoingToAnEvent_IsOnlyPossibleWhileItsOn_AtItsPlace()
        {
            var sim = TestTown.WithResidents(GameTime.FromDayAndTime(3, 18, 10));
            var mabel = Get(sim, StarterTown.ResidentId);
            var quiz = TestTown.Object(sim, StarterTown.QuizCornerId);
            var green = TestTown.Object(sim, StarterTown.VillageGreenId);

            Assert.That(sim.Planner.CanDo(mabel, ActivityCatalog.AttendEvent, quiz, sim.Time), Is.True);
            Assert.That(sim.Planner.CanDo(mabel, ActivityCatalog.AttendEvent, green, sim.Time), Is.False);
            Assert.That(sim.Planner.CanDo(mabel, ActivityCatalog.AttendEvent, quiz, GameTime.FromDayAndTime(3, 20, 0)), Is.False);
            Assert.That(sim.Planner.FreeTimeOptions(mabel).Any(o => o.ActionId == "attend-event" && o.TargetId == StarterTown.QuizCornerId), Is.True,
                "the AI can choose to go");

            sim.CommunityEventsEnabled = false;
            Assert.That(sim.Planner.CanDo(mabel, ActivityCatalog.AttendEvent, quiz, sim.Time), Is.False);
            Assert.That(sim.Planner.FreeTimeOptions(mabel).Any(o => o.ActionId == "attend-event"), Is.False);
        }

        [Test]
        public void Events_DrawACrowd_WhoEachRememberItOnce_AndTheTownRecordsIt()
        {
            var sim = StarterTown.CreateNewGame();
            sim.Advance((int)(GameTime.FromDayAndTime(StarterTown.FeteDayOfYear, 17, 1).TotalMinutes - sim.Time.TotalMinutes));

            var fete = StarterTown.CreateWorld().Calendar.Events.Single(e => e.Id == "village-fete").OccurrenceId(StarterTown.FeteDayOfYear);
            var went = sim.Residents.Where(r => sim.Memories.ForResident(r.Id).Any(m => m.Tags.Contains(fete))).ToList();
            Assert.That(went.Count, Is.GreaterThanOrEqualTo(3), "the festival should draw a crowd");
            Assert.That(went.All(r => sim.Memories.ForResident(r.Id).Count(m => m.Tags.Contains(fete)) == 1), Is.True);

            var gatherings = sim.History.Events.Where(e => e.Kind == TownEventKind.Gathering).ToList();
            Assert.That(gatherings.Count, Is.GreaterThanOrEqualTo(4), "two quiz nights, a get-together and the festival");
            Assert.That(gatherings.Last().Summary, Does.Contain("festival"));
            Assert.That(gatherings.Last().ResidentIds.OrderBy(i => i).ToList(), Is.EqualTo(went.Select(r => r.Id).OrderBy(i => i).ToList()));
        }

        [Test]
        public void EventsSwitchedOff_NobodyGoes()
        {
            var sim = StarterTown.CreateNewGame();
            sim.CommunityEventsEnabled = false;
            sim.Advance(8 * GameTime.MinutesPerDay);
            Assert.That(sim.History.Events.Any(e => e.Kind == TownEventKind.Gathering), Is.False);
            Assert.That(sim.Memories.All.Any(m => m.Tags.Contains("community")), Is.False);
        }

        [Test]
        public void ACouple_InviteEachOtherForCoffee()
        {
            var sim = TestTown.WithResidents(TestTown.WeekendAfternoon, adjust: (r, n) => n[NeedType.Fun] = 60, include: r => r.Id == Ivy || r.Id == Felix);
            Feel(sim, Ivy, Felix, romance: 80);
            Review(sim, Ivy, Felix);
            var ivy = Get(sim, Ivy);
            var felix = Get(sim, Felix);
            felix.MoneyPence = 5000;
            var table = TestTown.Object(sim, "obj-cafe-table-window");
            ivy.ResetActivity();
            felix.ResetActivity();

            sim.StartChoice(ivy, new PlanChoice(ActivityCatalog.Coffee, table, "Fancies a coffee", PlanPriority.Leisure));

            Assert.That(felix.ActivityId, Is.EqualTo(ActivityCatalog.Coffee.Id));
            Assert.That(felix.TargetObjectId, Is.EqualTo(table.Id));
            Assert.That(felix.Log.Any(e => e.Text.Contains("Ivy's invitation")), Is.True);
        }

        [Test]
        public void RomanceAndHistory_SurviveSaveAndLoad()
        {
            var sim = StarterTown.CreateNewGame();
            Feel(sim, Ivy, Felix, romance: 90);
            Review(sim, Ivy, Felix);
            sim.Advance(RomanceRules.DaysBeforeCommitment * GameTime.MinutesPerDay);
            Feel(sim, Ivy, Felix, romance: 90, friendship: 80);
            Review(sim, Ivy, Felix);
            sim.Relationships.Get(StarterTown.ResidentId, StarterTown.TomId).BrokeUpMinute = 600;
            Get(sim, StarterTown.PriyaId).OpenToRomance = false;

            var saved = SaveSerializer.Save(sim);
            var result = SaveSerializer.Load(saved, StarterTown.CreateWorld());

            Assert.That(result.Success, Is.True, string.Join("; ", result.Errors));
            Assert.That(result.Warnings, Is.Empty, string.Join("; ", result.Warnings));
            var loaded = result.Simulation;
            var couple = loaded.Relationships.PartnershipOf(Ivy);
            Assert.That(couple.Stage, Is.EqualTo(RomanceStage.Committed));
            Assert.That(couple.Since, Is.EqualTo(sim.Relationships.PartnershipOf(Ivy).Since));
            Assert.That(couple.StageSince, Is.EqualTo(sim.Relationships.PartnershipOf(Ivy).StageSince));
            Assert.That(loaded.Relationships.Get(Ivy, Felix).Romance, Is.EqualTo(sim.Relationships.Get(Ivy, Felix).Romance));
            Assert.That(loaded.Relationships.Get(StarterTown.ResidentId, StarterTown.TomId).BrokeUpMinute, Is.EqualTo(600));
            Assert.That(Get(loaded, StarterTown.PriyaId).OpenToRomance, Is.False);
            Assert.That(Get(loaded, StarterTown.ArthurId).OpenToRomance, Is.False);
            Assert.That(loaded.History.Events.Select(e => e.Id + e.Kind + e.Summary + e.Time).ToList(), Is.EqualTo(sim.History.Events.Select(e => e.Id + e.Kind + e.Summary + e.Time).ToList()));
            Assert.That(loaded.History.NextNumber, Is.EqualTo(sim.History.NextNumber));
            Assert.That(SaveSerializer.Save(loaded), Is.EqualTo(saved));
        }

        [Test]
        public void Version5Save_LoadsWithNoCouples_ThenTakesStarterOpenness()
        {
            var sim = StarterTown.CreateNewGame();
            var root = (Dictionary<string, object>)Json.Parse(SaveSerializer.Save(sim));
            root["version"] = 5.0;
            root.Remove("partnerships");
            root.Remove("history");
            foreach (var r in ((List<object>)root["residents"]).Cast<Dictionary<string, object>>())
            {
                r.Remove("openToRomance");
            }

            foreach (var r in ((List<object>)root["relationships"]).Cast<Dictionary<string, object>>())
            {
                r.Remove("romance");
                r.Remove("brokeUpMinutes");
            }

            var result = SaveSerializer.Load(Json.Write(root), StarterTown.CreateWorld());
            Assert.That(result.Success, Is.True, string.Join("; ", result.Errors));
            Assert.That(result.UpgradedFromVersion, Is.EqualTo(5));
            Assert.That(result.Simulation.Relationships.Partnerships, Is.Empty);
            Assert.That(result.Simulation.History.Events, Is.Empty);
            var arthur = Get(result.Simulation, StarterTown.ArthurId);
            Assert.That(arthur.OpenToRomance, Is.True, "older saves don't know");

            StarterTown.AddRomanceToOlderSave(result.Simulation);
            Assert.That(arthur.OpenToRomance, Is.False);
            Assert.That(Get(result.Simulation, Ivy).OpenToRomance, Is.True);
        }

        [Test]
        public void DamagedCouplesAndHistory_AreSkippedWithWarnings()
        {
            var sim = StarterTown.CreateNewGame();
            sim.Advance(60);
            var root = (Dictionary<string, object>)Json.Parse(SaveSerializer.Save(sim));
            root["partnerships"] = new List<object>
            {
                new Dictionary<string, object> { ["firstId"] = Ivy, ["secondId"] = Felix, ["stage"] = "Dating", ["sinceMinutes"] = 0.0, ["stageSinceMinutes"] = 0.0 },
                new Dictionary<string, object> { ["firstId"] = Ivy, ["secondId"] = StarterTown.TomId, ["stage"] = "Dating", ["sinceMinutes"] = 0.0 },
                new Dictionary<string, object> { ["firstId"] = StarterTown.NellId, ["secondId"] = StarterTown.NellId, ["stage"] = "Dating", ["sinceMinutes"] = 0.0 },
                new Dictionary<string, object> { ["firstId"] = StarterTown.NellId, ["secondId"] = StarterTown.TomId, ["stage"] = "Married", ["sinceMinutes"] = 0.0 },
                new Dictionary<string, object> { ["firstId"] = StarterTown.NellId, ["secondId"] = "res-9999", ["stage"] = "Dating", ["sinceMinutes"] = 0.0 },
                new Dictionary<string, object> { ["firstId"] = StarterTown.NellId, ["secondId"] = StarterTown.TomId, ["stage"] = "Dating", ["sinceMinutes"] = 99999999.0 },
            };
            root["history"] = new Dictionary<string, object>
            {
                ["nextNumber"] = 5.0,
                ["records"] = new List<object>
                {
                    new Dictionary<string, object> { ["id"] = "evt-000003", ["gameMinutes"] = 10.0, ["kind"] = "Couple", ["summary"] = "A couple.", ["residentIds"] = new List<object> { Ivy, "res-9999" } },
                    new Dictionary<string, object> { ["id"] = "evt-000003", ["gameMinutes"] = 10.0, ["kind"] = "Couple", ["summary"] = "Duplicate." },
                    new Dictionary<string, object> { ["id"] = "evt-000004", ["gameMinutes"] = 10.0, ["kind"] = "Wedding", ["summary"] = "Unknown kind." },
                    new Dictionary<string, object> { ["id"] = "evt-000006", ["gameMinutes"] = 99999999.0, ["kind"] = "Death", ["summary"] = "In the future." },
                },
            };

            var result = SaveSerializer.Load(Json.Write(root), StarterTown.CreateWorld());

            Assert.That(result.Success, Is.True, string.Join("; ", result.Errors));
            Assert.That(result.Simulation.Relationships.Partnerships.Count, Is.EqualTo(1));
            Assert.That(result.Simulation.Relationships.PartnerOf(Ivy), Is.EqualTo(Felix));
            Assert.That(result.Simulation.History.Events.Count, Is.EqualTo(1));
            Assert.That(result.Simulation.History.Events[0].ResidentIds.ToList(), Is.EqualTo(new List<string> { Ivy }));
            Assert.That(result.Simulation.History.NextNumber, Is.EqualTo(5));
            Assert.That(result.Warnings.Any(w => w.Contains("couple")), Is.True);
            Assert.That(result.Warnings.Any(w => w.Contains("town history")), Is.True);
        }

        [Test]
        public void TownHistory_IsCapped_AndNeverReusesIds()
        {
            var history = new TownHistory();
            for (var i = 0; i < TownHistory.MaxEvents + 10; i++)
            {
                history.Add(new GameTime(i), TownEventKind.Gathering, "A gathering.", null);
            }

            Assert.That(history.Events.Count, Is.EqualTo(TownHistory.MaxEvents));
            Assert.That(history.Events[0].Id, Is.EqualTo("evt-000011"));
            Assert.That(history.Add(new GameTime(9999), TownEventKind.Death, "x", null).Id, Is.EqualTo($"evt-{TownHistory.MaxEvents + 11:000000}"));
        }

        [Test]
        public void Settings_HaveSimulationToggles_DefaultingOn()
        {
            var settings = new GameSettings();
            Assert.That(settings.Simulation.romance, Is.True);
            Assert.That(settings.Simulation.communityEvents, Is.True);
            settings.simulation = null;
            Assert.That(settings.Validate(), Is.Empty);
            Assert.That(settings.Simulation.romance, Is.True, "an older settings file without the section");
        }
    }
}
