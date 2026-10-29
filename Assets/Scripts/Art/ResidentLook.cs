using System.Collections.Generic;
using TinyCity.Simulation.Content;

namespace TinyCity.Art
{
    public enum HairStyle
    {
        Short,
        Messy,
        Curly,
        Bob,
        Long,
        Ponytail,
        Bun,
        Bald,
    }

    /// <summary>How one resident looks: skin, hair, techwear with neon piping, and a few touches that make them easy to tell apart.</summary>
    public sealed class ResidentLook
    {
        public ResidentLook(Rgba skin, Rgba hair, HairStyle style, Rgba top, Rgba bottom, bool skirt = false, bool beard = false, bool glasses = false, Rgba? apron = null, Rgba? hat = null,
            Rgba? trim = null, Rgba? visor = null, Rgba? hairTips = null, bool coat = false, int shrink = 0, bool baby = false)
        {
            Shrink = shrink;
            IsBaby = baby;
            Trim = trim;
            Visor = visor;
            HairTips = hairTips;
            Coat = coat;
            Skin = skin;
            Hair = hair;
            Style = style;
            Top = top;
            Bottom = bottom;
            Skirt = skirt;
            Beard = beard;
            Glasses = glasses;
            Apron = apron;
            Hat = hat;
        }

        public Rgba Skin { get; }

        public Rgba Hair { get; }

        public HairStyle Style { get; }

        public Rgba Top { get; }

        /// <summary>Trousers, or the skirt when <see cref="Skirt"/> is set.</summary>
        public Rgba Bottom { get; }

        public bool Skirt { get; }

        public bool Beard { get; }

        public bool Glasses { get; }

        /// <summary>A work apron over the clothes, or null.</summary>
        public Rgba? Apron { get; }

        /// <summary>A cap, or null. Its band is the neon trim if there is one.</summary>
        public Rgba? Hat { get; }

        /// <summary>The neon piping on the clothes: collar, front seam, cuffs, belt or hem, and shoe toes. Null for none.</summary>
        public Rgba? Trim { get; }

        /// <summary>A glowing visor across the eyes (instead of glasses), or null.</summary>
        public Rgba? Visor { get; }

        /// <summary>Hair dyed a neon colour at the tips, or null.</summary>
        public Rgba? HairTips { get; }

        /// <summary>A long coat, open at the front, that hangs to the knees.</summary>
        public bool Coat { get; }

        /// <summary>How many rows shorter than an adult the figure is: children and teenagers are smaller, with the same size head.</summary>
        public int Shrink { get; }

        /// <summary>A baby, drawn as a bundle in their carer's arms instead of a figure.</summary>
        public bool IsBaby { get; }

        /// <summary>Rows shorter than an adult: a child of 3 to 12, and a teenager.</summary>
        public const int ChildShrink = 9;
        public const int TeenShrink = 4;

        private static readonly Dictionary<string, ResidentLook> Starters = new Dictionary<string, ResidentLook>
        {
            // Mabel, the gardener: auburn ponytail with mint tips, a cap and an olive utility jacket piped in mint.
            [StarterTown.ResidentId] = new ResidentLook(Palette.Skin[1], Palette.HairAuburn, HairStyle.Ponytail, Palette.TechOlive, Palette.CargoGrey, hat: Palette.TechCharcoal, trim: Palette.NeonMint, hairTips: Palette.NeonMint),

            // Arthur: bald with grey sides, glasses and a long navy coat with amber piping.
            [StarterTown.ArthurId] = new ResidentLook(Palette.Skin[0], Palette.HairGrey, HairStyle.Bald, Palette.TechNavy, Palette.TechPants, glasses: true, trim: Palette.NeonAmber, coat: true),

            // Nell, the repair-maker: short dark hair, a cyan visor and a charcoal work jacket with lime piping.
            [StarterTown.NellId] = new ResidentLook(Palette.Skin[2], Palette.HairDarkBrown, HairStyle.Short, Palette.TechCharcoal, Palette.CargoGrey, trim: Palette.NeonLime, visor: Palette.NeonCyan),

            // Rosa, the shopkeeper: black curls with pink tips, a violet jacket, plum skirt and a charcoal apron.
            [StarterTown.RosaId] = new ResidentLook(Palette.Skin[1], Palette.HairBlack, HairStyle.Curly, Palette.TechViolet, Palette.TechPlum, skirt: true, apron: Palette.TechCharcoal, trim: Palette.NeonPink, hairTips: Palette.NeonPink),

            // Tom, the cook: ginger hair and beard, a white chef's jacket with cyan piping and a charcoal apron.
            [StarterTown.TomId] = new ResidentLook(Palette.Skin[0], Palette.HairGinger, HairStyle.Short, Palette.White, Palette.TechPants, beard: true, apron: Palette.TechCharcoal, trim: Palette.NeonCyan),

            // Priya, the café server: long black hair with violet tips and a charcoal dress piped in café pink.
            [StarterTown.PriyaId] = new ResidentLook(Palette.Skin[3], Palette.HairBlack, HairStyle.Long, Palette.TechCharcoal, Palette.TechCharcoal, skirt: true, trim: Palette.CafeNeon, hairTips: Palette.NeonViolet),

            // Ivy, the baker: blonde bob with pink tips, a wine top and skirt, and a white apron piped in amber.
            [StarterTown.IvyId] = new ResidentLook(Palette.Skin[0], Palette.HairBlonde, HairStyle.Bob, Palette.TechWine, Palette.TechPants, skirt: true, apron: Palette.White, trim: Palette.NeonAmber, hairTips: Palette.NeonPink),

            // Samuel, the doctor: short black hair, beard, glasses and a white lab coat with cyan piping.
            [StarterTown.SamuelId] = new ResidentLook(Palette.Skin[4], Palette.HairBlack, HairStyle.Short, Palette.LabCoat, Palette.TechPants, beard: true, glasses: true, trim: Palette.NeonCyan, coat: true),

            // Felix: messy brown hair with cyan tips, and a violet visor to match the piping on his charcoal hoodie.
            [StarterTown.FelixId] = new ResidentLook(Palette.Skin[2], Palette.HairBrown, HairStyle.Messy, Palette.TechCharcoal, Palette.CargoGrey, trim: Palette.NeonViolet, visor: Palette.NeonViolet, hairTips: Palette.NeonCyan),

            // Edith: white hair in a bun, glasses and a long plum coat with amber piping over a dark skirt.
            [StarterTown.EdithId] = new ResidentLook(Palette.Skin[0], Palette.HairWhite, HairStyle.Bun, Palette.TechPlum, Palette.TechPants, skirt: true, glasses: true, trim: Palette.NeonAmber, coat: true),
        };

        /// <summary>
        /// The starter residents each have their own look. Anyone else gets one put together from their id, so it is
        /// always the same for them, with grey or white hair from 65. Children are smaller (and have no beard), and a
        /// baby under 3 is a bundle.
        /// </summary>
        public static ResidentLook For(string residentId, int age)
        {
            if (residentId != null && Starters.TryGetValue(residentId, out var look))
            {
                return look;
            }

            var h = Noise.Of(residentId ?? string.Empty);
            int Pick(int salt, int count) => (int)(Noise.Hash(salt, 7, h) % (uint)count);
            var hair = age >= 75 ? Palette.HairWhite : age >= 65 ? Palette.HairGrey : Palette.Hair[Pick(1, Palette.Hair.Length)];
            var styles = new[] { HairStyle.Short, HairStyle.Messy, HairStyle.Curly, HairStyle.Bob, HairStyle.Long, HairStyle.Ponytail, HairStyle.Bun };
            var visor = age >= 13 && age < 55 && Pick(9, 5) == 0;
            return new ResidentLook(
                Palette.Skin[Pick(2, Palette.Skin.Length)],
                hair,
                styles[Pick(3, styles.Length)],
                Palette.Clothes[Pick(4, Palette.Clothes.Length)],
                Palette.Bottoms[Pick(5, Palette.Bottoms.Length)],
                skirt: Pick(6, 3) == 0,
                beard: age >= 18 && Pick(7, 5) == 0,
                glasses: !visor && (age >= 55 || Pick(8, 4) == 0),
                trim: Palette.Neons[Pick(10, Palette.Neons.Length)],
                visor: visor ? Palette.Neons[Pick(11, Palette.Neons.Length)] : (Rgba?)null,
                hairTips: age < 40 && Pick(12, 3) == 0 ? Palette.Neons[Pick(13, Palette.Neons.Length)] : (Rgba?)null,
                coat: age >= 50 && Pick(14, 2) == 0,
                shrink: age < 13 ? ChildShrink : age < 18 ? TeenShrink : 0,
                baby: age < 3);
        }
    }
}
