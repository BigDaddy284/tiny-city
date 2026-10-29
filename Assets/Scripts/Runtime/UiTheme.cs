using System;
using TinyTown.Simulation.Configuration;
using UnityEngine;

namespace TinyTown.Runtime
{
    /// <summary>
    /// The interface theme: which <see cref="UiPalette"/> is in use, and the IMGUI skin and <see cref="GuiStyles"/>
    /// built from it. Skins and styles are made once per palette, the first time they are drawn, and kept.
    /// </summary>
    public static class UiTheme
    {
        private static Look dark;
        private static Look light;

        /// <summary>Raised when the palette in use changes.</summary>
        public static event Action Changed;

        public static ThemeChoice Choice { get; private set; } = ThemeChoice.Dark;

        public static UiPalette Palette => UiPalette.For(Choice);

        /// <summary>The skin for the palette in use. Call inside OnGUI only.</summary>
        public static GUISkin Skin => Current.Skin;

        /// <summary>The game's text styles for the palette in use. Call inside OnGUI only.</summary>
        public static GuiStyles Styles => Current.Styles;

        private static Look Current => Choice == ThemeChoice.Light ? light ??= new Look(Palette) : dark ??= new Look(Palette);

        public static void Choose(ThemeChoice choice)
        {
            if (choice == Choice)
            {
                return;
            }

            Choice = choice;
            Changed?.Invoke();
        }

        public static Color ToColor(UiColour c, float alpha = 1f) => new Color(c.R / 255f, c.G / 255f, c.B / 255f, alpha);

        private sealed class Look
        {
            public Look(UiPalette palette)
            {
                Skin = UiSkin.Build(palette);
                var previous = GUI.skin;
                GUI.skin = Skin;
                Styles = new GuiStyles(palette);
                GUI.skin = previous;
            }

            public GUISkin Skin { get; }

            public GuiStyles Styles { get; }
        }
    }
}
