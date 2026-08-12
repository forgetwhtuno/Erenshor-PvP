using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using UnityEngine;

namespace ErenshorPvP
{
    // Rewards are granted only after the verified temporary proxy is actually defeated.
    // They deliberately use the game's public reward/inventory entry points rather than
    // writing a character-save file.
    internal static class PvpRewardService
    {
        private static ConfigEntry<bool> _enabled;
        private static ConfigEntry<float> _xpFraction;
        private static ConfigEntry<int> _goldPerTwoLevels;
        private static ConfigEntry<int> _cooldownMinutes;
        private static ConfigEntry<long> _nextRewardUtcTicks;
        private static ConfigEntry<int> _cosmeticChancePercent;

        internal static void Initialize(ConfigFile config)
        {
            _enabled = config.Bind("Rewards", "Enabled", true, "Grant rewards only for a completed PvP proxy victory.");
            _xpFraction = config.Bind("Rewards", "XpFractionOfLevel", 0.50f, "Fraction of the current level's XP threshold awarded on a victory (0.01-0.50).");
            _goldPerTwoLevels = config.Bind("Rewards", "GoldPerTwoLevels", 1, "Gold per two player levels, rounded up (1-100).");
            _cooldownMinutes = config.Bind("Rewards", "VictoryCooldownMinutes", 30, "Minimum time between reward-bearing PvP victories (5-240 minutes).");
            _nextRewardUtcTicks = config.Bind("Rewards", "NextEligibleUtcTicks", 0L, "Internal anti-farm timestamp. Do not edit while a match is active.");
            _cosmeticChancePercent = config.Bind("Rewards", "CosmeticChancePercent", 0, "Deprecated and ignored. Cosmetic rewards are disabled until a slot-safe native unlock API is verified.");
        }

        // Panel accessors.
        internal static bool RewardsEnabled { get { return Enabled; } }
        internal static int XpPercent { get { return _xpFraction == null ? 0 : Mathf.RoundToInt(Mathf.Clamp(_xpFraction.Value, .01f, .50f) * 100f); } }
        internal static int GoldPerTwoLevels { get { return _goldPerTwoLevels == null ? 0 : Math.Max(1, Math.Min(100, _goldPerTwoLevels.Value)); } }
        internal static int CooldownMinutes { get { return _cooldownMinutes == null ? 0 : Math.Max(5, Math.Min(240, _cooldownMinutes.Value)); } }
        internal static int CosmeticChancePercent { get { return 0; } }

        // Whole minutes until the anti-farm cooldown releases; 0 means a victory pays out now.
        internal static int CooldownMinutesRemaining
        {
            get
            {
                if (_nextRewardUtcTicks == null) return 0;
                long remaining = _nextRewardUtcTicks.Value - DateTime.UtcNow.Ticks;
                if (remaining <= 0) return 0;
                return Math.Max(1, (int)Math.Ceiling(TimeSpan.FromTicks(remaining).TotalMinutes));
            }
        }

        // TransmogSlots are typed equipment positions, not a generic unlock inventory. Directly
        // inserting a random item can put a weapon in the chest slot and hide equipped armor.
        internal static string CosmeticSlotStatus
        {
            get { return "disabled"; }
        }

        internal static string Describe()
        {
            int minutes = Math.Max(5, Math.Min(240, _cooldownMinutes.Value));
            int percent = Mathf.RoundToInt(Mathf.Clamp(_xpFraction.Value, .01f, .50f) * 100f);
            return "victory_rewards=" + (Enabled ? (percent + "% level XP + level-based gold; " + minutes + "m cooldown") : "off") + "; cosmetics=disabled_pending_safe_api";
        }

        internal static string GrantVictory(Character player) { return GrantVictory(player, null); }

        internal static string GrantVictory(Character player, IList<PvpOpponentProfile> opponents)
        {
            if (!Enabled) return "[Erenshor PvP] Victory recorded. Rewards are disabled in config.";
            if (player == null || player.MyStats == null || GameData.PlayerInv == null)
                return "[Erenshor PvP] Victory recorded, but reward state was unavailable; no reward granted.";
            if (DateTime.UtcNow.Ticks < _nextRewardUtcTicks.Value)
                return "[Erenshor PvP] Victory recorded. Reward withheld by the anti-farm cooldown.";

            try
            {
                Stats stats = player.MyStats;
                int threshold = Math.Max(1, stats.ExperienceToLevelUp);
                int level = Math.Max(1, stats.Level);
                List<PvpOpponentProfile> defeated = opponents == null ? new List<PvpOpponentProfile>() : opponents.Where(x => x != null).ToList();
                int opponentCount = Math.Max(1, defeated.Count);
                float averageOpponentLevel = defeated.Count == 0 ? level : (float)defeated.Average(x => x.Level);
                float levelRisk = Mathf.Clamp(averageOpponentLevel / level, .65f, 1.35f);
                float partyRisk = Mathf.Clamp(.70f + (.12f * opponentCount), .82f, 1.30f);
                float riskScale = levelRisk * partyRisk;
                int xp = CalculateVictoryXp(threshold, _xpFraction.Value, riskScale);
                int gold = Math.Max(1, Mathf.RoundToInt(((level + 1) / 2f) * Math.Max(1, Math.Min(100, _goldPerTwoLevels.Value)) * riskScale));

                // false keeps this a fixed PvP award rather than a normal NPC kill subject to group modifiers.
                GameData.AddExperience(xp, false);
                GameData.PlayerInv.Gold += gold;
                GameData.PlayerInv.UpdatePlayerInventory();

                _nextRewardUtcTicks.Value = DateTime.UtcNow.AddMinutes(Math.Max(5, Math.Min(240, _cooldownMinutes.Value))).Ticks;
                return "[Erenshor PvP] Victory rewards (" + opponentCount + " attackers, avg level " + Mathf.RoundToInt(averageOpponentLevel) + "): +" + xp + " XP and +" + gold + " gold.";
            }
            catch (Exception ex)
            {
                return "[Erenshor PvP] Victory recorded, but rewards failed safely (" + ex.GetType().Name + ").";
            }
        }

        private static bool Enabled { get { return _enabled != null && _enabled.Value; } }

        // The configured fraction is a hard ceiling, not merely a baseline. A weaker-than-
        // expected encounter can pay less, but extra attackers or a small level advantage must
        // never turn the default "half a level" award into more than half a level.
        private static int CalculateVictoryXp(int levelThreshold, float configuredFraction, float riskScale)
        {
            int threshold = Math.Max(1, levelThreshold);
            float fraction = Mathf.Clamp(configuredFraction, .01f, .50f);
            int configuredMaximum = Math.Max(1, Mathf.FloorToInt(threshold * fraction));
            int scaled = Math.Max(1, Mathf.FloorToInt(configuredMaximum * Mathf.Clamp(riskScale, .10f, 1f)));
            return Math.Min(configuredMaximum, scaled);
        }

        internal static string RunSelfTests()
        {
            if (CalculateVictoryXp(3000, .50f, 1.30f) != 1500) return "FAIL pvp rewards: high-risk XP exceeded configured cap";
            if (CalculateVictoryXp(3000, .50f, .82f) != 1230) return "FAIL pvp rewards: low-risk XP was not reduced";
            if (CalculateVictoryXp(3000, .25f, 1f) != 750) return "FAIL pvp rewards: configured fraction ignored";
            if (CalculateVictoryXp(1, .01f, .10f) != 1) return "FAIL pvp rewards: minimum award invalid";
            return "PASS pvp rewards";
        }

    }
}
