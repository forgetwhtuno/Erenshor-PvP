using System;

namespace ErenshorPvP
{
    public sealed class PvpControlState
    {
        public bool GameplayReady;
        public bool Enabled;
        public bool ArrangedEnabled;
        public bool AmbushEnabled;
        public bool ShowLauncher;
        public bool ProtectedHere;
        public bool AmbushAllowedHere;
        public string Scene;
        public bool PanelOpen;
        public bool EncounterActive;
        public string PendingOpponent;
        public int PendingSecondsLeft;
    }

    public static class PvpControlApi
    {
        public const int ApiVersion = 1;
        public const string ModuleId = "pvp";
        public static bool HasDedicatedPanel { get { return true; } }
        public static bool IsPanelOpen { get { return PvpController.PanelOpen; } }
        public static PvpControlState GetBasicState()
        {
            PvpControlState state = new PvpControlState();
            state.GameplayReady = SuiteUiPolicy.IsGameplayReady();
            state.Enabled = PvpController.Enabled; state.ArrangedEnabled = PvpController.ArrangedEnabled; state.AmbushEnabled = PvpController.AmbushEnabled; state.ShowLauncher = PvpController.ShowLauncherPreference;
            state.ProtectedHere = PvpController.IsProtectedHere; state.AmbushAllowedHere = PvpController.AmbushAllowedHere; state.Scene = PvpController.CurrentScene;
            state.PanelOpen = PvpController.PanelOpen; state.EncounterActive = PvpTemporaryCloneFactory.HasActiveTeam; state.PendingOpponent = PvpController.PendingName; state.PendingSecondsLeft = PvpController.PendingSecondsLeft;
            return state;
        }
        public static string GetStatus() { return PvpController.HubStatus(); }
        public static bool OpenPanel() { if (!SuiteUiPolicy.IsGameplayReady()) return false; PvpController.RequestOpenPanel(); return true; }
        public static bool ClosePanel() { PvpController.ClosePanel(); return !PvpController.PanelOpen; }
        public static bool ResetPanelPosition() { PvpController.ResetPanelPosition(); return true; }
        public static bool ResetLauncherPosition() { PvpController.ResetLauncherPosition(); return true; }
        public static bool TogglePanel() { if (!SuiteUiPolicy.IsGameplayReady()) return false; if (PvpController.PanelOpen) PvpController.RequestClosePanel(); else PvpController.RequestOpenPanel(); return true; }
        public static bool SetShowLauncher(bool visible) { PvpController.SetShowLauncherPreference(visible); return true; }
        public static bool AcceptPending() { if (!PvpController.HasPending) return false; PvpController.Accept(); return true; }
        public static bool RefusePending() { if (!PvpController.HasPending) return false; PvpController.Refuse(); return true; }
        public static string FleeEncounter() { return PvpTemporaryCloneFactory.HasActiveTeam ? PvpTemporaryCloneFactory.Flee() : "No active PvP encounter."; }
        public static string SetAmbushHere(bool enabled) { return PvpController.SetAmbushHere(enabled); }
        public static bool AdjustAmbushChance(int delta) { PvpController.AdjustAmbushChance(delta); return true; }
        public static bool AdjustAmbushMinimum(int delta) { PvpController.AdjustAmbushMinimum(delta); return true; }
        public static bool AdjustAmbushMaximum(int delta) { PvpController.AdjustAmbushMaximum(delta); return true; }
        public static bool ToggleValidationLogging() { PvpController.ToggleValidationLogging(); return true; }
        public static bool HideDebugTab() { PvpController.HideDebugTab(); return true; }
        public static bool SetEnabled(bool enabled) { if (!SuiteUiPolicy.IsGameplayReady()) return false; PvpController.SetEnabled(enabled); return true; }
        public static bool SetArrangedEnabled(bool enabled) { PvpController.SetArrangedEnabled(enabled); return true; }
        public static bool SetAmbushEnabled(bool enabled) { PvpController.SetAmbushEnabled(enabled); return true; }
    }
}
