using TinyTown.Simulation.Configuration;
using UnityEngine;

namespace TinyTown.Runtime
{
    /// <summary>
    /// Builds an IMGUI skin from a <see cref="UiPalette"/>, in the Stills &amp; Reels look: rounded controls, a
    /// blue-to-purple gradient on selected buttons, slim slider tracks with round accent handles. Must be called
    /// inside OnGUI, because it starts from Unity's built-in skin.
    /// </summary>
    internal static class UiSkin
    {
        private const int Radius = 5;

        public static GUISkin Build(UiPalette p)
        {
            var skin = Object.Instantiate(GUI.skin);
            skin.name = p.Dark ? "TinyTown Dark" : "TinyTown Light";
            skin.hideFlags = HideFlags.HideAndDontSave;
            var text = UiTheme.ToColor(p.Text);
            var selected = UiTheme.ToColor(p.SelectedText);
            var muted = UiTheme.ToColor(p.Muted);

            skin.label.normal.textColor = text;

            var control = Rounded(p.Control, p.ControlBorder);
            var hover = Rounded(p.Hover, p.ControlBorder);
            var pressed = Rounded(p.Pressed, p.ControlBorder);
            var gradient = Gradient(p.AccentA, p.AccentB);
            Style(skin.button, control, hover, pressed, gradient, text, selected);
            skin.button.fontSize = 13;

            skin.toggle.normal.textColor = skin.toggle.hover.textColor = skin.toggle.active.textColor = text;
            skin.toggle.onNormal.textColor = skin.toggle.onHover.textColor = skin.toggle.onActive.textColor = text;

            var field = Rounded(p.Pill, p.ControlBorder);
            var focused = Rounded(p.Pill, p.AccentA);
            foreach (var state in new[] { skin.textField.normal, skin.textField.hover, skin.textField.active, skin.textField.onNormal })
            {
                state.background = field;
                state.textColor = text;
            }

            skin.textField.focused.background = skin.textField.onFocused.background = focused;
            skin.textField.focused.textColor = skin.textField.onFocused.textColor = text;
            skin.textField.border = Border();
            skin.settings.cursorColor = text;
            skin.settings.selectionColor = UiTheme.ToColor(p.AccentA, 0.45f);

            skin.box.normal.background = Rounded(p.Panel, p.BarBorder);
            skin.box.normal.textColor = text;
            skin.box.border = Border();

            skin.horizontalSlider.normal.background = Rounded(p.Control, p.ControlBorder);
            skin.horizontalSlider.border = Border();
            skin.horizontalSlider.fixedHeight = 8f;
            skin.horizontalSlider.margin = new RectOffset(4, 4, 8, 8);
            var thumb = Round(p.AccentA);
            var thumbLit = Round(p.AccentB);
            skin.horizontalSliderThumb.normal.background = thumb;
            skin.horizontalSliderThumb.hover.background = skin.horizontalSliderThumb.active.background = thumbLit;
            skin.horizontalSliderThumb.focused.background = thumb;
            skin.horizontalSliderThumb.fixedWidth = 16f;
            skin.horizontalSliderThumb.fixedHeight = 16f;
            skin.horizontalSliderThumb.border = new RectOffset(0, 0, 0, 0);
            skin.horizontalSliderThumb.overflow = new RectOffset(0, 0, 4, 4);

            var track = Solid(p.Dark ? p.Window : p.Canvas);
            var bar = Rounded(p.ControlBorder, p.ControlBorder);
            var barLit = Rounded(p.PillSep, p.PillSep);
            foreach (var (back, handle) in new[] { (skin.verticalScrollbar, skin.verticalScrollbarThumb), (skin.horizontalScrollbar, skin.horizontalScrollbarThumb) })
            {
                back.normal.background = track;
                back.border = new RectOffset(0, 0, 0, 0);
                handle.normal.background = bar;
                handle.hover.background = handle.active.background = barLit;
                handle.border = Border();
            }

            skin.scrollView.normal.background = null;
            return skin;
        }

        private static void Style(GUIStyle style, Texture2D normal, Texture2D hover, Texture2D pressed, Texture2D on, Color text, Color onText)
        {
            style.normal.background = style.focused.background = normal;
            style.hover.background = hover;
            style.active.background = pressed;
            style.onNormal.background = style.onHover.background = style.onActive.background = style.onFocused.background = on;
            style.normal.textColor = style.hover.textColor = style.active.textColor = style.focused.textColor = text;
            style.onNormal.textColor = style.onHover.textColor = style.onActive.textColor = style.onFocused.textColor = onText;
            style.border = Border();
        }

        private static RectOffset Border() => new RectOffset(Radius + 1, Radius + 1, Radius + 1, Radius + 1);

        private static Texture2D Solid(UiColour c)
        {
            var texture = PlaceholderArt.Solid(UiTheme.ToColor(c));
            texture.hideFlags = HideFlags.HideAndDontSave;
            return texture;
        }

        /// <summary>A small rounded rectangle with a one-pixel border, for nine-slice stretching.</summary>
        private static Texture2D Rounded(UiColour fill, UiColour edge) => Draw(16, 16, Radius, (x, _) => fill, edge);

        /// <summary>A rounded rectangle shading from <paramref name="a"/> on the left to <paramref name="b"/> on the right.</summary>
        private static Texture2D Gradient(UiColour a, UiColour b) => Draw(48, 16, Radius, (x, w) => Mix(a, b, x / (float)(w - 1)), null);

        /// <summary>A filled circle: a slider handle.</summary>
        private static Texture2D Round(UiColour fill) => Draw(16, 16, 8, (x, _) => fill, null);

        private static Texture2D Draw(int width, int height, int radius, System.Func<int, int, UiColour> fillAt, UiColour? edge)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color32[width * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    // Distance outside the rounded rectangle, measured from the pixel centre; under 0 is inside.
                    var cx = Mathf.Clamp(x + 0.5f, radius, width - radius);
                    var cy = Mathf.Clamp(y + 0.5f, radius, height - radius);
                    var distance = new Vector2(x + 0.5f - cx, y + 0.5f - cy).magnitude - radius;
                    var coverage = Mathf.Clamp01(0.5f - distance);
                    var colour = edge.HasValue && distance > -1.5f ? edge.Value : fillAt(x, width);
                    pixels[(y * width) + x] = new Color32(colour.R, colour.G, colour.B, (byte)Mathf.RoundToInt(coverage * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        private static UiColour Mix(UiColour a, UiColour b, float t) => new UiColour(
            (byte)Mathf.RoundToInt(Mathf.Lerp(a.R, b.R, t)),
            (byte)Mathf.RoundToInt(Mathf.Lerp(a.G, b.G, t)),
            (byte)Mathf.RoundToInt(Mathf.Lerp(a.B, b.B, t)));
    }
}
