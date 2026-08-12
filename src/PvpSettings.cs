using System;
using Lunaris.Config;

namespace ErenshorPvP
{
    // Thin native-Lunaris settings holder plus a small ConfigEntry<T>-compatible wrapper so
    // PvpController/PvpRewardService/PvpRecordService keep their existing .Value call sites
    // unchanged after the BepInEx ConfigFile.Bind migration. All 34 existing settings are
    // preserved verbatim (section/key/default/description) across their original three owning
    // classes; only the storage mechanism changed.
    internal sealed class PvpConfigEntry<T>
    {
        private readonly Func<T> _get;
        private readonly Action<T> _set;

        internal PvpConfigEntry(Func<T> get, Action<T> set)
        {
            _get = get;
            _set = set;
        }

        internal T Value
        {
            get { return _get(); }
            set { _set(value); }
        }
    }

    internal sealed class PvpSettings
    {
        [Config("Enabled", "PvP", "Enable off-map PvP party challenges and lethal proxy combat outside protected zones.")]
        public bool PvpEnabled = false;

        [Config("ArrangedChallenges", "PvP", "Allow consensual arranged challenges. These are the only PvP that asks first: you always get an Accept or Refuse prompt before one starts. Requires the main PvP toggle.")]
        public bool ArrangedChallenges = true;

        [Config("OfferCooldownMinutes", "PvP", "Global cooldown between incoming arranged offers or ambushes, clamped to 2-60 minutes.")]
        public int OfferCooldownMinutes = 12;

        [Config("Enabled", "Ambush", "Allow rare non-consensual attacks while the main PvP toggle is on. These never prompt; they simply begin. Protected zones and the scene allowlist are the only limits.")]
        public bool AmbushEnabled = true;

        [Config("Zones", "Ambush", "Exact scene allowlist for wild ambushes. Empty means no automatic ambushes.")]
        public string AmbushZones = "Faerie's Brake, Hidden Hills, Bonepits, Krakengard";

        [Config("MinimumMinutes", "Ambush", "Minimum minutes between natural ambush opportunities, clamped to 8-120.")]
        public int AmbushMinimumMinutes = 15;

        [Config("MaximumMinutes", "Ambush", "Maximum minutes between natural ambush opportunities, clamped to the minimum-240.")]
        public int AmbushMaximumMinutes = 35;

        [Config("OpportunityChancePercent", "Ambush", "Chance that an eligible ambush opportunity becomes an ambush (5-100). Failed opportunities reschedule the full interval.")]
        public int AmbushOpportunityChancePercent = 50;

        [Config("ProtectedZones", "PvP", "Protected scene names. Matching ignores spaces and punctuation.")]
        public string ProtectedZones = "Port Azure, Stowaway's Step, Island Tomb, Tutorial, Character Select";

        [Config("HighRiskZones", "PvP", "Exact scene names using the wider level range.")]
        public string HighRiskZones = "";

        [Config("StandardLevelRange", "PvP", "Ordinary-zone level range, clamped to 1-10.")]
        public int StandardLevelRange = 3;

        [Config("HighRiskLevelRange", "PvP", "High-risk-zone level range, clamped to 1-10.")]
        public int HighRiskLevelRange = 5;

        [Config("PanelOffsetX", "UI", "Persisted horizontal offset from the default upper-right panel position, matching the Party Tools convention. Updated when the panel finishes moving.")]
        public float PanelOffsetX = 0f;

        [Config("PanelOffsetY", "UI", "Persisted vertical offset from the default position below the upper-right minimap area. Updated when the panel finishes moving.")]
        public float PanelOffsetY = 0f;

        [Config("ShowTestTab", "UI", "Show the hidden TEST tab with force/verify/diagnose controls. Toggle in game with /epvp debug.")]
        public bool ShowTestTab = false;

        [Config("ShowQuickToggle", "UI", "Show the compact PvP on/off switch beside the minimap.")]
        public bool ShowQuickToggle = true;

        [Config("FullView", "UI", "Open the panel with the tab bar and all detail views. When false the panel stays compact and shows only the master switch, zone safety, and anything awaiting a decision. Toggled by the panel's Full checkbox.")]
        public bool FullView = false;

        [Config("ValidationLogging", "Debug", "Temporary detailed PvP acceptance logging. Turn off after validation with /epvp validation off; core failures and final results remain logged.")]
        public bool ValidationLogging = true;

        [Config("Enabled", "Rewards", "Grant rewards only for a completed PvP proxy victory.")]
        public bool RewardsEnabled = true;

        [Config("XpFractionOfLevel", "Rewards", "Fraction of the current level's XP threshold awarded on a victory (0.01-0.50).")]
        public float XpFractionOfLevel = 0.50f;

        [Config("GoldPerTwoLevels", "Rewards", "Gold per two player levels, rounded up (1-100).")]
        public int GoldPerTwoLevels = 1;

        [Config("VictoryCooldownMinutes", "Rewards", "Minimum time between reward-bearing PvP victories (5-240 minutes).")]
        public int VictoryCooldownMinutes = 30;

        [Config("NextEligibleUtcTicks", "Rewards", "Internal anti-farm timestamp. Do not edit while a match is active.")]
        public long NextEligibleUtcTicks = 0L;

        [Config("CosmeticChancePercent", "Rewards", "Deprecated and ignored. Cosmetic rewards are disabled until a slot-safe native unlock API is verified.")]
        public int CosmeticChancePercent = 0;

        [Config("Wins", "Record", "Completed PvP victories.")]
        public int RecordWins = 0;

        [Config("Losses", "Record", "Completed PvP defeats.")]
        public int RecordLosses = 0;

        [Config("Escapes", "Record", "PvP matches ending by player flight or attacker retreat/disengage.")]
        public int RecordEscapes = 0;

        [Config("LastOpponent", "Record", "Last completed PvP opponent.")]
        public string LastOpponent = "";

        [Config("LastResult", "Record", "Last completed PvP result.")]
        public string LastResult = "";

        [Config("ArrangedWins", "Record", "Wins in accepted arranged PvP.")]
        public int ArrangedWins = 0;

        [Config("ArrangedLosses", "Record", "Losses in accepted arranged PvP.")]
        public int ArrangedLosses = 0;

        [Config("AmbushWins", "Record", "Wild ambushes survived.")]
        public int AmbushWins = 0;

        [Config("AmbushLosses", "Record", "Defeats in wild ambushes.")]
        public int AmbushLosses = 0;

        [Config("LastMode", "Record", "Last completed PvP encounter mode.")]
        public string LastMode = "";
    }
}
