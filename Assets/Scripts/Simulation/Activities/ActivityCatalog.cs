using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.World;

namespace TinyCity.Simulation.Activities
{
    /// <summary>Every activity a resident can choose. Ids are stable because save files store them.</summary>
    public static class ActivityCatalog
    {
        /// <summary>The open land has no taps or showers, so people drink and wash at the pond.</summary>
        public static readonly ActivityDefinition DrinkFromPond = new ActivityDefinition(
            "drink-pond", "Drink dirty pond water", "drinking dirty pond water", ActivityKind.Care, NeedType.Thirst, new[] { ObjectType.FishingSpot }, 5,
            new Dictionary<NeedType, double> { [NeedType.Thirst] = 10 });

        public static readonly ActivityDefinition WashInPond = new ActivityDefinition(
            "wash-pond", "Wash in the dirty pond", "washing in the dirty pond", ActivityKind.Care, NeedType.Hygiene, new[] { ObjectType.FishingSpot }, 20,
            new Dictionary<NeedType, double> { [NeedType.Hygiene] = 5 });

        /// <summary>cleaning pond water to drink, which only the water purifier can do.</summary>
        public static readonly ActivityDefinition PurifyWater = new ActivityDefinition(
            "purify-water", "Clean pond water", "cleaning water", ActivityKind.Errand, null, new[] { ObjectType.FishingSpot }, 45,
            new Dictionary<NeedType, double> { [NeedType.Fun] = 0.2 });

        /// <summary>drinking a portion of clean water, which quenches thirst properly and does no harm.</summary>
        public static readonly ActivityDefinition DrinkCleanWater = new ActivityDefinition(
            "drink-clean", "Drink clean water", "drinking clean water", ActivityKind.Care, NeedType.Thirst, new[] { ObjectType.Kitchen, ObjectType.Bench, ObjectType.FishingSpot }, 5,
            new Dictionary<NeedType, double> { [NeedType.Thirst] = 14 });

        /// <summary>the weekly town meeting, where issues are raised and role changes are voted on.</summary>
        public static readonly ActivityDefinition AttendMeeting = new ActivityDefinition(
            "attend-meeting", "Go to the town meeting", "at the town meeting", ActivityKind.Leisure, NeedType.Social, new[] { ObjectType.GatheringSpot }, 90,
            new Dictionary<NeedType, double> { [NeedType.Social] = 1.0 });

        /// <summary>Drinking and washing out in the open, where nothing is laid on.</summary>
        public static bool IsInTheWild(ActivityDefinition a) => a == DrinkFromPond || a == WashInPond;

        /// <summary>Walking over to someone and talking with them. The duration is set by the conversation, not here.</summary>
        public static readonly ActivityDefinition Chat = new ActivityDefinition(
            "chat", "Chat", "chatting", ActivityKind.Social, NeedType.Social, new ObjectType[0], 20,
            new Dictionary<NeedType, double> { [NeedType.Social] = 2.2, [NeedType.Fun] = 0.4 });

        // Food (see Economy.FoodRules): nothing is free.

        /// <summary>Sowing a crop on an empty garden plot, or on open ground the resident chooses. It still has to be watered before it can be picked.</summary>
        public static readonly ActivityDefinition PlantCrops = new ActivityDefinition(
            "plant-crops", "Plant crops", "planting crops", ActivityKind.Errand, null, new[] { ObjectType.GardenPlot }, 45,
            new Dictionary<NeedType, double>(),
            new Dictionary<NeedType, double> { [NeedType.Hygiene] = 2.0, [NeedType.Thirst] = 1.3 });

        /// <summary>Watering a growing crop, once a day.</summary>
        public static readonly ActivityDefinition WaterCrops = new ActivityDefinition(
            "water-crops", "Water the crops", "watering the crops", ActivityKind.Errand, null, new[] { ObjectType.GardenPlot }, 15,
            new Dictionary<NeedType, double>());

        /// <summary>Picking a ready crop: vegetables for whoever picks it.</summary>
        public static readonly ActivityDefinition Harvest = new ActivityDefinition(
            "harvest", "Pick the crops", "picking vegetables", ActivityKind.Errand, null, new[] { ObjectType.GardenPlot }, 30,
            new Dictionary<NeedType, double>(),
            new Dictionary<NeedType, double> { [NeedType.Hygiene] = 1.5 });

        /// <summary>Fishing for food rather than fun: anyone can, and it gives fish.</summary>
        public static readonly ActivityDefinition CatchFish = new ActivityDefinition(
            "catch-fish", "Fish for food", "fishing for food", ActivityKind.Errand, null, new[] { ObjectType.FishingSpot }, 60,
            new Dictionary<NeedType, double> { [NeedType.Fun] = 0.3 });

        /// <summary>Turning raw food into cooked meals, which keep and fill you up better.</summary>
        public static readonly ActivityDefinition Cook = new ActivityDefinition(
            "cook", "Cook meals", "cooking", ActivityKind.Errand, null, new[] { ObjectType.Kitchen }, 40,
            new Dictionary<NeedType, double> { [NeedType.Fun] = 0.2 });

        /// <summary>Eating one cooked meal.</summary>
        public static readonly ActivityDefinition EatMeal = new ActivityDefinition(
            "eat-meal", "Eat a cooked meal", "eating a meal", ActivityKind.Care, NeedType.Hunger, new[] { ObjectType.Kitchen, ObjectType.Bench }, 25,
            new Dictionary<NeedType, double> { [NeedType.Hunger] = 3.2, [NeedType.Fun] = 0.2 });

        /// <summary>Eating raw vegetables or fish: it does, but not for long.</summary>
        public static readonly ActivityDefinition EatRaw = new ActivityDefinition(
            "eat-raw", "Eat something raw", "eating raw food", ActivityKind.Care, NeedType.Hunger, new[] { ObjectType.Kitchen, ObjectType.Bench }, 15,
            new Dictionary<NeedType, double> { [NeedType.Hunger] = 2.0 });

        /// <summary>Selling food from a market stall: others nearby can buy a portion each at the game's price.</summary>
        public static readonly ActivityDefinition RunStall = new ActivityDefinition(
            "run-stall", "Sell food at a stall", "selling food at a market stall", ActivityKind.Errand, null, new[] { ObjectType.MarketStall }, 90,
            new Dictionary<NeedType, double> { [NeedType.Social] = 0.4 });

        /// <summary>Buying a portion of food from someone selling at a stall.</summary>
        public static readonly ActivityDefinition BuyFood = new ActivityDefinition(
            "buy-food", "Buy food at a stall", "buying food at a stall", ActivityKind.Errand, null, new[] { ObjectType.MarketStall }, 5,
            new Dictionary<NeedType, double> { [NeedType.Social] = 0.5 });

        // Gathering and crafting (see Economy.CraftingRules).

        /// <summary>Picking up fallen branches by hand: a little wood, no tool needed.</summary>
        public static readonly ActivityDefinition GatherSticks = Gather("gather-sticks", "Pick up fallen branches", "picking up fallen branches", ObjectType.StickPile);

        /// <summary>Picking up loose stones by hand: a little stone, no tool needed.</summary>
        public static readonly ActivityDefinition GatherLooseStones = Gather("gather-loose-stones", "Pick up loose stones", "picking up loose stones", ObjectType.LooseStones);

        /// <summary>Chopping wood from the trees, which needs an axe.</summary>
        public static readonly ActivityDefinition GatherWood = new ActivityDefinition(
            "gather-wood", "Gather wood", "gathering wood", ActivityKind.Errand, null, new[] { ObjectType.Woodland }, 60,
            new Dictionary<NeedType, double>(),
            new Dictionary<NeedType, double> { [NeedType.Hygiene] = 2.0, [NeedType.Thirst] = 1.4 });

        /// <summary>Breaking stone off the rocks, which needs a pickaxe.</summary>
        public static readonly ActivityDefinition GatherStone = new ActivityDefinition(
            "gather-stone", "Break stone", "breaking stone", ActivityKind.Errand, null, new[] { ObjectType.RockPile }, 60,
            new Dictionary<NeedType, double>(),
            new Dictionary<NeedType, double> { [NeedType.Hygiene] = 2.2, [NeedType.Thirst] = 1.5 });

        /// <summary>Hardwood: needs a stone axe or better.</summary>
        public static readonly ActivityDefinition GatherHardwood = Gather("gather-hardwood", "Cut hardwood", "cutting hardwood", ObjectType.Woodland);

        /// <summary>Heartwood: needs an iron axe.</summary>
        public static readonly ActivityDefinition GatherHeartwood = Gather("gather-heartwood", "Cut heartwood", "cutting heartwood", ObjectType.Woodland);

        /// <summary>Iron ore: needs a stone pickaxe or better.</summary>
        public static readonly ActivityDefinition GatherIron = Gather("gather-iron", "Mine iron ore", "mining iron ore", ObjectType.RockPile);

        /// <summary>Crystal: needs an iron pickaxe.</summary>
        public static readonly ActivityDefinition GatherCrystal = Gather("gather-crystal", "Mine crystal", "mining crystal", ObjectType.RockPile);

        public static readonly ActivityDefinition CraftAxe = Craft("craft-axe", "Make a wooden axe", "making a wooden axe");

        public static readonly ActivityDefinition CraftPickaxe = Craft("craft-pickaxe", "Make a wooden pickaxe", "making a wooden pickaxe");

        public static readonly ActivityDefinition CraftStoneAxe = Craft("craft-stone-axe", "Make a stone axe", "making a stone axe");

        public static readonly ActivityDefinition CraftStonePickaxe = Craft("craft-stone-pickaxe", "Make a stone pickaxe", "making a stone pickaxe");

        public static readonly ActivityDefinition CraftIronAxe = Craft("craft-iron-axe", "Make an iron axe", "making an iron axe");

        public static readonly ActivityDefinition CraftIronPickaxe = Craft("craft-iron-pickaxe", "Make an iron pickaxe", "making an iron pickaxe");

        public static readonly ActivityDefinition CraftHoe = Craft("craft-hoe", "Make a hoe", "making a hoe");

        public static readonly ActivityDefinition CraftRod = Craft("craft-rod", "Make a fishing rod", "making a fishing rod");

        /// <summary>sketching a building on open ground the resident chooses; the plan itself comes from the AI.</summary>
        public static readonly ActivityDefinition PlanBuilding = new ActivityDefinition(
            "plan-building", "Plan a building", "planning a building", ActivityKind.Errand, null, new[] { ObjectType.BuildPlot }, 30,
            new Dictionary<NeedType, double> { [NeedType.Fun] = 0.5 });

        /// <summary>laying the next blocks of their own building, at its site.</summary>
        public static readonly ActivityDefinition Build = new ActivityDefinition(
            "build", "Build", "building", ActivityKind.Errand, null, new[] { ObjectType.BuildPlot }, 60,
            new Dictionary<NeedType, double> { [NeedType.Fun] = 0.3 },
            new Dictionary<NeedType, double> { [NeedType.Hygiene] = 1.8, [NeedType.Thirst] = 1.4 });

        /// <summary>activities done on open ground wherever the resident chooses, rather than at a thing.</summary>
        public static bool IsDoneOnGround(ActivityDefinition a) => a == PlantCrops || a == PlanBuilding;

        /// <summary>In a fixed order; the planner uses it to break ties deterministically.</summary>
        public static readonly IReadOnlyList<ActivityDefinition> All = new[]
        {
            Chat,
            PlantCrops, WaterCrops, Harvest, CatchFish, Cook, EatMeal, EatRaw, RunStall, BuyFood,
            GatherWood, GatherStone, CraftAxe, CraftPickaxe, CraftHoe, CraftRod,
            GatherHardwood, GatherHeartwood, GatherIron, GatherCrystal, CraftStoneAxe, CraftStonePickaxe, CraftIronAxe, CraftIronPickaxe,
            PlanBuilding, Build, GatherSticks, GatherLooseStones, DrinkFromPond, WashInPond,
            PurifyWater, DrinkCleanWater, AttendMeeting,
        };

        private static ActivityDefinition Gather(string id, string name, string verb, ObjectType where) => new ActivityDefinition(
            id, name, verb, ActivityKind.Errand, null, new[] { where }, 60,
            new Dictionary<NeedType, double>(),
            new Dictionary<NeedType, double> { [NeedType.Hygiene] = 2.2, [NeedType.Thirst] = 1.5 });

        private static ActivityDefinition Craft(string id, string name, string verb) => new ActivityDefinition(
            id, name, verb, ActivityKind.Errand, null, new[] { ObjectType.Workbench }, 30,
            new Dictionary<NeedType, double> { [NeedType.Fun] = 0.4 });

        public static bool TryGet(string id, out ActivityDefinition activity)
        {
            activity = All.FirstOrDefault(a => a.Id == id);
            return activity != null;
        }
    }
}
