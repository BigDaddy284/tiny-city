using System;
using System.Globalization;

namespace TinyTown.Simulation.Economy
{
    /// <summary>Money is whole pence (a long), so sums are exact and saves are stable.</summary>
    public static class Money
    {
        public static string Format(long pence)
        {
            var sign = pence < 0 ? "-" : string.Empty;
            var abs = Math.Abs(pence);
            return string.Format(CultureInfo.InvariantCulture, "{0}£{1}.{2:00}", sign, abs / 100, abs % 100);
        }

        public static long Pounds(double pounds) => (long)Math.Round(pounds * 100);
    }
}
