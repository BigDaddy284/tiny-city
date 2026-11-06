using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.World;

namespace TinyCity.Simulation.Content
{
    /// <summary>
    /// The twenty people Tiny City 2 can start with (owner, 5 November: twenty to start, and the most there can be).
    /// Each has a stable id (their look comes from it, see the art's <c>ResidentLook</c>), a name, an age, traits and a
    /// blunt one-sentence personality. Ids are stable because saves and residents.json refer to them.
    /// </summary>
    public static class StarterResidents
    {
        public const string ResidentId = "res-0001";
        public const string ArthurId = "res-0002";
        public const string NellId = "res-0003";
        public const string RosaId = "res-0004";
        public const string TomId = "res-0005";
        public const string PriyaId = "res-0006";
        public const string IvyId = "res-0007";
        public const string SamuelId = "res-0008";
        public const string FelixId = "res-0009";
        public const string EdithId = "res-0010";
        public const string MarcusId = "res-0011";
        public const string LolaId = "res-0012";
        public const string DannyId = "res-0013";
        public const string RubyId = "res-0014";
        public const string WalterId = "res-0015";

        private sealed class Design
        {
            public Design(string id, string first, string last, int age, Personality personality, double needsLevel, Dictionary<NeedType, double> needs = null)
            {
                Id = id;
                First = first;
                Last = last;
                Age = age;
                Personality = personality;
                NeedsLevel = needsLevel;
                Needs = needs ?? new Dictionary<NeedType, double>();
            }

            public string Id { get; }

            public string First { get; }

            public string Last { get; }

            public int Age { get; }

            public Personality Personality { get; }

            public double NeedsLevel { get; }

            public Dictionary<NeedType, double> Needs { get; }
        }

        private static readonly Design[] Designs =
        {
            new Design("res-0001", "Mabel", "Thornbury", 34, new Personality(active: 0.7, sociable: 0.6, tidy: 0.65, bookish: 0.55), 75, new Dictionary<NeedType, double> { [NeedType.Hunger] = 55 }),
            new Design("res-0002", "Arthur", "Bramble", 71, new Personality(active: 0.3, sociable: 0.8, tidy: 0.55, bookish: 0.75), 70, new Dictionary<NeedType, double> { [NeedType.Social] = 45 }),
            new Design("res-0003", "Nell", "Finch", 27, new Personality(active: 0.65, sociable: 0.35, tidy: 0.3, bookish: 0.25), 72, new Dictionary<NeedType, double> { [NeedType.Thirst] = 55 }),
            new Design("res-0004", "Rosa", "Pennywhistle", 46, new Personality(active: 0.5, sociable: 0.7, tidy: 0.8, bookish: 0.4), 74, new Dictionary<NeedType, double>()),
            new Design("res-0005", "Tom", "Hartley", 31, new Personality(active: 0.6, sociable: 0.6, tidy: 0.4, bookish: 0.3), 72, new Dictionary<NeedType, double> { [NeedType.Hunger] = 60 }),
            new Design("res-0006", "Priya", "Lane", 38, new Personality(active: 0.55, sociable: 0.85, tidy: 0.5, bookish: 0.6), 76, new Dictionary<NeedType, double> { [NeedType.Social] = 50 }),
            new Design("res-0007", "Ivy", "Marsh", 23, new Personality(active: 0.75, sociable: 0.45, tidy: 0.6, bookish: 0.35), 75, new Dictionary<NeedType, double>()),
            new Design("res-0008", "Samuel", "Okafor", 54, new Personality(active: 0.4, sociable: 0.55, tidy: 0.75, bookish: 0.85), 74, new Dictionary<NeedType, double>()),
            new Design("res-0009", "Felix", "Wren", 19, new Personality(active: 0.7, sociable: 0.5, tidy: 0.2, bookish: 0.5), 70, new Dictionary<NeedType, double> { [NeedType.Fun] = 50 }),
            new Design("res-0010", "Edith", "Crumb", 82, new Personality(active: 0.15, sociable: 0.6, tidy: 0.7, bookish: 0.8), 72, new Dictionary<NeedType, double>()),
            new Design("res-0011", "Marcus", "Vale", 29, new Personality(active: 0.9, sociable: 0.6, tidy: 0.65, bookish: 0.25), 76, new Dictionary<NeedType, double>()),
            new Design("res-0012", "Lola", "Brightwater", 22, new Personality(active: 0.6, sociable: 0.85, tidy: 0.35, bookish: 0.3), 74, new Dictionary<NeedType, double> { [NeedType.Fun] = 55 }),
            new Design("res-0013", "Danny", "Cho", 20, new Personality(active: 0.4, sociable: 0.45, tidy: 0.3, bookish: 0.65), 72, new Dictionary<NeedType, double> { [NeedType.Social] = 55 }),
            new Design("res-0014", "Ruby", "Starling", 41, new Personality(active: 0.45, sociable: 0.75, tidy: 0.6, bookish: 0.45), 74, new Dictionary<NeedType, double>()),
            new Design("res-0015", "Walter", "Penn", 76, new Personality(active: 0.35, sociable: 0.5, tidy: 0.7, bookish: 0.7), 72, new Dictionary<NeedType, double>()),
            new Design("res-0016", "Iris", "Calloway", 33, new Personality(active: 0.55, sociable: 0.5, tidy: 0.8, bookish: 0.6), 74),
            new Design("res-0017", "Bram", "Kettle", 52, new Personality(active: 0.75, sociable: 0.3, tidy: 0.45, bookish: 0.2), 72),
            new Design("res-0018", "Juno", "Reyes", 22, new Personality(active: 0.8, sociable: 0.75, tidy: 0.25, bookish: 0.35), 72),
            new Design("res-0019", "Edgar", "Flint", 61, new Personality(active: 0.5, sociable: 0.35, tidy: 0.7, bookish: 0.3), 72),
            new Design("res-0020", "Maya", "Quill", 44, new Personality(active: 0.5, sociable: 0.85, tidy: 0.6, bookish: 0.8), 76),
        };

        /// <summary>What type of person each one is, bluntly: the personalities the shipped residents.json gives.</summary>
        private static readonly Dictionary<string, string> DefaultPersonalities = new Dictionary<string, string>
        {
            ["res-0001"] = "Kind, patient and gentle. Puts everyone else first and never complains.",
            ["res-0002"] = "Nosy, chatty and opinionated. Knows everyone's business and says what he thinks.",
            ["res-0003"] = "Quiet, practical and controlling. Happiest fixing things, awkward with people.",
            ["res-0004"] = "Tidy, careful and tight-fisted. Counts everything and hates waste.",
            ["res-0005"] = "Proud, warm and a bit of a show-off. Loves feeding people and wants credit for it.",
            ["res-0006"] = "Friendly, curious and caring. Notices when someone is struggling and keeps the peace.",
            ["res-0007"] = "Restless, tough and selfish. Works hard but looks out for herself first.",
            ["res-0008"] = "Calm, careful and soft-hearted. Worries about everyone and can't say no.",
            ["res-0009"] = "Cheerful, trusting and reckless. Makes friends fast and jumps in without thinking.",
            ["res-0010"] = "Cautious, sharp-tongued and stubborn. Full of opinions and always keeps something back.",
            ["res-0011"] = "Strong, disciplined and bossy. Impatient with anyone who can't keep up.",
            ["res-0012"] = "Loud, fun and easily bored. Loves a crowd and hates dull work.",
            ["res-0013"] = "Shy, clever and stubborn. Would rather build alone than talk.",
            ["res-0014"] = "Shrewd, competitive and tough. Always after the best deal and hates losing.",
            ["res-0015"] = "Slow, steady and generous. Thinks before he acts and shares with hard workers.",
            ["res-0016"] = "Fair, methodical and serious. Wants things done properly and traded honestly.",
            ["res-0017"] = "Stubborn, proud and independent. Won't take orders from anyone.",
            ["res-0018"] = "Eager, curious and unsettled. Keen to prove herself and tries anything once.",
            ["res-0019"] = "Blunt, gruff and hard-working. Has no time for talk or excuses.",
            ["res-0020"] = "Organised, encouraging and a little bossy. Gets people working together.",
        };

        /// <summary>Every id a profile can use, in order.</summary>
        public static IReadOnlyList<string> Ids => Designs.Select(d => d.Id).ToList();

        /// <summary>
        /// The people a new world starts with, all standing at <paramref name="at"/> (the caller spreads them out).
        /// With <paramref name="profiles"/> (from <c>residents.json</c>), only those listed start, in their order, with
        /// the profile's name, age, background and personality; their look and traits come with their id.
        /// </summary>
        public static IReadOnlyList<Resident> Create(Location at, IReadOnlyList<ResidentProfile> profiles = null)
        {
            var chosen = profiles == null || profiles.Count == 0
                ? Designs.Select(d => (design: d, profile: (ResidentProfile)null))
                : profiles.Select(p => (design: Designs.FirstOrDefault(d => d.Id == p.Id), profile: p)).Where(x => x.design != null);
            var residents = new List<Resident>();
            foreach (var (d, profile) in chosen)
            {
                var needs = new Needs(d.NeedsLevel);
                foreach (var pair in d.Needs)
                {
                    needs[pair.Key] = pair.Value;
                }

                var r = new Resident(d.Id, profile?.FirstName ?? d.First, profile?.LastName ?? d.Last, profile?.Age ?? d.Age, d.Personality, at, needs)
                {
                    About = profile?.About ?? string.Empty,
                    Outlook = profile?.Personality ?? (DefaultPersonalities.TryGetValue(d.Id, out var personality) ? personality : string.Empty),
                };
                residents.Add(r);
            }

            return residents;
        }

        /// <summary>
        /// The profiles <c>residents.json</c> starts with: everyone as designed, with a blunt line on what type of
        /// person they are and no backstory.
        /// </summary>
        public static IReadOnlyList<ResidentProfile> DefaultProfiles() =>
            Designs.Select(d => new ResidentProfile(d.Id, d.First, d.Last, d.Age, string.Empty,
                DefaultPersonalities.TryGetValue(d.Id, out var p) ? p : null)).ToList();
    }
}
