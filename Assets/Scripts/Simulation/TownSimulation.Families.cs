using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation.Content;
using TinyCity.Simulation.Economy;
using TinyCity.Simulation.History;
using TinyCity.Simulation.Memory;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Social;

namespace TinyCity.Simulation
{
    /// <summary>
    /// Babies, children and growing up, following <see cref="PopulationRules"/>. Committed couples can have a baby,
    /// never while <see cref="PopulationRules.MaxLiving"/> people are alive. Babies are carried about by a parent;
    /// from <see cref="PopulationRules.WalkingAge"/> children live their own day at home, and at
    /// <see cref="PopulationRules.AdultAge"/> they move into a home of their own, or onto a park bench if none is free.
    /// </summary>
    public sealed partial class TownSimulation
    {
        /// <summary>
        /// Whether couples have babies. Off unless turned on, so test towns keep the residents they were given; the
        /// game turns it on from settings.json <c>simulation.babies</c>.
        /// </summary>
        public bool BabiesEnabled { get; set; }

        private void StepBirths()
        {
            if (!BabiesEnabled || !RomanceEnabled || Time.MinuteOfDay != PopulationRules.BirthMinute)
            {
                return;
            }

            foreach (var couple in Relationships.Partnerships.Where(p => p.Stage == RomanceStage.Committed).ToList())
            {
                var a = GetResident(couple.FirstId);
                var b = GetResident(couple.SecondId);
                if (CanHaveBaby(a, b) && PopulationRules.BabyArrives(couple.Id, Time.Day))
                {
                    HaveBaby(a, b);
                }
            }
        }

        /// <summary>
        /// Two living adults no older than <see cref="PopulationRules.MaxParentAge"/>, at least one with a home, who
        /// don't already have a baby or <see cref="PopulationRules.MaxChildrenPerCouple"/> children, while the city has room.
        /// </summary>
        public bool CanHaveBaby(Resident a, Resident b)
        {
            if (a == null || b == null || !a.IsAlive || !b.IsAlive || a.IsMinor || b.IsMinor
                || a.AgeYears > PopulationRules.MaxParentAge || b.AgeYears > PopulationRules.MaxParentAge
                || (a.IsHomeless && b.IsHomeless) || LivingCount >= PopulationRules.MaxLiving)
            {
                return false;
            }

            var children = ChildrenOf(a, b).ToList();
            return children.Count < PopulationRules.MaxChildrenPerCouple && !children.Any(c => c.IsAlive && c.IsBaby);
        }

        public IEnumerable<Resident> ChildrenOf(Resident a, Resident b) =>
            residents.Where(r => r.ParentIds.Contains(a.Id) && r.ParentIds.Contains(b.Id));

        /// <summary>A baby is born to the couple and lives at the home of whichever parent has one (the first named, if both do).</summary>
        internal Resident HaveBaby(Resident a, Resident b)
        {
            if (!CanHaveBaby(a, b))
            {
                return null;
            }

            var carer = a.IsHomeless ? b : a;
            var other = carer == a ? b : a;
            var taken = new HashSet<string>(residents.Select(r => r.FullName));
            var baby = Births.Create(NextResidentId(), Time.Day, carer, other, taken);
            AddResident(baby);
            foreach (var parent in new[] { a, b })
            {
                Relationships.Get(parent.Id, baby.Id).Change(friendship: 80, trust: 80, respect: 40);
                Relationships.Get(baby.Id, parent.Id).Change(friendship: 80, trust: 90, respect: 60);
                Memories.Add(parent.Id, Time, MemoryType.Event, 9, $"Our baby {baby.FirstName} was born today.", new[] { a.Id, b.Id, baby.Id }, new[] { baby.Id, "baby", "family" });
            }

            baby.AddLog(Time, $"Born to {a.FirstName} and {b.FirstName}");
            Memories.Add(baby.Id, Time, MemoryType.LifeHistory, 8, $"I was born in {World.TownName}, to {a.FullName} and {b.FullName}.", new[] { a.Id, b.Id }, new[] { a.Id, b.Id, "family", "born" });
            History.Add(Time, TownEventKind.Born, $"{a.FullName} and {b.FullName} had a baby, {baby.FirstName}.", new[] { baby.Id, a.Id, b.Id });
            return baby;
        }

        /// <summary>Who carries a baby about: a living parent who shares their home, or failing that any living parent.</summary>
        public Resident CarerOf(Resident baby)
        {
            var parents = baby.ParentIds.Select(GetResident).Where(p => p != null && p.IsAlive).ToList();
            return parents.FirstOrDefault(p => p.HomeMapId == baby.HomeMapId) ?? parents.FirstOrDefault();
        }

        /// <summary>A baby goes wherever their carer goes, and is fed and looked after: their needs stay met.</summary>
        private void StepBaby(Resident baby)
        {
            foreach (NeedType need in System.Enum.GetValues(typeof(NeedType)))
            {
                baby.Needs[need] = System.Math.Max(baby.Needs[need], 85);
            }

            var carer = CarerOf(baby);
            if (carer != null)
            {
                baby.Location = carer.Location;
                baby.Goal = $"Being looked after by {carer.FirstName}";
            }
            else if (World.TryGetObject(baby.BedObjectId ?? string.Empty, out var bed))
            {
                baby.Location = bed.Location;
                baby.Goal = "Being looked after at home";
            }
        }

        /// <summary>Milestones on a city-born resident's birthdays: walking, pocket money, then a home of their own.</summary>
        private void GrowingUp(Resident r)
        {
            if (r.AgeYears == PopulationRules.WalkingAge)
            {
                r.ResetActivity();
                r.AddLog(Time, "Old enough to go about on their own");
            }

            if (r.AgeYears == PopulationRules.PocketMoneyAge)
            {
                r.WeeklyIncomePence = Births.PocketMoney;
                r.IncomeLabel = Births.PocketMoneyLabel;
            }

            if (r.AgeYears != PopulationRules.AdultAge)
            {
                return;
            }

            r.WeeklyIncomePence = Newcomers.AllowanceIncome;
            r.IncomeLabel = Newcomers.AllowanceLabel;
            var home = PickFreeHome(r.Id);
            if (home == null)
            {
                MakeHomeless(r, "I grew up, and there was no home free for me.");
                return;
            }

            MoveInto(r, home);
            r.Groceries = 2;
            r.AddLog(Time, $"Moved out, into {home.Name}");
            Memories.Add(r.Id, Time, MemoryType.Event, 8, $"I grew up and moved out, into a home of my own: {home.Name}.", tags: new[] { home.Id, "home", "moved-out" });
            History.Add(Time, TownEventKind.MovedOut, $"{r.FullName} grew up and moved into {home.Name}.", new[] { r.Id });
        }
    }
}
