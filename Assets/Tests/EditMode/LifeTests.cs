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
    /// <summary>Ageing, health, illness, death and hobbies.</summary>
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
        public void Birthday_AddsAYear_OnceAYear_AndIsRemembered()
        {
            var sim = StarterTown.CreateNewGame();
            var mabel = Mabel(sim);
            var age = mabel.AgeYears;
            var birthday = HealthRules.BirthdayCycleDay(mabel);
            Assert.That(birthday, Is.InRange(1, HealthRules.DaysPerYear));

            sim.Advance(HealthRules.DaysPerYear * GameTime.MinutesPerDay);

            Assert.That(mabel.AgeYears, Is.EqualTo(age + 1));
            Assert.That(sim.Memories.ForResident(mabel.Id).Count(m => m.Summary.Contains("birthday")), Is.EqualTo(1));
        }

        [Test]
        public void TurningFortyFive_IsANewLifeStage()
        {
            var sim = TestTown.WithResident(GameTime.FromDayAndTime(1, 7, 0));
            var mabel = Mabel(sim);
            mabel.AgeYears = 44;
            var day = Enumerable.Range(2, HealthRules.DaysPerYear).First(d => HealthRules.IsBirthday(mabel, d));

            sim.Advance((int)(GameTime.FromDayAndTime(day, 0, 5).TotalMinutes - sim.Time.TotalMinutes));

            Assert.That(mabel.AgeYears, Is.EqualTo(45));
            Assert.That(mabel.LifeStage, Is.EqualTo(LifeStage.MiddleAged));
            Assert.That(sim.Memories.ForResident(mabel.Id).Any(m => m.Summary.Contains("middle age")), Is.True);
        }

        [Test]
        public void OldAge_IsOnlyPossibleFromEighty_AndGrowsWithAge()
        {
            Assert.That(HealthRules.OldAgeChancePerDay(79), Is.EqualTo(0));
            Assert.That(HealthRules.OldAgeChancePerDay(80), Is.GreaterThan(0));
            Assert.That(HealthRules.OldAgeChancePerDay(90), Is.GreaterThan(HealthRules.OldAgeChancePerDay(85)));
            Assert.That(HealthRules.MaxHealthFor(30), Is.EqualTo(100));
            Assert.That(HealthRules.MaxHealthFor(82), Is.LessThan(70));
        }

        [Test]
        public void VeryOldResident_DiesOfOldAge_AndIsRemembered()
        {
            var sim = StarterTown.CreateNewGame();
            var edith = Get(sim, StarterTown.EdithId);
            edith.AgeYears = 150; // a one-in-five chance each day, so this ends within days
            var arthur = Get(sim, StarterTown.ArthurId);

            for (var i = 0; i < 60 * GameTime.MinutesPerDay && edith.IsAlive; i++)
            {
                sim.Tick();
            }

            Assert.That(edith.IsAlive, Is.False);
            Assert.That(edith.CauseOfDeath, Is.EqualTo("old age"));
            Assert.That(edith.DiedAt?.MinuteOfDay, Is.EqualTo(HealthRules.OldAgeCheckMinute));
            Assert.That(sim.Residents.Contains(edith), Is.True, "the dead stay in the town's records");
            Assert.That(sim.Memories.ForResident(arthur.Id).Any(m => m.Summary.Contains("Edith Crumb") && m.Summary.Contains("died")), Is.True);
            Assert.That(edith.State, Is.EqualTo(ResidentState.Idle));
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
                HealthRules.ApplyMinute(r, resting: false);
            }

            Assert.That(r.Health, Is.EqualTo(80 - (2 * HealthRules.NeglectDrainPerHour)).Within(0.01));

            r.Needs[NeedType.Hunger] = 90;
            r.Needs[NeedType.Thirst] = 90;
            for (var i = 0; i < 60; i++)
            {
                HealthRules.ApplyMinute(r, resting: true);
            }

            Assert.That(r.Health, Is.EqualTo(80 - (2 * HealthRules.NeglectDrainPerHour) + HealthRules.RestingRecoveryPerHour).Within(0.01));
        }

        [Test]
        public void DailyIllnessCheck_IsDeterministic_AndRiskierWhenRunDown()
        {
            var sim = StarterTown.CreateNewGame();
            var r = Mabel(sim);
            var healthy = HealthRules.IllnessRisk(r, 3);
            r.Health = 40;
            r.Needs[NeedType.Hygiene] = 10;
            r.ExposedDay = 3;
            Assert.That(HealthRules.IllnessRisk(r, 3), Is.GreaterThan(healthy + 0.2));
            Assert.That(HealthRules.IllnessRisk(r, 3 + HealthRules.ExposureDays + 1), Is.LessThan(HealthRules.IllnessRisk(r, 3)));

            var first = Enumerable.Range(1, 200).Select(d => HealthRules.CatchesIllness(r, d)?.Id).ToList();
            var again = Enumerable.Range(1, 200).Select(d => HealthRules.CatchesIllness(r, d)?.Id).ToList();
            Assert.That(again, Is.EqualTo(first));
            Assert.That(first.Count(i => i != null), Is.GreaterThan(0));
        }

        [Test]
        public void Flu_KeepsThemOffWork_InBed_AndAwayFromChats()
        {
            var sim = TestTown.WithResidents(GameTime.FromDayAndTime(1, 8, 0));
            var nell = Get(sim, StarterTown.NellId);
            sim.FallIll(nell, IllnessCatalog.Flu);

            var worked = 0;
            var rested = 0;
            for (var i = 0; i < 10 * 60; i++)
            {
                sim.Tick();
                worked += nell.State == ResidentState.Performing && nell.ActivityId == ActivityCatalog.Work.Id ? 1 : 0;
                rested += nell.State == ResidentState.Performing && nell.ActivityId == ActivityCatalog.Rest.Id ? 1 : 0;
                Assert.That(nell.State, Is.Not.EqualTo(ResidentState.Talking));
            }

            Assert.That(worked, Is.EqualTo(0), Log(nell));
            Assert.That(rested, Is.GreaterThan(4 * 60), Log(nell));
            Assert.That(nell.Log.Any(e => e.Text.StartsWith("Called in sick")), Is.True, Log(nell));
            Assert.That(sim.Economy.Ledger.For(nell.Id).Any(t => t.Kind == TinyCity.Simulation.Economy.TransactionKind.Wage), Is.False);
        }

        [Test]
        public void ACold_StillGoesToWork()
        {
            var sim = TestTown.WithResident(GameTime.FromDayAndTime(1, 8, 0));
            var mabel = Mabel(sim);
            sim.FallIll(mabel, IllnessCatalog.Cold);

            sim.Advance(3 * 60);

            Assert.That(mabel.ActivityId, Is.EqualTo(ActivityCatalog.Work.Id), Log(mabel));
        }

        [Test]
        public void Illness_PassesWithTime_FasterWithRest_AndIsRemembered()
        {
            var sim = TestTown.WithResident(TestTown.WeekendAfternoon);
            var mabel = Mabel(sim);
            sim.FallIll(mabel, IllnessCatalog.Cold);

            for (var i = 0; i < IllnessCatalog.Cold.RecoveryMinutes && mabel.IllnessId != null; i++)
            {
                sim.Tick();
            }

            Assert.That(mabel.IllnessId, Is.Null, "resting and sleeping count double, so a cold is gone within its recovery time");
            Assert.That(mabel.Log.Any(e => e.Text.StartsWith("Felt better")), Is.True);
            Assert.That(sim.Memories.ForResident(mabel.Id).Any(m => m.Summary == "I got over a cold."), Is.True);
        }

        [Test]
        public void FluInPoorHealth_BecomesPneumonia_AndTheClinicTreatsIt()
        {
            var sim = TestTown.WithResident(GameTime.FromDayAndTime(1, 8, 0), TestTown.Object(TestTown.WithResident(TestTown.WorkHour), StarterTown.BedId).Location);
            var mabel = Mabel(sim);
            mabel.Health = HealthRules.FrailHealth + 0.1;
            sim.FallIll(mabel, IllnessCatalog.Flu);

            sim.Advance(60);
            Assert.That(mabel.IllnessId, Is.EqualTo(IllnessCatalog.Pneumonia.Id), Log(mabel));
            Assert.That(HealthRules.NeedsDoctor(mabel), Is.True);

            for (var i = 0; i < 6 * 60 && !mabel.IllnessTreated; i++)
            {
                sim.Tick();
            }

            Assert.That(mabel.IllnessTreated, Is.True, Log(mabel));
            Assert.That(mabel.Log.Any(e => e.Text.StartsWith("Started being seen by the doctor")), Is.True);
            Assert.That(sim.Memories.ForResident(mabel.Id).Any(m => m.Summary.Contains("doctor at the clinic")), Is.True);
        }

        [Test]
        public void TheClinic_OnlySeesPatientsWhenOpen()
        {
            var sim = TestTown.WithResident(GameTime.FromDayAndTime(6, 10, 0));
            var mabel = Mabel(sim);
            mabel.IllnessId = IllnessCatalog.Pneumonia.Id;
            mabel.IllnessMinutesLeft = IllnessCatalog.Pneumonia.RecoveryMinutes;
            var bed = TestTown.Object(sim, "obj-clinic-bed-1");

            Assert.That(sim.Planner.CanDo(mabel, ActivityCatalog.SeeDoctor, bed, sim.Time), Is.False, "the clinic is shut at weekends");
            Assert.That(sim.Planner.CanDo(mabel, ActivityCatalog.SeeDoctor, bed, GameTime.FromDayAndTime(8, 10, 0)), Is.True);
        }

        [Test]
        public void ExtremeNeglect_WithUntreatedIllness_CanKill_AndTheCauseIsRecorded()
        {
            var sim = TestTown.WithResident(GameTime.FromDayAndTime(6, 10, 0));
            var mabel = Mabel(sim);
            mabel.Health = 0.2;
            mabel.IllnessId = IllnessCatalog.Pneumonia.Id;
            mabel.IllnessMinutesLeft = IllnessCatalog.Pneumonia.RecoveryMinutes;
            mabel.Needs[NeedType.Thirst] = 0;
            mabel.Needs[NeedType.Hunger] = 0;

            sim.Advance(60);

            Assert.That(mabel.IsAlive, Is.False);
            Assert.That(mabel.CauseOfDeath, Is.EqualTo("pneumonia"));
        }

        [Test]
        public void ChattingWithSomeoneWithACold_IsAnExposure()
        {
            var sim = TestTown.MabelAndArthurOnTheGreen(TestTown.WeekendAfternoon, social: 30);
            var arthur = Get(sim, StarterTown.ArthurId);
            sim.FallIll(arthur, IllnessCatalog.Cold);

            for (var i = 0; i < 120 && sim.Conversations.All.Count(c => c.Kind == TinyCity.Simulation.Memory.ConversationKind.Chat) == 0; i++)
            {
                sim.Tick();
            }

            Assert.That(Mabel(sim).ExposedDay, Is.EqualTo(sim.Time.Day));
        }

        [Test]
        public void Hobbies_AreOnlyForThoseWhoHaveThem_AndAreFavourites()
        {
            var sim = TestTown.WithResidents(TestTown.WeekendAfternoon, needsLevel: 70);
            var mabel = Mabel(sim);
            var easel = TestTown.Object(sim, "obj-village-easel");
            var chess = TestTown.Object(sim, "obj-village-chess");

            Assert.That(mabel.Hobbies.Contains("painting"), Is.True);
            Assert.That(sim.Planner.CanDo(mabel, ActivityCatalog.Paint, easel, sim.Time), Is.True);
            Assert.That(sim.Planner.CanDo(mabel, ActivityCatalog.Chess, chess, sim.Time), Is.False, "Mabel doesn't play chess");
            Assert.That(sim.FreeTimeOptionsFor(mabel).Any(o => o.Activity == ActivityCatalog.Chess), Is.False);

            var choice = sim.Planner.Choose(mabel, sim.Time);
            Assert.That(choice.Activity.Hobby, Is.Not.Null, $"chose {choice.Activity.Id}");
        }

        [Test]
        public void SharedHobby_InvitesAFriend_AndIsRememberedTogether()
        {
            var sim = TestTown.WithResidents(
                TestTown.WeekendAfternoon,
                r => r.Id == StarterTown.ArthurId ? TestTown.Object(TestTown.WithResident(TestTown.WorkHour), "obj-village-chess").Location
                    : r.Id == StarterTown.NellId ? new Location(StarterTown.VillageMapId, 30, 8) : (Location?)null,
                (r, n) => n[NeedType.Fun] = r.Id == StarterTown.ArthurId ? 30 : 60,
                r => r.Id == StarterTown.ArthurId || r.Id == StarterTown.NellId,
                needsLevel: 85);
            var arthur = Get(sim, StarterTown.ArthurId);
            var nell = Get(sim, StarterTown.NellId);
            arthur.SetHobbies(new[] { "chess" });

            sim.Tick();
            Assert.That(arthur.ActivityId, Is.EqualTo(ActivityCatalog.Chess.Id), Log(arthur));
            Assert.That(nell.ActivityId, Is.EqualTo(ActivityCatalog.Chess.Id), Log(nell));
            Assert.That(nell.Log.Last().Text, Does.StartWith("Accepted Arthur's invitation"));

            for (var i = 0; i < 60 && nell.State != ResidentState.Performing; i++)
            {
                sim.Tick();
            }

            var social = nell.Needs[NeedType.Social];
            sim.Advance(5);

            Assert.That(nell.Needs[NeedType.Social], Is.GreaterThan(social), "company at the chess table");
            Assert.That(sim.Memories.ForResident(nell.Id).Count(m => m.Summary.Contains("playing chess with Arthur")), Is.EqualTo(1));
            Assert.That(sim.Memories.ForResident(arthur.Id).Count(m => m.Summary.Contains("playing chess with Nell")), Is.EqualTo(1));
            Assert.That(sim.Relationships.Get(nell.Id, arthur.Id).HaveMet, Is.True);
        }

        [Test]
        public void Prompts_MentionHobbiesAndHealth()
        {
            var sim = TestTown.WithResident(TestTown.WeekendAfternoon);
            var mabel = Mabel(sim);
            sim.FallIll(mabel, IllnessCatalog.Cold);
            var options = sim.FreeTimeOptionsFor(mabel);

            var text = DecisionPrompt.UserText(sim, mabel, options, new TinyCity.Simulation.Memory.MemoryRecord[0]);
            var body = DecisionPrompt.BuildBody("qwen2.5:7b", sim, mabel, options);

            Assert.That(text, Does.Contain("you are poorly with a cold"));
            Assert.That(body, Does.Contain("Your hobbies: gardening, painting"));
        }

        [Test]
        public void SaveAndLoad_KeepsHealthIllnessDeathAndHobbies()
        {
            var sim = StarterTown.CreateNewGame();
            var mabel = Mabel(sim);
            mabel.Health = 63.5;
            sim.FallIll(mabel, IllnessCatalog.Pneumonia);
            mabel.IllnessTreated = true;
            mabel.IllnessMinutesLeft = 1234;
            mabel.ExposedDay = 1;
            var edith = Get(sim, StarterTown.EdithId);
            sim.Advance(30);
            sim.Die(edith, "old age");

            var result = SaveSerializer.Load(SaveSerializer.Save(sim), StarterTown.CreateWorld());

            Assert.That(result.Success, Is.True, string.Join("; ", result.Errors));
            Assert.That(result.Warnings, Is.Empty, string.Join("; ", result.Warnings));
            var m = Mabel(result.Simulation);
            Assert.That(m.Health, Is.EqualTo(mabel.Health));
            Assert.That(m.IllnessId, Is.EqualTo("pneumonia"));
            Assert.That(m.IllnessTreated, Is.True);
            Assert.That(m.IllnessMinutesLeft, Is.EqualTo(mabel.IllnessMinutesLeft));
            Assert.That(m.ExposedDay, Is.EqualTo(1));
            Assert.That(m.Hobbies.ToList(), Is.EqualTo(new List<string> { "gardening", "painting" }));
            var e = Get(result.Simulation, StarterTown.EdithId);
            Assert.That(e.IsAlive, Is.False);
            Assert.That(e.DiedAt, Is.EqualTo(edith.DiedAt));
            Assert.That(e.CauseOfDeath, Is.EqualTo("old age"));
        }

        [Test]
        public void DamagedHealthData_IsRepairedWithWarnings()
        {
            var sim = StarterTown.CreateNewGame();
            var root = (Dictionary<string, object>)Json.Parse(SaveSerializer.Save(sim));
            var first = ((List<object>)root["residents"]).Cast<Dictionary<string, object>>().First();
            first["health"] = 5000.0;
            first["illness"] = new Dictionary<string, object> { ["id"] = "dragon-pox", ["minutesLeft"] = 10.0 };
            first["hobbies"] = new List<object> { "chess", "juggling-chainsaws", 3.0 };

            var result = SaveSerializer.Load(Json.Write(root), StarterTown.CreateWorld());

            Assert.That(result.Success, Is.True);
            var m = Mabel(result.Simulation);
            Assert.That(m.Health, Is.EqualTo(HealthRules.MaxHealthFor(m.AgeYears)));
            Assert.That(m.IllnessId, Is.Null);
            Assert.That(m.Hobbies.ToList(), Is.EqualTo(new List<string> { "chess" }));
            Assert.That(result.Warnings.Any(w => w.Contains("unknown illness")), Is.True);
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
            Assert.That(sim.Memories.All.Any(m => m.Summary.Contains("birthday")), Is.True);
        }
    }
}
