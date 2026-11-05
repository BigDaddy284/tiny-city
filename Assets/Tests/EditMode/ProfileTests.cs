using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinyCity.Simulation.Content;
using TinyCity.Simulation.Persistence;
using TinyCity.Simulation.Residents;

namespace TinyCity.Tests.EditMode
{
    /// <summary>The player's resident profiles (residents.json): reading, checking, and starting a town from them.</summary>
    public class ProfileTests
    {
        private static List<string> Businesses => StarterTown.CreateWorld().Businesses.Select(b => b.Id).ToList();

        private static List<ResidentProfile> Parse(string json, out List<string> problems) =>
            ResidentProfiles.Parse(json, StarterTown.StarterResidentIds, Businesses, out problems);

        [Test]
        public void DefaultProfiles_RoundTrip_AndStartTheDesignedTown()
        {
            var json = ResidentProfiles.Write(StarterTown.DefaultProfiles());
            var profiles = Parse(json, out var problems);

            Assert.That(problems, Is.Empty);
            Assert.That(profiles.Count, Is.EqualTo(StarterTown.StarterResidentIds.Count));

            // Tiny City 2's shipped profiles start everyone with no money (owner, 5 November), as the open land does without them.
            Assert.That(profiles.All(p => p.MoneyPence == 0), Is.True);
            var fromProfiles = FlatLand.CreateNewGame(profiles);
            var designed = FlatLand.CreateNewGame();
            Assert.That(SaveSerializer.Save(fromProfiles), Is.EqualTo(SaveSerializer.Save(designed)));
            Assert.That(designed.Residents.All(r => r.About.Length > 0), Is.True);
            Assert.That(designed.Residents.All(r => r.OwnsBusinessId == null), Is.True);
        }

        [Test]
        public void Profiles_ChooseWhoStarts_AndTheirDetails()
        {
            const string json = "{\"residents\":[" +
                "{\"id\":\"res-0004\",\"firstName\":\"Rosa\",\"lastName\":\"Grey\",\"age\":50,\"about\":\"Runs the shop and knows every price.\",\"money\":75.5,\"owns\":\"biz-store\"}," +
                "{\"id\":\"res-0001\",\"firstName\":\"Mae\",\"lastName\":\"Thornbury\",\"age\":30,\"about\":\"Grows things.\",\"money\":10}]}";
            var profiles = Parse(json, out var problems);
            var sim = StarterTown.CreateNewGame(profiles);

            Assert.That(problems, Is.Empty);
            Assert.That(sim.Residents.Select(r => r.Id), Is.EqualTo(new[] { "res-0004", "res-0001" }));
            var rosa = sim.Residents[0];
            Assert.That(rosa.FullName, Is.EqualTo("Rosa Grey"));
            Assert.That(rosa.AgeYears, Is.EqualTo(50));
            Assert.That(rosa.MoneyPence, Is.EqualTo(7550));
            Assert.That(rosa.About, Is.EqualTo("Runs the shop and knows every price."));
            Assert.That(rosa.OwnsBusinessId, Is.EqualTo("biz-store"));
            Assert.That(sim.Residents[1].FirstName, Is.EqualTo("Mae"));
        }

        [Test]
        public void BadProfiles_AreLeftOutOrRepaired_WithReasons()
        {
            const string json = "{\"residents\":[" +
                "{\"firstName\":\"No id\",\"age\":30}," +
                "{\"id\":\"res-0099\",\"firstName\":\"Stranger\",\"age\":30}," +
                "{\"id\":\"res-0001\",\"firstName\":\"\",\"age\":30}," +
                "{\"id\":\"res-0002\",\"firstName\":\"Arthur\",\"age\":12}," +
                "{\"id\":\"res-0003\",\"firstName\":\"Nell\",\"age\":27,\"money\":-5,\"owns\":\"biz-moon\"}," +
                "{\"id\":\"res-0004\",\"firstName\":\"Rosa\",\"age\":46,\"owns\":\"biz-store\"}," +
                "{\"id\":\"res-0005\",\"firstName\":\"Tom\",\"age\":31,\"owns\":\"biz-store\"}," +
                "{\"id\":\"res-0004\",\"firstName\":\"Rosa again\",\"age\":46}]}";
            var profiles = Parse(json, out var problems);

            Assert.That(profiles.Select(p => p.Id), Is.EqualTo(new[] { "res-0003", "res-0004", "res-0005" }));
            Assert.That(profiles[0].MoneyPence, Is.EqualTo(0));
            Assert.That(profiles[0].OwnsBusinessId, Is.Null);
            Assert.That(profiles[1].OwnsBusinessId, Is.EqualTo("biz-store"));
            Assert.That(profiles[2].OwnsBusinessId, Is.Null);
            Assert.That(problems.Count, Is.EqualTo(8));
        }

        [TestCase("not json")]
        [TestCase("{}")]
        [TestCase("[]")]
        public void UnreadableFile_GivesNoProfiles_AndTheDesignedTownStarts(string json)
        {
            var profiles = Parse(json, out var problems);

            Assert.That(profiles, Is.Empty);
            Assert.That(problems, Is.Not.Empty);
            Assert.That(StarterTown.CreateNewGame(profiles).Residents.Count, Is.EqualTo(StarterTown.StarterResidentIds.Count));
        }

        [Test]
        public void AboutAndBusiness_SurviveASave()
        {
            var profiles = Parse("{\"residents\":[{\"id\":\"res-0004\",\"firstName\":\"Rosa\",\"age\":46,\"about\":\"Shopkeeper at heart.\",\"owns\":\"biz-store\"}]}", out _);
            var sim = StarterTown.CreateNewGame(profiles);

            var loaded = SaveSerializer.Load(SaveSerializer.Save(sim), StarterTown.CreateWorld());

            Assert.That(loaded.Success, Is.True);
            Assert.That(loaded.Simulation.Residents[0].About, Is.EqualTo("Shopkeeper at heart."));
            Assert.That(loaded.Simulation.Residents[0].OwnsBusinessId, Is.EqualTo("biz-store"));
        }

        [Test]
        public void TheAiIsToldTheResidentsBackground()
        {
            var sim = StarterTown.CreateNewGame(Parse("{\"residents\":[{\"id\":\"res-0001\",\"firstName\":\"Mabel\",\"age\":34,\"about\":\"Secretly writes poems.\"}]}", out _));

            Assert.That(TinyCity.Simulation.AI.PromptText.Persona(sim.Residents[0]), Does.Contain("Secretly writes poems."));
        }
    }
}
