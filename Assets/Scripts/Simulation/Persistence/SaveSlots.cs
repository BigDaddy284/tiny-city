using System;
using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation.Time;

namespace TinyCity.Simulation.Persistence
{
    /// <summary>What the save list shows about a slot without loading the whole town.</summary>
    public sealed class SaveSummary
    {
        private SaveSummary(string error, int version, GameTime time, int residents, int living)
        {
            Error = error;
            Version = version;
            Time = time;
            Residents = residents;
            Living = living;
        }

        /// <summary>Why the save can't be read, or null when it looks fine.</summary>
        public string Error { get; }

        public bool IsReadable => Error == null;

        public int Version { get; }

        public GameTime Time { get; }

        public int Residents { get; }

        public int Living { get; }

        /// <summary>"Day 12, Friday 14:00 · 9 of 10 residents alive", or what is wrong with it.</summary>
        public string Describe()
        {
            if (!IsReadable)
            {
                return $"Can't be read: {Error}";
            }

            var older = Version < SaveSerializer.CurrentVersion ? " · from an older version, upgraded when loaded" : string.Empty;
            return $"Day {Time.Day}, {Time.Weekday} {Time.ClockText} · {Living} of {Residents} residents alive{older}";
        }

        /// <summary>
        /// Reads the date and residents from save text. A save that fails here won't load either, but a save that
        /// passes can still need repairs when it is loaded.
        /// </summary>
        public static SaveSummary Read(string json)
        {
            try
            {
                if (!(Json.Parse(json) is Dictionary<string, object> root) || !Equals(root.TryGetValue("format", out var f) ? f : null, SaveSerializer.Format))
                {
                    return Broken("it isn't a Tiny City save");
                }

                var version = root.TryGetValue("version", out var v) && v is double dv ? (int)dv : 0;
                if (version < 1 || version > SaveSerializer.CurrentVersion)
                {
                    return Broken(version > SaveSerializer.CurrentVersion ? $"it was made by a newer version of the game (save version {version})" : "it has no valid version");
                }

                var minutes = root.TryGetValue("gameMinutes", out var m) && m is double dm && dm >= 0 ? (long)dm : -1;
                if (minutes < 0)
                {
                    return Broken("it has no valid date");
                }

                var residents = root.TryGetValue("residents", out var r) && r is List<object> list ? list.OfType<Dictionary<string, object>>().ToList() : null;
                if (residents == null || residents.Count == 0)
                {
                    return Broken("it has no residents");
                }

                var living = residents.Count(x => !x.TryGetValue("death", out var d) || d == null);
                return new SaveSummary(null, version, new GameTime(minutes), residents.Count, living);
            }
            catch (Exception e) when (e is FormatException || e is InvalidCastException || e is OverflowException)
            {
                return Broken("the file is damaged");
            }
        }

        private static SaveSummary Broken(string error) => new SaveSummary(error, 0, new GameTime(0), 0, 0);
    }

    /// <summary>
    /// Slot ids and names. A slot id is the save's file name (without ".json"); the player sees the slot's name.
    /// Names are kept in a small index beside the saves, so copying a save file copies the town exactly.
    /// </summary>
    public sealed class SaveSlotIndex
    {
        /// <summary>The slot every save before Phase 5 build 3 used.</summary>
        public const string DefaultSlot = "town";

        public const int MaxNameLength = 40;

        public const string DefaultName = "My first town";

        private readonly Dictionary<string, string> names = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Names for every slot that has one, by slot id.</summary>
        public IReadOnlyDictionary<string, string> Names => names;

        /// <summary>Slot ids are "town" or "town-2", "town-3", …: safe as file names on any system.</summary>
        public static bool IsValidSlotId(string id)
        {
            if (id == DefaultSlot)
            {
                return true;
            }

            return id != null && id.StartsWith(DefaultSlot + "-", StringComparison.Ordinal)
                && int.TryParse(id.Substring(DefaultSlot.Length + 1), out var n) && n >= 2 && n.ToString() == id.Substring(DefaultSlot.Length + 1);
        }

        /// <summary>The first unused id: "town" if free, otherwise the lowest "town-N".</summary>
        public static string NewSlotId(IEnumerable<string> existing)
        {
            var taken = new HashSet<string>(existing ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            if (!taken.Contains(DefaultSlot))
            {
                return DefaultSlot;
            }

            for (var n = 2; ; n++)
            {
                var id = $"{DefaultSlot}-{n}";
                if (!taken.Contains(id))
                {
                    return id;
                }
            }
        }

        /// <summary>Trims a typed name and drops control characters; empty means no name.</summary>
        public static string CleanName(string name)
        {
            var kept = new string((name ?? string.Empty).Where(c => !char.IsControl(c)).ToArray()).Trim();
            return kept.Length > MaxNameLength ? kept.Substring(0, MaxNameLength).TrimEnd() : kept;
        }

        public string NameOf(string slotId)
        {
            if (slotId != null && names.TryGetValue(slotId, out var name))
            {
                return name;
            }

            return slotId == DefaultSlot ? DefaultName : $"Town {slotId?.Substring(slotId.LastIndexOf('-') + 1)}";
        }

        public void SetName(string slotId, string name)
        {
            var clean = CleanName(name);
            if (!IsValidSlotId(slotId))
            {
                return;
            }

            if (clean.Length == 0)
            {
                names.Remove(slotId);
            }
            else
            {
                names[slotId] = clean;
            }
        }

        public void Forget(string slotId) => names.Remove(slotId ?? string.Empty);

        /// <summary>A name for a copy that no other slot has yet: "Copy of X", "Copy of X (2)", ….</summary>
        public string CopyName(string sourceSlot, IEnumerable<string> slots)
        {
            var used = new HashSet<string>(slots.Select(NameOf), StringComparer.OrdinalIgnoreCase);
            var stem = CleanName("Copy of " + NameOf(sourceSlot));
            var candidate = stem;
            for (var n = 2; used.Contains(candidate); n++)
            {
                candidate = CleanName($"{stem.Substring(0, Math.Min(stem.Length, MaxNameLength - 5))} ({n})");
            }

            return candidate;
        }

        public string ToJson() => Json.Write(new Dictionary<string, object>
        {
            ["names"] = names.OrderBy(p => p.Key, StringComparer.Ordinal).ToDictionary(p => p.Key, p => (object)p.Value),
        });

        /// <summary>Reads an index written by <see cref="ToJson"/>. Anything damaged is skipped; a damaged file gives an empty index.</summary>
        public static SaveSlotIndex FromJson(string json)
        {
            var index = new SaveSlotIndex();
            try
            {
                if (Json.Parse(json ?? string.Empty) is Dictionary<string, object> root && root.TryGetValue("names", out var n) && n is Dictionary<string, object> map)
                {
                    foreach (var pair in map)
                    {
                        if (pair.Value is string name)
                        {
                            index.SetName(pair.Key, name);
                        }
                    }
                }
            }
            catch (FormatException)
            {
                // A damaged index only loses the names; the saves themselves are untouched.
            }

            return index;
        }
    }
}
