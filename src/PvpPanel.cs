using System;
using System.Collections.Generic;
using UnityEngine;

namespace ErenshorPvP
{
    internal enum PvpPanelTab { Status, Fight, Rules, Score, Debug }

    // Party Tools-style PvP window: same palette, header drag, offset persistence, and
    // upper-right anchoring below the minimap, so both mods read as one interface.
    //
    // The panel is compact by default and shows only what needs an answer right now: the
    // master switch, the current zone's safety, a pending challenge, and a live fight. The
    // FULL checkbox reveals the tab bar and every detail view. Layout runs through
    // GUILayout inside an auto-sizing window, so a tab can grow without a manual height
    // table, and long content scrolls rather than running off the screen.
    internal static class PvpPanel
    {
        private const int WindowId = 764317;

        private static Texture2D _backgroundTexture;
        private static Texture2D _borderTexture;
        private static Texture2D _barTexture;
        private static Texture2D _barBackTexture;
        private static GUIStyle _windowStyle;
        private static GUIStyle _titleStyle;
        private static GUIStyle _nameStyle;
        private static GUIStyle _valueStyle;
        private static GUIStyle _blockedStyle;
        private static GUIStyle _footerStyle;
        private static GUIStyle _buttonStyle;
        private static GUIStyle _tabStyle;
        private static GUIStyle _activeTabStyle;
        private static GUIStyle _toggleStyle;
        private static GUIStyle _wrapStyle;
        private static GUIStyle _subtleStyle;
        private static GUIStyle _commandStyle;
        private static GUIStyle _sectionStyle;
        private static GUIStyle _closeStyle;

        private static PvpPanelPositionState _positionState;
        private static Action<bool> _persistFullView;
        private static bool _fullView;
        private static PvpPanelTab _tab = PvpPanelTab.Status;
        private static Rect _window = new Rect(0f, 0f, Width, 150f);
        private static float _windowHeight = 150f;
        private static Vector2 _scroll;
        private static bool _dragging;
        private static Vector2 _dragOffset;

        private static int _debugAttackers = 2;
        private static int _planDefenders = 1;
        private static int _planAttackers = 3;
        private static string _result = string.Empty;
        private static PvpPanelTab _resultTab;
        private static float _resultExpires;
        private static readonly Dictionary<string, bool> Sections = new Dictionary<string, bool>(StringComparer.Ordinal);

        internal const float Width = 336f;
        private const float HeaderHeight = 28f;
        private const float ResultSeconds = 30f;
        private const float MinimumScrollHeight = 150f;
        private const float ReservedChrome = 250f;
        private const int DragControlHint = 0x45F0117;
        // Leaves the Full checkbox and close button clickable at the right of the header.
        private const float DragHandleInset = 80f;

        internal static void ConfigurePosition(float offsetX, float offsetY, Action<float, float> persist)
        {
            _positionState = new PvpPanelPositionState(offsetX, offsetY, persist);
        }

        internal static void ConfigureView(bool fullView, Action<bool> persist)
        {
            _fullView = fullView;
            _persistFullView = persist;
        }

        internal static void ResetPosition()
        {
            EnsurePositionState();
            _positionState.Reset();
            _window.x = 0f;
            _window.y = 0f;
        }

        internal static void SelectTab(PvpPanelTab tab)
        {
            _tab = tab;
            // Tabs only exist in the full view, so asking for one implies opening it.
            if (!_fullView) SetFullView(true);
        }

        internal static void ShowPendingChallenge()
        {
            // The compact view already carries the challenge card; do not force the full view on.
            _tab = PvpPanelTab.Status;
        }

        // True while the cursor is over the window, so the click can be kept out of the world.
        internal static bool PointerIsOverPanel(Vector2 screenPoint)
        {
            if (_dragging) return true;
            return _window.width > 0f && _window.Contains(screenPoint);
        }

        internal static void Draw()
        {
            EnsureStyles();
            EnsurePositionState();

            float height = Mathf.Max(60f, _windowHeight);
            PvpPanelPosition anchored = _positionState.ResolveAndRecover(Screen.width, Screen.height, Width, height);
            _window = new Rect(anchored.X, anchored.Y, Width, height);

            // Dragging is handled here, in screen space, rather than with GUI.DragWindow.
            // The panel re-anchors from the persisted offsets every frame, so letting the
            // window move itself fought that: the offsets were reverse-engineered from the
            // moved rect, and a drag past a screen edge could clamp into a corner it could
            // not be dragged back out of. The drag now writes the persisted position
            // directly, which is the single source of truth.
            HandleDrag(height);

            int previousDepth = GUI.depth;
            try
            {
                GUI.depth = -45;
                Rect drawn = GUILayout.Window(WindowId, _window, DrawWindow, GUIContent.none, _windowStyle,
                    new[] { GUILayout.Width(Width) });
                // Only the auto-fitted height is adopted; position stays owned by HandleDrag.
                _windowHeight = drawn.height;
                _window.height = drawn.height;
            }
            finally
            {
                GUI.depth = previousDepth;
            }
        }

        private static void HandleDrag(float height)
        {
            Event current = Event.current;
            if (current == null) return;
            int controlId = GUIUtility.GetControlID(DragControlHint, FocusType.Passive);
            Rect handle = new Rect(_window.x, _window.y, Width - DragHandleInset, HeaderHeight);

            if (current.type == EventType.MouseDown && current.button == 0 && handle.Contains(current.mousePosition))
            {
                GUIUtility.hotControl = controlId;
                _dragging = true;
                _dragOffset = current.mousePosition - new Vector2(_window.x, _window.y);
                current.Use();
                return;
            }

            // A drag owns the gesture until mouse-up even if the pointer leaves the panel.
            if (GUIUtility.hotControl != controlId) return;

            if (current.type == EventType.MouseDrag && _dragging)
            {
                PvpPanelPosition moved = _positionState.MoveTo(
                    Screen.width, Screen.height, Width, height,
                    current.mousePosition.x - _dragOffset.x,
                    current.mousePosition.y - _dragOffset.y);
                _window.x = moved.X;
                _window.y = moved.Y;
                current.Use();
                return;
            }

            if (current.type == EventType.MouseUp && current.button == 0)
            {
                GUIUtility.hotControl = 0;
                if (_dragging) _positionState.CommitIfMoved();
                _dragging = false;
                current.Use();
            }
        }

        private static void DrawWindow(int id)
        {
            DrawHeader();
            if (_fullView) DrawTabs();

            if (!_fullView) DrawCompactBody();
            else
            {
                float maxHeight = Mathf.Max(MinimumScrollHeight, Screen.height - ReservedChrome);
                _scroll = GUILayout.BeginScrollView(_scroll, false, false,
                    GUIStyle.none, GUI.skin.verticalScrollbar, GUIStyle.none,
                    new[] { GUILayout.MaxHeight(maxHeight) });
                DrawActiveTab();
                GUILayout.EndScrollView();
            }

            GUILayout.Space(2f);
            GUILayout.Label(FooterText(), _footerStyle);
        }

        private static void DrawHeader()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("ERENSHOR PVP", _titleStyle);
            GUILayout.FlexibleSpace();
            bool full = GUILayout.Toggle(_fullView, " Full", _toggleStyle, new[] { GUILayout.Width(48f) });
            if (full != _fullView) SetFullView(full);
            if (GUILayout.Button("x", _closeStyle, new[] { GUILayout.Width(20f) })) PvpController.ClosePanel();
            GUILayout.EndHorizontal();
            GUILayout.Space(3f);
        }

        private static void SetFullView(bool value)
        {
            _fullView = value;
            _scroll = Vector2.zero;
            if (_persistFullView != null) _persistFullView(value);
        }

        private static void DrawTabs()
        {
            GUILayout.BeginHorizontal();
            DrawTab("PVP", PvpPanelTab.Status);
            DrawTab("FIGHT", PvpPanelTab.Fight);
            DrawTab("RULES", PvpPanelTab.Rules);
            DrawTab("SCORE", PvpPanelTab.Score);
            if (PvpController.ShowDebugTab) DrawTab("TEST", PvpPanelTab.Debug);
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);
        }

        private static void DrawTab(string label, PvpPanelTab tab)
        {
            bool active = _tab == tab;
            if (GUILayout.Button(label, active ? _activeTabStyle : _tabStyle) && !active)
            {
                _tab = tab;
                _scroll = Vector2.zero;
            }
        }

        private static void DrawActiveTab()
        {
            if (PvpController.ShowDebugTab && _tab == PvpPanelTab.Debug) DrawDebugTab();
            else if (_tab == PvpPanelTab.Fight) DrawFightTab();
            else if (_tab == PvpPanelTab.Rules) DrawRulesTab();
            else if (_tab == PvpPanelTab.Score) DrawScoreTab();
            else DrawStatusTab();
        }

        // Compact view: the master switch, whether this zone can hurt you, and anything
        // that is actually waiting on a decision. Nothing else.
        private static void DrawCompactBody()
        {
            MasterToggle();
            Row("Zone", PvpController.CurrentScene, false);
            Row("Status", ZoneStatusText(), PvpController.IsProtectedHere);

            if (PvpController.HasPending) PendingCard();
            if (PvpTemporaryCloneFactory.HasActiveTeam) ActiveEncounterBlock(false);
            ResultArea();
        }

        private static void DrawStatusTab()
        {
            MasterToggle();

            bool enabled = PvpController.Enabled;
            bool previous = GUI.enabled;
            GUI.enabled = enabled;
            // The consent difference is the one thing a player can genuinely get wrong, so
            // each switch says what it does to you rather than only naming itself.
            bool arranged = GUILayout.Toggle(PvpController.ArrangedEnabled, "     Arranged challenges", _toggleStyle);
            if (arranged != PvpController.ArrangedEnabled) PvpController.SetArrangedEnabled(arranged);
            GUILayout.Label("          You are asked to Accept or Refuse.", _subtleStyle);

            bool ambush = GUILayout.Toggle(PvpController.AmbushEnabled, "     Wild ambushes", _toggleStyle);
            if (ambush != PvpController.AmbushEnabled) PvpController.SetAmbushEnabled(ambush);
            GUILayout.Label("          No warning. They simply begin.", _subtleStyle);
            GUI.enabled = previous;

            GUILayout.Space(4f);
            Row("Zone", PvpController.CurrentScene, false);
            Row("Status", ZoneStatusText(), PvpController.IsProtectedHere);
            Row("Level range", "+/-" + PvpController.LevelRangeHere, false);

            if (!PvpController.IsProtectedHere)
            {
                GUILayout.Space(3f);
                if (GUILayout.Button(PvpController.AmbushZoneListedHere ? "Stop ambushes here" : "Allow ambushes here", _buttonStyle))
                    Run(ToggleAmbushHere);
            }

            GUILayout.Space(4f);
            if (PvpController.HasPending) PendingCard();
            else Row("Challenge", "none pending", false);

            if (PvpTemporaryCloneFactory.HasActiveTeam) ActiveEncounterBlock(false);

            // Collapsed by default: reachable without the hidden TEST tab, but not clutter.
            if (Section("panelopts", "PANEL", false))
            {
                int click = ButtonPair("Reset position", "Status");
                if (click == 1) Run(ResetPositionText);
                else if (click == 2) Run(PvpController.Status);
            }

            ResultArea();
        }

        private static string ResetPositionText()
        {
            ResetPosition();
            return "[Erenshor PvP] Panel moved back to its default position.";
        }

        private static void MasterToggle()
        {
            bool enabled = PvpController.Enabled;
            bool toggled = GUILayout.Toggle(enabled, enabled ? " World PvP: ON" : " World PvP: OFF", _toggleStyle);
            if (toggled != enabled) PvpController.SetEnabled(toggled);
        }

        private static void PendingCard()
        {
            GUILayout.Space(3f);
            PvpTeamPlan team = PvpController.PendingTeam;
            PvpEncounterFlavor flavor = PvpController.PendingFlavor;
            int members = team == null ? 1 : team.Members.Count;

            Row("Challenge", "party of " + members + " - " + PvpController.PendingSecondsLeft + "s", true);
            if (flavor != null) GUILayout.Label(flavor.LeaderLine, _wrapStyle);
            if (team != null) GUILayout.Label(team.DescribeCompact(), _wrapStyle);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Accept", _buttonStyle)) PvpController.Accept();
            if (GUILayout.Button("Refuse", _buttonStyle)) PvpController.Refuse();
            GUILayout.EndHorizontal();
            GUILayout.Space(3f);
        }

        // Shared by the compact body and the FIGHT tab. `detailed` adds role/guild/spell
        // lines that would be clutter in the compact view.
        private static void ActiveEncounterBlock(bool detailed)
        {
            GUILayout.Space(3f);
            Row("Encounter", PvpTemporaryCloneFactory.ActiveMode.ToString().ToLowerInvariant(), true);
            if (detailed) Row("Motive", MotiveText(PvpTemporaryCloneFactory.ActiveMotive), false);

            List<PvpRosterEntry> roster = PvpTemporaryCloneFactory.Roster();
            for (int i = 0; i < roster.Count; i++)
            {
                PvpRosterEntry entry = roster[i];
                Row(entry.Name + " L" + entry.Level + " " + entry.ClassName, entry.HealthText, !entry.Alive);
                HealthBar(entry.HealthFraction, entry.Alive);
                if (!detailed) continue;
                GUILayout.Label(entry.Role.ToString().ToLowerInvariant() +
                    " - " + (string.IsNullOrEmpty(entry.GuildId) ? "no guild" : entry.GuildId) +
                    " - " + entry.KnownSpells + " spells", _subtleStyle);
            }

            Row("You", PvpController.PlayerHealthText, false);
            GUILayout.Space(3f);
            if (GUILayout.Button("Flee this fight", _buttonStyle)) Run(PvpTemporaryCloneFactory.Flee);
        }

        private static void DrawFightTab()
        {
            if (!PvpTemporaryCloneFactory.HasActiveTeam)
            {
                Row("Roster", "no active encounter", false);
                GUILayout.Space(4f);
                int idle = ButtonPair("Team", "Clone status");
                if (idle == 1) Run(PvpController.TeamText);
                else if (idle == 2) Run(PvpTemporaryCloneFactory.CloneStatus);
                // Diagnose is most useful with nothing running: it reports why no offer fired.
                idle = ButtonPair("Verify", "Diagnose");
                if (idle == 1) Run(PvpController.VerifyText);
                else if (idle == 2) Run(PvpController.DiagnoseText);
                ResultArea();
                return;
            }

            ActiveEncounterBlock(true);
            GUILayout.Space(4f);
            int click = ButtonPair("Verify", "Diagnose");
            if (click == 1) Run(PvpController.VerifyText);
            else if (click == 2) Run(PvpController.DiagnoseText);
            click = ButtonPair("Team", "Clone status");
            if (click == 1) Run(PvpController.TeamText);
            else if (click == 2) Run(PvpTemporaryCloneFactory.CloneStatus);
            if (GUILayout.Button("Despawn team (cleanup)", _commandStyle)) Run(DespawnManually);
            ResultArea();
        }

        private static void DrawRulesTab()
        {
            Row("Level range", "+/-" + PvpController.LevelRangeHere, false);
            Row("Your party", PvpController.DefenderCount + " (avg L" + PvpController.DefenderAverageLevel + ")", false);
            Row("Attackers", PvpController.PartySizeRuleText, false);
            Row("Next ambush", PvpController.NextAmbushText, false);
            Row("Next offer", PvpController.NextOfferText, false);

            if (Section("ambush", "AMBUSH CADENCE", true))
            {
                Stepper("Chance", PvpController.AmbushChancePercent + "%", PvpController.AdjustAmbushChance);
                Stepper("Gap min", PvpController.AmbushMinimumMinutes + "m", PvpController.AdjustAmbushMinimum);
                Stepper("Gap max", PvpController.AmbushMaximumMinutes + "m", PvpController.AdjustAmbushMaximum);
                int click = ButtonPair(
                    PvpController.AmbushZoneListedHere ? "Stop here" : "Allow here", "List zones");
                if (click == 1) Run(ToggleAmbushHere);
                else if (click == 2) Run(PvpController.AmbushZonesText);
            }

            if (Section("plan", "MATCH SIMULATOR", false))
            {
                Stepper("Defenders", _planDefenders.ToString(), AdjustPlanDefenders);
                Stepper("Attackers", _planAttackers.ToString(), AdjustPlanAttackers);
                if (GUILayout.Button("Simulate match", _commandStyle)) Run(SimulatePlan);
            }

            ResultArea();
        }

        private static void DrawScoreTab()
        {
            Row("Arranged", PvpRecordService.ArrangedWins + "W / " + PvpRecordService.ArrangedLosses + "L", false);
            Row("Ambush", PvpRecordService.AmbushWins + "W / " + PvpRecordService.AmbushLosses + "L", false);
            Row("Escaped", PvpRecordService.Escapes.ToString(), false);
            Row("Last", LastMatchText(), false);

            GUILayout.Space(4f);
            if (PvpRewardService.RewardsEnabled)
            {
                Row("Victory reward", PvpRewardService.XpPercent + "% XP + gold", false);
                int remaining = PvpRewardService.CooldownMinutesRemaining;
                Row("Reward cooldown", remaining == 0 ? "ready" : remaining + "m", remaining > 0);
            }
            else Row("Victory reward", "disabled", true);

            Row("Cosmetic drop", PvpRewardService.CosmeticChancePercent + "% chance", false);
            Row("Cosmetic slots", PvpRewardService.CosmeticSlotStatus, PvpRewardService.CosmeticSlotStatus != "available");
        }

        // Every /epvp command has a control here, so the chat syntax is never required.
        private static void DrawDebugTab()
        {
            if (Section("encounter", "ENCOUNTER", true))
            {
                Stepper("Attackers", _debugAttackers.ToString(), AdjustDebugAttackers);
                int click = ButtonPair("Force arranged", "Force ambush");
                if (click == 1) PvpController.ForceOffer(PvpEncounterMode.Arranged, _debugAttackers);
                else if (click == 2) PvpController.ForceOffer(PvpEncounterMode.Ambush, _debugAttackers);

                click = ButtonPair("Accept", "Refuse");
                if (click == 1) PvpController.Accept();
                else if (click == 2) PvpController.Refuse();

                click = ButtonPair("Team", "Clone status");
                if (click == 1) Run(PvpController.TeamText);
                else if (click == 2) Run(PvpTemporaryCloneFactory.CloneStatus);

                click = ButtonPair("Flee", "Despawn");
                if (click == 1) Run(PvpTemporaryCloneFactory.Flee);
                else if (click == 2) Run(DespawnManually);
            }

            if (Section("inspect", "INSPECT", false))
            {
                int click = ButtonPair("Verify", "Diagnose");
                if (click == 1) Run(PvpController.VerifyText);
                else if (click == 2) Run(PvpController.DiagnoseText);

                click = ButtonPair("Status", "Self test");
                if (click == 1) Run(PvpController.Status);
                else if (click == 2) Run(SelfTestText);

                bool validation = GUILayout.Toggle(PvpController.ValidationLogging, "     Detailed validation logging", _toggleStyle);
                if (validation != PvpController.ValidationLogging) PvpController.ToggleValidationLogging();

                if (GUILayout.Button("Spawn probe", _commandStyle)) Run(PvpSpawnCapability.InspectLiveState);
            }

            if (Section("clones", "ISOLATED CLONE TESTS", false))
            {
                int click = ButtonPair("Spawn clone", "Target clone");
                if (click == 1) Run(SpawnVisualCloneText);
                else if (click == 2) Run(PvpTemporaryCloneFactory.BeginTargetingTest);
                if (GUILayout.Button("Fight clone", _commandStyle)) Run(PvpTemporaryCloneFactory.BeginLethalFight);
            }

            if (Section("panel", "PANEL", false))
            {
                int click = ButtonPair("Hide test tab", "Reset position");
                if (click == 1) PvpController.HideDebugTab();
                else if (click == 2) ResetPosition();
            }

            ResultArea();
        }

        private static string DespawnManually() { return PvpTemporaryCloneFactory.Despawn("manual"); }
        private static string SpawnVisualCloneText() { return PvpTemporaryCloneFactory.SpawnVisualClone(); }
        private static string SelfTestText() { return "[Erenshor PvP] " + PvpController.SelfTest(); }
        private static string ToggleAmbushHere() { return PvpController.SetAmbushHere(!PvpController.AmbushZoneListedHere); }
        private static string SimulatePlan() { return PvpController.PlanText(_planDefenders, _planAttackers, 0, 0); }
        private static void AdjustDebugAttackers(int delta) { _debugAttackers = Math.Max(1, Math.Min(5, _debugAttackers + delta)); }
        private static void AdjustPlanDefenders(int delta) { _planDefenders = Math.Max(1, Math.Min(5, _planDefenders + delta)); }
        private static void AdjustPlanAttackers(int delta) { _planAttackers = Math.Max(1, Math.Min(5, _planAttackers + delta)); }

        // Command output goes to the social log as usual and is mirrored in the panel, so a
        // button press is still readable while the panel covers the chat area.
        private static void Run(Func<string> action)
        {
            if (action == null) return;
            string output;
            try { output = action(); }
            catch (Exception ex) { output = "[Erenshor PvP] Command failed: " + ex.GetType().Name + "."; }
            _result = output ?? string.Empty;
            _resultTab = _tab;
            _resultExpires = Time.unscaledTime + ResultSeconds;
            PvpController.Say(_result);
        }

        private static void ResultArea()
        {
            if (string.IsNullOrEmpty(_result)) return;
            if (_fullView && _resultTab != _tab) return;
            if (Time.unscaledTime > _resultExpires) return;
            GUILayout.Space(4f);
            GUILayout.Label(_result.Replace("[Erenshor PvP] ", string.Empty), _wrapStyle);
        }

        // Collapsible group header. Returns true when the body should be drawn.
        private static bool Section(string key, string title, bool defaultOpen)
        {
            bool open;
            if (!Sections.TryGetValue(key, out open))
            {
                open = defaultOpen;
                Sections[key] = open;
            }
            GUILayout.Space(3f);
            if (GUILayout.Button((open ? "- " : "+ ") + title, _sectionStyle))
            {
                open = !open;
                Sections[key] = open;
            }
            return open;
        }

        private static void Row(string label, string value, bool warn)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _nameStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label(value, warn ? _blockedStyle : _valueStyle);
            GUILayout.EndHorizontal();
        }

        private static void HealthBar(float fraction, bool alive)
        {
            Rect bar = GUILayoutUtility.GetRect(10f, 4f, new[] { GUILayout.ExpandWidth(true) });
            if (Event.current == null || Event.current.type != EventType.Repaint) return;
            GUI.DrawTexture(bar, _barBackTexture);
            if (alive && fraction > 0f)
                GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * fraction, bar.height), _barTexture);
        }

        // Returns 0 for no click, 1 for the left button, 2 for the right.
        private static int ButtonPair(string left, string right)
        {
            int clicked = 0;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(left, _commandStyle)) clicked = 1;
            if (GUILayout.Button(right, _commandStyle)) clicked = 2;
            GUILayout.EndHorizontal();
            return clicked;
        }

        private static void Stepper(string label, string value, Action<int> adjust)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, _nameStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("-", _commandStyle, new[] { GUILayout.Width(24f) }) && adjust != null) adjust(-1);
            GUILayout.Label(value, _valueStyle, new[] { GUILayout.Width(46f) });
            if (GUILayout.Button("+", _commandStyle, new[] { GUILayout.Width(24f) }) && adjust != null) adjust(1);
            GUILayout.EndHorizontal();
        }

        private static string ZoneStatusText()
        {
            if (PvpController.IsProtectedHere) return "protected";
            if (PvpController.AmbushAllowedHere) return "ambush enabled";
            return "arranged only";
        }

        private static string MotiveText(string motive)
        {
            return string.IsNullOrEmpty(motive) ? "none" : motive.Replace('_', ' ');
        }

        private static string LastMatchText()
        {
            string opponent = PvpRecordService.LastOpponent;
            if (string.IsNullOrEmpty(opponent)) return "no matches yet";
            string result = PvpRecordService.LastResult;
            if (result == "proxy_death") result = "win";
            else if (result == "player_death") result = "loss";
            else if (result == "player_fled") result = "fled";
            return opponent + " (" + result + ")";
        }

        private static string FooterText()
        {
            if (PvpController.HasPending) return "Arranged challenge - Accept or Refuse";
            if (!_fullView) return "F10 closes - tick Full for tabs and controls";
            if (PvpController.ShowDebugTab && _tab == PvpPanelTab.Debug) return "Test controls - /epvp debug hides this tab";
            if (_tab == PvpPanelTab.Fight) return "Live proxy state; refreshes each frame";
            if (_tab == PvpPanelTab.Rules) return "Changes save to config immediately";
            if (_tab == PvpPanelTab.Score) return "Rewards require a completed proxy victory";
            return "Drag the title bar to move this panel";
        }

        internal static void Close()
        {
            if (_positionState != null) _positionState.CommitIfMoved();
            _dragging = false;
        }

        internal static void Dispose()
        {
            Close();
            DestroyTexture(ref _backgroundTexture);
            DestroyTexture(ref _borderTexture);
            DestroyTexture(ref _barTexture);
            DestroyTexture(ref _barBackTexture);
            _windowStyle = null; _titleStyle = null; _nameStyle = null; _valueStyle = null;
            _blockedStyle = null; _footerStyle = null; _buttonStyle = null; _tabStyle = null;
            _activeTabStyle = null; _toggleStyle = null; _wrapStyle = null; _subtleStyle = null;
            _commandStyle = null; _sectionStyle = null; _closeStyle = null;
        }

        private static void EnsurePositionState()
        {
            if (_positionState == null) _positionState = new PvpPanelPositionState(0f, 0f, null);
        }

        private static void EnsureStyles()
        {
            if (_windowStyle != null && _backgroundTexture != null && _borderTexture != null &&
                _barTexture != null && _barBackTexture != null) return;

            if (_backgroundTexture == null) _backgroundTexture = MakeTexture(new Color(0.035f, 0.055f, 0.065f, 0.92f));
            if (_borderTexture == null) _borderTexture = MakeTexture(new Color(0.48f, 0.76f, 0.78f, 0.90f));
            if (_barTexture == null) _barTexture = MakeTexture(new Color(0.68f, 0.94f, 0.86f, 0.92f));
            if (_barBackTexture == null) _barBackTexture = MakeTexture(new Color(0.14f, 0.20f, 0.22f, 0.85f));

            _windowStyle = new GUIStyle(GUI.skin.window);
            _windowStyle.normal.background = _backgroundTexture;
            _windowStyle.onNormal.background = _backgroundTexture;
            _windowStyle.border = new RectOffset(1, 1, 1, 1);
            _windowStyle.padding = new RectOffset(12, 12, 8, 9);

            _titleStyle = new GUIStyle(GUI.skin.label);
            _titleStyle.fontSize = 14;
            _titleStyle.fontStyle = FontStyle.Bold;
            _titleStyle.clipping = TextClipping.Clip;
            _titleStyle.normal.textColor = new Color(0.82f, 0.96f, 0.97f, 1f);

            _nameStyle = new GUIStyle(GUI.skin.label);
            _nameStyle.fontSize = 12;
            _nameStyle.clipping = TextClipping.Clip;
            _nameStyle.normal.textColor = new Color(0.88f, 0.92f, 0.91f, 1f);

            _valueStyle = new GUIStyle(GUI.skin.label);
            _valueStyle.fontSize = 12;
            _valueStyle.fontStyle = FontStyle.Bold;
            _valueStyle.alignment = TextAnchor.MiddleRight;
            _valueStyle.clipping = TextClipping.Clip;
            _valueStyle.normal.textColor = new Color(0.68f, 0.94f, 0.86f, 1f);

            _blockedStyle = new GUIStyle(_valueStyle);
            _blockedStyle.normal.textColor = new Color(0.95f, 0.82f, 0.56f, 1f);

            _footerStyle = new GUIStyle(GUI.skin.label);
            _footerStyle.fontSize = 10;
            _footerStyle.clipping = TextClipping.Clip;
            _footerStyle.normal.textColor = new Color(0.66f, 0.76f, 0.76f, 0.95f);

            _buttonStyle = new GUIStyle(GUI.skin.button);
            _buttonStyle.fontSize = 12;
            _buttonStyle.fontStyle = FontStyle.Bold;
            _buttonStyle.padding = new RectOffset(8, 8, 4, 4);
            _buttonStyle.normal.textColor = new Color(0.82f, 0.96f, 0.97f, 1f);

            _tabStyle = new GUIStyle(GUI.skin.button);
            _tabStyle.fontSize = 11;
            _tabStyle.padding = new RectOffset(2, 2, 3, 3);
            _tabStyle.margin = new RectOffset(1, 1, 0, 0);
            _tabStyle.normal.textColor = new Color(0.66f, 0.76f, 0.76f, 0.95f);

            _activeTabStyle = new GUIStyle(_tabStyle);
            _activeTabStyle.fontStyle = FontStyle.Bold;
            _activeTabStyle.normal.textColor = new Color(0.82f, 0.96f, 0.97f, 1f);

            _commandStyle = new GUIStyle(GUI.skin.button);
            _commandStyle.fontSize = 11;
            _commandStyle.padding = new RectOffset(3, 3, 3, 3);
            _commandStyle.normal.textColor = new Color(0.82f, 0.96f, 0.97f, 1f);

            _closeStyle = new GUIStyle(_tabStyle);
            _closeStyle.fontStyle = FontStyle.Bold;

            _toggleStyle = new GUIStyle(GUI.skin.toggle);
            _toggleStyle.fontSize = 12;
            _toggleStyle.clipping = TextClipping.Clip;
            _toggleStyle.normal.textColor = new Color(0.88f, 0.92f, 0.91f, 1f);
            _toggleStyle.onNormal.textColor = new Color(0.82f, 0.96f, 0.97f, 1f);

            _sectionStyle = new GUIStyle(GUI.skin.label);
            _sectionStyle.fontSize = 10;
            _sectionStyle.fontStyle = FontStyle.Bold;
            _sectionStyle.clipping = TextClipping.Clip;
            _sectionStyle.alignment = TextAnchor.MiddleLeft;
            _sectionStyle.normal.textColor = new Color(0.48f, 0.76f, 0.78f, 1f);
            _sectionStyle.hover.textColor = new Color(0.82f, 0.96f, 0.97f, 1f);

            _wrapStyle = new GUIStyle(GUI.skin.label);
            _wrapStyle.fontSize = 11;
            _wrapStyle.wordWrap = true;
            _wrapStyle.normal.textColor = new Color(0.80f, 0.86f, 0.86f, 1f);

            _subtleStyle = new GUIStyle(GUI.skin.label);
            _subtleStyle.fontSize = 10;
            _subtleStyle.clipping = TextClipping.Clip;
            _subtleStyle.normal.textColor = new Color(0.62f, 0.72f, 0.73f, 0.95f);
        }

        private static Texture2D MakeTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private static void DestroyTexture(ref Texture2D texture)
        {
            if (texture == null) return;
            UnityEngine.Object.Destroy(texture);
            texture = null;
        }

        internal static string RunSelfTests()
        {
            string positioning = PvpPanelPositioning.RunSelfTests();
            if (!positioning.StartsWith("PASS", StringComparison.Ordinal)) return positioning;
            if (MotiveText("camp_claim") != "camp claim") return "FAIL motive text";
            if (MotiveText(string.Empty) != "none") return "FAIL empty motive text";

            int attackers = _debugAttackers;
            _debugAttackers = 1; AdjustDebugAttackers(-1);
            if (_debugAttackers != 1) return "FAIL attacker lower bound";
            _debugAttackers = 5; AdjustDebugAttackers(1);
            if (_debugAttackers != 5) return "FAIL attacker upper bound";
            _debugAttackers = attackers;

            int defenders = _planDefenders, planAttackers = _planAttackers;
            _planDefenders = 1; AdjustPlanDefenders(-1);
            if (_planDefenders != 1) return "FAIL plan defender lower bound";
            _planAttackers = 5; AdjustPlanAttackers(1);
            if (_planAttackers != 5) return "FAIL plan attacker upper bound";
            _planDefenders = defenders; _planAttackers = planAttackers;
            return "PASS pvp panel";
        }
    }
}
