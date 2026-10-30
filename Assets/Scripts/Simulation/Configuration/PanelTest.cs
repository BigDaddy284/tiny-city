using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace TinyCity.Simulation.Configuration
{
    /// <summary>
    /// Measures how smoothly the game runs with more and more camera panels. The game shows 1 panel, then 2, and so on
    /// up to <see cref="Largest"/>, letting each settle for <see cref="SettleSeconds"/> and then timing frames for
    /// <see cref="MeasureSeconds"/>. It only does the sums: the game feeds it each frame's time and sets the panel count
    /// it asks for. The suggested limit is the most panels whose frames stay smooth (<see cref="SmoothMilliseconds"/> on
    /// average, and <see cref="SlowestMilliseconds"/> for the slowest one in twenty).
    /// </summary>
    public sealed class PanelTest
    {
        public const int Largest = 50;
        public const double SettleSeconds = 1.5;
        public const double MeasureSeconds = 4.0;

        /// <summary>60 frames a second.</summary>
        public const double SmoothMilliseconds = 1000.0 / 60.0;

        /// <summary>30 frames a second: a frame slower than this is a visible stutter.</summary>
        public const double SlowestMilliseconds = 1000.0 / 30.0;

        private readonly int largest;
        private readonly List<double> frames = new List<double>();
        private readonly List<PanelTestResult> results = new List<PanelTestResult>();
        private double elapsed;

        public PanelTest(int largest = Largest)
        {
            if (largest < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(largest));
            }

            this.largest = largest;
            PanelCount = 1;
        }

        /// <summary>How many panels the game should show right now.</summary>
        public int PanelCount { get; private set; }

        public bool Finished { get; private set; }

        public IReadOnlyList<PanelTestResult> Results => results;

        /// <summary>A rough share of the way through, 0 to 1.</summary>
        public double Progress => Finished ? 1.0 : ((PanelCount - 1) + Math.Min(1.0, elapsed / (SettleSeconds + MeasureSeconds))) / largest;

        /// <summary>Takes one frame's time. Returns true when the panel count has changed (or the test has finished).</summary>
        public bool AddFrame(double seconds)
        {
            if (Finished || seconds < 0 || double.IsNaN(seconds))
            {
                return false;
            }

            elapsed += seconds;
            if (elapsed > SettleSeconds)
            {
                frames.Add(seconds * 1000.0);
            }

            if (elapsed < SettleSeconds + MeasureSeconds)
            {
                return false;
            }

            results.Add(PanelTestResult.Of(PanelCount, frames));
            frames.Clear();
            elapsed = 0;
            if (PanelCount >= largest)
            {
                Finished = true;
            }
            else
            {
                PanelCount++;
            }

            return true;
        }

        /// <summary>The most panels that stayed smooth, counting up from one until the first that didn't; 1 at the least.</summary>
        public int SuggestedLimit()
        {
            var limit = 1;
            foreach (var result in results.OrderBy(r => r.Panels))
            {
                if (!result.IsSmooth)
                {
                    break;
                }

                limit = result.Panels;
            }

            return limit;
        }

        /// <summary>A plain-text report: one line per panel count, then the suggested limit.</summary>
        public string Report(string machine)
        {
            var text = new StringBuilder();
            text.AppendLine("Tiny City camera panel test");
            if (!string.IsNullOrEmpty(machine))
            {
                text.AppendLine(machine);
            }

            text.AppendLine();
            text.AppendLine("Panels  Average ms  FPS   Slowest 1 in 20 (ms)  Smooth");
            foreach (var r in results)
            {
                text.AppendLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0,6}  {1,10:0.0}  {2,4:0}  {3,20:0.0}  {4}",
                    r.Panels,
                    r.AverageMilliseconds,
                    r.AverageMilliseconds > 0 ? 1000.0 / r.AverageMilliseconds : 0,
                    r.SlowMilliseconds,
                    r.IsSmooth ? "yes" : "no"));
            }

            text.AppendLine();
            text.AppendLine($"Suggested limit: {SuggestedLimit()} panels.");
            return text.ToString();
        }
    }

    /// <summary>The frame times measured with one number of panels.</summary>
    public sealed class PanelTestResult
    {
        public int Panels { get; private set; }

        public int Frames { get; private set; }

        public double AverageMilliseconds { get; private set; }

        /// <summary>The 95th percentile: the slowest one frame in twenty.</summary>
        public double SlowMilliseconds { get; private set; }

        public bool IsSmooth => Frames > 0 && AverageMilliseconds <= PanelTest.SmoothMilliseconds && SlowMilliseconds <= PanelTest.SlowestMilliseconds;

        public static PanelTestResult Of(int panels, IReadOnlyList<double> milliseconds)
        {
            var sorted = milliseconds.OrderBy(m => m).ToList();
            return new PanelTestResult
            {
                Panels = panels,
                Frames = sorted.Count,
                AverageMilliseconds = sorted.Count == 0 ? 0 : sorted.Average(),
                SlowMilliseconds = sorted.Count == 0 ? 0 : sorted[Math.Min(sorted.Count - 1, (int)Math.Ceiling(sorted.Count * 0.95) - 1)],
            };
        }
    }
}
