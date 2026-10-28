using TinyTown.Simulation.Time;

namespace TinyTown.Art
{
    /// <summary>The colours of the outdoors in one season. Indoor colours do not change with the season.</summary>
    public sealed class Palette
    {
        public static readonly Rgba Outline = Rgba.Hex("#3b2a2a");
        public static readonly Rgba Shadow = new Rgba(40, 30, 50, 70);
        public static readonly Rgba Earth = Rgba.Hex("#8a6142");
        public static readonly Rgba EarthDark = Rgba.Hex("#5e4030");
        public static readonly Rgba Wood = Rgba.Hex("#9c6b43");
        public static readonly Rgba WoodDark = Rgba.Hex("#6e4a32");
        public static readonly Rgba WoodLight = Rgba.Hex("#c99a68");
        public static readonly Rgba Stone = Rgba.Hex("#a8a39b");
        public static readonly Rgba StoneDark = Rgba.Hex("#7d776f");
        public static readonly Rgba White = Rgba.Hex("#f4f1ea");
        public static readonly Rgba Porcelain = Rgba.Hex("#eef3f5");
        public static readonly Rgba Metal = Rgba.Hex("#9aa4ad");
        public static readonly Rgba Glass = Rgba.Hex("#5d7f9e");
        public static readonly Rgba GlassLight = Rgba.Hex("#a9c8dc");
        public static readonly Rgba WindowGlow = Rgba.Hex("#ffd27a");
        public static readonly Rgba Snow = Rgba.Hex("#f3f6fa");
        public static readonly Rgba SnowShade = Rgba.Hex("#cdd8e6");

        private Palette(Season season)
        {
            Season = season;
            switch (season)
            {
                case Season.Spring:
                    Grass = Rgba.Hex("#86c165");
                    GrassDark = Rgba.Hex("#69a552");
                    GrassLight = Rgba.Hex("#a6d67c");
                    Leaves = Rgba.Hex("#7cc35a");
                    LeavesDark = Rgba.Hex("#4f9445");
                    LeavesLight = Rgba.Hex("#b4e07e");
                    Accent = Rgba.Hex("#f7b6cf");
                    Flowers = new[] { Rgba.Hex("#f7b6cf"), Rgba.Hex("#fff3a6"), Rgba.Hex("#ffffff"), Rgba.Hex("#c7b3f0") };
                    Water = Rgba.Hex("#5fa8d8");
                    break;
                case Season.Summer:
                    Grass = Rgba.Hex("#74b04e");
                    GrassDark = Rgba.Hex("#5a943e");
                    GrassLight = Rgba.Hex("#93c763");
                    Leaves = Rgba.Hex("#5ea844");
                    LeavesDark = Rgba.Hex("#3d7a35");
                    LeavesLight = Rgba.Hex("#8fcb5c");
                    Accent = Rgba.Hex("#e0533d");
                    Flowers = new[] { Rgba.Hex("#ffd54a"), Rgba.Hex("#ffffff"), Rgba.Hex("#e86d5a"), Rgba.Hex("#8fb3ff") };
                    Water = Rgba.Hex("#4ea0d6");
                    break;
                case Season.Autumn:
                    Grass = Rgba.Hex("#a3a356");
                    GrassDark = Rgba.Hex("#858a45");
                    GrassLight = Rgba.Hex("#bfb96a");
                    Leaves = Rgba.Hex("#e08a35");
                    LeavesDark = Rgba.Hex("#b0502c");
                    LeavesLight = Rgba.Hex("#f3c14f");
                    Accent = Rgba.Hex("#d4632e");
                    Flowers = new[] { Rgba.Hex("#e08a35"), Rgba.Hex("#c6482f"), Rgba.Hex("#f3c14f"), Rgba.Hex("#9a5a2e") };
                    Water = Rgba.Hex("#5a93b8");
                    break;
                default:
                    Grass = Rgba.Hex("#e9eff5");
                    GrassDark = Rgba.Hex("#ccd8e6");
                    GrassLight = Rgba.Hex("#fbfdff");
                    Leaves = Rgba.Hex("#7a6152");
                    LeavesDark = Rgba.Hex("#5a463c");
                    LeavesLight = Rgba.Hex("#a28775");
                    Accent = Rgba.Hex("#c94040");
                    Flowers = new[] { Rgba.Hex("#a7b98e"), Rgba.Hex("#8fa07a"), Rgba.Hex("#c94040"), Rgba.Hex("#d7e2ec") };
                    Water = Rgba.Hex("#b9d7ea");
                    break;
            }

            Path = season == Season.Winter ? Rgba.Hex("#c9bfb1") : Rgba.Hex("#dcc596");
            PathDark = season == Season.Winter ? Rgba.Hex("#a99d8f") : Rgba.Hex("#b99f72");
            PineLeaves = season == Season.Winter ? Rgba.Hex("#3f6b52") : Rgba.Hex("#3e7a4a");
            PineLeavesDark = Rgba.Hex("#2c5639");
        }

        public Season Season { get; }

        public bool IsWinter => Season == Season.Winter;

        public Rgba Grass { get; }

        public Rgba GrassDark { get; }

        public Rgba GrassLight { get; }

        /// <summary>Broadleaf tree colours; in winter these are the bare branches.</summary>
        public Rgba Leaves { get; }

        public Rgba LeavesDark { get; }

        public Rgba LeavesLight { get; }

        public Rgba PineLeaves { get; }

        public Rgba PineLeavesDark { get; }

        /// <summary>The season's highlight: blossom, poppies, rust or holly berries.</summary>
        public Rgba Accent { get; }

        public Rgba[] Flowers { get; }

        public Rgba Water { get; }

        public Rgba Path { get; }

        public Rgba PathDark { get; }

        public static Palette For(Season season) => new Palette(season);
    }
}
