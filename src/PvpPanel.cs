using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ErenshorPvP
{
    internal enum PvpPanelTab { Status, Fight, Rules, Score, Debug }

    // Production player UI. Retained uGUI only: one persistent Canvas, launcher, panel,
    // scroll view and tab pages. Values update in place; polling never rebuilds the tree.
    internal static class PvpPanel
    {
        private const int SortingOrder = 520;
        private const float PanelWidth = 470f;
        private const float PanelHeight = PvpWindowChromePolicy.ExpandedHeight;
        private const float HeaderHeight = PvpWindowChromePolicy.HeaderHeight;
        private const float TabHeight = 30f;
        private const float LauncherWidth = StandaloneLauncherVisual.Width;
        private const float LauncherHeight = StandaloneLauncherVisual.Height;

        private static GameObject _root;
        private static Canvas _canvas;
        private static RectTransform _panel;
        private static RectTransform _launcher;
        private static RectTransform _header, _headerGrip, _collapseChevron, _closeRect, _tabs, _viewport, _content, _footer;
        private static GameObject _panelObject;
        private static GameObject _launcherObject;
        private static TextMeshProUGUI _launcherText;
        private static TextMeshProUGUI _titleText;
        private static TextMeshProUGUI _statusText;
        private static TextMeshProUGUI _resultText;
        private static TextMeshProUGUI _pendingText;
        private static TextMeshProUGUI _fightText;
        private static TextMeshProUGUI _rulesText;
        private static TextMeshProUGUI _scoreText;
        private static TextMeshProUGUI _debugText;
        private static Button _acceptButton;
        private static Button _refuseButton;
        private static Button _fleeButton;
        private static Button _ambushHereButton;
        private static Button _debugTabButton;
        private static Button _enabledButton;
        private static Button _arrangedButton;
        private static Button _ambushButton;
        private static bool _toggleVisualInitialized;
        private static bool _lastEnabledVisual;
        private static bool _lastArrangedVisual;
        private static bool _lastAmbushVisual;

        // Sim Actions visual language translated to retained uGUI: dark translucent blue,
        // cyan/teal interaction surfaces, bright cyan headings, and compact high-contrast text.
        private static readonly Color PanelFill = new Color32(4, 23, 32, 184);
        private static readonly Color HeaderFill = new Color32(6, 33, 43, 224);
        private static readonly Color ViewportFill = new Color32(3, 18, 25, 158);
        private static readonly Color ButtonFill = new Color32(9, 43, 56, 220);
        private static readonly Color ButtonHover = new Color32(31, 97, 122, 235);
        private static readonly Color ButtonPressed = new Color32(8, 171, 219, 242);
        private static readonly Color ButtonDisabled = new Color32(8, 31, 40, 145);
        private static readonly Color ToggleOnFill = new Color32(18, 78, 96, 230);
        private static readonly Color CyanAccent = new Color32(8, 171, 219, 242);
        private static readonly Color TitleCyan = new Color32(143, 224, 255, 255);
        private static readonly Color HintCyan = new Color32(143, 199, 224, 255);
        private static readonly Dictionary<PvpPanelTab, GameObject> Pages = new Dictionary<PvpPanelTab, GameObject>();
        private static readonly Dictionary<PvpPanelTab, Button> TabButtons = new Dictionary<PvpPanelTab, Button>();
        private static PvpPanelTab _tab = PvpPanelTab.Status;
        private static bool _built;
        private static bool _panelOpen;
        private static bool _launcherVisible;
        private static bool _collapsed;
        private static bool _fleeConfirm;
        private static float _fleeConfirmUntil;
        private static float _lastScreenWidth;
        private static float _lastScreenHeight;
        private static Action<float, float> _persistPanel;
        private static Action<float, float> _persistLauncher;
        private static float _panelNormX = PvpUiGeometry.Unset;
        private static float _panelNormY = PvpUiGeometry.Unset;
        private static float _launcherNormX = PvpUiGeometry.Unset;
        private static float _launcherNormY = PvpUiGeometry.Unset;
        private static double _lastActivatedAt;

        internal static bool IsBuilt { get { return _built; } }
        internal static int CanvasSortOrder { get { return SortingOrder; } }
        internal static double LastActivatedAt { get { return _lastActivatedAt; } }

        internal static void ConfigurePosition(float panelX, float panelY, float launcherX, float launcherY,
            Action<float, float> persistPanel, Action<float, float> persistLauncher)
        {
            _panelNormX = PvpUiGeometry.InterpretStoredAxis(panelX);
            _panelNormY = PvpUiGeometry.InterpretStoredAxis(panelY);
            _launcherNormX = PvpUiGeometry.InterpretStoredAxis(launcherX);
            _launcherNormY = PvpUiGeometry.InterpretStoredAxis(launcherY);
            _persistPanel = persistPanel;
            _persistLauncher = persistLauncher;
        }

        internal static void Tick(bool panelOpen, bool launcherVisible)
        {
            bool wasPanelOpen = _panelOpen;
            _panelOpen = panelOpen;
            _launcherVisible = launcherVisible;
            if (panelOpen && !wasPanelOpen) TouchActivation();
            if (!SuiteUiPolicy.IsGameplayReady())
            {
                HideAll();
                PvpDragGuard.ForceReleaseIfOwned();
                return;
            }
            if (EventSystem.current == null)
            {
                HideAll();
                PvpDragGuard.ForceReleaseIfOwned();
                return;
            }
            if (!EnsureBuilt()) return;

            if (_lastScreenWidth != Screen.width || _lastScreenHeight != Screen.height)
            {
                _lastScreenWidth = Screen.width;
                _lastScreenHeight = Screen.height;
                ClampAndApplyPositions(false);
            }

            _panelObject.SetActive(panelOpen);
            _launcherObject.SetActive(launcherVisible);
            if (!panelOpen) _fleeConfirm = false;
            UpdateValues();
        }

        internal static void ShowPendingChallenge()
        {
            SelectTab(PvpPanelTab.Status);
        }

        internal static void SelectTab(PvpPanelTab tab)
        {
            _tab = tab;
            ApplyTabVisibility();
        }

        internal static void ResetPosition()
        {
            PvpDragGuard.ForceReleaseIfOwned();
            _panelNormX = PvpUiGeometry.Unset;
            _panelNormY = PvpUiGeometry.Unset;
            if (_built) ApplyPanelPosition(false);
            PersistPanelPosition();
        }

        internal static void ResetLauncherPosition()
        {
            PvpDragGuard.ForceReleaseIfOwned();
            _launcherNormX = PvpUiGeometry.Unset;
            _launcherNormY = PvpUiGeometry.Unset;
            if (_built) ApplyLauncherPosition(false);
            PersistLauncherPosition();
        }

        internal static void Close()
        {
            _panelOpen = false;
            if (_panelObject != null) _panelObject.SetActive(false);
            PvpDragGuard.ForceReleaseIfOwned();
            _fleeConfirm = false;
        }

        internal static void ReleaseDrag()
        {
            PvpDragGuard.ForceReleaseIfOwned();
        }

        internal static void Dispose()
        {
            PvpDragGuard.ForceReleaseIfOwned();
            Pages.Clear();
            TabButtons.Clear();
            if (_root != null)
            {
                try { UnityEngine.Object.DestroyImmediate(_root); } catch { }
            }
            _root = null; _canvas = null; _panel = null; _launcher = null;
            _header = _headerGrip = _collapseChevron = _closeRect = _tabs = _viewport = _content = _footer = null;
            _panelObject = null; _launcherObject = null; _launcherText = null;
            _titleText = null; _statusText = null; _resultText = null; _pendingText = null;
            _fightText = null; _rulesText = null; _scoreText = null; _debugText = null;
            _acceptButton = null; _refuseButton = null; _fleeButton = null; _ambushHereButton = null; _debugTabButton = null;
            _enabledButton = null; _arrangedButton = null; _ambushButton = null;
            _toggleVisualInitialized = false;
            _lastActivatedAt = 0d;
            _built = false; _panelOpen = false; _launcherVisible = false; _collapsed = false; _fleeConfirm = false;
        }

        internal static string RunSelfTests()
        {
            string a = PvpUiGeometry.RunSelfTests();
            string b = SuiteLauncherPolicy.RunSelfTests();
            string c = PvpUiPresentation.RunSelfTests();
            return a.StartsWith("PASS", StringComparison.Ordinal) &&
                   b.StartsWith("PASS", StringComparison.Ordinal) &&
                   c.StartsWith("PASS", StringComparison.Ordinal)
                ? "PASS:pvp_retained_ui_geometry_launcher_presentation_policy"
                : "FAIL:" + a + ";" + b + ";" + c;
        }

        private static bool EnsureBuilt()
        {
            if (_built) return true;
            if (EventSystem.current == null) return false;
            try
            {
                _root = new GameObject("ErenshorPvP.RetainedUI");
                UnityEngine.Object.DontDestroyOnLoad(_root);
                _canvas = _root.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.overrideSorting = true;
                _canvas.sortingOrder = SortingOrder;
                CanvasScaler scaler = _root.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = 1f;
                _root.AddComponent<GraphicRaycaster>();

                BuildLauncher();
                BuildPanel();
                _lastScreenWidth = Screen.width;
                _lastScreenHeight = Screen.height;
                ClampAndApplyPositions(false);
                ApplyTabVisibility();
                _built = true;
                return true;
            }
            catch (Exception ex)
            {
                try { UnityEngine.Object.DestroyImmediate(_root); } catch { }
                _root = null; _built = false;
                PvpController.Say("[Erenshor PvP] UI could not initialize: " + ex.GetType().Name);
                return false;
            }
        }

        private static void BuildLauncher()
        {
            _launcherObject = CreatePanelObject("PvP Launcher", _root.transform, PanelFill);
            _launcher = _launcherObject.GetComponent<RectTransform>();
            _launcher.sizeDelta = new Vector2(LauncherWidth, LauncherHeight);
            _launcher.anchorMin = _launcher.anchorMax = new Vector2(0f, 0f);
            _launcher.pivot = new Vector2(0f, 0f);

            RectTransform grip = CreateRect("Grip", _launcher, new Vector2(StandaloneLauncherVisual.GripWidth, LauncherHeight), new Vector2(0f, 0f));
            Image gripImage = grip.gameObject.AddComponent<Image>();
            gripImage.color = StandaloneLauncherVisual.GripBackground;
            PvpDragGuard drag = grip.gameObject.AddComponent<PvpDragGuard>();
            drag.Target = _launcher;
            drag.OnDragCompleted = PersistLauncherPosition;
            StandaloneLauncherVisual.StyleGrip(grip);

            RectTransform buttonRect = CreateRect("Open", _launcher, new Vector2(LauncherWidth - StandaloneLauncherVisual.GripWidth, LauncherHeight), new Vector2(StandaloneLauncherVisual.GripWidth, 0f));
            Button button = AddButton(buttonRect, "PVP [OFF]", delegate { PvpControlApi.TogglePanel(); });
            _launcherText = button.GetComponentInChildren<TextMeshProUGUI>();
            StandaloneLauncherVisual.StyleButton(button, _launcherText);
            StandaloneLauncherVisual.StyleRoot(_launcher);
        }

        private static void BuildPanel()
        {
            _panelObject = CreatePanelObject("PvP Panel", _root.transform, PanelFill);
            _panel = _panelObject.GetComponent<RectTransform>();
            _panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            _panel.anchorMin = _panel.anchorMax = new Vector2(0f, 0f);
            _panel.pivot = new Vector2(0f, 0f);

            _header = CreateRect("Header", _panel, new Vector2(PanelWidth, HeaderHeight), new Vector2(0f, PanelHeight - HeaderHeight));
            Image headerImage = _header.gameObject.AddComponent<Image>();
            headerImage.color = HeaderFill;
            RectTransform collapseRect = CreateRect("Collapse", _header, new Vector2(28f, 24f), new Vector2(5f, 5f));
            Button collapseButton = AddButton(collapseRect, string.Empty, ToggleCollapsed);
            _collapseChevron = collapseButton.GetComponent<RectTransform>();
            StandaloneLauncherVisual.AddVerticalChevron(_collapseChevron, true);

            _headerGrip = CreateRect("Header Drag Surface", _header, new Vector2(PanelWidth - 68f, HeaderHeight), new Vector2(34f, 0f));
            Image headerGripRaycast = _headerGrip.gameObject.AddComponent<Image>();
            headerGripRaycast.color = new Color(0f, 0f, 0f, 0f);
            _titleText = AddText(_headerGrip, "PVP", 15, TextAlignmentOptions.MidlineLeft, TitleCyan);
            SetOffsets(_titleText.rectTransform, 6f, 0f, 0f, 0f);
            PvpDragGuard panelDrag = _headerGrip.gameObject.AddComponent<PvpDragGuard>();
            panelDrag.Target = _panel;
            panelDrag.OnDragCompleted = PersistPanelPosition;
            panelDrag.OnPointerActivated = TouchActivation;

            _closeRect = CreateRect("Close", _header, new Vector2(28f, 24f), new Vector2(PanelWidth - 34f, 5f));
            AddButton(_closeRect, "X", delegate { PvpControlApi.ClosePanel(); });

            _tabs = CreateRect("Tabs", _panel, new Vector2(PanelWidth - 16f, TabHeight), new Vector2(8f, PanelHeight - HeaderHeight - TabHeight - 4f));
            BuildTabButton(_tabs, PvpPanelTab.Status, "STATUS", 0);
            BuildTabButton(_tabs, PvpPanelTab.Fight, "FIGHT", 1);
            BuildTabButton(_tabs, PvpPanelTab.Rules, "RULES", 2);
            BuildTabButton(_tabs, PvpPanelTab.Score, "SCORE", 3);
            BuildTabButton(_tabs, PvpPanelTab.Debug, "DEBUG", 4);

            float viewportY = 48f;
            float viewportH = PanelHeight - HeaderHeight - TabHeight - 58f;
            _viewport = CreateRect("Viewport", _panel, new Vector2(PanelWidth - 16f, viewportH), new Vector2(8f, viewportY));
            Image viewportImage = _viewport.gameObject.AddComponent<Image>();
            viewportImage.color = ViewportFill;
            _viewport.gameObject.AddComponent<RectMask2D>();
            ScrollRect scroll = _viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.vertical = true; scroll.scrollSensitivity = 22f;
            _content = CreateRect("Content", _viewport, new Vector2(0f, 800f), Vector2.zero);
            _content.anchorMin = new Vector2(0f, 1f); _content.anchorMax = new Vector2(1f, 1f); _content.pivot = new Vector2(0.5f, 1f);
            _content.anchoredPosition = Vector2.zero; _content.sizeDelta = new Vector2(0f, 800f);
            VerticalLayoutGroup layout = _content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8); layout.spacing = 6f;
            layout.childControlHeight = true; layout.childControlWidth = true; layout.childForceExpandHeight = false; layout.childForceExpandWidth = true;
            ContentSizeFitter fitter = _content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = _viewport; scroll.content = _content;

            BuildStatusPage(_content);
            BuildFightPage(_content);
            BuildRulesPage(_content);
            BuildScorePage(_content);
            BuildDebugPage(_content);

            _footer = CreateRect("Footer", _panel, new Vector2(PanelWidth - 16f, 34f), new Vector2(8f, 8f));
            _resultText = AddText(_footer, string.Empty, 12, TextAlignmentOptions.MidlineLeft, HintCyan);
        }

        private static void BuildStatusPage(RectTransform parent)
        {
            RectTransform page = CreatePage(parent, PvpPanelTab.Status);
            AddSectionHeader(page, "STATUS / SAFETY");
            _statusText = AddLayoutText(page, string.Empty, 14, 126f);
            _enabledButton = AddLayoutButton(page, PvpUiPresentation.ToggleLabel("PvP Enabled", PvpController.Enabled),
                delegate { PvpControlApi.SetEnabled(!PvpController.Enabled); });
            _arrangedButton = AddLayoutButton(page, PvpUiPresentation.ToggleLabel("Arranged Challenges", PvpController.ArrangedEnabled),
                delegate { PvpControlApi.SetArrangedEnabled(!PvpController.ArrangedEnabled); });
            _ambushButton = AddLayoutButton(page, PvpUiPresentation.ToggleLabel("Wild Ambushes", PvpController.AmbushEnabled),
                delegate { PvpControlApi.SetAmbushEnabled(!PvpController.AmbushEnabled); });
            _ambushHereButton = AddLayoutButton(page, "Allow ambushes here", delegate
            {
                string result = PvpControlApi.SetAmbushHere(!PvpController.AmbushZoneListedHere);
                SetResult(result);
            });
            AddSectionHeader(page, "PENDING CHALLENGE");
            _pendingText = AddLayoutText(page, "None pending.", 14, 88f);
            RectTransform row = AddButtonRow(page);
            _acceptButton = AddButton(CreateFillCell(row, 0, 2), "Accept", delegate { if (!PvpControlApi.AcceptPending()) SetResult("No pending challenge."); });
            _refuseButton = AddButton(CreateFillCell(row, 1, 2), "Refuse", delegate { if (!PvpControlApi.RefusePending()) SetResult("No pending challenge."); });
            AddLayoutButton(page, "Reset panel position", delegate { PvpControlApi.ResetPanelPosition(); });
        }

        private static void BuildFightPage(RectTransform parent)
        {
            RectTransform page = CreatePage(parent, PvpPanelTab.Fight);
            AddSectionHeader(page, "CURRENT ENCOUNTER");
            _fightText = AddLayoutText(page, string.Empty, 14, 240f);
            _fleeButton = AddLayoutButton(page, "Flee this fight", delegate
            {
                if (!PvpTemporaryCloneFactory.HasActiveTeam) { SetResult("No active PvP encounter."); return; }
                if (!_fleeConfirm || Time.unscaledTime > _fleeConfirmUntil)
                {
                    _fleeConfirm = true; _fleeConfirmUntil = Time.unscaledTime + 5f; SetResult("Press Confirm Flee within 5 seconds."); return;
                }
                _fleeConfirm = false; SetResult(PvpControlApi.FleeEncounter());
            });
            AddLayoutButton(page, "Verify runtime", delegate { SetResult(PvpController.VerifyText()); });
        }

        private static void BuildRulesPage(RectTransform parent)
        {
            RectTransform page = CreatePage(parent, PvpPanelTab.Rules);
            AddSectionHeader(page, "MATCH RULES");
            _rulesText = AddLayoutText(page, string.Empty, 14, 180f);
            AddStepper(page, "Ambush chance", delegate { PvpControlApi.AdjustAmbushChance(-1); }, delegate { PvpControlApi.AdjustAmbushChance(1); });
            AddStepper(page, "Minimum gap", delegate { PvpControlApi.AdjustAmbushMinimum(-1); }, delegate { PvpControlApi.AdjustAmbushMinimum(1); });
            AddStepper(page, "Maximum gap", delegate { PvpControlApi.AdjustAmbushMaximum(-1); }, delegate { PvpControlApi.AdjustAmbushMaximum(1); });
        }

        private static void BuildScorePage(RectTransform parent)
        {
            RectTransform page = CreatePage(parent, PvpPanelTab.Score);
            AddSectionHeader(page, "PVP RECORD");
            _scoreText = AddLayoutText(page, string.Empty, 14, 230f);
        }

        private static void BuildDebugPage(RectTransform parent)
        {
            RectTransform page = CreatePage(parent, PvpPanelTab.Debug);
            AddSectionHeader(page, "DIAGNOSTICS");
            _debugText = AddLayoutText(page, "Developer controls remain available through /epvp debug commands. Production UI does not expose spawn/despawn probes.", 13, 120f);
            AddLayoutButton(page, "Runtime verification", delegate { SetResult(PvpController.VerifyText()); });
            AddLayoutButton(page, "Concise status", delegate { SetResult(PvpController.HubStatus()); });
            AddLayoutButton(page, "Toggle validation logging", delegate { PvpControlApi.ToggleValidationLogging(); });
            AddLayoutButton(page, "Hide debug tab", delegate { PvpControlApi.HideDebugTab(); });
        }

        private static void UpdateValues()
        {
            if (!_built) return;
            if (_launcherText != null) _launcherText.text = PvpController.Enabled ? "PVP [ON]" : "PVP [OFF]";
            if (_titleText != null) _titleText.text = "ERENSHOR PvP  •  " + PvpController.HubStatus();
            if (_statusText != null)
            {
                _statusText.text =
                    "PvP: " + OnOff(PvpController.Enabled) + "\n" +
                    "Arranged challenges: " + OnOff(PvpController.ArrangedEnabled) + " (consensual)\n" +
                    "Wild ambushes: " + OnOff(PvpController.AmbushEnabled) + "\n" +
                    "Zone: " + PvpController.CurrentScene + "\n" +
                    "Zone safety: " + (PvpController.IsProtectedHere ? "PROTECTED" : "PvP eligible") + "\n" +
                    "Level range: +/-" + PvpController.LevelRangeHere;
            }
            bool enabled = PvpController.Enabled;
            bool arranged = PvpController.ArrangedEnabled;
            bool ambush = PvpController.AmbushEnabled;
            if (_enabledButton != null)
            {
                SetButtonText(_enabledButton, PvpUiPresentation.ToggleLabel("PvP Enabled", enabled));
                if (!_toggleVisualInitialized || _lastEnabledVisual != enabled) SetToggleButtonState(_enabledButton, enabled);
            }
            if (_arrangedButton != null)
            {
                SetButtonText(_arrangedButton, PvpUiPresentation.ToggleLabel("Arranged Challenges", arranged));
                if (!_toggleVisualInitialized || _lastArrangedVisual != arranged) SetToggleButtonState(_arrangedButton, arranged);
            }
            if (_ambushButton != null)
            {
                SetButtonText(_ambushButton, PvpUiPresentation.ToggleLabel("Wild Ambushes", ambush));
                if (!_toggleVisualInitialized || _lastAmbushVisual != ambush) SetToggleButtonState(_ambushButton, ambush);
            }
            _lastEnabledVisual = enabled;
            _lastArrangedVisual = arranged;
            _lastAmbushVisual = ambush;
            _toggleVisualInitialized = true;
            if (_ambushHereButton != null) SetButtonText(_ambushHereButton, PvpController.AmbushZoneListedHere ? "Stop ambushes here" : "Allow ambushes here");
            bool pending = PvpController.HasPending;
            if (_pendingText != null)
            {
                if (!pending) _pendingText.text = "None pending.";
                else
                {
                    PvpTeamPlan team = PvpController.PendingTeam;
                    int count = team == null ? 1 : team.Members.Count;
                    _pendingText.text = "Opponent: " + PvpController.PendingName + "\nParty size: " + count + "\nDecision expires: " + PvpController.PendingSecondsLeft + "s" +
                        (team == null ? string.Empty : "\n" + team.DescribeCompact());
                }
            }
            if (_acceptButton != null) _acceptButton.interactable = pending;
            if (_refuseButton != null) _refuseButton.interactable = pending;

            if (_fightText != null)
            {
                if (!PvpTemporaryCloneFactory.HasActiveTeam) _fightText.text = "No active PvP encounter.\nPlayer HP: " + PvpController.PlayerHealthText;
                else
                {
                    List<PvpRosterEntry> roster = PvpTemporaryCloneFactory.Roster();
                    System.Text.StringBuilder sb = new System.Text.StringBuilder();
                    sb.Append("Mode: ").Append(PvpTemporaryCloneFactory.ActiveMode.ToString().ToLowerInvariant()).Append('\n');
                    sb.Append("Player HP: ").Append(PvpController.PlayerHealthText).Append('\n');
                    for (int i = 0; i < roster.Count; i++)
                    {
                        PvpRosterEntry r = roster[i];
                        sb.Append(r.Name).Append(" L").Append(r.Level).Append(' ').Append(r.ClassName).Append(" — ").Append(r.HealthText).Append('\n');
                    }
                    _fightText.text = sb.ToString().TrimEnd();
                }
            }
            if (_fleeButton != null)
            {
                _fleeButton.interactable = PvpTemporaryCloneFactory.HasActiveTeam;
                SetButtonText(_fleeButton, _fleeConfirm && Time.unscaledTime <= _fleeConfirmUntil ? "Confirm Flee" : "Flee this fight");
                if (_fleeConfirm && Time.unscaledTime > _fleeConfirmUntil) _fleeConfirm = false;
            }
            if (_rulesText != null)
            {
                _rulesText.text =
                    "Your party: " + PvpController.DefenderCount + " (avg L" + PvpController.DefenderAverageLevel + ")\n" +
                    "Attacker count rule: " + PvpController.PartySizeRuleText + "\n" +
                    "Ambush chance: " + PvpController.AmbushChancePercent + "%\n" +
                    "Ambush gap: " + PvpController.AmbushMinimumMinutes + "–" + PvpController.AmbushMaximumMinutes + "m\n" +
                    "Next ambush: " + PvpController.NextAmbushText + "\n" +
                    "Next arranged offer: " + PvpController.NextOfferText;
            }
            if (_scoreText != null)
            {
                _scoreText.text =
                    "Arranged: " + PvpRecordService.ArrangedWins + "W / " + PvpRecordService.ArrangedLosses + "L\n" +
                    "Ambush: " + PvpRecordService.AmbushWins + "W / " + PvpRecordService.AmbushLosses + "L\n" +
                    "Escapes: " + PvpRecordService.Escapes + "\n" +
                    "Last: " + Safe(PvpRecordService.LastOpponent) + " — " + Safe(PvpRecordService.LastResult) + "\n" +
                    "Victory reward: " + (PvpRewardService.RewardsEnabled ? PvpRewardService.XpPercent + "% XP + gold" : "disabled") + "\n" +
                    "Reward cooldown: " + (PvpRewardService.CooldownMinutesRemaining == 0 ? "ready" : PvpRewardService.CooldownMinutesRemaining + "m");
            }
            if (_debugText != null) _debugText.text = "Validation logging: " + OnOff(PvpController.ValidationLogging) + "\nDeveloper spawn/probe commands remain command-only to keep production UI control-safe.";
            if (_debugTabButton != null) _debugTabButton.gameObject.SetActive(PvpController.ShowDebugTab);
            if (!PvpController.ShowDebugTab && _tab == PvpPanelTab.Debug) SelectTab(PvpPanelTab.Status);
        }

        private static void ApplyTabVisibility()
        {
            foreach (KeyValuePair<PvpPanelTab, GameObject> kv in Pages) if (kv.Value != null) kv.Value.SetActive(kv.Key == _tab);
            foreach (KeyValuePair<PvpPanelTab, Button> kv in TabButtons) if (kv.Value != null) kv.Value.interactable = kv.Key != _tab;
        }

        private static void ToggleCollapsed()
        {
            SetCollapsed(!_collapsed);
        }

        private static void SetCollapsed(bool collapsed)
        {
            if (_panel == null || _collapsed == collapsed) return;
            PvpDragGuard.ForceReleaseIfOwned();
            float oldHeight = _panel.sizeDelta.y;
            float oldY = _panel.anchoredPosition.y;
            _collapsed = collapsed;
            float newHeight = PvpWindowChromePolicy.Height(_collapsed);
            ResizePanel(_panel.sizeDelta.x, newHeight);
            _panel.anchoredPosition = new Vector2(_panel.anchoredPosition.x,
                PvpWindowChromePolicy.PreserveTopBottomY(oldY, oldHeight, newHeight));
            ApplyCollapsedVisibility();
            RebuildCollapseChevron();
            PersistPanelPosition();
            TouchActivation();
        }

        private static void ApplyCollapsedVisibility()
        {
            bool bodyVisible = !_collapsed;
            if (_tabs != null) _tabs.gameObject.SetActive(bodyVisible);
            if (_viewport != null) _viewport.gameObject.SetActive(bodyVisible);
            if (_footer != null) _footer.gameObject.SetActive(bodyVisible);
        }

        private static void RebuildCollapseChevron()
        {
            if (_collapseChevron == null) return;
            for (int i = _collapseChevron.childCount - 1; i >= 0; i--)
                if (_collapseChevron.GetChild(i).name == "Chevron") UnityEngine.Object.Destroy(_collapseChevron.GetChild(i).gameObject);
            StandaloneLauncherVisual.AddVerticalChevron(_collapseChevron, PvpWindowChromePolicy.ChevronPointsUp(_collapsed));
        }

        private static void HideAll()
        {
            // Visibility teardown is an ownership boundary. Release before disabling children so
            // no lost pointer-up/scene transition can leave Erenshor's camera drag flag stuck.
            PvpDragGuard.ForceReleaseIfOwned();
            if (_panelObject != null) _panelObject.SetActive(false);
            if (_launcherObject != null) _launcherObject.SetActive(false);
        }

        private static void TouchActivation()
        {
            try { _lastActivatedAt = Time.realtimeSinceStartup; }
            catch { _lastActivatedAt = 0d; }
        }

        private static void ClampAndApplyPositions(bool persist)
        {
            ApplyPanelPosition(persist);
            ApplyLauncherPosition(persist);
        }

        private static void ApplyPanelPosition(bool persist)
        {
            if (_panel == null) return;
            // Persisted coordinates always describe the expanded rect. The collapsed header derives
            // from that rect so screen changes cannot make expand/collapse drift vertically.
            PvpUiRect expanded = PvpUiGeometry.ResolvePanel(_panelNormX, _panelNormY, Screen.width, Screen.height, PanelWidth, PanelHeight);
            float displayHeight = PvpWindowChromePolicy.Height(_collapsed);
            float displayY = _collapsed
                ? PvpWindowChromePolicy.PreserveTopBottomY(expanded.Y, expanded.Height, displayHeight)
                : expanded.Y;
            ResizePanel(expanded.Width, displayHeight);
            _panel.anchoredPosition = new Vector2(expanded.X, displayY);
            PvpUiGeometry.Normalize(expanded, Screen.width, Screen.height, out _panelNormX, out _panelNormY);
            if (persist) PersistPanelPosition();
        }

        private static void ApplyLauncherPosition(bool persist)
        {
            if (_launcher == null) return;
            PvpUiRect r = PvpUiGeometry.ResolveLauncher(_launcherNormX, _launcherNormY, Screen.width, Screen.height, LauncherWidth, LauncherHeight);
            _launcher.anchoredPosition = new Vector2(r.X, r.Y);
            PvpUiGeometry.Normalize(r, Screen.width, Screen.height, out _launcherNormX, out _launcherNormY);
            if (persist) PersistLauncherPosition();
        }


        private static void ResizePanel(float width, float height)
        {
            if (_panel == null) return;
            _panel.sizeDelta = new Vector2(width, height);
            if (_header != null) { _header.sizeDelta = new Vector2(width, HeaderHeight); _header.anchoredPosition = new Vector2(0f, height - HeaderHeight); }
            if (_headerGrip != null) { _headerGrip.sizeDelta = new Vector2(Math.Max(80f, width - 68f), HeaderHeight); _headerGrip.anchoredPosition = new Vector2(34f, 0f); }
            if (_closeRect != null) _closeRect.anchoredPosition = new Vector2(Math.Max(4f, width - 34f), 5f);
            if (_tabs != null) { _tabs.sizeDelta = new Vector2(Math.Max(80f, width - 16f), TabHeight); _tabs.anchoredPosition = new Vector2(8f, height - HeaderHeight - TabHeight - 4f); }
            float tabWidth = Math.Max(16f, (width - 16f) / 5f);
            foreach (KeyValuePair<PvpPanelTab, Button> kv in TabButtons)
            {
                if (kv.Value == null) continue;
                int index = (int)kv.Key;
                RectTransform rt = kv.Value.transform as RectTransform;
                if (rt != null) { rt.sizeDelta = new Vector2(Math.Max(12f, tabWidth - 3f), TabHeight); rt.anchoredPosition = new Vector2(index * tabWidth, 0f); }
            }
            float viewportHeight = Math.Max(90f, height - HeaderHeight - TabHeight - 58f);
            if (_viewport != null) { _viewport.sizeDelta = new Vector2(Math.Max(80f, width - 16f), viewportHeight); _viewport.anchoredPosition = new Vector2(8f, 48f); }
            if (_content != null) _content.sizeDelta = new Vector2(0f, _content.sizeDelta.y);
            if (_footer != null) { _footer.sizeDelta = new Vector2(Math.Max(80f, width - 16f), 34f); _footer.anchoredPosition = new Vector2(8f, 8f); }
        }

        private static void PersistPanelPosition()
        {
            if (_panel == null) return;
            PvpUiRect display = new PvpUiRect(_panel.anchoredPosition.x, _panel.anchoredPosition.y, _panel.sizeDelta.x, _panel.sizeDelta.y);
            display = PvpUiGeometry.Clamp(display, Screen.width, Screen.height);
            _panel.anchoredPosition = new Vector2(display.X, display.Y);
            float expandedY = _collapsed
                ? PvpWindowChromePolicy.PreserveTopBottomY(display.Y, display.Height, PanelHeight)
                : display.Y;
            PvpUiRect expanded = PvpUiGeometry.Clamp(new PvpUiRect(display.X, expandedY, PanelWidth, PanelHeight), Screen.width, Screen.height);
            PvpUiGeometry.Normalize(expanded, Screen.width, Screen.height, out _panelNormX, out _panelNormY);
            if (_persistPanel != null) _persistPanel(_panelNormX, _panelNormY);
        }

        private static void PersistLauncherPosition()
        {
            if (_launcher == null) return;
            PvpUiRect r = new PvpUiRect(_launcher.anchoredPosition.x, _launcher.anchoredPosition.y, LauncherWidth, LauncherHeight);
            r = PvpUiGeometry.Clamp(r, Screen.width, Screen.height);
            _launcher.anchoredPosition = new Vector2(r.X, r.Y);
            PvpUiGeometry.Normalize(r, Screen.width, Screen.height, out _launcherNormX, out _launcherNormY);
            if (_persistLauncher != null) _persistLauncher(_launcherNormX, _launcherNormY);
        }

        private static GameObject CreatePanelObject(string name, Transform parent, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>(); image.color = color;
            CanvasGroup group = go.GetComponent<CanvasGroup>(); group.interactable = true; group.blocksRaycasts = true;
            return go;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 size, Vector2 pos)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f); rt.sizeDelta = size; rt.anchoredPosition = pos;
            return rt;
        }

        private static RectTransform CreatePage(RectTransform parent, PvpPanelTab tab)
        {
            GameObject go = new GameObject(tab + " Page", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            RectTransform rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0.5f, 1f); rt.sizeDelta = Vector2.zero;
            VerticalLayoutGroup layout = go.GetComponent<VerticalLayoutGroup>(); layout.spacing = 6f; layout.childControlHeight = true; layout.childControlWidth = true; layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = go.GetComponent<ContentSizeFitter>(); fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            LayoutElement le = go.AddComponent<LayoutElement>(); le.minHeight = 1f;
            Pages[tab] = go;
            return rt;
        }

        private static void BuildTabButton(RectTransform parent, PvpPanelTab tab, string label, int index)
        {
            float width = (PanelWidth - 16f) / 5f;
            RectTransform rt = CreateRect(tab + " Tab", parent, new Vector2(width - 3f, TabHeight), new Vector2(index * width, 0f));
            Button b = AddButton(rt, label, delegate { SelectTab(tab); });
            TabButtons[tab] = b;
            if (tab == PvpPanelTab.Debug) _debugTabButton = b;
        }

        private static void AddSectionHeader(RectTransform parent, string text)
        {
            TextMeshProUGUI t = AddLayoutText(parent, text, 13, 26f);
            t.color = TitleCyan;
            t.fontStyle = FontStyles.Bold;
        }

        private static TextMeshProUGUI AddLayoutText(RectTransform parent, string text, int size, float preferredHeight)
        {
            GameObject go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            RectTransform rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false);
            TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>(); tmp.text = text; tmp.fontSize = size; tmp.color = new Color32(224, 238, 244, 255); tmp.alignment = TextAlignmentOptions.TopLeft; tmp.enableWordWrapping = true; tmp.raycastTarget = false;
            LayoutElement le = go.GetComponent<LayoutElement>(); le.preferredHeight = preferredHeight;
            return tmp;
        }

        private static Button AddLayoutButton(RectTransform parent, string label, UnityEngine.Events.UnityAction callback)
        {
            GameObject go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            RectTransform rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false);
            LayoutElement le = go.GetComponent<LayoutElement>(); le.preferredHeight = 32f;
            Button b = go.GetComponent<Button>(); b.targetGraphic = go.GetComponent<Image>();
            b.onClick.AddListener(delegate { TouchActivation(); if (callback != null) callback(); });
            ApplyButtonStyle(b);
            AddText(rt, label, 13, TextAlignmentOptions.Center, Color.white);
            return b;
        }

        private static RectTransform AddButtonRow(RectTransform parent)
        {
            GameObject go = new GameObject("Button Row", typeof(RectTransform), typeof(LayoutElement));
            RectTransform rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false);
            LayoutElement le = go.GetComponent<LayoutElement>(); le.preferredHeight = 34f;
            return rt;
        }

        private static RectTransform CreateFillCell(RectTransform parent, int index, int count)
        {
            GameObject go = new GameObject("Cell", typeof(RectTransform));
            RectTransform rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false);
            rt.anchorMin = new Vector2((float)index / count, 0f); rt.anchorMax = new Vector2((float)(index + 1) / count, 1f); rt.offsetMin = new Vector2(index == 0 ? 0f : 3f, 0f); rt.offsetMax = new Vector2(index == count - 1 ? 0f : -3f, 0f);
            return rt;
        }

        private static void AddStepper(RectTransform parent, string label, UnityEngine.Events.UnityAction minus, UnityEngine.Events.UnityAction plus)
        {
            RectTransform row = AddButtonRow(parent);
            RectTransform labelRt = CreateFillCell(row, 0, 2);
            AddText(labelRt, label, 13, TextAlignmentOptions.MidlineLeft, HintCyan);
            RectTransform controls = CreateFillCell(row, 1, 2);
            AddButton(CreateFillCell(controls, 0, 2), "−", minus);
            AddButton(CreateFillCell(controls, 1, 2), "+", plus);
        }

        private static Button AddButton(RectTransform rt, string label, UnityEngine.Events.UnityAction callback)
        {
            Image image = rt.gameObject.GetComponent<Image>(); if (image == null) image = rt.gameObject.AddComponent<Image>();
            Button b = rt.gameObject.GetComponent<Button>(); if (b == null) b = rt.gameObject.AddComponent<Button>(); b.targetGraphic = image;
            b.onClick.AddListener(delegate { TouchActivation(); if (callback != null) callback(); });
            ApplyButtonStyle(b);
            AddText(rt, label, 13, TextAlignmentOptions.Center, Color.white);
            return b;
        }

        private static TextMeshProUGUI AddText(RectTransform parent, string text, int size, TextAlignmentOptions alignment, Color color)
        {
            GameObject go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false); rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>(); tmp.text = text; tmp.fontSize = size; tmp.alignment = alignment; tmp.color = color; tmp.raycastTarget = false; tmp.enableWordWrapping = true;
            return tmp;
        }

        private static void SetOffsets(RectTransform rt, float left, float bottom, float right, float top)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(left, bottom); rt.offsetMax = new Vector2(right, top);
        }

        private static void ApplyButtonStyle(Button button)
        {
            if (button == null || button.targetGraphic == null) return;
            button.targetGraphic.color = Color.white;
            ColorBlock colors = button.colors;
            colors.normalColor = ButtonFill;
            colors.highlightedColor = ButtonHover;
            colors.pressedColor = ButtonPressed;
            colors.selectedColor = ButtonHover;
            colors.disabledColor = ButtonDisabled;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.targetGraphic.CrossFadeColor(ButtonFill, 0f, true, true);
        }

        private static void SetToggleButtonState(Button button, bool enabled)
        {
            if (button == null || button.targetGraphic == null) return;
            Color normal = enabled ? ToggleOnFill : ButtonFill;
            ColorBlock colors = button.colors;
            colors.normalColor = normal;
            colors.highlightedColor = ButtonHover;
            colors.pressedColor = ButtonPressed;
            colors.selectedColor = ButtonHover;
            colors.disabledColor = ButtonDisabled;
            button.colors = colors;
            if (button.interactable) button.targetGraphic.CrossFadeColor(normal, 0f, true, true);
        }

        private static void SetButtonText(Button button, string text)
        {
            if (button == null) return;
            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(); if (label != null) label.text = text;
        }

        private static void SetResult(string value)
        {
            if (_resultText != null) _resultText.text = string.IsNullOrWhiteSpace(value) ? string.Empty : value;
        }

        private static string OnOff(bool value) { return value ? "ON" : "OFF"; }
        private static string Safe(string value) { return string.IsNullOrWhiteSpace(value) ? "none" : value; }
    }
}
