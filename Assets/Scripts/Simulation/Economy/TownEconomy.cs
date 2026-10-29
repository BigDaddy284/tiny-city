using System;
using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation.Activities;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Time;
using TinyCity.Simulation.World;

namespace TinyCity.Simulation.Economy
{
    /// <summary>
    /// The game's money rules: wages per hour worked, weekly rent and income, and purchases from businesses with
    /// prices, stock and opening hours. Every change is recorded in the <see cref="Ledger"/>; the AI never touches money.
    /// </summary>
    public sealed class TownEconomy
    {
        /// <summary>Weekly rent and income are settled at this time every Monday.</summary>
        public static readonly int WeeklyBillsMinute = GameTime.MinuteOfDayFor(7, 0);

        private readonly TownWorld world;
        private readonly List<BusinessState> businesses;

        public TownEconomy(TownWorld world)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            businesses = world.Businesses.Select(b => new BusinessState(b)).ToList();
        }

        public IReadOnlyList<BusinessState> Businesses => businesses;

        public Ledger Ledger { get; } = new Ledger();

        public BusinessState GetBusiness(string businessId) => businesses.FirstOrDefault(b => b.Business.Id == businessId);

        public BusinessState BusinessAt(Location location) => businesses.FirstOrDefault(b => b.Business.MapId == location.MapId);

        /// <summary>
        /// Whether the resident can do this activity at this object now, as far as money and food go: groceries for
        /// cooking, and for purchases an open business with the item in stock and enough money to pay.
        /// </summary>
        public bool CanAfford(Resident r, ActivityDefinition activity, WorldObject obj, GameTime time, out string reason)
        {
            reason = null;
            if (activity == null)
            {
                return true;
            }

            if (activity.UsesGroceries && r.Groceries <= 0)
            {
                reason = "there was no food in the cupboard";
                return false;
            }

            if (activity.ItemId == null && !activity.NeedsOpenBusiness)
            {
                return true;
            }

            var shop = obj == null ? null : BusinessAt(obj.Location);
            if (shop != null && !shop.Business.IsOpen(time))
            {
                reason = $"the {shop.Business.Name.ToLowerInvariant()} was closed";
                return false;
            }

            if (activity.ItemId == null)
            {
                return true;
            }

            var item = shop?.Business.Item(activity.ItemId);
            if (item == null)
            {
                reason = "it isn't sold there";
                return false;
            }

            if (shop.Stock(item.Id) <= 0)
            {
                reason = $"the {shop.Business.Name.ToLowerInvariant()} had sold out of {item.Name.ToLowerInvariant()}";
                return false;
            }

            if (r.MoneyPence < item.PricePence)
            {
                reason = $"they couldn't afford {item.Name.ToLowerInvariant()} ({Money.Format(item.PricePence)})";
                return false;
            }

            return true;
        }

        /// <summary>The price of the activity here, or 0 if it's free.</summary>
        public long PriceOf(ActivityDefinition activity, WorldObject obj)
        {
            if (activity?.ItemId == null || obj == null)
            {
                return 0;
            }

            return BusinessAt(obj.Location)?.Business.Item(activity.ItemId)?.PricePence ?? 0;
        }

        /// <summary>Takes payment and stock (or groceries) as the activity starts. Returns false, with a reason, if it can't.</summary>
        internal bool Pay(Resident r, ActivityDefinition activity, WorldObject obj, GameTime time, out string reason)
        {
            if (!CanAfford(r, activity, obj, time, out reason))
            {
                return false;
            }

            if (activity.UsesGroceries)
            {
                r.Groceries--;
            }

            if (activity.ItemId == null)
            {
                return true;
            }

            var shop = BusinessAt(obj.Location);
            var item = shop.Business.Item(activity.ItemId);
            shop.SetStock(item.Id, shop.Stock(item.Id) - 1);
            shop.TakingsPence += item.PricePence;
            r.MoneyPence -= item.PricePence;
            r.Groceries += item.GroceryMeals;
            Ledger.Add(time, r.Id, -item.PricePence, TransactionKind.Purchase, $"{item.Name} at the {shop.Business.Name}", shop.Business.Id);
            return true;
        }

        /// <summary>Pays for the minutes worked since the last payment. Returns the amount, or 0.</summary>
        internal long PayWages(Resident r, GameTime time)
        {
            if (r.UnpaidWorkMinutes <= 0 || r.Job == null)
            {
                r.UnpaidWorkMinutes = 0;
                return 0;
            }

            var amount = r.Job.HourlyWagePence * r.UnpaidWorkMinutes / 60;
            var minutes = r.UnpaidWorkMinutes;
            r.UnpaidWorkMinutes = 0;
            if (amount <= 0)
            {
                return 0;
            }

            r.MoneyPence += amount;
            Ledger.Add(time, r.Id, amount, TransactionKind.Wage, $"Wages for {minutes / 60}h {minutes % 60:00}m as {r.Job.Title.ToLowerInvariant()}");
            return amount;
        }

        /// <summary>Monday morning: income such as a pension comes in, then rent goes out.</summary>
        internal void SettleWeek(Resident r, GameTime time)
        {
            if (r.WeeklyIncomePence > 0)
            {
                r.MoneyPence += r.WeeklyIncomePence;
                Ledger.Add(time, r.Id, r.WeeklyIncomePence, TransactionKind.Income, string.IsNullOrEmpty(r.IncomeLabel) ? "Weekly income" : r.IncomeLabel);
            }

            if (r.WeeklyRentPence > 0)
            {
                r.MoneyPence -= r.WeeklyRentPence;
                Ledger.Add(time, r.Id, -r.WeeklyRentPence, TransactionKind.Rent, "Weekly rent");
            }
        }

        /// <summary>Each business restocks as it opens for the day.</summary>
        internal void RestockOpening(GameTime time)
        {
            foreach (var shop in businesses)
            {
                if (shop.Business.IsOpenOn(time.Weekday) && time.MinuteOfDay == shop.Business.OpenMinuteOfDay)
                {
                    shop.Restock();
                }
            }
        }
    }
}
