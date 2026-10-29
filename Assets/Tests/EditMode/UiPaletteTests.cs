using System.Collections.Generic;
using NUnit.Framework;
using TinyCity.Simulation.Configuration;

namespace TinyCity.Tests.EditMode
{
    /// <summary>The interface theme: the same colours as Stills &amp; Reels and StackSpace, readable, and chosen as the player asked.</summary>
    public class UiPaletteTests
    {
        [Test]
        public void Choice_PicksThePalette_DarkByDefault()
        {
            Assert.That(UiPalette.For(ThemeChoice.Dark), Is.SameAs(UiPalette.DarkPalette));
            Assert.That(UiPalette.For(ThemeChoice.Light), Is.SameAs(UiPalette.LightPalette));
            Assert.That(UiPalette.For(new PlayerPreferences().Theme), Is.SameAs(UiPalette.DarkPalette));
        }

        [Test]
        public void Colours_MatchStillsAndReels()
        {
            // A few values copied from Stills & Reels' src/Theme.cs, so an accidental edit here is caught.
            Assert.That(UiPalette.DarkPalette.Window, Is.EqualTo(new UiColour(10, 13, 26)));
            Assert.That(UiPalette.DarkPalette.AccentA, Is.EqualTo(new UiColour(38, 104, 255)));
            Assert.That(UiPalette.DarkPalette.AccentB, Is.EqualTo(new UiColour(176, 72, 255)));
            Assert.That(UiPalette.LightPalette.Window, Is.EqualTo(new UiColour(244, 246, 251)));
            Assert.That(UiPalette.LightPalette.Text, Is.EqualTo(new UiColour(20, 26, 45)));
            Assert.That(UiPalette.LightPalette.AccentB, Is.EqualTo(new UiColour(150, 62, 240)));
            Assert.That(UiPalette.DarkPalette.Dark, Is.True);
            Assert.That(UiPalette.LightPalette.Dark, Is.False);
        }

        [Test]
        public void Text_IsReadableOnEverySurface()
        {
            foreach (var p in new[] { UiPalette.DarkPalette, UiPalette.LightPalette })
            {
                var surfaces = new Dictionary<string, UiColour>
                {
                    ["window"] = p.Window, ["bar"] = p.Bar, ["panel"] = p.Panel, ["pill"] = p.Pill,
                    ["control"] = p.Control, ["hover"] = p.Hover, ["pressed"] = p.Pressed,
                };
                foreach (var surface in surfaces)
                {
                    Assert.That(UiColour.Contrast(p.Text, surface.Value), Is.GreaterThanOrEqualTo(4.5), $"text on {surface.Key}, dark {p.Dark}");
                    Assert.That(UiColour.Contrast(p.SubText, surface.Value), Is.GreaterThanOrEqualTo(4.5), $"subtext on {surface.Key}, dark {p.Dark}");
                }

                Assert.That(UiColour.Contrast(p.SelectedText, p.AccentA), Is.GreaterThanOrEqualTo(4.5), $"selected button, dark {p.Dark}");
            }
        }
    }
}
