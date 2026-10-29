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

    /// <summary>How one resident looks: skin, hair, clothes and a few touches that make them easy to tell apart.</summary>
    public sealed class ResidentLook
    {
        public ResidentLook(Rgba skin, Rgba hair, HairStyle style, Rgba top, Rgba bottom, bool skirt = false, bool beard = false, bool glasses = false, Rgba? apron = null, Rgba? hat = null)
        {
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

        /// <summary>A sun hat, or null.</summary>
        public Rgba? Hat { get; }

        private static readonly Dictionary<string, ResidentLook> Starters = new Dictionary<string, ResidentLook>
        {
            // Mabel, the gardener: auburn ponytail under a straw hat.
            [StarterTown.ResidentId] = new ResidentLook(Palette.Skin[1], Palette.HairAuburn, HairStyle.Ponytail, Palette.Terracotta, Palette.Khaki, hat: Palette.Sand),

            // Arthur: bald with grey sides, glasses and a blue cardigan.
            [StarterTown.ArthurId] = new ResidentLook(Palette.Skin[0], Palette.HairGrey, HairStyle.Bald, Palette.Cornflower, Palette.Corduroy, glasses: true),

            // Nell, the repair-maker: short dark hair, green work shirt and jeans.
            [StarterTown.NellId] = new ResidentLook(Palette.Skin[2], Palette.HairDarkBrown, HairStyle.Short, Palette.Fern, Palette.Denim),

            // Rosa, the shopkeeper: black curls, rose skirt and a cream apron.
            [StarterTown.RosaId] = new ResidentLook(Palette.Skin[1], Palette.HairBlack, HairStyle.Curly, Palette.Rose, Palette.Rose.Shade(0.8), skirt: true, apron: Palette.Cream),

            // Tom, the cook: ginger hair and beard, white apron.
            [StarterTown.TomId] = new ResidentLook(Palette.Skin[0], Palette.HairGinger, HairStyle.Short, Palette.Marigold, Palette.Denim, beard: true, apron: Palette.White),

            // Priya, the café server: long black hair and a lavender dress.
            [StarterTown.PriyaId] = new ResidentLook(Palette.Skin[3], Palette.HairBlack, HairStyle.Long, Palette.Lavender, Palette.Lavender.Shade(0.85), skirt: true),

            // Ivy, the baker: blonde bob, yellow top, white apron.
            [StarterTown.IvyId] = new ResidentLook(Palette.Skin[0], Palette.HairBlonde, HairStyle.Bob, Palette.Buttercup, Palette.Khaki, skirt: true, apron: Palette.White),

            // Samuel, the doctor: short black hair, beard, glasses and a teal shirt.
            [StarterTown.SamuelId] = new ResidentLook(Palette.Skin[4], Palette.HairBlack, HairStyle.Short, Palette.Teal, Palette.Charcoal, beard: true, glasses: true),

            // Felix: messy brown hair and a pale blue hoodie.
            [StarterTown.FelixId] = new ResidentLook(Palette.Skin[2], Palette.HairBrown, HairStyle.Messy, Palette.Powder, Palette.Denim),

            // Edith: white hair in a bun, glasses, pebble cardigan and a brown skirt.
            [StarterTown.EdithId] = new ResidentLook(Palette.Skin[0], Palette.HairWhite, HairStyle.Bun, Palette.Pebble, Palette.Corduroy, skirt: true, glasses: true),
        };

        /// <summary>
        /// The starter residents each have their own look. Anyone else gets one put together from their id, so it is
        /// always the same for them, with grey or white hair from 65.
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
            var bottoms = new[] { Palette.Denim, Palette.Corduroy, Palette.Khaki, Palette.Charcoal };
            return new ResidentLook(
                Palette.Skin[Pick(2, Palette.Skin.Length)],
                hair,
                styles[Pick(3, styles.Length)],
                Palette.Clothes[Pick(4, Palette.Clothes.Length)],
                bottoms[Pick(5, bottoms.Length)],
                skirt: Pick(6, 3) == 0,
                beard: Pick(7, 5) == 0,
                glasses: age >= 55 || Pick(8, 4) == 0);
        }
    }
}
