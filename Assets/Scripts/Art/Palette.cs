using System;
using System.Collections.Generic;
using System.Reflection;
using TinyTown.Simulation.Time;

namespace TinyTown.Art
{
    /// <summary>
    /// The game's locked palette: every colour the art uses is named here, and nowhere else. The fixed colours
    /// are static; the colours of the outdoors change with the season and are on an instance from
    /// <see cref="For"/>. See Documentation/ArtStyle.md. The palette is locked by the owner: change it only with
    /// their approval, then update the fingerprint in <c>ArtStyleTests</c>.
    /// </summary>
    public sealed class Palette
    {
        // Materials used everywhere.
        public static readonly Rgba Outline = Rgba.Hex("#3b2a2a");
        public static readonly Rgba Shadow = new Rgba(40, 30, 50, 70);
        public static readonly Rgba ShadowTint = Rgba.Hex("#33304a");
        public static readonly Rgba Highlight = Rgba.Hex("#ffffff");
        public static readonly Rgba Earth = Rgba.Hex("#8a6142");
        public static readonly Rgba EarthDark = Rgba.Hex("#5e4030");
        public static readonly Rgba Wood = Rgba.Hex("#9c6b43");
        public static readonly Rgba WoodDark = Rgba.Hex("#6e4a32");
        public static readonly Rgba WoodLight = Rgba.Hex("#c99a68");
        public static readonly Rgba Stone = Rgba.Hex("#a8a39b");
        public static readonly Rgba StoneDark = Rgba.Hex("#7d776f");
        public static readonly Rgba Brick = Rgba.Hex("#9a6a55");
        public static readonly Rgba White = Rgba.Hex("#f4f1ea");
        public static readonly Rgba Porcelain = Rgba.Hex("#eef3f5");
        public static readonly Rgba PorcelainShade = Rgba.Hex("#dfe7ea");
        public static readonly Rgba Metal = Rgba.Hex("#9aa4ad");
        public static readonly Rgba Brass = Rgba.Hex("#e8c35a");
        public static readonly Rgba Iron = Rgba.Hex("#2d2d33");
        public static readonly Rgba Charcoal = Rgba.Hex("#3a3a40");
        public static readonly Rgba Slate = Rgba.Hex("#4a4a52");
        public static readonly Rgba SlateLight = Rgba.Hex("#6b6b75");
        public static readonly Rgba Ebony = Rgba.Hex("#3a2a2a");
        public static readonly Rgba Ivory = Rgba.Hex("#d8d4cc");
        public static readonly Rgba Glass = Rgba.Hex("#5d7f9e");
        public static readonly Rgba GlassLight = Rgba.Hex("#a9c8dc");
        public static readonly Rgba ShowerGlass = new Rgba(170, 220, 235);
        public static readonly Rgba DisplayGlass = new Rgba(200, 230, 245, 90);
        public static readonly Rgba DisplayGlassTop = new Rgba(220, 240, 250, 70);
        public static readonly Rgba WindowGlow = Rgba.Hex("#ffd27a");
        public static readonly Rgba Snow = Rgba.Hex("#f3f6fa");
        public static readonly Rgba SnowShade = Rgba.Hex("#cdd8e6");
        public static readonly Rgba Sand = Rgba.Hex("#d8c79a");
        public static readonly Rgba IceCrack = Rgba.Hex("#8fb3cc");

        // Bright colours for cushions, quilts, flags, goods and signs.
        public static readonly Rgba Red = Rgba.Hex("#d9675a");
        public static readonly Rgba Blue = Rgba.Hex("#5a8fd9");
        public static readonly Rgba Green = Rgba.Hex("#6fb36a");
        public static readonly Rgba Yellow = Rgba.Hex("#e0b04a");
        public static readonly Rgba Purple = Rgba.Hex("#a274c9");
        public static readonly Rgba Cream = Rgba.Hex("#f2e8d0");
        public static readonly Rgba Tomato = Rgba.Hex("#c94f3c");
        public static readonly Rgba Poppy = Rgba.Hex("#e0533d");
        public static readonly Rgba Pumpkin = Rgba.Hex("#e08a35");
        public static readonly Rgba Mustard = Rgba.Hex("#f0c05a");
        public static readonly Rgba Apple = Rgba.Hex("#8fbf5a");
        public static readonly Rgba LeafGreen = Rgba.Hex("#4f8a4e");
        public static readonly Rgba SignRed = Rgba.Hex("#d23c3c");
        public static readonly Rgba Bread = Rgba.Hex("#c98a3e");
        public static readonly Rgba Icing = Rgba.Hex("#f2b6c4");
        public static readonly Rgba Sky = Rgba.Hex("#9cc8e8");
        public static readonly Rgba Mat = Rgba.Hex("#5aa0c9");
        public static readonly Rgba MatDark = Rgba.Hex("#4c8db3");
        public static readonly Rgba Moss = Rgba.Hex("#7a8a6a");
        public static readonly Rgba Chalkboard = Rgba.Hex("#2f4a3a");
        public static readonly Rgba ClinicBlanket = Rgba.Hex("#a9cbe0");

        /// <summary>Cushions, quilts and armchairs, one per object by its id.</summary>
        public static readonly Rgba[] Accents = { Red, Blue, Green, Yellow, Purple };

        /// <summary>Bunting on the maypole.</summary>
        public static readonly Rgba[] Bunting = { Red, Yellow, Blue, Green };

        /// <summary>Goods on shop shelves.</summary>
        public static readonly Rgba[] Goods = { Red, Yellow, Green, Cream, Blue, Bread };

        /// <summary>Goods in shop windows.</summary>
        public static readonly Rgba[] WindowGoods = { Poppy, Mustard, Apple };

        // Cottages: each picks a wall, roof and door colour by its id.
        public static readonly Rgba[] CottageWalls = { Rgba.Hex("#f1e3c6"), Rgba.Hex("#f0cfc4"), Rgba.Hex("#f3e1a0"), Rgba.Hex("#d3dfc3"), Rgba.Hex("#d3e2ec"), Rgba.Hex("#ead7e6") };
        public static readonly Rgba[] CottageRoofs = { Rgba.Hex("#c8623f"), Rgba.Hex("#5f6f8a"), Rgba.Hex("#6f8f55"), Rgba.Hex("#8a5a6e"), Rgba.Hex("#c9a35a"), Rgba.Hex("#a8553a") };

        /// <summary>The thatched roof among <see cref="CottageRoofs"/>.</summary>
        public static readonly Rgba Thatch = CottageRoofs[4];

        public static readonly Rgba[] CottageDoors = { Rgba.Hex("#b8433a"), Rgba.Hex("#3f6a9c"), Rgba.Hex("#4f8a4e"), Rgba.Hex("#e0a93a"), Rgba.Hex("#7a4f8c") };

        // Shops from outside.
        public static readonly Rgba StoreWall = Rgba.Hex("#e9dcc0");
        public static readonly Rgba StoreRoof = Rgba.Hex("#4f7a5a");
        public static readonly Rgba StoreDoor = Rgba.Hex("#3d6b4a");
        public static readonly Rgba StoreAwning = Rgba.Hex("#4e9a5c");
        public static readonly Rgba CafeWall = Rgba.Hex("#f2d9b3");
        public static readonly Rgba CafeRoof = Rgba.Hex("#9c4a3a");
        public static readonly Rgba CafeDoor = Rgba.Hex("#8a3b30");
        public static readonly Rgba CafeAwning = Rgba.Hex("#d24b3e");
        public static readonly Rgba BakeryWall = Rgba.Hex("#f6e3a8");
        public static readonly Rgba BakeryRoof = Rgba.Hex("#b5733f");
        public static readonly Rgba BakeryDoor = Rgba.Hex("#7a5232");
        public static readonly Rgba BakeryAwning = Rgba.Hex("#e7a83a");
        public static readonly Rgba BakeryAwningStripe = Rgba.Hex("#fff4d6");
        public static readonly Rgba ClinicWall = Rgba.Hex("#f5f6f2");
        public static readonly Rgba ClinicRoof = Rgba.Hex("#5d7a99");
        public static readonly Rgba ClinicDoor = Rgba.Hex("#3f6a9c");
        public static readonly Rgba ShopAwning = Rgba.Hex("#5a7fb0");

        // Rooms.
        public static readonly Rgba WallCap = Rgba.Hex("#5e4a44");
        public static readonly Rgba FrontWallOutside = Rgba.Hex("#efe2c8");
        public static readonly Rgba RoomWindowSky = Rgba.Hex("#9ccbe6");
        public static readonly Rgba RoomWindowSkyHigh = Rgba.Hex("#bfe0f2");
        public static readonly Rgba PictureFrame = Rgba.Hex("#c99a3e");
        public static readonly Rgba PictureLand = Rgba.Hex("#7fb069");
        public static readonly Rgba PictureSky = Rgba.Hex("#a7d0ea");
        public static readonly Rgba Doormat = Rgba.Hex("#a0583a");
        public static readonly Rgba DoormatDark = Rgba.Hex("#8a4a30");
        public static readonly Rgba StoreWallpaper = Rgba.Hex("#cfe0c4");
        public static readonly Rgba CafeWallpaper = Rgba.Hex("#f0d7b8");
        public static readonly Rgba BakeryWallpaper = Rgba.Hex("#f6e7b8");
        public static readonly Rgba ClinicWallpaper = Rgba.Hex("#e3eef3");
        public static readonly Rgba ShopWallpaper = Rgba.Hex("#e6e0d4");
        public static readonly Rgba[] CottageWallpapers = { Rgba.Hex("#f2e2c4"), Rgba.Hex("#e9d3d8"), Rgba.Hex("#d7e4d2"), Rgba.Hex("#d9e2ee"), Rgba.Hex("#f0e6b8") };
        public static readonly Rgba[] StoreFloor = { Rgba.Hex("#efe6cf"), Rgba.Hex("#9cc29a") };
        public static readonly Rgba[] ClinicFloor = { Rgba.Hex("#f4f7f8"), Rgba.Hex("#cfe1ea") };
        public static readonly Rgba[] BakeryFloor = { Rgba.Hex("#d98b5f"), Rgba.Hex("#c57650") };
        public static readonly Rgba[] ShopFloor = { Rgba.Hex("#e8e2d6"), Rgba.Hex("#c9c1b3") };
        public static readonly Rgba CafeFloor = Rgba.Hex("#a2603f");
        public static readonly Rgba[] CottageFloors = { Rgba.Hex("#c08a5a"), Rgba.Hex("#b07a4c"), Rgba.Hex("#caa074") };

        // Light: sprites are multiplied by these (see Lighting).
        public static readonly Rgba Daylight = Rgba.Hex("#ffffff");
        public static readonly Rgba GoldenHour = Rgba.Hex("#ffd6b0");
        public static readonly Rgba Moonlight = Rgba.Hex("#6a77b8");
        public static readonly Rgba Lamplight = Rgba.Hex("#ffe2b8");

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

        /// <summary>
        /// Every named colour, fixed ones first (in the order written above) and then each season's, as
        /// (name, colour). Lists are numbered, such as <c>CottageRoofs[4]</c>. Used for the style guide and the lock test.
        /// </summary>
        public static IEnumerable<(string name, Rgba colour)> Entries()
        {
            foreach (var field in typeof(Palette).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.GetValue(null) is Rgba c)
                {
                    yield return (field.Name, c);
                }
                else if (field.GetValue(null) is Rgba[] list)
                {
                    for (var i = 0; i < list.Length; i++)
                    {
                        yield return ($"{field.Name}[{i}]", list[i]);
                    }
                }
            }

            foreach (Season season in Enum.GetValues(typeof(Season)))
            {
                var palette = For(season);
                foreach (var property in typeof(Palette).GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (property.GetValue(palette) is Rgba c)
                    {
                        yield return ($"{season}.{property.Name}", c);
                    }
                    else if (property.GetValue(palette) is Rgba[] list)
                    {
                        for (var i = 0; i < list.Length; i++)
                        {
                            yield return ($"{season}.{property.Name}[{i}]", list[i]);
                        }
                    }
                }
            }
        }
    }
}
