using System;
using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation.Activities;
using TinyCity.Simulation.Memory;
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

        public FoodStock(int capacity = MaxPortions)
        {
            Capacity = Math.Max(1, capacity);
        }

        /// <summary>The most portions this stock holds.</summary>
        public int Capacity { get; }

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
            count = Math.Min(count, Capacity - Total);
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

    /// <summary>
    /// Owner, 6 November ("they don't really care about anything or keep their stats up"): the camp's shared store.
    /// What the fishers and farmers bring in, the meals the cooks make and the water the purifiers clean go in it,
    /// and anyone may eat, drink and wash from it, so nobody depends on a hand-over in a chat going right. Saved.
    /// </summary>
    public sealed class CampStore
    {
        /// <summary>The most portions of food it holds.</summary>
        public const int MaxPortions = 240;

        /// <summary>The most portions of clean water it holds.</summary>
        public const int MaxWater = 120;

        public FoodStock Food { get; } = new FoodStock(MaxPortions);

        public int Water { get; internal set; }

        /// <summary>For prompts and the inspector, such as "6 cooked meals, 4 fish; 10 portions of clean water".</summary>
        public string Describe(int today) =>
            $"{(Food.IsEmpty ? "no food" : Food.Describe(today))}; {(Water == 0 ? "no clean water" : $"{Water} portion{(Water == 1 ? string.Empty : "s")} of clean water")}";

        internal void Clear()
        {
            Food.Clear();
            Water = 0;
        }
    }

    /// <summary>
    /// Ground a resident dug where they chose, to plant something of their choosing (Tiny City 2). It stays in the
    /// world as a garden plot until its crop is picked.
    /// </summary>
    public sealed class CropPatch
    {
        public const string IdPrefix = "obj-patch-";

        public CropPatch(string id, Location location, string what, string planterId)
        {
            Id = id;
            Location = location;
            What = what;
            PlanterId = planterId;
        }

        public string Id { get; }

        public Location Location { get; }

        /// <summary>What the planter said they are growing, such as "potatoes"; it grows into vegetables.</summary>
        public string What { get; }

        public string PlanterId { get; }

        public string Name => $"{char.ToUpperInvariant(What[0])}{What.Substring(1)} patch";
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
        internal static string WearNote(Resident r, ToolKind tool)
        {
            var name = CraftingRules.WithArticle(tool, r.Belongings.TierOf(tool));
            return r.Belongings.Wear(tool) ? $" (with {name}, which broke)" : $" (with {name})";
        }

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


        /// <summary>The food activities.</summary>
        public static bool IsMadeFood(ActivityDefinition a) =>
            a == ActivityCatalog.PlantCrops || a == ActivityCatalog.WaterCrops || a == ActivityCatalog.Harvest || a == ActivityCatalog.CatchFish
            || a == ActivityCatalog.Cook || a == ActivityCatalog.EatMeal
            || a == ActivityCatalog.RunStall || a == ActivityCatalog.BuyFood;

        /// <summary>Clean water a resident can carry, and how many portions one go of cleaning makes.</summary>
        public const int MaxCleanWater = 20;

        public const int PurifyPortions = 4;

        /// <summary>
        /// Health lost to a wash in the pond (owner, 6 November: "way too happy to continue drinking and washing in
        /// dirty water"). Pond water can't be drunk and raw food can't be eaten at all (owner, 6 November): only
        /// clean water and cooked meals.
        /// </summary>
        public const double PondWashHealthCost = 1;

        /// <summary>What the decision prompt says beside an option that hurts, so they know before they choose.</summary>
        public static string HealthWarning(Activities.ActivityDefinition a) =>
            a == Activities.ActivityCatalog.WashInPond ? $"dirty water: costs {PondWashHealthCost:0} health"
            : a == Activities.ActivityCatalog.DrinkCleanWater || a == Activities.ActivityCatalog.WashWithCleanWater ? "clean and safe, uses one portion of clean water"
            : null;


        /// <summary>Activities that use up a portion as they start.</summary>
        public static bool EatsFood(ActivityDefinition a) => a == ActivityCatalog.EatMeal;
    }

    /// <summary>
    /// The food supply: the crops on each patch, and the checks and results of the food and other made-things
    /// activities. Nothing is free.
    /// </summary>
    public sealed class FoodSupply
    {
        public const string PlacedSource = "crops";

        /// <summary>The longest name a planter can give what they grow.</summary>
        public const int MaxCropNameLength = 24;

        private readonly Dictionary<string, Crop> crops = new Dictionary<string, Crop>(StringComparer.Ordinal);
        private readonly SortedDictionary<string, CropPatch> patches = new SortedDictionary<string, CropPatch>(StringComparer.Ordinal);
        private TownWorld world;

        /// <summary>Puts the patches in this world (and none from an earlier town). Called by the simulation.</summary>
        internal void Attach(TownWorld townWorld)
        {
            world = townWorld;
            patches.Clear();
            crops.Clear();
            Rebuild();
        }

        /// <summary>The number the next patch's id gets. Saved, so ids are never reused.</summary>
        public int NextPatchNumber { get; internal set; } = 1;

        /// <summary>The camp's shared store of food and clean water.</summary>
        public CampStore Store { get; } = new CampStore();

        public IEnumerable<CropPatch> Patches => patches.Values;

        public CropPatch PatchOf(string objectId) => objectId != null && patches.TryGetValue(objectId, out var p) ? p : null;

        internal CropPatch AddPatch(Location location, string what, string planterId)
        {
            var clean = TextRules.Clean(what, MaxCropNameLength).ToLowerInvariant();
            var patch = new CropPatch($"{CropPatch.IdPrefix}{NextPatchNumber++}", location, clean.Length > 0 ? clean : "vegetables", planterId);
            patches[patch.Id] = patch;
            Rebuild();
            return patch;
        }

        internal void RestorePatch(CropPatch patch)
        {
            patches[patch.Id] = patch;
            Rebuild();
        }

        private void Rebuild() => world?.SetPlaced(PlacedSource, patches.Values.Select(p => new WorldObject(p.Id, p.Name, ObjectType.GardenPlot, p.Location)));

        /// <summary>What grows on this plot or patch, for logs and prompts.</summary>
        public string WhatGrowsAt(string objectId) => PatchOf(objectId)?.What ?? "crops";

        /// <summary>Who is selling food at this stall right now, or null. Set by the simulation.</summary>
        public Func<WorldObject, Resident> SellerAt { get; set; }

        /// <summary>whether the building rules allow this plan-or-build activity at this plot. Set by the simulation.</summary>
        public Func<Resident, ActivityDefinition, WorldObject, bool> BuildingAllows { get; set; }

        /// <summary>whether this resident's role lets them do this activity. Set by the simulation.</summary>
        public Func<Resident, ActivityDefinition, bool> RoleAllows { get; set; }

        /// <summary>whether this activity is their role's own work (a fisher fishing). Set by the simulation.</summary>
        public Func<Resident, ActivityDefinition, bool> Skilled { get; set; }

        /// <summary>whether the town meeting is on at this spot for them to join. Set by the simulation.</summary>
        public Func<Resident, WorldObject, bool> MeetingAllows { get; set; }

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

        /// <summary>Whether the world's rules allow this activity here now (role, food, crops, things, buildings).</summary>
        public static bool Allows(FoodSupply food, Resident r, ActivityDefinition a, WorldObject obj, int today)
        {
            if (food == null)
            {
                return true;
            }

            if (food.RoleAllows != null && !food.RoleAllows(r, a))
            {
                return false;
            }

            if (a == ActivityCatalog.PurifyWater)
            {
                return food.Store.Water < CampStore.MaxWater;
            }

            if (a == ActivityCatalog.DrinkCleanWater || a == ActivityCatalog.WashWithCleanWater)
            {
                return r.CleanWater > 0 || food.Store.Water > 0;
            }

            if (a == ActivityCatalog.AttendMeeting)
            {
                return food.MeetingAllows != null && food.MeetingAllows(r, obj);
            }

            if (Building.BuildingRules.IsBuilding(a))
            {
                return food.BuildingAllows != null && food.BuildingAllows(r, a, obj);
            }

            if (CraftingRules.IsCrafting(a))
            {
                return CraftingRules.Allows(r.Belongings, a);
            }

            var crop = food.CropAt(obj?.Id);
            if (a == ActivityCatalog.PlantCrops)
            {
                // With no object, it is anywhere on open ground the resident chooses; the game checks the spot then.
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

            if (a == ActivityCatalog.Cook)
            {
                return r.Food.Raw > 0 || food.Store.Food.Raw > 0;
            }

            if (a == ActivityCatalog.EatMeal)
            {
                return r.Food.Count(FoodKind.Meal) > 0 || food.Store.Food.Count(FoodKind.Meal) > 0;
            }

            if (a == ActivityCatalog.RunStall)
            {
                return r.Food.Total > 0 && food.SellerAt?.Invoke(obj) == null;
            }

            if (a == ActivityCatalog.BuyFood)
            {
                var seller = food.SellerAt?.Invoke(obj);
                return seller != null && seller != r && seller.Food.Total > 0;
            }

            return true;
        }

        /// <summary>Takes the portion an eating activity uses as it starts. False, with a reason, if there's none.</summary>
        internal bool TakeAtStart(Resident r, ActivityDefinition a, out string reason)
        {
            reason = null;
            var taken = 1;
            if (a == ActivityCatalog.EatMeal)
            {
                // Their own meal first, then one from the camp store.
                taken = r.Food.Take(FoodKind.Meal, 1);
                if (taken == 0)
                {
                    taken = Store.Food.Take(FoodKind.Meal, 1);
                }
            }

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
                return $"Planted {WhatGrowsAt(obj.Id)} at the {obj.Name.ToLowerInvariant()}";
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

                var what = WhatGrowsAt(obj.Id);
                SetCrop(obj.Id, null);
                if (patches.Remove(obj.Id))
                {
                    Rebuild();
                }

                var hoe = r.Belongings.Has(ToolKind.Hoe);
                var kept = Store.Food.Add(FoodKind.Vegetables, FoodRules.HarvestPortions + (hoe ? CraftingRules.HoeExtraPortions : 0), today + FoodRules.VegetableDays);
                return $"Picked {FoodRules.Portions(FoodKind.Vegetables, kept)} ({what}) at the {obj.Name.ToLowerInvariant()} and put them in the camp store" + (hoe ? FoodRules.WearNote(r, ToolKind.Hoe) : string.Empty);
            }

            if (a == ActivityCatalog.CatchFish)
            {
                var rod = r.Belongings.Has(ToolKind.FishingRod);
                var kept = Store.Food.Add(FoodKind.Fish, FoodRules.CatchPortions + (rod ? CraftingRules.RodExtraPortions : 0), today + FoodRules.FishDays);
                return $"Caught {FoodRules.Portions(FoodKind.Fish, kept)} and put them in the camp store" + (rod ? FoodRules.WearNote(r, ToolKind.FishingRod) : string.Empty);
            }

            if (a == ActivityCatalog.Cook)
            {
                var n = r.Food.TakeRaw(FoodRules.CookPortions);
                n += Store.Food.TakeRaw(FoodRules.CookPortions - n);
                if (n == 0)
                {
                    return null;
                }

                var stored = Store.Food.Add(FoodKind.Meal, n, today + FoodRules.MealDays);
                return $"Cooked {FoodRules.Portions(FoodKind.Meal, n)} and put {(stored == n ? "them" : stored.ToString())} in the camp store";
            }

            return null;
        }
    }
}
