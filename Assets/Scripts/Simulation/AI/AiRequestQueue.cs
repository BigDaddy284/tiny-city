using System;
using System.Collections.Generic;
using System.Linq;

namespace TinyCity.Simulation.AI
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

        /// <summary>How many calls have finished (answered or failed) and count towards the average.</summary>
        public int CompletedCalls => completedCalls;

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
    /// The single place AI requests go. It sends up to <see cref="MaxConcurrent"/> requests at a time (one unless
    /// Ollama is set up to answer several at once), serves higher priorities first while
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

        /// <summary>
        /// Once the model has answered a few times, a request is given up after this many times its average answer
        /// time (but never sooner than <see cref="MinAdaptiveTimeoutSeconds"/>, nor later than the configured
        /// timeout), so one lost reply from a fast model doesn't hold up everyone else for the whole timeout.
        /// </summary>
        public const double AdaptiveTimeoutFactor = 6;

        public const double MinAdaptiveTimeoutSeconds = 10;

        public const int AdaptiveTimeoutAfterCalls = 5;

        /// <summary>A resident whose request was sent this recently waits behind other residents' requests of the same priority.</summary>
        public const double RecentlyServedSeconds = 15;

        /// <summary>How many priority classes worse a just-served resident's request counts, fading over <see cref="RecentlyServedSeconds"/>.</summary>
        public const double RecentlyServedPenalty = 0.75;

        private readonly IAiTransport transport;
        private readonly List<AiRequest> pending = new List<AiRequest>();
        private readonly Dictionary<string, double> lastServed = new Dictionary<string, double>();
        private readonly List<Slot> active = new List<Slot>();
        private long nextId = 1;

        public AiRequestQueue(IAiTransport transport, string url, double timeoutSeconds, int maxAttempts, int maxPending = DefaultMaxPending, int maxConcurrent = 1)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            Url = url;
            TimeoutSeconds = Math.Max(1, timeoutSeconds);
            MaxAttempts = Math.Max(1, maxAttempts);
            MaxPending = Math.Max(1, maxPending);
            MaxConcurrent = Math.Max(1, maxConcurrent);
        }

        /// <summary>The most requests sent and not yet answered at once. Ollama answers them side by side when its OLLAMA_NUM_PARALLEL allows.</summary>
        public int MaxConcurrent { get; }

        public string Url { get; }

        public double TimeoutSeconds { get; }

        public int MaxAttempts { get; }

        public int MaxPending { get; }

        /// <summary>How long the request being answered now may take: see <see cref="AdaptiveTimeoutFactor"/>.</summary>
        public double CurrentTimeoutSeconds => Stats.CompletedCalls < AdaptiveTimeoutAfterCalls
            ? TimeoutSeconds
            : Math.Min(TimeoutSeconds, Math.Max(MinAdaptiveTimeoutSeconds, AdaptiveTimeoutFactor * Stats.AverageLatencySeconds));

        public AiQueueStats Stats { get; } = new AiQueueStats();

        /// <summary>The oldest request being answered now, or null.</summary>
        public AiRequest Active => active.Count == 0 ? null : active[0].Request;

        /// <summary>Every request being answered now, oldest first.</summary>
        public IReadOnlyList<AiRequest> ActiveRequests => active.Select(a => a.Request).ToList();

        public int ActiveCount => active.Count;

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

            foreach (var slot in active.Where(a => a.Request.Key == request.Key))
            {
                slot.Superseded = true;
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
            foreach (var slot in active.ToList())
            {
                UpdateActive(slot, now);
            }

            while (active.Count < MaxConcurrent)
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
            foreach (var slot in active.ToList())
            {
                slot.Call?.Cancel();
                active.Remove(slot);
                GiveUp(slot.Request, reason);
            }

            foreach (var request in pending.ToList())
            {
                GiveUp(request, reason);
            }

            pending.Clear();
        }

        /// <summary>Called after a successful connection check so earlier failures don't count against the service.</summary>
        public void ResetFailureCount() => Stats.ConsecutiveTransportFailures = 0;

        private void UpdateActive(Slot slot, double now)
        {
            var request = slot.Request;
            if (slot.Superseded || !SafeIsStillWanted(request))
            {
                slot.Call.Cancel();
                active.Remove(slot);
                Stats.Stale++;
                return;
            }

            if (!slot.Call.IsDone)
            {
                var limit = CurrentTimeoutSeconds;
                if (now - slot.SentAt > limit)
                {
                    slot.Call.Cancel();
                    active.Remove(slot);
                    Stats.TimedOut++;
                    RetryOrGiveUp(request, $"no answer within {limit:0} seconds", now);
                }

                return;
            }

            var call = slot.Call;
            active.Remove(slot);
            Stats.RecordLatency(Math.Max(0, now - slot.SentAt));
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
            IAiCall call = null;
            var startError = "the request could not be started";
            try
            {
                call = transport.Post(Url, request.Body, TimeoutSeconds);
            }
            catch (Exception e)
            {
                startError = $"the request could not be started: {e.Message}";
            }

            if (call != null)
            {
                active.Add(new Slot { Request = request, Call = call, SentAt = now });
            }
            else
            {
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

        /// <summary>A request on its way to Ollama.</summary>
        private sealed class Slot
        {
            public AiRequest Request;
            public IAiCall Call;
            public double SentAt;
            public bool Superseded;
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
