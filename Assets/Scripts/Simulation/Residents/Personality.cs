using System;

namespace TinyCity.Simulation.Residents
{
    /// <summary>
    /// Core traits from 0 to 1. They change how quickly some needs fall and which leisure a resident prefers.
    /// Only game logic changes them; the AI may never rewrite them directly.
    /// </summary>
    public sealed class Personality
    {
        public Personality(double active, double sociable, double tidy, double bookish)
        {
            Active = Clamp01(active);
            Sociable = Clamp01(sociable);
            Tidy = Clamp01(tidy);
            Bookish = Clamp01(bookish);
        }

        public double Active { get; }

        public double Sociable { get; }

        public double Tidy { get; }

        public double Bookish { get; }

        public string Describe()
        {
            return $"{Word(Active, "Energetic", "Easy-going")}, {Word(Sociable, "outgoing", "private")}, " +
                $"{Word(Tidy, "tidy", "relaxed about mess")}, {Word(Bookish, "loves books", "not much of a reader")}";
        }

        private static string Word(double value, string high, string low) => value >= 0.5 ? high : low;

        private static double Clamp01(double v) => double.IsNaN(v) ? 0.5 : Math.Max(0, Math.Min(1, v));
    }
}
