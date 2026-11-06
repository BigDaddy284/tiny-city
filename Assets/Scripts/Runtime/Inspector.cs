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
        private const float GripWidth = 10f;
        private const string WidthPrefsKey = "TinyCity.SidebarWidth";

        /// <summary>The side panel's width: drag its left edge to change it (owner, 5 November); kept in PlayerPrefs.</summary>
        public static float Width { get; private set; } = DefaultWidth;

        private static bool widthLoaded;
        private bool resizing;
        private const float DefaultListHeight = 180f;
        private const float DefaultFooterHeight = 150f;
        private const float MinSectionHeight = 60f;
        private const float SplitterHeight = 12f;
        private const string ListHeightPrefsKey = "TinyCity.ListHeight";
        private const string FooterHeightPrefsKey = "TinyCity.FooterHeight";

        /// <summary>How tall the People and Roles lists and the bottom section are: drag the bars between them (owner, 5 November); kept in PlayerPrefs.</summary>
        private static float listHeight = DefaultListHeight;
        private static float footerHeight = DefaultFooterHeight;
        private static bool heightsLoaded;
        private Vector2 footerScroll;
        private const int TimelineShown = 40;

        private const int TradesShown = 20;
        private static readonly string[] TabNames = { "Now", "People", "Memories", "Things", "Timeline" };

        private readonly GameController game;
        private GuiStyles styles;
        private int tab;
        private Vector2 scroll;
        private bool peopleOpen;
        private bool rolesOpen;
        private Vector2 rolesScroll;
        private Vector2 peopleScroll;
        private GUIStyle countStyle;
        private GUISkin countSkin;
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

            if (!heightsLoaded)
            {
                heightsLoaded = true;
                listHeight = PlayerPrefs.GetFloat(ListHeightPrefsKey, DefaultListHeight);
                footerHeight = PlayerPrefs.GetFloat(FooterHeightPrefsKey, DefaultFooterHeight);
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
            }
        }

        /// <summary>A thin bar down the left edge, with a handle halfway down to show where to grab, brighter while dragging.</summary>
        private void DrawGrip()
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            var x = Screen.width - Width;
            var hover = new Rect(x - (GripWidth / 2f), 0f, GripWidth, Screen.height).Contains(Event.current.mousePosition);
            GUI.color = UiTheme.ToColor(UiTheme.Palette.BarBorder);
            GUI.DrawTexture(new Rect(x - 1f, 0f, resizing ? 4f : 2f, Screen.height), Texture2D.whiteTexture);
            DrawHandle(new Rect(x - 4f, (Screen.height / 2f) - 30f, 8f, 60f), vertical: true, active: resizing || hover);
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
            DrawGrip();

            GUILayout.BeginArea(new Rect(area.x + 14f, 10f, Width - 28f, Screen.height - 20f));
            DrawTownControls(r);
            GUILayout.Space(6f);
            tab = GUILayout.Toolbar(tab, TabNames, Fitted(TabNames, (Width - 28f) / TabNames.Length - GUI.skin.button.margin.horizontal));
            scroll = BeginVerticalScroll(scroll);
            // On the Now tab the name, who they are, where and what they're doing sit on the left, with their health
            // and needs as bars beside them (owner, 5 November).
            var columns = tab == 0 && r.IsAlive;
            var leftWidth = Mathf.Floor(ScrollContentWidth() * 0.55f);
            if (columns)
            {
                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical(GUILayout.Width(leftWidth));
            }

            GUILayout.Label($"{r.FullName}{RoleText(r)}", styles.Title);
            if (columns)
            {
                DrawNowSummary(r);
                GUILayout.EndVertical();
                GUILayout.Space(8f);
                GUILayout.BeginVertical();
                var barsWidth = ScrollContentWidth() - leftWidth - 12f;
                Bar("Health", r.Health, HealthRules.MaxHealth, barsWidth);
                for (var i = 0; i < Needs.Count; i++)
                {
                    Bar(((NeedType)i).ToString(), r.Needs[(NeedType)i], Needs.Max, barsWidth);
                }

                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
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
                    DrawThings(r);
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
            // The title, then the journal, settings and hide buttons (owner, 5 November: no Town View button; a
            // middle click lets go of whoever the camera follows).
            GUILayout.BeginHorizontal();
            GUILayout.Label("Tiny City 2", styles.Title);
            GUILayout.FlexibleSpace();
            if (GUILayout.Toggle(game.JournalOpen, "Journal", GUI.skin.button, FitWidth("Journal"), GUILayout.Height(28f)) != game.JournalOpen)
            {
                game.ToggleJournal();
            }

            if (GUILayout.Toggle(game.SettingsOpen, "Settings", GUI.skin.button, FitWidth("Settings"), GUILayout.Height(28f)) != game.SettingsOpen)
            {
                game.ToggleSettings();
            }

            if (GUILayout.Button("Hide ▶", FitWidth("Hide ▶"), GUILayout.Height(28f)))
            {
                game.ToggleSidebar();
            }

            GUILayout.EndHorizontal();
            GUILayout.Label($"Town: {game.CurrentTownName} · {Simulation.LivingCount} people", styles.Small);
            GUILayout.Label($"{Simulation.Time.Weekday}, Day {Simulation.Time.Day}   {Simulation.Time.ClockText}", styles.Text);
            GUILayout.Label(DescribeMeeting(), styles.Small);

            // The speeds, then the People and Roles drop-downs, all the same width on one line.
            var hasRoles = Simulation.Brief.Roles.Count > 0;
            var count = GameController.Speeds.Length + (hasRoles ? 2 : 1);
            var gap = GUI.skin.button.margin.horizontal;
            var eachWidth = Mathf.Max(20f, ((Width - 28f) / count) - gap);
            var each = GUILayout.Width(eachWidth);
            var row = Fitted(RowLabels, eachWidth);
            GUILayout.BeginHorizontal();
            for (var i = 0; i < GameController.Speeds.Length; i++)
            {
                var label = GameController.Speeds[i] == 0 ? "Pause" : $"{GameController.Speeds[i]}x";
                if (GUILayout.Toggle(game.SpeedIndex == i, label, row, each) && game.SpeedIndex != i)
                {
                    game.SetSpeed(i);
                }
            }

            // The people list and the roles list drop down under this row, one at a time.
            var openPeople = GUILayout.Toggle(peopleOpen, peopleOpen ? "People ▲" : "People ▼", row, each);
            if (openPeople != peopleOpen)
            {
                peopleOpen = openPeople;
                rolesOpen = rolesOpen && !peopleOpen;
            }

            if (hasRoles)
            {
                var openRoles = GUILayout.Toggle(rolesOpen, rolesOpen ? "Roles ▲" : "Roles ▼", row, each);
                if (openRoles != rolesOpen)
                {
                    rolesOpen = openRoles;
                    peopleOpen = peopleOpen && !rolesOpen;
                }
            }
            else
            {
                rolesOpen = false;
            }

            GUILayout.EndHorizontal();
            if (game.Speed > 0)
            {
                var thinking = Simulation.Residents.Count(x => x.IsAlive && x.State == ResidentState.Deciding);
                var waiting = game.HoldingForAi
                    ? "  Paused: waiting for Ollama."
                    : thinking > 0 ? $"  {thinking} thinking." : string.Empty;
                GUILayout.Label($"Running at {game.ActualSpeed:0.0}x of {game.Speed}x.{waiting}", styles.Small);
            }

            DrawPeopleFinder(r);
            if (rolesOpen)
            {
                DrawRoles(r);
            }
        }

        /// <summary>
        /// Tiny City 2: every role on offer with who has taken it up (or nobody), and the role changes waiting for
        /// the next town meeting. Picking a name shows that person.
        /// </summary>
        private void DrawRoles(Resident r)
        {
            rolesScroll = BeginVerticalScroll(rolesScroll, GUILayout.Height(listHeight));

            // Owner, 6 November: two columns, the first half of the roles on the left and the rest on the right.
            var roles = Simulation.Brief.Roles.ToList();
            var perColumn = (roles.Count + 1) / 2;
            var columnWidth = (ScrollContentWidth() - 6f) / 2f;
            GUILayout.BeginHorizontal();
            for (var column = 0; column < 2; column++)
            {
                GUILayout.BeginVertical(GUILayout.Width(columnWidth));
                foreach (var role in roles.Skip(column * perColumn).Take(perColumn))
                {
                    var holders = Simulation.Residents.Where(o => o.IsAlive && o.Role == role.Name).OrderBy(o => o.FirstName, StringComparer.Ordinal).ToList();
                    GUILayout.Label(Capital(role.Name), styles.Text);
                    if (holders.Count == 0)
                    {
                        GUILayout.Label("   nobody yet", styles.Small);
                    }

                    foreach (var holder in holders)
                    {
                        if (GUILayout.Toggle(holder == r, holder.FullName, GUI.skin.button, GUILayout.Width(columnWidth)) && holder != r)
                        {
                            Pick(holder);
                        }
                    }
                }

                GUILayout.EndVertical();
                if (column == 0)
                {
                    GUILayout.Space(6f);
                }
            }

            GUILayout.EndHorizontal();

            var without = Simulation.Residents.Count(o => o.IsAlive && o.Role == null);
            GUILayout.Label($"No role: {without}", styles.Small);
            if (Simulation.RoleRequests.Count > 0)
            {
                GUILayout.Label("Asked to change at the next meeting:", styles.Small);
                foreach (var request in Simulation.RoleRequests.OrderBy(o => o.Key, StringComparer.Ordinal))
                {
                    var who = Simulation.GetResident(request.Key);
                    if (who != null)
                    {
                        GUILayout.Label(request.Value == TownSimulation.RoleNone ? $"   {who.FullName}: give up being the {who.Role}" : $"   {who.FullName}: {who.Role ?? "no role"} to {request.Value}", styles.Small);
                    }
                }
            }

            EndVerticalScroll();
            ListSplitter();
        }

        private string DescribeMeeting()
        {
            if (Simulation.Meeting != null && Simulation.Meeting.Day == Simulation.Time.Day)
            {
                return $"Town meeting on now at the meeting circle: {Simulation.Meeting.Attendees.Count} there";
            }

            var day = Simulation.NextMeetingDay(Simulation.Time.Day);
            if (day == Simulation.Time.Day && Simulation.Time.MinuteOfDay >= TownSimulation.MeetingCloseMinute)
            {
                day = Simulation.NextMeetingDay(day + 1);
            }

            return day <= 0 ? "No town meetings" : $"Next town meeting: {TinyCity.Simulation.Time.GameTime.FromDayAndTime(day, 0, 0).Weekday} (day {day}) at 10:00";
        }

        /// <summary>
        /// How many people live in the city (in a small box), a button that opens a list of everyone
        /// (filtered and sorted), and a box to find someone by name, whose matches appear underneath as you type.
        /// Picking someone shows them here and in the active panel.
        /// </summary>
        private void DrawPeopleFinder(Resident r)
        {
            if (peopleOpen)
            {
                DrawNames(r);
            }
        }

        /// <summary>
        /// Tiny City 2's People list (owner, 6 November: no search, filters or orders, "just the peoples names to click
        /// on"): everyone alive by first name, two to a row.
        /// </summary>
        private void DrawNames(Resident r)
        {
            peopleScroll = BeginVerticalScroll(peopleScroll, GUILayout.Height(listHeight));
            var shown = PeopleList.Of(Simulation.Residents, PeopleFilter.Living, PeopleOrder.FirstName);
            if (shown.Count == 0)
            {
                GUILayout.Label("Nobody here.", styles.Small);
            }

            var labels = shown.Select(o => o.FullName).ToArray();
            var picked = GUILayout.SelectionGrid(shown.IndexOf(r), labels, 2, GUILayout.Width(ScrollContentWidth()));
            if (picked >= 0 && picked < shown.Count && shown[picked] != r)
            {
                Pick(shown[picked]);
            }

            EndVerticalScroll();
            ListSplitter();
        }

        /// <summary>The longest words in the speed row, so it is fitted to them.</summary>
        private static readonly string[] RowLabels = { "Pause", "People ▼", "Roles ▼" };

        private static readonly Dictionary<string, GUIStyle> fittedStyles = new Dictionary<string, GUIStyle>();

        /// <summary>
        /// A button style whose text is as big as it can be (up to the skin's own size) while every one of
        /// <paramref name="labels"/> fits in <paramref name="width"/> (owner, 6 November: the text fits the buttons at the
        /// smallest panel width).
        /// </summary>
        private static GUIStyle Fitted(string[] labels, float width)
        {
            var key = $"{GUI.skin.GetHashCode()}|{Mathf.RoundToInt(width)}|{string.Join("|", labels)}";
            if (fittedStyles.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var style = new GUIStyle(GUI.skin.button) { clipping = TextClipping.Clip };
            var size = style.fontSize > 0 ? style.fontSize : 13;
            style.padding = new RectOffset(Mathf.Min(style.padding.left, 4), Mathf.Min(style.padding.right, 4), style.padding.top, style.padding.bottom);
            for (; size > 8; size--)
            {
                style.fontSize = size;
                if (labels.All(o => style.CalcSize(new GUIContent(o)).x <= width))
                {
                    break;
                }
            }

            style.fontSize = size;
            if (fittedStyles.Count > 64)
            {
                fittedStyles.Clear();
            }

            fittedStyles[key] = style;
            return style;
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

        /// <summary>The bar under the People or Roles list: drag it to make the list taller or shorter.</summary>
        private static void ListSplitter()
        {
            if (Splitter(ref listHeight, MinSectionHeight, Mathf.Max(MinSectionHeight, Screen.height * 0.7f), upward: false))
            {
                PlayerPrefs.SetFloat(ListHeightPrefsKey, listHeight);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// A bar across the panel with a handle in the middle: press on it and drag up or down to change
        /// <paramref name="height"/> (the section above it, or with <paramref name="upward"/> the one below). True when let go.
        /// </summary>
        private static bool Splitter(ref float height, float min, float max, bool upward)
        {
            var rect = GUILayoutUtility.GetRect(1f, SplitterHeight, GUILayout.ExpandWidth(true));
            var id = GUIUtility.GetControlID(FocusType.Passive);
            var e = Event.current;
            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown when e.button == 0 && rect.Contains(e.mousePosition):
                    GUIUtility.hotControl = id;
                    e.Use();
                    break;
                case EventType.MouseDrag when GUIUtility.hotControl == id:
                    height = Mathf.Clamp(height + (upward ? -e.delta.y : e.delta.y), min, max);
                    e.Use();
                    break;
                case EventType.MouseUp when GUIUtility.hotControl == id:
                    GUIUtility.hotControl = 0;
                    e.Use();
                    return true;
                case EventType.Repaint:
                    var active = GUIUtility.hotControl == id;
                    GUI.color = UiTheme.ToColor(UiTheme.Palette.BarBorder);
                    GUI.DrawTexture(new Rect(rect.x, rect.center.y - 0.5f, rect.width, 1f), Texture2D.whiteTexture);
                    DrawHandle(new Rect(rect.center.x - 30f, rect.y + 2f, 60f, rect.height - 4f), vertical: false, active: active || rect.Contains(e.mousePosition));
                    break;
            }

            height = Mathf.Clamp(height, min, max);
            return false;
        }

        /// <summary>A rounded-looking grab handle: a filled bar with three grip lines across it.</summary>
        private static void DrawHandle(Rect rect, bool vertical, bool active)
        {
            GUI.color = UiTheme.ToColor(active ? UiTheme.Palette.AccentB : UiTheme.Palette.Muted);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = UiTheme.ToColor(UiTheme.Palette.Panel);
            for (var i = -1; i <= 1; i++)
            {
                var line = vertical
                    ? new Rect(rect.x + 2f, rect.center.y + (i * 5f) - 0.5f, rect.width - 4f, 1f)
                    : new Rect(rect.center.x + (i * 5f) - 0.5f, rect.y + 2f, 1f, rect.height - 4f);
                GUI.DrawTexture(line, Texture2D.whiteTexture);
            }

            GUI.color = Color.white;
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
        private string Describe(Resident other) => $"{other.FullName}{RoleText(other)}";

        private static string RoleText(Resident r) => string.IsNullOrEmpty(r.Role) ? string.Empty : $" ({r.Role})";

        /// <summary>The Now tab's left column under the name: who they are, where they are and what they're doing.</summary>
        private void DrawNowSummary(Resident r)
        {
            GUILayout.Label(r.Personality.Describe(), styles.Small);
            GUILayout.Label($"Where: {DescribeLocation(r)}", styles.Text);
            GUILayout.Label($"Doing: {DescribeActivity(r)}", styles.Text);
        }

        private void DrawNow(Resident r)
        {
            if (!r.IsAlive)
            {
                GUILayout.Label(r.Personality.Describe(), styles.Small);
                GUILayout.Label($"Died on day {r.DiedAt?.Day} at {r.DiedAt?.ClockText}, of {r.CauseOfDeath}. Remembered by the town.", styles.Text);
                return;
            }

            GUILayout.Space(4f);

            if (r.State == ResidentState.Deciding)
            {
                GUILayout.Label(Simulation.NeedsDayPlan(r) ? "Planning their day with the AI." : "Choosing what to do with the AI.", styles.Small);
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
                else if (r.GoalSource != DecisionSource.Ai)
                {
                    GUILayout.Label($"Why: {r.Goal}", styles.Small);
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
            GUILayout.Label("City news", styles.Text);
            var history = Simulation.History.Events;
            for (var i = history.Count - 1; i >= Math.Max(0, history.Count - 3); i--)
            {
                GUILayout.Label($"Day {history[i].Time.Day}: {history[i].Summary}", styles.Small);
            }
        }

        private void DrawPeople(Resident r)
        {
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

        /// <summary>
        /// Tiny City 2's Things tab (owner, 5 November): what the resident is carrying, item by item, then their
        /// trades (food and money changing hands, tax), newest first, and the money in and out.
        /// </summary>
        private void DrawThings(Resident r)
        {
            var day = Simulation.Time.Day;
            GUILayout.Label(string.IsNullOrEmpty(r.Role) ? "No role chosen" : $"Role: {r.Role}", styles.Text);
            if (Simulation.RoleRequests.TryGetValue(r.Id, out var wanted))
            {
                GUILayout.Label(wanted == TownSimulation.RoleNone ? "Asked to give up their role at the next meeting" : $"Asked to become the {wanted} at the next meeting", styles.Small);
            }
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

            GUILayout.Label($"Clean water: {(r.CleanWater == 0 ? "none" : r.CleanWater.ToString())}", styles.Small);

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
        }

        private void DrawTimeline(Resident r)
        {
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
            if (Splitter(ref footerHeight, MinSectionHeight, Mathf.Max(MinSectionHeight, Screen.height * 0.6f), upward: true))
            {
                PlayerPrefs.SetFloat(FooterHeightPrefsKey, footerHeight);
                PlayerPrefs.Save();
            }

            footerScroll = BeginVerticalScroll(footerScroll, GUILayout.Height(footerHeight));
            DrawFooterContents();
            EndVerticalScroll();
        }

        private void DrawFooterContents()
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
            GUILayout.Label("Click someone to follow them and see who they are · middle click: stop following · N/B: next/previous resident · drag and wheel: pan and zoom · J: journal · S: saved towns · H: hide this panel · Settings: top right", styles.Small);
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

        /// <summary>A labelled bar <paramref name="width"/> wide: name, a bar coloured by how full it is, and the number.</summary>
        private void Bar(string name, double value, double max, float width)
        {
            const float labelWidth = 62f;
            const float numberWidth = 28f;
            var rect = GUILayoutUtility.GetRect(width, 18f, GUILayout.Width(width));
            GUI.Label(new Rect(rect.x, rect.y, labelWidth, rect.height), name, styles.Small);
            var bar = new Rect(rect.x + labelWidth, rect.y + 4f, Mathf.Max(10f, rect.width - labelWidth - numberWidth - 4f), rect.height - 8f);
            var p = UiTheme.Palette;
            GUI.color = UiTheme.ToColor(p.Control);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            var fraction = Mathf.Clamp01((float)(value / Math.Max(1.0, max)));
            value = value / Math.Max(1.0, max) * Needs.Max;
            GUI.color = UiTheme.ToColor(value < 20 ? p.Error : value < 50 ? p.Warning : p.Success);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * fraction, bar.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(bar.xMax + 4f, rect.y, numberWidth, rect.height), $"{value:0}", styles.Small);
        }

        private string DescribeLocation(Resident r)
        {
            var map = Simulation.World.GetMap(r.Location.MapId);
            var at = Simulation.World.Objects.FirstOrDefault(o => o.Location == r.Location);
            return at != null ? $"{map.Name}, at the {at.Name.ToLowerInvariant()}" : map.Name;
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

            return $"{other.FirstName}: {rel.Describe()} (friendship {rel.Friendship:0}, trust {rel.Trust:0}{(rel.Dislike >= 10 ? $", dislike {rel.Dislike:0}" : string.Empty)}), {rel.Interactions} chat(s)";
        }

        private static string Capital(string text) => string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);
    }
}
