namespace TinyTown.Simulation.Health
{
    /// <summary>
    /// Deterministic "dice": the same inputs always give the same number in [0, 1), on every platform and after
    /// saving and loading. Used wherever the game needs luck, such as catching a cold.
    /// </summary>
    public static class Chance
    {
        public static double Roll(string id, long day, int salt)
        {
            unchecked
            {
                // FNV-1a over the id, then the day and salt, then a final mix. string.GetHashCode is not stable, so it isn't used.
                var hash = 14695981039346656037UL;
                foreach (var c in id ?? string.Empty)
                {
                    hash = (hash ^ c) * 1099511628211UL;
                }

                hash = (hash ^ (ulong)day) * 1099511628211UL;
                hash = (hash ^ (ulong)salt) * 1099511628211UL;
                hash ^= hash >> 33;
                hash *= 0xff51afd7ed558ccdUL;
                hash ^= hash >> 33;
                return (hash >> 11) / (double)(1UL << 53);
            }
        }
    }
}
