using System;
using System.Collections.Generic;
using System.Linq;
using TinyCity.Simulation;
using TinyCity.Simulation.Activities;
using TinyCity.Simulation.Economy;
using TinyCity.Simulation.Health;
using TinyCity.Simulation.History;
using TinyCity.Simulation.Memory;
using TinyCity.Simulation.Residents;
using TinyCity.Simulation.Social;
using UnityEngine;

namespace TinyCity.Runtime
{
    /// <summary>
    /// The panel on the right: town controls at the top, the chosen resident in tabs, and saving and AI status at the
    /// bottom. Game facts are shown as plain text; anything the AI wrote sits in a tinted box and says so.
    /// </summary>
    internal sealed class Inspector
    {
        public const float DefaultWidth = 380f;
        public const float MinWidth = 320f;
        private const float GripWidth = 6f;
        private const string WidthPrefsKey = "TinyCity.SidebarWidth";

        /// <summary>The side panel's width: drag its left edge to change it (owner, 5 November); kept in PlayerPrefs.</summary>
        public static float Width { get; private set; } = DefaultWidth;

        private static bool widthLoaded;
        private bool resizing;
        private const int SearchMatchesShown = 6;
        private const float PeopleListHeight = 180f;
        private const int GridColumns = 2;
        private const string SearchControl = "inspector-people-search";
        private const int TimelineShown = 40;

        private static readonly string[] TabNames = { "Now", "People", "Memories", "Work", "Timeline" };
        private static readonly string[] FilterNames = { "Everyone", "Living", "Died" };
        private static readonly string[] OrderNames = { "First Name", "Surname", "Oldest", "Youngest" };
        private static readonly string[] OrderNamesV2 = { "First Name", "Surname" };
        private const int TradesShown = 20;
        private static readonly string[] TabNamesV2 = { "Now", "People", "Memories", "Things", "Timeline" };

        private readonly GameController game;
        private GuiStyles styles;
        private int tab;
        private Vector2 scroll;
        private bool peopleOpen;
        private bool expanded;
        private Vector2 gridScroll;
        private PeopleFilter peopleFilter;
        private PeopleOrder peopleOrder;
        private Vector2 peopleScroll;
        private string search = string.Empty;
        private GUIStyle countStyle;
        private GUISkin countSkin;
        private (string residentId, long minute, string text) nextPlan;
        private List<TimelineEntry> timeline = new List<TimelineEntry>();
        private (string residentId, long minute, long history, TownSimulation sim) timelineFor;

        public Inspector(GameController game)
        {
            this.game = game;
        }

        private TownSimulation Simulation => game.Simulation;

        private static float MaxWidth => Mathf.Max(MinWidth, Screen.width * 0.75f);

        /// <summary>Drags the panel's left edge: press on it, move, let go. The width is remembered between runs.</summary>
        private void HandleResize()
        {
            if (!widthLoaded)
            {
                widthLoaded = true;
                Width = PlayerPrefs.GetFloat(WidthPrefsKey, DefaultWidth);
            }

            Width = Mathf.Clamp(Width, MinWidth, MaxWidth);
            var grip = new Rect(Screen.width - Width - (GripWidth / 2f), 0f, GripWidth, Screen.height);
            var e = Event.current;
            switch (e.type)
            {
                case EventType.MouseDown when e.button == 0 && grip.Contains(e.mousePosition):
                    resizing = true;
                    e.Use();
                    break;
                case EventType.MouseDrag when resizing:
                    Width = Mathf.Clamp(Screen.width - e.mousePosition.x, MinWidth, MaxWidth);
                    e.Use();
                    break;
                case EventType.MouseUp when resizing:
                    resizing = false;
                    PlayerPrefs.SetFloat(WidthPrefsKey, Width);
                    PlayerPrefs.Save();
                    e.Use();
                    break;
                case EventType.Repaint:
                    // A thin bar shows where to grab, brighter while dragging.
                    GUI.color = UiTheme.ToColor(UiTheme.Palette.BarBorder);
                    GUI.DrawTexture(new Rect(grip.x + 1f, 0f, resizing ? 4f : 2f, Screen.height), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    break;
            }
        }

        public void Draw(GuiStyles guiStyles)
        {
            styles = guiStyles;
            var r = game.SelectedResident;
            HandleResize();
            var area = new Rect(Screen.width - Width, 0f, Width, Screen.height);
            GUI.color = UiTheme.ToColor(UiTheme.Palette.Panel);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = UiTheme.ToColor(UiTheme.Palette.BarBorder);
            GUI.DrawTexture(new Rect(area.x, 0f, 1f, area.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(area.x + 14f, 10f, Width - 28f, Screen.height - 20f));
            DrawTownControls(r);
            if (expanded)
            {
                DrawPeopleGrid(r);
                DrawFooter();
                GUILayout.EndArea();
                return;
            }

            GUILayout.Space(6f);
            tab = GUILayout.Toolbar(tab, Simulation.FoodMustBeMade ? TabNamesV2 : TabNames);
            scroll = BeginVerticalScroll(scroll);
            GUILayout.Label(Simulation.AiRunsEverything ? $"{r.FullName}{RoleText(r)}" : $"{r.FullName}, {r.AgeYears} ({Words(r.LifeStage.ToString())})", styles.Title);
            if (r.IsNewcomer)
            {
                GUILayout.Label($"Moved to the city on day {r.MovedInDay}", styles.Text);
            }

            if (r.BornDay > 0)
            {
                var parents = string.Join(" and ", r.ParentIds.Select(id => Simulation.GetResident(id)?.FullName).Where(n => n != null));
                GUILayout.Label($"Born in the city on day {r.BornDay}{(parents.Length > 0 ? ", to " + parents : string.Empty)}", styles.Text);
            }

            if (r.IsBaby && r.IsAlive)
            {
                var carer = Simulation.CarerOf(r);
                GUILayout.Label(carer == null ? "A baby, looked after at home" : $"A baby, carried about by {carer.FirstName}", styles.Text);
            }

            if (r.IsHomeless && r.IsAlive)
            {
                GUILayout.Label("Nowhere to live: sleeps on a park bench until a home comes free", styles.Text);
            }

            switch (tab)
            {
                case 1:
                    DrawPeople(r);
                    break;
                case 2:
                    DrawMemories(r);
                    break;
                case 3:
                    DrawWork(r);
                    break;
                case 4:
                    DrawTimeline(r);
                    break;
                default:
                    DrawNow(r);
                    break;
            }

            EndVerticalScroll();
            DrawFooter();
            GUILayout.EndArea();
        }

        private void DrawTownControls(Resident r)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Tiny City 2", styles.Title);
            GUILayout.FlexibleSpace();
            if (GUILayout.Toggle(game.SettingsOpen, "Settings", GUI.skin.button, FitWidth("Settings"), GUILayout.Height(28f)) != game.SettingsOpen)
            {
                game.ToggleSettings();
            }

            if (GUILayout.Button("Hide ▶", FitWidth("Hide ▶"), GUILayout.Height(28f)))
            {
                game.ToggleSidebar();
            }

            GUILayout.EndHorizontal();
            GUILayout.Label($"Town: {game.CurrentTownName}", styles.Small);
            GUILayout.Label($"{Simulation.Time.Weekday}, Day {Simulation.Time.Day}   {Simulation.Time.ClockText}", styles.Text);
            GUILayout.Label(DescribeCalendar(), styles.Small);

            GUILayout.BeginHorizontal();
            for (var i = 0; i < GameController.Speeds.Length; i++)
            {
                var label = GameController.Speeds[i] == 0 ? "Pause" : $"{GameController.Speeds[i]}x";
                if (GUILayout.Toggle(game.SpeedIndex == i, label, GUI.skin.button) && game.SpeedIndex != i)
                {
                    game.SetSpeed(i);
                }
            }

            GUILayout.EndHorizontal();
            if (game.Speed > 0)
            {
                var waiting = game.HoldingForAi
                    ? (game.Ai == null || game.Ai.Status != TinyCity.Simulation.AI.AiConnectionStatus.Connected ? "  Paused: waiting for Ollama." : "  Waiting for the AI to think.")
                    : string.Empty;
                GUILayout.Label($"Running at {game.ActualSpeed:0.0}x of {game.Speed}x.{waiting}", styles.Small);
            }

            // The journal, Town View, then the camera panels: fewer or more, and everyone in view.
            // Each button is as wide as its words, so the five fit the side panel's width.
            var panels = game.Panels;
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(game.JournalOpen, "Town Journal", GUI.skin.button, FitWidth("Town Journal")) != game.JournalOpen)
            {
                game.ToggleJournal();
            }

            if (GUILayout.Toggle(panels.InTownView, "Town View", GUI.skin.button, FitWidth("Town View")) != panels.InTownView)
            {
                game.ToggleTownView();
            }

            GUILayout.EndHorizontal();

            DrawPeopleFinder(r);
        }

        /// <summary>
        /// How many people live in the city (in a small box), a button that opens a list of everyone
        /// (filtered and sorted), and a box to find someone by name, whose matches appear underneath as you type.
        /// Picking someone shows them here and in the active panel.
        /// </summary>
        private void DrawPeopleFinder(Resident r)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Box(Simulation.LivingCount.ToString(), CountStyle(), GUILayout.Width(28f));
            peopleOpen = GUILayout.Toggle(peopleOpen, peopleOpen ? "People ▲" : "People ▼", GUI.skin.button, GUILayout.Width(110f));
            GUI.SetNextControlName(SearchControl);
            search = GUILayout.TextField(search ?? string.Empty, 40);
            if (string.IsNullOrEmpty(search) && GUI.GetNameOfFocusedControl() != SearchControl)
            {
                var box = GUILayoutUtility.GetLastRect();
                GUI.Label(new Rect(box.x + 6f, box.y, box.width - 6f, box.height), "Find a person…", styles.Small);
            }

            GUILayout.EndHorizontal();

            foreach (var match in PeopleList.Search(Simulation.Residents, search, SearchMatchesShown))
            {
                if (GUILayout.Button(Describe(match)))
                {
                    Pick(match);
                    search = string.Empty;
                    GUIUtility.keyboardControl = 0;
                }
            }

            if (!peopleOpen)
            {
                expanded = false;
                return;
            }

            GUILayout.BeginHorizontal();
            peopleFilter = (PeopleFilter)GUILayout.Toolbar((int)peopleFilter, FilterNames);
            expanded = GUILayout.Toggle(expanded, expanded ? "Back" : "Expand", GUI.skin.button, GUILayout.Width(70f));
            GUILayout.EndHorizontal();
            if (Simulation.AiRunsEverything && (int)peopleOrder >= OrderNamesV2.Length)
            {
                peopleOrder = PeopleOrder.FirstName;
            }

            peopleOrder = (PeopleOrder)GUILayout.Toolbar((int)peopleOrder, Simulation.AiRunsEverything ? OrderNamesV2 : OrderNames);
            if (expanded)
            {
                return;
            }

            peopleScroll = BeginVerticalScroll(peopleScroll, GUILayout.Height(PeopleListHeight));
            var shown = PeopleList.Of(Simulation.Residents, peopleFilter, peopleOrder);
            if (shown.Count == 0)
            {
                GUILayout.Label("Nobody here.", styles.Small);
            }

            foreach (var other in shown)
            {
                if (GUILayout.Toggle(other == r, Describe(other), GUI.skin.button) && other != r)
                {
                    Pick(other);
                }
            }

            EndVerticalScroll();
        }

        /// <summary>
        /// The expanded people view: everyone (as filtered and sorted above, first name A–Z to start with) in a grid
        /// that fills the rest of the side panel instead of the resident's tabs. Picking someone keeps the grid open.
        /// </summary>
        private void DrawPeopleGrid(Resident r)
        {
            GUILayout.Space(4f);
            var shown = PeopleList.Of(Simulation.Residents, peopleFilter, peopleOrder);
            gridScroll = BeginVerticalScroll(gridScroll);
            if (shown.Count == 0)
            {
                GUILayout.Label("Nobody here.", styles.Small);
            }

            var labels = shown.Select(Describe).ToArray();
            var picked = GUILayout.SelectionGrid(shown.IndexOf(r), labels, GridColumns, GUILayout.Height(Mathf.Ceil(labels.Length / (float)GridColumns) * 30f));
            if (picked >= 0 && picked < shown.Count && shown[picked] != r)
            {
                Pick(shown[picked]);
            }

            EndVerticalScroll();
        }

        /// <summary>
        /// The width left for what's inside a scroll view in the side panel once its scroll bar is drawn, so nothing
        /// is wider than the panel and no sideways scroll bar is needed.
        /// </summary>
        private static float ScrollContentWidth()
        {
            var bar = GUI.skin.verticalScrollbar;
            var barWidth = (bar.fixedWidth > 0f ? bar.fixedWidth : 12f) + bar.margin.horizontal;
            return Width - 28f - barWidth - 4f;
        }

        /// <summary>A scroll view that only scrolls up and down: its content is kept to <see cref="ScrollContentWidth"/>.</summary>
        private static Vector2 BeginVerticalScroll(Vector2 position, params GUILayoutOption[] options)
        {
            position = GUILayout.BeginScrollView(position, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, options);
            GUILayout.BeginVertical(GUILayout.Width(ScrollContentWidth()));
            return position;
        }

        private static void EndVerticalScroll()
        {
            GUILayout.EndVertical();
            GUILayout.EndScrollView();
        }

        /// <summary>A box the same size as the − and + buttons, for the number of camera panels.</summary>
        private static GUILayoutOption FitWidth(string text) => GUILayout.Width(GUI.skin.button.CalcSize(new GUIContent(text)).x);

        private GUIStyle CountStyle()
        {
            if (countStyle == null || countSkin != GUI.skin)
            {
                var button = GUI.skin.button;
                countStyle = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, margin = button.margin, padding = button.padding, fontSize = button.fontSize, fixedHeight = button.fixedHeight };
                countSkin = GUI.skin;
            }

            return countStyle;
        }

        private void Pick(Resident other) => game.Select(other, followInActivePanel: true);

        /// <summary>
        /// A person's button: in Tiny City 2 nobody dies, so instead of an age it shows the role they chose, if any
        /// (owner, 5 November).
        /// </summary>
        private string Describe(Resident other) => Simulation.AiRunsEverything
            ? $"{other.FullName}{RoleText(other)}"
            : $"{other.FullName}, {other.AgeYears}{(other.IsAlive ? string.Empty : " †")}";

        private static string RoleText(Resident r) => string.IsNullOrEmpty(r.Role) ? string.Empty : $" ({r.Role})";

        private void DrawNow(Resident r)
        {
            GUILayout.Label($"{DescribeLivelihood(r)} · {r.Personality.Describe()}", styles.Small);
            if (!r.IsAlive)
            {
                GUILayout.Label($"Died on day {r.DiedAt?.Day} at {r.DiedAt?.ClockText}, of {r.CauseOfDeath}. Remembered by the town.", styles.Text);
                return;
            }

            GUILayout.Label($"Health: {r.Health:0}/{HealthRules.MaxHealthFor(r.AgeYears):0}{DescribeIllness(r)}", styles.Text);
            GUILayout.Label($"Where: {DescribeLocation(r)}", styles.Text);
            GUILayout.Label($"Doing: {DescribeActivity(r)}", styles.Text);

            if (r.State == ResidentState.Deciding)
            {
                if (Simulation.AiRunsEverything)
                {
                    GUILayout.Label(Simulation.NeedsDayPlan(r) ? "Planning their day with the AI; the city waits for them." : "Choosing what to do with the AI; the city waits for them.", styles.Small);
                }
                else
                {
                    var left = Mathf.Max(0, (int)(r.DecisionDeadline - Simulation.Time.TotalMinutes));
                    GUILayout.Label($"Waiting for the AI to suggest something; if nothing comes within {left} game minutes, their routine decides.", styles.Small);
                }
            }
            else if (!string.IsNullOrEmpty(r.Goal))
            {
                if (r.GoalSource == DecisionSource.Ai)
                {
                    GUILayout.Label("Why: chosen with the AI. In their own words (the AI's explanation, not a game fact):", styles.Small);
                    GUILayout.Label(r.Goal, styles.AiNote);
                }

                if (r.PlannedSteps.Count > 0)
                {
                    GUILayout.Label($"Planned next (checked again when it's time): {string.Join(", then ", r.PlannedSteps.Select(DescribePlanStep))}", styles.Small);
                }
                else
                {
                    GUILayout.Label($"Why (game routine): {r.Goal}", styles.Small);
                }
            }

            if (r.Reaction != null)
            {
                GUILayout.Label($"Noticed {r.Reaction.Seen}; asking the AI whether to go over and talk.", styles.Small);
            }

            if (r.DayPlan != null && r.DayPlan.Day == DayPlan.DayFor(Simulation.Time))
            {
                GUILayout.Label("Today's plan, written by the AI (a guide, not a game fact):", styles.Small);
                var now = r.DayPlan.Now(Simulation.Time);
                foreach (var entry in r.DayPlan.Entries)
                {
                    GUILayout.Label($"{(entry == now ? "▶ " : "   ")}{entry.ClockText} {entry.Text}", styles.AiNote);
                }
            }

            if (!Simulation.AiRunsEverything)
            {
                GUILayout.Label($"Next, by routine: {NextPlan(r)}", styles.Small);
            }

            var said = Simulation.Conversations.LastLineBy(r.Id);
            if (said != null)
            {
                if (said.FromAi)
                {
                    GUILayout.Label($"Last said ({said.Time.ClockText}), written by the AI:", styles.Small);
                    GUILayout.Label($"“{said.Text}”", styles.AiNote);
                }
                else
                {
                    GUILayout.Label($"Last said ({said.Time.ClockText}): “{said.Text}”", styles.Small);
                }
            }

            GUILayout.Space(6f);
            for (var i = 0; i < Needs.Count; i++)
            {
                NeedBar((NeedType)i, r.Needs[(NeedType)i]);
            }

            GUILayout.Space(6f);
            GUILayout.Label("City news", styles.Text);
            var history = Simulation.History.Events;
            for (var i = history.Count - 1; i >= Math.Max(0, history.Count - 3); i--)
            {
                GUILayout.Label($"Day {history[i].Time.Day}: {history[i].Summary}", styles.Small);
            }
        }

        private void DrawPeople(Resident r)
        {
            GUILayout.Label(DescribeLoveLife(r), styles.Text);
            GUILayout.Space(4f);
            var strangers = new List<string>();
            var known = Simulation.Residents
                .Where(o => o != r)
                .OrderByDescending(o => Simulation.Relationships.TryGet(r.Id, o.Id, out var rel) && rel.HaveMet ? rel.Friendship : -1)
                .ToList();
            foreach (var other in known)
            {
                if (Simulation.Relationships.TryGet(r.Id, other.Id, out var rel) && rel.HaveMet)
                {
                    GUILayout.Label(DescribeRelationship(r, other), styles.Small);
                }
                else
                {
                    strangers.Add(other.FirstName);
                }
            }

            if (strangers.Count > 0)
            {
                GUILayout.Label($"Hasn't met yet: {string.Join(", ", strangers)}", styles.Small);
            }
        }

        private void DrawMemories(Resident r)
        {
            var memories = Simulation.Memories.ForResident(r.Id).ToList();
            GUILayout.Label($"Remembers ({memories.Count})", styles.Text);
            foreach (var memory in memories.OrderByDescending(m => m.Time.TotalMinutes).ThenByDescending(m => m.Id).Take(12))
            {
                var text = $"Day {memory.Time.Day}: {memory.Summary} (importance {memory.Importance})";
                if (memory.Type == MemoryType.Reflection)
                {
                    GUILayout.Label("Reflection written by the AI: " + text, styles.AiNote);
                }
                else if (memory.Type == MemoryType.Insight)
                {
                    GUILayout.Label("Insight written by the AI: " + text, styles.AiNote);
                }
                else
                {
                    GUILayout.Label(text, styles.Small);
                }
            }

            GUILayout.Space(6f);
            GUILayout.Label("Recent conversations", styles.Text);
            foreach (var chat in Simulation.Conversations.Involving(r.Id).Where(c => c.Kind == ConversationKind.Chat).Reverse().Take(3))
            {
                var others = string.Join(", ", chat.Participants.Where(p => p != r.Id).Select(p => Simulation.GetResident(p)?.FirstName ?? p));
                GUILayout.Label($"Day {chat.Started.Day} {chat.Started.ClockText} with {others}: {chat.Context}", styles.Small);
                foreach (var line in chat.Lines)
                {
                    var speaker = Simulation.GetResident(line.SpeakerId)?.FirstName ?? line.SpeakerId;
                    GUILayout.Label($"   {speaker}{(line.FromAi ? " (AI)" : string.Empty)}: {line.Text}", line.FromAi ? styles.AiNote : styles.Small);
                }
            }
        }

        private void DrawWork(Resident r)
        {
            if (Simulation.FoodMustBeMade)
            {
                DrawThings(r);
                return;
            }

            GUILayout.Label(DescribeLivelihood(r), styles.Text);
            GUILayout.Label(Simulation.FoodMustBeMade
                ? $"Money: {Money.Format(r.MoneyPence)} · food: {r.Food.Describe(Simulation.Time.Day)}"
                : $"Money: {Money.Format(r.MoneyPence)} · food at home for {r.Groceries} meal(s)", styles.Text);
            if (Simulation.FoodMustBeMade)
            {
                GUILayout.Label($"Things: {r.Belongings.Describe()}", styles.Small);
                var project = Simulation.Construction.Of(r.Id);
                if (project != null)
                {
                    GUILayout.Label(project.IsFinished
                        ? $"Built a {project.Plan.Name}"
                        : $"Building a {project.Plan.Name}: {project.Placed}/{project.Plan.Cells.Count} blocks", styles.Small);
                }
                else if (r.Sketch != null)
                {
                    GUILayout.Label("Planning a building…", styles.Small);
                }
            }

            var bills = r.WeeklyRentPence > 0 ? $"Rent {Money.Format(r.WeeklyRentPence)} a week, due Monday morning" : "No rent";
            var income = r.WeeklyIncomePence > 0 ? $" · {r.IncomeLabel} {Money.Format(r.WeeklyIncomePence)} a week" : string.Empty;
            GUILayout.Label(bills + income, styles.Small);
            GUILayout.Space(4f);
            GUILayout.Label("Recent payments", styles.Text);
            foreach (var payment in Simulation.Economy.Ledger.For(r.Id).Reverse().Take(10))
            {
                var sign = payment.AmountPence >= 0 ? "+" : string.Empty;
                GUILayout.Label($"Day {payment.Time.Day} {payment.Time.ClockText}  {sign}{Money.Format(payment.AmountPence)}  {payment.Description}", styles.Small);
            }
        }

        /// <summary>
        /// Tiny City 2's Things tab (owner, 5 November): what the resident is carrying, item by item, then their
        /// trades (food and money changing hands, tax), newest first, and the money in and out.
        /// </summary>
        private void DrawThings(Resident r)
        {
            var day = Simulation.Time.Day;
            GUILayout.Label(string.IsNullOrEmpty(r.Role) ? "No role chosen" : $"Role: {r.Role}", styles.Text);
            GUILayout.Label($"Money: {Money.Format(r.MoneyPence)}", styles.Text);
            GUILayout.Space(4f);
            GUILayout.Label("CARRYING", styles.Section);
            if (r.Food.Total == 0)
            {
                GUILayout.Label("Food: none", styles.Small);
            }

            foreach (var batch in r.Food.Batches)
            {
                var off = batch.GoesOffDay - day;
                var when = off <= 0 ? "goes off today" : off == 1 ? "goes off tomorrow" : $"goes off in {off} days (day {batch.GoesOffDay})";
                GUILayout.Label($"{FoodRules.Portions(batch.Kind, batch.Count)}, {when}", styles.Small);
            }

            var anyMaterial = false;
            foreach (MaterialKind kind in Enum.GetValues(typeof(MaterialKind)))
            {
                var n = r.Belongings.Count(kind);
                if (n > 0)
                {
                    anyMaterial = true;
                    GUILayout.Label($"{Capital(CraftingRules.Name(kind))}: {n}", styles.Small);
                }
            }

            if (!anyMaterial)
            {
                GUILayout.Label("Materials: none", styles.Small);
            }

            var anyTool = false;
            foreach (var (tool, tier, usesLeft) in r.Belongings.Tools)
            {
                anyTool = true;
                GUILayout.Label($"{Capital(CraftingRules.Name(tool, tier))}: {usesLeft} use{(usesLeft == 1 ? string.Empty : "s")} left", styles.Small);
            }

            if (!anyTool)
            {
                GUILayout.Label("Tools: none", styles.Small);
            }

            var project = Simulation.Construction.Of(r.Id);
            if (project != null)
            {
                GUILayout.Label(project.IsFinished
                    ? $"Built a {project.Plan.Name}"
                    : $"Building a {project.Plan.Name}: {project.Placed}/{project.Plan.Cells.Count} blocks", styles.Small);
            }
            else if (r.Sketch != null)
            {
                GUILayout.Label("Planning a building…", styles.Small);
            }

            GUILayout.Space(4f);
            GUILayout.Label("TRADES", styles.Section);
            var trades = Simulation.TradesOf(r.Id).Take(TradesShown).ToList();
            if (trades.Count == 0)
            {
                GUILayout.Label("No trades yet.", styles.Small);
            }

            foreach (var trade in trades)
            {
                GUILayout.Label($"Day {trade.Time.Day} {trade.Time.ClockText}  {trade.Summary}", styles.Small);
            }

            GUILayout.Space(4f);
            GUILayout.Label("MONEY IN AND OUT", styles.Section);
            var payments = Simulation.Economy.Ledger.For(r.Id).Reverse().Take(10).ToList();
            if (payments.Count == 0)
            {
                GUILayout.Label("No money has changed hands yet.", styles.Small);
            }

            foreach (var payment in payments)
            {
                var sign = payment.AmountPence >= 0 ? "+" : string.Empty;
                GUILayout.Label($"Day {payment.Time.Day} {payment.Time.ClockText}  {sign}{Money.Format(payment.AmountPence)}  {payment.Description}", styles.Small);
            }
        }

        private void DrawTimeline(Resident r)
        {
            GUILayout.Label($"Born on day {HealthRules.BirthdayCycleDay(r)} of each {HealthRules.DaysPerYear}-day year · {r.Personality.Describe()}", styles.Small);
            GUILayout.Label($"Hobbies: {(r.Hobbies.Count > 0 ? string.Join(", ", r.Hobbies) : "none")}", styles.Small);
            GUILayout.Label(DescribeLoveLife(r), styles.Small);
            if (!r.IsAlive)
            {
                GUILayout.Label($"Died on day {r.DiedAt?.Day} at {r.DiedAt?.ClockText}, of {r.CauseOfDeath}.", styles.Text);
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Export Life Story"))
            {
                game.ExportLifeStory(r);
            }

            if (GUILayout.Button("In the Journal"))
            {
                game.ShowJournalFor(r);
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(4f);
            GUILayout.Label("Timeline, newest first", styles.Text);
            var timeline = Timeline(r);
            if (timeline.Count == 0)
            {
                GUILayout.Label("Nothing in the city records yet.", styles.Small);
            }

            foreach (var e in Enumerable.Reverse(timeline).Take(TimelineShown))
            {
                var line = $"Day {e.Time.Day} {e.Time.ClockText}: {e.Text}";
                switch (e.Source)
                {
                    case TimelineSource.AiReflection:
                        GUILayout.Label("Reflection written by the AI. " + line, styles.AiNote);
                        break;
                    case TimelineSource.Memory:
                        GUILayout.Label(line + " (their memory)", styles.Small);
                        break;
                    default:
                        GUILayout.Label(line, styles.Small);
                        break;
                }
            }

            if (timeline.Count > TimelineShown)
            {
                GUILayout.Label($"{timeline.Count - TimelineShown} older entries are in the exported life story.", styles.Small);
            }

            GUILayout.Space(4f);
            GUILayout.Label("Recently", styles.Text);
            foreach (var entry in r.Log.Reverse().Take(10))
            {
                GUILayout.Label($"{entry.Time.ClockText}  {entry.Text}", styles.Small);
            }
        }

        private void DrawFooter()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save (F5)"))
            {
                game.Save("manual");
            }

            if (GUILayout.Button("Load (F9)"))
            {
                game.TryLoad();
            }

            if (GUILayout.Button(game.SavesOpen ? "Close Saves (S)" : "Saved Towns (S)"))
            {
                game.ToggleSaves();
            }

            GUILayout.EndHorizontal();
            GUILayout.Label("Click someone to follow them and see who they are · Town View: the whole land · N/B: next/previous resident · drag and wheel: pan and zoom · J: journal · S: saved towns · H: hide this panel · Settings: top right", styles.Small);
            GUILayout.Label(game.Ai.Summary(), styles.Small);
            var lastEvent = game.Ai.RecentEvents.LastOrDefault();
            if (lastEvent != null)
            {
                GUILayout.Label(lastEvent, styles.Small);
            }

            if (!string.IsNullOrEmpty(game.Status))
            {
                GUILayout.Label(game.Status, styles.Small);
            }
        }

        /// <summary>The resident's timeline, worked out again at most once per game minute or when the records change.</summary>
        private List<TimelineEntry> Timeline(Resident r)
        {
            var key = (r.Id, Simulation.Time.TotalMinutes, Simulation.History.NextNumber, Simulation);
            if (!key.Equals(timelineFor))
            {
                timeline = LifeStory.Timeline(Simulation, r.Id);
                timelineFor = key;
            }

            return timeline;
        }

        /// <summary>What the routine would have them do if they stopped now. Worked out at most once per game minute.</summary>
        private string NextPlan(Resident r)
        {
            if (nextPlan.residentId == r.Id && nextPlan.minute == Simulation.Time.TotalMinutes)
            {
                return nextPlan.text;
            }

            var choice = Simulation.Planner.Choose(r, Simulation.Time);
            var text = choice == null ? "take it easy"
                : choice.TargetResident != null ? $"chat with {choice.TargetResident.FirstName}"
                : $"{choice.Activity.Name.ToLowerInvariant()} at the {choice.Target?.Name.ToLowerInvariant()} ({choice.Reason})";
            nextPlan = (r.Id, Simulation.Time.TotalMinutes, text);
            return text;
        }

        private void NeedBar(NeedType need, double value)
        {
            var rect = GUILayoutUtility.GetRect(ScrollContentWidth(), 18f, GUILayout.ExpandWidth(false));
            GUI.Label(new Rect(rect.x, rect.y, 90f, rect.height), need.ToString(), styles.Small);
            var bar = new Rect(rect.x + 90f, rect.y + 3f, rect.width - 130f, rect.height - 6f);
            var p = UiTheme.Palette;
            GUI.color = UiTheme.ToColor(p.Control);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            var fraction = (float)(value / Needs.Max);
            GUI.color = UiTheme.ToColor(value < 20 ? p.Error : value < 50 ? p.Warning : p.Success);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * fraction, bar.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(bar.xMax + 6f, rect.y, 40f, rect.height), $"{value:0}", styles.Small);
        }

        private string DescribeLocation(Resident r)
        {
            var map = Simulation.World.GetMap(r.Location.MapId);
            var at = Simulation.World.Objects.FirstOrDefault(o => o.Location == r.Location);
            var business = Simulation.World.BusinessOnMap(map.Id);
            var name = business == null ? map.Name : $"{map.Name} ({OpenText(business)}, {business.HoursText})";
            return at != null ? $"{name}, at the {at.Name.ToLowerInvariant()}" : name;
        }

        private string OpenText(Business business)
        {
            if (Simulation.Economy.IsOpen(business, Simulation.Time))
            {
                return "open";
            }

            return business.IsOpen(Simulation.Time) ? "closed, nobody at work" : "closed";
        }

        private static string DescribeIllness(Resident r)
        {
            var illness = IllnessCatalog.Get(r.IllnessId);
            if (illness == null)
            {
                return string.Empty;
            }

            var left = r.IllnessMinutesLeft / 60;
            var progress = illness.Serious && !r.IllnessTreated ? "needs the doctor" : $"about {left} h of recovery left (half that resting)";
            return $" · ill with {illness.Name}, {progress}";
        }

        private static string DescribeLivelihood(Resident r)
        {
            if (r.Job != null)
            {
                var hours = $"{r.Job.StartMinuteOfDay / 60:00}:{r.Job.StartMinuteOfDay % 60:00}–{r.Job.EndMinuteOfDay / 60:00}:{r.Job.EndMinuteOfDay % 60:00}";
                var wage = r.Job.HourlyWagePence > 0 ? $", {Money.Format(r.Job.HourlyWagePence)}/hour" : string.Empty;
                return $"{r.Job.Title}, {r.Job.DaysText()} {hours}{wage}";
            }

            return string.IsNullOrEmpty(r.IncomeLabel) ? "No job" : $"No job ({r.IncomeLabel.ToLowerInvariant()})";
        }

        private string DescribePlanStep(PlanStep step)
        {
            if (step.ActionId == FreeTimeOption.RelaxActionId)
            {
                return "take it easy";
            }

            if (!ActivityCatalog.TryGet(step.ActionId, out var activity))
            {
                return step.ActionId;
            }

            if (activity == ActivityCatalog.Chat)
            {
                var partner = Simulation.GetResident(step.TargetId);
                return partner == null ? "a chat" : $"a chat with {partner.FirstName}";
            }

            return Simulation.World.TryGetObject(step.TargetId ?? string.Empty, out var obj)
                ? $"{activity.Name.ToLowerInvariant()} at the {obj.Name.ToLowerInvariant()}"
                : activity.Name.ToLowerInvariant();
        }

        private string DescribeActivity(Resident r)
        {
            ActivityCatalog.TryGet(r.ActivityId, out var activity);
            switch (r.State)
            {
                case ResidentState.Performing:
                    return activity?.Verb ?? "something";
                case ResidentState.Travelling when r.TargetResidentId != null:
                    return $"looking for {Simulation.GetResident(r.TargetResidentId)?.FirstName ?? "someone"} for a chat";
                case ResidentState.Talking:
                    var chat = Simulation.GetConversation(r.ConversationId);
                    var partner = chat == null ? null : Simulation.GetResident(chat.OtherThan(r.Id));
                    var topic = string.IsNullOrEmpty(chat?.Topic) ? string.Empty : $" about {chat.Topic}";
                    return $"chatting with {partner?.FirstName ?? "someone"}{topic}";
                case ResidentState.Travelling:
                    Simulation.World.TryGetObject(r.TargetObjectId, out var target);
                    return $"walking to the {target?.Name.ToLowerInvariant()} to {activity?.Name.ToLowerInvariant()}";
                case ResidentState.Waiting:
                    return "taking it easy";
                case ResidentState.Deciding:
                    return "thinking about what to do";
                default:
                    return "deciding what to do";
            }
        }

        private string DescribeRelationship(Resident r, Resident other)
        {
            if (!Simulation.Relationships.TryGet(r.Id, other.Id, out var rel) || !rel.HaveMet)
            {
                return $"{other.FirstName}: hasn't met yet";
            }

            var heart = Simulation.Relationships.PartnerOf(r.Id) == other.Id ? ", partner"
                : rel.Romance >= RomanceRules.SoftSpot ? $", soft spot ({rel.Romance:0})" : string.Empty;
            return $"{other.FirstName}: {rel.Describe()} (friendship {rel.Friendship:0}, trust {rel.Trust:0}{(rel.Dislike >= 10 ? $", dislike {rel.Dislike:0}" : string.Empty)}{heart}), {rel.Interactions} chat(s)";
        }

        private string DescribeLoveLife(Resident r)
        {
            var couple = Simulation.Relationships.PartnershipOf(r.Id);
            if (couple != null)
            {
                var partner = Simulation.GetResident(couple.OtherThan(r.Id));
                return couple.Stage == RomanceStage.Committed
                    ? $"Love life: committed to {partner?.FirstName} (together since day {couple.Since.Day})"
                    : $"Love life: dating {partner?.FirstName} since day {couple.Since.Day}";
            }

            if (!Simulation.RomanceEnabled)
            {
                return "Love life: romance is switched off in settings";
            }

            return r.OpenToRomance ? "Love life: single" : "Love life: not looking for romance";
        }

        private string DescribeCalendar()
        {
            if (!Simulation.CommunityEventsEnabled)
            {
                return "Community events are switched off in settings";
            }

            var on = Simulation.Planner.CurrentEvent(Simulation.Time);
            if (on != null)
            {
                Simulation.World.TryGetObject(on.PlaceObjectId, out var place);
                return $"On now: {on.Name} at the {place?.Name.ToLowerInvariant()}, until {on.EndMinute / 60:00}:{on.EndMinute % 60:00}";
            }

            var next = Simulation.World.Calendar.Next(Simulation.Time);
            return next == null ? "Nothing on the city calendar" : $"Next: {next.Value.evt.Name}, {next.Value.start.Weekday} (day {next.Value.start.Day}) at {next.Value.start.ClockText}";
        }

        private static string Capital(string text) => string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);

        private static string Words(string pascal) =>
            string.Concat(pascal.Select((c, i) => i > 0 && char.IsUpper(c) ? " " + char.ToLowerInvariant(c) : c.ToString()));
    }
}
