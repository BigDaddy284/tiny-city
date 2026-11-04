using TinyCity.Simulation.Activities;
using TinyCity.Simulation.Economy;
using TinyCity.Simulation.Memory;
using TinyCity.Simulation.Residents;

namespace TinyCity.Simulation
{
    /// <summary>Tiny City 2's food: nothing is free, so every meal is grown or caught, cooked or eaten raw, and food goes off.</summary>
    public sealed partial class TownSimulation
    {
        /// <summary>The crops and food rules. Its <see cref="FoodSupply.MustBeMade"/> is <see cref="FoodMustBeMade"/>.</summary>
        public FoodSupply Food { get; } = new FoodSupply();

        /// <summary>
        /// Tiny City 2 (the game sets it with <see cref="AiRunsEverything"/>): v1's free food (snacks, leftovers,
        /// groceries and the café's and bakery's endless stock) is gone, and residents eat only what they grow,
        /// catch and cook. Off by default, so test towns and v1 keep v1's food.
        /// </summary>
        public bool FoodMustBeMade
        {
            get => Food.MustBeMade;
            set => Food.MustBeMade = value;
        }

        private void StepFood()
        {
            if (!FoodMustBeMade)
            {
                return;
            }

            var spoilNow = Time.MinuteOfDay == FoodRules.SpoilMinute;
            foreach (var r in residents)
            {
                if (!r.IsAlive)
                {
                    continue;
                }

                if (r.Groceries > 0)
                {
                    // What they had in the cupboard when the town started (or they arrived) becomes cooked meals.
                    r.Food.Add(FoodKind.Meal, r.Groceries, Time.Day + FoodRules.MealDays);
                    r.Groceries = 0;
                }

                if (spoilNow)
                {
                    var gone = r.Food.Spoil(Time.Day);
                    if (gone > 0)
                    {
                        r.AddLog(Time, $"Threw away {gone} portion{(gone == 1 ? string.Empty : "s")} of food that had gone off");
                    }
                }
            }
        }

        private void FinishFood(Resident r, ActivityDefinition activity)
        {
            if (!FoodMustBeMade || !FoodRules.IsMadeFood(activity) || FoodRules.EatsFood(activity))
            {
                return;
            }

            World.TryGetObject(r.TargetObjectId, out var target);
            var made = Food.Finish(r, activity, target, Time.Day);
            if (made == null)
            {
                r.AddLog(Time, $"Finished {activity.Verb}, but there was nothing to show for it");
                return;
            }

            r.AddLog(Time, made);
            if (activity == ActivityCatalog.PlantCrops || activity == ActivityCatalog.Harvest)
            {
                var note = activity == ActivityCatalog.PlantCrops
                    ? $"I planted crops at the {target.Name.ToLowerInvariant()}. They need watering on {FoodRules.WateredDaysToGrow} different days before they can be picked."
                    : made.Replace("Picked", "I picked") + ".";
                Memories.Add(r.Id, Time, MemoryType.Event, 3, note, tags: new[] { "food", target.Id });
            }
        }
    }
}
