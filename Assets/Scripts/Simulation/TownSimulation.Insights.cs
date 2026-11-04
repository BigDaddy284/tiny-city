using System;
using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation.Memory;
using TinyCity.Simulation.Residents;

namespace TinyCity.Simulation
{
    /// <summary>One insight the AI drew, with the memories it rests on.</summary>
    public sealed class InsightProposal
    {
        public InsightProposal(string text, IReadOnlyList<string> evidenceIds)
        {
            Text = text;
            EvidenceIds = evidenceIds ?? new List<string>();
        }

        public string Text { get; }

        public IReadOnlyList<string> EvidenceIds { get; }
    }

    /// <summary>
    /// Tiny City 2: reflection with insights, as the generative agents do. Once the importance of what a resident
    /// has lived through since their last insight adds up to <see cref="InsightThreshold"/>, the AI is asked what
    /// it all means to them. Each insight cites the memories it comes from and is kept as an
    /// <see cref="MemoryType.Insight"/> memory; tomorrow's plan is told about the latest ones.
    /// </summary>
    public sealed partial class TownSimulation
    {
        public const int InsightThreshold = 60;
        public const int InsightEvidenceCount = 15;
        public const int MaxInsightsPerReflection = 3;
        public const int InsightImportance = 6;
        public const int InsightTextLength = 160;

        private static long MemoryNumber(MemoryRecord m) =>
            m != null && m.Id.Length > 4 && long.TryParse(m.Id.Substring(4), out var n) ? n : 0;

        /// <summary>The newest insight this resident has, or null.</summary>
        public MemoryRecord LatestInsight(string residentId) =>
            Memories.ForResident(residentId).Where(m => m.Type == MemoryType.Insight).OrderByDescending(MemoryNumber).FirstOrDefault();

        /// <summary>Memories since the last insight, oldest first (insights themselves are left out).</summary>
        public List<MemoryRecord> MemoriesSinceInsight(string residentId)
        {
            var since = MemoryNumber(LatestInsight(residentId));
            return Memories.ForResident(residentId)
                .Where(m => m.Type != MemoryType.Insight && MemoryNumber(m) > since)
                .OrderBy(MemoryNumber)
                .ToList();
        }

        public bool IsInsightDue(Resident r) =>
            r != null && r.IsAlive && !r.IsBaby && MemoriesSinceInsight(r.Id).Sum(m => m.Importance) >= InsightThreshold;

        /// <summary>The memories offered as evidence: the most recent since the last insight, oldest first.</summary>
        public List<MemoryRecord> InsightEvidence(string residentId)
        {
            var since = MemoriesSinceInsight(residentId);
            return since.Skip(Math.Max(0, since.Count - InsightEvidenceCount)).ToList();
        }

        /// <summary>
        /// Keeps the AI's insights, each citing at least one of the <paramref name="offeredIds"/>. Stale if an
        /// insight was added since the question was asked; invalid if none of them is usable.
        /// </summary>
        public DecisionOutcome ApplyInsights(string residentId, IReadOnlyCollection<string> offeredIds, IEnumerable<InsightProposal> proposals)
        {
            var r = GetResident(residentId);
            if (r == null)
            {
                return DecisionOutcome.UnknownResident;
            }

            if (!r.IsAlive)
            {
                return DecisionOutcome.NotAlive;
            }

            var offered = new HashSet<string>(offeredIds ?? Array.Empty<string>());
            var latest = LatestInsight(r.Id);
            if (offered.Count == 0 || (latest != null && offered.Any(id => MemoryNumber(Memories.Get(id)) < MemoryNumber(latest))))
            {
                return DecisionOutcome.Stale;
            }

            var kept = 0;
            foreach (var p in (proposals ?? Enumerable.Empty<InsightProposal>()).Take(MaxInsightsPerReflection))
            {
                var evidence = p.EvidenceIds.Where(offered.Contains).Distinct().ToList();
                var text = TextRules.Clean(p.Text, InsightTextLength);
                if (evidence.Count == 0 || text.Length == 0)
                {
                    continue;
                }

                var people = evidence.SelectMany(id => Memories.Get(id)?.Participants ?? Enumerable.Empty<string>()).Where(id => id != r.Id).Distinct();
                if (Memories.Add(r.Id, Time, MemoryType.Insight, InsightImportance, text, people, new[] { "insight" }.Concat(evidence)) != null)
                {
                    kept++;
                }
            }

            if (kept == 0)
            {
                return DecisionOutcome.InvalidText;
            }

            r.AddLog(Time, kept == 1 ? "Had a realisation" : $"Had {kept} realisations");
            return DecisionOutcome.Applied;
        }
    }
}
