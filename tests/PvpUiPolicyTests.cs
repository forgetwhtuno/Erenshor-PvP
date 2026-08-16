using System;
using ErenshorPvP;

internal static class PvpUiPolicyTests
{
    private static int Main()
    {
        try
        {
            Assert(PvpUiGeometry.RunSelfTests().StartsWith("PASS", StringComparison.Ordinal), "normalized retained position policy");
            Assert(SuiteLauncherPolicy.RunSelfTests().StartsWith("PASS", StringComparison.Ordinal), "mandatory launcher fallback policy");
            Assert(PvpHubPresentation.Build(true, false) == "Enabled | Idle", "idle hub status exact");
            Assert(PvpHubPresentation.Build(true, true) == "Enabled | Match active", "active hub status exact");
            Assert(PvpHubPresentation.Build(false, false).Length < 240, "hub status remains bounded");
            Assert(PvpUiPresentation.ToggleLabel("PvP Enabled", true) == "PvP Enabled [ON]", "explicit PvP ON label");
            Assert(PvpUiPresentation.ToggleLabel("Arranged Challenges", false) == "Arranged Challenges [OFF]", "explicit arranged OFF label");
            Assert(PvpUiPresentation.ToggleLabel("Wild Ambushes", true) == "Wild Ambushes [ON]", "explicit ambush ON label");
            Assert(PvpUiPresentation.RunSelfTests().StartsWith("PASS", StringComparison.Ordinal), "explicit toggle presentation policy");
            string uiState = PvpUiStatePolicy.Build("pvp", true, 520, 4.25d);
            Assert(Field(uiState, "module") == "pvp" && Field(uiState, "open") == "true" &&
                Field(uiState, "closeable") == "true", "PvP ui.state advertises visual close contract");
            Assert(Field(uiState, "sortOrder") == "520" && Field(uiState, "activated") == "4.25",
                "PvP ui.state reports deterministic stacking metadata");
            string boundedState = PvpUiStatePolicy.Build("pvp", true, 50000, double.NaN);
            Assert(Field(boundedState, "sortOrder") == "10000" && Field(boundedState, "activated") == "0",
                "PvP ui.state bounds malformed ordering values");
            Assert(PvpProxyStartupPolicy.InvariantPasses(true, true, true, true, true, true, true, true, false), "proxy startup invariant accepts complete synthetic graph");
            Assert(!PvpProxyStartupPolicy.InvariantPasses(true, true, true, true, false, true, true, true, false), "proxy startup invariant rejects broken Character-to-NPC link");
            Assert(!PvpProxyStartupPolicy.InvariantPasses(true, true, true, true, true, true, true, true, true), "proxy startup invariant rejects persistent Sim identity");
            Assert(!PvpProxyStartupPolicy.ShouldRunNativeNpcStart(true, true), "live native NPC.Start bypass is temporary-proxy-only");
            Assert(PvpProxyStartupPolicy.ShouldRunNativeNpcStart(false, true), "ordinary native NPC.Start remains untouched");
            Assert(PvpProxyStartupPolicy.ShouldRunNativeNpcStart(true, false), "resource-prefab proxy retains unproven native Start lifecycle");
            Assert(PvpProxyStartupPolicy.RewardBoundaryPasses(true, true, true, true, true, true, true), "reward boundary accepts fully suppressed proxy");
            Assert(!PvpProxyStartupPolicy.RewardBoundaryPasses(false, true, true, true, true, true, true), "reward boundary rejects unreadable/nonzero borrowed XP");
            Assert(!PvpProxyStartupPolicy.RewardBoundaryPasses(true, true, true, true, true, false, true), "reward boundary rejects native loot gold");
            Assert(PvpProxyStartupPolicy.ZeroHealingAssessment(0, 0, 0, 0) == "expected_no_heal_loadout", "zero healing is expected with no heal-capable attackers");
            Assert(PvpProxyStartupPolicy.ZeroHealingAssessment(1, 0, 0, 0) == "heal_ai_not_evaluated", "heal-capable roster without heal checks is diagnostic");
            Assert(PvpProxyStartupPolicy.ZeroHealingAssessment(1, 3, 0, 0) == "heal_capable_but_no_cast_started", "heal checks without casts remain diagnostic");
            Assert(PvpProxyStartupPolicy.ZeroHealingAssessment(1, 3, 2, 0) == "heal_capable_casting_observed_no_effective_heal", "casting without healing remains diagnostic");
            PvpPointerOwnershipState pointer = new PvpPointerOwnershipState();
            Assert(pointer.PointerDown() && pointer.OwnsPointer && !pointer.IsDragging, "PvP drag owns input at pointer-down before threshold");
            Assert(!pointer.PointerDown(), "repeated pointer-down does not double-acquire");
            Assert(!pointer.BeginDrag() && pointer.IsDragging, "begin-drag reuses existing pointer ownership");
            Assert(pointer.Release() && !pointer.OwnsPointer && !pointer.IsDragging, "pointer release clears ownership and drag state");
            Assert(!pointer.Release(), "repeated release is idempotent");
            PvpPointerOwnershipState recovered = new PvpPointerOwnershipState();
            Assert(recovered.BeginDrag() && recovered.OwnsPointer && recovered.IsDragging, "begin-drag can recover a missed pointer-down callback");
            Assert(recovered.Release(), "recovered gesture releases cleanly");
            for (int i = 0; i < 20; i++)
            {
                PvpPointerOwnershipState cycle = new PvpPointerOwnershipState();
                Assert(cycle.PointerDown(), "cycle acquires input");
                cycle.BeginDrag();
                Assert(cycle.Release() && !cycle.OwnsPointer && !cycle.IsDragging, "repeated open/drag/close cycle leaves no stuck ownership");
            }
            PvpMatchLifecyclePolicy lifecycle = new PvpMatchLifecyclePolicy(false);
            Assert(lifecycle.State == PvpMatchLifecycleState.Disabled, "disabled lifecycle starts inert");
            lifecycle.SetEnabled(true);
            Assert(lifecycle.State == PvpMatchLifecycleState.Ready, "enable makes a fresh match ready");
            Assert(lifecycle.Queue("match-a") && lifecycle.State == PvpMatchLifecycleState.PendingChallenge, "challenge setup owns one pending match");
            Assert(lifecycle.BeginSpawn("match-a") && lifecycle.State == PvpMatchLifecycleState.Spawning, "accept advances pending match to spawn");
            lifecycle.BeginCleanup(); lifecycle.CompleteCleanup(true);
            Assert(lifecycle.State == PvpMatchLifecycleState.Ready && lifecycle.MatchId == string.Empty, "failed spawn cleans to fresh ready state");
            Assert(lifecycle.BeginSpawn("match-b") && lifecycle.SpawnSucceeded() && lifecycle.State == PvpMatchLifecycleState.Active, "valid match becomes active once");
            Assert(!lifecycle.BeginSpawn("match-c"), "active match rejects duplicate attacker group");
            lifecycle.BeginCleanup(); lifecycle.CompleteCleanup(true);
            Assert(lifecycle.State == PvpMatchLifecycleState.Ready, "terminal victory or loss cleanup permits immediate second match");
            Assert(lifecycle.BeginSpawn("match-c") && lifecycle.SpawnSucceeded(), "repeated match is a fresh lifecycle");
            lifecycle.BeginCleanup(); lifecycle.CompleteCleanup(false);
            Assert(lifecycle.State == PvpMatchLifecycleState.Disabled && lifecycle.MatchId == string.Empty, "zone/disable cleanup releases active ownership without restart");
            Console.WriteLine("PvpUiPolicyTests: PASS"); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine("PvpUiPolicyTests: FAIL " + ex.Message); return 1; }
    }
    private static string Field(string line, string key)
    {
        string[] pairs = (line ?? string.Empty).Split('&');
        for (int i = 0; i < pairs.Length; i++)
        {
            int eq = pairs[i].IndexOf('=');
            if (eq <= 0) continue;
            if (pairs[i].Substring(0, eq) == key) return pairs[i].Substring(eq + 1);
        }
        return string.Empty;
    }

    private static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
