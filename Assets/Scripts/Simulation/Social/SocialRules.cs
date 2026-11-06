using System;
using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation.Activities;
using TinyCity.Simulation.Health;
using TinyCity.Simulation.Memory;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Time;

namespace TinyCity.Simulation.Social
{
    /// <summary>The game's rules for who can talk, for how long, and what a conversation does to a relationship.</summary>
    public static class SocialRules
    {
        /// <summary>A conversation may end early only after this many lines, so both residents contribute.</summary>
        public const int MinLines = 4;

        /// <summary>Chats are at most this many lines, since each line is one AI request.</summary>
        public const int MaxLines = 6;

        /// <summary>Game minutes between lines.</summary>
        public const int MinutesPerLine = 2;

        /// <summary>
        /// No conversation lasts longer than this many minutes of talking. Minutes spent waiting for an AI line don't
        /// count, so a chat at high speed isn't cut short by the AI's real-time answer speed.
        /// </summary>
        public const int MaxConversationMinutes = 40;

        /// <summary>The same two residents won't start another conversation within this many game minutes.</summary>
        public const int PairCooldownMinutes = 180;

        /// <summary>Longest walk, in steps, worth making to find someone to talk to.</summary>
        public const int MaxSeekDistance = 45;

        /// <summary>Game minutes a resident keeps looking for someone before giving up.</summary>
        public const int SeekMinutes = 45;

        /// <summary>The AI is offered chats with at most this many people, the nearest, so prompts stay short in a busy town.</summary>
        public const int MaxChatOptions = 3;

        public const int MaxTopicLength = 40;

        /// <summary>Friendship at which a memory of becoming good friends is made.</summary>
        public const double GoodFriendsAt = 40;

        /// <summary>How many lines this pair will exchange: longer between friends and between two chatty people.</summary>
        public static int LinesFor(Resident a, Resident b, Relationship ab, Relationship ba)
        {
            var lines = MinLines + 2;
            if ((ab.Friendship + ba.Friendship) / 2 >= 30)
            {
                lines += 2;
            }

            if (a.Personality.Sociable + b.Personality.Sociable >= 1.2)
            {
                lines += 2;
            }

            return Math.Min(MaxLines, lines);
        }

        public static bool CooldownOver(RelationshipBook book, string a, string b, GameTime now) =>
            !book.TryGet(a, b, out var rel) || rel.LastInteractionMinute == Relationship.Never
            || now.TotalMinutes - rel.LastInteractionMinute >= PairCooldownMinutes;

        /// <summary>A reason this resident must leave a conversation now, or null to stay.</summary>
        public static string ReasonToLeave(Resident r, GameTime now)
        {
            var urgent = ActivityPlanner.MostUrgentNeed(r);
            if (urgent != null)
            {
                return $"{urgent.Value.ToString().ToLowerInvariant()} couldn't wait";
            }

            return null;
        }

        /// <summary>Applies a finished conversation to both residents' views of each other.</summary>
        public static void ApplyConversation(Relationship aViewOfB, Relationship bViewOfA, IEnumerable<ConversationLine> lines, GameTime now)
        {
            foreach (var line in lines)
            {
                var listenerView = line.SpeakerId == aViewOfB.OwnerId ? bViewOfA : aViewOfB;
                var speakerView = listenerView == aViewOfB ? bViewOfA : aViewOfB;
                switch (line.Tone)
                {
                    case ConversationTone.Warm:
                        listenerView.Change(friendship: 1.5, trust: 0.5);
                        speakerView.Change(friendship: 0.5);
                        break;
                    case ConversationTone.Playful:
                        listenerView.Change(friendship: 2);
                        speakerView.Change(friendship: 0.5);
                        break;
                    case ConversationTone.Disagree:
                        listenerView.Change(friendship: -1, respect: 0.5, dislike: 3);
                        break;
                    default:
                        listenerView.Change(friendship: 0.5);
                        break;
                }
            }

            foreach (var view in new[] { aViewOfB, bViewOfA })
            {
                view.Change(friendship: 1, trust: 0.5);
                view.Interactions++;
                view.LastInteractionMinute = now.TotalMinutes;
            }
        }

        public static string CleanTopic(string topic) => TextRules.Clean(topic, MaxTopicLength).TrimEnd('.', '!', '?');

        public static bool HadDisagreement(IEnumerable<ConversationLine> lines) => lines.Any(l => l.Tone == ConversationTone.Disagree);
    }
}
