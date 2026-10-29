using TinyCity.Simulation.Configuration;
using UnityEngine;

namespace TinyCity.Runtime
{
    /// <summary>
    /// The IMGUI styles shared by the inspector, windows and camera panels, in the colours of a <see cref="UiPalette"/>.
    /// <see cref="UiTheme"/> makes one per palette, inside OnGUI with its skin set. Labels drawn on the town itself
    /// (resident names, object and door signs) keep the dark ink of the village art whatever the theme.
    /// </summary>
    public sealed class GuiStyles
    {
        /// <summary>The ink of labels drawn on the town.</summary>
        public static readonly Color Ink = new Color(0.3f, 0.2f, 0.12f);

        public GuiStyles(UiPalette p)
        {
            var text = UiTheme.ToColor(p.Text);
            var subText = UiTheme.ToColor(p.SubText);
            Title = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, wordWrap = true };
            Text = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
            Small = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            Section = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, wordWrap = true, margin = new RectOffset(4, 4, 10, 2) };
            Title.normal.textColor = Text.normal.textColor = text;
            Small.normal.textColor = Section.normal.textColor = subText;

            Name = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            Sign = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            Name.normal.textColor = Sign.normal.textColor = Ink;

            Header = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, clipping = TextClipping.Clip, alignment = TextAnchor.MiddleLeft };
            Header.normal.textColor = text;
            HeaderButton = new GUIStyle(GUI.skin.button) { fontSize = 11, padding = new RectOffset(2, 2, 1, 1), border = new RectOffset(4, 4, 4, 4) };

            Bubble = new GUIStyle(GUI.skin.box) { fontSize = 13, wordWrap = true, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(8, 8, 6, 6) };
            Bubble.normal.textColor = text;

            AiNote = new GUIStyle(GUI.skin.box) { fontSize = 12, wordWrap = true, alignment = TextAnchor.UpperLeft, padding = new RectOffset(6, 6, 4, 4) };
            AiNote.normal.background = PlaceholderArt.Solid(UiTheme.ToColor(p.Pill));
            AiNote.normal.textColor = text;

            Notice = new GUIStyle(GUI.skin.button) { fontSize = 13, wordWrap = true, alignment = TextAnchor.UpperLeft, padding = new RectOffset(10, 10, 6, 6) };
            Notice.normal.background = GUI.skin.box.normal.background;
            Notice.normal.textColor = Notice.hover.textColor = Notice.active.textColor = text;

            Entry = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true, padding = new RectOffset(4, 4, 2, 2) };
            Entry.normal.textColor = Entry.hover.textColor = text;
            Entry.hover.background = PlaceholderArt.Solid(UiTheme.ToColor(p.Hover));

            Warning = new GUIStyle(Small);
            Warning.normal.textColor = UiTheme.ToColor(p.Warning);
            foreach (var texture in new[] { AiNote.normal.background, Entry.hover.background })
            {
                texture.hideFlags = HideFlags.HideAndDontSave;
            }
        }

        public GUIStyle Title { get; }

        public GUIStyle Text { get; }

        public GUIStyle Small { get; }

        /// <summary>A small bold heading inside a window.</summary>
        public GUIStyle Section { get; }

        /// <summary>A line that needs attention, in the theme's warning colour.</summary>
        public GUIStyle Warning { get; }

        /// <summary>Object names and door signs drawn on the town.</summary>
        public GUIStyle Sign { get; }

        /// <summary>Residents' names over their heads.</summary>
        public GUIStyle Name { get; }

        /// <summary>The text in a camera panel's header bar.</summary>
        public GUIStyle Header { get; }

        public GUIStyle HeaderButton { get; }

        public GUIStyle Bubble { get; }

        /// <summary>A tinted box for anything the AI wrote, so it is never mistaken for a game fact.</summary>
        public GUIStyle AiNote { get; }

        /// <summary>A pop-up notification.</summary>
        public GUIStyle Notice { get; }

        /// <summary>A clickable line in the journal.</summary>
        public GUIStyle Entry { get; }
    }
}
