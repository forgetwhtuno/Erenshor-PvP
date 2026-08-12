using System;

namespace ErenshorPvP
{
    internal enum PvpEligibilityDecision
    {
        Eligible, Disabled, ProtectedZone, InvalidPlayer, InvalidSim, RemoteHuman,
        WrongScene, TooFar, Dead, PartyMember, Occupied, LevelMismatch, Cooldown
    }

    internal struct PvpPolicyInput
    {
        internal bool Enabled;
        internal bool ProtectedZone;
        internal bool PlayerValid;
        internal bool SimValid;
        internal bool RemoteHuman;
        internal bool SameScene;
        internal bool PartyMember;
        internal bool Occupied;
        internal bool Alive;
        internal bool Cooldown;
        internal float Distance;
        internal int PlayerLevel;
        internal int SimLevel;
        internal int LevelRange;
    }

    internal static class PvpPolicy
    {
        internal static PvpEligibilityDecision Evaluate(PvpPolicyInput input)
        {
            if (!input.Enabled) return PvpEligibilityDecision.Disabled;
            if (input.ProtectedZone) return PvpEligibilityDecision.ProtectedZone;
            if (!input.PlayerValid) return PvpEligibilityDecision.InvalidPlayer;
            if (!input.SimValid) return PvpEligibilityDecision.InvalidSim;
            if (input.RemoteHuman) return PvpEligibilityDecision.RemoteHuman;
            if (!input.SameScene) return PvpEligibilityDecision.WrongScene;
            if (input.PartyMember) return PvpEligibilityDecision.PartyMember;
            if (!input.Alive) return PvpEligibilityDecision.Dead;
            if (input.Occupied) return PvpEligibilityDecision.Occupied;
            if (input.Distance > 25f) return PvpEligibilityDecision.TooFar;
            if (input.Cooldown) return PvpEligibilityDecision.Cooldown;
            if (input.PlayerLevel > 0 && input.SimLevel > 0 &&
                Math.Abs(input.PlayerLevel - input.SimLevel) > Math.Max(1, Math.Min(10, input.LevelRange)))
                return PvpEligibilityDecision.LevelMismatch;
            return PvpEligibilityDecision.Eligible;
        }

        internal static string Token(PvpEligibilityDecision value) { return value.ToString().ToLowerInvariant(); }

        internal static string RunSelfTests()
        {
            PvpPolicyInput good = new PvpPolicyInput { Enabled = true, PlayerValid = true, SimValid = true,
                SameScene = true, Alive = true, Distance = 10f, PlayerLevel = 10, SimLevel = 12, LevelRange = 3 };
            if (Evaluate(good) != PvpEligibilityDecision.Eligible) return "FAIL pvp eligible";
            good.ProtectedZone = true;
            if (Evaluate(good) != PvpEligibilityDecision.ProtectedZone) return "FAIL pvp protected";
            good.ProtectedZone = false; good.RemoteHuman = true;
            if (Evaluate(good) != PvpEligibilityDecision.RemoteHuman) return "FAIL pvp remote";
            good.RemoteHuman = false; good.SimLevel = 20;
            if (Evaluate(good) != PvpEligibilityDecision.LevelMismatch) return "FAIL pvp level";
            good.SimLevel = 10; good.PartyMember = true;
            if (Evaluate(good) != PvpEligibilityDecision.PartyMember) return "FAIL pvp party";
            good.PartyMember = false; good.Occupied = true;
            if (Evaluate(good) != PvpEligibilityDecision.Occupied) return "FAIL pvp occupied";
            good.Occupied = false; good.Cooldown = true;
            if (Evaluate(good) != PvpEligibilityDecision.Cooldown) return "FAIL pvp cooldown";
            return "PASS pvp policy";
        }
    }
}
