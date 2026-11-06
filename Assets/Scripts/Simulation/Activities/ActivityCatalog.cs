using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.World;

namespace TinyCity.Simulation.Activities
{
    /// <summary>Every activity a resident can choose. Ids are stable because save files store them.</summary>
    public static class ActivityCatalog
    {
        public static readonly ActivityDefinition Sleep = new ActivityDefinition(
            "sleep", "Sleep", "sleeping", ActivityKind.Sleep, NeedType.Energy, new[] { ObjectType.Bed, ObjectType.Bench, ObjectType.SleepingSpot }, 10 * 60,
            new Dictionary<NeedType, double> { [NeedType.Energy] = 12.5 / 60 },
            new Dictionary<NeedType, double>
            {
                [NeedType.Energy] = 0,
                [NeedType.Hunger] = 0.4,
                [NeedType.Thirst] = 0.4,
                [NeedType.Toilet] = 0.4,
                [NeedType.Hygiene] = 0.5,
                [NeedType.Social] = 0,
                [NeedType.Exercise] = 0,
                [NeedType.Fun] = 0,
            });

        /// <summary>A proper meal at home. Uses one meal's worth of groceries.</summary>
        public static readonly ActivityDefinition Eat = new ActivityDefinition(
            "eat", "Cook and eat", "cooking and eating", ActivityKind.Care, NeedType.Hunger, new[] { ObjectType.Kitchen }, 30,
            new Dictionary<NeedType, double> { [NeedType.Hunger] = 2.6, [NeedType.Thirst] = 0.4 },
            usesGroceries: true);

        /// <summary>Odds and ends from the cupboard: always possible, so hunger can never be blocked by an empty larder or purse.</summary>
        public static readonly ActivityDefinition Snack = new ActivityDefinition(
            "snack", "Make do with a snack", "eating whatever's in the cupboard", ActivityKind.Care, NeedType.Hunger, new[] { ObjectType.Kitchen }, 15,
            new Dictionary<NeedType, double> { [NeedType.Hunger] = 2.0 });

        /// <summary>What the market stalls give away at the end of the day. Only for the homeless, who have no kitchen for a snack.</summary>
        public static readonly ActivityDefinition Leftovers = new ActivityDefinition(
            "leftovers", "Ask for leftovers", "getting by on leftovers from the market stalls", ActivityKind.Care, NeedType.Hunger, new[] { ObjectType.MarketStall }, 15,
            new Dictionary<NeedType, double> { [NeedType.Hunger] = 2.0 });

        public static readonly ActivityDefinition CafeMeal = new ActivityDefinition(
            "cafe-meal", "Eat at the café", "having a meal at the café", ActivityKind.Care, NeedType.Hunger, new[] { ObjectType.CafeTable }, 40,
            new Dictionary<NeedType, double> { [NeedType.Hunger] = 2.4, [NeedType.Thirst] = 0.6, [NeedType.Social] = 0.5, [NeedType.Fun] = 0.6 },
            itemId: "cafe-meal", shared: true);

        public static readonly ActivityDefinition Pastry = new ActivityDefinition(
            "pastry", "Buy a pastry", "enjoying a pastry", ActivityKind.Care, NeedType.Hunger, new[] { ObjectType.BakeryDisplay }, 10,
            new Dictionary<NeedType, double> { [NeedType.Hunger] = 3.0, [NeedType.Fun] = 0.8 },
            itemId: "pastry");

        public static readonly ActivityDefinition Drink = new ActivityDefinition(
            "drink", "Drink water", "having a drink", ActivityKind.Care, NeedType.Thirst, new[] { ObjectType.Sink, ObjectType.GardenTap }, 5,
            new Dictionary<NeedType, double> { [NeedType.Thirst] = 14 });

        public static readonly ActivityDefinition UseToilet = new ActivityDefinition(
            "toilet", "Use the toilet", "using the toilet", ActivityKind.Care, NeedType.Toilet, new[] { ObjectType.Toilet }, 5,
            new Dictionary<NeedType, double> { [NeedType.Toilet] = 20 });

        public static readonly ActivityDefinition Shower = new ActivityDefinition(
            "shower", "Shower", "showering", ActivityKind.Care, NeedType.Hygiene, new[] { ObjectType.Shower }, 15,
            new Dictionary<NeedType, double> { [NeedType.Hygiene] = 6.5 });

        /// <summary>Tiny City 2: the open land has no taps, toilets or showers, so people drink and wash at the pond and go among the trees.</summary>
        public static readonly ActivityDefinition DrinkFromPond = new ActivityDefinition(
            "drink-pond", "Drink from the pond", "drinking from the pond", ActivityKind.Care, NeedType.Thirst, new[] { ObjectType.FishingSpot }, 5,
            new Dictionary<NeedType, double> { [NeedType.Thirst] = 10 });

        public static readonly ActivityDefinition WashInPond = new ActivityDefinition(
            "wash-pond", "Wash in the pond", "washing in the pond", ActivityKind.Care, NeedType.Hygiene, new[] { ObjectType.FishingSpot }, 20,
            new Dictionary<NeedType, double> { [NeedType.Hygiene] = 5 });

        /// <summary>Tiny City 2: cleaning pond water to drink, which only the water purifier can do.</summary>
        public static readonly ActivityDefinition PurifyWater = new ActivityDefinition(
            "purify-water", "Clean pond water", "cleaning water", ActivityKind.Errand, null, new[] { ObjectType.FishingSpot }, 45,
            new Dictionary<NeedType, double> { [NeedType.Fun] = 0.2 });

        /// <summary>Tiny City 2: drinking a portion of clean water, which quenches thirst properly and does no harm.</summary>
        public static readonly ActivityDefinition DrinkCleanWater = new ActivityDefinition(
            "drink-clean", "Drink clean water", "drinking clean water", ActivityKind.Care, NeedType.Thirst, new[] { ObjectType.Kitchen, ObjectType.Bench, ObjectType.FishingSpot }, 5,
            new Dictionary<NeedType, double> { [NeedType.Thirst] = 14 });

        /// <summary>Tiny City 2: the weekly town meeting, where issues are raised and role changes are voted on.</summary>
        public static readonly ActivityDefinition AttendMeeting = new ActivityDefinition(
            "attend-meeting", "Go to the town meeting", "at the town meeting", ActivityKind.Leisure, NeedType.Social, new[] { ObjectType.GatheringSpot }, 90,
            new Dictionary<NeedType, double> { [NeedType.Social] = 1.0 });

        /// <summary>Activities that exist only on Tiny City 2's open land (never in v1).</summary>
        public static bool IsOpenLandOnly(ActivityDefinition a) => IsInTheWild(a) || a == PurifyWater || a == DrinkCleanWater || a == AttendMeeting;

        /// <summary>Drinking, washing and the toilet out in the open: Tiny City 2 only, where nothing is laid on.</summary>
        public static bool IsInTheWild(ActivityDefinition a) => a == DrinkFromPond || a == WashInPond;

        public static readonly ActivityDefinition Work = new ActivityDefinition(
            "work", "Work", "working", ActivityKind.Work, null, new[] { ObjectType.GardenPlot, ObjectType.Workbench, ObjectType.Counter }, 8 * 60,
            new Dictionary<NeedType, double> { [NeedType.Exercise] = 0.03 },
            new Dictionary<NeedType, double> { [NeedType.Hygiene] = 1.5, [NeedType.Thirst] = 1.2, [NeedType.Energy] = 1.1, [NeedType.Fun] = 0.8 });

        public static readonly ActivityDefinition Exercise = new ActivityDefinition(
            "exercise", "Exercise", "exercising", ActivityKind.Leisure, NeedType.Exercise, new[] { ObjectType.ExerciseSpot }, 45,
            new Dictionary<NeedType, double> { [NeedType.Exercise] = 2.2, [NeedType.Fun] = 0.3 },
            new Dictionary<NeedType, double> { [NeedType.Energy] = 2.0, [NeedType.Hygiene] = 3.0, [NeedType.Thirst] = 1.5 });

        public static readonly ActivityDefinition Read = new ActivityDefinition(
            "read", "Read a book", "reading", ActivityKind.Leisure, NeedType.Fun, new[] { ObjectType.Armchair }, 60,
            new Dictionary<NeedType, double> { [NeedType.Fun] = 1.0 });

        public static readonly ActivityDefinition WatchTown = new ActivityDefinition(
            "watch-town", "Sit in the park", "sitting in the park watching the town", ActivityKind.Leisure, NeedType.Social, new[] { ObjectType.Bench, ObjectType.Fountain }, 45,
            new Dictionary<NeedType, double> { [NeedType.Fun] = 0.6, [NeedType.Social] = 0.7 });

        public static readonly ActivityDefinition Coffee = new ActivityDefinition(
            "coffee", "Have a coffee", "having a coffee at the café", ActivityKind.Leisure, NeedType.Fun, new[] { ObjectType.CafeTable }, 25,
            new Dictionary<NeedType, double> { [NeedType.Fun] = 1.0, [NeedType.Thirst] = 1.5, [NeedType.Social] = 0.5, [NeedType.Energy] = 0.1 },
            itemId: "coffee", shared: true);

        /// <summary>Stocking up the kitchen at home. An errand: chosen when groceries run low, not to restore a need.</summary>
        public static readonly ActivityDefinition BuyGroceries = new ActivityDefinition(
            "buy-groceries", "Buy groceries", "shopping for groceries", ActivityKind.Errand, null, new[] { ObjectType.ShopShelf }, 15,
            new Dictionary<NeedType, double> { [NeedType.Fun] = 0.1 },
            itemId: "groceries");

        /// <summary>Lying down at home to get over an illness. Not chosen for a need; the planner picks it for anyone unwell.</summary>
        public static readonly ActivityDefinition Rest = new ActivityDefinition(
            "rest", "Rest in bed", "resting in bed, feeling poorly", ActivityKind.Care, null, new[] { ObjectType.Bed }, 90,
            new Dictionary<NeedType, double> { [NeedType.Energy] = 4.0 / 60 },
            new Dictionary<NeedType, double> { [NeedType.Energy] = 0, [NeedType.Hunger] = 0.6, [NeedType.Thirst] = 0.6, [NeedType.Exercise] = 0.2, [NeedType.Fun] = 0.6 });

        /// <summary>A visit to the clinic, free of charge, while it's open. Treats a serious illness so recovery can begin.</summary>
        public static readonly ActivityDefinition SeeDoctor = new ActivityDefinition(
            "see-doctor", "See the doctor", "being seen by the doctor", ActivityKind.Care, null, new[] { ObjectType.ClinicBed }, 45,
            new Dictionary<NeedType, double> { [NeedType.Social] = 0.3 },
            needsOpenBusiness: true);

        public static readonly ActivityDefinition Paint = Hobby("paint", "Paint", "painting at the easel", "painting", ObjectType.Easel, 60,
            new Dictionary<NeedType, double> { [NeedType.Fun] = 1.2 });

        public static readonly ActivityDefinition PlayPiano = Hobby("play-piano", "Play the piano", "playing the piano", "piano", ObjectType.Piano, 45,
            new Dictionary<NeedType, double> { [NeedType.Fun] = 1.2, [NeedType.Social] = 0.2 });

        public static readonly ActivityDefinition Chess = Hobby("chess", "Play chess", "playing chess", "chess", ObjectType.ChessTable, 60,
            new Dictionary<NeedType, double> { [NeedType.Fun] = 1.0 }, shared: true);

        public static readonly ActivityDefinition Fish = Hobby("fish", "Go fishing", "fishing at the pond", "fishing", ObjectType.FishingSpot, 75,
            new Dictionary<NeedType, double> { [NeedType.Fun] = 0.9 }, shared: true);

        public static readonly ActivityDefinition Garden = Hobby("garden", "Tend the garden", "pottering in the community garden", "gardening", ObjectType.GardenPlot, 50,
            new Dictionary<NeedType, double> { [NeedType.Fun] = 0.8, [NeedType.Exercise] = 0.6 }, shared: true);

        public static readonly ActivityDefinition Bake = Hobby("bake", "Bake something", "baking at home", "baking", ObjectType.Kitchen, 45,
            new Dictionary<NeedType, double> { [NeedType.Fun] = 1.0, [NeedType.Hunger] = 0.3 });

        public static readonly ActivityDefinition Knit = Hobby("knit", "Knit", "knitting", "knitting", ObjectType.Armchair, 60,
            new Dictionary<NeedType, double> { [NeedType.Fun] = 1.0 });

        /// <summary>Walking over to someone and talking with them. The duration is set by the conversation, not here.</summary>
        public static readonly ActivityDefinition Chat = new ActivityDefinition(
            "chat", "Chat", "chatting", ActivityKind.Social, NeedType.Social, new ObjectType[0], 20,
            new Dictionary<NeedType, double> { [NeedType.Social] = 2.2, [NeedType.Fun] = 0.4 });

        /// <summary>
        /// Going to a community event on the calendar. Only possible at the event's spot while it's on, and lasts until
        /// it ends; see <see cref="Community.CommunityCalendar"/>.
        /// </summary>
        public static readonly ActivityDefinition AttendEvent = new ActivityDefinition(
            "attend-event", "Go to a city event", "joining in at a city event", ActivityKind.Leisure, NeedType.Social, new[] { ObjectType.GatheringSpot }, 3 * 60,
            new Dictionary<NeedType, double> { [NeedType.Social] = 1.2, [NeedType.Fun] = 1.0 });

        /// <summary>A drink at the neon bar in the evening, ideally with company. Paid for, so only while the bar is open.</summary>
        public static readonly ActivityDefinition BarDrink = new ActivityDefinition(
            "bar-drink", "Have a drink at the bar", "having a drink at the neon bar", ActivityKind.Leisure, NeedType.Fun, new[] { ObjectType.BarTable }, 40,
            new Dictionary<NeedType, double> { [NeedType.Fun] = 1.1, [NeedType.Social] = 0.9, [NeedType.Thirst] = 1.0 },
            itemId: "cocktail", shared: true);

        /// <summary>Dancing at the bar while it's open: free, good fun and good exercise, and tiring.</summary>
        public static readonly ActivityDefinition Dance = new ActivityDefinition(
            "dance", "Go dancing", "dancing at the neon bar", ActivityKind.Leisure, NeedType.Fun, new[] { ObjectType.DanceFloor }, 40,
            new Dictionary<NeedType, double> { [NeedType.Fun] = 1.3, [NeedType.Social] = 0.6, [NeedType.Exercise] = 1.2 },
            new Dictionary<NeedType, double> { [NeedType.Energy] = 2.0, [NeedType.Hygiene] = 2.0, [NeedType.Thirst] = 1.5 },
            shared: true, needsOpenBusiness: true);

        /// <summary>A game of pool at the cue club, paid by the game; better with an opponent.</summary>
        public static readonly ActivityDefinition PlayPool = new ActivityDefinition(
            "play-pool", "Play pool", "playing pool at the cue club", ActivityKind.Leisure, NeedType.Fun, new[] { ObjectType.PoolTable }, 45,
            new Dictionary<NeedType, double> { [NeedType.Fun] = 1.1, [NeedType.Social] = 0.6 },
            itemId: "pool-game", shared: true);

        /// <summary>A frame of snooker: slower than pool, and the full-size table costs a little more.</summary>
        public static readonly ActivityDefinition PlaySnooker = new ActivityDefinition(
            "play-snooker", "Play snooker", "playing snooker at the cue club", ActivityKind.Leisure, NeedType.Fun, new[] { ObjectType.SnookerTable }, 60,
            new Dictionary<NeedType, double> { [NeedType.Fun] = 1.0, [NeedType.Social] = 0.5 },
            itemId: "snooker-frame", shared: true);

        /// <summary>A game of bowling: fun, sociable and a little exercise.</summary>
        public static readonly ActivityDefinition Bowling = new ActivityDefinition(
            "bowling", "Go bowling", "bowling at Strike Lanes", ActivityKind.Leisure, NeedType.Fun, new[] { ObjectType.BowlingLane }, 60,
            new Dictionary<NeedType, double> { [NeedType.Fun] = 1.2, [NeedType.Social] = 0.8, [NeedType.Exercise] = 0.5 },
            new Dictionary<NeedType, double> { [NeedType.Energy] = 1.3, [NeedType.Thirst] = 1.2 },
            itemId: "bowling-game", shared: true);

        /// <summary>A go on the arcade cabinets: quick, cheap and great fun, on your own.</summary>
        public static readonly ActivityDefinition PlayArcade = new ActivityDefinition(
            "play-arcade", "Play the arcade games", "playing games at the arcade", ActivityKind.Leisure, NeedType.Fun, new[] { ObjectType.ArcadeMachine }, 30,
            new Dictionary<NeedType, double> { [NeedType.Fun] = 1.5 },
            itemId: "arcade-credits");

        /// <summary>A session at the gym: the best exercise in town, and tiring.</summary>
        public static readonly ActivityDefinition GymWorkout = new ActivityDefinition(
            "gym", "Work out at the gym", "working out at the gym", ActivityKind.Leisure, NeedType.Exercise, new[] { ObjectType.GymMachine }, 50,
            new Dictionary<NeedType, double> { [NeedType.Exercise] = 2.6, [NeedType.Fun] = 0.3 },
            new Dictionary<NeedType, double> { [NeedType.Energy] = 2.2, [NeedType.Hygiene] = 3.2, [NeedType.Thirst] = 1.6 },
            itemId: "gym-pass");

        /// <summary>A wander round the market stalls: free, and a chance to see people.</summary>
        public static readonly ActivityDefinition BrowseMarket = new ActivityDefinition(
            "browse-market", "Browse the market", "browsing the market stalls", ActivityKind.Leisure, NeedType.Social, new[] { ObjectType.MarketStall }, 30,
            new Dictionary<NeedType, double> { [NeedType.Fun] = 0.7, [NeedType.Social] = 0.8 });

        // Tiny City 2's food (see Economy.FoodRules): nothing here exists in v1, and v1's free food doesn't exist here.

        /// <summary>Sowing a crop on an empty garden plot, or on open ground the resident chooses. It still has to be watered before it can be picked.</summary>
        public static readonly ActivityDefinition PlantCrops = new ActivityDefinition(
            "plant-crops", "Plant crops", "planting crops", ActivityKind.Errand, null, new[] { ObjectType.GardenPlot }, 45,
            new Dictionary<NeedType, double> { [NeedType.Exercise] = 0.5 },
            new Dictionary<NeedType, double> { [NeedType.Hygiene] = 2.0, [NeedType.Thirst] = 1.3 });

        /// <summary>Watering a growing crop, once a day.</summary>
        public static readonly ActivityDefinition WaterCrops = new ActivityDefinition(
            "water-crops", "Water the crops", "watering the crops", ActivityKind.Errand, null, new[] { ObjectType.GardenPlot }, 15,
            new Dictionary<NeedType, double> { [NeedType.Exercise] = 0.3 });

        /// <summary>Picking a ready crop: vegetables for whoever picks it.</summary>
        public static readonly ActivityDefinition Harvest = new ActivityDefinition(
            "harvest", "Pick the crops", "picking vegetables", ActivityKind.Errand, null, new[] { ObjectType.GardenPlot }, 30,
            new Dictionary<NeedType, double> { [NeedType.Exercise] = 0.4 },
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

        // Tiny City 2's gathering and crafting (see Economy.CraftingRules).

        /// <summary>Picking up fallen branches by hand: a little wood, no tool needed.</summary>
        public static readonly ActivityDefinition GatherSticks = Gather("gather-sticks", "Pick up fallen branches", "picking up fallen branches", ObjectType.StickPile, 1.2);

        /// <summary>Picking up loose stones by hand: a little stone, no tool needed.</summary>
        public static readonly ActivityDefinition GatherLooseStones = Gather("gather-loose-stones", "Pick up loose stones", "picking up loose stones", ObjectType.LooseStones, 1.3);

        /// <summary>Chopping wood from the trees, which needs an axe.</summary>
        public static readonly ActivityDefinition GatherWood = new ActivityDefinition(
            "gather-wood", "Gather wood", "gathering wood", ActivityKind.Errand, null, new[] { ObjectType.Woodland }, 60,
            new Dictionary<NeedType, double> { [NeedType.Exercise] = 0.8 },
            new Dictionary<NeedType, double> { [NeedType.Energy] = 1.5, [NeedType.Hygiene] = 2.0, [NeedType.Thirst] = 1.4 });

        /// <summary>Breaking stone off the rocks, which needs a pickaxe.</summary>
        public static readonly ActivityDefinition GatherStone = new ActivityDefinition(
            "gather-stone", "Break stone", "breaking stone", ActivityKind.Errand, null, new[] { ObjectType.RockPile }, 60,
            new Dictionary<NeedType, double> { [NeedType.Exercise] = 1.0 },
            new Dictionary<NeedType, double> { [NeedType.Energy] = 1.7, [NeedType.Hygiene] = 2.2, [NeedType.Thirst] = 1.5 });

        /// <summary>Hardwood: needs a stone axe or better.</summary>
        public static readonly ActivityDefinition GatherHardwood = Gather("gather-hardwood", "Cut hardwood", "cutting hardwood", ObjectType.Woodland, 1.6);

        /// <summary>Heartwood: needs an iron axe.</summary>
        public static readonly ActivityDefinition GatherHeartwood = Gather("gather-heartwood", "Cut heartwood", "cutting heartwood", ObjectType.Woodland, 1.7);

        /// <summary>Iron ore: needs a stone pickaxe or better.</summary>
        public static readonly ActivityDefinition GatherIron = Gather("gather-iron", "Mine iron ore", "mining iron ore", ObjectType.RockPile, 1.8);

        /// <summary>Crystal: needs an iron pickaxe.</summary>
        public static readonly ActivityDefinition GatherCrystal = Gather("gather-crystal", "Mine crystal", "mining crystal", ObjectType.RockPile, 1.9);

        public static readonly ActivityDefinition CraftAxe = Craft("craft-axe", "Make a wooden axe", "making a wooden axe");

        public static readonly ActivityDefinition CraftPickaxe = Craft("craft-pickaxe", "Make a wooden pickaxe", "making a wooden pickaxe");

        public static readonly ActivityDefinition CraftStoneAxe = Craft("craft-stone-axe", "Make a stone axe", "making a stone axe");

        public static readonly ActivityDefinition CraftStonePickaxe = Craft("craft-stone-pickaxe", "Make a stone pickaxe", "making a stone pickaxe");

        public static readonly ActivityDefinition CraftIronAxe = Craft("craft-iron-axe", "Make an iron axe", "making an iron axe");

        public static readonly ActivityDefinition CraftIronPickaxe = Craft("craft-iron-pickaxe", "Make an iron pickaxe", "making an iron pickaxe");

        public static readonly ActivityDefinition CraftHoe = Craft("craft-hoe", "Make a hoe", "making a hoe");

        public static readonly ActivityDefinition CraftRod = Craft("craft-rod", "Make a fishing rod", "making a fishing rod");

        /// <summary>Tiny City 2: sketching a building on open ground the resident chooses; the plan itself comes from the AI.</summary>
        public static readonly ActivityDefinition PlanBuilding = new ActivityDefinition(
            "plan-building", "Plan a building", "planning a building", ActivityKind.Errand, null, new[] { ObjectType.BuildPlot }, 30,
            new Dictionary<NeedType, double> { [NeedType.Fun] = 0.5 });

        /// <summary>Tiny City 2: laying the next blocks of their own building, at its site.</summary>
        public static readonly ActivityDefinition Build = new ActivityDefinition(
            "build", "Build", "building", ActivityKind.Errand, null, new[] { ObjectType.BuildPlot }, 60,
            new Dictionary<NeedType, double> { [NeedType.Fun] = 0.3, [NeedType.Exercise] = 0.6 },
            new Dictionary<NeedType, double> { [NeedType.Energy] = 1.5, [NeedType.Hygiene] = 1.8, [NeedType.Thirst] = 1.4 });

        /// <summary>Tiny City 2: activities done on open ground wherever the resident chooses, rather than at a thing.</summary>
        public static bool IsDoneOnGround(ActivityDefinition a) => a == PlantCrops || a == PlanBuilding || a == Sleep;

        /// <summary>In a fixed order; the planner uses it to break ties deterministically.</summary>
        public static readonly IReadOnlyList<ActivityDefinition> All = new[]
        {
            Sleep, Eat, Snack, CafeMeal, Pastry, Drink, UseToilet, Shower, Work, Exercise, Read, WatchTown, Coffee, BuyGroceries, Chat,
            Rest, SeeDoctor, Paint, PlayPiano, Chess, Fish, Garden, Bake, Knit, AttendEvent, BarDrink, Dance,
            PlayPool, PlaySnooker, Bowling, PlayArcade, GymWorkout, BrowseMarket, Leftovers,
            PlantCrops, WaterCrops, Harvest, CatchFish, Cook, EatMeal, EatRaw, RunStall, BuyFood,
            GatherWood, GatherStone, CraftAxe, CraftPickaxe, CraftHoe, CraftRod,
            GatherHardwood, GatherHeartwood, GatherIron, GatherCrystal, CraftStoneAxe, CraftStonePickaxe, CraftIronAxe, CraftIronPickaxe,
            PlanBuilding, Build, GatherSticks, GatherLooseStones, DrinkFromPond, WashInPond,
            PurifyWater, DrinkCleanWater, AttendMeeting,
        };

        private static ActivityDefinition Gather(string id, string name, string verb, ObjectType where, double effort) => new ActivityDefinition(
            id, name, verb, ActivityKind.Errand, null, new[] { where }, 60,
            new Dictionary<NeedType, double> { [NeedType.Exercise] = 1.0 },
            new Dictionary<NeedType, double> { [NeedType.Energy] = effort, [NeedType.Hygiene] = 2.2, [NeedType.Thirst] = 1.5 });

        private static ActivityDefinition Craft(string id, string name, string verb) => new ActivityDefinition(
            id, name, verb, ActivityKind.Errand, null, new[] { ObjectType.Workbench }, 30,
            new Dictionary<NeedType, double> { [NeedType.Fun] = 0.4 });

        /// <summary>Every hobby id, from the hobby activities.</summary>
        public static IEnumerable<string> HobbyIds => All.Where(a => a.Hobby != null).Select(a => a.Hobby);

        public static ActivityDefinition ForHobby(string hobby) => All.FirstOrDefault(a => a.Hobby == hobby);

        private static ActivityDefinition Hobby(string id, string name, string verb, string hobby, ObjectType where, int minutes, Dictionary<NeedType, double> gains, bool shared = false) =>
            new ActivityDefinition(id, name, verb, ActivityKind.Leisure, NeedType.Fun, new[] { where }, minutes, gains, hobby: hobby, shared: shared);

        public static bool TryGet(string id, out ActivityDefinition activity)
        {
            activity = All.FirstOrDefault(a => a.Id == id);
            return activity != null;
        }

        /// <summary>The activities that can restore a need when it is urgent, in preference order.</summary>
        public static IEnumerable<ActivityDefinition> ForNeed(NeedType need)
        {
            return All.Where(a => a.PrimaryNeed == need && (a.Kind == ActivityKind.Care || a.Kind == ActivityKind.Sleep || a.Kind == ActivityKind.Leisure));
        }
    }
}
