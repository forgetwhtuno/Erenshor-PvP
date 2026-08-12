using System;
using System.Collections.Generic;

namespace ErenshorPvP
{
    internal enum PvpMatchDecision
    {
        Eligible, InvalidPartySize, OutsideLevelRange, SoloAttackerTooWeak,
        FullPartyRequired, AttackerPartyTooWeak
    }

    internal struct PvpMatchInput
    {
        internal int DefenderPartySize;
        internal int AttackerPartySize;
        internal int DefenderAverageLevel;
        internal int AttackerAverageLevel;
        internal int LevelRange;
    }

    // This is a pure policy only. It deliberately does not spawn, group, or control Sims.
    internal static class PvpMatchmakingPolicy
    {
        internal static int CalculateDefenderAverageLevel(int playerLevel, IList<int> activePartyLevels)
        {
            int total = Math.Max(0, playerLevel);
            int count = playerLevel > 0 ? 1 : 0;
            if (activePartyLevels != null)
            {
                foreach (int level in activePartyLevels)
                {
                    if (level <= 0) continue;
                    total += level;
                    count++;
                }
            }
            return count == 0 ? 0 : (int)Math.Round((double)total / count);
        }

        internal static PvpMatchDecision Evaluate(PvpMatchInput input)
        {
            if (input.DefenderPartySize < 1 || input.DefenderPartySize > 5 || input.AttackerPartySize < 1 || input.AttackerPartySize > 5)
                return PvpMatchDecision.InvalidPartySize;
            int range = Math.Max(1, Math.Min(10, input.LevelRange));
            if (input.DefenderAverageLevel > 0 && input.AttackerAverageLevel > 0 && Math.Abs(input.DefenderAverageLevel - input.AttackerAverageLevel) > range)
                return PvpMatchDecision.OutsideLevelRange;
            if (input.DefenderPartySize == 2 && input.AttackerPartySize == 1 && input.AttackerAverageLevel < input.DefenderAverageLevel + 2)
                return PvpMatchDecision.SoloAttackerTooWeak;
            if (input.DefenderPartySize >= 4 && input.AttackerPartySize != 5)
                return PvpMatchDecision.FullPartyRequired;
            if (input.DefenderPartySize >= 4 && input.AttackerAverageLevel < input.DefenderAverageLevel)
                return PvpMatchDecision.AttackerPartyTooWeak;
            return PvpMatchDecision.Eligible;
        }

        internal static string RunSelfTests()
        {
            if (CalculateDefenderAverageLevel(10, new[] { 12, 14 }) != 12) return "FAIL defender average";
            if (CalculateDefenderAverageLevel(10, new[] { 0, -1 }) != 10) return "FAIL defender average invalid member";
            if (CalculateDefenderAverageLevel(0, null) != 0) return "FAIL defender average unavailable";
            PvpMatchInput value = new PvpMatchInput { DefenderPartySize = 1, AttackerPartySize = 3, DefenderAverageLevel = 10, AttackerAverageLevel = 11, LevelRange = 3 };
            if (Evaluate(value) != PvpMatchDecision.Eligible) return "FAIL match solo";
            value.DefenderPartySize = 2; value.AttackerPartySize = 1; value.AttackerAverageLevel = 11;
            if (Evaluate(value) != PvpMatchDecision.SoloAttackerTooWeak) return "FAIL match duo";
            value.DefenderPartySize = 5; value.AttackerPartySize = 4; value.DefenderAverageLevel = 10; value.AttackerAverageLevel = 10;
            if (Evaluate(value) != PvpMatchDecision.FullPartyRequired) return "FAIL match full";
            return "PASS pvp matchmaking";
        }
    }
}
