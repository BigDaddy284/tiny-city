using System;
using System.Collections.Generic;
using System.Linq;
using TinyTown.Simulation.Time;

namespace TinyTown.Simulation.Economy
{
    /// <summary>Something a business sells. Prices and stock limits are content; the game alone changes stock.</summary>
    public sealed class ShopItem
    {
        public ShopItem(string id, string name, long pricePence, int maxStock, int groceryMeals = 0)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Item id is required.", nameof(id));
            }

            Id = id;
            Name = name ?? id;
            PricePence = Math.Max(0, pricePence);
            MaxStock = Math.Max(1, maxStock);
            GroceryMeals = Math.Max(0, groceryMeals);
        }

        public string Id { get; }

        public string Name { get; }

        public long PricePence { get; }

        /// <summary>Stock after the morning restock.</summary>
        public int MaxStock { get; }

        /// <summary>Meals added to the buyer's groceries at home, for food bought to take home.</summary>
        public int GroceryMeals { get; }
    }

    /// <summary>A shop, café, bakery or clinic: its building, opening hours and what it sells. Static content.</summary>
    public sealed class Business
    {
        public Business(string id, string name, string mapId, int openMinuteOfDay, int closeMinuteOfDay, int openDays, IEnumerable<ShopItem> items)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Business id is required.", nameof(id));
            }

            if (openMinuteOfDay >= closeMinuteOfDay)
            {
                throw new ArgumentException($"Business '{id}' must close after it opens.");
            }

            Id = id;
            Name = name ?? id;
            MapId = mapId;
            OpenMinuteOfDay = openMinuteOfDay;
            CloseMinuteOfDay = closeMinuteOfDay;
            OpenDays = openDays;
            Items = (items ?? Enumerable.Empty<ShopItem>()).ToList();
            if (Items.Select(i => i.Id).Distinct().Count() != Items.Count)
            {
                throw new ArgumentException($"Business '{id}' sells the same item twice.");
            }
        }

        public string Id { get; }

        public string Name { get; }

        /// <summary>The interior map; objects on it belong to the business.</summary>
        public string MapId { get; }

        public int OpenMinuteOfDay { get; }

        public int CloseMinuteOfDay { get; }

        /// <summary>Bit 0 is Monday, as for jobs.</summary>
        public int OpenDays { get; }

        public IReadOnlyList<ShopItem> Items { get; }

        public ShopItem Item(string itemId) => Items.FirstOrDefault(i => i.Id == itemId);

        public bool IsOpenOn(Weekday day) => (OpenDays & (1 << (int)day)) != 0;

        public bool IsOpen(GameTime time) =>
            IsOpenOn(time.Weekday) && time.MinuteOfDay >= OpenMinuteOfDay && time.MinuteOfDay < CloseMinuteOfDay;

        public string HoursText => $"{OpenMinuteOfDay / 60:00}:{OpenMinuteOfDay % 60:00}–{CloseMinuteOfDay / 60:00}:{CloseMinuteOfDay % 60:00}";
    }

    /// <summary>A business's live state: stock and takings. Saved.</summary>
    public sealed class BusinessState
    {
        private readonly Dictionary<string, int> stock = new Dictionary<string, int>();

        internal BusinessState(Business business)
        {
            Business = business;
            Restock();
        }

        public Business Business { get; }

        /// <summary>Everything taken at the till since the game began, in pence.</summary>
        public long TakingsPence { get; internal set; }

        public int Stock(string itemId) => stock.TryGetValue(itemId, out var n) ? n : 0;

        internal void SetStock(string itemId, int amount)
        {
            var item = Business.Item(itemId);
            if (item != null)
            {
                stock[itemId] = Math.Max(0, Math.Min(item.MaxStock, amount));
            }
        }

        internal void Restock()
        {
            foreach (var item in Business.Items)
            {
                stock[item.Id] = item.MaxStock;
            }
        }
    }
}
