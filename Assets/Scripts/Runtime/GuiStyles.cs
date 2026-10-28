using UnityEngine;

namespace TinyTown.Runtime
{
    /// <summary>The IMGUI styles shared by the inspector and the camera panels. Must be created inside OnGUI.</summary>
    public sealed class GuiStyles
    {
        public static readonly Color Ink = new Color(0.3f, 0.2f, 0.12f);

        public GuiStyles()
        {
            Title = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, wordWrap = true };
            Text = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
            Small = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            Name = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            Title.normal.textColor = Text.normal.textColor = Small.normal.textColor = Name.normal.textColor = Ink;

            Header = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, clipping = TextClipping.Clip, alignment = TextAnchor.MiddleLeft };
            Header.normal.textColor = new Color(1f, 0.97f, 0.9f);
            HeaderButton = new GUIStyle(GUI.skin.button) { fontSize = 11, padding = new RectOffset(2, 2, 1, 1) };

            Bubble = new GUIStyle(GUI.skin.box) { fontSize = 13, wordWrap = true, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(8, 8, 6, 6) };
            Bubble.normal.background = PlaceholderArt.Solid(new Color(1f, 0.99f, 0.95f, 0.95f));
            Bubble.normal.textColor = Ink;

            AiNote = new GUIStyle(GUI.skin.box) { fontSize = 12, wordWrap = true, alignment = TextAnchor.UpperLeft, padding = new RectOffset(6, 6, 4, 4) };
            AiNote.normal.background = PlaceholderArt.Solid(new Color(0.9f, 0.93f, 1f, 1f));
            AiNote.normal.textColor = new Color(0.2f, 0.25f, 0.45f);

            Notice = new GUIStyle(GUI.skin.button) { fontSize = 13, wordWrap = true, alignment = TextAnchor.UpperLeft, padding = new RectOffset(10, 10, 6, 6) };
            Notice.normal.background = PlaceholderArt.Solid(new Color(1f, 0.95f, 0.82f, 0.97f));
            Notice.hover.background = PlaceholderArt.Solid(new Color(1f, 0.9f, 0.7f, 1f));
            Notice.normal.textColor = Notice.hover.textColor = Ink;

            Entry = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true, padding = new RectOffset(4, 4, 2, 2) };
            Entry.normal.textColor = Ink;
            Entry.hover.background = PlaceholderArt.Solid(new Color(1f, 0.9f, 0.72f, 1f));
            Entry.hover.textColor = Ink;
        }

        public GUIStyle Title { get; }

        public GUIStyle Text { get; }

        public GUIStyle Small { get; }

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
