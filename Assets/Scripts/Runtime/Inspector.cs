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
        public const float Width = 380f;
        private const int RosterPerRow = 5;
        private const int TimelineShown = 40;

        private static readonly string[] TabNames = { "Now", "People", "Memories", "Work", "Timeline" };

        private readonly GameController game;
        private GuiStyles styles;
        private int tab;
        private Vector2 scroll;
        private (string residentId, long minute, string text) nextPlan;
        private List<TimelineEntry> timeline = new List<TimelineEntry>();
        private (string residentId, long minute, long history, TownSimulation sim) timelineFor;

        public Inspector(GameController game)
        {
            this.game = game;
        }

        private TownSimulation Simulation => game.Simulation;

        public void Draw(GuiStyles guiStyles)
        {
            styles = guiStyles;
            var r = game.SelectedResident;
            var area = new Rect(Screen.width - Width, 0f, Width, Screen.height);
            GUI.color = UiTheme.ToColor(UiTheme.Palette.Panel);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = UiTheme.ToColor(UiTheme.Palette.BarBorder);
            GUI.DrawTexture(new Rect(area.x, 0f, 1f, area.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(area.x + 14f, 10f, Width - 28f, Screen.height - 20f));
            DrawTownControls(r);
            GUILayout.Space(6f);
            tab = GUILayout.Toolbar(tab, TabNames);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label($"{r.FullName}, {r.AgeYears} ({Words(r.LifeStage.ToString())})", styles.Title);
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

            GUILayout.EndScrollView();
            DrawFooter();
            GUILayout.EndArea();
        }

        private void DrawTownControls(Resident r)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Tiny City", styles.Title);
            GUILayout.FlexibleSpace();
            if (GUILayout.Toggle(game.SettingsOpen, "Settings", GUI.skin.button, GUILayout.Width(90f), GUILayout.Height(28f)) != game.SettingsOpen)
            {
                game.ToggleSettings();
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

            var panels = game.Panels;
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Camera panels: {panels.Count}", styles.Small, GUILayout.Width(120f));
            if (GUILayout.Button("−", GUILayout.Width(28f)))
            {
                game.SetPanelCount(panels.Count - 1);
            }

            if (GUILayout.Button("+", GUILayout.Width(28f)))
            {
                game.SetPanelCount(panels.Count + 1);
            }

            if (GUILayout.Button("Watch everyone"))
            {
                game.WatchEveryone();
            }

            GUILayout.EndHorizontal();
            if (GUILayout.Button(game.JournalOpen ? "Close the town journal (J)" : "Open the town journal (J)"))
            {
                game.ToggleJournal();
            }

            // Five names to a row, so a town of any size fits. Picking someone shows them here and in the active panel.
            var roster = Simulation.Residents;
            for (var row = 0; row < roster.Count; row += RosterPerRow)
            {
                GUILayout.BeginHorizontal();
                foreach (var other in roster.Skip(row).Take(RosterPerRow))
                {
                    var name = other.IsAlive ? other.FirstName : $"{other.FirstName} †";
                    if (GUILayout.Toggle(other == r, name, GUI.skin.button) && other != r)
                    {
                        game.Select(other, followInActivePanel: true);
                    }
                }

                GUILayout.EndHorizontal();
            }
        }

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
                var left = Mathf.Max(0, (int)(r.DecisionDeadline - Simulation.Time.TotalMinutes));
                GUILayout.Label($"Waiting for the AI to suggest something; if nothing comes within {left} game minutes, their routine decides.", styles.Small);
            }
            else if (!string.IsNullOrEmpty(r.Goal))
            {
                if (r.GoalSource == DecisionSource.Ai)
                {
                    GUILayout.Label("Why: chosen with the AI. In their own words (the AI's explanation, not a game fact):", styles.Small);
                    GUILayout.Label(r.Goal, styles.AiNote);
                }
                else
                {
                    GUILayout.Label($"Why (game routine): {r.Goal}", styles.Small);
                }
            }

            GUILayout.Label($"Next, by routine: {NextPlan(r)}", styles.Small);

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
            GUILayout.Label(DescribeLivelihood(r), styles.Text);
            GUILayout.Label($"Money: {Money.Format(r.MoneyPence)} · food at home for {r.Groceries} meal(s)", styles.Text);
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
            if (GUILayout.Button("Export life story"))
            {
                game.ExportLifeStory(r);
            }

            if (GUILayout.Button("In the journal"))
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

            if (GUILayout.Button(game.SavesOpen ? "Close saves (S)" : "Saved towns (S)"))
            {
                game.ToggleSaves();
            }

            GUILayout.EndHorizontal();
            GUILayout.Label("Click a panel to make it active. N/B: next/previous resident in it · Tab: free camera, other map · F: follow · drag and wheel: pan and zoom · +/−: panels · J: journal · S: saved towns · Settings: top right", styles.Small);
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
            var rect = GUILayoutUtility.GetRect(Width - 28f, 18f);
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
            var name = business == null ? map.Name : $"{map.Name} ({(business.IsOpen(Simulation.Time) ? "open" : "closed")}, {business.HoursText})";
            return at != null ? $"{name}, at the {at.Name.ToLowerInvariant()}" : name;
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

        private static string Words(string pascal) =>
            string.Concat(pascal.Select((c, i) => i > 0 && char.IsUpper(c) ? " " + char.ToLowerInvariant(c) : c.ToString()));
    }
}
