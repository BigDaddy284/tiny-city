using System.Collections.Generic;
using System.Linq;
using TinyTown.Simulation.Residents;
using TinyTown.Simulation.World;

namespace TinyTown.Simulation.Activities
{
    /// <summary>Every activity a resident can choose. Ids are stable because save files store them.</summary>
    public static class ActivityCatalog
    {
        public static readonly ActivityDefinition Sleep = new ActivityDefinition(
            "sleep", "Sleep", "sleeping", ActivityKind.Sleep, NeedType.Energy, new[] { ObjectType.Bed }, 10 * 60,
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

        public static readonly ActivityDefinition CafeMeal = new ActivityDefinition(
            "cafe-meal", "Eat at the café", "having a meal at the café", ActivityKind.Care, NeedType.Hunger, new[] { ObjectType.CafeTable }, 40,
            new Dictionary<NeedType, double> { [NeedType.Hunger] = 2.4, [NeedType.Thirst] = 0.6, [NeedType.Social] = 0.5, [NeedType.Fun] = 0.6 },
            itemId: "cafe-meal");

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
            "watch-town", "Sit in the park", "sitting in the park watching the town", ActivityKind.Leisure, NeedType.Social, new[] { ObjectType.Bench }, 45,
            new Dictionary<NeedType, double> { [NeedType.Fun] = 0.6, [NeedType.Social] = 0.7 });

        public static readonly ActivityDefinition Coffee = new ActivityDefinition(
            "coffee", "Have a coffee", "having a coffee at the café", ActivityKind.Leisure, NeedType.Fun, new[] { ObjectType.CafeTable }, 25,
            new Dictionary<NeedType, double> { [NeedType.Fun] = 1.0, [NeedType.Thirst] = 1.5, [NeedType.Social] = 0.5, [NeedType.Energy] = 0.1 },
            itemId: "coffee");

        /// <summary>Stocking up the kitchen at home. An errand: chosen when groceries run low, not to restore a need.</summary>
        public static readonly ActivityDefinition BuyGroceries = new ActivityDefinition(
            "buy-groceries", "Buy groceries", "shopping for groceries", ActivityKind.Errand, null, new[] { ObjectType.ShopShelf }, 15,
            new Dictionary<NeedType, double> { [NeedType.Fun] = 0.1 },
            itemId: "groceries");

        /// <summary>Walking over to someone and talking with them. The duration is set by the conversation, not here.</summary>
        public static readonly ActivityDefinition Chat = new ActivityDefinition(
            "chat", "Chat", "chatting", ActivityKind.Social, NeedType.Social, new ObjectType[0], 20,
            new Dictionary<NeedType, double> { [NeedType.Social] = 2.2, [NeedType.Fun] = 0.4 });

        /// <summary>In a fixed order; the planner uses it to break ties deterministically.</summary>
        public static readonly IReadOnlyList<ActivityDefinition> All = new[]
        {
            Sleep, Eat, Snack, CafeMeal, Pastry, Drink, UseToilet, Shower, Work, Exercise, Read, WatchTown, Coffee, BuyGroceries, Chat,
        };

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
