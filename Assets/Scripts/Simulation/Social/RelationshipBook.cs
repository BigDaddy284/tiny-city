using System;
using System.Collections.Generic;
using System.Linq;

namespace TinyCity.Simulation.Social
{
    /// <summary>All relationships in the town, created on first use with neutral values. Never more than one per ordered pair.</summary>
    public sealed class RelationshipBook
    {
        private readonly Dictionary<(string, string), Relationship> byPair = new Dictionary<(string, string), Relationship>();
        private readonly List<Relationship> ordered = new List<Relationship>();

        /// <summary>In creation order, so saves are stable.</summary>
        public IReadOnlyList<Relationship> All => ordered;

        public Relationship Get(string ownerId, string otherId)
        {
            if (string.IsNullOrWhiteSpace(ownerId) || string.IsNullOrWhiteSpace(otherId) || ownerId == otherId)
            {
                throw new ArgumentException("A relationship needs two different residents.");
            }

            if (!byPair.TryGetValue((ownerId, otherId), out var relationship))
            {
                relationship = new Relationship(ownerId, otherId);
                byPair.Add((ownerId, otherId), relationship);
                ordered.Add(relationship);
            }

            return relationship;
        }

        /// <summary>Looks without creating.</summary>
        public bool TryGet(string ownerId, string otherId, out Relationship relationship) =>
            byPair.TryGetValue((ownerId, otherId), out relationship);

        public IEnumerable<Relationship> Of(string ownerId) => ordered.Where(r => r.OwnerId == ownerId);

        private readonly List<Partnership> partnerships = new List<Partnership>();

        /// <summary>Couples together now. A resident is in at most one.</summary>
        public IReadOnlyList<Partnership> Partnerships => partnerships;

        public Partnership PartnershipOf(string residentId) => partnerships.FirstOrDefault(p => p.Includes(residentId));

        public string PartnerOf(string residentId) => PartnershipOf(residentId)?.OtherThan(residentId);

        internal Partnership StartPartnership(string aId, string bId, Time.GameTime since)
        {
            if (PartnershipOf(aId) != null || PartnershipOf(bId) != null || aId == bId)
            {
                throw new InvalidOperationException("Both residents must be single.");
            }

            var partnership = new Partnership(aId, bId, RomanceStage.Dating, since);
            partnerships.Add(partnership);
            return partnership;
        }

        internal void EndPartnership(Partnership partnership) => partnerships.Remove(partnership);
    }
}
