using System;
using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation.Configuration;
using TinyCity.Simulation.Memory;
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

        /// <summary>Tiny City 2: the plan day each resident was last asked to plan, and the day a plan was given up on.</summary>
        private readonly Dictionary<string, int> requestedPlans = new Dictionary<string, int>();
        private readonly Dictionary<string, int> skippedPlans = new Dictionary<string, int>();

        /// <summary>
        /// Tiny City 2's AI budget: a resident's new memories are rated once there are this many to rate in one
        /// request, or once the oldest has waited <see cref="RatingMaxWaitMinutes"/> game minutes.
        /// </summary>
        public const int RatingBatchMin = 4;

        public const int RatingMaxWaitMinutes = 6 * 60;

        /// <summary>Tiny City 2: when an insight request was last sent or given up for each resident (real seconds).</summary>
        private readonly Dictionary<string, double> insightAskedAt = new Dictionary<string, double>();
        private long lastInsightCheckMinute = -1;

        /// <summary>How long after an insight request was sent or given up before the same resident is asked again.</summary>
        public const double InsightRetrySeconds = 300;

        /// <summary>Tiny City 2: the reaction question last sent for each resident.</summary>
        private readonly Dictionary<string, long> requestedReactions = new Dictionary<string, long>();
        private readonly Dictionary<string, long> requestedMeetingTurns = new Dictionary<string, long>();

        /// <summary>Tiny City 2: the building plan question last sent for each resident.</summary>
        private readonly Dictionary<string, long> requestedSketches = new Dictionary<string, long>();
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
        private double gameMinutesPerSecond = 1;
        private double lastNow;
        private double embedPausedUntil;

        public AiService(OllamaSettings settings, IAiTransport transport)
        {
            this.settings = settings ?? new OllamaSettings();
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            Queue = new AiRequestQueue(transport, OllamaProtocol.ChatUrl(this.settings.endpoint), this.settings.timeoutSeconds, 1 + Math.Max(0, this.settings.maxRetries),
                maxConcurrent: this.settings.parallelRequests);
            Status = this.settings.enabled ? AiConnectionStatus.Checking : AiConnectionStatus.Disabled;
            StatusDetail = this.settings.enabled ? "looking for Ollama" : "turned off in Settings";
        }

        public AiRequestQueue Queue { get; }

        public TownSimulation Simulation { get; private set; }

        public AiConnectionStatus Status { get; private set; }

        public string StatusDetail { get; private set; }

        public string Model => settings.model;

        public string EmbeddingModel => settings.embeddingModel ?? string.Empty;

        /// <summary>True while the embedding model is installed in Ollama, so memories can be found by meaning.</summary>
        public bool EmbeddingsAvailable { get; private set; }

        /// <summary>The most memories embedded in one request.</summary>
        public const int EmbedBatch = 16;

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
            requestedPlans.Clear();
            skippedPlans.Clear();
            requestedReactions.Clear();
            requestedMeetingTurns.Clear();
            requestedSketches.Clear();
            insightAskedAt.Clear();
            lastInsightCheckMinute = -1;
            requestedLines.Clear();
            Simulation = simulation;
            if (simulation != null)
            {
                simulation.DecisionWaitMinutes = DecisionWaitMinutesAt(gameMinutesPerSecond);
                simulation.AiDecisionsEnabled = Status == AiConnectionStatus.Connected;
            }
        }

        /// <summary>
        /// Tells the service how fast the game runs (game minutes per real second: 1 at 1x, 4 at 4x), so a resident
        /// waits long enough in real time for the AI's answer. Pausing keeps the last wait.
        /// </summary>
        public void SetGameSpeed(double minutesPerSecond)
        {
            if (minutesPerSecond <= 0)
            {
                return;
            }

            gameMinutesPerSecond = minutesPerSecond;
            if (Simulation != null)
            {
                Simulation.DecisionWaitMinutes = DecisionWaitMinutesAt(minutesPerSecond);
            }
        }

        /// <summary>
        /// Game minutes a resident waits for a decision at this speed: the setting's minutes, or its real seconds
        /// turned into game minutes if that is longer.
        /// </summary>
        public int DecisionWaitMinutesAt(double minutesPerSecond) =>
            Math.Min(OllamaSettings.MaxDecisionWaitMinutes, Math.Max(settings.decisionWaitMinutes, (int)Math.Ceiling(settings.decisionWaitSeconds * Math.Max(0, minutesPerSecond))));

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
            requestedPlans.Clear();
            skippedPlans.Clear();
            requestedReactions.Clear();
            requestedMeetingTurns.Clear();
            requestedSketches.Clear();
            insightAskedAt.Clear();
            lastInsightCheckMinute = -1;
            requestedLines.Clear();
            SetSimulationEnabled(false);
            nextCheckAt = 0;
            Status = settings.enabled ? AiConnectionStatus.Checking : AiConnectionStatus.Disabled;
            AddEvent(reason);
        }

        public void Update(double now)
        {
            lastNow = now;
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
                    if (Simulation?.AiRunsEverything == true)
                    {
                        // Tiny City 2 has nothing to fall back on: an unusable answer is asked again, so say what went wrong.
                        var problem = string.IsNullOrEmpty(s.LastError) ? string.Empty : $" Last unusable answer: {s.LastError}";
                        return $"AI: connected to {Model}. Queue {Queue.PendingCount + Queue.ActiveCount}{latency}. Used {s.Accepted}, unusable {s.InvalidReplies}, given up and asked again {s.GaveUp}.{problem}";
                    }

                    return $"AI: connected to {Model}. Queue {Queue.PendingCount + Queue.ActiveCount}{latency}. Used {s.Accepted}, fell back {s.GaveUp}.";
                case AiConnectionStatus.Disabled:
                    return Simulation?.AiRunsEverything == true
                        ? "AI: off (Settings). The city waits until it's turned on."
                        : "AI: off (Settings). Residents use their built-in routine.";
                default:
                    return Simulation?.AiRunsEverything == true
                        ? $"AI: {Words(Status)} ({StatusDetail}). The city waits until Ollama answers."
                        : $"AI: {Words(Status)} ({StatusDetail}). Residents use their built-in routine meanwhile.";
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
            var embeddings = !string.IsNullOrWhiteSpace(EmbeddingModel) && OllamaProtocol.HasModel(names, EmbeddingModel);
            if (!embeddings && !string.IsNullOrWhiteSpace(EmbeddingModel) && EmbeddingsAvailable != embeddings)
            {
                AddEvent($"{EmbeddingModel} is not installed, so memories are found without their meaning. Run: ollama pull {EmbeddingModel}");
            }

            EmbeddingsAvailable = embeddings;
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

                if (r.Reaction != null && (!requestedReactions.TryGetValue(r.Id, out var askedReaction) || askedReaction != r.Reaction.Id))
                {
                    requestedReactions[r.Id] = r.Reaction.Id;
                    Queue.Enqueue(ReactionRequest(sim, r, r.Reaction), now);
                }

                var turn = sim.MeetingTurnOf(r.Id);
                if (turn != null && sim.Time.MinuteOfDay >= TownSimulation.MeetingTurnsFromMinute
                    && (!requestedMeetingTurns.TryGetValue(r.Id, out var askedTurn) || askedTurn != turn.Id))
                {
                    requestedMeetingTurns[r.Id] = turn.Id;
                    Queue.Enqueue(MeetingRequest(sim, r, turn), now);
                }

                if (r.Sketch != null && (!requestedSketches.TryGetValue(r.Id, out var askedSketch) || askedSketch != r.Sketch.Id))
                {
                    requestedSketches[r.Id] = r.Sketch.Id;
                    Queue.Enqueue(SketchRequest(sim, r, r.Sketch), now);
                }

                if (WaitingForDayPlan(sim, r, now))
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
                else if (!sim.AiRunsEverything && now - asked.askedAt > LineWaitSeconds && sim.UseSmallTalk(session.Id, asked.lineId))
                {
                    AddEvent($"{sim.GetResident(session.Lines.Last().SpeakerId)?.FirstName} used small talk: no AI line within {LineWaitSeconds:0}s");
                }
            }

            RequestInsights(sim, now);
            RequestMemoryWork(sim, now);

            if (requestedLines.Count > sim.ActiveConversations.Count)
            {
                foreach (var finished in requestedLines.Keys.Where(id => sim.GetConversation(id) == null).ToList())
                {
                    requestedLines.Remove(finished);
                }
            }
        }

        /// <summary>
        /// Tiny City 2: a resident starting their day plans it before their first choice. Returns true while that
        /// plan is still being asked for; once it is made, or given up on for today, their choices go ahead.
        /// </summary>
        private bool WaitingForDayPlan(TownSimulation sim, Resident r, double now)
        {
            if (!sim.AiRunsEverything || !sim.NeedsDayPlan(r))
            {
                return false;
            }

            var day = DayPlan.DayFor(sim.Time);
            if (skippedPlans.TryGetValue(r.Id, out var skipped) && skipped == day)
            {
                return false;
            }

            if (!requestedPlans.TryGetValue(r.Id, out var asked) || asked != day)
            {
                requestedPlans[r.Id] = day;
                Queue.Enqueue(DayPlanRequest(sim, r, day), now);
            }

            return true;
        }

        /// <summary>
        /// Background memory work at the lowest priority: one importance rating request per resident with unrated
        /// memories, and one embedding request for the town, each only while none like it is waiting or in flight.
        /// </summary>
        private void RequestMemoryWork(TownSimulation sim, double now)
        {
            // Memory work never holds up a choice or a line: it waits until nothing else is queued or being answered.
            if (Queue.Pending.Concat(Queue.ActiveRequests).Any(q => q.Priority != AiPriority.Flavour))
            {
                return;
            }

            var queued = new HashSet<string>(Queue.Pending.Concat(Queue.ActiveRequests).Select(q => q.Key));
            foreach (var r in sim.Residents)
            {
                if (!r.IsAlive || queued.Contains($"importance:{r.Id}"))
                {
                    continue;
                }

                var batch = sim.Memories.NeedingRating(r.Id).Take(ImportancePrompt.MaxPerRequest).ToList();
                if (batch.Count > 0 && (!sim.AiRunsEverything || batch.Count >= RatingBatchMin
                    || sim.Time.TotalMinutes - batch[0].Time.TotalMinutes >= RatingMaxWaitMinutes))
                {
                    Queue.Enqueue(ImportanceRequest(sim, r, batch), now);
                }
            }

            if (EmbeddingsAvailable && now >= embedPausedUntil && !queued.Contains("embed"))
            {
                var batch = sim.Memories.NeedingEmbedding().Take(EmbedBatch).ToList();
                if (batch.Count > 0)
                {
                    Queue.Enqueue(EmbedRequest(sim, batch), now);
                }
            }
        }

        private AiRequest ImportanceRequest(TownSimulation sim, Resident r, IReadOnlyList<MemoryRecord> batch)
        {
            var ids = batch.Select(m => m.Id).ToList();
            return new AiRequest(
                $"importance:{r.Id}",
                r.Id,
                AiPriority.Flavour,
                $"memory ratings for {r.FirstName}",
                ImportancePrompt.BuildBody(Model, r, batch),
                text =>
                {
                    if (!OllamaProtocol.TryReadChatContent(text, out var content, out var error)
                        || !ImportancePrompt.TryParse(content, ids, out var ratings, out error))
                    {
                        return AiReplyCheck.Invalid(error);
                    }

                    foreach (var pair in ratings)
                    {
                        sim.Memories.RateImportance(pair.Key, pair.Value);
                    }

                    // A memory the AI skipped keeps the game's figure rather than being asked about forever.
                    foreach (var id in ids.Where(id => !ratings.ContainsKey(id)))
                    {
                        var m = sim.Memories.Get(id);
                        if (m != null)
                        {
                            sim.Memories.RateImportance(id, m.Importance);
                        }
                    }

                    return AiReplyCheck.Accepted();
                },
                () => Simulation == sim && r.IsAlive && ids.Any(id => sim.Memories.Get(id)?.ImportanceRated == false),
                reason =>
                {
                    // Keep the game's figure, so a model that can't rate doesn't make the queue ask again and again.
                    foreach (var id in ids)
                    {
                        var m = sim.Memories.Get(id);
                        if (m != null && !m.ImportanceRated)
                        {
                            sim.Memories.RateImportance(id, m.Importance);
                        }
                    }

                    AddEvent($"{r.FirstName}'s memories kept the game's ratings: {reason}");
                });
        }

        private AiRequest EmbedRequest(TownSimulation sim, IReadOnlyList<MemoryRecord> batch)
        {
            var ids = batch.Select(m => m.Id).ToList();
            return new AiRequest(
                "embed",
                null,
                AiPriority.Flavour,
                $"meaning of {ids.Count} memories",
                OllamaProtocol.BuildEmbedBody(EmbeddingModel, batch.Select(m => m.Summary)),
                text =>
                {
                    if (!OllamaProtocol.TryReadEmbeddings(text, ids.Count, out var vectors, out var error))
                    {
                        return AiReplyCheck.Invalid(error);
                    }

                    for (var i = 0; i < ids.Count; i++)
                    {
                        sim.Memories.SetEmbedding(ids[i], vectors[i]);
                    }

                    return AiReplyCheck.Accepted();
                },
                () => Simulation == sim && EmbeddingsAvailable,
                reason =>
                {
                    embedPausedUntil = lastNow + RecheckSeconds;
                    AddEvent($"Embeddings paused for {RecheckSeconds:0}s: {reason}");
                },
                OllamaProtocol.EmbedUrl(Endpoint));
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

                    var outcome = sim.ApplyConversationLine(session.Id, lineId, speakerId, reply.Say, reply.Tone, reply.Topic, reply.End, reply.GiveFood, reply.PricePence, reply.AcceptOffer, reply.GiveMoneyPence, reply.TaxPercent, reply.PayTax, reply.GiveItem, reply.GiveAmount);
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
                        || !DecisionPrompt.TryParse(content, options, out var reply, out error, sim.PlanStepLimit))
                    {
                        return AiReplyCheck.Invalid(error);
                    }

                    var outcome = sim.ApplyDecision(r.Id, decisionId, reply.Action, reply.TargetId, reply.Reason, reply.Say, reply.Then, reply.X, reply.Y, reply.What, reply.PricePence);
                    switch (outcome)
                    {
                        case DecisionOutcome.Applied:
                            AddEvent($"{r.FirstName} chose '{reply.Action}'{(r.PlannedSteps.Count > 0 ? $" then {string.Join(", ", r.PlannedSteps.Select(s => s.ActionId))}" : string.Empty)} ({Queue.Stats.LastLatencySeconds:0.0}s)");
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

        /// <summary>Tiny City 2: once a game minute, asks for insights from anyone whose recent memories add up to enough.</summary>
        private void RequestInsights(TownSimulation sim, double now)
        {
            if (!sim.AiRunsEverything || sim.Time.TotalMinutes == lastInsightCheckMinute)
            {
                return;
            }

            lastInsightCheckMinute = sim.Time.TotalMinutes;
            foreach (var r in sim.Residents)
            {
                if ((insightAskedAt.TryGetValue(r.Id, out var asked) && now - asked < InsightRetrySeconds) || !sim.IsInsightDue(r))
                {
                    continue;
                }

                insightAskedAt[r.Id] = now;
                Queue.Enqueue(InsightRequest(sim, r), now);
            }
        }

        private AiRequest InsightRequest(TownSimulation sim, Resident r)
        {
            var evidence = sim.InsightEvidence(r.Id);
            var ids = evidence.Select(m => m.Id).ToList();
            return new AiRequest(
                $"insight:{r.Id}",
                r.Id,
                AiPriority.Reflection,
                $"insights for {r.FirstName}",
                InsightPrompt.BuildBody(Model, r, evidence),
                text =>
                {
                    if (!OllamaProtocol.TryReadChatContent(text, out var content, out var error)
                        || !InsightPrompt.TryParse(content, evidence, out var insights, out error))
                    {
                        return AiReplyCheck.Invalid(error);
                    }

                    var outcome = sim.ApplyInsights(r.Id, ids, insights);
                    switch (outcome)
                    {
                        case DecisionOutcome.Applied:
                            insightAskedAt.Remove(r.Id);
                            AddEvent($"{r.FirstName} had a realisation");
                            return AiReplyCheck.Accepted();
                        case DecisionOutcome.InvalidText:
                            return AiReplyCheck.Invalid("no usable insight");
                        default:
                            return AiReplyCheck.Stale(outcome.ToString());
                    }
                },
                () => Simulation == sim && r.IsAlive,
                reason =>
                {
                    insightAskedAt[r.Id] = lastNow;
                    AddEvent($"{r.FirstName}'s insights were skipped for now: {reason}");
                });
        }

        private AiRequest SketchRequest(TownSimulation sim, Resident r, Building.PendingSketch sketch)
        {
            return new AiRequest(
                $"sketch:{r.Id}",
                r.Id,
                AiPriority.Decision,
                $"{r.FirstName}'s building plan",
                SketchPrompt.BuildBody(Model, sim, r, sketch),
                text =>
                {
                    if (!OllamaProtocol.TryReadChatContent(text, out var content, out var error)
                        || !SketchPrompt.TryParse(content, out var reply, out error))
                    {
                        return AiReplyCheck.Invalid(error);
                    }

                    var outcome = sim.ApplySketch(r.Id, sketch.Id, reply.Name, reply.Height, reply.Rows, out var planError);
                    if (outcome == DecisionOutcome.InvalidText)
                    {
                        return AiReplyCheck.Invalid(planError);
                    }

                    if (outcome != DecisionOutcome.Applied)
                    {
                        return AiReplyCheck.Stale(outcome.ToString());
                    }

                    AddEvent($"{r.FirstName} planned a {sim.Construction.Of(r.Id)?.Plan.Name}");
                    return AiReplyCheck.Accepted();
                },
                () => Simulation == sim && r.IsAlive && r.Sketch == sketch,
                reason => sim.DropSketch(r.Id, sketch.Id));
        }

        private AiRequest MeetingRequest(TownSimulation sim, Resident r, PendingMeetingTurn turn)
        {
            return new AiRequest(
                $"meeting:{r.Id}",
                r.Id,
                AiPriority.Dialogue,
                $"{r.FirstName}'s turn at the town meeting",
                MeetingPrompt.BuildBody(Model, sim, r),
                text =>
                {
                    if (!OllamaProtocol.TryReadChatContent(text, out var content, out var error)
                        || !MeetingPrompt.TryParse(content, sim, out var reply, out error))
                    {
                        return AiReplyCheck.Invalid(error);
                    }

                    var outcome = sim.ApplyMeetingTurn(r.Id, turn.Id, reply.Say, reply.Votes);
                    if (outcome != DecisionOutcome.Applied)
                    {
                        return AiReplyCheck.Stale(outcome.ToString());
                    }

                    AddEvent($"{r.FirstName} spoke at the town meeting");
                    return AiReplyCheck.Accepted();
                },
                () => Simulation == sim && r.IsAlive && sim.MeetingTurnOf(r.Id) == turn,
                reason => sim.DropMeetingTurn(r.Id, turn.Id));
        }

        private AiRequest ReactionRequest(TownSimulation sim, Resident r, PendingReaction reaction)
        {
            var other = sim.GetResident(reaction.OtherId);
            return new AiRequest(
                $"react:{r.Id}",
                r.Id,
                AiPriority.Dialogue,
                $"{r.FirstName} noticed {other?.FirstName}",
                ReactionPrompt.BuildBody(Model, sim, r, reaction),
                text =>
                {
                    if (!OllamaProtocol.TryReadChatContent(text, out var content, out var error)
                        || !ReactionPrompt.TryParse(content, out var reply, out error))
                    {
                        return AiReplyCheck.Invalid(error);
                    }

                    var outcome = sim.ApplyReaction(r.Id, reaction.Id, reply.GoOver, reply.Reason);
                    if (outcome != DecisionOutcome.Applied)
                    {
                        return AiReplyCheck.Stale(outcome.ToString());
                    }

                    if (reply.GoOver)
                    {
                        AddEvent($"{r.FirstName} noticed {other?.FirstName} and went over to talk");
                    }

                    return AiReplyCheck.Accepted();
                },
                () => Simulation == sim && r.IsAlive && r.Reaction == reaction,
                reason => sim.DropReaction(r.Id, reaction.Id));
        }

        private AiRequest DayPlanRequest(TownSimulation sim, Resident r, int day)
        {
            return new AiRequest(
                $"dayplan:{r.Id}",
                r.Id,
                AiPriority.Decision,
                $"day {day} plan for {r.FirstName}",
                DayPlanPrompt.BuildBody(Model, sim, r, day),
                text =>
                {
                    if (!OllamaProtocol.TryReadChatContent(text, out var content, out var error)
                        || !DayPlanPrompt.TryParse(content, out var entries, out var role, out error))
                    {
                        return AiReplyCheck.Invalid(error);
                    }

                    var outcome = sim.ApplyDayPlan(r.Id, day, entries, role);
                    switch (outcome)
                    {
                        case DecisionOutcome.Applied:
                            AddEvent($"{r.FirstName} planned the day: {r.DayPlan.Entries.Count} parts ({Queue.Stats.LastLatencySeconds:0.0}s)");
                            return AiReplyCheck.Accepted();
                        case DecisionOutcome.InvalidText:
                            return AiReplyCheck.Invalid("the plan had nothing usable in it");
                        default:
                            return AiReplyCheck.Stale(outcome.ToString());
                    }
                },
                () => Simulation == sim && r.IsAlive && DayPlan.DayFor(sim.Time) == day && r.DayPlan?.Day != day,
                reason =>
                {
                    skippedPlans[r.Id] = day;
                    AddEvent($"{r.FirstName} goes without a plan today: {reason}");
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
