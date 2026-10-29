using System;
using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation;
using TinyCity.Simulation.History;
using UnityEngine;

namespace TinyCity.Runtime
{
    /// <summary>
    /// Pops up the town's big moments (see <see cref="Journal.IsNotable"/>) in the corner of the camera area. Clicking
    /// one goes to whoever it was about. Everything stays in the journal after the pop-up fades.
    /// </summary>
    public sealed class Notifications
    {
        public const int MaxShown = 4;
        public const float ShowSeconds = 12f;
        private const float Width = 330f;

        private readonly List<(TownEvent entry, float until)> shown = new List<(TownEvent, float)>();
        private long nextNumber = 1;

        /// <summary>The notifications on screen now, newest last.</summary>
        public IEnumerable<TownEvent> Showing => shown.Select(s => s.entry);

        /// <summary>Starts watching a town from now, so loading a save doesn't replay its whole history.</summary>
        public void Reset(TownSimulation simulation)
        {
            shown.Clear();
            nextNumber = simulation.History.NextNumber;
        }

        /// <summary>Picks up notable events added since the last call. Call once per frame.</summary>
        public void Update(TownSimulation simulation, float now)
        {
            if (simulation.History.NextNumber != nextNumber)
            {
                foreach (var entry in simulation.History.Since(nextNumber).Where(e => Journal.IsNotable(e.Kind)))
                {
                    shown.Add((entry, now + ShowSeconds));
                }

                nextNumber = simulation.History.NextNumber;
            }

            shown.RemoveAll(s => s.until <= now);
            while (shown.Count > MaxShown)
            {
                shown.RemoveAt(0);
            }
        }

        /// <summary>Draws the pop-ups down from the top left of <paramref name="area"/>; a click calls <paramref name="clicked"/> and dismisses it.</summary>
        public void Draw(Rect area, GuiStyles styles, Action<TownEvent> clicked)
        {
            var y = area.y + CameraPanels.HeaderHeight + 8f;
            foreach (var (entry, _) in shown.ToList())
            {
                var content = new GUIContent($"{Journal.DateText(entry.Time)}\n{entry.Summary}");
                var width = Mathf.Min(Width, area.width - 20f);
                var height = styles.Notice.CalcHeight(content, width);
                if (GUI.Button(new Rect(area.x + 10f, y, width, height), content, styles.Notice))
                {
                    shown.RemoveAll(s => s.entry == entry);
                    clicked?.Invoke(entry);
                }

                y += height + 6f;
            }
        }
    }
}
