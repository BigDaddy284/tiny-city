using System;
using TinyTown.Simulation.Time;

namespace TinyTown.Art
{
    /// <summary>
    /// The colour of the light at a time of day. Sprites are multiplied by <see cref="Tint"/>, and lit windows are
    /// shown with <see cref="WindowGlow"/> opacity. Days are longer in summer and shorter in winter.
    /// </summary>
    public readonly struct Daylight : IEquatable<Daylight>
    {
        public Daylight(Rgba tint, double windowGlow)
        {
            Tint = tint;
            WindowGlow = windowGlow;
        }

        public Rgba Tint { get; }

        /// <summary>0 by day, 1 at night.</summary>
        public double WindowGlow { get; }

        public bool Equals(Daylight other) => Tint.Equals(other.Tint) && Math.Abs(WindowGlow - other.WindowGlow) < 0.004;

        public override bool Equals(object obj) => obj is Daylight other && Equals(other);

        public override int GetHashCode() => Tint.GetHashCode();
    }

    public static class Lighting
    {
        public static readonly Rgba Day = Rgba.Hex("#ffffff");
        public static readonly Rgba Golden = Rgba.Hex("#ffd6b0");
        public static readonly Rgba Night = Rgba.Hex("#6a77b8");
        public static readonly Rgba Lamplight = Rgba.Hex("#ffe2b8");

        /// <summary>How long dawn and dusk take, in minutes.</summary>
        public const int Twilight = 90;

        /// <summary>Sunrise and sunset in each season, as minutes after midnight.</summary>
        public static (int sunrise, int sunset) SunTimes(Season season)
        {
            switch (season)
            {
                case Season.Spring: return (6 * 60, 20 * 60);
                case Season.Summer: return (5 * 60, 21 * 60 + 30);
                case Season.Autumn: return (6 * 60 + 30, 19 * 60);
                default: return (7 * 60 + 30, 17 * 60 + 30);
            }
        }

        public static Daylight At(GameTime time, bool outdoors) => At(time.MinuteOfDay, Seasons.Of(time), outdoors);

        public static Daylight At(int minuteOfDay, Season season, bool outdoors)
        {
            var (sunrise, sunset) = SunTimes(season);
            var m = ((minuteOfDay % 1440) + 1440) % 1440;

            // 0 at night, 1 in full daylight, passing through golden light at dawn and dusk.
            double daylight;
            if (m < sunrise - (Twilight / 2) || m > sunset + (Twilight / 2))
            {
                daylight = 0;
            }
            else if (m < sunrise + (Twilight / 2))
            {
                daylight = (m - (sunrise - (Twilight / 2))) / (double)Twilight;
            }
            else if (m > sunset - (Twilight / 2))
            {
                daylight = (sunset + (Twilight / 2) - m) / (double)Twilight;
            }
            else
            {
                daylight = 1;
            }

            if (!outdoors)
            {
                // Indoors the lamps are lit after dark.
                return new Daylight(Lamplight.Mix(Day, daylight), 0);
            }

            var tint = daylight < 0.5 ? Night.Mix(Golden, daylight * 2) : Golden.Mix(Day, (daylight - 0.5) * 2);
            return new Daylight(tint, Math.Max(0, Math.Min(1, (0.75 - daylight) / 0.5)));
        }
    }
}
