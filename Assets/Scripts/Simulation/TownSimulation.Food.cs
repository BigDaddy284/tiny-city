using System;
using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation.Activities;
using TinyCity.Simulation.Economy;
using TinyCity.Simulation.Memory;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Social;
using TinyCity.Simulation.World;

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

        /// <summary>Tiny City 2: the spots on bare ground where people sleep, each gone once its sleeper gets up.</summary>
        public SleepingGround Sleeping { get; } = new SleepingGround();

        private void StepSleepingGround()
        {
            Sleeping.Prune(spot => residents.Any(o => o.Id == spot.SleeperId && o.IsAlive && o.TargetObjectId == spot.Id
                && (o.State == ResidentState.Travelling || o.State == ResidentState.Performing)));
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

        /// <summary>What each stall-holder has sold since they set up (not saved).</summary>
        private readonly Dictionary<string, (int portions, long pence)> stallSales = new Dictionary<string, (int, long)>(StringComparer.Ordinal);

        /// <summary>The living resident selling food at this stall right now, or null.</summary>
        private Resident SellerAt(WorldObject stall) => stall == null ? null : residents.FirstOrDefault(o =>
            o.IsAlive && o.State == ResidentState.Performing && o.ActivityId == ActivityCatalog.RunStall.Id && o.TargetObjectId == stall.Id);

        /// <summary>As a food activity starts: eating takes a portion, buying pays the stall-holder. False, with a reason, if it can't.</summary>
        private bool StartFood(Resident r, ActivityDefinition activity, out string reason)
        {
            reason = null;
            if (FoodRules.EatsFood(activity))
            {
                return Food.TakeAtStart(r, activity, out reason);
            }

            if (activity == ActivityCatalog.RunStall)
            {
                stallSales[r.Id] = (0, 0);
                return true;
            }

            if (activity != ActivityCatalog.BuyFood)
            {
                return true;
            }

            World.TryGetObject(r.TargetObjectId, out var stall);
            var seller = SellerAt(stall);
            if (seller == null || seller == r || seller.Food.Total == 0)
            {
                reason = "nobody was selling food there any more";
                return false;
            }

            var price = seller.StallPricePence;
            if (r.MoneyPence < price)
            {
                reason = $"they couldn't afford it ({Money.Format(price)})";
                return false;
            }

            var kind = seller.Food.Count(FoodKind.Meal) > 0 ? FoodKind.Meal : seller.Food.Batches.First().Kind;
            if (seller.Food.MoveTo(r.Food, 1) == 0)
            {
                reason = "they had no room for more food";
                return false;
            }

            Pay(r, seller, price, FoodRules.Portions(kind, 1));
            var paid = price > 0 ? $" for {Money.Format(price)}" : " for nothing";
            var sold = stallSales.TryGetValue(seller.Id, out var so) ? so : (0, 0L);
            stallSales[seller.Id] = (sold.Item1 + 1, sold.Item2 + price);
            r.AddLog(Time, $"{(price > 0 ? "Bought" : "Was given")} {FoodRules.Portions(kind, 1)} from {seller.FullName}{paid}");
            seller.AddLog(Time, $"{(price > 0 ? "Sold" : "Gave")} {FoodRules.Portions(kind, 1)} to {r.FullName}{paid}");
            return true;
        }

        /// <summary>
        /// Tiny City 2: what a chat line did with food. A speaker may offer some of their food (free, or for a price),
        /// and the other person's next line may accept it; the game moves the food and money only if both still can.
        /// </summary>
        private void ApplyFoodInChat(ConversationSession session, Resident speaker, int giveFood, long pricePence, bool acceptOffer)
        {
            if (!FoodMustBeMade)
            {
                return;
            }

            var offer = session.FoodOffer;
            if (offer != null && offer.ToId == speaker.Id)
            {
                session.FoodOffer = null;
                var giver = GetResident(offer.FromId);
                if (acceptOffer && giver != null && giver.IsAlive)
                {
                    var portions = Math.Min(offer.Portions, giver.Food.Total);
                    if (portions <= 0)
                    {
                        speaker.AddLog(Time, $"{giver.FirstName} had no food left to give after all");
                    }
                    else if (speaker.MoneyPence < offer.PricePence)
                    {
                        speaker.AddLog(Time, $"Couldn't afford the food {giver.FirstName} offered ({Money.Format(offer.PricePence)})");
                    }
                    else
                    {
                        var moved = giver.Food.MoveTo(speaker.Food, portions);
                        if (moved > 0)
                        {
                            var what = $"{moved} portion{(moved == 1 ? string.Empty : "s")} of food";
                            Pay(speaker, giver, offer.PricePence, what);
                            var price = offer.PricePence > 0 ? $" for {Money.Format(offer.PricePence)}" : " for nothing";
                            giver.AddLog(Time, $"Gave {speaker.FullName} {what}{price}");
                            speaker.AddLog(Time, $"Got {what} from {giver.FullName}{price}");
                            Memories.Add(giver.Id, Time, MemoryType.Event, 4, offer.PricePence > 0 ? $"I sold {speaker.FullName} {what} for {Money.Format(offer.PricePence)}." : $"I gave {speaker.FullName} {what}.", new[] { speaker.Id }, new[] { speaker.Id, "food" });
                            Memories.Add(speaker.Id, Time, MemoryType.Event, 4, offer.PricePence > 0 ? $"{giver.FullName} sold me {what} for {Money.Format(offer.PricePence)}." : $"{giver.FullName} gave me {what}.", new[] { giver.Id }, new[] { giver.Id, "food" });
                        }
                    }
                }
            }

            if (giveFood > 0 && speaker.Food.Total > 0)
            {
                var other = session.ParticipantIds.FirstOrDefault(id => id != speaker.Id);
                if (other != null)
                {
                    session.FoodOffer = new FoodOffer(speaker.Id, other, Math.Min(giveFood, speaker.Food.Total), Math.Max(0, Math.Min(pricePence, FoodRules.LargestPricePence)));
                }
            }
        }

        /// <summary>
        /// Tiny City 2: money handed over in a chat (a gift, a wage, a payment: whatever they say it is), and a tax
        /// man's demand for a share of the other person's food and materials, which that person's next line pays or
        /// refuses. Only someone whose role <see cref="WorldBrief.CanTax"/> can ask; whether anyone pays is up to them.
        /// </summary>
        private void ApplyMoneyAndTaxInChat(ConversationSession session, Resident speaker, long giveMoneyPence, int taxPercent, bool payTax)
        {
            if (!FoodMustBeMade)
            {
                return;
            }

            var other = GetResident(session.ParticipantIds.FirstOrDefault(id => id != speaker.Id));
            var demand = session.TaxDemand;
            if (demand != null && demand.ToId == speaker.Id)
            {
                session.TaxDemand = null;
                var taxer = GetResident(demand.FromId);
                if (taxer != null && taxer.IsAlive)
                {
                    if (payTax)
                    {
                        var paid = PayTax(speaker, taxer, demand.Percent);
                        speaker.AddLog(Time, $"Paid {taxer.FullName} {demand.Percent}% tax: {paid}");
                        taxer.AddLog(Time, $"Collected {demand.Percent}% tax from {speaker.FullName}: {paid}");
                        Memories.Add(speaker.Id, Time, MemoryType.Event, 5, $"{taxer.FullName} taxed me {demand.Percent}% and I paid: {paid}.", new[] { taxer.Id }, new[] { taxer.Id, "tax" });
                        Memories.Add(taxer.Id, Time, MemoryType.Event, 5, $"I taxed {speaker.FullName} {demand.Percent}% and they paid: {paid}.", new[] { speaker.Id }, new[] { speaker.Id, "tax" });
                    }
                    else
                    {
                        speaker.AddLog(Time, $"Refused to pay {taxer.FullName}'s tax");
                        Memories.Add(speaker.Id, Time, MemoryType.Event, 5, $"{taxer.FullName} tried to tax me {demand.Percent}%, and I refused.", new[] { taxer.Id }, new[] { taxer.Id, "tax" });
                        Memories.Add(taxer.Id, Time, MemoryType.Event, 5, $"I asked {speaker.FullName} for {demand.Percent}% tax, and they refused.", new[] { speaker.Id }, new[] { speaker.Id, "tax" });
                    }
                }
            }

            if (other == null)
            {
                return;
            }

            if (giveMoneyPence > 0)
            {
                var amount = Math.Min(giveMoneyPence, Math.Max(0, speaker.MoneyPence));
                if (amount > 0)
                {
                    speaker.MoneyPence -= amount;
                    other.MoneyPence += amount;
                    Economy.Ledger.Add(Time, speaker.Id, -amount, TransactionKind.Gift, $"handed to {other.FullName}");
                    Economy.Ledger.Add(Time, other.Id, amount, TransactionKind.Gift, $"from {speaker.FullName}");
                    speaker.AddLog(Time, $"Gave {other.FullName} {Money.Format(amount)}");
                    other.AddLog(Time, $"Got {Money.Format(amount)} from {speaker.FullName}");
                    Memories.Add(speaker.Id, Time, MemoryType.Event, 4, $"I gave {other.FullName} {Money.Format(amount)}.", new[] { other.Id }, new[] { other.Id, "money" });
                    Memories.Add(other.Id, Time, MemoryType.Event, 4, $"{speaker.FullName} gave me {Money.Format(amount)}.", new[] { speaker.Id }, new[] { speaker.Id, "money" });
                }
            }

            if (taxPercent > 0 && Brief.CanTax(speaker.Role))
            {
                session.TaxDemand = new TaxDemand(speaker.Id, other.Id, Math.Min(taxPercent, 100));
            }
        }

        /// <summary>Hands the given share of each kind of food and material from one resident to another (rounded down). Says what moved.</summary>
        private string PayTax(Resident payer, Resident taxer, int percent)
        {
            var parts = new List<string>();
            var food = payer.Food.Total * percent / 100;
            var moved = food > 0 ? payer.Food.MoveTo(taxer.Food, food) : 0;
            if (moved > 0)
            {
                parts.Add($"{moved} portion{(moved == 1 ? string.Empty : "s")} of food");
            }

            foreach (MaterialKind m in Enum.GetValues(typeof(MaterialKind)))
            {
                var n = payer.Belongings.Count(m) * percent / 100;
                if (n > 0 && payer.Belongings.Take(m, n))
                {
                    var kept = taxer.Belongings.Add(m, n);
                    if (kept < n)
                    {
                        payer.Belongings.Add(m, n - kept);
                    }

                    if (kept > 0)
                    {
                        parts.Add($"{kept} {CraftingRules.Name(m)}");
                    }
                }
            }

            return parts.Count == 0 ? "nothing, as they had too little" : string.Join(", ", parts);
        }

        /// <summary>Money between residents for food, through the ledger.</summary>
        private void Pay(Resident buyer, Resident seller, long pence, string what)
        {
            if (pence <= 0)
            {
                return;
            }

            buyer.MoneyPence -= pence;
            seller.MoneyPence += pence;
            Economy.Ledger.Add(Time, buyer.Id, -pence, TransactionKind.Purchase, $"{what} from {seller.FullName}");
            Economy.Ledger.Add(Time, seller.Id, pence, TransactionKind.Sale, $"{what} for {buyer.FullName}");
        }

        private void FinishFood(Resident r, ActivityDefinition activity)
        {
            if (FoodMustBeMade && activity == ActivityCatalog.RunStall)
            {
                stallSales.TryGetValue(r.Id, out var sold);
                stallSales.Remove(r.Id);
                var note = sold.portions == 0
                    ? "I sold food at a market stall, but nobody bought any."
                    : sold.pence == 0
                    ? $"I gave away {sold.portions} portion{(sold.portions == 1 ? string.Empty : "s")} of food at a market stall."
                    : $"I sold {sold.portions} portion{(sold.portions == 1 ? string.Empty : "s")} of food at a market stall for {Money.Format(sold.pence)}, asking {Money.Format(r.StallPricePence)} a portion.";
                r.AddLog(Time, sold.portions == 0 ? "Packed up the stall: nobody bought anything" : $"Packed up the stall: sold {sold.portions} for {Money.Format(sold.pence)}");
                Memories.Add(r.Id, Time, MemoryType.Event, 3, note, tags: new[] { "food", "stall", r.TargetObjectId });
                return;
            }

            if (!FoodMustBeMade || !FoodRules.IsMadeFood(activity) || FoodRules.EatsFood(activity) || activity == ActivityCatalog.BuyFood)
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
