using System;
using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation.Activities;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.World;

namespace TinyCity.Simulation.Economy
{
    /// <summary>What food a resident can have in Tiny City 2. Saved by name, so add new kinds at the end.</summary>
    public enum FoodKind
    {
        /// <summary>Picked from a garden plot. Raw, but keeps for a while.</summary>
        Vegetables,

        /// <summary>Caught at a fishing spot. Raw, and goes off quickly.</summary>
        Fish,

        /// <summary>Cooked at a kitchen from raw food. Fills you up best.</summary>
        Meal,
    }

    /// <summary>Some portions of one kind of food that all go off on the same day.</summary>
    public sealed class FoodBatch
    {
        public FoodBatch(FoodKind kind, int count, int goesOffDay)
        {
            Kind = kind;
            Count = count;
            GoesOffDay = goesOffDay;
        }

        public FoodKind Kind { get; }

        public int Count { get; internal set; }

        /// <summary>The game day it is thrown away, at <see cref="FoodRules.SpoilMinute"/>.</summary>
        public int GoesOffDay { get; }
    }

    /// <summary>The food a resident has (Tiny City 2). Portions are used oldest first.</summary>
    public sealed class FoodStock
    {
        /// <summary>No one keeps more than this many portions; the rest is left behind.</summary>
        public const int MaxPortions = 60;

        private readonly List<FoodBatch> batches = new List<FoodBatch>();

        public IReadOnlyList<FoodBatch> Batches => batches;

        public bool IsEmpty => batches.Count == 0;

        public int Total => batches.Sum(b => b.Count);

        public int Count(FoodKind kind) => batches.Where(b => b.Kind == kind).Sum(b => b.Count);

        /// <summary>Vegetables and fish: food that can be eaten as it is or cooked.</summary>
        public int Raw => batches.Where(b => b.Kind != FoodKind.Meal).Sum(b => b.Count);

        /// <summary>Adds portions, as many as there's room for. Returns how many were kept.</summary>
        public int Add(FoodKind kind, int count, int goesOffDay)
        {
            count = Math.Min(count, MaxPortions - Total);
            if (count <= 0)
            {
                return 0;
            }

            var same = batches.FirstOrDefault(b => b.Kind == kind && b.GoesOffDay == goesOffDay);
            if (same != null)
            {
                same.Count += count;
            }
            else
            {
                batches.Add(new FoodBatch(kind, count, goesOffDay));
                batches.Sort((a, b) => a.GoesOffDay != b.GoesOffDay ? a.GoesOffDay.CompareTo(b.GoesOffDay) : a.Kind.CompareTo(b.Kind));
            }

            return count;
        }

        /// <summary>Takes up to <paramref name="count"/> portions of this kind, or of any raw food if null, soonest to go off first.</summary>
        public int Take(FoodKind? kind, int count, bool raw = false)
        {
            var taken = 0;
            foreach (var b in batches)
            {
                if (taken >= count)
                {
                    break;
                }

                if ((kind != null && b.Kind != kind) || (raw && b.Kind == FoodKind.Meal))
                {
                    continue;
                }

                var n = Math.Min(b.Count, count - taken);
                b.Count -= n;
                taken += n;
            }

            batches.RemoveAll(b => b.Count <= 0);
            return taken;
        }

        public int TakeRaw(int count) => Take(null, count, raw: true);

        /// <summary>
        /// Hands up to <paramref name="count"/> portions to someone else, cooked meals first, each keeping the day it
        /// goes off. Returns how many moved (fewer if the other has no room).
        /// </summary>
        public int MoveTo(FoodStock other, int count)
        {
            var moved = 0;
            foreach (var b in batches.OrderBy(b => b.Kind == FoodKind.Meal ? 0 : 1).ThenBy(b => b.GoesOffDay).ToList())
            {
                if (moved >= count)
                {
                    break;
                }

                var n = other.Add(b.Kind, Math.Min(b.Count, count - moved), b.GoesOffDay);
                b.Count -= n;
                moved += n;
            }

            batches.RemoveAll(b => b.Count <= 0);
            return moved;
        }

        /// <summary>Throws away everything that goes off on or before this day. Returns how many portions went.</summary>
        public int Spoil(int day)
        {
            var gone = batches.Where(b => b.GoesOffDay <= day).Sum(b => b.Count);
            batches.RemoveAll(b => b.GoesOffDay <= day);
            return gone;
        }

        internal void Clear() => batches.Clear();

        /// <summary>For prompts and the inspector, such as "3 cooked meals, 2 fish (1 goes off tomorrow)".</summary>
        public string Describe(int today)
        {
            if (IsEmpty)
            {
                return "no food at all";
            }

            var parts = new List<string>();
            foreach (FoodKind kind in Enum.GetValues(typeof(FoodKind)))
            {
                var n = Count(kind);
                if (n > 0)
                {
                    parts.Add(FoodRules.Portions(kind, n));
                }
            }

            var soon = batches.Where(b => b.GoesOffDay <= today + 1).Sum(b => b.Count);
            return string.Join(", ", parts) + (soon > 0 ? $" ({soon} go{(soon == 1 ? "es" : string.Empty)} off by tomorrow morning)" : string.Empty);
        }
    }

    /// <summary>A crop on a garden plot (Tiny City 2): planted, then watered on different days until it can be picked.</summary>
    public sealed class Crop
    {
        public Crop(int plantedDay, int wateredDays, int lastWateredDay)
        {
            PlantedDay = plantedDay;
            WateredDays = wateredDays;
            LastWateredDay = lastWateredDay;
        }

        public int PlantedDay { get; }

        public int WateredDays { get; internal set; }

        /// <summary>0 until it is first watered.</summary>
        public int LastWateredDay { get; internal set; }

        public bool IsReady => WateredDays >= FoodRules.WateredDaysToGrow;

        public string Describe(int today) => IsReady
            ? "ready to pick"
            : $"growing, watered on {WateredDays} of the {FoodRules.WateredDaysToGrow} days it needs" + (LastWateredDay == today ? " (watered today)" : " (not watered today)");
    }

    /// <summary>Every number for Tiny City 2's food, where nothing is free and every meal starts as something grown or caught.</summary>
    public static class FoodRules
    {
        /// <summary>Food that has gone off is thrown away at 04:00, when a plan day starts.</summary>
        public const int SpoilMinute = 4 * 60;

        public const int VegetableDays = 5;
        public const int FishDays = 2;
        public const int MealDays = 3;

        /// <summary>A crop is ready once it has been watered on this many different days, from the day it was planted.</summary>
        public const int WateredDaysToGrow = 2;

        /// <summary>Vegetables picked from a ready crop.</summary>
        public const int HarvestPortions = 12;

        /// <summary>Fish caught in one go at a fishing spot.</summary>
        public const int CatchPortions = 3;

        /// <summary>Raw portions one cooking turns into as many meals.</summary>
        public const int CookPortions = 4;

        /// <summary>Uses a tool once, and says so if it broke.</summary>
        internal static string WearNote(Resident r, ToolKind tool) =>
            r.Belongings.Wear(tool) ? $" (with {CraftingRules.WithArticle(tool)}, which broke)" : $" (with {CraftingRules.WithArticle(tool)})";

        /// <summary>What a portion costs at someone's stall: the game's fixed prices, so a seller only chooses to sell.</summary>
        public const long MealPricePence = 600;

        public const long RawPricePence = 250;

        /// <summary>The most portions offered in one go in a chat, and the highest price asked for them.</summary>
        public const int MaxOfferPortions = 6;

        public const long MaxOfferPence = 5000;

        public static int DaysToKeep(FoodKind kind) => kind == FoodKind.Fish ? FishDays : kind == FoodKind.Meal ? MealDays : VegetableDays;

        public static string Portions(FoodKind kind, int n)
        {
            switch (kind)
            {
                case FoodKind.Meal:
                    return n == 1 ? "1 cooked meal" : $"{n} cooked meals";
                case FoodKind.Fish:
                    return $"{n} fish";
                default:
                    return n == 1 ? "1 portion of vegetables" : $"{n} portions of vegetables";
            }
        }

        /// <summary>v1's food, which comes from nowhere: none of it exists when food must be made.</summary>
        public static bool IsFreeFood(ActivityDefinition a) =>
            a == ActivityCatalog.Eat || a == ActivityCatalog.Snack || a == ActivityCatalog.Leftovers || a == ActivityCatalog.BuyGroceries
            || a == ActivityCatalog.CafeMeal || a == ActivityCatalog.Pastry;

        /// <summary>Tiny City 2's food activities, which don't exist in v1.</summary>
        public static bool IsMadeFood(ActivityDefinition a) =>
            a == ActivityCatalog.PlantCrops || a == ActivityCatalog.WaterCrops || a == ActivityCatalog.Harvest || a == ActivityCatalog.CatchFish
            || a == ActivityCatalog.Cook || a == ActivityCatalog.EatMeal || a == ActivityCatalog.EatRaw
            || a == ActivityCatalog.RunStall || a == ActivityCatalog.BuyFood;

        /// <summary>Activities that use up a portion as they start.</summary>
        public static bool EatsFood(ActivityDefinition a) => a == ActivityCatalog.EatMeal || a == ActivityCatalog.EatRaw;
    }

    /// <summary>
    /// Tiny City 2's food supply: the crops on each garden plot, and the checks and results of the food activities.
    /// Off (<see cref="MustBeMade"/> false) it changes nothing, so v1 towns keep v1's free food.
    /// </summary>
    public sealed class FoodSupply
    {
        private readonly Dictionary<string, Crop> crops = new Dictionary<string, Crop>(StringComparer.Ordinal);

        public bool MustBeMade { get; set; }

        /// <summary>Who is selling food at this stall right now, or null. Set by the simulation.</summary>
        public Func<WorldObject, Resident> SellerAt { get; set; }

        public IReadOnlyDictionary<string, Crop> Crops => crops;

        public Crop CropAt(string objectId) => objectId != null && crops.TryGetValue(objectId, out var c) ? c : null;

        internal void SetCrop(string objectId, Crop crop)
        {
            if (crop == null)
            {
                crops.Remove(objectId);
            }
            else
            {
                crops[objectId] = crop;
            }
        }

        /// <summary>Whether food allows this activity here now. Without a supply, only v1's food exists.</summary>
        public static bool Allows(FoodSupply food, Resident r, ActivityDefinition a, WorldObject obj, int today)
        {
            if (food == null || !food.MustBeMade)
            {
                return !FoodRules.IsMadeFood(a) && !CraftingRules.IsCrafting(a);
            }

            if (a == ActivityCatalog.GatherStone)
            {
                return r.Belongings.Has(ToolKind.Pickaxe);
            }

            var tool = CraftingRules.ToolMadeBy(a);
            if (tool != null)
            {
                return !r.Belongings.Has(tool.Value) && r.Belongings.Count(MaterialKind.Wood) >= CraftingRules.WoodFor(tool.Value);
            }

            if (FoodRules.IsFreeFood(a))
            {
                return false;
            }

            var crop = food.CropAt(obj?.Id);
            if (a == ActivityCatalog.PlantCrops)
            {
                return crop == null;
            }

            if (a == ActivityCatalog.WaterCrops)
            {
                return crop != null && !crop.IsReady && crop.LastWateredDay != today;
            }

            if (a == ActivityCatalog.Harvest)
            {
                return crop != null && crop.IsReady;
            }

            if (a == ActivityCatalog.Cook || a == ActivityCatalog.EatRaw)
            {
                return r.Food.Raw > 0;
            }

            if (a == ActivityCatalog.EatMeal)
            {
                return r.Food.Count(FoodKind.Meal) > 0;
            }

            if (a == ActivityCatalog.RunStall)
            {
                return r.Food.Total > 0 && food.SellerAt?.Invoke(obj) == null;
            }

            if (a == ActivityCatalog.BuyFood)
            {
                var seller = food.SellerAt?.Invoke(obj);
                return seller != null && seller != r && seller.Food.Total > 0 && r.MoneyPence >= PriceOfNext(seller.Food);
            }

            return true;
        }

        /// <summary>What the next portion from this stock sells for: a cooked meal if there is one.</summary>
        public static long PriceOfNext(FoodStock stock) => stock.Count(FoodKind.Meal) > 0 ? FoodRules.MealPricePence : FoodRules.RawPricePence;

        /// <summary>Takes the portion an eating activity uses as it starts. False, with a reason, if there's none.</summary>
        internal bool TakeAtStart(Resident r, ActivityDefinition a, out string reason)
        {
            reason = null;
            var taken = a == ActivityCatalog.EatMeal ? r.Food.Take(FoodKind.Meal, 1) : a == ActivityCatalog.EatRaw ? r.Food.TakeRaw(1) : 1;
            if (taken == 0)
            {
                reason = "they had no food left";
                return false;
            }

            return true;
        }

        /// <summary>What a finished food activity made, for the log, or null if it made nothing.</summary>
        internal string Finish(Resident r, ActivityDefinition a, WorldObject obj, int today)
        {
            var crop = CropAt(obj?.Id);
            if (a == ActivityCatalog.PlantCrops)
            {
                if (crop != null)
                {
                    return null;
                }

                SetCrop(obj.Id, new Crop(today, 0, 0));
                return $"Planted crops at the {obj.Name.ToLowerInvariant()}";
            }

            if (a == ActivityCatalog.WaterCrops)
            {
                if (crop == null || crop.IsReady || crop.LastWateredDay == today)
                {
                    return null;
                }

                crop.WateredDays++;
                crop.LastWateredDay = today;
                return crop.IsReady ? $"Watered the crops at the {obj.Name.ToLowerInvariant()}; they're ready to pick" : $"Watered the crops at the {obj.Name.ToLowerInvariant()}";
            }

            if (a == ActivityCatalog.Harvest)
            {
                if (crop == null || !crop.IsReady)
                {
                    return null;
                }

                SetCrop(obj.Id, null);
                var hoe = r.Belongings.Has(ToolKind.Hoe);
                var kept = r.Food.Add(FoodKind.Vegetables, FoodRules.HarvestPortions + (hoe ? CraftingRules.HoeExtraPortions : 0), today + FoodRules.VegetableDays);
                return $"Picked {FoodRules.Portions(FoodKind.Vegetables, kept)} at the {obj.Name.ToLowerInvariant()}" + (hoe ? FoodRules.WearNote(r, ToolKind.Hoe) : string.Empty);
            }

            if (a == ActivityCatalog.CatchFish)
            {
                var rod = r.Belongings.Has(ToolKind.FishingRod);
                var kept = r.Food.Add(FoodKind.Fish, FoodRules.CatchPortions + (rod ? CraftingRules.RodExtraPortions : 0), today + FoodRules.FishDays);
                return $"Caught {FoodRules.Portions(FoodKind.Fish, kept)}" + (rod ? FoodRules.WearNote(r, ToolKind.FishingRod) : string.Empty);
            }

            if (a == ActivityCatalog.Cook)
            {
                var n = r.Food.TakeRaw(FoodRules.CookPortions);
                if (n == 0)
                {
                    return null;
                }

                r.Food.Add(FoodKind.Meal, n, today + FoodRules.MealDays);
                return $"Cooked {FoodRules.Portions(FoodKind.Meal, n)}";
            }

            return null;
        }
    }
}
