using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinyCity.Simulation;
using TinyCity.Simulation.AI;
using TinyCity.Simulation.Content;
using TinyCity.Simulation.Persistence;
using TinyCity.Simulation.Residents;

namespace TinyCity.Tests.EditMode
{
    /// <summary>Tiny City 2: a brief the player gives everyone, and roles anyone may take up, change or drop in their morning plan.</summary>
    public class RoleTests
    {
        private static TownSimulation NewGame(WorldBrief brief = null)
        {
            var sim = FlatLand.CreateNewGame(brief: brief);
            sim.AiRunsEverything = true;
            sim.FoodMustBeMade = true;
            return sim;
        }

        private static List<DayPlanEntry> Plan() => new List<DayPlanEntry> { new DayPlanEntry(9, "Find food") };

        [Test]
        public void TheBriefAndRoles_AreReadFromResidentsJson_OrTheShippedOnesAreUsed()
        {
            var brief = ResidentProfiles.ParseBrief("{\"brief\":\"Be kind.\",\"roles\":[{\"name\":\"Farmer\",\"about\":\"grows food\"},\"Hunter\",{\"about\":\"no name\"},\"farmer\"]}", out var problems);
            Assert.That(brief.Text, Is.EqualTo("Be kind."));
            Assert.That(brief.Roles.Select(r => r.Name), Is.EqualTo(new[] { "farmer", "hunter" }), "lower case, no repeats");
            Assert.That(problems.Count, Is.EqualTo(1), "the role with no name");

            var none = ResidentProfiles.ParseBrief("{\"residents\":[]}", out _);
            Assert.That(none.Text, Is.EqualTo(WorldBrief.Default.Text));
            Assert.That(none.Roles.Count, Is.EqualTo(WorldBrief.Default.Roles.Count));

            var written = ResidentProfiles.Write(StarterTown.DefaultProfiles(), brief);
            var back = ResidentProfiles.ParseBrief(written, out _);
            Assert.That((back.Text, back.Roles.Count), Is.EqualTo(("Be kind.", 2)));
        }

        [Test]
        public void EveryonesPrompts_CarryTheBrief_AndTheRolesOnOffer()
        {
            var sim = NewGame(new WorldBrief("Stay alive first.", new[] { new RoleOption("farmer", "grows food"), new RoleOption("fisher", "") }));
            var r = sim.Residents[0];
            var persona = PromptText.Persona(r, sim);
            Assert.That(persona, Does.Contain("What everyone here knows: Stay alive first.").And.Contain("You haven't taken up any role.").And.Not.Contain("neon-lit city"));

            var plan = DayPlanPrompt.UserText(sim, r, DayPlan.DayFor(sim.Time));
            Assert.That(plan, Does.Contain("farmer (grows food); fisher").And.Contain("\"role\""));
        }

        [Test]
        public void AResident_ChoosesTheirOwnRole_ChangesIt_OrGivesItUp()
        {
            var sim = NewGame();
            var r = sim.Residents[0];
            var other = sim.Residents[1];
            Assert.That(sim.Residents.All(o => o.Role == null), Is.True, "nobody is given a role");

            Assert.That(sim.ApplyDayPlan(r.Id, DayPlan.DayFor(sim.Time), Plan(), "Farmer"), Is.EqualTo(DecisionOutcome.Applied));
            Assert.That(r.Role, Is.EqualTo("farmer"));
            Assert.That(PromptText.Persona(r, sim), Does.Contain("You have chosen to be the farmer."));
            Assert.That(sim.Memories.ForResident(r.Id).Any(m => m.Summary == "I decided to take up being the farmer."), Is.True);
            var others = new System.Text.StringBuilder();
            PromptText.AppendPeople(others, sim, other);
            Assert.That(others.ToString(), Does.Contain($"{r.FullName} (the farmer)"), "the others can see it");

            var loaded = SaveSerializer.Load(SaveSerializer.Save(sim), FlatLand.CreateWorld()).Simulation;
            Assert.That(loaded.GetResident(r.Id).Role, Is.EqualTo("farmer"));
            Assert.That(loaded.Brief.Text, Is.EqualTo(WorldBrief.Default.Text), "the world keeps its brief");

            Assert.That(sim.ApplyDayPlan(other.Id, DayPlan.DayFor(sim.Time), Plan(), "astronaut"), Is.EqualTo(DecisionOutcome.Applied));
            Assert.That(other.Role, Is.Null, "a role not on offer is ignored");
            sim.Advance(24 * 60);
            Assert.That(sim.ApplyDayPlan(r.Id, DayPlan.DayFor(sim.Time), Plan(), TownSimulation.RoleNone), Is.EqualTo(DecisionOutcome.Applied));
            Assert.That(r.Role, Is.Null, "given up");
        }

        [Test]
        public void APersonality_IsReadFromTheProfile_SavedAndUsedInPrompts()
        {
            var profiles = ResidentProfiles.Parse("{\"residents\":[{\"id\":\"res-0001\",\"firstName\":\"Mabel\",\"lastName\":\"T\",\"age\":30,\"personality\":\"You're out for yourself and your own survival.\"}]}", StarterTown.StarterResidentIds, new string[0], out var problems);
            Assert.That(profiles[0].Personality, Is.EqualTo("You're out for yourself and your own survival."), string.Join("; ", problems));
            Assert.That(ResidentProfiles.Parse(ResidentProfiles.Write(profiles), StarterTown.StarterResidentIds, new string[0], out _)[0].Personality, Is.EqualTo(profiles[0].Personality));

            var sim = FlatLand.CreateNewGame(profiles);
            sim.AiRunsEverything = true;
            sim.FoodMustBeMade = true;
            var r = sim.Residents[0];
            Assert.That(r.Outlook, Is.EqualTo(profiles[0].Personality));
            Assert.That(PromptText.Persona(r, sim), Does.Contain("out for yourself"));
            var loaded = SaveSerializer.Load(SaveSerializer.Save(sim), FlatLand.CreateWorld()).Simulation;
            Assert.That(loaded.GetResident(r.Id).Outlook, Is.EqualTo(r.Outlook));
        }

        [Test]
        public void TheShippedResidents_EachHaveAPersonality()
        {
            var dir = new System.IO.DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir != null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "Assets", "StreamingAssets", "Configuration", "residents.json")))
            {
                dir = dir.Parent;
            }

            Assert.That(dir, Is.Not.Null, "residents.json not found above the test folder");
            var json = System.IO.File.ReadAllText(System.IO.Path.Combine(dir.FullName, "Assets", "StreamingAssets", "Configuration", "residents.json"));
            var profiles = ResidentProfiles.Parse(json, StarterTown.StarterResidentIds, new string[0], out var problems);
            Assert.That(problems, Is.Empty);
            Assert.That(profiles.All(p => p.Personality.Length > 10), Is.True);
        }

        [Test]
        public void DayPlanAnswers_CarryTheRole()
        {
            Assert.That(DayPlanPrompt.TryParse("{\"plan\":[{\"hour\":9,\"plan\":\"Fish\"}],\"role\":\"fisher\"}", out var entries, out var role, out var error), Is.True, error);
            Assert.That((entries.Count, role), Is.EqualTo((1, "fisher")));
        }
    }
}
