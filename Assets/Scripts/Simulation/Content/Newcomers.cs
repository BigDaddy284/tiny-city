using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation.Activities;
using TinyCity.Simulation.Economy;
using TinyCity.Simulation.Health;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.World;

namespace TinyCity.Simulation.Content
{
    /// <summary>
    /// Who moves to the city from out of town. Everything about a newcomer comes from <see cref="Chance"/> rolls on
    /// their id, so the same town always gets the same people.
    /// </summary>
    public static class Newcomers
    {
        private static readonly string[] FirstNames =
        {
            "Aiko", "Bea", "Callum", "Dara", "Elias", "Freya", "Gus", "Hana", "Idris", "Juno",
            "Kit", "Lena", "Marek", "Nia", "Oscar", "Pia", "Quinn", "Remy", "Suki", "Theo",
            "Uma", "Vic", "Wade", "Yara", "Zane", "Ada", "Bryn", "Cleo", "Dev", "Esme",
            "Finn", "Greta", "Hugo", "Iris", "Jonah", "Kira", "Leo", "Maya", "Noel", "Orla",
        };

        private static readonly string[] LastNames =
        {
            "Ashby", "Blake", "Castell", "Drummond", "Ellery", "Fairweather", "Garnet", "Holloway", "Ingram", "Joss",
            "Kestrel", "Lowe", "Mercer", "Nakamura", "Orme", "Pike", "Quill", "Reyes", "Sato", "Thorne",
            "Underhill", "Vance", "Whitlock", "Yardley", "Zubiri", "Achebe", "Brannigan", "Cole", "Duval", "Egan",
        };

        private static readonly string[] Colours = { "#4E9DD9", "#D95B8E", "#5BC28A", "#C99A3E", "#9A6CD9", "#D9734E", "#3EA7A3", "#B85C5C" };

        public static readonly long FlatRent = Money.Pounds(100);
        public static readonly long CottageRent = Money.Pounds(120);

        public const string RemoteWorkLabel = "Works remotely";
        public static readonly long RemoteWorkIncome = Money.Pounds(300);
        public const string AllowanceLabel = "Jobseeker's allowance";
        public static readonly long AllowanceIncome = Money.Pounds(150);

        /// <summary>
        /// Makes the newcomer with this id, standing at <paramref name="arrival"/> and about to live in
        /// <paramref name="home"/>. Names already taken by someone in town are skipped where possible.
        /// </summary>
        public static Resident Create(string id, int day, TileMap home, WorldObject bed, Location arrival, ICollection<string> takenNames)
        {
            double Roll(int salt) => Chance.Roll("newcomer:" + id, 0, salt);

            var first = FirstNames[(int)(Roll(1) * FirstNames.Length)];
            var last = LastNames[(int)(Roll(2) * LastNames.Length)];
            for (var attempt = 0; attempt < 20 && takenNames.Contains(first + " " + last); attempt++)
            {
                first = FirstNames[(int)(Roll(100 + attempt) * FirstNames.Length)];
                last = LastNames[(int)(Roll(200 + attempt) * LastNames.Length)];
            }

            var age = 20 + (int)(Roll(3) * 41);
            var personality = new Personality(Trait(Roll(4)), Trait(Roll(5)), Trait(Roll(6)), Trait(Roll(7)));
            var needs = new Needs(72);
            var r = new Resident(id, first, last, age, 1 + (int)(Roll(8) * 365), Colours[(int)(Roll(9) * Colours.Length)], personality, home.Id, bed.Id, null, arrival, needs)
            {
                MovedInDay = day,
                MoneyPence = Money.Pounds(300 + (int)(Roll(10) * 501)),
                Groceries = 3,
                WeeklyRentPence = home.Id.StartsWith(StarterTown.FlatMapPrefix) ? FlatRent : CottageRent,
                OpenToRomance = Roll(11) < 0.8,
            };

            var remote = Roll(12) < 0.6;
            r.WeeklyIncomePence = remote ? RemoteWorkIncome : AllowanceIncome;
            r.IncomeLabel = remote ? RemoteWorkLabel : AllowanceLabel;

            var hobbies = ActivityCatalog.HobbyIds.ToList();
            var chosen = new List<string>();
            for (var salt = 20; chosen.Count < 2 && salt < 40 && hobbies.Count > 0; salt++)
            {
                var hobby = hobbies[(int)(Roll(salt) * hobbies.Count)];
                if (!chosen.Contains(hobby))
                {
                    chosen.Add(hobby);
                }
            }

            r.SetHobbies(chosen);
            return r;
        }

        /// <summary>A personality trait between 0.15 and 0.9: nobody is entirely one thing.</summary>
        private static double Trait(double roll) => 0.15 + (roll * 0.75);
    }
}
