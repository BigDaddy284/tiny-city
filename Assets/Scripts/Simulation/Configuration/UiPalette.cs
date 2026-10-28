using System;

namespace TinyTown.Simulation.Configuration
{
    /// <summary>An opaque colour for the interface, 0–255 per channel.</summary>
    public readonly struct UiColour : IEquatable<UiColour>
    {
        public UiColour(byte r, byte g, byte b)
        {
            R = r;
            G = g;
            B = b;
        }

        public byte R { get; }

        public byte G { get; }

        public byte B { get; }

        /// <summary>Relative luminance as WCAG defines it, 0 (black) to 1 (white).</summary>
        public double Luminance => (0.2126 * Linear(R)) + (0.7152 * Linear(G)) + (0.0722 * Linear(B));

        /// <summary>The WCAG contrast ratio between two colours, from 1 to 21.</summary>
        public static double Contrast(UiColour a, UiColour b)
        {
            var light = Math.Max(a.Luminance, b.Luminance);
            var dark = Math.Min(a.Luminance, b.Luminance);
            return (light + 0.05) / (dark + 0.05);
        }

        public bool Equals(UiColour other) => R == other.R && G == other.G && B == other.B;

        public override bool Equals(object obj) => obj is UiColour other && Equals(other);

        public override int GetHashCode() => (R << 16) | (G << 8) | B;

        public override string ToString() => $"({R}, {G}, {B})";

        private static double Linear(byte channel)
        {
            var c = channel / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
    }

    /// <summary>
    /// The colours of the side panel, windows and buttons. They are the owner's Stills &amp; Reels palette
    /// (github.com/BigDaddy284/Stills-and-Reels, <c>src/Theme.cs</c>), value for value, with the error and success
    /// colours StackSpace added, so all three apps look the same. Field names match those files so they can be kept
    /// in step. The game world's art has its own locked palette (<c>Art/Palette.cs</c>) and is not themed.
    /// </summary>
    public sealed class UiPalette
    {
        public static readonly UiPalette DarkPalette = new UiPalette
        {
            Dark = true,
            Window = C(10, 13, 26),
            Bar = C(13, 17, 33),
            BarBorder = C(30, 37, 62),
            Canvas = C(9, 12, 24),
            Panel = C(14, 19, 37),
            Text = C(234, 238, 250),
            SubText = C(172, 182, 210),
            Muted = C(104, 114, 146),
            Hover = C(26, 33, 62),
            Pressed = C(34, 43, 80),
            Pill = C(12, 18, 44),
            PillSep = C(62, 74, 128),
            AccentA = C(38, 104, 255),
            AccentB = C(176, 72, 255),
            SelectedText = C(255, 255, 255),
            Control = C(32, 40, 70),
            ControlBorder = C(46, 56, 92),
            Warning = C(255, 190, 110),
            Glow = C(40, 70, 150),
            Error = C(255, 120, 140),
            Success = C(80, 200, 140),
        };

        public static readonly UiPalette LightPalette = new UiPalette
        {
            Dark = false,
            Window = C(244, 246, 251),
            Bar = C(255, 255, 255),
            BarBorder = C(221, 226, 238),
            Canvas = C(236, 240, 247),
            Panel = C(250, 251, 254),
            Text = C(20, 26, 45),
            SubText = C(74, 85, 112),
            Muted = C(138, 147, 168),
            Hover = C(232, 237, 249),
            Pressed = C(218, 226, 246),
            Pill = C(243, 245, 255),
            PillSep = C(196, 204, 232),
            AccentA = C(37, 99, 245),
            AccentB = C(150, 62, 240),
            SelectedText = C(255, 255, 255),
            Control = C(222, 228, 242),
            ControlBorder = C(200, 208, 228),
            Warning = C(196, 98, 0),
            Glow = C(160, 185, 255),
            Error = C(190, 30, 60),
            Success = C(20, 120, 70),
        };

        private UiPalette()
        {
        }

        public bool Dark { get; private set; }

        /// <summary>Window backgrounds.</summary>
        public UiColour Window { get; private set; }

        /// <summary>Camera panel header bars.</summary>
        public UiColour Bar { get; private set; }

        /// <summary>Hairlines between bars and content, and inactive panel frames.</summary>
        public UiColour BarBorder { get; private set; }

        /// <summary>Behind the town, where no map is drawn.</summary>
        public UiColour Canvas { get; private set; }

        /// <summary>The side panel, pop-ups and speech bubbles.</summary>
        public UiColour Panel { get; private set; }

        public UiColour Text { get; private set; }

        public UiColour SubText { get; private set; }

        /// <summary>Disabled text only: it is too faint for reading.</summary>
        public UiColour Muted { get; private set; }

        public UiColour Hover { get; private set; }

        public UiColour Pressed { get; private set; }

        /// <summary>Boxes holding what the AI wrote.</summary>
        public UiColour Pill { get; private set; }

        public UiColour PillSep { get; private set; }

        /// <summary>Gradient start (blue): selected buttons, the active panel's frame, slider handles.</summary>
        public UiColour AccentA { get; private set; }

        /// <summary>Gradient end (purple).</summary>
        public UiColour AccentB { get; private set; }

        public UiColour SelectedText { get; private set; }

        /// <summary>Button fills, slider tracks, need-bar tracks.</summary>
        public UiColour Control { get; private set; }

        public UiColour ControlBorder { get; private set; }

        public UiColour Warning { get; private set; }

        public UiColour Glow { get; private set; }

        public UiColour Error { get; private set; }

        public UiColour Success { get; private set; }

        public static UiPalette For(ThemeChoice choice) => choice == ThemeChoice.Light ? LightPalette : DarkPalette;

        private static UiColour C(byte r, byte g, byte b) => new UiColour(r, g, b);
    }
}
