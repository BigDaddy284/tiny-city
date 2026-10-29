using System;
using System.Collections.Generic;
using System.Linq;

namespace TinyCity.Simulation.Residents
{
    /// <summary>Who the people list shows.</summary>
    public enum PeopleFilter
    {
        Everyone,
        Living,
        Died,
    }

    /// <summary>How the people list is sorted.</summary>
    public enum PeopleOrder
    {
        FirstName,
        LastName,
        Oldest,
        Youngest,
    }

    /// <summary>
    /// The inspector's list of residents and its "find a person" box. It only reads the residents, so the
    /// observer can look anyone up without changing the town.
    /// </summary>
    public static class PeopleList
    {
        /// <summary>The residents the filter lets through, in the chosen order. Ties keep their id order.</summary>
        public static List<Resident> Of(IEnumerable<Resident> residents, PeopleFilter filter, PeopleOrder order)
        {
            var shown = residents.Where(r => filter == PeopleFilter.Everyone || r.IsAlive == (filter == PeopleFilter.Living));
            IOrderedEnumerable<Resident> sorted;
            switch (order)
            {
                case PeopleOrder.LastName:
                    sorted = shown.OrderBy(r => r.LastName, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.FirstName, StringComparer.OrdinalIgnoreCase);
                    break;
                case PeopleOrder.Oldest:
                    sorted = shown.OrderByDescending(r => r.AgeYears);
                    break;
                case PeopleOrder.Youngest:
                    sorted = shown.OrderBy(r => r.AgeYears);
                    break;
                default:
                    sorted = shown.OrderBy(r => r.FirstName, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.LastName, StringComparer.OrdinalIgnoreCase);
                    break;
            }

            return sorted.ThenBy(r => r.Id, StringComparer.Ordinal).ToList();
        }

        /// <summary>
        /// Residents whose name matches what has been typed so far, best first: names that start with it, then names
        /// with a word that starts with it, then names that merely contain it. Nothing typed matches nobody.
        /// </summary>
        public static List<Resident> Search(IEnumerable<Resident> residents, string text, int max)
        {
            var typed = (text ?? string.Empty).Trim();
            if (typed.Length == 0 || max <= 0)
            {
                return new List<Resident>();
            }

            return residents
                .Select(r => (resident: r, rank: Rank(r.FullName, typed)))
                .Where(m => m.rank >= 0)
                .OrderBy(m => m.rank)
                .ThenBy(m => m.resident.FullName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(m => m.resident.Id, StringComparer.Ordinal)
                .Take(max)
                .Select(m => m.resident)
                .ToList();
        }

        private static int Rank(string name, string typed)
        {
            if (name.StartsWith(typed, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (name.Split(' ').Any(word => word.StartsWith(typed, StringComparison.OrdinalIgnoreCase)))
            {
                return 1;
            }

            return name.IndexOf(typed, StringComparison.OrdinalIgnoreCase) >= 0 ? 2 : -1;
        }
    }
}
