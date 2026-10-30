using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation.Economy;

namespace TinyCity.Simulation.World
{
    /// <summary>
    /// What the card shown when the pointer is over a building says: its name, whether it's open (a business) or who
    /// lives there (a home), and who is inside right now. A building with several doors, such as a tower of flats,
    /// covers every place inside it. It only reads the town.
    /// </summary>
    public sealed class PlaceInfo
    {
        /// <summary>The most names listed on one line before "and N more".</summary>
        public const int MaxNames = 8;

        private PlaceInfo(string title, IReadOnlyList<string> lines, string enterMapId)
        {
            Title = title;
            Lines = lines;
            EnterMapId = enterMapId;
        }

        public string Title { get; }

        public IReadOnlyList<string> Lines { get; }

        /// <summary>The place a camera shows when the building is opened: the first with someone in it, otherwise the first.</summary>
        public string EnterMapId { get; }

        /// <summary>Describes the building whose inside is <paramref name="mapIds"/>, or returns null when none of them exist.</summary>
        public static PlaceInfo Of(TownSimulation simulation, IEnumerable<string> mapIds)
        {
            var maps = new List<TileMap>();
            foreach (var id in mapIds ?? Enumerable.Empty<string>())
            {
                if (id != null && simulation.World.TryGetMap(id, out var map) && !map.IsExterior && !maps.Contains(map))
                {
                    maps.Add(map);
                }
            }

            if (maps.Count == 0)
            {
                return null;
            }

            maps = maps.OrderBy(m => m.Name, System.StringComparer.OrdinalIgnoreCase).ThenBy(m => m.Id, System.StringComparer.Ordinal).ToList();

            var ids = new HashSet<string>(maps.Select(m => m.Id));
            var living = simulation.Residents.Where(r => r.IsAlive).ToList();
            var lines = new List<string>();
            var business = maps.Select(m => simulation.World.BusinessOnMap(m.Id)).FirstOrDefault(b => b != null);
            if (business != null)
            {
                lines.Add(OpenText(simulation, business));
            }
            else
            {
                var home = living.Where(r => r.HomeMapId != null && ids.Contains(r.HomeMapId)).Select(r => r.FullName).ToList();
                lines.Add(home.Count == 0 ? "Nobody lives here" : (maps.Count > 1 ? "Homes of " : "Home of ") + Names(home));
            }

            var inside = living.Where(r => ids.Contains(r.Location.MapId)).Select(r => r.FullName).ToList();
            lines.Add(inside.Count == 0 ? "Nobody inside" : "Inside: " + Names(inside));

            var enter = maps.FirstOrDefault(m => living.Any(r => r.Location.MapId == m.Id)) ?? maps[0];
            return new PlaceInfo(business?.Name ?? TitleOf(maps), lines, enter.Id);
        }

        private static string OpenText(TownSimulation simulation, Business business)
        {
            if (simulation.Economy.IsOpen(business, simulation.Time))
            {
                return $"Open now, {business.HoursText}";
            }

            if (business.IsOpen(simulation.Time))
            {
                return "Closed, nobody at work";
            }

            return business.IsOpenOn(simulation.Time.Weekday) ? $"Closed, open {business.HoursText}" : "Closed today";
        }

        /// <summary>One place's name, or for several (flats in a tower) the part of the first name before its comma.</summary>
        private static string TitleOf(IReadOnlyList<TileMap> maps)
        {
            var name = maps[0].Name;
            var comma = name.IndexOf(',');
            return maps.Count > 1 && comma > 0 ? name.Substring(0, comma) : name;
        }

        private static string Names(IReadOnlyList<string> names)
        {
            if (names.Count > MaxNames)
            {
                return string.Join(", ", names.Take(MaxNames)) + $" and {names.Count - MaxNames} more";
            }

            return names.Count == 1 ? names[0] : string.Join(", ", names.Take(names.Count - 1)) + " and " + names[names.Count - 1];
        }
    }
}
