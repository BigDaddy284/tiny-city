using System.Linq;
using NUnit.Framework;
using TinyCity.Simulation.Content;
using TinyCity.Simulation.Residents;

namespace TinyCity.Tests.EditMode
{
    /// <summary>The inspector's people list and "find a person" box.</summary>
    public class PeopleListTests
    {
        [Test]
        public void List_FiltersByLivingOrDied_AndSorts()
        {
            var sim = StarterTown.CreateNewGame();
            var arthur = sim.GetResident(StarterTown.ArthurId);
            arthur.IsAlive = false;

            var everyone = PeopleList.Of(sim.Residents, PeopleFilter.Everyone, PeopleOrder.FirstName);
            Assert.That(everyone.Count, Is.EqualTo(sim.Residents.Count));
            Assert.That(everyone.Select(r => r.FirstName).ToList(), Is.EqualTo(sim.Residents.Select(r => r.FirstName).OrderBy(n => n, System.StringComparer.OrdinalIgnoreCase).ToList()));

            Assert.That(PeopleList.Of(sim.Residents, PeopleFilter.Died, PeopleOrder.FirstName).Select(r => r.Id).ToList(), Is.EqualTo(new[] { arthur.Id }));
            Assert.That(PeopleList.Of(sim.Residents, PeopleFilter.Living, PeopleOrder.FirstName).Any(r => r == arthur), Is.False);

            var oldest = PeopleList.Of(sim.Residents, PeopleFilter.Everyone, PeopleOrder.Oldest);
            var youngest = PeopleList.Of(sim.Residents, PeopleFilter.Everyone, PeopleOrder.Youngest);
            Assert.That(oldest[0].AgeYears, Is.EqualTo(sim.Residents.Max(r => r.AgeYears)));
            Assert.That(youngest[0].AgeYears, Is.EqualTo(sim.Residents.Min(r => r.AgeYears)));

            var byLast = PeopleList.Of(sim.Residents, PeopleFilter.Everyone, PeopleOrder.LastName).Select(r => r.LastName).ToList();
            Assert.That(byLast, Is.EqualTo(byLast.OrderBy(n => n, System.StringComparer.OrdinalIgnoreCase).ToList()));
        }

        [Test]
        public void Search_ShowsMatchesAsYouType_BestFirst()
        {
            var sim = StarterTown.CreateNewGame();
            var arthur = sim.GetResident(StarterTown.ArthurId);

            Assert.That(PeopleList.Search(sim.Residents, "", 6).Count, Is.EqualTo(0), "nothing typed, nothing shown");
            Assert.That(PeopleList.Search(sim.Residents, "zzqq", 6).Count, Is.EqualTo(0));
            Assert.That(PeopleList.Search(sim.Residents, "ar", 6)[0], Is.EqualTo(arthur), "a first name that starts with it comes first");
            Assert.That(PeopleList.Search(sim.Residents, "ARTH", 6)[0], Is.EqualTo(arthur), "case doesn't matter");
            Assert.That(PeopleList.Search(sim.Residents, arthur.LastName.Substring(0, 3), 6).Contains(arthur), Is.True, "surnames match too");
            Assert.That(PeopleList.Search(sim.Residents, "a", 2).Count, Is.LessThanOrEqualTo(2));
            Assert.That(PeopleList.Search(sim.Residents, "e", 20).All(r => r.FullName.ToLowerInvariant().Contains("e")), Is.True);
        }
    }
}
