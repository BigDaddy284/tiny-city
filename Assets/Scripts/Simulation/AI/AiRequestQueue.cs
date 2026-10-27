using System;
using System.Collections.Generic;
using System.Linq;

namespace TinyTown.Simulation.AI
{
    /// <summary>Running totals for the inspector and for measuring AI performance.</summary>
    public sealed class AiQueueStats
    {
        public int Sent { get; internal set; }

        public int Accepted { get; internal set; }

        /// <summary>Requests abandoned after their last attempt failed, or dropped because the queue was full.</summary>
        public int GaveUp { get; internal set; }

        public int TimedOut { get; internal set; }

        public int InvalidReplies { get; internal set; }

        public int TransportErrors { get; internal set; }

        /// <summary>Requests discarded because the situation changed or a newer request replaced them.</summary>
        public int Stale { get; internal set; }

        public int Dropped { get; internal set; }

        public int ConsecutiveTransportFailures { get; internal set; }

        public int LongestQueue { get; internal set; }

        /// <summary>Seconds from sending to answer for the last completed call, or 0.</summary>
        public double LastLatencySeconds { get; internal set; }

        public double AverageLatencySeconds => completedCalls == 0 ? 0 : totalLatency / completedCalls;

        public string LastError { get; internal set; } = string.Empty;

        private int completedCalls;
        private double totalLatency;

        internal void RecordLatency(double seconds)
        {
            LastLatencySeconds = seconds;
            totalLatency += seconds;
            completedCalls++;
        }
    }

    /// <summary>
    /// The single place AI requests go. It sends one request at a time, serves higher priorities first while
    /// ageing waiting requests so nobody starves, lets residents who were just served wait behind others of the same
    /// priority, replaces duplicates, enforces timeouts and a bounded number of
    /// attempts, and drops requests whose situation has changed. It never throws because of a bad answer.
    /// </summary>
    public sealed class AiRequestQueue
    {
        public const int DefaultMaxPending = 32;

        /// <summary>Every this many seconds of waiting counts as one priority class better, so low priorities still get served.</summary>
        public const double AgingSecondsPerClass = 20;

        public const double RetryDelaySeconds = 2;

        /// <summary>A resident whose request was sent this recently waits behind other residents' requests of the same priority.</summary>
        public const double RecentlyServedSeconds = 15;

        /// <summary>How many priority classes worse a just-served resident's request counts, fading over <see cref="RecentlyServedSeconds"/>.</summary>
        public const double RecentlyServedPenalty = 0.75;

        private readonly IAiTransport transport;
        private readonly List<AiRequest> pending = new List<AiRequest>();
        private readonly Dictionary<string, double> lastServed = new Dictionary<string, double>();
        private long nextId = 1;
        private IAiCall activeCall;
        private double activeSentAt;
        private bool activeSuperseded;

        public AiRequestQueue(IAiTransport transport, string url, double timeoutSeconds, int maxAttempts, int maxPending = DefaultMaxPending)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            Url = url;
            TimeoutSeconds = Math.Max(1, timeoutSeconds);
            MaxAttempts = Math.Max(1, maxAttempts);
            MaxPending = Math.Max(1, maxPending);
        }

        public string Url { get; }

        public double TimeoutSeconds { get; }

        public int MaxAttempts { get; }

        public int MaxPending { get; }

        public AiQueueStats Stats { get; } = new AiQueueStats();

        public AiRequest Active { get; private set; }

        public int PendingCount => pending.Count;

        public IReadOnlyList<AiRequest> Pending => pending;

        /// <summary>
        /// Adds a request, replacing any waiting request with the same key. Returns false if the queue is full and
        /// this request is the least important, in which case it is given up immediately.
        /// </summary>
        public bool Enqueue(AiRequest request, double now)
        {
            var duplicate = pending.FindIndex(p => p.Key == request.Key);
            if (duplicate >= 0)
            {
                pending.RemoveAt(duplicate);
                Stats.Stale++;
            }

            if (Active != null && Active.Key == request.Key)
            {
                activeSuperseded = true;
            }

            request.Id = nextId++;
            request.EnqueuedAt = now;
            request.NotBefore = now;
            request.Attempts = 0;

            if (pending.Count >= MaxPending)
            {
                var worst = pending.Concat(new[] { request })
                    .OrderByDescending(p => (int)p.Priority)
                    .ThenByDescending(p => p.Id)
                    .First();
                pending.Remove(worst);
                Stats.Dropped++;
                GiveUp(worst, "the AI queue was full");
                if (worst == request)
                {
                    return false;
                }
            }

            pending.Add(request);
            Stats.LongestQueue = Math.Max(Stats.LongestQueue, pending.Count);
            return true;
        }

        public void Update(double now)
        {
            if (Active != null)
            {
                UpdateActive(now);
            }

            while (Active == null)
            {
                var next = NextReady(now);
                if (next == null)
                {
                    return;
                }

                pending.Remove(next);
                if (!SafeIsStillWanted(next))
                {
                    Stats.Stale++;
                    continue;
                }

                Send(next, now);
            }
        }

        /// <summary>Abandons everything, telling each request's owner, e.g. when Ollama goes offline.</summary>
        public void CancelAll(string reason)
        {
            if (Active != null)
            {
                activeCall?.Cancel();
                var active = Active;
                ClearActive();
                GiveUp(active, reason);
            }

            foreach (var request in pending.ToList())
            {
                GiveUp(request, reason);
            }

            pending.Clear();
        }

        /// <summary>Called after a successful connection check so earlier failures don't count against the service.</summary>
        public void ResetFailureCount() => Stats.ConsecutiveTransportFailures = 0;

        private void UpdateActive(double now)
        {
            var request = Active;
            if (activeSuperseded || !SafeIsStillWanted(request))
            {
                activeCall.Cancel();
                ClearActive();
                Stats.Stale++;
                return;
            }

            if (!activeCall.IsDone)
            {
                if (now - activeSentAt > TimeoutSeconds)
                {
                    activeCall.Cancel();
                    ClearActive();
                    Stats.TimedOut++;
                    RetryOrGiveUp(request, $"no answer within {TimeoutSeconds:0} seconds", now);
                }

                return;
            }

            var call = activeCall;
            ClearActive();
            Stats.RecordLatency(Math.Max(0, now - activeSentAt));
            if (!call.Succeeded)
            {
                Stats.TransportErrors++;
                Stats.ConsecutiveTransportFailures++;
                RetryOrGiveUp(request, string.IsNullOrEmpty(call.Error) ? "the request failed" : call.Error, now);
                return;
            }

            Stats.ConsecutiveTransportFailures = 0;
            AiReplyCheck check;
            try
            {
                check = request.HandleReply(call.ResponseText ?? string.Empty);
            }
            catch (Exception e)
            {
                check = AiReplyCheck.Invalid($"the answer could not be handled: {e.Message}");
            }

            switch (check.Status)
            {
                case AiReplyStatus.Accepted:
                    Stats.Accepted++;
                    break;
                case AiReplyStatus.Stale:
                    Stats.Stale++;
                    break;
                default:
                    Stats.InvalidReplies++;
                    RetryOrGiveUp(request, check.Detail, now);
                    break;
            }
        }

        private AiRequest NextReady(double now)
        {
            AiRequest best = null;
            var bestScore = double.PositiveInfinity;
            foreach (var request in pending)
            {
                if (request.NotBefore > now)
                {
                    continue;
                }

                var score = (int)request.Priority - ((now - request.EnqueuedAt) / AgingSecondsPerClass) + RecentPenalty(request.ResidentId, now);
                if (score < bestScore || (score == bestScore && request.Id < best.Id))
                {
                    best = request;
                    bestScore = score;
                }
            }

            return best;
        }

        private double RecentPenalty(string residentId, double now)
        {
            if (residentId == null || !lastServed.TryGetValue(residentId, out var served))
            {
                return 0;
            }

            var since = now - served;
            return since >= RecentlyServedSeconds || since < 0 ? 0 : RecentlyServedPenalty * (1 - (since / RecentlyServedSeconds));
        }

        private void Send(AiRequest request, double now)
        {
            if (request.ResidentId != null)
            {
                // Residents are few, but forget old entries anyway so the record can't grow without bound.
                foreach (var stale in lastServed.Where(p => now - p.Value >= RecentlyServedSeconds).Select(p => p.Key).ToList())
                {
                    lastServed.Remove(stale);
                }

                lastServed[request.ResidentId] = now;
            }

            request.Attempts++;
            Stats.Sent++;
            Active = request;
            activeSentAt = now;
            activeSuperseded = false;
            var startError = "the request could not be started";
            try
            {
                activeCall = transport.Post(Url, request.Body, TimeoutSeconds);
            }
            catch (Exception e)
            {
                activeCall = null;
                startError = $"the request could not be started: {e.Message}";
            }

            if (activeCall == null)
            {
                ClearActive();
                Stats.TransportErrors++;
                Stats.ConsecutiveTransportFailures++;
                RetryOrGiveUp(request, startError, now);
            }
        }

        private void RetryOrGiveUp(AiRequest request, string error, double now)
        {
            request.LastError = error ?? string.Empty;
            Stats.LastError = $"{request.Description}: {request.LastError}";
            if (request.Attempts < MaxAttempts && SafeIsStillWanted(request) && pending.Count < MaxPending)
            {
                request.NotBefore = now + (RetryDelaySeconds * request.Attempts);
                pending.Add(request);
                return;
            }

            GiveUp(request, request.LastError);
        }

        private void GiveUp(AiRequest request, string reason)
        {
            Stats.GaveUp++;
            try
            {
                request.OnGiveUp(reason);
            }
            catch (Exception)
            {
                // An owner's clean-up failing must not stop the queue; the simulation's own deadline still applies.
            }
        }

        private void ClearActive()
        {
            Active = null;
            activeCall = null;
            activeSuperseded = false;
        }

        private static bool SafeIsStillWanted(AiRequest request)
        {
            try
            {
                return request.IsStillWanted();
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
