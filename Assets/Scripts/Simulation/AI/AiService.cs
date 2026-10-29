using System;
using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation.Configuration;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Social;

namespace TinyCity.Simulation.AI
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

        /// <summary>
        /// Real seconds a conversation line waits for the AI before built-in small talk is used. Measured in real time,
        /// not game time, so AI lines keep coming at 4x and 10x speed (the conversation pauses while it waits).
        /// </summary>
        public const double LineWaitSeconds = 6;

        private enum CheckPhase
        {
            ListModels,
            LoadModel,
        }

        private readonly OllamaSettings settings;
        private readonly IAiTransport transport;
        private readonly Dictionary<string, long> requestedDecisions = new Dictionary<string, long>();
        private readonly Dictionary<string, int> requestedReflections = new Dictionary<string, int>();
        private readonly Dictionary<string, (long lineId, double askedAt)> requestedLines = new Dictionary<string, (long, double)>();
        private readonly List<string> events = new List<string>();
        private IAiCall checkCall;
        private CheckPhase checkPhase;
        private double checkStartedAt;
        private double nextCheckAt;
        private IAiCall listCall;
        private double listStartedAt;
        private bool listWanted;
        private List<string> installedModels = new List<string>();

        public AiService(OllamaSettings settings, IAiTransport transport)
        {
            this.settings = settings ?? new OllamaSettings();
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            Queue = new AiRequestQueue(transport, OllamaProtocol.ChatUrl(this.settings.endpoint), this.settings.timeoutSeconds, 1 + Math.Max(0, this.settings.maxRetries));
            Status = this.settings.enabled ? AiConnectionStatus.Checking : AiConnectionStatus.Disabled;
            StatusDetail = this.settings.enabled ? "looking for Ollama" : "turned off in Settings";
        }

        public AiRequestQueue Queue { get; }

        public TownSimulation Simulation { get; private set; }

        public AiConnectionStatus Status { get; private set; }

        public string StatusDetail { get; private set; }

        public string Model => settings.model;

        public string Endpoint => settings.endpoint;

        public bool Enabled => settings.enabled;

        /// <summary>The models Ollama last said it has, sorted by name; empty until a list has been read.</summary>
        public IReadOnlyList<string> InstalledModels => installedModels;

        /// <summary>Why the last model list could not be read, or null.</summary>
        public string ModelListError { get; private set; }

        /// <summary>True while a model list asked for with <see cref="RefreshModelList"/> is on its way.</summary>
        public bool ListingModels => listWanted || listCall != null;

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

        /// <summary>Asks Ollama which models are installed, even while the AI is turned off, for the Settings window.</summary>
        public void RefreshModelList()
        {
            listWanted = true;
        }

        /// <summary>Turns the AI on or off while the game runs. Turning it off drops every waiting request.</summary>
        public void SetEnabled(bool enabled)
        {
            if (settings.enabled == enabled)
            {
                return;
            }

            settings.enabled = enabled;
            Restart(enabled ? "AI turned on in Settings" : "AI turned off in Settings");
            StatusDetail = enabled ? "looking for Ollama" : "turned off in Settings";
        }

        /// <summary>Switches to another Ollama model, checking and loading it before it is used.</summary>
        public void UseModel(string model)
        {
            model = model?.Trim();
            if (string.IsNullOrEmpty(model) || model.Length > PlayerPreferences.MaxModelNameLength || model == settings.model)
            {
                return;
            }

            settings.model = model;
            Restart($"switched to {model}");
            StatusDetail = $"switching to {model}";
        }

        private void Restart(string reason)
        {
            checkCall?.Cancel();
            checkCall = null;
            Queue.CancelAll(reason);
            requestedDecisions.Clear();
            requestedReflections.Clear();
            requestedLines.Clear();
            SetSimulationEnabled(false);
            nextCheckAt = 0;
            Status = settings.enabled ? AiConnectionStatus.Checking : AiConnectionStatus.Disabled;
            AddEvent(reason);
        }

        public void Update(double now)
        {
            UpdateModelList(now);
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
                    return "AI: off (Settings). Residents use their built-in routine.";
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

            Remember(names);
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

        private void UpdateModelList(double now)
        {
            if (listCall == null)
            {
                if (!listWanted)
                {
                    return;
                }

                listWanted = false;
                listStartedAt = now;
                try
                {
                    listCall = transport.Get(OllamaProtocol.TagsUrl(Endpoint), CheckTimeoutSeconds);
                }
                catch (Exception e)
                {
                    listCall = null;
                    ModelListError = $"could not contact {Endpoint}: {e.Message}";
                    return;
                }

                if (listCall == null)
                {
                    ModelListError = $"could not contact {Endpoint}";
                }

                return;
            }

            if (!listCall.IsDone)
            {
                if (now - listStartedAt > CheckTimeoutSeconds)
                {
                    listCall.Cancel();
                    listCall = null;
                    ModelListError = $"no reply from {Endpoint} within {CheckTimeoutSeconds:0}s. Is Ollama running?";
                }

                return;
            }

            var call = listCall;
            listCall = null;
            if (!call.Succeeded)
            {
                ModelListError = OllamaProtocol.ReadError(call.ResponseText) ?? call.Error ?? "no reply";
                return;
            }

            if (!OllamaProtocol.TryReadModelNames(call.ResponseText, out var names, out var error))
            {
                ModelListError = error;
                return;
            }

            Remember(names);
        }

        private void Remember(List<string> names)
        {
            ModelListError = null;
            installedModels = names.Distinct().OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
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

            foreach (var session in sim.ActiveConversations.ToList())
            {
                if (!session.AwaitingLine)
                {
                    continue;
                }

                if (!requestedLines.TryGetValue(session.Id, out var asked) || asked.lineId != session.LineRequestId)
                {
                    requestedLines[session.Id] = (session.LineRequestId, now);
                    Queue.Enqueue(LineRequest(sim, session), now);
                }
                else if (now - asked.askedAt > LineWaitSeconds && sim.UseSmallTalk(session.Id, asked.lineId))
                {
                    AddEvent($"{sim.GetResident(session.Lines.Last().SpeakerId)?.FirstName} used small talk: no AI line within {LineWaitSeconds:0}s");
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
                reason =>
                {
                    if (sim.UseSmallTalk(session.Id, lineId))
                    {
                        AddEvent($"{speaker.FirstName} used small talk: {reason}");
                    }
                });
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
