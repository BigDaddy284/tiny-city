using System;

namespace TinyTown.Simulation.Residents
{
    public enum NeedType
    {
        Hunger,
        Thirst,
        Energy,
        Hygiene,
        Toilet,
        Social,
        Exercise,
        Fun,
    }

    /// <summary>
    /// Need levels from 0 (desperate) to 100 (fully satisfied). Values are always clamped and never NaN.
    /// </summary>
    public sealed class Needs
    {
        public const double Min = 0;
        public const double Max = 100;
        public static readonly int Count = Enum.GetValues(typeof(NeedType)).Length;

        private readonly double[] values = new double[Count];

        public Needs(double initial = 80)
        {
            for (var i = 0; i < Count; i++)
            {
                values[i] = Clamp(initial);
            }
        }

        public double this[NeedType need]
        {
            get => values[(int)need];
            set => values[(int)need] = Clamp(value);
        }

        public Needs Clone()
        {
            var copy = new Needs();
            Array.Copy(values, copy.values, Count);
            return copy;
        }

        public static double Clamp(double value)
        {
            if (double.IsNaN(value))
            {
                return Max / 2;
            }

            return value < Min ? Min : value > Max ? Max : value;
        }
    }
}
