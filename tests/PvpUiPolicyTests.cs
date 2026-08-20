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
            Assert(PvpWindowChromePolicy.RunSelfTests().StartsWith("PASS", StringComparison.Ordinal), "Forgotten Roads PvP header collapse policy");
            Assert(PvpWindowChromePolicy.ChevronPointsUp(false), "expanded PvP header points up to collapse");
            Assert(!PvpWindowChromePolicy.ChevronPointsUp(true), "collapsed PvP header points down to expand");
            Assert(Math.Abs(PvpWindowChromePolicy.PreserveTopBottomY(100f, 520f, 34f) - 586f) < .001f, "collapse preserves visual top edge");
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
            Assert(PvpProxyStartupPolicy.ShouldRunNativeNpcStart(true, true), "live-source clone receives its own native NPC.Start lifecycle");
            Assert(PvpProxyStartupPolicy.ShouldRunNativeNpcStart(false, true), "ordinary native NPC.Start remains untouched");
            Assert(PvpProxyStartupPolicy.ShouldRunNativeNpcStart(true, false), "resource-prefab proxy receives native Start lifecycle");
            Assert(!PvpNativeNavHealthPolicy.LaunchAloneIsHealthy(true), "nav coroutine handle alone is never health proof");
            Assert(!PvpNativeNavHealthPolicy.IsHealthy(true, true, true, false, false), "UpdateNav entry without completed first step is unhealthy");
            Assert(!PvpNativeNavHealthPolicy.IsHealthy(true, true, true, true, true), "first MoveNext/UpdateNav fault is unhealthy");
            Assert(PvpNativeNavHealthPolicy.IsHealthy(true, true, true, true, false), "native Start plus successful UpdateNav progression is healthy");
            Assert(PvpNativeNavHealthPolicy.CompleteNavFailure(5, 5), "all proxy nav faults are a complete nav failure");
            Assert(!PvpNativeNavHealthPolicy.CompleteNavFailure(5, 1), "partial nav fault is not complete team failure");
            Assert(PvpNativeNavHealthPolicy.NeedsPursuit(true, 12f, 3f), "out-of-range melee profile requires pursuit");
            Assert(!PvpNativeNavHealthPolicy.NeedsPursuit(true, 8f, 10f), "ranged profile already in range does not require artificial movement");
            Assert(PvpNativeNavHealthPolicy.PursuitSatisfied(true, true, false), "native destination attempt satisfies pursuit progression");
            Assert(PvpProxyStartupPolicy.MaintenanceStatePasses(true, true, true, true, true, true, true, true, true), "proxy maintenance invariant accepts NPC-side runtime state");
            Assert(!PvpProxyStartupPolicy.MaintenanceStatePasses(true, true, false, true, true, true, true, true, true), "proxy maintenance invariant rejects missing NPC.MyStats");
            Assert(!PvpProxyStartupPolicy.MaintenanceStatePasses(true, true, true, true, true, false, true, true, true), "proxy maintenance invariant rejects missing NameFlash");
            // Regression for the 5v5 that logged nameFlash=True / requiredRuntimeState=PASS and still threw
            // ~1,130 NPC.HandleNameTag NREs: NameFlash is not the field HandleNameTag dereferences, so a
            // proxy that satisfies NameFlash but lacks NamePlateTxt/NamePlateObject must NOT pass.
            Assert(!PvpProxyStartupPolicy.MaintenanceStatePasses(true, true, true, true, true, true, true, false, true), "proxy maintenance invariant rejects missing NamePlateTxt even when NameFlash is bound");
            Assert(!PvpProxyStartupPolicy.MaintenanceStatePasses(true, true, true, true, true, true, true, true, false), "proxy maintenance invariant rejects missing NamePlateObject even when NameFlash is bound");
            Assert(!PvpProxyStartupPolicy.MaintenanceStatePasses(false, true, true, true, true, true, true, true, true), "proxy maintenance invariant only applies to registered temporary proxies");
            Assert(PvpProxyStartupPolicy.ShouldInterceptMaintenance(true, false), "invalid temporary proxy is intercepted for terminal cleanup");
            Assert(!PvpProxyStartupPolicy.ShouldInterceptMaintenance(false, false), "vanilla NPC is never intercepted by PvP failsafe");
            Assert(!PvpProxyStartupPolicy.ShouldInterceptMaintenance(true, true), "valid temporary proxy keeps native maintenance");
            Assert(PvpProxyStartupPolicy.RewardBoundaryPasses(true, true, true, true, true, true, true), "reward boundary accepts fully suppressed proxy");
            Assert(!PvpProxyStartupPolicy.RewardBoundaryPasses(false, true, true, true, true, true, true), "reward boundary rejects unreadable/nonzero borrowed XP");
            Assert(!PvpProxyStartupPolicy.RewardBoundaryPasses(true, true, true, true, true, false, true), "reward boundary rejects native loot gold");
            Assert(PvpProxyStartupPolicy.ZeroHealingAssessment(0, 0, 0, 0) == "expected_no_heal_loadout", "zero healing is expected with no heal-capable attackers");
            Assert(PvpProxyStartupPolicy.ZeroHealingAssessment(1, 0, 0, 0) == "heal_ai_not_evaluated", "heal-capable roster without heal checks is diagnostic");
            Assert(PvpProxyStartupPolicy.ZeroHealingAssessment(1, 3, 0, 0) == "heal_capable_but_no_cast_started", "heal checks without casts remain diagnostic");
            Assert(PvpProxyStartupPolicy.ZeroHealingAssessment(1, 3, 2, 0) == "heal_capable_casting_observed_no_effective_heal", "casting without healing remains diagnostic");

            // 0.5.11 per-proxy ability-use observability. A zero-spell proxy (pure-melee loadout) is
            // a real, expected outcome and must not be reported as any kind of failure.
            Assert(PvpProxyStartupPolicy.ProxyAbilityUseAssessment(0, 0, 0, 0, 0, 0, 0, 0) == "no_class_abilities_loaded", "zero-spell proxy is reported accurately, not as a failure");
            // Spells loaded but the AI never even evaluated them (no decisions, no heal checks).
            Assert(PvpProxyStartupPolicy.ProxyAbilityUseAssessment(3, 0, 0, 0, 0, 0, 0, 0) == "ability_ai_not_evaluated", "loaded-but-unevaluated is distinguished from never-loaded");
            // AI evaluated (decisions occurred) but no StartSpell-family cast was ever observed:
            // distinguishes "spells loaded" from "spells actually started casting".
            Assert(PvpProxyStartupPolicy.ProxyAbilityUseAssessment(3, 0, 0, 2, 1, 0, 0, 0) == "ability_evaluated_no_cast_started", "evaluated-but-no-cast is distinguished from merely having spells loaded");
            // Cast started but neither damage nor healing landed.
            Assert(PvpProxyStartupPolicy.ProxyAbilityUseAssessment(3, 0, 0, 2, 1, 1, 0, 0) == "cast_started_no_effective_outcome", "cast without outcome remains diagnostic, not a pass/fail verdict");
            // Confirmed use via damage, and separately via healing.
            Assert(PvpProxyStartupPolicy.ProxyAbilityUseAssessment(3, 0, 0, 2, 1, 1, 40, 0) == "ability_use_confirmed", "effective damage confirms ability use");
            Assert(PvpProxyStartupPolicy.ProxyAbilityUseAssessment(0, 2, 4, 0, 0, 1, 0, 25) == "ability_use_confirmed", "effective healing confirms ability use");
            // A heal-capable proxy with heal checks but zero heals is distinguished from one that
            // never evaluated healing at all - this reuses ZeroHealingAssessment unchanged (already
            // covered above), and this proves the reuse composes correctly at the per-proxy call site.
            Assert(PvpProxyStartupPolicy.ZeroHealingAssessment(1, 0, 0, 0) == "heal_ai_not_evaluated" &&
                PvpProxyStartupPolicy.ZeroHealingAssessment(1, 5, 2, 0) == "heal_capable_casting_observed_no_effective_heal",
                "heal-checked-but-zero-heals stays distinguishable from never-evaluated at the per-proxy call site");

            Assert(PvpWorldCombatPolicy.RunSelfTests().StartsWith("PASS", StringComparison.Ordinal), "MMO-style world-combat expansion policy");
            Assert(!PvpWorldCombatPolicy.IsProtectedNonCombat(true, false, true, true, true, true, true), "local/world Sim identity outranks neutral NPC heuristics");
            Assert(!PvpWorldCombatPolicy.IsProtectedNonCombat(false, true, true, true, true, true, true), "owned/summoned pet identity outranks neutral NPC heuristics");
            Assert(PvpWorldCombatPolicy.IsProtectedNonCombat(false, false, true, false, false, false, false), "vendor is protected noncombat world actor");
            Assert(PvpWorldCombatPolicy.DecideAggro(true, false, false, false, false, false) == PvpInteractionDecision.AllowWorld, "proxy may join native world combat");
            Assert(PvpWorldCombatPolicy.DecideAggro(false, false, true, false, false, false) == PvpInteractionDecision.AllowWorld, "outside world actor may aggro PvP attacker");
            Assert(PvpWorldCombatPolicy.DecideDamage(true, false, false, false, false, false, false) == PvpInteractionDecision.AllowWorld, "outside world damage may hit defender");
            Assert(PvpWorldCombatPolicy.DecideDamage(false, true, false, false, false, false, false) == PvpInteractionDecision.AllowWorld, "outside world damage may hit attacker");
            Assert(PvpWorldCombatPolicy.DecideSpellStart(false, true, false, false, true, false, false, false) == PvpInteractionDecision.AllowMatch, "attacker untargeted AoE is admitted without proximity veto");
            Assert(PvpWorldCombatPolicy.DecideSpellStart(true, false, false, false, true, false, false, false) == PvpInteractionDecision.AllowMatch, "defender untargeted AoE is admitted without proximity veto");
            Assert(PvpWorldCombatPolicy.DecideDamage(false, false, true, false, false, false, true) == PvpInteractionDecision.Block, "participant damage to proven protected neutral is rejected narrowly");

            Assert(PvpPluginIdentityPolicy.ExactlyOneExpectedIdentity(new[] { "Lunaris", "ErenshorPvP", "OtherMod" }), "exactly one PvP plugin identity expected");
            Assert(!PvpPluginIdentityPolicy.ExactlyOneExpectedIdentity(new[] { "ErenshorPvP", "ErenshorPvP" }), "duplicate PvP plugin identity rejected");
            Assert(!PvpPluginIdentityPolicy.ExactlyOneExpectedIdentity(new[] { "Lunaris", "OtherMod" }), "missing PvP plugin identity rejected");

            Assert(!PvpCombatStartupPolicy.HasCombatEvidence(true, false, false, false, false, false, false, false), "native Update alone is not combat evidence");
            Assert(!PvpCombatStartupPolicy.HasCombatEvidence(false, true, false, false, false, false, false, false), "forced target alone is not combat evidence");
            Assert(PvpCombatStartupPolicy.HasCombatEvidence(true, true, false, false, false, false, false, false), "native target acquisition path counts only after Update");
            Assert(PvpCombatStartupPolicy.HasCombatEvidence(false, false, true, false, false, false, false, false), "native pursuit counts as combat active");
            Assert(PvpCombatStartupPolicy.HasCombatEvidence(false, false, false, true, false, false, false, false), "melee-only/combat decision counts as active");
            Assert(PvpCombatStartupPolicy.HasCombatEvidence(false, false, false, false, false, true, false, false), "spell attacker counts as active");
            Assert(PvpCombatStartupPolicy.HasCombatEvidence(false, false, false, false, true, false, false, false), "healer/support attacker counts as active");
            Assert(!PvpCombatStartupPolicy.ShouldFailInactive(true, false, 5.9f, 6f), "startup watchdog waits bounded window");
            Assert(PvpCombatStartupPolicy.ShouldFailInactive(true, false, 6f, 6f), "completely inert active team fails technically");
            Assert(!PvpCombatStartupPolicy.ShouldFailInactive(true, true, 99f, 6f), "engaged team never fails inert watchdog");
            Assert(PvpCombatStartupPolicy.IsTechnicalFailure("technical_failure_ai_inactive"), "technical failure token exact");
            Assert(!PvpCombatStartupPolicy.ShouldRecordCompetitiveResult("technical_failure_ai_inactive"), "technical failure receives no match/history credit");
            Assert(!PvpCombatStartupPolicy.CanGrantVictoryReward("technical_failure_ai_inactive", true), "technical failure grants zero reward");
            Assert(PvpCombatStartupPolicy.CanGrantVictoryReward("proxy_death", true), "legitimate victory remains reward eligible exactly once downstream");

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
            Assert(lifecycle.BeginSpawn("match-a") && lifecycle.State == PvpMatchLifecycleState.Preparing, "accept advances pending match to preparation");
            Assert(lifecycle.HoldAttackers && !lifecycle.DefenderMayAttackProxy && !lifecycle.CombatReleased, "preparation holds both attack permissions");
            Assert(lifecycle.SpawnSucceeded() && lifecycle.State == PvpMatchLifecycleState.Countdown, "runtime-ready attackers advance to countdown");
            Assert(lifecycle.HoldAttackers && !lifecycle.DefenderMayAttackProxy && !lifecycle.CombatReleased, "countdown holds both sides before GO");
            Assert(lifecycle.Go() && lifecycle.State == PvpMatchLifecycleState.Active, "GO alone transitions match active");
            Assert(lifecycle.GoTransitions == 1 && lifecycle.CombatReleased && !lifecycle.HoldAttackers && lifecycle.DefenderMayAttackProxy, "GO releases both sides exactly once");
            Assert(!lifecycle.Go() && lifecycle.GoTransitions == 1, "GO cannot run twice");
            Assert(!lifecycle.BeginSpawn("match-c"), "active match rejects duplicate attacker group");
            lifecycle.BeginCleanup(); lifecycle.CompleteCleanup(true);
            Assert(lifecycle.State == PvpMatchLifecycleState.Ready && lifecycle.MatchId == string.Empty, "terminal cleanup returns fresh ready state");
            Assert(lifecycle.BeginSpawn("match-b") && lifecycle.State == PvpMatchLifecycleState.Preparing, "second match starts without restart");
            Assert(lifecycle.SpawnSucceeded() && lifecycle.State == PvpMatchLifecycleState.Countdown, "second match reaches countdown");
            Assert(lifecycle.Go() && lifecycle.State == PvpMatchLifecycleState.Active && lifecycle.GoTransitions == 1, "second match gets one fresh GO");
            lifecycle.BeginCleanup();
            PvpMatchLifecycleState cleanupState = lifecycle.State;
            lifecycle.BeginCleanup();
            Assert(lifecycle.State == cleanupState, "cleanup begins idempotently");
            lifecycle.CompleteCleanup(false);
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
