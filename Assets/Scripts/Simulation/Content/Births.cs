using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation.Economy;
using TinyCity.Simulation.Health;
using TinyCity.Simulation.Residents;

namespace TinyCity.Simulation.Content
{
    /// <summary>
    /// A baby born in the city. Everything about them comes from <see cref="Chance"/> rolls on their id, and their
    /// personality and first hobby from their parents, so the same couple always has the same child.
    /// </summary>
    public static class Births
    {
        public const string PocketMoneyLabel = "Pocket money";
        public static readonly long PocketMoney = Money.Pounds(10);

        /// <summary>
        /// The newborn with this id, living at <paramref name="carer"/>'s home with them and taking their surname.
        /// Names already taken by someone in town are skipped where possible.
        /// </summary>
        public static Resident Create(string id, int day, Resident carer, Resident other, ICollection<string> takenNames)
        {
            double Roll(int salt) => Chance.Roll("baby:" + id, 0, salt);

            var names = Newcomers.FirstNames;
            var first = names[(int)(Roll(1) * names.Length)];
            for (var attempt = 0; attempt < 20 && (takenNames.Contains(first + " " + carer.LastName) || first == carer.FirstName || first == other.FirstName); attempt++)
            {
                first = names[(int)(Roll(100 + attempt) * names.Length)];
            }

            double Blend(double x, double y, int salt) => ((x + y) / 2) + ((Roll(salt) - 0.5) * 0.3);
            var personality = new Personality(
                Blend(carer.Personality.Active, other.Personality.Active, 2),
                Blend(carer.Personality.Sociable, other.Personality.Sociable, 3),
                Blend(carer.Personality.Tidy, other.Personality.Tidy, 4),
                Blend(carer.Personality.Bookish, other.Personality.Bookish, 5));

            var baby = new Resident(id, first, carer.LastName, 0, HealthRules.BirthdayDayOfYearFor(day), Newcomers.Colours[(int)(Roll(6) * Newcomers.Colours.Length)],
                personality, carer.HomeMapId, carer.BedObjectId, null, carer.Location, new Needs(90))
            {
                BornDay = day,
                OpenToRomance = Roll(7) < 0.85,
            };

            baby.SetParents(new[] { carer.Id, other.Id });
            var hobbies = carer.Hobbies.Concat(other.Hobbies).Distinct().ToList();
            if (hobbies.Count > 0)
            {
                baby.SetHobbies(new[] { hobbies[(int)(Roll(8) * hobbies.Count)] });
            }

            return baby;
        }
    }
}
