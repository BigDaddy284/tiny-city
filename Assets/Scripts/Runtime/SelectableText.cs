using System.Collections.Generic;
using UnityEngine;

namespace TinyCity.Runtime
{
    /// <summary>
    /// Text the player can highlight with the mouse and copy with Ctrl+C (owner, 6 November), drawn exactly like a
    /// label. IMGUI only lets text areas be selected, so each one is a read-only text area in the label's style:
    /// <see cref="BlockEditing"/> stops typing, cutting, pasting and deleting from changing it.
    /// </summary>
    public static class SelectableText
    {
        private const string ControlName = "tinycity-selectable-text";

        private static readonly Dictionary<GUIStyle, GUIStyle> Looks = new Dictionary<GUIStyle, GUIStyle>();

        /// <summary>Draws <paramref name="text"/> in the layout like <c>GUILayout.Label</c>, but selectable.</summary>
        public static void Label(string text, GUIStyle style, params GUILayoutOption[] options)
        {
            GUI.SetNextControlName(ControlName);
            GUILayout.TextArea(text ?? string.Empty, LookOf(style), options);
        }

        /// <summary>
        /// Call before drawing, each OnGUI event: while one of these texts has the keyboard, only copying, selecting
        /// all and moving or extending the selection get through.
        /// </summary>
        public static void BlockEditing(Event e)
        {
            if (GUI.GetNameOfFocusedControl() != ControlName)
            {
                return;
            }

            if (e.type == EventType.KeyDown && !Allowed(e))
            {
                e.Use();
            }
            else if ((e.type == EventType.ValidateCommand || e.type == EventType.ExecuteCommand) && e.commandName != "Copy" && e.commandName != "SelectAll")
            {
                e.Use();
            }
        }

        private static bool Allowed(Event e)
        {
            switch (e.keyCode)
            {
                case KeyCode.C:
                case KeyCode.A:
                    return e.control || e.command;
                case KeyCode.LeftArrow:
                case KeyCode.RightArrow:
                case KeyCode.UpArrow:
                case KeyCode.DownArrow:
                case KeyCode.Home:
                case KeyCode.End:
                case KeyCode.PageUp:
                case KeyCode.PageDown:
                case KeyCode.Escape:
                case KeyCode.LeftShift:
                case KeyCode.RightShift:
                case KeyCode.LeftControl:
                case KeyCode.RightControl:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>The label's style with every state drawn as its normal one, so focus and hover don't change how it looks.</summary>
        private static GUIStyle LookOf(GUIStyle style)
        {
            if (style == null)
            {
                style = GUI.skin.label;
            }

            if (!Looks.TryGetValue(style, out var look))
            {
                if (Looks.Count > 200)
                {
                    // The theme was changed several times: its old styles are no longer drawn.
                    Looks.Clear();
                }

                look = new GUIStyle(style);
                look.hover = look.active = look.focused = look.onNormal = look.onHover = look.onActive = look.onFocused = style.normal;
                Looks[style] = look;
            }

            return look;
        }
    }
}
