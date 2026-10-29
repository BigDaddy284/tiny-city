using System;
using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation.Time;

namespace TinyCity.Simulation.Economy
{
    public enum TransactionKind
    {
        Wage,
        Income,
        Rent,
        Purchase,
    }

    /// <summary>One change to a resident's money. Every change goes through the ledger, so balances can be explained.</summary>
    public sealed class Transaction
    {
        internal Transaction(string id, GameTime time, string residentId, long amountPence, TransactionKind kind, string description, string businessId)
        {
            Id = id;
            Time = time;
            ResidentId = residentId;
            AmountPence = amountPence;
            Kind = kind;
            Description = description ?? string.Empty;
            BusinessId = businessId;
        }

        /// <summary>Stable id such as "txn-000042", never reused.</summary>
        public string Id { get; }

        public GameTime Time { get; }

        public string ResidentId { get; }

        /// <summary>Positive for money in, negative for money out.</summary>
        public long AmountPence { get; }

        public TransactionKind Kind { get; }

        public string Description { get; }

        /// <summary>The business paid, for purchases; otherwise null.</summary>
        public string BusinessId { get; }
    }

    /// <summary>The town's record of payments, oldest first. Capped; the oldest records are dropped first.</summary>
    public sealed class Ledger
    {
        public const int MaxRecords = 3000;

        private readonly List<Transaction> records = new List<Transaction>();

        public IReadOnlyList<Transaction> All => records;

        public long NextNumber { get; private set; } = 1;

        public IEnumerable<Transaction> For(string residentId) => records.Where(t => t.ResidentId == residentId);

        internal Transaction Add(GameTime time, string residentId, long amountPence, TransactionKind kind, string description, string businessId = null)
        {
            var record = new Transaction($"txn-{NextNumber:000000}", time, residentId, amountPence, kind, description, businessId);
            NextNumber++;
            Insert(record);
            return record;
        }

        /// <summary>Restores a saved record exactly. Used only by the save loader.</summary>
        internal void Restore(Transaction record, long nextNumber)
        {
            Insert(record);
            NextNumber = Math.Max(NextNumber, nextNumber);
        }

        internal void RestoreNextNumber(long nextNumber) => NextNumber = Math.Max(NextNumber, nextNumber);

        private void Insert(Transaction record)
        {
            records.Add(record);
            if (records.Count > MaxRecords)
            {
                records.RemoveRange(0, records.Count - MaxRecords);
            }
        }
    }
}
