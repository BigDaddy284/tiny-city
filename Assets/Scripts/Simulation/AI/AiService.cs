using System;
using System.Collections.Generic;
using System.Linq;
using TinyTown.Simulation.Configuration;
using TinyTown.Simulation.Residents;
using TinyTown.Simulation.Social;

namespace TinyTown.Simulation.AI
{
    public enum AiConnectionStatus
    {
        Disabled,
        Checking,
        LoadingModel,
        Connected,
        ModelMissing,
        Offline,
    }

    /// <summary>
    /// Connects the town to Ollama: checks the service and model, turns AI decisions on only while it works,
    /// asks for free-time decisions and bedtime reflections through the central queue, and hands every answer to
    /// the simulation for validation. Whenever anything goes wrong the built-in routine carries on.
    /// Call <see cref="Update"/> once per frame with a real-time clock in seconds.
    /// </summary>
    public sealed class AiService
    {
        /// <summary>How often to look for Ollama again while it is offline or the model is missing.</summary>
        public const double RecheckSeconds = 30;

        public const double CheckTimeoutSeconds = 10;

        /// <summary>Consecutive connection errors after which Ollama is treated as offline.</summary>
        public const int OfflineAfterFailures = 3;

        public const int MaxEvents = 20;

        private enum CheckPhase
        {
            ListModels,
            LoadModel,
        }

        private readonly OllamaSettings settings;
        private readonly IAiTransport transport;
        private readonly Dictionary<string, long> requestedDecisions = new Dictionary<string, long>();
        private readonly Dictionary<string, int> requestedReflections = new Dictionary<string, int>();
        private readonly Dictionary<string, long> requestedLines = new Dictionary<string, long>();
        private readonly List<string> events = new List<string>();
        private IAiCall checkCall;
        private CheckPhase checkPhase;
        private double checkStartedAt;
        private double nextCheckAt;

        public AiService(OllamaSettings settings, IAiTransport transport)
        {
            this.settings = settings ?? new OllamaSettings();
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            Queue = new AiRequestQueue(transport, OllamaProtocol.ChatUrl(this.settings.endpoint), this.settings.timeoutSeconds, 1 + Math.Max(0, this.settings.maxRetries));
            Status = this.settings.enabled ? AiConnectionStatus.Checking : AiConnectionStatus.Disabled;
            StatusDetail = this.settings.enabled ? "looking for Ollama" : "turned off in settings.json";
        }

        public AiRequestQueue Queue { get; }

        public TownSimulation Simulation { get; private set; }

        public AiConnectionStatus Status { get; private set; }

        public string StatusDetail { get; private set; }

        public string Model => settings.model;

        public string Endpoint => settings.endpoint;

        /// <summary>Recent noteworthy AI events, newest last, for the inspector.</summary>
        public IReadOnlyList<string> RecentEvents => events;

        /// <summary>Receives the same messages as <see cref="RecentEvents"/>, e.g. for the Unity log.</summary>
        public Action<string> Log { get; set; }

        /// <summary>Starts serving a (new or loaded) town. Anything still queued for the previous town is dropped.</summary>
        public void Attach(TownSimulation simulation)
        {
            Queue.CancelAll("a different town was loaded");
            requestedDecisions.Clear();
            requestedReflections.Clear();
            requestedLines.Clear();
            Simulation = simulation;
            if (simulation != null)
            {
                simulation.DecisionWaitMinutes = settings.decisionWaitMinutes;
                simulation.AiDecisionsEnabled = Status == AiConnectionStatus.Connected;
            }
        }

        public void Update(double now)
        {
            if (!settings.enabled)
            {
                Status = AiConnectionStatus.Disabled;
                SetSimulationEnabled(false);
                return;
            }

            UpdateConnection(now);
            SetSimulationEnabled(Status == AiConnectionStatus.Connected);
            if (Status == AiConnectionStatus.Connected && Simulation != null)
            {
                RequestWhatIsNeeded(now);
            }

            Queue.Update(now);
        }

        /// <summary>One line for the inspector.</summary>
        public string Summary()
        {
            var s = Queue.Stats;
            switch (Status)
            {
                case AiConnectionStatus.Connected:
                    var latency = s.LastLatencySeconds > 0 ? $", last answer {s.LastLatencySeconds:0.0}s (avg {s.AverageLatencySeconds:0.0}s)" : string.Empty;
                    return $"AI: connected to {Model}. Queue {Queue.PendingCount + (Queue.Active == null ? 0 : 1)}{latency}. Used {s.Accepted}, fell back {s.GaveUp}.";
                case AiConnectionStatus.Disabled:
                    return "AI: off (settings.json). Residents use their built-in routine.";
                default:
                    return $"AI: {Words(Status)} ({StatusDetail}). Residents use their built-in routine meanwhile.";
            }
        }

        private void UpdateConnection(double now)
        {
            if (checkCall != null)
            {
                var limit = checkPhase == CheckPhase.ListModels ? CheckTimeoutSeconds : settings.timeoutSeconds;
                if (!checkCall.IsDone)
                {
                    if (now - checkStartedAt > limit)
                    {
                        checkCall.Cancel();
                        checkCall = null;
                        GoOffline(checkPhase == CheckPhase.ListModels
                            ? $"no reply from {Endpoint} within {limit:0}s"
                            : $"{Model} took longer than {limit:0}s to load", now);
                    }

                    return;
                }

                var call = checkCall;
                checkCall = null;
                if (checkPhase == CheckPhase.ListModels)
                {
                    FinishModelCheck(call, now);
                }
                else
                {
                    FinishModelLoad(call, now);
                }

                return;
            }

            if (Status == AiConnectionStatus.Connected)
            {
                if (Queue.Stats.ConsecutiveTransportFailures >= OfflineAfterFailures)
                {
                    GoOffline($"lost connection: {Queue.Stats.LastError}", now);
                    SetSimulationEnabled(false);
                    Queue.CancelAll("Ollama stopped answering");
                }

                return;
            }

            if (now >= nextCheckAt)
            {
                StartCheck(CheckPhase.ListModels, now);
            }
        }

        private void StartCheck(CheckPhase phase, double now)
        {
            checkPhase = phase;
            checkStartedAt = now;
            try
            {
                checkCall = phase == CheckPhase.ListModels
                    ? transport.Get(OllamaProtocol.TagsUrl(Endpoint), CheckTimeoutSeconds)
                    : transport.Post(OllamaProtocol.ChatUrl(Endpoint), OllamaProtocol.BuildLoadModelBody(Model), settings.timeoutSeconds);
            }
            catch (Exception e)
            {
                checkCall = null;
                GoOffline($"could not contact {Endpoint}: {e.Message}", now);
                return;
            }

            if (checkCall == null)
            {
                GoOffline($"could not contact {Endpoint}", now);
                return;
            }

            if (phase == CheckPhase.LoadModel)
            {
                Status = AiConnectionStatus.LoadingModel;
                StatusDetail = $"loading {Model}";
            }
            else if (Status != AiConnectionStatus.Offline && Status != AiConnectionStatus.ModelMissing)
            {
                Status = AiConnectionStatus.Checking;
                StatusDetail = $"looking for Ollama at {Endpoint}";
            }
        }

        private void FinishModelCheck(IAiCall call, double now)
        {
            if (!call.Succeeded)
            {
                GoOffline(OllamaProtocol.ReadError(call.ResponseText) ?? call.Error ?? "no reply", now);
                return;
            }

            if (!OllamaProtocol.TryReadModelNames(call.ResponseText, out var names, out var error))
            {
                GoOffline(error, now);
                return;
            }

            if (!OllamaProtocol.HasModel(names, Model))
            {
                if (Status != AiConnectionStatus.ModelMissing)
                {
                    AddEvent($"Ollama is running but {Model} is not installed. Run: ollama pull {Model}");
                }

                Status = AiConnectionStatus.ModelMissing;
                StatusDetail = $"{Model} is not installed; run 'ollama pull {Model}'";
                nextCheckAt = now + RecheckSeconds;
                return;
            }

            StartCheck(CheckPhase.LoadModel, now);
        }

        private void FinishModelLoad(IAiCall call, double now)
        {
            if (!call.Succeeded)
            {
                GoOffline(OllamaProtocol.ReadError(call.ResponseText) ?? call.Error ?? $"{Model} failed to load", now);
                return;
            }

            Status = AiConnectionStatus.Connected;
            StatusDetail = $"{Model} ready";
            Queue.ResetFailureCount();
            AddEvent($"Connected to Ollama ({Model}) in {now - checkStartedAt:0.0}s");
        }

        private void GoOffline(string reason, double now)
        {
            if (Status != AiConnectionStatus.Offline || StatusDetail != reason)
            {
                AddEvent($"Ollama unavailable: {reason}. Using built-in routines.");
            }

            Status = AiConnectionStatus.Offline;
            StatusDetail = reason;
            nextCheckAt = now + RecheckSeconds;
        }

        private void RequestWhatIsNeeded(double now)
        {
            var sim = Simulation;
            foreach (var r in sim.Residents)
            {
                if (!r.IsAlive)
                {
                    continue;
                }

                if (r.State == ResidentState.Deciding
                    && (!requestedDecisions.TryGetValue(r.Id, out var asked) || asked != r.PendingDecisionId))
                {
                    requestedDecisions[r.Id] = r.PendingDecisionId;
                    Queue.Enqueue(DecisionRequest(sim, r), now);
                }

                if (sim.IsReflectionDue(r))
                {
                    var day = TownSimulation.ReflectionDayFor(sim.Time);
                    if (!requestedReflections.TryGetValue(r.Id, out var askedDay) || askedDay != day)
                    {
                        requestedReflections[r.Id] = day;
                        Queue.Enqueue(ReflectionRequest(sim, r, day), now);
                    }
                }
            }

            foreach (var session in sim.ActiveConversations)
            {
                if (session.AwaitingLine
                    && (!requestedLines.TryGetValue(session.Id, out var askedLine) || askedLine != session.LineRequestId))
                {
                    requestedLines[session.Id] = session.LineRequestId;
                    Queue.Enqueue(LineRequest(sim, session), now);
                }
            }

            if (requestedLines.Count > sim.ActiveConversations.Count)
            {
                foreach (var finished in requestedLines.Keys.Where(id => sim.GetConversation(id) == null).ToList())
                {
                    requestedLines.Remove(finished);
                }
            }
        }

        private AiRequest LineRequest(TownSimulation sim, ConversationSession session)
        {
            var lineId = session.LineRequestId;
            var speakerId = session.SpeakerId;
            var speaker = sim.GetResident(speakerId);
            return new AiRequest(
                $"dialogue:{session.Id}",
                speakerId,
                AiPriority.Dialogue,
                $"a line for {speaker.FirstName}",
                DialoguePrompt.BuildBody(Model, sim, session),
                text =>
                {
                    if (!OllamaProtocol.TryReadChatContent(text, out var content, out var error)
                        || !DialoguePrompt.TryParse(content, out var reply, out error))
                    {
                        return AiReplyCheck.Invalid(error);
                    }

                    var outcome = sim.ApplyConversationLine(session.Id, lineId, speakerId, reply.Say, reply.Tone, reply.Topic, reply.End);
                    switch (outcome)
                    {
                        case DecisionOutcome.Applied:
                            return AiReplyCheck.Accepted();
                        case DecisionOutcome.InvalidText:
                            return AiReplyCheck.Invalid("the line was empty");
                        default:
                            return AiReplyCheck.Stale(outcome.ToString());
                    }
                },
                () => Simulation == sim && sim.GetConversation(session.Id) == session && session.AwaitingLine && session.LineRequestId == lineId,
                reason => AddEvent($"{speaker.FirstName} used small talk: {reason}"));
        }

        private AiRequest DecisionRequest(TownSimulation sim, Resident r)
        {
            var decisionId = r.PendingDecisionId;
            var options = sim.FreeTimeOptionsFor(r);
            return new AiRequest(
                $"decision:{r.Id}",
                r.Id,
                AiPriority.Decision,
                $"free-time decision for {r.FirstName}",
                DecisionPrompt.BuildBody(Model, sim, r, options),
                text =>
                {
                    if (!OllamaProtocol.TryReadChatContent(text, out var content, out var error)
                        || !DecisionPrompt.TryParse(content, options, out var reply, out error))
                    {
                        return AiReplyCheck.Invalid(error);
                    }

                    var outcome = sim.ApplyDecision(r.Id, decisionId, reply.Action, reply.TargetId, reply.Reason, reply.Say);
                    switch (outcome)
                    {
                        case DecisionOutcome.Applied:
                            AddEvent($"{r.FirstName} chose '{reply.Action}' ({Queue.Stats.LastLatencySeconds:0.0}s)");
                            return AiReplyCheck.Accepted();
                        case DecisionOutcome.Stale:
                        case DecisionOutcome.NotAlive:
                        case DecisionOutcome.UnknownResident:
                            return AiReplyCheck.Stale(outcome.ToString());
                        default:
                            return AiReplyCheck.Invalid($"chose '{reply.Action}' at '{reply.TargetId}', which isn't allowed ({outcome})");
                    }
                },
                () => Simulation == sim && r.IsAlive && r.State == ResidentState.Deciding && r.PendingDecisionId == decisionId,
                reason =>
                {
                    if (sim.AbandonDecision(r.Id, decisionId, "AI answer unusable"))
                    {
                        AddEvent($"{r.FirstName} used the built-in routine: {reason}");
                    }
                });
        }

        private AiRequest ReflectionRequest(TownSimulation sim, Resident r, int day)
        {
            return new AiRequest(
                $"reflection:{r.Id}",
                r.Id,
                AiPriority.Reflection,
                $"day {day} reflection for {r.FirstName}",
                ReflectionPrompt.BuildBody(Model, sim, r, day),
                text =>
                {
                    if (!OllamaProtocol.TryReadChatContent(text, out var content, out var error)
                        || !ReflectionPrompt.TryParse(content, out var reply, out error))
                    {
                        return AiReplyCheck.Invalid(error);
                    }

                    var outcome = sim.ApplyReflection(r.Id, day, reply.Summary, reply.Importance);
                    switch (outcome)
                    {
                        case DecisionOutcome.Applied:
                            AddEvent($"{r.FirstName} reflected on day {day}");
                            return AiReplyCheck.Accepted();
                        case DecisionOutcome.InvalidText:
                            return AiReplyCheck.Invalid("the reflection was empty");
                        default:
                            return AiReplyCheck.Stale(outcome.ToString());
                    }
                },
                () => Simulation == sim && r.IsAlive && r.LastReflectionDay < day,
                reason => AddEvent($"{r.FirstName}'s day {day} reflection was skipped: {reason}"));
        }

        private void SetSimulationEnabled(bool enabled)
        {
            if (Simulation != null)
            {
                Simulation.AiDecisionsEnabled = enabled;
            }
        }

        private void AddEvent(string text)
        {
            var stamped = Simulation == null ? text : $"{Simulation.Time.ClockText} {text}";
            events.Add(stamped);
            if (events.Count > MaxEvents)
            {
                events.RemoveAt(0);
            }

            Log?.Invoke(text);
        }

        private static string Words(AiConnectionStatus status)
        {
            switch (status)
            {
                case AiConnectionStatus.LoadingModel:
                    return "loading model";
                case AiConnectionStatus.ModelMissing:
                    return "model missing";
                default:
                    return status.ToString().ToLowerInvariant();
            }
        }
    }
}
