using System;
using System.Collections.Generic;
using System.Reflection;
using TinyCity.Simulation.Time;

namespace TinyCity.Art
{
    /// <summary>
    /// The game's locked palette (Tiny City's neon cyberpunk look): every colour the art uses is named here, and nowhere else. The fixed colours
    /// are static; the colours of the outdoors change with the season and are on an instance from
    /// <see cref="For"/>. See Documentation/ArtStyle.md. The palette is locked by the owner: change it only with
    /// their approval, then update the fingerprint in <c>ArtStyleTests</c>.
    /// </summary>
    public sealed class Palette
    {
        // Materials used everywhere.
        public static readonly Rgba Outline = Rgba.Hex("#140f2b");
        public static readonly Rgba Shadow = new Rgba(40, 30, 50, 70);
        public static readonly Rgba ShadowTint = Rgba.Hex("#1a1236");
        public static readonly Rgba Highlight = Rgba.Hex("#ffffff");
        public static readonly Rgba Earth = Rgba.Hex("#2a2048");
        public static readonly Rgba EarthDark = Rgba.Hex("#1a1433");
        public static readonly Rgba Wood = Rgba.Hex("#4a3a6e");
        public static readonly Rgba WoodDark = Rgba.Hex("#2e2350");
        public static readonly Rgba WoodLight = Rgba.Hex("#6f5a9c");
        public static readonly Rgba Stone = Rgba.Hex("#4b4775");
        public static readonly Rgba StoneDark = Rgba.Hex("#33305a");
        public static readonly Rgba Brick = Rgba.Hex("#3c2f63");
        public static readonly Rgba White = Rgba.Hex("#e6e8ff");
        public static readonly Rgba Porcelain = Rgba.Hex("#eef3f5");
        public static readonly Rgba PorcelainShade = Rgba.Hex("#dfe7ea");
        public static readonly Rgba Metal = Rgba.Hex("#7f88b8");
        public static readonly Rgba Brass = Rgba.Hex("#ffcf5a");
        public static readonly Rgba Iron = Rgba.Hex("#15122a");
        public static readonly Rgba Charcoal = Rgba.Hex("#221d3d");
        public static readonly Rgba Slate = Rgba.Hex("#2c2650");
        public static readonly Rgba SlateLight = Rgba.Hex("#463d78");
        public static readonly Rgba Ebony = Rgba.Hex("#3a2a2a");
        public static readonly Rgba Ivory = Rgba.Hex("#d8d4cc");
        public static readonly Rgba Glass = Rgba.Hex("#2a4fa8");
        public static readonly Rgba GlassLight = Rgba.Hex("#66d9ff");
        public static readonly Rgba ShowerGlass = new Rgba(170, 220, 235);
        public static readonly Rgba DisplayGlass = new Rgba(200, 230, 245, 90);
        public static readonly Rgba DisplayGlassTop = new Rgba(220, 240, 250, 70);
        public static readonly Rgba WindowGlow = Rgba.Hex("#ffc15a");
        public static readonly Rgba Snow = Rgba.Hex("#e8ecff");
        public static readonly Rgba SnowShade = Rgba.Hex("#b8c2ec");
        public static readonly Rgba Sand = Rgba.Hex("#5a4a86");
        public static readonly Rgba IceCrack = Rgba.Hex("#8fb3cc");

        // Bright colours for cushions, quilts, flags, goods and signs.
        public static readonly Rgba Red = Rgba.Hex("#ff4f7a");
        public static readonly Rgba Blue = Rgba.Hex("#3a7bff");
        public static readonly Rgba Green = Rgba.Hex("#2fe0b0");
        public static readonly Rgba Yellow = Rgba.Hex("#ffd24a");
        public static readonly Rgba Purple = Rgba.Hex("#b45cff");
        public static readonly Rgba Cream = Rgba.Hex("#f0e6ff");
        public static readonly Rgba Tomato = Rgba.Hex("#ff5a6e");
        public static readonly Rgba Poppy = Rgba.Hex("#ff3d8b");
        public static readonly Rgba Pumpkin = Rgba.Hex("#ff9a3d");
        public static readonly Rgba Mustard = Rgba.Hex("#ffd24a");
        public static readonly Rgba Apple = Rgba.Hex("#3dffb4");
        public static readonly Rgba LeafGreen = Rgba.Hex("#19c9a0");
        public static readonly Rgba SignRed = Rgba.Hex("#ff2e6e");
        public static readonly Rgba Bread = Rgba.Hex("#ffb347");
        public static readonly Rgba Icing = Rgba.Hex("#ff9fe0");
        public static readonly Rgba Sky = Rgba.Hex("#4fd8ff");
        public static readonly Rgba Mat = Rgba.Hex("#2fb8ff");
        public static readonly Rgba MatDark = Rgba.Hex("#1f8fd0");
        public static readonly Rgba Moss = Rgba.Hex("#3a5a7a");
        public static readonly Rgba Chalkboard = Rgba.Hex("#14203a");
        public static readonly Rgba ClinicBlanket = Rgba.Hex("#5fc8ff");

        /// <summary>Cushions, quilts and armchairs, one per object by its id.</summary>
        public static readonly Rgba[] Accents = { Red, Blue, Green, Yellow, Purple };

        /// <summary>Bunting on the maypole.</summary>
        public static readonly Rgba[] Bunting = { Red, Yellow, Blue, Green };

        /// <summary>Goods on shop shelves.</summary>
        public static readonly Rgba[] Goods = { Red, Yellow, Green, Cream, Blue, Bread };

        /// <summary>Goods in shop windows.</summary>
        public static readonly Rgba[] WindowGoods = { Poppy, Mustard, Apple };

        // Cottages: each picks a wall, roof and door colour by its id.
        public static readonly Rgba[] CottageWalls = { Rgba.Hex("#c2367f"), Rgba.Hex("#5a3fb5"), Rgba.Hex("#2f5fb8"), Rgba.Hex("#1f8f9a"), Rgba.Hex("#8a2fa0"), Rgba.Hex("#3a3f9a") };
        public static readonly Rgba[] CottageRoofs = { Rgba.Hex("#1c1636"), Rgba.Hex("#221a40"), Rgba.Hex("#18203a"), Rgba.Hex("#2a1838"), Rgba.Hex("#201a3a"), Rgba.Hex("#141a30") };

        public static readonly Rgba[] CottageDoors = { Rgba.Hex("#35e0ff"), Rgba.Hex("#ff4fb0"), Rgba.Hex("#ffd24a"), Rgba.Hex("#3dffb4"), Rgba.Hex("#b45cff") };

        // Shops from outside.
        public static readonly Rgba StoreWall = Rgba.Hex("#2b5f8f");
        public static readonly Rgba StoreRoof = Rgba.Hex("#1a2a4a");
        public static readonly Rgba StoreDoor = Rgba.Hex("#35e0ff");
        public static readonly Rgba StoreNeon = Rgba.Hex("#00e5c0");
        public static readonly Rgba CafeWall = Rgba.Hex("#8a2f7a");
        public static readonly Rgba CafeRoof = Rgba.Hex("#2a1640");
        public static readonly Rgba CafeDoor = Rgba.Hex("#ff5ab4");
        public static readonly Rgba CafeNeon = Rgba.Hex("#ff3d8b");
        public static readonly Rgba BakeryWall = Rgba.Hex("#9a5a2a");
        public static readonly Rgba BakeryRoof = Rgba.Hex("#2a1a30");
        public static readonly Rgba BakeryDoor = Rgba.Hex("#ffb347");
        public static readonly Rgba BakeryNeon = Rgba.Hex("#ff9a3d");
        public static readonly Rgba ClinicWall = Rgba.Hex("#d8e4ff");
        public static readonly Rgba ClinicRoof = Rgba.Hex("#2a3a6a");
        public static readonly Rgba ClinicDoor = Rgba.Hex("#35e0ff");
        public static readonly Rgba ShopNeon = Rgba.Hex("#7a5cff");

        // Rooms.
        public static readonly Rgba WallCap = Rgba.Hex("#120d24");
        public static readonly Rgba FrontWallOutside = Rgba.Hex("#3a2f63");
        public static readonly Rgba RoomWindowSky = Rgba.Hex("#2a1f5a");
        public static readonly Rgba RoomWindowSkyHigh = Rgba.Hex("#5a3a9a");
        public static readonly Rgba PictureFrame = Rgba.Hex("#35e0ff");
        public static readonly Rgba PictureLand = Rgba.Hex("#ff3d8b");
        public static readonly Rgba PictureSky = Rgba.Hex("#3a2a7a");
        public static readonly Rgba Doormat = Rgba.Hex("#ff3d8b");
        public static readonly Rgba DoormatDark = Rgba.Hex("#b02a6a");
        public static readonly Rgba StoreWallpaper = Rgba.Hex("#1f3a5f");
        public static readonly Rgba CafeWallpaper = Rgba.Hex("#4a1f4f");
        public static readonly Rgba BakeryWallpaper = Rgba.Hex("#4f2f2a");
        public static readonly Rgba ClinicWallpaper = Rgba.Hex("#c8d6f5");
        public static readonly Rgba ShopWallpaper = Rgba.Hex("#2f2a55");
        public static readonly Rgba[] CottageWallpapers = { Rgba.Hex("#2a2050"), Rgba.Hex("#3a1f4a"), Rgba.Hex("#1f2f4f"), Rgba.Hex("#2a2a5a"), Rgba.Hex("#3a2a3f") };
        public static readonly Rgba[] StoreFloor = { Rgba.Hex("#1f2a48"), Rgba.Hex("#2a3d66") };
        public static readonly Rgba[] ClinicFloor = { Rgba.Hex("#dfe8ff"), Rgba.Hex("#b8c8f0") };
        public static readonly Rgba[] BakeryFloor = { Rgba.Hex("#3a2430"), Rgba.Hex("#2e1c26") };
        public static readonly Rgba[] ShopFloor = { Rgba.Hex("#2a2648"), Rgba.Hex("#221e3d") };
        public static readonly Rgba CafeFloor = Rgba.Hex("#2e2045");
        public static readonly Rgba[] CottageFloors = { Rgba.Hex("#2e2650"), Rgba.Hex("#272043"), Rgba.Hex("#363060") };

        // Residents (Phase 6, build 2): skin, hair, clothes and the little mood bubbles.
        public static readonly Rgba[] Skin = { Rgba.Hex("#f6d9c2"), Rgba.Hex("#ebc19c"), Rgba.Hex("#d29a72"), Rgba.Hex("#b47a52"), Rgba.Hex("#7f5038") };
        public static readonly Rgba Blush = Rgba.Hex("#e8a29a");
        public static readonly Rgba HairBlack = Rgba.Hex("#2e2626");
        public static readonly Rgba HairDarkBrown = Rgba.Hex("#4a3226");
        public static readonly Rgba HairBrown = Rgba.Hex("#6b4630");
        public static readonly Rgba HairAuburn = Rgba.Hex("#a4523a");
        public static readonly Rgba HairGinger = Rgba.Hex("#d0813e");
        public static readonly Rgba HairBlonde = Rgba.Hex("#e2c27a");
        public static readonly Rgba HairGrey = Rgba.Hex("#b9b4ad");
        public static readonly Rgba HairWhite = Rgba.Hex("#ece9e3");
        public static readonly Rgba Terracotta = Rgba.Hex("#d9734e");
        public static readonly Rgba Cornflower = Rgba.Hex("#5b7db1");
        public static readonly Rgba Fern = Rgba.Hex("#6fae5c");
        public static readonly Rgba Rose = Rgba.Hex("#c2577a");
        public static readonly Rgba Marigold = Rgba.Hex("#e0a33a");
        public static readonly Rgba Lavender = Rgba.Hex("#8e6cc0");
        public static readonly Rgba Buttercup = Rgba.Hex("#d4b44a");
        public static readonly Rgba Teal = Rgba.Hex("#3e8e8a");
        public static readonly Rgba Powder = Rgba.Hex("#7fa7d9");
        public static readonly Rgba Pebble = Rgba.Hex("#b0a8a0");
        public static readonly Rgba Denim = Rgba.Hex("#4d5f86");
        public static readonly Rgba Corduroy = Rgba.Hex("#7a5a3e");
        public static readonly Rgba Khaki = Rgba.Hex("#a8946a");

        // Techwear: dark jackets, coats and trousers that the neon trim stands out against.
        public static readonly Rgba TechCharcoal = Rgba.Hex("#2c2e48");
        public static readonly Rgba TechNavy = Rgba.Hex("#283a70");
        public static readonly Rgba TechPlum = Rgba.Hex("#4e2860");
        public static readonly Rgba TechOlive = Rgba.Hex("#2f5048");
        public static readonly Rgba TechViolet = Rgba.Hex("#45348a");
        public static readonly Rgba TechWine = Rgba.Hex("#62223f");
        public static readonly Rgba TechPants = Rgba.Hex("#1f2136");
        public static readonly Rgba CargoGrey = Rgba.Hex("#41455c");
        public static readonly Rgba LabCoat = Rgba.Hex("#e2e8f6");

        // Neon trim on clothes, visors and dyed hair tips.
        public static readonly Rgba NeonCyan = Rgba.Hex("#35e0ff");
        public static readonly Rgba NeonPink = Rgba.Hex("#ff4fb0");
        public static readonly Rgba NeonLime = Rgba.Hex("#a6ff4d");
        public static readonly Rgba NeonAmber = Rgba.Hex("#ffb52e");
        public static readonly Rgba NeonViolet = Rgba.Hex("#b45cff");
        public static readonly Rgba NeonMint = Rgba.Hex("#3dffb4");

        /// <summary>Clothes for residents without a look of their own, chosen by their id.</summary>
        public static readonly Rgba[] Clothes = { TechCharcoal, TechNavy, TechPlum, TechOlive, TechViolet, TechWine };

        /// <summary>Trousers and skirts for residents without a look of their own.</summary>
        public static readonly Rgba[] Bottoms = { TechPants, CargoGrey, TechCharcoal };

        /// <summary>Neon trims for residents without a look of their own.</summary>
        public static readonly Rgba[] Neons = { NeonCyan, NeonPink, NeonLime, NeonAmber, NeonViolet, NeonMint };

        /// <summary>Hair for residents without a look of their own, chosen by their id (grey and white come with age).</summary>
        public static readonly Rgba[] Hair = { HairBlack, HairDarkBrown, HairBrown, HairAuburn, HairGinger, HairBlonde };

        public static readonly Rgba Bubble = Rgba.Hex("#fffaf0");
        public static readonly Rgba Heart = Rgba.Hex("#e0566a");
        public static readonly Rgba Sleepy = Rgba.Hex("#6a86c9");
        public static readonly Rgba Queasy = Rgba.Hex("#8cbf6a");

        // Light: sprites are multiplied by these (see Lighting).
        public static readonly Rgba Daylight = Rgba.Hex("#ffffff");
        public static readonly Rgba GoldenHour = Rgba.Hex("#ffb8d8");
        public static readonly Rgba Moonlight = Rgba.Hex("#7a6ad8");
        public static readonly Rgba Lamplight = Rgba.Hex("#e8d0ff");

        private Palette(Season season)
        {
            Season = season;
            switch (season)
            {
                case Season.Spring:
                    Grass = Rgba.Hex("#2a2456");
                    GrassDark = Rgba.Hex("#221d48");
                    GrassLight = Rgba.Hex("#352e6a");
                    Leaves = Rgba.Hex("#22c7b4");
                    LeavesDark = Rgba.Hex("#138a86");
                    LeavesLight = Rgba.Hex("#6ff5dc");
                    Accent = Rgba.Hex("#35e0ff");
                    Flowers = new[] { Rgba.Hex("#35e0ff"), Rgba.Hex("#ff4fb0"), Rgba.Hex("#ffd24a"), Rgba.Hex("#b45cff") };
                    Water = Rgba.Hex("#1d4fb0");
                    break;
                case Season.Summer:
                    Grass = Rgba.Hex("#2c2254");
                    GrassDark = Rgba.Hex("#231b46");
                    GrassLight = Rgba.Hex("#3a2c6a");
                    Leaves = Rgba.Hex("#ff4fb0");
                    LeavesDark = Rgba.Hex("#b02a7e");
                    LeavesLight = Rgba.Hex("#ff9fe0");
                    Accent = Rgba.Hex("#ff3d8b");
                    Flowers = new[] { Rgba.Hex("#ff4fb0"), Rgba.Hex("#35e0ff"), Rgba.Hex("#ffd24a"), Rgba.Hex("#3dffb4") };
                    Water = Rgba.Hex("#1f58c0");
                    break;
                case Season.Autumn:
                    Grass = Rgba.Hex("#2e2450");
                    GrassDark = Rgba.Hex("#251c42");
                    GrassLight = Rgba.Hex("#3c2f64");
                    Leaves = Rgba.Hex("#ff9a3d");
                    LeavesDark = Rgba.Hex("#c25a2a");
                    LeavesLight = Rgba.Hex("#ffd24a");
                    Accent = Rgba.Hex("#ff9a3d");
                    Flowers = new[] { Rgba.Hex("#ff9a3d"), Rgba.Hex("#ff4f7a"), Rgba.Hex("#ffd24a"), Rgba.Hex("#b45cff") };
                    Water = Rgba.Hex("#22489a");
                    break;
                default:
                    Grass = Rgba.Hex("#c9d2f0");
                    GrassDark = Rgba.Hex("#a8b2dc");
                    GrassLight = Rgba.Hex("#e8ecff");
                    Leaves = Rgba.Hex("#3a2f5a");
                    LeavesDark = Rgba.Hex("#2a2148");
                    LeavesLight = Rgba.Hex("#5a4a86");
                    Accent = Rgba.Hex("#35e0ff");
                    Flowers = new[] { Rgba.Hex("#7a8ad0"), Rgba.Hex("#5a6ab0"), Rgba.Hex("#35e0ff"), Rgba.Hex("#d0d8ff") };
                    Water = Rgba.Hex("#8fb0e8");
                    break;
            }

            Path = season == Season.Winter ? Rgba.Hex("#6a6aa8") : Rgba.Hex("#4a3a86");
            PathDark = season == Season.Winter ? Rgba.Hex("#4f4f8a") : Rgba.Hex("#33286a");
            PineLeaves = season == Season.Winter ? Rgba.Hex("#2a6a9a") : Rgba.Hex("#2a7ad0");
            PineLeavesDark = Rgba.Hex("#1a4a96");
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
