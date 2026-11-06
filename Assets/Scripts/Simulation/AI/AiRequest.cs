using System;

namespace TinyCity.Simulation.AI
{
    /// <summary>Lower values are served first. Optional flavour never delays a decision someone is waiting on.</summary>
    public enum AiPriority
    {
        Urgent = 0,
        Decision = 1,
        Dialogue = 2,
        Reflection = 3,
        Flavour = 4,
    }

    public enum AiReplyStatus
    {
        /// <summary>The answer was valid and the game applied it.</summary>
        Accepted,

        /// <summary>The answer was malformed or not allowed; worth retrying if attempts remain.</summary>
        Invalid,

        /// <summary>The situation has moved on, so the answer no longer matters. Not retried.</summary>
        Stale,
    }

    public readonly struct AiReplyCheck
    {
        private AiReplyCheck(AiReplyStatus status, string detail)
        {
            Status = status;
            Detail = detail ?? string.Empty;
        }

        public AiReplyStatus Status { get; }

        public string Detail { get; }

        public static AiReplyCheck Accepted(string detail = null) => new AiReplyCheck(AiReplyStatus.Accepted, detail);

        public static AiReplyCheck Invalid(string detail) => new AiReplyCheck(AiReplyStatus.Invalid, detail);

        public static AiReplyCheck Stale(string detail = null) => new AiReplyCheck(AiReplyStatus.Stale, detail);
    }

    /// <summary>
    /// One request for the AI. The handler validates and applies the answer; <see cref="IsStillWanted"/> lets the
    /// queue drop requests whose situation has changed before or while they are sent.
    /// </summary>
    public sealed class AiRequest
    {
        public AiRequest(
            string key,
            string residentId,
            AiPriority priority,
            string description,
            string body,
            Func<string, AiReplyCheck> handleReply,
            Func<bool> isStillWanted = null,
            Action<string> onGiveUp = null,
            string url = null)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("A request needs a deduplication key.", nameof(key));
            }

            Key = key;
            ResidentId = residentId;
            Priority = priority;
            Description = description ?? key;
            Body = body ?? throw new ArgumentNullException(nameof(body));
            HandleReply = handleReply ?? throw new ArgumentNullException(nameof(handleReply));
            IsStillWanted = isStillWanted ?? (() => true);
            OnGiveUp = onGiveUp ?? (_ => { });
            Url = url;
        }

        /// <summary>
        /// A request whose body is made just before each time it is sent, so a prompt that waited in the queue
        /// describes the town as it is then (such as which roles are still free), not as it was when it was asked.
        /// </summary>
        public AiRequest(
            string key,
            string residentId,
            AiPriority priority,
            string description,
            Func<string> makeBody,
            Func<string, AiReplyCheck> handleReply,
            Func<bool> isStillWanted = null,
            Action<string> onGiveUp = null)
            : this(key, residentId, priority, description, string.Empty, handleReply, isStillWanted, onGiveUp)
        {
            this.makeBody = makeBody ?? throw new ArgumentNullException(nameof(makeBody));
        }

        private readonly Func<string> makeBody;

        /// <summary>Makes the body again from the town as it is now, for a request made with a body maker.</summary>
        internal void RefreshBody()
        {
            if (makeBody != null)
            {
                Body = makeBody() ?? string.Empty;
            }
        }

        /// <summary>Where to send it, or null for the queue's own address (the chat endpoint).</summary>
        public string Url { get; }

        /// <summary>Assigned by the queue; unique for the queue's lifetime.</summary>
        public long Id { get; internal set; }

        /// <summary>Requests with the same key replace each other, e.g. one decision per resident.</summary>
        public string Key { get; }

        public string ResidentId { get; }

        public AiPriority Priority { get; }

        /// <summary>Short text for diagnostics, e.g. "decision for Mabel".</summary>
        public string Description { get; }

        public string Body { get; private set; }

        public Func<string, AiReplyCheck> HandleReply { get; }

        public Func<bool> IsStillWanted { get; }

        public Action<string> OnGiveUp { get; }

        public int Attempts { get; internal set; }

        public double EnqueuedAt { get; internal set; }

        public double NotBefore { get; internal set; }

        public string LastError { get; internal set; } = string.Empty;
    }
}
