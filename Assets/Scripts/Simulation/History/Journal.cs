using System;
using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Time;

namespace TinyCity.Simulation.History
{
    /// <summary>The journal's filter buttons. Each <see cref="TownEventKind"/> belongs to one.</summary>
    public enum JournalTopic
    {
        All,
        Life,
        Love,
        Health,
        Friends,
        Gatherings,
    }

    /// <summary>
    /// Reading the town history as a journal: topics, which events deserve a notification, and searching. Nothing
    /// here changes the history.
    /// </summary>
    public static class Journal
    {
        /// <summary>The most entries one search returns, newest first.</summary>
        public const int MaxResults = 300;

        public static JournalTopic TopicOf(TownEventKind kind)
        {
            switch (kind)
            {
                case TownEventKind.Couple:
                case TownEventKind.Committed:
                case TownEventKind.BreakUp:
                case TownEventKind.Reconciled:
                    return JournalTopic.Love;
                case TownEventKind.Illness:
                case TownEventKind.SeriousIllness:
                case TownEventKind.Recovered:
                    return JournalTopic.Health;
                case TownEventKind.Met:
                case TownEventKind.Friends:
                    return JournalTopic.Friends;
                case TownEventKind.Gathering:
                    return JournalTopic.Gatherings;
                default:
                    return JournalTopic.Life;
            }
        }

        /// <summary>The big moments that pop up as a notification: deaths, love, new life stages, serious illness and newcomers.</summary>
        public static bool IsNotable(TownEventKind kind)
        {
            switch (kind)
            {
                case TownEventKind.Death:
                case TownEventKind.Couple:
                case TownEventKind.Committed:
                case TownEventKind.BreakUp:
                case TownEventKind.Reconciled:
                case TownEventKind.LifeStage:
                case TownEventKind.SeriousIllness:
                case TownEventKind.MovedIn:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Events newest first, at most <see cref="MaxResults"/>. <paramref name="residentId"/> keeps only events
        /// involving that resident. Every word of <paramref name="text"/> must appear, ignoring case, in the summary, the
        /// full name of someone involved, or the date ("day 12", "monday").
        /// </summary>
        public static List<TownEvent> Search(TownHistory history, Func<string, Resident> residents, JournalTopic topic = JournalTopic.All, string residentId = null, string text = null)
        {
            var words = (text ?? string.Empty).ToLowerInvariant().Split(new[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);
            var results = new List<TownEvent>();
            for (var i = history.Events.Count - 1; i >= 0 && results.Count < MaxResults; i--)
            {
                var e = history.Events[i];
                if ((topic != JournalTopic.All && TopicOf(e.Kind) != topic)
                    || (!string.IsNullOrEmpty(residentId) && !e.ResidentIds.Contains(residentId)))
                {
                    continue;
                }

                if (words.Length > 0)
                {
                    var haystack = SearchText(e, residents);
                    if (!words.All(w => haystack.Contains(w)))
                    {
                        continue;
                    }
                }

                results.Add(e);
            }

            return results;
        }

        /// <summary>"Day 12, Friday 14:00", the way the journal and exports date things.</summary>
        public static string DateText(GameTime time) => $"Day {time.Day}, {time.Weekday} {time.ClockText}";

        private static string SearchText(TownEvent e, Func<string, Resident> residents)
        {
            var names = e.ResidentIds.Select(id => residents?.Invoke(id)?.FullName ?? string.Empty);
            return $"{e.Summary} {string.Join(" ", names)} day {e.Time.Day} {e.Time.Weekday} {TopicOf(e.Kind)}".ToLowerInvariant();
        }
    }
}
