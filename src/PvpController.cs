using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ErenshorPvP
{
    internal static class PvpController
    {
        // Wired by ErenshorPvPPlugin to Lunaris's Config.Save(); called explicitly after every
        // settings mutation below, since native Lunaris config (unlike BepInEx's ConfigEntry)
        // does not persist a .Value write to disk on its own.
        internal static Action SaveSettings;
        internal static bool RuntimeHooksAvailable;

        private static PvpConfigEntry<bool> _enabled;
        private static PvpConfigEntry<bool> _arrangedEnabled;
        private static PvpConfigEntry<int> _offerCooldownMinutes;
        private static PvpConfigEntry<bool> _ambushEnabled;
        private static PvpConfigEntry<string> _ambushZones;
        private static PvpConfigEntry<int> _ambushMinimumMinutes;
        private static PvpConfigEntry<int> _ambushMaximumMinutes;
        private static PvpConfigEntry<int> _ambushChancePercent;
        private static PvpConfigEntry<string> _protectedZones;
        private static PvpConfigEntry<string> _highRiskZones;
        private static PvpConfigEntry<int> _standardRange;
        private static PvpConfigEntry<int> _highRiskRange;
        private static PvpConfigEntry<float> _panelNormalizedX;
        private static PvpConfigEntry<float> _panelNormalizedY;
        private static PvpConfigEntry<bool> _showDebugTab;
        private static PvpConfigEntry<bool> _showQuickToggle;
        private static PvpConfigEntry<float> _launcherNormalizedX;
        private static PvpConfigEntry<float> _launcherNormalizedY;
        private static PvpConfigEntry<bool> _validationLogging;
        private static bool _pendingExternalOpen;
        private static bool _pendingExternalClose;
        private static float _nextScan;
        private static float _nextOffer;
        private static float _nextAmbush;
        private static string _pendingName;
        private static PvpTeamPlan _pendingTeam;
        private static string _pendingMatchId;
        private static float _pendingExpires;
        private static PvpEncounterFlavor _pendingFlavor;
        private static readonly Dictionary<string, float> Cooldowns = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private static bool _open;
        private static PvpMatchLifecyclePolicy _lifecycle = new PvpMatchLifecyclePolicy(false);
        private static int _matchCountdownValue;
        private static float _matchCountdownNextAt;
        private static string _countdownMatchId = string.Empty;

        internal static bool Enabled { get { return _enabled != null && _enabled.Value; } }
        internal static bool ShowLauncherPreference { get { return _showQuickToggle == null || _showQuickToggle.Value; } }
        internal static bool ValidationLogging { get { return _validationLogging != null && _validationLogging.Value; } }
        internal static bool HasPending { get { return !string.IsNullOrEmpty(_pendingMatchId) && Time.unscaledTime < _pendingExpires; } }

        internal static void Initialize(PvpSettings settings)
        {
            _enabled = new PvpConfigEntry<bool>(() => settings.PvpEnabled, v => settings.PvpEnabled = v);
            _arrangedEnabled = new PvpConfigEntry<bool>(() => settings.ArrangedChallenges, v => settings.ArrangedChallenges = v);
            _offerCooldownMinutes = new PvpConfigEntry<int>(() => settings.OfferCooldownMinutes, v => settings.OfferCooldownMinutes = v);
            _ambushEnabled = new PvpConfigEntry<bool>(() => settings.AmbushEnabled, v => settings.AmbushEnabled = v);
            _ambushZones = new PvpConfigEntry<string>(() => settings.AmbushZones, v => settings.AmbushZones = v);
            _ambushMinimumMinutes = new PvpConfigEntry<int>(() => settings.AmbushMinimumMinutes, v => settings.AmbushMinimumMinutes = v);
            _ambushMaximumMinutes = new PvpConfigEntry<int>(() => settings.AmbushMaximumMinutes, v => settings.AmbushMaximumMinutes = v);
            _ambushChancePercent = new PvpConfigEntry<int>(() => settings.AmbushOpportunityChancePercent, v => settings.AmbushOpportunityChancePercent = v);
            _protectedZones = new PvpConfigEntry<string>(() => settings.ProtectedZones, v => settings.ProtectedZones = v);
            _highRiskZones = new PvpConfigEntry<string>(() => settings.HighRiskZones, v => settings.HighRiskZones = v);
            _standardRange = new PvpConfigEntry<int>(() => settings.StandardLevelRange, v => settings.StandardLevelRange = v);
            _highRiskRange = new PvpConfigEntry<int>(() => settings.HighRiskLevelRange, v => settings.HighRiskLevelRange = v);
            _panelNormalizedX = new PvpConfigEntry<float>(() => settings.PanelNormalizedX, v => settings.PanelNormalizedX = v);
            _panelNormalizedY = new PvpConfigEntry<float>(() => settings.PanelNormalizedY, v => settings.PanelNormalizedY = v);
            _showDebugTab = new PvpConfigEntry<bool>(() => settings.ShowTestTab, v => settings.ShowTestTab = v);
            _showQuickToggle = new PvpConfigEntry<bool>(() => settings.ShowQuickToggle, v => settings.ShowQuickToggle = v);
            _launcherNormalizedX = new PvpConfigEntry<float>(() => settings.LauncherNormalizedX, v => settings.LauncherNormalizedX = v);
            _launcherNormalizedY = new PvpConfigEntry<float>(() => settings.LauncherNormalizedY, v => settings.LauncherNormalizedY = v);
            _validationLogging = new PvpConfigEntry<bool>(() => settings.ValidationLogging, v => settings.ValidationLogging = v);
            PvpRewardService.Initialize(settings);
            PvpRecordService.Initialize(settings);
            _lifecycle = new PvpMatchLifecyclePolicy(Enabled);
            PvpPanel.ConfigurePosition(_panelNormalizedX.Value, _panelNormalizedY.Value, _launcherNormalizedX.Value, _launcherNormalizedY.Value, PersistPanelPosition, PersistLauncherPosition);
            _nextScan = Time.unscaledTime + 12f;
            ScheduleNextAmbush(Time.unscaledTime);
        }

        // Called after every settings mutation in this class and the two config-owning
        // services it initializes, so native Lunaris config is actually written to disk
        // (BepInEx's ConfigEntry persisted a .Value write on its own; Lunaris does not).
        internal static void PersistSettings()
        {
            TryPersistSettings();
        }

        // Reward claiming needs to know whether its durable marker reached Lunaris before it
        // touches inventory. Other settings remain best-effort for backwards compatibility.
        internal static bool TryPersistSettings()
        {
            if (SaveSettings == null) return false;
            try { SaveSettings(); return true; } catch { return false; }
        }

        internal static void Tick()
        {
            // External panel open/close remains available even when a game update invalidates one
            // of the required Harmony hooks. Gameplay itself fails closed below.
            if (_pendingExternalClose)
            {
                _pendingExternalClose = false;
                ClosePanel();
            }
            if (_pendingExternalOpen)
            {
                _pendingExternalOpen = false;
                _open = true;
            }
            if (!RuntimeHooksAvailable)
            {
                ClearPending();
                return;
            }
            if (!IsGameplayReady())
            {
                ClearPending();
                ClosePanel();
                if (PvpTemporaryCloneFactory.HasActiveTeam) PvpTemporaryCloneFactory.Despawn("game_not_ready");
                return;
            }
            float now = Time.unscaledTime;
            if (!Enabled)
            {
                ClearPending();
                if (PvpTemporaryCloneFactory.HasActiveTeam) PvpTemporaryCloneFactory.Despawn("pvp_disabled");
                return;
            }
            PvpTemporaryCloneFactory.Tick();
            if (_lifecycle.State == PvpMatchLifecycleState.Countdown)
            {
                TickMatchCountdown(now);
                return;
            }
            if (_lifecycle.State == PvpMatchLifecycleState.Preparing) return;
            if (HasPending && now >= _pendingExpires) ClearPending();
            if (HasPending || now < _nextScan || now < _nextOffer) return;
            _nextScan = now + 12f;
            string scene = SceneManager.GetActiveScene().name;
            PvpEncounterMode mode = PvpEncounterMode.Arranged;
            if (AmbushAllowed(scene) && now >= _nextAmbush)
            {
                ScheduleNextAmbush(now);
                if (UnityEngine.Random.Range(0, 100) < Math.Max(5, Math.Min(100, _ambushChancePercent.Value))) mode = PvpEncounterMode.Ambush;
            }
            // Skip quietly rather than letting TryOffer log a block every scan.
            if (mode == PvpEncounterMode.Arranged && !ArrangedEnabled) return;
            TryOffer(false, -1, mode);
        }

        private static void TryOffer(bool forced, int attackerCount, PvpEncounterMode mode)
        {
            float now = Time.unscaledTime;
            if (!IsGameplayReady()) { Diagnostic("scan blocked: game_not_ready"); return; }
            if (PvpCompatibility.IsCoopSession()) { Diagnostic("scan blocked: coop_session_not_supported"); return; }
            if (PvpTemporaryCloneFactory.HasActiveTeam) { Diagnostic("scan blocked: pvp_team_active"); return; }
            if (!Enabled) { Say("[Erenshor PvP] Turn PvP on first: /epvp on"); return; }
            if (HasPending) { Say("[Erenshor PvP] A challenge is already waiting."); return; }
            string scene = SceneManager.GetActiveScene().name;
            if (IsProtectedScene(scene)) { Diagnostic("scan protected_zone scene=" + scene); return; }
            if (mode == PvpEncounterMode.Ambush && !AmbushAllowed(scene))
            { Diagnostic("scan ambush_zone_blocked scene=" + scene); if (forced) Say("[Erenshor PvP] Wild ambushes are not allowed in " + scene + "."); return; }
            if (mode == PvpEncounterMode.Arranged && !ArrangedEnabled)
            { Diagnostic("scan arranged_disabled"); if (forced) Say("[Erenshor PvP] Arranged challenges are switched off."); return; }
            if (IsZoning()) { Diagnostic("scan zoning scene=" + scene); return; }

            PvpTeamPlan team;
            PvpEligibilityDecision decision;
            if (!TrySelectOffMap(attackerCount, null, teamOut: out team, decisionOut: out decision))
            {
                Diagnostic("scan no_candidate scene=" + scene + " reason=" + PvpPolicy.Token(decision));
                if (forced) Say("[Erenshor PvP] No eligible off-map party for this request (" + PvpPolicy.Token(decision) + ").");
                return;
            }
            string clearanceReason;
            if (!PvpTemporaryCloneFactory.CanSpawnClearTeam(team.Members.Count, out clearanceReason))
            {
                Diagnostic("scan no_clear_spawn reason=" + clearanceReason);
                if (forced) Say("[Erenshor PvP] Move to a clear combat area before starting PvP: " + clearanceReason + ".");
                return;
            }
            string matchId = Guid.NewGuid().ToString("N");
            PvpEncounterFlavor flavor = PvpEncounterFlavorFactory.Create(mode, team, UnityEngine.Random.Range(int.MinValue, int.MaxValue), PvpCompatibility.IsVerifiedHuntCampActive());
            _nextOffer = now + Math.Max(2, Math.Min(60, _offerCooldownMinutes.Value)) * 60f;
            if (mode == PvpEncounterMode.Ambush)
            {
                Say("[Erenshor PvP] AMBUSH: " + flavor.SystemLine);
                Say("[PvP] Incoming: " + team.DescribeCompact()); Say("[PvP] " + flavor.LeaderLine);
                Publish("pvp_ambush", matchId, team.LeaderName, scene, "ambush", flavor.Motive);
                StartEncounter(team, matchId, team.LeaderName, mode, flavor);
                return;
            }
            _pendingTeam = team; _pendingName = team.LeaderName; _pendingMatchId = matchId; _pendingFlavor = flavor;
            _lifecycle.Queue(matchId);
            _pendingExpires = now + 30f;
            _open = true;
            PvpPanel.ShowPendingChallenge();
            Publish("pvp_challenge", _pendingMatchId, _pendingName, scene, "arranged", flavor.Motive);
            Say("[Erenshor PvP] ARRANGED: " + flavor.SystemLine + " Party of " + _pendingName + " (" + team.Members.Count + ") wishes to fight. Open the PvP panel or use /epvp accept|refuse.");
            Say("[PvP] Incoming: " + team.DescribeCompact());
            Say("[PvP] " + flavor.LeaderLine);
        }

        internal static bool HandleCommand(string argument)
        {
            string option = (argument ?? string.Empty).Trim();
            if (option.StartsWith("/epvp", StringComparison.OrdinalIgnoreCase)) option = option.Length <= 5 ? string.Empty : option.Substring(5).Trim();
            if (option.StartsWith("epvp ", StringComparison.OrdinalIgnoreCase)) option = option.Substring(4).Trim();
            if (option.Equals("on", StringComparison.OrdinalIgnoreCase)) { SetEnabled(true); _open = true; }
            else if (option.Equals("off", StringComparison.OrdinalIgnoreCase)) { SetEnabled(false); }
            else if (option.Equals("debug", StringComparison.OrdinalIgnoreCase))
            {
                if (_showDebugTab != null) { _showDebugTab.Value = !_showDebugTab.Value; PersistSettings(); }
                if (ShowDebugTab) { _open = true; PvpPanel.SelectTab(PvpPanelTab.Debug); }
                else PvpPanel.SelectTab(PvpPanelTab.Status);
                Say("[Erenshor PvP] Test tab " + (ShowDebugTab ? "shown." : "hidden."));
                return true;
            }
            else if (option.Equals("panelreset", StringComparison.OrdinalIgnoreCase))
            { PvpPanel.ResetPosition(); _open = true; Say("[Erenshor PvP] Panel moved back to its default position."); return true; }
            else if (option.Equals("validation", StringComparison.OrdinalIgnoreCase) || option.StartsWith("validation ", StringComparison.OrdinalIgnoreCase))
            { Say(SetValidationLogging(option)); return true; }
            else if (option.Equals("accept", StringComparison.OrdinalIgnoreCase)) Accept();
            else if (option.Equals("refuse", StringComparison.OrdinalIgnoreCase) || option.Equals("decline", StringComparison.OrdinalIgnoreCase)) Refuse();
            else if (option.Equals("selftest", StringComparison.OrdinalIgnoreCase)) { Say("[Erenshor PvP] " + SelfTest()); return true; }
            else if (option.Equals("spawnprobe", StringComparison.OrdinalIgnoreCase)) { Say(PvpSpawnCapability.InspectLiveState()); return true; }
            else if (option.Equals("spawnclone", StringComparison.OrdinalIgnoreCase)) { Say(PvpTemporaryCloneFactory.SpawnVisualClone()); return true; }
            else if (option.Equals("targetclone", StringComparison.OrdinalIgnoreCase)) { Say(PvpTemporaryCloneFactory.BeginTargetingTest()); return true; }
            else if (option.Equals("fightclone", StringComparison.OrdinalIgnoreCase)) { Say(PvpTemporaryCloneFactory.BeginLethalFight()); return true; }
            else if (option.Equals("clonestatus", StringComparison.OrdinalIgnoreCase)) { Say(PvpTemporaryCloneFactory.CloneStatus()); return true; }
            else if (option.Equals("team", StringComparison.OrdinalIgnoreCase)) { Say(TeamText()); return true; }
            else if (option.Equals("verify", StringComparison.OrdinalIgnoreCase))
            { Say(PvpTemporaryCloneFactory.VerifyRuntime() + " " + PvpCombatContainment.VerifyRuntime()); return true; }
            else if (option.Equals("diagnose", StringComparison.OrdinalIgnoreCase)) { Say(Diagnostics()); return true; }
            else if (option.Equals("ambushzones", StringComparison.OrdinalIgnoreCase)) { Say(AmbushZonesText()); return true; }
            else if (option.StartsWith("ambushhere", StringComparison.OrdinalIgnoreCase))
            { Say(SetAmbushHere(option)); return true; }
            // Checked after ambushzones/ambushhere; the trailing space keeps them distinct.
            else if (option.Equals("arranged", StringComparison.OrdinalIgnoreCase) || option.StartsWith("arranged ", StringComparison.OrdinalIgnoreCase))
            { Say(SetConsentSwitch(option, true)); return true; }
            else if (option.Equals("ambush", StringComparison.OrdinalIgnoreCase) || option.StartsWith("ambush ", StringComparison.OrdinalIgnoreCase))
            { Say(SetConsentSwitch(option, false)); return true; }
            else if (option.Equals("flee", StringComparison.OrdinalIgnoreCase) || option.Equals("escape", StringComparison.OrdinalIgnoreCase))
            { Say(PvpTemporaryCloneFactory.Flee()); return true; }
            else if (option.Equals("despawn", StringComparison.OrdinalIgnoreCase)) { Say(PvpTemporaryCloneFactory.Despawn("manual")); return true; }
            else if (option.Equals("force", StringComparison.OrdinalIgnoreCase) || option.StartsWith("force ", StringComparison.OrdinalIgnoreCase))
            {
                int requested = -1; PvpEncounterMode mode = PvpEncounterMode.Arranged;
                string[] forceParts = option.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                int index = 1;
                if (forceParts.Length > index && (forceParts[index].Equals("ambush", StringComparison.OrdinalIgnoreCase) ||
                    forceParts[index].Equals("arranged", StringComparison.OrdinalIgnoreCase) || forceParts[index].Equals("challenge", StringComparison.OrdinalIgnoreCase)))
                { mode = forceParts[index].Equals("ambush", StringComparison.OrdinalIgnoreCase) ? PvpEncounterMode.Ambush : PvpEncounterMode.Arranged; index++; }
                if (forceParts.Length > index && (!int.TryParse(forceParts[index], out requested) || requested < 1 || requested > 5))
                { Say("[Erenshor PvP] Usage: /epvp force [arranged|ambush] [attackers 1-5]"); return true; }
                if (forceParts.Length > index + 1) { Say("[Erenshor PvP] Usage: /epvp force [arranged|ambush] [attackers 1-5]"); return true; }
                TryOffer(true, requested, mode); return true;
            }
            else if (option.StartsWith("plan", StringComparison.OrdinalIgnoreCase)) { Say(Plan(option)); return true; }
            else _open = true;
            Say(Status());
            return true;
        }

        internal static void RefreshUi()
        {
            bool ready = IsGameplayReady();
            bool showLauncher = SuiteLauncherPolicy.ShouldShow(ready, SuiteUiPolicy.IsHubAvailable(), SuiteBridgeRegistered, ShowLauncherPreference);
            PvpPanel.Tick(ready && _open, showLauncher);
        }

        internal static bool ArrangedEnabled { get { return _arrangedEnabled != null && _arrangedEnabled.Value; } }
        internal static bool AmbushEnabled { get { return _ambushEnabled != null && _ambushEnabled.Value; } }
        internal static bool ShowDebugTab { get { return _showDebugTab != null && _showDebugTab.Value; } }
        internal static string CurrentScene { get { try { return SceneManager.GetActiveScene().name ?? string.Empty; } catch { return string.Empty; } } }
        internal static bool IsProtectedHere { get { return IsProtectedScene(CurrentScene); } }
        internal static bool AmbushAllowedHere { get { return AmbushAllowed(CurrentScene); } }
        internal static bool AmbushZoneListedHere { get { return IsListed(CurrentScene, _ambushZones == null ? string.Empty : _ambushZones.Value); } }
        internal static int LevelRangeHere { get { return RangeForScene(CurrentScene); } }
        internal static int DefenderCount { get { return CurrentDefenderCount(); } }
        internal static int DefenderAverageLevel { get { int count; int level; CurrentDefenderParty(out count, out level); return level; } }
        internal static string PendingName { get { return _pendingName ?? string.Empty; } }
        internal static PvpTeamPlan PendingTeam { get { return _pendingTeam; } }
        internal static PvpEncounterFlavor PendingFlavor { get { return _pendingFlavor; } }
        internal static int PendingSecondsLeft { get { return HasPending ? Math.Max(0, Mathf.CeilToInt(_pendingExpires - Time.unscaledTime)) : 0; } }
        internal static int AmbushChancePercent { get { return Math.Max(5, Math.Min(100, _ambushChancePercent == null ? 50 : _ambushChancePercent.Value)); } }
        internal static int AmbushMinimumMinutes { get { return Math.Max(8, Math.Min(120, _ambushMinimumMinutes == null ? 15 : _ambushMinimumMinutes.Value)); } }
        internal static int AmbushMaximumMinutes { get { return Math.Max(AmbushMinimumMinutes, Math.Min(240, _ambushMaximumMinutes == null ? 35 : _ambushMaximumMinutes.Value)); } }
        internal static string NextAmbushText { get { return AmbushAllowedHere ? Countdown(_nextAmbush) : "not here"; } }
        internal static string NextOfferText { get { return Countdown(_nextOffer); } }

        internal static string PlayerHealthText
        {
            get
            {
                try
                {
                    Character player = GameData.PlayerControl == null ? null : GameData.PlayerControl.Myself;
                    if (player == null || player.MyStats == null) return "?";
                    return player.MyStats.CurrentHP + "/" + player.MyStats.CurrentMaxHP;
                }
                catch { return "?"; }
            }
        }

        // Mirrors PvpTeamPlanner.DesiredSize so the panel states the live rule, not a guess.
        internal static string PartySizeRuleText
        {
            get
            {
                int defenders = CurrentDefenderCount();
                if (defenders == 1) return "1-5";
                if (defenders == 2) return "1-3";
                if (defenders == 3) return "3-5";
                return "5";
            }
        }

        internal static void SetShowLauncherPreference(bool value)
        {
            if (_showQuickToggle == null) return;
            _showQuickToggle.Value = value;
            PersistSettings();
        }

        internal static void SetEnabled(bool value)
        {
            if (_enabled == null) return;
            _enabled.Value = value;
            PersistSettings();
            if (!value || _lifecycle.State == PvpMatchLifecycleState.Disabled) _lifecycle.SetEnabled(value);
            if (!value)
            {
                ClearPending();
                if (PvpTemporaryCloneFactory.HasActiveTeam) PvpTemporaryCloneFactory.Despawn("pvp_disabled");
            }
        }

        internal static void SetArrangedEnabled(bool value)
        {
            if (_arrangedEnabled == null) return;
            _arrangedEnabled.Value = value;
            PersistSettings();
            if (!value) ClearPending();
        }

        internal static void SetAmbushEnabled(bool value)
        {
            if (_ambushEnabled == null) return;
            _ambushEnabled.Value = value;
            PersistSettings();
            if (value) ScheduleNextAmbush(Time.unscaledTime);
        }

        internal static void AdjustAmbushChance(int delta)
        {
            if (_ambushChancePercent == null) return;
            _ambushChancePercent.Value = Math.Max(5, Math.Min(100, AmbushChancePercent + (delta * 5)));
            PersistSettings();
        }

        internal static void AdjustAmbushMinimum(int delta)
        {
            if (_ambushMinimumMinutes == null) return;
            _ambushMinimumMinutes.Value = Math.Max(8, Math.Min(120, AmbushMinimumMinutes + (delta * 5)));
            if (_ambushMaximumMinutes != null && _ambushMaximumMinutes.Value < _ambushMinimumMinutes.Value)
                _ambushMaximumMinutes.Value = _ambushMinimumMinutes.Value;
            PersistSettings();
        }

        internal static void AdjustAmbushMaximum(int delta)
        {
            if (_ambushMaximumMinutes == null) return;
            _ambushMaximumMinutes.Value = Math.Max(AmbushMinimumMinutes, Math.Min(240, AmbushMaximumMinutes + (delta * 5)));
            PersistSettings();
        }

        internal static void ForceOffer(PvpEncounterMode mode, int attackers)
        {
            TryOffer(true, attackers < 1 || attackers > 5 ? -1 : attackers, mode);
        }

        // Optional standalone-mod contract. This is deliberately a request, not a command:
        // every normal PvP safety and matchmaking rule is re-evaluated here.
        internal static string RequestNamedAmbush(string preferredLeader, string source)
        {
            string leader = (preferredLeader ?? string.Empty).Trim();
            if (leader.Length == 0 || leader.Length > 80) return "blocked:invalid_leader";
            float now = Time.unscaledTime;
            if (!IsGameplayReady()) return "blocked:game_not_ready";
            if (!Enabled) return "blocked:pvp_disabled";
            if (PvpCompatibility.IsCoopSession()) return "blocked:coop_session_not_supported";
            if (PvpTemporaryCloneFactory.HasActiveTeam) return "blocked:pvp_team_active";
            if (HasPending) return "blocked:challenge_pending";
            if (now < _nextOffer) return "blocked:global_cooldown";
            string scene = SceneManager.GetActiveScene().name;
            if (IsProtectedScene(scene)) return "blocked:protected_zone";
            if (!AmbushAllowed(scene)) return "blocked:ambush_not_allowed_here";
            if (IsZoning()) return "blocked:zoning";

            PvpTeamPlan team; PvpEligibilityDecision decision;
            if (!TrySelectOffMap(-1, leader, out team, out decision)) return "blocked:" + PvpPolicy.Token(decision);
            string clearanceReason;
            if (!PvpTemporaryCloneFactory.CanSpawnClearTeam(team.Members.Count, out clearanceReason))
                return "blocked:no_clear_spawn_" + Normalize(clearanceReason);

            string matchId = Guid.NewGuid().ToString("N");
            PvpEncounterFlavor flavor = new PvpEncounterFlavor("nemesis_grudge",
                leader + " has tracked down a persistent rival.", leader + ": found you. Let's settle this.");
            _nextOffer = now + Math.Max(2, Math.Min(60, _offerCooldownMinutes.Value)) * 60f;
            Say("[Erenshor PvP] NEMESIS AMBUSH: " + leader + " has tracked you down.");
            Say("[PvP] Incoming: " + team.DescribeCompact());
            Publish("pvp_ambush", matchId, team.LeaderName, scene, "ambush", "nemesis_grudge");
            StartEncounter(team, matchId, team.LeaderName, PvpEncounterMode.Ambush, flavor);
            PvpDiagnostics.Log("external_request source=" + Normalize(source) + "; leader=" + leader + "; result=started; match=" + matchId.Substring(0, 8));
            return PvpTemporaryCloneFactory.HasActiveTeam ? "started:" + matchId : "blocked:spawn_or_combat_start_failed";
        }

        internal static string VerifyText()
        {
            return PvpTemporaryCloneFactory.VerifyRuntime() + " " + PvpCombatContainment.VerifyRuntime();
        }

        internal static string DiagnoseText() { return Diagnostics(); }

        // Set by ErenshorPvPPlugin after PvpSuiteAuraProvider.Register() completes without
        // throwing. Never assumed true merely because Suite Hub is present in-scene.
        internal static bool SuiteBridgeRegistered;

        internal static bool PanelOpen { get { return _open; } }
        internal static void RequestOpenPanel() { _pendingExternalOpen = true; }
        internal static void RequestClosePanel() { _pendingExternalClose = true; }
        internal static void ResetPanelPosition() { PvpPanel.ResetPosition(); }
        internal static void ResetLauncherPosition()
        {
            if (_launcherNormalizedX != null) _launcherNormalizedX.Value = PvpUiGeometry.Unset;
            if (_launcherNormalizedY != null) _launcherNormalizedY.Value = PvpUiGeometry.Unset;
            PvpPanel.ResetLauncherPosition();
            PersistSettings();
        }

        internal static void ClosePanel() { _open = false; PvpPanel.Close(); }

        private static string Countdown(float deadline)
        {
            int seconds = Mathf.CeilToInt(deadline - Time.unscaledTime);
            if (seconds <= 0) return "ready";
            if (seconds < 90) return seconds + "s";
            return Mathf.CeilToInt(seconds / 60f) + "m";
        }

        private static void PersistPanelPosition(float x, float y)
        {
            if (_panelNormalizedX != null) _panelNormalizedX.Value = x;
            if (_panelNormalizedY != null) _panelNormalizedY.Value = y;
            PersistSettings();
        }

        private static void PersistLauncherPosition(float x, float y)
        {
            if (_launcherNormalizedX != null) _launcherNormalizedX.Value = x;
            if (_launcherNormalizedY != null) _launcherNormalizedY.Value = y;
            PersistSettings();
        }

        internal static string Status()
        {
            return "[Erenshor PvP] " + (Enabled ? "ON" : "OFF") + "; zone=" + SceneManager.GetActiveScene().name +
                "; protected=" + IsProtectedScene(SceneManager.GetActiveScene().name) + "; coop_blocked=" + PvpCompatibility.IsCoopSession() +
                "; ambush_allowed=" + AmbushAllowed(SceneManager.GetActiveScene().name) +
                "; " + PvpRewardService.Describe() + "; " + PvpRecordService.Describe();
        }

        // Concise status for the Suite Hub descriptor, which validates status text to at most 240
        // characters (see ErenshorSuiteHub's SuiteDescriptorValidation.ValidateModule). Status()
        // above regularly exceeds that once PvpRewardService/PvpRecordService.Describe() are
        // appended, which rejected the whole descriptor ("status too long") and hid PvP from the
        // Hub entirely. Detailed match/combat/reward data belongs in the PvP panel, not the Hub
        // summary line, so this intentionally reports only enabled state + a coarse activity word.
        internal static string HubStatus()
        {
            return PvpHubPresentation.Build(Enabled, PvpTemporaryCloneFactory.HasActiveTeam);
        }

        internal static string SelfTest()
        {
            string eligibility = PvpPolicy.RunSelfTests();
            return eligibility.StartsWith("PASS", StringComparison.Ordinal) ? eligibility + "; " + ErenshorPvpApi.RunSelfTests() + "; " + PvpMatchmakingPolicy.RunSelfTests() + "; " + PvpTeamPlanner.RunSelfTests() + "; " + PvpCombatContainment.RunSelfTests() + "; " + PvpTemporaryCloneFactory.RunSpawnPolicySelfTests() + "; " + PvpEncounterFlavorFactory.RunSelfTests() + "; " + PvpRewardService.RunSelfTests() + "; " + PvpPanel.RunSelfTests() : eligibility;
        }

        private static string Diagnostics()
        {
            string scene = SceneManager.GetActiveScene().name;
            int offMap = 0; int sameZone = 0;
            string spawnReason; bool clearSpawn = PvpTemporaryCloneFactory.CanSpawnClearTeam(5, out spawnReason);
            try
            {
                HashSet<SimPlayerTracking> active = new HashSet<SimPlayerTracking>();
                foreach (SimPlayer sim in UnityEngine.Object.FindObjectsOfType<SimPlayer>()) if (sim != null && sim.MySimTracking != null) active.Add(sim.MySimTracking);
                if (GameData.SimMngr != null && GameData.SimMngr.Sims != null)
                    foreach (SimPlayerTracking tracking in GameData.SimMngr.Sims)
                    {
                        if (tracking == null) continue;
                        if (active.Contains(tracking) || TrackingIsInScene(tracking, scene)) sameZone++;
                        else offMap++;
                    }
            }
            catch { }
            return "[Erenshor PvP] diagnose ready=" + IsGameplayReady() + "; scene=" + scene + "; protected=" + IsProtectedScene(scene) +
                "; ambush_allowed=" + AmbushAllowed(scene) + "; next_ambush_seconds=" + Math.Max(0, Mathf.RoundToInt(_nextAmbush - Time.unscaledTime)) +
                "; hunt_camp=" + PvpCompatibility.IsVerifiedHuntCampActive() +
                "; zoning=" + IsZoning() + "; coop=" + PvpCompatibility.IsCoopSession() + "; defenders=" + CurrentDefenderCount() +
                "; defender_avg_level=" + DefenderAverageLevel + "; phase=" + _lifecycle.State.ToString().ToLowerInvariant() +
                "; clear_spawn_5=" + clearSpawn + (clearSpawn ? string.Empty : "; clear_spawn_reason=" + spawnReason) +
                "; off_map_profiles=" + offMap + "; same_zone_profiles=" + sameZone + "; " + PvpTemporaryCloneFactory.DiagnosticStatus();
        }

        internal static void SceneTransition()
        {
            PvpPanel.ReleaseDrag();
            ClearPending();
            PvpTemporaryCloneFactory.Despawn("scene_transition");
            // A scene transition never resumes the interrupted encounter or immediately rolls a
            // replacement. A manual force request remains available after the destination loads.
            _nextScan = Time.unscaledTime + 300f;
            if (_nextOffer < Time.unscaledTime + 300f) _nextOffer = Time.unscaledTime + 300f;
            if (_nextAmbush < Time.unscaledTime + 300f) _nextAmbush = Time.unscaledTime + 300f;
        }
        // Hot unload releases retained UI, drag ownership, proxy state, and optional bridge state.
        internal static void Shutdown() { ClearPending(); ResetMatchCountdown(); PvpTemporaryCloneFactory.Shutdown(); _open = false; PvpPanel.Dispose(); _pendingExternalOpen = false; _pendingExternalClose = false; SuiteBridgeRegistered = false; SuiteUiPolicy.Reset(); }

        private static string Plan(string option)
        {
            string[] pieces = option.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (pieces.Length < 3) return "[Erenshor PvP] Usage: /epvp plan <yourParty 1-5> <attackerParty 1-5> [yourAvgLevel] [attackerAvgLevel]";
            int defenders, attackers, defenderLevel = 0, attackerLevel = 0;
            if (!int.TryParse(pieces[1], out defenders) || !int.TryParse(pieces[2], out attackers) ||
                (pieces.Length > 3 && !int.TryParse(pieces[3], out defenderLevel)) ||
                (pieces.Length > 4 && !int.TryParse(pieces[4], out attackerLevel)))
                return "[Erenshor PvP] Party sizes and levels must be whole numbers.";
            return PlanText(defenders, attackers, defenderLevel, attackerLevel);
        }

        // Shared by `/epvp plan` and the RULES tab simulator.
        internal static string PlanText(int defenders, int attackers, int defenderLevel, int attackerLevel)
        {
            PvpMatchDecision result = PvpMatchmakingPolicy.Evaluate(new PvpMatchInput { DefenderPartySize = defenders, AttackerPartySize = attackers, DefenderAverageLevel = defenderLevel, AttackerAverageLevel = attackerLevel, LevelRange = RangeForScene(SceneManager.GetActiveScene().name) });
            return "[Erenshor PvP] plan defenders=" + defenders + " attackers=" + attackers + " result=" + result.ToString().ToLowerInvariant() + "; simulation only, no Sims spawned.";
        }

        // `/epvp arranged on|off` and `/epvp ambush on|off`. The bare form reports state.
        // The two paths differ in consent, so every reply says which one it is.
        private static string SetConsentSwitch(string option, bool arranged)
        {
            string keyword = arranged ? "arranged" : "ambush";
            string name = arranged ? "Arranged challenges" : "Wild ambushes";
            string[] parts = option.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 1)
                return "[Erenshor PvP] " + name + ": " + (CurrentConsentState(arranged) ? "on" : "off") + ". " + ConsentHint(arranged) + MasterHint();
            if (parts.Length != 2 ||
                (!parts[1].Equals("on", StringComparison.OrdinalIgnoreCase) && !parts[1].Equals("off", StringComparison.OrdinalIgnoreCase)))
                return "[Erenshor PvP] Usage: /epvp " + keyword + " on|off";

            bool turnOn = parts[1].Equals("on", StringComparison.OrdinalIgnoreCase);
            if (arranged) SetArrangedEnabled(turnOn); else SetAmbushEnabled(turnOn);
            return "[Erenshor PvP] " + name + " " + (turnOn ? "on. " + ConsentHint(arranged) + MasterHint() : "off.");
        }

        private static bool CurrentConsentState(bool arranged) { return arranged ? ArrangedEnabled : AmbushEnabled; }

        private static string ConsentHint(bool arranged)
        {
            return arranged
                ? "You are always asked to Accept or Refuse before one starts."
                : "These start without asking; protected zones and the scene allowlist are the only limits.";
        }

        private static string MasterHint()
        {
            return Enabled ? string.Empty : " World PvP is off, so nothing happens until /epvp on.";
        }

        internal static string AmbushZonesText()
        {
            return "[Erenshor PvP] Ambush zone allowlist: " + (_ambushZones == null || string.IsNullOrWhiteSpace(_ambushZones.Value) ? "none" : _ambushZones.Value);
        }

        internal static string TeamText()
        {
            return HasPending && _pendingTeam != null
                ? "[Erenshor PvP] Pending: " + _pendingTeam.DescribeCompact()
                : PvpTemporaryCloneFactory.TeamStatus();
        }

        internal static void HideDebugTab()
        {
            if (_showDebugTab != null) { _showDebugTab.Value = false; PersistSettings(); }
            PvpPanel.SelectTab(PvpPanelTab.Status);
        }

        internal static void ToggleValidationLogging()
        {
            if (_validationLogging != null) { _validationLogging.Value = !_validationLogging.Value; PersistSettings(); }
        }

        private static string SetValidationLogging(string option)
        {
            string[] pieces = (option ?? string.Empty).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (pieces.Length > 1)
            {
                if (pieces[1].Equals("on", StringComparison.OrdinalIgnoreCase)) _validationLogging.Value = true;
                else if (pieces[1].Equals("off", StringComparison.OrdinalIgnoreCase)) _validationLogging.Value = false;
                else return "[Erenshor PvP] Usage: /epvp validation on|off";
                PersistSettings();
            }
            return "[Erenshor PvP] Detailed validation logging " + (ValidationLogging ? "ON." : "OFF. Core failures and results still log.");
        }

        // PvP uses only an off-map tracking profile. Any SimPlayer presently active in the
        // scene remains exclusively available to Practice Duels.
        private static bool TrySelectOffMap(int attackerCount, string preferredLeader, out PvpTeamPlan teamOut, out PvpEligibilityDecision decisionOut)
        {
            teamOut = null; decisionOut = PvpEligibilityDecision.InvalidSim;
            Character player = GameData.PlayerControl == null ? null : GameData.PlayerControl.Myself;
            if (!IsAlive(player)) { decisionOut = PvpEligibilityDecision.InvalidPlayer; return false; }
            if (GameData.SimMngr == null || GameData.SimMngr.Sims == null) return false;
            int playerLevel = player.MyStats == null ? 0 : player.MyStats.Level;
            int defenderCount; int defenderAverageLevel;
            CurrentDefenderParty(out defenderCount, out defenderAverageLevel);
            if (defenderAverageLevel <= 0) defenderAverageLevel = playerLevel;
            string currentScene = SceneManager.GetActiveScene().name;
            HashSet<SimPlayerTracking> active = new HashSet<SimPlayerTracking>();
            foreach (SimPlayer sim in UnityEngine.Object.FindObjectsOfType<SimPlayer>())
            {
                try { if (sim != null && sim.MySimTracking != null) active.Add(sim.MySimTracking); } catch { }
            }
            List<PvpOpponentProfile> eligible = new List<PvpOpponentProfile>();
            foreach (SimPlayerTracking tracking in GameData.SimMngr.Sims)
            {
                if (tracking == null || active.Contains(tracking) || TrackingIsInScene(tracking, currentScene) || string.IsNullOrWhiteSpace(tracking.SimName)) continue;
                if (defenderAverageLevel > 0 && tracking.Level > 0 && Math.Abs(defenderAverageLevel - tracking.Level) > RangeForScene(currentScene)) continue;
                float until; if (Cooldowns.TryGetValue(tracking.SimName, out until) && until > Time.unscaledTime) continue;
                PvpOpponentProfile profile = PvpOpponentProfile.FromTracking(tracking);
                if (profile != null) eligible.Add(profile);
            }
            if (eligible.Count == 0) return false;
            teamOut = PvpTeamPlanner.Build(defenderCount, defenderAverageLevel, eligible, UnityEngine.Random.Range(int.MinValue, int.MaxValue), attackerCount, preferredLeader);
            if (teamOut == null || teamOut.Members.Count == 0) return false;
            if (attackerCount >= 1 && teamOut.Members.Count != attackerCount) { teamOut = null; decisionOut = PvpEligibilityDecision.InvalidSim; return false; }
            PvpMatchDecision match = PvpMatchmakingPolicy.Evaluate(new PvpMatchInput
            {
                DefenderPartySize = defenderCount, AttackerPartySize = teamOut.Members.Count,
                DefenderAverageLevel = defenderAverageLevel, AttackerAverageLevel = teamOut.AverageLevel,
                LevelRange = RangeForScene(SceneManager.GetActiveScene().name)
            });
            if (match != PvpMatchDecision.Eligible) { decisionOut = PvpEligibilityDecision.LevelMismatch; teamOut = null; return false; }
            PvpDiagnostics.Log("match_plan defenders=" + defenderCount + "; defender_avg_level=" + defenderAverageLevel +
                "; attackers=" + teamOut.Members.Count + "; attacker_avg_level=" + teamOut.AverageLevel +
                "; level_range=" + RangeForScene(currentScene) + "; roster=" + teamOut.DescribeCompact());
            decisionOut = PvpEligibilityDecision.Eligible;
            return true;
        }

        internal static void Accept()
        {
            if (!HasPending) { ClearPending(); Say("[Erenshor PvP] That challenge expired."); return; }
            string id = _pendingMatchId; string name = _pendingName; PvpTeamPlan team = _pendingTeam; PvpEncounterFlavor flavor = _pendingFlavor; ClearPending(); ClosePanel();
            Publish("pvp_accepted", id, name, SceneManager.GetActiveScene().name, "arranged", flavor == null ? "party_match" : flavor.Motive);
            StartEncounter(team, id, name, PvpEncounterMode.Arranged, flavor);
        }

        private static void StartEncounter(PvpTeamPlan team, string id, string name, PvpEncounterMode mode, PvpEncounterFlavor flavor)
        {
            if (!_lifecycle.BeginSpawn(id))
            {
                Say("[Erenshor PvP] Encounter cancelled: lifecycle was not ready.");
                return;
            }
            MarkTeamCooldown(team, mode == PvpEncounterMode.Ambush ? 600f : 120f);
            string spawned = PvpTemporaryCloneFactory.SpawnTeam(team, id, mode, flavor == null ? string.Empty : flavor.Motive);
            if (spawned.IndexOf("spawned", StringComparison.OrdinalIgnoreCase) < 0)
            {
                PublishTerminalOnce("pvp_cancelled", id, name, SceneManager.GetActiveScene().name,
                    mode.ToString().ToLowerInvariant(), "proxy_spawn_failed");
                Say("[Erenshor PvP] Encounter cancelled: " + spawned);
                _lifecycle.BeginCleanup();
                _lifecycle.CompleteCleanup(Enabled);
                return;
            }
            string preparationReason;
            if (!PvpTemporaryCloneFactory.PrepareForCountdown(out preparationReason))
            {
                PvpDiagnostics.Warning("match_preparation_failed match=" + ShortMatch(id) + "; reason=" + preparationReason);
                Say("[Erenshor PvP] Encounter cancelled: attacker runtime preparation failed (" + preparationReason + ").");
                PvpTemporaryCloneFactory.Despawn("preparation_failed");
                return;
            }
            if (!_lifecycle.SpawnSucceeded())
            {
                PvpTemporaryCloneFactory.Despawn("countdown_state_failed");
                return;
            }
            if (mode == PvpEncounterMode.Arranged) BeginMatchCountdown(id);
            else ReleaseMatchAtGo(id, false); // Preserve wild-ambush immediacy; no visible 3-2-1 warning.
        }

        private static void BeginMatchCountdown(string matchId)
        {
            _countdownMatchId = matchId ?? string.Empty;
            _matchCountdownValue = 3;
            _matchCountdownNextAt = Time.unscaledTime + 1f;
            PvpDiagnostics.Log("countdown_started match=" + ShortMatch(_countdownMatchId) + "; attackers_held=true; defenders_held=true");
            Say("[Erenshor PvP] Opponents ready. Match begins in 3...");
            Say("[PvP] 3");
        }

        private static void TickMatchCountdown(float now)
        {
            if (_lifecycle.State != PvpMatchLifecycleState.Countdown) return;
            if (!PvpTemporaryCloneFactory.HasActiveTeam)
            {
                ResetMatchCountdown();
                _lifecycle.BeginCleanup();
                _lifecycle.CompleteCleanup(Enabled);
                return;
            }
            string holdReason;
            if (!PvpTemporaryCloneFactory.MaintainCountdownHold(out holdReason))
            {
                PvpDiagnostics.Warning("countdown_hold_failed match=" + ShortMatch(_countdownMatchId) + "; reason=" + holdReason);
                ResetMatchCountdown();
                PvpTemporaryCloneFactory.Despawn("countdown_hold_failed");
                return;
            }
            if (now < _matchCountdownNextAt) return;
            _matchCountdownValue--;
            _matchCountdownNextAt = now + 1f;
            if (_matchCountdownValue > 0)
            {
                Say("[PvP] " + _matchCountdownValue);
                return;
            }

            string matchId = _countdownMatchId;
            ReleaseMatchAtGo(matchId, true);
        }

        private static void ReleaseMatchAtGo(string matchId, bool announceGo)
        {
            if (!_lifecycle.Go())
            {
                ResetMatchCountdown();
                PvpTemporaryCloneFactory.Despawn("go_state_failed");
                return;
            }
            string started = PvpTemporaryCloneFactory.BeginLethalFight();
            if (!PvpCombatContainment.LethalFightActive)
            {
                PvpDiagnostics.Warning("match_go_failed match=" + ShortMatch(matchId) + "; reason=combat_start_failed");
                ResetMatchCountdown();
                PvpTemporaryCloneFactory.Despawn("combat_start_failed");
                Say(started);
                return;
            }
            ResetMatchCountdown();
            PvpDiagnostics.Log("match_go match=" + ShortMatch(matchId) + "; attackers_released=true; defenders_released=true; go_count=" + _lifecycle.GoTransitions);
            if (announceGo) Say("[PvP] GO");
            Say(started);
        }

        private static void ResetMatchCountdown()
        {
            _matchCountdownValue = 0;
            _matchCountdownNextAt = 0f;
            _countdownMatchId = string.Empty;
        }

        private static string ShortMatch(string matchId)
        {
            return string.IsNullOrEmpty(matchId) ? "none" : matchId.Substring(0, Math.Min(8, matchId.Length));
        }

        private static int CurrentDefenderCount()
        {
            int count; int averageLevel;
            CurrentDefenderParty(out count, out averageLevel);
            return count;
        }

        private static void CurrentDefenderParty(out int count, out int averageLevel)
        {
            count = 1;
            List<int> partyLevels = new List<int>();
            int playerLevel = 0;
            try
            {
                Character player = GameData.PlayerControl == null ? null : GameData.PlayerControl.Myself;
                if (player != null && player.MyStats != null) playerLevel = player.MyStats.Level;
                if (GameData.GroupMembers != null)
                {
                    foreach (SimPlayerTracking member in GameData.GroupMembers)
                    {
                        Character actor = member == null || member.MyAvatar == null || member.MyAvatar.MyStats == null
                            ? null : member.MyAvatar.MyStats.Myself;
                        if (actor == null || !actor.Alive) continue;
                        int level = actor.MyStats == null ? member.Level : actor.MyStats.Level;
                        if (level <= 0) level = member.Level;
                        partyLevels.Add(level);
                        count++;
                    }
                }
            }
            catch { }
            count = Math.Max(1, Math.Min(5, count));
            averageLevel = PvpMatchmakingPolicy.CalculateDefenderAverageLevel(playerLevel, partyLevels);
        }

        internal static void Refuse()
        {
            if (!string.IsNullOrEmpty(_pendingMatchId)) { MarkTeamCooldown(_pendingTeam, 120f); Publish("pvp_refused", _pendingMatchId, _pendingName, "", "refuse", ""); }
            if (!string.IsNullOrEmpty(_pendingName)) Say(_pendingName + ": all good.");
            ClearPending(); ClosePanel();
        }

        private static void MarkTeamCooldown(PvpTeamPlan team, float seconds)
        {
            if (team == null) return;
            float until = Time.unscaledTime + Math.Max(1f, seconds);
            foreach (PvpTeamMember member in team.Members)
                if (member != null && member.Profile != null && !string.IsNullOrWhiteSpace(member.Profile.Name)) Cooldowns[member.Profile.Name] = until;
        }

        private static void ClearPending() { _lifecycle.ClearPending(); _pendingName = string.Empty; _pendingTeam = null; _pendingMatchId = string.Empty; _pendingExpires = 0f; _pendingFlavor = null; }
        // Called only after the factory has released combat target/navigation ownership and its
        // temporary actor/spell collections. The duplicate-safe policy tolerates nested cleanup.
        internal static void EncounterCleaned() { ResetMatchCountdown(); _lifecycle.BeginCleanup(); _lifecycle.CompleteCleanup(Enabled); }
        private static bool IsAlive(Character value) { try { return value != null && value.gameObject != null && value.gameObject.activeInHierarchy && value.Alive; } catch { return false; } }
        // An avatar can disappear briefly while the native manager pools or respawns it. CurScene
        // remains the authoritative location signal during that gap, so a same-zone Sim must not
        // become eligible for lethal PvP merely because FindObjectsOfType cannot see its body.
        private static bool TrackingIsInScene(SimPlayerTracking tracking, string scene)
        {
            if (tracking == null || string.IsNullOrWhiteSpace(scene)) return false;
            try { return Normalize(tracking.CurScene) == Normalize(scene); }
            catch { return true; }
        }
        private static bool IsGameplayReady()
        {
            return SuiteUiPolicy.IsGameplayReady();
        }
        private static bool IsZoning() { try { return GameData.Zoning; } catch { return true; } }
        private static int RangeForScene(string scene) { return IsListed(scene, _highRiskZones.Value) ? Clamp(_highRiskRange.Value) : Clamp(_standardRange.Value); }
        private static int Clamp(int value) { return Math.Max(1, Math.Min(10, value)); }
        private static bool IsListed(string scene, string csv)
        {
            string normalized = Normalize(scene); if (normalized.Length == 0) return false;
            foreach (string item in (csv ?? string.Empty).Split(',')) if (Normalize(item) == normalized) return true;
            return false;
        }
        private static bool AmbushAllowed(string scene)
        {
            return Enabled && _ambushEnabled != null && _ambushEnabled.Value && !IsProtectedScene(scene) && IsListed(scene, _ambushZones == null ? string.Empty : _ambushZones.Value);
        }
        private static void ScheduleNextAmbush(float now)
        {
            int minimum = Math.Max(8, Math.Min(120, _ambushMinimumMinutes == null ? 15 : _ambushMinimumMinutes.Value));
            int maximum = Math.Max(minimum, Math.Min(240, _ambushMaximumMinutes == null ? 35 : _ambushMaximumMinutes.Value));
            _nextAmbush = now + UnityEngine.Random.Range(minimum * 60f, maximum * 60f + 1f);
        }
        private static string SetAmbushHere(string option)
        {
            string[] parts = option.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || (!parts[1].Equals("on", StringComparison.OrdinalIgnoreCase) && !parts[1].Equals("off", StringComparison.OrdinalIgnoreCase)))
                return "[Erenshor PvP] Usage: /epvp ambushhere on|off";
            return SetAmbushHere(parts[1].Equals("on", StringComparison.OrdinalIgnoreCase));
        }

        // Shared by the chat command and the panel's allow/stop button.
        internal static string SetAmbushHere(bool turnOn)
        {
            string scene = SceneManager.GetActiveScene().name;
            if (turnOn && IsProtectedScene(scene)) return "[Erenshor PvP] " + scene + " is protected and cannot become an ambush zone.";
            List<string> zones = new List<string>();
            foreach (string item in (_ambushZones.Value ?? string.Empty).Split(','))
                if (!string.IsNullOrWhiteSpace(item) && Normalize(item) != Normalize(scene)) zones.Add(item.Trim());
            if (turnOn) zones.Add(scene);
            _ambushZones.Value = string.Join(", ", zones.ToArray());
            PersistSettings();
            if (turnOn && _nextAmbush < Time.unscaledTime + 300f) _nextAmbush = Time.unscaledTime + 300f;
            return "[Erenshor PvP] Wild ambushes " + (turnOn ? "allowed" : "disabled") + " in " + scene + ".";
        }
        private static bool IsProtectedScene(string scene)
        {
            if (IsListed(scene, _protectedZones.Value)) return true;
            string normalized = Normalize(scene);
            if (normalized.Length == 0) return true;
            // Hard safety floor for loading/character-select, tutorials, and city hubs.
            return normalized.Contains("tutorial") || normalized.Contains("characterselect") ||
                   normalized.Contains("portazure") || normalized.Contains("stowawaysstep") ||
                   normalized.Contains("island") || normalized.Contains("city");
        }
        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            char[] result = new char[value.Length]; int count = 0;
            foreach (char c in value) if (char.IsLetterOrDigit(c)) result[count++] = char.ToLowerInvariant(c);
            return new string(result, 0, count);
        }
        private static void Publish(string type, string id, string opponent, string zone, string decision, string reason)
        {
            // Terminal events carry PvP's own verdict. Non-terminal events (challenge, ambush
            // start) have a motive rather than an outcome, so classifying them would be nonsense.
            bool terminal = type == "pvp_cancelled" || type == "pvp_match_completed";
            ErenshorPvpEvents.Publish(new PvpSemanticEvent(type, id, opponent, zone, decision, reason,
                terminal ? ErenshorPvpApi.ClassifyOutcome(reason) : string.Empty));
        }
        private static void PublishTerminalOnce(string type, string id, string opponent, string zone, string decision, string reason)
        {
            string classification = ErenshorPvpApi.ClassifyOutcome(reason);
            if (ErenshorPvpApi.TryRecordResult(id, opponent, reason, decision, classification))
                ErenshorPvpEvents.Publish(new PvpSemanticEvent(type, id, opponent, zone, decision, reason, classification));
        }
        internal static void Say(string value) { try { UpdateSocialLog.LogAdd(value, "lightblue"); } catch { try { UpdateSocialLog.LogAdd(value); } catch { } } }
        private static void Diagnostic(string value) { PvpDiagnostics.Warning(value); }
    }
}
