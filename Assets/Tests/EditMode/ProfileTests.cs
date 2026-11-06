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
        private static List<ResidentProfile> Parse(string json, out List<string> problems) =>
            ResidentProfiles.Parse(json, StarterResidents.Ids, out problems);

        [Test]
        public void DefaultProfiles_RoundTrip_AndStartTheDesignedTown()
        {
            var json = ResidentProfiles.Write(StarterResidents.DefaultProfiles());
            var profiles = Parse(json, out var problems);

            Assert.That(problems, Is.Empty);
            Assert.That(profiles.Count, Is.EqualTo(StarterResidents.Ids.Count));

            var fromProfiles = FlatLand.CreateNewGame(profiles);
            var designed = FlatLand.CreateNewGame();
            Assert.That(SaveSerializer.Save(fromProfiles), Is.EqualTo(SaveSerializer.Save(designed)));
            Assert.That(designed.Residents.All(r => r.About.Length == 0 && r.Outlook.Length > 0), Is.True, "no backstories, only what type of person they are");
        }

        [Test]
        public void Profiles_ChooseWhoStarts_AndTheirDetails()
        {
            const string json = "{\"residents\":[" +
                "{\"id\":\"res-0004\",\"firstName\":\"Rosa\",\"lastName\":\"Grey\",\"age\":50,\"about\":\"Runs the shop and knows every price.\"}," +
                "{\"id\":\"res-0001\",\"firstName\":\"Mae\",\"lastName\":\"Thornbury\",\"age\":30,\"about\":\"Grows things.\"}]}";
            var profiles = Parse(json, out var problems);
            var sim = FlatLand.CreateNewGame(profiles);

            Assert.That(problems, Is.Empty);
            Assert.That(sim.Residents.Select(r => r.Id), Is.EqualTo(new[] { "res-0004", "res-0001" }));
            var rosa = sim.Residents[0];
            Assert.That(rosa.FullName, Is.EqualTo("Rosa Grey"));
            Assert.That(rosa.AgeYears, Is.EqualTo(50));
            Assert.That(rosa.About, Is.EqualTo("Runs the shop and knows every price."));
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
                "{\"id\":\"res-0003\",\"firstName\":\"Nell\",\"age\":27}," +
                "{\"id\":\"res-0004\",\"firstName\":\"Rosa\",\"age\":46}," +
                "{\"id\":\"res-0005\",\"firstName\":\"Tom\",\"age\":31}," +
                "{\"id\":\"res-0004\",\"firstName\":\"Rosa again\",\"age\":46}]}";
            var profiles = Parse(json, out var problems);

            Assert.That(profiles.Select(p => p.Id), Is.EqualTo(new[] { "res-0003", "res-0004", "res-0005" }));
            Assert.That(problems.Count, Is.EqualTo(5));
        }

        [TestCase("not json")]
        [TestCase("{}")]
        [TestCase("[]")]
        public void UnreadableFile_GivesNoProfiles_AndTheDesignedTownStarts(string json)
        {
            var profiles = Parse(json, out var problems);

            Assert.That(profiles, Is.Empty);
            Assert.That(problems, Is.Not.Empty);
            Assert.That(FlatLand.CreateNewGame(profiles).Residents.Count, Is.EqualTo(StarterResidents.Ids.Count));
        }

        [Test]
        public void About_SurvivesASave()
        {
            var profiles = Parse("{\"residents\":[{\"id\":\"res-0004\",\"firstName\":\"Rosa\",\"age\":46,\"about\":\"Shopkeeper at heart.\"}]}", out _);
            var sim = FlatLand.CreateNewGame(profiles);

            var loaded = SaveSerializer.Load(SaveSerializer.Save(sim), FlatLand.CreateWorld());

            Assert.That(loaded.Success, Is.True);
            Assert.That(loaded.Simulation.Residents[0].About, Is.EqualTo("Shopkeeper at heart."));
        }

        [Test]
        public void TheAiIsToldTheResidentsBackground()
        {
            var sim = FlatLand.CreateNewGame(Parse("{\"residents\":[{\"id\":\"res-0001\",\"firstName\":\"Mabel\",\"age\":34,\"about\":\"Secretly writes poems.\"}]}", out _));

            Assert.That(TinyCity.Simulation.AI.PromptText.Persona(sim.Residents[0], sim), Does.Contain("Secretly writes poems."));
        }
    }
}
