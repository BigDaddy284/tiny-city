using System;
using System.Runtime.InteropServices;
using TinyCity.Simulation.Configuration;
using UnityEngine;

namespace TinyCity.Runtime
{
    /// <summary>
    /// Colours the game window's title bar and border with the interface theme, as Stills &amp; Reels and
    /// StackSpace do: the theme's window colour behind the title, its text colour for the title, and dark-mode
    /// caption buttons for the dark theme. Windows 11 takes the exact colours; Windows 10 only switches between
    /// its dark and light title bars. Elsewhere (the editor, other systems) it does nothing.
    /// </summary>
    internal static class WindowFrame
    {
        public static void Apply(UiPalette palette)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try
            {
                var window = FindGameWindow();
                if (window == IntPtr.Zero)
                {
                    return;
                }

                var dark = palette.Dark ? 1 : 0;
                if (DwmSetWindowAttribute(window, UseImmersiveDarkMode, ref dark, sizeof(int)) != 0)
                {
                    // Windows 10 before 20H1 used another number for the same setting.
                    DwmSetWindowAttribute(window, UseImmersiveDarkModeOld, ref dark, sizeof(int));
                }

                var caption = (int)palette.Window.ColorRef;
                var text = (int)palette.Text.ColorRef;
                var border = (int)palette.BarBorder.ColorRef;
                DwmSetWindowAttribute(window, CaptionColour, ref caption, sizeof(int));
                DwmSetWindowAttribute(window, TextColour, ref text, sizeof(int));
                DwmSetWindowAttribute(window, BorderColour, ref border, sizeof(int));
            }
            catch (Exception e) when (e is DllNotFoundException || e is EntryPointNotFoundException)
            {
                Debug.Log($"[TinyCity] The title bar keeps its system colours: {e.Message}");
            }
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private const int UseImmersiveDarkModeOld = 19;
        private const int UseImmersiveDarkMode = 20;
        private const int BorderColour = 34;
        private const int CaptionColour = 35;
        private const int TextColour = 36;

        private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

        private static uint searchedProcess;
        private static IntPtr foundWindow;

        /// <summary>This process's visible Unity window (the active window can be another app's while loading).</summary>
        private static IntPtr FindGameWindow()
        {
            searchedProcess = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
            foundWindow = IntPtr.Zero;
            EnumWindows(CheckWindow, IntPtr.Zero);
            return foundWindow;
        }

        [AOT.MonoPInvokeCallback(typeof(EnumWindowsProc))]
        private static bool CheckWindow(IntPtr window, IntPtr parameter)
        {
            GetWindowThreadProcessId(window, out var owner);
            if (owner != searchedProcess || !IsWindowVisible(window))
            {
                return true;
            }

            var name = new System.Text.StringBuilder(64);
            GetClassName(window, name, name.Capacity);
            if (name.ToString() != "UnityWndClass")
            {
                return true;
            }

            foundWindow = window;
            return false;
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr window, System.Text.StringBuilder name, int capacity);
#endif
    }
}
