using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinyCity.Simulation;
using TinyCity.Simulation.Activities;
using TinyCity.Simulation.Content;
using TinyCity.Simulation.Economy;
using TinyCity.Simulation.Persistence;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Time;
using TinyCity.Simulation.World;

namespace TinyCity.Tests.EditMode
{
    public class EconomyTests
    {
        private static Resident Mabel(TownSimulation sim) => sim.GetResident(StarterTown.ResidentId);

        private static WorldObject CafeTable(TownSimulation sim) => TestTown.Object(sim, "obj-cafe-table-window");

        /// <summary>Saturday at 10:00, when the café and store are open.</summary>
        private static GameTime SaturdayMorning => GameTime.FromDayAndTime(6, 10, 0);

        [Test]
        public void Shops_OpenOnlyWhileTheirStaffAreIn()
        {
            var counter = TestTown.Object(StarterTown.CreateNewGame(), "obj-store-counter");
            var store = StarterTown.CreateWorld().Businesses.Single(b => b.Id == StarterTown.StoreId);

            var athome = TestTown.WithResidents(SaturdayMorning);
            Assert.That(store.IsOpen(athome.Time), Is.True, "within opening hours");
            Assert.That(athome.Economy.IsOpen(store, athome.Time), Is.False, "but Rosa is still at home");
            Assert.That(athome.Economy.CanAfford(Mabel(athome), ActivityCatalog.BuyGroceries, TestTown.Object(athome, "obj-store-shelves"), athome.Time, out var reason), Is.False);
            Assert.That(reason, Does.Contain("closed"));

            var atWork = TestTown.WithResidents(SaturdayMorning, r => r.Id == StarterTown.RosaId ? counter.Location : (Location?)null);
            Assert.That(atWork.Economy.IsOpen(store, atWork.Time), Is.True, "Rosa is behind the counter");
            Assert.That(atWork.Economy.IsOpen(store, GameTime.FromDayAndTime(6, 18, 0)), Is.False, "she's there, but it's after closing time");
            Assert.That(atWork.Economy.IsOpen(store, GameTime.FromDayAndTime(7, 10, 0)), Is.False, "she doesn't work Sundays");

            atWork.Die(atWork.GetResident(StarterTown.RosaId), "test");
            Assert.That(atWork.Economy.IsOpen(store, atWork.Time), Is.False, "with nobody left to run it, the store stays shut");

            var noStaff = TestTown.WithResident(SaturdayMorning);
            Assert.That(noStaff.Economy.IsOpen(store, noStaff.Time), Is.True, "a town with nobody employed there keeps to the clock");
        }

        [Test]
        public void Money_IsFormattedInPounds()
        {
            Assert.That(Money.Format(1250), Is.EqualTo("£12.50"));
            Assert.That(Money.Format(-350), Is.EqualTo("-£3.50"));
            Assert.That(Money.Pounds(2.8), Is.EqualTo(280));
        }

        [Test]
        public void Buying_TakesMoneyAndStock_AndIsRecorded()
        {
            var sim = TestTown.WithResident(SaturdayMorning);
            var mabel = Mabel(sim);
            var before = mabel.MoneyPence;
            var cafe = sim.Economy.GetBusiness(StarterTown.CafeId);
            var stock = cafe.Stock("coffee");

            Assert.That(sim.Economy.Pay(mabel, ActivityCatalog.Coffee, CafeTable(sim), sim.Time, out var reason), Is.True, reason);

            Assert.That(mabel.MoneyPence, Is.EqualTo(before - 280));
            Assert.That(cafe.Stock("coffee"), Is.EqualTo(stock - 1));
            Assert.That(cafe.TakingsPence, Is.EqualTo(280));
            var record = sim.Economy.Ledger.All.Single();
            Assert.That(record.Id, Is.EqualTo("txn-000001"));
            Assert.That(record.AmountPence, Is.EqualTo(-280));
            Assert.That(record.Kind, Is.EqualTo(TransactionKind.Purchase));
            Assert.That(record.BusinessId, Is.EqualTo(StarterTown.CafeId));
        }

        [Test]
        public void ClosedShop_SoldOut_OrTooExpensive_SellsNothing()
        {
            var late = TestTown.WithResident(GameTime.FromDayAndTime(6, 22, 0));
            Assert.That(late.Economy.CanAfford(Mabel(late), ActivityCatalog.Coffee, CafeTable(late), late.Time, out var reason), Is.False);
            Assert.That(reason, Does.Contain("closed"));

            var soldOut = TestTown.WithResident(SaturdayMorning);
            soldOut.Economy.GetBusiness(StarterTown.CafeId).SetStock("coffee", 0);
            Assert.That(soldOut.Economy.CanAfford(Mabel(soldOut), ActivityCatalog.Coffee, CafeTable(soldOut), soldOut.Time, out reason), Is.False);
            Assert.That(reason, Does.Contain("sold out"));

            var broke = TestTown.WithResident(SaturdayMorning);
            Mabel(broke).MoneyPence = 100;
            Assert.That(broke.Economy.Pay(Mabel(broke), ActivityCatalog.Coffee, CafeTable(broke), broke.Time, out reason), Is.False);
            Assert.That(reason, Does.Contain("afford"));
            Assert.That(Mabel(broke).MoneyPence, Is.EqualTo(100));
            Assert.That(broke.Economy.Ledger.All, Is.Empty);

            var wrongPlace = TestTown.WithResident(SaturdayMorning);
            var armchair = TestTown.Object(wrongPlace, "obj-cottage1-armchair");
            Assert.That(wrongPlace.Economy.CanAfford(Mabel(wrongPlace), ActivityCatalog.Coffee, armchair, wrongPlace.Time, out _), Is.False);
        }

        [Test]
        public void ShopsRestock_AsTheyOpen()
        {
            var sim = TestTown.WithResident(GameTime.FromDayAndTime(6, 7, 50));
            var cafe = sim.Economy.GetBusiness(StarterTown.CafeId);
            cafe.SetStock("coffee", 3);

            sim.Advance(5);
            Assert.That(cafe.Stock("coffee"), Is.EqualTo(3));

            sim.Advance(10);
            Assert.That(cafe.Stock("coffee"), Is.EqualTo(cafe.Business.Item("coffee").MaxStock));
        }

        [Test]
        public void Wages_ArePaidForTheTimeWorked_WhenTheShiftEnds()
        {
            var sim = StarterTown.CreateNewGame();
            var mabel = Mabel(sim);
            sim.Advance((18 - 7) * 60);

            var wages = sim.Economy.Ledger.For(mabel.Id).Where(t => t.Kind == TransactionKind.Wage).ToList();
            Assert.That(wages.Count, Is.GreaterThanOrEqualTo(1));
            var paid = wages.Sum(t => t.AmountPence);
            Assert.That(paid, Is.GreaterThan(Money.Pounds(12) * 6), "most of an 8-hour shift at £12 an hour");
            Assert.That(paid, Is.LessThanOrEqualTo(Money.Pounds(12) * 8.5), "paid from arriving, which can be a few minutes early");
            Assert.That(mabel.UnpaidWorkMinutes, Is.EqualTo(0));
            Assert.That(mabel.Log.Any(e => e.Text.StartsWith("Was paid")), Is.True);
        }

        [Test]
        public void MondayMorning_PensionComesIn_ThenRentGoesOut()
        {
            var sim = StarterTown.CreateNewGame();
            var arthur = sim.GetResident(StarterTown.ArthurId);
            var before = arthur.MoneyPence;

            sim.Tick();

            var bills = sim.Economy.Ledger.For(arthur.Id).ToList();
            Assert.That(bills.Select(t => t.Kind).ToList(), Is.EqualTo(new List<TransactionKind> { TransactionKind.Income, TransactionKind.Rent }));
            Assert.That(arthur.MoneyPence, Is.EqualTo(before + arthur.WeeklyIncomePence - arthur.WeeklyRentPence));

            sim.Advance(GameTime.MinutesPerDay);
            Assert.That(sim.Economy.Ledger.For(arthur.Id).Count(t => t.Kind == TransactionKind.Rent), Is.EqualTo(1), "rent is weekly");
        }

        [Test]
        public void RentTheyCantCover_IsOwed_AndRemembered()
        {
            var sim = StarterTown.CreateNewGame();
            var felix = sim.GetResident(StarterTown.FelixId);
            felix.MoneyPence = 0;
            felix.WeeklyIncomePence = 0;

            sim.Tick();

            Assert.That(felix.MoneyPence, Is.EqualTo(-felix.WeeklyRentPence));
            Assert.That(sim.Memories.ForResident(felix.Id).Any(m => m.Summary.Contains("couldn't cover this week's rent")), Is.True);
        }

        [Test]
        public void Cooking_UsesGroceries()
        {
            var sim = TestTown.WithResident(TestTown.WeekendAfternoon, Bed(), adjust: n => n[NeedType.Hunger] = 10);
            var mabel = Mabel(sim);
            var groceries = mabel.Groceries;

            sim.Advance(60);

            Assert.That(mabel.Log.Any(e => e.Text.StartsWith("Started cooking")), Is.True, string.Join("\n", mabel.Log.Select(e => e.Text)));
            Assert.That(mabel.Groceries, Is.EqualTo(groceries - 1));
        }

        [Test]
        public void NoFoodAndNoMoney_StillNeverStarves()
        {
            var sim = TestTown.WithResident(TestTown.WeekendAfternoon, Bed(), adjust: n => n[NeedType.Hunger] = 10);
            var mabel = Mabel(sim);
            mabel.Groceries = 0;
            mabel.MoneyPence = 0;

            sim.Advance(90);

            Assert.That(mabel.Log.Any(e => e.Text.Contains("snack")), Is.True, string.Join("\n", mabel.Log.Select(e => e.Text)));
            Assert.That(mabel.Needs[NeedType.Hunger], Is.GreaterThan(30));
            Assert.That(mabel.MoneyPence, Is.EqualTo(0));
        }

        [Test]
        public void LowOnFood_GoesShopping()
        {
            var sim = TestTown.WithResident(SaturdayMorning, Bed());
            var mabel = Mabel(sim);
            mabel.Groceries = 1;
            var before = mabel.MoneyPence;

            sim.Advance(3 * 60);

            var purchase = sim.Economy.Ledger.For(mabel.Id).FirstOrDefault(t => t.BusinessId == StarterTown.StoreId);
            Assert.That(purchase, Is.Not.Null, string.Join("\n", mabel.Log.Select(e => e.Text)));
            Assert.That(mabel.Groceries, Is.GreaterThanOrEqualTo(1 + 6 - 1));
            Assert.That(mabel.MoneyPence, Is.LessThanOrEqualTo(before - Money.Pounds(12)));
        }

        [Test]
        public void FreeTimeOptions_ShowPrices_AndLeaveOutWhatTheyCantAfford()
        {
            var sim = TestTown.WithResident(SaturdayMorning, CafeTable(TestTown.WithResident(SaturdayMorning)).Location, needsLevel: 50);
            var mabel = Mabel(sim);

            var coffee = sim.FreeTimeOptionsFor(mabel).FirstOrDefault(o => o.Activity == ActivityCatalog.Coffee);
            Assert.That(coffee, Is.Not.Null);
            Assert.That(coffee.PricePence, Is.EqualTo(280));
            Assert.That(TinyCity.Simulation.AI.DecisionPrompt.Describe(coffee), Does.Contain("costs £2.80"));

            mabel.MoneyPence = 0;
            Assert.That(sim.FreeTimeOptionsFor(mabel).Any(o => o.PricePence > 0), Is.False);
        }

        [Test]
        public void AWeekInTheTenResidentTown_NobodyIsStuck_AndTheBooksBalance()
        {
            var sim = StarterTown.CreateNewGame();
            Assert.That(sim.Residents.Count, Is.EqualTo(10));
            var start = sim.Residents.ToDictionary(r => r.Id, r => r.MoneyPence);
            var lowSince = new Dictionary<(string, NeedType), long>();
            var worstStretch = 0L;
            var worst = string.Empty;

            for (var minute = 0; minute < 7 * GameTime.MinutesPerDay; minute++)
            {
                sim.Tick();
                foreach (var r in sim.Residents)
                {
                    foreach (var need in new[] { NeedType.Hunger, NeedType.Thirst, NeedType.Energy, NeedType.Toilet })
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

            Assert.That(worstStretch, Is.LessThan(3 * 60), $"a critical need stayed near zero too long: {worst}");

            foreach (var r in sim.Residents)
            {
                var recorded = sim.Economy.Ledger.For(r.Id).Sum(t => t.AmountPence);
                Assert.That(r.MoneyPence, Is.EqualTo(start[r.Id] + recorded), $"{r.FirstName}'s money matches the ledger");
                if (r.Job != null)
                {
                    Assert.That(sim.Economy.Ledger.For(r.Id).Count(t => t.Kind == TransactionKind.Wage), Is.GreaterThanOrEqualTo(3), $"{r.FirstName} went to work");
                }
            }

            Assert.That(sim.Economy.Ledger.All.Any(t => t.Kind == TransactionKind.Purchase), Is.True, "someone bought something");

            var reloaded = SaveSerializer.Load(SaveSerializer.Save(sim), StarterTown.CreateWorld());
            Assert.That(reloaded.Success, Is.True, string.Join("; ", reloaded.Errors));
            Assert.That(reloaded.Warnings, Is.Empty, string.Join("; ", reloaded.Warnings));
        }

        [Test]
        public void SaveAndLoad_KeepsMoneyStockAndTheLedger()
        {
            var sim = TestTown.WithResident(SaturdayMorning);
            var mabel = Mabel(sim);
            sim.Economy.Pay(mabel, ActivityCatalog.Coffee, CafeTable(sim), sim.Time, out _);
            mabel.UnpaidWorkMinutes = 0;
            mabel.Groceries = 3;

            var result = SaveSerializer.Load(SaveSerializer.Save(sim), StarterTown.CreateWorld());

            Assert.That(result.Success, Is.True, string.Join("; ", result.Errors));
            Assert.That(result.Warnings, Is.Empty, string.Join("; ", result.Warnings));
            var loaded = result.Simulation;
            var loadedMabel = Mabel(loaded);
            Assert.That(loadedMabel.MoneyPence, Is.EqualTo(mabel.MoneyPence));
            Assert.That(loadedMabel.Groceries, Is.EqualTo(3));
            Assert.That(loadedMabel.WeeklyRentPence, Is.EqualTo(mabel.WeeklyRentPence));
            Assert.That(loadedMabel.Job.HourlyWagePence, Is.EqualTo(mabel.Job.HourlyWagePence));
            Assert.That(loadedMabel.Job.WorkDays, Is.EqualTo(mabel.Job.WorkDays));
            Assert.That(loaded.Economy.GetBusiness(StarterTown.CafeId).Stock("coffee"), Is.EqualTo(sim.Economy.GetBusiness(StarterTown.CafeId).Stock("coffee")));
            Assert.That(loaded.Economy.GetBusiness(StarterTown.CafeId).TakingsPence, Is.EqualTo(280));
            Assert.That(loaded.Economy.Ledger.All.Single().Description, Is.EqualTo(sim.Economy.Ledger.All.Single().Description));

            Mabel(loaded).MoneyPence += 0;
            loaded.Economy.Pay(Mabel(loaded), ActivityCatalog.Coffee, CafeTable(loaded), loaded.Time, out _);
            Assert.That(loaded.Economy.Ledger.All.Last().Id, Is.EqualTo("txn-000002"), "ids continue after loading");
        }

        [Test]
        public void ShiftWorkers_KeepTheirDaysThroughASave()
        {
            var sim = StarterTown.CreateNewGame();
            var priya = sim.GetResident(StarterTown.PriyaId);

            var loaded = SaveSerializer.Load(SaveSerializer.Save(sim), StarterTown.CreateWorld()).Simulation.GetResident(StarterTown.PriyaId);

            Assert.That(loaded.Job.WorksOn(Weekday.Monday), Is.False);
            Assert.That(loaded.Job.WorksOn(Weekday.Sunday), Is.True);
            Assert.That(loaded.Job.DaysText(), Is.EqualTo(priya.Job.DaysText()));
        }

        [Test]
        public void DamagedMoneyRecords_AreSkippedWithAWarning()
        {
            var sim = TestTown.WithResident(SaturdayMorning);
            sim.Economy.Pay(Mabel(sim), ActivityCatalog.Coffee, CafeTable(sim), sim.Time, out _);
            var root = (Dictionary<string, object>)Json.Parse(SaveSerializer.Save(sim));
            var ledger = (Dictionary<string, object>)((Dictionary<string, object>)root["economy"])["ledger"];
            ((List<object>)ledger["records"]).Add(new Dictionary<string, object> { ["id"] = "txn-000099", ["kind"] = "Theft", ["residentId"] = "nobody" });
            var stock = (Dictionary<string, object>)((List<object>)((Dictionary<string, object>)root["economy"])["businesses"]).Cast<Dictionary<string, object>>().First(b => (string)b["id"] == StarterTown.CafeId)["stock"];
            stock["coffee"] = 99999.0;

            var result = SaveSerializer.Load(Json.Write(root), StarterTown.CreateWorld());

            Assert.That(result.Success, Is.True);
            Assert.That(result.Warnings.Any(w => w.Contains("money record")), Is.True, string.Join("; ", result.Warnings));
            Assert.That(result.Simulation.Economy.Ledger.All.Count, Is.EqualTo(1));
            Assert.That(result.Simulation.Economy.GetBusiness(StarterTown.CafeId).Stock("coffee"), Is.EqualTo(40), "stock is capped");
        }

        [Test]
        public void Version3Save_GetsStarterFinances()
        {
            var sim = StarterTown.CreateNewGame();
            var root = (Dictionary<string, object>)Json.Parse(SaveSerializer.Save(sim));
            root["version"] = 3.0;
            root.Remove("economy");
            foreach (var r in ((List<object>)root["residents"]).Cast<Dictionary<string, object>>())
            {
                foreach (var key in new[] { "money", "groceries", "weeklyRent", "weeklyIncome", "incomeLabel", "unpaidWorkMinutes" })
                {
                    r.Remove(key);
                }

                if (r["job"] is Dictionary<string, object> job)
                {
                    job.Remove("workDays");
                    job.Remove("hourlyWage");
                }
            }

            var result = SaveSerializer.Load(Json.Write(root), StarterTown.CreateWorld());
            Assert.That(result.Success, Is.True, string.Join("; ", result.Errors));
            Assert.That(result.UpgradedFromVersion, Is.EqualTo(3));
            var loaded = result.Simulation;
            Assert.That(Mabel(loaded).MoneyPence, Is.EqualTo(0));
            Assert.That(Mabel(loaded).Job.HourlyWagePence, Is.EqualTo(0));

            StarterTown.AddFinancesToOlderSave(loaded);

            Assert.That(Mabel(loaded).MoneyPence, Is.EqualTo(Mabel(sim).MoneyPence));
            Assert.That(Mabel(loaded).Job.HourlyWagePence, Is.EqualTo(Money.Pounds(12)));
            Assert.That(loaded.GetResident(StarterTown.ArthurId).WeeklyIncomePence, Is.GreaterThan(0));
            Assert.That(loaded.Economy.GetBusiness(StarterTown.CafeId).Stock("coffee"), Is.EqualTo(40));
        }

        private static Location Bed() => TestTown.Object(TestTown.WithResident(TestTown.WeekendAfternoon), StarterTown.BedId).Location;
    }
}
